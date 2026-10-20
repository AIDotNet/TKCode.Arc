using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.DesignTokens;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Pages;

public static class TreeViewPage
{
    private sealed record FileNode(string Name, IReadOnlyList<FileNode>? Children = null)
    {
        public override string ToString() => Name;
    }

    private static readonly Dictionary<string, (string Language, string Code)> s_sources = new()
    {
        ["button.tsx"] = ("tsx", "export function Button({ children }: { children: React.ReactNode }) {\n  return <button className=\"button\">{children}</button>;\n}"),
        ["button.module.css"] = ("css", ".button {\n  height: 44px;\n  border-radius: 18px;\n  padding-inline: 16px;\n}"),
        ["tree-view.tsx"] = ("tsx", "export function TreeView({ nodes }: TreeViewProps) {\n  const [expanded, setExpanded] = useState(new Set<string>());\n  return <div role=\"tree\">{nodes.map(render)}</div>;\n}"),
        ["README.md"] = ("md", "# Arc\n\nComponents with legible hierarchy and calm motion."),
        ["package.json"] = ("json", "{\n  \"name\": \"arc\",\n  \"version\": \"1.0.0\"\n}"),
        ["motion-tokens.ts"] = ("ts", "export const motionTokens = {\n  duration: { fast: 0.16, standard: 0.24 },\n};")
    };

    private static TreeViewItem Folder(string name, bool open, params TreeViewItem[] children)
    {
        var folder = new TreeViewItem { Header = name, IsExpanded = open };
        folder.Items.AddRange(children);
        return folder;
    }

    private static TreeViewItem File(string name) => new() { Header = name };

    public static Control Create()
    {
        // Inline nodes, as XAML would declare them.
        var tree = new TreeView { MinWidth = 260 };
        tree.Items.AddRange(new[]
        {
            Folder("src", true,
                Folder("components", true,
                    Folder("button", false, File("button.tsx"), File("button.module.css")),
                    Folder("tree-view", false, File("tree-view.tsx"), File("tree-view.module.css"))),
                Folder("lib", false, File("motion-tokens.ts"), File("utils.ts"))),
            Folder("docs", false, File("getting-started.md"), File("theming.md")),
            File("package.json"),
            File("README.md")
        });
        var viewer = new CodeBlock { Code = "// Select a file", FileName = "No file selected", Language = "ts" };
        var status = GalleryUi.Text("Click or press Enter on a file.", ArcDesignTokenKind.TextSecondary);
        tree.ItemInvoked += (_, e) =>
        {
            var name = ((TreeViewItem)e.Item).Header ?? string.Empty;
            status.Text = $"Invoked {name}";
            if (s_sources.TryGetValue(name, out var source))
            {
                viewer.FileName = name;
                viewer.Language = source.Language;
                viewer.Code = source.Code;
            }
        };
        var pair = new Grid { ColumnDefinitions = new ColumnDefinitions("280,*"), ColumnSpacing = 16 };
        pair.Children.Add(tree);
        Grid.SetColumn(viewer, 1);
        viewer.VerticalAlignment = VerticalAlignment.Top;
        pair.Children.Add(viewer);

        // Data nodes with a children selector.
        var data = new List<FileNode>
        {
            new("app", [new("layout.tsx"), new("page.tsx"), new("globals.css")]),
            new("public", [new("logo.svg"), new("favicon.ico")]),
            new("next.config.ts"),
            new("tsconfig.json")
        };
        var dataTree = new TreeView
        {
            ItemsSource = data,
            ChildrenSelector = item => ((FileNode)item).Children,
            HeaderSelector = item => ((FileNode)item).Name,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 300
        };
        var selected = GalleryUi.Text("SelectedItem: (none)", ArcDesignTokenKind.TextSecondary);
        dataTree.SelectionChanged += (_, e) => selected.Text = $"SelectedItem: {e.NewItem}";
        var expandAll = new Button { Content = "Expand all", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        expandAll.Click += (_, _) => dataTree.ExpandAll();
        var collapseAll = new Button { Content = "Collapse all", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        collapseAll.Click += (_, _) => dataTree.CollapseAll();
        var reveal = new Button { Content = "Select globals.css", Variant = ButtonVariant.Ghost, Size = ControlSize.Small };
        reveal.Click += (_, _) => dataTree.SelectedItem = data[0].Children![2];

        // Thousands of nodes: rows are virtualized, so opening a folder costs one screenful.
        var big = Enumerable.Range(1, 40).Select(f => new FileNode($"package-{f:00}",
            Enumerable.Range(1, 250).Select(i => new FileNode($"module-{f:00}-{i:000}.ts")).ToList())).ToList();
        var bigTree = new TreeView
        {
            ItemsSource = big,
            ChildrenSelector = item => ((FileNode)item).Children,
            MaxHeight = 380,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 340
        };
        var bigExpand = new Button { Content = "Open all 10,000 files", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        bigExpand.Click += (_, _) => bigTree.ExpandAll();
        var bigCollapse = new Button { Content = "Collapse all", Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
        bigCollapse.Click += (_, _) => bigTree.CollapseAll();

        return GalleryUi.Column(20,
            GalleryUi.Card("File tree", "Folders open with their space growing on a spring while new rows stagger in; the selection glides between rows. Arrows, Home/End, Enter/Space and typing a name all work.",
                GalleryUi.Column(12, pair, status)),
            GalleryUi.Card("Data with a children selector", "ItemsSource plus ChildrenSelector; SelectedItem is two-way and selecting a hidden node opens its ancestors.",
                GalleryUi.Column(12, GalleryUi.Row(expandAll, collapseAll, reveal), dataTree, selected)),
            GalleryUi.Card("Thousands of nodes", "40 folders with 250 files each. Rows are virtualized and pooled; only rows near the viewport animate.",
                GalleryUi.Column(12, GalleryUi.Row(bigExpand, bigCollapse), bigTree)));
    }
}
