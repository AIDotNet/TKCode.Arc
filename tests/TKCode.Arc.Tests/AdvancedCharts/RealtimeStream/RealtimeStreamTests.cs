using System.Collections.ObjectModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TKCode.Arc.Motion;
using Shouldly;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.AdvancedCharts.RealtimeStreams;

public class RealtimeStreamTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 8, 9, 30, 0, TimeSpan.Zero);

    private static DateTimeOffset At(double seconds) => T0.AddSeconds(seconds);

    private static long Ticks(double seconds) => At(seconds).UtcTicks;

    private static RealtimeStreamPoint[] Ramp(int count, double step = 1)
        => Enumerable.Range(0, count).Select(i => new RealtimeStreamPoint(At(i * step), 100 + i)).ToArray();

    private static RealtimeStreamPlot PlotOf(RealtimeStream chart) => TestHelpers.Descendant<RealtimeStreamPlot>(chart);

    [Fact]
    public void Buffer_Finds_Readings_By_Binary_Search_And_Keeps_Sequence_Numbers()
    {
        var buffer = new RealtimeStreamBuffer();
        for (var i = 0; i < 1000; i++)
        {
            buffer.Add(i * 10, i, null).ShouldBeTrue();
        }
        buffer.LowerBound(-1).ShouldBe(0);
        buffer.LowerBound(55).ShouldBe(6);
        buffer.LowerBound(50).ShouldBe(5);
        buffer.UpperBound(50).ShouldBe(6);
        buffer.UpperBound(99999).ShouldBe(1000);

        buffer.DropFirst(100);
        buffer.Dropped.ShouldBe(100);
        buffer.Count.ShouldBe(900);
        buffer.ValueAt(0).ShouldBe(100);
        buffer.LowerBound(1000).ShouldBe(0);

        buffer.Add(5, 1, null).ShouldBeFalse("older than the newest reading");
        buffer.Add(20000, double.NaN, null).ShouldBeFalse("not a number");
        buffer.Add(9990, 1, "same time is fine").ShouldBeTrue();

        // Moving within the arrays when room is short keeps the contents.
        for (var i = 0; i < 5000; i++)
        {
            buffer.Add(10000 + i, i, null);
            buffer.DropFirst(1);
        }
        buffer.TicksAt(buffer.Count - 1).ShouldBe(14999);
        buffer.LowerBound(14999).ShouldBe(buffer.Count - 1);
    }

    [AvaloniaFact]
    public void Window_Slices_To_The_Readings_In_View()
    {
        var chart = new RealtimeStream { Points = Ramp(100), WindowDuration = TimeSpan.FromSeconds(10) };
        var window = TestHelpers.Show(chart);
        try
        {
            chart.HeadTicks.ShouldBe(Ticks(99));
            var (first, last) = chart.VisibleRange;
            chart.Buffer.TicksAt(first).ShouldBe(Ticks(89));
            last.ShouldBe(chart.Buffer.Count - 1);
            (last - first).ShouldBe(10);
            chart.WindowDuration = TimeSpan.FromSeconds(30);
            (chart.VisibleRange.Last - chart.VisibleRange.First).ShouldBe(30);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Old_Readings_Are_Trimmed_From_An_Endless_Feed()
    {
        var chart = new RealtimeStream { WindowDuration = TimeSpan.FromSeconds(5), Interval = TimeSpan.FromSeconds(1) };
        var window = TestHelpers.Show(chart);
        try
        {
            for (var i = 0; i < 5000; i++)
            {
                chart.Append(At(i), i, i % 100 == 0 ? "flag" : null);
            }
            chart.Buffer.Count.ShouldBeLessThan(100);
            chart.Markers.Count.ShouldBeLessThan(3);
            chart.VisibleRange.Last.ShouldBe(chart.Buffer.Count - 1);
            (chart.VisibleRange.Last - chart.VisibleRange.First).ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Live_Edge_Trails_The_Newest_Reading_By_The_Interval()
    {
        var now = 1_000_000L;
        var chart = new RealtimeStream { Interval = TimeSpan.FromSeconds(2), Clock = () => now };
        chart.Append(At(0), 1);
        chart.Append(At(10), 2);
        var second = TimeSpan.TicksPerSecond;
        chart.LiveEdge(now).ShouldBe(Ticks(8));
        chart.LiveEdge(now + second / 2).ShouldBe(Ticks(8.5));
        chart.LiveEdge(now + 2 * second).ShouldBe(Ticks(10));
        chart.LiveEdge(now + 60 * second).ShouldBe(Ticks(10), "never past the newest reading");
    }

    [AvaloniaFact]
    public void Full_Motion_Scrolls_At_Frame_Rate_And_Interpolates_The_Leading_Edge()
    {
        var now = 1_000_000L;
        var chart = new RealtimeStream { Interval = TimeSpan.FromSeconds(1), Clock = () => now };
        var window = TestHelpers.Show(chart, ArcMotionLevel.Full);
        try
        {
            chart.Append(At(0), 10);
            chart.Append(At(1), 20);
            chart.Append(At(2), 40);
            // The new reading is not at the edge yet: the line has one interval to get to it.
            chart.HeadTicks.ShouldBeLessThan(Ticks(2));
            var before = chart.HeadTicks;

            chart.AdvanceFrame(0).ShouldBeTrue();
            now += TimeSpan.TicksPerSecond / 2;
            chart.AdvanceFrame(0.5).ShouldBeTrue();
            chart.HeadTicks.ShouldBeGreaterThan(before);
            chart.HeadTicks.ShouldBeLessThanOrEqualTo(Ticks(2));
            chart.LeadingValue.ShouldBeGreaterThan(20);
            chart.LeadingValue.ShouldBeLessThanOrEqualTo(40);

            // With nothing new the view reaches the newest reading and the loop may rest.
            now += TimeSpan.TicksPerSecond * 5;
            for (var i = 0; i < 200 && chart.AdvanceFrame(0.1); i++)
            {
            }
            chart.HeadTicks.ShouldBe(Ticks(2));
            chart.LeadingValue.ShouldBe(40);
            chart.AdvanceFrame(0.1).ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Scrolling_Frame_Allocates_Nothing()
    {
        var now = 1_000_000L;
        // A long interval leaves half a minute of scrolling before the view catches up with the newest reading.
        var chart = new RealtimeStream { Interval = TimeSpan.FromSeconds(30) };
        var window = TestHelpers.Show(chart, ArcMotionLevel.Full);
        try
        {
            chart.Clock = () => now;
            for (var i = 0; i < 60; i++)
            {
                chart.Append(At(i), 100 + i % 7);
            }
            var plot = PlotOf(chart);
            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(400, 300));
            using var context = bitmap.CreateDrawingContext();
            void Frames(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    now += TimeSpan.TicksPerMillisecond * 16;
                    chart.AdvanceFrame(0.016);
                    plot.Render(context);
                }
            }
            // The first frames settle the axis and fill the caches; rebuilding the line while the axis moves is expected.
            Frames(100);
            var head = chart.HeadTicks;
            var builds = plot.GeometryBuilds;
            builds.ShouldBeGreaterThan(0);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Frames(20);
            (GC.GetAllocatedBytesForCurrentThread() - before).ShouldBe(0);
            chart.HeadTicks.ShouldBeGreaterThan(head, "the frames did scroll");
            plot.GeometryBuilds.ShouldBe(builds, "scrolling moves the geometry it has");

            chart.Append(At(60), 104);
            Frames(1);
            plot.GeometryBuilds.ShouldBe(builds + 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Shows_The_Newest_Reading_And_Redraws_Only_When_One_Arrives()
    {
        var chart = new RealtimeStream();
        var window = TestHelpers.Show(chart, ArcMotionLevel.Reduced);
        try
        {
            chart.Append(At(0), 1);
            chart.Append(At(1), 2);
            chart.HeadTicks.ShouldBe(Ticks(1));
            PlotOf(chart).IsLoopRunning.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Axis_Follows_The_Data_With_Room_And_Round_Numbers()
    {
        var (low, high) = RealtimeStream.AxisFor(112, 187, null, null);
        low.ShouldBeLessThan(112);
        high.ShouldBeGreaterThan(187);
        (low % 20).ShouldBe(0);
        (high % 20).ShouldBe(0);

        // No negative room below readings that are never negative.
        RealtimeStream.AxisFor(1, 90, null, null).Low.ShouldBe(0);
        RealtimeStream.AxisFor(-40, 90, null, null).Low.ShouldBeLessThan(-40);

        // A flat line still gets a span.
        var flat = RealtimeStream.AxisFor(50, 50, null, null);
        flat.High.ShouldBeGreaterThan(flat.Low);

        // Fixed ends stay exactly as given.
        RealtimeStream.AxisFor(112, 187, 0, 500).ShouldBe((0, 500));
        RealtimeStream.AxisFor(112, 187, 0, null).Low.ShouldBe(0);
        RealtimeStream.AxisFor(112, 187, null, 300).High.ShouldBe(300);
    }

    [AvaloniaFact]
    public void Axis_Tracks_The_Window_Or_Stays_Fixed()
    {
        var chart = new RealtimeStream { WindowDuration = TimeSpan.FromSeconds(5) };
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Append(At(0), 1000);
            for (var i = 1; i < 20; i++)
            {
                chart.Append(At(i), 10 + i);
            }
            // The 1000 has left the view.
            chart.AxisHigh.ShouldBeLessThan(100);
            chart.AxisLow.ShouldBe(chart.TargetLow, "no motion: the axis is at its target");

            chart.Minimum = 0;
            chart.Maximum = 2000;
            chart.TargetLow.ShouldBe(0);
            chart.TargetHigh.ShouldBe(2000);
            chart.AxisHigh.ShouldBe(2000);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Axis_Eases_Toward_Its_Target_Under_Full_Motion()
    {
        var chart = new RealtimeStream { Minimum = 0, Maximum = 100 };
        var window = TestHelpers.Show(chart, ArcMotionLevel.Full);
        try
        {
            chart.Append(At(0), 10);
            chart.Maximum = 200;
            chart.TargetHigh.ShouldBe(200);
            chart.AxisHigh.ShouldBeLessThan(200);
            chart.AdvanceFrame(0.05);
            chart.AxisHigh.ShouldBeGreaterThan(100);
            chart.AxisHigh.ShouldBeLessThan(200);
            for (var i = 0; i < 200; i++)
            {
                chart.AdvanceFrame(0.05);
            }
            chart.AxisHigh.ShouldBe(200);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Appends_And_Collection_Changes_Reach_The_Chart()
    {
        var points = new ObservableCollection<RealtimeStreamPoint>(Ramp(3));
        var chart = new RealtimeStream { Points = points };
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Buffer.Count.ShouldBe(3);
            points.Add(new RealtimeStreamPoint(At(3), 500));
            chart.Buffer.Count.ShouldBe(4);
            chart.HeadTicks.ShouldBe(Ticks(3));

            // A feed trimming its own history leaves the view alone.
            points.RemoveAt(0);
            chart.Buffer.Count.ShouldBe(4);

            // Anything else reads the collection again.
            points[0] = new RealtimeStreamPoint(At(1), 7);
            chart.Buffer.Count.ShouldBe(3);
            chart.Buffer.ValueAt(0).ShouldBe(7);

            chart.Append(new RealtimeStreamPoint(At(100), 1));
            chart.Append(At(50), 2);
            chart.Append(At(101), double.NaN);
            chart.Buffer.LastTicks.ShouldBe(Ticks(100), "older and non-finite readings are ignored");

            points.Clear();
            chart.Buffer.Count.ShouldBe(0);
            chart.Classes.Contains(RealtimeStream.EmptyPseudoClass).ShouldBeTrue();
            chart.IsEmpty.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Collection_Is_Not_Watched_While_Detached()
    {
        var points = new ObservableCollection<RealtimeStreamPoint>(Ramp(3));
        var chart = new RealtimeStream { Points = points };
        var window = TestHelpers.Show(chart);
        window.Content = null;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        points.Add(new RealtimeStreamPoint(At(3), 1));
        chart.Buffer.Count.ShouldBe(3);
        window.Content = chart;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        chart.Buffer.Count.ShouldBe(4);
        window.Close();
    }

    [AvaloniaFact]
    public void Anomalies_Get_A_Marker_And_Are_Announced_Once()
    {
        var spoken = new List<string>();
        var chart = new RealtimeStream { Unit = "ms", SpokenSink = spoken.Add };
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Append(At(0), 100);
            chart.Append(At(1), 400, "Retry burst");
            chart.Append(At(2), 110);
            chart.Markers.Count.ShouldBe(1);
            chart.Markers[0].Sequence.ShouldBe(1);
            chart.Markers[0].Progress.ShouldBe(1, "no motion: no pop");
            spoken.Count.ShouldBe(1);
            spoken[0].ShouldBe("Anomaly at 09:30:01: 400ms, Retry burst");

            // More frames and more readings do not repeat it.
            chart.AdvanceFrame(0.1);
            chart.Append(At(3), 120);
            spoken.Count.ShouldBe(1);
            chart.Spoken.ShouldBe(spoken[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void History_Is_Marked_But_Not_Announced()
    {
        var spoken = new List<string>();
        var points = new[] { new RealtimeStreamPoint(At(0), 1, "Old flag"), new RealtimeStreamPoint(At(1), 2) };
        var chart = new RealtimeStream { SpokenSink = spoken.Add, Points = points };
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Markers.Count.ShouldBe(1);
            spoken.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Markers_Pop_Once_The_View_Reaches_Them()
    {
        var now = 1_000_000L;
        var chart = new RealtimeStream { Interval = TimeSpan.FromSeconds(1) };
        var window = TestHelpers.Show(chart, ArcMotionLevel.Full);
        try
        {
            chart.Clock = () => now;
            chart.Append(At(0), 1);
            chart.Append(At(1), 2, "Flag");
            chart.Markers[0].Progress.ShouldBe(0);
            chart.AdvanceFrame(0.01);
            chart.Markers[0].Progress.ShouldBe(0, "not on screen yet");
            now += TimeSpan.TicksPerSecond * 2;
            for (var i = 0; i < 100 && chart.Markers[0].Progress < 1; i++)
            {
                chart.AdvanceFrame(0.05);
            }
            chart.Markers[0].Progress.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void The_Pop_Overshoots_Once()
    {
        RealtimeStreamPlot.PopScale(0).ShouldBe(0, 1e-9);
        RealtimeStreamPlot.PopScale(1).ShouldBe(1, 1e-9);
        var samples = Enumerable.Range(0, 101).Select(i => RealtimeStreamPlot.PopScale(i / 100.0)).ToArray();
        samples.Max().ShouldBeGreaterThan(1.05);
        samples.Max().ShouldBeLessThan(1.2);
        var turns = 0;
        for (var i = 2; i < samples.Length; i++)
        {
            if (Math.Sign(samples[i] - samples[i - 1]) != Math.Sign(samples[i - 1] - samples[i - 2]))
            {
                turns++;
            }
        }
        turns.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Pausing_Holds_The_View_While_Readings_Keep_Coming()
    {
        var events = new List<RealtimeStreamPausedChangedEventArgs>();
        var chart = new RealtimeStream();
        chart.PausedChanged += (_, e) => events.Add(e);
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Append(At(0), 1);
            chart.Append(At(1), 2);
            chart.IsPaused = true;
            chart.IsHeld.ShouldBeTrue();
            chart.Classes.Contains(RealtimeStream.PausedPseudoClass).ShouldBeTrue();
            chart.Append(At(2), 3);
            chart.HeadTicks.ShouldBe(Ticks(1));
            chart.Buffer.Count.ShouldBe(3);
            TestHelpers.Descendant<RollingDigits>(chart).Value.ShouldBe(3, "the header stays current");

            chart.IsPaused = false;
            chart.HeadTicks.ShouldBe(Ticks(2));
            events.Select(e => (e.IsPaused, e.Reason)).ShouldBe(
            [
                (true, RealtimeStreamHoldReason.Request),
                (false, RealtimeStreamHoldReason.Request)
            ]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Resuming_Glides_Back_Under_Full_Motion()
    {
        var now = 1_000_000L;
        var chart = new RealtimeStream { Interval = TimeSpan.Zero };
        var window = TestHelpers.Show(chart, ArcMotionLevel.Full);
        try
        {
            chart.Clock = () => now;
            chart.Append(At(0), 1);
            chart.Append(At(1), 1);
            chart.IsPaused = true;
            var held = chart.HeadTicks;
            for (var i = 2; i < 12; i++)
            {
                chart.Append(At(i), i);
            }
            chart.AdvanceFrame(0.1);
            chart.HeadTicks.ShouldBe(held);
            chart.IsPaused = false;
            chart.HeadTicks.ShouldBe(held, "the view does not jump");
            chart.AdvanceFrame(0.1);
            chart.HeadTicks.ShouldBeGreaterThan(held);
            chart.HeadTicks.ShouldBeLessThan(Ticks(11));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Status_Button_Toggles_And_Says_Its_State_In_Words()
    {
        var chart = new RealtimeStream { LiveLabel = "On air", PausedLabel = "Stopped" };
        var window = TestHelpers.Show(chart);
        try
        {
            var button = TestHelpers.Descendant<Button>(chart, "PART_Status");
            var label = TestHelpers.Descendant<TextBlock>(chart, "PART_StatusText");
            label.Text.ShouldBe("On air");
            AutomationProperties.GetName(button).ShouldBe("On air. Pause updates");
            TestHelpers.Descendant<Control>(chart, "PART_LiveDot").IsVisible.ShouldBeTrue();

            button.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);

            chart.IsPaused.ShouldBeTrue();
            label.Text.ShouldBe("Stopped");
            AutomationProperties.GetName(button).ShouldBe("Stopped. Resume updates");
            TestHelpers.Descendant<Control>(chart, "PART_LiveDot").IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<Control>(chart, "PART_PausedGlyph").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hover_Holds_And_Reads_The_Nearest_Point()
    {
        var events = new List<RealtimeStreamPausedChangedEventArgs>();
        var chart = new RealtimeStream { Points = Ramp(40) };
        chart.PausedChanged += (_, e) => events.Add(e);
        var window = TestHelpers.Show(chart);
        try
        {
            var plot = PlotOf(chart);
            plot.Focusable.ShouldBeTrue();
            var head = chart.HeadTicks;

            chart.PointerAt(plot.Bounds.Width - 8, touch: false);
            chart.IsHeld.ShouldBeTrue();
            chart.IsPaused.ShouldBeFalse("hover is not a request");
            chart.CursorIndex.ShouldBe(chart.Buffer.Count - 1);

            chart.Append(At(100), 1);
            chart.HeadTicks.ShouldBe(head);

            chart.PointerGone(touch: false);
            chart.IsHeld.ShouldBeFalse();
            chart.CursorIndex.ShouldBe(-1);
            events.Select(e => (e.IsPaused, e.Reason)).ShouldBe(
            [
                (true, RealtimeStreamHoldReason.Pointer),
                (false, RealtimeStreamHoldReason.Pointer)
            ]);

            chart.IsPauseOnHover = false;
            chart.PointerAt(plot.Bounds.Width - 8, touch: false);
            chart.IsHeld.ShouldBeFalse();
            chart.CursorIndex.ShouldBeGreaterThanOrEqualTo(0, "the reading still works");
            chart.PointerGone(touch: false);

            chart.PointerAt(plot.Bounds.Width - 8, touch: true);
            chart.IsHeld.ShouldBeTrue("a touch holds whatever IsPauseOnHover says");
            chart.PointerGone(touch: true);
            chart.IsHeld.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Reads_Points_And_Holds_The_View()
    {
        var spoken = new List<string>();
        var chart = new RealtimeStream { Points = Ramp(40), WindowDuration = TimeSpan.FromSeconds(30), Unit = "ms", SpokenSink = spoken.Add };
        var window = TestHelpers.Show(chart);
        try
        {
            var plot = PlotOf(chart);
            var (first, last) = chart.VisibleRange;
            plot.Focus(NavigationMethod.Tab);
            chart.IsHeld.ShouldBeTrue();
            chart.IsPaused.ShouldBeFalse();
            chart.CursorIndex.ShouldBe(last);
            spoken[^1].ShouldBe("09:30:39: 139ms");

            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.CursorIndex.ShouldBe(last - 1);
            spoken[^1].ShouldBe("09:30:38: 138ms");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.CursorIndex.ShouldBe(last);
            window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            chart.CursorIndex.ShouldBe(last - 10);
            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            chart.CursorIndex.ShouldBe(last);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            chart.CursorIndex.ShouldBe(first);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.CursorIndex.ShouldBe(first, "stays in view");
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            chart.CursorIndex.ShouldBe(last);

            // Space resumes from the hold and pauses again; Escape clears and resumes.
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            chart.IsHeld.ShouldBeFalse();
            chart.CursorIndex.ShouldBe(-1);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            chart.IsPaused.ShouldBeTrue();
            chart.CursorIndex.ShouldBe(last);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.IsPaused.ShouldBeFalse();
            chart.IsHeld.ShouldBeFalse();
            chart.CursorIndex.ShouldBe(-1);

            // After Escape an arrow takes hold again at the newest point.
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.IsHeld.ShouldBeTrue();
            chart.CursorIndex.ShouldBe(last);

            // Enter does what Space does.
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            chart.IsHeld.ShouldBeFalse();

            // Leaving the plot lets go of the hold.
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.IsHeld.ShouldBeTrue();
            TestHelpers.Descendant<Button>(chart, "PART_Status").Focus();
            chart.IsHeld.ShouldBeFalse();
            chart.CursorIndex.ShouldBe(-1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Event_Args_Name_The_Keyboard()
    {
        var events = new List<RealtimeStreamPausedChangedEventArgs>();
        var chart = new RealtimeStream { Points = Ramp(10) };
        chart.PausedChanged += (_, e) => events.Add(e);
        var window = TestHelpers.Show(chart);
        try
        {
            PlotOf(chart).Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            events.Select(e => (e.IsPaused, e.Reason)).ShouldBe(
            [
                (true, RealtimeStreamHoldReason.Keyboard),
                (false, RealtimeStreamHoldReason.Keyboard)
            ]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Focus_Does_Not_Hold_By_Itself()
    {
        var chart = new RealtimeStream { Points = Ramp(10) };
        var window = TestHelpers.Show(chart);
        try
        {
            PlotOf(chart).Focus(NavigationMethod.Pointer);
            chart.IsHeld.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_State_Skips_The_Keyboard_And_Shows_Its_Label()
    {
        var chart = new RealtimeStream { Label = "Latency", EmptyLabel = "No readings yet" };
        var window = TestHelpers.Show(chart);
        try
        {
            var plot = PlotOf(chart);
            plot.Focusable.ShouldBeFalse();
            plot.Bounds.Height.ShouldBe(chart.PlotHeight + chart.TimeAxisHeight);
            chart.Summary.ShouldBe("Latency. No readings yet.");
            TestHelpers.Descendant<RollingDigits>(chart).IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(TestHelpers.Descendant<StackPanel>(chart, "PART_ValueRow")).ShouldBe("Latency: No readings yet");
            chart.HandleKey(Key.Left).ShouldBeFalse();

            chart.Append(At(0), 12);
            plot.Focusable.ShouldBeTrue();
            TestHelpers.Descendant<RollingDigits>(chart).IsVisible.ShouldBeTrue();

            chart.Clear();
            chart.IsEmpty.ShouldBeTrue();
            plot.Focusable.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Header_Value_Is_Real_Text_And_Formatters_Apply_Everywhere()
    {
        var chart = new RealtimeStream { Label = "Latency", Unit = "ms" };
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Append(new DateTimeOffset(2026, 11, 8, 14, 5, 9, TimeSpan.FromHours(2)), -1234.5);
            var row = TestHelpers.Descendant<StackPanel>(chart, "PART_ValueRow");
            var digits = TestHelpers.Descendant<RollingDigits>(chart);
            AutomationProperties.GetName(row).ShouldBe("Latency: −1,234.5ms");
            digits.Decimals.ShouldBe(1);
            TestHelpers.Descendant<TextBlock>(chart, "PART_Unit").Text.ShouldBe("ms");
            chart.FormatTime(chart.Buffer.LastTicks).ShouldBe("14:05:09", "the offset the reading carries");
            chart.Describe(0).ShouldBe("14:05:09: −1,234.5ms");

            chart.ValueFormatter = v => $"{v:0.0} units";
            chart.TimeFormatter = t => t.ToString("mm:ss");
            digits.IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(chart, "PART_ValueText").Text.ShouldBe("-1234.5 units");
            chart.Describe(0).ShouldBe("05:09: -1234.5 units");
            AutomationProperties.GetName(row).ShouldBe("Latency: -1234.5 units");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Automation_Peer_Names_The_Plot_And_Speaks_The_Reading()
    {
        var chart = new RealtimeStream { Label = "Latency", Points = Ramp(5) };
        var window = TestHelpers.Show(chart);
        try
        {
            var plot = PlotOf(chart);
            var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(plot);
            peer.GetName().ShouldStartWith("Latency.");
            peer.GetHelpText().ShouldStartWith("Latency. Latest 104");
            plot.Focus(NavigationMethod.Tab);
            peer.GetHelpText().ShouldBe("09:30:04: 104");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            peer.GetHelpText().ShouldStartWith("Latency. Latest");
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Time_Labels_Thin_On_Narrow_Plots()
    {
        var wide = RealtimeStreamPlot.ChooseTimeStep(30, 800, 50, 28);
        var narrow = RealtimeStreamPlot.ChooseTimeStep(30, 240, 50, 28);
        wide.ShouldBe(5);
        narrow.ShouldBe(10);
        (narrow * 240 / 30).ShouldBeGreaterThanOrEqualTo(78);
    }

    [AvaloniaFact]
    public void Plot_Fills_Its_Width_And_Draws_In_Both_Sizes()
    {
        var chart = new RealtimeStream { Points = Ramp(40), PlotHeight = 120 };
        var window = TestHelpers.Show(chart);
        try
        {
            var plot = PlotOf(chart);
            plot.Bounds.Width.ShouldBe(400);
            plot.Bounds.Height.ShouldBe(120 + chart.TimeAxisHeight);
            window.Width = 260;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            plot.Bounds.Width.ShouldBe(260);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }
}
