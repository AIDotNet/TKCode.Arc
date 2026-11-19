using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Core.Tests.Streaming;

/// <summary>切分器在调研中验证过的各类流式陷阱上不能过早冻结。</summary>
public sealed class SegmenterTests
{
    private static int FrozenAfter(string text)
    {
        var segmenter = new StreamSegmenter();
        // 逐字符推进，模拟最细粒度的流
        for (var i = 1; i <= text.Length; i++)
        {
            segmenter.Advance(text.AsSpan(segmenter.FrozenEnd, i - segmenter.FrozenEnd), segmenter.FrozenEnd);
        }
        return segmenter.FrozenEnd;
    }

    [Fact]
    public void ParagraphFollowedByBlankAndText_Freezes()
    {
        Assert.Equal("first\n\n".Length, FrozenAfter("first\n\nsecond paragraph"));
    }

    [Fact]
    public void LastBlock_IsNeverFrozen()
    {
        Assert.Equal(0, FrozenAfter("only paragraph\n\n"));
    }

    [Fact]
    public void FenceWithBlankLines_IsNotCut()
    {
        Assert.Equal(0, FrozenAfter("```js\na\n\nb\n"));
    }

    [Fact]
    public void ClosedFence_FreezesWhenNextLineArrives()
    {
        var text = "```js\na\n\nb\n```\nafter";
        Assert.Equal("```js\na\n\nb\n```\n".Length, FrozenAfter(text));
    }

    [Fact]
    public void ListFollowedByBlankAndAnotherItem_IsNotCut()
    {
        Assert.Equal(0, FrozenAfter("- a\n- b\n\n- c"));
    }

    [Fact]
    public void ListContinuationIndented_IsNotCut()
    {
        Assert.Equal(0, FrozenAfter("- a\n\n  more of a\n"));
    }

    [Fact]
    public void ListThenParagraph_Freezes()
    {
        Assert.Equal("- a\n- b\n\n".Length, FrozenAfter("- a\n- b\n\nParagraph"));
    }

    [Fact]
    public void PartialListMarker_WaitsForDecision()
    {
        // "2" 之后可能是 "2. "（列表续项），不能立即切
        Assert.Equal(0, FrozenAfter("1. a\n\n2"));
    }

    [Fact]
    public void DollarMathWithBlankLine_IsNotCut()
    {
        Assert.Equal(0, FrozenAfter("$$\na\n\nb\n"));
    }

    [Fact]
    public void HtmlComment_SpanningBlankLines_IsNotCut()
    {
        Assert.Equal(0, FrozenAfter("<!--\na\n\nb\n"));
    }

    [Fact]
    public void CustomContainer_SpanningBlankLines_IsNotCut()
    {
        Assert.Equal(0, FrozenAfter("::: note\na\n\nb\n"));
    }

    [Fact]
    public void ReferenceDefinition_StopsFreezing()
    {
        var segmenter = new StreamSegmenter();
        var text = "intro\n\n[a]: https://x\n\nmore\n\nand more";
        segmenter.Advance(text, 0);
        Assert.True(segmenter.SawDefinition);
        // 定义行本身不能作为切点后的首行，之后也不再冻结
        Assert.Equal(0, segmenter.FrozenEnd);
    }

    [Fact]
    public void OpenFenceAtRunStart_IsReportedForFastPath()
    {
        var segmenter = new StreamSegmenter();
        var text = "intro\n\n```python\nprint(1)\n";
        segmenter.Advance(text, 0);
        Assert.Equal("intro\n\n".Length, segmenter.FrozenEnd);
        Assert.True(segmenter.TryGetOpenFence("", out var fenceStart, out var contentStart, out _, out var info));
        Assert.Equal("intro\n\n".Length, fenceStart);
        Assert.Equal("intro\n\n```python\n".Length, contentStart);
        Assert.Equal("python", info);
    }

    [Fact]
    public void Boundary_NeverMovesBackwards()
    {
        var text = StreamingEquivalenceTests.Fixture("chat-mixed.md");
        var segmenter = new StreamSegmenter();
        var last = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            var frozen = segmenter.Advance(text.AsSpan(segmenter.FrozenEnd, i - segmenter.FrozenEnd), segmenter.FrozenEnd);
            Assert.True(frozen >= last);
            last = frozen;
        }
    }
}
