using System.Globalization;
using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TreeTableBudgetDemo : UserControl
{
    private sealed record Spend(string Id, string Name, string Owner, int Resources, decimal Monthly, IReadOnlyList<Spend>? Children = null);

    private static readonly CultureInfo s_en = CultureInfo.GetCultureInfo("en-US");

    private static readonly Spend[] s_spend =
    [
        new("platform", "Platform", "Mira Chen", 38, 18420,
        [
            new("compute", "Compute", "Mira Chen", 22, 11200,
            [
                new("compute-web", "Web servers", "Sam Ortiz", 12, 6300),
                new("compute-jobs", "Batch jobs", "Lee Park", 10, 4900),
            ]),
            new("storage", "Storage", "Noor Ali", 11, 4120),
            new("network", "Network", "Kay Dube", 5, 3100),
        ]),
        new("data", "Data", "Theo Walsh", 24, 12640,
        [
            new("warehouse", "Warehouse", "Theo Walsh", 9, 7800),
            new("streams", "Event streams", "Ana Ruiz", 15, 4840),
        ]),
        new("security", "Security", "Ivy Brooks", 9, 3280),
    ];

    public TreeTableBudgetDemo()
    {
        InitializeComponent();
        Table.KeySelector = item => ((Spend)item).Id;
        Table.RowLabelSelector = item => ((Spend)item).Name;
        Table.ChildrenSelector = item => ((Spend)item).Children;
        Table.Columns.Add(TreeTableColumn.Create<Spend>("Name", s => s.Name));
        Table.Columns.Add(new TreeTableColumn { Header = "Owner", ValueSelector = s => ((Spend)s).Owner, HideBelow = 560 });
        Table.Columns.Add(new TreeTableColumn
        {
            Header = "Resources", ValueSelector = s => ((Spend)s).Resources, Width = new GridLength(104), IsEndAligned = true, HideBelow = 420
        });
        Table.Columns.Add(new TreeTableColumn
        {
            Header = "Monthly", ValueSelector = s => ((Spend)s).Monthly, Width = new GridLength(112), IsEndAligned = true,
            Formatter = value => value is decimal amount ? amount.ToString("C0", s_en) : string.Empty
        });
        Table.ItemsSource = s_spend;
        Table.ExpandedKeys = new object[] { "platform" };
    }

    private void OnSelectionChanged(object? sender, TreeTableSelectionChangedEventArgs e)
    {
        // Parents roll up their children, so only leaves are summed.
        var leaves = e.SelectedItems.Cast<Spend>().Where(s => s.Children is not { Count: > 0 }).ToList();
        Footer.Text = leaves.Count == 0
            ? "Select rows to total their monthly spend."
            : $"{leaves.Count} {(leaves.Count == 1 ? "line" : "lines")} selected · {leaves.Sum(s => s.Monthly).ToString("C0", s_en)} per month";
    }
}
