using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View.Overlays;

/// <summary>
/// Style 2 — edge glow (design 2f). Sides glow for SL/SR, corners for
/// FL/FR/RL/RR, top-center for C. LFE has no element (pair with side bars).
/// Silent = fully invisible.
/// </summary>
public sealed class EdgeGlowStyle : IOverlayStyle
{
    private readonly Rectangle _leftGlow = new();
    private readonly Rectangle _rightGlow = new();
    private readonly Rectangle _leftEdge = new();
    private readonly Rectangle _rightEdge = new();
    private readonly Rectangle[] _corners = new Rectangle[4]; // FL, FR, RL, RR
    private readonly Rectangle _centerGlow = new();

    private readonly LinearGradientBrush _leftBrush;
    private readonly LinearGradientBrush _rightBrush;
    private readonly SolidColorBrush _leftEdgeBrush = new(Colors.Transparent);
    private readonly SolidColorBrush _rightEdgeBrush = new(Colors.Transparent);
    private readonly RadialGradientBrush[] _cornerBrushes = new RadialGradientBrush[4];
    private readonly LinearGradientBrush _centerBrush;

    public EdgeGlowStyle()
    {
        _leftBrush = HorizontalFade(leftToRight: true);
        _rightBrush = HorizontalFade(leftToRight: false);
        _leftGlow.Fill = _leftBrush;
        _rightGlow.Fill = _rightBrush;
        _leftEdge.Fill = _leftEdgeBrush;
        _rightEdge.Fill = _rightEdgeBrush;

        for (int i = 0; i < 4; i++)
        {
            // Gradient origin sits at the screen corner; fades out by 60 %
            var originX = i is 0 or 2 ? 0.0 : 1.0; // FL/RL left, FR/RR right
            var originY = i is 0 or 1 ? 0.0 : 1.0; // FL/FR top, RL/RR bottom
            _cornerBrushes[i] = new RadialGradientBrush
            {
                Center = new Point(originX, originY),
                GradientOrigin = new Point(originX, originY),
                RadiusX = 1.0,
                RadiusY = 1.0,
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Colors.Transparent, 0),
                    new GradientStop(Colors.Transparent, 0.6)
                }
            };
            _corners[i] = new Rectangle { Fill = _cornerBrushes[i] };
        }

        _centerBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 0),
            EndPoint = new Point(0.5, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Colors.Transparent, 0),
                new GradientStop(Colors.Transparent, 1)
            }
        };
        _centerGlow.Fill = _centerBrush;
    }

    private static LinearGradientBrush HorizontalFade(bool leftToRight)
    {
        return new LinearGradientBrush
        {
            StartPoint = leftToRight ? new Point(0, 0.5) : new Point(1, 0.5),
            EndPoint = leftToRight ? new Point(1, 0.5) : new Point(0, 0.5),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Colors.Transparent, 0),
                new GradientStop(Colors.Transparent, 1)
            }
        };
    }

    public void Attach(Canvas canvas)
    {
        canvas.Children.Add(_leftGlow);
        canvas.Children.Add(_rightGlow);
        canvas.Children.Add(_leftEdge);
        canvas.Children.Add(_rightEdge);
        foreach (var corner in _corners) canvas.Children.Add(corner);
        canvas.Children.Add(_centerGlow);
    }

    public void Detach(Canvas canvas)
    {
        canvas.Children.Remove(_leftGlow);
        canvas.Children.Remove(_rightGlow);
        canvas.Children.Remove(_leftEdge);
        canvas.Children.Remove(_rightEdge);
        foreach (var corner in _corners) canvas.Children.Remove(corner);
        canvas.Children.Remove(_centerGlow);
    }

    public void ApplyLayout(AppSettings settings, Rect workArea)
    {
        var u = workArea.Height / 1080.0;
        var s = settings.Bars.OverlaySize;
        var w = workArea.Width;
        var h = workArea.Height;

        var depth = 170 * u * s;
        SetRect(_leftGlow, 0, 0, depth, h);
        SetRect(_rightGlow, w - depth, 0, depth, h);

        var edge = 8 * u;
        SetRect(_leftEdge, 0, 0, edge, h);
        SetRect(_rightEdge, w - edge, 0, edge, h);

        var corner = 560 * u * s;
        SetRect(_corners[0], 0, 0, corner, corner);                 // FL top-left
        SetRect(_corners[1], w - corner, 0, corner, corner);        // FR top-right
        SetRect(_corners[2], 0, h - corner, corner, corner);        // RL bottom-left
        SetRect(_corners[3], w - corner, h - corner, corner, corner); // RR bottom-right

        var cw = 720 * u * s;
        var ch = 150 * u * s;
        SetRect(_centerGlow, (w - cw) / 2, 0, cw, ch);
    }

    public void Render(LevelFrame frame, BarSettings bars)
    {
        var scale = bars.ColorScale;

        // Sides: SL (6), SR (7)
        SetFade(_leftBrush, scale, frame.Levels[6]);
        SetFade(_rightBrush, scale, frame.Levels[7]);
        _leftEdgeBrush.Color = EdgeColor(scale, frame.Levels[6]);
        _rightEdgeBrush.Color = EdgeColor(scale, frame.Levels[7]);

        // Corners: FL (0), FR (1), RL (4), RR (5)
        SetCorner(_cornerBrushes[0], scale, frame.Levels[0]);
        SetCorner(_cornerBrushes[1], scale, frame.Levels[1]);
        SetCorner(_cornerBrushes[2], scale, frame.Levels[4]);
        SetCorner(_cornerBrushes[3], scale, frame.Levels[5]);

        // Center: C (2)
        SetFadeStops(_centerBrush, scale, frame.Levels[2], alphaCap: 0.8);
    }

    private static void SetFade(LinearGradientBrush brush, ColorScale scale, double level)
    {
        SetFadeStops(brush, scale, level, alphaCap: 0.85);
    }

    private static void SetFadeStops(GradientBrush brush, ColorScale scale, double level, double alphaCap)
    {
        var color = level < ScaleEngine.InvisibleBelow
            ? Colors.Transparent
            : ScaleEngine.WithAlpha(scale, level, Math.Min(alphaCap, level));
        brush.GradientStops[0].Color = color;
        // stop 1 stays transparent
    }

    private static void SetCorner(RadialGradientBrush brush, ColorScale scale, double level)
    {
        brush.GradientStops[0].Color = level < ScaleEngine.InvisibleBelow
            ? Colors.Transparent
            : ScaleEngine.WithAlpha(scale, level, Math.Min(0.8, level));
    }

    private static Color EdgeColor(ColorScale scale, double level)
    {
        return level < ScaleEngine.InvisibleBelow
            ? Colors.Transparent
            : ScaleEngine.WithAlpha(scale, level, Math.Min(1.0, level * 1.4));
    }

    private static void SetRect(Rectangle rect, double x, double y, double width, double height)
    {
        rect.Width = width;
        rect.Height = height;
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
    }
}
