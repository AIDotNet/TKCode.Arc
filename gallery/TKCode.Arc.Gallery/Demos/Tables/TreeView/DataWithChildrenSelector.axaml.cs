using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class TreeViewDataWithChildrenSelectorDemo : UserControl
{
    private sealed record FileNode(string Name, IReadOnlyList<FileNode>? Children = null)
    {
        public override string ToString() => Name;
    }

    private readonly List<FileNode> _nodes =
    [
        new("app", [new("layout.tsx"), new("page.tsx"), new("globals.css")]),
        new("public", [new("logo.svg"), new("favicon.ico")]),
        new("next.config.ts"),
        new("tsconfig.json")
    ];

    public TreeViewDataWithChildrenSelectorDemo()
    {
        InitializeComponent();
        Tree.ItemsSource = _nodes;
        Tree.ChildrenSelector = item => ((FileNode)item).Children;
        Tree.HeaderSelector = item => ((FileNode)item).Name;
    }

    private void OnExpandAllClick(object? sender, RoutedEventArgs e) => Tree.ExpandAll();

    private void OnCollapseAllClick(object? sender, RoutedEventArgs e) => Tree.CollapseAll();

    private void OnRevealClick(object? sender, RoutedEventArgs e) => Tree.SelectedItem = _nodes[0].Children![2];

    private void OnSelectionChanged(object? sender, TreeViewSelectionChangedEventArgs e) =>
        Selected.Text = $"SelectedItem: {e.NewItem}";
}
