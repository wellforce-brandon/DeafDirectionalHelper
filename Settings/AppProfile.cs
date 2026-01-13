using System;
using System.Text.Json.Serialization;

namespace DeafDirectionalHelper.Settings;

public class AppProfile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string Name { get; set; } = "New Profile";

    [JsonPropertyName("exePath")]
    public string? ExePath { get; set; }

    [JsonPropertyName("processName")]
    public string? ProcessName { get; set; }

    [JsonIgnore]
    public bool IsDefault => Id == "default";

    // Profile-specific settings (display/audio visualization)
    [JsonPropertyName("displayMode")]
    public DisplayMode DisplayMode { get; set; } = DisplayMode.Bars;

    [JsonPropertyName("transparentMode")]
    public bool TransparentMode { get; set; } = false;

    [JsonPropertyName("sensitivity")]
    public double Sensitivity { get; set; } = 1.0;

    [JsonPropertyName("minThreshold")]
    public double MinThreshold { get; set; } = 0.05;

    [JsonPropertyName("ignoreBalancedSounds")]
    public bool IgnoreBalancedSounds { get; set; } = false;

    [JsonPropertyName("hideLfe")]
    public bool HideLfe { get; set; } = false;

    [JsonPropertyName("hideYou")]
    public bool HideYou { get; set; } = false;

    [JsonPropertyName("maxOpacity")]
    public double MaxOpacity { get; set; } = 1.0;

    [JsonPropertyName("surroundLayout")]
    public SurroundLayout SurroundLayout { get; set; } = SurroundLayout.Spatial;

    [JsonPropertyName("spatialScale")]
    public double SpatialScale { get; set; } = 1.0;

    [JsonPropertyName("dualLayout")]
    public DualLayout DualLayout { get; set; } = DualLayout.Vertical;

    [JsonPropertyName("leftIndicatorPercent")]
    public double LeftIndicatorPercent { get; set; } = 0.35;

    [JsonPropertyName("rightIndicatorPercent")]
    public double RightIndicatorPercent { get; set; } = 0.65;

    [JsonPropertyName("width")]
    public int Width { get; set; } = 50;

    /// <summary>
    /// Creates the default profile with standard settings.
    /// </summary>
    public static AppProfile CreateDefault() => new()
    {
        Id = "default",
        Name = "Default",
        ExePath = null,
        ProcessName = null
    };

    /// <summary>
    /// Creates a new profile by copying settings from another profile.
    /// </summary>
    public static AppProfile CreateFrom(AppProfile source, string name, string? exePath)
    {
        var profile = new AppProfile
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            ExePath = exePath,
            ProcessName = !string.IsNullOrEmpty(exePath) ? System.IO.Path.GetFileNameWithoutExtension(exePath) : null
        };
        profile.CopySettingsFrom(source);
        return profile;
    }

    /// <summary>
    /// Copies all settings (not identity) from another profile.
    /// </summary>
    public void CopySettingsFrom(AppProfile other)
    {
        DisplayMode = other.DisplayMode;
        TransparentMode = other.TransparentMode;
        Sensitivity = other.Sensitivity;
        MinThreshold = other.MinThreshold;
        IgnoreBalancedSounds = other.IgnoreBalancedSounds;
        HideLfe = other.HideLfe;
        HideYou = other.HideYou;
        MaxOpacity = other.MaxOpacity;
        SurroundLayout = other.SurroundLayout;
        SpatialScale = other.SpatialScale;
        DualLayout = other.DualLayout;
        LeftIndicatorPercent = other.LeftIndicatorPercent;
        RightIndicatorPercent = other.RightIndicatorPercent;
        Width = other.Width;
    }

    /// <summary>
    /// Applies this profile's settings to the app settings (BarSettings and DisplaySettings).
    /// </summary>
    public void ApplyToSettings(BarSettings bars, DisplaySettings display)
    {
        display.Mode = DisplayMode;
        bars.TransparentMode = TransparentMode;
        bars.Sensitivity = Sensitivity;
        bars.MinThreshold = MinThreshold;
        bars.IgnoreBalancedSounds = IgnoreBalancedSounds;
        bars.HideLfe = HideLfe;
        bars.HideYou = HideYou;
        bars.MaxOpacity = MaxOpacity;
        bars.SurroundLayout = SurroundLayout;
        bars.SpatialScale = SpatialScale;
        bars.DualLayout = DualLayout;
        bars.LeftIndicatorPercent = LeftIndicatorPercent;
        bars.RightIndicatorPercent = RightIndicatorPercent;
        bars.Width = Width;
    }

    /// <summary>
    /// Loads settings from current app settings into this profile.
    /// </summary>
    public void LoadFromSettings(BarSettings bars, DisplaySettings display)
    {
        DisplayMode = display.Mode;
        TransparentMode = bars.TransparentMode;
        Sensitivity = bars.Sensitivity;
        MinThreshold = bars.MinThreshold;
        IgnoreBalancedSounds = bars.IgnoreBalancedSounds;
        HideLfe = bars.HideLfe;
        HideYou = bars.HideYou;
        MaxOpacity = bars.MaxOpacity;
        SurroundLayout = bars.SurroundLayout;
        SpatialScale = bars.SpatialScale;
        DualLayout = bars.DualLayout;
        LeftIndicatorPercent = bars.LeftIndicatorPercent;
        RightIndicatorPercent = bars.RightIndicatorPercent;
        Width = bars.Width;
    }
}
