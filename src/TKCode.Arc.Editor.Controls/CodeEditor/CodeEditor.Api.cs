// The editor operations features call (upstream's ICodeEditor in codeEditorWidget.ts: getPosition, getSelection(s),
// setPosition, setSelection(s), revealLine / revealRange (InCenter / InCenterIfOutsideViewport / AtTop),
// executeEdits, getScrolledVisiblePosition, getTopForLineNumber, focus; commit b57d5c9e), MIT license.
// Differences from upstream: internal members of the control; they do nothing (or return null) before the editor has
// a view model; executeEdits takes the end cursor state as selections or a CursorStateComputer.

using Avalonia;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

public sealed partial class CodeEditor
{
    /// <summary>The primary cursor's model position, or null without a view model (upstream's getPosition).</summary>
    internal TextPosition? GetPosition() => ViewModel?.GetPosition();

    /// <summary>The primary selection (upstream's getSelection).</summary>
    internal TextSelection? GetSelection() => ViewModel?.GetSelection();

    /// <summary>Every selection, the primary first (upstream's getSelections).</summary>
    internal IReadOnlyList<TextSelection> GetSelections() => ViewModel?.GetSelections() ?? [];

    /// <summary>Moves the cursor to a model position, selecting nothing (upstream's setPosition).</summary>
    internal void SetPosition(TextPosition position, string source = "api") =>
        SetSelections([new TextSelection(position.LineNumber, position.Column, position.LineNumber, position.Column)], source);

    /// <summary>Sets the primary selection, removing the others (upstream's setSelection).</summary>
    internal void SetSelection(TextSelection selection, string source = "api") => SetSelections([selection], source);

    /// <summary>Sets the selections (upstream's setSelections).</summary>
    internal void SetSelections(IReadOnlyList<TextSelection> selections, string source = "api", CursorChangeReason reason = CursorChangeReason.NotSet) =>
        ViewModel?.SetSelections(source, selections, reason);

    /// <summary>Scrolls a model range into view (upstream's revealRange and its InCenter / AtTop variants).</summary>
    internal void RevealRange(TextRange range, VerticalRevealType verticalType = VerticalRevealType.Simple, ScrollType scrollType = ScrollType.Smooth, bool revealHorizontal = true)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var viewRange = viewModel.CoordinatesConverter.ConvertModelRangeToViewRange(Model.ValidateRange(range));
        viewModel.RevealRange("api", revealHorizontal, viewRange, verticalType, scrollType);
    }

    /// <summary>Scrolls a model line into view (upstream's revealLine).</summary>
    internal void RevealLine(int lineNumber, VerticalRevealType verticalType = VerticalRevealType.Simple, ScrollType scrollType = ScrollType.Smooth) =>
        RevealRange(new TextRange(lineNumber, 1, lineNumber, 1), verticalType, scrollType, revealHorizontal: false);

    /// <summary>
    /// Applies edits as one undoable step and sets the cursors after them (upstream's executeEdits); false in a
    /// read-only editor. <paramref name="endCursorState"/> gives the selections afterwards; null leaves them to the edits.
    /// </summary>
    internal bool ExecuteEdits(string? source, IReadOnlyList<SingleEditOperation> edits, IReadOnlyList<TextSelection>? endCursorState = null) =>
        ExecuteEdits(source, edits, endCursorState is null ? static _ => null : _ => endCursorState);

    /// <summary>Applies edits as one undoable step; <paramref name="cursorStateComputer"/> computes the selections afterwards.</summary>
    internal bool ExecuteEdits(string? source, IReadOnlyList<SingleEditOperation> edits, CursorStateComputer cursorStateComputer)
    {
        if (ViewModel is not { } viewModel || IsReadOnly)
        {
            return false;
        }

        viewModel.ExecuteEdits(source, edits, cursorStateComputer, EditSources.Unknown(source));
        return true;
    }

    /// <summary>
    /// Where a model position is drawn, relative to the editor's view: the top and left of its column and the line's
    /// height; null when its line is not on screen (upstream's getScrolledVisiblePosition).
    /// </summary>
    internal (double Top, double Left, double Height)? GetScrolledVisiblePosition(TextPosition position)
    {
        if (_view is not { ViewModel: { } viewModel } view)
        {
            return null;
        }

        var viewPosition = viewModel.CoordinatesConverter.ConvertModelPositionToViewPosition(Model.ValidatePosition(position));
        var viewport = viewModel.ViewLayout.GetLinesViewportData();
        if (viewPosition.LineNumber < viewport.StartLineNumber || viewPosition.LineNumber > viewport.EndLineNumber)
        {
            return null;
        }

        return (view.GetY(viewPosition.LineNumber), view.GetX(viewPosition.LineNumber, viewPosition.Column), viewModel.ViewLayout.GetLineHeightForLineNumber(viewPosition.LineNumber));
    }

    /// <summary>The vertical offset of a model line in the scrollable content (upstream's getTopForLineNumber).</summary>
    internal double GetTopForLineNumber(int lineNumber)
    {
        if (ViewModel is not { } viewModel)
        {
            return 0;
        }

        var viewLine = viewModel.CoordinatesConverter.ConvertModelPositionToViewPosition(new TextPosition(Model.ValidatePosition(new TextPosition(lineNumber, 1)).LineNumber, 1)).LineNumber;
        return viewModel.ViewLayout.GetVerticalOffsetForLineNumber(viewLine);
    }

    /// <summary>The size of the editor's view (upstream's getLayoutInfo width and height), or an empty size.</summary>
    internal Size GetLayoutSize() => _view?.Bounds.Size ?? default;

    /// <summary>Builds the keybinding resolver again (after keybindings were registered or removed at run time).</summary>
    internal void InvalidateKeybindings() => _view?.InvalidateKeybindings();
}
