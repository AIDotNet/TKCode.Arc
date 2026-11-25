// The editor actions of vscode's editing contributions, with their ids, labels, preconditions and keybindings (commit
// b57d5c9e, MIT license): src/vs/editor/contrib/linesOperations/browser/linesOperations.ts,
// comment/browser/comment.ts, caretOperations/browser/caretOperations.ts and transpose.ts,
// insertFinalNewLine/browser/insertFinalNewLine.ts, lineSelection/browser/lineSelection.ts and
// indentation/browser/indentation.ts. Each action runs the engine's port (TKCode.Arc.Editor.Contrib.*) on the editor.
// Differences from upstream:
//  - menus (menuOpts, the command palette metadata) are not ported;
//  - indentUsingSpaces / indentUsingTabs / changeTabDisplaySize show a quick pick upstream; here the tab size is the
//    command's argument (an int or a string of one), or the model's tab size without one;
//  - the case actions are always registered (.NET has the regex features upstream checks for).

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib;
using TKCode.Arc.Editor.Contrib.CaretOperations;
using TKCode.Arc.Editor.Contrib.Comment;
using TKCode.Arc.Editor.Contrib.Indentation;
using TKCode.Arc.Editor.Contrib.InsertFinalNewLine;
using TKCode.Arc.Editor.Contrib.LinesOperations;
using TKCode.Arc.Editor.Platform;
using LineSelectionImpl = TKCode.Arc.Editor.Contrib.LineSelection.LineSelection;
using LinesOperationsImpl = TKCode.Arc.Editor.Contrib.LinesOperations.LinesOperations;

namespace TKCode.Arc;

/// <summary>The line, comment, caret, indentation and line selection actions.</summary>
internal static class EditingActions
{
    private static readonly ContextKeyExpression Writable = EditorContextKeys.Writable;

    private static EditorKeybinding TextFocus(int primary) => new(primary) { When = EditorContextKeys.EditorTextFocus };

    private static EditorKeybinding InputFocus(int primary) => new(primary) { When = EditorContextKeys.TextInputFocus };

    private static EditorAction Action(string id, string label, ContextKeyExpression? precondition, Action<IEditorActionHost> run, params EditorKeybinding[] keybindings) =>
        EditorAction.Create(id, label, precondition, (editor, _) => run(editor), keybindings);

    // ------ linesOperations.ts

    public static readonly EditorAction CopyLinesUpAction = Action(
        "editor.action.copyLinesUpAction", "Copy Line Up", Writable,
        e => LinesOperationsImpl.CopyLines(e, false, "editor.action.copyLinesUpAction"),
        TextFocus(KeyMod.Alt | KeyMod.Shift | (int)KeyCode.UpArrow) with { Linux = new PlatformKeybindings { Primary = KeyMod.CtrlCmd | KeyMod.Alt | KeyMod.Shift | (int)KeyCode.UpArrow } });

    public static readonly EditorAction CopyLinesDownAction = Action(
        "editor.action.copyLinesDownAction", "Copy Line Down", Writable,
        e => LinesOperationsImpl.CopyLines(e, true, "editor.action.copyLinesDownAction"),
        TextFocus(KeyMod.Alt | KeyMod.Shift | (int)KeyCode.DownArrow) with { Linux = new PlatformKeybindings { Primary = KeyMod.CtrlCmd | KeyMod.Alt | KeyMod.Shift | (int)KeyCode.DownArrow } });

    public static readonly EditorAction DuplicateSelectionAction = Action(
        "editor.action.duplicateSelection", "Duplicate Selection", Writable,
        e => LinesOperationsImpl.DuplicateSelection(e, "editor.action.duplicateSelection"));

    public static readonly EditorAction MoveLinesUpAction = Action(
        "editor.action.moveLinesUpAction", "Move Line Up", Writable,
        e => LinesOperationsImpl.MoveLines(e, false, "editor.action.moveLinesUpAction"),
        TextFocus(KeyMod.Alt | (int)KeyCode.UpArrow) with { Linux = new PlatformKeybindings { Primary = KeyMod.Alt | (int)KeyCode.UpArrow } });

    public static readonly EditorAction MoveLinesDownAction = Action(
        "editor.action.moveLinesDownAction", "Move Line Down", Writable,
        e => LinesOperationsImpl.MoveLines(e, true, "editor.action.moveLinesDownAction"),
        TextFocus(KeyMod.Alt | (int)KeyCode.DownArrow) with { Linux = new PlatformKeybindings { Primary = KeyMod.Alt | (int)KeyCode.DownArrow } });

    public static readonly EditorAction SortLinesAscendingAction = Action(
        "editor.action.sortLinesAscending", "Sort Lines Ascending", Writable,
        e => LinesOperationsImpl.SortLines(e, false, "editor.action.sortLinesAscending"));

    public static readonly EditorAction SortLinesDescendingAction = Action(
        "editor.action.sortLinesDescending", "Sort Lines Descending", Writable,
        e => LinesOperationsImpl.SortLines(e, true, "editor.action.sortLinesDescending"));

    public static readonly EditorAction DeleteDuplicateLinesAction = Action(
        "editor.action.removeDuplicateLines", "Delete Duplicate Lines", Writable,
        e => LinesOperationsImpl.DeleteDuplicateLines(e, "editor.action.removeDuplicateLines"));

    public static readonly EditorAction TrimTrailingWhitespaceAction = EditorAction.Create(
        "editor.action.trimTrailingWhitespace", "Trim Trailing Whitespace", Writable,
        (editor, args) => LinesOperationsImpl.TrimTrailingWhitespace(editor, args as TrimTrailingWhitespaceArgs, "editor.action.trimTrailingWhitespace"),
        TextFocus(KeyCodes.KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyK, KeyMod.CtrlCmd | (int)KeyCode.KeyX)));

    public static readonly EditorAction DeleteLinesAction = Action(
        "editor.action.deleteLines", "Delete Line", Writable,
        e => LinesOperationsImpl.DeleteLines(e, "editor.action.deleteLines"),
        InputFocus(KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.KeyK));

    public static readonly EditorAction IndentLinesAction = Action(
        "editor.action.indentLines", "Indent Line", Writable,
        e => LinesOperationsImpl.IndentLines(e, "editor.action.indentLines"),
        TextFocus(KeyMod.CtrlCmd | (int)KeyCode.BracketRight));

    public static readonly EditorAction OutdentLinesAction = Action(
        "editor.action.outdentLines", "Outdent Line", Writable,
        LinesOperationsImpl.OutdentLines,
        TextFocus(KeyMod.CtrlCmd | (int)KeyCode.BracketLeft));

    public static readonly EditorAction InsertLineBeforeAction = Action(
        "editor.action.insertLineBefore", "Insert Line Above", Writable,
        e => LinesOperationsImpl.InsertLineBefore(e, "editor.action.insertLineBefore"),
        TextFocus(KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.Enter));

    public static readonly EditorAction InsertLineAfterAction = Action(
        "editor.action.insertLineAfter", "Insert Line Below", Writable,
        e => LinesOperationsImpl.InsertLineAfter(e, "editor.action.insertLineAfter"),
        TextFocus(KeyMod.CtrlCmd | (int)KeyCode.Enter));

    public static readonly EditorAction DeleteAllLeftAction = Action(
        "deleteAllLeft", "Delete All Left", Writable,
        e => LinesOperationsImpl.DeleteAllLeft(e, "deleteAllLeft"),
        InputFocus(0) with { Mac = new PlatformKeybindings { Primary = KeyMod.CtrlCmd | (int)KeyCode.Backspace } });

    public static readonly EditorAction DeleteAllRightAction = Action(
        "deleteAllRight", "Delete All Right", Writable,
        e => LinesOperationsImpl.DeleteAllRight(e, "deleteAllRight"),
        InputFocus(0) with { Mac = new PlatformKeybindings { Primary = KeyMod.WinCtrl | (int)KeyCode.KeyK, Secondary = [KeyMod.CtrlCmd | (int)KeyCode.Delete] } });

    public static readonly EditorAction JoinLinesAction = Action(
        "editor.action.joinLines", "Join Lines", Writable,
        e => LinesOperationsImpl.JoinLines(e, "editor.action.joinLines"),
        TextFocus(0) with { Mac = new PlatformKeybindings { Primary = KeyMod.WinCtrl | (int)KeyCode.KeyJ } });

    public static readonly EditorAction TransposeAction = Action(
        "editor.action.transpose", "Transpose Characters around the Cursor", Writable,
        e => LinesOperationsImpl.Transpose(e, "editor.action.transpose"));

    public static readonly EditorAction UpperCaseAction = CaseAction("editor.action.transformToUppercase", "Transform to Uppercase", CaseTransforms.ToUpperCase);

    public static readonly EditorAction LowerCaseAction = CaseAction("editor.action.transformToLowercase", "Transform to Lowercase", CaseTransforms.ToLowerCase);

    public static readonly EditorAction ReverseLinesAction = Action(
        "editor.action.reverseLines", "Reverse lines", Writable,
        e => LinesOperationsImpl.ReverseLines(e, "editor.action.reverseLines"));

    public static readonly EditorAction SnakeCaseAction = CaseAction("editor.action.transformToSnakecase", "Transform to Snake Case", CaseTransforms.ToSnakeCase);

    public static readonly EditorAction CamelCaseAction = CaseAction("editor.action.transformToCamelcase", "Transform to Camel Case", CaseTransforms.ToCamelCase);

    public static readonly EditorAction PascalCaseAction = CaseAction("editor.action.transformToPascalcase", "Transform to Pascal Case", CaseTransforms.ToPascalCase);

    public static readonly EditorAction TitleCaseAction = CaseAction("editor.action.transformToTitlecase", "Transform to Title Case", CaseTransforms.ToTitleCase);

    public static readonly EditorAction KebabCaseAction = CaseAction("editor.action.transformToKebabcase", "Transform to Kebab Case", CaseTransforms.ToKebabCase);

    private static EditorAction CaseAction(string id, string label, Func<string, string, string> modifyText) =>
        Action(id, label, Writable, e => LinesOperationsImpl.ChangeCase(e, modifyText, id));

    // ------ comment.ts

    public static readonly EditorAction ToggleCommentLineAction = Action(
        "editor.action.commentLine", "Toggle Line Comment", Writable,
        e => CommentOperations.CommentLine(e, LineCommentType.Toggle, "editor.action.commentLine"),
        TextFocus(KeyMod.CtrlCmd | (int)KeyCode.Slash));

    public static readonly EditorAction AddLineCommentAction = Action(
        "editor.action.addCommentLine", "Add Line Comment", Writable,
        e => CommentOperations.CommentLine(e, LineCommentType.ForceAdd, "editor.action.addCommentLine"),
        TextFocus(KeyCodes.KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyK, KeyMod.CtrlCmd | (int)KeyCode.KeyC)));

    public static readonly EditorAction RemoveLineCommentAction = Action(
        "editor.action.removeCommentLine", "Remove Line Comment", Writable,
        e => CommentOperations.CommentLine(e, LineCommentType.ForceRemove, "editor.action.removeCommentLine"),
        TextFocus(KeyCodes.KeyChord(KeyMod.CtrlCmd | (int)KeyCode.KeyK, KeyMod.CtrlCmd | (int)KeyCode.KeyU)));

    public static readonly EditorAction BlockCommentAction = Action(
        "editor.action.blockComment", "Toggle Block Comment", Writable,
        e => CommentOperations.BlockComment(e, "editor.action.blockComment"),
        TextFocus(KeyMod.Shift | KeyMod.Alt | (int)KeyCode.KeyA) with { Linux = new PlatformKeybindings { Primary = KeyMod.CtrlCmd | KeyMod.Shift | (int)KeyCode.KeyA } });

    // ------ caretOperations.ts, transpose.ts

    public static readonly EditorAction MoveCaretLeftAction = Action(
        "editor.action.moveCarretLeftAction", "Move Selected Text Left", Writable,
        e => CaretOperations.MoveCaret(e, true, "editor.action.moveCarretLeftAction"));

    public static readonly EditorAction MoveCaretRightAction = Action(
        "editor.action.moveCarretRightAction", "Move Selected Text Right", Writable,
        e => CaretOperations.MoveCaret(e, false, "editor.action.moveCarretRightAction"));

    public static readonly EditorAction TransposeLettersAction = Action(
        "editor.action.transposeLetters", "Transpose Letters", Writable,
        e => CaretOperations.TransposeLetters(e, "editor.action.transposeLetters"),
        InputFocus(0) with { Mac = new PlatformKeybindings { Primary = KeyMod.WinCtrl | (int)KeyCode.KeyT } });

    // ------ insertFinalNewLine.ts

    public static readonly EditorAction InsertFinalNewLineAction = Action(
        "editor.action.insertFinalNewLine", "Insert Final New Line", Writable,
        e => InsertFinalNewLineCommand.Run(e, "editor.action.insertFinalNewLine"));

    // ------ lineSelection.ts

    public static readonly EditorAction ExpandLineSelectionAction = EditorAction.Create(
        "expandLineSelection", "Expand Line Selection", null,
        (editor, args) => LineSelectionImpl.ExpandLineSelection(editor, args is string source ? source : "keyboard"),
        InputFocus(KeyMod.CtrlCmd | (int)KeyCode.KeyL));

    // ------ indentation.ts

    public static readonly EditorAction IndentationToSpacesAction = Action(
        "editor.action.indentationToSpaces", "Convert Indentation to Spaces", Writable,
        e => IndentationOperations.IndentationToSpaces(e, "editor.action.indentationToSpaces"));

    public static readonly EditorAction IndentationToTabsAction = Action(
        "editor.action.indentationToTabs", "Convert Indentation to Tabs", Writable,
        e => IndentationOperations.IndentationToTabs(e, "editor.action.indentationToTabs"));

    public static readonly EditorAction IndentUsingTabs = EditorAction.Create(
        "editor.action.indentUsingTabs", "Indent Using Tabs", null,
        (editor, args) => IndentationOperations.ChangeIndentationSize(editor, false, false, IndentationOperations.ParseTabSizeArg(args)));

    public static readonly EditorAction IndentUsingSpaces = EditorAction.Create(
        "editor.action.indentUsingSpaces", "Indent Using Spaces", null,
        (editor, args) => IndentationOperations.ChangeIndentationSize(editor, true, false, IndentationOperations.ParseTabSizeArg(args)));

    public static readonly EditorAction ChangeTabDisplaySize = EditorAction.Create(
        "editor.action.changeTabDisplaySize", "Change Tab Display Size", null,
        (editor, args) => IndentationOperations.ChangeIndentationSize(editor, true, true, IndentationOperations.ParseTabSizeArg(args)));

    public static readonly EditorAction DetectIndentation = Action(
        "editor.action.detectIndentation", "Detect Indentation from Content", null,
        IndentationOperations.DetectIndentation);

    public static readonly EditorAction ReindentLinesAction = Action(
        "editor.action.reindentlines", "Reindent Lines", Writable,
        e => IndentationOperations.ReindentLines(e, "editor.action.reindentlines"));

    public static readonly EditorAction ReindentSelectedLinesAction = Action(
        "editor.action.reindentselectedlines", "Reindent Selected Lines", Writable,
        e => IndentationOperations.ReindentSelectedLines(e, "editor.action.reindentselectedlines"));

    /// <summary>The actions in upstream's registration order (linesOperations, comment, caret, final new line, line selection, indentation).</summary>
    public static IReadOnlyList<EditorCommand> All { get; } =
    [
        CopyLinesUpAction, CopyLinesDownAction, DuplicateSelectionAction, MoveLinesUpAction, MoveLinesDownAction,
        SortLinesAscendingAction, SortLinesDescendingAction, DeleteDuplicateLinesAction, TrimTrailingWhitespaceAction,
        DeleteLinesAction, IndentLinesAction, OutdentLinesAction, InsertLineBeforeAction, InsertLineAfterAction,
        DeleteAllLeftAction, DeleteAllRightAction, JoinLinesAction, TransposeAction, UpperCaseAction, LowerCaseAction,
        ReverseLinesAction, SnakeCaseAction, CamelCaseAction, PascalCaseAction, TitleCaseAction, KebabCaseAction,
        ToggleCommentLineAction, AddLineCommentAction, RemoveLineCommentAction, BlockCommentAction,
        MoveCaretLeftAction, MoveCaretRightAction, TransposeLettersAction,
        InsertFinalNewLineAction,
        ExpandLineSelectionAction,
        IndentationToSpacesAction, IndentationToTabsAction, IndentUsingTabs, IndentUsingSpaces, ChangeTabDisplaySize,
        DetectIndentation, ReindentLinesAction, ReindentSelectedLinesAction,
    ];
}
