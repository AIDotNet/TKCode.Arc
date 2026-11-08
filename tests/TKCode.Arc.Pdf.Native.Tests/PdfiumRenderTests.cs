using Avalonia;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Pdf.Native.Tests;

/// <summary>Bitmaps: real pixels, regions, odd requests.</summary>
public sealed class PdfiumRenderTests
{
    [Fact]
    public async Task A_page_renders_opaque_white_paper_with_ink()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        var pixels = await Pdfium.RenderAsync(document, 2);
        pixels.Width.ShouldBe(793);
        pixels.Height.ShouldBe(1123);
        pixels.At(1, 1).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
        var ink = pixels.CountInk(new PixelRect(0, 0, pixels.Width, pixels.Height));
        ink.ShouldBeGreaterThan(1000);
        ink.ShouldBeLessThan(pixels.Width * pixels.Height / 2);
        pixels.At(pixels.InkBounds().X + 2, pixels.InkBounds().Y + 2).A.ShouldBe((byte)255);
    }

    [Fact]
    public async Task The_cover_band_has_its_color_channels_in_the_right_order()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        var (b, g, r, a) = (await Pdfium.RenderAsync(document, 0)).At(20, 20);
        a.ShouldBe((byte)255);
        // The band is mostly blue in make-sample.py: (0.16, 0.33, 0.85).
        b.ShouldBeGreaterThan(g);
        g.ShouldBeGreaterThan(r);
        ((int)b).ShouldBeInRange(205, 225);
    }

    [Theory]
    [InlineData(40, 60, 300, 200)]
    [InlineData(0, 0, 100, 100)]
    [InlineData(200, 800, 400, 300)]
    public async Task A_region_matches_the_same_area_of_the_full_render(int x, int y, int width, int height)
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        var full = await Pdfium.RenderAsync(document, 2);
        var part = await Pdfium.RenderAsync(document, 2, new PixelRect(x, y, width, height));
        part.Width.ShouldBe(width);
        part.Height.ShouldBe(height);
        var differing = 0;
        for (var py = 0; py < height; py++)
        {
            for (var px = 0; px < width; px++)
            {
                var (b1, g1, r1, _) = part.At(px, py);
                var (b2, g2, r2, _) = full.At(x + px, y + py);
                if (Math.Abs(b1 - b2) > 2 || Math.Abs(g1 - g2) > 2 || Math.Abs(r1 - r2) > 2)
                {
                    differing++;
                }
            }
        }
        differing.ShouldBe(0);
    }

    [Fact]
    public async Task A_region_shows_detail_at_a_scale_too_large_for_a_whole_page()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Rotated);
        // 16 x: the black square of page 0, (300, 450)-(350, 500) in points, is around (6400, 9600) in pixels. The whole page would be 136 MP.
        var part = await Pdfium.RenderAsync(document, 0, new PixelRect(6500, 9700, 200, 200), scale: 16);
        part.At(100, 100).ShouldBe(((byte)0, (byte)0, (byte)0, (byte)255));
    }

    [Fact]
    public async Task A_region_is_clamped_to_the_page_and_nothing_is_no_image()
    {
        using var document = await Pdfium.OpenAsync(Pdfium.Sample);
        var page = document.GetPageSize(2);
        var target = new PixelSize((int)page.Width, (int)page.Height);
        var clamped = await document.RenderAsync(new PageRenderRequest(2, target, new PixelRect(target.Width - 10, target.Height - 10, 100, 100)), CancellationToken.None);
        clamped!.Bitmap.PixelSize.ShouldBe(new PixelSize(10, 10));
        (await document.RenderAsync(new PageRenderRequest(2, target, new PixelRect(target.Width + 5, 0, 10, 10)), CancellationToken.None)).ShouldBeNull();
        (await document.RenderAsync(new PageRenderRequest(2, new PixelSize(0, 0), null), CancellationToken.None)).ShouldBeNull();
        await Should.ThrowAsync<ArgumentOutOfRangeException>(document.RenderAsync(new PageRenderRequest(12, target, null), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Pages_beyond_the_handle_cache_keep_rendering()
    {
        using var document = await Pdfium.OpenAsync(PdfSource.FromBytes(GeneratedPdf.Build(40)));
        document.PageCount.ShouldBe(40);
        for (var round = 0; round < 2; round++)
        {
            for (var page = 0; page < document.PageCount; page++)
            {
                (await Pdfium.RenderAsync(document, page, scale: 0.25)).InkBounds().Width.ShouldBeGreaterThan(0);
            }
        }
    }
}
