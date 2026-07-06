using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View;

/// <summary>
/// Topmost, non-activating toast layer anchored top-right of the target monitor
/// (40 px margins), stacking up to 2 toasts (design 3c). The window background
/// is fully transparent, so clicks only land on the opaque cards; everything
/// else passes through. Replaces NotifyIcon balloon tips (unreliable on Win11).
/// All methods must be called on the UI thread.
/// </summary>
public sealed class ToastHost
{
    private const double CardWidth = 420;
    private const double Margin = 40;
    private const int MaxToasts = 2;
    private const int AutoDismissSeconds = 6;

    private Window? _window;
    private StackPanel? _stack;

    // --- Public API -------------------------------------------------------

    /// <summary>Unknown-game card: Create profile / Not now / Ignore / Exclude.</summary>
    public void ShowUnknownGame(string processName, string? exePath, Action onCreate, Action onIgnore, Action onExclude)
    {
        var card = BuildBigCard(
            iconExePath: exePath,
            title: "New game detected",
            body: $"{processName}.exe is running and making sound. Give it its own profile? " +
                  "Overlay style, colors and positions will switch automatically every time it runs.",
            primaryText: $"Create {processName} profile",
            onPrimary: onCreate,
            secondaryText: "Not now",
            linkText: "Ignore this game",
            onLink: onIgnore,
            link2Text: "Not a game? Exclude",
            onLink2: onExclude);

        AddToast(card, autoDismiss: false);
    }

    /// <summary>Ask-first card shown before switching to a known game's profile.</summary>
    public void ShowAskSwitch(AppProfile profile, Action onSwitch)
    {
        var card = BuildBigCard(
            iconExePath: profile.ExePath,
            title: $"{profile.ProcessName}.exe detected",
            body: $"Switch to the ★ {profile.Name} profile?",
            primaryText: "Switch",
            onPrimary: onSwitch,
            secondaryText: "Not now",
            linkText: null,
            onLink: null);

        AddToast(card, autoDismiss: false);
    }

    /// <summary>Compact status toast (replaces balloon tips); auto-dismisses.</summary>
    public void ShowInfo(string text)
    {
        var card = BuildCardShell(padding: new Thickness(16, 12, 16, 12));
        var content = new StackPanel();

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var dot = new Ellipse
        {
            Width = 10, Height = 10,
            Fill = Brush("Success"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetColumn(dot, 0);
        row.Children.Add(dot);

        var textBlock = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13.5,
            Foreground = Brush("Text"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(textBlock, 1);
        row.Children.Add(textBlock);

        var close = MakeLinkButton("✕", Brush("TextMuted"));
        close.Margin = new Thickness(12, 0, 0, 0);
        close.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(close, 2);
        row.Children.Add(close);

        content.Children.Add(row);

        // 6 px progress bar along the bottom, draining over the dismiss window
        var progress = new Border
        {
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = Brush("Border"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = CardWidth - 32,
            Margin = new Thickness(0, 10, 0, 0)
        };
        content.Children.Add(progress);

        card.Child = content;

        var toast = AddToast(card, autoDismiss: true);
        close.Click += (_, _) => Dismiss(toast);

        var drain = new DoubleAnimation(progress.Width, 0, TimeSpan.FromSeconds(AutoDismissSeconds));
        progress.BeginAnimation(FrameworkElement.WidthProperty, drain);
    }

    /// <summary>Compact auto-switched card with Undo; auto-dismisses after 6 s.</summary>
    public void ShowProfileSwitched(AppProfile profile, Action onUndo)
    {
        var card = BuildCardShell(padding: new Thickness(16, 14, 16, 14));
        var content = new StackPanel();

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new Ellipse
        {
            Width = 10, Height = 10,
            Fill = Brush("Success"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });

        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13.5,
            Foreground = Brush("Text"),
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = CardWidth - 140
        };
        text.Inlines.Add(new Run($"Switched to ★ {profile.Name}") { FontWeight = FontWeights.Bold });
        text.Inlines.Add(new Run($" — {StyleLabel(profile.OverlayStyle)} · {profile.ColorScale} scale")
        {
            Foreground = Brush("TextSecondary")
        });
        row.Children.Add(text);

        var undo = MakeLinkButton("Undo", Brush("Interactive"));
        undo.Margin = new Thickness(14, 0, 0, 0);
        undo.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(undo);

        content.Children.Add(row);

        // 6 px progress bar along the bottom, draining over the dismiss window
        var progress = new Border
        {
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = Brush("Border"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = CardWidth - 32,
            Margin = new Thickness(0, 12, 0, 0)
        };
        content.Children.Add(progress);

        card.Child = content;

        var toast = AddToast(card, autoDismiss: true);
        undo.Click += (_, _) => { onUndo(); Dismiss(toast); };

        var drain = new DoubleAnimation(progress.Width, 0, TimeSpan.FromSeconds(AutoDismissSeconds));
        progress.BeginAnimation(FrameworkElement.WidthProperty, drain);
    }

    // --- Card construction --------------------------------------------------

    private Border BuildBigCard(string? iconExePath, string title, string body,
        string primaryText, Action onPrimary, string secondaryText,
        string? linkText, Action? onLink,
        string? link2Text = null, Action? onLink2 = null)
    {
        var card = BuildCardShell(padding: new Thickness(24));
        var content = new StackPanel();

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal };
        headerRow.Children.Add(BuildExeIcon(iconExePath));

        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("Text"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0)
        };
        headerRow.Children.Add(titleBlock);
        content.Children.Add(headerRow);

        content.Children.Add(new TextBlock
        {
            Text = body,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13.5,
            Foreground = Brush("TextSecondary"),
            Margin = new Thickness(0, 12, 0, 16)
        });

        // WrapPanel: with two link actions the row can exceed the card width.
        var buttonRow = new WrapPanel { Orientation = Orientation.Horizontal };

        var primary = new Button
        {
            Content = primaryText,
            Style = (Style)Application.Current.FindResource("PrimaryButton"),
            MinHeight = 38
        };
        buttonRow.Children.Add(primary);

        var secondary = new Button
        {
            Content = secondaryText,
            Style = (Style)Application.Current.FindResource("SecondaryButton"),
            MinHeight = 38,
            Margin = new Thickness(10, 0, 0, 0)
        };
        buttonRow.Children.Add(secondary);

        Button? link = null;
        if (linkText != null)
        {
            link = MakeLinkButton(linkText, Brush("TextMuted"));
            link.Margin = new Thickness(14, 0, 0, 0);
            link.VerticalAlignment = VerticalAlignment.Center;
            buttonRow.Children.Add(link);
        }

        Button? link2 = null;
        if (link2Text != null)
        {
            link2 = MakeLinkButton(link2Text, Brush("TextMuted"));
            link2.Margin = new Thickness(14, 0, 0, 0);
            link2.VerticalAlignment = VerticalAlignment.Center;
            buttonRow.Children.Add(link2);
        }

        content.Children.Add(buttonRow);
        card.Child = content;

        // Wire after AddToast is called by the caller: use Loaded-independent capture.
        primary.Click += (_, _) => { onPrimary(); Dismiss(card); };
        secondary.Click += (_, _) => Dismiss(card);
        if (link != null && onLink != null)
            link.Click += (_, _) => { onLink(); Dismiss(card); };
        if (link2 != null && onLink2 != null)
            link2.Click += (_, _) => { onLink2(); Dismiss(card); };

        return card;
    }

    private Border BuildCardShell(Thickness padding)
    {
        return new Border
        {
            Width = CardWidth,
            Background = new SolidColorBrush(Color.FromArgb(240, 0x0D, 0x0F, 0x13)),
            BorderBrush = Brush("Border"),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(16),
            Padding = padding,
            Margin = new Thickness(0, 0, 0, 12)
        };
    }

    private FrameworkElement BuildExeIcon(string? exePath)
    {
        // 56 px square; real file icon when readable, "EXE" placeholder otherwise
        var box = new Border
        {
            Width = 56, Height = 56,
            Background = Brush("Raised"),
            CornerRadius = new CornerRadius(10)
        };

        if (exePath != null)
        {
            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon != null)
                {
                    var source = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    box.Child = new Image { Source = source, Width = 36, Height = 36 };
                    return box;
                }
            }
            catch
            {
                // Fall through to the text placeholder.
            }
        }

        box.Child = new TextBlock
        {
            Text = "EXE",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("TextMuted"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        return box;
    }

    private static Button MakeLinkButton(string text, System.Windows.Media.Brush foreground)
    {
        var button = new Button
        {
            Content = text,
            Style = (Style)Application.Current.FindResource("DangerLinkButton"),
            Foreground = foreground,
            FontSize = 13
        };
        return button;
    }

    // --- Host window / stacking ---------------------------------------------

    private Border AddToast(Border card, bool autoDismiss)
    {
        EnsureWindow();

        // Newest on top; cap the stack
        _stack!.Children.Insert(0, card);
        while (_stack.Children.Count > MaxToasts)
            _stack.Children.RemoveAt(_stack.Children.Count - 1);

        _window!.Show();
        Reposition();

        if (autoDismiss)
        {
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(AutoDismissSeconds)
            };
            timer.Tick += (_, _) => { timer.Stop(); Dismiss(card); };
            timer.Start();
        }

        return card;
    }

    private void Dismiss(Border card)
    {
        if (_stack == null) return;
        _stack.Children.Remove(card);
        if (_stack.Children.Count == 0)
            _window?.Hide();
        else
            Reposition();
    }

    private void EnsureWindow()
    {
        if (_window != null) return;

        _stack = new StackPanel();
        _window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ShowInTaskbar = false,
            ShowActivated = false,
            Focusable = false,
            Topmost = true,
            SizeToContent = SizeToContent.WidthAndHeight,
            Content = _stack
        };
        _window.SizeChanged += (_, _) => Reposition();
    }

    private void Reposition()
    {
        if (_window == null) return;
        // Same screen-coordinate convention as the overlay views
        _window.Left = MainWindow.ScreenLeft + MainWindow.ScreenWidth - _window.ActualWidth - Margin;
        _window.Top = MainWindow.ScreenTop + Margin;
    }

    public void Close()
    {
        _window?.Close();
        _window = null;
        _stack = null;
    }

    private static System.Windows.Media.Brush Brush(string key)
    {
        return (System.Windows.Media.Brush)Application.Current.FindResource(key);
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
