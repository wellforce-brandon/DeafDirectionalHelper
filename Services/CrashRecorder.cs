using System;
using System.Windows;
using System.Windows.Threading;

namespace DeafDirectionalHelper.Services;

/// <summary>
/// Records the most recent unhandled exception for this session so a
/// feedback report filed shortly after a crash can include it. In-memory
/// only, single slot (a ring buffer of one) - this app has no server to
/// persist telemetry to, and a session rarely sees more than one crash
/// before the user either recovers or restarts.
/// </summary>
public static class CrashRecorder
{
    public static string? LastException { get; private set; }

    /// <summary>Call once from App.OnStartup, before any window is created.</summary>
    public static void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Record(ex);
        };

        if (Application.Current != null)
        {
            Application.Current.DispatcherUnhandledException += (_, e) =>
            {
                Record(e.Exception);
                // Don't set e.Handled: this hook only records for feedback,
                // it must not change the app's existing crash behavior.
            };
        }
    }

    private static void Record(Exception ex)
    {
        try
        {
            LastException = $"{ex.GetType().Name}: {ex.Message}\n{FirstFrames(ex.StackTrace, 6)}";
        }
        catch
        {
            // Recording the crash must never itself throw.
        }
    }

    private static string FirstFrames(string? stackTrace, int count)
    {
        if (string.IsNullOrEmpty(stackTrace)) return "(no stack trace)";
        var lines = stackTrace.Split('\n');
        return string.Join('\n', lines[..Math.Min(count, lines.Length)]);
    }
}
