using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using DeafDirectionalHelper.Audio;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View
{
    public sealed class ColoredSpeakers : INotifyPropertyChanged
    {
        private readonly Speakers _speakers;
        private readonly SettingsManager _settingsManager;

        public ColoredSpeakers(Speakers speakers)
        {
            _speakers = speakers;
            _settingsManager = SettingsManager.Instance;

            // Left side speakers - update all left bar sections
            _speakers.Speaker1.PropertyChanged += (_, _) => NotifyLeftSideChanged();
            _speakers.Speaker5.PropertyChanged += (_, _) => NotifyLeftSideChanged();
            _speakers.Speaker7.PropertyChanged += (_, _) => NotifyLeftSideChanged();

            // Right side speakers - update all right bar sections
            _speakers.Speaker2.PropertyChanged += (_, _) => NotifyRightSideChanged();
            _speakers.Speaker6.PropertyChanged += (_, _) => NotifyRightSideChanged();
            _speakers.Speaker8.PropertyChanged += (_, _) => NotifyRightSideChanged();

            // Center and LFE only affect their own colors (for 7.1 view)
            _speakers.Speaker3.PropertyChanged += (_, _) => NotifyCenterChanged();
            _speakers.Speaker4.PropertyChanged += (_, _) => NotifyLfeChanged();

            // Subscribe to settings changes to update opacity when MaxOpacity changes
            _settingsManager.SettingsChanged += (_, _) => NotifyAllOpacityChanged();
        }

        private void NotifyAllOpacityChanged()
        {
            NotifyLeftSideChanged();
            NotifyRightSideChanged();
            NotifyCenterChanged();
            NotifyLfeChanged();
        }

        /// <summary>
        /// Applies threshold and sensitivity to an audio level
        /// </summary>
        private float ApplySettings(float value)
        {
            var settings = _settingsManager.Settings.Bars;

            // Ignore values below minimum threshold
            if (value < settings.MinThreshold)
                return 0f;

            // Apply sensitivity (lower = less sensitive, requires louder sounds)
            var adjusted = value * (float)settings.Sensitivity;

            // Clamp to 0-1 range
            return Math.Min(1f, Math.Max(0f, adjusted));
        }

        /// <summary>
        /// Combined audio activity level for left side (0.0 to 1.0)
        /// Excludes center channel to avoid both bars lighting up together
        /// </summary>
        public float LeftActivity
        {
            get
            {
                var left = ApplySettings(Math.Max(Math.Max(_speakers.Speaker1, _speakers.Speaker5), _speakers.Speaker7));
                var right = ApplySettings(Math.Max(Math.Max(_speakers.Speaker2, _speakers.Speaker6), _speakers.Speaker8));

                // If ignoring balanced sounds (player sounds), filter out when L/R are nearly equal
                if (_settingsManager.Settings.Bars.IgnoreBalancedSounds && IsBalancedSound(left, right))
                    return 0f;

                return left;
            }
        }

        /// <summary>
        /// Combined audio activity level for right side (0.0 to 1.0)
        /// Excludes center channel to avoid both bars lighting up together
        /// </summary>
        public float RightActivity
        {
            get
            {
                var left = ApplySettings(Math.Max(Math.Max(_speakers.Speaker1, _speakers.Speaker5), _speakers.Speaker7));
                var right = ApplySettings(Math.Max(Math.Max(_speakers.Speaker2, _speakers.Speaker6), _speakers.Speaker8));

                // If ignoring balanced sounds (player sounds), filter out when L/R are nearly equal
                if (_settingsManager.Settings.Bars.IgnoreBalancedSounds && IsBalancedSound(left, right))
                    return 0f;

                return right;
            }
        }

        /// <summary>
        /// Checks if a sound is "balanced" (likely a player sound like footsteps or gunshots)
        /// Balanced = both L/R are significant AND their difference is small
        /// </summary>
        private static bool IsBalancedSound(float left, float right)
        {
            const float minLevelToFilter = 0.15f; // Only filter louder sounds (below this, let them through)
            const float maxDifferenceRatio = 0.12f; // Max 12% difference to be considered "balanced"

            var dominant = Math.Max(left, right);
            if (dominant < minLevelToFilter)
                return false; // Don't filter quiet sounds

            var difference = Math.Abs(left - right);
            return difference < dominant * maxDifferenceRatio;
        }

        public Brush ColorSpeakerLeftTop =>
            _gradient.At(LeftActivity).toBrush();

        public Brush ColorSpeakerLeftCenter =>
            _gradient.At(LeftActivity).toBrush();

        public Brush ColorSpeakerLeftBottom =>
            _gradient.At(LeftActivity).toBrush();

        public Brush ColorSpeakerRightTop =>
            _gradient.At(RightActivity).toBrush();

        public Brush ColorSpeakerRightCenter =>
            _gradient.At(RightActivity).toBrush();

        public Brush ColorSpeakerRightBottom =>
            _gradient.At(RightActivity).toBrush();

        // Combined left/right colors for horizontal dual mode
        public Brush ColorLeft => _gradient.At(LeftActivity).toBrush();
        public Brush ColorRight => _gradient.At(RightActivity).toBrush();

        // Individual speaker colors for 7.1 mode
        public Brush ColorSpeaker1 => _gradient.At(ApplySettings(_speakers.Speaker1)).toBrush(); // Front Left
        public Brush ColorSpeaker2 => _gradient.At(ApplySettings(_speakers.Speaker2)).toBrush(); // Front Right
        public Brush ColorSpeaker3 => _gradient.At(ApplySettings(_speakers.Speaker3)).toBrush(); // Center
        public Brush ColorSpeaker4 => _gradient.At(ApplySettings(_speakers.Speaker4)).toBrush(); // LFE/Sub
        public Brush ColorSpeaker5 => _gradient.At(ApplySettings(_speakers.Speaker5)).toBrush(); // Rear Left
        public Brush ColorSpeaker6 => _gradient.At(ApplySettings(_speakers.Speaker6)).toBrush(); // Rear Right
        public Brush ColorSpeaker7 => _gradient.At(ApplySettings(_speakers.Speaker7)).toBrush(); // Side Left
        public Brush ColorSpeaker8 => _gradient.At(ApplySettings(_speakers.Speaker8)).toBrush(); // Side Right

        // Individual speaker opacity for transparent mode (7.1 view)
        // Returns maxOpacity if not in transparent mode, otherwise based on speaker activity
        private double GetSpeakerOpacity(float speakerValue)
        {
            var settings = _settingsManager.Settings.Bars;
            var maxOpacity = settings.MaxOpacity;

            if (!settings.TransparentMode)
                return maxOpacity;

            var level = ApplySettings(speakerValue);
            var threshold = settings.ActivationThreshold;

            // Return opacity based on activity - scale up to maxOpacity
            if (level < threshold)
                return 0.0;

            // Scale from 0.3 to maxOpacity based on activity level
            var baseOpacity = Math.Min(1.0, level * 2 + 0.3);
            return baseOpacity * maxOpacity;
        }

        // LFE opacity - returns 0 if hidden, otherwise normal opacity
        private double GetLfeOpacity()
        {
            if (_settingsManager.Settings.Bars.HideLfe)
                return 0.0;
            return GetSpeakerOpacity(_speakers.Speaker4);
        }

        // Combined left/right opacity based on activity level
        private double GetCombinedOpacity(float activityLevel)
        {
            var settings = _settingsManager.Settings.Bars;
            var maxOpacity = settings.MaxOpacity;

            if (!settings.TransparentMode)
                return maxOpacity;

            var threshold = settings.ActivationThreshold;

            if (activityLevel < threshold)
                return 0.0;

            var baseOpacity = Math.Min(1.0, activityLevel * 2 + 0.3);
            return baseOpacity * maxOpacity;
        }

        // Combined left/right opacity for horizontal dual mode
        public double OpacityLeft => GetCombinedOpacity(LeftActivity);
        public double OpacityRight => GetCombinedOpacity(RightActivity);

        public double OpacitySpeaker1 => GetSpeakerOpacity(_speakers.Speaker1);
        public double OpacitySpeaker2 => GetSpeakerOpacity(_speakers.Speaker2);
        public double OpacitySpeaker3 => GetSpeakerOpacity(_speakers.Speaker3);
        public double OpacitySpeaker4 => GetLfeOpacity();
        public double OpacitySpeaker5 => GetSpeakerOpacity(_speakers.Speaker5);
        public double OpacitySpeaker6 => GetSpeakerOpacity(_speakers.Speaker6);
        public double OpacitySpeaker7 => GetSpeakerOpacity(_speakers.Speaker7);
        public double OpacitySpeaker8 => GetSpeakerOpacity(_speakers.Speaker8);

        // Opacity for left bar sections (combines relevant speakers)
        public double OpacityLeftTop => GetSpeakerOpacity(_speakers.Speaker1);      // Front Left
        public double OpacityLeftCenter => GetSpeakerOpacity(_speakers.Speaker7);   // Side Left
        public double OpacityLeftBottom => GetSpeakerOpacity(_speakers.Speaker5);   // Rear Left

        // Opacity for right bar sections (combines relevant speakers)
        public double OpacityRightTop => GetSpeakerOpacity(_speakers.Speaker2);     // Front Right
        public double OpacityRightCenter => GetSpeakerOpacity(_speakers.Speaker8);  // Side Right
        public double OpacityRightBottom => GetSpeakerOpacity(_speakers.Speaker6);  // Rear Right

        private readonly ColorGradient _gradient = new(new[]
        {
            new ColorGradient.ColorRange(0, 0.005f, Colors.White, Colors.White),
            new ColorGradient.ColorRange(0.005f, 0.60f, Colors.Yellow, Colors.Red),
            new ColorGradient.ColorRange(0.60f, 1, Colors.Red, Colors.Red)
        });

        public event PropertyChangedEventHandler? PropertyChanged;

        private void NotifyLeftSideChanged()
        {
            OnPropertyChanged(nameof(LeftActivity));
            OnPropertyChanged(nameof(ColorSpeakerLeftTop));
            OnPropertyChanged(nameof(ColorSpeakerLeftCenter));
            OnPropertyChanged(nameof(ColorSpeakerLeftBottom));
            OnPropertyChanged(nameof(ColorLeft));
            OnPropertyChanged(nameof(ColorSpeaker1));
            OnPropertyChanged(nameof(ColorSpeaker5));
            OnPropertyChanged(nameof(ColorSpeaker7));
            OnPropertyChanged(nameof(OpacitySpeaker1));
            OnPropertyChanged(nameof(OpacitySpeaker5));
            OnPropertyChanged(nameof(OpacitySpeaker7));
            OnPropertyChanged(nameof(OpacityLeftTop));
            OnPropertyChanged(nameof(OpacityLeftCenter));
            OnPropertyChanged(nameof(OpacityLeftBottom));
            OnPropertyChanged(nameof(OpacityLeft));
        }

        private void NotifyRightSideChanged()
        {
            OnPropertyChanged(nameof(RightActivity));
            OnPropertyChanged(nameof(ColorSpeakerRightTop));
            OnPropertyChanged(nameof(ColorSpeakerRightCenter));
            OnPropertyChanged(nameof(ColorSpeakerRightBottom));
            OnPropertyChanged(nameof(ColorRight));
            OnPropertyChanged(nameof(ColorSpeaker2));
            OnPropertyChanged(nameof(ColorSpeaker6));
            OnPropertyChanged(nameof(ColorSpeaker8));
            OnPropertyChanged(nameof(OpacitySpeaker2));
            OnPropertyChanged(nameof(OpacitySpeaker6));
            OnPropertyChanged(nameof(OpacitySpeaker8));
            OnPropertyChanged(nameof(OpacityRightTop));
            OnPropertyChanged(nameof(OpacityRightCenter));
            OnPropertyChanged(nameof(OpacityRightBottom));
            OnPropertyChanged(nameof(OpacityRight));
        }

        private void NotifyCenterChanged()
        {
            OnPropertyChanged(nameof(ColorSpeaker3));
            OnPropertyChanged(nameof(OpacitySpeaker3));
        }

        private void NotifyLfeChanged()
        {
            OnPropertyChanged(nameof(ColorSpeaker4));
            OnPropertyChanged(nameof(OpacitySpeaker4));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public static class ColorExtension
    {
        public static Brush toBrush(this Color c)
        {
            return new SolidColorBrush {Color = c};
        }
    }
}