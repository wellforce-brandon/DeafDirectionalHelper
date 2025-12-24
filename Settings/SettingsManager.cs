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
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    Console.WriteLine($"Settings loaded from {_settingsPath}");
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex.Message}");
        }

        Console.WriteLine("Using default settings");
        return new AppSettings();
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
