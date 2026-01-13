using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace DeafDirectionalHelper.Services;

/// <summary>
/// Monitors running processes to detect when profiled applications are active.
/// </summary>
public class ProcessMonitor : IDisposable
{
    private readonly Timer _pollTimer;
    private readonly object _lock = new();
    private HashSet<string> _watchedProcesses = new(StringComparer.OrdinalIgnoreCase);
    private string? _lastActiveProcess;
    private bool _disposed;

    /// <summary>
    /// Fired when the active watched process changes.
    /// Argument is the process name (without extension) or null if no watched process is running.
    /// </summary>
    public event EventHandler<string?>? ActiveProcessChanged;

    /// <summary>
    /// The polling interval in milliseconds.
    /// </summary>
    public int PollIntervalMs { get; set; } = 2000;

    /// <summary>
    /// Whether monitoring is currently active.
    /// </summary>
    public bool IsMonitoring { get; private set; }

    public ProcessMonitor()
    {
        _pollTimer = new Timer(PollProcesses, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Updates the list of process names to watch for.
    /// </summary>
    public void UpdateWatchList(IEnumerable<string> processNames)
    {
        lock (_lock)
        {
            _watchedProcesses = new HashSet<string>(processNames, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Starts monitoring for processes.
    /// </summary>
    public void Start()
    {
        if (IsMonitoring) return;
        IsMonitoring = true;
        _pollTimer.Change(0, PollIntervalMs);
    }

    /// <summary>
    /// Stops monitoring for processes.
    /// </summary>
    public void Stop()
    {
        if (!IsMonitoring) return;
        IsMonitoring = false;
        _pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>
    /// Gets the currently active watched process, or null if none.
    /// </summary>
    public string? GetCurrentActiveProcess()
    {
        lock (_lock)
        {
            if (_watchedProcesses.Count == 0) return null;

            try
            {
                var processes = Process.GetProcesses();
                foreach (var process in processes)
                {
                    try
                    {
                        var processName = process.ProcessName;
                        if (_watchedProcesses.Contains(processName))
                        {
                            return processName;
                        }
                    }
                    catch
                    {
                        // Some processes may not allow access
                    }
                }
            }
            catch
            {
                // Process enumeration failed
            }

            return null;
        }
    }

    private void PollProcesses(object? state)
    {
        if (_disposed) return;

        try
        {
            var currentActive = GetCurrentActiveProcess();

            // Only fire event if the active process changed
            if (currentActive != _lastActiveProcess)
            {
                _lastActiveProcess = currentActive;
                ActiveProcessChanged?.Invoke(this, currentActive);
            }
        }
        catch
        {
            // Ignore polling errors
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pollTimer.Dispose();
    }
}
