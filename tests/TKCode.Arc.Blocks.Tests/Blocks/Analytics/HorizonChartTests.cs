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

/// <summary>Horizon chart: the pure engine asserts the measured numbers; the block is checked headless.</summary>
public class HorizonChartTests
{
    #region Engine: fold

    [Theory]
    [InlineData(1, 0.9200)]
    [InlineData(2, 0.7172)]
    [InlineData(3, 0.5691)]
    [InlineData(4, 0.4682)]
    public void Layer_Alpha_Is_One_Minus_Point_Oh_Eight_To_One_Over_Bands(int bands, double alpha)
    {
        HorizonEngine.LayerAlpha(bands).ShouldBe(alpha, 5e-5);
        // All bands stacked always reach 0.92.
        HorizonEngine.StackedAlpha(bands, bands).ShouldBe(0.92, 1e-12);
    }

    [Fact]
    public void Stacked_Alpha_Matches_The_Measured_Ramp()
    {
        HorizonEngine.StackedAlpha(1, 3).ShouldBe(0.569, 1e-3);
        HorizonEngine.StackedAlpha(2, 3).ShouldBe(0.814, 1e-3);
        HorizonEngine.StackedAlpha(2, 2).ShouldBe(0.920, 1e-3);
        HorizonEngine.StackedAlpha(2, 4).ShouldBe(0.717, 1e-3);
        HorizonEngine.StackedAlpha(3, 4).ShouldBe(0.850, 1e-3);
        // A fold frozen between bands recomputes alpha from the continuous count (2.98977 → 0.5704).
        HorizonEngine.LayerAlpha(2.98977).ShouldBe(0.5704, 1e-4);
    }

    [Fact]
    public void Layer_K_Only_Shows_Where_B_Times_V_Exceeds_K_Minus_One()
    {
        // B 3, v 0.5: layer 1 is full (1.5 ≥ 1), layer 2 half, layer 3 nothing.
        HorizonEngine.LayerFill(0.5, 3, 1).ShouldBe(1);
        HorizonEngine.LayerFill(0.5, 3, 2).ShouldBe(0.5, 1e-12);
        HorizonEngine.LayerFill(0.5, 3, 3).ShouldBe(0);
        HorizonEngine.LayerFill(1, 3, 3).ShouldBe(1);
        HorizonEngine.IsLayerVisible(3, 3).ShouldBeTrue();
        HorizonEngine.IsLayerVisible(4, 3).ShouldBeFalse();
        // Mid-fold at 2.5 the fourth layer is still empty, the third is live.
        HorizonEngine.IsLayerVisible(3, 2.5).ShouldBeTrue();
        HorizonEngine.IsLayerVisible(4, 2.5).ShouldBeFalse();
        HorizonEngine.IsLayerVisible(4, 3.01).ShouldBeTrue();
    }

    [Fact]
    public void Layer_Matrix_Maps_The_Unit_Polygon_With_Y_Prime_Equals_B_Y_Plus_K()
    {
        var m = HorizonEngine.LayerMatrix(100, 20, 118, 28, 119, 3, 2);
        // x: day 118 lands on the right edge of the plot.
        m.Transform(new Point(118, 0)).X.ShouldBe(218, 1e-9);
        // Baseline (y 0) of layer 2 sits at plotY + 2·h (below the strip, clipped).
        m.Transform(new Point(0, 0)).Y.ShouldBe(20 + 2 * 28, 1e-9);
        // A value of 2/3 of the extent (B·v = 2) reaches the strip's top edge.
        m.Transform(new Point(0, -2.0 / 3)).Y.ShouldBe(20, 1e-9);
        // Layer 1 reaches the top at v = 1/3.
        HorizonEngine.LayerMatrix(100, 20, 118, 28, 119, 3, 1).Transform(new Point(0, -1.0 / 3)).Y.ShouldBe(20, 1e-9);
    }

    [Fact]
    public void Unit_Polygon_Has_The_Measured_Command_Count_And_Both_Sides_Grow_From_The_Bottom()
    {
        var values = new double[119];
        values[5] = 12;
        values[6] = -36;
        values[7] = -72;
        var points = new Point[121];
        HorizonEngine.UnitPolygon(values, 36, upper: true, points);
        points.Length.ShouldBe(119 + 2);
        points[0].ShouldBe(new Point(0, 0));
        points[120].ShouldBe(new Point(118, 0));
        points[6].Y.ShouldBe(-12.0 / 36, 1e-12);
        points[7].Y.ShouldBe(0);
        HorizonEngine.UnitPolygon(values, 36, upper: false, points);
        points[7].Y.ShouldBe(-1, 1e-12);
        // Above the extent clamps to 1.
        points[8].Y.ShouldBe(-1, 1e-12);
        points[6].Y.ShouldBe(0);
    }

    [Theory]
    [InlineData(35, 36)]
    [InlineData(36, 36)]
    [InlineData(36.4, 48)]
    [InlineData(5, 12)]
    [InlineData(0, 12)]
    public void Auto_Extent_Rounds_Up_To_A_Multiple_Of_Twelve(double maxAbs, double extent)
    {
        HorizonEngine.AutoExtent(maxAbs).ShouldBe(extent);
    }

    [Fact]
    public void Fold_Spring_Is_Critically_Damped_With_The_Measured_Frequency()
    {
        var spec = AnalyticsSprings.Fold;
        spec.Stiffness.ShouldBe(75, 1.5);
        spec.Damping.ShouldBe(17.4, 0.3);
        spec.NaturalFrequency.ShouldBe(8.7, 0.1);
        // 3 → 1 on the closed form x(t) = 1 + 2 (1 + ωt) e^{−ωt}: never below 1, 99% done at about 760ms.
        var channel = new AnalyticsSpringChannel(3, 0.0005, 0.005);
        channel.SpringTo(1, spec);
        var omega = spec.NaturalFrequency;
        var t = 0.0;
        var lowest = 3.0;
        for (var i = 0; i < 120; i++)
        {
            channel.Step(1.0 / 120);
            t += 1.0 / 120;
            lowest = Math.Min(lowest, channel.Value);
            channel.Value.ShouldBe(1 + 2 * (1 + omega * t) * Math.Exp(-omega * t), 0.003);
        }
        lowest.ShouldBeGreaterThanOrEqualTo(1 - 1e-9);
        var settled = new AnalyticsSpringChannel(3, 0.0005, 0.005);
        settled.SpringTo(1, spec);
        var at99 = 0.0;
        for (var i = 0; i < 400 && settled.Value - 1 > 0.02; i++)
        {
            settled.Step(0.002);
            at99 += 0.002;
        }
        at99.ShouldBeInRange(0.65, 0.85);
    }

    [Fact]
    public void Reorder_And_Scrub_Springs_Match_Their_Fits()
    {
        AnalyticsSprings.Reorder.Stiffness.ShouldBe(89, 2);
        AnalyticsSprings.Reorder.Damping.ShouldBe(18.8, 0.4);
        AnalyticsSprings.Scrub.Stiffness.ShouldBe(1000, 100);
        AnalyticsSprings.Scrub.Damping.ShouldBe(63, 4);
        // The scrub reaches a far target in about 250ms.
        var channel = new AnalyticsSpringChannel(0, 0.0005, 0.005);
        channel.SpringTo(1, AnalyticsSprings.Scrub);
        var t = 0.0;
        while (channel.IsMoving && t < 2)
        {
            channel.Step(0.004);
            t += 0.004;
        }
        t.ShouldBeInRange(0.2, 0.4);
    }

    #endregion

    #region Engine: layout, hit, sort, legend

    [Fact]
    public void Layout_Columns_Match_The_Measured_Widths()
    {
        // Wide: block 1152 → content 1150; the band column is 870 wide starting at 176.
        var wide = HorizonEngine.Resolve(1150);
        wide.Tier.ShouldBe(HorizonTier.Wide);
        wide.PlotLeft.ShouldBe(176);
        wide.PlotWidth.ShouldBe(870, 1e-9);
        wide.ValueRight.ShouldBe(1126);
        wide.TotalHeight(14).ShouldBe(8 + 14 * 36 + 26 + 16);
        // Medium (container 738 − 2): 116 | 488 | 52, gap 16.
        var medium = HorizonEngine.Resolve(736);
        medium.Tier.ShouldBe(HorizonTier.Medium);
        medium.PlotWidth.ShouldBe(488, 1e-9);
        // Compact (504 − 2): 84 | 322 | 44, gap 10, rows 34.
        var compact = HorizonEngine.Resolve(502);
        compact.Tier.ShouldBe(HorizonTier.Compact);
        compact.PlotWidth.ShouldBe(322, 1e-9);
        compact.Metrics.RowHeight.ShouldBe(34);
        HorizonEngine.Resolve(358 - 2).PlotWidth.ShouldBe(176 - 2 + 2 - 0, 1e-9);
    }

    [Fact]
    public void Tier_Switches_At_900_And_560_Of_The_Container()
    {
        HorizonEngine.TierFor(900.5).ShouldBe(HorizonTier.Wide);
        HorizonEngine.TierFor(900).ShouldBe(HorizonTier.Medium);
        HorizonEngine.TierFor(561).ShouldBe(HorizonTier.Medium);
        HorizonEngine.TierFor(560).ShouldBe(HorizonTier.Compact);
    }

    [Fact]
    public void Day_At_Rounds_The_Position_Over_The_Band_Column()
    {
        // Measured: x 500 over the 241…1111 column of 119 days is day 35, x 400 is day 22.
        HorizonEngine.DayAt(500, 241, 870, 119).ShouldBe(35);
        HorizonEngine.DayAt(400, 241, 870, 119).ShouldBe(22);
        HorizonEngine.DayAt(0, 241, 870, 119).ShouldBe(0);
        HorizonEngine.DayAt(5000, 241, 870, 119).ShouldBe(118);
        HorizonEngine.DayAt(300, 241, 870, 1).ShouldBe(0);
        HorizonEngine.DayAt(300, 241, 870, 0).ShouldBe(-1);
    }

    [Fact]
    public void Key_Day_Steps_By_One_Or_A_Week_And_Clamps()
    {
        HorizonEngine.KeyDay(Key.Left, false, 10, 119).ShouldBe(9);
        HorizonEngine.KeyDay(Key.Right, true, 10, 119).ShouldBe(17);
        HorizonEngine.KeyDay(Key.Left, true, 3, 119).ShouldBe(0);
        HorizonEngine.KeyDay(Key.Right, false, 118, 119).ShouldBe(118);
        HorizonEngine.KeyDay(Key.Home, false, 50, 119).ShouldBe(0);
        HorizonEngine.KeyDay(Key.End, false, 50, 119).ShouldBe(118);
        // With no crosshair an arrow starts from the latest day.
        HorizonEngine.KeyDay(Key.Left, false, -1, 119).ShouldBe(117);
        HorizonEngine.KeyDay(Key.Up, false, 5, 119).ShouldBeNull();
        HorizonEngine.KeyDay(Key.PageDown, false, 5, 119).ShouldBeNull();
    }

    [Fact]
    public void Order_Sorts_By_Name_Peak_And_Latest_With_Stable_Ties()
    {
        string[] names = ["Cedar", "Alder", "Birch", "Dune"];
        double[][] values =
        [
            [1, -10, 2],
            [0, 3, 3],
            [4, 12, -3],
            [-12, 0, 3]
        ];
        HorizonEngine.Order(names, values, HorizonSort.Name).ShouldBe([1, 2, 0, 3]);
        // Peaks 10, 3, 12, 12: the tie keeps the data order (Birch before Dune).
        HorizonEngine.Order(names, values, HorizonSort.Peak).ShouldBe([2, 3, 0, 1]);
        // Latest 2, 3, −3, 3: Alder and Dune tie on 3.
        HorizonEngine.Order(names, values, HorizonSort.Latest).ShouldBe([1, 3, 0, 2]);
    }

    [Fact]
    public void Extremes_Find_The_Highest_And_Lowest_Row_Of_A_Day()
    {
        double[][] values = [[1, 5], [2, -4], [3, double.NaN]];
        HorizonEngine.Extremes(values, 0).ShouldBe((2, 0));
        HorizonEngine.Extremes(values, 1).ShouldBe((0, 1));
    }

    [Fact]
    public void Legend_Cells_Share_The_Line_And_Labels_Use_Their_Text_Widths()
    {
        var widths = new double[8];
        HorizonEngine.LegendWidths(300, [0, 1, 1, 1, 1, 1, 1, 0], widths);
        widths[0].ShouldBe(0);
        widths[1].ShouldBe((300 - 7) / 6.0, 1e-9);
        widths[1].ShouldBe(48.83, 0.01);
        widths.Sum().ShouldBe(293, 1e-9);
        // Four bands: eight cells of (300 − 7) / 8.
        HorizonEngine.LegendWidths(300, [1, 1, 1, 1, 1, 1, 1, 1], widths);
        widths[0].ShouldBe(36.625, 1e-9);
        var columns = HorizonEngine.LegendColumns(300, 34.6, 7.7, 34.6);
        columns.Below.ShouldBe(111.55, 0.01);
        columns.Above.ShouldBe(columns.Below);
        // The grow and opacity targets per layer.
        HorizonEngine.LegendGrow(3, 3).ShouldBe(1);
        HorizonEngine.LegendGrow(4, 3).ShouldBe(0);
        HorizonEngine.LegendOpacity(1, 3).ShouldBe(0.569, 1e-3);
        HorizonEngine.LegendOpacity(4, 4).ShouldBe(0.92, 1e-9);
        HorizonEngine.LegendOpacity(2, 1).ShouldBe(0);
    }

    [Fact]
    public void Month_Ticks_Mark_The_First_Day_And_Every_First_Of_A_Month()
    {
        var days = Enumerable.Range(0, 70).Select(i => new HorizonDay(new DateOnly(2027, 2, 2).AddDays(i))).ToArray();
        var ticks = HorizonEngine.MonthTicks(days);
        ticks.Select(t => t.Label).ShouldBe(["Feb", "Mar", "Apr"]);
        ticks.Select(t => t.Index).ShouldBe([0, 27, 58]);
    }

    #endregion

    #region Block

    private static HorizonChart Chart(int days = 30)
    {
        var chart = new HorizonChart { Title = "Orders", Description = "Test data", BaselineLabel = "forecast" };
        var list = new List<HorizonDay>();
        for (var i = 0; i < days; i++)
        {
            list.Add(new HorizonDay(new DateOnly(2027, 2, 2).AddDays(i)));
        }
        chart.Days = list;
        chart.Series.Add(new HorizonSeries("Cedar", Wave(days, 1, 6)));
        chart.Series.Add(new HorizonSeries("Alder", Wave(days, 0, 30)));
        chart.Series.Add(new HorizonSeries("Birch", Wave(days, -1, 14)));
        return chart;
    }

    private static double[] Wave(int days, int sign, double amplitude)
    {
        var values = new double[days];
        for (var i = 0; i < days; i++)
        {
            values[i] = sign == 0 ? (i < days / 2 ? -amplitude : amplitude / 3) : sign * amplitude * ((i % 5) / 4.0);
        }
        return values;
    }

    private static Window Show(Control control, ArcMotionLevel level = ArcMotionLevel.Off, double width = 1152)
    {
        var window = TestHelpers.Show(control, level);
        window.Width = width;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void Defaults_Are_Peak_Three_Bands_And_The_Latest_Day()
    {
        var chart = Chart();
        var window = Show(chart);
        try
        {
            chart.Sort.ShouldBe(HorizonSort.Peak);
            chart.Bands.ShouldBe(3);
            chart.SelectedDayIndex.ShouldBeNull();
            chart.CurrentExtent.ShouldBe(36);
            var plot = chart.PlotPartControl!;
            // Peaks: Alder 30, Birch 14, Cedar 6.
            plot.RowOrder.ShouldBe([1, 2, 0]);
            plot.FoldedBands.ShouldBe(3);
            plot.Day.ShouldBe(-1);
            plot.CrosshairOpacity.ShouldBe(0);
            plot.ValueText(1).ShouldBe(AnalyticsFormat.Percent(10, 0, true));
            TestHelpers.Descendant<TextBlock>(chart, "PART_Date").Text.ShouldBe("Wednesday, March 3");
            TestHelpers.Descendant<TextBlock>(chart, "PART_Status").Text.ShouldBe("Most recent day");
            chart.Classes.Contains(":compact").ShouldBeFalse();
            chart.Classes.Contains(":medium").ShouldBeFalse();
            chart.Bounds.Width.ShouldBe(1152, 1);
            plot.Bounds.Height.ShouldBe(8 + 3 * 36 + 26 + 16);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switches_Offer_The_Measured_Options_And_Follow_The_Properties()
    {
        var chart = Chart();
        var window = Show(chart);
        try
        {
            var sort = TestHelpers.Descendant<SegmentedControl>(chart, "PART_SortSwitch");
            var bands = TestHelpers.Descendant<SegmentedControl>(chart, "PART_BandsSwitch");
            sort.SelectedIndex.ShouldBe(1);
            bands.SelectedIndex.ShouldBe(2);
            chart.Sort = HorizonSort.Latest;
            sort.SelectedIndex.ShouldBe(2);
            chart.Bands = 7;
            chart.Bands.ShouldBe(4);
            bands.SelectedIndex.ShouldBe(3);
            // The switches drive the properties too.
            bands.SelectedIndex = 0;
            chart.Bands.ShouldBe(1);
            sort.SelectedIndex = 0;
            chart.Sort.ShouldBe(HorizonSort.Name);
            chart.PlotPartControl!.RowOrder.ShouldBe([1, 2, 0]);
            chart.IsSortSwitchVisible = false;
            TestHelpers.Descendant<Control>(chart, "PART_SortGroup").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Day_Selection_Writes_The_Readout_And_Every_Row_And_Raises_The_Event()
    {
        var chart = Chart();
        var days = new List<int?>();
        chart.DayChanged += (_, e) => days.Add(e.Index);
        var window = Show(chart);
        try
        {
            chart.SelectDay(7);
            var plot = chart.PlotPartControl!;
            plot.Day.ShouldBe(7);
            plot.CrosshairOpacity.ShouldBe(1);
            plot.CrosshairPosition.ShouldBe(7);
            TestHelpers.Descendant<TextBlock>(chart, "PART_Date").Text.ShouldBe("Tuesday, February 9");
            TestHelpers.Descendant<TextBlock>(chart, "PART_Status").Text.ShouldBe("Picked day");
            plot.ValueText(1).ShouldBe(AnalyticsFormat.Percent(-30, 0, true));
            chart.Classes.Contains(":scrubbing").ShouldBeTrue();
            chart.ClearSelection();
            plot.CrosshairOpacity.ShouldBe(0);
            // Hiding keeps the line where it was; the readout is back on the latest day.
            plot.CrosshairPosition.ShouldBe(7);
            TestHelpers.Descendant<TextBlock>(chart, "PART_Status").Text.ShouldBe("Most recent day");
            days.ShouldBe([7, null]);
            chart.SelectedDayIndex = 99;
            chart.SelectedDayIndex.ShouldBe(29);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Moves_The_Crosshair_By_Day_And_Week_Clears_On_Escape_And_Announces()
    {
        var chart = Chart(119);
        var window = Show(chart);
        try
        {
            var plot = chart.PlotPartControl!;
            plot.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            // Keyboard focus puts the crosshair on the latest day.
            chart.SelectedDayIndex.ShouldBe(118);
            TestHelpers.Descendant<TextBlock>(chart, "PART_Status").Text.ShouldBe("Picked day");
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.SelectedDayIndex.ShouldBe(117);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            chart.SelectedDayIndex.ShouldBe(110);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            chart.SelectedDayIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.SelectedDayIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            chart.SelectedDayIndex.ShouldBe(118);
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            chart.SelectedDayIndex.ShouldBe(118);
            var history = chart.AnnouncerPartControl!.History;
            history.Count.ShouldBeGreaterThanOrEqualTo(5);
            history[^1].ShouldContain("Top ");
            history[^1].ShouldContain("bottom ");
            history[^1].ShouldContain("Alder");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.SelectedDayIndex.ShouldBeNull();
            TestHelpers.Descendant<TextBlock>(chart, "PART_Status").Text.ShouldBe("Most recent day");
            plot.IsFocused.ShouldBeTrue();
            // From the latest day again: Left goes to the one before it.
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.SelectedDayIndex.ShouldBe(117);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Over_The_Band_Column_Sets_The_Day_And_Leaving_Clears_It()
    {
        var chart = Chart(119);
        var window = Show(chart);
        try
        {
            var plot = chart.PlotPartControl!;
            var layout = plot.CurrentLayout;
            var origin = plot.TranslatePoint(new Point(0, 0), window)!.Value;
            var inside = new Point(origin.X + layout.PlotLeft + layout.PlotWidth * 0.5, origin.Y + 60);
            window.MouseMove(inside);
            chart.SelectedDayIndex.ShouldBe(59);
            // The name column does not respond: leaving the band column hides the crosshair.
            window.MouseMove(new Point(origin.X + 40, origin.Y + 60));
            chart.SelectedDayIndex.ShouldBeNull();
            window.MouseMove(inside);
            window.MouseMove(new Point(origin.X + layout.PlotLeft + layout.PlotWidth + 200, origin.Y + 5000));
            chart.SelectedDayIndex.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Off_Motion_Jumps_The_Fold_And_The_Rows()
    {
        var chart = Chart();
        var bandEvents = new List<int>();
        var sortEvents = new List<HorizonSort>();
        chart.BandsChanged += (_, e) => bandEvents.Add(e.Bands);
        chart.SortChanged += (_, e) => sortEvents.Add(e.Sort);
        var window = Show(chart);
        try
        {
            var plot = chart.PlotPartControl!;
            chart.Bands = 1;
            plot.FoldedBands.ShouldBe(1);
            chart.Sort = HorizonSort.Name;
            plot.RowOrder.ShouldBe([1, 2, 0]);
            plot.RowOffset(0).ShouldBe(0);
            chart.Sort = HorizonSort.Latest;
            bandEvents.ShouldBe([1]);
            sortEvents.ShouldBe([HorizonSort.Name, HorizonSort.Latest]);
            chart.AnnouncerPartControl!.History.ShouldContain("One band of 36%");
            chart.AnnouncerPartControl.History.ShouldContain("Sorted by latest value");
            chart.Bands = 4;
            chart.AnnouncerPartControl.History.ShouldContain("4 bands of 9%");
            chart.LegendPartControl!.CellGrow(0).ShouldBe(1);
            chart.Bands = 2;
            chart.LegendPartControl.CellGrow(0).ShouldBe(0);
            chart.LegendPartControl.CellGrow(3).ShouldBe(1);
            chart.LegendPartControl.CellOpacity(3).ShouldBe(HorizonEngine.StackedAlpha(1, 2), 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Fold_Runs_On_The_Fold_Spring_And_Retargets_From_Mid_Flight()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var chart = Chart();
        var window = Show(chart, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var plot = chart.PlotPartControl!;
            var legend = chart.LegendPartControl!;
            chart.Bands = 1;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(200));
            // 3 → 1 critically damped: 1 + 2 (1 + ωt) e^{−ωt} at 0.2s with ω 8.73.
            plot.FoldedBands.ShouldBeInRange(1 + 2 * (1 + 8.73 * 0.26) * Math.Exp(-8.73 * 0.26), 1 + 2 * (1 + 8.73 * 0.17) * Math.Exp(-8.73 * 0.17));
            plot.FoldedBands.ShouldBeGreaterThan(1);
            // The legend runs 480ms in-out: half way at 240ms.
            legend.CellGrow(1).ShouldBeGreaterThan(0);
            legend.CellGrow(1).ShouldBeLessThan(1);
            var mid = plot.FoldedBands;
            chart.Bands = 4;
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(16));
            // The velocity carries: no jump back to a start value.
            plot.FoldedBands.ShouldBeInRange(mid - 0.2, mid + 0.4);
            clock.Advance(TimeSpan.FromMilliseconds(2200));
            plot.FoldedBands.ShouldBe(4, 0.01);
            legend.CellGrow(0).ShouldBe(1, 1e-6);
            plot.IsMoving.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sort_Moves_Every_Row_At_Once_On_The_Reorder_Spring_And_Reduced_Jumps()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var chart = Chart();
        var window = Show(chart, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var plot = chart.PlotPartControl!;
            // Peak order: Alder, Birch, Cedar. By name: Alder, Birch, Cedar too; by latest: differs.
            chart.Sort = HorizonSort.Latest;
            Dispatcher.UIThread.RunJobs();
            var moved = Enumerable.Range(0, 3).Where(r => Math.Abs(plot.RowOffset(r)) > 0.01).ToList();
            moved.ShouldNotBeEmpty();
            clock.Advance(TimeSpan.FromMilliseconds(100));
            foreach (var row in moved)
            {
                Math.Abs(plot.RowOffset(row)).ShouldBeLessThan(36 * 2 + 0.01);
            }
            clock.Advance(TimeSpan.FromMilliseconds(1200));
            plot.IsMoving.ShouldBeFalse();
            Enumerable.Range(0, 3).All(r => plot.RowOffset(r) == 0).ShouldBeTrue();

            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            chart.Sort = HorizonSort.Name;
            Dispatcher.UIThread.RunJobs();
            Enumerable.Range(0, 3).All(r => plot.RowOffset(r) == 0).ShouldBeTrue();
            chart.Bands = 1;
            Dispatcher.UIThread.RunJobs();
            plot.FoldedBands.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Crosshair_Appears_In_Place_Then_Follows_On_A_Spring_And_Fades_On_Leave()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var chart = Chart(119);
        var window = Show(chart, ArcMotionLevel.Full);
        try
        {
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var plot = chart.PlotPartControl!;
            chart.SelectDay(100);
            Dispatcher.UIThread.RunJobs();
            // Appears at its target with no slide; the fade runs 160ms.
            plot.CrosshairPosition.ShouldBe(100);
            plot.CrosshairOpacity.ShouldBeLessThan(1);
            clock.Advance(TimeSpan.FromMilliseconds(200));
            plot.CrosshairOpacity.ShouldBe(1);
            chart.SelectDay(20);
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(50));
            plot.CrosshairPosition.ShouldBeInRange(21, 99);
            chart.Sort.ShouldBe(HorizonSort.Peak);
            clock.Advance(TimeSpan.FromMilliseconds(600));
            plot.CrosshairPosition.ShouldBe(20, 0.01);
            chart.ClearSelection();
            Dispatcher.UIThread.RunJobs();
            clock.Advance(TimeSpan.FromMilliseconds(250));
            plot.CrosshairOpacity.ShouldBe(0);
            plot.CrosshairPosition.ShouldBe(20, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Breakpoints_Follow_The_Blocks_Own_Width()
    {
        var chart = Chart();
        var host = new Border { Width = 500, Child = chart };
        var window = Show(host);
        try
        {
            chart.Classes.Contains(":compact").ShouldBeTrue();
            var plot = chart.PlotPartControl!;
            plot.CurrentLayout.Tier.ShouldBe(HorizonTier.Compact);
            plot.Bounds.Height.ShouldBe(8 + 3 * 34 + 26 + 16);
            TestHelpers.Descendant<HorizonLegend>(chart, "PART_Legend").IsFilled.ShouldBeTrue();
            host.Width = 800;
            Dispatcher.UIThread.RunJobs();
            chart.Classes.Contains(":medium").ShouldBeTrue();
            chart.Classes.Contains(":compact").ShouldBeFalse();
            host.Width = 1000;
            Dispatcher.UIThread.RunJobs();
            chart.Classes.Contains(":medium").ShouldBeFalse();
            TestHelpers.Descendant<HorizonLegend>(chart, "PART_Legend").IsFilled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Texts_Caption_And_Formatter_Follow_The_Band_Count()
    {
        var chart = Chart();
        var window = Show(chart);
        try
        {
            var legend = chart.LegendPartControl!;
            legend.Caption.ShouldBe("Rows fold into 3 bands of 12%. Darker color means a larger gap from the forecast.");
            chart.Bands = 1;
            legend.Caption.ShouldBe("With a single band each row is an ordinary area chart up to 36% from the forecast.");
            chart.ValueFormatter = v => v.ToString("0") + " ms";
            chart.Bands = 2;
            legend.Caption.ShouldContain("18 ms");
            chart.PlotPartControl!.ValueText(1).ShouldBe("10 ms");
            // The values the chart is measured against are properties of the labels.
            chart.BaselineLabel = null;
            chart.Labels = new HorizonChartLabels { BaselineText = "plan" };
            legend.Caption.ShouldContain("from the plan");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Reader_Table_Lists_Rows_In_Sort_Order_With_Latest_Highest_And_Lowest()
    {
        var chart = Chart(10);
        var window = Show(chart);
        try
        {
            var table = chart.TablePartControl!;
            table.RowHeaders.ShouldBe(["Alder", "Birch", "Cedar"]);
            table.Caption.ShouldBe("Orders, values against the forecast");
            table.Rows[0].Count.ShouldBe(3);
            table.Rows[0][0].ShouldStartWith("Latest: ");
            table.Rows[0][0].ShouldEndWith("on Feb 11");
            table.Rows[0][1].ShouldStartWith("Highest: +10% on ");
            table.Rows[0][2].ShouldStartWith("Lowest: −30% on ");
            chart.Sort = HorizonSort.Name;
            table.RowHeaders.ShouldBe(["Alder", "Birch", "Cedar"]);
            chart.Sort = HorizonSort.Latest;
            table.RowHeaders[0].ShouldBe("Alder");
            Avalonia.Automation.AutomationProperties.GetName(chart.PlotPartControl!).ShouldBe("Orders. 3 rows");
            Avalonia.Automation.AutomationProperties.GetHelpText(chart.PlotPartControl!)!.ShouldContain("Escape");
            Avalonia.Automation.AutomationProperties.GetName(chart.LegendPartControl!)!.ShouldContain("from −36% under the forecast to +36% over it, 3 bands per side");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Series_Source_Is_Followed_And_An_Empty_Chart_Takes_Space_For_Its_Message()
    {
        var chart = new HorizonChart { Title = "Empty" };
        var window = Show(chart);
        try
        {
            chart.HasRows.ShouldBeFalse();
            chart.Classes.Contains(":empty").ShouldBeTrue();
            var source = new Avalonia.Collections.AvaloniaList<HorizonSeries>();
            chart.SeriesSource = source;
            source.Add(new HorizonSeries("One", [1, 2, 3, -4]));
            chart.HasRows.ShouldBeTrue();
            chart.DayCount.ShouldBe(4);
            chart.Classes.Contains(":empty").ShouldBeFalse();
            chart.PlotPartControl!.RowOrder.ShouldBe([0]);
            chart.CurrentExtent.ShouldBe(12);
            source.RemoveAt(0);
            chart.HasRows.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
