namespace DeafDirectionalHelper.Audio;

/// <summary>
/// How the app decides which audio endpoint to read levels from.
/// All modes read output meters only (WASAPI) - never game memory.
/// </summary>
public enum CaptureMode
{
    /// <summary>Read the endpoint hosting the game's audio session, wherever it plays.</summary>
    FollowGame,

    /// <summary>Read the Windows default render device; follows default-device changes.</summary>
    WindowsDefault,

    /// <summary>Read one specific device (legacy behavior: configured match, else first 8-channel, else default).</summary>
    FixedDevice
}
