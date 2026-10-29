using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The ten-step color palette of a seed color, following the published algorithm of the MIT-licensed
/// <c>@ant-design/colors</c> package (<c>generate</c>, v7 with <c>@ant-design/fast-color</c>): five lighter steps and four
/// darker ones around the seed in HSV (hue turns 2° per step, saturation and value move in fixed steps, rounded to two
/// decimals), and for the dark theme each step is the background mixed toward a light-theme step by a fixed amount.
/// Index 5 is the seed in the light palette and the dark theme's primary in the dark one.
/// </summary>
internal static class ArcPaletteGenerator
{
    private const int HueStep = 2;
    private const double SaturationStep = 0.16;
    private const double SaturationStep2 = 0.05;
    private const double BrightnessStep1 = 0.05;
    private const double BrightnessStep2 = 0.15;
    private const int LightColorCount = 5;
    private const int DarkColorCount = 4;

    /// <summary>Dark theme: (light palette index, percent mixed from the background toward it) per output index.</summary>
    private static readonly (int Index, int Amount)[] s_darkColorMap =
    [
        (7, 15), (6, 25), (5, 30), (5, 45), (5, 65), (5, 85), (4, 90), (3, 95), (2, 97), (1, 98)
    ];

    /// <summary>The default dark background the dark palette is mixed against.</summary>
    public static readonly Color DefaultDarkBackground = Color.FromRgb(0x14, 0x14, 0x14);

    /// <summary>
    /// Ten opaque colors for <paramref name="seed"/> (its alpha is ignored), light to dark for the light theme, or mixed
    /// against <paramref name="darkBackground"/> (default <c>#141414</c>) when <paramref name="isDark"/>.
    /// </summary>
    public static Color[] Generate(Color seed, bool isDark, Color? darkBackground = null)
    {
        var (h, s, v) = ToHsv(seed.R, seed.G, seed.B);
        var patterns = new Color[LightColorCount + 1 + DarkColorCount];
        var n = 0;
        for (var i = LightColorCount; i > 0; i--)
        {
            patterns[n++] = FromHsv(GetHue(h, i, true), GetSaturation(h, s, i, true), GetValue(v, i, true));
        }
        patterns[n++] = Color.FromRgb(seed.R, seed.G, seed.B);
        for (var i = 1; i <= DarkColorCount; i++)
        {
            patterns[n++] = FromHsv(GetHue(h, i, false), GetSaturation(h, s, i, false), GetValue(v, i, false));
        }
        if (!isDark)
        {
            return patterns;
        }
        var bg = darkBackground ?? DefaultDarkBackground;
        var dark = new Color[s_darkColorMap.Length];
        for (var i = 0; i < s_darkColorMap.Length; i++)
        {
            var (index, amount) = s_darkColorMap[i];
            dark[i] = Mix(bg, patterns[index], amount);
        }
        return dark;
    }

    /// <summary>fast-color <c>getHue</c> / <c>getSaturation</c> / <c>getValue</c> from 0–255 channels; hue is rounded to whole degrees.</summary>
    private static (int H, double S, double V) ToHsv(byte r, byte g, byte b)
    {
        int max = Math.Max(r, Math.Max(g, b));
        int min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        int h;
        double s;
        if (delta == 0)
        {
            h = 0;
            s = 0;
        }
        else
        {
            double sector;
            if (r == max)
            {
                sector = (g - b) / delta + (g < b ? 6 : 0);
            }
            else if (g == max)
            {
                sector = (b - r) / delta + 2;
            }
            else
            {
                sector = (r - g) / delta + 4;
            }
            h = (int)JsRound(60 * sector);
            s = delta / max;
        }
        return (h, s, max / 255.0);
    }

    private static int GetHue(int h, int i, bool light)
    {
        int hue;
        if (h >= 60 && h <= 240)
        {
            hue = light ? h - HueStep * i : h + HueStep * i;
        }
        else
        {
            hue = light ? h + HueStep * i : h - HueStep * i;
        }
        if (hue < 0)
        {
            hue += 360;
        }
        else if (hue >= 360)
        {
            hue -= 360;
        }
        return hue;
    }

    private static double GetSaturation(int h, double s, int i, bool light)
    {
        // Grey keeps its saturation.
        if (h == 0 && s == 0)
        {
            return s;
        }
        double saturation;
        if (light)
        {
            saturation = s - SaturationStep * i;
        }
        else if (i == DarkColorCount)
        {
            saturation = s + SaturationStep;
        }
        else
        {
            saturation = s + SaturationStep2 * i;
        }
        if (saturation > 1)
        {
            saturation = 1;
        }
        // The lightest step keeps its saturation within 0.06–0.1.
        if (light && i == LightColorCount && saturation > 0.1)
        {
            saturation = 0.1;
        }
        if (saturation < 0.06)
        {
            saturation = 0.06;
        }
        return JsRound(saturation * 100) / 100;
    }

    private static double GetValue(double v, int i, bool light)
    {
        var value = light ? v + BrightnessStep1 * i : v - BrightnessStep2 * i;
        value = Math.Max(0, Math.Min(1, value));
        return JsRound(value * 100) / 100;
    }

    /// <summary>fast-color <c>fromHsv</c>: each channel rounded on its own.</summary>
    private static Color FromHsv(int h, double s, double v)
    {
        var vv = Channel(v * 255);
        if (s <= 0)
        {
            return Color.FromRgb(vv, vv, vv);
        }
        var hh = h / 60.0;
        var i = (int)Math.Floor(hh);
        var ff = hh - i;
        var p = Channel(v * (1.0 - s) * 255);
        var q = Channel(v * (1.0 - s * ff) * 255);
        var t = Channel(v * (1.0 - s * (1.0 - ff)) * 255);
        return i switch
        {
            0 => Color.FromRgb(vv, t, p),
            1 => Color.FromRgb(q, vv, p),
            2 => Color.FromRgb(p, vv, t),
            3 => Color.FromRgb(p, q, vv),
            4 => Color.FromRgb(t, p, vv),
            _ => Color.FromRgb(vv, p, q)
        };
    }

    /// <summary>fast-color <c>mix</c>: <paramref name="from"/> moved <paramref name="amount"/> percent toward <paramref name="to"/>.</summary>
    private static Color Mix(Color from, Color to, int amount)
    {
        var p = amount / 100.0;
        return Color.FromRgb(
            Channel((to.R - from.R) * p + from.R),
            Channel((to.G - from.G) * p + from.G),
            Channel((to.B - from.B) * p + from.B));
    }

    private static byte Channel(double value) => (byte)Math.Clamp(JsRound(value), 0, 255);

    /// <summary>JavaScript <c>Math.round</c>: nearest integer, halves toward positive infinity.</summary>
    private static double JsRound(double value)
    {
        var floor = Math.Floor(value);
        return value - floor >= 0.5 ? floor + 1 : floor;
    }
}
