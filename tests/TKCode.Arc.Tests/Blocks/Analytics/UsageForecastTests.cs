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

/// <summary>Usage forecast: the engine (series, crossings, outcomes, thresholds, drag rules) and the block (headless).</summary>
public class UsageForecastTests
{
    private static UsageSeries Linear(double perDay = 1, int today = 20, int days = 30)
    {
        // Mondays to Sundays do not matter: every day adds the same, the weekend ratio is overridden by explicit forecast.
        var readings = Enumerable.Range(1, today).Select(d => d * perDay).ToArray();
        var forecast = Enumerable.Range(today + 1, days - today).Select(d => d * perDay).ToArray();
        return UsageSeries.Build(days, today, 0, readings, new DateOnly(2027, 6, 1), forecast);
    }

    #region Engine

    [Fact]
    public void Crossing_Interpolates_Inside_The_First_Segment_That_Reaches_The_Level()
    {
        var series = Linear();
        UsageForecastEngine.Crossing(series.Line, 25.5)!.Value.ShouldBe(25.5, 1e-9);
        UsageForecastEngine.Crossing(series.Line, 100).ShouldBeNull();
        UsageForecastEngine.Crossing(series.Line, 0)!.Value.ShouldBe(0);
        // Notes: 25.15 shows day 26; 27.76 shows day 28.
        UsageForecastEngine.DayOf(25.15).ShouldBe(26);
        UsageForecastEngine.DayOf(27.76).ShouldBe(28);
        UsageForecastEngine.DayOf(26.0).ShouldBe(26);
    }

    [Fact]
    public void Analysis_Names_The_Four_Outcomes()
    {
        var series = Linear();
        UsageForecastEngine.Analyze(series, 12.5, 0, []).Outcome.ShouldBe(UsageOutcome.Passed);
        UsageForecastEngine.Analyze(series, 25.5, 0, []).Outcome.ShouldBe(UsageOutcome.WillCross);
        UsageForecastEngine.Analyze(series, 29.5, 0, []).Outcome.ShouldBe(UsageOutcome.CrossesOnLastDay);
        UsageForecastEngine.Analyze(series, 35, 0, []).Outcome.ShouldBe(UsageOutcome.StaysUnder);
        var will = UsageForecastEngine.Analyze(series, 25.5, 2, []);
        will.CrossingDay.ShouldBe(26);
        will.OverageCost.ShouldBe((30 - 25.5) * 2, 1e-9);
        will.Used.ShouldBe(20);
        will.UsedPercent.ShouldBe(78);
        UsageForecastEngine.Analyze(series, 35, 2, []).OverageCost.ShouldBe(0);
    }

    [Fact]
    public void Thresholds_Report_Since_Reached_Expected_And_Not_Expected()
    {
        var series = Linear(2, today: 20, days: 30);
        var a = UsageForecastEngine.Analyze(series, 50, 0, [2, 50, 80, 100, 130]);
        a.Thresholds[0].Status.ShouldBe(UsageAlertStatus.AlreadyPast);
        a.Thresholds[0].Day.ShouldBe(1);
        a.Thresholds[1].Status.ShouldBe(UsageAlertStatus.Reached);
        a.Thresholds[1].Day.ShouldBe(13);
        a.Thresholds[2].Status.ShouldBe(UsageAlertStatus.Reached);
        a.Thresholds[2].Day.ShouldBe(20);
        a.Thresholds[3].Status.ShouldBe(UsageAlertStatus.Expected);
        a.Thresholds[3].Day.ShouldBe(25);
        a.Thresholds[4].Status.ShouldBe(UsageAlertStatus.NotExpected);
        a.Thresholds[4].X.ShouldBeNull();
    }

    [Fact]
    public void Cone_Is_Zero_Wide_Today_Follows_H_To_The_Two_Thirds_And_Is_Asymmetric()
    {
        var readings = Enumerable.Range(1, 21).Select(d => d * 1.2).ToArray();
        var series = UsageSeries.Build(30, 21, 0, readings, new DateOnly(2027, 6, 1));
        series.Upper[21].ShouldBe(series.Line[21], 1e-9);
        series.Lower[21].ShouldBe(series.Line[21], 1e-9);
        var up1 = series.Upper[22] - series.Line[22];
        var up8 = series.Upper[29] - series.Line[29];
        (up8 / up1).ShouldBe(Math.Pow(8, 2.0 / 3.0), 1e-9);
        for (var d = 22; d <= 30; d++)
        {
            (series.Line[d] - series.Lower[d]).ShouldBe(0.70 * (series.Upper[d] - series.Line[d]), 1e-9);
        }
        // The first spread is 0.37 weekday increments (1.2).
        up1.ShouldBe(0.37 * 1.2, 1e-9);
    }

    [Fact]
    public void Forecast_Adds_Weekend_Days_At_0_43_Of_A_Weekday()
    {
        // Jun 1 2027 is a Tuesday: weekends are days 5-6, 12-13, 19-20, 26-27.
        var start = new DateOnly(2027, 6, 1);
        var readings = new double[21];
        var sum = 0.0;
        for (var d = 1; d <= 21; d++)
        {
            sum += start.AddDays(d - 1).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0.43 : 1;
            readings[d - 1] = sum;
        }
        var series = UsageSeries.Build(30, 21, 0, readings, start);
        series.IsWeekend(26).ShouldBeTrue();
        var weekend = series.Line[26] - series.Line[25];
        var weekday = series.Line[25] - series.Line[24];
        (weekend / weekday).ShouldBe(0.43 * (1 + 0.005 * 5) / (1 + 0.005 * 4), 1e-9);
    }

    [Fact]
    public void Drag_Rules_Snap_Squeeze_And_Fade()
    {
        UsageForecastEngine.Snap(26.3, 8, 0.5).ShouldBe(26.5, 1e-9);
        UsageForecastEngine.Snap(26.2, 8, 0.5).ShouldBe(26.0, 1e-9);
        UsageForecastEngine.Rubber(0).ShouldBe(0);
        UsageForecastEngine.Rubber(41).ShouldBeInRange(8, 10);
        UsageForecastEngine.Rubber(151).ShouldBeInRange(11.5, 13);
        UsageForecastEngine.Rubber(100000).ShouldBeLessThanOrEqualTo(13);
        UsageForecastEngine.LabelFade(100, 100).ShouldBe(0);
        UsageForecastEngine.LabelFade(131.1, 100).ShouldBe(0.26, 0.01);
        UsageForecastEngine.LabelFade(153.9, 100).ShouldBe(1);
    }

    [Fact]
    public void Domain_Uses_The_Metric_Axis_Or_Picks_A_Round_Top()
    {
        UsageForecastEngine.Domain(0, 30, 10, 20).ShouldBe((0, 30));
        var (min, max) = UsageForecastEngine.Domain(0, double.NaN, 34, 32);
        min.ShouldBe(0);
        max.ShouldBeGreaterThanOrEqualTo(34);
        (max / 5).ShouldBeOneOf(5, 10, 12.5, 7.5, 6, 8, 7, 10, 20, 25);
    }

    #endregion

    #region Block

    private static Window Show(UsageForecast block, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(block, level);
        window.Width = 900;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Block_Shows_The_Measured_Numbers_Of_The_Sample_Cycle()
    {
        var block = UsageForecastTestData.Block();
        var window = Show(block);
        try
        {
            block.SelectedMetric.ShouldBe(block.Metrics[0]);
            block.Outcome.ShouldBe(UsageOutcome.WillCross);
            block.CrossingDay.ShouldNotBeNull();
            block.OverageCost.ShouldBeGreaterThan(0);
            block.Classes.Contains(":over-budget").ShouldBeTrue();
            block.Headline!.Text.ShouldContain("hit the budget on Jun");
            block.Detail!.Text.ShouldContain("before the cycle ends");
            var (used, forecast, cost) = block.Tiles;
            used!.Number.Amount.ShouldBe(block.Series.Line[21], 1e-9);
            forecast!.Number.Amount.ShouldBe(block.ProjectedTotal, 1e-9);
            cost!.IsVisible.ShouldBeTrue();
            cost.Number.Amount.ShouldBe(block.OverageCost, 1e-6);
            cost.Tone.ShouldBe(1);
            block.Chips.Count.ShouldBe(3);
            block.AlertThresholds.Select(t => t.IsEnabled).ShouldBe([false, true, true]);
            block.AlertThresholds[0].Status.ShouldBe(UsageAlertStatus.Reached);
            block.AlertThresholds[2].Status.ShouldBe(UsageAlertStatus.Expected);
            block.Table!.RowHeaders.Count.ShouldBe(30);
            block.Table.RowHeaders[29].ShouldContain("(projected)");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Budget_Above_The_Forecast_Stays_Under_With_Free_Overage_And_Hidden_Marker()
    {
        var block = UsageForecastTestData.Block();
        var window = Show(block);
        try
        {
            block.Metrics[0].Budget = 40;
            Dispatcher.UIThread.RunJobs();
            block.Outcome.ShouldBe(UsageOutcome.StaysUnder);
            block.CrossingDay.ShouldBeNull();
            block.Headline!.Text.ShouldBe("On this pace you finish under budget");
            block.Tiles.Cost!.Number.Amount.ShouldBe(0);
            block.Tiles.Cost.Tone.ShouldBe(0);
            block.Classes.Contains(":over-budget").ShouldBeFalse();
            block.Plot!.IsCrossingVisible.ShouldBeFalse();
            block.AlertThresholds[2].Status.ShouldBe(UsageAlertStatus.NotExpected);
            block.Chips[block.AlertThresholds[2]].StatusText.ShouldBe("Not on track");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Budget_Below_Usage_Says_The_Budget_Was_Passed()
    {
        var block = UsageForecastTestData.Block();
        var window = Show(block);
        try
        {
            block.Metrics[0].Budget = 12;
            Dispatcher.UIThread.RunJobs();
            block.Outcome.ShouldBe(UsageOutcome.Passed);
            block.Headline!.Text.ShouldStartWith("You went over budget on");
            block.Detail!.Text.ShouldContain("above budget now");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Handle_Keys_Step_Page_And_Jump_The_Budget()
    {
        var block = UsageForecastTestData.Block();
        var window = Show(block);
        try
        {
            var metric = block.Metrics[0];
            var committed = new List<(double Value, bool Committed)>();
            block.BudgetChanged += (_, e) => committed.Add((e.NewValue, e.IsCommitted));
            block.Handle!.Focus();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            metric.Budget.ShouldBe(26.5, 1e-9);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            metric.Budget.ShouldBe(26, 1e-9);
            window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            metric.Budget.ShouldBe(28.5, 1e-9);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            metric.Budget.ShouldBe(8, 1e-9);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            metric.Budget.ShouldBe(40, 1e-9);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            metric.Budget.ShouldBe(40, 1e-9);
            committed.ShouldNotBeEmpty();
            committed.All(c => c.Committed).ShouldBeTrue();
            block.Announcer!.History.Last().ShouldContain("Budget");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Drag_Is_Relative_Snaps_To_The_Step_And_Reports_Interactive_Then_Committed()
    {
        var block = UsageForecastTestData.Block();
        var window = Show(block);
        try
        {
            var plot = block.Plot!;
            var handle = block.Handle!;
            var events = new List<(double Value, bool Committed)>();
            block.BudgetChanged += (_, e) => events.Add((e.NewValue, e.IsCommitted));
            var start = plot.TranslatePoint(new Point(plot.X(5), plot.DisplayedBudgetY + 3), window)!.Value;
            window.MouseMove(start, RawInputModifiers.None);
            window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
            // Pressing off the line centre does not jump the value.
            block.Metrics[0].Budget.ShouldBe(26, 1e-9);
            var up = new Point(start.X, start.Y - 2 * plot.PixelsPerUnit);
            window.MouseMove(up, RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            block.Metrics[0].Budget.ShouldBe(28, 1e-9);
            block.Classes.Contains(":dragging").ShouldBeTrue();
            events.Last().Committed.ShouldBeFalse();
            window.MouseUp(up, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            events.Last().Committed.ShouldBeTrue();
            block.Classes.Contains(":dragging").ShouldBeFalse();
            handle.IsDragging.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Drag_Beyond_The_Range_Keeps_The_Limit_And_Squeezes_The_Line()
    {
        var block = UsageForecastTestData.Block();
        var window = Show(block);
        try
        {
            var plot = block.Plot!;
            var start = plot.TranslatePoint(new Point(plot.X(5), plot.DisplayedBudgetY), window)!.Value;
            window.MouseMove(start, RawInputModifiers.None);
            window.MouseDown(start, MouseButton.Left, RawInputModifiers.None);
            var far = new Point(start.X, start.Y - 400);
            window.MouseMove(far, RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs();
            block.Metrics[0].Budget.ShouldBe(40, 1e-9);
            var limit = plot.Y(40);
            plot.TargetBudgetY.ShouldBeLessThan(limit);
            (limit - plot.TargetBudgetY).ShouldBeInRange(8, 13);
            window.MouseUp(far, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            plot.TargetBudgetY.ShouldBe(limit, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Alert_Chip_Toggles_Threshold_And_Raises_The_Event()
    {
        var block = UsageForecastTestData.Block();
        var window = Show(block);
        try
        {
            var toggled = new List<(double, bool)>();
            block.AlertThresholdToggled += (_, e) => toggled.Add((e.Threshold.Percent, e.IsEnabled));
            var threshold = block.AlertThresholds[0];
            var chip = block.Chips[threshold];
            chip.IsChecked.ShouldBe(false);
            chip.IsChecked = true;
            threshold.IsEnabled.ShouldBeTrue();
            toggled.ShouldBe([(50, true)]);
            threshold.IsEnabled = false;
            chip.IsChecked.ShouldBe(false);
            toggled.Count.ShouldBe(2);
            block.Announcer!.History.Last().ShouldContain("50%");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Metric_Switch_Changes_Everything_And_Raises_The_Event()
    {
        var block = UsageForecastTestData.Block();
        var window = Show(block);
        try
        {
            UsageMetric? seen = null;
            block.MetricChanged += (_, e) => seen = e.Metric;
            block.MetricSwitch!.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            block.SelectedMetric.ShouldBe(block.Metrics[2]);
            block.SelectedMetricKey.ShouldBe("editors");
            seen.ShouldBe(block.Metrics[2]);
            block.Outcome.ShouldBe(UsageOutcome.Passed);
            block.Plot!.Data.AxisMin.ShouldBe(50);
            block.Plot.TickText(5).ShouldBe("80 editors");
            block.Tiles.Used!.Number.Amount.ShouldBe(block.Series.Line[21], 1e-9);
            block.Announcer!.History.Last().ShouldStartWith("Editors:");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Single_Metric_Hides_The_Switch_And_Zero_Price_Hides_The_Cost()
    {
        var metric = UsageForecastTestData.Events();
        metric.UnitPrice = 0;
        var block = UsageForecastTestData.Block(metric);
        var window = Show(block);
        try
        {
            block.MetricSwitch!.IsVisible.ShouldBeFalse();
            block.Tiles.Cost!.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Inspector_Keys_Move_By_Day_And_Week_And_Escape_Hides_The_Card()
    {
        var block = UsageForecastTestData.Block();
        var window = Show(block);
        try
        {
            var days = new List<int?>();
            block.InspectedDayChanged += (_, e) => days.Add(e.Day);
            block.Plot!.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            block.InspectedDay.ShouldBe(21);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            block.InspectedDay.ShouldBe(22);
            window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            block.InspectedDay.ShouldBe(29);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            block.InspectedDay.ShouldBe(30);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            block.InspectedDay.ShouldBe(30);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            block.InspectedDay.ShouldBe(1);
            block.Plot.PanelContent.Tag.ShouldBe("Measured");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            block.InspectedDay.ShouldBeNull();
            block.Plot.PanelAlpha.ShouldBe(0, 1e-9);
            days.ShouldContain((int?)22);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Panel_Flips_To_The_Left_Near_The_Right_Edge_And_Shows_Ranges_For_Forecast_Days()
    {
        var block = UsageForecastTestData.Block();
        var window = Show(block);
        try
        {
            block.InspectedDay = 28;
            Dispatcher.UIThread.RunJobs();
            var plot = block.Plot!;
            plot.PanelContent.Tag.ShouldBe("Projected");
            plot.PanelContent.Detail.ShouldStartWith("Range ");
            block.InspectedDay = 3;
            Dispatcher.UIThread.RunJobs();
            plot.PanelContent.Detail.ShouldEndWith("% of budget");
            block.InspectedDay = 21;
            plot.PanelContent.Tag.ShouldBe("Today");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Breakpoints_Follow_The_Block_Width()
    {
        var block = UsageForecastTestData.Block();
        var window = Show(block);
        try
        {
            block.Classes.Contains(":compact").ShouldBeFalse();
            block.Width = 520;
            Dispatcher.UIThread.RunJobs();
            block.Classes.Contains(":compact").ShouldBeTrue();
            block.Classes.Contains(":narrow").ShouldBeFalse();
            block.Plot!.Bounds.Height.ShouldBe(232, 0.5);
            block.Width = 400;
            Dispatcher.UIThread.RunJobs();
            block.Classes.Contains(":narrow").ShouldBeTrue();
            block.Tiles.Used!.IsStacked.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Motion

    [AvaloniaFact]
    public void Key_Moves_The_Line_On_A_Spring_And_The_Marker_Follows_It_Along_The_Path()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = UsageForecastTestData.Block();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var plot = block.Plot!;
            var x0 = plot.DisplayedCrossingX;
            var y0 = plot.DisplayedBudgetY;
            block.Handle!.Focus();
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            clock.Advance(TimeSpan.FromMilliseconds(60));
            plot.DisplayedBudgetY.ShouldBeGreaterThan(y0 + 5);
            plot.DisplayedBudgetY.ShouldBeLessThan(plot.TargetBudgetY);
            // Mid flight the marker sits on the displayed line at its own x.
            clock.Advance(TimeSpan.FromMilliseconds(100));
            plot.IsMoving.ShouldBeTrue();
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            plot.IsMoving.ShouldBeFalse();
            plot.DisplayedBudgetY.ShouldBe(plot.Y(8), 0.05);
            block.Outcome.ShouldBe(UsageOutcome.Passed);
            plot.DisplayedCrossingX.ShouldBeLessThan(x0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Metric_Switch_Morphs_Every_Vertex_On_One_Spring()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = UsageForecastTestData.Block();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var plot = block.Plot!;
            var before = plot.DisplayedY(0, 10);
            block.SelectedMetric = block.Metrics[1];
            Dispatcher.UIThread.RunJobs();
            var target = plot.Y(block.Series.Line[10]);
            clock.Advance(TimeSpan.FromMilliseconds(120));
            var mid = plot.DisplayedY(0, 10);
            Math.Abs(mid - before).ShouldBeGreaterThan(1);
            Math.Abs(mid - target).ShouldBeGreaterThan(1);
            plot.OutgoingTickAlpha(5).ShouldBeGreaterThan(0);
            clock.Advance(TimeSpan.FromMilliseconds(2000));
            plot.DisplayedY(0, 10).ShouldBe(target, 0.1);
            plot.IsMoving.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changed_Words_Swap_With_A_Stagger_And_Unchanged_Words_Stay()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = UsageForecastTestData.Block();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var headline = block.Headline!;
            block.Metrics[0].Budget = 40;
            Dispatcher.UIThread.RunJobs();
            headline.LeavingCount.ShouldBeGreaterThan(0);
            var words = headline.Words;
            words.First(w => w.Text == "On").Alpha.ShouldBe(1);
            words.First(w => w.Text == "under").Alpha.ShouldBe(0, 1e-9);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            headline.Words.First(w => w.Text == "under").Alpha.ShouldBeGreaterThan(0);
            clock.Advance(TimeSpan.FromMilliseconds(800));
            headline.LeavingCount.ShouldBe(0);
            headline.Words.All(w => Math.Abs(w.Alpha - 1) < 1e-9).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chip_Check_Slot_Opens_On_The_Quick_Spring_And_Status_Text_Morphs()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = UsageForecastTestData.Block();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var chip = block.Chips[block.AlertThresholds[0]];
            chip.SlotNow.ShouldBe(0);
            chip.IsChecked = true;
            clock.Advance(TimeSpan.FromMilliseconds(80));
            chip.SlotNow.ShouldBeInRange(1, 19.9);
            clock.Advance(TimeSpan.FromMilliseconds(800));
            chip.SlotNow.ShouldBe(20, 0.1);
            var before = chip.StatusText;
            block.Metrics[0].Budget = 40;
            Dispatcher.UIThread.RunJobs();
            chip.StatusText.ShouldNotBe(before);
            clock.Advance(TimeSpan.FromMilliseconds(1000));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Off_Motion_Jumps_Everything()
    {
        var block = UsageForecastTestData.Block();
        var window = Show(block, ArcMotionLevel.Off);
        try
        {
            var plot = block.Plot!;
            block.Metrics[0].Budget = 10;
            Dispatcher.UIThread.RunJobs();
            plot.DisplayedBudgetY.ShouldBe(plot.Y(10), 1e-6);
            block.SelectedMetric = block.Metrics[1];
            Dispatcher.UIThread.RunJobs();
            plot.DisplayedY(0, 10).ShouldBe(plot.Y(block.Series.Line[10]), 1e-6);
            plot.IsMoving.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
