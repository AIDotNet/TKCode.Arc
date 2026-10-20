using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace TKCode.Arc.Tests.Charts;

public class SlopeChartTests
{
    private static SlopeChart Channels() => new()
    {
        Label = "Conversion",
        StartLabel = "Q1",
        EndLabel = "Q2",
        ItemsSource = new[]
        {
            new SlopeItem("search", "Search", 3, 5), new SlopeItem("email", "Email", 6, 4), new SlopeItem("social", "Social", 2, 3)
        }
    };

    [AvaloniaFact]
    public void Spread_Pushes_Touching_Labels_Apart_Around_Their_Centre()
    {
        var spread = SlopeChart.Spread([100, 105, 300], 20, 8, 400);
        (spread[1] - spread[0]).ShouldBe(20, 1e-9);
        ((spread[0] + spread[1]) / 2).ShouldBe(102.5 + 0, 1e-9);
        spread[2].ShouldBe(300);
        var clamped = SlopeChart.Spread([2, 4], 20, 8, 400);
        clamped[0].ShouldBe(8);
        clamped[1].ShouldBe(28);
    }

    [AvaloniaFact]
    public void Defaults_And_Height_Follow_Arc()
    {
        var chart = Channels();
        chart.IsRankVisible.ShouldBeTrue();
        chart.EmptyLabel.ShouldBe("No data");
        var window = TestHelpers.Show(chart);
        try
        {
            chart.PlotHeightResolved.ShouldBe(200);
            chart.DesiredSize.Height.ShouldBe(228);
            chart.PlotHeight = 300;
            chart.PlotHeightResolved.ShouldBe(300);
            chart.Summary.ShouldBe("Conversion, Q1 to Q2. Search 3 to 5, Email 6 to 4, Social 2 to 3.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Moves_In_End_Value_Order()
    {
        var chart = Channels();
        var keys = new List<string?>();
        chart.ActiveKeyChanged += (_, e) => keys.Add(e.Key);
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Focus(NavigationMethod.Tab);
            chart.ActiveKey.ShouldBe("search");
            chart.Reading.ShouldBe("Search, Q1 3, Q2 5, +2, up 1 to rank 1");
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            chart.ActiveKey.ShouldBe("email");
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            chart.ActiveKey.ShouldBe("social");
            window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
            chart.ActiveKey.ShouldBe("email");
            chart.Reading.ShouldBe("Email, Q1 6, Q2 4, −2, down 1 to rank 2");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.ActiveKey.ShouldBeNull();
            keys.ShouldBe(["search", "email", "social", "email", null]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Formatters_Shape_The_Reading()
    {
        var chart = Channels();
        chart.ValueFormatter = v => $"{v:0.0}%";
        chart.ChangeFormatter = (change, _) => $"{change:+0.0;-0.0} pts";
        var window = TestHelpers.Show(chart);
        try
        {
            chart.ActiveKey = "social";
            chart.Reading.ShouldBe("Social, Q1 2.0%, Q2 3.0%, +1.0 pts, same rank");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointer_Picks_The_Nearest_Line()
    {
        var chart = Channels();
        var window = TestHelpers.Show(chart);
        try
        {
            // Hover just left of the first column, beside the Email label (the highest start value).
            var (start, _) = chart.LabelPositionOf("email");
            window.MouseMove(chart.TranslatePoint(new Avalonia.Point(100, 28 + start), window)!.Value);
            chart.ActiveKey.ShouldBe("email");
            window.MouseMove(new Avalonia.Point(-40, -40));
            chart.ActiveKey.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Removing_The_Active_Item_Clears_The_Reading()
    {
        var chart = Channels();
        var window = TestHelpers.Show(chart);
        try
        {
            chart.ActiveKey = "email";
            chart.ItemsSource = new[] { new SlopeItem("search", "Search", 3, 5) };
            chart.ActiveKey.ShouldBeNull();
            chart.Data.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }
}
