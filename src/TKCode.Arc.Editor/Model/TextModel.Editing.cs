// Ported from vscode (src/vs/editor/common/model/textModel.ts, commit b57d5c9e), MIT license.
// This part: replacing the value, the end of line sequence, edits, undo and redo.

using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc.Editor.Model;

internal sealed partial class TextModel
{
    private readonly EditStack _commandManager;
    private bool _isUndoing;
    private bool _isRedoing;
    private List<int>? _trimAutoWhitespaceLines;

    /// <summary>Replaces the whole text. Decorations and the undo stack are cleared.</summary>
    public void SetValue(string value, TextModelEditSource? reason = null)
    {
        AssertNotDisposed();
        ArgumentNullException.ThrowIfNull(value);
        SetValueFromTextBuffer(new PieceTreeTextBuffer(CreateTextBufferFactory(value).Create(_options.DefaultEOL)), reason);
    }

    /// <summary>Replaces the whole text with what a snapshot reads. Decorations and the undo stack are cleared.</summary>
    public void SetValue(ITextSnapshot value, TextModelEditSource? reason = null)
    {
        AssertNotDisposed();
        ArgumentNullException.ThrowIfNull(value);
        SetValueFromTextBuffer(
            new PieceTreeTextBuffer(CreateTextBufferFactoryFromSnapshot(value).Create(_options.DefaultEOL)), reason);
    }

    private ModelContentChangedEvent CreateContentChanged2(
        TextRange range,
        int rangeOffset,
        int rangeLength,
        string text,
        bool isUndoing,
        bool isRedoing,
        bool isFlush,
        bool isEolChange,
        TextModelEditSource reason) =>
        new(
            [new ModelContentChange(range, rangeOffset, rangeLength, text)],
            _buffer.GetEOL(),
            GetVersionId(),
            isUndoing,
            isRedoing,
            isFlush,
            isEolChange,
            [reason],
            [1]);

    private void SetValueFromTextBuffer(PieceTreeTextBuffer textBuffer, TextModelEditSource? reason)
    {
        AssertNotDisposed();
        var oldFullModelRange = GetFullModelRange();
        var oldModelValueLength = GetValueLengthInRange(oldFullModelRange);
        var endLineNumber = GetLineCount();
        var endColumn = GetLineMaxColumn(endLineNumber);

        _buffer = textBuffer;
        IncreaseVersionId();

        // Destroy all my decorations
        _decorations = new Dictionary<string, IntervalNode>();
        _decorationsTree = new DecorationsTrees();

        // Destroy my edit history and settings
        _commandManager.Clear();
        _trimAutoWhitespaceLines = null;

        EmitContentChangedEvent(
            new ModelRawContentChangedEvent([new ModelRawFlush()], _versionId, false, false),
            CreateContentChanged2(
                new TextRange(1, 1, endLineNumber, endColumn),
                0,
                oldModelValueLength,
                GetValue(),
                false,
                false,
                true,
                false,
                reason ?? EditSources.SetValue()));
    }

    /// <summary>Changes the end of line sequence of the whole text, without an undo step (see <see cref="PushEOL"/>).</summary>
    public void SetEOL(EndOfLineSequence eol)
    {
        AssertNotDisposed();
        var newEOL = eol == EndOfLineSequence.CRLF ? "\r\n" : "\n";
        if (_buffer.GetEOL() == newEOL)
        {
            // Nothing to do
            return;
        }

        var oldFullModelRange = GetFullModelRange();
        var oldModelValueLength = GetValueLengthInRange(oldFullModelRange);
        var endLineNumber = GetLineCount();
        var endColumn = GetLineMaxColumn(endLineNumber);

        OnBeforeEOLChange();
        _buffer.SetEOL(newEOL);
        IncreaseVersionId();
        OnAfterEOLChange();

        EmitContentChangedEvent(
            new ModelRawContentChangedEvent([new ModelRawEOLChanged()], _versionId, false, false),
            CreateContentChanged2(
                new TextRange(1, 1, endLineNumber, endColumn),
                0,
                oldModelValueLength,
                GetValue(),
                false,
                false,
                false,
                true,
                EditSources.EolChange()));
    }

    private void OnBeforeEOLChange()
    {
        // Ensure all decorations get their `range` set.
        _decorationsTree.EnsureAllNodesHaveRanges(this);
    }

    private void OnAfterEOLChange()
    {
        // Transform back `range` to offsets
        var versionId = GetVersionId();
        var allDecorations = _decorationsTree.CollectNodesPostOrder();
        for (int i = 0, len = allDecorations.Count; i < len; i++)
        {
            var node = allDecorations[i];
            var range = node.Range!.Value; // the range is defined due to `OnBeforeEOLChange`

            var delta = node.CachedAbsoluteStart - node.Start;

            var startOffset = _buffer.GetOffsetAt(range.StartLineNumber, range.StartColumn);
            var endOffset = _buffer.GetOffsetAt(range.EndLineNumber, range.EndColumn);

            node.CachedAbsoluteStart = startOffset;
            node.CachedAbsoluteEnd = endOffset;
            node.CachedVersionId = versionId;

            node.Start = startOffset - delta;
            node.End = endOffset - delta;

            _decorationsTree.RecomputeMaxEnd(node);
        }
    }

    /// <summary>Ends the current undo step: the next edit starts a new one.</summary>
    public void PushStackElement() => _commandManager.PushStackElement();

    /// <summary>Reopens the last undo step, so that the next edit joins it.</summary>
    public void PopStackElement() => _commandManager.PopStackElement();

    /// <summary>Changes the end of line sequence as an undo step.</summary>
    public void PushEOL(EndOfLineSequence eol)
    {
        var currentEOL = GetEOL() == "\n" ? EndOfLineSequence.LF : EndOfLineSequence.CRLF;
        if (currentEOL == eol)
        {
            return;
        }

        try
        {
            _onDidChangeDecorations.BeginDeferredEmit();
            _eventEmitter.BeginDeferredEmit();
            _initialUndoRedoSnapshot ??= _undoRedoService.CreateSnapshot(Uri);
            _commandManager.PushEOL(eol);
        }
        finally
        {
            _eventEmitter.EndDeferredEmit();
            _onDidChangeDecorations.EndDeferredEmit();
        }
    }

    private ValidAnnotatedEditOperation ValidateEditOperation(SingleEditOperation rawOperation)
    {
        var validatedRange = ValidateRange(rawOperation.Range);

        // Normalize edit when replacement text ends with lone CR
        // and the range ends right before a CRLF in the buffer.
        // We strip the trailing CR from the replacement text.
        var opText = rawOperation.Text;
        if (!string.IsNullOrEmpty(opText))
        {
            var endsWithLoneCR = opText[^1] == '\r';
            var removeTrailingCR = GetEOL() == "\r\n"
                && endsWithLoneCR
                && validatedRange.EndColumn == GetLineMaxColumn(validatedRange.EndLineNumber);
            if (removeTrailingCR)
            {
                opText = opText.Substring(0, opText.Length - 1);
            }
        }

        return new ValidAnnotatedEditOperation(
            rawOperation.Identifier,
            validatedRange,
            opText,
            rawOperation.ForceMoveMarkers,
            rawOperation.IsAutoWhitespaceEdit,
            rawOperation.IsTracked);
    }

    private List<ValidAnnotatedEditOperation> ValidateEditOperations(IReadOnlyList<SingleEditOperation> rawOperations)
    {
        var result = new List<ValidAnnotatedEditOperation>(rawOperations.Count);
        for (int i = 0, len = rawOperations.Count; i < len; i++)
        {
            result.Add(ValidateEditOperation(rawOperations[i]));
        }

        return result;
    }

    /// <summary>
    /// Applies edits as part of the current undo step (or a new one) and returns the selections that
    /// <paramref name="cursorStateComputer"/> computed from the inverse edits.
    /// </summary>
    public IReadOnlyList<TextSelection>? PushEditOperations(
        IReadOnlyList<TextSelection>? beforeCursorState,
        IReadOnlyList<SingleEditOperation> editOperations,
        CursorStateComputer? cursorStateComputer,
        UndoRedoGroup? group = null,
        TextModelEditSource? reason = null)
    {
        try
        {
            _onDidChangeDecorations.BeginDeferredEmit();
            _eventEmitter.BeginDeferredEmit();
            return PushEditOperationsCore(beforeCursorState, ValidateEditOperations(editOperations), cursorStateComputer, group, reason);
        }
        finally
        {
            _eventEmitter.EndDeferredEmit();
            _onDidChangeDecorations.EndDeferredEmit();
        }
    }

    private IReadOnlyList<TextSelection>? PushEditOperationsCore(
        IReadOnlyList<TextSelection>? beforeCursorState,
        List<ValidAnnotatedEditOperation> editOperations,
        CursorStateComputer? cursorStateComputer,
        UndoRedoGroup? group,
        TextModelEditSource? reason)
    {
        if (_options.TrimAutoWhitespace && _trimAutoWhitespaceLines is not null)
        {
            // Go through each saved line number and insert a trim whitespace edit
            // if it is safe to do so (no conflicts with other edits).

            var incomingEdits = editOperations.Select(op => (Range: ValidateRange(op.Range), op.Text)).ToList();

            // Sometimes, auto-formatters change ranges automatically which can cause undesired auto whitespace trimming near the cursor
            // We'll use the following heuristic: if the edits occur near the cursor, then it's ok to trim auto whitespace
            var editsAreNearCursors = true;
            if (beforeCursorState is not null)
            {
                for (int i = 0, len = beforeCursorState.Count; i < len; i++)
                {
                    var sel = beforeCursorState[i];
                    var foundEditNearSel = false;
                    for (int j = 0, lenJ = incomingEdits.Count; j < lenJ; j++)
                    {
                        var editRange = incomingEdits[j].Range;
                        var selIsAbove = editRange.StartLineNumber > sel.EndLineNumber;
                        var selIsBelow = sel.StartLineNumber > editRange.EndLineNumber;
                        if (!selIsAbove && !selIsBelow)
                        {
                            foundEditNearSel = true;
                            break;
                        }
                    }

                    if (!foundEditNearSel)
                    {
                        editsAreNearCursors = false;
                        break;
                    }
                }
            }

            if (editsAreNearCursors)
            {
                for (int i = 0, len = _trimAutoWhitespaceLines.Count; i < len; i++)
                {
                    var trimLineNumber = _trimAutoWhitespaceLines[i];
                    var maxLineColumn = GetLineMaxColumn(trimLineNumber);

                    var allowTrimLine = true;
                    for (int j = 0, lenJ = incomingEdits.Count; j < lenJ; j++)
                    {
                        var editRange = incomingEdits[j].Range;
                        var editText = incomingEdits[j].Text;

                        if (trimLineNumber < editRange.StartLineNumber || trimLineNumber > editRange.EndLineNumber)
                        {
                            // `trimLine` is completely outside this edit
                            continue;
                        }

                        // At this point:
                        //   editRange.startLineNumber <= trimLine <= editRange.endLineNumber

                        if (trimLineNumber == editRange.StartLineNumber && editRange.StartColumn == maxLineColumn
                            && editRange.IsEmpty && !string.IsNullOrEmpty(editText) && editText[0] == '\n')
                        {
                            // This edit inserts a new line (and maybe other text) after `trimLine`
                            continue;
                        }

                        if (trimLineNumber == editRange.StartLineNumber && editRange.StartColumn == 1
                            && editRange.IsEmpty && !string.IsNullOrEmpty(editText) && editText[^1] == '\n')
                        {
                            // This edit inserts a new line (and maybe other text) before `trimLine`
                            continue;
                        }

                        // Looks like we can't trim this line as it would interfere with an incoming edit
                        allowTrimLine = false;
                        break;
                    }

                    if (allowTrimLine)
                    {
                        var trimRange = new TextRange(trimLineNumber, 1, trimLineNumber, maxLineColumn);
                        editOperations.Add(new ValidAnnotatedEditOperation(null, trimRange, null, false, false, false));
                    }
                }
            }

            _trimAutoWhitespaceLines = null;
        }

        _initialUndoRedoSnapshot ??= _undoRedoService.CreateSnapshot(Uri);

        return _commandManager.PushEditOperation(beforeCursorState, editOperations, cursorStateComputer, group, reason);
    }

    internal void ApplyUndo(
        IReadOnlyList<TextChange> changes,
        EndOfLineSequence eol,
        int resultingAlternativeVersionId,
        IReadOnlyList<TextSelection>? resultingSelection)
    {
        var edits = changes.Select(change =>
        {
            var rangeStart = GetPositionAt(change.NewPosition);
            var rangeEnd = GetPositionAt(change.NewEnd);
            return new SingleEditOperation(
                new TextRange(rangeStart.LineNumber, rangeStart.Column, rangeEnd.LineNumber, rangeEnd.Column),
                change.OldText);
        }).ToList();
        ApplyUndoRedoEdits(edits, eol, true, false, resultingAlternativeVersionId, resultingSelection);
    }

    internal void ApplyRedo(
        IReadOnlyList<TextChange> changes,
        EndOfLineSequence eol,
        int resultingAlternativeVersionId,
        IReadOnlyList<TextSelection>? resultingSelection)
    {
        var edits = changes.Select(change =>
        {
            var rangeStart = GetPositionAt(change.OldPosition);
            var rangeEnd = GetPositionAt(change.OldEnd);
            return new SingleEditOperation(
                new TextRange(rangeStart.LineNumber, rangeStart.Column, rangeEnd.LineNumber, rangeEnd.Column),
                change.NewText);
        }).ToList();
        ApplyUndoRedoEdits(edits, eol, false, true, resultingAlternativeVersionId, resultingSelection);
    }

    private void ApplyUndoRedoEdits(
        List<SingleEditOperation> edits,
        EndOfLineSequence eol,
        bool isUndoing,
        bool isRedoing,
        int resultingAlternativeVersionId,
        IReadOnlyList<TextSelection>? resultingSelection)
    {
        try
        {
            _onDidChangeDecorations.BeginDeferredEmit();
            _eventEmitter.BeginDeferredEmit();
            _isUndoing = isUndoing;
            _isRedoing = isRedoing;
            var operations = ValidateEditOperations(edits);
            DoApplyEdits(operations, false, EditSources.ApplyEdits(), resultingSelection);
            SetEOL(eol);
            OverwriteAlternativeVersionId(resultingAlternativeVersionId);
        }
        finally
        {
            _isUndoing = false;
            _isRedoing = false;
            _eventEmitter.EndDeferredEmit(resultingSelection);
            _onDidChangeDecorations.EndDeferredEmit();
        }
    }

    /// <summary>
    /// Applies edits outside the undo stack. With <paramref name="computeUndoEdits"/> it returns the edits that undo
    /// them; otherwise null.
    /// </summary>
    public List<ValidEditOperation>? ApplyEdits(
        IReadOnlyList<SingleEditOperation> rawOperations,
        bool computeUndoEdits = false,
        TextModelEditSource? reason = null)
    {
        try
        {
            _onDidChangeDecorations.BeginDeferredEmit();
            _eventEmitter.BeginDeferredEmit();
            var operations = ValidateEditOperations(rawOperations);

            return DoApplyEdits(operations, computeUndoEdits, reason ?? EditSources.ApplyEdits());
        }
        finally
        {
            _eventEmitter.EndDeferredEmit();
            _onDidChangeDecorations.EndDeferredEmit();
        }
    }

    /// <summary>
    /// <see cref="ApplyEdits"/> for operations this model validated already (upstream's <c>applyEdits</c> recognizes
    /// them and does not validate them again).
    /// </summary>
    internal List<ValidEditOperation>? ApplyValidatedEdits(
        List<ValidAnnotatedEditOperation> operations,
        bool computeUndoEdits,
        TextModelEditSource reason)
    {
        try
        {
            _onDidChangeDecorations.BeginDeferredEmit();
            _eventEmitter.BeginDeferredEmit();
            return DoApplyEdits(operations, computeUndoEdits, reason);
        }
        finally
        {
            _eventEmitter.EndDeferredEmit();
            _onDidChangeDecorations.EndDeferredEmit();
        }
    }

    private List<ValidEditOperation>? DoApplyEdits(
        List<ValidAnnotatedEditOperation> rawOperations,
        bool computeUndoEdits,
        TextModelEditSource reason,
        IReadOnlyList<TextSelection>? resultingSelection = null)
    {
        var oldLineCount = _buffer.GetLineCount();
        var result = _buffer.ApplyEdits(rawOperations, _options.TrimAutoWhitespace, computeUndoEdits);
        var newLineCount = _buffer.GetLineCount();

        var contentChanges = result.Changes;
        _trimAutoWhitespaceLines = result.TrimAutoWhitespaceLineNumbers;

        if (contentChanges.Count != 0)
        {
            // We do a first pass to update decorations
            // because we want to read decorations in the second pass
            // where we will emit content change events
            // and we want to read the final decorations
            for (int i = 0, len = contentChanges.Count; i < len; i++)
            {
                var change = contentChanges[i];
                _decorationsTree.AcceptReplace(change.RangeOffset, change.RangeLength, change.Text.Length, change.ForceMoveMarkers);
            }

            var rawContentChanges = new List<ModelRawChange>();

            IncreaseVersionId();

            var lineCount = oldLineCount;
            for (int i = 0, len = contentChanges.Count; i < len; i++)
            {
                var change = contentChanges[i];
                var eolCount = EolCounter.CountEOL(change.Text).Count;
                _onDidChangeDecorations.Fire();

                var startLineNumber = change.Range.StartLineNumber;
                var endLineNumber = change.Range.EndLineNumber;

                var deletingLinesCnt = endLineNumber - startLineNumber;
                var insertingLinesCnt = eolCount;
                var editingLinesCnt = Math.Min(deletingLinesCnt, insertingLinesCnt);

                var changeLineCountDelta = insertingLinesCnt - deletingLinesCnt;

                var currentEditStartLineNumber = newLineCount - lineCount - changeLineCountDelta + startLineNumber;

                for (var j = editingLinesCnt; j >= 0; j--)
                {
                    var editLineNumber = startLineNumber + j;
                    var currentEditLineNumber = currentEditStartLineNumber + j;

                    rawContentChanges.Add(new ModelRawLineChanged(editLineNumber, currentEditLineNumber));
                }

                if (editingLinesCnt < deletingLinesCnt)
                {
                    // Must delete some lines
                    var spliceStartLineNumber = startLineNumber + editingLinesCnt;
                    var cnt = insertingLinesCnt - deletingLinesCnt;
                    var lastUntouchedLinePostEdit = newLineCount - lineCount - cnt + spliceStartLineNumber;
                    rawContentChanges.Add(new ModelRawLinesDeleted(spliceStartLineNumber + 1, endLineNumber, lastUntouchedLinePostEdit));
                }

                if (editingLinesCnt < insertingLinesCnt)
                {
                    // Must insert some lines
                    var spliceLineNumber = startLineNumber + editingLinesCnt;
                    var cnt = insertingLinesCnt - editingLinesCnt;
                    var fromLineNumber = newLineCount - lineCount - cnt + spliceLineNumber + 1;
                    rawContentChanges.Add(new ModelRawLinesInserted(spliceLineNumber + 1, fromLineNumber, cnt));
                }

                lineCount += changeLineCountDelta;
            }

            var changes = new List<ModelContentChange>(contentChanges.Count);
            foreach (var change in contentChanges)
            {
                changes.Add(new ModelContentChange(change.Range, change.RangeOffset, change.RangeLength, change.Text));
            }

            EmitContentChangedEvent(
                new ModelRawContentChangedEvent(rawContentChanges, GetVersionId(), _isUndoing, _isRedoing),
                new ModelContentChangedEvent(
                    changes,
                    _buffer.GetEOL(),
                    GetVersionId(),
                    _isUndoing,
                    _isRedoing,
                    false,
                    false,
                    [reason],
                    [contentChanges.Count]),
                resultingSelection);
        }

        return result.ReverseEdits;
    }

    public ValueTask Undo() => _undoRedoService.Undo(Uri);

    public bool CanUndo() => _undoRedoService.CanUndo(Uri);

    public ValueTask Redo() => _undoRedoService.Redo(Uri);

    public bool CanRedo() => _undoRedoService.CanRedo(Uri);
}
