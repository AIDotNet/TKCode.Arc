namespace TKCode.Arc;

/// <summary>
/// The field of <see cref="GravityWell"/> without any UI. A well follows the pointer on a critically damped spring;
/// every tile follows the field the well makes on its own underdamped spring, leaning toward the well, shrinking and
/// dimming the deeper it sits; a pulse sends a shockwave outward that kicks each tile once as its front passes.
/// Positions are tile centres in px from the grid's top left; time is in seconds.
/// </summary>
internal sealed class GravityWellSimulation
{
    /// <summary>Shockwave fronts travel at 900 px/s.</summary>
    public const double WaveSpeed = 900;

    /// <summary>At most four shockwaves are alive; a fifth replaces the oldest.</summary>
    public const int MaxWaves = 4;

    // The well: critically damped at ω = 18/s, so it trails the pointer without overshooting.
    private const double WellStiffness = 324;
    private const double WellDamping = 36;

    // A tile: underdamped (ζ ≈ 0.45), so quick moves leave a jelly wake.
    private const double TileStiffness = 210;
    private const double TileDamping = 13;

    // How the field shapes a tile: the share of the way to the well it leans, how much it shrinks and dims at the bottom.
    private const double Lean = 0.34;
    private const double Shrink = 0.44;
    private const double Dim = 0.36;

    private const double PresenceRate = 7;

    // A shockwave's kick at its origin: outward speed in px/s, a dip in scale, and how bright the ring flashes.
    private const double WaveKick = 700;
    private const double WaveDip = 3.2;
    private const double WaveRing = 1.35;
    private const double RingFadeRate = 4.5;

    private float[] _homeX = [];
    private float[] _homeY = [];
    private float[] _offsetX = [];
    private float[] _offsetY = [];
    private float[] _velocityX = [];
    private float[] _velocityY = [];
    private float[] _scale = [];
    private float[] _scaleVelocity = [];
    private float[] _depth = [];
    private float[] _ring = [];
    private readonly Wave[] _waves = new Wave[MaxWaves];
    private int _waveCount;
    private double _wellX;
    private double _wellY;
    private double _wellVelocityX;
    private double _wellVelocityY;
    private double _targetX;
    private double _targetY;
    private double _presence;
    private double _presenceTarget;
    private bool _placed;

    private struct Wave
    {
        public double X;
        public double Y;
        public double Age;
        public int Remaining;
        public bool[] Hit;
    }

    /// <summary>Reach of the well in px.</summary>
    public double Radius { get; set; } = 150;

    /// <summary>Pull strength, 0 to about 1.5.</summary>
    public double Strength { get; set; } = 1;

    /// <summary>When false tiles never move or scale (reduced motion); shockwaves only flash their rings.</summary>
    public bool CanMove { get; set; } = true;

    public int Count { get; private set; }

    public double WellX => _wellX;

    public double WellY => _wellY;

    /// <summary>How present the well is: 0 while nobody points at the grid, 1 once it has faded in.</summary>
    public double Presence => _presence;

    public int WaveCount => _waveCount;

    public double OffsetX(int index) => _offsetX[index];

    public double OffsetY(int index) => _offsetY[index];

    public double Scale(int index) => _scale[index];

    /// <summary>How deep in the well a tile sits, 0 to about 1; it drives the tile's dimming and stacking.</summary>
    public double Depth(int index) => _depth[index];

    /// <summary>The tile's opacity: 1 outside the well, lower toward its bottom.</summary>
    public double Opacity(int index) => 1 - Dim * _depth[index];

    /// <summary>
    /// The tile's stacking order: 10 outside the well and down to 1 at its bottom, so deeper tiles slide under their
    /// neighbours.
    /// </summary>
    public int ZIndex(int index) => 10 - (int)Math.Round(9 * _depth[index], MidpointRounding.AwayFromZero);

    /// <summary>How bright the tile's ring is, 1 when a shockwave just passed close to its origin, fading to 0.</summary>
    public double Ring(int index) => _ring[index];

    public double HomeX(int index) => _homeX[index];

    public double HomeY(int index) => _homeY[index];

    /// <summary>
    /// Sets the tiles' resting centres. Tiles keep their motion when only their positions change; a different count
    /// starts every tile at rest.
    /// </summary>
    public void SetTiles(ReadOnlySpan<double> centersX, ReadOnlySpan<double> centersY)
    {
        var count = Math.Min(centersX.Length, centersY.Length);
        if (count != Count)
        {
            Count = count;
            _homeX = new float[count];
            _homeY = new float[count];
            _offsetX = new float[count];
            _offsetY = new float[count];
            _velocityX = new float[count];
            _velocityY = new float[count];
            _scale = new float[count];
            _scaleVelocity = new float[count];
            _depth = new float[count];
            _ring = new float[count];
            Array.Fill(_scale, 1f);
            _waveCount = 0;
        }
        for (var i = 0; i < count; i++)
        {
            _homeX[i] = (float)centersX[i];
            _homeY[i] = (float)centersY[i];
        }
    }

    /// <summary>
    /// Moves the well's target to (<paramref name="x"/>, <paramref name="y"/>) and makes it present. With
    /// <paramref name="snap"/>, or the first time, the well jumps there instead of travelling.
    /// </summary>
    public void Aim(double x, double y, bool snap)
    {
        _targetX = x;
        _targetY = y;
        if (snap || !_placed)
        {
            _wellX = x;
            _wellY = y;
            _wellVelocityX = 0;
            _wellVelocityY = 0;
            _placed = true;
        }
        _presenceTarget = 1;
    }

    /// <summary>The pointer left or focus moved away: the well fades out where it is.</summary>
    public void Release() => _presenceTarget = 0;

    /// <summary>True when the well has faded out far enough that a new aim should start where it lands.</summary>
    public bool IsAbsent => _presence < 0.02;

    /// <summary>Sends a shockwave from (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public void Pulse(double x, double y)
    {
        if (_waveCount == MaxWaves)
        {
            Array.Copy(_waves, 1, _waves, 0, MaxWaves - 1);
            _waveCount--;
        }
        _waves[_waveCount++] = new Wave { X = x, Y = y, Remaining = Count, Hit = new bool[Count] };
    }

    /// <summary>Puts every tile back at rest and drops the shockwaves (the grid is shown still).</summary>
    public void Rest()
    {
        Array.Clear(_offsetX);
        Array.Clear(_offsetY);
        Array.Clear(_velocityX);
        Array.Clear(_velocityY);
        Array.Fill(_scale, 1f);
        Array.Clear(_scaleVelocity);
        Array.Clear(_depth);
        _waveCount = 0;
    }

    /// <summary>Advances the field by <paramref name="dt"/> seconds; returns true while any spring, fade or wave is moving.</summary>
    public bool Step(double dt)
    {
        dt = Math.Clamp(dt, 0, 1.0 / 30);
        _wellVelocityX += ((_targetX - _wellX) * WellStiffness - _wellVelocityX * WellDamping) * dt;
        _wellVelocityY += ((_targetY - _wellY) * WellStiffness - _wellVelocityY * WellDamping) * dt;
        _wellX += _wellVelocityX * dt;
        _wellY += _wellVelocityY * dt;
        _presence += (_presenceTarget - _presence) * (1 - Math.Exp(-PresenceRate * dt));
        if (Math.Abs(_presenceTarget - _presence) < 0.002)
        {
            _presence = _presenceTarget;
        }
        var moving = _presence != _presenceTarget || Math.Sqrt(Square(_targetX - _wellX) + Square(_targetY - _wellY)) > 0.1;

        for (var w = 0; w < _waveCount; w++)
        {
            _waves[w].Age += dt;
        }
        if (_waveCount > 0)
        {
            moving = true;
        }

        var radius = Math.Max(1, Radius);
        var spread = radius * radius;
        var strength = Math.Max(0, Strength);
        var ringFade = Math.Exp(-RingFadeRate * dt);
        for (var i = 0; i < Count; i++)
        {
            double homeX = _homeX[i];
            double homeY = _homeY[i];
            var toWellX = _wellX - homeX;
            var toWellY = _wellY - homeY;
            var distanceSquared = toWellX * toWellX + toWellY * toWellY;
            double pull = 0;
            double depth = 0;
            if (CanMove && _presence > 0)
            {
                pull = Math.Exp(-distanceSquared / (2 * spread)) * _presence * strength;
                depth = Math.Exp(-distanceSquared / spread) * _presence * Math.Min(1.2, strength);
            }
            var targetX = Lean * toWellX * pull;
            var targetY = Lean * toWellY * pull;
            var targetScale = 1 - Shrink * depth;
            _depth[i] = (float)depth;

            for (var w = 0; w < _waveCount; w++)
            {
                ref var wave = ref _waves[w];
                if (wave.Hit[i])
                {
                    continue;
                }
                var awayX = homeX - wave.X;
                var awayY = homeY - wave.Y;
                var distance = Math.Sqrt(awayX * awayX + awayY * awayY);
                if (CanMove && wave.Age * WaveSpeed < distance)
                {
                    continue;
                }
                wave.Hit[i] = true;
                wave.Remaining--;
                var falloff = Math.Exp(-distance / (3.2 * radius));
                if (CanMove)
                {
                    var safe = Math.Max(1, distance);
                    _velocityX[i] += (float)(awayX / safe * WaveKick * falloff);
                    _velocityY[i] += (float)(awayY / safe * WaveKick * falloff);
                    _scaleVelocity[i] -= (float)(WaveDip * falloff);
                }
                _ring[i] = (float)Math.Max(_ring[i], Math.Min(1, WaveRing * falloff));
            }

            if (CanMove)
            {
                _velocityX[i] += (float)(((targetX - _offsetX[i]) * TileStiffness - _velocityX[i] * TileDamping) * dt);
                _velocityY[i] += (float)(((targetY - _offsetY[i]) * TileStiffness - _velocityY[i] * TileDamping) * dt);
                _scaleVelocity[i] += (float)(((targetScale - _scale[i]) * TileStiffness - _scaleVelocity[i] * TileDamping) * dt);
                _offsetX[i] += (float)(_velocityX[i] * dt);
                _offsetY[i] += (float)(_velocityY[i] * dt);
                _scale[i] += (float)(_scaleVelocity[i] * dt);
                if (!moving && (Math.Abs(_velocityX[i]) + Math.Abs(_velocityY[i]) > 0.02 ||
                                Math.Abs(targetX - _offsetX[i]) + Math.Abs(targetY - _offsetY[i]) > 0.02 ||
                                Math.Abs(_scaleVelocity[i]) > 0.001 || Math.Abs(targetScale - _scale[i]) > 0.0005))
                {
                    moving = true;
                }
            }
            if (_ring[i] > 0)
            {
                _ring[i] = _ring[i] < 0.01 ? 0 : (float)(_ring[i] * ringFade);
                if (_ring[i] > 0)
                {
                    moving = true;
                }
            }
        }
        var kept = 0;
        for (var w = 0; w < _waveCount; w++)
        {
            if (_waves[w].Remaining > 0)
            {
                _waves[kept++] = _waves[w];
            }
        }
        for (var w = kept; w < _waveCount; w++)
        {
            _waves[w] = default;
        }
        _waveCount = kept;
        return moving;
    }

    private static double Square(double value) => value * value;
}
