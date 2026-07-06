# DeafDirectionalHelper - Accessibility Statement

## Purpose

DeafDirectionalHelper is an **accessibility tool** designed to help deaf and hard-of-hearing gamers perceive directional audio cues that hearing players naturally receive through their ears.

## The Problem We Solve

As described by the original creator:

> "As a 100% deaf from birth person, I can now hear artificially from one ear thanks to technology. And as a gamer, I can **hear** a sound, I can **recognize** it but I **cannot locate** it."

Hearing players can instantly perceive:
- Which direction footsteps are coming from
- Where gunshots originated
- The location of vehicles or environmental sounds

Deaf and hard-of-hearing players miss these critical audio cues, putting them at a significant disadvantage - not because of skill, but because of a disability.

## How It Works

### Technical Implementation

This application uses **standard Windows audio APIs** to visualize audio output:

1. **Windows WASAPI** (Windows Audio Session API) via the NAudio library
2. **MMDeviceEnumerator** - Standard Windows API to list audio devices
3. **AudioMeterInformation.PeakValues** - Reads current volume levels per speaker channel

### What We Read

- Audio levels from your sound card's **output** (what goes to your speakers/headphones)
- Peak values per channel (0.0 to 1.0 representing volume level)
- The same data that Windows volume meters display

### Audio Session Awareness

To find game audio automatically ("Follow the game" capture mode) and to offer
per-game profiles, the app also uses the Windows **audio session** APIs
(`IAudioSessionManager2` / `IAudioSessionControl2`). These expose exactly two
things per session: **which process owns it** (its process ID) and its **output
meter level** - the same information the Windows Volume Mixer shows next to
each app. This is still entirely output-side: nothing reads game memory,
injects code, or hooks any process. Game detection additionally looks only at
public window information (is the window foreground and near-fullscreen).

### What We Do NOT Do

| Action | This Tool |
|--------|-----------|
| Read game memory | NO |
| Modify game files | NO |
| Inject code into games | NO |
| Intercept network traffic | NO |
| Hook into game processes | NO |
| Bypass any protections | NO |
| Provide aim assist | NO |
| Automate any inputs | NO |

## Accessibility Parity, Not Advantage

This tool provides **accessibility parity** - it visualizes information that hearing players already perceive naturally.

Think of it like:
- A **flashing doorbell light** for deaf individuals
- **Closed captions** for dialogue
- **Bass shakers** that convert audio to vibration
- **LED strips** that react to music

We're not providing extra information - we're converting audio information to visual information so deaf players can perceive what hearing players already do.

## Technical Architecture

```
[Game Audio] --> [Windows Audio Mixer] --> [Sound Card Output]
                                                   |
                                                   v
                                     [Windows WASAPI Peak Meters]
                                                   |
                                                   v
                                     [DeafDirectionalHelper reads levels]
                                                   |
                                                   v
                                     [Visual overlay displayed]
```

The application sits at the end of the audio chain, reading output levels the same way any audio visualizer or VU meter would.

## Relevant Code Files

- `Audio/Speakers.cs` - Core audio reading implementation with detailed documentation
- `Audio/SessionLocator.cs` - Maps audio sessions to owning processes (Volume-Mixer-level data only)
- `Audio/EndpointSelector.cs` - Chooses which output device to read per capture mode
- `App.xaml.cs` - Application entry point with accessibility notice
- `View/Overlays/` - Converts audio levels to the visual overlays

## Comparison to Built-in Accessibility Features

Many games are now adding similar visual audio indicators:
- Fortnite's "Visualize Sound Effects" option
- Apex Legends' threat indicators
- Various games' accessibility settings for deaf players

DeafDirectionalHelper provides this functionality externally for games that don't have built-in accessibility features.

## Contact

If you have questions about this accessibility tool or would like to verify its implementation, please:
1. Review the source code in this repository
2. Open an issue on GitHub
3. Contact the maintainers

## License

This project is licensed under the MIT License - see the LICENSE file for details.
