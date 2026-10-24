namespace TKCode.Arc;

/// <summary>
/// The frosted copy behind the glass card: a Gaussian blur (three box passes per axis) and a saturation boost on a
/// 32-bit pixel buffer, the CPU counterpart of CSS <c>backdrop-filter: blur(…) saturate(…)</c>. Edges clamp, so the
/// copy does not darken at the borders. Pure and allocation-light: one scratch buffer per call.
/// </summary>
internal static class LoginImmersiveBlur
{
    /// <summary>
    /// The three box widths whose successive passes approximate a Gaussian of <paramref name="sigma"/>
    /// (odd widths; the first ones narrower, so the variance matches).
    /// </summary>
    public static int[] BoxSizes(double sigma, int passes = 3)
    {
        var ideal = Math.Sqrt(12 * sigma * sigma / passes + 1);
        var lower = (int)Math.Floor(ideal);
        if (lower % 2 == 0)
        {
            lower--;
        }
        lower = Math.Max(1, lower);
        var upper = lower + 2;
        var mIdeal = (12 * sigma * sigma - passes * lower * lower - 4 * passes * lower - 3 * passes) / (-4.0 * lower - 4);
        var m = (int)Math.Round(mIdeal);
        var sizes = new int[passes];
        for (var i = 0; i < passes; i++)
        {
            sizes[i] = i < m ? lower : upper;
        }
        return sizes;
    }

    /// <summary>
    /// Blurs <paramref name="pixels"/> (4 bytes per pixel, rows <paramref name="stride"/> bytes apart) with a Gaussian of
    /// <paramref name="sigma"/> pixels, then scales saturation by <paramref name="saturation"/> (1 = unchanged).
    /// <paramref name="redFirst"/> says whether the byte order is RGBA (true) or BGRA (false); alpha is the fourth byte.
    /// </summary>
    public static void Apply(byte[] pixels, int width, int height, int stride, double sigma, double saturation, bool redFirst)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }
        if (sigma > 0.25)
        {
            var scratch = new byte[pixels.Length];
            foreach (var size in BoxSizes(sigma))
            {
                var radius = (size - 1) / 2;
                if (radius <= 0)
                {
                    continue;
                }
                BoxHorizontal(pixels, scratch, width, height, stride, radius);
                BoxVertical(scratch, pixels, width, height, stride, radius);
            }
        }
        if (Math.Abs(saturation - 1) > 1e-6)
        {
            Saturate(pixels, width, height, stride, saturation, redFirst);
        }
    }

    private static void BoxHorizontal(byte[] source, byte[] target, int width, int height, int stride, int radius)
    {
        var window = 2 * radius + 1;
        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            for (var c = 0; c < 4; c++)
            {
                var sum = 0;
                for (var k = -radius; k <= radius; k++)
                {
                    sum += source[row + Math.Clamp(k, 0, width - 1) * 4 + c];
                }
                for (var x = 0; x < width; x++)
                {
                    target[row + x * 4 + c] = (byte)((sum + window / 2) / window);
                    var add = Math.Min(x + radius + 1, width - 1);
                    var remove = Math.Max(x - radius, 0);
                    sum += source[row + add * 4 + c] - source[row + remove * 4 + c];
                }
            }
        }
    }

    private static void BoxVertical(byte[] source, byte[] target, int width, int height, int stride, int radius)
    {
        var window = 2 * radius + 1;
        for (var x = 0; x < width; x++)
        {
            var column = x * 4;
            for (var c = 0; c < 4; c++)
            {
                var sum = 0;
                for (var k = -radius; k <= radius; k++)
                {
                    sum += source[Math.Clamp(k, 0, height - 1) * stride + column + c];
                }
                for (var y = 0; y < height; y++)
                {
                    target[y * stride + column + c] = (byte)((sum + window / 2) / window);
                    var add = Math.Min(y + radius + 1, height - 1);
                    var remove = Math.Max(y - radius, 0);
                    sum += source[add * stride + column + c] - source[remove * stride + column + c];
                }
            }
        }
    }

    /// <summary>CSS <c>saturate(s)</c>: the filter-effects colour matrix, applied to (premultiplied) red, green and blue.</summary>
    private static void Saturate(byte[] pixels, int width, int height, int stride, double s, bool redFirst)
    {
        var rr = 0.213 + 0.787 * s;
        var rg = 0.715 - 0.715 * s;
        var rb = 0.072 - 0.072 * s;
        var gr = 0.213 - 0.213 * s;
        var gg = 0.715 + 0.285 * s;
        var gb = 0.072 - 0.072 * s;
        var br = 0.213 - 0.213 * s;
        var bg = 0.715 - 0.715 * s;
        var bb = 0.072 + 0.928 * s;
        var ri = redFirst ? 0 : 2;
        var bi = redFirst ? 2 : 0;
        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            for (var x = 0; x < width; x++)
            {
                var i = row + x * 4;
                double r = pixels[i + ri];
                double g = pixels[i + 1];
                double b = pixels[i + bi];
                double a = pixels[i + 3];
                pixels[i + ri] = Clamp(rr * r + rg * g + rb * b, a);
                pixels[i + 1] = Clamp(gr * r + gg * g + gb * b, a);
                pixels[i + bi] = Clamp(br * r + bg * g + bb * b, a);
            }
        }
    }

    /// <summary>Rounds into 0..alpha (a premultiplied channel never exceeds its alpha).</summary>
    private static byte Clamp(double value, double alpha) => (byte)Math.Clamp(Math.Round(value), 0, alpha);
}
