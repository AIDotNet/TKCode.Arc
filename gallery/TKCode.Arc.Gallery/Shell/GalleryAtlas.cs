using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// Every Arc component on one page, by group, with a filter. It reads the catalog, so it grows with the library. A tile is a
/// bordered text, not a full Arc button: there are over a hundred of them, and in the browser that difference is visible.
/// The tiles are built when the section first comes into view.
/// </summary>
internal sealed class GalleryAtlas : StackPanel
{
    private readonly GalleryShell _shell;
    private readonly WaterfallPanel _groups = new() { MinLaneSize = 230, Spacing = 22 };
    private readonly TextBlock _detailTitle = GalleryUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 14, FontWeight.Medium);
    private readonly TextBlock _detailText = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 13);
    private readonly Border _detail;
    private readonly List<(Control Group, List<Tile> Tiles)> _built = new();

    public GalleryAtlas(GalleryShell shell)
    {
        _shell = shell;
        Spacing = 20;

        var title = new TextReveal { Level = TextLevel.H2 };
        GalleryText.BindFormat(title, TextReveal.TextProperty, GalleryShellLangResourceKind.HomeAtlasTitle, GalleryCatalog.Count);

        var filter = new SearchField();
        GalleryText.Bind(filter, TextBox.PlaceholderTextProperty, GalleryShellLangResourceKind.HomeAtlasFilter);
        GalleryText.Bind(filter, AutomationProperties.NameProperty, GalleryShellLangResourceKind.HomeAtlasFilter);
        filter.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Filter(filter.Text);
            }
        };
        FilterBox = filter;

        var detailStack = new StackPanel { Spacing = 2 };
        detailStack.Children.Add(_detailTitle);
        detailStack.Children.Add(_detailText);
        _detail = new Border { Padding = new Thickness(14, 10), MinHeight = 62, Child = detailStack };
        ArcTokens.Bind(_detail, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(_detail, Border.CornerRadiusProperty, ArcDesignTokenKind.RadiusControl);

        var heading = new GalleryTwoPane { EndWidth = 280, StackBelow = 760, Spacing = 20 };
        heading.Children.Add(GallerySite.Heading(GalleryShellLangResourceKind.HomeAtlasEyebrow, title, GalleryShellLangResourceKind.HomeAtlasSubtitle));
        filter.VerticalAlignment = VerticalAlignment.Bottom;
        heading.Children.Add(filter);

        Children.Add(heading);
        Children.Add(_detail);
        Children.Add(_groups);
        _groups.MinHeight = 400;
        _ = new GalleryFirstSight(_groups, Build);
        // A touch screen has no pointing: a tap opens the page, so the line that describes the pointed-at tile goes.
        SizeChanged += (_, e) => _detail.IsVisible = e.NewSize.Width >= GallerySite.PhoneWidth;
    }

    /// <summary>The filter box.</summary>
    public SearchField FilterBox { get; }

    /// <summary>The tiles that are showing, in order (empty until the section has been built).</summary>
    public IEnumerable<GalleryEntry> VisibleEntries => _built.SelectMany(group => group.Tiles).Where(tile => tile.IsVisible).Select(tile => tile.Entry);

    /// <summary>Builds the tiles if they have not been built yet.</summary>
    public void Build()
    {
        if (_built.Count > 0)
        {
            return;
        }
        foreach (var category in GalleryCatalog.Categories)
        {
            var name = GalleryUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 13, FontWeight.Medium);
            if (GalleryText.TryGetCategory(category.Code, out var kind))
            {
                GalleryText.Bind(name, TextBlock.TextProperty, kind);
            }
            else
            {
                name.Text = category.Title;
            }
            var count = GalleryUi.Mono(category.Entries.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), ArcDesignTokenKind.TextMuted, 12);
            var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Grid.SetColumn(count, 1);
            head.Children.Add(name);
            head.Children.Add(count);

            var wrap = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 4, LineSpacing = 4 };
            var tiles = new List<Tile>();
            foreach (var entry in category.Entries)
            {
                var tile = new Tile(entry, () => _shell.Navigate(GalleryRoute.To(entry)), Describe);
                tiles.Add(tile);
                wrap.Children.Add(tile);
            }
            var group = new StackPanel { Spacing = 8 };
            group.Children.Add(head);
            group.Children.Add(wrap);
            _groups.Children.Add(group);
            _built.Add((group, tiles));
        }
        Describe(GalleryCatalog.Categories[0].Entries[0]);
        Filter(FilterBox.Text);
    }

    private void Describe(GalleryEntry entry)
    {
        _detailTitle.Text = entry.Title;
        _detailText.Text = GalleryDocs.Summary(entry.ClassName, GalleryText.CurrentLanguage) ?? entry.Description;
    }

    private void Filter(string? query)
    {
        var text = query?.Trim() ?? string.Empty;
        foreach (var (group, tiles) in _built)
        {
            var any = false;
            foreach (var tile in tiles)
            {
                tile.IsVisible = text.Length == 0
                                 || tile.Entry.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                                 || tile.Entry.ClassName.Contains(text, StringComparison.OrdinalIgnoreCase);
                any |= tile.IsVisible;
            }
            group.IsVisible = any;
        }
    }

    /// <summary>One component: its name, which opens its page by a tap, a click or the keyboard.</summary>
    internal sealed class Tile : Border
    {
        private readonly TextBlock _name;
        private readonly Action _open;
        private readonly Action<GalleryEntry> _describe;
        private IDisposable? _background;
        private IDisposable? _foreground;

        public Tile(GalleryEntry entry, Action open, Action<GalleryEntry> describe)
        {
            Entry = entry;
            _open = open;
            _describe = describe;
            _name = GalleryUi.Text(entry.Title, ArcDesignTokenKind.TextSecondary, 12);
            _name.TextWrapping = TextWrapping.NoWrap;
            Child = _name;
            Padding = new Thickness(8, 3);
            BorderThickness = new Thickness(1);
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Hand);
            AutomationProperties.SetName(this, entry.Title);
            ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
            ArcTokens.Bind(this, CornerRadiusProperty, ArcDesignTokenKind.RadiusStep1);
            Highlight(false);
            AddHandler(InputElement.TappedEvent, (_, _) => _open());
        }

        public GalleryEntry Entry { get; }

        private void Highlight(bool on)
        {
            _background?.Dispose();
            _foreground?.Dispose();
            _background = ArcTokens.Bind(this, BackgroundProperty, on ? ArcDesignTokenKind.AccentStrong : ArcDesignTokenKind.Surface);
            _foreground = ArcTokens.Bind(_name, TextBlock.ForegroundProperty, on ? ArcDesignTokenKind.AccentForeground : ArcDesignTokenKind.TextSecondary);
        }

        protected override void OnPointerEntered(PointerEventArgs e)
        {
            base.OnPointerEntered(e);
            Highlight(true);
            _describe(Entry);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            Highlight(IsFocused);
        }

        protected override void OnGotFocus(FocusChangedEventArgs e)
        {
            base.OnGotFocus(e);
            Highlight(true);
            _describe(Entry);
        }

        protected override void OnLostFocus(FocusChangedEventArgs e)
        {
            base.OnLostFocus(e);
            Highlight(false);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key is Key.Enter or Key.Space)
            {
                e.Handled = true;
                _open();
            }
        }
    }
}
