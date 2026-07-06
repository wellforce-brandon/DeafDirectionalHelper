using System;
using System.Windows.Media;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View;

/// <summary>
/// Loudness color scales (plan §1.4): two-segment linear interpolation over
/// stops at t = 0 / 0.5 / 1. Levels below 0.005 render invisible - silent
/// means nothing on screen. Loudness is always triple-encoded elsewhere
/// (fill amount + ramp color + peak mark); hue is never the only signal.
/// </summary>
public static class ScaleEngine
{
    public const double InvisibleBelow = 0.005;

    // stops[scale] = { t0, t0.5, t1 }
    private static Color[] Stops(ColorScale scale) => scale switch
    {
        ColorScale.Thermal => new[] { FromHex("#F0E442"), FromHex("#E69F00"), FromHex("#D55E00") },
        ColorScale.Ice => new[] { FromHex("#FFFFFF"), FromHex("#56B4E9"), FromHex("#0072B2") },
        ColorScale.Violet => new[] { FromHex("#FFFFFF"), FromHex("#CC79A7"), FromHex("#882255") },
        _ => new[] { FromHex("#FFFF00"), FromHex("#FF8000"), FromHex("#FF0000") } // Classic
    };

    /// <summary>Ramp color for a processed level (0-1). Transparent below 0.005.</summary>
    public static Color At(ColorScale scale, double level)
    {
        if (level < InvisibleBelow)
            return Colors.Transparent;

        var t = Math.Clamp(level, 0.0, 1.0);
        var stops = Stops(scale);

        return t <= 0.5
            ? Lerp(stops[0], stops[1], t / 0.5)
            : Lerp(stops[1], stops[2], (t - 0.5) / 0.5);
    }

    /// <summary>Ramp color with an explicit alpha (0-1) applied.</summary>
    public static Color WithAlpha(ColorScale scale, double level, double alpha)
    {
        var color = At(scale, level);
        color.A = (byte)(Math.Clamp(alpha, 0.0, 1.0) * 255);
        return color;
    }

    /// <summary>The three ramp stops, e.g. for swatch gradients in settings UI.</summary>
    public static Color[] StopsFor(ColorScale scale) => Stops(scale);

    private static Color Lerp(Color a, Color b, double t)
    {
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private static Color FromHex(string hex)
    {
        return (Color)ColorConverter.ConvertFromString(hex)!;
    }
}
