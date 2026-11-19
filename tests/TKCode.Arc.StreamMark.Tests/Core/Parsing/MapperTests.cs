using StreamMark.Core.Model;
using StreamMark.Core.Remend;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Core.Tests.Parsing;

public sealed class MapperTests
{
    private static List<MarkdownBlock> Parse(string markdown) =>
        [.. new DocumentComposer().Parse(markdown, 0, RemendOptions.Default)];

    [Fact]
    public void BackslashInlineMath_BecomesMathObject()
    {
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(Parse(@"Energy \(E = mc^2\) here")));
        var math = Assert.Single(p.Content.Objects);
        Assert.Equal(InlineObjectKind.Math, math.Kind);
        Assert.Equal("E = mc^2", math.Value);
        Assert.False(math.Flag);
        Assert.Equal(InlineObject.Placeholder, p.Content.Text[math.Offset]);
    }

    [Fact]
    public void BackslashBlockMath_BecomesMathBlock()
    {
        var block = Assert.IsType<MathBlock>(Assert.Single(Parse("\\[\n\\frac{1}{2}\n\\]")));
        Assert.Equal(@"\frac{1}{2}", block.Latex);
        Assert.False(block.IsOpen);
    }

    [Fact]
    public void DollarBlockMath_OpenWhileStreaming()
    {
        // remend 会补全 $$，所以流式中的块公式也能先渲染出来
        var block = Assert.IsType<MathBlock>(Assert.Single(Parse("$$\nx^2")));
        Assert.Contains("x^2", block.Latex);
    }

    [Fact]
    public void TaskList_IsLiftedToListItem()
    {
        var list = Assert.IsType<ListBlock>(Assert.Single(Parse("- [x] done\n- [ ] todo")));
        Assert.Equal(true, list.Items[0].Task);
        Assert.Equal(false, list.Items[1].Task);
        var text = Assert.IsType<ParagraphBlock>(list.Items[0].Children[0]).Content.Text;
        Assert.Equal("done", text);
    }

    [Fact]
    public void Table_MapsAlignmentAndCells()
    {
        var table = Assert.IsType<TableBlock>(Assert.Single(Parse("| a | b |\n|:--|--:|\n| 1 | **2** |")));
        Assert.Equal([TableAlignment.Left, TableAlignment.Right], table.Columns);
        Assert.True(table.Rows[0].IsHeader);
        Assert.Equal("2", table.Rows[1].Cells[1].Text);
        Assert.Equal(InlineStyle.Bold, table.Rows[1].Cells[1].Spans[0].Style);
    }

    [Fact]
    public void Alert_MapsKind()
    {
        var alert = Assert.IsType<AlertBlock>(Assert.Single(Parse("> [!WARNING]\n> careful")));
        Assert.Equal("WARNING", alert.Kind);
    }

    [Fact]
    public void IncompleteLink_IsMarkedNotClickable()
    {
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(Parse("see [the docs](https://exa")));
        Assert.Empty(p.Content.Links);
        Assert.Contains(p.Content.Spans, s => s.Style.HasFlag(InlineStyle.IncompleteLink));
    }

    [Fact]
    public void UnclosedBold_IsRenderedBoldWhileStreaming()
    {
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(Parse("this is **important")));
        Assert.Contains(p.Content.Spans, s => s.Style == InlineStyle.Bold && p.Content.Text.Substring(s.Start, s.Length) == "important");
    }

    [Fact]
    public void CjkSoftBreak_HasNoSpace_LatinSoftBreak_HasSpace()
    {
        var cjk = Assert.IsType<ParagraphBlock>(Assert.Single(Parse("第一行\n第二行")));
        Assert.Equal("第一行第二行", cjk.Content.Text);
        var latin = Assert.IsType<ParagraphBlock>(Assert.Single(Parse("first\nsecond")));
        Assert.Equal("first second", latin.Content.Text);
    }

    [Fact]
    public void TildeEscape_KeepsSourceOffsetsAligned()
    {
        // remend 会把 20~25 转义成 20\~25；块区间必须仍然对应原始源码
        var markdown = "range 20~25 here\n\nnext";
        var blocks = Parse(markdown);
        Assert.Equal(2, blocks.Count);
        Assert.Equal(markdown.IndexOf("next", StringComparison.Ordinal), blocks[1].Range.Start);
        Assert.Equal("range 20~25 here", Assert.IsType<ParagraphBlock>(blocks[0]).Content.Text);
    }

    [Fact]
    public void SuperscriptAndInsertedSyntax_AreNotInterpreted()
    {
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(Parse("C++ and C++, a == b == c, 2^10 and 3^4")));
        Assert.Empty(p.Content.Spans);
    }
}
