using TKCode.Arc.Motion;
using AtomUI.MotionScene;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;

namespace TKCode.Arc.Primitives;

/// <summary>
/// P10 enter: origin-aware scale + offset + fade, played on AtomUI's popup motion actor so the frame shadow
/// moves with the surface. Default timing is Arc's popover / tooltip enter (fast, ease enter).
/// </summary>
public sealed class ArcPopupEnterMotion : AbstractMotion
{
    private readonly RelativePoint _origin;
    private readonly Vector _offset;
    private readonly double _scale;
    private readonly bool _fadeOnly;

    public ArcPopupEnterMotion(RelativePoint origin, Vector offset, double scale = 0.97, bool fadeOnly = false,
                               TimeSpan? duration = null, Easing? easing = null)
        : base(duration ?? ArcMotionPresets.Fast, easing ?? ArcEasings.Enter, FillMode.Forward)
    {
        _origin = origin;
        _offset = offset;
        _scale = scale;
        _fadeOnly = fadeOnly;
    }

    protected override void ConfigureTransitions()
    {
        base.ConfigureTransitions();
        RenderTransformOrigin = _origin;
    }

    protected override void ConfigureMotionStartValue(BaseMotionActor actor)
    {
        actor.Opacity = 0.0;
        actor.MotionTransform = _fadeOnly ? null : BuildTranslateScaleAndTransform(_scale, _scale, _offset.X, _offset.Y);
    }

    protected override void ConfigureMotionEndValue(BaseMotionActor actor)
    {
        actor.Opacity = 1.0;
        actor.MotionTransform = _fadeOnly ? null : BuildTranslateScaleAndTransform(1, 1, 0, 0);
    }

    protected override void NotifyCompleted(BaseMotionActor actor)
    {
        actor.Opacity = 1.0;
        actor.MotionTransform = null;
    }
}

/// <summary>P10 exit: shorter than the enter, fades and shrinks slightly toward the origin.</summary>
public sealed class ArcPopupExitMotion : AbstractMotion
{
    private readonly RelativePoint _origin;
    private readonly double _scale;
    private readonly bool _fadeOnly;

    public ArcPopupExitMotion(RelativePoint origin, double scale = 0.98, bool fadeOnly = false, TimeSpan? duration = null)
        : base(duration ?? ArcMotionPresets.Instant, ArcEasings.Standard, FillMode.Forward)
    {
        _origin = origin;
        _scale = scale;
        _fadeOnly = fadeOnly;
    }

    protected override void ConfigureTransitions()
    {
        base.ConfigureTransitions();
        RenderTransformOrigin = _origin;
    }

    protected override void ConfigureMotionStartValue(BaseMotionActor actor)
    {
        actor.Opacity = 1.0;
        actor.MotionTransform = _fadeOnly ? null : BuildScaleTransform(1.0);
    }

    protected override void ConfigureMotionEndValue(BaseMotionActor actor)
    {
        actor.Opacity = 0.0;
        actor.MotionTransform = _fadeOnly ? null : BuildScaleTransform(_scale);
    }

    protected override void NotifyCompleted(BaseMotionActor actor)
    {
        actor.Opacity = 1.0;
        actor.MotionTransform = null;
    }
}
