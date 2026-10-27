using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Motion;
using Xunit;

namespace TKCode.Arc.Tests.Blocks.Analytics;

/// <summary>KPI drilldown: the arithmetic (pure) and the block's behaviour (headless).</summary>
public class KpiDrilldownTests
{
    private static readonly DateOnly End = new(2027, 3, 15);

    // Pure arithmetic -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Period_Splits_Current_And_Prior_And_Changes_Between_Last_Days()
    {
        var series = Enumerable.Range(1, 20).Select(i => (double)i * 10).ToArray();
        var period = KpiDrilldownMath.Period(series, 7, KpiKind.Total, End);
        period.Current.ShouldBe([140, 150, 160, 170, 180, 190, 200]);
        period.Previous.ShouldBe([70, 80, 90, 100, 110, 120, 130]);
        period.Last.ShouldBe(200);
        period.PriorLast.ShouldBe(130);
        period.Change!.Value.ShouldBe((200 - 130) / 130.0 * 100, 1e-9);
        period.FirstDate.ShouldBe(End.AddDays(-6));
        period.DateOf(6).ShouldBe(End);
    }

    [Fact]
    public void Rate_Change_Is_A_Difference_In_Points_And_A_Short_Series_Has_No_Prior()
    {
        KpiDrilldownMath.ChangeOf(27.9, 28.1, KpiKind.Rate)!.Value.ShouldBe(-0.2, 1e-9);
        KpiDrilldownMath.ChangeOf(1, 0, KpiKind.Total).ShouldBeNull();
        var period = KpiDrilldownMath.Period([1, 2, 3], 7, KpiKind.Total, End);
        period.HasPrevious.ShouldBeFalse();
        period.Change.ShouldBeNull();
        period.Current[0].ShouldBe(1);
    }

    [Fact]
    public void Sparkline_Rows_Leave_Two_Of_Thirty_Two_Units_Of_Air()
    {
        double[] n = new double[3];
        KpiDrilldownMath.Normalize([5, 10, 7.5], n);
        n.ShouldBe([0, 1, 0.5]);
        KpiDrilldownMath.SparkY(1, 32).ShouldBe(2);
        KpiDrilldownMath.SparkY(0, 32).ShouldBe(30);
        KpiDrilldownMath.SparkY(0.5, 64).ShouldBe(32);
    }

    [Fact]
    public void Detail_Domain_Is_Min_Max_Of_What_Is_Drawn_And_Maps_To_28_And_178()
    {
        KpiDrilldownMath.Domain([5, 9], [2, 4], compare: true).ShouldBe((2, 9));
        KpiDrilldownMath.Domain([5, 9], [2, 4], compare: false).ShouldBe((5, 9));
        KpiDrilldownMath.PlotY(9, 5, 9).ShouldBe(28);
        KpiDrilldownMath.PlotY(5, 5, 9).ShouldBe(178);
        KpiDrilldownMath.PlotY(7, 5, 9).ShouldBe(103);
        var flat = KpiDrilldownMath.Domain([4, 4], [], false);
        flat.Min.ShouldBeLessThan(4);
        flat.Max.ShouldBeGreaterThan(4);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(34, 14)]
    [InlineData(71, 29)]
    public void Cursor_Index_Maps_To_A_Day_Of_The_Period(int index, int day)
    {
        KpiDrilldownMath.DayOfIndex(index, 72, 30).ShouldBe(day);
        KpiDrilldownMath.IndexOfDay(day, 72, 30).ShouldBe(index);
    }

    [Fact]
    public void Hover_Snaps_To_The_Nearest_Of_72_Points_And_The_Readout_Is_Clamped_70_From_The_Ends()
    {
        KpiDrilldownMath.NearestIndex(0, 1046, 72).ShouldBe(0);
        KpiDrilldownMath.NearestIndex(1046 / 71.0 * 10.4, 1046, 72).ShouldBe(10);
        KpiDrilldownMath.NearestIndex(1200, 1046, 72).ShouldBe(71);
        KpiDrilldownMath.ReadoutCenter(10, 1046).ShouldBe(70);
        KpiDrilldownMath.ReadoutCenter(1040, 1046).ShouldBe(976);
        KpiDrilldownMath.ReadoutCenter(500, 1046).ShouldBe(500);
        KpiDrilldownMath.MiddleTickDay(30).ShouldBe(14);
        KpiDrilldownMath.MiddleTickDay(7).ShouldBe(3);
    }

    [Theory]
    [InlineData(1280, 3, false)]
    [InlineData(760, 3, false)]
    [InlineData(759.6, 2, false)]
    [InlineData(520.2, 2, false)]
    [InlineData(519.3, 1, true)]
    public void Columns_Follow_The_Container_Width(double width, int columns, bool compact)
    {
        KpiDrilldownMath.Columns(width).ShouldBe(columns);
        KpiDrilldownMath.IsCompact(width).ShouldBe(compact);
    }

    [Fact]
    public void Breakdown_Shares_Sum_To_One_And_Changes_Compare_Periods()
    {
        var breakdown = new KpiBreakdown("d", [new KpiBreakdownRow("A", 60, 50), new KpiBreakdownRow("B", 40, 50)]);
        var lines = KpiDrilldownMath.Breakdown(breakdown, 30, KpiKind.Total);
        lines[0].Share.ShouldBe(0.6, 1e-9);
        lines[1].Share.ShouldBe(0.4, 1e-9);
        lines[0].Change!.Value.ShouldBe(20, 1e-9);
        lines[1].Change!.Value.ShouldBe(-20, 1e-9);
    }

    [Fact]
    public void Metric_Formats_Match_The_Notes()
    {
        var arr = new KpiMetric("a", "A") { Format = KpiValueFormat.CurrencyCompact };
        arr.FormatValue(184_300).ShouldBe("$184.3k");
        new KpiMetric { Format = KpiValueFormat.Number }.FormatValue(2418).ShouldBe("2,418");
        new KpiMetric { Format = KpiValueFormat.Percent }.FormatValue(118.5).ShouldBe("118.5%");
        var rate = new KpiMetric { Kind = KpiKind.Rate };
        rate.ChangeFormat.Format(-0.2).ShouldBe("−0.2 pts");
        rate.ChangeFormat.Format(1.1).ShouldBe("+1.1 pts");
        new KpiMetric().ChangeFormat.Format(3.3).ShouldBe("+3.3%");
    }

    // Headless --------------------------------------------------------------------------------------------------------------

    private static double[] Series(double start, double step, int count = 180) =>
        Enumerable.Range(0, count).Select(i => start + i * step + Math.Sin(i * 0.7) * step * 3).ToArray();

    private static KpiDrilldown Block(int metrics = 6, bool dimensions = true)
    {
        var block = new KpiDrilldown { Width = 1152, Title = "Overview", EndDate = End };
        for (var i = 0; i < metrics; i++)
        {
            var metric = new KpiMetric($"m{i}", $"Metric {i}", Series(1000 + i * 100, 2 + i))
            {
                Kind = i == 2 ? KpiKind.Rate : KpiKind.Total,
                Format = i == 2 ? KpiValueFormat.Percent : KpiValueFormat.Number,
                IsLowerBetter = i == 3,
                Description = "A description."
            };
            if (dimensions)
            {
                metric.Breakdowns.Add(new KpiBreakdown("plan", [new KpiBreakdownRow("Alpha", metric.Values![^1] * 0.6, 100), new KpiBreakdownRow("Beta", metric.Values[^1] * 0.4, 100)]));
                metric.Breakdowns.Add(new KpiBreakdown("region", [new KpiBreakdownRow("North", metric.Values![^1] * 0.7, 100), new KpiBreakdownRow("South", metric.Values[^1] * 0.3, 100)]));
            }
            block.Metrics.Add(metric);
        }
        if (dimensions)
        {
            block.Dimensions.Add(new KpiDimension("plan", "Plan"));
            block.Dimensions.Add(new KpiDimension("region", "Region"));
        }
        return block;
    }

    private static Avalonia.Controls.Window Show(KpiDrilldown block, ArcMotionLevel level = ArcMotionLevel.Off)
    {
        var window = TestHelpers.Show(block, level);
        window.Width = 1300;
        window.Height = 1000;
        Pump();
        return window;
    }

    private static void Pump(int rounds = 4)
    {
        for (var i = 0; i < rounds; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Shows_Six_Cards_In_Three_Columns_With_Spoken_Names()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            block.Cards.Count.ShouldBe(6);
            block.ColumnCount.ShouldBe(3);
            block.Classes.Contains(":compact").ShouldBeFalse();
            block.Stage!.Grid!.RowHeights.ShouldAllBe(h => Math.Abs(h - 268) < 1);
            block.Cards[0].Bounds.Width.ShouldBe((1152 - 32 - 24) / 3.0, 1);
            AutomationProperties.GetName(block.Cards[0]).ShouldStartWith("Metric 0, ");
            AutomationProperties.GetName(block.Cards[0]).ShouldEndWith("against the prior 30 days. Show details");
            block.RangeSwitch!.SelectedIndex.ShouldBe(1);
            block.CompareButton!.IsChecked.ShouldBe(true);
            block.Cards[2].Delta!.Text.ShouldContain("pts");
            block.Cards[3].Delta!.Direction.ShouldNotBe(AnalyticsDirection.Flat);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Layout_Follows_The_Blocks_Own_Width()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            block.Width = 640;
            Pump();
            block.ColumnCount.ShouldBe(2);
            block.Classes.Contains(":two-column").ShouldBeTrue();
            block.Width = 400;
            Pump();
            block.ColumnCount.ShouldBe(1);
            block.Classes.Contains(":compact").ShouldBeTrue();
            block.CompareButton!.IsIconOnly.ShouldBeTrue();
            block.Table!.IsCompact.ShouldBeTrue();
            AutomationProperties.GetName(block.CompareButton).ShouldBe("Compare");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_A_Card_Shows_The_Detail_Hides_The_Card_And_Escape_Returns_Focus()
    {
        var block = Block();
        var changes = new List<KpiSelectionChangedEventArgs>();
        block.SelectedChanged += (_, e) => changes.Add(e);
        var window = Show(block);
        try
        {
            block.Cards[1].Focus();
            block.Open("m1");
            Pump();
            block.SelectedKey.ShouldBe("m1");
            block.IsDetailOpen.ShouldBeTrue();
            block.Classes.Contains(":open").ShouldBeTrue();
            changes.Count.ShouldBe(1);
            changes[0].NewKey.ShouldBe("m1");
            block.Stage!.Phase.ShouldBe(KpiPanelPhase.Open);
            block.Cards[1].Opacity.ShouldBe(0);
            block.Stage.Grid!.IsHitTestVisible.ShouldBeFalse();
            block.Cards[0].IsTabStop.ShouldBeFalse();
            block.Stage.PanelRect.Left.ShouldBe(8, 0.5);
            block.Stage.PanelRect.Width.ShouldBe(block.Stage.Bounds.Width - 16, 0.5);
            block.Chart!.Bounds.Height.ShouldBe(220, 0.5);
            block.Chart.DateLabels.Length.ShouldBe(3);
            block.Table!.RowCount.ShouldBe(2);
            block.VersusText.ShouldContain("vs ");
            block.VersusText.ShouldContain("30 days");

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Pump();
            block.SelectedKey.ShouldBeNull();
            block.IsDetailOpen.ShouldBeFalse();
            block.Cards[1].Opacity.ShouldBe(1);
            block.Stage.Phase.ShouldBe(KpiPanelPhase.Closed);
            changes.Count.ShouldBe(2);
            changes[1].NewKey.ShouldBeNull();
            block.Cards[0].IsTabStop.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Panel_Grows_On_One_Spring_From_The_Card_Box_And_Folds_Back()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var window = Show(block, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var card = block.Cards[0];
            var cardBox = card.Bounds.Translate(block.Stage!.Grid!.Bounds.Position);
            block.Open("m0");
            Pump();
            var start = block.Stage.PanelRect;
            start.Width.ShouldBe(cardBox.Width, 1);
            start.Height.ShouldBe(cardBox.Height, 1);
            block.Stage.BodyOpacity.ShouldBe(0, 0.05);

            // The body waits 120 ms before it fades in.
            clock.Advance(TimeSpan.FromMilliseconds(100));
            block.Stage.BodyOpacity.ShouldBe(0, 0.001);
            // 170 ms in (the notes: 50% at about 170 ms): between the card and the final box.
            clock.Advance(TimeSpan.FromMilliseconds(70));
            Pump(2);
            var mid = block.Stage.PanelRect;
            var target = block.Stage.PanelTarget;
            var progress = (mid.Width - start.Width) / (target.Width - start.Width);
            progress.ShouldBeInRange(0.35, 0.65);
            // 50 ms into the 480 ms (.16, 1, .3, 1) fade the body is on its way.
            block.Stage.BodyOpacity.ShouldBeInRange(0.2, 0.9);
            // The area's height follows the panel.
            block.Stage.Bounds.Height.ShouldBeGreaterThanOrEqualTo(mid.Height + 16 - 1);

            clock.Advance(TimeSpan.FromMilliseconds(1200));
            Pump(2);
            block.Stage.PanelRect.Width.ShouldBe(target.Width, 1);
            block.Stage.BodyOpacity.ShouldBe(1, 0.01);

            block.Close();
            Pump();
            block.Stage.Phase.ShouldBe(KpiPanelPhase.Closing);
            block.Cards[0].Opacity.ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(130));
            block.Stage.BodyOpacity.ShouldBe(0, 0.02);
            clock.Advance(TimeSpan.FromMilliseconds(1500));
            Pump(2);
            block.Stage.Phase.ShouldBe(KpiPanelPhase.Closed);
            block.Cards[0].Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Shows_The_Final_Box_And_Fades()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var block = Block();
        var window = Show(block, ArcMotionLevel.Reduced);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            block.Open("m0");
            Pump();
            var target = block.Stage!.PanelTarget;
            block.Stage.PanelRect.Width.ShouldBe(target.Width, 0.5);
            block.Stage.PanelOpacity.ShouldBeLessThan(0.99);
            clock.Advance(TimeSpan.FromMilliseconds(300));
            block.Stage.PanelOpacity.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Range_Switch_Retargets_Every_Card_And_Raises_The_Event()
    {
        var block = Block();
        var changes = new List<KpiRangeChangedEventArgs>();
        block.RangeChanged += (_, e) => changes.Add(e);
        var window = Show(block);
        try
        {
            var before = block.Cards[0].Delta!.Text;
            block.RangeSwitch!.SelectedIndex = 0;
            Pump();
            block.RangeDays.ShouldBe(7);
            changes.Count.ShouldBe(1);
            changes[0].OldDays.ShouldBe(30);
            changes[0].NewDays.ShouldBe(7);
            block.Cards[0].Delta!.Text.ShouldNotBe(before);
            AutomationProperties.GetName(block.Cards[0])!.ShouldContain("prior 7 days");
            block.Cards[0].Sparkline!.Rows.Count.ShouldBe(40);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Compare_Off_Shrinks_The_Domain_Hides_The_Prior_Line_And_The_Readout_Row()
    {
        var block = Block();
        var events = new List<bool>();
        block.CompareChanged += (_, e) => events.Add(e.IsEnabled);
        var window = Show(block);
        try
        {
            block.Open("m0");
            Pump();
            var chart = block.Chart!;
            var wide = chart.DomainTarget;
            chart.PriorOpacity.ShouldBe(1);
            chart.ContentFor(10).Rows.ShouldNotBeNull();
            block.IsCompareEnabled = false;
            Pump();
            events.ShouldBe([false]);
            chart.PriorOpacity.ShouldBe(0);
            chart.DomainTarget.Min.ShouldBeGreaterThanOrEqualTo(wide.Min);
            chart.DomainTarget.Max.ShouldBeLessThanOrEqualTo(wide.Max);
            chart.ContentFor(10).Rows.ShouldBeNull();
            block.Classes.Contains(":compare-off").ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chart_Keyboard_Moves_The_Cursor_By_Days_And_Announces()
    {
        var block = Block();
        var window = Show(block);
        try
        {
            block.Open("m0");
            Pump();
            var chart = block.Chart!;
            chart.Focus(NavigationMethod.Tab);
            Pump();
            chart.CursorIndex.ShouldBe(-1);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Pump();
            chart.CursorDay.ShouldBe(29);
            chart.CursorIndex.ShouldBe(71);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            Pump();
            chart.CursorDay.ShouldBe(28);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            Pump();
            chart.CursorDay.ShouldBe(0);
            chart.CursorIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Pump();
            chart.CursorDay.ShouldBe(29);
            chart.Readout.IsShown.ShouldBeTrue();
            chart.ReadoutContent!.Title.ShouldBe(AnalyticsFormat.Date(End, "MMM d"));
            block.Announcer!.History.Count.ShouldBeGreaterThan(0);
            AutomationProperties.GetName(chart)!.ShouldContain("across the last 30 days");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dimension_Switch_Changes_The_Table_And_Raises_The_Event()
    {
        var block = Block();
        var keys = new List<string?>();
        block.DimensionChanged += (_, e) => keys.Add(e.NewKey);
        var window = Show(block);
        try
        {
            block.Open("m0");
            Pump();
            block.Table!.RowParts(0).Name.Text.ShouldBe("Alpha");
            block.DimensionSwitch!.SelectedIndex = 1;
            Pump();
            keys.ShouldBe(["region"]);
            block.SelectedDimensionKey.ShouldBe("region");
            block.Table.RowParts(0).Name.Text.ShouldBe("North");
            block.Table.RowParts(0).Bar.Value.ShouldBe(0.7, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_Dimensions_The_Breakdown_Is_Left_Out()
    {
        var block = Block(4, dimensions: false);
        var window = Show(block);
        try
        {
            block.Open("m0");
            Pump();
            TestHelpers.Descendant<Avalonia.Controls.Border>(block, KpiDrilldown.BreakdownPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
