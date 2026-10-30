using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The timing of <see cref="TicketRouter"/>: about two seconds per ticket at speed 1 (arrive 220ms, classify, labels with a
/// 50ms stagger, the 240ms connector pulse, then the row's flight and landing), the bounce-free 0.24s height spring of
/// rows entering and leaving, the ticket crossfade (out 100ms, in 160ms with a small rise) and the travel spring.
/// </summary>
internal static class TicketRouterMotion
{
    /// <summary>The ticket card crossfades in.</summary>
    public const double Arrive = 0.22;

    /// <summary>The simulated classifier's reading time.</summary>
    public const double Classify = 0.7;

    /// <summary>The labels fade in (4 × 50ms stagger + 160ms) and hold before the pulse.</summary>
    public const double Labeled = 0.56;

    /// <summary>The pulse draws along the connector.</summary>
    public const double Pulse = 0.24;

    /// <summary>The row flies into its lane.</summary>
    public const double Landing = 0.36;

    /// <summary>The labels' stagger.</summary>
    public static readonly TimeSpan Stagger = TimeSpan.FromMilliseconds(50);

    /// <summary>A label's fade in.</summary>
    public static readonly TimeSpan LabelFade = TimeSpan.FromMilliseconds(160);

    /// <summary>The ticket card fades out (the swap's first half).</summary>
    public static readonly TimeSpan SwapOut = TimeSpan.FromMilliseconds(100);

    /// <summary>The ticket card fades in with a small rise (the swap's second half).</summary>
    public static readonly TimeSpan SwapIn = TimeSpan.FromMilliseconds(160);

    /// <summary>The rise of the ticket card and the labels, in pixels.</summary>
    public const double Rise = 4;

    /// <summary>Rows enter and leave by animating their measured height on this spring (visual duration 0.24s, no bounce).</summary>
    public static readonly SpringSpec RowHeight = SpringSpec.FromVisual(0.24, 0);

    /// <summary>The position spring a ticket travels on into its lane (interpretation: 0.32s, no bounce).</summary>
    public static readonly SpringSpec Travel = SpringSpec.FromVisual(0.32, 0);

    /// <summary>The landing highlight: held a moment, then fades over this long.</summary>
    public static readonly TimeSpan HighlightFade = TimeSpan.FromMilliseconds(900);

    public static readonly TimeSpan HighlightHold = TimeSpan.FromMilliseconds(240);
}

/// <summary>
/// The fade (and, with full motion, the small rise) of one part of the stage: a label of the classifier strip or the
/// ticket card. Code-driven because each part starts after its own stagger delay from a phase change of the flow, which a
/// ControlTheme transition cannot sequence. Reduced motion keeps the fade and drops the rise; Off shows the end state.
/// </summary>
internal sealed class TicketRouterFade
{
    private readonly Control _target;
    private readonly TranslateTransform _shift = new();
    private readonly MotionValue<double> _opacity;
    private readonly MotionValue<double> _rise;

    public TicketRouterFade(Control target, bool visible)
    {
        _target = target;
        _target.RenderTransform = _shift;
        _opacity = new MotionValue<double>(target, visible ? 1 : 0, MotionVectors.Double);
        _opacity.Changed += value => _target.Opacity = Math.Clamp(value, 0, 1);
        _rise = new MotionValue<double>(target, 0, MotionVectors.Double);
        _rise.Changed += value => _shift.Y = value;
        _target.Opacity = visible ? 1 : 0;
    }

    /// <summary>The opacity drawn now.</summary>
    public double Opacity => _opacity.Value;

    /// <summary>Fades in over <paramref name="duration"/> after <paramref name="delay"/>, rising <paramref name="rise"/> pixels with full motion.</summary>
    public Task<bool> In(TimeSpan duration, TimeSpan delay, double rise)
    {
        if (rise > 0 && ArcMotion.IsFull(_target))
        {
            _rise.Jump(rise);
            _shift.Y = rise;
            _ = _rise.AnimateTo(0, MotionSpec.Tween(duration, ArcEasings.Enter, delay));
        }
        else
        {
            _rise.Jump(0);
            _shift.Y = 0;
        }
        return _opacity.AnimateTo(1, MotionSpec.Tween(duration, ArcEasings.Standard, delay));
    }

    /// <summary>Fades out over <paramref name="duration"/>.</summary>
    public Task<bool> Out(TimeSpan duration) => _opacity.AnimateTo(0, MotionSpec.Tween(duration, ArcEasings.Standard));

    /// <summary>Shows or hides at once.</summary>
    public void Set(bool visible)
    {
        _opacity.Jump(visible ? 1 : 0);
        _rise.Jump(0);
        _target.Opacity = visible ? 1 : 0;
        _shift.Y = 0;
    }
}
