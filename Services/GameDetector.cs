using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using DeafDirectionalHelper.Audio;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.Services;

/// <summary>
/// Detects games two ways (evolves ProcessMonitor):
///
/// - Known games: a running process matches a profile's ProcessName
///   (same semantics as the old ProcessMonitor, so silent games still switch).
/// - Unknown games (plan D7): a process has an active, audible audio session
///   AND owns the foreground window with fullscreen-ish bounds (>= 95% of its
///   monitor), is not ignored, has no profile, and wasn't offered this run.
///
/// Only observes process lists, audio session meters, and window bounds -
/// never game memory or content.
/// </summary>
public sealed class GameDetector : IDisposable
{
    private const int PollIntervalMs = 2000;
    private const float AudibleThreshold = 0.05f;
    private const double FullscreenishFraction = 0.95;

    private readonly SessionLocator _sessions;
    private readonly Timer _pollTimer;
    private readonly object _lock = new();
    private readonly HashSet<string> _offeredThisRun = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _watchedProcesses = new(StringComparer.OrdinalIgnoreCase);
    private string? _lastActiveProcess;
    private bool _disposed;

    /// <summary>
    /// Fired when the active profiled process changes (name without extension,
    /// or null when no watched process is running). Same contract as the old
    /// ProcessMonitor.ActiveProcessChanged.
    /// </summary>
    public event EventHandler<string?>? ActiveProcessChanged;

    /// <summary>Fired once per run per process when an unprofiled game is detected making sound.</summary>
    public event EventHandler<UnknownGameEventArgs>? UnknownGameDetected;

    public bool IsMonitoring { get; private set; }

    public GameDetector(SessionLocator sessions)
    {
        _sessions = sessions;
        _pollTimer = new Timer(Poll, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void UpdateWatchList(IEnumerable<string> processNames)
    {
        lock (_lock)
        {
            _watchedProcesses = new HashSet<string>(processNames, StringComparer.OrdinalIgnoreCase);
        }
    }

    public void Start()
    {
        if (IsMonitoring) return;
        IsMonitoring = true;
        _pollTimer.Change(0, PollIntervalMs);
    }

    public void Stop()
    {
        if (!IsMonitoring) return;
        IsMonitoring = false;
        _pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void Poll(object? state)
    {
        if (_disposed) return;

        try
        {
            PollKnownGames();
            PollUnknownGames();
        }
        catch
        {
            // Never let a poll error kill the timer.
        }
    }

    private void PollKnownGames()
    {
        string? currentActive = null;

        HashSet<string> watched;
        lock (_lock) watched = _watchedProcesses;

        if (watched.Count > 0)
        {
            try
            {
                foreach (var process in Process.GetProcesses())
                {
                    try
                    {
                        if (watched.Contains(process.ProcessName))
                        {
                            currentActive = process.ProcessName;
                            break;
                        }
                    }
                    catch
                    {
                        // Some processes deny access.
                    }
                }
            }
            catch
            {
                // Process enumeration failed; treat as no change.
                return;
            }
        }

        if (currentActive != _lastActiveProcess)
        {
            _lastActiveProcess = currentActive;
            ActiveProcessChanged?.Invoke(this, currentActive);
        }
    }

    private void PollUnknownGames()
    {
        var settings = SettingsManager.Instance.Settings;
        if (!settings.OfferProfileForUnknownGames)
            return;

        var foregroundPid = GetForegroundPid();
        if (foregroundPid == 0)
            return;

        var candidate = _sessions.GetAudibleSessions()
            .FirstOrDefault(s => s.Pid == foregroundPid && s.SessionPeak > AudibleThreshold);
        if (candidate == null)
            return;

        if (_offeredThisRun.Contains(candidate.ProcessName))
            return;

        if (settings.IgnoredGames.Any(g => string.Equals(g, candidate.ProcessName, StringComparison.OrdinalIgnoreCase)))
            return;

        // Excluded programs never appear in the session snapshot, but keep an
        // explicit check in case the filtering point ever moves.
        if (settings.ExcludedPrograms.Any(g => string.Equals(g, candidate.ProcessName, StringComparison.OrdinalIgnoreCase)))
            return;

        if (ProfileManager.Instance.GetProfileForProcess(candidate.ProcessName) != null)
            return;

        if (!IsForegroundWindowFullscreenish())
            return;

        _offeredThisRun.Add(candidate.ProcessName);
        UnknownGameDetected?.Invoke(this, new UnknownGameEventArgs
        {
            ProcessName = candidate.ProcessName,
            ExePath = TryGetExePath(candidate.Pid)
        });
    }

    private static string? TryGetExePath(uint pid)
    {
        try
        {
            // MainModule throws on elevated/protected processes.
            using var process = Process.GetProcessById((int)pid);
            return process.MainModule?.FileName;
        }
        catch
        {
            // Anti-cheat games (EAC etc.) run elevated, so MainModule is
            // access-denied - but QueryFullProcessImageName works with
            // limited query rights (verified against a live EAC process).
            return TryQueryProcessImageName(pid);
        }
    }

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, uint pid);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint flags,
        System.Text.StringBuilder exeName, ref uint size);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private static string? TryQueryProcessImageName(uint pid)
    {
        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero) return null;
        try
        {
            var buffer = new System.Text.StringBuilder(1024);
            var size = (uint)buffer.Capacity;
            return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString() : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    // --- Win32: foreground window + fullscreen-ish check ---

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    private static uint GetForegroundPid()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return 0;
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    private static bool IsForegroundWindowFullscreenish()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;
        if (!GetWindowRect(hwnd, out var rect)) return false;

        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info)) return false;

        double windowArea = Math.Max(0, rect.Right - rect.Left) * (double)Math.Max(0, rect.Bottom - rect.Top);
        double monitorArea = (info.rcMonitor.Right - info.rcMonitor.Left) * (double)(info.rcMonitor.Bottom - info.rcMonitor.Top);
        if (monitorArea <= 0) return false;

        return windowArea / monitorArea >= FullscreenishFraction;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pollTimer.Dispose();
    }
}

public sealed class UnknownGameEventArgs : EventArgs
{
    public required string ProcessName { get; init; }
    public string? ExePath { get; init; }
}
