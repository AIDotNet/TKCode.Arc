// Stand-in for the parts of vscode's cursors controller (src/vs/editor/common/cursor/cursor.ts: CursorsController,
// AutoClosedAction, CompositionState; commit b57d5c9e, MIT license) and view model (viewModelImpl.ts: type, paste,
// compositionType, startComposition, endComposition, executeEdits, executeCommand) that the typing tests go through,
// and of the editor commands they run (coreCommands.ts: Tab, Outdent, Undo, Redo; linesOperations.ts: IndentLines).
//
// Differences from upstream:
//  - there is one cursor list (model coordinates are view coordinates); the cursors are not normalized, so
//    overlapping cursors are not merged (tests that need merging are not ported);
//  - an exception in an edit propagates to the test (upstream reports it to onUnexpectedError);
//  - the cursor markers (tracked ranges) are updated whenever the cursors change outside an edit and after each
//    edit, instead of being stopped and restarted around it;
//  - the editor has focus unless a test says otherwise (HasFocus), as upstream's tests set it.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Tests;

internal sealed partial class OperationsTestEditor : IViewModelContentListener
{
    /// <summary>The source upstream's keyboard handler passes to type, compositionType and endComposition.</summary>
    public const string Keyboard = "keyboard";

    private readonly List<string?> _selectionTrackedRanges = new();
    private List<AutoClosedAction> _autoClosedActions = new();
    private CompositionState? _compositionState;
    private bool _isHandling;

    /// <summary>Insert or overtype (upstream's InputMode).</summary>
    public EditorInputMode InputMode { get; set; } = EditorInputMode.Insert;

    /// <summary>Upstream's viewModel.setHasFocus; the tests set it to true.</summary>
    public bool HasFocus { get; set; } = true;

    private void OnCreated()
    {
        Model.RegisterViewModel(this);
        TrackSelections();
    }

    private void OnDisposing()
    {
        Model.UnregisterViewModel(this);
        _autoClosedActions.Clear();
    }

    private void OnCursorsChanged()
    {
        if (_isHandling)
        {
            // CursorCollection.setSelections in _interpretCommandResult: no validation of the auto-closed actions.
            return;
        }
        TrackSelections();
        ValidateAutoClosedActions();
    }

    // ------ view model / controller entry points

    /// <summary>Upstream's viewModel.setSelections: validated cursor states.</summary>
    public void SetSelectionsValidated(params TextSelection[] selections) => SetStates(selections);

    /// <summary>Upstream's viewModel.type.</summary>
    public void Type(string text, string? source = null)
    {
        ExecuteEdit(() =>
        {
            if (source == Keyboard)
            {
                // If this event is coming straight from the keyboard, look for electric characters and enter

                var len = text.Length;
                var offset = 0;
                while (offset < len)
                {
                    var charLength = Strings.NextCharLength(text, offset);
                    var chr = text.Substring(offset, charLength);

                    // Here we must interpret each typed character individually
                    ExecuteEditOperation(TypeOperations.TypeWithInterceptors(
                        _compositionState is not null, PrevEditOperationType, Config, Model, GetSelections(), GetAutoClosedCharacters(), chr));

                    offset += charLength;
                }
            }
            else
            {
                ExecuteEditOperation(TypeOperations.TypeWithoutInterceptors(PrevEditOperationType, Config, Model, GetSelections(), text));
            }
        });
    }

    /// <summary>Upstream's viewModel.compositionType.</summary>
    public void CompositionType(string text, int replacePrevCharCnt, int replaceNextCharCnt, int positionDelta, string? source = null)
    {
        if (text.Length == 0 && replacePrevCharCnt == 0 && replaceNextCharCnt == 0)
        {
            // this edit is a no-op
            if (positionDelta != 0)
            {
                // but it still wants to move the cursor
                var newSelections = GetSelections().Select(selection =>
                {
                    var position = selection.Position;
                    return new TextSelection(position.LineNumber, position.Column + positionDelta, position.LineNumber, position.Column + positionDelta);
                }).ToArray();
                SetStates(newSelections);
            }
            return;
        }
        ExecuteEdit(() => ExecuteEditOperation(TypeOperations.CompositionType(
            PrevEditOperationType, Config, Model, GetSelections(), text, replacePrevCharCnt, replaceNextCharCnt, positionDelta)));
    }

    /// <summary>Upstream's viewModel.startComposition.</summary>
    public void StartComposition() => _compositionState = new CompositionState(Model, GetSelections());

    /// <summary>Upstream's viewModel.endComposition.</summary>
    public void EndComposition(string? source = null)
    {
        var compositionOutcome = _compositionState?.DeduceOutcome(Model, GetSelections());
        _compositionState = null;

        ExecuteEdit(() =>
        {
            if (source == Keyboard)
            {
                // composition finishes, let's check if we need to auto complete if necessary.
                ExecuteEditOperation(TypeOperations.CompositionEndWithInterceptors(
                    PrevEditOperationType, Config, Model, compositionOutcome, GetSelections(), GetAutoClosedCharacters()));
            }
        });
    }

    /// <summary>Upstream's viewModel.paste.</summary>
    public void Paste(string text, bool pasteOnNewLine, IReadOnlyList<string>? multicursorText = null, bool isBlock = false) =>
        ExecuteEdit(() => ExecuteEditOperation(TypeOperations.Paste(Config, Model, GetSelections(), text, pasteOnNewLine, multicursorText ?? [], isBlock)));

    /// <summary>Upstream's CoreEditingCommands.Tab.</summary>
    public void Tab()
    {
        Model.PushStackElement();
        ExecuteCommands(TypeOperations.Tab(Config, Model, GetSelections()));
        Model.PushStackElement();
    }

    /// <summary>Upstream's CoreEditingCommands.Outdent.</summary>
    public void Outdent()
    {
        Model.PushStackElement();
        ExecuteCommands(TypeOperations.Outdent(Config, Model, GetSelections()));
        Model.PushStackElement();
    }

    /// <summary>Upstream's IndentLinesAction (editor.action.indentLines).</summary>
    public void IndentLines()
    {
        Model.PushStackElement();
        ExecuteCommands(TypeOperations.Indent(Config, Model, GetSelections()));
        Model.PushStackElement();
    }

    /// <summary>Upstream's viewModel.executeCommand: only the primary cursor, one command.</summary>
    public void ExecuteCommand(ICommand command)
    {
        ExecuteEdit(() =>
        {
            // killSecondaryCursors
            if (Cursors.Count > 1)
            {
                Cursors = [Cursors[0]];
                TrackSelections();
            }
            ExecuteEditOperation(new EditOperationResult(EditOperationType.Other, [command], false, false));
        });
    }

    /// <summary>Upstream's editor.executeEdits (and viewModel.executeEdits, which tracks auto-closed pairs of suggestions).</summary>
    public void ExecuteEdits(string source, IReadOnlyList<SingleEditOperation> edits, IReadOnlyList<TextSelection>? endCursorState = null)
    {
        if (Options.ReadOnly)
        {
            return;
        }

        List<(int OpenCharIndex, int CloseCharIndex)>? autoClosingIndices = null;
        if (source is "snippet" or "suggest")
        {
            autoClosingIndices = FindAutoClosingPairs(edits);
        }

        var operations = edits.ToList();
        if (autoClosingIndices is not null)
        {
            operations[0] = operations[0] with { IsTracked = true };
        }
        var autoClosedCharactersRanges = new List<TextRange>();
        var autoClosedEnclosingRanges = new List<TextRange>();
        var selections = Model.PushEditOperations(GetSelections(), operations, undoEdits =>
        {
            if (autoClosingIndices is not null)
            {
                for (int i = 0, len = autoClosingIndices.Count; i < len; i++)
                {
                    var (openCharInnerIndex, closeCharInnerIndex) = autoClosingIndices[i];
                    var undoEdit = undoEdits[i];
                    var lineNumber = undoEdit.Range.StartLineNumber;
                    var openCharIndex = undoEdit.Range.StartColumn - 1 + openCharInnerIndex;
                    var closeCharIndex = undoEdit.Range.StartColumn - 1 + closeCharInnerIndex;

                    autoClosedCharactersRanges.Add(new TextRange(lineNumber, closeCharIndex + 1, lineNumber, closeCharIndex + 2));
                    autoClosedEnclosingRanges.Add(new TextRange(lineNumber, openCharIndex + 1, lineNumber, closeCharIndex + 2));
                }
            }
            var computed = endCursorState;
            if (computed is not null)
            {
                // Don't recover the selection from markers because
                // we know what it should be.
                _isHandling = true;
            }

            return computed;
        }, null, EditSources.Unknown(source));
        if (selections is not null)
        {
            _isHandling = false;
            SetStates(selections);
        }
        if (autoClosedCharactersRanges.Count > 0)
        {
            PushAutoClosedAction(autoClosedCharactersRanges, autoClosedEnclosingRanges);
        }
    }

    /// <summary>Upstream's CoreEditingCommands.Undo.</summary>
    public async Task Undo()
    {
        if (Options.ReadOnly)
        {
            return;
        }
        await Model.Undo();
    }

    /// <summary>Upstream's CoreEditingCommands.Redo.</summary>
    public async Task Redo()
    {
        if (Options.ReadOnly)
        {
            return;
        }
        await Model.Redo();
    }

    /// <summary>Upstream's cursor.getAutoClosedCharacters.</summary>
    public IReadOnlyList<TextRange> GetAutoClosedCharacters() => AutoClosedAction.GetAllAutoClosedCharacters(_autoClosedActions);

    // ------ IViewModel: the cursor's onModelContentChanged

    void IViewModelContentListener.OnDidChangeContentOrInjectedText(IModelContentOrInjectedTextChangedEvent e)
    {
    }

    void IViewModelContentListener.EmitContentChangeEvent(IModelContentOrInjectedTextChangedEvent e)
    {
        if (e is not InternalModelContentChangeEvent contentChange)
        {
            // If injected texts change, the view positions of all cursors need to be updated.
            if (_isHandling)
            {
                return;
            }
            _isHandling = true;
            try
            {
                SetStates(GetSelections());
            }
            finally
            {
                _isHandling = false;
            }
            TrackSelections();
            ValidateAutoClosedActions();
            return;
        }

        var raw = contentChange.RawContentChangedEvent;
        if (_isHandling)
        {
            return;
        }

        var hadFlushEvent = raw.ContainsEvent(RawContentChangedType.Flush);
        PrevEditOperationType = EditOperationType.Other;

        if (hadFlushEvent)
        {
            // a model.setValue() was called
            SetSelections([new TextSelection(1, 1, 1, 1)]);
        }
        else if (HasFocus && raw.ResultingSelection is { Count: > 0 } resultingSelection)
        {
            SetStates(resultingSelection);
        }
        else
        {
            SetStates(ReadSelectionFromMarkers());
        }
    }

    // ------ auxiliary handling logic

    /// <summary>Upstream's _executeEdit.</summary>
    private void ExecuteEdit(Action callback)
    {
        if (Options.ReadOnly)
        {
            // we cannot edit when read only...
            return;
        }

        _isHandling = true;
        try
        {
            EnsureValidState();
            callback();
        }
        finally
        {
            _isHandling = false;
        }

        TrackSelections();
        ValidateAutoClosedActions();
    }

    /// <summary>Upstream's _executeEditOperation.</summary>
    private void ExecuteEditOperation(EditOperationResult? opResult)
    {
        if (opResult is null)
        {
            // Nothing to execute
            return;
        }

        if (opResult.ShouldPushStackElementBefore)
        {
            Model.PushStackElement();
        }

        var result = CommandExecutor.ExecuteCommands(Model, GetSelections(), opResult.Commands);
        if (result is not null)
        {
            // The commands were applied correctly
            InterpretCommandResult(result);

            // Check for auto-closing closed characters
            var autoClosedCharactersRanges = new List<TextRange>();
            var autoClosedEnclosingRanges = new List<TextRange>();

            for (var i = 0; i < opResult.Commands.Count; i++)
            {
                if (opResult.Commands[i] is BaseTypeWithAutoClosingCommand { EnclosingRange: { } enclosingRange, CloseCharacterRange: { } closeCharacterRange })
                {
                    autoClosedCharactersRanges.Add(closeCharacterRange);
                    autoClosedEnclosingRanges.Add(enclosingRange);
                }
            }

            if (autoClosedCharactersRanges.Count > 0)
            {
                PushAutoClosedAction(autoClosedCharactersRanges, autoClosedEnclosingRanges);
            }

            PrevEditOperationType = opResult.Type;
        }

        if (opResult.ShouldPushStackElementAfter)
        {
            Model.PushStackElement();
        }

        if (result is not null && opResult.FollowUp is not null)
        {
            ExecuteEditOperation(opResult.FollowUp(GetSelections()));
        }
    }

    private void InterpretCommandResult(IReadOnlyList<TextSelection> cursorState)
    {
        if (cursorState.Count == 0)
        {
            cursorState = ReadSelectionFromMarkers();
        }

        SetSelections(ValidateSelections(cursorState));
    }

    /// <summary>Upstream's setStates: validated cursors, the auto-closed actions checked against them.</summary>
    private void SetStates(IReadOnlyList<TextSelection> selections)
    {
        var limited = selections.Count > Options.MultiCursorLimit ? selections.Take(Options.MultiCursorLimit).ToList() : selections;
        SetSelections(ValidateSelections(limited));
    }

    private void EnsureValidState() => Cursors = ValidateSelections(GetSelections()).Select(CreateState).ToList();

    private List<TextSelection> ValidateSelections(IReadOnlyList<TextSelection> selections) =>
        selections.Select(s =>
        {
            var start = Model.ValidatePosition(s.SelectionStart);
            var position = Model.ValidatePosition(s.Position);
            return new TextSelection(start, position);
        }).ToList();

    private void TrackSelections()
    {
        var selections = GetSelections();
        while (_selectionTrackedRanges.Count > selections.Count)
        {
            var last = _selectionTrackedRanges.Count - 1;
            Model.SetTrackedRange(_selectionTrackedRanges[last], null, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges);
            _selectionTrackedRanges.RemoveAt(last);
        }
        for (var i = 0; i < selections.Count; i++)
        {
            var old = i < _selectionTrackedRanges.Count ? _selectionTrackedRanges[i] : null;
            var id = Model.SetTrackedRange(old, selections[i].Range, TrackedRangeStickiness.AlwaysGrowsWhenTypingAtEdges);
            if (i < _selectionTrackedRanges.Count)
            {
                _selectionTrackedRanges[i] = id;
            }
            else
            {
                _selectionTrackedRanges.Add(id);
            }
        }
    }

    private List<TextSelection> ReadSelectionFromMarkers()
    {
        var result = new List<TextSelection>();
        for (var i = 0; i < Cursors.Count; i++)
        {
            var selection = Cursors[i].Selection;
            var id = i < _selectionTrackedRanges.Count ? _selectionTrackedRanges[i] : null;
            if (id is null || Model.GetTrackedRange(id) is not { } range)
            {
                result.Add(selection);
                continue;
            }

            if (selection.IsEmpty && !range.IsEmpty)
            {
                // Avoid selecting text when recovering from markers
                result.Add(TextSelection.FromRange(range.CollapseToEnd(), selection.Direction));
                continue;
            }

            result.Add(TextSelection.FromRange(range, selection.Direction));
        }
        return result;
    }

    private void ValidateAutoClosedActions()
    {
        if (_autoClosedActions.Count > 0)
        {
            var selections = GetSelections().Select(s => s.Range).ToList();
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

    private void PushAutoClosedAction(List<TextRange> autoClosedCharactersRanges, List<TextRange> autoClosedEnclosingRanges)
    {
        var autoClosedCharactersDeltaDecorations = new List<ModelDeltaDecoration>();
        var autoClosedEnclosingDeltaDecorations = new List<ModelDeltaDecoration>();

        for (int i = 0, len = autoClosedCharactersRanges.Count; i < len; i++)
        {
            autoClosedCharactersDeltaDecorations.Add(new ModelDeltaDecoration(
                autoClosedCharactersRanges[i],
                new ModelDecorationOptions("auto-closed-character")
                {
                    InlineClassName = "auto-closed-character",
                    Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
                }));
            autoClosedEnclosingDeltaDecorations.Add(new ModelDeltaDecoration(
                autoClosedEnclosingRanges[i],
                new ModelDecorationOptions("auto-closed-enclosing")
                {
                    Stickiness = TrackedRangeStickiness.NeverGrowsWhenTypingAtEdges,
                }));
        }

        var autoClosedCharactersDecorations = Model.DeltaDecorations([], autoClosedCharactersDeltaDecorations);
        var autoClosedEnclosingDecorations = Model.DeltaDecorations([], autoClosedEnclosingDeltaDecorations);
        _autoClosedActions.Add(new AutoClosedAction(Model, autoClosedCharactersDecorations, autoClosedEnclosingDecorations));
    }

    private List<(int OpenCharIndex, int CloseCharIndex)>? FindAutoClosingPairs(IReadOnlyList<SingleEditOperation> edits)
    {
        if (edits.Count == 0)
        {
            return null;
        }

        var indices = new List<(int, int)>();
        for (int i = 0, len = edits.Count; i < len; i++)
        {
            var edit = edits[i];
            if (string.IsNullOrEmpty(edit.Text) || edit.Text.Contains('\n', StringComparison.Ordinal))
            {
                return null;
            }

            // /([)\]}>'"`])([^)\]}>'"`]*)$/
            const string closers = ")]}>'\"`";
            var closeCharIndex = edit.Text.LastIndexOfAny(closers.ToCharArray());
            if (closeCharIndex < 0)
            {
                return null;
            }
            var closeChar = edit.Text[closeCharIndex].ToString();

            if (!Config.AutoClosingPairs.AutoClosingPairsCloseSingleChar.TryGetValue(closeChar, out var autoClosingPairsCandidates)
                || autoClosingPairsCandidates.Count != 1)
            {
                return null;
            }

            var openChar = autoClosingPairsCandidates[0].Open;
            // String.prototype.lastIndexOf with fromIndex closeCharIndex - 1 (clamped to 0)
            var openCharIndex = closeCharIndex - 1 < 0
                ? (edit.Text.StartsWith(openChar, StringComparison.Ordinal) ? 0 : -1)
                : edit.Text.LastIndexOf(openChar, closeCharIndex - 1, StringComparison.Ordinal);
            if (openCharIndex == -1)
            {
                return null;
            }

            indices.Add((openCharIndex, closeCharIndex));
        }

        return indices;
    }

    /// <summary>Closing characters the editor inserted, tracked while the cursors stay between them and their opening.</summary>
    private sealed class AutoClosedAction
    {
        private readonly TextModel _model;
        private string[] _autoClosedCharactersDecorations;
        private string[] _autoClosedEnclosingDecorations;

        public AutoClosedAction(TextModel model, string[] autoClosedCharactersDecorations, string[] autoClosedEnclosingDecorations)
        {
            _model = model;
            _autoClosedCharactersDecorations = autoClosedCharactersDecorations;
            _autoClosedEnclosingDecorations = autoClosedEnclosingDecorations;
        }

        public static List<TextRange> GetAllAutoClosedCharacters(IReadOnlyList<AutoClosedAction> autoClosedActions)
        {
            var autoClosedCharacters = new List<TextRange>();
            foreach (var autoClosedAction in autoClosedActions)
            {
                autoClosedCharacters.AddRange(autoClosedAction.GetAutoClosedCharactersRanges());
            }
            return autoClosedCharacters;
        }

        public void Dispose()
        {
            // As upstream (cursor.ts AutoClosedAction.dispose): through ChangeDecorations, which may be called from a
            // decorations listener; DeltaDecorations there reports a recursive call.
            _model.ChangeDecorations(accessor =>
            {
                _autoClosedCharactersDecorations = accessor.DeltaDecorations(_autoClosedCharactersDecorations, []);
                _autoClosedEnclosingDecorations = accessor.DeltaDecorations(_autoClosedEnclosingDecorations, []);
                return 0;
            });
        }

        public List<TextRange> GetAutoClosedCharactersRanges()
        {
            var result = new List<TextRange>();
            for (var i = 0; i < _autoClosedCharactersDecorations.Length; i++)
            {
                if (_model.GetDecorationRange(_autoClosedCharactersDecorations[i]) is { } decorationRange)
                {
                    result.Add(decorationRange);
                }
            }
            return result;
        }

        public bool IsValid(List<TextRange> selections)
        {
            var enclosingRanges = new List<TextRange>();
            for (var i = 0; i < _autoClosedEnclosingDecorations.Length; i++)
            {
                if (_model.GetDecorationRange(_autoClosedEnclosingDecorations[i]) is { } decorationRange)
                {
                    enclosingRanges.Add(decorationRange);
                    if (decorationRange.StartLineNumber != decorationRange.EndLineNumber)
                    {
                        // Stop tracking if the range becomes multiline...
                        return false;
                    }
                }
            }
            enclosingRanges = StableSort(enclosingRanges);

            var sortedSelections = StableSort(selections);

            for (var i = 0; i < sortedSelections.Count; i++)
            {
                if (i >= enclosingRanges.Count)
                {
                    return false;
                }
                if (!enclosingRanges[i].StrictContainsRange(sortedSelections[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<TextRange> StableSort(List<TextRange> ranges) =>
            ranges.Select((range, index) => (range, index))
                .OrderBy(x => x.range, Comparer<TextRange>.Create(TextRange.CompareRangesUsingStarts))
                .ThenBy(x => x.index)
                .Select(x => x.range)
                .ToList();
    }

    private sealed record CompositionLineState(string Text, int LineNumber, int StartSelectionOffset, int EndSelectionOffset);

    /// <summary>The lines of the cursors when a composition started, to deduce what it did when it ends.</summary>
    private sealed class CompositionState
    {
        private readonly List<CompositionLineState>? _original;

        public CompositionState(TextModel textModel, IReadOnlyList<TextSelection> selections)
        {
            _original = Capture(textModel, selections);
        }

        private static List<CompositionLineState>? Capture(TextModel textModel, IReadOnlyList<TextSelection> selections)
        {
            var result = new List<CompositionLineState>();
            foreach (var selection in selections)
            {
                if (selection.StartLineNumber != selection.EndLineNumber)
                {
                    return null;
                }
                var lineNumber = selection.StartLineNumber;
                result.Add(new CompositionLineState(
                    textModel.GetLineContent(lineNumber),
                    lineNumber,
                    selection.StartColumn - 1,
                    selection.EndColumn - 1));
            }
            return result;
        }

        /// <summary>
        /// Returns the inserted text during this composition.
        /// If the composition resulted in existing text being changed (i.e. not a pure insertion) it returns null.
        /// </summary>
        public List<CompositionOutcome>? DeduceOutcome(TextModel textModel, IReadOnlyList<TextSelection> selections)
        {
            if (_original is null)
            {
                return null;
            }
            var current = Capture(textModel, selections);
            if (current is null)
            {
                return null;
            }
            if (_original.Count != current.Count)
            {
                return null;
            }
            var result = new List<CompositionOutcome>();
            for (int i = 0, len = _original.Count; i < len; i++)
            {
                result.Add(DeduceOutcome(_original[i], current[i]));
            }
            return result;
        }

        private static CompositionOutcome DeduceOutcome(CompositionLineState original, CompositionLineState current)
        {
            var commonPrefix = Math.Min(
                original.StartSelectionOffset,
                Math.Min(current.StartSelectionOffset, CommonPrefixLength(original.Text, current.Text)));
            var commonSuffix = Math.Min(
                original.Text.Length - original.EndSelectionOffset,
                Math.Min(current.Text.Length - current.EndSelectionOffset, CommonSuffixLength(original.Text, current.Text)));
            var deletedText = original.Text.Substring(commonPrefix, original.Text.Length - commonSuffix - commonPrefix);
            var insertedTextStartOffset = commonPrefix;
            var insertedTextEndOffset = current.Text.Length - commonSuffix;
            var insertedText = current.Text.Substring(insertedTextStartOffset, insertedTextEndOffset - insertedTextStartOffset);
            var insertedTextRange = new TextRange(current.LineNumber, insertedTextStartOffset + 1, current.LineNumber, insertedTextEndOffset + 1);
            return new CompositionOutcome(
                deletedText,
                original.StartSelectionOffset - commonPrefix,
                original.EndSelectionOffset - commonPrefix,
                insertedText,
                current.StartSelectionOffset - commonPrefix,
                current.EndSelectionOffset - commonPrefix,
                insertedTextRange);
        }

        private static int CommonPrefixLength(string a, string b)
        {
            var len = Math.Min(a.Length, b.Length);
            for (var i = 0; i < len; i++)
            {
                if (a[i] != b[i])
                {
                    return i;
                }
            }
            return len;
        }

        private static int CommonSuffixLength(string a, string b)
        {
            var len = Math.Min(a.Length, b.Length);
            var aLastIndex = a.Length - 1;
            var bLastIndex = b.Length - 1;
            for (var i = 0; i < len; i++)
            {
                if (a[aLastIndex - i] != b[bLastIndex - i])
                {
                    return i;
                }
            }
            return len;
        }
    }
}
