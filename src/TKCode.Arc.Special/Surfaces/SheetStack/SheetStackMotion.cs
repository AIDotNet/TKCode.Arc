using Avalonia.Animation.Easings;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The springs and tweens of <see cref="SheetStack"/>, fitted from frame data of Arc Sheet stack (design reference): a
/// new level rises on ω 12 ζ 0.945, levels behind move on ω 12.3 ζ 0.99, heights change on <see cref="ArcSpring.Smooth"/>,
/// closing levels fall on ω 15.3 ζ 1, the scrim fades in over 300 ms (.16,1,.3,1) and out over 240 ms (.22,1,.36,1);
/// dialogs fade in over 195 ms (.22,1,.36,1) and out over 110 ms (ease), scaling from 0.97 and settling from 18 px lower.
/// Reduced motion fades a level in over 210 ms and out over 140 ms (ease).
/// </summary>
internal static class SheetStackMotion
{
    public static readonly SpringSpec Rise = Physical(12, 0.945);
    public static readonly SpringSpec Depth = Physical(12.3, 0.99);
    public static readonly SpringSpec Height = ArcMotionPresets.Smooth;
    public static readonly SpringSpec Exit = Physical(15.3, 1);

    public static readonly Easing CssEase = new SplineEasing(0.25, 0.1, 0.25, 1);
    public static readonly TimeSpan ScrimIn = TimeSpan.FromMilliseconds(300);
    public static readonly TimeSpan ScrimOut = ArcMotionPresets.Standard;
    public static readonly TimeSpan DialogFadeIn = TimeSpan.FromMilliseconds(195);
    public static readonly TimeSpan DialogFadeOut = TimeSpan.FromMilliseconds(110);
    public static readonly TimeSpan DialogDragFadeOut = TimeSpan.FromMilliseconds(120);
    public static readonly TimeSpan ReducedFadeIn = TimeSpan.FromMilliseconds(210);
    public static readonly TimeSpan ReducedFadeOut = TimeSpan.FromMilliseconds(140);
    public const double DialogEnterOffset = 18;
    public const double DialogEnterScale = 0.97;

    private static SpringSpec Physical(double omega, double zeta)
        => SpringSpec.FromPhysical(omega * omega, 2 * zeta * omega);
}

/// <summary>
/// A spring stepped by its owner's frame loop. Each step solves the spring in closed form from the current position and
/// velocity, so a new target (or a target that moves every frame, like the top sheet's edge while its height springs)
/// keeps the velocity: the motion turns around smoothly instead of restarting.
/// </summary>
internal struct SheetStackSpring
{
    public double Value;
    public double Velocity;
    public double Target;
    public SpringSpec Spec;
    public double RestDelta;
    public double RestSpeed;

    public SheetStackSpring(double value, double restDelta, double restSpeed)
    {
        Value = value;
        Target = value;
        Velocity = 0;
        Spec = SheetStackMotion.Depth;
        RestDelta = restDelta;
        RestSpeed = restSpeed;
    }

    public readonly bool IsMoving => Value != Target || Velocity != 0;

    public void Jump(double value)
    {
        Value = value;
        Target = value;
        Velocity = 0;
    }

    public void To(double target, SpringSpec spec)
    {
        Target = target;
        Spec = spec;
    }

    /// <summary>Advances by <paramref name="dt"/> seconds; returns true while still moving.</summary>
    public bool Step(double dt)
    {
        if (!IsMoving)
        {
            return false;
        }
        var (x, v) = SpringSolver.Evaluate(Spec, Value - Target, Velocity, dt);
        if (Math.Abs(x) < RestDelta && Math.Abs(v) < RestSpeed)
        {
            Value = Target;
            Velocity = 0;
            return false;
        }
        Value = Target + x;
        Velocity = v;
        return true;
    }
}

/// <summary>A tween stepped by its owner's frame loop (scrim and fades).</summary>
internal struct SheetStackTween
{
    public double Value;
    private double _from;
    private double _to;
    private double _elapsed;
    private double _duration;
    private Easing? _easing;

    public SheetStackTween(double value)
    {
        Value = value;
        _from = value;
        _to = value;
        _elapsed = 0;
        _duration = 0;
        _easing = null;
    }

    public readonly double Target => _to;

    public readonly bool IsMoving => _easing is not null;

    public void Jump(double value)
    {
        Value = value;
        _from = value;
        _to = value;
        _easing = null;
    }

    public void To(double target, TimeSpan duration, Easing easing)
    {
        if (target == _to && (_easing is not null || Value == target))
        {
            return;
        }
        _from = Value;
        _to = target;
        _elapsed = 0;
        _duration = Math.Max(duration.TotalSeconds, 1e-6);
        _easing = easing;
        if (_from == _to)
        {
            _easing = null;
        }
    }

    public bool Step(double dt)
    {
        if (_easing is null)
        {
            return false;
        }
        _elapsed += dt;
        var p = Math.Min(1, _elapsed / _duration);
        Value = _from + (_to - _from) * _easing.Ease(p);
        if (p >= 1)
        {
            Value = _to;
            _easing = null;
            return false;
        }
        return true;
    }
}
