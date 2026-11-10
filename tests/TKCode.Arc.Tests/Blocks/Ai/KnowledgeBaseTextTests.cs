using System.Globalization;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The pure text work of <see cref="KnowledgeBase"/>: chunking, terms, matching, the BM25 search, previews and sizes.</summary>
public class KnowledgeBaseTextTests
{
    [Fact]
    public void Split_Keeps_Paragraphs_Together_Up_To_The_Target()
    {
        var text = "First paragraph is short.\n\nSecond one is short too.\n\n" + new string('x', 30) + " end.";
        KnowledgeBaseText.Split(text, 80).ShouldBe(["First paragraph is short.\n\nSecond one is short too.", new string('x', 30) + " end."]);
        KnowledgeBaseText.Split("   ").ShouldBeEmpty();
        KnowledgeBaseText.Split(null).ShouldBeEmpty();
    }

    [Fact]
    public void Split_Cuts_Long_Paragraphs_At_Sentences_And_Spaces()
    {
        var sentence = "This sentence has exactly some words in it. ";
        var paragraph = string.Concat(Enumerable.Repeat(sentence, 10)).Trim();
        var chunks = KnowledgeBaseText.Split(paragraph, 100);
        chunks.Count.ShouldBeGreaterThan(3);
        chunks.ShouldAllBe(c => c.Length <= 100);
        chunks.ShouldAllBe(c => c.EndsWith('.'));
        var word = string.Join(' ', Enumerable.Repeat("word", 60));
        KnowledgeBaseText.Split(word, 50).ShouldAllBe(c => c.Length <= 50 && !c.StartsWith(' '));
    }

    [Fact]
    public void Chunk_Numbers_The_Pieces()
    {
        var chunks = KnowledgeBase.ChunkText("doc", "A.\n\nB.");
        chunks.Single().ShouldBe(new KnowledgeChunk("doc", 0, "A.\n\nB."));
    }

    [Fact]
    public void Terms_Drop_Common_And_Short_Words_And_Split_Cjk()
    {
        KnowledgeBaseText.Terms("How do I get a REFUND for the bike?").ShouldBe(["get", "refund", "bike"]);
        KnowledgeBaseText.Terms("退货 policy").ShouldBe(["退", "货", "policy"]);
        KnowledgeBaseText.Terms("refund refund").ShouldBe(["refund"]);
        KnowledgeBaseText.Terms("  ").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("refunds", "refund", true)]
    [InlineData("refunded", "refund", true)]
    [InlineData("refund", "refunds", true)]
    [InlineData("class", "class", true)]
    [InlineData("bike", "bikes", true)]
    [InlineData("go", "goes", false)]
    [InlineData("at", "a", false)]
    [InlineData("return", "refund", false)]
    public void Words_Match_Terms_By_Prefix_And_Plural(string word, string term, bool expected)
    {
        KnowledgeBaseText.Matches(word, term).ShouldBe(expected);
    }

    [Fact]
    public void Search_Ranks_Ready_Chunks_And_Ignores_The_Rest()
    {
        var documents = KnowledgeBaseSamples.Documents();
        var passages = KnowledgeBaseText.Search(documents, "battery warranty capacity", 3);
        passages.Count.ShouldBe(3);
        passages[0].Document.Id.ShouldBe("warranty");
        passages[0].Text.ShouldContain("70 percent");
        // Battery care is still queued: its chunks are not searched.
        passages.ShouldAllBe(p => p.Document.Id != "battery");
        passages.Select(p => p.Score).ShouldBeInOrder(SortDirection.Descending);
        KnowledgeBaseText.Search(documents, "the and of", 3).ShouldBeEmpty();
        KnowledgeBaseText.Search(documents, "battery", 0).ShouldBeEmpty();
    }

    [Fact]
    public void Preview_Starts_Near_The_First_Match_And_Marks_Every_Matching_Word()
    {
        var text = string.Join(' ', Enumerable.Repeat("filler", 80)) + " the refund arrives. Refunds are quick. " +
                   string.Join(' ', Enumerable.Repeat("tail", 40));
        var (preview, ranges) = KnowledgeBaseText.Preview(text, ["refund"], 120);
        preview.ShouldStartWith("… ");
        preview.ShouldEndWith(" …");
        preview.Length.ShouldBeLessThanOrEqualTo(124);
        ranges.Count.ShouldBe(2);
        preview.Substring(ranges[0].Start, ranges[0].Length).ShouldBe("refund");
        preview.Substring(ranges[1].Start, ranges[1].Length).ShouldBe("Refunds");

        var (start, none) = KnowledgeBaseText.Preview("Short text.", ["zebra"], 120);
        start.ShouldBe("Short text.");
        none.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(48_000, "47 KB")]
    [InlineData(2_411_724, "2.3 MB")]
    [InlineData(36_000_000, "34 MB")]
    public void Sizes_Read_Like_People_Write_Them(long bytes, string expected)
    {
        KnowledgeBaseText.FormatSize(bytes, CultureInfo.GetCultureInfo("en-US")).ShouldBe(expected);
    }

    [Theory]
    [InlineData("a.PDF", KnowledgeDocumentKind.Pdf)]
    [InlineData("notes.md", KnowledgeDocumentKind.Markdown)]
    [InlineData("sheet.xlsx", KnowledgeDocumentKind.Spreadsheet)]
    [InlineData("data.json", KnowledgeDocumentKind.Code)]
    [InlineData("page.html", KnowledgeDocumentKind.Web)]
    [InlineData("letter.docx", KnowledgeDocumentKind.Document)]
    [InlineData("README", KnowledgeDocumentKind.Text)]
    public void Kinds_Come_From_Extensions(string name, KnowledgeDocumentKind kind)
    {
        KnowledgeBase.KindOf(name).ShouldBe(kind);
    }
}
