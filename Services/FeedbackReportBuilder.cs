using System;
using System.IO;
using System.Linq;
using System.Text;
using DeafDirectionalHelper.Audio;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.Services;

public enum FeedbackCategory
{
    Bug,
    CaptureOrOverlay,
    FeatureRequest,
    Accessibility,
    Other
}

public enum FeedbackSeverity
{
    Low,
    Medium,
    High
}

public sealed record FeedbackReport(
    string Title,
    string Body,
    string[] Labels);

/// <summary>
/// Composes a feedback report as Markdown: the user's own words first, then
/// an auto-attached "Diagnostics" section (plan functional requirement 3 -
/// the user never types this). Everything that could carry a token/email is
/// redacted before it's included, since this ships to a public repo (no
/// server exists here to keep a private triage destination behind).
/// </summary>
public static class FeedbackReportBuilder
{
    private const int LogTailLines = 25;
    private const int MaxBodyChars = 8000;

    public static FeedbackReport Build(FeedbackCategory category, FeedbackSeverity severity,
        string message, Speakers speakers)
    {
        var settings = SettingsManager.Instance.Settings;
        var profile = ProfileManager.Instance.ActiveProfile;

        var body = new StringBuilder();
        body.AppendLine("## Report");
        body.AppendLine();
        body.AppendLine(message.Trim());
        body.AppendLine();
        body.AppendLine("## Diagnostics");
        body.AppendLine();
        body.AppendLine("| | |");
        body.AppendLine("|---|---|");
        body.AppendLine($"| App version | {AppVersion.Version} |");
        body.AppendLine($"| OS | {Environment.OSVersion.VersionString} |");
        body.AppendLine($"| Capture mode | {settings.General.CaptureMode} |");
        body.AppendLine($"| Output device | {speakers.CurrentChannelCount}-channel |");
        body.AppendLine($"| Active profile | {profile.Name} |");
        body.AppendLine($"| Overlay style | {settings.Bars.OverlayStyle}" +
                         $"{(settings.Bars.PairWithSideBars ? " + side bars" : "")} |");
        body.AppendLine($"| Color scale | {settings.Bars.ColorScale} |");

        var lastException = CrashRecorder.LastException;
        if (lastException != null)
        {
            body.AppendLine();
            body.AppendLine("### Last error this session");
            body.AppendLine("```");
            body.AppendLine(TextRedactor.Redact(lastException));
            body.AppendLine("```");
        }

        var logTail = TryReadLogTail();
        if (logTail != null)
        {
            body.AppendLine();
            body.AppendLine($"### Audio log (last {LogTailLines} lines)");
            body.AppendLine("```");
            body.AppendLine(TextRedactor.Redact(logTail));
            body.AppendLine("```");
        }

        var title = $"[{CategoryDisplayName(category)}] {Truncate(message.Trim(), 80)}";
        var labels = new[] { CategoryLabel(category), SeverityLabel(severity) };

        return new FeedbackReport(title, TextRedactor.Truncate(body.ToString(), MaxBodyChars), labels);
    }

    private static string? TryReadLogTail()
    {
        if (!SettingsManager.Instance.Settings.General.EnableAudioLogging)
            return null;

        try
        {
            var path = Path.Combine(AudioEventLogger.Instance.GetLogDirectory(), "audio_events.log");
            if (!File.Exists(path)) return null;

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var lines = new System.Collections.Generic.List<string>();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                lines.Add(line);
                if (lines.Count > LogTailLines) lines.RemoveAt(0);
            }
            return lines.Count > 0 ? string.Join('\n', lines) : null;
        }
        catch
        {
            return null; // Diagnostics are best-effort; never block a submission.
        }
    }

    public static string CategoryDisplayName(FeedbackCategory category) => category switch
    {
        FeedbackCategory.Bug => "Bug",
        FeedbackCategory.CaptureOrOverlay => "Capture/overlay issue",
        FeedbackCategory.FeatureRequest => "Feature request",
        FeedbackCategory.Accessibility => "Accessibility",
        _ => "Other"
    };

    private static string CategoryLabel(FeedbackCategory category) => category switch
    {
        FeedbackCategory.Bug => "bug",
        FeedbackCategory.CaptureOrOverlay => "capture",
        FeedbackCategory.FeatureRequest => "enhancement",
        FeedbackCategory.Accessibility => "accessibility",
        _ => "other"
    };

    private static string SeverityLabel(FeedbackSeverity severity) => severity switch
    {
        FeedbackSeverity.Low => "severity:low",
        FeedbackSeverity.Medium => "severity:medium",
        _ => "severity:high"
    };

    private static string Truncate(string value, int max)
    {
        return value.Length <= max ? value : value[..(max - 1)] + "…";
    }
}
