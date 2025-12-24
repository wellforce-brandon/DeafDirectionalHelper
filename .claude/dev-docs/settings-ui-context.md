# Settings UI & Enhanced Features - Context

## Key Files

### Existing Files to Modify
| File | Changes |
|------|---------|
| `MainWindow.xaml.cs` | Add system tray, settings window launch, app lifecycle |
| `MainWindow.xaml` | Minimal changes (mostly code-behind) |
| `View/LeftScreen.xaml.cs` | Add drag support, opacity control |
| `View/RightScreen.xaml.cs` | Add drag support, opacity control |
| `View/LeftScreen.xaml` | Add drag handle element |
| `View/RightScreen.xaml` | Add drag handle element |
| `View/ColoredSpeakers.cs` | Add opacity/visibility properties |
| `CanetisRadar2.csproj` | Add Windows Forms reference for NotifyIcon |

### New Files to Create
| File | Purpose |
|------|---------|
| `View/SettingsWindow.xaml` | Settings UI layout |
| `View/SettingsWindow.xaml.cs` | Settings UI logic |
| `View/FullSurroundWindow.xaml` | 7.1 mode spatial display |
| `View/FullSurroundWindow.xaml.cs` | 7.1 mode logic |
| `Settings/AppSettings.cs` | Settings model class |
| `Settings/SettingsManager.cs` | Load/save settings to JSON |
| `Helpers/HotkeyManager.cs` | Global hotkey registration |
| `Resources/icon.ico` | System tray icon |

---

## Architectural Decisions

### Why JSON for Settings (not Registry)
- Human-readable for debugging
- Easy to backup/restore
- No admin permissions needed
- Can include comments (JSON5 if needed)
- Portable between machines

### Why Windows Forms NotifyIcon (not custom)
- Battle-tested, reliable
- Full balloon/tooltip support
- Context menu built-in
- No additional NuGet packages
- Works on all Windows versions

### Why RegisterHotKey API (not keyboard hooks)
- Lower overhead
- System handles conflicts
- Standard Windows pattern
- No need for low-level access
- Works even when app not focused

### Why Separate 7.1 Window (not modify existing)
- Current left/right bars work well for their purpose
- 7.1 needs completely different layout
- Can show both modes simultaneously if desired
- Easier to implement and test independently
- Can be positioned anywhere on any screen

---

## Dependencies

### NuGet Packages
- **NAudio 2.2.1** (existing) - Audio capture
- No new packages needed

### Framework References
- **System.Windows.Forms** - For NotifyIcon (system tray)
- **PresentationCore** (existing) - WPF
- **WindowsBase** (existing) - WPF

### Windows APIs (P/Invoke)
- `RegisterHotKey` / `UnregisterHotKey` - Global hotkeys
- Possibly `SetWindowPos` - For always-on-top control

---

## Configuration Schema

```json
{
  "version": 1,
  "bars": {
    "leftX": 0,
    "rightX": 1870,
    "width": 50,
    "locked": true,
    "transparentMode": false,
    "opacity": 1.0,
    "fadeInMs": 100,
    "fadeOutMs": 500
  },
  "display": {
    "mode": "bars",
    "enabled": true
  },
  "hotkeys": {
    "toggleEnabled": "Ctrl+Shift+R",
    "toggleMode": "Ctrl+Shift+M"
  },
  "general": {
    "startMinimized": false,
    "startWithWindows": false,
    "audioDevice": "CABLE-C Input (VB-Audio Voicemeeter VAIO)"
  }
}
```

---

## Integration Points

### MainWindow (Coordinator)
- Creates and manages SettingsWindow
- Creates and manages system tray icon
- Handles app lifecycle (minimize to tray, exit)
- Routes hotkey events to appropriate handlers

### LeftScreen / RightScreen
- Receive position updates from settings
- Report drag events back to coordinator
- Receive opacity/visibility commands

### ColoredSpeakers (ViewModel)
- New `IsEnabled` property to pause updates
- New `Opacity` property for transparency
- New `IsTransparentMode` for fade behavior

### SettingsManager (New)
- Singleton for settings access
- Auto-save on changes (debounced)
- Load with defaults fallback
- Settings change events for UI updates

---

## 7.1 Speaker Layout

```
Screen Layout (approximate positions):

    [FL]          [FC]          [FR]
     Front Left    Center       Front Right
     Speaker1      Speaker3     Speaker2

    [SL]                        [SR]
     Side Left                   Side Right
     Speaker7                    Speaker8

    [BL]          [LFE]         [BR]
     Back Left    Subwoofer     Back Right
     Speaker5     Speaker4      Speaker6
```

Each indicator: Small colored circle/square (50-80px)
Position: Overlayed on screen at approximate spatial location
Center reference: Center of primary screen

---

## State Management

### Application States
1. **Running (Visible)** - Normal operation, bars showing
2. **Running (Hidden)** - Minimized to tray, bars still active
3. **Running (Disabled)** - Bars hidden, monitoring paused
4. **Settings Open** - Settings window visible
5. **Exiting** - Cleanup and shutdown

### Bar States
1. **Visible** - Normal colored bars
2. **Transparent-Idle** - Invisible, waiting for sound
3. **Transparent-Active** - Fading in/visible due to sound
4. **Transparent-FadingOut** - Sound stopped, fading out
5. **Dragging** - User moving bar position

---

## File Locations

- **Settings file**: `%APPDATA%\CanetisRadar2\settings.json`
- **Log file** (if needed): `%APPDATA%\CanetisRadar2\log.txt`
- **App icon**: Embedded resource in executable