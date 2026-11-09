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
/// Every entry of some catalog groups on one page: one card per group, with the group's name and size at the top and a
/// tile per entry, packed in columns. It reads the catalog, so it grows with the library. A tile is a bordered text, not a
/// full Arc button: there can be over a hundred of them, and in the browser that difference is visible. Pointing at a tile
/// (or tabbing to it) opens its description in a tooltip right there; a tile added recently carries the "New" badge. The
/// cards are built when the list first comes into view. When nothing matches the filter, the list says so instead of going
/// blank.
/// </summary>
internal sealed class GalleryAtlas : WaterfallPanel
{
    private readonly GalleryShell _shell;
    private readonly IReadOnlyList<GalleryCategory> _categories;
    private readonly List<(Control Card, List<Tile> Tiles)> _built = new();
    private readonly TextBlock _empty = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextMuted, 14);
    private string _filter = string.Empty;

    public GalleryAtlas(GalleryShell shell, IReadOnlyList<GalleryCategory> categories)
    {
        _shell = shell;
        _categories = categories;
        // As many lanes of 300 as fit: one on a phone, three on a wide page.
        Columns = 0;
        MinLaneSize = 300;
        Spacing = 16;
        MinHeight = 120;
        _empty.IsVisible = false;
        _empty.Margin = new Thickness(4, 8);
        Children.Add(_empty);
        _ = new GalleryFirstSight(this, Build);
        GalleryText.Track(this, RefreshText);
    }

    /// <summary>How many entries the list holds in all.</summary>
    public int Total => _categories.Sum(category => category.AllEntries.Count());

    /// <summary>The tiles that are showing, in order (empty until the list has been built).</summary>
    public IEnumerable<GalleryEntry> VisibleEntries => _built.SelectMany(group => group.Tiles).Where(tile => tile.IsVisible).Select(tile => tile.Entry);

    /// <summary>How many entries <paramref name="query"/> would keep, whether or not the list has been built.</summary>
    public int CountMatches(string? query)
    {
        var filter = query?.Trim() ?? string.Empty;
        return filter.Length == 0 ? Total : _categories.Sum(category => category.AllEntries.Count(entry => Matches(entry, filter)));
    }

    /// <summary>Builds the cards if they have not been built yet.</summary>
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
            var name = GalleryUi.Text(category.Title, ArcDesignTokenKind.Foreground, 14, FontWeight.Medium);
            name.VerticalAlignment = VerticalAlignment.Center;
            if (GalleryText.TryGetCategory(category.Code, out var kind) || GalleryText.TryGetBlockGroup(category.Code, out kind))
            {
                GalleryText.Bind(name, TextBlock.TextProperty, kind);
            }
            var count = GalleryUi.Mono(entries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), ArcDesignTokenKind.TextMuted, 12);
            count.VerticalAlignment = VerticalAlignment.Center;
            var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
            Grid.SetColumn(count, 1);
            head.Children.Add(name);
            head.Children.Add(count);

            var wrap = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 6, LineSpacing = 6 };
            var tiles = new List<Tile>();
            foreach (var entry in entries)
            {
                var tile = new Tile(entry, () => _shell.Navigate(GalleryRoute.To(entry)));
                tiles.Add(tile);
                wrap.Children.Add(tile);
            }
            var stack = new StackPanel { Spacing = 12 };
            stack.Children.Add(head);
            stack.Children.Add(wrap);
            var card = GallerySite.Panel(stack, padding: 16);
            Children.Add(card);
            _built.Add((card, tiles));
        }
        Filter(_filter);
    }

    /// <summary>Shows the entries whose name contains <paramref name="query"/>; the cards with none left go too.</summary>
    public void Filter(string? query)
    {
        _filter = query?.Trim() ?? string.Empty;
        var shown = 0;
        foreach (var (card, tiles) in _built)
        {
            var any = false;
            foreach (var tile in tiles)
            {
                tile.IsVisible = _filter.Length == 0 || Matches(tile.Entry, _filter);
                any |= tile.IsVisible;
            }
            card.IsVisible = any;
            shown += any ? 1 : 0;
        }
        _empty.Text = GalleryText.Format(GalleryShellLangResourceKind.NoComponentsMatch, _filter);
        _empty.IsVisible = _built.Count > 0 && shown == 0;
    }

    private static bool Matches(GalleryEntry entry, string filter) =>
        entry.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) || entry.ClassName.Contains(filter, StringComparison.OrdinalIgnoreCase);

    /// <summary>The text that follows the language: the tooltips (docs summaries) and the empty line.</summary>
    private void RefreshText()
    {
        foreach (var (_, tiles) in _built)
        {
            foreach (var tile in tiles)
            {
                tile.RefreshTip();
            }
        }
        _empty.Text = GalleryText.Format(GalleryShellLangResourceKind.NoComponentsMatch, _filter);
    }

    /// <summary>
    /// One entry: its name, which opens its page by a tap, a click or the keyboard, and its description in a tooltip when
    /// pointed at or focused.
    /// </summary>
    internal sealed class Tile : Border
    {
        private readonly TextBlock _name;
        private readonly Action _open;
        private IDisposable? _background;
        private IDisposable? _foreground;

        public Tile(GalleryEntry entry, Action open)
        {
            Entry = entry;
            _open = open;
            _name = GalleryUi.Text(entry.Title, ArcDesignTokenKind.TextSecondary, 13);
            _name.TextWrapping = TextWrapping.NoWrap;
            _name.VerticalAlignment = VerticalAlignment.Center;
            if (entry.IsNew)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                row.Children.Add(_name);
                row.Children.Add(new GalleryNewBadge(10));
                Child = row;
            }
            else
            {
                Child = _name;
            }
            Padding = new Thickness(10, 5);
            Focusable = true;
            Cursor = new Cursor(StandardCursorType.Hand);
            AutomationProperties.SetName(this, entry.Title);
            ArcTokens.Bind(this, CornerRadiusProperty, ArcDesignTokenKind.RadiusControl);
            RefreshTip();
            Highlight(false);
            AddHandler(InputElement.TappedEvent, (_, _) => _open());
        }

        public GalleryEntry Entry { get; }

        /// <summary>Reads the description again, in the current language.</summary>
        public void RefreshTip() => Tooltip.SetTip(this, GalleryDocs.Summary(Entry.ClassName, GalleryText.CurrentLanguage) ?? Entry.Description);

        private void Highlight(bool on)
        {
            _background?.Dispose();
            _foreground?.Dispose();
            _background = ArcTokens.Bind(this, BackgroundProperty, on ? ArcDesignTokenKind.AccentStrong : ArcDesignTokenKind.SurfaceMuted);
            _foreground = ArcTokens.Bind(_name, TextBlock.ForegroundProperty, on ? ArcDesignTokenKind.AccentForeground : ArcDesignTokenKind.TextSecondary);
        }

        protected override void OnPointerEntered(PointerEventArgs e)
        {
            base.OnPointerEntered(e);
            Highlight(true);
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
