// Ported from vscode (src/vs/editor/common/cursor/cursorTypeEditOperations.ts: AutoIndentOperation,
// AutoClosingOvertypeOperation, AutoClosingOvertypeWithInterceptorsOperation, AutoClosingOpenCharTypeOperation,
// CompositionEndOvertypeOperation, SurroundSelectionOperation, InterceptorElectricCharOperation and
// SimpleCharacterTypeOperation, commit b57d5c9e), MIT license.
// EnterOperation, PasteOperation, CompositionOperation, TypeWithoutInterceptorsOperation and TabOperation are in
// CursorTypeEditOperations.Enter.cs; the commands and module functions in CursorTypeEditOperations.Commands.cs.
//
// Differences from upstream:
//  - the operations are static classes; "no edit" (undefined or null upstream) is null;
//  - the auto-closing pair found for a character is the record struct AutoClosingPairOpenClose;
//  - model.bracketPairs is model.GetBracketPairs with the configuration's language configuration service, which is the
//    model's upstream;
//  - the strategies are enums (EditorAutoClosingStrategy.Never for 'never', ...), and the input mode is
//    CursorConfiguration.InputMode;
//  - config.electricChars.hasOwnProperty(ch) is true only for a one-character ch in ElectricChars;
//  - strings are cut with JavaScript's substring semantics (JsString.Substring).

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Cursor;

/// <summary>An auto-closing pair, with the closing characters to insert (they may be shorter than its close).</summary>
internal readonly record struct AutoClosingPairOpenClose(string Open, string Close);

/// <summary>Re-indents the line when a typed character changes what its indentation should be.</summary>
internal static class AutoIndentOperation
{
    public static EditOperationResult? GetEdits(
        CursorConfiguration config,
        TextModel model,
        IReadOnlyList<TextSelection> selections,
        string ch,
        bool isDoingComposition)
    {
        if (!isDoingComposition && IsAutoIndentType(config, model, selections))
        {
            var indentationForSelections = new List<(TextSelection Selection, string Indentation)>();
            foreach (var selection in selections)
            {
                var indentation = FindActualIndentationForSelection(config, model, selection, ch);
                if (indentation is null)
                {
                    // Auto indentation failed
                    return null;
                }
                indentationForSelections.Add((selection, indentation));
            }
            var autoClosingPair = AutoClosingOpenCharTypeOperation.GetAutoClosingPair(config, model, selections, ch, false);
            return GetIndentationAndAutoClosingPairEdits(config, model, indentationForSelections, ch, autoClosingPair);
        }
        return null;
    }

    private static bool IsAutoIndentType(CursorConfiguration config, TextModel model, IReadOnlyList<TextSelection> selections)
    {
        if (config.AutoIndent < EditorAutoIndentStrategy.Full)
        {
            return false;
        }
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            if (!model.Tokenization.IsCheapToTokenize(selections[i].Range.End.LineNumber))
            {
                return false;
            }
        }
        return true;
    }

    private static string? FindActualIndentationForSelection(CursorConfiguration config, TextModel model, TextSelection selection, string ch)
    {
        var actualIndentation = AutoIndent.GetIndentActionForType(
            config, model, selection, ch, new IndentConverter(config), config.LanguageConfigurationService);

        if (actualIndentation is null)
        {
            return null;
        }

        var currentIndentation = LanguageConfigurations.GetIndentationAtPosition(model, selection.StartLineNumber, selection.StartColumn);
        if (actualIndentation == config.NormalizeIndentation(currentIndentation))
        {
            return null;
        }
        return actualIndentation;
    }

    private static EditOperationResult GetIndentationAndAutoClosingPairEdits(
        CursorConfiguration config,
        TextModel model,
        List<(TextSelection Selection, string Indentation)> indentationForSelections,
        string ch,
        AutoClosingPairOpenClose? autoClosingPair)
    {
        var autoCloseWithUndoStop = autoClosingPair is not null && autoClosingPair.Value.Open.Length > ch.Length;
        var commands = new List<ICommand?>(indentationForSelections.Count);
        foreach (var (selection, indentation) in indentationForSelections)
        {
            if (autoClosingPair is { } pair && !autoCloseWithUndoStop)
            {
                // Apply both auto closing pair edits and auto indentation edits
                var indentationEdit = GetEditFromIndentationAndSelection(config, model, indentation, selection, ch, false);
                commands.Add(new TypeWithIndentationAndAutoClosingCommand(indentationEdit, selection, ch, pair.Close));
            }
            else
            {
                // Apply only auto indentation edits
                var indentationEdit = GetEditFromIndentationAndSelection(config, model, indentation, selection, ch, true);
                commands.Add(TypeEditOperations.TypeCommand(indentationEdit.Range, indentationEdit.Text, false));
            }
        }
        if (autoClosingPair is { } closingPair && autoCloseWithUndoStop)
        {
            return AutoClosingOpenCharTypeOperation.TypeAndAutoCloseWithUndoStop(model, commands, ch, closingPair.Close);
        }
        return new EditOperationResult(EditOperationType.TypingOther, commands, shouldPushStackElementBefore: true, shouldPushStackElementAfter: false);
    }

    private static (TextRange Range, string Text) GetEditFromIndentationAndSelection(
        CursorConfiguration config,
        TextModel model,
        string indentation,
        TextSelection selection,
        string ch,
        bool includeChInEdit = true)
    {
        var startLineNumber = selection.StartLineNumber;
        var firstNonWhitespaceColumn = model.GetLineFirstNonWhitespaceColumn(startLineNumber);
        var text = config.NormalizeIndentation(indentation);
        if (firstNonWhitespaceColumn != 0)
        {
            var startLine = model.GetLineContent(startLineNumber);
            text += JsString.Substring(startLine, firstNonWhitespaceColumn - 1, selection.StartColumn - 1);
        }
        text += includeChInEdit ? ch : "";
        var range = new TextRange(startLineNumber, 1, selection.EndLineNumber, selection.EndColumn);
        return (range, text);
    }
}

/// <summary>Types over a closing character that the editor auto-closed.</summary>
internal static class AutoClosingOvertypeOperation
{
    public static EditOperationResult? GetEdits(
        EditOperationType prevEditOperationType,
        CursorConfiguration config,
        TextModel model,
        IReadOnlyList<TextSelection> selections,
        IReadOnlyList<TextRange> autoClosedCharacters,
        string ch)
    {
        if (TypeEditOperations.IsAutoClosingOvertype(config, model, selections, autoClosedCharacters, ch))
        {
            return RunAutoClosingOvertype(prevEditOperationType, selections, ch);
        }
        return null;
    }

    private static EditOperationResult RunAutoClosingOvertype(EditOperationType prevEditOperationType, IReadOnlyList<TextSelection> selections, string ch)
    {
        var commands = new ICommand?[selections.Count];
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var selection = selections[i];
            var position = selection.Position;
            var typeSelection = new TextRange(position.LineNumber, position.Column, position.LineNumber, position.Column + 1);
            commands[i] = new ReplaceCommand(typeSelection, ch);
        }
        return new EditOperationResult(
            EditOperationType.TypingOther,
            commands,
            shouldPushStackElementBefore: TypeEditOperations.ShouldPushStackElementBetween(prevEditOperationType, EditOperationType.TypingOther),
            shouldPushStackElementAfter: false);
    }
}

/// <summary>At the end of a composition: removes the closing character the composed one typed over.</summary>
internal static class AutoClosingOvertypeWithInterceptorsOperation
{
    public static EditOperationResult? GetEdits(
        CursorConfiguration config,
        TextModel model,
        IReadOnlyList<TextSelection> selections,
        IReadOnlyList<TextRange> autoClosedCharacters,
        string ch)
    {
        if (TypeEditOperations.IsAutoClosingOvertype(config, model, selections, autoClosedCharacters, ch))
        {
            // Unfortunately, the close character is at this point "doubled", so we need to delete it...
            var commands = selections
                .Select(s => (ICommand?)new ReplaceCommand(new TextRange(s.PositionLineNumber, s.PositionColumn, s.PositionLineNumber, s.PositionColumn + 1), "", false))
                .ToList();
            return new EditOperationResult(EditOperationType.TypingOther, commands, shouldPushStackElementBefore: true, shouldPushStackElementAfter: false);
        }
        return null;
    }
}

/// <summary>Types an opening character and auto-closes it.</summary>
internal static class AutoClosingOpenCharTypeOperation
{
    public static EditOperationResult? GetEdits(
        CursorConfiguration config,
        TextModel model,
        IReadOnlyList<TextSelection> selections,
        string ch,
        bool chIsAlreadyTyped,
        bool isDoingComposition)
    {
        if (!isDoingComposition)
        {
            var autoClosingPair = GetAutoClosingPair(config, model, selections, ch, chIsAlreadyTyped);
            if (autoClosingPair is { } pair)
            {
                return RunAutoClosingOpenCharType(model, selections, ch, chIsAlreadyTyped, pair);
            }
        }
        return null;
    }

    private static EditOperationResult RunAutoClosingOpenCharType(
        TextModel model,
        IReadOnlyList<TextSelection> selections,
        string ch,
        bool chIsAlreadyTyped,
        AutoClosingPairOpenClose autoClosingPair)
    {
        if (!chIsAlreadyTyped && autoClosingPair.Open.Length > ch.Length)
        {
            return TypeAndAutoCloseWithUndoStop(
                model,
                selections.Select(selection => (ICommand?)TypeEditOperations.TypeCommand(selection, ch, false)).ToList(),
                ch,
                autoClosingPair.Close);
        }
        var commands = new ICommand?[selections.Count];
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var selection = selections[i];
            commands[i] = new TypeWithAutoClosingCommand(selection, ch, !chIsAlreadyTyped, autoClosingPair.Close);
        }
        return new EditOperationResult(EditOperationType.TypingOther, commands, shouldPushStackElementBefore: true, shouldPushStackElementAfter: false);
    }

    /// <summary>
    /// Executes <paramref name="typeCommands"/>, which type <paramref name="ch"/>, and then inserts
    /// <paramref name="autoClosingPairClose"/> after <paramref name="ch"/> with an undo stop in between.
    /// This way, undo first removes only the auto-closed characters and keeps a typed multi-character opening pair
    /// (e.g. <c>begin</c>).
    /// </summary>
    public static EditOperationResult TypeAndAutoCloseWithUndoStop(
        TextModel model,
        IReadOnlyList<ICommand?> typeCommands,
        string ch,
        string autoClosingPairClose)
    {
        var expectedVersionId = model.GetVersionId() + 1;
        return new EditOperationResult(
            EditOperationType.TypingOther,
            typeCommands,
            shouldPushStackElementBefore: true,
            shouldPushStackElementAfter: true,
            followUp: selections =>
            {
                var canAutoClose = model.GetVersionId() == expectedVersionId
                    && selections.Count == typeCommands.Count
                    && selections.All(s =>
                        s.IsEmpty
                        && s.PositionColumn > ch.Length
                        && model.GetValueInRange(new TextRange(s.PositionLineNumber, s.PositionColumn - ch.Length, s.PositionLineNumber, s.PositionColumn)) == ch);
                if (!canAutoClose)
                {
                    return null;
                }
                return new EditOperationResult(
                    EditOperationType.TypingOther,
                    selections.Select(selection => (ICommand?)new TypeWithAutoClosingCommand(selection, ch, false, autoClosingPairClose)).ToList(),
                    shouldPushStackElementBefore: false,
                    shouldPushStackElementAfter: false);
            });
    }

    /// <summary>Returns the auto-closing pair whose opening ends with <paramref name="ch"/>, together with the closing characters to insert.</summary>
    public static AutoClosingPairOpenClose? GetAutoClosingPair(
        CursorConfiguration config,
        TextModel model,
        IReadOnlyList<TextSelection> selections,
        string ch,
        bool chIsAlreadyTyped)
    {
        foreach (var selection in selections)
        {
            if (!selection.IsEmpty)
            {
                return null;
            }
        }
        // This method is called both when typing (regularly) and when composition ends
        // This means that we need to work with a text buffer where sometimes `ch` is not
        // there (it is being typed right now) or with a text buffer where `ch` has already been typed
        //
        // In order to avoid adding checks for `chIsAlreadyTyped` in all places, we will work
        // with two conceptual positions, the position before `ch` and the position after `ch`
        //
        var positions = selections.Select(s =>
        {
            var position = s.Position;
            if (chIsAlreadyTyped)
            {
                return (LineNumber: position.LineNumber, BeforeColumn: position.Column - ch.Length, AfterColumn: position.Column);
            }
            else
            {
                return (LineNumber: position.LineNumber, BeforeColumn: position.Column, AfterColumn: position.Column);
            }
        }).ToList();
        // Find the longest auto-closing open pair in case of multiple ending in `ch`
        // e.g. when having [f","] and [","], it picks [f","] if the character before is f
        var pair = FindAutoClosingPairOpen(config, model, positions.Select(p => new TextPosition(p.LineNumber, p.BeforeColumn)).ToList(), ch);
        if (pair is null)
        {
            return null;
        }
        EditorAutoClosingStrategy autoCloseConfig;
        Func<char, bool> shouldAutoCloseBefore;
        var shouldCheckBracketBalance = false;

        var chIsQuote = TypeEditOperations.IsQuote(ch);
        if (chIsQuote)
        {
            autoCloseConfig = config.AutoClosingQuotes;
            shouldAutoCloseBefore = config.ShouldAutoCloseBefore.Quote;
        }
        else
        {
            var pairIsForComments = !string.IsNullOrEmpty(config.BlockCommentStartToken) && pair.Open.Contains(config.BlockCommentStartToken, StringComparison.Ordinal);
            if (pairIsForComments)
            {
                autoCloseConfig = config.AutoClosingComments;
                shouldAutoCloseBefore = config.ShouldAutoCloseBefore.Comment;
            }
            else
            {
                autoCloseConfig = config.AutoClosingBrackets;
                shouldAutoCloseBefore = config.ShouldAutoCloseBefore.Bracket;
                shouldCheckBracketBalance = true;
            }
        }
        if (autoCloseConfig == EditorAutoClosingStrategy.Never)
        {
            return null;
        }
        // Sometimes, it is possible to have two auto-closing pairs that have a containment relationship
        // e.g. when having [(,)] and [(*,*)]
        // - when typing (, the resulting state is (|)
        // - when typing *, the desired resulting state is (*|*), not (*|*))
        var containedPair = FindContainedAutoClosingPair(config, pair);
        var containedPairClose = containedPair is not null ? containedPair.Close : "";
        var isContainedPairPresent = true;

        foreach (var (lineNumber, beforeColumn, afterColumn) in positions)
        {
            var lineText = model.GetLineContent(lineNumber);
            var lineBefore = JsString.Substring(lineText, 0, beforeColumn - 1);
            var lineAfter = JsString.Substring(lineText, afterColumn - 1);

            if (!lineAfter.StartsWith(containedPairClose, StringComparison.Ordinal))
            {
                isContainedPairPresent = false;
            }
            // Only consider auto closing the pair if an allowed character follows or if another autoclosed pair closing brace follows
            if (lineAfter.Length > 0)
            {
                var characterAfter = lineAfter[0];
                var isBeforeCloseBrace = IsBeforeClosingBrace(config, lineAfter);
                if (!isBeforeCloseBrace && !shouldAutoCloseBefore(characterAfter))
                {
                    return null;
                }
            }
            if (
                shouldCheckBracketBalance
                // When 'always', always insert the closing bracket
                && autoCloseConfig != EditorAutoClosingStrategy.Always
                // Need to check character is not already typed so brackets are still imbalanced
                && !chIsAlreadyTyped
                && model.GetBracketPairs(config.LanguageConfigurationService).HasUnmatchedClosingBracketAfter(new TextPosition(lineNumber, beforeColumn), pair.Open))
            {
                return null;
            }
            // Do not auto-close ' or " after a word character
            if (pair.Open.Length == 1 && (ch == "'" || ch == "\"") && autoCloseConfig != EditorAutoClosingStrategy.Always)
            {
                var wordSeparators = WordCharacterClassifier.GetMapForWordSeparators(config.WordSeparators);
                if (lineBefore.Length > 0)
                {
                    var characterBefore = lineBefore[^1];
                    if (wordSeparators.Get(characterBefore) == WordCharacterClass.Regular)
                    {
                        return null;
                    }
                }
            }
            if (!model.Tokenization.IsCheapToTokenize(lineNumber))
            {
                // Do not force tokenization
                return null;
            }
            model.Tokenization.ForceTokenization(lineNumber);
            var lineTokens = model.Tokenization.GetLineTokens(lineNumber);
            var scopedLineTokens = Supports.CreateScopedLineTokens(lineTokens, beforeColumn - 1);
            if (!pair.ShouldAutoClose(scopedLineTokens, beforeColumn - scopedLineTokens.FirstCharOffset))
            {
                return null;
            }
            // Typing for example a quote could either start a new string, in which case auto-closing is desirable
            // or it could end a previously started string, in which case auto-closing is not desirable
            //
            // In certain cases, it is really not possible to look at the previous token to determine
            // what would happen. That's why we do something really unusual, we pretend to type a different
            // character and ask the tokenizer what the outcome of doing that is: after typing a neutral
            // character, are we in a string (i.e. the quote would most likely end a string) or not?
            //
            var neutralCharacter = pair.FindNeutralCharacter();
            if (!string.IsNullOrEmpty(neutralCharacter))
            {
                var tokenType = model.Tokenization.GetTokenTypeIfInsertingCharacter(lineNumber, beforeColumn, neutralCharacter);
                if (!pair.IsOK(tokenType))
                {
                    return null;
                }
            }
        }
        if (isContainedPairPresent)
        {
            return new AutoClosingPairOpenClose(pair.Open, pair.Close.Substring(0, pair.Close.Length - containedPairClose.Length));
        }
        else
        {
            return new AutoClosingPairOpenClose(pair.Open, pair.Close);
        }
    }

    /// <summary>
    /// Find another auto-closing pair that is contained by the one passed in.
    ///
    /// e.g. when having [(,)] and [(*,*)] as auto-closing pairs
    /// this method will find [(,)] as a containment pair for [(*,*)]
    /// </summary>
    private static StandardAutoClosingPairConditional? FindContainedAutoClosingPair(CursorConfiguration config, StandardAutoClosingPairConditional pair)
    {
        if (pair.Open.Length <= 1)
        {
            return null;
        }
        var lastChar = JsString.CharAt(pair.Close, pair.Close.Length - 1);
        // get candidates with the same last character as close
        var candidates = config.AutoClosingPairs.AutoClosingPairsCloseByEnd.GetValueOrDefault(lastChar) ?? [];
        StandardAutoClosingPairConditional? result = null;
        foreach (var candidate in candidates)
        {
            if (candidate.Open != pair.Open
                && pair.Open.Contains(candidate.Open, StringComparison.Ordinal)
                && pair.Close.EndsWith(candidate.Close, StringComparison.Ordinal))
            {
                if (result is null || candidate.Open.Length > result.Open.Length)
                {
                    result = candidate;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Determine if typing <paramref name="ch"/> at all <paramref name="positions"/> in the <paramref name="model"/>
    /// results in an auto closing open sequence being typed.
    ///
    /// Auto closing open sequences can consist of multiple characters, which
    /// can lead to ambiguities. In such a case, the longest auto-closing open
    /// sequence is returned.
    /// </summary>
    private static StandardAutoClosingPairConditional? FindAutoClosingPairOpen(
        CursorConfiguration config,
        TextModel model,
        IReadOnlyList<TextPosition> positions,
        string ch)
    {
        if (!config.AutoClosingPairs.AutoClosingPairsOpenByEnd.TryGetValue(ch, out var candidates))
        {
            return null;
        }
        // Determine which auto-closing pair it is
        StandardAutoClosingPairConditional? result = null;
        foreach (var candidate in candidates)
        {
            if (result is null || candidate.Open.Length > result.Open.Length)
            {
                var candidateIsMatch = true;
                foreach (var position in positions)
                {
                    var relevantText = model.GetValueInRange(new TextRange(position.LineNumber, position.Column - candidate.Open.Length + 1, position.LineNumber, position.Column));
                    if (relevantText + ch != candidate.Open)
                    {
                        candidateIsMatch = false;
                        break;
                    }
                    if (candidate.BeforeText is not null)
                    {
                        var prefix = model.GetValueInRange(new TextRange(position.LineNumber, 1, position.LineNumber, position.Column));
                        if (!candidate.ShouldAutoCloseBefore(prefix + ch))
                        {
                            candidateIsMatch = false;
                            break;
                        }
                    }
                }
                if (candidateIsMatch)
                {
                    result = candidate;
                }
            }
        }
        return result;
    }

    private static bool IsBeforeClosingBrace(CursorConfiguration config, string lineAfter)
    {
        // If the start of lineAfter can be interpretted as both a starting or ending brace, default to returning false
        var nextChar = JsString.CharAt(lineAfter, 0);
        var potentialStartingBraces = config.AutoClosingPairs.AutoClosingPairsOpenByStart.GetValueOrDefault(nextChar) ?? [];
        var potentialClosingBraces = config.AutoClosingPairs.AutoClosingPairsCloseByStart.GetValueOrDefault(nextChar) ?? [];

        var isBeforeStartingBrace = potentialStartingBraces.Any(x => lineAfter.StartsWith(x.Open, StringComparison.Ordinal));
        var isBeforeClosingBrace = potentialClosingBraces.Any(x => lineAfter.StartsWith(x.Close, StringComparison.Ordinal));

        return !isBeforeStartingBrace && isBeforeClosingBrace;
    }
}

/// <summary>At the end of a composition in overtype mode: types over as many characters as were composed.</summary>
internal static class CompositionEndOvertypeOperation
{
    public static EditOperationResult? GetEdits(CursorConfiguration config, IReadOnlyList<CompositionOutcome> compositions)
    {
        var isOvertypeMode = config.InputMode == EditorInputMode.Overtype;
        if (!isOvertypeMode)
        {
            return null;
        }
        var commands = compositions.Select(composition => (ICommand?)new ReplaceOvertypeCommandOnCompositionEnd(composition.InsertedTextRange)).ToList();
        return new EditOperationResult(EditOperationType.TypingOther, commands, shouldPushStackElementBefore: true, shouldPushStackElementAfter: false);
    }
}

/// <summary>Wraps the selections in a surrounding pair when its opening character is typed.</summary>
internal static class SurroundSelectionOperation
{
    public static EditOperationResult? GetEdits(
        CursorConfiguration config,
        TextModel model,
        IReadOnlyList<TextSelection> selections,
        string ch,
        bool isDoingComposition)
    {
        if (!isDoingComposition && IsSurroundSelectionType(config, model, selections, ch))
        {
            return RunSurroundSelectionType(config, selections, ch);
        }
        return null;
    }

    private static EditOperationResult RunSurroundSelectionType(CursorConfiguration config, IReadOnlyList<TextSelection> selections, string ch)
    {
        var commands = new ICommand?[selections.Count];
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var selection = selections[i];
            var closeCharacter = config.SurroundingPairs[ch];
            commands[i] = new SurroundSelectionCommand(selection, ch, closeCharacter);
        }
        return new EditOperationResult(EditOperationType.Other, commands, shouldPushStackElementBefore: true, shouldPushStackElementAfter: true);
    }

    private static bool IsSurroundSelectionType(CursorConfiguration config, TextModel model, IReadOnlyList<TextSelection> selections, string ch)
    {
        if (!TypeEditOperations.ShouldSurroundChar(config, ch) || !config.SurroundingPairs.ContainsKey(ch))
        {
            return false;
        }
        var isTypingAQuoteCharacter = TypeEditOperations.IsQuote(ch);
        foreach (var selection in selections)
        {
            if (selection.IsEmpty)
            {
                return false;
            }
            var selectionContainsOnlyWhitespace = true;
            for (var lineNumber = selection.StartLineNumber; lineNumber <= selection.EndLineNumber; lineNumber++)
            {
                var lineText = model.GetLineContent(lineNumber);
                var startIndex = lineNumber == selection.StartLineNumber ? selection.StartColumn - 1 : 0;
                var endIndex = lineNumber == selection.EndLineNumber ? selection.EndColumn - 1 : lineText.Length;
                var selectedText = JsString.Substring(lineText, startIndex, endIndex);
                if (ContainsNonSpaceOrTab(selectedText))
                {
                    // this selected text contains something other than whitespace
                    selectionContainsOnlyWhitespace = false;
                    break;
                }
            }
            if (selectionContainsOnlyWhitespace)
            {
                return false;
            }
            if (isTypingAQuoteCharacter && selection.StartLineNumber == selection.EndLineNumber && selection.StartColumn + 1 == selection.EndColumn)
            {
                var selectionText = model.GetValueInRange(selection);
                if (TypeEditOperations.IsQuote(selectionText))
                {
                    // Typing a quote character on top of another quote character
                    // => disable surround selection type
                    return false;
                }
            }
        }
        return true;
    }

    // /[^ \t]/.test(text)
    internal static bool ContainsNonSpaceOrTab(string text)
    {
        foreach (var c in text)
        {
            if (c != ' ' && c != '\t')
            {
                return true;
            }
        }
        return false;
    }
}

/// <summary>Re-indents a line to its matching bracket when an electric character (a closing bracket) is typed.</summary>
internal static class InterceptorElectricCharOperation
{
    public static EditOperationResult? GetEdits(
        EditOperationType prevEditOperationType,
        CursorConfiguration config,
        TextModel model,
        IReadOnlyList<TextSelection> selections,
        string ch,
        bool isDoingComposition)
    {
        // Electric characters make sense only when dealing with a single cursor,
        // as multiple cursors typing brackets for example would interfer with bracket matching
        if (!isDoingComposition && IsTypeInterceptorElectricChar(config, model, selections))
        {
            var r = TypeInterceptorElectricChar(prevEditOperationType, config, model, selections[0], ch);
            if (r is not null)
            {
                return r;
            }
        }
        return null;
    }

    private static bool IsTypeInterceptorElectricChar(CursorConfiguration config, TextModel model, IReadOnlyList<TextSelection> selections)
    {
        if (selections.Count == 1 && model.Tokenization.IsCheapToTokenize(selections[0].Range.End.LineNumber))
        {
            return true;
        }
        return false;
    }

    private static EditOperationResult? TypeInterceptorElectricChar(
        EditOperationType prevEditOperationType,
        CursorConfiguration config,
        TextModel model,
        TextSelection selection,
        string ch)
    {
        if (ch.Length != 1 || !config.ElectricChars.Contains(ch[0]) || !selection.IsEmpty)
        {
            return null;
        }
        var position = selection.Position;
        model.Tokenization.ForceTokenization(position.LineNumber);
        var lineTokens = model.Tokenization.GetLineTokens(position.LineNumber);
        ElectricAction? electricAction;
        try
        {
            electricAction = config.OnElectricCharacter(ch, lineTokens, position.Column);
        }
        catch (Exception e)
        {
            UnexpectedErrors.Report(e);
            return null;
        }
        if (electricAction is null)
        {
            return null;
        }
        if (!string.IsNullOrEmpty(electricAction.MatchOpenBracket))
        {
            var endColumn = (lineTokens.GetLineContent() + ch).LastIndexOf(electricAction.MatchOpenBracket, StringComparison.Ordinal) + 1;
            var match = model.GetBracketPairs(config.LanguageConfigurationService).FindMatchingBracketUp(
                electricAction.MatchOpenBracket,
                new TextPosition(position.LineNumber, endColumn));
            if (match is { } matchRange)
            {
                if (matchRange.StartLineNumber == position.LineNumber)
                {
                    // matched something on the same line => no change in indentation
                    return null;
                }
                var matchLine = model.GetLineContent(matchRange.StartLineNumber);
                var matchLineIndentation = AutoIndent.GetLeadingWhitespace(matchLine);
                var newIndentation = config.NormalizeIndentation(matchLineIndentation);
                var lineText = model.GetLineContent(position.LineNumber);
                var firstNonWhitespaceColumn = model.GetLineFirstNonWhitespaceColumn(position.LineNumber);
                var lineFirstNonBlankColumn = firstNonWhitespaceColumn != 0 ? firstNonWhitespaceColumn : position.Column;
                var prefix = JsString.Substring(lineText, lineFirstNonBlankColumn - 1, position.Column - 1);
                var typeText = newIndentation + prefix + ch;
                var typeSelection = new TextRange(position.LineNumber, 1, position.LineNumber, position.Column);
                var command = new ReplaceCommand(typeSelection, typeText);
                return new EditOperationResult(
                    TypeEditOperations.GetTypingOperation(typeText, prevEditOperationType),
                    [command],
                    shouldPushStackElementBefore: false,
                    shouldPushStackElementAfter: true);
            }
        }
        return null;
    }
}

/// <summary>Types a character: inserts it, or types over the next one in overtype mode.</summary>
internal static class SimpleCharacterTypeOperation
{
    public static EditOperationResult GetEdits(
        CursorConfiguration config,
        EditOperationType prevEditOperationType,
        IReadOnlyList<TextSelection> selections,
        string ch,
        bool isDoingComposition)
    {
        // A simple character type
        var commands = new ICommand?[selections.Count];
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            var overtype = config.InputMode == EditorInputMode.Overtype && !isDoingComposition;
            commands[i] = overtype ? new ReplaceOvertypeCommand(selections[i], ch) : new ReplaceCommand(selections[i], ch);
        }

        var opType = TypeEditOperations.GetTypingOperation(ch, prevEditOperationType);
        return new EditOperationResult(
            opType,
            commands,
            shouldPushStackElementBefore: TypeEditOperations.ShouldPushStackElementBetween(prevEditOperationType, opType),
            shouldPushStackElementAfter: false);
    }
}
