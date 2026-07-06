using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DeafDirectionalHelper.Settings;

public class AppSettings
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 3;

    [JsonPropertyName("ignoredGames")]
    public List<string> IgnoredGames { get; set; } = new();

    // Process names (no extension). Excluded programs never trigger the
    // unknown-game offer and their audio sessions are invisible to
    // follow-game capture (weaker IgnoredGames only suppresses the offer).
    [JsonPropertyName("excludedPrograms")]
    public List<string> ExcludedPrograms { get; set; } = new();

    [JsonPropertyName("firstRunCompleted")]
    public bool FirstRunCompleted { get; set; } = false;

    [JsonPropertyName("bars")]
    public BarSettings Bars { get; set; } = new();

    [JsonPropertyName("display")]
    public DisplaySettings Display { get; set; } = new();

    [JsonPropertyName("hotkeys")]
    public HotkeySettings Hotkeys { get; set; } = new();

    [JsonPropertyName("general")]
    public GeneralSettings General { get; set; } = new();

    [JsonPropertyName("profiles")]
    public List<AppProfile> Profiles { get; set; } = new() { AppProfile.CreateDefault() };

    [JsonPropertyName("activeProfileId")]
    public string ActiveProfileId { get; set; } = "default";

    [JsonPropertyName("autoSwitchProfiles")]
    public bool AutoSwitchProfiles { get; set; } = true;

    [JsonPropertyName("profileSwitchBehavior")]
    public ProfileSwitchBehavior ProfileSwitchBehavior { get; set; } = ProfileSwitchBehavior.SwitchWithToast;

    [JsonPropertyName("offerProfileForUnknownGames")]
    public bool OfferProfileForUnknownGames { get; set; } = true;
}

/// <summary>What happens when a profiled game starts (plan 3d).</summary>
public enum ProfileSwitchBehavior
{
    Silent,
    SwitchWithToast,
    AskFirst
}

public class BarSettings
{
    [JsonPropertyName("leftX")]
    public double LeftX { get; set; } = 0;

    [JsonPropertyName("rightX")]
    public double RightX { get; set; } = -1; // -1 means auto (screen width - bar width)

    [JsonPropertyName("width")]
    public int Width { get; set; } = 50;

    [JsonPropertyName("locked")]
    public bool Locked { get; set; } = true;

    [JsonPropertyName("transparentMode")]
    public bool TransparentMode { get; set; } = false;

    [JsonPropertyName("opacity")]
    public double Opacity { get; set; } = 1.0;

    [JsonPropertyName("fadeInMs")]
    public int FadeInMs { get; set; } = 100;

    [JsonPropertyName("fadeOutMs")]
    public int FadeOutMs { get; set; } = 500;

    [JsonPropertyName("activationThreshold")]
    public double ActivationThreshold { get; set; } = 0.01;

    [JsonPropertyName("sensitivity")]
    public double Sensitivity { get; set; } = 1.0; // 0.1 to 3.0, lower = less sensitive

    [JsonPropertyName("minThreshold")]
    public double MinThreshold { get; set; } = 0.05; // Ignore audio levels below this

    [JsonPropertyName("ignoreBalancedSounds")]
    public bool IgnoreBalancedSounds { get; set; } = false; // Filter out sounds equal on L/R (player sounds)

    [JsonPropertyName("hideLfe")]
    public bool HideLfe { get; set; } = false; // Hide LFE/subwoofer indicator in 7.1 view

    [JsonPropertyName("hideYou")]
    public bool HideYou { get; set; } = false; // Hide listener "YOU" indicator in 7.1 spatial view

    [JsonPropertyName("maxOpacity")]
    public double MaxOpacity { get; set; } = 1.0; // Maximum opacity for 7.1 indicators (0.5 to 1.0)

    [JsonPropertyName("surroundLayout")]
    public SurroundLayout SurroundLayout { get; set; } = SurroundLayout.Spatial;

    [JsonPropertyName("spatialScale")]
    public double SpatialScale { get; set; } = 1.0; // 0.5 to 2.0, scales speaker distance from center

    [JsonPropertyName("dualLayout")]
    public DualLayout DualLayout { get; set; } = DualLayout.Vertical;

    [JsonPropertyName("leftIndicatorPercent")]
    public double LeftIndicatorPercent { get; set; } = 0.35; // 0.0 to 0.5 (left side of screen)

    [JsonPropertyName("rightIndicatorPercent")]
    public double RightIndicatorPercent { get; set; } = 0.65; // 0.5 to 1.0 (right side of screen)

    // --- v2 presentation (edited by the new settings shell, rendered by the
    //     Phase 4 overlays; profiles carry per-game copies of these) ---

    [JsonPropertyName("overlayStyle")]
    public OverlayStyle OverlayStyle { get; set; } = OverlayStyle.SideBars;

    [JsonPropertyName("pairWithSideBars")]
    public bool PairWithSideBars { get; set; } = false;

    [JsonPropertyName("colorScale")]
    public ColorScale ColorScale { get; set; } = ColorScale.Thermal;

    [JsonPropertyName("overlaySize")]
    public double OverlaySize { get; set; } = 1.0; // 0.5 to 2.0

    [JsonPropertyName("ringCount")]
    public int RingCount { get; set; } = 5; // 3 | 5 | 7

    [JsonPropertyName("ringMapping")]
    public RingMapping RingMapping { get; set; } = RingMapping.Distance;

    [JsonPropertyName("anchor")]
    public OverlayAnchor Anchor { get; set; } = OverlayAnchor.Bottom;

    [JsonPropertyName("linkIndicators")]
    public bool LinkIndicators { get; set; } = true; // mirrored left/right positions
}

public class DisplaySettings
{
    [JsonPropertyName("mode")]
    public DisplayMode Mode { get; set; } = DisplayMode.Bars;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("targetMonitor")]
    public int TargetMonitor { get; set; } = 0; // 0 = primary, 1+ = other monitors

    [JsonPropertyName("overlayFps")]
    public int OverlayFps { get; set; } = 60; // 30 | 60 | 144 | 240
}

public enum DisplayMode
{
    Bars,
    Full7Point1,
    Both
}

public enum SurroundLayout
{
    Spatial,      // Current circular layout around listener
    HorizontalLine // LR - LM - LF - C - LFE - RF - RM - RR
}

public enum DualLayout
{
    Vertical,      // Current side bars (full height on left/right edges)
    HorizontalLine // Left bar - Right bar across bottom of screen
}

public class HotkeySettings
{
    [JsonPropertyName("toggleEnabled")]
    public string ToggleEnabled { get; set; } = "Ctrl+Shift+R";

    [JsonPropertyName("toggleMode")]
    public string ToggleMode { get; set; } = "Ctrl+Shift+M";

    [JsonPropertyName("showSettings")]
    public string ShowSettings { get; set; } = "Ctrl+Shift+S";
}

public enum LogRetentionType
{
    Size,   // Delete logs when total size exceeds limit
    Date    // Delete logs older than X days
}

public class GeneralSettings
{
    [JsonPropertyName("startMinimized")]
    public bool StartMinimized { get; set; } = false;

    [JsonPropertyName("startWithWindows")]
    public bool StartWithWindows { get; set; } = false;

    [JsonPropertyName("audioDevice")]
    public string? AudioDevice { get; set; } = null; // null = auto-detect

    [JsonPropertyName("captureMode")]
    public Audio.CaptureMode CaptureMode { get; set; } = Audio.CaptureMode.FollowGame;

    [JsonPropertyName("enableAudioLogging")]
    public bool EnableAudioLogging { get; set; } = false;

    [JsonPropertyName("logRetentionType")]
    public LogRetentionType LogRetentionType { get; set; } = LogRetentionType.Size;

    [JsonPropertyName("logRetentionDays")]
    public int LogRetentionDays { get; set; } = 7; // Keep logs for 7 days (date-based)

    [JsonPropertyName("logRetentionSizeMB")]
    public int LogRetentionSizeMB { get; set; } = 100; // Max 100 MB total logs (size-based)
}
