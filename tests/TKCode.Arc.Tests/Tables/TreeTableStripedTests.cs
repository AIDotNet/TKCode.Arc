using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Tables;

/// <summary>IsStriped on TreeTable: odd visible rows regardless of depth; expand, collapse, sort and selection.</summary>
public class TreeTableStripedTests
{
    private sealed record Item(string Id, string Name, int Cost, List<Item>? Children = null);

    private static List<Item> Data() =>
    [
        new("a", "A", 3, [new("a1", "A1", 5), new("a2", "A2", 4, [new("a2x", "A2x", 1)])]),
        new("b", "B", 2, [new("b1", "B1", 9)]),
        new("c", "C", 1),
        new("d", "D", 7),
    ];

    private static TreeTable Create(bool striped, bool selectable = false)
    {
        var table = new TreeTable
        {
            ItemsSource = Data(),
            ChildrenSelector = i => ((Item)i).Children,
            KeySelector = i => ((Item)i).Id,
            RowLabelSelector = i => ((Item)i).Name,
            IsStriped = striped,
            IsSelectable = selectable,
            Width = 640
        };
        table.Columns.Add(TreeTableColumn.Create<Item>("Name", i => i.Name));
        table.Columns.Add(TreeTableColumn.Create<Item>("Cost", i => i.Cost));
        return table;
    }

    private static Window Show(TreeTable table)
    {
        var window = TestHelpers.Show(table);
        window.Width = 800;
        window.Height = 700;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static string[] Keys(TreeTable table) => table.VisibleRows.Select(l => (string)l.Key).ToArray();

    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    private static void ShouldAlternate(TreeTable table)
    {
        var lines = table.VisibleRows.Where(l => l.Kind == TreeTableLineKind.Row).ToList();
        lines.Count.ShouldBeGreaterThan(2);
        for (var i = 0; i < lines.Count; i++)
        {
            var row = table.RowsHost!.RowFor(lines[i].Key)!;
            row.IsStriped.ShouldBe((i & 1) == 1, $"row {lines[i].Key} at {i}");
        }
    }

    private static Color Background(TreeTable table, string key) =>
        ((ISolidColorBrush)table.RowsHost!.RowFor(key)!.Background!).Color;

    [AvaloniaFact]
    public void Is_Off_By_Default()
    {
        new TreeTable().IsStriped.ShouldBeFalse();
        var table = Create(striped: false);
        var window = Show(table);
        try
        {
            table.RowsHost!.Rows.ShouldAllBe(r => !r.IsStriped);
            Background(table, "b").ShouldBe(Background(table, "a"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Odd_Visible_Rows_Are_Striped_And_Toggling_Updates()
    {
        var table = Create(striped: true);
        var window = Show(table);
        try
        {
            ShouldAlternate(table);
            Background(table, "b").ShouldNotBe(Background(table, "a"));
            table.IsStriped = false;
            Settle();
            table.RowsHost!.Rows.ShouldAllBe(r => !r.IsStriped);
            table.IsStriped = true;
            Settle();
            ShouldAlternate(table);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Expand_And_Collapse_Restripe_By_Visible_Index_Not_Depth()
    {
        var table = Create(striped: true);
        var window = Show(table);
        try
        {
            table.ExpandAll();
            Settle();
            table.VisibleRows.Count.ShouldBe(8);
            ShouldAlternate(table);
            // a1 (depth 2) is the second visible row, a2 (depth 2) the third, a2x (depth 3) the fourth.
            table.RowsHost!.RowFor("a1")!.IsStriped.ShouldBeTrue();
            table.RowsHost.RowFor("a2")!.IsStriped.ShouldBeFalse();
            table.RowsHost.RowFor("a2x")!.IsStriped.ShouldBeTrue();
            table.Collapse(table.ItemsSource!.Cast<Item>().First(i => i.Id == "a"));
            Settle();
            Keys(table).ShouldBe(["a", "b", "b1", "c", "d"]);
            ShouldAlternate(table);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stripes_Follow_The_Order_After_A_Sort()
    {
        var table = Create(striped: true);
        var window = Show(table);
        try
        {
            table.SortKey = "Cost";
            table.SortDirection = DataTableSortDirection.Ascending;
            Settle();
            Keys(table).ShouldBe(["c", "b", "a", "d"]);
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
        var table = Create(striped: true, selectable: true);
        var window = Show(table);
        try
        {
            // c is the third row (plain), d the fourth (striped); both are leaves, so both stay selected.
            Background(table, "d").ShouldNotBe(Background(table, "c"));
            table.SelectedKeys = new object[] { "c", "d" };
            Settle();
            var odd = table.RowsHost!.RowFor("d")!;
            odd.IsStriped.ShouldBeTrue();
            odd.Classes.Contains(TreeTableRow.SelectedPseudoClass).ShouldBeTrue();
            Background(table, "d").ShouldBe(Background(table, "c"));
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
        var token = new TreeTableToken();
        token.CalculateTokenValues(dark);
        var n = ArcPalette.Neutrals(dark);
        token.StripeBackground.ShouldNotBe(n.Surface);
        token.StripeHoverBackground.ShouldNotBe(token.StripeBackground);
    }
}
