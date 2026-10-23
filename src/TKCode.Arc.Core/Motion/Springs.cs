using Avalonia.Animation.Easings;

namespace TKCode.Arc.Motion;

/// <summary>
/// Physical spring parameters (damped harmonic oscillator). Equivalent to Motion's
/// <c>physical(visualDuration, bounce)</c> helper used throughout Arc.
/// </summary>
public readonly record struct SpringSpec(double Stiffness, double Damping, double Mass = 1.0)
{
    /// <summary>root = 2π / (1.2 · visualDuration); k = root²; ζ = clamp(1 − bounce, 0.05, 1); c = 2ζ√k.</summary>
    public static SpringSpec FromVisual(double visualDuration, double bounce)
    {
        if (visualDuration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(visualDuration));
        }
        var root = 2 * Math.PI / (1.2 * visualDuration);
        var stiffness = root * root;
        var zeta = Math.Clamp(1 - bounce, 0.05, 1);
        return new SpringSpec(stiffness, 2 * zeta * Math.Sqrt(stiffness));
    }

    public static SpringSpec FromPhysical(double stiffness, double damping, double mass = 1.0)
    {
        if (stiffness <= 0 || damping <= 0 || mass <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stiffness), "Spring parameters must be positive.");
        }
        return new SpringSpec(stiffness, damping, mass);
    }

    public double NaturalFrequency => Math.Sqrt(Stiffness / Mass);

    public double DampingRatio => Damping / (2 * Math.Sqrt(Stiffness * Mass));

    /// <summary>Estimates how long the spring needs to settle from a unit displacement at rest.</summary>
    public TimeSpan EstimateSettle(double restDelta = 0.005, double restSpeed = 0.01)
    {
        // Sample the closed form until it stays within the rest thresholds.
        const double dt = 1.0 / 240.0;
        double t = 0;
        double lastOutside = 0;
        while (t < 10)
        {
            var (x, v) = SpringSolver.Evaluate(this, -1, 0, t);
            if (Math.Abs(x) > restDelta || Math.Abs(v) > restSpeed)
            {
                lastOutside = t;
            }
            else if (t - lastOutside > 0.25)
            {
                break;
            }
            t += dt;
        }
        return TimeSpan.FromSeconds(Math.Max(lastOutside, dt));
    }
}

/// <summary>Closed-form solution of the damped harmonic oscillator.</summary>
public static class SpringSolver
{
    /// <summary>
    /// Evaluates displacement from the target and velocity at time <paramref name="t"/> (seconds) for an
    /// initial displacement <paramref name="x0"/> (from − target) and initial velocity <paramref name="v0"/>.
    /// </summary>
    public static (double Displacement, double Velocity) Evaluate(in SpringSpec spec, double x0, double v0, double t)
    {
        var w0 = spec.NaturalFrequency;
        var zeta = spec.DampingRatio;
        if (Math.Abs(zeta - 1) < 1e-6)
        {
            var e = Math.Exp(-w0 * t);
            var bCoef = v0 + w0 * x0;
            var x = e * (x0 + bCoef * t);
            var v = e * (bCoef - w0 * (x0 + bCoef * t));
            return (x, v);
        }
        if (zeta < 1)
        {
            var wd = w0 * Math.Sqrt(1 - zeta * zeta);
            var e = Math.Exp(-zeta * w0 * t);
            var bCoef = (v0 + zeta * w0 * x0) / wd;
            var cos = Math.Cos(wd * t);
            var sin = Math.Sin(wd * t);
            var x = e * (x0 * cos + bCoef * sin);
            var v = e * ((bCoef * wd - zeta * w0 * x0) * cos - (x0 * wd + zeta * w0 * bCoef) * sin);
            return (x, v);
        }
        {
            var wh = w0 * Math.Sqrt(zeta * zeta - 1);
            var e = Math.Exp(-zeta * w0 * t);
            var bCoef = (v0 + zeta * w0 * x0) / wh;
            var cosh = Math.Cosh(wh * t);
            var sinh = Math.Sinh(wh * t);
            var x = e * (x0 * cosh + bCoef * sinh);
            var v = e * ((bCoef * wh - zeta * w0 * x0) * cosh + (x0 * wh - zeta * w0 * bCoef) * sinh);
            return (x, v);
        }
    }
}

/// <summary>Arc spring presets.</summary>
public enum ArcSpring
{
    /// <summary>visualDuration 0.4, bounce 0: motion that reports state (thumbs, panels, progress, values).</summary>
    Smooth,

    /// <summary>visualDuration 0.26, bounce 0.12: presses, small indicators, icon swaps, pop-ins, menus.</summary>
    Snappy,

    /// <summary>visualDuration 0.42, bounce 0.16: shared highlights, width that follows content, shape morphs.</summary>
    Morph,

    /// <summary>stiffness 520, damping 38.</summary>
    Responsive,

    /// <summary>stiffness 340, damping 34.</summary>
    Gentle,

    /// <summary>
    /// visualDuration 0.25, bounce 0: labels and pills whose width follows their text, selection backgrounds that
    /// glide across filters and tabs (fitted to Arc's AI blocks, where these springs settle in about 0.4s).
    /// </summary>
    Quick,

    /// <summary>
    /// visualDuration 0.33, bounce 0: disclosure heights, docked panels and sheets, rows that reflow or reorder
    /// (fitted to Arc's AI blocks; between <see cref="Quick"/> and <see cref="Smooth"/>).
    /// </summary>
    Settle
}

/// <summary>Arc motion presets (values from Arc's motion-tokens.ts).</summary>
public static class ArcMotionPresets
{
    public static readonly SpringSpec Smooth = SpringSpec.FromVisual(0.4, 0);
    public static readonly SpringSpec Snappy = SpringSpec.FromVisual(0.26, 0.12);
    public static readonly SpringSpec Morph = SpringSpec.FromVisual(0.42, 0.16);
    public static readonly SpringSpec Responsive = SpringSpec.FromPhysical(520, 38);
    public static readonly SpringSpec Gentle = SpringSpec.FromPhysical(340, 34);
    public static readonly SpringSpec Quick = SpringSpec.FromVisual(0.25, 0);
    public static readonly SpringSpec Settle = SpringSpec.FromVisual(0.33, 0);

    public static readonly TimeSpan Instant = TimeSpan.FromMilliseconds(120);
    public static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(160);
    public static readonly TimeSpan Exit = TimeSpan.FromMilliseconds(180);
    public static readonly TimeSpan Standard = TimeSpan.FromMilliseconds(240);
    public static readonly TimeSpan Considered = TimeSpan.FromMilliseconds(480);

    /// <summary>Duration paired with <see cref="ArcEasings.CssSpring"/> (<c>--duration-spring</c>).</summary>
    public static readonly TimeSpan CssSpring = TimeSpan.FromMilliseconds(580);

    public static readonly TimeSpan StaggerChar = TimeSpan.FromMilliseconds(16);
    public static readonly TimeSpan StaggerWord = TimeSpan.FromMilliseconds(40);
    public static readonly TimeSpan StaggerLine = TimeSpan.FromMilliseconds(80);
    public static readonly TimeSpan StaggerItem = TimeSpan.FromMilliseconds(35);

    public static SpringSpec Get(ArcSpring spring) => spring switch
    {
        ArcSpring.Smooth => Smooth,
        ArcSpring.Snappy => Snappy,
        ArcSpring.Morph => Morph,
        ArcSpring.Responsive => Responsive,
        ArcSpring.Gentle => Gentle,
        ArcSpring.Quick => Quick,
        ArcSpring.Settle => Settle,
        _ => Smooth
    };
}

/// <summary>Arc cubic-bezier easings and the CSS spring curve.</summary>
public static class ArcEasings
{
    public static readonly Easing Enter = new SplineEasing(0.16, 1, 0.3, 1);
    public static readonly Easing Exit = new SplineEasing(0.7, 0, 0.84, 0);
    public static readonly Easing Standard = new SplineEasing(0.22, 1, 0.36, 1);
    public static readonly Easing InOut = new SplineEasing(0.65, 0, 0.35, 1);

    /// <summary>Arc's <c>--ease-spring</c> linear() curve (bounce 0.15), paired with <c>--duration-spring</c> 580ms.</summary>
    public static readonly Easing CssSpring = new ArcLinearEasing(
    [
        0, 0.0258, 0.09, 0.1763, 0.2732, 0.3724, 0.4683, 0.5573, 0.6376, 0.7082, 0.7689, 0.8202, 0.8628, 0.8976,
        0.9256, 0.9476, 0.9648, 0.9778, 0.9875, 0.9945, 0.9994, 1.0026, 1.0047, 1.0058, 1.0062, 1.0062, 1.0059,
        1.0055, 1.0049, 1.0043, 1.0036, 1.0031, 1.0025, 1.002, 1.0016, 1.0013, 1
    ]);
}

/// <summary>Piecewise linear easing with evenly spaced stops (CSS <c>linear()</c>).</summary>
public sealed class ArcLinearEasing : Easing
{
    private readonly double[] _points;

    public ArcLinearEasing(double[] points)
    {
        if (points.Length < 2)
        {
            throw new ArgumentException("At least two stops are required.", nameof(points));
        }
        _points = points;
    }

    public override double Ease(double progress)
    {
        if (progress <= 0)
        {
            return _points[0];
        }
        if (progress >= 1)
        {
            return _points[^1];
        }
        var scaled = progress * (_points.Length - 1);
        var index = (int)scaled;
        var frac = scaled - index;
        return _points[index] + (_points[index + 1] - _points[index]) * frac;
    }
}

/// <summary>
/// A spring baked into an easing curve over its settle time. Used with Avalonia transitions, which run on a
/// fixed duration: the curve has the spring's shape, but an interrupted transition restarts with zero velocity.
/// Velocity-preserving motion uses <see cref="MotionValue{T}"/>.
/// </summary>
public sealed class ArcSpringEasing : Easing
{
    private readonly SpringSpec _spec;
    private readonly double _settleSeconds;

    public ArcSpringEasing(SpringSpec spec)
    {
        _spec = spec;
        _settleSeconds = spec.EstimateSettle().TotalSeconds;
    }

    public SpringSpec Spec => _spec;

    /// <summary>The duration the transition should use so the curve ends at rest.</summary>
    public TimeSpan SettleDuration => TimeSpan.FromSeconds(_settleSeconds);

    public override double Ease(double progress)
    {
        if (progress <= 0)
        {
            return 0;
        }
        if (progress >= 1)
        {
            return 1;
        }
        var (x, _) = SpringSolver.Evaluate(_spec, -1, 0, progress * _settleSeconds);
        return 1 + x;
    }

    private static readonly Dictionary<ArcSpring, ArcSpringEasing> s_cache = new();

    /// <summary>Returns a cached easing for a preset.</summary>
    public static ArcSpringEasing For(ArcSpring spring)
    {
        lock (s_cache)
        {
            if (!s_cache.TryGetValue(spring, out var easing))
            {
                easing = new ArcSpringEasing(ArcMotionPresets.Get(spring));
                s_cache[spring] = easing;
            }
            return easing;
        }
    }
}
