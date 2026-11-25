// Ported from vscode (src/vs/editor/contrib/folding/browser/hiddenRangeModel.ts, commit b57d5c9e), MIT license.
// Differences: onDidChange is a .NET event (DidChange); adjustSelections works on a list of TextSelection values and
// replaces its entries.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.Folding;

/// <summary>The lines the collapsed regions of a folding model hide (upstream's HiddenRangeModel).</summary>
internal sealed class HiddenRangeModel : IDisposable
{
    private readonly FoldingModel _foldingModel;
    private IReadOnlyList<TextRange> _hiddenRanges;
    private bool _hasLineChanges;
    private bool _listening;

    public HiddenRangeModel(FoldingModel model)
    {
        _foldingModel = model;
        _foldingModel.DidChange += OnFoldingModelChanged;
        _listening = true;
        _hiddenRanges = [];
        if (model.Regions.Length > 0)
        {
            UpdateHiddenRanges();
        }
    }

    /// <summary>Raised with the new hidden ranges (upstream's onDidChange).</summary>
    public event Action<IReadOnlyList<TextRange>>? DidChange;

    /// <summary>The hidden ranges: whole lines, from the line after each collapsed region's start to its end.</summary>
    public IReadOnlyList<TextRange> HiddenRanges => _hiddenRanges;

    private void OnFoldingModelChanged(FoldingModelChangeEvent e) => UpdateHiddenRanges();

    /// <summary>Notes that the text changed lines, so that the next update applies the ranges again.</summary>
    public void NotifyChangeModelContent(ModelContentChangedEvent e)
    {
        if (_hiddenRanges.Count > 0 && !_hasLineChanges)
        {
            _hasLineChanges = e.Changes.Any(static change =>
                change.Range.EndLineNumber != change.Range.StartLineNumber || EolCounter.CountEOL(change.Text).Count != 0);
        }
    }

    private void UpdateHiddenRanges()
    {
        var updateHiddenAreas = false;
        var newHiddenAreas = new List<TextRange>();
        var k = 0;

        var lastCollapsedStart = int.MaxValue;
        var lastCollapsedEnd = -1;

        var ranges = _foldingModel.Regions;
        for (var i = 0; i < ranges.Length; i++)
        {
            if (!ranges.IsCollapsed(i))
            {
                continue;
            }

            var startLineNumber = ranges.GetStartLineNumber(i) + 1; // the first line is not hidden
            var endLineNumber = ranges.GetEndLineNumber(i);
            if (lastCollapsedStart <= startLineNumber && endLineNumber <= lastCollapsedEnd)
            {
                // ignore ranges contained in collapsed regions
                continue;
            }

            if (!updateHiddenAreas && k < _hiddenRanges.Count && _hiddenRanges[k].StartLineNumber == startLineNumber && _hiddenRanges[k].EndLineNumber == endLineNumber)
            {
                // reuse the old ranges
                newHiddenAreas.Add(_hiddenRanges[k]);
                k++;
            }
            else
            {
                updateHiddenAreas = true;
                newHiddenAreas.Add(new TextRange(startLineNumber, 1, endLineNumber, 1));
            }

            lastCollapsedStart = startLineNumber;
            lastCollapsedEnd = endLineNumber;
        }

        if (_hasLineChanges || updateHiddenAreas || k < _hiddenRanges.Count)
        {
            ApplyHiddenRanges(newHiddenAreas);
        }
    }

    private void ApplyHiddenRanges(IReadOnlyList<TextRange> newHiddenAreas)
    {
        _hiddenRanges = newHiddenAreas;
        _hasLineChanges = false;
        DidChange?.Invoke(newHiddenAreas);
    }

    public bool HasRanges() => _hiddenRanges.Count > 0;

    public bool IsHidden(int line) => FindRange(_hiddenRanges, line) is not null;

    /// <summary>Moves selection ends out of hidden ranges, to the end of the line before them; true when one moved.</summary>
    public bool AdjustSelections(IList<TextSelection> selections)
    {
        var hasChanges = false;
        var editorModel = _foldingModel.TextModel;
        TextRange? lastRange = null;

        int? AdjustLine(int line)
        {
            if (lastRange is null || !IsInside(line, lastRange.Value))
            {
                lastRange = FindRange(_hiddenRanges, line);
            }

            if (lastRange is { } range)
            {
                return range.StartLineNumber - 1;
            }

            return null;
        }

        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var selection = selections[i];
            var adjustedStartLine = AdjustLine(selection.StartLineNumber);
            if (adjustedStartLine is { } start and not 0)
            {
                selection = selection.SetStartPosition(start, editorModel.GetLineMaxColumn(start));
                hasChanges = true;
            }

            var adjustedEndLine = AdjustLine(selection.EndLineNumber);
            if (adjustedEndLine is { } end and not 0)
            {
                selection = selection.SetEndPosition(end, editorModel.GetLineMaxColumn(end));
                hasChanges = true;
            }

            selections[i] = selection;
        }

        return hasChanges;
    }

    public void Dispose()
    {
        if (_hiddenRanges.Count > 0)
        {
            _hiddenRanges = [];
            DidChange?.Invoke(_hiddenRanges);
        }

        if (_listening)
        {
            _foldingModel.DidChange -= OnFoldingModelChanged;
            _listening = false;
        }

        DidChange = null;
    }

    private static bool IsInside(int line, TextRange range) => line >= range.StartLineNumber && line <= range.EndLineNumber;

    private static TextRange? FindRange(IReadOnlyList<TextRange> ranges, int line)
    {
        // findFirstIdxMonotonousOrArrLen(ranges, r => line < r.startLineNumber) - 1
        int low = 0, high = ranges.Count;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (line < ranges[mid].StartLineNumber)
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }

        var i = low - 1;
        if (i >= 0 && ranges[i].EndLineNumber >= line)
        {
            return ranges[i];
        }

        return null;
    }
}
