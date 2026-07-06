using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using DeafDirectionalHelper.Audio;
using DeafDirectionalHelper.Settings;
using NAudio.CoreAudioApi;

namespace DeafDirectionalHelper.View;

/// <summary>
/// "Signal doctor" (design 3b): shown when the overlay is armed but the selected
/// device has been silent while a game plays audio to a different device.
/// Offers two one-click fixes that apply immediately.
/// </summary>
public partial class SignalDoctorWindow : Window
{
    private const string RoutingGuideUrl = "https://github.com/wellforce-brandon/DeafDirectionalHelper#71-surround-setup";

    private readonly MismatchEventArgs _mismatch;
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly DispatcherTimer _meterTimer;
    private MMDevice? _selectedDevice;
    private MMDevice? _candidateDevice;

    public SignalDoctorWindow(MismatchEventArgs mismatch)
    {
        InitializeComponent();
        Helpers.DarkChrome.Apply(this);
        _mismatch = mismatch;

        BannerDetail.Text = $"{mismatch.GameProcess}.exe has been playing audio for {mismatch.SilentSeconds} s, " +
                            "but none of it reached the device you're listening to.";
        SelectedDeviceLabel.Text = mismatch.SelectedDeviceName;
        SelectedSilentLabel.Text = $"silent {mismatch.SilentSeconds} s";
        CandidateDeviceLabel.Text = mismatch.GameDeviceName;
        CandidateSessionLabel.Text = $"● {mismatch.GameProcess}.exe session";
        CandidateChannelsChip.Content = $"{mismatch.GameDeviceChannels} CH";
        StereoNote.Visibility = mismatch.GameDeviceChannels < 8 ? Visibility.Visible : Visibility.Collapsed;
        FollowGameHelper.Text = $"tracks {mismatch.GameProcess}.exe wherever it plays, forever";

        TryOpenDevices();

        _meterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _meterTimer.Tick += (_, _) => UpdateMeters();
        _meterTimer.Start();

        Closed += (_, _) =>
        {
            _meterTimer.Stop();
            _enumerator.Dispose();
        };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Escape)
                Close();
        };
    }

    private void TryOpenDevices()
    {
        try
        {
            _selectedDevice = _enumerator.GetDevice(_mismatch.SelectedDeviceId);
            SelectedChannelsChip.Content = $"{_selectedDevice.AudioMeterInformation.PeakValues.Count} CH";
        }
        catch
        {
            _selectedDevice = null;
        }

        try
        {
            _candidateDevice = _enumerator.GetDevice(_mismatch.GameDeviceId);
        }
        catch
        {
            _candidateDevice = null;
        }
    }

    private void UpdateMeters()
    {
        SetMeter(SelectedMeterFill, ReadPeak(_selectedDevice));
        SetMeter(CandidateMeterFill, ReadPeak(_candidateDevice));
    }

    private static float ReadPeak(MMDevice? device)
    {
        try
        {
            return device?.AudioMeterInformation.MasterPeakValue ?? 0f;
        }
        catch
        {
            return 0f;
        }
    }

    private void SetMeter(FrameworkElement fill, float level)
    {
        var track = (FrameworkElement)fill.Parent;
        fill.Width = Math.Clamp(level, 0f, 1f) * track.ActualWidth;
    }

    private void ListenHere_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.Update(s =>
        {
            s.General.CaptureMode = CaptureMode.FixedDevice;
            s.General.AudioDevice = _mismatch.GameDeviceName;
        });
        Close();
    }

    private void FollowGame_Click(object sender, RoutedEventArgs e)
    {
        SettingsManager.Instance.Update(s => s.General.CaptureMode = CaptureMode.FollowGame);
        Close();
    }

    private void RoutingGuide_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo { FileName = RoutingGuideUrl, UseShellExecute = true });
    }
}
