using TKCode.Arc.Primitives;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Charts;

public class DonutChartTests
{
    private static DonutChart Create() => new()
    {
        Label = "Visits by source",
        Unit = "visits",
        ItemsSource = new[]
        {
            new ChartPoint("Search", 50), new ChartPoint("Direct", 30), new ChartPoint("Social", 14),
            new ChartPoint("Email", 3), new ChartPoint("Podcasts", 2), new ChartPoint("Print", 1)
        }
    };

    private static string CenterText(DonutChart chart, string part)
        => (chart.GetVisualDescendants().OfType<ArcContentSwap>().First(s => s.Name == part).Content as TextBlock)?.Text ?? string.Empty;

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var chart = new DonutChart();
        chart.Diameter.ShouldBe(208);
        chart.RingThickness.ShouldBe(24);
        chart.GroupBelow.ShouldBe(0.04);
        chart.MaxSegments.ShouldBe(6);
        chart.TotalLabel.ShouldBe("Total");
        chart.OtherLabel.ShouldBe("Other");
        chart.EmptyLabel.ShouldBe("No data yet");
        chart.LegendAction.ShouldBe(DonutLegendAction.Toggle);
    }

    [AvaloniaFact]
    public void Small_Parts_Fold_Into_Other()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Slices.Select(s => s.Label).ShouldBe(["Search", "Direct", "Social", "Other"]);
            chart.Slices[^1].Value.ShouldBe(6);
            DonutChart.MembersText(chart.Slices[^1]).ShouldBe("Email, Podcasts and 1 more");
            chart.Total.ShouldBe(100);
            CenterText(chart, DonutChart.CenterLabelPart).ShouldBe("Total");
            CenterText(chart, DonutChart.CenterValuePart).ShouldBe("100");
            chart.DataTableText.ShouldContain("Email (Other)\t3 visits\t3%");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Single_Small_Part_Is_Not_Grouped()
    {
        var chart = new DonutChart { ItemsSource = new[] { new ChartPoint("A", 90), new ChartPoint("B", 8), new ChartPoint("C", 2) } };
        chart.Data.Count.ShouldBe(3);
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Slices.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Legend_Rows_Toggle_Segments_And_Recount_The_Total()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            IReadOnlyList<string>? hidden = null;
            chart.HiddenKeysChanged += (_, e) => hidden = e.Hidden;
            var rows = chart.GetVisualDescendants().OfType<DonutChartLegendRow>().ToList();
            rows.Count.ShouldBe(4);
            rows[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            chart.HiddenKeys.ShouldBe(["Search"]);
            hidden.ShouldBe(["Search"]);
            chart.Total.ShouldBe(50);
            chart.Announcement.ShouldBe("Search hidden. Total 50 visits.");
            // Arrow keys walk the rows and preview the focused segment in the center.
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            rows[1].IsFocused.ShouldBeTrue();
            CenterText(chart, DonutChart.CenterLabelPart).ShouldBe("Direct");
            CenterText(chart, DonutChart.CenterMetaPart).ShouldBe("60% of total");
            // The last visible segment cannot be hidden.
            chart.HiddenKeys = ["Search", "Direct", "Social"];
            Dispatcher.UIThread.RunJobs();
            chart.ToggleHidden(DonutChart.OtherKey);
            chart.HiddenKeys!.Count.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Select_Mode_Pins_The_Active_Key()
    {
        var chart = Create();
        chart.LegendAction = DonutLegendAction.Select;
        var window = TestHelpers.Show(chart);
        try
        {
            var changes = 0;
            chart.ActiveKeyChanged += (_, _) => changes++;
            var rows = chart.GetVisualDescendants().OfType<DonutChartLegendRow>().ToList();
            rows[2].Activate();
            chart.ActiveKey.ShouldBe("Social");
            changes.ShouldBe(1);
            chart.ActiveIndex.ShouldBe(2);
            CenterText(chart, DonutChart.CenterValuePart).ShouldBe("14");
            rows[2].Activate();
            chart.ActiveKey.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Ring_Without_Legend_Is_A_Keyboard_Stop()
    {
        var chart = Create();
        chart.ShowLegend = false;
        var window = TestHelpers.Show(chart);
        try
        {
            var plot = TestHelpers.Descendant<ChartPlot>(chart);
            plot.Focusable.ShouldBeTrue();
            plot.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.CurrentKey.ShouldBe("Search");
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.CurrentKey.ShouldBe(DonutChart.OtherKey);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            chart.ActiveKey.ShouldBe(DonutChart.OtherKey);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.ActiveKey.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Colors_Follow_Keys_Across_Datasets()
    {
        var chart = Create();
        var window = TestHelpers.Show(chart);
        try
        {
            var social = chart.Slices.First(s => s.Key == "Social");
            var before = chart.SliceColor(social);
            chart.ItemsSource = new[] { new ChartPoint("Social", 40), new ChartPoint("Search", 60) };
            Dispatcher.UIThread.RunJobs();
            chart.SliceColor(chart.Slices.First(s => s.Key == "Social")).ShouldBe(before);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_Data_Shows_The_Empty_Label()
    {
        var chart = new DonutChart { Label = "Spend" };
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Summary.ShouldBe("Spend. No data yet.");
            CenterText(chart, DonutChart.CenterLabelPart).ShouldBe("No data yet");
        }
        finally
        {
            window.Close();
        }
    }
}
