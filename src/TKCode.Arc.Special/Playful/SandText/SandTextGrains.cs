using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The grains of a <see cref="SandText"/>: where each one sits in the letters, when it lets go and where the wind takes
/// it. A grain's place is a pure function of the shared progress, so dissolving and gathering are the same path run
/// forwards and backwards, and turning around half way needs no extra state. Nothing is allocated after
/// <see cref="Build"/>.
/// </summary>
/// <remarks>
/// The timeline is measured in "spans": a sweep front crosses the text from the windward side in <see cref="Sweep"/>,
/// each grain waits up to <see cref="Jitter"/> more behind the front, then flies for <see cref="Life"/>.
/// </remarks>
internal sealed class SandTextGrains
{
    /// <summary>The share of the timeline the release front takes to cross the text.</summary>
    public const double Sweep = 0.55;

    /// <summary>The largest extra wait of a grain behind the front, so the edge crumbles instead of cutting.</summary>
    public const double Jitter = 0.15;

    /// <summary>How long one grain flies before it has faded out.</summary>
    public const double Life = 0.6;

    /// <summary>The whole timeline: the last grain lands at progress 1.</summary>
    public const double Span = Sweep + Jitter + Life;

    private float[] _homeX = [];
    private float[] _homeY = [];
    private float[] _u = [];
    private float[] _delay = [];
    private float[] _reach = [];
    private float[] _lift = [];
    private float[] _phase = [];
    private float[] _tone = [];

    /// <summary>How many grains make up the text.</summary>
    public int Count { get; private set; }

    /// <summary>The side length of one grain in px.</summary>
    public double Size { get; private set; }

    /// <summary>The ink box the grains were sampled from.</summary>
    public Rect Bounds { get; private set; }

    /// <summary>The way the wind blows.</summary>
    public SandTextDrift Drift { get; private set; }

    /// <summary>
    /// Samples the letters on a square grid inside <paramref name="bounds"/>. When more than <paramref name="maxGrains"/>
    /// cells are inside, the grid is coarsened (larger grains) until they fit.
    /// </summary>
    /// <param name="bounds">The ink box of the text.</param>
    /// <param name="grainSize">The smallest grain, px.</param>
    /// <param name="contains">Whether a point is inside a letter.</param>
    /// <param name="drift">The way the wind blows.</param>
    /// <param name="distance">How far a typical grain travels, px.</param>
    /// <param name="maxGrains">The most grains the text may have.</param>
    public void Build(Rect bounds, double grainSize, Func<Point, bool> contains, SandTextDrift drift, double distance, int maxGrains)
    {
        Bounds = bounds;
        Drift = drift;
        Count = 0;
        if (bounds.Width <= 0 || bounds.Height <= 0 || maxGrains <= 0)
        {
            Size = Math.Max(0.5, grainSize);
            return;
        }
        var step = Math.Max(0.5, grainSize);
        // Coarsen the grid until the letters fit the budget (each pass costs one sampling of the box).
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var inside = CountInside(bounds, step, contains);
            if (inside <= maxGrains)
            {
                break;
            }
            step *= Math.Sqrt((double)inside / maxGrains) * 1.02;
        }
        Size = step;
        var capacity = Math.Min(maxGrains, Cells(bounds, step));
        Ensure(capacity);
        var seed = 0x9E3779B9u;
        for (var y = bounds.Top + step / 2; y < bounds.Bottom; y += step)
        {
            for (var x = bounds.Left + step / 2; x < bounds.Right; x += step)
            {
                if (Count >= capacity || !contains(new Point(x, y)))
                {
                    continue;
                }
                var i = Count++;
                _homeX[i] = (float)(x - step / 2);
                _homeY[i] = (float)(y - step / 2);
                var u = drift switch
                {
                    SandTextDrift.Left => 1 - (x - bounds.Left) / bounds.Width,
                    SandTextDrift.Up => (y - bounds.Top) / bounds.Height,
                    _ => (x - bounds.Left) / bounds.Width
                };
                _u[i] = (float)Math.Clamp(u, 0, 1);
                _delay[i] = (float)(_u[i] * Sweep + Next(ref seed) * Jitter);
                _reach[i] = (float)(distance * (0.55 + 0.9 * Next(ref seed)));
                _lift[i] = (float)(distance * (Next(ref seed) - 0.25) * 0.5);
                _phase[i] = (float)(Next(ref seed) * Math.PI * 2);
                _tone[i] = (float)(0.72 + 0.28 * Next(ref seed));
            }
        }
    }

    /// <summary>The release front at timeline position <paramref name="t"/> (0 to <see cref="Span"/>), as a share of the text (0 to 1+).</summary>
    public static double Front(double t) => t / Sweep;

    /// <summary>A grain's place in the sweep (0 on the windward edge, 1 on the far one).</summary>
    public double Order(int i) => _u[i];

    /// <summary>The grain's resting top-left corner.</summary>
    public Point Home(int i) => new(_homeX[i], _homeY[i]);

    /// <summary>The grain's own brightness, 0.72 to 1, so the sand has some texture.</summary>
    public double Tone(int i) => _tone[i];

    /// <summary>How far into its flight a grain is at timeline position <paramref name="t"/>: 0 at home, 1 gone.</summary>
    public double Flight(int i, double t) => Math.Clamp((t - _delay[i]) / Life, 0, 1);

    /// <summary>Where grain <paramref name="i"/> is at timeline position <paramref name="t"/>, how opaque and how large.</summary>
    public void Place(int i, double t, out double x, out double y, out double opacity, out double size)
    {
        var f = Flight(i, t);
        x = _homeX[i];
        y = _homeY[i];
        size = Size;
        opacity = 1;
        if (f <= 0)
        {
            return;
        }
        // The wind picks a grain up slowly and then carries it (ease-in), with a little flutter across the way.
        var travel = f * f * (3 - 2 * f) * 0.35 + f * f * 0.65;
        var flutter = Math.Sin(_phase[i] + f * 7) * 2.5 * f;
        var reach = _reach[i] * travel;
        var lift = _lift[i] * travel + flutter;
        switch (Drift)
        {
            case SandTextDrift.Left:
                x -= reach;
                y -= lift;
                break;
            case SandTextDrift.Up:
                y -= reach;
                x += lift;
                break;
            default:
                x += reach;
                y -= lift;
                break;
        }
        opacity = f < 0.3 ? 1 : 1 - (f - 0.3) / 0.7;
        size = Size * (1 - 0.45 * f);
    }

    private void Ensure(int capacity)
    {
        if (_homeX.Length >= capacity)
        {
            return;
        }
        _homeX = new float[capacity];
        _homeY = new float[capacity];
        _u = new float[capacity];
        _delay = new float[capacity];
        _reach = new float[capacity];
        _lift = new float[capacity];
        _phase = new float[capacity];
        _tone = new float[capacity];
    }

    private static int Cells(Rect bounds, double step) =>
        (int)Math.Ceiling(bounds.Width / step) * (int)Math.Ceiling(bounds.Height / step);

    private static int CountInside(Rect bounds, double step, Func<Point, bool> contains)
    {
        var count = 0;
        for (var y = bounds.Top + step / 2; y < bounds.Bottom; y += step)
        {
            for (var x = bounds.Left + step / 2; x < bounds.Right; x += step)
            {
                if (contains(new Point(x, y)))
                {
                    count++;
                }
            }
        }
        return count;
    }

    /// <summary>A small deterministic generator (xorshift), so the same text always crumbles the same way.</summary>
    private static double Next(ref uint state)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (state & 0xFFFFFF) / (double)0x1000000;
    }
}
