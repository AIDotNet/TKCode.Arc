using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>
/// The three light layers over one face of a <see cref="HoloCard"/>, drawn over the face's content and blended into it:
/// a rainbow foil (color dodge), a sprinkle of glitter (overlay, front only) and a glare (overlay), then the 1 px edge
/// light. Each layer is baked into a reused <see cref="WriteableBitmap"/> whenever the light changes and drawn with a
/// Skia blend mode; Avalonia applies <see cref="RenderOptions.BitmapBlendingMode"/> to bitmaps only (fills and visuals
/// ignore it), which is why the gradients are baked rather than filled. The foil and the glare are smooth, so they are
/// baked at a quarter of the face's size and scaled up; the glitter is baked at device resolution so its dots stay crisp.
/// </summary>
/// <remarks>
/// Recipes (Arc Holo card, design reference; notes section 3), all scaled by the light strength and the card's
/// intensity: the foil is a repeating 118° gradient (pink, peach, mint, sky, lilac; period 35 %) on a box 260 % of the
/// face, positioned at the foil position, hue-turned, masked by a radial falloff around the light (1 to 10 %, 0.35 from
/// 70 %), opacity 0.24; the glitter is our own procedurally scattered tile of white dots (120 px, 20 dots in three
/// sizes), drifting against the pointer, masked to a disc around the light (0 at 55 %), opacity 0.55; the glare is a
/// radial white-to-black falloff around the light (white 36 % → white 10 % at 32 % → black 14 %), opacity 1.
/// </remarks>
internal sealed class HoloCardSheen : Control
{
    private const int FoilDownsample = 4;
    private const int GlareDownsample = 2;
    private const double GlitterTile = 120;
    private const double GlitterOverscan = 24;
    private const double FoilAngleDegrees = 118;
    private const double FoilPeriod = 0.35;
    private const double FoilBoxScale = 2.6;
    private const int LutSize = 256;

    private static readonly RenderOptions s_colorDodge = new() { BitmapBlendingMode = BitmapBlendingMode.ColorDodge, BitmapInterpolationMode = BitmapInterpolationMode.MediumQuality };
    private static readonly RenderOptions s_overlay = new() { BitmapBlendingMode = BitmapBlendingMode.Overlay, BitmapInterpolationMode = BitmapInterpolationMode.MediumQuality };
    private static readonly RenderOptions s_overlaySharp = new() { BitmapBlendingMode = BitmapBlendingMode.Overlay, BitmapInterpolationMode = BitmapInterpolationMode.LowQuality };

    /// <summary>The foil's colour stops over one period (position in the period, sRGB 0..1).</summary>
    private static readonly (double At, double R, double G, double B)[] s_foilStops = BuildFoilStops();

    /// <summary>The glitter tile: 20 dots (x, y in the 120 px tile, radius in px), scattered from a fixed seed.</summary>
    internal static readonly (double X, double Y, double Radius)[] GlitterDots = BuildGlitterDots();

    /// <summary>The 1 px light along the inside of the face's edge.</summary>
    public static readonly StyledProperty<IBrush?> EdgeLightProperty =
        AvaloniaProperty.Register<HoloCardSheen, IBrush?>(nameof(EdgeLight));

    private readonly float[] _lut = new float[LutSize * 3];
    private WriteableBitmap? _foil;
    private WriteableBitmap? _glare;
    private WriteableBitmap? _glitter;
    private int[] _row = [];
    private int[] _written = new int[4096];
    private int _writtenCount;
    private double _scaling = 1;
    private bool _dirty = true;
    private Pen? _edgePen;

    // Baked state (what the bitmaps show).
    private Vector _bakedLight = new(double.NaN, double.NaN);
    private double _bakedShine = double.NaN;
    private double _bakedFlip = double.NaN;
    private double _bakedIntensity = double.NaN;
    private bool _bakedGlitter;

    public HoloCardSheen()
    {
        IsHitTestVisible = false;
    }

    /// <summary>The light position on the face, 0..1 on each axis.</summary>
    public Vector Light { get; private set; } = new(0.5, 0.5);

    /// <summary>The light strength, 0..1 (0.35 at rest, 1 hovered).</summary>
    public double Shine { get; private set; } = HoloCardMath.RestShine;

    /// <summary>The flip angle in degrees (drives the mid-spin sweep).</summary>
    public double FlipAngle { get; private set; }

    /// <summary>The card's intensity, 0..1.</summary>
    public double Intensity { get; private set; } = 1;

    /// <summary>Whether this face shows glitter (the front, at full motion).</summary>
    public bool ShowsGlitter { get; private set; }

    /// <summary>The corner radius of the face (for the edge light).</summary>
    public double CornerRadius { get; set; } = 22;

    /// <summary>Whether the layers changed since they were last baked (tests).</summary>
    internal bool IsDirty => _dirty;

    /// <summary>The edge light's colour.</summary>
    public IBrush? EdgeLight
    {
        get => GetValue(EdgeLightProperty);
        set => SetValue(EdgeLightProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EdgeLightProperty)
        {
            _edgePen = EdgeLight is { } brush ? new Pen(brush, 1) : null;
            InvalidateVisual();
        }
    }

    /// <summary>Sets what the layers show; rebakes on the next render when anything moved.</summary>
    public void Update(Vector light, double shine, double flipAngle, double intensity, bool glitter)
    {
        if (light == Light && shine == Shine && flipAngle == FlipAngle && intensity == Intensity && glitter == ShowsGlitter)
        {
            return;
        }
        Light = light;
        Shine = shine;
        FlipAngle = flipAngle;
        Intensity = intensity;
        ShowsGlitter = glitter;
        _dirty = true;
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        if (scaling != _scaling)
        {
            _scaling = scaling;
            DisposeBitmaps();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DisposeBitmaps();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        DisposeBitmaps();
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width < 1 || size.Height < 1)
        {
            return;
        }
        var strength = Shine * Intensity;
        var rect = new Rect(size);
        if (strength > 0.001)
        {
            EnsureBitmaps(size);
            if (_dirty || _bakedLight != Light || _bakedShine != Shine || _bakedFlip != FlipAngle ||
                _bakedIntensity != Intensity || _bakedGlitter != ShowsGlitter)
            {
                Bake(size);
            }
            using (context.PushRenderOptions(s_colorDodge))
            {
                context.DrawImage(_foil!, new Rect(0, 0, _foil!.PixelSize.Width, _foil.PixelSize.Height), rect);
            }
            if (ShowsGlitter)
            {
                using (context.PushRenderOptions(s_overlaySharp))
                {
                    context.DrawImage(_glitter!, new Rect(0, 0, _glitter!.PixelSize.Width, _glitter.PixelSize.Height), rect);
                }
            }
            using (context.PushRenderOptions(s_overlay))
            {
                context.DrawImage(_glare!, new Rect(0, 0, _glare!.PixelSize.Width, _glare.PixelSize.Height), rect);
            }
        }
        if (_edgePen is not null)
        {
            var radius = Math.Max(0, CornerRadius - 0.5);
            context.DrawRectangle(null, _edgePen, new RoundedRect(rect.Deflate(0.5), radius));
        }
    }

    private void EnsureBitmaps(Size size)
    {
        if (_foil is not null && _glare is not null && _glitter is not null)
        {
            return;
        }
        DisposeBitmaps();
        var lowW = Math.Max(2, (int)Math.Ceiling(size.Width / FoilDownsample));
        var lowH = Math.Max(2, (int)Math.Ceiling(size.Height / FoilDownsample));
        var dpi96 = new Vector(96, 96);
        _foil = new WriteableBitmap(new PixelSize(lowW, lowH), dpi96, PixelFormat.Bgra8888, AlphaFormat.Premul);
        var glareW = Math.Max(2, (int)Math.Ceiling(size.Width / GlareDownsample));
        var glareH = Math.Max(2, (int)Math.Ceiling(size.Height / GlareDownsample));
        _glare = new WriteableBitmap(new PixelSize(glareW, glareH), dpi96, PixelFormat.Bgra8888, AlphaFormat.Premul);
        var hiW = Math.Max(1, (int)Math.Ceiling(size.Width * _scaling));
        var hiH = Math.Max(1, (int)Math.Ceiling(size.Height * _scaling));
        _glitter = new WriteableBitmap(new PixelSize(hiW, hiH), dpi96 * _scaling, PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var fb = _glitter.Lock())
        {
            var zero = new int[hiW];
            for (var y = 0; y < hiH; y++)
            {
                Marshal.Copy(zero, 0, fb.Address + y * fb.RowBytes, hiW);
            }
        }
        _writtenCount = 0;
        if (_row.Length < Math.Max(lowW, glareW))
        {
            _row = new int[Math.Max(lowW, glareW)];
        }
        _dirty = true;
    }

    private void DisposeBitmaps()
    {
        _foil?.Dispose();
        _glare?.Dispose();
        _glitter?.Dispose();
        _foil = null;
        _glare = null;
        _glitter = null;
        _writtenCount = 0;
        _dirty = true;
    }

    private void Bake(Size size)
    {
        var strength = Math.Clamp(Shine * Intensity, 0, 1);
        BakeFoil(size, strength);
        BakeGlare(size, strength);
        if (ShowsGlitter)
        {
            BakeGlitter(size, strength);
        }
        _bakedLight = Light;
        _bakedShine = Shine;
        _bakedFlip = FlipAngle;
        _bakedIntensity = Intensity;
        _bakedGlitter = ShowsGlitter;
        _dirty = false;
    }

    private void BakeFoil(Size size, double strength)
    {
        BuildLut(HoloCardMath.Hue(Light, FlipAngle));
        var position = HoloCardMath.FoilPosition(Light, FlipAngle);
        double w = size.Width, h = size.Height;
        double boxW = w * FoilBoxScale, boxH = h * FoilBoxScale;
        // background-position p% puts the box at (container − box)·p.
        var offX = (w - boxW) * position.X;
        var offY = (h - boxH) * position.Y;
        var angle = HoloCardProjection.Radians(FoilAngleDegrees);
        double dirX = Math.Sin(angle), dirY = -Math.Cos(angle);
        var length = Math.Abs(boxW * dirX) + Math.Abs(boxH * dirY);
        // Mask: radial-gradient(circle at light, 1 at 10 %, 0.35 at 70 %), farthest corner.
        double lx = Light.X * w, ly = Light.Y * h;
        var radius = FarthestCorner(lx, ly, w, h);
        var alpha = HoloCardMath.FoilOpacity * strength;
        var bitmap = _foil!;
        int pw = bitmap.PixelSize.Width, ph = bitmap.PixelSize.Height;
        double sx = w / pw, sy = h / ph;
        using var fb = bitmap.Lock();
        for (var py = 0; py < ph; py++)
        {
            var y = (py + 0.5) * sy;
            for (var px = 0; px < pw; px++)
            {
                var x = (px + 0.5) * sx;
                var t = ((x - offX - boxW / 2) * dirX + (y - offY - boxH / 2) * dirY) / length + 0.5;
                var phase = t / FoilPeriod;
                phase -= Math.Floor(phase);
                var index = Math.Min(LutSize - 1, (int)(phase * LutSize)) * 3;
                var distance = Math.Sqrt((x - lx) * (x - lx) + (y - ly) * (y - ly)) / radius;
                var mask = distance <= 0.1 ? 1 : distance >= 0.7 ? 0.35 : 1 - (distance - 0.1) / 0.6 * 0.65;
                var a = alpha * mask;
                _row[px] = PackPremultiplied(_lut[index] * a, _lut[index + 1] * a, _lut[index + 2] * a, a, Dither(px, py));
            }
            Marshal.Copy(_row, 0, fb.Address + py * fb.RowBytes, pw);
        }
    }

    private void BakeGlare(Size size, double strength)
    {
        double w = size.Width, h = size.Height;
        double lx = Light.X * w, ly = Light.Y * h;
        var radius = FarthestCorner(lx, ly, w, h);
        var bitmap = _glare!;
        int pw = bitmap.PixelSize.Width, ph = bitmap.PixelSize.Height;
        double sx = w / pw, sy = h / ph;
        using var fb = bitmap.Lock();
        for (var py = 0; py < ph; py++)
        {
            var y = (py + 0.5) * sy;
            for (var px = 0; px < pw; px++)
            {
                var x = (px + 0.5) * sx;
                var r = Math.Sqrt((x - lx) * (x - lx) + (y - ly) * (y - ly)) / radius;
                // Premultiplied stops: white .36 at 0, white .10 at 32 %, black .14 at 100 %.
                double c, a;
                if (r <= 0.32)
                {
                    var f = r / 0.32;
                    c = 0.36 + (0.10 - 0.36) * f;
                    a = c;
                }
                else
                {
                    var f = Math.Min(1, (r - 0.32) / 0.68);
                    c = 0.10 * (1 - f);
                    a = 0.10 + (0.14 - 0.10) * f;
                }
                _row[px] = PackPremultiplied(c * strength, c * strength, c * strength, a * strength, Dither(px, py));
            }
            Marshal.Copy(_row, 0, fb.Address + py * fb.RowBytes, pw);
        }
    }

    private void BakeGlitter(Size size, double strength)
    {
        var bitmap = _glitter!;
        int pw = bitmap.PixelSize.Width, ph = bitmap.PixelSize.Height;
        var scale = _scaling;
        using var fb = bitmap.Lock();
        var address = fb.Address;
        var stride = fb.RowBytes;
        // Clear last bake's dots.
        for (var i = 0; i < _writtenCount; i++)
        {
            Marshal.WriteInt32(address, _written[i], 0);
        }
        _writtenCount = 0;
        // The glitter box is the face grown by 24 on every side and moved against the light; its mask is a disc
        // around the light, measured in that box (0 at 55 % of the distance to its farthest corner).
        double boxW = size.Width + 2 * GlitterOverscan, boxH = size.Height + 2 * GlitterOverscan;
        var drift = HoloCardMath.GlitterDrift(Light);
        double originX = -GlitterOverscan + drift.X, originY = -GlitterOverscan + drift.Y;
        double cx = originX + Light.X * boxW, cy = originY + Light.Y * boxH;
        var reach = 0.55 * FarthestCorner(Light.X * boxW, Light.Y * boxH, boxW, boxH);
        var alpha = HoloCardMath.GlitterOpacity * strength;
        var tilesX = (int)Math.Ceiling(boxW / GlitterTile);
        var tilesY = (int)Math.Ceiling(boxH / GlitterTile);
        for (var ty = 0; ty < tilesY; ty++)
        {
            for (var tx = 0; tx < tilesX; tx++)
            {
                foreach (var dot in GlitterDots)
                {
                    var x = originX + tx * GlitterTile + dot.X;
                    var y = originY + ty * GlitterTile + dot.Y;
                    if (x < -2 || y < -2 || x > size.Width + 2 || y > size.Height + 2)
                    {
                        continue;
                    }
                    var distance = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    var mask = 1 - distance / reach;
                    if (mask <= 0)
                    {
                        continue;
                    }
                    SplatDot(address, stride, pw, ph, x * scale, y * scale, dot.Radius * scale, alpha * mask);
                }
            }
        }
    }

    private void SplatDot(nint address, int stride, int pw, int ph, double x, double y, double radius, double alpha)
    {
        // Anti-aliased disc: coverage = radius + 0.5 − distance to the pixel centre, clamped; tiny dots spread their
        // area over the neighbouring pixels instead.
        var tiny = radius < 0.7;
        var area = Math.PI * radius * radius;
        var reach = (int)Math.Ceiling(radius + 1);
        var x0 = (int)Math.Floor(x) - reach;
        var y0 = (int)Math.Floor(y) - reach;
        for (var py = y0; py <= y0 + 2 * reach + 1; py++)
        {
            if (py < 0 || py >= ph)
            {
                continue;
            }
            for (var px = x0; px <= x0 + 2 * reach + 1; px++)
            {
                if (px < 0 || px >= pw)
                {
                    continue;
                }
                double dx = px + 0.5 - x, dy = py + 0.5 - y;
                double coverage;
                if (tiny)
                {
                    var wx = Math.Max(0, 1 - Math.Abs(dx));
                    var wy = Math.Max(0, 1 - Math.Abs(dy));
                    coverage = Math.Min(1, area * wx * wy * 1.6);
                }
                else
                {
                    coverage = Math.Clamp(radius + 0.5 - Math.Sqrt(dx * dx + dy * dy), 0, 1);
                }
                if (coverage <= 0.002)
                {
                    continue;
                }
                var offset = py * stride + px * 4;
                var previous = Marshal.ReadInt32(address, offset);
                var previousAlpha = ((previous >> 24) & 0xFF) / 255.0;
                var a = Math.Min(1, previousAlpha + alpha * coverage * (1 - previousAlpha));
                Marshal.WriteInt32(address, offset, PackPremultiplied(a, a, a, a));
                if (previous == 0)
                {
                    if (_writtenCount == _written.Length)
                    {
                        Array.Resize(ref _written, _written.Length * 2);
                    }
                    _written[_writtenCount++] = offset;
                }
            }
        }
    }

    private void BuildLut(double hueDegrees)
    {
        var angle = HoloCardProjection.Radians(hueDegrees);
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        // CSS hue-rotate() matrix.
        double a00 = 0.213 + cos * 0.787 - sin * 0.213, a01 = 0.715 - cos * 0.715 - sin * 0.715, a02 = 0.072 - cos * 0.072 + sin * 0.928;
        double a10 = 0.213 - cos * 0.213 + sin * 0.143, a11 = 0.715 + cos * 0.285 + sin * 0.140, a12 = 0.072 - cos * 0.072 - sin * 0.283;
        double a20 = 0.213 - cos * 0.213 - sin * 0.787, a21 = 0.715 - cos * 0.715 + sin * 0.715, a22 = 0.072 + cos * 0.928 + sin * 0.072;
        var stop = 0;
        for (var i = 0; i < LutSize; i++)
        {
            var t = (i + 0.5) / LutSize;
            while (stop < s_foilStops.Length - 2 && t > s_foilStops[stop + 1].At)
            {
                stop++;
            }
            var (at0, r0, g0, b0) = s_foilStops[stop];
            var (at1, r1, g1, b1) = s_foilStops[stop + 1];
            var f = Math.Clamp((t - at0) / (at1 - at0), 0, 1);
            var r = r0 + (r1 - r0) * f;
            var g = g0 + (g1 - g0) * f;
            var b = b0 + (b1 - b0) * f;
            _lut[i * 3] = (float)Math.Clamp(a00 * r + a01 * g + a02 * b, 0, 1);
            _lut[i * 3 + 1] = (float)Math.Clamp(a10 * r + a11 * g + a12 * b, 0, 1);
            _lut[i * 3 + 2] = (float)Math.Clamp(a20 * r + a21 * g + a22 * b, 0, 1);
        }
    }

    private static double FarthestCorner(double x, double y, double w, double h)
    {
        var dx = Math.Max(x, w - x);
        var dy = Math.Max(y, h - y);
        return Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
    }

    /// <summary>4 × 4 ordered dither offset in −0.5..0.5 of one 8-bit step: the faint gradients would band otherwise.</summary>
    private static double Dither(int x, int y) => (s_bayer[((y & 3) << 2) | (x & 3)] + 0.5) / 16 - 0.5;

    private static readonly byte[] s_bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    private static int PackPremultiplied(double r, double g, double b, double a, double dither = 0)
    {
        var ai = Quantize(a, dither);
        var ri = Math.Min(ai, Quantize(r, dither));
        var gi = Math.Min(ai, Quantize(g, dither));
        var bi = Math.Min(ai, Quantize(b, dither));
        return (ai << 24) | (ri << 16) | (gi << 8) | bi;
    }

    private static int Quantize(double value, double dither) => Math.Clamp((int)Math.Round(value * 255 + dither), 0, 255);

    private static (double, double, double, double)[] BuildFoilStops()
    {
        // oklch stops at 0, 7, 14, 21, 28, 35 % of the gradient line; the period is 35 %, so positions are /0.35.
        (double At, double L, double C, double H)[] stops =
        [
            (0.0, 0.80, 0.05, 340), (0.2, 0.86, 0.045, 70), (0.4, 0.88, 0.045, 150),
            (0.6, 0.84, 0.045, 220), (0.8, 0.80, 0.05, 290), (1.0, 0.80, 0.05, 340),
        ];
        var result = new (double, double, double, double)[stops.Length];
        for (var i = 0; i < stops.Length; i++)
        {
            var color = Oklch.ToColor(stops[i].L, stops[i].C, stops[i].H);
            result[i] = (stops[i].At, color.R / 255.0, color.G / 255.0, color.B / 255.0);
        }
        return result;
    }

    private static (double X, double Y, double Radius)[] BuildGlitterDots()
    {
        // Our own scatter: a jittered 5 × 4 grid over the 120 px tile (no clumps, no visible rows), sizes 4 large
        // (1.5 px across), 8 medium (1 px) and 8 small (0.6 px), from a fixed xorshift seed.
        var dots = new (double, double, double)[20];
        uint state = 0x9E3779B9;
        double Next()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFF) / (double)0x1000000;
        }
        const double cellW = GlitterTile / 5;
        const double cellH = GlitterTile / 4;
        for (var i = 0; i < 20; i++)
        {
            var col = i % 5;
            var row = i / 5;
            var x = (col + 0.15 + 0.7 * Next()) * cellW;
            var y = (row + 0.15 + 0.7 * Next()) * cellH;
            var pick = Next();
            var radius = i % 5 == 2 ? 0.75 : pick < 0.5 ? 0.5 : 0.3;
            dots[i] = (x, y, radius);
        }
        return dots;
    }
}
