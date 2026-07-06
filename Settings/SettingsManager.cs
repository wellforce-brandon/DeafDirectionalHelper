using System;
using System.IO;
using System.Text.Json;

namespace DeafDirectionalHelper.Settings;

public class SettingsManager
{
    private static SettingsManager? _instance;
    public static SettingsManager Instance => _instance ??= new SettingsManager();

    private readonly string _settingsPath;
    private AppSettings _settings;

    public AppSettings Settings => _settings;

    public event EventHandler? SettingsChanged;

    private SettingsManager()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "DeafDirectionalHelper");
        Directory.CreateDirectory(appFolder);
        _settingsPath = Path.Combine(appFolder, "settings.json");

        _settings = Load();

        if (_migrated)
            Save(); // persist the migrated shape immediately (backup already written)
    }

    private bool _migrated;

    private AppSettings Load()
    {
        var backupPath = _settingsPath + ".bak";

        // Try loading main settings file
        var settings = TryLoadFromFile(_settingsPath);
        if (settings != null)
        {
            Console.WriteLine($"Settings loaded from {_settingsPath}");
            return Migrate(settings);
        }

        // Try loading from backup if main file failed
        settings = TryLoadFromFile(backupPath);
        if (settings != null)
        {
            Console.WriteLine($"Settings restored from backup: {backupPath}");
            return Migrate(settings);
        }

        Console.WriteLine("Using default settings");
        return new AppSettings();
    }

    private static AppSettings? TryLoadFromFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppSettings>(json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings from {path}: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Migration to the v3 (UI overhaul) settings shape.
    ///
    /// From v1: capture mode inferred (a configured device means FixedDevice),
    /// legacy display mode + layouts mapped onto OverlayStyle for the live
    /// settings and every stored profile, SpatialScale becomes OverlaySize.
    /// From any pre-v3: FirstRunCompleted is set (existing installs never see
    /// the wizard). A backup of the original file is written first.
    /// </summary>
    private AppSettings Migrate(AppSettings settings)
    {
        if (settings.Version >= 3)
            return settings;

        WriteMigrationBackup(settings.Version);

        if (settings.Version < 2)
        {
            if (!string.IsNullOrEmpty(settings.General.AudioDevice))
                settings.General.CaptureMode = Audio.CaptureMode.FixedDevice;

            // v1 files predate OverlayStyle: map the legacy mode/layout combo
            settings.Bars.OverlayStyle = MapLegacyStyle(
                settings.Display.Mode, settings.Bars.SurroundLayout, settings.Bars.DualLayout);
            settings.Bars.PairWithSideBars = settings.Display.Mode == DisplayMode.Both;
            settings.Bars.OverlaySize = Math.Clamp(settings.Bars.SpatialScale, 0.5, 2.0);

            foreach (var profile in settings.Profiles)
            {
                profile.OverlayStyle = MapLegacyStyle(
                    profile.DisplayMode, profile.SurroundLayout, profile.DualLayout);
                profile.PairWithSideBars = profile.DisplayMode == DisplayMode.Both;
                profile.OverlaySize = Math.Clamp(profile.SpatialScale, 0.5, 2.0);
            }
        }

        settings.FirstRunCompleted = true;

        Console.WriteLine($"Settings migrated v{settings.Version} -> v3");
        settings.Version = 3;
        _migrated = true;
        return settings;
    }

    private static OverlayStyle MapLegacyStyle(DisplayMode mode, SurroundLayout surround, DualLayout dual)
    {
        return mode switch
        {
            DisplayMode.Bars => dual == DualLayout.HorizontalLine ? OverlayStyle.CompassStrip : OverlayStyle.SideBars,
            DisplayMode.Full7Point1 => surround == SurroundLayout.HorizontalLine ? OverlayStyle.CompassStrip : OverlayStyle.RadarRing,
            DisplayMode.Both => OverlayStyle.RadarRing, // + PairWithSideBars
            _ => OverlayStyle.SideBars
        };
    }

    private void WriteMigrationBackup(int fromVersion)
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var backupPath = Path.Combine(
                    Path.GetDirectoryName(_settingsPath)!, $"settings.v{fromVersion}.bak.json");
                File.Copy(_settingsPath, backupPath, overwrite: true);
                Console.WriteLine($"Pre-migration backup written: {backupPath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: could not write migration backup: {ex.Message}");
        }
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(_settings, options);

            // Create backup of existing settings before overwriting
            var backupPath = _settingsPath + ".bak";
            if (File.Exists(_settingsPath))
            {
                try
                {
                    File.Copy(_settingsPath, backupPath, overwrite: true);
                }
                catch (Exception backupEx)
                {
                    Console.WriteLine($"Warning: Could not create settings backup: {backupEx.Message}");
                }
            }

            File.WriteAllText(_settingsPath, json);
            Console.WriteLine($"Settings saved to {_settingsPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    public void Update(Action<AppSettings> updateAction)
    {
        updateAction(_settings);
        Save();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Updates settings without saving to disk or firing events.
    /// Use this for real-time dragging updates, then call Save() on completion.
    /// </summary>
    public void UpdateSilent(Action<AppSettings> updateAction)
    {
        updateAction(_settings);
    }

    /// <summary>
    /// Notifies listeners that settings changed (without saving).
    /// </summary>
    public void NotifyChanged()
    {
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        _settings = new AppSettings();
        Save();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
