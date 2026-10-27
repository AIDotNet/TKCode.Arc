using System.Globalization;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TableSortedFromCodeDemo : UserControl
{
    private sealed record Project(string Name, string Owner, string Status, decimal Budget, DateOnly Due);

    private static readonly CultureInfo s_en = CultureInfo.GetCultureInfo("en-US");

    private static readonly Project[] s_projects =
    [
        new("Atlas redesign", "Mina Park", "Active", 42000, new DateOnly(2026, 11, 14)),
        new("Billing migration", "Theo Walsh", "Review", 18500, new DateOnly(2026, 10, 30)),
        new("Checkout v3", "Ana Ruiz", "Active", 126000, new DateOnly(2027, 1, 9)),
        new("Docs search", "Kai Moreno", "Paused", 9600, new DateOnly(2026, 12, 2)),
        new("Edge caching", "Lee Chen", "Done", 31250, new DateOnly(2026, 9, 18)),
        new("Field reports", "Ivy Brooks", "Active", 57800, new DateOnly(2026, 11, 28))
    ];

    public TableSortedFromCodeDemo()
    {
        InitializeComponent();
        Table.ItemsSource = s_projects;
        Table.Columns.Add(DataTableColumn.Create<Project>("Project", p => p.Name));
        Table.Columns.Add(new DataTableColumn("Budget", p => ((Project)p).Budget) { Key = "Budget", Formatter = Money });
        Table.Columns.Add(new DataTableColumn("Owner", p => ((Project)p).Owner) { IsSortable = false });
    }

    private static string Money(object? value) => value is decimal amount ? amount.ToString("C0", s_en) : "–";
}
