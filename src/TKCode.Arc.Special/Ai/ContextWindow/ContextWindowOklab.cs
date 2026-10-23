using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A color of the context window grid in OKLab with straight alpha, plus the alpha of the reserve outline. Cells move
/// between two of these the way CSS transitions do: in OKLab, with premultiplied alpha, so a filled cell fades into an
/// outlined reserve cell without passing through black.
/// </summary>
internal struct ContextWindowCellInk
{
    public float L;
    public float A;
    public float B;

    /// <summary>The fill's alpha (0 for the reserve's empty fill).</summary>
    public float Alpha;

    /// <summary>The outline's alpha (1 for the reserve, 0 otherwise).</summary>
    public float Outline;

    public static ContextWindowCellInk Fill(Color color)
    {
        var (l, a, b) = ContextWindowOklab.FromColor(color);
        return new ContextWindowCellInk { L = (float)l, A = (float)a, B = (float)b, Alpha = color.A / 255f, Outline = 0 };
    }

    public static ContextWindowCellInk Outlined(Color hiddenFill) => Fill(hiddenFill) with { Alpha = 0, Outline = 1 };

    /// <summary>The ink <paramref name="t"/> of the way from <paramref name="from"/> to <paramref name="to"/> (premultiplied).</summary>
    public static ContextWindowCellInk Lerp(in ContextWindowCellInk from, in ContextWindowCellInk to, double t)
    {
        var u = 1 - t;
        var alpha = from.Alpha * u + to.Alpha * t;
        var result = new ContextWindowCellInk { Alpha = (float)alpha, Outline = (float)(from.Outline * u + to.Outline * t) };
        if (alpha <= 1e-6)
        {
            result.L = to.L;
            result.A = to.A;
            result.B = to.B;
            return result;
        }
        var wa = from.Alpha * u / alpha;
        var wb = to.Alpha * t / alpha;
        result.L = (float)(from.L * wa + to.L * wb);
        result.A = (float)(from.A * wa + to.A * wb);
        result.B = (float)(from.B * wa + to.B * wb);
        return result;
    }

    /// <summary>The fill as an opaque sRGB color (its alpha is applied by the caller).</summary>
    public readonly Color ToRgb() => ContextWindowOklab.ToColor(L, A, B);
}

/// <summary>sRGB ↔ OKLab (Björn Ottosson's matrices), allocation free.</summary>
internal static class ContextWindowOklab
{
    public static (double L, double A, double B) FromColor(Color color)
    {
        var r = Decode(color.R / 255.0);
        var g = Decode(color.G / 255.0);
        var b = Decode(color.B / 255.0);
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return (0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    public static Color ToColor(double lightness, double a, double b, byte alpha = 255)
    {
        var l = lightness + 0.3963377774 * a + 0.2158037573 * b;
        var m = lightness - 0.1055613458 * a - 0.0638541728 * b;
        var s = lightness - 0.0894841775 * a - 1.2914855480 * b;
        l = l * l * l;
        m = m * m * m;
        s = s * s * s;
        var red = 4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s;
        var green = -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s;
        var blue = -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s;
        return Color.FromArgb(alpha, Encode(red), Encode(green), Encode(blue));
    }

    private static double Decode(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    private static byte Encode(double c)
    {
        c = Math.Clamp(c, 0, 1);
        var v = c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;
        return (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
    }
}
