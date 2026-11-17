using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>The paper renderer of the document extraction: page size, blocks, spans, their paint, pointer and accessibility.</summary>
public class DocumentPageTests
{
    #region Rig

    private static (Window Window, DocumentPage Page) Show(ExtractionDocument? document, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var page = new DocumentPage { Document = document, ArticleName = document?.Name };
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = page }, level);
        window.Width = 640;
        window.Height = 900;
        Settle(window);
        return (window, page);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Frames(ManualFrameClock clock, Window window, int milliseconds)
    {
        clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
        Dispatcher.UIThread.RunJobs();
        _ = window;
    }

    private static List<string> Texts(Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Inlines?.Text ?? t.Text ?? string.Empty).ToList();

    /// <summary>Every span id of a document, in reading order.</summary>
    private static List<string> SpanIdsOf(ExtractionDocument document)
    {
        var ids = new List<string>();
        void Add(DocumentText? text)
        {
            if (text is null)
            {
                return;
            }
            ids.AddRange(text.Inlines.OfType<DocumentSpan>().Select(s => s.Id));
        }
        void Block(DocumentBlock block)
        {
            switch (block)
            {
                case DocumentEmailHeader header:
                    Add(header.Subject); Add(header.FromName); Add(header.FromAddress); Add(header.To); Add(header.Date);
                    break;
                case DocumentHeading heading:
                    Add(heading.Text);
                    break;
                case DocumentParagraph paragraph:
                    Add(paragraph.Text);
                    break;
                case DocumentLines lines:
                    lines.Lines.ToList().ForEach(Add);
                    break;
                case DocumentPairs pairs:
                    pairs.Pairs.ToList().ForEach(p => Add(p.Value));
                    break;
                case DocumentMetaStrip strip:
                    strip.Cells.ToList().ForEach(p => Add(p.Value));
                    break;
                case DocumentColumns columns:
                    columns.Columns.ToList().ForEach(Block);
                    break;
                case DocumentTable table:
                    foreach (var row in table.Rows)
                    {
                        row.Cells.ToList().ForEach(Add);
                        Add(row.Detail);
                    }
                    break;
                case DocumentTotals totals:
                    totals.Rows.ToList().ForEach(r => Add(r.Value));
                    break;
            }
        }
        document.Blocks.ToList().ForEach(Block);
        return ids;
    }

    private static DocumentText Text(params object[] parts)
    {
        var text = new DocumentText();
        foreach (var part in parts)
        {
            text.Inlines.Add(part is string s ? new DocumentRun(s) : (DocumentInline)part);
        }
        return text;
    }

    /// <summary>A Pdf page with three narrow columns; the first holds a long span that wraps.</summary>
    private static ExtractionDocument NarrowColumns()
    {
        var document = new ExtractionDocument { Id = "note", Name = "note.pdf", Kind = DocumentKind.Pdf };
        var columns = new DocumentColumns();
        columns.Columns.Add(new DocumentParagraph
        {
            Text = Text("Before ", new DocumentSpan("long", "a long span that runs over several lines of a narrow column"), " after.")
        });
        columns.Columns.Add(new DocumentParagraph { Text = "Second column." });
        columns.Columns.Add(new DocumentParagraph { Text = "Third column." });
        document.Blocks.Add(columns);
        document.Blocks.Add(new DocumentRule());
        document.Blocks.Add(new DocumentParagraph { Text = Text("A short ", new DocumentSpan("short", "span"), ".") });
        return document;
    }

    private static readonly DocumentSpanPaint s_linked = new(DocumentSpanState.Linked, 1, false);

    #endregion

    #region Size and blocks

    [AvaloniaFact]
    public void A_Pdf_Page_Is_540_By_764_And_An_Email_Is_540_Wide_With_Its_Content_Height()
    {
        var (window, page) = Show(ExtractionFixtures.Invoice());
        try
        {
            page.Bounds.Size.ShouldBe(new Size(DocumentPage.BaseWidth, DocumentPage.PdfHeight));
            page.IsPdf.ShouldBeTrue();

            page.Document = ExtractionFixtures.Email();
            Settle(window);
            page.IsPdf.ShouldBeFalse();
            page.Bounds.Width.ShouldBe(DocumentPage.BaseWidth);
            page.Bounds.Height.ShouldBeInRange(300, 1000);
            page.Bounds.Height.ShouldBe(page.PageContent!.Bounds.Height, 1);

            page.Document = null;
            Settle(window);
            page.Bounds.Size.ShouldBe(new Size(DocumentPage.BaseWidth, DocumentPage.PdfHeight));
            page.PageContent.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Block_Of_The_Invoice_Is_Drawn_As_Text()
    {
        var (window, page) = Show(ExtractionFixtures.Invoice());
        try
        {
            var texts = Texts(page);
            texts.ShouldContain("oakridge ceramics");                  // masthead name and detail
            texts.ShouldContain("Invoice");                            // masthead title
            texts.ShouldContain("From");                               // lines label
            texts.ShouldContain("Oakridge Ceramics Ltd");              // lines (a span)
            texts.ShouldContain("3011 AB Rotterdam, Netherlands");     // second column
            texts.ShouldContain("Invoice no.");                        // meta strip label
            texts.ShouldContain("OC-2026-1187");                       // meta strip value
            texts.ShouldContain("Unit price");                         // table head
            texts.ShouldContain("Glazed floor tiles, 20×20 cm");       // table cell
            texts.ShouldContain("Batch 41, matte sand");               // table detail line
            texts.ShouldContain("1,280.00");
            texts.ShouldContain("VAT 21%");                            // totals
            texts.ShouldContain("EUR 2,686.20");                       // strong row, two spans
            texts.ShouldContain("Payment details");                    // heading
            texts.ShouldContain("Account holder");                     // pairs
            texts.ShouldContain("NL70 ZZTE 0417 2635 98");
            texts.ShouldContain(t => t.StartsWith("Please note our new account", StringComparison.Ordinal)); // muted paragraph
            texts.ShouldContain("accounts@oakridge.example · oakridge.example"); // footer
            texts.ShouldContain("Page 1 of 1");
            page.GetVisualDescendants().OfType<DocumentPageMark>().Count().ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Block_Of_The_Email_Is_Drawn_As_Text()
    {
        var (window, page) = Show(ExtractionFixtures.Email());
        try
        {
            var texts = Texts(page);
            texts.ShouldContain("Your parcel FB-77302 has shipped");
            texts.ShouldContain("FB");                                 // avatar initials
            texts.ShouldContain("Fernhill Books <orders@fernhillbooks.example>");
            texts.ShouldContain("To Jonas Weber");
            texts.ShouldContain("Tue, 22 Sep 2026, 08:15");
            texts.ShouldContain("fernhill books");
            texts.ShouldContain("Hello Jonas,");
            texts.ShouldContain("Tracking number");
            texts.ShouldContain("Address");
            texts.ShouldContain("Items in this parcel");
            texts.ShouldContain("Notebook, dot grid, A5");
            texts.ShouldContain("€51.50");
            // No page number on an email, and no letterhead mark in this one.
            texts.ShouldNotContain("Page 1 of 1");
            page.GetVisualDescendants().OfType<DocumentPageMark>().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Footer_Is_Pinned_To_The_Bottom_Of_A_Pdf_Page()
    {
        var (window, page) = Show(ExtractionFixtures.Invoice());
        try
        {
            var number = page.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Page 1 of 1");
            var bottom = number.TranslatePoint(new Point(0, number.Bounds.Height), page)!.Value.Y;
            bottom.ShouldBe(DocumentPage.PdfHeight - 30, 0.5);
            var right = number.TranslatePoint(new Point(number.Bounds.Width, 0), page)!.Value.X;
            right.ShouldBe(DocumentPage.BaseWidth - 48, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Text_Columns_Of_A_Table_Take_Half_The_Width_And_Numbers_Are_Right_Aligned()
    {
        var (window, page) = Show(ExtractionFixtures.Invoice());
        try
        {
            var head = page.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Amount");
            head.TextAlignment.ShouldBe(TextAlignment.Right);
            var right = head.TranslatePoint(new Point(head.Bounds.Width, 0), page)!.Value.X;
            right.ShouldBe(DocumentPage.BaseWidth - 48, 0.5);
            var description = page.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Description");
            var qty = page.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Qty");
            var qtyLeft = qty.TranslatePoint(default, page)!.Value.X - 12;
            var descriptionLeft = description.TranslatePoint(default, page)!.Value.X;
            (qtyLeft - descriptionLeft).ShouldBe(444 / 2.0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Initials_Are_The_First_Letters_Of_The_First_Two_Words()
    {
        DocumentPageBuilder.Initials("Fernhill Books").ShouldBe("FB");
        DocumentPageBuilder.Initials("  ada  lovelace byron ").ShouldBe("AL");
        DocumentPageBuilder.Initials("Solo").ShouldBe("S");
        DocumentPageBuilder.Initials("").ShouldBe("");
    }

    #endregion

    #region Span geometry

    [AvaloniaFact]
    public void Every_Span_Of_The_Fixtures_Has_Rectangles_Inside_The_Page()
    {
        foreach (var document in new[] { ExtractionFixtures.Invoice(), ExtractionFixtures.Email() })
        {
            var (window, page) = Show(document);
            try
            {
                var expected = SpanIdsOf(document);
                expected.ShouldNotBeEmpty();
                page.SpanIds.ShouldBe(expected);
                // The headless stub font is much wider than Inter, so here the invoice's last lines run past the bottom of
                // its page and some amounts past the left of their cells (the snapshots show both fit with the real font):
                // every rectangle lies on the lines of its own text (grown by the padding) and inside the page's width; an
                // email grows to hold them all.
                var paper = new Rect(0, 0, page.Bounds.Width, page.Bounds.Height);
                foreach (var id in expected)
                {
                    page.TryGetSpanRects(id, out var rects).ShouldBeTrue(id);
                    rects.ShouldNotBeEmpty();
                    var texts = page.GetSpanPieces(id)
                                    .Select(p => new Rect(p.Text.TranslatePoint(default, page)!.Value, p.Text.Bounds.Size).Inflate(new Thickness(2, 1)))
                                    .ToList();
                    foreach (var rect in rects)
                    {
                        rect.Width.ShouldBeGreaterThan(2 * DocumentPage.SpanInlinePadding, id);
                        rect.Height.ShouldBeGreaterThan(2 * DocumentPage.SpanBlockPadding, id);
                        texts.Any(t => t.Top <= rect.Top && rect.Bottom <= t.Bottom).ShouldBeTrue($"{id} {rect}");
                        rect.Left.ShouldBeGreaterThanOrEqualTo(0, $"{id} {rect}");
                        rect.Right.ShouldBeLessThanOrEqualTo(paper.Right, $"{id} {rect}");
                        if (!page.IsPdf)
                        {
                            paper.Contains(rect).ShouldBeTrue($"{id} {rect}");
                        }
                    }
                }
                page.TryGetSpanRects("nope", out var none).ShouldBeFalse();
                none.ShouldBeEmpty();
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void A_Span_Rectangle_Is_Its_Text_Box_Padded_2_By_1()
    {
        var (window, page) = Show(ExtractionFixtures.Invoice());
        try
        {
            var (text, _, _) = page.GetSpanPieces("iban").Single();
            text.Text.ShouldBe("NL70 ZZTE 0417 2635 98");
            page.TryGetSpanRects("iban", out var rects).ShouldBeTrue();
            var rect = rects.ShouldHaveSingleItem();
            var origin = text.TranslatePoint(text.TextOrigin, page)!.Value;
            var local = TextRangeRenderer.GetRects(text.TextLayout, 0, text.Text!.Length).Single();
            rect.ShouldBe(local.Translate(new Vector(origin.X, origin.Y)).Inflate(new Thickness(2, 1)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Span_That_Wraps_Has_One_Rectangle_Per_Line()
    {
        var (window, page) = Show(NarrowColumns());
        try
        {
            page.TryGetSpanRects("long", out var rects).ShouldBeTrue();
            rects.Count.ShouldBeGreaterThan(1);
            var (text, _, _) = page.GetSpanPieces("long").Single();
            var column = new Rect(text.TranslatePoint(default, page)!.Value, text.Bounds.Size).Inflate(new Thickness(2, 1));
            for (var i = 0; i < rects.Count; i++)
            {
                column.Contains(rects[i]).ShouldBeTrue($"{rects[i]} in {column}");
                if (i > 0)
                {
                    rects[i].Top.ShouldBeGreaterThan(rects[i - 1].Top);
                    rects[i].Height.ShouldBe(rects[0].Height, 0.01);
                }
            }
            page.TryGetSpanRects("short", out var shortRects).ShouldBeTrue();
            shortRects.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rectangles_Are_Cached_Until_The_Layout_Changes()
    {
        var (window, page) = Show(NarrowColumns());
        try
        {
            page.TryGetSpanRects("long", out var first).ShouldBeTrue();
            page.TryGetSpanRects("long", out var second).ShouldBeTrue();
            second.ShouldBeSameAs(first);

            // A wider page column: the same span wraps differently, so the list is new.
            var (text, _, _) = page.GetSpanPieces("long").Single();
            text.FontSize = 7;
            Settle(window);
            page.TryGetSpanRects("long", out var third).ShouldBeTrue();
            third.ShouldNotBeSameAs(first);
            third.Count.ShouldBeLessThan(first.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SpanAt_Agrees_With_The_Rectangles()
    {
        var (window, page) = Show(ExtractionFixtures.Invoice());
        try
        {
            foreach (var id in page.SpanIds)
            {
                page.TryGetSpanRects(id, out var rects).ShouldBeTrue();
                foreach (var rect in rects)
                {
                    page.SpanAt(rect.Center).ShouldBe(id);
                    // The padding belongs to the span too, where no other span's text is (the stub font can make
                    // right-aligned numbers overflow their cells).
                    var edge = new Point(rect.Left + 0.5, rect.Center.Y);
                    if (!page.SpanIds.Any(other => other != id && page.TryGetSpanRects(other, out var o) && o.Any(r => r.Contains(edge))))
                    {
                        page.SpanAt(edge).ShouldBe(id);
                    }
                }
            }
            page.SpanAt(new Point(4, 4)).ShouldBeNull();
            // A text that is not a span.
            var plain = page.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Bluefin Logistics BV");
            page.SpanAt(plain.TranslatePoint(new Point(plain.Bounds.Width / 2, plain.Bounds.Height / 2), page)!.Value).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Paint

    [AvaloniaFact]
    public void SetSpan_Tints_The_Span_In_Its_Tone_And_The_Check_Underline_Follows_NeedsCheck()
    {
        var (window, page) = Show(ExtractionFixtures.Invoice());
        try
        {
            page.SetSpan("vendor", new DocumentSpanPaint(DocumentSpanState.Linked, 2, false));
            page.GetSpanTint("vendor").ShouldBe(DocumentPage.LinkedAlpha);
            var (text, background, underline) = page.GetSpanPieces("vendor").Single();
            text.Decorations.ShouldContain(background);
            text.Decorations.ShouldContain(underline);
            TestHelpers.ColorOf(background.Background).ShouldBe(PromptTone.SeriesColor(page, 2));
            background.Opacity.ShouldBe(DocumentPage.LinkedAlpha);
            background.CornerRadius.ShouldBe(DocumentPage.SpanRadius);
            background.Outset.ShouldBe(new Thickness(2, 1));
            underline.Opacity.ShouldBe(0);

            page.SetSpan("vendor", new DocumentSpanPaint(DocumentSpanState.Lit, 2, true));
            page.GetSpanTint("vendor").ShouldBe(DocumentPage.LitAlpha);
            underline.Opacity.ShouldBe(1);
            underline.Underline.ShouldNotBeNull();
            underline.Underline!.Thickness.ShouldBe(1);
            underline.Underline.DashStyle.ShouldNotBeNull();
            underline.UnderlineOffset.ShouldBe(DocumentPage.CheckUnderlineOffset);
            var warning = ArcTokens.GetColor(page, ArcDesignTokenKind.Warning);
            TestHelpers.ColorOf(underline.Underline.Brush).ShouldBe(Oklch.MixOklab(warning, DocumentInk.Ink, 0.15));

            page.SetSpan("vendor", new DocumentSpanPaint(DocumentSpanState.Fresh, 2, false));
            page.GetSpanTint("vendor").ShouldBe(DocumentPage.FreshAlpha);
            underline.Opacity.ShouldBe(0);
            page.GetSpanPaint("vendor").ShouldBe(new DocumentSpanPaint(DocumentSpanState.Fresh, 2, false));

            // Tones outside 1 to 4 use tone 4; an unknown id does nothing.
            page.SetSpan("vat", new DocumentSpanPaint(DocumentSpanState.Linked, 9, false));
            TestHelpers.ColorOf(page.GetSpanPieces("vat").Single().Background.Background).ShouldBe(PromptTone.SeriesColor(page, 4));
            page.SetSpan("missing", s_linked);

            page.SetSpan("vendor", null);
            page.GetSpanTint("vendor").ShouldBe(0);
            page.GetSpanPaint("vendor").ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tint_Changes_Take_160ms_At_Full_Motion_And_Fresh_Comes_On_At_Once()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, page) = Show(ExtractionFixtures.Invoice(), ArcMotionLevel.Full);
        try
        {
            page.SetSpan("vendor", s_linked);
            page.GetSpanTint("vendor").ShouldBe(0);
            Frames(clock, window, 50);
            page.GetSpanTint("vendor").ShouldBeInRange(0.01, 0.095);
            Frames(clock, window, 150);
            page.GetSpanTint("vendor").ShouldBe(DocumentPage.LinkedAlpha, 1e-9);

            page.SetSpan("vendor", new DocumentSpanPaint(DocumentSpanState.Fresh, 1, false));
            page.GetSpanTint("vendor").ShouldBe(DocumentPage.FreshAlpha);

            page.SetSpan("vendor", s_linked);
            Frames(clock, window, 50);
            page.GetSpanTint("vendor").ShouldBeInRange(0.105, 0.31);
            Frames(clock, window, 150);
            page.GetSpanTint("vendor").ShouldBe(DocumentPage.LinkedAlpha, 1e-9);

            // Clearing fades out too.
            page.ClearSpans();
            page.GetSpanPaint("vendor").ShouldBeNull();
            Frames(clock, window, 50);
            page.GetSpanTint("vendor").ShouldBeInRange(0.001, 0.099);
            Frames(clock, window, 150);
            page.GetSpanTint("vendor").ShouldBe(0, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Fades_In_120ms_Linear_And_Off_Jumps()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, page) = Show(ExtractionFixtures.Invoice(), ArcMotionLevel.Reduced);
        try
        {
            page.SetSpan("vendor", new DocumentSpanPaint(DocumentSpanState.Lit, 1, false));
            Frames(clock, window, 60);
            // Linear: half way after half the time (frames are 1/60 s, so allow a frame).
            page.GetSpanTint("vendor").ShouldBe(DocumentPage.LitAlpha / 2, DocumentPage.LitAlpha / 7);
            Frames(clock, window, 80);
            page.GetSpanTint("vendor").ShouldBe(DocumentPage.LitAlpha, 1e-9);

            ArcMotion.SetLevel(window, ArcMotionLevel.Off);
            page.SetSpan("vendor", s_linked);
            page.GetSpanTint("vendor").ShouldBe(DocumentPage.LinkedAlpha);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ClearSpans_Clears_Every_Paint()
    {
        var (window, page) = Show(ExtractionFixtures.Invoice());
        try
        {
            foreach (var id in page.SpanIds)
            {
                page.SetSpan(id, new DocumentSpanPaint(DocumentSpanState.Linked, 3, true));
            }
            page.ClearSpans();
            foreach (var id in page.SpanIds)
            {
                page.GetSpanPaint(id).ShouldBeNull();
                page.GetSpanTint(id).ShouldBe(0);
                page.GetSpanPieces(id).All(p => p.Underline.Opacity == 0).ShouldBeTrue();
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Replacing_The_Document_Drops_The_Old_Spans()
    {
        var (window, page) = Show(ExtractionFixtures.Invoice());
        try
        {
            page.SetSpan("vendor", s_linked);
            page.Document = ExtractionFixtures.Email();
            Settle(window);
            page.SpanIds.ShouldBe(SpanIdsOf(ExtractionFixtures.Email()));
            page.TryGetSpanRects("vendor", out _).ShouldBeFalse();
            page.GetSpanPaint("vendor").ShouldBeNull();
            page.GetSpanPaint("total").ShouldBeNull();
            Texts(page).ShouldNotContain("Invoice");
            page.TryGetSpanRects("merchant", out var rects).ShouldBeTrue();
            rects.ShouldNotBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Pointer

    [AvaloniaFact]
    public void Painted_Spans_Report_Hover_And_Press_With_A_Hand_Cursor()
    {
        var (window, page) = Show(ExtractionFixtures.Invoice());
        try
        {
            var log = new List<string>();
            page.SpanPointerEntered += (_, e) => log.Add("enter " + e.SpanId);
            page.SpanPointerExited += (_, e) => log.Add("exit " + e.SpanId);
            page.SpanPressed += (_, e) => log.Add("press " + e.SpanId);
            Point At(string id)
            {
                page.TryGetSpanRects(id, out var rects).ShouldBeTrue();
                return page.TranslatePoint(rects[0].Center, window)!.Value;
            }
            void Move(Point point)
            {
                window.MouseMove(point, RawInputModifiers.None);
                Settle(window);
            }

            // Not painted: plain text.
            Move(At("vendor"));
            log.ShouldBeEmpty();
            page.HoveredSpanId.ShouldBeNull();
            page.Cursor.ShouldBeNull();

            page.SetSpan("vendor", s_linked);
            page.SetSpan("invoiceNumber", s_linked);
            var vendorPoint = At("vendor");
            Move(new Point(vendorPoint.X + 1, vendorPoint.Y));
            log.ShouldBe(new[] { "enter vendor" });
            page.HoveredSpanId.ShouldBe("vendor");
            page.Cursor.ShouldNotBeNull();

            Move(At("invoiceNumber"));
            log.ShouldBe(new[] { "enter vendor", "exit vendor", "enter invoiceNumber" });

            window.MouseDown(At("invoiceNumber"), MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(At("invoiceNumber"), MouseButton.Left, RawInputModifiers.None);
            Settle(window);
            log[^1].ShouldBe("press invoiceNumber");

            // Clearing the paint under the pointer ends the hover.
            page.SetSpan("invoiceNumber", null);
            log[^1].ShouldBe("exit invoiceNumber");
            page.HoveredSpanId.ShouldBeNull();
            page.Cursor.ShouldBeNull();

            page.SetSpan("vendor", s_linked);
            Move(At("vendor"));
            Move(new Point(5, 5));
            log[^1].ShouldBe("exit vendor");
            page.Focusable.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Theme and accessibility

    [AvaloniaFact]
    public async Task The_Paper_Stays_White_In_The_Dark_Theme_And_The_Tints_Follow_The_Theme()
    {
        var (window, page) = Show(ExtractionFixtures.Invoice());
        try
        {
            page.SetSpan("vendor", s_linked);
            var light = PromptTone.SeriesColor(page, 1);
            await TestHelpers.ApplyAsync(TestHelpers.Dark);
            try
            {
                Settle(window);
                TestHelpers.ColorOf(TestHelpers.Descendant<Border>(page, "PART_Shadow").Background).ShouldBe(Colors.White);
                TestHelpers.ColorOf(TestHelpers.Descendant<Border>(page, "PART_Hairline").Background).ShouldBe(Colors.White);
                var vendor = page.GetSpanPieces("vendor").Single();
                TestHelpers.ColorOf(vendor.Text.Foreground).ShouldBe(DocumentInk.Ink);
                var dark = PromptTone.SeriesColor(page, 1);
                dark.ShouldNotBe(light);
                TestHelpers.ColorOf(vendor.Background.Background).ShouldBe(dark);
            }
            finally
            {
                await TestHelpers.ApplyAsync(null);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Page_Is_A_Document_Named_By_ArticleName_Whose_Text_Is_Readable()
    {
        var (window, page) = Show(ExtractionFixtures.Invoice());
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(page);
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Document);
            peer.GetName().ShouldBe("oakridge-invoice-1187.pdf");
            var names = new List<string>();
            void Walk(AutomationPeer node)
            {
                foreach (var child in node.GetChildren())
                {
                    if (child.GetAutomationControlType() == AutomationControlType.Text)
                    {
                        names.Add(child.GetName());
                    }
                    Walk(child);
                }
            }
            Walk(peer);
            names.ShouldContain("Oakridge Ceramics Ltd");
            names.ShouldContain("Invoice");
            names.ShouldContain("EUR 2,686.20");

            page.ArticleName = "renamed.pdf";
            peer.GetName().ShouldBe("renamed.pdf");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
