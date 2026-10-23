using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc.Primitives;

/// <summary>
/// A decoration on a character range of a <see cref="TextLayout"/>: a rounded background repeated on every wrapped
/// line (CSS <c>box-decoration-break: clone</c>), a strikethrough and an underline, all drawn by
/// <see cref="TextRangeRenderer"/> without relayout. Indices are into the layout's text: the string for
/// <c>Text</c>, the concatenated run text for <c>Inlines</c> (an <c>InlineUIContainer</c> counts as one character).
/// </summary>
/// <remarks>
/// The per-line rectangles are cached on the decoration, keyed on the layout instance and
/// (<see cref="Start"/>, <see cref="Length"/>, <see cref="Extent"/>): a new layout (text, width or style change)
/// measures again, while changing <see cref="Opacity"/>, brushes, pens, <see cref="Outset"/>,
/// <see cref="CornerRadius"/> or <see cref="UnderlineOffset"/> only needs a redraw (for example
/// <see cref="DecoratedTextBlock.InvalidateDecorations"/>) and allocates nothing. A decoration shared by two layouts
/// works but measures again whenever the layout it is drawn on changes.
/// </remarks>
public sealed class TextRangeDecoration
{
    private object? _layout;
    private int _start;
    private int _length;
    private TextRangeExtent _extent;
    private TextRangeLine[] _lines = [];
    private int _lineCount;

    /// <summary>Creates an empty decoration (set <see cref="Start"/> and <see cref="Length"/>).</summary>
    public TextRangeDecoration()
    {
    }

    /// <summary>Creates a decoration on <paramref name="length"/> characters from <paramref name="start"/>.</summary>
    /// <param name="start">First character index.</param>
    /// <param name="length">Number of characters.</param>
    public TextRangeDecoration(int start, int length)
    {
        Start = start;
        Length = length;
    }

    /// <summary>First character index. Parts outside the text are clipped.</summary>
    public int Start { get; set; }

    /// <summary>Number of characters; zero or negative draws nothing.</summary>
    public int Length { get; set; }

    /// <summary>Fill under the glyphs, one rounded rectangle per line; null draws no background.</summary>
    public IBrush? Background { get; set; }

    /// <summary>Corner radius of each line's background, clamped to half its height (and width).</summary>
    public double CornerRadius { get; set; }

    /// <summary>
    /// How far each line's background reaches past the text on every side (horizontal padding, vertical outset);
    /// negative values inset it. Does not move the strikethrough or underline.
    /// </summary>
    public Thickness Outset { get; set; }

    /// <summary>Vertical extent of the background before <see cref="Outset"/>: the text box (default) or the line box.</summary>
    public TextRangeExtent Extent { get; set; } = TextRangeExtent.Text;

    /// <summary>Pen of the strikethrough drawn over the glyphs at the font's strikethrough position; null draws none.</summary>
    public IPen? StrikeThrough { get; set; }

    /// <summary>Pen of the underline drawn over the glyphs (a dash style gives a dotted underline); null draws none.</summary>
    public IPen? Underline { get; set; }

    /// <summary>Distance of the underline below the baseline in DIPs (default 2).</summary>
    public double UnderlineOffset { get; set; } = 2;

    /// <summary>
    /// Opacity of the whole decoration, 0 to 1 (default 1); 0 draws nothing. Animate it by setting it and redrawing:
    /// the cached rectangles are reused.
    /// </summary>
    public double Opacity { get; set; } = 1;

    /// <summary>Any value the owner wants to keep with the decoration (a citation id, a diff operation).</summary>
    public object? Tag { get; set; }

    /// <summary>How many times the rectangles were measured (tests: the cache is reused when only paint changes).</summary>
    internal int MeasureCount { get; private set; }

    /// <summary>
    /// The measured lines for <paramref name="layout"/>: the cached ones while the layout instance and the range are
    /// unchanged, otherwise measured again into the same buffer.
    /// </summary>
    internal ReadOnlySpan<TextRangeLine> GetLines(TextLayout layout)
    {
        if (!ReferenceEquals(_layout, layout) || _start != Start || _length != Length || _extent != Extent)
        {
            _layout = layout;
            _start = Start;
            _length = Length;
            _extent = Extent;
            _lineCount = TextRangeRenderer.Measure(layout, _start, _length, _extent, ref _lines);
            MeasureCount++;
        }
        return new ReadOnlySpan<TextRangeLine>(_lines, 0, _lineCount);
    }

    /// <summary>Drops the cached layout reference (the owner detached or removed the decoration).</summary>
    internal void ResetCache()
    {
        _layout = null;
        _lineCount = 0;
    }
}

/// <summary>
/// One visual line of a measured range in layout coordinates: the background box (extent applied, before the
/// outset; its left and right are also the strikethrough and underline ends), the baseline and the strikethrough y.
/// </summary>
internal readonly record struct TextRangeLine(Rect Box, double Baseline, double StrikeY);
