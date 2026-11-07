using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TKCode.Arc;

/// <summary>The kinds of procedural noise a card face uses.</summary>
internal enum WalletStackNoise
{
    /// <summary>Fine isotropic grain (black, glass and color cards).</summary>
    Grain,

    /// <summary>Anisotropic noise stretched along x: the horizontal lines of brushed metal.</summary>
    Brushed,
}

/// <summary>
/// Procedural textures of the card faces, generated in code (our own value noise, seeds and guilloche parameters; nothing
/// is copied): gray noise bitmaps for the blend layers, the engraved rosette of the black card, and the blurred,
/// saturated copies of cards that frosted glass shows through.
/// </summary>
internal static class WalletStackTextures
{
    /// <summary>
    /// A gray noise bitmap of <paramref name="size"/> pixels whose alpha is <paramref name="alpha"/> (the layer's opacity,
    /// baked in so a blend mode can apply it without an opacity layer). Frequencies are per CSS px; <paramref name="scaling"/>
    /// converts them to device pixels.
    /// </summary>
    public static WriteableBitmap? Noise(PixelSize size, double scaling, WalletStackNoise kind, double frequency, double alpha, int seed)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return null;
        }
        var bitmap = new WriteableBitmap(size, new Vector(96 * scaling, 96 * scaling), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using var frame = bitmap.Lock();
        var row = new int[size.Width];
        var a = Math.Clamp(alpha, 0, 1);
        double fx, fy;
        if (kind == WalletStackNoise.Brushed)
        {
            fx = 0.003 / scaling;
            fy = frequency / scaling;
        }
        else
        {
            fx = fy = frequency / scaling;
        }
        for (var y = 0; y < size.Height; y++)
        {
            for (var x = 0; x < size.Width; x++)
            {
                // Two octaves of value noise, desaturated (gray), around mid gray.
                var n = 0.62 * ValueNoise(x * fx, y * fy, seed) + 0.38 * ValueNoise(x * fx * 2, y * fy * 2, seed + 17);
                var gray = Math.Clamp(0.5 + (n - 0.5) * 0.9, 0, 1);
                row[x] = Premultiplied(gray, gray, gray, a);
            }
            Marshal.Copy(row, 0, frame.Address + y * frame.RowBytes, size.Width);
        }
        return bitmap;
    }

    /// <summary>Smooth value noise in [0, 1] (quintic-faded bilinear interpolation of hashed lattice values).</summary>
    public static double ValueNoise(double x, double y, int seed)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var tx = Fade(x - x0);
        var ty = Fade(y - y0);
        var a = Hash(x0, y0, seed);
        var b = Hash(x0 + 1, y0, seed);
        var c = Hash(x0, y0 + 1, seed);
        var d = Hash(x0 + 1, y0 + 1, seed);
        var top = a + (b - a) * tx;
        var bottom = c + (d - c) * tx;
        return top + (bottom - top) * ty;
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

    private static double Hash(int x, int y, int seed)
    {
        unchecked
        {
            var h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
            h = (h ^ (h >> 13)) * 1103515245u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (double)0xFFFFFF;
        }
    }

    private static int Premultiplied(double r, double g, double b, double a)
    {
        var ai = (int)Math.Round(a * 255);
        var ri = (int)Math.Round(r * a * 255);
        var gi = (int)Math.Round(g * a * 255);
        var bi = (int)Math.Round(b * a * 255);
        return (ai << 24) | (ri << 16) | (gi << 8) | bi;
    }

    /// <summary>
    /// Our guilloche rosette for the black card, in a 856×540 design box (ID-1 × 10): 14 closed rings about (616, 216)
    /// (72% / 40% of the face), radii 70 → 304, squashed to 0.78 vertically, each with a 9-lobe wave whose phase turns a
    /// little from ring to ring so the lobes twist into a spiral.
    /// </summary>
    public static StreamGeometry Guilloche()
    {
        const int rings = 14;
        const int lobes = 9;
        const int segments = 216;
        const double cx = 616, cy = 216, inner = 70, outer = 304, aspect = 0.78, amplitude = 9.5;
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        for (var ring = 0; ring < rings; ring++)
        {
            var radius = inner + (outer - inner) * ring / (rings - 1);
            var phase = ring * 0.21;
            for (var s = 0; s <= segments; s++)
            {
                var t = s * Math.PI * 2 / segments;
                var r = radius + amplitude * Math.Sin(lobes * t + phase) * (0.6 + 0.4 * Math.Cos(2 * t - phase));
                var point = new Point(cx + r * Math.Cos(t), cy + r * aspect * Math.Sin(t));
                if (s == 0)
                {
                    context.BeginFigure(point, false);
                }
                else
                {
                    context.LineTo(point);
                }
            }
            context.EndFigure(true);
        }
        return geometry;
    }

    /// <summary>
    /// A frosted-glass backdrop of <paramref name="source"/>: the bitmap (with a transparent margin of
    /// <paramref name="pad"/> DIPs) scaled down by <paramref name="downscale"/>, blurred with three box passes
    /// approximating a Gaussian of <paramref name="sigma"/> DIPs and saturated by <paramref name="saturation"/>
    /// (CSS <c>blur(24px) saturate(1.4)</c>). Returns null when the platform cannot read pixels back.
    /// </summary>
    public static WriteableBitmap? Frost(Bitmap source, Size size, double pad, double sigma, double saturation, double downscale = 0.25)
    {
        var pixel = new PixelSize(
            Math.Max(2, (int)Math.Ceiling((size.Width + 2 * pad) * downscale)),
            Math.Max(2, (int)Math.Ceiling((size.Height + 2 * pad) * downscale)));
        using var small = new RenderTargetBitmap(pixel, new Vector(96, 96));
        using (var context = small.CreateDrawingContext())
        {
            var dest = new Rect(pad * downscale, pad * downscale, size.Width * downscale, size.Height * downscale);
            using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
            {
                context.DrawImage(source, new Rect(source.PixelSize.ToSize(1)), dest);
            }
        }
        // Read back in the bitmap's own channel order (Skia often stores RGBA) and write the result the same way.
        var format = small.Format ?? PixelFormat.Bgra8888;
        var rgba = format == PixelFormat.Rgba8888;
        var stride = pixel.Width * 4;
        var buffer = new int[pixel.Width * pixel.Height];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            small.CopyPixels(new PixelRect(pixel), handle.AddrOfPinnedObject(), buffer.Length * 4, stride);
        }
        finally
        {
            handle.Free();
        }
        var radius = Math.Max(1, (int)Math.Round(BoxRadius(sigma * downscale)));
        var channels = new double[4][];
        for (var c = 0; c < 4; c++)
        {
            channels[c] = new double[buffer.Length];
        }
        for (var i = 0; i < buffer.Length; i++)
        {
            var p = buffer[i];
            channels[0][i] = p & 0xFF;
            channels[1][i] = (p >> 8) & 0xFF;
            channels[2][i] = (p >> 16) & 0xFF;
            channels[3][i] = (p >> 24) & 0xFF;
        }
        var scratch = new double[buffer.Length];
        foreach (var channel in channels)
        {
            for (var pass = 0; pass < 3; pass++)
            {
                BoxBlur(channel, scratch, pixel.Width, pixel.Height, radius, horizontal: true);
                BoxBlur(scratch, channel, pixel.Width, pixel.Height, radius, horizontal: false);
            }
        }
        var output = new WriteableBitmap(pixel, new Vector(96, 96), rgba ? PixelFormat.Rgba8888 : PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var frame = output.Lock())
        {
            var row = new int[pixel.Width];
            for (var y = 0; y < pixel.Height; y++)
            {
                for (var x = 0; x < pixel.Width; x++)
                {
                    var i = y * pixel.Width + x;
                    var a = channels[3][i];
                    double c0 = channels[0][i], g = channels[1][i], c2 = channels[2][i];
                    var r = rgba ? c0 : c2;
                    var b = rgba ? c2 : c0;
                    if (a > 0.5)
                    {
                        // Saturate the unpremultiplied color about its luma (CSS saturate()), then premultiply again.
                        var k = 255 / a;
                        r *= k;
                        g *= k;
                        b *= k;
                        var luma = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                        r = Math.Clamp(luma + (r - luma) * saturation, 0, 255) / k;
                        g = Math.Clamp(luma + (g - luma) * saturation, 0, 255) / k;
                        b = Math.Clamp(luma + (b - luma) * saturation, 0, 255) / k;
                    }
                    var low = rgba ? r : b;
                    var high = rgba ? b : r;
                    row[x] = (Clamp(a) << 24) | (Clamp(high) << 16) | (Clamp(g) << 8) | Clamp(low);
                }
                Marshal.Copy(row, 0, frame.Address + y * frame.RowBytes, pixel.Width);
            }
        }
        return output;
    }

    private static int Clamp(double value) => (int)Math.Clamp(Math.Round(value), 0, 255);

    /// <summary>The box radius whose three passes approximate a Gaussian of <paramref name="sigma"/>.</summary>
    public static double BoxRadius(double sigma) => (Math.Sqrt(4 * sigma * sigma + 1) - 1) / 2;

    private static void BoxBlur(double[] source, double[] target, int width, int height, int radius, bool horizontal)
    {
        var length = horizontal ? width : height;
        var lines = horizontal ? height : width;
        var window = 2 * radius + 1;
        for (var line = 0; line < lines; line++)
        {
            double sum = 0;
            for (var k = -radius; k <= radius; k++)
            {
                sum += Sample(source, width, height, line, k, length, horizontal);
            }
            for (var i = 0; i < length; i++)
            {
                var index = horizontal ? line * width + i : i * width + line;
                target[index] = sum / window;
                sum += Sample(source, width, height, line, i + radius + 1, length, horizontal)
                     - Sample(source, width, height, line, i - radius, length, horizontal);
            }
        }
    }

    // Outside the bitmap is transparent (the card's margin), so edges fade as CSS blur does over a transparent backdrop.
    private static double Sample(double[] source, int width, int height, int line, int i, int length, bool horizontal)
    {
        if (i < 0 || i >= length)
        {
            return 0;
        }
        return horizontal ? source[line * width + i] : source[i * width + line];
    }
}
