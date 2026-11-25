// The word commands of vscode (src/vs/editor/contrib/wordOperations/browser/wordOperations.ts and
// src/vs/editor/contrib/wordPartOperations/browser/wordPartOperations.ts, commit b57d5c9e, MIT license) as editor
// commands with upstream's keybindings; each runs the engine's WordCommands entry of the same id. deleteInsideWord is
// an action (it has a label upstream).
// Differences from upstream: the cursorWordLeft / Right keybindings' when clause leaves out "not (accessibility mode
// on Windows)" (there is no accessibility mode yet); the command aliases cursorWordPartStartLeft[Select] are commands of
// their own.

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib;
using TKCode.Arc.Editor.Contrib.WordOperations;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>The word movement and word deletion commands.</summary>
internal static class WordOperationsCommands
{
    private static EditorKeybinding InputFocus(int primary, int mac) =>
        new(primary)
        {
            When = EditorContextKeys.TextInputFocus,
            Mac = new PlatformKeybindings { Primary = mac },
        };

    // the keybindings by command id (wordOperations.ts and wordPartOperations.ts kbOpts)
    private static EditorKeybinding[] KeybindingsOf(string id) => id switch
    {
        "cursorWordLeft" => [InputFocus(KeyMod.CtrlCmd | (int)KeyCode.LeftArrow, KeyMod.Alt | (int)KeyCode.LeftArrow)],
        "cursorWordLeftSelect" => [InputFocus(KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.LeftArrow, KeyMod.Alt | KeyMod.Shift | (int)KeyCode.LeftArrow)],
        "cursorWordEndRight" => [InputFocus(KeyMod.CtrlCmd | (int)KeyCode.RightArrow, KeyMod.Alt | (int)KeyCode.RightArrow)],
        "cursorWordEndRightSelect" => [InputFocus(KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.RightArrow, KeyMod.Alt | KeyMod.Shift | (int)KeyCode.RightArrow)],
        "deleteWordLeft" => [InputFocus(KeyMod.CtrlCmd | (int)KeyCode.Backspace, KeyMod.Alt | (int)KeyCode.Backspace)],
        "deleteWordRight" => [InputFocus(KeyMod.CtrlCmd | (int)KeyCode.Delete, KeyMod.Alt | (int)KeyCode.Delete)],
        "deleteWordPartLeft" => [InputFocus(0, KeyMod.WinCtrl | KeyMod.Alt | (int)KeyCode.Backspace)],
        "deleteWordPartRight" => [InputFocus(0, KeyMod.WinCtrl | KeyMod.Alt | (int)KeyCode.Delete)],
        "cursorWordPartLeft" => [InputFocus(0, KeyMod.WinCtrl | KeyMod.Alt | (int)KeyCode.LeftArrow)],
        "cursorWordPartLeftSelect" => [InputFocus(0, KeyMod.WinCtrl | KeyMod.Alt | KeyMod.Shift | (int)KeyCode.LeftArrow)],
        "cursorWordPartRight" => [InputFocus(0, KeyMod.WinCtrl | KeyMod.Alt | (int)KeyCode.RightArrow)],
        "cursorWordPartRightSelect" => [InputFocus(0, KeyMod.WinCtrl | KeyMod.Alt | KeyMod.Shift | (int)KeyCode.RightArrow)],
        _ => [],
    };

    private static EditorCommand ToEditorCommand(WordCommandDefinition definition) =>
        EditorCommand.Create(
            definition.Id,
            definition.Writable ? EditorContextKeys.Writable : null,
            (editor, _) => definition.Run(editor),
            KeybindingsOf(definition.Id));

    private static EditorCommand Alias(string alias, string id)
    {
        var definition = WordCommands.Get(id)!;
        return EditorCommand.Create(alias, null, (editor, _) => definition.Run(editor));
    }

    /// <summary>deleteInsideWord (an action: "Delete Word"); its argument may be <see cref="WordCommands.DeleteInsideWordArgs"/>.</summary>
    public static readonly EditorAction DeleteInsideWord = EditorAction.Create(
        "deleteInsideWord", "Delete Word", EditorContextKeys.Writable,
        (editor, args) => WordCommands.DeleteInsideWord(editor, args is WordCommands.DeleteInsideWordArgs { OnlyWord: true }));

    /// <summary>Every word command, in upstream's registration order, with the deleteInsideWord action and the two aliases.</summary>
    public static IReadOnlyList<EditorCommand> All { get; } =
    [
        .. WordCommands.All.Select(ToEditorCommand),
        DeleteInsideWord,
        Alias("cursorWordPartStartLeft", "cursorWordPartLeft"),
        Alias("cursorWordPartStartLeftSelect", "cursorWordPartLeftSelect"),
    ];
}
