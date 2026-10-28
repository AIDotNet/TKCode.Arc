namespace TKCode.Arc;

/// <summary>
/// The dot field of <see cref="DotGrid"/> without any UI: a centred grid of dots, each riding a damped spring toward the
/// offset the lens and the ripples ask of it, and a glow per dot that eases toward how strongly it is lit. Positions are
/// in px from the field's top left; time is in seconds.
/// </summary>
internal sealed class DotGridSimulation
{
    /// <summary>The smallest gap the grid accepts, so a stray small value cannot build tens of thousands of dots.</summary>
    public const double MinimumGap = 6;

    /// <summary>Ripple fronts travel at 620 px/s.</summary>
    public const double RippleSpeed = 620;

    /// <summary>A ripple lives 1.5s and fades over its whole life.</summary>
    public const double RippleLife = 1.5;

    /// <summary>At most six ripples overlap; a seventh replaces the oldest.</summary>
    public const int MaxRipples = 6;

    /// <summary>Half the width of a ripple's band, in px.</summary>
    public const double RippleBand = 44;

    /// <summary>How far a ripple's crest pushes a dot outward, in px.</summary>
    public const double RipplePush = 10;

    // The dot springs are underdamped (ζ ≈ 0.43) so a moved dot overshoots a little and wobbles back.
    private const double SpringStiffness = 440;
    private const double SpringDamping = 18;

    // The lens trails the pointer and fades in and out; the glow eases toward its target.
    private const double LensFollowRate = 17;
    private const double LensPowerRate = 7.5;
    private const double GlowRate = 15;

    // The lens profile u·(1 − u)² peaks at u = 1/3 with 4/27; scaling by 27/4 makes that peak equal the strength.
    private const double LensPeakScale = 27.0 / 4.0;

    private float[] _homeX = [];
    private float[] _homeY = [];
    private float[] _offsetX = [];
    private float[] _offsetY = [];
    private float[] _velocityX = [];
    private float[] _velocityY = [];
    private float[] _glow = [];
    private readonly Ripple[] _ripples = new Ripple[MaxRipples];
    private int _rippleCount;
    private double _lensX;
    private double _lensY;
    private double _lensPower;
    private double _pointerX;
    private double _pointerY;
    private bool _pointerInside;

    private struct Ripple
    {
        public double X;
        public double Y;
        public double Age;
    }

    /// <summary>Distance between dots in px (at least <see cref="MinimumGap"/>).</summary>
    public double Gap { get; private set; } = 22;

    /// <summary>Lens radius in px.</summary>
    public double Radius { get; set; } = 150;

    /// <summary>The largest push of the lens in px.</summary>
    public double Strength { get; set; } = 16;

    /// <summary>When false the dots never move: the lens only lights dots and ripples are ignored (reduced motion).</summary>
    public bool CanMove { get; set; } = true;

    public double Width { get; private set; }

    public double Height { get; private set; }

    public int Columns { get; private set; }

    public int Rows { get; private set; }

    public int Count { get; private set; }

    /// <summary>How present the lens is, 0 when the pointer is away and 1 once it has faded in.</summary>
    public double LensPower => _lensPower;

    public double LensX => _lensX;

    public double LensY => _lensY;

    public int RippleCount => _rippleCount;

    public bool PointerInside => _pointerInside;

    public double X(int index) => _homeX[index] + _offsetX[index];

    public double Y(int index) => _homeY[index] + _offsetY[index];

    public double HomeX(int index) => _homeX[index];

    public double HomeY(int index) => _homeY[index];

    /// <summary>How lit a dot is: 0 at rest, up to 1 at the heart of the lens or on a ripple's crest.</summary>
    public double Glow(int index) => _glow[index];

    /// <summary>
    /// Lays the grid out for a field of <paramref name="width"/> × <paramref name="height"/>: as many columns and rows as
    /// fit at <paramref name="gap"/>, centred, with every dot at rest.
    /// </summary>
    public void Build(double width, double height, double gap)
    {
        Width = Math.Max(0, width);
        Height = Math.Max(0, height);
        Gap = Math.Max(MinimumGap, double.IsFinite(gap) ? gap : MinimumGap);
        Columns = Width > 0 ? (int)Math.Floor(Width / Gap) + 1 : 0;
        Rows = Height > 0 ? (int)Math.Floor(Height / Gap) + 1 : 0;
        Count = Columns * Rows;
        _homeX = new float[Count];
        _homeY = new float[Count];
        _offsetX = new float[Count];
        _offsetY = new float[Count];
        _velocityX = new float[Count];
        _velocityY = new float[Count];
        _glow = new float[Count];
        var left = (Width - (Columns - 1) * Gap) / 2;
        var top = (Height - (Rows - 1) * Gap) / 2;
        var index = 0;
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                _homeX[index] = (float)(left + column * Gap);
                _homeY[index] = (float)(top + row * Gap);
                index++;
            }
        }
    }

    /// <summary>The pointer moved to (<paramref name="x"/>, <paramref name="y"/>) inside the field.</summary>
    public void PointerMoved(double x, double y)
    {
        if (!_pointerInside && _lensPower <= 0)
        {
            // A lens that has fully faded starts where the pointer comes in instead of sliding over from where it left.
            _lensX = x;
            _lensY = y;
        }
        _pointerX = x;
        _pointerY = y;
        _pointerInside = true;
    }

    /// <summary>The pointer left the field (or a finger lifted): the lens fades out where it is.</summary>
    public void PointerLeft() => _pointerInside = false;

    /// <summary>Starts a ripple at (<paramref name="x"/>, <paramref name="y"/>); returns false when dots cannot move.</summary>
    public bool AddRipple(double x, double y)
    {
        if (!CanMove)
        {
            return false;
        }
        if (_rippleCount == MaxRipples)
        {
            Array.Copy(_ripples, 1, _ripples, 0, MaxRipples - 1);
            _rippleCount--;
        }
        _ripples[_rippleCount++] = new Ripple { X = x, Y = y };
        return true;
    }

    /// <summary>Jumps the lens to the pointer (a touch puts it straight under the finger).</summary>
    public void SnapLens()
    {
        _lensX = _pointerX;
        _lensY = _pointerY;
    }

    /// <summary>Drops every offset, velocity, glow and ripple (the field is shown at rest).</summary>
    public void Rest()
    {
        Array.Clear(_offsetX);
        Array.Clear(_offsetY);
        Array.Clear(_velocityX);
        Array.Clear(_velocityY);
        Array.Clear(_glow);
        _rippleCount = 0;
        _lensPower = 0;
    }

    /// <summary>
    /// Advances the field by <paramref name="dt"/> seconds. Returns true while anything still moves: the lens is
    /// fading or the pointer is inside, a ripple is alive, or a dot has not settled.
    /// </summary>
    public bool Step(double dt)
    {
        dt = Math.Clamp(dt, 0, 1.0 / 24);
        var follow = 1 - Math.Exp(-LensFollowRate * dt);
        _lensX += (_pointerX - _lensX) * follow;
        _lensY += (_pointerY - _lensY) * follow;
        var powerTarget = _pointerInside ? 1.0 : 0.0;
        _lensPower += (powerTarget - _lensPower) * (1 - Math.Exp(-LensPowerRate * dt));
        if (Math.Abs(powerTarget - _lensPower) < 0.002)
        {
            _lensPower = powerTarget;
        }

        var kept = 0;
        for (var i = 0; i < _rippleCount; i++)
        {
            var ripple = _ripples[i];
            ripple.Age += dt;
            if (ripple.Age < RippleLife)
            {
                _ripples[kept++] = ripple;
            }
        }
        _rippleCount = kept;

        var moving = _pointerInside || _rippleCount > 0 || (_lensPower > 0 && _lensPower < 1);
        var radius = Math.Max(1, Radius);
        var strength = Strength;
        var glowEase = (float)(1 - Math.Exp(-GlowRate * dt));
        var stiffness = (float)(SpringStiffness * dt);
        var damping = (float)(SpringDamping * dt);
        var step = (float)dt;
        for (var i = 0; i < Count; i++)
        {
            double homeX = _homeX[i];
            double homeY = _homeY[i];
            double targetX = 0;
            double targetY = 0;
            double light = 0;
            if (_lensPower > 0)
            {
                var dx = homeX - _lensX;
                var dy = homeY - _lensY;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance < radius && distance > 0.001)
                {
                    var u = distance / radius;
                    var falloff = (1 - u) * (1 - u);
                    var push = LensPeakScale * u * falloff * strength * _lensPower;
                    targetX += dx / distance * push;
                    targetY += dy / distance * push;
                    light = Math.Max(light, falloff * _lensPower);
                }
            }
            for (var r = 0; r < _rippleCount; r++)
            {
                ref var ripple = ref _ripples[r];
                var dx = homeX - ripple.X;
                var dy = homeY - ripple.Y;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                var band = (distance - ripple.Age * RippleSpeed) / RippleBand;
                if (band < -2.2 || band > 2.2 || distance < 0.001)
                {
                    continue;
                }
                var life = 1 - ripple.Age / RippleLife;
                var crest = Math.Exp(-band * band) * life * life;
                targetX += dx / distance * RipplePush * crest;
                targetY += dy / distance * RipplePush * crest;
                light = Math.Max(light, 0.9 * crest);
            }

            if (CanMove)
            {
                var vx = _velocityX[i] + ((float)targetX - _offsetX[i]) * stiffness - _velocityX[i] * damping;
                var vy = _velocityY[i] + ((float)targetY - _offsetY[i]) * stiffness - _velocityY[i] * damping;
                _velocityX[i] = vx;
                _velocityY[i] = vy;
                _offsetX[i] += vx * step;
                _offsetY[i] += vy * step;
                if (!moving && (Math.Abs(vx) > 0.6 || Math.Abs(vy) > 0.6 ||
                                Math.Abs(targetX - _offsetX[i]) > 0.05 || Math.Abs(targetY - _offsetY[i]) > 0.05))
                {
                    moving = true;
                }
            }
            var glow = _glow[i] + ((float)light - _glow[i]) * glowEase;
            _glow[i] = glow;
            if (!moving && Math.Abs(light - glow) > 0.01)
            {
                moving = true;
            }
        }
        return moving;
    }
}
