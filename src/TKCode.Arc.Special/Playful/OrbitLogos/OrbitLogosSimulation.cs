namespace TKCode.Arc;

/// <summary>
/// The two rings of <see cref="OrbitLogos"/> without any UI: which ring each logo sits on, how far each ring has
/// turned, and the motion between a free turn and a parked angle. Angles are in radians, measured clockwise from the
/// right (the y axis points down); the detail side is at <see cref="DetailAngle"/> (0 beside the orbit, π/2 under it).
/// </summary>
/// <remarks>
/// A ring is either turning freely or following a target. While free it turns at its own period (the inner ring once
/// every 64 s, the outer once every 88 s, opposite ways) scaled by <see cref="Speed"/> and by a spin factor that eases
/// to 0 on a critically damped spring whenever the orbit is held (pointer or focus inside, a category chosen, a card
/// open) and back to 1 when it is released. A target is always reached the short way round.
/// </remarks>
internal sealed class OrbitLogosSimulation
{
    public const double InnerPeriod = 64;
    public const double OuterPeriod = 88;

    private const double Tau = Math.PI * 2;
    private const double SpinFrequency = 6.5;

    /// <summary>A chosen logo: a touch of overshoot (ζ 0.72) so it settles into its spot.</summary>
    private const double SelectFrequency = 7.5;
    private const double SelectDamping = 0.72;

    /// <summary>A category, or the inner ring stepping aside: critically damped, no overshoot.</summary>
    private const double TurnFrequency = 5.5;

    private readonly Ring[] _rings = [new(), new()];
    private int[] _ringOf = [];
    private double[] _slot = [];
    private double _spin = 1;
    private double _spinVelocity;

    public OrbitLogosSimulation()
    {
        _rings[0].Direction = 1;
        _rings[0].Period = InnerPeriod;
        _rings[1].Direction = -1;
        _rings[1].Period = OuterPeriod;
    }

    /// <summary>Where the parked logo goes: 0 for the right, π/2 for the bottom.</summary>
    public double DetailAngle { get; set; }

    /// <summary>Multiplies the free turn. 0 holds the orbit still.</summary>
    public double Speed { get; set; } = 1;

    /// <summary>Holds the orbit still without a target.</summary>
    public bool Paused { get; set; }

    /// <summary>The pointer or focus is inside, a category is chosen or a card is open.</summary>
    public bool Held { get; set; }

    /// <summary>False under reduced motion: targets are reached at once and nothing turns.</summary>
    public bool CanMove { get; set; } = true;

    public int Count => _ringOf.Length;

    /// <summary>The share of the free turn left, 1 turning at full speed and 0 at rest.</summary>
    public double Spin => _spin;

    public int RingCount(int ring) => _rings[ring].Count;

    public bool HasTarget(int ring) => _rings[ring].HasTarget;

    public double RingAngle(int ring) => _rings[ring].Angle;

    public double TargetAngle(int ring) => _rings[ring].Target;

    public int RingOf(int index) => _ringOf[index];

    /// <summary>The angle of a logo's place on its ring when the ring is not turned.</summary>
    public double Slot(int index) => _slot[index];

    /// <summary>The current angle of a logo.</summary>
    public double Angle(int index) => _rings[_ringOf[index]].Angle + _slot[index];

    /// <summary>The offset of a logo from the centre, on a ring of the given radius.</summary>
    public (double X, double Y) Offset(int index, double innerRadius, double outerRadius)
    {
        var radius = _ringOf[index] == 0 ? innerRadius : outerRadius;
        var angle = Angle(index);
        return (radius * Math.Cos(angle), radius * Math.Sin(angle));
    }

    /// <summary>Places <paramref name="count"/> logos on the rings: about two fifths on the inner one, spread evenly.</summary>
    public void Build(int count)
    {
        var inner = count < 3 ? 0 : (int)Math.Round(count * 0.4);
        var outer = count - inner;
        _ringOf = new int[count];
        _slot = new double[count];
        _rings[0].Count = inner;
        _rings[1].Count = outer;
        var seen = new[] { 0, 0 };
        for (var i = 0; i < count; i++)
        {
            // Spread the inner logos through the list order so neighbours in a category sit on both rings.
            var onInner = inner > 0 && (i + 1) * inner / count > i * inner / count;
            var ring = onInner ? 0 : 1;
            _ringOf[i] = ring;
            var n = _rings[ring].Count;
            var phase = ring == 0 ? -Math.PI / 2 : -Math.PI / 2 + Math.PI / Math.Max(1, n);
            _slot[i] = phase + Tau * seen[ring]++ / n;
        }
        foreach (var ring in _rings)
        {
            ring.Angle = 0;
            ring.Velocity = 0;
            ring.HasTarget = false;
        }
        _spin = 1;
        _spinVelocity = 0;
    }

    /// <summary>
    /// Chooses where the rings go. A chosen logo (<paramref name="selected"/> ≥ 0) parks on its ring at
    /// <see cref="DetailAngle"/> with a touch of overshoot, and when it is on the outer ring the inner ring turns a gap
    /// between its logos to that angle so the connector stays clear. Otherwise a category (<paramref name="matches"/>
    /// not null) turns its logos toward the detail side. Without either the rings turn freely.
    /// </summary>
    public void Retarget(int selected, IReadOnlyList<bool>? matches, bool snap = false)
    {
        Span<double> desired = stackalloc double[2];
        Span<bool> has = stackalloc bool[2];
        Span<double> frequency = stackalloc double[2];
        Span<double> damping = stackalloc double[2];
        frequency[0] = frequency[1] = TurnFrequency;
        damping[0] = damping[1] = 1;

        if (matches is not null)
        {
            for (var ring = 0; ring < 2; ring++)
            {
                double sx = 0, sy = 0;
                var count = 0;
                for (var i = 0; i < _ringOf.Length; i++)
                {
                    if (_ringOf[i] == ring && i < matches.Count && matches[i])
                    {
                        sx += Math.Cos(_slot[i]);
                        sy += Math.Sin(_slot[i]);
                        count++;
                    }
                }
                if (count > 0 && Math.Sqrt(sx * sx + sy * sy) > 0.05 * count)
                {
                    desired[ring] = DetailAngle - Math.Atan2(sy, sx);
                    has[ring] = true;
                }
            }
        }

        if (selected >= 0 && selected < _ringOf.Length)
        {
            var ring = _ringOf[selected];
            desired[ring] = DetailAngle - _slot[selected];
            has[ring] = true;
            frequency[ring] = SelectFrequency;
            damping[ring] = SelectDamping;
            if (ring == 1 && _rings[0].Count > 0)
            {
                desired[0] = StepAsideAngle();
                has[0] = true;
            }
        }

        for (var ring = 0; ring < 2; ring++)
        {
            var state = _rings[ring];
            if (!has[ring] || state.Count == 0)
            {
                state.HasTarget = false;
                state.Velocity = 0;
                continue;
            }
            state.Target = state.Angle + Wrap(desired[ring] - state.Angle);
            state.HasTarget = true;
            state.Frequency = frequency[ring];
            state.Damping = damping[ring];
            if (snap || !CanMove)
            {
                state.Angle = state.Target;
                state.Velocity = 0;
            }
        }
    }

    /// <summary>
    /// Advances by <paramref name="dt"/> seconds. Returns whether anything is still moving: a ring turning freely, a ring
    /// on its way to a target or the spin factor still easing.
    /// </summary>
    public bool Step(double dt)
    {
        if (!CanMove)
        {
            _spin = 0;
            _spinVelocity = 0;
            return false;
        }
        dt = Math.Min(dt, 1.0 / 20);
        var anyTarget = _rings[0].HasTarget || _rings[1].HasTarget;
        var spinTarget = Held || Paused || Speed <= 0 || anyTarget ? 0.0 : 1.0;
        const int substeps = 2;
        var h = dt / substeps;
        for (var s = 0; s < substeps; s++)
        {
            var acceleration = SpinFrequency * SpinFrequency * (spinTarget - _spin) - 2 * SpinFrequency * _spinVelocity;
            _spinVelocity += acceleration * h;
            _spin += _spinVelocity * h;
            foreach (var ring in _rings)
            {
                if (ring.Count == 0)
                {
                    continue;
                }
                if (ring.HasTarget)
                {
                    var offset = ring.Target - ring.Angle;
                    var a = ring.Frequency * ring.Frequency * offset - 2 * ring.Damping * ring.Frequency * ring.Velocity;
                    ring.Velocity += a * h;
                    ring.Angle += ring.Velocity * h;
                }
                else
                {
                    ring.Angle += ring.Direction * Tau / ring.Period * Speed * Math.Max(0, _spin) * h;
                }
            }
        }

        var moving = false;
        if (Math.Abs(spinTarget - _spin) < 0.002 && Math.Abs(_spinVelocity) < 0.01)
        {
            _spin = spinTarget;
            _spinVelocity = 0;
        }
        else
        {
            moving = true;
        }
        foreach (var ring in _rings)
        {
            if (ring.Count == 0)
            {
                continue;
            }
            if (ring.HasTarget)
            {
                if (Math.Abs(ring.Target - ring.Angle) < 0.0002 && Math.Abs(ring.Velocity) < 0.002)
                {
                    ring.Angle = ring.Target;
                    ring.Velocity = 0;
                }
                else
                {
                    moving = true;
                }
            }
            else if (_spin > 0 && Speed > 0)
            {
                moving = true;
            }
        }
        return moving;
    }

    /// <summary>Wraps an angle difference into [-π, π], so a ring turns the short way round.</summary>
    public static double Wrap(double angle)
    {
        angle %= Tau;
        if (angle > Math.PI)
        {
            angle -= Tau;
        }
        else if (angle < -Math.PI)
        {
            angle += Tau;
        }
        return angle;
    }

    /// <summary>The inner ring angle that puts a gap between two of its logos on the detail side, nearest the current one.</summary>
    private double StepAsideAngle()
    {
        var ring = _rings[0];
        var n = ring.Count;
        double? best = null;
        var bestTurn = double.MaxValue;
        for (var i = 0; i < _ringOf.Length; i++)
        {
            if (_ringOf[i] != 0)
            {
                continue;
            }
            var middle = _slot[i] + Math.PI / n;
            var candidate = DetailAngle - middle;
            var turn = Math.Abs(Wrap(candidate - ring.Angle));
            if (turn < bestTurn)
            {
                bestTurn = turn;
                best = candidate;
            }
        }
        return best ?? ring.Angle;
    }

    private sealed class Ring
    {
        public int Count;
        public double Angle;
        public double Velocity;
        public double Target;
        public bool HasTarget;
        public double Frequency = TurnFrequency;
        public double Damping = 1;
        public double Direction = 1;
        public double Period = InnerPeriod;
    }
}
