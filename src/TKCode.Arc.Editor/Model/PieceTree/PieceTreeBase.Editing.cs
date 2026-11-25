// Ported from vscode's src/vs/editor/common/model/pieceTreeTextBuffer/pieceTreeBase.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// Editing and CRLF handling half of PieceTreeBase.

namespace TKCode.Arc.Editor.Model;

internal sealed partial class PieceTreeBase
{
    internal void Insert(int offset, string value, bool eolNormalized = false)
    {
        _eolNormalized = _eolNormalized && eolNormalized;
        _lastVisitedLine.LineNumber = 0;
        _lastVisitedLine.Value = string.Empty;

        if (Root != Sentinel)
        {
            var nodePosition = NodeAt(offset)!.Value;
            var node = nodePosition.Node;
            var remainder = nodePosition.Remainder;
            var nodeStartOffset = nodePosition.NodeStartOffset;
            var piece = node.Piece;
            var bufferIndex = piece.BufferIndex;
            var insertPosInBuffer = PositionInBuffer(node, remainder);
            if (node.Piece.BufferIndex == 0
                && piece.End.Line == _lastChangeBufferPos.Line
                && piece.End.Column == _lastChangeBufferPos.Column
                && nodeStartOffset + piece.Length == offset
                && value.Length < AverageBufferSize)
            {
                // changed buffer
                AppendToNode(node, value);
                ComputeBufferMetadata();
                return;
            }

            if (nodeStartOffset == offset)
            {
                InsertContentToNodeLeft(value, node);
                _searchCache.Validate(offset);
            }
            else if (nodeStartOffset + node.Piece.Length > offset)
            {
                // we are inserting into the middle of a node.
                var nodesToDel = new List<TreeNode>();
                var newRightPiece = new Piece(
                    piece.BufferIndex,
                    insertPosInBuffer,
                    piece.End,
                    GetLineFeedCnt(piece.BufferIndex, insertPosInBuffer, piece.End),
                    OffsetInBuffer(bufferIndex, piece.End) - OffsetInBuffer(bufferIndex, insertPosInBuffer));

                if (ShouldCheckCrlf() && EndWithCr(value))
                {
                    var headOfRight = NodeCharCodeAt(node, remainder);

                    if (headOfRight == 10) // \n
                    {
                        var newStart = new BufferCursor(newRightPiece.Start.Line + 1, 0);
                        newRightPiece = new Piece(
                            newRightPiece.BufferIndex,
                            newStart,
                            newRightPiece.End,
                            GetLineFeedCnt(newRightPiece.BufferIndex, newStart, newRightPiece.End),
                            newRightPiece.Length - 1);

                        value += "\n";
                    }
                }

                // reuse node for content before insertion point.
                if (ShouldCheckCrlf() && StartWithLf(value))
                {
                    var tailOfLeft = NodeCharCodeAt(node, remainder - 1);
                    if (tailOfLeft == 13) // \r
                    {
                        var previousPos = PositionInBuffer(node, remainder - 1);
                        DeleteNodeTail(node, previousPos);
                        value = "\r" + value;

                        if (node.Piece.Length == 0)
                        {
                            nodesToDel.Add(node);
                        }
                    }
                    else
                    {
                        DeleteNodeTail(node, insertPosInBuffer);
                    }
                }
                else
                {
                    DeleteNodeTail(node, insertPosInBuffer);
                }

                var newPieces = CreateNewPieces(value);
                if (newRightPiece.Length > 0)
                {
                    RbInsertRight(node, newRightPiece);
                }

                var tmpNode = node;
                for (var k = 0; k < newPieces.Count; k++)
                {
                    tmpNode = RbInsertRight(tmpNode, newPieces[k]);
                }

                DeleteNodes(nodesToDel);
            }
            else
            {
                InsertContentToNodeRight(value, node);
            }
        }
        else
        {
            // insert new node
            var pieces = CreateNewPieces(value);
            var node = RbInsertLeft(null, pieces[0]);

            for (var k = 1; k < pieces.Count; k++)
            {
                node = RbInsertRight(node, pieces[k]);
            }
        }

        // todo, this is too brutal. Total line feed count should be updated the same way as lf_left.
        ComputeBufferMetadata();
    }

    internal void Delete(int offset, int cnt)
    {
        _lastVisitedLine.LineNumber = 0;
        _lastVisitedLine.Value = string.Empty;

        if (cnt <= 0 || Root == Sentinel)
        {
            return;
        }

        var startPosition = NodeAt(offset)!.Value;
        var endPosition = NodeAt(offset + cnt)!.Value;
        var startNode = startPosition.Node;
        var endNode = endPosition.Node;

        if (startNode == endNode)
        {
            var startSplitPosInBuffer = PositionInBuffer(startNode, startPosition.Remainder);
            var endSplitPosInBuffer = PositionInBuffer(startNode, endPosition.Remainder);

            if (startPosition.NodeStartOffset == offset)
            {
                if (cnt == startNode.Piece.Length)
                {
                    // delete node
                    var next = startNode.Next();
                    RbTreeBase.RbDelete(this, startNode);
                    ValidateCrlfWithPrevNode(next);
                    ComputeBufferMetadata();
                    return;
                }

                DeleteNodeHead(startNode, endSplitPosInBuffer);
                _searchCache.Validate(offset);
                ValidateCrlfWithPrevNode(startNode);
                ComputeBufferMetadata();
                return;
            }

            if (startPosition.NodeStartOffset + startNode.Piece.Length == offset + cnt)
            {
                DeleteNodeTail(startNode, startSplitPosInBuffer);
                ValidateCrlfWithNextNode(startNode);
                ComputeBufferMetadata();
                return;
            }

            // delete content in the middle, this node will be splitted to nodes
            ShrinkNode(startNode, startSplitPosInBuffer, endSplitPosInBuffer);
            ComputeBufferMetadata();
            return;
        }

        var nodesToDelete = new List<TreeNode>();

        var startSplit = PositionInBuffer(startNode, startPosition.Remainder);
        DeleteNodeTail(startNode, startSplit);
        _searchCache.Validate(offset);
        if (startNode.Piece.Length == 0)
        {
            nodesToDelete.Add(startNode);
        }

        // update last touched node
        var endSplitPos = PositionInBuffer(endNode, endPosition.Remainder);
        DeleteNodeHead(endNode, endSplitPos);
        if (endNode.Piece.Length == 0)
        {
            nodesToDelete.Add(endNode);
        }

        // delete nodes in between
        var secondNode = startNode.Next();
        for (var node = secondNode; node != Sentinel && node != endNode; node = node.Next())
        {
            nodesToDelete.Add(node);
        }

        var prev = startNode.Piece.Length == 0 ? startNode.Prev() : startNode;
        DeleteNodes(nodesToDelete);
        ValidateCrlfWithNextNode(prev);
        ComputeBufferMetadata();
    }

    private void InsertContentToNodeLeft(string value, TreeNode node)
    {
        // we are inserting content to the beginning of node
        var nodesToDel = new List<TreeNode>();
        if (ShouldCheckCrlf() && EndWithCr(value) && StartWithLf(node))
        {
            // move `\n` to new node.
            var piece = node.Piece;
            var newStart = new BufferCursor(piece.Start.Line + 1, 0);
            var newPiece = new Piece(
                piece.BufferIndex,
                newStart,
                piece.End,
                GetLineFeedCnt(piece.BufferIndex, newStart, piece.End),
                piece.Length - 1);

            node.Piece = newPiece;

            value += "\n";
            RbTreeBase.UpdateTreeMetadata(this, node, -1, -1);

            if (node.Piece.Length == 0)
            {
                nodesToDel.Add(node);
            }
        }

        var newPieces = CreateNewPieces(value);
        var newNode = RbInsertLeft(node, newPieces[newPieces.Count - 1]);
        for (var k = newPieces.Count - 2; k >= 0; k--)
        {
            newNode = RbInsertLeft(newNode, newPieces[k]);
        }

        ValidateCrlfWithPrevNode(newNode);
        DeleteNodes(nodesToDel);
    }

    private void InsertContentToNodeRight(string value, TreeNode node)
    {
        // we are inserting to the right of this node.
        if (AdjustCarriageReturnFromNext(value, node))
        {
            // move \n to the new node.
            value += "\n";
        }

        var newPieces = CreateNewPieces(value);
        var newNode = RbInsertRight(node, newPieces[0]);
        var tmpNode = newNode;

        for (var k = 1; k < newPieces.Count; k++)
        {
            tmpNode = RbInsertRight(tmpNode, newPieces[k]);
        }

        ValidateCrlfWithPrevNode(newNode);
    }

    private List<Piece> CreateNewPieces(string text)
    {
        if (text.Length > AverageBufferSize)
        {
            // the content is large, operations like substring, charCode becomes slow
            // so here we split it into smaller chunks, just like what we did for CR/LF normalization
            var newPieces = new List<Piece>();
            while (text.Length > AverageBufferSize)
            {
                var lastChar = text[AverageBufferSize - 1];
                string splitText;
                if (lastChar == PieceTreeStrings.CarriageReturn || (lastChar >= 0xD800 && lastChar <= 0xDBFF))
                {
                    // last character is \r or a high surrogate => keep it back
                    splitText = text.Substring(0, AverageBufferSize - 1);
                    text = text.Substring(AverageBufferSize - 1);
                }
                else
                {
                    splitText = text.Substring(0, AverageBufferSize);
                    text = text.Substring(AverageBufferSize);
                }

                var lineStarts = CreateLineStartsFast(splitText);
                newPieces.Add(new Piece(
                    _buffers.Count, /* buffer index */
                    new BufferCursor(0, 0),
                    new BufferCursor(lineStarts.Length - 1, splitText.Length - lineStarts[lineStarts.Length - 1]),
                    lineStarts.Length - 1,
                    splitText.Length));
                _buffers.Add(new StringBuffer(splitText, lineStarts));
            }

            var lastLineStarts = CreateLineStartsFast(text);
            newPieces.Add(new Piece(
                _buffers.Count, /* buffer index */
                new BufferCursor(0, 0),
                new BufferCursor(lastLineStarts.Length - 1, text.Length - lastLineStarts[lastLineStarts.Length - 1]),
                lastLineStarts.Length - 1,
                text.Length));
            _buffers.Add(new StringBuffer(text, lastLineStarts));

            return newPieces;
        }

        var startOffset = _buffers[0].Length;
        var changeLineStarts = CreateLineStartsFast(text, false);
        var changeBuffer = _buffers[0];

        var start = _lastChangeBufferPos;
        if (changeBuffer.LineStarts[changeBuffer.LineStarts.Length - 1] == startOffset
            && startOffset != 0
            && StartWithLf(text)
            && changeBuffer.EndsWithCr)
        {
            _lastChangeBufferPos = new BufferCursor(_lastChangeBufferPos.Line, _lastChangeBufferPos.Column + 1);
            start = _lastChangeBufferPos;

            for (var i = 0; i < changeLineStarts.Length; i++)
            {
                changeLineStarts[i] += startOffset + 1;
            }

            changeBuffer.AppendLineStarts(changeLineStarts, true);
            changeBuffer.Append('_');
            changeBuffer.Append(text);
            startOffset += 1;
        }
        else
        {
            if (startOffset != 0)
            {
                for (var i = 0; i < changeLineStarts.Length; i++)
                {
                    changeLineStarts[i] += startOffset;
                }
            }

            changeBuffer.AppendLineStarts(changeLineStarts, true);
            changeBuffer.Append(text);
        }

        var endOffset = _buffers[0].Length;
        var endIndex = _buffers[0].LineStarts.Length - 1;
        var endColumn = endOffset - _buffers[0].LineStarts[endIndex];
        var endPos = new BufferCursor(endIndex, endColumn);
        var newPiece = new Piece(
            0, // todo@peng
            start,
            endPos,
            GetLineFeedCnt(0, start, endPos),
            endOffset - startOffset);
        _lastChangeBufferPos = endPos;
        return new List<Piece> { newPiece };
    }

    private void AppendToNode(TreeNode node, string value)
    {
        if (AdjustCarriageReturnFromNext(value, node))
        {
            value += "\n";
        }

        var hitCrlf = ShouldCheckCrlf() && StartWithLf(value) && EndWithCr(node);
        var startOffset = _buffers[0].Length;
        _buffers[0].Append(value);
        var lineStarts = CreateLineStartsFast(value, false);
        for (var i = 0; i < lineStarts.Length; i++)
        {
            lineStarts[i] += startOffset;
        }

        if (hitCrlf)
        {
            var previousStartOffset = _buffers[0].LineStarts[_buffers[0].LineStarts.Length - 2];
            _buffers[0].RemoveLastLineStart();
            // _lastChangeBufferPos is already wrong
            _lastChangeBufferPos = new BufferCursor(_lastChangeBufferPos.Line - 1, startOffset - previousStartOffset);
        }

        _buffers[0].AppendLineStarts(lineStarts, true);
        var endIndex = _buffers[0].LineStarts.Length - 1;
        var endColumn = _buffers[0].Length - _buffers[0].LineStarts[endIndex];
        var newEnd = new BufferCursor(endIndex, endColumn);
        var newLength = node.Piece.Length + value.Length;
        var oldLineFeedCnt = node.Piece.LineFeedCnt;
        var newLineFeedCnt = GetLineFeedCnt(0, node.Piece.Start, newEnd);
        var lfDelta = newLineFeedCnt - oldLineFeedCnt;

        node.Piece = new Piece(
            node.Piece.BufferIndex,
            node.Piece.Start,
            newEnd,
            newLineFeedCnt,
            newLength);

        _lastChangeBufferPos = newEnd;
        RbTreeBase.UpdateTreeMetadata(this, node, value.Length, lfDelta);
    }

    private void DeleteNodeTail(TreeNode node, BufferCursor pos)
    {
        var piece = node.Piece;
        var originalLfCnt = piece.LineFeedCnt;
        var originalEndOffset = OffsetInBuffer(piece.BufferIndex, piece.End);

        var newEnd = pos;
        var newEndOffset = OffsetInBuffer(piece.BufferIndex, newEnd);
        var newLineFeedCnt = GetLineFeedCnt(piece.BufferIndex, piece.Start, newEnd);

        var lfDelta = newLineFeedCnt - originalLfCnt;
        var sizeDelta = newEndOffset - originalEndOffset;
        var newLength = piece.Length + sizeDelta;

        node.Piece = new Piece(
            piece.BufferIndex,
            piece.Start,
            newEnd,
            newLineFeedCnt,
            newLength);

        RbTreeBase.UpdateTreeMetadata(this, node, sizeDelta, lfDelta);
    }

    private void DeleteNodeHead(TreeNode node, BufferCursor pos)
    {
        var piece = node.Piece;
        var originalLfCnt = piece.LineFeedCnt;
        var originalStartOffset = OffsetInBuffer(piece.BufferIndex, piece.Start);

        var newStart = pos;
        var newLineFeedCnt = GetLineFeedCnt(piece.BufferIndex, newStart, piece.End);
        var newStartOffset = OffsetInBuffer(piece.BufferIndex, newStart);
        var lfDelta = newLineFeedCnt - originalLfCnt;
        var sizeDelta = originalStartOffset - newStartOffset;
        var newLength = piece.Length + sizeDelta;
        node.Piece = new Piece(
            piece.BufferIndex,
            newStart,
            piece.End,
            newLineFeedCnt,
            newLength);

        RbTreeBase.UpdateTreeMetadata(this, node, sizeDelta, lfDelta);
    }

    private void ShrinkNode(TreeNode node, BufferCursor start, BufferCursor end)
    {
        var piece = node.Piece;
        var originalStartPos = piece.Start;
        var originalEndPos = piece.End;

        // old piece, originalStartPos, start
        var oldLength = piece.Length;
        var oldLfCnt = piece.LineFeedCnt;
        var newEnd = start;
        var newLineFeedCnt = GetLineFeedCnt(piece.BufferIndex, piece.Start, newEnd);
        var newLength = OffsetInBuffer(piece.BufferIndex, start) - OffsetInBuffer(piece.BufferIndex, originalStartPos);

        node.Piece = new Piece(
            piece.BufferIndex,
            piece.Start,
            newEnd,
            newLineFeedCnt,
            newLength);

        RbTreeBase.UpdateTreeMetadata(this, node, newLength - oldLength, newLineFeedCnt - oldLfCnt);

        // new right piece, end, originalEndPos
        var newPiece = new Piece(
            piece.BufferIndex,
            end,
            originalEndPos,
            GetLineFeedCnt(piece.BufferIndex, end, originalEndPos),
            OffsetInBuffer(piece.BufferIndex, originalEndPos) - OffsetInBuffer(piece.BufferIndex, end));

        var newNode = RbInsertRight(node, newPiece);
        ValidateCrlfWithPrevNode(newNode);
    }

    private void DeleteNodes(List<TreeNode> nodes)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            RbTreeBase.RbDelete(this, nodes[i]);
        }
    }

    // #region CRLF

    private bool ShouldCheckCrlf() => !(_eolNormalized && _eol == "\n");

    private bool StartWithLf(string value)
    {
        return value.Length > 0 && value[0] == PieceTreeStrings.LineFeed;
    }

    private bool StartWithLf(TreeNode value)
    {
        if (value == Sentinel || value.Piece.LineFeedCnt == 0)
        {
            return false;
        }

        var piece = value.Piece;
        var lineStarts = _buffers[piece.BufferIndex].LineStarts;
        var line = piece.Start.Line;
        var startOffset = lineStarts[line] + piece.Start.Column;
        if (line == lineStarts.Length - 1)
        {
            // last line, so there is no line feed at the end of this line
            return false;
        }

        var nextLineOffset = lineStarts[line + 1];
        if (nextLineOffset > startOffset + 1)
        {
            return false;
        }

        return _buffers[piece.BufferIndex].CharCodeAt(startOffset) == PieceTreeStrings.LineFeed;
    }

    private bool EndWithCr(string value)
    {
        return value.Length > 0 && value[value.Length - 1] == PieceTreeStrings.CarriageReturn;
    }

    private bool EndWithCr(TreeNode value)
    {
        if (value == Sentinel || value.Piece.LineFeedCnt == 0)
        {
            return false;
        }

        return NodeCharCodeAt(value, value.Piece.Length - 1) == PieceTreeStrings.CarriageReturn;
    }

    private void ValidateCrlfWithPrevNode(TreeNode nextNode)
    {
        if (ShouldCheckCrlf() && StartWithLf(nextNode))
        {
            var node = nextNode.Prev();
            if (EndWithCr(node))
            {
                FixCrlf(node, nextNode);
            }
        }
    }

    private void ValidateCrlfWithNextNode(TreeNode node)
    {
        if (ShouldCheckCrlf() && EndWithCr(node))
        {
            var nextNode = node.Next();
            if (StartWithLf(nextNode))
            {
                FixCrlf(node, nextNode);
            }
        }
    }

    private void FixCrlf(TreeNode prev, TreeNode next)
    {
        var nodesToDel = new List<TreeNode>();
        // update node
        var lineStarts = _buffers[prev.Piece.BufferIndex].LineStarts;
        BufferCursor newEnd;
        if (prev.Piece.End.Column == 0)
        {
            // it means, last line ends with \r, not \r\n
            newEnd = new BufferCursor(
                prev.Piece.End.Line - 1,
                lineStarts[prev.Piece.End.Line] - lineStarts[prev.Piece.End.Line - 1] - 1);
        }
        else
        {
            // \r\n
            newEnd = new BufferCursor(prev.Piece.End.Line, prev.Piece.End.Column - 1);
        }

        var prevNewLength = prev.Piece.Length - 1;
        var prevNewLfCnt = prev.Piece.LineFeedCnt - 1;
        prev.Piece = new Piece(
            prev.Piece.BufferIndex,
            prev.Piece.Start,
            newEnd,
            prevNewLfCnt,
            prevNewLength);

        RbTreeBase.UpdateTreeMetadata(this, prev, -1, -1);
        if (prev.Piece.Length == 0)
        {
            nodesToDel.Add(prev);
        }

        // update nextNode
        var newStart = new BufferCursor(next.Piece.Start.Line + 1, 0);
        var newLength = next.Piece.Length - 1;
        var newLineFeedCnt = GetLineFeedCnt(next.Piece.BufferIndex, newStart, next.Piece.End);
        next.Piece = new Piece(
            next.Piece.BufferIndex,
            newStart,
            next.Piece.End,
            newLineFeedCnt,
            newLength);

        RbTreeBase.UpdateTreeMetadata(this, next, -1, -1);
        if (next.Piece.Length == 0)
        {
            nodesToDel.Add(next);
        }

        // create new piece which contains \r\n
        var pieces = CreateNewPieces("\r\n");
        RbInsertRight(prev, pieces[0]);
        // delete empty nodes

        for (var i = 0; i < nodesToDel.Count; i++)
        {
            RbTreeBase.RbDelete(this, nodesToDel[i]);
        }
    }

    private bool AdjustCarriageReturnFromNext(string value, TreeNode node)
    {
        if (ShouldCheckCrlf() && EndWithCr(value))
        {
            var nextNode = node.Next();
            if (StartWithLf(nextNode))
            {
                // move `\n` forward
                value += "\n";

                if (nextNode.Piece.Length == 1)
                {
                    RbTreeBase.RbDelete(this, nextNode);
                }
                else
                {
                    var piece = nextNode.Piece;
                    var newStart = new BufferCursor(piece.Start.Line + 1, 0);
                    var newLength = piece.Length - 1;
                    var newLineFeedCnt = GetLineFeedCnt(piece.BufferIndex, newStart, piece.End);
                    nextNode.Piece = new Piece(
                        piece.BufferIndex,
                        newStart,
                        piece.End,
                        newLineFeedCnt,
                        newLength);

                    RbTreeBase.UpdateTreeMetadata(this, nextNode, -1, -1);
                }

                return true;
            }
        }

        return false;
    }

    // #endregion
}
