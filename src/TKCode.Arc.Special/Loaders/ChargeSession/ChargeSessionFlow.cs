namespace TKCode.Arc;

/// <summary>
/// The energy packets of <see cref="ChargeSession"/> (Arc Charge session, design reference, measured from the preview's
/// arc-length tracks): a pool of 18 packets moving by distance (scene units) from the charger along the cable and on
/// through the pack to the fill front. They cruise at 300 u/s, ease out of the charger (0.45 → 1 over the first 65 u),
/// slow to 0.7 over the last 90 u before the front, and all speeds scale by <c>0.4 + 0.6·availability</c>. One leaves
/// every 216.7 ms at full availability (the emitter accumulates availability, so it slows and stops with the power).
/// A packet fades in over its first 28 u and out between 30 u and 8 u before the front; at 8 u it lands, recycles and
/// adds 0.15 to the front's flash, which decays with τ 0.2 s. Pure and allocation-free.
/// </summary>
internal sealed class ChargeSessionFlow
{
    /// <summary>Pool size.</summary>
    public const int PoolSize = 18;

    /// <summary>Cruising speed in scene units per second.</summary>
    public const double Cruise = 300;

    /// <summary>Seconds between packets at full availability (13 frames; 65 u apart at cruise).</summary>
    public const double Interval = 0.2167;

    /// <summary>Flash added by one landing.</summary>
    public const double FlashPerLanding = 0.15;

    /// <summary>Decay time constant of the flash.</summary>
    public const double FlashTau = 0.20;

    /// <summary>Distance before the front where a packet lands (its opacity reaches zero).</summary>
    public const double LandAt = 8;

    /// <summary>
    /// Where the emitter starts: fitted so that the first packet of a ramp from zero leaves after about 376 ms, as measured.
    /// </summary>
    public const double InitialEmitter = 0.37;

    private readonly double[] _distance = new double[PoolSize];
    private readonly bool[] _alive = new bool[PoolSize];
    private double _emitter = InitialEmitter;
    private bool _static;

    /// <summary>The front's extra brightness (opacity is 0.5 plus this).</summary>
    public double Flash { get; private set; }

    /// <summary>Packets in flight.</summary>
    public int Count { get; private set; }

    /// <summary>Packets landed since creation (tests read it).</summary>
    public int Landed { get; private set; }

    /// <summary>Whether the packets are laid out at rest (reduced motion) rather than moving.</summary>
    public bool IsStatic => _static;

    /// <summary>Nothing in flight and the flash gone: the flow needs no more frames.</summary>
    public bool IsIdle => Count == 0 && Flash <= 0;

    /// <summary>Whether pool slot <paramref name="index"/> holds a packet.</summary>
    public bool IsAlive(int index) => _alive[index];

    /// <summary>Distance along the path of the packet in slot <paramref name="index"/>.</summary>
    public double DistanceOf(int index) => _distance[index];

    /// <summary>Ease out of the charger: 0.45 at the plug to 1 after 65 u.</summary>
    public static double StartFactor(double distance) => 0.45 + 0.55 * ChargeSessionCurve.Smoothstep(0, 65, distance);

    /// <summary>Slow toward the front: 0.7 at the front to 1 from 90 u away.</summary>
    public static double EndFactor(double remaining) => 0.7 + 0.3 * ChargeSessionCurve.Smoothstep(0, 90, remaining);

    /// <summary>Speed in u/s at <paramref name="distance"/>, <paramref name="remaining"/> u before the front.</summary>
    public static double SpeedAt(double distance, double remaining, double availability) =>
        Cruise * StartFactor(distance) * EndFactor(remaining) * (0.4 + 0.6 * Math.Clamp(availability, 0, 1));

    /// <summary>Opacity of a moving packet: fade in over 28 u, fade out between 30 u and 8 u before the front.</summary>
    public static double OpacityAt(double distance, double remaining) =>
        ChargeSessionCurve.Smoothstep(0, 28, distance) * ChargeSessionCurve.Smoothstep(LandAt, 30, remaining);

    /// <summary>Opacity of the packet in slot <paramref name="index"/> for a path ending at <paramref name="end"/>.</summary>
    public double OpacityOf(int index, double end)
    {
        if (!_alive[index])
        {
            return 0;
        }
        if (_static)
        {
            return 1;
        }
        return OpacityAt(_distance[index], end - _distance[index]);
    }

    /// <summary>
    /// Advances <paramref name="dt"/> seconds with the path ending at <paramref name="end"/> u: moves and lands packets,
    /// decays the flash and emits by availability.
    /// </summary>
    public void Step(double dt, double availability, double end)
    {
        if (dt <= 0)
        {
            return;
        }
        if (_static)
        {
            Clear();
        }
        for (var i = 0; i < PoolSize; i++)
        {
            if (!_alive[i])
            {
                continue;
            }
            var s = _distance[i];
            s += SpeedAt(s, end - s, availability) * dt;
            _distance[i] = s;
            if (end - s <= LandAt)
            {
                _alive[i] = false;
                Count--;
                Landed++;
                Flash += FlashPerLanding;
            }
        }
        Flash = ChargeSessionSmoother.Approach(Flash, 0, dt, FlashTau);
        if (Flash < 1e-3)
        {
            Flash = 0;
        }
        var rate = Math.Clamp(availability, 0, 1) / Interval;
        _emitter += rate * dt;
        while (_emitter >= 1)
        {
            // A long frame can owe several packets: each starts as far along as it would have got since it was due, so
            // they keep their spacing instead of piling up at the plug.
            _emitter -= 1;
            var overdue = rate > 0 ? Math.Min(dt, _emitter / rate) : 0;
            Emit(SpeedAt(0, end, availability) * overdue);
        }
    }

    /// <summary>
    /// Lays the packets out at rest for reduced motion: from 30 u before the front back every 64 u while at least 28 u from
    /// the plug; <paramref name="visible"/> false empties the path (a stopped session).
    /// </summary>
    public void LayOut(double end, bool visible)
    {
        Clear();
        _static = true;
        if (!visible)
        {
            return;
        }
        for (var k = 0; k < PoolSize; k++)
        {
            var s = end - 30 - 64 * k;
            if (s < 28)
            {
                break;
            }
            _alive[k] = true;
            _distance[k] = s;
            Count++;
        }
    }

    /// <summary>Empties the path and the flash.</summary>
    public void Clear()
    {
        Array.Clear(_alive);
        Count = 0;
        Flash = 0;
        _static = false;
    }

    private void Emit(double distance)
    {
        for (var i = 0; i < PoolSize; i++)
        {
            if (!_alive[i])
            {
                _alive[i] = true;
                _distance[i] = distance;
                Count++;
                return;
            }
        }
    }
}
