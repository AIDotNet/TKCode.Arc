using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Analytics;

/// <summary><see cref="PivotBuilder"/>: the engine (keys, aggregations, totals), moving fields, aggregations, the table and the chip keys.</summary>
public class PivotBuilderTests
{
    private sealed record Sale(string Region, string Quarter, double Amount);

    private static readonly Sale[] Sales =
    [
        new("North", "Q1", 10), new("North", "Q2", 20), new("South", "Q1", 5), new("South", "Q1", 15), new("North", "Q1", 30),
    ];

    private static PivotBuilder Own()
    {
        var pivot = new PivotBuilder();
        pivot.Fields.Add(PivotBuilderField.Create<Sale>("region", "Region", s => s.Region));
        pivot.Fields.Add(PivotBuilderField.Create<Sale>("quarter", "Quarter", s => s.Quarter));
        pivot.Fields.Add(PivotBuilderField.Create<Sale>("amount", "Amount", s => s.Amount, isNumeric: true));
        pivot.RowFields.Add("region");
        pivot.ColumnFields.Add("quarter");
        pivot.ValueFields.Add(new PivotBuilderValue("amount"));
        pivot.ItemsSource = Sales;
        return pivot;
    }

    [AvaloniaFact]
    public void Engine_Groups_Aggregates_And_Totals()
    {
        var pivot = Own();
        var r = pivot.Result.ShouldNotBeNull();
        r.RowKeys.Select(k => k[0]).ShouldBe(["North", "South"]);
        r.ColumnKeys.Select(k => k[0]).ShouldBe(["Q1", "Q2"]);
        r.Cell(0, 0, 0).ShouldBe(40);
        r.Cell(1, 1, 0).ShouldBeNull();
        r.RowTotal(1, 0).ShouldBe(20);
        r.ColumnTotal(0, 0).ShouldBe(60);
        r.GrandTotal(0).ShouldBe(80);

        pivot.SetAggregation("amount", PivotBuilderAggregation.Average);
        pivot.Result!.Cell(0, 0, 0).ShouldBe(20);
        pivot.SetAggregation("amount", PivotBuilderAggregation.Max);
        pivot.Result!.GrandTotal(0).ShouldBe(30);
        pivot.SetAggregation("amount", PivotBuilderAggregation.Count);
        pivot.Result!.GrandTotal(0).ShouldBe(5);
    }

    [AvaloniaFact]
    public void Moving_Fields_Changes_The_Layout_And_Raises()
    {
        var pivot = Own();
        var events = new List<PivotBuilderLayoutChangedEventArgs>();
        pivot.LayoutChanged += (_, e) => events.Add(e);
        pivot.MoveField("quarter", PivotBuilderZone.Rows, 0);
        pivot.RowFields.ShouldBe(["quarter", "region"]);
        pivot.ColumnFields.ShouldBeEmpty();
        pivot.ZoneOf("quarter").ShouldBe(PivotBuilderZone.Rows);
        pivot.Result!.RowKeys.Count.ShouldBe(3);
        pivot.MoveField("region", PivotBuilderZone.Values);
        pivot.ValueFields.Last().Aggregation.ShouldBe(PivotBuilderAggregation.Count);
        pivot.SetAggregation("region", PivotBuilderAggregation.Sum);
        pivot.ValueFields.Last().Aggregation.ShouldBe(PivotBuilderAggregation.Count);
        pivot.MoveField("region", PivotBuilderZone.None);
        pivot.ZoneOf("region").ShouldBe(PivotBuilderZone.None);
        events.Select(e => e.FieldKey).ShouldBe(["quarter", "region", "region"]);
    }

    [AvaloniaFact]
    public void Table_Shows_Cells_And_A_Total_Row()
    {
        var pivot = Own();
        var window = TestHelpers.Show(pivot);
        window.Width = 1000;
        Dispatcher.UIThread.RunJobs();
        var table = pivot.Table.ShouldNotBeNull();
        // Region, Q1, Q2, Total.
        table.Columns.Count.ShouldBe(4);
        var rows = ((IEnumerable<object>)table.ItemsSource!).Cast<PivotBuilderRow>().ToList();
        rows.Count.ShouldBe(3);
        rows[^1].IsTotal.ShouldBeTrue();
        rows[^1].Cells.ShouldBe([60, 20, 80]);
        pivot.ShowTotals = false;
        table.Columns.Count.ShouldBe(3);
        ((IEnumerable<object>)table.ItemsSource!).Count().ShouldBe(2);
        window.Close();
    }

    [AvaloniaFact]
    public void Delete_On_A_Chip_Removes_It_And_Announces()
    {
        var pivot = Own();
        var window = TestHelpers.Show(pivot);
        window.Width = 1000;
        Dispatcher.UIThread.RunJobs();
        var chip = pivot.Zones[PivotBuilderZone.Columns].Chips.Children.OfType<PivotBuilderChip>().Single();
        AutomationProperties.GetName(chip).ShouldNotBeNull().ShouldContain("Columns");
        chip.Focus(NavigationMethod.Tab);
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        pivot.ColumnFields.ShouldBeEmpty();
        pivot.Announcer!.Announcement.ShouldBe("Quarter moved to Fields");
        pivot.FieldBox!.Chips.Children.OfType<PivotBuilderChip>().Select(c => c.FieldKey).ShouldContain("quarter");
        window.Close();
    }

    [AvaloniaFact]
    public void Samples_Start_With_A_Layout()
    {
        var pivot = new PivotBuilder();
        var window = TestHelpers.Show(pivot);
        window.Width = 1000;
        Dispatcher.UIThread.RunJobs();
        pivot.IsShowingSamples.ShouldBeTrue();
        pivot.RowFields.ShouldBe(["region"]);
        pivot.Result!.RowKeys.Count.ShouldBe(4);
        pivot.Result.ColumnKeys.Count.ShouldBe(4);
        window.Close();
    }
}
