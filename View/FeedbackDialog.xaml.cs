using System.Windows;
using System.Windows.Controls;
using DeafDirectionalHelper.Audio;
using DeafDirectionalHelper.Services;

namespace DeafDirectionalHelper.View;

/// <summary>
/// The feedback dialog (adaptive install of the BP feedback-widget template).
/// No server exists in this app, so submission never holds a credential:
/// Send copies the report to the clipboard and opens GitHub's prefilled
/// "new issue" page, where the user's own login is the authentication.
/// Does not show a feedback trigger of its own (no recursive entry point).
/// </summary>
public partial class FeedbackDialog : ThemedDialog
{
    private const int MinMessageLength = 10;

    private readonly Speakers _speakers;
    private FeedbackReport? _lastReport;
    private bool _sent;

    public FeedbackDialog(Speakers speakers)
    {
        InitializeComponent();
        _speakers = speakers;

        CategoryCombo.SelectedIndex = 0;
        SeveritySegmented.SelectedIndex = 1; // Medium
        Loaded += (_, _) => MessageTextBox.Focus();
    }

    private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var length = MessageTextBox.Text.Length;
        CounterText.Text = $"{length} / 2000";
        SendButton.IsEnabled = MessageTextBox.Text.Trim().Length >= MinMessageLength;
    }

    private void Send_Click(object sender, RoutedEventArgs e)
    {
        var category = System.Enum.Parse<FeedbackCategory>(
            (string)((ComboBoxItem)CategoryCombo.SelectedItem).Tag);
        var severity = System.Enum.Parse<FeedbackSeverity>(
            (string)((ListBoxItem)SeveritySegmented.SelectedItem).Tag);

        var report = FeedbackReportBuilder.Build(category, severity, MessageTextBox.Text, _speakers);
        _lastReport = report;

        var result = FeedbackSender.Send(report);
        ShowStatus(result);
    }

    private void ShowStatus(FeedbackSendResult result)
    {
        _sent = true;
        FormPanel.Visibility = Visibility.Collapsed;
        StatusPanel.Visibility = Visibility.Visible;
        SendButton.Visibility = Visibility.Collapsed;
        CancelButton.Content = "Close";

        switch (result)
        {
            case FeedbackSendResult.OpenedBrowser:
                StatusTitle.Text = "Report copied — GitHub is open";
                StatusDetail.Text = "Paste it (Ctrl+V) into the issue body and click Submit new issue.";
                CopyAgainButton.Visibility = Visibility.Visible;
                break;

            case FeedbackSendResult.ClipboardOnly:
                StatusTitle.Text = "Report copied to clipboard";
                StatusDetail.Text = "Couldn't open your browser automatically. Paste the report into a new issue at " +
                                     "github.com/wellforce-brandon/DeafDirectionalHelper/issues/new.";
                CopyAgainButton.Visibility = Visibility.Visible;
                break;

            default:
                StatusTitle.Text = "Couldn't copy automatically";
                StatusDetail.Text = "Select the text below and copy it manually, then paste it into a new issue at " +
                                     "github.com/wellforce-brandon/DeafDirectionalHelper/issues/new.";
                FallbackTextBox.Text = _lastReport?.Body ?? "";
                FallbackTextBox.Visibility = Visibility.Visible;
                break;
        }
    }

    private void CopyAgain_Click(object sender, RoutedEventArgs e)
    {
        if (_lastReport != null)
            FeedbackSender.Send(_lastReport); // re-copy + re-open, same as the first attempt
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = _sent;
        Close();
    }
}
