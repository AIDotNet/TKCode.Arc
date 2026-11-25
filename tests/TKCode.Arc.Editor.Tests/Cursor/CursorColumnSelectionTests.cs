// Tests for ColumnSelection, ported from vscode (src/vs/editor/test/browser/controller/cursor.test.ts, suite
// 'Editor Controller - Cursor', commit b57d5c9e), MIT license: 'column select 1', 'issue #4905', 'issue #20087' (mouse
// and keyboard), 'issue #118062' and 'column select with keyboard'. Upstream runs the ColumnSelect and
// CursorColumnSelectLeft/Right/Up/Down commands on a test code editor; here they are driven through the operations on
// a text model (OperationsTestEditor), the way the commands call them (coreCommands.ts ColumnSelectCommand): the
// previous column selection comes from the editor (or from the primary cursor), and the result replaces the cursors
// and is remembered.

using TKCode.Arc.Editor.Cursor;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class CursorColumnSelectionTests
{
    private static TextSelection S(int selectionStartLineNumber, int selectionStartColumn, int positionLineNumber, int positionColumn) =>
        new(selectionStartLineNumber, selectionStartColumn, positionLineNumber, positionColumn);

    // CoreNavigationCommands.ColumnSelect
    private static void ColumnSelect(OperationsTestEditor editor, TextPosition position, int mouseColumn, bool doColumnSelect)
    {
        var prevColumnSelectData = editor.GetCursorColumnSelectData();
        var validatedViewPosition = editor.Model.ValidatePosition(position);
        var fromViewLineNumber = doColumnSelect ? prevColumnSelectData.FromViewLineNumber : validatedViewPosition.LineNumber;
        var fromViewVisualColumn = doColumnSelect ? prevColumnSelectData.FromViewVisualColumn : mouseColumn - 1;
        editor.ApplyColumnSelect(ColumnSelection.ColumnSelect(
            editor.Config, editor.Model, fromViewLineNumber, fromViewVisualColumn, validatedViewPosition.LineNumber, mouseColumn - 1));
    }

    private static void ColumnSelectLeft(OperationsTestEditor editor) =>
        editor.ApplyColumnSelect(ColumnSelection.ColumnSelectLeft(editor.Config, editor.Model, editor.GetCursorColumnSelectData()));

    private static void ColumnSelectRight(OperationsTestEditor editor, int times = 1)
    {
        for (var i = 0; i < times; i++)
        {
            editor.ApplyColumnSelect(ColumnSelection.ColumnSelectRight(editor.Config, editor.Model, editor.GetCursorColumnSelectData()));
        }
    }

    private static void ColumnSelectUp(OperationsTestEditor editor) =>
        editor.ApplyColumnSelect(ColumnSelection.ColumnSelectUp(editor.Config, editor.Model, editor.GetCursorColumnSelectData(), false));

    private static void ColumnSelectDown(OperationsTestEditor editor, int times = 1)
    {
        for (var i = 0; i < times; i++)
        {
            editor.ApplyColumnSelect(ColumnSelection.ColumnSelectDown(editor.Config, editor.Model, editor.GetCursorColumnSelectData(), false));
        }
    }

    private static readonly string[] s_gulpLines =
    [
        "var gulp = require(\"gulp\");",
        "var path = require(\"path\");",
        "var rimraf = require(\"rimraf\");",
        "var isarray = require(\"isarray\");",
        "var merge = require(\"merge-stream\");",
        "var concat = require(\"gulp-concat\");",
        "var newer = require(\"gulp-newer\");",
    ];

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
    public void Column_Select_1()
    {
        using var editor = OperationsTestEditor.Create([
            "\tprivate compute(a:number): boolean {",
            "\t\tif (a + 3 === 0 || a + 5 === 0) {",
            "\t\t\treturn false;",
            "\t\t}",
            "\t}",
        ]);

        editor.MoveTo(1, 7, false);
        editor.AssertCursor(new TextPosition(1, 7));

        ColumnSelect(editor, new TextPosition(4, 4), 15, true);

        editor.AssertCursor(
            S(1, 7, 1, 12),
            S(2, 4, 2, 9),
            S(3, 3, 3, 6),
            S(4, 4, 4, 4));
    }

    [Fact]
    public void Issue_4905_Column_Select_Is_Biased_To_The_Right()
    {
        using var editor = OperationsTestEditor.Create(s_gulpLines);

        editor.MoveTo(1, 4, false);
        editor.AssertCursor(new TextPosition(1, 4));

        ColumnSelect(editor, new TextPosition(4, 1), 1, true);

        editor.AssertCursor(
            S(1, 4, 1, 1),
            S(2, 4, 2, 1),
            S(3, 4, 3, 1),
            S(4, 4, 4, 1));
    }

    [Fact]
    public void Issue_20087_Column_Select_With_Mouse()
    {
        using var editor = OperationsTestEditor.Create(s_propertyLines);

        editor.MoveTo(10, 10, false);
        editor.AssertCursor(new TextPosition(10, 10));

        TextSelection[] expected =
        [
            S(10, 10, 10, 1),
            S(9, 10, 9, 1),
            S(8, 10, 8, 1),
            S(7, 10, 7, 1),
            S(6, 10, 6, 1),
            S(5, 10, 5, 1),
            S(4, 10, 4, 1),
            S(3, 10, 3, 1),
            S(2, 10, 2, 1),
            S(1, 10, 1, 1),
        ];

        ColumnSelect(editor, new TextPosition(1, 1), 1, true);
        editor.AssertCursor(expected);

        ColumnSelect(editor, new TextPosition(1, 1), 1, true);
        editor.AssertCursor(expected);
    }

    [Fact]
    public void Issue_20087_Column_Select_With_Keyboard()
    {
        using var editor = OperationsTestEditor.Create(s_propertyLines);

        editor.MoveTo(10, 10, false);
        editor.AssertCursor(new TextPosition(10, 10));

        ColumnSelectLeft(editor);
        editor.AssertCursor(S(10, 10, 10, 9));

        ColumnSelectLeft(editor);
        editor.AssertCursor(S(10, 10, 10, 8));

        ColumnSelectRight(editor);
        editor.AssertCursor(S(10, 10, 10, 9));

        ColumnSelectUp(editor);
        editor.AssertCursor(S(10, 10, 10, 9), S(9, 10, 9, 9));

        ColumnSelectDown(editor);
        editor.AssertCursor(S(10, 10, 10, 9));
    }

    [Fact]
    public void Issue_118062_Column_Selection_Cannot_Select_First_Position_Of_A_Line()
    {
        using var editor = OperationsTestEditor.Create(["hello world"]);

        editor.MoveTo(1, 2, false);
        editor.AssertCursor(new TextPosition(1, 2));

        ColumnSelectLeft(editor);
        editor.AssertCursor(S(1, 2, 1, 1));
    }

    [Fact]
    public void Column_Select_With_Keyboard()
    {
        using var editor = OperationsTestEditor.Create(s_gulpLines);

        editor.MoveTo(1, 4, false);
        editor.AssertCursor(new TextPosition(1, 4));

        ColumnSelectRight(editor);
        editor.AssertCursor(S(1, 4, 1, 5));

        ColumnSelectDown(editor);
        editor.AssertCursor(S(1, 4, 1, 5), S(2, 4, 2, 5));

        ColumnSelectDown(editor);
        editor.AssertCursor(S(1, 4, 1, 5), S(2, 4, 2, 5), S(3, 4, 3, 5));

        ColumnSelectDown(editor, 4);
        editor.AssertCursor(
            S(1, 4, 1, 5), S(2, 4, 2, 5), S(3, 4, 3, 5), S(4, 4, 4, 5), S(5, 4, 5, 5), S(6, 4, 6, 5), S(7, 4, 7, 5));

        ColumnSelectRight(editor);
        editor.AssertCursor(
            S(1, 4, 1, 6), S(2, 4, 2, 6), S(3, 4, 3, 6), S(4, 4, 4, 6), S(5, 4, 5, 6), S(6, 4, 6, 6), S(7, 4, 7, 6));

        // 10 times
        ColumnSelectRight(editor, 10);
        editor.AssertCursor(
            S(1, 4, 1, 16), S(2, 4, 2, 16), S(3, 4, 3, 16), S(4, 4, 4, 16), S(5, 4, 5, 16), S(6, 4, 6, 16), S(7, 4, 7, 16));

        // 10 times
        ColumnSelectRight(editor, 10);
        editor.AssertCursor(
            S(1, 4, 1, 26), S(2, 4, 2, 26), S(3, 4, 3, 26), S(4, 4, 4, 26), S(5, 4, 5, 26), S(6, 4, 6, 26), S(7, 4, 7, 26));

        // 2 times => reaching the ending of lines 1 and 2
        ColumnSelectRight(editor, 2);
        editor.AssertCursor(
            S(1, 4, 1, 28), S(2, 4, 2, 28), S(3, 4, 3, 28), S(4, 4, 4, 28), S(5, 4, 5, 28), S(6, 4, 6, 28), S(7, 4, 7, 28));

        // 4 times => reaching the ending of line 3
        ColumnSelectRight(editor, 4);
        editor.AssertCursor(
            S(1, 4, 1, 28), S(2, 4, 2, 28), S(3, 4, 3, 32), S(4, 4, 4, 32), S(5, 4, 5, 32), S(6, 4, 6, 32), S(7, 4, 7, 32));

        // 2 times => reaching the ending of line 4
        ColumnSelectRight(editor, 2);
        editor.AssertCursor(
            S(1, 4, 1, 28), S(2, 4, 2, 28), S(3, 4, 3, 32), S(4, 4, 4, 34), S(5, 4, 5, 34), S(6, 4, 6, 34), S(7, 4, 7, 34));

        // 1 time => reaching the ending of line 7
        ColumnSelectRight(editor);
        editor.AssertCursor(
            S(1, 4, 1, 28), S(2, 4, 2, 28), S(3, 4, 3, 32), S(4, 4, 4, 34), S(5, 4, 5, 35), S(6, 4, 6, 35), S(7, 4, 7, 35));

        // 3 times => reaching the ending of lines 5 & 6
        ColumnSelectRight(editor, 3);
        editor.AssertCursor(
            S(1, 4, 1, 28), S(2, 4, 2, 28), S(3, 4, 3, 32), S(4, 4, 4, 34), S(5, 4, 5, 37), S(6, 4, 6, 37), S(7, 4, 7, 35));

        // cannot go anywhere anymore
        ColumnSelectRight(editor);
        editor.AssertCursor(
            S(1, 4, 1, 28), S(2, 4, 2, 28), S(3, 4, 3, 32), S(4, 4, 4, 34), S(5, 4, 5, 37), S(6, 4, 6, 37), S(7, 4, 7, 35));

        // cannot go anywhere anymore even if we insist
        ColumnSelectRight(editor, 4);
        editor.AssertCursor(
            S(1, 4, 1, 28), S(2, 4, 2, 28), S(3, 4, 3, 32), S(4, 4, 4, 34), S(5, 4, 5, 37), S(6, 4, 6, 37), S(7, 4, 7, 35));

        // can easily go back
        ColumnSelectLeft(editor);
        editor.AssertCursor(
            S(1, 4, 1, 28), S(2, 4, 2, 28), S(3, 4, 3, 32), S(4, 4, 4, 34), S(5, 4, 5, 36), S(6, 4, 6, 36), S(7, 4, 7, 35));
    }
}
