using System.Windows;
using System.Windows.Media;

namespace DeafDirectionalHelper.View;

/// <summary>
/// Base class for the app's modal dialogs: dark chrome, Bg background,
/// fixed size, centered on owner. Every dialog wires IsDefault on the safe
/// action and IsCancel on dismiss - Enter/Esc always work; danger is never
/// the default.
/// </summary>
public class ThemedDialog : Window
{
    public ThemedDialog()
    {
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        Background = (Brush)Application.Current.FindResource("Bg");
        Helpers.DarkChrome.Apply(this);
    }
}
