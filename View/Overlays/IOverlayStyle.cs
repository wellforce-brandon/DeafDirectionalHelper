using System.Windows;
using System.Windows.Controls;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View.Overlays;

/// <summary>
/// One frame of display-ready audio levels, produced by the 100 ms UI tick
/// (plan D6/§1.5). Channel order: 0 FL, 1 FR, 2 C, 3 LFE, 4 RL, 5 RR, 6 SL, 7 SR.
/// </summary>
public sealed class LevelFrame
{
    public readonly double[] Levels = new double[8]; // smoothed display levels
    public readonly double[] Trails = new double[8]; // decaying peak trails
    public double LeftActivity;
    public double RightActivity;
    public bool AnyActive;
}

/// <summary>
/// A single overlay style renderer. Attach adds cached shapes to the host
/// canvas once; ApplyLayout positions them for the current settings/work area;
/// Render only mutates existing elements (no per-frame allocation).
/// </summary>
public interface IOverlayStyle
{
    void Attach(Canvas canvas);
    void Detach(Canvas canvas);
    void ApplyLayout(AppSettings settings, Rect workArea);
    void Render(LevelFrame frame, BarSettings bars);
}
