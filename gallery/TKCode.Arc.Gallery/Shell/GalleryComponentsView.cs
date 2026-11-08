using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The overview of the library (<c>/components</c>): the components, blocks, effects and mobile controls, one tab each, with
/// a filter. The list of a tab is built when it is first shown.
/// </summary>
internal sealed class GalleryComponentsView : UserControl
{
    private readonly GalleryAtlas[] _atlases;
    private readonly SearchField _filter = new();
    private readonly TextBlock _detailTitle = GalleryUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 14, FontWeight.Medium);
    private readonly TextBlock _detailText = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 13);

    public GalleryComponentsView(GalleryShell shell)
    {
        var tabs = new GallerySegmented(GalleryShellLangResourceKind.Components, GalleryShellLangResourceKind.Category_Blocks,
                                        GalleryShellLangResourceKind.ProductSpecial, GalleryShellLangResourceKind.ProductMobile)
        {
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _atlases =
        [
            new GalleryAtlas(shell, GalleryCatalog.AllCategories.ToList(), Describe),
            new GalleryAtlas(shell, GalleryCatalog.Blocks.Subcategories ?? [], Describe),
            new GalleryAtlas(shell, GalleryCatalog.Special, Describe),
            new GalleryAtlas(shell, [GalleryCatalog.Mobile], Describe)
        ];
        var lists = new Panel();
        foreach (var atlas in _atlases)
        {
            atlas.IsVisible = false;
            lists.Children.Add(atlas);
        }
        tabs.SelectionChanged += Show;
        tabs.SelectedIndex = 0;
        Tabs = tabs;
        Show(0);

        GalleryText.Bind(_filter, TextBox.PlaceholderTextProperty, GalleryShellLangResourceKind.HomeAtlasFilter);
        GalleryText.Bind(_filter, AutomationProperties.NameProperty, GalleryShellLangResourceKind.HomeAtlasFilter);
        _filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                foreach (var atlas in _atlases)
                {
                    atlas.Filter(_filter.Text);
                }
            }
        };

        var controls = new GalleryTwoPane { EndWidth = 280, StackBelow = 640, Spacing = 12 };
        controls.Children.Add(tabs);
        controls.Children.Add(_filter);

        var detailStack = new StackPanel { Spacing = 2 };
        detailStack.Children.Add(_detailTitle);
        detailStack.Children.Add(_detailText);
        var detail = new Border { Padding = new Thickness(14, 10), MinHeight = 62, Child = detailStack };
        ArcTokens.Bind(detail, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(detail, Border.CornerRadiusProperty, ArcDesignTokenKind.RadiusControl);

        var body = new StackPanel { Spacing = 20 };
        body.Children.Add(GallerySite.Heading(null, GallerySite.Reveal(GalleryShellLangResourceKind.Components, TextLevel.H1), GalleryShellLangResourceKind.ComponentsText));
        body.Children.Add(controls);
        body.Children.Add(detail);
        body.Children.Add(lists);

        var column = new StackPanel();
        column.Children.Add(body);
        column.Children.Add(GallerySiteFooter.Create(shell));
        // A touch screen has no pointing: a tap opens the page, so the line that describes the pointed-at tile goes.
        column.SizeChanged += (_, e) => detail.IsVisible = e.NewSize.Width >= GallerySite.PhoneWidth;
        Content = GallerySite.Page(column, out _);
        Describe(GalleryCatalog.Categories[0].Entries[0]);
    }

    /// <summary>The tabs: components, blocks, effects and mobile.</summary>
    public GallerySegmented Tabs { get; }

    /// <summary>The filter box.</summary>
    public SearchField FilterBox => _filter;

    /// <summary>The list of the tab that is showing.</summary>
    public GalleryAtlas Current => _atlases.First(atlas => atlas.IsVisible);

    /// <summary>Shows a tab: 0 components, 1 blocks, 2 effects, 3 mobile.</summary>
    internal void Show(int index)
    {
        for (var i = 0; i < _atlases.Length; i++)
        {
            _atlases[i].IsVisible = i == index;
        }
    }

    private void Describe(GalleryEntry entry)
    {
        _detailTitle.Text = entry.Title;
        _detailText.Text = GalleryDocs.Summary(entry.ClassName, GalleryText.CurrentLanguage) ?? entry.Description;
    }
}
