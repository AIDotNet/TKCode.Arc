using Avalonia;
using Avalonia.Animation;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Motion;

/// <summary>Shared spring configuration for the declarative spring transitions.</summary>
internal static class SpringTransitionConfig
{
    public static void Apply(TransitionBase transition, ArcSpring spring, double visualDuration, double bounce)
    {
        var easing = visualDuration > 0
            ? new ArcSpringEasing(SpringSpec.FromVisual(visualDuration, bounce))
            : ArcSpringEasing.For(spring);
        transition.Easing = easing;
        transition.Duration = easing.SettleDuration;
    }
}

/// <summary>
/// Declares a spring in a ControlTheme. The spring shape is baked into the easing over its settle time
/// (Avalonia transitions run on a fixed duration and restart from the current value with zero velocity).
/// </summary>
public interface ISpringTransition
{
    ArcSpring Spring { get; set; }

    /// <summary>Custom visual duration in seconds; when greater than zero it overrides <see cref="Spring"/>.</summary>
    double VisualDuration { get; set; }

    double Bounce { get; set; }
}

#pragma warning disable CS1591
public class SpringDoubleTransition : DoubleTransition, ISpringTransition
{
    private ArcSpring _spring = ArcSpring.Smooth;
    private double _visualDuration;
    private double _bounce;

    public SpringDoubleTransition() => SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce);

    public ArcSpring Spring { get => _spring; set { _spring = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
    public double VisualDuration { get => _visualDuration; set { _visualDuration = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
    public double Bounce { get => _bounce; set { _bounce = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
}

public class SpringTransformOperationsTransition : TransformOperationsTransition, ISpringTransition
{
    private ArcSpring _spring = ArcSpring.Snappy;
    private double _visualDuration;
    private double _bounce;

    public SpringTransformOperationsTransition() => SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce);

    public ArcSpring Spring { get => _spring; set { _spring = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
    public double VisualDuration { get => _visualDuration; set { _visualDuration = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
    public double Bounce { get => _bounce; set { _bounce = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
}

public class SpringPointTransition : PointTransition, ISpringTransition
{
    private ArcSpring _spring = ArcSpring.Morph;
    private double _visualDuration;
    private double _bounce;

    public SpringPointTransition() => SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce);

    public ArcSpring Spring { get => _spring; set { _spring = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
    public double VisualDuration { get => _visualDuration; set { _visualDuration = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
    public double Bounce { get => _bounce; set { _bounce = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
}

public class SpringSizeTransition : SizeTransition, ISpringTransition
{
    private ArcSpring _spring = ArcSpring.Morph;
    private double _visualDuration;
    private double _bounce;

    public SpringSizeTransition() => SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce);

    public ArcSpring Spring { get => _spring; set { _spring = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
    public double VisualDuration { get => _visualDuration; set { _visualDuration = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
    public double Bounce { get => _bounce; set { _bounce = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
}

public class SpringThicknessTransition : ThicknessTransition, ISpringTransition
{
    private ArcSpring _spring = ArcSpring.Smooth;
    private double _visualDuration;
    private double _bounce;

    public SpringThicknessTransition() => SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce);

    public ArcSpring Spring { get => _spring; set { _spring = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
    public double VisualDuration { get => _visualDuration; set { _visualDuration = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
    public double Bounce { get => _bounce; set { _bounce = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
}

public class SpringCornerRadiusTransition : CornerRadiusTransition, ISpringTransition
{
    private ArcSpring _spring = ArcSpring.Morph;
    private double _visualDuration;
    private double _bounce;

    public SpringCornerRadiusTransition() => SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce);

    public ArcSpring Spring { get => _spring; set { _spring = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
    public double VisualDuration { get => _visualDuration; set { _visualDuration = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
    public double Bounce { get => _bounce; set { _bounce = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
}

public class SpringVectorTransition : VectorTransition, ISpringTransition
{
    private ArcSpring _spring = ArcSpring.Smooth;
    private double _visualDuration;
    private double _bounce;

    public SpringVectorTransition() => SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce);

    public ArcSpring Spring { get => _spring; set { _spring = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
    public double VisualDuration { get => _visualDuration; set { _visualDuration = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
    public double Bounce { get => _bounce; set { _bounce = value; SpringTransitionConfig.Apply(this, _spring, _visualDuration, _bounce); } }
}
#pragma warning restore CS1591

/// <summary>Tween presets for color and opacity transitions declared in ControlThemes.</summary>
public enum ArcTween
{
    Instant,
    Fast,
    Exit,
    Standard,
    Considered,

    /// <summary>Standard duration (240ms) on the enter curve: reveals such as images fading in from a blur.</summary>
    Enter,

    /// <summary>Arc's CSS spring: <c>var(--duration-spring) var(--ease-spring)</c> (580ms linear() curve, bounce 0.15). Press releases, hover lifts.</summary>
    Spring
}

/// <summary>
/// A brush transition preconfigured with an Arc tween (default: fast + standard ease). Solid colors mix with premultiplied
/// alpha, so a transparent end contributes no color: <c>Transparent</c> is transparent white, and Avalonia's
/// <see cref="BrushTransition"/> (a plain ARGB mix) passes through a translucent light gray on the way from or to it, which
/// reads as a flash on dark surfaces (a ghost button's hover). Other brushes switch halfway, as BrushTransition does.
/// </summary>
public class ArcBrushTransition : InterpolatingTransitionBase<IBrush?>
{
    private ArcTween _tween = ArcTween.Fast;

    public ArcBrushTransition() => ArcTweenConfig.Apply(this, _tween);

    public ArcTween Tween { get => _tween; set { _tween = value; ArcTweenConfig.Apply(this, _tween); } }

    protected override IBrush? Interpolate(double progress, IBrush? from, IBrush? to)
    {
        if (progress >= 1)
        {
            return to;
        }
        if (from is not (ISolidColorBrush or null) || to is not (ISolidColorBrush or null) || (from is null && to is null))
        {
            return progress < 0.5 ? from : to;
        }
        return new ImmutableSolidColorBrush(MixPremultiplied(Effective(from), Effective(to), Math.Max(progress, 0)));
    }

    private static Color Effective(IBrush? brush) => brush is ISolidColorBrush solid
        ? Color.FromArgb((byte)Math.Round(solid.Color.A * Math.Clamp(solid.Opacity, 0, 1)), solid.Color.R, solid.Color.G, solid.Color.B)
        : Colors.Transparent;

    /// <summary>
    /// Mixes two colors with premultiplied alpha: a transparent end keeps the other end's hue, so every intermediate color
    /// lies between the two as they appear on screen.
    /// </summary>
    public static Color MixPremultiplied(Color from, Color to, double progress)
    {
        var a1 = from.A / 255.0;
        var a2 = to.A / 255.0;
        var a = a1 + (a2 - a1) * progress;
        if (a <= 0)
        {
            return Color.FromArgb(0, to.R, to.G, to.B);
        }
        byte Channel(byte c1, byte c2) =>
            (byte)Math.Clamp(Math.Round((c1 * a1 * (1 - progress) + c2 * a2 * progress) / a), 0, 255);
        return Color.FromArgb((byte)Math.Round(a * 255), Channel(from.R, to.R), Channel(from.G, to.G), Channel(from.B, to.B));
    }
}

/// <summary>A double (opacity) transition preconfigured with an Arc tween.</summary>
public class ArcDoubleTransition : DoubleTransition
{
    private ArcTween _tween = ArcTween.Fast;

    public ArcDoubleTransition() => ArcTweenConfig.Apply(this, _tween);

    public ArcTween Tween { get => _tween; set { _tween = value; ArcTweenConfig.Apply(this, _tween); } }
}

/// <summary>A box shadows transition preconfigured with an Arc tween.</summary>
public class ArcBoxShadowsTransition : BoxShadowsTransition
{
    private ArcTween _tween = ArcTween.Fast;

    public ArcBoxShadowsTransition() => ArcTweenConfig.Apply(this, _tween);

    public ArcTween Tween { get => _tween; set { _tween = value; ArcTweenConfig.Apply(this, _tween); } }
}

/// <summary>A color transition preconfigured with an Arc tween.</summary>
public class ArcColorTransition : ColorTransition
{
    private ArcTween _tween = ArcTween.Fast;

    public ArcColorTransition() => ArcTweenConfig.Apply(this, _tween);

    public ArcTween Tween { get => _tween; set { _tween = value; ArcTweenConfig.Apply(this, _tween); } }
}

/// <summary>An effect (blur) transition preconfigured with an Arc tween.</summary>
public class ArcEffectTransition : EffectTransition
{
    private ArcTween _tween = ArcTween.Fast;

    public ArcEffectTransition() => ArcTweenConfig.Apply(this, _tween);

    public ArcTween Tween { get => _tween; set { _tween = value; ArcTweenConfig.Apply(this, _tween); } }
}

/// <summary>A thickness transition preconfigured with an Arc tween.</summary>
public class ArcThicknessTransition : ThicknessTransition
{
    private ArcTween _tween = ArcTween.Fast;

    public ArcThicknessTransition() => ArcTweenConfig.Apply(this, _tween);

    public ArcTween Tween { get => _tween; set { _tween = value; ArcTweenConfig.Apply(this, _tween); } }
}

/// <summary>A transform transition preconfigured with an Arc tween (rotations, non-spring moves).</summary>
public class ArcTransformOperationsTransition : TransformOperationsTransition
{
    private ArcTween _tween = ArcTween.Fast;

    public ArcTransformOperationsTransition() => ArcTweenConfig.Apply(this, _tween);

    public ArcTween Tween { get => _tween; set { _tween = value; ArcTweenConfig.Apply(this, _tween); } }
}

internal static class ArcTweenConfig
{
    public static void Apply(TransitionBase transition, ArcTween tween)
    {
        transition.Duration = tween switch
        {
            ArcTween.Instant => ArcMotionPresets.Instant,
            ArcTween.Fast => ArcMotionPresets.Fast,
            ArcTween.Exit => ArcMotionPresets.Exit,
            ArcTween.Standard => ArcMotionPresets.Standard,
            ArcTween.Considered => ArcMotionPresets.Considered,
            ArcTween.Enter => ArcMotionPresets.Standard,
            ArcTween.Spring => ArcMotionPresets.CssSpring,
            _ => ArcMotionPresets.Fast
        };
        transition.Easing = tween switch
        {
            ArcTween.Exit => ArcEasings.Exit,
            ArcTween.Considered or ArcTween.Enter => ArcEasings.Enter,
            ArcTween.Spring => ArcEasings.CssSpring,
            _ => ArcEasings.Standard
        };
    }
}
