using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DeafDirectionalHelper.View.Overlays;

/// <summary>
/// Shared center of RadarRing and RingPing: white up-chevron (you, facing
/// forward), an "F" label above the ring, and the pulsing LFE disc.
/// </summary>
public sealed class CenterCluster
{
    private readonly Path _chevron;
    private readonly TextBlock _label;
    private readonly Ellipse _lfe;
    private readonly SolidColorBrush _lfeBrush = new(Colors.Transparent);
    private readonly ScaleTransform _lfeScale = new(1, 1);

    public CenterCluster()
    {
        _chevron = new Path
        {
            Fill = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255))
        };
        _label = new TextBlock
        {
            Text = "F",
            FontFamily = new FontFamily("Consolas"),
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255))
        };
        _lfe = new Ellipse
        {
            Fill = _lfeBrush,
            RenderTransform = _lfeScale,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
    }

    public void Attach(Canvas canvas)
    {
        canvas.Children.Add(_lfe);
        canvas.Children.Add(_chevron);
        canvas.Children.Add(_label);
    }

    public void Detach(Canvas canvas)
    {
        canvas.Children.Remove(_lfe);
        canvas.Children.Remove(_chevron);
        canvas.Children.Remove(_label);
    }

    public void Layout(Point center, double outerRadius, double u, double s)
    {
        _chevron.Data = OverlayShapes.Chevron(28 * u * s, 24 * u * s);
        Canvas.SetLeft(_chevron, center.X);
        Canvas.SetTop(_chevron, center.Y);

        _label.FontSize = Math.Max(10, 18 * u * s);
        _label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(_label, center.X - _label.DesiredSize.Width / 2);
        Canvas.SetTop(_label, center.Y - outerRadius - _label.DesiredSize.Height - 4 * u);

        var lfeSize = 68 * u * s;
        _lfe.Width = lfeSize;
        _lfe.Height = lfeSize;
        Canvas.SetLeft(_lfe, center.X - lfeSize / 2);
        Canvas.SetTop(_lfe, center.Y - lfeSize / 2);
    }

    public void Render(double lfeLevel)
    {
        if (lfeLevel < ScaleEngine.InvisibleBelow)
        {
            _lfeBrush.Color = Colors.Transparent;
        }
        else
        {
            var alpha = Math.Min(0.9, 0.12 + lfeLevel);
            _lfeBrush.Color = Color.FromArgb((byte)(alpha * 255), 255, 255, 255);
            var scale = 1 + lfeLevel * 0.35;
            _lfeScale.ScaleX = scale;
            _lfeScale.ScaleY = scale;
        }
    }
}
