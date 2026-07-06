using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeafDirectionalHelper.View.Settings;

public partial class HotkeysPage : UserControl
{
    private static readonly (string Keys, string Description)[] Hotkeys =
    {
        ("Ctrl+Shift+R", "Overlay on / off"),
        ("Ctrl+Shift+M", "Next overlay style"),
        ("Ctrl+Shift+S", "Open settings"),
        ("Ctrl+Shift+P", "Reset positions"),
        ("Ctrl+Shift+H", "Show the hotkey card"),
        ("Ctrl+Shift+E", "Move mode (edit positions on screen)")
    };

    public HotkeysPage()
    {
        InitializeComponent();

        foreach (var (keys, description) in Hotkeys)
        {
            var row = new Border { Style = (Style)FindResource("SettingsRow") };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var chip = new ContentControl
            {
                Style = (Style)FindResource("KbdChip"),
                Content = keys,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            grid.Children.Add(chip);

            var text = new TextBlock
            {
                Text = description,
                FontSize = 14.5,
                Foreground = (Brush)FindResource("Text"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            row.Child = grid;
            HotkeyList.Children.Add(row);
        }
    }
}
