// Ported from vscode (src/vs/editor/contrib/folding/browser/foldingModel.ts: IDecorationProvider, FoldingModel;
// commit b57d5c9e), MIT license. The module's functions (toggleCollapseState, setCollapseState*, get*FoldLine) are in
// FoldingModelFunctions.cs.
// Differences:
//  - onDidChange is a .NET event (DidChange), raised synchronously as upstream's Emitter;
//  - the line checksum uses upstream's string hash (base/common/hash.ts: stringHash with the seed of hash()), ported
//    here as a private helper;
//  - removeManualRanges takes ranges (selections convert implicitly to TextRange).

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Folding;

/// <summary>Gives the folding model its decorations (upstream's IDecorationProvider).</summary>
internal interface IFoldingDecorationProvider
{
    /// <summary>The decoration options of a region by its state.</summary>
    ModelDecorationOptions GetDecorationOption(bool isCollapsed, bool isHidden, bool isManual);

    /// <summary>Changes decorations (the editor's changeDecorations).</summary>
    T? ChangeDecorations<T>(Func<TextModel.ModelDecorationsChangeAccessor, T> callback);

    /// <summary>Removes decorations.</summary>
    void RemoveDecorations(IReadOnlyList<string> decorationIds);
}

/// <summary>What changed in a folding model (upstream's FoldingModelChangeEvent).</summary>
internal sealed record FoldingModelChangeEvent(FoldingModel Model, IReadOnlyList<FoldingRegion>? CollapseStateChanged = null);

/// <summary>A folded or manual range to keep across sessions (upstream's ILineMemento).</summary>
internal sealed record FoldingLineMemento(int StartLineNumber, int EndLineNumber) : ILineRange
{
    public int? Checksum { get; init; }

    public bool? IsCollapsed { get; init; }

    public FoldSource? Source { get; init; }
}

/// <summary>
/// The folding regions of a text model with their collapsed state; each region is a decoration of the model, so
/// regions move with edits until the providers compute them again (upstream's FoldingModel).
/// </summary>
internal sealed class FoldingModel : IDisposable
{
    private readonly TextModel _textModel;
    private readonly IFoldingDecorationProvider _decorationProvider;
    private FoldingRegions _regions;
    private string[] _editorDecorationIds;

    public FoldingModel(TextModel textModel, IFoldingDecorationProvider decorationProvider)
    {
        _textModel = textModel;
        _decorationProvider = decorationProvider;
        _regions = FoldingRegions.Empty();
        _editorDecorationIds = [];
    }

    /// <summary>Raised when the regions or their collapsed state changed (upstream's onDidChange).</summary>
    public event Action<FoldingModelChangeEvent>? DidChange;

    public FoldingRegions Regions => _regions;

    public TextModel TextModel => _textModel;

    public IFoldingDecorationProvider DecorationProvider => _decorationProvider;

    /// <summary>Collapses the expanded regions and expands the collapsed ones of <paramref name="toggledRegions"/>.</summary>
    public void ToggleCollapseState(IReadOnlyList<FoldingRegion> toggledRegions)
    {
        if (toggledRegions.Count == 0)
        {
            return;
        }

        var sorted = toggledRegions.OrderBy(static r => r.RegionIndex).ToList();

        var processed = new HashSet<string>(StringComparer.Ordinal);
        _decorationProvider.ChangeDecorations(accessor =>
        {
            var k = 0; // index from [0 ... this.regions.length]
            var dirtyRegionEndLine = -1; // end of the range where decorations need to be updated
            var lastHiddenLine = -1; // the end of the last hidden lines

            void UpdateDecorationsUntil(int index)
            {
                while (k < index)
                {
                    var endLineNumber = _regions.GetEndLineNumber(k);
                    var isCollapsed = _regions.IsCollapsed(k);
                    if (endLineNumber <= dirtyRegionEndLine)
                    {
                        var isManual = _regions.GetSource(k) != FoldSource.Provider;
                        accessor.ChangeDecorationOptions(_editorDecorationIds[k], _decorationProvider.GetDecorationOption(isCollapsed, endLineNumber <= lastHiddenLine, isManual));
                    }

                    if (isCollapsed && endLineNumber > lastHiddenLine)
                    {
                        lastHiddenLine = endLineNumber;
                    }

                    k++;
                }
            }

            foreach (var region in sorted)
            {
                var index = region.RegionIndex;
                var editorDecorationId = index < _editorDecorationIds.Length ? _editorDecorationIds[index] : null;
                if (editorDecorationId is not null && processed.Add(editorDecorationId))
                {
                    UpdateDecorationsUntil(index); // update all decorations up to current index using the old dirtyRegionEndLine

                    var newCollapseState = !_regions.IsCollapsed(index);
                    _regions.SetCollapsed(index, newCollapseState);

                    dirtyRegionEndLine = Math.Max(dirtyRegionEndLine, _regions.GetEndLineNumber(index));
                }
            }

            UpdateDecorationsUntil(_regions.Length);
            return true;
        });
        DidChange?.Invoke(new FoldingModelChangeEvent(this, sorted));
    }

    /// <summary>
    /// Removes the manual (user defined or recovered) ranges at empty selections (the innermost one at each, or all
    /// when one is outside any) and those that intersect non-empty selections.
    /// </summary>
    public void RemoveManualRanges(IReadOnlyList<TextRange> ranges)
    {
        var rangeIndexesToRemove = new HashSet<int>();
        var removeAll = false;
        foreach (var range in ranges)
        {
            if (range.IsEmpty)
            {
                var index = _regions.FindRange(range.StartLineNumber);
                while (index != -1 && _regions.GetSource(index) == FoldSource.Provider)
                {
                    index = _regions.GetParentIndex(index);
                }

                if (index == -1)
                {
                    removeAll = true;
                }
                else
                {
                    rangeIndexesToRemove.Add(index);
                }
            }
        }

        var newFoldingRanges = new List<FoldRange>();
        for (var i = 0; i < _regions.Length; i++)
        {
            var foldRange = _regions.ToFoldRange(i);
            if (foldRange.Source == FoldSource.Provider || (!removeAll && !rangeIndexesToRemove.Contains(i) && !IntersectsSelection(foldRange)))
            {
                newFoldingRanges.Add(foldRange);
            }
        }

        UpdatePost(FoldingRegions.FromFoldRanges(newFoldingRanges));

        bool IntersectsSelection(FoldRange foldRange)
        {
            foreach (var range in ranges)
            {
                if (!range.IsEmpty && !(range.StartLineNumber > foldRange.EndLineNumber || foldRange.StartLineNumber > range.EndLineNumber))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Merges newly computed regions with the collapsed and manual ones (upstream's update).</summary>
    public void Update(FoldingRegions newRegions, ISelectedLines? selection = null)
    {
        var foldedOrManualRanges = CurrentFoldedOrManualRanges(selection);
        var newRanges = FoldingRegions.SanitizeAndMerge(newRegions, foldedOrManualRanges, _textModel.GetLineCount(), selection);
        UpdatePost(FoldingRegions.FromFoldRanges(newRanges));
    }

    /// <summary>Takes <paramref name="newRegions"/> as the regions and decorates them (upstream's updatePost).</summary>
    public void UpdatePost(FoldingRegions newRegions)
    {
        var newEditorDecorations = new List<ModelDeltaDecoration>(newRegions.Length);
        var lastHiddenLine = -1;
        for (int index = 0, limit = newRegions.Length; index < limit; index++)
        {
            var startLineNumber = newRegions.GetStartLineNumber(index);
            var endLineNumber = newRegions.GetEndLineNumber(index);
            var isCollapsed = newRegions.IsCollapsed(index);
            var isManual = newRegions.GetSource(index) != FoldSource.Provider;
            var decorationRange = new TextRange(
                startLineNumber,
                _textModel.GetLineMaxColumn(startLineNumber),
                endLineNumber,
                _textModel.GetLineMaxColumn(endLineNumber) + 1);
            newEditorDecorations.Add(new ModelDeltaDecoration(decorationRange, _decorationProvider.GetDecorationOption(isCollapsed, endLineNumber <= lastHiddenLine, isManual)));
            if (isCollapsed && endLineNumber > lastHiddenLine)
            {
                lastHiddenLine = endLineNumber;
            }
        }

        _decorationProvider.ChangeDecorations(accessor => _editorDecorationIds = accessor.DeltaDecorations(_editorDecorationIds, newEditorDecorations));
        _regions = newRegions;
        DidChange?.Invoke(new FoldingModelChangeEvent(this));
    }

    private List<FoldRange> CurrentFoldedOrManualRanges(ISelectedLines? selection = null)
    {
        var foldedRanges = new List<FoldRange>();
        for (int i = 0, limit = _regions.Length; i < limit; i++)
        {
            var isCollapsed = _regions.IsCollapsed(i);
            var source = _regions.GetSource(i);
            if (isCollapsed || source != FoldSource.Provider)
            {
                var foldRange = _regions.ToFoldRange(i);
                if (i < _editorDecorationIds.Length && _textModel.GetDecorationRange(_editorDecorationIds[i]) is { } decRange)
                {
                    if (isCollapsed && selection is not null && selection.StartsInside(decRange.StartLineNumber + 1, decRange.EndLineNumber))
                    {
                        isCollapsed = false; // uncollapse is the range is blocked
                    }

                    foldedRanges.Add(new FoldRange(decRange.StartLineNumber, decRange.EndLineNumber, foldRange.Type, isCollapsed, source));
                }
            }
        }

        return foldedRanges;
    }

    /// <summary>The collapsed state, for persistence only (upstream's getMemento); null when nothing is folded or manual.</summary>
    public List<FoldingLineMemento>? GetMemento()
    {
        var foldedOrManualRanges = CurrentFoldedOrManualRanges();
        var result = new List<FoldingLineMemento>();
        var maxLineNumber = _textModel.GetLineCount();
        foreach (var range in foldedOrManualRanges)
        {
            if (range.StartLineNumber >= range.EndLineNumber || range.StartLineNumber < 1 || range.EndLineNumber > maxLineNumber)
            {
                continue;
            }

            var checksum = GetLinesChecksum(range.StartLineNumber + 1, range.EndLineNumber);
            result.Add(new FoldingLineMemento(range.StartLineNumber, range.EndLineNumber)
            {
                IsCollapsed = range.IsCollapsed,
                Source = range.Source,
                Checksum = checksum,
            });
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>Applies a persisted state (upstream's applyMemento).</summary>
    public void ApplyMemento(IReadOnlyList<FoldingLineMemento>? state)
    {
        if (state is null)
        {
            return;
        }

        var rangesToRestore = new List<FoldRange>();
        var maxLineNumber = _textModel.GetLineCount();
        foreach (var range in state)
        {
            if (range.StartLineNumber >= range.EndLineNumber || range.StartLineNumber < 1 || range.EndLineNumber > maxLineNumber)
            {
                continue;
            }

            var checksum = GetLinesChecksum(range.StartLineNumber + 1, range.EndLineNumber);
            if (range.Checksum is null or 0 || checksum == range.Checksum)
            {
                rangesToRestore.Add(new FoldRange(range.StartLineNumber, range.EndLineNumber, null, range.IsCollapsed ?? true, range.Source ?? FoldSource.Provider));
            }
        }

        var newRanges = FoldingRegions.SanitizeAndMerge(_regions, rangesToRestore, maxLineNumber);
        UpdatePost(FoldingRegions.FromFoldRanges(newRanges));
    }

    private int GetLinesChecksum(int lineNumber1, int lineNumber2)
    {
        var h = Hash(_textModel.GetLineContent(lineNumber1) + _textModel.GetLineContent(lineNumber2));
        return h % 1000000; // 6 digits is plenty
    }

    // upstream's hash(string): doHash -> stringHash(s, 0), keeping int32 arithmetic
    private static int Hash(string s)
    {
        var hashVal = NumberHash(149417, 0);
        foreach (var ch in s)
        {
            hashVal = NumberHash(ch, hashVal);
        }

        return hashVal;

        static int NumberHash(int val, int initialHashVal) => unchecked(((initialHashVal << 5) - initialHashVal) + val);
    }

    public void Dispose()
    {
        _decorationProvider.RemoveDecorations(_editorDecorationIds);
        _editorDecorationIds = [];
        DidChange = null;
    }

    /// <summary>The regions that contain <paramref name="lineNumber"/>, innermost first, that pass <paramref name="filter"/> (with their level, 1 innermost).</summary>
    public List<FoldingRegion> GetAllRegionsAtLine(int lineNumber, Func<FoldingRegion, int, bool>? filter = null)
    {
        var result = new List<FoldingRegion>();
        var index = _regions.FindRange(lineNumber);
        var level = 1;
        while (index >= 0)
        {
            var current = _regions.ToRegion(index);
            if (filter is null || filter(current, level))
            {
                result.Add(current);
            }

            level++;
            index = current.ParentIndex;
        }

        return result;
    }

    /// <summary>The innermost region that contains <paramref name="lineNumber"/>, or null.</summary>
    public FoldingRegion? GetRegionAtLine(int lineNumber)
    {
        var index = _regions.FindRange(lineNumber);
        return index >= 0 ? _regions.ToRegion(index) : null;
    }

    /// <summary>The regions inside <paramref name="region"/> (all with null) that pass <paramref name="filter"/>.</summary>
    public List<FoldingRegion> GetRegionsInside(FoldingRegion? region, Func<FoldingRegion, bool>? filter = null)
    {
        var result = new List<FoldingRegion>();
        var index = region is not null ? region.RegionIndex + 1 : 0;
        var endLineNumber = region is not null ? region.EndLineNumber : int.MaxValue;
        for (int i = index, len = _regions.Length; i < len; i++)
        {
            var current = _regions.ToRegion(i);
            if (_regions.GetStartLineNumber(i) < endLineNumber)
            {
                if (filter is null || filter(current))
                {
                    result.Add(current);
                }
            }
            else
            {
                break;
            }
        }

        return result;
    }

    /// <summary>The regions inside <paramref name="region"/> that pass <paramref name="filter"/>, which also gets the level (1 directly inside).</summary>
    public List<FoldingRegion> GetRegionsInside(FoldingRegion? region, Func<FoldingRegion, int, bool> filter)
    {
        var result = new List<FoldingRegion>();
        var index = region is not null ? region.RegionIndex + 1 : 0;
        var endLineNumber = region is not null ? region.EndLineNumber : int.MaxValue;
        var levelStack = new List<FoldingRegion>();
        for (int i = index, len = _regions.Length; i < len; i++)
        {
            var current = _regions.ToRegion(i);
            if (_regions.GetStartLineNumber(i) < endLineNumber)
            {
                while (levelStack.Count > 0 && !current.ContainedBy(levelStack[^1]))
                {
                    levelStack.RemoveAt(levelStack.Count - 1);
                }

                levelStack.Add(current);
                if (filter(current, levelStack.Count))
                {
                    result.Add(current);
                }
            }
            else
            {
                break;
            }
        }

        return result;
    }
}
