# DeafDirectionalHelper

An accessibility tool for deaf and hard-of-hearing gamers to visualize directional audio cues.

> **Accessibility Statement**: This tool reads audio OUTPUT from your Windows sound card using standard Windows APIs. It does NOT interact with, read from, or modify any game. See [ACCESSIBILITY.md](ACCESSIBILITY.md) for full details.

## The Problem

As someone who has been single-sided deaf (SSD) since age 5, I can **hear** sounds but I **cannot locate** them directionally.

In competitive gaming, this is a significant disadvantage. Other players can hear enemies approaching from behind or to the side, while I'm limited to visual cues only. Directional audio is a core game mechanic that I simply cannot use.

This tool bridges that gap by converting directional audio into visual indicators, giving SSD and deaf gamers the same spatial awareness that hearing players take for granted.

## How It Works

DeafDirectionalHelper reads audio levels from your sound card's output using Windows WASAPI (the same API that Windows volume meters use) and displays visual indicators showing which direction sounds are coming from.

### Technical Approach

```
[Any Audio Source] --> [Windows Audio] --> [Sound Card] --> [We read peak levels here]
                                                                      |
                                                                      v
                                                            [Visual Overlay]
```

- Uses standard Windows audio APIs (NAudio/WASAPI)
- Reads `AudioMeterInformation.PeakValues` - same data as Windows volume meters
- Works with any audio source (games, music, videos, etc.)
- No game-specific code or interaction

## Features

### Overlay styles

1. **Side bars** - Two vertical bars at the screen edges with front / side / rear segments and decaying peak marks
2. **Radar ring** - A compass donut near the screen bottom; sectors light toward the sound
3. **Ring ping** - Concentric rings; by default loud sounds ping close to the center (distance mapping), or fill outward like a meter
4. **Compass** - A slim strip of channel meters (RL SL FL C FR SR RR + LFE) along the top edge
5. **Edge glow** - The screen edges themselves glow toward the sound; nothing sits in your view

Any radial/strip style can be **paired with side bars**. Cycle styles anytime with `Ctrl+Shift+M`, and reposition/resize on screen with move mode (`Ctrl+Shift+E`).

### Loudness color scales

Loudness is triple-encoded: fill amount + ramp color + a peak mark, so hue is never the only signal. Silence renders nothing.

- **Thermal** (default) - yellow → amber → vermillion (Okabe-Ito, colorblind-safe)
- **Ice** - white → sky blue → deep blue (single hue, safest for all CVD)
- **Violet** - white → orchid → plum (for red/green-heavy games)
- **Classic** - the original yellow → orange → red

### Audio capture modes

- **Follow the game** (recommended) - reads the game's own audio session on whichever device it plays to; zero setup
- **Windows default device** - follows headphone ↔ speaker swaps
- **One specific device** - best for full 7.1 via an 8-channel virtual cable (Voicemeeter, VB-Cable)

If the overlay is armed but silent while a game plays audio to a different device, the **Signal Doctor** pops up with live meters and two one-click fixes.

### Game detection & profiles

- Games are discovered by their audio sessions; an unknown fullscreen game making sound triggers a one-time toast offering a profile
- Per-game profiles carry the full presentation (style, colors, size, positions, sensitivity) and switch automatically - silently, with an undoable toast, or ask-first
- First-run wizard picks your device, style and shows the hotkeys; afterwards the app lives in the system tray with a themed flyout

### Settings

- Sensitivity, noise floor and overlay strength with live preview
- Transparent mode (indicators appear only when sound plays, with fade in/out)
- Balanced-sound filter to hide your own footsteps
- Overlay size 50-200 %, per-style position controls, keyboard-navigable settings window
- Global hotkeys for quick control

## Requirements

- Windows 10/11
- .NET 8.0 Runtime
- Audio device (7.1 surround recommended, stereo supported)

## Installation

1. Download the latest release
2. Extract to a folder
3. Run `DeafDirectionalHelper.exe`
4. Configure your audio device in settings if needed

## Hotkeys

| Hotkey | Action |
|--------|--------|
| Ctrl+Shift+R | Overlay on / off |
| Ctrl+Shift+M | Next overlay style |
| Ctrl+Shift+S | Open settings |
| Ctrl+Shift+P | Reset positions |
| Ctrl+Shift+H | Show the hotkey card |
| Ctrl+Shift+E | Move mode (drag, arrow keys, +/- size, Enter saves, Esc reverts) |

Hotkeys are global - they work while a game has focus.

> **Note**: like all overlay tools, the indicators cannot render over exclusive-fullscreen games. Use borderless windowed mode.

## Screenshots

<a href="https://cdn.discordapp.com/attachments/733779754121166978/822505618149605477/unknown.png">
  <img src="https://cdn.discordapp.com/attachments/733779754121166978/822505788626173952/unknown.png" alt="screenshot 1"/>
</a>

What you see there is a 3 screen setup, the bars are on the 2 other screens while the game is on the middle. This allows keeping fullscreen and high refresh rates.

## Important Notes

### What This Tool Does

- Reads audio OUTPUT levels from Windows APIs
- Displays visual overlay based on audio levels
- Provides accessibility parity for deaf/hard-of-hearing players

### What This Tool Does NOT Do

- Does NOT read game memory or processes
- Does NOT modify any game files
- Does NOT inject code into any application
- Does NOT intercept network traffic
- Does NOT provide any gameplay automation

For complete technical details, see:
- [ACCESSIBILITY.md](ACCESSIBILITY.md) - Full accessibility statement
- [Audio/README.md](Audio/README.md) - Technical implementation details

## Building from Source

```bash
dotnet build --configuration Release
```

## License

Licensed under MIT - see [LICENSE](LICENSE) for details.

## Credits & Attribution

This project is a fork of [CanetisRadar2](https://github.com/Alaanor/CanetisRadar2) by **Maxime Bonvin** ([@Alaanor](https://github.com/Alaanor)).

The original CanetisRadar2 was created to help deaf and hard-of-hearing gamers visualize directional audio cues. We are deeply grateful to Maxime for creating this tool and releasing it under the MIT license, making it possible for the community to continue developing accessibility solutions.

### Original Project
- **Repository**: https://github.com/Alaanor/CanetisRadar2
- **Author**: Maxime Bonvin
- **License**: MIT License (Copyright (c) 2021 Maxime Bonvin)

### Changes in DeafDirectionalHelper
- Renamed project for clarity
- Added 7.1 surround sound view
- Added per-speaker transparency controls
- Added accessibility documentation
- Various UI improvements and settings
