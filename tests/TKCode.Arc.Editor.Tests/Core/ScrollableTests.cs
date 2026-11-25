// Ported from vscode (src/vs/base/test/common/scrollable.test.ts, commit b57d5c9e), MIT license.
// The suite 'SmoothScrollingOperation' is SmoothScrollingOperationTests. ScrollableTests below is not upstream: it checks
// the clamping of ScrollState and that smooth scrolling runs on the frames the host schedules.

using Shouldly;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class SmoothScrollingOperationTests
{
    private const double VIEWPORT_HEIGHT = 800;
    private const double ANIMATION_DURATION = 125;
    private const double LINE_HEIGHT = 20;

    private sealed record Position(double ScrollLeft, double ScrollTop, double Width, double Height) : ISmoothScrollPosition;

    private sealed class TestSmoothScrollingOperation(double from, double to, double viewportSize, double startTime, double duration)
        : SmoothScrollingOperation(
            new Position(0, from, 0, viewportSize),
            new Position(0, to, 0, viewportSize),
            startTime - 10,
            duration + 10)
    {
        public SmoothScrollingUpdate TestTick(double now) => Tick(now);
    }

    private static (int, int) ExtractLines(TestSmoothScrollingOperation scrollable, double now)
    {
        var scrollTop = scrollable.TestTick(now).ScrollTop;
        var scrollBottom = scrollTop + VIEWPORT_HEIGHT;

        var startLineNumber = (int)Math.Floor(scrollTop / LINE_HEIGHT);
        var endLineNumber = (int)Math.Ceiling(scrollBottom / LINE_HEIGHT);

        return (startLineNumber, endLineNumber);
    }

    private static (int, int)[] SimulateSmoothScroll(double from, double to)
    {
        var scrollable = new TestSmoothScrollingOperation(from, to, VIEWPORT_HEIGHT, 0, ANIMATION_DURATION);

        return
        [
            ExtractLines(scrollable, 0),
            ExtractLines(scrollable, 25),
            ExtractLines(scrollable, 50),
            ExtractLines(scrollable, 75),
            ExtractLines(scrollable, 100),
            ExtractLines(scrollable, 125),
        ];
    }

    private static void AssertSmoothScroll(double from, double to, (int, int)[] expected)
    {
        var actual = SimulateSmoothScroll(from, to);
        actual.ShouldBe(expected);
    }

    [Fact]
    public void Scroll_25_Lines_40_Fit()
    {
        AssertSmoothScroll(0, 500,
        [
            (5, 46),
            (14, 55),
            (20, 61),
            (23, 64),
            (24, 65),
            (25, 65),
        ]);
    }

    [Fact]
    public void Scroll_75_Lines_40_Fit()
    {
        AssertSmoothScroll(0, 1500,
        [
            (15, 56),
            (44, 85),
            (62, 103),
            (71, 112),
            (74, 115),
            (75, 115),
        ]);
    }

    [Fact]
    public void Scroll_100_Lines_40_Fit()
    {
        AssertSmoothScroll(0, 2000,
        [
            (20, 61),
            (59, 100),
            (82, 123),
            (94, 135),
            (99, 140),
            (100, 140),
        ]);
    }

    [Fact]
    public void Scroll_125_Lines_40_Fit()
    {
        AssertSmoothScroll(0, 2500,
        [
            (16, 57),
            (29, 70),
            (107, 148),
            (119, 160),
            (124, 165),
            (125, 165),
        ]);
    }

    [Fact]
    public void Scroll_500_Lines_40_Fit()
    {
        AssertSmoothScroll(0, 10000,
        [
            (16, 57),
            (29, 70),
            (482, 523),
            (494, 535),
            (499, 540),
            (500, 540),
        ]);
    }
}

public sealed class ScrollableTests
{
    /// <summary>A host's frame scheduler: keeps the callbacks until the test runs the next frame.</summary>
    private sealed class FrameScheduler
    {
        public List<Action> Pending { get; } = new();

        public int Cancelled { get; private set; }

        public IDisposable Schedule(Action callback)
        {
            Pending.Add(callback);
            return new Cancellation(this, callback);
        }

        public void RunFrame()
        {
            var callbacks = Pending.ToArray();
            Pending.Clear();
            foreach (var callback in callbacks)
            {
                callback();
            }
        }

        private sealed class Cancellation(FrameScheduler scheduler, Action callback) : IDisposable
        {
            public void Dispose()
            {
                if (scheduler.Pending.Remove(callback))
                {
                    scheduler.Cancelled++;
                }
            }
        }
    }

    [Fact]
    public void ScrollState_Clamps_The_Position_To_The_Dimensions_And_Keeps_The_Raw_One()
    {
        var state = new ScrollState(false, 100, 300, 250, 50, 400, -10);
        state.ScrollLeft.ShouldBe(200);
        state.RawScrollLeft.ShouldBe(250);
        state.ScrollTop.ShouldBe(0);
        state.RawScrollTop.ShouldBe(-10);

        var integers = new ScrollState(true, 100.7, 300.2, 10.9, 50.5, 400.9, 20.4);
        integers.Width.ShouldBe(100);
        integers.ScrollLeft.ShouldBe(10);
        integers.ScrollTop.ShouldBe(20);
    }

    [Fact]
    public void SetScrollPositionNow_Fires_Scroll_With_What_Changed()
    {
        var scheduler = new FrameScheduler();
        using var scrollable = new Scrollable(new ScrollableOptions(true, 125, scheduler.Schedule));
        var events = new List<ScrollEvent>();
        scrollable.Scroll += events.Add;

        scrollable.SetScrollDimensions(new NewScrollDimensions(Width: 100, ScrollWidth: 100, Height: 50, ScrollHeight: 500), false);
        scrollable.SetScrollPositionNow(new NewScrollPosition(ScrollTop: 120));
        scrollable.SetScrollPositionNow(new NewScrollPosition(ScrollTop: 120));

        events.Count.ShouldBe(2);
        events[1].ScrollTopChanged.ShouldBeTrue();
        events[1].OldScrollTop.ShouldBe(0);
        events[1].ScrollTop.ShouldBe(120);
        events[1].HeightChanged.ShouldBeFalse();
        events[1].InSmoothScrolling.ShouldBeFalse();
        scheduler.Pending.ShouldBeEmpty();
    }

    [Fact]
    public void SetScrollPositionSmooth_Runs_On_The_Host_Frames_And_SetScrollPositionNow_Cancels_It()
    {
        var scheduler = new FrameScheduler();
        using var scrollable = new Scrollable(new ScrollableOptions(false, 60_000, scheduler.Schedule));
        var events = new List<ScrollEvent>();
        scrollable.Scroll += events.Add;
        scrollable.SetScrollDimensions(new NewScrollDimensions(Width: 100, ScrollWidth: 100, Height: 50, ScrollHeight: 5000), false);
        events.Clear();

        scrollable.SetScrollPositionSmooth(new NewScrollPosition(ScrollTop: 1000));

        // nothing moves until the host runs a frame
        events.ShouldBeEmpty();
        scrollable.HasPendingScrollAnimation().ShouldBeTrue();
        scrollable.GetFutureScrollPosition().ScrollTop.ShouldBe(1000);
        scheduler.Pending.Count.ShouldBe(1);

        scheduler.RunFrame();

        events.Count.ShouldBe(1);
        events[0].InSmoothScrolling.ShouldBeTrue();
        events[0].ScrollTop.ShouldBeGreaterThan(0);
        events[0].ScrollTop.ShouldBeLessThan(1000);
        scheduler.Pending.Count.ShouldBe(1); // the next frame

        scrollable.SetScrollPositionNow(new NewScrollPosition(ScrollTop: 10));

        scheduler.Cancelled.ShouldBe(1);
        scheduler.Pending.ShouldBeEmpty();
        scrollable.HasPendingScrollAnimation().ShouldBeFalse();
        scrollable.GetCurrentScrollPosition().ScrollTop.ShouldBe(10);
    }

    [Fact]
    public void SetScrollPositionSmooth_Without_A_Duration_Scrolls_At_Once()
    {
        var scheduler = new FrameScheduler();
        using var scrollable = new Scrollable(new ScrollableOptions(false, 0, scheduler.Schedule));
        scrollable.SetScrollDimensions(new NewScrollDimensions(Width: 100, ScrollWidth: 100, Height: 50, ScrollHeight: 5000), false);

        scrollable.SetScrollPositionSmooth(new NewScrollPosition(ScrollTop: 1000));

        scheduler.Pending.ShouldBeEmpty();
        scrollable.GetCurrentScrollPosition().ScrollTop.ShouldBe(1000);
    }
}
