// Ported from vscode's src/vs/editor/common/model/intervalTree.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// The tree searches: intervalSearch, search, collectNodesFromOwner and collectNodesPostOrder.

namespace TKCode.Arc.Editor.Model;

internal sealed partial class IntervalTree
{
    private static List<IntervalNode> CollectNodesFromOwner(IntervalTree T, int ownerId)
    {
        IntervalNode node = T.Root;
        List<IntervalNode> result = new List<IntervalNode>();
        while (!ReferenceEquals(node, T.Sentinel))
        {
            if (IntervalTreeNodeMetadata.GetNodeIsVisited(node))
            {
                // going up from this node
                IntervalTreeNodeMetadata.SetNodeIsVisited(node.Left, false);
                IntervalTreeNodeMetadata.SetNodeIsVisited(node.Right, false);
                node = node.Parent;
                continue;
            }

            if (!ReferenceEquals(node.Left, T.Sentinel) && !IntervalTreeNodeMetadata.GetNodeIsVisited(node.Left))
            {
                // go left
                node = node.Left;
                continue;
            }

            // handle current node
            if (node.OwnerId == ownerId)
            {
                result.Add(node);
            }

            IntervalTreeNodeMetadata.SetNodeIsVisited(node, true);

            if (!ReferenceEquals(node.Right, T.Sentinel) && !IntervalTreeNodeMetadata.GetNodeIsVisited(node.Right))
            {
                // go right
                node = node.Right;
                continue;
            }
        }

        IntervalTreeNodeMetadata.SetNodeIsVisited(T.Root, false);

        return result;
    }

    private static List<IntervalNode> CollectNodesPostOrder(IntervalTree T)
    {
        IntervalNode node = T.Root;
        List<IntervalNode> result = new List<IntervalNode>();
        while (!ReferenceEquals(node, T.Sentinel))
        {
            if (IntervalTreeNodeMetadata.GetNodeIsVisited(node))
            {
                // going up from this node
                IntervalTreeNodeMetadata.SetNodeIsVisited(node.Left, false);
                IntervalTreeNodeMetadata.SetNodeIsVisited(node.Right, false);
                node = node.Parent;
                continue;
            }

            if (!ReferenceEquals(node.Left, T.Sentinel) && !IntervalTreeNodeMetadata.GetNodeIsVisited(node.Left))
            {
                // go left
                node = node.Left;
                continue;
            }

            if (!ReferenceEquals(node.Right, T.Sentinel) && !IntervalTreeNodeMetadata.GetNodeIsVisited(node.Right))
            {
                // go right
                node = node.Right;
                continue;
            }

            // handle current node
            result.Add(node);
            IntervalTreeNodeMetadata.SetNodeIsVisited(node, true);
        }

        IntervalTreeNodeMetadata.SetNodeIsVisited(T.Root, false);

        return result;
    }

    private static List<IntervalNode> Search(
        IntervalTree T,
        int filterOwnerId,
        bool filterOutValidation,
        bool filterFontDecorations,
        int cachedVersionId,
        bool onlyMarginDecorations)
    {
        IntervalNode node = T.Root;
        int delta = 0;
        int nodeStart = 0;
        int nodeEnd = 0;
        List<IntervalNode> result = new List<IntervalNode>();
        while (!ReferenceEquals(node, T.Sentinel))
        {
            if (IntervalTreeNodeMetadata.GetNodeIsVisited(node))
            {
                // going up from this node
                IntervalTreeNodeMetadata.SetNodeIsVisited(node.Left, false);
                IntervalTreeNodeMetadata.SetNodeIsVisited(node.Right, false);
                if (ReferenceEquals(node, node.Parent.Right))
                {
                    delta -= node.Parent.Delta;
                }
                node = node.Parent;
                continue;
            }

            if (!ReferenceEquals(node.Left, T.Sentinel) && !IntervalTreeNodeMetadata.GetNodeIsVisited(node.Left))
            {
                // go left
                node = node.Left;
                continue;
            }

            // handle current node
            nodeStart = delta + node.Start;
            nodeEnd = delta + node.End;

            node.SetCachedOffsets(nodeStart, nodeEnd, cachedVersionId);

            bool include = true;
            if (filterOwnerId != 0 && node.OwnerId != 0 && node.OwnerId != filterOwnerId)
            {
                include = false;
            }
            if (filterOutValidation && IntervalTreeNodeMetadata.GetNodeIsForValidation(node))
            {
                include = false;
            }
            if (filterFontDecorations && IntervalTreeNodeMetadata.GetNodeAffectsFont(node))
            {
                include = false;
            }
            if (onlyMarginDecorations && !IntervalTreeNodeMetadata.GetNodeIsInGlyphMargin(node))
            {
                include = false;
            }

            if (include)
            {
                result.Add(node);
            }

            IntervalTreeNodeMetadata.SetNodeIsVisited(node, true);

            if (!ReferenceEquals(node.Right, T.Sentinel) && !IntervalTreeNodeMetadata.GetNodeIsVisited(node.Right))
            {
                // go right
                delta += node.Delta;
                node = node.Right;
                continue;
            }
        }

        IntervalTreeNodeMetadata.SetNodeIsVisited(T.Root, false);

        return result;
    }

    private static List<IntervalNode> IntervalSearch(
        IntervalTree T,
        int intervalStart,
        int intervalEnd,
        int filterOwnerId,
        bool filterOutValidation,
        bool filterFontDecorations,
        int cachedVersionId,
        bool onlyMarginDecorations)
    {
        // https://en.wikipedia.org/wiki/Interval_tree#Augmented_tree
        // Now, it is known that two intervals A and B overlap only when both
        // A.low <= B.high and A.high >= B.low. When searching the trees for
        // nodes overlapping with a given interval, you can immediately skip:
        //  a) all nodes to the right of nodes whose low value is past the end of the given interval.
        //  b) all nodes that have their maximum 'high' value below the start of the given interval.

        IntervalNode node = T.Root;
        int delta = 0;
        int nodeMaxEnd = 0;
        int nodeStart = 0;
        int nodeEnd = 0;
        List<IntervalNode> result = new List<IntervalNode>();
        while (!ReferenceEquals(node, T.Sentinel))
        {
            if (IntervalTreeNodeMetadata.GetNodeIsVisited(node))
            {
                // going up from this node
                IntervalTreeNodeMetadata.SetNodeIsVisited(node.Left, false);
                IntervalTreeNodeMetadata.SetNodeIsVisited(node.Right, false);
                if (ReferenceEquals(node, node.Parent.Right))
                {
                    delta -= node.Parent.Delta;
                }
                node = node.Parent;
                continue;
            }

            if (!IntervalTreeNodeMetadata.GetNodeIsVisited(node.Left))
            {
                // first time seeing this node
                nodeMaxEnd = delta + node.MaxEnd;
                if (nodeMaxEnd < intervalStart)
                {
                    // cover case b) from above
                    // there is no need to search this node or its children
                    IntervalTreeNodeMetadata.SetNodeIsVisited(node, true);
                    continue;
                }

                if (!ReferenceEquals(node.Left, T.Sentinel))
                {
                    // go left
                    node = node.Left;
                    continue;
                }
            }

            // handle current node
            nodeStart = delta + node.Start;
            if (nodeStart > intervalEnd)
            {
                // cover case a) from above
                // there is no need to search this node or its right subtree
                IntervalTreeNodeMetadata.SetNodeIsVisited(node, true);
                continue;
            }

            nodeEnd = delta + node.End;

            if (nodeEnd >= intervalStart)
            {
                // There is overlap
                node.SetCachedOffsets(nodeStart, nodeEnd, cachedVersionId);

                bool include = true;
                if (filterOwnerId != 0 && node.OwnerId != 0 && node.OwnerId != filterOwnerId)
                {
                    include = false;
                }
                if (filterOutValidation && IntervalTreeNodeMetadata.GetNodeIsForValidation(node))
                {
                    include = false;
                }
                if (filterFontDecorations && IntervalTreeNodeMetadata.GetNodeAffectsFont(node))
                {
                    include = false;
                }
                if (onlyMarginDecorations && !IntervalTreeNodeMetadata.GetNodeIsInGlyphMargin(node))
                {
                    include = false;
                }

                if (include)
                {
                    result.Add(node);
                }
            }

            IntervalTreeNodeMetadata.SetNodeIsVisited(node, true);

            if (!ReferenceEquals(node.Right, T.Sentinel) && !IntervalTreeNodeMetadata.GetNodeIsVisited(node.Right))
            {
                // go right
                delta += node.Delta;
                node = node.Right;
                continue;
            }
        }

        IntervalTreeNodeMetadata.SetNodeIsVisited(T.Root, false);

        return result;
    }
}
