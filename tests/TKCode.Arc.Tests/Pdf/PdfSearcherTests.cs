using Avalonia;
using Shouldly;

namespace TKCode.Arc.Tests.Pdf;

public class PdfSearcherTests
{
    private static (int Start, int Length)[] Find(string text, string query, bool matchCase = false, bool wholeWord = false)
    {
        var normalized = PdfTextNormalizer.Normalize(text, matchCase);
        return PdfSearcher.FindOriginal(normalized, PdfSearcher.PrepareQuery(query, matchCase), wholeWord).ToArray();
    }

    private static string[] FindTexts(string text, string query, bool matchCase = false, bool wholeWord = false) =>
        Find(text, query, matchCase, wholeWord).Select(hit => text.Substring(hit.Start, hit.Length)).ToArray();

    #region Case and whole words

    [Fact]
    public void Ignores_Case_Unless_Asked_Not_To()
    {
        FindTexts("Apple apple APPLE", "apple").ShouldBe(["Apple", "apple", "APPLE"]);
        FindTexts("Apple apple APPLE", "APPLE").ShouldBe(["Apple", "apple", "APPLE"]);
        FindTexts("Apple apple APPLE", "apple", matchCase: true).ShouldBe(["apple"]);
    }

    [Fact]
    public void Case_Folding_Unites_The_Forms_Of_A_Letter()
    {
        // Sigma has a final form, and the sharp s has no one-letter capital.
        FindTexts("\u03A3\u0391\u03A3 \u03C3\u03B1\u03C2", "\u03C3\u03B1\u03C3").ShouldBe(["\u03A3\u0391\u03A3", "\u03C3\u03B1\u03C2"]);
        FindTexts("Stra\u00DFe", "STRASSE").ShouldBe(["Stra\u00DFe"]);
        FindTexts("STRASSE", "stra\u00DFe").ShouldBe(["STRASSE"]);
    }

    [Fact]
    public void Whole_Word_Needs_A_Boundary_At_Both_Ends()
    {
        FindTexts("cat concat cats cat.", "cat", wholeWord: true).Length.ShouldBe(2);
        Find("cat concat cats cat.", "cat", wholeWord: true).Select(hit => hit.Start).ShouldBe([0, 16]);
        FindTexts("a_cat cat2 2cat", "cat", wholeWord: true).ShouldBe(["cat"]);
    }

    [Fact]
    public void Whole_Word_Does_Not_Apply_To_Cjk()
    {
        FindTexts("\u6211\u662F\u4E2D\u6587\u5B57\u7B26", "\u4E2D\u6587", wholeWord: true).ShouldBe(["\u4E2D\u6587"]);
        // Next to Chinese a Latin word still stands on its own.
        FindTexts("\u4E2Dabc\u6587", "abc", wholeWord: true).ShouldBe(["abc"]);
        FindTexts("\u30AB\u30BF\u30AB\u30CA", "\u30AB\u30CA", wholeWord: true).ShouldBe(["\u30AB\u30CA"]);
    }

    [Fact]
    public void Whole_Word_Treats_A_Supplementary_Letter_As_A_Letter()
    {
        // U+10400 DESERET CAPITAL LONG I folds to U+10428; on either side of "cat" it joins the word.
        FindTexts("\U00010400cat cat", "cat", wholeWord: true).ShouldBe(["cat"]);
        Find("\U00010400cat cat", "cat", wholeWord: true)[0].Start.ShouldBe(6);
    }

    #endregion

    #region Normalization and the map back

    [Fact]
    public void Ligature_Maps_Back_To_The_Ligature()
    {
        // "office" with one character for "ffi": NFKC makes the text 2 longer than the original.
        const string text = "the o\uFB03ce and more office";
        var hits = Find(text, "office");
        hits.Length.ShouldBe(2);
        hits[0].ShouldBe((4, 4));
        text.Substring(hits[0].Start, hits[0].Length).ShouldBe("o\uFB03ce");
        // After the ligature the map has not drifted.
        hits[1].ShouldBe((text.LastIndexOf("office", StringComparison.Ordinal), 6));
    }

    [Fact]
    public void Part_Of_A_Ligature_Maps_To_The_Whole_Ligature()
    {
        Find("o\uFB03ce", "f").ShouldBe([(1, 1)]);
        Find("o\uFB03ce", "ffi").ShouldBe([(1, 1)]);
        Find("\uFB01n", "fin").ShouldBe([(0, 2)]);
    }

    [Fact]
    public void Query_Is_Normalized_Like_The_Text()
    {
        Find("office", "o\uFB03ce").ShouldBe([(0, 6)]);
        Find("\uFF21\uFF22\uFF23", "abc").ShouldBe([(0, 3)]);
        Find("\u2460", "1").ShouldBe([(0, 1)]);
    }

    [Fact]
    public void Surrogate_Pairs_Stay_Whole_In_The_Map()
    {
        // U+20BB7 has two UTF-16 units; the characters after it must keep their true indexes.
        const string text = "x\U00020BB7y \U00020BB7z";
        Find(text, "\U00020BB7").ShouldBe([(1, 2), (5, 2)]);
        Find(text, "y").ShouldBe([(3, 1)]);
        Find(text, "z").ShouldBe([(7, 1)]);
    }

    [Fact]
    public void Compatibility_Surrogate_Shrinks_And_Maps_To_Both_Units()
    {
        // U+1D400 MATHEMATICAL BOLD CAPITAL A: two units become one letter a.
        const string text = "\U0001D400\U0001D401 b";
        Find(text, "a").ShouldBe([(0, 2)]);
        Find(text, "ab").ShouldBe([(0, 4)]);
        Find(text, "b").ShouldBe([(2, 2), (5, 1)]);
    }

    [Fact]
    public void Composed_And_Decomposed_Forms_Match_And_Map_To_What_Is_There()
    {
        Find("cafe\u0301 x", "caf\u00E9").ShouldBe([(0, 5)]);
        Find("caf\u00E9 x", "cafe\u0301").ShouldBe([(0, 4)]);
        Find("cafe\u0301 x", "x").ShouldBe([(6, 1)]);
        // Conjoining Hangul.
        Find("\u1112\u1161\u11AB", "\uD55C").ShouldBe([(0, 3)]);
    }

    [Fact]
    public void Broken_Text_Does_Not_Throw()
    {
        // string.Normalize throws on a lone surrogate.
        var text = "a\uD800b \uDC00c";
        Find(text, "b").ShouldBe([(2, 1)]);
        Find(text, "c").ShouldBe([(5, 1)]);
    }

    [Fact]
    public void White_Space_Is_Compressed_But_Mapped()
    {
        const string text = "red   \t\n apple";
        Find(text, "red apple").ShouldBe([(0, text.Length)]);
        Find(text, "red  \n apple").ShouldBe([(0, text.Length)]);
        Find(text, "apple").ShouldBe([(9, 5)]);
        Find("no\u00A0break", "no break").ShouldBe([(0, 8)]);
    }

    [Fact]
    public void A_Query_Of_Nothing_Finds_Nothing()
    {
        PdfSearcher.PrepareQuery("  \n ", false).ShouldBe(string.Empty);
        PdfSearcher.PrepareQuery("\u00AD", false).ShouldBe(string.Empty);
        Find("abc", "   ").ShouldBeEmpty();
        Find("abc", "").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("co-\noperate")]
    [InlineData("co-\r\noperate")]
    [InlineData("co- \n operate")]
    [InlineData("co\u00AD\noperate")]
    [InlineData("co\u0002\r\noperate")]
    [InlineData("co\u0002operate")]
    [InlineData("co\u2010\noperate")]
    public void Hyphen_At_A_Line_End_Joins_The_Word(string text)
    {
        var hits = Find(text, "cooperate");
        hits.Length.ShouldBe(1);
        hits[0].Start.ShouldBe(0);
        hits[0].Length.ShouldBe(text.Length);
    }

    [Fact]
    public void A_Hyphen_Inside_A_Line_Stays()
    {
        Find("co-operate", "cooperate").ShouldBeEmpty();
        Find("co-operate", "co-operate").ShouldBe([(0, 10)]);
        // Nothing to join: a dash before a line, or a hyphen before something else than a letter.
        Find("end -\nnext", "endnext").ShouldBeEmpty();
        Find("well-\n- done", "well").ShouldBe([(0, 4)]);
    }

    [Fact]
    public void Soft_Hyphen_And_Zero_Width_Characters_Do_Not_Break_A_Word()
    {
        Find("co\u00ADoperate", "cooperate").ShouldBe([(0, 10)]);
        Find("wo\u200Brd", "word").ShouldBe([(0, 5)]);
    }

    [Fact]
    public void Line_Break_Inside_Chinese_Is_Not_A_Space()
    {
        const string text = "\u4E2D\u6587\n\u5B57\u7B26";
        Find(text, "\u6587\u5B57").ShouldBe([(1, 3)]);
        // Between Latin words it is.
        Find("red\nblue", "red blue").ShouldBe([(0, 8)]);
        Find("red\nblue", "redblue").ShouldBeEmpty();
    }

    [Fact]
    public void The_Map_Is_Monotonic_For_Mixed_Text()
    {
        var normalized = PdfTextNormalizer.Normalize("a\uFB03 \U0001D400\n\u4E2D-\n\uFF21 \U00020BB7\u00DF", matchCase: false);
        for (var i = 0; i < normalized.Text.Length; i++)
        {
            normalized.Ends[i].ShouldBeGreaterThan(normalized.Starts[i]);
            if (i > 0)
            {
                normalized.Starts[i].ShouldBeGreaterThanOrEqualTo(normalized.Starts[i - 1]);
            }
        }
    }

    #endregion

    #region Pages

    [Fact]
    public void Matches_Carry_Their_Rectangles_Line_By_Line()
    {
        var page = ScriptedPdfDocument.Layout("first li-\nne second");
        var searcher = new PdfSearcher(new PdfPageTextCache(new ScriptedPdfDocument("x")), 1);
        var matches = searcher.FindOnPage(3, page, PdfSearcher.PrepareQuery("line", false), default);
        matches.Length.ShouldBe(1);
        matches[0].PageIndex.ShouldBe(3);
        matches[0].Rects.Length.ShouldBe(2);
        matches[0].Bounds.Height.ShouldBeGreaterThan(ScriptedPdfDocument.LineHeight);
    }

    [Fact]
    public async Task Scan_Starts_At_A_Page_Wraps_Around_And_Reports_Every_Page()
    {
        using var document = new ScriptedPdfDocument("apple", "pear", "apple apple", "apple");
        var searcher = new PdfSearcher(new PdfPageTextCache(document), 4);
        var seen = new List<(int Page, int Count)>();
        await searcher.ScanAsync("apple", default, 2, (page, matches) => seen.Add((page, matches.Length)), CancellationToken.None);
        seen.ShouldBe([(2, 2), (3, 1), (0, 1), (1, 0)]);
    }

    [Fact]
    public async Task Scan_Runs_On_The_Thread_Pool()
    {
        using var document = new ScriptedPdfDocument("apple");
        var searcher = new PdfSearcher(new PdfPageTextCache(document), 1);
        var pool = false;
        await searcher.ScanAsync("apple", default, 0, (_, _) => pool = Thread.CurrentThread.IsThreadPoolThread, CancellationToken.None);
        pool.ShouldBeTrue();
    }

    [Fact]
    public async Task Cancelling_Stops_The_Scan()
    {
        using var document = new ScriptedPdfDocument("apple", "apple", "apple");
        document.HoldText = true;
        var cancel = new CancellationTokenSource();
        var searcher = new PdfSearcher(new PdfPageTextCache(document), 3);
        var seen = new List<int>();
        var scan = searcher.ScanAsync("apple", default, 0, (page, _) => seen.Add(page), cancel.Token);
        (await WaitForAsync(() => document.TextReads.Count == 1)).ShouldBeTrue();
        cancel.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(scan);
        document.Release(0);
        seen.ShouldBeEmpty();
        document.TextReads.ShouldBe([0]);
    }

    [Fact]
    public async Task A_Page_That_Cannot_Be_Read_Does_Not_End_The_Scan()
    {
        using var document = new ScriptedPdfDocument("apple", "apple", "apple");
        document.Unreadable.Add(1);
        var searcher = new PdfSearcher(new PdfPageTextCache(document), 3);
        var seen = new List<(int, int)>();
        await searcher.ScanAsync("apple", default, 0, (page, matches) => seen.Add((page, matches.Length)), CancellationToken.None);
        seen.ShouldBe([(0, 1), (1, 0), (2, 1)]);
    }

    [Fact]
    public async Task Closing_The_Document_Ends_The_Scan()
    {
        var document = new ScriptedPdfDocument("apple", "apple");
        document.HoldText = true;
        var searcher = new PdfSearcher(new PdfPageTextCache(document), 2);
        var scan = searcher.ScanAsync("apple", default, 0, (_, _) => { }, CancellationToken.None);
        (await WaitForAsync(() => document.TextReads.Count == 1)).ShouldBeTrue();
        document.Dispose();
        await Should.ThrowAsync<OperationCanceledException>(scan);
    }

    [Fact]
    public async Task Text_Cache_Keeps_The_64_Most_Recent_Pages()
    {
        var pages = Enumerable.Range(0, 70).Select(i => $"page {i}").ToArray();
        using var document = new ScriptedPdfDocument(pages);
        using var cache = new PdfPageTextCache(document);
        for (var i = 0; i < 70; i++)
        {
            await cache.GetAsync(i, CancellationToken.None);
        }
        cache.Count.ShouldBe(PdfPageTextCache.Capacity);
        PdfPageTextCache.Capacity.ShouldBe(64);
        cache.TryGet(0).ShouldBeNull();
        cache.TryGet(5).ShouldBeNull();
        cache.TryGet(6).ShouldNotBeNull();
        cache.TryGet(69).ShouldNotBeNull();
        document.TextReads.Count.ShouldBe(70);

        // Use keeps a page in: 6 survives what would have pushed it out.
        cache.TryGet(6);
        await cache.GetAsync(0, CancellationToken.None);
        cache.TryGet(6).ShouldNotBeNull();
        cache.TryGet(7).ShouldBeNull();
    }

    [Fact]
    public async Task Text_Cache_Reads_A_Page_Once_For_Everyone()
    {
        using var document = new ScriptedPdfDocument("a");
        document.HoldText = true;
        using var cache = new PdfPageTextCache(document);
        var cancel = new CancellationTokenSource();
        var first = cache.GetAsync(0, cancel.Token);
        var second = cache.GetAsync(0, CancellationToken.None);
        cancel.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(first);
        document.Release(0);
        (await second).Text.ShouldBe("a");
        document.TextReads.ShouldBe([0]);
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                return false;
            }
            await Task.Delay(5);
        }
        return true;
    }

    [Fact]
    public void Results_Are_In_Document_Order_Whatever_The_Scan_Order()
    {
        var results = new PdfSearchResults();
        results.Reset(2);
        static PdfSearchMatch M(int page, int start = 0) => new(page, start, 1, [new Rect(0, 0, 1, 1)]);
        results.Add(2, [M(2), M(2, 5)]).ShouldBe(0);
        results.Add(4, [M(4)]).ShouldBe(0);
        results.Add(0, [M(0)]).ShouldBe(1);
        results.Add(1, [M(1), M(1, 3)]).ShouldBe(2);
        Enumerable.Range(0, results.Count).Select(i => results[i].PageIndex).ShouldBe([0, 1, 1, 2, 2, 4]);
        results.TryFindPage(1, out var list, out var first).ShouldBeTrue();
        list[first].Start.ShouldBe(0);
        list[first + 1].Start.ShouldBe(3);
        results.TryFindPage(3, out _, out _).ShouldBeFalse();
        results.TryFindPage(4, out var later, out var at).ShouldBeTrue();
        later[at].PageIndex.ShouldBe(4);
    }

    #endregion
}
