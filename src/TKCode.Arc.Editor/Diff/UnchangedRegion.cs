// Ported from vscode (src/vs/editor/browser/widget/diffEditor/diffEditorViewModel.ts: UnchangedRegion and
// RevealPreference, commit b57d5c9e), MIT license.
// Differences:
// - Pure state without observables: the visible line counts are plain properties and Changed is raised when they
//   change (upstream's ISettableObservable with transactions). isDragged / shouldHideControls (the drag handles) are
//   not ported.
// - It lives in the engine (it depends on nothing of the browser), so the diff editor control and the engine's tests
//   share it.

namespace TKCode.Arc.Editor.Diff;

/// <summary>Where a hidden line is revealed from (upstream's RevealPreference).</summary>
internal enum RevealPreference
{
    FromCloserSide,
    FromTop,
    FromBottom,
}

/// <summary>
/// A run of lines that are the same on both sides of a diff and that the diff editor can hide: <see cref="LineCount"/>
/// lines from <see cref="OriginalLineNumber"/> / <see cref="ModifiedLineNumber"/>, of which the first
/// <see cref="VisibleLineCountTop"/> and the last <see cref="VisibleLineCountBottom"/> are shown.
/// </summary>
internal sealed class UnchangedRegion
{
    /// <summary>
    /// The regions between the changes that are long enough to hide, keeping <paramref name="minContext"/> lines next
    /// to each change and hiding at least <paramref name="minHiddenLineCount"/> lines.
    /// </summary>
    public static List<UnchangedRegion> FromDiffs(
        IReadOnlyList<DetailedLineRangeMapping> changes,
        int originalLineCount,
        int modifiedLineCount,
        int minHiddenLineCount,
        int minContext)
    {
        var inversedMappings = LineRangeMapping.Inverse(changes, originalLineCount, modifiedLineCount);
        var result = new List<UnchangedRegion>();

        foreach (var mapping in inversedMappings)
        {
            var origStart = mapping.Original.StartLineNumber;
            var modStart = mapping.Modified.StartLineNumber;
            var length = mapping.Original.Length;

            var atStart = origStart == 1 && modStart == 1;
            var atEnd = origStart + length == originalLineCount + 1 && modStart + length == modifiedLineCount + 1;

            if ((atStart || atEnd) && length >= minContext + minHiddenLineCount)
            {
                if (atStart && !atEnd)
                {
                    length -= minContext;
                }

                if (atEnd && !atStart)
                {
                    origStart += minContext;
                    modStart += minContext;
                    length -= minContext;
                }

                result.Add(new UnchangedRegion(origStart, modStart, length, 0, 0));
            }
            else if (length >= minContext * 2 + minHiddenLineCount)
            {
                origStart += minContext;
                modStart += minContext;
                length -= minContext * 2;
                result.Add(new UnchangedRegion(origStart, modStart, length, 0, 0));
            }
        }

        return result;
    }

    private int _visibleLineCountTop;
    private int _visibleLineCountBottom;

    public UnchangedRegion(int originalLineNumber, int modifiedLineNumber, int lineCount, int visibleLineCountTop, int visibleLineCountBottom)
    {
        OriginalLineNumber = originalLineNumber;
        ModifiedLineNumber = modifiedLineNumber;
        LineCount = lineCount;
        _visibleLineCountTop = Math.Max(Math.Min(visibleLineCountTop, lineCount), 0);
        _visibleLineCountBottom = Math.Max(Math.Min(visibleLineCountBottom, lineCount - visibleLineCountTop), 0);
    }

    public int OriginalLineNumber { get; }

    public int ModifiedLineNumber { get; }

    public int LineCount { get; }

    /// <summary>Raised when the visible line counts changed.</summary>
    public event Action<UnchangedRegion>? Changed;

    public LineRange OriginalUnchangedRange => LineRangeDiffExtensions.OfLength(OriginalLineNumber, LineCount);

    public LineRange ModifiedUnchangedRange => LineRangeDiffExtensions.OfLength(ModifiedLineNumber, LineCount);

    /// <summary>The number of lines shown at the top of the region.</summary>
    public int VisibleLineCountTop => _visibleLineCountTop;

    /// <summary>The number of lines shown at the bottom of the region.</summary>
    public int VisibleLineCountBottom => _visibleLineCountBottom;

    /// <summary>Whether no line is hidden.</summary>
    public bool IsFullyVisible => _visibleLineCountTop + _visibleLineCountBottom >= LineCount;

    /// <summary>Splits the region at the given visible ranges (upstream's setVisibleRanges).</summary>
    public List<UnchangedRegion> SetVisibleRanges(IReadOnlyList<LineRangeMapping> visibleRanges)
    {
        var result = new List<UnchangedRegion>();

        var hiddenModified = new LineRangeSet(visibleRanges.Select(r => r.Modified)).SubtractFrom(ModifiedUnchangedRange);

        var originalStartLineNumber = OriginalLineNumber;
        var modifiedStartLineNumber = ModifiedLineNumber;
        var modifiedEndLineNumberEx = ModifiedLineNumber + LineCount;
        if (hiddenModified.Ranges.Count == 0)
        {
            ShowAll();
            result.Add(this);
        }
        else
        {
            var i = 0;
            foreach (var r in hiddenModified.Ranges)
            {
                var isLast = i == hiddenModified.Ranges.Count - 1;
                i++;

                var length = (isLast ? modifiedEndLineNumberEx : r.EndLineNumberExclusive) - modifiedStartLineNumber;

                var newR = new UnchangedRegion(originalStartLineNumber, modifiedStartLineNumber, length, 0, 0);
                newR.SetHiddenModifiedRange(r);
                result.Add(newR);

                originalStartLineNumber = newR.OriginalUnchangedRange.EndLineNumberExclusive;
                modifiedStartLineNumber = newR.ModifiedUnchangedRange.EndLineNumberExclusive;
            }
        }

        return result;
    }

    public LineRange GetHiddenOriginalRange() =>
        LineRangeDiffExtensions.OfLength(OriginalLineNumber + _visibleLineCountTop, LineCount - _visibleLineCountTop - _visibleLineCountBottom);

    public LineRange GetHiddenModifiedRange() =>
        LineRangeDiffExtensions.OfLength(ModifiedLineNumber + _visibleLineCountTop, LineCount - _visibleLineCountTop - _visibleLineCountBottom);

    public void SetHiddenModifiedRange(LineRange range)
    {
        var visibleLineCountTop = range.StartLineNumber - ModifiedLineNumber;
        var visibleLineCountBottom = ModifiedLineNumber + LineCount - range.EndLineNumberExclusive;
        SetState(visibleLineCountTop, visibleLineCountBottom);
    }

    public int GetMaxVisibleLineCountTop() => LineCount - _visibleLineCountBottom;

    public int GetMaxVisibleLineCountBottom() => LineCount - _visibleLineCountTop;

    public void ShowMoreAbove(int count = 10) =>
        Set(Math.Min(_visibleLineCountTop + count, GetMaxVisibleLineCountTop()), _visibleLineCountBottom);

    public void ShowMoreBelow(int count = 10) =>
        Set(_visibleLineCountTop, Math.Min(_visibleLineCountBottom + count, LineCount - _visibleLineCountTop));

    public void ShowAll() => Set(_visibleLineCountTop, LineCount - _visibleLineCountTop);

    public void ShowModifiedLine(int lineNumber, RevealPreference preference)
    {
        var top = lineNumber + 1 - (ModifiedLineNumber + _visibleLineCountTop);
        var bottom = ModifiedLineNumber - _visibleLineCountBottom + LineCount - lineNumber;
        if ((preference == RevealPreference.FromCloserSide && top < bottom) || preference == RevealPreference.FromTop)
        {
            Set(_visibleLineCountTop + top, _visibleLineCountBottom);
        }
        else
        {
            Set(_visibleLineCountTop, _visibleLineCountBottom + bottom);
        }
    }

    public void ShowOriginalLine(int lineNumber, RevealPreference preference)
    {
        var top = lineNumber - OriginalLineNumber;
        var bottom = OriginalLineNumber + LineCount - lineNumber;
        if ((preference == RevealPreference.FromCloserSide && top < bottom) || preference == RevealPreference.FromTop)
        {
            Set(Math.Min(_visibleLineCountTop + bottom - top, GetMaxVisibleLineCountTop()), _visibleLineCountBottom);
        }
        else
        {
            Set(_visibleLineCountTop, Math.Min(_visibleLineCountBottom + top - bottom, GetMaxVisibleLineCountBottom()));
        }
    }

    public void CollapseAll() => Set(0, 0);

    public void SetState(int visibleLineCountTop, int visibleLineCountBottom)
    {
        visibleLineCountTop = Math.Max(Math.Min(visibleLineCountTop, LineCount), 0);
        visibleLineCountBottom = Math.Max(Math.Min(visibleLineCountBottom, LineCount - visibleLineCountTop), 0);
        Set(visibleLineCountTop, visibleLineCountBottom);
    }

    private void Set(int top, int bottom)
    {
        if (top == _visibleLineCountTop && bottom == _visibleLineCountBottom)
        {
            return;
        }

        _visibleLineCountTop = top;
        _visibleLineCountBottom = bottom;
        Changed?.Invoke(this);
    }

    public override string ToString() =>
        $"UnchangedRegion(original {OriginalLineNumber}, modified {ModifiedLineNumber}, {LineCount} lines, top {_visibleLineCountTop}, bottom {_visibleLineCountBottom})";
}
