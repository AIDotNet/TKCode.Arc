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
/// Every entry of some catalog groups on one page, by group. It reads the catalog, so it grows with the library. A tile is a
/// bordered text, not a full Arc button: there can be over a hundred of them, and in the browser that difference is visible.
/// The tiles are built when the list first comes into view.
/// </summary>
internal sealed class GalleryAtlas : WaterfallPanel
{
    private readonly GalleryShell _shell;
    private readonly IReadOnlyList<GalleryCategory> _categories;
    private readonly Action<GalleryEntry> _describe;
    private readonly List<(Control Group, List<Tile> Tiles)> _built = new();
    private string _filter = string.Empty;

    public GalleryAtlas(GalleryShell shell, IReadOnlyList<GalleryCategory> categories, Action<GalleryEntry> describe)
    {
        _shell = shell;
        _categories = categories;
        _describe = describe;
        MinLaneSize = 230;
        Spacing = 22;
        MinHeight = 200;
        _ = new GalleryFirstSight(this, Build);
    }

    /// <summary>The tiles that are showing, in order (empty until the list has been built).</summary>
    public IEnumerable<GalleryEntry> VisibleEntries => _built.SelectMany(group => group.Tiles).Where(tile => tile.IsVisible).Select(tile => tile.Entry);

    /// <summary>Builds the tiles if they have not been built yet.</summary>
    public void Build()
    {
        if (_built.Count > 0)
        {
            return;
        }
        foreach (var category in _categories)
        {
            var entries = category.AllEntries.ToList();
            if (entries.Count == 0)
            {
                continue;
            }
            var name = GalleryUi.Text(category.Title, ArcDesignTokenKind.Foreground, 13, FontWeight.Medium);
            if (GalleryText.TryGetCategory(category.Code, out var kind) || GalleryText.TryGetBlockGroup(category.Code, out kind))
            {
                GalleryText.Bind(name, TextBlock.TextProperty, kind);
            }
            var count = GalleryUi.Mono(entries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), ArcDesignTokenKind.TextMuted, 12);
            var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            Grid.SetColumn(count, 1);
            head.Children.Add(name);
            head.Children.Add(count);

            var wrap = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 4, LineSpacing = 4 };
            var tiles = new List<Tile>();
            foreach (var entry in entries)
            {
                var tile = new Tile(entry, () => _shell.Navigate(GalleryRoute.To(entry)), _describe);
                tiles.Add(tile);
                wrap.Children.Add(tile);
            }
            var group = new StackPanel { Spacing = 8 };
            group.Children.Add(head);
            group.Children.Add(wrap);
            Children.Add(group);
            _built.Add((group, tiles));
        }
        Filter(_filter);
    }

    /// <summary>Shows the entries whose name contains <paramref name="query"/>.</summary>
    public void Filter(string? query)
    {
        _filter = query?.Trim() ?? string.Empty;
        foreach (var (group, tiles) in _built)
        {
            var any = false;
            foreach (var tile in tiles)
            {
                tile.IsVisible = _filter.Length == 0
                                 || tile.Entry.Title.Contains(_filter, StringComparison.OrdinalIgnoreCase)
                                 || tile.Entry.ClassName.Contains(_filter, StringComparison.OrdinalIgnoreCase);
                any |= tile.IsVisible;
            }
            group.IsVisible = any;
        }
    }

    /// <summary>One entry: its name, which opens its page by a tap, a click or the keyboard.</summary>
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
