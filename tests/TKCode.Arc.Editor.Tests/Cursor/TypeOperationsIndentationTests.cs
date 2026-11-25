// Tests for Enter, Tab and auto-indentation on type, and for electric characters, ported from vscode
// (src/vs/editor/test/browser/controller/cursor.test.ts, suite 'Editor Controller', commit b57d5c9e), MIT license:
// the tests from 'Enter honors increaseIndentPattern' to 'ElectricCharacter - issue #23711'. Upstream types through the
// view model of a test code editor; here the typing goes through TypeOperations on OperationsTestEditor.
//
// model.tokenization.forceTokenization has nothing to do here (the tokens are always accurate) and is left out.
// Not ported: 'issue #122714: tabSize=1 prevent typing a string matching decreaseIndentPattern in an empty file'
// expects '\end{' + '}' after typing '{', but the 'latex' language it registers has only indentation rules (no
// brackets or auto-closing pairs, so nothing auto-closes '{'); here the line becomes '\end{'. Where upstream's '}'
// comes from could not be determined without running upstream.

using Shouldly;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class TypeOperationsIndentationTests : IDisposable
{
    private const string Keyboard = OperationsTestEditor.Keyboard;
    private const string IndentRulesLanguageId = TypingTestContext.IndentRulesLanguageId;

    private static readonly EditorOptions s_autoIndentFull = EditorOptions.Default with { AutoIndent = EditorAutoIndentStrategy.Full };

    private readonly TypingTestContext _context = new();

    public void Dispose() => _context.Dispose();

    private static TextSelection S(int selectionStartLineNumber, int selectionStartColumn, int positionLineNumber, int positionColumn) =>
        new(selectionStartLineNumber, selectionStartColumn, positionLineNumber, positionColumn);

    private static TextModelCreationOptions ModelOpts(bool? insertSpaces = null, int? tabSize = null, int? indentSize = null, bool? detectIndentation = null) =>
        TypingTestContext.ModelOpts(insertSpaces, tabSize, indentSize, detectIndentation: detectIndentation);

    [Fact]
    public void Enter_Honors_IncreaseIndentPattern()
    {
        using var editor = _context.UsingCursor(["if (true) {", "\tif (true) {"], IndentRulesLanguageId, ModelOpts(insertSpaces: false), s_autoIndentFull);

        editor.MoveTo(1, 12, false);
        editor.AssertCursor(S(1, 12, 1, 12));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(2, 2, 2, 2));

        editor.MoveTo(3, 13, false);
        editor.AssertCursor(S(3, 13, 3, 13));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(4, 3, 4, 3));
    }

    [Fact]
    public void Type_Honors_DecreaseIndentPattern()
    {
        using var editor = _context.UsingCursor(["if (true) {", "\t"], IndentRulesLanguageId, editorOpts: s_autoIndentFull);

        editor.MoveTo(2, 2, false);
        editor.AssertCursor(S(2, 2, 2, 2));

        editor.Type("}", Keyboard);
        editor.AssertCursor(S(2, 2, 2, 2));
        editor.Model.GetLineContent(2).ShouldBe("}", "001");
    }

    [Fact]
    public void Enter_Honors_UnIndentedLinePattern()
    {
        using var editor = _context.UsingCursor(["if (true) {", "\t\t\treturn true"], IndentRulesLanguageId, ModelOpts(insertSpaces: false), s_autoIndentFull);

        editor.MoveTo(2, 15, false);
        editor.AssertCursor(S(2, 15, 2, 15));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(3, 2, 3, 2));
    }

    [Fact]
    public void Enter_Honors_IndentNextLinePattern()
    {
        using var editor = _context.UsingCursor(
            ["if (true)", "\treturn true;", "if (true)", "\t\t\t\treturn true"],
            IndentRulesLanguageId,
            ModelOpts(insertSpaces: false),
            s_autoIndentFull);

        editor.MoveTo(2, 14, false);
        editor.AssertCursor(S(2, 14, 2, 14));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(3, 1, 3, 1));

        editor.MoveTo(5, 16, false);
        editor.AssertCursor(S(5, 16, 5, 16));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(6, 2, 6, 2));
    }

    [Fact]
    public void Enter_Honors_IndentNextLinePattern_2()
    {
        using var editor = _context.CreateEditor(string.Join('\n', "if (true)", "\tif (true)"), IndentRulesLanguageId, ModelOpts(insertSpaces: false), s_autoIndentFull);

        editor.MoveTo(2, 11, false);
        editor.AssertCursor(S(2, 11, 2, 11));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(3, 3, 3, 3));

        editor.Type("console.log();", Keyboard);
        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(4, 1, 4, 1));
    }

    [Fact]
    public void Enter_Honors_Intential_Indent()
    {
        using var editor = _context.UsingCursor(["if (true) {", "\tif (true) {", "return true;", "}}"], IndentRulesLanguageId, editorOpts: s_autoIndentFull);

        editor.MoveTo(3, 13, false);
        editor.AssertCursor(S(3, 13, 3, 13));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(4, 1, 4, 1));
        editor.Model.GetLineContent(3).ShouldBe("return true;", "001");
    }

    [Fact]
    public void Enter_Supports_Selection_1()
    {
        using var editor = _context.UsingCursor(["if (true) {", "\tif (true) {", "\t\treturn true;", "\t}a}"], IndentRulesLanguageId, ModelOpts(insertSpaces: false));

        editor.MoveTo(4, 3, false);
        editor.MoveTo(4, 4, true);
        editor.AssertCursor(S(4, 3, 4, 4));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(5, 1, 5, 1));
        editor.Model.GetLineContent(4).ShouldBe("\t}", "001");
    }

    [Fact]
    public void Enter_Supports_Selection_2()
    {
        using var editor = _context.UsingCursor(["if (true) {", "\tif (true) {"], IndentRulesLanguageId, ModelOpts(insertSpaces: false));

        editor.MoveTo(2, 12, false);
        editor.MoveTo(2, 13, true);
        editor.AssertCursor(S(2, 12, 2, 13));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(3, 3, 3, 3));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(4, 3, 4, 3));
    }

    [Fact]
    public void Enter_Honors_TabSize_And_InsertSpaces_1()
    {
        using var editor = _context.UsingCursor(["if (true) {", "\tif (true) {"], IndentRulesLanguageId);

        editor.MoveTo(1, 12, false);
        editor.AssertCursor(S(1, 12, 1, 12));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(2, 5, 2, 5));

        editor.MoveTo(3, 13, false);
        editor.AssertCursor(S(3, 13, 3, 13));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(4, 9, 4, 9));
    }

    public static TheoryData<string, bool, TextSelection, TextSelection> EnterHonorsTabSizeCases => new()
    {
        { "Enter honors tabSize and insertSpaces 2", true, S(2, 5, 2, 5), S(4, 9, 4, 9) },
        { "Enter honors tabSize and insertSpaces 3", false, S(2, 2, 2, 2), S(4, 3, 4, 3) },
    };

    [Theory]
    [MemberData(nameof(EnterHonorsTabSizeCases))]
    public void Enter_Honors_TabSize_And_InsertSpaces(string name, bool insertSpaces, TextSelection firstExpected, TextSelection secondExpected)
    {
        _ = name;
        using var editor = _context.UsingCursor(["if (true) {", "    if (true) {"], IndentRulesLanguageId, ModelOpts(insertSpaces: insertSpaces));

        editor.MoveTo(1, 12, false);
        editor.AssertCursor(S(1, 12, 1, 12));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(firstExpected);

        editor.MoveTo(3, 16, false);
        editor.AssertCursor(S(3, 16, 3, 16));

        editor.Type("\n", Keyboard);
        editor.Model.GetLineContent(3).ShouldBe("    if (true) {");
        editor.AssertCursor(secondExpected);
    }

    [Fact]
    public void Enter_Supports_Intentional_Indentation()
    {
        using var editor = _context.UsingCursor(
            ["\tif (true) {", "\t\tswitch(true) {", "\t\t\tcase true:", "\t\t\t\tbreak;", "\t\t}", "\t}"],
            IndentRulesLanguageId,
            ModelOpts(insertSpaces: false),
            s_autoIndentFull);

        editor.MoveTo(5, 4, false);
        editor.AssertCursor(S(5, 4, 5, 4));

        editor.Type("\n", Keyboard);
        editor.Model.GetLineContent(5).ShouldBe("\t\t}");
        editor.AssertCursor(S(6, 3, 6, 3));
    }

    public static TheoryData<string, string[], bool, int, TextSelection, string> EnterInMiddleOfLineCases => new()
    {
        { "Enter should not adjust cursor position when press enter in the middle of a line 1", ["if (true) {", "\tif (true) {", "\t\treturn true;", "\t}a}"], false, 9, S(4, 3, 4, 3), "\t\t true;" },
        { "Enter should not adjust cursor position when press enter in the middle of a line 2", ["if (true) {", "\tif (true) {", "\t\treturn true;", "\t}a}"], false, 3, S(4, 3, 4, 3), "\t\treturn true;" },
        { "Enter should not adjust cursor position when press enter in the middle of a line 3", ["if (true) {", "  if (true) {", "    return true;", "  }a}"], true, 11, S(4, 5, 4, 5), "     true;" },
    };

    [Theory]
    [MemberData(nameof(EnterInMiddleOfLineCases))]
    public void Enter_Should_Not_Adjust_Cursor_Position_When_Press_Enter_In_The_Middle_Of_A_Line(
        string name, string[] text, bool insertSpaces, int column, TextSelection expectedSelection, string expectedLine)
    {
        _ = name;
        using var editor = _context.UsingCursor(text, IndentRulesLanguageId, ModelOpts(insertSpaces: insertSpaces));

        editor.MoveTo(3, column, false);
        editor.AssertCursor(S(3, column, 3, column));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(expectedSelection);
        editor.Model.GetLineContent(4).ShouldBe(expectedLine, "001");
    }

    public static TheoryData<string, string[], int, TextSelection, string> EnterInLeadingWhitespaceCases => new()
    {
        { "Enter should adjust cursor position when press enter in the middle of leading whitespaces 1", ["if (true) {", "\tif (true) {", "\t\treturn true;", "\t}a}"], 2, S(4, 2, 4, 2), "\t\treturn true;" },
        { "Enter should adjust cursor position when press enter in the middle of leading whitespaces 2", ["\tif (true) {", "\t\tif (true) {", "\t    \treturn true;", "\t\t}a}"], 4, S(4, 3, 4, 3), "\t\t\treturn true;" },
    };

    [Theory]
    [MemberData(nameof(EnterInLeadingWhitespaceCases))]
    public void Enter_Should_Adjust_Cursor_Position_When_Press_Enter_In_The_Middle_Of_Leading_Whitespaces(
        string name, string[] text, int column, TextSelection expectedSelection, string expectedLine)
    {
        _ = name;
        using var editor = _context.UsingCursor(text, IndentRulesLanguageId, ModelOpts(insertSpaces: false));
        var model = editor.Model;

        editor.MoveTo(3, column, false);
        editor.AssertCursor(S(3, column, 3, column));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(expectedSelection);
        model.GetLineContent(4).ShouldBe(expectedLine, "001");

        editor.MoveTo(4, 1, false);
        editor.AssertCursor(S(4, 1, 4, 1));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(5, 1, 5, 1));
        model.GetLineContent(5).ShouldBe(expectedLine, "002");
    }

    [Fact]
    public void Enter_Should_Adjust_Cursor_Position_When_Press_Enter_In_The_Middle_Of_Leading_Whitespaces_3()
    {
        using var editor = _context.UsingCursor(["if (true) {", "  if (true) {", "    return true;", "}a}"], IndentRulesLanguageId);
        var model = editor.Model;

        editor.MoveTo(3, 2, false);
        editor.AssertCursor(S(3, 2, 3, 2));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(4, 2, 4, 2));
        model.GetLineContent(4).ShouldBe("    return true;", "001");

        editor.MoveTo(4, 3, false);
        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(5, 3, 5, 3));
        model.GetLineContent(5).ShouldBe("    return true;", "002");
    }

    [Fact]
    public void Enter_Should_Adjust_Cursor_Position_When_Press_Enter_In_The_Middle_Of_Leading_Whitespaces_4()
    {
        using var editor = _context.UsingCursor(
            ["if (true) {", "  if (true) {", "\t  return true;", "}a}", "", "if (true) {", "  if (true) {", "\t  return true;", "}a}"],
            IndentRulesLanguageId,
            ModelOpts(tabSize: 2, indentSize: 2));
        var model = editor.Model;

        editor.MoveTo(3, 3, false);
        editor.AssertCursor(S(3, 3, 3, 3));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(4, 4, 4, 4));
        model.GetLineContent(4).ShouldBe("    return true;", "001");

        editor.MoveTo(9, 4, false);
        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(10, 5, 10, 5));
        model.GetLineContent(10).ShouldBe("    return true;", "001");
    }

    [Fact]
    public void Enter_Should_Adjust_Cursor_Position_When_Press_Enter_In_The_Middle_Of_Leading_Whitespaces_5()
    {
        using var editor = _context.UsingCursor(
            ["if (true) {", "  if (true) {", "    return true;", "    return true;", ""],
            IndentRulesLanguageId,
            ModelOpts(tabSize: 2));

        editor.MoveTo(3, 5, false);
        editor.MoveTo(4, 3, true);
        editor.AssertCursor(S(3, 5, 4, 3));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(4, 3, 4, 3));
        editor.Model.GetLineContent(4).ShouldBe("    return true;", "001");
    }

    public static TheoryData<string, int, int, int, int> EnterWithSelectionCases => new()
    {
        { "issue microsoft/monaco-editor#108 part 1/2: Auto indentation on Enter with selection is half broken", 3, 8, 2, 12 },
        { "issue microsoft/monaco-editor#108 part 2/2: Auto indentation on Enter with selection is half broken", 2, 12, 3, 8 },
    };

    [Theory]
    [MemberData(nameof(EnterWithSelectionCases))]
    public void Issue_Monaco_Editor_108_Auto_Indentation_On_Enter_With_Selection_Is_Half_Broken(
        string name, int fromLineNumber, int fromColumn, int toLineNumber, int toColumn)
    {
        _ = name;
        using var editor = _context.UsingCursor(
            ["function baz() {", "\tvar x = 1;", "\t\t\t\t\t\t\treturn x;", "}"],
            IndentRulesLanguageId,
            ModelOpts(insertSpaces: false));

        editor.MoveTo(fromLineNumber, fromColumn, false);
        editor.MoveTo(toLineNumber, toColumn, true);
        editor.AssertCursor(S(fromLineNumber, fromColumn, toLineNumber, toColumn));

        editor.Type("\n", Keyboard);
        editor.Model.GetLineContent(3).ShouldBe("\treturn x;");
        editor.AssertCursor(new TextPosition(3, 2));
    }

    [Fact]
    public void OnEnter_Works_If_There_Are_No_Indentation_Rules()
    {
        using var editor = _context.UsingCursor(
            ["<?", "\tif (true) {", "\t\techo $hi;", "\t\techo $bye;", "\t}", "?>"],
            modelOpts: ModelOpts(insertSpaces: false));
        var model = editor.Model;

        editor.MoveTo(5, 3, false);
        editor.AssertCursor(S(5, 3, 5, 3));

        editor.Type("\n", Keyboard);
        model.GetLineContent(6).ShouldBe("\t");
        editor.AssertCursor(S(6, 2, 6, 2));
        model.GetLineContent(5).ShouldBe("\t}");
    }

    [Fact]
    public void OnEnter_Works_If_There_Are_No_Indentation_Rules_2()
    {
        using var editor = _context.UsingCursor(["\tif (5)", "\t\treturn 5;", "\t"], modelOpts: ModelOpts(insertSpaces: false));

        editor.MoveTo(3, 2, false);
        editor.AssertCursor(S(3, 2, 3, 2));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(4, 2, 4, 2));
        editor.Model.GetLineContent(4).ShouldBe("\t");
    }

    [Fact]
    public void Bug_16543_Tab_Should_Indent_To_Correct_Indentation_Spot_Immediately()
    {
        using var editor = _context.CreateEditor(
            string.Join('\n', "function baz() {", "\tfunction hello() { // something here", "\t", "", "\t}", "}"),
            IndentRulesLanguageId,
            ModelOpts(insertSpaces: false));

        editor.MoveTo(4, 1, false);
        editor.AssertCursor(S(4, 1, 4, 1));

        editor.Tab();
        editor.Model.GetLineContent(4).ShouldBe("\t\t");
    }

    public static TheoryData<string, string, int, string> TabOnWhitespaceOnlyLineCases => new()
    {
        { "bug #2938 (1): When pressing Tab on white-space only lines, indent straight to the right spot (similar to empty lines)", "\t", 2, "\t\t\t" },
        { "bug #2938 (2): When pressing Tab on white-space only lines, indent straight to the right spot (similar to empty lines)", "    ", 1, "\t\t\t" },
        { "bug #2938 (3): When pressing Tab on white-space only lines, indent straight to the right spot (similar to empty lines)", "\t\t\t", 3, "\t\t\t\t" },
        { "bug #2938 (4): When pressing Tab on white-space only lines, indent straight to the right spot (similar to empty lines)", "\t\t\t\t", 4, "\t\t\t\t\t" },
    };

    [Theory]
    [MemberData(nameof(TabOnWhitespaceOnlyLineCases))]
    public void Bug_2938_When_Pressing_Tab_On_White_Space_Only_Lines_Indent_Straight_To_The_Right_Spot(
        string name, string whitespaceLine, int column, string expected)
    {
        _ = name;
        using var editor = _context.CreateEditor(
            string.Join('\n', "\tfunction baz() {", "\t\tfunction hello() { // something here", "\t\t", whitespaceLine, "\t\t}", "\t}"),
            IndentRulesLanguageId,
            ModelOpts(insertSpaces: false));

        editor.MoveTo(4, column, false);
        editor.AssertCursor(S(4, column, 4, column));

        editor.Tab();
        editor.Model.GetLineContent(4).ShouldBe(expected);
    }

    [Fact]
    public void Bug_31015_When_Pressing_Tab_On_Lines_And_Enter_Rules_Are_Avail_Indent_Straight_To_The_Right_SpotTab()
    {
        var onEnterLanguageId = _context.SetupOnEnterLanguage(IndentAction.Indent);
        using var editor = _context.CreateEditor(string.Join('\n', "    if (a) {", "        ", "", "", "    }"), onEnterLanguageId);
        var model = editor.Model;

        editor.MoveTo(3, 1);
        editor.Tab();
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
        using var editor = _context.CreateEditor(
            string.Join('\n', "class Greeter", "  def initialize(name)", "    @name = name", "    en"),
            rubyLanguageId,
            editorOpts: s_autoIndentFull);

        editor.MoveTo(4, 7, false);
        editor.AssertCursor(S(4, 7, 4, 7));

        editor.Type("d", Keyboard);
        editor.Model.GetLineContent(4).ShouldBe("  end");
    }

    [Fact]
    public void Auto_Indent_On_Type_IncreaseIndentPattern_Has_Higher_Priority_Than_DecreaseIndent_When_Inheriting()
    {
        using var editor = _context.UsingCursor(
            ["\tif (true) {", "\t\tconsole.log();", "\t} else if {", "\t\tconsole.log()", "\t}"],
            IndentRulesLanguageId);

        editor.MoveTo(5, 3, false);
        editor.AssertCursor(S(5, 3, 5, 3));

        editor.Type("e", Keyboard);
        editor.AssertCursor(S(5, 4, 5, 4));
        editor.Model.GetLineContent(5).ShouldBe("\t}e", "This line should not decrease indent");
    }

    [Fact]
    public void Type_Honors_Users_Indentation_Adjustment()
    {
        using var editor = _context.UsingCursor(
            ["\tif (true ||", "\t ) {", "\t}", "if (true ||", ") {", "}"],
            IndentRulesLanguageId);

        editor.MoveTo(2, 3, false);
        editor.AssertCursor(S(2, 3, 2, 3));

        editor.Type(" ", Keyboard);
        editor.AssertCursor(S(2, 4, 2, 4));
        editor.Model.GetLineContent(2).ShouldBe("\t  ) {", "This line should not decrease indent");
    }

    [Fact]
    public void Bug_29972_If_A_Line_Is_Line_Comment_Open_Bracket_Should_Not_Indent_Next_Line()
    {
        using var editor = _context.UsingCursor(["if (true) {", "\t// {", "\t\t"], IndentRulesLanguageId, editorOpts: s_autoIndentFull);

        editor.MoveTo(3, 3, false);
        editor.AssertCursor(S(3, 3, 3, 3));

        editor.Type("}", Keyboard);
        editor.AssertCursor(S(3, 2, 3, 2));
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

        using var editor = _context.CreateEditor(
            string.Join('\n', "int main() {", "  return 0;", "}", "", "bool Foo::bar(const string &a,", "              const string &b) {", "  foo();", "", ")"),
            languageId,
            ModelOpts(tabSize: 2, indentSize: 2),
            EditorOptions.Default with { AutoIndent = EditorAutoIndentStrategy.Advanced });

        editor.MoveTo(8, 1, false);
        editor.AssertCursor(S(8, 1, 8, 1));

        editor.Tab();
        editor.Model.GetValue().ShouldBe(string.Join('\n', "int main() {", "  return 0;", "}", "", "bool Foo::bar(const string &a,", "              const string &b) {", "  foo();", "  ", ")"));
        editor.GetSelection().ShouldBe(S(8, 3, 8, 3));
    }

    [Fact]
    public void Issue_57197_Indent_Rules_Regex_Should_Be_Stateless()
    {
        var languageId = _context.SetupIndentRulesLanguage("lang", new IndentationRule(
            DecreaseIndentPattern: JsRegExp.Of(@"^\s*}$", "gm"),
            IncreaseIndentPattern: JsRegExp.Of(@"^(?![^\S\n]*(?!--|––|——)(?:[-❍❑■⬜□☐▪▫–—≡→›✘xX✔✓☑+]|\[[ xX+-]?\])\s[^\n]*)[^\S\n]*(.+:)[^\S\n]*(?:(?=@[^\s*~(]+(?::\/\/[^\s*~(:]+)?(?:\([^)]*\))?)|$)", "gm")));
        using var editor = _context.UsingCursor(["Project:"], languageId, ModelOpts(insertSpaces: false), s_autoIndentFull);

        editor.MoveTo(1, 9, false);
        editor.AssertCursor(S(1, 9, 1, 9));

        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(2, 2, 2, 2));

        editor.MoveTo(1, 9, false);
        editor.AssertCursor(S(1, 9, 1, 9));
        editor.Type("\n", Keyboard);
        editor.AssertCursor(S(2, 2, 2, 2));
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

        using var editor = _context.CreateEditor(
            string.Join('\n', "{", "  \"scripts: {\"", "    \"watch\": \"a {\"", "    \"build{\": \"b\"", "    \"tasks\": []", "    \"tasks\": [\"a\"]", "  \"}\"", "\"}\""),
            languageId,
            ModelOpts(tabSize: 2, indentSize: 2),
            s_autoIndentFull);
        var model = editor.Model;

        editor.MoveTo(3, 19, false);
        editor.AssertCursor(S(3, 19, 3, 19));

        editor.Type("\n", Keyboard);
        model.GetLineContent(4).ShouldBe("    ");

        editor.MoveTo(5, 18, false);
        editor.AssertCursor(S(5, 18, 5, 18));

        editor.Type("\n", Keyboard);
        model.GetLineContent(6).ShouldBe("    ");

        editor.MoveTo(7, 15, false);
        editor.AssertCursor(S(7, 15, 7, 15));

        editor.Type("\n", Keyboard);
        model.GetLineContent(8).ShouldBe("      ");
        model.GetLineContent(9).ShouldBe("    ]");

        editor.MoveTo(10, 18, false);
        editor.AssertCursor(S(10, 18, 10, 18));

        editor.Type("\n", Keyboard);
        model.GetLineContent(11).ShouldBe("    ]");
    }

    [Fact]
    public void Issue_111128_Multicursor_Enter_Issue_With_Indentation()
    {
        using var editor = _context.CreateEditor("    let a, b, c;", IndentRulesLanguageId, ModelOpts(detectIndentation: false, insertSpaces: false, tabSize: 4));
        editor.SetSelectionsValidated(S(1, 11, 1, 11), S(1, 14, 1, 14));
        editor.Type("\n", Keyboard);
        editor.Model.GetValue().ShouldBe("    let a,\n\t b,\n\t c;");
    }

    private void TestElectricCharacter(string[] text, int lineNumber, int column, string chr, string expected)
    {
        using var editor = _context.UsingCursor(text, TypingTestContext.ElectricCharLanguageId);
        editor.MoveTo(lineNumber, column);
        editor.Type(chr, Keyboard);
        editor.Model.GetLineContent(lineNumber).ShouldBe(expected);
    }

    public static TheoryData<string, string[], int, int, string, string> ElectricCharacterCases => new()
    {
        { "ElectricCharacter - does nothing if no electric char", ["  if (a) {", ""], 2, 1, "*", "*" },
        { "ElectricCharacter - indents in order to match bracket", ["  if (a) {", ""], 2, 1, "}", "  }" },
        { "ElectricCharacter - unindents in order to match bracket", ["  if (a) {", "    "], 2, 5, "}", "  }" },
        { "ElectricCharacter - matches with correct bracket", ["  if (a) {", "    if (b) {", "    }", "    "], 4, 1, "}", "  }    " },
        { "ElectricCharacter - does nothing if bracket does not match", ["  if (a) {", "    if (b) {", "    }", "  }  "], 4, 6, "}", "  }  }" },
        { "ElectricCharacter - matches bracket even in line with content", ["  if (a) {", "// hello"], 2, 1, "}", "  }// hello" },
        { "ElectricCharacter - is no-op if bracket is lined up", ["  if (a) {", "  "], 2, 3, "}", "  }" },
        { "ElectricCharacter - is no-op if there is non-whitespace text before", ["  if (a) {", "a"], 2, 2, "}", "a}" },
        { "ElectricCharacter - is no-op if pairs are all matched before", ["foo(() => {", "  ( 1 + 2 ) ", "})"], 2, 13, "*", "  ( 1 + 2 ) *" },
        { "ElectricCharacter - is no-op if the line has other content", ["Math.max(", "\t2", "\t3"], 3, 3, ")", "\t3)" },
        { "ElectricCharacter - appends text", ["  if (a) {", "/*"], 2, 3, "*", "/** */" },
        { "ElectricCharacter - appends text 2", ["  if (a) {", "  /*"], 2, 5, "*", "  /** */" },
    };

    [Theory]
    [MemberData(nameof(ElectricCharacterCases))]
    public void ElectricCharacter(string name, string[] text, int lineNumber, int column, string chr, string expected)
    {
        _ = name;
        TestElectricCharacter(text, lineNumber, column, chr, expected);
    }

    [Fact]
    public void ElectricCharacter_Is_No_Op_If_Matching_Bracket_Is_On_The_Same_Line()
    {
        using var editor = _context.UsingCursor(["(div"], TypingTestContext.ElectricCharLanguageId);
        var model = editor.Model;

        editor.MoveTo(1, 5);
        string? changeText = null;
        model.DidChangeContent += e => changeText = e.Changes[0].Text;
        editor.Type(")", Keyboard);
        model.GetLineContent(1).ShouldBe("(div)");
        changeText.ShouldBe(")");
    }

    [Fact]
    public void ElectricCharacter_Issue_23711_Replacing_Selected_Text_With_Brackets_Fails_To_Delete_Old_Text_With_Backwards_Dragged_Selection()
    {
        using var editor = _context.UsingCursor(["{", "word"], TypingTestContext.ElectricCharLanguageId);

        editor.MoveTo(2, 5);
        editor.MoveTo(2, 1, true);
        editor.Type("}", Keyboard);
        editor.Model.GetLineContent(2).ShouldBe("}");
    }
}
