using StreamMark.Core.Model;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Core.Tests.Parsing;

public sealed class TaskMarkerTests
{
    private static async Task<DocumentSnapshot> Parse(string text)
    {
        var document = new StreamingDocument();
        document.Load(text);
        await document.WhenIdle();
        return document.Latest;
    }

    // 所有列表项，含嵌套在列表项、引用里的
    private static IEnumerable<ListItemBlock> Items(IEnumerable<MarkdownBlock> blocks)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case ListBlock list:
                    foreach (var item in list.Items)
                    {
                        yield return item;
                        foreach (var nested in Items(item.Children))
                        {
                            yield return nested;
                        }
                    }
                    break;
                case QuoteBlock quote:
                    foreach (var nested in Items(quote.Children))
                    {
                        yield return nested;
                    }
                    break;
            }
        }
    }

    private static string Slice(string text, SourceRange range) => text.Substring(range.Start, range.Length);

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task TaskMarker_PointsAtTheBracketsInSource_ForEveryListShape(string newline)
    {
        var text = string.Join(newline,
            "- [ ] unordered open",
            "- [x] unordered done",
            "- plain item",
            "",
            "1. [X] ordered upper-case",
            "2. [ ] ordered open",
            "",
            "- [ ] parent 中文 😀",
            "  - [x] child same label",
            "  - [ ] child same label",
            "    - [ ] grandchild",
            "",
            "> - [ ] quoted task",
            "> - [x] quoted done",
            "",
            "- [ ] same label",
            "- [ ] same label",
            "");
        var snapshot = await Parse(text);
        var items = Items(snapshot.Blocks).ToList();

        var tasks = items.Where(i => i.Task is not null).ToList();
        Assert.Equal(12, tasks.Count);
        foreach (var item in tasks)
        {
            var marker = Slice(text, item.TaskMarker);
            Assert.Equal(3, item.TaskMarker.Length);
            Assert.Equal(item.Task is true, marker is "[x]" or "[X]");
            Assert.True(marker is "[ ]" or "[x]" or "[X]", $"unexpected marker text '{marker}'");
            // 标记在列表项里面，且在子块之前
            Assert.True(item.TaskMarker.Start >= item.Range.Start && item.TaskMarker.End <= item.Range.End);
        }
        foreach (var item in items.Where(i => i.Task is null))
        {
            Assert.Equal(0, item.TaskMarker.Length);
        }

        // 重复的标签靠源码位置区分：两个 "same label" 的标记位置不同
        var same = tasks.Where(i => i.Children is [ParagraphBlock p, ..] && p.Content.Text == "same label").ToList();
        Assert.Equal(2, same.Count);
        Assert.NotEqual(same[0].TaskMarker.Start, same[1].TaskMarker.Start);
        Assert.Equal(text.IndexOf("- [ ] same label", StringComparison.Ordinal) + 2, same[0].TaskMarker.Start);
    }

    [Fact]
    public async Task TaskMarker_ChangesFingerprint_WhenCheckedStateOrRelativePositionChanges()
    {
        static ulong Fingerprint(DocumentSnapshot snapshot) => snapshot[0].ComputeFingerprint();

        var open = await Parse("- [ ] item\n");
        var done = await Parse("- [x] item\n");
        var spaced = await Parse("-  [ ] item\n");

        // [ ] → [x]：状态变了，必须产生 Update
        Assert.NotEqual(Fingerprint(open), Fingerprint(done));
        Assert.NotEmpty(SnapshotDiff.Compute(open, done));

        // 项目符号后多一个空格：标记在项内的相对位置变了，指纹也要变（否则视图会拿着旧区间）
        var openItem = Assert.Single(Items(open.Blocks));
        var spacedItem = Assert.Single(Items(spaced.Blocks));
        Assert.Equal(openItem.TaskMarker.Start + 1, spacedItem.TaskMarker.Start);
        Assert.NotEqual(Fingerprint(open), Fingerprint(spaced));
    }

    [Fact]
    public async Task TaskMarker_FingerprintIsStableWhenWholeListShifts()
    {
        var before = await Parse("- [ ] a\n- [x] b\n");
        var after = await Parse("intro paragraph\n\n- [ ] a\n- [x] b\n");

        // 前面多了一段，列表整体平移：内容指纹相同（只有 Range 不同）
        Assert.Equal(before[0].ComputeFingerprint(), after[1].ComputeFingerprint());
        Assert.NotEqual(before[0].Range.Start, after[1].Range.Start);
    }
}
