using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeafDirectionalHelper.View;

/// <summary>Hotkey reference card (design 2j). Enter/Esc both dismiss.</summary>
public partial class HotkeysWindow : ThemedDialog
{
    private static readonly (string Keys, string Description)[] Hotkeys =
    {
        ("Ctrl+Shift+R", "Overlay on / off"),
        ("Ctrl+Shift+M", "Next overlay style"),
        ("Ctrl+Shift+S", "Open settings"),
        ("Ctrl+Shift+P", "Reset positions"),
        ("Ctrl+Shift+H", "Show this card"),
        ("Ctrl+Shift+E", "Move mode")
    };

    public HotkeysWindow()
    {
        InitializeComponent();

        foreach (var (keys, description) in Hotkeys)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
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
}
