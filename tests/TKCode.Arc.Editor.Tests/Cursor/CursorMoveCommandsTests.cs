// Tests for CursorMoveCommands and CursorMove, ported from vscode
// (src/vs/editor/test/browser/controller/cursorMoveCommand.test.ts, commit b57d5c9e), MIT license: the suites
// 'Cursor move command test', 'Cursor move by blankline test' and 'Cursor move command - foldedLine unit', all cases.
// Upstream runs CoreNavigationCommands.CursorMove on a test code editor's view model; here it runs the engine's
// CoreNavigationCommands.CursorMove on a TestCodeEditor's view model. The view port tests replace
// getCompletelyVisibleViewRange as upstream does (TestViewModel.CompletelyVisibleViewRange); the folded line tests hide
// the lines with setHiddenAreas, as upstream.
// The CursorMove.Parse and IsCursorMoveArgs tests at the end are not upstream's.

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Cursor;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class CursorMoveCommandsTests
{
    private static readonly string Text = string.Join('\n',
    [
        "    \tMy First Line\t ",
        "\tMy Second Line",
        "    Third Line🐶",
        "",
        "1",
    ]);

    private static void ExecuteTest(Action<TestViewModel> callback)
    {
        using var editor = TestCodeEditor.Create(Text);
        var viewModel = editor.ViewModel;
        callback(viewModel);
    }

    [Fact]
    public void Move_Left_Should_Move_To_Left_Character() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 8);
        MoveLeft(viewModel);
        CursorEqual(viewModel, 1, 7);
    });

    [Fact]
    public void Move_Left_Should_Move_To_Left_By_N_Characters() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 8);
        MoveLeft(viewModel, 3);
        CursorEqual(viewModel, 1, 5);
    });

    [Fact]
    public void Move_Left_Should_Move_To_Left_By_Half_Line() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 8);
        MoveLeft(viewModel, 1, CursorMove.RawUnit.HalfLine);
        CursorEqual(viewModel, 1, 1);
    });

    [Fact]
    public void Move_Left_Moves_To_Previous_Line() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 2, 3);
        MoveLeft(viewModel, 10);
        CursorEqual(viewModel, 1, 21);
    });

    [Fact]
    public void Move_Right_Should_Move_To_Right_Character() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 5);
        MoveRight(viewModel);
        CursorEqual(viewModel, 1, 6);
    });

    [Fact]
    public void Move_Right_Should_Move_To_Right_By_N_Characters() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 2);
        MoveRight(viewModel, 6);
        CursorEqual(viewModel, 1, 8);
    });

    [Fact]
    public void Move_Right_Should_Move_To_Right_By_Half_Line() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 4);
        MoveRight(viewModel, 1, CursorMove.RawUnit.HalfLine);
        CursorEqual(viewModel, 1, 14);
    });

    [Fact]
    public void Move_Right_Moves_To_Next_Line() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 8);
        MoveRight(viewModel, 100);
        CursorEqual(viewModel, 2, 1);
    });

    [Fact]
    public void Move_To_First_Character_Of_Line_From_Middle() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 8);
        MoveToLineStart(viewModel);
        CursorEqual(viewModel, 1, 1);
    });

    [Fact]
    public void Move_To_First_Character_Of_Line_From_First_Non_White_Space_Character() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 6);
        MoveToLineStart(viewModel);
        CursorEqual(viewModel, 1, 1);
    });

    [Fact]
    public void Move_To_First_Character_Of_Line_From_First_Character() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 1);
        MoveToLineStart(viewModel);
        CursorEqual(viewModel, 1, 1);
    });

    [Fact]
    public void Move_To_First_Non_White_Space_Character_Of_Line_From_Middle() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 8);
        MoveToLineFirstNonWhitespaceCharacter(viewModel);
        CursorEqual(viewModel, 1, 6);
    });

    [Fact]
    public void Move_To_First_Non_White_Space_Character_Of_Line_From_First_Non_White_Space_Character() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 6);
        MoveToLineFirstNonWhitespaceCharacter(viewModel);
        CursorEqual(viewModel, 1, 6);
    });

    [Fact]
    public void Move_To_First_Non_White_Space_Character_Of_Line_From_First_Character() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 1);
        MoveToLineFirstNonWhitespaceCharacter(viewModel);
        CursorEqual(viewModel, 1, 6);
    });

    [Fact]
    public void Move_To_End_Of_Line_From_Middle() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 8);
        MoveToLineEnd(viewModel);
        CursorEqual(viewModel, 1, 21);
    });

    [Fact]
    public void Move_To_End_Of_Line_From_Last_Non_White_Space_Character() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 19);
        MoveToLineEnd(viewModel);
        CursorEqual(viewModel, 1, 21);
    });

    [Fact]
    public void Move_To_End_Of_Line_From_Line_End() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 21);
        MoveToLineEnd(viewModel);
        CursorEqual(viewModel, 1, 21);
    });

    [Fact]
    public void Move_To_Last_Non_White_Space_Character_From_Middle() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 8);
        MoveToLineLastNonWhitespaceCharacter(viewModel);
        CursorEqual(viewModel, 1, 19);
    });

    [Fact]
    public void Move_To_Last_Non_White_Space_Character_From_Last_Non_White_Space_Character() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 19);
        MoveToLineLastNonWhitespaceCharacter(viewModel);
        CursorEqual(viewModel, 1, 19);
    });

    [Fact]
    public void Move_To_Last_Non_White_Space_Character_From_Line_End() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 21);
        MoveToLineLastNonWhitespaceCharacter(viewModel);
        CursorEqual(viewModel, 1, 19);
    });

    [Fact]
    public void Move_To_Center_Of_Line_Not_From_Center() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 8);
        MoveToLineCenter(viewModel);
        CursorEqual(viewModel, 1, 11);
    });

    [Fact]
    public void Move_To_Center_Of_Line_From_Center() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 11);
        MoveToLineCenter(viewModel);
        CursorEqual(viewModel, 1, 11);
    });

    [Fact]
    public void Move_To_Center_Of_Line_From_Start() => ExecuteTest(viewModel =>
    {
        MoveToLineStart(viewModel);
        MoveToLineCenter(viewModel);
        CursorEqual(viewModel, 1, 11);
    });

    [Fact]
    public void Move_To_Center_Of_Line_From_End() => ExecuteTest(viewModel =>
    {
        MoveToLineEnd(viewModel);
        MoveToLineCenter(viewModel);
        CursorEqual(viewModel, 1, 11);
    });

    [Fact]
    public void Move_Up_By_Cursor_Move_Command() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 3, 5);
        CursorEqual(viewModel, 3, 5);

        MoveUp(viewModel, 2);
        CursorEqual(viewModel, 1, 5);

        MoveUp(viewModel, 1);
        CursorEqual(viewModel, 1, 1);
    });

    [Fact]
    public void Move_Up_By_Model_Line_Cursor_Move_Command() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 3, 5);
        CursorEqual(viewModel, 3, 5);

        MoveUpByModelLine(viewModel, 2);
        CursorEqual(viewModel, 1, 5);

        MoveUpByModelLine(viewModel, 1);
        CursorEqual(viewModel, 1, 1);
    });

    [Fact]
    public void Move_Down_By_Model_Line_Cursor_Move_Command() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 3, 5);
        CursorEqual(viewModel, 3, 5);

        MoveDownByModelLine(viewModel, 2);
        CursorEqual(viewModel, 5, 2);

        MoveDownByModelLine(viewModel, 1);
        CursorEqual(viewModel, 5, 2);
    });

    [Fact]
    public void Move_Up_With_Selection_By_Cursor_Move_Command() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 3, 5);
        CursorEqual(viewModel, 3, 5);

        MoveUp(viewModel, 1, true);
        CursorEqual(viewModel, 2, 2, 3, 5);

        MoveUp(viewModel, 1, true);
        CursorEqual(viewModel, 1, 5, 3, 5);
    });

    [Fact]
    public void Move_Up_And_Down_With_Tabs_By_Cursor_Move_Command() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 1, 5);
        CursorEqual(viewModel, 1, 5);

        MoveDown(viewModel, 4);
        CursorEqual(viewModel, 5, 2);

        MoveUp(viewModel, 1);
        CursorEqual(viewModel, 4, 1);

        MoveUp(viewModel, 1);
        CursorEqual(viewModel, 3, 5);

        MoveUp(viewModel, 1);
        CursorEqual(viewModel, 2, 2);

        MoveUp(viewModel, 1);
        CursorEqual(viewModel, 1, 5);
    });

    [Fact]
    public void Move_Up_And_Down_With_End_Of_Lines_Starting_From_A_Long_One_By_Cursor_Move_Command() => ExecuteTest(viewModel =>
    {
        MoveToEndOfLine(viewModel);
        CursorEqual(viewModel, 1, 21);

        MoveToEndOfLine(viewModel);
        CursorEqual(viewModel, 1, 21);

        MoveDown(viewModel, 2);
        CursorEqual(viewModel, 3, 17);

        MoveDown(viewModel, 1);
        CursorEqual(viewModel, 4, 1);

        MoveDown(viewModel, 1);
        CursorEqual(viewModel, 5, 2);

        MoveUp(viewModel, 4);
        CursorEqual(viewModel, 1, 21);
    });

    [Fact]
    public void Move_To_View_Top_Line_Moves_To_First_Visible_Line_If_It_Is_First_Line() => ExecuteTest(viewModel =>
    {
        viewModel.CompletelyVisibleViewRange = new TextRange(1, 1, 10, 1);

        MoveTo(viewModel, 2, 2);
        MoveToTop(viewModel);

        CursorEqual(viewModel, 1, 6);
    });

    [Fact]
    public void Move_To_View_Top_Line_Moves_To_Top_Visible_Line_When_First_Line_Is_Not_Visible() => ExecuteTest(viewModel =>
    {
        viewModel.CompletelyVisibleViewRange = new TextRange(2, 1, 10, 1);

        MoveTo(viewModel, 4, 1);
        MoveToTop(viewModel);

        CursorEqual(viewModel, 2, 2);
    });

    [Fact]
    public void Move_To_View_Top_Line_Moves_To_Nth_Line_From_Top() => ExecuteTest(viewModel =>
    {
        viewModel.CompletelyVisibleViewRange = new TextRange(1, 1, 10, 1);

        MoveTo(viewModel, 4, 1);
        MoveToTop(viewModel, 3);

        CursorEqual(viewModel, 3, 5);
    });

    [Fact]
    public void Move_To_View_Top_Line_Moves_To_Last_Line_If_N_Is_Greater_Than_Last_Visible_Line_Number() => ExecuteTest(viewModel =>
    {
        viewModel.CompletelyVisibleViewRange = new TextRange(1, 1, 3, 1);

        MoveTo(viewModel, 2, 2);
        MoveToTop(viewModel, 4);

        CursorEqual(viewModel, 3, 5);
    });

    [Fact]
    public void Move_To_View_Center_Line_Moves_To_The_Center_Line() => ExecuteTest(viewModel =>
    {
        viewModel.CompletelyVisibleViewRange = new TextRange(3, 1, 3, 1);

        MoveTo(viewModel, 2, 2);
        MoveToCenter(viewModel);

        CursorEqual(viewModel, 3, 5);
    });

    [Fact]
    public void Move_To_View_Bottom_Line_Moves_To_Last_Visible_Line_If_It_Is_Last_Line() => ExecuteTest(viewModel =>
    {
        viewModel.CompletelyVisibleViewRange = new TextRange(1, 1, 5, 1);

        MoveTo(viewModel, 2, 2);
        MoveToBottom(viewModel);

        CursorEqual(viewModel, 5, 1);
    });

    [Fact]
    public void Move_To_View_Bottom_Line_Moves_To_Last_Visible_Line_When_Last_Line_Is_Not_Visible() => ExecuteTest(viewModel =>
    {
        viewModel.CompletelyVisibleViewRange = new TextRange(2, 1, 3, 1);

        MoveTo(viewModel, 2, 2);
        MoveToBottom(viewModel);

        CursorEqual(viewModel, 3, 5);
    });

    [Fact]
    public void Move_To_View_Bottom_Line_Moves_To_Nth_Line_From_Bottom() => ExecuteTest(viewModel =>
    {
        viewModel.CompletelyVisibleViewRange = new TextRange(1, 1, 5, 1);

        MoveTo(viewModel, 4, 1);
        MoveToBottom(viewModel, 3);

        CursorEqual(viewModel, 3, 5);
    });

    [Fact]
    public void Move_To_View_Bottom_Line_Moves_To_First_Line_If_N_Is_Lesser_Than_First_Visible_Line_Number() => ExecuteTest(viewModel =>
    {
        viewModel.CompletelyVisibleViewRange = new TextRange(2, 1, 5, 1);

        MoveTo(viewModel, 4, 1);
        MoveToBottom(viewModel, 5);

        CursorEqual(viewModel, 2, 2);
    });

    // ------ suite 'Cursor move by blankline test'

    private static readonly string BlankLineText = string.Join('\n',
    [
        "    \tMy First Line\t ",
        "\tMy Second Line",
        "    Third Line🐶",
        "",
        "1",
        "2",
        "3",
        "",
        "         ",
        "a",
        "b",
    ]);

    private static void ExecuteBlankLineTest(Action<TestViewModel> callback)
    {
        using var editor = TestCodeEditor.Create(BlankLineText);
        var viewModel = editor.ViewModel;
        callback(viewModel);
    }

    [Fact]
    public void Move_Down_Should_Move_To_Start_Of_Next_Blank_Line() => ExecuteBlankLineTest(viewModel =>
    {
        MoveDownByBlankLine(viewModel, false);
        CursorEqual(viewModel, 4, 1);
    });

    [Fact]
    public void Move_Up_Should_Move_To_Start_Of_Previous_Blank_Line() => ExecuteBlankLineTest(viewModel =>
    {
        MoveTo(viewModel, 7, 1);
        MoveUpByBlankLine(viewModel, false);
        CursorEqual(viewModel, 4, 1);
    });

    [Fact]
    public void Move_Down_Should_Skip_Over_Whitespace_If_Already_On_Blank_Line() => ExecuteBlankLineTest(viewModel =>
    {
        MoveTo(viewModel, 8, 1);
        MoveDownByBlankLine(viewModel, false);
        CursorEqual(viewModel, 11, 1);
    });

    [Fact]
    public void Move_Up_Should_Skip_Over_Whitespace_If_Already_On_Blank_Line() => ExecuteBlankLineTest(viewModel =>
    {
        MoveTo(viewModel, 9, 1);
        MoveUpByBlankLine(viewModel, false);
        CursorEqual(viewModel, 4, 1);
    });

    [Fact]
    public void Move_Up_Should_Go_To_First_Column_Of_First_Line_If_Not_Empty() => ExecuteBlankLineTest(viewModel =>
    {
        MoveTo(viewModel, 2, 1);
        MoveUpByBlankLine(viewModel, false);
        CursorEqual(viewModel, 1, 1);
    });

    [Fact]
    public void Move_Down_Should_Go_To_First_Column_Of_Last_Line_If_Not_Empty() => ExecuteBlankLineTest(viewModel =>
    {
        MoveTo(viewModel, 10, 1);
        MoveDownByBlankLine(viewModel, false);
        CursorEqual(viewModel, 11, 1);
    });

    [Fact]
    public void Select_Down_Should_Select_To_Start_Of_Next_Blank_Line() => ExecuteBlankLineTest(viewModel =>
    {
        MoveDownByBlankLine(viewModel, true);
        SelectionEqual(viewModel.GetSelection(), 4, 1, 1, 1);
    });

    [Fact]
    public void Select_Up_Should_Select_To_Start_Of_Previous_Blank_Line() => ExecuteBlankLineTest(viewModel =>
    {
        MoveTo(viewModel, 7, 1);
        MoveUpByBlankLine(viewModel, true);
        SelectionEqual(viewModel.GetSelection(), 4, 1, 7, 1);
    });

    // ------ suite 'Cursor move command - foldedLine unit'
    // Tests for 'foldedLine' unit: moves by model lines but treats each fold as a single step.
    // This is the semantics required by vim's j/k: move through visible lines, skip hidden ones.

    private static void ExecuteFoldTest(Action<TestViewModel> callback)
    {
        using var editor = TestCodeEditor.Create(["line1", "line2", "line3", "line4", "line5"]);
        var viewModel = editor.ViewModel;
        callback(viewModel);
    }

    [Fact]
    public void Move_Down_By_FoldedLine_Skips_A_Fold_Below_The_Cursor() => ExecuteFoldTest(viewModel =>
    {
        // Line 4 is hidden (folded under line 3 as header)
        viewModel.SetHiddenAreas([new TextRange(4, 1, 4, 1)]);
        MoveTo(viewModel, 2, 1);
        // j from line 2 → line 3 (visible fold header)
        MoveDownByFoldedLine(viewModel);
        CursorEqual(viewModel, 3, 1);
        // j from line 3 (fold header) → line 4 is hidden, lands on line 5
        MoveDownByFoldedLine(viewModel);
        CursorEqual(viewModel, 5, 1);
    });

    [Fact]
    public void Move_Up_By_FoldedLine_Skips_A_Fold_Above_The_Cursor() => ExecuteFoldTest(viewModel =>
    {
        // Line 3 is hidden (folded under line 2 as header)
        viewModel.SetHiddenAreas([new TextRange(3, 1, 3, 1)]);
        MoveTo(viewModel, 4, 1);
        // k from line 4: line 3 is hidden, lands on line 2 (fold header)
        MoveUpByFoldedLine(viewModel);
        CursorEqual(viewModel, 2, 1);
        // k from line 2 → line 1
        MoveUpByFoldedLine(viewModel);
        CursorEqual(viewModel, 1, 1);
    });

    [Fact]
    public void Move_Down_By_FoldedLine_With_Count_Treats_Each_Fold_As_One_Step() => ExecuteFoldTest(viewModel =>
    {
        // Line 3 is hidden
        viewModel.SetHiddenAreas([new TextRange(3, 1, 3, 1)]);
        MoveTo(viewModel, 1, 1);
        // 3j from line 1: step1→2, step2→3(hidden)→4, step3→5
        MoveDownByFoldedLine(viewModel, 3);
        CursorEqual(viewModel, 5, 1);
    });

    [Fact]
    public void Move_Down_By_FoldedLine_Skips_A_Multi_Line_Fold_As_One_Step() => ExecuteFoldTest(viewModel =>
    {
        // Lines 2-4 are hidden (folded under line 1 as header)
        viewModel.SetHiddenAreas([new TextRange(2, 1, 4, 1)]);
        MoveTo(viewModel, 1, 1);
        // j from line 1: lines 2-4 are all hidden, lands directly on line 5
        MoveDownByFoldedLine(viewModel);
        CursorEqual(viewModel, 5, 1);
    });

    [Fact]
    public void Move_Down_By_FoldedLine_At_Last_Line_Stays_At_Last_Line() => ExecuteFoldTest(viewModel =>
    {
        MoveTo(viewModel, 5, 1);
        MoveDownByFoldedLine(viewModel);
        CursorEqual(viewModel, 5, 1);
    });

    [Fact]
    public void Move_Up_By_FoldedLine_At_First_Line_Stays_At_First_Line() => ExecuteFoldTest(viewModel =>
    {
        MoveTo(viewModel, 1, 1);
        MoveUpByFoldedLine(viewModel);
        CursorEqual(viewModel, 1, 1);
    });

    [Fact]
    public void Move_Down_By_FoldedLine_With_Count_Clamps_To_Last_Visible_Line_After_Fold() => ExecuteFoldTest(viewModel =>
    {
        // Lines 2-4 are hidden. Visible lines are 1 and 5.
        viewModel.SetHiddenAreas([new TextRange(2, 1, 4, 1)]);
        MoveTo(viewModel, 1, 1);
        // 2j should land on line 5 and clamp there.
        MoveDownByFoldedLine(viewModel, 2);
        CursorEqual(viewModel, 5, 1);
    });

    [Fact]
    public void Move_Up_By_FoldedLine_With_Count_Clamps_To_First_Visible_Line_Before_Fold() => ExecuteFoldTest(viewModel =>
    {
        // Lines 2-4 are hidden. Visible lines are 1 and 5.
        viewModel.SetHiddenAreas([new TextRange(2, 1, 4, 1)]);
        MoveTo(viewModel, 5, 1);
        // 2k should land on line 1 and clamp there.
        MoveUpByFoldedLine(viewModel, 2);
        CursorEqual(viewModel, 1, 1);
    });

    // ------ argument parsing (not upstream's)

    [Fact]
    public void Parse_Fills_In_The_Defaults()
    {
        var parsed = CursorMove.Parse(new CursorMove.RawArguments { To = CursorMove.RawDirection.Down });
        parsed.ShouldBe(new CursorMove.ParsedArguments(CursorMove.Direction.Down, CursorMove.Unit.None, false, 1, false));

        // args.value || 1
        CursorMove.Parse(new CursorMove.RawArguments { To = "left", Value = 0 })!.Value.ShouldBe(1);
        CursorMove.Parse(new CursorMove.RawArguments { To = "left", Value = 7, By = "halfLine", Select = true, NoHistory = true })
            .ShouldBe(new CursorMove.ParsedArguments(CursorMove.Direction.Left, CursorMove.Unit.HalfLine, true, 7, true));
    }

    [Theory]
    [InlineData("left", nameof(CursorMove.Direction.Left))]
    [InlineData("right", nameof(CursorMove.Direction.Right))]
    [InlineData("up", nameof(CursorMove.Direction.Up))]
    [InlineData("down", nameof(CursorMove.Direction.Down))]
    [InlineData("prevBlankLine", nameof(CursorMove.Direction.PrevBlankLine))]
    [InlineData("nextBlankLine", nameof(CursorMove.Direction.NextBlankLine))]
    [InlineData("wrappedLineStart", nameof(CursorMove.Direction.WrappedLineStart))]
    [InlineData("wrappedLineFirstNonWhitespaceCharacter", nameof(CursorMove.Direction.WrappedLineFirstNonWhitespaceCharacter))]
    [InlineData("wrappedLineColumnCenter", nameof(CursorMove.Direction.WrappedLineColumnCenter))]
    [InlineData("wrappedLineEnd", nameof(CursorMove.Direction.WrappedLineEnd))]
    [InlineData("wrappedLineLastNonWhitespaceCharacter", nameof(CursorMove.Direction.WrappedLineLastNonWhitespaceCharacter))]
    [InlineData("viewPortTop", nameof(CursorMove.Direction.ViewPortTop))]
    [InlineData("viewPortCenter", nameof(CursorMove.Direction.ViewPortCenter))]
    [InlineData("viewPortBottom", nameof(CursorMove.Direction.ViewPortBottom))]
    [InlineData("viewPortIfOutside", nameof(CursorMove.Direction.ViewPortIfOutside))]
    public void Parse_Maps_Every_Direction(string to, string expected) =>
        CursorMove.Parse(new CursorMove.RawArguments { To = to })!.Direction.ToString().ShouldBe(expected);

    [Theory]
    [InlineData("line", nameof(CursorMove.Unit.Line))]
    [InlineData("wrappedLine", nameof(CursorMove.Unit.WrappedLine))]
    [InlineData("character", nameof(CursorMove.Unit.Character))]
    [InlineData("halfLine", nameof(CursorMove.Unit.HalfLine))]
    [InlineData("foldedLine", nameof(CursorMove.Unit.FoldedLine))]
    [InlineData("parsec", nameof(CursorMove.Unit.None))]
    public void Parse_Maps_Every_Unit(string by, string expected) =>
        CursorMove.Parse(new CursorMove.RawArguments { To = "up", By = by })!.Unit.ToString().ShouldBe(expected);

    [Fact]
    public void Parse_Rejects_Illegal_Arguments()
    {
        CursorMove.Parse(new CursorMove.RawArguments()).ShouldBeNull();
        CursorMove.Parse(new CursorMove.RawArguments { To = "" }).ShouldBeNull();
        CursorMove.Parse(new CursorMove.RawArguments { To = "sideways" }).ShouldBeNull();
    }

    [Fact]
    public void Illegal_Arguments_Do_Not_Move() => ExecuteTest(viewModel =>
    {
        MoveTo(viewModel, 2, 3);
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = "sideways" });
        CursorEqual(viewModel, 2, 3);
    });

    [Fact]
    public void IsCursorMoveArgs_Checks_The_Argument_Types()
    {
        CursorMove.IsCursorMoveArgs(null).ShouldBeFalse();
        CursorMove.IsCursorMoveArgs("left").ShouldBeFalse();
        CursorMove.IsCursorMoveArgs(new CursorMove.RawArguments()).ShouldBeFalse();
        CursorMove.IsCursorMoveArgs(new CursorMove.RawArguments { To = "left" }).ShouldBeTrue();

        CursorMove.IsCursorMoveArgs(Args(("to", "left"))).ShouldBeTrue();
        CursorMove.IsCursorMoveArgs(Args(("to", "left"), ("select", true), ("by", "line"), ("value", 2), ("noHistory", false))).ShouldBeTrue();
        CursorMove.IsCursorMoveArgs(Args(("to", "left"), ("value", 2.5))).ShouldBeTrue();
        CursorMove.IsCursorMoveArgs(Args(("to", "left"), ("select", null))).ShouldBeTrue();

        CursorMove.IsCursorMoveArgs(Args()).ShouldBeFalse();
        CursorMove.IsCursorMoveArgs(Args(("to", 1))).ShouldBeFalse();
        CursorMove.IsCursorMoveArgs(Args(("to", "left"), ("select", "yes"))).ShouldBeFalse();
        CursorMove.IsCursorMoveArgs(Args(("to", "left"), ("by", 3))).ShouldBeFalse();
        CursorMove.IsCursorMoveArgs(Args(("to", "left"), ("value", "2"))).ShouldBeFalse();
        CursorMove.IsCursorMoveArgs(Args(("to", "left"), ("noHistory", 1))).ShouldBeFalse();
    }

    private static Dictionary<string, object?> Args(params (string Key, object? Value)[] entries) =>
        entries.ToDictionary(e => e.Key, e => e.Value);

    // ------ helpers: upstream's move functions

    private static void MoveToLineStart(TestViewModel viewModel) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.WrappedLineStart });

    private static void MoveToLineFirstNonWhitespaceCharacter(TestViewModel viewModel) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.WrappedLineFirstNonWhitespaceCharacter });

    private static void MoveToLineCenter(TestViewModel viewModel) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.WrappedLineColumnCenter });

    private static void MoveToLineEnd(TestViewModel viewModel) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.WrappedLineEnd });

    private static void MoveToLineLastNonWhitespaceCharacter(TestViewModel viewModel) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.WrappedLineLastNonWhitespaceCharacter });

    private static void MoveLeft(TestViewModel viewModel, int? value = null, string? by = null, bool? select = null) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.Left, By = by, Value = value, Select = select });

    private static void MoveRight(TestViewModel viewModel, int? value = null, string? by = null, bool? select = null) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.Right, By = by, Value = value, Select = select });

    private static void MoveUp(TestViewModel viewModel, int noOfLines = 1, bool? select = null) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.Up, By = CursorMove.RawUnit.WrappedLine, Value = noOfLines, Select = select });

    private static void MoveUpByBlankLine(TestViewModel viewModel, bool? select = null) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.PrevBlankLine, By = CursorMove.RawUnit.WrappedLine, Select = select });

    private static void MoveUpByModelLine(TestViewModel viewModel, int noOfLines = 1, bool? select = null) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.Up, Value = noOfLines, Select = select });

    private static void MoveDown(TestViewModel viewModel, int noOfLines = 1, bool? select = null) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.Down, By = CursorMove.RawUnit.WrappedLine, Value = noOfLines, Select = select });

    private static void MoveDownByBlankLine(TestViewModel viewModel, bool? select = null) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.NextBlankLine, By = CursorMove.RawUnit.WrappedLine, Select = select });

    private static void MoveDownByModelLine(TestViewModel viewModel, int noOfLines = 1, bool? select = null) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.Down, Value = noOfLines, Select = select });

    private static void MoveDownByFoldedLine(TestViewModel viewModel, int noOfLines = 1, bool? select = null) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.Down, By = CursorMove.RawUnit.FoldedLine, Value = noOfLines, Select = select });

    private static void MoveUpByFoldedLine(TestViewModel viewModel, int noOfLines = 1, bool? select = null) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.Up, By = CursorMove.RawUnit.FoldedLine, Value = noOfLines, Select = select });

    private static void MoveToTop(TestViewModel viewModel, int noOfLines = 1, bool? select = null) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.ViewPortTop, Value = noOfLines, Select = select });

    private static void MoveToCenter(TestViewModel viewModel, bool? select = null) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.ViewPortCenter, Select = select });

    private static void MoveToBottom(TestViewModel viewModel, int noOfLines = 1, bool? select = null) =>
        CoreNavigationCommands.CursorMove.RunCoreEditorCommand(viewModel, new() { To = CursorMove.RawDirection.ViewPortBottom, Value = noOfLines, Select = select });

    private static void CursorEqual(TestViewModel viewModel, int posLineNumber, int posColumn, int? selLineNumber = null, int? selColumn = null)
    {
        viewModel.GetPosition().ShouldBe(new TextPosition(posLineNumber, posColumn), "position equal");
        SelectionEqual(viewModel.GetSelection(), posLineNumber, posColumn, selLineNumber ?? posLineNumber, selColumn ?? posColumn);
    }

    private static void SelectionEqual(TextSelection selection, int posLineNumber, int posColumn, int selLineNumber, int selColumn) =>
        (selection.SelectionStartLineNumber, selection.SelectionStartColumn, selection.PositionLineNumber, selection.PositionColumn)
            .ShouldBe((selLineNumber, selColumn, posLineNumber, posColumn), "selection equal");

    private static void MoveTo(TestViewModel viewModel, int lineNumber, int column, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.MoveToSelect : CoreNavigationCommands.MoveTo).RunCoreEditorCommand(viewModel, new() { Position = new TextPosition(lineNumber, column) });

    private static void MoveToEndOfLine(TestViewModel viewModel, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.CursorEndSelect : CoreNavigationCommands.CursorEnd).RunCoreEditorCommand(viewModel, new());
}
