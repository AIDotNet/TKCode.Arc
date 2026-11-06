using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class TreeTableStripedDemo : UserControl
{
    private sealed record Entry(string Path, string Name, double SizeMb, IReadOnlyList<Entry>? Children = null);

    private static readonly Entry[] s_files =
    [
        new("design", "design", 148.2,
        [
            new("design/brand.fig", "brand.fig", 62.4),
            new("design/specs.pdf", "specs.pdf", 85.8),
        ]),
        new("src", "src", 12.6,
        [
            new("src/button.cs", "button.cs", 3.1),
            new("src/index.cs", "index.cs", 5.2),
        ]),
        new("README.md", "README.md", 0.01),
        new("LICENSE", "LICENSE", 0.02),
    ];

    public TreeTableStripedDemo()
    {
        InitializeComponent();
        Table.KeySelector = item => ((Entry)item).Path;
        Table.ChildrenSelector = item => ((Entry)item).Children;
        Table.Columns.Add(TreeTableColumn.Create<Entry>("Name", f => f.Name));
        Table.Columns.Add(new TreeTableColumn
        {
            Header = "Size", ValueSelector = f => ((Entry)f).SizeMb, Width = new GridLength(96), IsEndAligned = true, StringFormat = "{0:0.##} MB"
        });
        Table.ItemsSource = s_files;
        Table.ExpandedKeys = new object[] { "design", "src" };
    }
}
