# DeafDirectionalHelper — UI Overhaul & Capture Fix: Phased Implementation Plan

> **For Claude Code.** Drop this file (and this folder) into the repo, then execute phase by phase.
> Suggested location: `.claude/plans/ui-overhaul/PLAN.md`.
> Design references: `Redesign.dc.html` (turns 2–6) and `Current UI.dc.html` (baseline) in this folder — HTML prototypes, **not** production code. Recreate them in WPF/.NET 8 using this spec; every needed value is written out below so the plan is self-sufficient.

---

## 0. Context & ground rules

- **Repo**: `wellforce-brandon/DeafDirectionalHelper` — C# 12, .NET 8 (`net8.0-windows`), WPF, NAudio 2.2.1. Fork of CanetisRadar2 (MIT, keep attribution).
- **Follow repo CLAUDE.md**: Simple > Complex, YAGNI, no DI frameworks / factories / service layers. Run `dotnet build` after every phase; test manually with audio playing.
- **Accessibility story must stay true**: the app reads audio **output levels only** (WASAPI meters — same data as the Windows volume mixer). Nothing in this plan reads game memory, injects code, or hooks games. Session APIs used below only expose *which process owns an audio session* and its meter — still output-side. Update ACCESSIBILITY.md wording in Phase 6.
- **The two burning user problems** this plan fixes first:
  1. **Overlay stays dark in-game**: `Speakers.SelectDevice()` locks onto one endpoint (configured match → else *first 8-channel device* → else default). If the game plays to a different endpoint, the meter read is silent forever. Recovery only handles *device lost*, never *wrong device*.
  2. **No real game detection**: `ProcessMonitor` only watches exes that already have profiles; it cannot discover a new game or notice where its audio goes.
- Windows-only APIs required: `DwmSetWindowAttribute` (dark title bars), `IMMNotificationClient` (default-device changes), `IAudioSessionManager2`/`IAudioSessionControl2` (session → PID), all long-stable.

### Open decisions (defaults chosen — confirm or override before the phase that uses them)

| # | Decision | Default chosen in this plan |
|---|----------|------------------------------|
| D1 | Settings shell | **2a sidebar navigation** (all later designs were built in it) |
| D2 | Overlay style list | `SideBars, RadarRing, RingPing, CompassStrip, EdgeGlow`. Legacy "Both" becomes a `PairWithSideBars` bool (radial/strip styles can pair with side bars). Legacy `HorizontalDualView` + 7.1 horizontal-line layouts **retired** (migration maps them to CompassStrip). |
| D3 | Ring ping mapping | Setting `RingMapping: Meter \| Distance`, default **Distance** (loud = inner ring) |
| D4 | Default color scale | **Thermal** (colorblind-safe); "Classic" available for continuity |
| D5 | Default bar width | **34 px** (new visual); migrated users keep their stored value |
| D6 | Render loop | Audio poll stays 200 ms; add a **100 ms UI smoothing tick** (easing + peak decay) |
| D7 | Unknown-game prompt heuristic | Process has an **active, audible audio session** AND a **foreground/fullscreen-ish main window**, AND not in `IgnoredGames` and has no profile |
| D8 | Startup | After first-run wizard completes, launch **minimized to tray** with a toast (replaces "settings window opens every launch") |

---

## 1. Design tokens (Phase 0 creates these as a ResourceDictionary)

### 1.1 Colors (`Theme.xaml`, keys as written)

| Key | Hex | Use |
|---|---|---|
| `Bg` | `#0B0C0E` | window background |
| `Panel` | `#14161A` | rails, cards, title bar |
| `Raised` | `#1C2026` | active nav item, kbd chips, hover |
| `Hairline` | `#23272E` | inner dividers |
| `Border` | `#2E333B` | card borders |
| `BorderStrong` | `#454C57` | control borders (combo, secondary button, segmented) |
| `Text` | `#F4F5F7` | primary text |
| `TextSecondary` | `#A8AEB8` | helpers, labels |
| `TextMuted` | `#6E757F` | tertiary, region tags |
| `Interactive` | `#56B4E9` | primary buttons, selection, active accents (Okabe–Ito sky blue) |
| `OnInteractive` | `#04121D` | text on Interactive |
| `Success` | `#009E73` | toggles-on, running states, OK badges |
| `OnSuccess` | `#02120C` | text on Success |
| `Warn` | `#E69F00` | unsaved, stereo-mode badges |
| `OnWarn` | `#1A1200` | text on Warn |
| `Danger` | `#D55E00` | exit/destructive fills |
| `OnDanger` | `#1A0A00` | text on Danger |
| `DangerText` | `#FF8A47` | destructive text links on dark |
| `Focus` | `#F0E442` | focus ring — everywhere |
| `FooterBg` | `#0F1114` | window footer strip |
| Doctor banner | bg `#2A1F08`, border `#4A3A10`, text `#F4E9C8`, strong `#FFD97A` | signal-doctor warning header |

Contrast intent: WCAG AAA for body text on its surface; accents are Okabe–Ito colorblind-safe hues. State is never carried by color alone (always paired with a label, position, or shape).

### 1.2 Typography

- Family: **Segoe UI** (UI), **Consolas** (numbers, hotkeys, badges, channel labels).
- Scale: window chrome title 12.5; page title 24/Bold; page subtitle 13.5 Secondary; section label 13/Bold, ALL-CAPS, letter-spacing ~0.07em, Secondary; row label 15.5; row helper 13 Secondary; control text 14–15; value chips Consolas 15/SemiBold; overlay channel labels Consolas 22/Bold (17 in compass cells).
- Hit targets ≥ 44 px for all primary interactive controls (compact card rows may use 38–40).

### 1.3 Control specs (recreate as WPF Styles/ControlTemplates)

- **Toggle switch**: 52×28, radius 16; ON = track `Success`, white 22 px knob right (inset 3); OFF = track `#2E333B` + 1 px `BorderStrong`, knob `TextSecondary` left. Adjacent state label "On"/"Off" 13/SemiBold in `Success`/`TextSecondary`. Animate knob 150 ms.
- **Segmented control**: container `Panel`, 1 px `BorderStrong`, radius 8, padding 3; option padding 8×16, radius 6; active = `Interactive` bg, `OnInteractive` text, Bold.
- **Slider**: track 6 px radius 3 `#2E333B`; filled portion `Interactive`; thumb 24 px circle, `Text` fill, 3 px `Interactive` border. Paired **value chip**: 52–56 px wide, Consolas 15/SemiBold, `Panel` bg, 1 px `BorderStrong`, radius 6.
- **Buttons**: primary = `Interactive` bg, radius 8, minHeight 44, 14.5/Bold `OnInteractive`; secondary = transparent, 2 px `BorderStrong`, radius 8, `Text`; danger fill = `Danger` bg / `OnDanger` text; danger link = `Danger`/`DangerText` underlined text, underline-offset ~3 px.
- **Radio card**: padding 14, radius 10; selected = `Raised` bg + 2 px `Interactive` border + filled radio dot (22 px ring, 10 px dot); unselected = `Panel` + 2 px `Border`.
- **Kbd chip**: Consolas 12.5–14/Bold, `Raised` bg, 1 px `BorderStrong` (bottom 3 px), radius 6–7, padding ~6×10.
- **Pills/badges**: Consolas 10.5–11/Bold, radius 12–20, padding 2–4×8–10 (e.g. `★ Default` on `Interactive`, `RECOMMENDED` on `Success`, `RUNNING NOW` on `Success`, `8 CH · SURROUND` on `Success`, `2 CH · STEREO` on `Warn`).
- **Focus visual**: 3 px `Focus` ring, offset 2–3 px outside the control (inset −3 for nav-rail items), on **every** focusable control — buttons, toggles, segmented, slider thumbs, combos, radio cards, nav items, links.
- **Windows**: dark chrome via `DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE(=20), true)` on every window incl. dialogs; window bg `Bg`; 1 px `Border` outer edge look.

### 1.4 Loudness color scales

Two-segment linear interpolation over stops at t = 0 / 0.5 / 1 (t = processed level 0–1). Level < 0.005 renders **invisible** (fill 0 / alpha 0) — silent means nothing on screen (replaces today's always-white blocks).

| Scale | t=0 | t=0.5 | t=1 | Notes |
|---|---|---|---|---|
| `Thermal` (default) | `#F0E442` | `#E69F00` | `#D55E00` | Okabe–Ito, colorblind-safe |
| `Ice` | `#FFFFFF` | `#56B4E9` | `#0072B2` | single-hue ramp — safest for all CVD |
| `Violet` | `#FFFFFF` | `#CC79A7` | `#882255` | for red/green-heavy games |
| `Classic` | `#FFFF00` | `#FF8000` | `#FF0000` | today's look |

Loudness is always **triple-encoded**: fill amount + ramp color + decaying peak mark. Hue is never the only signal.

### 1.5 Audio processing constants (unchanged unless noted)

- Poll device meter every 200 ms; `Speaker` peak retention 1 s (existing).
- `ApplySettings`: 0 if below `MinThreshold` (default 0.05); × `Sensitivity` (default 1.0, range 0.1–3.0); clamp 0–1.
- Balanced-sound filter: only if dominant ≥ 0.15 and |L−R| < 12 % of dominant (existing).
- **New UI smoothing tick, 100 ms** (D6): `disp += (target − disp) × 0.45`, snap to 0 below 0.005; peak trail `trail = max(trail × 0.93, disp)`, floor 0.01.
- Transparent mode fades: in 100 ms, out 500 ms (existing settings).
- Channel map (unchanged): 0 FL, 1 FR, 2 C, 3 LFE, 4 RL, 5 RR, 6 SL, 7 SR. Left activity = max(FL, RL, SL); right = max(FR, RR, SR).

---

## Phase 0 — Theme foundation & window chrome

**Goal:** the token system exists and every current window can opt in, with zero behavior change.

**New files**
- `Theme/Theme.xaml` — all §1.1 brushes, thicknesses, corner radii, text styles.
- `Theme/Controls.xaml` — styles/templates from §1.3 (`ToggleSwitch` [restyled CheckBox or custom], `SegmentedControl` [ListBox-based], Slider, primary/secondary/danger Buttons, ComboBox, radio card, kbd chip, pill).
- `Helpers/DarkChrome.cs` — `public static void Apply(Window w)` P/Invoking `DwmSetWindowAttribute` attr 20 (fallback 19 for older builds).
- `View/ThemeSampler.xaml(.cs)` — dev-only window rendering every control in rest/hover/focused/disabled (mirrors design option **5c**). Not shipped in release menu; launchable via debug flag.

**Tasks**
1. Create dictionaries, merge in `App.xaml`.
2. Implement DarkChrome; call from every existing window's ctor.
3. Build all control templates **including the 3 px `#F0E442` focus visual on every template** (today only CheckBox/Button have one).
4. Theme sampler window; verify keyboard focus ring is visible on every control type.

**Acceptance**
- `dotnet build` clean. All existing windows show dark title bars. Sampler shows every §1.3 control matching the spec values; Tab order traverses all of them with a visible yellow ring.

---

## Phase 1 — Audio capture engine (fixes "overlay never lights up")

**Goal:** three capture modes; the app finds game audio wherever it plays; a Signal Doctor catches mismatch live. No UI redesign yet — wire into existing settings with a temporary combo if needed.

**New files**
- `Audio/CaptureMode.cs` — `enum CaptureMode { FollowGame, WindowsDefault, FixedDevice }` (+ settings field, default `FollowGame`).
- `Audio/EndpointSelector.cs` — owns *which MMDevice to read*:
  - `FixedDevice`: current `SelectDevice()` logic (configured match → first 8-ch → default).
  - `WindowsDefault`: default render endpoint; subscribe `IMMNotificationClient.OnDefaultDeviceChanged` → reselect (NAudio: `MMDeviceEnumerator.RegisterEndpointNotificationCallback`).
  - `FollowGame`: ask `SessionLocator` for the endpoint hosting the tracked game's session; fallback → WindowsDefault behavior. **Read that endpoint's full channel meter** (8-ch if available — better than the session's own meter for surround).
- `Audio/SessionLocator.cs` — every 2 s (and on session-created notification if easy): for each active render endpoint, walk `device.AudioSessionManager.Sessions`; for each session get PID (`IAudioSessionControl2.GetProcessId` — NAudio `AudioSessionControl.GetProcessID`), state, and map PID → process name. Exposes: `TryGetEndpointForProcess(name)`, `GetAudibleSessions()` (list of {processName, pid, endpoint, sessionPeak?}).
- `Audio/SignalDoctor.cs` — detector: if **selected endpoint peak < 0.005** continuously for **10 s** while **some tracked/foreground game session is audible (session or its endpoint peak > 0.05)** → raise `MismatchDetected(gameProcess, gameEndpoint, selectedEndpoint)`. One-shot per game launch; reset on device switch. 
- `View/SignalDoctorWindow.xaml(.cs)` — per design **3b** (640 px wide, spec below).

**Modified**
- `Audio/Speakers.cs` — replace internal device selection with `EndpointSelector`; keep channel mapping + stereo/mono duplication exactly as-is.
- `Settings/AppSettings.cs` — add `CaptureMode`, `IgnoredGames: List<string>`, bump `Version = 2` (migration in Phase 6 task if any field renames).

**SignalDoctorWindow spec (3b)**
- 640 w, dark chrome, title "Signal doctor".
- Amber banner: bg `#2A1F08`, 1 px bottom `#4A3A10`; warning triangle (`Warn`); text `#F4E9C8` with bold `#FFD97A` lead: *"Overlay armed but silent. {exe} has been playing audio for 12 s, but none of it reached the device you're listening to."*
- Device rows (radius 10, `Panel` bg): selected row = 2 px `Interactive` border + pill `LISTENING HERE` (Interactive/OnInteractive) + channel count chip + horizontal meter (12 px tall, radius 6, `Bg` track, fill = live level in scale color) + right label "silent 12 s" (muted). Candidate row = 2 px `Success` border + pill `GAME AUDIO IS HERE` + live meter + "● {exe} session" in `Success`; primary button **"Listen here instead"** (Success fill) + note "left / right only on 2 channels" when 2-ch.
- Footer: secondary-outline button **"Switch to 'Follow the game'"** (Interactive border/text) + helper *"tracks {exe} wherever it plays, forever"* + link "7.1 routing guide".
- Buttons apply the change immediately, close, and suppress re-trigger for that game session.

**Acceptance**
- Repro the original bug (game → headphones, app fixed to virtual cable): doctor pops within ~12 s; both one-click fixes work; overlay lights up after.
- `FollowGame` mode: launch game outputting to non-default device — overlay reacts with no user action.
- Unplugging/replugging devices doesn't crash (existing recovery still works).

---

## Phase 2 — Game detection & per-game profiles v2

**Goal:** the app discovers games by their audio sessions, prompts per user preference, and profiles carry full per-game presentation.

**New/modified**
- `Services/GameDetector.cs` (evolves `ProcessMonitor`): inputs = `SessionLocator.GetAudibleSessions()` + foreground-window check (`GetForegroundWindow` → PID; "fullscreen-ish" = window bounds ≥ 95 % of its monitor). Emits:
  - `KnownGameStarted(profile)` — session/process matches a profile's `ProcessName`.
  - `UnknownGameDetected(processName, exePath?)` — D7 heuristic; not in `IgnoredGames`, no profile, first time this run.
  - `GameStopped(profile)` → revert to Default profile.
- `Settings/AppProfile.cs` v2 — add: `OverlayStyle` (D2 enum), `PairWithSideBars: bool`, `ColorScale` (Thermal/Ice/Violet/Classic), `OverlaySize: double (0.5–2.0, default 1)`, `RingCount (3|5|7, default 5)`, `RingMapping (Meter|Distance, default Distance)`, `Anchor (Top|Bottom)`, keep existing width/spread/positions/sensitivity/etc.
- `Settings/AppSettings.cs` — `ProfileSwitchBehavior { Silent, SwitchWithToast, AskFirst }` default `SwitchWithToast`; `OfferProfileForUnknownGames: bool = true`.
- `View/ToastHost.cs` — a click-through-except-buttons, topmost, per-monitor toast layer window (top-right, 40 px margins); stacks up to 2 toasts.
- Toast specs (**3c**, sized for 1080p ≈ 420 px wide in real px):
  - *Unknown game*: card bg `rgba(13,15,19,0.94)`, 2 px `Border`, radius 16, padding 24; 56 px `Raised` square with "EXE" (Consolas) as icon placeholder (use file icon if `exePath` accessible); title "New game detected" (24/Bold scaled ≈ 17/Bold real); body: *"{exe} is running and making sound. Give it its own profile? Overlay style, colors and positions will switch automatically every time it runs."*; buttons: **Create {Name} profile** (primary), **Not now** (secondary), **Ignore this game** (muted underline link → adds to `IgnoredGames`).
  - *Known game switched*: compact card: `Success` dot + "**Switched to ★ {name}** — {style} · {scale} scale" + **Undo** link (Interactive, underlined); auto-dismiss 6 s with a 6 px progress bar along the bottom (`Border` color). Shown only when behavior = SwitchWithToast; AskFirst uses the unknown-style card with **Switch** / **Not now**.
- Profile create flow: “Create profile” → ProfileEditor (Phase 5 restyles it; reuse current one until then) seeded from current settings + detected exe.

**Acceptance**
- Launch an unprofiled game (or any audible fullscreen app): unknown-game toast appears once; Ignore persists across restarts.
- Profiled game: auto-switch fires with toast + Undo restores previous profile; `Silent` and `AskFirst` behaviors honored.
- Settings JSON round-trips v2 fields; old v1 file loads without data loss (temporary inline migration; formalized in Phase 6).

---

## Phase 3 — Settings window rebuild (2a sidebar shell)

**Goal:** replace `SettingsWindow` with the 2a design; all existing settings reachable; full keyboard navigation per turn-5 spec.

**New files:** `View/Settings/SettingsShell.xaml(.cs)`, one UserControl per page: `OverlayPage`, `AudioDevicePage`, `ProfilesPage`, `GeneralPage`, `HotkeysPage`, `AboutPage`.

**Shell spec (2a)**
- Window 880×640 (min 820×600), `Bg`, dark chrome, title "DeafDirectionalHelper".
- Left rail 212 px, `Panel` bg, 1 px `Hairline` right edge, padding 14×10:
  - Header: "Settings" 15/Bold + profile pill `★ {active profile}` (Interactive bg pill).
  - Nav items 44 px tall, radius 6, 15 px: inactive `TextSecondary`, padding-left 16; active: `Raised` bg, 4 px `Interactive` left border, padding-left 12, SemiBold `Text`. Order: Overlay, Audio & device, Profiles, General, Hotkeys, About.
  - Rail footer (top `Hairline`): status line — 10 px `Success` dot + "{exe} detected · audio flowing" / "Overlay running · {n}-channel device" (12.5 Secondary) + muted Consolas hint "Ctrl+Shift+S opens this window".
- Footer bar: `FooterBg`, top `Hairline`, padding 14×26: left secondary button **Reset positions** + inline Consolas hint "Ctrl+Shift+P"; spacer; **Exit app…** danger link (opens Exit dialog); **Hide window** primary (min 130).
- Window close (X) hides (existing behavior); Esc hides.

**Overlay page** (rows: minHeight 58–64, 1 px `Raised` bottom divider; label 15.5 + helper 13 Secondary left, control right)
1. Live preview strip (top): card `Panel`/`Border` radius 8, padding 12×16; 180×74 mini screen (`#080A0C`, radius 5, `Hairline` border) with two 9 px vertical meters inset 12 px left/right filling bottom-up in scale colors from the **real capture**, "LIVE" Consolas 9 centered `#4A5158`; beside it title "Preview reacts to real audio" + helper.
2. **Show sound indicators** — toggle (maps `Display.Enabled`).
3. **Overlay style** — 5 radio pills (D2): Side bars · Radar ring · Ring ping · Compass · Edge glow; plus sub-toggle **Pair with side bars** visible for non-SideBars styles. Helper: "Switch anytime with Ctrl+Shift+M" (hotkey now cycles the 5 styles).
4. **Only show while sound plays** — toggle (`TransparentMode`); helper "Indicators fade out in silence instead of staying white".
5. **Sensitivity** — slider 0.1–3.0 step 0.1 + value chip (F1); helper "How easily indicators light up".
6. **Noise floor** — slider 0–0.30 step 0.01 + chip (F2); helper "Ignore sounds quieter than this".
7. **Filter self sounds** — toggle (`IgnoreBalancedSounds`); helper "Hides audio equal in both ears — your own steps".
8. **Overlay strength** — slider 0.3–1.0 step 0.05 (`MaxOpacity`), chip in %.
9. **Color scale** — 4 swatch-cards (Thermal default / Ice / Violet / Classic), each an 11 px 3-stop gradient dot + name; helper notes colorblind-safety (per design 2i).
10. **SIZE & POSITION** section (6a/6c — **contextual per style**, only matching rows render):

| Style | Rows shown |
|---|---|
| all | **Overlay size** slider 50–200 % step 5, default 100 % (tick mark at 100 %) |
| Side bars | **Bar width** 20–150 px (default 34, D5) · **Spread** 10–50 % default 30 % · **Link left / right** toggle (default on; off → separate Left 5–45 % / Right 55–95 % sliders, like today) |
| Radar ring | **Anchor** segmented Bottom/Top (default Bottom) |
| Ring ping | **Rings** segmented 3/5/7 (default 5) · **Ring mapping** segmented Distance/Meter (D3) · **Anchor** Bottom/Top |
| Compass | **Anchor** segmented Top/Bottom (default Top) |
| Edge glow | *(no extra rows — size = bloom depth; note text: "Hugs all four edges — nothing to move or spread")* |

  - Buttons: **Edit positions on screen** primary + "Ctrl+Shift+E" hint; **Reset** secondary + "Ctrl+Shift+P".

**Audio & device page (3a)**
- Capture-mode radio cards: **Follow the game** + `RECOMMENDED` pill (helper: "Reads the game's own audio session on whichever device it plays to. Zero setup — fixes 'overlay never lights up'."); **Windows default device** ("Follows whatever Windows is playing to — survives headphone ↔ speaker swaps."); **One specific device** ("Best for full 7.1 via an 8-channel virtual cable…") with device combo inside showing "{FriendlyName} · {n} ch".
- **Live signal check** card: `Success` dot + "Live signal check — receiving {exe} on {device}"; 8 vertical meters 26×56 (radius 5, `Bg` track, 1 px `Border`) labeled FL FR C LFE RL RR SL SR (Consolas 10.5), fills = live processed levels in scale color; amber note when source is 2-ch: "Stereo source: directions collapse to left / right. Route to an 8-channel device for full surround."
- "Refresh devices" secondary button.

**Profiles page (3d)**
- Row: "When a profiled game starts:" + segmented **Switch silently / Switch + toast / Ask first**.
- Row: "Offer a profile when an unknown game makes sound" toggle.
- Card grid, 2 columns, gap 12. Card: `Panel`, 1 px `Border`, radius 10, padding 14×16 — name 16/Bold (★ prefix on Default), exe Consolas 12 Secondary (+ "last seen …" when known); chip row: style chip (mini glyph + name), scale chip (gradient dot + name), "auto-switch on"; links **Edit** (Interactive) / **Duplicate** / **Delete** (`DangerText`; hidden on Default). Active-game card: 2 px `Success` border + `RUNNING NOW` pill. Last cell: 2 px dashed `BorderStrong` "+ New profile" card, helper "or launch a game and let detection offer one".
- Auto-switch pauses while settings window is open (existing behavior; show the amber note).

**General page** — port existing: Start minimized to tray / Start with Windows (registry logic unchanged) / audio-event logging + retention combos + log size + Open/Clear logs, restyled as rows.

**Hotkeys page** — kbd-chip table (2j content) incl. the new `Ctrl+Shift+E`; note hotkeys are global.
**About page** — name, version (`AppVersion`), description, GitHub link, CanetisRadar2/MIT attribution.

**Keyboard navigation (turn-5 spec — implement shell-wide)**
- Tab/Shift+Tab natural order; **Up/Down** move focus between *rows* (attach `KeyboardNavigation.DirectionalNavigation`/custom handler so Down lands on the next row's control); **Left/Right** adjust focused control (slider ±1 step, **Shift ±5 steps**; segmented prev/next; toggle off/on); **Space/Enter** toggle/activate (Enter cycles segmented); **F6** rail ↔ content; **Esc** hides window. All controls show the 3 px `Focus` ring (from Phase 0 templates). Set `AutomationProperties.Name` on every control (label + value).

**Acceptance**
- Every setting that exists today is reachable and persists. Contextual size rows swap correctly per style with no orphan "Bar width" on non-bar styles. Entire window operable mouse-free per the map above; focus never invisible. Live preview + signal meters move with real audio.

---

## Phase 4 — Overlay renderers

**Goal:** the five overlay styles from the designs replace `DualBarsView` / `Full7Point1View` / `HorizontalDualView`. All geometry below in design px on a 1920×1080 work area — convert to fractions of the target monitor's work area and multiply sizes by `S = OverlaySize` (and DPI-scale). Draw with WPF shapes/geometry — no bitmaps required.

**Architecture (keep simple, D2)**
- `View/Overlays/OverlayWindow.xaml(.cs)` — one borderless, transparent, topmost, click-through window per monitor spanning the work area; hosts the active style renderer + move-mode adorners. Reuse `WindowHelper.SetClickThrough`.
- `View/Overlays/IOverlayStyle` — `Attach(Canvas)`, `Render(LevelFrame)`, `ApplyLayout(settings, workArea)`. One class per style. `LevelFrame` = 8 smoothed levels + 8 peak trails + left/right activity (from the 100 ms tick, D6/§1.5).
- `View/ScaleEngine.cs` — §1.4 ramps: `Color At(level)`, `Color WithAlpha(level, a)`.

**Style 1 — SideBars (2e)** *(replaces DualBarsView)*
- Bar: width `34·S` (or user Bar width), inset 26 from screen edge, 90 top/bottom; 3 vertical segments, flex heights 264/348/264, 8 px gaps; segment radius = width/2; backplate `rgba(10,13,16,0.55)`, 2 px border `rgba(255,255,255,0.35)`.
- Per segment: fill rect anchored bottom, height = level %, fill = scale color (height/color transitions 120 ms); **peak tick** 4 px white bar, radius 2, inset 2 px, bottom = trail % (200 ms linear).
- Channels: left F=FL, S=SL, R=RL; right F=FR, S=SR, R=RR. Labels "F/S/R" Consolas 22/Bold `rgba(255,255,255,0.9)`, shadow 0 1 4 `rgba(0,0,0,0.9)`, beside bar at segment centers (right side of left bar, left side of right bar).
- Positions from `LeftIndicatorPercent`/`RightIndicatorPercent` (bar centers), clamped to halves as today.

**Style 2 — EdgeGlow (2f)**
- SL/SR: full-height side gradients, depth `170·S`, `LinearGradient(inward)` from `WithAlpha(level, min(0.85, level))` to transparent; plus a sharp 8 px solid edge line, alpha `min(1, level·1.4)`.
- FL/FR/RL/RR: corner radial gradients `560·S` square at each corner (FL top-left, FR top-right, RL bottom-left, RR bottom-right), center alpha `min(0.8, level)` fading out by 60 %.
- C: top-center `720×150·S` vertical gradient. LFE: none (or pair with side bars).

**Style 3 — RadarRing (2g)**
- Donut `220·S` at anchor (Bottom: horizontally centered, 70 px above work-area bottom; Top mirrors, 70 below top).
- Base: circle `rgba(9,11,14,0.5)`, 2 px border `rgba(255,255,255,0.3)`. Sector band: conic wedges 45° each starting at −22.5°, clockwise: **C, FR, SR, RR, [gap], RL, SL, FL**; wedge alpha `min(0.92, 0.15 + level·0.85)` in scale color; donut hole = inner 54 % (draw ring band geometry, e.g. `ArcSegment` path per wedge between r54 % and r100 %). Rear gap stays near-transparent (`rgba(255,255,255,0.05)`).
- Center: white up-chevron (triangle ~28×24, alpha 0.92); "F" Consolas 18/Bold above the ring; LFE = center disc 68·S px, alpha `min(0.9, 0.12 + level)`, `ScaleTransform 1 + level·0.35`, 120 ms.

**Style 4 — RingPing (4a/4b)**
- Box `280·S`, same anchor rules. 5 concentric bands; band radii as % of box half-size: `[22–26.8] [27.4–32.2] [32.8–37.6] [38.2–43.0] [43.6–48.4]` (0.3 % feather). Ring count 3/7 → redistribute bands evenly between 22 % and 48.4 % with 0.6 % gaps.
- Base outlines: 1 px `rgba(255,255,255,0.16)` per band (outermost 1.5 px @ 0.24). Same 45° sector layout as RadarRing.
- Level bucket `b = ceil(level·N)` (0 if level < 0.03; N = ring count). **Distance** (default): only band `N+1−b` lights (loud = innermost; quiet blips on the rim march inward as they grow). **Meter**: bands `1..b` light from center outward. Wedge alpha `min(0.95, 0.35 + level·0.65)`.
- Center chevron + "F" label as RadarRing. LFE: center pulse as RadarRing.

**Style 5 — CompassStrip (2h)**
- Strip `640·S × 96` at anchor (Top default: centered, 36 px from top). Bg `rgba(9,11,14,0.55)`, 2 px border `rgba(255,255,255,0.3)`, radius 18, padding 0 18 8; content bottom-aligned.
- 7 equal cells (gap 10), order **RL SL FL C FR SR RR**; per cell: meter 30×44 radius 6, track `rgba(255,255,255,0.12)`, fill bottom-up (120 ms) in scale color; label below, Consolas 17/Bold `rgba(255,255,255,0.9)` + shadow. Then a 2 px `rgba(255,255,255,0.2)` divider and an **LFE** cell (label 15 @ 0.75 alpha).
- Screen-center heading marker: small white down-triangle (18×12, alpha 0.85) just above the strip line at screen center.

**Shared behaviors**
- `PairWithSideBars` renders SideBars + the chosen radial/strip style together (replaces old "Both").
- Transparent-until-sound: per-element opacity as today + window fade 100/500 ms.
- `Ctrl+Shift+M` cycles the 5 styles; balloon/toast announces the new style.
- **Move mode** (`Ctrl+Shift+E`, also from settings button): disables click-through; dashed center guide (2 px, `Interactive` at 0.7, 14/28 px dashes); top-center pill "MOVE MODE — drag · snaps every 5 % · mirrored while linked" + Consolas hint "Ctrl+Shift+P resets" (pill: `rgba(8,10,13,0.85)`, 1.5 px `Interactive` border, radius 26); per-indicator % readout chips near each draggable; drag snaps to 5 % grid. Keyboard: **Tab** cycles indicators, **arrows** nudge 1 %, **Shift+arrows** 5 %, **+/−** Overlay size ±5 %, **Enter** commit, **Esc** revert, `Ctrl+Shift+E` exits. Settings sliders refresh live (existing `PositionChanged` pattern).
- Delete `HorizontalDualView` + horizontal-line layout code paths after migration mapping (D2).

**Acceptance**
- Each style matches its design card side-by-side (open `Redesign.dc.html` 2e/2f/2g/2h/4a/4b next to the running app). Silent screen = fully invisible overlays. Peak ticks decay visibly. Scale switching recolors all styles instantly. Overlay size 50–200 % scales per §6c mapping. Move mode: full mouse + keyboard parity. CPU stays reasonable (≤ a few % on the 100 ms tick; cache brushes/geometry, no per-frame allocation).

---

## Phase 5 — Dialogs, first-run, tray

**New/rebuilt windows** (all: dark chrome, `Bg`, radius-10 look, Phase-0 controls; **every dialog wires `IsDefault` on the safe action and `IsCancel` on dismiss — Enter/Esc always work; danger is never the default**):

1. `ThemedDialog` base + **Exit confirm** (2j): title "Exit the app?" 17/Bold; body 14.5 Secondary: *"Sound indicators stop until you start it again. To just hide this window, use **Hide** instead."*; buttons right: **Stay open** (secondary, IsDefault + IsCancel, focus ring visible on open) + **Exit app** (danger fill).
2. **Hotkeys dialog** (2j): 400 w; 5+1 rows: kbd chip (Consolas 14/Bold, `Raised`, 1 px `BorderStrong`, 3 px bottom border, radius 7) + description 14.5 — Ctrl+Shift+R "Overlay on / off", +M "Next overlay style", +S "Open settings", +P "Reset positions", +H "Show this card", +E "Move mode"; **Done** primary bottom-right.
3. **Profile editor** (2j): 460 w; labels *above* fields (14/SemiBold): "Profile name" text input (46 px, `Panel` bg, 2 px border — `Interactive` when focused, `BorderStrong` idle, radius 8); "Game or app (optional)" read-only path input + **Browse…** secondary; helper below: *"When {exe} is running, this profile switches on automatically."*; footer **Cancel** (IsCancel) + **Create profile / Save** (primary, IsDefault).
4. **First-run wizard** (2k), shown once (`FirstRunCompleted` flag), 620 w: progress = three 28×8 pills (active `Interactive`) + "STEP N OF 3" Consolas 12.
   - Step 1 "Pick the audio device to listen to" — body reiterates output-levels-only; radio rows 56 px with channel badges `8 CH · SURROUND` (Success) / `2 CH · STEREO` (Warn); default selection honors FollowGame (row "Follow the game (recommended)" pinned first).
   - Step 2 "Choose your overlay style" — 5 style cards with a small live preview (reuse mini-preview control + real audio).
   - Step 3 "Hotkeys" — chip table + **Finish**. Footer: "Skip setup" link + **Next →** primary. On finish: apply, launch minimized to tray with a toast (D8).
5. **Tray flyout** (2l): keep WinForms `NotifyIcon` for the icon; left/right-click opens a borderless WPF popup window anchored above the tray (290 w, `Panel`, 1 px `Border`, radius 10, shadow): header = icon 24 + "DeafDirectionalHelper" 13.5/Bold + `Success` dot + "Running · ★ {profile}" 12 + master **toggle** (42×24) that enables/disables indicators; items (40 px, radius 6, hover `Raised`): Open settings / Next overlay style / Reset positions, each with right-aligned Consolas 11 hotkey hint; divider; **Exit** in `DangerText` (opens Exit confirm). Replace old `ContextMenuStrip`. Balloon tips → ToastHost toasts.

**Acceptance:** Enter/Esc behave in every dialog; wizard completes and writes settings; tray flyout fully keyboard operable; no WinForms menu remains.

---

## Phase 6 — Migration, a11y polish, docs

1. **Settings migration v1→v2**: on load of `version: 1` — map `DisplayMode.Bars→SideBars`, `Full7Point1 (Spatial)→RadarRing`, `Full7Point1 (HorizontalLine)→CompassStrip`, `Bars (HorizontalLine dual)→CompassStrip`, `Both→RadarRing + PairWithSideBars`; `SpatialScale→OverlaySize`; keep width/spread/positions/thresholds; write backup `settings.v1.bak.json`; bump to 2. Same mapping inside each stored profile.
2. **Accessibility pass**: `AutomationProperties.Name/HelpText` on all controls (Narrator announces label + value); verify focus ring on every control in every window; verify all text ≥ AAA on its surface (tokens already are — check any new pairings); respect Windows "reduce motion"? (optional: skip transitions when `SystemParameters.ClientAreaAnimation` is false).
3. **Perf pass**: no allocations in the 100 ms tick; freeze brushes; overlay windows skip rendering when all levels 0.
4. **Docs**: README (new features, capture modes, styles, hotkeys incl. Ctrl+Shift+E, screenshots), ACCESSIBILITY.md (add session-API paragraph: reads session PID + output meters only), CLAUDE.md project-structure section, bump `AppVersion`.
5. Delete dead code: old SettingsWindow, HorizontalDualView, old view XAML, unused styles.

**Acceptance:** old settings file from a real install migrates losslessly (spot-check every field); `dotnet build -c Release` clean; manual smoke of all hotkeys, all styles, doctor, prompts, wizard, tray.

---

## Suggested Claude Code workflow

- One phase per session. Start each session: read this plan + `PROGRESS.md`; end each session: update `PROGRESS.md` (checklist per phase), run `/build-and-fix`, then `/code-review` on touched files.
- Per repo CLAUDE.md RULE 1/3: check LL-G + BP indexes before scripting-heavy work.
- Don't start Phase 4 before Phase 0 templates exist; Phases 1–2 are logic-first and can ship behind the current UI if needed.

## Lessons Learned / Gotchas (pre-seeded — append as you go)

- WPF `AllowsTransparency` windows can't render over **exclusive-fullscreen** games — document "borderless windowed" requirement (already true of the current app).
- NAudio 2.2.1: session PID via `AudioSessionControl.GetProcessID`; a per-session `IAudioMeterInformation` needs a COM QI on the session control — if flaky, skip session meters and use the **endpoint** meter of the endpoint hosting the session (the plan's primary path anyway).
- `DWMWA_USE_IMMERSIVE_DARK_MODE` is 20 on Win10 20H1+; try 19 as fallback; call **after** the window handle exists (`SourceInitialized`).
- Conic wedges: WPF has no conic gradient — build ring sectors as `Path`/`ArcSegment` geometry (also crisper than gradients).
- `Process.MainModule` throws on elevated/protected processes — wrap in try/catch, fall back to process name only.
- Balloon `NotifyIcon.ShowBalloonTip` is unreliable on Win11 — the ToastHost replaces it.
