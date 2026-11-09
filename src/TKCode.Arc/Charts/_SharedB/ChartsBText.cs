using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// Builds the text that team B's charts draw. Anything drawn more than once (every label a chart keeps) is a
/// <see cref="ChartsBRun"/> from <see cref="Run"/> or <see cref="TrimmedRun"/>; <see cref="Make"/> and <see cref="Trimmed"/>
/// give a <see cref="FormattedText"/>, which re-shapes on every draw, for text drawn once.
/// </summary>
internal static class ChartsBText
{
    public static FormattedText Make(string text, ChartsBPalette palette, double size, Color color,
                                     FontWeight weight = FontWeight.Normal, FontFamily? family = null)
        => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(family ?? palette.BodyFont, FontStyle.Normal, weight), size, palette.Brush(color));

    /// <summary>A single-line run for tooltip text that is drawn again and again (every frame of a glide); see <see cref="ChartsBRun"/>.</summary>
    public static ChartsBRun Run(string text, ChartsBPalette palette, double size, Color color,
                                 FontWeight weight = FontWeight.Normal, FontFamily? family = null)
        => new(text, new Typeface(family ?? palette.BodyFont, FontStyle.Normal, weight), size, palette.Brush(color));

    /// <summary>
    /// A kept run with the lines a <see cref="FormattedText"/> from <see cref="Make"/> would have (a label with a line break
    /// keeps its lines), for the labels a chart draws on every frame.
    /// </summary>
    public static ChartsBRun Label(string text, ChartsBPalette palette, double size, Color color,
                                   FontWeight weight = FontWeight.Normal, FontFamily? family = null)
        => new(text, new Typeface(family ?? palette.BodyFont, FontStyle.Normal, weight), size, palette.Brush(color), singleLine: false);

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

    /// <summary>A kept run on a single line, trimmed with an ellipsis to <paramref name="maxWidth"/>.</summary>
    public static ChartsBRun TrimmedRun(string text, ChartsBPalette palette, double size, Color color, double maxWidth,
                                        FontWeight weight = FontWeight.Normal)
    {
        var run = Label(text, palette, size, color, weight);
        run.Constrain(maxWidth);
        return run;
    }
}

/// <summary>
/// One piece of text that stays shaped between draws. <see cref="FormattedText"/> breaks and shapes its text again on every
/// draw, which a label pays on each frame of a glide, reveal or morph; a <see cref="TextLayout"/> keeps its lines. The
/// run is laid out with the brush it was made with; <see cref="Draw(DrawingContext, Point, IBrush)"/> paints the same
/// glyphs in another brush (a hover or fade color) without laying the text out again, so a color is never part of a cache key.
/// </summary>
internal sealed class ChartsBRun : IDisposable
{
    private readonly string _text;
    private readonly Typeface _typeface;
    private readonly double _size;
    private readonly IBrush _brush;
    private readonly bool _wrap;
    private readonly bool _singleLine;
    private double _maxWidth = double.PositiveInfinity;
    private TextLayout? _layout;

    /// <summary>
    /// An unconstrained run. <paramref name="singleLine"/> keeps only the first line (tooltip rows); otherwise a line
    /// break in the text makes a second line, as in a <see cref="FormattedText"/>.
    /// </summary>
    public ChartsBRun(string text, Typeface typeface, double size, IBrush brush, bool singleLine = true)
    {
        _text = text;
        _typeface = typeface;
        _size = size;
        _brush = brush;
        _singleLine = singleLine;
    }

    /// <summary>
    /// A run that is at most <paramref name="maxWidth"/> wide: wrapped (<paramref name="wrap"/>, the lines a
    /// <see cref="FormattedText"/> with that <c>MaxTextWidth</c> produces: words overflow and get a word ellipsis) or
    /// one line trimmed with a character ellipsis.
    /// </summary>
    public ChartsBRun(string text, Typeface typeface, double size, IBrush brush, double maxWidth, bool wrap) : this(text, typeface, size, brush, false)
    {
        _maxWidth = maxWidth > 0 ? maxWidth : double.PositiveInfinity;
        _wrap = wrap && !double.IsInfinity(_maxWidth);
    }

    private TextLayout Layout => _layout ??= double.IsInfinity(_maxWidth)
        ? new TextLayout(_text, _typeface, _size, _brush, TextAlignment.Left, TextWrapping.NoWrap, null, null, FlowDirection.LeftToRight,
            double.PositiveInfinity, double.PositiveInfinity, double.NaN, 0, _singleLine ? 1 : 0)
        : _wrap
            ? new TextLayout(_text, _typeface, _size, _brush, TextAlignment.Left, TextWrapping.WrapWithOverflow, TextTrimming.WordEllipsis, null,
                FlowDirection.LeftToRight, _maxWidth, double.PositiveInfinity, double.NaN, 0, 0)
            : new TextLayout(_text, _typeface, _size, _brush, TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis, null,
                FlowDirection.LeftToRight, _maxWidth, double.PositiveInfinity, double.NaN, 0, 1);

    public string Text => _text;

    /// <summary>Advance width including trailing whitespace (what <see cref="FormattedText.WidthIncludingTrailingWhitespace"/> gives).</summary>
    public double Width => Layout.WidthIncludingTrailingWhitespace;

    /// <summary>Same as <see cref="Width"/>, named as callers ported from <see cref="FormattedText"/> read it.</summary>
    public double WidthIncludingTrailingWhitespace => Layout.WidthIncludingTrailingWhitespace;

    /// <summary>Advance width without trailing whitespace (<see cref="FormattedText.Width"/>).</summary>
    public double TextWidth => Layout.Width;

    public double Height => Layout.Height;

    /// <summary>Distance from the top to the first line's baseline.</summary>
    public double Baseline => Layout.Baseline;

    /// <summary>Trims the line with an ellipsis when it is wider than <paramref name="maxWidth"/>.</summary>
    public void Constrain(double maxWidth)
    {
        if (maxWidth > 0 && Width > maxWidth)
        {
            Limit(maxWidth);
        }
    }

    /// <summary>Gives the line a width limit whether or not it needs one now (a bolder twin of a constrained run).</summary>
    public void Limit(double maxWidth)
    {
        if (maxWidth > 0)
        {
            _maxWidth = maxWidth;
            Dispose();
        }
    }

    /// <summary>Whether a width limit was set.</summary>
    public bool IsConstrained => !double.IsInfinity(_maxWidth);

    public void Draw(DrawingContext context, Point origin) => Layout.Draw(context, origin);

    /// <summary>
    /// Draws the laid-out glyphs in <paramref name="brush"/> instead of the brush the run was made with: the calls
    /// <see cref="TextLayout.Draw"/> makes, with the brush swapped (the <c>RollingDigits</c> glyph path).
    /// </summary>
    public void Draw(DrawingContext context, Point origin, IBrush brush)
    {
        if (ReferenceEquals(brush, _brush))
        {
            Layout.Draw(context, origin);
            return;
        }
        var y = origin.Y;
        var lines = Layout.TextLines;
        for (var l = 0; l < lines.Count; l++)
        {
            var line = lines[l];
            var x = origin.X + line.Start;
            var runs = line.TextRuns;
            for (var r = 0; r < runs.Count; r++)
            {
                if (runs[r] is not DrawableTextRun drawable)
                {
                    continue;
                }
                if (drawable is ShapedTextRun shaped && shaped.GlyphRun.GlyphInfos.Count > 0)
                {
                    using (context.PushTransform(Matrix.CreateTranslation(x, y + (line.Baseline - shaped.Baseline))))
                    {
                        context.DrawGlyphRun(brush, shaped.GlyphRun);
                    }
                }
                x += drawable.Size.Width;
            }
            y += line.Height;
        }
    }

    /// <summary>Releases the shaped lines; the next use shapes the text again.</summary>
    public void Dispose()
    {
        _layout?.Dispose();
        _layout = null;
    }
}
