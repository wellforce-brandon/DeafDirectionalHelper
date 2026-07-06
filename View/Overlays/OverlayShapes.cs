using System;
using System.Windows;
using System.Windows.Media;

namespace DeafDirectionalHelper.View.Overlays;

/// <summary>
/// Geometry helpers shared by the radial overlay styles. WPF has no conic
/// gradient, so ring sectors are built as arc-segment paths (also crisper).
/// Angles are degrees clockwise from north (up).
/// </summary>
public static class OverlayShapes
{
    /// <summary>Ring sector between two radii spanning [startDeg, startDeg+sweepDeg].</summary>
    public static Geometry RingSector(Point center, double rIn, double rOut, double startDeg, double sweepDeg)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var a0 = startDeg;
            var a1 = startDeg + sweepDeg;
            var largeArc = sweepDeg > 180;

            ctx.BeginFigure(PointAt(center, rOut, a0), isFilled: true, isClosed: true);
            ctx.ArcTo(PointAt(center, rOut, a1), new Size(rOut, rOut), 0, largeArc,
                SweepDirection.Clockwise, true, false);
            ctx.LineTo(PointAt(center, rIn, a1), true, false);
            ctx.ArcTo(PointAt(center, rIn, a0), new Size(rIn, rIn), 0, largeArc,
                SweepDirection.Counterclockwise, true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    /// <summary>Up-pointing chevron (heading marker) centered at origin.</summary>
    public static Geometry Chevron(double width, double height)
    {
        var w = width / 2;
        var h = height / 2;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(-w, h), true, true);
            ctx.LineTo(new Point(0, -h), true, false);
            ctx.LineTo(new Point(w, h), true, false);
            ctx.LineTo(new Point(0, h * 0.35), true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    public static Point PointAt(Point center, double radius, double degreesFromNorth)
    {
        var rad = degreesFromNorth * Math.PI / 180.0;
        return new Point(
            center.X + radius * Math.Sin(rad),
            center.Y - radius * Math.Cos(rad));
    }
}
