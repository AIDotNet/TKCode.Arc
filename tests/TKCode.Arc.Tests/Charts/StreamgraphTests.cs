using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Charts;

public class StreamgraphTests
{
    private static Streamgraph Create()
    {
        var chart = new Streamgraph
        {
            Label = "Tickets",
            Unit = "tickets",
            CategoryLabel = "Week",
            ItemsSource = new[] { "W1", "W2", "W3", "W4", "W5", "W6" }
        };
        chart.Series.Add(new ChartSeries { Key = "billing", Name = "Billing", Values = [10, 12, 9, 14, 20, 18] });
        chart.Series.Add(new ChartSeries { Key = "login", Name = "Login", Values = [5, 7, 8, 6, 4, 9] });
        chart.Series.Add(new ChartSeries { Key = "mobile", Name = "Mobile", Values = [3, 3, 5, 8, 6, 7] });
        return chart;
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var chart = new Streamgraph();
        chart.PlotHeight.ShouldBe(260);
        chart.Offset.ShouldBe(StreamOffset.Wiggle);
        chart.ShowDirectLabels.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Keyboard_Moves_Through_Time_And_Changes_The_Isolated_Layer()
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
            chart.ActiveIndex.ShouldBe(5);
            chart.ActiveSeriesKey.ShouldBeNull();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            chart.ActiveSeriesKey.ShouldBe("mobile");
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            chart.ActiveSeriesKey.ShouldBe("login");
            events[^1].SeriesKey.ShouldBe("login");
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(4);
            chart.Announcement.ShouldBe("W5. Login 4 tickets, 3 layers, 30 tickets in total");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBeNull();
            chart.ActiveSeriesKey.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Finds_The_Layer_Under_It()
    {
        var chart = Create();
        chart.Offset = StreamOffset.Zero;
        var window = TestHelpers.Show(chart);
        try
        {
            var plot = TestHelpers.Descendant<ChartPlot>(chart);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            // With a zero baseline the first layer sits at the bottom of the plot.
            window.MouseMove(plot.TranslatePoint(new Point(plot.Bounds.Width - 2, 250), window)!.Value, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(5);
            chart.ActiveSeriesKey.ShouldBe("billing");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Legend_Hides_And_Previews_Layers()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            var items = chart.GetVisualDescendants().OfType<ChartLegendItem>().ToList();
            items.Count.ShouldBe(3);
            items[0].Focus(NavigationMethod.Tab);
            chart.ActiveSeriesKey.ShouldBe("billing");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            chart.HiddenSeries.ShouldBe(["billing"]);
            chart.ActiveSeriesKey.ShouldBeNull();
            chart.Summary.ShouldNotContain("Billing");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Every_Offset_Builds_A_Finite_Stack()
    {
        foreach (var offset in new[] { StreamOffset.Wiggle, StreamOffset.Silhouette, StreamOffset.Zero })
        {
            var chart = Create();
            chart.Offset = offset;
            var window = TestHelpers.Show(chart);
            try
            {
                chart.ActiveIndex = 2;
                Dispatcher.UIThread.RunJobs();
                chart.GetPlotValueText(TestHelpers.Descendant<ChartPlot>(chart)).ShouldStartWith("W3.");
            }
            finally
            {
                window.Close();
            }
        }
    }
}
