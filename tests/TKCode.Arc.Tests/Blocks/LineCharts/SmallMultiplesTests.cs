using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.LineCharts;

/// <summary>
/// <see cref="SmallMultiples"/> on invented samples (four series over 24 months, one flat and one falling): growth and
/// flat trends, the two y scales, the panel orders, the synced crosshair from pointer, keyboard and code, dimming, the
/// readout, the responsive columns and plot heights, events and announcements.
/// </summary>
public class SmallMultiplesTests
{
    private static SmallMultiplesPeriod[] Months() =>
        Enumerable.Range(0, 24)
            .Select(i => new DateTime(2024, 11, 1).AddMonths(i))
            .Select(d => new SmallMultiplesPeriod(d.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                d.ToString("MMMM yyyy", CultureInfo.InvariantCulture)))
            .ToArray();

    private static double[] Line(double start, double end) =>
        Enumerable.Range(0, 24).Select(i => start + (end - start) * i / 23).ToArray();

    // Growth against 12 months earlier: Alpha +21.1%, Bravo +1.0% (flat), Charlie −14.8%, Delta +5.0% (just flat).
    private static SmallMultiplesSeries[] Samples() =>
    [
        new("a", "Alpha", Line(800, 1200)),
        new("b", "Bravo", Line(400, 408)),
        new("c", "Charlie", Line(600, 450)),
        new("d", "Delta", Line(100, 110)),
    ];

    private sealed class Rig : IDisposable
    {
        public Rig(double width = 1000, ArcMotionLevel level = ArcMotionLevel.Off, Action<SmallMultiples>? configure = null)
        {
            Chart = new SmallMultiples { Title = "Revenue by market", Description = "Monthly net revenue" };
            Chart.Periods.AddRange(Months());
            Chart.Series.AddRange(Samples());
            configure?.Invoke(Chart);
            Chart.CrosshairIndexChanged += (_, e) => Indexes.Add(e.Index);
            Chart.SortChanged += (_, e) => Sorts.Add(e.Sort);
            Chart.ScaleChanged += (_, e) => Scales.Add(e.Scale);
            Host = new Border { Width = width, Child = Chart, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            Window = TestHelpers.Show(Host, level);
            Window.Width = Math.Max(width + 40, 600);
            Window.Height = 900;
            Pump();
        }

        public SmallMultiples Chart { get; }

        public Border Host { get; }

        public Window Window { get; }

        public List<int?> Indexes { get; } = new();

        public List<SmallMultiplesSort> Sorts { get; } = new();

        public List<SmallMultiplesScale> Scales { get; } = new();

        public SmallMultiplesGrid Grid => Chart.Grid;

        public SmallMultiplesPanel Panel(string key) => Grid.Ordered.Single(p => p.Row.Key == key);

        public string[] Order => Grid.Ordered.Select(p => p.Row.Key).ToArray();

        public TextBlock Text(string part) => TestHelpers.Descendant<TextBlock>(Chart, part);

        public void Pump()
        {
            for (var i = 0; i < 3; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
            }
        }

        public void Key(Key key)
        {
            Window.KeyPressQwerty(PhysicalKeyOf(key), RawInputModifiers.None);
            Pump();
        }

        private static PhysicalKey PhysicalKeyOf(Key key) => key switch
        {
            Avalonia.Input.Key.Left => PhysicalKey.ArrowLeft,
            Avalonia.Input.Key.Right => PhysicalKey.ArrowRight,
            Avalonia.Input.Key.Home => PhysicalKey.Home,
            Avalonia.Input.Key.End => PhysicalKey.End,
            Avalonia.Input.Key.PageUp => PhysicalKey.PageUp,
            Avalonia.Input.Key.PageDown => PhysicalKey.PageDown,
            Avalonia.Input.Key.Escape => PhysicalKey.Escape,
            _ => PhysicalKey.Tab,
        };

        public void Dispose() => Window.Close();
    }

    // Math -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Growth_Compares_With_The_Lagged_Period_And_Flags_Flat_Trends()
    {
        var rows = SmallMultiplesMath.BuildRows(Samples(), 24, 12, 0.05);
        var alpha = rows.Single(r => r.Key == "a");
        alpha.CompareIndex.ShouldBe(11);
        alpha.Growth.ShouldBe((1200 - alpha.Values[11]) / alpha.Values[11], 1e-9);
        alpha.IsFlat.ShouldBeFalse();
        rows.Single(r => r.Key == "b").IsFlat.ShouldBeTrue();
        rows.Single(r => r.Key == "c").Growth.ShouldBeLessThan(0);
        rows.Single(r => r.Key == "d").IsFlat.ShouldBeTrue();
    }

    [Fact]
    public void A_Lag_Longer_Than_The_Data_Compares_With_The_First_Period()
    {
        var rows = SmallMultiplesMath.BuildRows([new SmallMultiplesSeries("x", "X", [100, 150, 200])], 3, 12, 0.05);
        rows[0].CompareIndex.ShouldBe(0);
        rows[0].Growth.ShouldBe(1.0, 1e-9);
    }

    [Fact]
    public void Independent_Domain_Pads_The_Own_Range_And_Shared_Starts_At_Zero()
    {
        var rows = SmallMultiplesMath.BuildRows(Samples(), 24, 12, 0.05);
        var (min, max) = rows.Single(r => r.Key == "a").Independent;
        min.ShouldBe(800 - 40, 1e-9);
        max.ShouldBe(1200 + 40, 1e-9);
        var shared = SmallMultiplesMath.SharedDomain(rows);
        shared.Min.ShouldBe(0);
        shared.Max.ShouldBe(1200 * 1.08, 1e-9);
    }

    [Fact]
    public void Sorting_Orders_By_Latest_Growth_Or_Name_And_Keeps_Ties_Stable()
    {
        var rows = SmallMultiplesMath.BuildRows(Samples(), 24, 12, 0.05);
        SmallMultiplesMath.Sorted(rows, SmallMultiplesSort.Latest, CultureInfo.InvariantCulture).Select(r => r.Key)
            .ShouldBe(["a", "c", "b", "d"]);
        SmallMultiplesMath.Sorted(rows, SmallMultiplesSort.Growth, CultureInfo.InvariantCulture).Select(r => r.Key)
            .ShouldBe(["a", "d", "b", "c"]);
        SmallMultiplesMath.Sorted(rows, SmallMultiplesSort.Name, CultureInfo.InvariantCulture).Select(r => r.Key)
            .ShouldBe(["a", "b", "c", "d"]);
    }

    [Fact]
    public void Default_Formats_Read_Thousands_Of_Dollars_And_Signed_Percentages()
    {
        LineChartsFormat.ThousandsOfDollars(412).ShouldBe("$412k");
        LineChartsFormat.ThousandsOfDollars(1710).ShouldBe("$1.71M");
        LineChartsFormat.ThousandsOfDollars(1000).ShouldBe("$1M");
        LineChartsFormat.SignedPercent(0.199).ShouldBe("+19.9%");
        LineChartsFormat.SignedPercent(-0.119).ShouldBe("−11.9%");
    }

    [Fact]
    public void Nice_Ticks_Use_One_Two_Or_Five_Steps()
    {
        LineChartsFormat.NiceTicks(1100, 1750, 3).ShouldBe([1200, 1400, 1600]);
        LineChartsFormat.NiceZeroAxis(6041, 4).ShouldBe((2000, 8000));
    }

    // Control ----------------------------------------------------------------------------------------------------------------

    [AvaloniaFact]
    public void Shows_One_Panel_Per_Series_In_Latest_Order_With_Four_Columns()
    {
        using var rig = new Rig();
        rig.Order.ShouldBe(["a", "c", "b", "d"]);
        rig.Grid.ColumnCount.ShouldBe(4);
        rig.Grid.PlotHeight.ShouldBe(92);
        rig.Panel("a").Bounds.Width.ShouldBe(rig.Panel("c").Bounds.X, 1);
    }

    [AvaloniaFact]
    public void Narrow_Containers_Use_Two_Columns_Shorter_Plots_And_The_Phone_Layout()
    {
        using var rig = new Rig(width: 420);
        rig.Grid.ColumnCount.ShouldBe(2);
        rig.Grid.PlotHeight.ShouldBe(76);
        rig.Grid.IsPhone.ShouldBeTrue();
        rig.Chart.Classes.Contains(SmallMultiples.CompactPseudoClass).ShouldBeTrue();
        rig.Panel("b").Bounds.Y.ShouldBeGreaterThan(0);
    }

    [AvaloniaFact]
    public void Wide_Containers_Get_The_Tallest_Plots()
    {
        using var rig = new Rig(width: 1200);
        rig.Grid.PlotHeight.ShouldBe(104);
    }

    [AvaloniaFact]
    public void Readout_Shows_The_Latest_Period_And_Total_Without_A_Crosshair()
    {
        using var rig = new Rig();
        rig.Text(SmallMultiples.PeriodPart).Text.ShouldBe("October 2026");
        rig.Text(SmallMultiples.StatePart).Text.ShouldBe("Latest");
        rig.Text(SmallMultiples.TotalPart).Text.ShouldBe("$2.17M");
    }

    [AvaloniaFact]
    public void Switching_The_Scale_Moves_Every_Panel_To_The_Shared_Domain_And_Back()
    {
        using var rig = new Rig();
        rig.Chart.Scale = SmallMultiplesScale.Shared;
        rig.Pump();
        rig.Panel("d").TargetDomain.ShouldBe(rig.Chart.SharedDomain);
        rig.Scales.ShouldBe([SmallMultiplesScale.Shared]);
        rig.Chart.Scale = SmallMultiplesScale.Independent;
        rig.Pump();
        rig.Panel("d").TargetDomain.Min.ShouldBe(rig.Panel("d").Row.Independent.Min, 1e-9);
    }

    [AvaloniaFact]
    public void The_Scale_Control_Writes_The_Property()
    {
        using var rig = new Rig();
        var control = TestHelpers.Descendant<SegmentedControl>(rig.Chart, SmallMultiples.ScaleSwitchPart);
        control.SelectedIndex = 1;
        rig.Pump();
        rig.Chart.Scale.ShouldBe(SmallMultiplesScale.Shared);
    }

    [AvaloniaFact]
    public void Sorting_Reorders_The_Panels_And_Raises_SortChanged()
    {
        using var rig = new Rig();
        rig.Chart.Sort = SmallMultiplesSort.Name;
        rig.Pump();
        rig.Order.ShouldBe(["a", "b", "c", "d"]);
        rig.Sorts.ShouldBe([SmallMultiplesSort.Name]);
        rig.Chart.LiveRegion!.LastAnnouncement.ShouldBe("Sorted by name.");
    }

    [AvaloniaFact]
    public void Sorting_With_Full_Motion_Flies_Panels_From_Their_Old_Places()
    {
        using var rig = new Rig(level: ArcMotionLevel.Full);
        var before = rig.Panel("b").Bounds.Position;
        rig.Chart.Sort = SmallMultiplesSort.Name;
        rig.Window.UpdateLayout();
        var panel = rig.Panel("b");
        // Right after the reorder the panel is drawn where it was and springs into its new slot.
        (panel.Bounds.Position + panel.FlipOffset).X.ShouldBe(before.X, 1);
        panel.Bounds.Position.ShouldNotBe(before);
    }

    [AvaloniaFact]
    public void Flat_Trends_Are_Dimmed_Until_The_Switch_Turns_It_Off()
    {
        using var rig = new Rig();
        rig.Panel("b").IsDimmed.ShouldBeTrue();
        rig.Panel("a").IsDimmed.ShouldBeFalse();
        rig.Panel("b").ContentOpacity.ShouldBe(SmallMultiplesPanel.DimOpacity, 1e-9);
        TestHelpers.Descendant<Switch>(rig.Chart, SmallMultiples.DimSwitchPart).IsChecked = false;
        rig.Pump();
        rig.Chart.IsFlatDimmed.ShouldBeFalse();
        rig.Panel("b").ContentOpacity.ShouldBe(1, 1e-9);
    }

    [AvaloniaFact]
    public void Code_Moves_The_Crosshair_And_The_Readout_Follows()
    {
        using var rig = new Rig();
        rig.Chart.SetCrosshairIndex(10);
        rig.Pump();
        rig.Chart.CrosshairIndex.ShouldBe(10);
        rig.Indexes.ShouldBe([10]);
        rig.Text(SmallMultiples.PeriodPart).Text.ShouldBe("September 2025");
        rig.Text(SmallMultiples.StatePart).Text.ShouldBe("11 of 24");
        rig.Grid.CrosshairPosition.ShouldBe(10);
        rig.Chart.SetCrosshairIndex(null);
        rig.Pump();
        rig.Indexes.ShouldBe([10, null]);
        double.IsNaN(rig.Grid.CrosshairPosition).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Starts_At_The_Latest_Period_And_Moves_By_Arrows_Pages_And_Ends()
    {
        using var rig = new Rig();
        rig.Grid.Focus(NavigationMethod.Tab);
        rig.Pump();
        rig.Chart.CrosshairIndex.ShouldBe(23);
        rig.Key(Key.Left);
        rig.Chart.CrosshairIndex.ShouldBe(22);
        rig.Key(Key.PageUp);
        rig.Chart.CrosshairIndex.ShouldBe(10);
        rig.Key(Key.Home);
        rig.Chart.CrosshairIndex.ShouldBe(0);
        rig.Key(Key.Left);
        rig.Chart.CrosshairIndex.ShouldBe(0);
        rig.Key(Key.End);
        rig.Chart.CrosshairIndex.ShouldBe(23);
        rig.Key(Key.Escape);
        rig.Chart.CrosshairIndex.ShouldBeNull();
    }

    [AvaloniaFact]
    public void Keyboard_Moves_Announce_The_Period_The_Total_And_The_Highest_Series()
    {
        using var rig = new Rig();
        rig.Grid.Focus(NavigationMethod.Tab);
        rig.Pump();
        rig.Key(Key.Home);
        rig.Chart.LiveRegion!.LastAnnouncement.ShouldBe("November 2024. Total $1.9M. Highest Alpha, $800k.");
    }

    [AvaloniaFact]
    public void Hovering_A_Panel_Places_The_Crosshair_On_The_Nearest_Period()
    {
        using var rig = new Rig();
        var panel = rig.Panel("c");
        var plot = panel.PlotRect;
        var point = rig.Grid.TranslatePoint(new Point(panel.Bounds.X + plot.X + plot.Width / 2, panel.Bounds.Y + plot.Center.Y),
            rig.Window)!.Value;
        rig.Window.MouseMove(point);
        rig.Pump();
        rig.Chart.CrosshairIndex.ShouldBe(12);
    }

    [AvaloniaFact]
    public void Panels_Are_Named_With_Their_Value_And_Trend()
    {
        using var rig = new Rig();
        AutomationProperties.GetName(rig.Panel("a")).ShouldBe("Alpha, $1.2M, Up 21.1% vs Oct 2025");
        AutomationProperties.GetName(rig.Panel("b")).ShouldBe("Bravo, $408k, Flat");
        AutomationProperties.GetName(rig.Panel("c")).ShouldStartWith("Charlie, $450k, Down ");
    }

    [AvaloniaFact]
    public void Custom_Formatters_Are_Used_For_Values()
    {
        using var rig = new Rig(configure: c => c.ValueFormatter = v => v.ToString("0", CultureInfo.InvariantCulture) + " u");
        rig.Text(SmallMultiples.TotalPart).Text.ShouldBe("2168 u");
    }

    [AvaloniaFact]
    public void Replacing_The_Data_Rebuilds_The_Panels_And_Keeps_Existing_Ones()
    {
        using var rig = new Rig();
        var alpha = rig.Panel("a");
        rig.Chart.Series.RemoveAt(3);
        rig.Pump();
        rig.Order.ShouldBe(["a", "c", "b"]);
        rig.Panel("a").ShouldBeSameAs(alpha);
    }

    [AvaloniaFact]
    public void Empty_Data_Sets_The_Empty_State()
    {
        using var rig = new Rig(configure: c => c.Series.Clear());
        rig.Chart.Classes.Contains(SmallMultiples.EmptyPseudoClass).ShouldBeTrue();
        rig.Grid.Ordered.ShouldBeEmpty();
    }
}
