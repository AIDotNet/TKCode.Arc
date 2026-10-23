namespace TKCode.Arc;

/// <summary>
/// Deterministic random numbers for sample data and seeded layouts (mulberry32, the generator the Showpiece data
/// recipes name; seeds 1729 for Customer galaxy, 20261022 for Semantic zoom and Revenue globe). Not for security.
/// </summary>
/// <remarks>
/// Algorithm (32-bit unsigned arithmetic, wrapping): <c>state += 0x6D2B79F5; t = state;
/// t = (t ^ (t >> 15)) * (t | 1); t ^= t + (t ^ (t >> 7)) * (t | 61); return t ^ (t >> 14);</c>
/// <see cref="NextDouble"/> divides that by 2³². This matches the JavaScript original (<c>Math.imul</c>, <c>>>></c>), so
/// seed 1 starts 0.6270739405881613, 0.002735721180215478, 0.5274470399599522. Derived draws consume a fixed number
/// of values each (documented per method), so a recipe that calls them in the same order reproduces its data.
/// </remarks>
internal sealed class ShowpieceSeed
{
    private uint _state;
    private double? _spareGaussian;

    public ShowpieceSeed(uint seed) => _state = seed;

    /// <summary>The next 32-bit value.</summary>
    public uint NextUInt()
    {
        unchecked
        {
            _state += 0x6D2B79F5u;
            var t = _state;
            t = (t ^ (t >> 15)) * (t | 1u);
            t ^= t + (t ^ (t >> 7)) * (t | 61u);
            return t ^ (t >> 14);
        }
    }

    /// <summary>Uniform in [0, 1) (one draw).</summary>
    public double NextDouble() => NextUInt() / 4294967296.0;

    /// <summary>Uniform in [min, max) (one draw).</summary>
    public double NextRange(double min, double max) => min + (max - min) * NextDouble();

    /// <summary>Uniform integer in [0, maxExclusive) (one draw; floor of <see cref="NextDouble"/> × n).</summary>
    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        }
        return Math.Min(maxExclusive - 1, (int)(NextDouble() * maxExclusive));
    }

    /// <summary>True with probability <paramref name="p"/> (one draw).</summary>
    public bool Chance(double p) => NextDouble() < p;

    /// <summary>
    /// Standard normal N(0, 1) by Box–Muller: two draws produce two values; the second is returned by the next call
    /// (so draws are consumed in pairs).
    /// </summary>
    public double NextGaussian()
    {
        if (_spareGaussian is { } spare)
        {
            _spareGaussian = null;
            return spare;
        }
        var u1 = 1 - NextDouble(); // (0, 1]: log is finite
        var u2 = NextDouble();
        var radius = Math.Sqrt(-2 * Math.Log(u1));
        var angle = 2 * Math.PI * u2;
        _spareGaussian = radius * Math.Sin(angle);
        return radius * Math.Cos(angle);
    }

    /// <summary>Normal with the given mean and standard deviation.</summary>
    public double NextGaussian(double mean, double deviation) => mean + deviation * NextGaussian();

    /// <summary>Log-normal with the given median and σ (of the underlying normal): <c>exp(ln(median) + σ·N(0,1))</c>.</summary>
    public double NextLogNormal(double median, double sigma) => Math.Exp(Math.Log(median) + sigma * NextGaussian());

    /// <summary>Triangular on [min, max] with its peak at <paramref name="mode"/> (one draw, inverse CDF).</summary>
    public double NextTriangular(double min, double mode, double max)
    {
        var u = NextDouble();
        var split = (mode - min) / (max - min);
        return u < split
            ? min + Math.Sqrt(u * (max - min) * (mode - min))
            : max - Math.Sqrt((1 - u) * (max - min) * (max - mode));
    }

    /// <summary>
    /// Poisson with mean <paramref name="lambda"/> by inversion (one draw: walks the CDF until it passes the draw). For
    /// λ above about 700 the terms underflow; the result then falls back to a rounded normal approximation (two draws).
    /// </summary>
    public int NextPoisson(double lambda)
    {
        if (lambda <= 0)
        {
            return 0;
        }
        if (lambda > 700)
        {
            return Math.Max(0, (int)Math.Round(NextGaussian(lambda, Math.Sqrt(lambda))));
        }
        var u = NextDouble();
        var k = 0;
        var p = Math.Exp(-lambda);
        var cumulative = p;
        while (u > cumulative && k < 100000)
        {
            k++;
            p *= lambda / k;
            cumulative += p;
            if (p < 1e-300)
            {
                break;
            }
        }
        return k;
    }

    /// <summary>An element chosen uniformly (one draw).</summary>
    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];

    /// <summary>An index chosen with probability proportional to its weight (one draw).</summary>
    public int PickWeighted(ReadOnlySpan<double> weights)
    {
        double total = 0;
        foreach (var w in weights)
        {
            total += Math.Max(0, w);
        }
        if (total <= 0)
        {
            return weights.Length == 0 ? -1 : Next(weights.Length);
        }
        var target = NextDouble() * total;
        for (var i = 0; i < weights.Length; i++)
        {
            target -= Math.Max(0, weights[i]);
            if (target < 0)
            {
                return i;
            }
        }
        return weights.Length - 1;
    }

    /// <summary>Fisher–Yates shuffle from the end (n − 1 draws).</summary>
    public void Shuffle<T>(Span<T> items)
    {
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
