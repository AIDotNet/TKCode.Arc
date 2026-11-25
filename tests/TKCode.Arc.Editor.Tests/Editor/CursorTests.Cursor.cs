// Ported from vscode (src/vs/editor/test/browser/controller/cursor.test.ts, commit b57d5c9e), MIT license.
// This part: the helpers at the top of the file and the suite 'Editor Controller - Cursor'.
// Adaptations:
//  - the editor is a TestCodeEditor (Editor/TestCodeEditor.cs): a TextModel, the real ViewModel and its
//    CursorsController; upstream's CoreNavigationCommands are the engine's (Commands/CoreCommands.Navigation.cs);
//  - upstream's table driven tests (absoluteMoveCases, moveLeftCases, ...) are one [Fact] per case, named after the case;
//  - viewModel.onEvent is the view model's OutgoingEvent; editor.onDidChangeCursorPosition is the view model's
//    CursorStateChangedEvent (upstream's widget fires it for every cursor state change);
//  - 'saveState & restoreState' does not round trip the saved state through JSON (SavedCursorState is an immutable
//    record);
//  - 'setSelection / setPosition with source' registers no tokenization support for its language (the test does not
//    depend on tokens, and the engine has no tokenization registry to register an encoded tokenizer in);
//  - editor.changeDecorations adds the decoration on the model with the editor's id as owner.

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class CursorTests_EditorControllerCursor
{
    private const string Line1 = "    \tMy First Line\t ";
    private const string Line2 = "\tMy Second Line";
    private const string Line3 = "    Third Line🐶";
    private const string Line4 = "";
    private const string Line5 = "1";

    private const string Text =
        Line1 + "\r\n" +
        Line2 + "\n" +
        Line3 + "\n" +
        Line4 + "\r\n" +
        Line5;

    // --------- utils

    private static TextSelection P(int lineNumber, int column) => new(lineNumber, column, lineNumber, column);

    private static TextSelection S(int selectionStartLineNumber, int selectionStartColumn, int positionLineNumber, int positionColumn) =>
        new(selectionStartLineNumber, selectionStartColumn, positionLineNumber, positionColumn);

    private static void MoveTo(TestViewModel viewModel, int lineNumber, int column, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.MoveToSelect : CoreNavigationCommands.MoveTo).RunCoreEditorCommand(viewModel, new() { Position = new TextPosition(lineNumber, column) });

    private static void MoveLeft(TestViewModel viewModel, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.CursorLeftSelect : CoreNavigationCommands.CursorLeft).RunCoreEditorCommand(viewModel, new());

    private static void MoveRight(TestViewModel viewModel, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.CursorRightSelect : CoreNavigationCommands.CursorRight).RunCoreEditorCommand(viewModel, new());

    private static void MoveDown(TestViewModel viewModel, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.CursorDownSelect : CoreNavigationCommands.CursorDown).RunCoreEditorCommand(viewModel, new());

    private static void MoveUp(TestViewModel viewModel, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.CursorUpSelect : CoreNavigationCommands.CursorUp).RunCoreEditorCommand(viewModel, new());

    private static void MoveToBeginningOfLine(TestViewModel viewModel, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.CursorHomeSelect : CoreNavigationCommands.CursorHome).RunCoreEditorCommand(viewModel, new());

    private static void MoveToEndOfLine(TestViewModel viewModel, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.CursorEndSelect : CoreNavigationCommands.CursorEnd).RunCoreEditorCommand(viewModel, new());

    private static void MoveToBeginningOfBuffer(TestViewModel viewModel, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.CursorTopSelect : CoreNavigationCommands.CursorTop).RunCoreEditorCommand(viewModel, new());

    private static void MoveToEndOfBuffer(TestViewModel viewModel, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.CursorBottomSelect : CoreNavigationCommands.CursorBottom).RunCoreEditorCommand(viewModel, new());

    private static void AssertCursor(TestViewModel viewModel, params TextSelection[] selections)
    {
        var actual = viewModel.GetSelections().Select(s => s.ToString()).ToArray();
        var expected = selections.Select(s => s.ToString()).ToArray();
        actual.ShouldBe(expected);
    }

    /// <summary>CoreNavigationCommands.ColumnSelect.</summary>
    private static void ColumnSelect(IViewModel viewModel, TextPosition position, TextPosition viewPosition, int mouseColumn, bool doColumnSelect) =>
        CoreNavigationCommands.ColumnSelect.RunCoreEditorCommand(viewModel, new()
        {
            Position = position,
            ViewPosition = viewPosition,
            MouseColumn = mouseColumn,
            DoColumnSelect = doColumnSelect,
        });

    private static void CursorColumnSelectLeft(IViewModel viewModel) =>
        CoreNavigationCommands.CursorColumnSelectLeft.RunCoreEditorCommand(viewModel, new());

    private static void CursorColumnSelectRight(IViewModel viewModel) =>
        CoreNavigationCommands.CursorColumnSelectRight.RunCoreEditorCommand(viewModel, new());

    private static void CursorColumnSelectUp(IViewModel viewModel) =>
        CoreNavigationCommands.CursorColumnSelectUp.RunCoreEditorCommand(viewModel, new());

    private static void CursorColumnSelectDown(IViewModel viewModel) =>
        CoreNavigationCommands.CursorColumnSelectDown.RunCoreEditorCommand(viewModel, new());

    private static void RunTest(Action<TestCodeEditor, TestViewModel> callback) =>
        TestCodeEditor.WithTestCodeEditor(Text, new TestCodeEditorOptions(), callback);

    [Fact]
    public void Cursor_Initialized() => RunTest((editor, viewModel) =>
    {
        AssertCursor(viewModel, P(1, 1));
    });

    // --------- absolute move

    private static void AbsoluteMoveCase(int lineNumber, int column, bool inSelectionMode, TextSelection expected) => RunTest((editor, viewModel) =>
    {
        MoveTo(viewModel, lineNumber, column, inSelectionMode);
        AssertCursor(viewModel, expected);
    });

    [Fact]
    public void No_Move() => AbsoluteMoveCase(1, 1, false, P(1, 1));

    [Fact]
    public void Move() => AbsoluteMoveCase(1, 2, false, P(1, 2));

    [Fact]
    public void Move_In_Selection_Mode() => AbsoluteMoveCase(1, 2, true, S(1, 1, 1, 2));

    [Fact]
    public void Move_Beyond_Line_End() => AbsoluteMoveCase(1, 25, false, P(1, Line1.Length + 1));

    [Fact]
    public void Move_Empty_Line() => AbsoluteMoveCase(4, 20, false, P(4, 1));

    [Fact]
    public void Move_One_Char_Line() => AbsoluteMoveCase(5, 20, false, P(5, 2));

    [Fact]
    public void Selection_Down() => AbsoluteMoveCase(2, 1, true, S(1, 1, 2, 1));

    [Fact]
    public void Move_And_Then_Select() => RunTest((editor, viewModel) =>
    {
        MoveTo(viewModel, 2, 3);
        AssertCursor(viewModel, P(2, 3));

        MoveTo(viewModel, 2, 15, true);
        AssertCursor(viewModel, S(2, 3, 2, 15));

        MoveTo(viewModel, 1, 2, true);
        AssertCursor(viewModel, S(2, 3, 1, 2));
    });

    // --------- move left

    [Fact]
    public void Move_Left_On_Top_Left_Position() => RunTest((editor, viewModel) =>
    {
        MoveLeft(viewModel);
        AssertCursor(viewModel, P(1, 1));
    });

    private static void MoveLeftCase(int lineNumber, int column, bool inSelectionMode, TextSelection expected) => RunTest((editor, viewModel) =>
    {
        MoveTo(viewModel, lineNumber, column);
        AssertCursor(viewModel, P(lineNumber, column));
        MoveLeft(viewModel, inSelectionMode);
        AssertCursor(viewModel, expected);
    });

    [Fact]
    public void Move_Left() => MoveLeftCase(1, 3, false, P(1, 2));

    [Fact]
    public void Move_Left_With_Surrogate_Pair() => MoveLeftCase(3, 17, false, P(3, 15));

    [Fact]
    public void Move_Left_Goes_To_Previous_Row() => MoveLeftCase(2, 1, false, P(1, 21));

    [Fact]
    public void Move_Left_Selection() => MoveLeftCase(2, 1, true, S(2, 1, 1, 21));

    // --------- move right

    private static void MoveRightCase(int lineNumber, int column, bool inSelectionMode, TextSelection expected) => RunTest((editor, viewModel) =>
    {
        MoveTo(viewModel, lineNumber, column);
        AssertCursor(viewModel, P(lineNumber, column));
        MoveRight(viewModel, inSelectionMode);
        AssertCursor(viewModel, expected);
    });

    [Fact]
    public void Move_Right_On_Bottom_Right_Position() => MoveRightCase(5, 2, false, P(5, 2));

    [Fact]
    public void Move_Right() => MoveRightCase(1, 3, false, P(1, 4));

    [Fact]
    public void Move_Right_With_Surrogate_Pair() => MoveRightCase(3, 15, false, P(3, 17));

    [Fact]
    public void Move_Right_Goes_To_Next_Row() => MoveRightCase(1, 21, false, P(2, 1));

    [Fact]
    public void Move_Right_Selection() => MoveRightCase(1, 21, true, S(1, 21, 2, 1));

    // --------- move down

    [Fact]
    public void Move_Down() => RunTest((editor, viewModel) =>
    {
        MoveDown(viewModel);
        AssertCursor(viewModel, P(2, 1));
        MoveDown(viewModel);
        AssertCursor(viewModel, P(3, 1));
        MoveDown(viewModel);
        AssertCursor(viewModel, P(4, 1));
        MoveDown(viewModel);
        AssertCursor(viewModel, P(5, 1));
        MoveDown(viewModel);
        AssertCursor(viewModel, P(5, 2));
    });

    [Fact]
    public void Move_Down_With_Selection() => RunTest((editor, viewModel) =>
    {
        MoveDown(viewModel, true);
        AssertCursor(viewModel, S(1, 1, 2, 1));
        MoveDown(viewModel, true);
        AssertCursor(viewModel, S(1, 1, 3, 1));
        MoveDown(viewModel, true);
        AssertCursor(viewModel, S(1, 1, 4, 1));
        MoveDown(viewModel, true);
        AssertCursor(viewModel, S(1, 1, 5, 1));
        MoveDown(viewModel, true);
        AssertCursor(viewModel, S(1, 1, 5, 2));
    });

    [Fact]
    public void Move_Down_With_Tabs() => RunTest((editor, viewModel) =>
    {
        MoveTo(viewModel, 1, 5);
        AssertCursor(viewModel, P(1, 5));
        MoveDown(viewModel);
        AssertCursor(viewModel, P(2, 2));
        MoveDown(viewModel);
        AssertCursor(viewModel, P(3, 5));
        MoveDown(viewModel);
        AssertCursor(viewModel, P(4, 1));
        MoveDown(viewModel);
        AssertCursor(viewModel, P(5, 2));
    });

    // --------- move up

    [Fact]
    public void Move_Up() => RunTest((editor, viewModel) =>
    {
        MoveTo(viewModel, 3, 5);
        AssertCursor(viewModel, P(3, 5));

        MoveUp(viewModel);
        AssertCursor(viewModel, P(2, 2));

        MoveUp(viewModel);
        AssertCursor(viewModel, P(1, 5));
    });

    [Fact]
    public void Move_Up_With_Selection() => RunTest((editor, viewModel) =>
    {
        MoveTo(viewModel, 3, 5);
        AssertCursor(viewModel, P(3, 5));

        MoveUp(viewModel, true);
        AssertCursor(viewModel, S(3, 5, 2, 2));

        MoveUp(viewModel, true);
        AssertCursor(viewModel, S(3, 5, 1, 5));
    });

    [Fact]
    public void Move_Up_And_Down_With_Tabs() => RunTest((editor, viewModel) =>
    {
        MoveTo(viewModel, 1, 5);
        AssertCursor(viewModel, P(1, 5));
        MoveDown(viewModel);
        MoveDown(viewModel);
        MoveDown(viewModel);
        MoveDown(viewModel);
        AssertCursor(viewModel, P(5, 2));
        MoveUp(viewModel);
        AssertCursor(viewModel, P(4, 1));
        MoveUp(viewModel);
        AssertCursor(viewModel, P(3, 5));
        MoveUp(viewModel);
        AssertCursor(viewModel, P(2, 2));
        MoveUp(viewModel);
        AssertCursor(viewModel, P(1, 5));
    });

    [Fact]
    public void Move_Up_And_Down_With_End_Of_Lines_Starting_From_A_Long_One() => RunTest((editor, viewModel) =>
    {
        MoveToEndOfLine(viewModel);
        AssertCursor(viewModel, P(1, Line1.Length + 1));
        MoveToEndOfLine(viewModel);
        AssertCursor(viewModel, P(1, Line1.Length + 1));
        MoveDown(viewModel);
        AssertCursor(viewModel, P(2, Line2.Length + 1));
        MoveDown(viewModel);
        AssertCursor(viewModel, P(3, Line3.Length + 1));
        MoveDown(viewModel);
        AssertCursor(viewModel, P(4, Line4.Length + 1));
        MoveDown(viewModel);
        AssertCursor(viewModel, P(5, Line5.Length + 1));
        MoveUp(viewModel);
        MoveUp(viewModel);
        MoveUp(viewModel);
        MoveUp(viewModel);
        AssertCursor(viewModel, P(1, Line1.Length + 1));
    });

    [Fact]
    public void Issue_44465_Cursor_Position_Not_Correct_When_Move() => RunTest((editor, viewModel) =>
    {
        viewModel.SetSelections("test", [S(1, 5, 1, 5)]);
        // going once up on the first line remembers the offset visual columns
        MoveUp(viewModel);
        AssertCursor(viewModel, P(1, 1));
        MoveDown(viewModel);
        AssertCursor(viewModel, P(2, 2));
        MoveUp(viewModel);
        AssertCursor(viewModel, P(1, 5));

        // going twice up on the first line discards the offset visual columns
        MoveUp(viewModel);
        AssertCursor(viewModel, P(1, 1));
        MoveUp(viewModel);
        AssertCursor(viewModel, P(1, 1));
        MoveDown(viewModel);
        AssertCursor(viewModel, P(2, 1));
    });

    private static readonly TestCodeEditorOptions s_wrapAt20 = new()
    {
        WrappingIndent = WrappingIndent.Indent,
        WordWrap = TestWordWrap.WordWrapColumn,
        WordWrapColumn = 20,
    };

    private static List<string> RunCursorDownAndUp(TestViewModel viewModel)
    {
        var cursorPositions = new List<string>();
        void ReportCursorPosition() => cursorPositions.Add(viewModel.GetCursorStates()[0].ViewState.Position.ToString());

        ReportCursorPosition();
        CoreNavigationCommands.CursorDown.RunCoreEditorCommand(viewModel, new());
        ReportCursorPosition();
        CoreNavigationCommands.CursorDown.RunCoreEditorCommand(viewModel, new());
        ReportCursorPosition();
        CoreNavigationCommands.CursorDown.RunCoreEditorCommand(viewModel, new());
        ReportCursorPosition();
        CoreNavigationCommands.CursorDown.RunCoreEditorCommand(viewModel, new());

        ReportCursorPosition();
        CoreNavigationCommands.CursorUp.RunCoreEditorCommand(viewModel, new());
        ReportCursorPosition();
        CoreNavigationCommands.CursorUp.RunCoreEditorCommand(viewModel, new());
        ReportCursorPosition();
        CoreNavigationCommands.CursorUp.RunCoreEditorCommand(viewModel, new());
        ReportCursorPosition();
        CoreNavigationCommands.CursorUp.RunCoreEditorCommand(viewModel, new());
        ReportCursorPosition();
        return cursorPositions;
    }

    [Fact]
    public void Issue_144041_Cursor_Up_Down_Works()
    {
        using var model = TestTextModel.Create(string.Join('\n', "Word1 Word2 Word3 Word4", "Word5 Word6 Word7 Word8"));

        TestCodeEditor.WithTestCodeEditor(model, s_wrapAt20, (editor, viewModel) =>
        {
            viewModel.SetSelections("test", [S(1, 1, 1, 1)]);

            RunCursorDownAndUp(viewModel).ShouldBe(
            [
                "(1,1)",
                "(2,5)",
                "(3,1)",
                "(4,5)",
                "(4,10)",
                "(3,1)",
                "(2,5)",
                "(1,1)",
                "(1,1)",
            ]);
        });
    }

    [Fact]
    public void Issue_140195_Cursor_Up_Down_Makes_Progress()
    {
        using var model = TestTextModel.Create(string.Join('\n', "Word1 Word2 Word3 Word4", "Word5 Word6 Word7 Word8"));

        TestCodeEditor.WithTestCodeEditor(model, s_wrapAt20, (editor, viewModel) =>
        {
            // editor.changeDecorations: the editor's decorations
            model.DeltaDecorations(
                [],
                [
                    new ModelDeltaDecoration(
                        new TextRange(1, 22, 1, 22),
                        new ModelDecorationOptions("test")
                        {
                            ShowIfCollapsed = true,
                            After = new ModelDecorationInjectedTextOptions("some very very very very very very very very long text"),
                        }),
                ],
                ownerId: 1);
            viewModel.SetSelections("test", [S(1, 1, 1, 1)]);

            RunCursorDownAndUp(viewModel).ShouldBe(
            [
                "(1,1)",
                "(2,5)",
                "(5,19)",
                "(6,1)",
                "(7,5)",
                "(6,1)",
                "(2,8)",
                "(1,1)",
                "(1,1)",
            ]);
        });
    }

    // --------- move to beginning of line

    [Fact]
    public void Move_To_Beginning_Of_Line() => RunTest((editor, viewModel) =>
    {
        MoveToBeginningOfLine(viewModel);
        AssertCursor(viewModel, P(1, 6));
        MoveToBeginningOfLine(viewModel);
        AssertCursor(viewModel, P(1, 1));
    });

    private static void MoveToBeginningOfLineCase(int column, bool inSelectionMode, TextSelection firstExpected, TextSelection secondExpected) => RunTest((editor, viewModel) =>
    {
        MoveTo(viewModel, 1, column);
        MoveToBeginningOfLine(viewModel, inSelectionMode);
        AssertCursor(viewModel, firstExpected);
        MoveToBeginningOfLine(viewModel, inSelectionMode);
        AssertCursor(viewModel, secondExpected);
    });

    [Fact]
    public void Move_To_Beginning_Of_Line_From_Within_Line() => MoveToBeginningOfLineCase(8, false, P(1, 6), P(1, 1));

    [Fact]
    public void Move_To_Beginning_Of_Line_From_Whitespace_At_Beginning_Of_Line() => MoveToBeginningOfLineCase(2, false, P(1, 6), P(1, 1));

    [Fact]
    public void Move_To_Beginning_Of_Line_From_Within_Line_Selection() => MoveToBeginningOfLineCase(8, true, S(1, 8, 1, 6), S(1, 8, 1, 1));

    private static void MoveToBeginningOfLineWithSelectionCase(int fromLineNumber, int fromColumn, int toLineNumber, int toColumn, bool inSelectionMode, TextSelection expected) =>
        RunTest((editor, viewModel) =>
        {
            MoveTo(viewModel, fromLineNumber, fromColumn);
            MoveTo(viewModel, toLineNumber, toColumn, true);
            MoveToBeginningOfLine(viewModel, inSelectionMode);
            AssertCursor(viewModel, expected);
        });

    [Fact]
    public void Move_To_Beginning_Of_Line_With_Selection_Multiline_Forward() => MoveToBeginningOfLineWithSelectionCase(1, 8, 3, 9, false, S(3, 5, 3, 5));

    [Fact]
    public void Move_To_Beginning_Of_Line_With_Selection_Multiline_Backward() => MoveToBeginningOfLineWithSelectionCase(3, 9, 1, 8, false, S(1, 6, 1, 6));

    [Fact]
    public void Move_To_Beginning_Of_Line_With_Selection_Single_Line_Forward() => MoveToBeginningOfLineWithSelectionCase(3, 2, 3, 9, false, S(3, 5, 3, 5));

    [Fact]
    public void Move_To_Beginning_Of_Line_With_Selection_Single_Line_Backward() => MoveToBeginningOfLineWithSelectionCase(3, 9, 3, 2, false, S(3, 5, 3, 5));

    [Fact]
    public void Issue_15401_End_Key_Is_Behaving_Weird_When_Text_Is_Selected_Part_1() => MoveToBeginningOfLineWithSelectionCase(1, 8, 3, 9, false, S(3, 5, 3, 5));

    [Fact]
    public void Issue_17011_Shift_Home_End_Now_Go_To_The_End_Of_The_Selection_Starts_Line_Not_The_Selections_End() =>
        MoveToBeginningOfLineWithSelectionCase(1, 8, 3, 9, true, S(1, 8, 3, 5));

    // --------- move to end of line

    [Fact]
    public void Move_To_End_Of_Line() => RunTest((editor, viewModel) =>
    {
        MoveToEndOfLine(viewModel);
        AssertCursor(viewModel, P(1, Line1.Length + 1));
        MoveToEndOfLine(viewModel);
        AssertCursor(viewModel, P(1, Line1.Length + 1));
    });

    private static void MoveToEndOfLineCase(int column, bool inSelectionMode, TextSelection expected) => RunTest((editor, viewModel) =>
    {
        MoveTo(viewModel, 1, column);
        MoveToEndOfLine(viewModel, inSelectionMode);
        AssertCursor(viewModel, expected);
        MoveToEndOfLine(viewModel, inSelectionMode);
        AssertCursor(viewModel, expected);
    });

    [Fact]
    public void Move_To_End_Of_Line_From_Within_Line() => MoveToEndOfLineCase(6, false, P(1, Line1.Length + 1));

    [Fact]
    public void Move_To_End_Of_Line_From_Whitespace_At_End_Of_Line() => MoveToEndOfLineCase(20, false, P(1, Line1.Length + 1));

    [Fact]
    public void Move_To_End_Of_Line_From_Within_Line_Selection() => MoveToEndOfLineCase(6, true, S(1, 6, 1, Line1.Length + 1));

    private static void MoveToEndOfLineWithSelectionCase(int fromLineNumber, int fromColumn, int toLineNumber, int toColumn, TextSelection expected) =>
        RunTest((editor, viewModel) =>
        {
            MoveTo(viewModel, fromLineNumber, fromColumn);
            MoveTo(viewModel, toLineNumber, toColumn, true);
            MoveToEndOfLine(viewModel, false);
            AssertCursor(viewModel, expected);
        });

    [Fact]
    public void Move_To_End_Of_Line_With_Selection_Multiline_Forward() => MoveToEndOfLineWithSelectionCase(1, 1, 3, 9, S(3, 17, 3, 17));

    [Fact]
    public void Move_To_End_Of_Line_With_Selection_Multiline_Backward() => MoveToEndOfLineWithSelectionCase(3, 9, 1, 1, S(1, 21, 1, 21));

    [Fact]
    public void Move_To_End_Of_Line_With_Selection_Single_Line_Forward() => MoveToEndOfLineWithSelectionCase(3, 1, 3, 9, S(3, 17, 3, 17));

    [Fact]
    public void Move_To_End_Of_Line_With_Selection_Single_Line_Backward() => MoveToEndOfLineWithSelectionCase(3, 9, 3, 1, S(3, 17, 3, 17));

    [Fact]
    public void Issue_15401_End_Key_Is_Behaving_Weird_When_Text_Is_Selected_Part_2() => MoveToEndOfLineWithSelectionCase(1, 1, 3, 9, S(3, 17, 3, 17));

    // --------- move to beginning of buffer

    [Fact]
    public void Move_To_Beginning_Of_Buffer() => RunTest((editor, viewModel) =>
    {
        MoveToBeginningOfBuffer(viewModel);
        AssertCursor(viewModel, P(1, 1));
    });

    private static void MoveToBeginningOfBufferCase(int lineNumber, int column, bool inSelectionMode, TextSelection expected) => RunTest((editor, viewModel) =>
    {
        MoveTo(viewModel, lineNumber, column);
        MoveToBeginningOfBuffer(viewModel, inSelectionMode);
        AssertCursor(viewModel, expected);
    });

    [Fact]
    public void Move_To_Beginning_Of_Buffer_From_Within_First_Line() => MoveToBeginningOfBufferCase(1, 3, false, P(1, 1));

    [Fact]
    public void Move_To_Beginning_Of_Buffer_From_Within_Another_Line() => MoveToBeginningOfBufferCase(3, 3, false, P(1, 1));

    [Fact]
    public void Move_To_Beginning_Of_Buffer_From_Within_First_Line_Selection() => MoveToBeginningOfBufferCase(1, 3, true, S(1, 3, 1, 1));

    [Fact]
    public void Move_To_Beginning_Of_Buffer_From_Within_Another_Line_Selection() => MoveToBeginningOfBufferCase(3, 3, true, S(3, 3, 1, 1));

    // --------- move to end of buffer

    [Fact]
    public void Move_To_End_Of_Buffer() => RunTest((editor, viewModel) =>
    {
        MoveToEndOfBuffer(viewModel);
        AssertCursor(viewModel, P(5, Line5.Length + 1));
    });

    private static void MoveToEndOfBufferCase(int lineNumber, int column, bool inSelectionMode, TextSelection expected) => RunTest((editor, viewModel) =>
    {
        MoveTo(viewModel, lineNumber, column);
        MoveToEndOfBuffer(viewModel, inSelectionMode);
        AssertCursor(viewModel, expected);
    });

    [Fact]
    public void Move_To_End_Of_Buffer_From_Within_Last_Line() => MoveToEndOfBufferCase(5, 1, false, P(5, Line5.Length + 1));

    [Fact]
    public void Move_To_End_Of_Buffer_From_Within_Another_Line() => MoveToEndOfBufferCase(3, 3, false, P(5, Line5.Length + 1));

    [Fact]
    public void Move_To_End_Of_Buffer_From_Within_Last_Line_Selection() => MoveToEndOfBufferCase(5, 1, true, S(5, 1, 5, Line5.Length + 1));

    [Fact]
    public void Move_To_End_Of_Buffer_From_Within_Another_Line_Selection() => MoveToEndOfBufferCase(3, 3, true, S(3, 3, 5, Line5.Length + 1));

    // --------- misc

    [Fact]
    public void Select_All() => RunTest((editor, viewModel) =>
    {
        CoreNavigationCommands.SelectAll.RunCoreEditorCommand(viewModel, new());
        AssertCursor(viewModel, S(1, 1, 5, Line5.Length + 1));
    });

    // --------- eventing

    [Fact]
    public void No_Move_Doesnt_Trigger_Event() => RunTest((editor, viewModel) =>
    {
        var events = new List<OutgoingViewModelEvent>();
        Action<OutgoingViewModelEvent> listener = e => events.Add(e);
        viewModel.OutgoingEvent += listener;
        MoveTo(viewModel, 1, 1);
        viewModel.OutgoingEvent -= listener;
        events.ShouldBeEmpty("was not expecting event");
    });

    [Fact]
    public void Move_Eventing() => RunTest((editor, viewModel) =>
    {
        var events = 0;
        Action<OutgoingViewModelEvent> listener = e =>
        {
            if (e is CursorStateChangedEvent cursorEvent)
            {
                events++;
                cursorEvent.Selections.ShouldBe([S(1, 2, 1, 2)]);
            }
        };
        viewModel.OutgoingEvent += listener;
        MoveTo(viewModel, 1, 2);
        events.ShouldBe(1, "receives 1 event");
        viewModel.OutgoingEvent -= listener;
    });

    [Fact]
    public void Move_In_Selection_Mode_Eventing() => RunTest((editor, viewModel) =>
    {
        var events = 0;
        Action<OutgoingViewModelEvent> listener = e =>
        {
            if (e is CursorStateChangedEvent cursorEvent)
            {
                events++;
                cursorEvent.Selections.ShouldBe([S(1, 1, 1, 2)]);
            }
        };
        viewModel.OutgoingEvent += listener;
        MoveTo(viewModel, 1, 2, true);
        events.ShouldBe(1, "receives 1 event");
        viewModel.OutgoingEvent -= listener;
    });

    // --------- state save & restore

    [Fact]
    public void SaveState_And_RestoreState() => RunTest((editor, viewModel) =>
    {
        MoveTo(viewModel, 2, 1, true);
        AssertCursor(viewModel, S(1, 1, 2, 1));

        var savedState = viewModel.SaveCursorState().ToArray();

        MoveTo(viewModel, 1, 1, false);
        AssertCursor(viewModel, P(1, 1));

        viewModel.RestoreCursorState(savedState);
        AssertCursor(viewModel, S(1, 1, 2, 1));
    });

    // --------- updating cursor

    [Fact]
    public void Independent_Model_Edit_1() => RunTest((editor, viewModel) =>
    {
        MoveTo(viewModel, 2, 16, true);

        editor.GetModel().ApplyEdits([EditOperation.Delete(new TextRange(2, 1, 2, 2))]);
        AssertCursor(viewModel, S(1, 1, 2, 15));
    });

    [Fact]
    public void Column_Select_1() => TestCodeEditor.WithTestCodeEditor(
        [
            "\tprivate compute(a:number): boolean {",
            "\t\tif (a + 3 === 0 || a + 5 === 0) {",
            "\t\t\treturn false;",
            "\t\t}",
            "\t}",
        ],
        new TestCodeEditorOptions(),
        (editor, viewModel) =>
        {
            MoveTo(viewModel, 1, 7, false);
            AssertCursor(viewModel, P(1, 7));

            ColumnSelect(viewModel, new TextPosition(4, 4), new TextPosition(4, 4), 15, true);

            AssertCursor(viewModel,
                S(1, 7, 1, 12),
                S(2, 4, 2, 9),
                S(3, 3, 3, 6),
                S(4, 4, 4, 4));
        });

    [Fact]
    public void Grapheme_Breaking() => TestCodeEditor.WithTestCodeEditor(
        [
            "abcabc",
            "ãããããã",
            "辻\U000E0100辻\U000E0100辻\U000E0100",
            "பு",
        ],
        new TestCodeEditorOptions(),
        (editor, viewModel) =>
        {
            viewModel.SetSelections("test", [S(2, 1, 2, 1)]);
            MoveRight(viewModel);
            AssertCursor(viewModel, P(2, 3));
            MoveLeft(viewModel);
            AssertCursor(viewModel, P(2, 1));

            viewModel.SetSelections("test", [S(3, 1, 3, 1)]);
            MoveRight(viewModel);
            AssertCursor(viewModel, P(3, 4));
            MoveLeft(viewModel);
            AssertCursor(viewModel, P(3, 1));

            viewModel.SetSelections("test", [S(4, 1, 4, 1)]);
            MoveRight(viewModel);
            AssertCursor(viewModel, P(4, 3));
            MoveLeft(viewModel);
            AssertCursor(viewModel, P(4, 1));

            viewModel.SetSelections("test", [S(1, 3, 1, 3)]);
            MoveDown(viewModel);
            AssertCursor(viewModel, P(2, 5));
            MoveDown(viewModel);
            AssertCursor(viewModel, P(3, 4));
            MoveUp(viewModel);
            AssertCursor(viewModel, P(2, 5));
            MoveUp(viewModel);
            AssertCursor(viewModel, P(1, 3));
        });

    [Fact]
    public void Issue_4905_Column_Select_Is_Biased_To_The_Right() => TestCodeEditor.WithTestCodeEditor(
        string.Join('\n',
            "var gulp = require(\"gulp\");",
            "var path = require(\"path\");",
            "var rimraf = require(\"rimraf\");",
            "var isarray = require(\"isarray\");",
            "var merge = require(\"merge-stream\");",
            "var concat = require(\"gulp-concat\");",
            "var newer = require(\"gulp-newer\");"),
        new TestCodeEditorOptions(),
        (editor, viewModel) =>
        {
            MoveTo(viewModel, 1, 4, false);
            AssertCursor(viewModel, P(1, 4));

            ColumnSelect(viewModel, new TextPosition(4, 1), new TextPosition(4, 1), 1, true);

            AssertCursor(viewModel,
                S(1, 4, 1, 1),
                S(2, 4, 2, 1),
                S(3, 4, 3, 1),
                S(4, 4, 4, 1));
        });

    private static readonly string[] s_propertyLines =
    [
        "<property id=\"SomeThing\" key=\"SomeKey\" value=\"000\"/>",
        "<property id=\"SomeThing\" key=\"SomeKey\" value=\"000\"/>",
        "<property id=\"SomeThing\" Key=\"SomeKey\" value=\"000\"/>",
        "<property id=\"SomeThing\" key=\"SomeKey\" value=\"000\"/>",
        "<property id=\"SomeThing\" key=\"SoMEKEy\" value=\"000\"/>",
        "<property id=\"SomeThing\" key=\"SomeKey\" value=\"000\"/>",
        "<property id=\"SomeThing\" key=\"SomeKey\" value=\"000\"/>",
        "<property id=\"SomeThing\" key=\"SomeKey\" valuE=\"000\"/>",
        "<property id=\"SomeThing\" key=\"SomeKey\" value=\"000\"/>",
        "<property id=\"SomeThing\" key=\"SomeKey\" value=\"00X\"/>",
    ];

    [Fact]
    public void Issue_20087_Column_Select_With_Mouse() => TestCodeEditor.WithTestCodeEditor(
        string.Join('\n', s_propertyLines),
        new TestCodeEditorOptions(),
        (editor, viewModel) =>
        {
            MoveTo(viewModel, 10, 10, false);
            AssertCursor(viewModel, P(10, 10));

            ColumnSelect(viewModel, new TextPosition(1, 1), new TextPosition(1, 1), 1, true);
            AssertCursor(viewModel,
                S(10, 10, 10, 1),
                S(9, 10, 9, 1),
                S(8, 10, 8, 1),
                S(7, 10, 7, 1),
                S(6, 10, 6, 1),
                S(5, 10, 5, 1),
                S(4, 10, 4, 1),
                S(3, 10, 3, 1),
                S(2, 10, 2, 1),
                S(1, 10, 1, 1));

            ColumnSelect(viewModel, new TextPosition(1, 1), new TextPosition(1, 1), 1, true);
            AssertCursor(viewModel,
                S(10, 10, 10, 1),
                S(9, 10, 9, 1),
                S(8, 10, 8, 1),
                S(7, 10, 7, 1),
                S(6, 10, 6, 1),
                S(5, 10, 5, 1),
                S(4, 10, 4, 1),
                S(3, 10, 3, 1),
                S(2, 10, 2, 1),
                S(1, 10, 1, 1));
        });

    [Fact]
    public void Issue_20087_Column_Select_With_Keyboard() => TestCodeEditor.WithTestCodeEditor(
        string.Join('\n', s_propertyLines),
        new TestCodeEditorOptions(),
        (editor, viewModel) =>
        {
            MoveTo(viewModel, 10, 10, false);
            AssertCursor(viewModel, P(10, 10));

            CursorColumnSelectLeft(viewModel);
            AssertCursor(viewModel, S(10, 10, 10, 9));

            CursorColumnSelectLeft(viewModel);
            AssertCursor(viewModel, S(10, 10, 10, 8));

            CursorColumnSelectRight(viewModel);
            AssertCursor(viewModel, S(10, 10, 10, 9));

            CursorColumnSelectUp(viewModel);
            AssertCursor(viewModel,
                S(10, 10, 10, 9),
                S(9, 10, 9, 9));

            CursorColumnSelectDown(viewModel);
            AssertCursor(viewModel, S(10, 10, 10, 9));
        });

    [Fact]
    public void Issue_118062_Column_Selection_Cannot_Select_First_Position_Of_A_Line() => TestCodeEditor.WithTestCodeEditor(
        "hello world",
        new TestCodeEditorOptions(),
        (editor, viewModel) =>
        {
            MoveTo(viewModel, 1, 2, false);
            AssertCursor(viewModel, P(1, 2));

            CursorColumnSelectLeft(viewModel);
            AssertCursor(viewModel, S(1, 2, 1, 1));
        });

    private static void Repeat(int count, Action action)
    {
        for (var i = 0; i < count; i++)
        {
            action();
        }
    }

    [Fact]
    public void Column_Select_With_Keyboard() => TestCodeEditor.WithTestCodeEditor(
        string.Join('\n',
            "var gulp = require(\"gulp\");",
            "var path = require(\"path\");",
            "var rimraf = require(\"rimraf\");",
            "var isarray = require(\"isarray\");",
            "var merge = require(\"merge-stream\");",
            "var concat = require(\"gulp-concat\");",
            "var newer = require(\"gulp-newer\");"),
        new TestCodeEditorOptions(),
        (editor, viewModel) =>
        {
            MoveTo(viewModel, 1, 4, false);
            AssertCursor(viewModel, P(1, 4));

            CursorColumnSelectRight(viewModel);
            AssertCursor(viewModel, S(1, 4, 1, 5));

            CursorColumnSelectDown(viewModel);
            AssertCursor(viewModel,
                S(1, 4, 1, 5),
                S(2, 4, 2, 5));

            CursorColumnSelectDown(viewModel);
            AssertCursor(viewModel,
                S(1, 4, 1, 5),
                S(2, 4, 2, 5),
                S(3, 4, 3, 5));

            Repeat(4, () => CursorColumnSelectDown(viewModel));
            AssertCursor(viewModel,
                S(1, 4, 1, 5),
                S(2, 4, 2, 5),
                S(3, 4, 3, 5),
                S(4, 4, 4, 5),
                S(5, 4, 5, 5),
                S(6, 4, 6, 5),
                S(7, 4, 7, 5));

            CursorColumnSelectRight(viewModel);
            AssertCursor(viewModel,
                S(1, 4, 1, 6),
                S(2, 4, 2, 6),
                S(3, 4, 3, 6),
                S(4, 4, 4, 6),
                S(5, 4, 5, 6),
                S(6, 4, 6, 6),
                S(7, 4, 7, 6));

            // 10 times
            Repeat(10, () => CursorColumnSelectRight(viewModel));
            AssertCursor(viewModel,
                S(1, 4, 1, 16),
                S(2, 4, 2, 16),
                S(3, 4, 3, 16),
                S(4, 4, 4, 16),
                S(5, 4, 5, 16),
                S(6, 4, 6, 16),
                S(7, 4, 7, 16));

            // 10 times
            Repeat(10, () => CursorColumnSelectRight(viewModel));
            AssertCursor(viewModel,
                S(1, 4, 1, 26),
                S(2, 4, 2, 26),
                S(3, 4, 3, 26),
                S(4, 4, 4, 26),
                S(5, 4, 5, 26),
                S(6, 4, 6, 26),
                S(7, 4, 7, 26));

            // 2 times => reaching the ending of lines 1 and 2
            Repeat(2, () => CursorColumnSelectRight(viewModel));
            AssertCursor(viewModel,
                S(1, 4, 1, 28),
                S(2, 4, 2, 28),
                S(3, 4, 3, 28),
                S(4, 4, 4, 28),
                S(5, 4, 5, 28),
                S(6, 4, 6, 28),
                S(7, 4, 7, 28));

            // 4 times => reaching the ending of line 3
            Repeat(4, () => CursorColumnSelectRight(viewModel));
            AssertCursor(viewModel,
                S(1, 4, 1, 28),
                S(2, 4, 2, 28),
                S(3, 4, 3, 32),
                S(4, 4, 4, 32),
                S(5, 4, 5, 32),
                S(6, 4, 6, 32),
                S(7, 4, 7, 32));

            // 2 times => reaching the ending of line 4
            Repeat(2, () => CursorColumnSelectRight(viewModel));
            AssertCursor(viewModel,
                S(1, 4, 1, 28),
                S(2, 4, 2, 28),
                S(3, 4, 3, 32),
                S(4, 4, 4, 34),
                S(5, 4, 5, 34),
                S(6, 4, 6, 34),
                S(7, 4, 7, 34));

            // 1 time => reaching the ending of line 7
            CursorColumnSelectRight(viewModel);
            AssertCursor(viewModel,
                S(1, 4, 1, 28),
                S(2, 4, 2, 28),
                S(3, 4, 3, 32),
                S(4, 4, 4, 34),
                S(5, 4, 5, 35),
                S(6, 4, 6, 35),
                S(7, 4, 7, 35));

            // 3 times => reaching the ending of lines 5 & 6
            Repeat(3, () => CursorColumnSelectRight(viewModel));
            AssertCursor(viewModel,
                S(1, 4, 1, 28),
                S(2, 4, 2, 28),
                S(3, 4, 3, 32),
                S(4, 4, 4, 34),
                S(5, 4, 5, 37),
                S(6, 4, 6, 37),
                S(7, 4, 7, 35));

            // cannot go anywhere anymore
            CursorColumnSelectRight(viewModel);
            AssertCursor(viewModel,
                S(1, 4, 1, 28),
                S(2, 4, 2, 28),
                S(3, 4, 3, 32),
                S(4, 4, 4, 34),
                S(5, 4, 5, 37),
                S(6, 4, 6, 37),
                S(7, 4, 7, 35));

            // cannot go anywhere anymore even if we insist
            Repeat(4, () => CursorColumnSelectRight(viewModel));
            AssertCursor(viewModel,
                S(1, 4, 1, 28),
                S(2, 4, 2, 28),
                S(3, 4, 3, 32),
                S(4, 4, 4, 34),
                S(5, 4, 5, 37),
                S(6, 4, 6, 37),
                S(7, 4, 7, 35));

            // can easily go back
            CursorColumnSelectLeft(viewModel);
            AssertCursor(viewModel,
                S(1, 4, 1, 28),
                S(2, 4, 2, 28),
                S(3, 4, 3, 32),
                S(4, 4, 4, 34),
                S(5, 4, 5, 36),
                S(6, 4, 6, 36),
                S(7, 4, 7, 35));
        });

    [Fact]
    public void SetSelection_SetPosition_With_Source()
    {
        const string languageId = "modelModeTest1";
        using var model = TestTextModel.Create("Just text", languageId);

        TestCodeEditor.WithTestCodeEditor(model, new TestCodeEditorOptions(), (editor1, cursor1) =>
        {
            CursorStateChangedEvent? e = null;
            Action<OutgoingViewModelEvent> listener = ev =>
            {
                if (ev is CursorStateChangedEvent cursorEvent)
                {
                    e = cursorEvent;
                }
            };
            cursor1.OutgoingEvent += listener;

            editor1.SetSelection(new TextRange(1, 2, 1, 3), "navigation");
            e.ShouldNotBeNull().Source.ShouldBe("navigation");

            e = null;
            editor1.SetPosition(new TextPosition(1, 2), "navigation");
            e.ShouldNotBeNull().Source.ShouldBe("navigation");
            cursor1.OutgoingEvent -= listener;
        });
    }
}
