using Avalonia;

namespace TKCode.Arc.Tests.Media;

/// <summary>The pure page layout (spec section 14).</summary>
public class PageViewerLayoutTests
{
    private static PageLayoutEngine Engine(int count, Size size, double zoom = 1, double spacing = 10, double padding = 20)
    {
        var engine = new PageLayoutEngine { Zoom = zoom, Spacing = spacing, Padding = padding };
        engine.SetPages(count, _ => size);
        return engine;
    }

    [Fact]
    public void Visible_Range_Is_The_Pages_In_The_Band()
    {
        // Page tops: 20, 130, 240, ...; each 100 tall with a 10 gap.
        var engine = Engine(10, new Size(50, 100));
        engine.GetVisibleRange(0, 100, 100).ShouldBe((0, 0));
        engine.GetVisibleRange(0, 140, 100).ShouldBe((0, 1));
        engine.GetVisibleRange(125, 235, 100).ShouldBe((1, 1));
        engine.GetVisibleRange(235, 245, 100).ShouldBe((2, 2));
        engine.GetVisibleRange(10_000, 11_000, 100).First.ShouldBeGreaterThan(engine.GetVisibleRange(10_000, 11_000, 100).Last);
    }

    [Fact]
    public void A_Band_Inside_A_Gap_Has_No_Pages()
    {
        var engine = Engine(3, new Size(50, 100));
        var (first, last) = engine.GetVisibleRange(121, 129, 100);
        last.ShouldBeLessThan(first);
    }

    [Fact]
    public void Zoom_Scales_The_Pages_But_Not_The_Gaps_Or_The_Canvas()
    {
        var engine = Engine(3, new Size(50, 100), zoom: 2);
        var viewport = new Size(500, 100);
        engine.GetPageRect(1, viewport).ShouldBe(new Rect(200, 230, 100, 200));
        engine.ContentSize.ShouldBe(new Size(140, 660));
    }

    [Fact]
    public void Pages_Of_Different_Widths_Are_Centered_And_The_Widest_Sets_The_Extent()
    {
        var engine = new PageLayoutEngine { Spacing = 0, Padding = 10 };
        engine.SetPages(2, i => i == 0 ? new Size(100, 50) : new Size(300, 50));
        engine.ContentSize.Width.ShouldBe(320);
        var viewport = new Size(200, 200);
        // Narrower viewport: the content sets the column, both pages center inside it.
        engine.GetPageRect(0, viewport).X.ShouldBe(110);
        engine.GetPageRect(1, viewport).X.ShouldBe(10);
        // Wider viewport: the pages center in the viewport.
        engine.GetPageRect(0, new Size(500, 200)).X.ShouldBe(200);
    }

    [Fact]
    public void Short_Content_Centers_Vertically()
    {
        var engine = Engine(1, new Size(50, 100));
        // Content is 140 tall in a 340 viewport.
        engine.GetPageRect(0, new Size(300, 340)).Y.ShouldBe(120);
    }

    [Fact]
    public void Single_Lays_Out_Only_The_Current_Page()
    {
        var engine = new PageLayoutEngine { Spacing = 10, Padding = 20, Mode = PageLayoutMode.Single, SingleIndex = 1 };
        engine.SetPages(3, i => i == 1 ? new Size(200, 400) : new Size(50, 50));
        engine.ContentSize.ShouldBe(new Size(240, 440));
        engine.GetVisibleRange(0, 10, 100).ShouldBe((1, 1));
        engine.GetPageRect(1, new Size(100, 100)).ShouldBe(new Rect(20, 20, 200, 400));
        engine.PageIndexAt(10_000, 100).ShouldBe(1);
    }

    [Fact]
    public void Queries_Scale_To_A_Hundred_Thousand_Pages()
    {
        var engine = Engine(100_000, new Size(600, 800));
        engine.Zoom = 1.25;
        var top = engine.GetPageRect(77_777, new Size(1000, 1000)).Y;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 100_000; i++)
        {
            engine.GetVisibleRange(top + i % 7, top + 500 + i % 7, 1000);
        }
        watch.Stop();
        engine.GetVisibleRange(top + 1, top + 500, 1000).ShouldBe((77_777, 77_777));
        // A linear scan would take minutes.
        watch.ElapsedMilliseconds.ShouldBeLessThan(2000);
    }

    [Fact]
    public void Page_At_A_Height_Picks_The_Closer_Page_In_A_Gap()
    {
        var engine = Engine(3, new Size(50, 100));
        engine.PageIndexAt(0, 100).ShouldBe(0);
        engine.PageIndexAt(121, 100).ShouldBe(0);
        engine.PageIndexAt(127, 100).ShouldBe(1);
        engine.PageIndexAt(99_999, 100).ShouldBe(2);
    }

    [Fact]
    public void Hit_Test_Finds_Page_Points_And_Misses_The_Canvas()
    {
        var engine = Engine(3, new Size(100, 100), zoom: 2);
        var viewport = new Size(400, 200);
        engine.HitTest(new Point(200, 120), viewport, out var page, out var point).ShouldBeTrue();
        page.ShouldBe(0);
        point.ShouldBe(new Point(50, 50));
        engine.HitTest(new Point(5, 120), viewport, out _, out _).ShouldBeFalse();
        engine.HitTest(new Point(200, 225), viewport, out _, out _).ShouldBeFalse();
    }

    [Fact]
    public void Fit_Zoom_Follows_The_Mode()
    {
        var engine = Engine(3, new Size(100, 200), padding: 10);
        var viewport = new Size(320, 220);
        engine.GetFitZoom(PageFitMode.Width, viewport).ShouldBe(3);
        engine.GetFitZoom(PageFitMode.Height, viewport).ShouldBe(1);
        engine.GetFitZoom(PageFitMode.Page, viewport).ShouldBe(1);
        engine.GetFitZoom(PageFitMode.None, viewport).ShouldBeNull();
        engine.GetFitZoom(PageFitMode.Width, new Size(10, 10)).ShouldBeNull();
    }

    [Fact]
    public void Fit_Uses_The_Current_Page_In_Single_And_The_Largest_Page_In_Continuous()
    {
        var engine = new PageLayoutEngine { Padding = 0 };
        engine.SetPages(2, i => i == 0 ? new Size(100, 100) : new Size(200, 100));
        engine.GetFitZoom(PageFitMode.Width, new Size(400, 400)).ShouldBe(2);
        engine.Mode = PageLayoutMode.Single;
        engine.GetFitZoom(PageFitMode.Width, new Size(400, 400)).ShouldBe(4);
    }

    [Theory]
    [InlineData(0.05, 0.1)]
    [InlineData(0.1, 0.25)]
    [InlineData(0.9, 1)]
    [InlineData(1, 1.25)]
    [InlineData(1.3, 1.5)]
    [InlineData(6, 8)]
    [InlineData(8, 8)]
    [InlineData(20, 8)]
    public void Next_Zoom_Step(double zoom, double expected) => PageLayoutEngine.NextZoomStep(zoom).ShouldBe(expected);

    [Theory]
    [InlineData(0.05, 0.1)]
    [InlineData(0.1, 0.1)]
    [InlineData(0.9, 0.75)]
    [InlineData(1, 0.75)]
    [InlineData(8, 6)]
    [InlineData(20, 8)]
    public void Previous_Zoom_Step(double zoom, double expected) => PageLayoutEngine.PreviousZoomStep(zoom).ShouldBe(expected);

    [Fact]
    public void Base_Size_Is_Capped_At_Sixteen_Megapixels_With_The_Same_Aspect()
    {
        PageRenderScheduler.CapSize(new PixelSize(4000, 4000)).ShouldBe(new PixelSize(4000, 4000));
        var capped = PageRenderScheduler.CapSize(new PixelSize(8000, 8000));
        ((long)capped.Width * capped.Height).ShouldBeLessThanOrEqualTo(PageRenderScheduler.MaxBasePixels);
        capped.Width.ShouldBe(capped.Height);
        capped.Width.ShouldBeGreaterThan(3990);
    }
}
