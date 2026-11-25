// Tests of SuggestDocumentation: a completion's documentation as text blocks for the details pane (upstream renders
// the Markdown with its markdown renderer in suggestWidgetDetails.ts; no counterpart test).

using Shouldly;
using TKCode.Arc.Editor.Contrib.Suggest;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib.Suggest;

public sealed class SuggestDocumentationTests
{
    [Fact]
    public void Plain_text_is_one_block()
    {
        SuggestDocumentation.ToBlocks("  Writes a value.  ").ShouldBe([new SuggestDocumentationBlock("Writes a value.", false)]);
        SuggestDocumentation.ToBlocks("   ").ShouldBeEmpty();
    }

    [Fact]
    public void Markdown_becomes_paragraphs_and_code_blocks()
    {
        var markdown = new MarkdownString("# Print\nWrites **value** to the\n_output_, see [docs](https://x).\n\n```csharp\nprint(\"hi\");\nprint(2);\n```\n- one\n- `two`\n\n    indented code");
        SuggestDocumentation.ToBlocks(markdown).ShouldBe(
        [
            new SuggestDocumentationBlock("Print", false),
            new SuggestDocumentationBlock("Writes value to the output, see docs.", false),
            new SuggestDocumentationBlock("print(\"hi\");\nprint(2);", true),
            new SuggestDocumentationBlock("• one", false),
            new SuggestDocumentationBlock("• two", false),
            new SuggestDocumentationBlock("indented code", true),
        ]);
    }

    [Fact]
    public void Escapes_and_snake_case_are_kept_as_text()
    {
        SuggestDocumentation.ToBlocks(new MarkdownString("a\\*b and some_snake_case name")).ShouldBe([new SuggestDocumentationBlock("a*b and some_snake_case name", false)]);
    }
}
