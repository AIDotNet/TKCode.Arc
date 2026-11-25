// Ported from vscode (src/vs/editor/contrib/linesOperations/test/browser/linesOperations.test.ts, commit b57d5c9e), MIT
// license. Differences from upstream: the actions are TestEditorActions that run the engine's LinesOperations methods;
// Handler.Type is EditingTestEditor.Type, CoreEditingCommands.Undo / Redo are Undo / Redo.

using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Contrib.LinesOperations;
using LinesOperationsImpl = TKCode.Arc.Editor.Contrib.LinesOperations.LinesOperations;
using TKCode.Arc.Editor.Model;
using Xunit;
using static TKCode.Arc.Editor.Tests.Contrib.EditingAssert;
using static TKCode.Arc.Editor.Tests.Contrib.EditingTestEditor;

namespace TKCode.Arc.Editor.Tests.Contrib;

public sealed partial class LinesOperationsTests
{
    private sealed class SortLinesAscendingAction() : TestEditorAction(e => LinesOperationsImpl.SortLines(e, false, "editor.action.sortLinesAscending"));
    private sealed class SortLinesDescendingAction() : TestEditorAction(e => LinesOperationsImpl.SortLines(e, true, "editor.action.sortLinesDescending"));
    private sealed class DeleteDuplicateLinesAction() : TestEditorAction(e => LinesOperationsImpl.DeleteDuplicateLines(e, "editor.action.removeDuplicateLines"));
    private sealed class ReverseLinesAction() : TestEditorAction(e => LinesOperationsImpl.ReverseLines(e, "editor.action.reverseLines"));
    private sealed class DeleteLinesAction() : TestEditorAction(e => LinesOperationsImpl.DeleteLines(e, "editor.action.deleteLines"));
    private sealed class IndentLinesAction() : TestEditorAction(e => LinesOperationsImpl.IndentLines(e, "editor.action.indentLines"));
    private sealed class InsertLineBeforeAction() : TestEditorAction(e => LinesOperationsImpl.InsertLineBefore(e, "editor.action.insertLineBefore"));
    private sealed class InsertLineAfterAction() : TestEditorAction(e => LinesOperationsImpl.InsertLineAfter(e, "editor.action.insertLineAfter"));
    private sealed class DeleteAllLeftAction() : TestEditorAction(e => LinesOperationsImpl.DeleteAllLeft(e, "deleteAllLeft"));
    private sealed class DeleteAllRightAction() : TestEditorAction(e => LinesOperationsImpl.DeleteAllRight(e, "deleteAllRight"));
    private sealed class JoinLinesAction() : TestEditorAction(e => LinesOperationsImpl.JoinLines(e, "editor.action.joinLines"));
    private sealed class TransposeAction() : TestEditorAction(e => LinesOperationsImpl.Transpose(e, "editor.action.transpose"));
    private sealed class UpperCaseAction() : TestEditorAction(e => LinesOperationsImpl.ChangeCase(e, CaseTransforms.ToUpperCase, "editor.action.transformToUppercase"));
    private sealed class LowerCaseAction() : TestEditorAction(e => LinesOperationsImpl.ChangeCase(e, CaseTransforms.ToLowerCase, "editor.action.transformToLowercase"));
    private sealed class TitleCaseAction() : TestEditorAction(e => LinesOperationsImpl.ChangeCase(e, CaseTransforms.ToTitleCase, "editor.action.transformToTitlecase"));
    private sealed class SnakeCaseAction() : TestEditorAction(e => LinesOperationsImpl.ChangeCase(e, CaseTransforms.ToSnakeCase, "editor.action.transformToSnakecase"));
    private sealed class CamelCaseAction() : TestEditorAction(e => LinesOperationsImpl.ChangeCase(e, CaseTransforms.ToCamelCase, "editor.action.transformToCamelcase"));
    private sealed class PascalCaseAction() : TestEditorAction(e => LinesOperationsImpl.ChangeCase(e, CaseTransforms.ToPascalCase, "editor.action.transformToPascalcase"));
    private sealed class KebabCaseAction() : TestEditorAction(e => LinesOperationsImpl.ChangeCase(e, CaseTransforms.ToKebabCase, "editor.action.transformToKebabcase"));


    // suite: Editor Contrib - Line Operations


    // suite: SortLinesAscendingAction
    [Fact]
    public void ShouldSortSelectedLinesInAscendingOrder()
    {
            WithTestCodeEditor(
                [
                    "omicron",
                    "beta",
                    "alpha"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var sortLinesAscendingAction = new SortLinesAscendingAction();

                    editor.SetSelection(new TextSelection(1, 1, 3, 5));
                    ExecuteAction(sortLinesAscendingAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), [
                        "alpha",
                        "beta",
                        "omicron"
                    ]);
                    AssertSelection(editor, new TextSelection(1, 1, 3, 7));
                });
    }


    [Fact]
    public void ShouldSortLinesInAscendingOrder()
    {
            WithTestCodeEditor(
                [
                    "omicron",
                    "beta",
                    "alpha"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var sortLinesAscendingAction = new SortLinesAscendingAction();

                    ExecuteAction(sortLinesAscendingAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), [
                        "alpha",
                        "beta",
                        "omicron"
                    ]);
                });
    }


    [Fact]
    public void ShouldSortMultipleSelectionsInAscendingOrder()
    {
            WithTestCodeEditor(
                [
                    "omicron",
                    "beta",
                    "alpha",
                    "",
                    "omicron",
                    "beta",
                    "alpha"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var sortLinesAscendingAction = new SortLinesAscendingAction();

                    editor.SetSelections([new TextSelection(1, 1, 3, 5), new TextSelection(5, 1, 7, 5)]);
                    ExecuteAction(sortLinesAscendingAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), [
                        "alpha",
                        "beta",
                        "omicron",
                        "",
                        "alpha",
                        "beta",
                        "omicron"
                    ]);
                    TextSelection[] expectedSelections = [
                        new TextSelection(1, 1, 3, 7),
                        new TextSelection(5, 1, 7, 7)
                    ];
                    var actualSelections = editor.GetSelections();
                        for (var index = 0; index < actualSelections.Count; index++)
                        {
                            AssertDeepStrictEqual(actualSelections[index].ToString(), expectedSelections[index].ToString());
                        }
                });
    }


    [Fact]
    public void AppliesToWholeDocumentWhenSelectionIsSingleLine()
    {
            WithTestCodeEditor(
                [
                    "omicron",
                    "beta",
                    "alpha"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var sortLinesAscendingAction = new SortLinesAscendingAction();

                    editor.SetSelection(new TextSelection(2, 1, 2, 4));
                    ExecuteAction(sortLinesAscendingAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), [
                        "alpha",
                        "beta",
                        "omicron"
                    ]);
                });
    }


    // suite: SortLinesDescendingAction
    [Fact]
    public void ShouldSortSelectedLinesInDescendingOrder()
    {
            WithTestCodeEditor(
                [
                    "alpha",
                    "beta",
                    "omicron"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var sortLinesDescendingAction = new SortLinesDescendingAction();

                    editor.SetSelection(new TextSelection(1, 1, 3, 7));
                    ExecuteAction(sortLinesDescendingAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), [
                        "omicron",
                        "beta",
                        "alpha"
                    ]);
                    AssertSelection(editor, new TextSelection(1, 1, 3, 5));
                });
    }


    [Fact]
    public void ShouldSortMultipleSelectionsInDescendingOrder()
    {
            WithTestCodeEditor(
                [
                    "alpha",
                    "beta",
                    "omicron",
                    "",
                    "alpha",
                    "beta",
                    "omicron"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var sortLinesDescendingAction = new SortLinesDescendingAction();

                    editor.SetSelections([new TextSelection(1, 1, 3, 7), new TextSelection(5, 1, 7, 7)]);
                    ExecuteAction(sortLinesDescendingAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), [
                        "omicron",
                        "beta",
                        "alpha",
                        "",
                        "omicron",
                        "beta",
                        "alpha"
                    ]);
                    TextSelection[] expectedSelections = [
                        new TextSelection(1, 1, 3, 5),
                        new TextSelection(5, 1, 7, 5)
                    ];
                    var actualSelections = editor.GetSelections();
                        for (var index = 0; index < actualSelections.Count; index++)
                        {
                            AssertDeepStrictEqual(actualSelections[index].ToString(), expectedSelections[index].ToString());
                        }
                });
    }


    // suite: DeleteDuplicateLinesAction
    [Fact]
    public void ShouldRemoveDuplicateLinesWithinSelection()
    {
            WithTestCodeEditor(
                [
                    "alpha",
                    "beta",
                    "beta",
                    "beta",
                    "alpha",
                    "omicron",
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var deleteDuplicateLinesAction = new DeleteDuplicateLinesAction();

                    editor.SetSelection(new TextSelection(1, 3, 6, 4));
                    ExecuteAction(deleteDuplicateLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), [
                        "alpha",
                        "beta",
                        "omicron",
                    ]);
                    AssertSelection(editor, new TextSelection(1, 1, 3, 8));
                });
    }


    [Fact]
    public void ShouldRemoveDuplicateLines()
    {
            WithTestCodeEditor(
                [
                    "alpha",
                    "beta",
                    "beta",
                    "beta",
                    "alpha",
                    "omicron",
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var deleteDuplicateLinesAction = new DeleteDuplicateLinesAction();

                    ExecuteAction(deleteDuplicateLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), [
                        "alpha",
                        "beta",
                        "omicron",
                    ]);
                    Assert.True(editor.GetSelection().IsEmpty);
                });
    }


    [Fact]
    public void ShouldRemoveDuplicateLinesInMultipleSelections()
    {
            WithTestCodeEditor(
                [
                    "alpha",
                    "beta",
                    "beta",
                    "omicron",
                    "",
                    "alpha",
                    "alpha",
                    "beta"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var deleteDuplicateLinesAction = new DeleteDuplicateLinesAction();

                    editor.SetSelections([new TextSelection(1, 2, 4, 3), new TextSelection(6, 2, 8, 3)]);
                    ExecuteAction(deleteDuplicateLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), [
                        "alpha",
                        "beta",
                        "omicron",
                        "",
                        "alpha",
                        "beta"
                    ]);
                    TextSelection[] expectedSelections = [
                        new TextSelection(1, 1, 3, 8),
                        new TextSelection(5, 1, 6, 5)
                    ];
                    var actualSelections = editor.GetSelections();
                        for (var index = 0; index < actualSelections.Count; index++)
                        {
                            AssertDeepStrictEqual(actualSelections[index].ToString(), expectedSelections[index].ToString());
                        }
                });
    }


    [Fact]
    public void AppliesToWholeDocumentWhenSelectionIsSingleLine2()
    {
            WithTestCodeEditor(
                [
                    "alpha",
                    "beta",
                    "alpha",
                    "omicron"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var deleteDuplicateLinesAction = new DeleteDuplicateLinesAction();

                    editor.SetSelection(new TextSelection(2, 1, 2, 2));
                    ExecuteAction(deleteDuplicateLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), ["alpha", "beta", "omicron"]);
                });
    }



    // suite: DeleteAllLeftAction
    [Fact]
    public void ShouldDeleteToTheLeftOfTheCursor()
    {
            WithTestCodeEditor(
                [
                    "one",
                    "two",
                    "three"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var deleteAllLeftAction = new DeleteAllLeftAction();

                    editor.SetSelection(new TextSelection(1, 2, 1, 2));
                    ExecuteAction(deleteAllLeftAction, editor);
                    AssertDeepStrictEqual(model.GetLineContent(1), "ne");

                    editor.SetSelections([new TextSelection(2, 2, 2, 2), new TextSelection(3, 2, 3, 2)]);
                    ExecuteAction(deleteAllLeftAction, editor);
                    AssertDeepStrictEqual(model.GetLineContent(2), "wo");
                    AssertDeepStrictEqual(model.GetLineContent(3), "hree");
                });
    }


    [Fact]
    public void ShouldJumpToThePreviousLineWhenOnFirstColumn()
    {
            WithTestCodeEditor(
                [
                    "one",
                    "two",
                    "three"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var deleteAllLeftAction = new DeleteAllLeftAction();

                    editor.SetSelection(new TextSelection(2, 1, 2, 1));
                    ExecuteAction(deleteAllLeftAction, editor);
                    AssertDeepStrictEqual(model.GetLineContent(1), "onetwo");

                    editor.SetSelections([new TextSelection(1, 1, 1, 1), new TextSelection(2, 1, 2, 1)]);
                    ExecuteAction(deleteAllLeftAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent()[0], "onetwothree");
                    AssertDeepStrictEqual(model.GetLinesContent().Length, 1);

                    editor.SetSelection(new TextSelection(1, 1, 1, 1));
                    ExecuteAction(deleteAllLeftAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent()[0], "onetwothree");
                });
    }


    [Fact]
    public void ShouldKeepDeletingLinesInMultiCursorMode()
    {
            WithTestCodeEditor(
                [
                    "hi my name is Carlos Matos",
                    "BCC",
                    "waso waso waso",
                    "my wife doesnt believe in me",
                    "nonononono",
                    "bitconneeeect"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var deleteAllLeftAction = new DeleteAllLeftAction();

                    var beforeSecondWasoSelection = new TextSelection(3, 5, 3, 5);
                    var endOfBCCSelection = new TextSelection(2, 4, 2, 4);
                    var endOfNonono = new TextSelection(5, 11, 5, 11);

                    editor.SetSelections([beforeSecondWasoSelection, endOfBCCSelection, endOfNonono]);

                    ExecuteAction(deleteAllLeftAction, editor);
                    var selections = editor.GetSelections()!;

                    AssertDeepStrictEqual(model.GetLineContent(2), "");
                    AssertDeepStrictEqual(model.GetLineContent(3), " waso waso");
                    AssertDeepStrictEqual(model.GetLineContent(5), "");

                    AssertDeepStrictEqual([
                        selections[0].StartLineNumber,
                        selections[0].StartColumn,
                        selections[0].EndLineNumber,
                        selections[0].EndColumn
                    ], [3, 1, 3, 1]);

                    AssertDeepStrictEqual([
                        selections[1].StartLineNumber,
                        selections[1].StartColumn,
                        selections[1].EndLineNumber,
                        selections[1].EndColumn
                    ], [2, 1, 2, 1]);

                    AssertDeepStrictEqual([
                        selections[2].StartLineNumber,
                        selections[2].StartColumn,
                        selections[2].EndLineNumber,
                        selections[2].EndColumn
                    ], [5, 1, 5, 1]);

                    ExecuteAction(deleteAllLeftAction, editor);
                    selections = editor.GetSelections()!;

                    AssertDeepStrictEqual(model.GetLineContent(1), "hi my name is Carlos Matos waso waso");
                    AssertDeepStrictEqual(selections.Count, 2);

                    AssertDeepStrictEqual([
                        selections[0].StartLineNumber,
                        selections[0].StartColumn,
                        selections[0].EndLineNumber,
                        selections[0].EndColumn
                    ], [1, 27, 1, 27]);

                    AssertDeepStrictEqual([
                        selections[1].StartLineNumber,
                        selections[1].StartColumn,
                        selections[1].EndLineNumber,
                        selections[1].EndColumn
                    ], [2, 29, 2, 29]);
                });
    }


    [Fact]
    public void ShouldWorkInMultiCursorMode()
    {
            WithTestCodeEditor(
                [
                    "hello",
                    "world",
                    "hello world",
                    "hello",
                    "bonjour",
                    "hola",
                    "world",
                    "hello world",
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var deleteAllLeftAction = new DeleteAllLeftAction();

                    editor.SetSelections([new TextSelection(1, 2, 1, 2), new TextSelection(1, 4, 1, 4)]);
                    ExecuteAction(deleteAllLeftAction, editor);
                    AssertDeepStrictEqual(model.GetLineContent(1), "lo");

                    editor.SetSelections([new TextSelection(2, 2, 2, 2), new TextSelection(2, 4, 2, 5)]);
                    ExecuteAction(deleteAllLeftAction, editor);
                    AssertDeepStrictEqual(model.GetLineContent(2), "d");

                    editor.SetSelections([new TextSelection(3, 2, 3, 5), new TextSelection(3, 7, 3, 7)]);
                    ExecuteAction(deleteAllLeftAction, editor);
                    AssertDeepStrictEqual(model.GetLineContent(3), "world");

                    editor.SetSelections([new TextSelection(4, 3, 4, 3), new TextSelection(4, 5, 5, 4)]);
                    ExecuteAction(deleteAllLeftAction, editor);
                    AssertDeepStrictEqual(model.GetLineContent(4), "jour");

                    editor.SetSelections([new TextSelection(5, 3, 6, 3), new TextSelection(6, 5, 7, 5), new TextSelection(7, 7, 7, 7)]);
                    ExecuteAction(deleteAllLeftAction, editor);
                    AssertDeepStrictEqual(model.GetLineContent(5), "world");
                });
    }


    [Fact]
    public void Issue36234ShouldPushUndoStop()
    {
            WithTestCodeEditor(
                [
                    "one",
                    "two",
                    "three"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var deleteAllLeftAction = new DeleteAllLeftAction();

                    editor.SetSelection(new TextSelection(1, 1, 1, 1));

                    editor.Type("Typing some text here on line ");
                    AssertDeepStrictEqual(model.GetLineContent(1), "Typing some text here on line one");
                    AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 31, 1, 31));

                    ExecuteAction(deleteAllLeftAction, editor);
                    AssertDeepStrictEqual(model.GetLineContent(1), "one");
                    AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 1, 1, 1));

                    editor.Undo();
                    AssertDeepStrictEqual(model.GetLineContent(1), "Typing some text here on line one");
                    AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 31, 1, 31));
                });
    }


    // suite: JoinLinesAction
    [Fact]
    public void ShouldJoinLinesAndInsertSpaceIfNecessary()
    {
            WithTestCodeEditor(
                [
                    "hello",
                    "world",
                    "hello ",
                    "world",
                    "hello\t\t",
                    "\tworld",
                    "hello   ",
                    "\tworld",
                    "",
                    "",
                    "hello world"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var joinLinesAction = new JoinLinesAction();

                    editor.SetSelection(new TextSelection(1, 2, 1, 2));
                    ExecuteAction(joinLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLineContent(1), "hello world");
                    AssertSelection(editor, new TextSelection(1, 6, 1, 6));

                    editor.SetSelection(new TextSelection(2, 2, 2, 2));
                    ExecuteAction(joinLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLineContent(2), "hello world");
                    AssertSelection(editor, new TextSelection(2, 7, 2, 7));

                    editor.SetSelection(new TextSelection(3, 2, 3, 2));
                    ExecuteAction(joinLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLineContent(3), "hello world");
                    AssertSelection(editor, new TextSelection(3, 7, 3, 7));

                    editor.SetSelection(new TextSelection(4, 2, 5, 3));
                    ExecuteAction(joinLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLineContent(4), "hello world");
                    AssertSelection(editor, new TextSelection(4, 2, 4, 8));

                    editor.SetSelection(new TextSelection(5, 1, 7, 3));
                    ExecuteAction(joinLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLineContent(5), "hello world");
                    AssertSelection(editor, new TextSelection(5, 1, 5, 3));
                });
    }


    [Fact]
    public void T50471JoinLinesAtTheEndOfDocument()
    {
            WithTestCodeEditor(
                [
                    "hello",
                    "world"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var joinLinesAction = new JoinLinesAction();

                    editor.SetSelection(new TextSelection(2, 1, 2, 1));
                    ExecuteAction(joinLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLineContent(1), "hello");
                    AssertDeepStrictEqual(model.GetLineContent(2), "world");
                    AssertSelection(editor, new TextSelection(2, 6, 2, 6));
                });
    }


    [Fact]
    public void ShouldWorkInMultiCursorMode2()
    {
            WithTestCodeEditor(
                [
                    "hello",
                    "world",
                    "hello ",
                    "world",
                    "hello\t\t",
                    "\tworld",
                    "hello   ",
                    "\tworld",
                    "",
                    "",
                    "hello world"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var joinLinesAction = new JoinLinesAction();

                    editor.SetSelections([
                        /** primary cursor */
                        new TextSelection(5, 2, 5, 2),
                        new TextSelection(1, 2, 1, 2),
                        new TextSelection(3, 2, 4, 2),
                        new TextSelection(5, 4, 6, 3),
                        new TextSelection(7, 5, 8, 4),
                        new TextSelection(10, 1, 10, 1)
                    ]);

                    ExecuteAction(joinLinesAction, editor);
                    AssertDeepStrictEqual(string.Join("\n", model.GetLinesContent()), "hello world\nhello world\nhello world\nhello world\n\nhello world");
                    AssertSelection(editor, [
                        /** primary cursor */
                        new TextSelection(3, 4, 3, 8),
                        new TextSelection(1, 6, 1, 6),
                        new TextSelection(2, 2, 2, 8),
                        new TextSelection(4, 5, 4, 9),
                        new TextSelection(6, 1, 6, 1)
                    ]);
                });
    }


    [Fact]
    public void ShouldPushUndoStop()
    {
            WithTestCodeEditor(
                [
                    "hello",
                    "world"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var joinLinesAction = new JoinLinesAction();

                    editor.SetSelection(new TextSelection(1, 6, 1, 6));

                    editor.Type(" my dear");
                    AssertDeepStrictEqual(model.GetLineContent(1), "hello my dear");
                    AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 14, 1, 14));

                    ExecuteAction(joinLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLineContent(1), "hello my dear world");
                    AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 14, 1, 14));

                    editor.Undo();
                    AssertDeepStrictEqual(model.GetLineContent(1), "hello my dear");
                    AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 14, 1, 14));
                });
    }


    // suite: ReverseLinesAction
    [Fact]
    public void ReversesLines()
    {
            WithTestCodeEditor(
                [
                    "alice",
                    "bob",
                    "charlie",
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var reverseLinesAction = new ReverseLinesAction();

                    ExecuteAction(reverseLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), ["charlie", "bob", "alice"]);
                });
    }


    [Fact]
    public void ExcludesEmptyLastLine()
    {
            WithTestCodeEditor(
                [
                    "alice",
                    "bob",
                    "charlie",
                    "",
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var reverseLinesAction = new ReverseLinesAction();

                    ExecuteAction(reverseLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), ["charlie", "bob", "alice", ""]);
                });
    }


    [Fact]
    public void UpdatesCursor()
    {
            WithTestCodeEditor(
                [
                    "alice",
                    "bob",
                    "charlie",
                ], null, editor => {
                    var reverseLinesAction = new ReverseLinesAction();
                    // cursor at third column of third line 'charlie'
                    editor.SetPosition(new TextPosition(3, 3));

                    ExecuteAction(reverseLinesAction, editor);
                    // cursor at third column of *first* line 'charlie'
                    AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 3));
                });
    }


    [Fact]
    public void PreservesCursorOnEmptyLastLine()
    {
            WithTestCodeEditor(
                [
                    "alice",
                    "bob",
                    "charlie",
                    "",
                ], null, editor => {
                    var reverseLinesAction = new ReverseLinesAction();
                    editor.SetPosition(new TextPosition(4, 1));

                    ExecuteAction(reverseLinesAction, editor);
                    AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(4, 1));
                });
    }


    [Fact]
    public void PreservesSelectedTextWhenSelectionsDoNotSpanLines()
    {
            WithTestCodeEditor(
                [
                    "alice",
                    "bob",
                    "charlie",
                    "",
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var reverseLinesAction = new ReverseLinesAction();
                    editor.SetSelections([new TextSelection(1, 1, 1, 3), new TextSelection(2, 1, 2, 4), new TextSelection(3, 1, 3, 5)]);
                    string[] expectedSelectedText = ["al", "bob", "char"];
                    AssertDeepStrictEqual(editor.GetSelections().Select(s => model.GetValueInRange(s)).ToList(), expectedSelectedText);

                    ExecuteAction(reverseLinesAction, editor);
                    AssertDeepStrictEqual(editor.GetSelections().Select(s => model.GetValueInRange(s)).ToList(), expectedSelectedText);
                });
    }


    [Fact]
    public void ReversesLinesWithinSelection()
    {
            WithTestCodeEditor(
                [
                    "line1",
                    "line2",
                    "line3",
                    "line4",
                    "line5",
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var reverseLinesAction = new ReverseLinesAction();

                    // Select lines 2-4
                    editor.SetSelection(new TextSelection(2, 1, 4, 6));
                    ExecuteAction(reverseLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), ["line1", "line4", "line3", "line2", "line5"]);
                });
    }


    [Fact]
    public void ReversesLinesWithinPartialSelection()
    {
            WithTestCodeEditor(
                [
                    "line1",
                    "line2",
                    "line3",
                    "line4",
                    "line5",
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var reverseLinesAction = new ReverseLinesAction();

                    // Select partial lines 2-4 (from middle of line2 to middle of line4)
                    editor.SetSelection(new TextSelection(2, 3, 4, 3));
                    ExecuteAction(reverseLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), ["line1", "line4", "line3", "line2", "line5"]);
                });
    }


    [Fact]
    public void ReversesLinesWithMultipleSelections()
    {
            WithTestCodeEditor(
                [
                    "line1",
                    "line2",
                    "line3",
                    "line4",
                    "line5",
                    "line6",
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var reverseLinesAction = new ReverseLinesAction();

                    // Select lines 1-2 and lines 4-5
                    editor.SetSelections([new TextSelection(1, 1, 2, 6), new TextSelection(4, 1, 5, 6)]);
                    ExecuteAction(reverseLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), ["line2", "line1", "line3", "line5", "line4", "line6"]);
                });
    }


    [Fact]
    public void UpdatesSelectionPositionsAfterReversal()
    {
            WithTestCodeEditor(
                [
                    "line1",
                    "line2",
                    "line3",
                    "line4",
                ], null, editor => {
                    var reverseLinesAction = new ReverseLinesAction();

                    // Select lines 1-3
                    editor.SetSelection(new TextSelection(1, 2, 3, 3));
                    ExecuteAction(reverseLinesAction, editor);

                    // After reversal, selection should be updated to maintain relative position
                    // Originally line 1 col 2 -> line 3 col 3, so after reversal should be line 3 col 2 -> line 1 col 3
                    var selection = editor.GetSelection()!;
                    // The selection should cover the same logical text after reversal
                    // Range normalization ensures startLineNumber <= endLineNumber
                    AssertDeepStrictEqual(selection.StartLineNumber, 1);
                    AssertDeepStrictEqual(selection.StartColumn, 3);
                    AssertDeepStrictEqual(selection.EndLineNumber, 3);
                    AssertDeepStrictEqual(selection.EndColumn, 2);
                });
    }


    [Fact]
    public void AppliesToWholeDocumentWhenSelectionIsSingleLine3()
    {
            WithTestCodeEditor(
                [
                    "line1",
                    "line2",
                    "line3",
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var reverseLinesAction = new ReverseLinesAction();

                    // Select only line 2
                    editor.SetSelection(new TextSelection(2, 1, 2, 6));
                    ExecuteAction(reverseLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), ["line3", "line2", "line1"]);
                });
    }


    [Fact]
    public void ExcludesEndLineWhenSelectionEndsAtColumn1()
    {
            WithTestCodeEditor(
                [
                    "line1",
                    "line2",
                    "line3",
                    "line4",
                    "line5",
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var reverseLinesAction = new ReverseLinesAction();

                    // Select from line 2 to line 4 column 1 (should exclude line 4)
                    editor.SetSelection(new TextSelection(2, 1, 4, 1));
                    ExecuteAction(reverseLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), ["line1", "line3", "line2", "line4", "line5"]);
                });
    }


    [Fact]
    public void AppliesToWholeDocumentWhenSelectionIsSingleLine4()
    {
            WithTestCodeEditor(
                [
                    "omicron",
                    "beta",
                    "alpha"
                ], null, editor => {
                    var model = editor.GetModel()!;
                    var reverseLinesAction = new ReverseLinesAction();

                    editor.SetSelection(new TextSelection(2, 1, 2, 4));
                    ExecuteAction(reverseLinesAction, editor);
                    AssertDeepStrictEqual(model.GetLinesContent(), [
                        "alpha",
                        "beta",
                        "omicron"
                    ]);
                });
    }


    [Fact]
    public void Transpose()
    {
        WithTestCodeEditor(
            [
                "hello world",
                "",
                "",
                "   ",
            ], null, editor => {
                var model = editor.GetModel()!;
                var transposeAction = new TransposeAction();

                editor.SetSelection(new TextSelection(1, 1, 1, 1));
                ExecuteAction(transposeAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(1), "hello world");
                AssertSelection(editor, new TextSelection(1, 2, 1, 2));

                editor.SetSelection(new TextSelection(1, 6, 1, 6));
                ExecuteAction(transposeAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(1), "hell oworld");
                AssertSelection(editor, new TextSelection(1, 7, 1, 7));

                editor.SetSelection(new TextSelection(1, 12, 1, 12));
                ExecuteAction(transposeAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(1), "hell oworl");
                AssertSelection(editor, new TextSelection(2, 2, 2, 2));

                editor.SetSelection(new TextSelection(3, 1, 3, 1));
                ExecuteAction(transposeAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(3), "");
                AssertSelection(editor, new TextSelection(4, 1, 4, 1));

                editor.SetSelection(new TextSelection(4, 2, 4, 2));
                ExecuteAction(transposeAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(4), "   ");
                AssertSelection(editor, new TextSelection(4, 3, 4, 3));
            }
        );

        // fix #16633
        WithTestCodeEditor(
            [
                "",
                "",
                "hello",
                "world",
                "",
                "hello world",
                "",
                "hello world"
            ], null, editor => {
                var model = editor.GetModel()!;
                var transposeAction = new TransposeAction();

                editor.SetSelection(new TextSelection(1, 1, 1, 1));
                ExecuteAction(transposeAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(2), "");
                AssertSelection(editor, new TextSelection(2, 1, 2, 1));

                editor.SetSelection(new TextSelection(3, 6, 3, 6));
                ExecuteAction(transposeAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(4), "oworld");
                AssertSelection(editor, new TextSelection(4, 2, 4, 2));

                editor.SetSelection(new TextSelection(6, 12, 6, 12));
                ExecuteAction(transposeAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(7), "d");
                AssertSelection(editor, new TextSelection(7, 2, 7, 2));

                editor.SetSelection(new TextSelection(8, 12, 8, 12));
                ExecuteAction(transposeAction, editor);
                AssertDeepStrictEqual(model.GetLineContent(8), "hello world");
                AssertSelection(editor, new TextSelection(8, 12, 8, 12));
            }
        );
    }
}
