// Ported from vscode (commit b57d5c9e), MIT license:
//  - src/vs/editor/contrib/inlineCompletions/test/browser/computeGhostText.test.ts: all 10 tests;
//  - src/vs/editor/contrib/inlineCompletions/test/browser/getSecondaryEdits.test.ts: all 4 tests. "cursor not on same
//    line as primary edit 1" compares the replacement itself instead of upstream's TextEdit.toString rendering
//    ('...ction fib(❰\n↦) {\n\t... 0;\n}❱': "\n" at the end of line 3 replaced with ") {\n\treturn 0;\n}").
// Below them, tests written for this port: inlineCompletionIsVisible and matchesSubString.

using Shouldly;
using TKCode.Arc.Editor.Contrib.InlineSuggestions;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ComputeGhostTextTests
{
    private const string Undefined = "<undefined>";

    // the result of both modes, or "prefix: ... | subword: ..." when they differ
    private static string GetOutput(string text, string suggestion)
    {
        var rangeStartOffset = text.IndexOf('[', StringComparison.Ordinal);
        var rangeEndOffset = text.IndexOf(']', StringComparison.Ordinal) - 1;
        var cleanedText = text.Replace("[", "", StringComparison.Ordinal).Replace("]", "", StringComparison.Ordinal);
        using var tempModel = TestTextModel.Create(cleanedText);
        var range = new TextRange(tempModel.GetPositionAt(rangeStartOffset), tempModel.GetPositionAt(rangeEndOffset));
        var prefix = GhostTextComputer.ComputeGhostText(new TextReplacement(range, suggestion), tempModel, InlineSuggestMode.Prefix)?.Render(cleanedText, true) ?? Undefined;
        var subword = GhostTextComputer.ComputeGhostText(new TextReplacement(range, suggestion), tempModel, InlineSuggestMode.Subword)?.Render(cleanedText, true) ?? Undefined;
        return prefix == subword ? prefix : $"prefix: {prefix} | subword: {subword}";
    }

    private static string Both(string prefix, string subword) => $"prefix: {prefix} | subword: {subword}";

    [Fact]
    public void Basic()
    {
        GetOutput("[foo]baz", "foobar").ShouldBe("foo[bar]baz");
        GetOutput("[aaa]aaa", "aaaaaa").ShouldBe("aaa[aaa]aaa");
        GetOutput("[foo]baz", "boobar").ShouldBe(Undefined);
        GetOutput("[foo]foo", "foofoo").ShouldBe("foo[foo]foo");
        GetOutput("foo[]", "bar\nhello").ShouldBe("foo[bar\nhello]");
    }

    [Fact]
    public void Empty_ghost_text() => GetOutput("[foo]", "foo").ShouldBe("foo");

    [Fact]
    public void Whitespace_indentation()
    {
        GetOutput("[ foo]", "foobar").ShouldBe(" foo[bar]");
        GetOutput("[\tfoo]", "foobar").ShouldBe("\tfoo[bar]");
        GetOutput("[\t foo]", "\tfoobar").ShouldBe("\t foo[bar]");
        GetOutput("[\tfoo]", "\t\tfoobar").ShouldBe(Both(Undefined, "\t[\t]foo[bar]"));
        GetOutput("[\t]", "\t\tfoobar").ShouldBe("\t[\tfoobar]");
        GetOutput("\t[]", "\t").ShouldBe("\t[\t]");
        GetOutput("\t[\t]", "").ShouldBe("\t\t");

        GetOutput("[ ]", "return 1").ShouldBe(" [return 1]");
    }

    [Fact]
    public void Whitespace_outside_of_indentation()
    {
        GetOutput("bar[ foo]", "foobar").ShouldBe(Undefined);
        GetOutput("bar[\tfoo]", "foobar").ShouldBe(Undefined);
    }

    [Fact]
    public void Unsupported_case() => GetOutput("fo[o\n]", "x\nbar").ShouldBe(Undefined);

    [Fact]
    public void New_line() => GetOutput("fo[o\n]", "o\nbar").ShouldBe("foo\n[bar]");

    [Fact]
    public void Multi_part_diffing()
    {
        GetOutput("foo[()]", "(x);").ShouldBe(Both(Undefined, "foo([x])[;]"));
        GetOutput("[\tfoo]", "\t\tfoobar").ShouldBe(Both(Undefined, "\t[\t]foo[bar]"));
        GetOutput("[(y ===)]", "(y === 1) { f(); }").ShouldBe(Both(Undefined, "(y ===[ 1])[ { f(); }]"));
        GetOutput("[(y ==)]", "(y === 1) { f(); }").ShouldBe(Both(Undefined, "(y ==[= 1])[ { f(); }]"));

        GetOutput("[(y ==)]", "(y === 1) { f(); }").ShouldBe(Both(Undefined, "(y ==[= 1])[ { f(); }]"));
    }

    [Fact]
    public void Multi_part_diffing_1() =>
        GetOutput("[if () ()]", "if (1 == f()) ()").ShouldBe(Both(Undefined, "if ([1 == f()]) ()"));

    [Fact]
    public void Multi_part_diffing_2()
    {
        GetOutput("[)]", "())").ShouldBe(Both(Undefined, "[(])[)]"));
        GetOutput("[))]", "(())").ShouldBe(Both(Undefined, "[((]))"));
    }

    [Fact]
    public void Parenthesis_matching() =>
        GetOutput("[console.log()]", "console.log({ label: \"(\" })").ShouldBe(Both(Undefined, "console.log([{ label: \"(\" }])"));

    // ---- getSecondaryEdits.test.ts

    [Fact]
    public void Secondary_edits_preserve_a_common_prefix_containing_a_surrogate_pair()
    {
        using var textModel = TestTextModel.Create("😀a\n😀a");
        TextPosition[] positions = [new(1, 4), new(2, 4)];
        var primaryEdit = new TextReplacement(new TextRange(1, 1, 1, 4), "😀ab");
        var secondaryEdits = GhostTextComputer.GetSecondaryEdits(textModel, positions, primaryEdit);

        secondaryEdits.ShouldBe([new TextReplacement(new TextRange(2, 4, 2, 4), "b")]);
        TextReplacement.ApplyToString(textModel.GetValue(), [primaryEdit, .. secondaryEdits]).ShouldBe("😀ab\n😀ab");
    }

    [Fact]
    public void Secondary_edits_basic()
    {
        using var textModel = TestTextModel.Create(string.Join('\n', "function fib(", "function fib("));
        TextPosition[] positions = [new(1, 14), new(2, 14)];
        var primaryEdit = new TextReplacement(new TextRange(1, 1, 1, 14), "function fib() {");
        GhostTextComputer.GetSecondaryEdits(textModel, positions, primaryEdit)
            .ShouldBe([new TextReplacement(new TextRange(2, 14, 2, 14), ") {")]);
    }

    [Fact]
    public void Secondary_edits_cursor_not_on_same_line_as_primary_edit_1()
    {
        using var textModel = TestTextModel.Create(string.Join('\n', "function fib(", "", "function fib(", ""));
        TextPosition[] positions = [new(2, 1), new(4, 1)];
        var primaryEdit = new TextReplacement(new TextRange(1, 1, 2, 1), string.Join('\n', "function fib() {", "\treturn 0;", "}"));
        GhostTextComputer.GetSecondaryEdits(textModel, positions, primaryEdit)
            .ShouldBe([new TextReplacement(new TextRange(3, 14, 4, 1), ") {\n\treturn 0;\n}")]);
    }

    [Fact]
    public void Secondary_edits_cursor_not_on_same_line_as_primary_edit_2()
    {
        using var textModel = TestTextModel.Create(string.Join('\n', "class A {", "", "class B {", "", "function f() {}"));
        TextPosition[] positions = [new(2, 1), new(4, 1)];
        var primaryEdit = new TextReplacement(new TextRange(1, 1, 2, 1), string.Join('\n', "class A {", "\tpublic x: number = 0;", "   public y: number = 0;", "}"));
        GhostTextComputer.GetSecondaryEdits(textModel, positions, primaryEdit)
            .ShouldBe([new TextReplacement(new TextRange(4, 1, 4, 1), string.Join('\n', "\tpublic x: number = 0;", "   public y: number = 0;", "}"))]);
    }

    // ---- written for the port

    [Fact]
    public void Visibility_follows_the_cursor_and_what_was_typed()
    {
        using var model = TestTextModel.Create("foo");
        var edit = new TextReplacement(new TextRange(1, 1, 1, 4), "foobar");

        GhostTextComputer.InlineCompletionIsVisible(edit, null, model, new TextPosition(1, 4)).ShouldBeTrue();
        // on another line
        GhostTextComputer.InlineCompletionIsVisible(edit, null, model, new TextPosition(2, 1)).ShouldBeFalse();
        // the original range started elsewhere
        GhostTextComputer.InlineCompletionIsVisible(edit, new TextRange(1, 2, 1, 4), model, new TextPosition(1, 4)).ShouldBeFalse();
        // nothing left to insert
        GhostTextComputer.InlineCompletionIsVisible(new TextReplacement(new TextRange(1, 1, 1, 4), "foo"), null, model, new TextPosition(1, 4)).ShouldBeFalse();
        // what is typed before the cursor disagrees with the suggestion
        GhostTextComputer.InlineCompletionIsVisible(new TextReplacement(new TextRange(1, 1, 1, 4), "fxbar"), null, model, new TextPosition(1, 4)).ShouldBeFalse();
    }

    [Fact]
    public void MatchesSubString_takes_characters_in_order_ignoring_case_without_backtracking()
    {
        GhostTextComputer.MatchesSubString("", "abc").ShouldBeTrue();
        GhostTextComputer.MatchesSubString("ac", "ABC").ShouldBeTrue();
        GhostTextComputer.MatchesSubString("ca", "abc").ShouldBeFalse();
        GhostTextComputer.MatchesSubString("abcd", "abc").ShouldBeFalse();
    }
}
