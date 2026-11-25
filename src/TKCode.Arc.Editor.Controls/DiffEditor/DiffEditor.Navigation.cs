// Scrolling, navigation and reverting in the diff editor (upstream's DiffEditorWidget.goToDiff / revert and
// RevertButtonsFeature in features/revertButtonsFeature.ts, and the scroll synchronization of DiffEditorViewZones;
// src/vs/editor/browser/widget/diffEditor, commit b57d5c9e; MIT license).
// Differences: both editors share the scroll position directly (their heights match through the alignment zones;
// upstream also follows animated scroll offsets); a revert replaces the change through LineRangeMapping.toRangeMapping
// (upstream: toExclusiveRange, which leaves a line break behind at the end of the document); the revert arrows are a
// gutter column of the modified editor and are not offered for selections (upstream: a button that reverts the
// selected part of a change).

using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Diff;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

public sealed partial class DiffEditor
{
    /// <summary>An arrow pointing right (from the original into the modified text) in a 16 × 16 box.</summary>
    internal static readonly Geometry RevertArrowGlyph = Geometry.Parse("M3,7.3 H10.2 L7.6,4.7 L8.6,3.7 L12.9,8 L8.6,12.3 L7.6,11.3 L10.2,8.7 H3 Z");

    internal const string RevertColumnId = "diffRevert";

    private CodeEditorGutterColumn? _revertColumn;
    private IDisposable? _revertColumnRegistration;
    private int? _hoveredRevertLine;
    private bool _syncingScroll;

    /// <summary>Whether changes can be reverted (side by side, modified text editable).</summary>
    internal bool CanRevert => RenderSideBySide && !IsReadOnly;

    private void UpdateGutterColumns()
    {
        foreach (var editor in new[] { _originalEditor, _modifiedEditor })
        {
            if (editor is not null)
            {
                // the + / − signs (upstream's renderIndicators, on by default)
                editor.Gutter.LinesDecorations.Width = IndicatorWidth;
                editor.Gutter.LinesDecorations.IsVisible = true;
            }
        }

        if (_modifiedEditor is { } modified && _revertColumn is null)
        {
            _revertColumn = new CodeEditorGutterColumn(RevertColumnId, -50, RevertColumnWidth)
            {
                Draw = DrawRevertArrow,
                PointerPressed = OnRevertColumnPressed,
                PointerMoved = e =>
                {
                    _hoveredRevertLine = e?.ModelLineNumber;
                    _revertColumn?.Invalidate();
                },
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            _revertColumnRegistration = modified.Gutter.Add(_revertColumn);
        }

        if (_revertColumn is { } column)
        {
            column.Width = RevertColumnWidth;
        }

        UpdateRevertColumn();
    }

    private void UpdateRevertColumn()
    {
        if (_revertColumn is { } column)
        {
            column.IsVisible = CanRevert;
            column.Invalidate();
        }
    }

    private void RemoveGutterColumns()
    {
        _revertColumnRegistration?.Dispose();
        _revertColumnRegistration = null;
        _revertColumn = null;
    }

    /// <summary>The change whose revert arrow is on a modified line: a change with inserted or changed lines that starts there.</summary>
    internal DetailedLineRangeMapping? GetRevertableChangeAt(int modifiedLineNumber)
    {
        if (_diff is not { } diff)
        {
            return null;
        }

        foreach (var change in diff.Changes)
        {
            if (!change.Modified.IsEmpty && change.InnerChanges is not null && change.Modified.StartLineNumber == modifiedLineNumber)
            {
                return change;
            }
        }

        return null;
    }

    private void DrawRevertArrow(DrawingContext context, CodeEditorGutterLine line)
    {
        if (!line.IsFirstOfModelLine || !IsDiffUpToDate || GetRevertableChangeAt(line.ModelLineNumber) is null)
        {
            return;
        }

        var brush = _hoveredRevertLine == line.ModelLineNumber ? Foreground ?? RevertArrowBrush : RevertArrowBrush;
        if (brush is not null)
        {
            DrawGlyph(context, RevertArrowGlyph, brush, line.Bounds);
        }
    }

    /// <summary>Draws a 16 × 16 glyph scaled into a cell and centered.</summary>
    internal static void DrawGlyph(DrawingContext context, Geometry glyph, IBrush brush, Avalonia.Rect cell)
    {
        var size = Math.Min(16, Math.Min(cell.Width, cell.Height));
        if (size <= 0)
        {
            return;
        }

        var scale = size / 16;
        var x = cell.X + (cell.Width - size) / 2;
        var y = cell.Y + (cell.Height - size) / 2;
        using (context.PushTransform(Avalonia.Matrix.CreateScale(scale, scale) * Avalonia.Matrix.CreateTranslation(x, y)))
        {
            context.DrawGeometry(brush, null, glyph);
        }
    }

    private bool OnRevertColumnPressed(CodeEditorGutterPointerEvent e)
    {
        if (GetRevertableChangeAt(e.ModelLineNumber) is not { } change)
        {
            return false;
        }

        Revert(change);
        return true;
    }

    /// <summary>
    /// Replaces the lines of a change in the modified text with the original lines (upstream's revert); one undo step.
    /// False when the diff is not up to date or the modified text is read-only.
    /// </summary>
    internal bool Revert(LineRangeMapping change)
    {
        if (!IsDiffUpToDate || IsReadOnly || _originalEditor is not { } original || _modifiedEditor is not { } modified)
        {
            return false;
        }

        var rangeMapping = change.ToRangeMapping();
        var text = original.Model.GetValueInRange(original.Model.ValidateRange(rangeMapping.OriginalRange));
        modified.PushUndoStop();
        var applied = modified.ExecuteEdits("diffEditor", [new SingleEditOperation(modified.Model.ValidateRange(rangeMapping.ModifiedRange), text)]);
        modified.PushUndoStop();
        return applied;
    }

    /// <summary>
    /// Moves the modified editor's cursor to the start of the next (or previous) change and scrolls it to the center,
    /// wrapping around (upstream's goToDiff). False without changes.
    /// </summary>
    internal bool GoToDiff(bool next)
    {
        if (_diff is not { Changes.Count: > 0 } diff || _modifiedEditor is not { } modified)
        {
            return false;
        }

        var changes = diff.Changes;
        var curLineNumber = modified.GetPosition()?.LineNumber ?? 1;
        DetailedLineRangeMapping? target = null;
        if (next)
        {
            if (modified.Model.GetLineCount() == curLineNumber)
            {
                target = changes[0];
            }
            else
            {
                target = changes.FirstOrDefault(d => d.Modified.StartLineNumber > curLineNumber) ?? changes[0];
            }
        }
        else
        {
            target = changes.LastOrDefault(d => d.Modified.StartLineNumber < curLineNumber) ?? changes[^1];
        }

        GoTo(target);
        return true;
    }

    private void GoTo(DetailedLineRangeMapping change)
    {
        if (_modifiedEditor is not { } modified)
        {
            return;
        }

        var lineNumber = Math.Min(change.Modified.StartLineNumber, modified.Model.GetLineCount());
        EnsureModifiedLineIsVisible(lineNumber, RevealPreference.FromCloserSide);
        modified.SetPosition(new TextPosition(lineNumber, 1));
        var endLine = Math.Max(lineNumber, Math.Min(change.Modified.EndLineNumberExclusive, modified.Model.GetLineCount()));
        modified.RevealRange(new TextRange(lineNumber, 1, endLine, 1), VerticalRevealType.Center, ScrollType.Immediate, revealHorizontal: false);
    }

    private void OnOriginalScrollChanged(ScrollChangedEvent e) => SyncScroll(e, _modifiedEditor);

    private void OnModifiedScrollChanged(ScrollChangedEvent e)
    {
        SyncScroll(e, _originalEditor);
        _overviewRuler?.InvalidateVisual();
    }

    // upstream: the scroll top of one side follows the other (DiffEditorViewZones), and so does the scroll left
    private void SyncScroll(ScrollChangedEvent e, CodeEditor? other)
    {
        _movedBlocks?.InvalidateVisual();
        if (_syncingScroll || !RenderSideBySide || other?.ViewModel is not { } viewModel || (!e.ScrollTopChanged && !e.ScrollLeftChanged))
        {
            return;
        }

        _syncingScroll = true;
        try
        {
            viewModel.ViewLayout.SetScrollPosition(
                new NewScrollPosition(e.ScrollLeftChanged ? e.ScrollLeft : null, e.ScrollTopChanged ? e.ScrollTop : null),
                ScrollType.Immediate);
        }
        finally
        {
            _syncingScroll = false;
        }
    }

    private void OnContentSizeChanged(ContentSizeChangedEvent e)
    {
        _movedBlocks?.InvalidateVisual();
        _overviewRuler?.InvalidateVisual();
    }

    /// <summary>Scrolls the modified editor (and so the original one) to a vertical offset.</summary>
    internal void ScrollTo(double scrollTop)
    {
        _modifiedEditor?.ViewModel?.ViewLayout.SetScrollPosition(new NewScrollPosition(null, Math.Max(0, scrollTop)), ScrollType.Immediate);
    }
}
