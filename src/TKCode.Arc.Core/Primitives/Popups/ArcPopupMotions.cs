using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;

namespace TKCode.Arc.Primitives;

/// <summary>
/// One half (enter or exit) of an <see cref="ArcPopup"/>'s motion, played on the popup's <see cref="ArcPopupFrame"/> so
/// the floating shadow moves with the surface. The motion runs between the resting pose (opacity 1, no transform) and
/// a hidden pose: <see cref="Offset"/> of travel and <see cref="Scale"/> around <see cref="Origin"/>, faded out when
/// <see cref="Fades"/>. An enter starts at the hidden pose and settles at rest; an exit leaves rest for the hidden pose.
/// Opacity and transform may run on different curves (<see cref="OpacitySpec"/>, <see cref="TransformSpec"/>).
/// </summary>
public abstract class ArcPopupMotion
{
    /// <summary>Transform origin, relative to the surface.</summary>
    public RelativePoint Origin { get; set; } = new(0.5, 0, RelativeUnit.Relative);

    /// <summary>Translation of the hidden pose (toward the anchor).</summary>
    public Vector Offset { get; set; }

    /// <summary>Uniform scale of the hidden pose.</summary>
    public double Scale { get; protected set; } = 1;

    /// <summary>When true only the opacity animates (reduced motion, warm-up opens).</summary>
    public bool FadeOnly { get; set; }

    /// <summary>When true the hidden pose is transparent; when false the surface keeps its opacity (a sheet slides).</summary>
    public bool Fades { get; set; } = true;

    /// <summary>Timing of the opacity.</summary>
    public abstract MotionSpec OpacitySpec { get; }

    /// <summary>Timing of the travel and scale.</summary>
    public abstract MotionSpec TransformSpec { get; }
}

/// <summary>
/// P10 enter: origin-aware scale + offset + fade. Default timing is Arc's popover / tooltip enter (fast, ease enter).
/// </summary>
public sealed class ArcPopupEnterMotion : ArcPopupMotion
{
    private readonly MotionSpec _spec;

    public ArcPopupEnterMotion(RelativePoint origin, Vector offset, double scale = 0.97, bool fadeOnly = false,
                               TimeSpan? duration = null, Easing? easing = null)
    {
        Origin = origin;
        Offset = offset;
        Scale = scale;
        FadeOnly = fadeOnly;
        _spec = MotionSpec.Tween(duration ?? ArcMotionPresets.Fast, easing ?? ArcEasings.Enter);
    }

    /// <inheritdoc />
    public override MotionSpec OpacitySpec => _spec;

    /// <inheritdoc />
    public override MotionSpec TransformSpec => _spec;
}

/// <summary>P10 exit: shorter than the enter, fades and shrinks slightly toward the origin.</summary>
public sealed class ArcPopupExitMotion : ArcPopupMotion
{
    private readonly MotionSpec _spec;

    public ArcPopupExitMotion(RelativePoint origin, double scale = 0.98, bool fadeOnly = false, TimeSpan? duration = null)
    {
        Origin = origin;
        Scale = scale;
        FadeOnly = fadeOnly;
        _spec = MotionSpec.Tween(duration ?? ArcMotionPresets.Instant, ArcEasings.Standard);
    }

    /// <inheritdoc />
    public override MotionSpec OpacitySpec => _spec;

    /// <inheritdoc />
    public override MotionSpec TransformSpec => _spec;
}
