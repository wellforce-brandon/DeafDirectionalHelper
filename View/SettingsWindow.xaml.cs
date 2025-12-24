using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DeafDirectionalHelper.Audio;
using DeafDirectionalHelper.Settings;
using NAudio.CoreAudioApi;
using Forms = System.Windows.Forms;

namespace DeafDirectionalHelper.View;

public partial class SettingsWindow : Window
{
    private readonly SettingsManager _settingsManager;
    private bool _isLoading = true;

    public event EventHandler? ExitRequested;
    public event EventHandler? SettingsUpdated;
    public event EventHandler? ResetPositionsRequested;

    public SettingsWindow()
    {
        InitializeComponent();
        _settingsManager = SettingsManager.Instance;
        LoadSettings();
        LoadMonitors();
        LoadAudioDevices();
        _isLoading = false;
    }

    private void LoadSettings()
    {
        var settings = _settingsManager.Settings;

        // Display
        EnabledCheckbox.IsChecked = settings.Display.Enabled;
        SelectComboBoxByTag(ModeComboBox, settings.Display.Mode.ToString());

        // Bars
        LockedCheckbox.IsChecked = settings.Bars.Locked;
        TransparentCheckbox.IsChecked = settings.Bars.TransparentMode;
        WidthSlider.Value = settings.Bars.Width;
        WidthLabel.Text = settings.Bars.Width.ToString();
        SensitivitySlider.Value = settings.Bars.Sensitivity;
        SensitivityLabel.Text = settings.Bars.Sensitivity.ToString("F1");
        ThresholdSlider.Value = settings.Bars.MinThreshold;
        ThresholdLabel.Text = settings.Bars.MinThreshold.ToString("F2");
        IgnoreBalancedCheckbox.IsChecked = settings.Bars.IgnoreBalancedSounds;
        HideLfeCheckbox.IsChecked = settings.Bars.HideLfe;
        SelectComboBoxByTag(DualLayoutComboBox, settings.Bars.DualLayout.ToString());
        SelectComboBoxByTag(SurroundLayoutComboBox, settings.Bars.SurroundLayout.ToString());
        SpatialScaleSlider.Value = settings.Bars.SpatialScale;
        SpatialScaleLabel.Text = $"{(int)(settings.Bars.SpatialScale * 100)}%";
        MaxOpacitySlider.Value = settings.Bars.MaxOpacity;
        MaxOpacityLabel.Text = $"{(int)(settings.Bars.MaxOpacity * 100)}%";

        // Indicator position sliders
        LeftIndicatorSlider.Value = settings.Bars.LeftIndicatorPercent;
        LeftIndicatorLabel.Text = $"{(int)(settings.Bars.LeftIndicatorPercent * 100)}%";
        RightIndicatorSlider.Value = settings.Bars.RightIndicatorPercent;
        RightIndicatorLabel.Text = $"{(int)(settings.Bars.RightIndicatorPercent * 100)}%";
        // Linked spread: calculate from left position (distance from center)
        var spread = 0.5 - settings.Bars.LeftIndicatorPercent;
        LinkedSpreadSlider.Value = spread;
        LinkedSpreadLabel.Text = $"{(int)(spread * 100)}%";

        // Update bar-specific settings visibility based on mode
        UpdateBarSettingsVisibility(settings.Display.Mode);

        // General
        StartMinimizedCheckbox.IsChecked = settings.General.StartMinimized;
        StartWithWindowsCheckbox.IsChecked = settings.General.StartWithWindows;
        EnableLoggingCheckbox.IsChecked = settings.General.EnableAudioLogging;
        UpdateLogSizeLabel();
    }

    private void UpdateLogSizeLabel()
    {
        try
        {
            var size = AudioEventLogger.Instance.GetCurrentLogSize();
            var sizeText = size switch
            {
                < 1024 => $"{size} B",
                < 1024 * 1024 => $"{size / 1024.0:F1} KB",
                _ => $"{size / (1024.0 * 1024.0):F1} MB"
            };
            LogSizeLabel.Text = $"Log size: {sizeText}";
        }
        catch
        {
            LogSizeLabel.Text = "Log size: N/A";
        }
    }

    private void LoadAudioDevices()
    {
        AudioDeviceComboBox.Items.Clear();

        // Add auto option
        var autoItem = new ComboBoxItem { Content = "(Auto-detect 8-channel device)", Tag = "" };
        AudioDeviceComboBox.Items.Add(autoItem);

        try
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            foreach (var device in devices)
            {
                var channels = device.AudioMeterInformation.PeakValues.Count;
                // Show all devices, mark stereo devices with a note
                var label = channels >= 8
                    ? $"{device.FriendlyName} ({channels}ch)"
                    : $"{device.FriendlyName} ({channels}ch - stereo mode)";

                var item = new ComboBoxItem
                {
                    Content = label,
                    Tag = device.FriendlyName
                };
                AudioDeviceComboBox.Items.Add(item);

                // Select if it matches current setting
                var currentDevice = _settingsManager.Settings.General.AudioDevice;
                if (!string.IsNullOrEmpty(currentDevice) && device.FriendlyName.Contains(currentDevice))
                {
                    AudioDeviceComboBox.SelectedItem = item;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading audio devices: {ex.Message}");
        }

        // Select auto if nothing selected
        if (AudioDeviceComboBox.SelectedItem == null)
        {
            AudioDeviceComboBox.SelectedIndex = 0;
        }
    }

    private void LoadMonitors()
    {
        MonitorComboBox.Items.Clear();

        var screens = Forms.Screen.AllScreens;
        var currentMonitor = _settingsManager.Settings.Display.TargetMonitor;

        for (int i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            var label = screen.Primary
                ? $"Monitor {i + 1} (Primary) - {screen.Bounds.Width}x{screen.Bounds.Height}"
                : $"Monitor {i + 1} - {screen.Bounds.Width}x{screen.Bounds.Height}";

            var item = new ComboBoxItem
            {
                Content = label,
                Tag = i
            };
            MonitorComboBox.Items.Add(item);

            if (i == currentMonitor)
            {
                MonitorComboBox.SelectedItem = item;
            }
        }

        // Default to first monitor if current selection is invalid
        if (MonitorComboBox.SelectedItem == null && MonitorComboBox.Items.Count > 0)
        {
            MonitorComboBox.SelectedIndex = 0;
        }
    }

    private void SelectComboBoxByTag(ComboBox comboBox, string tag)
    {
        foreach (ComboBoxItem item in comboBox.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                comboBox.SelectedItem = item;
                return;
            }
        }
        if (comboBox.Items.Count > 0)
            comboBox.SelectedIndex = 0;
    }

    // Event handlers

    private void EnabledCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _settingsManager.Update(s => s.Display.Enabled = EnabledCheckbox.IsChecked ?? true);
        SettingsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void ModeComboBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || ModeComboBox.SelectedItem == null) return;
        var tag = (ModeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (Enum.TryParse<DisplayMode>(tag, out var mode))
        {
            _settingsManager.Update(s => s.Display.Mode = mode);
            UpdateBarSettingsVisibility(mode);
            SettingsUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void MonitorComboBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || MonitorComboBox.SelectedItem == null) return;
        var tag = (MonitorComboBox.SelectedItem as ComboBoxItem)?.Tag;
        if (tag is int monitorIndex)
        {
            _settingsManager.Update(s => s.Display.TargetMonitor = monitorIndex);
            // Reset positions when monitor changes
            _settingsManager.Update(s =>
            {
                s.Bars.LeftX = 0;
                s.Bars.RightX = -1; // Auto (right edge)
            });
            ResetPositionsRequested?.Invoke(this, EventArgs.Empty);
            SettingsUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateBarSettingsVisibility(DisplayMode mode)
    {
        var settings = _settingsManager.Settings;

        // Mode Settings section - always visible, but sub-sections depend on mode
        var showDualSettings = mode == DisplayMode.Bars || mode == DisplayMode.Both;
        var show7Point1Settings = mode == DisplayMode.Full7Point1 || mode == DisplayMode.Both;

        // Dual mode settings (layout selector, link checkbox)
        DualModeSettings.Visibility = showDualSettings ? Visibility.Visible : Visibility.Collapsed;

        // 7.1 surround mode settings (layout selector, hide LFE)
        SurroundModeSettings.Visibility = show7Point1Settings ? Visibility.Visible : Visibility.Collapsed;

        // Update layout settings section
        UpdateLayoutSettingsVisibility();
    }

    private void UpdateLayoutSettingsVisibility()
    {
        var settings = _settingsManager.Settings;
        var mode = settings.Display.Mode;
        var dualLayout = settings.Bars.DualLayout;
        var surroundLayout = settings.Bars.SurroundLayout;
        var isLinked = settings.Bars.Locked;

        var isDualMode = mode == DisplayMode.Bars || mode == DisplayMode.Both;
        var is7Point1Mode = mode == DisplayMode.Full7Point1 || mode == DisplayMode.Both;
        var isVerticalLayout = dualLayout == DualLayout.Vertical;
        var isSpatialLayout = surroundLayout == SurroundLayout.Spatial;

        // Layout Settings section visibility - show if dual mode OR 7.1 spatial mode is active
        LayoutSettingsGroup.Visibility = (isDualMode || (is7Point1Mode && isSpatialLayout)) ? Visibility.Visible : Visibility.Collapsed;

        // Vertical layout specific settings (bar width) - only for vertical layout
        VerticalLayoutSettings.Visibility = isDualMode && isVerticalLayout ? Visibility.Visible : Visibility.Collapsed;

        // Position settings - always show for dual mode
        PositionSettings.Visibility = isDualMode ? Visibility.Visible : Visibility.Collapsed;

        // Show linked spread slider when linked, separate sliders when unlinked
        LinkedSpreadPanel.Visibility = isDualMode && isLinked ? Visibility.Visible : Visibility.Collapsed;
        LeftIndicatorPanel.Visibility = isDualMode && !isLinked ? Visibility.Visible : Visibility.Collapsed;
        RightIndicatorPanel.Visibility = isDualMode && !isLinked ? Visibility.Visible : Visibility.Collapsed;

        // 7.1 Spatial scale slider - only show for 7.1 mode with Spatial layout
        SpatialScalePanel.Visibility = is7Point1Mode && isSpatialLayout ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LockedCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _settingsManager.Update(s => s.Bars.Locked = LockedCheckbox.IsChecked ?? true);
        UpdateLayoutSettingsVisibility();
        SettingsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void TransparentCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _settingsManager.Update(s => s.Bars.TransparentMode = TransparentCheckbox.IsChecked ?? false);
        SettingsUpdated?.Invoke(this, EventArgs.Empty);
    }

    // ValueChanged handlers - update labels in real-time while dragging
    private void WidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WidthLabel != null)
            WidthLabel.Text = ((int)WidthSlider.Value).ToString();
    }

    private void SensitivitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SensitivityLabel != null)
            SensitivityLabel.Text = Math.Round(SensitivitySlider.Value, 1).ToString("F1");
    }

    private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThresholdLabel != null)
            ThresholdLabel.Text = Math.Round(ThresholdSlider.Value, 2).ToString("F2");
    }

    // DragCompleted handlers - save settings only when drag finishes
    private void WidthSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_isLoading) return;
        var width = (int)WidthSlider.Value;
        _settingsManager.Update(s => s.Bars.Width = width);
        SettingsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void SensitivitySlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_isLoading) return;
        var sensitivity = Math.Round(SensitivitySlider.Value, 1);
        _settingsManager.Update(s => s.Bars.Sensitivity = sensitivity);
        SettingsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void ThresholdSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_isLoading) return;
        var threshold = Math.Round(ThresholdSlider.Value, 2);
        _settingsManager.Update(s => s.Bars.MinThreshold = threshold);
        SettingsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void IgnoreBalancedCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _settingsManager.Update(s => s.Bars.IgnoreBalancedSounds = IgnoreBalancedCheckbox.IsChecked ?? false);
        SettingsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void HideLfeCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _settingsManager.Update(s => s.Bars.HideLfe = HideLfeCheckbox.IsChecked ?? false);
        SettingsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void DualLayoutComboBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || DualLayoutComboBox.SelectedItem == null) return;
        var tag = (DualLayoutComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (Enum.TryParse<DualLayout>(tag, out var layout))
        {
            _settingsManager.Update(s => s.Bars.DualLayout = layout);
            UpdateLayoutSettingsVisibility();
            SettingsUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SurroundLayoutComboBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || SurroundLayoutComboBox.SelectedItem == null) return;
        var tag = (SurroundLayoutComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (Enum.TryParse<SurroundLayout>(tag, out var layout))
        {
            _settingsManager.Update(s => s.Bars.SurroundLayout = layout);
            UpdateLayoutSettingsVisibility();
            SettingsUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SpatialScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SpatialScaleLabel != null)
            SpatialScaleLabel.Text = $"{(int)(SpatialScaleSlider.Value * 100)}%";
    }

    private void SpatialScaleSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_isLoading) return;
        var scale = Math.Round(SpatialScaleSlider.Value, 1);
        _settingsManager.Update(s => s.Bars.SpatialScale = scale);
        SettingsUpdated?.Invoke(this, EventArgs.Empty);
    }

    // Indicator position sliders - ValueChanged only updates labels (for responsiveness)
    private void LinkedSpreadSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LinkedSpreadLabel == null) return;
        LinkedSpreadLabel.Text = $"{(int)(LinkedSpreadSlider.Value * 100)}%";

        // Also update the individual slider labels to show preview
        var spread = LinkedSpreadSlider.Value;
        var leftPos = 0.5 - spread;
        var rightPos = 0.5 + spread;
        if (LeftIndicatorLabel != null)
            LeftIndicatorLabel.Text = $"{(int)(leftPos * 100)}%";
        if (RightIndicatorLabel != null)
            RightIndicatorLabel.Text = $"{(int)(rightPos * 100)}%";
    }

    private void LinkedSpreadSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_isLoading) return;

        // Spread is the distance from center (0.5), so left = 0.5 - spread, right = 0.5 + spread
        var spread = LinkedSpreadSlider.Value;
        var leftPos = 0.5 - spread;
        var rightPos = 0.5 + spread;

        _settingsManager.Update(s =>
        {
            s.Bars.LeftIndicatorPercent = leftPos;
            s.Bars.RightIndicatorPercent = rightPos;
        });

        // Update the individual sliders to reflect the new positions
        LeftIndicatorSlider.Value = leftPos;
        RightIndicatorSlider.Value = rightPos;

        SettingsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void LeftIndicatorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LeftIndicatorLabel != null)
            LeftIndicatorLabel.Text = $"{(int)(LeftIndicatorSlider.Value * 100)}%";
    }

    private void LeftIndicatorSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_isLoading) return;
        _settingsManager.Update(s => s.Bars.LeftIndicatorPercent = LeftIndicatorSlider.Value);
        SettingsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void RightIndicatorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RightIndicatorLabel != null)
            RightIndicatorLabel.Text = $"{(int)(RightIndicatorSlider.Value * 100)}%";
    }

    private void RightIndicatorSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_isLoading) return;
        _settingsManager.Update(s => s.Bars.RightIndicatorPercent = RightIndicatorSlider.Value);
        SettingsUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Updates the indicator sliders from the current settings (called when indicators are dragged)
    /// </summary>
    public void RefreshIndicatorSliders()
    {
        var settings = _settingsManager.Settings;
        _isLoading = true;

        LeftIndicatorSlider.Value = settings.Bars.LeftIndicatorPercent;
        LeftIndicatorLabel.Text = $"{(int)(settings.Bars.LeftIndicatorPercent * 100)}%";
        RightIndicatorSlider.Value = settings.Bars.RightIndicatorPercent;
        RightIndicatorLabel.Text = $"{(int)(settings.Bars.RightIndicatorPercent * 100)}%";

        // Update linked spread slider
        var spread = 0.5 - settings.Bars.LeftIndicatorPercent;
        LinkedSpreadSlider.Value = spread;
        LinkedSpreadLabel.Text = $"{(int)(spread * 100)}%";

        _isLoading = false;
    }

    private void MaxOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MaxOpacityLabel != null)
            MaxOpacityLabel.Text = $"{(int)(MaxOpacitySlider.Value * 100)}%";
    }

    private void MaxOpacitySlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_isLoading) return;
        var maxOpacity = Math.Round(MaxOpacitySlider.Value, 2);
        _settingsManager.Update(s => s.Bars.MaxOpacity = maxOpacity);
        SettingsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void ResetPositions_Click(object sender, RoutedEventArgs e)
    {
        _settingsManager.Update(s =>
        {
            s.Bars.LeftX = 0;
            s.Bars.RightX = -1; // Auto
        });
        SettingsUpdated?.Invoke(this, EventArgs.Empty);
        ResetPositionsRequested?.Invoke(this, EventArgs.Empty);
        MessageBox.Show("All positions have been reset.", "Reset", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void AudioDeviceComboBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || AudioDeviceComboBox.SelectedItem == null) return;
        var tag = (AudioDeviceComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        _settingsManager.Update(s => s.General.AudioDevice = string.IsNullOrEmpty(tag) ? null : tag);
    }

    private void RefreshDevices_Click(object sender, RoutedEventArgs e)
    {
        _isLoading = true;
        LoadAudioDevices();
        _isLoading = false;
    }

    private void StartMinimizedCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _settingsManager.Update(s => s.General.StartMinimized = StartMinimizedCheckbox.IsChecked ?? false);
    }

    private void StartWithWindowsCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var startWithWindows = StartWithWindowsCheckbox.IsChecked ?? false;
        _settingsManager.Update(s => s.General.StartWithWindows = startWithWindows);

        // Update Windows startup registry
        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                if (startWithWindows)
                {
                    var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                        key.SetValue("DeafDirectionalHelper", $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue("DeafDirectionalHelper", false);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating startup registry: {ex.Message}");
        }
    }

    private void EnableLoggingCheckbox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var enableLogging = EnableLoggingCheckbox.IsChecked ?? false;
        _settingsManager.Update(s => s.General.EnableAudioLogging = enableLogging);
        AudioEventLogger.Instance.IsEnabled = enableLogging;
    }

    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logDir = AudioEventLogger.Instance.GetLogDirectory();
            Process.Start("explorer.exe", logDir);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open log folder: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("Are you sure you want to clear all audio logs?",
            "Clear Logs", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            AudioEventLogger.Instance.ClearLogs();
            UpdateLogSizeLabel();
            MessageBox.Show("Logs cleared.", "Clear Logs", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Hotkeys_Click(object sender, RoutedEventArgs e)
    {
        var hotkeysWindow = new HotkeysWindow { Owner = this };
        hotkeysWindow.ShowDialog();
    }

    private void Hide_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to exit DeafDirectionalHelper?",
            "Exit Application",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Don't actually close, just hide
        e.Cancel = true;
        Hide();
    }
}
