using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Charts;

public class BrushChartTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static BrushChart Create(int days = 100) => new()
    {
        Label = "Daily active users",
        Unit = "users",
        ItemsSource = Enumerable.Range(0, days).Select(i => new ChartPoint(Start.AddDays(i), 100 + i)).ToArray(),
        Annotations = [new ChartAnnotation(Start.AddDays(10), "Launch", "Public launch")]
    };

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var chart = new BrushChart();
        chart.PlotHeight.ShouldBe(240);
        chart.OverviewHeight.ShouldBe(52);
        chart.MinSpan.ShouldBe(TimeSpan.FromDays(7));
        chart.EmptyLabel.ShouldBe("No data yet");
    }

    [AvaloniaFact]
    public void Window_Starts_On_The_Whole_Series_And_Steps_With_The_Keyboard()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            chart.WindowText.ShouldBe("Jan 1, 2026 to Apr 10, 2026");
            var ranges = new List<ChartTimeRange>();
            chart.RangeChanged += (_, e) => ranges.Add(e.Range);
            var thumbs = chart.GetVisualDescendants().OfType<BrushChartThumb>().ToList();
            thumbs.Count.ShouldBe(3);
            var end = thumbs.First(t => t.Kind == BrushChartThumbKind.End);
            end.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.Shift);
            chart.Range!.Value.End.ShouldBe(Start.AddDays(89));
            ranges.Count.ShouldBe(1);
            var win = thumbs.First(t => t.Kind == BrushChartThumbKind.Window);
            win.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            chart.Range!.Value.Start.ShouldBe(Start);
            // Zoom in around the center, then reset.
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.Range!.Value.ShouldBe(new ChartTimeRange(Start, Start.AddDays(99)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Handles_Respect_The_Minimum_Span()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            var start = chart.GetVisualDescendants().OfType<BrushChartThumb>().First(t => t.Kind == BrushChartThumbKind.Start);
            start.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            chart.Range!.Value.Span.ShouldBe(TimeSpan.FromDays(7));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Bound_Range_Moves_The_Window()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Range = new ChartTimeRange(Start.AddDays(20), Start.AddDays(40));
            Dispatcher.UIThread.RunJobs();
            chart.WindowText.ShouldBe("Jan 21, 2026 to Feb 10, 2026");
            chart.Summary.ShouldBe("Daily active users, Jan 1, 2026 to Apr 10, 2026. Showing Jan 21, 2026 to Feb 10, 2026.");
            chart.DataTableText.Split('\n').Length.ShouldBe(22);
            // A window narrower than the minimum span widens to it.
            chart.Range = new ChartTimeRange(Start.AddDays(50), Start.AddDays(51));
            Dispatcher.UIThread.RunJobs();
            chart.View.End.ShouldBe(chart.View.Start + 7 * ChartTime.Day);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Plot_Reads_Points_In_View_With_Events()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            var plot = TestHelpers.Descendant<ChartPlot>(chart);
            plot.Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            chart.ActiveIndex.ShouldBe(99);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(0);
            for (var i = 0; i < 10; i++)
            {
                window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            }
            chart.Announcement.ShouldBe("Sun, Jan 11, 2026, 110 users. Launch");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_The_Overview_Pans_Resizes_And_Draws_Windows()
    {
        var chart = Create();
        chart.PlotHeight = 120;
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Range = new ChartTimeRange(Start.AddDays(20), Start.AddDays(40));
            Dispatcher.UIThread.RunJobs();
            var overview = TestHelpers.Descendant<BrushChartOverview>(chart);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var width = overview.Bounds.Width;
            double X(int day) => day / 99.0 * width;
            Point At(double x) => overview.TranslatePoint(new Point(x, overview.Bounds.Height / 2), window)!.Value;

            // Pan: grab the middle of the window and drag ten days to the right.
            window.MouseDown(At(X(30)), MouseButton.Left);
            window.MouseMove(At(X(40)), RawInputModifiers.LeftMouseButton);
            window.MouseUp(At(X(40)), MouseButton.Left);
            (chart.View.End - chart.View.Start).ShouldBe(20 * ChartTime.Day, ChartTime.Day / 100);
            ChartTime.FromMs(chart.View.Start).ShouldBe(Start.AddDays(30), TimeSpan.FromHours(1));

            // Resize: drag the end grip further right.
            window.MouseDown(At(X(50)), MouseButton.Left);
            window.MouseMove(At(X(70)), RawInputModifiers.LeftMouseButton);
            window.MouseUp(At(X(70)), MouseButton.Left);
            ChartTime.FromMs(chart.View.End).ShouldBe(Start.AddDays(70), TimeSpan.FromHours(12));
            ChartTime.FromMs(chart.View.Start).ShouldBe(Start.AddDays(30), TimeSpan.FromHours(1));

            // New window: drag across empty track.
            window.MouseDown(At(X(80)), MouseButton.Left);
            window.MouseMove(At(X(95)), RawInputModifiers.LeftMouseButton);
            window.MouseUp(At(X(95)), MouseButton.Left);
            ChartTime.FromMs(chart.View.Start).ShouldBe(Start.AddDays(80), TimeSpan.FromHours(12));
            ChartTime.FromMs(chart.View.End).ShouldBe(Start.AddDays(95), TimeSpan.FromHours(12));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Too_Few_Points_Show_The_Empty_State()
    {
        var chart = new BrushChart { Label = "DAU", ItemsSource = new[] { new ChartPoint(Start, 1) } };
        var window = TestHelpers.Show(chart);
        try
        {
            chart.IsEmptySeries.ShouldBeTrue();
            chart.Summary.ShouldBe("DAU. No data yet.");
        }
        finally
        {
            window.Close();
        }
    }
}
