using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Analytics;

/// <summary>Metric explorer: the engine (axes, ladders, fades, hit testing, texts) and the block (headless).</summary>
public class MetricExplorerTests
{
    private static readonly DateTime s_end = new(2026, 10, 14);

    // 730 days: a line rising 0.1 a day with a bump every week, so ranges and periods differ.
    private static double[] Series(double start = 1000, double slope = 2, int count = 730)
    {
        var values = new double[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = start + slope * i + 10 * Math.Sin(i * 0.9);
        }
        return values;
    }

    private static string Latest => AnalyticsFormat.Number(Series()[^1]);

    private static ExplorerMetric Metric(string title, double start = 1000, double slope = 2, ExplorerValueFormat format = ExplorerValueFormat.Count) =>
        new(title, Series(start, slope), format) { ShortTitle = title.Substring(0, 3), EndDate = s_end, Description = title + " description." };

    private static MetricExplorer Block(params ExplorerMetric[] metrics)
    {
        var block = new MetricExplorer { Title = "Health", Caption = "Test data" };
        if (metrics.Length == 0)
        {
            metrics = [Metric("Alpha"), Metric("Beta", 50_000, 30), Metric("Gamma", 20, 0.01, ExplorerValueFormat.Percent), Metric("Delta", 500, -0.3)];
        }
        block.Metrics.AddRange(metrics);
        return block;
    }

    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off, double width = 1152)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    #region Engine

    [Fact]
    public void Axis_Has_Five_Ticks_On_The_Fine_Ladder_And_Covers_The_Padded_Range()
    {
        var axis = MetricExplorerMath.AxisTicks(1_240_000, 1_300_000, 6, 1);
        axis.Ticks.Length.ShouldBe(5);
        axis.Ticks[0].ShouldBeLessThanOrEqualTo(1_240_000 - 0.03 * 60_000 + 1e-6);
        axis.Ticks[4].ShouldBeGreaterThanOrEqualTo(1_300_000 + 0.03 * 60_000 - 1e-6);
        // Whatever the data, steps come from 1, 1.5, 2, 2.5, 3, 4, 5 × 10ⁿ.
        foreach (var (min, max) in new[] { (0.0, 1.0), (36.4, 41.9), (5120.0, 5890.0), (2150.0, 2350.0), (0.0021, 0.0094) })
        {
            var set = MetricExplorerMath.AxisTicks(min, max, 6, 1);
            var mantissa = set.Step / Math.Pow(10, Math.Floor(Math.Log10(set.Step)));
            new[] { 1.0, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0 }.ShouldContain(Math.Round(mantissa, 6));
            set.Ticks[0].ShouldBeLessThanOrEqualTo(min);
            set.Ticks[4].ShouldBeGreaterThanOrEqualTo(max);
            for (var i = 1; i < 5; i++)
            {
                (set.Ticks[i] - set.Ticks[i - 1]).ShouldBe(set.Step, set.Step * 1e-6);
            }
        }
    }

    [Fact]
    public void Axis_Respects_The_Decimals_Cap_Of_The_Metric()
    {
        // A count may never get a fractional step, a percentage at most one decimal, compact money two (in millions).
        MetricExplorerMath.AxisTicks(100, 103, 0, 1).Step.ShouldBeGreaterThanOrEqualTo(1);
        var percent = MetricExplorerMath.AxisTicks(26.1, 27.4, 1, 1);
        (percent.Step * 10).ShouldBe(Math.Round(percent.Step * 10), 1e-9);
        var money = MetricExplorerMath.MetricAxis(ExplorerValueFormat.CurrencyCompact, -1, 4_310_000, 4_640_000);
        (money.Step / 1e6 * 100).ShouldBe(Math.Round(money.Step / 1e6 * 100), 1e-9);
    }

    [Fact]
    public void Axis_Labels_Follow_The_Format()
    {
        MetricExplorerMath.AxisLabel(ExplorerValueFormat.CurrencyCompact, "$", -1, 1_280_000, 20_000, 1e6).ShouldBe("$1.28M");
        MetricExplorerMath.AxisLabel(ExplorerValueFormat.Count, "$", -1, 2350, 50, 1).ShouldBe("2,350");
        MetricExplorerMath.AxisLabel(ExplorerValueFormat.Percent, "$", -1, 36, 2, 1).ShouldBe("36%");
        MetricExplorerMath.AxisLabel(ExplorerValueFormat.Percent, "$", -1, 35, 2.5, 1).ShouldBe("35.0%");
        MetricExplorerMath.ValueText(ExplorerValueFormat.CurrencyCompact, "$", 4_620_000).ShouldBe("$4.62M");
        MetricExplorerMath.ValueText(ExplorerValueFormat.CurrencyCompact, "$", 412_000).ShouldBe("$412.00K");
    }

    [Fact]
    public void X_Ladder_Falls_Back_At_The_Measured_Plot_Widths()
    {
        // Notes 4.2: "Mmm d" labels need about 84px between ticks, "Mmm" labels about 48px.
        MetricExplorerMath.ChooseLevel(30, 352).ShouldBe(MetricExplorerTickLevel.Weekly);
        MetricExplorerMath.ChooseLevel(30, 346).ShouldBe(MetricExplorerTickLevel.Biweekly);
        MetricExplorerMath.ChooseLevel(90, 539).ShouldBe(MetricExplorerTickLevel.Biweekly);
        MetricExplorerMath.ChooseLevel(90, 533).ShouldBe(MetricExplorerTickLevel.SemiMonthly);
        MetricExplorerMath.ChooseLevel(90, 495).ShouldBe(MetricExplorerTickLevel.Monthly);
        MetricExplorerMath.ChooseLevel(364, 579).ShouldBe(MetricExplorerTickLevel.Monthly);
        MetricExplorerMath.ChooseLevel(364, 573).ShouldBe(MetricExplorerTickLevel.Quarterly);
    }

    [Fact]
    public void Ticks_Fall_On_The_Calendar_And_Read_As_Labels()
    {
        MetricExplorerMath.IsTick(MetricExplorerTickLevel.Weekly, new DateTime(2026, 10, 12)).ShouldBeTrue();
        MetricExplorerMath.IsTick(MetricExplorerTickLevel.Weekly, new DateTime(2026, 10, 13)).ShouldBeFalse();
        MetricExplorerMath.IsTick(MetricExplorerTickLevel.SemiMonthly, new DateTime(2026, 10, 15)).ShouldBeTrue();
        MetricExplorerMath.IsTick(MetricExplorerTickLevel.Monthly, new DateTime(2026, 10, 1)).ShouldBeTrue();
        MetricExplorerMath.IsTick(MetricExplorerTickLevel.Quarterly, new DateTime(2026, 10, 1)).ShouldBeTrue();
        MetricExplorerMath.IsTick(MetricExplorerTickLevel.Quarterly, new DateTime(2026, 11, 1)).ShouldBeFalse();
        // Two adjacent Mondays cannot both carry a two-week tick.
        var first = new DateTime(2026, 10, 5);
        (MetricExplorerMath.IsTick(MetricExplorerTickLevel.Biweekly, first) ^ MetricExplorerMath.IsTick(MetricExplorerTickLevel.Biweekly, first.AddDays(7))).ShouldBeTrue();
        MetricExplorerMath.TickLabel(MetricExplorerTickLevel.Weekly, new DateTime(2026, 9, 7)).ShouldBe("Sep 7");
        MetricExplorerMath.TickLabel(MetricExplorerTickLevel.Monthly, new DateTime(2026, 9, 1)).ShouldBe("Sep");
        MetricExplorerMath.TickLabel(MetricExplorerTickLevel.Quarterly, new DateTime(2026, 10, 1)).ShouldBe("Oct 2026");
    }

    [Fact]
    public void Edge_Fade_Is_Linear_Over_Twelve_Pixels_Like_The_Measurements()
    {
        // L = 52, R = 1106 (a 1112 wide plot minus 6).
        MetricExplorerMath.EdgeFade(1094.16, 52, 1106).ShouldBe(0.654, 0.005);
        MetricExplorerMath.EdgeFade(1097.73, 52, 1106).ShouldBe(0.356, 0.005);
        MetricExplorerMath.EdgeFade(62.3, 52, 1106).ShouldBe(0.527, 0.005);
        MetricExplorerMath.EdgeFade(66.55, 52, 1106).ShouldBe(0.879, 0.005);
        MetricExplorerMath.EdgeFade(52, 52, 1106).ShouldBe(0);
        MetricExplorerMath.EdgeFade(500, 52, 1106).ShouldBe(1);
    }

    [Fact]
    public void Chart_Height_Is_A_Quarter_Of_The_Width_Within_Limits()
    {
        MetricExplorerMath.ChartHeight(1112).ShouldBe(240);
        MetricExplorerMath.ChartHeight(880).ShouldBe(220);
        MetricExplorerMath.ChartHeight(700).ShouldBe(180);
    }

    [Fact]
    public void Nearest_Day_Rounds_To_The_Closest_Point_And_Clamps()
    {
        MetricExplorerMath.NearestIndex(52, 52, 1106, 30).ShouldBe(0);
        MetricExplorerMath.NearestIndex(1106, 52, 1106, 30).ShouldBe(29);
        MetricExplorerMath.NearestIndex(2000, 52, 1106, 30).ShouldBe(29);
        MetricExplorerMath.NearestIndex(-5, 52, 1106, 30).ShouldBe(0);
        // Half way between day 10 and 11 rounds up.
        MetricExplorerMath.NearestIndex(52 + (10.5 / 29) * 1054, 52, 1106, 30).ShouldBe(11);
    }

    [Fact]
    public void Sparkline_Samples_Are_Daily_Or_Weekly()
    {
        var values = Series();
        MetricExplorerMath.SparklineSamples(values, 30, 1).Length.ShouldBe(30);
        MetricExplorerMath.SparklineSamples(values, 90, 1).Length.ShouldBe(90);
        var year = MetricExplorerMath.SparklineSamples(values, 364, 7);
        year.Length.ShouldBe(53);
        year[^1].ShouldBe(values[^1]);
        year[^2].ShouldBe(values[^8]);
    }

    [Fact]
    public void Changes_Are_Relative_Or_In_Points_And_Flat_When_They_Round_To_Zero()
    {
        MetricExplorerMath.Change(ExplorerChangeMode.Relative, 110, 100).ShouldBe(10, 1e-9);
        MetricExplorerMath.Change(ExplorerChangeMode.Points, 27.3, 26.1).ShouldBe(1.2, 1e-9);
        double.IsNaN(MetricExplorerMath.Change(ExplorerChangeMode.Relative, 5, 0)).ShouldBeTrue();
        MetricExplorerMath.Trend(1.6, true).ShouldBe((AnalyticsDirection.Up, AnalyticsTone.Good));
        MetricExplorerMath.Trend(1.6, false).ShouldBe((AnalyticsDirection.Up, AnalyticsTone.Bad));
        MetricExplorerMath.Trend(-1.1, true).ShouldBe((AnalyticsDirection.Down, AnalyticsTone.Bad));
        MetricExplorerMath.Trend(-5.9, false).ShouldBe((AnalyticsDirection.Down, AnalyticsTone.Good));
        MetricExplorerMath.Trend(0.02, true).ShouldBe((AnalyticsDirection.Flat, AnalyticsTone.Flat));
        MetricExplorerMath.ChangeFormat(ExplorerChangeMode.Points).Format(-1.1).ShouldBe("−1.1 pts");
        MetricExplorerMath.ChangeFormat(ExplorerChangeMode.Relative).Format(1.24).ShouldBe("+1.2%");
    }

    [Fact]
    public void Window_Extent_Covers_This_Period_And_The_One_Before()
    {
        var values = new double[100];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = i;
        }
        // 10 days: today (99) back to 99 - 20 = 79 with both periods and the lead-in day.
        MetricExplorerMath.WindowExtent(values, 10, true).ShouldBe((79, 99));
        MetricExplorerMath.WindowExtent(values, 10, false).ShouldBe((89, 99));
    }

    #endregion

    #region Block

    [AvaloniaFact]
    public void Collapsed_Block_Shows_One_Card_Per_Metric_With_Value_Change_And_Sparkline()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            var stage = block.Stage!;
            stage.Cards.Count.ShouldBe(4);
            stage.Panel.IsVisible.ShouldBeFalse();
            var (label, value, change, range) = stage.Cards[0].Texts;
            label.ShouldBe("Alpha");
            value.ShouldBe(Latest);
            change.ShouldStartWith("+");
            range.ShouldBe("30 days");
            stage.Cards[0].SampleCount.ShouldBe(30);
            stage.Cards[0].Trend.ShouldBe((AnalyticsDirection.Up, AnalyticsTone.Good));
            // Four columns at 1152 wide, two under 760.
            stage.Slots[3].Y.ShouldBe(stage.Slots[0].Y);
            stage.Surfaces.Count.ShouldBe(4);
            block.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cards_Take_Two_Columns_Under_760_And_The_Compact_Layout_At_440()
    {
        var block = Block();
        var window = Show(block, width: 700);
        try
        {
            var stage = block.Stage!;
            stage.Slots[1].Y.ShouldBe(stage.Slots[0].Y);
            stage.Slots[2].Y.ShouldBeGreaterThan(stage.Slots[0].Y);
            block.Classes.Contains(MetricExplorer.TwoColumnPseudoClass).ShouldBeTrue();
            var wideHeight = stage.Cards[0].DesiredSize.Height;
            window.Width = 440;
            Dispatcher.UIThread.RunJobs();
            block.Classes.Contains(MetricExplorer.CompactPseudoClass).ShouldBeTrue();
            stage.Cards[0].IsCompact.ShouldBeTrue();
            stage.Cards[0].DesiredSize.Height.ShouldBeGreaterThan(wideHeight);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Open_Replaces_The_Cards_With_The_Chart_On_The_Chosen_Metric()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            var opened = new List<int>();
            block.MetricOpened += (_, e) => opened.Add(e.Index);
            block.Open(2);
            Dispatcher.UIThread.RunJobs();
            block.IsOpen.ShouldBeTrue();
            block.SelectedMetricIndex.ShouldBe(2);
            opened.ShouldBe([2]);
            var stage = block.Stage!;
            stage.Panel.IsVisible.ShouldBeTrue();
            stage.Panel.Chart.Series!.Title.ShouldBe("Gamma");
            stage.Panel.Tabs.SelectedIndex.ShouldBe(2);
            stage.Cards.All(c => !c.Focusable).ShouldBeTrue();
            stage.Surfaces.OpacityOf(2).ShouldBe(1);
            stage.Surfaces.OpacityOf(0).ShouldBe(0);
            stage.Surfaces.RectOf(2).Width.ShouldBe(stage.Bounds.Width, 0.5);
            block.Classes.Contains(MetricExplorer.OpenPseudoClass).ShouldBeTrue();
            block.Close();
            Dispatcher.UIThread.RunJobs();
            stage.Panel.IsVisible.ShouldBeFalse();
            stage.Surfaces.OpacityOf(0).ShouldBe(1);
            stage.Cards.All(c => c.Focusable).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chart_Has_Five_Rows_And_The_Readout_Shows_The_Metric()
    {
        var block = Block();
        block.IsOpen = true;
        var window = Show(block);
        try
        {
            var panel = block.Stage!.Panel;
            panel.Chart.TargetTicks.Ticks.Length.ShouldBe(5);
            panel.Chart.AxisTexts.Count(t => t.Length > 0).ShouldBe(5);
            panel.Chart.WindowIntervals.ShouldBe(29);
            panel.Chart.Bounds.Height.ShouldBe(240, 0.5);
            var readout = panel.Readout;
            readout.Title.Text.ShouldBe("Alpha");
            readout.Value.Text.ShouldBe(Latest);
            readout.Compare.Text.ShouldBe("vs 30 days ago");
            readout.Span.Text.ShouldBe("Sep 15 – Oct 14");
            readout.Delta.Direction.ShouldBe(AnalyticsDirection.Up);
            readout.Change.Amount.ShouldBeGreaterThan(0);
            panel.Tabs.ValuesShown.ShouldBeTrue();
            panel.Tabs.TabRects.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_And_Compact_Layouts_Change_The_Tabs_And_The_Range_Switch()
    {
        var block = Block();
        block.IsOpen = true;
        var window = Show(block, width: 600);
        try
        {
            var panel = block.Stage!.Panel;
            panel.Tabs.ValuesShown.ShouldBeFalse();
            var range = panel.RangeSwitch.Bounds;
            var readout = panel.Readout.Value.Bounds;
            range.Y.ShouldBeGreaterThan(readout.Y);
            window.Width = 440;
            Dispatcher.UIThread.RunJobs();
            panel.Tabs.Bounds.Width.ShouldBe(panel.Bounds.Width - 28, 1);
            // The close button leaves the tab row.
            panel.CloseButton.Bounds.Y.ShouldBeGreaterThan(panel.Tabs.Bounds.Bottom - 20);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_Range_Moves_The_Window_And_The_Cards_Follow()
    {
        var block = Block();
        block.IsOpen = true;
        var window = Show(block);
        try
        {
            var chart = block.Stage!.Panel.Chart;
            block.SelectedRangeIndex = 1;
            Dispatcher.UIThread.RunJobs();
            chart.WindowIntervals.ShouldBe(89);
            chart.Days.ShouldBe(90);
            block.Stage.Panel.Readout.Compare.Text.ShouldBe("vs 90 days ago");
            block.Stage.Cards[0].Texts.Range.ShouldBe("90 days");
            block.SelectedRangeIndex = 2;
            Dispatcher.UIThread.RunJobs();
            block.Stage.Cards[0].SampleCount.ShouldBe(53);
            chart.LevelAlpha(MetricExplorerTickLevel.Monthly).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Range_Window_Is_A_Smooth_Spring_That_Rebuilds_The_Lines_Each_Frame()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        block.IsOpen = true;
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var chart = block.Stage!.Panel.Chart;
            chart.WindowIntervals.ShouldBe(29);
            block.SelectedRangeIndex = 2;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var early = chart.WindowIntervals;
            early.ShouldBeGreaterThan(29);
            early.ShouldBeLessThan(363);
            clock.Advance(TimeSpan.FromMilliseconds(150));
            chart.WindowIntervals.ShouldBeGreaterThan(early);
            // Smooth spring: within 1% of the target after about 520ms, at rest by 1.5s.
            clock.Advance(TimeSpan.FromMilliseconds(300));
            (363 - chart.WindowIntervals).ShouldBeLessThan(0.05 * 334);
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            chart.WindowIntervals.ShouldBe(363, 1e-3);
            chart.IsMoving.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Range_Labels_Cross_Fade_Between_Ladders()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        block.IsOpen = true;
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var chart = block.Stage!.Panel.Chart;
            chart.LevelAlpha(MetricExplorerTickLevel.Weekly).ShouldBe(1);
            block.SelectedRangeIndex = 1;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(80));
            // The old ladder is half way out, the new one has not started yet (60ms delay) or has just begun.
            chart.LevelAlpha(MetricExplorerTickLevel.Weekly).ShouldBeInRange(0.05, 0.95);
            clock.Advance(TimeSpan.FromMilliseconds(600));
            chart.LevelAlpha(MetricExplorerTickLevel.Weekly).ShouldBe(0);
            chart.LevelAlpha(MetricExplorerTickLevel.Biweekly).ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_Metric_Morphs_Point_By_Point_From_The_Old_Shape()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        block.IsOpen = true;
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var chart = block.Stage!.Panel.Chart;
            var firstAxis = chart.TargetTicks;
            block.SelectedMetricIndex = 1;
            Dispatcher.UIThread.RunJobs();
            chart.Series!.Title.ShouldBe("Beta");
            chart.TargetTicks.Ticks[0].ShouldNotBe(firstAxis.Ticks[0]);
            chart.IsMoving.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(120));
            chart.IsMoving.ShouldBeTrue();
            // The y labels cross-fade one by one: the bottom slot first, the top slot 140ms later.
            clock.Advance(TimeSpan.FromMilliseconds(2000));
            chart.IsMoving.ShouldBeFalse();
            block.Stage.Panel.Readout.Title.Text.ShouldBe("Beta");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrubbing_Shows_The_Day_And_Compares_It_With_The_Day_One_Period_Earlier()
    {
        var block = Block();
        block.IsOpen = true;
        var window = Show(block);
        try
        {
            var events = new List<MetricExplorerScrubEventArgs>();
            block.ScrubChanged += (_, e) => events.Add(e);
            var panel = block.Stage!.Panel;
            panel.Chart.SetScrub(10);
            Dispatcher.UIThread.RunJobs();
            block.ScrubIndex.ShouldBe(10);
            block.Classes.Contains(MetricExplorer.ScrubbingPseudoClass).ShouldBeTrue();
            var last = events[^1];
            last.Index.ShouldBe(10);
            last.Date.ShouldBe(s_end.AddDays(-(29 - 10)));
            last.PreviousDate.ShouldBe(s_end.AddDays(-(29 - 10) - 30));
            last.Value.ShouldNotBeNull();
            last.PreviousValue.ShouldNotBeNull();
            panel.Readout.Compare.Text.ShouldBe("Sep 25 vs Aug 26");
            panel.Chart.SetScrub(null);
            Dispatcher.UIThread.RunJobs();
            block.ScrubIndex.ShouldBeNull();
            panel.Readout.Compare.Text.ShouldBe("vs 30 days ago");
            events[^1].Index.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Moves_Over_The_Plot_Inspect_The_Nearest_Day_And_Leaving_Clears_It()
    {
        var block = Block();
        block.IsOpen = true;
        var window = Show(block);
        try
        {
            var chart = block.Stage!.Panel.Chart;
            var origin = chart.TranslatePoint(new Point(0, 0), window)!.Value;
            var inset = chart.Geometry.PlotInset;
            var left = origin.X + inset.Left;
            var right = origin.X + chart.Bounds.Width - inset.Right;
            window.MouseMove(new Point(right, origin.Y + 60), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            chart.ScrubIndex.ShouldBe(29);
            window.MouseMove(new Point(left + (right - left) / 2, origin.Y + 60), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            chart.ScrubIndex!.Value.ShouldBeInRange(14, 15);
            window.MouseMove(new Point(left - 40, origin.Y + 60), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            chart.ScrubIndex.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chart_Keys_Step_Days_Home_End_And_Escape_Clears_Before_Closing()
    {
        var block = Block();
        block.IsOpen = true;
        var window = Show(block);
        try
        {
            var chart = block.Stage!.Panel.Chart;
            chart.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            chart.ScrubIndex.ShouldBe(29);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.ScrubIndex.ShouldBe(28);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            chart.ScrubIndex.ShouldBe(27);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            chart.ScrubIndex.ShouldBe(28);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            chart.ScrubIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.ScrubIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            chart.ScrubIndex.ShouldBe(29);
            window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            chart.ScrubIndex.ShouldBe(29);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.ScrubIndex.ShouldBeNull();
            block.IsOpen.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            block.IsOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Tabs_And_Range_Switch_Select_With_The_Arrow_Keys_And_Wrap()
    {
        var block = Block();
        block.IsOpen = true;
        var window = Show(block);
        try
        {
            var panel = block.Stage!.Panel;
            panel.Tabs.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            block.SelectedMetricIndex.ShouldBe(3);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            block.SelectedMetricIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            block.SelectedMetricIndex.ShouldBe(3);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            block.SelectedMetricIndex.ShouldBe(0);
            Dispatcher.UIThread.RunJobs();
            panel.Chart.Series!.Title.ShouldBe("Alpha");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_On_A_Card_Opens_It_And_Moves_Focus_To_The_Tabs_Escape_Returns_To_The_Card()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            var stage = block.Stage!;
            stage.Cards[1].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            TestHelpers.PumpUntil(() => stage.Panel.Tabs.IsFocused);
            block.IsOpen.ShouldBeTrue();
            block.SelectedMetricIndex.ShouldBe(1);
            stage.Panel.Tabs.IsFocused.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            TestHelpers.PumpUntil(() => stage.Cards[1].IsFocused);
            block.IsOpen.ShouldBeFalse();
            stage.Cards[1].IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Cards_Have_Spoken_Names_And_Announcements_Name_The_Change()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            var stage = block.Stage!;
            Avalonia.Automation.AutomationProperties.GetName(stage.Cards[0]).ShouldStartWith($"Alpha: {Latest}, up ");
            Avalonia.Automation.AutomationProperties.GetName(stage.Cards[0]).ShouldEndWith("over 30 days. Open chart");
            block.Open(0);
            Dispatcher.UIThread.RunJobs();
            block.SelectedMetricIndex = 3;
            Dispatcher.UIThread.RunJobs();
            block.Announcer!.History[^1].ShouldStartWith("Delta, last 30 days: ");
            block.Close();
            Dispatcher.UIThread.RunJobs();
            block.Announcer.History[^1].ShouldBe("Chart closed. 4 metrics shown.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Open_Grows_The_Cards_Surface_Into_The_Chart_And_Close_Shrinks_It_Back()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var stage = block.Stage!;
            var card = stage.Slots[2];
            stage.Surfaces.RectOf(2).Width.ShouldBe(card.Width, 0.5);
            block.Open(2);
            Dispatcher.UIThread.RunJobs();
            // The layout jumps to the chart's size at once; the surface is still card sized and starts to grow.
            stage.Panel.Bounds.Width.ShouldBe(stage.Bounds.Width, 0.5);
            stage.Surfaces.RectOf(2).Width.ShouldBeLessThan(stage.Bounds.Width - 100);
            stage.Surfaces.OpacityOf(0).ShouldBe(0);
            stage.PanelOpacity.ShouldBe(0, 0.01);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            var middle = stage.Surfaces.RectOf(2);
            middle.Width.ShouldBeGreaterThan(card.Width + 10);
            // The content has begun to fade in (240ms from 120ms).
            stage.PanelOpacity.ShouldBeGreaterThan(0.05);
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            stage.Surfaces.RectOf(2).Width.ShouldBe(stage.Bounds.Width, 0.5);
            stage.PanelOpacity.ShouldBe(1, 1e-6);

            block.Close();
            Dispatcher.UIThread.RunJobs();
            stage.Panel.IsVisible.ShouldBeFalse();
            stage.Surfaces.RectOf(2).Width.ShouldBeGreaterThan(stage.Bounds.Width - 20);
            stage.Surfaces.OpacityOf(0).ShouldBeLessThan(0.5);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            stage.Surfaces.RectOf(2).Width.ShouldBeLessThan(stage.Bounds.Width - 20);
            // The other cards' content waits 80ms, the opened card's 160ms.
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            stage.Surfaces.RectOf(2).Width.ShouldBe(card.Width, 0.5);
            stage.CardOpacity(0).ShouldBe(1, 1e-6);
            stage.CardOpacity(2).ShouldBe(1, 1e-6);
            stage.Surfaces.OpacityOf(0).ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Lands_The_Surface_At_Once_And_Keeps_A_Short_Fade()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var window = Show(block, ArcMotionLevel.Reduced);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var stage = block.Stage!;
            block.Open(1);
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(16));
            stage.Surfaces.RectOf(1).Width.ShouldBe(stage.Bounds.Width, 0.5);
            stage.PanelOpacity.ShouldBeLessThan(1);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            stage.PanelOpacity.ShouldBe(1, 1e-6);
            block.SelectedRangeIndex = 2;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(16));
            stage.Panel.Chart.WindowIntervals.ShouldBe(363);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Lower_Is_Better_Metrics_Turn_Red_When_They_Rise()
    {
        var burn = new ExplorerMetric("Burn", Series(1000, 1), ExplorerValueFormat.Currency) { IsHigherBetter = false, EndDate = s_end };
        var block = Block(burn);
        var window = Show(block);
        try
        {
            var card = block.Stage!.Cards[0];
            card.Trend.ShouldBe((AnalyticsDirection.Up, AnalyticsTone.Bad));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Ranges_And_Empty_Block_Behave()
    {
        var block = new MetricExplorer();
        var empty = Show(block);
        try
        {
            block.Stage!.Cards.Count.ShouldBe(0);
            block.Open(0);
            Dispatcher.UIThread.RunJobs();
            block.Stage.Panel.IsVisible.ShouldBeFalse();
        }
        finally
        {
            empty.Close();
        }

        var custom = Block();
        custom.Ranges.Add(new ExplorerRange("7D", 7, "7 days", "vs 7 days ago"));
        custom.Ranges.Add(new ExplorerRange("60D", 60, "60 days", "vs 60 days ago"));
        custom.IsOpen = true;
        var window = Show(custom);
        try
        {
            custom.EffectiveRanges.Count.ShouldBe(2);
            custom.Stage!.Panel.Chart.Days.ShouldBe(7);
            custom.SelectedRangeIndex = 1;
            Dispatcher.UIThread.RunJobs();
            custom.Stage.Panel.Chart.Days.ShouldBe(60);
            custom.SelectedRangeIndex = 9;
            custom.Stage.Panel.Chart.Days.ShouldBe(60);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Previous_Period_Line_Can_Be_Hidden()
    {
        var block = Block();
        block.IsOpen = true;
        block.ShowPreviousPeriod = false;
        var window = Show(block);
        try
        {
            block.Stage!.Panel.Chart.Series.ShouldNotBeNull();
            block.ShowPreviousPeriod = true;
            Dispatcher.UIThread.RunJobs();
            block.Stage.Panel.Chart.Series.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
