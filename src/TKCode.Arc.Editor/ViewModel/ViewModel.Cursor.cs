// Ported from vscode (src/vs/editor/common/viewModel/viewModelImpl.ts: ViewModel, commit b57d5c9e), MIT license.
// This part: the cursor operations (#region cursor operations), the view layout operations (#region viewLayout) and
// the event batching.
// Differences from upstream:
//  - saveCursorState and restoreCursorState use the record SavedCursorState (upstream's ICursorState);
//  - executeEdits takes upstream's ICursorStateComputer as the delegate CursorStateComputer and the edits as
//    SingleEditOperation records;
//  - a view model without a batchable target runs the callbacks as they are (upstream's tests pass
//    { batchChanges: cb => cb() }).

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewLayout;

namespace TKCode.Arc.Editor.ViewModel;

internal partial class ViewModel
{
    // #region cursor operations

    public CursorState GetPrimaryCursorState() => _cursor.GetPrimaryCursorState();

    public int GetLastAddedCursorIndex() => _cursor.GetLastAddedCursorIndex();

    public IReadOnlyList<CursorState> GetCursorStates() => _cursor.GetCursorStates();

    public bool SetCursorStates(string? source, CursorChangeReason reason, IReadOnlyList<PartialCursorState>? states) =>
        WithViewEventsCollector(eventsCollector => _cursor.SetStates(eventsCollector, source, reason, states));

    public ColumnSelectData GetCursorColumnSelectData() => _cursor.GetCursorColumnSelectData();

    public IReadOnlyList<TextRange> GetCursorAutoClosedCharacters() => _cursor.GetAutoClosedCharacters();

    public void SetCursorColumnSelectData(ColumnSelectData columnSelectData) => _cursor.SetCursorColumnSelectData(columnSelectData);

    public EditOperationType GetPrevEditOperationType() => _cursor.GetPrevEditOperationType();

    public void SetPrevEditOperationType(EditOperationType type) => _cursor.SetPrevEditOperationType(type);

    public TextSelection GetSelection() => _cursor.GetSelection();

    public IReadOnlyList<TextSelection> GetSelections() => _cursor.GetSelections();

    public TextPosition GetPosition() => _cursor.GetPrimaryCursorState().ModelState.Position;

    public void SetSelections(string? source, IReadOnlyList<TextSelection> selections, CursorChangeReason reason = CursorChangeReason.NotSet) =>
        WithViewEventsCollector(eventsCollector =>
        {
            _cursor.SetSelections(eventsCollector, source, selections, reason);
            return true;
        });

    public IReadOnlyList<SavedCursorState> SaveCursorState() => _cursor.SaveState();

    public void RestoreCursorState(IReadOnlyList<SavedCursorState> states) =>
        WithViewEventsCollector(eventsCollector =>
        {
            _cursor.RestoreState(eventsCollector, states);
            return true;
        });

    private void ExecuteCursorEdit(Action<ViewModelEventsCollector> callback)
    {
        if (_cursor.Context.CursorConfig.ReadOnly)
        {
            // we cannot edit when read only...
            _eventDispatcher.EmitOutgoingEvent(new ReadOnlyEditAttemptEvent());
            return;
        }
        WithViewEventsCollector(eventsCollector =>
        {
            callback(eventsCollector);
            return true;
        });
    }

    public void ExecuteEdits(string? source, IReadOnlyList<SingleEditOperation> edits, CursorStateComputer cursorStateComputer, TextModelEditSource reason) =>
        ExecuteCursorEdit(eventsCollector => _cursor.ExecuteEdits(eventsCollector, source, edits, cursorStateComputer, reason));

    public void StartComposition() =>
        ExecuteCursorEdit(eventsCollector => _cursor.StartComposition(eventsCollector));

    public void EndComposition(string? source = null) =>
        ExecuteCursorEdit(eventsCollector => _cursor.EndComposition(eventsCollector, source));

    public void Type(string text, string? source = null) =>
        ExecuteCursorEdit(eventsCollector => _cursor.Type(eventsCollector, text, source));

    public void CompositionType(string text, int replacePrevCharCnt, int replaceNextCharCnt, int positionDelta, string? source = null) =>
        ExecuteCursorEdit(eventsCollector => _cursor.CompositionType(eventsCollector, text, replacePrevCharCnt, replaceNextCharCnt, positionDelta, source));

    public void Paste(string text, bool pasteOnNewLine, IReadOnlyList<string>? multicursorText = null, string? source = null, bool isBlock = false) =>
        ExecuteCursorEdit(eventsCollector => _cursor.Paste(eventsCollector, text, pasteOnNewLine, multicursorText, source, isBlock));

    public void Cut(string? source = null) =>
        ExecuteCursorEdit(eventsCollector => _cursor.Cut(eventsCollector, source));

    public void ExecuteCommand(ICommand command, string? source = null) =>
        ExecuteCursorEdit(eventsCollector => _cursor.ExecuteCommand(eventsCollector, command, source));

    public void ExecuteCommands(IReadOnlyList<ICommand?> commands, string? source = null) =>
        ExecuteCursorEdit(eventsCollector => _cursor.ExecuteCommands(eventsCollector, commands, source));

    public void RevealAllCursors(string? source, bool revealHorizontal, bool minimalReveal = false) =>
        WithViewEventsCollector(eventsCollector =>
        {
            _cursor.RevealAll(eventsCollector, source, minimalReveal, VerticalRevealType.Simple, revealHorizontal, ScrollType.Smooth);
            return true;
        });

    public void RevealPrimaryCursor(string? source, bool revealHorizontal, bool minimalReveal = false) =>
        WithViewEventsCollector(eventsCollector =>
        {
            _cursor.RevealPrimary(eventsCollector, source, minimalReveal, VerticalRevealType.Simple, revealHorizontal, ScrollType.Smooth);
            return true;
        });

    public void RevealTopMostCursor(string? source)
    {
        var viewPosition = _cursor.GetTopMostViewPosition();
        var viewRange = new TextRange(viewPosition.LineNumber, viewPosition.Column, viewPosition.LineNumber, viewPosition.Column);
        WithViewEventsCollector(eventsCollector =>
        {
            eventsCollector.EmitViewEvent(new ViewRevealRangeRequestEvent(source, false, viewRange, null, VerticalRevealType.Simple, true, ScrollType.Smooth));
            return true;
        });
    }

    public void RevealBottomMostCursor(string? source)
    {
        var viewPosition = _cursor.GetBottomMostViewPosition();
        var viewRange = new TextRange(viewPosition.LineNumber, viewPosition.Column, viewPosition.LineNumber, viewPosition.Column);
        WithViewEventsCollector(eventsCollector =>
        {
            eventsCollector.EmitViewEvent(new ViewRevealRangeRequestEvent(source, false, viewRange, null, VerticalRevealType.Simple, true, ScrollType.Smooth));
            return true;
        });
    }

    public void RevealRange(string? source, bool revealHorizontal, TextRange viewRange, VerticalRevealType verticalType, ScrollType scrollType) =>
        WithViewEventsCollector(eventsCollector =>
        {
            eventsCollector.EmitViewEvent(new ViewRevealRangeRequestEvent(source, false, viewRange, null, verticalType, revealHorizontal, scrollType));
            return true;
        });

    // #endregion

    // #region viewLayout

    public void ChangeWhitespace(Action<IWhitespaceChangeAccessor> callback)
    {
        var hadAChange = ViewLayout.ChangeWhitespace(callback);
        if (hadAChange)
        {
            _eventDispatcher.EmitSingleViewEvent(new ViewZonesChangedEvent());
            _eventDispatcher.EmitOutgoingEvent(new OutgoingViewZonesChangedEvent());
        }
    }

    // #endregion

    private T WithViewEventsCollector<T>(Func<ViewModelEventsCollector, T> callback)
    {
        if (_transactionalTarget is null)
        {
            return EmitViewEvent(callback);
        }
        return _transactionalTarget.BatchChanges(() => EmitViewEvent(callback));
    }

    private T EmitViewEvent<T>(Func<ViewModelEventsCollector, T> callback)
    {
        try
        {
            var eventsCollector = _eventDispatcher.BeginEmitViewEvents();
            return callback(eventsCollector);
        }
        finally
        {
            _eventDispatcher.EndEmitViewEvents();
        }
    }

    public void BatchEvents(Action callback) =>
        WithViewEventsCollector(_ =>
        {
            callback();
            return true;
        });

    public TextPosition NormalizePosition(TextPosition position, PositionAffinity affinity)
    {
        return _lines.NormalizePosition(position, affinity);
    }

    /// <summary>Gets the column at which indentation stops at a given line.</summary>
    public int GetLineIndentColumn(int lineNumber)
    {
        return _lines.GetLineIndentColumn(lineNumber);
    }
}
