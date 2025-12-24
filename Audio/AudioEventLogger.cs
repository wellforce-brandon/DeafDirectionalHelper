using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeafDirectionalHelper.Audio
{
    public class AudioEventLogger : IDisposable
    {
        private static AudioEventLogger? _instance;
        public static AudioEventLogger Instance => _instance ??= new AudioEventLogger();

        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private readonly ConcurrentQueue<AudioEvent> _eventQueue;
        private readonly CancellationTokenSource _cts;
        private readonly Task _writerTask;
        private readonly object _fileLock = new();

        private const long MaxFileSizeBytes = 500 * 1024 * 1024; // 500 MB per file
        private const int MaxLogFiles = 2; // 2 files = 1 GB max total
        private const int FlushIntervalMs = 1000;

        public bool IsEnabled { get; set; } = true;
        public float TriggerThreshold { get; set; } = 0.01f; // Only log events above this level

        private AudioEventLogger()
        {
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeafDirectionalHelper", "Logs");

            Directory.CreateDirectory(_logDirectory);

            _logFilePath = Path.Combine(_logDirectory, "audio_events.log");
            _eventQueue = new ConcurrentQueue<AudioEvent>();
            _cts = new CancellationTokenSource();

            // Check if logging is enabled in settings
            try
            {
                IsEnabled = DeafDirectionalHelper.Settings.SettingsManager.Instance.Settings.General.EnableAudioLogging;
            }
            catch
            {
                IsEnabled = false; // Default to disabled if settings not available
            }

            // Start background writer
            _writerTask = Task.Run(WriteLoop);

            // Write header if enabled
            if (IsEnabled)
                LogHeader();
        }

        private void LogHeader()
        {
            var header = new StringBuilder();
            header.AppendLine("=".PadRight(90, '='));
            header.AppendLine($"DeafDirectionalHelper Audio Event Log - Started {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            header.AppendLine();
            header.AppendLine("Format: [Time] DIRECTION COLOR L[█████] R[█████] (left/right) <- active speakers");
            header.AppendLine();
            header.AppendLine("Direction: LEFT!/LEFT/CENTER/RIGHT/RIGHT! (! = strong directional)");
            header.AppendLine("Colors:    white (silent) -> YELLOW -> ORANGE -> ORNG-RD -> RED (loud)");
            header.AppendLine("Bars:      █ = activity level (5 segments = 100%)");
            header.AppendLine("=".PadRight(90, '='));

            lock (_fileLock)
            {
                File.AppendAllText(_logFilePath, header.ToString());
            }
        }

        public void LogEvent(int channel, float rawValue, float adjustedValue, bool triggered)
        {
            if (!IsEnabled) return;
            if (rawValue < TriggerThreshold && !triggered) return; // Skip very quiet events

            _eventQueue.Enqueue(new AudioEvent
            {
                Timestamp = DateTime.Now,
                Channel = channel,
                RawValue = rawValue,
                AdjustedValue = adjustedValue,
                Triggered = triggered
            });
        }

        public void LogSpeakerUpdate(float[] rawValues, float[] adjustedValues, float leftActivity, float rightActivity)
        {
            if (!IsEnabled) return;

            // Only log if there's meaningful activity on left or right
            if (leftActivity < 0.005f && rightActivity < 0.005f) return;

            var sb = new StringBuilder();
            sb.Append($"[{DateTime.Now:HH:mm:ss.fff}] ");

            // Determine direction and show it clearly
            var direction = GetDirection(leftActivity, rightActivity);
            var dominantActivity = Math.Max(leftActivity, rightActivity);
            var colorName = GetColorName(dominantActivity);

            sb.Append($"{direction,-6} {colorName,-7} ");

            // Show the bar levels as a simple visual
            var leftBar = GetActivityBar(leftActivity);
            var rightBar = GetActivityBar(rightActivity);
            sb.Append($"L[{leftBar}] R[{rightBar}] ");

            // Show numeric values
            sb.Append($"({leftActivity:F2}/{rightActivity:F2})");

            // If there's significant activity, show which speakers triggered it
            if (dominantActivity >= 0.1f)
            {
                sb.Append(" <- ");
                var activeSpeakers = new List<string>();
                string[] speakerNames = { "FrontL", "FrontR", "Center", "LFE", "RearL", "RearR", "SideL", "SideR" };
                for (int i = 0; i < adjustedValues.Length && i < 8; i++)
                {
                    if (adjustedValues[i] >= 0.1f)
                    {
                        activeSpeakers.Add($"{speakerNames[i]}:{adjustedValues[i]:F2}");
                    }
                }
                sb.Append(string.Join(", ", activeSpeakers));
            }

            _eventQueue.Enqueue(new AudioEvent
            {
                Timestamp = DateTime.Now,
                FormattedMessage = sb.ToString()
            });
        }

        private static string GetDirection(float left, float right)
        {
            const float threshold = 0.02f; // Minimum difference to consider directional
            var diff = left - right;

            if (Math.Abs(diff) < threshold)
                return "CENTER";
            else if (diff > 0.3f)
                return "LEFT!";
            else if (diff > threshold)
                return "LEFT";
            else if (diff < -0.3f)
                return "RIGHT!";
            else
                return "RIGHT";
        }

        private static string GetColorName(float activity)
        {
            // Based on ColorGradient thresholds in ColoredSpeakers.cs
            if (activity < 0.005f)
                return "white";
            else if (activity < 0.15f)
                return "YELLOW";
            else if (activity < 0.35f)
                return "ORANGE";
            else if (activity < 0.60f)
                return "ORNG-RD";
            else
                return "RED";
        }

        private static string GetActivityBar(float activity)
        {
            // Create a simple 5-character visual bar
            int filled = (int)(activity * 5);
            filled = Math.Min(5, Math.Max(0, filled));
            return new string('█', filled) + new string('░', 5 - filled);
        }

        private static string GetChannelName(int channel) => channel switch
        {
            1 => "FL",
            2 => "FR",
            3 => "C",
            4 => "LFE",
            5 => "RL",
            6 => "RR",
            7 => "SL",
            8 => "SR",
            _ => $"Ch{channel}"
        };

        private async Task WriteLoop()
        {
            var buffer = new StringBuilder();

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(FlushIntervalMs, _cts.Token);

                    // Collect all pending events
                    while (_eventQueue.TryDequeue(out var evt))
                    {
                        if (!string.IsNullOrEmpty(evt.FormattedMessage))
                        {
                            buffer.AppendLine(evt.FormattedMessage);
                        }
                        else
                        {
                            var channelName = GetChannelName(evt.Channel);
                            var triggered = evt.Triggered ? "*TRIGGER*" : "";
                            buffer.AppendLine(
                                $"[{evt.Timestamp:HH:mm:ss.fff}] {channelName,-3} | {evt.RawValue:F4} | {evt.AdjustedValue:F4} | {triggered}");
                        }
                    }

                    if (buffer.Length > 0)
                    {
                        lock (_fileLock)
                        {
                            // Check file size and rotate if needed
                            RotateLogsIfNeeded();
                            File.AppendAllText(_logFilePath, buffer.ToString());
                        }
                        buffer.Clear();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Audio logger error: {ex.Message}");
                }
            }

            // Flush remaining events
            if (buffer.Length > 0)
            {
                lock (_fileLock)
                {
                    File.AppendAllText(_logFilePath, buffer.ToString());
                }
            }
        }

        private void RotateLogsIfNeeded()
        {
            if (!File.Exists(_logFilePath)) return;

            var fileInfo = new FileInfo(_logFilePath);
            if (fileInfo.Length < MaxFileSizeBytes) return;

            // Rotate files
            var backupPath = Path.Combine(_logDirectory, "audio_events.1.log");

            // Delete oldest backup
            if (File.Exists(backupPath))
                File.Delete(backupPath);

            // Move current to backup
            File.Move(_logFilePath, backupPath);

            // Start fresh
            LogHeader();
        }

        public string GetLogFilePath() => _logFilePath;

        public string GetLogDirectory() => _logDirectory;

        public long GetCurrentLogSize()
        {
            if (!File.Exists(_logFilePath)) return 0;
            return new FileInfo(_logFilePath).Length;
        }

        public void ClearLogs()
        {
            lock (_fileLock)
            {
                foreach (var file in Directory.GetFiles(_logDirectory, "audio_events*.log"))
                {
                    try { File.Delete(file); } catch { }
                }
                LogHeader();
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            try
            {
                _writerTask.Wait(2000);
            }
            catch { }
            _cts.Dispose();
        }

        private struct AudioEvent
        {
            public DateTime Timestamp;
            public int Channel;
            public float RawValue;
            public float AdjustedValue;
            public bool Triggered;
            public string? FormattedMessage;
        }
    }
}
