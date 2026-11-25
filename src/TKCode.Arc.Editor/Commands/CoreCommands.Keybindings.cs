// Ported from vscode (src/vs/editor/browser/coreCommands.ts: the ids, preconditions and kbOpts of the commands that
// registerEditorCommand registers, and the registerColumnSelection keybinding rules; src/vs/editor/browser/
// editorExtensions.ts: the kbOpts of UndoCommand, RedoCommand and SelectAllCommand, which CoreEditingCommands.Undo,
// Redo and CoreNavigationCommands.SelectAll implement; commit b57d5c9e), MIT license.
// This is data only: each command's id, precondition and keybindings in an immutable table, which
// CoreCommandsRegistration.Register puts into a CommandsRegistry and a KeybindingsRegistry (parsing the conditions
// with ContextKeyExpr.Deserialize).
//  - keybindings are upstream's raw numbers (KeyMod | KeyCode; the constants below have upstream's values), 0 for "no
//    keybinding" as upstream; a platform entry (mac, win, linux) replaces the default primary and secondary on that
//    platform, as in upstream's IKeybindings;
//  - `when` and the preconditions are the serialized context key expressions (ContextKeyExpression.serialize()); a
//    keybinding's When is what Command.register computes: ContextKeyExpr.and(kbExpr, precondition), normalized (its
//    terms sorted and flattened) as ContextKeyExpr.and does it;
//  - the weight is KeybindingWeight.EditorCore (0), or EditorCore + 1;
//  - Args is a keybinding's `args` as JSON text.
// The overwritable handler commands (type, replacePreviousChar, compositionType, compositionStart, compositionEnd,
// paste, cut and their default: forms) are left out: they forward to editor.trigger, which is the host's.

using System.Collections.Immutable;

namespace TKCode.Arc.Editor.Commands;

/// <summary>A keybinding on one platform (upstream's IKeybindings: primary and secondary).</summary>
/// <param name="Primary">The primary keybinding (KeyMod | KeyCode); 0 for none.</param>
/// <param name="Secondary">The other keybindings.</param>
internal sealed record CoreCommandPlatformKeybinding(int Primary, ImmutableArray<int> Secondary);

/// <summary>A keybinding rule of a core command (upstream's kbOpts, or a rule registerKeybindingRule adds).</summary>
/// <param name="Weight">The keybinding weight (KeybindingWeight.EditorCore is 0).</param>
/// <param name="When">The serialized context key expression the keybinding is enabled in; null for always.</param>
/// <param name="Primary">The primary keybinding (KeyMod | KeyCode); 0 for none.</param>
/// <param name="Secondary">The other keybindings.</param>
/// <param name="Mac">The keybindings on macOS, when they differ.</param>
/// <param name="Win">The keybindings on Windows, when they differ.</param>
/// <param name="Linux">The keybindings on Linux, when they differ.</param>
/// <param name="Args">The arguments the keybinding passes, as JSON text; null for none.</param>
internal sealed record CoreCommandKeybinding(
    int Weight,
    string? When,
    int Primary,
    ImmutableArray<int> Secondary,
    CoreCommandPlatformKeybinding? Mac = null,
    CoreCommandPlatformKeybinding? Win = null,
    CoreCommandPlatformKeybinding? Linux = null,
    string? Args = null);

/// <summary>A core command's registration data.</summary>
/// <param name="Id">The command id.</param>
/// <param name="Precondition">The serialized precondition context key expression; null for none.</param>
/// <param name="Keybindings">The command's keybinding rules (empty for none).</param>
internal sealed record CoreCommandDescriptor(string Id, string? Precondition, ImmutableArray<CoreCommandKeybinding> Keybindings);

/// <summary>The ids, preconditions and keybindings of the core commands, as upstream registers them.</summary>
internal static class CoreCommandsTable
{
    // KeyMod (base/common/keyCodes.ts)
    private const int CtrlCmd = 1 << 11;
    private const int Shift = 1 << 10;
    private const int Alt = 1 << 9;
    private const int WinCtrl = 1 << 8;

    // KeyCode (base/common/keyCodes.ts)
    private const int Backspace = 1;
    private const int Tab = 2;
    private const int Escape = 9;
    private const int PageUp = 11;
    private const int PageDown = 12;
    private const int End = 13;
    private const int Home = 14;
    private const int LeftArrow = 15;
    private const int UpArrow = 16;
    private const int RightArrow = 17;
    private const int DownArrow = 18;
    private const int Delete = 20;
    private const int KeyA = 31;
    private const int KeyB = 32;
    private const int KeyD = 34;
    private const int KeyE = 35;
    private const int KeyF = 36;
    private const int KeyH = 38;
    private const int KeyN = 44;
    private const int KeyO = 45;
    private const int KeyP = 46;
    private const int KeyY = 55;
    private const int KeyZ = 56;

    // KeybindingWeight.EditorCore
    private const int CoreWeight = 0;

    // EditorContextKeys, serialized
    private const string TextInputFocus = "textInputFocus";
    // ContextKeyExpr.and(and(editorTextFocus, tabDoesNotMoveFocus), writable)
    private const string TabFocusWritable = "editorTextFocus && !editorReadonly && !editorTabMovesFocus";
    private const string Writable = "!editorReadonly";
    private const string ColumnSelectionCondition = "editorColumnSelection && textInputFocus";

    private static CoreCommandPlatformKeybinding P(int primary, params int[] secondary) => new(primary, [.. secondary]);

    private static CoreCommandKeybinding Kb(
        string? when,
        int primary,
        int[]? secondary = null,
        CoreCommandPlatformKeybinding? mac = null,
        CoreCommandPlatformKeybinding? win = null,
        CoreCommandPlatformKeybinding? linux = null,
        int weight = CoreWeight,
        string? args = null) =>
        new(weight, when, primary, secondary is null ? [] : [.. secondary], mac, win, linux, args);

    private static CoreCommandDescriptor C(string id, string? precondition = null, params CoreCommandKeybinding[] keybindings) =>
        new(id, precondition, [.. keybindings]);

    // registerColumnSelection(id, keybinding)
    private static CoreCommandKeybinding ColumnSelectionRule(int keybinding) =>
        Kb(ColumnSelectionCondition, keybinding, weight: CoreWeight + 1);

    /// <summary>Every core command, in upstream's registration order.</summary>
    public static readonly ImmutableArray<CoreCommandDescriptor> Commands =
    [
        // CoreNavigationCommands
        C("_moveTo"),
        C("_moveToSelect"),
        C("columnSelect"),
        C("cursorColumnSelectLeft", null,
            Kb(TextInputFocus, CtrlCmd | Shift | Alt | LeftArrow, linux: P(0)),
            ColumnSelectionRule(Shift | LeftArrow)),
        C("cursorColumnSelectRight", null,
            Kb(TextInputFocus, CtrlCmd | Shift | Alt | RightArrow, linux: P(0)),
            ColumnSelectionRule(Shift | RightArrow)),
        C("cursorColumnSelectUp", null,
            Kb(TextInputFocus, CtrlCmd | Shift | Alt | UpArrow, linux: P(0)),
            ColumnSelectionRule(Shift | UpArrow)),
        C("cursorColumnSelectPageUp", null,
            Kb(TextInputFocus, CtrlCmd | Shift | Alt | PageUp, linux: P(0)),
            ColumnSelectionRule(Shift | PageUp)),
        C("cursorColumnSelectDown", null,
            Kb(TextInputFocus, CtrlCmd | Shift | Alt | DownArrow, linux: P(0)),
            ColumnSelectionRule(Shift | DownArrow)),
        C("cursorColumnSelectPageDown", null,
            Kb(TextInputFocus, CtrlCmd | Shift | Alt | PageDown, linux: P(0)),
            ColumnSelectionRule(Shift | PageDown)),
        C("cursorMove"),
        C("cursorLeft", null,
            Kb(TextInputFocus, LeftArrow, mac: P(LeftArrow, WinCtrl | KeyB))),
        C("cursorLeftSelect", null,
            Kb(TextInputFocus, Shift | LeftArrow)),
        C("cursorRight", null,
            Kb(TextInputFocus, RightArrow, mac: P(RightArrow, WinCtrl | KeyF))),
        C("cursorRightSelect", null,
            Kb(TextInputFocus, Shift | RightArrow)),
        C("cursorUp", null,
            Kb(TextInputFocus, UpArrow, mac: P(UpArrow, WinCtrl | KeyP))),
        C("cursorUpSelect", null,
            Kb(TextInputFocus, Shift | UpArrow, [CtrlCmd | Shift | UpArrow], mac: P(Shift | UpArrow), linux: P(Shift | UpArrow))),
        C("cursorPageUp", null,
            Kb(TextInputFocus, PageUp)),
        C("cursorPageUpSelect", null,
            Kb(TextInputFocus, Shift | PageUp)),
        C("cursorDown", null,
            Kb(TextInputFocus, DownArrow, mac: P(DownArrow, WinCtrl | KeyN))),
        C("cursorDownSelect", null,
            Kb(TextInputFocus, Shift | DownArrow, [CtrlCmd | Shift | DownArrow], mac: P(Shift | DownArrow), linux: P(Shift | DownArrow))),
        C("cursorPageDown", null,
            Kb(TextInputFocus, PageDown)),
        C("cursorPageDownSelect", null,
            Kb(TextInputFocus, Shift | PageDown)),
        C("createCursor"),
        C("_lastCursorMoveToSelect"),
        C("cursorHome", null,
            Kb(TextInputFocus, Home, mac: P(Home, CtrlCmd | LeftArrow))),
        C("cursorHomeSelect", null,
            Kb(TextInputFocus, Shift | Home, mac: P(Shift | Home, CtrlCmd | Shift | LeftArrow))),
        C("cursorLineStart", null,
            Kb(TextInputFocus, 0, mac: P(WinCtrl | KeyA))),
        C("cursorLineStartSelect", null,
            Kb(TextInputFocus, 0, mac: P(WinCtrl | Shift | KeyA))),
        C("cursorEnd", null,
            Kb(TextInputFocus, End, mac: P(End, CtrlCmd | RightArrow), args: "{\"sticky\":false}")),
        C("cursorEndSelect", null,
            Kb(TextInputFocus, Shift | End, mac: P(Shift | End, CtrlCmd | Shift | RightArrow), args: "{\"sticky\":false}")),
        C("cursorLineEnd", null,
            Kb(TextInputFocus, 0, mac: P(WinCtrl | KeyE))),
        C("cursorLineEndSelect", null,
            Kb(TextInputFocus, 0, mac: P(WinCtrl | Shift | KeyE))),
        C("cursorTop", null,
            Kb(TextInputFocus, CtrlCmd | Home, mac: P(CtrlCmd | UpArrow))),
        C("cursorTopSelect", null,
            Kb(TextInputFocus, CtrlCmd | Shift | Home, mac: P(CtrlCmd | Shift | UpArrow))),
        C("cursorBottom", null,
            Kb(TextInputFocus, CtrlCmd | End, mac: P(CtrlCmd | DownArrow))),
        C("cursorBottomSelect", null,
            Kb(TextInputFocus, CtrlCmd | Shift | End, mac: P(CtrlCmd | Shift | DownArrow))),
        C("editorScroll"),
        C("scrollLineUp", null,
            Kb(TextInputFocus, CtrlCmd | UpArrow, mac: P(WinCtrl | PageUp))),
        C("scrollPageUp", null,
            Kb(TextInputFocus, CtrlCmd | PageUp, win: P(Alt | PageUp), linux: P(Alt | PageUp))),
        C("scrollEditorTop", null,
            Kb(TextInputFocus, 0)),
        C("scrollLineDown", null,
            Kb(TextInputFocus, CtrlCmd | DownArrow, mac: P(WinCtrl | PageDown))),
        C("scrollPageDown", null,
            Kb(TextInputFocus, CtrlCmd | PageDown, win: P(Alt | PageDown), linux: P(Alt | PageDown))),
        C("scrollEditorBottom", null,
            Kb(TextInputFocus, 0)),
        C("scrollLeft", null,
            Kb(TextInputFocus, 0)),
        C("scrollRight", null,
            Kb(TextInputFocus, 0)),
        C("_wordSelect"),
        C("_wordSelectDrag"),
        C("lastCursorWordSelect"),
        C("_lineSelect"),
        C("_lineSelectDrag"),
        C("lastCursorLineSelect"),
        C("lastCursorLineSelectDrag"),
        C("cancelSelection", "editorHasSelection",
            Kb("editorHasSelection && textInputFocus", Escape, [Shift | Escape])),
        C("removeSecondaryCursors", "editorHasMultipleSelections",
            Kb("editorHasMultipleSelections && textInputFocus", Escape, [Shift | Escape], weight: CoreWeight + 1)),
        C("revealLine"),
        C("editor.action.selectAll", null,
            Kb(null, CtrlCmd | KeyA)),
        C("setSelection"),

        // CoreEditingCommands
        C("lineBreakInsert", Writable,
            Kb("textInputFocus && !editorReadonly", 0, mac: P(WinCtrl | KeyO))),
        C("outdent", Writable,
            Kb(TabFocusWritable, Shift | Tab)),
        C("tab", Writable,
            Kb(TabFocusWritable, Tab)),
        C("deleteLeft", null,
            Kb(TextInputFocus, Backspace, [Shift | Backspace], mac: P(Backspace, Shift | Backspace, WinCtrl | KeyH, WinCtrl | Backspace))),
        C("deleteRight", null,
            Kb(TextInputFocus, Delete, mac: P(Delete, WinCtrl | KeyD, WinCtrl | Delete))),
        C("undo", null,
            Kb(null, CtrlCmd | KeyZ)),
        C("redo", null,
            Kb(null, CtrlCmd | KeyY, [CtrlCmd | Shift | KeyZ], mac: P(CtrlCmd | Shift | KeyZ))),
    ];

    /// <summary>The command with <paramref name="id"/>; null when it is not a core command.</summary>
    public static CoreCommandDescriptor? Get(string id)
    {
        foreach (var command in Commands)
        {
            if (command.Id == id)
            {
                return command;
            }
        }
        return null;
    }
}
