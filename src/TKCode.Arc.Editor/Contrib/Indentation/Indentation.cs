// Ported from vscode (src/vs/editor/contrib/indentation/common/indentation.ts and browser/indentation.ts, commit
// b57d5c9e), MIT license: getReindentEditOperations, IndentationToSpacesCommand, IndentationToTabsCommand,
// AutoIndentOnPasteCommand, the run methods of the indentation actions and AutoIndentOnPaste.trigger.
// Differences from upstream:
//  - the actions' run methods are static methods on an IEditorActionHost;
//  - ChangeIndentationSizeAction (indent using spaces / tabs, change tab display size) opens a quick pick upstream;
//    here the tab size is the action's argument (an int, or a string of one), and without one the model's tab size
//    stays (only insertSpaces changes);
//  - DetectIndentation detects with the default creation options (upstream asks the model service for the
//    configured ones);
//  - AutoIndentOnPaste.trigger is AutoIndentOnPaste.Trigger with the editor's options as parameters
//    (the control passes autoIndentOnPasteWithinString, true by default as upstream).

using System.Text.RegularExpressions;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Contrib.LinesOperations;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Tokens;

namespace TKCode.Arc.Editor.Contrib.Indentation;

/// <summary>The reindent functions and the indentation actions.</summary>
internal static class IndentationOperations
{
    /// <summary>getReindentEditOperations: the edits that reindent the lines by the language's indentation rules.</summary>
    public static List<SingleEditOperation> GetReindentEditOperations(TextModel model, LanguageConfigurationService languageConfigurationService, int startLineNumber, int endLineNumber)
    {
        if (model.GetLineCount() == 1 && model.GetLineMaxColumn(1) == 1)
        {
            // Model is empty
            return [];
        }

        var indentationRulesSupport = languageConfigurationService.GetLanguageConfiguration(model.GetLanguageId()).IndentRulesSupport;
        if (indentationRulesSupport is null)
        {
            return [];
        }

        var processedIndentRulesSupport = new ProcessedIndentRulesSupport(model, indentationRulesSupport, languageConfigurationService);
        endLineNumber = Math.Min(endLineNumber, model.GetLineCount());

        // Skip `unIndentedLinePattern` lines
        while (startLineNumber <= endLineNumber)
        {
            if (!processedIndentRulesSupport.ShouldIgnore(startLineNumber))
            {
                break;
            }

            startLineNumber++;
        }

        if (startLineNumber > endLineNumber - 1)
        {
            return [];
        }

        var options = model.GetOptions();
        var tabSize = options.TabSize;
        var indentSize = options.IndentSize;
        var insertSpaces = options.InsertSpaces;
        string ShiftIndent(string indentation) =>
            ShiftCommand.ShiftIndent(indentation, indentation.Length + 1, tabSize, indentSize, insertSpaces);
        string UnshiftIndent(string indentation) =>
            ShiftCommand.UnshiftIndent(indentation, indentation.Length + 1, tabSize, indentSize, insertSpaces);
        var indentEdits = new List<SingleEditOperation>();

        // indentation being passed to lines below

        // Calculate indentation for the first line
        // If there is no passed-in indentation, we use the indentation of the first line as base.
        var currentLineText = model.GetLineContent(startLineNumber);
        var globalIndent = AutoIndent.GetLeadingWhitespace(currentLineText);
        // idealIndentForNextLine doesn't equal globalIndent when there is a line matching `indentNextLinePattern`.
        var idealIndentForNextLine = globalIndent;

        if (processedIndentRulesSupport.ShouldIncrease(startLineNumber))
        {
            idealIndentForNextLine = ShiftIndent(idealIndentForNextLine);
            globalIndent = ShiftIndent(globalIndent);
        }
        else if (processedIndentRulesSupport.ShouldIndentNextLine(startLineNumber))
        {
            idealIndentForNextLine = ShiftIndent(idealIndentForNextLine);
        }

        startLineNumber++;

        // Calculate indentation adjustment for all following lines
        for (var lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
        {
            if (IsLineStartInsideString(model, lineNumber))
            {
                continue;
            }
            var text = model.GetLineContent(lineNumber);
            var oldIndentation = AutoIndent.GetLeadingWhitespace(text);
            var currentIdealIndent = idealIndentForNextLine;

            if (processedIndentRulesSupport.ShouldDecrease(lineNumber, currentIdealIndent))
            {
                idealIndentForNextLine = UnshiftIndent(idealIndentForNextLine);
                globalIndent = UnshiftIndent(globalIndent);
            }

            if (oldIndentation != idealIndentForNextLine)
            {
                indentEdits.Add(EditOperation.ReplaceMove(new TextSelection(lineNumber, 1, lineNumber, oldIndentation.Length + 1), global::TKCode.Arc.Editor.Indentation.NormalizeIndentation(idealIndentForNextLine, indentSize, insertSpaces)));
            }

            // calculate idealIndentForNextLine
            if (processedIndentRulesSupport.ShouldIgnore(lineNumber))
            {
                // In reindent phase, if the line matches `unIndentedLinePattern` we inherit indentation from above lines
                // but don't change globalIndent and idealIndentForNextLine.
                continue;
            }
            else if (processedIndentRulesSupport.ShouldIncrease(lineNumber, currentIdealIndent))
            {
                globalIndent = ShiftIndent(globalIndent);
                idealIndentForNextLine = globalIndent;
            }
            else if (processedIndentRulesSupport.ShouldIndentNextLine(lineNumber, currentIdealIndent))
            {
                idealIndentForNextLine = ShiftIndent(idealIndentForNextLine);
            }
            else
            {
                idealIndentForNextLine = globalIndent;
            }
        }

        return indentEdits;
    }

    private static bool IsLineStartInsideString(TextModel model, int lineNumber)
    {
        if (!model.Tokenization.IsCheapToTokenize(lineNumber))
        {
            return false;
        }
        model.Tokenization.TokenizeIfCheap(lineNumber);
        var lineTokens = model.Tokenization.GetLineTokens(lineNumber);
        var line = model.GetLineContent(lineNumber);
        var startsWithString = lineTokens.GetStandardTokenType(0) == StandardTokenType.String;
        if (line.Length > 0 && !startsWithString)
        {
            return false;
        }
        if (startsWithString && AutoIndent.GetLeadingWhitespace(line).Length > 0)
        {
            return true;
        }
        // Tokenize inserted whitespace to distinguish a new string from a continuation, including on empty lines.
        var tokensWithIndent = model.Tokenization.TokenizeLinesAt(lineNumber, [" " + line]);
        return tokensWithIndent is { Count: > 0 } && tokensWithIndent[0].GetStandardTokenType(0) == StandardTokenType.String;
    }

    /// <summary>IndentationToSpacesAction.run.</summary>
    public static void IndentationToSpaces(IEditorActionHost editor, string id)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }
        var modelOpts = model.GetOptions();
        if (editor.GetSelection() is not { } selection)
        {
            return;
        }
        var command = new IndentationToSpacesCommand(selection, modelOpts.TabSize);

        editor.PushUndoStop();
        editor.ExecuteCommands(id, [command]);
        editor.PushUndoStop();

        model.UpdateOptions(new TextModelUpdateOptions { InsertSpaces = true });
    }

    /// <summary>IndentationToTabsAction.run.</summary>
    public static void IndentationToTabs(IEditorActionHost editor, string id)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }
        var modelOpts = model.GetOptions();
        if (editor.GetSelection() is not { } selection)
        {
            return;
        }
        var command = new IndentationToTabsCommand(selection, modelOpts.TabSize);

        editor.PushUndoStop();
        editor.ExecuteCommands(id, [command]);
        editor.PushUndoStop();

        model.UpdateOptions(new TextModelUpdateOptions { InsertSpaces = false });
    }

    /// <summary>
    /// ChangeIndentationSizeAction.run after the pick: indent using spaces / tabs (<paramref name="displaySizeOnly"/>
    /// false) or change the tab display size (true). <paramref name="tabSize"/> null keeps the model's tab size.
    /// </summary>
    public static void ChangeIndentationSize(IEditorActionHost editor, bool insertSpaces, bool displaySizeOnly, int? tabSize)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }

        var pickedVal = tabSize is { } size ? Math.Clamp(size, 1, 8) : model.GetOptions().TabSize;
        if (displaySizeOnly)
        {
            model.UpdateOptions(new TextModelUpdateOptions { TabSize = pickedVal });
        }
        else
        {
            model.UpdateOptions(new TextModelUpdateOptions { TabSize = pickedVal, IndentSize = pickedVal, InsertSpaces = insertSpaces });
        }
    }

    /// <summary>The tab size an indentation action's argument gives: an int, or a string of one.</summary>
    public static int? ParseTabSizeArg(object? args) => args switch
    {
        int i => i,
        long l => (int)l,
        string s when int.TryParse(s, out var parsed) => parsed,
        System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Number } e when e.TryGetInt32(out var n) => n,
        _ => null,
    };

    /// <summary>DetectIndentation.run.</summary>
    public static void DetectIndentation(IEditorActionHost editor)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }

        var creationOpts = TextModel.DefaultCreationOptions;
        model.DetectIndentation(creationOpts.InsertSpaces, creationOpts.TabSize);
    }

    /// <summary>ReindentLinesAction.run.</summary>
    public static void ReindentLines(IEditorActionHost editor, string id)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }
        var edits = GetReindentEditOperations(model, editor.LanguageConfigurationService, 1, model.GetLineCount());
        if (edits.Count > 0)
        {
            editor.PushUndoStop();
            editor.ExecuteEdits(id, edits, null);
            editor.PushUndoStop();
        }
    }

    /// <summary>ReindentSelectedLinesAction.run.</summary>
    public static void ReindentSelectedLines(IEditorActionHost editor, string id)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }

        var selections = editor.GetSelections();
        var edits = new List<SingleEditOperation>();

        foreach (var selection in selections)
        {
            var startLineNumber = selection.StartLineNumber;
            var endLineNumber = selection.EndLineNumber;

            if (startLineNumber != endLineNumber && selection.EndColumn == 1)
            {
                endLineNumber--;
            }

            if (startLineNumber == 1)
            {
                if (startLineNumber == endLineNumber)
                {
                    continue;
                }
            }
            else
            {
                startLineNumber--;
            }

            var editOperations = GetReindentEditOperations(model, editor.LanguageConfigurationService, startLineNumber, endLineNumber);
            edits.AddRange(editOperations);
        }

        if (edits.Count > 0)
        {
            editor.PushUndoStop();
            editor.ExecuteEdits(id, edits, null);
            editor.PushUndoStop();
        }
    }

    // getIndentationEditOperations
    internal static void GetIndentationEditOperations(TextModel model, IEditOperationBuilder builder, int tabSize, bool tabsToSpaces)
    {
        if (model.GetLineCount() == 1 && model.GetLineMaxColumn(1) == 1)
        {
            // Model is empty
            return;
        }

        var spaces = new string(' ', tabSize);
        var spacesRegExp = new Regex(Regex.Escape(spaces), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        for (int lineNumber = 1, lineCount = model.GetLineCount(); lineNumber <= lineCount; lineNumber++)
        {
            var lastIndentationColumn = model.GetLineFirstNonWhitespaceColumn(lineNumber);
            if (lastIndentationColumn == 0)
            {
                lastIndentationColumn = model.GetLineMaxColumn(lineNumber);
            }

            if (lastIndentationColumn == 1)
            {
                continue;
            }

            var originalIndentationRange = new TextRange(lineNumber, 1, lineNumber, lastIndentationColumn);
            var originalIndentation = model.GetValueInRange(originalIndentationRange);
            var newIndentation = tabsToSpaces
                ? originalIndentation.Replace("\t", spaces, StringComparison.Ordinal)
                : spacesRegExp.Replace(originalIndentation, "\t");

            builder.AddEditOperation(originalIndentationRange, newIndentation);
        }
    }
}

/// <summary>Converts the tab indentation of every line to spaces.</summary>
internal sealed class IndentationToSpacesCommand(TextSelection selection, int tabSize) : ICommand
{
    private string? _selectionId;

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        _selectionId = builder.TrackSelection(selection);
        IndentationOperations.GetIndentationEditOperations(model, builder, tabSize, true);
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper) => helper.GetTrackedSelection(_selectionId!);
}

/// <summary>Converts the space indentation of every line to tabs.</summary>
internal sealed class IndentationToTabsCommand(TextSelection selection, int tabSize) : ICommand
{
    private string? _selectionId;

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        _selectionId = builder.TrackSelection(selection);
        IndentationOperations.GetIndentationEditOperations(model, builder, tabSize, false);
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper) => helper.GetTrackedSelection(_selectionId!);
}

/// <summary>Applies the indentation edits after a paste, keeping the cursor next to the pasted text.</summary>
internal sealed class AutoIndentOnPasteCommand : ICommand
{
    private readonly List<(TextRange Range, string Text)> _edits;
    private readonly TextSelection _initialSelection;
    private string? _selectionId;

    public AutoIndentOnPasteCommand(IReadOnlyList<(TextRange Range, string Text)> edits, TextSelection initialSelection)
    {
        _initialSelection = initialSelection;
        _edits = [.. edits];
        _selectionId = null;
    }

    public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
    {
        foreach (var edit in _edits)
        {
            builder.AddEditOperation(edit.Range, edit.Text);
        }

        var selectionIsSet = false;
        if (_edits.Count == 1 && _initialSelection.IsEmpty)
        {
            if (_edits[0].Range.StartColumn == _initialSelection.EndColumn &&
                _edits[0].Range.StartLineNumber == _initialSelection.EndLineNumber)
            {
                selectionIsSet = true;
                _selectionId = builder.TrackSelection(_initialSelection, true);
            }
            else if (_edits[0].Range.EndColumn == _initialSelection.StartColumn &&
                _edits[0].Range.EndLineNumber == _initialSelection.StartLineNumber)
            {
                selectionIsSet = true;
                _selectionId = builder.TrackSelection(_initialSelection, false);
            }
        }

        if (!selectionIsSet)
        {
            _selectionId = builder.TrackSelection(_initialSelection);
        }
    }

    public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper) => helper.GetTrackedSelection(_selectionId!);
}

/// <summary>Reindents pasted lines (upstream's AutoIndentOnPaste contribution, editor.autoIndentOnPaste).</summary>
internal static class AutoIndentOnPaste
{
    // /\S/
    private static readonly Regex s_nonWhitespace = new(@"\S", RegexOptions.CultureInvariant);

    /// <summary>AutoIndentOnPaste.trigger: adjusts the indentation of the lines a paste put in <paramref name="range"/>.</summary>
    public static void Trigger(IEditorActionHost editor, TextRange range, bool autoIndentOnPasteWithinString = true)
    {
        var selections = editor.GetSelections();
        if (selections.Count == 0 || selections.Count > 1)
        {
            return;
        }

        if (editor.GetModel() is not { } model)
        {
            return;
        }
        var containsOnlyWhitespace = RangeContainsOnlyWhitespaceCharacters(model, range);
        if (containsOnlyWhitespace)
        {
            return;
        }
        if (!autoIndentOnPasteWithinString && IsStartOrEndInString(model, range))
        {
            return;
        }
        if (!model.Tokenization.IsCheapToTokenize(range.StartLineNumber))
        {
            return;
        }
        var autoIndent = editor.GetOptions().AutoIndent;
        var options = model.GetOptions();
        var tabSize = options.TabSize;
        var insertSpaces = options.InsertSpaces;
        var textEdits = new List<(TextRange Range, string Text)>();

        var indentConverter = new MoveLinesCommand.IndentConverter(tabSize, options.IndentSize, insertSpaces);

        var startLineNumber = range.StartLineNumber;

        var firstLineText = model.GetLineContent(startLineNumber);
        if (!s_nonWhitespace.IsMatch(BlockCommentSubstring(firstLineText, range.StartColumn - 1)))
        {
            var indentOfFirstLine = AutoIndent.GetGoodIndentForLine(autoIndent, new TextModelVirtualModel(model), model.GetLanguageId(), startLineNumber, indentConverter, editor.LanguageConfigurationService);

            if (indentOfFirstLine is not null)
            {
                var oldIndentation = AutoIndent.GetLeadingWhitespace(firstLineText);
                var newSpaceCnt = IndentUtils.GetSpaceCnt(indentOfFirstLine, tabSize);
                var oldSpaceCnt = IndentUtils.GetSpaceCnt(oldIndentation, tabSize);

                if (newSpaceCnt != oldSpaceCnt)
                {
                    var newIndent = IndentUtils.GenerateIndent(newSpaceCnt, tabSize, insertSpaces);
                    textEdits.Add((new TextRange(startLineNumber, 1, startLineNumber, oldIndentation.Length + 1), newIndent));
                    firstLineText = newIndent + firstLineText.Substring(oldIndentation.Length);
                }
                else
                {
                    var indentMetadata = AutoIndent.GetIndentMetadata(model, startLineNumber, editor.LanguageConfigurationService);

                    if (indentMetadata == 0 || indentMetadata == IndentConsts.UnindentMask)
                    {
                        // we paste content into a line where only contains whitespaces
                        // after pasting, the indentation of the first line is already correct
                        // the first line doesn't match any indentation rule
                        // then no-op.
                        return;
                    }
                }
            }
        }

        var firstLineNumber = startLineNumber;

        // ignore empty or ignored lines
        while (startLineNumber < range.EndLineNumber)
        {
            if (!s_nonWhitespace.IsMatch(model.GetLineContent(startLineNumber + 1)))
            {
                startLineNumber++;
                continue;
            }
            break;
        }

        if (startLineNumber != range.EndLineNumber)
        {
            var text = firstLineText;
            var virtualModel = new MoveLinesCommand.MappedVirtualModel(
                model,
                static lineNumber => lineNumber,
                lineNumber => lineNumber == firstLineNumber ? text : null);
            var indentOfSecondLine = AutoIndent.GetGoodIndentForLine(autoIndent, virtualModel, model.GetLanguageId(), startLineNumber + 1, indentConverter, editor.LanguageConfigurationService);
            if (indentOfSecondLine is not null)
            {
                var newSpaceCntOfSecondLine = IndentUtils.GetSpaceCnt(indentOfSecondLine, tabSize);
                var oldSpaceCntOfSecondLine = IndentUtils.GetSpaceCnt(AutoIndent.GetLeadingWhitespace(model.GetLineContent(startLineNumber + 1)), tabSize);

                if (newSpaceCntOfSecondLine != oldSpaceCntOfSecondLine)
                {
                    var spaceCntOffset = newSpaceCntOfSecondLine - oldSpaceCntOfSecondLine;
                    for (var i = startLineNumber + 1; i <= range.EndLineNumber; i++)
                    {
                        var lineContent = model.GetLineContent(i);
                        var originalIndent = AutoIndent.GetLeadingWhitespace(lineContent);
                        var originalSpacesCnt = IndentUtils.GetSpaceCnt(originalIndent, tabSize);
                        var newSpacesCnt = originalSpacesCnt + spaceCntOffset;
                        var newIndent = IndentUtils.GenerateIndent(newSpacesCnt, tabSize, insertSpaces);

                        if (newIndent != originalIndent)
                        {
                            textEdits.Add((new TextRange(i, 1, i, originalIndent.Length + 1), newIndent));
                        }
                    }
                }
            }
        }

        if (textEdits.Count > 0 && editor.GetSelection() is { } selection)
        {
            editor.PushUndoStop();
            var cmd = new AutoIndentOnPasteCommand(textEdits, selection);
            editor.ExecuteCommands("autoIndentOnPaste", [cmd]);
            editor.PushUndoStop();
        }
    }

    // firstLineText.substring(0, end)
    private static string BlockCommentSubstring(string text, int end) => text.Substring(0, Math.Clamp(end, 0, text.Length));

    private static bool RangeContainsOnlyWhitespaceCharacters(TextModel model, TextRange range)
    {
        static bool LineContainsOnlyWhitespace(string content) => content.Trim().Length == 0;

        var containsOnlyWhitespace = true;
        if (range.StartLineNumber == range.EndLineNumber)
        {
            var lineContent = model.GetLineContent(range.StartLineNumber);
            var linePart = Substring(lineContent, range.StartColumn - 1, range.EndColumn - 1);
            containsOnlyWhitespace = LineContainsOnlyWhitespace(linePart);
        }
        else
        {
            for (var i = range.StartLineNumber; i <= range.EndLineNumber; i++)
            {
                var lineContent = model.GetLineContent(i);
                if (i == range.StartLineNumber)
                {
                    var linePart = Substring(lineContent, range.StartColumn - 1, lineContent.Length);
                    containsOnlyWhitespace = LineContainsOnlyWhitespace(linePart);
                }
                else if (i == range.EndLineNumber)
                {
                    var linePart = Substring(lineContent, 0, range.EndColumn - 1);
                    containsOnlyWhitespace = LineContainsOnlyWhitespace(linePart);
                }
                else
                {
                    containsOnlyWhitespace = model.GetLineFirstNonWhitespaceColumn(i) == 0;
                }
                if (!containsOnlyWhitespace)
                {
                    break;
                }
            }
        }
        return containsOnlyWhitespace;
    }

    private static string Substring(string text, int start, int end)
    {
        start = Math.Clamp(start, 0, text.Length);
        end = Math.Clamp(end, 0, text.Length);
        return start >= end ? "" : text.Substring(start, end - start);
    }

    private static bool IsStartOrEndInString(TextModel model, TextRange range) =>
        IsPositionInString(model, range.Start) || IsPositionInString(model, range.End);

    // getStandardTokenTypeAtPosition (src/vs/editor/common/tokens/lineTokens.ts) == String
    private static bool IsPositionInString(TextModel model, TextPosition position)
    {
        var lineNumber = position.LineNumber;
        if (!model.Tokenization.IsCheapToTokenize(lineNumber))
        {
            return false;
        }
        model.Tokenization.ForceTokenization(lineNumber);
        var lineTokens = model.Tokenization.GetLineTokens(lineNumber);
        var tokenIndex = lineTokens.FindTokenIndexAtOffset(position.Column - 1);
        return lineTokens.GetStandardTokenType(tokenIndex) == StandardTokenType.String;
    }
}
