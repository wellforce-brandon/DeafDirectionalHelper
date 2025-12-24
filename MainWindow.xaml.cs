using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DeafDirectionalHelper.Audio;
using DeafDirectionalHelper.Hotkeys;
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
        private readonly ColoredSpeakers _coloredSpeakers;
        private readonly SettingsManager _settingsManager;

        private DualBarsView? _dualBarsView;
        private HorizontalDualView? _horizontalDualView;
        private Full7Point1View? _full7Point1View;
        private SettingsWindow? _settingsWindow;
        private Forms.NotifyIcon? _notifyIcon;

        private bool _isMonitoring = true;
        private CancellationTokenSource? _monitoringCts;
        private GlobalHotkeyManager? _hotkeyManager;

        public MainWindow()
        {
            InitializeComponent();

            _settingsManager = SettingsManager.Instance;
            _speakers = new Speakers();
            _coloredSpeakers = new ColoredSpeakers(_speakers);

            SetupSystemTray();
            ShowScreens();
            StartMonitoring();
            SetupHotkeys();

            // Hide main window (we use system tray)
            Hide();
            WindowState = WindowState.Minimized;
            ShowInTaskbar = false;
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

            if (_hotkeyManager.RegisterHotkeys())
            {
                Console.WriteLine("Global hotkeys registered successfully");
                Console.WriteLine("  Ctrl+Shift+R: Toggle enable/disable");
                Console.WriteLine("  Ctrl+Shift+M: Toggle display mode");
                Console.WriteLine("  Ctrl+Shift+S: Show settings");
                Console.WriteLine("  Ctrl+Shift+P: Reset positions");
                Console.WriteLine("  Ctrl+Shift+H: Show hotkeys");
            }
        }

        private void ToggleDisplayMode()
        {
            var settings = _settingsManager.Settings;
            var newMode = settings.Display.Mode switch
            {
                Settings.DisplayMode.Bars => Settings.DisplayMode.Full7Point1,
                Settings.DisplayMode.Full7Point1 => Settings.DisplayMode.Both,
                Settings.DisplayMode.Both => Settings.DisplayMode.Bars,
                _ => Settings.DisplayMode.Bars
            };

            _settingsManager.Update(s => s.Display.Mode = newMode);
            UpdateDisplayMode();

            var modeName = newMode switch
            {
                Settings.DisplayMode.Bars => "Side Bars",
                Settings.DisplayMode.Full7Point1 => "7.1 Surround",
                Settings.DisplayMode.Both => "Both",
                _ => "Unknown"
            };
            _notifyIcon?.ShowBalloonTip(1000, "DeafDirectionalHelper", $"Display mode: {modeName}", Forms.ToolTipIcon.Info);
        }

        private void SetupSystemTray()
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = LoadTrayIcon(),
                Visible = true,
                Text = "DeafDirectionalHelper - Audio Visualizer"
            };

            // Context menu
            var contextMenu = new Forms.ContextMenuStrip();

            var settingsItem = new Forms.ToolStripMenuItem("Settings");
            settingsItem.Click += (_, _) => ShowSettings();
            contextMenu.Items.Add(settingsItem);

            var enableItem = new Forms.ToolStripMenuItem("Enable/Disable");
            enableItem.Click += (_, _) => ToggleEnabled();
            contextMenu.Items.Add(enableItem);

            contextMenu.Items.Add(new Forms.ToolStripSeparator());

            var exitItem = new Forms.ToolStripMenuItem("Exit");
            exitItem.Click += (_, _) => ExitApplication();
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
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
            // Create dual bars view (vertical side bars - single window with both bars)
            _dualBarsView = new DualBarsView(_coloredSpeakers);
            _dualBarsView.PositionChanged += OnIndicatorPositionChanged;

            // Create horizontal dual view
            _horizontalDualView = new HorizontalDualView(_coloredSpeakers);
            _horizontalDualView.IndicatorPositionChanged += OnIndicatorPositionChanged;

            // Create 7.1 view
            _full7Point1View = new Full7Point1View(_coloredSpeakers);

            // Show based on display mode
            UpdateDisplayMode();
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
                _settingsWindow = new SettingsWindow();
                _settingsWindow.ExitRequested += (_, _) => ExitApplication();
                _settingsWindow.SettingsUpdated += OnSettingsUpdated;
                _settingsWindow.ResetPositionsRequested += OnResetPositionsRequested;
                _settingsWindow.IsVisibleChanged += OnSettingsWindowVisibilityChanged;
            }

            _settingsWindow.Show();
            _settingsWindow.Activate();

            // Explicitly enable dragging when showing settings
            SetDraggingEnabled(true);
        }

        private void ShowHotkeys()
        {
            var hotkeysWindow = new HotkeysWindow();
            hotkeysWindow.ShowDialog();
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

            // The settings change will trigger ApplyLayout on all views
            // But also explicitly recenter views
            _dualBarsView?.Recenter();
            _horizontalDualView?.Recenter();
            _full7Point1View?.Recenter();

            // Update settings window sliders
            _settingsWindow?.RefreshIndicatorSliders();

            _notifyIcon?.ShowBalloonTip(1000, "DeafDirectionalHelper", "Positions reset", Forms.ToolTipIcon.Info);
        }

        private void OnSettingsWindowVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var isVisible = (bool)e.NewValue;
            SetDraggingEnabled(isVisible);
        }

        private void SetDraggingEnabled(bool enabled)
        {
            if (_dualBarsView != null)
                _dualBarsView.AllowDragging = enabled;
            if (_horizontalDualView != null)
                _horizontalDualView.AllowDragging = enabled;
            if (_full7Point1View != null)
                _full7Point1View.AllowDragging = enabled;
        }

        private void OnSettingsUpdated(object? sender, EventArgs e)
        {
            var settings = _settingsManager.Settings;

            // Update enabled state
            _isMonitoring = settings.Display.Enabled;

            // Views handle their own layout via OnSettingsChanged
            // Just update visibility based on enabled state
            UpdateScreenVisibility();
        }

        private void UpdateScreenVisibility()
        {
            var settings = _settingsManager.Settings;

            if (!settings.Display.Enabled)
            {
                _dualBarsView?.Hide();
                _horizontalDualView?.Hide();
                _full7Point1View?.Hide();
                return;
            }

            UpdateDisplayMode();
        }

        private void UpdateDisplayMode()
        {
            var settings = _settingsManager.Settings;
            var mode = settings.Display.Mode;
            var dualLayout = settings.Bars.DualLayout;

            // Show/hide based on mode
            var showBars = mode == Settings.DisplayMode.Bars || mode == Settings.DisplayMode.Both;
            var show7Point1 = mode == Settings.DisplayMode.Full7Point1 || mode == Settings.DisplayMode.Both;

            // For bars mode, check which layout to use
            var showVerticalBars = showBars && dualLayout == Settings.DualLayout.Vertical;
            var showHorizontalDual = showBars && dualLayout == Settings.DualLayout.HorizontalLine;

            if (_dualBarsView != null)
            {
                if (showVerticalBars) _dualBarsView.Show();
                else _dualBarsView.Hide();
            }

            if (_horizontalDualView != null)
            {
                if (showHorizontalDual) _horizontalDualView.Show();
                else _horizontalDualView.Hide();
            }

            if (_full7Point1View != null)
            {
                if (show7Point1) _full7Point1View.Show();
                else _full7Point1View.Hide();
            }
        }

        private void ToggleEnabled()
        {
            _settingsManager.Update(s => s.Display.Enabled = !s.Display.Enabled);
            _isMonitoring = _settingsManager.Settings.Display.Enabled;
            UpdateScreenVisibility();

            var status = _isMonitoring ? "enabled" : "disabled";
            _notifyIcon?.ShowBalloonTip(1000, "DeafDirectionalHelper", $"Sound indicators {status}", Forms.ToolTipIcon.Info);
        }

        private void StartMonitoring()
        {
            _monitoringCts = new CancellationTokenSource();
            var token = _monitoringCts.Token;

            Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    Thread.Sleep(200);

                    if (_isMonitoring)
                    {
                        Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
                        {
                            try
                            {
                                _speakers.Update();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error updating speakers: {ex.Message}");
                            }
                        });
                    }
                }
            }, token);
        }

        private void ExitApplication()
        {
            // Stop monitoring
            _monitoringCts?.Cancel();

            // Dispose hotkey manager
            _hotkeyManager?.Dispose();

            // Clean up system tray
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            // Close all windows
            _dualBarsView?.Close();
            _horizontalDualView?.Close();
            _full7Point1View?.Close();
            _settingsWindow?.Close();

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