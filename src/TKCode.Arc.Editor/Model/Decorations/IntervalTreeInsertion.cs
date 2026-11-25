// Ported from vscode's src/vs/editor/common/model/intervalTree.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// Insertion and deletion (RB-INSERT, RB-DELETE) and the rotations that maintain the augmented max end.

namespace TKCode.Arc.Editor.Model;

internal sealed partial class IntervalTree
{
    private static IntervalNode RbTreeInsert(IntervalTree T, IntervalNode newNode)
    {
        if (ReferenceEquals(T.Root, T.Sentinel))
        {
            newNode.Parent = T.Sentinel;
            newNode.Left = T.Sentinel;
            newNode.Right = T.Sentinel;
            IntervalTreeNodeMetadata.SetNodeColor(newNode, IntervalNodeColor.Black);
            T.Root = newNode;
            return T.Root;
        }

        TreeInsert(T, newNode);

        RecomputeMaxEndWalkToRoot(T, newNode.Parent);

        // repair tree
        IntervalNode x = newNode;
        while (!ReferenceEquals(x, T.Root) && IntervalTreeNodeMetadata.GetNodeColor(x.Parent) == IntervalNodeColor.Red)
        {
            if (ReferenceEquals(x.Parent, x.Parent.Parent.Left))
            {
                IntervalNode y = x.Parent.Parent.Right;

                if (IntervalTreeNodeMetadata.GetNodeColor(y) == IntervalNodeColor.Red)
                {
                    IntervalTreeNodeMetadata.SetNodeColor(x.Parent, IntervalNodeColor.Black);
                    IntervalTreeNodeMetadata.SetNodeColor(y, IntervalNodeColor.Black);
                    IntervalTreeNodeMetadata.SetNodeColor(x.Parent.Parent, IntervalNodeColor.Red);
                    x = x.Parent.Parent;
                }
                else
                {
                    if (ReferenceEquals(x, x.Parent.Right))
                    {
                        x = x.Parent;
                        LeftRotate(T, x);
                    }
                    IntervalTreeNodeMetadata.SetNodeColor(x.Parent, IntervalNodeColor.Black);
                    IntervalTreeNodeMetadata.SetNodeColor(x.Parent.Parent, IntervalNodeColor.Red);
                    RightRotate(T, x.Parent.Parent);
                }
            }
            else
            {
                IntervalNode y = x.Parent.Parent.Left;

                if (IntervalTreeNodeMetadata.GetNodeColor(y) == IntervalNodeColor.Red)
                {
                    IntervalTreeNodeMetadata.SetNodeColor(x.Parent, IntervalNodeColor.Black);
                    IntervalTreeNodeMetadata.SetNodeColor(y, IntervalNodeColor.Black);
                    IntervalTreeNodeMetadata.SetNodeColor(x.Parent.Parent, IntervalNodeColor.Red);
                    x = x.Parent.Parent;
                }
                else
                {
                    if (ReferenceEquals(x, x.Parent.Left))
                    {
                        x = x.Parent;
                        RightRotate(T, x);
                    }
                    IntervalTreeNodeMetadata.SetNodeColor(x.Parent, IntervalNodeColor.Black);
                    IntervalTreeNodeMetadata.SetNodeColor(x.Parent.Parent, IntervalNodeColor.Red);
                    LeftRotate(T, x.Parent.Parent);
                }
            }
        }

        IntervalTreeNodeMetadata.SetNodeColor(T.Root, IntervalNodeColor.Black);

        return newNode;
    }

    private static void TreeInsert(IntervalTree T, IntervalNode z)
    {
        int delta = 0;
        IntervalNode x = T.Root;
        int zAbsoluteStart = z.Start;
        int zAbsoluteEnd = z.End;
        while (true)
        {
            int cmp = IntervalCompare(zAbsoluteStart, zAbsoluteEnd, x.Start + delta, x.End + delta);
            if (cmp < 0)
            {
                // this node should be inserted to the left
                // => it is not affected by the node's delta
                if (ReferenceEquals(x.Left, T.Sentinel))
                {
                    z.Start -= delta;
                    z.End -= delta;
                    z.MaxEnd -= delta;
                    x.Left = z;
                    break;
                }
                else
                {
                    x = x.Left;
                }
            }
            else
            {
                // this node should be inserted to the right
                // => it is not affected by the node's delta
                if (ReferenceEquals(x.Right, T.Sentinel))
                {
                    z.Start -= delta + x.Delta;
                    z.End -= delta + x.Delta;
                    z.MaxEnd -= delta + x.Delta;
                    x.Right = z;
                    break;
                }
                else
                {
                    delta += x.Delta;
                    x = x.Right;
                }
            }
        }

        z.Parent = x;
        z.Left = T.Sentinel;
        z.Right = T.Sentinel;
        IntervalTreeNodeMetadata.SetNodeColor(z, IntervalNodeColor.Red);
    }

    private static void RbTreeDelete(IntervalTree T, IntervalNode z)
    {
        IntervalNode x;
        IntervalNode y;

        // RB-DELETE except we don't swap z and y in case c)
        // i.e. we always delete what's pointed at by z.

        if (ReferenceEquals(z.Left, T.Sentinel))
        {
            x = z.Right;
            y = z;

            // x's delta is no longer influenced by z's delta
            x.Delta += z.Delta;
            if (x.Delta < IntervalTreeNodeMetadata.MinSafeDelta || x.Delta > IntervalTreeNodeMetadata.MaxSafeDelta)
            {
                T.RequestNormalizeDelta = true;
            }
            x.Start += z.Delta;
            x.End += z.Delta;
        }
        else if (ReferenceEquals(z.Right, T.Sentinel))
        {
            x = z.Left;
            y = z;
        }
        else
        {
            y = Leftest(T, z.Right);
            x = y.Right;

            // y's delta is no longer influenced by z's delta,
            // but we don't want to walk the entire right-hand-side subtree of x.
            // we therefore maintain z's delta in y, and adjust only x
            x.Start += y.Delta;
            x.End += y.Delta;
            x.Delta += y.Delta;
            if (x.Delta < IntervalTreeNodeMetadata.MinSafeDelta || x.Delta > IntervalTreeNodeMetadata.MaxSafeDelta)
            {
                T.RequestNormalizeDelta = true;
            }

            y.Start += z.Delta;
            y.End += z.Delta;
            y.Delta = z.Delta;
            if (y.Delta < IntervalTreeNodeMetadata.MinSafeDelta || y.Delta > IntervalTreeNodeMetadata.MaxSafeDelta)
            {
                T.RequestNormalizeDelta = true;
            }
        }

        if (ReferenceEquals(y, T.Root))
        {
            T.Root = x;
            IntervalTreeNodeMetadata.SetNodeColor(x, IntervalNodeColor.Black);

            z.Detach();
            ResetSentinel(T);
            RecomputeMaxEnd(T, x);
            T.Root.Parent = T.Sentinel;
            return;
        }

        bool yWasRed = IntervalTreeNodeMetadata.GetNodeColor(y) == IntervalNodeColor.Red;

        if (ReferenceEquals(y, y.Parent.Left))
        {
            y.Parent.Left = x;
        }
        else
        {
            y.Parent.Right = x;
        }

        if (ReferenceEquals(y, z))
        {
            x.Parent = y.Parent;
        }
        else
        {
            if (ReferenceEquals(y.Parent, z))
            {
                x.Parent = y;
            }
            else
            {
                x.Parent = y.Parent;
            }

            y.Left = z.Left;
            y.Right = z.Right;
            y.Parent = z.Parent;
            IntervalTreeNodeMetadata.SetNodeColor(y, IntervalTreeNodeMetadata.GetNodeColor(z));

            if (ReferenceEquals(z, T.Root))
            {
                T.Root = y;
            }
            else
            {
                if (ReferenceEquals(z, z.Parent.Left))
                {
                    z.Parent.Left = y;
                }
                else
                {
                    z.Parent.Right = y;
                }
            }

            if (!ReferenceEquals(y.Left, T.Sentinel))
            {
                y.Left.Parent = y;
            }
            if (!ReferenceEquals(y.Right, T.Sentinel))
            {
                y.Right.Parent = y;
            }
        }

        z.Detach();

        if (yWasRed)
        {
            RecomputeMaxEndWalkToRoot(T, x.Parent);
            if (!ReferenceEquals(y, z))
            {
                RecomputeMaxEndWalkToRoot(T, y);
                RecomputeMaxEndWalkToRoot(T, y.Parent);
            }
            ResetSentinel(T);
            return;
        }

        RecomputeMaxEndWalkToRoot(T, x);
        RecomputeMaxEndWalkToRoot(T, x.Parent);
        if (!ReferenceEquals(y, z))
        {
            RecomputeMaxEndWalkToRoot(T, y);
            RecomputeMaxEndWalkToRoot(T, y.Parent);
        }

        // RB-DELETE-FIXUP
        IntervalNode w;
        while (!ReferenceEquals(x, T.Root) && IntervalTreeNodeMetadata.GetNodeColor(x) == IntervalNodeColor.Black)
        {
            if (ReferenceEquals(x, x.Parent.Left))
            {
                w = x.Parent.Right;

                if (IntervalTreeNodeMetadata.GetNodeColor(w) == IntervalNodeColor.Red)
                {
                    IntervalTreeNodeMetadata.SetNodeColor(w, IntervalNodeColor.Black);
                    IntervalTreeNodeMetadata.SetNodeColor(x.Parent, IntervalNodeColor.Red);
                    LeftRotate(T, x.Parent);
                    w = x.Parent.Right;
                }

                if (IntervalTreeNodeMetadata.GetNodeColor(w.Left) == IntervalNodeColor.Black && IntervalTreeNodeMetadata.GetNodeColor(w.Right) == IntervalNodeColor.Black)
                {
                    IntervalTreeNodeMetadata.SetNodeColor(w, IntervalNodeColor.Red);
                    x = x.Parent;
                }
                else
                {
                    if (IntervalTreeNodeMetadata.GetNodeColor(w.Right) == IntervalNodeColor.Black)
                    {
                        IntervalTreeNodeMetadata.SetNodeColor(w.Left, IntervalNodeColor.Black);
                        IntervalTreeNodeMetadata.SetNodeColor(w, IntervalNodeColor.Red);
                        RightRotate(T, w);
                        w = x.Parent.Right;
                    }

                    IntervalTreeNodeMetadata.SetNodeColor(w, IntervalTreeNodeMetadata.GetNodeColor(x.Parent));
                    IntervalTreeNodeMetadata.SetNodeColor(x.Parent, IntervalNodeColor.Black);
                    IntervalTreeNodeMetadata.SetNodeColor(w.Right, IntervalNodeColor.Black);
                    LeftRotate(T, x.Parent);
                    x = T.Root;
                }
            }
            else
            {
                w = x.Parent.Left;

                if (IntervalTreeNodeMetadata.GetNodeColor(w) == IntervalNodeColor.Red)
                {
                    IntervalTreeNodeMetadata.SetNodeColor(w, IntervalNodeColor.Black);
                    IntervalTreeNodeMetadata.SetNodeColor(x.Parent, IntervalNodeColor.Red);
                    RightRotate(T, x.Parent);
                    w = x.Parent.Left;
                }

                if (IntervalTreeNodeMetadata.GetNodeColor(w.Left) == IntervalNodeColor.Black && IntervalTreeNodeMetadata.GetNodeColor(w.Right) == IntervalNodeColor.Black)
                {
                    IntervalTreeNodeMetadata.SetNodeColor(w, IntervalNodeColor.Red);
                    x = x.Parent;
                }
                else
                {
                    if (IntervalTreeNodeMetadata.GetNodeColor(w.Left) == IntervalNodeColor.Black)
                    {
                        IntervalTreeNodeMetadata.SetNodeColor(w.Right, IntervalNodeColor.Black);
                        IntervalTreeNodeMetadata.SetNodeColor(w, IntervalNodeColor.Red);
                        LeftRotate(T, w);
                        w = x.Parent.Left;
                    }

                    IntervalTreeNodeMetadata.SetNodeColor(w, IntervalTreeNodeMetadata.GetNodeColor(x.Parent));
                    IntervalTreeNodeMetadata.SetNodeColor(x.Parent, IntervalNodeColor.Black);
                    IntervalTreeNodeMetadata.SetNodeColor(w.Left, IntervalNodeColor.Black);
                    RightRotate(T, x.Parent);
                    x = T.Root;
                }
            }
        }

        IntervalTreeNodeMetadata.SetNodeColor(x, IntervalNodeColor.Black);
        ResetSentinel(T);
    }

    private static IntervalNode Leftest(IntervalTree T, IntervalNode node)
    {
        while (!ReferenceEquals(node.Left, T.Sentinel))
        {
            node = node.Left;
        }
        return node;
    }

    private static void ResetSentinel(IntervalTree T)
    {
        T.Sentinel.Parent = T.Sentinel;
        T.Sentinel.Delta = 0; // optional
        T.Sentinel.Start = 0; // optional
        T.Sentinel.End = 0; // optional
    }

    private static void LeftRotate(IntervalTree T, IntervalNode x)
    {
        IntervalNode y = x.Right; // set y.

        y.Delta += x.Delta; // y's delta is no longer influenced by x's delta
        if (y.Delta < IntervalTreeNodeMetadata.MinSafeDelta || y.Delta > IntervalTreeNodeMetadata.MaxSafeDelta)
        {
            T.RequestNormalizeDelta = true;
        }
        y.Start += x.Delta;
        y.End += x.Delta;

        x.Right = y.Left; // turn y's left subtree into x's right subtree.
        if (!ReferenceEquals(y.Left, T.Sentinel))
        {
            y.Left.Parent = x;
        }
        y.Parent = x.Parent; // link x's parent to y.
        if (ReferenceEquals(x.Parent, T.Sentinel))
        {
            T.Root = y;
        }
        else if (ReferenceEquals(x, x.Parent.Left))
        {
            x.Parent.Left = y;
        }
        else
        {
            x.Parent.Right = y;
        }

        y.Left = x; // put x on y's left.
        x.Parent = y;

        RecomputeMaxEnd(T, x);
        RecomputeMaxEnd(T, y);
    }

    private static void RightRotate(IntervalTree T, IntervalNode y)
    {
        IntervalNode x = y.Left;

        y.Delta -= x.Delta;
        if (y.Delta < IntervalTreeNodeMetadata.MinSafeDelta || y.Delta > IntervalTreeNodeMetadata.MaxSafeDelta)
        {
            T.RequestNormalizeDelta = true;
        }
        y.Start -= x.Delta;
        y.End -= x.Delta;

        y.Left = x.Right;
        if (!ReferenceEquals(x.Right, T.Sentinel))
        {
            x.Right.Parent = y;
        }
        x.Parent = y.Parent;
        if (ReferenceEquals(y.Parent, T.Sentinel))
        {
            T.Root = x;
        }
        else if (ReferenceEquals(y, y.Parent.Right))
        {
            y.Parent.Right = x;
        }
        else
        {
            y.Parent.Left = x;
        }

        x.Right = y;
        y.Parent = x;

        RecomputeMaxEnd(T, y);
        RecomputeMaxEnd(T, x);
    }
}
