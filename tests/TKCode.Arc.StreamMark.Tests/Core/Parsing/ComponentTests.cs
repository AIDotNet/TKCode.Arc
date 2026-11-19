using StreamMark.Core.Model;
using StreamMark.Core.Parsing;
using StreamMark.Core.Remend;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Core.Tests.Parsing;

public sealed class ComponentTests
{
    private static List<MarkdownBlock> Parse(string markdown, MarkdownProfile? profile = null) =>
        [.. new DocumentComposer(profile).Parse(markdown, 0, RemendOptions.Default)];

    [Fact]
    public void Details_IsContainer_WithMarkdownInside_AcrossBlankLines()
    {
        var blocks = Parse("<details>\n<summary>Title</summary>\n\nInside **bold**.\n\n- a\n- b\n\n</details>\n\nAfter.");
        var details = Assert.IsType<ComponentBlock>(blocks[0]);
        Assert.Equal("details", details.Name);
        var summary = Assert.IsType<ComponentBlock>(details.Children[0]);
        Assert.Equal("summary", summary.Name);
        Assert.Equal("Title", Assert.IsType<ParagraphBlock>(summary.Children[0]).Content.Text);
        Assert.Contains(details.Children, b => b is ParagraphBlock p && p.Content.Spans.Any(s => s.Style == InlineStyle.Bold));
        Assert.Contains(details.Children, b => b is ListBlock);
        Assert.Equal("After.", Assert.IsType<ParagraphBlock>(blocks[1]).Content.Text);
    }

    [Fact]
    public void NestedSameNameContainers_CloseInOrder()
    {
        var blocks = Parse("<details>\nouter\n\n<details>\ninner\n</details>\n\nstill outer\n</details>\n\nafter");
        var outer = Assert.IsType<ComponentBlock>(blocks[0]);
        Assert.Contains(outer.Children, b => b is ComponentBlock { Name: "details" });
        Assert.Contains(outer.Children, b => b is ParagraphBlock p && p.Content.Text == "still outer");
        Assert.Equal("after", Assert.IsType<ParagraphBlock>(blocks[1]).Content.Text);
    }

    [Fact]
    public void InlinePairs_MapToStyles_OrComponents()
    {
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(Parse("Press <kbd>Ctrl</kbd>, H<sub>2</sub>O, <mark>hot</mark>, <cite href=\"https://x\">guide</cite>.")));
        string Styled(InlineStyle style) => string.Concat(p.Content.Spans.Where(s => s.Style.HasFlag(style)).Select(s => p.Content.Text.Substring(s.Start, s.Length)));
        Assert.Equal("Ctrl", Styled(InlineStyle.Keyboard));
        Assert.Equal("2", Styled(InlineStyle.Subscript));
        Assert.Equal("hot", Styled(InlineStyle.Mark));
        var cite = Assert.Single(p.Content.Components);
        Assert.Equal("cite", cite.Name);
        Assert.Equal("https://x", cite.Attributes["href"]);
        var citeSpan = Assert.Single(p.Content.Spans, s => s.ComponentIndex == 0);
        Assert.Equal("guide", p.Content.Text.Substring(citeSpan.Start, citeSpan.Length));
        Assert.DoesNotContain("<", p.Content.Text);
    }

    [Fact]
    public void SelfClosing_OnItsOwnLine_IsInlineObject_NotRawHtml()
    {
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(Parse("<Citation id=\"3\" source='web' />")));
        var obj = Assert.Single(p.Content.Objects);
        Assert.Equal(InlineObjectKind.Component, obj.Kind);
        Assert.Equal("citation", obj.Value);
        Assert.Equal("3", obj.Attributes["id"]);
        Assert.Equal("web", obj.Attributes["source"]);
    }

    [Fact]
    public void ImgTag_BecomesImageObject_WithSizeAttributes()
    {
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(Parse("<img src=\"https://e.com/a.png\" alt=\"A\" width=\"320\" height=\"200\">")));
        var image = Assert.Single(p.Content.Objects);
        Assert.Equal(InlineObjectKind.Image, image.Kind);
        Assert.Equal("https://e.com/a.png", image.Value);
        Assert.Equal("A", image.Alt);
        Assert.Equal("320", image.Attributes["width"]);
    }

    [Fact]
    public void UnclosedContainer_WhileStreaming_ExtendsToEnd()
    {
        var blocks = Parse("<think>\nstill thinking\n\nmore");
        var think = Assert.IsType<ComponentBlock>(Assert.Single(blocks));
        Assert.Equal("think", think.Name);
        Assert.Equal(2, think.Children.Length);
    }

    [Fact]
    public void UnclosedInlinePair_WhileStreaming_WrapsRest()
    {
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(Parse("see <cite href=\"u\">the gui")));
        Assert.Single(p.Content.Components);
        Assert.Contains(p.Content.Spans, s => s.ComponentIndex == 0 && p.Content.Text.Substring(s.Start, s.Length) == "the gui");
    }

    [Fact]
    public void NonWhitelistedTags_KeepDefaultBehaviour()
    {
        var blocks = Parse("<foo>\nraw\n</foo>");
        Assert.IsType<HtmlBlock>(Assert.Single(blocks));
    }

    [Fact]
    public void Segmenter_DoesNotFreezeInsideContainer()
    {
        var segmenter = new StreamSegmenter(["details", "think"]);
        var text = "<details>\n\npara\n\nmore\n";
        for (var i = 1; i <= text.Length; i++)
        {
            segmenter.Advance(text.AsSpan(segmenter.FrozenEnd, i - segmenter.FrozenEnd), segmenter.FrozenEnd);
        }
        Assert.Equal(0, segmenter.FrozenEnd);
        var closed = text + "</details>\n\nafter";
        segmenter.Advance(closed.AsSpan(segmenter.FrozenEnd), segmenter.FrozenEnd);
        Assert.True(segmenter.FrozenEnd > 0);
    }

    [Fact]
    public void PluginComponentAndMappers_AreApplied()
    {
        var profile = new MarkdownProfileBuilder()
            .AddComponent(new ComponentSpec("Callout", ComponentKind.Block))
            .AddComponent(new ComponentSpec("badge", ComponentKind.SelfClosing))
            .Build();
        var blocks = Parse("<Callout type=\"warn\">\nBe **careful**.\n</Callout>\n\nStatus <badge text=\"beta\"/> now", profile);
        var callout = Assert.IsType<ComponentBlock>(blocks[0]);
        Assert.Equal("callout", callout.Name);
        Assert.Equal("warn", callout.Attributes["type"]);
        var badge = Assert.Single(Assert.IsType<ParagraphBlock>(blocks[1]).Content.Objects);
        Assert.Equal("badge", badge.Value);
    }

    private static readonly MarkdownProfile RawProfile = new MarkdownProfileBuilder()
        .AddComponent(new ComponentSpec("tool-call", ComponentKind.Block) { Content = ComponentContent.Raw })
        .Build();

    [Fact]
    public void RawComponent_KeepsContentVerbatim()
    {
        // 空行、Markdown 记号、remend 会转义的写法（a~b、列表里的 "> 5"）都原样保留
        const string body = "{\"a\": \"**b\", \"t\": \"20~25\"}\n\n- > 5 items\n<details>";
        var blocks = Parse($"<tool-call name=\"search\">\n{body}\n</tool-call>\n\nafter", RawProfile);
        var call = Assert.IsType<ComponentBlock>(blocks[0]);
        Assert.Equal("tool-call", call.Name);
        Assert.Equal("search", call.Attributes["name"]);
        Assert.Equal(body, call.Raw);
        Assert.Empty(call.Children);
        Assert.False(call.IsOpen);
        Assert.Equal("after", Assert.IsType<ParagraphBlock>(blocks[1]).Content.Text);
    }

    [Fact]
    public void RawComponent_WhileStreaming_IsOpen_WithoutTailCompletion()
    {
        var call = Assert.IsType<ComponentBlock>(Assert.Single(Parse("<tool-call>\n{\"q\": \"**bold `x", RawProfile)));
        Assert.True(call.IsOpen);
        Assert.Equal("{\"q\": \"**bold `x", call.Raw);
    }

    [Fact]
    public void RawComponent_OnOneLine_AndCloseAfterContent()
    {
        var one = Assert.IsType<ComponentBlock>(Parse("<tool-call>{\"a\":1}</tool-call>\n\nnext", RawProfile)[0]);
        Assert.Equal("{\"a\":1}", one.Raw);
        Assert.False(one.IsOpen);
        var trailing = Assert.IsType<ComponentBlock>(Parse("<tool-call>\nline 1\nline 2</tool-call>\nnext", RawProfile)[0]);
        Assert.Equal("line 1\nline 2", trailing.Raw);
        Assert.False(trailing.IsOpen);
    }

    [Fact]
    public void RawComponent_InsideQuote_DropsQuoteMarkers()
    {
        var quote = Assert.IsType<QuoteBlock>(Assert.Single(Parse("> <tool-call>\n> a~b\n> c\n> </tool-call>", RawProfile)));
        var call = Assert.IsType<ComponentBlock>(Assert.Single(quote.Children));
        Assert.Equal("a~b\nc", call.Raw);
    }

    [Fact]
    public void MarkdownComponent_ReportsIsOpen()
    {
        Assert.True(Assert.IsType<ComponentBlock>(Assert.Single(Parse("<think>\nstill"))).IsOpen);
        Assert.False(Assert.IsType<ComponentBlock>(Assert.Single(Parse("<think>\ndone\n</think>"))).IsOpen);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(17)]
    public void RawComponent_StreamingSnapshots_MatchFullParse(int chunk)
    {
        var text = "Intro **x**.\n\n<tool-call name=\"a\">\n{\"q\": \"**b\",\n\n \"r\": \"20~25\"}\n</tool-call>\n\nMiddle.\n\n> <tool-call>\n> x\n> </tool-call>\n\n<tool-call>{\"one\":1}</tool-call>\n\n<think>\nhm\n\n</think>\n\nEnd `c`.";
        var composer = new DocumentComposer(RawProfile);
        var reference = new DocumentComposer(RawProfile);
        var version = 0L;
        for (var length = Math.Min(chunk, text.Length); ; length = Math.Min(length + chunk, text.Length))
        {
            var from = composer.FrozenEnd;
            var snapshot = composer.Update(text[from..length], from, ++version);
            AssertSame(reference.Parse(text[..length], 0, RemendOptions.Default), [.. snapshot.Blocks], $"length={length}");
            if (length == text.Length)
            {
                break;
            }
        }
        var final = composer.Finish(text, ++version);
        AssertSame(reference.Parse(text, 0, RemendOptions.Default), [.. final.Blocks], "final");
        var call = Assert.IsType<ComponentBlock>(final.Blocks.ToList()[1]);
        Assert.Equal("{\"q\": \"**b\",\n\n \"r\": \"20~25\"}", call.Raw);
        Assert.False(call.IsOpen);
    }

    [Fact]
    public void Segmenter_DoesNotFreezeInsideRawComponent_UntilClosingLine()
    {
        var segmenter = new StreamSegmenter([], ["tool-call"]);
        var text = "<tool-call>\n\na </tool-call> b\n\nmore\n";
        segmenter.Advance(text, 0);
        Assert.Equal(0, segmenter.FrozenEnd);
        var closed = text + "</tool-call>\n\nafter";
        segmenter.Advance(closed.AsSpan(segmenter.FrozenEnd), segmenter.FrozenEnd);
        Assert.True(segmenter.FrozenEnd > 0);
    }

    private static void AssertSame(IReadOnlyList<MarkdownBlock> expected, IReadOnlyList<MarkdownBlock> actual, string context)
    {
        Assert.True(expected.Count == actual.Count, $"{context}: {expected.Count} vs {actual.Count} blocks");
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.True(expected[i].Range == actual[i].Range, $"{context}: block {i} range {expected[i].Range} vs {actual[i].Range}");
            Assert.True(expected[i].ComputeFingerprint() == actual[i].ComputeFingerprint(), $"{context}: block {i}\n{expected[i]}\n---\n{actual[i]}");
        }
    }
}
