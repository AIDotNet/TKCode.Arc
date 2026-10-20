using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace TKCode.Arc.Tests.Charts;

public class TreemapTests
{
    private static TreemapNode Revenue() => new("all", "Revenue", children:
    [
        new TreemapNode("amer", "Americas", children: [new TreemapNode("us", "United States", 60, 10), new TreemapNode("ca", "Canada", 20, 30)]),
        new TreemapNode("emea", "Europe", children: [new TreemapNode("de", "Germany", 15, -5), new TreemapNode("fr", "France", 5)])
    ]);

    [AvaloniaFact]
    public void Flatten_Sums_Branches_And_Weights_The_Color_Measure()
    {
        var flat = TreemapLayout.Flatten(Revenue());
        flat["all"].Value.ShouldBe(100);
        flat["amer"].Value.ShouldBe(80);
        flat["amer"].Color!.Value.ShouldBe(15, 1e-9);
        flat["emea"].Color!.Value.ShouldBe(-5, 1e-9);
        flat["de"].Path.ShouldBe(["Europe", "Germany"]);
        flat["de"].Depth.ShouldBe(2);
    }

    [AvaloniaFact]
    public void Squarified_Tiles_Fill_The_View_In_Proportion()
    {
        var rects = new Dictionary<string, Rect>();
        TreemapLayout.Squarify([("a", 6), ("b", 6), ("c", 4), ("d", 3), ("e", 2), ("f", 2), ("g", 1)], new Rect(0, 0, 600, 400), rects);
        rects.Values.Sum(r => r.Width * r.Height).ShouldBe(240000, 1e-6);
        (rects["a"].Width * rects["a"].Height).ShouldBe(240000 * 6 / 24.0, 1e-6);
        foreach (var r in rects.Values)
        {
            new Rect(0, 0, 600, 400).Contains(r.Center).ShouldBeTrue();
        }
    }

    [AvaloniaFact]
    public void Enter_Zooms_Into_A_Branch_And_Escape_Zooms_Out()
    {
        var chart = new Treemap { Label = "Revenue", Root = Revenue(), PlotHeight = 200 };
        var focused = new List<string>();
        chart.FocusedNodeChanged += (_, e) => focused.Add(e.Id);
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Stage.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.Stage.ActiveId.ShouldBe("amer");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.Stage.ActiveId.ShouldBe("emea");
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            chart.FocusedNodeId.ShouldBe("emea");
            chart.Stage.ActiveId.ShouldBe("de");
            chart.Crumbs.Trail.ShouldBe(["all", "emea"]);
            chart.Stage.RectOf("de").ShouldNotBeNull();
            chart.Stage.RectOf("us").ShouldBeNull();
            chart.Stage.Reading.ShouldBe("Europe, Germany, 15, 75% of Europe");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.FocusedNodeId.ShouldBeNull();
            chart.Stage.ActiveId.ShouldBe("emea");
            focused.ShouldBe(["emea", "all"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Breadcrumb_Zooms_Back_To_Its_Level()
    {
        var chart = new Treemap { Label = "Revenue", Root = Revenue(), PlotHeight = 200, FocusedNodeId = "amer" };
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Crumbs.Trail.ShouldBe(["all", "amer"]);
            chart.Crumbs.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            chart.FocusedNodeId.ShouldBeNull();
            chart.Crumbs.Trail.ShouldBe(["all"]);
            chart.Crumbs.Focusable.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Color_Measure_Deepens_Tiles_In_Steps()
    {
        var chart = new Treemap { Root = Revenue(), ColorLabel = "Growth" };
        var window = TestHelpers.Show(chart);
        try
        {
            chart.IsColored.ShouldBeTrue();
            chart.ShadeOf(-5).ShouldBe(0.14, 1e-9);
            chart.ShadeOf(30).ShouldBe(0.58, 1e-9);
            chart.ShadeOf(null).ShouldBe(0.34, 1e-9);
            chart.HueOf("us").ShouldBe(chart.Palette.Series(0));
            chart.HueOf("de").ShouldBe(chart.Palette.Series(1));
            chart.ColorLabel = null;
            chart.ShadeOf(30).ShouldBe(0.34, 1e-9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reads_A_Hierarchy_Through_Selectors()
    {
        var data = new[] { ("Team A", new[] { ("Ann", 3.0), ("Bo", 1.0) }), ("Team B", new[] { ("Cy", 4.0) }) };
        var chart = new Treemap
        {
            Label = "Hours",
            ItemsSource = data.Select(t => (object)t).ToArray(),
            KeySelector = o => o is ValueTuple<string, (string, double)[]> team ? team.Item1 : (((string, double))o).Item1,
            ValueSelector = o => o is ValueTuple<string, double> person ? person.Item2 : 0,
            ChildrenSelector = o => o is ValueTuple<string, (string, double)[]> team ? team.Item2.Select(p => (object)p).ToArray() : null
        };
        var window = TestHelpers.Show(chart);
        try
        {
            chart.RootFlat.Label.ShouldBe("Hours");
            chart.RootFlat.Value.ShouldBe(8);
            chart.Flat["Team A"].Value.ShouldBe(4);
            chart.Flat["Ann"].Path.ShouldBe(["Team A", "Ann"]);
        }
        finally
        {
            window.Close();
        }
    }
}
