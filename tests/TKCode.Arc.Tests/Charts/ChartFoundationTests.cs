using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Charts;

public class ChartFoundationTests
{
    [Fact]
    public void Nice_Ticks_Use_1_2_2_5_5_Steps_From_Zero()
    {
        var ticks = ChartTicks.Nice(12, 523);
        ticks.Min.ShouldBe(0);
        ticks.Max.ShouldBe(600);
        ticks.Ticks.ShouldBe([0, 200, 400, 600]);

        var hundred = ChartTicks.Nice(0, 100);
        hundred.Ticks.ShouldBe([0, 25, 50, 75, 100]);

        // Data that dips below zero extends the scale downwards.
        var negative = ChartTicks.Nice(-30, 70);
        negative.Min.ShouldBeLessThanOrEqualTo(-30);
        negative.Ticks.Length.ShouldBeLessThanOrEqualTo(5);
    }

    [Fact]
    public void Bar_Ticks_Use_1_2_3_5_Steps()
    {
        var ticks = ChartTicks.FromZero(68);
        ticks.Max.ShouldBe(80);
        ticks.Ticks.ShouldBe([0, 20, 40, 60, 80]);
        ChartTicks.FromZero(0).Max.ShouldBe(1.2, 1e-9);
    }

    [Fact]
    public void Clean_Removes_Floating_Noise()
    {
        ChartTicks.Clean(0.1 + 0.2).ShouldBe(0.3);
        ChartTicks.Clean(2.5 * 3).ShouldBe(7.5);
    }

    [Fact]
    public void Monotone_Curve_Never_Swings_Past_The_Data()
    {
        double[] values = [0, 10, 10, 0, 5, 5, 30];
        var (xs, ys) = ChartCurves.SampleLine(values, true);
        xs[0].ShouldBe(0);
        xs[^1].ShouldBe(1);
        ys.Min().ShouldBeGreaterThanOrEqualTo(0);
        ys.Max().ShouldBeLessThanOrEqualTo(30);
        // A flat stretch stays flat.
        ChartCurves.ValueAt(xs, ys, 1.5 / 6).ShouldBe(10, 1e-9);
        // At most 16 samples per segment.
        xs.Length.ShouldBe(6 * 16 + 1);
    }

    [Fact]
    public void Resample_Uses_A_Fixed_Grid_And_Clamps_Below_Zero()
    {
        var grid = ChartCurves.Resample([4, -2, 8, 3], 161);
        grid.Length.ShouldBe(161);
        grid[0].ShouldBe(4);
        grid[^1].ShouldBe(3);
        grid.Min().ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Union_Merges_Sorted_Grids_Without_Duplicates()
    {
        var union = ChartCurves.Union([0, 0.5, 1], [0, 0.25, 0.5, 0.75, 1]);
        union.ShouldBe([0, 0.25, 0.5, 0.75, 1]);
    }

    [AvaloniaFact]
    public void Category_Axis_Thins_From_The_Latest_Label()
    {
        var chart = new LineChart { ItemsSource = Enumerable.Range(0, 30).Select(i => new ChartPoint($"D{i}", i)).ToArray() };
        var picks = new List<int>();
        ChartCategoryAxis.Picks(chart.Data, 300, 76, picks);
        picks.ShouldContain(29);
        picks.Count.ShouldBeLessThanOrEqualTo(4);
    }

    [Fact]
    public void Time_Ticks_Pick_Days_Weeks_Months_Or_Years_By_Width()
    {
        var start = ChartTime.ToMs(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var ticks = new List<(double, string)>();
        ChartTime.Ticks(start, start + 6 * ChartTime.Day, 800, ticks);
        ticks.Count.ShouldBe(7);
        ticks[0].Item2.ShouldBe("Jan 1");
        ChartTime.Ticks(start, start + 400 * ChartTime.Day, 600, ticks);
        ticks.ShouldContain(t => t.Item2 == "2027");
    }

    [AvaloniaFact]
    public void Data_Builder_Reads_Records_Selectors_And_Series()
    {
        var points = new LineChart
        {
            Label = "Visits",
            ItemsSource = new[] { new ChartPoint(new DateTime(2026, 9, 15), 10) { Key = "a" }, new ChartPoint(new DateTime(2026, 9, 16), 12) }
        };
        var data = points.Data;
        data.Count.ShouldBe(2);
        data.Keys[0].ShouldBe("a");
        data.Labels[0].ShouldBe("Tue, Sep 15");
        data.AxisLabels[1].ShouldBe("Sep 16");
        data.Series.Single().Values.ShouldBe([10, 12]);

        var records = new[] { (Day: "Mon", Revenue: 3.0, Cost: 1.0), (Day: "Tue", Revenue: 5.0, Cost: 2.0) };
        var selectors = new LineChart { ItemsSource = records, XSelector = o => (((string, double, double))o).Item1 };
        selectors.Series.Add(new ChartSeries { Name = "Revenue", ValueSelector = o => (((string, double, double))o).Item2 });
        selectors.Series.Add(new ChartSeries { Name = "Cost", ValueSelector = o => (((string, double, double))o).Item3 });
        selectors.Data.Series.Length.ShouldBe(2);
        selectors.Data.Series[1].Values.ShouldBe([1, 2]);
        selectors.Data.Labels.ShouldBe(["Mon", "Tue"]);

        // Plain series values, padded with zeros when shorter.
        var series = new LineChart();
        series.Series.Add(new ChartSeries { Key = "x", Name = "X", Values = [1, 2, 3] });
        series.Series.Add(new ChartSeries { Key = "y", Name = "Y", Values = [4] });
        series.Data.Count.ShouldBe(3);
        series.Data.Series[1].Values.ShouldBe([4, 0, 0]);
    }

    [AvaloniaFact]
    public void Data_Builder_Evaluates_Bindings_Against_Items()
    {
        var chart = new BarChart
        {
            ItemsSource = new[] { new Sample("A", 4), new Sample("B", 9) },
            XBinding = new Avalonia.Data.Binding(nameof(Sample.Name)),
            YBinding = new Avalonia.Data.Binding(nameof(Sample.Amount))
        };
        chart.Data.Labels.ShouldBe(["A", "B"]);
        chart.Data.Series[0].Values.ShouldBe([4, 9]);
    }

    public sealed record Sample(string Name, double Amount);

    [AvaloniaFact]
    public void Spring_Array_Keeps_Velocity_When_Retargeted()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var host = new Border { Width = 10, Height = 10 };
        var window = TestHelpers.Show(host, ArcMotionLevel.Full);
        try
        {
            var springs = new ChartSpringArray(host);
            springs.Jump([0.0, 0.0]);
            springs.AnimateTo([1.0, 2.0], ArcMotionPresets.Smooth);
            clock.Advance(TimeSpan.FromMilliseconds(100));
            var velocity = springs.Velocities.ToArray();
            var position = springs.Values.ToArray();
            velocity[0].ShouldBeGreaterThan(0);
            springs.AnimateTo([-1.0, -2.0], ArcMotionPresets.Smooth);
            // Retargeting starts from the same place, still moving the same way.
            springs.Values.ToArray().ShouldBe(position);
            clock.Advance(TimeSpan.FromMilliseconds(17));
            springs.Values[0].ShouldBeGreaterThan(position[0] - 0.05);
            clock.Advance(TimeSpan.FromSeconds(3));
            springs.IsAnimating.ShouldBeFalse();
            springs.Values.ToArray().ShouldBe([-1.0, -2.0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Line_Morph_Starts_From_The_Shape_On_Screen()
    {
        var clock = new ManualFrameClock();
        using var install = clock.Install();
        var host = new Border { Width = 10, Height = 10 };
        var window = TestHelpers.Show(host, ArcMotionLevel.Full);
        try
        {
            var morph = new ChartLineMorph(host);
            var (xs, ys) = ChartCurves.SampleLine([0, 0, 0], false);
            morph.SetTargets(["a"], [xs], [ys], 241, true, ArcMotionPresets.Smooth);
            morph.ValueAt(0, 0.5).ShouldBe(0);
            var (xs2, ys2) = ChartCurves.SampleLine([10, 10, 10, 10], false);
            morph.SetTargets(["a"], [xs2], [ys2], 241, true, ArcMotionPresets.Smooth);
            morph.IsAnimating.ShouldBeTrue();
            morph.Grid.Length.ShouldBeGreaterThanOrEqualTo(241);
            clock.Advance(TimeSpan.FromMilliseconds(120));
            var mid = morph.ValueAt(0, 0.5);
            mid.ShouldBeGreaterThan(0);
            mid.ShouldBeLessThan(10);
            clock.Advance(TimeSpan.FromSeconds(3));
            morph.ValueAt(0, 0.5).ShouldBe(10, 1e-6);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Series_Palette_Uses_Series_Tokens_Then_Neutral_Steps()
    {
        var chart = new DonutChart { ItemsSource = new[] { new ChartPoint("A", 1) } };
        var window = TestHelpers.Show(chart);
        try
        {
            chart.EnsurePalette();
            var palette = chart.Palette;
            palette.SeriesColor(0).ShouldBe(TestHelpers.ColorOf(chart.Series1Brush));
            palette.SeriesColor(3).ShouldBe(TestHelpers.ColorOf(chart.Series4Brush));
            palette.SeriesColor(4).ShouldNotBe(palette.SeriesColor(5));
            palette.SeriesColor(4).ShouldBe(palette.SeriesColor(7));
        }
        finally
        {
            window.Close();
        }
    }
}
