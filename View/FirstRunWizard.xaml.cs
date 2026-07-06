using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeafDirectionalHelper.Audio;
using DeafDirectionalHelper.Settings;
using NAudio.CoreAudioApi;

namespace DeafDirectionalHelper.View;

/// <summary>
/// First-run wizard (design 2k), shown once (FirstRunCompleted flag).
/// On finish: applies device + style choices and switches the app to
/// launch-minimized-to-tray (plan D8).
/// </summary>
public partial class FirstRunWizard : Window
{
    private static readonly (string Keys, string Description)[] Hotkeys =
    {
        ("Ctrl+Shift+R", "Overlay on / off"),
        ("Ctrl+Shift+M", "Next overlay style"),
        ("Ctrl+Shift+S", "Open settings"),
        ("Ctrl+Shift+P", "Reset positions"),
        ("Ctrl+Shift+H", "Hotkey card"),
        ("Ctrl+Shift+E", "Move mode")
    };

    private static readonly (OverlayStyle Style, string Name, string Description)[] Styles =
    {
        (OverlayStyle.SideBars, "Side bars", "Two vertical bars at the screen edges — front / side / rear segments."),
        (OverlayStyle.RadarRing, "Radar ring", "A compass donut near the screen bottom; sectors light toward the sound."),
        (OverlayStyle.RingPing, "Ring ping", "Concentric rings — loud sounds ping close to the center."),
        (OverlayStyle.CompassStrip, "Compass", "A slim strip of channel meters along the top edge."),
        (OverlayStyle.EdgeGlow, "Edge glow", "The screen edges themselves glow toward the sound — nothing in your view.")
    };

    private int _step;
    private CaptureMode _captureMode = CaptureMode.FollowGame;
    private string? _fixedDevice;
    private OverlayStyle _style = OverlayStyle.SideBars;

    public FirstRunWizard()
    {
        InitializeComponent();
        Helpers.DarkChrome.Apply(this);
        BuildDeviceRows();
        BuildStyleRows();
        BuildHotkeyRows();
    }

    // --- Step 1: capture/device rows ---

    private void BuildDeviceRows()
    {
        AddDeviceRow("Follow the game (recommended)",
            "Reads the game's own audio session wherever it plays. Zero setup.",
            badge: null, isChecked: true,
            onChecked: () => { _captureMode = CaptureMode.FollowGame; _fixedDevice = null; });

        AddDeviceRow("Windows default device",
            "Follows whatever Windows is playing to.",
            badge: null, isChecked: false,
            onChecked: () => { _captureMode = CaptureMode.WindowsDefault; _fixedDevice = null; });

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var channels = device.AudioMeterInformation.PeakValues.Count;
                var name = device.FriendlyName;
                AddDeviceRow(name, null,
                    badge: channels >= 8 ? ($"{channels} CH · SURROUND", true) : ($"{channels} CH · STEREO", false),
                    isChecked: false,
                    onChecked: () => { _captureMode = CaptureMode.FixedDevice; _fixedDevice = name; });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Wizard device enumeration failed: {ex.Message}");
        }
    }

    private void AddDeviceRow(string title, string? helper, (string Text, bool Surround)? badge,
        bool isChecked, Action onChecked)
    {
        var content = new StackPanel();
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
        titleRow.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("Text"),
            VerticalAlignment = VerticalAlignment.Center
        });
        if (badge.HasValue)
        {
            titleRow.Children.Add(new ContentControl
            {
                Style = (Style)FindResource("Pill"),
                Content = badge.Value.Text,
                Background = (Brush)FindResource(badge.Value.Surround ? "Success" : "Warn"),
                Foreground = (Brush)FindResource(badge.Value.Surround ? "OnSuccess" : "OnWarn"),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        content.Children.Add(titleRow);
        if (helper != null)
        {
            content.Children.Add(new TextBlock
            {
                Text = helper,
                Style = (Style)FindResource("RowHelperText"),
                Margin = new Thickness(0, 3, 0, 0)
            });
        }

        var radio = new RadioButton
        {
            Style = (Style)FindResource("RadioCard"),
            GroupName = "WizardDevice",
            Content = content,
            MinHeight = 56,
            Margin = new Thickness(0, 0, 0, 8),
            IsChecked = isChecked
        };
        System.Windows.Automation.AutomationProperties.SetName(radio, title);
        radio.Checked += (_, _) => onChecked();
        DeviceRows.Children.Add(radio);
    }

    // --- Step 2: style rows ---

    private void BuildStyleRows()
    {
        foreach (var (style, name, description) in Styles)
        {
            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 14.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("Text")
            });
            content.Children.Add(new TextBlock
            {
                Text = description,
                Style = (Style)FindResource("RowHelperText"),
                Margin = new Thickness(0, 3, 0, 0)
            });

            var radio = new RadioButton
            {
                Style = (Style)FindResource("RadioCard"),
                GroupName = "WizardStyle",
                Content = content,
                Margin = new Thickness(0, 0, 0, 8),
                IsChecked = style == OverlayStyle.SideBars
            };
            System.Windows.Automation.AutomationProperties.SetName(radio, $"Overlay style {name}");
            var captured = style;
            radio.Checked += (_, _) => _style = captured;
            StyleRows.Children.Add(radio);
        }
    }

    private void BuildHotkeyRows()
    {
        foreach (var (keys, description) in Hotkeys)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var chip = new ContentControl
            {
                Style = (Style)FindResource("KbdChip"),
                Content = keys,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            row.Children.Add(chip);

            var text = new TextBlock
            {
                Text = description,
                FontSize = 14.5,
                Foreground = (Brush)FindResource("Text"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            HotkeyRows.Children.Add(row);
        }
    }

    // --- Navigation ---

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_step < 2)
        {
            _step++;
            UpdateStep();
            return;
        }
        Finish(applyChoices: true);
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        Finish(applyChoices: false);
    }

    private void UpdateStep()
    {
        Step1.Visibility = _step == 0 ? Visibility.Visible : Visibility.Collapsed;
        Step2.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step3.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        StepLabel.Text = $"STEP {_step + 1} OF 3";
        NextButton.Content = _step == 2 ? "Finish" : "Next →";

        var active = (Brush)FindResource("Interactive");
        var idle = (Brush)FindResource("Border");
        Pill1.Background = _step >= 0 ? active : idle;
        Pill2.Background = _step >= 1 ? active : idle;
        Pill3.Background = _step >= 2 ? active : idle;
    }

    private void Finish(bool applyChoices)
    {
        SettingsManager.Instance.Update(s =>
        {
            if (applyChoices)
            {
                s.General.CaptureMode = _captureMode;
                s.General.AudioDevice = _fixedDevice;
                s.Bars.OverlayStyle = _style;
            }
            s.FirstRunCompleted = true;
            s.General.StartMinimized = true; // D8: live in the tray from now on
        });

        if (applyChoices)
            ProfileManager.Instance.SaveCurrentSettingsToProfile(ProfileManager.Instance.ActiveProfile);

        DialogResult = true;
        Close();
    }
}
