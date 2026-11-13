namespace TKCode.Arc;

/// <summary>
/// The physics of a <see cref="PrizeWheel"/>: where each segment sits, which one a weighted draw lands on, how a spin
/// travels from a throw or a planned duration to a spot inside the chosen segment, the idle drift, the slow coast of a
/// flick that was too gentle, and the pointer that bends against the pegs. Angles are degrees, clockwise, with 0 at the
/// pointer (the top); the wheel's drawing uses a 400-unit box with the centre at (200, 200). Pure and allocation free per
/// frame.
/// </summary>
internal sealed class PrizeWheelModel
{
    // --- the spin -----------------------------------------------------------------------------------------------------

    /// <summary>Spins thrown from the hub, by a key or by "Spin again" build their speed over this long (seconds).</summary>
    public const double RampSeconds = 0.1;

    /// <summary>A flick slower than this (degrees per second) does not spin the wheel.</summary>
    public const double WeakThrow = 260;

    /// <summary>The fastest throw the wheel accepts (degrees per second).</summary>
    public const double MaxThrow = 1620;

    /// <summary>The speed of a spin started without a gesture to measure: assistive activation and "Spin again".</summary>
    public const double MediumThrow = 970;

    /// <summary>The idle drift before a spin, degrees per second at speed 1 (a turn in 84 s).</summary>
    public const double DriftRate = 4.297;

    /// <summary>How far a full charge winds the wheel back before the throw (0.24 rad).</summary>
    public const double WindBack = 13.75;

    /// <summary>How long holding the hub takes to charge fully (seconds).</summary>
    public const double ChargeSeconds = 1.1;

    /// <summary>The velocity of a gentle flick decays with this time constant (seconds) while the wheel coasts.</summary>
    private const double CoastTau = 0.46;

    // --- the pointer --------------------------------------------------------------------------------------------------

    /// <summary>The pointer's largest bend either way (degrees); it stops there.</summary>
    public const double MaxFlap = 33.4;

    /// <summary>Stiffness of the pointer's spring (per second squared).</summary>
    private const double FlapStiffness = 1025;

    /// <summary>Damping of the pointer's spring (per second): a short, lively wobble.</summary>
    private const double FlapDamping = 15.5;

    // The pointer is a tapered capsule hanging from its pivot: a round head on the pivot that narrows to a round tip.
    internal const double PivotX = 200;
    internal const double PivotY = 7;
    internal const double HeadRadius = 6.5;
    internal const double TipRadius = 1.5;
    internal const double TipLength = 21;

    // The pegs sit on the rim, one at every slice edge.
    internal const double CentreX = 200;
    internal const double CentreY = 200;
    internal const double PegDistance = 170;
    internal const double PegRadius = 3.6;

    private static readonly double s_taper = (HeadRadius - TipRadius) / TipLength;
    private static readonly double s_side = Math.Sqrt(1 - s_taper * s_taper);

    private double[] _starts = [];
    private double[] _sweeps = [];
    private double[] _odds = [];
    private Motion _motion = Motion.Free;
    private double _from;
    private double _to;
    private double _duration;
    private double _elapsed;
    private bool _ramp;
    private double _coast;
    private double _pointerWheel;
    private double _flap;
    private double _flapVelocity;
    private bool _pressed;

    private enum Motion
    {
        /// <summary>Drifting at <see cref="Drift"/>, or still; a person may also be turning it (<see cref="TurnTo"/>).</summary>
        Free,

        /// <summary>A spin on its way to <see cref="Target"/>.</summary>
        Spinning,

        /// <summary>A gentle flick slowing down into the drift.</summary>
        Coasting
    }

    /// <summary>The number of segments.</summary>
    public int Count => _starts.Length;

    /// <summary>The wheel's turn in degrees, clockwise (the face is drawn at this angle).</summary>
    public double Rotation { get; private set; }

    /// <summary>The pointer's bend in degrees (negative: its tip pushed right by a wheel turning clockwise).</summary>
    public double Flap => _flap;

    /// <summary>The idle drift in degrees per second (0 holds the wheel still); applies only while the wheel is free.</summary>
    public double Drift { get; set; }

    /// <summary>Whether the pointer reacts to the pegs (off under reduced motion: no wobble).</summary>
    public bool IsPointerLive { get; set; } = true;

    /// <summary>True from the start of a spin until the wheel stops.</summary>
    public bool IsSpinning => _motion == Motion.Spinning;

    /// <summary>True while a gentle flick slows down.</summary>
    public bool IsCoasting => _motion == Motion.Coasting;

    /// <summary>The segment a running spin will land on, or the last one it landed on; -1 before any.</summary>
    public int Target { get; private set; } = -1;

    /// <summary>The number of pegs that have passed the pointer, either way, since the wheel was loaded.</summary>
    public int Ticks { get; private set; }

    /// <summary>How long the running spin lasts from its start, in seconds (with its speed ramp).</summary>
    public double SpinSeconds => _duration + (_ramp ? RampSeconds / 2 : 0);

    /// <summary>The distance of the running spin in degrees (positive clockwise).</summary>
    public double SpinDistance => _to - _from;

    /// <summary>Where segment <paramref name="i"/> starts, measured on the wheel.</summary>
    public double Start(int i) => _starts[i];

    /// <summary>How wide segment <paramref name="i"/> is.</summary>
    public double Sweep(int i) => _sweeps[i];

    /// <summary>A segment's share of the total weight (0 to 1).</summary>
    public double Chance(int i)
    {
        var total = 0.0;
        foreach (var odd in _odds)
        {
            total += odd;
        }
        return total > 0 ? _odds[i] / total : 0;
    }

    /// <summary>
    /// Lays the segments out. <paramref name="weights"/> are each segment's odds; when <paramref name="sizeByWeight"/> is
    /// set the slices are as wide as their odds, otherwise they are equal.
    /// </summary>
    public void Load(double[] weights, bool sizeByWeight)
    {
        var count = weights.Length;
        _starts = new double[count];
        _sweeps = new double[count];
        _odds = new double[count];
        var total = 0.0;
        for (var i = 0; i < count; i++)
        {
            _odds[i] = double.IsFinite(weights[i]) ? Math.Max(0, weights[i]) : 0;
            total += _odds[i];
        }
        var start = 0.0;
        for (var i = 0; i < count; i++)
        {
            var share = sizeByWeight && total > 0 ? _odds[i] / total : 1.0 / count;
            _starts[i] = start;
            _sweeps[i] = share * 360;
            start += _sweeps[i];
        }
        if (Target >= count)
        {
            Target = -1;
        }
    }

    /// <summary>
    /// Draws a segment by its odds: <paramref name="random"/> (0 to 1) falls into the segments' shares of the total
    /// weight. Segments with no weight are never drawn; when every weight is zero the draw is even.
    /// </summary>
    public int Pick(double random)
    {
        var count = _odds.Length;
        if (count == 0)
        {
            return -1;
        }
        var total = 0.0;
        foreach (var odd in _odds)
        {
            total += odd;
        }
        if (total <= 0)
        {
            return Math.Clamp((int)(random * count), 0, count - 1);
        }
        var point = Math.Clamp(random, 0, 0.999999999) * total;
        for (var i = 0; i < count; i++)
        {
            if (_odds[i] <= 0)
            {
                continue;
            }
            point -= _odds[i];
            if (point < 0)
            {
                return i;
            }
        }
        for (var i = count - 1; i >= 0; i--)
        {
            if (_odds[i] > 0)
            {
                return i;
            }
        }
        return count - 1;
    }

    /// <summary>The segment under the pointer at the current <see cref="Rotation"/>.</summary>
    public int AtPointer() => SegmentAt(Wrap(-Rotation));

    /// <summary>The segment that covers <paramref name="angle"/> on the wheel.</summary>
    public int SegmentAt(double angle)
    {
        angle = Wrap(angle);
        for (var i = 0; i < _starts.Length; i++)
        {
            if (angle >= _starts[i] && angle < _starts[i] + _sweeps[i])
            {
                return i;
            }
        }
        return _starts.Length - 1;
    }

    /// <summary>
    /// The distance (degrees) a throw at <paramref name="speed"/> degrees per second carries the wheel before friction
    /// stops it: never less than two and a half turns, and longer, and longer-lasting, the faster the throw.
    /// </summary>
    public static double NaturalDistance(double speed)
    {
        speed = Math.Abs(speed);
        return Math.Max(900, speed * (3.6 + 2.2 * speed / 1000) / 3);
    }

    /// <summary>
    /// Starts a spin thrown at <paramref name="velocity"/> degrees per second (negative turns the wheel
    /// counter-clockwise) that lands on segment <paramref name="index"/>, at a spot <paramref name="spot"/> (0 to 1)
    /// across its inner three quarters. The landing is the one nearest the throw's natural distance, and the spin lasts
    /// as long as a cubic slow-down from exactly that speed needs, so the wheel leaves the hand at the speed it was
    /// thrown. With <paramref name="ramp"/> the speed builds over <see cref="RampSeconds"/> instead.
    /// </summary>
    public void Throw(int index, double spot, double velocity, bool ramp)
    {
        var speed = Math.Clamp(Math.Abs(velocity), 1, MaxThrow);
        var direction = velocity < 0 ? -1 : 1;
        Target = index;
        _from = Rotation;
        // The pointer reads the wheel at -rotation, so the wheel must end at -aim (mod 360).
        var aim = Aim(index, spot);
        var natural = NaturalDistance(speed);
        var end = -aim + Math.Round((_from + direction * natural + aim) / 360) * 360;
        if ((end - _from) * direction < 360)
        {
            end += 360 * direction;
        }
        _to = end;
        _duration = 3 * Math.Abs(end - _from) / speed;
        _ramp = ramp;
        _elapsed = 0;
        _motion = Motion.Spinning;
    }

    /// <summary>
    /// Starts a spin that lands on segment <paramref name="index"/>, at a spot <paramref name="spot"/> (0 to 1) across
    /// it, after at least <paramref name="turns"/> full turns over <paramref name="seconds"/>: the path of a spin started
    /// from code. Its speed builds over <see cref="RampSeconds"/> like a spin thrown from the hub.
    /// </summary>
    public void Plan(int index, double spot, int turns, double seconds)
    {
        Target = index;
        _from = Rotation;
        var end = -Aim(index, spot);
        var least = _from + Math.Max(0, turns) * 360;
        end += Math.Ceiling((least - end) / 360) * 360;
        _to = end;
        _duration = Math.Max(0.01, seconds - RampSeconds / 2);
        _ramp = true;
        _elapsed = 0;
        _motion = Motion.Spinning;
    }

    /// <summary>Puts the wheel straight on segment <paramref name="index"/> without turning (reduced motion, no motion).</summary>
    public void Jump(int index, double spot)
    {
        Target = index;
        _from = Rotation;
        var end = -Aim(index, spot);
        end += Math.Round((_from - end) / 360) * 360;
        _to = end;
        Rotation = end;
        _pointerWheel = end;
        _motion = Motion.Free;
        _flap = 0;
        _flapVelocity = 0;
    }

    /// <summary>Lets a gentle flick coast at <paramref name="velocity"/> degrees per second, slowing into the drift.</summary>
    public void Coast(double velocity)
    {
        _coast = velocity;
        _motion = Math.Abs(velocity) > 1 ? Motion.Coasting : Motion.Free;
    }

    /// <summary>Stops any spin or coast where the wheel is (a person grabbed it or holds the hub).</summary>
    public void Hold() => _motion = Motion.Free;

    /// <summary>Turns the wheel to <paramref name="rotation"/> (dragging, winding back); the pointer follows on the next step.</summary>
    public void TurnTo(double rotation) => Rotation = rotation;

    /// <summary>
    /// Advances the wheel and the pointer by <paramref name="dt"/> seconds. Returns how many pegs passed the pointer.
    /// </summary>
    public int Step(double dt)
    {
        if (dt <= 0)
        {
            return 0;
        }
        switch (_motion)
        {
            case Motion.Spinning:
            {
                _elapsed += dt;
                var u = Math.Min(1, Clock(_elapsed, _ramp) / _duration);
                Rotation = _from + (_to - _from) * Ease(u);
                if (u >= 1)
                {
                    Rotation = _to;
                    _motion = Motion.Free;
                }
                break;
            }
            case Motion.Coasting:
            {
                _coast *= Math.Exp(-dt / CoastTau);
                Rotation += (_coast + Drift) * dt;
                if (Math.Abs(_coast) < 1)
                {
                    _motion = Motion.Free;
                }
                break;
            }
            default:
                Rotation += Drift * dt;
                break;
        }
        var before = _pointerWheel;
        var after = Rotation;
        _pointerWheel = after;
        var ticks = PegsBetween(before, after);
        Ticks += ticks;
        if (IsPointerLive)
        {
            StepPointer(dt, before, after);
        }
        else
        {
            _flap = 0;
            _flapVelocity = 0;
        }
        return ticks;
    }

    /// <summary>Whether the pointer still swings (the wheel may have stopped).</summary>
    public bool IsFlapMoving => Math.Abs(_flapVelocity) > 0.5 || (Math.Abs(_flap) > 0.05 && !_pressed);

    /// <summary>A wheel slowing under friction: velocity falls as (1 − t)², so the last turn crawls past the final pegs.</summary>
    public static double Ease(double t)
    {
        var inverse = 1 - t;
        return 1 - inverse * inverse * inverse;
    }

    /// <summary>The spin's clock: with a ramp it runs slow for the first <see cref="RampSeconds"/> (speed builds linearly).</summary>
    private static double Clock(double t, bool ramp)
    {
        if (!ramp)
        {
            return t;
        }
        return t < RampSeconds ? t * t / (2 * RampSeconds) : t - RampSeconds / 2;
    }

    /// <summary>The spot on the wheel the pointer should rest on: inside the segment's inner three quarters.</summary>
    private double Aim(int index, double spot) => _starts[index] + _sweeps[index] * (0.125 + 0.75 * Math.Clamp(spot, 0, 1));

    /// <summary>
    /// How deep the nearest peg overlaps the pointer bent to <paramref name="flap"/> with the wheel at
    /// <paramref name="wheel"/> (0 when nothing touches).
    /// </summary>
    internal double ContactAt(double flap, double wheel)
    {
        var deepest = 0.0;
        for (var i = 0; i < _starts.Length; i++)
        {
            var angle = Signed(_starts[i] + wheel);
            if (Math.Abs(angle) > 14)
            {
                continue;
            }
            deepest = Math.Max(deepest, -Clearance(flap, angle));
        }
        return deepest;
    }

    /// <summary>
    /// The angle the pointer rests at when a peg at screen angle <paramref name="peg"/> pushes it slowly from the left
    /// (a clockwise wheel); 0 when the peg does not reach it.
    /// </summary>
    internal static double RestingBend(double peg)
    {
        // Come from far right toward rest until the pointer touches the peg: it rests on the peg's right side.
        var clear = -70.0;
        for (var flap = -70.0; flap <= 0; flap += 0.5)
        {
            if (Clearance(flap, peg) < 0)
            {
                var touching = flap;
                for (var i = 0; i < 18; i++)
                {
                    var middle = (clear + touching) / 2;
                    if (Clearance(middle, peg) >= 0)
                    {
                        clear = middle;
                    }
                    else
                    {
                        touching = middle;
                    }
                }
                return clear;
            }
            clear = flap;
        }
        return 0;
    }

    private void StepPointer(double dt, double before, double after)
    {
        // Substeps follow the wheel so a peg cannot skip through the pointer at full speed.
        var steps = Math.Clamp((int)Math.Ceiling(Math.Abs(after - before) / 1.5), 4, 48);
        var h = dt / steps;
        _pressed = false;
        for (var s = 1; s <= steps; s++)
        {
            var wheel = before + (after - before) * s / steps;
            var start = _flap;
            var accel = -FlapStiffness * _flap - FlapDamping * _flapVelocity;
            _flapVelocity += accel * h;
            _flap += _flapVelocity * h;
            for (var i = 0; i < _starts.Length; i++)
            {
                var peg = Signed(_starts[i] + wheel);
                if (Math.Abs(peg) > 14 || Clearance(_flap, peg) >= 0)
                {
                    continue;
                }
                // The peg pushes the pointer aside (an inelastic push: the pointer moves with it).
                _flap = Resolve(_flap, peg, PegLocalX(_flap, peg) < 0 ? -1 : 1);
                _flapVelocity = (_flap - start) / h;
                _pressed = s == steps;
            }
            if (_flap > MaxFlap)
            {
                _flap = MaxFlap;
                _flapVelocity = Math.Min(0, _flapVelocity);
            }
            else if (_flap < -MaxFlap)
            {
                _flap = -MaxFlap;
                _flapVelocity = Math.Max(0, _flapVelocity);
            }
        }
        if (Math.Abs(_flap) < 0.01 && Math.Abs(_flapVelocity) < 0.3)
        {
            _flap = 0;
            _flapVelocity = 0;
        }
    }

    /// <summary>The nearest bend from <paramref name="flap"/>, on the pointer's side of the peg, at which they just touch.</summary>
    /// <remarks>A peg on the pointer's left pushes its tip right (<paramref name="direction"/> −1), and the other way round.</remarks>
    private static double Resolve(double flap, double peg, int direction)
    {
        var touching = flap;
        var clear = flap + direction * 80;
        for (var i = 0; i < 18; i++)
        {
            var middle = (touching + clear) / 2;
            if (Clearance(middle, peg) >= 0)
            {
                clear = middle;
            }
            else
            {
                touching = middle;
            }
        }
        return clear;
    }

    /// <summary>The gap between the pointer bent to <paramref name="flap"/> and a peg at screen angle <paramref name="peg"/> (negative: overlap).</summary>
    private static double Clearance(double flap, double peg)
    {
        var (x, y) = Local(flap, peg);
        x = Math.Abs(x);
        // Signed distance to a capsule whose radius narrows from the head (at the pivot) to the tip.
        var k = -s_taper * x + s_side * y;
        double distance;
        if (k < 0)
        {
            distance = Math.Sqrt(x * x + y * y) - HeadRadius;
        }
        else if (k > s_side * TipLength)
        {
            var dy = y - TipLength;
            distance = Math.Sqrt(x * x + dy * dy) - TipRadius;
        }
        else
        {
            distance = s_side * x + s_taper * y - HeadRadius;
        }
        return distance - PegRadius;
    }

    private static double PegLocalX(double flap, double peg) => Local(flap, peg).X;

    /// <summary>A peg's centre in the pointer's own frame: the pivot at the origin, the tip along +y.</summary>
    private static (double X, double Y) Local(double flap, double peg)
    {
        var a = peg * Math.PI / 180;
        var dx = CentreX + PegDistance * Math.Sin(a) - PivotX;
        var dy = CentreY - PegDistance * Math.Cos(a) - PivotY;
        var t = flap * Math.PI / 180;
        var cos = Math.Cos(t);
        var sin = Math.Sin(t);
        return (dx * cos + dy * sin, -dx * sin + dy * cos);
    }

    /// <summary>The number of segment edges that crossed the pointer, either way, while the wheel turned from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private int PegsBetween(double from, double to)
    {
        if (to == from || _starts.Length == 0)
        {
            return 0;
        }
        var low = Math.Min(from, to);
        var high = Math.Max(from, to);
        var count = 0;
        foreach (var start in _starts)
        {
            // An edge at wheel angle a is under the pointer when rotation = -a (mod 360).
            var first = -start;
            first += Math.Ceiling((low - first) / 360) * 360;
            if (first <= low)
            {
                first += 360;
            }
            if (first <= high)
            {
                count += 1 + (int)Math.Floor((high - first) / 360);
            }
        }
        return count;
    }

    private static double Wrap(double angle)
    {
        angle %= 360;
        return angle < 0 ? angle + 360 : angle;
    }

    private static double Signed(double angle)
    {
        angle = Wrap(angle);
        return angle > 180 ? angle - 360 : angle;
    }
}
