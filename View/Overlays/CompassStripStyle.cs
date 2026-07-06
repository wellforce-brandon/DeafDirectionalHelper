using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View.Overlays;

/// <summary>
/// Style 5 — compass strip (design 2h). A horizontal strip of 7 channel cells
/// (RL SL FL C FR SR RR) plus a divided LFE cell, anchored top (default) or
/// bottom, with a heading marker at screen center.
/// </summary>
public sealed class CompassStripStyle : IOverlayStyle
{
    private static readonly int[] CellChannels = { 4, 6, 0, 2, 1, 7, 5 }; // RL SL FL C FR SR RR
    private static readonly string[] CellLabels = { "RL", "SL", "FL", "C", "FR", "SR", "RR" };

    private readonly Border _strip;
    private readonly Grid _content;
    private readonly Border[] _fills = new Border[8];   // 7 cells + LFE
    private readonly SolidColorBrush[] _fillBrushes = new SolidColorBrush[8];
    private readonly Polygon _marker;

    public CompassStripStyle()
    {
        _content = new Grid { VerticalAlignment = VerticalAlignment.Bottom };
        for (int i = 0; i < 7; i++)
            _content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });      // divider
        _content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // LFE

        for (int i = 0; i < 7; i++)
            _content.Children.Add(BuildCell(i, CellLabels[i], labelAlpha: 0.9));

        var divider = new Border
        {
            Width = 2,
            Background = new SolidColorBrush(Color.FromArgb(51, 255, 255, 255)),
            Margin = new Thickness(4, 6, 4, 6)
        };
        Grid.SetColumn(divider, 7);
        _content.Children.Add(divider);

        var lfeCell = BuildCell(7, "LFE", labelAlpha: 0.75);
        Grid.SetColumn(lfeCell, 8);
        _content.Children.Add(lfeCell);

        _strip = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(140, 9, 11, 14)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(77, 255, 255, 255)),
            BorderThickness = new Thickness(2),
            Child = _content
        };

        _marker = new Polygon
        {
            Fill = new SolidColorBrush(Color.FromArgb(217, 255, 255, 255))
        };
    }

    private FrameworkElement BuildCell(int index, string label, double labelAlpha)
    {
        _fillBrushes[index] = new SolidColorBrush(Colors.Transparent);
        _fills[index] = new Border
        {
            Background = _fillBrushes[index],
            VerticalAlignment = VerticalAlignment.Bottom,
            Height = 0
        };

        var meter = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(31, 255, 255, 255)),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = _fills[index]
        };

        var text = new TextBlock
        {
            Text = label,
            FontFamily = new FontFamily("Consolas"),
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb((byte)(labelAlpha * 255), 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black, Opacity = 0.9, BlurRadius = 4, ShadowDepth = 1, Direction = 270
            }
        };

        var cell = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(5, 0, 5, 0) };
        cell.Children.Add(meter);
        cell.Children.Add(text);
        Grid.SetColumn(cell, index < 7 ? index : 8);

        // Stash references for layout scaling
        cell.Tag = (meter, text);
        return cell;
    }

    public void Attach(Canvas canvas)
    {
        canvas.Children.Add(_strip);
        canvas.Children.Add(_marker);
    }

    public void Detach(Canvas canvas)
    {
        canvas.Children.Remove(_strip);
        canvas.Children.Remove(_marker);
    }

    public void ApplyLayout(AppSettings settings, Rect workArea)
    {
        var u = workArea.Height / 1080.0;
        var s = settings.Bars.OverlaySize;

        var width = 640 * u * s;
        var height = 96 * u * s;
        var top = settings.Bars.Anchor switch
        {
            OverlayAnchor.Top => 36 * u,
            OverlayAnchor.Bottom => workArea.Height - 36 * u - height,
            _ => (workArea.Height - height) / 2
        };

        _strip.Width = width;
        _strip.Height = height;
        _strip.CornerRadius = new CornerRadius(18 * u * s);
        _strip.Padding = new Thickness(18 * u * s, 0, 18 * u * s, 8 * u * s);
        Canvas.SetLeft(_strip, (workArea.Width - width) / 2);
        Canvas.SetTop(_strip, top);

        foreach (var child in _content.Children)
        {
            if (child is StackPanel cell && cell.Tag is ValueTuple<Border, TextBlock>(var meter, var text))
            {
                meter.Width = 30 * u * s;
                meter.Height = 44 * u * s;
                text.FontSize = Math.Max(9, (Grid.GetColumn(cell) == 8 ? 15 : 17) * u * s);
                text.Margin = new Thickness(0, 3 * u * s, 0, 0);
            }
        }

        // Heading marker: down-triangle just above the strip at screen center
        var mw = 18 * u * s;
        var mh = 12 * u * s;
        _marker.Points = new PointCollection
        {
            new Point(0, 0), new Point(mw, 0), new Point(mw / 2, mh)
        };
        Canvas.SetLeft(_marker, workArea.Width / 2 - mw / 2);
        Canvas.SetTop(_marker, top - mh - 4 * u);
    }

    public void Render(LevelFrame frame, BarSettings bars)
    {
        var meterHeight = _fills[0].Parent is Border parent ? parent.Height : 44;

        for (int i = 0; i < 7; i++)
            RenderCell(i, frame.Levels[CellChannels[i]], bars.ColorScale, meterHeight);
        RenderCell(7, frame.Levels[3], bars.ColorScale, meterHeight); // LFE
    }

    private void RenderCell(int index, double level, ColorScale scale, double meterHeight)
    {
        _fills[index].Height = level * meterHeight;
        _fillBrushes[index].Color = level < ScaleEngine.InvisibleBelow
            ? Colors.Transparent
            : ScaleEngine.At(scale, level);
    }
}
