using StreamMark.Core.Model;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Core.Tests.Streaming;

public sealed class SnapshotEditTests
{
    private static DocumentSnapshot Parse(string text) => new DocumentComposer().Finish(text, 1);

    private static List<MarkdownBlock> Apply(DocumentSnapshot previous, SnapshotEdit edit)
    {
        var list = previous.Blocks.ToList();
        foreach (var op in edit.Ops)
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

    private static void AssertSame(IReadOnlyList<MarkdownBlock> expected, IReadOnlyList<MarkdownBlock> actual, string context)
    {
        Assert.True(expected.Count == actual.Count, $"{context}: {expected.Count} blocks vs {actual.Count}");
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.True(expected[i].GetType() == actual[i].GetType(), $"{context}: block {i} type");
            Assert.True(expected[i].Range == actual[i].Range, $"{context}: block {i} range {expected[i].Range} vs {actual[i].Range}");
            Assert.True(expected[i].ComputeFingerprint() == actual[i].ComputeFingerprint(), $"{context}: block {i} content");
        }
    }

    private const string Base = "# Title\n\nFirst paragraph.\n\nSecond paragraph.\n\nThird paragraph.\n\nLast paragraph.\n";

    [Fact]
    public void IdenticalText_ProducesNoOps()
    {
        var edit = SnapshotEdit.Compute(Parse(Base), Parse(Base));
        Assert.Empty(edit.Ops);
        Assert.All(edit.Moves, m => Assert.Equal(BlockFate.Unchanged, m.Fate));
    }

    [Fact]
    public void EditingTheLastParagraph_TouchesOnlyThatBlock()
    {
        var before = Parse(Base);
        var after = Parse(Base.Replace("Last paragraph.", "Changed last paragraph, now longer."));
        var edit = SnapshotEdit.Compute(before, after);

        var op = Assert.Single(edit.Ops);
        Assert.Equal(new DiffOp(DiffKind.Update, 4, after[4]), op);
        Assert.Equal([BlockFate.Unchanged, BlockFate.Unchanged, BlockFate.Unchanged, BlockFate.Unchanged, BlockFate.Replaced], edit.Moves.Select(m => m.Fate));
        // 没变的块没有任何操作，选区和状态留在原处
        Assert.Equal(before[1].Key, edit.MapKey(before[1].Key));
        Assert.Equal(after[4].Key, edit.MapKey(before[4].Key));
        // 被替换块内部的偏移不可信
        Assert.Null(edit.MapKey(before[4].Key + 3));
    }

    [Fact]
    public void InsertingBeforeEverything_ShiftsTheRestInsteadOfRebuildingIt()
    {
        var before = Parse(Base);
        var inserted = "Brand new opening paragraph.\n\n";
        var after = Parse(inserted + Base);
        var edit = SnapshotEdit.Compute(before, after);

        // 一个插入 + 每个旧块一次原地更新；没有删除
        Assert.Equal(DiffKind.Insert, edit.Ops[0].Kind);
        Assert.Equal(0, edit.Ops[0].Index);
        Assert.Equal(before.Count, edit.Ops.Count(o => o.Kind == DiffKind.Update));
        Assert.DoesNotContain(edit.Ops, o => o.Kind == DiffKind.Remove);
        Assert.All(edit.Moves, m => Assert.Equal(BlockFate.Shifted, m.Fate));
        AssertSame(after.Blocks.ToList(), Apply(before, edit), "insert at start");

        // 块内任意偏移都按同一距离平移
        var second = before[2];
        Assert.Equal(second.Key + inserted.Length, edit.MapKey(second.Key));
        Assert.Equal(second.Key + 5 + inserted.Length, edit.MapKey(second.Key + 5));
        // 块与块之间的空隙不属于任何块
        Assert.Null(edit.MapKey(second.Range.End));
    }

    [Fact]
    public void DeletingAMiddleParagraph_RemovesOnlyItAndShiftsTheRest()
    {
        var before = Parse(Base);
        var after = Parse(Base.Replace("Second paragraph.\n\n", ""));
        var edit = SnapshotEdit.Compute(before, after);

        Assert.Equal([BlockFate.Unchanged, BlockFate.Unchanged, BlockFate.Deleted, BlockFate.Shifted, BlockFate.Shifted], edit.Moves.Select(m => m.Fate));
        Assert.Equal(1, edit.Ops.Count(o => o.Kind == DiffKind.Remove));
        AssertSame(after.Blocks.ToList(), Apply(before, edit), "delete middle");

        // 被删除的块没有映射；它的去向是后面第一个存活的块，选区可以退到那里
        Assert.Null(edit.MapKey(before[2].Key));
        Assert.Same(after[2], edit.Moves[2].New);
        Assert.Equal(after[2].Key, edit.MapKey(before[3].Key));
    }

    [Fact]
    public void DeletingTheLastParagraph_HasNoFallbackBlock()
    {
        var before = Parse(Base);
        var after = Parse(Base.Replace("\nLast paragraph.\n", ""));
        var edit = SnapshotEdit.Compute(before, after);

        Assert.Equal(BlockFate.Deleted, edit.Moves[^1].Fate);
        Assert.Null(edit.Moves[^1].New);
        AssertSame(after.Blocks.ToList(), Apply(before, edit), "delete last");
    }

    [Fact]
    public void ReplacingOneParagraphWithTwo_PairsOneAndInsertsTheOther()
    {
        var before = Parse(Base);
        var after = Parse(Base.Replace("Second paragraph.", "Second A.\n\nSecond B."));
        var edit = SnapshotEdit.Compute(before, after);

        Assert.Equal(BlockFate.Replaced, edit.Moves[2].Fate);
        Assert.Contains(edit.Ops, o => o.Kind == DiffKind.Insert);
        AssertSame(after.Blocks.ToList(), Apply(before, edit), "one to two");
    }

    [Fact]
    public void StateInNestedBlocks_FollowsTheEnclosingShiftedBlock()
    {
        const string list = "- one\n- two\n\n```js\nlet a = 1;\n```\n";
        var before = Parse(list);
        var after = Parse("Intro.\n\n" + list);
        var edit = SnapshotEdit.Compute(before, after);

        var code = before.Blocks.OfType<CodeBlock>().Single();
        var item = ((ListBlock)before[0]).Items[1];
        Assert.Equal(code.Key + 8, edit.MapKey(code.Key));
        // 嵌套在列表里的块（它们的 Key 是各自的源码偏移）同样平移
        Assert.Equal(item.Key + 8, edit.MapKey(item.Key));
    }

    [Fact]
    public void TwoSeparateEdits_KeepTheUntouchedBlocksBetweenThemPaired()
    {
        const string text = "Intro.\n\n```js\nlet a = 1;\n```\n\nMiddle.\n\n```js\nlet b = 2;\n```\n";
        var before = Parse(text);
        // 开头增加一段，同时末尾的代码块被删除：前缀与后缀都对不上，中间的 Intro 与第一个代码块仍要配上
        var after = Parse("Brand new text.\n\n" + text[..text.IndexOf("Middle.", StringComparison.Ordinal)]);
        var edit = SnapshotEdit.Compute(before, after);

        Assert.Equal([BlockFate.Shifted, BlockFate.Shifted, BlockFate.Deleted, BlockFate.Deleted], edit.Moves.Select(m => m.Fate));
        Assert.Same(after[1], edit.Moves[0].New);
        Assert.Same(after[2], edit.Moves[1].New);
        Assert.Null(edit.Moves[3].New);
        AssertSame(after.Blocks.ToList(), Apply(before, edit), "two edits");
    }

    [Fact]
    public void TwoSeparateEdits_InsertAtTopAndChangeAtBottom()
    {
        var before = Parse(Base);
        var after = Parse("Opening line.\n\n" + Base.Replace("Last paragraph.", "A different ending."));
        var edit = SnapshotEdit.Compute(before, after);

        Assert.Equal([BlockFate.Shifted, BlockFate.Shifted, BlockFate.Shifted, BlockFate.Shifted, BlockFate.Replaced], edit.Moves.Select(m => m.Fate));
        AssertSame(after.Blocks.ToList(), Apply(before, edit), "top and bottom");
    }

    [Theory]
    [InlineData("chat-mixed.md")]
    [InlineData("lists-quotes.md")]
    [InlineData("code-heavy.md")]
    public void RandomEdits_AppliedOps_ReproduceTheNewDocument(string fixture)
    {
        var text = StreamingEquivalenceTests.Fixture(fixture);
        var random = new Random(20261021);
        string[] snippets = ["\n\nA new paragraph.\n\n", "x", "# Heading\n\n", "- item\n", "\n\n```\ncode\n```\n\n", "**bold** and [link](https://x.y)"];
        for (var round = 0; round < 80; round++)
        {
            var edited = text;
            for (var edits = random.Next(1, 4); edits > 0; edits--)
            {
                var at = random.Next(0, edited.Length + 1);
                edited = random.Next(3) == 0 && at < edited.Length
                    ? edited.Remove(at, Math.Min(random.Next(1, 240), edited.Length - at))
                    : edited.Insert(at, snippets[random.Next(snippets.Length)]);
            }

            var before = Parse(text);
            var after = Parse(edited);
            var edit = SnapshotEdit.Compute(before, after);
            var context = $"{fixture} round {round}";

            AssertSame(after.Blocks.ToList(), Apply(before, edit), context);
            Assert.Equal(before.Count, edit.Moves.Count);
            foreach (var move in edit.Moves)
            {
                if (move.Fate == BlockFate.Deleted)
                {
                    Assert.Null(edit.MapKey(move.Old.Key));
                    continue;
                }
                Assert.True(edit.MapKey(move.Old.Key) == move.New!.Key, $"{context}: key {move.Old.Key}");
                if (move.Fate is BlockFate.Unchanged or BlockFate.Shifted)
                {
                    Assert.True(move.Old.ComputeFingerprint() == move.New.ComputeFingerprint(), $"{context}: content of {move.Old.Key}");
                    Assert.Equal(move.Old.Range.Length, move.New.Range.Length);
                }
            }
        }
    }
}
