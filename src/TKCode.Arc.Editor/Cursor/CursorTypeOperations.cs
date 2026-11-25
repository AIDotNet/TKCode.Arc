// Ported from vscode (src/vs/editor/common/cursor/cursorTypeOperations.ts, commit b57d5c9e), MIT license.
//
// Differences from upstream:
//  - the commands are ICommand?[] and the selections IReadOnlyList<TextSelection>; "no edit" is null;
//  - the shift command options are built by one helper (upstream repeats the literal in indent and outdent);
//  - CompositionOutcome is a sealed record with upstream's members;
//  - /^[ \t]+$/ is a loop over the deleted text.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Cursor;

/// <summary>Typing, pasting, composing, Tab, indent and outdent: the edits they make at each cursor.</summary>
internal static class TypeOperations
{
    public static ICommand?[] Indent(CursorConfiguration config, ICursorSimpleModel? model, IReadOnlyList<TextSelection>? selections)
    {
        if (model is null || selections is null)
        {
            return [];
        }

        var commands = new ICommand?[selections.Count];
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            commands[i] = new ShiftCommand(selections[i], CreateShiftCommandOpts(config, isUnshift: false), config.LanguageConfigurationService);
        }
        return commands;
    }

    public static ICommand?[] Outdent(CursorConfiguration config, ICursorSimpleModel model, IReadOnlyList<TextSelection> selections)
    {
        var commands = new ICommand?[selections.Count];
        for (int i = 0, len = selections.Count; i < len; i++)
        {
            commands[i] = new ShiftCommand(selections[i], CreateShiftCommandOpts(config, isUnshift: true), config.LanguageConfigurationService);
        }
        return commands;
    }

    public static string ShiftIndent(CursorConfiguration config, string indentation, int? count = null) =>
        TypeEditOperations.ShiftIndent(config, indentation, count);

    public static string UnshiftIndent(CursorConfiguration config, string indentation, int? count = null) =>
        TypeEditOperations.UnshiftIndent(config, indentation, count);

    public static EditOperationResult Paste(
        CursorConfiguration config,
        ICursorSimpleModel model,
        IReadOnlyList<TextSelection> selections,
        string text,
        bool pasteOnNewLine,
        IReadOnlyList<string> multicursorText,
        bool isBlock = false) =>
        PasteOperation.GetEdits(config, model, selections, text, pasteOnNewLine, multicursorText, isBlock);

    public static ICommand?[] Tab(CursorConfiguration config, TextModel model, IReadOnlyList<TextSelection> selections) =>
        TabOperation.GetCommands(config, model, selections);

    public static EditOperationResult CompositionType(
        EditOperationType prevEditOperationType,
        CursorConfiguration config,
        TextModel model,
        IReadOnlyList<TextSelection> selections,
        string text,
        int replacePrevCharCnt,
        int replaceNextCharCnt,
        int positionDelta) =>
        CompositionOperation.GetEdits(prevEditOperationType, config, model, selections, text, replacePrevCharCnt, replaceNextCharCnt, positionDelta);

    /// <summary>
    /// This is very similar with typing, but the character is already in the text buffer!
    /// </summary>
    public static EditOperationResult? CompositionEndWithInterceptors(
        EditOperationType prevEditOperationType,
        CursorConfiguration config,
        TextModel model,
        IReadOnlyList<CompositionOutcome>? compositions,
        IReadOnlyList<TextSelection> selections,
        IReadOnlyList<TextRange> autoClosedCharacters)
    {
        if (compositions is null)
        {
            // could not deduce what the composition did
            return null;
        }

        string? insertedText = null;
        foreach (var composition in compositions)
        {
            if (insertedText is null)
            {
                insertedText = composition.InsertedText;
            }
            else if (insertedText != composition.InsertedText)
            {
                // not all selections agree on what was typed
                return null;
            }
        }

        if (string.IsNullOrEmpty(insertedText) || insertedText.Length != 1)
        {
            // we're only interested in the case where a single character was inserted
            return CompositionEndOvertypeOperation.GetEdits(config, compositions);
        }

        var ch = insertedText;

        var hasDeletion = false;
        foreach (var composition in compositions)
        {
            if (composition.DeletedText.Length != 0)
            {
                hasDeletion = true;
                break;
            }
        }

        if (hasDeletion)
        {
            // Check if this could have been a surround selection

            if (!TypeEditOperations.ShouldSurroundChar(config, ch) || !config.SurroundingPairs.ContainsKey(ch))
            {
                return null;
            }

            var isTypingAQuoteCharacter = TypeEditOperations.IsQuote(ch);

            foreach (var composition in compositions)
            {
                if (composition.DeletedSelectionStart != 0 || composition.DeletedSelectionEnd != composition.DeletedText.Length)
                {
                    // more text was deleted than was selected, so this could not have been a surround selection
                    return null;
                }
                if (IsSpacesOrTabsOnly(composition.DeletedText))
                {
                    // deleted text was only whitespace
                    return null;
                }
                if (isTypingAQuoteCharacter && TypeEditOperations.IsQuote(composition.DeletedText))
                {
                    // deleted text was a quote
                    return null;
                }
            }

            var positions = new List<TextPosition>();
            foreach (var selection in selections)
            {
                if (!selection.IsEmpty)
                {
                    return null;
                }
                positions.Add(selection.Position);
            }

            if (positions.Count != compositions.Count)
            {
                return null;
            }

            var commands = new List<ICommand?>();
            for (int i = 0, len = positions.Count; i < len; i++)
            {
                commands.Add(new CompositionSurroundSelectionCommand(positions[i], compositions[i].DeletedText, config.SurroundingPairs[ch]));
            }
            return new EditOperationResult(EditOperationType.TypingOther, commands, shouldPushStackElementBefore: true, shouldPushStackElementAfter: false);
        }

        var autoClosingOvertypeEdits = AutoClosingOvertypeWithInterceptorsOperation.GetEdits(config, model, selections, autoClosedCharacters, ch);
        if (autoClosingOvertypeEdits is not null)
        {
            return autoClosingOvertypeEdits;
        }

        var autoClosingOpenCharEdits = AutoClosingOpenCharTypeOperation.GetEdits(config, model, selections, ch, true, false);
        if (autoClosingOpenCharEdits is not null)
        {
            return autoClosingOpenCharEdits;
        }

        return CompositionEndOvertypeOperation.GetEdits(config, compositions);
    }

    public static EditOperationResult TypeWithInterceptors(
        bool isDoingComposition,
        EditOperationType prevEditOperationType,
        CursorConfiguration config,
        TextModel model,
        IReadOnlyList<TextSelection> selections,
        IReadOnlyList<TextRange> autoClosedCharacters,
        string ch)
    {
        var enterEdits = EnterOperation.GetEdits(config, model, selections, ch, isDoingComposition);
        if (enterEdits is not null)
        {
            return enterEdits;
        }

        var autoIndentEdits = AutoIndentOperation.GetEdits(config, model, selections, ch, isDoingComposition);
        if (autoIndentEdits is not null)
        {
            return autoIndentEdits;
        }

        var autoClosingOverTypeEdits = AutoClosingOvertypeOperation.GetEdits(prevEditOperationType, config, model, selections, autoClosedCharacters, ch);
        if (autoClosingOverTypeEdits is not null)
        {
            return autoClosingOverTypeEdits;
        }

        var autoClosingOpenCharEdits = AutoClosingOpenCharTypeOperation.GetEdits(config, model, selections, ch, false, isDoingComposition);
        if (autoClosingOpenCharEdits is not null)
        {
            return autoClosingOpenCharEdits;
        }

        var surroundSelectionEdits = SurroundSelectionOperation.GetEdits(config, model, selections, ch, isDoingComposition);
        if (surroundSelectionEdits is not null)
        {
            return surroundSelectionEdits;
        }

        var interceptorElectricCharOperation = InterceptorElectricCharOperation.GetEdits(prevEditOperationType, config, model, selections, ch, isDoingComposition);
        if (interceptorElectricCharOperation is not null)
        {
            return interceptorElectricCharOperation;
        }

        return SimpleCharacterTypeOperation.GetEdits(config, prevEditOperationType, selections, ch, isDoingComposition);
    }

    public static EditOperationResult TypeWithoutInterceptors(
        EditOperationType prevEditOperationType,
        CursorConfiguration config,
        TextModel model,
        IReadOnlyList<TextSelection> selections,
        string str) =>
        TypeWithoutInterceptorsOperation.GetEdits(prevEditOperationType, selections, str);

    private static ShiftCommandOpts CreateShiftCommandOpts(CursorConfiguration config, bool isUnshift) =>
        new(
            IsUnshift: isUnshift,
            TabSize: config.TabSize,
            IndentSize: config.IndentSize,
            InsertSpaces: config.InsertSpaces,
            UseTabStops: config.UseTabStops,
            AutoIndent: config.AutoIndent);

    // /^[ \t]+$/.test(text)
    private static bool IsSpacesOrTabsOnly(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }
        foreach (var c in text)
        {
            if (c != ' ' && c != '\t')
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>What a composition did at one cursor: the text it deleted and the text it inserted.</summary>
internal sealed record CompositionOutcome(
    string DeletedText,
    int DeletedSelectionStart,
    int DeletedSelectionEnd,
    string InsertedText,
    int InsertedSelectionStart,
    int InsertedSelectionEnd,
    TextRange InsertedTextRange);
