using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Charts;

public class WaffleChartTests
{
    private sealed record Share(string Name, double Amount);

    private static WaffleChart Energy() => new()
    {
        Label = "Energy",
        Unit = "TWh",
        ItemsSource = new[]
        {
            new WaffleCategory("coal", "Coal", 35), new WaffleCategory("gas", "Gas", 22),
            new WaffleCategory("wind", "Wind", 13), new WaffleCategory("other", "Other", 30)
        }
    };

    [AvaloniaFact]
    public void Allocates_Whole_Cells_By_Largest_Remainder()
    {
        var thirds = WaffleChart.Allocate([new("a", "A", 1), new("b", "B", 1), new("c", "C", 1)], 3, 100);
        thirds.ShouldBe([34, 33, 33]);
        thirds.Sum().ShouldBe(100);
        var negative = WaffleChart.Allocate([new("a", "A", -5), new("b", "B", 5)], 5, 100);
        negative.ShouldBe([0, 100]);
    }

    [AvaloniaFact]
    public void Defaults_Match_Arc()
    {
        var chart = new WaffleChart();
        chart.Rows.ShouldBe(10);
        chart.Columns.ShouldBe(10);
        chart.IsLegendVisible.ShouldBeTrue();
        chart.Decimals.ShouldBe(0);
        chart.EmptyLabel.ShouldBe("No data");
    }

    [AvaloniaFact]
    public void First_Category_Takes_Series1_And_Neutral_Rank_Shapes_Cycle()
    {
        var chart = Energy();
        var window = TestHelpers.Show(chart);
        try
        {
            chart.CellCounts.ShouldBe([35, 22, 13, 30]);
            chart.ColorOf("coal").ShouldBe(chart.Palette.Series(0));
            chart.ColorOf("gas").ShouldBe(chart.Palette.Series(1));
            chart.ShapeOf("coal").ShouldBe(ChartsBSwatchShape.Square);
            chart.ShapeOf("wind").ShouldBe(ChartsBSwatchShape.Round);
            chart.ShapeOf("other").ShouldBe(ChartsBSwatchShape.Framed);
            chart.AccentKey = WaffleChart.NoAccent;
            chart.ColorOf("coal").ShouldBe(chart.Palette.Series(1));
            chart.LegendItems.Count.ShouldBe(4);
            chart.Summary.ShouldBe("Energy. Coal 35%, Gas 22%, Wind 13%, Other 30%.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Walks_Cells_And_Jumps_Between_Categories()
    {
        var chart = Energy();
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Grid.Focus(NavigationMethod.Tab);
            chart.Grid.PointerSlot.ShouldBe(0);
            chart.Grid.Reading.ShouldBe("Coal, 35%, 35 TWh");
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            chart.Grid.PointerSlot.ShouldBe(1);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.Grid.PointerSlot.ShouldBe(11);
            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            chart.Grid.PointerSlot.ShouldBe(35);
            chart.Grid.PointerKey.ShouldBe("gas");
            window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            chart.Grid.PointerSlot.ShouldBe(0);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            chart.Grid.PointerSlot.ShouldBe(99);
            chart.Grid.PointerKey.ShouldBe("other");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.Grid.PointerSlot.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Hover_Reads_The_Cell_Under_The_Pointer()
    {
        var chart = Energy();
        var window = TestHelpers.Show(chart);
        try
        {
            // Bottom-left cell is slot 0 (coal); the grid sits in the first column of the side layout.
            var grid = chart.Grid;
            var bottomLeft = grid.TranslatePoint(new Avalonia.Point(4, grid.Bounds.Height - 4), window)!.Value;
            window.MouseMove(bottomLeft);
            Dispatcher.UIThread.RunJobs();
            grid.PointerKey.ShouldBe("coal");
            chart.LegendItems[1].Key.ShouldBe("gas");
            window.MouseMove(new Avalonia.Point(-50, -50));
            Dispatcher.UIThread.RunJobs();
            grid.PointerKey.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Legend_Toggles_The_Pinned_Category()
    {
        var chart = Energy();
        string? raised = "unset";
        chart.ActiveKeyChanged += (_, e) => raised = e.Key;
        var window = TestHelpers.Show(chart);
        try
        {
            var item = chart.LegendItems[1];
            item.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            chart.ActiveKey.ShouldBe("gas");
            raised.ShouldBe("gas");
            item.IsPinned.ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            chart.ActiveKey.ShouldBeNull();
            raised.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reads_Any_Items_Through_Selectors_Or_Bindings()
    {
        var items = new[] { new Share("Yes", 3), new Share("No", 1) };
        var selectors = new WaffleChart { ItemsSource = items, KeySelector = o => ((Share)o).Name, ValueSelector = o => ((Share)o).Amount };
        var bindings = new WaffleChart
        {
            ItemsSource = items,
            KeyMemberBinding = new ReflectionBinding(nameof(Share.Name)),
            ValueMemberBinding = new ReflectionBinding(nameof(Share.Amount))
        };
        var window = TestHelpers.Show(new Avalonia.Controls.StackPanel { Children = { selectors, bindings } });
        try
        {
            selectors.CellCounts.ShouldBe([75, 25]);
            bindings.CellCounts.ShouldBe([75, 25]);
            bindings.Categories[0].Label.ShouldBe("Yes");
            bindings.Categories[0].Source.ShouldBe(items[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Collection_Changes_Reassign_Cells()
    {
        var chart = new WaffleChart();
        chart.Items.Add(new WaffleCategory("a", "A", 1));
        var window = TestHelpers.Show(chart);
        try
        {
            chart.CellCounts.ShouldBe([100]);
            chart.Items.Add(new WaffleCategory("b", "B", 1));
            chart.CellCounts.ShouldBe([50, 50]);
            chart.LegendItems.Count.ShouldBe(2);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_Chart_Is_Not_Focusable_And_Says_Why()
    {
        var chart = new WaffleChart { Label = "Answers" };
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Grid.Focusable.ShouldBeFalse();
            chart.LegendItems.Count.ShouldBe(0);
            chart.Summary.ShouldBe("Answers. No data.");
            var peer = ControlAutomationPeer.CreatePeerForElement(chart.Grid);
            peer.GetHelpText().ShouldBe("Answers. No data.");
            peer.GetLocalizedControlType().ShouldBe("waffle chart");
        }
        finally
        {
            window.Close();
        }
    }
}
