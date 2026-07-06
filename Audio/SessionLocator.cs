using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using DeafDirectionalHelper.Settings;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace DeafDirectionalHelper.Audio;

/// <summary>
/// One audible audio session observed on a render endpoint.
/// Only exposes which process owns a session and its output meter -
/// the same output-side data the Windows volume mixer shows.
/// </summary>
public sealed record AudibleSession(
    string ProcessName,
    uint Pid,
    string DeviceId,
    string DeviceFriendlyName,
    int DeviceChannelCount,
    float SessionPeak);

/// <summary>
/// Polls all active render endpoints every 2 seconds and maps audio sessions
/// to the processes that own them (IAudioSessionControl2.GetProcessId via
/// NAudio's AudioSessionControl.GetProcessID). Lets the rest of the app answer
/// "which device is this game's audio actually playing on?".
/// </summary>
public sealed class SessionLocator : IDisposable
{
    private const int PollIntervalMs = 2000;

    // Below this, a session is treated as silent for discovery purposes.
    // Real game audio has been observed as low as ~0.005 (quiet ambience,
    // background-audio settings, etc.) - keep this well under that, while
    // still excluding true silence (consistently exactly 0 on idle devices).
    private const float MinPeakToReport = 0.003f;

    private readonly Timer _pollTimer;
    private readonly Dictionary<uint, string> _pidNameCache = new();
    // FriendlyName is a slow property-store COM read and channel count doesn't
    // change while a device is active, so cache both per device ID.
    private readonly Dictionary<string, (string Name, int Channels)> _deviceInfoCache = new();
    private volatile List<AudibleSession> _snapshot = new();
    private int _polling;
    private bool _disposed;

    public SessionLocator()
    {
        _pollTimer = new Timer(Poll, null, 0, PollIntervalMs);
    }

    /// <summary>Latest snapshot of audible sessions (peak >= 0.01) across all render endpoints.</summary>
    public IReadOnlyList<AudibleSession> GetAudibleSessions() => _snapshot;

    /// <summary>
    /// Finds the endpoint hosting an audible session owned by the given process
    /// (name compared without extension, case-insensitive).
    /// </summary>
    public AudibleSession? TryGetEndpointForProcess(string processName)
    {
        var name = TrimExe(processName);
        return _snapshot.FirstOrDefault(s => string.Equals(s.ProcessName, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The loudest audible session across all endpoints, if any.</summary>
    public AudibleSession? GetLoudestSession()
    {
        return _snapshot.OrderByDescending(s => s.SessionPeak).FirstOrDefault();
    }

    private void Poll(object? state)
    {
        if (_disposed) return;
        if (Interlocked.Exchange(ref _polling, 1) == 1) return; // skip overlapping polls

        try
        {
            var result = new List<AudibleSession>();

            // Excluded programs are invisible as audio sources: they never
            // appear in the snapshot, so follow-game capture, the unknown-game
            // offer and SignalDoctor all skip them at this single choke point.
            var excluded = new HashSet<string>(
                SettingsManager.Instance.Settings.ExcludedPrograms.Select(TrimExe),
                StringComparer.OrdinalIgnoreCase);

            // Fresh enumeration each poll: avoids NAudio's cached session lists
            // and picks up devices/sessions created since the last poll.
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                try
                {
                    CollectSessions(device, excluded, result);
                }
                catch
                {
                    // Device may have vanished mid-poll; skip it.
                }
            }

            _snapshot = result;
        }
        catch
        {
            // Keep the previous snapshot on enumeration failure.
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
        }
    }

    private void CollectSessions(MMDevice device, HashSet<string> excluded, List<AudibleSession> result)
    {
        var sessions = device.AudioSessionManager.Sessions;
        if (sessions == null) return;

        var deviceId = device.ID;
        if (!_deviceInfoCache.TryGetValue(deviceId, out var info))
        {
            info = (device.FriendlyName, device.AudioMeterInformation.PeakValues.Count);
            if (_deviceInfoCache.Count > 64)
                _deviceInfoCache.Clear();
            _deviceInfoCache[deviceId] = info;
        }
        var (deviceName, channels) = info;

        for (int i = 0; i < sessions.Count; i++)
        {
            try
            {
                var session = sessions[i];
                if (session.State != AudioSessionState.AudioSessionStateActive) continue;
                if (session.IsSystemSoundsSession) continue;

                var pid = session.GetProcessID;
                if (pid == 0) continue;

                var name = ResolveProcessName(pid);
                if (name == null) continue;
                if (excluded.Contains(name)) continue;

                // Per-session meter is a COM QI on the session control; if it is
                // flaky we skip the session rather than crash (endpoint meters
                // remain the primary signal path).
                float peak;
                try
                {
                    peak = session.AudioMeterInformation.MasterPeakValue;
                }
                catch
                {
                    continue;
                }

                if (peak < MinPeakToReport) continue;

                result.Add(new AudibleSession(name, pid, deviceId, deviceName, channels, peak));
            }
            catch
            {
                // Session may have ended mid-iteration; skip it.
            }
        }
    }

    private string? ResolveProcessName(uint pid)
    {
        if (_pidNameCache.TryGetValue(pid, out var cached))
            return cached;

        try
        {
            using var process = Process.GetProcessById((int)pid);
            var name = process.ProcessName;

            // Bound the cache; PIDs get recycled but names are re-resolved after a clear.
            if (_pidNameCache.Count > 256)
                _pidNameCache.Clear();

            _pidNameCache[pid] = name;
            return name;
        }
        catch
        {
            return null; // Process exited or access denied.
        }
    }

    private static string TrimExe(string processName)
    {
        return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pollTimer.Dispose();
    }
}
