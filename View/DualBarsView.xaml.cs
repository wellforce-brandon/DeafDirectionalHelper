using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DeafDirectionalHelper.Helpers;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View
{
    public partial class DualBarsView : Window
    {
        private readonly SettingsManager _settingsManager;
        private readonly ColoredSpeakers _speakers;

        private bool _isDraggingLeft;
        private bool _isDraggingRight;
        private Point _dragStartPoint;
        private double _dragStartBarX;

        private bool _isActive;
        private bool _allowDragging;

        public event EventHandler? PositionChanged;

        public bool AllowDragging
        {
            get => _allowDragging;
            set
            {
                _allowDragging = value;
                SetClickThrough(!value);
            }
        }

        public DualBarsView(ColoredSpeakers speakers)
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
        /// Applies the layout - positions both bars based on percent settings
        /// </summary>
        public void ApplyLayout()
        {
            var settings = _settingsManager.Settings;
            var screenWidth = MainWindow.ScreenWidth;
            var screenHeight = MainWindow.ScreenHeight;
            var barWidth = MainWindow.AppWidth;

            // Window spans the full screen
            Width = screenWidth;
            Height = screenHeight;
            Left = MainWindow.ScreenLeft;
            Top = MainWindow.ScreenTop;

            // Position left bar based on percent setting
            var leftCenterX = screenWidth * settings.Bars.LeftIndicatorPercent;
            var leftBarX = leftCenterX - barWidth / 2;
            // Clamp to left half
            leftBarX = Math.Max(0, Math.Min(leftBarX, screenWidth / 2 - barWidth));

            Canvas.SetLeft(LeftBar, leftBarX);
            Canvas.SetTop(LeftBar, 0);
            LeftBar.Width = barWidth;
            LeftBar.Height = screenHeight;

            // Position right bar based on percent setting
            var rightCenterX = screenWidth * settings.Bars.RightIndicatorPercent;
            var rightBarX = rightCenterX - barWidth / 2;
            // Clamp to right half
            rightBarX = Math.Max(screenWidth / 2, Math.Min(rightBarX, screenWidth - barWidth));

            Canvas.SetLeft(RightBar, rightBarX);
            Canvas.SetTop(RightBar, 0);
            RightBar.Width = barWidth;
            RightBar.Height = screenHeight;
        }

        /// <summary>
        /// Recenters the bars (resets positions to defaults)
        /// </summary>
        public void Recenter()
        {
            _settingsManager.Update(s =>
            {
                s.Bars.LeftIndicatorPercent = 0.35;
                s.Bars.RightIndicatorPercent = 0.65;
            });
            ApplyLayout();
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!AllowDragging) return;

            var clickPoint = e.GetPosition(MainCanvas);
            var barWidth = MainWindow.AppWidth;

            // Get current bar positions
            var leftBarX = Canvas.GetLeft(LeftBar);
            var rightBarX = Canvas.GetLeft(RightBar);
            var screenHeight = MainWindow.ScreenHeight;

            // Check if clicking on left bar
            if (clickPoint.X >= leftBarX && clickPoint.X <= leftBarX + barWidth &&
                clickPoint.Y >= 0 && clickPoint.Y <= screenHeight)
            {
                _isDraggingLeft = true;
                _dragStartPoint = clickPoint;
                _dragStartBarX = leftBarX;
                CaptureMouse();
                Cursor = Cursors.SizeWE;
                return;
            }

            // Check if clicking on right bar
            if (clickPoint.X >= rightBarX && clickPoint.X <= rightBarX + barWidth &&
                clickPoint.Y >= 0 && clickPoint.Y <= screenHeight)
            {
                _isDraggingRight = true;
                _dragStartPoint = clickPoint;
                _dragStartBarX = rightBarX;
                CaptureMouse();
                Cursor = Cursors.SizeWE;
                return;
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingLeft && !_isDraggingRight) return;

            var currentPoint = e.GetPosition(MainCanvas);
            var deltaX = currentPoint.X - _dragStartPoint.X;
            var screenWidth = MainWindow.ScreenWidth;
            var barWidth = MainWindow.AppWidth;
            var settings = _settingsManager.Settings;
            var isLinked = settings.Bars.Locked;

            if (_isDraggingLeft)
            {
                var newX = _dragStartBarX + deltaX;
                // Clamp to left half of screen
                newX = Math.Max(0, Math.Min(newX, screenWidth / 2 - barWidth));

                // Move left bar
                Canvas.SetLeft(LeftBar, newX);

                // If linked, move right bar symmetrically
                if (isLinked)
                {
                    var rightNewX = screenWidth - barWidth - newX;
                    rightNewX = Math.Max(screenWidth / 2, Math.Min(rightNewX, screenWidth - barWidth));
                    Canvas.SetLeft(RightBar, rightNewX);
                }
            }
            else if (_isDraggingRight)
            {
                var newX = _dragStartBarX + deltaX;
                // Clamp to right half of screen
                newX = Math.Max(screenWidth / 2, Math.Min(newX, screenWidth - barWidth));

                // Move right bar
                Canvas.SetLeft(RightBar, newX);

                // If linked, move left bar symmetrically
                if (isLinked)
                {
                    var leftNewX = screenWidth - barWidth - newX;
                    leftNewX = Math.Max(0, Math.Min(leftNewX, screenWidth / 2 - barWidth));
                    Canvas.SetLeft(LeftBar, leftNewX);
                }
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingLeft && !_isDraggingRight) return;

            var screenWidth = MainWindow.ScreenWidth;
            var barWidth = MainWindow.AppWidth;
            var settings = _settingsManager.Settings;
            var isLinked = settings.Bars.Locked;

            if (_isDraggingLeft)
            {
                // Calculate final percent from current Canvas position
                var leftBarX = Canvas.GetLeft(LeftBar);
                var newPercent = (leftBarX + barWidth / 2) / screenWidth;
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
                var rightBarX = Canvas.GetLeft(RightBar);
                var newPercent = (rightBarX + barWidth / 2) / screenWidth;
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
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
