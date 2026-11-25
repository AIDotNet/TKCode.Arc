// Ported from vscode's src/vs/editor/common/model/intervalTree.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// Editing: nodeAcceptEdit with the stickiness rules, searchForEditing and noOverlapReplace.

namespace TKCode.Arc.Editor.Model;

internal sealed partial class IntervalTree
{
    private enum MarkerMoveSemantics
    {
        MarkerDefined = 0,
        ForceMove = 1,
        ForceStay = 2,
    }

    private static bool AdjustMarkerBeforeColumn(
        int markerOffset,
        bool markerStickToPreviousCharacter,
        int checkOffset,
        MarkerMoveSemantics moveSemantics)
    {
        if (markerOffset < checkOffset)
        {
            return true;
        }
        if (markerOffset > checkOffset)
        {
            return false;
        }
        if (moveSemantics == MarkerMoveSemantics.ForceMove)
        {
            return false;
        }
        if (moveSemantics == MarkerMoveSemantics.ForceStay)
        {
            return true;
        }
        return markerStickToPreviousCharacter;
    }

    /// <summary>
    /// This is a lot more complicated than strictly necessary to maintain the same behaviour
    /// as when decorations were implemented using two markers.
    /// </summary>
    internal static void NodeAcceptEdit(IntervalNode node, int start, int end, int textLength, bool forceMoveMarkers)
    {
        TrackedRangeStickiness nodeStickiness = IntervalTreeNodeMetadata.GetNodeStickiness(node);
        bool startStickToPreviousCharacter = (
            nodeStickiness == TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges
            || nodeStickiness == TrackedRangeStickiness.GrowsOnlyWhenTypingBefore
        );
        bool endStickToPreviousCharacter = (
            nodeStickiness == TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges
            || nodeStickiness == TrackedRangeStickiness.GrowsOnlyWhenTypingBefore
        );

        int deletingCnt = end - start;
        int insertingCnt = textLength;
        int commonLength = Math.Min(deletingCnt, insertingCnt);

        int nodeStart = node.Start;
        bool startDone = false;

        int nodeEnd = node.End;
        bool endDone = false;

        if (start <= nodeStart && nodeEnd <= end && IntervalTreeNodeMetadata.GetCollapseOnReplaceEdit(node))
        {
            // This edit encompasses the entire decoration range
            // and the decoration has asked to become collapsed
            node.Start = start;
            startDone = true;
            node.End = start;
            endDone = true;
        }

        {
            MarkerMoveSemantics moveSemantics = forceMoveMarkers
                ? MarkerMoveSemantics.ForceMove
                : (deletingCnt > 0 ? MarkerMoveSemantics.ForceStay : MarkerMoveSemantics.MarkerDefined);
            if (!startDone && AdjustMarkerBeforeColumn(nodeStart, startStickToPreviousCharacter, start, moveSemantics))
            {
                startDone = true;
            }
            if (!endDone && AdjustMarkerBeforeColumn(nodeEnd, endStickToPreviousCharacter, start, moveSemantics))
            {
                endDone = true;
            }
        }

        if (commonLength > 0 && !forceMoveMarkers)
        {
            MarkerMoveSemantics moveSemantics = deletingCnt > insertingCnt
                ? MarkerMoveSemantics.ForceStay
                : MarkerMoveSemantics.MarkerDefined;
            if (!startDone && AdjustMarkerBeforeColumn(nodeStart, startStickToPreviousCharacter, start + commonLength, moveSemantics))
            {
                startDone = true;
            }
            if (!endDone && AdjustMarkerBeforeColumn(nodeEnd, endStickToPreviousCharacter, start + commonLength, moveSemantics))
            {
                endDone = true;
            }
        }

        {
            MarkerMoveSemantics moveSemantics = forceMoveMarkers
                ? MarkerMoveSemantics.ForceMove
                : MarkerMoveSemantics.MarkerDefined;
            if (!startDone && AdjustMarkerBeforeColumn(nodeStart, startStickToPreviousCharacter, end, moveSemantics))
            {
                node.Start = start + insertingCnt;
                startDone = true;
            }
            if (!endDone && AdjustMarkerBeforeColumn(nodeEnd, endStickToPreviousCharacter, end, moveSemantics))
            {
                node.End = start + insertingCnt;
                endDone = true;
            }
        }

        // Finish
        int deltaColumn = insertingCnt - deletingCnt;
        if (!startDone)
        {
            node.Start = Math.Max(0, nodeStart + deltaColumn);
        }
        if (!endDone)
        {
            node.End = Math.Max(0, nodeEnd + deltaColumn);
        }

        if (node.Start > node.End)
        {
            node.End = node.Start;
        }
    }

    private static List<IntervalNode> SearchForEditing(IntervalTree T, int start, int end)
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
                if (nodeMaxEnd < start)
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
            if (nodeStart > end)
            {
                // cover case a) from above
                // there is no need to search this node or its right subtree
                IntervalTreeNodeMetadata.SetNodeIsVisited(node, true);
                continue;
            }

            nodeEnd = delta + node.End;
            if (nodeEnd >= start)
            {
                node.SetCachedOffsets(nodeStart, nodeEnd, 0);
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

    private static void NoOverlapReplace(IntervalTree T, int start, int end, int textLength)
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
        int editDelta = textLength - (end - start);
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
                RecomputeMaxEnd(T, node);
                node = node.Parent;
                continue;
            }

            if (!IntervalTreeNodeMetadata.GetNodeIsVisited(node.Left))
            {
                // first time seeing this node
                nodeMaxEnd = delta + node.MaxEnd;
                if (nodeMaxEnd < start)
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
            if (nodeStart > end)
            {
                node.Start += editDelta;
                node.End += editDelta;
                node.Delta += editDelta;
                if (node.Delta < IntervalTreeNodeMetadata.MinSafeDelta || node.Delta > IntervalTreeNodeMetadata.MaxSafeDelta)
                {
                    T.RequestNormalizeDelta = true;
                }
                // cover case a) from above
                // there is no need to search this node or its right subtree
                IntervalTreeNodeMetadata.SetNodeIsVisited(node, true);
                continue;
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
    }
}
