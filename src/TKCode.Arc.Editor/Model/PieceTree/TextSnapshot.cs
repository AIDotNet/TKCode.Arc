// Not in vscode. Monaco reads models from web workers through copies of the text (mirror models); this engine reads
// them on the thread pool through a snapshot of the piece tree (design/architecture/code-editor.md section 2).

namespace TKCode.Arc.Editor.Model;

/// <summary>
/// The text of a buffer at one version, read by line or by range from any thread.
/// <para>
/// Creating it walks the piece tree once and records, for every piece, its backing buffer, where it starts there, its
/// length, its line feeds and the buffer's line start array, plus running totals. That is O(pieces) and copies no
/// text. Nothing it records changes afterwards: original chunks are immutable strings, the change buffer only appends,
/// and a buffer's line start array is replaced, never written to. So reads never look at the live tree and need no
/// lock. Finding a line or an offset is a binary search over the pieces.
/// </para>
/// </summary>
internal sealed class TextSnapshot
{
    private readonly SnapshotPiece[] _pieces;

    // _pieceStarts[i] is the document offset where piece i starts; _pieceLineFeeds[i] the line feeds before it.
    private readonly int[] _pieceStarts;
    private readonly int[] _pieceLineFeeds;

    internal TextSnapshot(SnapshotPiece[] pieces, string eol, int versionId)
    {
        _pieces = pieces;
        _pieceStarts = new int[pieces.Length];
        _pieceLineFeeds = new int[pieces.Length];
        var length = 0;
        var lineFeeds = 0;
        for (var i = 0; i < pieces.Length; i++)
        {
            _pieceStarts[i] = length;
            _pieceLineFeeds[i] = lineFeeds;
            length += pieces[i].Length;
            lineFeeds += pieces[i].LineFeedCount;
        }

        Length = length;
        LineCount = lineFeeds + 1;
        Eol = eol;
        VersionId = versionId;
    }

    /// <summary>The model's version id when the snapshot was taken (0 for a bare buffer).</summary>
    public int VersionId { get; }

    /// <summary>The end of line sequence of the text.</summary>
    public string Eol { get; }

    /// <summary>The number of characters (UTF-16 code units).</summary>
    public int Length { get; }

    public int LineCount { get; }

    /// <summary>The content of a line without its line break.</summary>
    public string GetLineContent(int lineNumber) => GetLine(lineNumber).ToString();

    /// <summary>
    /// The content of a line without its line break, as a view of the buffer when the line lies in one piece (no copy).
    /// </summary>
    public ReadOnlySpan<char> GetLineContentSpan(int lineNumber) => GetLine(lineNumber).AsSpan();

    /// <summary>
    /// <see cref="GetLineContentSpan(int)"/> for reading lines in order: <paramref name="cursor"/> remembers where the
    /// last line was, so the next one is found without a search. The cursor belongs to the caller (one per reading
    /// thread); start with <c>default</c>.
    /// </summary>
    public ReadOnlySpan<char> GetLineContentSpan(int lineNumber, ref LineCursor cursor) => GetLine(lineNumber, ref cursor).AsSpan();

    public int GetLineLength(int lineNumber) => GetLine(lineNumber).AsSpan().Length;

    /// <summary>The offset of a position (1-based line and column, column clamped to the line).</summary>
    public int GetOffsetAt(int lineNumber, int column)
    {
        var line = GetLineRange(lineNumber, out var contentEnd);
        return line + Math.Clamp(column - 1, 0, contentEnd - line);
    }

    /// <summary>The text between two positions, line breaks as they are in the buffer.</summary>
    public string GetValueInRange(TextRange range)
    {
        var start = GetOffsetAt(range.StartLineNumber, range.StartColumn);
        var end = GetOffsetAt(range.EndLineNumber, range.EndColumn);
        return GetText(start, end).ToString();
    }

    /// <summary>The whole text.</summary>
    public string GetText() => GetText(0, Length).ToString();

    private RawLine GetLine(int lineNumber)
    {
        var start = GetLineRange(lineNumber, 0, out var contentEnd, out var startPiece, out _);
        return GetText(start, contentEnd, startPiece);
    }

    private RawLine GetLine(int lineNumber, ref LineCursor cursor)
    {
        // The cursor's piece is a valid place to start only if the line's break is in it or later.
        var fromPiece = cursor.Piece < _pieces.Length && _pieceLineFeeds[cursor.Piece] < lineNumber - 1 ? cursor.Piece : 0;
        var start = GetLineRange(lineNumber, fromPiece, out var contentEnd, out var startPiece, out var breakPiece);
        cursor = new LineCursor(breakPiece);
        return GetText(start, contentEnd, startPiece);
    }

    /// <summary>The offset where a line starts; <paramref name="contentEnd"/> is where its content ends (before the break).</summary>
    private int GetLineRange(int lineNumber, out int contentEnd) => GetLineRange(lineNumber, 0, out contentEnd, out _, out _);

    /// <param name="lineNumber">The line.</param>
    /// <param name="fromPiece">Where the search for the line's start may begin.</param>
    /// <param name="contentEnd">Where the line's content ends (before its break).</param>
    /// <param name="startPiece">The piece with the break before the line.</param>
    /// <param name="breakPiece">The piece with the break after the line (the start piece for the last line).</param>
    private int GetLineRange(int lineNumber, int fromPiece, out int contentEnd, out int startPiece, out int breakPiece)
    {
        if (lineNumber < 1 || lineNumber > LineCount)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), lineNumber, "Illegal value for lineNumber");
        }

        var start = GetLineStartOffset(lineNumber, fromPiece, out startPiece);
        if (lineNumber == LineCount)
        {
            contentEnd = Length;
            breakPiece = startPiece;
            return start;
        }

        // The next line starts after the next line break, in this piece or a later one.
        var nextStart = GetLineStartOffset(lineNumber + 1, startPiece, out breakPiece);
        contentEnd = nextStart - LineBreakLengthBefore(nextStart, breakPiece);
        return start;
    }

    /// <summary>
    /// The length of the line break that ends just before <paramref name="offset"/> (2 for \r\n, else 1);
    /// <paramref name="piece"/> holds the character before the offset.
    /// </summary>
    private int LineBreakLengthBefore(int offset, int piece)
    {
        if (offset < 2 || CharBefore(offset, piece) != '\n')
        {
            return 1;
        }

        // the \r of a \r\n can be the last character of an earlier piece
        var before = offset - 1;
        while (piece > 0 && before <= _pieceStarts[piece])
        {
            piece--;
        }

        return CharBefore(before, piece) == '\r' ? 2 : 1;
    }

    /// <summary>The character just before <paramref name="offset"/>, which <paramref name="piece"/> holds.</summary>
    private char CharBefore(int offset, int piece)
    {
        ref readonly var p = ref _pieces[piece];
        return p.Buffer[p.BufferStart + offset - 1 - _pieceStarts[piece]];
    }

    /// <summary>
    /// The offset where a line starts. <paramref name="piece"/> is the piece that holds the line break before it (the
    /// first piece for line 1); the search for it starts at <paramref name="fromPiece"/>.
    /// </summary>
    private int GetLineStartOffset(int lineNumber, int fromPiece, out int piece)
    {
        if (lineNumber == 1)
        {
            piece = 0;
            return 0;
        }

        // The line starts after the (lineNumber - 1)-th line break: find the piece that holds that break, the last
        // piece with fewer line feeds before it. The next line's break is in this piece or one of the next few.
        var lineFeedIndex = lineNumber - 1;
        var p = fromPiece;
        var steps = 0;
        while (p + 1 < _pieces.Length && _pieceLineFeeds[p + 1] < lineFeedIndex && steps < 8)
        {
            p++;
            steps++;
        }

        if (p + 1 < _pieces.Length && _pieceLineFeeds[p + 1] < lineFeedIndex)
        {
            p = LastIndexBelow(_pieceLineFeeds, lineFeedIndex);
        }

        piece = p;
        ref readonly var found = ref _pieces[p];
        var breakInPiece = lineFeedIndex - _pieceLineFeeds[p];
        var bufferOffset = found.LineStarts[found.StartLine + breakInPiece];
        return _pieceStarts[p] + bufferOffset - found.BufferStart;
    }

    private RawLine GetText(int start, int end) => GetText(start, end, -1);

    /// <summary>The text between two offsets: a view of one buffer when one piece holds it, otherwise a new string.</summary>
    /// <param name="start">The first offset.</param>
    /// <param name="end">The offset after the text.</param>
    /// <param name="hint">A piece at or before the one that holds <paramref name="start"/>, or -1.</param>
    private RawLine GetText(int start, int end, int hint)
    {
        if (end <= start)
        {
            return new RawLine(string.Empty);
        }

        var p = hint >= 0 ? FindPieceAtOffsetFrom(start, hint) : FindPieceAtOffset(start);
        ref readonly var first = ref _pieces[p];
        var startInPiece = start - _pieceStarts[p];
        if (end - _pieceStarts[p] <= first.Length)
        {
            return new RawLine(first.Buffer, first.BufferStart + startInPiece, first.BufferStart + end - _pieceStarts[p]);
        }

        var result = new char[end - start];
        var written = 0;
        while (written < result.Length)
        {
            ref readonly var piece = ref _pieces[p];
            var count = Math.Min(piece.Length - startInPiece, result.Length - written);
            piece.Buffer.GetSpan(piece.BufferStart + startInPiece, count).CopyTo(result.AsSpan(written));
            written += count;
            startInPiece = 0;
            p++;
        }

        return new RawLine(new string(result));
    }

    private int FindPieceAtOffsetFrom(int offset, int hint)
    {
        var p = hint;
        while (p < _pieces.Length - 1 && offset >= _pieceStarts[p] + _pieces[p].Length)
        {
            p++;
        }

        return p;
    }

    /// <summary>The piece that holds the character at <paramref name="offset"/> (the last piece for the end).</summary>
    private int FindPieceAtOffset(int offset)
    {
        var p = LastIndexAtOrBelow(_pieceStarts, offset);
        // Skip back over empty pieces and forward to a piece that holds the offset.
        while (p < _pieces.Length - 1 && offset >= _pieceStarts[p] + _pieces[p].Length)
        {
            p++;
        }

        return p;
    }

    /// <summary>The last index whose value is below <paramref name="value"/> (values ascending, the first below it).</summary>
    private static int LastIndexBelow(int[] values, int value)
    {
        var low = 0;
        var high = values.Length - 1;
        while (low < high)
        {
            var mid = (low + high + 1) >> 1;
            if (values[mid] < value)
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

    private static int LastIndexAtOrBelow(int[] values, int value)
    {
        var low = 0;
        var high = values.Length - 1;
        while (low < high)
        {
            var mid = (low + high + 1) >> 1;
            if (values[mid] <= value)
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

    /// <summary>Where the last line read through it was; see <see cref="GetLineContentSpan(int, ref LineCursor)"/>.</summary>
    internal readonly struct LineCursor
    {
        internal LineCursor(int piece)
        {
            Piece = piece;
        }

        internal int Piece { get; }
    }

    /// <summary>What a snapshot keeps of one piece.</summary>
    internal readonly struct SnapshotPiece
    {
        public SnapshotPiece(StringBuffer buffer, int bufferStart, int length, int startLine, int lineFeedCount, int[] lineStarts)
        {
            Buffer = buffer;
            BufferStart = bufferStart;
            Length = length;
            StartLine = startLine;
            LineFeedCount = lineFeedCount;
            LineStarts = lineStarts;
        }

        public StringBuffer Buffer { get; }

        /// <summary>Where the piece starts in its buffer.</summary>
        public int BufferStart { get; }

        public int Length { get; }

        /// <summary>The buffer line the piece starts on: its k-th line break ends at <c>LineStarts[StartLine + k]</c>.</summary>
        public int StartLine { get; }

        public int LineFeedCount { get; }

        /// <summary>The buffer's line start array as it was; never written to after that.</summary>
        public int[] LineStarts { get; }
    }
}
