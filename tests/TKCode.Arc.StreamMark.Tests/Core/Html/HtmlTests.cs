using StreamMark.Core.Html;
using StreamMark.Core.Model;
using StreamMark.Core.Remend;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Core.Tests.Html;

public sealed class HtmlTests
{
    private static List<MarkdownBlock> Parse(string markdown) =>
        [.. new DocumentComposer().Parse(markdown, 0, RemendOptions.Default)];

    private static IEnumerable<MarkdownBlock> Flatten(IEnumerable<MarkdownBlock> blocks)
    {
        foreach (var block in blocks)
        {
            yield return block;
            var children = block switch
            {
                HtmlBoxBlock box => box.Children,
                QuoteBlock quote => quote.Children,
                ComponentBlock component => component.Children,
                ListBlock list => [.. list.Items.SelectMany(i => i.Children)],
                _ => [],
            };
            foreach (var child in Flatten(children))
            {
                yield return child;
            }
        }
    }

    [Fact]
    public void CenteredDiv_WithHeadingAndImage_BecomesBox()
    {
        var blocks = Parse("""
            <div align="center">
              <h1>Ant Design</h1>
              <img height="180" src="https://example.com/a.svg">
            </div>
            """);
        var box = Assert.IsType<HtmlBoxBlock>(Assert.Single(blocks));
        Assert.Equal(TextAlign.Center, box.Text.Align);
        var heading = Assert.IsType<HeadingBlock>(box.Children[0]);
        Assert.Equal(1, heading.Level);
        Assert.Equal("Ant Design", heading.Content.Text);
        var image = Assert.IsType<ParagraphBlock>(box.Children[1]).Content.Objects.Single();
        Assert.Equal(InlineObjectKind.Image, image.Kind);
        Assert.Equal("https://example.com/a.svg", image.Value);
        Assert.Equal("180", image.Attributes["height"]);
    }

    [Fact]
    public void NestedDivs_TextAfterInnerDiv_IsParagraph()
    {
        var blocks = Parse("<div>\n    <div align=\"center\">\n      <h1>T</h1>\n    </div>\nAnt Design 是企业级 UI 设计语言。\n</div>");
        var outer = Assert.IsType<HtmlBoxBlock>(Assert.Single(blocks));
        Assert.IsType<HtmlBoxBlock>(outer.Children[0]);
        Assert.Equal("Ant Design 是企业级 UI 设计语言。", Assert.IsType<ParagraphBlock>(outer.Children[1]).Content.Text);
    }

    [Fact]
    public void Style_MarginPaddingColorFontSize_AreParsed()
    {
        var blocks = Parse("<div style=\"margin-top: 50px; padding: 4px 8px; background:#f5f5f5; color: rgb(255, 0, 0); font-size: 20px; border-radius: 6px\">\nText <span style=\"color:#1677ff;font-weight:bold\">blue</span>\n</div>");
        var box = Assert.IsType<HtmlBoxBlock>(Assert.Single(blocks));
        Assert.Equal(50, box.Box.Margin.Top);
        Assert.Equal(new Edges(4, 8, 4, 8), box.Box.Padding);
        Assert.Equal(new CssColor(255, 0xf5, 0xf5, 0xf5), box.Box.Background);
        Assert.Equal(6, box.Box.BorderRadius);
        Assert.Equal(new CssColor(255, 255, 0, 0), box.Text.Foreground);
        Assert.Equal(20, box.Text.FontSize);
        var paragraph = Assert.IsType<ParagraphBlock>(box.Children.Single());
        Assert.Equal("Text blue", paragraph.Content.Text);
        var span = paragraph.Content.Spans.Single(s => s.TextStyleIndex >= 0);
        var style = paragraph.Content.TextStyles[span.TextStyleIndex];
        Assert.Equal(new CssColor(255, 0x16, 0x77, 0xff), style.Foreground);
        Assert.True(style.Bold);
    }

    [Fact]
    public void Css_ClampsAndColors()
    {
        var box = HtmlBoxStyle.Default;
        var text = HtmlTextStyle.Default;
        CssStyle.Apply("margin: 9999px; font-size: 400px; color: #abc; background-color: rgba(0,0,0,0.5)", ref box, ref text);
        Assert.Equal(CssStyle.MaxSpacing, box.Margin.Top);
        Assert.Equal(CssStyle.MaxFontSize, text.FontSize);
        Assert.Equal(new CssColor(255, 0xaa, 0xbb, 0xcc), text.Foreground);
        Assert.Equal(new CssColor(128, 0, 0, 0), box.Background);
        Assert.True(CssStyle.TryParseColor("RebeccaPurple", out var named));
        Assert.Equal(new CssColor(255, 0x66, 0x33, 0x99), named);
        Assert.False(CssStyle.TryParseColor("notacolor", out _));
        CssStyle.Apply("font-size: 1.5em", ref box, ref text);
        Assert.Equal(1.5, text.FontScale);
        Assert.Null(text.FontSize);
    }

    [Fact]
    public void Table_WithParagraphsAndLinkedImages_MapsCells()
    {
        var blocks = Parse("""
            <table>
              <tr>
                <td align="center">
                  <a href="https://example.com/x"><img src="./x.png" width="160"></a>
                  <p>火数云提供云服务器。</p>
                </td>
                <td>Second</td>
              </tr>
            </table>
            """);
        var table = Assert.IsType<TableBlock>(Assert.Single(blocks));
        Assert.Equal(TableAlignment.Center, table.Columns[0]);
        var cell = table.Rows.Single().Cells[0];
        Assert.Equal(InlineObject.Placeholder + "\n火数云提供云服务器。", cell.Text);
        var image = cell.Objects.Single();
        Assert.Equal("160", image.Attributes["width"]);
        // 图片带链接
        var linkSpan = cell.Spans.Single(s => s.Start == 0);
        Assert.Equal("https://example.com/x", cell.Links[linkSpan.LinkIndex].Url);
        Assert.Equal("Second", table.Rows[0].Cells[1].Text);
    }

    [Fact]
    public void HtmlAroundMarkdown_AcrossBlankLines_IsStitched()
    {
        var blocks = Parse("<div align=\"center\">\n\n## Title\n\nSome **bold**.\n\n</div>\n\nAfter.");
        Assert.Equal(2, blocks.Count);
        var box = Assert.IsType<HtmlBoxBlock>(blocks[0]);
        Assert.Equal(TextAlign.Center, box.Text.Align);
        Assert.IsType<HeadingBlock>(box.Children[0]);
        Assert.Equal("Some bold.", Assert.IsType<ParagraphBlock>(box.Children[1]).Content.Text);
        Assert.Equal("After.", Assert.IsType<ParagraphBlock>(blocks[1]).Content.Text);
        Assert.Equal(0, box.Key);
    }

    [Fact]
    public void InlineHtml_InParagraph_AcrossSoftBreak()
    {
        var blocks = Parse("前文<strong>商业应用，\n可以免费使用</strong>。<a href=\"https://e.com\">链接</a> <span style=\"color:red\">红</span>");
        var content = Assert.IsType<ParagraphBlock>(Assert.Single(blocks)).Content;
        Assert.Equal("前文商业应用，可以免费使用。链接 红", content.Text);
        var bold = content.Spans.Where(s => s.Style.HasFlag(InlineStyle.Bold)).ToList();
        Assert.Equal("商业应用，可以免费使用", string.Concat(bold.Select(s => content.Text.Substring(s.Start, s.Length))));
        Assert.Contains(content.Spans, s => s.Style.HasFlag(InlineStyle.Link) && content.Links[s.LinkIndex].Url == "https://e.com");
        var red = content.Spans.Single(s => s.TextStyleIndex >= 0);
        Assert.Equal(new CssColor(255, 255, 0, 0), content.TextStyles[red.TextStyleIndex].Foreground);
    }

    [Fact]
    public void MarkdownBadge_ImageInsideLink_HasLink()
    {
        var content = Assert.IsType<ParagraphBlock>(Assert.Single(Parse("[![build](https://img/b.svg)](https://ci)"))).Content;
        var span = Assert.Single(content.Spans, s => s.Start == content.Objects[0].Offset);
        Assert.Equal("https://ci", content.Links[span.LinkIndex].Url);
    }

    [Fact]
    public void UnknownElements_KeepSource_CommentsHidden()
    {
        var blocks = Parse("<custom-widget>x</custom-widget>\n\n<!-- hidden -->\n\n<script>alert(1)</script>\n\nText");
        Assert.Equal(3, blocks.Count);
        // 单行的自定义元素按 CommonMark 是段落中的行内 HTML：同样按原文显示
        Assert.Equal("<custom-widget>x</custom-widget>", Assert.IsType<ParagraphBlock>(blocks[0]).Content.Text);
        Assert.Contains("alert(1)", Assert.IsType<HtmlBlock>(blocks[1]).Html);
        Assert.Equal("Text", Assert.IsType<ParagraphBlock>(blocks[2]).Content.Text);
        // 行内的未知标签按原文显示
        var inline = Assert.IsType<ParagraphBlock>(Assert.Single(Parse("a <blink>b</blink> c"))).Content;
        Assert.Equal("a <blink>b</blink> c", inline.Text);
    }

    [Fact]
    public void PreCode_ListsDetails_Map()
    {
        var blocks = Parse("<pre><code class=\"language-cs\">var x = 1 &lt; 2;\n</code></pre>\n\n<ul>\n<li>first <b>bold</b>\n<li>second\n</ul>\n\n<details>\n<summary>More</summary>\n\nHidden.\n\n</details>");
        var code = Assert.IsType<CodeBlock>(blocks[0]);
        Assert.Equal("cs", code.Language);
        Assert.Equal("var x = 1 < 2;", code.Code);
        var list = Assert.IsType<ListBlock>(blocks[1]);
        Assert.Equal(2, list.Items.Length);
        Assert.Equal("first bold", Assert.IsType<ParagraphBlock>(list.Items[0].Children[0]).Content.Text);
        Assert.IsType<ComponentBlock>(blocks[2]);
    }

    [Fact]
    public void TreeBuilder_ImplicitClosesAndPartialTags()
    {
        var tree = new HtmlTreeBuilder();
        tree.Feed("<p>one<p>two<table><tr><td>a<td>b<tr><td>c</table><img src=\"x\"><div class=\"inco");
        var root = tree.Root.Children.OfType<HtmlElement>().ToList();
        Assert.Equal(["p", "p", "table", "img"], root.Select(e => e.Name));
        var rows = root[2].Children.OfType<HtmlElement>().ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows[0].Children.Count);
        Assert.False(tree.HasUnknownElements);
        Assert.False(tree.HasOpenElements);
    }

    [Fact]
    public void Streaming_PartialHtml_NeverShowsRawTags()
    {
        const string html = "<div align=\"center\">\n  <h1>Title</h1>\n  <p>Body <strong>bold</strong></p>\n</div>\n\nAfter";
        for (var i = 1; i <= html.Length; i++)
        {
            if (html[i - 1] == '<')
            {
                // 末尾单独的 "<" 可能是小于号，remend 不去掉它
                continue;
            }
            var blocks = Flatten(Parse(html[..i])).ToList();
            foreach (var block in blocks)
            {
                var text = block switch
                {
                    ParagraphBlock p => p.Content.Text,
                    HeadingBlock h => h.Content.Text,
                    HtmlBlock h => h.Html,
                    _ => "",
                };
                Assert.False(text.Contains('<'), $"prefix {i}: {html[..i].Replace("\n", "\\n")} -> {block.GetType().Name}: {text}");
            }
        }
    }
}
