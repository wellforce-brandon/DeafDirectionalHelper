using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace DeafDirectionalHelper.Audio;

public sealed class MismatchEventArgs : EventArgs
{
    public required string GameProcess { get; init; }
    public required string GameDeviceId { get; init; }
    public required string GameDeviceName { get; init; }
    public required int GameDeviceChannels { get; init; }
    public required string SelectedDeviceId { get; init; }
    public required string SelectedDeviceName { get; init; }
    public required int SilentSeconds { get; init; }
}

/// <summary>
/// Detects "overlay armed but silent": the selected endpoint has been flat
/// (peak &lt; 0.005) for 10 s while a tracked or foreground game is audibly
/// playing to a different endpoint. Fires once per game launch; resets when
/// the selected device changes.
/// </summary>
public sealed class SignalDoctor
{
    private const float SilentThreshold = 0.005f;
    private const float GameAudibleThreshold = 0.05f;
    private const int MismatchAfterMs = 10_000;

    private readonly SessionLocator _sessions;
    private readonly Stopwatch _silence = new();
    private readonly HashSet<string> _firedFor = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _dismissed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns true when the process name belongs to a profiled/tracked game.</summary>
    public Func<string, bool>? IsTrackedProcess { get; set; }

    /// <summary>Raised on the thread that called Tick (the UI dispatcher in practice).</summary>
    public event EventHandler<MismatchEventArgs>? MismatchDetected;

    public SignalDoctor(SessionLocator sessions, EndpointSelector endpoint)
    {
        _sessions = sessions;
        endpoint.DeviceChanged += (_, _) => Reset();
    }

    /// <summary>Feed each audio poll (200 ms) with the selected endpoint's peak.</summary>
    public void Tick(float selectedPeak, string? selectedDeviceId, string selectedDeviceName)
    {
        if (selectedDeviceId == null)
            return;

        if (selectedPeak >= SilentThreshold)
        {
            _silence.Reset();
            return;
        }

        if (!_silence.IsRunning)
            _silence.Restart();

        if (_silence.ElapsedMilliseconds < MismatchAfterMs)
            return;

        // Allow re-triggering for a game only after its session goes away (relaunch).
        var audible = _sessions.GetAudibleSessions();
        _firedFor.RemoveWhere(name => !audible.Any(s =>
            string.Equals(s.ProcessName, name, StringComparison.OrdinalIgnoreCase)));

        var candidate = audible.FirstOrDefault(s =>
            s.DeviceId != selectedDeviceId &&
            s.SessionPeak > GameAudibleThreshold &&
            !_firedFor.Contains(s.ProcessName) &&
            !_dismissed.Contains(s.ProcessName) &&
            IsGameLike(s));

        if (candidate == null)
            return;

        _firedFor.Add(candidate.ProcessName);

        MismatchDetected?.Invoke(this, new MismatchEventArgs
        {
            GameProcess = candidate.ProcessName,
            GameDeviceId = candidate.DeviceId,
            GameDeviceName = candidate.DeviceFriendlyName,
            GameDeviceChannels = candidate.DeviceChannelCount,
            SelectedDeviceId = selectedDeviceId,
            SelectedDeviceName = selectedDeviceName,
            SilentSeconds = (int)(_silence.ElapsedMilliseconds / 1000)
        });
    }

    /// <summary>
    /// Permanently (for this app run) suppress re-trigger for a game after the
    /// user saw the doctor for it, even if its session drops and comes back.
    /// </summary>
    public void Suppress(string processName) => _dismissed.Add(processName);

    private void Reset()
    {
        _silence.Reset();
    }

    private bool IsGameLike(AudibleSession session)
    {
        if (IsTrackedProcess?.Invoke(session.ProcessName) == true)
            return true;

        return session.Pid == GetForegroundPid();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    private static uint GetForegroundPid()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return 0;
        GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }
}
