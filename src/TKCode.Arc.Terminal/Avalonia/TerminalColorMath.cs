using TKCode.Terminal.Core;

namespace TKCode.Terminal.Avalonia;

/// <summary>
/// Color arithmetic the terminal renderer uses: WCAG relative luminance and contrast ratio, mixing, and the minimum
/// contrast adjustment (the same steps as xterm.js <c>ensureContrastRatio</c>). Public so that hosts drawing their own
/// layers (<see cref="ITerminalRenderLayer"/>) pick colors the way the view does.
/// </summary>
public static class TerminalColorMath
{
    /// <summary>WCAG 2 relative luminance of an sRGB color, from 0 (black) to 1 (white).</summary>
    public static double RelativeLuminance(TerminalRgb color) =>
        0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);

    /// <summary>WCAG 2 contrast ratio of two colors, from 1 (equal luminance) to 21 (black on white).</summary>
    public static double ContrastRatio(TerminalRgb a, TerminalRgb b) => ContrastRatio(RelativeLuminance(a), RelativeLuminance(b));

    /// <summary>Mixes <paramref name="from"/> towards <paramref name="to"/>: 0 gives <paramref name="from"/>, 1 gives <paramref name="to"/>.</summary>
    public static TerminalRgb Mix(TerminalRgb from, TerminalRgb to, double amount)
    {
        var t = Math.Clamp(amount, 0, 1);
        return new TerminalRgb(Lerp(from.R, to.R, t), Lerp(from.G, to.G, t), Lerp(from.B, to.B, t));
    }

    /// <summary>
    /// <paramref name="foreground"/>, or the nearest color in its direction (darker or lighter) whose contrast with
    /// <paramref name="background"/> is at least <paramref name="ratio"/>. Each step darkens the channels by 10% or moves them 10%
    /// towards white, as xterm.js does; when neither direction reaches the ratio, the one that comes closer wins.
    /// A ratio of 1 or less returns <paramref name="foreground"/>.
    /// </summary>
    public static TerminalRgb EnsureContrast(TerminalRgb foreground, TerminalRgb background, double ratio)
    {
        if (ratio <= 1)
        {
            return foreground;
        }
        var bgL = RelativeLuminance(background);
        var fgL = RelativeLuminance(foreground);
        if (ContrastRatio(fgL, bgL) >= ratio)
        {
            return foreground;
        }
        var first = fgL < bgL ? Darken(foreground, bgL, ratio) : Lighten(foreground, bgL, ratio);
        var firstRatio = ContrastRatio(RelativeLuminance(first), bgL);
        if (firstRatio >= ratio)
        {
            return first;
        }
        var second = fgL < bgL ? Lighten(foreground, bgL, ratio) : Darken(foreground, bgL, ratio);
        return ContrastRatio(RelativeLuminance(second), bgL) > firstRatio ? second : first;
    }

    private static TerminalRgb Darken(TerminalRgb c, double bgL, double ratio)
    {
        int r = c.R, g = c.G, b = c.B;
        while (ContrastRatio(RelativeLuminance(new TerminalRgb((byte)r, (byte)g, (byte)b)), bgL) < ratio && (r > 0 || g > 0 || b > 0))
        {
            r -= Math.Max(1, (int)Math.Ceiling(r * 0.1));
            g -= Math.Max(1, (int)Math.Ceiling(g * 0.1));
            b -= Math.Max(1, (int)Math.Ceiling(b * 0.1));
            r = Math.Max(0, r);
            g = Math.Max(0, g);
            b = Math.Max(0, b);
        }
        return new TerminalRgb((byte)r, (byte)g, (byte)b);
    }

    private static TerminalRgb Lighten(TerminalRgb c, double bgL, double ratio)
    {
        int r = c.R, g = c.G, b = c.B;
        while (ContrastRatio(RelativeLuminance(new TerminalRgb((byte)r, (byte)g, (byte)b)), bgL) < ratio && (r < 255 || g < 255 || b < 255))
        {
            r = Math.Min(255, r + Math.Max(1, (int)Math.Ceiling((255 - r) * 0.1)));
            g = Math.Min(255, g + Math.Max(1, (int)Math.Ceiling((255 - g) * 0.1)));
            b = Math.Min(255, b + Math.Max(1, (int)Math.Ceiling((255 - b) * 0.1)));
        }
        return new TerminalRgb((byte)r, (byte)g, (byte)b);
    }

    private static double ContrastRatio(double l1, double l2) => (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);

    private static double Channel(byte value)
    {
        var c = value / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static byte Lerp(byte a, byte b, double t) => (byte)Math.Round(a + (b - a) * t, MidpointRounding.AwayFromZero);
}
