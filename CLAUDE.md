# CanetisRadar2 - Claude Code Configuration

## Project Overview

**Project Name**: CanetisRadar2
**Description**: Audio visualization tool for deaf gamers - displays 7.1 surround sound channel levels as colored bars to indicate sound direction
**Tech Stack**: C# / .NET 8.0 / WPF / NAudio

## Tech Stack

- **Language**: C# 12 (.NET 8.0)
- **Framework**: WPF (Windows Presentation Foundation)
- **Audio Library**: NAudio 2.2.1
- **Target Platform**: Windows (net8.0-windows)

## Development Principles

### Non-Negotiable Rules

1. **Simple > Complex** - This is a focused utility app, keep it simple
2. **YAGNI until you need it** - Don't build for hypothetical futures
3. **Rule of Three** - Wait for 3rd duplicate before extracting
4. **Working > Perfect** - Ship working code, iterate based on use

### Preferred Patterns

- Direct code with clear intent
- Simple event handling and data binding
- Environment/config for runtime values
- Error handling on external calls (audio devices)

### Patterns to Avoid

- Factory patterns, DI frameworks, abstract base classes
- Service layers with repositories (direct code is fine)
- Over-modularization (this is a small app)

## Project Structure

```
CanetisRadar2/
├── Audio/                    # Audio capture and processing
│   ├── Speaker.cs            # Individual channel with value retention
│   └── Speakers.cs           # 7.1 channel management via NAudio
├── View/                     # WPF views
│   ├── ColoredSpeakers.cs    # Data binding (audio -> colors)
│   ├── LeftScreen.xaml       # Left edge display
│   └── RightScreen.xaml      # Right edge display
├── ColorGradient.cs          # Color interpolation
├── MainWindow.xaml.cs        # App coordinator
├── App.xaml                  # WPF entry point
├── .claude/                  # Claude Code configuration
│   ├── commands/             # Custom slash commands
│   ├── skills/               # Development guidelines
│   └── hooks/                # Automation scripts
└── CLAUDE.md                 # This file
```

## Custom Commands

Use these slash commands for common workflows:

- `/build-and-fix` - Run build and fix all C#/compilation errors
- `/code-review` - Review code for WPF/C# best practices
- `/dev-docs [task]` - Create implementation plan for complex features
- `/dev-docs-update` - Update docs before context compaction

## Audio Device Configuration

The app requires an **8-channel (7.1 surround) audio device**. Currently configured to use:
- **Primary**: `CABLE-C Input (VB-Audio Voicemeeter VAIO)` - 8 channels
- **Fallback**: Any device with 8 channels

If no 8-channel device found, the app will fail with a descriptive error.

## Building and Running

```bash
# Build
dotnet build

# Run
dotnet run

# Build release
dotnet build -c Release
```

## Key Implementation Details

### Screen Layout
- Two narrow bars (50px wide) on left and right edges of primary screen
- Each bar has 3 sections: Top (front), Center (side), Bottom (back)
- Colors indicate volume: White (silent) -> Yellow -> Red (loud)

### Audio Processing
- Polls audio device every 200ms
- Speaker class holds peak value for 1 second (retention for readability)
- ColoredSpeakers maps speaker values to WPF Brush properties

### Threading
- Audio polling runs on background thread
- UI updates via Dispatcher.BeginInvoke

## Development Workflow

### Before Starting a Task
1. Ask: "What's the simplest way to make this work?"
2. For complex changes, use `/dev-docs` to plan

### During Implementation
1. Keep it simple - this is a focused utility
2. Test manually with audio playing
3. Verify on single-monitor setup

### Before Finishing
1. Run `dotnet build` to verify no errors
2. Test the app manually
3. Check that bars appear correctly on screen edges

## Mantras

- "Simple + Working beats Complex + Perfect"
- "Ship it if it works"
- "This is a personal tool, not enterprise software"

---

**Remember**: This is a focused accessibility tool. Keep changes minimal and purposeful.
