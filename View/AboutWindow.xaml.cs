using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace DeafDirectionalHelper.View;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {AppVersion.Version}";
    }

    private void GitHubLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
