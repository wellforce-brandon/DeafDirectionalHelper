using System;
using DeafDirectionalHelper.Audio;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View.Overlays;

/// <summary>
/// The 100 ms UI smoothing tick (plan D6/§1.5): eases raw 200 ms poll values
/// into display levels and maintains decaying peak trails.
///   disp += (target - disp) × 0.45, snap to 0 below 0.005
///   trail = max(trail × 0.93, disp), floor 0.01
/// </summary>
public sealed class LevelEngine
{
    private const double Easing = 0.45;
    private const double TrailDecay = 0.93;
    private const double TrailFloor = 0.01;
    private const double SnapBelow = 0.005;

    private readonly Speakers _speakers;
    private readonly double[] _raw = new double[8];

    public LevelFrame Frame { get; } = new();

    public LevelEngine(Speakers speakers)
    {
        _speakers = speakers;
    }

    public void Tick(BarSettings bars)
    {
        _raw[0] = _speakers.Speaker1.Value;
        _raw[1] = _speakers.Speaker2.Value;
        _raw[2] = _speakers.Speaker3.Value;
        _raw[3] = _speakers.Speaker4.Value;
        _raw[4] = _speakers.Speaker5.Value;
        _raw[5] = _speakers.Speaker6.Value;
        _raw[6] = _speakers.Speaker7.Value;
        _raw[7] = _speakers.Speaker8.Value;

        // Balanced-sound filter (existing semantics): if the directional sides
        // are loud and near-equal, it's probably the player's own sound.
        var left = Math.Max(Process(_raw[0], bars), Math.Max(Process(_raw[4], bars), Process(_raw[6], bars)));
        var right = Math.Max(Process(_raw[1], bars), Math.Max(Process(_raw[5], bars), Process(_raw[7], bars)));
        var filtered = bars.IgnoreBalancedSounds && IsBalanced(left, right);

        var any = false;
        for (int i = 0; i < 8; i++)
        {
            var target = filtered ? 0.0 : Process(_raw[i], bars);

            var disp = Frame.Levels[i] + (target - Frame.Levels[i]) * Easing;
            if (disp < SnapBelow && target < SnapBelow)
                disp = 0;
            Frame.Levels[i] = disp;

            Frame.Trails[i] = Math.Max(Frame.Trails[i] * TrailDecay, disp);
            if (Frame.Trails[i] < TrailFloor)
                Frame.Trails[i] = TrailFloor;

            if (disp > SnapBelow) any = true;
        }

        Frame.LeftActivity = filtered ? 0 : Math.Max(Frame.Levels[0], Math.Max(Frame.Levels[4], Frame.Levels[6]));
        Frame.RightActivity = filtered ? 0 : Math.Max(Frame.Levels[1], Math.Max(Frame.Levels[5], Frame.Levels[7]));
        Frame.AnyActive = any;
    }

    private static double Process(double raw, BarSettings bars)
    {
        if (raw < bars.MinThreshold) return 0;
        return Math.Min(1.0, raw * bars.Sensitivity);
    }

    private static bool IsBalanced(double left, double right)
    {
        const double minLevelToFilter = 0.15;
        const double maxDifferenceRatio = 0.12;

        var dominant = Math.Max(left, right);
        if (dominant < minLevelToFilter) return false;
        return Math.Abs(left - right) < dominant * maxDifferenceRatio;
    }
}
