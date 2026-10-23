using Avalonia;
using Avalonia.Controls;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>The plan's motion constants, all measured on the reference (notes section 6).</summary>
internal static class AgentPlanMotion
{
    /// <summary>
    /// The one layout spring of every height and position change: critically damped, ω ≈ 20.1 rad/s (fitted 20.0-20.4).
    /// No <see cref="ArcSpring"/> preset equals it (Quick is 0.25s, Snappy bounces), so the spec is used directly.
    /// </summary>
    public static readonly SpringSpec Layout = AiGlyphMotion.Settle;

    /// <summary>A dropped row settling under the pointer (fitted ω 24.2, ζ 1.05).</summary>
    public static readonly SpringSpec Drop = SpringSpec.FromPhysical(586, 51);

    /// <summary>Swap: the old layer fades out in 60ms, the new one fades in and rises 3px in 145ms on the enter curve.</summary>
    public static readonly TimeSpan SwapExit = TimeSpan.FromMilliseconds(60);

    public static readonly TimeSpan SwapEnter = TimeSpan.FromMilliseconds(145);

    public const double SwapRise = 3;

    /// <summary>Colors, backgrounds and shadows: 160ms on the standard curve; a title's color: 240ms.</summary>
    public static readonly TimeSpan Color = TimeSpan.FromMilliseconds(160);

    public static readonly TimeSpan TitleColor = TimeSpan.FromMilliseconds(240);

    /// <summary>A rolling count: the slot width follows in 240ms; the old number leaves in 100ms, the new one arrives in 210ms.</summary>
    public static readonly TimeSpan CountWidth = TimeSpan.FromMilliseconds(240);

    /// <summary>The spinner turns once every 800ms (450°/s), linearly.</summary>
    public const double SpinTurnSeconds = 0.8;

    /// <summary>The spin in degrees per second.</summary>
    public const double SpinDegreesPerSecond = 450;

    /// <summary>The check draws 45ms after the disc appears, over 200ms on the standard curve.</summary>
    public static readonly TimeSpan CheckDelay = TimeSpan.FromMilliseconds(45);

    public static readonly TimeSpan CheckDraw = TimeSpan.FromMilliseconds(200);

    /// <summary>An inserted step's 8% accent wash holds 640ms, then fades out in 1175ms on the standard curve.</summary>
    public static readonly TimeSpan WashHold = TimeSpan.FromMilliseconds(640);

    public static readonly TimeSpan WashFade = TimeSpan.FromMilliseconds(1175);

    public const double WashAlpha = 0.08;

    /// <summary>Fades: a row entering 160ms, leaving 80ms; an activity line entering 100ms; a block folding away 60ms.</summary>
    public static readonly TimeSpan RowEnter = TimeSpan.FromMilliseconds(160);

    public static readonly TimeSpan RowExit = TimeSpan.FromMilliseconds(80);

    public static readonly TimeSpan LineEnter = TimeSpan.FromMilliseconds(100);

    public static readonly TimeSpan BlockExit = TimeSpan.FromMilliseconds(60);

    public static readonly TimeSpan FailEnter = TimeSpan.FromMilliseconds(125);

    /// <summary>The progress line fades in over 100ms when the plan is approved.</summary>
    public static readonly TimeSpan ProgressFade = TimeSpan.FromMilliseconds(100);

    /// <summary>Streaming: one chunk of 3-5 words every 34ms, each fading in over 120ms; the field takes over 40ms after the last.</summary>
    public static readonly TimeSpan ChunkInterval = TimeSpan.FromMilliseconds(34);

    public static readonly TimeSpan ChunkFade = TimeSpan.FromMilliseconds(120);

    public static readonly TimeSpan StreamSettle = TimeSpan.FromMilliseconds(40);

    /// <summary>True at full motion (travel, spin, draw, wash and chunk fades run).</summary>
    public static bool IsFull(Visual visual) => ArcMotion.GetEffectiveLevel(visual) == ArcMotionLevel.Full;

    /// <summary>True unless motion is off (short opacity fades still run under reduced motion).</summary>
    public static bool Fades(Visual visual) => ArcMotion.GetEffectiveLevel(visual) != ArcMotionLevel.Off;

    /// <summary>A tween on the enter curve, or instant.</summary>
    public static MotionSpec Enter(TimeSpan duration, TimeSpan delay = default) => MotionSpec.Tween(duration, ArcEasings.Enter, delay);

    /// <summary>A tween on the standard curve.</summary>
    public static MotionSpec Standard(TimeSpan duration, TimeSpan delay = default) => MotionSpec.Tween(duration, ArcEasings.Standard, delay);
}

/// <summary>
/// A height that follows a target on the plan's layout spring (code-driven: the spring is
/// <see cref="AgentPlanMotion.Layout"/>, which no ControlTheme transition or SpringSizeDecorator preset expresses, and it must
/// keep its velocity when a new line arrives mid-flight). The first value and every change below full motion jump.
/// </summary>
internal sealed class AgentPlanHeight
{
    private readonly Control _owner;
    private readonly MotionValue<double> _value;
    private bool _started;
    private double _laidOut = double.NaN;

    public AgentPlanHeight(Control owner)
    {
        _owner = owner;
        _value = new MotionValue<double>(owner, 0, MotionVectors.Double);
        _value.Changed += v =>
        {
            // Sub-pixel frames of the spring's tail would relayout the card for nothing.
            var snapped = Math.Round(v * 2) / 2;
            if (snapped != _laidOut || Math.Abs(v - _value.Target) < 0.01)
            {
                _laidOut = snapped;
                _owner.InvalidateMeasure();
            }
        };
    }

    // Attached, not Loaded: a row inserted a moment ago has not raised Loaded yet but must already spring open.
    private bool Attached => TopLevel.GetTopLevel(_owner) is not null;

    /// <summary>The height now.</summary>
    public double Value => Math.Max(0, _value.Value);

    /// <summary>Where it is heading.</summary>
    public double Target => _value.Target;

    /// <summary>True while it moves.</summary>
    public bool IsMoving => _value.IsAnimating;

    /// <summary>Heads to <paramref name="target"/> (springing at full motion once loaded, jumping otherwise).</summary>
    public void To(double target, bool animate = true)
    {
        if (!_started)
        {
            _started = true;
            if (!animate || !Attached)
            {
                _value.Jump(target);
                return;
            }
        }
        if (Math.Abs(_value.Target - target) < 0.01 && (_value.IsAnimating || Math.Abs(_value.Value - target) < 0.01))
        {
            return;
        }
        if (!animate || !Attached || !AgentPlanMotion.IsFull(_owner))
        {
            Jump(target);
            return;
        }
        _ = _value.AnimateTo(target, MotionSpec.Spring(AgentPlanMotion.Layout));
    }

    /// <summary>Starts from <paramref name="from"/> and springs to <paramref name="target"/>.</summary>
    public void From(double from, double target)
    {
        _started = true;
        _value.Jump(from);
        To(target);
    }

    /// <summary>Rests at <paramref name="value"/>.</summary>
    public void Jump(double value)
    {
        _started = true;
        if (!_value.IsAnimating && Math.Abs(_value.Value - value) < 0.01 && Math.Abs(_value.Target - value) < 0.01)
        {
            // Called from measure: an unchanged value must not invalidate the layout again.
            return;
        }
        _value.Jump(value);
    }
}
