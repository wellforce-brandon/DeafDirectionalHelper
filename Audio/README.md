# Audio Module - Technical Documentation

## Overview

This module reads audio output levels from Windows for accessibility visualization. It does **NOT** interact with any game or application directly.

## How Audio Reading Works

### API Used: Windows WASAPI

We use the standard Windows Audio Session API (WASAPI) through the NAudio library:

```csharp
// Standard Windows API to enumerate audio devices
var enumerator = new MMDeviceEnumerator();

// Get audio output device
var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);

// Read peak audio levels - same data Windows volume meters use
var peakValues = device.AudioMeterInformation.PeakValues;
```

### Data Flow

```
Game/Application
      |
      v
Windows Audio Engine
      |
      v
Sound Card Driver
      |
      v
AudioMeterInformation.PeakValues  <-- We read here
      |
      v
Speakers/Headphones
```

We read at the **output stage** - after all audio processing is complete, just before it reaches your speakers.

## Files in This Module

### Speakers.cs

Core audio reading class. Key points:
- Uses `MMDeviceEnumerator` to find audio devices
- Reads `AudioMeterInformation.PeakValues` for volume levels
- Supports 7.1 surround (8 channels), stereo (2 channels), and mono
- Updates values that the UI binds to for visualization

### Speaker.cs

Simple data class holding a single speaker's current volume level with property change notification.

### AudioEventLogger.cs

Optional logging for debugging audio activity. Logs to local app data folder, not transmitted anywhere.

### ColorGradient.cs

Utility for mapping volume levels (0.0-1.0) to colors (white -> yellow -> red).

## Channel Mapping (7.1 Surround)

| Channel | Position | Speaker Property |
|---------|----------|------------------|
| 0 | Front Left | Speaker1 |
| 1 | Front Right | Speaker2 |
| 2 | Center | Speaker3 |
| 3 | LFE/Subwoofer | Speaker4 |
| 4 | Rear Left | Speaker5 |
| 5 | Rear Right | Speaker6 |
| 6 | Side Left | Speaker7 |
| 7 | Side Right | Speaker8 |

## What This Code Does NOT Do

- Does NOT open or read any game process memory
- Does NOT use any game-specific APIs or hooks
- Does NOT require elevated permissions
- Does NOT inject DLLs or code into other processes
- Does NOT intercept or modify audio streams
- Does NOT require knowing what application is producing audio

## Verification

You can verify our audio reading approach is legitimate by:

1. Checking the NAudio library source: https://github.com/naudio/NAudio
2. Reviewing Windows WASAPI documentation: https://docs.microsoft.com/en-us/windows/win32/coreaudio/wasapi
3. Noting this is the same API used by:
   - Windows volume mixer
   - Audio visualizers
   - VU meter applications
   - LED sync software (Razer, Corsair, etc.)

## Dependencies

- **NAudio** (MIT License): .NET audio library providing WASAPI access
  - Source: https://github.com/naudio/NAudio
  - NuGet: https://www.nuget.org/packages/NAudio
