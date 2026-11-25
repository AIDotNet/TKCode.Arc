// Tests for the undo stops of the typing operations, ported from vscode
// (src/vs/editor/test/browser/controller/cursor.test.ts, suites 'Undo stops' and 'Overtype Mode', commit b57d5c9e),
// MIT license. Upstream types through the view model of a test code editor; here the typing goes through
// TypeOperations on OperationsTestEditor, which does what the cursors controller does with the results.
//
// Not ported here: 'there is an undo stop between deleting left and deleting right' and 'there is an undo stop between
// deleting right and deleting left' (they only delete; in CursorDeleteOperationsTests).
// Adapted: 'Overtype Mode' sets the editor's input mode (upstream sets the process-wide InputMode in setup).

using Shouldly;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TypeOperationsUndoStopsTests
{
    private static TextSelection S(int selectionStartLineNumber, int selectionStartColumn, int positionLineNumber, int positionColumn) =>
        new(selectionStartLineNumber, selectionStartColumn, positionLineNumber, positionColumn);

    private const string Keyboard = OperationsTestEditor.Keyboard;

    public static TheoryData<string, bool, string, int> TypingAndDeletingCases => new()
    {
        { "there is an undo stop between typing and deleting left", true, "A fir line", 6 },
        { "there is an undo stop between typing and deleting right", false, "A firstine", 8 },
    };

    [Theory]
    [MemberData(nameof(TypingAndDeletingCases))]
    public async Task There_Is_An_Undo_Stop_Between_Typing_And_Deleting(string name, bool deleteLeft, string expectedLine, int expectedColumn)
    {
        _ = name;
        using var editor = OperationsTestEditor.Create(string.Join('\n', "A  line", "Another line"));
        var model = editor.Model;

        editor.SetSelectionsValidated(S(1, 3, 1, 3));
        editor.Type("first", Keyboard);
        model.GetLineContent(1).ShouldBe("A first line");
        editor.AssertCursor(S(1, 8, 1, 8));

        for (var i = 0; i < 2; i++)
        {
            if (deleteLeft)
            {
                editor.DeleteLeft();
            }
            else
            {
                editor.DeleteRight();
            }
        }
        model.GetLineContent(1).ShouldBe(expectedLine);
        editor.AssertCursor(S(1, expectedColumn, 1, expectedColumn));

        await editor.Undo();
        model.GetLineContent(1).ShouldBe("A first line");
        editor.AssertCursor(S(1, 8, 1, 8));

        await editor.Undo();
        model.GetLineContent(1).ShouldBe("A  line");
        editor.AssertCursor(S(1, 3, 1, 3));
    }

    [Fact]
    public async Task There_Is_An_Undo_Stop_Between_Deleting_Left_And_Typing()
    {
        using var editor = OperationsTestEditor.Create(string.Join('\n', "A  line", "Another line"));
        var model = editor.Model;

        editor.SetSelectionsValidated(S(2, 8, 2, 8));
        for (var i = 0; i < 7; i++)
        {
            editor.DeleteLeft();
        }
        model.GetLineContent(2).ShouldBe(" line");
        editor.AssertCursor(S(2, 1, 2, 1));

        editor.Type("Second", Keyboard);
        model.GetLineContent(2).ShouldBe("Second line");
        editor.AssertCursor(S(2, 7, 2, 7));

        await editor.Undo();
        model.GetLineContent(2).ShouldBe(" line");
        editor.AssertCursor(S(2, 1, 2, 1));

        await editor.Undo();
        model.GetLineContent(2).ShouldBe("Another line");
        editor.AssertCursor(S(2, 8, 2, 8));
    }

    [Fact]
    public async Task There_Is_An_Undo_Stop_Between_Deleting_Right_And_Typing()
    {
        using var editor = OperationsTestEditor.Create(string.Join('\n', "A  line", "Another line"));
        var model = editor.Model;

        editor.SetSelectionsValidated(S(2, 9, 2, 9));
        for (var i = 0; i < 4; i++)
        {
            editor.DeleteRight();
        }
        model.GetLineContent(2).ShouldBe("Another ");
        editor.AssertCursor(S(2, 9, 2, 9));

        editor.Type("text", Keyboard);
        model.GetLineContent(2).ShouldBe("Another text");
        editor.AssertCursor(S(2, 13, 2, 13));

        await editor.Undo();
        model.GetLineContent(2).ShouldBe("Another ");
        editor.AssertCursor(S(2, 9, 2, 9));

        await editor.Undo();
        model.GetLineContent(2).ShouldBe("Another line");
        editor.AssertCursor(S(2, 9, 2, 9));
    }

    [Fact]
    public async Task Inserts_Undo_Stop_When_Typing_Space()
    {
        using var editor = OperationsTestEditor.Create(string.Join('\n', "A  line", "Another line"));
        var model = editor.Model;

        editor.SetSelectionsValidated(S(1, 3, 1, 3));
        editor.Type("first and interesting", Keyboard);
        model.GetLineContent(1).ShouldBe("A first and interesting line");
        editor.AssertCursor(S(1, 24, 1, 24));

        await editor.Undo();
        model.GetLineContent(1).ShouldBe("A first and line");
        editor.AssertCursor(S(1, 12, 1, 12));

        await editor.Undo();
        model.GetLineContent(1).ShouldBe("A first line");
        editor.AssertCursor(S(1, 8, 1, 8));

        await editor.Undo();
        model.GetLineContent(1).ShouldBe("A  line");
        editor.AssertCursor(S(1, 3, 1, 3));
    }

    [Fact]
    public async Task Can_Undo_Typing_And_EOL_Change_In_One_Undo_Stop()
    {
        using var editor = OperationsTestEditor.Create(string.Join('\n', "A  line", "Another line"));
        var model = editor.Model;

        editor.SetSelectionsValidated(S(1, 3, 1, 3));
        editor.Type("first", Keyboard);
        model.GetValue().ShouldBe("A first line\nAnother line");
        editor.AssertCursor(S(1, 8, 1, 8));

        model.PushEOL(EndOfLineSequence.CRLF);
        model.GetValue().ShouldBe("A first line\r\nAnother line");
        editor.AssertCursor(S(1, 8, 1, 8));

        await editor.Undo();
        model.GetValue().ShouldBe("A  line\nAnother line");
        editor.AssertCursor(S(1, 3, 1, 3));
    }

    [Fact]
    public async Task Issue_93585_Undo_Multi_Cursor_Edit_Corrupts_Document()
    {
        using var editor = OperationsTestEditor.Create(string.Join('\n', "hello world", "hello world"));
        var model = editor.Model;

        editor.SetSelectionsValidated(
            S(2, 7, 2, 12),
            S(1, 7, 1, 12));
        editor.Type("no", Keyboard);
        model.GetValue().ShouldBe("hello no\nhello no");

        await editor.Undo();
        model.GetValue().ShouldBe("hello world\nhello world");
    }

    [Fact]
    public async Task There_Is_A_Single_Undo_Stop_For_Consecutive_Whitespaces()
    {
        using var editor = OperationsTestEditor.Create("", modelOptions: TypingTestContext.ModelOpts(insertSpaces: false));
        var model = editor.Model;

        editor.Type("a", Keyboard);
        editor.Type("b", Keyboard);
        editor.Type(" ", Keyboard);
        editor.Type(" ", Keyboard);
        editor.Type("c", Keyboard);
        editor.Type("d", Keyboard);

        model.GetValue(EndOfLinePreference.LF).ShouldBe("ab  cd", "assert1");

        await editor.Undo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("ab  ", "assert2");

        await editor.Undo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("ab", "assert3");

        await editor.Undo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("", "assert4");
    }

    [Fact]
    public async Task There_Is_No_Undo_Stop_After_A_Single_Whitespace()
    {
        using var editor = OperationsTestEditor.Create("", modelOptions: TypingTestContext.ModelOpts(insertSpaces: false));
        var model = editor.Model;

        editor.Type("a", Keyboard);
        editor.Type("b", Keyboard);
        editor.Type(" ", Keyboard);
        editor.Type("c", Keyboard);
        editor.Type("d", Keyboard);

        model.GetValue(EndOfLinePreference.LF).ShouldBe("ab cd", "assert1");

        await editor.Undo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("ab", "assert3");

        await editor.Undo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("", "assert4");
    }

    // ------ suite 'Overtype Mode'

    private static OperationsTestEditor CreateOvertypeEditor()
    {
        var editor = OperationsTestEditor.Create(
            string.Join('\n', "123456789", "123456789"),
            modelOptions: TypingTestContext.ModelOpts(insertSpaces: false));
        editor.InputMode = EditorInputMode.Overtype;
        return editor;
    }

    [Fact]
    public void Overtype_Mode_Simple_Type()
    {
        using var editor = CreateOvertypeEditor();
        var model = editor.Model;

        editor.SetSelectionsValidated(S(1, 3, 1, 3));
        editor.Type("a", Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe(string.Join('\n', "12a456789", "123456789"), "assert1");

        editor.SetSelectionsValidated(S(1, 9, 1, 9));
        editor.Type("bbb", Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe(string.Join('\n', "12a45678bbb", "123456789"), "assert2");
    }

    [Fact]
    public void Overtype_Mode_Multi_Line_Selection_Type()
    {
        using var editor = CreateOvertypeEditor();
        var model = editor.Model;

        editor.SetSelectionsValidated(S(1, 5, 2, 3));
        editor.Type("cc", Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("1234cc456789", "assert1");
    }

    [Fact]
    public void Overtype_Mode_Simple_Paste()
    {
        using var editor = CreateOvertypeEditor();
        var model = editor.Model;

        editor.SetSelectionsValidated(S(1, 5, 1, 5));
        editor.Paste("cc", false);
        model.GetValue(EndOfLinePreference.LF).ShouldBe(string.Join('\n', "1234cc789", "123456789"), "assert1");

        editor.SetSelectionsValidated(S(1, 5, 1, 5));
        editor.Paste("dddddddd", false);
        model.GetValue(EndOfLinePreference.LF).ShouldBe(string.Join('\n', "1234dddddddd", "123456789"), "assert2");
    }

    [Fact]
    public void Overtype_Mode_Multi_Line_Selection_Paste()
    {
        using var editor = CreateOvertypeEditor();
        var model = editor.Model;

        editor.SetSelectionsValidated(S(1, 5, 2, 3));
        editor.Paste("cc", false);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("1234cc456789", "assert1");
    }

    [Fact]
    public void Overtype_Mode_Paste_Multi_Line_Text()
    {
        using var editor = CreateOvertypeEditor();
        var model = editor.Model;

        editor.SetSelectionsValidated(S(1, 5, 1, 5));
        editor.Paste(string.Join('\n', "aaaaaaa", "bbbbbbb"), false);
        model.GetValue(EndOfLinePreference.LF).ShouldBe(string.Join('\n', "1234aaaaaaa", "bbbbbbb", "123456789"), "assert1");
    }

    [Fact]
    public void Overtype_Mode_Composition_Type()
    {
        using var editor = CreateOvertypeEditor();
        var model = editor.Model;

        editor.SetSelectionsValidated(S(1, 5, 1, 5));
        editor.StartComposition();
        editor.CompositionType("セ", 0, 0, 0, Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe(string.Join('\n', "1234セ56789", "123456789"), "assert1");

        editor.EndComposition(Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe(string.Join('\n', "1234セ6789", "123456789"), "assert1");
    }
}
