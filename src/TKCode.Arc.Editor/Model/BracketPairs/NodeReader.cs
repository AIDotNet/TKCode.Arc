// Ported from vscode (src/vs/editor/common/model/bracketPairsTextModelPart/bracketPairsTree/nodeReader.ts, commit
// b57d5c9e), MIT license.

using static TKCode.Arc.Editor.Model.BracketPairs.LengthHelpers;

namespace TKCode.Arc.Editor.Model.BracketPairs;

/// <summary>
/// Allows to efficiently find a longest child at a given offset in a fixed node. The requested offsets must increase
/// monotonously.
/// </summary>
internal sealed class NodeReader
{
    private readonly List<AstNode> _nextNodes;
    private readonly List<Length> _offsets;
    private readonly List<int> _idxs;
    private Length _lastOffset = Length.Zero;

    public NodeReader(AstNode node)
    {
        _nextNodes = [node];
        _offsets = [Length.Zero];
        _idxs = [];
    }

    /// <summary>Returns the longest node at <paramref name="offset"/> that satisfies the predicate.</summary>
    /// <param name="offset">Must be greater than or equal to the last offset this method has been called with!</param>
    /// <param name="predicate">Whether a node may be reused.</param>
    public AstNode? ReadLongestNodeAt(Length offset, Func<AstNode, bool> predicate)
    {
        if (LengthLessThan(offset, _lastOffset))
        {
            throw new InvalidOperationException("Invalid offset");
        }

        _lastOffset = offset;

        // Find the longest node of all those that are closest to the current offset.
        while (true)
        {
            if (_nextNodes.Count == 0)
            {
                return null;
            }

            var curNode = _nextNodes[^1];
            var curNodeOffset = _offsets[^1];

            if (LengthLessThan(offset, curNodeOffset))
            {
                // The next best node is not here yet.
                // The reader must advance before a cached node is hit.
                return null;
            }

            if (LengthLessThan(curNodeOffset, offset))
            {
                // The reader is ahead of the current node.
                if (LengthAdd(curNodeOffset, curNode.Length) <= offset)
                {
                    // The reader is after the end of the current node.
                    NextNodeAfterCurrent();
                }
                else
                {
                    // The reader is somewhere in the current node.
                    var nextChildIdx = GetNextChildIdx(curNode);
                    if (nextChildIdx != -1)
                    {
                        // Go to the first child and repeat.
                        _nextNodes.Add(curNode.GetChild(nextChildIdx)!);
                        _offsets.Add(curNodeOffset);
                        _idxs.Add(nextChildIdx);
                    }
                    else
                    {
                        // We don't have children
                        NextNodeAfterCurrent();
                    }
                }
            }
            else
            {
                // readerOffsetBeforeChange === curNodeOffset
                if (predicate(curNode))
                {
                    NextNodeAfterCurrent();
                    return curNode;
                }

                var nextChildIdx = GetNextChildIdx(curNode);
                // look for shorter node
                if (nextChildIdx == -1)
                {
                    // There is no shorter node.
                    NextNodeAfterCurrent();
                    return null;
                }

                // Descend into first child & repeat.
                _nextNodes.Add(curNode.GetChild(nextChildIdx)!);
                _offsets.Add(curNodeOffset);
                _idxs.Add(nextChildIdx);
            }
        }
    }

    // Navigates to the longest node that continues after the current node.
    private void NextNodeAfterCurrent()
    {
        while (true)
        {
            var currentOffset = _offsets.Count > 0 ? _offsets[^1] : Length.Zero;
            var currentNode = _nextNodes.Count > 0 ? _nextNodes[^1] : null;
            _nextNodes.RemoveAt(_nextNodes.Count - 1);
            _offsets.RemoveAt(_offsets.Count - 1);

            if (_idxs.Count == 0)
            {
                // We just popped the root node, there is no next node.
                break;
            }

            // Parent is not undefined, because idxs is not empty
            var parent = _nextNodes[^1];
            var nextChildIdx = GetNextChildIdx(parent, _idxs[^1]);

            if (nextChildIdx != -1)
            {
                _nextNodes.Add(parent.GetChild(nextChildIdx)!);
                _offsets.Add(LengthAdd(currentOffset, currentNode!.Length));
                _idxs[^1] = nextChildIdx;
                break;
            }

            _idxs.RemoveAt(_idxs.Count - 1);
            // We fully consumed the parent.
            // Current node is now parent, so call nextNodeAfterCurrent again
        }
    }

    private static int GetNextChildIdx(AstNode node, int curIdx = -1)
    {
        while (true)
        {
            curIdx++;
            if (curIdx >= node.ChildrenLength)
            {
                return -1;
            }

            if (node.GetChild(curIdx) is not null)
            {
                return curIdx;
            }
        }
    }
}
