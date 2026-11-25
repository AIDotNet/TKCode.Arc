// Ported from vscode (src/vs/editor/common/model/bracketPairsTextModelPart/bracketPairsTree/ast.ts, commit b57d5c9e),
// MIT license.
//
// Differences: AstNode is the abstract base class (upstream: a union type over BaseAstNode's subclasses); code tests the
// kind with type patterns as well as Kind; OpeningBracketId is int; Number.MAX_SAFE_INTEGER is int.MaxValue.


using TKCode.Arc.Editor.Languages;
using static TKCode.Arc.Editor.Model.BracketPairs.LengthHelpers;

namespace TKCode.Arc.Editor.Model.BracketPairs;

internal enum AstNodeKind
{
    Text = 0,
    Bracket = 1,
    Pair = 2,
    UnexpectedClosingBracket = 3,
    List = 4,
}

/// <summary>The base implementation for all AST nodes.</summary>
internal abstract class AstNode
{
    protected AstNode(Length length)
    {
        LengthValue = length;
    }

    public abstract AstNodeKind Kind { get; }

    public abstract int ChildrenLength { get; }

    /// <summary>Might return null even if <paramref name="idx"/> is smaller than <see cref="ChildrenLength"/>.</summary>
    public abstract AstNode? GetChild(int idx);

    /// <summary>Try to avoid using this property, as implementations might need to allocate the resulting array.</summary>
    public abstract IReadOnlyList<AstNode> Children { get; }

    /// <summary>
    /// Represents the set of all (potentially) missing opening bracket ids in this node. E.g. in <c>{ ] ) }</c> that set
    /// is {<c>[</c>, <c>(</c>}.
    /// </summary>
    public abstract SmallImmutableSet<int> MissingOpeningBracketIds { get; }

    /// <summary>In case of a list, determines the height of the (2,3) tree.</summary>
    public abstract int ListHeight { get; }

    protected Length LengthValue { get; set; }

    /// <summary>The length of the entire node, which should equal the sum of lengths of all children.</summary>
    public Length Length => LengthValue;

    /// <param name="openBracketIds">The set of all opening brackets that have not yet been closed.</param>
    public abstract bool CanBeReused(SmallImmutableSet<int> openBracketIds);

    /// <summary>Flattens all lists in this AST. Only for debugging.</summary>
    public abstract AstNode FlattenLists();

    /// <summary>Creates a deep clone.</summary>
    public abstract AstNode DeepClone();

    public abstract int ComputeMinIndentation(Length offset, TextModel textModel);
}

/// <summary>Represents a bracket pair including its child (e.g. <c>{ ... }</c>). Might be unclosed. Immutable, if all children are immutable.</summary>
internal sealed class PairAstNode : AstNode
{
    public static PairAstNode Create(BracketAstNode openingBracket, AstNode? child, BracketAstNode? closingBracket)
    {
        var length = openingBracket.Length;
        if (child is not null)
        {
            length = LengthAdd(length, child.Length);
        }

        if (closingBracket is not null)
        {
            length = LengthAdd(length, closingBracket.Length);
        }

        return new PairAstNode(length, openingBracket, child, closingBracket, child?.MissingOpeningBracketIds ?? SmallImmutableSet<int>.GetEmpty());
    }

    private readonly SmallImmutableSet<int> _missingOpeningBracketIds;

    private PairAstNode(Length length, BracketAstNode openingBracket, AstNode? child, BracketAstNode? closingBracket, SmallImmutableSet<int> missingOpeningBracketIds)
        : base(length)
    {
        OpeningBracket = openingBracket;
        Child = child;
        ClosingBracket = closingBracket;
        _missingOpeningBracketIds = missingOpeningBracketIds;
    }

    public BracketAstNode OpeningBracket { get; }

    public AstNode? Child { get; }

    public BracketAstNode? ClosingBracket { get; }

    public override AstNodeKind Kind => AstNodeKind.Pair;

    public override int ListHeight => 0;

    public override int ChildrenLength => 3;

    public override SmallImmutableSet<int> MissingOpeningBracketIds => _missingOpeningBracketIds;

    public override AstNode? GetChild(int idx) => idx switch
    {
        0 => OpeningBracket,
        1 => Child,
        2 => ClosingBracket,
        _ => throw new ArgumentOutOfRangeException(nameof(idx), "Invalid child index"),
    };

    /// <summary>Avoid using this property, it allocates an array!</summary>
    public override IReadOnlyList<AstNode> Children
    {
        get
        {
            var result = new List<AstNode> { OpeningBracket };
            if (Child is not null)
            {
                result.Add(Child);
            }

            if (ClosingBracket is not null)
            {
                result.Add(ClosingBracket);
            }

            return result;
        }
    }

    public override bool CanBeReused(SmallImmutableSet<int> openBracketIds)
    {
        if (ClosingBracket is null)
        {
            // Unclosed pair ast nodes only
            // end at the end of the document
            // or when a parent node is closed.

            // This could be improved:
            // Only return false if some next token is neither "undefined" nor a bracket that closes a parent.

            return false;
        }

        if (openBracketIds.Intersects(MissingOpeningBracketIds))
        {
            return false;
        }

        return true;
    }

    public override AstNode FlattenLists() =>
        Create((BracketAstNode)OpeningBracket.FlattenLists(), Child?.FlattenLists(), (BracketAstNode?)ClosingBracket?.FlattenLists());

    public override AstNode DeepClone() =>
        new PairAstNode(Length, (BracketAstNode)OpeningBracket.DeepClone(), Child?.DeepClone(), (BracketAstNode?)ClosingBracket?.DeepClone(), MissingOpeningBracketIds);

    public override int ComputeMinIndentation(Length offset, TextModel textModel) =>
        Child?.ComputeMinIndentation(LengthAdd(offset, OpeningBracket.Length), textModel) ?? int.MaxValue;
}

internal abstract class ListAstNode : AstNode
{
    private SmallImmutableSet<int> _missingOpeningBracketIds;
    private int _cachedMinIndentation = -1;

    /// <summary>This method uses more memory-efficient list nodes that can only store 2 or 3 children.</summary>
    public static ListAstNode Create23(AstNode item1, AstNode item2, AstNode? item3, bool immutable = false)
    {
        var length = item1.Length;
        var missingBracketIds = item1.MissingOpeningBracketIds;

        if (item1.ListHeight != item2.ListHeight)
        {
            throw new InvalidOperationException("Invalid list heights");
        }

        length = LengthAdd(length, item2.Length);
        missingBracketIds = missingBracketIds.Merge(item2.MissingOpeningBracketIds);

        if (item3 is not null)
        {
            if (item1.ListHeight != item3.ListHeight)
            {
                throw new InvalidOperationException("Invalid list heights");
            }

            length = LengthAdd(length, item3.Length);
            missingBracketIds = missingBracketIds.Merge(item3.MissingOpeningBracketIds);
        }

        return immutable
            ? new Immutable23ListAstNode(length, item1.ListHeight + 1, item1, item2, item3, missingBracketIds)
            : new TwoThreeListAstNode(length, item1.ListHeight + 1, item1, item2, item3, missingBracketIds);
    }

    public static ListAstNode Create(IReadOnlyList<AstNode> items, bool immutable = false)
    {
        if (items.Count == 0)
        {
            return GetEmpty();
        }

        var length = items[0].Length;
        var unopenedBrackets = items[0].MissingOpeningBracketIds;
        for (var i = 1; i < items.Count; i++)
        {
            length = LengthAdd(length, items[i].Length);
            unopenedBrackets = unopenedBrackets.Merge(items[i].MissingOpeningBracketIds);
        }

        return immutable
            ? new ImmutableArrayListAstNode(length, items[0].ListHeight + 1, [.. items], unopenedBrackets)
            : new ArrayListAstNode(length, items[0].ListHeight + 1, [.. items], unopenedBrackets);
    }

    public static ListAstNode GetEmpty() => new ImmutableArrayListAstNode(Length.Zero, 0, [], SmallImmutableSet<int>.GetEmpty());

    /// <summary>Use <see cref="Create"/>.</summary>
    protected ListAstNode(Length length, int listHeight, SmallImmutableSet<int> missingOpeningBracketIds)
        : base(length)
    {
        ListHeight = listHeight;
        _missingOpeningBracketIds = missingOpeningBracketIds;
    }

    public override AstNodeKind Kind => AstNodeKind.List;

    public override int ListHeight { get; }

    public override SmallImmutableSet<int> MissingOpeningBracketIds => _missingOpeningBracketIds;

    protected virtual void ThrowIfImmutable()
    {
        // NOOP
    }

    protected abstract void SetChild(int idx, AstNode child);

    public AstNode? MakeLastElementMutable()
    {
        ThrowIfImmutable();
        var childCount = ChildrenLength;
        if (childCount == 0)
        {
            return null;
        }

        var lastChild = GetChild(childCount - 1)!;
        var mutable = lastChild is ListAstNode list ? list.ToMutable() : lastChild;
        if (!ReferenceEquals(lastChild, mutable))
        {
            SetChild(childCount - 1, mutable);
        }

        return mutable;
    }

    public AstNode? MakeFirstElementMutable()
    {
        ThrowIfImmutable();
        var childCount = ChildrenLength;
        if (childCount == 0)
        {
            return null;
        }

        var firstChild = GetChild(0)!;
        var mutable = firstChild is ListAstNode list ? list.ToMutable() : firstChild;
        if (!ReferenceEquals(firstChild, mutable))
        {
            SetChild(0, mutable);
        }

        return mutable;
    }

    public override bool CanBeReused(SmallImmutableSet<int> openBracketIds)
    {
        if (openBracketIds.Intersects(MissingOpeningBracketIds))
        {
            return false;
        }

        if (ChildrenLength == 0)
        {
            // Don't reuse empty lists.
            return false;
        }

        AstNode lastChild = this;
        while (lastChild is ListAstNode list)
        {
            var lastLength = list.ChildrenLength;
            if (lastLength == 0)
            {
                // Empty lists should never be contained in other lists.
                throw new InvalidOperationException("Empty lists should never be contained in other lists.");
            }

            lastChild = list.GetChild(lastLength - 1)!;
        }

        return lastChild.CanBeReused(openBracketIds);
    }

    public void HandleChildrenChanged()
    {
        ThrowIfImmutable();

        var count = ChildrenLength;

        var length = GetChild(0)!.Length;
        var unopenedBrackets = GetChild(0)!.MissingOpeningBracketIds;

        for (var i = 1; i < count; i++)
        {
            var child = GetChild(i)!;
            length = LengthAdd(length, child.Length);
            unopenedBrackets = unopenedBrackets.Merge(child.MissingOpeningBracketIds);
        }

        LengthValue = length;
        _missingOpeningBracketIds = unopenedBrackets;
        _cachedMinIndentation = -1;
    }

    public override AstNode FlattenLists()
    {
        var items = new List<AstNode>();
        foreach (var c in Children)
        {
            var normalized = c.FlattenLists();
            if (normalized is ListAstNode list)
            {
                items.AddRange(list.Children);
            }
            else
            {
                items.Add(normalized);
            }
        }

        return Create(items);
    }

    public override int ComputeMinIndentation(Length offset, TextModel textModel)
    {
        if (_cachedMinIndentation != -1)
        {
            return _cachedMinIndentation;
        }

        var minIndentation = int.MaxValue;
        var childOffset = offset;
        for (var i = 0; i < ChildrenLength; i++)
        {
            var child = GetChild(i);
            if (child is not null)
            {
                minIndentation = Math.Min(minIndentation, child.ComputeMinIndentation(childOffset, textModel));
                childOffset = LengthAdd(childOffset, child.Length);
            }
        }

        _cachedMinIndentation = minIndentation;
        return minIndentation;
    }

    /// <summary>Creates a shallow clone that is mutable, or itself if it is already mutable.</summary>
    public abstract ListAstNode ToMutable();

    public abstract void AppendChildOfSameHeight(AstNode node);

    public abstract AstNode? UnappendChild();

    public abstract void PrependChildOfSameHeight(AstNode node);

    public abstract AstNode? UnprependChild();
}

internal class TwoThreeListAstNode : ListAstNode
{
    private AstNode _item1;
    private AstNode _item2;
    private AstNode? _item3;

    public TwoThreeListAstNode(Length length, int listHeight, AstNode item1, AstNode item2, AstNode? item3, SmallImmutableSet<int> missingOpeningBracketIds)
        : base(length, listHeight, missingOpeningBracketIds)
    {
        _item1 = item1;
        _item2 = item2;
        _item3 = item3;
    }

    public override int ChildrenLength => _item3 is not null ? 3 : 2;

    public override AstNode? GetChild(int idx) => idx switch
    {
        0 => _item1,
        1 => _item2,
        2 => _item3,
        _ => throw new ArgumentOutOfRangeException(nameof(idx), "Invalid child index"),
    };

    protected override void SetChild(int idx, AstNode child)
    {
        switch (idx)
        {
            case 0:
                _item1 = child;
                return;
            case 1:
                _item2 = child;
                return;
            case 2:
                _item3 = child;
                return;
        }

        throw new ArgumentOutOfRangeException(nameof(idx), "Invalid child index");
    }

    public override IReadOnlyList<AstNode> Children => _item3 is not null ? [_item1, _item2, _item3] : [_item1, _item2];

    public AstNode Item1 => _item1;

    public AstNode Item2 => _item2;

    public AstNode? Item3 => _item3;

    public override AstNode DeepClone() =>
        new TwoThreeListAstNode(Length, ListHeight, _item1.DeepClone(), _item2.DeepClone(), _item3?.DeepClone(), MissingOpeningBracketIds);

    public override void AppendChildOfSameHeight(AstNode node)
    {
        if (_item3 is not null)
        {
            throw new InvalidOperationException("Cannot append to a full (2,3) tree node");
        }

        ThrowIfImmutable();
        _item3 = node;
        HandleChildrenChanged();
    }

    public override AstNode? UnappendChild()
    {
        if (_item3 is null)
        {
            throw new InvalidOperationException("Cannot remove from a non-full (2,3) tree node");
        }

        ThrowIfImmutable();
        var result = _item3;
        _item3 = null;
        HandleChildrenChanged();
        return result;
    }

    public override void PrependChildOfSameHeight(AstNode node)
    {
        if (_item3 is not null)
        {
            throw new InvalidOperationException("Cannot prepend to a full (2,3) tree node");
        }

        ThrowIfImmutable();
        _item3 = _item2;
        _item2 = _item1;
        _item1 = node;
        HandleChildrenChanged();
    }

    public override AstNode? UnprependChild()
    {
        if (_item3 is null)
        {
            throw new InvalidOperationException("Cannot remove from a non-full (2,3) tree node");
        }

        ThrowIfImmutable();
        var result = _item1;
        _item1 = _item2;
        _item2 = _item3;
        _item3 = null;

        HandleChildrenChanged();
        return result;
    }

    public override ListAstNode ToMutable() => this;
}

/// <summary>Immutable, if all children are immutable.</summary>
internal sealed class Immutable23ListAstNode(Length length, int listHeight, AstNode item1, AstNode item2, AstNode? item3, SmallImmutableSet<int> missingOpeningBracketIds)
    : TwoThreeListAstNode(length, listHeight, item1, item2, item3, missingOpeningBracketIds)
{
    public override ListAstNode ToMutable() => new TwoThreeListAstNode(Length, ListHeight, Item1, Item2, Item3, MissingOpeningBracketIds);

    protected override void ThrowIfImmutable() => throw new InvalidOperationException("this instance is immutable");
}

/// <summary>For debugging.</summary>
internal class ArrayListAstNode(Length length, int listHeight, List<AstNode> children, SmallImmutableSet<int> missingOpeningBracketIds)
    : ListAstNode(length, listHeight, missingOpeningBracketIds)
{
    private readonly List<AstNode> _children = children;

    public override int ChildrenLength => _children.Count;

    public override AstNode? GetChild(int idx) => idx >= 0 && idx < _children.Count ? _children[idx] : null;

    protected override void SetChild(int idx, AstNode child) => _children[idx] = child;

    public override IReadOnlyList<AstNode> Children => _children;

    public override AstNode DeepClone()
    {
        var children = new List<AstNode>(_children.Count);
        foreach (var child in _children)
        {
            children.Add(child.DeepClone());
        }

        return new ArrayListAstNode(Length, ListHeight, children, MissingOpeningBracketIds);
    }

    public override void AppendChildOfSameHeight(AstNode node)
    {
        ThrowIfImmutable();
        _children.Add(node);
        HandleChildrenChanged();
    }

    public override AstNode? UnappendChild()
    {
        ThrowIfImmutable();
        AstNode? item = null;
        if (_children.Count > 0)
        {
            item = _children[^1];
            _children.RemoveAt(_children.Count - 1);
        }

        HandleChildrenChanged();
        return item;
    }

    public override void PrependChildOfSameHeight(AstNode node)
    {
        ThrowIfImmutable();
        _children.Insert(0, node);
        HandleChildrenChanged();
    }

    public override AstNode? UnprependChild()
    {
        ThrowIfImmutable();
        AstNode? item = null;
        if (_children.Count > 0)
        {
            item = _children[0];
            _children.RemoveAt(0);
        }

        HandleChildrenChanged();
        return item;
    }

    public override ListAstNode ToMutable() => this;
}

/// <summary>Immutable, if all children are immutable.</summary>
internal sealed class ImmutableArrayListAstNode(Length length, int listHeight, List<AstNode> children, SmallImmutableSet<int> missingOpeningBracketIds)
    : ArrayListAstNode(length, listHeight, children, missingOpeningBracketIds)
{
    public override ListAstNode ToMutable() => new ArrayListAstNode(Length, ListHeight, [.. Children], MissingOpeningBracketIds);

    protected override void ThrowIfImmutable() => throw new InvalidOperationException("this instance is immutable");
}

internal abstract class ImmutableLeafAstNode(Length length) : AstNode(length)
{
    public override int ListHeight => 0;

    public override int ChildrenLength => 0;

    public override AstNode? GetChild(int idx) => null;

    public override IReadOnlyList<AstNode> Children => [];

    public override AstNode FlattenLists() => this;

    public override AstNode DeepClone() => this;
}

internal sealed class TextAstNode(Length length) : ImmutableLeafAstNode(length)
{
    public override AstNodeKind Kind => AstNodeKind.Text;

    public override SmallImmutableSet<int> MissingOpeningBracketIds => SmallImmutableSet<int>.GetEmpty();

    public override bool CanBeReused(SmallImmutableSet<int> openBracketIds) => true;

    public override int ComputeMinIndentation(Length offset, TextModel textModel)
    {
        var start = LengthToObj(offset);
        // Text ast nodes don't have partial indentation (ensured by the tokenizer).
        // Thus, if this text node does not start at column 0, the first line cannot have any indentation at all.
        var startLineNumber = (start.ColumnCount == 0 ? start.LineCount : start.LineCount + 1) + 1;
        var endLineNumber = LengthGetLineCount(LengthAdd(offset, Length)) + 1;

        var result = int.MaxValue;

        for (var lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
        {
            var firstNonWsColumn = textModel.GetLineFirstNonWhitespaceColumn(lineNumber);
            var lineContent = textModel.GetLineContent(lineNumber);
            if (firstNonWsColumn == 0)
            {
                continue;
            }

            var visibleColumn = CursorColumns.VisibleColumnFromColumn(lineContent, firstNonWsColumn, textModel.GetOptions().TabSize);
            result = Math.Min(result, visibleColumn);
        }

        return result;
    }
}

internal sealed class BracketAstNode : ImmutableLeafAstNode
{
    public static BracketAstNode Create(Length length, BracketKindBase bracketInfo, SmallImmutableSet<int> bracketIds) =>
        new(length, bracketInfo, bracketIds);

    private BracketAstNode(Length length, BracketKindBase bracketInfo, SmallImmutableSet<int> bracketIds)
        : base(length)
    {
        BracketInfo = bracketInfo;
        BracketIds = bracketIds;
    }

    public BracketKindBase BracketInfo { get; }

    /// <summary>
    /// In case of a opening bracket, this is the id of the opening bracket. In case of a closing bracket, this contains
    /// the ids of all opening brackets it can close.
    /// </summary>
    public SmallImmutableSet<int> BracketIds { get; }

    public override AstNodeKind Kind => AstNodeKind.Bracket;

    public override SmallImmutableSet<int> MissingOpeningBracketIds => SmallImmutableSet<int>.GetEmpty();

    public string Text => BracketInfo.BracketText;

    public string LanguageId => BracketInfo.LanguageId;

    // These nodes could be reused, but not in a general way. Their parent may be reused.
    public override bool CanBeReused(SmallImmutableSet<int> openBracketIds) => false;

    public override int ComputeMinIndentation(Length offset, TextModel textModel) => int.MaxValue;
}

internal sealed class InvalidBracketAstNode(SmallImmutableSet<int> closingBrackets, Length length) : ImmutableLeafAstNode(length)
{
    public override AstNodeKind Kind => AstNodeKind.UnexpectedClosingBracket;

    public override SmallImmutableSet<int> MissingOpeningBracketIds { get; } = closingBrackets;

    public override bool CanBeReused(SmallImmutableSet<int> openBracketIds) => !openBracketIds.Intersects(MissingOpeningBracketIds);

    public override int ComputeMinIndentation(Length offset, TextModel textModel) => int.MaxValue;
}
