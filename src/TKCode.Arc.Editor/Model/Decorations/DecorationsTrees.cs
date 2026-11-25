// Ported from vscode (src/vs/editor/common/model/textModel.ts: DecorationsTrees, IDecorationsTreesHost,
// isNodeInOverviewRuler, isOptionsInjectedText and isNodeInjectedText, commit b57d5c9e), MIT license.
//
// Difference from upstream: the queries return ModelDecoration copies (see ModelDecoration.cs), not the nodes.

namespace TKCode.Arc.Editor.Model;

/// <summary>What the decoration trees need from their model.</summary>
internal interface IDecorationsTreesHost
{
    int GetVersionId();

    /// <summary>The range between two offsets.</summary>
    TextRange GetRangeAt(int start, int end);
}

/// <summary>
/// The decorations of a model in three interval trees: those not in the overview ruler, those in it, and those with
/// injected text. Each query only looks at the trees it needs.
/// </summary>
internal sealed class DecorationsTrees
{
    /// <summary>The decorations that do not show up in the overview ruler.</summary>
    private readonly IntervalTree _decorationsTree0 = new();

    /// <summary>The decorations that show up in the overview ruler.</summary>
    private readonly IntervalTree _decorationsTree1 = new();

    /// <summary>The decorations that contain injected text.</summary>
    private readonly IntervalTree _injectedTextDecorationsTree = new();

    internal static bool IsNodeInOverviewRuler(IntervalNode node) => node.Options!.OverviewRuler is { Color.IsEmpty: false };

    internal static bool IsOptionsInjectedText(ModelDecorationOptions options) => options.After is not null || options.Before is not null;

    internal static bool IsNodeInjectedText(IntervalNode node) => IsOptionsInjectedText(node.Options!);

    public void EnsureAllNodesHaveRanges(IDecorationsTreesHost host) => _ = GetAll(host, 0, false, false, false, false);

    private static List<ModelDecoration> EnsureNodesHaveRanges(
        IDecorationsTreesHost host,
        List<IntervalNode> nodes,
        Func<IntervalNode, bool>? filter = null)
    {
        var result = new List<ModelDecoration>(nodes.Count);
        foreach (var node in nodes)
        {
            node.Range ??= host.GetRangeAt(node.CachedAbsoluteStart, node.CachedAbsoluteEnd);
            if (filter is null || filter(node))
            {
                result.Add(ToDecoration(node));
            }
        }

        return result;
    }

    internal static ModelDecoration ToDecoration(IntervalNode node) => new(node.Id!, node.OwnerId, node.Range!.Value, node.Options!);

    public List<ModelDecoration> GetAllInInterval(
        IDecorationsTreesHost host,
        int start,
        int end,
        int filterOwnerId,
        bool filterOutValidation,
        bool filterFontDecorations,
        bool onlyMarginDecorations)
    {
        var versionId = host.GetVersionId();
        var result = IntervalSearch(start, end, filterOwnerId, filterOutValidation, filterFontDecorations, versionId, onlyMarginDecorations);
        return EnsureNodesHaveRanges(host, result);
    }

    private List<IntervalNode> IntervalSearch(
        int start,
        int end,
        int filterOwnerId,
        bool filterOutValidation,
        bool filterFontDecorations,
        int cachedVersionId,
        bool onlyMarginDecorations)
    {
        var r0 = _decorationsTree0.IntervalSearch(start, end, filterOwnerId, filterOutValidation, filterFontDecorations, cachedVersionId, onlyMarginDecorations);
        var r1 = _decorationsTree1.IntervalSearch(start, end, filterOwnerId, filterOutValidation, filterFontDecorations, cachedVersionId, onlyMarginDecorations);
        var r2 = _injectedTextDecorationsTree.IntervalSearch(start, end, filterOwnerId, filterOutValidation, filterFontDecorations, cachedVersionId, onlyMarginDecorations);
        return Concat(r0, r1, r2);
    }

    public List<ModelDecoration> GetInjectedTextInInterval(IDecorationsTreesHost host, int start, int end, int filterOwnerId)
    {
        var versionId = host.GetVersionId();
        var result = _injectedTextDecorationsTree.IntervalSearch(start, end, filterOwnerId, false, false, versionId, false);
        return EnsureNodesHaveRanges(host, result, static i => i.Options!.ShowIfCollapsed || !i.Range!.Value.IsEmpty);
    }

    public List<ModelDecoration> GetFontDecorationsInInterval(IDecorationsTreesHost host, int start, int end, int filterOwnerId)
    {
        var versionId = host.GetVersionId();
        var decorations = _decorationsTree0.IntervalSearch(start, end, filterOwnerId, false, false, versionId, false);
        return EnsureNodesHaveRanges(host, decorations, static i => i.Options!.AffectsFont);
    }

    public List<ModelDecoration> GetAllInjectedText(IDecorationsTreesHost host, int filterOwnerId)
    {
        var versionId = host.GetVersionId();
        var result = _injectedTextDecorationsTree.Search(filterOwnerId, false, false, versionId, false);
        return EnsureNodesHaveRanges(host, result, static i => i.Options!.ShowIfCollapsed || !i.Range!.Value.IsEmpty);
    }

    public List<ModelDecoration> GetAllCustomLineHeights(IDecorationsTreesHost host, int filterOwnerId)
    {
        var versionId = host.GetVersionId();
        var result = Search(filterOwnerId, false, false, false, versionId, false);
        return EnsureNodesHaveRanges(host, result, static i => i.Options!.LineHeight is not null);
    }

    public List<ModelDecoration> GetCustomLineHeightsInInterval(IDecorationsTreesHost host, int start, int end, int filterOwnerId)
    {
        var versionId = host.GetVersionId();
        var result = IntervalSearch(start, end, filterOwnerId, false, false, versionId, false);
        return EnsureNodesHaveRanges(host, result, static i => i.Options!.LineHeight is not null);
    }

    public List<ModelDecoration> GetAll(
        IDecorationsTreesHost host,
        int filterOwnerId,
        bool filterOutValidation,
        bool filterFontDecorations,
        bool overviewRulerOnly,
        bool onlyMarginDecorations)
    {
        var versionId = host.GetVersionId();
        var result = Search(filterOwnerId, filterOutValidation, filterFontDecorations, overviewRulerOnly, versionId, onlyMarginDecorations);
        return EnsureNodesHaveRanges(host, result);
    }

    private List<IntervalNode> Search(
        int filterOwnerId,
        bool filterOutValidation,
        bool filterFontDecorations,
        bool overviewRulerOnly,
        int cachedVersionId,
        bool onlyMarginDecorations)
    {
        if (overviewRulerOnly)
        {
            return _decorationsTree1.Search(filterOwnerId, filterOutValidation, filterFontDecorations, cachedVersionId, onlyMarginDecorations);
        }

        var r0 = _decorationsTree0.Search(filterOwnerId, filterOutValidation, filterFontDecorations, cachedVersionId, onlyMarginDecorations);
        var r1 = _decorationsTree1.Search(filterOwnerId, filterOutValidation, filterFontDecorations, cachedVersionId, onlyMarginDecorations);
        var r2 = _injectedTextDecorationsTree.Search(filterOwnerId, filterOutValidation, filterFontDecorations, cachedVersionId, onlyMarginDecorations);
        return Concat(r0, r1, r2);
    }

    public List<IntervalNode> CollectNodesFromOwner(int ownerId) =>
        Concat(
            _decorationsTree0.CollectNodesFromOwner(ownerId),
            _decorationsTree1.CollectNodesFromOwner(ownerId),
            _injectedTextDecorationsTree.CollectNodesFromOwner(ownerId));

    public List<IntervalNode> CollectNodesPostOrder() =>
        Concat(
            _decorationsTree0.CollectNodesPostOrder(),
            _decorationsTree1.CollectNodesPostOrder(),
            _injectedTextDecorationsTree.CollectNodesPostOrder());

    private IntervalTree TreeOf(IntervalNode node)
    {
        if (IsNodeInjectedText(node))
        {
            return _injectedTextDecorationsTree;
        }

        return IsNodeInOverviewRuler(node) ? _decorationsTree1 : _decorationsTree0;
    }

    public void Insert(IntervalNode node) => TreeOf(node).Insert(node);

    public void Delete(IntervalNode node) => TreeOf(node).Delete(node);

    /// <summary>Recomputes the max end of a node whose children are already right (upstream's free recomputeMaxEnd).</summary>
    public void RecomputeMaxEnd(IntervalNode node) => IntervalTree.RecomputeMaxEnd(TreeOf(node), node);

    public TextRange GetNodeRange(IDecorationsTreesHost host, IntervalNode node)
    {
        var versionId = host.GetVersionId();
        if (node.CachedVersionId != versionId)
        {
            TreeOf(node).ResolveNode(node, versionId);
        }

        node.Range ??= host.GetRangeAt(node.CachedAbsoluteStart, node.CachedAbsoluteEnd);
        return node.Range.Value;
    }

    public void AcceptReplace(int offset, int length, int textLength, bool forceMoveMarkers)
    {
        _decorationsTree0.AcceptReplace(offset, length, textLength, forceMoveMarkers);
        _decorationsTree1.AcceptReplace(offset, length, textLength, forceMoveMarkers);
        _injectedTextDecorationsTree.AcceptReplace(offset, length, textLength, forceMoveMarkers);
    }

    private static List<IntervalNode> Concat(List<IntervalNode> r0, List<IntervalNode> r1, List<IntervalNode> r2)
    {
        if (r1.Count == 0 && r2.Count == 0)
        {
            return r0;
        }

        var result = new List<IntervalNode>(r0.Count + r1.Count + r2.Count);
        result.AddRange(r0);
        result.AddRange(r1);
        result.AddRange(r2);
        return result;
    }
}
