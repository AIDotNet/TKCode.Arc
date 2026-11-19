using StreamMark.Core.Model;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Core.Tests.Streaming;

public sealed class StreamingDocumentTests
{
    [Fact]
    public async Task ConcurrentAppends_ConvergeToFullParse()
    {
        var text = StreamingEquivalenceTests.Fixture("chat-mixed.md");
        var document = new StreamingDocument();
        var snapshots = 0;
        document.SnapshotReady += _ => Interlocked.Increment(ref snapshots);

        for (var i = 0; i < text.Length; i += 4)
        {
            document.Append(text.AsSpan(i, Math.Min(4, text.Length - i)));
        }
        await document.WhenIdle();
        Assert.False(document.Latest.IsComplete);

        document.Complete();
        await document.WhenIdle();
        var final = document.Latest;
        Assert.True(final.IsComplete);
        Assert.Equal(text.Length, final.SourceLength);
        Assert.IsType<HeadingBlock>(final[0]);
        Assert.True(snapshots >= 2);
        Assert.Throws<InvalidOperationException>(() => document.Append("x"));
    }

    [Fact]
    public async Task Reset_StartsNewGeneration()
    {
        var document = new StreamingDocument();
        document.Load("# one\n\ntwo");
        await document.WhenIdle();
        var first = document.Latest;
        Assert.Equal(2, first.Count);

        document.Reset();
        document.Append("fresh");
        await document.WhenIdle();
        var second = document.Latest;
        Assert.NotEqual(first.Generation, second.Generation);
        Assert.IsType<ParagraphBlock>(Assert.Single(second.Blocks));
        // 跨代差异：先删除旧块，再插入新块
        var ops = SnapshotDiff.Compute(first, second);
        Assert.Contains(ops, o => o.Kind == DiffKind.Remove);
    }
}
