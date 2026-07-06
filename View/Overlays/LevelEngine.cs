using System;
using DeafDirectionalHelper.Audio;
using DeafDirectionalHelper.Settings;

namespace DeafDirectionalHelper.View.Overlays;

/// <summary>
/// The 33 ms UI smoothing tick (plan D6/§1.5): eases raw 50 ms poll values
/// into display levels and maintains decaying peak trails. Meter ballistics
/// are asymmetric — rising levels attack fast so new sounds register almost
/// instantly, falling levels release at the original smooth rate (0.45 per
/// 100 ms, rescaled to the 33 ms tick).
///   disp += (target - disp) × (rising ? 0.75 : 0.18), snap to 0 below 0.005
///   trail = max(trail × 0.976, disp), floor 0.01
/// </summary>
public sealed class LevelEngine
{
    private const double AttackEasing = 0.75;
    private const double ReleaseEasing = 0.18;
    private const double TrailDecay = 0.976;
    private const double TrailFloor = 0.01;
    private const double SnapBelow = 0.005;

    private readonly Speakers _speakers;
    private readonly double[] _raw = new double[8];
    private bool _balancedLatch;

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

            var easing = target > Frame.Levels[i] ? AttackEasing : ReleaseEasing;
            var disp = Frame.Levels[i] + (target - Frame.Levels[i]) * easing;
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

    private bool IsBalanced(double left, double right)
    {
        // Hysteresis: engage when sides are within enterRatio, but once engaged
        // stay engaged until they diverge past exitRatio. Without this the
        // decision flickers tick-to-tick and self sounds bleed through.
        const double minLevelToFilter = 0.15;
        const double enterRatio = 0.25;
        const double exitRatio = 0.40;

        var dominant = Math.Max(left, right);
        if (dominant < minLevelToFilter)
        {
            _balancedLatch = false;
            return false;
        }

        var difference = Math.Abs(left - right) / dominant;
        _balancedLatch = difference < (_balancedLatch ? exitRatio : enterRatio);
        return _balancedLatch;
    }
}
