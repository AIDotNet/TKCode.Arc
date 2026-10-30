using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A table, view or column of the rich rows demo's schema tree.</summary>
public sealed class TreeViewSchemaNode
{
    private TreeViewSchemaNode(string name, string path, string detail, bool isColumn)
    {
        Name = name;
        Path = path;
        Detail = detail;
        IsColumn = isColumn;
    }

    public string Name { get; }

    /// <summary>Full name shown in the tooltip, such as <c>public.orders.total</c>.</summary>
    public string Path { get; }

    /// <summary>The column type, or the column count of a table.</summary>
    public string Detail { get; }

    public bool IsColumn { get; }

    /// <summary>Generated columns are shown but cannot be picked.</summary>
    public bool IsGenerated { get; private init; }

    public bool IsIndexed { get; private init; }

    public bool IsView { get; private init; }

    public IReadOnlyList<TreeViewSchemaNode>? Columns { get; private init; }

    public static TreeViewSchemaNode Table(string name, bool view, params (string Name, string Type, bool Generated, bool Indexed)[] columns) =>
        new(name, $"public.{name}", columns.Length == 1 ? "1 column" : $"{columns.Length} columns", false)
        {
            IsView = view,
            Columns = columns.Select(c => new TreeViewSchemaNode(c.Name, $"public.{name}.{c.Name}", c.Type, true)
            {
                IsGenerated = c.Generated,
                IsIndexed = c.Indexed
            }).ToList()
        };

    public override string ToString() => Path;
}

public partial class TreeViewRichRowsDemo : UserControl
{
    private readonly List<TreeViewSchemaNode> _tables =
    [
        TreeViewSchemaNode.Table("orders", false,
            ("id", "uuid", true, true),
            ("customer_id", "uuid", false, true),
            ("total", "numeric(10,2)", false, false),
            ("created_at", "timestamptz", true, false)),
        TreeViewSchemaNode.Table("customers", false,
            ("id", "uuid", true, true),
            ("email", "text", false, true),
            ("name", "text", false, false)),
        TreeViewSchemaNode.Table("monthly_revenue", true,
            ("month", "date", false, false),
            ("revenue", "numeric", false, false))
    ];

    public TreeViewRichRowsDemo()
    {
        InitializeComponent();
        Tree.ItemsSource = _tables;
        Tree.ChildrenSelector = item => ((TreeViewSchemaNode)item).Columns;
        // Type-ahead and screen readers use the header; the template draws the rest of the row.
        Tree.HeaderSelector = item => ((TreeViewSchemaNode)item).Name;
        Tree.IconSelector = item => item is TreeViewSchemaNode { IsColumn: true } column
            ? column.IsGenerated ? ArcIcons.Lock : ArcIcons.Columns2
            : ((TreeViewSchemaNode)item).IsView ? ArcIcons.Eye : ArcIcons.Table;
        Tree.DisabledSelector = item => ((TreeViewSchemaNode)item).IsGenerated;
        Tree.Expand(_tables[0]);
    }

    private void OnSelectionChanged(object? sender, TreeViewSelectionChangedEventArgs e) =>
        Selected.Text = $"SelectedItem: {e.NewItem}";
}
