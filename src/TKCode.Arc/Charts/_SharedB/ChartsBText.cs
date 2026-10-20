using System.Globalization;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Builds the cached <see cref="FormattedText"/> runs that team B's charts draw.</summary>
internal static class ChartsBText
{
    public static FormattedText Make(string text, ChartsBPalette palette, double size, Color color,
                                     FontWeight weight = FontWeight.Normal, FontFamily? family = null)
        => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(family ?? palette.BodyFont, FontStyle.Normal, weight), size, palette.Brush(color));

    /// <summary>Single line, trimmed with an ellipsis to <paramref name="maxWidth"/>.</summary>
    public static FormattedText Trimmed(string text, ChartsBPalette palette, double size, Color color, double maxWidth,
                                        FontWeight weight = FontWeight.Normal)
    {
        var formatted = Make(text, palette, size, color, weight);
        if (maxWidth > 0 && formatted.WidthIncludingTrailingWhitespace > maxWidth)
        {
            formatted.MaxTextWidth = maxWidth;
            formatted.MaxLineCount = 1;
            formatted.Trimming = TextTrimming.CharacterEllipsis;
        }
        return formatted;
    }
}
