using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// A laid-out text of a self-drawn Showpiece scene: its lines, first baseline and size, drawn in any brush (see
/// <see cref="ShowpieceTextCache"/>).
/// </summary>
/// <remarks>
/// Avalonia 12's <c>FormattedText.Draw</c> breaks and shapes its lines again on every draw, cached or not (about 2 KB and
/// 17 µs per short label, more for trimmed or CJK text). A <see cref="TextLayout"/> keeps its lines, so drawing one only
/// emits its glyph runs. Drawing those runs with the caller's brush, instead of the brush the layout was built with,
/// lets one layout serve every color and fade level.
/// </remarks>
internal sealed class ShowpieceText : IDisposable
{
    private readonly TextLayout? _tail;

    public ShowpieceText(TextLayout layout)
        : this(layout, layout.TextLines.Count, null)
    {
    }

    /// <summary>The first <paramref name="keep"/> lines of <paramref name="layout"/>, then the first line of <paramref name="tail"/>.</summary>
    public ShowpieceText(TextLayout layout, int keep, TextLayout? tail)
    {
        Layout = layout;
        _tail = tail;
        var source = layout.TextLines;
        var count = Math.Min(keep, source.Count);
        var lines = new TextLine[count + (tail is { TextLines.Count: > 0 } ? 1 : 0)];
        for (var i = 0; i < count; i++)
        {
            lines[i] = source[i];
        }
        if (lines.Length > count)
        {
            lines[count] = tail!.TextLines[0];
        }
        Lines = lines;
        // The metrics TextLayout and FormattedText report: summed heights, widest line, the first line's baseline.
        foreach (var line in lines)
        {
            Height += line.Height;
            Width = Math.Max(Width, line.Width);
            WidthIncludingTrailingWhitespace = Math.Max(WidthIncludingTrailingWhitespace, line.WidthIncludingTrailingWhitespace);
        }
        Baseline = lines.Length > 0 ? lines[0].Baseline : layout.Height * 0.8;
    }

    /// <summary>The layout the text was laid out in (with a tail: its lines before the last).</summary>
    public TextLayout Layout { get; }

    /// <summary>The lines drawn, top to bottom.</summary>
    public IReadOnlyList<TextLine> Lines { get; }

    /// <summary>Distance from the top to the first line's baseline.</summary>
    public double Baseline { get; }

    /// <summary>Widest line, trailing whitespace excluded (as <c>FormattedText.Width</c>).</summary>
    public double Width { get; }

    /// <summary>Widest line with its trailing whitespace.</summary>
    public double WidthIncludingTrailingWhitespace { get; }

    /// <summary>Height of all lines.</summary>
    public double Height { get; }

    /// <summary>
    /// One line laid out without a width limit (left to right, like <c>FormattedText</c> without a width). The brush
    /// only matters for <see cref="TextLayout.Draw"/>; <see cref="Draw"/> takes its own.
    /// </summary>
    public static ShowpieceText Lay(string text, Typeface typeface, double fontSize) =>
        new(new TextLayout(text, typeface, fontSize, Brushes.Black));

    /// <summary>Draws the text in <paramref name="brush"/> with its top left corner at <paramref name="origin"/> (as <c>DrawText</c>).</summary>
    public void Draw(DrawingContext context, IBrush brush, Point origin) => DrawRuns(context, Lines, brush, origin);

    /// <summary>Draws the text in <paramref name="brush"/> with its first baseline at (<paramref name="x"/>, <paramref name="baseline"/>).</summary>
    public void DrawAtBaseline(DrawingContext context, IBrush brush, double x, double baseline) =>
        DrawRuns(context, Lines, brush, new Point(x, baseline - Baseline));

    /// <summary>
    /// Draws lines' glyph runs with <paramref name="brush"/> instead of the brush they were laid out with: the same calls
    /// as <see cref="TextLayout.Draw"/> for text without decorations, backgrounds or baseline shifts (each line from its
    /// start, each run on the line's baseline, one translated <c>DrawGlyphRun</c> per run).
    /// </summary>
    /// <remarks>
    /// Right-to-left and mixed text draws correctly too: a laid-out line keeps its runs in visual order (Avalonia
    /// reorders them when it finishes the line, and <see cref="TextLine.Draw"/> walks them in this same order), and every
    /// run's glyph run carries its own bidi level.
    /// </remarks>
    public static void DrawRuns(DrawingContext context, IReadOnlyList<TextLine> lines, IBrush brush, Point origin)
    {
        var y = origin.Y;
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

    /// <summary>Releases the layouts (the text must not be drawn afterwards).</summary>
    public void Dispose()
    {
        Layout.Dispose();
        _tail?.Dispose();
    }
}
