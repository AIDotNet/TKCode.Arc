namespace TKCode.Arc;

/// <summary>
/// The physics of a <see cref="PrizeWheel"/>: where each segment sits, which one a weighted draw lands on, the
/// decelerating path from the current angle to a spot inside the chosen segment, and the pointer's flapper as the pegs
/// at the segment edges strike it. Angles are degrees, clockwise, with 0 at the pointer (the top). Pure and allocation
/// free per frame.
/// </summary>
internal sealed class PrizeWheelModel
{
    /// <summary>How far before the pointer a peg first touches the flapper.</summary>
    public const double ContactWindow = 9;

    /// <summary>The flapper's largest swing when a peg pushes it.</summary>
    public const double MaxFlap = 26;

    private double[] _starts = [];
    private double[] _sweeps = [];
    private double[] _odds = [];
    private double _from;
    private double _to;
    private double _elapsed;
    private double _duration;
    private double _flap;
    private double _flapVelocity;

    /// <summary>The number of segments.</summary>
    public int Count => _starts.Length;

    /// <summary>The wheel's turn in degrees (it only grows; the face is drawn at this angle).</summary>
    public double Rotation { get; private set; }

    /// <summary>The flapper's swing in degrees (positive: pushed the way the wheel turns).</summary>
    public double Flap => _flap;

    /// <summary>True from the start of a spin until the wheel stops.</summary>
    public bool IsSpinning { get; private set; }

    /// <summary>The segment a running spin will land on, or the last one it landed on; -1 before any.</summary>
    public int Target { get; private set; } = -1;

    /// <summary>The number of pegs that have passed the pointer since the wheel was loaded.</summary>
    public int Ticks { get; private set; }

    /// <summary>Where segment <paramref name="i"/> starts, measured on the wheel.</summary>
    public double Start(int i) => _starts[i];

    /// <summary>How wide segment <paramref name="i"/> is.</summary>
    public double Sweep(int i) => _sweeps[i];

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
    /// Starts a spin that lands on segment <paramref name="index"/>, at a spot <paramref name="spot"/> (0 to 1) across it
    /// (kept away from the edges), after at least <paramref name="turns"/> full turns over <paramref name="seconds"/>.
    /// </summary>
    public void Plan(int index, double spot, int turns, double seconds)
    {
        Target = index;
        _from = Rotation;
        var aim = _starts[index] + _sweeps[index] * (0.18 + 0.64 * Math.Clamp(spot, 0, 1));
        // The pointer reads the wheel at -rotation, so the wheel must end at -aim (mod 360), after the full turns.
        var end = -aim;
        var least = _from + Math.Max(0, turns) * 360;
        end += Math.Ceiling((least - end) / 360) * 360;
        _to = end;
        _elapsed = 0;
        _duration = Math.Max(0.01, seconds);
        IsSpinning = true;
    }

    /// <summary>Puts the wheel straight on segment <paramref name="index"/> without turning (reduced motion, no motion).</summary>
    public void Jump(int index, double spot)
    {
        Plan(index, spot, 0, 0.01);
        Rotation = _to;
        _from = _to;
        IsSpinning = false;
        _flap = 0;
        _flapVelocity = 0;
    }

    /// <summary>
    /// Advances the spin and the flapper by <paramref name="dt"/> seconds. Returns how many pegs passed the pointer.
    /// </summary>
    public int Step(double dt)
    {
        var before = Rotation;
        if (IsSpinning)
        {
            _elapsed += dt;
            var t = Math.Min(1, _elapsed / _duration);
            Rotation = _from + (_to - _from) * Ease(t);
            if (t >= 1)
            {
                Rotation = _to;
                IsSpinning = false;
            }
        }
        var ticks = PegsBetween(before, Rotation);
        Ticks += ticks;
        StepFlap(dt, Rotation - before);
        return ticks;
    }

    /// <summary>Whether the flapper still swings (the wheel may have stopped).</summary>
    public bool IsFlapMoving => Math.Abs(_flap) > 0.05 || Math.Abs(_flapVelocity) > 0.5;

    /// <summary>A wheel slowing under friction: fast at first, then a long, gentle stop (cubic ease out).</summary>
    public static double Ease(double t)
    {
        var inverse = 1 - t;
        return 1 - inverse * inverse * inverse;
    }

    /// <summary>How far the nearest peg ahead of the pointer pushes the flapper at this rotation.</summary>
    public double Contact()
    {
        var push = 0.0;
        for (var i = 0; i < _starts.Length; i++)
        {
            // The peg's place on screen, from -180 to 180, negative before the pointer.
            var screen = Signed(_starts[i] + Rotation);
            if (screen > -ContactWindow && screen <= 0)
            {
                push = Math.Max(push, (screen + ContactWindow) / ContactWindow * MaxFlap);
            }
        }
        return push;
    }

    private void StepFlap(double dt, double turned)
    {
        if (dt <= 0)
        {
            return;
        }
        var push = Contact();
        if (_flap <= push && turned > 0 && push > 0)
        {
            // A peg holds the flapper: it rides on the peg.
            _flapVelocity = (push - _flap) / dt;
            _flap = push;
            return;
        }
        // Free: a stiff, lightly damped spring brings the flapper back with a little wobble.
        const double stiffness = 900;
        const double damping = 22;
        var steps = Math.Max(1, (int)Math.Ceiling(dt / (1.0 / 240)));
        var h = dt / steps;
        for (var s = 0; s < steps; s++)
        {
            var accel = -stiffness * _flap - damping * _flapVelocity;
            _flapVelocity += accel * h;
            _flap += _flapVelocity * h;
        }
        if (_flap < push)
        {
            _flap = push;
            _flapVelocity = 0;
        }
        if (!IsFlapMoving)
        {
            _flap = push;
            _flapVelocity = 0;
        }
    }

    /// <summary>The number of segment edges that crossed the pointer while the wheel turned from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private int PegsBetween(double from, double to)
    {
        if (to <= from || _starts.Length == 0)
        {
            return 0;
        }
        var count = 0;
        foreach (var start in _starts)
        {
            // An edge at wheel angle a is under the pointer when rotation = -a (mod 360).
            var first = -start;
            first += Math.Ceiling((from - first) / 360) * 360;
            if (first <= from)
            {
                first += 360;
            }
            if (first <= to)
            {
                count += 1 + (int)Math.Floor((to - first) / 360);
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
