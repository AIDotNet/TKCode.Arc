// Upstream names this command in src/vs/editor/common/standaloneStrings.ts (announceCursorPosition: "Run the command:
// Announce Cursor Position to hear the current line and column", commit b57d5c9e, MIT license); its implementation is
// in the workbench, outside the editor sources ported here, so this one is written for the editor: it alerts the
// cursor's line and column and the size of the selection.

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Platform;
using EditorKeyCode = TKCode.Arc.Editor.KeyCode;

namespace TKCode.Arc;

/// <summary>Reads out the cursor's line and column (editor.action.announceCursorPosition).</summary>
internal static class AnnounceCursorPositionAction
{
    public const string Id = "editor.action.announceCursorPosition";

    /// <summary>The action, on Ctrl+Alt+Shift+G (Cmd+Alt+Shift+G on macOS).</summary>
    public static readonly EditorAction Action = EditorAction.Create(
        Id,
        "Announce Cursor Position",
        EditorContextKeys.Focus,
        static (editor, _) => editor.Alert(editor.GetCursorStatusText()),
        new EditorKeybinding(KeyMod.CtrlCmd | KeyMod.Alt | KeyMod.Shift | (int)EditorKeyCode.KeyG)
        {
            Weight = KeybindingWeight.EditorContrib,
        });
}
