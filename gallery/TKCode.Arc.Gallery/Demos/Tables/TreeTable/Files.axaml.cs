using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TreeTableFilesDemo : UserControl
{
    private sealed record Entry(string Path, string Name, double SizeMb, string Modified, IReadOnlyList<Entry>? Children = null);

    private static readonly Entry[] s_files =
    [
        new("design", "design", 148.2, "Oct 22",
        [
            new("design/brand", "brand", 62.4, "Oct 20",
            [
                new("design/brand/logo.svg", "logo.svg", 0.2, "Oct 20"),
                new("design/brand/palette.fig", "palette.fig", 62.2, "Oct 18"),
            ]),
            new("design/specs.pdf", "specs.pdf", 85.8, "Oct 22"),
        ]),
        new("src", "src", 12.6, "Oct 24",
        [
            new("src/components", "components", 7.4, "Oct 24",
            [
                new("src/components/button.cs", "button.cs", 3.1, "Oct 24"),
                new("src/components/tree-table.cs", "tree-table.cs", 4.3, "Oct 23"),
            ]),
            new("src/index.cs", "index.cs", 5.2, "Oct 21"),
        ]),
        new("README.md", "README.md", 0.01, "Oct 1"),
        new("LICENSE", "LICENSE", 0.02, "Sep 2"),
    ];

    public TreeTableFilesDemo()
    {
        InitializeComponent();
        Table.KeySelector = item => ((Entry)item).Path;
        Table.ChildrenSelector = item => ((Entry)item).Children;
        Table.Columns.Add(TreeTableColumn.Create<Entry>("Name", f => f.Name));
        Table.Columns.Add(new TreeTableColumn
        {
            Header = "Size", ValueSelector = f => ((Entry)f).SizeMb, Width = new GridLength(96), IsEndAligned = true, StringFormat = "{0:0.##} MB"
        });
        Table.Columns.Add(new TreeTableColumn { Header = "Modified", ValueSelector = f => ((Entry)f).Modified, Width = new GridLength(112), HideBelow = 480, IsSortable = false });
        Table.ItemsSource = s_files;
        Table.ExpandedKeys = new object[] { "src" };
        Table.SortKey = "Size";
        Table.SortDirection = DataTableSortDirection.Descending;
    }

    private void OnRowActivated(object? sender, TreeTableRowEventArgs e) => Status.Text = $"Opened {((Entry)e.Item).Path}";
}
