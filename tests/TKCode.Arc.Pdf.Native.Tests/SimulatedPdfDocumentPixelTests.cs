using Avalonia;
using Shouldly;
using Xunit;

namespace TKCode.Arc.Pdf.Native.Tests;

/// <summary>
/// The simulated document's pixels. Here and not in TKCode.Arc.Tests because they need real Skia bitmaps: the headless stub
/// bitmap keeps no pixels, so reading one back there returns freed memory.
/// </summary>
public sealed class SimulatedPdfDocumentPixelTests
{
    private static readonly (byte B, byte G, byte R, byte A) s_paper = (255, 255, 255, 255);

    [Fact]
    public async Task Rendering_Is_Repeatable_And_The_Ink_Is_Where_The_Boxes_Are()
    {
        Pdfium.EnsureAvalonia();
        using var document = new SimulatedPdfDocument();
        var page = document.GetPageSize(1);
        var target = new PixelSize((int)page.Width, (int)page.Height);
        var request = new PageRenderRequest(1, target, null);
        using var first = (await document.RenderAsync(request, default))!.Bitmap;
        using var second = (await document.RenderAsync(request, default))!.Bitmap;
        first.PixelSize.ShouldBe(target);
        var pixels = Pixels.Of(first);
        var again = Pixels.Of(second);
        for (var y = 0; y < target.Height; y++)
        {
            for (var x = 0; x < target.Width; x++)
            {
                if (pixels.At(x, y) != again.At(x, y))
                {
                    Assert.Fail($"The two renders differ at {x},{y}.");
                }
            }
        }

        var text = await document.GetPageTextAsync(1, default);
        var inked = 0;
        for (var i = 0; i < text.Text.Length; i++)
        {
            var box = text.CharBoxes[i];
            if (box.Width > 0)
            {
                var center = box.Center;
                pixels.At((int)center.X, (int)center.Y).ShouldNotBe(s_paper, $"No ink under character {i}.");
                inked++;
            }
        }
        inked.ShouldBeGreaterThan(100);
        pixels.At(0, 0).ShouldBe(s_paper);
    }

    [Fact]
    public async Task A_Region_Is_That_Part_Of_The_Whole_Page()
    {
        Pdfium.EnsureAvalonia();
        using var document = new SimulatedPdfDocument(3);
        var target = new PixelSize(816, 1056);
        var region = new PixelRect(70, 70, 200, 50);
        using var whole = (await document.RenderAsync(new PageRenderRequest(0, target, null), default))!.Bitmap;
        using var part = (await document.RenderAsync(new PageRenderRequest(0, target, region), default))!.Bitmap;
        part.PixelSize.ShouldBe(region.Size);
        var wholePixels = Pixels.Of(whole);
        var partPixels = Pixels.Of(part);
        var inked = 0;
        for (var y = 0; y < region.Height; y++)
        {
            for (var x = 0; x < region.Width; x++)
            {
                var expected = wholePixels.At(x + region.X, y + region.Y);
                if (partPixels.At(x, y) != expected)
                {
                    Assert.Fail($"The region differs from the page at {x},{y}.");
                }
                if (expected != s_paper)
                {
                    inked++;
                }
            }
        }
        // The region has to contain ink, or matching white paper proves nothing.
        inked.ShouldBeGreaterThan(0);
    }
}
