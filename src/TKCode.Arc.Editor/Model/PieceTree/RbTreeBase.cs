// Ported from vscode's src/vs/editor/common/model/pieceTreeTextBuffer/rbTreeBase.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
//
// Adaptation: vscode has one module-level SENTINEL shared by every tree. This engine edits different buffers from
// different threads, so each tree owns its sentinel (TreeNode.Sentinel), and the operations take it from the node.

namespace TKCode.Arc.Editor.Model;

internal enum NodeColor
{
    Black = 0,
    Red = 1,
}

/// <summary>A red-black tree node; <see cref="Piece"/> is null on the sentinel.</summary>
internal sealed class TreeNode
{
    internal TreeNode Parent;
    internal TreeNode Left;
    internal TreeNode Right;
    internal NodeColor Color;

    // Piece
    internal Piece Piece;
    internal int SizeLeft; // size of the left subtree (not inorder)
    internal int LfLeft; // line feeds cnt in the left subtree (not in order)

    internal TreeNode Sentinel { get; private set; }

    internal TreeNode(Piece piece, NodeColor color, TreeNode sentinel)
    {
        Piece = piece;
        Color = color;
        SizeLeft = 0;
        LfLeft = 0;
        Sentinel = sentinel;
        Parent = sentinel;
        Left = sentinel;
        Right = sentinel;
    }

    /// <summary>Makes this node the sentinel of its tree: it points at itself and has no piece.</summary>
    internal void InitializeAsSentinel()
    {
        Sentinel = this;
        Parent = this;
        Left = this;
        Right = this;
        Color = NodeColor.Black;
        SizeLeft = 0;
        LfLeft = 0;
    }

    internal TreeNode Next()
    {
        if (Right != Sentinel)
        {
            return RbTreeBase.Leftest(Right);
        }

        var node = this;

        while (node.Parent != Sentinel)
        {
            if (node.Parent.Left == node)
            {
                break;
            }

            node = node.Parent;
        }

        if (node.Parent == Sentinel)
        {
            return Sentinel;
        }

        return node.Parent;
    }

    internal TreeNode Prev()
    {
        if (Left != Sentinel)
        {
            return RbTreeBase.Righttest(Left);
        }

        var node = this;

        while (node.Parent != Sentinel)
        {
            if (node.Parent.Right == node)
            {
                break;
            }

            node = node.Parent;
        }

        if (node.Parent == Sentinel)
        {
            return Sentinel;
        }

        return node.Parent;
    }

    internal void Detach()
    {
        Parent = null!;
        Left = null!;
        Right = null!;
    }
}

internal static class RbTreeBase
{
    internal static TreeNode CreateSentinel()
    {
        var sentinel = new TreeNode(null!, NodeColor.Black, null!);
        sentinel.InitializeAsSentinel();
        return sentinel;
    }

    internal static TreeNode Leftest(TreeNode node)
    {
        while (node.Left != node.Sentinel)
        {
            node = node.Left;
        }

        return node;
    }

    internal static TreeNode Righttest(TreeNode node)
    {
        while (node.Right != node.Sentinel)
        {
            node = node.Right;
        }

        return node;
    }

    private static int CalculateSize(TreeNode node)
    {
        if (node == node.Sentinel)
        {
            return 0;
        }

        return node.SizeLeft + node.Piece.Length + CalculateSize(node.Right);
    }

    private static int CalculateLf(TreeNode node)
    {
        if (node == node.Sentinel)
        {
            return 0;
        }

        return node.LfLeft + node.Piece.LineFeedCnt + CalculateLf(node.Right);
    }

    private static void ResetSentinel(TreeNode sentinel)
    {
        sentinel.Parent = sentinel;
    }

    internal static void LeftRotate(PieceTreeBase tree, TreeNode x)
    {
        var sentinel = tree.Sentinel;
        var y = x.Right;

        // fix size_left
        y.SizeLeft += x.SizeLeft + (x.Piece is not null ? x.Piece.Length : 0);
        y.LfLeft += x.LfLeft + (x.Piece is not null ? x.Piece.LineFeedCnt : 0);
        x.Right = y.Left;

        if (y.Left != sentinel)
        {
            y.Left.Parent = x;
        }

        y.Parent = x.Parent;
        if (x.Parent == sentinel)
        {
            tree.Root = y;
        }
        else if (x.Parent.Left == x)
        {
            x.Parent.Left = y;
        }
        else
        {
            x.Parent.Right = y;
        }

        y.Left = x;
        x.Parent = y;
    }

    internal static void RightRotate(PieceTreeBase tree, TreeNode y)
    {
        var sentinel = tree.Sentinel;
        var x = y.Left;
        y.Left = x.Right;
        if (x.Right != sentinel)
        {
            x.Right.Parent = y;
        }

        x.Parent = y.Parent;

        // fix size_left
        y.SizeLeft -= x.SizeLeft + (x.Piece is not null ? x.Piece.Length : 0);
        y.LfLeft -= x.LfLeft + (x.Piece is not null ? x.Piece.LineFeedCnt : 0);

        if (y.Parent == sentinel)
        {
            tree.Root = x;
        }
        else if (y == y.Parent.Right)
        {
            y.Parent.Right = x;
        }
        else
        {
            y.Parent.Left = x;
        }

        x.Right = y;
        y.Parent = x;
    }

    internal static void RbDelete(PieceTreeBase tree, TreeNode z)
    {
        var sentinel = tree.Sentinel;
        TreeNode x;
        TreeNode y;

        if (z.Left == sentinel)
        {
            y = z;
            x = y.Right;
        }
        else if (z.Right == sentinel)
        {
            y = z;
            x = y.Left;
        }
        else
        {
            y = Leftest(z.Right);
            x = y.Right;
        }

        if (y == tree.Root)
        {
            tree.Root = x;

            // if x is null, we are removing the only node
            x.Color = NodeColor.Black;
            z.Detach();
            ResetSentinel(sentinel);
            tree.Root.Parent = sentinel;

            return;
        }

        var yWasRed = y.Color == NodeColor.Red;

        if (y == y.Parent.Left)
        {
            y.Parent.Left = x;
        }
        else
        {
            y.Parent.Right = x;
        }

        if (y == z)
        {
            x.Parent = y.Parent;
            RecomputeTreeMetadata(tree, x);
        }
        else
        {
            if (y.Parent == z)
            {
                x.Parent = y;
            }
            else
            {
                x.Parent = y.Parent;
            }

            // as we make changes to x's hierarchy, update size_left of subtree first
            RecomputeTreeMetadata(tree, x);

            y.Left = z.Left;
            y.Right = z.Right;
            y.Parent = z.Parent;
            y.Color = z.Color;

            if (z == tree.Root)
            {
                tree.Root = y;
            }
            else
            {
                if (z == z.Parent.Left)
                {
                    z.Parent.Left = y;
                }
                else
                {
                    z.Parent.Right = y;
                }
            }

            if (y.Left != sentinel)
            {
                y.Left.Parent = y;
            }

            if (y.Right != sentinel)
            {
                y.Right.Parent = y;
            }

            // update metadata
            // we replace z with y, so in this sub tree, the length change is z.item.length
            y.SizeLeft = z.SizeLeft;
            y.LfLeft = z.LfLeft;
            RecomputeTreeMetadata(tree, y);
        }

        z.Detach();

        if (x.Parent.Left == x)
        {
            var newSizeLeft = CalculateSize(x);
            var newLfLeft = CalculateLf(x);
            if (newSizeLeft != x.Parent.SizeLeft || newLfLeft != x.Parent.LfLeft)
            {
                var delta = newSizeLeft - x.Parent.SizeLeft;
                var lfDelta = newLfLeft - x.Parent.LfLeft;
                x.Parent.SizeLeft = newSizeLeft;
                x.Parent.LfLeft = newLfLeft;
                UpdateTreeMetadata(tree, x.Parent, delta, lfDelta);
            }
        }

        RecomputeTreeMetadata(tree, x.Parent);

        if (yWasRed)
        {
            ResetSentinel(sentinel);
            return;
        }

        // RB-DELETE-FIXUP
        TreeNode w;
        while (x != tree.Root && x.Color == NodeColor.Black)
        {
            if (x == x.Parent.Left)
            {
                w = x.Parent.Right;

                if (w.Color == NodeColor.Red)
                {
                    w.Color = NodeColor.Black;
                    x.Parent.Color = NodeColor.Red;
                    LeftRotate(tree, x.Parent);
                    w = x.Parent.Right;
                }

                if (w.Left.Color == NodeColor.Black && w.Right.Color == NodeColor.Black)
                {
                    w.Color = NodeColor.Red;
                    x = x.Parent;
                }
                else
                {
                    if (w.Right.Color == NodeColor.Black)
                    {
                        w.Left.Color = NodeColor.Black;
                        w.Color = NodeColor.Red;
                        RightRotate(tree, w);
                        w = x.Parent.Right;
                    }

                    w.Color = x.Parent.Color;
                    x.Parent.Color = NodeColor.Black;
                    w.Right.Color = NodeColor.Black;
                    LeftRotate(tree, x.Parent);
                    x = tree.Root;
                }
            }
            else
            {
                w = x.Parent.Left;

                if (w.Color == NodeColor.Red)
                {
                    w.Color = NodeColor.Black;
                    x.Parent.Color = NodeColor.Red;
                    RightRotate(tree, x.Parent);
                    w = x.Parent.Left;
                }

                if (w.Left.Color == NodeColor.Black && w.Right.Color == NodeColor.Black)
                {
                    w.Color = NodeColor.Red;
                    x = x.Parent;
                }
                else
                {
                    if (w.Left.Color == NodeColor.Black)
                    {
                        w.Right.Color = NodeColor.Black;
                        w.Color = NodeColor.Red;
                        LeftRotate(tree, w);
                        w = x.Parent.Left;
                    }

                    w.Color = x.Parent.Color;
                    x.Parent.Color = NodeColor.Black;
                    w.Left.Color = NodeColor.Black;
                    RightRotate(tree, x.Parent);
                    x = tree.Root;
                }
            }
        }

        x.Color = NodeColor.Black;
        ResetSentinel(sentinel);
    }

    internal static void FixInsert(PieceTreeBase tree, TreeNode x)
    {
        RecomputeTreeMetadata(tree, x);

        while (x != tree.Root && x.Parent.Color == NodeColor.Red)
        {
            if (x.Parent == x.Parent.Parent.Left)
            {
                var y = x.Parent.Parent.Right;

                if (y.Color == NodeColor.Red)
                {
                    x.Parent.Color = NodeColor.Black;
                    y.Color = NodeColor.Black;
                    x.Parent.Parent.Color = NodeColor.Red;
                    x = x.Parent.Parent;
                }
                else
                {
                    if (x == x.Parent.Right)
                    {
                        x = x.Parent;
                        LeftRotate(tree, x);
                    }

                    x.Parent.Color = NodeColor.Black;
                    x.Parent.Parent.Color = NodeColor.Red;
                    RightRotate(tree, x.Parent.Parent);
                }
            }
            else
            {
                var y = x.Parent.Parent.Left;

                if (y.Color == NodeColor.Red)
                {
                    x.Parent.Color = NodeColor.Black;
                    y.Color = NodeColor.Black;
                    x.Parent.Parent.Color = NodeColor.Red;
                    x = x.Parent.Parent;
                }
                else
                {
                    if (x == x.Parent.Left)
                    {
                        x = x.Parent;
                        RightRotate(tree, x);
                    }

                    x.Parent.Color = NodeColor.Black;
                    x.Parent.Parent.Color = NodeColor.Red;
                    LeftRotate(tree, x.Parent.Parent);
                }
            }
        }

        tree.Root.Color = NodeColor.Black;
    }

    internal static void UpdateTreeMetadata(PieceTreeBase tree, TreeNode x, int delta, int lineFeedCntDelta)
    {
        var sentinel = tree.Sentinel;
        // node length change or line feed count change
        while (x != tree.Root && x != sentinel)
        {
            if (x.Parent.Left == x)
            {
                x.Parent.SizeLeft += delta;
                x.Parent.LfLeft += lineFeedCntDelta;
            }

            x = x.Parent;
        }
    }

    internal static void RecomputeTreeMetadata(PieceTreeBase tree, TreeNode x)
    {
        if (x == tree.Root)
        {
            return;
        }

        // go upwards till the node whose left subtree is changed.
        while (x != tree.Root && x == x.Parent.Right)
        {
            x = x.Parent;
        }

        if (x == tree.Root)
        {
            // well, it means we add a node to the end (inorder)
            return;
        }

        // x is the node whose right subtree is changed.
        x = x.Parent;

        var delta = CalculateSize(x.Left) - x.SizeLeft;
        var lfDelta = CalculateLf(x.Left) - x.LfLeft;
        x.SizeLeft += delta;
        x.LfLeft += lfDelta;

        // go upwards till root. O(logN)
        while (x != tree.Root && (delta != 0 || lfDelta != 0))
        {
            if (x.Parent.Left == x)
            {
                x.Parent.SizeLeft += delta;
                x.Parent.LfLeft += lfDelta;
            }

            x = x.Parent;
        }
    }
}
