using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Pages;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using AtomWindow = AtomUI.Desktop.Controls.Window;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// Gallery shell, laid out like uiarc.dev/components: a top bar, a component sidebar (22 groups with counts)
/// and the selected component's page.
/// </summary>
public sealed class GalleryWindow : AtomWindow
{
    private const double SidebarWidth = 280;

    private readonly GalleryTheme _theme = new();
    private readonly ContentControl _pageHost = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly GallerySidebar _sidebar = new();

    public GalleryWindow()
    {
        Title = "TKCode.Arc Gallery";
        Width = 1280;
        Height = 860;
        MinWidth = 900;
        MinHeight = 600;

        var topBar = TopBar = new GalleryTopBar(_theme, this, _sidebar.FocusSearch);
        Grid.SetRow(topBar, 0);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions($"{SidebarWidth},*") };
        _sidebar.EntrySelected += entry => _pageHost.Content = GalleryPages.Create(entry);
        Grid.SetColumn(_pageHost, 1);
        body.Children.Add(_sidebar);
        body.Children.Add(_pageHost);
        Grid.SetRow(body, 1);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        root.Children.Add(topBar);
        root.Children.Add(body);
        ArcTokens.Bind(root, Panel.BackgroundProperty, ArcDesignTokenKind.Background);
        Content = root;
        _sidebar.Select(GalleryCatalog.Categories[0].Entries[0]);
    }

    public GalleryTheme Appearance => _theme;

    public GallerySidebar Sidebar => _sidebar;

    public GalleryTopBar TopBar { get; }

    /// <summary>Opens <paramref name="entry"/>'s page and reveals it in the sidebar.</summary>
    public void Select(GalleryEntry entry) => _sidebar.Select(entry);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.K && (e.KeyModifiers.HasFlag(KeyModifiers.Meta) || e.KeyModifiers.HasFlag(KeyModifiers.Control)))
        {
            _sidebar.FocusSearch();
            e.Handled = true;
        }
    }
}
