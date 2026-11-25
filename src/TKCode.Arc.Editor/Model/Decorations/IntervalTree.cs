// Ported from vscode's src/vs/editor/common/model/intervalTree.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// IntervalTree: the red-black interval tree and its public operations.
//
// The TS source uses one module-level SENTINEL node. This engine runs trees for different models on different
// threads, so each tree owns its sentinel (Sentinel) instead.

namespace TKCode.Arc.Editor.Model;

/// <summary>
/// A red-black interval tree of decoration nodes, augmented with each subtree's maximum end (Monaco's
/// <c>IntervalTree</c>). The start/end pairs are column offsets in the text model.
/// </summary>
internal sealed partial class IntervalTree
{
    /// <summary>The nil leaf of this tree; every link that has no node points here.</summary>
    internal IntervalNode Sentinel { get; }

    internal IntervalNode Root { get; set; }

    internal bool RequestNormalizeDelta { get; set; }

    internal IntervalTree()
    {
        Sentinel = new IntervalNode(null, 0, 0);
        Sentinel.Parent = Sentinel;
        Sentinel.Left = Sentinel;
        Sentinel.Right = Sentinel;
        IntervalTreeNodeMetadata.SetNodeColor(Sentinel, IntervalNodeColor.Black);
        Root = Sentinel;
        RequestNormalizeDelta = false;
    }

    internal List<IntervalNode> IntervalSearch(
        int start,
        int end,
        int filterOwnerId,
        bool filterOutValidation,
        bool filterFontDecorations,
        int cachedVersionId,
        bool onlyMarginDecorations)
    {
        if (ReferenceEquals(Root, Sentinel))
        {
            return new List<IntervalNode>();
        }
        return IntervalSearch(this, start, end, filterOwnerId, filterOutValidation, filterFontDecorations, cachedVersionId, onlyMarginDecorations);
    }

    internal List<IntervalNode> Search(
        int filterOwnerId,
        bool filterOutValidation,
        bool filterFontDecorations,
        int cachedVersionId,
        bool onlyMarginDecorations)
    {
        if (ReferenceEquals(Root, Sentinel))
        {
            return new List<IntervalNode>();
        }
        return Search(this, filterOwnerId, filterOutValidation, filterFontDecorations, cachedVersionId, onlyMarginDecorations);
    }

    /// <summary>
    /// Will not set <c>CachedAbsoluteStart</c> nor <c>CachedAbsoluteEnd</c> on the returned nodes.
    /// </summary>
    internal List<IntervalNode> CollectNodesFromOwner(int ownerId)
    {
        return CollectNodesFromOwner(this, ownerId);
    }

    /// <summary>
    /// Will not set <c>CachedAbsoluteStart</c> nor <c>CachedAbsoluteEnd</c> on the returned nodes.
    /// </summary>
    internal List<IntervalNode> CollectNodesPostOrder()
    {
        return CollectNodesPostOrder(this);
    }

    internal void Insert(IntervalNode node)
    {
        RbTreeInsert(this, node);
        NormalizeDeltaIfNecessary();
    }

    internal void Delete(IntervalNode node)
    {
        RbTreeDelete(this, node);
        NormalizeDeltaIfNecessary();
    }

    internal void ResolveNode(IntervalNode node, int cachedVersionId)
    {
        IntervalNode initialNode = node;
        int delta = 0;
        while (!ReferenceEquals(node, Root))
        {
            if (ReferenceEquals(node, node.Parent.Right))
            {
                delta += node.Parent.Delta;
            }
            node = node.Parent;
        }

        int nodeStart = initialNode.Start + delta;
        int nodeEnd = initialNode.End + delta;
        initialNode.SetCachedOffsets(nodeStart, nodeEnd, cachedVersionId);
    }

    internal void AcceptReplace(int offset, int length, int textLength, bool forceMoveMarkers)
    {
        // Our strategy is to remove all directly impacted nodes, and then add them back to the tree.

        // (1) collect all nodes that are intersecting this edit as nodes of interest
        List<IntervalNode> nodesOfInterest = SearchForEditing(this, offset, offset + length);

        // (2) remove all nodes that are intersecting this edit
        for (int i = 0, len = nodesOfInterest.Count; i < len; i++)
        {
            IntervalNode node = nodesOfInterest[i];
            RbTreeDelete(this, node);
        }
        NormalizeDeltaIfNecessary();

        // (3) edit all tree nodes except the nodes of interest
        NoOverlapReplace(this, offset, offset + length, textLength);
        NormalizeDeltaIfNecessary();

        // (4) edit the nodes of interest and insert them back in the tree
        for (int i = 0, len = nodesOfInterest.Count; i < len; i++)
        {
            IntervalNode node = nodesOfInterest[i];
            node.Start = node.CachedAbsoluteStart;
            node.End = node.CachedAbsoluteEnd;
            NodeAcceptEdit(node, offset, offset + length, textLength, forceMoveMarkers);
            node.MaxEnd = node.End;
            RbTreeInsert(this, node);
        }
        NormalizeDeltaIfNecessary();
    }

    internal List<IntervalNode> GetAllInOrder()
    {
        return Search(this, 0, false, false, 0, false);
    }

    private void NormalizeDeltaIfNecessary()
    {
        if (!RequestNormalizeDelta)
        {
            return;
        }
        RequestNormalizeDelta = false;
        NormalizeDelta(this);
    }
}
