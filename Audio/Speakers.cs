using System;
using System.Diagnostics;
using System.Linq;
using DeafDirectionalHelper.Settings;
using NAudio.CoreAudioApi;

namespace DeafDirectionalHelper.Audio
{
    /// <summary>
    /// Reads audio output levels from Windows audio APIs for accessibility visualization.
    ///
    /// ACCESSIBILITY IMPLEMENTATION NOTES:
    /// ====================================
    /// This class reads audio data using Windows WASAPI (Windows Audio Session API) through
    /// the NAudio library. It specifically uses AudioMeterInformation.PeakValues which provides
    /// the current peak audio levels being OUTPUT to your speakers/headphones.
    ///
    /// DATA SOURCE:
    /// - MMDeviceEnumerator: Standard Windows API to enumerate audio devices
    /// - AudioMeterInformation.PeakValues: Reads the current volume level per channel
    /// - This is the SAME data that volume meters in Windows use
    ///
    /// WHAT THIS READS:
    /// - Audio levels from your sound card's output (what goes to your speakers)
    /// - Peak values per channel (0.0 to 1.0 representing volume level)
    /// - Standard Windows API - no game interaction required
    ///
    /// WHAT THIS DOES NOT DO:
    /// - Does NOT read from game memory or processes
    /// - Does NOT hook into any game's audio system
    /// - Does NOT intercept network packets
    /// - Does NOT require any game-specific code
    ///
    /// This is functionally equivalent to a physical VU meter or LED strip connected
    /// to your speakers - it simply visualizes what audio is being played.
    ///
    /// Channel mapping for 7.1 surround:
    /// - Channel 0: Front Left
    /// - Channel 1: Front Right
    /// - Channel 2: Center
    /// - Channel 3: LFE (Subwoofer)
    /// - Channel 4: Rear Left
    /// - Channel 5: Rear Right
    /// - Channel 6: Side Left
    /// - Channel 7: Side Right
    /// </summary>
    public sealed class Speakers
    {
        private const long RecoveryIntervalMs = 2000; // Try to recover every 2 seconds

        private MMDevice? _device;
        private readonly MMDeviceEnumerator _enumerator;
        private readonly Stopwatch _recoveryTimer = new();
        private bool _deviceLost;

        public readonly Speaker Speaker1 = new Speaker();
        public readonly Speaker Speaker2 = new Speaker();
        public readonly Speaker Speaker3 = new Speaker();
        public readonly Speaker Speaker4 = new Speaker();
        public readonly Speaker Speaker5 = new Speaker();
        public readonly Speaker Speaker6 = new Speaker();
        public readonly Speaker Speaker7 = new Speaker();
        public readonly Speaker Speaker8 = new Speaker();

        public string CurrentDeviceName => _device?.FriendlyName ?? "None";
        public int CurrentChannelCount => _device?.AudioMeterInformation.PeakValues.Count ?? 0;

        public Speakers()
        {
            _enumerator = new MMDeviceEnumerator();
            SelectDevice();

            // Subscribe to settings changes to switch devices
            SettingsManager.Instance.SettingsChanged += (_, _) => SelectDevice();
        }

        public void SelectDevice()
        {
            // List all audio devices for diagnostics
            Console.WriteLine("=== Available Audio Devices ===");
            var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var d in devices)
            {
                Console.WriteLine($"  {d.FriendlyName} - Channels: {d.AudioMeterInformation.PeakValues.Count}");
            }
            Console.WriteLine("===============================");

            var settings = SettingsManager.Instance.Settings;
            var preferredDevice = settings.General.AudioDevice;

            MMDevice? selectedDevice = null;

            // If a specific device is configured, try to find it
            if (!string.IsNullOrEmpty(preferredDevice))
            {
                selectedDevice = devices.FirstOrDefault(d => d.FriendlyName.Contains(preferredDevice));
                if (selectedDevice != null)
                {
                    Console.WriteLine($"Using configured device: {selectedDevice.FriendlyName} ({selectedDevice.AudioMeterInformation.PeakValues.Count} channels)");
                }
            }

            // Fall back to auto-detection if no device configured or not found
            if (selectedDevice == null)
            {
                // Try to find any device with 8 channels
                selectedDevice = devices.FirstOrDefault(d => d.AudioMeterInformation.PeakValues.Count == 8);

                if (selectedDevice != null)
                {
                    Console.WriteLine($"Auto-selected 8-channel device: {selectedDevice.FriendlyName}");
                }
                else
                {
                    // Last resort: use default device
                    selectedDevice = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
                    Console.WriteLine($"No 8-channel device found. Using default: {selectedDevice.FriendlyName} ({selectedDevice.AudioMeterInformation.PeakValues.Count} channels)");
                }
            }

            _device = selectedDevice;

            if (_deviceLost && _device != null)
            {
                _deviceLost = false;
                Console.WriteLine("Audio device recovered.");
            }
        }

        private void TryRecoverDevice()
        {
            // Throttle recovery attempts
            if (_recoveryTimer.IsRunning && _recoveryTimer.ElapsedMilliseconds < RecoveryIntervalMs)
                return;

            _recoveryTimer.Restart();

            if (!_deviceLost)
            {
                _deviceLost = true;
                Console.WriteLine("Audio device lost. Attempting recovery...");
            }

            try
            {
                SelectDevice();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Device recovery failed: {ex.Message}");
            }
        }

        public void Update()
        {
            if (_device == null)
            {
                TryRecoverDevice();
                return;
            }

            AudioMeterInformationChannels peakValues;
            int channelCount;

            try
            {
                peakValues = _device.AudioMeterInformation.PeakValues;
                channelCount = peakValues.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Audio device error: {ex.Message}");
                _device = null!;
                TryRecoverDevice();
                return;
            }

            float[] rawValues;

            if (channelCount >= 8)
            {
                // Full 7.1 surround
                Speaker1.Value = peakValues[0]; // Front Left
                Speaker2.Value = peakValues[1]; // Front Right
                Speaker3.Value = peakValues[2]; // Center
                Speaker4.Value = peakValues[3]; // LFE
                Speaker5.Value = peakValues[4]; // Rear Left
                Speaker6.Value = peakValues[5]; // Rear Right
                Speaker7.Value = peakValues[6]; // Side Left
                Speaker8.Value = peakValues[7]; // Side Right

                rawValues = new[] {
                    peakValues[0], peakValues[1], peakValues[2], peakValues[3],
                    peakValues[4], peakValues[5], peakValues[6], peakValues[7]
                };
            }
            else if (channelCount >= 2)
            {
                // Stereo - duplicate left/right to all positions
                var left = peakValues[0];
                var right = peakValues[1];

                Speaker1.Value = left;   // Front Left
                Speaker2.Value = right;  // Front Right
                Speaker3.Value = Math.Max(left, right); // Center (mix)
                Speaker4.Value = Math.Max(left, right); // LFE (mix)
                Speaker5.Value = left;   // Rear Left
                Speaker6.Value = right;  // Rear Right
                Speaker7.Value = left;   // Side Left
                Speaker8.Value = right;  // Side Right

                rawValues = new[] { left, right, Math.Max(left, right), Math.Max(left, right), left, right, left, right };
            }
            else if (channelCount == 1)
            {
                // Mono - same value everywhere
                var mono = peakValues[0];
                Speaker1.Value = mono;
                Speaker2.Value = mono;
                Speaker3.Value = mono;
                Speaker4.Value = mono;
                Speaker5.Value = mono;
                Speaker6.Value = mono;
                Speaker7.Value = mono;
                Speaker8.Value = mono;

                rawValues = new[] { mono, mono, mono, mono, mono, mono, mono, mono };
            }
            else
            {
                return;
            }

            // Log the audio event
            LogAudioEvent(rawValues);
        }

        private void LogAudioEvent(float[] rawValues)
        {
            var settings = SettingsManager.Instance.Settings.Bars;
            var logger = AudioEventLogger.Instance;
            logger.TriggerThreshold = (float)settings.MinThreshold;

            // Calculate adjusted values
            var adjustedValues = new float[8];
            for (int i = 0; i < 8; i++)
            {
                if (rawValues[i] < settings.MinThreshold)
                    adjustedValues[i] = 0f;
                else
                    adjustedValues[i] = Math.Min(1f, rawValues[i] * (float)settings.Sensitivity);
            }

            // Calculate left/right activity (excluding center channel)
            var leftActivity = Math.Max(Math.Max(adjustedValues[0], adjustedValues[4]), adjustedValues[6]);
            var rightActivity = Math.Max(Math.Max(adjustedValues[1], adjustedValues[5]), adjustedValues[7]);

            logger.LogSpeakerUpdate(rawValues, adjustedValues, leftActivity, rightActivity);
        }
    }
}