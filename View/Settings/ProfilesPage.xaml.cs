using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View.Settings;

public partial class ProfilesPage : UserControl
{
    private readonly SettingsManager _settingsManager = SettingsManager.Instance;
    private readonly ProfileManager _profileManager = ProfileManager.Instance;
    private readonly Action _notifyChanged;
    private bool _isLoading = true;

    public ProfilesPage(Action notifyChanged)
    {
        InitializeComponent();
        _notifyChanged = notifyChanged;
        Reload();
        _profileManager.ProfilesChanged += (_, _) => Dispatcher.BeginInvoke(Reload);
    }

    public void SetAutoSwitchPausedNoteVisible(bool visible)
    {
        PausedNote.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Reload()
    {
        _isLoading = true;

        var behavior = _settingsManager.Settings.ProfileSwitchBehavior.ToString();
        foreach (ListBoxItem item in BehaviorSegmented.Items)
        {
            if ((string)item.Tag == behavior)
                BehaviorSegmented.SelectedItem = item;
        }

        OfferToggle.IsChecked = _settingsManager.Settings.OfferProfileForUnknownGames;
        OfferStateLabel.Text = OfferToggle.IsChecked == true ? "On" : "Off";
        OfferStateLabel.Style = (Style)FindResource(OfferToggle.IsChecked == true ? "ToggleStateOnText" : "ToggleStateOffText");

        RebuildCards();
        _isLoading = false;
    }

    private void BehaviorSegmented_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || BehaviorSegmented.SelectedItem == null) return;
        var behavior = Enum.Parse<ProfileSwitchBehavior>((string)((ListBoxItem)BehaviorSegmented.SelectedItem).Tag);
        _settingsManager.Update(s => s.ProfileSwitchBehavior = behavior);
    }

    private void OfferToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        _settingsManager.Update(s => s.OfferProfileForUnknownGames = OfferToggle.IsChecked == true);
        OfferStateLabel.Text = OfferToggle.IsChecked == true ? "On" : "Off";
        OfferStateLabel.Style = (Style)FindResource(OfferToggle.IsChecked == true ? "ToggleStateOnText" : "ToggleStateOffText");
    }

    // --- Cards ---

    private void RebuildCards()
    {
        CardGrid.Children.Clear();

        // Find the profile a running watched process would use (RUNNING NOW pill)
        var runningProfileId = FindRunningProfileId();

        foreach (var profile in _profileManager.Profiles)
            CardGrid.Children.Add(BuildProfileCard(profile, profile.Id == runningProfileId));

        CardGrid.Children.Add(BuildNewProfileCard());
    }

    private string? FindRunningProfileId()
    {
        try
        {
            var names = Process.GetProcesses().Select(p => p.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return _profileManager.Profiles
                .FirstOrDefault(p => !p.IsDefault && p.ProcessName != null && names.Contains(p.ProcessName))
                ?.Id;
        }
        catch
        {
            return null;
        }
    }

    private FrameworkElement BuildProfileCard(AppProfile profile, bool running)
    {
        var content = new StackPanel();

        // Name + running pill
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
        nameRow.Children.Add(new TextBlock
        {
            Text = (profile.IsDefault ? "★ " : "") + profile.Name,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Text")
        });
        if (running)
        {
            nameRow.Children.Add(new ContentControl
            {
                Style = (Style)FindResource("Pill"),
                Content = "RUNNING NOW",
                Background = (Brush)FindResource("Success"),
                Foreground = (Brush)FindResource("OnSuccess"),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        content.Children.Add(nameRow);

        content.Children.Add(new TextBlock
        {
            Text = profile.ProcessName != null ? $"{profile.ProcessName}.exe" : "always available",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Foreground = (Brush)FindResource("TextSecondary"),
            Margin = new Thickness(0, 2, 0, 8)
        });

        // Chips: style, scale (with gradient dot), auto-switch
        var chips = new WrapPanel();
        chips.Children.Add(MakeChip(StyleLabel(profile.OverlayStyle)));
        chips.Children.Add(MakeScaleChip(profile.ColorScale));
        if (!profile.IsDefault)
            chips.Children.Add(MakeChip("auto-switch on"));
        content.Children.Add(chips);

        // Links
        var links = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        links.Children.Add(MakeLink("Edit", (Brush)FindResource("Interactive"), () => EditProfile(profile)));
        links.Children.Add(MakeLink("Duplicate", (Brush)FindResource("TextSecondary"), () =>
        {
            _profileManager.DuplicateProfile(profile);
        }));
        if (!profile.IsDefault)
        {
            links.Children.Add(MakeLink("Delete", (Brush)FindResource("DangerText"), () =>
            {
                if (ThemedMessageBox.ShowYesNo($"Delete the {profile.Name} profile?", "Delete profile",
                        Window.GetWindow(this)))
                {
                    _profileManager.DeleteProfile(profile.Id);
                    _notifyChanged();
                }
            }));
        }
        content.Children.Add(links);

        return new Border
        {
            Background = (Brush)FindResource("Panel"),
            BorderBrush = running ? (Brush)FindResource("Success") : (Brush)FindResource("Border"),
            BorderThickness = new Thickness(running ? 2 : 1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 14, 16, 14),
            Margin = new Thickness(0, 0, 12, 12),
            Child = content
        };
    }

    private FrameworkElement BuildNewProfileCard()
    {
        var button = new Button
        {
            Content = "+ New profile",
            Style = (Style)FindResource("SecondaryButton"),
            BorderBrush = (Brush)FindResource("BorderStrong"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        button.Click += (_, _) => CreateProfile();

        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(button);
        panel.Children.Add(new TextBlock
        {
            Text = "or launch a game and let detection offer one",
            FontSize = 12,
            Foreground = (Brush)FindResource("TextMuted"),
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });

        return new Border
        {
            BorderBrush = (Brush)FindResource("BorderStrong"),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(0, 0, 12, 12),
            MinHeight = 120,
            Child = panel
        };
    }

    private void CreateProfile()
    {
        var editor = new ProfileEditorWindow { Owner = Window.GetWindow(this), IsNewProfile = true };
        editor.SetProfile("New Profile", null, false);
        if (editor.ShowDialog() == true)
        {
            _profileManager.CreateProfile(editor.ProfileName, editor.ExePath);
            _notifyChanged();
        }
    }

    private void EditProfile(AppProfile profile)
    {
        var editor = new ProfileEditorWindow { Owner = Window.GetWindow(this) };
        editor.SetProfile(profile.Name, profile.ExePath, profile.IsDefault);
        if (editor.ShowDialog() == true)
        {
            _profileManager.UpdateProfile(profile, editor.ProfileName, editor.ExePath);
            _notifyChanged();
        }
    }

    // --- Small builders ---

    private FrameworkElement MakeChip(string text)
    {
        return new Border
        {
            Background = (Brush)FindResource("Raised"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 6, 6),
            Child = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondary")
            }
        };
    }

    private FrameworkElement MakeScaleChip(ColorScale scale)
    {
        var stops = ScaleEngine.StopsFor(scale);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new Ellipse
        {
            Width = 9, Height = 9,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0),
            Fill = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(stops[0], 0),
                new GradientStop(stops[1], 0.5),
                new GradientStop(stops[2], 1)
            }, 0)
        });
        row.Children.Add(new TextBlock
        {
            Text = scale.ToString(),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Foreground = (Brush)FindResource("TextSecondary"),
            VerticalAlignment = VerticalAlignment.Center
        });

        return new Border
        {
            Background = (Brush)FindResource("Raised"),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 6, 6),
            Child = row
        };
    }

    private static Button MakeLink(string text, Brush foreground, Action onClick)
    {
        var button = new Button
        {
            Content = text,
            Style = (Style)Application.Current.FindResource("DangerLinkButton"),
            Foreground = foreground,
            FontSize = 13,
            Margin = new Thickness(0, 0, 16, 0)
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private static string StyleLabel(OverlayStyle style) => style switch
    {
        OverlayStyle.SideBars => "Side bars",
        OverlayStyle.RadarRing => "Radar ring",
        OverlayStyle.RingPing => "Ring ping",
        OverlayStyle.CompassStrip => "Compass",
        OverlayStyle.EdgeGlow => "Edge glow",
        _ => style.ToString()
    };
}
