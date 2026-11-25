// Ported from vscode's src/vs/editor/common/model/intervalTree.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// normalizeDelta: folds the accumulated deltas back into the absolute positions when they grow too large.

namespace TKCode.Arc.Editor.Model;

internal sealed partial class IntervalTree
{
    private static void NormalizeDelta(IntervalTree T)
    {
        IntervalNode node = T.Root;
        int delta = 0;
        while (!ReferenceEquals(node, T.Sentinel))
        {
            if (!ReferenceEquals(node.Left, T.Sentinel) && !IntervalTreeNodeMetadata.GetNodeIsVisited(node.Left))
            {
                // go left
                node = node.Left;
                continue;
            }

            if (!ReferenceEquals(node.Right, T.Sentinel) && !IntervalTreeNodeMetadata.GetNodeIsVisited(node.Right))
            {
                // go right
                delta += node.Delta;
                node = node.Right;
                continue;
            }

            // handle current node
            node.Start = delta + node.Start;
            node.End = delta + node.End;
            node.Delta = 0;
            RecomputeMaxEnd(T, node);

            IntervalTreeNodeMetadata.SetNodeIsVisited(node, true);

            // going up from this node
            IntervalTreeNodeMetadata.SetNodeIsVisited(node.Left, false);
            IntervalTreeNodeMetadata.SetNodeIsVisited(node.Right, false);
            if (ReferenceEquals(node, node.Parent.Right))
            {
                delta -= node.Parent.Delta;
            }
            node = node.Parent;
        }

        IntervalTreeNodeMetadata.SetNodeIsVisited(T.Root, false);
    }
}
