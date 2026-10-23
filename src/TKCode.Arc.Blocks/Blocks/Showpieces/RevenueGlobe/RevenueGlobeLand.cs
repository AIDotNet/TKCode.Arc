namespace TKCode.Arc;

/// <summary>
/// The dots a <see cref="RevenueGlobe"/> draws its land and sea with: unit vectors (x, y, z triples; z is the north
/// pole, x points at longitude 0) on two Fibonacci lattices, the dense one kept where there is land and the sparse one
/// where there is sea. <see cref="Default"/> samples the land mask that ships with the library: Natural Earth 1:110m
/// Land (public domain), rasterized to 0.5° cells by <c>tools/geo/generate_land_mask.py</c>.
/// </summary>
public sealed class RevenueGlobeLand
{
    /// <summary>Lattice size the default land is sampled from (nearest neighbours 1.7° apart).</summary>
    public const int DefaultLandLattice = 12800;

    /// <summary>Lattice size the default sea is sampled from (nearest neighbours 4.25° apart).</summary>
    public const int DefaultOceanLattice = 2050;

    internal const string MaskResourceName = "TKCode.Arc.Blocks.RevenueGlobe.LandMask";

    private static RevenueGlobeLand? s_default;
    private readonly float[] _land;
    private readonly float[] _ocean;

    private RevenueGlobeLand(float[] land, float[] ocean)
    {
        _land = land;
        _ocean = ocean;
    }

    /// <summary>The built-in land: Natural Earth 1:110m, sampled on 12,800 and 2,050 point lattices.</summary>
    public static RevenueGlobeLand Default => s_default ??= Sample(DefaultLandLattice, DefaultOceanLattice);

    /// <summary>
    /// The built-in Natural Earth land sampled on lattices of your own size: fewer points for slow devices (7,000 and
    /// 1,200 halve the dots), more for a large globe.
    /// </summary>
    public static RevenueGlobeLand Sample(int landLattice, int oceanLattice)
    {
        using var stream = typeof(RevenueGlobeLand).Assembly.GetManifestResourceStream(MaskResourceName);
        if (stream is null)
        {
            return new RevenueGlobeLand([], []);
        }
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return FromMask(bytes, Math.Max(0, landLattice), Math.Max(0, oceanLattice));
    }

    /// <summary>Land dots as x, y, z triples of unit vectors.</summary>
    public ReadOnlyMemory<float> LandPoints => _land;

    /// <summary>Sea dots as x, y, z triples of unit vectors.</summary>
    public ReadOnlyMemory<float> OceanPoints => _ocean;

    /// <summary>Number of land dots.</summary>
    public int LandCount => _land.Length / 3;

    /// <summary>Number of sea dots.</summary>
    public int OceanCount => _ocean.Length / 3;

    /// <summary>Land and sea from your own unit vectors (x, y, z triples, z north, x toward longitude 0).</summary>
    public static RevenueGlobeLand FromPoints(float[] land, float[] ocean)
    {
        ArgumentNullException.ThrowIfNull(land);
        ArgumentNullException.ThrowIfNull(ocean);
        if (land.Length % 3 != 0 || ocean.Length % 3 != 0)
        {
            throw new ArgumentException("Points are x, y, z triples.");
        }
        return new RevenueGlobeLand((float[])land.Clone(), (float[])ocean.Clone());
    }

    /// <summary>
    /// Samples a land bit mask (the <c>LNDM</c> format written by <c>tools/geo/generate_land_mask.py</c>) on Fibonacci
    /// lattices of <paramref name="landLattice"/> and <paramref name="oceanLattice"/> points.
    /// </summary>
    public static RevenueGlobeLand FromMask(ReadOnlySpan<byte> mask, int landLattice = DefaultLandLattice, int oceanLattice = DefaultOceanLattice)
    {
        var grid = new RevenueGlobeLandMask(mask);
        return new RevenueGlobeLand(Sample(grid, landLattice, keepLand: true), Sample(grid, oceanLattice, keepLand: false));
    }

    /// <summary>The i-th of n points of a Fibonacci (golden angle) lattice: z = 1 − (2i + 1)/n, φ = i·golden angle.</summary>
    internal static (double X, double Y, double Z) LatticePoint(int i, int n)
    {
        var goldenAngle = Math.PI * (3 - Math.Sqrt(5));
        var z = 1 - (2.0 * i + 1) / n;
        var r = Math.Sqrt(Math.Max(0, 1 - z * z));
        var phi = i * goldenAngle;
        return (r * Math.Cos(phi), r * Math.Sin(phi), z);
    }

    private static float[] Sample(in RevenueGlobeLandMask grid, int n, bool keepLand)
    {
        var points = new List<float>(n);
        for (var i = 0; i < n; i++)
        {
            var (x, y, z) = LatticePoint(i, n);
            var latitude = Math.Asin(Math.Clamp(z, -1, 1)) * 180 / Math.PI;
            var longitude = Math.Atan2(y, x) * 180 / Math.PI;
            if (grid.IsLand(latitude, longitude) == keepLand)
            {
                points.Add((float)x);
                points.Add((float)y);
                points.Add((float)z);
            }
        }
        return points.ToArray();
    }
}

/// <summary>
/// Reads an <c>LNDM</c> land mask: bytes 0-3 "LNDM", byte 4 cells per degree k, then 180k rows (north to south) of
/// 360k bits (west to east from −180°, least significant bit first, rows padded to whole bytes); a set bit is land.
/// </summary>
internal readonly ref struct RevenueGlobeLandMask
{
    private readonly ReadOnlySpan<byte> _bits;
    private readonly int _k;
    private readonly int _columns;
    private readonly int _rows;
    private readonly int _stride;

    public RevenueGlobeLandMask(ReadOnlySpan<byte> mask)
    {
        if (mask.Length < 8 || mask[0] != (byte)'L' || mask[1] != (byte)'N' || mask[2] != (byte)'D' || mask[3] != (byte)'M' || mask[4] == 0)
        {
            throw new ArgumentException("Not an LNDM land mask.", nameof(mask));
        }
        _k = mask[4];
        _columns = 360 * _k;
        _rows = 180 * _k;
        _stride = (_columns + 7) / 8;
        if (mask.Length < 8 + _stride * _rows)
        {
            throw new ArgumentException("The land mask is truncated.", nameof(mask));
        }
        _bits = mask[8..];
    }

    public bool IsLand(double latitude, double longitude)
    {
        var row = Math.Clamp((int)Math.Floor((90 - latitude) * _k), 0, _rows - 1);
        var column = Math.Clamp((int)Math.Floor((longitude + 180) * _k), 0, _columns - 1);
        return (_bits[row * _stride + (column >> 3)] & (1 << (column & 7))) != 0;
    }
}
