using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DeafDirectionalHelper.Helpers;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View
{
    public partial class HorizontalDualView : Window
    {
        private readonly SettingsManager _settingsManager;
        private readonly ColoredSpeakers _speakers;
        private bool _isDraggingLeft;
        private bool _isDraggingRight;
        private Point _dragStartPoint;
        private double _dragStartIndicatorX;
        private bool _isActive;
        private bool _allowDragging;

        private const int BarSize = 80;
        private const int BarY = 10;

        public event EventHandler? IndicatorPositionChanged;

        public bool AllowDragging
        {
            get => _allowDragging;
            set
            {
                _allowDragging = value;
                SetClickThrough(!value);
            }
        }

        public HorizontalDualView(ColoredSpeakers speakers)
        {
            InitializeComponent();

            _settingsManager = SettingsManager.Instance;
            _speakers = speakers;
            DataContext = speakers;
            Topmost = true;

            // Subscribe to settings changes to update layout
            _settingsManager.SettingsChanged += OnSettingsChanged;

            // Subscribe to audio activity changes for transparent mode
            speakers.PropertyChanged += OnSpeakersPropertyChanged;

            // Apply initial layout
            ApplyLayout();
            ApplyTransparentMode();

            // Start as click-through (non-interactable) unless dragging is already enabled
            Loaded += (_, _) => SetClickThrough(!_allowDragging);
        }

        private void SetClickThrough(bool clickThrough) =>
            WindowHelper.SetClickThrough(this, clickThrough);

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            ApplyLayout();
            ApplyTransparentMode();
        }

        private void OnSpeakersPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ColoredSpeakers.LeftActivity) &&
                e.PropertyName != nameof(ColoredSpeakers.RightActivity)) return;

            var settings = _settingsManager.Settings;
            if (!settings.Bars.TransparentMode) return;

            var isActive = _speakers.LeftActivity >= settings.Bars.ActivationThreshold ||
                          _speakers.RightActivity >= settings.Bars.ActivationThreshold;

            if (isActive != _isActive)
            {
                _isActive = isActive;
                AnimateOpacity(isActive ? settings.Bars.Opacity : 0,
                              isActive ? settings.Bars.FadeInMs : settings.Bars.FadeOutMs);
            }
        }

        private void ApplyTransparentMode()
        {
            // Window is always fully visible - element-level opacity handles max opacity and transparent mode
            Opacity = 1;
        }

        private void AnimateOpacity(double targetOpacity, int durationMs) =>
            WindowHelper.AnimateOpacity(this, targetOpacity, durationMs);

        /// <summary>
        /// Applies the horizontal dual layout - Two indicators positioned by percent settings
        /// </summary>
        public void ApplyLayout()
        {
            var settings = _settingsManager.Settings;
            var screenWidth = MainWindow.ScreenWidth;

            // Window spans the full screen width at the bottom
            Width = screenWidth;
            Height = 100;
            Left = MainWindow.ScreenLeft;
            Top = MainWindow.ScreenTop + MainWindow.ScreenHeight - Height - 50;

            // Position indicators based on percent settings
            var leftX = screenWidth * settings.Bars.LeftIndicatorPercent - BarSize / 2;
            var rightX = screenWidth * settings.Bars.RightIndicatorPercent - BarSize / 2;

            var labelY = BarY + 32; // Center label vertically in 80px bar

            // Left indicator
            Canvas.SetLeft(LeftBar, leftX);
            Canvas.SetTop(LeftBar, BarY);
            Canvas.SetLeft(LeftLabel, leftX + 32);
            Canvas.SetTop(LeftLabel, labelY);

            // Right indicator
            Canvas.SetLeft(RightBar, rightX);
            Canvas.SetTop(RightBar, BarY);
            Canvas.SetLeft(RightLabel, rightX + 32);
            Canvas.SetTop(RightLabel, labelY);
        }

        /// <summary>
        /// Recenters the view (resets indicator positions to defaults)
        /// </summary>
        public void Recenter()
        {
            _settingsManager.Update(s =>
            {
                s.Bars.LeftIndicatorPercent = 0.35;
                s.Bars.RightIndicatorPercent = 0.65;
            });
            ApplyLayout();
            IndicatorPositionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!AllowDragging) return;

            var clickPoint = e.GetPosition(this);
            var settings = _settingsManager.Settings;
            var screenWidth = MainWindow.ScreenWidth;

            // Calculate indicator positions
            var leftX = screenWidth * settings.Bars.LeftIndicatorPercent - BarSize / 2;
            var rightX = screenWidth * settings.Bars.RightIndicatorPercent - BarSize / 2;

            // Check if clicking on left indicator
            if (clickPoint.X >= leftX && clickPoint.X <= leftX + BarSize &&
                clickPoint.Y >= BarY && clickPoint.Y <= BarY + BarSize)
            {
                _isDraggingLeft = true;
                _dragStartPoint = clickPoint;
                _dragStartIndicatorX = leftX;
                CaptureMouse();
                Cursor = Cursors.SizeWE;
                return;
            }

            // Check if clicking on right indicator
            if (clickPoint.X >= rightX && clickPoint.X <= rightX + BarSize &&
                clickPoint.Y >= BarY && clickPoint.Y <= BarY + BarSize)
            {
                _isDraggingRight = true;
                _dragStartPoint = clickPoint;
                _dragStartIndicatorX = rightX;
                CaptureMouse();
                Cursor = Cursors.SizeWE;
                return;
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingLeft && !_isDraggingRight) return;

            var currentPoint = e.GetPosition(this);
            var deltaX = currentPoint.X - _dragStartPoint.X;
            var screenWidth = MainWindow.ScreenWidth;
            var settings = _settingsManager.Settings;
            var isLinked = settings.Bars.Locked;

            if (_isDraggingLeft)
            {
                var newX = _dragStartIndicatorX + deltaX;
                // Clamp to left half of screen
                newX = Math.Max(0, Math.Min(newX, screenWidth / 2 - BarSize));

                // Move left indicator
                Canvas.SetLeft(LeftBar, newX);
                Canvas.SetLeft(LeftLabel, newX + 32);

                // If linked, move right indicator symmetrically
                if (isLinked)
                {
                    var rightNewX = screenWidth - BarSize - newX;
                    rightNewX = Math.Max(screenWidth / 2, Math.Min(rightNewX, screenWidth - BarSize));
                    Canvas.SetLeft(RightBar, rightNewX);
                    Canvas.SetLeft(RightLabel, rightNewX + 32);
                }
            }
            else if (_isDraggingRight)
            {
                var newX = _dragStartIndicatorX + deltaX;
                // Clamp to right half of screen
                newX = Math.Max(screenWidth / 2, Math.Min(newX, screenWidth - BarSize));

                // Move right indicator
                Canvas.SetLeft(RightBar, newX);
                Canvas.SetLeft(RightLabel, newX + 32);

                // If linked, move left indicator symmetrically
                if (isLinked)
                {
                    var leftNewX = screenWidth - BarSize - newX;
                    leftNewX = Math.Max(0, Math.Min(leftNewX, screenWidth / 2 - BarSize));
                    Canvas.SetLeft(LeftBar, leftNewX);
                    Canvas.SetLeft(LeftLabel, leftNewX + 32);
                }
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingLeft && !_isDraggingRight) return;

            var screenWidth = MainWindow.ScreenWidth;
            var settings = _settingsManager.Settings;
            var isLinked = settings.Bars.Locked;

            if (_isDraggingLeft)
            {
                // Calculate final percent from current Canvas position
                var leftX = Canvas.GetLeft(LeftBar);
                var newPercent = (leftX + BarSize / 2) / screenWidth;
                newPercent = Math.Max(0.05, Math.Min(0.45, newPercent));

                if (isLinked)
                {
                    var rightPercent = 1.0 - newPercent;
                    _settingsManager.Update(s =>
                    {
                        s.Bars.LeftIndicatorPercent = newPercent;
                        s.Bars.RightIndicatorPercent = rightPercent;
                    });
                }
                else
                {
                    _settingsManager.Update(s => s.Bars.LeftIndicatorPercent = newPercent);
                }
            }
            else if (_isDraggingRight)
            {
                // Calculate final percent from current Canvas position
                var rightX = Canvas.GetLeft(RightBar);
                var newPercent = (rightX + BarSize / 2) / screenWidth;
                newPercent = Math.Max(0.55, Math.Min(0.95, newPercent));

                if (isLinked)
                {
                    var leftPercent = 1.0 - newPercent;
                    _settingsManager.Update(s =>
                    {
                        s.Bars.LeftIndicatorPercent = leftPercent;
                        s.Bars.RightIndicatorPercent = newPercent;
                    });
                }
                else
                {
                    _settingsManager.Update(s => s.Bars.RightIndicatorPercent = newPercent);
                }
            }

            _isDraggingLeft = false;
            _isDraggingRight = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;

            // Notify that position changed (for updating settings window sliders)
            IndicatorPositionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
