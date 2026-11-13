namespace TKCode.Arc;

/// <summary>
/// The motion of a <see cref="MagnifyDock"/>: each icon's size follows a lens centred on the pointer (or the focused
/// icon) on its own spring, the row re-flows around the grown icons and stays centred, and a launched icon hops a few
/// times. Positions are along the row, in px from its left edge; icons are bottom-aligned. Pure and allocation free per
/// frame.
/// </summary>
internal sealed class MagnifyDockModel
{
    /// <summary>Seconds one hop of the launch bounce takes.</summary>
    public const double HopSeconds = 0.4;

    private static readonly double[] HopHeights = [0.62, 0.3, 0.12];

    private const double Stiffness = 420;
    private const double Damping = 34;

    private double[] _scale = [];
    private double[] _velocity = [];
    private double[] _bounce = [];
    private double[] _center = [];

    /// <summary>The number of icons.</summary>
    public int Count => _scale.Length;

    /// <summary>The side of an icon at rest, px.</summary>
    public double ItemSize { get; set; } = 48;

    /// <summary>The space between icons, px.</summary>
    public double Gap { get; set; } = 8;

    /// <summary>The scale of the icon right under the lens.</summary>
    public double Magnification { get; set; } = 1.8;

    /// <summary>How far from its centre the lens still grows icons, px.</summary>
    public double Reach { get; set; } = 150;

    /// <summary>Whether sizes follow springs (false: they jump to their targets, for reduced motion).</summary>
    public bool Springs { get; set; } = true;

    /// <summary>The lens centre along the row (px from its left edge), or NaN when there is none.</summary>
    public double Lens { get; set; } = double.NaN;

    /// <summary>The row's width at rest.</summary>
    public double RestWidth => Count == 0 ? 0 : Count * ItemSize + (Count - 1) * Gap;

    /// <summary>The row's current width, with the grown icons.</summary>
    public double Width
    {
        get
        {
            var width = Count == 0 ? 0 : (Count - 1) * Gap;
            for (var i = 0; i < Count; i++)
            {
                width += ItemSize * _scale[i];
            }
            return width;
        }
    }

    /// <summary>Sets the number of icons; existing icons keep their state.</summary>
    public void Resize(int count)
    {
        if (count == _scale.Length)
        {
            return;
        }
        var scale = new double[count];
        var velocity = new double[count];
        var bounce = new double[count];
        var keep = Math.Min(count, _scale.Length);
        Array.Copy(_scale, scale, keep);
        Array.Copy(_velocity, velocity, keep);
        Array.Copy(_bounce, bounce, keep);
        for (var i = keep; i < count; i++)
        {
            scale[i] = 1;
            bounce[i] = -1;
        }
        _scale = scale;
        _velocity = velocity;
        _bounce = bounce;
        _center = new double[count];
    }

    /// <summary>The current scale of icon <paramref name="i"/>.</summary>
    public double Scale(int i) => _scale[i];

    /// <summary>The current centre of icon <paramref name="i"/> along the row; valid after <see cref="Layout"/>.</summary>
    public double Center(int i) => _center[i];

    /// <summary>The centre of icon <paramref name="i"/> at rest, along the row.</summary>
    public double RestCenter(int i) => i * (ItemSize + Gap) + ItemSize / 2;

    /// <summary>How high icon <paramref name="i"/> is lifted by its launch bounce, px.</summary>
    public double Lift(int i)
    {
        var t = _bounce[i];
        if (t < 0)
        {
            return 0;
        }
        var hop = (int)(t / HopSeconds);
        if (hop >= HopHeights.Length)
        {
            return 0;
        }
        var phase = (t - hop * HopSeconds) / HopSeconds;
        return HopHeights[hop] * ItemSize * Math.Sin(Math.PI * phase);
    }

    /// <summary>Whether icon <paramref name="i"/> is bouncing.</summary>
    public bool IsBouncing(int i) => _bounce[i] >= 0;

    /// <summary>Starts icon <paramref name="i"/>'s launch bounce.</summary>
    public void Launch(int i)
    {
        if (i >= 0 && i < Count)
        {
            _bounce[i] = 0;
        }
    }

    /// <summary>The size the lens gives icon <paramref name="i"/>: a smooth bell around the lens, 1 outside its reach.</summary>
    public double TargetScale(int i)
    {
        if (double.IsNaN(Lens) || Reach <= 0)
        {
            return 1;
        }
        var distance = Math.Abs(RestCenter(i) - Lens) / Reach;
        if (distance >= 1)
        {
            return 1;
        }
        var bell = (1 + Math.Cos(Math.PI * distance)) / 2;
        return 1 + (Math.Max(1, Magnification) - 1) * bell;
    }

    /// <summary>Advances the springs and bounces by <paramref name="dt"/> seconds. Returns whether anything still moves.</summary>
    public bool Step(double dt)
    {
        var moving = false;
        var steps = Math.Max(1, (int)Math.Ceiling(dt / (1.0 / 240)));
        var h = dt / steps;
        for (var i = 0; i < Count; i++)
        {
            var target = TargetScale(i);
            if (!Springs)
            {
                _scale[i] = target;
                _velocity[i] = 0;
            }
            else
            {
                for (var s = 0; s < steps; s++)
                {
                    var accel = Stiffness * (target - _scale[i]) - Damping * _velocity[i];
                    _velocity[i] += accel * h;
                    _scale[i] += _velocity[i] * h;
                }
                if (Math.Abs(target - _scale[i]) < 0.0005 && Math.Abs(_velocity[i]) < 0.005)
                {
                    _scale[i] = target;
                    _velocity[i] = 0;
                }
                else
                {
                    moving = true;
                }
            }
            if (_bounce[i] >= 0)
            {
                _bounce[i] += dt;
                if (_bounce[i] >= HopSeconds * HopHeights.Length)
                {
                    _bounce[i] = -1;
                }
                else
                {
                    moving = true;
                }
            }
        }
        return moving;
    }

    /// <summary>Stops every bounce and puts every icon at its target size.</summary>
    public void Settle()
    {
        for (var i = 0; i < Count; i++)
        {
            _scale[i] = TargetScale(i);
            _velocity[i] = 0;
            _bounce[i] = -1;
        }
    }

    /// <summary>
    /// Places the icons for the current sizes: the grown row stays centred on the rest row's centre. Returns the row's
    /// left edge.
    /// </summary>
    public double Layout()
    {
        var width = Width;
        var left = RestWidth / 2 - width / 2;
        var x = left;
        for (var i = 0; i < Count; i++)
        {
            var size = ItemSize * _scale[i];
            _center[i] = x + size / 2;
            x += size + Gap;
        }
        return left;
    }

    /// <summary>The widest the row can grow, over every lens position: the space the dock reserves.</summary>
    public double MaxWidth()
    {
        if (Count == 0)
        {
            return 0;
        }
        var saved = Lens;
        var widest = RestWidth;
        var samples = Math.Max(8, Count * 4);
        for (var s = 0; s <= samples; s++)
        {
            Lens = RestWidth * s / samples;
            var width = (Count - 1) * Gap;
            for (var i = 0; i < Count; i++)
            {
                width += ItemSize * TargetScale(i);
            }
            widest = Math.Max(widest, width);
        }
        Lens = saved;
        return widest;
    }
}
