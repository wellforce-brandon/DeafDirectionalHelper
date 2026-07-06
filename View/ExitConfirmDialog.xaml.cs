using System.Windows;

namespace DeafDirectionalHelper.View;

/// <summary>
/// Exit confirmation (design 2j). Stay open is the default (Enter) and the
/// cancel action (Esc); the danger fill is never the default.
/// DialogResult true = really exit.
/// </summary>
public partial class ExitConfirmDialog : ThemedDialog
{
    public ExitConfirmDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => StayOpenButton.Focus(); // focus ring visible on open
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
