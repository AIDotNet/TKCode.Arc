// Ported from vscode's src/vs/editor/common/model/pieceTreeTextBuffer/pieceTreeBase.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// Reading one line of PieceTreeBase: getLineContent and getLineRawContent.
//
// Difference from upstream: a line can be read as a view of the buffer it lies in (GetLineContentSpan). Upstream
// always makes a string, which for a line inside one piece is a slice of the buffer's string in JavaScript and a copy
// in .NET. GetLineRawContent therefore first finds where the line is (GetRawLine) and makes the string last.

using System.Text;

namespace TKCode.Arc.Editor.Model;

internal sealed partial class PieceTreeBase
{
    internal string GetLineContent(int lineNumber)
    {
        if (_lastVisitedLine.LineNumber == lineNumber)
        {
            return _lastVisitedLine.Value;
        }

        _lastVisitedLine.LineNumber = lineNumber;

        if (lineNumber == _lineCnt)
        {
            _lastVisitedLine.Value = GetLineRawContent(lineNumber);
        }
        else if (_eolNormalized)
        {
            _lastVisitedLine.Value = GetLineRawContent(lineNumber, _eolLength);
        }
        else
        {
            _lastVisitedLine.Value = PieceTreeStrings.TrimTrailingLineBreak(GetLineRawContent(lineNumber));
        }

        return _lastVisitedLine.Value;
    }

    /// <summary>
    /// The content of a line without its line break, like <see cref="GetLineContent"/>, but without making a string
    /// when the line lies inside one piece: the span is then a view of the backing storage. That storage never changes
    /// (original chunks are immutable and the change buffer only grows), so the span keeps showing the line as it was
    /// when it was taken.
    /// </summary>
    internal ReadOnlySpan<char> GetLineContentSpan(int lineNumber)
    {
        if (_lastVisitedLine.LineNumber == lineNumber)
        {
            return _lastVisitedLine.Value;
        }

        if (lineNumber == _lineCnt)
        {
            return GetRawLine(lineNumber, 0).AsSpan();
        }

        if (_eolNormalized)
        {
            return GetRawLine(lineNumber, _eolLength).AsSpan();
        }

        return PieceTreeStrings.TrimTrailingLineBreak(GetRawLine(lineNumber, 0).AsSpan());
    }

    internal string GetLineRawContent(int lineNumber, int endOffset = 0) => GetRawLine(lineNumber, endOffset).ToString();

    /// <summary>
    /// A line with its line break (less <paramref name="endOffset"/> characters at its end). A line inside one piece
    /// comes back as a place in that piece's buffer, so that who only reads it need not copy it; a line that runs over
    /// several pieces is put together as a string.
    /// </summary>
    private RawLine GetRawLine(int lineNumber, int endOffset)
    {
        var x = Root;

        var ret = default(RawLineBuilder);
        var cache = _searchCache.Get2(lineNumber);
        if (cache is not null)
        {
            x = cache.Node;
            var prevAccumulatedValue = GetAccumulatedValue(x, lineNumber - cache.NodeStartLineNumber - 1);
            var buffer = _buffers[x.Piece.BufferIndex];
            var startOffset = OffsetInBuffer(x.Piece.BufferIndex, x.Piece.Start);
            if (cache.NodeStartLineNumber + x.Piece.LineFeedCnt == lineNumber)
            {
                ret.Append(buffer, startOffset + prevAccumulatedValue, startOffset + x.Piece.Length);
            }
            else
            {
                var accumulatedValue = GetAccumulatedValue(x, lineNumber - cache.NodeStartLineNumber);
                return new RawLine(buffer, startOffset + prevAccumulatedValue, startOffset + accumulatedValue - endOffset);
            }
        }
        else
        {
            var nodeStartOffset = 0;
            var originalLineNumber = lineNumber;
            while (x != Sentinel)
            {
                if (x.Left != Sentinel && x.LfLeft >= lineNumber - 1)
                {
                    x = x.Left;
                }
                else if (x.LfLeft + x.Piece.LineFeedCnt > lineNumber - 1)
                {
                    var prevAccumulatedValue = GetAccumulatedValue(x, lineNumber - x.LfLeft - 2);
                    var accumulatedValue = GetAccumulatedValue(x, lineNumber - x.LfLeft - 1);
                    var buffer = _buffers[x.Piece.BufferIndex];
                    var startOffset = OffsetInBuffer(x.Piece.BufferIndex, x.Piece.Start);
                    nodeStartOffset += x.SizeLeft;
                    _searchCache.Set(new CacheEntry
                    {
                        Node = x,
                        NodeStartOffset = nodeStartOffset,
                        NodeStartLineNumber = originalLineNumber - (lineNumber - 1 - x.LfLeft),
                    });

                    return new RawLine(buffer, startOffset + prevAccumulatedValue, startOffset + accumulatedValue - endOffset);
                }
                else if (x.LfLeft + x.Piece.LineFeedCnt == lineNumber - 1)
                {
                    var prevAccumulatedValue = GetAccumulatedValue(x, lineNumber - x.LfLeft - 2);
                    var buffer = _buffers[x.Piece.BufferIndex];
                    var startOffset = OffsetInBuffer(x.Piece.BufferIndex, x.Piece.Start);

                    ret.Append(buffer, startOffset + prevAccumulatedValue, startOffset + x.Piece.Length);
                    break;
                }
                else
                {
                    lineNumber -= x.LfLeft + x.Piece.LineFeedCnt;
                    nodeStartOffset += x.SizeLeft + x.Piece.Length;
                    x = x.Right;
                }
            }
        }

        // search in order, to find the node contains end column
        x = x.Next();
        while (x != Sentinel)
        {
            var buffer = _buffers[x.Piece.BufferIndex];

            if (x.Piece.LineFeedCnt > 0)
            {
                var accumulatedValue = GetAccumulatedValue(x, 0);
                var startOffset = OffsetInBuffer(x.Piece.BufferIndex, x.Piece.Start);

                ret.Append(buffer, startOffset, startOffset + accumulatedValue - endOffset);
                return ret.ToRawLine();
            }

            var plainStartOffset = OffsetInBuffer(x.Piece.BufferIndex, x.Piece.Start);
            ret.Append(buffer, plainStartOffset, plainStartOffset + x.Piece.Length);

            x = x.Next();
        }

        return ret.ToRawLine();
    }
}
