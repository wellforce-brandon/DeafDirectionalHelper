using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DeafDirectionalHelper.Helpers;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View
{
    public partial class Full7Point1View : Window
    {
        private readonly SettingsManager _settingsManager;
        private bool _isDragging;
        private Point _dragStartPoint;
        private double _dragStartLeft;
        private double _dragStartTop;
        private bool _allowDragging;

        public bool AllowDragging
        {
            get => _allowDragging;
            set
            {
                _allowDragging = value;
                SetClickThrough(!value);
            }
        }

        public Full7Point1View(ColoredSpeakers speakers)
        {
            InitializeComponent();

            _settingsManager = SettingsManager.Instance;
            DataContext = speakers;
            Topmost = true;

            // Subscribe to settings changes to update layout
            _settingsManager.SettingsChanged += OnSettingsChanged;

            // Apply initial layout
            ApplyLayout();

            // Start as click-through (non-interactable) unless dragging is already enabled
            Loaded += (_, _) => SetClickThrough(!_allowDragging);
        }

        private void SetClickThrough(bool clickThrough) =>
            WindowHelper.SetClickThrough(this, clickThrough);

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            ApplyLayout();
        }

        /// <summary>
        /// Applies the current layout setting (Spatial or HorizontalLine)
        /// </summary>
        public void ApplyLayout()
        {
            var settings = _settingsManager.Settings;
            var layout = settings.Bars.SurroundLayout;

            if (layout == SurroundLayout.HorizontalLine)
            {
                PositionSpeakersHorizontal(settings.Bars.HideLfe);
            }
            else
            {
                PositionSpeakersSpatial(settings.Bars.HideLfe, settings.Bars.HideYou);
            }

            // Recenter after layout change
            Left = MainWindow.ScreenLeft + (MainWindow.ScreenWidth - Width) / 2;
            Top = MainWindow.ScreenTop + MainWindow.ScreenHeight - Height - 100;
        }

        private void PositionSpeakersSpatial(bool hideLfe, bool hideYou)
        {
            var settings = _settingsManager.Settings;
            var scale = settings.Bars.SpatialScale;

            // Scale window size based on scale factor
            var baseWidth = 400.0;
            var baseHeight = 350.0;
            Width = baseWidth * scale;
            Height = baseHeight * scale;

            var cx = Width / 2;
            var cy = Height / 2 + 30 * scale; // Listener slightly below center

            // Keep original speaker sizes (no scaling)
            FrontLeft.Width = FrontLeft.Height = 60;
            FrontRight.Width = FrontRight.Height = 60;
            Center.Width = Center.Height = 70;
            LFE.Width = LFE.Height = 80;
            RearLeft.Width = RearLeft.Height = 50;
            RearRight.Width = RearRight.Height = 50;
            SideLeft.Width = SideLeft.Height = 50;
            SideRight.Width = SideRight.Height = 50;

            // Show/hide listener based on setting
            Listener.Visibility = hideYou ? Visibility.Collapsed : Visibility.Visible;
            ListenerLabel.Visibility = hideYou ? Visibility.Collapsed : Visibility.Visible;

            // Base positions relative to listener at scale 1.0 (offsets from cx, cy)
            // These are the original hardcoded positions converted to offsets from center

            // Front Left - top left area (was cx-130, 20 at base)
            var flX = cx - 130 * scale;
            var flY = 20 * scale;
            Canvas.SetLeft(FrontLeft, flX);
            Canvas.SetTop(FrontLeft, flY);
            Canvas.SetLeft(FrontLeftLabel, flX + 22);
            Canvas.SetTop(FrontLeftLabel, flY + 23);

            // Front Right - top right area (was cx+70, 20 at base)
            var frX = cx + 70 * scale;
            var frY = 20 * scale;
            Canvas.SetLeft(FrontRight, frX);
            Canvas.SetTop(FrontRight, frY);
            Canvas.SetLeft(FrontRightLabel, frX + 22);
            Canvas.SetTop(FrontRightLabel, frY + 23);

            // Center - top center (was cx-35, 10 at base)
            var cY = 10 * scale;
            Canvas.SetLeft(Center, cx - 35);
            Canvas.SetTop(Center, cY);
            Canvas.SetLeft(CenterLabel, cx - 5);
            Canvas.SetTop(CenterLabel, cY + 25);

            // LFE/Subwoofer - center front (was cx-40, 90 at base)
            LFE.Visibility = hideLfe ? Visibility.Collapsed : Visibility.Visible;
            LFELabel.Visibility = hideLfe ? Visibility.Collapsed : Visibility.Visible;
            var lfeY = 90 * scale;
            Canvas.SetLeft(LFE, cx - 40);
            Canvas.SetTop(LFE, lfeY);
            Canvas.SetLeft(LFELabel, cx - 12);
            Canvas.SetTop(LFELabel, lfeY + 30);

            // Side Left - middle left (was 20, cy-25 at base)
            var slX = 20 * scale;
            Canvas.SetLeft(SideLeft, slX);
            Canvas.SetTop(SideLeft, cy - 25);
            Canvas.SetLeft(SideLeftLabel, slX + 15);
            Canvas.SetTop(SideLeftLabel, cy - 7);

            // Side Right - middle right (was w-70, cy-25 at base)
            var srX = Width - 70 * scale;
            Canvas.SetLeft(SideRight, srX);
            Canvas.SetTop(SideRight, cy - 25);
            Canvas.SetLeft(SideRightLabel, srX + 15);
            Canvas.SetTop(SideRightLabel, cy - 7);

            // Rear Left - bottom left (was 50, h-80 at base)
            var rlX = 50 * scale;
            var rlY = Height - 80 * scale;
            Canvas.SetLeft(RearLeft, rlX);
            Canvas.SetTop(RearLeft, rlY);
            Canvas.SetLeft(RearLeftLabel, rlX + 15);
            Canvas.SetTop(RearLeftLabel, rlY + 17);

            // Rear Right - bottom right (was w-100, h-80 at base)
            var rrX = Width - 100 * scale;
            var rrY = Height - 80 * scale;
            Canvas.SetLeft(RearRight, rrX);
            Canvas.SetTop(RearRight, rrY);
            Canvas.SetLeft(RearRightLabel, rrX + 15);
            Canvas.SetTop(RearRightLabel, rrY + 17);

            // Listener - always at center
            Canvas.SetLeft(Listener, cx - 15);
            Canvas.SetTop(Listener, cy - 15);
            Canvas.SetLeft(ListenerLabel, cx - 12);
            Canvas.SetTop(ListenerLabel, cy - 5);
        }

        private void PositionSpeakersHorizontal(bool hideLfe)
        {
            // Horizontal line layout: LR - LM - LF - [big gap] - C - LFE - [big gap] - RF - RM - RR
            // Left group pushed left, right group pushed right, center stays in middle
            // Speakers within each group of 3 are also spaced out

            var speakerSize = 60;
            var spacing = 10;
            var groupSpacing = 40; // Spacing between speakers within each group of 3
            var centerGap = 80; // Double gap between groups and center

            // Calculate total width
            // Left group: 3 speakers + 2 group spacings
            // Center group: 1-2 speakers + (0-1) spacing
            // Right group: 3 speakers + 2 group spacings
            // Plus: 2 center gaps + padding
            var centerCount = hideLfe ? 1 : 2;
            var totalWidth = 6 * speakerSize + 4 * groupSpacing + centerCount * speakerSize + (centerCount - 1) * spacing + 2 * centerGap + 40;

            // Set compact horizontal size
            Width = totalWidth;
            Height = 100;

            // Hide listener in horizontal mode
            Listener.Visibility = Visibility.Collapsed;
            ListenerLabel.Visibility = Visibility.Collapsed;

            // LFE visibility
            LFE.Visibility = hideLfe ? Visibility.Collapsed : Visibility.Visible;
            LFELabel.Visibility = hideLfe ? Visibility.Collapsed : Visibility.Visible;

            var y = 10;
            var labelY = y + 22;
            var x = 20;
            var groupStep = speakerSize + groupSpacing; // Step within groups of 3
            var centerStep = speakerSize + spacing; // Tighter step for center group

            // Resize all speakers to uniform size for horizontal layout
            FrontLeft.Width = FrontLeft.Height = speakerSize;
            FrontRight.Width = FrontRight.Height = speakerSize;
            Center.Width = Center.Height = speakerSize;
            LFE.Width = LFE.Height = speakerSize;
            RearLeft.Width = RearLeft.Height = speakerSize;
            RearRight.Width = RearRight.Height = speakerSize;
            SideLeft.Width = SideLeft.Height = speakerSize;
            SideRight.Width = SideRight.Height = speakerSize;

            // === LEFT GROUP (spaced out) ===
            // Position 1: Rear Left (LR)
            Canvas.SetLeft(RearLeft, x);
            Canvas.SetTop(RearLeft, y);
            Canvas.SetLeft(RearLeftLabel, x + 20);
            Canvas.SetTop(RearLeftLabel, labelY);
            x += groupStep;

            // Position 2: Side Left (LM)
            Canvas.SetLeft(SideLeft, x);
            Canvas.SetTop(SideLeft, y);
            Canvas.SetLeft(SideLeftLabel, x + 20);
            Canvas.SetTop(SideLeftLabel, labelY);
            x += groupStep;

            // Position 3: Front Left (LF)
            Canvas.SetLeft(FrontLeft, x);
            Canvas.SetTop(FrontLeft, y);
            Canvas.SetLeft(FrontLeftLabel, x + 20);
            Canvas.SetTop(FrontLeftLabel, labelY);
            x += speakerSize; // Just the speaker size, gap comes next

            // === CENTER GAP (doubled) ===
            x += centerGap;

            // === CENTER GROUP ===
            // Position 4: Center (C)
            Canvas.SetLeft(Center, x);
            Canvas.SetTop(Center, y);
            Canvas.SetLeft(CenterLabel, x + 25);
            Canvas.SetTop(CenterLabel, labelY);
            x += centerStep;

            // Position 5: LFE (if not hidden)
            if (!hideLfe)
            {
                Canvas.SetLeft(LFE, x);
                Canvas.SetTop(LFE, y);
                Canvas.SetLeft(LFELabel, x + 18);
                Canvas.SetTop(LFELabel, labelY);
                x += speakerSize; // Just the speaker size, gap comes next
            }
            else
            {
                x -= spacing; // Adjust since we didn't use centerStep
            }

            // === CENTER GAP (doubled) ===
            x += centerGap;

            // === RIGHT GROUP (spaced out) ===
            // Position 6: Front Right (RF)
            Canvas.SetLeft(FrontRight, x);
            Canvas.SetTop(FrontRight, y);
            Canvas.SetLeft(FrontRightLabel, x + 20);
            Canvas.SetTop(FrontRightLabel, labelY);
            x += groupStep;

            // Position 7: Side Right (RM)
            Canvas.SetLeft(SideRight, x);
            Canvas.SetTop(SideRight, y);
            Canvas.SetLeft(SideRightLabel, x + 20);
            Canvas.SetTop(SideRightLabel, labelY);
            x += groupStep;

            // Position 8: Rear Right (RR)
            Canvas.SetLeft(RearRight, x);
            Canvas.SetTop(RearRight, y);
            Canvas.SetLeft(RearRightLabel, x + 20);
            Canvas.SetTop(RearRightLabel, labelY);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (!AllowDragging) return;

            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            _dragStartLeft = Left;
            _dragStartTop = Top;
            CaptureMouse();
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_isDragging) return;

            var currentPoint = e.GetPosition(this);
            var deltaX = currentPoint.X - _dragStartPoint.X;
            var deltaY = currentPoint.Y - _dragStartPoint.Y;

            var newLeft = _dragStartLeft + deltaX;
            var newTop = _dragStartTop + deltaY;

            // Clamp to target screen bounds
            var minLeft = MainWindow.ScreenLeft;
            var maxLeft = MainWindow.ScreenLeft + MainWindow.ScreenWidth - Width;
            var minTop = MainWindow.ScreenTop;
            var maxTop = MainWindow.ScreenTop + MainWindow.ScreenHeight - Height;
            newLeft = Math.Max(minLeft, Math.Min(newLeft, maxLeft));
            newTop = Math.Max(minTop, Math.Min(newTop, maxTop));

            Left = newLeft;
            Top = newTop;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!_isDragging) return;

            _isDragging = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        }

        /// <summary>
        /// Recenters the 7.1 view to its default position on the target screen
        /// </summary>
        public void Recenter()
        {
            Left = MainWindow.ScreenLeft + (MainWindow.ScreenWidth - Width) / 2;
            Top = MainWindow.ScreenTop + MainWindow.ScreenHeight - Height - 100;
        }
    }
}
