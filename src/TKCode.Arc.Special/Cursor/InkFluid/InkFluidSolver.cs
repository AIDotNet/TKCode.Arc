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

    /// <summary>Velocity grid columns and rows.</summary>
    public int Columns { get; private set; }

    public int Rows { get; private set; }

    /// <summary>Ink grid columns and rows (the size of the composed picture).</summary>
    public int InkColumns { get; private set; }

    public int InkRows { get; private set; }

    /// <summary>
    /// Sizes the grids for a field of <paramref name="aspect"/> (width over height): the velocity grid has
    /// <paramref name="cells"/> on its short side, the ink grid <paramref name="inkCells"/>. Clears the fluid.
    /// </summary>
    public void Resize(double aspect, int cells, int inkCells)
    {
        aspect = double.IsFinite(aspect) && aspect > 0 ? Math.Clamp(aspect, 0.1, 10) : 1;
        (Columns, Rows) = Fit(aspect, Math.Max(8, cells));
        (InkColumns, InkRows) = Fit(aspect, Math.Max(8, inkCells));
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
        AddGaussian(_neutral, _accent, InkColumns, InkRows, x, y, radius * inkShort, (float)neutral, (float)accent);
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

    private static void AddGaussian(float[] first, float[] second, int columns, int rows, double x, double y, double sigma,
                                    float a, float b)
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
        var u = _u;
        var v = _v;
        var divergence = _divergence;
        for (var j = 0; j < h; j++)
        {
            var row = j * w;
            for (var i = 0; i < w; i++)
            {
                var center = row + i;
                // Walls: the velocity just outside mirrors the velocity at the edge, so nothing flows through.
                var left = i > 0 ? u[center - 1] : -u[center];
                var right = i < w - 1 ? u[center + 1] : -u[center];
                var top = j > 0 ? v[center - w] : -v[center];
                var bottom = j < h - 1 ? v[center + w] : -v[center];
                divergence[center] = 0.5f * (right - left + bottom - top);
            }
        }

        var pressure = _pressure;
        var next = _pressureNext;
        for (var k = 0; k < pressure.Length; k++)
        {
            pressure[k] *= PressureCarry;
        }
        for (var iteration = 0; iteration < PressureIterations; iteration++)
        {
            for (var j = 0; j < h; j++)
            {
                var row = j * w;
                var up = j > 0 ? -w : 0;
                var down = j < h - 1 ? w : 0;
                for (var i = 0; i < w; i++)
                {
                    var center = row + i;
                    var left = i > 0 ? pressure[center - 1] : pressure[center];
                    var right = i < w - 1 ? pressure[center + 1] : pressure[center];
                    next[center] = 0.25f * (left + right + pressure[center + up] + pressure[center + down] - divergence[center]);
                }
            }
            (pressure, next) = (next, pressure);
        }
        _pressure = pressure;
        _pressureNext = next;

        for (var j = 0; j < h; j++)
        {
            var row = j * w;
            var up = j > 0 ? -w : 0;
            var down = j < h - 1 ? w : 0;
            for (var i = 0; i < w; i++)
            {
                var center = row + i;
                var left = i > 0 ? pressure[center - 1] : pressure[center];
                var right = i < w - 1 ? pressure[center + 1] : pressure[center];
                u[center] -= 0.5f * (right - left);
                v[center] -= 0.5f * (pressure[center + down] - pressure[center + up]);
            }
        }
    }

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
                _uNext[center] = u * keep;
                _vNext[center] = v * keep;
            }
        }
        (_u, _uNext) = (_uNext, _u);
        (_v, _vNext) = (_vNext, _v);
    }

    private void AdvectInk(float dt)
    {
        int w = InkColumns, h = InkRows;
        float toVelocityX = (float)Columns / w;
        float toVelocityY = (float)Rows / h;
        var keep = 1 / (1 + InkDissipation * dt);
        var fade = Math.Min(1, TintFade * dt);
        for (var j = 0; j < h; j++)
        {
            var row = j * w;
            var vy = (j + 0.5f) * toVelocityY - 0.5f;
            for (var i = 0; i < w; i++)
            {
                var vx = (i + 0.5f) * toVelocityX - 0.5f;
                // The velocity is in velocity cells per second; one velocity cell spans 1 / toVelocity ink cells.
                Sample2(_u, _v, Columns, Rows, vx, vy, out var u, out var v);
                Sample2(_neutral, _accent, w, h, i - dt * u / toVelocityX, j - dt * v / toVelocityY, out var neutral, out var accent);
                neutral *= keep;
                accent *= keep;
                var moved = accent * fade;
                _neutralNext[row + i] = neutral + moved;
                _accentNext[row + i] = accent - moved;
            }
        }
        (_neutral, _neutralNext) = (_neutralNext, _neutral);
        (_accent, _accentNext) = (_accentNext, _accent);
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
        var cap = (float)Math.Clamp(strength, 0, 1);
        float inkR = ink.R, inkG = ink.G, inkB = ink.B;
        float accR = accent.R, accG = accent.G, accB = accent.B;
        for (var j = 0; j < h; j++)
        {
            var row = j * w;
            var up = j > 0 ? -w : 0;
            var down = j < h - 1 ? w : 0;
            for (var i = 0; i < w; i++)
            {
                var center = row + i;
                var neutral = _neutral[center];
                var tinted = _accent[center];
                if (neutral + tinted < 0.0004f)
                {
                    pixels[center] = 0;
                    continue;
                }
                var neutralCover = Cover(neutral) * cap;
                // Accent ink reads a little stronger than neutral ink, so fresh strokes carry colour before they settle.
                var accentCover = Math.Min(1, Cover(tinted) * cap * 1.7f);
                var alpha = neutralCover + accentCover * (1 - neutralCover);
                var share = accentCover / Math.Max(neutralCover + accentCover, 1e-4f);
                var r = inkR + (accR - inkR) * share;
                var g = inkG + (accG - inkG) * share;
                var b = inkB + (accB - inkB) * share;

                // Light from the upper left on the ink's slope: dense ridges catch it, their far sides fall into shade.
                var left = i > 0 ? Density(center - 1) : Density(center);
                var right = i < w - 1 ? Density(center + 1) : Density(center);
                var slopeX = right - left;
                var slopeY = Density(center + down) - Density(center + up);
                var light = (-0.45f * slopeX - 0.55f * slopeY) * 2.2f + 0.6f;
                var shade = 0.84f + 0.28f * Math.Clamp(light, 0, 1);
                r *= shade;
                g *= shade;
                b *= shade;

                // A touch of noise in the coverage keeps faint gradients from banding.
                alpha = Math.Clamp(alpha + (Hash(i, j) - 0.5f) / 255f, 0, 1);
                var a8 = (int)(alpha * 255 + 0.5f);
                var r8 = Math.Min(a8, (int)(r * alpha + 0.5f));
                var g8 = Math.Min(a8, (int)(g * alpha + 0.5f));
                var b8 = Math.Min(a8, (int)(b * alpha + 0.5f));
                pixels[center] = (a8 << 24) | (r8 << 16) | (g8 << 8) | b8;
            }
        }
    }

    private float Density(int index) => _neutral[index] + _accent[index];

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
