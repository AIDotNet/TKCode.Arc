using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Media;

/// <summary>The render pipeline (spec section 4), driven through the control with a counting source.</summary>
public class PageViewerSchedulerTests
{
    private const int FastSettleMs = 15;

    private static (PageViewer Viewer, PageViewerPresenter Presenter, Window Window) Show(IPageSource source, PageFitMode fit = PageFitMode.None, Action<PageViewer>? configure = null)
    {
        var viewer = new PageViewer { PageSource = source, FitMode = fit };
        viewer.Scheduler.SettleDelay = TimeSpan.FromMilliseconds(FastSettleMs);
        configure?.Invoke(viewer);
        var window = TestHelpers.Show(viewer);
        Dispatcher.UIThread.RunJobs();
        return (viewer, TestHelpers.Descendant<PageViewerPresenter>(viewer), window);
    }

    private static void Drain() => TestHelpers.PumpUntil(() => false, 2 * FastSettleMs + 40);

    [AvaloniaFact]
    public void Settle_Delay_Is_120_Ms_By_Spec()
    {
        new PageViewer().Scheduler.SettleDelay.ShouldBe(TimeSpan.FromMilliseconds(120));
    }

    [AvaloniaFact]
    public void Only_The_Visible_Pages_And_One_On_Each_Side_Are_Rendered()
    {
        // 300 tall pages: page 3 and a part of page 4 are on screen.
        var source = new FakePageSource(20, new Size(200, 300));
        var (viewer, presenter, window) = Show(source);
        try
        {
            ((IScrollable)presenter).Offset = new Vector(0, 3 * 312);
            source.Requests.Clear();
            viewer.Scheduler.Reset();
            presenter.OnLayoutChanged();
            Dispatcher.UIThread.RunJobs();
            source.Requests.Select(r => r.PageIndex).OrderBy(i => i).ShouldBe([2, 3, 4]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Requested_Size_Is_Page_Size_Times_Zoom()
    {
        var source = new FakePageSource(1, new Size(200, 300));
        var (viewer, _, window) = Show(source);
        try
        {
            source.Requests[0].TargetSize.ShouldBe(new PixelSize(200, 300));
            source.Requests[0].Region.ShouldBeNull();
            viewer.Zoom = 1.5;
            TestHelpers.PumpUntil(() => source.Requests.Count > 1).ShouldBeTrue();
            source.Requests[^1].TargetSize.ShouldBe(new PixelSize(300, 450));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Requested_Size_Includes_The_Render_Scale()
    {
        var source = new FakePageSource(1, new Size(100, 100));
        var viewer = new PageViewer { PageSource = source, FitMode = PageFitMode.None };
        viewer.Scheduler.SettleDelay = TimeSpan.FromMilliseconds(FastSettleMs);
        var window = TestHelpers.Show(viewer);
        try
        {
            window.SetRenderScaling(2);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.PumpUntil(() => source.Requests.Any(r => r.TargetSize == new PixelSize(200, 200))).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Nearest_To_The_Viewport_Center_Goes_First()
    {
        var source = new FakePageSource(20, new Size(200, 80)) { Hold = true };
        var (viewer, presenter, window) = Show(source);
        try
        {
            // Everything from page 0 on is on screen; a running slot frees at a time, so the queue decides the order.
            source.Hold = true;
            source.Requests.Clear();
            viewer.Scheduler.Reset();
            ((IScrollable)presenter).Offset = new Vector(0, 5 * 92);
            Dispatcher.UIThread.RunJobs();
            var firstPages = source.Requests.Take(3).Select(r => r.PageIndex).ToList();
            var center = (presenter.Offset.Y + 126 - 8) / 92;
            var order = firstPages.Select(p => Math.Abs(p + 0.4 - center)).ToList();
            order.ShouldBe(order.OrderBy(d => d).ToList());
        }
        finally
        {
            source.Hold = false;
            source.Release();
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Nothing_Is_Requested_While_The_Zoom_Moves_And_The_Old_Bitmap_Stretches()
    {
        var source = new FakePageSource(1, new Size(200, 300));
        var (viewer, _, window) = Show(source);
        try
        {
            var before = source.Requests.Count;
            viewer.Zoom = 1.1;
            viewer.Zoom = 1.2;
            viewer.Zoom = 1.3;
            Dispatcher.UIThread.RunJobs();
            source.Requests.Count.ShouldBe(before);
            viewer.Scheduler.GetBitmaps(0, out var stretched, out _);
            stretched.ShouldNotBeNull();
            stretched.TargetSize.ShouldBe(new PixelSize(200, 300));
            TestHelpers.PumpUntil(() => source.Requests.Count > before).ShouldBeTrue();
            source.Requests.Count.ShouldBe(before + 1);
            source.Requests[^1].TargetSize.ShouldBe(new PixelSize(260, 390));
            viewer.Scheduler.GetBitmaps(0, out var sharp, out _);
            sharp!.TargetSize.ShouldBe(new PixelSize(260, 390));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Over_Sixteen_Megapixels_The_Base_Is_Capped_And_A_Viewport_Region_Follows()
    {
        // At zoom 3 the page is 6000 × 6000 = 36 MP.
        var source = new FakePageSource(1, new Size(2000, 2000));
        var (viewer, presenter, window) = Show(source, configure: v => v.Zoom = 3);
        try
        {
            TestHelpers.PumpUntil(() => source.Requests.Any(r => r.Region is not null)).ShouldBeTrue();
            var whole = source.Requests.First(r => r.Region is null);
            ((long)whole.TargetSize.Width * whole.TargetSize.Height).ShouldBeLessThanOrEqualTo(PageRenderScheduler.MaxBasePixels);
            var detail = source.Requests.First(r => r.Region is not null);
            detail.TargetSize.ShouldBe(new PixelSize(6000, 6000));
            var region = detail.Region!.Value;
            // The viewport is 400 × 252, plus 25% on each side, on a 64 pixel grid.
            region.Width.ShouldBeLessThanOrEqualTo(400 * 3 / 2 + 2 * 64);
            region.Height.ShouldBeLessThanOrEqualTo(252 * 3 / 2 + 2 * 64);
            region.Width.ShouldBeGreaterThanOrEqualTo(400);
            viewer.Scheduler.GetBitmaps(0, out var baseEntry, out var detailEntry);
            baseEntry.ShouldNotBeNull();
            detailEntry.ShouldNotBeNull();
            detailEntry.Region.ShouldBe(region);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Detail_Waits_Until_Scrolling_Rests()
    {
        var source = new FakePageSource(1, new Size(2000, 2000));
        var (viewer, presenter, window) = Show(source, configure: v => v.Zoom = 3);
        try
        {
            TestHelpers.PumpUntil(() => source.Requests.Any(r => r.Region is not null)).ShouldBeTrue();
            var details = source.Requests.Count(r => r.Region is not null);
            // Far outside the current region, so a new one is needed.
            ((IScrollable)presenter).Offset = new Vector(3000, 3000);
            Dispatcher.UIThread.RunJobs();
            source.Requests.Count(r => r.Region is not null).ShouldBe(details);
            TestHelpers.PumpUntil(() => source.Requests.Count(r => r.Region is not null) > details).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Small_Scroll_Inside_The_Region_Asks_For_No_New_Detail()
    {
        var source = new FakePageSource(1, new Size(2000, 2000));
        var (viewer, presenter, window) = Show(source, configure: v => v.Zoom = 3);
        try
        {
            TestHelpers.PumpUntil(() => source.Requests.Any(r => r.Region is not null)).ShouldBeTrue();
            ((IScrollable)presenter).Offset = new Vector(1000, 1000);
            TestHelpers.PumpUntil(() => source.Requests.Count(r => r.Region is not null) > 1).ShouldBeTrue();
            var count = source.Requests.Count;
            ((IScrollable)presenter).Offset = new Vector(1005, 1005);
            Drain();
            source.Requests.Count.ShouldBe(count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Picture_That_Holds_Every_Pixel_Needs_No_Detail()
    {
        var picture = new TrackedBitmap(new PixelSize(32, 32));
        var viewer = new PageViewer { ItemsSource = new[] { picture }, FitMode = PageFitMode.None };
        viewer.Scheduler.SettleDelay = TimeSpan.FromMilliseconds(FastSettleMs);
        var window = TestHelpers.Show(viewer);
        try
        {
            Dispatcher.UIThread.RunJobs();
            viewer.Scheduler.GetBitmaps(0, out var entry, out _);
            entry.ShouldNotBeNull();
            entry.Bitmap.ShouldBeSameAs(picture);
            entry.OwnsBitmap.ShouldBeFalse();
            viewer.Scheduler.CachedBytes.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
        // Closing the window drops the cache; a picture of the app stays with the app.
        TestHelpers.PumpUntil(() => false, 20);
        picture.IsDisposed.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_Request_Not_Yet_Started_Is_Dropped_When_It_Leaves_The_Target()
    {
        var source = new FakePageSource(200, new Size(200, 60)) { Hold = true };
        var (viewer, presenter, window) = Show(source);
        try
        {
            viewer.Scheduler.RunningCount.ShouldBe(4);
            viewer.Scheduler.QueuedCount.ShouldBeGreaterThan(0);
            var started = source.Requests.Count;
            viewer.GoToPage(150, animate: false);
            Dispatcher.UIThread.RunJobs();
            // The old queue is gone; the first requests of the new view start as the cancelled ones finish.
            source.Hold = false;
            source.Release();
            TestHelpers.PumpUntil(() => viewer.Scheduler.RunningCount == 0 && viewer.Scheduler.QueuedCount == 0).ShouldBeTrue();
            source.Requests.Skip(started).ShouldAllBe(r => r.PageIndex >= 140);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Running_Request_Is_Cancelled_When_It_Leaves_The_Target_And_Its_Bitmap_Is_Not_Kept()
    {
        var source = new FakePageSource(100, new Size(200, 300)) { Hold = true };
        var (viewer, _, window) = Show(source);
        try
        {
            source.Requests.ShouldContain(r => r.PageIndex == 0);
            viewer.GoToPage(80, animate: false);
            TestHelpers.PumpUntil(() => source.Cancelled.Any(r => r.PageIndex == 0)).ShouldBeTrue();
            viewer.Scheduler.GetBitmaps(0, out var entry, out _);
            entry.ShouldBeNull();
        }
        finally
        {
            source.Hold = false;
            source.Release();
            window.Close();
        }
    }

    /// <summary>A source that ignores cancellation, to reach the bitmap that arrives too late.</summary>
    private sealed class StubbornSource : IPageSource
    {
        private TaskCompletionSource<PageImage?> _gate = new();

        public int PageCount => 100;

        public List<PageRenderRequest> Requests { get; } = new();

        public event EventHandler? PagesChanged { add { } remove { } }

        public Size GetPageSize(int pageIndex) => new(200, 300);

        public int MaxRunning { get; private set; }

        private readonly Dictionary<int, int> _running = new();

        public async ValueTask<PageImage?> RenderAsync(PageRenderRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            _running[request.PageIndex] = _running.GetValueOrDefault(request.PageIndex) + 1;
            MaxRunning = Math.Max(MaxRunning, _running[request.PageIndex]);
            var result = await _gate.Task;
            _running[request.PageIndex]--;
            return result;
        }

        public void Finish(PageImage? image)
        {
            var gate = _gate;
            _gate = new TaskCompletionSource<PageImage?>();
            gate.SetResult(image);
        }
    }

    [AvaloniaFact]
    public void A_Bitmap_That_Arrives_After_Cancellation_Is_Disposed()
    {
        var source = new StubbornSource();
        var (viewer, _, window) = Show(source);
        try
        {
            viewer.GoToPage(80, animate: false);
            Dispatcher.UIThread.RunJobs();
            var late = new TrackedBitmap(new PixelSize(4, 4));
            source.Finish(new PageImage(late));
            TestHelpers.PumpUntil(() => late.IsDisposed).ShouldBeTrue();
            viewer.Scheduler.GetBitmaps(0, out var entry, out _);
            entry.ShouldBeNull();
        }
        finally
        {
            source.Finish(null);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Page_Runs_One_Request_At_A_Time()
    {
        var source = new StubbornSource();
        var (viewer, _, window) = Show(source);
        try
        {
            var first = source.Requests.Count(r => r.PageIndex == 0);
            first.ShouldBe(1);
            viewer.Zoom = 1.5;
            TestHelpers.PumpUntil(() => false, 3 * FastSettleMs);
            // The new size waits for the old request to end.
            source.Requests.Count(r => r.PageIndex == 0).ShouldBe(1);
            source.Finish(null);
            TestHelpers.PumpUntil(() => source.Requests.Count(r => r.PageIndex == 0) == 2).ShouldBeTrue();
            source.Requests.Last(r => r.PageIndex == 0).TargetSize.ShouldBe(new PixelSize(300, 450));
            source.MaxRunning.ShouldBe(1);
        }
        finally
        {
            source.Finish(null);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Cache_Stays_Within_Its_Budget_And_Disposes_What_It_Drops_After_The_Frame()
    {
        // Each owned bitmap is 300 × 300 × 4 = 360 KB; the budget holds about three.
        var source = new FakePageSource(50, new Size(300, 300)) { MatchTargetSize = true };
        var (viewer, presenter, window) = Show(source, configure: v => v.RenderCacheBytes = 1_200_000);
        try
        {
            for (var page = 0; page < 40; page += 3)
            {
                viewer.GoToPage(page, animate: false);
                TestHelpers.PumpUntil(() => viewer.Scheduler.RunningCount == 0 && viewer.Scheduler.QueuedCount == 0).ShouldBeTrue();
            }
            viewer.Scheduler.CachedBytes.ShouldBeLessThanOrEqualTo(1_200_000);
            TestHelpers.PumpUntil(() => source.Bitmaps.Any(b => b.IsDisposed)).ShouldBeTrue();
            source.Bitmaps.Count(b => !b.IsDisposed).ShouldBe(viewer.Scheduler.CachedCount);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dropped_Bitmaps_Live_Until_The_Dispatcher_Reaches_Background_Priority()
    {
        var source = new FakePageSource(5, new Size(100, 100)) { MatchTargetSize = true };
        var (viewer, _, window) = Show(source);
        try
        {
            var bitmap = source.Bitmaps[0];
            viewer.Scheduler.Reset();
            bitmap.IsDisposed.ShouldBeFalse();
            Dispatcher.UIThread.RunJobs();
            bitmap.IsDisposed.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Visible_Pages_Are_Never_Evicted()
    {
        var source = new FakePageSource(3, new Size(300, 300)) { MatchTargetSize = true };
        var (viewer, _, window) = Show(source, configure: v => v.RenderCacheBytes = 1);
        try
        {
            TestHelpers.PumpUntil(() => viewer.Scheduler.RunningCount == 0).ShouldBeTrue();
            viewer.Scheduler.GetBitmaps(0, out var entry, out _);
            entry.ShouldNotBeNull();
            entry.Bitmap.PixelSize.ShouldBe(new PixelSize(300, 300));
            // The neighbour would not fit the budget, so it is not even asked for.
            source.Requests.ShouldAllBe(r => r.PageIndex == 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrolling_Back_Is_Served_From_The_Cache()
    {
        var source = new FakePageSource(30, new Size(200, 300));
        var (viewer, _, window) = Show(source);
        try
        {
            viewer.GoToPage(10, animate: false);
            TestHelpers.PumpUntil(() => viewer.Scheduler.RunningCount == 0).ShouldBeTrue();
            var count = source.Requests.Count;
            viewer.GoToPage(0, animate: false);
            Drain();
            source.Requests.Count.ShouldBe(count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Is_Loading_Tracks_The_First_Bitmap_Of_The_Visible_Pages()
    {
        var source = new FakePageSource(3, new Size(200, 300)) { Hold = true };
        var (viewer, _, window) = Show(source);
        try
        {
            viewer.IsLoading.ShouldBeTrue();
            viewer.Classes.Contains(":loading").ShouldBeTrue();
            source.Hold = false;
            source.Release();
            TestHelpers.PumpUntil(() => !viewer.IsLoading).ShouldBeTrue();
            viewer.Classes.Contains(":loading").ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Null_Page_Keeps_The_Placeholder_And_Is_Not_Asked_For_Again()
    {
        var source = new FakePageSource(1, new Size(200, 300)) { ReturnsNull = true };
        var (viewer, presenter, window) = Show(source);
        try
        {
            viewer.IsLoading.ShouldBeFalse();
            viewer.Scheduler.GetBitmaps(0, out var entry, out _);
            entry.ShouldBeNull();
            var count = source.Requests.Count;
            ((IScrollable)presenter).Offset = new Vector(0, 5);
            Drain();
            source.Requests.Count.ShouldBe(count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Page_That_Fails_Keeps_The_Placeholder_And_Is_Not_Asked_For_Again()
    {
        var source = new FakePageSource(1, new Size(200, 300)) { Throws = true };
        var (viewer, presenter, window) = Show(source);
        try
        {
            viewer.IsLoading.ShouldBeFalse();
            var count = source.Requests.Count;
            ((IScrollable)presenter).Offset = new Vector(0, 5);
            Drain();
            source.Requests.Count.ShouldBe(count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pages_Changed_Drops_The_Bitmaps_And_Reads_The_Pages_Again()
    {
        var source = new FakePageSource(3, new Size(200, 300));
        var (viewer, _, window) = Show(source);
        try
        {
            var before = source.Requests.Count;
            source.SetPages(5, new Size(200, 300));
            Dispatcher.UIThread.RunJobs();
            viewer.PageCount.ShouldBe(5);
            source.Requests.Count.ShouldBeGreaterThan(before);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leaving_The_Tree_Cancels_And_Disposes()
    {
        var source = new FakePageSource(3, new Size(100, 100)) { MatchTargetSize = true };
        var (viewer, _, window) = Show(source);
        var bitmaps = source.Bitmaps.ToList();
        bitmaps.ShouldNotBeEmpty();
        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        bitmaps.ShouldAllBe(b => b.IsDisposed);
        viewer.Scheduler.CachedCount.ShouldBe(0);
        window.Content = viewer;
        Dispatcher.UIThread.RunJobs();
        viewer.Scheduler.CachedCount.ShouldBeGreaterThan(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Replacing_The_Source_Drops_The_Old_Bitmaps_And_A_Late_One_Is_Disposed()
    {
        var old = new StubbornSource();
        var (viewer, _, window) = Show(old);
        try
        {
            var next = new FakePageSource(2, new Size(100, 100));
            viewer.PageSource = next;
            Dispatcher.UIThread.RunJobs();
            viewer.PageCount.ShouldBe(2);
            var late = new TrackedBitmap(new PixelSize(4, 4));
            old.Finish(new PageImage(late));
            TestHelpers.PumpUntil(() => late.IsDisposed).ShouldBeTrue();
        }
        finally
        {
            old.Finish(null);
            window.Close();
        }
    }
}
