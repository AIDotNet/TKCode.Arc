// Ported from vscode (src/vs/editor/test/browser/controller/cursor.test.ts, commit b57d5c9e), MIT license.
// Suite 'Editor Controller', the tests from 'issue #99629: Emoji modifiers in text treated separately when using
// backspace' up to (not including) 'issue #61070: backtick (`) should auto-close after a word character'; this file has
// the tests up to 'issue #90973', CursorTests.EditorController2.Indentation.cs the rest.
//
// Adaptations:
//  - the tests run on a TestCodeEditor (the real TextModel, ViewModel and CursorsController); the suite's languages
//    come from TypingTestContext, whose LanguageConfigurationService the editors use;
//  - upstream's createTextModel + withTestCodeEditor / usingCursor is CreateEditor (the editor owns the model);
//  - the CoreNavigationCommands and CoreEditingCommands.DeleteLeft and Tab are the engine's (Commands/CoreCommands*.cs),
//    and Undo / Redo are the model's undo and redo (awaited);
//  - the test editor options (wordWrap, wrappingIndent, useTabStops, stickyTabStops, autoIndent) are
//    TestCodeEditorOptions / EditorOptions;
//  - the parameterized upstream tests (for-loops over case tables) are one [Fact] per case, named after the case;
//  - model.tokenization.forceTokenization is left out: the tokens of these languages are always accurate here.

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed partial class CursorTests_EditorController2 : IDisposable
{
    private const string Keyboard = "keyboard";

    private readonly TypingTestContext _context = new();

    public void Dispose() => _context.Dispose();

    // ------ helpers

    private static TextSelection S(int selectionStartLineNumber, int selectionStartColumn, int positionLineNumber, int positionColumn) =>
        new(selectionStartLineNumber, selectionStartColumn, positionLineNumber, positionColumn);

    private static TextPosition P(int lineNumber, int column) => new(lineNumber, column);

    private static TextModelCreationOptions ModelOpts(
        bool? insertSpaces = null,
        int? tabSize = null,
        int? indentSize = null,
        bool? trimAutoWhitespace = null,
        bool? detectIndentation = null) =>
        TypingTestContext.ModelOpts(insertSpaces, tabSize, indentSize, trimAutoWhitespace, detectIndentation);

    private static TestCodeEditorOptions EditorOpts(EditorOptions editorOptions) => new() { EditorOptions = editorOptions };

    private static readonly TestCodeEditorOptions s_autoIndentFull = EditorOpts(EditorOptions.Default with { AutoIndent = EditorAutoIndentStrategy.Full });

    /// <summary>Upstream's createTextModel + withTestCodeEditor (or usingCursor): an editor with focus on a new model.</summary>
    private TestCodeEditor CreateEditor(string text, string? languageId = null, TextModelCreationOptions? modelOpts = null, TestCodeEditorOptions? options = null)
    {
        var editor = TestCodeEditor.Create(text, (options ?? new TestCodeEditorOptions()) with
        {
            LanguageId = languageId,
            ModelOptions = modelOpts,
            LanguageConfigurationService = _context.LanguageConfigurationService,
        });
        editor.ViewModel.SetHasFocus(true);
        return editor;
    }

    private TestCodeEditor CreateEditor(string[] lines, string? languageId = null, TextModelCreationOptions? modelOpts = null, TestCodeEditorOptions? options = null) =>
        CreateEditor(string.Join('\n', lines), languageId, modelOpts, options);

    // moveTo, moveLeft, moveRight
    private static void MoveTo(TestViewModel viewModel, int lineNumber, int column, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.MoveToSelect : CoreNavigationCommands.MoveTo).RunCoreEditorCommand(viewModel, new() { Position = new TextPosition(lineNumber, column) });

    private static void MoveLeft(TestViewModel viewModel, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.CursorLeftSelect : CoreNavigationCommands.CursorLeft).RunCoreEditorCommand(viewModel, new());

    private static void MoveRight(TestViewModel viewModel, bool inSelectionMode = false) =>
        (inSelectionMode ? CoreNavigationCommands.CursorRightSelect : CoreNavigationCommands.CursorRight).RunCoreEditorCommand(viewModel, new());

    // assertCursor
    private static void AssertCursor(TestViewModel viewModel, params TextSelection[] expected) =>
        viewModel.GetSelections().Select(s => s.ToString()).ShouldBe(expected.Select(s => s.ToString()));

    private static void AssertCursor(TestViewModel viewModel, TextPosition expected) =>
        AssertCursor(viewModel, new TextSelection(expected.LineNumber, expected.Column, expected.LineNumber, expected.Column));

    private static void DeleteLeft(TestCodeEditor editor) => CoreEditingCommands.DeleteLeft.RunEditorCommand(editor, null);

    private static void Tab(TestCodeEditor editor) => CoreEditingCommands.Tab.RunEditorCommand(editor, null);

    // ------ tests

    [Fact]
    public void Issue_99629_Emoji_Modifiers_In_Text_Treated_Separately_When_Using_Backspace()
    {
        using var editor = CreateEditor("\U0001F476\U0001F3FE", options: EditorOpts(EditorOptions.Default with { UseTabStops = false }));
        var model = editor.Model;

        var len = model.GetValueLength();
        editor.SetSelections([S(1, 1 + len, 1, 1 + len)]);

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("");
    }

    [Fact]
    public void Issue_99629_Emoji_Modifiers_In_Text_Treated_Separately_When_Using_Backspace_ZWJ_Sequence()
    {
        using var editor = CreateEditor(
            "\U0001F468‍\U0001F469\U0001F3FD‍\U0001F467‍\U0001F466",
            options: EditorOpts(EditorOptions.Default with { UseTabStops = false }));
        var model = editor.Model;

        var len = model.GetValueLength();
        editor.SetSelections([S(1, 1 + len, 1, 1 + len)]);

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\U0001F468‍\U0001F469\U0001F3FD‍\U0001F467");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\U0001F468‍\U0001F469\U0001F3FD");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\U0001F468");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("");
    }

    [Fact]
    public void Issue_105730_Move_Left_Behaves_Differently_For_Multiple_Cursors()
    {
        using var editor = CreateEditor(
            "asdfghjkl, asdfghjkl, asdfghjkl, ",
            options: new TestCodeEditorOptions { WordWrap = TestWordWrap.WordWrapColumn, WordWrapColumn = 24 });
        var viewModel = editor.ViewModel;

        viewModel.SetSelections("test", [S(1, 10, 1, 12), S(1, 21, 1, 23), S(1, 32, 1, 34)]);
        MoveLeft(viewModel, false);
        AssertCursor(viewModel, S(1, 10, 1, 10), S(1, 21, 1, 21), S(1, 32, 1, 32));

        viewModel.SetSelections("test", [S(1, 10, 1, 12), S(1, 21, 1, 23), S(1, 32, 1, 34)]);
        MoveLeft(viewModel, true);
        AssertCursor(viewModel, S(1, 10, 1, 11), S(1, 21, 1, 22), S(1, 32, 1, 33));
    }

    [Fact]
    public void Issue_105730_Move_Right_Should_Always_Skip_Wrap_Point()
    {
        using var editor = CreateEditor(
            "asdfghjkl, asdfghjkl, asdfghjkl, \nasdfghjkl,",
            options: new TestCodeEditorOptions { WordWrap = TestWordWrap.WordWrapColumn, WordWrapColumn = 24 });
        var viewModel = editor.ViewModel;

        viewModel.SetSelections("test", [S(1, 22, 1, 22)]);
        MoveRight(viewModel, false);
        MoveRight(viewModel, false);
        AssertCursor(viewModel, S(1, 24, 1, 24));

        viewModel.SetSelections("test", [S(1, 22, 1, 22)]);
        MoveRight(viewModel, true);
        MoveRight(viewModel, true);
        AssertCursor(viewModel, S(1, 22, 1, 24));
    }

    [Fact]
    public void Move_Right_Normalizes_The_Cursor_To_The_Next_Wrapped_Line()
    {
        using var editor = CreateEditor(
            "text edit",
            options: new TestCodeEditorOptions { WordWrap = TestWordWrap.WordWrapColumn, WordWrapColumn = 5, WrappingIndent = WrappingIndent.None });
        var viewModel = editor.ViewModel;

        viewModel.SetSelections("test", [S(1, 5, 1, 5)]);
        MoveRight(viewModel);

        viewModel.GetCursorStates()[0].ViewState.Position.ShouldBe(P(2, 1));
    }

    [Fact]
    public void Issue_123178_Sticky_Tab_In_Consecutive_Wrapped_Lines()
    {
        using var editor = CreateEditor(
            "    aaaa        aaaa",
            modelOpts: ModelOpts(tabSize: 4),
            options: new TestCodeEditorOptions
            {
                WordWrap = TestWordWrap.WordWrapColumn,
                WordWrapColumn = 8,
                EditorOptions = EditorOptions.Default with { StickyTabStops = true },
            });
        var viewModel = editor.ViewModel;

        viewModel.SetSelections("test", [S(1, 9, 1, 9)]);
        MoveRight(viewModel, false);
        AssertCursor(viewModel, S(1, 10, 1, 10));

        MoveLeft(viewModel, false);
        AssertCursor(viewModel, S(1, 9, 1, 9));
    }

    [Fact]
    public void Cursor_Honors_InsertSpaces_Configuration_On_New_Line()
    {
        using var editor = CreateEditor(["    \tMy First Line\t ", "\tMy Second Line", "    Third Line", "", "1"]);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        CoreNavigationCommands.MoveTo.RunCoreEditorCommand(viewModel, new() { Position = P(1, 21) });
        viewModel.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    \tMy First Line\t ");
        model.GetLineContent(2).ShouldBe("        ");
    }

    [Fact]
    public async Task Cursor_Honors_InsertSpaces_Configuration_On_Tab()
    {
        using var editor = CreateEditor(
            ["    \tMy First Line\t ", "My Second Line123", "    Third Line", "", "1"],
            modelOpts: ModelOpts(tabSize: 13, indentSize: 13));
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        // Tab on column 1
        CoreNavigationCommands.MoveTo.RunCoreEditorCommand(viewModel, new() { Position = P(2, 1) });
        Tab(editor);
        model.GetLineContent(2).ShouldBe("             My Second Line123");
        await editor.Undo();

        // Tab on column 2
        model.GetLineContent(2).ShouldBe("My Second Line123");
        CoreNavigationCommands.MoveTo.RunCoreEditorCommand(viewModel, new() { Position = P(2, 2) });
        Tab(editor);
        model.GetLineContent(2).ShouldBe("M            y Second Line123");
        await editor.Undo();

        // Tab on column 3
        model.GetLineContent(2).ShouldBe("My Second Line123");
        CoreNavigationCommands.MoveTo.RunCoreEditorCommand(viewModel, new() { Position = P(2, 3) });
        Tab(editor);
        model.GetLineContent(2).ShouldBe("My            Second Line123");
        await editor.Undo();

        // Tab on column 4
        model.GetLineContent(2).ShouldBe("My Second Line123");
        CoreNavigationCommands.MoveTo.RunCoreEditorCommand(viewModel, new() { Position = P(2, 4) });
        Tab(editor);
        model.GetLineContent(2).ShouldBe("My           Second Line123");
        await editor.Undo();

        // Tab on column 5
        model.GetLineContent(2).ShouldBe("My Second Line123");
        CoreNavigationCommands.MoveTo.RunCoreEditorCommand(viewModel, new() { Position = P(2, 5) });
        Tab(editor);
        model.GetLineContent(2).ShouldBe("My S         econd Line123");
        await editor.Undo();

        // Tab on column 5
        model.GetLineContent(2).ShouldBe("My Second Line123");
        CoreNavigationCommands.MoveTo.RunCoreEditorCommand(viewModel, new() { Position = P(2, 5) });
        Tab(editor);
        model.GetLineContent(2).ShouldBe("My S         econd Line123");
        await editor.Undo();

        // Tab on column 13
        model.GetLineContent(2).ShouldBe("My Second Line123");
        CoreNavigationCommands.MoveTo.RunCoreEditorCommand(viewModel, new() { Position = P(2, 13) });
        Tab(editor);
        model.GetLineContent(2).ShouldBe("My Second Li ne123");
        await editor.Undo();

        // Tab on column 14
        model.GetLineContent(2).ShouldBe("My Second Line123");
        CoreNavigationCommands.MoveTo.RunCoreEditorCommand(viewModel, new() { Position = P(2, 14) });
        Tab(editor);
        model.GetLineContent(2).ShouldBe("My Second Lin             e123");
    }

    // enterAutoIndentCases
    private void EnterAutoIndentCase(IndentAction indentAction, string text, string expected)
    {
        var languageId = _context.SetupOnEnterLanguage(indentAction);
        using var editor = CreateEditor([text], languageId);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 7, false);
        AssertCursor(viewModel, S(1, 7, 1, 7));

        viewModel.Type("\n", Keyboard);
        editor.Model.GetValue(EndOfLinePreference.CRLF).ShouldBe(expected);
    }

    [Fact]
    public void Enter_Auto_Indents_With_InsertSpaces_Setting_1() =>
        EnterAutoIndentCase(IndentAction.Indent, "\thello", "\thello\r\n        ");

    [Fact]
    public void Enter_Auto_Indents_With_InsertSpaces_Setting_2() =>
        EnterAutoIndentCase(IndentAction.None, "\thello", "\thello\r\n    ");

    [Fact]
    public void Enter_Auto_Indents_With_InsertSpaces_Setting_3() =>
        EnterAutoIndentCase(IndentAction.IndentOutdent, "\thell()", "\thell(\r\n        \r\n    )");

    [Fact]
    public void Issue_148256_Pressing_Enter_Creates_Line_With_Bad_Indent_With_InsertSpaces_True()
    {
        using var editor = CreateEditor(["  \t"]);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 4, false);
        viewModel.Type("\n", Keyboard);
        editor.Model.GetValue().ShouldBe("  \t\n    ");
    }

    [Fact]
    public void Issue_148256_Pressing_Enter_Creates_Line_With_Bad_Indent_With_InsertSpaces_False()
    {
        using var editor = CreateEditor(["  \t"]);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        model.UpdateOptions(new TextModelUpdateOptions { InsertSpaces = false });
        MoveTo(viewModel, 1, 4, false);
        viewModel.Type("\n", Keyboard);
        model.GetValue().ShouldBe("  \t\n\t");
    }

    [Fact]
    public void RemoveAutoWhitespace_Off()
    {
        using var editor = CreateEditor(["    some  line abc  "], modelOpts: ModelOpts(trimAutoWhitespace: false));
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        // Move cursor to the end, verify that we do not trim whitespaces if line has values
        MoveTo(viewModel, 1, model.GetLineContent(1).Length + 1);
        viewModel.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    some  line abc  ");
        model.GetLineContent(2).ShouldBe("    ");

        // Try to enter again, we should trimmed previous line
        viewModel.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    some  line abc  ");
        model.GetLineContent(2).ShouldBe("    ");
        model.GetLineContent(3).ShouldBe("    ");
    }

    [Fact]
    public void RemoveAutoWhitespace_On_Removes_Only_Whitespace_The_Cursor_Added_1()
    {
        using var editor = CreateEditor(["    "]);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, model.GetLineContent(1).Length + 1);
        viewModel.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    ");
        model.GetLineContent(2).ShouldBe("    ");

        viewModel.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    ");
        model.GetLineContent(2).ShouldBe("");
        model.GetLineContent(3).ShouldBe("    ");
    }

    [Fact]
    public void Issue_115033_Indent_And_AppendText()
    {
        const string languageId = "onEnterMode";
        _context.Add(_context.LanguageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            OnEnterRules = [new OnEnterRule(JsRegExp.Of(".*"), new EnterAction(IndentAction.Indent) { AppendText = "x" })],
        }));
        using var editor = CreateEditor(["text"], languageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 5);
        viewModel.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("text");
        model.GetLineContent(2).ShouldBe("    x");
        AssertCursor(viewModel, P(2, 6));
    }

    [Fact]
    public void Issue_6862_Editor_Removes_Auto_Inserted_Indentation_When_Formatting_On_Type()
    {
        var languageId = _context.SetupOnEnterLanguage(IndentAction.IndentOutdent);
        using var editor = CreateEditor(["function foo (params: string) {}"], languageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 32);
        viewModel.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("function foo (params: string) {");
        model.GetLineContent(2).ShouldBe("    ");
        model.GetLineContent(3).ShouldBe("}");

        viewModel.ExecuteCommand(new Issue6862TestCommand(viewModel), "autoFormat");
        model.GetLineContent(1).ShouldBe("function foo(params: string) {");
        model.GetLineContent(2).ShouldBe("    ");
        model.GetLineContent(3).ShouldBe("}");
    }

    private sealed class Issue6862TestCommand(TestViewModel viewModel) : ICommand
    {
        private string? _selectionId;

        public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
        {
            builder.AddEditOperation(new TextRange(1, 13, 1, 14), "");
            _selectionId = builder.TrackSelection(viewModel.GetSelection());
        }

        public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper) =>
            helper.GetTrackedSelection(_selectionId!);
    }

    [Fact]
    public void RemoveAutoWhitespace_On_Removes_Only_Whitespace_The_Cursor_Added_2()
    {
        const string languageId = "testLang";
        using var editor = CreateEditor(["    if (a) {", "        ", "", "", "    }"], languageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 3, 1);
        Tab(editor);
        model.GetLineContent(1).ShouldBe("    if (a) {");
        model.GetLineContent(2).ShouldBe("        ");
        model.GetLineContent(3).ShouldBe("    ");
        model.GetLineContent(4).ShouldBe("");
        model.GetLineContent(5).ShouldBe("    }");

        MoveTo(viewModel, 4, 1);
        Tab(editor);
        model.GetLineContent(1).ShouldBe("    if (a) {");
        model.GetLineContent(2).ShouldBe("        ");
        model.GetLineContent(3).ShouldBe("");
        model.GetLineContent(4).ShouldBe("    ");
        model.GetLineContent(5).ShouldBe("    }");

        MoveTo(viewModel, 5, model.GetLineMaxColumn(5));
        viewModel.Type("something", Keyboard);
        model.GetLineContent(1).ShouldBe("    if (a) {");
        model.GetLineContent(2).ShouldBe("        ");
        model.GetLineContent(3).ShouldBe("");
        model.GetLineContent(4).ShouldBe("");
        model.GetLineContent(5).ShouldBe("    }something");
    }

    [Fact]
    public void RemoveAutoWhitespace_On_Test_1()
    {
        using var editor = CreateEditor(["    some  line abc  "]);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        // Move cursor to the end, verify that we do not trim whitespaces if line has values
        MoveTo(viewModel, 1, model.GetLineContent(1).Length + 1);
        viewModel.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    some  line abc  ");
        model.GetLineContent(2).ShouldBe("    ");

        // Try to enter again, we should trimmed previous line
        viewModel.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    some  line abc  ");
        model.GetLineContent(2).ShouldBe("");
        model.GetLineContent(3).ShouldBe("    ");

        // More whitespaces
        Tab(editor);
        model.GetLineContent(1).ShouldBe("    some  line abc  ");
        model.GetLineContent(2).ShouldBe("");
        model.GetLineContent(3).ShouldBe("        ");

        // Enter and verify that trimmed again
        viewModel.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    some  line abc  ");
        model.GetLineContent(2).ShouldBe("");
        model.GetLineContent(3).ShouldBe("");
        model.GetLineContent(4).ShouldBe("        ");

        // Trimmed if we will keep only text
        MoveTo(viewModel, 1, 5);
        viewModel.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    ");
        model.GetLineContent(2).ShouldBe("    some  line abc  ");
        model.GetLineContent(3).ShouldBe("");
        model.GetLineContent(4).ShouldBe("");
        model.GetLineContent(5).ShouldBe("");

        // Trimmed if we will keep only text by selection
        MoveTo(viewModel, 2, 5);
        MoveTo(viewModel, 3, 1, true);
        viewModel.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    ");
        model.GetLineContent(2).ShouldBe("    ");
        model.GetLineContent(3).ShouldBe("    ");
        model.GetLineContent(4).ShouldBe("");
        model.GetLineContent(5).ShouldBe("");
    }

    [Fact]
    public void Issue_15118_Remove_Auto_Whitespace_When_Pasting_Entire_Line()
    {
        using var editor = CreateEditor(["    function f() {", "        // I'm gonna copy this line", "        return 3;", "    }"]);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 3, model.GetLineMaxColumn(3));
        viewModel.Type("\n", Keyboard);

        model.GetValue().ShouldBe(string.Join('\n',
            "    function f() {",
            "        // I'm gonna copy this line",
            "        return 3;",
            "        ",
            "    }"));
        AssertCursor(viewModel, P(4, model.GetLineMaxColumn(4)));

        viewModel.Paste("        // I'm gonna copy this line\n", true);
        model.GetValue().ShouldBe(string.Join('\n',
            "    function f() {",
            "        // I'm gonna copy this line",
            "        return 3;",
            "        // I'm gonna copy this line",
            "",
            "    }"));
        AssertCursor(viewModel, P(5, 1));
    }

    [Fact]
    public void Issue_40695_Maintain_Cursor_Position_When_Copying_Lines_Using_Ctrl_C_Ctrl_V()
    {
        using var editor = CreateEditor(["    function f() {", "        // I'm gonna copy this line", "        // Another line", "        return 3;", "    }"]);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        editor.SetSelections([S(4, 10, 4, 10)]);
        viewModel.Paste("        // I'm gonna copy this line\n", true);

        model.GetValue().ShouldBe(string.Join('\n',
            "    function f() {",
            "        // I'm gonna copy this line",
            "        // Another line",
            "        // I'm gonna copy this line",
            "        return 3;",
            "    }"));
        AssertCursor(viewModel, P(5, 10));
    }

    [Fact]
    public void UseTabStops_Is_Off()
    {
        using var editor = CreateEditor(["    x", "        a    ", "    "], options: EditorOpts(EditorOptions.Default with { UseTabStops = false }));
        var viewModel = editor.ViewModel;

        // DeleteLeft removes just one whitespace
        MoveTo(viewModel, 2, 9);
        DeleteLeft(editor);
        editor.Model.GetLineContent(2).ShouldBe("       a    ");
    }

    [Fact]
    public async Task Backspace_Removes_Whitespaces_With_Tab_Size()
    {
        using var editor = CreateEditor([" \t \t     x", "        a    ", "    "], options: EditorOpts(EditorOptions.Default with { UseTabStops = true }));
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        // DeleteLeft does not remove tab size, because some text exists before
        MoveTo(viewModel, 2, model.GetLineContent(2).Length + 1);
        DeleteLeft(editor);
        model.GetLineContent(2).ShouldBe("        a   ");

        // DeleteLeft removes tab size = 4
        MoveTo(viewModel, 2, 9);
        DeleteLeft(editor);
        model.GetLineContent(2).ShouldBe("    a   ");

        // DeleteLeft removes tab size = 4
        DeleteLeft(editor);
        model.GetLineContent(2).ShouldBe("a   ");

        // Undo DeleteLeft - get us back to original indentation
        await editor.Undo();
        model.GetLineContent(2).ShouldBe("        a   ");

        // Nothing is broken when cursor is in (1,1)
        MoveTo(viewModel, 1, 1);
        DeleteLeft(editor);
        model.GetLineContent(1).ShouldBe(" \t \t     x");

        // DeleteLeft stops at tab stops even in mixed whitespace case
        MoveTo(viewModel, 1, 10);
        DeleteLeft(editor);
        model.GetLineContent(1).ShouldBe(" \t \t    x");

        DeleteLeft(editor);
        model.GetLineContent(1).ShouldBe(" \t \tx");

        DeleteLeft(editor);
        model.GetLineContent(1).ShouldBe(" \tx");

        DeleteLeft(editor);
        model.GetLineContent(1).ShouldBe("x");

        // DeleteLeft on last line
        MoveTo(viewModel, 3, model.GetLineContent(3).Length + 1);
        DeleteLeft(editor);
        model.GetLineContent(3).ShouldBe("");

        // DeleteLeft with removing new line symbol
        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("x\n        a   ");

        // In case of selection DeleteLeft only deletes selected text
        MoveTo(viewModel, 2, 3);
        MoveTo(viewModel, 2, 4, true);
        DeleteLeft(editor);
        model.GetLineContent(2).ShouldBe("       a   ");
    }

    [Fact]
    public async Task PR_5423_Auto_Indent_Undo_Redo_Is_Funky()
    {
        using var editor = CreateEditor([""], modelOpts: ModelOpts(insertSpaces: false));
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        viewModel.Type("\n", Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n", "assert1");

        Tab(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t", "assert2");

        viewModel.Type("y", Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\ty", "assert2");

        viewModel.Type("\n", Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\ty\n\t", "assert3");

        viewModel.Type("x");
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\ty\n\tx", "assert4");

        CoreNavigationCommands.CursorLeft.RunCoreEditorCommand(viewModel, new());
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\ty\n\tx", "assert5");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\ty\nx", "assert6");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\tyx", "assert7");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\tx", "assert8");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\nx", "assert9");

        DeleteLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("x", "assert10");

        await editor.Undo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\nx", "assert11");

        await editor.Undo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\ty\nx", "assert12");

        await editor.Undo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\ty\n\tx", "assert13");

        await editor.Redo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\ty\nx", "assert14");

        await editor.Redo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\nx", "assert15");

        await editor.Redo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("x", "assert16");
    }

    [Fact]
    public async Task Issue_90973_Undo_Brings_Back_Model_Alternative_Version()
    {
        using var editor = CreateEditor([""], modelOpts: ModelOpts(insertSpaces: false));
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        var beforeVersion = model.GetVersionId();
        var beforeAltVersion = model.GetAlternativeVersionId();
        viewModel.Type("Hello", Keyboard);
        await editor.Undo();
        var afterVersion = model.GetVersionId();
        var afterAltVersion = model.GetAlternativeVersionId();

        afterVersion.ShouldNotBe(beforeVersion);
        afterAltVersion.ShouldBe(beforeAltVersion);
    }
}
