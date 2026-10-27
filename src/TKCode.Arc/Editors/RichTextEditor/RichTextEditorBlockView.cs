using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The laid out form of one block: its text layout (with marks as run overrides), metrics, and inline code boxes.
/// Views are cached by block identity, so an edit reflows only the blocks it replaced.
/// </summary>
internal sealed class RichTextEditorBlockView : IDisposable
{
    public RichTextEditorBlockView(RichTextEditorBlock block)
    {
        Block = block;
    }

    public RichTextEditorBlock Block { get; }

    public TextLayout? Layout { get; set; }

    /// <summary>Height of the block's box (code blocks include their padding).</summary>
    public double Height { get; set; }

    /// <summary>Height of the block's own content; a table cell's <see cref="Height"/> is its row's height, the tallest cell's.</summary>
    public double ContentHeight { get; set; }

    /// <summary>Text origin inside the box (a table cell adds its column's offset).</summary>
    public double TextX { get; set; }

    /// <summary>The text's offset inside the block's box, before a table cell's column offset.</summary>
    public double PadX { get; set; }

    /// <summary>The left edge and width of a table cell's box; the whole width for any other block.</summary>
    public double BoxX { get; set; }

    public double BoxWidth { get; set; }

    /// <summary>True for a cell in the last column / the last row of its table (those draw the closing borders).</summary>
    public bool IsLastColumn { get; set; }

    public bool IsLastRow { get; set; }

    /// <summary>True for a table cell in the header row or header column: it is tinted and its text is medium weight.</summary>
    public bool IsHeader { get; set; }

    /// <summary>An image's drawn size, or the placeholder's.</summary>
    public Size ImageSize { get; set; }

    public double TextY { get; set; }

    public double FontSize { get; set; }

    public double LineHeight { get; set; }

    public FontWeight Weight { get; set; }

    public double LayoutWidth { get; set; }

    public int StyleVersion { get; set; }

    /// <summary>Uncommitted IME text shown at <see cref="PreeditAt"/>, or null.</summary>
    public string? Preedit { get; set; }

    public int PreeditAt { get; set; }

    /// <summary>Inline code backgrounds relative to the text origin.</summary>
    public List<Rect>? CodeRects { get; set; }

    /// <summary>Highlight backgrounds relative to the text origin.</summary>
    public List<(Rect Rect, RichTextColor Color)>? FillRects { get; set; }

    /// <summary>Superscript and subscript text, drawn over the room the layout keeps for it (positions relative to the text origin).</summary>
    public List<(TextLayout Layout, Point At)>? ScriptRuns { get; set; }

    private double[]? _lineTops;

    /// <summary>Maps a block offset to the layout's text position (shifted past preedit text).</summary>
    public int ToLayout(int offset) => Preedit is { Length: > 0 } preedit && offset >= PreeditAt ? offset + preedit.Length : offset;

    /// <summary>Maps a layout text position back to a block offset.</summary>
    public int FromLayout(int position)
    {
        if (Preedit is not { Length: > 0 } preedit || position <= PreeditAt)
        {
            return Math.Clamp(position, 0, Block.Length);
        }
        return Math.Clamp(position <= PreeditAt + preedit.Length ? PreeditAt : position - preedit.Length, 0, Block.Length);
    }

    public double LineTop(int line)
    {
        if (Layout is null)
        {
            return 0;
        }
        if (_lineTops is null)
        {
            var lines = Layout.TextLines;
            _lineTops = new double[lines.Count + 1];
            for (var i = 0; i < lines.Count; i++)
            {
                _lineTops[i + 1] = _lineTops[i] + lines[i].Height;
            }
        }
        return _lineTops[Math.Clamp(line, 0, _lineTops.Length - 1)];
    }

    public int LineCount => Layout?.TextLines.Count ?? 1;

    public int LineOf(int offset)
    {
        if (Layout is null)
        {
            return 0;
        }
        var position = ToLayout(offset);
        var lines = Layout.TextLines;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var contentEnd = line.FirstTextSourceIndex + line.Length - line.NewLineLength;
            if (position < contentEnd)
            {
                return i;
            }
            // The end of a line that ends in a newline stays on it; a wrap boundary belongs to the next line.
            if (position == contentEnd && (line.NewLineLength > 0 || i == lines.Count - 1))
            {
                return i;
            }
        }
        return lines.Count - 1;
    }

    public void Dispose()
    {
        Layout?.Dispose();
        if (ScriptRuns is { } runs)
        {
            foreach (var (layout, _) in runs)
            {
                layout.Dispose();
            }
        }
        Layout = null;
    }
}
