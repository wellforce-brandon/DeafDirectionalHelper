using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using DeafDirectionalHelper.Audio;

namespace DeafDirectionalHelper.View.Settings;

public partial class AboutPage : UserControl
{
    private readonly Speakers _speakers;

    public AboutPage(Speakers speakers)
    {
        InitializeComponent();
        _speakers = speakers;
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

    private void Feedback_Click(object sender, RoutedEventArgs e)
    {
        new FeedbackDialog(_speakers) { Owner = Window.GetWindow(this) }.ShowDialog();
    }
}
