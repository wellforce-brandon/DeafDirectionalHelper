# Application Profiles System - v2 Plan

## Overview
Add per-application profiles that automatically switch settings when specific games/applications are running. Users configure profiles using the **existing main settings UI** - no separate modal with duplicate controls.

## Key Design Decisions

### 1. Default Profile is Explicit
- "Default" is a real, editable profile stored in the profiles list
- It's always the first profile and cannot be deleted
- When no profiled application is running, Default is active
- Users can customize Default just like any other profile

### 2. Main UI for All Settings
- No duplicate settings controls in a profile editor modal
- Profile editor modal ONLY handles: exe path, display name
- All Mode/Layout/Sensitivity/etc. settings use existing SettingsWindow controls
- "Save to Profile" button commits current UI state to the selected profile

### 3. Auto-Switcher Pauses While Settings Open
- When SettingsWindow is visible, auto-switching is disabled
- This prevents jarring mid-edit profile switches
- Auto-switching resumes when settings window is hidden/closed
- Manual profile selection in the dropdown still works while settings open

### 4. Profile Storage Structure
```csharp
public class AppProfile
{
    public string Id { get; set; }           // GUID or "default"
    public string Name { get; set; }         // Display name ("Apex Legends", "Default")
    public string? ExePath { get; set; }     // null for Default profile
    public string? ProcessName { get; set; } // Cached from exe (e.g., "r5apex")

    // All the settings that can vary per-profile:
    public DisplayMode DisplayMode { get; set; }
    public bool TransparentMode { get; set; }
    public double Sensitivity { get; set; }
    public double MinThreshold { get; set; }
    public bool IgnoreBalancedSounds { get; set; }
    public bool HideLfe { get; set; }
    public bool HideYou { get; set; }
    public double MaxOpacity { get; set; }
    public SurroundLayout SurroundLayout { get; set; }
    public double SpatialScale { get; set; }
    public DualLayout DualLayout { get; set; }
    public double LeftIndicatorPercent { get; set; }
    public double RightIndicatorPercent { get; set; }
    public int Width { get; set; }
}
```

### 5. Settings NOT in Profiles (Global)
These stay in GeneralSettings and apply regardless of active profile:
- `StartMinimized`, `StartWithWindows`
- `AudioDevice`
- `EnableAudioLogging`, log retention settings
- `TargetMonitor` (display settings)
- `Hotkeys`
- Bar positions (`LeftX`, `RightX`) - these are screen-specific
- `Locked` (link indicators)
- `Opacity`, `FadeInMs`, `FadeOutMs`, `ActivationThreshold`

---

## UI Design

### Profile Section in SettingsWindow (Right Column, above Audio)

```
┌─ Application Profiles ─────────────────────────────────┐
│ [x] Auto-switch when apps are running                  │
│                                                        │
│ Current Profile: [Default              ▼]              │
│                                                        │
│ ┌────────────────────────────────────────────────────┐ │
│ │ ★ Default                                    [Edit]│ │
│ │   Apex Legends (r5apex.exe)                  [Edit]│ │
│ │   Valorant (VALORANT-Win64-Shipping.exe)     [Edit]│ │
│ └────────────────────────────────────────────────────┘ │
│                                                        │
│ [New Profile]  [Save Current]  [Delete]                │
│                                                        │
│ ⚠ Auto-switching paused while settings open           │
└────────────────────────────────────────────────────────┘
```

### Button Behaviors

**Current Profile Dropdown:**
- Shows all profiles (Default first, then alphabetical)
- Selecting a profile loads its settings into the main UI
- Prompts to save unsaved changes before switching

**New Profile Button:**
- Opens simple modal: browse for exe, enter display name
- Creates profile with current UI settings as starting point
- Auto-selects the new profile after creation

**Save Current Button:**
- Saves current main UI settings to the currently selected profile
- Shows confirmation: "Saved to [Profile Name]"
- Button highlights/pulses when there are unsaved changes

**Edit Button (per profile row):**
- For Default: disabled or hidden (no exe to edit)
- For others: opens modal to change exe path or display name

**Delete Button:**
- Deletes currently selected profile
- Cannot delete Default
- Confirms before deleting

### Unsaved Changes Handling

1. Track dirty state with `_hasUnsavedProfileChanges` flag
2. Any settings change sets the flag to true
3. Visual indicator when dirty (e.g., "Save Current" button changes color)
4. When switching profiles or closing settings:
   - If dirty, prompt: "Save changes to [current profile]?"
   - Yes: save then proceed
   - No: discard changes, proceed
   - Cancel: stay on current profile

---

## Implementation Plan

### Phase 1: Data Model & Storage

**File: `Settings/AppProfile.cs` (new)**
```csharp
public class AppProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Profile";
    public string? ExePath { get; set; }
    public string? ProcessName { get; set; }
    public bool IsDefault => Id == "default";

    // Profile-specific settings
    public DisplayMode DisplayMode { get; set; } = DisplayMode.Bars;
    public bool TransparentMode { get; set; } = false;
    public double Sensitivity { get; set; } = 1.0;
    public double MinThreshold { get; set; } = 0.05;
    public bool IgnoreBalancedSounds { get; set; } = false;
    public bool HideLfe { get; set; } = false;
    public bool HideYou { get; set; } = false;
    public double MaxOpacity { get; set; } = 1.0;
    public SurroundLayout SurroundLayout { get; set; } = SurroundLayout.Spatial;
    public double SpatialScale { get; set; } = 1.0;
    public DualLayout DualLayout { get; set; } = DualLayout.Vertical;
    public double LeftIndicatorPercent { get; set; } = 0.35;
    public double RightIndicatorPercent { get; set; } = 0.65;
    public int Width { get; set; } = 50;

    public static AppProfile CreateDefault() => new()
    {
        Id = "default",
        Name = "Default",
        ExePath = null,
        ProcessName = null
    };

    public void CopySettingsFrom(AppProfile other) { /* copy all settings */ }
    public void CopySettingsTo(BarSettings bars, DisplaySettings display) { /* apply to app settings */ }
    public void LoadSettingsFrom(BarSettings bars, DisplaySettings display) { /* load from app settings */ }
}
```

**File: `Settings/AppSettings.cs` (modify)**
```csharp
// Add to AppSettings class:
[JsonPropertyName("profiles")]
public List<AppProfile> Profiles { get; set; } = new() { AppProfile.CreateDefault() };

[JsonPropertyName("activeProfileId")]
public string ActiveProfileId { get; set; } = "default";

[JsonPropertyName("autoSwitchProfiles")]
public bool AutoSwitchProfiles { get; set; } = true;
```

### Phase 2: Profile Manager Service

**File: `Settings/ProfileManager.cs` (new)**
```csharp
public class ProfileManager
{
    public static ProfileManager Instance { get; }

    public event EventHandler<AppProfile>? ProfileActivated;
    public event EventHandler? ProfilesChanged;

    public AppProfile ActiveProfile { get; private set; }
    public IReadOnlyList<AppProfile> Profiles => _settings.Profiles.AsReadOnly();
    public bool AutoSwitchEnabled { get; set; }
    public bool AutoSwitchPaused { get; set; } // Paused when settings window open

    public void ActivateProfile(string profileId);
    public void ActivateProfile(AppProfile profile);
    public AppProfile CreateProfile(string name, string exePath);
    public void DeleteProfile(string profileId);
    public void SaveCurrentSettingsToProfile(AppProfile profile);
    public void LoadProfileIntoSettings(AppProfile profile);
    public AppProfile? GetProfileForProcess(string processName);
    public AppProfile GetDefaultProfile();
    public void EnsureDefaultExists();
}
```

### Phase 3: Process Monitor

**File: `Services/ProcessMonitor.cs` (new)**
```csharp
public class ProcessMonitor : IDisposable
{
    private readonly Timer _pollTimer;
    private readonly HashSet<string> _watchedProcesses;
    private string? _lastActiveProcess;

    public event EventHandler<string?>? ActiveProcessChanged;

    public ProcessMonitor();
    public void UpdateWatchList(IEnumerable<string> processNames);
    public string? GetCurrentActiveProcess();

    private void PollProcesses(object? state);
}
```

**Polling Strategy:**
- Poll every 2 seconds (not too aggressive)
- Only check for processes in the watch list (from profiles)
- Fire event only when active process changes
- Return null when no watched process is running

### Phase 4: Profile Editor Modal (Simplified)

**File: `View/ProfileEditorWindow.xaml` (new, simple)**
- TextBox for profile name
- TextBox for exe path + Browse button
- OK / Cancel buttons
- Dark theme matching rest of app

**No settings controls in this modal** - that's the key difference from v1.

### Phase 5: SettingsWindow Integration

**Modify: `View/SettingsWindow.xaml`**
- Add "Application Profiles" GroupBox to right column (above Audio section)
- Auto-switch checkbox
- Profile dropdown (ComboBox)
- Profile list (ListBox with Edit buttons)
- New/Save/Delete buttons
- Warning text about auto-switch paused

**Modify: `View/SettingsWindow.xaml.cs`**
```csharp
// New fields
private AppProfile? _editingProfile;
private bool _hasUnsavedProfileChanges;

// New methods
private void LoadProfilesList();
private void LoadProfileIntoUI(AppProfile profile);
private void SaveUIToProfile(AppProfile profile);
private void MarkProfileDirty();
private bool PromptSaveChanges(); // Returns false if user cancels

// Event handlers
private void ProfileDropdown_Changed(...);
private void NewProfile_Click(...);
private void SaveProfile_Click(...);
private void DeleteProfile_Click(...);
private void EditProfile_Click(...);
private void AutoSwitchCheckbox_Changed(...);

// Override existing settings handlers to call MarkProfileDirty()
```

### Phase 6: MainWindow Integration

**Modify: `MainWindow.xaml.cs`**
```csharp
private ProcessMonitor _processMonitor;
private ProfileManager _profileManager;

// In constructor:
_profileManager = ProfileManager.Instance;
_processMonitor = new ProcessMonitor();
_processMonitor.ActiveProcessChanged += OnActiveProcessChanged;
_profileManager.ProfileActivated += OnProfileActivated;

// Pause auto-switch when settings visible
_settingsWindow.IsVisibleChanged += (s, e) => {
    _profileManager.AutoSwitchPaused = _settingsWindow.IsVisible;
};

private void OnActiveProcessChanged(object? sender, string? processName)
{
    if (_profileManager.AutoSwitchPaused) return;

    var profile = processName != null
        ? _profileManager.GetProfileForProcess(processName)
        : _profileManager.GetDefaultProfile();

    if (profile != null && profile.Id != _profileManager.ActiveProfile.Id)
    {
        _profileManager.ActivateProfile(profile);
    }
}

private void OnProfileActivated(object? sender, AppProfile profile)
{
    // Settings already applied by ProfileManager
    // Just update displays
    Dispatcher.Invoke(UpdateDisplayMode);
}
```

---

## File Changes Summary

### New Files
1. `Settings/AppProfile.cs` - Profile data model
2. `Settings/ProfileManager.cs` - Profile management service
3. `Services/ProcessMonitor.cs` - Process detection
4. `View/ProfileEditorWindow.xaml` - Simple exe path editor
5. `View/ProfileEditorWindow.xaml.cs` - Code-behind

### Modified Files
1. `Settings/AppSettings.cs` - Add Profiles list, ActiveProfileId, AutoSwitchProfiles
2. `View/SettingsWindow.xaml` - Add profiles section UI
3. `View/SettingsWindow.xaml.cs` - Profile selection, dirty tracking, save/load
4. `MainWindow.xaml.cs` - ProcessMonitor integration, auto-switch pausing
5. `AppVersion.cs` - Bump to 1.4.0

---

## Edge Cases & Error Handling

1. **Exe file moved/deleted**: Show warning icon next to profile, still allow manual selection
2. **Multiple profiles match same process**: First match wins (alphabetical after Default)
3. **Profile with no exe**: Only Default can have null exe, others require valid path
4. **Settings window closed with unsaved changes**: Prompt to save
5. **Profile deleted while active**: Switch to Default
6. **First run migration**: Create Default profile from existing settings

---

## Testing Checklist

- [ ] Default profile is always present and first in list
- [ ] Can edit Default profile settings
- [ ] Cannot delete Default profile
- [ ] New profile creates with current settings
- [ ] Selecting profile loads its settings into UI
- [ ] Save button commits UI to selected profile
- [ ] Unsaved changes warning when switching profiles
- [ ] Auto-switch pauses when settings window open
- [ ] Auto-switch resumes when settings window hidden
- [ ] Process detection correctly identifies running games
- [ ] Profile switches when game launches
- [ ] Profile reverts to Default when game closes
- [ ] Profiles persist across app restart
- [ ] Edit modal only shows exe path/name fields

---

## Version
1.4.0 - Application profiles with auto-switching

## Estimated Complexity
Medium-High: ~500-700 lines of new code across 5 new files + modifications to 4 existing files.
