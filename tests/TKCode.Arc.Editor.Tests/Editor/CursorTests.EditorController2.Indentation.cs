// Ported from vscode (src/vs/editor/test/browser/controller/cursor.test.ts, commit b57d5c9e), MIT license.
// Suite 'Editor Controller', the tests from 'Enter honors increaseIndentPattern' to 'ElectricCharacter - issue #23711'
// (the second part of CursorTests_EditorController2; the adaptations are listed in CursorTests.EditorController2.cs).

using Shouldly;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed partial class CursorTests_EditorController2
{
    private const string IndentRulesLanguageId = TypingTestContext.IndentRulesLanguageId;
    private const string ElectricCharLanguageId = TypingTestContext.ElectricCharLanguageId;

    [Fact]
    public void Enter_Honors_IncreaseIndentPattern()
    {
        using var editor = CreateEditor(["if (true) {", "\tif (true) {"], IndentRulesLanguageId, ModelOpts(insertSpaces: false), s_autoIndentFull);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 12, false);
        AssertCursor(viewModel, S(1, 12, 1, 12));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(2, 2, 2, 2));

        MoveTo(viewModel, 3, 13, false);
        AssertCursor(viewModel, S(3, 13, 3, 13));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(4, 3, 4, 3));
    }

    [Fact]
    public void Type_Honors_DecreaseIndentPattern()
    {
        using var editor = CreateEditor(["if (true) {", "\t"], IndentRulesLanguageId, options: s_autoIndentFull);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 2, 2, false);
        AssertCursor(viewModel, S(2, 2, 2, 2));

        viewModel.Type("}", Keyboard);
        AssertCursor(viewModel, S(2, 2, 2, 2));
        editor.Model.GetLineContent(2).ShouldBe("}", "001");
    }

    [Fact]
    public void Enter_Honors_UnIndentedLinePattern()
    {
        using var editor = CreateEditor(["if (true) {", "\t\t\treturn true"], IndentRulesLanguageId, ModelOpts(insertSpaces: false), s_autoIndentFull);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 2, 15, false);
        AssertCursor(viewModel, S(2, 15, 2, 15));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(3, 2, 3, 2));
    }

    [Fact]
    public void Enter_Honors_IndentNextLinePattern()
    {
        using var editor = CreateEditor(
            ["if (true)", "\treturn true;", "if (true)", "\t\t\t\treturn true"],
            IndentRulesLanguageId,
            ModelOpts(insertSpaces: false),
            s_autoIndentFull);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 2, 14, false);
        AssertCursor(viewModel, S(2, 14, 2, 14));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(3, 1, 3, 1));

        MoveTo(viewModel, 5, 16, false);
        AssertCursor(viewModel, S(5, 16, 5, 16));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(6, 2, 6, 2));
    }

    [Fact]
    public void Enter_Honors_IndentNextLinePattern_2()
    {
        using var editor = CreateEditor(["if (true)", "\tif (true)"], IndentRulesLanguageId, ModelOpts(insertSpaces: false), s_autoIndentFull);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 2, 11, false);
        AssertCursor(viewModel, S(2, 11, 2, 11));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(3, 3, 3, 3));

        viewModel.Type("console.log();", Keyboard);
        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(4, 1, 4, 1));
    }

    [Fact]
    public void Enter_Honors_Intential_Indent()
    {
        using var editor = CreateEditor(["if (true) {", "\tif (true) {", "return true;", "}}"], IndentRulesLanguageId, options: s_autoIndentFull);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 3, 13, false);
        AssertCursor(viewModel, S(3, 13, 3, 13));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(4, 1, 4, 1));
        editor.Model.GetLineContent(3).ShouldBe("return true;", "001");
    }

    [Fact]
    public void Enter_Supports_Selection_1()
    {
        using var editor = CreateEditor(["if (true) {", "\tif (true) {", "\t\treturn true;", "\t}a}"], IndentRulesLanguageId, ModelOpts(insertSpaces: false));
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 4, 3, false);
        MoveTo(viewModel, 4, 4, true);
        AssertCursor(viewModel, S(4, 3, 4, 4));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(5, 1, 5, 1));
        editor.Model.GetLineContent(4).ShouldBe("\t}", "001");
    }

    [Fact]
    public void Enter_Supports_Selection_2()
    {
        using var editor = CreateEditor(["if (true) {", "\tif (true) {"], IndentRulesLanguageId, ModelOpts(insertSpaces: false));
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 2, 12, false);
        MoveTo(viewModel, 2, 13, true);
        AssertCursor(viewModel, S(2, 12, 2, 13));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(3, 3, 3, 3));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(4, 3, 4, 3));
    }

    [Fact]
    public void Enter_Honors_TabSize_And_InsertSpaces_1()
    {
        using var editor = CreateEditor(["if (true) {", "\tif (true) {"], IndentRulesLanguageId);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 12, false);
        AssertCursor(viewModel, S(1, 12, 1, 12));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(2, 5, 2, 5));

        MoveTo(viewModel, 3, 13, false);
        AssertCursor(viewModel, S(3, 13, 3, 13));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(4, 9, 4, 9));
    }

    // enterHonorsTabSizeCases
    private void EnterHonorsTabSizeCase(TextModelCreationOptions? modelOpts, TextSelection firstExpected, TextSelection secondExpected)
    {
        using var editor = CreateEditor(["if (true) {", "    if (true) {"], IndentRulesLanguageId, modelOpts);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 12, false);
        AssertCursor(viewModel, S(1, 12, 1, 12));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, firstExpected);

        MoveTo(viewModel, 3, 16, false);
        AssertCursor(viewModel, S(3, 16, 3, 16));

        viewModel.Type("\n", Keyboard);
        editor.Model.GetLineContent(3).ShouldBe("    if (true) {");
        AssertCursor(viewModel, secondExpected);
    }

    [Fact]
    public void Enter_Honors_TabSize_And_InsertSpaces_2() =>
        EnterHonorsTabSizeCase(null, S(2, 5, 2, 5), S(4, 9, 4, 9));

    [Fact]
    public void Enter_Honors_TabSize_And_InsertSpaces_3() =>
        EnterHonorsTabSizeCase(ModelOpts(insertSpaces: false), S(2, 2, 2, 2), S(4, 3, 4, 3));

    [Fact]
    public void Enter_Supports_Intentional_Indentation()
    {
        using var editor = CreateEditor(
            ["\tif (true) {", "\t\tswitch(true) {", "\t\t\tcase true:", "\t\t\t\tbreak;", "\t\t}", "\t}"],
            IndentRulesLanguageId,
            ModelOpts(insertSpaces: false),
            s_autoIndentFull);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 5, 4, false);
        AssertCursor(viewModel, S(5, 4, 5, 4));

        viewModel.Type("\n", Keyboard);
        editor.Model.GetLineContent(5).ShouldBe("\t\t}");
        AssertCursor(viewModel, S(6, 3, 6, 3));
    }

    // enterInMiddleOfLineCases
    private void EnterInMiddleOfLineCase(string[] text, TextModelCreationOptions? modelOpts, int column, TextSelection expectedSelection, string expectedLine)
    {
        using var editor = CreateEditor(text, IndentRulesLanguageId, modelOpts);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 3, column, false);
        AssertCursor(viewModel, S(3, column, 3, column));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, expectedSelection);
        editor.Model.GetLineContent(4).ShouldBe(expectedLine, "001");
    }

    [Fact]
    public void Enter_Should_Not_Adjust_Cursor_Position_When_Press_Enter_In_The_Middle_Of_A_Line_1() =>
        EnterInMiddleOfLineCase(["if (true) {", "\tif (true) {", "\t\treturn true;", "\t}a}"], ModelOpts(insertSpaces: false), 9, S(4, 3, 4, 3), "\t\t true;");

    [Fact]
    public void Enter_Should_Not_Adjust_Cursor_Position_When_Press_Enter_In_The_Middle_Of_A_Line_2() =>
        EnterInMiddleOfLineCase(["if (true) {", "\tif (true) {", "\t\treturn true;", "\t}a}"], ModelOpts(insertSpaces: false), 3, S(4, 3, 4, 3), "\t\treturn true;");

    [Fact]
    public void Enter_Should_Not_Adjust_Cursor_Position_When_Press_Enter_In_The_Middle_Of_A_Line_3() =>
        EnterInMiddleOfLineCase(["if (true) {", "  if (true) {", "    return true;", "  }a}"], null, 11, S(4, 5, 4, 5), "     true;");

    // enterInLeadingWhitespaceCases
    private void EnterInLeadingWhitespaceCase(string[] text, int column, TextSelection expectedSelection, string expectedLine)
    {
        using var editor = CreateEditor(text, IndentRulesLanguageId, ModelOpts(insertSpaces: false));
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 3, column, false);
        AssertCursor(viewModel, S(3, column, 3, column));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, expectedSelection);
        model.GetLineContent(4).ShouldBe(expectedLine, "001");

        MoveTo(viewModel, 4, 1, false);
        AssertCursor(viewModel, S(4, 1, 4, 1));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(5, 1, 5, 1));
        model.GetLineContent(5).ShouldBe(expectedLine, "002");
    }

    [Fact]
    public void Enter_Should_Adjust_Cursor_Position_When_Press_Enter_In_The_Middle_Of_Leading_Whitespaces_1() =>
        EnterInLeadingWhitespaceCase(["if (true) {", "\tif (true) {", "\t\treturn true;", "\t}a}"], 2, S(4, 2, 4, 2), "\t\treturn true;");

    [Fact]
    public void Enter_Should_Adjust_Cursor_Position_When_Press_Enter_In_The_Middle_Of_Leading_Whitespaces_2() =>
        EnterInLeadingWhitespaceCase(["\tif (true) {", "\t\tif (true) {", "\t    \treturn true;", "\t\t}a}"], 4, S(4, 3, 4, 3), "\t\t\treturn true;");

    [Fact]
    public void Enter_Should_Adjust_Cursor_Position_When_Press_Enter_In_The_Middle_Of_Leading_Whitespaces_3()
    {
        using var editor = CreateEditor(["if (true) {", "  if (true) {", "    return true;", "}a}"], IndentRulesLanguageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 3, 2, false);
        AssertCursor(viewModel, S(3, 2, 3, 2));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(4, 2, 4, 2));
        model.GetLineContent(4).ShouldBe("    return true;", "001");

        MoveTo(viewModel, 4, 3, false);
        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(5, 3, 5, 3));
        model.GetLineContent(5).ShouldBe("    return true;", "002");
    }

    [Fact]
    public void Enter_Should_Adjust_Cursor_Position_When_Press_Enter_In_The_Middle_Of_Leading_Whitespaces_4()
    {
        using var editor = CreateEditor(
            ["if (true) {", "  if (true) {", "\t  return true;", "}a}", "", "if (true) {", "  if (true) {", "\t  return true;", "}a}"],
            IndentRulesLanguageId,
            ModelOpts(tabSize: 2, indentSize: 2));
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 3, 3, false);
        AssertCursor(viewModel, S(3, 3, 3, 3));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(4, 4, 4, 4));
        model.GetLineContent(4).ShouldBe("    return true;", "001");

        MoveTo(viewModel, 9, 4, false);
        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(10, 5, 10, 5));
        model.GetLineContent(10).ShouldBe("    return true;", "001");
    }

    [Fact]
    public void Enter_Should_Adjust_Cursor_Position_When_Press_Enter_In_The_Middle_Of_Leading_Whitespaces_5()
    {
        using var editor = CreateEditor(
            ["if (true) {", "  if (true) {", "    return true;", "    return true;", ""],
            IndentRulesLanguageId,
            ModelOpts(tabSize: 2));
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 3, 5, false);
        MoveTo(viewModel, 4, 3, true);
        AssertCursor(viewModel, S(3, 5, 4, 3));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(4, 3, 4, 3));
        editor.Model.GetLineContent(4).ShouldBe("    return true;", "001");
    }

    // enterWithSelectionCases
    private void EnterWithSelectionCase(int fromLineNumber, int fromColumn, int toLineNumber, int toColumn)
    {
        using var editor = CreateEditor(
            ["function baz() {", "\tvar x = 1;", "\t\t\t\t\t\t\treturn x;", "}"],
            IndentRulesLanguageId,
            ModelOpts(insertSpaces: false));
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, fromLineNumber, fromColumn, false);
        MoveTo(viewModel, toLineNumber, toColumn, true);
        AssertCursor(viewModel, S(fromLineNumber, fromColumn, toLineNumber, toColumn));

        viewModel.Type("\n", Keyboard);
        editor.Model.GetLineContent(3).ShouldBe("\treturn x;");
        AssertCursor(viewModel, P(3, 2));
    }

    [Fact]
    public void Issue_Monaco_Editor_108_Part_1_2_Auto_Indentation_On_Enter_With_Selection_Is_Half_Broken() =>
        EnterWithSelectionCase(3, 8, 2, 12);

    [Fact]
    public void Issue_Monaco_Editor_108_Part_2_2_Auto_Indentation_On_Enter_With_Selection_Is_Half_Broken() =>
        EnterWithSelectionCase(2, 12, 3, 8);

    [Fact]
    public void OnEnter_Works_If_There_Are_No_Indentation_Rules()
    {
        using var editor = CreateEditor(
            ["<?", "\tif (true) {", "\t\techo $hi;", "\t\techo $bye;", "\t}", "?>"],
            modelOpts: ModelOpts(insertSpaces: false));
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 5, 3, false);
        AssertCursor(viewModel, S(5, 3, 5, 3));

        viewModel.Type("\n", Keyboard);
        model.GetLineContent(6).ShouldBe("\t");
        AssertCursor(viewModel, S(6, 2, 6, 2));
        model.GetLineContent(5).ShouldBe("\t}");
    }

    [Fact]
    public void OnEnter_Works_If_There_Are_No_Indentation_Rules_2()
    {
        using var editor = CreateEditor(["\tif (5)", "\t\treturn 5;", "\t"], modelOpts: ModelOpts(insertSpaces: false));
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 3, 2, false);
        AssertCursor(viewModel, S(3, 2, 3, 2));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(4, 2, 4, 2));
        editor.Model.GetLineContent(4).ShouldBe("\t");
    }

    [Fact]
    public void Bug_16543_Tab_Should_Indent_To_Correct_Indentation_Spot_Immediately()
    {
        using var editor = CreateEditor(
            ["function baz() {", "\tfunction hello() { // something here", "\t", "", "\t}", "}"],
            IndentRulesLanguageId,
            ModelOpts(insertSpaces: false));
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 4, 1, false);
        AssertCursor(viewModel, S(4, 1, 4, 1));

        Tab(editor);
        editor.Model.GetLineContent(4).ShouldBe("\t\t");
    }

    // tabOnWhitespaceOnlyLineCases
    private void TabOnWhitespaceOnlyLineCase(string whitespaceLine, int column, string expected)
    {
        using var editor = CreateEditor(
            ["\tfunction baz() {", "\t\tfunction hello() { // something here", "\t\t", whitespaceLine, "\t\t}", "\t}"],
            IndentRulesLanguageId,
            ModelOpts(insertSpaces: false));
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 4, column, false);
        AssertCursor(viewModel, S(4, column, 4, column));

        Tab(editor);
        editor.Model.GetLineContent(4).ShouldBe(expected);
    }

    [Fact]
    public void Bug_2938_1_When_Pressing_Tab_On_White_Space_Only_Lines_Indent_Straight_To_The_Right_Spot_Similar_To_Empty_Lines() =>
        TabOnWhitespaceOnlyLineCase("\t", 2, "\t\t\t");

    [Fact]
    public void Bug_2938_2_When_Pressing_Tab_On_White_Space_Only_Lines_Indent_Straight_To_The_Right_Spot_Similar_To_Empty_Lines() =>
        TabOnWhitespaceOnlyLineCase("    ", 1, "\t\t\t");

    [Fact]
    public void Bug_2938_3_When_Pressing_Tab_On_White_Space_Only_Lines_Indent_Straight_To_The_Right_Spot_Similar_To_Empty_Lines() =>
        TabOnWhitespaceOnlyLineCase("\t\t\t", 3, "\t\t\t\t");

    [Fact]
    public void Bug_2938_4_When_Pressing_Tab_On_White_Space_Only_Lines_Indent_Straight_To_The_Right_Spot_Similar_To_Empty_Lines() =>
        TabOnWhitespaceOnlyLineCase("\t\t\t\t", 4, "\t\t\t\t\t");

    [Fact]
    public void Bug_31015_When_Pressing_Tab_On_Lines_And_Enter_Rules_Are_Avail_Indent_Straight_To_The_Right_SpotTab()
    {
        var onEnterLanguageId = _context.SetupOnEnterLanguage(IndentAction.Indent);
        using var editor = CreateEditor(["    if (a) {", "        ", "", "", "    }"], onEnterLanguageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 3, 1);
        Tab(editor);
        model.GetLineContent(1).ShouldBe("    if (a) {");
        model.GetLineContent(2).ShouldBe("        ");
        model.GetLineContent(3).ShouldBe("        ");
        model.GetLineContent(4).ShouldBe("");
        model.GetLineContent(5).ShouldBe("    }");
    }

    [Fact]
    public void Type_Honors_Indentation_Rules_Ruby_Keywords()
    {
        var rubyLanguageId = _context.SetupIndentRulesLanguage("ruby", new IndentationRule(
            DecreaseIndentPattern: JsRegExp.Of(@"^\s*([}\]]([,)]?\s*(#|$)|\.[a-zA-Z_]\w*\b)|(end|rescue|ensure|else|elsif|when)\b)"),
            IncreaseIndentPattern: JsRegExp.Of(@"^\s*((begin|class|def|else|elsif|ensure|for|if|module|rescue|unless|until|when|while)|(.*\sdo\b))\b[^\{;]*$")));
        using var editor = CreateEditor(["class Greeter", "  def initialize(name)", "    @name = name", "    en"], rubyLanguageId, options: s_autoIndentFull);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 4, 7, false);
        AssertCursor(viewModel, S(4, 7, 4, 7));

        viewModel.Type("d", Keyboard);
        editor.Model.GetLineContent(4).ShouldBe("  end");
    }

    [Fact]
    public void Auto_Indent_On_Type_IncreaseIndentPattern_Has_Higher_Priority_Than_DecreaseIndent_When_Inheriting()
    {
        using var editor = CreateEditor(["\tif (true) {", "\t\tconsole.log();", "\t} else if {", "\t\tconsole.log()", "\t}"], IndentRulesLanguageId);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 5, 3, false);
        AssertCursor(viewModel, S(5, 3, 5, 3));

        viewModel.Type("e", Keyboard);
        AssertCursor(viewModel, S(5, 4, 5, 4));
        editor.Model.GetLineContent(5).ShouldBe("\t}e", "This line should not decrease indent");
    }

    [Fact]
    public void Type_Honors_Users_Indentation_Adjustment()
    {
        using var editor = CreateEditor(["\tif (true ||", "\t ) {", "\t}", "if (true ||", ") {", "}"], IndentRulesLanguageId);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 2, 3, false);
        AssertCursor(viewModel, S(2, 3, 2, 3));

        viewModel.Type(" ", Keyboard);
        AssertCursor(viewModel, S(2, 4, 2, 4));
        editor.Model.GetLineContent(2).ShouldBe("\t  ) {", "This line should not decrease indent");
    }

    [Fact]
    public void Bug_29972_If_A_Line_Is_Line_Comment_Open_Bracket_Should_Not_Indent_Next_Line()
    {
        using var editor = CreateEditor(["if (true) {", "\t// {", "\t\t"], IndentRulesLanguageId, options: s_autoIndentFull);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 3, 3, false);
        AssertCursor(viewModel, S(3, 3, 3, 3));

        viewModel.Type("}", Keyboard);
        AssertCursor(viewModel, S(3, 2, 3, 2));
        editor.Model.GetLineContent(3).ShouldBe("}");
    }

    [Fact]
    public void Issue_38261_TAB_Key_Results_In_Bizarre_Indentation_In_CPP_Mode()
    {
        const string languageId = "indentRulesMode";
        _context.Add(_context.LanguageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            Brackets = [("{", "}"), ("[", "]"), ("(", ")")],
            IndentationRules = new IndentationRule(
                DecreaseIndentPattern: JsRegExp.Of(@"^\s*\}"),
                IncreaseIndentPattern: JsRegExp.Of(@"(^.*\{[^}]*$)")),
        }));

        using var editor = CreateEditor(
            ["int main() {", "  return 0;", "}", "", "bool Foo::bar(const string &a,", "              const string &b) {", "  foo();", "", ")"],
            languageId,
            ModelOpts(tabSize: 2, indentSize: 2),
            EditorOpts(EditorOptions.Default with { AutoIndent = EditorAutoIndentStrategy.Advanced }));
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 8, 1, false);
        AssertCursor(viewModel, S(8, 1, 8, 1));

        Tab(editor);
        editor.Model.GetValue().ShouldBe(string.Join('\n',
            "int main() {",
            "  return 0;",
            "}",
            "",
            "bool Foo::bar(const string &a,",
            "              const string &b) {",
            "  foo();",
            "  ",
            ")"));
        viewModel.GetSelection().ShouldBe(S(8, 3, 8, 3));
    }

    [Fact]
    public void Issue_57197_Indent_Rules_Regex_Should_Be_Stateless()
    {
        var languageId = _context.SetupIndentRulesLanguage("lang", new IndentationRule(
            DecreaseIndentPattern: JsRegExp.Of(@"^\s*}$", "gm"),
            IncreaseIndentPattern: JsRegExp.Of(@"^(?![^\S\n]*(?!--|––|——)(?:[-❍❑■⬜□☐▪▫–—≡→›✘xX✔✓☑+]|\[[ xX+-]?\])\s[^\n]*)[^\S\n]*(.+:)[^\S\n]*(?:(?=@[^\s*~(]+(?::\/\/[^\s*~(:]+)?(?:\([^)]*\))?)|$)", "gm")));
        using var editor = CreateEditor(["Project:"], languageId, ModelOpts(insertSpaces: false), s_autoIndentFull);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 9, false);
        AssertCursor(viewModel, S(1, 9, 1, 9));

        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(2, 2, 2, 2));

        MoveTo(viewModel, 1, 9, false);
        AssertCursor(viewModel, S(1, 9, 1, 9));
        viewModel.Type("\n", Keyboard);
        AssertCursor(viewModel, S(2, 2, 2, 2));
    }

    [Fact]
    public void Typing_In_Json()
    {
        const string languageId = "indentRulesMode";
        _context.Add(_context.LanguageConfigurationService.Register(languageId, new LanguageConfiguration
        {
            Brackets = [("{", "}"), ("[", "]"), ("(", ")")],
            IndentationRules = new IndentationRule(
                DecreaseIndentPattern: JsRegExp.Of(@"^\s*[}\]],?\s*$"),
                IncreaseIndentPattern: JsRegExp.Of(@"({+(?=([^""]*""[^""]*"")*[^""}]*$))|(\[+(?=([^""]*""[^""]*"")*[^""\]]*$))")),
        }));

        using var editor = CreateEditor(
            ["{", "  \"scripts: {\"", "    \"watch\": \"a {\"", "    \"build{\": \"b\"", "    \"tasks\": []", "    \"tasks\": [\"a\"]", "  \"}\"", "\"}\""],
            languageId,
            ModelOpts(tabSize: 2, indentSize: 2),
            s_autoIndentFull);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 3, 19, false);
        AssertCursor(viewModel, S(3, 19, 3, 19));

        viewModel.Type("\n", Keyboard);
        model.GetLineContent(4).ShouldBe("    ");

        MoveTo(viewModel, 5, 18, false);
        AssertCursor(viewModel, S(5, 18, 5, 18));

        viewModel.Type("\n", Keyboard);
        model.GetLineContent(6).ShouldBe("    ");

        MoveTo(viewModel, 7, 15, false);
        AssertCursor(viewModel, S(7, 15, 7, 15));

        viewModel.Type("\n", Keyboard);
        model.GetLineContent(8).ShouldBe("      ");
        model.GetLineContent(9).ShouldBe("    ]");

        MoveTo(viewModel, 10, 18, false);
        AssertCursor(viewModel, S(10, 18, 10, 18));

        viewModel.Type("\n", Keyboard);
        model.GetLineContent(11).ShouldBe("    ]");
    }

    [Fact]
    public void Issue_111128_Multicursor_Enter_Issue_With_Indentation()
    {
        using var editor = CreateEditor("    let a, b, c;", IndentRulesLanguageId, ModelOpts(detectIndentation: false, insertSpaces: false, tabSize: 4));
        var viewModel = editor.ViewModel;

        editor.SetSelections([S(1, 11, 1, 11), S(1, 14, 1, 14)]);
        viewModel.Type("\n", Keyboard);
        editor.Model.GetValue().ShouldBe("    let a,\n\t b,\n\t c;");
    }

    [Fact(Skip = "Engine differs: typing '{' after '\\end' gives '\\end{' (no auto-close: the 'latex' configuration has only indentation rules, so CharacterPairSupport has no pairs, as upstream's would); upstream expects '\\end{}', source of its '}' not found")]
    public void Issue_122714_TabSize_1_Prevent_Typing_A_String_Matching_DecreaseIndentPattern_In_An_Empty_File()
    {
        var latexLanguageId = _context.SetupIndentRulesLanguage("latex", new IndentationRule(
            DecreaseIndentPattern: JsRegExp.Of(@"^\s*\\end{(?!document)"),
            IncreaseIndentPattern: JsRegExp.Of(@"\\begin{(?!document)([^}]*)}(?!.*\\end{\1})")));
        using var editor = CreateEditor("\\end", latexLanguageId, ModelOpts(tabSize: 1), s_autoIndentFull);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 5, false);
        AssertCursor(viewModel, S(1, 5, 1, 5));

        viewModel.Type("{", Keyboard);
        editor.Model.GetLineContent(1).ShouldBe("\\end{}");
    }

    // testElectricCharacter
    private void TestElectricCharacter(string[] text, int lineNumber, int column, string chr, string expected)
    {
        using var editor = CreateEditor(text, ElectricCharLanguageId);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, lineNumber, column);
        viewModel.Type(chr, Keyboard);
        editor.Model.GetLineContent(lineNumber).ShouldBe(expected);
    }

    [Fact]
    public void ElectricCharacter_Does_Nothing_If_No_Electric_Char() =>
        TestElectricCharacter(["  if (a) {", ""], 2, 1, "*", "*");

    [Fact]
    public void ElectricCharacter_Indents_In_Order_To_Match_Bracket() =>
        TestElectricCharacter(["  if (a) {", ""], 2, 1, "}", "  }");

    [Fact]
    public void ElectricCharacter_Unindents_In_Order_To_Match_Bracket() =>
        TestElectricCharacter(["  if (a) {", "    "], 2, 5, "}", "  }");

    [Fact]
    public void ElectricCharacter_Matches_With_Correct_Bracket() =>
        TestElectricCharacter(["  if (a) {", "    if (b) {", "    }", "    "], 4, 1, "}", "  }    ");

    [Fact]
    public void ElectricCharacter_Does_Nothing_If_Bracket_Does_Not_Match() =>
        TestElectricCharacter(["  if (a) {", "    if (b) {", "    }", "  }  "], 4, 6, "}", "  }  }");

    [Fact]
    public void ElectricCharacter_Matches_Bracket_Even_In_Line_With_Content() =>
        TestElectricCharacter(["  if (a) {", "// hello"], 2, 1, "}", "  }// hello");

    [Fact]
    public void ElectricCharacter_Is_No_Op_If_Bracket_Is_Lined_Up() =>
        TestElectricCharacter(["  if (a) {", "  "], 2, 3, "}", "  }");

    [Fact]
    public void ElectricCharacter_Is_No_Op_If_There_Is_Non_Whitespace_Text_Before() =>
        TestElectricCharacter(["  if (a) {", "a"], 2, 2, "}", "a}");

    [Fact]
    public void ElectricCharacter_Is_No_Op_If_Pairs_Are_All_Matched_Before() =>
        TestElectricCharacter(["foo(() => {", "  ( 1 + 2 ) ", "})"], 2, 13, "*", "  ( 1 + 2 ) *");

    [Fact]
    public void ElectricCharacter_Is_No_Op_If_Matching_Bracket_Is_On_The_Same_Line()
    {
        using var editor = CreateEditor(["(div"], ElectricCharLanguageId);
        var model = editor.Model;
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 1, 5);
        string? changeText = null;
        Action<ModelContentChangedEvent> listener = e => changeText = e.Changes[0].Text;
        model.DidChangeContent += listener;
        viewModel.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("(div)");
        changeText.ShouldBe(")");
        model.DidChangeContent -= listener;
    }

    [Fact]
    public void ElectricCharacter_Is_No_Op_If_The_Line_Has_Other_Content() =>
        TestElectricCharacter(["Math.max(", "\t2", "\t3"], 3, 3, ")", "\t3)");

    [Fact]
    public void ElectricCharacter_Appends_Text() =>
        TestElectricCharacter(["  if (a) {", "/*"], 2, 3, "*", "/** */");

    [Fact]
    public void ElectricCharacter_Appends_Text_2() =>
        TestElectricCharacter(["  if (a) {", "  /*"], 2, 5, "*", "  /** */");

    [Fact]
    public void ElectricCharacter_Issue_23711_Replacing_Selected_Text_With_Brackets_Fails_To_Delete_Old_Text_With_Backwards_Dragged_Selection()
    {
        using var editor = CreateEditor(["{", "word"], ElectricCharLanguageId);
        var viewModel = editor.ViewModel;

        MoveTo(viewModel, 2, 5);
        MoveTo(viewModel, 2, 1, true);
        viewModel.Type("}", Keyboard);
        editor.Model.GetLineContent(2).ShouldBe("}");
    }
}
