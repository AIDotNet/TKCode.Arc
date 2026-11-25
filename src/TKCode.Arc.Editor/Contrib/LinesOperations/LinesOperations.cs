// Ported from vscode (src/vs/editor/contrib/linesOperations/browser/linesOperations.ts, commit b57d5c9e), MIT license:
// the run methods of the line actions.
// Differences from upstream:
//  - each action's run is a static method on an IEditorActionHost, named after the action class; the control registers
//    the actions with their ids, labels and keybindings (Contrib/Editing/EditingActions.cs in the controls package), the
//    case transforms are in CaseTransforms.cs;
//  - the trim trailing whitespace action reads files.trimTrailingWhitespaceInRegexAndStrings from the host, and its
//    `reason: 'auto-save'` argument is TrimTrailingWhitespaceArgs;
//  - the action id each one passes as the edit source is a parameter.

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Cursor;

namespace TKCode.Arc.Editor.Contrib.LinesOperations;

/// <summary>The arguments of the trim trailing whitespace action (upstream's TrimTrailingWhitespaceArgs).</summary>
/// <param name="Reason"><c>auto-save</c> keeps the whitespace where cursors are.</param>
internal sealed record TrimTrailingWhitespaceArgs(string? Reason);

/// <summary>The line actions of linesOperations.ts.</summary>
internal static class LinesOperations
{
    // /[\s﻿\xA0]+$/g
    private static readonly Regex s_trailingWhitespace = new(@"[\s﻿ ]+\z", RegexOptions.CultureInvariant);

    /// <summary>AbstractCopyLinesAction.run: copy line up (Shift+Alt+Up) or down (Shift+Alt+Down).</summary>
    public static void CopyLines(IEditorActionHost editor, bool down, string id)
    {
        if (editor.GetModel() is null)
        {
            return;
        }

        var selections = editor.GetSelections().Select((selection, index) => new CopyLinesSelection(selection, index)).ToList();
        if (selections.Count == 0)
        {
            return;
        }
        selections.Sort((a, b) => TextRange.CompareRangesUsingStarts(a.Selection, b.Selection));

        // Remove selections that would result in copying the same line
        var prev = selections[0];
        for (var i = 1; i < selections.Count; i++)
        {
            var curr = selections[i];
            if (prev.Selection.EndLineNumber == curr.Selection.StartLineNumber)
            {
                // these two selections would copy the same line
                if (prev.Index < curr.Index)
                {
                    // prev wins
                    curr.Ignore = true;
                }
                else
                {
                    // curr wins
                    prev.Ignore = true;
                    prev = curr;
                }
            }
        }

        var commands = new List<ICommand?>();
        foreach (var selection in selections)
        {
            commands.Add(new CopyLinesCommand(selection.Selection, down, selection.Ignore));
        }

        editor.PushUndoStop();
        editor.ExecuteCommands(id, commands);
        editor.PushUndoStop();
    }

    /// <summary>DuplicateSelectionAction.run.</summary>
    public static void DuplicateSelection(IEditorActionHost editor, string id)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }

        var commands = new List<ICommand?>();
        var selections = editor.GetSelections();

        foreach (var selection in selections)
        {
            if (selection.IsEmpty)
            {
                commands.Add(new CopyLinesCommand(selection, true));
            }
            else
            {
                var insertSelection = new TextSelection(selection.EndLineNumber, selection.EndColumn, selection.EndLineNumber, selection.EndColumn);
                commands.Add(new ReplaceCommandThatSelectsText(insertSelection, model.GetValueInRange(selection)));
            }
        }

        editor.PushUndoStop();
        editor.ExecuteCommands(id, commands);
        editor.PushUndoStop();
    }

    /// <summary>AbstractMoveLinesAction.run: move line up (Alt+Up) or down (Alt+Down).</summary>
    public static void MoveLines(IEditorActionHost editor, bool down, string id)
    {
        var commands = new List<ICommand?>();
        var selections = editor.GetSelections();
        var autoIndent = editor.GetOptions().AutoIndent;

        foreach (var selection in selections)
        {
            commands.Add(new MoveLinesCommand(selection, down, autoIndent, editor.LanguageConfigurationService));
        }

        editor.PushUndoStop();
        editor.ExecuteCommands(id, commands);
        editor.PushUndoStop();
    }

    /// <summary>AbstractSortLinesAction.run.</summary>
    public static void SortLines(IEditorActionHost editor, bool descending, string id)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }

        var selections = editor.GetSelections();
        if (selections.Count == 1 && selections[0].Range.IsSingleLine)
        {
            // Apply to whole document.
            selections = [new TextSelection(1, 1, model.GetLineCount(), model.GetLineMaxColumn(model.GetLineCount()))];
        }

        foreach (var selection in selections)
        {
            if (!SortLinesCommand.CanRun(model, selection, descending))
            {
                return;
            }
        }

        var commands = new ICommand?[selections.Count];
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            commands[i] = new SortLinesCommand(selections[i], descending);
        }

        editor.PushUndoStop();
        editor.ExecuteCommands(id, commands);
        editor.PushUndoStop();
    }

    /// <summary>DeleteDuplicateLinesAction.run.</summary>
    public static void DeleteDuplicateLines(IEditorActionHost editor, string id)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }

        if (model.GetLineCount() == 1 && model.GetLineMaxColumn(1) == 1)
        {
            return;
        }

        var edits = new List<SingleEditOperation>();
        var endCursorState = new List<TextSelection>();

        var linesDeleted = 0;
        var updateSelection = true;

        var selections = editor.GetSelections();
        if (selections.Count == 1 && selections[0].Range.IsSingleLine)
        {
            // Apply to whole document.
            selections = [new TextSelection(1, 1, model.GetLineCount(), model.GetLineMaxColumn(model.GetLineCount()))];
            updateSelection = false;
        }

        foreach (var selection in selections)
        {
            var uniqueLines = new HashSet<string>(StringComparer.Ordinal);
            var lines = new List<string>();

            for (var i = selection.StartLineNumber; i <= selection.EndLineNumber; i++)
            {
                var line = model.GetLineContent(i);

                if (uniqueLines.Contains(line))
                {
                    continue;
                }

                lines.Add(line);
                uniqueLines.Add(line);
            }

            var selectionToReplace = new TextSelection(
                selection.StartLineNumber,
                1,
                selection.EndLineNumber,
                model.GetLineMaxColumn(selection.EndLineNumber));

            var adjustedSelectionStart = selection.StartLineNumber - linesDeleted;
            var finalSelection = new TextSelection(
                adjustedSelectionStart,
                1,
                adjustedSelectionStart + lines.Count - 1,
                lines[^1].Length + 1);

            edits.Add(EditOperation.Replace(selectionToReplace, string.Join('\n', lines)));
            endCursorState.Add(finalSelection);

            linesDeleted += (selection.EndLineNumber - selection.StartLineNumber + 1) - lines.Count;
        }

        editor.PushUndoStop();
        editor.ExecuteEdits(id, edits, updateSelection ? endCursorState : null);
        editor.PushUndoStop();
    }

    /// <summary>ReverseLinesAction.run.</summary>
    public static void ReverseLines(IEditorActionHost editor, string id)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }

        var originalSelections = editor.GetSelections();
        var selections = originalSelections;
        if (selections.Count == 1 && selections[0].Range.IsSingleLine)
        {
            // Apply to whole document.
            selections = [new TextSelection(1, 1, model.GetLineCount(), model.GetLineMaxColumn(model.GetLineCount()))];
        }

        var edits = new List<SingleEditOperation>();
        var resultingSelections = new List<TextSelection>();

        for (var i = 0; i < selections.Count; i++)
        {
            var selection = selections[i];
            var originalSelection = originalSelections[i];
            var endLineNumber = selection.EndLineNumber;
            if (selection.StartLineNumber < selection.EndLineNumber && selection.EndColumn == 1)
            {
                endLineNumber--;
            }

            var range = new TextRange(selection.StartLineNumber, 1, endLineNumber, model.GetLineMaxColumn(endLineNumber));

            // Exclude last line if empty and we're at the end of the document
            if (endLineNumber == model.GetLineCount() && model.GetLineContent(range.EndLineNumber).Length == 0)
            {
                range = range.SetEndPosition(range.EndLineNumber - 1, model.GetLineMaxColumn(range.EndLineNumber - 1));
            }

            var lines = new List<string>();
            for (var j = range.EndLineNumber; j >= range.StartLineNumber; j--)
            {
                lines.Add(model.GetLineContent(j));
            }
            edits.Add(EditOperation.Replace(range, string.Join('\n', lines)));

            int UpdateLineNumber(int lineNumber) =>
                lineNumber <= range.EndLineNumber ? range.EndLineNumber - lineNumber + range.StartLineNumber : lineNumber;

            TextSelection UpdateSelection(TextSelection sel)
            {
                if (sel.IsEmpty)
                {
                    // keep just the cursor
                    return new TextSelection(UpdateLineNumber(sel.PositionLineNumber), sel.PositionColumn, UpdateLineNumber(sel.PositionLineNumber), sel.PositionColumn);
                }

                // keep selection - maintain direction by creating backward selection
                var newSelectionStart = UpdateLineNumber(sel.SelectionStartLineNumber);
                var newPosition = UpdateLineNumber(sel.PositionLineNumber);
                return new TextSelection(newSelectionStart, sel.SelectionStartColumn, newPosition, sel.PositionColumn);
            }

            resultingSelections.Add(UpdateSelection(originalSelection));
        }

        editor.PushUndoStop();
        editor.ExecuteEdits(id, edits, resultingSelections);
        editor.PushUndoStop();
    }

    /// <summary>TrimTrailingWhitespaceAction.run (Ctrl+K Ctrl+X).</summary>
    public static void TrimTrailingWhitespace(IEditorActionHost editor, TrimTrailingWhitespaceArgs? args, string id)
    {
        IReadOnlyList<TextPosition> cursors = [];
        if (args?.Reason == "auto-save")
        {
            // See https://github.com/editorconfig/editorconfig-vscode/issues/47
            // It is very convenient for the editor config extension to invoke this action.
            // So, if we get a reason:'auto-save' passed in, let's preserve cursor positions.
            cursors = editor.GetSelections().Select(static s => new TextPosition(s.PositionLineNumber, s.PositionColumn)).ToList();
        }

        if (editor.GetSelection() is not { } selection)
        {
            return;
        }

        var command = new TrimTrailingWhitespaceCommand(selection, cursors, editor.TrimTrailingWhitespaceInRegexAndStrings);

        editor.PushUndoStop();
        editor.ExecuteCommands(id, [command]);
        editor.PushUndoStop();
    }

    /// <summary>DeleteLinesAction.run (Ctrl+Shift+K).</summary>
    public static void DeleteLines(IEditorActionHost editor, string id)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }

        var ops = GetLinesToRemove(editor);

        if (model.GetLineCount() == 1 && model.GetLineMaxColumn(1) == 1)
        {
            // Model is empty
            return;
        }

        var linesDeleted = 0;
        var edits = new List<SingleEditOperation>();
        var cursorState = new List<TextSelection>();
        for (int i = 0, len = ops.Count; i < len; i++)
        {
            var op = ops[i];

            var startLineNumber = op.StartLineNumber;
            var endLineNumber = op.EndLineNumber;

            var startColumn = 1;
            var endColumn = model.GetLineMaxColumn(endLineNumber);
            if (endLineNumber < model.GetLineCount())
            {
                endLineNumber += 1;
                endColumn = 1;
            }
            else if (startLineNumber > 1)
            {
                startLineNumber -= 1;
                startColumn = model.GetLineMaxColumn(startLineNumber);
            }

            edits.Add(EditOperation.Replace(new TextSelection(startLineNumber, startColumn, endLineNumber, endColumn), ""));
            cursorState.Add(new TextSelection(startLineNumber - linesDeleted, op.PositionColumn, startLineNumber - linesDeleted, op.PositionColumn));
            linesDeleted += op.EndLineNumber - op.StartLineNumber + 1;
        }

        editor.PushUndoStop();
        editor.ExecuteEdits(id, edits, cursorState);
        editor.RevealAllCursors(true);
        editor.PushUndoStop();
    }

    private static List<DeleteLinesOperation> GetLinesToRemove(IEditorActionHost editor)
    {
        // Construct delete operations
        var operations = editor.GetSelections().Select(s =>
        {
            var endLineNumber = s.EndLineNumber;
            if (s.StartLineNumber < s.EndLineNumber && s.EndColumn == 1)
            {
                endLineNumber -= 1;
            }

            return new DeleteLinesOperation
            {
                StartLineNumber = s.StartLineNumber,
                SelectionStartColumn = s.SelectionStartColumn,
                EndLineNumber = endLineNumber,
                PositionColumn = s.PositionColumn,
            };
        }).ToList();

        // Sort delete operations
        operations.Sort((a, b) =>
        {
            if (a.StartLineNumber == b.StartLineNumber)
            {
                return a.EndLineNumber - b.EndLineNumber;
            }
            return a.StartLineNumber - b.StartLineNumber;
        });

        // Merge delete operations which are adjacent or overlapping
        var mergedOperations = new List<DeleteLinesOperation>();
        var previousOperation = operations[0];
        for (var i = 1; i < operations.Count; i++)
        {
            if (previousOperation.EndLineNumber + 1 >= operations[i].StartLineNumber)
            {
                // Merge current operations into the previous one
                previousOperation.EndLineNumber = operations[i].EndLineNumber;
            }
            else
            {
                // Push previous operation
                mergedOperations.Add(previousOperation);
                previousOperation = operations[i];
            }
        }
        // Push the last operation
        mergedOperations.Add(previousOperation);

        return mergedOperations;
    }

    /// <summary>IndentLinesAction.run (Ctrl+]).</summary>
    public static void IndentLines(IEditorActionHost editor, string id)
    {
        if (editor.GetViewModel() is not { } viewModel)
        {
            return;
        }
        editor.PushUndoStop();
        editor.ExecuteCommands(id, TypeOperations.Indent(viewModel.CursorConfig, editor.GetModel(), editor.GetSelections()));
        editor.PushUndoStop();
    }

    /// <summary>OutdentLinesAction.run (Ctrl+[): the core outdent command.</summary>
    public static void OutdentLines(IEditorActionHost editor) =>
        CoreEditingCommands.Outdent.RunEditorCommand(editor, null);

    /// <summary>InsertLineBeforeAction.run (Ctrl+Shift+Enter).</summary>
    public static void InsertLineBefore(IEditorActionHost editor, string id)
    {
        if (editor.GetViewModel() is not { } viewModel)
        {
            return;
        }
        editor.PushUndoStop();
        editor.ExecuteCommands(id, EnterOperation.LineInsertBefore(viewModel.CursorConfig, editor.GetModel(), editor.GetSelections()));
    }

    /// <summary>InsertLineAfterAction.run (Ctrl+Enter).</summary>
    public static void InsertLineAfter(IEditorActionHost editor, string id)
    {
        if (editor.GetViewModel() is not { } viewModel)
        {
            return;
        }
        editor.PushUndoStop();
        editor.ExecuteCommands(id, EnterOperation.LineInsertAfter(viewModel.CursorConfig, editor.GetModel(), editor.GetSelections()));
    }

    /// <summary>DeleteAllLeftAction.run (Cmd+Backspace on macOS).</summary>
    public static void DeleteAllLeft(IEditorActionHost editor, string id) =>
        DeleteAllToBoundary(editor, id, GetRangesToDeleteLeft, GetEndCursorStateLeft);

    /// <summary>DeleteAllRightAction.run (Ctrl+K, Cmd+Delete on macOS).</summary>
    public static void DeleteAllRight(IEditorActionHost editor, string id) =>
        DeleteAllToBoundary(editor, id, GetRangesToDeleteRight, GetEndCursorStateRight);

    // AbstractDeleteAllToBoundaryAction.run
    private static void DeleteAllToBoundary(
        IEditorActionHost editor,
        string id,
        Func<IEditorActionHost, List<TextRange>> getRangesToDelete,
        Func<TextRange, List<TextRange>, List<TextSelection>> getEndCursorState)
    {
        if (editor.GetModel() is null || editor.GetSelection() is not { } primaryCursor)
        {
            return;
        }

        var rangesToDelete = getRangesToDelete(editor);
        if (rangesToDelete.Count == 0)
        {
            return;
        }
        // merge overlapping selections
        var effectiveRanges = new List<TextRange>();

        for (int i = 0, count = rangesToDelete.Count - 1; i < count; i++)
        {
            var range = rangesToDelete[i];
            var nextRange = rangesToDelete[i + 1];

            if (range.IntersectRanges(nextRange) is null)
            {
                effectiveRanges.Add(range);
            }
            else
            {
                rangesToDelete[i + 1] = range.PlusRange(nextRange);
            }
        }

        effectiveRanges.Add(rangesToDelete[^1]);

        var endCursorState = getEndCursorState(primaryCursor, effectiveRanges);

        var edits = effectiveRanges.Select(static range => EditOperation.Replace(range, "")).ToList();

        editor.PushUndoStop();
        editor.ExecuteEdits(id, edits, endCursorState);
        editor.PushUndoStop();
    }

    // DeleteAllLeftAction._getEndCursorState
    private static List<TextSelection> GetEndCursorStateLeft(TextRange primaryCursor, List<TextRange> rangesToDelete)
    {
        TextSelection? endPrimaryCursor = null;
        var endCursorState = new List<TextSelection>();
        var deletedLines = 0;

        foreach (var range in rangesToDelete)
        {
            TextSelection endCursor;
            if (range.EndColumn == 1 && deletedLines > 0)
            {
                var newStartLine = range.StartLineNumber - deletedLines;
                endCursor = new TextSelection(newStartLine, range.StartColumn, newStartLine, range.StartColumn);
            }
            else
            {
                endCursor = new TextSelection(range.StartLineNumber, range.StartColumn, range.StartLineNumber, range.StartColumn);
            }

            deletedLines += range.EndLineNumber - range.StartLineNumber;

            if (range.IntersectRanges(primaryCursor) is not null)
            {
                endPrimaryCursor = endCursor;
            }
            else
            {
                endCursorState.Add(endCursor);
            }
        }

        if (endPrimaryCursor is { } primary)
        {
            endCursorState.Insert(0, primary);
        }

        return endCursorState;
    }

    // DeleteAllLeftAction._getRangesToDelete
    private static List<TextRange> GetRangesToDeleteLeft(IEditorActionHost editor)
    {
        if (editor.GetModel() is not { } model)
        {
            return [];
        }

        var rangesToDelete = editor.GetSelections().Select(static s => s.Range).ToList();
        rangesToDelete.Sort(TextRange.CompareRangesUsingStarts);
        return rangesToDelete.Select(selection =>
        {
            if (selection.IsEmpty)
            {
                if (selection.StartColumn == 1)
                {
                    var deleteFromLine = Math.Max(1, selection.StartLineNumber - 1);
                    var deleteFromColumn = selection.StartLineNumber == 1 ? 1 : model.GetLineLength(deleteFromLine) + 1;
                    return new TextRange(deleteFromLine, deleteFromColumn, selection.StartLineNumber, 1);
                }

                return new TextRange(selection.StartLineNumber, 1, selection.StartLineNumber, selection.StartColumn);
            }

            return new TextRange(selection.StartLineNumber, 1, selection.EndLineNumber, selection.EndColumn);
        }).ToList();
    }

    // DeleteAllRightAction._getEndCursorState
    private static List<TextSelection> GetEndCursorStateRight(TextRange primaryCursor, List<TextRange> rangesToDelete)
    {
        TextSelection? endPrimaryCursor = null;
        var endCursorState = new List<TextSelection>();
        for (int i = 0, len = rangesToDelete.Count, offset = 0; i < len; i++)
        {
            var range = rangesToDelete[i];
            var endCursor = new TextSelection(range.StartLineNumber - offset, range.StartColumn, range.StartLineNumber - offset, range.StartColumn);

            if (range.IntersectRanges(primaryCursor) is not null)
            {
                endPrimaryCursor = endCursor;
            }
            else
            {
                endCursorState.Add(endCursor);
            }
        }

        if (endPrimaryCursor is { } primary)
        {
            endCursorState.Insert(0, primary);
        }

        return endCursorState;
    }

    // DeleteAllRightAction._getRangesToDelete
    private static List<TextRange> GetRangesToDeleteRight(IEditorActionHost editor)
    {
        if (editor.GetModel() is not { } model)
        {
            return [];
        }

        var rangesToDelete = editor.GetSelections().Select(sel =>
        {
            if (sel.IsEmpty)
            {
                var maxColumn = model.GetLineMaxColumn(sel.StartLineNumber);

                if (sel.StartColumn == maxColumn)
                {
                    return new TextRange(sel.StartLineNumber, sel.StartColumn, sel.StartLineNumber + 1, 1);
                }

                return new TextRange(sel.StartLineNumber, sel.StartColumn, sel.StartLineNumber, maxColumn);
            }
            return sel.Range;
        }).ToList();

        rangesToDelete.Sort(TextRange.CompareRangesUsingStarts);
        return rangesToDelete;
    }

    /// <summary>JoinLinesAction.run (Ctrl+J on macOS).</summary>
    public static void JoinLines(IEditorActionHost editor, string id)
    {
        var selections = editor.GetSelections().ToList();
        if (selections.Count == 0)
        {
            return;
        }

        if (editor.GetSelection() is not { } primaryCursor)
        {
            return;
        }

        selections.Sort((a, b) => TextRange.CompareRangesUsingStarts(a, b));
        var reducedSelections = new List<TextSelection>();

        var lastSelection = selections[0];
        for (var k = 1; k < selections.Count; k++)
        {
            var previousValue = lastSelection;
            var currentValue = selections[k];
            if (previousValue.IsEmpty)
            {
                if (previousValue.EndLineNumber == currentValue.StartLineNumber)
                {
                    if (primaryCursor.Equals(previousValue))
                    {
                        primaryCursor = currentValue;
                    }
                    lastSelection = currentValue;
                    continue;
                }

                if (currentValue.StartLineNumber > previousValue.EndLineNumber + 1)
                {
                    reducedSelections.Add(previousValue);
                    lastSelection = currentValue;
                }
                else
                {
                    lastSelection = new TextSelection(previousValue.StartLineNumber, previousValue.StartColumn, currentValue.EndLineNumber, currentValue.EndColumn);
                }
            }
            else
            {
                if (currentValue.StartLineNumber > previousValue.EndLineNumber)
                {
                    reducedSelections.Add(previousValue);
                    lastSelection = currentValue;
                }
                else
                {
                    lastSelection = new TextSelection(previousValue.StartLineNumber, previousValue.StartColumn, currentValue.EndLineNumber, currentValue.EndColumn);
                }
            }
        }

        reducedSelections.Add(lastSelection);

        if (editor.GetModel() is not { } model)
        {
            return;
        }

        var edits = new List<SingleEditOperation>();
        var endCursorState = new List<TextSelection>();
        var endPrimaryCursor = primaryCursor;
        var lineOffset = 0;

        for (int i = 0, len = reducedSelections.Count; i < len; i++)
        {
            var selection = reducedSelections[i];
            var startLineNumber = selection.StartLineNumber;
            var startColumn = 1;
            var columnDeltaOffset = 0;
            int endLineNumber;
            int endColumn;

            var selectionEndPositionOffset = model.GetLineLength(selection.EndLineNumber) - selection.EndColumn;

            if (selection.IsEmpty || selection.StartLineNumber == selection.EndLineNumber)
            {
                var position = selection.Range.Start;
                if (position.LineNumber < model.GetLineCount())
                {
                    endLineNumber = startLineNumber + 1;
                    endColumn = model.GetLineMaxColumn(endLineNumber);
                }
                else
                {
                    endLineNumber = position.LineNumber;
                    endColumn = model.GetLineMaxColumn(position.LineNumber);
                }
            }
            else
            {
                endLineNumber = selection.EndLineNumber;
                endColumn = model.GetLineMaxColumn(endLineNumber);
            }

            var trimmedLinesContent = model.GetLineContent(startLineNumber);

            for (var j = startLineNumber + 1; j <= endLineNumber; j++)
            {
                var lineText = model.GetLineContent(j);
                var firstNonWhitespaceIdx = model.GetLineFirstNonWhitespaceColumn(j);

                if (firstNonWhitespaceIdx >= 1)
                {
                    var insertSpace = true;
                    if (trimmedLinesContent.Length == 0)
                    {
                        insertSpace = false;
                    }

                    if (insertSpace && (trimmedLinesContent[^1] == ' ' || trimmedLinesContent[^1] == '\t'))
                    {
                        insertSpace = false;
                        trimmedLinesContent = s_trailingWhitespace.Replace(trimmedLinesContent, " ");
                    }

                    var lineTextWithoutIndent = lineText.Substring(firstNonWhitespaceIdx - 1);

                    trimmedLinesContent += (insertSpace ? " " : "") + lineTextWithoutIndent;

                    if (insertSpace)
                    {
                        columnDeltaOffset = lineTextWithoutIndent.Length + 1;
                    }
                    else
                    {
                        columnDeltaOffset = lineTextWithoutIndent.Length;
                    }
                }
                else
                {
                    columnDeltaOffset = 0;
                }
            }

            var deleteSelection = new TextRange(startLineNumber, startColumn, endLineNumber, endColumn);

            if (!deleteSelection.IsEmpty)
            {
                TextSelection resultSelection;

                if (selection.IsEmpty)
                {
                    edits.Add(EditOperation.Replace(deleteSelection, trimmedLinesContent));
                    resultSelection = new TextSelection(deleteSelection.StartLineNumber - lineOffset, trimmedLinesContent.Length - columnDeltaOffset + 1, startLineNumber - lineOffset, trimmedLinesContent.Length - columnDeltaOffset + 1);
                }
                else
                {
                    if (selection.StartLineNumber == selection.EndLineNumber)
                    {
                        edits.Add(EditOperation.Replace(deleteSelection, trimmedLinesContent));
                        resultSelection = new TextSelection(selection.StartLineNumber - lineOffset, selection.StartColumn,
                            selection.EndLineNumber - lineOffset, selection.EndColumn);
                    }
                    else
                    {
                        edits.Add(EditOperation.Replace(deleteSelection, trimmedLinesContent));
                        resultSelection = new TextSelection(selection.StartLineNumber - lineOffset, selection.StartColumn,
                            selection.StartLineNumber - lineOffset, trimmedLinesContent.Length - selectionEndPositionOffset);
                    }
                }

                if (deleteSelection.IntersectRanges(primaryCursor) is not null)
                {
                    endPrimaryCursor = resultSelection;
                }
                else
                {
                    endCursorState.Add(resultSelection);
                }
            }

            lineOffset += deleteSelection.EndLineNumber - deleteSelection.StartLineNumber;
        }

        endCursorState.Insert(0, endPrimaryCursor);
        editor.PushUndoStop();
        editor.ExecuteEdits(id, edits, endCursorState);
        editor.PushUndoStop();
    }

    /// <summary>TransposeAction.run: transpose the characters around each cursor.</summary>
    public static void Transpose(IEditorActionHost editor, string id)
    {
        var selections = editor.GetSelections();
        if (editor.GetModel() is not { } model)
        {
            return;
        }

        var commands = new List<ICommand?>();

        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var selection = selections[i];

            if (!selection.IsEmpty)
            {
                continue;
            }

            var cursor = selection.Range.Start;
            var maxColumn = model.GetLineMaxColumn(cursor.LineNumber);

            if (cursor.Column >= maxColumn)
            {
                if (cursor.LineNumber == model.GetLineCount())
                {
                    continue;
                }

                // The cursor is at the end of current line and current line is not empty
                // then we transpose the character before the cursor and the line break if there is any following line.
                var deleteSelection = new TextRange(cursor.LineNumber, Math.Max(1, cursor.Column - 1), cursor.LineNumber + 1, 1);
                var chars = Reverse(model.GetValueInRange(deleteSelection));

                commands.Add(new ReplaceCommand(new TextSelection(cursor.LineNumber, Math.Max(1, cursor.Column - 1), cursor.LineNumber + 1, 1), chars));
            }
            else
            {
                var deleteSelection = new TextRange(cursor.LineNumber, Math.Max(1, cursor.Column - 1), cursor.LineNumber, cursor.Column + 1);
                var chars = Reverse(model.GetValueInRange(deleteSelection));
                commands.Add(new ReplaceCommandThatPreservesSelection(deleteSelection, chars,
                    new TextSelection(cursor.LineNumber, cursor.Column + 1, cursor.LineNumber, cursor.Column + 1)));
            }
        }

        editor.PushUndoStop();
        editor.ExecuteCommands(id, commands);
        editor.PushUndoStop();
    }

    // .split('').reverse().join(''): reverses the UTF-16 code units.
    private static string Reverse(string text)
    {
        var chars = text.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    /// <summary>AbstractCaseAction.run: replaces each selection (or the word at each cursor) with <paramref name="modifyText"/>'s result.</summary>
    public static void ChangeCase(IEditorActionHost editor, Func<string, string, string> modifyText, string id)
    {
        var selections = editor.GetSelections();
        if (editor.GetModel() is not { } model)
        {
            return;
        }

        var wordSeparators = editor.GetOptions().WordSeparators;
        var textEdits = new List<SingleEditOperation>();

        foreach (var selection in selections)
        {
            if (selection.IsEmpty)
            {
                var cursor = selection.Range.Start;
                var word = editor.GetConfiguredWordAtPosition(cursor);

                if (word is null)
                {
                    continue;
                }

                var wordRange = new TextRange(cursor.LineNumber, word.StartColumn, cursor.LineNumber, word.EndColumn);
                var text = model.GetValueInRange(wordRange);
                textEdits.Add(EditOperation.Replace(wordRange, modifyText(text, wordSeparators)));
            }
            else
            {
                var text = model.GetValueInRange(selection);
                textEdits.Add(EditOperation.Replace(selection, modifyText(text, wordSeparators)));
            }
        }

        editor.PushUndoStop();
        editor.ExecuteEdits(id, textEdits, null);
        editor.PushUndoStop();
    }

    private sealed class CopyLinesSelection(TextSelection selection, int index)
    {
        public TextSelection Selection { get; } = selection;

        public int Index { get; } = index;

        public bool Ignore { get; set; }
    }

    // IDeleteLinesOperation
    private sealed class DeleteLinesOperation
    {
        public int StartLineNumber { get; init; }

        public int SelectionStartColumn { get; init; }

        public int EndLineNumber { get; set; }

        public int PositionColumn { get; init; }
    }
}
