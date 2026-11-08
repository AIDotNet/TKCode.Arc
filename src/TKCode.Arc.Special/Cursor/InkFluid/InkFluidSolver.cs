using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The fluid of <see cref="InkFluid"/> without any UI: Stam's stable fluids on a small velocity grid (semi-Lagrangian
/// advection, vorticity confinement, a Jacobi pressure solve) carrying two shares of ink on a finer grid, a neutral
/// share and an accent share that hands itself to the neutral one as it spreads. Everything runs on the CPU, so it works
/// on every Avalonia platform; <see cref="Compose"/> turns the ink into premultiplied BGRA pixels.
/// </summary>
/// <remarks>
/// Grid coordinates are cell centres with y down; velocities are in velocity cells per second. Positions handed in from
/// outside are fractions of the field (0 to 1 on both axes).
/// <para>
/// The hot loops read the arrays through refs without bounds checks: every index is derived from the grid sizes that
/// <see cref="Resize"/> allocated. The stencils (pressure, picture) take four cells at a time where SIMD is accelerated,
/// and their scalar tails use the same formulas. Ink below <see cref="InkFloor"/> is dropped, so most of a calm field is
/// exact zeros: each ink row keeps the span of cells that hold ink, and advection and composing skip everything outside.
/// </para>
/// </remarks>
internal sealed class InkFluidSolver
{
    /// <summary>The pressure solve runs 20 Jacobi iterations per step.</summary>
    public const int PressureIterations = 20;

    // How fast velocity and ink fade, per second (divided by 1 + rate·dt each step).
    private const float VelocityDissipation = 0.3f;
    private const float InkDissipation = 1.0f;

    // How fast accent ink turns neutral, per second.
    private const float TintFade = 0.25f;

    // Vorticity confinement: puts back the small swirls the coarse grid smears out.
    private const float CurlStrength = 16f;

    // The pressure of the last step is kept at this share as the next solve's starting guess.
    private const float PressureCarry = 0.8f;

    // Below this density a cell paints nothing.
    private const float EmptyDensity = 0.0004f;

    // Accent ink covers this much more than neutral ink at the same density.
    private const float AccentBoost = 1.7f;

    // Ink below this is invisible (the picture ignores density under 0.0004) and is dropped. Without it, exponential fading
    // never reaches zero: every cell stays live, and values sink into denormals, which are very slow on x64.
    private const float InkFloor = 1e-4f;

    // The same for velocity (cells per second) and pressure.
    private const float VelocityFloor = 1e-4f;
    private const float PressureFloor = 1e-6f;

    // Cost grows with the cell count, so a wide banner would cost several times a card at the same quality. Past this
    // aspect the grids shrink to keep the cell count of this aspect; the cells stay square.
    private const double MaxCostAspect = 2.5;

    private static readonly float[] Absorb = BuildAbsorb();

    private float[] _u = [];
    private float[] _v = [];
    private float[] _uNext = [];
    private float[] _vNext = [];
    private float[] _pressure = [];
    private float[] _pressureNext = [];
    private float[] _divergence = [];
    private float[] _curl = [];
    private float[] _neutral = [];
    private float[] _accent = [];
    private float[] _neutralNext = [];
    private float[] _accentNext = [];
    private float[] _density = [];
    private float[] _dither = [];

    // Per ink row, the first and last cell that may hold ink (first > last when none does); every cell outside is zero.
    private int[] _inkFirst = [];
    private int[] _inkLast = [];
    private int[] _nextFirst = [];
    private int[] _nextLast = [];

    // Advection's lookups: where each ink column falls on the velocity grid, and one velocity row at an ink row's height.
    private int[] _columnCell = [];
    private float[] _columnWeight = [];
    private float[] _rowU = [];
    private float[] _rowV = [];

    /// <summary>Velocity grid columns and rows.</summary>
    public int Columns { get; private set; }

    public int Rows { get; private set; }

    /// <summary>Ink grid columns and rows (the size of the composed picture).</summary>
    public int InkColumns { get; private set; }

    public int InkRows { get; private set; }

    /// <summary>
    /// Sizes the grids for a field of <paramref name="aspect"/> (width over height): the velocity grid has
    /// <paramref name="cells"/> on its short side, the ink grid <paramref name="inkCells"/>, both fewer on a field wider
    /// than <see cref="MaxCostAspect"/> (see there). Clears the fluid.
    /// </summary>
    public void Resize(double aspect, int cells, int inkCells)
    {
        aspect = double.IsFinite(aspect) && aspect > 0 ? Math.Clamp(aspect, 0.1, 10) : 1;
        var stretch = aspect >= 1 ? aspect : 1 / aspect;
        var shrink = stretch > MaxCostAspect ? Math.Sqrt(MaxCostAspect / stretch) : 1;
        (Columns, Rows) = Fit(aspect, Math.Max(8, (int)Math.Round(cells * shrink)));
        (InkColumns, InkRows) = Fit(aspect, Math.Max(8, (int)Math.Round(inkCells * shrink)));
        var count = Columns * Rows;
        _u = new float[count];
        _v = new float[count];
        _uNext = new float[count];
        _vNext = new float[count];
        _pressure = new float[count];
        _pressureNext = new float[count];
        _divergence = new float[count];
        _curl = new float[count];
        var inkCount = InkColumns * InkRows;
        _neutral = new float[inkCount];
        _accent = new float[inkCount];
        _neutralNext = new float[inkCount];
        _accentNext = new float[inkCount];
        _density = new float[inkCount];
        _dither = new float[inkCount];
        for (var j = 0; j < InkRows; j++)
        {
            for (var i = 0; i < InkColumns; i++)
            {
                // A touch of noise in the coverage keeps faint gradients from banding.
                _dither[j * InkColumns + i] = (Hash(i, j) - 0.5f) / 255f;
            }
        }
        _inkFirst = new int[InkRows];
        _inkLast = new int[InkRows];
        _nextFirst = new int[InkRows];
        _nextLast = new int[InkRows];
        ResetInkSpans();
        _columnCell = new int[InkColumns];
        _columnWeight = new float[InkColumns];
        var toVelocity = (float)Columns / InkColumns;
        for (var i = 0; i < InkColumns; i++)
        {
            var x = Math.Clamp((i + 0.5f) * toVelocity - 0.5f, 0, Columns - 1.001f);
            _columnCell[i] = (int)x;
            _columnWeight[i] = x - (int)x;
        }
        _rowU = new float[Columns];
        _rowV = new float[Columns];
    }

    /// <summary>The total ink on the field (both shares), for tests and for knowing when it has faded.</summary>
    public double TotalInk()
    {
        double sum = 0;
        for (var i = 0; i < _neutral.Length; i++)
        {
            sum += _neutral[i] + _accent[i];
        }
        return sum;
    }

    /// <summary>The accent share of the ink, summed over the field.</summary>
    public double TotalAccent()
    {
        double sum = 0;
        for (var i = 0; i < _accent.Length; i++)
        {
            sum += _accent[i];
        }
        return sum;
    }

    /// <summary>The fastest speed on the velocity grid, in cells per second.</summary>
    public double MaxSpeed()
    {
        double max = 0;
        for (var i = 0; i < _u.Length; i++)
        {
            max = Math.Max(max, Math.Abs(_u[i]) + Math.Abs(_v[i]));
        }
        return max;
    }

    /// <summary>Clears velocity, pressure and ink.</summary>
    public void Clear()
    {
        Array.Clear(_u);
        Array.Clear(_v);
        Array.Clear(_pressure);
        Array.Clear(_neutral);
        Array.Clear(_accent);
        ResetInkSpans();
    }

    /// <summary>
    /// Adds a soft blob of force and ink at (<paramref name="x"/>, <paramref name="y"/>) (fractions of the field): the
    /// velocity (<paramref name="forceX"/>, <paramref name="forceY"/>) in cells per second, and the two shares of ink, all
    /// falling off as a Gaussian of <paramref name="radius"/> (a fraction of the short side).
    /// </summary>
    public void Splat(double x, double y, double forceX, double forceY, double neutral, double accent, double radius)
    {
        if (Columns == 0)
        {
            return;
        }
        var shortCells = Math.Min(Columns, Rows);
        AddGaussian(_u, _v, Columns, Rows, x, y, radius * shortCells, (float)forceX, (float)forceY);
        var inkShort = Math.Min(InkColumns, InkRows);
        var (left, right, top, bottom) = AddGaussian(_neutral, _accent, InkColumns, InkRows, x, y, radius * inkShort, (float)neutral, (float)accent);
        for (var j = top; j <= bottom; j++)
        {
            _inkFirst[j] = Math.Min(_inkFirst[j], left);
            _inkLast[j] = Math.Max(_inkLast[j], right);
        }
    }

    /// <summary>Advances the fluid by <paramref name="dt"/> seconds.</summary>
    public void Step(double dt)
    {
        if (Columns == 0)
        {
            return;
        }
        var step = (float)Math.Clamp(dt, 0, 1.0 / 30);
        ConfineVorticity(step);
        Project();
        AdvectVelocity(step);
        AdvectInk(step);
    }

    private static (int Columns, int Rows) Fit(double aspect, int cells) =>
        aspect >= 1 ? ((int)Math.Round(cells * aspect, MidpointRounding.AwayFromZero), cells)
                    : (cells, (int)Math.Round(cells / aspect, MidpointRounding.AwayFromZero));

    private void ResetInkSpans()
    {
        Array.Fill(_inkFirst, InkColumns);
        Array.Fill(_inkLast, -1);
    }

    /// <summary>Adds the blob; returns the rectangle it may have touched.</summary>
    private static (int Left, int Right, int Top, int Bottom) AddGaussian(float[] first, float[] second, int columns, int rows, double x, double y,
                                                                          double sigma, float a, float b)
    {
        sigma = Math.Max(0.5, sigma);
        var cx = x * columns - 0.5;
        var cy = y * rows - 0.5;
        var reach = (int)Math.Ceiling(sigma * 3);
        var left = Math.Max(0, (int)Math.Floor(cx) - reach);
        var right = Math.Min(columns - 1, (int)Math.Ceiling(cx) + reach);
        var top = Math.Max(0, (int)Math.Floor(cy) - reach);
        var bottom = Math.Min(rows - 1, (int)Math.Ceiling(cy) + reach);
        var inverse = 1 / (2 * sigma * sigma);
        for (var j = top; j <= bottom; j++)
        {
            var dy = j - cy;
            var row = j * columns;
            for (var i = left; i <= right; i++)
            {
                var dx = i - cx;
                var weight = (float)Math.Exp(-(dx * dx + dy * dy) * inverse);
                if (weight < 0.001f)
                {
                    continue;
                }
                first[row + i] += a * weight;
                second[row + i] += b * weight;
            }
        }
        return (left, right, top, bottom);
    }

    private void ConfineVorticity(float dt)
    {
        int w = Columns, h = Rows;
        var u = _u;
        var v = _v;
        var curl = _curl;
        for (var j = 0; j < h; j++)
        {
            var up = Math.Max(0, j - 1) * w;
            var down = Math.Min(h - 1, j + 1) * w;
            var row = j * w;
            for (var i = 0; i < w; i++)
            {
                var l = Math.Max(0, i - 1);
                var r = Math.Min(w - 1, i + 1);
                curl[row + i] = 0.5f * (v[row + r] - v[row + l] - (u[down + i] - u[up + i]));
            }
        }
        for (var j = 1; j < h - 1; j++)
        {
            var row = j * w;
            for (var i = 1; i < w - 1; i++)
            {
                var gx = 0.5f * (Math.Abs(curl[row + i + 1]) - Math.Abs(curl[row + i - 1]));
                var gy = 0.5f * (Math.Abs(curl[row + w + i]) - Math.Abs(curl[row - w + i]));
                var length = MathF.Sqrt(gx * gx + gy * gy) + 1e-5f;
                var c = curl[row + i] * CurlStrength * dt / length;
                u[row + i] += gy * c;
                v[row + i] -= gx * c;
            }
        }
    }

    private void Project()
    {
        int w = Columns, h = Rows;
        ComputeDivergence();

        var pressure = _pressure;
        var next = _pressureNext;
        for (var k = 0; k < pressure.Length; k++)
        {
            var carried = pressure[k] * PressureCarry;
            pressure[k] = Math.Abs(carried) < PressureFloor ? 0 : carried;
        }
        for (var iteration = 0; iteration < PressureIterations; iteration++)
        {
            for (var j = 0; j < h; j++)
            {
                JacobiRow(pressure, next, j * w, w, j > 0 ? -w : 0, j < h - 1 ? w : 0);
            }
            (pressure, next) = (next, pressure);
        }
        _pressure = pressure;
        _pressureNext = next;

        for (var j = 0; j < h; j++)
        {
            SubtractGradientRow(pressure, j * w, w, j > 0 ? -w : 0, j < h - 1 ? w : 0);
        }
    }

    private void ComputeDivergence()
    {
        int w = Columns, h = Rows;
        var half = Vector128.Create(0.5f);
        ref var u = ref MemoryMarshal.GetArrayDataReference(_u);
        ref var v = ref MemoryMarshal.GetArrayDataReference(_v);
        ref var divergence = ref MemoryMarshal.GetArrayDataReference(_divergence);
        for (var j = 0; j < h; j++)
        {
            var row = j * w;
            var inner = j > 0 && j < h - 1;
            var i = 0;
            if (inner && w > 1)
            {
                Unsafe.Add(ref divergence, row) = DivergenceAt(ref u, ref v, row, 0, j, w, h);
                i = 1;
                if (Vector128.IsHardwareAccelerated)
                {
                    for (; i + 4 <= w - 1; i += 4)
                    {
                        var center = row + i;
                        var du = Load(ref u, center + 1) - Load(ref u, center - 1);
                        var dv = Load(ref v, center + w) - Load(ref v, center - w);
                        Store(half * (du + dv), ref divergence, center);
                    }
                }
            }
            for (; i < w; i++)
            {
                Unsafe.Add(ref divergence, row + i) = DivergenceAt(ref u, ref v, row, i, j, w, h);
            }
        }
    }

    private static float DivergenceAt(ref float u, ref float v, int row, int i, int j, int w, int h)
    {
        var center = row + i;
        var here = Unsafe.Add(ref u, center);
        var hereV = Unsafe.Add(ref v, center);
        // Walls: the velocity just outside mirrors the velocity at the edge, so nothing flows through.
        var left = i > 0 ? Unsafe.Add(ref u, center - 1) : -here;
        var right = i < w - 1 ? Unsafe.Add(ref u, center + 1) : -here;
        var top = j > 0 ? Unsafe.Add(ref v, center - w) : -hereV;
        var bottom = j < h - 1 ? Unsafe.Add(ref v, center + w) : -hereV;
        return 0.5f * (right - left + bottom - top);
    }

    /// <summary>One Jacobi sweep of a row; the walls repeat the pressure at the edge (up and down are 0 on the outer rows).</summary>
    private void JacobiRow(float[] pressureArray, float[] nextArray, int row, int w, int up, int down)
    {
        ref var pressure = ref MemoryMarshal.GetArrayDataReference(pressureArray);
        ref var next = ref MemoryMarshal.GetArrayDataReference(nextArray);
        ref var divergence = ref MemoryMarshal.GetArrayDataReference(_divergence);
        Unsafe.Add(ref next, row) = JacobiAt(ref pressure, ref divergence, row, 0, w, up, down);
        var i = 1;
        if (Vector128.IsHardwareAccelerated)
        {
            var quarter = Vector128.Create(0.25f);
            for (; i + 4 <= w - 1; i += 4)
            {
                var center = row + i;
                var sum = Load(ref pressure, center - 1) + Load(ref pressure, center + 1) +
                          Load(ref pressure, center + up) + Load(ref pressure, center + down) -
                          Load(ref divergence, center);
                Store(quarter * sum, ref next, center);
            }
        }
        for (; i < w; i++)
        {
            Unsafe.Add(ref next, row + i) = JacobiAt(ref pressure, ref divergence, row, i, w, up, down);
        }
    }

    private static float JacobiAt(ref float pressure, ref float divergence, int row, int i, int w, int up, int down)
    {
        var center = row + i;
        var here = Unsafe.Add(ref pressure, center);
        var left = i > 0 ? Unsafe.Add(ref pressure, center - 1) : here;
        var right = i < w - 1 ? Unsafe.Add(ref pressure, center + 1) : here;
        return 0.25f * (left + right + Unsafe.Add(ref pressure, center + up) + Unsafe.Add(ref pressure, center + down) - Unsafe.Add(ref divergence, center));
    }

    private void SubtractGradientRow(float[] pressureArray, int row, int w, int up, int down)
    {
        ref var pressure = ref MemoryMarshal.GetArrayDataReference(pressureArray);
        ref var u = ref MemoryMarshal.GetArrayDataReference(_u);
        ref var v = ref MemoryMarshal.GetArrayDataReference(_v);
        SubtractGradientAt(ref pressure, ref u, ref v, row, 0, w, up, down);
        var i = 1;
        if (Vector128.IsHardwareAccelerated)
        {
            var half = Vector128.Create(0.5f);
            for (; i + 4 <= w - 1; i += 4)
            {
                var center = row + i;
                Store(Load(ref u, center) - half * (Load(ref pressure, center + 1) - Load(ref pressure, center - 1)), ref u, center);
                Store(Load(ref v, center) - half * (Load(ref pressure, center + down) - Load(ref pressure, center + up)), ref v, center);
            }
        }
        for (; i < w; i++)
        {
            SubtractGradientAt(ref pressure, ref u, ref v, row, i, w, up, down);
        }
    }

    private static void SubtractGradientAt(ref float pressure, ref float u, ref float v, int row, int i, int w, int up, int down)
    {
        var center = row + i;
        var here = Unsafe.Add(ref pressure, center);
        var left = i > 0 ? Unsafe.Add(ref pressure, center - 1) : here;
        var right = i < w - 1 ? Unsafe.Add(ref pressure, center + 1) : here;
        Unsafe.Add(ref u, center) -= 0.5f * (right - left);
        Unsafe.Add(ref v, center) -= 0.5f * (Unsafe.Add(ref pressure, center + down) - Unsafe.Add(ref pressure, center + up));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> Load(ref float start, int index) => Vector128.LoadUnsafe(ref Unsafe.Add(ref start, index));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Store(Vector128<float> value, ref float start, int index) => value.StoreUnsafe(ref Unsafe.Add(ref start, index));

    private void AdvectVelocity(float dt)
    {
        int w = Columns, h = Rows;
        var keep = 1 / (1 + VelocityDissipation * dt);
        for (var j = 0; j < h; j++)
        {
            var row = j * w;
            for (var i = 0; i < w; i++)
            {
                var center = row + i;
                Sample2(_u, _v, w, h, i - dt * _u[center], j - dt * _v[center], out var u, out var v);
                u *= keep;
                v *= keep;
                _uNext[center] = Math.Abs(u) < VelocityFloor ? 0 : u;
                _vNext[center] = Math.Abs(v) < VelocityFloor ? 0 : v;
            }
        }
        (_u, _uNext) = (_uNext, _u);
        (_v, _vNext) = (_vNext, _v);
    }

    private void AdvectInk(float dt)
    {
        int w = InkColumns, h = InkRows;
        var toVelocityX = (float)Columns / w;
        var toVelocityY = (float)Rows / h;
        var keep = 1 / (1 + InkDissipation * dt);
        var fade = Math.Min(1, TintFade * dt);
        // The velocity is in velocity cells per second; one velocity cell spans 1 / toVelocity ink cells.
        var scaleX = dt / toVelocityX;
        var scaleY = dt / toVelocityY;
        var reach = InkReach(scaleX, scaleY);
        var maxX = w - 1.001f;
        var maxY = h - 1.001f;
        ref var neutral = ref MemoryMarshal.GetArrayDataReference(_neutral);
        ref var accent = ref MemoryMarshal.GetArrayDataReference(_accent);
        ref var rowU = ref MemoryMarshal.GetArrayDataReference(_rowU);
        ref var rowV = ref MemoryMarshal.GetArrayDataReference(_rowV);
        ref var columnCell = ref MemoryMarshal.GetArrayDataReference(_columnCell);
        ref var columnWeight = ref MemoryMarshal.GetArrayDataReference(_columnWeight);
        for (var j = 0; j < h; j++)
        {
            var row = j * w;
            var neutralRow = _neutralNext.AsSpan(row, w);
            var accentRow = _accentNext.AsSpan(row, w);

            // A cell can only receive ink from within the reach of where it is, so only those cells are computed.
            var from = w;
            var to = -1;
            for (var source = Math.Max(0, j - reach); source <= Math.Min(h - 1, j + reach); source++)
            {
                from = Math.Min(from, _inkFirst[source]);
                to = Math.Max(to, _inkLast[source]);
            }
            if (from > to)
            {
                neutralRow.Clear();
                accentRow.Clear();
                _nextFirst[j] = w;
                _nextLast[j] = -1;
                continue;
            }
            from = Math.Max(0, from - reach);
            to = Math.Min(w - 1, to + reach);
            neutralRow[..from].Clear();
            accentRow[..from].Clear();
            neutralRow[(to + 1)..].Clear();
            accentRow[(to + 1)..].Clear();

            LoadVelocityRow((j + 0.5f) * toVelocityY - 0.5f);
            ref var neutralNext = ref MemoryMarshal.GetReference(neutralRow);
            ref var accentNext = ref MemoryMarshal.GetReference(accentRow);
            var first = w;
            var last = -1;
            for (var i = from; i <= to; i++)
            {
                var cell = Unsafe.Add(ref columnCell, i);
                var blend = Unsafe.Add(ref columnWeight, i);
                var u = Unsafe.Add(ref rowU, cell) + (Unsafe.Add(ref rowU, cell + 1) - Unsafe.Add(ref rowU, cell)) * blend;
                var v = Unsafe.Add(ref rowV, cell) + (Unsafe.Add(ref rowV, cell + 1) - Unsafe.Add(ref rowV, cell)) * blend;

                var x = i - scaleX * u;
                var y = j - scaleY * v;
                x = x < 0 ? 0 : x > maxX ? maxX : x;
                y = y < 0 ? 0 : y > maxY ? maxY : y;
                var x0 = (int)x;
                var y0 = (int)y;
                var fx = x - x0;
                var fy = y - y0;
                var i00 = y0 * w + x0;
                var i01 = i00 + w;
                var top = 1 - fy;
                var w00 = (1 - fx) * top;
                var w10 = fx * top;
                var w01 = (1 - fx) * fy;
                var w11 = fx * fy;
                var n = (Unsafe.Add(ref neutral, i00) * w00 + Unsafe.Add(ref neutral, i00 + 1) * w10 +
                         Unsafe.Add(ref neutral, i01) * w01 + Unsafe.Add(ref neutral, i01 + 1) * w11) * keep;
                var a = (Unsafe.Add(ref accent, i00) * w00 + Unsafe.Add(ref accent, i00 + 1) * w10 +
                         Unsafe.Add(ref accent, i01) * w01 + Unsafe.Add(ref accent, i01 + 1) * w11) * keep;
                var moved = a * fade;
                n += moved;
                a -= moved;
                if (n < InkFloor)
                {
                    n = 0;
                }
                if (a < InkFloor)
                {
                    a = 0;
                }
                Unsafe.Add(ref neutralNext, i) = n;
                Unsafe.Add(ref accentNext, i) = a;
                if (n + a > 0)
                {
                    first = Math.Min(first, i);
                    last = i;
                }
            }
            _nextFirst[j] = first;
            _nextLast[j] = last;
        }
        (_neutral, _neutralNext) = (_neutralNext, _neutral);
        (_accent, _accentNext) = (_accentNext, _accent);
        (_inkFirst, _nextFirst) = (_nextFirst, _inkFirst);
        (_inkLast, _nextLast) = (_nextLast, _inkLast);
    }

    /// <summary>The most ink cells any ink can travel this step, plus the sampling footprint.</summary>
    private int InkReach(float scaleX, float scaleY)
    {
        float maxU = 0, maxV = 0;
        for (var k = 0; k < _u.Length; k++)
        {
            maxU = Math.Max(maxU, Math.Abs(_u[k]));
            maxV = Math.Max(maxV, Math.Abs(_v[k]));
        }
        return (int)MathF.Ceiling(Math.Max(maxU * scaleX, maxV * scaleY)) + 2;
    }

    /// <summary>Interpolates the velocity field to one row at <paramref name="y"/> (velocity cells).</summary>
    private void LoadVelocityRow(float y)
    {
        int w = Columns, h = Rows;
        y = Math.Clamp(y, 0, h - 1.001f);
        var row = (int)y * w;
        var blend = y - (int)y;
        LerpRow(_u, _rowU, row, w, blend);
        LerpRow(_v, _rowV, row, w, blend);
    }

    private static void LerpRow(float[] field, float[] output, int row, int w, float blend)
    {
        ref var above = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(field), row);
        ref var below = ref Unsafe.Add(ref above, w);
        ref var target = ref MemoryMarshal.GetArrayDataReference(output);
        var i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            var amount = Vector128.Create(blend);
            for (; i + 4 <= w; i += 4)
            {
                var start = Load(ref above, i);
                Store(start + (Load(ref below, i) - start) * amount, ref target, i);
            }
        }
        for (; i < w; i++)
        {
            var start = Unsafe.Add(ref above, i);
            Unsafe.Add(ref target, i) = start + (Unsafe.Add(ref below, i) - start) * blend;
        }
    }

    /// <summary>
    /// Bilinear samples of two fields of the same size at cell coordinates (x, y), clamped to the edges; the two share
    /// the index and weight arithmetic.
    /// </summary>
    private static void Sample2(float[] first, float[] second, int w, int h, float x, float y, out float a, out float b)
    {
        x = Math.Clamp(x, 0, w - 1.001f);
        y = Math.Clamp(y, 0, h - 1.001f);
        var x0 = (int)x;
        var y0 = (int)y;
        var fx = x - x0;
        var fy = y - y0;
        var i00 = y0 * w + x0;
        var i10 = i00 + (x0 < w - 1 ? 1 : 0);
        var i01 = i00 + (y0 < h - 1 ? w : 0);
        var i11 = i01 + (i10 - i00);
        var w00 = (1 - fx) * (1 - fy);
        var w10 = fx * (1 - fy);
        var w01 = (1 - fx) * fy;
        var w11 = fx * fy;
        a = first[i00] * w00 + first[i10] * w10 + first[i01] * w01 + first[i11] * w11;
        b = second[i00] * w00 + second[i10] * w10 + second[i01] * w01 + second[i11] * w11;
    }

    /// <summary>
    /// Writes the ink as premultiplied BGRA pixels (<see cref="InkColumns"/> × <see cref="InkRows"/>, row by row):
    /// neutral ink in <paramref name="ink"/>, accent ink in <paramref name="accent"/>, each covering more the denser it
    /// is up to <paramref name="strength"/>, with a soft light from the ink's slope so wisps look rounded.
    /// </summary>
    public void Compose(int[] pixels, Color ink, Color accent, double strength)
    {
        int w = InkColumns, h = InkRows;
        if (pixels.Length < w * h)
        {
            return;
        }
        var look = new ComposeLook(ink, accent, (float)Math.Clamp(strength, 0, 1));
        ComputeDensity();
        for (var j = 0; j < h; j++)
        {
            var row = j * w;
            var from = _inkFirst[j];
            var to = _inkLast[j];
            var line = pixels.AsSpan(row, w);
            if (from > to)
            {
                line.Clear();
                continue;
            }
            line[..from].Clear();
            line[(to + 1)..].Clear();
            ComposeRow(pixels, look, row, j, from, to);
        }
    }

    private void ComputeDensity()
    {
        var count = _density.Length;
        ref var neutral = ref MemoryMarshal.GetArrayDataReference(_neutral);
        ref var accent = ref MemoryMarshal.GetArrayDataReference(_accent);
        ref var density = ref MemoryMarshal.GetArrayDataReference(_density);
        var k = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            for (; k + 4 <= count; k += 4)
            {
                Store(Load(ref neutral, k) + Load(ref accent, k), ref density, k);
            }
        }
        for (; k < count; k++)
        {
            Unsafe.Add(ref density, k) = Unsafe.Add(ref neutral, k) + Unsafe.Add(ref accent, k);
        }
    }

    /// <summary>Composes the cells <paramref name="from"/> to <paramref name="to"/> of one ink row.</summary>
    private void ComposeRow(int[] pixels, in ComposeLook look, int row, int j, int from, int to)
    {
        var w = InkColumns;
        var up = j > 0 ? -w : 0;
        var down = j < InkRows - 1 ? w : 0;
        var i = from;
        if (i == 0)
        {
            pixels[row] = ComposeCell(look, row, 0, up, down);
            i = 1;
        }
        if (Vector128.IsHardwareAccelerated)
        {
            ref var density = ref MemoryMarshal.GetArrayDataReference(_density);
            ref var neutral = ref MemoryMarshal.GetArrayDataReference(_neutral);
            ref var accent = ref MemoryMarshal.GetArrayDataReference(_accent);
            ref var dither = ref MemoryMarshal.GetArrayDataReference(_dither);
            ref var output = ref MemoryMarshal.GetArrayDataReference(pixels);
            var last = Math.Min(to, w - 2);
            var empty = Vector128.Create(EmptyDensity);
            for (; i + 3 <= last; i += 4)
            {
                var center = row + i;
                var here = Load(ref density, center);
                var skip = Vector128.LessThan(here, empty);
                if (Vector128.EqualsAll(skip.AsInt32(), Vector128<int>.AllBitsSet))
                {
                    Vector128<int>.Zero.StoreUnsafe(ref Unsafe.Add(ref output, center));
                    continue;
                }
                // The coverage table has no gather on Vector128, so it alone is read cell by cell.
                ref var n = ref Unsafe.Add(ref neutral, center);
                ref var a = ref Unsafe.Add(ref accent, center);
                var neutralCover = Vector128.Create(Cover(n), Cover(Unsafe.Add(ref n, 1)), Cover(Unsafe.Add(ref n, 2)), Cover(Unsafe.Add(ref n, 3))) * look.Cap;
                var accentCover = Vector128.Min(look.One,
                    Vector128.Create(Cover(a), Cover(Unsafe.Add(ref a, 1)), Cover(Unsafe.Add(ref a, 2)), Cover(Unsafe.Add(ref a, 3))) * look.AccentCap);
                var alpha = neutralCover + accentCover * (look.One - neutralCover);
                var share = accentCover / Vector128.Max(neutralCover + accentCover, look.MinCover);

                var slopeX = Load(ref density, center + 1) - Load(ref density, center - 1);
                var slopeY = Load(ref density, center + down) - Load(ref density, center + up);
                var light = (look.LightX * slopeX + look.LightY * slopeY) * look.LightGain + look.LightBase;
                light = Vector128.Min(look.One, Vector128.Max(Vector128<float>.Zero, light));
                var shade = look.ShadeBase + look.ShadeGain * light;

                alpha = Vector128.Min(look.One, Vector128.Max(Vector128<float>.Zero, alpha + Load(ref dither, center)));
                var a8 = Vector128.ConvertToInt32(alpha * look.Scale + look.Half);
                var colorScale = shade * alpha;
                var r8 = Vector128.Min(a8, Vector128.ConvertToInt32((look.InkR + look.DeltaR * share) * colorScale + look.Half));
                var g8 = Vector128.Min(a8, Vector128.ConvertToInt32((look.InkG + look.DeltaG * share) * colorScale + look.Half));
                var b8 = Vector128.Min(a8, Vector128.ConvertToInt32((look.InkB + look.DeltaB * share) * colorScale + look.Half));
                var packed = Vector128.ShiftLeft(a8, 24) | Vector128.ShiftLeft(r8, 16) | Vector128.ShiftLeft(g8, 8) | b8;
                Vector128.AndNot(packed, skip.AsInt32()).StoreUnsafe(ref Unsafe.Add(ref output, center));
            }
        }
        for (; i <= to; i++)
        {
            pixels[row + i] = ComposeCell(look, row, i, up, down);
        }
    }

    private int ComposeCell(in ComposeLook look, int row, int i, int up, int down)
    {
        var center = row + i;
        var here = _density[center];
        if (here < EmptyDensity)
        {
            return 0;
        }
        var neutralCover = Cover(_neutral[center]) * look.CapValue;
        // Accent ink reads a little stronger than neutral ink, so fresh strokes carry colour before they settle.
        var accentCover = Math.Min(1, Cover(_accent[center]) * look.CapValue * AccentBoost);
        var alpha = neutralCover + accentCover * (1 - neutralCover);
        var share = accentCover / Math.Max(neutralCover + accentCover, 1e-4f);

        // Light from the upper left on the ink's slope: dense ridges catch it, their far sides fall into shade.
        var left = i > 0 ? _density[center - 1] : here;
        var right = i < InkColumns - 1 ? _density[center + 1] : here;
        var slopeX = right - left;
        var slopeY = _density[center + down] - _density[center + up];
        var light = (-0.45f * slopeX - 0.55f * slopeY) * 2.2f + 0.6f;
        var shade = 0.84f + 0.28f * Math.Clamp(light, 0, 1);

        alpha = Math.Clamp(alpha + _dither[center], 0, 1);
        var colorScale = shade * alpha;
        var a8 = (int)(alpha * 255 + 0.5f);
        var r8 = Math.Min(a8, (int)((look.InkRValue + look.DeltaRValue * share) * colorScale + 0.5f));
        var g8 = Math.Min(a8, (int)((look.InkGValue + look.DeltaGValue * share) * colorScale + 0.5f));
        var b8 = Math.Min(a8, (int)((look.InkBValue + look.DeltaBValue * share) * colorScale + 0.5f));
        return (a8 << 24) | (r8 << 16) | (g8 << 8) | b8;
    }

    /// <summary>The picture's constants, scalar and broadcast to all lanes.</summary>
    private readonly struct ComposeLook
    {
        public readonly float CapValue, InkRValue, InkGValue, InkBValue, DeltaRValue, DeltaGValue, DeltaBValue;
        public readonly Vector128<float> Cap, AccentCap, InkR, InkG, InkB, DeltaR, DeltaG, DeltaB;
        public readonly Vector128<float> One = Vector128.Create(1f);
        public readonly Vector128<float> Half = Vector128.Create(0.5f);
        public readonly Vector128<float> Scale = Vector128.Create(255f);
        public readonly Vector128<float> MinCover = Vector128.Create(1e-4f);
        public readonly Vector128<float> LightX = Vector128.Create(-0.45f);
        public readonly Vector128<float> LightY = Vector128.Create(-0.55f);
        public readonly Vector128<float> LightGain = Vector128.Create(2.2f);
        public readonly Vector128<float> LightBase = Vector128.Create(0.6f);
        public readonly Vector128<float> ShadeBase = Vector128.Create(0.84f);
        public readonly Vector128<float> ShadeGain = Vector128.Create(0.28f);

        public ComposeLook(Color ink, Color accent, float cap)
        {
            CapValue = cap;
            InkRValue = ink.R;
            InkGValue = ink.G;
            InkBValue = ink.B;
            DeltaRValue = accent.R - InkRValue;
            DeltaGValue = accent.G - InkGValue;
            DeltaBValue = accent.B - InkBValue;
            Cap = Vector128.Create(cap);
            AccentCap = Vector128.Create(cap * AccentBoost);
            InkR = Vector128.Create(InkRValue);
            InkG = Vector128.Create(InkGValue);
            InkB = Vector128.Create(InkBValue);
            DeltaR = Vector128.Create(DeltaRValue);
            DeltaG = Vector128.Create(DeltaGValue);
            DeltaB = Vector128.Create(DeltaBValue);
        }
    }

    /// <summary>How much of the background a density covers: 1 − e^(−1.4·density), from a lookup table.</summary>
    private static float Cover(float density)
    {
        if (density <= 0)
        {
            return 0;
        }
        var position = density * 128;
        if (position >= Absorb.Length - 1)
        {
            return Absorb[^1];
        }
        var index = (int)position;
        return Absorb[index] + (Absorb[index + 1] - Absorb[index]) * (position - index);
    }

    private static float[] BuildAbsorb()
    {
        var table = new float[1025];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = (float)(1 - Math.Exp(-1.4 * i / 128.0));
        }
        return table;
    }

    private static float Hash(int x, int y)
    {
        var n = (uint)(x * 374761393 + y * 668265263);
        n = (n ^ (n >> 13)) * 1274126177;
        return (n ^ (n >> 16)) / (float)uint.MaxValue;
    }
}
