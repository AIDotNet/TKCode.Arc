using Avalonia;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Pdf.Native.Tests;

/// <summary>Text, links and outline, with coordinates checked against what PDFium really draws.</summary>
public sealed class PdfiumContentTests
{
    private const double Dip = 96.0 / 72.0;

    /// <summary>A heading of make-sample.py starts at x 64 with its baseline at y 756 of an 842 pt page.</summary>
    [Fact]
    public async Task Text_has_a_box_per_character_with_a_top_left_origin()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        var page = await document.GetPageTextAsync(2, CancellationToken.None).WaitAsync(Pdfium.Timeout);
        page.CharBoxes.Count.ShouldBe(page.Text.Length);
        var start = page.Text.IndexOf("1. Pages and coordinates", StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0);
        var first = page.CharBoxes[start];
        first.Left.ShouldBe(64 * Dip, 3);
        first.Top.ShouldBeLessThan((842 - 756) * Dip);
        first.Bottom.ShouldBeGreaterThan((842 - 756) * Dip - 2);
        first.Height.ShouldBeInRange(22 * Dip * 0.7, 22 * Dip * 1.4);
        // Reading goes to the right along one line, and the line sits above the first paragraph.
        page.CharBoxes[start + 5].Left.ShouldBeGreaterThan(first.Right - 1);
        page.CharBoxes[start + 5].Top.ShouldBe(first.Top, 2);
        var paragraph = page.Text.IndexOf("A page viewer measures", StringComparison.Ordinal);
        page.CharBoxes[paragraph].Top.ShouldBeGreaterThan(first.Bottom);

        // HitTest and GetRects work on real boxes.
        var center = new Point(page.CharBoxes[start + 3].Center.X, page.CharBoxes[start + 3].Center.Y);
        page.HitTest(center).ShouldBe(start + 3);
        page.GetRects(start, 24).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Character_boxes_sit_on_the_rendered_glyphs()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        var page = await document.GetPageTextAsync(2, CancellationToken.None).WaitAsync(Pdfium.Timeout);
        var pixels = await Pdfium.RenderAsync(document, 2);
        var start = page.Text.IndexOf("A page viewer measures", StringComparison.Ordinal);
        for (var i = start; i < start + 22; i++)
        {
            if (char.IsWhiteSpace(page.Text[i]))
            {
                continue;
            }
            var box = page.CharBoxes[i];
            pixels.CountInk(new PixelRect((int)box.Left, (int)box.Top, (int)Math.Ceiling(box.Width) + 1, (int)Math.Ceiling(box.Height) + 1))
                  .ShouldBeGreaterThan(0, $"no ink under '{page.Text[i]}' at {box}");
        }
    }

    [Fact]
    public async Task Text_extraction_reads_the_chinese_of_the_cid_font()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        var page = await document.GetPageTextAsync(2, CancellationToken.None).WaitAsync(Pdfium.Timeout);
        page.Text.ShouldContain("页面与坐标");
    }

    [Fact]
    public async Task Text_of_every_page_is_valid_unicode()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        for (var i = 0; i < document.PageCount; i++)
        {
            var page = await document.GetPageTextAsync(i, CancellationToken.None).WaitAsync(Pdfium.Timeout);
            page.Text.ShouldNotBeEmpty();
            // Search normalizes page text, which throws on invalid UTF-16.
            page.Text.Normalize(System.Text.NormalizationForm.FormKC).ShouldNotBeEmpty();
        }
    }

    /// <summary>
    /// Page space (x, y) of the fixture's 400 x 600 pt pages against what a reader sees, in points from the top left, for each
    /// /Rotate. The last page has the same drawing shifted along with its MediaBox.
    /// </summary>
    public static TheoryData<int, double, double, double, double, double, double, double, double> RotatedPages => new()
    {
        // page, width, height, baseline start x, y, square center x, y, first glyph direction x, y
        { 0, 400, 600, 50, 100, 325, 475, 1, 0 },
        { 1, 600, 400, 500, 50, 125, 325, 0, 1 },
        { 2, 400, 600, 350, 500, 75, 125, -1, 0 },
        { 3, 600, 400, 100, 350, 475, 75, 0, -1 },
        { 4, 400, 600, 50, 100, 325, 475, 1, 0 },
    };

    [Theory]
    [MemberData(nameof(RotatedPages))]
    public async Task Rotation_and_page_boxes_are_converted(int index, double width, double height, double textX, double textY, double squareX, double squareY, double directionX, double directionY)
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Rotated);
        document.PageCount.ShouldBe(5);
        var size = document.GetPageSize(index);
        size.Width.ShouldBe(width * Dip, 0.01);
        size.Height.ShouldBe(height * Dip, 0.01);

        // The render is PDFium's own view of the page: it has the square where the table says.
        var pixels = await Pdfium.RenderAsync(document, index);
        pixels.At((int)(squareX * Dip), (int)(squareY * Dip)).ShouldBe(((byte)0, (byte)0, (byte)0, (byte)255));

        var page = await document.GetPageTextAsync(index, CancellationToken.None).WaitAsync(Pdfium.Timeout);
        page.Text.ShouldStartWith("MARK");
        var first = page.CharBoxes[0];
        var last = page.CharBoxes[3];
        var start = new Point(textX * Dip, textY * Dip);
        first.Inflate(3).Contains(start).ShouldBeTrue($"{first} should touch {start}");
        var moved = last.Center - first.Center;
        (moved.X * directionX).ShouldBeGreaterThanOrEqualTo(0);
        (moved.Y * directionY).ShouldBeGreaterThanOrEqualTo(0);
        (Math.Abs(moved.X * directionX) + Math.Abs(moved.Y * directionY)).ShouldBeGreaterThan(40);

        // The boxes hold all the ink of the word: leave out the square and what remains is inside them.
        var square = new PixelRect((int)((squareX - 30) * Dip), (int)((squareY - 30) * Dip), (int)(60 * Dip), (int)(60 * Dip));
        var ink = pixels.InkBounds(square);
        var boxes = first.Union(page.CharBoxes[1]).Union(page.CharBoxes[2]).Union(last).Inflate(2);
        boxes.Contains(new Rect(ink.X, ink.Y, ink.Width, ink.Height)).ShouldBeTrue($"ink {ink} is outside the boxes {boxes}");
    }

    [Fact]
    public async Task Outline_has_titles_levels_and_destinations()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        var outline = await document.GetOutlineAsync(CancellationToken.None).WaitAsync(Pdfium.Timeout);
        outline.Count.ShouldBe(7);
        outline[0].Title.ShouldBe("Cover");
        outline[0].Destination.ShouldBe(new PdfDestination(0, null));
        outline[1].Title.ShouldBe("Contents  目录");

        var viewing = outline[2];
        viewing.Title.ShouldBe("Viewing  查看");
        viewing.Destination.ShouldBeNull();
        viewing.Children.Count.ShouldBe(3);
        var chapter = viewing.Children[0];
        chapter.Title.ShouldBe("1. Pages and coordinates  页面与坐标");
        chapter.Children.ShouldBeEmpty();
        chapter.Destination!.PageIndex.ShouldBe(2);
        // [page /XYZ 0 786 null]: 786 of 842 pt from the bottom.
        chapter.Destination.PointInPage!.Value.X.ShouldBe(0, 0.5);
        chapter.Destination.PointInPage!.Value.Y.ShouldBe((842 - 786) * Dip, 0.5);

        // [page /FitH 786] has the top and no left.
        var documents = outline[3];
        documents.Destination!.PageIndex.ShouldBe(5);
        documents.Destination.PointInPage!.Value.X.ShouldBe(0, 0.5);
        documents.Destination.PointInPage!.Value.Y.ShouldBe((842 - 786) * Dip, 0.5);

        outline[6].Destination!.PageIndex.ShouldBe(11);
    }

    [Fact]
    public async Task A_document_without_an_outline_has_an_empty_one()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Rotated);
        (await document.GetOutlineAsync(CancellationToken.None).WaitAsync(Pdfium.Timeout)).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_cyclic_outline_terminates_with_each_entry_once()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Fixture("cyclic-outline.pdf"));
        var outline = await document.GetOutlineAsync(CancellationToken.None).WaitAsync(Pdfium.Timeout);
        outline.Select(item => item.Title).ShouldBe(["A", "B"]);
        var c = outline[1].Children.ShouldHaveSingleItem();
        c.Title.ShouldBe("C");
        c.Children.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_outline_stops_at_64_levels()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Fixture("deep-outline.pdf"));
        var outline = await document.GetOutlineAsync(CancellationToken.None).WaitAsync(Pdfium.Timeout);
        var depth = 0;
        for (var level = outline; level.Count > 0; level = level[0].Children)
        {
            level.ShouldHaveSingleItem().Title.ShouldBe($"Level {depth}");
            depth++;
        }
        depth.ShouldBe(64);
    }

    [Fact]
    public async Task The_outline_stops_at_ten_thousand_nodes()
    {
        using var document = await Pdfium.OpenAsync(PdfSource.FromBytes(GeneratedPdf.Build(3, outlineEntries: 12_000)));
        var outline = await document.GetOutlineAsync(CancellationToken.None).WaitAsync(Pdfium.Timeout);
        outline.Count.ShouldBe(10_000);
        outline[^1].Title.ShouldBe("Entry 9999");
    }

    [Fact]
    public async Task A_long_outline_still_has_points_past_the_pages_it_loads_for_them()
    {
        const int pages = 100;
        using var document = await Pdfium.OpenAsync(PdfSource.FromBytes(GeneratedPdf.Build(pages, outlineEntries: pages, rotatedPage: 99)));
        var outline = await document.GetOutlineAsync(CancellationToken.None).WaitAsync(Pdfium.Timeout);
        outline.Count.ShouldBe(pages);
        foreach (var index in new[] { 0, 63, 64, 98 })
        {
            var destination = outline[index].Destination!;
            destination.PageIndex.ShouldBe(index);
            destination.PointInPage!.Value.X.ShouldBe(72 * Dip, 0.5);
            destination.PointInPage!.Value.Y.ShouldBe((792 - 700) * Dip, 0.5);
        }
        // The rotated page's size has no page to borrow from and the budget is spent: the whole page, not a wrong point.
        outline[99].Destination.ShouldBe(new PdfDestination(99, null));
    }

    [Fact]
    public async Task Internal_links_lead_to_a_page_and_a_point()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        var links = await document.GetLinksAsync(1, CancellationToken.None).WaitAsync(Pdfium.Timeout);
        var text = await document.GetPageTextAsync(1, CancellationToken.None).WaitAsync(Pdfium.Timeout);
        var internalLinks = links.Where(link => link.Destination is not null).ToList();
        internalLinks.Count.ShouldBe(10);
        internalLinks.Select(link => link.Destination!.PageIndex).ShouldBe([2, 3, 4, 5, 6, 7, 8, 9, 10, 11]);

        var first = internalLinks[0];
        first.Uri.ShouldBeNull();
        first.Destination!.PointInPage!.Value.Y.ShouldBe((842 - 786) * Dip, 0.5);
        // The area is over its text.
        var label = text.Text.IndexOf("1. Pages and coordinates", StringComparison.Ordinal);
        var labelBox = text.CharBoxes[label];
        first.Bounds.Inflate(2).Contains(labelBox.Center).ShouldBeTrue($"{first.Bounds} should cover {labelBox}");
        first.Bounds.Top.ShouldBeLessThan(document.GetPageSize(1).Height / 2);
    }

    [Fact]
    public async Task An_external_annotation_link_has_its_address_and_is_not_repeated_as_a_web_link()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        var links = await document.GetLinksAsync(1, CancellationToken.None).WaitAsync(Pdfium.Timeout);
        var external = links.Where(link => link.Uri is not null).ToList();
        var link = external.ShouldHaveSingleItem();
        link.Uri.ShouldBe(new Uri("https://github.com/AIDotNet/TKCode.Arc"));
        link.Destination.ShouldBeNull();
    }

    [Fact]
    public async Task Go_to_actions_uri_actions_and_addresses_in_the_text_are_all_links()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        // Chapter 8: a link with a GoTo action, one with a URI action, and an address that is only text.
        var page = await document.GetPageTextAsync(9, CancellationToken.None).WaitAsync(Pdfium.Timeout);
        var links = await document.GetLinksAsync(9, CancellationToken.None).WaitAsync(Pdfium.Timeout);
        links.Count.ShouldBe(3);
        links.ShouldContain(link => link.Destination != null && link.Destination.PageIndex == 1);
        links.ShouldContain(link => link.Uri == new Uri("https://github.com/AIDotNet/TKCode.Arc"));
        var web = links.Single(link => link.Uri == new Uri("https://www.example.com/arc/pdf-guide"));
        var address = page.Text.IndexOf("https://www.example.com", StringComparison.Ordinal);
        web.Bounds.Inflate(2).Contains(page.CharBoxes[address].Center).ShouldBeTrue();
    }

    [Fact]
    public async Task Links_on_a_rotated_page_and_to_a_rotated_page_are_in_page_coordinates()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Rotated);
        // Rect [50 495 120 525] on /Rotate 90 reads x = y, y = x. The target (50, 500) on /Rotate 180 reads x = 400 - x, y = y.
        var link = (await document.GetLinksAsync(1, CancellationToken.None).WaitAsync(Pdfium.Timeout)).ShouldHaveSingleItem();
        link.Bounds.X.ShouldBe(495 * Dip, 0.5);
        link.Bounds.Y.ShouldBe(50 * Dip, 0.5);
        link.Bounds.Width.ShouldBe(30 * Dip, 0.5);
        link.Bounds.Height.ShouldBe(70 * Dip, 0.5);
        link.Destination!.PageIndex.ShouldBe(2);
        link.Destination.PointInPage!.Value.X.ShouldBe(350 * Dip, 0.5);
        link.Destination.PointInPage!.Value.Y.ShouldBe(500 * Dip, 0.5);
    }
}
