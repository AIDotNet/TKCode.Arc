// Ported from vscode's src/vs/editor/common/model/pieceTreeTextBuffer/pieceTreeBase.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// Node lookup, position arithmetic and tree insertion of PieceTreeBase.

namespace TKCode.Arc.Editor.Model;

internal sealed partial class PieceTreeBase
{
    // #region node operations

    private BufferCursor PositionInBuffer(TreeNode node, int remainder)
    {
        var piece = node.Piece;
        var bufferIndex = piece.BufferIndex;
        var lineStarts = _buffers[bufferIndex].LineStarts;

        var startOffset = lineStarts[piece.Start.Line] + piece.Start.Column;

        var offset = startOffset + remainder;

        // binary search offset between startOffset and endOffset
        var low = piece.Start.Line;
        var high = piece.End.Line;

        var mid = 0;
        var midStop = 0;
        var midStart = 0;

        while (low <= high)
        {
            mid = low + ((high - low) / 2);
            midStart = lineStarts[mid];

            if (mid == high)
            {
                break;
            }

            midStop = lineStarts[mid + 1];

            if (offset < midStart)
            {
                high = mid - 1;
            }
            else if (offset >= midStop)
            {
                low = mid + 1;
            }
            else
            {
                break;
            }
        }

        return new BufferCursor(mid, offset - midStart);
    }

    private int GetLineFeedCnt(int bufferIndex, BufferCursor start, BufferCursor end)
    {
        // we don't need to worry about start: abc\r|\n, or abc|\r, or abc|\n, or abc|\r\n doesn't change the fact that,
        // there is one line break after start.
        // now let's take care of end: abc\r|\n, if end is in between \r and \n, we need to add line feed count by 1
        if (end.Column == 0)
        {
            return end.Line - start.Line;
        }

        var lineStarts = _buffers[bufferIndex].LineStarts;
        if (end.Line == lineStarts.Length - 1)
        {
            // it means, there is no \n after end, otherwise, there will be one more lineStart.
            return end.Line - start.Line;
        }

        var nextLineStartOffset = lineStarts[end.Line + 1];
        var endOffset = lineStarts[end.Line] + end.Column;
        if (nextLineStartOffset > endOffset + 1)
        {
            // there are more than 1 character after end, which means it can't be \n
            return end.Line - start.Line;
        }

        // endOffset + 1 === nextLineStartOffset
        // character at endOffset is \n, so we check the character before first
        // if character at endOffset is \r, end.column is 0 and we can't get here.
        var previousCharOffset = endOffset - 1; // end.column > 0 so it's okay.
        var buffer = _buffers[bufferIndex];

        if (buffer.CharCodeAt(previousCharOffset) == 13)
        {
            return end.Line - start.Line + 1;
        }

        return end.Line - start.Line;
    }

    private int OffsetInBuffer(int bufferIndex, BufferCursor cursor)
    {
        var lineStarts = _buffers[bufferIndex].LineStarts;
        return lineStarts[cursor.Line] + cursor.Column;
    }

    private IndexResult GetIndexOf(TreeNode node, int accumulatedValue)
    {
        var piece = node.Piece;
        var pos = PositionInBuffer(node, accumulatedValue);
        var lineCnt = pos.Line - piece.Start.Line;

        if (OffsetInBuffer(piece.BufferIndex, piece.End) - OffsetInBuffer(piece.BufferIndex, piece.Start) == accumulatedValue)
        {
            // we are checking the end of this node, so a CRLF check is necessary.
            var realLineCnt = GetLineFeedCnt(node.Piece.BufferIndex, piece.Start, pos);
            if (realLineCnt != lineCnt)
            {
                // aha yes, CRLF
                return new IndexResult(realLineCnt, 0);
            }
        }

        return new IndexResult(lineCnt, pos.Column);
    }

    private int GetAccumulatedValue(TreeNode node, int index)
    {
        if (index < 0)
        {
            return 0;
        }

        var piece = node.Piece;
        var lineStarts = _buffers[piece.BufferIndex].LineStarts;
        var expectedLineStartIndex = piece.Start.Line + index + 1;
        if (expectedLineStartIndex > piece.End.Line)
        {
            return lineStarts[piece.End.Line] + piece.End.Column - lineStarts[piece.Start.Line] - piece.Start.Column;
        }

        return lineStarts[expectedLineStartIndex] - lineStarts[piece.Start.Line] - piece.Start.Column;
    }

    private NodePosition? NodeAt(int offset)
    {
        var x = Root;
        var cache = _searchCache.Get(offset);
        if (cache is not null)
        {
            return new NodePosition(cache.Node, offset - cache.NodeStartOffset, cache.NodeStartOffset);
        }

        var nodeStartOffset = 0;

        while (x != Sentinel)
        {
            if (x.SizeLeft > offset)
            {
                x = x.Left;
            }
            else if (x.SizeLeft + x.Piece.Length >= offset)
            {
                nodeStartOffset += x.SizeLeft;
                var ret = new NodePosition(x, offset - x.SizeLeft, nodeStartOffset);
                _searchCache.Set(new CacheEntry
                {
                    Node = x,
                    NodeStartOffset = nodeStartOffset,
                });
                return ret;
            }
            else
            {
                offset -= x.SizeLeft + x.Piece.Length;
                nodeStartOffset += x.SizeLeft + x.Piece.Length;
                x = x.Right;
            }
        }

        return null;
    }

    private NodePosition? NodeAt2(int lineNumber, int column)
    {
        var x = Root;
        var nodeStartOffset = 0;

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
                nodeStartOffset += x.SizeLeft;

                return new NodePosition(
                    x,
                    Math.Min(prevAccumulatedValue + column - 1, accumulatedValue),
                    nodeStartOffset);
            }
            else if (x.LfLeft + x.Piece.LineFeedCnt == lineNumber - 1)
            {
                var prevAccumulatedValue = GetAccumulatedValue(x, lineNumber - x.LfLeft - 2);
                if (prevAccumulatedValue + column - 1 <= x.Piece.Length)
                {
                    return new NodePosition(
                        x,
                        prevAccumulatedValue + column - 1,
                        nodeStartOffset);
                }

                column -= x.Piece.Length - prevAccumulatedValue;
                break;
            }
            else
            {
                lineNumber -= x.LfLeft + x.Piece.LineFeedCnt;
                nodeStartOffset += x.SizeLeft + x.Piece.Length;
                x = x.Right;
            }
        }

        // search in order, to find the node contains position.column
        x = x.Next();
        while (x != Sentinel)
        {
            if (x.Piece.LineFeedCnt > 0)
            {
                var accumulatedValue = GetAccumulatedValue(x, 0);
                var foundOffset = OffsetOfNode(x);
                return new NodePosition(
                    x,
                    Math.Min(column - 1, accumulatedValue),
                    foundOffset);
            }

            if (x.Piece.Length >= column - 1)
            {
                var foundOffset = OffsetOfNode(x);
                return new NodePosition(
                    x,
                    column - 1,
                    foundOffset);
            }

            column -= x.Piece.Length;

            x = x.Next();
        }

        return null;
    }

    private int NodeCharCodeAt(TreeNode node, int offset)
    {
        if (node.Piece.LineFeedCnt < 1)
        {
            return -1;
        }

        var buffer = _buffers[node.Piece.BufferIndex];
        var newOffset = OffsetInBuffer(node.Piece.BufferIndex, node.Piece.Start) + offset;
        return buffer.CharCodeAt(newOffset);
    }

    private int OffsetOfNode(TreeNode? node)
    {
        if (node is null || node == Sentinel)
        {
            return 0;
        }

        var pos = node.SizeLeft;
        while (node != Root)
        {
            if (node.Parent.Right == node)
            {
                pos += node.Parent.SizeLeft + node.Parent.Piece.Length;
            }

            node = node.Parent;
        }

        return pos;
    }

    // #endregion

    // #region Tree operations

    /// <summary>
    ///      node              node
    ///     /  \              /  \
    ///    a   b    &lt;----   a    b
    ///                         /
    ///                        z
    /// </summary>
    private TreeNode RbInsertRight(TreeNode? node, Piece p)
    {
        var z = new TreeNode(p, NodeColor.Red, Sentinel);

        var x = Root;
        if (x == Sentinel)
        {
            Root = z;
            z.Color = NodeColor.Black;
        }
        else if (node!.Right == Sentinel)
        {
            node!.Right = z;
            z.Parent = node!;
        }
        else
        {
            var nextNode = RbTreeBase.Leftest(node!.Right);
            nextNode.Left = z;
            z.Parent = nextNode;
        }

        RbTreeBase.FixInsert(this, z);
        return z;
    }

    /// <summary>
    ///      node              node
    ///     /  \              /  \
    ///    a   b     ----&gt;   a    b
    ///                       \
    ///                        z
    /// </summary>
    private TreeNode RbInsertLeft(TreeNode? node, Piece p)
    {
        var z = new TreeNode(p, NodeColor.Red, Sentinel);

        if (Root == Sentinel)
        {
            Root = z;
            z.Color = NodeColor.Black;
        }
        else if (node!.Left == Sentinel)
        {
            node!.Left = z;
            z.Parent = node!;
        }
        else
        {
            var prevNode = RbTreeBase.Righttest(node!.Left); // a
            prevNode.Right = z;
            z.Parent = prevNode;
        }

        RbTreeBase.FixInsert(this, z);
        return z;
    }

    // #endregion
}
