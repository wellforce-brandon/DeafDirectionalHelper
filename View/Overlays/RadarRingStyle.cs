using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View.Overlays;

/// <summary>
/// Style 3 — radar ring (design 2g). A donut of 45° sectors starting at
/// −22.5°, clockwise: C, FR, SR, RR, [rear gap], RL, SL, FL. The hole is the
/// inner 54 %; LFE pulses as a center disc.
/// </summary>
public sealed class RadarRingStyle : IOverlayStyle
{
    // Sector k (from north, clockwise) → channel index; -1 = rear gap
    private static readonly int[] SectorChannels = { 2, 1, 7, 5, -1, 4, 6, 0 };

    private readonly Ellipse _base;
    private readonly Path[] _wedges = new Path[8];
    private readonly SolidColorBrush[] _wedgeBrushes = new SolidColorBrush[8];
    private readonly CenterCluster _center = new();

    public RadarRingStyle()
    {
        _base = new Ellipse
        {
            Fill = new SolidColorBrush(Color.FromArgb(128, 9, 11, 14)),
            Stroke = new SolidColorBrush(Color.FromArgb(77, 255, 255, 255)),
            StrokeThickness = 2
        };

        for (int k = 0; k < 8; k++)
        {
            _wedgeBrushes[k] = new SolidColorBrush(
                SectorChannels[k] < 0 ? Color.FromArgb(13, 255, 255, 255) : Colors.Transparent);
            _wedges[k] = new Path { Fill = _wedgeBrushes[k] };
        }
    }

    public void Attach(Canvas canvas)
    {
        canvas.Children.Add(_base);
        foreach (var wedge in _wedges) canvas.Children.Add(wedge);
        _center.Attach(canvas);
    }

    public void Detach(Canvas canvas)
    {
        canvas.Children.Remove(_base);
        foreach (var wedge in _wedges) canvas.Children.Remove(wedge);
        _center.Detach(canvas);
    }

    public void ApplyLayout(AppSettings settings, Rect workArea)
    {
        var u = workArea.Height / 1080.0;
        var s = settings.Bars.OverlaySize;
        var size = 220 * u * s;
        var r = size / 2;

        var centerY = settings.Bars.Anchor == OverlayAnchor.Bottom
            ? workArea.Height - 70 * u - r
            : 70 * u + r;
        var center = new Point(workArea.Width / 2, centerY);

        _base.Width = size;
        _base.Height = size;
        Canvas.SetLeft(_base, center.X - r);
        Canvas.SetTop(_base, center.Y - r);

        for (int k = 0; k < 8; k++)
        {
            var start = -22.5 + 45 * k;
            _wedges[k].Data = OverlayShapes.RingSector(center, r * 0.54, r, start, 45);
        }

        _center.Layout(center, r, u, s);
    }

    public void Render(LevelFrame frame, BarSettings bars)
    {
        for (int k = 0; k < 8; k++)
        {
            var channel = SectorChannels[k];
            if (channel < 0) continue; // rear gap stays faint & static

            var level = frame.Levels[channel];
            _wedgeBrushes[k].Color = level < ScaleEngine.InvisibleBelow
                ? Colors.Transparent
                : ScaleEngine.WithAlpha(bars.ColorScale, level, Math.Min(0.92, 0.15 + level * 0.85));
        }

        _center.Render(frame.Levels[3]);
    }
}
