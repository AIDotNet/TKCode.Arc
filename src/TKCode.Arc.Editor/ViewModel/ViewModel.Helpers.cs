// Ported from vscode (src/vs/editor/common/viewModel/viewModelImpl.ts: IBatchableTarget, ViewportStart,
// OverviewRulerDecorations, HiddenAreasModel, mergeLineRangeArray, rangeArraysEqual and StableViewport; and
// src/vs/editor/common/model.ts: IAttachedView; commit b57d5c9e), MIT license.
// Differences from upstream:
//  - IBatchableTarget.batchChanges is generic in a Func, as upstream;
//  - HiddenAreasModel keys its sources by reference (upstream's Map<unknown, Range[]>) in a list that keeps the order in
//    which the sources were first added, as a Map does; a null source is a key of its own (upstream's undefined);
//  - mergeLineRangeArray keeps upstream's behaviour of advancing both arrays after a merge (upstream's test 'hidden areas
//    from multiple sources that overlap' describes it; ViewModelLinesFromProjectedModel normalizes the result);
//  - OverviewRulerDecorations keeps its groups in a dictionary by color, with the insertion order in a list.

using TKCode.Arc.Editor.Model;
using EditorViewLayout = TKCode.Arc.Editor.ViewLayout.ViewLayout;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>Applies the changes a callback makes in one batch (upstream's IBatchableTarget, the code editor).</summary>
internal interface IBatchableTarget
{
    /// <summary>Allows the target to apply the changes introduced by the callback in a batch.</summary>
    T BatchChanges<T>(Func<T> cb);
}

/// <summary>The view a model is attached to, told which model lines it shows (upstream's IAttachedView).</summary>
internal interface IAttachedView
{
    /// <param name="visibleLines">The visible model ranges.</param>
    /// <param name="stabilized">Whether the visible lines have stopped changing (scrolling ended).</param>
    void SetVisibleLines(IReadOnlyList<TextRange> visibleLines, bool stabilized);
}

/// <summary>Remembers the first line of the viewport as a tracked range, to scroll back to it after edits.</summary>
internal sealed class ViewportStart : IDisposable
{
    private readonly TextModel _model;

    private ViewportStart(TextModel model, int viewLineNumber, bool isValid, string modelTrackedRange, double startLineDelta)
    {
        _model = model;
        ViewLineNumber = viewLineNumber;
        IsValid = isValid;
        ModelTrackedRange = modelTrackedRange;
        StartLineDelta = startLineDelta;
    }

    public int ViewLineNumber { get; private set; }

    public bool IsValid { get; private set; }

    public string ModelTrackedRange { get; private set; }

    public double StartLineDelta { get; private set; }

    public static ViewportStart Create(TextModel model)
    {
        var viewportStartLineTrackedRange = model.SetTrackedRange(null, new TextRange(1, 1, 1, 1), TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges)!;
        return new ViewportStart(model, 1, false, viewportStartLineTrackedRange, 0);
    }

    public void Dispose()
    {
        _model.SetTrackedRange(ModelTrackedRange, null, TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges);
    }

    public void Update(IViewModel viewModel, int startLineNumber)
    {
        var position = viewModel.CoordinatesConverter.ConvertViewPositionToModelPosition(new TextPosition(startLineNumber, viewModel.GetLineMinColumn(startLineNumber)));
        var viewportStartLineTrackedRange = viewModel.Model.SetTrackedRange(
            ModelTrackedRange,
            new TextRange(position.LineNumber, position.Column, position.LineNumber, position.Column),
            TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges)!;
        var viewportStartLineTop = viewModel.ViewLayout.GetVerticalOffsetForLineNumber(startLineNumber);
        var scrollTop = viewModel.ViewLayout.GetCurrentScrollTop();

        ViewLineNumber = startLineNumber;
        IsValid = true;
        ModelTrackedRange = viewportStartLineTrackedRange;
        StartLineDelta = scrollTop - viewportStartLineTop;
    }

    public void Invalidate()
    {
        IsValid = false;
    }
}

/// <summary>Groups the overview ruler decorations by color, merging adjacent ones of the same lane.</summary>
internal sealed class OverviewRulerDecorations
{
    private readonly Dictionary<string, (OverviewRulerDecorationsGroup Group, List<int> Data)> _asMap = new(StringComparer.Ordinal);

    public List<OverviewRulerDecorationsGroup> AsArray { get; } = new();

    public void Accept(string color, int zIndex, int startLineNumber, int endLineNumber, int lane)
    {
        if (_asMap.TryGetValue(color, out var prev))
        {
            var prevData = prev.Data;
            var prevLane = prevData[prevData.Count - 3];
            var prevEndLineNumber = prevData[prevData.Count - 1];
            if (prevLane == lane && prevEndLineNumber + 1 >= startLineNumber)
            {
                // merge into prev
                if (endLineNumber > prevEndLineNumber)
                {
                    prevData[prevData.Count - 1] = endLineNumber;
                }
                return;
            }

            // push
            prevData.Add(lane);
            prevData.Add(startLineNumber);
            prevData.Add(endLineNumber);
        }
        else
        {
            var data = new List<int> { lane, startLineNumber, endLineNumber };
            var group = new OverviewRulerDecorationsGroup(color, zIndex, data);
            _asMap[color] = (group, data);
            AsArray.Add(group);
        }
    }
}

/// <summary>The hidden areas of each source (folding, the API, ...) and their merge.</summary>
internal sealed class HiddenAreasModel
{
    private static readonly object s_nullSource = new();

    private readonly List<(object Source, IReadOnlyList<TextRange> Ranges)> _hiddenAreas = new();
    private bool _shouldRecompute;
    private IReadOnlyList<TextRange> _ranges = [];

    public void SetHiddenAreas(object? source, IReadOnlyList<TextRange> ranges)
    {
        var key = source ?? s_nullSource;
        var index = _hiddenAreas.FindIndex(e => ReferenceEquals(e.Source, key));
        if (index >= 0 && RangeArraysEqual(_hiddenAreas[index].Ranges, ranges))
        {
            return;
        }

        if (index >= 0)
        {
            _hiddenAreas[index] = (key, ranges);
        }
        else
        {
            _hiddenAreas.Add((key, ranges));
        }
        _shouldRecompute = true;
    }

    /// <summary>The returned list is immutable; the same instance comes back while the merge does not change.</summary>
    public IReadOnlyList<TextRange> GetMergedRanges()
    {
        if (!_shouldRecompute)
        {
            return _ranges;
        }
        _shouldRecompute = false;
        IReadOnlyList<TextRange> newRanges = [];
        foreach (var (_, hiddenAreas) in _hiddenAreas)
        {
            newRanges = MergeLineRangeArray(newRanges, hiddenAreas);
        }
        if (RangeArraysEqual(_ranges, newRanges))
        {
            return _ranges;
        }
        _ranges = newRanges;
        return _ranges;
    }

    private static List<TextRange> MergeLineRangeArray(IReadOnlyList<TextRange> arr1, IReadOnlyList<TextRange> arr2)
    {
        var result = new List<TextRange>();
        var i = 0;
        var j = 0;
        while (i < arr1.Count && j < arr2.Count)
        {
            var item1 = arr1[i];
            var item2 = arr2[j];

            if (item1.EndLineNumber < item2.StartLineNumber - 1)
            {
                result.Add(arr1[i++]);
            }
            else if (item2.EndLineNumber < item1.StartLineNumber - 1)
            {
                result.Add(arr2[j++]);
            }
            else
            {
                var startLineNumber = Math.Min(item1.StartLineNumber, item2.StartLineNumber);
                var endLineNumber = Math.Max(item1.EndLineNumber, item2.EndLineNumber);
                result.Add(new TextRange(startLineNumber, 1, endLineNumber, 1));
                i++;
                j++;
            }
        }
        while (i < arr1.Count)
        {
            result.Add(arr1[i++]);
        }
        while (j < arr2.Count)
        {
            result.Add(arr2[j++]);
        }
        return result;
    }

    private static bool RangeArraysEqual(IReadOnlyList<TextRange> arr1, IReadOnlyList<TextRange> arr2)
    {
        if (arr1.Count != arr2.Count)
        {
            return false;
        }
        for (var i = 0; i < arr1.Count; i++)
        {
            if (!arr1[i].Equals(arr2[i]))
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>Maintain a stable viewport by trying to keep the first line in the viewport constant.</summary>
internal sealed class StableViewport
{
    public StableViewport(TextPosition? viewportStartModelPosition, double startLineDelta)
    {
        ViewportStartModelPosition = viewportStartModelPosition;
        StartLineDelta = startLineDelta;
    }

    public TextPosition? ViewportStartModelPosition { get; }

    public double StartLineDelta { get; }

    public void RecoverViewportStart(ICoordinatesConverter coordinatesConverter, EditorViewLayout viewLayout)
    {
        if (ViewportStartModelPosition is not { } viewportStartModelPosition)
        {
            return;
        }
        var viewPosition = coordinatesConverter.ConvertModelPositionToViewPosition(viewportStartModelPosition);
        var viewPositionTop = viewLayout.GetVerticalOffsetForLineNumber(viewPosition.LineNumber);
        viewLayout.SetScrollPosition(new NewScrollPosition(ScrollTop: viewPositionTop + StartLineDelta), ScrollType.Immediate);
    }
}
