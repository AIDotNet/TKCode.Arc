// Ported from vscode (src/vs/editor/common/cursor/cursorTypeEditOperations.ts: EnterOperation, PasteOperation,
// CompositionOperation, TypeWithoutInterceptorsOperation and TabOperation, commit b57d5c9e), MIT license.
//
// Differences from upstream:
//  - the operations are static classes; "no edit" (undefined upstream) is null;
//  - lineInsertBefore and lineInsertAfter take a nullable model and selections as upstream does;
//  - PasteOperation sorts a copy of the selections with a stable sort (upstream sorts the caller's array in place,
//    which the caller then drops); the commands are in the sorted order, as upstream's are;
//  - strings.splitLines is PieceTreeStrings.SplitLines; /^\s*$/ is JsString.IsWhitespaceOnly.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Cursor;

/// <summary>Pressing Enter: a line break with the indentation the language rules give the new line.</summary>
internal static class EnterOperation
{
    public static EditOperationResult? GetEdits(
        CursorConfiguration config,
        TextModel model,
        IReadOnlyList<TextSelection> selections,
        string ch,
        bool isDoingComposition)
    {
        if (!isDoingComposition && ch == "\n")
        {
            var commands = new ICommand?[selections.Count];
            for (int i = 0, len = selections.Count; i < len; i++)
            {
                commands[i] = Enter(config, model, false, selections[i]);
            }
            return new EditOperationResult(EditOperationType.TypingOther, commands, shouldPushStackElementBefore: true, shouldPushStackElementAfter: false);
        }
        return null;
    }

    private static ICommand Enter(CursorConfiguration config, TextModel model, bool keepPosition, TextRange range)
    {
        if (config.AutoIndent == EditorAutoIndentStrategy.None)
        {
            return TypeEditOperations.TypeCommand(range, "\n", keepPosition);
        }
        if (!model.Tokenization.IsCheapToTokenize(range.Start.LineNumber) || config.AutoIndent == EditorAutoIndentStrategy.Keep)
        {
            var lineText = model.GetLineContent(range.StartLineNumber);
            var indentation = JsString.Substring(AutoIndent.GetLeadingWhitespace(lineText), 0, range.StartColumn - 1);
            return TypeEditOperations.TypeCommand(range, "\n" + config.NormalizeIndentation(indentation), keepPosition);
        }
        var r = EnterActions.GetEnterAction(config.AutoIndent, model, range, config.LanguageConfigurationService);
        if (r is not null)
        {
            if (r.IndentAction == IndentAction.None)
            {
                // Nothing special
                return TypeEditOperations.TypeCommand(range, "\n" + config.NormalizeIndentation(r.Indentation + r.AppendText), keepPosition);
            }
            else if (r.IndentAction == IndentAction.Indent)
            {
                // Indent once
                return TypeEditOperations.TypeCommand(range, "\n" + config.NormalizeIndentation(r.Indentation + r.AppendText), keepPosition);
            }
            else if (r.IndentAction == IndentAction.IndentOutdent)
            {
                // Ultra special
                var normalIndent = config.NormalizeIndentation(r.Indentation);
                var increasedIndent = config.NormalizeIndentation(r.Indentation + r.AppendText);
                var typeText = "\n" + increasedIndent + "\n" + normalIndent;
                if (keepPosition)
                {
                    return new ReplaceCommandWithoutChangingPosition(range, typeText, true);
                }
                else
                {
                    return new ReplaceCommandWithOffsetCursorState(range, typeText, -1, increasedIndent.Length - normalIndent.Length, true);
                }
            }
            else if (r.IndentAction == IndentAction.Outdent)
            {
                var actualIndentation = TypeEditOperations.UnshiftIndent(config, r.Indentation);
                return TypeEditOperations.TypeCommand(range, "\n" + config.NormalizeIndentation(actualIndentation + r.AppendText), keepPosition);
            }
        }

        var line = model.GetLineContent(range.StartLineNumber);
        var lineIndentation = JsString.Substring(AutoIndent.GetLeadingWhitespace(line), 0, range.StartColumn - 1);

        if (config.AutoIndent >= EditorAutoIndentStrategy.Full)
        {
            var ir = AutoIndent.GetIndentForEnter(config.AutoIndent, model, range, new IndentConverter(config), config.LanguageConfigurationService);

            if (ir is not null)
            {
                var oldEndViewColumn = config.VisibleColumnFromColumn(model, range.End);
                var oldEndColumn = range.EndColumn;
                var newLineContent = model.GetLineContent(range.EndLineNumber);
                var firstNonWhitespace = Strings.FirstNonWhitespaceIndex(newLineContent);
                if (firstNonWhitespace >= 0)
                {
                    range = range.SetEndPosition(range.EndLineNumber, Math.Max(range.EndColumn, firstNonWhitespace + 1));
                }
                else
                {
                    range = range.SetEndPosition(range.EndLineNumber, model.GetLineMaxColumn(range.EndLineNumber));
                }
                if (keepPosition)
                {
                    return new ReplaceCommandWithoutChangingPosition(range, "\n" + config.NormalizeIndentation(ir.AfterEnter), true);
                }
                else
                {
                    var offset = 0;
                    if (oldEndColumn <= firstNonWhitespace + 1)
                    {
                        if (!config.InsertSpaces)
                        {
                            oldEndViewColumn = (int)Math.Ceiling(oldEndViewColumn / (double)config.IndentSize);
                        }
                        offset = Math.Min(oldEndViewColumn + 1 - config.NormalizeIndentation(ir.AfterEnter).Length - 1, 0);
                    }
                    return new ReplaceCommandWithOffsetCursorState(range, "\n" + config.NormalizeIndentation(ir.AfterEnter), 0, offset, true);
                }
            }
        }
        return TypeEditOperations.TypeCommand(range, "\n" + config.NormalizeIndentation(lineIndentation), keepPosition);
    }

    /// <summary>Inserts a line above each cursor (Ctrl+Shift+Enter).</summary>
    public static ICommand?[] LineInsertBefore(CursorConfiguration config, TextModel? model, IReadOnlyList<TextSelection>? selections)
    {
        if (model is null || selections is null)
        {
            return [];
        }
        var commands = new ICommand?[selections.Count];
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var lineNumber = selections[i].PositionLineNumber;
            if (lineNumber == 1)
            {
                commands[i] = new ReplaceCommandWithoutChangingPosition(new TextRange(1, 1, 1, 1), "\n");
            }
            else
            {
                lineNumber--;
                var column = model.GetLineMaxColumn(lineNumber);

                commands[i] = Enter(config, model, false, new TextRange(lineNumber, column, lineNumber, column));
            }
        }
        return commands;
    }

    /// <summary>Inserts a line below each cursor (Ctrl+Enter).</summary>
    public static ICommand?[] LineInsertAfter(CursorConfiguration config, TextModel? model, IReadOnlyList<TextSelection>? selections)
    {
        if (model is null || selections is null)
        {
            return [];
        }
        var commands = new ICommand?[selections.Count];
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var lineNumber = selections[i].PositionLineNumber;
            var column = model.GetLineMaxColumn(lineNumber);
            commands[i] = Enter(config, model, false, new TextRange(lineNumber, column, lineNumber, column));
        }
        return commands;
    }

    /// <summary>Breaks the line at each cursor and keeps the cursor where it is.</summary>
    public static ICommand?[] LineBreakInsert(CursorConfiguration config, TextModel model, IReadOnlyList<TextSelection> selections)
    {
        var commands = new ICommand?[selections.Count];
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            commands[i] = Enter(config, model, true, selections[i]);
        }
        return commands;
    }
}

/// <summary>Pasting text: as a block, one line per cursor, or the whole text at every cursor.</summary>
internal static class PasteOperation
{
    public static EditOperationResult GetEdits(
        CursorConfiguration config,
        ICursorSimpleModel model,
        IReadOnlyList<TextSelection> selections,
        string text,
        bool pasteOnNewLine,
        IReadOnlyList<string> multicursorText,
        bool isBlock = false)
    {
        var distributeBlockToCursor = isBlock && config.MultiCursorPaste == MultiCursorPaste.Spread && selections.Count == 1;
        if (distributeBlockToCursor)
        {
            return BlockPaste(config, selections[0], text);
        }
        var distributedPaste = DistributePasteToCursors(config, selections, text, pasteOnNewLine, multicursorText);
        if (distributedPaste is not null)
        {
            var sortedSelections = selections.ToList();
            // A stable sort, as JavaScript's
            sortedSelections = sortedSelections
                .Select((selection, index) => (selection, index))
                .OrderBy(x => x.selection, Comparer<TextSelection>.Create(static (a, b) => TextRange.CompareRangesUsingStarts(a, b)))
                .ThenBy(x => x.index)
                .Select(x => x.selection)
                .ToList();
            return DistributedPaste(config, model, sortedSelections, distributedPaste);
        }
        else
        {
            return SimplePaste(config, model, selections, text, pasteOnNewLine);
        }
    }

    private static EditOperationResult BlockPaste(CursorConfiguration config, TextSelection selection, string text)
    {
        var lines = PieceTreeStrings.SplitLines(text);
        var shouldOvertypeOnPaste = config.OvertypeOnPaste && config.InputMode == EditorInputMode.Overtype;
        var command = new ColumnSelectionPasteCommand(selection, lines, config.TabSize, shouldOvertypeOnPaste);
        return new EditOperationResult(EditOperationType.Other, [command], shouldPushStackElementBefore: true, shouldPushStackElementAfter: true);
    }

    private static IReadOnlyList<string>? DistributePasteToCursors(
        CursorConfiguration config,
        IReadOnlyList<TextSelection> selections,
        string text,
        bool pasteOnNewLine,
        IReadOnlyList<string> multicursorText)
    {
        if (selections.Count == 1)
        {
            return null;
        }
        if (multicursorText is not null && multicursorText.Count == selections.Count)
        {
            return multicursorText;
        }
        if (pasteOnNewLine)
        {
            return null;
        }
        if (config.MultiCursorPaste == MultiCursorPaste.Spread)
        {
            // Try to spread the pasted text in case the line count matches the cursor count
            // Remove trailing \n if present
            if (text.Length > 0 && text[^1] == '\n')
            {
                text = text.Substring(0, text.Length - 1);
            }
            // Remove trailing \r if present
            if (text.Length > 0 && text[^1] == '\r')
            {
                text = text.Substring(0, text.Length - 1);
            }
            var lines = PieceTreeStrings.SplitLines(text);
            if (lines.Length == selections.Count)
            {
                return lines;
            }
        }
        return null;
    }

    private static EditOperationResult DistributedPaste(
        CursorConfiguration config,
        ICursorSimpleModel model,
        IReadOnlyList<TextSelection> selections,
        IReadOnlyList<string> text)
    {
        var commands = new ICommand?[selections.Count];
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var shouldOvertypeOnPaste = config.OvertypeOnPaste && config.InputMode == EditorInputMode.Overtype;
            commands[i] = shouldOvertypeOnPaste ? new ReplaceOvertypeCommand(selections[i], text[i]) : new ReplaceCommand(selections[i], text[i]);
        }
        return new EditOperationResult(EditOperationType.Other, commands, shouldPushStackElementBefore: true, shouldPushStackElementAfter: true);
    }

    private static EditOperationResult SimplePaste(
        CursorConfiguration config,
        ICursorSimpleModel model,
        IReadOnlyList<TextSelection> selections,
        string text,
        bool pasteOnNewLine)
    {
        var commands = new ICommand?[selections.Count];
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var selection = selections[i];
            var position = selection.Position;
            if (pasteOnNewLine && !selection.IsEmpty)
            {
                pasteOnNewLine = false;
            }
            if (pasteOnNewLine && text.IndexOf('\n', StringComparison.Ordinal) != text.Length - 1)
            {
                pasteOnNewLine = false;
            }
            if (pasteOnNewLine)
            {
                // Paste entire line at the beginning of line
                var typeSelection = new TextRange(position.LineNumber, 1, position.LineNumber, 1);
                commands[i] = new ReplaceCommandThatPreservesSelection(typeSelection, text, selection, true);
            }
            else
            {
                var shouldOvertypeOnPaste = config.OvertypeOnPaste && config.InputMode == EditorInputMode.Overtype;
                commands[i] = shouldOvertypeOnPaste ? new ReplaceOvertypeCommand(selection, text) : new ReplaceCommand(selection, text);
            }
        }
        return new EditOperationResult(EditOperationType.Other, commands, shouldPushStackElementBefore: true, shouldPushStackElementAfter: true);
    }
}

/// <summary>An input method composition step: replaces characters around each cursor with the composed text.</summary>
internal static class CompositionOperation
{
    public static EditOperationResult GetEdits(
        EditOperationType prevEditOperationType,
        CursorConfiguration config,
        TextModel model,
        IReadOnlyList<TextSelection> selections,
        string text,
        int replacePrevCharCnt,
        int replaceNextCharCnt,
        int positionDelta)
    {
        var commands = selections.Select(selection => CompositionType(model, selection, text, replacePrevCharCnt, replaceNextCharCnt, positionDelta)).ToList();
        return new EditOperationResult(
            EditOperationType.TypingOther,
            commands,
            shouldPushStackElementBefore: TypeEditOperations.ShouldPushStackElementBetween(prevEditOperationType, EditOperationType.TypingOther),
            shouldPushStackElementAfter: false);
    }

    private static ICommand? CompositionType(
        TextModel model,
        TextSelection selection,
        string text,
        int replacePrevCharCnt,
        int replaceNextCharCnt,
        int positionDelta)
    {
        if (!selection.IsEmpty)
        {
            // looks like https://github.com/microsoft/vscode/issues/2773
            // where a cursor operation occurred before a canceled composition
            // => ignore composition
            return null;
        }
        var pos = selection.Position;
        var startColumn = Math.Max(1, pos.Column - replacePrevCharCnt);
        var endColumn = Math.Min(model.GetLineMaxColumn(pos.LineNumber), pos.Column + replaceNextCharCnt);
        var range = new TextRange(pos.LineNumber, startColumn, pos.LineNumber, endColumn);
        return new ReplaceCommandWithOffsetCursorState(range, text, 0, positionDelta);
    }
}

/// <summary>Types text as is, without auto-closing, auto-indentation or electric characters.</summary>
internal static class TypeWithoutInterceptorsOperation
{
    public static EditOperationResult GetEdits(EditOperationType prevEditOperationType, IReadOnlyList<TextSelection> selections, string str)
    {
        var commands = new ICommand?[selections.Count];
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            commands[i] = new ReplaceCommand(selections[i], str);
        }
        var opType = TypeEditOperations.GetTypingOperation(str, prevEditOperationType);
        return new EditOperationResult(
            opType,
            commands,
            shouldPushStackElementBefore: TypeEditOperations.ShouldPushStackElementBetween(prevEditOperationType, opType),
            shouldPushStackElementAfter: false);
    }
}

/// <summary>Pressing Tab: indents to the right spot on a blank line, inserts whitespace, or indents the selected lines.</summary>
internal static class TabOperation
{
    public static ICommand?[] GetCommands(CursorConfiguration config, TextModel model, IReadOnlyList<TextSelection> selections)
    {
        var commands = new ICommand?[selections.Count];
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var selection = selections[i];
            if (selection.IsEmpty)
            {
                var lineText = model.GetLineContent(selection.StartLineNumber);
                if (JsString.IsWhitespaceOnly(lineText) && model.Tokenization.IsCheapToTokenize(selection.StartLineNumber))
                {
                    var goodIndent = GoodIndentForLine(config, model, selection.StartLineNumber);
                    if (string.IsNullOrEmpty(goodIndent))
                    {
                        goodIndent = "\t";
                    }
                    var possibleTypeText = config.NormalizeIndentation(goodIndent);
                    if (!lineText.StartsWith(possibleTypeText, StringComparison.Ordinal))
                    {
                        commands[i] = new ReplaceCommand(new TextRange(selection.StartLineNumber, 1, selection.StartLineNumber, lineText.Length + 1), possibleTypeText, true);
                        continue;
                    }
                }
                commands[i] = ReplaceJumpToNextIndent(config, model, selection, true);
            }
            else
            {
                if (selection.StartLineNumber == selection.EndLineNumber)
                {
                    var lineMaxColumn = model.GetLineMaxColumn(selection.StartLineNumber);
                    if (selection.StartColumn != 1 || selection.EndColumn != lineMaxColumn)
                    {
                        // This is a single line selection that is not the entire line
                        commands[i] = ReplaceJumpToNextIndent(config, model, selection, false);
                        continue;
                    }
                }
                commands[i] = new ShiftCommand(
                    selection,
                    new ShiftCommandOpts(
                        IsUnshift: false,
                        TabSize: config.TabSize,
                        IndentSize: config.IndentSize,
                        InsertSpaces: config.InsertSpaces,
                        UseTabStops: config.UseTabStops,
                        AutoIndent: config.AutoIndent),
                    config.LanguageConfigurationService);
            }
        }
        return commands;
    }

    private static string? GoodIndentForLine(CursorConfiguration config, TextModel model, int lineNumber)
    {
        IndentAction? action = null;
        var indentation = "";
        var expectedIndentAction = AutoIndent.GetInheritIndentForLine(config.AutoIndent, model, lineNumber, false, config.LanguageConfigurationService);
        if (expectedIndentAction is not null)
        {
            action = expectedIndentAction.Action;
            indentation = expectedIndentAction.Indentation;
        }
        else if (lineNumber > 1)
        {
            int lastLineNumber;
            for (lastLineNumber = lineNumber - 1; lastLineNumber >= 1; lastLineNumber--)
            {
                var lineText = model.GetLineContent(lastLineNumber);
                var nonWhitespaceIdx = Strings.LastNonWhitespaceIndex(lineText);
                if (nonWhitespaceIdx >= 0)
                {
                    break;
                }
            }
            if (lastLineNumber < 1)
            {
                // No previous line with content found
                return null;
            }
            var maxColumn = model.GetLineMaxColumn(lastLineNumber);
            var expectedEnterAction = EnterActions.GetEnterAction(
                config.AutoIndent, model, new TextRange(lastLineNumber, maxColumn, lastLineNumber, maxColumn), config.LanguageConfigurationService);
            if (expectedEnterAction is not null)
            {
                indentation = expectedEnterAction.Indentation + expectedEnterAction.AppendText;
            }
        }
        // `if (action)`: IndentAction.None is 0 and falsy upstream
        if (action is { } a && a != IndentAction.None)
        {
            if (a == IndentAction.Indent)
            {
                indentation = TypeEditOperations.ShiftIndent(config, indentation);
            }
            if (a == IndentAction.Outdent)
            {
                indentation = TypeEditOperations.UnshiftIndent(config, indentation);
            }
            indentation = config.NormalizeIndentation(indentation);
        }
        if (string.IsNullOrEmpty(indentation))
        {
            return null;
        }
        return indentation;
    }

    private static ReplaceCommand ReplaceJumpToNextIndent(CursorConfiguration config, ICursorSimpleModel model, TextSelection selection, bool insertsAutoWhitespace)
    {
        var typeText = "";
        var position = selection.Range.Start;
        if (config.InsertSpaces)
        {
            var visibleColumnFromColumn = config.VisibleColumnFromColumn(model, position);
            var indentSize = config.IndentSize;
            var spacesCnt = indentSize - (visibleColumnFromColumn % indentSize);
            for (var i = 0; i < spacesCnt; i++)
            {
                typeText += " ";
            }
        }
        else
        {
            typeText = "\t";
        }
        return new ReplaceCommand(selection, typeText, insertsAutoWhitespace);
    }
}
