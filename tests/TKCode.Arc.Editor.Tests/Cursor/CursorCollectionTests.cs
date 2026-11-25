// Tests for CursorCollection and OneCursor, and for the CursorMoveCommands that place cursors (moveTo, selectAll,
// word, line, addCursorUp/Down, ...), run on the view model of a TestCodeEditor through the engine's
// CoreNavigationCommands (and ContribCursorCommandsStandIn for the multicursor and line selection contributions).
// Ported from vscode (commit b57d5c9e, MIT license):
//  - src/vs/editor/test/browser/controller/cursor.test.ts, suite 'Editor Controller - Cursor': 'cursor initialized',
//    the absolute move cases, 'move and then select', 'select all', 'no move doesn't trigger event', 'move eventing',
//    'move in selection mode eventing', 'saveState & restoreState' and 'Independent model edit 1'; suite
//    'Editor Controller': 'issue #7100', 'issue #112039', 'issue #158236' and 'issue #111513';
//  - src/vs/editor/contrib/multicursor/test/browser/multicursor.test.ts: 'issue #1336'.
// Differences: the eventing tests count the view model's outgoing cursor events (TestViewModel.CursorStateChangedCount,
// upstream's editor.onDidChangeCursorPosition); saveState and restoreState keep SavedCursorState records instead of a
// JSON state.
// Not ported: 'setSelection / setPosition with source' (editor events), the cursor up/down issues with wrapping
// (#144041, #140195), the column select tests (CursorColumnSelectionTests has them), 'issue #26393' (wrapping) and
// 'issue #2205' (paste). The tests after 'issue #1336' are not upstream's: they cover the cursor merging of
// CursorCollection.normalize, the last added cursor, the multi cursor limit, marker tracking and the remaining
// CursorMoveCommands entry points (the top and bottom most cursors through the reveal requests they make).

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.ViewModel;
using Xunit;
using static TKCode.Arc.Editor.Tests.ContribCursorCommandsStandIn;

namespace TKCode.Arc.Editor.Tests;

public sealed class CursorCollectionTests
{
    private const string Line1 = "    \tMy First Line\t ";
    private const string Line2 = "\tMy Second Line";
    private const string Line3 = "    Third Line🐶";
    private const string Line4 = "";
    private const string Line5 = "1";

    private const string Text = Line1 + "\r\n" + Line2 + "\n" + Line3 + "\n" + Line4 + "\r\n" + Line5;

    private static void RunTest(Action<TestViewModel> callback)
    {
        using var editor = TestCodeEditor.Create(Text);
        var viewModel = editor.ViewModel;
        callback(viewModel);
    }

    private static void AssertCursor(TestViewModel viewModel, params TextSelection[] expected) =>
        viewModel.GetSelections().Select(s => s.ToString()).ShouldBe(expected.Select(s => s.ToString()));

    private static void AssertCursor(TestViewModel viewModel, TextPosition expected) =>
        AssertCursor(viewModel, new TextSelection(expected.LineNumber, expected.Column, expected.LineNumber, expected.Column));

    private static void MoveTo(TestViewModel viewModel, int lineNumber, int column, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.MoveToSelect : CoreNavigationCommands.MoveTo).RunCoreEditorCommand(viewModel, new() { Position = new TextPosition(lineNumber, column) });

    private static TextSelection S(int a, int b, int c, int d) => new(a, b, c, d);

    private static TextPosition P(int lineNumber, int column) => new(lineNumber, column);

    [Fact]
    public void Cursor_Initialized() => RunTest(viewModel => AssertCursor(viewModel, P(1, 1)));

    // --------- absolute move

    [Theory]
    [InlineData("no move", 1, 1, false, 1, 1, 1, 1)]
    [InlineData("move", 1, 2, false, 1, 2, 1, 2)]
    [InlineData("move in selection mode", 1, 2, true, 1, 1, 1, 2)]
    [InlineData("move beyond line end", 1, 25, false, 1, 21, 1, 21)]
    [InlineData("move empty line", 4, 20, false, 4, 1, 4, 1)]
    [InlineData("move one char line", 5, 20, false, 5, 2, 5, 2)]
    [InlineData("selection down", 2, 1, true, 1, 1, 2, 1)]
    public void Absolute_Move_Cases(
        string name, int lineNumber, int column, bool inSelectionMode, int selStartLine, int selStartColumn, int posLine, int posColumn)
    {
        _ = name;
        RunTest(viewModel =>
        {
            MoveTo(viewModel, lineNumber, column, inSelectionMode);
            AssertCursor(viewModel, S(selStartLine, selStartColumn, posLine, posColumn));
        });
    }

    [Fact]
    public void Move_And_Then_Select() => RunTest(viewModel =>
    {
        MoveTo(viewModel, 2, 3);
        AssertCursor(viewModel, P(2, 3));

        MoveTo(viewModel, 2, 15, true);
        AssertCursor(viewModel, S(2, 3, 2, 15));

        MoveTo(viewModel, 1, 2, true);
        AssertCursor(viewModel, S(2, 3, 1, 2));
    });

    // --------- misc

    [Fact]
    public void Select_All() => RunTest(viewModel =>
    {
        CoreNavigationCommands.SelectAll.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, S(1, 1, 5, Line5.Length + 1));
    });

    // --------- eventing

    [Fact]
    public void No_Move_Does_Not_Trigger_Event() => RunTest(viewModel =>
    {
        MoveTo(viewModel, 1, 1);
        viewModel.CursorStateChangedCount.ShouldBe(0, "was not expecting event");
    });

    [Fact]
    public void Move_Eventing() => RunTest(viewModel =>
    {
        MoveTo(viewModel, 1, 2);
        viewModel.CursorStateChangedCount.ShouldBe(1, "receives 1 event");
        viewModel.GetSelections().ShouldBe([S(1, 2, 1, 2)]);
    });

    [Fact]
    public void Move_In_Selection_Mode_Eventing() => RunTest(viewModel =>
    {
        MoveTo(viewModel, 1, 2, true);
        viewModel.CursorStateChangedCount.ShouldBe(1, "receives 1 event");
        viewModel.GetSelections().ShouldBe([S(1, 1, 1, 2)]);
    });

    // --------- state save & restore

    [Fact]
    public void SaveState_And_RestoreState() => RunTest(viewModel =>
    {
        MoveTo(viewModel, 2, 1, true);
        AssertCursor(viewModel, S(1, 1, 2, 1));

        var savedState = viewModel.SaveCursorState();

        MoveTo(viewModel, 1, 1, false);
        AssertCursor(viewModel, P(1, 1));

        viewModel.RestoreCursorState(savedState);
        AssertCursor(viewModel, S(1, 1, 2, 1));
    });

    // --------- updating cursor

    [Fact]
    public void Independent_Model_Edit_1() => RunTest(viewModel =>
    {
        MoveTo(viewModel, 2, 16, true);

        viewModel.Model.ApplyEdits([EditOperation.Delete(new TextRange(2, 1, 2, 2))]);
        AssertCursor(viewModel, S(1, 1, 2, 15));
        viewModel.LastChangeReason.ShouldBe(CursorChangeReason.RecoverFromMarkers);
    });

    // --------- suite 'Editor Controller'

    [Fact]
    public void Issue_7100_Mouse_Word_Selection_Is_Strange_When_Non_Word_Character_Is_At_The_End_Of_Line()
    {
        using var editor = TestCodeEditor.Create(["before.a", "before", "hello:", "there:", "this is strange:", "here", "it", "is"]);
        var viewModel = editor.ViewModel;

        CoreNavigationCommands.WordSelect.RunCoreEditorCommand(viewModel, new() { Position = P(3, 7) });
        AssertCursor(viewModel, S(3, 7, 3, 7));

        CoreNavigationCommands.WordSelectDrag.RunCoreEditorCommand(viewModel, new() { Position = P(4, 7) });
        AssertCursor(viewModel, S(3, 7, 4, 7));
    }

    [Fact]
    public void Issue_112039_Shift_Continuing_A_Double_Triple_Click_And_Drag_Selection_Does_Not_Remember_Its_Starting_Mode()
    {
        using var editor = TestCodeEditor.Create(["just some text", "and another line", "and another one"]);
        var viewModel = editor.ViewModel;

        CoreNavigationCommands.WordSelect.RunCoreEditorCommand(viewModel, new() { Position = P(2, 6) });
        CoreNavigationCommands.MoveToSelect.RunCoreEditorCommand(viewModel, new() { Position = P(1, 8) });
        AssertCursor(viewModel, S(2, 12, 1, 6));
    }

    [Fact]
    public void Issue_158236_Shift_Click_Selection_Does_Not_Work_On_Line_Number_Indicator()
    {
        using var editor = TestCodeEditor.Create(["just some text", "and another line", "and another one"]);
        var viewModel = editor.ViewModel;

        CoreNavigationCommands.MoveTo.RunCoreEditorCommand(viewModel, new() { Position = P(3, 5) });
        CoreNavigationCommands.LineSelectDrag.RunCoreEditorCommand(viewModel, new() { Position = P(2, 1) });
        AssertCursor(viewModel, S(3, 5, 2, 1));
    }

    [Fact]
    public void Issue_111513_Text_Gets_Automatically_Selected_When_Typing_At_The_Same_Location_In_Another_Editor()
    {
        using var model = TestTextModel.Create(string.Join('\n', ["just", "", "some text"]));

        TestCodeEditor.WithTestCodeEditor(model, new TestCodeEditorOptions { EditorId = 1 }, (editor1, viewModel1) =>
        {
            editor1.SetSelections([S(2, 1, 2, 1)]);
            TestCodeEditor.WithTestCodeEditor(model, new TestCodeEditorOptions { EditorId = 2 }, (editor2, viewModel2) =>
            {
                editor2.SetSelections([S(2, 1, 2, 1)]);
                viewModel2.Type("e", "keyboard");
                AssertCursor(viewModel2, P(2, 2));
                AssertCursor(viewModel1, P(2, 2));
            });
        });
    }

    // --------- multicursor.test.ts

    [Fact]
    public void Issue_1336_Insert_Cursor_Below_On_Last_Line_Adds_A_Cursor_To_The_End_Of_The_Current_Line()
    {
        using var editor = TestCodeEditor.Create(["abc"]);
        var viewModel = editor.ViewModel;
        InsertCursorBelow(viewModel);
        viewModel.GetSelections().Count.ShouldBe(1);
    }

    // --------- not upstream's: merging (CursorCollection.normalize)

    [Fact]
    public void Collapsed_Cursors_At_The_Same_Position_Merge()
    {
        using var editor = TestCodeEditor.Create(["abc", "def"]);
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(1, 2, 1, 2), S(2, 1, 2, 1), S(1, 2, 1, 2)]);
        AssertCursor(viewModel, S(1, 2, 1, 2), S(2, 1, 2, 1));
    }

    [Fact]
    public void A_Collapsed_Cursor_Touching_A_Selection_Merges_Into_It()
    {
        using var editor = TestCodeEditor.Create(["abcdef"]);
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(1, 2, 1, 4), S(1, 4, 1, 4)]);
        AssertCursor(viewModel, S(1, 2, 1, 4));
    }

    [Fact]
    public void Touching_Selections_Do_Not_Merge()
    {
        using var editor = TestCodeEditor.Create(["abcdef"]);
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(1, 1, 1, 3), S(1, 3, 1, 5)]);
        AssertCursor(viewModel, S(1, 1, 1, 3), S(1, 3, 1, 5));
    }

    [Fact]
    public void Overlapping_Selections_Merge_In_The_Direction_Of_The_Earlier_Cursor()
    {
        using var editor = TestCodeEditor.Create(["abcdefgh"]);
        var viewModel = editor.ViewModel;

        // The primary cursor (index 0) wins: its selection goes right to left.
        viewModel.SetSelections("test", [S(1, 5, 1, 2), S(1, 3, 1, 7), S(1, 8, 1, 8)]);
        AssertCursor(viewModel, S(1, 7, 1, 2), S(1, 8, 1, 8));
    }

    [Fact]
    public void The_Last_Added_Cursor_Gives_Its_Direction_When_It_Loses()
    {
        using var editor = TestCodeEditor.Create(["abcdefgh"]);
        var viewModel = editor.ViewModel;

        // The third cursor is the last added one; it loses to the second (an earlier cursor) but the merged selection
        // keeps its direction, and the winner becomes the last added cursor.
        viewModel.SetSelections("test", [S(1, 1, 1, 1), S(1, 5, 1, 2), S(1, 3, 1, 7)]);
        AssertCursor(viewModel, S(1, 1, 1, 1), S(1, 2, 1, 7));
        viewModel.GetLastAddedCursorIndex().ShouldBe(1);
    }

    [Fact]
    public void The_Winner_Gives_Its_Direction_When_The_Last_Added_Cursor_Is_Not_Involved()
    {
        using var editor = TestCodeEditor.Create(["abcdefghij"]);
        var viewModel = editor.ViewModel;

        viewModel.SetSelections("test", [S(1, 1, 1, 1), S(1, 5, 1, 2), S(1, 3, 1, 7), S(1, 9, 1, 9)]);
        AssertCursor(viewModel, S(1, 1, 1, 1), S(1, 7, 1, 2), S(1, 9, 1, 9));
        viewModel.GetLastAddedCursorIndex().ShouldBe(2);
    }

    [Fact]
    public void Merging_Can_Be_Turned_Off()
    {
        using var editor = TestCodeEditor.Create(["abcdef"], new TestCodeEditorOptions { EditorOptions = new EditorOptions { MultiCursorMergeOverlapping = false } });
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(1, 2, 1, 2), S(1, 2, 1, 2), S(1, 1, 1, 4)]);
        AssertCursor(viewModel, S(1, 2, 1, 2), S(1, 2, 1, 2), S(1, 1, 1, 4));
    }

    [Fact]
    public void Moving_Cursors_Onto_Each_Other_Merges_Them()
    {
        using var editor = TestCodeEditor.Create(["abc", "def", "ghi"]);
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(2, 2, 2, 2), S(3, 3, 3, 3), S(1, 1, 1, 1)]);

        CoreNavigationCommands.CursorTop.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, P(1, 1));
    }

    [Fact]
    public void Moving_Down_Merges_Cursors_On_The_Last_Line()
    {
        using var editor = TestCodeEditor.Create(["abc", "def"]);
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(1, 4, 1, 4), S(2, 4, 2, 4)]);

        CoreNavigationCommands.CursorDown.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, P(2, 4));
    }

    [Fact]
    public void The_Multi_Cursor_Limit_Cuts_The_States()
    {
        using var editor = TestCodeEditor.Create(["abc", "def", "ghi"], new TestCodeEditorOptions { EditorOptions = new EditorOptions { MultiCursorLimit = 2 } });
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(1, 1, 1, 1), S(2, 1, 2, 1), S(3, 1, 3, 1)]);
        AssertCursor(viewModel, S(1, 1, 1, 1), S(2, 1, 2, 1));
    }

    // --------- not upstream's: secondary cursors and the last added cursor

    [Fact]
    public void CreateCursor_Adds_A_Cursor_And_Removes_It_Again()
    {
        using var editor = TestCodeEditor.Create(["abc", "def", "ghi"]);
        var viewModel = editor.ViewModel;

        CoreNavigationCommands.CreateCursor.RunCoreEditorCommand(viewModel, new() { Position = P(2, 2) });
        AssertCursor(viewModel, S(1, 1, 1, 1), S(2, 2, 2, 2));
        viewModel.GetLastAddedCursorIndex().ShouldBe(1);

        CoreNavigationCommands.CreateCursor.RunCoreEditorCommand(viewModel, new() { Position = P(3, 3) });
        viewModel.GetLastAddedCursorIndex().ShouldBe(2);

        // a cursor at the same place toggles it off
        CoreNavigationCommands.CreateCursor.RunCoreEditorCommand(viewModel, new() { Position = P(2, 2) });
        AssertCursor(viewModel, S(1, 1, 1, 1), S(3, 3, 3, 3));
        viewModel.GetLastAddedCursorIndex().ShouldBe(1);
    }

    [Fact]
    public void CreateCursor_On_A_Whole_Line()
    {
        using var editor = TestCodeEditor.Create(["abc", "def", "ghi"]);
        var viewModel = editor.ViewModel;
        CoreNavigationCommands.CreateCursor.RunCoreEditorCommand(viewModel, new() { Position = P(2, 2), WholeLine = true });
        AssertCursor(viewModel, S(1, 1, 1, 1), S(2, 1, 3, 1));
    }

    [Fact]
    public void LastCursorMoveToSelect_Moves_The_Last_Added_Cursor()
    {
        using var editor = TestCodeEditor.Create(["abc", "def", "ghi"]);
        var viewModel = editor.ViewModel;
        CoreNavigationCommands.CreateCursor.RunCoreEditorCommand(viewModel, new() { Position = P(2, 2) });
        CoreNavigationCommands.LastCursorMoveToSelect.RunCoreEditorCommand(viewModel, new() { Position = P(3, 2) });
        AssertCursor(viewModel, S(1, 1, 1, 1), S(2, 2, 3, 2));
    }

    [Fact]
    public void LastCursorLineSelect_And_LastCursorWordSelect_Use_The_Last_Added_Cursor()
    {
        using var editor = TestCodeEditor.Create(["abc def", "ghi jkl", "mno"]);
        var viewModel = editor.ViewModel;
        CoreNavigationCommands.CreateCursor.RunCoreEditorCommand(viewModel, new() { Position = P(2, 2) });

        CoreNavigationCommands.LastCursorLineSelect.RunCoreEditorCommand(viewModel, new() { Position = P(2, 2) });
        AssertCursor(viewModel, S(1, 1, 1, 1), S(2, 1, 3, 1));

        CoreNavigationCommands.LastCursorLineSelectDrag.RunCoreEditorCommand(viewModel, new() { Position = P(3, 2) });
        AssertCursor(viewModel, S(1, 1, 1, 1), S(2, 1, 3, 4));

        CoreNavigationCommands.CreateCursor.RunCoreEditorCommand(viewModel, new() { Position = P(1, 6) });
        CoreNavigationCommands.LastCursorWordSelect.RunCoreEditorCommand(viewModel, new() { Position = P(1, 6) });
        AssertCursor(viewModel, S(1, 1, 1, 1), S(2, 1, 3, 4), S(1, 5, 1, 8));
    }

    [Fact]
    public void RemoveSecondaryCursors_Keeps_The_Primary_Cursor()
    {
        using var editor = TestCodeEditor.Create(["abc", "def", "ghi"]);
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(2, 2, 2, 3), S(1, 1, 1, 1), S(3, 1, 3, 1)]);
        CoreNavigationCommands.RemoveSecondaryCursors.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, S(2, 2, 2, 3));
        viewModel.GetLastAddedCursorIndex().ShouldBe(0);
    }

    [Fact]
    public void Top_Most_And_Bottom_Most_View_Positions()
    {
        using var editor = TestCodeEditor.Create(["abc", "def", "ghi"]);
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(2, 2, 2, 2), S(1, 1, 1, 3), S(3, 3, 3, 3)]);
        var reveals = new RevealRecorder();
        viewModel.AddViewEventHandler(reveals);
        viewModel.RevealTopMostCursor(null);
        viewModel.RevealBottomMostCursor(null);
        viewModel.RemoveViewEventHandler(reveals);
        reveals.Ranges.ShouldBe([new TextRange(P(1, 3)), new TextRange(P(3, 3))]);
    }

    // --------- not upstream's: OneCursor validation and markers

    [Fact]
    public void Selections_Are_Validated_Against_The_Model()
    {
        using var editor = TestCodeEditor.Create(["abc", "de"]);
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(0, 0, 9, 9)]);
        AssertCursor(viewModel, S(1, 1, 2, 3));

        var state = viewModel.GetPrimaryCursorState();
        state.ViewState.Selection.ShouldBe(state.ModelState.Selection);
    }

    [Fact]
    public void A_View_State_Gives_The_Model_State()
    {
        using var editor = TestCodeEditor.Create(["abc", "de"]);
        var viewModel = editor.ViewModel;
        var viewState = new SingleCursorState(new TextRange(1, 2, 1, 2), SelectionStartKind.Simple, 0, P(2, 9), 3);
        viewModel.SetCursorStates("test", CursorChangeReason.Explicit, [CursorState.FromViewState(viewState)]);

        var state = viewModel.GetPrimaryCursorState();
        state.ModelState.Selection.ShouldBe(S(1, 2, 2, 3));
        state.ViewState.Selection.ShouldBe(S(1, 2, 2, 3));
        // the leftover visible columns go to the model state unchanged, and from it back to the view state
        state.ModelState.LeftoverVisibleColumns.ShouldBe(3);
        state.ViewState.LeftoverVisibleColumns.ShouldBe(3);
    }

    [Fact]
    public void A_Collapsed_Cursor_Does_Not_Select_Text_Inserted_At_It()
    {
        using var editor = TestCodeEditor.Create(["abc"]);
        var viewModel = editor.ViewModel;
        MoveTo(viewModel, 1, 2);
        // a replace (not an insert) leaves the markers to their stickiness: the tracked range grows over the text
        viewModel.Model.ApplyEdits([EditOperation.Replace(new TextRange(1, 2, 1, 2), "xyz")]);
        AssertCursor(viewModel, P(1, 5));
    }

    [Fact]
    public void A_Selection_Grows_With_Text_Typed_At_Its_Edges()
    {
        using var editor = TestCodeEditor.Create(["abcdef"]);
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(1, 5, 1, 2)]);
        viewModel.Model.ApplyEdits([EditOperation.Replace(new TextRange(1, 2, 1, 2), "x"), EditOperation.Replace(new TextRange(1, 5, 1, 5), "y")]);
        AssertCursor(viewModel, S(1, 7, 1, 2));
    }

    [Fact]
    public void Setting_The_Value_Resets_The_Cursors()
    {
        using var editor = TestCodeEditor.Create(["abc", "def"]);
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(2, 2, 2, 3), S(1, 1, 1, 2)]);
        viewModel.Model.SetValue("xyz");
        AssertCursor(viewModel, P(1, 1));
        viewModel.LastChangeReason.ShouldBe(CursorChangeReason.ContentFlush);
    }

    // --------- not upstream's: the other CursorMoveCommands

    [Fact]
    public void ExpandLineSelection_Selects_Whole_Lines()
    {
        using var editor = TestCodeEditor.Create(["abc", "def", "ghi"]);
        var viewModel = editor.ViewModel;
        MoveTo(viewModel, 1, 2);
        ExpandLineSelection(viewModel);
        AssertCursor(viewModel, S(1, 1, 2, 1));

        ExpandLineSelection(viewModel);
        AssertCursor(viewModel, S(1, 1, 3, 1));

        ExpandLineSelection(viewModel);
        AssertCursor(viewModel, S(1, 1, 3, 4));
    }

    [Fact]
    public void CancelSelection_Collapses_The_Primary_Selection_To_Its_Position()
    {
        using var editor = TestCodeEditor.Create(["abc", "def"]);
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(1, 1, 2, 2), S(2, 3, 2, 3)]);
        CoreNavigationCommands.CancelSelection.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, P(2, 2));

        CoreNavigationCommands.CancelSelection.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, P(2, 2));
    }

    [Fact]
    public void Line_Selection_Extends_Up_And_Down()
    {
        using var editor = TestCodeEditor.Create(["a", "b", "c", "d"]);
        var viewModel = editor.ViewModel;

        CoreNavigationCommands.LineSelect.RunCoreEditorCommand(viewModel, new() { Position = P(2, 1) });
        AssertCursor(viewModel, S(2, 1, 3, 1));

        CoreNavigationCommands.LineSelectDrag.RunCoreEditorCommand(viewModel, new() { Position = P(3, 1) });
        AssertCursor(viewModel, S(2, 1, 4, 1));

        CoreNavigationCommands.LineSelectDrag.RunCoreEditorCommand(viewModel, new() { Position = P(4, 1) });
        AssertCursor(viewModel, S(2, 1, 4, 2));

        CoreNavigationCommands.LineSelectDrag.RunCoreEditorCommand(viewModel, new() { Position = P(1, 1) });
        AssertCursor(viewModel, S(3, 1, 1, 1));

        CoreNavigationCommands.LineSelectDrag.RunCoreEditorCommand(viewModel, new() { Position = P(2, 1) });
        AssertCursor(viewModel, S(2, 1, 3, 1));
    }

    [Fact]
    public void Line_Selection_On_The_Last_Line_Ends_At_Its_End()
    {
        using var editor = TestCodeEditor.Create(["a", "bcd"]);
        var viewModel = editor.ViewModel;
        CoreNavigationCommands.LineSelect.RunCoreEditorCommand(viewModel, new() { Position = P(2, 2) });
        AssertCursor(viewModel, S(2, 1, 2, 4));
    }

    [Fact]
    public void InsertCursorAbove_And_Below()
    {
        using var editor = TestCodeEditor.Create(["abcdef", "ab", "abcdef"]);
        var viewModel = editor.ViewModel;
        MoveTo(viewModel, 2, 3);

        InsertCursorBelow(viewModel);
        AssertCursor(viewModel, S(2, 3, 2, 3), S(3, 3, 3, 3));

        InsertCursorAbove(viewModel);
        AssertCursor(viewModel, S(2, 3, 2, 3), S(1, 3, 1, 3), S(3, 3, 3, 3));

        // the cursors above and below already exist: adding more merges them
        InsertCursorAbove(viewModel, logicalLine: true);
        AssertCursor(viewModel, S(2, 3, 2, 3), S(1, 3, 1, 3), S(3, 3, 3, 3));
    }

    [Fact]
    public void InsertCursorBelow_Keeps_The_Selection_Shape()
    {
        using var editor = TestCodeEditor.Create(["abcdef", "abcdef"]);
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(1, 2, 1, 4)]);
        InsertCursorBelow(viewModel);
        AssertCursor(viewModel, S(1, 2, 1, 4), S(2, 2, 2, 4));
    }

    [Fact]
    public void ViewPortIfOutside_Moves_Cursors_Into_The_Viewport()
    {
        using var editor = TestCodeEditor.Create(["l1", "l2", "l3", "l4", "l5", "l6"]);
        var viewModel = editor.ViewModel;
        viewModel.CompletelyVisibleViewRange = new TextRange(2, 1, 5, 1);
        viewModel.SetSelections("test", [S(1, 2, 1, 2), S(3, 1, 3, 1), S(6, 3, 6, 3)]);

        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.ViewPortIfOutside });

        // line 5 is the last line of the range and counts as outside it (its end column is 1)
        AssertCursor(viewModel, S(2, 2, 2, 2), S(3, 1, 3, 1), S(4, 3, 4, 3));
    }

    private sealed class RevealRecorder : ViewEventHandler
    {
        public List<TextRange?> Ranges { get; } = new();

        public override bool OnRevealRangeRequest(ViewRevealRangeRequestEvent e)
        {
            Ranges.Add(e.Range);
            return false;
        }
    }

    [Fact]
    public void SimpleMove_And_ViewportMove_Return_Null_For_Other_Directions()
    {
        using var editor = TestCodeEditor.Create(["abc"]);
        var viewModel = editor.ViewModel;
        var cursors = viewModel.GetCursorStates();
        CursorMoveCommands.SimpleMove(viewModel, cursors, CursorMove.Direction.ViewPortTop, false, 1, CursorMove.Unit.None).ShouldBeNull();
        CursorMoveCommands.ViewportMove(viewModel, cursors, CursorMove.Direction.Left, false, 1).ShouldBeNull();
    }
}
