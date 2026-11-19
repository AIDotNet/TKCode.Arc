using StreamMark.Core.Model;
using StreamMark.Core.Remend;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Core.Tests.Parsing;

public sealed class BlockKeyTests
{
    // 回归：脚注组在 Markdig 中没有区间，曾以 0 为 key，与第一个块重复
    [Fact]
    public void TopLevelKeys_AreUnique_IncludingFootnotes()
    {
        var blocks = new DocumentComposer().Parse("# Title\n\nText[^a] and[^b].\n\n[^a]: First.\n[^b]: Second.\n", 0, RemendOptions.Default).ToList();
        Assert.Equal(blocks.Count, blocks.Select(b => b.Key).Distinct().Count());
        var footnotes = Assert.IsType<FootnotesBlock>(blocks[^1]);
        Assert.True(footnotes.Key > blocks[1].Key);
    }
}
