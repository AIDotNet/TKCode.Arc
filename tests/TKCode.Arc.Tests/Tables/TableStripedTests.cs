using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Tables;

/// <summary>IsStriped on Table: odd rows by shown order, under virtualization, sort and edits; selection stays on top.</summary>
public class TableStripedTests
{
    private sealed record Row(string Name, int Value);

    private static ObservableCollection<Row> Rows(int count) =>
        new(Enumerable.Range(0, count).Select(i => new Row($"Row {i:D3}", count - i)));

    private static Table Create(ObservableCollection<Row> rows, bool striped, bool selectable = false)
    {
        var table = new Table { ItemsSource = rows, IsStriped = striped, IsSelectable = selectable, Height = 400 };
        table.Columns.Add(DataTableColumn.Create<Row>("Name", r => r.Name));
        table.Columns.Add(DataTableColumn.Create<Row>("Value", r => r.Value));
        return table;
    }

    private static Window Show(Table table)
    {
        var window = TestHelpers.Show(table);
        window.Width = 800;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static List<TableRow> Realized(Table table) =>
        table.Presenter!.RealizedRows.Where(r => !r.IsLeaving).OrderBy(r => r.DisplayIndex).ToList();

    private static void ShouldAlternate(Table table)
    {
        var plain = table.Palette.Surface;
        var stripe = table.Palette.Stripe[0];
        stripe.ShouldNotBe(plain);
        var rows = Realized(table);
        rows.Count.ShouldBeGreaterThan(2);
        foreach (var row in rows)
        {
            row.BodyFill.ShouldBe((row.DisplayIndex & 1) == 1 ? stripe : plain, $"row {row.DisplayIndex}");
        }
    }

    [AvaloniaFact]
    public void Is_Off_By_Default_And_Rows_Share_One_Fill()
    {
        new Table().IsStriped.ShouldBeFalse();
        var table = Create(Rows(6), striped: false);
        var window = Show(table);
        try
        {
            Realized(table).ShouldAllBe(r => !r.IsStriped && r.BodyFill == table.Palette.Surface);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Odd_Rows_Get_The_Stripe_And_Toggling_Repaints()
    {
        var table = Create(Rows(6), striped: true);
        var window = Show(table);
        try
        {
            ShouldAlternate(table);
            table.IsStriped = false;
            Dispatcher.UIThread.RunJobs();
            Realized(table).ShouldAllBe(r => r.BodyFill == table.Palette.Surface);
            table.IsStriped = true;
            Dispatcher.UIThread.RunJobs();
            ShouldAlternate(table);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stripes_Follow_Display_Order_After_A_Sort()
    {
        var table = Create(Rows(8), striped: true);
        var window = Show(table);
        try
        {
            table.Sort(table.Columns[1], DataTableSortDirection.Ascending);
            Dispatcher.UIThread.RunJobs();
            ((Row)Realized(table)[0].Item!).Value.ShouldBe(1);
            ShouldAlternate(table);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stripes_Stay_Correct_When_Scrolling_Recycles_Rows()
    {
        var rows = Rows(300);
        var table = Create(rows, striped: true);
        var window = Show(table);
        try
        {
            Realized(table).Count.ShouldBeLessThan(100);
            foreach (var index in new[] { 120, 251, 299, 7 })
            {
                table.ScrollIntoView(rows[index]);
                Dispatcher.UIThread.RunJobs();
                Realized(table).ShouldContain(r => r.DisplayIndex == index);
                ShouldAlternate(table);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stripes_Realternate_After_Insert_And_Remove()
    {
        var rows = Rows(6);
        var table = Create(rows, striped: true);
        var window = Show(table);
        try
        {
            rows.Insert(0, new Row("Inserted", 99));
            Dispatcher.UIThread.RunJobs();
            ShouldAlternate(table);
            rows.RemoveAt(2);
            Dispatcher.UIThread.RunJobs();
            ShouldAlternate(table);
            rows.RemoveAt(0);
            Dispatcher.UIThread.RunJobs();
            ShouldAlternate(table);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selection_Beats_The_Stripe()
    {
        var rows = Rows(6);
        var table = Create(rows, striped: true, selectable: true);
        var window = Show(table);
        try
        {
            table.SelectedItems = new List<Row> { rows[1], rows[2] };
            Dispatcher.UIThread.RunJobs();
            var palette = table.Palette;
            var odd = Realized(table)[1];
            var even = Realized(table)[2];
            odd.IsSelected.ShouldBeTrue();
            odd.BodyFill.ShouldBe(palette.Selected[0]);
            even.BodyFill.ShouldBe(palette.Selected[0]);
            odd.BodyFill.ShouldNotBe(palette.Stripe[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_Stripe_Differs_From_The_Surface_In_Light_And_Dark(bool dark)
    {
        var token = new TableToken();
        token.CalculateTokenValues(dark);
        var n = ArcPalette.Neutrals(dark);
        token.StripeBackground.ShouldNotBe(n.Surface);
        token.StripeColumnBackground.ShouldNotBe(n.SurfaceMuted);
    }
}
