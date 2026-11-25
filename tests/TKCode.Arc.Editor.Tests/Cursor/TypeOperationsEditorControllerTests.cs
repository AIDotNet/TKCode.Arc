// Tests for TypeOperations (typing, pasting, composing, Tab, outdent), ported from vscode
// (src/vs/editor/test/browser/controller/cursor.test.ts, suite 'Editor Controller', commit b57d5c9e), MIT license:
// the tests from 'issue microsoft/monaco-editor#443' to 'issue #90973' that type, paste, compose or press Tab.
// Upstream runs them on a test code editor; here they go through the operations on OperationsTestEditor, which does
// what the view model and cursors controller do with them (undo stops, auto-closed characters, cursor markers).
//
// Not ported (no typing, or ported elsewhere): 'issue #23539' (model EOL undo), 'bug #16740', 'issue #128602',
// 'issue #1140', 'issue #99629' (two), 'UseTabStops is off', 'Backspace removes whitespaces with tab size' (in
// CursorDeleteOperationsTests), 'issue #84897' and 'issue #122914' (delete only), 'issue #832', 'issue #33788',
// 'issue #12887', 'Double-click on punctuation ...' (word selection), 'issue #46314' (two editors, tokenization events).
// Not ported (need the view model's wrapping): 'issue #36740', 'issue #110376', 'issue #98320', 'issue #41573',
// 'issue #22717' (wrapping font metrics), 'issue #112301', 'move right normalizes the cursor ...', 'issue #123178'.

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TypeOperationsEditorControllerTests : IDisposable
{
    private const string Keyboard = OperationsTestEditor.Keyboard;

    private readonly TypingTestContext _context = new();

    public void Dispose() => _context.Dispose();

    private static TextSelection S(int selectionStartLineNumber, int selectionStartColumn, int positionLineNumber, int positionColumn) =>
        new(selectionStartLineNumber, selectionStartColumn, positionLineNumber, positionColumn);

    private static TextPosition P(int lineNumber, int column) => new(lineNumber, column);

    private static TextModelCreationOptions ModelOpts(bool? insertSpaces = null, int? tabSize = null, int? indentSize = null, bool? trimAutoWhitespace = null) =>
        TypingTestContext.ModelOpts(insertSpaces, tabSize, indentSize, trimAutoWhitespace);

    // CoreNavigationCommands.CursorLeft / CursorRight / CursorDown
    private static void MoveLeft(OperationsTestEditor editor, bool inSelectionMode = false) =>
        editor.Move((config, model, cursor) => MoveOperations.MoveLeft(config, model, cursor, inSelectionMode, 1));

    private static void MoveRight(OperationsTestEditor editor, bool inSelectionMode = false) =>
        editor.Move((config, model, cursor) => MoveOperations.MoveRight(config, model, cursor, inSelectionMode, 1));

    [Fact]
    public void Issue_Monaco_Editor_443_Indentation_Of_A_Single_Row_Deletes_Selected_Text_In_Some_Cases()
    {
        using var editor = _context.CreateEditor(string.Join('\n', "Hello world!", "another line"), modelOpts: ModelOpts(insertSpaces: false));
        editor.SetSelectionsValidated(S(1, 1, 1, 13));

        // Check that indenting maintains the selection start at column 1
        editor.Tab();
        editor.GetSelection().ShouldBe(S(1, 1, 1, 14));
    }

    [Fact]
    public async Task Bug_9121_Auto_Indent_Undo_Redo_Is_Funky()
    {
        using var editor = _context.CreateEditor("", modelOpts: ModelOpts(insertSpaces: false, trimAutoWhitespace: false));
        var model = editor.Model;

        editor.Type("\n", Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n", "assert1");

        editor.Tab();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t", "assert2");

        editor.Type("\n", Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t\n\t", "assert3");

        editor.Type("x");
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t\n\tx", "assert4");

        MoveLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t\n\tx", "assert5");

        editor.DeleteLeft();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t\nx", "assert6");

        editor.DeleteLeft();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\tx", "assert7");

        editor.DeleteLeft();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\nx", "assert8");

        editor.DeleteLeft();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("x", "assert9");

        await editor.Undo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\nx", "assert10");

        await editor.Undo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t\nx", "assert11");

        await editor.Undo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t\n\tx", "assert12");

        await editor.Redo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t\nx", "assert13");

        await editor.Redo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\nx", "assert14");

        await editor.Redo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("x", "assert15");
    }

    [Fact]
    public async Task Issue_47733_Undo_Mangles_Unicode_Characters()
    {
        const string languageId = "myMode";
        _context.Add(_context.LanguageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            SurroundingPairs = [new AutoClosingPair("%", "%")],
        }));

        using var editor = _context.CreateEditor("'👁'", languageId);
        var model = editor.Model;
        editor.SetSelectionsValidated(S(1, 1, 1, 2));

        editor.Type("%", Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("%'%👁'", "assert1");

        await editor.Undo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("'👁'", "assert2");
    }

    [Fact]
    public async Task Issue_46208_Allow_Empty_Selections_In_The_Undo_Redo_Stack()
    {
        using var editor = _context.CreateEditor("");
        var model = editor.Model;

        editor.Type("Hello", Keyboard);
        editor.Type(" ", Keyboard);
        editor.Type("world", Keyboard);
        editor.Type(" ", Keyboard);
        model.GetLineContent(1).ShouldBe("Hello world ");
        editor.AssertCursor(P(1, 13));

        MoveLeft(editor);
        MoveRight(editor);

        model.PushEditOperations([], [EditOperation.ReplaceMove(new TextRange(1, 12, 1, 13), "")], _ => []);
        model.GetLineContent(1).ShouldBe("Hello world");
        editor.AssertCursor(P(1, 12));

        await editor.Undo();
        model.GetLineContent(1).ShouldBe("Hello world ");
        editor.AssertCursor(S(1, 13, 1, 13));

        await editor.Undo();
        model.GetLineContent(1).ShouldBe("Hello world");
        editor.AssertCursor(P(1, 12));

        await editor.Undo();
        model.GetLineContent(1).ShouldBe("Hello");
        editor.AssertCursor(P(1, 6));

        await editor.Undo();
        model.GetLineContent(1).ShouldBe("");
        editor.AssertCursor(P(1, 1));

        await editor.Redo();
        model.GetLineContent(1).ShouldBe("Hello");
        editor.AssertCursor(P(1, 6));

        await editor.Redo();
        model.GetLineContent(1).ShouldBe("Hello world");
        editor.AssertCursor(P(1, 12));

        await editor.Redo();
        model.GetLineContent(1).ShouldBe("Hello world ");
        editor.AssertCursor(P(1, 13));

        await editor.Redo();
        model.GetLineContent(1).ShouldBe("Hello world");
        editor.AssertCursor(P(1, 12));

        await editor.Redo();
        model.GetLineContent(1).ShouldBe("Hello world");
        editor.AssertCursor(P(1, 12));
    }

    [Fact]
    public void Bug_16815_Shift_Tab_Doesnt_Go_Back_To_Tabstop()
    {
        var languageId = _context.SetupOnEnterLanguage(IndentAction.IndentOutdent);
        using var editor = _context.CreateEditor("     function baz() {", languageId);
        var model = editor.Model;

        editor.MoveTo(1, 6, false);
        editor.AssertCursor(S(1, 6, 1, 6));

        editor.Outdent();
        model.GetLineContent(1).ShouldBe("    function baz() {");
        editor.AssertCursor(S(1, 5, 1, 5));
    }

    [Fact]
    public void Bug_18293_Regression_Editor_Cant_Outdent_Whitespace_Line()
    {
        using var editor = _context.CreateEditor("      ");
        var model = editor.Model;

        editor.MoveTo(1, 7, false);
        editor.AssertCursor(S(1, 7, 1, 7));

        editor.Outdent();
        model.GetLineContent(1).ShouldBe("    ");
        editor.AssertCursor(S(1, 5, 1, 5));
    }

    [Fact]
    public void Issue_95591_Unindenting_Moves_Cursor_To_Beginning_Of_Line()
    {
        using var editor = _context.CreateEditor("        ", editorOpts: EditorOptions.Default with { UseTabStops = false });
        var model = editor.Model;

        editor.MoveTo(1, 9, false);
        editor.AssertCursor(S(1, 9, 1, 9));

        editor.Outdent();
        model.GetLineContent(1).ShouldBe("    ");
        editor.AssertCursor(S(1, 5, 1, 5));
    }

    [Fact]
    public void Bug_16657_Editor_Tab_On_Empty_Line_Of_Zero_Indentation_Moves_Cursor_To_Position_1_1()
    {
        using var editor = _context.CreateEditor(
            string.Join('\n', "function baz() {", "\tfunction hello() { // something here", "\t", "", "\t}", "}", ""),
            modelOpts: ModelOpts(insertSpaces: false));
        var model = editor.Model;

        editor.MoveTo(7, 1, false);
        editor.AssertCursor(S(7, 1, 7, 1));

        editor.Tab();
        model.GetLineContent(7).ShouldBe("\t");
        editor.AssertCursor(S(7, 2, 7, 2));
    }

    [Fact]
    public void Bug_11476_Double_Bracket_Surrounding_Undo_Is_Broken()
    {
        using var editor = _context.UsingCursor(["hello"], TypingTestContext.SurroundingLanguageId);

        editor.MoveTo(1, 3, false);
        editor.MoveTo(1, 5, true);
        editor.AssertCursor(S(1, 3, 1, 5));

        editor.Type("(", Keyboard);
        editor.AssertCursor(S(1, 4, 1, 6));

        editor.Type("(", Keyboard);
        editor.AssertCursor(S(1, 5, 1, 7));
    }

    [Fact]
    public void Issue_206774_SurroundSelectionCommand_With_Empty_CharAfterSelection_Should_Not_Throw()
    {
        using var editor = _context.UsingCursor(["hello world"], TypingTestContext.EmptyClosingSurroundLanguageId);
        var model = editor.Model;

        // Select "hello"
        editor.MoveTo(1, 1, false);
        editor.MoveTo(1, 6, true);
        editor.AssertCursor(S(1, 1, 1, 6));

        // Type < which should surround with '<' and empty string
        editor.Type("<", Keyboard);

        model.GetValue().ShouldBe("<hello world");
    }

    [Fact]
    public void Issue_10212_Pasting_Entire_Line_Does_Not_Replace_Selection()
    {
        using var editor = _context.UsingCursor(["line1", "line2"]);
        var model = editor.Model;

        editor.MoveTo(2, 1, false);
        editor.MoveTo(2, 6, true);

        editor.Paste("line1\n", true);

        model.GetLineContent(1).ShouldBe("line1");
        model.GetLineContent(2).ShouldBe("line1");
        model.GetLineContent(3).ShouldBe("");
    }

    [Fact]
    public void Issue_74722_Pasting_Whole_Line_Does_Not_Replace_Selection()
    {
        using var editor = _context.UsingCursor(["line1", "line sel 2", "line3"]);
        var model = editor.Model;

        editor.SetSelectionsValidated(S(2, 6, 2, 9));

        editor.Paste("line1\n", true);

        model.GetLineContent(1).ShouldBe("line1");
        model.GetLineContent(2).ShouldBe("line line1");
        model.GetLineContent(3).ShouldBe(" 2");
        model.GetLineContent(4).ShouldBe("line3");
    }

    [Fact]
    public void Issue_4996_Multiple_Cursor_Paste_Pastes_Contents_Of_All_Cursors()
    {
        using var editor = _context.UsingCursor(["line1", "line2", "line3"]);
        var model = editor.Model;

        editor.SetSelectionsValidated(S(1, 1, 1, 1), S(2, 1, 2, 1));

        editor.Paste("a\nb\nc\nd", false, ["a\nb", "c\nd"]);

        model.GetValue().ShouldBe(string.Join('\n', "a", "bline1", "c", "dline2", "line3"));
    }

    public static TheoryData<string, string[], TextSelection[], string, string[]> MultiCursorPasteCases => new()
    {
        {
            "issue #16155: Paste into multiple cursors has edge case when number of lines equals number of cursors - 1",
            ["test", "test", "test", "test"],
            [S(1, 1, 1, 5), S(2, 1, 2, 5), S(3, 1, 3, 5), S(4, 1, 4, 5)],
            "aaa\nbbb\nccc\n",
            ["aaa", "bbb", "ccc", "", "aaa", "bbb", "ccc", "", "aaa", "bbb", "ccc", "", "aaa", "bbb", "ccc", ""]
        },
        {
            "issue #43722: Multiline paste doesn't work anymore",
            ["test", "test", "test", "test"],
            [S(1, 1, 1, 5), S(2, 1, 2, 5), S(3, 1, 3, 5), S(4, 1, 4, 5)],
            "aaa\r\nbbb\r\nccc\r\nddd\r\n",
            ["aaa", "bbb", "ccc", "ddd"]
        },
        {
            "issue #46440: (1) Pasting a multi-line selection pastes entire selection into every insertion point",
            ["line1", "line2", "line3"],
            [S(1, 1, 1, 1), S(2, 1, 2, 1), S(3, 1, 3, 1)],
            "a\nb\nc",
            ["aline1", "bline2", "cline3"]
        },
        {
            "issue #46440: (2) Pasting a multi-line selection pastes entire selection into every insertion point",
            ["line1", "line2", "line3"],
            [S(1, 1, 1, 1), S(2, 1, 2, 1), S(3, 1, 3, 1)],
            "a\nb\nc\n",
            ["aline1", "bline2", "cline3"]
        },
    };

    [Theory]
    [MemberData(nameof(MultiCursorPasteCases))]
    public void Multi_Cursor_Paste_Cases(string name, string[] text, TextSelection[] selections, string pastedText, string[] expected)
    {
        _ = name;
        using var editor = _context.UsingCursor(text);

        editor.SetSelectionsValidated(selections);

        editor.Paste(pastedText, false, null);

        editor.Model.GetValue().ShouldBe(string.Join('\n', expected));
    }

    [Fact]
    public void Issue_256039_Paste_From_Multiple_Cursors_With_Empty_Selections_And_MultiCursorPaste_Full()
    {
        using var editor = _context.UsingCursor(["line1", "line2", "line3"], editorOpts: EditorOptions.Default with { MultiCursorPaste = MultiCursorPaste.Full });

        // 2 cursors on lines 1 and 2
        editor.SetSelectionsValidated(S(1, 1, 1, 1), S(2, 1, 2, 1));

        editor.Paste("line1\nline2\n", true, ["line1\n", "line2\n"]);

        // Each cursor gets its respective line
        editor.Model.GetValue().ShouldBe(string.Join('\n', "line1", "line1", "line2", "line2", "line3"));
    }

    [Fact]
    public async Task Issue_3071_Investigate_Why_Undo_Stack_Gets_Corrupted()
    {
        using var editor = _context.CreateEditor(string.Join('\n', "some lines", "and more lines", "just some text"));
        var model = editor.Model;

        editor.MoveTo(1, 1, false);
        editor.MoveTo(3, 4, true);

        var isFirst = true;
        model.DidChangeContent += _ =>
        {
            if (isFirst)
            {
                isFirst = false;
                editor.Type("\t", Keyboard);
            }
        };

        editor.Tab();
        model.GetValue().ShouldBe("\t just some text", "001");

        await editor.Undo();
        model.GetValue().ShouldBe(string.Join('\n', "    some lines", "    and more lines", "    just some text"), "002");

        await editor.Undo();
        model.GetValue().ShouldBe(string.Join('\n', "some lines", "and more lines", "just some text"), "003");

        await editor.Undo();
        model.GetValue().ShouldBe(string.Join('\n', "some lines", "and more lines", "just some text"), "004");
    }

    [Fact]
    public void Issue_12950_Cannot_Double_Click_To_Insert_Emoji_Using_OSX_Emoji_Panel()
    {
        using var editor = _context.UsingCursor(["some lines", "and more lines", "just some text"], languageId: null);

        editor.MoveTo(3, 1, false);

        editor.Type("😍", Keyboard);

        editor.Model.GetValue().ShouldBe(string.Join('\n', "some lines", "and more lines", "😍just some text"));
    }

    [Fact]
    public void Issue_3463_Pressing_Tab_Adds_Spaces_But_Not_As_Many_As_For_A_Tab()
    {
        using var editor = _context.CreateEditor(string.Join('\n', "function a() {", "\tvar a = {", "\t\tx: 3", "\t};", "}"));

        editor.MoveTo(3, 2, false);
        editor.Tab();
        editor.Model.GetLineContent(3).ShouldBe("\t    \tx: 3");
    }

    [Fact]
    public void Issue_4312_Trying_To_Type_A_Tab_Character_Over_A_Sequence_Of_Spaces_Results_In_Unexpected_Behaviour()
    {
        using var editor = _context.CreateEditor(
            string.Join('\n', "var foo = 123;       // this is a comment", "var bar = 4;       // another comment"),
            modelOpts: ModelOpts(insertSpaces: false));

        editor.MoveTo(1, 15, false);
        editor.MoveTo(1, 22, true);
        editor.Tab();
        editor.Model.GetLineContent(1).ShouldBe("var foo = 123;\t// this is a comment");
    }

    [Fact]
    public async Task Issue_9675_Undo_Redo_Adds_A_Stop_In_Between_CHN_Characters()
    {
        using var editor = _context.UsingCursor([]);
        var model = editor.Model;
        editor.AssertCursor(P(1, 1));

        // Typing sennsei in Japanese - Hiragana
        editor.Type("ｓ", Keyboard);
        editor.CompositionType("せ", 1, 0, 0);
        editor.CompositionType("せｎ", 1, 0, 0);
        editor.CompositionType("せん", 2, 0, 0);
        editor.CompositionType("せんｓ", 2, 0, 0);
        editor.CompositionType("せんせ", 3, 0, 0);
        editor.CompositionType("せんせ", 3, 0, 0);
        editor.CompositionType("せんせい", 3, 0, 0);
        editor.CompositionType("せんせい", 4, 0, 0);
        editor.CompositionType("せんせい", 4, 0, 0);
        editor.CompositionType("せんせい", 4, 0, 0);

        model.GetLineContent(1).ShouldBe("せんせい");
        editor.AssertCursor(P(1, 5));

        await editor.Undo();
        model.GetLineContent(1).ShouldBe("");
        editor.AssertCursor(P(1, 1));
    }

    [Fact]
    public void Issue_23983_Calling_Model_SetEOL_Does_Not_Reset_Cursor_Position()
    {
        using var editor = _context.UsingCursor(["first line", "second line"]);
        var model = editor.Model;
        model.SetEOL(EndOfLineSequence.CRLF);

        editor.SetSelectionsValidated(S(2, 2, 2, 2));
        model.SetEOL(EndOfLineSequence.LF);

        editor.AssertCursor(S(2, 2, 2, 2));
    }

    [Fact]
    public void Issue_23983_Calling_Model_SetValue_Resets_Cursor_Position()
    {
        using var editor = _context.UsingCursor(["first line", "second line"]);
        var model = editor.Model;
        model.SetEOL(EndOfLineSequence.CRLF);

        editor.SetSelectionsValidated(S(2, 2, 2, 2));
        model.SetValue(string.Join('\n', "different first line", "different second line", "new third line"));

        editor.AssertCursor(S(1, 1, 1, 1));
    }

    [Fact]
    public async Task Issue_44805_Should_Not_Be_Able_To_Undo_In_Readonly_Editor()
    {
        using var editor = _context.CreateEditor("", editorOpts: EditorOptions.Default with { ReadOnly = true });
        var model = editor.Model;

        model.PushEditOperations([S(1, 1, 1, 1)], [new SingleEditOperation(new TextRange(1, 1, 1, 1), "Hello world!")], _ => [S(1, 1, 1, 1)]);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("Hello world!");

        await editor.Undo();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("Hello world!");
    }

    [Fact]
    public void Issue_37967_Problem_Replacing_Consecutive_Characters()
    {
        using var editor = _context.CreateEditor(
            string.Join('\n', "const a = \"foo\";", "const b = \"\""),
            editorOpts: EditorOptions.Default with { MultiCursorMergeOverlapping = false });
        var model = editor.Model;

        editor.SetSelectionsValidated(
            S(1, 12, 1, 12),
            S(1, 16, 1, 16),
            S(2, 12, 2, 12),
            S(2, 13, 2, 13));

        editor.DeleteLeft();

        editor.AssertCursor(
            S(1, 11, 1, 11),
            S(1, 14, 1, 14),
            S(2, 11, 2, 11),
            S(2, 11, 2, 11));

        editor.Type("'", Keyboard);

        model.GetLineContent(1).ShouldBe("const a = 'foo';");
        model.GetLineContent(2).ShouldBe("const b = ''");
    }

    [Fact]
    public async Task Issue_15761_Cursor_Doesnt_Move_In_A_Redo_Operation()
    {
        using var editor = _context.CreateEditor("hello");

        editor.SetSelectionsValidated(S(1, 4, 1, 4));

        editor.ExecuteEdits("test", [new SingleEditOperation(new TextRange(1, 1, 1, 1), "*", ForceMoveMarkers: true)]);
        editor.AssertCursor(S(1, 5, 1, 5));

        await editor.Undo();
        editor.AssertCursor(S(1, 4, 1, 4));

        await editor.Redo();
        editor.AssertCursor(S(1, 5, 1, 5));
    }

    [Fact]
    public async Task Issue_42783_API_Calls_With_Undo_Leave_Cursor_In_Wrong_Position()
    {
        using var editor = _context.CreateEditor("ab");

        editor.SetSelectionsValidated(S(1, 1, 1, 1));

        editor.ExecuteEdits("test", [new SingleEditOperation(new TextRange(1, 1, 1, 3), "")]);
        editor.AssertCursor(S(1, 1, 1, 1));

        await editor.Undo();
        editor.AssertCursor(S(1, 1, 1, 1));

        editor.ExecuteEdits("test", [new SingleEditOperation(new TextRange(1, 1, 1, 2), "")]);
        editor.AssertCursor(S(1, 1, 1, 1));
    }

    [Fact]
    public void Issue_85712_Paste_Line_Moves_Cursor_To_Start_Of_Current_Line_Rather_Than_Start_Of_Next_Line()
    {
        using var editor = _context.CreateEditor(string.Join('\n', "abc123", ""));

        editor.SetSelectionsValidated(S(2, 1, 2, 1));
        editor.Paste("something\n", true);
        editor.Model.GetValue().ShouldBe(string.Join('\n', "abc123", "something", ""));
        editor.AssertCursor(P(3, 1));
    }

    [Fact]
    public void Cursor_Honors_InsertSpaces_Configuration_On_New_Line()
    {
        using var editor = _context.UsingCursor(["    \tMy First Line\t ", "\tMy Second Line", "    Third Line", "", "1"]);
        var model = editor.Model;

        editor.MoveTo(1, 21);
        editor.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    \tMy First Line\t ");
        model.GetLineContent(2).ShouldBe("        ");
    }

    [Fact]
    public async Task Cursor_Honors_InsertSpaces_Configuration_On_Tab()
    {
        using var editor = _context.CreateEditor(
            string.Join('\n', "    \tMy First Line\t ", "My Second Line123", "    Third Line", "", "1"),
            modelOpts: ModelOpts(tabSize: 13, indentSize: 13));
        var model = editor.Model;

        // Tab on column 1
        editor.MoveTo(2, 1);
        editor.Tab();
        model.GetLineContent(2).ShouldBe("             My Second Line123");
        await editor.Undo();

        // Tab on column 2
        model.GetLineContent(2).ShouldBe("My Second Line123");
        editor.MoveTo(2, 2);
        editor.Tab();
        model.GetLineContent(2).ShouldBe("M            y Second Line123");
        await editor.Undo();

        // Tab on column 3
        model.GetLineContent(2).ShouldBe("My Second Line123");
        editor.MoveTo(2, 3);
        editor.Tab();
        model.GetLineContent(2).ShouldBe("My            Second Line123");
        await editor.Undo();

        // Tab on column 4
        model.GetLineContent(2).ShouldBe("My Second Line123");
        editor.MoveTo(2, 4);
        editor.Tab();
        model.GetLineContent(2).ShouldBe("My           Second Line123");
        await editor.Undo();

        // Tab on column 5
        model.GetLineContent(2).ShouldBe("My Second Line123");
        editor.MoveTo(2, 5);
        editor.Tab();
        model.GetLineContent(2).ShouldBe("My S         econd Line123");
        await editor.Undo();

        // Tab on column 5
        model.GetLineContent(2).ShouldBe("My Second Line123");
        editor.MoveTo(2, 5);
        editor.Tab();
        model.GetLineContent(2).ShouldBe("My S         econd Line123");
        await editor.Undo();

        // Tab on column 13
        model.GetLineContent(2).ShouldBe("My Second Line123");
        editor.MoveTo(2, 13);
        editor.Tab();
        model.GetLineContent(2).ShouldBe("My Second Li ne123");
        await editor.Undo();

        // Tab on column 14
        model.GetLineContent(2).ShouldBe("My Second Line123");
        editor.MoveTo(2, 14);
        editor.Tab();
        model.GetLineContent(2).ShouldBe("My Second Lin             e123");
    }

    // The indent action is an int: IndentAction is internal and a theory's parameters are public.
    public static TheoryData<string, int, string, string> EnterAutoIndentCases => new()
    {
        { "Enter auto-indents with insertSpaces setting 1", (int)IndentAction.Indent, "\thello", "\thello\r\n        " },
        { "Enter auto-indents with insertSpaces setting 2", (int)IndentAction.None, "\thello", "\thello\r\n    " },
        { "Enter auto-indents with insertSpaces setting 3", (int)IndentAction.IndentOutdent, "\thell()", "\thell(\r\n        \r\n    )" },
    };

    [Theory]
    [MemberData(nameof(EnterAutoIndentCases))]
    public void Enter_Auto_Indents_With_InsertSpaces_Setting(string name, int indentAction, string text, string expected)
    {
        _ = name;
        var languageId = _context.SetupOnEnterLanguage((IndentAction)indentAction);
        using var editor = _context.UsingCursor([text], languageId);

        editor.MoveTo(1, 7, false);
        editor.AssertCursor(S(1, 7, 1, 7));

        editor.Type("\n", Keyboard);
        editor.Model.GetValue(EndOfLinePreference.CRLF).ShouldBe(expected);
    }

    [Fact]
    public void Issue_148256_Pressing_Enter_Creates_Line_With_Bad_Indent_With_InsertSpaces_True()
    {
        using var editor = _context.UsingCursor(["  \t"]);

        editor.MoveTo(1, 4, false);
        editor.Type("\n", Keyboard);
        editor.Model.GetValue().ShouldBe("  \t\n    ");
    }

    [Fact]
    public void Issue_148256_Pressing_Enter_Creates_Line_With_Bad_Indent_With_InsertSpaces_False()
    {
        using var editor = _context.UsingCursor(["  \t"]);
        var model = editor.Model;

        model.UpdateOptions(new TextModelUpdateOptions { InsertSpaces = false });
        editor.MoveTo(1, 4, false);
        editor.Type("\n", Keyboard);
        model.GetValue().ShouldBe("  \t\n\t");
    }

    [Fact]
    public void RemoveAutoWhitespace_Off()
    {
        using var editor = _context.UsingCursor(["    some  line abc  "], modelOpts: ModelOpts(trimAutoWhitespace: false));
        var model = editor.Model;

        // Move cursor to the end, verify that we do not trim whitespaces if line has values
        editor.MoveTo(1, model.GetLineContent(1).Length + 1);
        editor.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    some  line abc  ");
        model.GetLineContent(2).ShouldBe("    ");

        // Try to enter again, we should trimmed previous line
        editor.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    some  line abc  ");
        model.GetLineContent(2).ShouldBe("    ");
        model.GetLineContent(3).ShouldBe("    ");
    }

    [Fact]
    public void RemoveAutoWhitespace_On_Removes_Only_Whitespace_The_Cursor_Added_1()
    {
        using var editor = _context.UsingCursor(["    "]);
        var model = editor.Model;

        editor.MoveTo(1, model.GetLineContent(1).Length + 1);
        editor.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    ");
        model.GetLineContent(2).ShouldBe("    ");

        editor.Type("\n", Keyboard);
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
        using var editor = _context.UsingCursor(["text"], languageId);
        var model = editor.Model;

        editor.MoveTo(1, 5);
        editor.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("text");
        model.GetLineContent(2).ShouldBe("    x");
        editor.AssertCursor(P(2, 6));
    }

    [Fact]
    public void Issue_6862_Editor_Removes_Auto_Inserted_Indentation_When_Formatting_On_Type()
    {
        var languageId = _context.SetupOnEnterLanguage(IndentAction.IndentOutdent);
        using var editor = _context.UsingCursor(["function foo (params: string) {}"], languageId);
        var model = editor.Model;

        editor.MoveTo(1, 32);
        editor.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("function foo (params: string) {");
        model.GetLineContent(2).ShouldBe("    ");
        model.GetLineContent(3).ShouldBe("}");

        editor.ExecuteCommand(new TrackSelectionTestCommand(editor.GetSelection()));
        model.GetLineContent(1).ShouldBe("function foo(params: string) {");
        model.GetLineContent(2).ShouldBe("    ");
        model.GetLineContent(3).ShouldBe("}");
    }

    private sealed class TrackSelectionTestCommand : ICommand
    {
        private readonly TextSelection _selection;
        private string? _selectionId;

        public TrackSelectionTestCommand(TextSelection selection)
        {
            _selection = selection;
        }

        public void GetEditOperations(TextModel model, IEditOperationBuilder builder)
        {
            builder.AddEditOperation(new TextRange(1, 13, 1, 14), "");
            _selectionId = builder.TrackSelection(_selection);
        }

        public TextSelection ComputeCursorState(TextModel model, ICursorStateComputerData helper) => helper.GetTrackedSelection(_selectionId!);
    }

    [Fact]
    public void RemoveAutoWhitespace_On_Removes_Only_Whitespace_The_Cursor_Added_2()
    {
        using var editor = _context.CreateEditor(string.Join('\n', "    if (a) {", "        ", "", "", "    }"), "testLang");
        var model = editor.Model;

        editor.MoveTo(3, 1);
        editor.Tab();
        model.GetLineContent(1).ShouldBe("    if (a) {");
        model.GetLineContent(2).ShouldBe("        ");
        model.GetLineContent(3).ShouldBe("    ");
        model.GetLineContent(4).ShouldBe("");
        model.GetLineContent(5).ShouldBe("    }");

        editor.MoveTo(4, 1);
        editor.Tab();
        model.GetLineContent(1).ShouldBe("    if (a) {");
        model.GetLineContent(2).ShouldBe("        ");
        model.GetLineContent(3).ShouldBe("");
        model.GetLineContent(4).ShouldBe("    ");
        model.GetLineContent(5).ShouldBe("    }");

        editor.MoveTo(5, model.GetLineMaxColumn(5));
        editor.Type("something", Keyboard);
        model.GetLineContent(1).ShouldBe("    if (a) {");
        model.GetLineContent(2).ShouldBe("        ");
        model.GetLineContent(3).ShouldBe("");
        model.GetLineContent(4).ShouldBe("");
        model.GetLineContent(5).ShouldBe("    }something");
    }

    [Fact]
    public void RemoveAutoWhitespace_On_Test_1()
    {
        using var editor = _context.CreateEditor("    some  line abc  ");
        var model = editor.Model;

        // Move cursor to the end, verify that we do not trim whitespaces if line has values
        editor.MoveTo(1, model.GetLineContent(1).Length + 1);
        editor.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    some  line abc  ");
        model.GetLineContent(2).ShouldBe("    ");

        // Try to enter again, we should trimmed previous line
        editor.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    some  line abc  ");
        model.GetLineContent(2).ShouldBe("");
        model.GetLineContent(3).ShouldBe("    ");

        // More whitespaces
        editor.Tab();
        model.GetLineContent(1).ShouldBe("    some  line abc  ");
        model.GetLineContent(2).ShouldBe("");
        model.GetLineContent(3).ShouldBe("        ");

        // Enter and verify that trimmed again
        editor.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    some  line abc  ");
        model.GetLineContent(2).ShouldBe("");
        model.GetLineContent(3).ShouldBe("");
        model.GetLineContent(4).ShouldBe("        ");

        // Trimmed if we will keep only text
        editor.MoveTo(1, 5);
        editor.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    ");
        model.GetLineContent(2).ShouldBe("    some  line abc  ");
        model.GetLineContent(3).ShouldBe("");
        model.GetLineContent(4).ShouldBe("");
        model.GetLineContent(5).ShouldBe("");

        // Trimmed if we will keep only text by selection
        editor.MoveTo(2, 5);
        editor.MoveTo(3, 1, true);
        editor.Type("\n", Keyboard);
        model.GetLineContent(1).ShouldBe("    ");
        model.GetLineContent(2).ShouldBe("    ");
        model.GetLineContent(3).ShouldBe("    ");
        model.GetLineContent(4).ShouldBe("");
        model.GetLineContent(5).ShouldBe("");
    }

    [Fact]
    public void Issue_15118_Remove_Auto_Whitespace_When_Pasting_Entire_Line()
    {
        using var editor = _context.CreateEditor(string.Join('\n', "    function f() {", "        // I'm gonna copy this line", "        return 3;", "    }"));
        var model = editor.Model;

        editor.MoveTo(3, model.GetLineMaxColumn(3));
        editor.Type("\n", Keyboard);

        model.GetValue().ShouldBe(string.Join('\n', "    function f() {", "        // I'm gonna copy this line", "        return 3;", "        ", "    }"));
        editor.AssertCursor(P(4, model.GetLineMaxColumn(4)));

        editor.Paste("        // I'm gonna copy this line\n", true);
        model.GetValue().ShouldBe(string.Join('\n', "    function f() {", "        // I'm gonna copy this line", "        return 3;", "        // I'm gonna copy this line", "", "    }"));
        editor.AssertCursor(P(5, 1));
    }

    [Fact]
    public void Issue_40695_Maintain_Cursor_Position_When_Copying_Lines_Using_Ctrl_C_Ctrl_V()
    {
        using var editor = _context.CreateEditor(string.Join('\n', "    function f() {", "        // I'm gonna copy this line", "        // Another line", "        return 3;", "    }"));
        var model = editor.Model;

        editor.SetSelectionsValidated(S(4, 10, 4, 10));
        editor.Paste("        // I'm gonna copy this line\n", true);

        model.GetValue().ShouldBe(string.Join('\n', "    function f() {", "        // I'm gonna copy this line", "        // Another line", "        // I'm gonna copy this line", "        return 3;", "    }"));
        editor.AssertCursor(P(5, 10));
    }

    [Fact]
    public async Task PR_5423_Auto_Indent_Undo_Redo_Is_Funky()
    {
        using var editor = _context.CreateEditor("", modelOpts: ModelOpts(insertSpaces: false));
        var model = editor.Model;

        editor.Type("\n", Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n", "assert1");

        editor.Tab();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\t", "assert2");

        editor.Type("y", Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\ty", "assert2");

        editor.Type("\n", Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\ty\n\t", "assert3");

        editor.Type("x");
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\ty\n\tx", "assert4");

        MoveLeft(editor);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\ty\n\tx", "assert5");

        editor.DeleteLeft();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\ty\nx", "assert6");

        editor.DeleteLeft();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\tyx", "assert7");

        editor.DeleteLeft();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\n\tx", "assert8");

        editor.DeleteLeft();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("\nx", "assert9");

        editor.DeleteLeft();
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
        using var editor = _context.CreateEditor("", modelOpts: ModelOpts(insertSpaces: false));
        var model = editor.Model;

        var beforeVersion = model.GetVersionId();
        var beforeAltVersion = model.GetAlternativeVersionId();
        editor.Type("Hello", Keyboard);
        await editor.Undo();
        var afterVersion = model.GetVersionId();
        var afterAltVersion = model.GetAlternativeVersionId();

        afterVersion.ShouldNotBe(beforeVersion);
        afterAltVersion.ShouldBe(beforeAltVersion);
    }
}
