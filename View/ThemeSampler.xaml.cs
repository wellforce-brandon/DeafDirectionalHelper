using System.Windows;

namespace DeafDirectionalHelper.View;

public partial class ThemeSampler : Window
{
    public ThemeSampler()
    {
        InitializeComponent();
        Helpers.DarkChrome.Apply(this);
    }
}
