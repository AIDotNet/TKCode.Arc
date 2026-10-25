using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Gallery.Pages;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AtomTextBox = AtomUI.Desktop.Controls.TextBox;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The uiarc.dev component sidebar: a search field, an All / Ready / Planned filter, 22 collapsible groups with
/// counts whose items hang from a rail, and a progress card. The selected item is marked by a bar that glides
/// along the rail; Up / Down move the selection, and typing in the search field narrows the groups live.
/// </summary>
public sealed class GallerySidebar : Border
{
    private const double RailInset = 15;

    private sealed class Group
    {
        public required GalleryCategory Category { get; init; }
        public required Control Root { get; init; }
        public required GalleryNavRow Header { get; init; }
        public required ArcGlyph Chevron { get; init; }
        public required TextBlock Count { get; init; }
        public required SpringSizeDecorator Items { get; init; }
        public required Border Rail { get; init; }
        public bool UserExpanded { get; set; }
        public bool Visible { get; set; } = true;
        public Group? Parent { get; set; }
        public List<Group> Children { get; } = new();
        public List<Item> Entries { get; } = new();
        /// <summary>Matches under this group: its own entries and those of its subgroups.</summary>
        public int Matches { get; set; }
        /// <summary>No entry anywhere below: a placeholder group, listed while nothing is filtered.</summary>
        public bool IsPlaceholder => Entries.Count == 0 && Children.All(c => c.IsPlaceholder);
        public Section? Section { get; set; }
        public bool IsOpen => Items.IsExpanded && Visible && (Parent?.IsOpen ?? Section?.IsOpen ?? true);
    }

    /// <summary>A top-level sidebar section (Docs, Components, Blocks): a collapsible header over its groups.</summary>
    private sealed class Section
    {
        public required Control Root { get; init; }
        public required GalleryNavRow Header { get; init; }
        public required ArcGlyph Chevron { get; init; }
        public required TextBlock Count { get; init; }
        public required SpringSizeDecorator Body { get; init; }
        public List<Group> Groups { get; } = new();
        public bool Visible { get; set; } = true;
        public bool IsOpen => Body.IsExpanded && Visible;
    }

    private sealed class Item
    {
        public required GalleryEntry Entry { get; init; }
        public required Group Group { get; init; }
        public required GalleryNavRow Row { get; init; }
        public required bool Implemented { get; init; }
        public bool Visible { get; set; } = true;
    }

    private readonly List<Group> _groups = new();
    private readonly List<Item> _items = new();
    private readonly Dictionary<string, Item> _byClass = new();
    private readonly Panel _content = new();
    private readonly GlidingIndicator _indicator = new() { IsHitTestVisible = false, Spring = ArcSpring.Snappy };
    private readonly AtomTextBox _search = new();
    private readonly Border _searchBox = new();
    private readonly GallerySegmented _filter = new(GalleryShellLangResourceKind.FilterAll, GalleryShellLangResourceKind.FilterReady, GalleryShellLangResourceKind.FilterPlanned);
    private readonly List<Section> _sections = new();
    private readonly TextBlock _empty = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextMuted, 13);
    private readonly Control _shortcut;
    private readonly Button _clear;
    private IDisposable? _searchBorder;
    private Item? _selected;
    private Rect _lastRect;

    public event Action<GalleryEntry>? EntrySelected;

    /// <summary>The entry whose page is open.</summary>
    public GalleryEntry? SelectedEntry => _selected?.Entry;

    /// <summary>The entries the search text and filter currently let through, in catalog order.</summary>
    public IReadOnlyList<GalleryEntry> VisibleEntries => _items.Where(i => i.Visible).Select(i => i.Entry).ToList();

    /// <summary>The search field's text; setting it narrows the groups like typing does.</summary>
    public string SearchText
    {
        get => _search.Text ?? string.Empty;
        set => _search.Text = value;
    }

    /// <summary>0 = All, 1 = Ready, 2 = Planned.</summary>
    public int FilterIndex
    {
        get => _filter.SelectedIndex;
        set
        {
            _filter.SelectedIndex = value;
            ApplyFilter();
        }
    }

    public GallerySidebar()
    {
        // The footer counts the whole Components section: Arc's components and the extras. Blocks and the Special
        // components are listed in their own sections and stay out of it (as they do on uiarc.dev).
        var components = GalleryCatalog.ComponentEntries.ToList();
        var implemented = components.Count(e => GalleryPages.IsImplemented(e.ClassName));
        _clear = BuildClearButton();
        _shortcut = BuildShortcutHint();

        var list = new StackPanel { Margin = new Thickness(8, 4, 8, 28) };
        // The sections follow uiarc.dev's sidebar order: Docs, Components, Blocks.
        list.Children.Add(BuildSection(GalleryShellLangResourceKind.Category_Docs, ArcIcons.FileText, [GalleryCatalog.Docs], flatten: true));
        // Arc's groups, then the extras group (both are part of the section, so both count in the footer).
        list.Children.Add(BuildSection(GalleryShellLangResourceKind.Components, ArcIcons.LayoutGrid, GalleryCatalog.AllCategories.ToArray()));
        list.Children.Add(BuildSection(GalleryShellLangResourceKind.Category_Blocks, ArcIcons.Box, GalleryCatalog.Blocks.Subcategories!, isBlockGroups: true));
        // The Special components section (uiarc.dev's fourth section).
        list.Children.Add(BuildSection(GalleryShellLangResourceKind.Category_Special, ArcIcons.Sparkles, GalleryCatalog.Special));
        // The Mobile section: the TKCode.Arc.Mobile components, listed flat (their demos run inside a phone frame).
        list.Children.Add(BuildSection(GalleryShellLangResourceKind.Category_Mobile, ArcIcons.Smartphone, [GalleryCatalog.Mobile], flatten: true));
        _empty.Margin = new Thickness(16, 12);
        _empty.IsVisible = false;
        list.Children.Add(_empty);

        ArcTokens.Bind(_indicator, GlidingIndicator.FillProperty, ArcDesignTokenKind.Foreground);
        _indicator.CornerRadius = new CornerRadius(1);
        _content.Children.Add(list);
        // Drawn over the list so the bar covers the rail line it rides on.
        _content.Children.Add(_indicator);
        _content.LayoutUpdated += (_, _) => SyncIndicator(animate: false);

        var scroll = new ScrollViewer
        {
            Content = _content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            // The list fades out into the footer instead of being cut off.
            OpacityMask = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Colors.Black, 0),
                    new GradientStop(Colors.Black, 0.94),
                    new GradientStop(Colors.Transparent, 1)
                }
            }
        };

        var top = new StackPanel { Spacing = 12, Margin = new Thickness(16, 20, 16, 8) };
        top.Children.Add(BuildSearchBox());
        top.Children.Add(_filter);

        var footer = BuildFooter(implemented, components.Count);
        footer.Margin = new Thickness(16, 8, 16, 16);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        Grid.SetRow(top, 0);
        Grid.SetRow(scroll, 1);
        Grid.SetRow(footer, 2);
        root.Children.Add(top);
        root.Children.Add(scroll);
        root.Children.Add(footer);

        BorderThickness = new Thickness(0, 0, 1, 0);
        Child = root;
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Background);
        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _filter.SelectionChanged += _ => ApplyFilter();
        _search.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                ApplyFilter();
            }
        };
        ApplyFilter();
        GalleryText.Track(this, UpdateEmptyText);
    }

    #region Building

    private Control BuildSearchBox()
    {
        GalleryText.Bind(_search, TextBox.PlaceholderTextProperty, GalleryShellLangResourceKind.SearchComponents);
        _search.StyleVariant = AtomUI.Controls.InputControlStyleVariant.Borderless;
        _search.BorderThickness = new Thickness(0);
        _search.Padding = new Thickness(0);
        _search.MinHeight = 0;
        _search.FontSize = 14;
        _search.VerticalAlignment = VerticalAlignment.Center;
        _search.VerticalContentAlignment = VerticalAlignment.Center;
        _search.Background = Brushes.Transparent;
        ArcTokens.Bind(_search, TemplatedControl.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_search, TemplatedControl.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _search.GotFocus += (_, _) => SetSearchBorder(focused: true);
        _search.LostFocus += (_, _) => SetSearchBorder(focused: false);

        var icon = GalleryUi.Glyph(ArcIcons.Search, 16, ArcDesignTokenKind.TextMuted);
        icon.Margin = new Thickness(14, 0, 8, 0);
        var tail = new Panel { Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        tail.Children.Add(_shortcut);
        tail.Children.Add(_clear);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), MinHeight = 40 };
        Grid.SetColumn(_search, 1);
        Grid.SetColumn(tail, 2);
        grid.Children.Add(icon);
        grid.Children.Add(_search);
        grid.Children.Add(tail);

        _searchBox.Child = grid;
        _searchBox.CornerRadius = new CornerRadius(999);
        _searchBox.BorderThickness = new Thickness(1);
        ArcTokens.Bind(_searchBox, BackgroundProperty, ArcDesignTokenKind.Surface);
        SetSearchBorder(focused: false);
        // A click anywhere on the pill focuses the field.
        _searchBox.PointerPressed += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, _search))
            {
                _search.Focus();
            }
        };
        return _searchBox;
    }

    private void SetSearchBorder(bool focused)
    {
        _searchBorder?.Dispose();
        _searchBorder = ArcTokens.Bind(_searchBox, BorderBrushProperty, focused ? ArcDesignTokenKind.BorderStrong : ArcDesignTokenKind.Border);
    }

    private Button BuildClearButton()
    {
        var clear = new Button
        {
            Content = GalleryUi.Glyph(GalleryIcons.X, 14, ArcDesignTokenKind.TextSecondary),
            Variant = ButtonVariant.Ghost,
            Size = ControlSize.Small,
            MinHeight = 28,
            MinWidth = 28,
            Padding = new Thickness(0),
            IsPressScaleEnabled = false,
            IsVisible = false
        };
        GalleryText.Bind(clear, Avalonia.Automation.AutomationProperties.NameProperty, GalleryShellLangResourceKind.ClearSearch);
        clear.Click += (_, _) =>
        {
            _search.Text = string.Empty;
            _search.Focus();
        };
        return clear;
    }

    private static Control BuildShortcutHint()
    {
        var hint = GalleryUi.Text(OperatingSystem.IsMacOS() ? "⌘ K" : "Ctrl K", ArcDesignTokenKind.TextMuted, 12);
        hint.Margin = new Thickness(0, 0, 6, 0);
        hint.VerticalAlignment = VerticalAlignment.Center;
        return hint;
    }

    /// <summary>
    /// A collapsible sidebar section and the groups under it. A flattened section (Docs) shows its one group's rows
    /// without that group's own header, which would repeat the section's title.
    /// </summary>
    private Control BuildSection(GalleryShellLangResourceKind titleKind, Geometry icon, GalleryCategory[] categories,
        bool isBlockGroups = false, bool flatten = false)
    {
        var chevron = new ArcGlyph { Data = ArcIcons.ChevronDown, Size = 14 };
        ArcTokens.Bind(chevron, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        var count = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextMuted, 12);
        var header = BuildSectionHeader(icon, titleKind, chevron, count);
        var stack = new StackPanel();
        var body = new SpringSizeDecorator { Axis = SpringSizeAxis.Height, IsExpanded = true, Spring = ArcSpring.Smooth, Child = stack };
        var root = new StackPanel { Children = { header, body } };

        var section = new Section { Root = root, Header = header, Chevron = chevron, Count = count, Body = body };
        foreach (var category in categories)
        {
            var group = BuildGroup(category, isBlockGroups);
            group.Section = section;
            if (flatten)
            {
                group.Header.IsVisible = false;
                group.UserExpanded = true;
            }
            section.Groups.Add(group);
            stack.Children.Add(group.Root);
        }
        header.Click += () =>
        {
            body.IsExpanded = !body.IsExpanded;
            chevron.RenderTransform = body.IsExpanded ? null : new RotateTransform(-90);
            SyncIndicator(animate: false);
        };
        _sections.Add(section);
        return root;
    }

    private static GalleryNavRow BuildSectionHeader(Geometry icon, GalleryShellLangResourceKind titleKind, ArcGlyph chevron, TextBlock count)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto") };
        var glyph = new ArcGlyph { Data = icon, Size = 16, VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        GalleryText.Bind(title, TextBlock.TextProperty, titleKind);
        count.Margin = new Thickness(0, 0, 8, 0);
        count.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(title, 1);
        Grid.SetColumn(count, 2);
        Grid.SetColumn(chevron, 3);
        grid.Children.Add(glyph);
        grid.Children.Add(title);
        grid.Children.Add(count);
        grid.Children.Add(chevron);

        var header = new GalleryNavRow(grid, fontSize: 14, height: 36, padding: new Thickness(8, 0),
            weight: FontWeight.Medium, rest: ArcDesignTokenKind.Foreground, hoverFill: ArcDesignTokenKind.SurfaceMuted)
        {
            Margin = new Thickness(0, 4, 0, 2)
        };
        GalleryText.Bind(header, Avalonia.Automation.AutomationProperties.NameProperty, titleKind);
        return header;
    }

    private Group BuildGroup(GalleryCategory category, bool isBlockGroup = false)
    {
        var chevron = new ArcGlyph { Data = ArcIcons.ChevronRight, Size = 14, RenderTransformOrigin = RelativePoint.Center };
        ArcTokens.Bind(chevron, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        var title = new TextBlock { Text = category.Title, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        GalleryShellLangResourceKind titleKind;
        var hasTitle = isBlockGroup
            ? GalleryText.TryGetBlockGroup(category.Code, out titleKind)
            : GalleryText.TryGetCategory(category.Code, out titleKind);
        if (hasTitle)
        {
            GalleryText.Bind(title, TextBlock.TextProperty, titleKind);
        }
        var count = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextMuted, 11);
        var chip = GalleryUi.Chip(count, padding: new Thickness(7, 1));
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(title, 1);
        Grid.SetColumn(chip, 2);
        row.Children.Add(chevron);
        row.Children.Add(title);
        row.Children.Add(chip);

        var header = new GalleryNavRow(row, category.Title, fontSize: 14, height: 32, padding: new Thickness(8, 0),
            rest: ArcDesignTokenKind.Foreground, hoverFill: ArcDesignTokenKind.SurfaceMuted);
        if (hasTitle)
        {
            GalleryText.Bind(header, Avalonia.Automation.AutomationProperties.NameProperty, titleKind);
        }

        var stack = new StackPanel { Margin = new Thickness(0, 2, 0, 6) };
        var rail = GalleryUi.Rail(stack, RailInset);
        var items = new SpringSizeDecorator { Axis = SpringSizeAxis.Height, IsExpanded = false, Spring = ArcSpring.Smooth, Child = rail };
        var root = new StackPanel();
        root.Children.Add(header);
        root.Children.Add(items);

        var group = new Group
        {
            Category = category,
            Root = root,
            Header = header,
            Chevron = chevron,
            Count = count,
            Items = items,
            Rail = rail
        };
        foreach (var entry in category.Entries)
        {
            var item = BuildItem(group, entry);
            group.Entries.Add(item);
            _items.Add(item);
            _byClass[entry.ClassName] = item;
            stack.Children.Add(item.Row);
        }
        // Subgroups are built first, so they come before this group in _groups.
        foreach (var subcategory in category.Subcategories ?? Array.Empty<GalleryCategory>())
        {
            var child = BuildGroup(subcategory, isBlockGroup: true);
            child.Parent = group;
            group.Children.Add(child);
            stack.Children.Add(child.Root);
        }
        header.Click += () =>
        {
            group.UserExpanded = !group.UserExpanded;
            Refresh(group);
            SyncIndicator(animate: false);
        };
        _groups.Add(group);
        return group;
    }

    private Item BuildItem(Group group, GalleryEntry entry)
    {
        var implemented = GalleryPages.IsImplemented(entry.ClassName);
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        content.Children.Add(new TextBlock { Text = entry.Title, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
        if (entry.IsNew)
        {
            var badge = new GalleryNewBadge { Margin = new Thickness(8, 0, 4, 0) };
            Grid.SetColumn(badge, 1);
            content.Children.Add(badge);
        }
        else if (!implemented)
        {
            var phase = GalleryUi.Text($"P{entry.Phase}", ArcDesignTokenKind.TextMuted, 11);
            phase.VerticalAlignment = VerticalAlignment.Center;
            phase.Margin = new Thickness(8, 0, 4, 0);
            Grid.SetColumn(phase, 1);
            content.Children.Add(phase);
        }
        // Planned components read quieter until the pointer is on them.
        var row = implemented
            ? new GalleryNavRow(content, entry.Title)
            : new GalleryNavRow(content, entry.Title, rest: ArcDesignTokenKind.TextMuted, hover: ArcDesignTokenKind.TextSecondary);
        // One tab stop per group: arrows move between items.
        row.IsTabStop = false;
        var item = new Item { Entry = entry, Group = group, Row = row, Implemented = implemented };
        row.Click += () => Select(entry);
        return item;
    }

    private Control BuildFooter(int implemented, int total)
    {
        var dot = new Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(dot, Shape.FillProperty, ArcDesignTokenKind.Success);
        var label = new TextBlock { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        GalleryText.Bind(label, TextBlock.TextProperty, GalleryShellLangResourceKind.Implemented);
        var count = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextMuted, 12);
        GalleryText.BindFormat(count, TextBlock.TextProperty, GalleryShellLangResourceKind.ImplementedCount, implemented, total);
        count.VerticalAlignment = VerticalAlignment.Center;
        count.Margin = new Thickness(0, 0, 8, 0);
        var chevron = GalleryUi.Glyph(ArcIcons.ChevronRight, 14, ArcDesignTokenKind.TextMuted);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto") };
        Grid.SetColumn(label, 1);
        Grid.SetColumn(count, 2);
        Grid.SetColumn(chevron, 3);
        grid.Children.Add(dot);
        grid.Children.Add(label);
        grid.Children.Add(count);
        grid.Children.Add(chevron);

        var card = new GalleryNavRow(grid, fontSize: 13, height: 40, padding: new Thickness(16, 0, 12, 0),
            radius: 20, weight: FontWeight.Medium, rest: ArcDesignTokenKind.Foreground,
            restFill: ArcDesignTokenKind.Surface, hoverFill: ArcDesignTokenKind.SurfaceMuted, stroke: ArcDesignTokenKind.Border);
        // The card is the shortcut to the finished components.
        card.Click += () =>
        {
            _filter.SelectedIndex = 1;
            ApplyFilter();
        };
        GalleryText.Bind(card, Avalonia.Automation.AutomationProperties.NameProperty, GalleryShellLangResourceKind.ShowImplementedComponents);
        return card;
    }

    #endregion

    #region Selection

    /// <summary>Selects <paramref name="entry"/>, opens its group and glides the rail bar to it.</summary>
    public void Select(GalleryEntry entry)
    {
        if (!_byClass.TryGetValue(entry.ClassName, out var item))
        {
            return;
        }
        if (_selected is { } previous)
        {
            previous.Row.IsSelected = false;
            previous.Row.IsTabStop = false;
        }
        _selected = item;
        item.Row.IsSelected = true;
        item.Row.IsTabStop = true;
        // Open the selected item's group and every group above it, and the section they sit in.
        var top = item.Group;
        for (var group = item.Group; group is not null; group = group.Parent)
        {
            group.UserExpanded = true;
            Refresh(group);
            top = group;
        }
        if (top.Section is { } section)
        {
            section.Body.IsExpanded = true;
            section.Chevron.RenderTransform = null;
        }
        // Reveal the row with room around it so it never lands under the fade at the bottom of the list.
        item.Row.BringIntoView(new Rect(0, -12, Math.Max(item.Row.Bounds.Width, 1), Math.Max(item.Row.Bounds.Height, 1) + 44));
        SyncIndicator(animate: true);
        EntrySelected?.Invoke(entry);
    }

    /// <summary>True while keyboard focus is in the search field.</summary>
    public bool IsSearchFocused => _search.IsKeyboardFocusWithin;

    /// <summary>Focuses the search field and selects its text (Cmd/Ctrl+K).</summary>
    public void FocusSearch()
    {
        _search.Focus();
        _search.SelectAll();
    }

    private void SyncIndicator(bool animate)
    {
        if (_selected is not { } item || !item.Visible || !item.Group.IsOpen || item.Row.Bounds.Height <= 0)
        {
            _indicator.Hide(animate: false);
            _lastRect = default;
            return;
        }
        if (item.Row.TranslatePoint(default, _content) is not { } origin ||
            item.Group.Rail.TranslatePoint(default, _content) is not { } railOrigin)
        {
            return;
        }
        var rect = new Rect(railOrigin.X - 0.5, origin.Y + 7, 2, Math.Max(0, item.Row.Bounds.Height - 14));
        if (!animate && rect == _lastRect)
        {
            return;
        }
        _lastRect = rect;
        // Layout passes (group collapse, resize, filtering) snap; only a selection change glides.
        _indicator.MoveTo(rect, animate);
    }

    #endregion

    #region Filtering

    private string Query => _search.Text?.Trim() ?? string.Empty;

    private bool IsFiltering => Query.Length > 0 || _filter.SelectedIndex != 0;

    private void ApplyFilter()
    {
        var query = Query;
        var mode = _filter.SelectedIndex;
        // Subgroups come before their parents in _groups, so a parent can add up its children's matches.
        foreach (var group in _groups)
        {
            var matches = 0;
            foreach (var item in group.Entries)
            {
                var visible = Matches(item, query, mode);
                item.Visible = visible;
                item.Row.IsVisible = visible;
                matches += visible ? 1 : 0;
            }
            group.Matches = matches + group.Children.Sum(c => c.Matches);
            group.Visible = group.Matches > 0 || (!IsFiltering && group.IsPlaceholder);
            group.Root.IsVisible = group.Visible;
            group.Count.Text = group.Matches.ToString();
            Refresh(group);
        }
        foreach (var section in _sections)
        {
            section.Count.Text = section.Groups.Sum(g => g.Matches).ToString();
            // A section with no groups yet stays listed while nothing is filtered, like a placeholder group.
            section.Visible = section.Groups.Count == 0 ? !IsFiltering : section.Groups.Any(g => g.Visible);
            section.Root.IsVisible = section.Visible;
        }
        _empty.IsVisible = !_groups.Any(g => g.Visible);
        UpdateEmptyText();
        _clear.IsVisible = query.Length > 0;
        _shortcut.IsVisible = query.Length == 0;
        SyncIndicator(animate: false);
    }

    /// <summary>The message of an empty list: no match for the query, or nothing under the filter. Follows the language.</summary>
    private void UpdateEmptyText()
    {
        var query = Query;
        _empty.Text = query.Length > 0
            ? GalleryText.Format(GalleryShellLangResourceKind.NoComponentsMatch, query)
            : GalleryText.Get(GalleryShellLangResourceKind.NothingHereYet);
    }

    private static bool Matches(Item item, string query, int mode)
    {
        if (mode == 1 && !item.Implemented)
        {
            return false;
        }
        if (mode == 2 && item.Implemented)
        {
            return false;
        }
        return query.Length == 0 ||
               item.Entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               item.Entry.ClassName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               item.Entry.Slug.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A group is open when the user opened it, or when a filter is narrowing the list to it.</summary>
    private void Refresh(Group group)
    {
        var open = IsFiltering ? group.Visible : group.UserExpanded;
        group.Items.IsExpanded = open;
        group.Chevron.RenderTransform = open ? new RotateTransform(90) : null;
    }

    #endregion

    #region Keyboard

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var inSearch = IsSearchFocused;
        switch (e.Key)
        {
            case Key.Down or Key.Up:
                Move(e.Key == Key.Down ? 1 : -1, focusItem: !inSearch);
                e.Handled = true;
                break;
            case Key.Enter when inSearch:
                var first = _items.FirstOrDefault(i => i.Visible);
                if (first is not null)
                {
                    Select(first.Entry);
                }
                e.Handled = true;
                break;
            case Key.Escape when inSearch && Query.Length > 0:
                _search.Text = string.Empty;
                e.Handled = true;
                break;
        }
    }

    private void Move(int step, bool focusItem)
    {
        var open = _items.Where(i => i.Visible && i.Group.IsOpen).ToList();
        if (open.Count == 0)
        {
            return;
        }
        var index = _selected is null ? -1 : open.IndexOf(_selected);
        var next = index < 0 ? (step > 0 ? 0 : open.Count - 1) : index + step;
        if (next < 0 || next >= open.Count)
        {
            return;
        }
        Select(open[next].Entry);
        if (focusItem)
        {
            open[next].Row.Focus(NavigationMethod.Directional);
        }
    }

    #endregion
}
