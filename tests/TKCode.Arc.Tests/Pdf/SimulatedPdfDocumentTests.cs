using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Shouldly;

namespace TKCode.Arc.Tests.Pdf;

// Pixels are checked in TKCode.Arc.Pdf.Native.Tests (SimulatedPdfDocumentPixelTests): this project has no Skia, and the
// headless stub bitmap keeps no pixels, so CopyPixels here reads freed memory.
public class SimulatedPdfDocumentTests
{
    [AvaloniaFact]
    public async Task Text_Is_Real_And_Differs_By_Page()
    {
        using var document = new SimulatedPdfDocument();
        var first = await document.GetPageTextAsync(0, default);
        var second = await document.GetPageTextAsync(1, default);
        first.Text.ShouldStartWith("Page 1\n");
        second.Text.ShouldStartWith("Page 2\n");
        first.Text.ShouldNotBe(second.Text);
        first.CharBoxes.Count.ShouldBe(first.Text.Length);
        first.GetRects(0, 6).Count.ShouldBe(1);
        // The same document built again says the same.
        using var again = new SimulatedPdfDocument();
        (await again.GetPageTextAsync(1, default)).Text.ShouldBe(second.Text);
    }

    [AvaloniaFact]
    public async Task Has_A_Fixed_Outline_And_One_Link_Of_Each_Kind_That_Stay_In_Range()
    {
        using var document = new SimulatedPdfDocument(2);
        var outline = await document.GetOutlineAsync(default);
        outline.Count.ShouldBe(3);
        outline[1].Children.Count.ShouldBe(2);
        foreach (var item in outline.Concat(outline.SelectMany(o => o.Children)))
        {
            if (item.Destination is { } destination)
            {
                destination.PageIndex.ShouldBeInRange(0, 1);
            }
        }

        var links = await document.GetLinksAsync(0, default);
        links.Count.ShouldBe(2);
        links.Count(l => l.Destination is not null).ShouldBe(1);
        links.Single(l => l.Uri is not null).Uri!.Scheme.ShouldBe("https");
        var text = await document.GetPageTextAsync(0, default);
        var at = text.HitTest(links[0].Bounds.Center);
        links[0].Bounds.Inflate(1).Contains(text.CharBoxes[at]).ShouldBeTrue();
        (await document.GetLinksAsync(1, default)).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task After_Dispose_Calls_End_As_Cancelled()
    {
        var document = new SimulatedPdfDocument();
        document.Dispose();
        document.IsDisposed.ShouldBeTrue();
        await Should.ThrowAsync<OperationCanceledException>(() => document.GetPageTextAsync(0, default));
        await Should.ThrowAsync<OperationCanceledException>(() => document.RenderAsync(new PageRenderRequest(0, new PixelSize(10, 10), null), default).AsTask());
    }

    [AvaloniaFact]
    public void Honors_The_Page_Size()
    {
        using var document = new SimulatedPdfDocument(1, new Size(300, 400));
        document.GetPageSize(0).ShouldBe(new Size(300, 400));
    }
}
