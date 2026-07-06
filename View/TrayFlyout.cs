using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View;

/// <summary>
/// Tray flyout (design 2l): a borderless WPF popup window anchored above the
/// tray, replacing the old WinForms ContextMenuStrip. Header shows run state,
/// active profile and a master indicators toggle; items carry hotkey hints;
/// Exit opens the themed confirm dialog. Fully keyboard operable (Tab/Enter,
/// Esc closes); deactivation closes it.
/// </summary>
public sealed class TrayFlyout
{
    private const double FlyoutWidth = 290;

    private readonly Action _onToggleEnabled;
    private readonly Action _onOpenSettings;
    private readonly Action _onNextStyle;
    private readonly Action _onResetPositions;
    private readonly Action _onExitConfirmed;

    private Window? _window;
    private CheckBox? _masterToggle;
    private TextBlock? _statusText;

    public TrayFlyout(Action onToggleEnabled, Action onOpenSettings, Action onNextStyle,
        Action onResetPositions, Action onExitConfirmed)
    {
        _onToggleEnabled = onToggleEnabled;
        _onOpenSettings = onOpenSettings;
        _onNextStyle = onNextStyle;
        _onResetPositions = onResetPositions;
        _onExitConfirmed = onExitConfirmed;
    }

    public void Toggle()
    {
        if (_window is { IsVisible: true })
        {
            _window.Hide();
            return;
        }
        Show();
    }

    private void Show()
    {
        _window ??= BuildWindow();
        RefreshState();

        // Anchor above the tray: bottom-right of the primary work area
        var work = SystemParameters.WorkArea;
        _window.Show();
        _window.UpdateLayout();
        _window.Left = work.Right - _window.ActualWidth - 8;
        _window.Top = work.Bottom - _window.ActualHeight - 8;
        _window.Activate();
    }

    public void Close()
    {
        _window?.Close();
        _window = null;
    }

    private void RefreshState()
    {
        if (_masterToggle != null)
            _masterToggle.IsChecked = SettingsManager.Instance.Settings.Display.Enabled;
        if (_statusText != null)
            _statusText.Text = $"Running · ★ {ProfileManager.Instance.ActiveProfile.Name}";
    }

    private Window BuildWindow()
    {
        var root = new StackPanel();

        // Header
        var header = new Grid { Margin = new Thickness(14, 12, 14, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = TryLoadIcon();
        if (icon != null)
        {
            var image = new Image { Source = icon, Width = 24, Height = 24, VerticalAlignment = VerticalAlignment.Center };
            header.Children.Add(image);
        }

        var titleStack = new StackPanel { Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        titleStack.Children.Add(new TextBlock
        {
            Text = "DeafDirectionalHelper",
            FontSize = 13.5,
            FontWeight = FontWeights.Bold,
            Foreground = Brush("Text")
        });
        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        statusRow.Children.Add(new Ellipse
        {
            Width = 8, Height = 8,
            Fill = Brush("Success"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        });
        _statusText = new TextBlock
        {
            Text = "Running",
            FontSize = 12,
            Foreground = Brush("TextSecondary"),
            VerticalAlignment = VerticalAlignment.Center
        };
        statusRow.Children.Add(_statusText);
        titleStack.Children.Add(statusRow);
        Grid.SetColumn(titleStack, 1);
        header.Children.Add(titleStack);

        _masterToggle = new CheckBox
        {
            Style = (Style)Application.Current.FindResource("ToggleSwitch"),
            LayoutTransform = new ScaleTransform(0.8, 0.8),
            VerticalAlignment = VerticalAlignment.Center
        };
        System.Windows.Automation.AutomationProperties.SetName(_masterToggle, "Sound indicators");
        _masterToggle.Click += (_, _) => _onToggleEnabled();
        Grid.SetColumn(_masterToggle, 2);
        header.Children.Add(_masterToggle);

        root.Children.Add(header);
        root.Children.Add(new Border { Height = 1, Background = Brush("Hairline"), Margin = new Thickness(10, 0, 10, 6) });

        // Items
        root.Children.Add(MakeItem("Open settings", "Ctrl+Shift+S", _onOpenSettings));
        root.Children.Add(MakeItem("Next overlay style", "Ctrl+Shift+M", _onNextStyle));
        root.Children.Add(MakeItem("Reset positions", "Ctrl+Shift+P", _onResetPositions));

        root.Children.Add(new Border { Height = 1, Background = Brush("Hairline"), Margin = new Thickness(10, 6, 10, 6) });

        var exit = MakeItem("Exit", null, () =>
        {
            _window?.Hide();
            var dialog = new ExitConfirmDialog();
            if (dialog.ShowDialog() == true)
                _onExitConfirmed();
        });
        ((TextBlock)((Grid)exit.Content).Children[0]).Foreground = Brush("DangerText");
        root.Children.Add(exit);

        var chrome = new Border
        {
            Background = Brush("Panel"),
            BorderBrush = Brush("Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(0, 0, 0, 8),
            Child = root,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black, Opacity = 0.5, BlurRadius = 18, ShadowDepth = 4, Direction = 270
            }
        };

        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Topmost = true,
            SizeToContent = SizeToContent.WidthAndHeight,
            Width = double.NaN,
            Content = new Border { Child = chrome, Width = FlyoutWidth, Margin = new Thickness(12) }
        };
        window.Deactivated += (_, _) => window.Hide();
        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                window.Hide();
                e.Handled = true;
            }
        };
        return window;
    }

    private Button MakeItem(string text, string? hotkey, Action onClick)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 13.5,
            Foreground = Brush("Text"),
            VerticalAlignment = VerticalAlignment.Center
        });

        if (hotkey != null)
        {
            var hint = new TextBlock
            {
                Text = hotkey,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = Brush("TextMuted"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(hint, 1);
            grid.Children.Add(hint);
        }

        var button = new Button
        {
            Height = 40,
            Margin = new Thickness(8, 0, 8, 0),
            Padding = new Thickness(10, 0, 10, 0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Content = grid,
            FocusVisualStyle = (Style)Application.Current.FindResource("FocusRingInset")
        };
        button.Template = BuildItemTemplate();
        button.Click += (_, _) =>
        {
            _window?.Hide();
            onClick();
        };
        return button;
    }

    private static ControlTemplate BuildItemTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border), "Root");
        border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        border.SetValue(Border.PaddingProperty, new Thickness(10, 0, 10, 0));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, Brush("Raised"), "Root"));
        template.Triggers.Add(hover);
        return template;
    }

    private static ImageSource? TryLoadIcon()
    {
        try
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons", "Icon32.png");
            if (System.IO.File.Exists(path))
                return new BitmapImage(new Uri(path));
        }
        catch
        {
            // Header just shows text without the icon.
        }
        return null;
    }

    private static System.Windows.Media.Brush Brush(string key)
    {
        return (System.Windows.Media.Brush)Application.Current.FindResource(key);
    }
}
