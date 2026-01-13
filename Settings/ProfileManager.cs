using System;
using System.Collections.Generic;
using System.Linq;

namespace DeafDirectionalHelper.Settings;

/// <summary>
/// Manages application profiles and profile switching.
/// </summary>
public class ProfileManager
{
    private static ProfileManager? _instance;
    public static ProfileManager Instance => _instance ??= new ProfileManager();

    private readonly SettingsManager _settingsManager;

    /// <summary>
    /// Fired when a profile is activated (settings have been applied).
    /// </summary>
    public event EventHandler<AppProfile>? ProfileActivated;

    /// <summary>
    /// Fired when the profiles list changes (add/remove/edit).
    /// </summary>
    public event EventHandler? ProfilesChanged;

    /// <summary>
    /// The currently active profile.
    /// </summary>
    public AppProfile ActiveProfile { get; private set; }

    /// <summary>
    /// All profiles (read-only view).
    /// </summary>
    public IReadOnlyList<AppProfile> Profiles => _settingsManager.Settings.Profiles.AsReadOnly();

    /// <summary>
    /// Whether auto-switching is enabled in settings.
    /// </summary>
    public bool AutoSwitchEnabled
    {
        get => _settingsManager.Settings.AutoSwitchProfiles;
        set => _settingsManager.Update(s => s.AutoSwitchProfiles = value);
    }

    /// <summary>
    /// Temporarily pauses auto-switching (e.g., when settings window is open).
    /// </summary>
    public bool AutoSwitchPaused { get; set; }

    private ProfileManager()
    {
        _settingsManager = SettingsManager.Instance;
        EnsureDefaultExists();

        // Load the active profile
        var activeId = _settingsManager.Settings.ActiveProfileId;
        ActiveProfile = GetProfileById(activeId) ?? GetDefaultProfile();
    }

    /// <summary>
    /// Ensures the default profile always exists.
    /// </summary>
    public void EnsureDefaultExists()
    {
        var profiles = _settingsManager.Settings.Profiles;
        if (!profiles.Any(p => p.IsDefault))
        {
            profiles.Insert(0, AppProfile.CreateDefault());
            _settingsManager.Save();
        }
    }

    /// <summary>
    /// Gets the default profile.
    /// </summary>
    public AppProfile GetDefaultProfile()
    {
        return _settingsManager.Settings.Profiles.First(p => p.IsDefault);
    }

    /// <summary>
    /// Gets a profile by ID.
    /// </summary>
    public AppProfile? GetProfileById(string id)
    {
        return _settingsManager.Settings.Profiles.FirstOrDefault(p => p.Id == id);
    }

    /// <summary>
    /// Gets a profile that matches the given process name.
    /// </summary>
    public AppProfile? GetProfileForProcess(string processName)
    {
        return _settingsManager.Settings.Profiles
            .Where(p => !p.IsDefault && !string.IsNullOrEmpty(p.ProcessName))
            .FirstOrDefault(p => string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Activates a profile by ID, applying its settings.
    /// </summary>
    public void ActivateProfile(string profileId)
    {
        var profile = GetProfileById(profileId);
        if (profile != null)
        {
            ActivateProfile(profile);
        }
    }

    /// <summary>
    /// Activates a profile, applying its settings.
    /// </summary>
    public void ActivateProfile(AppProfile profile)
    {
        if (profile.Id == ActiveProfile.Id) return;

        ActiveProfile = profile;

        // Apply profile settings to app settings
        _settingsManager.Update(s =>
        {
            s.ActiveProfileId = profile.Id;
            profile.ApplyToSettings(s.Bars, s.Display);
        });

        ProfileActivated?.Invoke(this, profile);
    }

    /// <summary>
    /// Loads a profile's settings into the app settings WITHOUT changing the active profile ID.
    /// Used when selecting a profile in the UI to edit it.
    /// </summary>
    public void LoadProfileForEditing(AppProfile profile)
    {
        ActiveProfile = profile;

        _settingsManager.Update(s =>
        {
            profile.ApplyToSettings(s.Bars, s.Display);
        });

        // Don't fire ProfileActivated - this is just for UI editing
    }

    /// <summary>
    /// Saves the current app settings to the specified profile.
    /// </summary>
    public void SaveCurrentSettingsToProfile(AppProfile profile)
    {
        profile.LoadFromSettings(_settingsManager.Settings.Bars, _settingsManager.Settings.Display);
        _settingsManager.Save();
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Creates a new profile with the current settings.
    /// </summary>
    public AppProfile CreateProfile(string name, string? exePath)
    {
        var newProfile = new AppProfile
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            ExePath = exePath,
            ProcessName = !string.IsNullOrEmpty(exePath) ? System.IO.Path.GetFileNameWithoutExtension(exePath) : null
        };

        // Copy current settings to the new profile
        newProfile.LoadFromSettings(_settingsManager.Settings.Bars, _settingsManager.Settings.Display);

        _settingsManager.Settings.Profiles.Add(newProfile);
        _settingsManager.Save();

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        return newProfile;
    }

    /// <summary>
    /// Updates a profile's name and exe path.
    /// </summary>
    public void UpdateProfile(AppProfile profile, string name, string? exePath)
    {
        profile.Name = name;
        profile.ExePath = exePath;
        profile.ProcessName = !string.IsNullOrEmpty(exePath) ? System.IO.Path.GetFileNameWithoutExtension(exePath) : null;
        _settingsManager.Save();
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Deletes a profile. Cannot delete the default profile.
    /// </summary>
    public bool DeleteProfile(string profileId)
    {
        if (profileId == "default") return false;

        var profile = GetProfileById(profileId);
        if (profile == null) return false;

        _settingsManager.Settings.Profiles.Remove(profile);

        // If we deleted the active profile, switch to default
        if (_settingsManager.Settings.ActiveProfileId == profileId)
        {
            ActivateProfile(GetDefaultProfile());
        }

        _settingsManager.Save();
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Gets all process names that are being watched for auto-switching.
    /// </summary>
    public IEnumerable<string> GetWatchedProcessNames()
    {
        return _settingsManager.Settings.Profiles
            .Where(p => !p.IsDefault && !string.IsNullOrEmpty(p.ProcessName))
            .Select(p => p.ProcessName!);
    }
}
