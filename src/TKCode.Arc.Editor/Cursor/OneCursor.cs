// Ported from vscode (src/vs/editor/common/cursor/oneCursor.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - the class is OneCursor (after the file), not Cursor: a type named Cursor would be hidden by the namespace
//    TKCode.Arc.Editor.Cursor everywhere outside it (and would clash with Avalonia's Cursor in the controls);
//  - modelState and viewState are properties with a private setter (upstream's public fields);
//  - the model's _setTrackedRange and _getTrackedRange are SetTrackedRange and GetTrackedRange.

using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Cursor;

/// <summary>Represents a single cursor.</summary>
internal sealed class OneCursor
{
    private string? _selTrackedRange;
    private bool _trackSelection;

    public OneCursor(CursorContext context)
    {
        _selTrackedRange = null;
        _trackSelection = true;

        ModelState = null!;
        ViewState = null!;
        SetStateCore(
            context,
            new SingleCursorState(new TextRange(1, 1, 1, 1), SelectionStartKind.Simple, 0, new TextPosition(1, 1), 0),
            new SingleCursorState(new TextRange(1, 1, 1, 1), SelectionStartKind.Simple, 0, new TextPosition(1, 1), 0));
    }

    public SingleCursorState ModelState { get; private set; }

    public SingleCursorState ViewState { get; private set; }

    public void Dispose(CursorContext context) => RemoveTrackedRange(context);

    public void StartTrackingSelection(CursorContext context)
    {
        _trackSelection = true;
        UpdateTrackedRange(context);
    }

    public void StopTrackingSelection(CursorContext context)
    {
        _trackSelection = false;
        RemoveTrackedRange(context);
    }

    private void UpdateTrackedRange(CursorContext context)
    {
        if (!_trackSelection)
        {
            // don't track the selection
            return;
        }
        _selTrackedRange = context.Model.SetTrackedRange(_selTrackedRange, ModelState.Selection, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges);
    }

    private void RemoveTrackedRange(CursorContext context) =>
        _selTrackedRange = context.Model.SetTrackedRange(_selTrackedRange, null, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges);

    public CursorState AsCursorState() => new(ModelState, ViewState);

    public TextSelection ReadSelectionFromMarkers(CursorContext context)
    {
        var range = context.Model.GetTrackedRange(_selTrackedRange!)!.Value;

        if (ModelState.Selection.IsEmpty && !range.IsEmpty)
        {
            // Avoid selecting text when recovering from markers
            return TextSelection.FromRange(range.CollapseToEnd(), ModelState.Selection.Direction);
        }

        return TextSelection.FromRange(range, ModelState.Selection.Direction);
    }

    public void EnsureValidState(CursorContext context) => SetStateCore(context, ModelState, ViewState);

    public void SetState(CursorContext context, SingleCursorState? modelState, SingleCursorState? viewState) =>
        SetStateCore(context, modelState, viewState);

    private static TextPosition ValidatePositionWithCache(ICursorSimpleModel viewModel, TextPosition position, TextPosition cacheInput, TextPosition cacheOutput)
    {
        if (position == cacheInput)
        {
            return cacheOutput;
        }
        return viewModel.NormalizePosition(position, PositionAffinity.None);
    }

    private static SingleCursorState ValidateViewState(ICursorSimpleModel viewModel, SingleCursorState viewState)
    {
        var position = viewState.Position;
        var sStartPosition = viewState.SelectionStart.Start;
        var sEndPosition = viewState.SelectionStart.End;

        var validPosition = viewModel.NormalizePosition(position, PositionAffinity.None);
        var validSStartPosition = ValidatePositionWithCache(viewModel, sStartPosition, position, validPosition);
        var validSEndPosition = ValidatePositionWithCache(viewModel, sEndPosition, sStartPosition, validSStartPosition);

        if (position == validPosition && sStartPosition == validSStartPosition && sEndPosition == validSEndPosition)
        {
            // fast path: the state is valid
            return viewState;
        }

        return new SingleCursorState(
            new TextRange(validSStartPosition, validSEndPosition),
            viewState.SelectionStartKind,
            viewState.SelectionStartLeftoverVisibleColumns + sStartPosition.Column - validSStartPosition.Column,
            validPosition,
            viewState.LeftoverVisibleColumns + position.Column - validPosition.Column);
    }

    private void SetStateCore(CursorContext context, SingleCursorState? modelState, SingleCursorState? viewState)
    {
        if (viewState is not null)
        {
            viewState = ValidateViewState(context.ViewModel, viewState);
        }

        if (modelState is null)
        {
            if (viewState is null)
            {
                return;
            }
            // We only have the view state => compute the model state
            var selectionStart = context.Model.ValidateRange(
                context.CoordinatesConverter.ConvertViewRangeToModelRange(viewState.SelectionStart));

            var position = context.Model.ValidatePosition(
                context.CoordinatesConverter.ConvertViewPositionToModelPosition(viewState.Position));

            modelState = new SingleCursorState(selectionStart, viewState.SelectionStartKind, viewState.SelectionStartLeftoverVisibleColumns, position, viewState.LeftoverVisibleColumns);
        }
        else
        {
            // Validate new model state
            var selectionStart = context.Model.ValidateRange(modelState.SelectionStart);
            var selectionStartLeftoverVisibleColumns = modelState.SelectionStart == selectionStart ? modelState.SelectionStartLeftoverVisibleColumns : 0;

            var position = context.Model.ValidatePosition(modelState.Position);
            var leftoverVisibleColumns = modelState.Position == position ? modelState.LeftoverVisibleColumns : 0;

            modelState = new SingleCursorState(selectionStart, modelState.SelectionStartKind, selectionStartLeftoverVisibleColumns, position, leftoverVisibleColumns);
        }

        if (viewState is null)
        {
            // We only have the model state => compute the view state
            var viewSelectionStart1 = context.CoordinatesConverter.ConvertModelPositionToViewPosition(new TextPosition(modelState.SelectionStart.StartLineNumber, modelState.SelectionStart.StartColumn));
            var viewSelectionStart2 = context.CoordinatesConverter.ConvertModelPositionToViewPosition(new TextPosition(modelState.SelectionStart.EndLineNumber, modelState.SelectionStart.EndColumn));
            var viewSelectionStart = new TextRange(viewSelectionStart1.LineNumber, viewSelectionStart1.Column, viewSelectionStart2.LineNumber, viewSelectionStart2.Column);
            var viewPosition = context.CoordinatesConverter.ConvertModelPositionToViewPosition(modelState.Position);
            viewState = new SingleCursorState(viewSelectionStart, modelState.SelectionStartKind, modelState.SelectionStartLeftoverVisibleColumns, viewPosition, modelState.LeftoverVisibleColumns);
        }
        else
        {
            // Validate new view state
            var viewSelectionStart = context.CoordinatesConverter.ValidateViewRange(viewState.SelectionStart, modelState.SelectionStart);
            var viewPosition = context.CoordinatesConverter.ValidateViewPosition(viewState.Position, modelState.Position);
            viewState = new SingleCursorState(viewSelectionStart, modelState.SelectionStartKind, modelState.SelectionStartLeftoverVisibleColumns, viewPosition, modelState.LeftoverVisibleColumns);
        }

        ModelState = modelState;
        ViewState = viewState;

        UpdateTrackedRange(context);
    }
}
