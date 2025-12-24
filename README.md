# DeafDirectionalHelper

An accessibility tool for deaf and hard-of-hearing gamers to visualize directional audio cues.

> **Accessibility Statement**: This tool reads audio OUTPUT from your Windows sound card using standard Windows APIs. It does NOT interact with, read from, or modify any game. See [ACCESSIBILITY.md](ACCESSIBILITY.md) for full details.

## The Problem

As a 100% deaf from birth person, I can now hear artificially from one ear thanks to technology. And as a gamer, I can **hear** a sound, I can **recognize** it but I **cannot locate** it.

At first as a casual gamer I was not really bothered by this fact, but over time I realized while watching strong players on YouTube that they could notice enemies way before I even could. The difference is that they have the audio cue while I'm only relying on the visual one.

Hence I wish to find a way to **quickly** notice from what direction the sound is.

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

### Display Modes

1. **Side Bars** - Two vertical bars (left/right) with sections for front/side/rear
2. **7.1 Surround View** - Visual representation of all 8 speaker positions
3. **Both** - Show both views simultaneously

### Visual Indicators

- White = Silent
- Yellow = Quiet sound
- Orange = Medium sound
- Red = Loud sound

### Settings

- Sensitivity and threshold adjustments
- Transparent mode (indicators appear only when sound detected)
- Per-speaker opacity in 7.1 view
- Hide LFE/subwoofer indicator option
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
| Ctrl+Shift+R | Toggle enable/disable |
| Ctrl+Shift+M | Toggle display mode |
| Ctrl+Shift+S | Show settings |
| Ctrl+Shift+P | Reset positions |

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
