// Ported from vscode (src/vs/editor/common/cursor/cursorDeleteOperations.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - deleteLeft and deleteRight return a named tuple instead of a two-element array;
//  - the strategies are the enums EditorAutoClosingStrategy and EditorAutoClosingEditStrategy instead of strings, and
//    autoClosingPairsOpen is keyed by a one-character string as in AutoClosingPairs;
//  - cut sorts a copy of the selections (upstream sorts the caller's array in place) with a stable insertion sort:
//    upstream's comparator compares a selection's start with the other's end, which is not a consistent ordering, and
//    List.Sort may throw on such a comparator where JavaScript's sort does not.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;

namespace TKCode.Arc.Editor.Cursor;

/// <summary>Delete left (backspace), delete right (delete) and cut.</summary>
internal static class DeleteOperations
{
    public static (bool ShouldPushStackElementBefore, ICommand?[] Commands) DeleteRight(
        EditOperationType prevEditOperationType, CursorConfiguration config, ICursorSimpleModel model, IReadOnlyList<TextSelection> selections)
    {
        var commands = new ICommand?[selections.Count];
        var shouldPushStackElementBefore = prevEditOperationType != EditOperationType.DeletingRight;
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var selection = selections[i];

            var deleteSelection = GetDeleteRightRange(selection, model, config);

            if (deleteSelection.IsEmpty)
            {
                // Probably at end of file => ignore
                commands[i] = null;
                continue;
            }

            if (deleteSelection.StartLineNumber != deleteSelection.EndLineNumber)
            {
                shouldPushStackElementBefore = true;
            }

            commands[i] = new ReplaceCommand(deleteSelection, "");
        }
        return (shouldPushStackElementBefore, commands);
    }

    private static TextRange GetDeleteRightRange(TextSelection selection, ICursorSimpleModel model, CursorConfiguration config)
    {
        if (!selection.IsEmpty)
        {
            return selection;
        }

        var position = selection.Position;
        var rightOfPosition = MoveOperations.Right(config, model, position);

        if (config.TrimWhitespaceOnDelete && rightOfPosition.LineNumber != position.LineNumber)
        {
            // Smart line join (deleting leading whitespace) is on
            // (and) Delete is happening at the end of a line
            var currentLineHasContent = model.GetLineFirstNonWhitespaceColumn(position.LineNumber) > 0;
            var firstNonWhitespaceColumn = model.GetLineFirstNonWhitespaceColumn(rightOfPosition.LineNumber);
            if (currentLineHasContent && firstNonWhitespaceColumn > 0)
            {
                // The next line has content
                return new TextRange(
                    rightOfPosition.LineNumber,
                    firstNonWhitespaceColumn,
                    position.LineNumber,
                    position.Column);
            }
        }

        return new TextRange(
            rightOfPosition.LineNumber,
            rightOfPosition.Column,
            position.LineNumber,
            position.Column);
    }

    public static bool IsAutoClosingPairDelete(
        EditorAutoClosingEditStrategy autoClosingDelete,
        EditorAutoClosingStrategy autoClosingBrackets,
        EditorAutoClosingStrategy autoClosingQuotes,
        IReadOnlyDictionary<string, List<StandardAutoClosingPairConditional>> autoClosingPairsOpen,
        ICursorSimpleModel model,
        IReadOnlyList<TextSelection> selections,
        IReadOnlyList<TextRange> autoClosedCharacters)
    {
        if (autoClosingBrackets == EditorAutoClosingStrategy.Never && autoClosingQuotes == EditorAutoClosingStrategy.Never)
        {
            return false;
        }
        if (autoClosingDelete == EditorAutoClosingEditStrategy.Never)
        {
            return false;
        }

        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var selection = selections[i];
            var position = selection.Position;

            if (!selection.IsEmpty)
            {
                return false;
            }

            var lineText = model.GetLineContent(position.LineNumber);
            if (position.Column < 2 || position.Column >= lineText.Length + 1)
            {
                return false;
            }
            var character = lineText[position.Column - 2];

            if (!autoClosingPairsOpen.TryGetValue(character.ToString(), out var autoClosingPairCandidates))
            {
                return false;
            }

            if (CursorCommon.IsQuote(character))
            {
                if (autoClosingQuotes == EditorAutoClosingStrategy.Never)
                {
                    return false;
                }
            }
            else
            {
                if (autoClosingBrackets == EditorAutoClosingStrategy.Never)
                {
                    return false;
                }
            }

            var afterCharacter = lineText[position.Column - 1];

            var foundAutoClosingPair = false;
            foreach (var autoClosingPairCandidate in autoClosingPairCandidates)
            {
                if (autoClosingPairCandidate.Open.Length == 1 && autoClosingPairCandidate.Open[0] == character
                    && autoClosingPairCandidate.Close.Length == 1 && autoClosingPairCandidate.Close[0] == afterCharacter)
                {
                    foundAutoClosingPair = true;
                }
            }
            if (!foundAutoClosingPair)
            {
                return false;
            }

            // Must delete the pair only if it was automatically inserted by the editor
            if (autoClosingDelete == EditorAutoClosingEditStrategy.Auto)
            {
                var found = false;
                for (int j = 0, lenJ = autoClosedCharacters.Count; j < lenJ; j++)
                {
                    var autoClosedCharacter = autoClosedCharacters[j];
                    if (position.LineNumber == autoClosedCharacter.StartLineNumber && position.Column == autoClosedCharacter.StartColumn)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static (bool ShouldPushStackElementBefore, ICommand?[] Commands) RunAutoClosingPairDelete(
        CursorConfiguration config, ICursorSimpleModel model, IReadOnlyList<TextSelection> selections)
    {
        var commands = new ICommand?[selections.Count];
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var position = selections[i].Position;
            var deleteSelection = new TextRange(
                position.LineNumber,
                position.Column - 1,
                position.LineNumber,
                position.Column + 1);
            commands[i] = new ReplaceCommand(deleteSelection, "");
        }
        return (true, commands);
    }

    public static (bool ShouldPushStackElementBefore, ICommand?[] Commands) DeleteLeft(
        EditOperationType prevEditOperationType,
        CursorConfiguration config,
        ICursorSimpleModel model,
        IReadOnlyList<TextSelection> selections,
        IReadOnlyList<TextRange> autoClosedCharacters)
    {
        if (IsAutoClosingPairDelete(config.AutoClosingDelete, config.AutoClosingBrackets, config.AutoClosingQuotes, config.AutoClosingPairs.AutoClosingPairsOpenByEnd, model, selections, autoClosedCharacters))
        {
            return RunAutoClosingPairDelete(config, model, selections);
        }

        var commands = new ICommand?[selections.Count];
        var shouldPushStackElementBefore = prevEditOperationType != EditOperationType.DeletingLeft;
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var deleteRange = GetDeleteLeftRange(selections[i], model, config);

            // Ignore empty delete ranges, as they have no effect
            // They happen if the cursor is at the beginning of the file.
            if (deleteRange.IsEmpty)
            {
                commands[i] = null;
                continue;
            }

            if (deleteRange.StartLineNumber != deleteRange.EndLineNumber)
            {
                shouldPushStackElementBefore = true;
            }

            commands[i] = new ReplaceCommand(deleteRange, "");
        }
        return (shouldPushStackElementBefore, commands);
    }

    private static TextRange GetDeleteLeftRange(TextSelection selection, ICursorSimpleModel model, CursorConfiguration config)
    {
        if (!selection.IsEmpty)
        {
            return selection;
        }

        var position = selection.Position;

        // Unintend when using tab stops and cursor is within indentation
        if (config.UseTabStops && position.Column > 1)
        {
            var lineContent = model.GetLineContent(position.LineNumber);

            var firstNonWhitespaceIndex = Strings.FirstNonWhitespaceIndex(lineContent);
            var lastIndentationColumn = firstNonWhitespaceIndex == -1
                ? /* entire string is whitespace */ lineContent.Length + 1
                : firstNonWhitespaceIndex + 1;

            if (position.Column <= lastIndentationColumn)
            {
                var fromVisibleColumn = config.VisibleColumnFromColumn(model, position);
                var toVisibleColumn = CursorColumns.PrevIndentTabStop(fromVisibleColumn, config.IndentSize);
                var toColumn = config.ColumnFromVisibleColumn(model, position.LineNumber, toVisibleColumn);
                return new TextRange(position.LineNumber, toColumn, position.LineNumber, position.Column);
            }
        }

        return new TextRange(GetPositionAfterDeleteLeft(position, model), position);
    }

    private static TextPosition GetPositionAfterDeleteLeft(TextPosition position, ICursorSimpleModel model)
    {
        if (position.Column > 1)
        {
            // Convert 1-based columns to 0-based offsets and back.
            var idx = Strings.GetLeftDeleteOffset(position.Column - 1, model.GetLineContent(position.LineNumber));
            return position.With(null, idx + 1);
        }
        else if (position.LineNumber > 1)
        {
            var newLine = position.LineNumber - 1;
            return new TextPosition(newLine, model.GetLineMaxColumn(newLine));
        }
        else
        {
            return position;
        }
    }

    public static EditOperationResult Cut(CursorConfiguration config, ICursorSimpleModel model, IReadOnlyList<TextSelection> selections)
    {
        var sorted = SortForCut(selections);
        var commands = new ICommand?[sorted.Length];
        TextRange? lastCutRange = null;
        for (int i = 0, len = sorted.Length; i < len; i++)
        {
            var selection = sorted[i];

            if (selection.IsEmpty)
            {
                if (config.EmptySelectionClipboard)
                {
                    // This is a full line cut

                    var position = selection.Position;

                    int startLineNumber;
                    int startColumn;
                    int endLineNumber;
                    int endColumn;

                    if (position.LineNumber < model.GetLineCount())
                    {
                        // Cutting a line in the middle of the model
                        startLineNumber = position.LineNumber;
                        startColumn = 1;
                        endLineNumber = position.LineNumber + 1;
                        endColumn = 1;
                    }
                    else if (position.LineNumber > 1 && lastCutRange?.EndLineNumber != position.LineNumber)
                    {
                        // Cutting the last line & there are more than 1 lines in the model & a previous cut operation does not touch the current cut operation
                        startLineNumber = position.LineNumber - 1;
                        startColumn = model.GetLineMaxColumn(position.LineNumber - 1);
                        endLineNumber = position.LineNumber;
                        endColumn = model.GetLineMaxColumn(position.LineNumber);
                    }
                    else
                    {
                        // Cutting the single line that the model contains
                        startLineNumber = position.LineNumber;
                        startColumn = 1;
                        endLineNumber = position.LineNumber;
                        endColumn = model.GetLineMaxColumn(position.LineNumber);
                    }

                    var deleteSelection = new TextRange(
                        startLineNumber,
                        startColumn,
                        endLineNumber,
                        endColumn);
                    lastCutRange = deleteSelection;

                    if (!deleteSelection.IsEmpty)
                    {
                        commands[i] = new ReplaceCommand(deleteSelection, "");
                    }
                    else
                    {
                        commands[i] = null;
                    }
                }
                else
                {
                    // Cannot cut empty selection
                    commands[i] = null;
                }
            }
            else
            {
                commands[i] = new ReplaceCommand(selection, "");
            }
        }
        return new EditOperationResult(EditOperationType.Other, commands, shouldPushStackElementBefore: true, shouldPushStackElementAfter: true);
    }

    // selections.sort((a, b) => Position.compare(a.getStartPosition(), b.getEndPosition())), as a stable insertion sort.
    private static TextSelection[] SortForCut(IReadOnlyList<TextSelection> selections)
    {
        var result = new TextSelection[selections.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var item = selections[i];
            var j = i - 1;
            while (j >= 0 && Compare(result[j], item) > 0)
            {
                result[j + 1] = result[j];
                j--;
            }
            result[j + 1] = item;
        }
        return result;

        static int Compare(TextSelection a, TextSelection b) => a.Range.Start.CompareTo(b.Range.End);
    }
}
