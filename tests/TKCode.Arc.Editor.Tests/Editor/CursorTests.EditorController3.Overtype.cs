// Ported from vscode (src/vs/editor/test/browser/controller/cursor.test.ts, commit b57d5c9e), MIT license.
//
// Suite 'Editor Controller', third part, continued (helpers and notes in CursorTests.EditorController3.cs): from
// 'issue #25658' to the end of the suite. Adaptations:
//  - editor.trigger('keyboard', 'type', { text }) is TestCodeEditor.Type, which runs the same view model call;
//  - a disposable listener (model.onDidChangeDecorations) is a handler added and removed with += and -=;
//  - 'issue #111513' (two editors on one model): two TestCodeEditors on a model the test owns, with different editor
//    ids, as upstream's two test code editors have.

using Shouldly;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;
using static TKCode.Arc.Editor.Tests.TypingTestContext;

namespace TKCode.Arc.Editor.Tests;

public sealed partial class CursorTests_EditorController3
{
    [Fact]
    public void Issue_25658_Do_Not_Auto_Close_Single_Double_Quotes_After_Word_Characters()
    {
        using var editor = UsingCursor([""], AutoClosingLanguageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        void TypeCharacters(string chars)
        {
            for (int i = 0, len = chars.Length; i < len; i++)
            {
                viewModel.Type(chars[i].ToString(), Keyboard);
            }
        }

        // First gif
        model.Tokenization.ForceTokenization(model.GetLineCount());
        TypeCharacters("teste1 = teste' ok");
        model.GetLineContent(1).ShouldBe("teste1 = teste' ok");

        viewModel.SetSelections("test", [S(1, 1000, 1, 1000)]);
        TypeCharacters("\n");
        model.Tokenization.ForceTokenization(model.GetLineCount());
        TypeCharacters("teste2 = teste 'ok");
        model.GetLineContent(2).ShouldBe("teste2 = teste 'ok'");

        viewModel.SetSelections("test", [S(2, 1000, 2, 1000)]);
        TypeCharacters("\n");
        model.Tokenization.ForceTokenization(model.GetLineCount());
        TypeCharacters("teste3 = teste\" ok");
        model.GetLineContent(3).ShouldBe("teste3 = teste\" ok");

        viewModel.SetSelections("test", [S(3, 1000, 3, 1000)]);
        TypeCharacters("\n");
        model.Tokenization.ForceTokenization(model.GetLineCount());
        TypeCharacters("teste4 = teste \"ok");
        model.GetLineContent(4).ShouldBe("teste4 = teste \"ok\"");

        // Second gif
        viewModel.SetSelections("test", [S(4, 1000, 4, 1000)]);
        TypeCharacters("\n");
        model.Tokenization.ForceTokenization(model.GetLineCount());
        TypeCharacters("teste '");
        model.GetLineContent(5).ShouldBe("teste ''");

        viewModel.SetSelections("test", [S(5, 1000, 5, 1000)]);
        TypeCharacters("\n");
        model.Tokenization.ForceTokenization(model.GetLineCount());
        TypeCharacters("teste \"");
        model.GetLineContent(6).ShouldBe("teste \"\"");

        viewModel.SetSelections("test", [S(6, 1000, 6, 1000)]);
        TypeCharacters("\n");
        model.Tokenization.ForceTokenization(model.GetLineCount());
        TypeCharacters("teste'");
        model.GetLineContent(7).ShouldBe("teste'");

        viewModel.SetSelections("test", [S(7, 1000, 7, 1000)]);
        TypeCharacters("\n");
        model.Tokenization.ForceTokenization(model.GetLineCount());
        TypeCharacters("teste\"");
        model.GetLineContent(8).ShouldBe("teste\"");
    }

    [Fact]
    public void Issue_37315_Overtypes_Only_Those_Characters_That_It_Inserted()
    {
        using var editor = UsingCursor(["", "y=();"], AutoClosingLanguageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;
        AssertCursor(viewModel, P(1, 1));

        viewModel.Type("x=(", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        viewModel.Type("asd", Keyboard);
        model.GetLineContent(1).ShouldBe("x=(asd)");

        // overtype!
        viewModel.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("x=(asd)");

        // do not overtype!
        viewModel.SetSelections("test", [S(2, 4, 2, 4)]);
        viewModel.Type(")", Keyboard);
        model.GetLineContent(2).ShouldBe("y=());");
    }

    [Fact]
    public void Issue_37315_Stops_Overtyping_Once_Cursor_Leaves_Area()
    {
        using var editor = UsingCursor(["", "y=();"], AutoClosingLanguageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;
        AssertCursor(viewModel, P(1, 1));

        viewModel.Type("x=(", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        viewModel.SetSelections("test", [S(1, 5, 1, 5)]);
        viewModel.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("x=())");
    }

    [Fact]
    public void Issue_37315_It_Overtypes_Only_Once()
    {
        using var editor = UsingCursor(["", "y=();"], AutoClosingLanguageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;
        AssertCursor(viewModel, P(1, 1));

        viewModel.Type("x=(", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        viewModel.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        viewModel.SetSelections("test", [S(1, 4, 1, 4)]);
        viewModel.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("x=())");
    }

    [Fact]
    public void Issue_37315_It_Can_Remember_Multiple_Auto_Closed_Instances()
    {
        using var editor = UsingCursor(["", "y=();"], AutoClosingLanguageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;
        AssertCursor(viewModel, P(1, 1));

        viewModel.Type("x=(", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        viewModel.Type("(", Keyboard);
        model.GetLineContent(1).ShouldBe("x=(())");

        viewModel.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("x=(())");

        viewModel.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("x=(())");
    }

    [Fact]
    public void Issue_205598_Retains_Auto_Closed_Actions_Created_By_A_Reentrant_Decorations_Listener()
    {
        using var editor = UsingCursor([""], AutoClosingLanguageId);
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
            Action<ModelDecorationsChangedEvent> listener = _ =>
            {
                if (armed)
                {
                    armed = false;
                    editor.Type("(", Keyboard);
                }
            };
            model.DidChangeDecorations += listener;
            editor.SetPosition(P(1, model.GetLineMaxColumn(1)));
            model.DidChangeDecorations -= listener;
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
        using var editor = UsingCursor(["", "y=();"], AutoClosingLanguageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;
        AssertCursor(viewModel, P(1, 1));

        viewModel.Type("x=(", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        viewModel.Type("asd", Keyboard);
        model.GetLineContent(1).ShouldBe("x=(asd)");

        DeleteLeft(editor);
        DeleteLeft(editor);
        DeleteLeft(editor);
        model.GetLineContent(1).ShouldBe("x=()");

        // delete closing char!
        DeleteLeft(editor);
        model.GetLineContent(1).ShouldBe("x=");

        // do not delete closing char!
        viewModel.SetSelections("test", [S(2, 4, 2, 4)]);
        DeleteLeft(editor);
        model.GetLineContent(2).ShouldBe("y=);");
    }

    [Fact]
    public void Issue_78527_Does_Not_Close_Quote_On_Odd_Count()
    {
        using var editor = UsingCursor(["std::cout << '\"' << entryMap"], AutoClosingLanguageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(1, 29, 1, 29)]);

        viewModel.Type("[", Keyboard);
        model.GetLineContent(1).ShouldBe("std::cout << '\"' << entryMap[]");

        viewModel.Type("\"", Keyboard);
        model.GetLineContent(1).ShouldBe("std::cout << '\"' << entryMap[\"\"]");

        viewModel.Type("a", Keyboard);
        model.GetLineContent(1).ShouldBe("std::cout << '\"' << entryMap[\"a\"]");

        viewModel.Type("\"", Keyboard);
        model.GetLineContent(1).ShouldBe("std::cout << '\"' << entryMap[\"a\"]");

        viewModel.Type("]", Keyboard);
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

        using var editor = UsingCursor(
            ["foo'hello'"],
            languageId,
            editorOpts: EditorOptions.Default with { AutoClosingBrackets = EditorAutoClosingStrategy.BeforeWhitespace });
        await AssertType(editor, 1, 4, "(", "(", "does not auto close @ (1, 4)");
    }

    [Fact]
    public void Issue_78975_Parentheses_Swallowing_Does_Not_Work_When_Parentheses_Are_Inserted_By_Autocomplete()
    {
        using var editor = UsingCursor(["<div id"], AutoClosingLanguageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(1, 8, 1, 8)]);

        viewModel.ExecuteEdits("snippet", [new SingleEditOperation(new TextRange(1, 6, 1, 8), "id=\"\"")], _ => [S(1, 10, 1, 10)], EditSources.Unknown());
        model.GetLineContent(1).ShouldBe("<div id=\"\"");

        viewModel.Type("a", Keyboard);
        model.GetLineContent(1).ShouldBe("<div id=\"a\"");

        viewModel.Type("\"", Keyboard);
        model.GetLineContent(1).ShouldBe("<div id=\"a\"");
    }

    [Fact]
    public void Issue_315131_Parentheses_Inserted_By_A_Suggestion_Are_Tracked_As_Auto_Closed()
    {
        using var editor = UsingCursor(["flo"], AutoClosingLanguageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(1, 4, 1, 4)]);

        viewModel.ExecuteEdits("suggest", [new SingleEditOperation(new TextRange(1, 1, 1, 4), "floor()")], _ => [S(1, 7, 1, 7)], EditSources.Unknown());
        DeleteLeft(editor);
        model.GetLineContent(1).ShouldBe("floor");
    }

    [Fact]
    public void Issue_78833_Add_Config_To_Use_Old_Brackets_Quotes_Overtyping()
    {
        using var editor = UsingCursor(
            ["", "y=();"],
            AutoClosingLanguageId,
            editorOpts: EditorOptions.Default with { AutoClosingOvertype = EditorAutoClosingEditStrategy.Always });
        var model = editor.Model;
        var viewModel = editor.ViewModel;
        AssertCursor(viewModel, P(1, 1));

        viewModel.Type("x=(", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        viewModel.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        viewModel.SetSelections("test", [S(1, 4, 1, 4)]);
        viewModel.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("x=()");

        viewModel.SetSelections("test", [S(2, 4, 2, 4)]);
        viewModel.Type(")", Keyboard);
        model.GetLineContent(2).ShouldBe("y=();");
    }

    [Fact]
    public void Issue_15825_Accents_On_Mac_US_Intl_Keyboard()
    {
        using var editor = UsingCursor([], AutoClosingLanguageId);
        var viewModel = editor.ViewModel;
        AssertCursor(viewModel, P(1, 1));

        // Typing ` + e on the mac US intl kb layout
        viewModel.StartComposition();
        viewModel.Type("`", Keyboard);
        viewModel.CompositionType("è", 1, 0, 0, Keyboard);
        viewModel.EndComposition(Keyboard);

        editor.Model.GetValue().ShouldBe("è");
    }

    [Fact]
    public void Issue_90016_Allow_Accents_On_Mac_US_Intl_Keyboard_To_Surround_Selection()
    {
        using var editor = UsingCursor(["test"], AutoClosingLanguageId);
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(1, 1, 1, 5)]);

        // Typing ` + e on the mac US intl kb layout
        viewModel.StartComposition();
        viewModel.Type("'", Keyboard);
        viewModel.CompositionType("'", 1, 0, 0, Keyboard);
        viewModel.CompositionType("'", 1, 0, 0, Keyboard);
        viewModel.EndComposition(Keyboard);

        editor.Model.GetValue().ShouldBe("'test'");
    }

    [Fact]
    public void Issue_53357_Over_Typing_Ignores_Characters_After_Backslash()
    {
        using var editor = UsingCursor(["console.log();"], AutoClosingLanguageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        viewModel.SetSelections("test", [S(1, 13, 1, 13)]);

        viewModel.Type("'", Keyboard);
        model.GetValue().ShouldBe("console.log('');");

        viewModel.Type("it", Keyboard);
        model.GetValue().ShouldBe("console.log('it');");

        viewModel.Type("\\", Keyboard);
        model.GetValue().ShouldBe("console.log('it\\');");

        viewModel.Type("'", Keyboard);
        model.GetValue().ShouldBe("console.log('it\\'');");
    }

    [Fact]
    public void Issue_84998_Overtyping_Brackets_Doesnt_Work_After_Backslash()
    {
        using var editor = UsingCursor([""], AutoClosingLanguageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        viewModel.SetSelections("test", [S(1, 1, 1, 1)]);

        viewModel.Type("\\", Keyboard);
        model.GetValue().ShouldBe("\\");

        viewModel.Type("(", Keyboard);
        model.GetValue().ShouldBe("\\()");

        viewModel.Type("abc", Keyboard);
        model.GetValue().ShouldBe("\\(abc)");

        viewModel.Type("\\", Keyboard);
        model.GetValue().ShouldBe("\\(abc\\)");

        viewModel.Type(")", Keyboard);
        model.GetValue().ShouldBe("\\(abc\\)");
    }

    [Fact]
    public void Issue_2773_Accents_Are_Inserted_In_The_Wrong_Position_Mac()
    {
        using var editor = UsingCursor(["hello", "world"], AutoClosingLanguageId);
        var viewModel = editor.ViewModel;
        AssertCursor(viewModel, P(1, 1));

        // Typing ` and pressing shift+down on the mac US intl kb layout
        // Here we're just replaying what the cursor gets
        viewModel.StartComposition();
        viewModel.Type("`", Keyboard);
        MoveDown(viewModel, true);
        viewModel.CompositionType("`", 1, 0, 0, Keyboard);
        viewModel.CompositionType("`", 1, 0, 0, Keyboard);
        viewModel.EndComposition(Keyboard);

        editor.Model.GetValue().ShouldBe("`hello\nworld");
        AssertCursor(viewModel, S(1, 2, 2, 2));
    }

    [Fact]
    public void Issue_26820_Auto_Close_Quotes_When_Not_Used_As_Accents()
    {
        using var editor = UsingCursor([""], AutoClosingLanguageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;
        AssertCursor(viewModel, P(1, 1));

        // on the mac US intl kb layout

        // Typing ' + space
        viewModel.StartComposition();
        viewModel.Type("'", Keyboard);
        viewModel.CompositionType("'", 1, 0, 0, Keyboard);
        viewModel.EndComposition(Keyboard);
        model.GetValue().ShouldBe("''");

        // Typing one more ' + space
        viewModel.StartComposition();
        viewModel.Type("'", Keyboard);
        viewModel.CompositionType("'", 1, 0, 0, Keyboard);
        viewModel.EndComposition(Keyboard);
        model.GetValue().ShouldBe("''");

        // Typing ' as a closing tag
        model.SetValue("'abc");
        viewModel.SetSelections("test", [S(1, 5, 1, 5)]);
        viewModel.StartComposition();
        viewModel.Type("'", Keyboard);
        viewModel.CompositionType("'", 1, 0, 0, Keyboard);
        viewModel.EndComposition(Keyboard);

        model.GetValue().ShouldBe("'abc'");

        // quotes before the newly added character are all paired.
        model.SetValue("'abc'def ");
        viewModel.SetSelections("test", [S(1, 10, 1, 10)]);
        viewModel.StartComposition();
        viewModel.Type("'", Keyboard);
        viewModel.CompositionType("'", 1, 0, 0, Keyboard);
        viewModel.EndComposition(Keyboard);

        model.GetValue().ShouldBe("'abc'def ''");

        // No auto closing if there is non-whitespace character after the cursor
        model.SetValue("abc");
        viewModel.SetSelections("test", [S(1, 1, 1, 1)]);
        viewModel.StartComposition();
        viewModel.Type("'", Keyboard);
        viewModel.CompositionType("'", 1, 0, 0, Keyboard);
        viewModel.EndComposition(Keyboard);

        // No auto closing if it's after a word.
        model.SetValue("abc");
        viewModel.SetSelections("test", [S(1, 4, 1, 4)]);
        viewModel.StartComposition();
        viewModel.Type("'", Keyboard);
        viewModel.CompositionType("'", 1, 0, 0, Keyboard);
        viewModel.EndComposition(Keyboard);

        model.GetValue().ShouldBe("abc'");
    }

    [Fact]
    public void Issue_144690_Quotes_Do_Not_Overtype_When_Using_US_Intl_PC_Keyboard_Layout()
    {
        using var editor = UsingCursor([""], AutoClosingLanguageId);
        var viewModel = editor.ViewModel;
        AssertCursor(viewModel, P(1, 1));

        // Pressing ' + ' + ;

        viewModel.StartComposition();
        viewModel.Type("'", Keyboard);
        viewModel.CompositionType("'", 1, 0, 0, Keyboard);
        viewModel.CompositionType("'", 1, 0, 0, Keyboard);
        viewModel.EndComposition(Keyboard);
        viewModel.StartComposition();
        viewModel.Type("'", Keyboard);
        viewModel.CompositionType("';", 1, 0, 0, Keyboard);
        viewModel.CompositionType("';", 2, 0, 0, Keyboard);
        viewModel.EndComposition(Keyboard);

        editor.Model.GetValue().ShouldBe("'';");
    }

    [Fact]
    public void Issue_144693_Typing_A_Quote_Using_US_Intl_PC_Keyboard_Layout_Always_Surrounds_Words()
    {
        using var editor = UsingCursor(["const hello = 3;"], AutoClosingLanguageId);
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(1, 7, 1, 12)]);

        // Pressing ' + e

        viewModel.StartComposition();
        viewModel.Type("'", Keyboard);
        viewModel.CompositionType("é", 1, 0, 0, Keyboard);
        viewModel.CompositionType("é", 1, 0, 0, Keyboard);
        viewModel.EndComposition(Keyboard);

        editor.Model.GetValue().ShouldBe("const é = 3;");
    }

    [Fact]
    public void Issue_82701_Auto_Close_Does_Not_Execute_When_IME_Is_Canceled_Via_Backspace()
    {
        using var editor = UsingCursor(["{}"], AutoClosingLanguageId);
        var viewModel = editor.ViewModel;
        viewModel.SetSelections("test", [S(1, 2, 1, 2)]);

        // Typing a + backspace
        viewModel.StartComposition();
        viewModel.Type("a", Keyboard);
        viewModel.CompositionType("", 1, 0, 0, Keyboard);
        viewModel.EndComposition(Keyboard);
        editor.Model.GetValue().ShouldBe("{}");
    }

    [Fact]
    public void Issue_20891_All_Cursors_Should_Do_The_Same_Thing()
    {
        using var editor = UsingCursor(["var a = asd"], AutoClosingLanguageId);
        var viewModel = editor.ViewModel;

        viewModel.SetSelections("test", [S(1, 9, 1, 9), S(1, 12, 1, 12)]);

        // type a `
        viewModel.Type("`", Keyboard);

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

        using var editor = CreateEditor("var x = 'hi';", languageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        editor.SetSelections([S(1, 9, 1, 10), S(1, 12, 1, 13)]);
        viewModel.Type("\"", Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("var x = \"hi\";", "assert1");

        editor.SetSelections([S(1, 9, 1, 10), S(1, 12, 1, 13)]);
        viewModel.Type("'", Keyboard);
        model.GetValue(EndOfLinePreference.LF).ShouldBe("var x = 'hi';", "assert2");
    }

    [Fact]
    public void All_Cursors_Should_Do_The_Same_Thing_When_Deleting_Left()
    {
        using var editor = CreateEditor(string.Join('\n', ["var a = ()"]), AutoClosingLanguageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        viewModel.SetSelections("test", [S(1, 4, 1, 4), S(1, 10, 1, 10)]);

        // delete left
        DeleteLeft(editor);

        model.GetValue().ShouldBe("va a = )");
    }

    [Fact]
    public void Issue_7100_Mouse_Word_Selection_Is_Strange_When_Non_Word_Character_Is_At_The_End_Of_Line()
    {
        using var editor = CreateEditor(string.Join('\n',
        [
            "before.a",
            "before",
            "hello:",
            "there:",
            "this is strange:",
            "here",
            "it",
            "is",
        ]));
        var viewModel = editor.ViewModel;

        // CoreNavigationCommands.WordSelect
        CoreNavigationCommands.WordSelect.RunCoreEditorCommand(viewModel, new() { Position = P(3, 7) });
        AssertCursor(viewModel, S(3, 7, 3, 7));

        // CoreNavigationCommands.WordSelectDrag
        CoreNavigationCommands.WordSelectDrag.RunCoreEditorCommand(viewModel, new() { Position = P(4, 7) });
        AssertCursor(viewModel, S(3, 7, 4, 7));
    }

    [Fact]
    public void Issue_112039_Shift_Continuing_A_Double_Triple_Click_And_Drag_Selection_Does_Not_Remember_Its_Starting_Mode()
    {
        using var editor = CreateEditor(string.Join('\n',
        [
            "just some text",
            "and another line",
            "and another one",
        ]));
        var viewModel = editor.ViewModel;

        // CoreNavigationCommands.WordSelect
        CoreNavigationCommands.WordSelect.RunCoreEditorCommand(viewModel, new() { Position = P(2, 6) });
        // CoreNavigationCommands.MoveToSelect
        MoveTo(viewModel, 1, 8, inSelectionMode: true);
        AssertCursor(viewModel, S(2, 12, 1, 6));
    }

    [Fact]
    public void Issue_158236_Shift_Click_Selection_Does_Not_Work_On_Line_Number_Indicator()
    {
        using var editor = CreateEditor(string.Join('\n',
        [
            "just some text",
            "and another line",
            "and another one",
        ]));
        var viewModel = editor.ViewModel;

        // CoreNavigationCommands.MoveTo
        MoveTo(viewModel, 3, 5);
        // CoreNavigationCommands.LineSelectDrag
        CoreNavigationCommands.LineSelectDrag.RunCoreEditorCommand(viewModel, new() { Position = P(2, 1) });
        AssertCursor(viewModel, S(3, 5, 2, 1));
    }

    [Fact]
    public void Issue_111513_Text_Gets_Automatically_Selected_When_Typing_At_The_Same_Location_In_Another_Editor()
    {
        using var model = TestTextModel.Create(string.Join('\n',
        [
            "just",
            "",
            "some text",
        ]));

        TestCodeEditor.WithTestCodeEditor(model, new TestCodeEditorOptions { EditorId = 1 }, (editor1, viewModel1) =>
        {
            editor1.SetSelections([S(2, 1, 2, 1)]);
            TestCodeEditor.WithTestCodeEditor(model, new TestCodeEditorOptions { EditorId = 2 }, (editor2, viewModel2) =>
            {
                editor2.SetSelections([S(2, 1, 2, 1)]);
                viewModel2.Type("e", Keyboard);
                AssertCursor(viewModel2, P(2, 2));
                AssertCursor(viewModel1, P(2, 2));
            });
        });
    }
}
