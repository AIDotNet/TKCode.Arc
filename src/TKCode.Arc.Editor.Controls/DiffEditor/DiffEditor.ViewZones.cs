// The diff editor's view zones and hidden areas (upstream's DiffEditorViewZones in
// src/vs/editor/browser/widget/diffEditor/components/diffEditorViewZones/diffEditorViewZones.ts and
// HideUnchangedRegionsFeature in features/hideUnchangedRegionsFeature.ts, commit b57d5c9e; MIT license):
// - side by side, empty space (diagonal fill) on the shorter side of each alignment, so both sides line up;
// - inline, a zone before each change in the modified editor that shows the removed lines;
// - with HideUnchangedRegions, the unchanged regions hidden on both sides with a bar that shows them again.
// Differences: the zones are rebuilt as a whole when anything changes (upstream diffs observable lists); the alignment
// counts wrapped lines but not foreign view zones (the diff editor owns every zone of its editors); the bar of a hidden
// region expands it fully on click (upstream: drag handles, "show more above / below" and an outline of the hidden
// symbols); synced moved text (movedTextToCompare) is not ported.

using Avalonia.Controls;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Diff;
using TKCode.Arc.Editor.ViewModel;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc;

public sealed partial class DiffEditor
{
    /// <summary>The lines kept visible next to a change (upstream's hideUnchangedRegions.contextLineCount).</summary>
    internal const int UnchangedRegionContextLineCount = 3;

    /// <summary>The fewest lines a hidden region has (upstream's hideUnchangedRegions.minimumLineCount).</summary>
    internal const int UnchangedRegionMinimumLineCount = 3;

    private readonly List<string> _originalZoneIds = [];
    private readonly List<string> _modifiedZoneIds = [];
    private List<UnchangedRegion> _unchangedRegions = [];

    /// <summary>The unchanged regions (empty unless <see cref="HideUnchangedRegions"/>).</summary>
    internal IReadOnlyList<UnchangedRegion> UnchangedRegions => _unchangedRegions;

    /// <summary>The ids of the view zones of the original editor (tests).</summary>
    internal IReadOnlyList<string> OriginalZoneIds => _originalZoneIds;

    /// <summary>The ids of the view zones of the modified editor (tests).</summary>
    internal IReadOnlyList<string> ModifiedZoneIds => _modifiedZoneIds;

    private void UpdateUnchangedRegions(DocumentDiff? diff)
    {
        var old = _unchangedRegions;
        foreach (var region in old)
        {
            region.Changed -= OnUnchangedRegionChanged;
        }

        if (!HideUnchangedRegions || diff is null || _originalEditor is not { } original || _modifiedEditor is not { } modified)
        {
            _unchangedRegions = [];
            ApplyHiddenAreas();
            return;
        }

        var modifiedLineCount = modified.Model.GetLineCount();
        var regions = UnchangedRegion.FromDiffs(diff.Changes, original.Model.GetLineCount(), modifiedLineCount,
            UnchangedRegionMinimumLineCount, UnchangedRegionContextLineCount);
        if (regions.Count == 1 && regions[0].ModifiedLineNumber == 1 && regions[0].LineCount == modifiedLineCount)
        {
            // everything unchanged: nothing to hide
            regions = [];
        }

        foreach (var region in regions)
        {
            // a region the user opened stays open when the diff is computed again
            if (old.Any(o => o.IsFullyVisible && o.ModifiedUnchangedRange.IntersectsStrict(region.ModifiedUnchangedRange)))
            {
                region.ShowAll();
            }

            region.Changed += OnUnchangedRegionChanged;
        }

        _unchangedRegions = regions;
        ApplyHiddenAreas();
    }

    private void OnUnchangedRegionChanged(UnchangedRegion region)
    {
        ApplyHiddenAreas();
        UpdateViewZones();
        _movedBlocks?.InvalidateVisual();
        _overviewRuler?.InvalidateVisual();
    }

    // upstream: editor.setHiddenAreas(regions.map(r => r.getHiddenXRange().toInclusiveRange()))
    private void ApplyHiddenAreas()
    {
        ApplyHiddenAreas(_originalEditor?.ViewModel, r => r.GetHiddenOriginalRange());
        ApplyHiddenAreas(_modifiedEditor?.ViewModel, r => r.GetHiddenModifiedRange());
    }

    private void ApplyHiddenAreas(EditorViewModel? viewModel, Func<UnchangedRegion, LineRange> getHiddenRange)
    {
        if (viewModel is null)
        {
            return;
        }

        var ranges = new List<TextRange>();
        foreach (var region in _unchangedRegions)
        {
            if (getHiddenRange(region).ToInclusiveRange() is { } range)
            {
                ranges.Add(range);
            }
        }

        viewModel.SetHiddenAreas(ranges, this);
    }

    private void OnOriginalViewModelChanged(EditorViewModel viewModel)
    {
        ApplyHiddenAreas(viewModel, r => r.GetHiddenOriginalRange());
        UpdateViewZones();
    }

    private void OnModifiedViewModelChanged(EditorViewModel viewModel)
    {
        ApplyHiddenAreas(viewModel, r => r.GetHiddenModifiedRange());
        UpdateViewZones();
    }

    // upstream: hideUnchangedRegionsFeature reveals the lines the cursor moves to
    private void OnOriginalCursorChanged(CursorStateChangedEvent e)
    {
        if (_originalEditor is not { } original || _unchangedRegions.Count == 0 || e.Reason == CursorChangeReason.ContentFlush)
        {
            return;
        }

        foreach (var selection in original.GetSelections())
        {
            EnsureOriginalLineIsVisible(selection.StartLineNumber);
            EnsureOriginalLineIsVisible(selection.EndLineNumber);
        }
    }

    private void OnModifiedCursorChanged(CursorStateChangedEvent e)
    {
        if (_modifiedEditor is not { } modified || _unchangedRegions.Count == 0 || e.Reason == CursorChangeReason.ContentFlush)
        {
            return;
        }

        foreach (var selection in modified.GetSelections())
        {
            EnsureModifiedLineIsVisible(selection.StartLineNumber, RevealPreference.FromCloserSide);
            EnsureModifiedLineIsVisible(selection.EndLineNumber, RevealPreference.FromCloserSide);
        }
    }

    /// <summary>Shows a modified line that is in a hidden region (upstream's ensureModifiedLineIsVisible).</summary>
    internal void EnsureModifiedLineIsVisible(int lineNumber, RevealPreference preference)
    {
        if (_diff is not { Changes.Count: > 0 })
        {
            return;
        }

        foreach (var region in _unchangedRegions)
        {
            if (region.GetHiddenModifiedRange().Contains(lineNumber))
            {
                region.ShowModifiedLine(lineNumber, preference);
                return;
            }
        }
    }

    /// <summary>Shows an original line that is in a hidden region (upstream's ensureOriginalLineIsVisible).</summary>
    internal void EnsureOriginalLineIsVisible(int lineNumber)
    {
        if (_diff is not { Changes.Count: > 0 })
        {
            return;
        }

        foreach (var region in _unchangedRegions)
        {
            if (region.GetHiddenOriginalRange().Contains(lineNumber))
            {
                region.ShowOriginalLine(lineNumber, RevealPreference.FromCloserSide);
                return;
            }
        }
    }

    /// <summary>
    /// The space wrapped lines take beyond their first view line (upstream's getAdditionalLineHeights without the
    /// foreign view zones: every zone of the two editors belongs to the diff editor).
    /// </summary>
    private static List<AdditionalLineHeight> GetAdditionalLineHeights(CodeEditor editor, double lineHeight)
    {
        var result = new List<AdditionalLineHeight>();
        if (!editor.WordWrap || editor.ViewModel is not { } viewModel)
        {
            return result;
        }

        var converter = viewModel.CoordinatesConverter;
        var lineCount = editor.Model.GetLineCount();
        for (var line = 1; line <= lineCount; line++)
        {
            var count = converter.GetModelLineViewLineCount(line);
            if (count > 1)
            {
                result.Add(new AdditionalLineHeight(line, lineHeight * (count - 1)));
            }
        }

        return result;
    }

    /// <summary>Puts the zones of the current diff, mode and unchanged regions into the editors.</summary>
    internal void UpdateViewZones()
    {
        if (_originalEditor is not { } original || _modifiedEditor is not { } modified)
        {
            return;
        }

        var originalZones = new List<CodeEditorViewZone>();
        var modifiedZones = new List<CodeEditorViewZone>();
        var sideBySide = RenderSideBySide;
        var originalLineHeight = original.View?.LineHeight ?? 0;
        var modifiedLineHeight = modified.View?.LineHeight ?? 0;

        if (_diff is { } diff && modifiedLineHeight > 0 && (originalLineHeight > 0 || !sideBySide))
        {
            if (originalLineHeight <= 0)
            {
                originalLineHeight = modifiedLineHeight;
            }

            var originalModel = original.Model;
            var originalLineCount = originalModel.GetLineCount();
            var alignments = DiffAlignment.ComputeRangeAlignment(
                diff.Changes,
                sideBySide ? GetAdditionalLineHeights(original, originalLineHeight) : [],
                GetAdditionalLineHeights(modified, modifiedLineHeight),
                originalLineHeight,
                modifiedLineHeight,
                innerHunkAlignment: sideBySide,
                line => originalModel.GetLineMaxColumn(line),
                originalLineCount);

            foreach (var a in alignments)
            {
                if (a.Diff is { } change && !sideBySide)
                {
                    if (!a.OriginalRange.IsEmpty && a.OriginalRange.EndLineNumberExclusive - 1 <= originalLineCount)
                    {
                        modifiedZones.Add(new CodeEditorViewZone
                        {
                            AfterLineNumber = a.ModifiedRange.StartLineNumber - 1,
                            HeightInPx = a.OriginalRange.Length * modifiedLineHeight,
                            Control = new DiffEditorDeletedLinesView(this, modified, ReadLines(originalModel, a.OriginalRange), a.OriginalRange.StartLineNumber, change),
                            MarginControl = new DiffEditorDeletedLinesMargin(this, modified, a.OriginalRange.Length),
                            ShowInHiddenAreas = true,
                        });
                    }

                    continue;
                }

                var delta = a.ModifiedHeightInPx - a.OriginalHeightInPx;
                if (!sideBySide || Math.Abs(delta) < 0.5)
                {
                    continue;
                }

                if (delta > 0)
                {
                    originalZones.Add(new CodeEditorViewZone
                    {
                        AfterLineNumber = a.OriginalRange.EndLineNumberExclusive - 1,
                        HeightInPx = delta,
                        Control = new DiffEditorEmptyLinesView(this),
                        ShowInHiddenAreas = true,
                        SuppressMouseDown = true,
                    });
                }
                else
                {
                    Control? margin = null;
                    if (a.Diff is { Modified.IsEmpty: true } deletion && CanRevert)
                    {
                        margin = new DiffEditorRevertArrowView(this, modified, deletion);
                    }

                    modifiedZones.Add(new CodeEditorViewZone
                    {
                        AfterLineNumber = a.ModifiedRange.EndLineNumberExclusive - 1,
                        HeightInPx = -delta,
                        Control = new DiffEditorEmptyLinesView(this),
                        MarginControl = margin,
                        ShowInHiddenAreas = true,
                        SuppressMouseDown = true,
                    });
                }
            }
        }

        foreach (var region in _unchangedRegions)
        {
            var hiddenOriginal = region.GetHiddenOriginalRange();
            var hiddenModified = region.GetHiddenModifiedRange();
            if (hiddenModified.IsEmpty)
            {
                continue;
            }

            if (sideBySide)
            {
                originalZones.Add(CreateCollapsedRegionZone(region, hiddenOriginal.StartLineNumber - 1, hiddenOriginal.Length));
            }

            modifiedZones.Add(CreateCollapsedRegionZone(region, hiddenModified.StartLineNumber - 1, hiddenModified.Length));
        }

        ReplaceZones(original, _originalZoneIds, originalZones);
        ReplaceZones(modified, _modifiedZoneIds, modifiedZones);
        _movedBlocks?.InvalidateVisual();
        _overviewRuler?.InvalidateVisual();
    }

    private CodeEditorViewZone CreateCollapsedRegionZone(UnchangedRegion region, int afterLineNumber, int hiddenLineCount) => new()
    {
        AfterLineNumber = afterLineNumber,
        HeightInPx = CollapsedRegionHeight,
        Control = new DiffEditorCollapsedRegionView(this, region, hiddenLineCount),
        // the bar goes on under the gutter
        MarginControl = new DiffEditorCollapsedRegionView(this, region, hiddenLineCount, showLabel: false),
        ShowInHiddenAreas = true,
        SuppressMouseDown = true,
    };

    private static string[] ReadLines(Editor.Model.TextModel model, LineRange range)
    {
        var lines = new string[range.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = model.GetLineContent(range.StartLineNumber + i);
        }

        return lines;
    }

    private static void ReplaceZones(CodeEditor editor, List<string> ids, List<CodeEditorViewZone> zones)
    {
        if (ids.Count == 0 && zones.Count == 0)
        {
            return;
        }

        editor.ChangeViewZones(accessor =>
        {
            foreach (var id in ids)
            {
                accessor.RemoveZone(id);
            }

            ids.Clear();
            foreach (var zone in zones)
            {
                ids.Add(accessor.AddZone(zone));
            }
        });
    }

    private void ClearViewZones()
    {
        if (_originalEditor is { } original)
        {
            ReplaceZones(original, _originalZoneIds, []);
        }

        if (_modifiedEditor is { } modified)
        {
            ReplaceZones(modified, _modifiedZoneIds, []);
        }

        _originalZoneIds.Clear();
        _modifiedZoneIds.Clear();
        foreach (var region in _unchangedRegions)
        {
            region.Changed -= OnUnchangedRegionChanged;
        }

        _unchangedRegions = [];
        ApplyHiddenAreas();
    }
}
