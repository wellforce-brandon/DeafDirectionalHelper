using System;
using System.Linq;
using DeafDirectionalHelper.Settings;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace DeafDirectionalHelper.Audio;

/// <summary>
/// Owns which MMDevice the app reads levels from, per the configured CaptureMode:
///
/// - FixedDevice:    legacy behavior (configured match, else first 8-channel, else default)
/// - WindowsDefault: default render endpoint, re-selected on OnDefaultDeviceChanged
/// - FollowGame:     the endpoint hosting the tracked game's audio session
///                   (full endpoint channel meter - better for surround than the
///                   session's own meter); falls back to WindowsDefault behavior
/// </summary>
public sealed class EndpointSelector : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator;
    private readonly SessionLocator _sessions;
    private readonly NotificationClient _notificationClient;

    private MMDevice? _device;
    private string? _deviceId;
    private string? _deviceName; // cached: FriendlyName is a slow property-store COM read
    private volatile bool _devicesDirty = true; // force initial selection
    private bool _disposed;

    /// <summary>
    /// Process name (without extension) of the game to follow in FollowGame mode.
    /// Set by process/game detection; null means "no specific game tracked".
    /// </summary>
    public string? TrackedProcessName { get; set; }

    public MMDevice? Current => _device;
    public string? CurrentDeviceId => _deviceId;
    public string CurrentDeviceName => _deviceName ?? "None";

    /// <summary>Raised after the selected endpoint actually changed.</summary>
    public event EventHandler? DeviceChanged;

    public EndpointSelector(SessionLocator sessions)
    {
        _sessions = sessions;
        _enumerator = new MMDeviceEnumerator();
        _notificationClient = new NotificationClient(this);

        try
        {
            _enumerator.RegisterEndpointNotificationCallback(_notificationClient);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Endpoint notifications unavailable: {ex.Message}");
        }

        SettingsManager.Instance.SettingsChanged += (_, _) => Invalidate();
    }

    /// <summary>Forces re-selection on the next EnsureSelected call.</summary>
    public void Invalidate()
    {
        _devicesDirty = true;
    }

    /// <summary>Drops the current device (read failure) and forces re-selection.</summary>
    public void MarkDeviceLost()
    {
        _device = null;
        _deviceId = null;
        _deviceName = null;
        _devicesDirty = true;
    }

    /// <summary>
    /// Called from the 50 ms poll. Cheap when nothing changed: FollowGame checks
    /// the SessionLocator snapshot (in-memory), everything else waits for the
    /// dirty flag set by settings changes or device notifications.
    /// </summary>
    public void EnsureSelected()
    {
        var mode = SettingsManager.Instance.Settings.General.CaptureMode;

        if (mode == CaptureMode.FollowGame)
        {
            var desiredId = FindGameEndpointId();
            if (desiredId != null)
            {
                if (desiredId != _deviceId)
                    SwitchTo(desiredId, "follow-game");
                return;
            }
            // No audible game session: fall through to default-device behavior.
        }

        if (!_devicesDirty && _device != null)
            return;

        _devicesDirty = false;

        try
        {
            var selected = mode == CaptureMode.FixedDevice
                ? SelectFixedDevice()
                : _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);

            if (selected != null && selected.ID != _deviceId)
            {
                _device = selected;
                _deviceId = selected.ID;
                _deviceName = selected.FriendlyName;
                Console.WriteLine($"Capture endpoint: {_deviceName} ({selected.AudioMeterInformation.PeakValues.Count} channels, mode {mode})");
                DeviceChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Endpoint selection failed: {ex.Message}");
        }
    }

    private string? FindGameEndpointId()
    {
        // Prefer the explicitly tracked game.
        if (TrackedProcessName != null)
        {
            var tracked = _sessions.TryGetEndpointForProcess(TrackedProcessName);
            if (tracked != null) return tracked.DeviceId;
        }

        // Sticky fallback: keep the current endpoint while it still hosts audible
        // audio, so two apps on different devices don't cause 2 s ping-ponging.
        var audible = _sessions.GetAudibleSessions();
        if (_deviceId != null && audible.Any(s => s.DeviceId == _deviceId))
            return _deviceId;

        // Else follow the loudest audible session so a brand-new game lights
        // the overlay with zero configuration. Also the practical fallback for
        // anti-cheat-wrapped games, where the profile's process (the launcher)
        // never matches the child process that actually owns the audio session.
        return _sessions.GetLoudestSession()?.DeviceId;
    }

    private void SwitchTo(string deviceId, string reason)
    {
        try
        {
            var device = _enumerator.GetDevice(deviceId);
            _device = device;
            _deviceId = deviceId;
            _deviceName = device.FriendlyName;
            Console.WriteLine($"Capture endpoint: {_deviceName} ({device.AudioMeterInformation.PeakValues.Count} channels, {reason})");
            DeviceChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Endpoint switch failed: {ex.Message}");
        }
    }

    /// <summary>Legacy selection: configured match, else first 8-channel device, else default.</summary>
    private MMDevice? SelectFixedDevice()
    {
        var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        var preferred = SettingsManager.Instance.Settings.General.AudioDevice;

        if (!string.IsNullOrEmpty(preferred))
        {
            var match = devices.FirstOrDefault(d => d.FriendlyName.Contains(preferred));
            if (match != null) return match;
        }

        var eightChannel = devices.FirstOrDefault(d => d.AudioMeterInformation.PeakValues.Count == 8);
        if (eightChannel != null) return eightChannel;

        return _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _enumerator.UnregisterEndpointNotificationCallback(_notificationClient);
        }
        catch
        {
            // Enumerator may already be gone at shutdown.
        }
        _enumerator.Dispose();
    }

    /// <summary>
    /// IMMNotificationClient callbacks arrive on COM threads; they only set the
    /// dirty flag, and re-selection happens on the next poll tick.
    /// </summary>
    private sealed class NotificationClient : IMMNotificationClient
    {
        private readonly EndpointSelector _owner;

        public NotificationClient(EndpointSelector owner) => _owner = owner;

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow == DataFlow.Render)
                _owner.Invalidate();
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => _owner.Invalidate();
        public void OnDeviceAdded(string pwstrDeviceId) => _owner.Invalidate();
        public void OnDeviceRemoved(string deviceId) => _owner.Invalidate();
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }
}
