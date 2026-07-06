using System;
using System.Diagnostics;
using System.Net;
using System.Windows;

namespace DeafDirectionalHelper.Services;

public enum FeedbackSendResult
{
    /// <summary>Body copied to clipboard and the browser opened to a prefilled issue form.</summary>
    OpenedBrowser,
    /// <summary>Body copied to clipboard, but the browser could not be opened; give the user the link.</summary>
    ClipboardOnly,
    /// <summary>Nothing could be done automatically; show the report so the user can copy it by hand.</summary>
    Failed
}

/// <summary>
/// Hands a feedback report off to GitHub without ever holding a credential in
/// this app: the body goes on the clipboard, and the browser opens to a
/// prefilled "new issue" page (title + labels only - short enough to be a
/// safe URL; the body is pasted by the user, sidestepping URL length limits).
/// The user's own GitHub login is the authentication - there is no server
/// here to authenticate to, and no PAT this shipped .exe could leak.
/// </summary>
public static class FeedbackSender
{
    private const string TriageRepo = "wellforce-brandon/DeafDirectionalHelper";

    public static FeedbackSendResult Send(FeedbackReport report)
    {
        var clipboardOk = TrySetClipboard(report.Body);

        var url = BuildIssueUrl(report);
        var browserOk = TryOpenBrowser(url);

        if (browserOk)
            return FeedbackSendResult.OpenedBrowser;
        return clipboardOk ? FeedbackSendResult.ClipboardOnly : FeedbackSendResult.Failed;
    }

    private static string BuildIssueUrl(FeedbackReport report)
    {
        var labels = string.Join(',', report.Labels);
        return $"https://github.com/{TriageRepo}/issues/new" +
               $"?title={WebUtility.UrlEncode(report.Title)}" +
               $"&labels={WebUtility.UrlEncode(labels)}";
    }

    private static bool TrySetClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch
        {
            return false; // Clipboard can be held by another process; never let that break the flow.
        }
    }

    private static bool TryOpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            return true;
        }
        catch
        {
            return false; // No default browser association, etc. - fall back to clipboard-only.
        }
    }
}
