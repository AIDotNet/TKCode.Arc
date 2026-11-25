// Tests of the plain-text snippet fallback of the suggest controller (PlainSnippet; no upstream counterpart: upstream
// always inserts through a snippet session). The cases follow snippetParser.test.ts's grammar.

using Shouldly;
using TKCode.Arc.Editor.Contrib.Suggest;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib.Suggest;

public sealed class PlainSnippetTests
{
    [Theory]
    [InlineData("foo", "foo", 3, 0)]
    [InlineData("foo($1)", "foo()", 4, 0)]
    [InlineData("foo(${1:bar})$0", "foo(bar)", 4, 3)]
    [InlineData("$0 then ${2:b} ${1:a}", " then b a", 8, 1)]
    [InlineData("if (${1:cond}) {\n\t$0\n}", "if (cond) {\n\t\n}", 4, 4)]
    [InlineData("${1:outer ${2:inner}}", "outer inner", 0, 11)]
    [InlineData("${1|one,two,three|}", "one", 0, 3)]
    [InlineData("a\\$b\\}c\\\\", "a$b}c\\", 6, 0)]
    [InlineData("${TM_FILENAME:untitled}.cs", "untitled.cs", 11, 0)]
    [InlineData("$UNKNOWN!", "!", 1, 0)]
    [InlineData("cost: $", "cost: $", 7, 0)]
    [InlineData("${1/(.*)/${1:/upcase}/}x", "x", 0, 0)]
    public void Parse_drops_the_snippet_syntax(string template, string text, int selectionStart, int selectionLength)
    {
        var plain = PlainSnippet.Parse(template);
        plain.Text.ShouldBe(text);
        plain.SelectionStart.ShouldBe(selectionStart);
        plain.SelectionLength.ShouldBe(selectionLength);
    }

    [Fact]
    public void The_clipboard_variable_reads_the_clipboard_text()
    {
        PlainSnippet.Parse("[$CLIPBOARD]", "pasted").Text.ShouldBe("[pasted]");
        PlainSnippet.Parse("[${CLIPBOARD}]").Text.ShouldBe("[]");
    }

    [Fact]
    public void Escape_makes_plain_text_insert_as_itself()
    {
        var text = "a $1 ${2:x} } \\ b";
        PlainSnippet.Escape(text).ShouldBe("a \\$1 \\${2:x\\} \\} \\\\ b");
        PlainSnippet.Parse(PlainSnippet.Escape(text)).Text.ShouldBe(text);
    }
}
