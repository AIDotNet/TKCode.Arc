// Adaptation of vscode's word commands (src/vs/editor/contrib/wordOperations/browser/wordOperations.ts and
// contrib/wordPartOperations/browser/wordPartOperations.ts, commit b57d5c9e), MIT license, for the tests: what each
// command's runEditorCommand does, on an OperationsTestEditor instead of a code editor.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;

namespace TKCode.Arc.Editor.Tests;

internal static class WordCommands
{
    // MoveWordCommand
    private static void MoveWord(
        OperationsTestEditor editor,
        bool inSelectionMode,
        Func<WordCharacterClassifier, OperationsTestEditor, TextPosition, bool, TextPosition> move,
        string? wordSeparators = null)
    {
        var classifier = WordCharacterClassifier.GetMapForWordSeparators(wordSeparators ?? editor.Options.WordSeparators);
        var selections = editor.GetSelections();
        var hasMulticursor = selections.Count > 1;
        var result = selections.Select(sel =>
        {
            var inPosition = new TextPosition(sel.PositionLineNumber, sel.PositionColumn);
            var outPosition = move(classifier, editor, inPosition, hasMulticursor);
            return inSelectionMode
                ? new TextSelection(sel.SelectionStartLineNumber, sel.SelectionStartColumn, outPosition.LineNumber, outPosition.Column)
                : new TextSelection(outPosition.LineNumber, outPosition.Column, outPosition.LineNumber, outPosition.Column);
        }).ToList();

        editor.Model.PushStackElement();
        editor.SetSelections(result);
    }

    private static void WordLeft(OperationsTestEditor editor, bool inSelectionMode, WordNavigationType type, string? wordSeparators = null) =>
        MoveWord(editor, inSelectionMode, (ws, ed, pos, multi) => WordOperations.MoveWordLeft(ws, ed.Model, pos, type, multi), wordSeparators);

    private static void WordRight(OperationsTestEditor editor, bool inSelectionMode, WordNavigationType type, string? wordSeparators = null) =>
        MoveWord(editor, inSelectionMode, (ws, ed, pos, _) => WordOperations.MoveWordRight(ws, ed.Model, pos, type), wordSeparators);

    /// <summary>cursorWordLeft / cursorWordLeftSelect.</summary>
    public static void CursorWordLeft(OperationsTestEditor editor, bool inSelectionMode = false) =>
        WordLeft(editor, inSelectionMode, WordNavigationType.WordStartFast);

    /// <summary>cursorWordStartLeft / cursorWordStartLeftSelect.</summary>
    public static void CursorWordStartLeft(OperationsTestEditor editor, bool inSelectionMode = false) =>
        WordLeft(editor, inSelectionMode, WordNavigationType.WordStart);

    /// <summary>cursorWordEndLeft / cursorWordEndLeftSelect.</summary>
    public static void CursorWordEndLeft(OperationsTestEditor editor, bool inSelectionMode = false) =>
        WordLeft(editor, inSelectionMode, WordNavigationType.WordEnd);

    /// <summary>cursorWordAccessibilityLeft / cursorWordAccessibilityLeftSelect: always the default word separators.</summary>
    public static void CursorWordAccessibilityLeft(OperationsTestEditor editor, bool inSelectionMode = false) =>
        WordLeft(editor, inSelectionMode, WordNavigationType.WordAccessibility, EditorOptions.UsualWordSeparators);

    /// <summary>cursorWordRight / cursorWordRightSelect.</summary>
    public static void CursorWordRight(OperationsTestEditor editor, bool inSelectionMode = false) =>
        WordRight(editor, inSelectionMode, WordNavigationType.WordEnd);

    /// <summary>cursorWordEndRight / cursorWordEndRightSelect.</summary>
    public static void MoveWordEndRight(OperationsTestEditor editor, bool inSelectionMode = false) =>
        WordRight(editor, inSelectionMode, WordNavigationType.WordEnd);

    /// <summary>cursorWordStartRight / cursorWordStartRightSelect.</summary>
    public static void MoveWordStartRight(OperationsTestEditor editor, bool inSelectionMode = false) =>
        WordRight(editor, inSelectionMode, WordNavigationType.WordStart);

    /// <summary>cursorWordAccessibilityRight / cursorWordAccessibilityRightSelect: always the default word separators.</summary>
    public static void CursorWordAccessibilityRight(OperationsTestEditor editor, bool inSelectionMode = false) =>
        WordRight(editor, inSelectionMode, WordNavigationType.WordAccessibility, EditorOptions.UsualWordSeparators);

    /// <summary>cursorWordPartLeft / cursorWordPartLeftSelect.</summary>
    public static void CursorWordPartLeft(OperationsTestEditor editor, bool inSelectionMode = false) =>
        MoveWord(editor, inSelectionMode, (ws, ed, pos, multi) => WordPartOperations.MoveWordPartLeft(ws, ed.Model, pos, multi));

    /// <summary>cursorWordPartRight / cursorWordPartRightSelect.</summary>
    public static void CursorWordPartRight(OperationsTestEditor editor, bool inSelectionMode = false) =>
        MoveWord(editor, inSelectionMode, (ws, ed, pos, _) => WordPartOperations.MoveWordPartRight(ws, ed.Model, pos));

    // DeleteWordCommand
    private static void DeleteWord(
        OperationsTestEditor editor,
        bool whitespaceHeuristics,
        Func<DeleteWordContext, TextRange> delete,
        IReadOnlyList<TextRange>? autoClosedCharacters = null)
    {
        var classifier = WordCharacterClassifier.GetMapForWordSeparators(editor.Options.WordSeparators);
        var model = editor.Model;
        var autoClosingPairs = editor.LanguageConfigurationService.GetLanguageConfiguration(model.GetLanguageId()).GetAutoClosingPairs();

        var commands = editor.GetSelections().Select(sel =>
        {
            var deleteRange = delete(new DeleteWordContext(
                WordSeparators: classifier,
                Model: model,
                Selection: sel,
                WhitespaceHeuristics: whitespaceHeuristics,
                AutoClosingDelete: editor.Options.AutoClosingDelete,
                AutoClosingBrackets: editor.Options.AutoClosingBrackets,
                AutoClosingQuotes: editor.Options.AutoClosingQuotes,
                AutoClosingPairs: autoClosingPairs,
                AutoClosedCharacters: autoClosedCharacters ?? []));
            return (ICommand?)new ReplaceCommand(deleteRange, "");
        }).ToList();

        model.PushStackElement();
        editor.ExecuteCommands(commands);
        model.PushStackElement();
    }

    // DeleteWordLeftCommand._delete
    private static TextRange DeleteLeft(TextRange? r) => r ?? new TextRange(1, 1, 1, 1);

    // DeleteWordRightCommand._delete
    private static TextRange DeleteRight(DeleteWordContext ctx, TextRange? r)
    {
        if (r is { } range)
        {
            return range;
        }
        var lineCount = ctx.Model.GetLineCount();
        var maxColumn = ctx.Model.GetLineMaxColumn(lineCount);
        return new TextRange(lineCount, maxColumn, lineCount, maxColumn);
    }

    /// <summary>deleteWordLeft.</summary>
    public static void DeleteWordLeft(OperationsTestEditor editor) =>
        DeleteWord(editor, true, ctx => DeleteLeft(WordOperations.DeleteWordLeft(ctx, WordNavigationType.WordStart)));

    /// <summary>deleteWordStartLeft.</summary>
    public static void DeleteWordStartLeft(OperationsTestEditor editor) =>
        DeleteWord(editor, false, ctx => DeleteLeft(WordOperations.DeleteWordLeft(ctx, WordNavigationType.WordStart)));

    /// <summary>deleteWordEndLeft.</summary>
    public static void DeleteWordEndLeft(OperationsTestEditor editor) =>
        DeleteWord(editor, false, ctx => DeleteLeft(WordOperations.DeleteWordLeft(ctx, WordNavigationType.WordEnd)));

    /// <summary>deleteWordRight.</summary>
    public static void DeleteWordRight(OperationsTestEditor editor) =>
        DeleteWord(editor, true, ctx => DeleteRight(ctx, WordOperations.DeleteWordRight(ctx, WordNavigationType.WordEnd)));

    /// <summary>deleteWordStartRight.</summary>
    public static void DeleteWordStartRight(OperationsTestEditor editor) =>
        DeleteWord(editor, false, ctx => DeleteRight(ctx, WordOperations.DeleteWordRight(ctx, WordNavigationType.WordStart)));

    /// <summary>deleteWordEndRight.</summary>
    public static void DeleteWordEndRight(OperationsTestEditor editor) =>
        DeleteWord(editor, false, ctx => DeleteRight(ctx, WordOperations.DeleteWordRight(ctx, WordNavigationType.WordEnd)));

    /// <summary>deleteWordPartLeft.</summary>
    public static void DeleteWordPartLeft(OperationsTestEditor editor) =>
        DeleteWord(editor, true, ctx => DeleteLeft(WordPartOperations.DeleteWordPartLeft(ctx)));

    /// <summary>deleteWordPartRight.</summary>
    public static void DeleteWordPartRight(OperationsTestEditor editor) =>
        DeleteWord(editor, true, ctx => DeleteRight(ctx, WordPartOperations.DeleteWordPartRight(ctx)));

    /// <summary>deleteInsideWord (DeleteInsideWord.run).</summary>
    public static void DeleteInsideWord(OperationsTestEditor editor, bool onlyWord = false)
    {
        var classifier = WordCharacterClassifier.GetMapForWordSeparators(editor.Options.WordSeparators);
        var model = editor.Model;
        var commands = editor.GetSelections()
            .Select(sel => (ICommand?)new ReplaceCommand(WordOperations.DeleteInsideWord(classifier, model, sel, onlyWord), ""))
            .ToList();

        model.PushStackElement();
        editor.ExecuteCommands(commands);
        model.PushStackElement();
    }
}
