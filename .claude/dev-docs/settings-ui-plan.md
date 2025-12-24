# Settings UI & Enhanced Features - Implementation Plan

## Executive Summary

Add a comprehensive settings/control UI for CanetisRadar2 with system tray support, movable bars, multiple display modes, and keyboard shortcuts. This transforms the app from a fixed utility into a configurable, user-friendly tool.

---

## Feature Overview

### 1. Settings Window
- Movable, separate window for controlling the app
- Can live on any screen or be hidden
- System tray icon when minimized
- Full app exit option

### 2. Bar Positioning
- Horizontal drag to reposition left/right bars
- Locked mode: move both bars symmetrically
- Unlocked mode: move bars independently
- Position persistence (save/load)

### 3. Visual Modes
- **Current mode**: Solid colored bars (always visible)
- **Transparent mode**: Bars invisible until sound activates them
- **Full 7.1 mode**: All 8 channels displayed in spatial positions

### 4. Controls
- Enable/disable sound indicators (toggle + keybind)
- Keyboard shortcut to launch app
- Hotkey support for common actions

---

## Implementation Phases

### Phase 1: System Tray & Settings Window Foundation
**Goal**: Basic settings window with system tray support

1. Add `System.Windows.Forms` reference for NotifyIcon
2. Create SettingsWindow.xaml with basic layout
3. Implement system tray icon with context menu
4. Handle minimize-to-tray behavior
5. Add "Exit" functionality (full app close)

**Dependencies**: None
**Risk**: Low - standard WPF patterns

### Phase 2: Bar Positioning System
**Goal**: Draggable bars with lock/unlock modes

1. Add drag handles to Left/Right screens
2. Implement mouse drag logic for horizontal movement
3. Create position sync for locked mode
4. Add unlock toggle in settings
5. Save/load positions to settings file

**Dependencies**: Phase 1 (settings UI exists)
**Risk**: Medium - need to handle multi-monitor coordinates

### Phase 3: Transparent Mode
**Goal**: Bars invisible until activated

1. Add opacity property to bar windows
2. Implement fade-in on sound detection
3. Implement fade-out after sound stops (with delay)
4. Add toggle in settings UI
5. Smooth animation for transitions

**Dependencies**: Phase 1
**Risk**: Low - straightforward opacity manipulation

### Phase 4: Full 7.1 Mode
**Goal**: All 8 channels in spatial layout

1. Design 7.1 spatial layout (screen positions)
2. Create new window/overlay for 7.1 display
3. Map all 8 speakers to visual positions
4. Add mode toggle in settings
5. Handle switching between modes

**Dependencies**: Phase 1
**Risk**: Medium - UI layout complexity

### Phase 5: Keyboard Shortcuts & Launch
**Goal**: Hotkeys and startup integration

1. Register global hotkey for enable/disable
2. Add hotkey configuration in settings
3. Create Windows shortcut for app launch
4. Optional: Add to startup programs
5. Hotkey for mode switching

**Dependencies**: Phase 1
**Risk**: Medium - global hotkey registration can conflict

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Multi-monitor positioning bugs | Medium | High | Test on single monitor first, add bounds checking |
| Global hotkey conflicts | Medium | Low | Allow customizable hotkeys, graceful fallback |
| System tray not showing | Low | Medium | Use proven NotifyIcon patterns |
| Settings file corruption | Low | Medium | Use JSON with defaults fallback |
| Performance with transparency | Low | Low | Use hardware-accelerated opacity |

---

## Success Metrics

- [ ] Settings window opens/closes without issues
- [ ] App minimizes to system tray
- [ ] App can be fully exited from tray menu
- [ ] Bars can be dragged horizontally
- [ ] Lock/unlock mode works correctly
- [ ] Transparent mode fades in/out smoothly
- [ ] 7.1 mode shows all channels spatially
- [ ] Hotkey enables/disables indicators
- [ ] Settings persist across app restarts

---

## Rollback Strategy

Each phase is independent. If a phase causes issues:
1. Revert the phase's changes
2. Previous phases remain functional
3. Settings file versioning allows backward compatibility

---

## Technical Decisions

### Settings Storage
- **Choice**: JSON file in AppData
- **Why**: Simple, human-readable, no database needed
- **Alternative rejected**: Registry (harder to debug/backup)

### System Tray
- **Choice**: Windows Forms NotifyIcon (via interop)
- **Why**: Most reliable, well-documented
- **Alternative rejected**: Hardcodetray (extra dependency)

### Hotkeys
- **Choice**: Windows API RegisterHotKey
- **Why**: Works globally, standard approach
- **Alternative rejected**: Low-level keyboard hooks (overkill)

### 7.1 Layout
- **Choice**: Overlay window with positioned indicators
- **Why**: Flexible, can be on any screen
- **Alternative rejected**: Modifying existing bars (too constrained)
