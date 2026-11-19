// Property tests ported from vercel/streamdown packages/remend/__tests__/streaming-properties.test.ts,
// Copyright 2023 Vercel, Inc., Apache-2.0; modified: fast-check is replaced by a seeded generator
// with the same document shapes, and Markdig stands in for mdast-util-from-markdown as the
// CommonMark oracle for code.

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Xunit;
using RemendApi = StreamMark.Core.Remend.Remend;

namespace StreamMark.Core.Tests.Remend;

public sealed class RemendStreamingPropertyTests
{
    // A streaming consumer feeds every prefix of a document through remend, so
    // these tests assemble documents from COMPLETE constructs and derive all
    // truncation from the streaming cut. The atoms deliberately include
    // identifiers with double-underscore runs (snake__case style), which look
    // like emphasis delimiters to a context-free counter.
    private static readonly string[] InlineAtoms =
    [
        "plain words",
        "**bold text**",
        "*italic text*",
        "_underscore italic_",
        "__strong text__",
        "___strong italic___",
        "~~struck text~~",
        "`inline code`",
        "``double `tick` span``",
        "snake__case",
        "user__id",
        "a_b_c",
        "[label](https://example.com)",
        "![alt](https://example.com/pic.png)",
        "<b>tag</b>",
        "value is 20~25 degrees",
    ];

    private static readonly string[] BlockAtoms =
    [
        "# heading",
        "> a quote",
        "- item one\n- item two",
        "```js\nconst total__count = 1;\n```",
        "~~~\ntilde fenced\n~~~",
        "$$\nx^2\n$$",
    ];

    // The longest fragment healing may legitimately drop: an incomplete
    // construct cut mid-stream is at most one atom long, plus the stripped
    // trailing space
    private static readonly int MaxLoss = InlineAtoms.Concat(BlockAtoms).Max(a => a.Length) + 2;

    private const int Seed = 20231031;

    // Length of the longest prefix of `input` that appears as a subsequence of
    // `output`. Healing may insert characters (closers, escapes) and drop a
    // trailing fragment, so authored text is preserved exactly when everything
    // except a bounded tail survives as a subsequence.
    private static int PreservedPrefixLength(string input, string output)
    {
        var matched = 0;
        for (var i = 0; i < output.Length && matched < input.Length; i++)
        {
            if (output[i] == input[matched])
            {
                matched++;
            }
        }
        return matched;
    }

    private sealed record CodeNode(string Type, string Value, string? Lang, string? Meta);

    private static readonly MarkdownPipeline CommonMark = new MarkdownPipelineBuilder().Build();

    // A CommonMark parser is the oracle for what is code, including fences nested
    // in block quotes and list items that a line-based scan can miss
    private static List<CodeNode> CodeNodes(string markdown)
    {
        var found = new List<CodeNode>();
        foreach (var node in Markdown.Parse(markdown, CommonMark).Descendants())
        {
            switch (node)
            {
                case FencedCodeBlock fenced:
                    found.Add(new CodeNode("code", fenced.Lines.ToString(), NullIfEmpty(fenced.Info), NullIfEmpty(fenced.Arguments)));
                    break;
                case CodeBlock block:
                    found.Add(new CodeNode("code", block.Lines.ToString(), null, null));
                    break;
                case CodeInline inline:
                    found.Add(new CodeNode("inlineCode", inline.Content, null, null));
                    break;
            }
        }
        return found;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    // Healing must never change code. Every code block and span the parser finds
    // in the prefix survives unchanged, with two exceptions at the end of the
    // text. Healing strips trailing whitespace, which may trim the last node. And
    // closing an open span absorbs the spans the prefix read inside it: in
    // ``a `b` the prefix holds the span `b`, which closing `` swallows.
    private static string? CheckCodeUnchanged(string prefix, string healed)
    {
        var before = CodeNodes(prefix);
        var after = CodeNodes(healed);

        var common = 0;
        while (common < before.Count && common < after.Count && before[common] == after[common])
        {
            common++;
        }
        if (before.Count > 0
            && common == before.Count - 1
            && common == after.Count - 1
            && before[^1] with { Value = before[^1].Value.TrimEnd() } == after[common])
        {
            return null;
        }

        var lost = before.Skip(common).ToList();
        var added = after.Skip(common).ToList();
        if (added.Count == 0 && lost.Count == 0)
        {
            return null;
        }
        var closedSpan = added.Count == 1
            && added[0].Type == "inlineCode"
            && lost.All(node => node.Type == "inlineCode" && added[0].Value.Contains(node.Value, StringComparison.Ordinal));
        return closedSpan
            ? null
            : $"healing changed code: {Quote(prefix)} -> {Quote(healed)}\nlost: {string.Join(", ", lost)}\nadded: {string.Join(", ", added)}";
    }

    private static string Quote(string value) => RemendFixtureTests.Quote(value);

    private static string? CheckStreamingSafe(string prefix)
    {
        var healed = RemendApi.Complete(prefix);
        if (CheckCodeUnchanged(prefix, healed) is { } codeFailure)
        {
            return codeFailure;
        }

        var loss = prefix.Length - PreservedPrefixLength(prefix, healed);
        if (loss > MaxLoss)
        {
            return $"healing dropped {loss} chars of {Quote(prefix)} -> {Quote(healed)}";
        }

        var rehealed = RemendApi.Complete(healed);
        if (rehealed != healed)
        {
            return $"healing is not idempotent: {Quote(prefix)} -> {Quote(healed)} -> {Quote(rehealed)}";
        }
        return null;
    }

    // A document is a sequence of atoms. Inline atoms join with spaces into
    // paragraphs, block atoms stand alone, and everything joins with blank lines
    // so fences and headings begin at a line start.
    private static string GenerateDocument(Random random, string[] inlineAtoms)
    {
        var groups = new List<string>();
        var groupCount = random.Next(1, 7);
        for (var g = 0; g < groupCount; g++)
        {
            // oneof with weights 3 (inline subarray) : 1 (single block atom)
            if (random.Next(4) < 3)
            {
                List<string> picked;
                do
                {
                    picked = inlineAtoms.Where(_ => random.Next(2) == 0).ToList();
                }
                while (picked.Count == 0);
                groups.Add(string.Join(" ", picked));
            }
            else
            {
                groups.Add(BlockAtoms[random.Next(BlockAtoms.Length)]);
            }
        }
        return string.Join("\n\n", groups);
    }

    // Container blocks shift where a line's content starts, so every construct
    // must behave the same once its lines carry a quote marker or list indent
    private static Func<string, string> PrefixLines(string first, string rest, string blank) => doc =>
        string.Join("\n", doc.Split('\n').Select((line, index) =>
            index == 0 ? first + line : line.Length > 0 ? rest + line : blank));

    private static readonly Func<string, string>[] Containers =
    [
        PrefixLines("> ", "> ", ">"),   // quote
        PrefixLines(">", ">", ">"),     // tightQuote
        PrefixLines("- ", "  ", ""),    // bullet
        PrefixLines("1. ", "   ", ""),  // ordered
    ];

    private static string Contain(Random random, string doc)
    {
        var count = random.Next(1, 3);
        for (var i = 0; i < count; i++)
        {
            doc = Containers[random.Next(Containers.Length)](doc);
        }
        return doc;
    }

    private static void AssertAll(IEnumerable<string?> failures)
    {
        var list = failures.Where(f => f is not null).Take(10).ToList();
        Assert.True(list.Count == 0, string.Join("\n\n", list));
    }

    [Fact]
    public void PreservesAuthoredTextAndReHealsOnEveryCut()
    {
        var random = new Random(Seed);
        AssertAll(Enumerable.Range(0, 2000).Select(_ =>
        {
            var doc = GenerateDocument(random, InlineAtoms);
            var cut = random.Next(doc.Length + 1);
            return CheckStreamingSafe(doc[..cut]);
        }).ToList());
    }

    [Fact]
    public void PreservesAuthoredTextAndCodeOnEveryCutInsideContainers()
    {
        var random = new Random(Seed + 1);
        AssertAll(Enumerable.Range(0, 2000).Select(_ =>
        {
            var doc = Contain(random, GenerateDocument(random, InlineAtoms));
            var cut = random.Next(doc.Length + 1);
            return CheckStreamingSafe(doc[..cut]);
        }).ToList());
    }

    [Fact]
    public void EveryPrefixOfGeneratedDocumentsIsStreamingSafe()
    {
        // Stronger than one random cut per document: sweep every cut
        var random = new Random(Seed + 2);
        AssertAll(Enumerable.Range(0, 150).SelectMany(i =>
        {
            var doc = GenerateDocument(random, InlineAtoms);
            if (i % 2 == 1)
            {
                doc = Contain(random, doc);
            }
            return Enumerable.Range(0, doc.Length + 1).Select(cut => CheckStreamingSafe(doc[..cut]));
        }).ToList());
    }

    [Fact]
    public void DoesNotModifyCompleteDocuments()
    {
        // Escape-oriented handlers (comparison operators, single tilde between
        // digits) intentionally rewrite complete text, so their trigger shapes
        // are excluded here
        var noOpAtoms = InlineAtoms.Where(atom => !atom.Contains("20~25", StringComparison.Ordinal)).ToArray();
        var random = new Random(Seed + 3);
        AssertAll(Enumerable.Range(0, 1000).Select(_ =>
        {
            var doc = GenerateDocument(random, noOpAtoms);
            var healed = RemendApi.Complete(doc);
            return healed == doc ? null : $"modified complete document {Quote(doc)} -> {Quote(healed)}";
        }).ToList());
    }

    // Every prefix of a fixed corpus, deterministically. The corpus mixes
    // constructs that interact.
    private static readonly string[] Corpus =
    [
        "Use snake__case for names and __bold text__ throughout.",
        "The `obj__attr` field pairs with **bold** and _italic_ text.",
        "```python\ndef f():\n    return a__b\n```\n\nAfter the fence __open",
        "~~~\ntilde __fence\n~~~\n\n~~struck~~ and more",
        "A [link](https://example.com) and ![img](https://example.com/a.png) done.",
        "Math $$\nx^2 + y^2\n$$ and `code` mixed with ___strong italic___.",
        "| a | b |\n| - | - |\n| 1 | 2 |\n\nTable then **bold**",
        "Nested **bold with *italic* inside** plus ``double `tick` span``.",
    ];

    [Fact]
    public void ExhaustivePrefixSweepIsStreamingSafe()
    {
        AssertAll(Corpus.SelectMany(doc =>
            Enumerable.Range(0, doc.Length + 1).Select(cut => CheckStreamingSafe(doc[..cut]))).ToList());
    }

    [Fact]
    public void HealingALongTailIsIdempotent()
    {
        // A long streamed message exercises the pooled region buffers
        var random = new Random(Seed + 4);
        var builder = new System.Text.StringBuilder();
        while (builder.Length < 60_000)
        {
            builder.Append(GenerateDocument(random, InlineAtoms)).Append("\n\n");
        }
        var doc = builder.ToString();
        AssertAll(Enumerable.Range(0, 40).Select(_ => CheckStreamingSafe(doc[..random.Next(doc.Length + 1)])).ToList());
    }
}
