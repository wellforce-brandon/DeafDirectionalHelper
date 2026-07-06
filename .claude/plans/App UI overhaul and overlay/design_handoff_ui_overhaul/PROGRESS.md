# UI Overhaul — Progress

Tracks phase-by-phase progress against `IMPLEMENTATION_PLAN.md`. Update at the end of every session.

## Phase 0 — Theme foundation & window chrome ✅ (2026-07-06)

- [x] `Theme/Theme.xaml` — all §1.1 color tokens (Color + frozen SolidColorBrush pairs), §1.2 typography styles, corner radii, font families
- [x] `Theme/Controls.xaml` — §1.3 templates: ToggleSwitch (restyled CheckBox, 150 ms knob), SegmentedControl (ListBox-based), ThemedSlider + ValueChip, Primary/Secondary/Danger/DangerLink/Link buttons, ThemedComboBox, RadioCard, KbdChip, Pill; all with 3 px `#F0E442` FocusRing (plus `FocusRingInset` variant for nav/list items)
- [x] `Helpers/DarkChrome.cs` — `DwmSetWindowAttribute` attr 20, fallback 19, applied on `SourceInitialized` when the handle doesn't exist yet
- [x] DarkChrome applied to every window ctor: MainWindow, SettingsWindow, HotkeysWindow, AboutWindow, ProfileEditorWindow, ThemedMessageBox, ThemeSampler (overlay views are borderless — nothing to darken)
- [x] `View/ThemeSampler.xaml(.cs)` — dev-only sampler of every control/state; launch with `-themesampler` command-line flag (checked in `App.OnStartup`; not reachable from any menu)
- [x] Dictionaries merged in `App.xaml`
- [x] `dotnet build` clean (0 warnings, 0 errors); runtime smoke test with `-themesampler` — no XAML parse errors, dark title bars confirmed

Notes / deviations:
- WPF has no letter-spacing property; §1.2 section-label tracking (~0.07em) is not reproduced. ALL-CAPS + Bold + Secondary color carry the style.
- Zero behavior change: all new styles are keyed (no implicit styles), so existing windows look unchanged until Phase 3+ opts them in.

Remaining manual check (needs a human at the machine): Tab through the sampler and confirm the yellow focus ring is visible on every control type.

## Phase 1 — Audio capture engine ✅ (2026-07-06)

- [x] `Audio/CaptureMode.cs` — FollowGame / WindowsDefault / FixedDevice; stored as `general.captureMode`
- [x] `Audio/SessionLocator.cs` — 2 s poll over all render endpoints, session → PID → process name (`AudioSessionControl.GetProcessID`), exposes `TryGetEndpointForProcess` / `GetAudibleSessions` / `GetLoudestSession`
- [x] `Audio/EndpointSelector.cs` — owns device selection per mode; `IMMNotificationClient` sets a dirty flag (reselect on next 200 ms tick, no COM-thread work); FollowGame prefers tracked process, is sticky to the current endpoint while it has audible audio (no ping-pong), else follows loudest session, else default
- [x] `Audio/SignalDoctor.cs` — selected endpoint silent (< 0.005) for 10 s while a tracked-or-foreground session is audible (> 0.05) elsewhere → `MismatchDetected`; one-shot per game, per-run suppression once shown, resets on device switch
- [x] `View/SignalDoctorWindow.xaml(.cs)` — 640 w per 3b: amber banner, LISTENING HERE vs GAME AUDIO IS HERE rows with live meters (150 ms DispatcherTimer on endpoint `MasterPeakValue`), "Listen here instead" (Success fill), "Switch to 'Follow the game'" footer, routing-guide link, Esc closes
- [x] `Audio/Speakers.cs` — device selection replaced by EndpointSelector; channel mapping + stereo/mono duplication untouched; exposes `Sessions` / `Endpoint` / `LastRawPeak`
- [x] Settings: `IgnoredGames` list added, `Version = 2`; inline v1→v2 migration in SettingsManager (formalized in Phase 6)
- [x] Temporary "Capture:" combo in the existing SettingsWindow Audio group (Phase 3 replaces with 3a radio cards); device combo disabled unless FixedDevice
- [x] Build clean; 15 s runtime smoke test on real machine: migration fired, endpoint selected, no crashes

Notes / deviations:
- **Migration choice**: v1 files with a configured `audioDevice` migrate to `FixedDevice` (preserves the user's working setup) instead of the plan's blanket FollowGame default; fresh installs default to FollowGame. Signal Doctor still catches any mismatch and offers the one-click switch.
- FollowGame without a tracked (profiled) game follows the loudest audible session's endpoint, with stickiness to the current endpoint while it stays audible.
- Doctor meters use solid Interactive/Success fills; the "scale color" fill arrives with ScaleEngine in Phase 4.
- Acceptance repro (game → headphones while app fixed to a silent virtual cable) still needs a live manual test with a real game.


## Phase 2 — Game detection & profiles v2 ✅ (2026-07-06)

- [x] `Services/GameDetector.cs` (replaces ProcessMonitor, which is deleted): known games still detected by process scan (so silent games switch, same as before); unknown games per D7 = audible session (peak > 0.05) owned by the foreground PID + window ≥ 95 % of its monitor + not ignored + no profile + once per run
- [x] `Settings/AppProfile.cs` v2 — OverlayStyle / PairWithSideBars / ColorScale / OverlaySize / RingCount / RingMapping / Anchor (with enums `OverlayStyle`, `ColorScale`, `RingMapping`, `OverlayAnchor`); included in CopySettingsFrom
- [x] `Settings/AppSettings.cs` — `ProfileSwitchBehavior { Silent, SwitchWithToast, AskFirst }` (default SwitchWithToast) + `OfferProfileForUnknownGames` (default true)
- [x] `View/ToastHost.cs` — topmost non-activating transparent window, top-right 40 px margins on the target monitor, stacks max 2; unknown-game card (exe icon or "EXE" placeholder, Create / Not now / Ignore), compact switched card (Success dot, bold "Switched to ★ {name}", style · scale, Undo, 6 s auto-dismiss with draining progress bar), ask-first card (Switch / Not now)
- [x] MainWindow wiring: behavior switch (revert-to-Default always silent), Undo restores the pre-switch profile, unknown toast → ProfileEditor seeded with detected name/exe → CreateProfile (seeds from current settings), Ignore persists to `ignoredGames`
- [x] Balloon tip on profile switch removed (ToastHost is the feedback channel now)
- [x] Build clean; 12 s runtime smoke test passed

Notes / deviations:
- Profile v2 presentation fields round-trip in JSON but don't map into BarSettings yet; Phase 3 (Overlay page) and Phase 4 (renderers) consume them.
- No temporary UI for ProfileSwitchBehavior / OfferProfileForUnknownGames; defaults apply until the Phase 3 Profiles page (3d) adds the controls.
- Acceptance items needing a human: fullscreen audible unprofiled app → one toast; Ignore persists across restart; profiled game → switch toast + working Undo; Silent/AskFirst behaviors (edit settings.json to test until Phase 3 UI exists).


## Phase 3 — Settings window rebuild (2a shell) ✅ (2026-07-06)

- [x] `View/ScaleEngine.cs` — §1.4 two-segment ramps (Thermal/Ice/Violet/Classic), invisible below 0.005, swatch stops
- [x] `Settings` bridge — BarSettings gains the v2 presentation fields (+ `LinkIndicators`); AppProfile Apply/Load maps them, so profiles are the WYSIWYG carrier
- [x] `View/Settings/SettingsShell.xaml(.cs)` — 880×640, 212 px rail (profile pill, 6 nav items, live status footer line, Ctrl+Shift+S hint), FooterBg bar (Reset positions + hint, Exit app… danger link, Hide window primary), Esc hides, X hides (`CloseForExit` for real exit), F6 rail↔content, Shift+arrow = 5 slider steps, Up/Down = row navigation
- [x] `OverlayPage` — live preview strip (real capture, 100 ms tick, scale colors), all 8 spec rows, 4 color-scale swatches with gradient dots, contextual SIZE & POSITION per style (side-bar width/spread/link with unlinked left-right sliders, anchor, rings, ring mapping, edge-glow note)
- [x] `AudioDevicePage` — 3 capture radio cards per 3a (RECOMMENDED pill, device combo inside the fixed card), live 8-channel signal check with FL…SR labels + stereo warning, refresh
- [x] `ProfilesPage` — switch-behavior segmented (Silent/Toast/Ask), unknown-game offer toggle, 2-column card grid (★ Default, exe in Consolas, style/scale/auto-switch chips, RUNNING NOW pill, Edit/Duplicate/Delete links, + New profile cell), auto-switch-paused amber note
- [x] `GeneralPage` — start minimized, start with Windows (registry port), target monitor, audio logging + retention (by size/by age) + log size + open/clear
- [x] `HotkeysPage` (kbd chips incl. Ctrl+Shift+E) and `AboutPage`
- [x] MainWindow swapped to SettingsShell (same events/methods contract); build clean; runtime smoke OK
- [x] Legacy bridge: overlay-style changes also set DisplayMode/SurroundLayout so the old views keep rendering until Phase 4

Notes / deviations:
- Profile model change: settings edits now persist into the active profile automatically (WYSIWYG); the old explicit "Save to profile" flow is gone with the old window.
- "Edit positions on screen" button present but disabled until Phase 4 move mode; Exit confirm uses ThemedMessageBox until the Phase 5 themed dialog; new-profile card border is solid (WPF Border has no dashed stroke).
- Legacy-view-only settings (hideLfe, hideYou, surround/dual layout) intentionally have no new UI; they're retired with the old views in Phase 4/6 (values still round-trip in JSON).
- Enter-cycles-segmented not implemented (Space/arrows work); revisit in Phase 6 a11y pass if wanted.
- Old `View/SettingsWindow` is now unreferenced; deletion is scheduled for Phase 6.
- Human checks: keyboard-only walk of every page (focus ring always visible), live preview + signal meters move with audio, profile cards reflect running game.


## Phase 4 — Overlay renderers (5 styles) ✅ (2026-07-06)

- [x] `View/Overlays/LevelEngine.cs` — 100 ms D6 tick: easing 0.45, snap-to-0 below 0.005, trail decay 0.93 floor 0.01, balanced-sound filter (same semantics as old ColoredSpeakers), left/right activity
- [x] `View/Overlays/IOverlayStyle.cs` + `OverlayShapes.cs` (arc-segment ring sectors — WPF has no conic gradient) + `CenterCluster.cs` (chevron / "F" / LFE pulse shared by both ring styles)
- [x] `SideBarsStyle` (2e) — 3 pill segments per side (F/S/R), bottom-up fills in scale color, 4 px white peak ticks, Consolas labels with shadow, backplate; positions from indicator percents
- [x] `EdgeGlowStyle` (2f) — SL/SR side gradients (depth 170·S) + 8 px edge lines, corner radials 560·S for FL/FR/RL/RR, top-center 720×150·S for C, no LFE
- [x] `RadarRingStyle` (2g) — 220·S donut, 45° sectors from −22.5° (C FR SR RR · gap · RL SL FL), inner 54 % hole, faint static rear gap, anchor top/bottom
- [x] `RingPingStyle` (4a/4b) — 280·S box, 3/5/7 bands redistributed 22 %–48.4 % with 0.6 % gaps, Distance (loud = innermost, default) and Meter mappings, dead zone 0.03
- [x] `CompassStripStyle` (2h) — 640·S×96 strip, RL SL FL C FR SR RR cells + LFE after divider, heading marker at screen center, anchor top default
- [x] `OverlayWindow.cs` — single click-through topmost host on the target monitor, PairWithSideBars renders bars + the chosen style, transparent-mode window fades (FadeInMs/FadeOutMs), style rebuild on settings change
- [x] Move mode (Ctrl+Shift+E or the Overlay-page button) — click-through off, dashed Interactive center guide, top pill with the key map, % readout chips, bar dragging with 5 % snap (mirrored while linked), Tab selects bar, arrows nudge 1 % (Shift 5 %), +/− overlay size, Enter commits, Esc reverts; settings sliders refresh live
- [x] Ctrl+Shift+M now cycles the 5 styles with a ToastHost announcement (balloons for toggle/reset also moved to toasts)
- [x] Deleted: DualBarsView, HorizontalDualView, Full7Point1View, ColoredSpeakers, ColorGradient; legacy bridge removed from OverlayPage
- [x] Build clean; runtime smoke on the real machine — new shell + overlay host up, all 6 hotkeys registered, no errors

Notes / deviations:
- Geometry scales by `u = workArea.Height / 1080` (uniform, height-based) × OverlaySize; bar width uses the stored px value × u × S.
- 120/200 ms per-element transitions are approximated by the 100 ms easing tick instead of WPF animations (cheaper, no per-frame allocation; brushes/geometry cached, colors mutated in place).
- DisplayMode/SurroundLayout/DualLayout are now unused by rendering; Phase 6 migration maps them into OverlayStyle and deletes the enums/old SettingsWindow.
- Settings-window-open auto-dragging was replaced by explicit move mode (per design 6).
- Human checks: compare each style against `Redesign.dc.html` cards with audio playing; verify silent = invisible fills, peak-tick decay, instant scale recolor, 50–200 % sizing, move-mode mouse+keyboard parity, CPU a few % max.


## Phase 5 — Dialogs, first-run, tray ✅ (2026-07-06)

- [x] `View/ThemedDialog.cs` base (dark chrome, Bg, centered, no resize) + `ExitConfirmDialog` (2j): Stay open = IsDefault + IsCancel with focus ring on open, Exit app = danger fill — danger is never the default
- [x] `ThemedTextBox` style (46 px, Panel bg, 2 px border, Interactive on focus, radius 8) added to Controls.xaml
- [x] `HotkeysWindow` rebuilt per 2j: 400 w, kbd-chip rows (all 6 hotkeys), "global" note, Done (Enter/Esc both dismiss)
- [x] `ProfileEditorWindow` rebuilt per 2j: 460 w, labels above fields, themed name input + read-only path + Browse…, dynamic "When {exe} is running…" helper, Cancel (IsCancel) + Create profile/Save (IsDefault)
- [x] `FirstRunWizard` (2k): 620 w, 3 progress pills + STEP N OF 3, step 1 device rows with `8 CH · SURROUND`/`2 CH · STEREO` badges and Follow-the-game pinned first, step 2 style cards, step 3 hotkey chips; Skip setup link; Finish applies choices + sets FirstRunCompleted + StartMinimized (D8)
- [x] `TrayFlyout` (2l): 290 w borderless WPF popup above the tray — header (icon, name, Success dot + "Running · ★ {profile}", master toggle), 40 px items with Consolas hotkey hints (Open settings / Next overlay style / Reset positions), divider, DangerText Exit → themed confirm; Esc/deactivate closes; replaces the WinForms ContextMenuStrip entirely
- [x] Startup flow: no FirstRunCompleted → wizard once → tray + toast; StartMinimized → tray + toast; else settings window (existing behavior preserved for current installs)
- [x] Settings migration extended to v3: any pre-v3 file gets FirstRunCompleted = true (existing installs never see the wizard) — verified live (`Settings migrated v1 -> v3`)
- [x] Shell "Exit app…" now uses the themed dialog; build clean; runtime smoke OK

Notes / deviations:
- Wizard step 2 style cards are text cards (name + description), not live-audio previews — revisit if wanted.
- Tray master toggle reuses the 52×28 ToggleSwitch scaled 0.8 rather than a separate 42×24 template.
- ThemedMessageBox still exists for generic confirms (delete profile, clear logs); it predates the plan and works.
- Human checks: Enter/Esc in every dialog, wizard end-to-end on a fresh settings file (delete %APPDATA%\DeafDirectionalHelper\settings.json to test), tray flyout keyboard-only.


## Phase 6 — Migration, a11y polish, docs ✅ (2026-07-06)

- [x] Formal migration in SettingsManager: pre-migration backup `settings.v{n}.bak.json`, v1 style mapping (Bars→SideBars, Bars+HorizontalLine→CompassStrip, Full7Point1+Spatial→RadarRing, Full7Point1+HorizontalLine→CompassStrip, Both→RadarRing+PairWithSideBars) applied to live settings AND every stored profile, SpatialScale→OverlaySize (clamped 0.5–2.0), capture-mode inference, FirstRunCompleted, saved immediately
- [x] **Verified on the real install**: v1 file → backup written → v3 saved; user's Full7Point1/Spatial + SpatialScale 1.8 became RadarRing @ 180 %; BF6/Diablo profiles mapped to RadarRing, Default to CompassStrip; capture stayed FixedDevice
- [x] Perf: OverlayWindow skips all style rendering once silent and peak trails have decayed (`_wasIdle` latch); theme brushes frozen since Phase 0; render loop allocation-free
- [x] A11y (done incrementally in Phases 0–5): 3 px Focus ring on every template, AutomationProperties.Name across shell/pages/dialogs/wizard, AAA token pairs, state never color-only
- [x] Dead code deleted: old SettingsWindow (last one); legacy enums/fields (DisplayMode, SurroundLayout, DualLayout, SpatialScale, HideLfe, HideYou) kept ONLY so old JSON deserializes for migration
- [x] Docs: README (5 styles, color scales, capture modes, detection/profiles, doctor, full hotkey table incl. Ctrl+Shift+E, exclusive-fullscreen note), ACCESSIBILITY.md (audio-session-API paragraph: PID + output meters only), CLAUDE.md project structure, AppVersion → **2.0.0**
- [x] Debug + Release builds clean; final smoke run error-free

Notes / deviations:
- Migration lands on version 3 (not the plan's 2): v2 was the transient Phase 1–5 dev shape, v3 adds FirstRunCompleted.
- "Reduce motion" (SystemParameters.ClientAreaAnimation) not implemented — plan marked it optional; the only continuous animations are the 100 ms level updates, which are the app's core function.
- README screenshots not refreshed (needs real gameplay footage; old screenshot links left in place).

---

**ALL PHASES COMPLETE.** Remaining human verification: visual side-by-side of each overlay style vs `Redesign.dc.html`, keyboard-only walk of the shell + dialogs + tray flyout, Signal Doctor repro with a real game, wizard on a fresh settings file, CPU check during gameplay.

## Post-completion fixes

- **Stale game profile active across restarts** (user-reported): `activeProfileId` persists the last-active profile, so a profile like Diablo stayed active forever if the app closed while the game ran. Fix: `MainWindow.RevertStaleProfile()` at startup activates Default when the active profile's process isn't running. Verified live (`Profile 'Diablo' was active but Diablo IV.exe is not running - reverting to Default`).
- **Overlay hidden again when launched from the pinned Start-menu icon**: the shortcut targets `bin\Release`, which predated the topmost fix. Both configs rebuilt; fix hardened with an `EVENT_SYSTEM_FOREGROUND` WinEvent hook (re-assert on every foreground change, not just every 2 s). Verified at the Win32 level (overlay keeps WS_EX_TOPMOST, z-rank 19, after Start menu open/close). Note: the ring is legitimately covered *while* the Start menu is open — shell windows sit above all overlays.

- **Overlay vanished when the settings window opened at launch** (user-reported): the overlay's Win32 topmost z-band was silently lost when SettingsShell was shown+activated, while `window.Topmost` still read True (WPF caches the property). Any settings change "fixed" it because ApplySettings set window bounds → SetWindowPos re-asserted topmost. Fix: `WindowHelper.ReassertTopmost` (SetWindowPos HWND_TOPMOST + SWP_NOACTIVATE) called after overlay Show, on every SettingsShell.Activated, and every 2 s from the render tick. Verified: ring stays visible with settings open at launch.

## Lessons Learned / Gotchas (routed to LL-G)

- **→ LL-G `kb/wpf/templatebinding-no-conversion.md` (HIGH, new)**: TemplateBinding does no type conversion (double → Text renders empty, silently) and Binding.StringFormat is ignored on object-typed targets like ContentControl.Content; use ContentPresenter + ContentStringFormat. Also covers TemplateBinding-to-ActualWidth unreliability.
- `po:Freeze="True"` on resource-dictionary brushes requires the `presentationOptions` xmlns plus `mc:Ignorable="po"`, or XAML parse fails at runtime (builds fine). (Minor; kept local.)
- Plan-pre-seeded gotchas all confirmed in practice: DWMWA attr 20 needs the window handle (SourceInitialized), `Process.MainModule` throws on protected processes, NAudio session PID via `AudioSessionControl.GetProcessID` with per-session meters wrapped in try/catch, WPF conic wedges as ArcSegment paths, NotifyIcon balloons replaced by ToastHost.
- NAudio 2.2.1: `AudioSessionState` lives in `NAudio.CoreAudioApi.Interfaces`, not `NAudio.CoreAudioApi` (CS0103 if only the latter is imported).
- `TemplateBinding` does no type conversion — binding a double into a `TextBlock.Text` inside a template silently renders empty; use `ContentPresenter` + `ContentStringFormat` for value chips.
