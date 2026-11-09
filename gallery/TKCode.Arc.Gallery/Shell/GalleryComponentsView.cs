using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The overview of the library (<c>/components</c>): the components, blocks, effects and mobile controls, one tab each, with
/// a filter. Every tab shows its size, and while a filter is typed, how many of its entries match, so a search that finds
/// nothing in one tab points at the tab that has it. The list of a tab is built when it is first shown; a pointed-at tile
/// describes itself in a tooltip (<see cref="GalleryAtlas"/>).
/// </summary>
internal sealed class GalleryComponentsView : UserControl
{
    private readonly GalleryAtlas[] _atlases;
    private readonly TextBlock[] _counts;
    private readonly SearchField _filter = new();

    public GalleryComponentsView(GalleryShell shell)
    {
        _atlases =
        [
            new GalleryAtlas(shell, GalleryCatalog.AllCategories.ToList()),
            new GalleryAtlas(shell, GalleryCatalog.Blocks.Subcategories ?? []),
            new GalleryAtlas(shell, GalleryCatalog.Special),
            new GalleryAtlas(shell, [GalleryCatalog.Mobile])
        ];
        GalleryShellLangResourceKind[] labels =
        [
            GalleryShellLangResourceKind.Components, GalleryShellLangResourceKind.Category_Blocks,
            GalleryShellLangResourceKind.ProductSpecial, GalleryShellLangResourceKind.ProductMobile
        ];
        _counts = new TextBlock[labels.Length];
        var contents = new Control[labels.Length];
        for (var i = 0; i < labels.Length; i++)
        {
            contents[i] = TabLabel(labels[i], out _counts[i]);
        }
        var tabs = new GallerySegmented(contents) { HorizontalAlignment = HorizontalAlignment.Left };
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
                Apply(_filter.Text);
            }
        };

        var controls = new GalleryTwoPane { EndWidth = 320, StackBelow = 640, Spacing = 12 };
        controls.Children.Add(tabs);
        controls.Children.Add(_filter);
        // A phone has room for the four names, not for their counts.
        controls.SizeChanged += (_, e) =>
        {
            var phone = e.NewSize.Width < GallerySite.PhoneWidth;
            foreach (var count in _counts)
            {
                count.IsVisible = !phone;
            }
            foreach (var segment in tabs.Segments)
            {
                segment.Padding = phone ? new Thickness(8, 0) : new Thickness(12, 0);
            }
        };

        var body = new StackPanel { Spacing = 24 };
        body.Children.Add(GallerySite.Heading(null, GallerySite.Reveal(GalleryShellLangResourceKind.Components, TextLevel.H1), GalleryShellLangResourceKind.ComponentsText));
        body.Children.Add(controls);
        body.Children.Add(lists);

        var column = new StackPanel();
        column.Children.Add(body);
        column.Children.Add(GallerySiteFooter.Create(shell));
        Content = GallerySite.Page(column, out _);
        SyncCounts(string.Empty);
    }

    /// <summary>The tabs: components, blocks, effects and mobile.</summary>
    public GallerySegmented Tabs { get; }

    /// <summary>The filter box.</summary>
    public SearchField FilterBox => _filter;

    /// <summary>The list of the tab that is showing.</summary>
    public GalleryAtlas Current => _atlases.First(atlas => atlas.IsVisible);

    /// <summary>The count shown on each tab, in tab order.</summary>
    internal IReadOnlyList<TextBlock> Counts => _counts;

    /// <summary>Shows a tab: 0 components, 1 blocks, 2 effects, 3 mobile.</summary>
    internal void Show(int index)
    {
        for (var i = 0; i < _atlases.Length; i++)
        {
            _atlases[i].IsVisible = i == index;
        }
    }

    /// <summary>A tab's content: its name, which follows the language, and a muted count after it.</summary>
    private static Control TabLabel(GalleryShellLangResourceKind kind, out TextBlock count)
    {
        var name = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        GalleryText.Bind(name, TextBlock.TextProperty, kind);
        count = GalleryUi.Mono(string.Empty, ArcDesignTokenKind.TextMuted, 12);
        count.VerticalAlignment = VerticalAlignment.Center;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        row.Children.Add(name);
        row.Children.Add(count);
        return row;
    }

    private void Apply(string? query)
    {
        foreach (var atlas in _atlases)
        {
            atlas.Filter(query);
        }
        SyncCounts(query);
    }

    /// <summary>Every tab's size, or while a filter is typed, how many of its entries match.</summary>
    private void SyncCounts(string? query)
    {
        for (var i = 0; i < _atlases.Length; i++)
        {
            _counts[i].Text = _atlases[i].CountMatches(query).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
