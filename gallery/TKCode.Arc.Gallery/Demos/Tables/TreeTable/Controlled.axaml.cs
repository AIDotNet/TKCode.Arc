using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TreeTableControlledDemo : UserControl
{
    private sealed record Team(string Id, string Name, int Cost, IReadOnlyList<Team>? Children = null);

    private static readonly Team[] s_teams =
    [
        new("design", "Design", 30, [new("brand", "Brand", 10), new("product-design", "Product design", 20)]),
        new("eng", "Engineering", 90, [new("backend", "Backend", 60), new("apps", "Apps", 30)]),
        new("ops", "Operations", 15)
    ];

    public TreeTableControlledDemo()
    {
        InitializeComponent();
        Table.KeySelector = item => ((Team)item).Id;
        Table.ChildrenSelector = item => ((Team)item).Children;
        Table.Columns.Add(TreeTableColumn.Create<Team>("Team", t => t.Name));
        Table.Columns.Add(new TreeTableColumn { Header = "Cost", ValueSelector = t => ((Team)t).Cost, Width = new GridLength(96), IsEndAligned = true, StringFormat = "${0}k" });
        Table.ItemsSource = s_teams;
        Table.RowExpandedChanged += (_, _) => Report();
        Table.SelectionChanged += (_, _) => Report();
        Table.SortChanged += (_, _) => Report();
        Report();
    }

    private void Report() =>
        State.Text = $"Expanded: {Format(Table.ExpandedKeys)}  ·  Selected: {Format(Table.SelectedKeys)}  ·  Sort: {Table.SortKey ?? "none"} {Table.SortDirection}";

    private static string Format(IReadOnlyList<object>? keys) => keys is { Count: > 0 } ? string.Join(", ", keys) : "none";

    private void OnExpandAll(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Table.ExpandAll();

    private void OnCollapseAll(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Table.CollapseAll();

    private void OnSortCost(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Table.SortKey = "Cost";
        Table.SortDirection = Table.SortDirection == DataTableSortDirection.Ascending ? DataTableSortDirection.Descending : DataTableSortDirection.Ascending;
        Report();
    }

    private void OnSelectEngineering(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        Table.SelectedKeys = new object[] { "eng", "backend", "apps" };
}
