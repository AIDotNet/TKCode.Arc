using System.Globalization;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Hue (degrees), saturation, value and alpha (0..1). The hue is kept when saturation or value reach zero.</summary>
internal readonly record struct ColorPickerHsva(double H, double S, double V, double A);

/// <summary>sRGB channels and alpha, 0..1.</summary>
internal readonly record struct ColorPickerRgba(double R, double G, double B, double A);

/// <summary>
/// Color math of Arc's color picker (same formulas and rounding): HSV, HSL and OKLCH conversions, the four text
/// formats, the tolerant parser (hex, rgb(), hsl(), oklch() in modern or comma syntax) and WCAG contrast of the color
/// composited over a background. Hex is CSS order (#RRGGBBAA), not Avalonia's #AARRGGBB.
/// </summary>
internal static class ColorPickerMath
{
    public static double Clamp01(double value) => Math.Min(1, Math.Max(0, value));

    private static double Round(double value, int digits = 0)
    {
        var f = Math.Pow(10, digits);
        return Math.Round(value * f, MidpointRounding.AwayFromZero) / f;
    }

    public static ColorPickerRgba HsvToRgb(ColorPickerHsva c)
    {
        double F(double n)
        {
            var k = (n + c.H / 60) % 6;
            return c.V - c.V * c.S * Math.Max(0, Math.Min(k, Math.Min(4 - k, 1)));
        }
        return new ColorPickerRgba(F(5), F(3), F(1), c.A);
    }

    public static ColorPickerHsva RgbToHsv(ColorPickerRgba c, double hue = 0)
    {
        var max = Math.Max(c.R, Math.Max(c.G, c.B));
        var min = Math.Min(c.R, Math.Min(c.G, c.B));
        var d = max - min;
        var h = hue;
        if (d > 1e-6)
        {
            if (max == c.R)
            {
                h = 60 * (((c.G - c.B) / d) % 6);
            }
            else if (max == c.G)
            {
                h = 60 * ((c.B - c.R) / d + 2);
            }
            else
            {
                h = 60 * ((c.R - c.G) / d + 4);
            }
            if (h < 0)
            {
                h += 360;
            }
        }
        return new ColorPickerHsva(h, max == 0 ? 0 : d / max, max, c.A);
    }

    public static ColorPickerRgba HslToRgb(double h, double s, double l, double a)
    {
        double K(double n) => (n + h / 30) % 12;
        var chroma = s * Math.Min(l, 1 - l);
        double F(double n) => l - chroma * Math.Max(-1, Math.Min(K(n) - 3, Math.Min(9 - K(n), 1)));
        return new ColorPickerRgba(F(0), F(8), F(4), a);
    }

    public static (double H, double S, double L) RgbToHsl(ColorPickerRgba c, double hue)
    {
        var max = Math.Max(c.R, Math.Max(c.G, c.B));
        var min = Math.Min(c.R, Math.Min(c.G, c.B));
        var l = (max + min) / 2;
        var d = max - min;
        var s = d < 1e-6 ? 0 : d / (1 - Math.Abs(2 * l - 1));
        return (d < 1e-6 ? hue : RgbToHsv(c with { A = 1 }).H, s, l);
    }

    public static double ToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    private static double FromLinear(double c) => c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;

    public static (double L, double C, double H) RgbToOklch(ColorPickerRgba c, double hue)
    {
        double lr = ToLinear(c.R), lg = ToLinear(c.G), lb = ToLinear(c.B);
        var l = Math.Cbrt(0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb);
        var m = Math.Cbrt(0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb);
        var s = Math.Cbrt(0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb);
        var bigL = 0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s;
        var bigA = 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s;
        var bigB = 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s;
        var chroma = Math.Sqrt(bigA * bigA + bigB * bigB);
        var h = Math.Atan2(bigB, bigA) * 180 / Math.PI;
        if (h < 0)
        {
            h += 360;
        }
        return (bigL, chroma, chroma < 0.0005 ? hue : h);
    }

    public static ColorPickerRgba OklchToRgb(double bigL, double chroma, double hue, double alpha)
    {
        var a = chroma * Math.Cos(hue * Math.PI / 180);
        var b = chroma * Math.Sin(hue * Math.PI / 180);
        var l = Math.Pow(bigL + 0.3963377774 * a + 0.2158037573 * b, 3);
        var m = Math.Pow(bigL - 0.1055613458 * a - 0.0638541728 * b, 3);
        var s = Math.Pow(bigL - 0.0894841775 * a - 1.291485548 * b, 3);
        return new ColorPickerRgba(
            Clamp01(FromLinear(4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s)),
            Clamp01(FromLinear(-1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s)),
            Clamp01(FromLinear(-0.0041960863 * l - 0.7034186147 * m + 1.707614701 * s)),
            alpha);
    }

    public static byte Byte(double c) => (byte)Math.Round(Clamp01(c) * 255, MidpointRounding.AwayFromZero);

    private static string Hex2(double c) => Byte(c).ToString("X2", CultureInfo.InvariantCulture);

    /// <summary>Upper-case hex, with two alpha digits when the color is not opaque (Arc's onValueChange value).</summary>
    public static string ToHex(ColorPickerHsva c)
    {
        var rgb = HsvToRgb(c);
        return $"#{Hex2(rgb.R)}{Hex2(rgb.G)}{Hex2(rgb.B)}{(c.A < 0.999 ? Hex2(c.A) : string.Empty)}";
    }

    public static Color ToColor(ColorPickerHsva c)
    {
        var rgb = HsvToRgb(c);
        return Color.FromArgb(Byte(c.A), Byte(rgb.R), Byte(rgb.G), Byte(rgb.B));
    }

    public static Color Opaque(ColorPickerHsva c) => ToColor(c with { A = 1 });

    /// <summary>The fully saturated, full-value hue (the area's base and the hue thumb's fill).</summary>
    public static Color PureHue(double hue) => ToColor(new ColorPickerHsva(hue, 1, 1, 1));

    public static ColorPickerHsva FromColor(Color color, double hue = 0) =>
        RgbToHsv(new ColorPickerRgba(color.R / 255.0, color.G / 255.0, color.B / 255.0, color.A / 255.0), hue);

    /// <summary>The color as text in <paramref name="kind"/> (Arc's format()).</summary>
    public static string Format(ColorPickerHsva c, ColorPickerFormat kind)
    {
        var rgb = HsvToRgb(c);
        var inv = CultureInfo.InvariantCulture;
        var alpha = c.A < 0.999 ? $" / {Math.Round(c.A * 100, MidpointRounding.AwayFromZero).ToString(inv)}%" : string.Empty;
        switch (kind)
        {
            case ColorPickerFormat.Hex:
                return ToHex(c);
            case ColorPickerFormat.Rgb:
                return $"rgb({Byte(rgb.R)} {Byte(rgb.G)} {Byte(rgb.B)}{alpha})";
            case ColorPickerFormat.Hsl:
            {
                var (h, s, l) = RgbToHsl(rgb, c.H);
                var hue = Math.Round(h, MidpointRounding.AwayFromZero) % 360;
                return $"hsl({hue.ToString(inv)} {Math.Round(s * 100, MidpointRounding.AwayFromZero).ToString(inv)}% {Math.Round(l * 100, MidpointRounding.AwayFromZero).ToString(inv)}%{alpha})";
            }
            default:
            {
                var (l, chroma, h) = RgbToOklch(rgb, c.H);
                return $"oklch({Round(l * 100, 1).ToString(inv)}% {Round(chroma, 3).ToString(inv)} {(Round(h, 1) % 360).ToString(inv)}{alpha})";
            }
        }
    }

    private static double ReadAlpha(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 1;
        }
        return text.EndsWith('%') ? Clamp01(ParseNumber(text[..^1]) / 100) : Clamp01(ParseNumber(text));
    }

    private static double ParseNumber(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;

    /// <summary>Reads hex, rgb(), hsl() and oklch() in modern or comma syntax; null for anything else.</summary>
    public static ColorPickerHsva? Parse(string? input, double hue = 0)
    {
        if (input is null)
        {
            return null;
        }
        var text = input.Trim().ToLowerInvariant();
        if (TryHex(text, out var hex))
        {
            return RgbToHsv(hex, hue);
        }
        if (TryFunction(text, "rgb", out var args) && args.Count is 3 or 4)
        {
            double r = ParseNumber(args[0]), g = ParseNumber(args[1]), b = ParseNumber(args[2]);
            if (double.IsNaN(r) || double.IsNaN(g) || double.IsNaN(b))
            {
                return null;
            }
            return RgbToHsv(new ColorPickerRgba(Clamp01(r / 255), Clamp01(g / 255), Clamp01(b / 255), ReadAlpha(args.Count == 4 ? args[3] : null)), hue);
        }
        if (TryFunction(text, "hsl", out args) && args.Count is 3 or 4)
        {
            var hText = args[0].EndsWith("deg", StringComparison.Ordinal) ? args[0][..^3] : args[0];
            double h = ParseNumber(hText), s = ParseNumber(args[1].TrimEnd('%')), l = ParseNumber(args[2].TrimEnd('%'));
            if (double.IsNaN(h) || double.IsNaN(s) || double.IsNaN(l))
            {
                return null;
            }
            h %= 360;
            var next = RgbToHsv(HslToRgb(h, Clamp01(s / 100), Clamp01(l / 100), ReadAlpha(args.Count == 4 ? args[3] : null)), h);
            return next with { H = h };
        }
        if (TryFunction(text, "oklch", out args, commas: false) && args.Count is 3 or 4)
        {
            var lText = args[0];
            var percent = lText.EndsWith('%');
            var l = ParseNumber(percent ? lText[..^1] : lText);
            var hText = args[2].EndsWith("deg", StringComparison.Ordinal) ? args[2][..^3] : args[2];
            double c = ParseNumber(args[1]), h = ParseNumber(hText);
            if (double.IsNaN(l) || double.IsNaN(c) || double.IsNaN(h))
            {
                return null;
            }
            return RgbToHsv(OklchToRgb(Clamp01(percent ? l / 100 : l), c, h, ReadAlpha(args.Count == 4 ? args[3] : null)), hue);
        }
        return null;
    }

    private static bool TryHex(string text, out ColorPickerRgba rgba)
    {
        rgba = default;
        var digits = text.StartsWith('#') ? text[1..] : text;
        if (digits.Length is not (3 or 4 or 6 or 8) || !digits.All(Uri.IsHexDigit))
        {
            return false;
        }
        if (digits.Length <= 4)
        {
            digits = string.Concat(digits.Select(d => $"{d}{d}"));
        }
        double N(int i) => int.Parse(digits.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        rgba = new ColorPickerRgba(N(0), N(2), N(4), digits.Length == 8 ? N(6) : 1);
        return true;
    }

    /// <summary>Splits <c>name(a b c / d)</c> or <c>namea(a, b, c, d)</c> into its arguments.</summary>
    private static bool TryFunction(string text, string name, out List<string> args, bool commas = true)
    {
        args = new List<string>();
        string body;
        if (text.StartsWith(name + "a(", StringComparison.Ordinal) && commas)
        {
            body = text[(name.Length + 2)..];
        }
        else if (text.StartsWith(name + "(", StringComparison.Ordinal))
        {
            body = text[(name.Length + 1)..];
        }
        else
        {
            return false;
        }
        if (!body.EndsWith(')'))
        {
            return false;
        }
        body = body[..^1];
        var slash = body.IndexOf('/');
        string? alpha = null;
        if (slash >= 0)
        {
            alpha = body[(slash + 1)..].Trim();
            body = body[..slash];
        }
        var separators = commas ? new[] { ' ', ',', '\t' } : new[] { ' ', '\t' };
        args.AddRange(body.Split(separators, StringSplitOptions.RemoveEmptyEntries));
        if (args.Any(a => a.StartsWith('-')))
        {
            // Arc's patterns only take unsigned numbers.
            return false;
        }
        if (alpha is not null)
        {
            if (alpha.Length == 0 || args.Count != 3)
            {
                return false;
            }
            args.Add(alpha);
        }
        return args.Count > 0;
    }

    /// <summary>WCAG contrast of the color composited over <paramref name="background"/>, against that background.</summary>
    public static double Contrast(ColorPickerHsva c, ColorPickerRgba background)
    {
        var fg = HsvToRgb(c);
        double Mix(double channel, double bg) => channel * fg.A + bg * (1 - fg.A);
        static double Luminance(double r, double g, double b) => 0.2126 * ToLinear(r) + 0.7152 * ToLinear(g) + 0.0722 * ToLinear(b);
        var a = Luminance(Mix(fg.R, background.R), Mix(fg.G, background.G), Mix(fg.B, background.B));
        var b = Luminance(background.R, background.G, background.B);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>WCAG level: AAA ≥ 7, AA ≥ 4.5, AA large ≥ 3, else Fails.</summary>
    public static string Level(double ratio) => ratio >= 7 ? "AAA" : ratio >= 4.5 ? "AA" : ratio >= 3 ? "AA large" : "Fails";

    public static ColorPickerRgba Rgba(Color color) => new(color.R / 255.0, color.G / 255.0, color.B / 255.0, color.A / 255.0);
}
