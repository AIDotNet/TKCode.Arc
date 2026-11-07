namespace TKCode.Arc;

/// <summary>The state of the hole at one moment of a swallow: core radius and the shockwave ring.</summary>
/// <param name="CoreRadius">Radius of the black core (DIP); 0 while the hole is closed.</param>
/// <param name="ShockRadius">Radius of the shockwave ring (DIP).</param>
/// <param name="ShockOpacity">Peak opacity of the shockwave ring; 0 when there is none.</param>
internal readonly record struct BlackHoleHoleState(double CoreRadius, double ShockRadius, double ShockOpacity);

/// <summary>
/// The measured motion of Arc Black hole (design reference), as pure functions of time. Every constant is the value
/// measured at the default 1.3 s duration (milliseconds below); other durations scale all times proportionally.
/// Swallow time runs 0 → duration; a release plays the same curves backwards (<see cref="SwallowTimeForRelease"/>).
/// </summary>
internal static class BlackHoleMath
{
    /// <summary>The duration every measured time below belongs to (ms).</summary>
    public const double ReferenceDuration = 1300;

    /// <summary>The hole opens with easeOutBack from 15 ms over 190 ms.</summary>
    public const double OpenStart = 15;
    public const double OpenLength = 190;

    /// <summary>The core grows about linearly while it eats, from 240 to 1080 ms, by <c>growth</c> (+40 %).</summary>
    public const double GrowStart = 240;
    public const double GrowEnd = 1080;

    /// <summary>Held at its largest until 1110 ms, then collapses with an ease-in over 95 ms.</summary>
    public const double CollapseStart = 1110;
    public const double CollapseLength = 95;

    /// <summary>Exponent of the collapse ease-in (measured between quadratic and cubic).</summary>
    public const double CollapsePower = 2.5;

    /// <summary>The shockwave: ease-out-cubic radius from 1140 ms over 160 ms, opacity peak·(1 − p)².</summary>
    public const double ShockStart = 1140;
    public const double ShockLength = 160;

    /// <summary>Each point of a swallowed item starts falling 110 ms + 1.84 ms per DIP of distance after the start.</summary>
    public const double FallDelay = 110;
    public const double FallStagger = 1.84;

    /// <summary>Every point falls for 610 ms (easeInSine on the radius).</summary>
    public const double FallLength = 610;

    /// <summary>The last point must arrive by 0.9 of the duration (before the collapse).</summary>
    public const double LastArrival = 0.9 * ReferenceDuration;

    /// <summary>Logarithmic spiral pitch: θ = θ0 + k·ln(r0 / r), k = 0.88 rad (clockwise on screen).</summary>
    public const double SpiralPitch = 0.88;

    /// <summary>easeOutBack overshoot constant.</summary>
    public const double BackOvershoot = 1.70158;

    /// <summary>easeOutBack (s = 1.70158): overshoots about 10 % at 0.66, ends at 1.</summary>
    public static double EaseOutBack(double p)
    {
        p = Math.Clamp(p, 0, 1);
        var q = p - 1;
        return 1 + (BackOvershoot + 1) * q * q * q + BackOvershoot * q * q;
    }

    /// <summary>easeOutCubic.</summary>
    public static double EaseOutCubic(double p)
    {
        p = Math.Clamp(p, 0, 1);
        var q = 1 - p;
        return 1 - q * q * q;
    }

    /// <summary>easeInSine.</summary>
    public static double EaseInSine(double p)
    {
        p = Math.Clamp(p, 0, 1);
        return 1 - Math.Cos(p * Math.PI / 2);
    }

    /// <summary>
    /// The hole at swallow time <paramref name="t"/> (ms on the reference 1.3 s scale): opens with an overshoot to
    /// <paramref name="radius"/>, grows linearly by <paramref name="growth"/> while it eats, collapses, then sends out
    /// one shockwave to <paramref name="shockRadius"/> with peak opacity <paramref name="shockPeak"/>.
    /// </summary>
    public static BlackHoleHoleState Hole(double t, double radius, double growth, double shockRadius, double shockPeak)
    {
        double core;
        if (t <= OpenStart)
        {
            core = 0;
        }
        else if (t < OpenStart + OpenLength)
        {
            core = radius * EaseOutBack((t - OpenStart) / OpenLength);
        }
        else if (t < GrowStart)
        {
            core = radius;
        }
        else if (t < GrowEnd)
        {
            core = radius * (1 + growth * (t - GrowStart) / (GrowEnd - GrowStart));
        }
        else if (t < CollapseStart)
        {
            core = radius * (1 + growth);
        }
        else if (t < CollapseStart + CollapseLength)
        {
            var p = (t - CollapseStart) / CollapseLength;
            core = radius * (1 + growth) * (1 - Math.Pow(p, CollapsePower));
        }
        else
        {
            core = 0;
        }
        double ringRadius = 0, ringOpacity = 0;
        if (t > ShockStart && t < ShockStart + ShockLength)
        {
            var p = (t - ShockStart) / ShockLength;
            ringRadius = shockRadius * EaseOutCubic(p);
            ringOpacity = shockPeak * (1 - p) * (1 - p);
        }
        return new BlackHoleHoleState(core, ringRadius, ringOpacity);
    }

    /// <summary>
    /// The stagger (ms per DIP of distance) for items whose farthest point is <paramref name="farthest"/> DIP from the
    /// hole: the measured 1.84, or less when that would land the farthest point after <see cref="LastArrival"/>.
    /// </summary>
    public static double Stagger(double farthest)
    {
        if (farthest <= 0)
        {
            return FallStagger;
        }
        var fit = (LastArrival - FallDelay - FallLength) / farthest;
        return Math.Max(0, Math.Min(FallStagger, fit));
    }

    /// <summary>When a point <paramref name="distance"/> DIP from the hole starts falling (ms, reference scale).</summary>
    public static double FallStart(double distance, double stagger) => FallDelay + stagger * distance;

    /// <summary>The radius factor r / r0 of a point at fall time <paramref name="t"/> that starts at <paramref name="start"/>.</summary>
    public static double FallFactor(double t, double start)
    {
        var p = (t - start) / FallLength;
        if (p <= 0)
        {
            return 1;
        }
        if (p >= 1)
        {
            return 0;
        }
        return Math.Cos(p * Math.PI / 2);
    }

    /// <summary>
    /// Where a point that started at polar (<paramref name="r0"/>, <paramref name="theta0"/>) around the hole is at fall
    /// factor <paramref name="factor"/> (r / r0): on the logarithmic spiral θ = θ0 + k·ln(r0 / r).
    /// </summary>
    public static (double X, double Y) Spiral(double r0, double theta0, double factor)
    {
        if (factor >= 1)
        {
            return (r0 * Math.Cos(theta0), r0 * Math.Sin(theta0));
        }
        if (factor <= 1e-4 || r0 <= 1e-6)
        {
            return (0, 0);
        }
        var r = r0 * factor;
        var theta = theta0 - SpiralPitch * Math.Log(factor);
        return (r * Math.Cos(theta), r * Math.Sin(theta));
    }

    /// <summary>
    /// Point-mass lens: the source offset β for an image at offset (<paramref name="dx"/>, <paramref name="dy"/>) from the
    /// hole with Einstein radius <paramref name="einstein"/>: β = θ − θE²/θ along the same direction (so images inside
    /// the Einstein ring come from the far side, mirrored).
    /// </summary>
    public static (double X, double Y) LensSource(double dx, double dy, double einstein)
    {
        var r2 = dx * dx + dy * dy;
        if (einstein <= 0 || r2 <= 1e-12)
        {
            return (dx, dy);
        }
        var f = 1 - einstein * einstein / r2;
        return (dx * f, dy * f);
    }

    /// <summary>
    /// The swallow time shown at release time <paramref name="releaseTime"/>: the release is the swallow played backwards
    /// over <c>speed × duration</c> (both on the same scale).
    /// </summary>
    public static double SwallowTimeForRelease(double releaseTime, double duration, double speed)
    {
        var length = duration * Math.Max(0.05, speed);
        var p = Math.Clamp(releaseTime / length, 0, 1);
        return duration * (1 - p);
    }
}
