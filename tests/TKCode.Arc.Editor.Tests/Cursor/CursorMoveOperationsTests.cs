// Tests for MoveOperations, ported from vscode (commit b57d5c9e, MIT license):
//  - src/vs/editor/test/browser/controller/cursor.test.ts, suite 'Editor Controller - Cursor': the left, right, up,
//    down, line start and end and buffer start and end moves, 'issue #44465' and 'grapheme breaking';
//  - src/vs/editor/test/browser/controller/cursorMoveCommand.test.ts, suites 'Cursor move command test' (the left,
//    right, up and down moves) and 'Cursor move by blankline test'.
// Upstream runs the core navigation commands on a test code editor; here the moves are driven through the operations
// on a text model (OperationsTestEditor), the way CursorMoveCommands calls them when the view is the model (no
// wrapping): CursorLeft/Right/Up/Down are MoveOperations.MoveLeft/Right/Up/Down by 1, CursorHome and CursorEnd are
// MoveToBeginningOfLine and MoveToEndOfLine (not sticky), CursorTop and CursorBottom MoveToBeginningOfBuffer and
// MoveToEndOfBuffer; a half line is Math.round(lineLength / 2) columns.
//
// Not ported (they need the view model, wrapping, decorations or the controller): absolute moves and 'move and then
// select' (CursorMoveCommands.moveTo), 'select all', eventing, save/restore state, 'Independent model edit 1',
// 'issue #144041', 'issue #140195', moves to the wrapped line start, end, center and non-whitespace characters, the
// viewport moves and the folded line moves.

using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class CursorMoveOperationsTests
{
    private const string Line1 = "    \tMy First Line\t ";
    private const string Line2 = "\tMy Second Line";
    private const string Line3 = "    Third Line🐶";
    private const string Line4 = "";
    private const string Line5 = "1";

    private const string Text = Line1 + "\r\n" + Line2 + "\n" + Line3 + "\n" + Line4 + "\r\n" + Line5;

    private static void RunTest(Action<OperationsTestEditor> callback)
    {
        using var editor = OperationsTestEditor.Create(Text);
        callback(editor);
    }

    // CoreNavigationCommands.CursorLeft / CursorLeftSelect
    private static void MoveLeft(OperationsTestEditor editor, bool inSelectionMode = false, int noOfColumns = 1) =>
        editor.Move((config, model, cursor) => Cursor.MoveOperations.MoveLeft(config, model, cursor, inSelectionMode, noOfColumns));

    private static void MoveRight(OperationsTestEditor editor, bool inSelectionMode = false, int noOfColumns = 1) =>
        editor.Move((config, model, cursor) => Cursor.MoveOperations.MoveRight(config, model, cursor, inSelectionMode, noOfColumns));

    private static void MoveDown(OperationsTestEditor editor, bool inSelectionMode = false, int linesCount = 1) =>
        editor.Move((config, model, cursor) => Cursor.MoveOperations.MoveDown(config, model, cursor, inSelectionMode, linesCount));

    private static void MoveUp(OperationsTestEditor editor, bool inSelectionMode = false, int linesCount = 1) =>
        editor.Move((config, model, cursor) => Cursor.MoveOperations.MoveUp(config, model, cursor, inSelectionMode, linesCount));

    private static void MoveToBeginningOfLine(OperationsTestEditor editor, bool inSelectionMode = false) =>
        editor.Move((config, model, cursor) => Cursor.MoveOperations.MoveToBeginningOfLine(config, model, cursor, inSelectionMode));

    private static void MoveToEndOfLine(OperationsTestEditor editor, bool inSelectionMode = false) =>
        editor.Move((config, model, cursor) => Cursor.MoveOperations.MoveToEndOfLine(config, model, cursor, inSelectionMode, false));

    private static void MoveToBeginningOfBuffer(OperationsTestEditor editor, bool inSelectionMode = false) =>
        editor.Move((config, model, cursor) => Cursor.MoveOperations.MoveToBeginningOfBuffer(config, model, cursor, inSelectionMode));

    private static void MoveToEndOfBuffer(OperationsTestEditor editor, bool inSelectionMode = false) =>
        editor.Move((config, model, cursor) => Cursor.MoveOperations.MoveToEndOfBuffer(config, model, cursor, inSelectionMode));

    private static void MoveHalfLineLeft(OperationsTestEditor editor) =>
        editor.Move((config, model, cursor) =>
            Cursor.MoveOperations.MoveLeft(config, model, cursor, false, HalfLine(model.GetLineContent(cursor.Position.LineNumber))));

    private static void MoveHalfLineRight(OperationsTestEditor editor) =>
        editor.Move((config, model, cursor) =>
            Cursor.MoveOperations.MoveRight(config, model, cursor, false, HalfLine(model.GetLineContent(cursor.Position.LineNumber))));

    private static void MoveDownByBlankLine(OperationsTestEditor editor, bool inSelectionMode) =>
        editor.Move((config, model, cursor) => Cursor.MoveOperations.MoveToNextBlankLine(config, model, cursor, inSelectionMode));

    private static void MoveUpByBlankLine(OperationsTestEditor editor, bool inSelectionMode) =>
        editor.Move((config, model, cursor) => Cursor.MoveOperations.MoveToPrevBlankLine(config, model, cursor, inSelectionMode));

    // Math.round(length / 2)
    private static int HalfLine(string line) => (line.Length + 1) / 2;

    private static TextPosition P(int lineNumber, int column) => new(lineNumber, column);

    private static TextSelection S(int selectionStartLineNumber, int selectionStartColumn, int positionLineNumber, int positionColumn) =>
        new(selectionStartLineNumber, selectionStartColumn, positionLineNumber, positionColumn);

    private static void AssertCursor(OperationsTestEditor editor, TextSelection expected) => editor.AssertCursor(expected);

    private static void AssertCursor(OperationsTestEditor editor, TextPosition expected) => editor.AssertCursor(expected);

    // cursorMoveCommand.test.ts: cursorEqual
    private static void CursorEqual(OperationsTestEditor editor, int posLineNumber, int posColumn, int? selLineNumber = null, int? selColumn = null) =>
        editor.AssertCursor(S(selLineNumber ?? posLineNumber, selColumn ?? posColumn, posLineNumber, posColumn));

    // --------- move left

    [Fact]
    public void Move_Left_On_Top_Left_Position()
    {
        RunTest(editor =>
        {
            MoveLeft(editor);
            AssertCursor(editor, P(1, 1));
        });
    }

    [Theory]
    [InlineData("move left", 1, 3, false, 1, 2, 1, 2)]
    [InlineData("move left with surrogate pair", 3, 17, false, 3, 15, 3, 15)]
    [InlineData("move left goes to previous row", 2, 1, false, 1, 21, 1, 21)]
    [InlineData("move left selection", 2, 1, true, 2, 1, 1, 21)]
    public void Move_Left_Cases(string name, int lineNumber, int column, bool inSelectionMode, int expectedStartLine, int expectedStartColumn, int expectedLine, int expectedColumn)
    {
        _ = name;
        RunTest(editor =>
        {
            editor.MoveTo(lineNumber, column);
            AssertCursor(editor, P(lineNumber, column));
            MoveLeft(editor, inSelectionMode);
            AssertCursor(editor, S(expectedStartLine, expectedStartColumn, expectedLine, expectedColumn));
        });
    }

    // --------- move right

    [Theory]
    [InlineData("move right on bottom right position", 5, 2, false, 5, 2, 5, 2)]
    [InlineData("move right", 1, 3, false, 1, 4, 1, 4)]
    [InlineData("move right with surrogate pair", 3, 15, false, 3, 17, 3, 17)]
    [InlineData("move right goes to next row", 1, 21, false, 2, 1, 2, 1)]
    [InlineData("move right selection", 1, 21, true, 1, 21, 2, 1)]
    public void Move_Right_Cases(string name, int lineNumber, int column, bool inSelectionMode, int expectedStartLine, int expectedStartColumn, int expectedLine, int expectedColumn)
    {
        _ = name;
        RunTest(editor =>
        {
            editor.MoveTo(lineNumber, column);
            AssertCursor(editor, P(lineNumber, column));
            MoveRight(editor, inSelectionMode);
            AssertCursor(editor, S(expectedStartLine, expectedStartColumn, expectedLine, expectedColumn));
        });
    }

    // --------- move down

    [Fact]
    public void Move_Down()
    {
        RunTest(editor =>
        {
            MoveDown(editor);
            AssertCursor(editor, P(2, 1));
            MoveDown(editor);
            AssertCursor(editor, P(3, 1));
            MoveDown(editor);
            AssertCursor(editor, P(4, 1));
            MoveDown(editor);
            AssertCursor(editor, P(5, 1));
            MoveDown(editor);
            AssertCursor(editor, P(5, 2));
        });
    }

    [Fact]
    public void Move_Down_With_Selection()
    {
        RunTest(editor =>
        {
            MoveDown(editor, true);
            AssertCursor(editor, S(1, 1, 2, 1));
            MoveDown(editor, true);
            AssertCursor(editor, S(1, 1, 3, 1));
            MoveDown(editor, true);
            AssertCursor(editor, S(1, 1, 4, 1));
            MoveDown(editor, true);
            AssertCursor(editor, S(1, 1, 5, 1));
            MoveDown(editor, true);
            AssertCursor(editor, S(1, 1, 5, 2));
        });
    }

    [Fact]
    public void Move_Down_With_Tabs()
    {
        RunTest(editor =>
        {
            editor.MoveTo(1, 5);
            AssertCursor(editor, P(1, 5));
            MoveDown(editor);
            AssertCursor(editor, P(2, 2));
            MoveDown(editor);
            AssertCursor(editor, P(3, 5));
            MoveDown(editor);
            AssertCursor(editor, P(4, 1));
            MoveDown(editor);
            AssertCursor(editor, P(5, 2));
        });
    }

    // --------- move up

    [Fact]
    public void Move_Up()
    {
        RunTest(editor =>
        {
            editor.MoveTo(3, 5);
            AssertCursor(editor, P(3, 5));

            MoveUp(editor);
            AssertCursor(editor, P(2, 2));

            MoveUp(editor);
            AssertCursor(editor, P(1, 5));
        });
    }

    [Fact]
    public void Move_Up_With_Selection()
    {
        RunTest(editor =>
        {
            editor.MoveTo(3, 5);
            AssertCursor(editor, P(3, 5));

            MoveUp(editor, true);
            AssertCursor(editor, S(3, 5, 2, 2));

            MoveUp(editor, true);
            AssertCursor(editor, S(3, 5, 1, 5));
        });
    }

    [Fact]
    public void Move_Up_And_Down_With_Tabs()
    {
        RunTest(editor =>
        {
            editor.MoveTo(1, 5);
            AssertCursor(editor, P(1, 5));
            MoveDown(editor);
            MoveDown(editor);
            MoveDown(editor);
            MoveDown(editor);
            AssertCursor(editor, P(5, 2));
            MoveUp(editor);
            AssertCursor(editor, P(4, 1));
            MoveUp(editor);
            AssertCursor(editor, P(3, 5));
            MoveUp(editor);
            AssertCursor(editor, P(2, 2));
            MoveUp(editor);
            AssertCursor(editor, P(1, 5));
        });
    }

    [Fact]
    public void Move_Up_And_Down_With_End_Of_Lines_Starting_From_A_Long_One()
    {
        RunTest(editor =>
        {
            MoveToEndOfLine(editor);
            AssertCursor(editor, P(1, Line1.Length + 1));
            MoveToEndOfLine(editor);
            AssertCursor(editor, P(1, Line1.Length + 1));
            MoveDown(editor);
            AssertCursor(editor, P(2, Line2.Length + 1));
            MoveDown(editor);
            AssertCursor(editor, P(3, Line3.Length + 1));
            MoveDown(editor);
            AssertCursor(editor, P(4, Line4.Length + 1));
            MoveDown(editor);
            AssertCursor(editor, P(5, Line5.Length + 1));
            MoveUp(editor);
            MoveUp(editor);
            MoveUp(editor);
            MoveUp(editor);
            AssertCursor(editor, P(1, Line1.Length + 1));
        });
    }

    [Fact]
    public void Issue_44465_Cursor_Position_Not_Correct_When_Move()
    {
        RunTest(editor =>
        {
            editor.SetSelections([S(1, 5, 1, 5)]);
            // going once up on the first line remembers the offset visual columns
            MoveUp(editor);
            AssertCursor(editor, P(1, 1));
            MoveDown(editor);
            AssertCursor(editor, P(2, 2));
            MoveUp(editor);
            AssertCursor(editor, P(1, 5));

            // going twice up on the first line discards the offset visual columns
            MoveUp(editor);
            AssertCursor(editor, P(1, 1));
            MoveUp(editor);
            AssertCursor(editor, P(1, 1));
            MoveDown(editor);
            AssertCursor(editor, P(2, 1));
        });
    }

    // --------- move to beginning of line

    [Fact]
    public void Move_To_Beginning_Of_Line()
    {
        RunTest(editor =>
        {
            MoveToBeginningOfLine(editor);
            AssertCursor(editor, P(1, 6));
            MoveToBeginningOfLine(editor);
            AssertCursor(editor, P(1, 1));
        });
    }

    [Theory]
    [InlineData("move to beginning of line from within line", 8, false, 1, 6, 1, 6, 1, 1, 1, 1)]
    [InlineData("move to beginning of line from whitespace at beginning of line", 2, false, 1, 6, 1, 6, 1, 1, 1, 1)]
    [InlineData("move to beginning of line from within line selection", 8, true, 1, 8, 1, 6, 1, 8, 1, 1)]
    public void Move_To_Beginning_Of_Line_Cases(
        string name, int column, bool inSelectionMode,
        int firstStartLine, int firstStartColumn, int firstLine, int firstColumn,
        int secondStartLine, int secondStartColumn, int secondLine, int secondColumn)
    {
        _ = name;
        RunTest(editor =>
        {
            editor.MoveTo(1, column);
            MoveToBeginningOfLine(editor, inSelectionMode);
            AssertCursor(editor, S(firstStartLine, firstStartColumn, firstLine, firstColumn));
            MoveToBeginningOfLine(editor, inSelectionMode);
            AssertCursor(editor, S(secondStartLine, secondStartColumn, secondLine, secondColumn));
        });
    }

    [Theory]
    [InlineData("move to beginning of line with selection multiline forward", 1, 8, 3, 9, false, 3, 5, 3, 5)]
    [InlineData("move to beginning of line with selection multiline backward", 3, 9, 1, 8, false, 1, 6, 1, 6)]
    [InlineData("move to beginning of line with selection single line forward", 3, 2, 3, 9, false, 3, 5, 3, 5)]
    [InlineData("move to beginning of line with selection single line backward", 3, 9, 3, 2, false, 3, 5, 3, 5)]
    [InlineData("issue #15401: \"End\" key is behaving weird when text is selected part 1", 1, 8, 3, 9, false, 3, 5, 3, 5)]
    [InlineData("issue #17011: Shift+home/end now go to the end of the selection start's line, not the selection's end", 1, 8, 3, 9, true, 1, 8, 3, 5)]
    public void Move_To_Beginning_Of_Line_With_Selection_Cases(
        string name, int fromLineNumber, int fromColumn, int toLineNumber, int toColumn, bool inSelectionMode,
        int expectedStartLine, int expectedStartColumn, int expectedLine, int expectedColumn)
    {
        _ = name;
        RunTest(editor =>
        {
            editor.MoveTo(fromLineNumber, fromColumn);
            editor.MoveTo(toLineNumber, toColumn, true);
            MoveToBeginningOfLine(editor, inSelectionMode);
            AssertCursor(editor, S(expectedStartLine, expectedStartColumn, expectedLine, expectedColumn));
        });
    }

    // --------- move to end of line

    [Fact]
    public void Move_To_End_Of_Line()
    {
        RunTest(editor =>
        {
            MoveToEndOfLine(editor);
            AssertCursor(editor, P(1, Line1.Length + 1));
            MoveToEndOfLine(editor);
            AssertCursor(editor, P(1, Line1.Length + 1));
        });
    }

    [Theory]
    [InlineData("move to end of line from within line", 6, false, 1, 21)]
    [InlineData("move to end of line from whitespace at end of line", 20, false, 1, 21)]
    [InlineData("move to end of line from within line selection", 6, true, 1, 6)]
    public void Move_To_End_Of_Line_Cases(string name, int column, bool inSelectionMode, int expectedStartLine, int expectedStartColumn)
    {
        _ = name;
        var expected = S(expectedStartLine, expectedStartColumn, 1, Line1.Length + 1);
        RunTest(editor =>
        {
            editor.MoveTo(1, column);
            MoveToEndOfLine(editor, inSelectionMode);
            AssertCursor(editor, expected);
            MoveToEndOfLine(editor, inSelectionMode);
            AssertCursor(editor, expected);
        });
    }

    [Theory]
    [InlineData("move to end of line with selection multiline forward", 1, 1, 3, 9, 3, 17)]
    [InlineData("move to end of line with selection multiline backward", 3, 9, 1, 1, 1, 21)]
    [InlineData("move to end of line with selection single line forward", 3, 1, 3, 9, 3, 17)]
    [InlineData("move to end of line with selection single line backward", 3, 9, 3, 1, 3, 17)]
    [InlineData("issue #15401: \"End\" key is behaving weird when text is selected part 2", 1, 1, 3, 9, 3, 17)]
    public void Move_To_End_Of_Line_With_Selection_Cases(
        string name, int fromLineNumber, int fromColumn, int toLineNumber, int toColumn, int expectedLine, int expectedColumn)
    {
        _ = name;
        RunTest(editor =>
        {
            editor.MoveTo(fromLineNumber, fromColumn);
            editor.MoveTo(toLineNumber, toColumn, true);
            MoveToEndOfLine(editor, false);
            AssertCursor(editor, S(expectedLine, expectedColumn, expectedLine, expectedColumn));
        });
    }

    // --------- move to beginning of buffer

    [Fact]
    public void Move_To_Beginning_Of_Buffer()
    {
        RunTest(editor =>
        {
            MoveToBeginningOfBuffer(editor);
            AssertCursor(editor, P(1, 1));
        });
    }

    [Theory]
    [InlineData("move to beginning of buffer from within first line", 1, 3, false, 1, 1)]
    [InlineData("move to beginning of buffer from within another line", 3, 3, false, 1, 1)]
    [InlineData("move to beginning of buffer from within first line selection", 1, 3, true, 1, 3)]
    [InlineData("move to beginning of buffer from within another line selection", 3, 3, true, 3, 3)]
    public void Move_To_Beginning_Of_Buffer_Cases(string name, int lineNumber, int column, bool inSelectionMode, int expectedStartLine, int expectedStartColumn)
    {
        _ = name;
        RunTest(editor =>
        {
            editor.MoveTo(lineNumber, column);
            MoveToBeginningOfBuffer(editor, inSelectionMode);
            AssertCursor(editor, S(expectedStartLine, expectedStartColumn, 1, 1));
        });
    }

    // --------- move to end of buffer

    [Fact]
    public void Move_To_End_Of_Buffer()
    {
        RunTest(editor =>
        {
            MoveToEndOfBuffer(editor);
            AssertCursor(editor, P(5, Line5.Length + 1));
        });
    }

    [Theory]
    [InlineData("move to end of buffer from within last line", 5, 1, false, 5, 2)]
    [InlineData("move to end of buffer from within another line", 3, 3, false, 5, 2)]
    [InlineData("move to end of buffer from within last line selection", 5, 1, true, 5, 1)]
    [InlineData("move to end of buffer from within another line selection", 3, 3, true, 3, 3)]
    public void Move_To_End_Of_Buffer_Cases(string name, int lineNumber, int column, bool inSelectionMode, int expectedStartLine, int expectedStartColumn)
    {
        _ = name;
        RunTest(editor =>
        {
            editor.MoveTo(lineNumber, column);
            MoveToEndOfBuffer(editor, inSelectionMode);
            AssertCursor(editor, S(expectedStartLine, expectedStartColumn, 5, Line5.Length + 1));
        });
    }

    [Fact]
    public void Grapheme_Breaking()
    {
        using var editor = OperationsTestEditor.Create([
            "abcabc",
            "ãããããã",
            "辻\U000E0100辻\U000E0100辻\U000E0100",
            "பு",
        ]);

        editor.SetSelections([S(2, 1, 2, 1)]);
        MoveRight(editor);
        AssertCursor(editor, P(2, 3));
        MoveLeft(editor);
        AssertCursor(editor, P(2, 1));

        editor.SetSelections([S(3, 1, 3, 1)]);
        MoveRight(editor);
        AssertCursor(editor, P(3, 4));
        MoveLeft(editor);
        AssertCursor(editor, P(3, 1));

        editor.SetSelections([S(4, 1, 4, 1)]);
        MoveRight(editor);
        AssertCursor(editor, P(4, 3));
        MoveLeft(editor);
        AssertCursor(editor, P(4, 1));

        editor.SetSelections([S(1, 3, 1, 3)]);
        MoveDown(editor);
        AssertCursor(editor, P(2, 5));
        MoveDown(editor);
        AssertCursor(editor, P(3, 4));
        MoveUp(editor);
        AssertCursor(editor, P(2, 5));
        MoveUp(editor);
        AssertCursor(editor, P(1, 3));
    }

    // --------- cursorMoveCommand.test.ts: 'Cursor move command test'

    private static void ExecuteMoveCommandTest(Action<OperationsTestEditor> callback)
    {
        using var editor = OperationsTestEditor.Create([Line1, Line2, Line3, Line4, Line5]);
        callback(editor);
    }

    [Fact]
    public void Move_Left_Should_Move_To_Left_Character()
    {
        ExecuteMoveCommandTest(editor =>
        {
            editor.MoveTo(1, 8);
            MoveLeft(editor);
            CursorEqual(editor, 1, 7);
        });
    }

    [Fact]
    public void Move_Left_Should_Move_To_Left_By_N_Characters()
    {
        ExecuteMoveCommandTest(editor =>
        {
            editor.MoveTo(1, 8);
            MoveLeft(editor, noOfColumns: 3);
            CursorEqual(editor, 1, 5);
        });
    }

    [Fact]
    public void Move_Left_Should_Move_To_Left_By_Half_Line()
    {
        ExecuteMoveCommandTest(editor =>
        {
            editor.MoveTo(1, 8);
            MoveHalfLineLeft(editor);
            CursorEqual(editor, 1, 1);
        });
    }

    [Fact]
    public void Move_Left_Moves_To_Previous_Line()
    {
        ExecuteMoveCommandTest(editor =>
        {
            editor.MoveTo(2, 3);
            MoveLeft(editor, noOfColumns: 10);
            CursorEqual(editor, 1, 21);
        });
    }

    [Fact]
    public void Move_Right_Should_Move_To_Right_Character()
    {
        ExecuteMoveCommandTest(editor =>
        {
            editor.MoveTo(1, 5);
            MoveRight(editor);
            CursorEqual(editor, 1, 6);
        });
    }

    [Fact]
    public void Move_Right_Should_Move_To_Right_By_N_Characters()
    {
        ExecuteMoveCommandTest(editor =>
        {
            editor.MoveTo(1, 2);
            MoveRight(editor, noOfColumns: 6);
            CursorEqual(editor, 1, 8);
        });
    }

    [Fact]
    public void Move_Right_Should_Move_To_Right_By_Half_Line()
    {
        ExecuteMoveCommandTest(editor =>
        {
            editor.MoveTo(1, 4);
            MoveHalfLineRight(editor);
            CursorEqual(editor, 1, 14);
        });
    }

    [Fact]
    public void Move_Right_Moves_To_Next_Line()
    {
        ExecuteMoveCommandTest(editor =>
        {
            editor.MoveTo(1, 8);
            MoveRight(editor, noOfColumns: 100);
            CursorEqual(editor, 2, 1);
        });
    }

    [Fact]
    public void Move_Up_By_Cursor_Move_Command()
    {
        ExecuteMoveCommandTest(editor =>
        {
            editor.MoveTo(3, 5);
            CursorEqual(editor, 3, 5);

            MoveUp(editor, linesCount: 2);
            CursorEqual(editor, 1, 5);

            MoveUp(editor, linesCount: 1);
            CursorEqual(editor, 1, 1);
        });
    }

    [Fact]
    public void Move_Up_By_Model_Line_Cursor_Move_Command()
    {
        ExecuteMoveCommandTest(editor =>
        {
            editor.MoveTo(3, 5);
            CursorEqual(editor, 3, 5);

            MoveUp(editor, linesCount: 2);
            CursorEqual(editor, 1, 5);

            MoveUp(editor, linesCount: 1);
            CursorEqual(editor, 1, 1);
        });
    }

    [Fact]
    public void Move_Down_By_Model_Line_Cursor_Move_Command()
    {
        ExecuteMoveCommandTest(editor =>
        {
            editor.MoveTo(3, 5);
            CursorEqual(editor, 3, 5);

            MoveDown(editor, linesCount: 2);
            CursorEqual(editor, 5, 2);

            MoveDown(editor, linesCount: 1);
            CursorEqual(editor, 5, 2);
        });
    }

    [Fact]
    public void Move_Up_With_Selection_By_Cursor_Move_Command()
    {
        ExecuteMoveCommandTest(editor =>
        {
            editor.MoveTo(3, 5);
            CursorEqual(editor, 3, 5);

            MoveUp(editor, true, 1);
            CursorEqual(editor, 2, 2, 3, 5);

            MoveUp(editor, true, 1);
            CursorEqual(editor, 1, 5, 3, 5);
        });
    }

    [Fact]
    public void Move_Up_And_Down_With_Tabs_By_Cursor_Move_Command()
    {
        ExecuteMoveCommandTest(editor =>
        {
            editor.MoveTo(1, 5);
            CursorEqual(editor, 1, 5);

            MoveDown(editor, linesCount: 4);
            CursorEqual(editor, 5, 2);

            MoveUp(editor, linesCount: 1);
            CursorEqual(editor, 4, 1);

            MoveUp(editor, linesCount: 1);
            CursorEqual(editor, 3, 5);

            MoveUp(editor, linesCount: 1);
            CursorEqual(editor, 2, 2);

            MoveUp(editor, linesCount: 1);
            CursorEqual(editor, 1, 5);
        });
    }

    [Fact]
    public void Move_Up_And_Down_With_End_Of_Lines_Starting_From_A_Long_One_By_Cursor_Move_Command()
    {
        ExecuteMoveCommandTest(editor =>
        {
            MoveToEndOfLine(editor);
            CursorEqual(editor, 1, 21);

            MoveToEndOfLine(editor);
            CursorEqual(editor, 1, 21);

            MoveDown(editor, linesCount: 2);
            CursorEqual(editor, 3, 17);

            MoveDown(editor, linesCount: 1);
            CursorEqual(editor, 4, 1);

            MoveDown(editor, linesCount: 1);
            CursorEqual(editor, 5, 2);

            MoveUp(editor, linesCount: 4);
            CursorEqual(editor, 1, 21);
        });
    }

    // --------- cursorMoveCommand.test.ts: 'Cursor move by blankline test'

    private static void ExecuteBlankLineTest(Action<OperationsTestEditor> callback)
    {
        using var editor = OperationsTestEditor.Create([
            Line1,
            Line2,
            Line3,
            "",
            "1",
            "2",
            "3",
            "",
            "         ",
            "a",
            "b",
        ]);
        callback(editor);
    }

    [Fact]
    public void Move_Down_Should_Move_To_Start_Of_Next_Blank_Line()
    {
        ExecuteBlankLineTest(editor =>
        {
            MoveDownByBlankLine(editor, false);
            CursorEqual(editor, 4, 1);
        });
    }

    [Fact]
    public void Move_Up_Should_Move_To_Start_Of_Previous_Blank_Line()
    {
        ExecuteBlankLineTest(editor =>
        {
            editor.MoveTo(7, 1);
            MoveUpByBlankLine(editor, false);
            CursorEqual(editor, 4, 1);
        });
    }

    [Fact]
    public void Move_Down_Should_Skip_Over_Whitespace_If_Already_On_Blank_Line()
    {
        ExecuteBlankLineTest(editor =>
        {
            editor.MoveTo(8, 1);
            MoveDownByBlankLine(editor, false);
            CursorEqual(editor, 11, 1);
        });
    }

    [Fact]
    public void Move_Up_Should_Skip_Over_Whitespace_If_Already_On_Blank_Line()
    {
        ExecuteBlankLineTest(editor =>
        {
            editor.MoveTo(9, 1);
            MoveUpByBlankLine(editor, false);
            CursorEqual(editor, 4, 1);
        });
    }

    [Fact]
    public void Move_Up_Should_Go_To_First_Column_Of_First_Line_If_Not_Empty()
    {
        ExecuteBlankLineTest(editor =>
        {
            editor.MoveTo(2, 1);
            MoveUpByBlankLine(editor, false);
            CursorEqual(editor, 1, 1);
        });
    }

    [Fact]
    public void Move_Down_Should_Go_To_First_Column_Of_Last_Line_If_Not_Empty()
    {
        ExecuteBlankLineTest(editor =>
        {
            editor.MoveTo(10, 1);
            MoveDownByBlankLine(editor, false);
            CursorEqual(editor, 11, 1);
        });
    }

    [Fact]
    public void Select_Down_Should_Select_To_Start_Of_Next_Blank_Line()
    {
        ExecuteBlankLineTest(editor =>
        {
            MoveDownByBlankLine(editor, true);
            CursorEqual(editor, 4, 1, 1, 1);
        });
    }

    [Fact]
    public void Select_Up_Should_Select_To_Start_Of_Previous_Blank_Line()
    {
        ExecuteBlankLineTest(editor =>
        {
            editor.MoveTo(7, 1);
            MoveUpByBlankLine(editor, true);
            CursorEqual(editor, 4, 1, 7, 1);
        });
    }
}
