# Settings UI & Enhanced Features - Implementation Checklist

## Phase 1: System Tray & Settings Window Foundation

### Setup
- [ ] Add `UseWindowsForms` to .csproj for NotifyIcon
- [ ] Create app icon (or use placeholder)
- [ ] Create `Settings/AppSettings.cs` model class
- [ ] Create `Settings/SettingsManager.cs` for load/save

### Settings Window
- [ ] Create `View/SettingsWindow.xaml` with basic layout
- [ ] Add sections: General, Bars, Display, Hotkeys
- [ ] Implement close button (hides window, doesn't exit)
- [ ] Add "Exit Application" button
- [ ] Wire up settings bindings

### System Tray
- [ ] Add NotifyIcon to MainWindow
- [ ] Create tray icon context menu (Settings, Enable/Disable, Exit)
- [ ] Handle minimize to tray (hide main window)
- [ ] Handle tray icon double-click (show settings)
- [ ] Handle app exit from tray menu

### App Lifecycle
- [ ] Modify MainWindow to not show by default (start in tray or minimized)
- [ ] Keep bars visible when main window hidden
- [ ] Proper cleanup on exit (dispose NotifyIcon, stop monitoring)

### Testing Phase 1
- [ ] Settings window opens from tray
- [ ] App minimizes to tray correctly
- [ ] Exit fully closes app
- [ ] Settings persist after restart

---

## Phase 2: Bar Positioning System

### Drag Infrastructure
- [ ] Add `IsDragging` state to bar windows
- [ ] Implement `MouseDown` handler on bars (start drag)
- [ ] Implement `MouseMove` handler (update position)
- [ ] Implement `MouseUp` handler (end drag, save position)
- [ ] Add visual feedback during drag (cursor change)

### Position Management
- [ ] Add `LeftBarX`, `RightBarX` to AppSettings
- [ ] Load positions on startup
- [ ] Save positions when drag ends
- [ ] Add position reset button in settings

### Lock Mode
- [ ] Add `BarsLocked` setting
- [ ] When locked: moving left bar updates right bar (mirrored)
- [ ] When locked: moving right bar updates left bar (mirrored)
- [ ] Add lock/unlock toggle in settings UI
- [ ] Visual indicator of lock state

### Bounds Checking
- [ ] Prevent bars from going off-screen
- [ ] Handle multi-monitor setups
- [ ] Clamp positions to valid range

### Testing Phase 2
- [ ] Bars can be dragged horizontally
- [ ] Lock mode moves both bars symmetrically
- [ ] Unlock mode allows independent movement
- [ ] Positions saved and restored on restart
- [ ] Bars stay within screen bounds

---

## Phase 3: Transparent Mode

### Opacity System
- [ ] Add `Opacity` property to bar windows
- [ ] Add `TransparentMode` setting
- [ ] Add `BaseOpacity` setting (0-1 for non-transparent mode)

### Fade Animation
- [ ] Create opacity animation helper
- [ ] Implement fade-in when sound detected (100ms default)
- [ ] Implement fade-out when sound stops (500ms default, with delay)
- [ ] Add `FadeInDuration` and `FadeOutDuration` settings

### Sound Detection Threshold
- [ ] Add `ActivationThreshold` setting
- [ ] Only activate transparency when sound > threshold
- [ ] Configurable in settings UI

### Settings UI
- [ ] Add "Transparent Mode" toggle
- [ ] Add opacity slider (for base opacity)
- [ ] Add threshold slider
- [ ] Add fade duration controls

### Testing Phase 3
- [ ] Transparent mode makes bars invisible when silent
- [ ] Bars fade in smoothly when sound plays
- [ ] Bars fade out after sound stops
- [ ] Threshold prevents activation from noise floor
- [ ] Non-transparent mode unaffected

---

## Phase 4: Full 7.1 Mode

### Window Setup
- [ ] Create `View/FullSurroundWindow.xaml`
- [ ] Design spatial layout (8 indicators positioned on screen)
- [ ] Make window transparent background, click-through
- [ ] Always on top like current bars

### Speaker Indicators
- [ ] Create indicator control (colored circle/square)
- [ ] Position indicators at spatial locations
- [ ] Map all 8 speakers to indicators
- [ ] Apply same color gradient as bars

### Mode Switching
- [ ] Add `DisplayMode` setting (bars, full7.1, both)
- [ ] Hide bars when in full7.1 mode
- [ ] Show 7.1 window when in full7.1 mode
- [ ] Add mode toggle in settings

### 7.1 Layout Customization
- [ ] Allow repositioning of 7.1 window
- [ ] Consider allowing individual indicator repositioning (future)
- [ ] Scale indicators based on screen size

### Testing Phase 4
- [ ] 7.1 mode shows all 8 channel indicators
- [ ] Indicators positioned correctly (spatial layout)
- [ ] Colors respond to audio correctly
- [ ] Mode switching works smoothly
- [ ] Can use full7.1 mode with transparent behavior

---

## Phase 5: Keyboard Shortcuts & Launch

### Hotkey Registration
- [ ] Create `Helpers/HotkeyManager.cs`
- [ ] Use RegisterHotKey Windows API
- [ ] Handle WM_HOTKEY messages
- [ ] Graceful handling of conflicts

### Core Hotkeys
- [ ] Toggle enable/disable (Ctrl+Shift+R default)
- [ ] Toggle mode (Ctrl+Shift+M default)
- [ ] Show settings (Ctrl+Shift+S default)

### Settings UI
- [ ] Add hotkey configuration section
- [ ] Allow custom hotkey recording
- [ ] Show current hotkey bindings
- [ ] Clear/reset hotkey options

### Windows Integration
- [ ] Create Start Menu shortcut option
- [ ] Add "Start with Windows" setting
- [ ] Implement startup registry entry (optional)

### Testing Phase 5
- [ ] Hotkeys work when app not focused
- [ ] Custom hotkeys can be set
- [ ] Hotkey conflicts handled gracefully
- [ ] Start with Windows works
- [ ] Shortcuts created correctly

---

## Final Integration & Polish

### Settings Persistence
- [ ] All settings save correctly
- [ ] Settings load on startup
- [ ] Handle corrupted settings file (reset to defaults)
- [ ] Settings version migration (for future changes)

### Error Handling
- [ ] Handle audio device disconnection
- [ ] Handle monitor configuration changes
- [ ] Graceful degradation if features fail

### Documentation
- [ ] Update CLAUDE.md with new features
- [ ] Update README.md with usage instructions
- [ ] Document hotkey defaults

### Testing Final
- [ ] Full workflow: launch -> configure -> use -> exit
- [ ] Test on single monitor
- [ ] Test settings persistence
- [ ] Test all hotkeys
- [ ] Test mode switching
- [ ] Memory/resource usage acceptable

---

## Notes

- Start with Phase 1 - it's the foundation for everything else
- Each phase can be committed separately
- Test thoroughly after each phase before proceeding
- Keep existing bar functionality working throughout
