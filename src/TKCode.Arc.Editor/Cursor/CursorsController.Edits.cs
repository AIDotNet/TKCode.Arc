// Ported from vscode (src/vs/editor/common/cursor/cursor.ts: CursorsController, commit b57d5c9e), MIT license.
// This part: the edits made at the cursors (executeEdits, type, compositionType, paste, cut, executeCommand(s), the
// composition) and the auto-closed character tracking.
// Differences from upstream:
//  - the edit operations are records: upstream's `edits[0]._isTracked = true` replaces the first one with a copy that
//    is tracked (the caller's list is not changed);
//  - the regular expression /([)\]}>'"`])([^)\]}>'"`]*)$/ of _findAutoClosingPairs is the last index of one of those
//    characters, and String.prototype.lastIndexOf(open, closeCharIndex - 1) is LastIndexOfFrom (a match may start at
//    the index and run past it, as in JavaScript, which .NET's LastIndexOf does not allow);
//  - an exception in an edit callback is reported to UnexpectedErrors, as upstream's onUnexpectedError.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc.Editor.Cursor;

internal sealed partial class CursorsController
{
    // ------ auxiliary handling logic

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

        var autoClosedCharactersDecorations = _model.DeltaDecorations([], autoClosedCharactersDeltaDecorations);
        var autoClosedEnclosingDecorations = _model.DeltaDecorations([], autoClosedEnclosingDeltaDecorations);
        _autoClosedActions.Add(new AutoClosedAction(_model, autoClosedCharactersDecorations, autoClosedEnclosingDecorations));
    }

    private void ExecuteEditOperation(EditOperationResult? opResult, TextModelEditSource editReason)
    {
        if (opResult is null)
        {
            // Nothing to execute
            return;
        }

        if (opResult.ShouldPushStackElementBefore)
        {
            _model.PushStackElement();
        }

        var result = CommandExecutor.ExecuteCommands(_model, _cursors.GetSelections(), opResult.Commands, editReason);
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

            _prevEditOperationType = opResult.Type;
        }

        if (opResult.ShouldPushStackElementAfter)
        {
            _model.PushStackElement();
        }

        if (result is not null && opResult.FollowUp is not null)
        {
            ExecuteEditOperation(opResult.FollowUp(GetSelections()), editReason);
        }
    }

    private void InterpretCommandResult(IReadOnlyList<TextSelection>? cursorState)
    {
        if (cursorState is null || cursorState.Count == 0)
        {
            cursorState = _cursors.ReadSelectionFromMarkers();
        }

        _columnSelectData = null;
        _cursors.SetSelections(cursorState);
        _cursors.Normalize();
    }

    // -----------------------------------------------------------------------------------------------------------
    // ----- handlers beyond this point

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
            var closeCharIndex = edit.Text.AsSpan().LastIndexOfAny(")]}>'\"`");
            if (closeCharIndex < 0)
            {
                return null;
            }
            var closeChar = edit.Text[closeCharIndex].ToString();

            if (!Context.CursorConfig.AutoClosingPairs.AutoClosingPairsCloseSingleChar.TryGetValue(closeChar, out var autoClosingPairsCandidates)
                || autoClosingPairsCandidates.Count != 1)
            {
                return null;
            }

            var openChar = autoClosingPairsCandidates[0].Open;
            var openCharIndex = LastIndexOfFrom(edit.Text, openChar, closeCharIndex - 1);
            if (openCharIndex == -1)
            {
                return null;
            }

            indices.Add((openCharIndex, closeCharIndex));
        }

        return indices;
    }

    /// <summary>JavaScript's <c>str.lastIndexOf(search, fromIndex)</c>: the last match that starts at or before fromIndex.</summary>
    private static int LastIndexOfFrom(string str, string search, int fromIndex)
    {
        var start = Math.Min(Math.Max(fromIndex, 0), str.Length - search.Length);
        for (var i = start; i >= 0; i--)
        {
            if (string.CompareOrdinal(str, i, search, 0, search.Length) == 0)
            {
                return i;
            }
        }
        return -1;
    }

    public void ExecuteEdits(
        ViewModelEventsCollector eventsCollector,
        string? source,
        IReadOnlyList<SingleEditOperation> edits,
        CursorStateComputer cursorStateComputer,
        TextModelEditSource reason)
    {
        List<(int OpenCharIndex, int CloseCharIndex)>? autoClosingIndices = null;
        if (source is "snippet" or "suggest")
        {
            autoClosingIndices = FindAutoClosingPairs(edits);
        }

        if (autoClosingIndices is not null)
        {
            var tracked = edits.ToList();
            tracked[0] = tracked[0] with { IsTracked = true };
            edits = tracked;
        }
        var autoClosedCharactersRanges = new List<TextRange>();
        var autoClosedEnclosingRanges = new List<TextRange>();
        var selections = _model.PushEditOperations(GetSelections(), edits, undoEdits =>
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
            var computed = cursorStateComputer(undoEdits);
            if (computed is not null)
            {
                // Don't recover the selection from markers because
                // we know what it should be.
                _isHandling = true;
            }

            return computed;
        }, null, reason);
        if (selections is not null)
        {
            _isHandling = false;
            SetSelections(eventsCollector, source, selections, CursorChangeReason.NotSet);
        }
        if (autoClosedCharactersRanges.Count > 0)
        {
            PushAutoClosedAction(autoClosedCharactersRanges, autoClosedEnclosingRanges);
        }
    }

    private void ExecuteEdit(Action callback, ViewModelEventsCollector eventsCollector, string? source, CursorChangeReason cursorChangeReason = CursorChangeReason.NotSet)
    {
        if (Context.CursorConfig.ReadOnly)
        {
            // we cannot edit when read only...
            return;
        }

        var oldState = CursorModelState.From(_model, this);
        _cursors.StopTrackingSelections();
        _isHandling = true;

        try
        {
            _cursors.EnsureValidState();
            callback();
        }
        catch (Exception err)
        {
            UnexpectedErrors.Report(err);
        }

        _isHandling = false;
        _cursors.StartTrackingSelections();
        ValidateAutoClosedActions();
        if (EmitStateChangedIfNecessary(eventsCollector, source, cursorChangeReason, oldState, false))
        {
            RevealAll(eventsCollector, source, false, VerticalRevealType.Simple, true, ScrollType.Smooth);
        }
    }

    public List<TextRange> GetAutoClosedCharacters() => AutoClosedAction.GetAllAutoClosedCharacters(_autoClosedActions);

    public void StartComposition(ViewModelEventsCollector eventsCollector)
    {
        _compositionState = new CompositionState(_model, GetSelections());
    }

    public void EndComposition(ViewModelEventsCollector eventsCollector, string? source = null)
    {
        var reason = EditSources.Cursor("compositionEnd", source);

        var compositionOutcome = _compositionState?.DeduceOutcome(_model, GetSelections());
        _compositionState = null;

        ExecuteEdit(() =>
        {
            if (source == "keyboard")
            {
                // composition finishes, let's check if we need to auto complete if necessary.
                ExecuteEditOperation(TypeOperations.CompositionEndWithInterceptors(
                    _prevEditOperationType, Context.CursorConfig, _model, compositionOutcome, GetSelections(), GetAutoClosedCharacters()), reason);
            }
        }, eventsCollector, source);
    }

    public void Type(ViewModelEventsCollector eventsCollector, string text, string? source = null)
    {
        var reason = EditSources.Cursor("type", source);

        ExecuteEdit(() =>
        {
            if (source == "keyboard")
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
                        _compositionState is not null, _prevEditOperationType, Context.CursorConfig, _model, GetSelections(), GetAutoClosedCharacters(), chr), reason);

                    offset += charLength;
                }
            }
            else
            {
                ExecuteEditOperation(TypeOperations.TypeWithoutInterceptors(_prevEditOperationType, Context.CursorConfig, _model, GetSelections(), text), reason);
            }
        }, eventsCollector, source);
    }

    public void CompositionType(
        ViewModelEventsCollector eventsCollector,
        string text,
        int replacePrevCharCnt,
        int replaceNextCharCnt,
        int positionDelta,
        string? source = null)
    {
        var reason = EditSources.Cursor("compositionType", source);

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
                }).ToList();
                SetSelections(eventsCollector, source, newSelections, CursorChangeReason.NotSet);
            }
            return;
        }
        ExecuteEdit(() =>
        {
            ExecuteEditOperation(TypeOperations.CompositionType(
                _prevEditOperationType, Context.CursorConfig, _model, GetSelections(), text, replacePrevCharCnt, replaceNextCharCnt, positionDelta), reason);
        }, eventsCollector, source);
    }

    public void Paste(
        ViewModelEventsCollector eventsCollector,
        string text,
        bool pasteOnNewLine,
        IReadOnlyList<string>? multicursorText = null,
        string? source = null,
        bool isBlock = false)
    {
        var reason = EditSources.Cursor("paste", source);

        ExecuteEdit(() =>
        {
            ExecuteEditOperation(TypeOperations.Paste(Context.CursorConfig, _model, GetSelections(), text, pasteOnNewLine, multicursorText ?? [], isBlock), reason);
        }, eventsCollector, source, CursorChangeReason.Paste);
    }

    public void Cut(ViewModelEventsCollector eventsCollector, string? source = null)
    {
        var reason = EditSources.Cursor("cut", source);
        ExecuteEdit(() =>
        {
            ExecuteEditOperation(DeleteOperations.Cut(Context.CursorConfig, _model, GetSelections()), reason);
        }, eventsCollector, source);
    }

    public void ExecuteCommand(ViewModelEventsCollector eventsCollector, ICommand command, string? source = null)
    {
        var reason = EditSources.Cursor("executeCommand", source);

        ExecuteEdit(() =>
        {
            _cursors.KillSecondaryCursors();

            ExecuteEditOperation(new EditOperationResult(EditOperationType.Other, [command], false, false), reason);
        }, eventsCollector, source);
    }

    public void ExecuteCommands(ViewModelEventsCollector eventsCollector, IReadOnlyList<ICommand?> commands, string? source = null)
    {
        var reason = EditSources.Cursor("executeCommands", source);

        ExecuteEdit(() =>
        {
            ExecuteEditOperation(new EditOperationResult(EditOperationType.Other, commands, false, false), reason);
        }, eventsCollector, source);
    }
}
