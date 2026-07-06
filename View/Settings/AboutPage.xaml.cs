using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace DeafDirectionalHelper.View.Settings;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
        VersionText.Text = $"Version {AppVersion.Version}";
    }

    private void GitHub_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/wellforce-brandon/DeafDirectionalHelper",
            UseShellExecute = true
        });
    }
}
