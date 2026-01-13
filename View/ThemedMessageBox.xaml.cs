using System.Windows;

namespace DeafDirectionalHelper.View;

public enum ThemedMessageBoxButton
{
    OK,
    YesNo,
    YesNoCancel
}

public enum ThemedMessageBoxResult
{
    None,
    OK,
    Yes,
    No,
    Cancel
}

public partial class ThemedMessageBox : Window
{
    public ThemedMessageBoxResult Result { get; private set; } = ThemedMessageBoxResult.None;

    private ThemedMessageBox(string message, string title, ThemedMessageBoxButton buttons)
    {
        InitializeComponent();
        MessageText.Text = message;
        Title = title;

        // Show appropriate buttons
        switch (buttons)
        {
            case ThemedMessageBoxButton.OK:
                OkButton.Visibility = Visibility.Visible;
                break;
            case ThemedMessageBoxButton.YesNo:
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
                break;
            case ThemedMessageBoxButton.YesNoCancel:
                YesButton.Visibility = Visibility.Visible;
                NoButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
                break;
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        Result = ThemedMessageBoxResult.OK;
        DialogResult = true;
        Close();
    }

    private void Yes_Click(object sender, RoutedEventArgs e)
    {
        Result = ThemedMessageBoxResult.Yes;
        DialogResult = true;
        Close();
    }

    private void No_Click(object sender, RoutedEventArgs e)
    {
        Result = ThemedMessageBoxResult.No;
        DialogResult = false;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Result = ThemedMessageBoxResult.Cancel;
        DialogResult = null;
        Close();
    }

    /// <summary>
    /// Shows a themed message box with OK button.
    /// </summary>
    public static void Show(string message, string title = "Message", Window? owner = null)
    {
        var dialog = new ThemedMessageBox(message, title, ThemedMessageBoxButton.OK);
        if (owner != null)
        {
            dialog.Owner = owner;
        }
        dialog.ShowDialog();
    }

    /// <summary>
    /// Shows a themed confirmation dialog with Yes/No buttons.
    /// Returns true if Yes was clicked, false otherwise.
    /// </summary>
    public static bool ShowYesNo(string message, string title = "Confirm", Window? owner = null)
    {
        var dialog = new ThemedMessageBox(message, title, ThemedMessageBoxButton.YesNo);
        if (owner != null)
        {
            dialog.Owner = owner;
        }
        dialog.ShowDialog();
        return dialog.Result == ThemedMessageBoxResult.Yes;
    }

    /// <summary>
    /// Shows a themed confirmation dialog with Yes/No/Cancel buttons.
    /// Returns the result enum.
    /// </summary>
    public static ThemedMessageBoxResult ShowYesNoCancel(string message, string title = "Confirm", Window? owner = null)
    {
        var dialog = new ThemedMessageBox(message, title, ThemedMessageBoxButton.YesNoCancel);
        if (owner != null)
        {
            dialog.Owner = owner;
        }
        dialog.ShowDialog();
        return dialog.Result;
    }
}
