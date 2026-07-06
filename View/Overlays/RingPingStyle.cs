using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View.Overlays;

/// <summary>
/// Style 4 — ring ping (designs 4a/4b). Concentric bands, same 45° sector
/// layout as RadarRing. Distance mapping (default): the loudness bucket picks
/// ONE band, loud = innermost — a quiet blip appears on the rim and marches
/// inward as it gets louder. Meter mapping: bands fill from the center out.
/// </summary>
public sealed class RingPingStyle : IOverlayStyle
{
    private static readonly int[] SectorChannels = { 2, 1, 7, 5, -1, 4, 6, 0 };

    private const double InnerPct = 0.22;
    private const double OuterPct = 0.484;
    private const double GapPct = 0.006;
    private const double LevelDeadZone = 0.03;

    private readonly List<Ellipse> _outlines = new();
    // _bands[band][sector]; band 0 = innermost
    private Path[][] _bands = Array.Empty<Path[]>();
    private SolidColorBrush[][] _bandBrushes = Array.Empty<SolidColorBrush[]>();
    private readonly CenterCluster _center = new();

    private Canvas? _canvas;
    private int _ringCount;

    public void Attach(Canvas canvas)
    {
        _canvas = canvas;
        _center.Attach(canvas);
        // Bands/outlines are (re)built in ApplyLayout because ring count is a setting.
    }

    public void Detach(Canvas canvas)
    {
        RemoveRings(canvas);
        _center.Detach(canvas);
        _canvas = null;
    }

    private void RemoveRings(Canvas canvas)
    {
        foreach (var outline in _outlines) canvas.Children.Remove(outline);
        _outlines.Clear();
        foreach (var band in _bands)
            foreach (var wedge in band)
                canvas.Children.Remove(wedge);
        _bands = Array.Empty<Path[]>();
        _bandBrushes = Array.Empty<SolidColorBrush[]>();
    }

    public void ApplyLayout(AppSettings settings, Rect workArea)
    {
        if (_canvas == null) return;

        var u = workArea.Height / 1080.0;
        var s = settings.Bars.OverlaySize;
        var box = 280 * u * s;
        _ringCount = Math.Clamp(settings.Bars.RingCount, 3, 7);

        var centerY = settings.Bars.Anchor == OverlayAnchor.Bottom
            ? workArea.Height - 70 * u - box / 2
            : 70 * u + box / 2;
        var center = new Point(workArea.Width / 2, centerY);

        RemoveRings(_canvas);

        // Bands evenly distributed between 22 % and 48.4 % of the box, 0.6 % gaps
        var n = _ringCount;
        var bandPct = (OuterPct - InnerPct - (n - 1) * GapPct) / n;

        _bands = new Path[n][];
        _bandBrushes = new SolidColorBrush[n][];

        for (int band = 0; band < n; band++)
        {
            var rIn = (InnerPct + band * (bandPct + GapPct)) * box;
            var rOut = rIn + bandPct * box;

            var outline = new Ellipse
            {
                Width = rOut * 2,
                Height = rOut * 2,
                Stroke = new SolidColorBrush(band == n - 1
                    ? Color.FromArgb(61, 255, 255, 255)
                    : Color.FromArgb(41, 255, 255, 255)),
                StrokeThickness = band == n - 1 ? 1.5 : 1
            };
            Canvas.SetLeft(outline, center.X - rOut);
            Canvas.SetTop(outline, center.Y - rOut);
            _canvas.Children.Add(outline);
            _outlines.Add(outline);

            _bands[band] = new Path[8];
            _bandBrushes[band] = new SolidColorBrush[8];
            for (int k = 0; k < 8; k++)
            {
                if (SectorChannels[k] < 0) continue; // no rear-gap wedges here

                var brush = new SolidColorBrush(Colors.Transparent);
                var wedge = new Path
                {
                    Fill = brush,
                    Data = OverlayShapes.RingSector(center, rIn, rOut, -22.5 + 45 * k, 45)
                };
                _bandBrushes[band][k] = brush;
                _bands[band][k] = wedge;
                _canvas.Children.Add(wedge);
            }
        }

        _center.Layout(center, OuterPct * box, u, s);
    }

    public void Render(LevelFrame frame, BarSettings bars)
    {
        var n = _ringCount;
        if (n == 0 || _bands.Length != n) return;

        for (int k = 0; k < 8; k++)
        {
            var channel = SectorChannels[k];
            if (channel < 0) continue;

            var level = frame.Levels[channel];
            var bucket = level < LevelDeadZone ? 0 : (int)Math.Ceiling(level * n);
            bucket = Math.Min(bucket, n);

            var color = bucket == 0
                ? Colors.Transparent
                : ScaleEngine.WithAlpha(bars.ColorScale, level, Math.Min(0.95, 0.35 + level * 0.65));

            for (int band = 0; band < n; band++)
            {
                bool lit;
                if (bars.RingMapping == RingMapping.Distance)
                {
                    // Loud = innermost: 1-based lit band = n + 1 - bucket
                    lit = bucket > 0 && band == n - bucket;
                }
                else
                {
                    // Meter: fill from center outward
                    lit = band < bucket;
                }
                _bandBrushes[band][k].Color = lit ? color : Colors.Transparent;
            }
        }

        _center.Render(frame.Levels[3]);
    }
}
