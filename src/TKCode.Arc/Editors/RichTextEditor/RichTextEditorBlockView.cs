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

    /// <summary>Text origin inside the box.</summary>
    public double TextX { get; set; }

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
        Layout = null;
    }
}
