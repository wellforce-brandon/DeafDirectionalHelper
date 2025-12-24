using System.Windows;

namespace DeafDirectionalHelper.View;

public partial class HotkeysWindow : Window
{
    public HotkeysWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
