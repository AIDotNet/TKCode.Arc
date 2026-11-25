// Tests for WordOperations.Word (word selection), ported from vscode
// (src/vs/editor/test/browser/controller/cursor.test.ts, suite 'Editor Controller', commit b57d5c9e), MIT license:
// 'issue #832: word right', 'issue #33788', 'issue #12887', 'Double-click on punctuation ...', 'issue #7100' and
// 'issue #112039'. Upstream runs the WordSelect, WordSelectDrag and MoveToSelect commands on a test code editor; here
// they are driven through the operation on a text model (OperationsTestEditor), the way CursorMoveCommands.word and
// moveTo call it: WordSelect is Word(inSelectionMode: false), WordSelectDrag Word(inSelectionMode: true), and
// MoveToSelect on a word selection continues it with Word(inSelectionMode: true).

using Shouldly;
using TKCode.Arc.Editor.Cursor;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class WordSelectionTests
{
    private static void Word(OperationsTestEditor editor, TextPosition position, bool inSelectionMode)
    {
        var validated = editor.Model.ValidatePosition(position);
        editor.Move((config, model, cursor) => WordOperations.Word(config, model, cursor, inSelectionMode, validated));
    }

    private static void WordSelect(OperationsTestEditor editor, TextPosition position) => Word(editor, position, false);

    private static void WordSelectDrag(OperationsTestEditor editor, TextPosition position) => Word(editor, position, true);

    [Fact]
    public void Issue_832_Word_Right()
    {
        using var editor = OperationsTestEditor.Create(["   /* Just some   more   text a+= 3 +5-3 + 7 */  "]);
        editor.MoveTo(1, 1, false);

        void AssertWordRight(int col, int expectedCol)
        {
            if (col == 1)
            {
                WordSelect(editor, new TextPosition(1, col));
            }
            else
            {
                WordSelectDrag(editor, new TextPosition(1, col));
            }

            editor.GetSelection().StartColumn.ShouldBe(1, "TEST FOR " + col);
            editor.GetSelection().EndColumn.ShouldBe(expectedCol, "TEST FOR " + col);
        }

        AssertWordRight(1, "   ".Length + 1);
        AssertWordRight(2, "   ".Length + 1);
        AssertWordRight(3, "   ".Length + 1);
        AssertWordRight(4, "   ".Length + 1);
        AssertWordRight(5, "   /".Length + 1);
        AssertWordRight(6, "   /*".Length + 1);
        AssertWordRight(7, "   /* ".Length + 1);
        AssertWordRight(8, "   /* Just".Length + 1);
        AssertWordRight(9, "   /* Just".Length + 1);
        AssertWordRight(10, "   /* Just".Length + 1);
        AssertWordRight(11, "   /* Just".Length + 1);
        AssertWordRight(12, "   /* Just ".Length + 1);
        AssertWordRight(13, "   /* Just some".Length + 1);
        AssertWordRight(14, "   /* Just some".Length + 1);
        AssertWordRight(15, "   /* Just some".Length + 1);
        AssertWordRight(16, "   /* Just some".Length + 1);
        AssertWordRight(17, "   /* Just some ".Length + 1);
        AssertWordRight(18, "   /* Just some  ".Length + 1);
        AssertWordRight(19, "   /* Just some   ".Length + 1);
        AssertWordRight(20, "   /* Just some   more".Length + 1);
        AssertWordRight(21, "   /* Just some   more".Length + 1);
        AssertWordRight(22, "   /* Just some   more".Length + 1);
        AssertWordRight(23, "   /* Just some   more".Length + 1);
        AssertWordRight(24, "   /* Just some   more ".Length + 1);
        AssertWordRight(25, "   /* Just some   more  ".Length + 1);
        AssertWordRight(26, "   /* Just some   more   ".Length + 1);
        AssertWordRight(27, "   /* Just some   more   text".Length + 1);
        AssertWordRight(28, "   /* Just some   more   text".Length + 1);
        AssertWordRight(29, "   /* Just some   more   text".Length + 1);
        AssertWordRight(30, "   /* Just some   more   text".Length + 1);
        AssertWordRight(31, "   /* Just some   more   text ".Length + 1);
        AssertWordRight(32, "   /* Just some   more   text a".Length + 1);
        AssertWordRight(33, "   /* Just some   more   text a+".Length + 1);
        AssertWordRight(34, "   /* Just some   more   text a+=".Length + 1);
        AssertWordRight(35, "   /* Just some   more   text a+= ".Length + 1);
        AssertWordRight(36, "   /* Just some   more   text a+= 3".Length + 1);
        AssertWordRight(37, "   /* Just some   more   text a+= 3 ".Length + 1);
        AssertWordRight(38, "   /* Just some   more   text a+= 3 +".Length + 1);
        AssertWordRight(39, "   /* Just some   more   text a+= 3 +5".Length + 1);
        AssertWordRight(40, "   /* Just some   more   text a+= 3 +5-".Length + 1);
        AssertWordRight(41, "   /* Just some   more   text a+= 3 +5-3".Length + 1);
        AssertWordRight(42, "   /* Just some   more   text a+= 3 +5-3 ".Length + 1);
        AssertWordRight(43, "   /* Just some   more   text a+= 3 +5-3 +".Length + 1);
        AssertWordRight(44, "   /* Just some   more   text a+= 3 +5-3 + ".Length + 1);
        AssertWordRight(45, "   /* Just some   more   text a+= 3 +5-3 + 7".Length + 1);
        AssertWordRight(46, "   /* Just some   more   text a+= 3 +5-3 + 7 ".Length + 1);
        AssertWordRight(47, "   /* Just some   more   text a+= 3 +5-3 + 7 *".Length + 1);
        AssertWordRight(48, "   /* Just some   more   text a+= 3 +5-3 + 7 */".Length + 1);
        AssertWordRight(49, "   /* Just some   more   text a+= 3 +5-3 + 7 */ ".Length + 1);
        AssertWordRight(50, "   /* Just some   more   text a+= 3 +5-3 + 7 */  ".Length + 1);
    }

    [Fact]
    public void Issue_33788_Wrong_Cursor_Position_When_Double_Click_To_Select_A_Word()
    {
        using var editor = OperationsTestEditor.Create(["Just some text"]);

        WordSelect(editor, new TextPosition(1, 8));
        editor.GetSelection().ShouldBe(new TextSelection(1, 6, 1, 10));

        WordSelectDrag(editor, new TextPosition(1, 8));
        editor.GetSelection().ShouldBe(new TextSelection(1, 6, 1, 10));
    }

    [Fact]
    public void Issue_12887_Double_Click_Highlighting_Separating_White_Space()
    {
        using var editor = OperationsTestEditor.Create(["abc def"]);

        WordSelect(editor, new TextPosition(1, 5));
        editor.GetSelection().ShouldBe(new TextSelection(1, 5, 1, 8));
    }

    [Fact]
    public void Double_Click_On_Punctuation_Should_Select_The_Character_Not_Adjacent_Space()
    {
        using var editor = OperationsTestEditor.Create(["// a b c 1 2 3 ~ ! @ # $ % ^ & * ( ) _ + \\ /"]);

        // Test double-click on '@' at position 20
        WordSelect(editor, new TextPosition(1, 20));
        editor.GetSelection().ShouldBe(new TextSelection(1, 20, 1, 21), "Should select @ character");

        // Test double-click on '#' at position 22
        WordSelect(editor, new TextPosition(1, 22));
        editor.GetSelection().ShouldBe(new TextSelection(1, 22, 1, 23), "Should select # character");

        // Test double-click on '!' at position 18
        WordSelect(editor, new TextPosition(1, 18));
        editor.GetSelection().ShouldBe(new TextSelection(1, 18, 1, 19), "Should select ! character");

        // Test double-click on first '/' in '//' at position 1
        WordSelect(editor, new TextPosition(1, 1));
        editor.GetSelection().ShouldBe(new TextSelection(1, 1, 1, 3), "Should select // token");

        // Test double-click on second '/' in '//' at position 2
        WordSelect(editor, new TextPosition(1, 2));
        editor.GetSelection().ShouldBe(new TextSelection(1, 1, 1, 3), "Should select // token");

        // Test double-click on '\' at position 42
        WordSelect(editor, new TextPosition(1, 42));
        editor.GetSelection().ShouldBe(new TextSelection(1, 42, 1, 43), "Should select \\ character");
    }

    [Fact]
    public void Issue_7100_Mouse_Word_Selection_Is_Strange_When_Non_Word_Character_Is_At_The_End_Of_Line()
    {
        using var editor = OperationsTestEditor.Create([
            "before.a",
            "before",
            "hello:",
            "there:",
            "this is strange:",
            "here",
            "it",
            "is",
        ]);

        WordSelect(editor, new TextPosition(3, 7));
        editor.AssertCursor(new TextSelection(3, 7, 3, 7));

        WordSelectDrag(editor, new TextPosition(4, 7));
        editor.AssertCursor(new TextSelection(3, 7, 4, 7));
    }

    [Fact]
    public void Issue_112039_Shift_Continuing_A_Double_Triple_Click_And_Drag_Selection_Does_Not_Remember_Its_Starting_Mode()
    {
        using var editor = OperationsTestEditor.Create([
            "just some text",
            "and another line",
            "and another one",
        ]);

        WordSelect(editor, new TextPosition(2, 6));
        // MoveToSelect on a word selection: CursorMoveCommands.moveTo continues it with CursorMoveCommands.word
        editor.Cursors[0].SelectionStartKind.ShouldBe(SelectionStartKind.Word);
        WordSelectDrag(editor, new TextPosition(1, 8));
        editor.AssertCursor(new TextSelection(2, 12, 1, 6));
    }
}
