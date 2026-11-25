// Ported from vscode's src/vs/editor/common/model/pieceTreeTextBuffer/pieceTreeBase.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
//
// Adaptations:
//  - the search methods (findMatchesLineByLine / findMatchesInNode / findMatchesInLine) are not ported;
//  - the sentinel belongs to the tree instance instead of being one module-level node;
//  - buffer 0 (the change buffer) is an append-only char store instead of a growing string, and snapshots capture the
//    buffer reference, start offset and length of every piece so they can be read while the tree is edited;
//  - offsets and line starts are int (UTF-16 code units, like JavaScript strings).

using System.Text;

namespace TKCode.Arc.Editor.Model;

internal readonly struct BufferCursor
{
    internal BufferCursor(int line, int column)
    {
        Line = line;
        Column = column;
    }

    /// <summary>Line number in the current buffer.</summary>
    internal int Line { get; }

    /// <summary>Column number in the current buffer.</summary>
    internal int Column { get; }
}

internal readonly struct NodePosition
{
    internal NodePosition(TreeNode node, int remainder, int nodeStartOffset)
    {
        Node = node;
        Remainder = remainder;
        NodeStartOffset = nodeStartOffset;
    }

    /// <summary>The node of the piece.</summary>
    internal TreeNode Node { get; }

    /// <summary>Remainder in the current piece.</summary>
    internal int Remainder { get; }

    /// <summary>Node start offset in the document.</summary>
    internal int NodeStartOffset { get; }
}

internal readonly struct IndexResult
{
    internal IndexResult(int index, int remainder)
    {
        Index = index;
        Remainder = remainder;
    }

    internal int Index { get; }

    internal int Remainder { get; }
}

internal sealed class LineStarts
{
    internal LineStarts(int[] lineStartsArray, int cr, int lf, int crlf, bool isBasicAscii)
    {
        LineStartsArray = lineStartsArray;
        Cr = cr;
        Lf = lf;
        Crlf = crlf;
        IsBasicAscii = isBasicAscii;
    }

    internal int[] LineStartsArray { get; }

    internal int Cr { get; }

    internal int Lf { get; }

    internal int Crlf { get; }

    internal bool IsBasicAscii { get; }
}

internal sealed class Piece
{
    internal Piece(int bufferIndex, BufferCursor start, BufferCursor end, int lineFeedCnt, int length)
    {
        BufferIndex = bufferIndex;
        Start = start;
        End = end;
        LineFeedCnt = lineFeedCnt;
        Length = length;
    }

    internal int BufferIndex { get; }

    internal BufferCursor Start { get; }

    internal BufferCursor End { get; }

    internal int LineFeedCnt { get; }

    internal int Length { get; }
}

/// <summary>
/// An append-only char store: once a character has been published at an index it never changes, so a snapshot may read
/// it from another thread while the tree keeps appending. Growing replaces the array with a larger copy.
/// </summary>
internal sealed class AppendOnlyCharBuffer
{
    private char[] _buffer;
    private int _length;

    internal AppendOnlyCharBuffer(int capacity = 1024)
    {
        _buffer = new char[Math.Max(1, capacity)];
    }

    internal int Length => Volatile.Read(ref _length);

    internal char this[int index] => Volatile.Read(ref _buffer)[index];

    internal ReadOnlySpan<char> GetSpan(int start, int length) =>
        new(Volatile.Read(ref _buffer), start, length);

    internal void Append(string value)
    {
        if (value.Length == 0)
        {
            return;
        }

        var length = _length;
        EnsureCapacity(length + value.Length);
        value.CopyTo(0, _buffer, length, value.Length);
        Volatile.Write(ref _length, length + value.Length);
    }

    internal void Append(char value)
    {
        var length = _length;
        EnsureCapacity(length + 1);
        _buffer[length] = value;
        Volatile.Write(ref _length, length + 1);
    }

    private void EnsureCapacity(int required)
    {
        if (required <= _buffer.Length)
        {
            return;
        }

        var newCapacity = Math.Max(required, _buffer.Length * 2);
        var newBuffer = new char[newCapacity];
        Array.Copy(_buffer, newBuffer, _length);
        Volatile.Write(ref _buffer, newBuffer);
    }
}

/// <summary>A backing store of pieces: an immutable original chunk, or the tree's change buffer.</summary>
internal sealed class StringBuffer
{
    private string? _text;
    private readonly AppendOnlyCharBuffer? _changeBuffer;

    internal StringBuffer(string buffer, int[] lineStarts)
    {
        _text = buffer;
        LineStarts = lineStarts;
    }

    internal StringBuffer(AppendOnlyCharBuffer changeBuffer, int[] lineStarts)
    {
        _changeBuffer = changeBuffer;
        LineStarts = lineStarts;
    }

    /// <summary>The text of an original chunk. Mutated only by the builder, before any tree owns the buffer.</summary>
    internal string Text
    {
        get => _text ?? throw new InvalidOperationException("The change buffer has no immutable text.");
        set => _text = value;
    }

    internal bool IsChangeBuffer => _changeBuffer is not null;

    internal int Length => _changeBuffer is not null ? _changeBuffer.Length : _text!.Length;

    internal int[] LineStarts { get; set; }

    internal char this[int index] => _changeBuffer is not null ? _changeBuffer[index] : _text![index];

    internal int CharCodeAt(int index)
    {
        if (index < 0 || index >= Length)
        {
            return -1;
        }

        return this[index];
    }

    /// <summary>
    /// Returns a span over the backing storage. Original chunks are immutable strings; the change buffer is append-only,
    /// so a span over characters that were already published stays valid while the tree keeps appending.
    /// </summary>
    internal ReadOnlySpan<char> GetSpan(int start, int length) =>
        _changeBuffer is not null ? _changeBuffer.GetSpan(start, length) : _text!.AsSpan(start, length);

    /// <summary>Returns the text between <paramref name="start"/> and <paramref name="end"/> (end-exclusive).</summary>
    internal string Substring(int start, int end)
    {
        var slice = Slice(start, end);
        return slice.IsEmpty ? string.Empty : new string(slice);
    }

    /// <summary>
    /// The text between <paramref name="start"/> and <paramref name="end"/> (end-exclusive) as a view of the backing
    /// storage, with the index rules of <see cref="Substring"/>. Nothing is copied: JavaScript's <c>substring</c> is a
    /// slice of its parent string, and this is its equivalent for code that only reads the text.
    /// </summary>
    internal ReadOnlySpan<char> Slice(int start, int end)
    {
        if (start > end)
        {
            (start, end) = (end, start);
        }

        start = Math.Max(0, start);
        end = Math.Min(end, Length);
        if (end <= start)
        {
            return ReadOnlySpan<char>.Empty;
        }

        return GetSpan(start, end - start);
    }

    internal void Append(string value) => _changeBuffer!.Append(value);

    internal void Append(char value) => _changeBuffer!.Append(value);

    internal bool EndsWithCr =>
        _changeBuffer is not null
        && _changeBuffer.Length > 0
        && _changeBuffer[_changeBuffer.Length - 1] == PieceTreeStrings.CarriageReturn;

    /// <summary>Appends the line starts of another chunk, leaving out its leading 0.</summary>
    internal void AppendLineStarts(int[] starts, bool skipFirst)
    {
        var skip = skipFirst ? 1 : 0;
        var added = starts.Length - skip;
        if (added <= 0)
        {
            return;
        }

        var merged = new int[LineStarts.Length + added];
        Array.Copy(LineStarts, merged, LineStarts.Length);
        Array.Copy(starts, skip, merged, LineStarts.Length, added);
        LineStarts = merged;
    }

    internal void RemoveLastLineStart()
    {
        if (LineStarts.Length == 0)
        {
            return;
        }

        var shortened = new int[LineStarts.Length - 1];
        Array.Copy(LineStarts, shortened, shortened.Length);
        LineStarts = shortened;
    }
}

internal readonly struct SnapshotPiece
{
    internal SnapshotPiece(StringBuffer buffer, int start, int length)
    {
        Buffer = buffer;
        Start = start;
        Length = length;
    }

    internal StringBuffer Buffer { get; }

    internal int Start { get; }

    internal int Length { get; }
}

/// <summary>
/// Readonly snapshot of a piece tree. Everything it reads (the backing buffer of each piece, the piece's start offset
/// and length) is captured when the snapshot is created; reading never looks at the live tree again.
/// </summary>
internal sealed class PieceTreeSnapshot : ITextSnapshot
{
    private readonly List<SnapshotPiece> _pieces;
    private readonly string _bom;
    private int _index;

    internal PieceTreeSnapshot(PieceTreeBase tree, string bom)
    {
        _pieces = new List<SnapshotPiece>();
        _bom = bom;
        _index = 0;
        tree.CollectSnapshotPieces(_pieces);
    }

    public string? Read()
    {
        if (_pieces.Count == 0)
        {
            if (_index == 0)
            {
                _index++;
                return _bom;
            }

            return null;
        }

        if (_index > _pieces.Count - 1)
        {
            return null;
        }

        var piece = _pieces[_index];
        var content = piece.Buffer.Substring(piece.Start, piece.Start + piece.Length);
        if (_index == 0)
        {
            _index++;
            return _bom + content;
        }

        _index++;
        return content;
    }
}

internal sealed class CacheEntry
{
    internal TreeNode Node = null!;
    internal int NodeStartOffset;
    internal int NodeStartLineNumber;
}

internal sealed class PieceTreeSearchCache
{
    private readonly int _limit;
    private List<CacheEntry> _cache;

    internal PieceTreeSearchCache(int limit)
    {
        _limit = limit;
        _cache = new List<CacheEntry>();
    }

    internal CacheEntry? Get(int offset)
    {
        for (var i = _cache.Count - 1; i >= 0; i--)
        {
            var nodePos = _cache[i];
            if (nodePos.NodeStartOffset <= offset
                && nodePos.NodeStartOffset + nodePos.Node.Piece.Length >= offset)
            {
                return nodePos;
            }
        }

        return null;
    }

    internal CacheEntry? Get2(int lineNumber)
    {
        for (var i = _cache.Count - 1; i >= 0; i--)
        {
            var nodePos = _cache[i];
            if (nodePos.NodeStartLineNumber > 0
                && nodePos.NodeStartLineNumber < lineNumber
                && nodePos.NodeStartLineNumber + nodePos.Node.Piece.LineFeedCnt >= lineNumber)
            {
                return nodePos;
            }
        }

        return null;
    }

    internal void Set(CacheEntry nodePosition)
    {
        if (_cache.Count >= _limit)
        {
            _cache.RemoveAt(0);
        }

        _cache.Add(nodePosition);
    }

    internal void Validate(int offset)
    {
        var hasInvalidValue = false;
        var tmp = _cache;
        for (var i = 0; i < tmp.Count; i++)
        {
            var nodePos = tmp[i];
            if (nodePos.Node.Parent is null || nodePos.NodeStartOffset >= offset)
            {
                tmp[i] = null!;
                hasInvalidValue = true;
            }
        }

        if (hasInvalidValue)
        {
            var newArray = new List<CacheEntry>(tmp.Count);
            foreach (var entry in tmp)
            {
                if (entry is not null)
                {
                    newArray.Add(entry);
                }
            }

            _cache = newArray;
        }
    }
}

internal sealed partial class PieceTreeBase
{
    internal const int AverageBufferSize = 65535;

    internal TreeNode Root;
    internal TreeNode Sentinel { get; }

    private List<StringBuffer> _buffers = null!; // 0 is change buffer, others are readonly original buffer.
    private int _lineCnt;
    private int _length;
    private string _eol = "\n";
    private int _eolLength;
    private bool _eolNormalized;
    private BufferCursor _lastChangeBufferPos;
    private PieceTreeSearchCache _searchCache = null!;
    private LastVisitedLine _lastVisitedLine;

    internal PieceTreeBase(List<StringBuffer> chunks, string eol, bool eolNormalized)
    {
        Sentinel = RbTreeBase.CreateSentinel();
        Root = Sentinel;
        Create(chunks, eol, eolNormalized);
    }

    private struct LastVisitedLine
    {
        internal int LineNumber;
        internal string Value;
    }

    internal static int[] CreateLineStartsFast(string str, bool readOnly = true)
    {
        _ = readOnly;
        var r = new List<int>(16) { 0 };

        for (int i = 0, len = str.Length; i < len; i++)
        {
            var chr = str[i];

            if (chr == PieceTreeStrings.CarriageReturn)
            {
                if (i + 1 < len && str[i + 1] == PieceTreeStrings.LineFeed)
                {
                    // \r\n... case
                    r.Add(i + 2);
                    i++; // skip \n
                }
                else
                {
                    // \r... case
                    r.Add(i + 1);
                }
            }
            else if (chr == PieceTreeStrings.LineFeed)
            {
                r.Add(i + 1);
            }
        }

        return r.ToArray();
    }

    internal static LineStarts CreateLineStarts(string str)
    {
        var r = new List<int>(16) { 0 };
        var cr = 0;
        var lf = 0;
        var crlf = 0;
        var isBasicAscii = true;

        for (int i = 0, len = str.Length; i < len; i++)
        {
            var chr = str[i];

            if (chr == PieceTreeStrings.CarriageReturn)
            {
                if (i + 1 < len && str[i + 1] == PieceTreeStrings.LineFeed)
                {
                    // \r\n... case
                    crlf++;
                    r.Add(i + 2);
                    i++; // skip \n
                }
                else
                {
                    cr++;
                    // \r... case
                    r.Add(i + 1);
                }
            }
            else if (chr == PieceTreeStrings.LineFeed)
            {
                lf++;
                r.Add(i + 1);
            }
            else if (isBasicAscii)
            {
                if (chr != '\t' && (chr < 32 || chr > 126))
                {
                    isBasicAscii = false;
                }
            }
        }

        return new LineStarts(r.ToArray(), cr, lf, crlf, isBasicAscii);
    }

    private void Create(List<StringBuffer> chunks, string eol, bool eolNormalized)
    {
        _buffers = new List<StringBuffer>
        {
            new(new AppendOnlyCharBuffer(), new[] { 0 }),
        };
        _lastChangeBufferPos = new BufferCursor(0, 0);
        Root = Sentinel;
        _lineCnt = 1;
        _length = 0;
        _eol = eol;
        _eolLength = eol.Length;
        _eolNormalized = eolNormalized;

        TreeNode? lastNode = null;
        for (int i = 0, len = chunks.Count; i < len; i++)
        {
            var chunk = chunks[i];
            if (chunk.Length > 0)
            {
                if (chunk.LineStarts.Length == 0)
                {
                    chunk.LineStarts = CreateLineStartsFast(chunk.Text);
                }

                var lineStarts = chunk.LineStarts;
                var piece = new Piece(
                    i + 1,
                    new BufferCursor(0, 0),
                    new BufferCursor(lineStarts.Length - 1, chunk.Length - lineStarts[lineStarts.Length - 1]),
                    lineStarts.Length - 1,
                    chunk.Length);
                _buffers.Add(chunk);
                lastNode = RbInsertRight(lastNode, piece);
            }
        }

        _searchCache = new PieceTreeSearchCache(1);
        _lastVisitedLine = new LastVisitedLine { LineNumber = 0, Value = string.Empty };
        ComputeBufferMetadata();
    }

    internal void NormalizeEol(string eol)
    {
        const int averageBufferSize = AverageBufferSize;
        var min = averageBufferSize - (averageBufferSize / 3);
        var max = min * 2;

        var tempChunk = new StringBuilder();
        var tempChunkLen = 0;
        var chunks = new List<StringBuffer>();

        Iterate(Root, node =>
        {
            var str = GetNodeContent(node);
            var len = str.Length;
            if (tempChunkLen <= min || tempChunkLen + len < max)
            {
                tempChunk.Append(str);
                tempChunkLen += len;
                return true;
            }

            // flush anyways
            var text = PieceTreeStrings.ReplaceLineBreaks(tempChunk.ToString(), eol);
            chunks.Add(new StringBuffer(text, CreateLineStartsFast(text)));
            tempChunk.Clear();
            tempChunk.Append(str);
            tempChunkLen = len;
            return true;
        });

        if (tempChunkLen > 0)
        {
            var text = PieceTreeStrings.ReplaceLineBreaks(tempChunk.ToString(), eol);
            chunks.Add(new StringBuffer(text, CreateLineStartsFast(text)));
        }

        Create(chunks, eol, true);
    }

    internal string GetEol() => _eol;

    internal void SetEol(string newEol)
    {
        _eol = newEol;
        _eolLength = _eol.Length;
        NormalizeEol(newEol);
    }

    internal ITextSnapshot CreateSnapshot(string bom) => new PieceTreeSnapshot(this, bom);

    internal bool Equal(PieceTreeBase other)
    {
        if (GetLength() != other.GetLength())
        {
            return false;
        }

        if (GetLineCount() != other.GetLineCount())
        {
            return false;
        }

        var offset = 0;
        var ret = Iterate(Root, node =>
        {
            if (node == Sentinel)
            {
                return true;
            }

            var str = GetNodeContent(node);
            var len = str.Length;
            var startPosition = other.NodeAt(offset);
            var endPosition = other.NodeAt(offset + len);
            if (startPosition is null || endPosition is null)
            {
                return false;
            }

            var val = other.GetValueInRange2(startPosition.Value, endPosition.Value);

            offset += len;
            return str == val;
        });

        return ret;
    }

    internal int GetOffsetAt(int lineNumber, int column)
    {
        var leftLen = 0; // inorder

        var x = Root;

        while (x != Sentinel)
        {
            if (x.Left != Sentinel && x.LfLeft + 1 >= lineNumber)
            {
                x = x.Left;
            }
            else if (x.LfLeft + x.Piece.LineFeedCnt + 1 >= lineNumber)
            {
                leftLen += x.SizeLeft;
                // lineNumber >= 2
                var accumulatedValueInCurrentIndex = GetAccumulatedValue(x, lineNumber - x.LfLeft - 2);
                return leftLen + accumulatedValueInCurrentIndex + column - 1;
            }
            else
            {
                lineNumber -= x.LfLeft + x.Piece.LineFeedCnt;
                leftLen += x.SizeLeft + x.Piece.Length;
                x = x.Right;
            }
        }

        return leftLen;
    }

    internal TextPosition GetPositionAt(int offset)
    {
        offset = Math.Max(0, offset);

        var x = Root;
        var lfCnt = 0;
        var originalOffset = offset;

        while (x != Sentinel)
        {
            if (x.SizeLeft != 0 && x.SizeLeft >= offset)
            {
                x = x.Left;
            }
            else if (x.SizeLeft + x.Piece.Length >= offset)
            {
                var index = GetIndexOf(x, offset - x.SizeLeft);

                lfCnt += x.LfLeft + index.Index;

                if (index.Index == 0)
                {
                    var lineStartOffset = GetOffsetAt(lfCnt + 1, 1);
                    var column = originalOffset - lineStartOffset;
                    return new TextPosition(lfCnt + 1, column + 1);
                }

                return new TextPosition(lfCnt + 1, index.Remainder + 1);
            }
            else
            {
                offset -= x.SizeLeft + x.Piece.Length;
                lfCnt += x.LfLeft + x.Piece.LineFeedCnt;

                if (x.Right == Sentinel)
                {
                    // last node
                    var lineStartOffset = GetOffsetAt(lfCnt + 1, 1);
                    var column = originalOffset - offset - lineStartOffset;
                    return new TextPosition(lfCnt + 1, column + 1);
                }

                x = x.Right;
            }
        }

        return new TextPosition(1, 1);
    }

    internal string GetValueInRange(TextRange range, string? eol = null)
    {
        if (range.StartLineNumber == range.EndLineNumber && range.StartColumn == range.EndColumn)
        {
            return string.Empty;
        }

        var startPosition = NodeAt2(range.StartLineNumber, range.StartColumn)!.Value;
        var endPosition = NodeAt2(range.EndLineNumber, range.EndColumn)!.Value;

        var value = GetValueInRange2(startPosition, endPosition);
        if (eol is not null)
        {
            if (eol != _eol || !_eolNormalized)
            {
                return PieceTreeStrings.ReplaceLineBreaks(value, eol);
            }

            if (eol == GetEol() && _eolNormalized)
            {
                return value;
            }

            return PieceTreeStrings.ReplaceLineBreaks(value, eol);
        }

        return value;
    }

    internal string GetValueInRange2(NodePosition startPosition, NodePosition endPosition)
    {
        if (startPosition.Node == endPosition.Node)
        {
            var node = startPosition.Node;
            var buffer = _buffers[node.Piece.BufferIndex];
            var startOffset = OffsetInBuffer(node.Piece.BufferIndex, node.Piece.Start);
            return buffer.Substring(startOffset + startPosition.Remainder, startOffset + endPosition.Remainder);
        }

        var x = startPosition.Node;
        var firstBuffer = _buffers[x.Piece.BufferIndex];
        var firstStartOffset = OffsetInBuffer(x.Piece.BufferIndex, x.Piece.Start);
        var ret = new StringBuilder();
        ret.Append(firstBuffer.Substring(firstStartOffset + startPosition.Remainder, firstStartOffset + x.Piece.Length));

        x = x.Next();
        while (x != Sentinel)
        {
            var buffer = _buffers[x.Piece.BufferIndex];
            var startOffset = OffsetInBuffer(x.Piece.BufferIndex, x.Piece.Start);

            if (x == endPosition.Node)
            {
                ret.Append(buffer.Substring(startOffset, startOffset + endPosition.Remainder));
                break;
            }

            ret.Append(buffer.Substring(startOffset, startOffset + x.Piece.Length));

            x = x.Next();
        }

        return ret.ToString();
    }

    internal string[] GetLinesContent()
    {
        var lines = new List<string>();
        var currentLine = new StringBuilder();
        var danglingCr = false;

        Iterate(Root, node =>
        {
            if (node == Sentinel)
            {
                return true;
            }

            var piece = node.Piece;
            var pieceLength = piece.Length;
            if (pieceLength == 0)
            {
                return true;
            }

            var buffer = _buffers[piece.BufferIndex];
            var lineStarts = buffer.LineStarts;

            var pieceStartLine = piece.Start.Line;
            var pieceEndLine = piece.End.Line;
            var pieceStartOffset = lineStarts[pieceStartLine] + piece.Start.Column;

            if (danglingCr)
            {
                if (buffer.CharCodeAt(pieceStartOffset) == PieceTreeStrings.LineFeed)
                {
                    // pretend the \n was in the previous piece..
                    pieceStartOffset++;
                    pieceLength--;
                }

                lines.Add(currentLine.ToString());
                currentLine.Clear();
                danglingCr = false;
                if (pieceLength == 0)
                {
                    return true;
                }
            }

            if (pieceStartLine == pieceEndLine)
            {
                // this piece has no new lines
                if (!_eolNormalized && buffer.CharCodeAt(pieceStartOffset + pieceLength - 1) == PieceTreeStrings.CarriageReturn)
                {
                    danglingCr = true;
                    currentLine.Append(buffer.Substring(pieceStartOffset, pieceStartOffset + pieceLength - 1));
                }
                else
                {
                    currentLine.Append(buffer.Substring(pieceStartOffset, pieceStartOffset + pieceLength));
                }

                return true;
            }

            // add the text before the first line start in this piece
            currentLine.Append(_eolNormalized
                ? buffer.Substring(pieceStartOffset, Math.Max(pieceStartOffset, lineStarts[pieceStartLine + 1] - _eolLength))
                : PieceTreeStrings.TrimTrailingLineBreak(buffer.Substring(pieceStartOffset, lineStarts[pieceStartLine + 1])));
            lines.Add(currentLine.ToString());

            for (var line = pieceStartLine + 1; line < pieceEndLine; line++)
            {
                currentLine.Clear();
                currentLine.Append(_eolNormalized
                    ? buffer.Substring(lineStarts[line], lineStarts[line + 1] - _eolLength)
                    : PieceTreeStrings.TrimTrailingLineBreak(buffer.Substring(lineStarts[line], lineStarts[line + 1])));
                lines.Add(currentLine.ToString());
            }

            if (!_eolNormalized && buffer.CharCodeAt(lineStarts[pieceEndLine] + piece.End.Column - 1) == PieceTreeStrings.CarriageReturn)
            {
                danglingCr = true;
                if (piece.End.Column == 0)
                {
                    // The last line ended with a \r, let's undo the push, it will be pushed by next iteration
                    lines.RemoveAt(lines.Count - 1);
                }
                else
                {
                    currentLine.Clear();
                    currentLine.Append(buffer.Substring(lineStarts[pieceEndLine], lineStarts[pieceEndLine] + piece.End.Column - 1));
                }
            }
            else
            {
                currentLine.Clear();
                currentLine.Append(buffer.Substring(lineStarts[pieceEndLine], lineStarts[pieceEndLine] + piece.End.Column));
            }

            return true;
        });

        if (danglingCr)
        {
            lines.Add(currentLine.ToString());
            currentLine.Clear();
        }

        lines.Add(currentLine.ToString());
        return lines.ToArray();
    }

    internal int GetLength() => _length;

    internal int GetLineCount() => _lineCnt;

    private int GetCharCodeCore(NodePosition nodePos)
    {
        if (nodePos.Remainder == nodePos.Node.Piece.Length)
        {
            // the char we want to fetch is at the head of next node.
            var matchingNode = nodePos.Node.Next();
            if (matchingNode == Sentinel)
            {
                return 0;
            }

            var matchingBuffer = _buffers[matchingNode.Piece.BufferIndex];
            var matchingStartOffset = OffsetInBuffer(matchingNode.Piece.BufferIndex, matchingNode.Piece.Start);
            return matchingBuffer.CharCodeAt(matchingStartOffset);
        }

        var buffer = _buffers[nodePos.Node.Piece.BufferIndex];
        var startOffset = OffsetInBuffer(nodePos.Node.Piece.BufferIndex, nodePos.Node.Piece.Start);
        var targetOffset = startOffset + nodePos.Remainder;

        return buffer.CharCodeAt(targetOffset);
    }

    internal int GetLineCharCode(int lineNumber, int index)
    {
        var nodePos = NodeAt2(lineNumber, index + 1)!.Value;
        return GetCharCodeCore(nodePos);
    }

    internal int GetLineLength(int lineNumber)
    {
        if (lineNumber == GetLineCount())
        {
            var startOffset = GetOffsetAt(lineNumber, 1);
            return GetLength() - startOffset;
        }

        return GetOffsetAt(lineNumber + 1, 1) - GetOffsetAt(lineNumber, 1) - _eolLength;
    }

    internal int GetCharCode(int offset)
    {
        var nodePos = NodeAt(offset)!.Value;
        return GetCharCodeCore(nodePos);
    }

    internal string GetNearestChunk(int offset)
    {
        var nodePos = NodeAt(offset)!.Value;
        if (nodePos.Remainder == nodePos.Node.Piece.Length)
        {
            // the offset is at the head of next node.
            var matchingNode = nodePos.Node.Next();
            if (matchingNode == Sentinel)
            {
                return string.Empty;
            }

            var matchingBuffer = _buffers[matchingNode.Piece.BufferIndex];
            var matchingStartOffset = OffsetInBuffer(matchingNode.Piece.BufferIndex, matchingNode.Piece.Start);
            return matchingBuffer.Substring(matchingStartOffset, matchingStartOffset + matchingNode.Piece.Length);
        }

        var buffer = _buffers[nodePos.Node.Piece.BufferIndex];
        var startOffset = OffsetInBuffer(nodePos.Node.Piece.BufferIndex, nodePos.Node.Piece.Start);
        var targetOffset = startOffset + nodePos.Remainder;
        var targetEnd = startOffset + nodePos.Node.Piece.Length;
        return buffer.Substring(targetOffset, targetEnd);
    }

    internal string GetLinesRawContent() => GetContentOfSubTree(Root);

    private void ComputeBufferMetadata()
    {
        var x = Root;

        var lfCnt = 1;
        var len = 0;

        while (x != Sentinel)
        {
            lfCnt += x.LfLeft + x.Piece.LineFeedCnt;
            len += x.SizeLeft + x.Piece.Length;
            x = x.Right;
        }

        _lineCnt = lfCnt;
        _length = len;
        _searchCache.Validate(_length);
    }

    internal bool Iterate(TreeNode node, Func<TreeNode, bool> callback)
    {
        if (node == Sentinel)
        {
            return callback(Sentinel);
        }

        var leftRet = Iterate(node.Left, callback);
        if (!leftRet)
        {
            return leftRet;
        }

        return callback(node) && Iterate(node.Right, callback);
    }

    private string GetNodeContent(TreeNode node)
    {
        if (node == Sentinel)
        {
            return string.Empty;
        }

        var buffer = _buffers[node.Piece.BufferIndex];
        var piece = node.Piece;
        var startOffset = OffsetInBuffer(piece.BufferIndex, piece.Start);
        var endOffset = OffsetInBuffer(piece.BufferIndex, piece.End);
        return buffer.Substring(startOffset, endOffset);
    }

    internal string GetPieceContent(Piece piece)
    {
        var buffer = _buffers[piece.BufferIndex];
        var startOffset = OffsetInBuffer(piece.BufferIndex, piece.Start);
        var endOffset = OffsetInBuffer(piece.BufferIndex, piece.End);
        return buffer.Substring(startOffset, endOffset);
    }

    internal void CollectSnapshotPieces(List<SnapshotPiece> pieces) => CollectSnapshotPieces(Root, pieces);

    private void CollectSnapshotPieces(TreeNode node, List<SnapshotPiece> pieces)
    {
        if (node == Sentinel)
        {
            return;
        }

        CollectSnapshotPieces(node.Left, pieces);

        var piece = node.Piece;
        var buffer = _buffers[piece.BufferIndex];
        var startOffset = OffsetInBuffer(piece.BufferIndex, piece.Start);
        var endOffset = OffsetInBuffer(piece.BufferIndex, piece.End);
        pieces.Add(new SnapshotPiece(buffer, startOffset, endOffset - startOffset));

        CollectSnapshotPieces(node.Right, pieces);
    }

    private string GetContentOfSubTree(TreeNode node)
    {
        var str = new StringBuilder();

        Iterate(node, current =>
        {
            str.Append(GetNodeContent(current));
            return true;
        });

        return str.ToString();
    }
}
