using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DeafDirectionalHelper.Audio;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View.Settings;

/// <summary>
/// The 2a sidebar settings shell. Replaces the old SettingsWindow with the
/// same outward contract (events + methods MainWindow relies on).
/// Close (X) and Esc hide instead of closing; auto-switch pauses while visible.
/// </summary>
public partial class SettingsShell : Window
{
    private readonly SettingsManager _settingsManager = SettingsManager.Instance;
    private readonly ProfileManager _profileManager = ProfileManager.Instance;

    private readonly OverlayPage _overlayPage;
    private readonly AudioDevicePage _audioDevicePage;
    private readonly ProfilesPage _profilesPage;
    private readonly GeneralPage _generalPage;
    private readonly HotkeysPage _hotkeysPage;
    private readonly AboutPage _aboutPage;

    public event EventHandler? ExitRequested;
    public event EventHandler? SettingsUpdated;
    public event EventHandler? ResetPositionsRequested;
    public event EventHandler? MoveModeRequested;

    public SettingsShell(Speakers speakers)
    {
        InitializeComponent();
        Helpers.DarkChrome.Apply(this);

        _overlayPage = new OverlayPage(speakers, NotifyPageChangedSettings);
        _overlayPage.ResetPositionsRequested += (_, _) => ResetPositionsRequested?.Invoke(this, EventArgs.Empty);
        _overlayPage.MoveModeRequested += (_, _) => MoveModeRequested?.Invoke(this, EventArgs.Empty);
        _audioDevicePage = new AudioDevicePage(speakers, NotifyPageChangedSettings);
        _profilesPage = new ProfilesPage(NotifyPageChangedSettings);
        _generalPage = new GeneralPage(NotifyPageChangedSettings);
        _hotkeysPage = new HotkeysPage();
        _aboutPage = new AboutPage();

        NavList.SelectedIndex = 0;
        UpdateProfilePill();
        UpdateStatusLine(speakers);

        _profileManager.ProfilesChanged += (_, _) => Dispatcher.BeginInvoke(UpdateProfilePill);

        // Pause auto-switching while the window is visible (existing behavior)
        IsVisibleChanged += (_, _) =>
        {
            _profileManager.AutoSwitchPaused = IsVisible;
            _profilesPage.SetAutoSwitchPausedNoteVisible(IsVisible && _profileManager.AutoSwitchEnabled);
            if (IsVisible)
                RefreshAllPages();
        };

        // Status line refresh piggybacks on a slow timer inside AudioDevicePage's
        // meter tick; keep the rail cheap with a 1 s timer here.
        var statusTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        statusTimer.Tick += (_, _) => { if (IsVisible) UpdateStatusLine(speakers); };
        statusTimer.Start();

        PreviewKeyDown += OnShellKeyDown;
    }

    // --- Outward contract (kept from old SettingsWindow) ---

    public void RefreshIndicatorSliders() => _overlayPage.RefreshPositionSliders();

    public void OnProfileAutoSwitched(AppProfile profile)
    {
        UpdateProfilePill();
        RefreshAllPages();
    }

    // --- Internals ---

    private void NotifyPageChangedSettings()
    {
        // Pages already wrote + saved via SettingsManager.Update. Persist the
        // change into the active profile (profiles are WYSIWYG in the new UI),
        // then let MainWindow react.
        _profileManager.SaveCurrentSettingsToProfile(_profileManager.ActiveProfile);
        SettingsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshAllPages()
    {
        _overlayPage.LoadFromSettings();
        _audioDevicePage.LoadFromSettings();
        _profilesPage.Reload();
        _generalPage.LoadFromSettings();
    }

    private void UpdateProfilePill()
    {
        ProfilePill.Content = $"★ {_profileManager.ActiveProfile.Name}";
    }

    private void UpdateStatusLine(Speakers speakers)
    {
        var tracked = speakers.Endpoint.TrackedProcessName;
        var channels = speakers.CurrentChannelCount;
        StatusText.Text = tracked != null
            ? $"{tracked}.exe detected · audio flowing"
            : $"Overlay running · {channels}-channel device";
    }

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PageHost == null) return;
        PageHost.Content = NavList.SelectedIndex switch
        {
            0 => _overlayPage,
            1 => _audioDevicePage,
            2 => _profilesPage,
            3 => _generalPage,
            4 => _hotkeysPage,
            5 => (object)_aboutPage,
            _ => _overlayPage
        };
    }

    private void OnShellKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }

        // F6 moves focus between the rail and the page content
        if (e.Key == Key.F6)
        {
            if (NavList.IsKeyboardFocusWithin)
            {
                ((UIElement?)PageHost.Content)?.MoveFocus(
                    new TraversalRequest(FocusNavigationDirection.First));
            }
            else
            {
                var item = NavList.ItemContainerGenerator.ContainerFromIndex(
                    Math.Max(0, NavList.SelectedIndex)) as ListBoxItem;
                item?.Focus();
            }
            e.Handled = true;
            return;
        }

        // Shift+Left/Right on a slider jumps 5 steps (plan turn-5 keyboard map)
        if ((e.Key == Key.Left || e.Key == Key.Right) &&
            (Keyboard.Modifiers & ModifierKeys.Shift) != 0 &&
            Keyboard.FocusedElement is Slider slider)
        {
            var delta = slider.SmallChange * 5 * (e.Key == Key.Right ? 1 : -1);
            slider.Value = Math.Clamp(slider.Value + delta, slider.Minimum, slider.Maximum);
            e.Handled = true;
            return;
        }

        // Up/Down move between rows inside page content (sliders would otherwise
        // consume them as value changes; Left/Right stay the adjust keys)
        if ((e.Key == Key.Up || e.Key == Key.Down) &&
            !NavList.IsKeyboardFocusWithin &&
            Keyboard.FocusedElement is UIElement focused &&
            focused is not ComboBox && !(focused is ComboBoxItem))
        {
            var direction = e.Key == Key.Up
                ? FocusNavigationDirection.Up
                : FocusNavigationDirection.Down;
            if (focused.MoveFocus(new TraversalRequest(direction)))
                e.Handled = true;
        }
    }

    private void ResetPositions_Click(object sender, RoutedEventArgs e)
    {
        ResetPositionsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ExitConfirmDialog { Owner = this };
        if (dialog.ShowDialog() == true)
            ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    private void HideWindow_Click(object sender, RoutedEventArgs e) => Hide();

    private bool _allowClose;

    /// <summary>Really close (app exit); normal Close/X just hides.</summary>
    public void CloseForExit()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_allowClose) return;

        // X hides; the app lives in the tray (existing behavior)
        e.Cancel = true;
        Hide();
    }
}
