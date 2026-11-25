// Ported from vscode's src/vs/editor/common/model/intervalTree.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// The augmented max-end maintenance and intervalCompare.

namespace TKCode.Arc.Editor.Model;

internal sealed partial class IntervalTree
{
    private static int ComputeMaxEnd(IntervalTree T, IntervalNode node)
    {
        int maxEnd = node.End;
        if (!ReferenceEquals(node.Left, T.Sentinel))
        {
            int leftMaxEnd = node.Left.MaxEnd;
            if (leftMaxEnd > maxEnd)
            {
                maxEnd = leftMaxEnd;
            }
        }
        if (!ReferenceEquals(node.Right, T.Sentinel))
        {
            int rightMaxEnd = node.Right.MaxEnd + node.Delta;
            if (rightMaxEnd > maxEnd)
            {
                maxEnd = rightMaxEnd;
            }
        }
        return maxEnd;
    }

    internal static void RecomputeMaxEnd(IntervalTree T, IntervalNode node)
    {
        node.MaxEnd = ComputeMaxEnd(T, node);
    }

    private static void RecomputeMaxEndWalkToRoot(IntervalTree T, IntervalNode node)
    {
        while (!ReferenceEquals(node, T.Sentinel))
        {
            int maxEnd = ComputeMaxEnd(T, node);

            if (node.MaxEnd == maxEnd)
            {
                // no need to go further
                return;
            }

            node.MaxEnd = maxEnd;
            node = node.Parent;
        }
    }

    internal static int IntervalCompare(int aStart, int aEnd, int bStart, int bEnd)
    {
        if (aStart == bStart)
        {
            return aEnd - bEnd;
        }
        return aStart - bStart;
    }
}
