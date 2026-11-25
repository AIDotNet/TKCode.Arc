// Ported from vscode (src/vs/editor/contrib/wordOperations/browser/wordOperations.ts and
// src/vs/editor/contrib/wordPartOperations/browser/wordPartOperations.ts, commit b57d5c9e), MIT license: the
// runEditorCommand of MoveWordCommand, DeleteWordCommand and DeleteInsideWord, and the commands built on them.
// Differences from upstream:
//  - the commands are WordCommandDefinition entries (id, whether they edit, run) on an IEditorActionHost; the control
//    registers them with upstream's keybindings (Contrib/Editing/WordOperationsCommands.cs in the controls package);
//  - the word segmenter locales (Intl.Segmenter) are not ported, so the classifier is the word separators only;
//  - the cursor accessibility commands (cursorWordAccessibility*) are registered without keybindings, as upstream
//    (their accessibility-mode keybindings on Windows are part of the accessibility track).

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.Contrib.WordOperations;

/// <summary>A word command: its id, whether it edits (upstream's precondition EditorContextKeys.writable), what it does.</summary>
internal sealed class WordCommandDefinition(string id, bool writable, Action<IEditorActionHost> run)
{
    public string Id { get; } = id;
    public bool Writable { get; } = writable;
    public Action<IEditorActionHost> Run { get; } = run;
}

/// <summary>The word commands of wordOperations.ts and wordPartOperations.ts.</summary>
internal static class WordCommands
{
    private delegate TextPosition MoveFunc(WordCharacterClassifier wordSeparators, TextModel model, TextPosition position, bool hasMulticursor);

    /// <summary>MoveWordCommand.runEditorCommand.</summary>
    private static void MoveWord(IEditorActionHost editor, bool inSelectionMode, MoveFunc move, string? wordSeparatorsOverride = null)
    {
        if (editor.GetModel() is not { } model || editor.GetViewModel() is not { } viewModel)
        {
            return;
        }
        var wordSeparators = WordCharacterClassifier.GetMapForWordSeparators(wordSeparatorsOverride ?? editor.GetOptions().WordSeparators);
        var selections = editor.GetSelections();
        var hasMulticursor = selections.Count > 1;
        var result = selections.Select(sel =>
        {
            var inPosition = new TextPosition(sel.PositionLineNumber, sel.PositionColumn);
            var outPosition = move(wordSeparators, model, inPosition, hasMulticursor);
            return MoveTo(sel, outPosition, inSelectionMode);
        }).ToList();

        model.PushStackElement();
        viewModel.SetCursorStates("moveWordCommand", CursorChangeReason.Explicit, CursorState.FromModelSelections(result));
        if (result.Count == 1)
        {
            editor.RevealPosition(new TextPosition(result[0].PositionLineNumber, result[0].PositionColumn));
        }
    }

    // _moveTo
    private static TextSelection MoveTo(TextSelection from, TextPosition to, bool inSelectionMode) =>
        inSelectionMode
            // move just position
            ? new TextSelection(from.SelectionStartLineNumber, from.SelectionStartColumn, to.LineNumber, to.Column)
            // move everything
            : new TextSelection(to.LineNumber, to.Column, to.LineNumber, to.Column);

    private static void WordLeft(IEditorActionHost editor, bool inSelectionMode, WordNavigationType type, string? wordSeparators = null) =>
        MoveWord(editor, inSelectionMode, (ws, model, pos, multi) => Cursor.WordOperations.MoveWordLeft(ws, model, pos, type, multi), wordSeparators);

    private static void WordRight(IEditorActionHost editor, bool inSelectionMode, WordNavigationType type, string? wordSeparators = null) =>
        MoveWord(editor, inSelectionMode, (ws, model, pos, _) => Cursor.WordOperations.MoveWordRight(ws, model, pos, type), wordSeparators);

    /// <summary>DeleteWordCommand.runEditorCommand.</summary>
    private static void DeleteWord(IEditorActionHost editor, string id, bool whitespaceHeuristics, Func<DeleteWordContext, TextRange> delete)
    {
        if (editor.GetModel() is not { } model || editor.GetViewModel() is not { } viewModel)
        {
            return;
        }
        var options = editor.GetOptions();
        var wordSeparators = WordCharacterClassifier.GetMapForWordSeparators(options.WordSeparators);
        var autoClosingPairs = editor.LanguageConfigurationService.GetLanguageConfiguration(model.GetLanguageId()).GetAutoClosingPairs();
        var autoClosedCharacters = viewModel.GetCursorAutoClosedCharacters();

        var commands = editor.GetSelections().Select(sel =>
        {
            var deleteRange = delete(new DeleteWordContext(
                WordSeparators: wordSeparators,
                Model: model,
                Selection: sel,
                WhitespaceHeuristics: whitespaceHeuristics,
                AutoClosingDelete: options.AutoClosingDelete,
                AutoClosingBrackets: options.AutoClosingBrackets,
                AutoClosingQuotes: options.AutoClosingQuotes,
                AutoClosingPairs: autoClosingPairs,
                AutoClosedCharacters: autoClosedCharacters));
            return (ICommand?)new ReplaceCommand(deleteRange, "");
        }).ToList();

        editor.PushUndoStop();
        editor.ExecuteCommands(id, commands);
        editor.PushUndoStop();
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

    /// <summary>DeleteInsideWord.run; <paramref name="onlyWord"/> leaves the surrounding whitespace.</summary>
    public static void DeleteInsideWord(IEditorActionHost editor, bool onlyWord)
    {
        if (editor.GetModel() is not { } model)
        {
            return;
        }
        var wordSeparators = WordCharacterClassifier.GetMapForWordSeparators(editor.GetOptions().WordSeparators);
        var commands = editor.GetSelections()
            .Select(sel => (ICommand?)new ReplaceCommand(Cursor.WordOperations.DeleteInsideWord(wordSeparators, model, sel, onlyWord), ""))
            .ToList();

        editor.PushUndoStop();
        editor.ExecuteCommands("deleteInsideWord", commands);
        editor.PushUndoStop();
    }

    /// <summary>The argument of deleteInsideWord (upstream's { onlyWord }).</summary>
    public sealed record DeleteInsideWordArgs(bool OnlyWord);

    /// <summary>Every word command, in upstream's registration order (wordOperations.ts, then wordPartOperations.ts).</summary>
    public static IReadOnlyList<WordCommandDefinition> All { get; } =
    [
        new("cursorWordStartLeft", false, e => WordLeft(e, false, WordNavigationType.WordStart)),
        new("cursorWordEndLeft", false, e => WordLeft(e, false, WordNavigationType.WordEnd)),
        new("cursorWordLeft", false, e => WordLeft(e, false, WordNavigationType.WordStartFast)),
        new("cursorWordStartLeftSelect", false, e => WordLeft(e, true, WordNavigationType.WordStart)),
        new("cursorWordEndLeftSelect", false, e => WordLeft(e, true, WordNavigationType.WordEnd)),
        new("cursorWordLeftSelect", false, e => WordLeft(e, true, WordNavigationType.WordStartFast)),
        new("cursorWordStartRight", false, e => WordRight(e, false, WordNavigationType.WordStart)),
        new("cursorWordEndRight", false, e => WordRight(e, false, WordNavigationType.WordEnd)),
        new("cursorWordRight", false, e => WordRight(e, false, WordNavigationType.WordEnd)),
        new("cursorWordStartRightSelect", false, e => WordRight(e, true, WordNavigationType.WordStart)),
        new("cursorWordEndRightSelect", false, e => WordRight(e, true, WordNavigationType.WordEnd)),
        new("cursorWordRightSelect", false, e => WordRight(e, true, WordNavigationType.WordEnd)),
        new("cursorWordAccessibilityLeft", false, e => WordLeft(e, false, WordNavigationType.WordAccessibility, EditorOptions.UsualWordSeparators)),
        new("cursorWordAccessibilityLeftSelect", false, e => WordLeft(e, true, WordNavigationType.WordAccessibility, EditorOptions.UsualWordSeparators)),
        new("cursorWordAccessibilityRight", false, e => WordRight(e, false, WordNavigationType.WordAccessibility, EditorOptions.UsualWordSeparators)),
        new("cursorWordAccessibilityRightSelect", false, e => WordRight(e, true, WordNavigationType.WordAccessibility, EditorOptions.UsualWordSeparators)),
        new("deleteWordStartLeft", true, e => DeleteWord(e, "deleteWordStartLeft", false, ctx => DeleteLeft(Cursor.WordOperations.DeleteWordLeft(ctx, WordNavigationType.WordStart)))),
        new("deleteWordEndLeft", true, e => DeleteWord(e, "deleteWordEndLeft", false, ctx => DeleteLeft(Cursor.WordOperations.DeleteWordLeft(ctx, WordNavigationType.WordEnd)))),
        new("deleteWordLeft", true, e => DeleteWord(e, "deleteWordLeft", true, ctx => DeleteLeft(Cursor.WordOperations.DeleteWordLeft(ctx, WordNavigationType.WordStart)))),
        new("deleteWordStartRight", true, e => DeleteWord(e, "deleteWordStartRight", false, ctx => DeleteRight(ctx, Cursor.WordOperations.DeleteWordRight(ctx, WordNavigationType.WordStart)))),
        new("deleteWordEndRight", true, e => DeleteWord(e, "deleteWordEndRight", false, ctx => DeleteRight(ctx, Cursor.WordOperations.DeleteWordRight(ctx, WordNavigationType.WordEnd)))),
        new("deleteWordRight", true, e => DeleteWord(e, "deleteWordRight", true, ctx => DeleteRight(ctx, Cursor.WordOperations.DeleteWordRight(ctx, WordNavigationType.WordEnd)))),
        new("deleteWordPartLeft", true, e => DeleteWord(e, "deleteWordPartLeft", true, ctx => DeleteLeft(WordPartOperations.DeleteWordPartLeft(ctx)))),
        new("deleteWordPartRight", true, e => DeleteWord(e, "deleteWordPartRight", true, ctx => DeleteRight(ctx, WordPartOperations.DeleteWordPartRight(ctx)))),
        new("cursorWordPartLeft", false, e => MoveWord(e, false, (ws, model, pos, multi) => WordPartOperations.MoveWordPartLeft(ws, model, pos, multi))),
        new("cursorWordPartLeftSelect", false, e => MoveWord(e, true, (ws, model, pos, multi) => WordPartOperations.MoveWordPartLeft(ws, model, pos, multi))),
        new("cursorWordPartRight", false, e => MoveWord(e, false, (ws, model, pos, _) => WordPartOperations.MoveWordPartRight(ws, model, pos))),
        new("cursorWordPartRightSelect", false, e => MoveWord(e, true, (ws, model, pos, _) => WordPartOperations.MoveWordPartRight(ws, model, pos))),
    ];

    /// <summary>A word command by id (or by upstream's alias cursorWordPartStartLeft[Select]).</summary>
    public static WordCommandDefinition? Get(string id)
    {
        id = id switch
        {
            "cursorWordPartStartLeft" => "cursorWordPartLeft",
            "cursorWordPartStartLeftSelect" => "cursorWordPartLeftSelect",
            _ => id,
        };
        return All.FirstOrDefault(c => c.Id == id);
    }
}
