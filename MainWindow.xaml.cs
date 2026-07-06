using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DeafDirectionalHelper.Audio;
using DeafDirectionalHelper.Hotkeys;
using DeafDirectionalHelper.Services;
using DeafDirectionalHelper.Settings;
using DeafDirectionalHelper.View;
using Forms = System.Windows.Forms;

namespace DeafDirectionalHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Multi-monitor support using target screen
        internal static Forms.Screen TargetScreen => GetTargetScreen(SettingsManager.Instance.Settings.Display.TargetMonitor);
        internal static double ScreenWidth => TargetScreen.WorkingArea.Width;
        internal static double ScreenHeight => TargetScreen.WorkingArea.Height;
        internal static double ScreenLeft => TargetScreen.WorkingArea.Left;
        internal static double ScreenTop => TargetScreen.WorkingArea.Top;
        internal static int AppWidth => SettingsManager.Instance.Settings.Bars.Width;

        private static Forms.Screen GetTargetScreen(int index)
        {
            var screens = Forms.Screen.AllScreens;
            if (index < 0 || index >= screens.Length)
                return Forms.Screen.PrimaryScreen!;
            return screens[index];
        }

        private readonly Speakers _speakers;
        private readonly SettingsManager _settingsManager;
        private readonly ProfileManager _profileManager;
        private readonly GameDetector _gameDetector;
        private readonly ToastHost _toastHost = new();
        private string? _profileBeforeAutoSwitch;

        private View.Overlays.OverlayWindow? _overlayWindow;
        private View.Settings.SettingsShell? _settingsWindow;
        private TrayFlyout? _trayFlyout;
        private Forms.NotifyIcon? _notifyIcon;

        private bool _isMonitoring = true;
        private CancellationTokenSource? _monitoringCts;
        private GlobalHotkeyManager? _hotkeyManager;
        private SignalDoctor? _signalDoctor;
        private SignalDoctorWindow? _signalDoctorWindow;

        public MainWindow()
        {
            InitializeComponent();
            Helpers.DarkChrome.Apply(this);

            _settingsManager = SettingsManager.Instance;
            _profileManager = ProfileManager.Instance;
            RevertStaleProfile();
            _speakers = new Speakers();

            // Signal doctor: catches "overlay armed but silent on the wrong device"
            _signalDoctor = new SignalDoctor(_speakers.Sessions, _speakers.Endpoint)
            {
                IsTrackedProcess = name => _profileManager.GetProfileForProcess(name) != null
            };
            _signalDoctor.MismatchDetected += OnAudioMismatchDetected;

            // Game detection: known games by process, unknown games by audible
            // session + fullscreen-ish foreground window (plan D7)
            _gameDetector = new GameDetector(_speakers.Sessions);
            _gameDetector.ActiveProcessChanged += OnActiveProcessChanged;
            _gameDetector.UnknownGameDetected += OnUnknownGameDetected;
            UpdateProcessMonitorWatchList();

            // Subscribe to profile changes to update watch list
            _profileManager.ProfilesChanged += (_, _) => UpdateProcessMonitorWatchList();
            _profileManager.ProfileActivated += OnProfileActivated;

            SetupSystemTray();
            ShowScreens();
            StartMonitoring();
            SetupHotkeys();

            // Start game detection
            _gameDetector.Start();

            // Hide main window (we use system tray)
            Hide();
            WindowState = WindowState.Minimized;
            ShowInTaskbar = false;

            // First run: wizard once, then live in the tray (plan D8)
            if (!_settingsManager.Settings.FirstRunCompleted)
            {
                new FirstRunWizard().ShowDialog();
                _overlayWindow?.ApplySettings();
                _toastHost.ShowInfo("Running in the tray — Ctrl+Shift+S opens settings");
            }
            else if (_settingsManager.Settings.General.StartMinimized)
            {
                _toastHost.ShowInfo("Running in the tray — Ctrl+Shift+S opens settings");
            }
            else
            {
                ShowSettings();
            }
        }

        /// <summary>
        /// The settings file remembers the last-active profile. If the app was
        /// closed while a game ran (or the machine rebooted), that game profile
        /// would stay active forever - revert to Default unless its process is
        /// actually running right now.
        /// </summary>
        private void RevertStaleProfile()
        {
            var active = _profileManager.ActiveProfile;
            if (active.IsDefault || string.IsNullOrEmpty(active.ProcessName))
                return;

            try
            {
                var processes = System.Diagnostics.Process.GetProcessesByName(active.ProcessName);
                var running = processes.Length > 0;
                foreach (var process in processes)
                    process.Dispose();
                if (running)
                    return;
            }
            catch
            {
                return; // can't tell - leave the profile alone
            }

            Console.WriteLine($"Profile '{active.Name}' was active but {active.ProcessName}.exe is not running - reverting to Default");
            _profileManager.ActivateProfile(_profileManager.GetDefaultProfile());
        }

        private void UpdateProcessMonitorWatchList()
        {
            var processNames = _profileManager.GetWatchedProcessNames();
            _gameDetector.UpdateWatchList(processNames);
        }

        private void OnAudioMismatchDetected(object? sender, MismatchEventArgs e)
        {
            // Tick runs on the dispatcher, so we're already on the UI thread.
            if (_signalDoctorWindow != null && _signalDoctorWindow.IsVisible)
                return;

            _signalDoctorWindow = new SignalDoctorWindow(e);
            _signalDoctorWindow.Closed += (_, _) =>
            {
                _signalDoctorWindow = null;
                _signalDoctor?.Suppress(e.GameProcess);
            };
            _signalDoctorWindow.Show();
            _signalDoctorWindow.Activate();
        }

        private void OnActiveProcessChanged(object? sender, string? processName)
        {
            // Follow-game capture tracks whichever profiled process is running
            _speakers.Endpoint.TrackedProcessName = processName;

            // Don't auto-switch if paused (settings window open) or disabled
            if (_profileManager.AutoSwitchPaused || !_profileManager.AutoSwitchEnabled)
                return;

            Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
            {
                AppProfile targetProfile;

                if (processName != null)
                {
                    var profile = _profileManager.GetProfileForProcess(processName);
                    targetProfile = profile ?? _profileManager.GetDefaultProfile();
                }
                else
                {
                    targetProfile = _profileManager.GetDefaultProfile();
                }

                if (targetProfile.Id == _profileManager.ActiveProfile.Id)
                    return;

                // Reverting to Default (game stopped) is always silent.
                var behavior = targetProfile.IsDefault
                    ? ProfileSwitchBehavior.Silent
                    : _settingsManager.Settings.ProfileSwitchBehavior;

                switch (behavior)
                {
                    case ProfileSwitchBehavior.Silent:
                        _profileManager.ActivateProfile(targetProfile);
                        break;

                    case ProfileSwitchBehavior.SwitchWithToast:
                        _profileBeforeAutoSwitch = _profileManager.ActiveProfile.Id;
                        _profileManager.ActivateProfile(targetProfile);
                        _toastHost.ShowProfileSwitched(targetProfile, onUndo: () =>
                        {
                            if (_profileBeforeAutoSwitch != null)
                                _profileManager.ActivateProfile(_profileBeforeAutoSwitch);
                        });
                        break;

                    case ProfileSwitchBehavior.AskFirst:
                        _toastHost.ShowAskSwitch(targetProfile,
                            onSwitch: () => _profileManager.ActivateProfile(targetProfile));
                        break;
                }
            });
        }

        private void OnUnknownGameDetected(object? sender, UnknownGameEventArgs e)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
            {
                var mergeTargets = _profileManager.Profiles.Where(p => !p.IsDefault).ToList();

                _toastHost.ShowUnknownGame(e.ProcessName, e.ExePath,
                    mergeTargets,
                    onMerge: profile =>
                    {
                        _profileManager.AddProcessToProfile(profile, e.ProcessName);
                        _toastHost.ShowInfo($"{e.ProcessName}.exe now activates the {profile.Name} profile");
                    },
                    onCreate: () =>
                    {
                        var editor = new ProfileEditorWindow { IsNewProfile = true };
                        editor.SetProfile(e.ProcessName, e.ExePath, false);
                        if (editor.ShowDialog() == true)
                        {
                            // Seeded from current settings + detected exe; auto-switch
                            // picks it up on the next detection poll.
                            var profile = _profileManager.CreateProfile(editor.ProfileName,
                                editor.ExePath ?? e.ExePath ?? e.ProcessName + ".exe");
                            if (editor.AdditionalProcessNames.Count > 0)
                                _profileManager.UpdateProfile(profile, profile.Name, profile.ExePath,
                                    editor.AdditionalProcessNames);
                        }
                    },
                    onIgnore: () =>
                    {
                        _settingsManager.Update(s =>
                        {
                            if (!s.IgnoredGames.Contains(e.ProcessName))
                                s.IgnoredGames.Add(e.ProcessName);
                        });
                    },
                    onExclude: () =>
                    {
                        _settingsManager.Update(s =>
                        {
                            if (!s.ExcludedPrograms.Contains(e.ProcessName, StringComparer.OrdinalIgnoreCase))
                                s.ExcludedPrograms.Add(e.ProcessName);
                        });
                    });
            });
        }

        private void OnProfileActivated(object? sender, AppProfile profile)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
            {
                // Update display mode based on new profile
                UpdateDisplayMode();

                // Update settings window if open (switch feedback now comes
                // from ToastHost per ProfileSwitchBehavior, not balloon tips)
                _settingsWindow?.OnProfileAutoSwitched(profile);
            });
        }

        private void SetupHotkeys()
        {
            _hotkeyManager = new GlobalHotkeyManager();
            _hotkeyManager.Initialize(this);

            _hotkeyManager.ToggleEnabledPressed += () =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, ToggleEnabled);
            };

            _hotkeyManager.ToggleModePressed += () =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, ToggleDisplayMode);
            };

            _hotkeyManager.ShowSettingsPressed += () =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, ShowSettings);
            };

            _hotkeyManager.ResetPositionsPressed += () =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, ResetPositions);
            };

            _hotkeyManager.ShowHotkeysPressed += () =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, ShowHotkeys);
            };

            _hotkeyManager.MoveModePressed += () =>
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, ToggleMoveMode);
            };

            if (_hotkeyManager.RegisterHotkeys())
            {
                Console.WriteLine("Global hotkeys registered successfully");
                Console.WriteLine("  Ctrl+Shift+R: Toggle enable/disable");
                Console.WriteLine("  Ctrl+Shift+M: Next overlay style");
                Console.WriteLine("  Ctrl+Shift+S: Show settings");
                Console.WriteLine("  Ctrl+Shift+P: Reset positions");
                Console.WriteLine("  Ctrl+Shift+H: Show hotkeys");
                Console.WriteLine("  Ctrl+Shift+E: Move mode");
            }
        }

        private void ToggleDisplayMode()
        {
            // Ctrl+Shift+M cycles the five overlay styles (plan D2)
            var current = _settingsManager.Settings.Bars.OverlayStyle;
            var next = (Settings.OverlayStyle)(((int)current + 1) % 5);

            _settingsManager.Update(s => s.Bars.OverlayStyle = next);
            _profileManager.SaveCurrentSettingsToProfile(_profileManager.ActiveProfile);
            _overlayWindow?.ApplySettings();
            _settingsWindow?.OnProfileAutoSwitched(_profileManager.ActiveProfile);

            var styleName = next switch
            {
                Settings.OverlayStyle.SideBars => "Side bars",
                Settings.OverlayStyle.RadarRing => "Radar ring",
                Settings.OverlayStyle.RingPing => "Ring ping",
                Settings.OverlayStyle.CompassStrip => "Compass",
                Settings.OverlayStyle.EdgeGlow => "Edge glow",
                _ => next.ToString()
            };
            _toastHost.ShowInfo($"Overlay style: {styleName}");
        }

        private void ToggleMoveMode()
        {
            if (_overlayWindow == null) return;

            if (_overlayWindow.IsInMoveMode)
            {
                _overlayWindow.ExitMoveMode(commit: true);
                return;
            }

            if (!_settingsManager.Settings.Display.Enabled)
            {
                _toastHost.ShowInfo("Turn the overlay on first (Ctrl+Shift+R)");
                return;
            }

            _overlayWindow.EnterMoveMode();
        }

        private void SetupSystemTray()
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = LoadTrayIcon(),
                Visible = true,
                Text = "DeafDirectionalHelper - Audio Visualizer"
            };

            // WPF flyout replaces the old WinForms ContextMenuStrip (design 2l)
            _trayFlyout = new TrayFlyout(
                onToggleEnabled: ToggleEnabled,
                onOpenSettings: ShowSettings,
                onNextStyle: ToggleDisplayMode,
                onResetPositions: ResetPositions,
                onSendFeedback: ShowFeedbackDialog,
                onExitConfirmed: ExitApplication);

            _notifyIcon.MouseUp += (_, e) =>
            {
                if (e.Button is Forms.MouseButtons.Left or Forms.MouseButtons.Right)
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal, () => _trayFlyout?.Toggle());
            };
            _notifyIcon.DoubleClick += (_, _) => ShowSettings();
        }

        private static System.Drawing.Icon LoadTrayIcon()
        {
            // Try to load custom icon from Icons folder
            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons", "app.ico");
            if (System.IO.File.Exists(iconPath))
            {
                return new System.Drawing.Icon(iconPath);
            }

            // Fallback to system icon
            return SystemIcons.Application;
        }

        private void ShowScreens()
        {
            _overlayWindow = new View.Overlays.OverlayWindow(_speakers);
            _overlayWindow.PositionChanged += OnIndicatorPositionChanged;
            UpdateScreenVisibility();
        }

        private void OnIndicatorPositionChanged(object? sender, EventArgs e)
        {
            // Update the settings window sliders when any indicator is dragged
            _settingsWindow?.RefreshIndicatorSliders();
        }

        private void ShowSettings()
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = new View.Settings.SettingsShell(_speakers);
                _settingsWindow.ExitRequested += (_, _) => ExitApplication();
                _settingsWindow.SettingsUpdated += OnSettingsUpdated;
                _settingsWindow.ResetPositionsRequested += OnResetPositionsRequested;
                _settingsWindow.MoveModeRequested += (_, _) => ToggleMoveMode();

                // Showing/activating the settings window can knock the overlay
                // out of the Win32 topmost band; put it straight back.
                _settingsWindow.Activated += (_, _) => _overlayWindow?.ReassertTopmost();
            }

            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        private void ShowHotkeys()
        {
            var hotkeysWindow = new HotkeysWindow();
            hotkeysWindow.ShowDialog();
        }

        internal void ShowFeedbackDialog()
        {
            var dialog = new View.FeedbackDialog(_speakers);
            dialog.ShowDialog();
        }

        private void OnResetPositionsRequested(object? sender, EventArgs e)
        {
            ResetPositions();
        }

        private void ResetPositions()
        {
            // Reset indicator positions to defaults (percent-based)
            _settingsManager.Update(s =>
            {
                s.Bars.LeftIndicatorPercent = 0.35;
                s.Bars.RightIndicatorPercent = 0.65;
            });

            _overlayWindow?.ApplySettings();
            _settingsWindow?.RefreshIndicatorSliders();
            _toastHost.ShowInfo("Positions reset");
        }

        private void OnSettingsUpdated(object? sender, EventArgs e)
        {
            _isMonitoring = _settingsManager.Settings.Display.Enabled;
            _overlayWindow?.ApplySettings();
            UpdateScreenVisibility();
        }

        private void UpdateScreenVisibility()
        {
            _overlayWindow?.SetVisible(_settingsManager.Settings.Display.Enabled);
        }

        private void UpdateDisplayMode()
        {
            _overlayWindow?.ApplySettings();
        }

        private void ToggleEnabled()
        {
            _settingsManager.Update(s => s.Display.Enabled = !s.Display.Enabled);
            _isMonitoring = _settingsManager.Settings.Display.Enabled;
            UpdateScreenVisibility();

            var status = _isMonitoring ? "on" : "off";
            _toastHost.ShowInfo($"Sound indicators {status}");
        }

        private void StartMonitoring()
        {
            _monitoringCts = new CancellationTokenSource();
            var token = _monitoringCts.Token;

            Task.Run(() =>
            {
                var pollCount = 0;
                while (!token.IsCancellationRequested)
                {
                    Thread.Sleep(50);

                    if (!_isMonitoring)
                        continue;

                    try
                    {
                        // Stays on this thread: the WASAPI meter reads are COM
                        // calls too slow for the dispatcher at this cadence —
                        // queued at Normal priority they starve render/input
                        // and freeze the app.
                        _speakers.Update();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error updating speakers: {ex.Message}");
                    }

                    // SignalDoctor shows UI from its event, so it ticks on the
                    // dispatcher; its 10 s silence window needs no more than
                    // the original 200 ms cadence.
                    if (++pollCount % 4 == 0)
                    {
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                        {
                            _signalDoctor?.Tick(_speakers.LastRawPeak,
                                _speakers.Endpoint.CurrentDeviceId,
                                _speakers.Endpoint.CurrentDeviceName);
                        });
                    }
                }
            }, token);
        }

        private void ExitApplication()
        {
            // Stop monitoring
            _monitoringCts?.Cancel();

            // Stop game detection
            _gameDetector.Stop();
            _gameDetector.Dispose();
            _toastHost.Close();

            // Stop audio session/endpoint plumbing
            _signalDoctorWindow?.Close();
            _speakers.Sessions.Dispose();
            _speakers.Endpoint.Dispose();

            // Dispose hotkey manager
            _hotkeyManager?.Dispose();

            // Clean up system tray
            _trayFlyout?.Close();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            // Close all windows
            _overlayWindow?.StopAndClose();
            _settingsWindow?.CloseForExit();

            // Save settings
            _settingsManager.Save();

            // Exit application
            Application.Current.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            ExitApplication();
            base.OnClosed(e);
        }
    }
}