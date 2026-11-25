// Ported from vscode (src/vs/editor/test/browser/controller/cursor.test.ts, commit b57d5c9e), MIT license.
// This part: the suites 'Undo stops' and 'Overtype Mode'.
// Adaptations:
//  - the editor is a TestCodeEditor (Editor/TestCodeEditor.cs) on a model the test owns; CoreEditingCommands.DeleteLeft
//    and DeleteRight are the engine's (Commands/CoreCommands.Editing.cs), and CoreEditingCommands.Undo is
//    TestCodeEditor.Undo (awaited, so the tests are async);
//  - upstream's table driven tests (typingAndDeletingCases) are one [Fact] per case, named after the case;
//  - 'Overtype Mode' gives the editor the overtype input mode (upstream sets the process-wide InputMode in setup and
//    resets it in teardown; here the input mode is part of the editor's options).

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class CursorTests_UndoStops
{
    private static TextSelection S(int selectionStartLineNumber, int selectionStartColumn, int positionLineNumber, int positionColumn) =>
        new(selectionStartLineNumber, selectionStartColumn, positionLineNumber, positionColumn);

    private static void AssertCursor(TestViewModel viewModel, params TextSelection[] selections)
    {
        var actual = viewModel.GetSelections().Select(s => s.ToString()).ToArray();
        var expected = selections.Select(s => s.ToString()).ToArray();
        actual.ShouldBe(expected);
    }

    private static void DeleteLeft(TestCodeEditor editor) => CoreEditingCommands.DeleteLeft.RunEditorCommand(editor, null);

    private static void DeleteRight(TestCodeEditor editor) => CoreEditingCommands.DeleteRight.RunEditorCommand(editor, null);

    private static void Repeat(int count, Action action)
    {
        for (var i = 0; i < count; i++)
        {
            action();
        }
    }

    private static TextModel CreateModel() => TestTextModel.Create(string.Join('\n', "A  line", "Another line"));

    private static async Task TypingAndDeletingCase(Action<TestCodeEditor> deleteCommand, string expectedLine, TextSelection expectedSelection)
    {
        using var model = CreateModel();
        using var editor = TestCodeEditor.Create(model);
        var viewModel = editor.ViewModel;
        viewModel.SetHasFocus(true);

        viewModel.SetSelections("test", [S(1, 3, 1, 3)]);
        viewModel.Type("first", "keyboard");
        model.GetLineContent(1).ShouldBe("A first line");
        AssertCursor(viewModel, S(1, 8, 1, 8));

        deleteCommand(editor);
        deleteCommand(editor);
        model.GetLineContent(1).ShouldBe(expectedLine);
        AssertCursor(viewModel, expectedSelection);

        await editor.Undo();
        model.GetLineContent(1).ShouldBe("A first line");
        AssertCursor(viewModel, S(1, 8, 1, 8));

        await editor.Undo();
        model.GetLineContent(1).ShouldBe("A  line");
        AssertCursor(viewModel, S(1, 3, 1, 3));
    }

    [Fact]
    public Task There_Is_An_Undo_Stop_Between_Typing_And_Deleting_Left() =>
        TypingAndDeletingCase(DeleteLeft, "A fir line", S(1, 6, 1, 6));

    [Fact]
    public Task There_Is_An_Undo_Stop_Between_Typing_And_Deleting_Right() =>
        TypingAndDeletingCase(DeleteRight, "A firstine", S(1, 8, 1, 8));

    [Fact]
    public async Task There_Is_An_Undo_Stop_Between_Deleting_Left_And_Typing()
    {
        using var model = CreateModel();
        using var editor = TestCodeEditor.Create(model);
        var viewModel = editor.ViewModel;
        viewModel.SetHasFocus(true);

        viewModel.SetSelections("test", [S(2, 8, 2, 8)]);
        Repeat(7, () => DeleteLeft(editor));
        model.GetLineContent(2).ShouldBe(" line");
        AssertCursor(viewModel, S(2, 1, 2, 1));

        viewModel.Type("Second", "keyboard");
        model.GetLineContent(2).ShouldBe("Second line");
        AssertCursor(viewModel, S(2, 7, 2, 7));

        await editor.Undo();
        model.GetLineContent(2).ShouldBe(" line");
        AssertCursor(viewModel, S(2, 1, 2, 1));

        await editor.Undo();
        model.GetLineContent(2).ShouldBe("Another line");
        AssertCursor(viewModel, S(2, 8, 2, 8));
    }

    [Fact]
    public async Task There_Is_An_Undo_Stop_Between_Deleting_Left_And_Deleting_Right()
    {
        using var model = CreateModel();
        using var editor = TestCodeEditor.Create(model);
        var viewModel = editor.ViewModel;
        viewModel.SetHasFocus(true);

        viewModel.SetSelections("test", [S(2, 8, 2, 8)]);
        Repeat(7, () => DeleteLeft(editor));
        model.GetLineContent(2).ShouldBe(" line");
        AssertCursor(viewModel, S(2, 1, 2, 1));

        Repeat(5, () => DeleteRight(editor));
        model.GetLineContent(2).ShouldBe("");
        AssertCursor(viewModel, S(2, 1, 2, 1));

        await editor.Undo();
        model.GetLineContent(2).ShouldBe(" line");
        AssertCursor(viewModel, S(2, 1, 2, 1));

        await editor.Undo();
        model.GetLineContent(2).ShouldBe("Another line");
        AssertCursor(viewModel, S(2, 8, 2, 8));
    }

    [Fact]
    public async Task There_Is_An_Undo_Stop_Between_Deleting_Right_And_Typing()
    {
        using var model = CreateModel();
        using var editor = TestCodeEditor.Create(model);
        var viewModel = editor.ViewModel;
        viewModel.SetHasFocus(true);

        viewModel.SetSelections("test", [S(2, 9, 2, 9)]);
        Repeat(4, () => DeleteRight(editor));
        model.GetLineContent(2).ShouldBe("Another ");
        AssertCursor(viewModel, S(2, 9, 2, 9));

        viewModel.Type("text", "keyboard");
        model.GetLineContent(2).ShouldBe("Another text");
        AssertCursor(viewModel, S(2, 13, 2, 13));

        await editor.Undo();
        model.GetLineContent(2).ShouldBe("Another ");
        AssertCursor(viewModel, S(2, 9, 2, 9));

        await editor.Undo();
        model.GetLineContent(2).ShouldBe("Another line");
        AssertCursor(viewModel, S(2, 9, 2, 9));
    }

    [Fact]
    public async Task There_Is_An_Undo_Stop_Between_Deleting_Right_And_Deleting_Left()
    {
        using var model = CreateModel();
        using var editor = TestCodeEditor.Create(model);
        var viewModel = editor.ViewModel;
        viewModel.SetHasFocus(true);

        viewModel.SetSelections("test", [S(2, 9, 2, 9)]);
        Repeat(4, () => DeleteRight(editor));
        model.GetLineContent(2).ShouldBe("Another ");
        AssertCursor(viewModel, S(2, 9, 2, 9));

        Repeat(6, () => DeleteLeft(editor));
        model.GetLineContent(2).ShouldBe("An");
        AssertCursor(viewModel, S(2, 3, 2, 3));

        await editor.Undo();
        model.GetLineContent(2).ShouldBe("Another ");
        AssertCursor(viewModel, S(2, 9, 2, 9));

        await editor.Undo();
        model.GetLineContent(2).ShouldBe("Another line");
        AssertCursor(viewModel, S(2, 9, 2, 9));
    }

    [Fact]
    public async Task Inserts_Undo_Stop_When_Typing_Space()
    {
        using var model = CreateModel();
        using var editor = TestCodeEditor.Create(model);
        var viewModel = editor.ViewModel;
        viewModel.SetHasFocus(true);

        viewModel.SetSelections("test", [S(1, 3, 1, 3)]);
        viewModel.Type("first and interesting", "keyboard");
        model.GetLineContent(1).ShouldBe("A first and interesting line");
        AssertCursor(viewModel, S(1, 24, 1, 24));

        await editor.Undo();
        model.GetLineContent(1).ShouldBe("A first and line");
        AssertCursor(viewModel, S(1, 12, 1, 12));

        await editor.Undo();
        model.GetLineContent(1).ShouldBe("A first line");
        AssertCursor(viewModel, S(1, 8, 1, 8));

        await editor.Undo();
        model.GetLineContent(1).ShouldBe("A  line");
        AssertCursor(viewModel, S(1, 3, 1, 3));
    }

    [Fact]
    public async Task Can_Undo_Typing_And_EOL_Change_In_One_Undo_Stop()
    {
        using var model = CreateModel();
        using var editor = TestCodeEditor.Create(model);
        var viewModel = editor.ViewModel;
        viewModel.SetHasFocus(true);

        viewModel.SetSelections("test", [S(1, 3, 1, 3)]);
        viewModel.Type("first", "keyboard");
        model.GetValue().ShouldBe("A first line\nAnother line");
        AssertCursor(viewModel, S(1, 8, 1, 8));

        model.PushEOL(EndOfLineSequence.CRLF);
        model.GetValue().ShouldBe("A first line\r\nAnother line");
        AssertCursor(viewModel, S(1, 8, 1, 8));

        await editor.Undo();
        model.GetValue().ShouldBe("A  line\nAnother line");
        AssertCursor(viewModel, S(1, 3, 1, 3));
    }

    [Fact]
    public async Task Issue_93585_Undo_Multi_Cursor_Edit_Corrupts_Document()
    {
        using var model = TestTextModel.Create(string.Join('\n', "hello world", "hello world"));
        using var editor = TestCodeEditor.Create(model);
        var viewModel = editor.ViewModel;
        viewModel.SetHasFocus(true);

        viewModel.SetSelections("test",
        [
            S(2, 7, 2, 12),
            S(1, 7, 1, 12),
        ]);
        viewModel.Type("no", "keyboard");
        model.GetValue().ShouldBe("hello no\nhello no");

        await editor.Undo();
        model.GetValue().ShouldBe("hello world\nhello world");
    }

    private static TextModel CreateEmptyModelWithTabs() =>
        TestTextModel.Create("", null, TextModel.DefaultCreationOptions with { InsertSpaces = false });

    [Fact]
    public async Task There_Is_A_Single_Undo_Stop_For_Consecutive_Whitespaces()
    {
        using var model = CreateEmptyModelWithTabs();
        using var editor = TestCodeEditor.Create(model);
        var viewModel = editor.ViewModel;
        viewModel.SetHasFocus(true);

        viewModel.Type("a", "keyboard");
        viewModel.Type("b", "keyboard");
        viewModel.Type(" ", "keyboard");
        viewModel.Type(" ", "keyboard");
        viewModel.Type("c", "keyboard");
        viewModel.Type("d", "keyboard");

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
        using var model = CreateEmptyModelWithTabs();
        using var editor = TestCodeEditor.Create(model);
        var viewModel = editor.ViewModel;
        viewModel.SetHasFocus(true);

        viewModel.Type("a", "keyboard");
        viewModel.Type("b", "keyboard");
        viewModel.Type(" ", "keyboard");
        viewModel.Type("c", "keyboard");
        viewModel.Type("d", "keyboard");

        model.GetValue(EndOfLinePreference.LF).ShouldBe("ab cd", "assert1");

        await editor.Undo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("ab", "assert3");

        await editor.Undo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("", "assert4");
    }
}

public sealed class CursorTests_OvertypeMode
{
    private static readonly TestCodeEditorOptions s_overtype = new() { InputMode = EditorInputMode.Overtype };

    private static TextSelection S(int selectionStartLineNumber, int selectionStartColumn, int positionLineNumber, int positionColumn) =>
        new(selectionStartLineNumber, selectionStartColumn, positionLineNumber, positionColumn);

    private static TextModel CreateModel() =>
        TestTextModel.Create(
            string.Join('\n', "123456789", "123456789"),
            null,
            TextModel.DefaultCreationOptions with { InsertSpaces = false });

    private static void WithOvertypeEditor(Action<TextModel, TestViewModel> callback)
    {
        using var model = CreateModel();
        TestCodeEditor.WithTestCodeEditor(model, s_overtype, (editor, viewModel) => callback(model, viewModel));
    }

    [Fact]
    public void Simple_Type() => WithOvertypeEditor((model, viewModel) =>
    {
        viewModel.SetSelections("test", [S(1, 3, 1, 3)]);
        viewModel.Type("a", "keyboard");
        model.GetValue(EndOfLinePreference.LF).ShouldBe(string.Join('\n',
            "12a456789",
            "123456789"), "assert1");

        viewModel.SetSelections("test", [S(1, 9, 1, 9)]);
        viewModel.Type("bbb", "keyboard");
        model.GetValue(EndOfLinePreference.LF).ShouldBe(string.Join('\n',
            "12a45678bbb",
            "123456789"), "assert2");
    });

    [Fact]
    public void Multi_Line_Selection_Type() => WithOvertypeEditor((model, viewModel) =>
    {
        viewModel.SetSelections("test", [S(1, 5, 2, 3)]);
        viewModel.Type("cc", "keyboard");
        model.GetValue(EndOfLinePreference.LF).ShouldBe("1234cc456789", "assert1");
    });

    [Fact]
    public void Simple_Paste() => WithOvertypeEditor((model, viewModel) =>
    {
        viewModel.SetSelections("test", [S(1, 5, 1, 5)]);
        viewModel.Paste("cc", false);
        model.GetValue(EndOfLinePreference.LF).ShouldBe(string.Join('\n',
            "1234cc789",
            "123456789"), "assert1");

        viewModel.SetSelections("test", [S(1, 5, 1, 5)]);
        viewModel.Paste("dddddddd", false);
        model.GetValue(EndOfLinePreference.LF).ShouldBe(string.Join('\n',
            "1234dddddddd",
            "123456789"), "assert2");
    });

    [Fact]
    public void Multi_Line_Selection_Paste() => WithOvertypeEditor((model, viewModel) =>
    {
        viewModel.SetSelections("test", [S(1, 5, 2, 3)]);
        viewModel.Paste("cc", false);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("1234cc456789", "assert1");
    });

    [Fact]
    public void Paste_Multi_Line_Text() => WithOvertypeEditor((model, viewModel) =>
    {
        viewModel.SetSelections("test", [S(1, 5, 1, 5)]);
        viewModel.Paste(string.Join('\n', "aaaaaaa", "bbbbbbb"), false);
        model.GetValue(EndOfLinePreference.LF).ShouldBe(string.Join('\n',
            "1234aaaaaaa",
            "bbbbbbb",
            "123456789"), "assert1");
    });

    [Fact]
    public void Composition_Type() => WithOvertypeEditor((model, viewModel) =>
    {
        viewModel.SetSelections("test", [S(1, 5, 1, 5)]);
        viewModel.StartComposition();
        viewModel.CompositionType("セ", 0, 0, 0, "keyboard");
        model.GetValue(EndOfLinePreference.LF).ShouldBe(string.Join('\n',
            "1234セ56789",
            "123456789"), "assert1");

        viewModel.EndComposition("keyboard");
        model.GetValue(EndOfLinePreference.LF).ShouldBe(string.Join('\n',
            "1234セ6789",
            "123456789"), "assert1");
    });
}
