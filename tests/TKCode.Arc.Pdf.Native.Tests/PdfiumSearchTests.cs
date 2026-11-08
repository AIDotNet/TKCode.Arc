using Avalonia;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Pdf.Native.Tests;

/// <summary>Search over the text real PDFium extracts, and page labels that never make the caller wait.</summary>
public sealed class PdfiumSearchTests
{
    private static async Task<List<PdfSearchMatch>> ScanAsync(PdfDocument document, string query, PdfSearchOptions options = default, int start = 0)
    {
        using var cache = new PdfPageTextCache(document);
        var searcher = new PdfSearcher(cache, document.PageCount);
        var found = new List<PdfSearchMatch>();
        await searcher.ScanAsync(query, options, start, (_, matches) =>
        {
            lock (found)
            {
                found.AddRange(matches);
            }
        }, CancellationToken.None).WaitAsync(Pdfium.Timeout);
        return found;
    }

    [Fact]
    public async Task Finds_The_Word_The_Sample_Has_Exactly_Once()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        var found = await ScanAsync(document, "lighthouse");
        var match = found.ShouldHaveSingleItem();
        var text = await document.GetPageTextAsync(match.PageIndex, CancellationToken.None).WaitAsync(Pdfium.Timeout);
        text.Text.Substring(match.Start, match.Length).ShouldBe("lighthouse");
        // The highlight is where the characters are.
        match.Rects.Length.ShouldBe(1);
        match.Bounds.Contains(text.CharBoxes[match.Start].Center).ShouldBeTrue();
        match.Bounds.Width.ShouldBeGreaterThan(20);
    }

    [Fact]
    public async Task Finds_Chinese_From_Text_Extraction()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        // The sample has it in Chinese exactly once too, in a font PDFium does not need to draw to extract.
        var match = (await ScanAsync(document, "灯塔")).ShouldHaveSingleItem();
        var text = await document.GetPageTextAsync(match.PageIndex, CancellationToken.None).WaitAsync(Pdfium.Timeout);
        text.Text.Substring(match.Start, match.Length).ShouldBe("灯塔");
        match.Rects.ShouldNotBeEmpty();

        // Whole word has nothing to say about it.
        (await ScanAsync(document, "灯塔", new PdfSearchOptions(false, true))).Count.ShouldBe(1);
        // The same page has its English twin.
        var english = (await ScanAsync(document, "lighthouse")).ShouldHaveSingleItem();
        english.PageIndex.ShouldBe(match.PageIndex);
    }

    [Fact]
    public async Task Chinese_Is_Found_Across_Line_Ends()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        // A sentence long enough to wrap in the sample (make-sample.py wraps Chinese at any character).
        const string sentence = "搜索基于纯文本和每个字符的位置框";
        var found = await ScanAsync(document, sentence);
        found.ShouldHaveSingleItem();
        var whole = await ScanAsync(document, "灯塔这个词在本文档中只出现一次");
        whole.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Case_And_Whole_Word_Options_Work_On_Real_Text()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        var any = await ScanAsync(document, "LIGHTHOUSE");
        any.Count.ShouldBe(1);
        (await ScanAsync(document, "LIGHTHOUSE", new PdfSearchOptions(true, false))).ShouldBeEmpty();
        (await ScanAsync(document, "lighthouse", new PdfSearchOptions(true, true))).Count.ShouldBe(1);
        (await ScanAsync(document, "lighthous", new PdfSearchOptions(false, true))).ShouldBeEmpty();
        (await ScanAsync(document, "lighthous")).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Scan_Reports_Every_Page_Once_From_Where_It_Starts()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        using var cache = new PdfPageTextCache(document);
        var searcher = new PdfSearcher(cache, document.PageCount);
        var pages = new List<int>();
        await searcher.ScanAsync("the", default, 5, (page, _) => pages.Add(page), CancellationToken.None).WaitAsync(Pdfium.Timeout);
        pages.ShouldBe(Enumerable.Range(5, 7).Concat(Enumerable.Range(0, 5)));
    }

    [Fact]
    public async Task A_Hyphen_At_A_Line_End_Is_Joined_However_PDFium_Writes_It()
    {
        var file = GeneratedPdf.Build(1, lines: _ => ["We co-", "operate and re-", "use all."]);
        using var document = await Pdfium.OpenAsync(PdfSource.FromBytes(file));
        var found = await ScanAsync(document, "cooperate");
        var match = found.ShouldHaveSingleItem();
        match.Rects.Length.ShouldBe(2);
        (await ScanAsync(document, "reuse")).ShouldHaveSingleItem();
        (await ScanAsync(document, "operate and")).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Selection_Text_Has_A_Real_Hyphen_Whatever_PDFium_Wrote()
    {
        var file = GeneratedPdf.Build(1, lines: _ => ["We co-", "operate."]);
        using var document = await Pdfium.OpenAsync(PdfSource.FromBytes(file));
        var page = await document.GetPageTextAsync(0, CancellationToken.None).WaitAsync(Pdfium.Timeout);
        page.CharBoxes.Count.ShouldBe(page.Text.Length);
        // What this build of PDFium does with such a hyphen; the search and selection tests depend on knowing it.
        page.Text.ShouldContain("co\u0002operate");
        PdfSelectionLayer.Clean(page.Text).ShouldContain("co-\noperate");
    }

    [Fact]
    public async Task Disposing_During_A_Scan_Ends_It_Without_A_Crash()
    {
        var file = GeneratedPdf.Build(400);
        var document = await Pdfium.OpenAsync(PdfSource.FromBytes(file));
        using var cache = new PdfPageTextCache(document);
        var searcher = new PdfSearcher(cache, document.PageCount);
        var first = new TaskCompletionSource();
        var scan = searcher.ScanAsync("page", default, 0, (_, _) => first.TrySetResult(), CancellationToken.None);
        await first.Task.WaitAsync(Pdfium.Timeout);
        document.Dispose();
        await Should.ThrowAsync<OperationCanceledException>(scan.WaitAsync(Pdfium.Timeout));
    }

    #region Page labels

    [Fact]
    public async Task Labels_Are_Known_At_Open_For_An_Ordinary_Document()
    {
        using var document = await Pdfium.OpenAsync(PdfSource.FromBytes(GeneratedPdf.Build(20, pageLabels: true)));
        document.GetPageLabel(0).ShouldBe("i");
        document.GetPageLabel(19).ShouldBe("xx");
    }

    [Fact]
    public async Task Asking_For_A_Label_Never_Waits_For_The_PDFium_Thread()
    {
        using var document = await Pdfium.OpenAsync(PdfSource.FromBytes(GeneratedPdf.Build(2000, pageLabels: true)));
        // Nothing on that thread can run while this holds it: a call that needed it could only time out.
        using (new ThreadBlocker())
        {
            var asking = Task.Run(() =>
            {
                document.GetPageLabel(0);
                document.GetPageLabel(1999);
            });
            await asking.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task Labels_Of_A_Long_Document_Arrive_In_The_Background()
    {
        // Past the first batch at open: the rest comes with events, without anyone asking, while other work goes on.
        var file = GeneratedPdf.Build(3000, pageLabels: true);
        var changes = 0;
        using var document = await Pdfium.OpenAsync(PdfSource.FromBytes(file));
        document.PageLabelsChanged += (_, _) => Interlocked.Increment(ref changes);
        document.GetPageLabel(0).ShouldBe("i");

        var deadline = DateTime.UtcNow + Pdfium.Timeout;
        while (document.GetPageLabel(2999) is null)
        {
            DateTime.UtcNow.ShouldBeLessThan(deadline);
            // Work asked for now gets in between the batches.
            (await document.GetPageTextAsync(1, CancellationToken.None).WaitAsync(Pdfium.Timeout)).Text.ShouldNotBeEmpty();
            await Task.Delay(1);
        }
        document.GetPageLabel(2999).ShouldNotBeNullOrEmpty();
        document.GetPageLabel(1500).ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_Document_Closed_While_Its_Labels_Are_Read_Stays_Closed()
    {
        var document = await Pdfium.OpenAsync(PdfSource.FromBytes(GeneratedPdf.Build(3000, pageLabels: true)));
        document.Dispose();
        Should.Throw<ObjectDisposedException>(() => document.GetPageLabel(0));

        // The thread is fine afterwards.
        using var next = await Pdfium.OpenAsync(Pdfium.Sample);
        next.GetPageLabel(0).ShouldNotBeNull();
    }

    [Fact]
    public async Task A_Document_Without_Labels_Reads_None()
    {
        using var document = await Pdfium.OpenAsync(PdfSource.FromBytes(GeneratedPdf.Build(600)));
        document.GetPageLabel(0).ShouldBeNull();
        document.GetPageLabel(599).ShouldBeNull();
    }

    #endregion
}
