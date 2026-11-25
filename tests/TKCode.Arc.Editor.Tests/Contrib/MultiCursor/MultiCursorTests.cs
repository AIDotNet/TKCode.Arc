// Ported from vscode (src/vs/editor/contrib/multicursor/test/browser/multicursor.test.ts, commit b57d5c9e), MIT license.
// Differences: the editor is TestMultiCursorEditor (an IMultiCursorEditor over the test code editor); the actions are
// MultiCursorActions' methods; sinon's spy on highlightFindOptions is the state-only find controller's counter and its
// fake timers are the test editor's queued timers. Not ported: 'a selection session does not survive replacing the
// editor model' (an editor here keeps its model).

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Contrib.MultiCursor;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class MultiCursorTests
{
    private static TextSelection Sel(int a, int b, int c, int d) => new(a, b, c, d);

    private static string F(IEnumerable<TextSelection> selections) => string.Join(" ", selections.Select(static s => $"[{s.StartLineNumber},{s.StartColumn},{s.EndLineNumber},{s.EndColumn}]"));

    private static string F(IEnumerable<TextRange> ranges) => string.Join(" ", ranges.Select(static s => $"[{s.StartLineNumber},{s.StartColumn},{s.EndLineNumber},{s.EndColumn}]"));

    private static string L(params int[][] ranges) => string.Join(" ", ranges.Select(static r => $"[{r[0]},{r[1]},{r[2]},{r[3]}]"));

    private static string Lines(params int[] lines) => string.Join(" ", lines.Select(static l => $"[{l},1,{l},4]"));

    private static void TestMulticursor(string[] text, Action<TestMultiCursorEditor, StandaloneMultiCursorFindController> callback, MultiCursorOptions? options = null, bool hasTextFocus = true, TestCodeEditorOptions? editorOptions = null)
    {
        using var editor = new TestMultiCursorEditor(TestCodeEditor.Create(text, editorOptions), options, hasTextFocus);
        callback(editor, editor.FindControllerImpl);
    }

    private static MultiCursorOptions Mode(SelectedTextMatchMode mode) => MultiCursorOptions.Default with { SelectedTextMatchMode = mode };

    // ---- suite 'Multicursor'

    [Fact]
    public void Issue_26393_Multiple_Cursors_And_Word_Wrap()
    {
        TestMulticursor(
            [new string('a', 20), new string('a', 20)],
            (editor, _) =>
            {
                var viewModel = editor.Editor.ViewModel;
                MultiCursorActions.InsertCursorBelow(editor);

                viewModel.GetCursorStates().Count.ShouldBe(2);
                viewModel.GetCursorStates()[0].ViewState.Position.LineNumber.ShouldBe(1);
                viewModel.GetCursorStates()[1].ViewState.Position.LineNumber.ShouldBe(3);

                editor.Editor.SetPosition(new TextPosition(4, 1));
                MultiCursorActions.InsertCursorAbove(editor);

                viewModel.GetCursorStates().Count.ShouldBe(2);
                viewModel.GetCursorStates()[0].ViewState.Position.LineNumber.ShouldBe(4);
                viewModel.GetCursorStates()[1].ViewState.Position.LineNumber.ShouldBe(2);
            },
            editorOptions: new TestCodeEditorOptions { WordWrap = TestWordWrap.WordWrapColumn, WordWrapColumn = 10 });
    }

    [Fact]
    public void Issue_2205_Multi_Cursor_Pastes_In_Reverse_Order()
    {
        TestMulticursor(["abc", "def"], (editor, _) =>
        {
            editor.SetSelection(Sel(2, 1, 2, 1));
            MultiCursorActions.InsertCursorAbove(editor);
            editor.GetSelections().Count.ShouldBe(2);

            editor.Editor.Paste("1\n2", multicursorText: ["1", "2"], source: "test");

            editor.Model!.GetLineContent(1).ShouldBe("1abc");
            editor.Model.GetLineContent(2).ShouldBe("2def");
        });
    }

    [Fact]
    public void Issue_1336_Insert_Cursor_Below_On_Last_Line_Adds_A_Cursor_To_The_End_Of_The_Current_Line()
    {
        TestMulticursor(["abc"], (editor, _) =>
        {
            MultiCursorActions.InsertCursorBelow(editor);
            editor.GetSelections().Count.ShouldBe(1);
        });
    }

    // ---- suite 'Multicursor selection'

    [Fact]
    public void Issue_8817_Cursor_Position_Changes_When_You_Cancel_Multicursor()
    {
        TestMulticursor(["var x = (3 * 5)", "var y = (3 * 5)", "var z = (3 * 5)"], (editor, _) =>
        {
            editor.SetSelection(Sel(2, 9, 2, 16));

            MultiCursorActions.SelectHighlights(editor);
            F(editor.GetSelections()).ShouldBe(L([2, 9, 2, 16], [1, 9, 1, 16], [3, 9, 3, 16]));

            CoreNavigationCommands.RemoveSecondaryCursors.RunCoreEditorCommand(editor.Editor.ViewModel, new());

            F([editor.GetSelection()]).ShouldBe(L([2, 9, 2, 16]));
        });
    }

    [Fact]
    public void Issue_5400_Select_All_Occurrences_Of_Find_Match_Does_Not_Select_All_If_Find_Uses_Regex()
    {
        TestMulticursor(
            ["something", "someething", "someeething", "nothing"],
            (editor, findController) =>
            {
                editor.SetSelection(Sel(1, 1, 1, 1));
                findController.GetState().Change(new NewFindState { SearchString = "some+thing", IsRegex = true, IsRevealed = true }, false);

                MultiCursorActions.SelectHighlights(editor);
                F(editor.GetSelections()).ShouldBe(L([1, 1, 1, 10], [2, 1, 2, 11], [3, 1, 3, 12]));

                findController.GetState().SearchString.ShouldBe("some+thing");
            },
            hasTextFocus: false);
    }

    [Fact]
    public void AddSelectionToNextFindMatchAction_Can_Work_With_Multiline()
    {
        TestMulticursor(["", "qwe", "rty", "", "qwe", "", "rty", "qwe", "rty"], (editor, _) =>
        {
            editor.SetSelection(Sel(2, 1, 3, 4));

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            F(editor.GetSelections()).ShouldBe(L([2, 1, 3, 4], [8, 1, 9, 4]));

            CoreNavigationCommands.RemoveSecondaryCursors.RunCoreEditorCommand(editor.Editor.ViewModel, new());
            F([editor.GetSelection()]).ShouldBe(L([2, 1, 3, 4]));
        });
    }

    [Fact]
    public void Issue_6661_AddSelectionToNextFindMatchAction_Can_Work_With_Touching_Ranges()
    {
        TestMulticursor(["abcabc", "abc", "abcabc"], (editor, _) =>
        {
            editor.SetSelection(Sel(1, 1, 1, 4));

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            F(editor.GetSelections()).ShouldBe(L([1, 1, 1, 4], [1, 4, 1, 7]));

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            F(editor.GetSelections()).ShouldBe(L([1, 1, 1, 4], [1, 4, 1, 7], [2, 1, 2, 4], [3, 1, 3, 4], [3, 4, 3, 7]));

            editor.Editor.Type("z", "test");
            F(editor.GetSelections()).ShouldBe(L([1, 2, 1, 2], [1, 3, 1, 3], [2, 2, 2, 2], [3, 2, 3, 2], [3, 3, 3, 3]));
            editor.Editor.GetValue().ShouldBe("zz\nz\nzz");
        });
    }

    [Fact]
    public void Issue_23541_Multiline_Ctrl_D_Does_Not_Work_In_CRLF_Files()
    {
        TestMulticursor(["", "qwe", "rty", "", "qwe", "", "rty", "qwe", "rty"], (editor, _) =>
        {
            editor.Model!.SetEOL(EndOfLineSequence.CRLF);
            editor.SetSelection(Sel(2, 1, 3, 4));

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            F(editor.GetSelections()).ShouldBe(L([2, 1, 3, 4], [8, 1, 9, 4]));

            CoreNavigationCommands.RemoveSecondaryCursors.RunCoreEditorCommand(editor.Editor.ViewModel, new());
            F([editor.GetSelection()]).ShouldBe(L([2, 1, 3, 4]));
        });
    }

    private static readonly string[] s_pizza = ["abc pizza", "abc house", "abc bar"];

    [Fact]
    public void AddSelectionToNextFindMatchAction_Starting_With_Single_Collapsed_Selection()
    {
        TestMulticursor(s_pizza, (editor, _) =>
        {
            editor.SetSelections([Sel(1, 2, 1, 2)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(2, 1, 2, 4)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(2, 1, 2, 4), Sel(3, 1, 3, 4)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(2, 1, 2, 4), Sel(3, 1, 3, 4)]);
        });
    }

    [Fact]
    public void AddSelectionToNextFindMatchAction_Starting_With_Two_Selections_One_Being_Collapsed_1()
    {
        TestMulticursor(s_pizza, (editor, _) =>
        {
            editor.SetSelections([Sel(1, 1, 1, 4), Sel(2, 2, 2, 2)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(2, 1, 2, 4)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(2, 1, 2, 4), Sel(3, 1, 3, 4)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(2, 1, 2, 4), Sel(3, 1, 3, 4)]);
        });
    }

    [Fact]
    public void AddSelectionToNextFindMatchAction_Starting_With_Two_Selections_One_Being_Collapsed_2()
    {
        TestMulticursor(s_pizza, (editor, _) =>
        {
            editor.SetSelections([Sel(1, 2, 1, 2), Sel(2, 1, 2, 4)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(2, 1, 2, 4)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(2, 1, 2, 4), Sel(3, 1, 3, 4)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(2, 1, 2, 4), Sel(3, 1, 3, 4)]);
        });
    }

    [Fact]
    public void AddSelectionToNextFindMatchAction_Starting_With_All_Collapsed_Selections()
    {
        TestMulticursor(s_pizza, (editor, _) =>
        {
            editor.SetSelections([Sel(1, 2, 1, 2), Sel(2, 2, 2, 2), Sel(3, 1, 3, 1)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(2, 1, 2, 4), Sel(3, 1, 3, 4)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 4), Sel(2, 1, 2, 4), Sel(3, 1, 3, 4)]);
        });
    }

    [Fact]
    public void AddSelectionToNextFindMatchAction_Starting_With_All_Collapsed_Selections_On_Different_Words()
    {
        TestMulticursor(s_pizza, (editor, _) =>
        {
            editor.SetSelections([Sel(1, 6, 1, 6), Sel(2, 6, 2, 6), Sel(3, 6, 3, 6)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 5, 1, 10), Sel(2, 5, 2, 10), Sel(3, 5, 3, 8)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 5, 1, 10), Sel(2, 5, 2, 10), Sel(3, 5, 3, 8)]);
        });
    }

    [Fact]
    public void Issue_20651_AddSelectionToNextFindMatchAction_Case_Insensitive()
    {
        TestMulticursor(["test", "testte", "Test", "testte", "test"], (editor, _) =>
        {
            editor.SetSelections([Sel(1, 1, 1, 5)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 5), Sel(2, 1, 2, 5)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 5), Sel(2, 1, 2, 5), Sel(3, 1, 3, 5)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 5), Sel(2, 1, 2, 5), Sel(3, 1, 3, 5), Sel(4, 1, 4, 5)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 5), Sel(2, 1, 2, 5), Sel(3, 1, 3, 5), Sel(4, 1, 4, 5), Sel(5, 1, 5, 5)]);

            MultiCursorActions.AddSelectionToNextFindMatch(editor);
            editor.GetSelections().ShouldBe([Sel(1, 1, 1, 5), Sel(2, 1, 2, 5), Sel(3, 1, 3, 5), Sel(4, 1, 4, 5), Sel(5, 1, 5, 5)]);
        });
    }
}
