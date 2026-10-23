namespace TKCode.Arc;

/// <summary>
/// The moving state of every dot of a <see cref="CustomerGalaxy"/> in flat arrays (no allocation per frame): where each
/// dot is drawn (world position and radius), the rigid rotation and slow drift of the layout, the per-dot springs of a
/// grouping change, and the dimming of groups.
/// <para>
/// A grouping change (spec 6.2): about 70% of the dots set off at once, the rest after 140–330 ms; each first wanders
/// slowly (30–100 px/s in a random direction for 100–250 ms), then a spring (ω 8.3, ζ 0.85) with a clockwise swirl
/// pulls it into its slot (peak speed about 3.5 px/s per px of travel, a curved path 1.1–1.6× the straight line). The
/// spring works on the offset from the slot, so slots that turn with their cluster are tracked without lag. A dot swells
/// by up to 0.3 px while it flies.
/// </para>
/// </summary>
/// <remarks>
/// Code-driven motion: 2,400 independent springs with launch delays, a swirl term and moving targets, stepped together
/// in the stage's frame loop; neither a ControlTheme transition nor one <c>MotionValue</c> per dot can express or
/// afford that.
/// </remarks>
internal sealed class GalaxyField
{
    /// <summary>Morph spring: natural frequency, damping ratio and swirl (clockwise) strength.</summary>
    public const double MorphOmega = 8.3;

    public const double MorphDamping = 0.85;

    public const double MorphSwirl = 0.35;

    /// <summary>Radius spring of a morph (critical).</summary>
    public const double RadiusOmega = 8.3;

    /// <summary>A flying dot swells by this much (world px) at full speed.</summary>
    public const double SwellSize = 0.3;

    /// <summary>The speed (world px/s) counted as full speed for the swell.</summary>
    public const double SwellSpeed = 900;

    /// <summary>Spin of the rotation: decays at 2.55/s while the pointer rests on the stage, recovers at 2.7/s.</summary>
    public const double SpinDecayRate = 2.55;

    public const double SpinRecoverRate = 2.7;

    /// <summary>Dimming approaches its target at 15/s (about 0.26 s).</summary>
    public const double DimRate = 15;

    private const byte Settled = 0;
    private const byte Holding = 1;
    private const byte Wandering = 2;
    private const byte Springing = 3;

    private GalaxyModel _model = GalaxyModel.Empty;
    private GalaxyGroupSet? _layout;
    private GalaxyGroupSet? _dimSet;
    private double[] _x = Array.Empty<double>();
    private double[] _y = Array.Empty<double>();
    private double[] _r = Array.Empty<double>();
    private double[] _dx = Array.Empty<double>();
    private double[] _dy = Array.Empty<double>();
    private double[] _vx = Array.Empty<double>();
    private double[] _vy = Array.Empty<double>();
    private double[] _dr = Array.Empty<double>();
    private double[] _vr = Array.Empty<double>();
    private double[] _swell = Array.Empty<double>();
    private double[] _launch = Array.Empty<double>();
    private double[] _wanderEnd = Array.Empty<double>();
    private double[] _wx = Array.Empty<double>();
    private double[] _wy = Array.Empty<double>();
    private byte[] _phase = Array.Empty<byte>();
    private double[] _dim = Array.Empty<double>();
    private double[] _dimTarget = Array.Empty<double>();
    private int[] _dimLevel = Array.Empty<int>();
    private int _moving;
    private double _morphClock;
    private uint _morphCount;

    /// <summary>Rotation angle of the layout (radians, clockwise on screen).</summary>
    public double Angle { get; set; } = InitialAngle;

    /// <summary>The overview's resting angle (also the reduced-motion frame): arms over the top and under the core.</summary>
    public const double InitialAngle = 2.4;

    /// <summary>0…1: how much of the rotation speed is applied (it eases to 0 while the pointer rests on the stage).</summary>
    public double Spin { get; set; } = 1;

    /// <summary>Time of the slow drift (advances with the rotation).</summary>
    public double DriftClock { get; set; }

    public GalaxyModel Model => _model;

    /// <summary>The cluster layout, or null for the overview spiral.</summary>
    public GalaxyGroupSet? Layout => _layout;

    /// <summary>The groups dimming applies to (the clusters, or the health states in the overview).</summary>
    public GalaxyGroupSet? DimSet => _dimSet;

    public int Count => _model.Count;

    /// <summary>Drawn world positions and radii (after the last step).</summary>
    public double[] X => _x;

    public double[] Y => _y;

    public double[] R => _r;

    /// <summary>The dimming level (0 most dimmed … 6 full) of every group of <see cref="DimSet"/>.</summary>
    public int[] DimLevels => _dimLevel;

    /// <summary>Dots still flying to their slots.</summary>
    public int MovingCount => _moving;

    public bool IsMorphing => _moving > 0;

    public bool IsDimming
    {
        get
        {
            for (var g = 0; g < _dim.Length; g++)
            {
                if (_dim[g] != _dimTarget[g])
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>The dim group of a dot (−1 when none).</summary>
    public int DimGroupOf(int i) => _dimSet is { } set && i < set.GroupOf.Length ? set.GroupOf[i] : -1;

    /// <summary>Takes a model and a layout and puts every dot in its slot at once.</summary>
    public void Load(GalaxyModel model, GalaxyGroupSet? layout)
    {
        _model = model;
        var n = model.Count;
        if (_x.Length != n)
        {
            _x = new double[n];
            _y = new double[n];
            _r = new double[n];
            _dx = new double[n];
            _dy = new double[n];
            _vx = new double[n];
            _vy = new double[n];
            _dr = new double[n];
            _vr = new double[n];
            _swell = new double[n];
            _launch = new double[n];
            _wanderEnd = new double[n];
            _wx = new double[n];
            _wy = new double[n];
            _phase = new byte[n];
        }
        else
        {
            Array.Clear(_phase);
            Array.Clear(_swell);
        }
        _layout = layout;
        SetDimSet(layout ?? model.HealthSet);
        _moving = 0;
        for (var i = 0; i < n; i++)
        {
            Target(i, out _x[i], out _y[i], out _r[i]);
        }
    }

    /// <summary>
    /// Moves every dot to its slot in <paramref name="layout"/> (null: the overview spiral): springs with launch jitter
    /// when <paramref name="animate"/>, at once otherwise. Dots already flying keep their speed.
    /// </summary>
    public void MorphTo(GalaxyGroupSet? layout, bool animate)
    {
        _layout = layout;
        SetDimSet(layout ?? _model.HealthSet);
        var n = _model.Count;
        if (!animate)
        {
            _moving = 0;
            for (var i = 0; i < n; i++)
            {
                _phase[i] = Settled;
                _swell[i] = 0;
                Target(i, out _x[i], out _y[i], out _r[i]);
            }
            return;
        }
        _morphClock = 0;
        _morphCount++;
        var seed = new ShowpieceSeed(GalaxyModel.Seed + _morphCount * 7919u);
        _moving = n;
        for (var i = 0; i < n; i++)
        {
            Target(i, out var tx, out var ty, out var tr);
            var flying = _phase[i] == Springing && (_vx[i] * _vx[i] + _vy[i] * _vy[i]) > 2500;
            // Absolute position now; the wander integrates it, the spring then works on the offset from the slot.
            _dx[i] = _x[i];
            _dy[i] = _y[i];
            _dr[i] = _r[i] - tr;
            _vr[i] = flying ? _vr[i] : 0;
            var lag = seed.NextDouble() < 0.7 ? 0 : 0.14 + 0.19 * seed.NextDouble();
            var wander = 0.10 + 0.15 * seed.NextDouble();
            var speed = 30 + 70 * seed.NextDouble();
            var heading = seed.NextDouble() * 2 * Math.PI;
            if (flying)
            {
                // Interrupted mid-flight: keep the velocity and spring on toward the new slot.
                _launch[i] = 0;
                _wanderEnd[i] = 0;
                _wx[i] = _vx[i];
                _wy[i] = _vy[i];
                _dx[i] = _x[i] - tx;
                _dy[i] = _y[i] - ty;
                _phase[i] = Springing;
                continue;
            }
            _launch[i] = lag;
            _wanderEnd[i] = lag + wander;
            _wx[i] = speed * Math.Cos(heading);
            _wy[i] = speed * Math.Sin(heading);
            _vx[i] = 0;
            _vy[i] = 0;
            _phase[i] = Holding;
        }
    }

    private void SetDimSet(GalaxyGroupSet set)
    {
        if (ReferenceEquals(_dimSet, set))
        {
            return;
        }
        _dimSet = set;
        _dim = new double[set.Count];
        _dimTarget = new double[set.Count];
        _dimLevel = new int[set.Count];
        for (var g = 0; g < set.Count; g++)
        {
            _dim[g] = 1;
            _dimTarget[g] = 1;
            _dimLevel[g] = GalaxyLayout.DimLevels - 1;
        }
    }

    /// <summary>
    /// Dims every group except <paramref name="keep"/> (−1: none dimmed); at once unless <paramref name="animate"/>.
    /// </summary>
    public void DimAllBut(int keep, bool animate)
    {
        for (var g = 0; g < _dimTarget.Length; g++)
        {
            var target = keep < 0 || g == keep ? 1.0 : GalaxyLayout.DimFactor;
            _dimTarget[g] = target;
            if (!animate)
            {
                _dim[g] = target;
                _dimLevel[g] = GalaxyLayout.DimLevel(target);
            }
        }
    }

    /// <summary>The slot of dot <paramref name="i"/> now (world position and radius).</summary>
    public void Target(int i, out double x, out double y, out double r)
    {
        var model = _model;
        if (_layout is { HasGeometry: true } layout)
        {
            var g = layout.GroupOf[i];
            var c = Math.Cos(Angle);
            var s = Math.Sin(Angle);
            var lx = layout.LocalX[i];
            var ly = layout.LocalY[i];
            var center = layout.Centers[g];
            x = center.X + lx * c - ly * s;
            y = center.Y + lx * s + ly * c;
            r = model.Radius[i];
            return;
        }
        var rho = model.Rho[i] + model.DriftRadius[i] * Math.Sin(model.DriftRate[i] * DriftClock + model.DriftPhase[i]);
        var theta = model.Theta[i] + Angle +
                    model.DriftAngle[i] * Math.Sin(model.DriftRate[i] * 0.83 * DriftClock + model.DriftPhase[i] * 1.7);
        var toward = rho * Math.Sin(theta);
        x = rho * Math.Cos(theta);
        y = GalaxyLayout.Squash * toward;
        r = model.Radius[i] * GalaxyLayout.DepthScale(toward);
    }

    /// <summary>
    /// Advances by <paramref name="dt"/> seconds: rotation and drift (when <paramref name="rotate"/>; the spin eases toward
    /// <paramref name="spinTarget"/>), the morph springs and the dimming (snapped unless <paramref name="animate"/>).
    /// Returns true while anything moves.
    /// </summary>
    public bool Step(double dt, bool animate, bool rotate, double spinTarget, double rotationSpeed)
    {
        if (!(dt > 0))
        {
            return IsMorphing || IsDimming || (rotate && Spin > 1e-3);
        }
        dt = Math.Min(dt, 0.1);
        var moving = false;
        var layoutMoves = false;
        if (rotate)
        {
            var rate = spinTarget > Spin ? SpinRecoverRate : SpinDecayRate;
            Spin = ShowpieceInertia.Approach(Spin, spinTarget, 1 / rate, dt, 1e-3);
            if (Spin > 0)
            {
                Angle += rotationSpeed * Spin * dt;
                DriftClock += Spin * dt;
                layoutMoves = true;
                moving = true;
            }
        }
        if (_moving > 0)
        {
            StepMorph(dt);
            moving = true;
        }
        else if (layoutMoves)
        {
            var n = _model.Count;
            for (var i = 0; i < n; i++)
            {
                Target(i, out _x[i], out _y[i], out _r[i]);
            }
        }
        for (var g = 0; g < _dim.Length; g++)
        {
            if (_dim[g] == _dimTarget[g])
            {
                continue;
            }
            _dim[g] = animate ? ShowpieceInertia.Approach(_dim[g], _dimTarget[g], 1 / DimRate, dt, 2e-3) : _dimTarget[g];
            _dimLevel[g] = GalaxyLayout.DimLevel(_dim[g]);
            moving = true;
        }
        return moving;
    }

    private void StepMorph(double dt)
    {
        var start = _morphClock;
        _morphClock += dt;
        var clock = _morphClock;
        var n = _model.Count;
        var steps = Math.Max(1, (int)Math.Ceiling(dt * 120));
        var h = dt / steps;
        var w2 = MorphOmega * MorphOmega;
        var damping = 2 * MorphDamping * MorphOmega;
        var r2 = RadiusOmega * RadiusOmega;
        var rDamping = 2 * RadiusOmega;
        var swellTau = 0.12;
        var swellDecay = 1 - Math.Exp(-dt / swellTau);
        var moving = 0;
        for (var i = 0; i < n; i++)
        {
            Target(i, out var tx, out var ty, out var tr);
            var phase = _phase[i];
            if (phase == Settled)
            {
                _x[i] = tx;
                _y[i] = ty;
                _r[i] = tr;
                continue;
            }
            if (phase == Holding || phase == Wandering)
            {
                // Absolute position in _dx/_dy until the spring takes over.
                if (clock > _launch[i])
                {
                    var from = Math.Max(start, _launch[i]);
                    var to = Math.Min(clock, _wanderEnd[i]);
                    if (to > from)
                    {
                        _dx[i] += _wx[i] * (to - from);
                        _dy[i] += _wy[i] * (to - from);
                    }
                    phase = Wandering;
                }
                if (clock >= _wanderEnd[i])
                {
                    _dx[i] -= tx;
                    _dy[i] -= ty;
                    _vx[i] = _wx[i];
                    _vy[i] = _wy[i];
                    phase = Springing;
                }
                else
                {
                    _phase[i] = phase;
                    _x[i] = _dx[i];
                    _y[i] = _dy[i];
                    _r[i] = tr + _dr[i];
                    moving++;
                    continue;
                }
            }
            double dx = _dx[i], dy = _dy[i], vx = _vx[i], vy = _vy[i], dr = _dr[i], vr = _vr[i];
            for (var s = 0; s < steps; s++)
            {
                // Offset from the slot: spring + clockwise swirl (x → down, y → left) + damping, semi-implicit Euler.
                var ax = -w2 * (dx + MorphSwirl * dy) - damping * vx;
                var ay = -w2 * (dy - MorphSwirl * dx) - damping * vy;
                vx += ax * h;
                vy += ay * h;
                dx += vx * h;
                dy += vy * h;
                var ar = -r2 * dr - rDamping * vr;
                vr += ar * h;
                dr += vr * h;
            }
            var speed = Math.Sqrt(vx * vx + vy * vy);
            var swell = _swell[i] + (Math.Min(1, speed / SwellSpeed) - _swell[i]) * swellDecay;
            if (Math.Abs(dx) < 0.05 && Math.Abs(dy) < 0.05 && speed < 0.6 && Math.Abs(dr) < 0.01 && swell < 0.01)
            {
                _phase[i] = Settled;
                _swell[i] = 0;
                _x[i] = tx;
                _y[i] = ty;
                _r[i] = tr;
                continue;
            }
            _phase[i] = Springing;
            _dx[i] = dx;
            _dy[i] = dy;
            _vx[i] = vx;
            _vy[i] = vy;
            _dr[i] = dr;
            _vr[i] = vr;
            _swell[i] = swell;
            _x[i] = tx + dx;
            _y[i] = ty + dy;
            _r[i] = Math.Max(0.05, tr + dr + SwellSize * swell);
            moving++;
        }
        _moving = moving;
    }
}
