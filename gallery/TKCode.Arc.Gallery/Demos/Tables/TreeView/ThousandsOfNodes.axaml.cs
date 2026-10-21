using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class TreeViewThousandsOfNodesDemo : UserControl
{
    private sealed record FileNode(string Name, IReadOnlyList<FileNode>? Children = null)
    {
        public override string ToString() => Name;
    }

    public TreeViewThousandsOfNodesDemo()
    {
        InitializeComponent();
        // 40 folders with 250 files each: rows are virtualized, so opening a folder costs one screenful.
        Tree.ItemsSource = Enumerable.Range(1, 40).Select(f => new FileNode($"package-{f:00}",
            Enumerable.Range(1, 250).Select(i => new FileNode($"module-{f:00}-{i:000}.ts")).ToList())).ToList();
        Tree.ChildrenSelector = item => ((FileNode)item).Children;
    }

    private void OnExpandAllClick(object? sender, RoutedEventArgs e) => Tree.ExpandAll();

    private void OnCollapseAllClick(object? sender, RoutedEventArgs e) => Tree.CollapseAll();
}
