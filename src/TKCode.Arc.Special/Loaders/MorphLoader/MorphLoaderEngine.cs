using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The motion of a <see cref="MorphLoader"/>, free of Avalonia so it can be stepped and checked frame by frame (Arc
/// morph loader, design reference; constants measured from the public preview).
/// <para>
/// Every parameter of every stroke (center x and y, length, direction, bend, width, opacity: 28 values) sits on its own
/// critically damped spring that keeps its velocity when it is given a new target, so any shape morphs into any other
/// and interruptions never jerk. The loading loop is not part of the springs: it is an offset added on top, scaled by a
/// loop weight that drops to 0 the moment a result arrives (the strokes freeze into the resting shape and fold from
/// there) and returns to 1 within about 100ms when loading resumes. The loop clock only runs while loading. The
/// drawing's rotation either follows the loop (ring: 330 degrees/s; square: a quarter tumble per cycle) or springs
/// forward to the next whole turn, carrying the speed it had. A result also pops the drawing (keyframes) and draws the
/// strokes of the check or cross one after another, 35ms apart.
/// </para>
/// <para>
/// One engine steps all 28 springs, the loop and the rotation in a single frame callback, instead of 30
/// <see cref="MotionValue{T}"/> drivers: the loop offsets, the loop weight, the staggered starts and the rotation hand-off
/// all read the same clock, and a test can step it exactly. The springs are the same closed-form solution
/// (<see cref="SpringSolver"/>) MotionValue uses, retargeted in place with their velocity.
/// </para>
/// </summary>
internal sealed class MorphLoaderEngine
{
    /// <summary>Variant to variant morphs and the gather back from a result (k ≈ 130.6, c ≈ 22.9).</summary>
    public static readonly SpringSpec ShapeSpring = SpringSpec.FromVisual(0.46, 0);

    /// <summary>Loading to check or cross (k ≈ 101.3, c ≈ 20.1).</summary>
    public static readonly SpringSpec ResultSpring = SpringSpec.FromVisual(0.52, 0);

    /// <summary>The forward turn to upright (k ≈ 76.3, c ≈ 17.5).</summary>
    public static readonly SpringSpec TurnSpring = SpringSpec.FromVisual(0.60, 0);

    /// <summary>The loop weight's return to 1 after a result (critically damped, ω = 50: 0.5 at 33ms, 0.96 at 100ms).</summary>
    public static readonly SpringSpec WeightSpring = SpringSpec.FromPhysical(2500, 100);

    /// <summary>The longest frame the engine advances at once (a stalled frame does not jump the loop).</summary>
    public const double MaxStep = 1.0 / 16;

    /// <summary>The delay between the strokes of a check or cross.</summary>
    public const double StrokeStagger = 0.035;

    /// <summary>How long the color takes to reach the success or danger tone (and back).</summary>
    public const double ToneDuration = 0.24;

    private const int Count = MorphLoaderShapes.StrokeCount * MorphLoaderStroke.ParameterCount;
    // At rest within a thousandth of a grid unit (or degree) and a hundredth per second: far below a pixel.
    private const double RestDelta = 1e-3;
    private const double RestSpeed = 1e-2;

    private readonly double[] _x = new double[Count];
    private readonly double[] _v = new double[Count];
    private readonly double[] _target = new double[Count];
    private readonly SpringSpec[] _spec = new SpringSpec[Count];
    private readonly bool[] _moving = new bool[Count];
    private readonly double[] _pendingTarget = new double[Count];
    private readonly SpringSpec[] _pendingSpec = new SpringSpec[Count];
    private readonly double[] _pendingDelay = new double[Count];
    private readonly MorphLoaderStroke[] _rest = new MorphLoaderStroke[MorphLoaderShapes.StrokeCount];
    private readonly MorphLoaderStroke[] _shown = new MorphLoaderStroke[MorphLoaderShapes.StrokeCount];

    private double _theta;
    private double _thetaVelocity;
    private double _thetaTarget;
    private bool _thetaSprings;
    private bool _thetaMoving;

    private double _loopTime;
    private double _weight;
    private double _weightVelocity;
    private double _weightTarget;

    private double _popTime = double.NaN;
    private double _statusAge = double.PositiveInfinity;
    private bool _animated;

    public MorphLoaderEngine(double strokeWidth = 2.5, double dotWidth = 4, double barWidth = 3)
    {
        StrokeWidth = strokeWidth;
        DotWidth = dotWidth;
        BarWidth = barWidth;
        Array.Fill(_pendingDelay, double.NaN);
        Jump(MorphLoaderVariant.Dots, MorphLoaderStatus.Loading, true);
    }

    public MorphLoaderVariant Variant { get; private set; }
    public MorphLoaderStatus Status { get; private set; }
    public double StrokeWidth { get; private set; }
    public double DotWidth { get; private set; }
    public double BarWidth { get; private set; }

    /// <summary>The strokes as drawn now (springs plus the weighted loop; the length is not clamped yet).</summary>
    public ReadOnlySpan<MorphLoaderStroke> Strokes => _shown;

    /// <summary>The drawing's rotation in degrees (unbounded internally, kept small by whole turns).</summary>
    public double Rotation => _theta;

    /// <summary>The drawing's rotation speed in degrees per second (the spring's, or the loop's while it turns the drawing).</summary>
    public double RotationVelocity => _thetaSprings ? _thetaVelocity : _weight * MorphLoaderShapes.LoopTurnRate(Variant, _loopTime);

    /// <summary>True while the rotation follows the loop (ring, square) rather than a spring.</summary>
    public bool RotationFollowsLoop => !_thetaSprings;

    /// <summary>The pop scale now (1 at rest).</summary>
    public double Scale => double.IsNaN(_popTime) ? 1 : MorphLoaderShapes.PopAt(_popTime);

    /// <summary>The loop clock in seconds (runs only while loading; wraps after <see cref="MorphLoaderShapes.LoopWrap"/>).</summary>
    public double LoopTime => _loopTime;

    /// <summary>How much of the loop is shown (0 .. 1).</summary>
    public double LoopWeight => _weight;

    /// <summary>Seconds since the status last changed (infinite after a jump).</summary>
    public double StatusAge => _statusAge;

    /// <summary>The target of one stroke parameter (for tests).</summary>
    public double TargetOf(int stroke, int parameter) => _target[stroke * MorphLoaderStroke.ParameterCount + parameter];

    /// <summary>The spring value of one stroke parameter, without the loop (for tests).</summary>
    public double BaseOf(int stroke, int parameter) => _x[stroke * MorphLoaderStroke.ParameterCount + parameter];

    /// <summary>The spring velocity of one stroke parameter (for tests).</summary>
    public double VelocityOf(int stroke, int parameter) => _v[stroke * MorphLoaderStroke.ParameterCount + parameter];

    /// <summary>True while a stroke parameter waits for its staggered start (for tests).</summary>
    public bool IsWaiting(int stroke) => !double.IsNaN(_pendingDelay[stroke * MorphLoaderStroke.ParameterCount]);

    /// <summary>True while anything moves besides the endless loop (springs, rotation spring, staggered starts, pop, tone).</summary>
    public bool IsSettling
    {
        get
        {
            if (_thetaSprings && _thetaMoving)
            {
                return true;
            }
            if (!double.IsNaN(_popTime) || _statusAge < ToneDuration)
            {
                return true;
            }
            if (Math.Abs(_weight - _weightTarget) > RestDelta || Math.Abs(_weightVelocity) > RestSpeed)
            {
                return true;
            }
            for (var i = 0; i < Count; i++)
            {
                if (_moving[i] || !double.IsNaN(_pendingDelay[i]))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>True while the engine needs frames: always while loading (and animated), otherwise while settling.</summary>
    public bool NeedsFrames => _animated && (Status == MorphLoaderStatus.Loading || IsSettling);

    /// <summary>
    /// Shows <paramref name="variant"/> and <paramref name="status"/> at rest at once: no morph, the drawing upright, no pop.
    /// With <paramref name="animated"/> the loading loop runs at full weight; without it (reduced motion, motion off) every
    /// state is a static shape.
    /// </summary>
    public void Jump(MorphLoaderVariant variant, MorphLoaderStatus status, bool animated)
    {
        Variant = variant;
        Status = status;
        _animated = animated;
        MorphLoaderShapes.WriteRest(MorphLoaderShapes.ShapeFor(variant, status), StrokeWidth, DotWidth, BarWidth, _rest);
        for (var s = 0; s < MorphLoaderShapes.StrokeCount; s++)
        {
            for (var p = 0; p < MorphLoaderStroke.ParameterCount; p++)
            {
                var i = s * MorphLoaderStroke.ParameterCount + p;
                _x[i] = _target[i] = _rest[s][p];
                _v[i] = 0;
                _moving[i] = false;
                _pendingDelay[i] = double.NaN;
                _spec[i] = ShapeSpring;
            }
        }
        _theta = 0;
        _thetaVelocity = 0;
        _thetaTarget = 0;
        _thetaMoving = false;
        _thetaSprings = !(animated && status == MorphLoaderStatus.Loading && MorphLoaderShapes.Turns(variant));
        _weight = _weightTarget = animated && status == MorphLoaderStatus.Loading ? 1 : 0;
        _weightVelocity = 0;
        _popTime = double.NaN;
        _statusAge = double.PositiveInfinity;
        Compose();
    }

    /// <summary>
    /// Morphs toward <paramref name="variant"/> and <paramref name="status"/> from wherever the strokes are, keeping every
    /// velocity. Loading to a result freezes the loop, turns forward to upright, draws the strokes 35ms apart and pops;
    /// a result back to loading gathers the strokes into the variant together; a variant change while loading morphs all
    /// strokes together (leaving the dots, the strokes move before they lengthen).
    /// </summary>
    public void MorphTo(MorphLoaderVariant variant, MorphLoaderStatus status)
    {
        if (!_animated)
        {
            Jump(variant, status, false);
            return;
        }
        var previousVariant = Variant;
        var previousStatus = Status;
        if (variant == previousVariant && status == previousStatus)
        {
            return;
        }
        var thetaVelocity = RotationVelocity;
        Variant = variant;
        Status = status;
        var result = status != MorphLoaderStatus.Loading;
        if (result)
        {
            if (status != previousStatus)
            {
                // The loop vanishes in one frame: the strokes fold from the resting shape.
                _weight = 0;
                _weightVelocity = 0;
                _weightTarget = 0;
                _popTime = 0;
                _statusAge = 0;
                Retarget(MorphLoaderShapes.ShapeFor(variant, status), ResultSpring, StrokeStagger);
                TurnUpright(thetaVelocity);
            }
            // A variant change during a result only matters once loading resumes.
            Compose();
            return;
        }
        if (previousStatus != MorphLoaderStatus.Loading)
        {
            // Back to loading: the loop fades in within about 100ms while the strokes gather together.
            _weightTarget = 1;
            _statusAge = 0;
        }
        else if (previousVariant == MorphLoaderVariant.Dots && variant != MorphLoaderVariant.Dots)
        {
            // Leaving the dots, the length starts a dot's width below zero (drawn as zero): the dots travel first and only
            // then stretch into lines.
            for (var s = 0; s < MorphLoaderShapes.StrokeCount; s++)
            {
                var i = s * MorphLoaderStroke.ParameterCount + MorphLoaderStroke.LengthIndex;
                if (_x[i] < 0.05)
                {
                    _x[i] = Math.Min(_x[i], -DotWidth);
                }
            }
        }
        Retarget(MorphLoaderShapes.ShapeFor(variant, status), ShapeSpring, 0);
        if (MorphLoaderShapes.Turns(variant))
        {
            // The ring turns at once, the square tumbles from wherever it is; a ring entering the square stops turning.
            _thetaSprings = false;
            _thetaMoving = false;
            _thetaVelocity = 0;
        }
        else
        {
            TurnUpright(thetaVelocity);
        }
        Compose();
    }

    /// <summary>Changes the stroke widths: ring, square, check and cross; dots; bars (morphs while animated).</summary>
    public void SetWidths(double strokeWidth, double dotWidth, double barWidth)
    {
        if (strokeWidth == StrokeWidth && dotWidth == DotWidth && barWidth == BarWidth)
        {
            return;
        }
        StrokeWidth = strokeWidth;
        DotWidth = dotWidth;
        BarWidth = barWidth;
        MorphLoaderShapes.WriteRest(MorphLoaderShapes.ShapeFor(Variant, Status), StrokeWidth, DotWidth, BarWidth, _rest);
        for (var s = 0; s < MorphLoaderShapes.StrokeCount; s++)
        {
            var i = s * MorphLoaderStroke.ParameterCount + MorphLoaderStroke.WidthIndex;
            var width = _rest[s].Width;
            if (!double.IsNaN(_pendingDelay[i]))
            {
                _pendingTarget[i] = width;
            }
            else if (_animated)
            {
                _target[i] = width;
                _spec[i] = ShapeSpring;
                _moving[i] = true;
            }
            else
            {
                _x[i] = _target[i] = width;
            }
        }
        Compose();
    }

    /// <summary>Advances everything by <paramref name="seconds"/> (at most <see cref="MaxStep"/>). Returns <see cref="NeedsFrames"/>.</summary>
    public bool Step(double seconds)
    {
        var dt = Math.Clamp(seconds, 0, MaxStep);
        if (!_animated || dt <= 0)
        {
            return NeedsFrames;
        }
        var loading = Status == MorphLoaderStatus.Loading;
        var previousLoop = _loopTime;
        if (loading)
        {
            _loopTime += dt;
        }
        var wrapped = false;
        if (_loopTime >= MorphLoaderShapes.LoopWrap)
        {
            _loopTime -= MorphLoaderShapes.LoopWrap;
            wrapped = true;
        }

        // The loop weight.
        if (Math.Abs(_weight - _weightTarget) > RestDelta || Math.Abs(_weightVelocity) > RestSpeed)
        {
            var (d, v) = SpringSolver.Evaluate(WeightSpring, _weight - _weightTarget, _weightVelocity, dt);
            _weight = _weightTarget + d;
            _weightVelocity = v;
            if (Math.Abs(d) <= RestDelta && Math.Abs(v) <= RestSpeed)
            {
                _weight = _weightTarget;
                _weightVelocity = 0;
            }
        }

        // The rotation.
        if (_thetaSprings)
        {
            if (_thetaMoving)
            {
                var (d, v) = SpringSolver.Evaluate(TurnSpring, _theta - _thetaTarget, _thetaVelocity, dt);
                _theta = _thetaTarget + d;
                _thetaVelocity = v;
                if (Math.Abs(d) <= RestDelta && Math.Abs(v) <= RestSpeed)
                {
                    _theta = _thetaTarget;
                    _thetaVelocity = 0;
                    _thetaMoving = false;
                }
            }
        }
        else if (loading)
        {
            if (Variant == MorphLoaderVariant.Ring)
            {
                _theta += _weight * MorphLoaderShapes.RingSpeed * dt;
            }
            else
            {
                var turns = MorphLoaderShapes.SquareTurns(_loopTime) - MorphLoaderShapes.SquareTurns(previousLoop);
                if (wrapped)
                {
                    turns += MorphLoaderShapes.LoopWrap / MorphLoaderShapes.SquarePeriod;
                }
                _theta += _weight * 90 * turns;
            }
        }
        if (Math.Abs(_theta) > 3600)
        {
            var shift = 360 * Math.Floor(_theta / 360);
            _theta -= shift;
            _thetaTarget -= shift;
        }

        // The strokes.
        for (var i = 0; i < Count; i++)
        {
            if (!double.IsNaN(_pendingDelay[i]))
            {
                _pendingDelay[i] -= dt;
                if (_pendingDelay[i] <= 1e-9)
                {
                    _pendingDelay[i] = double.NaN;
                    _target[i] = _pendingTarget[i];
                    _spec[i] = _pendingSpec[i];
                    _moving[i] = true;
                }
            }
            if (!_moving[i])
            {
                continue;
            }
            var (d, v) = SpringSolver.Evaluate(_spec[i], _x[i] - _target[i], _v[i], dt);
            _x[i] = _target[i] + d;
            _v[i] = v;
            if (Math.Abs(d) <= RestDelta && Math.Abs(v) <= RestSpeed)
            {
                _x[i] = _target[i];
                _v[i] = 0;
                _moving[i] = false;
            }
        }

        if (!double.IsNaN(_popTime))
        {
            _popTime += dt;
            if (_popTime >= MorphLoaderShapes.PopDuration)
            {
                _popTime = double.NaN;
            }
        }
        if (!double.IsPositiveInfinity(_statusAge))
        {
            _statusAge += dt;
            if (_statusAge >= ToneDuration)
            {
                _statusAge = double.PositiveInfinity;
            }
        }
        Compose();
        return NeedsFrames;
    }

    private void Retarget(MorphLoaderShape shape, SpringSpec spec, double stagger)
    {
        MorphLoaderShapes.WriteRest(shape, StrokeWidth, DotWidth, BarWidth, _rest);
        for (var s = 0; s < MorphLoaderShapes.StrokeCount; s++)
        {
            var baseIndex = s * MorphLoaderStroke.ParameterCount;
            var rest = _rest[s];
            // The direction is aimed from where the stroke will be when it starts (its current direction and bend).
            rest.Angle = MorphLoaderShapes.AimAngle(_x[baseIndex + MorphLoaderStroke.AngleIndex], _x[baseIndex + MorphLoaderStroke.BendIndex],
                rest.Angle, rest.Bend);
            var delay = stagger * s;
            for (var p = 0; p < MorphLoaderStroke.ParameterCount; p++)
            {
                var i = baseIndex + p;
                if (delay > 0)
                {
                    _pendingTarget[i] = rest[p];
                    _pendingSpec[i] = spec;
                    _pendingDelay[i] = delay;
                }
                else
                {
                    _pendingDelay[i] = double.NaN;
                    _target[i] = rest[p];
                    _spec[i] = spec;
                    _moving[i] = true;
                }
            }
        }
    }

    private void TurnUpright(double velocity)
    {
        _thetaSprings = true;
        _thetaVelocity = velocity;
        _thetaTarget = MorphLoaderShapes.NextUpright(_theta);
        _thetaMoving = Math.Abs(_thetaTarget - _theta) > RestDelta || Math.Abs(velocity) > RestSpeed;
    }

    private void Compose()
    {
        for (var s = 0; s < MorphLoaderShapes.StrokeCount; s++)
        {
            var baseIndex = s * MorphLoaderStroke.ParameterCount;
            var stroke = new MorphLoaderStroke();
            for (var p = 0; p < MorphLoaderStroke.ParameterCount; p++)
            {
                stroke[p] = _x[baseIndex + p];
            }
            stroke.Width = Math.Max(0, stroke.Width);
            stroke.Opacity = Math.Clamp(stroke.Opacity, 0, 1);
            stroke.Bend = Math.Max(0, stroke.Bend);
            _shown[s] = stroke;
        }
        if (_animated)
        {
            MorphLoaderShapes.ApplyLoop(Variant, _loopTime, _weight, _shown);
        }
    }
}
