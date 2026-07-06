using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View.Overlays;

/// <summary>
/// Style 1 — refined side bars (design 2e). Two vertical bars, three pill
/// segments each: top = Front, center = Side, bottom = Rear. Fills rise from
/// the bottom of each segment in the scale color; a white peak tick decays.
/// Left bar: F=FL S=SL R=RL; right bar: F=FR S=SR R=RR.
/// </summary>
public sealed class SideBarsStyle : IOverlayStyle
{
    private static readonly double[] SegmentRatios = { 264, 348, 264 }; // design px, flex
    private const double DesignGap = 8;
    private const double DesignMarginV = 90;

    // [side, segment]: side 0 = left, 1 = right
    private static readonly int[,] Channels = { { 0, 6, 4 }, { 1, 7, 5 } };
    private static readonly string[] SegmentLabels = { "F", "S", "R" };

    private readonly Border[] _backplates = new Border[2];
    private readonly Border[,] _tracks = new Border[2, 3];
    private readonly Border[,] _fills = new Border[2, 3];
    private readonly SolidColorBrush[,] _fillBrushes = new SolidColorBrush[2, 3];
    private readonly Border[,] _ticks = new Border[2, 3];
    private readonly TextBlock[,] _labels = new TextBlock[2, 3];

    private readonly double[,] _segHeights = new double[2, 3];
    private Rect _workArea;

    public SideBarsStyle()
    {
        for (int side = 0; side < 2; side++)
        {
            _backplates[side] = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(140, 10, 13, 16)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(89, 255, 255, 255)),
                BorderThickness = new Thickness(2)
            };

            for (int seg = 0; seg < 3; seg++)
            {
                _fillBrushes[side, seg] = new SolidColorBrush(Colors.Transparent);
                _fills[side, seg] = new Border
                {
                    Background = _fillBrushes[side, seg],
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Height = 0
                };
                _tracks[side, seg] = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                    ClipToBounds = true,
                    Child = _fills[side, seg]
                };
                _ticks[side, seg] = new Border
                {
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(2),
                    Height = 4,
                    Visibility = Visibility.Collapsed
                };
                _labels[side, seg] = new TextBlock
                {
                    Text = SegmentLabels[seg],
                    FontFamily = new FontFamily("Consolas"),
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black, Opacity = 0.9, BlurRadius = 4, ShadowDepth = 1, Direction = 270
                    }
                };
            }
        }
    }

    public void Attach(Canvas canvas)
    {
        for (int side = 0; side < 2; side++)
        {
            canvas.Children.Add(_backplates[side]);
            for (int seg = 0; seg < 3; seg++)
            {
                canvas.Children.Add(_tracks[side, seg]);
                canvas.Children.Add(_ticks[side, seg]);
                canvas.Children.Add(_labels[side, seg]);
            }
        }
    }

    public void Detach(Canvas canvas)
    {
        for (int side = 0; side < 2; side++)
        {
            canvas.Children.Remove(_backplates[side]);
            for (int seg = 0; seg < 3; seg++)
            {
                canvas.Children.Remove(_tracks[side, seg]);
                canvas.Children.Remove(_ticks[side, seg]);
                canvas.Children.Remove(_labels[side, seg]);
            }
        }
    }

    public void ApplyLayout(AppSettings settings, Rect workArea)
    {
        _workArea = workArea;
        var u = workArea.Height / 1080.0;
        var s = settings.Bars.OverlaySize;

        var barWidth = Math.Max(8, settings.Bars.Width * u * s);
        var marginV = DesignMarginV * u;
        var gap = DesignGap * u;
        var totalH = workArea.Height - 2 * marginV;
        var ratioSum = SegmentRatios[0] + SegmentRatios[1] + SegmentRatios[2];
        var contentH = totalH - 2 * gap;

        var centers = new[]
        {
            settings.Bars.LeftIndicatorPercent * workArea.Width,
            settings.Bars.RightIndicatorPercent * workArea.Width
        };

        for (int side = 0; side < 2; side++)
        {
            var x = Math.Clamp(centers[side] - barWidth / 2, 0, workArea.Width - barWidth);

            var pad = 6 * u;
            _backplates[side].Width = barWidth + 2 * pad;
            _backplates[side].Height = totalH + 2 * pad;
            _backplates[side].CornerRadius = new CornerRadius((barWidth + 2 * pad) / 2);
            Canvas.SetLeft(_backplates[side], x - pad);
            Canvas.SetTop(_backplates[side], marginV - pad);

            var y = marginV;
            for (int seg = 0; seg < 3; seg++)
            {
                var segH = contentH * SegmentRatios[seg] / ratioSum;
                _segHeights[side, seg] = segH;

                _tracks[side, seg].Width = barWidth;
                _tracks[side, seg].Height = segH;
                _tracks[side, seg].CornerRadius = new CornerRadius(barWidth / 2);
                Canvas.SetLeft(_tracks[side, seg], x);
                Canvas.SetTop(_tracks[side, seg], y);

                _ticks[side, seg].Width = Math.Max(2, barWidth - 4 * u);
                Canvas.SetLeft(_ticks[side, seg], x + 2 * u);

                var label = _labels[side, seg];
                label.FontSize = Math.Max(12, 22 * u * s);
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var labelX = side == 0
                    ? x + barWidth + 8 * u
                    : x - 8 * u - label.DesiredSize.Width;
                Canvas.SetLeft(label, labelX);
                Canvas.SetTop(label, y + segH / 2 - label.DesiredSize.Height / 2);

                y += segH + gap;
            }
        }
    }

    public void Render(LevelFrame frame, BarSettings bars)
    {
        var u = _workArea.Height / 1080.0;
        var marginV = DesignMarginV * u;
        var gap = DesignGap * u;

        for (int side = 0; side < 2; side++)
        {
            var y = marginV;
            for (int seg = 0; seg < 3; seg++)
            {
                var level = frame.Levels[Channels[side, seg]];
                var trail = frame.Trails[Channels[side, seg]];
                var segH = _segHeights[side, seg];

                _fills[side, seg].Height = level * segH;
                _fillBrushes[side, seg].Color = ScaleEngine.At(bars.ColorScale, level);

                // Peak tick: bottom = trail %, hidden once the trail decays to its floor
                if (trail > 0.015)
                {
                    _ticks[side, seg].Visibility = Visibility.Visible;
                    Canvas.SetTop(_ticks[side, seg], y + segH - trail * segH - 4 + 2 * u);
                }
                else
                {
                    _ticks[side, seg].Visibility = Visibility.Collapsed;
                }

                y += segH + gap;
            }
        }
    }

    // --- Move-mode support ---

    /// <summary>Returns 0 (left), 1 (right) or -1 for a canvas-space point near a bar.</summary>
    public int HitTestBar(Point point, AppSettings settings)
    {
        var centers = new[]
        {
            settings.Bars.LeftIndicatorPercent * _workArea.Width,
            settings.Bars.RightIndicatorPercent * _workArea.Width
        };
        for (int side = 0; side < 2; side++)
        {
            if (Math.Abs(point.X - centers[side]) < 60)
                return side;
        }
        return -1;
    }
}
