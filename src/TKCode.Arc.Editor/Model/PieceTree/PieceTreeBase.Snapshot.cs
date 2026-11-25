// Not in vscode: creates the random-access TextSnapshot of the tree (see TextSnapshot.cs).

namespace TKCode.Arc.Editor.Model;

internal sealed partial class PieceTreeBase
{
    /// <summary>A snapshot of the text that other threads may read by line; O(pieces), no text is copied.</summary>
    internal TextSnapshot CreateTextSnapshot(int versionId)
    {
        var pieces = new List<TextSnapshot.SnapshotPiece>();
        var stack = new Stack<TreeNode>();
        var node = Root;
        while (stack.Count > 0 || node != Sentinel)
        {
            if (node != Sentinel)
            {
                stack.Push(node);
                node = node.Left;
                continue;
            }

            node = stack.Pop();
            var piece = node.Piece;
            if (piece.Length > 0)
            {
                var buffer = _buffers[piece.BufferIndex];
                pieces.Add(new TextSnapshot.SnapshotPiece(
                    buffer,
                    OffsetInBuffer(piece.BufferIndex, piece.Start),
                    piece.Length,
                    piece.Start.Line,
                    piece.LineFeedCnt,
                    buffer.LineStarts));
            }

            node = node.Right;
        }

        if (pieces.Count == 0)
        {
            var empty = _buffers[0];
            pieces.Add(new TextSnapshot.SnapshotPiece(empty, 0, 0, 0, 0, [0]));
        }

        return new TextSnapshot(pieces.ToArray(), _eol, versionId);
    }
}
