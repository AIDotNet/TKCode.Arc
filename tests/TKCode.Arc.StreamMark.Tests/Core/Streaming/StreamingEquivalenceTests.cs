using System.Collections.Immutable;
using StreamMark.Core.Model;
using StreamMark.Core.Remend;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Core.Tests.Streaming;

/// <summary>
/// 核心属性：无论文本以怎样的分片流入，每一步的快照都必须与"把当前已到达的全部文本整篇解析"的结果一致；
/// 冻结边界只前进、已冻结的块对象不再变化；流结束后与整篇解析完全一致。
/// </summary>
public sealed class StreamingEquivalenceTests
{
    public static TheoryData<string, int> Cases()
    {
        var data = new TheoryData<string, int>();
        foreach (var file in new[] { "chat-mixed.md", "code-heavy.md", "lists-quotes.md", "components.md", "html-readme.md" })
        {
            foreach (var chunk in new[] { 1, 3, 7, 32, 500 })
            {
                data.Add(file, chunk);
            }
        }
        // 随机分片（固定种子）
        data.Add("chat-mixed.md", -1);
        data.Add("code-heavy.md", -2);
        data.Add("lists-quotes.md", -3);
        data.Add("components.md", -4);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EverySnapshot_MatchesFullParseOfPrefix(string file, int chunk)
    {
        var text = Fixture(file);
        var composer = new DocumentComposer();
        var reference = new DocumentComposer();
        var previousFrozenEnd = 0;
        DocumentSnapshot? previous = null;
        var length = 0;
        var version = 0L;

        foreach (var piece in Split(text, chunk))
        {
            length += piece;
            var prefix = text[..length];
            var from = composer.FrozenEnd;
            var snapshot = composer.Update(prefix[from..], from, ++version);

            Assert.True(snapshot.FrozenSourceEnd >= previousFrozenEnd, "frozen boundary moved backwards");
            previousFrozenEnd = snapshot.FrozenSourceEnd;

            if (previous is not null)
            {
                for (var i = 0; i < previous.FrozenCount; i++)
                {
                    Assert.Same(previous[i], snapshot[i]);
                }
            }
            previous = snapshot;

            var expected = reference.Parse(prefix, 0, RemendOptions.Default);
            AssertSameBlocks(expected, snapshot, $"{file} chunk={chunk} length={length}");
        }

        var final = composer.Finish(text, ++version);
        AssertSameBlocks(reference.Parse(text, 0, RemendOptions.Default), final, $"{file} final");
        Assert.True(final.IsComplete);
    }

    [Fact]
    public void FrozenBoundary_ActuallyAdvances_ForLongDocuments()
    {
        var text = Fixture("chat-mixed.md");
        var composer = new DocumentComposer();
        DocumentSnapshot? snapshot = null;
        for (var i = 1; i <= text.Length; i += 5)
        {
            var from = composer.FrozenEnd;
            snapshot = composer.Update(text[from..i], from, i);
        }
        // 大部分块应已冻结，尾部只剩最后几个块
        Assert.NotNull(snapshot);
        Assert.True(snapshot.FrozenCount >= snapshot.Count - 3, $"frozen {snapshot.FrozenCount} of {snapshot.Count}");
    }

    [Fact]
    public void ReferenceDefinitions_ResolveAfterFinish()
    {
        var text = Fixture("references.md");
        var composer = new DocumentComposer();
        for (var i = 1; i <= text.Length; i++)
        {
            var from = composer.FrozenEnd;
            composer.Update(text[from..i], from, i);
        }
        var final = composer.Finish(text, text.Length + 1);
        var paragraph = Assert.IsType<ParagraphBlock>(final[0]);
        Assert.Contains(paragraph.Content.Links, l => l.Url == "https://example.com/docs");
        Assert.Contains(paragraph.Content.Spans, s => s.Style.HasFlag(InlineStyle.FootnoteReference));
    }

    private static void AssertSameBlocks(ImmutableArray<MarkdownBlock> expected, DocumentSnapshot actual, string context)
    {
        var actualBlocks = actual.Blocks.ToList();
        Assert.True(expected.Length == actualBlocks.Count,
            $"{context}: expected {expected.Length} blocks [{Describe(expected)}] but got {actualBlocks.Count} [{Describe(actualBlocks)}]");
        for (var i = 0; i < expected.Length; i++)
        {
            var e = expected[i];
            var a = actualBlocks[i];
            Assert.True(e.GetType() == a.GetType(), $"{context}: block {i} type {e.GetType().Name} vs {a.GetType().Name}");
            Assert.True(e.Range == a.Range, $"{context}: block {i} ({e.GetType().Name}) range {e.Range} vs {a.Range}");
            Assert.True(e.ComputeFingerprint() == a.ComputeFingerprint(), $"{context}: block {i} ({e.GetType().Name}) content differs:\n{e}\n---\n{a}");
        }
    }

    private static string Describe(IEnumerable<MarkdownBlock> blocks) =>
        string.Join(", ", blocks.Select(b => $"{b.GetType().Name}@{b.Range.Start}+{b.Range.Length}"));

    internal static IEnumerable<int> Split(string text, int chunk)
    {
        if (chunk > 0)
        {
            for (var i = 0; i < text.Length; i += chunk)
            {
                yield return Math.Min(chunk, text.Length - i);
            }
            yield break;
        }
        var random = new Random(-chunk * 7919);
        var position = 0;
        while (position < text.Length)
        {
            var size = Math.Min(random.Next(1, 24), text.Length - position);
            position += size;
            yield return size;
        }
    }

    internal static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "streaming", name)).Replace("\r\n", "\n");
}
