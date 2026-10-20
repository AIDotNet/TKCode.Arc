using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace TKCode.Arc.Tests.Charts;

public class RidgelineTests
{
    private static RidgelineSeries[] Months() =>
    [
        new("jan", "Jan", Enumerable.Range(0, 21).Select(i => (double)i).ToArray()),
        new("jul", "Jul", Enumerable.Range(10, 21).Select(i => (double)i).ToArray())
    ];

    [AvaloniaFact]
    public void Statistics_Match_Arc()
    {
        var stats = RidgelineStats.Of([4, 1, 3, 2, double.NaN, 5]);
        stats.N.ShouldBe(5);
        stats.Min.ShouldBe(1);
        stats.Max.ShouldBe(5);
        stats.Median.ShouldBe(3);
        stats.Q1.ShouldBe(2);
        stats.Q3.ShouldBe(4);
        stats.FractionAtOrBelow(2.5).ShouldBe(0.4);
        stats.Silverman().ShouldBeGreaterThan(0);
        var xs = new double[Ridgeline.Samples];
        var ys = new double[Ridgeline.Samples];
        stats.Density(0, 6, 0.5, xs, ys);
        xs[0].ShouldBe(0);
        xs[^1].ShouldBe(6);
        ys.Max().ShouldBeGreaterThan(0);
    }

    [AvaloniaFact]
    public void Domain_Rounds_To_Nice_Steps_Or_Stays_Fixed()
    {
        var chart = new Ridgeline { ItemsSource = Months() };
        var window = TestHelpers.Show(chart);
        try
        {
            var (lo, hi) = chart.Domain;
            lo.ShouldBeLessThan(0);
            hi.ShouldBeGreaterThan(30);
            (lo % 5).ShouldBe(0);
            chart.DomainMinimum = -10;
            chart.DomainMaximum = 40;
            chart.Domain.ShouldBe((-10, 40));
            chart.Bounds.Height.ShouldBeGreaterThan(30 * 2 + 28);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Lifts_Ridges_And_Moves_The_Reading()
    {
        var chart = new Ridgeline { Label = "Highs", Unit = "°C", ItemsSource = Months(), DomainMinimum = -10, DomainMaximum = 40 };
        var lifted = new List<string?>();
        chart.ActiveIdChanged += (_, e) => lifted.Add(e.Id);
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            chart.ActiveId.ShouldBe("jan");
            chart.CursorValue.ShouldBe(10);
            chart.Reading!.ShouldStartWith("Jan: median 10°C, middle half 5°C to 15°C, range 0°C to 20°C, 21 values");
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            chart.CursorValue!.Value.ShouldBeGreaterThan(10);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            chart.ActiveId.ShouldBe("jul");
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            chart.ActiveId.ShouldBeNull();
            chart.CursorValue.ShouldBeNull();
            lifted.ShouldBe(["jan", "jul", null]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Negative_Values_Use_A_True_Minus()
    {
        var chart = new Ridgeline { Label = "Lows", Unit = "°C" };
        chart.Items.Add(new RidgelineSeries("a", "A", [-3, -2, -1]));
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Summary.ShouldBe("Lows. A: median −2°C, middle half −2.5°C to −1.5°C, range −3°C to −1°C, 3 values.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Empty_Series_Show_The_Empty_Label()
    {
        var chart = new Ridgeline { Label = "Nothing" };
        var window = TestHelpers.Show(chart);
        try
        {
            chart.Focusable.ShouldBeFalse();
            chart.Summary.ShouldBe("Nothing. No data yet.");
        }
        finally
        {
            window.Close();
        }
    }
}
