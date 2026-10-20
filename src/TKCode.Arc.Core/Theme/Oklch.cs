using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// OKLCH / OKLab conversions with CSS Color 4 gamut mapping, used to port Arc's oklch() tokens to sRGB.
/// </summary>
public static class Oklch
{
    /// <summary>Converts an sRGB color to OKLCH (L in 0..1, C, H in degrees).</summary>
    public static (double L, double C, double H) FromColor(Color color)
    {
        var (l, a, b) = LinearSrgbToOklab(Decode(color.R / 255.0), Decode(color.G / 255.0), Decode(color.B / 255.0));
        var c = Math.Sqrt(a * a + b * b);
        var h = Math.Atan2(b, a) * 180.0 / Math.PI;
        if (h < 0)
        {
            h += 360;
        }
        return (l, c, h);
    }

    /// <summary>Converts OKLCH to sRGB, reducing chroma until the color fits the sRGB gamut.</summary>
    public static Color ToColor(double l, double c, double h, double alpha = 1.0)
    {
        var (r, g, b) = GamutMap(l, c, h);
        return Color.FromArgb(ToByte(alpha), ToByte(Encode(r)), ToByte(Encode(g)), ToByte(Encode(b)));
    }

    /// <summary>Returns <paramref name="color"/> with a new alpha (0..1).</summary>
    public static Color WithAlpha(Color color, double alpha) => Color.FromArgb(ToByte(alpha), color.R, color.G, color.B);

    /// <summary>Mixes two colors in OKLab space (t = 0 → a, t = 1 → b), including alpha.</summary>
    public static Color MixOklab(Color a, Color b, double t)
    {
        var (l1, a1, b1) = LinearSrgbToOklab(Decode(a.R / 255.0), Decode(a.G / 255.0), Decode(a.B / 255.0));
        var (l2, a2, b2) = LinearSrgbToOklab(Decode(b.R / 255.0), Decode(b.G / 255.0), Decode(b.B / 255.0));
        var (r, g, bl) = OklabToLinearSrgb(l1 + (l2 - l1) * t, a1 + (a2 - a1) * t, b1 + (b2 - b1) * t);
        var alpha = a.A / 255.0 + (b.A / 255.0 - a.A / 255.0) * t;
        return Color.FromArgb(ToByte(alpha), ToByte(Encode(Clamp01(r))), ToByte(Encode(Clamp01(g))), ToByte(Encode(Clamp01(bl))));
    }

    /// <summary>WCAG 2 contrast ratio between two opaque colors.</summary>
    public static double Contrast(Color a, Color b)
    {
        var la = RelativeLuminance(a);
        var lb = RelativeLuminance(b);
        var hi = Math.Max(la, lb);
        var lo = Math.Min(la, lb);
        return (hi + 0.05) / (lo + 0.05);
    }

    internal static double RelativeLuminance(Color c)
        => 0.2126 * Decode(c.R / 255.0) + 0.7152 * Decode(c.G / 255.0) + 0.0722 * Decode(c.B / 255.0);

    private static (double R, double G, double B) GamutMap(double l, double c, double h)
    {
        if (l >= 1)
        {
            return (1, 1, 1);
        }
        if (l <= 0)
        {
            return (0, 0, 0);
        }
        var rgb = OklchToLinear(l, c, h);
        if (InGamut(rgb))
        {
            return rgb;
        }
        const double jnd = 0.02;
        const double eps = 0.0001;
        double lo = 0;
        var hi = c;
        var minInGamut = true;
        (double R, double G, double B)? clipped = null;
        while (hi - lo > eps)
        {
            var mid = (lo + hi) / 2;
            var current = OklchToLinear(l, mid, h);
            if (minInGamut && InGamut(current))
            {
                lo = mid;
                continue;
            }
            var clip = (Clamp01(current.R), Clamp01(current.G), Clamp01(current.B));
            clipped = clip;
            var target = OklchToOklab(l, mid, h);
            var actual = LinearSrgbToOklab(clip.Item1, clip.Item2, clip.Item3);
            var e = Math.Sqrt(Square(target.L - actual.L) + Square(target.A - actual.A) + Square(target.B - actual.B));
            if (e < jnd)
            {
                if (jnd - e < eps)
                {
                    return clip;
                }
                minInGamut = false;
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }
        return clipped ?? (Clamp01(rgb.R), Clamp01(rgb.G), Clamp01(rgb.B));
    }

    private static (double L, double A, double B) OklchToOklab(double l, double c, double h)
    {
        var hr = h * Math.PI / 180.0;
        return (l, c * Math.Cos(hr), c * Math.Sin(hr));
    }

    private static (double R, double G, double B) OklchToLinear(double l, double c, double h)
    {
        var (ll, a, b) = OklchToOklab(l, c, h);
        return OklabToLinearSrgb(ll, a, b);
    }

    private static (double R, double G, double B) OklabToLinearSrgb(double l, double a, double b)
    {
        var l_ = l + 0.3963377774 * a + 0.2158037573 * b;
        var m_ = l - 0.1055613458 * a - 0.0638541728 * b;
        var s_ = l - 0.0894841775 * a - 1.2914855480 * b;
        var ll = l_ * l_ * l_;
        var mm = m_ * m_ * m_;
        var ss = s_ * s_ * s_;
        return (+4.0767416621 * ll - 3.3077115913 * mm + 0.2309699292 * ss,
            -1.2684380046 * ll + 2.6097574011 * mm - 0.3413193965 * ss,
            -0.0041960863 * ll - 0.7034186147 * mm + 1.7076147010 * ss);
    }

    private static (double L, double A, double B) LinearSrgbToOklab(double r, double g, double b)
    {
        var l = Math.Cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
        var m = Math.Cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
        var s = Math.Cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
        return (0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
            1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
            0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s);
    }

    private static bool InGamut((double R, double G, double B) rgb)
    {
        const double e = 1e-6;
        return rgb.R >= -e && rgb.R <= 1 + e && rgb.G >= -e && rgb.G <= 1 + e && rgb.B >= -e && rgb.B <= 1 + e;
    }

    private static double Encode(double x)
    {
        x = Clamp01(x);
        return x <= 0.0031308 ? 12.92 * x : 1.055 * Math.Pow(x, 1 / 2.4) - 0.055;
    }

    private static double Decode(double x) => x <= 0.04045 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);

    private static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;

    private static double Square(double x) => x * x;

    private static byte ToByte(double unit) => (byte)Math.Round(Clamp01(unit) * 255.0);
}
