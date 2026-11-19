using StreamMark.Core.Pacing;
using Xunit;

namespace StreamMark.Core.Tests.Pacing;

public sealed class StreamPacerTests
{
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void BurstyInput_IsRevealedGradually()
    {
        var pacer = new StreamPacer();
        pacer.Push(new string('a', 300), Ms(0));
        var first = pacer.Tick(Ms(16));
        Assert.InRange(first.Length, 1, 299);
    }

    [Fact]
    public void AfterIdleGap_FirstTick_DoesNotDumpBacklog()
    {
        var pacer = new StreamPacer();
        pacer.Push("hello", Ms(0));
        for (var t = 16; pacer.Backlog > 0 && t < 2000; t += 16)
        {
            pacer.Tick(Ms(t));
        }
        Assert.Equal(0, pacer.Backlog);
        // 5 秒空闲后到达一大段：下一帧只放出一部分
        pacer.Push(new string('a', 300), Ms(5000));
        var first = pacer.Tick(Ms(5016));
        Assert.InRange(first.Length, 1, 299);
    }

    [Fact]
    public void NoCharacter_WaitsLongerThanMaxLag()
    {
        var pacer = new StreamPacer();
        pacer.Push(new string('a', 1000), Ms(0));
        pacer.Tick(Ms(1));
        pacer.Tick(Ms(450));
        Assert.Equal(0, pacer.Backlog);
    }

    [Fact]
    public void Finish_DrainsWithinMaxDrain()
    {
        var pacer = new StreamPacer();
        for (var t = 0; t < 200; t += 20)
        {
            pacer.Push("hello world ", Ms(t));
            pacer.Tick(Ms(t));
        }
        pacer.Finish(Ms(200));
        var time = 200.0;
        while (!pacer.IsDrained && time < 2000)
        {
            time += 16;
            pacer.Tick(Ms(time));
        }
        Assert.True(pacer.IsDrained);
        Assert.True(time - 200 <= 540, $"drained after {time - 200}ms");
    }

    [Fact]
    public void Release_NeverSplitsGraphemeClusters()
    {
        var pacer = new StreamPacer(new StreamPacerOptions { Preset = PacingPreset.Smooth });
        var text = string.Concat(Enumerable.Repeat("👨‍👩‍👧中文é", 40));
        pacer.Push(text, Ms(0));
        var revealed = "";
        for (var t = 16; t < 1000; t += 16)
        {
            var piece = pacer.Tick(Ms(t));
            if (piece.Length > 0)
            {
                Assert.False(char.IsLowSurrogate(piece[0]));
                Assert.NotEqual('\u200D', piece[0]);
                Assert.False(char.IsHighSurrogate(piece[^1]));
            }
            revealed += piece;
        }
        Assert.Equal(text, revealed);
    }

    [Fact]
    public void LargeAppend_IsBypassed()
    {
        var pacer = new StreamPacer();
        pacer.Push(new string('x', 10_000), Ms(0));
        Assert.Equal(10_000, pacer.Tick(Ms(16)).Length);
    }

    [Fact]
    public void RevealTime_IsRecordedForRecentText()
    {
        var pacer = new StreamPacer();
        pacer.Push("abcdef", Ms(0));
        pacer.Tick(Ms(500));
        Assert.Equal(Ms(500), pacer.GetRevealTime(0));
        Assert.Null(pacer.GetRevealTime(100));
    }

    [Fact]
    public void SkipCompletedBlocks_ReleasesUpToLastBlankLine()
    {
        var pacer = new StreamPacer(new StreamPacerOptions { SkipCompletedBlocks = true });
        var text = "First paragraph is complete.\n\nSecond paragraph is complete too.\n\nThird is still stream";
        pacer.Push(text, Ms(0));
        var first = pacer.Tick(Ms(16));
        Assert.EndsWith("too.\n\n", first);
        Assert.True(pacer.Backlog > 0);

        var normal = new StreamPacer();
        normal.Push(text, Ms(0));
        Assert.True(normal.Tick(Ms(16)).Length < first.Length);
    }
}
