using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DeafDirectionalHelper.Audio;
using DeafDirectionalHelper.Settings;
using NAudio.CoreAudioApi;

namespace DeafDirectionalHelper.View.Settings;

public partial class AudioDevicePage : UserControl
{
    private static readonly string[] ChannelLabels = { "FL", "FR", "C", "LFE", "RL", "RR", "SL", "SR" };

    private readonly SettingsManager _settingsManager = SettingsManager.Instance;
    private readonly Speakers _speakers;
    private readonly Action _notifyChanged;
    private readonly DispatcherTimer _meterTimer;
    private readonly Border[] _meterFills = new Border[8];
    private bool _isLoading = true;

    public AudioDevicePage(Speakers speakers, Action notifyChanged)
    {
        InitializeComponent();
        _speakers = speakers;
        _notifyChanged = notifyChanged;

        BuildMeters();
        LoadFromSettings();

        _meterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _meterTimer.Tick += (_, _) => UpdateMeters();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) _meterTimer.Start();
            else _meterTimer.Stop();
        };
    }

    public void LoadFromSettings()
    {
        _isLoading = true;
        var mode = _settingsManager.Settings.General.CaptureMode;
        FollowGameCard.IsChecked = mode == CaptureMode.FollowGame;
        WindowsDefaultCard.IsChecked = mode == CaptureMode.WindowsDefault;
        FixedDeviceCard.IsChecked = mode == CaptureMode.FixedDevice;
        DeviceCombo.IsEnabled = mode == CaptureMode.FixedDevice;
        LoadDevices();
        _isLoading = false;
    }

    private void LoadDevices()
    {
        DeviceCombo.Items.Clear();
        DeviceCombo.Items.Add(new ComboBoxItem { Content = "(Auto: first 8-channel device)", Tag = "" });

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var current = _settingsManager.Settings.General.AudioDevice;

            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var channels = device.AudioMeterInformation.PeakValues.Count;
                var item = new ComboBoxItem
                {
                    Content = $"{device.FriendlyName} · {channels} ch",
                    Tag = device.FriendlyName
                };
                DeviceCombo.Items.Add(item);

                if (!string.IsNullOrEmpty(current) && device.FriendlyName.Contains(current))
                    DeviceCombo.SelectedItem = item;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading audio devices: {ex.Message}");
        }

        DeviceCombo.SelectedItem ??= DeviceCombo.Items[0];
    }

    private void BuildMeters()
    {
        for (int i = 0; i < 8; i++)
        {
            var fill = new Border
            {
                CornerRadius = new CornerRadius(0, 0, 4, 4),
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = 0
            };
            _meterFills[i] = fill;

            var track = new Border
            {
                Width = 26, Height = 56,
                Background = (Brush)FindResource("Bg"),
                BorderBrush = (Brush)FindResource("Border"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                ClipToBounds = true,
                Child = fill
            };

            var cell = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            cell.Children.Add(track);
            cell.Children.Add(new TextBlock
            {
                Text = ChannelLabels[i],
                FontFamily = new FontFamily("Consolas"),
                FontSize = 10.5,
                Foreground = (Brush)FindResource("TextSecondary"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
            MeterPanel.Children.Add(cell);
        }
    }

    private void UpdateMeters()
    {
        var bars = _settingsManager.Settings.Bars;
        var values = new[]
        {
            _speakers.Speaker1.Value, _speakers.Speaker2.Value,
            _speakers.Speaker3.Value, _speakers.Speaker4.Value,
            _speakers.Speaker5.Value, _speakers.Speaker6.Value,
            _speakers.Speaker7.Value, _speakers.Speaker8.Value
        };

        for (int i = 0; i < 8; i++)
        {
            var level = values[i] < bars.MinThreshold ? 0 : Math.Min(1.0, values[i] * bars.Sensitivity);
            _meterFills[i].Height = level * 54;
            _meterFills[i].Background = level < ScaleEngine.InvisibleBelow
                ? Brushes.Transparent
                : new SolidColorBrush(ScaleEngine.At(bars.ColorScale, level));
        }

        var channels = _speakers.CurrentChannelCount;
        var tracked = _speakers.Endpoint.TrackedProcessName;
        SignalCheckLabel.Text = tracked != null
            ? $"Live signal check — receiving {tracked}.exe on {_speakers.CurrentDeviceName}"
            : $"Live signal check — listening on {_speakers.CurrentDeviceName}";
        StereoNote.Visibility = channels > 0 && channels < 8 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CaptureMode_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        var mode = Enum.Parse<CaptureMode>((string)((RadioButton)sender).Tag);
        _settingsManager.Update(s => s.General.CaptureMode = mode);
        DeviceCombo.IsEnabled = mode == CaptureMode.FixedDevice;
        _notifyChanged();
    }

    private void DeviceCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || DeviceCombo.SelectedItem == null) return;
        var tag = (DeviceCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        _settingsManager.Update(s => s.General.AudioDevice = string.IsNullOrEmpty(tag) ? null : tag);
        _notifyChanged();
    }

    private void RefreshDevices_Click(object sender, RoutedEventArgs e)
    {
        _isLoading = true;
        LoadDevices();
        _isLoading = false;
    }
}
