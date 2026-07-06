using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using DeafDirectionalHelper.Audio;
using DeafDirectionalHelper.Settings;
using Forms = System.Windows.Forms;

namespace DeafDirectionalHelper.View.Settings;

public partial class GeneralPage : UserControl
{
    private readonly SettingsManager _settingsManager = SettingsManager.Instance;
    private readonly Action _notifyChanged;
    private bool _isLoading = true;

    public GeneralPage(Action notifyChanged)
    {
        InitializeComponent();
        _notifyChanged = notifyChanged;
        LoadFromSettings();
    }

    public void LoadFromSettings()
    {
        _isLoading = true;
        var general = _settingsManager.Settings.General;

        MinimizedToggle.IsChecked = general.StartMinimized;
        StartupToggle.IsChecked = general.StartWithWindows;
        LoggingToggle.IsChecked = general.EnableAudioLogging;

        LoadMonitors();
        SelectByTag(RetentionSegmented, general.LogRetentionType.ToString());
        SelectComboByTag(RetentionSizeCombo, general.LogRetentionSizeMB.ToString());
        SelectComboByTag(RetentionDaysCombo, general.LogRetentionDays.ToString());

        UpdateStateLabels();
        UpdateLoggingVisibility();
        UpdateLogSizeLabel();
        _isLoading = false;
    }

    private void LoadMonitors()
    {
        MonitorCombo.Items.Clear();
        var screens = Forms.Screen.AllScreens;
        var current = _settingsManager.Settings.Display.TargetMonitor;

        for (int i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            var label = screen.Primary
                ? $"Monitor {i + 1} (Primary) · {screen.Bounds.Width}×{screen.Bounds.Height}"
                : $"Monitor {i + 1} · {screen.Bounds.Width}×{screen.Bounds.Height}";
            var item = new ComboBoxItem { Content = label, Tag = i };
            MonitorCombo.Items.Add(item);
            if (i == current)
                MonitorCombo.SelectedItem = item;
        }

        if (MonitorCombo.SelectedItem == null && MonitorCombo.Items.Count > 0)
            MonitorCombo.SelectedIndex = 0;
    }

    // --- Handlers ---

    private void MinimizedToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _settingsManager.Update(s => s.General.StartMinimized = MinimizedToggle.IsChecked == true);
        UpdateStateLabels();
    }

    private void StartupToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var enabled = StartupToggle.IsChecked == true;
        _settingsManager.Update(s => s.General.StartWithWindows = enabled);
        UpdateStateLabels();

        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                if (enabled)
                {
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName;
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

    private void MonitorCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || MonitorCombo.SelectedItem == null) return;
        var index = (int)((ComboBoxItem)MonitorCombo.SelectedItem).Tag;
        _settingsManager.Update(s => s.Display.TargetMonitor = index);
        _notifyChanged();
    }

    private void LoggingToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _settingsManager.Update(s => s.General.EnableAudioLogging = LoggingToggle.IsChecked == true);
        UpdateStateLabels();
        UpdateLoggingVisibility();
    }

    private void RetentionSegmented_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || RetentionSegmented.SelectedItem == null) return;
        var type = Enum.Parse<LogRetentionType>((string)((ListBoxItem)RetentionSegmented.SelectedItem).Tag);
        _settingsManager.Update(s => s.General.LogRetentionType = type);
        UpdateLoggingVisibility();
    }

    private void RetentionSizeCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || RetentionSizeCombo.SelectedItem == null) return;
        var mb = int.Parse((string)((ComboBoxItem)RetentionSizeCombo.SelectedItem).Tag);
        _settingsManager.Update(s => s.General.LogRetentionSizeMB = mb);
    }

    private void RetentionDaysCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || RetentionDaysCombo.SelectedItem == null) return;
        var days = int.Parse((string)((ComboBoxItem)RetentionDaysCombo.SelectedItem).Tag);
        _settingsManager.Update(s => s.General.LogRetentionDays = days);
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start("explorer.exe", AudioEventLogger.Instance.GetLogDirectory());
        }
        catch (Exception ex)
        {
            ThemedMessageBox.Show($"Could not open log folder: {ex.Message}", "Error", Window.GetWindow(this));
        }
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        if (ThemedMessageBox.ShowYesNo("Are you sure you want to clear all audio logs?", "Clear logs",
                Window.GetWindow(this)))
        {
            AudioEventLogger.Instance.ClearLogs();
            UpdateLogSizeLabel();
        }
    }

    // --- UI state ---

    private void UpdateStateLabels()
    {
        SetLabel(MinimizedStateLabel, MinimizedToggle.IsChecked == true);
        SetLabel(StartupStateLabel, StartupToggle.IsChecked == true);
        SetLabel(LoggingStateLabel, LoggingToggle.IsChecked == true);
    }

    private void SetLabel(TextBlock label, bool on)
    {
        label.Text = on ? "On" : "Off";
        label.Style = (Style)FindResource(on ? "ToggleStateOnText" : "ToggleStateOffText");
    }

    private void UpdateLoggingVisibility()
    {
        var general = _settingsManager.Settings.General;
        LoggingRows.Visibility = general.EnableAudioLogging ? Visibility.Visible : Visibility.Collapsed;
        RetentionSizeCombo.Visibility = general.LogRetentionType == LogRetentionType.Size
            ? Visibility.Visible : Visibility.Collapsed;
        RetentionDaysCombo.Visibility = general.LogRetentionType == LogRetentionType.Date
            ? Visibility.Visible : Visibility.Collapsed;
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

    private static void SelectByTag(ListBox listBox, string tag)
    {
        foreach (ListBoxItem item in listBox.Items)
        {
            if ((string)item.Tag == tag)
            {
                listBox.SelectedItem = item;
                return;
            }
        }
    }

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if ((string)item.Tag == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }
}
