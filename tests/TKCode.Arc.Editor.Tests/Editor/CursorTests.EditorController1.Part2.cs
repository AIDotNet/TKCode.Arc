// Ported from vscode (src/vs/editor/test/browser/controller/cursor.test.ts, commit b57d5c9e), MIT license.
// Suite 'Editor Controller', second part of CursorTests_EditorController1: from 'issue #23983' to 'issue #122914'. The
// helpers and the adaptations are in CursorTests.EditorController1.cs.

using Shouldly;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed partial class CursorTests_EditorController1
{
    [Fact]
    public void Issue_23983_Calling_Model_SetEOL_Does_Not_Reset_Cursor_Position()
    {
        using var editor = UsingCursor(["first line", "second line"]);
        var viewModel = editor.ViewModel;
        var model = editor.Model;

        model.SetEOL(EndOfLineSequence.CRLF);

        viewModel.SetSelections("test", [S(2, 2, 2, 2)]);
        model.SetEOL(EndOfLineSequence.LF);

        AssertCursor(viewModel, S(2, 2, 2, 2));
    }

    [Fact]
    public void Issue_23983_Calling_Model_SetValue_Resets_Cursor_Position()
    {
        using var editor = UsingCursor(["first line", "second line"]);
        var viewModel = editor.ViewModel;
        var model = editor.Model;

        model.SetEOL(EndOfLineSequence.CRLF);

        viewModel.SetSelections("test", [S(2, 2, 2, 2)]);
        model.SetValue(Lines("different first line", "different second line", "new third line"));

        AssertCursor(viewModel, S(1, 1, 1, 1));
    }

    private static TestCodeEditorOptions WordWrapColumn(int column, EditorOptions? editorOptions = null) => new()
    {
        WordWrap = TestWordWrap.WordWrapColumn,
        WordWrapColumn = column,
        EditorOptions = editorOptions ?? EditorOptions.Default,
    };

    [Fact]
    public void Issue_36740_Wordwrap_Creates_An_Extra_Step_Character_At_The_Wrapping_Point()
    {
        // a single model line => 4 view lines
        using var editor = WithTestCodeEditor(
            [string.Concat("Lorem ipsum ", "dolor sit amet ", "consectetur ", "adipiscing elit")],
            WordWrapColumn(16));
        var viewModel = editor.ViewModel;

        viewModel.SetSelections("test", [S(1, 7, 1, 7)]);

        MoveRight(viewModel);
        AssertCursor(viewModel, S(1, 8, 1, 8));

        MoveRight(viewModel);
        AssertCursor(viewModel, S(1, 9, 1, 9));

        MoveRight(viewModel);
        AssertCursor(viewModel, S(1, 10, 1, 10));

        MoveRight(viewModel);
        AssertCursor(viewModel, S(1, 11, 1, 11));

        MoveRight(viewModel);
        AssertCursor(viewModel, S(1, 12, 1, 12));

        MoveRight(viewModel);
        AssertCursor(viewModel, S(1, 13, 1, 13));

        // moving to view line 2
        MoveRight(viewModel);
        AssertCursor(viewModel, S(1, 14, 1, 14));

        MoveLeft(viewModel);
        AssertCursor(viewModel, S(1, 13, 1, 13));

        // moving back to view line 1
        MoveLeft(viewModel);
        AssertCursor(viewModel, S(1, 12, 1, 12));
    }

    [Fact]
    public void Issue_110376_Multiple_Selections_With_Wordwrap_Behave_Differently()
    {
        // a single model line => 4 view lines
        using var editor = WithTestCodeEditor(
            [string.Concat("just a sentence. just a ", "sentence. just a sentence.")],
            WordWrapColumn(25));
        var viewModel = editor.ViewModel;

        viewModel.SetSelections("test", [
            S(1, 1, 1, 16),
            S(1, 18, 1, 33),
            S(1, 35, 1, 50),
        ]);

        MoveLeft(viewModel);
        AssertCursor(viewModel,
            S(1, 1, 1, 1),
            S(1, 18, 1, 18),
            S(1, 35, 1, 35));

        viewModel.SetSelections("test", [
            S(1, 1, 1, 16),
            S(1, 18, 1, 33),
            S(1, 35, 1, 50),
        ]);

        MoveRight(viewModel);
        AssertCursor(viewModel,
            S(1, 16, 1, 16),
            S(1, 33, 1, 33),
            S(1, 50, 1, 50));
    }

    [Fact]
    public void Issue_98320_Multi_Cursor_Wrap_Lines_And_CursorSelectRight_Cursors_Out_Of_Sync()
    {
        // a single model line => 4 view lines
        using var editor = WithTestCodeEditor(
            [Lines(
                "lorem_ipsum-1993x11x13",
                "dolor_sit_amet-1998x04x27",
                "consectetur-2007x10x08",
                "adipiscing-2012x07x27",
                "elit-2015x02x27")],
            WordWrapColumn(16));
        var viewModel = editor.ViewModel;

        viewModel.SetSelections("test", [
            S(1, 13, 1, 13),
            S(2, 16, 2, 16),
            S(3, 13, 3, 13),
            S(4, 12, 4, 12),
            S(5, 6, 5, 6),
        ]);
        AssertCursor(viewModel,
            S(1, 13, 1, 13),
            S(2, 16, 2, 16),
            S(3, 13, 3, 13),
            S(4, 12, 4, 12),
            S(5, 6, 5, 6));

        MoveRight(viewModel, true);
        AssertCursor(viewModel,
            S(1, 13, 1, 14),
            S(2, 16, 2, 17),
            S(3, 13, 3, 14),
            S(4, 12, 4, 13),
            S(5, 6, 5, 7));

        MoveRight(viewModel, true);
        AssertCursor(viewModel,
            S(1, 13, 1, 15),
            S(2, 16, 2, 18),
            S(3, 13, 3, 15),
            S(4, 12, 4, 14),
            S(5, 6, 5, 8));

        MoveRight(viewModel, true);
        AssertCursor(viewModel,
            S(1, 13, 1, 16),
            S(2, 16, 2, 19),
            S(3, 13, 3, 16),
            S(4, 12, 4, 15),
            S(5, 6, 5, 9));

        MoveRight(viewModel, true);
        AssertCursor(viewModel,
            S(1, 13, 1, 17),
            S(2, 16, 2, 20),
            S(3, 13, 3, 17),
            S(4, 12, 4, 16),
            S(5, 6, 5, 10));
    }

    [Fact]
    public void Issue_41573_Delete_Across_Multiple_Lines_Does_Not_Shrink_The_Selection_When_Word_Wraps()
    {
        using var editor = WithTestCodeEditor(
            Lines("Authorization: 'Bearer pHKRfCTFSnGxs6akKlb9ddIXcca0sIUSZJutPHYqz7vEeHdMTMh0SGN0IGU3a0n59DXjTLRsj5EJ2u33qLNIFi9fk5XF8pK39PndLYUZhPt4QvHGLScgSkK0L4gwzkzMloTQPpKhqiikiIOvyNNSpd2o8j29NnOmdTUOKi9DVt74PD2ohKxyOrWZ6oZprTkb3eKajcpnS0LABKfaw2rmv4',"),
            WordWrapColumn(100));
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 43, false);
        MoveTo(viewModel, 1, 147, true);
        AssertCursor(viewModel, S(1, 43, 1, 147));

        editor.GetModel().ApplyEdits([new SingleEditOperation(new TextRange(1, 1, 1, 43), "")]);

        AssertCursor(viewModel, S(1, 1, 1, 105));
    }

    [Fact]
    public void Issue_22717_Moving_Text_Cursor_Cause_An_Incorrect_Position_In_Chinese()
    {
        // a single model line => 4 view lines
        using var editor = WithTestCodeEditor([Lines("一二三四五六七八九十", "12345678901234567890")]);
        var viewModel = editor.ViewModel;

        viewModel.SetSelections("test", [S(1, 5, 1, 5)]);

        MoveDown(viewModel);
        AssertCursor(viewModel, S(2, 9, 2, 9));

        MoveRight(viewModel);
        AssertCursor(viewModel, S(2, 10, 2, 10));

        MoveRight(viewModel);
        AssertCursor(viewModel, S(2, 11, 2, 11));

        MoveUp(viewModel);
        AssertCursor(viewModel, S(1, 6, 1, 6));
    }

    [Fact]
    public void Issue_112301_New_StickyTabStops_Feature_Interferes_With_Word_Wrap()
    {
        using var editor = WithTestCodeEditor(
            [Lines("function hello() {", "        console.log(`this is a long console message`)", "}")],
            WordWrapColumn(32, EditorOptions.Default with { StickyTabStops = true }));
        var viewModel = editor.ViewModel;

        viewModel.SetSelections("test", [S(2, 31, 2, 31)]);
        MoveRight(viewModel, false);
        AssertCursor(viewModel, P(2, 32));

        MoveRight(viewModel, false);
        AssertCursor(viewModel, P(2, 33));

        MoveRight(viewModel, false);
        AssertCursor(viewModel, P(2, 34));

        MoveLeft(viewModel, false);
        AssertCursor(viewModel, P(2, 33));

        MoveLeft(viewModel, false);
        AssertCursor(viewModel, P(2, 32));

        MoveLeft(viewModel, false);
        AssertCursor(viewModel, P(2, 31));
    }

    [Fact]
    public async Task Issue_44805_Should_Not_Be_Able_To_Undo_In_Readonly_Editor()
    {
        var model = CreateTextModel(Lines(""));

        using var editor = WithTestCodeEditor(model, EditorOpts(EditorOptions.Default with { ReadOnly = true }));

        model.PushEditOperations(
            [S(1, 1, 1, 1)],
            [new SingleEditOperation(new TextRange(1, 1, 1, 1), "Hello world!")],
            _ => [S(1, 1, 1, 1)]);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("Hello world!");

        await Undo(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("Hello world!");
    }

    [Fact]
    public void Issue_46314_ViewModel_Is_Out_Of_Sync_With_Model()
    {
        const string languageId = "modelModeTest1";
        var model = CreateTextModel("Just text", languageId);
        using var registration = TestTokenizationSupports.RegisterByLine(model, (_, _) => []);

        using (var editor1 = WithTestCodeEditor(model, new TestCodeEditorOptions { EditorId = 1 }))
        {
            using (var editor2 = WithTestCodeEditor(model, new TestCodeEditorOptions { EditorId = 2 }))
            {
                void OnDidChangeCursorPosition(OutgoingViewModelEvent e)
                {
                    if (e is CursorStateChangedEvent)
                    {
                        // model.tokenization.tokenizeIfCheap(1)
                        if (model.Tokenization.IsCheapToTokenize(1))
                        {
                            model.Tokenization.ForceTokenization(1);
                        }
                    }
                }
                editor1.ViewModel.OutgoingEvent += OnDidChangeCursorPosition;

                model.ApplyEdits([new SingleEditOperation(new TextRange(1, 1, 1, 1), "-")]);

                editor1.ViewModel.OutgoingEvent -= OnDidChangeCursorPosition;
            }
        }

    }

    [Fact]
    public void Issue_37967_Problem_Replacing_Consecutive_Characters()
    {
        var model = CreateTextModel(Lines("const a = \"foo\";", "const b = \"\""));

        using var editor = WithTestCodeEditor(model, EditorOpts(EditorOptions.Default with { MultiCursorMergeOverlapping = false }));
        var viewModel = editor.ViewModel;

        editor.SetSelections([
            S(1, 12, 1, 12),
            S(1, 16, 1, 16),
            S(2, 12, 2, 12),
            S(2, 13, 2, 13),
        ]);

        DeleteLeft(editor);

        AssertCursor(viewModel,
            S(1, 11, 1, 11),
            S(1, 14, 1, 14),
            S(2, 11, 2, 11),
            S(2, 11, 2, 11));

        viewModel.Type("'", "keyboard");

        model.GetLineContent(1).ShouldBe("const a = 'foo';");
        model.GetLineContent(2).ShouldBe("const b = ''");
    }

    [Fact]
    public async Task Issue_15761_Cursor_Doesnt_Move_In_A_Redo_Operation()
    {
        var model = CreateTextModel(Lines("hello"));

        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        editor.SetSelections([S(1, 4, 1, 4)]);

        editor.ExecuteEdits("test", [new SingleEditOperation(new TextRange(1, 1, 1, 1), "*", ForceMoveMarkers: true)]);
        AssertCursor(viewModel, S(1, 5, 1, 5));

        await Undo(editor);
        AssertCursor(viewModel, S(1, 4, 1, 4));

        await Redo(editor);
        AssertCursor(viewModel, S(1, 5, 1, 5));
    }

    [Fact]
    public async Task Issue_42783_API_Calls_With_Undo_Leave_Cursor_In_Wrong_Position()
    {
        var model = CreateTextModel(Lines("ab"));

        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        editor.SetSelections([S(1, 1, 1, 1)]);

        editor.ExecuteEdits("test", [new SingleEditOperation(new TextRange(1, 1, 1, 3), "")]);
        AssertCursor(viewModel, S(1, 1, 1, 1));

        await Undo(editor);
        AssertCursor(viewModel, S(1, 1, 1, 1));

        editor.ExecuteEdits("test", [new SingleEditOperation(new TextRange(1, 1, 1, 2), "")]);
        AssertCursor(viewModel, S(1, 1, 1, 1));
    }

    [Fact]
    public void Issue_85712_Paste_Line_Moves_Cursor_To_Start_Of_Current_Line_Rather_Than_Start_Of_Next_Line()
    {
        var model = CreateTextModel(Lines("abc123", ""));

        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        editor.SetSelections([S(2, 1, 2, 1)]);
        viewModel.Paste("something\n", true);
        model.GetValue().ShouldBe(Lines("abc123", "something", ""));
        AssertCursor(viewModel, P(3, 1));
    }

    // leftDeleteCases

    private void LeftDeleteCase(TestCodeEditorOptions options)
    {
        var model = CreateTextModel(Lines("สวัสดี"));

        using var editor = WithTestCodeEditor(model, options);

        editor.SetSelections([S(1, 7, 1, 7)]);

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("สวัสด");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("สวัส");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("สวั");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("สว");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("ส");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("");
    }

    [Fact]
    public void Issue_84897_Left_Delete_Behavior_In_Some_Languages_Is_Changed() =>
        LeftDeleteCase(new TestCodeEditorOptions());

    [Fact]
    public void Issue_122914_Left_Delete_Behavior_In_Some_Languages_Is_Changed_UseTabStops_False() =>
        LeftDeleteCase(EditorOpts(EditorOptions.Default with { UseTabStops = false }));
}
