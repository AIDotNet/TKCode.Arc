using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// The font a <see cref="CodeEditorView"/> draws its text with, and what the line cache needs to shape it: the glyphs
/// of the printable ASCII characters when the font is monospaced and has them all (the fast path), the fixed advance,
/// and the baseline inside a line box.
/// </summary>
internal sealed class CodeEditorTextMetrics
{
    private readonly ushort[]? _asciiGlyphs;

    private CodeEditorTextMetrics(CodeEditorTextMetrics source, IBrush foreground)
    {
        _asciiGlyphs = source._asciiGlyphs;
        Typeface = source.Typeface;
        GlyphTypeface = source.GlyphTypeface;
        FontSize = source.FontSize;
        CharWidth = source.CharWidth;
        LineHeight = source.LineHeight;
        Baseline = source.Baseline;
        Foreground = foreground;
    }

    internal CodeEditorTextMetrics(Typeface typeface, double fontSize, double charWidth, double lineHeight, double baseline, IBrush foreground)
    {
        Typeface = typeface;
        FontSize = fontSize;
        CharWidth = charWidth;
        LineHeight = lineHeight;
        Baseline = baseline;
        Foreground = foreground;
        if (FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphTypeface))
        {
            _asciiGlyphs = CreateAsciiGlyphs(glyphTypeface);
            GlyphTypeface = _asciiGlyphs is null ? null : glyphTypeface;
        }
    }

    internal Typeface Typeface { get; }

    /// <summary>The face the fast path draws with; null when the font cannot take it (not monospaced, ASCII glyphs missing).</summary>
    internal GlyphTypeface? GlyphTypeface { get; }

    internal double FontSize { get; }

    /// <summary>The advance of one column; the fast path places every glyph on this grid.</summary>
    internal double CharWidth { get; }

    internal double LineHeight { get; }

    /// <summary>The y of the baseline from the top of a line box.</summary>
    internal double Baseline { get; }

    /// <summary>The brush the fallback layouts are built with (glyph runs take theirs when drawn).</summary>
    internal IBrush Foreground { get; }

    /// <summary>The same font with another brush for the fallback layouts.</summary>
    internal CodeEditorTextMetrics WithForeground(IBrush foreground) =>
        ReferenceEquals(foreground, Foreground) ? this : new CodeEditorTextMetrics(this, foreground);

    /// <summary>The glyph of a printable ASCII character or a space; 0 when the fast path is off.</summary>
    internal ushort GetAsciiGlyph(char c) => _asciiGlyphs is { } glyphs && c is >= ' ' and <= '~' ? glyphs[c - ' '] : (ushort)0;

    internal static bool IsFastPathCharacter(char c) => c is >= ' ' and <= '~' or '\t';

    // The fast path skips shaping, so it needs a glyph for each printable ASCII character and one advance for all of
    // them (a monospaced font; ligatures are not applied, as upstream's fast path does not).
    private static ushort[]? CreateAsciiGlyphs(GlyphTypeface glyphTypeface)
    {
        var map = glyphTypeface.CharacterToGlyphMap;
        var glyphs = new ushort[('~' - ' ') + 1];
        for (var c = ' '; c <= '~'; c++)
        {
            if (!map.TryGetGlyph(c, out var glyph) || glyph == 0)
            {
                return null;
            }

            glyphs[c - ' '] = glyph;
        }

        var advances = new ushort[glyphs.Length];
        if (!glyphTypeface.TryGetHorizontalGlyphAdvances(glyphs, advances))
        {
            return null;
        }

        var first = advances[0];
        foreach (var advance in advances)
        {
            if (Math.Abs(advance - first) > 1)
            {
                return null;
            }
        }

        return first == 0 ? null : glyphs;
    }
}

/// <summary>
/// Shaped view lines, keyed by their content, tab size and token colors (the font, the foreground and the palette of
/// the token colors are the cache's; a change of any of them starts a new cache). Two generations, as the terminal's row cache, but a generation ends only when the cache
/// holds more than <see cref="Capacity"/> lines (or twice the lines a frame draws): then what was not used since the
/// generation before is dropped. So lines that scroll out and back in are not shaped again
/// (design/architecture/code-editor.md section 7).
/// </summary>
internal sealed class CodeEditorTextCache
{
    /// <summary>Lines longer than this (in UTF-16 code units, tabs expanded) are shaped and drawn in pieces, only the visible ones.</summary>
    internal const int LongLineLength = 2000;

    /// <summary>The length of one piece of a long line.</summary>
    internal const int ChunkLength = 256;

    /// <summary>The lines the cache keeps before it drops the ones not used lately.</summary>
    internal const int Capacity = 512;

    private Dictionary<LineKey, CodeEditorShapedLine> _current = new();
    private Dictionary<LineKey, CodeEditorShapedLine> _previous = new();

    internal CodeEditorTextCache(CodeEditorTextMetrics metrics)
    {
        Metrics = metrics;
    }

    internal CodeEditorTextMetrics Metrics { get; private set; }

    /// <summary>How many lines were prepared (tabs expanded, glyphs looked up); a cache hit adds nothing.</summary>
    internal int LinesCreated { get; private set; }

    /// <summary>How many glyph runs and text layouts were built.</summary>
    internal int PiecesShaped { get; set; }

    internal int Count => _current.Count + _previous.Count;

    /// <summary>Drops every line and shapes with new metrics from now on.</summary>
    internal void Reset(CodeEditorTextMetrics metrics)
    {
        Clear();
        Metrics = metrics;
    }

    internal void Clear()
    {
        foreach (var line in _current.Values)
        {
            line.Dispose();
        }

        foreach (var line in _previous.Values)
        {
            line.Dispose();
        }

        _current.Clear();
        _previous.Clear();
    }

    /// <summary>The brushes of the token colors by color id (the color map); null when there are none.</summary>
    internal IBrush[]? Palette { get; private set; }

    /// <summary>Uses new token colors from now on; the lines are shaped again, as their layouts carry brushes.</summary>
    internal void SetPalette(IBrush[]? palette)
    {
        Clear();
        Palette = palette;
    }

    /// <summary>The brush of a color id; <paramref name="fallback"/> for -1, an id the palette lacks, or no palette.</summary>
    internal IBrush GetBrush(int colorId, IBrush fallback) =>
        Palette is { } palette && colorId >= 0 && colorId < palette.Length && palette[colorId] is { } brush ? brush : fallback;

    internal CodeEditorShapedLine Get(string content, int tabSize, CodeEditorLineColors? colors = null)
    {
        var key = new LineKey(content, tabSize, colors);
        if (_current.TryGetValue(key, out var line))
        {
            return line;
        }

        if (!_previous.Remove(key, out line))
        {
            line = new CodeEditorShapedLine(this, content, tabSize, colors);
            LinesCreated++;
        }

        _current[key] = line;
        return line;
    }

    /// <summary>
    /// Ends a frame that drew <paramref name="drawnLines"/> lines: when the cache is full, the lines not used since the
    /// last time it was full are dropped.
    /// </summary>
    internal void EndFrame(int drawnLines)
    {
        if (Count <= Math.Max(Capacity, drawnLines * 2))
        {
            return;
        }

        foreach (var line in _previous.Values)
        {
            line.Dispose();
        }

        _previous.Clear();
        (_previous, _current) = (_current, _previous);
    }

    private readonly record struct LineKey(string Content, int TabSize, CodeEditorLineColors? Colors);
}
