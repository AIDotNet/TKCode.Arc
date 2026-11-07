using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The look of one run, read from the control and its token when the run starts.</summary>
internal readonly record struct BlackHoleLook(
    double CoreRadius,
    double CoreGrowth,
    double EinsteinRatio,
    double HaloDecay,
    double HaloOpacity,
    double PhotonRingWidth,
    Color PhotonRingColor,
    double ShockRadius,
    double ShockWidth,
    double ShockOpacity,
    Color ShockColor);

/// <summary>
/// The CPU painter of a black hole run. It holds two snapshots of the wrapped content at device resolution (the rest of
/// the content, and the swallowed items alone) and writes one frame at a time, in four steps per band of 8 rows, the
/// bands in parallel:
/// <list type="number">
/// <item>the rest of the content, lensed by a point mass at the hole (inverse mapping, one bilinear sample per pixel; the
/// mapping is exact near the hole and interpolated across 8 × 8 tiles where its curvature keeps the error under
/// 0.05 px; flat tiles that stay flat are filled);</item>
/// <item>the swallowed items as a 5 DIP mesh whose vertices fall along the logarithmic spiral, rasterized as textured
/// triangles (affine, bilinear), each band visiting only the triangles binned to it;</item>
/// <item>the hole: dark halo and photon ring composed once per frame into a radial lookup table, then the black core;</item>
/// <item>the shockwave ring after the collapse, from a second radial table.</item>
/// </list>
/// Frames are double-buffered: <see cref="Render"/> paints the back buffer and then makes it <see cref="Output"/>, so a
/// finished frame can be uploaded while the next one is painted (on another thread). Pixels are premultiplied 32-bit
/// values in the snapshot's channel order. Buffers are allocated once per run; a frame allocates nothing of its own.
/// </summary>
internal sealed class BlackHoleRenderer
{
    private const int BandHeight = 8;
    private const double Spacing = 5;

    /// <summary>Radial tables are sampled every quarter device pixel (finer for a thin photon ring) and interpolated.</summary>
    internal const float LutStep = 0.25f;

    /// <summary>The finest table step: a photon ring a fraction of a pixel wide while the hole opens or closes.</summary>
    internal const float LutStepMin = 1f / 16;

    /// <summary>Largest error of the interpolated lens mapping (device px).</summary>
    internal const float LensTolerance = 0.05f;

    /// <summary>The clock <see cref="ProfilePasses"/> reads (ms); the benchmark swaps in process CPU time.</summary>
    internal static Func<double> ProfileClock = static () => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    private readonly int _w;
    private readonly int _h;
    private readonly int _bands;
    private readonly double _scale;
    private readonly bool _redFirst;
    private readonly uint[] _rest;
    private readonly uint[] _items;
    private uint[] _front;
    private uint[] _back;
    private readonly Action<int> _band;

    // Mesh: vertices (source position in device pixels, polar start around the hole in DIP, fall start) and triangles.
    private float[] _srcX = [];
    private float[] _srcY = [];
    private double[] _r0 = [];
    private double[] _theta0 = [];
    private double[] _start = [];
    private float[] _dstX = [];
    private float[] _dstY = [];
    private int[] _tris = [];
    private int _triCount;
    // Per-triangle setup of the current frame: A, B, C (device px), 1/area, bounds, and the source coordinates.
    private float[] _setup = [];
    private const int SetupStride = 16;
    // Triangles binned by band (counting sort, stable): band b's triangles are _binItems[_binStart[b] .. _binStart[b + 1]).
    private readonly int[] _binStart;
    private readonly int[] _binCursor;
    private int[] _binItems = [];

    // Flat areas of the rest snapshot: 8 × 8 tiles of one color, and how many rings of equal tiles surround each
    // (Chebyshev distance in tiles; −1 = not flat). A lensed tile whose displacement stays inside that margin is a fill.
    private const int Tile = 8;
    private int _tilesX;
    private int _tilesY;
    private int[] _tileMargin = [];
    private uint[] _tileColor = [];

    // Radial tables of the current frame: premultiplied colors every _holeStep / LutStep device px.
    private uint[] _holeLut = [];
    private int _holeLutCount;
    private float _holeReachPx;
    private float _holeStep = LutStep;
    private uint[] _shockLut = [];
    private int _shockLutCount;
    private float _shockInnerPx;
    private float _shockOuterPx;

    private BlackHoleLook _look;
    private double _cx;
    private double _cy;
    private double _core;
    private double _einstein;
    private double _shockR;
    private double _shockA;
    private uint _ringRgb;
    private uint _shockRgb;

    public BlackHoleRenderer(int width, int height, double scale, bool redFirst)
    {
        _w = Math.Max(1, width);
        _h = Math.Max(1, height);
        _bands = (_h + BandHeight - 1) / BandHeight;
        _scale = scale;
        _redFirst = redFirst;
        _rest = new uint[_w * _h];
        _items = new uint[_w * _h];
        _front = new uint[_w * _h];
        _back = new uint[_w * _h];
        _binStart = new int[_bands + 1];
        _binCursor = new int[_bands];
        _band = RenderBand;
    }

    public int Width => _w;

    public int Height => _h;

    /// <summary>The snapshots store red in the lowest byte (RGBA) rather than blue (BGRA).</summary>
    public bool RedFirst => _redFirst;

    /// <summary>Device pixels per DIP.</summary>
    public double Scale => _scale;

    /// <summary>The content without the swallowed items.</summary>
    public uint[] Rest => _rest;

    /// <summary>The swallowed items alone, at their places.</summary>
    public uint[] Items => _items;

    /// <summary>The last finished frame.</summary>
    public uint[] Output => _front;

    /// <summary>Mesh vertex count (tests and diagnostics).</summary>
    public int VertexCount => _srcX.Length;

    /// <summary>Mesh triangle count.</summary>
    public int TriangleCount => _triCount;

    /// <summary>Bands of 8 rows a frame is painted in.</summary>
    public int Bands => _bands;

    /// <summary>The hole centre in DIP of the content.</summary>
    public Point Center { get; private set; }

    /// <summary>The stagger used for this run (ms per DIP at the reference duration).</summary>
    public double Stagger { get; private set; }

    /// <summary>Wall time of the last <see cref="Render"/> (ms).</summary>
    public double LastFrameMilliseconds { get; private set; }

    /// <summary>The hole of the last frame (DIP).</summary>
    public BlackHoleHoleState LastHole { get; private set; }

    /// <summary>
    /// Builds the falling mesh over <paramref name="targets"/> (DIP rectangles of the content) for a hole at
    /// <paramref name="center"/>, and the stagger that lands the farthest point before the collapse.
    /// </summary>
    public void BuildMesh(IReadOnlyList<Rect> targets, Point center, BlackHoleLook look)
    {
        _look = look;
        Center = center;
        var bounds = new Rect(0, 0, _w / _scale, _h / _scale);
        var vertices = 0;
        var triangles = 0;
        foreach (var target in targets)
        {
            var r = target.Intersect(bounds);
            if (r.Width <= 0 || r.Height <= 0)
            {
                continue;
            }
            var cols = Math.Max(1, (int)Math.Ceiling(r.Width / Spacing));
            var rows = Math.Max(1, (int)Math.Ceiling(r.Height / Spacing));
            vertices += (cols + 1) * (rows + 1);
            triangles += cols * rows * 2;
        }
        _srcX = new float[vertices];
        _srcY = new float[vertices];
        _r0 = new double[vertices];
        _theta0 = new double[vertices];
        _start = new double[vertices];
        _dstX = new float[vertices];
        _dstY = new float[vertices];
        _tris = new int[triangles * 3];
        _setup = new float[triangles * SetupStride];
        // Room for every triangle in two bands; a frame with longer spans grows it once.
        _binItems = new int[Math.Max(16, triangles * 2)];
        _triCount = 0;
        var v = 0;
        var farthest = 0.0;
        foreach (var target in targets)
        {
            var r = target.Intersect(bounds);
            if (r.Width <= 0 || r.Height <= 0)
            {
                continue;
            }
            var cols = Math.Max(1, (int)Math.Ceiling(r.Width / Spacing));
            var rows = Math.Max(1, (int)Math.Ceiling(r.Height / Spacing));
            var first = v;
            for (var j = 0; j <= rows; j++)
            {
                var y = r.Y + r.Height * j / rows;
                for (var i = 0; i <= cols; i++)
                {
                    var x = r.X + r.Width * i / cols;
                    _srcX[v] = (float)(x * _scale);
                    _srcY[v] = (float)(y * _scale);
                    var dx = x - center.X;
                    var dy = y - center.Y;
                    _r0[v] = Math.Sqrt(dx * dx + dy * dy);
                    _theta0[v] = Math.Atan2(dy, dx);
                    farthest = Math.Max(farthest, _r0[v]);
                    v++;
                }
            }
            for (var j = 0; j < rows; j++)
            {
                for (var i = 0; i < cols; i++)
                {
                    var a = first + j * (cols + 1) + i;
                    var b = a + 1;
                    var c = a + cols + 1;
                    var d = c + 1;
                    AddTriangle(a, b, d);
                    AddTriangle(a, d, c);
                }
            }
        }
        Stagger = BlackHoleMath.Stagger(farthest);
        MapFlatTiles();
        AllocateLuts();
        for (var i = 0; i < v; i++)
        {
            _start[i] = BlackHoleMath.FallStart(_r0[i], Stagger);
        }
    }

    private void AddTriangle(int a, int b, int c)
    {
        _tris[_triCount * 3] = a;
        _tris[_triCount * 3 + 1] = b;
        _tris[_triCount * 3 + 2] = c;
        _triCount++;
    }

    /// <summary>Where mesh vertex <paramref name="index"/> is at swallow time <paramref name="t"/> (DIP of the content).</summary>
    public Point VertexAt(int index, double t)
    {
        var f = BlackHoleMath.FallFactor(t, _start[index]);
        var (x, y) = BlackHoleMath.Spiral(_r0[index], _theta0[index], f);
        return new Point(Center.X + x, Center.Y + y);
    }

    /// <summary>
    /// Paints the frame at swallow time <paramref name="t"/> (ms on the 1.3 s reference scale) into the back buffer and
    /// makes it <see cref="Output"/>. Safe to call on a worker thread while the previous <see cref="Output"/> is read.
    /// </summary>
    public void Render(double t)
    {
        var started = Stopwatch.GetTimestamp();
        Prepare(t);
        if (_bands > 1 && Environment.ProcessorCount > 1)
        {
            Parallel.For(0, _bands, _band);
        }
        else
        {
            for (var b = 0; b < _bands; b++)
            {
                RenderBand(b);
            }
        }
        (_front, _back) = (_back, _front);
        LastFrameMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    /// <summary>Per-frame setup shared by all bands: hole state, mesh positions, triangle setup and bins, radial tables.</summary>
    private void Prepare(double t)
    {
        var look = _look;
        var hole = BlackHoleMath.Hole(t, look.CoreRadius, look.CoreGrowth, look.ShockRadius, look.ShockOpacity);
        LastHole = hole;
        _cx = Center.X * _scale;
        _cy = Center.Y * _scale;
        _core = hole.CoreRadius;
        _einstein = look.EinsteinRatio * hole.CoreRadius * _scale;
        _shockR = hole.ShockRadius;
        _shockA = hole.ShockOpacity;
        _ringRgb = Pack(look.PhotonRingColor);
        _shockRgb = Pack(look.ShockColor);
        // The mesh at time t: every vertex on its spiral.
        for (var i = 0; i < _srcX.Length; i++)
        {
            var f = BlackHoleMath.FallFactor(t, _start[i]);
            if (f >= 1)
            {
                _dstX[i] = _srcX[i];
                _dstY[i] = _srcY[i];
                continue;
            }
            var (x, y) = BlackHoleMath.Spiral(_r0[i], _theta0[i], f);
            _dstX[i] = (float)(_cx + x * _scale);
            _dstY[i] = (float)(_cy + y * _scale);
        }
        SetupTriangles();
        BinTriangles();
        BuildHoleLut();
        BuildShockLut();
    }

    /// <summary>Times each pass alone over the whole frame on one thread, on <see cref="ProfileClock"/> (benchmark).</summary>
    internal (double Lens, double Mesh, double Hole, double Shock) ProfilePasses(double t)
    {
        Render(t);
        Prepare(t);
        var clock = ProfileClock;
        var a = clock();
        for (var b = 0; b < _bands; b++)
        {
            LensRows(b);
        }
        var lens = clock() - a;
        a = clock();
        for (var b = 0; b < _bands; b++)
        {
            MeshRows(b);
        }
        var mesh = clock() - a;
        a = clock();
        for (var b = 0; b < _bands; b++)
        {
            HoleRows(b);
        }
        var holeMs = clock() - a;
        a = clock();
        for (var b = 0; b < _bands; b++)
        {
            ShockRows(b);
        }
        return (lens, mesh, holeMs, clock() - a);
    }

    private uint Pack(Color color)
    {
        // RGB only (alpha is applied per pixel); straight values, premultiplied when blended.
        return _redFirst
            ? (uint)(color.R | (color.G << 8) | (color.B << 16))
            : (uint)(color.B | (color.G << 8) | (color.R << 16));
    }

    private void SetupTriangles()
    {
        var s = _setup;
        for (var t = 0; t < _triCount; t++)
        {
            var o = t * SetupStride;
            var ia = _tris[t * 3];
            var ib = _tris[t * 3 + 1];
            var ic = _tris[t * 3 + 2];
            float ax = _dstX[ia], ay = _dstY[ia], bx = _dstX[ib], by = _dstY[ib], cx = _dstX[ic], cy = _dstY[ic];
            var area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
            if (Math.Abs(area) < 1e-4f)
            {
                s[o + 15] = 0;
                continue;
            }
            if (area < 0)
            {
                (ib, ic) = (ic, ib);
                (bx, cx) = (cx, bx);
                (by, cy) = (cy, by);
                area = -area;
            }
            var minX = Math.Min(ax, Math.Min(bx, cx));
            var maxX = Math.Max(ax, Math.Max(bx, cx));
            var minY = Math.Min(ay, Math.Min(by, cy));
            var maxY = Math.Max(ay, Math.Max(by, cy));
            if (maxX < 0 || maxY < 0 || minX >= _w || minY >= _h)
            {
                s[o + 15] = 0;
                continue;
            }
            s[o] = ax;
            s[o + 1] = ay;
            s[o + 2] = bx;
            s[o + 3] = by;
            s[o + 4] = cx;
            s[o + 5] = cy;
            s[o + 6] = 1f / area;
            s[o + 7] = minX;
            s[o + 8] = maxX;
            s[o + 9] = minY;
            s[o + 10] = maxY;
            s[o + 11] = _srcX[ia];
            s[o + 12] = _srcY[ia];
            // Source of B and C, as vertex indices (exact in float up to 2^24 vertices).
            s[o + 13] = ib;
            s[o + 14] = ic;
            s[o + 15] = 1;
        }
    }

    /// <summary>The bands a live triangle's rows fall in (inclusive).</summary>
    private (int First, int Last) BandSpan(int o)
    {
        var s = _setup;
        var first = Math.Max(0, (int)Math.Floor(s[o + 9]) / BandHeight);
        var last = Math.Min(_bands - 1, Math.Max(0, (int)Math.Ceiling(s[o + 10])) / BandHeight);
        return (first, last);
    }

    /// <summary>Counting sort of the live triangles into the bands their rows cover, keeping mesh order inside a band.</summary>
    private void BinTriangles()
    {
        var start = _binStart;
        Array.Clear(start);
        var s = _setup;
        for (var t = 0; t < _triCount; t++)
        {
            var o = t * SetupStride;
            if (s[o + 15] == 0)
            {
                continue;
            }
            var (first, last) = BandSpan(o);
            for (var b = first; b <= last; b++)
            {
                start[b + 1]++;
            }
        }
        for (var b = 0; b < _bands; b++)
        {
            start[b + 1] += start[b];
        }
        if (start[_bands] > _binItems.Length)
        {
            // Long stretched arms late in a fall: grow once (rare), never shrink.
            _binItems = new int[start[_bands] + start[_bands] / 2];
        }
        Array.Copy(start, _binCursor, _bands);
        for (var t = 0; t < _triCount; t++)
        {
            var o = t * SetupStride;
            if (s[o + 15] == 0)
            {
                continue;
            }
            var (first, last) = BandSpan(o);
            for (var b = first; b <= last; b++)
            {
                _binItems[_binCursor[b]++] = t;
            }
        }
    }

    /// <summary>The triangles binned to <paramref name="band"/>, in mesh order (tests).</summary>
    internal ReadOnlySpan<int> BinnedTriangles(int band) => _binItems.AsSpan(_binStart[band], _binStart[band + 1] - _binStart[band]);

    /// <summary>Whether triangle <paramref name="index"/> is drawn this frame and its row range (tests).</summary>
    internal (bool Live, float MinY, float MaxY) TriangleRows(int index)
    {
        var o = index * SetupStride;
        return (_setup[o + 15] != 0, _setup[o + 9], _setup[o + 10]);
    }

    private void RenderBand(int band)
    {
        LensRows(band);
        MeshRows(band);
        HoleRows(band);
        ShockRows(band);
    }

    // ------------------------------------------------------------------ pass 1: lens

    /// <summary>Finds the flat tiles of the rest snapshot and their margins (once per run).</summary>
    private void MapFlatTiles()
    {
        _tilesX = (_w + Tile - 1) / Tile;
        _tilesY = (_h + Tile - 1) / Tile;
        _tileMargin = new int[_tilesX * _tilesY];
        _tileColor = new uint[_tilesX * _tilesY];
        for (var ty = 0; ty < _tilesY; ty++)
        {
            for (var tx = 0; tx < _tilesX; tx++)
            {
                var i = ty * _tilesX + tx;
                var x0 = tx * Tile;
                var y0 = ty * Tile;
                var x1 = Math.Min(_w, x0 + Tile);
                var y1 = Math.Min(_h, y0 + Tile);
                var color = _rest[y0 * _w + x0];
                var flat = x1 - x0 == Tile && y1 - y0 == Tile;
                for (var y = y0; y < y1 && flat; y++)
                {
                    var row = y * _w;
                    for (var x = x0; x < x1; x++)
                    {
                        if (_rest[row + x] != color)
                        {
                            flat = false;
                            break;
                        }
                    }
                }
                _tileMargin[i] = flat ? 0 : -1;
                _tileColor[i] = color;
            }
        }
        // Grow the margins ring by ring (up to 6 tiles).
        for (var k = 1; k <= 6; k++)
        {
            var grew = false;
            for (var ty = 1; ty < _tilesY - 1; ty++)
            {
                for (var tx = 1; tx < _tilesX - 1; tx++)
                {
                    var i = ty * _tilesX + tx;
                    if (_tileMargin[i] != k - 1)
                    {
                        continue;
                    }
                    var ok = true;
                    for (var dy = -1; dy <= 1 && ok; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            var j = i + dy * _tilesX + dx;
                            if (_tileMargin[j] < k - 1 || _tileColor[j] != _tileColor[i])
                            {
                                ok = false;
                                break;
                            }
                        }
                    }
                    if (ok)
                    {
                        // Marked k + 100 for this ring so later tiles of the same ring still see k − 1.
                        _tileMargin[i] = k + 100;
                        grew = true;
                    }
                }
            }
            for (var i = 0; i < _tileMargin.Length; i++)
            {
                if (_tileMargin[i] > 100)
                {
                    _tileMargin[i] -= 100;
                }
            }
            if (!grew)
            {
                break;
            }
        }
    }

    /// <summary>The number of flat tiles (tests and diagnostics).</summary>
    internal int FlatTiles => _tileMargin.Count(m => m >= 0);

    /// <summary>
    /// Whether the lens mapping may be interpolated bilinearly across an 8 × 8 tile whose nearest point is
    /// <paramref name="rmin"/> device px from the hole, for Einstein radius <paramref name="einstein"/> (px). The displacement
    /// θE²/r has second derivatives below 6·θE²/r³, so bilinear interpolation over a side h errs by at most
    /// (h²/8)·2·6·θE²/r³ = 1.5·h²·θE²/r³; this keeps that under <see cref="LensTolerance"/>.
    /// </summary>
    internal static bool CanInterpolateLens(float rmin, float einstein)
    {
        if (rmin <= 1.5f * einstein)
        {
            return false;
        }
        return 1.5f * Tile * Tile * einstein * einstein < LensTolerance * rmin * rmin * rmin;
    }

    private void LensRows(int band)
    {
        var w = _w;
        var y0 = band * BandHeight;
        var y1 = Math.Min(_h, y0 + BandHeight);
        var output = _back;
        if (_einstein <= 0.01)
        {
            Array.Copy(_rest, y0 * w, output, y0 * w, (y1 - y0) * w);
            return;
        }
        var e2 = (float)(_einstein * _einstein);
        var einstein = (float)_einstein;
        var cx = (float)_cx;
        var cy = (float)_cy;
        // Bands are one tile row high: decide per tile.
        var ty = y0 / Tile;
        for (var tx = 0; tx < _tilesX; tx++)
        {
            var x0 = tx * Tile;
            var x1 = Math.Min(w, x0 + Tile);
            // Nearest point of the tile to the hole; beyond the Einstein ring the shift there is the largest, e2 / r.
            var nx = Math.Clamp(cx, x0, x1) - cx;
            var ny = Math.Clamp(cy, y0, y1) - cy;
            var rmin = MathF.Sqrt(nx * nx + ny * ny);
            var margin = _tileMargin[ty * _tilesX + tx];
            if (margin >= 1 && rmin > 1.5f * einstein && e2 / rmin + 1 <= margin * Tile)
            {
                // A flat tile that the lens keeps inside its flat surroundings.
                var color = _tileColor[ty * _tilesX + tx];
                for (var y = y0; y < y1; y++)
                {
                    output.AsSpan(y * w + x0, x1 - x0).Fill(color);
                }
                continue;
            }
            if (CanInterpolateLens(rmin, einstein))
            {
                LensTileInterpolated(output, x0, x1, y0, y1, e2, cx, cy);
            }
            else
            {
                LensTileExact(output, x0, x1, y0, y1, e2, cx, cy);
            }
        }
    }

    /// <summary>The lens source (pixel-index coordinates) of the pixel centre (px, py).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LensSource(float px, float py, float e2, float cx, float cy, out float sx, out float sy)
    {
        var dx = px - cx;
        var dy = py - cy;
        var f = 1 - e2 / (dx * dx + dy * dy);
        sx = cx + dx * f - 0.5f;
        sy = cy + dy * f - 0.5f;
    }

    private void LensTileExact(uint[] output, int x0, int x1, int y0, int y1, float e2, float cx, float cy)
    {
        var w = _w;
        for (var y = y0; y < y1; y++)
        {
            var dy = y + 0.5f - cy;
            var dy2 = dy * dy;
            var row = y * w;
            for (var x = x0; x < x1; x++)
            {
                var dx = x + 0.5f - cx;
                var r2 = dx * dx + dy2;
                if (r2 < 1e-6f)
                {
                    output[row + x] = 0;
                    continue;
                }
                var f = 1 - e2 / r2;
                output[row + x] = SampleFast(_rest, cx + dx * f - 0.5f, cy + dy * f - 0.5f);
            }
        }
    }

    /// <summary>The exact mapping at the tile's four corner pixel centres, bilinear in between.</summary>
    private void LensTileInterpolated(uint[] output, int x0, int x1, int y0, int y1, float e2, float cx, float cy)
    {
        var w = _w;
        float xa = x0 + 0.5f, xb = x1 - 0.5f, ya = y0 + 0.5f, yb = y1 - 0.5f;
        LensSource(xa, ya, e2, cx, cy, out var s00x, out var s00y);
        LensSource(xb, ya, e2, cx, cy, out var s10x, out var s10y);
        LensSource(xa, yb, e2, cx, cy, out var s01x, out var s01y);
        LensSource(xb, yb, e2, cx, cy, out var s11x, out var s11y);
        var spanX = x1 - x0 - 1;
        var spanY = y1 - y0 - 1;
        var invX = spanX > 0 ? 1f / spanX : 0f;
        var invY = spanY > 0 ? 1f / spanY : 0f;
        for (var y = y0; y < y1; y++)
        {
            var v = (y - y0) * invY;
            var lx = s00x + (s01x - s00x) * v;
            var ly = s00y + (s01y - s00y) * v;
            var rx = s10x + (s11x - s10x) * v;
            var ry = s10y + (s11y - s10y) * v;
            var stepX = (rx - lx) * invX;
            var stepY = (ry - ly) * invX;
            var row = y * w;
            for (var x = x0; x < x1; x++)
            {
                var i = x - x0;
                output[row + x] = SampleFast(_rest, lx + stepX * i, ly + stepY * i);
            }
        }
    }

    // ------------------------------------------------------------------ pass 2: falling mesh

    private void MeshRows(int band)
    {
        var y0 = band * BandHeight;
        var y1 = Math.Min(_h, y0 + BandHeight);
        var s = _setup;
        var w = _w;
        var output = _back;
        var from = _binStart[band];
        var to = _binStart[band + 1];
        for (var n = from; n < to; n++)
        {
            var o = _binItems[n] * SetupStride;
            if (s[o + 10] < y0 || s[o + 9] >= y1)
            {
                continue;
            }
            float ax = s[o], ay = s[o + 1], bx = s[o + 2], by = s[o + 3], cx = s[o + 4], cy = s[o + 5];
            var inv = s[o + 6];
            var ib = (int)s[o + 13];
            var ic = (int)s[o + 14];
            float ua = s[o + 11], va = s[o + 12], ub = _srcX[ib], vb = _srcY[ib], uc = _srcX[ic], vc = _srcY[ic];
            var rowFrom = Math.Max(y0, (int)Math.Floor(s[o + 9]));
            var rowTo = Math.Min(y1 - 1, (int)Math.Ceiling(s[o + 10]));
            var colMin = Math.Max(0f, (float)Math.Floor(s[o + 7]));
            var colMax = Math.Min(w - 1f, (float)Math.Ceiling(s[o + 8]));
            // Edge functions, each linear in x on a row: E(px) = kx·px + (k0 + ky·py). wa for edge BC, wb for CA, wc for AB.
            float kaX = -(cy - by), kaY = cx - bx, ka0 = -(cx - bx) * by + (cy - by) * bx;
            float kbX = -(ay - cy), kbY = ax - cx, kb0 = -(ax - cx) * cy + (ay - cy) * cx;
            float kcX = -(by - ay), kcY = bx - ax, kc0 = -(bx - ax) * ay + (by - ay) * ax;
            for (var y = rowFrom; y <= rowTo; y++)
            {
                var py = y + 0.5f;
                var ea = ka0 + kaY * py;
                var eb = kb0 + kbY * py;
                var ec = kc0 + kcY * py;
                // The span of pixel centres where all three are ≥ 0.
                var lo = colMin + 0.5f;
                var hi = colMax + 0.5f;
                if (!Clip(kaX, ea, ref lo, ref hi) || !Clip(kbX, eb, ref lo, ref hi) || !Clip(kcX, ec, ref lo, ref hi))
                {
                    continue;
                }
                var x0 = (int)Math.Ceiling(lo - 0.5f);
                var x1 = (int)Math.Floor(hi - 0.5f);
                var row = y * w;
                for (var x = x0; x <= x1; x++)
                {
                    var px = x + 0.5f;
                    var wa = Math.Max(0, kaX * px + ea);
                    var wb = Math.Max(0, kbX * px + eb);
                    var wc = Math.Max(0, kcX * px + ec);
                    var u = (wa * ua + wb * ub + wc * uc) * inv;
                    var v = (wa * va + wb * vb + wc * vc) * inv;
                    var src = SampleFast(_items, u - 0.5f, v - 0.5f);
                    if (src == 0)
                    {
                        continue;
                    }
                    output[row + x] = Over(src, output[row + x]);
                }
            }
        }
    }

    /// <summary>Narrows [lo, hi] to where k·x + e ≥ 0; false when nothing is left.</summary>
    private static bool Clip(float k, float e, ref float lo, ref float hi)
    {
        if (k > 1e-12f)
        {
            lo = Math.Max(lo, -e / k);
        }
        else if (k < -1e-12f)
        {
            hi = Math.Min(hi, -e / k);
        }
        else if (e < 0)
        {
            return false;
        }
        return lo <= hi;
    }

    // ------------------------------------------------------------------ radial tables

    /// <summary>Sizes the tables for the largest hole and ring of the run (the core peaks at R·(1 + growth), 10 % more while opening).</summary>
    private void AllocateLuts()
    {
        var look = _look;
        var coreMax = Math.Max(0.01, look.CoreRadius) * (1 + Math.Max(0, look.CoreGrowth)) * 1.15;
        var reach = HoleReach(coreMax, coreMax / Math.Max(0.01, look.CoreRadius));
        _holeLut = new uint[(int)Math.Ceiling(reach * _scale / LutStepMin) + 4];
        var sigma = Math.Max(0.3, look.ShockWidth / 2.3548);
        _shockLut = new uint[(int)Math.Ceiling(6 * sigma * _scale / LutStep) + 4];
    }

    /// <summary>How far the hole's halo reaches (DIP) for core radius <paramref name="core"/> at geometry scale <paramref name="k"/>.</summary>
    private double HoleReach(double core, double k)
    {
        var decay = Math.Max(0.1, _look.HaloDecay * k);
        return core + 4 * k + decay * Math.Log(Math.Max(1, _look.HaloOpacity * 512));
    }

    /// <summary>
    /// The halo and photon ring at <paramref name="rp"/> device px from the hole's centre, composed in that order
    /// (premultiplied "over" is associative, so both layers blend onto the content as one color).
    /// </summary>
    internal uint HoleColorAt(double rp)
    {
        var look = _look;
        var k = _core / Math.Max(0.01, look.CoreRadius);
        var ringCenter = _core + 1.5 * k;
        var ringSigma = Math.Max(0.2, look.PhotonRingWidth * k / 2.3548);
        var haloStart = _core + 4 * k;
        var haloFrom = _core + 0.5 * k;
        var decay = Math.Max(0.1, look.HaloDecay * k);
        var haloA = look.HaloOpacity;
        var ringA = look.PhotonRingColor.A / 255.0;
        var r = rp / _scale;
        uint color = 0;
        double halo;
        if (r >= haloStart)
        {
            halo = haloA * Math.Exp(-(r - haloStart) / decay);
        }
        else if (r > haloFrom)
        {
            var p = (r - haloFrom) / (haloStart - haloFrom);
            halo = haloA * p * p * (3 - 2 * p);
        }
        else
        {
            halo = 0;
        }
        if (halo > 0.002)
        {
            color = Gray(0, halo);
        }
        var z = (r - ringCenter) / ringSigma;
        var ring = ringA * Math.Exp(-0.5 * z * z);
        if (ring > 0.002)
        {
            color = Over(Tint(_ringRgb, ring), color);
        }
        return color;
    }

    /// <summary>The black core's coverage at <paramref name="rp"/> device px (1 px antialiased edge), kept out of the table
    /// because its clamp has a corner that linear interpolation would round off.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CoreAt(float corePx, float rp)
    {
        var cover = corePx - rp + 0.5f;
        if (cover <= 0)
        {
            return 0;
        }
        return cover >= 1 ? 0xFF000000u : (uint)(cover * 255 + 0.5f) << 24;
    }

    /// <summary>The hole blended onto <paramref name="dst"/> at <paramref name="rp"/> device px, as the frame does it (tests).</summary>
    internal uint HoleBlendAt(float rp, uint dst)
    {
        var halo = HoleLutAt(rp);
        if (halo != 0)
        {
            dst = Over(halo, dst);
        }
        var core = _holeLutCount == 0 ? 0 : CoreAt((float)(_core * _scale), rp);
        return core == 0 ? dst : Over(core, dst);
    }

    /// <summary>The shockwave's color at <paramref name="rp"/> device px from the centre.</summary>
    internal uint ShockColorAt(double rp)
    {
        var sigma = Math.Max(0.3, _look.ShockWidth / 2.3548);
        var z = (rp / _scale - _shockR) / sigma;
        var a = _shockA * Math.Exp(-0.5 * z * z);
        return a > 0.002 ? Tint(_shockRgb, a) : 0;
    }

    private void BuildHoleLut()
    {
        _holeLutCount = 0;
        if (_core <= 0.01)
        {
            return;
        }
        var k = _core / Math.Max(0.01, _look.CoreRadius);
        _holeReachPx = (float)(HoleReach(_core, k) * _scale);
        // At least six samples per standard deviation of the photon ring (its peak then interpolates within a level).
        var ringSigmaPx = (float)(Math.Max(0.2, _look.PhotonRingWidth * k / 2.3548) * _scale);
        _holeStep = Math.Clamp(ringSigmaPx / 6, LutStepMin, LutStep);
        var count = (int)Math.Ceiling(_holeReachPx / _holeStep) + 2;
        if (count > _holeLut.Length)
        {
            _holeLut = new uint[count + 8];
        }
        for (var i = 0; i < count; i++)
        {
            _holeLut[i] = HoleColorAt(i * _holeStep);
        }
        _holeLutCount = count;
    }

    private void BuildShockLut()
    {
        _shockLutCount = 0;
        if (_shockA <= 0.002 || _shockR <= 0)
        {
            return;
        }
        var sigma = Math.Max(0.3, _look.ShockWidth / 2.3548);
        _shockOuterPx = (float)((_shockR + 3 * sigma) * _scale);
        _shockInnerPx = (float)(Math.Max(0, _shockR - 3 * sigma) * _scale);
        var count = (int)Math.Ceiling((_shockOuterPx - _shockInnerPx) / LutStep) + 2;
        if (count > _shockLut.Length)
        {
            _shockLut = new uint[count + 8];
        }
        for (var i = 0; i < count; i++)
        {
            _shockLut[i] = ShockColorAt(_shockInnerPx + i * LutStep);
        }
        _shockLutCount = count;
    }

    /// <summary>A radial table read at <paramref name="index"/> (fractional), interpolated between its entries.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadLut(uint[] lut, int count, float index)
    {
        var i = (int)index;
        if (i >= count - 1)
        {
            return lut[count - 1];
        }
        return Lerp(lut[i], lut[i + 1], (uint)((index - i) * 256f));
    }

    /// <summary>The halo-and-ring table at <paramref name="rp"/> device px, as the frame reads it (tests).</summary>
    internal uint HoleLutAt(float rp) => _holeLutCount == 0 ? 0 : ReadLut(_holeLut, _holeLutCount, rp / _holeStep);

    /// <summary>The shockwave table at <paramref name="rp"/> device px, as the frame reads it (tests).</summary>
    internal uint ShockLutAt(float rp) => _shockLutCount == 0 || rp < _shockInnerPx || rp > _shockOuterPx
        ? 0
        : ReadLut(_shockLut, _shockLutCount, (rp - _shockInnerPx) / LutStep);

    // ------------------------------------------------------------------ pass 3: the hole

    private void HoleRows(int band)
    {
        if (_holeLutCount == 0)
        {
            return;
        }
        var y0 = band * BandHeight;
        var y1 = Math.Min(_h, y0 + BandHeight);
        var output = _back;
        var lut = _holeLut;
        var count = _holeLutCount;
        var reach = _holeReachPx;
        var reach2 = reach * reach;
        var corePx = (float)(_core * _scale);
        var invStep = 1 / _holeStep;
        var cx = (float)_cx;
        var cy = (float)_cy;
        var top = Math.Max(y0, (int)MathF.Floor(cy - reach));
        var bottom = Math.Min(y1 - 1, (int)MathF.Ceiling(cy + reach));
        for (var y = top; y <= bottom; y++)
        {
            var dy = y + 0.5f - cy;
            var dy2 = dy * dy;
            var half = MathF.Sqrt(MathF.Max(0, reach2 - dy2));
            var left = Math.Max(0, (int)MathF.Floor(cx - half));
            var right = Math.Min(_w - 1, (int)MathF.Ceiling(cx + half));
            var row = y * _w;
            for (var x = left; x <= right; x++)
            {
                var dx = x + 0.5f - cx;
                var d2 = dx * dx + dy2;
                if (d2 > reach2)
                {
                    continue;
                }
                var rp = MathF.Sqrt(d2);
                var color = ReadLut(lut, count, rp * invStep);
                var dst = output[row + x];
                if (color != 0)
                {
                    dst = Over(color, dst);
                }
                var core = CoreAt(corePx, rp);
                output[row + x] = core == 0 ? dst : Over(core, dst);
            }
        }
    }

    // ------------------------------------------------------------------ pass 4: shockwave

    private void ShockRows(int band)
    {
        if (_shockLutCount == 0)
        {
            return;
        }
        var y0 = band * BandHeight;
        var y1 = Math.Min(_h, y0 + BandHeight);
        var output = _back;
        var lut = _shockLut;
        var count = _shockLutCount;
        var outer = _shockOuterPx;
        var inner = _shockInnerPx;
        var cx = (float)_cx;
        var cy = (float)_cy;
        var top = Math.Max(y0, (int)MathF.Floor(cy - outer));
        var bottom = Math.Min(y1 - 1, (int)MathF.Ceiling(cy + outer));
        if (top > bottom)
        {
            return;
        }
        var outer2 = outer * outer;
        var inner2 = inner * inner;
        for (var y = top; y <= bottom; y++)
        {
            var dy = y + 0.5f - cy;
            var dy2 = dy * dy;
            var half = MathF.Sqrt(MathF.Max(0, outer2 - dy2));
            var left = Math.Max(0, (int)MathF.Floor(cx - half));
            var right = Math.Min(_w - 1, (int)MathF.Ceiling(cx + half));
            // Skip the inside of the ring on this row.
            var innerHalf = inner2 > dy2 ? MathF.Sqrt(inner2 - dy2) : -1f;
            var holeFrom = innerHalf > 0 ? (int)MathF.Ceiling(cx - innerHalf) : int.MaxValue;
            var holeTo = innerHalf > 0 ? (int)MathF.Floor(cx + innerHalf) - 1 : int.MinValue;
            var row = y * _w;
            for (var x = left; x <= right; x++)
            {
                if (x == holeFrom && holeTo >= holeFrom)
                {
                    x = holeTo;
                    continue;
                }
                var dx = x + 0.5f - cx;
                var d2 = dx * dx + dy2;
                if (d2 > outer2 || d2 < inner2)
                {
                    continue;
                }
                var color = ReadLut(lut, count, (MathF.Sqrt(d2) - inner) * (1 / LutStep));
                if (color != 0)
                {
                    output[row + x] = Over(color, output[row + x]);
                }
            }
        }
    }

    // ------------------------------------------------------------------ pixels

    /// <summary><see cref="Sample"/> in single precision, with the inside-the-buffer case inlined.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint SampleFast(uint[] src, float x, float y)
    {
        var ix = (int)(x + 4096f) - 4096;
        var iy = (int)(y + 4096f) - 4096;
        if ((uint)ix >= (uint)(_w - 1) || (uint)iy >= (uint)(_h - 1))
        {
            return Sample(src, x, y);
        }
        var i = iy * _w + ix;
        var c00 = src[i];
        var c10 = src[i + 1];
        var c01 = src[i + _w];
        var c11 = src[i + _w + 1];
        if (c00 == c10 && c00 == c01 && c00 == c11)
        {
            return c00;
        }
        var wx = (uint)((x - ix) * 256f);
        return Lerp(Lerp(c00, c10, wx), Lerp(c01, c11, wx), (uint)((y - iy) * 256f));
    }

    /// <summary>Bilinear sample of a premultiplied buffer at pixel coordinates (texel centres on integers); transparent outside.</summary>
    private uint Sample(uint[] src, double x, double y)
    {
        var fx = Math.Floor(x);
        var fy = Math.Floor(y);
        var x0 = (int)fx;
        var y0 = (int)fy;
        if (x0 < -1 || y0 < -1 || x0 >= _w || y0 >= _h)
        {
            return 0;
        }
        var wx = (uint)((x - fx) * 256);
        var wy = (uint)((y - fy) * 256);
        var w = _w;
        uint c00 = 0, c10 = 0, c01 = 0, c11 = 0;
        var inX0 = x0 >= 0;
        var inX1 = x0 + 1 < w;
        if (y0 >= 0)
        {
            var row = y0 * w;
            if (inX0)
            {
                c00 = src[row + x0];
            }
            if (inX1)
            {
                c10 = src[row + x0 + 1];
            }
        }
        if (y0 + 1 < _h)
        {
            var row = (y0 + 1) * w;
            if (inX0)
            {
                c01 = src[row + x0];
            }
            if (inX1)
            {
                c11 = src[row + x0 + 1];
            }
        }
        if ((c00 | c10 | c01 | c11) == 0)
        {
            return 0;
        }
        return Lerp(Lerp(c00, c10, wx), Lerp(c01, c11, wx), wy);
    }

    /// <summary>Per-channel a + (b − a)·w/256 on packed pixels, two channels per multiply.</summary>
    internal static uint Lerp(uint a, uint b, uint w)
    {
        if (w == 0 || a == b)
        {
            return a;
        }
        var iw = 256 - w;
        var rb = (((a & 0x00FF00FFu) * iw + (b & 0x00FF00FFu) * w) >> 8) & 0x00FF00FFu;
        var ga = ((((a >> 8) & 0x00FF00FFu) * iw + ((b >> 8) & 0x00FF00FFu) * w) >> 8) & 0x00FF00FFu;
        return rb | (ga << 8);
    }

    /// <summary>Premultiplied source over destination.</summary>
    internal static uint Over(uint src, uint dst)
    {
        var sa = src >> 24;
        if (sa >= 255 || dst == 0)
        {
            return src;
        }
        var k = 255 - sa;
        return src + (Div255(dst & 0x00FF00FFu, k) | (Div255((dst >> 8) & 0x00FF00FFu, k) << 8));
    }

    /// <summary>Two 8-bit lanes times <paramref name="k"/> / 255, rounded.</summary>
    private static uint Div255(uint lanes, uint k)
    {
        var t = lanes * k + 0x00800080u;
        return ((t + ((t >> 8) & 0x00FF00FFu)) >> 8) & 0x00FF00FFu;
    }

    /// <summary>A premultiplied gray at <paramref name="alpha"/> (0–1).</summary>
    private static uint Gray(byte level, double alpha)
    {
        var a = (uint)Math.Clamp(alpha * 255 + 0.5, 0, 255);
        var g = (uint)(level * a / 255);
        return (a << 24) | (g << 16) | (g << 8) | g;
    }

    /// <summary>A packed straight RGB at <paramref name="alpha"/>, premultiplied.</summary>
    private static uint Tint(uint rgb, double alpha)
    {
        var a = (uint)Math.Clamp(alpha * 255 + 0.5, 0, 255);
        var c0 = (rgb & 0xFF) * a / 255;
        var c1 = ((rgb >> 8) & 0xFF) * a / 255;
        var c2 = ((rgb >> 16) & 0xFF) * a / 255;
        return (a << 24) | (c2 << 16) | (c1 << 8) | c0;
    }
}
