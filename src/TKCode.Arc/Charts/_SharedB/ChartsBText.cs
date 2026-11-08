using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>Builds the cached <see cref="FormattedText"/> runs that team B's charts draw.</summary>
internal static class ChartsBText
{
    public static FormattedText Make(string text, ChartsBPalette palette, double size, Color color,
                                     FontWeight weight = FontWeight.Normal, FontFamily? family = null)
        => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(family ?? palette.BodyFont, FontStyle.Normal, weight), size, palette.Brush(color));

    /// <summary>A run for text that is redrawn on every frame of an animation; see <see cref="ChartsBRun"/>.</summary>
    public static ChartsBRun Run(string text, ChartsBPalette palette, double size, Color color,
                                 FontWeight weight = FontWeight.Normal, FontFamily? family = null)
        => new(text, new Typeface(family ?? palette.BodyFont, FontStyle.Normal, weight), size, palette.Brush(color));

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

/// <summary>
/// One line of text that stays shaped between draws. <see cref="FormattedText"/> breaks and shapes its text again on every
/// draw, which a tooltip or a centre label pays on each frame of a glide or morph; a <see cref="TextLayout"/> keeps its lines.
/// </summary>
internal sealed class ChartsBRun : IDisposable
{
    private readonly string _text;
    private readonly Typeface _typeface;
    private readonly double _size;
    private readonly IBrush _brush;
    private double _maxWidth = double.PositiveInfinity;
    private TextLayout? _layout;

    public ChartsBRun(string text, Typeface typeface, double size, IBrush brush)
    {
        _text = text;
        _typeface = typeface;
        _size = size;
        _brush = brush;
    }

    private TextLayout Layout => _layout ??= new TextLayout(_text, _typeface, _size, _brush, TextAlignment.Left, TextWrapping.NoWrap,
        double.IsInfinity(_maxWidth) ? null : TextTrimming.CharacterEllipsis, null, FlowDirection.LeftToRight, _maxWidth,
        double.PositiveInfinity, double.NaN, 0, 1);

    public double Width => Layout.WidthIncludingTrailingWhitespace;

    public double Height => Layout.Height;

    /// <summary>Trims the line with an ellipsis when it is wider than <paramref name="maxWidth"/>.</summary>
    public void Constrain(double maxWidth)
    {
        if (maxWidth > 0 && Width > maxWidth)
        {
            _maxWidth = maxWidth;
            Dispose();
        }
    }

    public void Draw(DrawingContext context, Point origin) => Layout.Draw(context, origin);

    /// <summary>Releases the shaped lines; the next use shapes the text again.</summary>
    public void Dispose()
    {
        _layout?.Dispose();
        _layout = null;
    }
}
