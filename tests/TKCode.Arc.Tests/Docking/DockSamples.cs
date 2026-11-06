using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Tests.Docking;

/// <summary>The IDE layout of spec section 4.4, built in code, with every pane reachable by name.</summary>
internal sealed class DockSample
{
    public DockSample()
    {
        Explorer = Tool("explorer", "Explorer", DockSide.Left);
        Search = Tool("search", "Search", DockSide.Left);
        Readme = Document("doc:readme", "README.md");
        Terminal = Tool("terminal", "Terminal", DockSide.Bottom);
        ToolGroup = new DockGroup(Explorer, Search) { Size = 22 };
        DocumentGroup = new DockGroup(Readme);
        Documents = new DockDocumentArea { Size = 70, Root = DocumentGroup };
        TerminalGroup = new DockGroup(Terminal) { Size = 30 };
        Right = new DockSplit(Orientation.Vertical, Documents, TerminalGroup) { Size = 78 };
        Root = new DockSplit(Orientation.Horizontal, ToolGroup, Right);
        Workspace = new DockWorkspace { Root = Root };
    }

    public DockPane Explorer { get; }
    public DockPane Search { get; }
    public DockPane Readme { get; }
    public DockPane Terminal { get; }
    public DockGroup ToolGroup { get; }
    public DockGroup DocumentGroup { get; }
    public DockDocumentArea Documents { get; }
    public DockGroup TerminalGroup { get; }
    public DockSplit Right { get; }
    public DockSplit Root { get; }
    public DockWorkspace Workspace { get; }

    public static DockPane Tool(string id, string title, DockSide side)
        => new(id, title, new TextBlock { Text = title }) { PreferredSide = side };

    public static DockPane Document(string id, string title)
        => new(id, title, new TextBlock { Text = title });
}

internal static class DockVisualExtensions
{
    /// <summary>The window a visual is shown in, or null while it is not in a visual tree.</summary>
    public static TopLevel? GetVisualRoot(this Avalonia.Visual visual)
    {
        Avalonia.Visual? node = visual;
        while (node is not null && node is not TopLevel)
        {
            node = Avalonia.VisualTree.VisualExtensions.GetVisualParent(node);
        }
        return node as TopLevel;
    }
}
