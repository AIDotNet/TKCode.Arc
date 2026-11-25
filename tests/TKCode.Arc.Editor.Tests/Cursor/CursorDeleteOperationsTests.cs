// Tests for DeleteOperations, ported from vscode (src/vs/editor/test/browser/controller/cursor.test.ts, commit b57d5c9e),
// MIT license: from suite 'Editor Controller', 'bug #16740', 'issue #128602', 'issue #1140', 'issue #99629' (two),
// 'UseTabStops is off', 'Backspace removes whitespaces with tab size', 'All cursors should do the same thing when
// deleting left' and 'issue #118270'; from suite 'Undo stops', the two tests that only delete. Upstream runs the
// DeleteLeft, DeleteRight and cut commands on a test code editor; here they are driven through the operations on a
// text model (OperationsTestEditor), the way coreCommands.ts and cursor.ts call them (undo stop before when the
// operation asks for one, then CommandExecutor).
//
// Adapted: 'issue #118270' types 'x=(' and 'asd' through the view model, which records the auto-closed ')'; here the
// line is created with that text and the auto-closed range is passed to DeleteLeft as the controller would. The undo
// checks compare the text only: the cursor state after an undo comes from the controller.
// Not ported: tests that type, paste or indent first.

using Shouldly;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class CursorDeleteOperationsTests
{
    private const string AutoClosingLanguageId = "autoClosingMode";

    private static TextSelection S(int selectionStartLineNumber, int selectionStartColumn, int positionLineNumber, int positionColumn) =>
        new(selectionStartLineNumber, selectionStartColumn, positionLineNumber, positionColumn);

    private static IDisposable RegisterAutoClosingLanguage(LanguageConfigurationService service) =>
        service.Register(AutoClosingLanguageId, new LanguageConfiguration
        {
            Comments = new CommentRule { BlockComment = ("/*", "*/") },
            AutoClosingPairs =
            [
                new AutoClosingPairConditional("{", "}"),
                new AutoClosingPairConditional("[", "]"),
                new AutoClosingPairConditional("(", ")"),
                new AutoClosingPairConditional("'", "'") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("\"", "\"") { NotIn = ["string"] },
                new AutoClosingPairConditional("`", "`") { NotIn = ["string", "comment"] },
                new AutoClosingPairConditional("/**", " */") { NotIn = ["string"] },
                new AutoClosingPairConditional("begin", "end") { NotIn = ["string"] },
            ],
        });

    [Fact]
    public void Bug_16740_Editor_Cut_Line_Doesnt_Quite_Cut_The_Last_Line()
    {
        // Part 1 => there is text on the last line
        using (var editor = OperationsTestEditor.Create(["asdasd", "qwerty"]))
        {
            editor.MoveTo(2, 1, false);
            editor.AssertCursor(S(2, 1, 2, 1));

            editor.Cut();
            editor.Model.GetLineCount().ShouldBe(1);
            editor.Model.GetLineContent(1).ShouldBe("asdasd");
        }

        // Part 2 => there is no text on the last line
        using (var editor = OperationsTestEditor.Create(["asdasd", ""]))
        {
            editor.MoveTo(2, 1, false);
            editor.AssertCursor(S(2, 1, 2, 1));

            editor.Cut();
            editor.Model.GetLineCount().ShouldBe(1);
            editor.Model.GetLineContent(1).ShouldBe("asdasd");

            editor.Cut();
            editor.Model.GetLineCount().ShouldBe(1);
            editor.Model.GetLineContent(1).ShouldBe("");
        }
    }

    [Fact]
    public void Issue_128602_When_Cutting_Multiple_Lines_Ctrl_X_The_Last_Line_Will_Not_Be_Erased()
    {
        using var editor = OperationsTestEditor.Create(["a1", "a2", "a3"]);

        editor.SetSelections([
            S(1, 1, 1, 1),
            S(2, 1, 2, 1),
            S(3, 1, 3, 1),
        ]);

        editor.Cut();
        editor.Model.GetLineCount().ShouldBe(1);
        editor.Model.GetLineContent(1).ShouldBe("");
    }

    [Fact]
    public void Issue_1140_Backspace_Stops_Prematurely()
    {
        using var editor = OperationsTestEditor.Create(["function baz() {", "  return 1;", "};"]);

        editor.MoveTo(3, 2, false);
        editor.MoveTo(1, 14, true);
        editor.AssertCursor(S(3, 2, 1, 14));

        editor.DeleteLeft();
        editor.AssertCursor(S(1, 14, 1, 14));
        editor.Model.GetLineCount().ShouldBe(1);
        editor.Model.GetLineContent(1).ShouldBe("function baz(;");
    }

    [Fact]
    public void Issue_99629_Emoji_Modifiers_In_Text_Treated_Separately_When_Using_Backspace()
    {
        using var editor = OperationsTestEditor.Create("👶🏾", EditorOptions.Default with { UseTabStops = false });

        var len = editor.Model.GetValueLength();
        editor.SetSelections([S(1, 1 + len, 1, 1 + len)]);

        editor.DeleteLeft();
        editor.Model.GetValue(EndOfLinePreference.LF).ShouldBe("");
    }

    [Fact]
    public void Issue_99629_Emoji_Modifiers_In_Text_Treated_Separately_When_Using_Backspace_ZWJ_Sequence()
    {
        using var editor = OperationsTestEditor.Create("👨‍👩🏽‍👧‍👦", EditorOptions.Default with { UseTabStops = false });

        var len = editor.Model.GetValueLength();
        editor.SetSelections([S(1, 1 + len, 1, 1 + len)]);

        editor.DeleteLeft();
        editor.Model.GetValue(EndOfLinePreference.LF).ShouldBe("👨‍👩🏽‍👧");

        editor.DeleteLeft();
        editor.Model.GetValue(EndOfLinePreference.LF).ShouldBe("👨‍👩🏽");

        editor.DeleteLeft();
        editor.Model.GetValue(EndOfLinePreference.LF).ShouldBe("👨");

        editor.DeleteLeft();
        editor.Model.GetValue(EndOfLinePreference.LF).ShouldBe("");
    }

    [Fact]
    public void UseTabStops_Is_Off()
    {
        using var editor = OperationsTestEditor.Create(
            string.Join('\n', "    x", "        a    ", "    "),
            EditorOptions.Default with { UseTabStops = false });

        // DeleteLeft removes just one whitespace
        editor.MoveTo(2, 9);
        editor.DeleteLeft();
        editor.Model.GetLineContent(2).ShouldBe("       a    ");
    }

    [Fact]
    public async Task Backspace_Removes_Whitespaces_With_Tab_Size()
    {
        using var editor = OperationsTestEditor.Create(
            string.Join('\n', " \t \t     x", "        a    ", "    "),
            EditorOptions.Default with { UseTabStops = true });
        var model = editor.Model;

        // DeleteLeft does not remove tab size, because some text exists before
        editor.MoveTo(2, model.GetLineContent(2).Length + 1);
        editor.DeleteLeft();
        model.GetLineContent(2).ShouldBe("        a   ");

        // DeleteLeft removes tab size = 4
        editor.MoveTo(2, 9);
        editor.DeleteLeft();
        model.GetLineContent(2).ShouldBe("    a   ");

        // DeleteLeft removes tab size = 4
        editor.DeleteLeft();
        model.GetLineContent(2).ShouldBe("a   ");

        // Undo DeleteLeft - get us back to original indentation
        await model.Undo();
        model.GetLineContent(2).ShouldBe("        a   ");

        // Nothing is broken when cursor is in (1,1)
        editor.MoveTo(1, 1);
        editor.DeleteLeft();
        model.GetLineContent(1).ShouldBe(" \t \t     x");

        // DeleteLeft stops at tab stops even in mixed whitespace case
        editor.MoveTo(1, 10);
        editor.DeleteLeft();
        model.GetLineContent(1).ShouldBe(" \t \t    x");

        editor.DeleteLeft();
        model.GetLineContent(1).ShouldBe(" \t \tx");

        editor.DeleteLeft();
        model.GetLineContent(1).ShouldBe(" \tx");

        editor.DeleteLeft();
        model.GetLineContent(1).ShouldBe("x");

        // DeleteLeft on last line
        editor.MoveTo(3, model.GetLineContent(3).Length + 1);
        editor.DeleteLeft();
        model.GetLineContent(3).ShouldBe("");

        // DeleteLeft with removing new line symbol
        editor.DeleteLeft();
        model.GetValue(EndOfLinePreference.LF).ShouldBe("x\n        a   ");

        // In case of selection DeleteLeft only deletes selected text
        editor.MoveTo(2, 3);
        editor.MoveTo(2, 4, true);
        editor.DeleteLeft();
        model.GetLineContent(2).ShouldBe("       a   ");
    }

    [Fact]
    public void All_Cursors_Should_Do_The_Same_Thing_When_Deleting_Left()
    {
        using var languageConfigurationService = new LanguageConfigurationService();
        using var registration = RegisterAutoClosingLanguage(languageConfigurationService);
        using var editor = OperationsTestEditor.Create("var a = ()", null, AutoClosingLanguageId, languageConfigurationService);

        editor.SetSelections([
            S(1, 4, 1, 4),
            S(1, 10, 1, 10),
        ]);

        // delete left
        editor.DeleteLeft();

        editor.GetValue().ShouldBe("va a = )");
    }

    [Fact]
    public void Issue_118270_Auto_Closing_Deletes_Only_Those_Characters_That_It_Inserted()
    {
        using var languageConfigurationService = new LanguageConfigurationService();
        using var registration = RegisterAutoClosingLanguage(languageConfigurationService);
        using var editor = OperationsTestEditor.Create(string.Join('\n', "x=(asd)", "y=();"), null, AutoClosingLanguageId, languageConfigurationService);

        // upstream types 'x=(' (which auto-closes the ')') and then 'asd'
        TextRange[] autoClosedCharacters = [new TextRange(1, 7, 1, 8)];
        editor.SetSelections([S(1, 7, 1, 7)]);

        editor.DeleteLeft(autoClosedCharacters);
        editor.DeleteLeft([new TextRange(1, 6, 1, 7)]);
        editor.DeleteLeft([new TextRange(1, 5, 1, 6)]);
        editor.Model.GetLineContent(1).ShouldBe("x=()");

        // delete closing char!
        editor.DeleteLeft([new TextRange(1, 4, 1, 5)]);
        editor.Model.GetLineContent(1).ShouldBe("x=");

        // do not delete closing char!
        editor.SetSelections([S(2, 4, 2, 4)]);
        editor.DeleteLeft([]);
        editor.Model.GetLineContent(2).ShouldBe("y=);");
    }

    [Fact]
    public async Task There_Is_An_Undo_Stop_Between_Deleting_Left_And_Deleting_Right()
    {
        using var editor = OperationsTestEditor.Create(["A  line", "Another line"]);
        var model = editor.Model;

        editor.SetSelections([S(2, 8, 2, 8)]);
        for (var i = 0; i < 7; i++)
        {
            editor.DeleteLeft();
        }
        model.GetLineContent(2).ShouldBe(" line");
        editor.AssertCursor(S(2, 1, 2, 1));

        for (var i = 0; i < 5; i++)
        {
            editor.DeleteRight();
        }
        model.GetLineContent(2).ShouldBe("");
        editor.AssertCursor(S(2, 1, 2, 1));

        await model.Undo();
        model.GetLineContent(2).ShouldBe(" line");

        await model.Undo();
        model.GetLineContent(2).ShouldBe("Another line");
    }

    [Fact]
    public async Task There_Is_An_Undo_Stop_Between_Deleting_Right_And_Deleting_Left()
    {
        using var editor = OperationsTestEditor.Create(["A  line", "Another line"]);
        var model = editor.Model;

        editor.SetSelections([S(2, 9, 2, 9)]);
        for (var i = 0; i < 4; i++)
        {
            editor.DeleteRight();
        }
        model.GetLineContent(2).ShouldBe("Another ");
        editor.AssertCursor(S(2, 9, 2, 9));

        for (var i = 0; i < 6; i++)
        {
            editor.DeleteLeft();
        }
        model.GetLineContent(2).ShouldBe("An");
        editor.AssertCursor(S(2, 3, 2, 3));

        await model.Undo();
        model.GetLineContent(2).ShouldBe("Another ");

        await model.Undo();
        model.GetLineContent(2).ShouldBe("Another line");
    }
}
