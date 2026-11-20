using StreamMark.Core.Model;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Core.Tests.Streaming;

/// <summary>增量块引擎在各类流式陷阱上的顶层冻结行为验证（替代原独立切分器测试）。</summary>
public sealed class SegmenterTests
{
    private static int FrozenAfter(string text)
    {
        var composer = new DocumentComposer();
        // 逐字符推进，模拟最细粒度的流
        for (var i = 1; i <= text.Length; i++)
        {
            composer.AppendDelta(text.AsSpan(i - 1, 1), i);
        }
        return composer.FrozenEnd;
    }

    [Fact]
    public void ParagraphFollowedByBlankAndCompleteLine_Freezes()
    {
        Assert.Equal("first\n\n".Length, FrozenAfter("first\n\nsecond paragraph\n"));
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
    public void ClosedFence_FreezesWhenNextCompleteLineArrives()
    {
        var text = "```js\na\n\nb\n```\nafter\n";
        Assert.Equal("```js\na\n\nb\n```\n".Length, FrozenAfter(text));
    }

    [Fact]
    public void ListFollowedByBlankAndAnotherItem_IsNotCut()
    {
        Assert.Equal(0, FrozenAfter("- a\n- b\n\n- c\n"));
    }

    [Fact]
    public void ListContinuationIndented_IsNotCut()
    {
        Assert.Equal(0, FrozenAfter("- a\n\n  more of a\n"));
    }

    [Fact]
    public void ListThenParagraph_Freezes()
    {
        Assert.Equal("- a\n- b\n\n".Length, FrozenAfter("- a\n- b\n\nParagraph\n"));
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
    public void ReferenceDefinition_ContinuesFreezingAndResolvesForwardAndBackwardLinks()
    {
        var composer = new DocumentComposer();
        var text = "see [b]\n\n[a]: https://a.example\n\nuse [a]\n\nmore\n\n[b]: https://b.example\n\ntail\n";
        var streaming = composer.AppendDelta(text.AsSpan(), 1);
        // 出现链接定义后仍继续冻结后续已完成块，避免长文退化为 O(N)
        Assert.True(composer.FrozenEnd > "see [b]\n\n[a]: https://a.example\n\n".Length);
        var final = composer.Finish(text, 2);
        var firstPara = Assert.IsType<ParagraphBlock>(final[0]);
        Assert.Single(firstPara.Content.Links);
        Assert.Equal("https://b.example", firstPara.Content.Links[0].Url);
        var secondPara = Assert.IsType<ParagraphBlock>(final[1]);
        Assert.Single(secondPara.Content.Links);
        Assert.Equal("https://a.example", secondPara.Content.Links[0].Url);
        // 流式快照中前向引用也已随定义到达即时解析
        var streamingFirst = Assert.IsType<ParagraphBlock>(streaming[0]);
        Assert.Single(streamingFirst.Content.Links);
        Assert.Equal("https://b.example", streamingFirst.Content.Links[0].Url);
    }

    [Fact]
    public void OpenFenceAtTail_EmitsOpenCodeBlockIncrementally()
    {
        var composer = new DocumentComposer();
        var text = "intro\n\n```python\nprint(1)\n";
        var snap = composer.AppendDelta(text.AsSpan(), 1);
        Assert.Equal("intro\n\n".Length, composer.FrozenEnd);
        var code = Assert.IsType<CodeBlock>(snap[1]);
        Assert.True(code.IsOpen);
        Assert.Equal("python", code.Language);
        Assert.Equal("print(1)", code.Code);
    }

    [Fact]
    public void Boundary_NeverMovesBackwards()
    {
        var text = StreamingEquivalenceTests.Fixture("chat-mixed.md");
        var composer = new DocumentComposer();
        var last = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            composer.AppendDelta(text.AsSpan(i - 1, 1), i);
            Assert.True(composer.FrozenEnd >= last);
            last = composer.FrozenEnd;
        }
    }
}
