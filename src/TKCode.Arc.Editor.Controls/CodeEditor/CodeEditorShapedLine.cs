using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using TKCode.Arc.Editor;

namespace TKCode.Arc;

/// <summary>
/// One view line, ready to draw, and the column to x mapping of what it draws.
/// <para>
/// Fast path (printable ASCII and tabs, in a monospaced font that has those glyphs): one glyph per column, looked up
/// in the font's character map, each advancing <see cref="CodeEditorTextMetrics.CharWidth"/>; no shaping. The x of a
/// column is its visible column times the char width.
/// </para>
/// <para>
/// Fallback (anything else: CJK, emoji, RTL, combining marks, or a font the fast path cannot use): a
/// <see cref="TextLayout"/>, which shapes and falls back to other fonts, with real advances, as upstream draws them. The x
/// of a column comes from the layout's own hit testing, so cursors and selections sit where the glyphs are drawn even
/// when a wide character is not exactly two columns wide.
/// </para>
/// <para>
/// Tabs are expanded to spaces up to the next tab stop of the visible columns (CursorColumns: full-width characters
/// and emoji count two). A line longer than <see cref="CodeEditorTextCache.LongLineLength"/> is cut into pieces of about
/// <see cref="CodeEditorTextCache.ChunkLength"/>, each shaped when first drawn and drawn only when visible. A fallback
/// piece starts at the grid x of its first visible column, so the pieces of a very long CJK line stay near the grid
/// and the x of a column is that start plus the hit test inside the piece.
/// </para>
/// <para>
/// Colors (the line's tokens, <see cref="CodeEditorLineColors"/>): a fast-path piece is drawn as one glyph run per run of
/// tokens of one color; a fallback layout gets a run of text properties per token. A line with a bold, italic,
/// underlined or struck-through token takes the fallback path, whose layouts can change the face.
/// </para>
/// </summary>
internal sealed class CodeEditorShapedLine : IDisposable
{
    private readonly CodeEditorTextCache _cache;
    private readonly CodeEditorTextMetrics _metrics;
    private readonly int _contentLength;

    // the expanded offset of each content offset (0..content.Length); null when they are equal (no tabs)
    private readonly int[]? _offsetToIndex;
    private readonly ushort[]? _glyphs;
    private readonly Piece[] _pieces;
    private readonly CodeEditorLineColors? _colors;

    internal CodeEditorShapedLine(CodeEditorTextCache cache, string content, int tabSize, CodeEditorLineColors? colors = null)
    {
        _cache = cache;
        _metrics = cache.Metrics;
        _contentLength = content.Length;
        _colors = colors;
        IsFastPath = _metrics.GlyphTypeface is not null && IsFastPathText(content) && colors is not { HasFontStyle: true };
        if (IsFastPath)
        {
            (Text, _offsetToIndex) = ExpandAsciiTabs(content, tabSize);
            _glyphs = new ushort[Text.Length];
            for (var i = 0; i < Text.Length; i++)
            {
                _glyphs[i] = _metrics.GetAsciiGlyph(Text[i]);
            }

            _pieces = CreateGridPieces(Text.Length, _metrics.CharWidth);
        }
        else
        {
            (Text, _offsetToIndex, _pieces) = ExpandFallback(content, tabSize, _metrics.CharWidth);
        }
    }

    /// <summary>The text as drawn: tabs expanded to spaces.</summary>
    internal string Text { get; }

    /// <summary>Whether the line takes the fast path (glyph runs on the column grid) rather than a text layout.</summary>
    internal bool IsFastPath { get; }

    internal int PieceCount => _pieces.Length;

    /// <summary>How many pieces have been shaped so far (a short line has one piece).</summary>
    internal int ShapedPieceCount
    {
        get
        {
            var count = 0;
            foreach (var piece in _pieces)
            {
                if (piece.IsShaped)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>The width of the line as drawn (a long fallback line shapes its last piece for it).</summary>
    internal double Width => GetX(_contentLength + 1);

    /// <summary>The x of a 1-based column from the start of the text.</summary>
    internal double GetX(int column)
    {
        var index = ToIndex(Math.Clamp(column - 1, 0, _contentLength));
        if (IsFastPath)
        {
            return index * _metrics.CharWidth;
        }

        if (Text.Length == 0)
        {
            return 0;
        }

        var piece = _pieces[FindPieceByIndex(index)];
        var layout = GetLayout(piece);
        return piece.Origin + layout.HitTestTextPosition(index - piece.Start).X;
    }

    /// <summary>The 1-based column nearest to an x from the start of the text.</summary>
    internal int GetColumn(double x)
    {
        int index;
        if (IsFastPath || Text.Length == 0)
        {
            index = (int)Math.Round(Math.Max(0, x) / _metrics.CharWidth);
        }
        else
        {
            var piece = _pieces[FindPieceByX(x)];
            var hit = GetLayout(piece).HitTestPoint(new Point(x - piece.Origin, _metrics.LineHeight / 2));
            index = piece.Start + hit.CharacterHit.FirstCharacterIndex + hit.CharacterHit.TrailingLength;
        }

        return ToOffset(Math.Clamp(index, 0, Text.Length)) + 1;
    }

    /// <summary>
    /// Draws the pieces of the line that meet the visible range [<paramref name="visibleLeft"/>,
    /// <paramref name="visibleRight"/>] (x from the start of the text), with the text starting at
    /// <paramref name="originX"/> and the line box at <paramref name="top"/>.
    /// </summary>
    internal void Draw(DrawingContext context, IBrush brush, double originX, double top, double visibleLeft, double visibleRight)
    {
        // a fallback piece may be wider than its columns: keep one more piece on either side
        var margin = _pieces.Length > 1 && !IsFastPath ? CodeEditorTextCache.ChunkLength * _metrics.CharWidth : 0;
        foreach (var piece in _pieces)
        {
            if (piece.End < visibleLeft - margin || piece.Origin > visibleRight + margin)
            {
                continue;
            }

            if (IsFastPath)
            {
                var runs = GetGlyphRuns(piece);
                if (runs.Count > 0)
                {
                    using (context.PushTransform(Matrix.CreateTranslation(originX + piece.Origin, top)))
                    {
                        foreach (var (run, colorId) in runs)
                        {
                            context.DrawGlyphRun(_cache.GetBrush(colorId, brush), run);
                        }
                    }
                }
            }
            else
            {
                var layout = GetLayout(piece);
                var baseline = layout.TextLines.Count > 0 ? layout.TextLines[0].Baseline : layout.Baseline;
                layout.Draw(context, new Point(originX + piece.Origin, top + _metrics.Baseline - baseline));
            }
        }
    }

    /// <summary>The x of each glyph cluster start as drawn (for tests): (index in <see cref="Text"/>, x from the start of the text).</summary>
    internal IReadOnlyList<(int Index, double X)> GetDrawnClusterPositions()
    {
        var result = new List<(int, double)>();
        foreach (var piece in _pieces)
        {
            if (IsFastPath)
            {
                foreach (var (run, _) in GetGlyphRuns(piece))
                {
                    var x = piece.Origin + run.BaselineOrigin.X;
                    foreach (var glyph in run.GlyphInfos)
                    {
                        result.Add((piece.Start + glyph.GlyphCluster, x));
                        x += glyph.GlyphAdvance;
                    }
                }
            }
            else
            {
                var layout = GetLayout(piece);
                foreach (var line in layout.TextLines)
                {
                    var x = piece.Origin + line.Start;
                    // a shaped run numbers its clusters from its own start, so the run's offset in the line is added
                    var runStart = line.FirstTextSourceIndex;
                    foreach (var textRun in line.TextRuns)
                    {
                        if (textRun is ShapedTextRun shaped)
                        {
                            foreach (var glyph in shaped.ShapedBuffer)
                            {
                                result.Add((piece.Start + runStart + glyph.GlyphCluster, x));
                                x += glyph.GlyphAdvance;
                            }
                        }
                        else
                        {
                            x += textRun is DrawableTextRun drawable ? drawable.Size.Width : 0;
                        }

                        runStart += textRun.Length;
                    }
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var piece in _pieces)
        {
            if (piece.Runs is { } runs)
            {
                foreach (var (run, _) in runs)
                {
                    run.Dispose();
                }
            }

            piece.Layout?.Dispose();
            piece.Runs = null;
            piece.Layout = null;
        }
    }

    // The glyph runs of a fast-path piece: one per run of one color (one for the piece without colors); none for blanks.
    private List<(GlyphRun Run, int ColorId)> GetGlyphRuns(Piece piece)
    {
        if (piece.Runs is { } existing)
        {
            return existing;
        }

        piece.IsShaped = true;
        var runs = new List<(GlyphRun, int)>();
        foreach (var (start, end, colorId) in GetColorSegments(piece.Start, piece.Start + piece.Length))
        {
            var hasInk = false;
            for (var i = start; i < end; i++)
            {
                if (Text[i] != ' ')
                {
                    hasInk = true;
                    break;
                }
            }

            if (!hasInk)
            {
                continue;
            }

            var infos = new GlyphInfo[end - start];
            for (var i = 0; i < infos.Length; i++)
            {
                infos[i] = new GlyphInfo(_glyphs![start + i], i, _metrics.CharWidth);
            }

            runs.Add((new GlyphRun(
                _metrics.GlyphTypeface!,
                _metrics.FontSize,
                Text.AsMemory(start, end - start),
                infos,
                new Point((start - piece.Start) * _metrics.CharWidth, _metrics.Baseline)), colorId));
            _cache.PiecesShaped++;
        }

        piece.Runs = runs;
        return runs;
    }

    /// <summary>
    /// The runs of one color between two indices in <see cref="Text"/>: (start, end, color id); color id -1 is the view's
    /// foreground (a line without colors).
    /// </summary>
    private IEnumerable<(int Start, int End, int ColorId)> GetColorSegments(int from, int to)
    {
        if (_colors is null)
        {
            yield return (from, to, -1);
            yield break;
        }

        var segmentStart = 0;
        for (var i = 0; i < _colors.Count && segmentStart < to; i++)
        {
            var segmentEnd = i == _colors.Count - 1 ? Text.Length : ToIndex(Math.Clamp(_colors.GetEndOffset(i), 0, _contentLength));
            var start = Math.Max(segmentStart, from);
            var end = Math.Min(segmentEnd, to);
            if (start < end)
            {
                yield return (start, end, _colors.GetForeground(i));
            }

            segmentStart = segmentEnd;
        }
    }

    private TextLayout GetLayout(Piece piece)
    {
        if (piece.Layout is { } layout)
        {
            return layout;
        }

        piece.IsShaped = true;
        piece.Layout = new TextLayout(
            Text.Substring(piece.Start, piece.Length),
            _metrics.Typeface,
            _metrics.FontSize,
            _metrics.Foreground,
            textStyleOverrides: GetStyleOverrides(piece));
        _cache.PiecesShaped++;
        return piece.Layout;
    }

    // The text properties of each token of a fallback piece (indices relative to the piece); null without colors.
    private List<ValueSpan<TextRunProperties>>? GetStyleOverrides(Piece piece)
    {
        if (_colors is null)
        {
            return null;
        }

        var overrides = new List<ValueSpan<TextRunProperties>>();
        var segmentStart = 0;
        var pieceEnd = piece.Start + piece.Length;
        for (var i = 0; i < _colors.Count && segmentStart < pieceEnd; i++)
        {
            var segmentEnd = i == _colors.Count - 1 ? Text.Length : ToIndex(Math.Clamp(_colors.GetEndOffset(i), 0, _contentLength));
            var start = Math.Max(segmentStart, piece.Start);
            var end = Math.Min(segmentEnd, pieceEnd);
            if (start < end)
            {
                var style = _colors.GetFontStyle(i);
                var typeface = new Typeface(
                    _metrics.Typeface.FontFamily,
                    (style & Editor.Tokens.FontStyle.Italic) != 0 ? Avalonia.Media.FontStyle.Italic : _metrics.Typeface.Style,
                    (style & Editor.Tokens.FontStyle.Bold) != 0 ? FontWeight.Bold : _metrics.Typeface.Weight);
                TextDecorationCollection? decorations = null;
                if ((style & Editor.Tokens.FontStyle.Underline) != 0)
                {
                    decorations = [.. TextDecorations.Underline];
                }

                if ((style & Editor.Tokens.FontStyle.Strikethrough) != 0)
                {
                    decorations ??= [];
                    foreach (var decoration in TextDecorations.Strikethrough)
                    {
                        decorations.Add(decoration);
                    }
                }

                overrides.Add(new ValueSpan<TextRunProperties>(
                    start - piece.Start,
                    end - start,
                    new GenericTextRunProperties(
                        typeface,
                        _metrics.FontSize,
                        textDecorations: decorations,
                        foregroundBrush: _cache.GetBrush(_colors.GetForeground(i), _metrics.Foreground))));
            }

            segmentStart = segmentEnd;
        }

        return overrides;
    }

    private int ToIndex(int offset) => _offsetToIndex is { } map ? map[offset] : offset;

    // the content offset of an index in Text; inside the spaces of a tab, the nearer edge of the tab
    private int ToOffset(int index)
    {
        if (_offsetToIndex is not { } map)
        {
            return index;
        }

        var low = 0;
        var high = _contentLength;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (map[mid] <= index)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        if (low < _contentLength && map[low] < index && index - map[low] > map[low + 1] - index)
        {
            low++;
        }

        return low;
    }

    private int FindPieceByIndex(int index)
    {
        var low = 0;
        var high = _pieces.Length - 1;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (_pieces[mid].Start <= index)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low;
    }

    private int FindPieceByX(double x)
    {
        var low = 0;
        var high = _pieces.Length - 1;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (_pieces[mid].Origin <= x)
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low;
    }

    private static bool IsFastPathText(string content)
    {
        foreach (var c in content)
        {
            if (!CodeEditorTextMetrics.IsFastPathCharacter(c))
            {
                return false;
            }
        }

        return true;
    }

    private static (string Text, int[]? OffsetToIndex) ExpandAsciiTabs(string content, int tabSize)
    {
        if (content.IndexOf('\t') < 0)
        {
            return (content, null);
        }

        var map = new int[content.Length + 1];
        var builder = new StringBuilder(content.Length + 16);
        for (var i = 0; i < content.Length; i++)
        {
            map[i] = builder.Length;
            if (content[i] == '\t')
            {
                builder.Append(' ', CursorColumns.NextRenderTabStop(builder.Length, tabSize) - builder.Length);
            }
            else
            {
                builder.Append(content[i]);
            }
        }

        map[content.Length] = builder.Length;
        return (builder.ToString(), map);
    }

    private static Piece[] CreateGridPieces(int length, double charWidth)
    {
        if (length <= CodeEditorTextCache.LongLineLength)
        {
            return [new Piece(0, length, 0, length * charWidth)];
        }

        var pieces = new Piece[(length + CodeEditorTextCache.ChunkLength - 1) / CodeEditorTextCache.ChunkLength];
        for (var i = 0; i < pieces.Length; i++)
        {
            var start = i * CodeEditorTextCache.ChunkLength;
            var pieceLength = Math.Min(CodeEditorTextCache.ChunkLength, length - start);
            pieces[i] = new Piece(start, pieceLength, start * charWidth, (start + pieceLength) * charWidth);
        }

        return pieces;
    }

    // One pass over the graphemes, as CursorColumns counts visible columns: expands tabs, maps offsets, and for a long
    // line starts a piece every ChunkLength code units at a grapheme boundary, at the grid x of its visible column.
    private static (string Text, int[]? OffsetToIndex, Piece[] Pieces) ExpandFallback(string content, int tabSize, double charWidth)
    {
        var hasTabs = content.IndexOf('\t') >= 0;
        var isLong = content.Length > CodeEditorTextCache.LongLineLength;
        if (!hasTabs && !isLong)
        {
            return (content, null, [new Piece(0, content.Length, 0, double.PositiveInfinity)]);
        }

        var map = hasTabs ? new int[content.Length + 1] : null;
        var builder = hasTabs ? new StringBuilder(content.Length + 16) : null;
        var starts = new List<(int Index, int VisibleColumn)> { (0, 0) };
        var iterator = new GraphemeIterator(content);
        var visibleColumn = 0;
        var index = 0;
        while (!iterator.Eol())
        {
            var offset = iterator.Offset;
            if (isLong && index - starts[^1].Index >= CodeEditorTextCache.ChunkLength)
            {
                starts.Add((index, visibleColumn));
            }

            var codePoint = Strings.GetNextCodePoint(content, content.Length, offset);
            var length = iterator.NextGraphemeLength();
            int next;
            if (codePoint == '\t')
            {
                next = CursorColumns.NextRenderTabStop(visibleColumn, tabSize);
            }
            else
            {
                next = visibleColumn + (Strings.IsFullWidthCharacter(codePoint) || Strings.IsEmojiImprecise(codePoint) ? 2 : 1);
            }

            for (var i = 0; i < length; i++)
            {
                if (map is not null)
                {
                    map[offset + i] = index + i;
                }
            }

            if (codePoint == '\t' && builder is not null)
            {
                builder.Append(' ', next - visibleColumn);
                index += next - visibleColumn;
            }
            else
            {
                builder?.Append(content, offset, length);
                index += length;
            }

            visibleColumn = next;
        }

        if (map is not null)
        {
            map[content.Length] = index;
        }

        var text = builder?.ToString() ?? content;
        var pieces = new Piece[starts.Count];
        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i].Index;
            var end = i + 1 < starts.Count ? starts[i + 1].Index : text.Length;
            var endX = i + 1 < starts.Count ? starts[i + 1].VisibleColumn * charWidth : double.PositiveInfinity;
            pieces[i] = new Piece(start, end - start, starts[i].VisibleColumn * charWidth, endX);
        }

        return (text, map, pieces);
    }

    private sealed class Piece(int start, int length, double origin, double end)
    {
        /// <summary>The first index in Text.</summary>
        public int Start { get; } = start;

        public int Length { get; } = length;

        /// <summary>The x the piece is drawn at, from the start of the text.</summary>
        public double Origin { get; } = origin;

        /// <summary>The x of the next piece (fallback: infinity for the last), to skip pieces outside the view.</summary>
        public double End { get; } = end;

        public bool IsShaped { get; set; }

        /// <summary>The glyph runs of a fast-path piece, by color.</summary>
        public List<(GlyphRun Run, int ColorId)>? Runs { get; set; }

        public TextLayout? Layout { get; set; }
    }
}
