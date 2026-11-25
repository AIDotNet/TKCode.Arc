// Ported from vscode (src/vs/editor/common/model/bracketPairsTextModelPart/bracketPairsTree/concat23Trees.ts, commit
// b57d5c9e), MIT license.

namespace TKCode.Arc.Editor.Model.BracketPairs;

/// <summary>Concatenates (2,3) trees.</summary>
internal static class Concat23Trees
{
    /// <summary>
    /// Concatenates a list of (2,3) AstNode's into a single (2,3) AstNode. This mutates the items of the input array!
    /// If all items have the same height, this method has runtime O(items.length). Otherwise, it has runtime
    /// O(items.length * max(log(items.length), items.max(i => i.height))).
    /// </summary>
    public static AstNode? Concat(List<AstNode> items)
    {
        if (items.Count == 0)
        {
            return null;
        }

        if (items.Count == 1)
        {
            return items[0];
        }

        var i = 0;

        // Reads nodes of same height and concatenates them to a single node.
        AstNode? ReadNode()
        {
            if (i >= items.Count)
            {
                return null;
            }

            var start = i;
            var height = items[start].ListHeight;

            i++;
            while (i < items.Count && items[i].ListHeight == height)
            {
                i++;
            }

            if (i - start >= 2)
            {
                return ConcatOfSameHeight(start == 0 && i == items.Count ? items : items.GetRange(start, i - start), false);
            }

            return items[start];
        }

        // The items might not have the same height.
        // We merge all items by using a binary concat operator.
        var first = ReadNode()!; // There must be a first item
        var second = ReadNode();
        if (second is null)
        {
            return first;
        }

        for (var item = ReadNode(); item is not null; item = ReadNode())
        {
            // Prefer concatenating smaller trees, as the runtime of concat depends on the tree height.
            if (HeightDiff(first, second) <= HeightDiff(second, item))
            {
                first = ConcatTwo(first, second);
                second = item;
            }
            else
            {
                second = ConcatTwo(second, item);
            }
        }

        return ConcatTwo(first, second);
    }

    public static AstNode? ConcatOfSameHeight(List<AstNode> items, bool createImmutableLists = false)
    {
        if (items.Count == 0)
        {
            return null;
        }

        if (items.Count == 1)
        {
            return items[0];
        }

        var length = items.Count;
        // All trees have same height, just create parent nodes.
        while (length > 3)
        {
            var newLength = length >> 1;
            for (var i = 0; i < newLength; i++)
            {
                var j = i << 1;
                items[i] = ListAstNode.Create23(items[j], items[j + 1], j + 3 == length ? items[j + 2] : null, createImmutableLists);
            }

            length = newLength;
        }

        return ListAstNode.Create23(items[0], items[1], length >= 3 ? items[2] : null, createImmutableLists);
    }

    private static int HeightDiff(AstNode node1, AstNode node2) => Math.Abs(node1.ListHeight - node2.ListHeight);

    private static AstNode ConcatTwo(AstNode node1, AstNode node2)
    {
        if (node1.ListHeight == node2.ListHeight)
        {
            return ListAstNode.Create23(node1, node2, null, false);
        }

        if (node1.ListHeight > node2.ListHeight)
        {
            // node1 is the tree we want to insert into
            return Append((ListAstNode)node1, node2);
        }

        return Prepend((ListAstNode)node2, node1);
    }

    /// <summary>
    /// Appends the given node to the end of this (2,3) tree. Returns the new root.
    /// </summary>
    private static AstNode Append(ListAstNode list, AstNode nodeToAppend)
    {
        list = list.ToMutable();
        AstNode curNode = list;
        var parents = new List<ListAstNode>();
        AstNode? nodeToAppendOfCorrectHeight;
        while (true)
        {
            // assert nodeToInsert.listHeight <= curNode.listHeight
            if (nodeToAppend.ListHeight == curNode.ListHeight)
            {
                nodeToAppendOfCorrectHeight = nodeToAppend;
                break;
            }

            // assert 0 <= nodeToInsert.listHeight < curNode.listHeight
            if (curNode is not ListAstNode curList)
            {
                throw new InvalidOperationException("unexpected");
            }

            parents.Add(curList);
            // assert 2 <= curNode.childrenLength <= 3
            curNode = curList.MakeLastElementMutable()!;
        }

        // assert nodeToAppendOfCorrectHeight!.listHeight === curNode.listHeight
        for (var i = parents.Count - 1; i >= 0; i--)
        {
            var parent = parents[i];
            if (nodeToAppendOfCorrectHeight is not null)
            {
                // Can we take the element?
                if (parent.ChildrenLength >= 3)
                {
                    // we need to split to maintain (2,3)-tree property.
                    // Send the third element + the new element to the parent.
                    nodeToAppendOfCorrectHeight = ListAstNode.Create23(parent.UnappendChild()!, nodeToAppendOfCorrectHeight, null, false);
                }
                else
                {
                    parent.AppendChildOfSameHeight(nodeToAppendOfCorrectHeight);
                    nodeToAppendOfCorrectHeight = null;
                }
            }
            else
            {
                parent.HandleChildrenChanged();
            }
        }

        return nodeToAppendOfCorrectHeight is not null
            ? ListAstNode.Create23(list, nodeToAppendOfCorrectHeight, null, false)
            : list;
    }

    /// <summary>
    /// Prepends the given node to the start of this (2,3) tree. Returns the new root.
    /// </summary>
    private static AstNode Prepend(ListAstNode list, AstNode nodeToAppend)
    {
        list = list.ToMutable();
        AstNode curNode = list;
        var parents = new List<ListAstNode>();
        // assert nodeToInsert.listHeight <= curNode.listHeight
        while (nodeToAppend.ListHeight != curNode.ListHeight)
        {
            // assert 0 <= nodeToInsert.listHeight < curNode.listHeight
            if (curNode is not ListAstNode curList)
            {
                throw new InvalidOperationException("unexpected");
            }

            parents.Add(curList);
            // assert 2 <= curNode.childrenFast.length <= 3
            curNode = curList.MakeFirstElementMutable()!;
        }

        AstNode? nodeToPrependOfCorrectHeight = nodeToAppend;
        // assert nodeToAppendOfCorrectHeight!.listHeight === curNode.listHeight
        for (var i = parents.Count - 1; i >= 0; i--)
        {
            var parent = parents[i];
            if (nodeToPrependOfCorrectHeight is not null)
            {
                // Can we take the element?
                if (parent.ChildrenLength >= 3)
                {
                    // we need to split to maintain (2,3)-tree property.
                    // Send the third element + the new element to the parent.
                    nodeToPrependOfCorrectHeight = ListAstNode.Create23(nodeToPrependOfCorrectHeight, parent.UnprependChild()!, null, false);
                }
                else
                {
                    parent.PrependChildOfSameHeight(nodeToPrependOfCorrectHeight);
                    nodeToPrependOfCorrectHeight = null;
                }
            }
            else
            {
                parent.HandleChildrenChanged();
            }
        }

        return nodeToPrependOfCorrectHeight is not null
            ? ListAstNode.Create23(nodeToPrependOfCorrectHeight, list, null, false)
            : list;
    }
}
