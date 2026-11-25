// Tests of the Markdown subset hovers render (HoverMarkdown, written for this port; upstream renders with marked) and of
// the ported helpers of src/vs/base/common/htmlContent.ts (escapeMarkdownSyntaxTokens, appendText, isEmptyMarkdownString)
// and of the code block tokenization (textToHtmlTokenizer.ts).

using Shouldly;
using TKCode.Arc.Editor.Contrib.Hover;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Languages.BuiltIn;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class HoverMarkdownTests
{
    [Fact]
    public void Paragraphs_Are_Split_By_Blank_Lines_And_Soft_Breaks_Join()
    {
        var blocks = HoverMarkdown.Parse("first line\nsame paragraph\n\nsecond");
        blocks.Count.ShouldBe(2);
        HoverMarkdown.ToPlainText(((MarkdownParagraph)blocks[0]).Runs).ShouldBe("first line same paragraph");
        HoverMarkdown.ToPlainText(((MarkdownParagraph)blocks[1]).Runs).ShouldBe("second");
    }

    [Fact]
    public void Hard_Breaks_Are_Line_Breaks()
    {
        var runs = ((MarkdownParagraph)HoverMarkdown.Parse("a  \nb\\\nc").Single()).Runs;
        runs.Count(r => r.Kind == MarkdownRunKind.LineBreak).ShouldBe(2);
        HoverMarkdown.ToPlainText(runs).ShouldBe("a\nb\nc");
    }

    [Fact]
    public void Emphasis_Strong_Strikethrough_And_Code_Spans()
    {
        var runs = ((MarkdownParagraph)HoverMarkdown.Parse("a **bold** _it_ ~~gone~~ `x * y` ***both***").Single()).Runs;

        runs.Single(r => r.Text == "bold").Style.ShouldBe(MarkdownRunStyle.Bold);
        runs.Single(r => r.Text == "it").Style.ShouldBe(MarkdownRunStyle.Italic);
        runs.Single(r => r.Text == "gone").Style.ShouldBe(MarkdownRunStyle.Strikethrough);
        runs.Single(r => r.Kind == MarkdownRunKind.Code).Text.ShouldBe("x * y");
        runs.Single(r => r.Text == "both").Style.ShouldBe(MarkdownRunStyle.Bold | MarkdownRunStyle.Italic);
    }

    [Fact]
    public void Links_And_Autolinks_Carry_Their_Target()
    {
        var runs = ((MarkdownParagraph)HoverMarkdown.Parse("see [the **docs**](https://example.com \"Docs\") or <https://a.b/c>").Single()).Runs;

        var link = runs.Where(r => r.Href == "https://example.com").ToList();
        string.Concat(link.Select(r => r.Text)).ShouldBe("the docs");
        link.Single(r => r.Text == "docs").Style.ShouldBe(MarkdownRunStyle.Bold);
        link[0].Title.ShouldBe("Docs");
        runs.Single(r => r.Href == "https://a.b/c").Text.ShouldBe("https://a.b/c");
    }

    [Fact]
    public void Fenced_And_Indented_Code_Blocks()
    {
        var blocks = HoverMarkdown.Parse("```typescript\nfunction f(): void\n  return\n```\n\ntext\n\n    indented\n    code");

        var fenced = blocks[0].ShouldBeOfType<MarkdownCodeBlock>();
        fenced.Language.ShouldBe("typescript");
        fenced.Code.ShouldBe("function f(): void\n  return");
        blocks[1].ShouldBeOfType<MarkdownParagraph>();
        var indented = blocks[2].ShouldBeOfType<MarkdownCodeBlock>();
        indented.Language.ShouldBeNull();
        indented.Code.ShouldBe("indented\ncode");
    }

    [Fact]
    public void An_Unclosed_Fence_Runs_To_The_End()
    {
        var block = HoverMarkdown.Parse("~~~\nsome\ncode").Single().ShouldBeOfType<MarkdownCodeBlock>();
        block.Code.ShouldBe("some\ncode");
    }

    [Fact]
    public void Headings_Lists_Quotes_And_Rules()
    {
        var blocks = HoverMarkdown.Parse("# Title\n\n- one\n- two\n\n1. first\n2. second\n\n> quoted\n\n---\n\nSetext\n===");

        blocks[0].ShouldBeOfType<MarkdownHeading>().Level.ShouldBe(1);
        var bullets = blocks[1].ShouldBeOfType<MarkdownList>();
        bullets.Ordered.ShouldBeFalse();
        bullets.Items.Count.ShouldBe(2);
        var ordered = blocks[2].ShouldBeOfType<MarkdownList>();
        ordered.Ordered.ShouldBeTrue();
        ordered.Start.ShouldBe(1);
        blocks[3].ShouldBeOfType<MarkdownQuote>();
        blocks[4].ShouldBeOfType<MarkdownRule>();
        var setext = blocks[5].ShouldBeOfType<MarkdownHeading>();
        setext.Level.ShouldBe(1);
        HoverMarkdown.ToPlainText(setext.Runs).ShouldBe("Setext");
    }

    [Fact]
    public void Escapes_And_Character_References()
    {
        var text = HoverMarkdown.ToPlainText("\\*not bold\\* &amp; &lt;tag&gt; &#65;");
        text.ShouldBe("*not bold* & <tag> A");
    }

    [Fact]
    public void Html_Line_Break_Is_A_Line_Break_Other_Tags_Stay_Text()
    {
        HoverMarkdown.ToPlainText("a<br>b <span>c</span>").ShouldBe("a\nb <span>c</span>");
    }

    [Fact]
    public void Escape_Markdown_Syntax_Tokens_Round_Trips_Through_The_Parser()
    {
        const string text = "List<T> [a](b) *c* _d_ `e` #f -g";
        HoverMarkdown.EscapeMarkdownSyntaxTokens("*a* `b`").ShouldBe("\\*a\\* \\`b\\`");
        HoverMarkdown.ToPlainText(HoverMarkdown.EscapeMarkdownSyntaxTokens(text)).ShouldBe(text);
    }

    [Fact]
    public void Append_Text_Keeps_Spaces_And_Starts_Paragraphs_At_Newlines()
    {
        var markdown = HoverMarkdown.AppendText("a  b\n> c");
        markdown.ShouldBe("a&nbsp;&nbsp;b\n\n\\>&nbsp;c");
        HoverMarkdown.ToPlainText(markdown).ShouldBe("a  b\n> c");
    }

    [Fact]
    public void Is_Empty_Markdown_String()
    {
        HoverMarkdown.IsEmptyMarkdownString((MarkdownString?)null).ShouldBeTrue();
        HoverMarkdown.IsEmptyMarkdownString(new MarkdownString("")).ShouldBeTrue();
        HoverMarkdown.IsEmptyMarkdownString([new MarkdownString(""), new MarkdownString("x")]).ShouldBeFalse();
        HoverMarkdown.IsEmptyMarkdownString(Array.Empty<MarkdownString>()).ShouldBeTrue();
    }

    [Fact]
    public void Code_Block_Language_Is_The_Fence_Name_Or_Alias_Else_The_Editor_Language()
    {
        using var services = CreateServices();
        var languages = services.LanguageService;

        MarkdownCodeTokenizer.GetLanguageId(languages, "TypeScript", "csharp").ShouldBe("typescript");
        MarkdownCodeTokenizer.GetLanguageId(languages, "javascript", "csharp").ShouldBe("javascript");
        MarkdownCodeTokenizer.GetLanguageId(languages, null, "csharp").ShouldBe("csharp");
        MarkdownCodeTokenizer.GetLanguageId(languages, "no-such-language", "csharp").ShouldBe("plaintext");
        MarkdownCodeTokenizer.GetLanguageId(languages, null, null).ShouldBe("plaintext");
    }

    [Fact]
    public void Code_Blocks_Are_Tokenized_Line_By_Line_With_State()
    {
        using var services = CreateServices();
        var support = services.TokenizationRegistry.GetOrCreateAsync("javascript").GetAwaiter().GetResult();
        support.ShouldNotBeNull();

        var lines = MarkdownCodeTokenizer.Tokenize("const a = 1; /* open\nstill comment */ let b", support);

        lines.Count.ShouldBe(2);
        string.Concat(lines[0].Select(t => t.Text)).ShouldBe("const a = 1; /* open");
        string.Concat(lines[1].Select(t => t.Text)).ShouldBe("still comment */ let b");
        lines[0].Count.ShouldBeGreaterThan(1);
        // the comment carries over to the second line: its first token has the colour of the comment that ended line one
        lines[1][0].Foreground.ShouldBe(lines[0][^1].Foreground);
        lines[1][0].Foreground.ShouldNotBe(lines[0][0].Foreground);
    }

    private static LanguageServices CreateServices()
    {
        var services = new LanguageServices();
        foreach (var language in BuiltInLanguages.All)
        {
            language.Register(services);
        }

        return services;
    }

    [Fact]
    public void Without_A_Tokenizer_Every_Line_Is_One_Token()
    {
        var lines = MarkdownCodeTokenizer.Tokenize("a b\n\nc", null);
        lines.Select(l => l.Count).ShouldBe([1, 1, 1]);
        lines[1][0].Text.ShouldBe("");
    }
}
