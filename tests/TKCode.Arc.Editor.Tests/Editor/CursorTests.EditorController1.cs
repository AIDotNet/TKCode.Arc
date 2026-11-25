// Ported from vscode (src/vs/editor/test/browser/controller/cursor.test.ts, commit b57d5c9e), MIT license.
// Suite 'Editor Controller', from its setup to 'issue #122914' (the tests before 'issue #99629: Emoji modifiers in text
// treated separately when using backspace'), run on a TestCodeEditor: a TextModel, the real ViewModel and its
// CursorsController. This file has the helpers and the tests up to 'issue #9675'; the rest are in
// CursorTests.EditorController1.Part2.cs.
//
// Adaptations:
//  - the suite's languages are TypingTestContext's (its LanguageConfigurationService is given to every editor); there
//    is no language service, so registering a language is registering its configuration;
//  - withTestCodeEditor and usingCursor return the editor (the test disposes it) instead of taking a callback, so that
//    the tests that undo can await the model's undo and redo (ValueTask here, a promise upstream);
//  - CoreNavigationCommands and CoreEditingCommands are the engine's (Commands/CoreCommands*.cs); the editing commands
//    run on the TestCodeEditor, whose pushUndoStop does nothing in a read-only editor, as the code editor widget's;
//  - 'issue #46314': tokenization registries are per model here (the support goes into the model's), and
//    tokenizeIfCheap is written out as upstream's body (isCheapToTokenize, then forceTokenization); the
//    onDidChangeCursorPosition listener is the view model's outgoing CursorStateChangedEvent;
//  - the test cases that upstream generates in loops (multiCursorPasteCases, leftDeleteCases) are one test each, under
//    their upstream names.

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed partial class CursorTests_EditorController1 : IDisposable
{
    private readonly TypingTestContext _context = new();
    private readonly List<TextModel> _models = new();

    public void Dispose()
    {
        foreach (var model in _models)
        {
            model.Dispose();
        }
        _context.Dispose();
    }

    // --------- the suite's helpers

    private static TextSelection S(int selectionStartLineNumber, int selectionStartColumn, int positionLineNumber, int positionColumn) =>
        new(selectionStartLineNumber, selectionStartColumn, positionLineNumber, positionColumn);

    private static TextPosition P(int lineNumber, int column) => new(lineNumber, column);

    private static string Lines(params string[] lines) => string.Join('\n', lines);

    private static TextModelCreationOptions ModelOpts(bool? insertSpaces = null, bool? trimAutoWhitespace = null) =>
        TypingTestContext.ModelOpts(insertSpaces: insertSpaces, trimAutoWhitespace: trimAutoWhitespace);

    /// <summary>Upstream's createTextModel of the suite (disposed with the suite).</summary>
    private TextModel CreateTextModel(string text, string? languageId = null, TextModelCreationOptions? options = null)
    {
        var model = TestTextModel.Create(text, languageId, options);
        _models.Add(model);
        return model;
    }

    /// <summary>Upstream's withTestCodeEditor of the suite: an editor (with focus) on the model.</summary>
    private TestCodeEditor WithTestCodeEditor(TextModel model, TestCodeEditorOptions? options = null)
    {
        var editor = TestCodeEditor.Create(model, (options ?? new TestCodeEditorOptions()) with
        {
            LanguageConfigurationService = _context.LanguageConfigurationService,
        });
        editor.ViewModel.SetHasFocus(true);
        return editor;
    }

    private TestCodeEditor WithTestCodeEditor(string text, TestCodeEditorOptions? options = null) =>
        WithTestCodeEditor(CreateTextModel(text), options);

    private TestCodeEditor WithTestCodeEditor(string[] lines, TestCodeEditorOptions? options = null) =>
        WithTestCodeEditor(CreateTextModel(string.Join('\n', lines)), options);

    /// <summary>Upstream's usingCursor.</summary>
    private TestCodeEditor UsingCursor(string[] text, string? languageId = null, TextModelCreationOptions? modelOpts = null, TestCodeEditorOptions? editorOpts = null) =>
        WithTestCodeEditor(CreateTextModel(string.Join('\n', text), languageId, modelOpts), editorOpts);

    private static TestCodeEditorOptions EditorOpts(EditorOptions editorOptions) => new() { EditorOptions = editorOptions };

    private static void AssertCursor(TestViewModel viewModel, params TextSelection[] expected) =>
        viewModel.GetSelections().Select(s => s.ToString()).ShouldBe(expected.Select(s => s.ToString()));

    private static void AssertCursor(TestViewModel viewModel, TextPosition expected) =>
        AssertCursor(viewModel, S(expected.LineNumber, expected.Column, expected.LineNumber, expected.Column));

    // --------- the file's utils (CoreNavigationCommands)

    private static void MoveTo(TestViewModel viewModel, int lineNumber, int column, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.MoveToSelect : CoreNavigationCommands.MoveTo).RunCoreEditorCommand(viewModel, new() { Position = P(lineNumber, column) });

    private static void MoveLeft(TestViewModel viewModel, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.CursorLeftSelect : CoreNavigationCommands.CursorLeft).RunCoreEditorCommand(viewModel, new());

    private static void MoveRight(TestViewModel viewModel, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.CursorRightSelect : CoreNavigationCommands.CursorRight).RunCoreEditorCommand(viewModel, new());

    private static void MoveDown(TestViewModel viewModel, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.CursorDownSelect : CoreNavigationCommands.CursorDown).RunCoreEditorCommand(viewModel, new());

    private static void MoveUp(TestViewModel viewModel, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.CursorUpSelect : CoreNavigationCommands.CursorUp).RunCoreEditorCommand(viewModel, new());

    /// <summary>CoreNavigationCommands.WordSelect.</summary>
    private static void WordSelect(TestViewModel viewModel, TextPosition position) =>
        CoreNavigationCommands.WordSelect.RunCoreEditorCommand(viewModel, new() { Position = position });

    /// <summary>CoreNavigationCommands.WordSelectDrag.</summary>
    private static void WordSelectDrag(TestViewModel viewModel, TextPosition position) =>
        CoreNavigationCommands.WordSelectDrag.RunCoreEditorCommand(viewModel, new() { Position = position });

    // --------- CoreEditingCommands

    private static void Tab(TestCodeEditor editor) => CoreEditingCommands.Tab.RunEditorCommand(editor, null);

    private static void Outdent(TestCodeEditor editor) => CoreEditingCommands.Outdent.RunEditorCommand(editor, null);

    private static void DeleteLeft(TestCodeEditor editor) => CoreEditingCommands.DeleteLeft.RunEditorCommand(editor, null);

    private static ValueTask Undo(TestCodeEditor editor) => CoreEditingCommands.Undo.RunEditorCommand(editor, null);

    private static ValueTask Redo(TestCodeEditor editor) => CoreEditingCommands.Redo.RunEditorCommand(editor, null);

    // --------- tests

    [Fact]
    public void Issue_Microsoft_Monaco_Editor_443_Indentation_Of_A_Single_Row_Deletes_Selected_Text_In_Some_Cases()
    {
        var model = CreateTextModel(Lines("Hello world!", "another line"), null, ModelOpts(insertSpaces: false));
        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        viewModel.SetSelections("test", [S(1, 1, 1, 13)]);

        // Check that indenting maintains the selection start at column 1
        Tab(editor);
        viewModel.GetSelection().ShouldBe(S(1, 1, 1, 14));
    }

    [Fact]
    public async Task Bug_9121_Auto_Indent_Undo_Redo_Is_Funky()
    {
        var model = CreateTextModel(Lines(""), null, ModelOpts(insertSpaces: false, trimAutoWhitespace: false));
        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        viewModel.Type("\n", "keyboard");
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n", "assert1");

        Tab(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t", "assert2");

        viewModel.Type("\n", "keyboard");
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t\n\t", "assert3");

        viewModel.Type("x");
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t\n\tx", "assert4");

        CoreNavigationCommands.CursorLeft.RunCoreEditorCommand(viewModel, new());
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t\n\tx", "assert5");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t\nx", "assert6");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\tx", "assert7");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\nx", "assert8");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("x", "assert9");

        await Undo(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\nx", "assert10");

        await Undo(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t\nx", "assert11");

        await Undo(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t\n\tx", "assert12");

        await Redo(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t\nx", "assert13");

        await Redo(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\nx", "assert14");

        await Redo(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("x", "assert15");
    }

    [Fact]
    public async Task Issue_23539_Setting_Model_EOL_Isnt_Undoable()
    {
        using var editor = WithTestCodeEditor(["Hello", "world"]);
        var viewModel = editor.ViewModel;
        var model = editor.GetModel();

        AssertCursor(viewModel, P(1, 1));
        model.SetEOL(EndOfLineSequence.LF);
        model.GetValue().ShouldBe("Hello\nworld");

        model.PushEOL(EndOfLineSequence.CRLF);
        model.GetValue().ShouldBe("Hello\r\nworld");

        await Undo(editor);
        model.GetValue().ShouldBe("Hello\nworld");
    }

    [Fact]
    public async Task Issue_47733_Undo_Mangles_Unicode_Characters()
    {
        const string languageId = "myMode";

        _context.Add(_context.LanguageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            SurroundingPairs = [new AutoClosingPair("%", "%")],
        }));

        var model = CreateTextModel("'👁'", languageId);

        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        editor.SetSelection(S(1, 1, 1, 2));

        viewModel.Type("%", "keyboard");
        model.GetValue(EndOfLinePreference.LF).ShouldBe("%'%👁'", "assert1");

        await Undo(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("'👁'", "assert2");
    }

    [Fact]
    public async Task Issue_46208_Allow_Empty_Selections_In_The_Undo_Redo_Stack()
    {
        var model = CreateTextModel("");

        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        viewModel.Type("Hello", "keyboard");
        viewModel.Type(" ", "keyboard");
        viewModel.Type("world", "keyboard");
        viewModel.Type(" ", "keyboard");
        model.GetLineContent(1).ShouldBe("Hello world ");
        AssertCursor(viewModel, P(1, 13));

        MoveLeft(viewModel);
        MoveRight(viewModel);

        model.PushEditOperations([], [EditOperation.ReplaceMove(new TextRange(1, 12, 1, 13), "")], _ => []);
        model.GetLineContent(1).ShouldBe("Hello world");
        AssertCursor(viewModel, P(1, 12));

        await Undo(editor);
        model.GetLineContent(1).ShouldBe("Hello world ");
        AssertCursor(viewModel, S(1, 13, 1, 13));

        await Undo(editor);
        model.GetLineContent(1).ShouldBe("Hello world");
        AssertCursor(viewModel, P(1, 12));

        await Undo(editor);
        model.GetLineContent(1).ShouldBe("Hello");
        AssertCursor(viewModel, P(1, 6));

        await Undo(editor);
        model.GetLineContent(1).ShouldBe("");
        AssertCursor(viewModel, P(1, 1));

        await Redo(editor);
        model.GetLineContent(1).ShouldBe("Hello");
        AssertCursor(viewModel, P(1, 6));

        await Redo(editor);
        model.GetLineContent(1).ShouldBe("Hello world");
        AssertCursor(viewModel, P(1, 12));

        await Redo(editor);
        model.GetLineContent(1).ShouldBe("Hello world ");
        AssertCursor(viewModel, P(1, 13));

        await Redo(editor);
        model.GetLineContent(1).ShouldBe("Hello world");
        AssertCursor(viewModel, P(1, 12));

        await Redo(editor);
        model.GetLineContent(1).ShouldBe("Hello world");
        AssertCursor(viewModel, P(1, 12));
    }

    [Fact]
    public void Bug_16815_Shift_Tab_Doesnt_Go_Back_To_Tabstop()
    {
        var languageId = _context.SetupOnEnterLanguage(IndentAction.IndentOutdent);
        var model = CreateTextModel(Lines("     function baz() {"), languageId);

        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 6, false);
        AssertCursor(viewModel, S(1, 6, 1, 6));

        Outdent(editor);
        model.GetLineContent(1).ShouldBe("    function baz() {");
        AssertCursor(viewModel, S(1, 5, 1, 5));
    }

    [Fact]
    public void Bug_18293_Regression_Editor_Cant_Outdent_Whitespace_Line()
    {
        var model = CreateTextModel(Lines("      "));

        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 7, false);
        AssertCursor(viewModel, S(1, 7, 1, 7));

        Outdent(editor);
        model.GetLineContent(1).ShouldBe("    ");
        AssertCursor(viewModel, S(1, 5, 1, 5));
    }

    [Fact]
    public void Issue_95591_Unindenting_Moves_Cursor_To_Beginning_Of_Line()
    {
        var model = CreateTextModel(Lines("        "));

        using var editor = WithTestCodeEditor(model, EditorOpts(EditorOptions.Default with { UseTabStops = false }));
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 9, false);
        AssertCursor(viewModel, S(1, 9, 1, 9));

        Outdent(editor);
        model.GetLineContent(1).ShouldBe("    ");
        AssertCursor(viewModel, S(1, 5, 1, 5));
    }

    [Fact]
    public void Bug_16657_Editor_Tab_On_Empty_Line_Of_Zero_Indentation_Moves_Cursor_To_Position_1_1()
    {
        var model = CreateTextModel(
            Lines(
                "function baz() {",
                "\tfunction hello() { // something here",
                "\t",
                "",
                "\t}",
                "}",
                ""),
            null,
            ModelOpts(insertSpaces: false));

        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 7, 1, false);
        AssertCursor(viewModel, S(7, 1, 7, 1));

        Tab(editor);
        model.GetLineContent(7).ShouldBe("\t");
        AssertCursor(viewModel, S(7, 2, 7, 2));
    }

    [Fact]
    public void Bug_16740_Editor_Cut_Line_Doesnt_Quite_Cut_The_Last_Line()
    {
        // Part 1 => there is text on the last line
        using (var editor = WithTestCodeEditor(["asdasd", "qwerty"]))
        {
            var viewModel = editor.ViewModel;
            var model = editor.GetModel();

            MoveTo(viewModel, 2, 1, false);
            AssertCursor(viewModel, S(2, 1, 2, 1));

            viewModel.Cut("keyboard");
            model.GetLineCount().ShouldBe(1);
            model.GetLineContent(1).ShouldBe("asdasd");
        }

        // Part 2 => there is no text on the last line
        using (var editor = WithTestCodeEditor(["asdasd", ""]))
        {
            var viewModel = editor.ViewModel;
            var model = editor.GetModel();

            MoveTo(viewModel, 2, 1, false);
            AssertCursor(viewModel, S(2, 1, 2, 1));

            viewModel.Cut("keyboard");
            model.GetLineCount().ShouldBe(1);
            model.GetLineContent(1).ShouldBe("asdasd");

            viewModel.Cut("keyboard");
            model.GetLineCount().ShouldBe(1);
            model.GetLineContent(1).ShouldBe("");
        }
    }

    [Fact]
    public void Issue_128602_When_Cutting_Multiple_Lines_Ctrl_X_The_Last_Line_Will_Not_Be_Erased()
    {
        using var editor = WithTestCodeEditor(["a1", "a2", "a3"]);
        var viewModel = editor.ViewModel;
        var model = editor.GetModel();

        viewModel.SetSelections("test", [
            S(1, 1, 1, 1),
            S(2, 1, 2, 1),
            S(3, 1, 3, 1),
        ]);

        viewModel.Cut("keyboard");
        model.GetLineCount().ShouldBe(1);
        model.GetLineContent(1).ShouldBe("");
    }

    [Fact]
    public void Bug_11476_Double_Bracket_Surrounding_Undo_Is_Broken()
    {
        using var editor = UsingCursor(["hello"], TypingTestContext.SurroundingLanguageId);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 3, false);
        MoveTo(viewModel, 1, 5, true);
        AssertCursor(viewModel, S(1, 3, 1, 5));

        viewModel.Type("(", "keyboard");
        AssertCursor(viewModel, S(1, 4, 1, 6));

        viewModel.Type("(", "keyboard");
        AssertCursor(viewModel, S(1, 5, 1, 7));
    }

    [Fact]
    public void Issue_206774_SurroundSelectionCommand_With_Empty_CharAfterSelection_Should_Not_Throw()
    {
        // This test reproduces the issue where SurroundSelectionCommand throws when charAfterSelection is empty
        // The problem is that addTrackedEditOperation ignores empty strings, causing computeCursorState to fail
        // when trying to access inverseEditOperations[1].range (which is undefined)

        using var editor = UsingCursor(["hello world"], TypingTestContext.EmptyClosingSurroundLanguageId);
        var viewModel = editor.ViewModel;
        var model = editor.Model;

        // Select "hello"
        MoveTo(viewModel, 1, 1, false);
        MoveTo(viewModel, 1, 6, true);
        AssertCursor(viewModel, S(1, 1, 1, 6));

        // Type < which should surround with '<' and empty string
        // This reproduces the crash where charAfterSelection is empty
        viewModel.Type("<", "keyboard");

        // Test passes if we don't crash - the exact cursor position depends on the fix
        // The main issue is that computeCursorState fails when charAfterSelection is empty
        model.GetValue().ShouldBe("<hello world");
    }

    [Fact]
    public void Issue_1140_Backspace_Stops_Prematurely()
    {
        var model = CreateTextModel(Lines("function baz() {", "  return 1;", "};"));

        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 3, 2, false);
        MoveTo(viewModel, 1, 14, true);
        AssertCursor(viewModel, S(3, 2, 1, 14));

        DeleteLeft(editor);
        AssertCursor(viewModel, S(1, 14, 1, 14));
        model.GetLineCount().ShouldBe(1);
        model.GetLineContent(1).ShouldBe("function baz(;");
    }

    [Fact]
    public void Issue_10212_Pasting_Entire_Line_Does_Not_Replace_Selection()
    {
        using var editor = UsingCursor(["line1", "line2"]);
        var viewModel = editor.ViewModel;
        var model = editor.Model;

        MoveTo(viewModel, 2, 1, false);
        MoveTo(viewModel, 2, 6, true);

        viewModel.Paste("line1\n", true);

        model.GetLineContent(1).ShouldBe("line1");
        model.GetLineContent(2).ShouldBe("line1");
        model.GetLineContent(3).ShouldBe("");
    }

    [Fact]
    public void Issue_74722_Pasting_Whole_Line_Does_Not_Replace_Selection()
    {
        using var editor = UsingCursor(["line1", "line sel 2", "line3"]);
        var viewModel = editor.ViewModel;
        var model = editor.Model;

        viewModel.SetSelections("test", [S(2, 6, 2, 9)]);

        viewModel.Paste("line1\n", true);

        model.GetLineContent(1).ShouldBe("line1");
        model.GetLineContent(2).ShouldBe("line line1");
        model.GetLineContent(3).ShouldBe(" 2");
        model.GetLineContent(4).ShouldBe("line3");
    }

    [Fact]
    public void Issue_4996_Multiple_Cursor_Paste_Pastes_Contents_Of_All_Cursors()
    {
        using var editor = UsingCursor(["line1", "line2", "line3"]);
        var viewModel = editor.ViewModel;
        var model = editor.Model;

        viewModel.SetSelections("test", [S(1, 1, 1, 1), S(2, 1, 2, 1)]);

        viewModel.Paste("a\nb\nc\nd", false, ["a\nb", "c\nd"]);

        model.GetValue().ShouldBe(Lines("a", "bline1", "c", "dline2", "line3"));
    }

    // multiCursorPasteCases

    private void MultiCursorPasteCase(string[] text, TextSelection[] selections, string pastedText, string[] expected)
    {
        using var editor = UsingCursor(text);
        var viewModel = editor.ViewModel;
        var model = editor.Model;

        viewModel.SetSelections("test", selections);

        viewModel.Paste(pastedText, false, null);

        model.GetValue().ShouldBe(string.Join('\n', expected));
    }

    [Fact]
    public void Issue_16155_Paste_Into_Multiple_Cursors_Has_Edge_Case_When_Number_Of_Lines_Equals_Number_Of_Cursors_Minus_1() =>
        MultiCursorPasteCase(
            ["test", "test", "test", "test"],
            [S(1, 1, 1, 5), S(2, 1, 2, 5), S(3, 1, 3, 5), S(4, 1, 4, 5)],
            "aaa\nbbb\nccc\n",
            ["aaa", "bbb", "ccc", "", "aaa", "bbb", "ccc", "", "aaa", "bbb", "ccc", "", "aaa", "bbb", "ccc", ""]);

    [Fact]
    public void Issue_43722_Multiline_Paste_Doesnt_Work_Anymore() =>
        MultiCursorPasteCase(
            ["test", "test", "test", "test"],
            [S(1, 1, 1, 5), S(2, 1, 2, 5), S(3, 1, 3, 5), S(4, 1, 4, 5)],
            "aaa\r\nbbb\r\nccc\r\nddd\r\n",
            ["aaa", "bbb", "ccc", "ddd"]);

    [Fact]
    public void Issue_46440_1_Pasting_A_Multi_Line_Selection_Pastes_Entire_Selection_Into_Every_Insertion_Point() =>
        MultiCursorPasteCase(
            ["line1", "line2", "line3"],
            [S(1, 1, 1, 1), S(2, 1, 2, 1), S(3, 1, 3, 1)],
            "a\nb\nc",
            ["aline1", "bline2", "cline3"]);

    [Fact]
    public void Issue_46440_2_Pasting_A_Multi_Line_Selection_Pastes_Entire_Selection_Into_Every_Insertion_Point() =>
        MultiCursorPasteCase(
            ["line1", "line2", "line3"],
            [S(1, 1, 1, 1), S(2, 1, 2, 1), S(3, 1, 3, 1)],
            "a\nb\nc\n",
            ["aline1", "bline2", "cline3"]);

    [Fact]
    public void Issue_256039_Paste_From_Multiple_Cursors_With_Empty_Selections_And_MultiCursorPaste_Full()
    {
        using var editor = UsingCursor(
            ["line1", "line2", "line3"],
            editorOpts: EditorOpts(EditorOptions.Default with { MultiCursorPaste = MultiCursorPaste.Full }));
        var viewModel = editor.ViewModel;
        var model = editor.Model;

        // 2 cursors on lines 1 and 2
        viewModel.SetSelections("test", [S(1, 1, 1, 1), S(2, 1, 2, 1)]);

        viewModel.Paste("line1\nline2\n", true, ["line1\n", "line2\n"]);

        // Each cursor gets its respective line
        model.GetValue().ShouldBe(Lines("line1", "line1", "line2", "line2", "line3"));
    }

    [Fact]
    public async Task Issue_3071_Investigate_Why_Undo_Stack_Gets_Corrupted()
    {
        var model = CreateTextModel(Lines("some lines", "and more lines", "just some text"));

        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 1, false);
        MoveTo(viewModel, 3, 4, true);

        var isFirst = true;
        void OnDidChangeContent(ModelContentChangedEvent e)
        {
            if (isFirst)
            {
                isFirst = false;
                viewModel.Type("\t", "keyboard");
            }
        }
        model.DidChangeContent += OnDidChangeContent;

        Tab(editor);
        model.GetValue().ShouldBe(Lines("\t just some text"), "001");

        await Undo(editor);
        model.GetValue().ShouldBe(Lines("    some lines", "    and more lines", "    just some text"), "002");

        await Undo(editor);
        model.GetValue().ShouldBe(Lines("some lines", "and more lines", "just some text"), "003");

        await Undo(editor);
        model.GetValue().ShouldBe(Lines("some lines", "and more lines", "just some text"), "004");

        model.DidChangeContent -= OnDidChangeContent;
    }

    [Fact]
    public void Issue_12950_Cannot_Double_Click_To_Insert_Emoji_Using_OSX_Emoji_Panel()
    {
        using var editor = UsingCursor(["some lines", "and more lines", "just some text"], languageId: null);
        var viewModel = editor.ViewModel;
        var model = editor.Model;

        MoveTo(viewModel, 3, 1, false);

        viewModel.Type("😍", "keyboard");

        model.GetValue().ShouldBe(Lines("some lines", "and more lines", "😍just some text"));
    }

    [Fact]
    public void Issue_3463_Pressing_Tab_Adds_Spaces_But_Not_As_Many_As_For_A_Tab()
    {
        var model = CreateTextModel(Lines("function a() {", "\tvar a = {", "\t\tx: 3", "\t};", "}"));

        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 3, 2, false);
        Tab(editor);
        model.GetLineContent(3).ShouldBe("\t    \tx: 3");
    }

    [Fact]
    public void Issue_4312_Trying_To_Type_A_Tab_Character_Over_A_Sequence_Of_Spaces_Results_In_Unexpected_Behaviour()
    {
        var model = CreateTextModel(
            Lines("var foo = 123;       // this is a comment", "var bar = 4;       // another comment"),
            null,
            ModelOpts(insertSpaces: false));

        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 15, false);
        MoveTo(viewModel, 1, 22, true);
        Tab(editor);
        model.GetLineContent(1).ShouldBe("var foo = 123;\t// this is a comment");
    }

    [Fact]
    public void Issue_832_Word_Right()
    {
        using var editor = UsingCursor(["   /* Just some   more   text a+= 3 +5-3 + 7 */  "]);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 1, false);

        void AssertWordRight(int col, int expectedCol)
        {
            var position = P(1, col);
            if (col == 1)
            {
                WordSelect(viewModel, position);
            }
            else
            {
                WordSelectDrag(viewModel, position);
            }

            viewModel.GetSelection().StartColumn.ShouldBe(1, "TEST FOR " + col);
            viewModel.GetSelection().EndColumn.ShouldBe(expectedCol, "TEST FOR " + col);
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
        var model = CreateTextModel(Lines("Just some text"));

        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        WordSelect(viewModel, P(1, 8));
        viewModel.GetSelection().ShouldBe(S(1, 6, 1, 10));

        WordSelectDrag(viewModel, P(1, 8));
        viewModel.GetSelection().ShouldBe(S(1, 6, 1, 10));
    }

    [Fact]
    public void Issue_12887_Double_Click_Highlighting_Separating_White_Space()
    {
        var model = CreateTextModel(Lines("abc def"));

        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        WordSelect(viewModel, P(1, 5));
        viewModel.GetSelection().ShouldBe(S(1, 5, 1, 8));
    }

    [Fact]
    public void Double_Click_On_Punctuation_Should_Select_The_Character_Not_Adjacent_Space()
    {
        var model = CreateTextModel(Lines("// a b c 1 2 3 ~ ! @ # $ % ^ & * ( ) _ + \\ /"));

        using var editor = WithTestCodeEditor(model);
        var viewModel = editor.ViewModel;

        // Test double-click on '@' at position 20
        WordSelect(viewModel, P(1, 20));
        viewModel.GetSelection().ShouldBe(S(1, 20, 1, 21), "Should select @ character");

        // Test double-click on '#' at position 22
        WordSelect(viewModel, P(1, 22));
        viewModel.GetSelection().ShouldBe(S(1, 22, 1, 23), "Should select # character");

        // Test double-click on '!' at position 18
        WordSelect(viewModel, P(1, 18));
        viewModel.GetSelection().ShouldBe(S(1, 18, 1, 19), "Should select ! character");

        // Test double-click on first '/' in '//' at position 1
        WordSelect(viewModel, P(1, 1));
        viewModel.GetSelection().ShouldBe(S(1, 1, 1, 3), "Should select // token");

        // Test double-click on second '/' in '//' at position 2
        WordSelect(viewModel, P(1, 2));
        viewModel.GetSelection().ShouldBe(S(1, 1, 1, 3), "Should select // token");

        // Test double-click on '\' at position 42
        WordSelect(viewModel, P(1, 42));
        viewModel.GetSelection().ShouldBe(S(1, 42, 1, 43), "Should select \\ character");
    }

    [Fact]
    public async Task Issue_9675_Undo_Redo_Adds_A_Stop_In_Between_CHN_Characters()
    {
        using var editor = WithTestCodeEditor(Array.Empty<string>());
        var viewModel = editor.ViewModel;
        var model = editor.GetModel();
        AssertCursor(viewModel, P(1, 1));

        // Typing sennsei in Japanese - Hiragana
        viewModel.Type("ｓ", "keyboard");
        viewModel.CompositionType("せ", 1, 0, 0);
        viewModel.CompositionType("せｎ", 1, 0, 0);
        viewModel.CompositionType("せん", 2, 0, 0);
        viewModel.CompositionType("せんｓ", 2, 0, 0);
        viewModel.CompositionType("せんせ", 3, 0, 0);
        viewModel.CompositionType("せんせ", 3, 0, 0);
        viewModel.CompositionType("せんせい", 3, 0, 0);
        viewModel.CompositionType("せんせい", 4, 0, 0);
        viewModel.CompositionType("せんせい", 4, 0, 0);
        viewModel.CompositionType("せんせい", 4, 0, 0);

        model.GetLineContent(1).ShouldBe("せんせい");
        AssertCursor(viewModel, P(1, 5));

        await Undo(editor);
        model.GetLineContent(1).ShouldBe("");
        AssertCursor(viewModel, P(1, 1));
    }
}
