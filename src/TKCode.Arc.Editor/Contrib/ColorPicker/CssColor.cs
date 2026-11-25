// Ported from vscode (src/vs/base/common/color.ts: RGBA, HSLA, Color.Format.CSS.formatRGB / formatRGBA / formatHSL /
// formatHSLA / formatHex / formatHexA, commit b57d5c9e), MIT license.
// Differences: only what the color picker and the default color computer use; RGBA and HSLA are value types with
// upstream's clamping and rounding (channels truncated to integers, fractions rounded to three decimals); JavaScript's
// Math.round (half up) and Number#toFixed are written out.

using System.Globalization;

namespace TKCode.Arc.Editor.Contrib.ColorPicker;

/// <summary>A color in sRGB: channels 0..255 (integers), alpha 0..1 (upstream's RGBA).</summary>
internal readonly record struct CssRgba
{
    public CssRgba(double r, double g, double b, double a = 1)
    {
        R = (int)Math.Min(255, Math.Max(0, r));
        G = (int)Math.Min(255, Math.Max(0, g));
        B = (int)Math.Min(255, Math.Max(0, b));
        A = CssColor.RoundFloat(Math.Max(Math.Min(1, a), 0), 3);
    }

    public int R { get; }

    public int G { get; }

    public int B { get; }

    public double A { get; }
}

/// <summary>A color as hue 0..360 (integer), saturation, luminosity and alpha 0..1 (upstream's HSLA).</summary>
internal readonly record struct CssHsla
{
    public CssHsla(double h, double s, double l, double a)
    {
        H = (int)Math.Max(Math.Min(360, h), 0);
        S = CssColor.RoundFloat(Math.Max(Math.Min(1, s), 0), 3);
        L = CssColor.RoundFloat(Math.Max(Math.Min(1, l), 0), 3);
        A = CssColor.RoundFloat(Math.Max(Math.Min(1, a), 0), 3);
    }

    public int H { get; }

    public double S { get; }

    public double L { get; }

    public double A { get; }

    /// <summary>Converts an RGB color to HSL (upstream's HSLA.fromRGBA).</summary>
    public static CssHsla FromRgba(CssRgba rgba)
    {
        var r = rgba.R / 255.0;
        var g = rgba.G / 255.0;
        var b = rgba.B / 255.0;
        var a = rgba.A;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        double h = 0;
        double s = 0;
        var l = (min + max) / 2;
        var chroma = max - min;

        if (chroma > 0)
        {
            s = Math.Min(l <= 0.5 ? chroma / (2 * l) : chroma / (2 - (2 * l)), 1);
            if (max == r)
            {
                h = ((g - b) / chroma) + (g < b ? 6 : 0);
            }
            else if (max == g)
            {
                h = ((b - r) / chroma) + 2;
            }
            else
            {
                h = ((r - g) / chroma) + 4;
            }

            h *= 60;
            h = CssColor.JsRound(h);
        }

        return new CssHsla(h, s, l, a);
    }

    /// <summary>Converts an HSL color to RGB (upstream's HSLA.toRGBA).</summary>
    public static CssRgba ToRgba(CssHsla hsla)
    {
        var h = hsla.H / 360.0;
        var s = hsla.S;
        var l = hsla.L;
        double r, g, b;

        if (s == 0)
        {
            r = g = b = l; // achromatic
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - (l * s);
            var p = (2 * l) - q;
            r = Hue2Rgb(p, q, h + (1 / 3.0));
            g = Hue2Rgb(p, q, h);
            b = Hue2Rgb(p, q, h - (1 / 3.0));
        }

        return new CssRgba(CssColor.JsRound(r * 255), CssColor.JsRound(g * 255), CssColor.JsRound(b * 255), hsla.A);
    }

    private static double Hue2Rgb(double p, double q, double t)
    {
        if (t < 0)
        {
            t += 1;
        }

        if (t > 1)
        {
            t -= 1;
        }

        if (t < 1 / 6.0)
        {
            return p + ((q - p) * 6 * t);
        }

        if (t < 1 / 2.0)
        {
            return q;
        }

        if (t < 2 / 3.0)
        {
            return p + ((q - p) * ((2 / 3.0) - t) * 6);
        }

        return p;
    }
}

/// <summary>CSS text of colors (upstream's Color.Format.CSS).</summary>
internal static class CssColor
{
    /// <summary><c>rgb(r, g, b)</c>, or <c>rgba(...)</c> when not opaque (upstream's formatRGB).</summary>
    public static string FormatRgb(CssRgba color) =>
        color.A == 1
            ? string.Create(CultureInfo.InvariantCulture, $"rgb({color.R}, {color.G}, {color.B})")
            : FormatRgba(color);

    /// <summary><c>rgba(r, g, b, a)</c> with alpha to at most two decimals (upstream's formatRGBA).</summary>
    public static string FormatRgba(CssRgba color) =>
        string.Create(CultureInfo.InvariantCulture, $"rgba({color.R}, {color.G}, {color.B}, {Math.Round(color.A, 2, MidpointRounding.AwayFromZero)})");

    /// <summary><c>hsl(h, s%, l%)</c>, or <c>hsla(...)</c> when not opaque (upstream's formatHSL).</summary>
    public static string FormatHsl(CssRgba color)
    {
        var hsla = CssHsla.FromRgba(color);
        return hsla.A == 1
            ? string.Create(CultureInfo.InvariantCulture, $"hsl({hsla.H}, {JsRound(hsla.S * 100)}%, {JsRound(hsla.L * 100)}%)")
            : FormatHsla(hsla);
    }

    /// <summary><c>hsla(h, s%, l%, a)</c> with alpha to two decimals (upstream's formatHSLA).</summary>
    public static string FormatHsla(CssHsla hsla) =>
        string.Create(CultureInfo.InvariantCulture, $"hsla({hsla.H}, {JsRound(hsla.S * 100)}%, {JsRound(hsla.L * 100)}%, {hsla.A.ToString("F2", CultureInfo.InvariantCulture)})");

    /// <summary><c>#rrggbb</c> (upstream's formatHex).</summary>
    public static string FormatHex(CssRgba color) =>
        string.Create(CultureInfo.InvariantCulture, $"#{color.R:x2}{color.G:x2}{color.B:x2}");

    /// <summary><c>#rrggbbaa</c>; with <paramref name="compact"/>, opaque colors as <c>#rrggbb</c> (upstream's formatHexA).</summary>
    public static string FormatHexA(CssRgba color, bool compact = false)
    {
        if (compact && color.A == 1)
        {
            return FormatHex(color);
        }

        return string.Create(CultureInfo.InvariantCulture, $"#{color.R:x2}{color.G:x2}{color.B:x2}{(int)JsRound(color.A * 255):x2}");
    }

    /// <summary>The color of a <see cref="Languages.LanguageColor"/> (channels 0..1) as CSS RGBA, rounding as the color picker does.</summary>
    public static CssRgba FromLanguageColor(Languages.LanguageColor color) =>
        new(JsRound(255 * color.Red), JsRound(255 * color.Green), JsRound(255 * color.Blue), color.Alpha);

    /// <summary>JavaScript's Math.round: half up.</summary>
    public static double JsRound(double value) => Math.Floor(value + 0.5);

    /// <summary>Upstream's roundFloat.</summary>
    public static double RoundFloat(double number, int decimalPoints)
    {
        var dec = Math.Pow(10, decimalPoints);
        return JsRound(number * dec) / dec;
    }
}
