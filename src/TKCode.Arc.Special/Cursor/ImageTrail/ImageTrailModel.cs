namespace TKCode.Arc;

/// <summary>
/// The trail of an <see cref="ImageTrail"/>: a fixed pool of slots, each an image dropped along the pointer's path that
/// pops in, rests and then sinks away. The pointer's path is measured, and a new image is dropped every
/// <see cref="Spacing"/> px of travel, cycling through the pictures. Pure and allocation free after construction.
/// </summary>
internal sealed class ImageTrailModel
{
    /// <summary>Seconds an image takes to pop in.</summary>
    public const double EnterSeconds = 0.22;

    /// <summary>Seconds an image takes to sink away.</summary>
    public const double ExitSeconds = 0.4;

    private double[] _x = [];
    private double[] _y = [];
    private double[] _age = [];
    private double[] _tilt = [];
    private int[] _picture = [];
    private bool[] _live = [];
    private int _next;
    private int _nextPicture;
    private double _travel;
    private double _lastX = double.NaN;
    private double _lastY = double.NaN;
    private uint _seed = 0x2545F491u;

    public ImageTrailModel(int capacity = 14) => Resize(capacity);

    /// <summary>Px of pointer travel between two images.</summary>
    public double Spacing { get; set; } = 64;

    /// <summary>Seconds an image stays, from its drop to the start of its exit.</summary>
    public double Lifetime { get; set; } = 0.9;

    /// <summary>How many pictures there are to cycle through.</summary>
    public int Pictures { get; set; }

    /// <summary>The number of slots; the oldest image gives way when a new one needs room.</summary>
    public int Capacity => _live.Length;

    /// <summary>The slot the next image goes into; slots are drawn from here round, oldest first.</summary>
    public int Oldest => _next;

    /// <summary>The number of images on screen.</summary>
    public int LiveCount
    {
        get
        {
            var count = 0;
            foreach (var live in _live)
            {
                if (live)
                {
                    count++;
                }
            }
            return count;
        }
    }

    /// <summary>The total number of images dropped since the trail began.</summary>
    public int Dropped { get; private set; }

    public bool IsLive(int slot) => _live[slot];

    public double X(int slot) => _x[slot];

    public double Y(int slot) => _y[slot];

    public double Tilt(int slot) => _tilt[slot];

    public int Picture(int slot) => _picture[slot];

    public double Age(int slot) => _age[slot];

    /// <summary>Changes the number of slots, clearing the trail.</summary>
    public void Resize(int capacity)
    {
        capacity = Math.Max(1, capacity);
        _x = new double[capacity];
        _y = new double[capacity];
        _age = new double[capacity];
        _tilt = new double[capacity];
        _picture = new int[capacity];
        _live = new bool[capacity];
        _next = 0;
    }

    /// <summary>Removes every image and forgets the pointer's last place.</summary>
    public void Clear()
    {
        Array.Clear(_live);
        Lift();
    }

    /// <summary>The pointer left or was lifted: the next move starts a new path instead of drawing a line from here.</summary>
    public void Lift()
    {
        _lastX = double.NaN;
        _lastY = double.NaN;
        _travel = 0;
    }

    /// <summary>
    /// The pointer moved to (<paramref name="x"/>, <paramref name="y"/>). The first point of a path drops an image at
    /// once; after that one is dropped every <see cref="Spacing"/> px, placed along the path. Returns how many were dropped.
    /// </summary>
    public int MoveTo(double x, double y)
    {
        if (Pictures <= 0)
        {
            return 0;
        }
        if (double.IsNaN(_lastX))
        {
            _lastX = x;
            _lastY = y;
            _travel = 0;
            Drop(x, y, 0, 0);
            return 1;
        }
        var dx = x - _lastX;
        var dy = y - _lastY;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance <= 0)
        {
            return 0;
        }
        var spacing = Math.Max(4, Spacing);
        var count = (int)Math.Floor((_travel + distance) / spacing);
        // Drop images at the exact points the spacing falls on, so a fast flick still leaves an even trail; when one
        // move covers more than the pool holds, only the last ones are dropped.
        var skip = Math.Max(0, count - Capacity);
        var dropped = 0;
        for (var k = skip; k < count; k++)
        {
            var t = (spacing - _travel + k * spacing) / distance;
            Drop(_lastX + dx * t, _lastY + dy * t, dx / distance, dy / distance);
            dropped++;
        }
        _travel = _travel + distance - count * spacing;
        _lastX = x;
        _lastY = y;
        return dropped;
    }

    /// <summary>Drops one image at (<paramref name="x"/>, <paramref name="y"/>), tilted a little toward the way it was moving.</summary>
    public void Drop(double x, double y, double directionX, double directionY)
    {
        if (Pictures <= 0)
        {
            return;
        }
        var slot = _next;
        _next = (_next + 1) % Capacity;
        _x[slot] = x;
        _y[slot] = y;
        _age[slot] = 0;
        // A small random tilt, leaning with horizontal movement.
        _tilt[slot] = (Next() - 0.5) * 12 + directionX * 4;
        _picture[slot] = _nextPicture % Pictures;
        _nextPicture = (_nextPicture + 1) % Math.Max(1, Pictures);
        _live[slot] = true;
        Dropped++;
    }

    /// <summary>Ages every image by <paramref name="dt"/> seconds. Returns whether any image is still on screen.</summary>
    public bool Step(double dt)
    {
        var any = false;
        var end = Lifetime + ExitSeconds;
        for (var i = 0; i < _live.Length; i++)
        {
            if (!_live[i])
            {
                continue;
            }
            _age[i] += dt;
            if (_age[i] >= end)
            {
                _live[i] = false;
                continue;
            }
            any = true;
        }
        return any;
    }

    /// <summary>
    /// How slot <paramref name="slot"/> looks at its age: opacity, scale and how far it has sunk (px). With
    /// <paramref name="moves"/> false (reduced motion) it only fades.
    /// </summary>
    public void Pose(int slot, bool moves, out double opacity, out double scale, out double sink)
    {
        var age = _age[slot];
        opacity = 1;
        scale = 1;
        sink = 0;
        if (age < EnterSeconds)
        {
            var t = age / EnterSeconds;
            opacity = t;
            if (moves)
            {
                // Pops in past full size and settles (an ease with a small overshoot).
                const double overshoot = 1.9;
                var u = t - 1;
                scale = 0.6 + 0.4 * (1 + (overshoot + 1) * u * u * u + overshoot * u * u);
            }
            return;
        }
        var exit = age - Lifetime;
        if (exit > 0)
        {
            var t = Math.Min(1, exit / ExitSeconds);
            opacity = 1 - t;
            if (moves)
            {
                scale = 1 - 0.18 * t * t;
                sink = 22 * t * t;
            }
        }
    }

    private double Next()
    {
        _seed ^= _seed << 13;
        _seed ^= _seed >> 17;
        _seed ^= _seed << 5;
        return (_seed & 0xFFFFFF) / (double)0x1000000;
    }
}
