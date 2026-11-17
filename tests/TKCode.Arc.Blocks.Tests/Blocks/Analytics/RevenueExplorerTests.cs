using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Analytics;

/// <summary>Revenue explorer: the engine (stats, y domain, range rules, keys) and the block (headless).</summary>
public class RevenueExplorerTests
{
    // 24 months from Mar 2024, seeded like the gallery's sample data (opening 1,000,000).
    private static RevenueMonth[] Months(int count = 24)
    {
        var months = new RevenueMonth[count];
        for (var i = 0; i < count; i++)
        {
            months[i] = new RevenueMonth(new DateOnly(2024, 3, 1).AddMonths(i), 40000 + 1000 * i, 14000 + 500 * i, -(16000 + 600 * i));
        }
        return months;
    }

    private static RevenueExplorer Block(int count = 24)
    {
        var block = new RevenueExplorer { Title = "Revenue", OpeningMrr = 1_000_000 };
        block.Months.AddRange(Months(count));
        return block;
    }

    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off, double width = 1152)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width + 40;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static RevenueData Data(int count = 24) => new(Months(count), 1_000_000);

    #region Engine

    [Fact]
    public void Closing_Revenue_Is_Derived_From_The_Opening_And_Net_New()
    {
        var data = Data(3);
        // Month 0: 40,000 + 14,000 − 16,000 = 38,000.
        data.Net[0].ShouldBe(38000);
        data.Mrr[0].ShouldBe(1_038_000);
        data.Mrr[1].ShouldBe(1_038_000 + 41000 + 14500 - 16600, 1e-9);
        data.OpeningOf(0).ShouldBe(1_000_000);
        data.OpeningOf(2).ShouldBe(data.Mrr[1]);
        // A given closing revenue wins, and a positive churn is taken as its negative.
        var given = new RevenueData([new RevenueMonth(new DateOnly(2025, 1, 15), 10, 5, 3, 777)], double.NaN);
        given.Mrr[0].ShouldBe(777);
        given.Churn[0].ShouldBe(-3);
        given.Months[0].ShouldBe(new DateOnly(2025, 1, 1));
    }

    [Fact]
    public void Stats_Compare_With_The_Month_Before_And_The_Previous_Range()
    {
        var data = Data();
        var stats = data.Stats(12, 23);
        stats.EndMrr.ShouldBe(data.Mrr[23]);
        stats.MrrChange!.Value.ShouldBe(data.Mrr[23] / data.Mrr[11] - 1, 1e-12);
        var average = Enumerable.Range(12, 12).Average(i => data.Net[i]);
        var previous = Enumerable.Range(0, 12).Average(i => data.Net[i]);
        stats.AverageNetNew.ShouldBe(average, 1e-9);
        stats.NetNewChange!.Value.ShouldBe(average / previous - 1, 1e-12);
        // Churn over the months' opening revenue, in percent; the change is in points.
        var rate = Enumerable.Range(12, 12).Sum(i => -data.Churn[i]) / Enumerable.Range(12, 12).Sum(i => data.OpeningOf(i)) * 100;
        stats.ChurnRate!.Value.ShouldBe(rate, 1e-9);
        var before = Enumerable.Range(0, 12).Sum(i => -data.Churn[i]) / Enumerable.Range(0, 12).Sum(i => data.OpeningOf(i)) * 100;
        stats.ChurnChange!.Value.ShouldBe(rate - before, 1e-9);
    }

    [Fact]
    public void Stats_Have_No_Change_Where_The_Previous_Range_Is_Missing()
    {
        var data = Data();
        var all = data.Stats(0, 23);
        all.NetNewChange.ShouldBeNull();
        all.ChurnChange.ShouldBeNull();
        all.MrrChange!.Value.ShouldBe(data.Mrr[23] / 1_000_000 - 1, 1e-12);
        // 3 months from the start: the previous 3 months do not exist either.
        data.Stats(2, 4).NetNewChange.ShouldBeNull();
        data.Stats(3, 5).NetNewChange.ShouldNotBeNull();
        // Without an opening revenue the first month has no base.
        var unknown = new RevenueData(Months(6), double.NaN);
        unknown.Stats(0, 5).ChurnRate.ShouldBeNull();
        unknown.Stats(0, 5).MrrChange.ShouldBeNull();
    }

    [Fact]
    public void Domain_Follows_The_Measured_Fit()
    {
        var data = Data();
        var maxPos = Enumerable.Range(12, 12).Max(i => data.New[i] + data.Expansion[i]);
        var minNeg = Enumerable.Range(12, 12).Min(i => data.Churn[i]);
        var all = data.Domain(12, 23, true, true, true, false, 228);
        all.Min.ShouldBe(1.2 * minNeg, 1e-6);
        all.Max.ShouldBe(1.18 * maxPos + 0.08 * -minNeg, 1e-6);
        // No negatives: the top is 1.18 × the highest stack and the bottom is zero.
        var positive = data.Domain(12, 23, true, true, false, false, 228);
        positive.Min.ShouldBe(0);
        positive.Max.ShouldBe(1.18 * maxPos, 1e-6);
        // A hidden series counts as zero: only expansion stacks from zero.
        var onlyExpansion = data.Domain(12, 23, false, true, false, false, 228);
        onlyExpansion.Max.ShouldBe(1.18 * Enumerable.Range(12, 12).Max(i => data.Expansion[i]), 1e-6);
        // Everything off keeps the domain of "new" alone.
        var none = data.Domain(12, 23, false, false, false, false, 228);
        none.Max.ShouldBe(data.Domain(12, 23, true, false, false, false, 228).Max, 1e-9);
        // Ticks stay inside the domain, include zero and are at least 34px apart.
        all.Ticks.ShouldContain(0);
        all.Ticks.ShouldAllBe(t => t >= all.Min && t <= all.Max);
        (all.Ticks.Length - 1).ShouldBeLessThanOrEqualTo(228 / 34);
    }

    [Fact]
    public void Compare_Brings_The_Previous_Period_Into_The_Domain()
    {
        // Revenue falls in the newest year, so the previous period has the higher stack.
        var months = Months();
        for (var i = 12; i < 24; i++)
        {
            months[i].New = 5000;
            months[i].Expansion = 1000;
        }
        var data = new RevenueData(months, 1_000_000);
        var without = data.Domain(12, 23, true, true, true, false, 228);
        var with = data.Domain(12, 23, true, true, true, true, 228);
        with.Max.ShouldBeGreaterThan(without.Max * 3);
        // The first year has nothing before it.
        data.Domain(0, 11, true, true, true, true, 228).Max.ShouldBe(data.Domain(0, 11, true, true, true, false, 228).Max);
    }

    [Fact]
    public void Ranges_Presets_And_The_Minimum_Are_Clamped()
    {
        var data = Data();
        data.PresetRange(3).ShouldBe((21, 23));
        data.PresetRange(12).ShouldBe((12, 23));
        data.PresetRange(0).ShouldBe((0, 23));
        data.PresetRange(99).ShouldBe((0, 23));
        data.ClampRange(5, 6, 3).ShouldBe((5, 7));
        data.ClampRange(22, 30, 3).ShouldBe((21, 23));
        data.ClampRange(-4, 1, 3).ShouldBe((0, 2));
        data.CenterOn(10, 0, 11).ShouldBe(5);
        data.CenterOn(2, 0, 11).ShouldBe(0);
        data.CenterOn(23, 0, 11).ShouldBe(12);
    }

    [Fact]
    public void Overview_Keys_Move_The_Window_And_The_Handles_Without_Swapping()
    {
        var data = Data();
        // Window: width kept, ends clamp, Home / End jump.
        data.Step(0, 12, 23, -1, 3).ShouldBe((11, 22));
        data.Step(0, 12, 23, 1, 3).ShouldBe((12, 23));
        data.Step(0, 12, 23, -3, 3).ShouldBe((9, 20));
        data.Step(0, 12, 23, int.MinValue, 3).ShouldBe((0, 11));
        data.Step(0, 2, 13, int.MaxValue, 3).ShouldBe((12, 23));
        // Start handle: up to end − 2.
        data.Step(1, 12, 23, 3, 3).ShouldBe((15, 23));
        data.Step(1, 20, 23, 3, 3).ShouldBe((21, 23));
        data.Step(1, 12, 23, int.MaxValue, 3).ShouldBe((21, 23));
        data.Step(1, 12, 23, int.MinValue, 3).ShouldBe((0, 23));
        // End handle: down to start + 2.
        data.Step(2, 12, 23, -3, 3).ShouldBe((12, 20));
        data.Step(2, 12, 23, int.MinValue, 3).ShouldBe((12, 14));
        data.Step(2, 12, 20, int.MaxValue, 3).ShouldBe((12, 23));
    }

    [Theory]
    [InlineData(96.7, 1)]
    [InlineData(46, 1)]
    [InlineData(40, 1)]
    [InlineData(30, 2)]
    [InlineData(13, 4)]
    public void Month_Labels_Thin_Out_Under_Forty_Pixels(double pixelsPerMonth, int stride)
        => RevenueData.LabelStride(pixelsPerMonth).ShouldBe(stride);

    [Fact]
    public void Default_Texts_Follow_The_Reference_Wording_Rules()
    {
        var culture = AnalyticsFormat.DefaultCulture;
        RevenueData.DefaultMonthLabel(new DateOnly(2026, 1, 1), culture).ShouldBe("2026");
        RevenueData.DefaultMonthLabel(new DateOnly(2026, 3, 1), culture).ShouldBe("Mar");
        RevenueData.DefaultMonthYear(new DateOnly(2026, 3, 1), culture).ShouldBe("Mar 2026");
        RevenueData.DefaultRange(new DateOnly(2025, 9, 1), new DateOnly(2026, 8, 1), culture).ShouldBe("Sep 2025 – Aug 2026");
        RevenueData.DefaultRange(new DateOnly(2026, 3, 1), new DateOnly(2026, 8, 1), culture).ShouldBe("Mar – Aug 2026");
    }


    [AvaloniaFact]
    public void Chart_Keys_Open_The_Readout_Step_And_Escape_Closes_It()
    {
        var block = Block();
        var scrubs = new List<RevenueScrubChangedEventArgs>();
        block.ScrubChanged += (_, e) => scrubs.Add(e);
        var window = Show(block);
        try
        {
            var chart = block.Chart!;
            chart.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            // Focusing the plot shows the readout of the last month of the range.
            block.ScrubIndex.ShouldBe(23);
            chart.Readout.IsShown.ShouldBeTrue();
            chart.Readout.Content!.Title.ShouldBe("Feb 2026");
            chart.Readout.Content.Rows!.Count.ShouldBe(4);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            block.ScrubIndex.ShouldBe(22);
            // Shift does not enlarge the step on the plot.
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            block.ScrubIndex.ShouldBe(21);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            block.ScrubIndex.ShouldBe(12);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            block.ScrubIndex.ShouldBe(13);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            block.ScrubIndex.ShouldBe(23);
            block.Announcer!.Announcement.ShouldStartWith("Feb 2026: new ");
            scrubs[^1].Month.ShouldBe(block.EffectiveMonths[23]);

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            block.ScrubIndex.ShouldBeNull();
            chart.Readout.IsShown.ShouldBeFalse();
            chart.IsFocused.ShouldBeTrue();
            // From no cursor the next arrow shows the last month again.
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            block.ScrubIndex.ShouldBe(23);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Overview_Keys_Move_The_Window_And_The_Handles()
    {
        var block = Block();
        var changes = new List<RevenueRangeChangedEventArgs>();
        block.RangeChanged += (_, e) => changes.Add(e);
        var window = Show(block);
        try
        {
            var thumbs = block.Overview!.Thumbs;
            thumbs[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            (block.RangeStart, block.RangeEnd).ShouldBe((11, 22));
            changes.Single().RangeSource.ShouldBe(RevenueRangeSource.Keyboard);
            block.PresetSwitch!.SelectedIndex.ShouldBe(-1);
            block.Announcer!.Announcement.ShouldBe("Showing Feb 2025 – Jan 2026");
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            (block.RangeStart, block.RangeEnd).ShouldBe((0, 11));
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Shift);
            (block.RangeStart, block.RangeEnd).ShouldBe((3, 14));

            thumbs[1].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            (block.RangeStart, block.RangeEnd).ShouldBe((12, 14));
            thumbs[2].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            (block.RangeStart, block.RangeEnd).ShouldBe((12, 14));
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            (block.RangeStart, block.RangeEnd).ShouldBe((12, 23));
            block.PresetSwitch.SelectedIndex.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Series_Chips_Collapse_The_Layers_Refit_The_Axis_And_Show_The_Empty_Text()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var events = new List<RevenueSeriesChangedEventArgs>();
        block.SeriesVisibilityChanged += (_, e) => events.Add(e);
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var chart = block.Chart!;
            var topBefore = chart.Axis.TargetMax;
            var bottomBefore = chart.Axis.TargetMin;
            block.Chips[2]!.Invoke();
            Dispatcher.UIThread.RunJobs();
            block.IsChurnVisible.ShouldBeFalse();
            events.Single().Series.ShouldBe(RevenueSeries.Churn);
            events.Single().IsVisible.ShouldBeFalse();
            clock.Advance(TimeSpan.FromMilliseconds(100));
            chart.SeriesFactor(RevenueSeries.Churn).ShouldBeInRange(0.01, 0.99);
            // The layer settles at zero within about 750ms; the domain loses its negative side.
            clock.Advance(TimeSpan.FromMilliseconds(1200));
            chart.SeriesFactor(RevenueSeries.Churn).ShouldBe(0, 0.002);
            chart.Axis.TargetMin.ShouldBe(0);
            chart.Axis.TargetMin.ShouldBeGreaterThan(bottomBefore);
            chart.Axis.TargetMax.ShouldBeLessThan(topBefore);
            chart.Axis.DomainMin.ShouldBe(0, 0.5);

            block.Chips[0]!.Invoke();
            block.Chips[1]!.Invoke();
            Dispatcher.UIThread.RunJobs();
            block.Classes.Contains(":empty-series").ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(60));
            // The empty text waits 120ms, then fades in 240ms.
            chart.EmptyOpacity.ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(600));
            chart.EmptyOpacity.ShouldBe(1, 0.001);
            // The domain stays that of "new" alone.
            chart.Axis.TargetMax.ShouldBeGreaterThan(0);
            block.Chips[1]!.Invoke();
            block.Classes.Contains(":empty-series").ShouldBeFalse();
            block.Chips[1]!.IsOn.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Compare_Switch_Moves_The_Thumb_And_Blends_The_Dashes()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var events = new List<RevenueCompareChangedEventArgs>();
        block.CompareChanged += (_, e) => events.Add(e);
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var chart = block.Chart!;
            var switchControl = block.CompareSwitch!;
            var topBefore = chart.Axis.TargetMax;
            switchControl.Invoke();
            Dispatcher.UIThread.RunJobs();
            block.IsCompareEnabled.ShouldBeTrue();
            block.Classes.Contains(":compare").ShouldBeTrue();
            events.Single().IsCompared.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(100));
            chart.CompareFactor.ShouldBeInRange(0.01, 0.99);
            switchControl.ThumbOffset.ShouldBeInRange(0.1, 11.9);
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            chart.CompareFactor.ShouldBe(1, 0.002);
            switchControl.ThumbOffset.ShouldBe(12, 0.01);
            // The previous year was smaller in this data, so the domain does not grow; with a range at the start it has no previous.
            chart.Axis.TargetMax.ShouldBe(topBefore, topBefore * 0.001);
            AutomationProperties.GetName(switchControl).ShouldBe("Compare to previous period");

            // Reduced motion: the switch back lands at once.
            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            switchControl.Invoke();
            Dispatcher.UIThread.RunJobs();
            chart.CompareFactor.ShouldBe(0);
            switchControl.ThumbOffset.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Shows_The_Crosshair_And_The_Panel_Flips_Left_At_The_Right_Edge()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            var chart = block.Chart!;
            var x = chart.CrossX(15);
            window.MouseMove(chart.TranslatePoint(new Point(x, 100), window)!.Value, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            block.ScrubIndex.ShouldBe(15);
            chart.Readout.IsShown.ShouldBeTrue();
            chart.Readout.Position.X.ShouldBe(x + 14, 0.5);
            chart.Readout.Position.Y.ShouldBe(14);

            var last = chart.CrossX(23);
            window.MouseMove(chart.TranslatePoint(new Point(last - 1, 100), window)!.Value, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            block.ScrubIndex.ShouldBe(23);
            chart.Readout.Position.X.ShouldBe(last - 14 - 204, 0.5);

            window.MouseMove(chart.TranslatePoint(new Point(20, 100), window)!.Value, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            block.ScrubIndex.ShouldBeNull();
            chart.Readout.IsShown.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_The_Window_Follows_The_Pointer_And_Snaps_On_Release()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var overview = block.Overview!;
            var viewport = block.Viewport!;
            var step = (overview.Bounds.Width - 48) / 23;
            var press = overview.TranslatePoint(new Point(48 + 17.5 * step, 20), window)!.Value;
            window.MouseDown(press, MouseButton.Left, RawInputModifiers.None);
            // Two and a half months to the left.
            window.MouseMove(new Point(press.X - 2.5 * step, press.Y), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            block.Classes.Contains(":dragging").ShouldBeTrue();
            // The window is exactly where the pointer put it; the range is the nearest whole months.
            viewport.WindowStart.ShouldBe(9.5, 0.01);
            viewport.WindowEnd.ShouldBe(20.5, 0.01);
            (block.RangeStart, block.RangeEnd).ShouldBe((10, 21));
            // The chart is still on its way.
            viewport.ChartStart.ShouldBeGreaterThan(9.6);
            clock.Advance(TimeSpan.FromMilliseconds(120));
            viewport.ChartStart.ShouldBeLessThan(11.9);
            window.MouseUp(new Point(press.X - 2.5 * step, press.Y), MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            block.Classes.Contains(":dragging").ShouldBeFalse();
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            viewport.WindowStart.ShouldBe(10, 0.001);
            viewport.WindowEnd.ShouldBe(21, 0.001);
            viewport.ChartStart.ShouldBe(10, 0.001);
            viewport.ChartEnd.ShouldBe(21, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_The_Track_Centres_The_Window_There_And_Handles_Keep_Three_Months()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            var overview = block.Overview!;
            var step = (overview.Bounds.Width - 48) / 23;
            var click = overview.TranslatePoint(new Point(48 + 6 * step, 52), window)!.Value;
            window.MouseDown(click, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(click, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            // The window is 11 intervals wide: its centre goes to month 6, so it starts at 0.5 → 1 (rounded away from zero).
            (block.RangeStart, block.RangeEnd).ShouldBe((1, 12));

            // Pull the right handle far to the left: it stops two months after the start.
            var edge = overview.TranslatePoint(new Point(48 + 12 * step, 20), window)!.Value;
            window.MouseDown(edge, MouseButton.Left, RawInputModifiers.None);
            window.MouseMove(new Point(edge.X - 20 * step, edge.Y), RawInputModifiers.None);
            window.MouseUp(new Point(edge.X - 20 * step, edge.Y), MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            (block.RangeStart, block.RangeEnd).ShouldBe((1, 3));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Range_Change_Slides_Chart_And_Window_Together_And_Reduced_Motion_Jumps()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var viewport = block.Viewport!;
            block.ApplyPreset(3);
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(150));
            viewport.ChartStart.ShouldBeInRange(12.5, 20.5);
            // Chart and window ride the same spring.
            viewport.WindowStart.ShouldBe(viewport.ChartStart, 1e-9);
            viewport.WindowEnd.ShouldBe(viewport.ChartEnd, 1e-9);
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            viewport.ChartStart.ShouldBe(21, 0.001);
            block.Chart!.LabelOpacity(12).ShouldBe(0);
            block.Chart.LabelOpacity(22).ShouldBe(1);

            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            block.ApplyPreset(0);
            Dispatcher.UIThread.RunJobs();
            viewport.ChartStart.ShouldBe(0);
            viewport.WindowEnd.ShouldBe(23);
            block.Chart.Axis.DomainMax.ShouldBe(block.Chart.Axis.TargetMax);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Breakpoints_Follow_The_Blocks_Own_Width()
    {
        var block = Block();
        var host = new Border { Width = 380, Child = block };
        var window = Show(host);
        try
        {
            var classes = block.Classes;
            classes.Contains(":tight").ShouldBeTrue();
            classes.Contains(":stacked").ShouldBeTrue();
            classes.Contains(":compact").ShouldBeTrue();
            block.Chart!.Bounds.Height.ShouldBe(216);
            block.Metrics!.IsStacked.ShouldBeTrue();
            block.Metrics.Mrr.ValueSize.ShouldBe(22);
            block.PresetSwitch!.IsStretched.ShouldBeTrue();
            block.Chips[0]!.IsCompact.ShouldBeTrue();
            block.Header!.IsWrapped.ShouldBeTrue();

            host.Width = 1152;
            Dispatcher.UIThread.RunJobs();
            classes.Contains(":tight").ShouldBeFalse();
            classes.Contains(":compact").ShouldBeFalse();
            block.Chart.Bounds.Height.ShouldBe(256);
            block.Metrics.Mrr.ValueSize.ShouldBe(28);
            block.Metrics.IsStacked.ShouldBeFalse();
            block.Header.IsWrapped.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_Block_Shows_Dashes_And_Does_Not_Throw()
    {
        var block = new RevenueExplorer { Title = "Revenue" };
        var window = Show(block);
        try
        {
            block.Metrics!.Mrr.Rolling.IsVisible.ShouldBeFalse();
            block.Table!.RowHeaders.Count.ShouldBe(0);
            block.ApplyPreset(3);
            block.SetRange(0, 5);
            block.IsChurnVisible = false;
            block.IsCompareEnabled = true;
            block.Months.AddRange(Months(12));
            Dispatcher.UIThread.RunJobs();
            block.Table.RowHeaders.Count.ShouldBe(12);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
