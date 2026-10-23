namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiAnswerMarkdown"/>: the block kinds of the answer subset, inline markup and citations, the no-break space
/// before a pill, fence info strings, plain text, and the streaming rules (whole lines for headings, rows and code; open
/// markup at the end of a stream).
/// </summary>
public class AiAnswerMarkdownTests
{
    private static string Text(AiAnswerInline[] inlines) => string.Concat(inlines.Select(i => i.Kind switch
    {
        AiAnswerInlineKind.Citation => "{" + i.Number + "}",
        AiAnswerInlineKind.Code => "`" + i.Text + "`",
        _ => (i.Style & AiAnswerInlineStyle.Bold) != 0 ? "<b>" + i.Text + "</b>" : (i.Style & AiAnswerInlineStyle.Italic) != 0 ? "<i>" + i.Text + "</i>" : i.Text
    })).Replace(AiAnswerMarkdown.NoBreakSpace, '~');

    [Fact]
    public void Sample_Answer_Has_Every_Block_Kind_In_Order()
    {
        var blocks = AiAnswerMarkdown.Parse(AiAnswerSamples.Answer);
        blocks.Select(b => b.Kind).ShouldBe(
        [
            AiAnswerBlockKind.Paragraph, AiAnswerBlockKind.Heading, AiAnswerBlockKind.List, AiAnswerBlockKind.Heading,
            AiAnswerBlockKind.Paragraph, AiAnswerBlockKind.Table, AiAnswerBlockKind.Heading, AiAnswerBlockKind.Paragraph,
            AiAnswerBlockKind.Code, AiAnswerBlockKind.Paragraph
        ]);
        blocks[1].Level.ShouldBe(3);
        Text(blocks[1].Texts[0]).ShouldBe("Move it in three phases");
        blocks[2].Ordered.ShouldBeTrue();
        blocks[2].Texts.Count.ShouldBe(3);
        Text(blocks[2].Texts[0]).ShouldBe("<b>Mirror.</b> Copy each job into the new system and let it run on every push without blocking anything.~{1}");
        Text(blocks[2].Texts[1]).ShouldEndWith("disagrees.~{4}~{6}");
        blocks[5].HasHeader.ShouldBeTrue();
        blocks[5].Rows.ShouldBe([3, 3, 3, 3, 3]);
        Text(blocks[5].Texts[0]).ShouldBe(string.Empty);
        Text(blocks[5].Texts[4]).ShouldBe("Minutes");
        blocks[8].Language.ShouldBe("yaml");
        blocks[8].FileName.ShouldBe("ci/required-checks.yml");
        blocks[8].Code.ShouldStartWith("# Checks that must pass before a merge.\nrequired:\n  - build");
        blocks[8].IsClosed.ShouldBeTrue();
        Text(blocks[7].Texts[0]).ShouldContain("once in `ci/required-checks.yml` and compare");
    }

    [Fact]
    public void Paragraph_Lines_Join_With_Spaces_And_Two_Trailing_Spaces_Break_The_Line()
    {
        var blocks = AiAnswerMarkdown.Parse("one\ntwo  \nthree\n\nnext");
        blocks.Count.ShouldBe(2);
        Text(blocks[0].Texts[0]).ShouldBe("one two\nthree");
        Text(blocks[1].Texts[0]).ShouldBe("next");
    }

    [Theory]
    [InlineData("Use **bold** here", "Use <b>bold</b> here")]
    [InlineData("Use __bold__ here", "Use <b>bold</b> here")]
    [InlineData("an *aside* here", "an <i>aside</i> here")]
    [InlineData("snake_case_name stays", "snake_case_name stays")]
    [InlineData("2 * 3 * 4", "2 * 3 * 4")]
    [InlineData("**open never closes", "**open never closes")]
    [InlineData("Run `npm ci` first", "Run `npm ci` first")]
    [InlineData("``a ` b``", "`a ` b`")]
    [InlineData("an `open code", "an `open code")]
    [InlineData("see [the docs](https://example.com/x) now", "see the docs now")]
    [InlineData("an ![image](a.png) alt", "an image alt")]
    [InlineData("a &amp; b &lt;c&gt;", "a & b <c>")]
    [InlineData(@"not \*emphasis\*", "not *emphasis*")]
    public void Inline_Markup(string markdown, string expected) =>
        Text(AiAnswerMarkdown.ParseInlines(markdown)).ShouldBe(expected);

    [Theory]
    [InlineData("text. [1]", "text.~{1}")]
    [InlineData("text.[1]", "text.~{1}")]
    [InlineData("a [1] [2] b", "a~{1}~{2} b")]
    [InlineData("a [1][2]", "a~{1}{2}")]
    [InlineData("[1] first", "{1} first")]
    [InlineData("`code` [3]", "`code`~{3}")]
    [InlineData(@"escaped \[1] stays", "escaped [1] stays")]
    [InlineData("link [1](https://x.dev)", "link 1")]
    [InlineData("ref [docs][1] label", "ref docs label")]
    [InlineData("zero [01] and four digits [1234]", "zero [01] and four digits [1234]")]
    [InlineData("in code `[1]` no", "in code `[1]` no")]
    public void Citations_Get_A_No_Break_Space_Before_Them(string markdown, string expected) =>
        Text(AiAnswerMarkdown.ParseInlines(markdown)).ShouldBe(expected);

    [Fact]
    public void A_Link_Definition_At_The_Start_Of_A_Line_Is_Not_A_Citation()
    {
        Text(AiAnswerMarkdown.ParseInlines("[1]: https://example.com")).ShouldBe("[1]: https://example.com");
    }

    [Fact]
    public void Bullet_Lists_Continuations_And_Loose_Items()
    {
        var blocks = AiAnswerMarkdown.Parse("- one\n  still one\n- two\n\n- three\n\nAfter.");
        blocks.Count.ShouldBe(2);
        blocks[0].Kind.ShouldBe(AiAnswerBlockKind.List);
        blocks[0].Ordered.ShouldBeFalse();
        blocks[0].Texts.Select(Text).ShouldBe(["one still one", "two", "three"]);
        blocks[1].Kind.ShouldBe(AiAnswerBlockKind.Paragraph);
    }

    [Fact]
    public void An_Ordered_List_Keeps_Its_Start_And_A_Bullet_Starts_A_New_List()
    {
        var blocks = AiAnswerMarkdown.Parse("3. three\n4. four\n- bullet");
        blocks.Count.ShouldBe(2);
        blocks[0].ListStart.ShouldBe(3);
        blocks[0].Texts.Count.ShouldBe(2);
        blocks[1].Ordered.ShouldBeFalse();
    }

    [Fact]
    public void Thematic_Breaks_Are_Dropped_And_Headings_Lose_Closing_Hashes()
    {
        var blocks = AiAnswerMarkdown.Parse("## Title ##\n\n---\n\nText");
        blocks.Select(b => b.Kind).ShouldBe([AiAnswerBlockKind.Heading, AiAnswerBlockKind.Paragraph]);
        Text(blocks[0].Texts[0]).ShouldBe("Title");
        blocks[0].Level.ShouldBe(2);
    }

    [Fact]
    public void Table_Cells_Split_At_Pipes_Outside_Code_And_Short_Rows_Are_Padded()
    {
        AiAnswerMarkdown.SplitRow("| a | `x|y` | b \\| c |").ShouldBe(["a", "`x|y`", "b | c"]);
        var blocks = AiAnswerMarkdown.Parse("| A | B |\n|:--|--:|\n| one |\n| 1 | 2 | 3 |");
        blocks[0].Rows.ShouldBe([2, 2, 2]);
        Text(blocks[0].Texts[3]).ShouldBe(string.Empty);
        Text(blocks[0].Texts[5]).ShouldBe("2");
    }

    [Fact]
    public void A_Table_Without_A_Delimiter_Row_Has_No_Header()
    {
        AiAnswerMarkdown.Parse("| a | b |\n| c | d |")[0].HasHeader.ShouldBeFalse();
    }

    [Theory]
    [InlineData("yaml ci/checks.yml", "yaml", "ci/checks.yml")]
    [InlineData("yaml title=\"ci/checks.yml\"", "yaml", "ci/checks.yml")]
    [InlineData("checks.yml", "yml", "checks.yml")]
    [InlineData("ts", "ts", "")]
    [InlineData("", "", "")]
    public void Fence_Info_Gives_Language_And_File(string info, string language, string file)
    {
        var (lang, name) = AiAnswerMarkdown.SplitInfo(info);
        lang.ShouldBe(language);
        name.ShouldBe(file);
    }

    [Fact]
    public void An_Unclosed_Fence_Runs_To_The_End()
    {
        var blocks = AiAnswerMarkdown.Parse("```sh\necho one\n", final: false);
        blocks.Single().Code.ShouldBe("echo one");
        blocks.Single().IsClosed.ShouldBeFalse();
        AiAnswerMarkdown.Parse("```sh\necho one\n").Single().IsClosed.ShouldBeTrue();
    }

    [Fact]
    public void Streaming_Holds_Back_A_Heading_Until_Its_Line_Ends()
    {
        AiAnswerMarkdown.Parse("Intro.\n\n### Move it in", final: false).Select(b => b.Kind).ShouldBe([AiAnswerBlockKind.Paragraph]);
        var done = AiAnswerMarkdown.Parse("Intro.\n\n### Move it in three phases\n", final: false);
        done.Select(b => b.Kind).ShouldBe([AiAnswerBlockKind.Paragraph, AiAnswerBlockKind.Heading]);
        // The end of the stream shows a last line that never got its break.
        AiAnswerMarkdown.Parse("Intro.\n\n### Move it in", final: true).Count.ShouldBe(2);
    }

    [Fact]
    public void Streaming_Shows_Table_Rows_And_Code_Lines_Whole()
    {
        var table = AiAnswerMarkdown.Parse("| | Hosted |\n|---|---|\n| Setup | Min", final: false).Single();
        table.Rows.Count.ShouldBe(1);
        table.HasHeader.ShouldBeTrue();
        // The header shows before its delimiter row arrives.
        AiAnswerMarkdown.Parse("| | Hosted |\n", final: false).Single().HasHeader.ShouldBeTrue();
        var code = AiAnswerMarkdown.Parse("```yaml\nrequired:\n  - bu", final: false).Single();
        code.Code.ShouldBe("required:");
        AiAnswerMarkdown.Parse("Text.\n\n```yam", final: false).Count.ShouldBe(1);
    }

    [Fact]
    public void Streaming_Shows_Paragraph_And_List_Words_As_They_Come()
    {
        Text(AiAnswerMarkdown.Parse("Keep both pipelines", final: false).Single().Texts[0]).ShouldBe("Keep both pipelines");
        var list = AiAnswerMarkdown.Parse("1. **Mirror.** Copy each", final: false).Single();
        Text(list.Texts[0]).ShouldBe("<b>Mirror.</b> Copy each");
    }

    [Fact]
    public void Streaming_Takes_Open_Markup_As_Open()
    {
        Text(AiAnswerMarkdown.Parse("1. **Retire passwords", final: false).Single().Texts[0]).ShouldBe("<b>Retire passwords</b>");
        Text(AiAnswerMarkdown.Parse("with `mediation:", final: false).Single().Texts[0]).ShouldBe("with `mediation:`");
        // Only the end of the stream is open: an earlier paragraph shows its markers.
        Text(AiAnswerMarkdown.Parse("**never\n\nnext", final: false)[0].Texts[0]).ShouldBe("**never");
        Text(AiAnswerMarkdown.Parse("1. **Retire passwords", final: true).Single().Texts[0]).ShouldBe("**Retire passwords");
    }

    [Fact]
    public void Every_Prefix_Of_The_Sample_Parses_And_Settles_On_The_Whole()
    {
        var whole = AiAnswerMarkdown.Parse(AiAnswerSamples.Answer);
        for (var i = 0; i <= AiAnswerSamples.Answer.Length; i++)
        {
            Should.NotThrow(() => AiAnswerMarkdown.Parse(AiAnswerSamples.Answer[..i], final: false));
        }
        var streamed = AiAnswerMarkdown.Parse(AiAnswerSamples.Answer, final: false);
        streamed.Count.ShouldBe(whole.Count);
        for (var i = 0; i < whole.Count; i++)
        {
            streamed[i].SameAs(whole[i]).ShouldBeTrue($"block {i}");
        }
    }

    [Fact]
    public void Plain_Text_Writes_Citations_As_Numbers()
    {
        AiAnswerMarkdown.ToPlainText(AiAnswerMarkdown.ParseInlines("**Mirror.** Copy `x y`. [1] [2]")).ShouldBe("Mirror. Copy x y. [1] [2]");
        var plain = AiAnswerDocument.BuildPlainText(AiAnswerMarkdown.Parse(AiAnswerSamples.Answer));
        plain.ShouldStartWith("Keep both pipelines running");
        plain.ShouldContain("\n\nMove it in three phases\n\n1. Mirror. Copy each job");
        plain.ShouldContain("2. Compare.");
        plain.ShouldContain(" | Hosted runners | Self-hosted pool\nSetup | Minutes | Days");
        plain.ShouldContain("required:\n  - build");
        plain.ShouldEndWith("look at caching first. [2] [8]");
    }
}
