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
    }

    private AppSettings Load()
    {
        var backupPath = _settingsPath + ".bak";

        // Try loading main settings file
        var settings = TryLoadFromFile(_settingsPath);
        if (settings != null)
        {
            Console.WriteLine($"Settings loaded from {_settingsPath}");
            return settings;
        }

        // Try loading from backup if main file failed
        settings = TryLoadFromFile(backupPath);
        if (settings != null)
        {
            Console.WriteLine($"Settings restored from backup: {backupPath}");
            return settings;
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
