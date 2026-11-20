using System.Collections.Immutable;
using System.Text;
using StreamMark.Core.Model;
using StreamMark.Core.Parsing;
using StreamMark.Core.Streaming;
using Xunit;

namespace StreamMark.Core.Tests.Parsing;

/// <summary>
/// 原生零 Markdig 增量解析器的综合回归套件：
/// 覆盖 CommonMark 0.31.2 核心规范、GFM/CJK/StreamMark 扩展、容器内 O(1) 增量引用复用，
/// 以及零依赖自定义块/行内语法扩展（<see cref="IBlockSyntaxRule"/> / <see cref="IInlineSyntaxRule"/>）。
/// </summary>
public sealed class IncrementalParserTests
{
    // ---- 1. CommonMark 0.31.2 核心块级与行内规范 ----

    [Fact]
    public void CommonMark_AtxAndSetextHeadings_AndThematicBreaks()
    {
        var md = """
            # Heading 1 ###
            ###### Heading 6
            ####### Not a heading

            Setext H1
            ===

            Setext H2
            ---

            ***

            - - -

            ___
            """;

        var blocks = MarkdownParser.Parse(md);
        Assert.Equal(8, blocks.Length);

        var h1 = Assert.IsType<HeadingBlock>(blocks[0]);
        Assert.Equal(1, h1.Level);
        Assert.Equal("Heading 1", h1.Content.Text);
        Assert.Equal("heading-1", h1.Id);

        var h6 = Assert.IsType<HeadingBlock>(blocks[1]);
        Assert.Equal(6, h6.Level);
        Assert.Equal("Heading 6", h6.Content.Text);

        var notH = Assert.IsType<ParagraphBlock>(blocks[2]);
        Assert.Equal("####### Not a heading", notH.Content.Text);

        var s1 = Assert.IsType<HeadingBlock>(blocks[3]);
        Assert.Equal(1, s1.Level);
        Assert.Equal("Setext H1", s1.Content.Text);

        var s2 = Assert.IsType<HeadingBlock>(blocks[4]);
        Assert.Equal(2, s2.Level);
        Assert.Equal("Setext H2", s2.Content.Text);

        Assert.IsType<ThematicBreakBlock>(blocks[5]);
        Assert.IsType<ThematicBreakBlock>(blocks[6]);
        Assert.IsType<ThematicBreakBlock>(blocks[7]);
    }

    [Fact]
    public void CommonMark_HeadingSlugs_DeduplicateGitHubStyle()
    {
        var blocks = MarkdownParser.Parse("# API Guide\n\n## API Guide\n\n### API Guide\n");
        Assert.Equal("api-guide", Assert.IsType<HeadingBlock>(blocks[0]).Id);
        Assert.Equal("api-guide-1", Assert.IsType<HeadingBlock>(blocks[1]).Id);
        Assert.Equal("api-guide-2", Assert.IsType<HeadingBlock>(blocks[2]).Id);
    }

    [Fact]
    public void CommonMark_FencedAndIndentedCodeBlocks()
    {
        var md = "  ```csharp title=demo\n    line1\n  line2\n  ```\n\n~~~~\n```\nnot closed by ticks\n~~~~\n\n    indented 1\n\tindented tab\n\n    after blank\n";
        var blocks = MarkdownParser.Parse(md);
        Assert.Equal(3, blocks.Length);

        var c1 = Assert.IsType<CodeBlock>(blocks[0]);
        Assert.True(c1.IsFenced);
        Assert.False(c1.IsOpen);
        Assert.Equal("csharp", c1.Language);
        Assert.Equal("csharp title=demo", c1.Info);
        Assert.Equal("  line1\nline2", c1.Code);

        var c2 = Assert.IsType<CodeBlock>(blocks[1]);
        Assert.True(c2.IsFenced);
        Assert.Equal("```\nnot closed by ticks", c2.Code);

        var c3 = Assert.IsType<CodeBlock>(blocks[2]);
        Assert.False(c3.IsFenced);
        Assert.Equal("indented 1\nindented tab\n\nafter blank", c3.Code);
    }

    [Fact]
    public void CommonMark_Lists_DifferentBulletsTightLooseAndInterruptRules()
    {
        var md = """
            The number of windows is
            14. Not interrupting paragraph.

            1. First
            2. Second

            * Bullet A
            + Bullet B

            - Loose 1

            - Loose 2
            """;

        var blocks = MarkdownParser.Parse(md);
        Assert.Equal(5, blocks.Length);

        var p = Assert.IsType<ParagraphBlock>(blocks[0]);
        Assert.Contains("14. Not interrupting paragraph.", p.Content.Text);

        var ordered = Assert.IsType<ListBlock>(blocks[1]);
        Assert.True(ordered.IsOrdered);
        Assert.Equal(1, ordered.StartNumber);
        Assert.Equal(2, ordered.Items.Length);
        Assert.False(ordered.IsLoose);

        var listA = Assert.IsType<ListBlock>(blocks[2]);
        Assert.Equal('*', listA.Bullet);
        Assert.Single(listA.Items);

        var listB = Assert.IsType<ListBlock>(blocks[3]);
        Assert.Equal('+', listB.Bullet);
        Assert.Single(listB.Items);

        var loose = Assert.IsType<ListBlock>(blocks[4]);
        Assert.True(loose.IsLoose);
        Assert.Equal(2, loose.Items.Length);
    }

    [Fact]
    public void CommonMark_BlockQuote_NestedAndLazyContinuation()
    {
        var md = "> line one\nlazy line two\n>\n> > nested quote\n";
        var blocks = MarkdownParser.Parse(md);
        var outer = Assert.IsType<QuoteBlock>(Assert.Single(blocks));
        Assert.Equal(2, outer.Children.Length);
        var p = Assert.IsType<ParagraphBlock>(outer.Children[0]);
        Assert.Equal("line one lazy line two", p.Content.Text);
        var inner = Assert.IsType<QuoteBlock>(outer.Children[1]);
        Assert.Equal("nested quote", Assert.IsType<ParagraphBlock>(Assert.Single(inner.Children)).Content.Text);
    }

    [Fact]
    public void CommonMark_Emphasis_IntrawordUnderscore_RuleOf3_AndCjkFriendly()
    {
        var md = "foo_bar_baz and foo*bar*baz and ***both*** and **中文。**后续与前**（中文）**后";
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(MarkdownParser.Parse(md)));

        Assert.Contains("foo_bar_baz", p.Content.Text);
        Assert.Contains("foobarbaz", p.Content.Text);
        Assert.Contains("both", p.Content.Text);
        Assert.Contains("中文。后续", p.Content.Text);
        Assert.Contains("前（中文）后", p.Content.Text);

        Assert.Contains(p.Content.Spans, s =>
            s.Style == InlineStyle.Italic && p.Content.Text.Substring(s.Start, s.Length) == "bar");
        Assert.Contains(p.Content.Spans, s =>
            s.Style == (InlineStyle.Bold | InlineStyle.Italic) && p.Content.Text.Substring(s.Start, s.Length) == "both");
        Assert.Contains(p.Content.Spans, s =>
            s.Style == InlineStyle.Bold && p.Content.Text.Substring(s.Start, s.Length) == "中文。");
        Assert.Contains(p.Content.Spans, s =>
            s.Style == InlineStyle.Bold && p.Content.Text.Substring(s.Start, s.Length) == "（中文）");
    }

    [Fact]
    public void CommonMark_CodeSpans_BackslashEscapes_AndHtmlEntities()
    {
        var md = @"`` `code` `` and ` a ` and \*literal\* and &amp; &copy; &#35; &#x22; &#0;";
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(MarkdownParser.Parse(md)));

        Assert.Contains("`code`", p.Content.Text);
        Assert.Contains("*literal*", p.Content.Text);
        Assert.Contains("& \u00A9 # \" \uFFFD", p.Content.Text);
        Assert.Contains(p.Content.Spans, s =>
            s.Style == InlineStyle.Code && p.Content.Text.Substring(s.Start, s.Length) == "`code`");
        Assert.Contains(p.Content.Spans, s =>
            s.Style == InlineStyle.Code && p.Content.Text.Substring(s.Start, s.Length) == "a");
    }

    [Fact]
    public void CommonMark_Links_Refs_ImageInLink_Autolinks_AndNoNestedLinks()
    {
        var md = """
            [Full][Ref] and [Shortcut] and [![alt](img.png)](https://example.com) and <https://angle.test> and [https://spoof.test](https://real.test) and [visit https://inner.test](https://outer.test).

            [ref]: https://first.test "Title 1"
            [REF]: https://second.test "Title 2"
            [shortcut]: <https://short.test>
            """;

        var p = Assert.IsType<ParagraphBlock>(Assert.Single(MarkdownParser.Parse(md)));
        Assert.Equal(6, p.Content.Links.Length);
        Assert.Equal("https://first.test", p.Content.Links[0].Url);
        Assert.Equal("Title 1", p.Content.Links[0].Title);
        Assert.Equal("https://short.test", p.Content.Links[1].Url);
        Assert.Equal("https://example.com", p.Content.Links[2].Url);
        Assert.Equal("https://angle.test", p.Content.Links[3].Url);
        Assert.Equal("https://real.test", p.Content.Links[4].Url);
        Assert.Equal("https://outer.test", p.Content.Links[5].Url);

        var img = Assert.Single(p.Content.Objects);
        Assert.Equal(InlineObjectKind.Image, img.Kind);
        Assert.Equal("img.png", img.Value);
        Assert.Equal("alt", img.Alt);
    }

    // ---- 2. GFM & StreamMark 方言扩展 ----

    [Fact]
    public void Gfm_PipeTable_ProtectsEscapedCodeAndMathPipes()
    {
        var md = """
            | Expr | Notes |
            |:-----|------:|
            | `a|b` | $x \| y$ and a\|b |
            | only one |
            """;

        var table = Assert.IsType<TableBlock>(Assert.Single(MarkdownParser.Parse(md)));
        Assert.Equal([TableAlignment.Left, TableAlignment.Right], table.Columns);
        Assert.Equal(3, table.Rows.Length);
        Assert.Equal("a|b", table.Rows[1].Cells[0].Text);
        Assert.Contains("a|b", table.Rows[1].Cells[1].Text);
        Assert.Single(table.Rows[1].Cells[1].Objects);
        Assert.Equal("only one", table.Rows[2].Cells[0].Text);
        Assert.Equal(string.Empty, table.Rows[2].Cells[1].Text);
    }

    [Fact]
    public void Extensions_Emoji_SoftlineBreakAsHardline_AndDefinitionListFollowedByCode()
    {
        var profile = new MarkdownProfileBuilder().UseSoftlineBreakAsHardline().Build();
        var md = "Line 1 :rocket:\nLine 2 :not_an_emoji:\n\nTerm A\n: Def A1\n: Def A2\n\nTerm B\n: Def B\n\n    code block\n";
        var blocks = MarkdownParser.Parse(md, profile);
        Assert.Equal(3, blocks.Length);

        var p = Assert.IsType<ParagraphBlock>(blocks[0]);
        Assert.Equal("Line 1 \U0001F680\nLine 2 :not_an_emoji:", p.Content.Text);

        var dl = Assert.IsType<DefinitionListBlock>(blocks[1]);
        Assert.Equal(2, dl.Items.Length);
        Assert.Equal("Term A", Assert.Single(dl.Items[0].Terms).Text);
        Assert.Equal(2, dl.Items[0].Definitions.Length);
        Assert.Equal("Term B", Assert.Single(dl.Items[1].Terms).Text);

        var code = Assert.IsType<CodeBlock>(blocks[2]);
        Assert.False(code.IsFenced);
        Assert.Equal("code block", code.Code);
    }

    // ---- 3. 容器内增量缓存与 O(1) 引用复用验证（参考 @lobehub/streamdown） ----

    [Fact]
    public void Incremental_StreamingLargeTable_ReusesClosedRowReferences()
    {
        var composer = new DocumentComposer();
        var sb = new StringBuilder();
        sb.Append("| Col A | Col B |\n|-------|-------|\n");
        var snap = composer.AppendDelta(sb.ToString(), version: 1);

        TableRow? cachedRow1 = null;
        TableRow? cachedRow50 = null;

        for (var r = 1; r <= 100; r++)
        {
            var delta = $"| row {r} | **val {r}** |\n";
            sb.Append(delta);
            snap = composer.AppendDelta(delta, version: r + 1);

            var table = Assert.IsType<TableBlock>(Assert.Single(snap.Blocks));
            Assert.Equal(r + 1, table.Rows.Length);

            if (r == 2)
            {
                cachedRow1 = table.Rows[1];
            }
            else if (r > 2 && cachedRow1 is not null)
            {
                // 第 1 数据行在后续 98 行流式追加中必须复用完全相同的不可变引用
                Assert.Same(cachedRow1, table.Rows[1]);
            }

            if (r == 51)
            {
                cachedRow50 = table.Rows[50];
            }
            else if (r > 51 && cachedRow50 is not null)
            {
                Assert.Same(cachedRow50, table.Rows[50]);
            }
        }

        var finalSnap = composer.FinishDelta(ReadOnlySpan<char>.Empty, version: 200);
        var finalTable = Assert.IsType<TableBlock>(Assert.Single(finalSnap.Blocks));
        Assert.Equal(101, finalTable.Rows.Length);
        Assert.Same(cachedRow1, finalTable.Rows[1]);
        Assert.Same(cachedRow50, finalTable.Rows[50]);
    }

    [Fact]
    public void Incremental_StreamingLargeList_ReusesClosedItemReferences()
    {
        var composer = new DocumentComposer();
        ListItemBlock? cachedItem0 = null;
        ListItemBlock? cachedItem40 = null;

        for (var i = 0; i < 80; i++)
        {
            var delta = $"- item **{i}**\n";
            var snap = composer.AppendDelta(delta, version: i + 1);
            var list = Assert.IsType<ListBlock>(Assert.Single(snap.Blocks));
            Assert.Equal(i + 1, list.Items.Length);

            if (i == 1)
            {
                cachedItem0 = list.Items[0];
            }
            else if (i > 1 && cachedItem0 is not null)
            {
                // 当下一项已经到来时，前面已闭合的列表项必须 O(1) 引用复用
                Assert.Same(cachedItem0, list.Items[0]);
            }

            if (i == 41)
            {
                cachedItem40 = list.Items[40];
            }
            else if (i > 41 && cachedItem40 is not null)
            {
                Assert.Same(cachedItem40, list.Items[40]);
            }
        }

        var finalSnap = composer.FinishDelta(ReadOnlySpan<char>.Empty, version: 200);
        var finalList = Assert.IsType<ListBlock>(Assert.Single(finalSnap.Blocks));
        Assert.Equal(80, finalList.Items.Length);
        Assert.Same(cachedItem0, finalList.Items[0]);
        Assert.Same(cachedItem40, finalList.Items[40]);
    }

    [Fact]
    public void Incremental_FinishWithoutLateDefinitions_PreservesFrozenBlockReferences()
    {
        var composer = new DocumentComposer();
        var s1 = composer.AppendDelta("# Title\n\nFirst paragraph.\n\nSecond paragraph.", version: 1);
        Assert.True(s1.FrozenCount >= 2);
        var frozenTitle = s1[0];
        var frozenFirst = s1[1];

        var finalSnap = composer.FinishDelta(ReadOnlySpan<char>.Empty, version: 2);
        Assert.Equal(3, finalSnap.Count);
        Assert.Same(frozenTitle, finalSnap[0]);
        Assert.Same(frozenFirst, finalSnap[1]);
    }

    [Fact]
    public void Incremental_FinishWithLateReferenceDefinition_SelectivelyRebuildsAffectedFrozenBlockOnly()
    {
        var composer = new DocumentComposer();
        var s1 = composer.AppendDelta("# Unrelated Heading\n\nSee [the spec][spec].\n\nMiddle paragraph.\n\n", version: 1);
        Assert.True(s1.FrozenCount >= 2);
        var frozenHeading = s1[0];
        var frozenBeforeRef = Assert.IsType<ParagraphBlock>(s1[1]);
        Assert.Empty(frozenBeforeRef.Content.Links);

        composer.AppendDelta("[spec]: https://spec.commonmark.org\n", version: 2);
        var finalSnap = composer.FinishDelta(ReadOnlySpan<char>.Empty, version: 3);

        // 无关的已冻结标题块保持原对象引用；只有引用了 [spec] 的块被定向重物化并解析出链接
        Assert.Same(frozenHeading, finalSnap[0]);
        var resolvedPara = Assert.IsType<ParagraphBlock>(finalSnap[1]);
        Assert.NotSame(frozenBeforeRef, resolvedPara);
        var link = Assert.Single(resolvedPara.Content.Links);
        Assert.Equal("https://spec.commonmark.org", link.Url);
    }

    // ---- 4. 零依赖自定义块/行内语法扩展（IBlockSyntaxRule / IInlineSyntaxRule） ----

    [Fact]
    public async Task CustomSyntaxRules_BlockRaw_BlockContainer_AndInlineRules_WorkStaticAndStreaming()
    {
        var profile = new MarkdownProfileBuilder()
            .AddBlockRule(new CalloutRawRule())
            .AddBlockRule(new BoxContainerRule())
            .AddInlineRule(new HighlightInlineRule())
            .AddInlineRule(new MentionInlineRule())
            .Build();

        var md = """
            Hello @alice, this is ==very *important*==!

            @@@note
            raw **not parsed** line 1
            raw line 2
            @@@

            +++box
            Inner **bold** paragraph and @bob.
            +++
            """;

        // 1. 静态解析
        var staticBlocks = MarkdownParser.Parse(md, profile);
        AssertCustomBlocks(staticBlocks);

        // 2. 逐块流式解析（含 RawBody 开启时的 IsOpen=true → 闭合后的 IsOpen=false）
        var doc = new StreamingDocument(profile);
        var sawOpenCallout = false;
        for (var i = 0; i < md.Length; i += 5)
        {
            doc.Append(md.Substring(i, Math.Min(5, md.Length - i)));
            await doc.WhenIdle();
            if (doc.Latest.Blocks.OfType<ComponentBlock>().Any(b => b.Name == "callout" && b.IsOpen))
            {
                sawOpenCallout = true;
            }
        }
        doc.Complete();
        await doc.WhenIdle();

        Assert.True(sawOpenCallout);
        AssertCustomBlocks([.. doc.Latest.Blocks]);
    }

    private static void AssertCustomBlocks(IReadOnlyList<MarkdownBlock> blocks)
    {
        Assert.Equal(3, blocks.Count);

        var p = Assert.IsType<ParagraphBlock>(blocks[0]);
        Assert.Equal("Hello @alice, this is very important!", p.Content.Text);
        var mention = Assert.Single(p.Content.Links);
        Assert.Equal("user://alice", mention.Url);
        Assert.Contains(p.Content.Spans, s =>
            s.Style.HasFlag(InlineStyle.Mark) && p.Content.Text.Substring(s.Start, s.Length).Contains("very", StringComparison.Ordinal));
        Assert.Contains(p.Content.Spans, s =>
            s.Style.HasFlag(InlineStyle.Mark | InlineStyle.Italic) && p.Content.Text.Substring(s.Start, s.Length) == "important");

        var callout = Assert.IsType<ComponentBlock>(blocks[1]);
        Assert.Equal("callout", callout.Name);
        Assert.Equal("note", callout.Attributes["kind"]);
        Assert.False(callout.IsOpen);
        Assert.Equal("raw **not parsed** line 1\nraw line 2", callout.Raw);

        var box = Assert.IsType<ComponentBlock>(blocks[2]);
        Assert.Equal("box", box.Name);
        Assert.False(box.IsOpen);
        var innerPara = Assert.IsType<ParagraphBlock>(Assert.Single(box.Children));
        Assert.Contains("bold", innerPara.Content.Text);
        Assert.Equal("user://bob", Assert.Single(innerPara.Content.Links).Url);
    }

    private sealed class CalloutRawRule : IBlockSyntaxRule
    {
        public string OpeningCharacters => "@";

        public bool TryOpen(ReadOnlySpan<char> line, out BlockRuleState state)
        {
            if (line.StartsWith("@@@", StringComparison.Ordinal))
            {
                var kind = line[3..].Trim().ToString();
                state = new BlockRuleState
                {
                    Kind = BlockRuleKind.RawBody,
                    Name = "callout",
                    Attributes = string.IsNullOrEmpty(kind)
                        ? ComponentAttributes.Empty
                        : new ComponentAttributes([new KeyValuePair<string, string>("kind", kind)]),
                    ConsumedLength = line.Length,
                };
                return true;
            }
            state = default;
            return false;
        }

        public BlockContinueResult TryContinue(ReadOnlySpan<char> line, ref BlockRuleState state)
        {
            if (line.Trim().SequenceEqual("@@@"))
            {
                state.SawClose = true;
                return BlockContinueResult.BreakDiscard;
            }
            return BlockContinueResult.Continue;
        }

        public MarkdownBlock Build(in BlockRuleState state, ImmutableArray<MarkdownBlock> children, string? rawContent, bool isOpen, SourceRange range) =>
            new ComponentBlock(state.Name, state.Attributes, []) { Raw = rawContent ?? string.Empty, IsOpen = isOpen, Range = range };
    }

    private sealed class BoxContainerRule : IBlockSyntaxRule
    {
        public string OpeningCharacters => "+";

        public bool TryOpen(ReadOnlySpan<char> line, out BlockRuleState state)
        {
            if (line.StartsWith("+++", StringComparison.Ordinal))
            {
                var name = line[3..].Trim();
                state = new BlockRuleState
                {
                    Kind = BlockRuleKind.Container,
                    Name = name.IsEmpty ? "box" : name.ToString(),
                    Attributes = ComponentAttributes.Empty,
                    ConsumedLength = line.Length,
                };
                return true;
            }
            state = default;
            return false;
        }

        public BlockContinueResult TryContinue(ReadOnlySpan<char> line, ref BlockRuleState state)
        {
            if (line.Trim().SequenceEqual("+++"))
            {
                state.SawClose = true;
                return BlockContinueResult.BreakDiscard;
            }
            return BlockContinueResult.Continue;
        }

        public MarkdownBlock Build(in BlockRuleState state, ImmutableArray<MarkdownBlock> children, string? rawContent, bool isOpen, SourceRange range) =>
            new ComponentBlock(state.Name, state.Attributes, children) { IsOpen = isOpen, Range = range };
    }

    private sealed class HighlightInlineRule : IInlineSyntaxRule
    {
        public string OpeningCharacters => "=";

        public bool TryMatch(ReadOnlySpan<char> text, InlineSyntaxContext context, out int consumed)
        {
            consumed = 0;
            if (text.Length < 5 || text[0] != '=' || text[1] != '=')
            {
                return false;
            }
            var close = text[2..].IndexOf("==", StringComparison.Ordinal);
            if (close <= 0)
            {
                return false;
            }
            context.ParseInlines(text.Slice(2, close), InlineStyle.Mark);
            consumed = close + 4;
            return true;
        }
    }

    private sealed class MentionInlineRule : IInlineSyntaxRule
    {
        public string OpeningCharacters => "@";

        public bool TryMatch(ReadOnlySpan<char> text, InlineSyntaxContext context, out int consumed)
        {
            consumed = 0;
            if (text.Length < 2 || text[0] != '@')
            {
                return false;
            }
            var end = 1;
            while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] == '_'))
            {
                end++;
            }
            if (end <= 1)
            {
                return false;
            }
            var handle = text.Slice(1, end - 1).ToString();
            context.AppendLink(text[..end], $"user://{handle}");
            consumed = end;
            return true;
        }
    }

    // ---- 4. Round-2 300 TPS 优化专项回归 ----

    [Fact]
    public void BareAutolink_ColonLookback_PreservesAllSchemesAndBoundaries()
    {
        var md = """
            See https://alpha.test and (http://beta.test/path?q=1) and ftp://files.test and mailto:hi@example.com and www.gamma.test!
            中文https://cjk.test。还有 HTTPS://UPPER.TEST 与 [https://spoof.test](https://real.test) 与 h*t*tps://broken.test 与 http:// 与 :rocket: https://emoji.test :smile:
            """;

        var blocks = MarkdownParser.Parse(md);
        var p = Assert.IsType<ParagraphBlock>(Assert.Single(blocks));
        var urls = p.Content.Links.Select(l => l.Url).ToList();
        Assert.Equal(
            [
                "https://alpha.test",
                "http://beta.test/path?q=1",
                "ftp://files.test",
                "mailto:hi@example.com",
                "http://www.gamma.test",
                "https://cjk.test",
                "HTTPS://UPPER.TEST",
                "https://real.test",
                "https://emoji.test",
            ],
            urls);
        Assert.Contains("🚀", p.Content.Text);
        Assert.Contains("😄", p.Content.Text);
        Assert.Contains("http://", p.Content.Text);
    }

    [Fact]
    public void Finish_WithoutLateReferenceUpdate_SharesFrozenGeneration_AndCachesFootnotesBlock()
    {
        var composer = new DocumentComposer();
        var s1 = composer.AppendDelta("Intro[^1]\n\n[^1]: Note with [later]\n\nPara A\n\nPara B\n".AsSpan(), 1);
        Assert.True(composer.FrozenEnd > "Intro[^1]\n\n[^1]: Note with [later]\n\n".Length);
        var fn1 = Assert.IsType<FootnotesBlock>(s1[^1]);

        var s2 = composer.AppendDelta("\nPara C\n".AsSpan(), 2);
        var fn2 = Assert.IsType<FootnotesBlock>(s2[^1]);
        // 脚注未修改时复用同一 FootnotesBlock 实例
        Assert.Same(fn1, fn2);

        // 无迟到引用命中已冻结块时，FinishDelta 保持 Generation 不变（SharesFrozenWith == true）
        var noLateRef = new DocumentComposer();
        var pre = noLateRef.AppendDelta("First block\n\nSecond block\n\nTail ".AsSpan(), 1);
        var done = noLateRef.FinishDelta("finished".AsSpan(), 2);
        Assert.Equal(pre.Generation, done.Generation);
        Assert.True(pre.SharesFrozenWith(done));
        Assert.DoesNotContain(SnapshotDiff.Compute(pre, done), op => op.Index < pre.FrozenCount);

        // 脚注体内的前向链接在迟到定义到达后也能正确解析
        var s3 = composer.AppendDelta("\n[later]: https://later.test\n\nTail\n".AsSpan(), 3);
        var final = composer.FinishDelta(ReadOnlySpan<char>.Empty, 4);
        var finalFn = Assert.IsType<FootnotesBlock>(final[^1]);
        var fnPara = Assert.IsType<ParagraphBlock>(Assert.Single(finalFn.Footnotes[0].Children));
        Assert.Equal("https://later.test", Assert.Single(fnPara.Content.Links).Url);
    }
}
