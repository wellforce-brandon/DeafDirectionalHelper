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
        public readonly Speaker Speaker1 = new Speaker();
        public readonly Speaker Speaker2 = new Speaker();
        public readonly Speaker Speaker3 = new Speaker();
        public readonly Speaker Speaker4 = new Speaker();
        public readonly Speaker Speaker5 = new Speaker();
        public readonly Speaker Speaker6 = new Speaker();
        public readonly Speaker Speaker7 = new Speaker();
        public readonly Speaker Speaker8 = new Speaker();

        /// <summary>Session-to-process mapping across all render endpoints (2 s poll).</summary>
        public SessionLocator Sessions { get; }

        /// <summary>Owns which endpoint is read, per the configured CaptureMode.</summary>
        public EndpointSelector Endpoint { get; }

        /// <summary>Max raw channel peak from the last successful Update; feeds SignalDoctor.</summary>
        public float LastRawPeak { get; private set; }

        public string CurrentDeviceName => Endpoint.CurrentDeviceName;
        public int CurrentChannelCount => Endpoint.Current?.AudioMeterInformation.PeakValues.Count ?? 0;

        public Speakers()
        {
            // List all audio devices once for diagnostics
            Console.WriteLine("=== Available Audio Devices ===");
            using (var enumerator = new MMDeviceEnumerator())
            {
                foreach (var d in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                {
                    Console.WriteLine($"  {d.FriendlyName} - Channels: {d.AudioMeterInformation.PeakValues.Count}");
                }
            }
            Console.WriteLine("===============================");

            Sessions = new SessionLocator();
            Endpoint = new EndpointSelector(Sessions);
            Endpoint.EnsureSelected();
        }

        public void Update()
        {
            Endpoint.EnsureSelected();

            var device = Endpoint.Current;
            if (device == null)
                return;

            AudioMeterInformationChannels peakValues;
            int channelCount;

            try
            {
                peakValues = device.AudioMeterInformation.PeakValues;
                channelCount = peakValues.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Audio device error: {ex.Message}");
                Endpoint.MarkDeviceLost();
                return;
            }

            float[] rawValues;

            if (channelCount >= 8)
            {
                // Full 7.1 surround. NAudio's channel indexer makes a COM
                // round-trip per access, so read each channel exactly once.
                rawValues = new float[8];
                for (int i = 0; i < 8; i++)
                    rawValues[i] = peakValues[i];

                Speaker1.Value = rawValues[0]; // Front Left
                Speaker2.Value = rawValues[1]; // Front Right
                Speaker3.Value = rawValues[2]; // Center
                Speaker4.Value = rawValues[3]; // LFE
                Speaker5.Value = rawValues[4]; // Rear Left
                Speaker6.Value = rawValues[5]; // Rear Right
                Speaker7.Value = rawValues[6]; // Side Left
                Speaker8.Value = rawValues[7]; // Side Right
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

            LastRawPeak = rawValues.Max();

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