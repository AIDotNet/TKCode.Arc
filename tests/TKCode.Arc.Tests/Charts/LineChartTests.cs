using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Charts;

public class LineChartTests
{
    private static LineChart Create()
    {
        var chart = new LineChart
        {
            Label = "Signups",
            Unit = "users",
            ItemsSource = new[] { new DateTime(2026, 9, 1), new DateTime(2026, 9, 2), new DateTime(2026, 9, 3), new DateTime(2026, 9, 4) }
        };
        chart.Series.Add(new ChartSeries { Key = "now", Name = "This period", Values = [120, 180, 150, 240] });
        chart.Series.Add(new ChartSeries { Key = "before", Name = "Previous", Values = [100, 90, 140, 160], IsDashed = true });
        return chart;
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var chart = new LineChart();
        chart.PlotHeight.ShouldBe(220);
        chart.Curve.ShouldBe(ChartCurve.Smooth);
        chart.EmptyLabel.ShouldBe("No data for this range");
        chart.CategoryLabel.ShouldBe("Date");
        chart.ShowGrid.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Renders_Plot_Legend_And_Summary()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            var plot = TestHelpers.Descendant<ChartPlot>(chart);
            plot.Bounds.Height.ShouldBe(220 + 26);
            var legend = TestHelpers.Descendant<ChartLegend>(chart);
            legend.IsVisible.ShouldBeTrue();
            legend.Children.Count.ShouldBe(2);
            chart.Summary.ShouldBe("Signups, Tue, Sep 1 to Fri, Sep 4. This period, latest 240 users. Previous, latest 160 users.");
            chart.DataTableText.Split('\n')[0].ShouldBe("Date\tThis period\tPrevious");
            chart.DataTableText.Split('\n').Length.ShouldBe(5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Single_Series_Has_No_Legend_Unless_Asked()
    {
        var chart = new LineChart { Label = "Latency", ItemsSource = new[] { 1.0, 3.0, 2.0 } };
        var window = TestHelpers.Show(chart);
        try
        {
            TestHelpers.Descendant<ChartLegend>(chart).IsVisible.ShouldBeFalse();
            chart.ShowLegend = true;
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<ChartLegend>(chart).IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Moves_The_Crosshair_And_Raises_Events()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            var events = new List<ChartActiveIndexChangedEventArgs>();
            chart.ActiveIndexChanged += (_, e) => events.Add(e);
            var plot = TestHelpers.Descendant<ChartPlot>(chart);
            plot.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            chart.ActiveIndex.ShouldBe(3);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(2);
            events[^1].Label.ShouldBe("Thu, Sep 3");
            events[^1].Values.Select(v => v.Value).ShouldBe([150, 140]);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(3);
            chart.GetPlotValueText(plot).ShouldBe("Fri, Sep 4: This period 240 users, Previous 160 users");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBeNull();
            events[^1].Index.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Hover_Reads_The_Nearest_Point_And_Leaving_Releases()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            var plot = TestHelpers.Descendant<ChartPlot>(chart);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var width = plot.Bounds.Width - 48;
            var point = plot.TranslatePoint(new Avalonia.Point(width / 3 + 2, 100), window)!.Value;
            window.MouseMove(point, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(1);
            var outside = plot.TranslatePoint(new Avalonia.Point(width + 20, 100), window)!.Value;
            window.MouseMove(outside, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Legend_Toggles_Hide_Series()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            IReadOnlyList<string>? reported = null;
            chart.HiddenSeriesChanged += (_, e) => reported = e.Hidden;
            var items = TestHelpers.Descendant<ChartLegend>(chart).Children.OfType<ChartLegendItem>().ToList();
            items[1].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            chart.HiddenSeries.ShouldBe(["before"]);
            reported.ShouldBe(["before"]);
            items[1].IsSeriesHidden.ShouldBeTrue();
            chart.Summary.ShouldNotContain("Previous");
            // Hiding every series says so.
            chart.HiddenSeries = ["now", "before"];
            Dispatcher.UIThread.RunJobs();
            chart.VisibleSeriesCount().ShouldBe(0);
            chart.HiddenSeries = null;
            Dispatcher.UIThread.RunJobs();
            items[1].IsSeriesHidden.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void New_Range_Releases_The_Crosshair_And_Rescales()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            chart.ActiveIndex = 2;
            chart.Series[0].Values = [1200, 1800, 1500, 2400];
            Dispatcher.UIThread.RunJobs();
            chart.ActiveIndex.ShouldBeNull();
            chart.Summary.ShouldContain("latest 2,400 users");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_And_Loading_States()
    {
        var chart = new LineChart { Label = "Refunds" };
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Summary.ShouldBe("Refunds. No data for this range.");
            chart.IsLoading = true;
            chart.Announcement.ShouldBe("Loading");
            chart.IsLoading = false;
            chart.Announcement.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Chart_Is_Collected_While_Its_Series_Values_Live_On()
    {
        var values = new ObservableCollection<double> { 12, 18, 15 };
        var weak = TestHelpers.ShowAndClose(() => new LineChart { Label = "Signups", Series = { new ChartSeries { Key = "now", Name = "Now", Values = values } } });
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        values.Add(24);
    }

    [AvaloniaFact]
    public void A_Chart_Given_Items_While_Away_Is_Collected_While_They_Live_On()
    {
        var items = new ObservableCollection<ChartPoint> { new(new DateTime(2026, 9, 15), 10), new(new DateTime(2026, 9, 16), 12) };
        var weak = ShowCloseThenSetItems(items);
        TestHelpers.Collect(weak);
        weak.IsAlive.ShouldBeFalse();
        items.Add(new ChartPoint(new DateTime(2026, 9, 17), 14));
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference ShowCloseThenSetItems(System.Collections.IEnumerable items)
    {
        LineChart? shown = null;
        var weak = TestHelpers.ShowAndClose(() => shown = new LineChart { Label = "Visits" });
        // Set after the window closed: a detached chart must not start listening.
        shown!.ItemsSource = items;
        return weak;
    }

    [AvaloniaFact]
    public void A_Chart_Put_Back_Shows_Values_Changed_While_It_Was_Away()
    {
        var values = new ObservableCollection<double> { 12, 18, 15 };
        var items = new ObservableCollection<ChartPoint> { new(new DateTime(2026, 9, 15), 10), new(new DateTime(2026, 9, 16), 12) };
        var chart = new LineChart { Label = "Signups", Series = { new ChartSeries { Key = "now", Name = "Now", Values = values } } };
        var visits = new LineChart { Label = "Visits", ItemsSource = items };
        var host = new StackPanel { Children = { chart, visits } };
        var window = TestHelpers.Show(host);
        try
        {
            chart.Summary.ShouldContain("Now, latest 15.");
            host.Children.Clear();
            Dispatcher.UIThread.RunJobs();
            values.Add(24);
            items.Add(new ChartPoint(new DateTime(2026, 9, 17), 14));
            host.Children.Add(chart);
            host.Children.Add(visits);
            Dispatcher.UIThread.RunJobs();
            chart.Summary.ShouldContain("Now, latest 24.");
            visits.Data.Count.ShouldBe(3);
            values.Add(31);
            items.Add(new ChartPoint(new DateTime(2026, 9, 18), 16));
            Dispatcher.UIThread.RunJobs();
            chart.Summary.ShouldContain("Now, latest 31.");
            visits.Data.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }
}
