using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Spring presets measured on the Showpieces (Arc design reference, fitted from frame captures; see the block specs in
/// <c>design/components/blocks/showpieces/</c>). Core <c>Springs.cs</c> is not touched: every preset is a
/// <see cref="SpringSpec"/> built with <see cref="SpringSpec.FromPhysical"/>. Use them with
/// <see cref="MotionValue{T}"/> (<c>MotionSpec.Spring(ShowpieceSprings.X)</c>) or with
/// <see cref="ShowpieceSpringChannel"/> inside the cameras.
/// </summary>
internal static class ShowpieceSprings
{
    /// <summary>A critically damped spring with natural frequency <paramref name="omega"/> rad/s (k = ω², c = 2ω).</summary>
    public static SpringSpec Critical(double omega)
    {
        if (!(omega > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(omega));
        }
        return SpringSpec.FromPhysical(omega * omega, 2 * omega);
    }

    // Shared cameras -----------------------------------------------------------------------------------------------

    /// <summary>Semantic zoom's render camera chasing its target: critical ω 25 (k 625, c 50); 95% in about 180ms.</summary>
    public static readonly SpringSpec CameraFollow = Critical(25);

    /// <summary>Exploded view's scroll progress smoothing: k 330, c 60 (ζ ≈ 1.65, overdamped; the measured fit).</summary>
    public static readonly SpringSpec ScrollFollow = SpringSpec.FromPhysical(330, 60);

    /// <summary>Customer galaxy wheel / button / key zoom: critical ω 10.3 (98% in 0.5–0.6s, = FromVisual(0.51, 0)).</summary>
    public static readonly SpringSpec ZoomFollow = Critical(10.3);

    /// <summary>Customer galaxy flights (double click, list row, arrows, Show all, Fit): critical ω 5.7 (98% in about 1s).</summary>
    public static readonly SpringSpec FlyFollow = Critical(5.7);

    /// <summary>Customer galaxy recentering after the wheel stops (bounds applied after the gesture): critical ω 5.</summary>
    public static readonly SpringSpec Recenter = Critical(5);

    // Orbit cameras (ActivityTerrain) ----------------------------------------------------------------------------

    /// <summary>Terrain click-to-fly (target and zoom share it): k 21.7, c 9.24 (ζ ≈ 0.99); 50/90/99% at 358/825/1400ms.</summary>
    public static readonly SpringSpec OrbitFlyIn = SpringSpec.FromPhysical(21.7, 9.24);

    /// <summary>Terrain Escape / empty click back to the whole week: k 34.8, c 11.9.</summary>
    public static readonly SpringSpec OrbitExit = SpringSpec.FromPhysical(34.8, 11.9);

    /// <summary>Terrain Reset view (yaw, pitch, zoom, target together): k 31.2, c 11.3.</summary>
    public static readonly SpringSpec OrbitReset = SpringSpec.FromPhysical(31.2, 11.3);

    /// <summary>Terrain perspective ↔ top view: k 28.3, c 10.8.</summary>
    public static readonly SpringSpec OrbitTopView = SpringSpec.FromPhysical(28.3, 10.8);

    /// <summary>Terrain keyboard orbit steps (Shift + arrows): k 30, c 11 (the spec's "k≈30 / c≈11" family).</summary>
    public static readonly SpringSpec OrbitStep = SpringSpec.FromPhysical(30, 11);

    /// <summary>Terrain Ctrl + wheel zoom: k 223, c 30.7 (≈ 470ms to rest).</summary>
    public static readonly SpringSpec OrbitZoom = SpringSpec.FromPhysical(223, 30.7);

    /// <summary>Terrain pitch back inside 10–84° after an overdrag: k 121.5, c 23.5 (fit 110–133 / 22–25).</summary>
    public static readonly SpringSpec PitchReturn = SpringSpec.FromPhysical(121.5, 23.5);

    /// <summary>Terrain hover card position: k 138, c 20.6 (ζ ≈ 0.88, 0.3% overshoot).</summary>
    public static readonly SpringSpec CardFollow = SpringSpec.FromPhysical(138, 20.6);

    // Sphere camera (RevenueGlobe) -------------------------------------------------------------------------------

    /// <summary>Globe flight progress: k 38.4, c 12.4 (ω ≈ 6.2, critical).</summary>
    public static readonly SpringSpec GlobeFly = SpringSpec.FromPhysical(38.4, 12.4);

    /// <summary>Globe pitch always pulled home to 22°: critical ω 3 (about 2.5s).</summary>
    public static readonly SpringSpec PitchHome = Critical(3);

    /// <summary>Globe zoom back to ×1 when a selection clears: critical ω 8 (≈ 0.5s to 90%).</summary>
    public static readonly SpringSpec ZoomClear = Critical(8);

    // Layout morph -------------------------------------------------------------------------------------------------

    /// <summary>Layout morph per-card progress: k 100, c 20 (critical ω 10, ≈ 1.15s to rest).</summary>
    public static readonly SpringSpec MorphProgress = SpringSpec.FromPhysical(100, 20);

    /// <summary>Layout morph card opening to its detail: k 130, c 26.</summary>
    public static readonly SpringSpec MorphOpen = SpringSpec.FromPhysical(130, 26);

    /// <summary>Layout morph hover lift (<c>--lift</c>): k 500, c 49.</summary>
    public static readonly SpringSpec MorphLift = SpringSpec.FromPhysical(500, 49);

    /// <summary>Layout morph carousel / helix snap after a drag: k 110, c 19.5 (with the release velocity).</summary>
    public static readonly SpringSpec MorphSnap = SpringSpec.FromPhysical(110, 19.5);

    /// <summary>Layout morph: the old caption layer fading out (and other cards dimming on open): critical ω 25.</summary>
    public static readonly SpringSpec MorphCaptionOut = Critical(25);

    /// <summary>Layout morph: the new caption layer fading in about 400ms after the card starts: critical ω 10.</summary>
    public static readonly SpringSpec MorphCaptionIn = Critical(10);
}

/// <summary>
/// One spring-driven scalar advanced by explicit time steps (the cameras' channels). Each <see cref="Step"/> evaluates
/// the closed-form damped oscillator from the current position and velocity, so it is exact for any step length and a
/// retarget (<see cref="SpringTo(double, in SpringSpec)"/>) keeps the velocity, like <see cref="MotionValue{T}"/>.
/// </summary>
/// <remarks>
/// Why not <see cref="MotionValue{T}"/>: the cameras are pure state machines advanced by the stage's single frame
/// loop (and by tests) with explicit time steps; several channels (center, zoom, bounds, inertia) must move in lockstep
/// inside one step with clamps between them, which independent scheduler-driven values cannot do, and a ControlTheme
/// transition cannot express a chase toward a target that moves every frame.
/// </remarks>
internal struct ShowpieceSpringChannel
{
    public ShowpieceSpringChannel(double value, double restDelta, double restSpeed)
    {
        Value = value;
        Target = value;
        Velocity = 0;
        Spec = ShowpieceSprings.CameraFollow;
        RestDelta = restDelta;
        RestSpeed = restSpeed;
        IsMoving = false;
    }

    /// <summary>Current value.</summary>
    public double Value;

    /// <summary>Current velocity (units per second).</summary>
    public double Velocity;

    /// <summary>The value the spring heads to.</summary>
    public double Target;

    /// <summary>The spring of the current motion.</summary>
    public SpringSpec Spec;

    /// <summary>Distance to the target under which the channel may come to rest.</summary>
    public double RestDelta;

    /// <summary>Speed under which the channel may come to rest.</summary>
    public double RestSpeed;

    /// <summary>A spring is running.</summary>
    public bool IsMoving;

    /// <summary>Sets the value at once and stops (velocity 0).</summary>
    public void Jump(double value)
    {
        Value = value;
        Target = value;
        Velocity = 0;
        IsMoving = false;
    }

    /// <summary>Stops where it is.</summary>
    public void Stop()
    {
        Target = Value;
        Velocity = 0;
        IsMoving = false;
    }

    /// <summary>Springs to <paramref name="target"/> from the current value, keeping the current velocity.</summary>
    public void SpringTo(double target, in SpringSpec spec)
    {
        Target = target;
        Spec = spec;
        IsMoving = Math.Abs(Value - target) > RestDelta || Math.Abs(Velocity) > RestSpeed;
        if (!IsMoving)
        {
            Value = target;
            Velocity = 0;
        }
    }

    /// <summary>Springs to <paramref name="target"/> starting with <paramref name="velocity"/>.</summary>
    public void SpringTo(double target, in SpringSpec spec, double velocity)
    {
        Velocity = velocity;
        SpringTo(target, spec);
    }

    /// <summary>Advances by <paramref name="dt"/> seconds; returns true while still moving.</summary>
    public bool Step(double dt)
    {
        if (!IsMoving)
        {
            return false;
        }
        if (dt <= 0)
        {
            return true;
        }
        var (x, v) = SpringSolver.Evaluate(Spec, Value - Target, Velocity, dt);
        Value = Target + x;
        Velocity = v;
        if (Math.Abs(x) <= RestDelta && Math.Abs(v) <= RestSpeed)
        {
            Value = Target;
            Velocity = 0;
            IsMoving = false;
        }
        return IsMoving;
    }
}
