using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Charts;

public class BarChartTests
{
    private static BarChart Create() => new()
    {
        Label = "Active minutes",
        Period = "Sep 15–21",
        Unit = "min",
        ItemsSource = new[]
        {
            new ChartPoint("Mon", 30) { Label = "Monday" }, new ChartPoint("Tue", 50) { Label = "Tuesday" },
            new ChartPoint("Wed", 20) { Label = "Wednesday" }, new ChartPoint("Thu", 60) { Label = "Thursday" }
        }
    };

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var chart = new BarChart();
        chart.PlotHeight.ShouldBe(176);
        chart.AverageLabel.ShouldBe("Daily average");
        chart.ValueLabel.ShouldBe("Total");
        chart.ShowAverage.ShouldBeTrue();
        chart.CategoryLabel.ShouldBe("Day");
    }

    [AvaloniaFact]
    public void Headline_Reads_The_Average_At_Rest_And_The_Bar_While_Scrubbing()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Average.ShouldBe(40);
            var digits = TestHelpers.Descendant<RollingDigits>(chart);
            digits.DisplayText.ShouldBe("40");
            var swaps = chart.GetVisualDescendants().OfType<ArcContentSwap>().ToList();
            var kind = swaps.First(s => s.Name == BarChart.KindPart);
            var when = swaps.First(s => s.Name == BarChart.WhenPart);
            kind.Content.ShouldBe("Daily average");
            when.Content.ShouldBe("Sep 15–21");
            chart.ActiveIndex = 1;
            Dispatcher.UIThread.RunJobs();
            digits.DisplayText.ShouldBe("50");
            kind.Content.ShouldBe("Total");
            when.Content.ShouldBe("Tuesday");
            chart.Summary.ShouldBe("Active minutes, Sep 15–21. Daily average 40 min. Highest Thursday, 60 min. Lowest Wednesday, 20 min.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Pages_At_Most_Seven_Bars()
    {
        var chart = new BarChart { Label = "Steps", ItemsSource = Enumerable.Range(0, 20).Select(i => new ChartPoint($"D{i}", i)).ToArray() };
        var window = TestHelpers.Show(chart);
        try
        {
            var plot = TestHelpers.Descendant<ChartPlot>(chart);
            plot.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            chart.ActiveIndex.ShouldBe(19);
            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(12);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(11);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Slots_Count_Back_From_The_Latest_Bar()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            var plot = TestHelpers.Descendant<ChartPlot>(chart);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var width = plot.Bounds.Width - 52;
            // The right-most quarter of the plot is the latest bar.
            window.MouseMove(plot.TranslatePoint(new Point(width - 5, 80), window)!.Value, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(3);
            window.MouseMove(plot.TranslatePoint(new Point(5, 80), window)!.Value, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void New_Range_Announces_The_Period()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Period = "Sep 22–28";
            chart.ItemsSource = new[] { new ChartPoint("Mon", 10), new ChartPoint("Tue", 30) };
            Dispatcher.UIThread.RunJobs();
            chart.Average.ShouldBe(20);
            chart.Announcement.ShouldBe("Sep 22–28. Daily average 20 min.");
            chart.DataTableText.ShouldBe("Day\tActive minutes\nMon\t10 min\nTue\t30 min");
        }
        finally
        {
            window.Close();
        }
    }
}
