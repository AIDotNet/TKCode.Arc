using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TableStripedDemo : UserControl
{
    private sealed record Project(string Name, string Owner, string Status);

    private static readonly Project[] s_projects =
    [
        new("Atlas redesign", "Mina Park", "Active"),
        new("Billing migration", "Theo Walsh", "Review"),
        new("Checkout v3", "Ana Ruiz", "Active"),
        new("Docs search", "Kai Moreno", "Paused"),
        new("Edge caching", "Lee Chen", "Done"),
        new("Field reports", "Ivy Brooks", "Active")
    ];

    public TableStripedDemo()
    {
        InitializeComponent();
        Table.Columns.Add(DataTableColumn.Create<Project>("Project", p => p.Name));
        Table.Columns.Add(DataTableColumn.Create<Project>("Owner", p => p.Owner));
        Table.Columns.Add(DataTableColumn.Create<Project>("Status", p => p.Status));
        Table.ItemsSource = s_projects;
    }
}
