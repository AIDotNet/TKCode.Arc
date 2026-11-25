// Tests for typing over auto-closed characters, auto-closing on composition end and surrounding with quotes, ported
// from vscode (src/vs/editor/test/browser/controller/cursor.test.ts, suite 'Editor Controller', commit b57d5c9e),
// MIT license: the tests from 'issue #25658' to 'issue #41825'. Upstream types through the view model of a test code
// editor; here the typing goes through TypeOperations on OperationsTestEditor, which tracks the auto-closed characters
// and the composition state as the cursors controller does.
//
// Not ported: 'All cursors should do the same thing when deleting left' (in CursorDeleteOperationsTests),
// 'issue #7100', 'issue #112039', 'issue #158236' (word and line selection with the mouse), 'issue #111513' (two
// editors on one model: needs a second view model of the same model).
// Adapted: editor.trigger('keyboard', 'type', { text }) is Type(text, "keyboard"), which is what it runs.

using Shouldly;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;
using static TKCode.Arc.Editor.Tests.TypingTestContext;

namespace TKCode.Arc.Editor.Tests;

public sealed class TypeOperationsAutoClosingOvertypeTests : IDisposable
{
    private const string Keyboard = OperationsTestEditor.Keyboard;

    private readonly TypingTestContext _context = new();

    public void Dispose() => _context.Dispose();

    private static TextSelection S(int selectionStartLineNumber, int selectionStartColumn, int positionLineNumber, int positionColumn) =>
        new(selectionStartLineNumber, selectionStartColumn, positionLineNumber, positionColumn);

    private static TextPosition P(int lineNumber, int column) => new(lineNumber, column);

    // CoreNavigationCommands.CursorDown / CursorDownSelect
    private static void MoveDown(OperationsTestEditor editor, bool inSelectionMode = false) =>
        editor.Move((config, model, cursor) => MoveOperations.MoveDown(config, model, cursor, inSelectionMode, 1));

    [Fact]
    public void Issue_25658_Do_Not_Auto_Close_Single_Double_Quotes_After_Word_Characters()
    {
        using var editor = _context.UsingCursor([""], AutoClosingLanguageId);
        var model = editor.Model;

        void TypeCharacters(string chars)
        {
            for (int i = 0, len = chars.Length; i < len; i++)
            {
                editor.Type(chars[i].ToString(), Keyboard);
            }
        }

        // First gif
        TypeCharacters("teste1 = teste' ok");
        model.GetLineContent(1).ShouldBe("teste1 = teste' ok");

        editor.SetSelectionsValidated(S(1, 1000, 1, 1000));
        TypeCharacters("\n");
        TypeCharacters("teste2 = teste 'ok");
        model.GetLineContent(2).ShouldBe("teste2 = teste 'ok'");

        editor.SetSelectionsValidated(S(2, 1000, 2, 1000));
        TypeCharacters("\n");
        TypeCharacters("teste3 = teste\" ok");
        model.GetLineContent(3).ShouldBe("teste3 = teste\" ok");

        editor.SetSelectionsValidated(S(3, 1000, 3, 1000));
        TypeCharacters("\n");
        TypeCharacters("teste4 = teste \"ok");
        model.GetLineContent(4).ShouldBe("teste4 = teste \"ok\"");

        // Second gif
        editor.SetSelectionsValidated(S(4, 1000, 4, 1000));
        TypeCharacters("\n");
        TypeCharacters("teste '");
        model.GetLineContent(5).ShouldBe("teste ''");

        editor.SetSelectionsValidated(S(5, 1000, 5, 1000));
        TypeCharacters("\n");
        TypeCharacters("teste \"");
        model.GetLineContent(6).ShouldBe("teste \"\"");

        editor.SetSelectionsValidated(S(6, 1000, 6, 1000));
        TypeCharacters("\n");
        TypeCharacters("teste'");
        model.GetLineContent(7).ShouldBe("teste'");

        editor.SetSelectionsValidated(S(7, 1000, 7, 1000));
        TypeCharacters("\n");
        TypeCharacters("teste\"");
        model.GetLineContent(8).ShouldBe("teste\"");
    }

    [Fact]
    public void Issue_37315_Overtypes_Only_Those_Characters_That_It_Inserted()
    {
        using var editor = _context.UsingCursor(["", "y=();"], AutoClosingLanguageId);
        var model = editor.Model;
        editor.AssertCursor(P(1, 1));

        editor.Type("x=(", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        editor.Type("asd", Keyboard);
        model.GetLineContent(1).ShouldBe("x=(asd)");

        // overtype!
        editor.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("x=(asd)");

        // do not overtype!
        editor.SetSelectionsValidated(S(2, 4, 2, 4));
        editor.Type(")", Keyboard);
        model.GetLineContent(2).ShouldBe("y=());");
    }

    [Fact]
    public void Issue_37315_Stops_Overtyping_Once_Cursor_Leaves_Area()
    {
        using var editor = _context.UsingCursor(["", "y=();"], AutoClosingLanguageId);
        var model = editor.Model;
        editor.AssertCursor(P(1, 1));

        editor.Type("x=(", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        editor.SetSelectionsValidated(S(1, 5, 1, 5));
        editor.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("x=())");
    }

    [Fact]
    public void Issue_37315_It_Overtypes_Only_Once()
    {
        using var editor = _context.UsingCursor(["", "y=();"], AutoClosingLanguageId);
        var model = editor.Model;
        editor.AssertCursor(P(1, 1));

        editor.Type("x=(", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        editor.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        editor.SetSelectionsValidated(S(1, 4, 1, 4));
        editor.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("x=())");
    }

    [Fact]
    public void Issue_37315_It_Can_Remember_Multiple_Auto_Closed_Instances()
    {
        using var editor = _context.UsingCursor(["", "y=();"], AutoClosingLanguageId);
        var model = editor.Model;
        editor.AssertCursor(P(1, 1));

        editor.Type("x=(", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        editor.Type("(", Keyboard);
        model.GetLineContent(1).ShouldBe("x=(())");

        editor.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("x=(())");

        editor.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("x=(())");
    }

    [Fact]
    public void Issue_205598_Retains_Auto_Closed_Actions_Created_By_A_Reentrant_Decorations_Listener()
    {
        using var editor = _context.UsingCursor([""], AutoClosingLanguageId);
        var model = editor.Model;

        var results = new List<string>();
        for (var iteration = 0; iteration < 3; iteration++)
        {
            editor.ExecuteEdits("test", [new SingleEditOperation(model.GetFullModelRange(), "")], [S(1, 1, 1, 1)]);
            for (var pair = 0; pair <= iteration; pair++)
            {
                editor.Type("(", Keyboard);
            }
            var armed = true;
            var listening = true;
            model.DidChangeDecorations += _ =>
            {
                if (listening && armed)
                {
                    armed = false;
                    editor.Type("(", Keyboard);
                }
            };
            editor.SetPosition(P(1, model.GetLineMaxColumn(1)));
            listening = false;
            editor.Type(")", Keyboard);
            editor.SetPosition(P(1, 1));
            var autoClosedDecorations = model.GetAllDecorations().Count(d => d.Options.Description.StartsWith("auto-closed-", StringComparison.Ordinal));
            results.Add($"{model.GetValue()} {autoClosedDecorations}");
        }
        results.ShouldBe(["()() 0", "(())() 0", "((()))() 0"]);
    }

    [Fact]
    public void Issue_118270_Auto_Closing_Deletes_Only_Those_Characters_That_It_Inserted()
    {
        using var editor = _context.UsingCursor(["", "y=();"], AutoClosingLanguageId);
        var model = editor.Model;
        editor.AssertCursor(P(1, 1));

        editor.Type("x=(", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        editor.Type("asd", Keyboard);
        model.GetLineContent(1).ShouldBe("x=(asd)");

        editor.DeleteLeft();
        editor.DeleteLeft();
        editor.DeleteLeft();
        model.GetLineContent(1).ShouldBe("x=()");

        // delete closing char!
        editor.DeleteLeft();
        model.GetLineContent(1).ShouldBe("x=");

        // do not delete closing char!
        editor.SetSelectionsValidated(S(2, 4, 2, 4));
        editor.DeleteLeft();
        model.GetLineContent(2).ShouldBe("y=);");
    }

    [Fact]
    public void Issue_78527_Does_Not_Close_Quote_On_Odd_Count()
    {
        using var editor = _context.UsingCursor(["std::cout << '\"' << entryMap"], AutoClosingLanguageId);
        var model = editor.Model;
        editor.SetSelectionsValidated(S(1, 29, 1, 29));

        editor.Type("[", Keyboard);
        model.GetLineContent(1).ShouldBe("std::cout << '\"' << entryMap[]");

        editor.Type("\"", Keyboard);
        model.GetLineContent(1).ShouldBe("std::cout << '\"' << entryMap[\"\"]");

        editor.Type("a", Keyboard);
        model.GetLineContent(1).ShouldBe("std::cout << '\"' << entryMap[\"a\"]");

        editor.Type("\"", Keyboard);
        model.GetLineContent(1).ShouldBe("std::cout << '\"' << entryMap[\"a\"]");

        editor.Type("]", Keyboard);
        model.GetLineContent(1).ShouldBe("std::cout << '\"' << entryMap[\"a\"]");
    }

    [Fact]
    public async Task Issue_85983_Editor_AutoClosingBrackets_BeforeWhitespace_Is_Incorrect_For_Python()
    {
        const string languageId = "pythonMode";
        _context.Add(_context.LanguageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            AutoClosingPairs =
            [
                new AutoClosingPairConditional("{", "}"),
                new AutoClosingPairConditional("[", "]"),
                new AutoClosingPairConditional("(", ")"),
                new AutoClosingPairConditional("\"", "\"") { NotIn = ["string"] },
                new AutoClosingPairConditional("r\"", "\"") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("R\"", "\"") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("u\"", "\"") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("U\"", "\"") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("f\"", "\"") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("F\"", "\"") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("b\"", "\"") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("B\"", "\"") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("'", "'") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("r'", "'") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("R'", "'") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("u'", "'") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("U'", "'") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("f'", "'") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("F'", "'") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("b'", "'") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("B'", "'") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("`", "`") { NotIn = ["string"] },
            ],
        }));

        using var editor = _context.UsingCursor(
            ["foo'hello'"],
            languageId,
            editorOpts: EditorOptions.Default with { AutoClosingBrackets = EditorAutoClosingStrategy.BeforeWhitespace });
        await AssertType(editor, 1, 4, "(", "(", "does not auto close @ (1, 4)");
    }

    [Fact]
    public void Issue_78975_Parentheses_Swallowing_Does_Not_Work_When_Parentheses_Are_Inserted_By_Autocomplete()
    {
        using var editor = _context.UsingCursor(["<div id"], AutoClosingLanguageId);
        var model = editor.Model;
        editor.SetSelectionsValidated(S(1, 8, 1, 8));

        editor.ExecuteEdits("snippet", [new SingleEditOperation(new TextRange(1, 6, 1, 8), "id=\"\"")], [S(1, 10, 1, 10)]);
        model.GetLineContent(1).ShouldBe("<div id=\"\"");

        editor.Type("a", Keyboard);
        model.GetLineContent(1).ShouldBe("<div id=\"a\"");

        editor.Type("\"", Keyboard);
        model.GetLineContent(1).ShouldBe("<div id=\"a\"");
    }

    [Fact]
    public void Issue_315131_Parentheses_Inserted_By_A_Suggestion_Are_Tracked_As_Auto_Closed()
    {
        using var editor = _context.UsingCursor(["flo"], AutoClosingLanguageId);
        editor.SetSelectionsValidated(S(1, 4, 1, 4));

        editor.ExecuteEdits("suggest", [new SingleEditOperation(new TextRange(1, 1, 1, 4), "floor()")], [S(1, 7, 1, 7)]);
        editor.DeleteLeft();
        editor.Model.GetLineContent(1).ShouldBe("floor");
    }

    [Fact]
    public void Issue_78833_Add_Config_To_Use_Old_Brackets_Quotes_Overtyping()
    {
        using var editor = _context.UsingCursor(
            ["", "y=();"],
            AutoClosingLanguageId,
            editorOpts: EditorOptions.Default with { AutoClosingOvertype = EditorAutoClosingEditStrategy.Always });
        var model = editor.Model;
        editor.AssertCursor(P(1, 1));

        editor.Type("x=(", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        editor.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        editor.SetSelectionsValidated(S(1, 4, 1, 4));
        editor.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        editor.SetSelectionsValidated(S(2, 4, 2, 4));
        editor.Type(")", Keyboard);
        model.GetLineContent(2).ShouldBe("y=();");
    }

    [Fact]
    public void Issue_15825_Accents_On_Mac_US_Intl_Keyboard()
    {
        using var editor = _context.UsingCursor([], AutoClosingLanguageId);
        editor.AssertCursor(P(1, 1));

        // Typing ` + e on the mac US intl kb layout
        editor.StartComposition();
        editor.Type("`", Keyboard);
        editor.CompositionType("è", 1, 0, 0, Keyboard);
        editor.EndComposition(Keyboard);

        editor.Model.GetValue().ShouldBe("è");
    }

    [Fact]
    public void Issue_90016_Allow_Accents_On_Mac_US_Intl_Keyboard_To_Surround_Selection()
    {
        using var editor = _context.UsingCursor(["test"], AutoClosingLanguageId);
        editor.SetSelectionsValidated(S(1, 1, 1, 5));

        // Typing ` + e on the mac US intl kb layout
        editor.StartComposition();
        editor.Type("'", Keyboard);
        editor.CompositionType("'", 1, 0, 0, Keyboard);
        editor.CompositionType("'", 1, 0, 0, Keyboard);
        editor.EndComposition(Keyboard);

        editor.Model.GetValue().ShouldBe("'test'");
    }

    [Fact]
    public void Issue_53357_Over_Typing_Ignores_Characters_After_Backslash()
    {
        using var editor = _context.UsingCursor(["console.log();"], AutoClosingLanguageId);
        var model = editor.Model;

        editor.SetSelectionsValidated(S(1, 13, 1, 13));

        editor.Type("'", Keyboard);
        model.GetValue().ShouldBe("console.log('');");

        editor.Type("it", Keyboard);
        model.GetValue().ShouldBe("console.log('it');");

        editor.Type("\\", Keyboard);
        model.GetValue().ShouldBe("console.log('it\\');");

        editor.Type("'", Keyboard);
        model.GetValue().ShouldBe("console.log('it\\'');");
    }

    [Fact]
    public void Issue_84998_Overtyping_Brackets_Doesnt_Work_After_Backslash()
    {
        using var editor = _context.UsingCursor([""], AutoClosingLanguageId);
        var model = editor.Model;

        editor.SetSelectionsValidated(S(1, 1, 1, 1));

        editor.Type("\\", Keyboard);
        model.GetValue().ShouldBe("\\");

        editor.Type("(", Keyboard);
        model.GetValue().ShouldBe("\\()");

        editor.Type("abc", Keyboard);
        model.GetValue().ShouldBe("\\(abc)");

        editor.Type("\\", Keyboard);
        model.GetValue().ShouldBe("\\(abc\\)");

        editor.Type(")", Keyboard);
        model.GetValue().ShouldBe("\\(abc\\)");
    }

    [Fact]
    public void Issue_2773_Accents_Are_Inserted_In_The_Wrong_Position_Mac()
    {
        using var editor = _context.UsingCursor(["hello", "world"], AutoClosingLanguageId);
        editor.AssertCursor(P(1, 1));

        // Typing ` and pressing shift+down on the mac US intl kb layout
        // Here we're just replaying what the cursor gets
        editor.StartComposition();
        editor.Type("`", Keyboard);
        MoveDown(editor, true);
        editor.CompositionType("`", 1, 0, 0, Keyboard);
        editor.CompositionType("`", 1, 0, 0, Keyboard);
        editor.EndComposition(Keyboard);

        editor.Model.GetValue().ShouldBe("`hello\nworld");
        editor.AssertCursor(S(1, 2, 2, 2));
    }

    [Fact]
    public void Issue_26820_Auto_Close_Quotes_When_Not_Used_As_Accents()
    {
        using var editor = _context.UsingCursor([""], AutoClosingLanguageId);
        var model = editor.Model;
        editor.AssertCursor(P(1, 1));

        // on the mac US intl kb layout

        // Typing ' + space
        editor.StartComposition();
        editor.Type("'", Keyboard);
        editor.CompositionType("'", 1, 0, 0, Keyboard);
        editor.EndComposition(Keyboard);
        model.GetValue().ShouldBe("''");

        // Typing one more ' + space
        editor.StartComposition();
        editor.Type("'", Keyboard);
        editor.CompositionType("'", 1, 0, 0, Keyboard);
        editor.EndComposition(Keyboard);
        model.GetValue().ShouldBe("''");

        // Typing ' as a closing tag
        model.SetValue("'abc");
        editor.SetSelectionsValidated(S(1, 5, 1, 5));
        editor.StartComposition();
        editor.Type("'", Keyboard);
        editor.CompositionType("'", 1, 0, 0, Keyboard);
        editor.EndComposition(Keyboard);

        model.GetValue().ShouldBe("'abc'");

        // quotes before the newly added character are all paired.
        model.SetValue("'abc'def ");
        editor.SetSelectionsValidated(S(1, 10, 1, 10));
        editor.StartComposition();
        editor.Type("'", Keyboard);
        editor.CompositionType("'", 1, 0, 0, Keyboard);
        editor.EndComposition(Keyboard);

        model.GetValue().ShouldBe("'abc'def ''");

        // No auto closing if there is non-whitespace character after the cursor
        model.SetValue("abc");
        editor.SetSelectionsValidated(S(1, 1, 1, 1));
        editor.StartComposition();
        editor.Type("'", Keyboard);
        editor.CompositionType("'", 1, 0, 0, Keyboard);
        editor.EndComposition(Keyboard);

        // No auto closing if it's after a word.
        model.SetValue("abc");
        editor.SetSelectionsValidated(S(1, 4, 1, 4));
        editor.StartComposition();
        editor.Type("'", Keyboard);
        editor.CompositionType("'", 1, 0, 0, Keyboard);
        editor.EndComposition(Keyboard);

        model.GetValue().ShouldBe("abc'");
    }

    [Fact]
    public void Issue_144690_Quotes_Do_Not_Overtype_When_Using_US_Intl_PC_Keyboard_Layout()
    {
        using var editor = _context.UsingCursor([""], AutoClosingLanguageId);
        editor.AssertCursor(P(1, 1));

        // Pressing ' + ' + ;

        editor.StartComposition();
        editor.Type("'", Keyboard);
        editor.CompositionType("'", 1, 0, 0, Keyboard);
        editor.CompositionType("'", 1, 0, 0, Keyboard);
        editor.EndComposition(Keyboard);
        editor.StartComposition();
        editor.Type("'", Keyboard);
        editor.CompositionType("';", 1, 0, 0, Keyboard);
        editor.CompositionType("';", 2, 0, 0, Keyboard);
        editor.EndComposition(Keyboard);

        editor.Model.GetValue().ShouldBe("'';");
    }

    [Fact]
    public void Issue_144693_Typing_A_Quote_Using_US_Intl_PC_Keyboard_Layout_Always_Surrounds_Words()
    {
        using var editor = _context.UsingCursor(["const hello = 3;"], AutoClosingLanguageId);
        editor.SetSelectionsValidated(S(1, 7, 1, 12));

        // Pressing ' + e

        editor.StartComposition();
        editor.Type("'", Keyboard);
        editor.CompositionType("é", 1, 0, 0, Keyboard);
        editor.CompositionType("é", 1, 0, 0, Keyboard);
        editor.EndComposition(Keyboard);

        editor.Model.GetValue().ShouldBe("const é = 3;");
    }

    [Fact]
    public void Issue_82701_Auto_Close_Does_Not_Execute_When_IME_Is_Canceled_Via_Backspace()
    {
        using var editor = _context.UsingCursor(["{}"], AutoClosingLanguageId);
        editor.SetSelectionsValidated(S(1, 2, 1, 2));

        // Typing a + backspace
        editor.StartComposition();
        editor.Type("a", Keyboard);
        editor.CompositionType("", 1, 0, 0, Keyboard);
        editor.EndComposition(Keyboard);
        editor.Model.GetValue().ShouldBe("{}");
    }

    [Fact]
    public void Issue_20891_All_Cursors_Should_Do_The_Same_Thing()
    {
        using var editor = _context.UsingCursor(["var a = asd"], AutoClosingLanguageId);

        editor.SetSelectionsValidated(S(1, 9, 1, 9), S(1, 12, 1, 12));

        // type a `
        editor.Type("`", Keyboard);

        editor.Model.GetValue().ShouldBe("var a = `asd`");
    }

    [Fact]
    public void Issue_41825_Special_Handling_Of_Quotes_In_Surrounding_Pairs()
    {
        const string languageId = "myMode";
        _context.Add(_context.LanguageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            SurroundingPairs = [new AutoClosingPair("\"", "\""), new AutoClosingPair("'", "'")],
        }));

        using var editor = _context.CreateEditor("var x = 'hi';", languageId);
        var model = editor.Model;

        editor.SetSelectionsValidated(S(1, 9, 1, 10), S(1, 12, 1, 13));
        editor.Type("\"", Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("var x = \"hi\";", "assert1");

        editor.SetSelectionsValidated(S(1, 9, 1, 10), S(1, 12, 1, 13));
        editor.Type("'", Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("var x = 'hi';", "assert2");
    }
}
