using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Charts;

public class SparklineTests
{
    private static Sparkline Create() => new()
    {
        Label = "Revenue",
        Value = "$48.2K",
        Change = "+4.1%",
        Tone = ChartTone.Success,
        ItemsSource = new[] { 12.0, 18, 15, 22, 30 },
        PointLabels = ["Mon", "Tue", "Wed", "Thu", "Fri"]
    };

    private static object? Content(Sparkline chart, string part)
        => chart.GetVisualDescendants().OfType<ArcContentSwap>().First(s => s.Name == part).Content;

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var chart = new Sparkline();
        chart.DrawingWidth.ShouldBe(160);
        chart.DrawingHeight.ShouldBe(52);
        chart.Tone.ShouldBe(ChartTone.Accent);
        chart.ShowArea.ShouldBeTrue();
        chart.IsInteractive.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Plot_Keeps_The_Drawing_Aspect_Ratio()
    {
        var chart = Create();
        chart.Width = 320;
        var window = TestHelpers.Show(chart);
        try
        {
            var plot = TestHelpers.Descendant<ChartPlot>(chart);
            plot.Bounds.Width.ShouldBe(320);
            plot.Bounds.Height.ShouldBe(104);
            AutomationProperties.GetName(chart).ShouldBe("Revenue: $48.2K. +4.1%");
            chart.Classes.Contains(Sparkline.SuccessPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Scrubbing_Rolls_The_Headline_And_Aside()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            Content(chart, Sparkline.HeadlinePart).ShouldBe("$48.2K");
            Content(chart, Sparkline.AsidePart).ShouldBe("+4.1%");
            var plot = TestHelpers.Descendant<ChartPlot>(chart);
            plot.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(3);
            Content(chart, Sparkline.HeadlinePart).ShouldBe("22");
            Content(chart, Sparkline.AsidePart).ShouldBe("Thu");
            chart.Classes.Contains(Sparkline.ScrubbingPseudoClass).ShouldBeTrue();
            chart.GetPlotValueText(plot).ShouldBe("Thu: 22");
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBeNull();
            Content(chart, Sparkline.AsidePart).ShouldBe("+4.1%");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_A_Value_The_Aside_Reads_The_Point()
    {
        var chart = new Sparkline { Label = "Visits", ItemsSource = new[] { 1200.0, 1500, 900 } };
        var window = TestHelpers.Show(chart);
        try
        {
            TestHelpers.Descendant<ChartPlot>(chart);
            chart.ActiveIndex = 1;
            Dispatcher.UIThread.RunJobs();
            Content(chart, Sparkline.AsidePart).ShouldBe("1,500");
            chart.GetPlotValueText(TestHelpers.Descendant<ChartPlot>(chart)).ShouldBe("Point 2 of 3: 1,500");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Scrubs_And_Leaving_Returns_To_The_Latest()
    {
        var chart = Create();
        chart.Width = 200;
        var window = TestHelpers.Show(chart);
        try
        {
            var plot = TestHelpers.Descendant<ChartPlot>(chart);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            window.MouseMove(plot.TranslatePoint(new Point(2, 10), window)!.Value, RawInputModifiers.None);
            chart.ActiveIndex.ShouldBe(0);
            window.MouseMove(new Point(399, 299), RawInputModifiers.None);
            chart.ActiveIndex.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Non_Interactive_Drops_The_Tab_Stop()
    {
        var chart = Create();
        chart.IsInteractive = false;
        var window = TestHelpers.Show(chart);
        try
        {
            var plot = TestHelpers.Descendant<ChartPlot>(chart);
            plot.Focusable.ShouldBeFalse();
            plot.IsHitTestVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
