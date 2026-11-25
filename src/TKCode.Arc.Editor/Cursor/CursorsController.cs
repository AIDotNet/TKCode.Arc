// Ported from vscode (src/vs/editor/common/cursor/cursor.ts: CursorsController, commit b57d5c9e), MIT license.
// This part: state, events, reveal, save and restore, and the reaction to model changes. The edits (type, paste,
// composition, cut, commands, executeEdits) are in CursorsController.Edits.cs; CursorModelState, AutoClosedAction and
// the composition state in CursorsControllerTypes.cs; CommandExecutor (also in upstream's cursor.ts) in
// CommandExecutor.cs.
// Differences from upstream:
//  - the model is the TextModel (upstream's ITextModel) and the view model an ICursorSimpleModel, as upstream;
//  - upstream extends Disposable; this implements IDisposable;
//  - upstream's editorCommon.ICursorState (saveState and restoreState) is the record SavedCursorState, whose positions
//    are never missing: a line number or column of 0 counts as missing (upstream's falsy check);
//  - `source || 'keyboard'` treats an empty source like a null one, as upstream.

using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Cursor;

/// <summary>
/// The cursors of a view model: keeps them valid across model and line mapping changes, runs the edits made at them
/// and reports their changes as view and outgoing events.
/// </summary>
internal sealed partial class CursorsController : IDisposable
{
    private readonly TextModel _model;
    private int _knownModelVersionId;
    private readonly ICursorSimpleModel _viewModel;
    private readonly ICoordinatesConverter _coordinatesConverter;
    private CursorCollection _cursors;

    private bool _hasFocus;
    private bool _isHandling;
    private CompositionState? _compositionState;
    private ColumnSelectData? _columnSelectData;
    private List<AutoClosedAction> _autoClosedActions;
    private EditOperationType _prevEditOperationType;

    public CursorsController(TextModel model, ICursorSimpleModel viewModel, ICoordinatesConverter coordinatesConverter, CursorConfiguration cursorConfig)
    {
        _model = model;
        _knownModelVersionId = _model.GetVersionId();
        _viewModel = viewModel;
        _coordinatesConverter = coordinatesConverter;
        Context = new CursorContext(_model, _viewModel, _coordinatesConverter, cursorConfig);
        _cursors = new CursorCollection(Context);

        _hasFocus = false;
        _isHandling = false;
        _compositionState = null;
        _columnSelectData = null;
        _autoClosedActions = new List<AutoClosedAction>();
        _prevEditOperationType = EditOperationType.Other;
    }

    public CursorContext Context { get; private set; }

    public void Dispose()
    {
        _cursors.Dispose();
        var autoClosedActions = _autoClosedActions;
        _autoClosedActions = new List<AutoClosedAction>();
        foreach (var autoClosedAction in autoClosedActions)
        {
            autoClosedAction.Dispose();
        }
    }

    public void UpdateConfiguration(CursorConfiguration cursorConfig)
    {
        Context = new CursorContext(_model, _viewModel, _coordinatesConverter, cursorConfig);
        _cursors.UpdateContext(Context);
    }

    public void OnLineMappingChanged(ViewModelEventsCollector eventsCollector)
    {
        if (_knownModelVersionId != _model.GetVersionId())
        {
            // There are model change events that I didn't yet receive.
            //
            // This can happen when editing the model, and the view model receives the change events first,
            // and the view model emits line mapping changed events, all before the cursor gets a chance to
            // recover from markers.
            //
            // The model change listener above will be called soon and we'll ensure a valid cursor state there.
            return;
        }
        // Ensure valid state
        SetStates(eventsCollector, "viewModel", CursorChangeReason.NotSet, ToPartial(GetCursorStates()));
    }

    public void SetHasFocus(bool hasFocus)
    {
        _hasFocus = hasFocus;
    }

    private void ValidateAutoClosedActions()
    {
        if (_autoClosedActions.Count > 0)
        {
            var selections = _cursors.GetSelections().Select(s => s.Range).ToList();
            var validActions = new List<AutoClosedAction>();
            var invalidActions = new List<AutoClosedAction>();
            foreach (var autoClosedAction in _autoClosedActions)
            {
                if (autoClosedAction.IsValid(selections))
                {
                    validActions.Add(autoClosedAction);
                }
                else
                {
                    invalidActions.Add(autoClosedAction);
                }
            }
            // Disposal fires decoration events whose listeners may re-enter the cursor.
            _autoClosedActions = validActions;
            foreach (var invalidAction in invalidActions)
            {
                invalidAction.Dispose();
            }
        }
    }

    // ------ some getters/setters

    public CursorState GetPrimaryCursorState() => _cursors.GetPrimaryCursor();

    public int GetLastAddedCursorIndex() => _cursors.GetLastAddedCursorIndex();

    public List<CursorState> GetCursorStates() => _cursors.GetAll();

    public bool SetStates(ViewModelEventsCollector eventsCollector, string? source, CursorChangeReason reason, IReadOnlyList<PartialCursorState>? states)
    {
        var reachedMaxCursorCount = false;
        var multiCursorLimit = Context.CursorConfig.MultiCursorLimit;
        if (states is not null && states.Count > multiCursorLimit)
        {
            states = states.Take(multiCursorLimit).ToList();
            reachedMaxCursorCount = true;
        }

        var oldState = CursorModelState.From(_model, this);

        _cursors.SetStates(states);
        _cursors.Normalize();
        _columnSelectData = null;

        ValidateAutoClosedActions();

        return EmitStateChangedIfNecessary(eventsCollector, source, reason, oldState, reachedMaxCursorCount);
    }

    public void SetCursorColumnSelectData(ColumnSelectData columnSelectData)
    {
        _columnSelectData = columnSelectData;
    }

    public void RevealAll(
        ViewModelEventsCollector eventsCollector,
        string? source,
        bool minimalReveal,
        VerticalRevealType verticalType,
        bool revealHorizontal,
        ScrollType scrollType)
    {
        var viewPositions = _cursors.GetViewPositions();

        TextRange? revealViewRange = null;
        IReadOnlyList<TextSelection>? revealViewSelections = null;
        if (viewPositions.Count > 1)
        {
            revealViewSelections = _cursors.GetViewSelections();
        }
        else
        {
            revealViewRange = new TextRange(viewPositions[0], viewPositions[0]);
        }

        eventsCollector.EmitViewEvent(new ViewRevealRangeRequestEvent(source, minimalReveal, revealViewRange, revealViewSelections, verticalType, revealHorizontal, scrollType));
    }

    public void RevealPrimary(
        ViewModelEventsCollector eventsCollector,
        string? source,
        bool minimalReveal,
        VerticalRevealType verticalType,
        bool revealHorizontal,
        ScrollType scrollType)
    {
        var primaryCursor = _cursors.GetPrimaryCursor();
        TextSelection[] revealViewSelections = [primaryCursor.ViewState.Selection];
        eventsCollector.EmitViewEvent(new ViewRevealRangeRequestEvent(source, minimalReveal, null, revealViewSelections, verticalType, revealHorizontal, scrollType));
    }

    public List<SavedCursorState> SaveState()
    {
        var result = new List<SavedCursorState>();

        var selections = _cursors.GetSelections();
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var selection = selections[i];

            result.Add(new SavedCursorState(
                !selection.IsEmpty,
                new TextPosition(selection.SelectionStartLineNumber, selection.SelectionStartColumn),
                new TextPosition(selection.PositionLineNumber, selection.PositionColumn)));
        }

        return result;
    }

    public void RestoreState(ViewModelEventsCollector eventsCollector, IReadOnlyList<SavedCursorState> states)
    {
        var desiredSelections = new List<TextSelection>();

        for (int i = 0, len = states.Count; i < len; i++)
        {
            var state = states[i];

            var positionLineNumber = 1;
            var positionColumn = 1;

            // Avoid missing properties on the literal
            if (state.Position.LineNumber != 0)
            {
                positionLineNumber = state.Position.LineNumber;
            }
            if (state.Position.Column != 0)
            {
                positionColumn = state.Position.Column;
            }

            var selectionStartLineNumber = positionLineNumber;
            var selectionStartColumn = positionColumn;

            // Avoid missing properties on the literal
            if (state.SelectionStart.LineNumber != 0)
            {
                selectionStartLineNumber = state.SelectionStart.LineNumber;
            }
            if (state.SelectionStart.Column != 0)
            {
                selectionStartColumn = state.SelectionStart.Column;
            }

            desiredSelections.Add(new TextSelection(selectionStartLineNumber, selectionStartColumn, positionLineNumber, positionColumn));
        }

        SetStates(eventsCollector, "restoreState", CursorChangeReason.NotSet, CursorState.FromModelSelections(desiredSelections));
        RevealAll(eventsCollector, "restoreState", false, VerticalRevealType.Simple, true, ScrollType.Immediate);
    }

    public void OnModelContentChanged(ViewModelEventsCollector eventsCollector, IModelContentOrInjectedTextChangedEvent modelEvent)
    {
        if (modelEvent is not InternalModelContentChangeEvent contentChange)
        {
            // If injected texts change, the view positions of all cursors need to be updated.
            if (_isHandling)
            {
                // The view positions will be updated when handling finishes
                return;
            }
            // setStates might remove markers, which could trigger a decoration change.
            // If there are injected text decorations for that line, `onModelContentChanged` is emitted again
            // and an endless recursion happens.
            // _isHandling prevents that.
            _isHandling = true;
            try
            {
                SetStates(eventsCollector, "modelChange", CursorChangeReason.NotSet, ToPartial(GetCursorStates()));
            }
            finally
            {
                _isHandling = false;
            }
        }
        else
        {
            var e = contentChange.RawContentChangedEvent;
            _knownModelVersionId = e.VersionId;
            if (_isHandling)
            {
                return;
            }

            var hadFlushEvent = e.ContainsEvent(RawContentChangedType.Flush);
            _prevEditOperationType = EditOperationType.Other;

            if (hadFlushEvent)
            {
                // a model.setValue() was called
                _cursors.Dispose();
                _cursors = new CursorCollection(Context);
                ValidateAutoClosedActions();
                EmitStateChangedIfNecessary(eventsCollector, "model", CursorChangeReason.ContentFlush, null, false);
            }
            else
            {
                if (_hasFocus && e.ResultingSelection is { Count: > 0 } resultingSelection)
                {
                    var cursorState = CursorState.FromModelSelections(resultingSelection);
                    var reason = e.IsUndoing ? CursorChangeReason.Undo : e.IsRedoing ? CursorChangeReason.Redo : CursorChangeReason.RecoverFromMarkers;
                    if (SetStates(eventsCollector, "modelChange", reason, cursorState))
                    {
                        RevealAll(eventsCollector, "modelChange", false, VerticalRevealType.Simple, true, ScrollType.Smooth);
                    }
                }
                else
                {
                    var selectionsFromMarkers = _cursors.ReadSelectionFromMarkers();
                    SetStates(eventsCollector, "modelChange", CursorChangeReason.RecoverFromMarkers, CursorState.FromModelSelections(selectionsFromMarkers));
                }
            }
        }
    }

    public TextSelection GetSelection() => _cursors.GetPrimaryCursor().ModelState.Selection;

    public TextPosition GetTopMostViewPosition() => _cursors.GetTopMostViewPosition();

    public TextPosition GetBottomMostViewPosition() => _cursors.GetBottomMostViewPosition();

    public ColumnSelectData GetCursorColumnSelectData()
    {
        if (_columnSelectData is { } columnSelectData)
        {
            return columnSelectData;
        }
        var primaryCursor = _cursors.GetPrimaryCursor();
        var viewSelectionStart = primaryCursor.ViewState.SelectionStart.Start;
        var viewPosition = primaryCursor.ViewState.Position;
        return new ColumnSelectData(
            IsReal: false,
            FromViewLineNumber: viewSelectionStart.LineNumber,
            FromViewVisualColumn: Context.CursorConfig.VisibleColumnFromColumn(_viewModel, viewSelectionStart),
            ToViewLineNumber: viewPosition.LineNumber,
            ToViewVisualColumn: Context.CursorConfig.VisibleColumnFromColumn(_viewModel, viewPosition));
    }

    public List<TextSelection> GetSelections() => _cursors.GetSelections();

    public TextPosition GetPosition() => _cursors.GetPrimaryCursor().ModelState.Position;

    public void SetSelections(ViewModelEventsCollector eventsCollector, string? source, IReadOnlyList<TextSelection> selections, CursorChangeReason reason)
    {
        SetStates(eventsCollector, source, reason, CursorState.FromModelSelections(selections));
    }

    public EditOperationType GetPrevEditOperationType() => _prevEditOperationType;

    public void SetPrevEditOperationType(EditOperationType type)
    {
        _prevEditOperationType = type;
    }

    // -----------------------------------------------------------------------------------------------------------
    // ----- emitting events

    private bool EmitStateChangedIfNecessary(
        ViewModelEventsCollector eventsCollector,
        string? source,
        CursorChangeReason reason,
        CursorModelState? oldState,
        bool reachedMaxCursorCount)
    {
        var newState = CursorModelState.From(_model, this);
        if (newState.Equals(oldState))
        {
            return false;
        }

        var selections = _cursors.GetSelections();
        var viewSelections = _cursors.GetViewSelections();

        // Let the view get the event first.
        eventsCollector.EmitViewEvent(new ViewCursorStateChangedEvent(viewSelections, selections, reason));

        // Only after the view has been notified, let the rest of the world know...
        if (oldState is null
            || oldState.CursorState.Count != newState.CursorState.Count
            || newState.CursorState.Where((newCursorState, i) => !newCursorState.ModelState.Equals(oldState.CursorState[i].ModelState)).Any())
        {
            var oldSelections = oldState?.CursorState.Select(s => s.ModelState.Selection).ToList();
            var oldModelVersionId = oldState?.ModelVersionId ?? 0;
            eventsCollector.EmitOutgoingEvent(new CursorStateChangedEvent(
                oldSelections,
                selections,
                oldModelVersionId,
                newState.ModelVersionId,
                string.IsNullOrEmpty(source) ? "keyboard" : source,
                reason,
                reachedMaxCursorCount));
        }

        return true;
    }

    private static List<PartialCursorState> ToPartial(IReadOnlyList<CursorState> states) =>
        states.Select(PartialCursorState.From).ToList();
}
