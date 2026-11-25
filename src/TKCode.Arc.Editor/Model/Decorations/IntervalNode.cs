// Ported from vscode's src/vs/editor/common/model/intervalTree.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
// IntervalNode: one decoration in the tree, plus its cached absolute offsets.

namespace TKCode.Arc.Editor.Model;

/// <summary>
/// A node of <see cref="IntervalTree"/>. A node outside a tree links to itself; the tree relinks it to its
/// own sentinel on insertion.
/// </summary>
internal sealed class IntervalNode
{
    /// <summary>Binary encoded color, visited flag, validation, stickiness, collapse-on-replace, margin and font flags.</summary>
    internal int Metadata;

    internal IntervalNode Parent;
    internal IntervalNode Left;
    internal IntervalNode Right;

    internal int Start;
    internal int End;
    internal int Delta;
    internal int MaxEnd;

    internal string? Id;
    internal int OwnerId;
    internal ModelDecorationOptions? Options;

    internal int CachedVersionId;
    internal int CachedAbsoluteStart;
    internal int CachedAbsoluteEnd;
    internal TextRange? Range;

    internal IntervalNode(string? id, int start, int end)
    {
        Metadata = 0;

        Parent = this;
        Left = this;
        Right = this;
        IntervalTreeNodeMetadata.SetNodeColor(this, IntervalNodeColor.Red);

        Start = start;
        End = end;
        // FORCE_OVERFLOWING_TEST: Delta = start;
        Delta = 0;
        MaxEnd = end;

        Id = id;
        OwnerId = 0;
        Options = null;
        IntervalTreeNodeMetadata.SetNodeIsForValidation(this, false);
        IntervalTreeNodeMetadata.SetNodeIsInGlyphMargin(this, false);
        IntervalTreeNodeMetadata.SetNodeStickiness(this, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges);
        IntervalTreeNodeMetadata.SetCollapseOnReplaceEdit(this, false);
        IntervalTreeNodeMetadata.SetNodeAffectsFont(this, false);

        CachedVersionId = 0;
        CachedAbsoluteStart = start;
        CachedAbsoluteEnd = end;
        Range = null;

        IntervalTreeNodeMetadata.SetNodeIsVisited(this, false);
    }

    internal void Reset(int versionId, int start, int end, TextRange? range)
    {
        Start = start;
        End = end;
        MaxEnd = end;
        CachedVersionId = versionId;
        CachedAbsoluteStart = start;
        CachedAbsoluteEnd = end;
        Range = range;
    }

    internal void SetOptions(ModelDecorationOptions options)
    {
        Options = options;
        IntervalTreeNodeMetadata.SetNodeIsForValidation(this, options.IsForValidation);
        IntervalTreeNodeMetadata.SetNodeIsInGlyphMargin(this, options.IsInGlyphMargin);
        IntervalTreeNodeMetadata.SetNodeStickiness(this, options.Stickiness);
        IntervalTreeNodeMetadata.SetCollapseOnReplaceEdit(this, options.CollapseOnReplaceEdit);
        IntervalTreeNodeMetadata.SetNodeAffectsFont(this, options.AffectsFont);
    }

    internal void SetCachedOffsets(int absoluteStart, int absoluteEnd, int cachedVersionId)
    {
        if (CachedVersionId != cachedVersionId)
        {
            Range = null;
        }
        CachedVersionId = cachedVersionId;
        CachedAbsoluteStart = absoluteStart;
        CachedAbsoluteEnd = absoluteEnd;
    }

    internal void Detach()
    {
        Parent = null!;
        Left = null!;
        Right = null!;
    }
}
