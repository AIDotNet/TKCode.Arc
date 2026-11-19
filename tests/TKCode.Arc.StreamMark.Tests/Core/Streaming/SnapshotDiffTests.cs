using StreamMark.Core.Model;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Core.Tests.Streaming;

public sealed class SnapshotDiffTests
{
    private static List<MarkdownBlock> Apply(DocumentSnapshot? previous, DocumentSnapshot next)
    {
        var list = previous?.Blocks.ToList() ?? [];
        foreach (var op in SnapshotDiff.Compute(previous, next))
        {
            switch (op.Kind)
            {
                case DiffKind.Insert:
                    list.Insert(op.Index, op.Block!);
                    break;
                case DiffKind.Update:
                    list[op.Index] = op.Block!;
                    break;
                case DiffKind.Remove:
                    list.RemoveAt(op.Index);
                    break;
            }
        }
        return list;
    }

    [Fact]
    public void AppliedOps_ReproduceEverySnapshot()
    {
        var text = StreamingEquivalenceTests.Fixture("chat-mixed.md");
        var composer = new DocumentComposer();
        DocumentSnapshot? previous = null;
        List<MarkdownBlock> applied = [];
        for (var i = 3; i <= text.Length + 3; i += 3)
        {
            var end = Math.Min(i, text.Length);
            var from = composer.FrozenEnd;
            var snapshot = composer.Update(text[from..end], from, i);
            applied = Apply(previous, snapshot);
            Assert.Equal(snapshot.Count, applied.Count);
            for (var b = 0; b < applied.Count; b++)
            {
                Assert.Equal(snapshot[b].Key, applied[b].Key);
                Assert.Equal(snapshot[b].ComputeFingerprint(), applied[b].ComputeFingerprint());
            }
            previous = snapshot;
        }
        var final = composer.Finish(text, long.MaxValue);
        applied = Apply(previous, final);
        Assert.Equal(final.Count, applied.Count);
    }

    [Fact]
    public void TypingIntoLastParagraph_ProducesSingleUpdate()
    {
        var composer = new DocumentComposer();
        var first = composer.Update("# Title\n\nHello wor", 0, 1);
        var from = composer.FrozenEnd;
        var second = composer.Update("# Title\n\nHello world"[from..], from, 2);
        var ops = SnapshotDiff.Compute(first, second);
        var op = Assert.Single(ops);
        Assert.Equal(DiffKind.Update, op.Kind);
        Assert.Equal(1, op.Index);
    }

    [Fact]
    public void FreezingABlock_DoesNotProduceAnUpdateForIt()
    {
        var composer = new DocumentComposer();
        var first = composer.Update("para one", 0, 1);
        Assert.Equal(0, first.FrozenCount);
        var from = composer.FrozenEnd;
        var second = composer.Update("para one\n\npara two"[from..], from, 2);
        Assert.True(second.FrozenCount >= 1);
        var ops = SnapshotDiff.Compute(first, second);
        // 第一段从尾部变为冻结，内容不变，不应出现在差异中
        Assert.DoesNotContain(ops, o => o.Index == 0);
    }
}
