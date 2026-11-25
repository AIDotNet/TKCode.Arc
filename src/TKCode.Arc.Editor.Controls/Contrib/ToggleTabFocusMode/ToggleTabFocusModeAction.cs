// Ported from vscode (src/vs/editor/contrib/toggleTabFocusMode/browser/toggleTabFocusMode.ts, commit b57d5c9e), MIT
// license.
// Differences from upstream:
//  - an editor action (upstream: a workbench Action2) that toggles the editor's own TabFocusMode instead of the
//    process-wide TabFocus; the keybinding works while the editor has focus;
//  - the alert goes to the editor's live region (CodeEditorAria).

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Platform;
using EditorKeyCode = TKCode.Arc.Editor.KeyCode;

namespace TKCode.Arc;

/// <summary>Toggles whether Tab moves the focus out of the editor (upstream's ToggleTabFocusModeAction).</summary>
internal static class ToggleTabFocusModeAction
{
    public const string Id = "editor.action.toggleTabFocusMode";

    internal const string OnMessage = "Pressing Tab will now move focus to the next focusable element";

    internal const string OffMessage = "Pressing Tab will now insert the tab character";

    /// <summary>The action, on Ctrl+M (Ctrl+Shift+M on macOS).</summary>
    public static readonly EditorAction Action = EditorAction.Create(
        Id,
        "Toggle Tab Key Moves Focus",
        null,
        static (editor, _) => Run(editor),
        new EditorKeybinding(KeyMod.CtrlCmd | (int)EditorKeyCode.KeyM)
        {
            Mac = new() { Primary = KeyMod.WinCtrl | KeyMod.Shift | (int)EditorKeyCode.KeyM },
            Weight = KeybindingWeight.EditorContrib,
        });

    private static void Run(CodeEditor editor)
    {
        var newValue = !editor.TabFocusMode;
        editor.TabFocusMode = newValue;
        editor.Alert(newValue ? OnMessage : OffMessage);
    }
}
