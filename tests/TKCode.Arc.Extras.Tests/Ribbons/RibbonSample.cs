using Avalonia.Controls;

namespace TKCode.Arc.Tests.Ribbons;

/// <summary>A modeling-app ribbon for the tests: Home (Clipboard, Font, Insert, Arrange) and View (Zoom), with a contextual tab.</summary>
internal sealed class RibbonSample
{
    public RibbonSample()
    {
        Paste = new RibbonSplitButton { Label = "Paste", Icon = ArcIcons.ClipboardPaste, QuickAccessKey = "paste" };
        PasteSpecial = new MenuEntry { Header = "Paste special" };
        Paste.Items.Add(PasteSpecial);
        Cut = new RibbonButton { Label = "Cut", Icon = ArcIcons.Copy, Size = RibbonItemSize.Medium, QuickAccessKey = "cut", Shortcut = "Ctrl+X" };
        Copy = new RibbonButton { Label = "Copy", Icon = ArcIcons.Copy, Size = RibbonItemSize.Medium, QuickAccessKey = "copy" };
        Painter = new RibbonButton { Label = "Format painter", Icon = ArcIcons.Settings, Size = RibbonItemSize.Medium };
        Clipboard = Group("Clipboard", Paste, Cut, Copy, Painter);

        Bold = new RibbonToggleButton { Label = "Bold", Icon = ArcIcons.Bold, Size = RibbonItemSize.Small, QuickAccessKey = "bold" };
        Italic = new RibbonToggleButton { Label = "Italic", Icon = ArcIcons.Italic, Size = RibbonItemSize.Small };
        Font = Group("Font", Bold, Italic, new RibbonSeparator(),
            new RibbonButton { Label = "Grow font", Icon = ArcIcons.ArrowUp, Size = RibbonItemSize.Medium },
            new RibbonButton { Label = "Shrink font", Icon = ArcIcons.ArrowDown, Size = RibbonItemSize.Medium });
        Font.IsLauncherVisible = true;

        Node = new RibbonButton { Label = "Node", Icon = ArcIcons.Plus, QuickAccessKey = "node" };
        Insert = Group("Insert", Node,
            new RibbonButton { Label = "Connector", Icon = ArcIcons.ArrowRight },
            new RibbonButton { Label = "Comment", Icon = ArcIcons.Pin });

        Align = new RibbonDropDownButton { Label = "Align", Icon = ArcIcons.ArrowLeft, QuickAccessKey = "align" };
        AlignLeft = new MenuEntry { Header = "Align left" };
        Align.Items.Add(AlignLeft);
        Align.Items.Add(new MenuEntry { Header = "Align right" });
        Arrange = Group("Arrange", Align,
            new RibbonButton { Label = "Group", Icon = ArcIcons.Plus, Size = RibbonItemSize.Medium },
            new RibbonButton { Label = "Ungroup", Icon = ArcIcons.Trash2, Size = RibbonItemSize.Medium });

        Home = new RibbonTab { Header = "Home" };
        Home.Groups.Add(Clipboard);
        Home.Groups.Add(Font);
        Home.Groups.Add(Insert);
        Home.Groups.Add(Arrange);

        ZoomIn = new RibbonButton { Label = "Zoom in", Icon = ArcIcons.Plus, Size = RibbonItemSize.Medium };
        View = new RibbonTab { Header = "View" };
        View.Groups.Add(Group("Zoom", ZoomIn, new RibbonButton { Label = "Zoom out", Icon = ArcIcons.ArrowDown, Size = RibbonItemSize.Medium }));

        Tools = new RibbonTab { Header = "Node tools", IsContextual = true, IsVisible = false };
        Tools.Groups.Add(Group("Style", new RibbonButton { Label = "Fill", Icon = ArcIcons.Settings }));

        Ribbon = new TKCode.Arc.Ribbon();
        Ribbon.Tabs.Add(Home);
        Ribbon.Tabs.Add(View);
        Ribbon.Tabs.Add(Tools);
    }

    public TKCode.Arc.Ribbon Ribbon { get; }
    public RibbonTab Home { get; }
    public RibbonTab View { get; }
    public RibbonTab Tools { get; }
    public RibbonGroup Clipboard { get; }
    public RibbonGroup Font { get; }
    public RibbonGroup Insert { get; }
    public RibbonGroup Arrange { get; }
    public RibbonSplitButton Paste { get; }
    public MenuEntry PasteSpecial { get; }
    public RibbonButton Cut { get; }
    public RibbonButton Copy { get; }
    public RibbonButton Painter { get; }
    public RibbonToggleButton Bold { get; }
    public RibbonToggleButton Italic { get; }
    public RibbonButton Node { get; }
    public RibbonDropDownButton Align { get; }
    public MenuEntry AlignLeft { get; }
    public RibbonButton ZoomIn { get; }

    public IEnumerable<RibbonGroup> HomeGroups => Home.Groups;

    private static RibbonGroup Group(string header, params Control[] items)
    {
        var group = new RibbonGroup { Header = header };
        foreach (var item in items)
        {
            group.Items.Add(item);
        }
        return group;
    }
}
