using System.Globalization;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// uiarc.dev's <c>customers-table</c> block, composed from Arc controls because TKCode.Arc.Blocks has no such block:
/// saved-view tabs with live counts, a search field, three filter menus with per-value counts, a sortable grid of 10 rows
/// a page with checkbox selection and a menu per row, pagination, and a floating bar of bulk actions. Actions report
/// through the app's toasts, with Undo for archiving. Its breakpoints follow its own width, not the window's.
/// </summary>
public sealed class DemoCustomersTable : Border
{
    private const int PageSize = 10;
    private const double RowHeight = 52;
    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    private enum SortKey
    {
        Name,
        Plan,
        Mrr,
        Seats,
        Health,
        Owner,
        Seen
    }

    private enum Col
    {
        Check,
        Company,
        Plan,
        Mrr,
        Seats,
        Health,
        Owner,
        Seen,
        Menu
    }

    /// <summary>The block's width classes (uiarc.dev's container queries at 1000, 860, 640 and 440).</summary>
    private enum Breadth
    {
        Tiny,
        Narrow,
        Compact,
        Medium,
        Wide
    }

    private sealed record SavedView(string Id, string Label, Func<DemoCustomer, bool> Test, string EmptyTitle);

    private sealed record SortColumn(SortKey Key, string Label, bool FirstAscending, string AscendingPhrase, string DescendingPhrase);

    /// <summary>One filter menu (Plan, Status, Owner) and the values picked in it.</summary>
    private sealed class Filter(string label, string plural, (string Value, string Label)[] options, Func<DemoCustomer, string> valueOf)
    {
        public string Label { get; } = label;
        public string Plural { get; } = plural;
        public (string Value, string Label)[] Options { get; } = options;
        public Func<DemoCustomer, string> ValueOf { get; } = valueOf;
        public HashSet<string> Picked { get; } = [];
        public Dictionary<string, (MenuEntry Entry, TextBlock Count)> Rows { get; } = new();
        public MenuEntrySeparator? ClearSeparator { get; set; }
        public MenuEntry? ClearEntry { get; set; }
        public TextMorph? PillText { get; set; }
        public Border? PillFill { get; set; }
        public Rectangle? PillDash { get; set; }
        public AvaloniaButton? Pill { get; set; }
    }

    /// <summary>The controls of one row on the page that change when the selection does.</summary>
    private sealed record RowParts(DemoCustomer Customer, Checkbox Check, Border Tint, Control MenuTrigger);

    private static readonly SavedView[] s_views =
    [
        new("all", "All customers", _ => true, "No customers yet"),
        new("trials", "Trials ending", c => c.Status == CustomerStatus.Trial && c.TrialDays <= 7, "No trials end in the next 7 days"),
        new("risk", "At risk", c => c.Health == CustomerHealth.Risk, "No accounts are at risk"),
        new("enterprise", "Enterprise", c => c.Plan == CustomerPlan.Enterprise, "No enterprise accounts")
    ];

    private static readonly SortColumn[] s_sorts =
    [
        new(SortKey.Name, "Company", true, "A to Z", "Z to A"),
        new(SortKey.Plan, "Plan", false, "smallest plan first", "largest plan first"),
        new(SortKey.Mrr, "MRR", false, "lowest first", "highest first"),
        new(SortKey.Seats, "Seats", false, "fewest first", "most first"),
        new(SortKey.Health, "Health", false, "healthiest first", "most at risk first"),
        new(SortKey.Owner, "Owner", true, "A to Z", "Z to A"),
        new(SortKey.Seen, "Last seen", false, "least recent first", "most recent first")
    ];

    private readonly IDemoHost _host;
    private readonly List<DemoCustomer> _all = DemoData.Customers();
    private readonly HashSet<string> _archived = [];
    private readonly HashSet<string> _selected = [];
    private readonly Filter[] _filters;
    private readonly List<RowParts> _rowParts = [];
    private readonly Dictionary<(StyledElement, AvaloniaProperty), IDisposable> _bindings = new();

    private readonly AnimatedCounter _summaryCount = Counter();
    private readonly TextBlock _summaryNoun = DemoUi.Text("accounts", ArcDesignTokenKind.TextMuted);
    private readonly AnimatedCounter _summaryMrr = Counter("$");
    private readonly StackPanel _tabRow = new() { Orientation = Orientation.Horizontal };
    private readonly Border _tabIndicator = new() { Height = 2, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly List<(SavedView View, AvaloniaButton Button, TextBlock Label, Border Pill, TextBlock Count)> _tabs = [];
    private readonly SearchField _search;
    private readonly WrapPanel _toolbar = new() { ItemSpacing = 8, LineSpacing = 8, Margin = new Thickness(20, 12) };
    private readonly AvaloniaButton _clearAll;
    private readonly Border _head = new() { Height = 36, BorderThickness = new Thickness(0, 1, 0, 0) };
    private readonly StackPanel _body = new() { ClipToBounds = true };
    private readonly Checkbox _pageCheck = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _range = DemoUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary);
    private readonly Pagination _pager = new() { Label = "Customer pages" };
    private readonly DockPanel _footer = new();
    private readonly Border _dockSpace = new() { Height = 76, IsVisible = false };

    // The floating bar of bulk actions, and the panel that places it over the bottom of the table.
    private readonly Border _dock = new() { Padding = new Thickness(5), BorderThickness = new Thickness(1) };
    private readonly Panel _dockHost = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom };
    private readonly TextBlock _dockCount = DemoUi.Text(string.Empty, weight: FontWeight.Medium);
    private readonly AvaloniaButton _selectAll;
    private readonly TextBlock _selectAllText = DemoUi.Text(string.Empty, weight: FontWeight.Medium);
    private readonly List<TextBlock> _dockLabels = [];
    private readonly ContentControl _exportContent = new();
    private readonly DispatcherTimer _exportTimer = new() { Interval = TimeSpan.FromSeconds(1.8) };

    private SavedView _view = s_views[0];
    private SortColumn _sort = s_sorts[2];
    private bool _ascending;
    private int _page = 1;
    private Breadth _width = Breadth.Wide;
    private bool _syncingPager;
    private bool _indicatorPlaced;
    private List<DemoCustomer> _visible = [];

    public DemoCustomersTable(IDemoHost host)
    {
        _host = host;
        CornerRadius = new CornerRadius(DemoUi.CardRadius);
        BorderThickness = new Thickness(1);
        ClipToBounds = true;
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);
        AutomationProperties.SetName(this, "Customers");

        _filters =
        [
            new Filter("Plan", "plans",
                Enum.GetValues<CustomerPlan>().Select(plan => (plan.ToString(), DemoData.Label(plan))).ToArray(), c => c.Plan.ToString()),
            new Filter("Status", "statuses",
                Enum.GetValues<CustomerStatus>().Select(status => (status.ToString(), DemoData.Label(status))).ToArray(), c => c.Status.ToString()),
            new Filter("Owner", "owners",
                DemoData.CustomerOwners.Select(id => (id, DemoData.Person(id).Name)).ToArray(), c => c.Owner)
        ];

        _search = new SearchField
        {
            Label = "Search customers",
            IsLabelVisible = false,
            Placeholder = "Search company, domain, or owner",
            Size = ControlSize.Small,
            Width = 340
        };
        _search.TextChanged += (_, _) =>
        {
            ResetPageAndSelection();
            Refresh(RowEntrance.Fade);
        };
        _clearAll = Bare(DemoUi.Text("Clear all", ArcDesignTokenKind.TextSecondary, weight: FontWeight.Medium), new Thickness(12, 0));
        _clearAll.Height = 32;
        _clearAll.IsVisible = false;
        _clearAll.Click += (_, _) => ClearSearchAndFilters();

        _selectAll = Bare(_selectAllText, new Thickness(10, 0));
        _selectAll.Height = 32;
        _selectAll.Click += (_, _) => SelectAllInView();
        _exportTimer.Tick += (_, _) =>
        {
            _exportTimer.Stop();
            _exportContent.Content = DockLabel(ArcIcons.Download, "Export");
        };

        var column = new StackPanel();
        column.Children.Add(BuildHeader());
        column.Children.Add(BuildTabs());
        column.Children.Add(BuildToolbar());
        column.Children.Add(_head);
        column.Children.Add(_body);
        column.Children.Add(BuildFooter());
        column.Children.Add(_dockSpace);
        Child = column;
        BuildDock();

        _pageCheck.Click += (_, _) => TogglePage();
        _pager.PageChanged += (_, e) =>
        {
            if (_syncingPager)
            {
                return;
            }
            _page = e.NewPage;
            Refresh(RowEntrance.Stagger);
        };
        _body.Transitions = [new DoubleTransition { Property = HeightProperty, Duration = TimeSpan.FromMilliseconds(240), Easing = new Avalonia.Animation.Easings.CubicEaseOut() }];

        SizeChanged += (_, e) => ApplyWidth(e.NewSize.Width);
        Refresh(RowEntrance.None);
    }

    /// <summary>The floating bar of bulk actions; the page lays it over its scroller and calls <see cref="PlaceDock"/>.</summary>
    public Control Dock => _dockHost;

    #region Building

    private Control BuildHeader()
    {
        var summary = new WrapPanel { ItemSpacing = 4 };
        summary.Children.Add(_summaryCount);
        summary.Children.Add(_summaryNoun);
        summary.Children.Add(DemoUi.Text("·", ArcDesignTokenKind.TextMuted));
        summary.Children.Add(_summaryMrr);
        summary.Children.Add(DemoUi.Text("MRR", ArcDesignTokenKind.TextMuted));

        var header = new StackPanel { Spacing = 2, Margin = new Thickness(20, 16, 20, 12) };
        header.Children.Add(DemoUi.Text("Customers", ArcDesignTokenKind.Foreground, 15, FontWeight.Medium));
        header.Children.Add(summary);
        return header;
    }

    private static AnimatedCounter Counter(string? prefix = null)
    {
        var counter = new AnimatedCounter { Prefix = prefix, CultureName = "en-US", FontSize = DemoUi.BodySize, FontWeight = FontWeight.Medium };
        ArcTokens.Bind(counter, TemplatedControl.ForegroundProperty, ArcDesignTokenKind.Foreground);
        return counter;
    }

    private Control BuildTabs()
    {
        ArcTokens.Bind(_tabIndicator, BackgroundProperty, ArcDesignTokenKind.Accent);
        foreach (var view in s_views)
        {
            var label = DemoUi.Line(view.Label, ArcDesignTokenKind.TextSecondary, weight: FontWeight.Medium);
            label.VerticalAlignment = VerticalAlignment.Center;
            var count = DemoUi.Text("0", ArcDesignTokenKind.TextSecondary, 12, FontWeight.Medium);
            count.HorizontalAlignment = HorizontalAlignment.Center;
            count.VerticalAlignment = VerticalAlignment.Center;
            var pill = new Border { MinWidth = 22, Height = 20, Padding = new Thickness(6, 0), CornerRadius = new CornerRadius(10), Child = count, VerticalAlignment = VerticalAlignment.Center };
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { label, pill } };
            var button = Bare(content, new Thickness(12, 0));
            button.Height = 40;
            AutomationProperties.SetName(button, view.Label);
            button.Click += (_, _) => SelectView(view);
            _tabs.Add((view, button, label, pill, count));
            _tabRow.Children.Add(button);
        }
        _tabRow.KeyDown += OnTabKeyDown;

        var strip = new Panel { Margin = new Thickness(12, 0) };
        strip.Children.Add(_tabRow);
        strip.Children.Add(_tabIndicator);
        strip.LayoutUpdated += (_, _) => PlaceIndicator();
        AutomationProperties.SetName(strip, "Saved views");

        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = strip
        };
        var frame = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = scroller };
        ArcTokens.Bind(frame, BorderBrushProperty, ArcDesignTokenKind.Border);
        return frame;
    }

    private Control BuildToolbar()
    {
        _toolbar.Children.Add(_search);
        foreach (var filter in _filters)
        {
            _toolbar.Children.Add(BuildFilterMenu(filter));
        }
        _toolbar.Children.Add(_clearAll);
        return _toolbar;
    }

    private Control BuildFilterMenu(Filter filter)
    {
        filter.PillText = new TextMorph { Text = filter.Label, FontSize = DemoUi.BodySize, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(filter.PillText, TextMorph.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var chevron = DemoUi.Glyph(ArcIcons.ChevronDown, 14);
        chevron.VerticalAlignment = VerticalAlignment.Center;
        chevron.RenderTransform = TransformOperations.Parse("rotate(0deg)");
        chevron.Transitions = [new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(300), Easing = new Avalonia.Animation.Easings.CubicEaseOut() }];

        filter.PillDash = new Rectangle { StrokeThickness = 1, StrokeDashArray = [4, 3], RadiusX = 8, RadiusY = 8 };
        ArcTokens.Bind(filter.PillDash, Shape.StrokeProperty, ArcDesignTokenKind.BorderStrong);
        filter.PillFill = new Border { BorderThickness = new Thickness(1), IsVisible = false }.BindCornerRadius(ArcDesignTokenKind.RadiusControl);
        ArcTokens.Bind(filter.PillFill, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(filter.PillFill, BorderBrushProperty, ArcDesignTokenKind.BorderStrong);
        // A dashed outline needs a shape (Border has no dashes); its corners follow the control radius, at most a capsule.
        filter.PillDash.SizeChanged += (_, e) =>
        {
            var radius = Math.Min(e.NewSize.Height / 2, ArcTokens.GetCornerRadius(filter.PillDash, ArcDesignTokenKind.RadiusControl, new CornerRadius(8)).TopLeft);
            filter.PillDash.RadiusX = radius;
            filter.PillDash.RadiusY = radius;
        };

        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, Children = { filter.PillText, chevron } };
        var face = new Panel { Children = { filter.PillDash, filter.PillFill, label } };
        var pill = Bare(face, new Thickness(0));
        pill.Height = 32;
        pill.VerticalContentAlignment = VerticalAlignment.Stretch;
        filter.Pill = pill;
        AutomationProperties.SetName(pill, $"Filter by {filter.Label.ToLowerInvariant()}");

        var menu = new DropdownMenu { Trigger = pill, Alignment = MenuAlignment.Start };
        var owners = filter.Label == "Owner";
        foreach (var (value, text) in filter.Options)
        {
            var count = DemoUi.Text("0", ArcDesignTokenKind.TextMuted, 12);
            count.VerticalAlignment = VerticalAlignment.Center;
            var header = new DockPanel { MinWidth = 150 };
            DockPanel.SetDock(count, Avalonia.Controls.Dock.Right);
            count.Margin = new Thickness(16, 0, 0, 0);
            header.Children.Add(count);
            header.Children.Add(DemoUi.Line(text));
            var entry = new MenuEntry
            {
                Header = header,
                ToggleType = MenuEntryToggleType.CheckBox,
                StaysOpenOnClick = true,
                Icon = owners ? new Avatar { DisplayName = text, Source = DemoPhotos.For(text), Size = AvatarSize.Small } : null
            };
            TextSearch.SetText(entry, text);
            entry.Click += (_, _) =>
            {
                if (entry.IsChecked)
                {
                    filter.Picked.Add(value);
                }
                else
                {
                    filter.Picked.Remove(value);
                }
                ResetPageAndSelection();
                Refresh(RowEntrance.Fade);
            };
            filter.Rows[value] = (entry, count);
            menu.Items.Add(entry);
        }
        filter.ClearSeparator = new MenuEntrySeparator { IsVisible = false };
        filter.ClearEntry = new MenuEntry { Header = $"Clear {filter.Label.ToLowerInvariant()} filter", IsVisible = false };
        filter.ClearEntry.Click += (_, _) =>
        {
            filter.Picked.Clear();
            ResetPageAndSelection();
            Refresh(RowEntrance.Fade);
        };
        menu.Items.Add(filter.ClearSeparator);
        menu.Items.Add(filter.ClearEntry);
        menu.Opened += (_, _) => chevron.RenderTransform = TransformOperations.Parse("rotate(180deg)");
        menu.Closed += (_, _) => chevron.RenderTransform = TransformOperations.Parse("rotate(0deg)");
        return menu;
    }

    private Control BuildFooter()
    {
        _range.VerticalAlignment = VerticalAlignment.Center;
        _pager.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(_pager, Avalonia.Controls.Dock.Right);
        _footer.Children.Add(_pager);
        _footer.Children.Add(_range);
        var frame = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(20, 8, 12, 8), Child = _footer };
        ArcTokens.Bind(frame, BorderBrushProperty, ArcDesignTokenKind.Border);
        return frame;
    }

    private void BuildDock()
    {
        var divider = new Border { Width = 1, Height = 20, Margin = new Thickness(4, 0), VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(divider, BackgroundProperty, ArcDesignTokenKind.Border);

        var assignMenu = new DropdownMenu { Side = MenuSide.Top, Alignment = MenuAlignment.Center };
        assignMenu.Items.Add(new MenuEntryLabel { Text = "Assign to" });
        foreach (var owner in DemoData.CustomerOwners)
        {
            var name = DemoData.Person(owner).Name;
            var entry = new MenuEntry { Header = name, ToggleType = MenuEntryToggleType.Radio, GroupName = "assign", Icon = new Avatar { DisplayName = name, Source = DemoPhotos.For(name), Size = AvatarSize.Small } };
            entry.Click += (_, _) =>
            {
                entry.IsChecked = false;
                AssignSelected(owner);
            };
            assignMenu.Items.Add(entry);
        }
        var assign = DockButton(s_userRound, "Assign owner");
        assignMenu.Trigger = assign;

        _exportContent.Content = DockLabel(ArcIcons.Download, "Export");
        var export = Bare(_exportContent, new Thickness(10, 0));
        export.Height = 32;
        export.Click += (_, _) => Export();

        var archive = DockButton(ArcIcons.Archive, "Archive");
        archive.Click += (_, _) => ArchiveSelected();

        var clear = Bare(DemoUi.Glyph(ArcIcons.X, 16), new Thickness(0));
        clear.Width = 32;
        clear.Height = 32;
        AutomationProperties.SetName(clear, "Clear selection");
        clear.Click += (_, _) =>
        {
            _selected.Clear();
            SyncSelection();
        };

        _dockCount.Margin = new Thickness(12, 0, 8, 0);
        _dockCount.VerticalAlignment = VerticalAlignment.Center;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Height = 32 };
        row.Children.AddRange([_dockCount, _selectAll, divider, assignMenu, export, archive, clear]);
        AutomationProperties.SetName(row, "Bulk actions");

        _dock.Child = row;
        _dock.CornerRadius = new CornerRadius(999);
        ArcTokens.Bind(_dock, BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(_dock, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(_dock, BoxShadowProperty, ArcDesignTokenKind.ShadowFloating);
        _dock.Opacity = 0;
        _dock.IsHitTestVisible = false;
        _dock.RenderTransform = TransformOperations.Parse("translateY(28px)");
        _dock.Transitions =
        [
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(160) },
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(320), Easing = new Avalonia.Animation.Easings.CubicEaseOut() }
        ];
        _dockHost.Margin = new Thickness(0, 0, 0, 16);
        _dockHost.Children.Add(_dock);
    }

    private AvaloniaButton DockButton(Geometry? icon, string label, ArcDesignTokenKind color = ArcDesignTokenKind.TextSecondary)
    {
        var button = Bare(DockLabel(icon, label, color), new Thickness(10, 0));
        button.Height = 32;
        return button;
    }

    private StackPanel DockLabel(Geometry? icon, string label, ArcDesignTokenKind color = ArcDesignTokenKind.TextSecondary)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (icon is not null)
        {
            var glyph = DemoUi.Glyph(icon, 16, color);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            content.Children.Add(glyph);
        }
        var text = DemoUi.Text(label, color, weight: FontWeight.Medium);
        text.VerticalAlignment = VerticalAlignment.Center;
        text.TextWrapping = TextWrapping.NoWrap;
        text.IsVisible = _width >= Breadth.Compact || icon is null;
        if (icon is not null)
        {
            _dockLabels.Add(text);
        }
        content.Children.Add(text);
        return content;
    }

    /// <summary>
    /// A button without chrome (a transparent hit area around <paramref name="content"/>), for the tabs, filter pills,
    /// sort headers and dock actions, whose looks are the block's own rather than an Arc button variant.
    /// </summary>
    private static AvaloniaButton Bare(Control content, Thickness padding)
    {
        var button = new AvaloniaButton
        {
            Content = content,
            Padding = padding,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalContentAlignment = VerticalAlignment.Center,
            Template = new FuncControlTemplate<AvaloniaButton>((owner, _) => new ContentPresenter
            {
                Name = "PART_ContentPresenter",
                [!ContentPresenter.ContentProperty] = owner[!ContentControl.ContentProperty],
                [!ContentPresenter.PaddingProperty] = owner[!PaddingProperty],
                [!ContentPresenter.BackgroundProperty] = owner[!BackgroundProperty],
                [!ContentPresenter.VerticalContentAlignmentProperty] = owner[!ContentControl.VerticalContentAlignmentProperty],
                [!ContentPresenter.HorizontalContentAlignmentProperty] = owner[!ContentControl.HorizontalContentAlignmentProperty],
                CornerRadius = new CornerRadius(8)
            })
        };
        return button;
    }

    #endregion

    #region The view

    private enum RowEntrance
    {
        None,
        Fade,
        Stagger,
        SlideLeft,
        SlideRight
    }

    private IEnumerable<DemoCustomer> Live => _all.Where(c => !_archived.Contains(c.Id));

    private bool MatchesSearch(DemoCustomer customer)
    {
        var query = (_search.Text ?? string.Empty).Trim().ToLowerInvariant();
        return query.Length == 0
               || customer.Name.ToLowerInvariant().Contains(query)
               || customer.Domain.ToLowerInvariant().Contains(query)
               || DemoData.Person(customer.Owner).Name.ToLowerInvariant().Contains(query);
    }

    private bool PassesFilters(DemoCustomer customer, Filter? except = null) =>
        _filters.All(filter => filter == except || filter.Picked.Count == 0 || filter.Picked.Contains(filter.ValueOf(customer)));

    private bool HasQuery => !string.IsNullOrWhiteSpace(_search.Text) || _filters.Any(filter => filter.Picked.Count > 0);

    private List<DemoCustomer> Rows(SavedView view) => Live.Where(c => MatchesSearch(c) && PassesFilters(c) && view.Test(c)).ToList();

    private int Compare(DemoCustomer a, DemoCustomer b)
    {
        var order = _sort.Key switch
        {
            SortKey.Plan => ((int)a.Plan, a.Mrr).CompareTo(((int)b.Plan, b.Mrr)),
            SortKey.Mrr => a.Mrr.CompareTo(b.Mrr),
            SortKey.Seats => a.Seats.CompareTo(b.Seats),
            SortKey.Health => ((int)b.Health).CompareTo((int)a.Health),
            SortKey.Owner => string.Compare(DemoData.Person(a.Owner).Name, DemoData.Person(b.Owner).Name, StringComparison.Ordinal),
            SortKey.Seen => (-a.SeenHours).CompareTo(-b.SeenHours),
            _ => string.Compare(a.Name, b.Name, StringComparison.Ordinal)
        };
        if (!_ascending)
        {
            order = -order;
        }
        return order != 0 ? order : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
    }

    private int PageCount => Math.Max(1, (int)Math.Ceiling(_visible.Count / (double)PageSize));

    private IEnumerable<DemoCustomer> CurrentPage => _visible.Skip((_page - 1) * PageSize).Take(PageSize);

    /// <summary>Recomputes the view (rows, counts, labels) and rebuilds the page of rows.</summary>
    private void Refresh(RowEntrance entrance)
    {
        _visible = Rows(_view);
        _visible.Sort(Compare);
        _page = Math.Clamp(_page, 1, PageCount);
        _selected.IntersectWith(_visible.Select(c => c.Id));

        _summaryCount.Value = _visible.Count;
        _summaryNoun.Text = _visible.Count == 1 ? "account" : "accounts";
        _summaryMrr.Value = _visible.Sum(c => c.Mrr);

        foreach (var tab in _tabs)
        {
            var active = tab.View == _view;
            tab.Count.Text = Rows(tab.View).Count.ToString(s_culture);
            Rebind(tab.Label, TextBlock.ForegroundProperty, active ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextSecondary);
            Rebind(tab.Count, TextBlock.ForegroundProperty, active ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextSecondary);
            Rebind(tab.Pill, BackgroundProperty, active ? ArcDesignTokenKind.AccentSubtle : ArcDesignTokenKind.SurfaceMuted);
            tab.Button.TabIndex = active ? 0 : -1;
            tab.Button.IsTabStop = active;
        }

        foreach (var filter in _filters)
        {
            SyncFilter(filter);
        }
        _clearAll.IsVisible = HasQuery;

        BuildHead();
        BuildRows(entrance);

        var start = _visible.Count == 0 ? 0 : (_page - 1) * PageSize + 1;
        var end = Math.Min(_page * PageSize, _visible.Count);
        _range.Text = _visible.Count == 0 ? "No results" : $"{start}–{end} of {_visible.Count}";
        _syncingPager = true;
        _pager.PageCount = (int)Math.Ceiling(_visible.Count / (double)PageSize);
        _pager.CurrentPage = _page;
        _syncingPager = false;
        AutomationProperties.SetName(_range, _visible.Count == 0
            ? "No customers match"
            : $"Showing {start} to {end} of {_visible.Count} customer{(_visible.Count == 1 ? string.Empty : "s")}");
        SyncSelection();
    }

    /// <summary>Binds a token to a property of a control that stays, replacing the token bound there before.</summary>
    private void Rebind(StyledElement target, AvaloniaProperty property, ArcDesignTokenKind kind)
    {
        if (_bindings.Remove((target, property), out var old))
        {
            old.Dispose();
        }
        _bindings[(target, property)] = ArcTokens.Bind(target, property, kind);
    }

    private void SyncFilter(Filter filter)
    {
        foreach (var (value, _) in filter.Options)
        {
            var (entry, count) = filter.Rows[value];
            entry.IsChecked = filter.Picked.Contains(value);
            count.Text = Live.Count(c => MatchesSearch(c) && PassesFilters(c, filter) && _view.Test(c) && filter.ValueOf(c) == value)
                             .ToString(s_culture);
        }
        var picked = filter.Options.Where(option => filter.Picked.Contains(option.Value)).Select(option => option.Label).ToList();
        var text = picked.Count switch
        {
            0 => null,
            1 => picked[0],
            2 => $"{picked[0]}, {picked[1]}",
            _ => $"{picked.Count} {filter.Plural}"
        };
        filter.PillText!.Text = text is null ? filter.Label : $"{filter.Label}: {text}";
        Rebind(filter.PillText, TextMorph.ForegroundProperty, text is null ? ArcDesignTokenKind.TextSecondary : ArcDesignTokenKind.Foreground);
        filter.PillFill!.IsVisible = text is not null;
        filter.PillDash!.IsVisible = text is null;
        filter.ClearSeparator!.IsVisible = text is not null;
        filter.ClearEntry!.IsVisible = text is not null;
        AutomationProperties.SetName(filter.Pill!, text is null ? $"Filter by {filter.Label.ToLowerInvariant()}" : $"{filter.Label} filter: {text}");
    }

    private void SelectView(SavedView view)
    {
        if (view == _view)
        {
            return;
        }
        var direction = Array.IndexOf(s_views, view) - Array.IndexOf(s_views, _view);
        _view = view;
        ResetPageAndSelection();
        Refresh(direction > 0 ? RowEntrance.SlideLeft : RowEntrance.SlideRight);
    }

    private void OnTabKeyDown(object? sender, KeyEventArgs e)
    {
        var index = Array.IndexOf(s_views, _view);
        var next = e.Key switch
        {
            Key.Left => (index + s_views.Length - 1) % s_views.Length,
            Key.Right => (index + 1) % s_views.Length,
            Key.Home => 0,
            Key.End => s_views.Length - 1,
            _ => -1
        };
        if (next < 0)
        {
            return;
        }
        e.Handled = true;
        SelectView(s_views[next]);
        _tabs[next].Button.Focus(NavigationMethod.Directional);
    }

    private void PlaceIndicator()
    {
        var tab = _tabs.FirstOrDefault(t => t.View == _view).Button;
        if (tab is null || tab.Bounds.Width <= 0)
        {
            return;
        }
        var left = tab.Bounds.X + 12;
        var width = Math.Max(0, tab.Bounds.Width - 24);
        if (Math.Abs(_tabIndicator.Margin.Left - left) < 0.5 && Math.Abs(_tabIndicator.Width - width) < 0.5)
        {
            return;
        }
        if (!_indicatorPlaced)
        {
            _indicatorPlaced = true;
            _tabIndicator.Margin = new Thickness(left, 0, 0, 0);
            _tabIndicator.Width = width;
            _tabIndicator.Transitions =
            [
                new ThicknessTransition { Property = MarginProperty, Duration = TimeSpan.FromMilliseconds(320), Easing = new Avalonia.Animation.Easings.CubicEaseOut() },
                new DoubleTransition { Property = WidthProperty, Duration = TimeSpan.FromMilliseconds(320), Easing = new Avalonia.Animation.Easings.CubicEaseOut() }
            ];
            return;
        }
        _tabIndicator.Margin = new Thickness(left, 0, 0, 0);
        _tabIndicator.Width = width;
    }

    private void ResetPageAndSelection()
    {
        _page = 1;
        _selected.Clear();
    }

    private void ClearSearchAndFilters()
    {
        foreach (var filter in _filters)
        {
            filter.Picked.Clear();
        }
        // Clearing the text runs the search handler, which refreshes; refresh anyway when it was already empty.
        var hadText = !string.IsNullOrEmpty(_search.Text);
        _search.Text = string.Empty;
        if (!hadText)
        {
            ResetPageAndSelection();
            Refresh(RowEntrance.Fade);
        }
    }

    private void ApplyWidth(double width)
    {
        var next = width >= 1000 ? Breadth.Wide : width >= 860 ? Breadth.Medium : width >= 640 ? Breadth.Compact : width >= 440 ? Breadth.Narrow : Breadth.Tiny;
        _search.Width = width < 640 ? Math.Max(0, width - 40) : 340;
        if (next == _width)
        {
            return;
        }
        _width = next;
        foreach (var label in _dockLabels)
        {
            label.IsVisible = width >= 560;
        }
        _footer.HorizontalAlignment = next == Breadth.Tiny ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        Refresh(RowEntrance.None);
    }

    #endregion

    #region Grid

    private (Col Col, GridLength Width)[] Columns => _width switch
    {
        Breadth.Wide =>
        [
            (Col.Check, new(36)), (Col.Company, GridLength.Star), (Col.Plan, new(112)), (Col.Mrr, new(84)), (Col.Seats, new(56)),
            (Col.Health, new(144)), (Col.Owner, new(132)), (Col.Seen, new(96)), (Col.Menu, new(36))
        ],
        Breadth.Medium =>
        [
            (Col.Check, new(36)), (Col.Company, GridLength.Star), (Col.Plan, new(112)), (Col.Mrr, new(84)), (Col.Health, new(144)),
            (Col.Owner, new(28)), (Col.Seen, new(96)), (Col.Menu, new(36))
        ],
        Breadth.Compact =>
        [
            (Col.Check, new(36)), (Col.Company, GridLength.Star), (Col.Mrr, new(84)), (Col.Health, new(144)), (Col.Owner, new(28)),
            (Col.Menu, new(36))
        ],
        _ => [(Col.Check, new(36)), (Col.Company, GridLength.Star), (Col.Mrr, new(76)), (Col.Health, new(18)), (Col.Menu, new(32))]
    };

    private Thickness RowPadding => _width <= Breadth.Narrow ? new Thickness(8, 0) : new Thickness(16, 0);

    private Grid RowGrid()
    {
        var grid = new Grid { ColumnSpacing = 12, Margin = RowPadding };
        foreach (var (_, width) in Columns)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(width));
        }
        return grid;
    }

    private void BuildHead()
    {
        ArcTokens.Bind(_head, BorderBrushProperty, ArcDesignTokenKind.Border);
        var grid = RowGrid();
        var columns = Columns;
        for (var i = 0; i < columns.Length; i++)
        {
            var cell = columns[i].Col switch
            {
                Col.Check => _pageCheck,
                Col.Menu => null,
                Col.Health when _width <= Breadth.Narrow => null,
                Col.Owner when _width < Breadth.Wide => null,
                var col => SortHeader(col)
            };
            if (cell is null)
            {
                continue;
            }
            if (cell.Parent is Panel old)
            {
                old.Children.Remove(cell);
            }
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }
        _head.Child = grid;
    }

    private Control SortHeader(Col col)
    {
        var key = col switch
        {
            Col.Plan => SortKey.Plan,
            Col.Mrr => SortKey.Mrr,
            Col.Seats => SortKey.Seats,
            Col.Health => SortKey.Health,
            Col.Owner => SortKey.Owner,
            Col.Seen => SortKey.Seen,
            _ => SortKey.Name
        };
        var sort = s_sorts.First(s => s.Key == key);
        var active = sort == _sort;
        var end = col is Col.Mrr or Col.Seats;
        var label = DemoUi.Line(sort.Label, active ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextMuted, 12, FontWeight.Medium);
        label.VerticalAlignment = VerticalAlignment.Center;
        var arrow = DemoUi.Glyph(ArcIcons.ArrowUp, 14, active ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextMuted);
        arrow.VerticalAlignment = VerticalAlignment.Center;
        arrow.Opacity = active ? 1 : 0;
        arrow.RenderTransform = TransformOperations.Parse(active && _ascending ? "rotate(0deg)" : "rotate(180deg)");
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        if (end)
        {
            content.Children.AddRange([arrow, label]);
        }
        else
        {
            content.Children.AddRange([label, arrow]);
        }
        var button = Bare(content, new Thickness(6, 0));
        button.Height = 28;
        button.Margin = new Thickness(-6, 0);
        button.HorizontalAlignment = end ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        button.PointerEntered += (_, _) => arrow.Opacity = active ? 1 : 0.5;
        button.PointerExited += (_, _) => arrow.Opacity = active ? 1 : 0;
        AutomationProperties.SetName(button, $"Sort by {sort.Label}");
        button.Click += (_, _) =>
        {
            _ascending = sort == _sort ? !_ascending : sort.FirstAscending;
            _sort = sort;
            _page = 1;
            Refresh(RowEntrance.Stagger);
        };
        return button;
    }

    private void BuildRows(RowEntrance entrance)
    {
        _body.Children.Clear();
        _rowParts.Clear();
        AutomationProperties.SetName(_body,
            $"Customers, sorted by {_sort.Label}, {(_ascending ? _sort.AscendingPhrase : _sort.DescendingPhrase)}");
        var rows = CurrentPage.ToList();
        if (rows.Count == 0)
        {
            _body.Height = 232;
            _body.Children.Add(EmptyState());
            return;
        }
        _body.Height = RowHeight * (_visible.Count > PageSize ? PageSize : rows.Count);
        var full = ArcMotion.IsFull(this) && entrance != RowEntrance.None;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = BuildRow(rows[i]);
            _body.Children.Add(row);
            if (!full)
            {
                continue;
            }
            var delay = entrance switch
            {
                RowEntrance.Fade => TimeSpan.FromSeconds(0.08),
                RowEntrance.Stagger => TimeSpan.FromSeconds(Math.Min(i * 0.03 * 0.6, 0.2)),
                _ => TimeSpan.Zero
            };
            row.Opacity = 0;
            var offset = entrance switch
            {
                RowEntrance.SlideLeft => 20,
                RowEntrance.SlideRight => -20,
                _ => 0
            };
            row.RenderTransform = TransformOperations.Parse($"translateX({offset}px)");
            row.Transitions =
            [
                new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(200), Delay = delay },
                new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(260), Delay = delay, Easing = new Avalonia.Animation.Easings.CubicEaseOut() }
            ];
            Dispatcher.UIThread.Post(() =>
            {
                row.Opacity = 1;
                row.RenderTransform = TransformOperations.Parse("translateX(0px)");
            }, DispatcherPriority.Loaded);
        }
    }

    private Control EmptyState()
    {
        var box = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 320 };
        var filtered = HasQuery;
        var title = DemoUi.Text(filtered ? "No customers match" : _view.EmptyTitle, ArcDesignTokenKind.Foreground, 14, FontWeight.Medium);
        title.TextAlignment = TextAlignment.Center;
        var text = DemoUi.Text(filtered ? "Try a different search or remove a filter." : "Accounts show up here as soon as they match this view.",
            ArcDesignTokenKind.TextMuted);
        text.TextAlignment = TextAlignment.Center;
        box.Children.Add(title);
        box.Children.Add(text);
        if (filtered)
        {
            var clear = DemoUi.Button("Clear search and filters");
            clear.HorizontalAlignment = HorizontalAlignment.Center;
            clear.Margin = new Thickness(0, 8, 0, 0);
            clear.Click += (_, _) => ClearSearchAndFilters();
            box.Children.Add(clear);
        }
        var frame = new Border { Height = 232, BorderThickness = new Thickness(0, 1, 0, 0), Child = box };
        ArcTokens.Bind(frame, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return frame;
    }

    private Control BuildRow(DemoCustomer customer)
    {
        var grid = RowGrid();
        var columns = Columns;
        Checkbox check = null!;
        Control trigger = null!;
        for (var i = 0; i < columns.Length; i++)
        {
            Control cell;
            switch (columns[i].Col)
            {
                case Col.Check:
                    check = new Checkbox { HorizontalAlignment = HorizontalAlignment.Center, IsChecked = _selected.Contains(customer.Id) };
                    AutomationProperties.SetName(check, $"Select {customer.Name}");
                    check.Click += (_, _) =>
                    {
                        if (!_selected.Remove(customer.Id))
                        {
                            _selected.Add(customer.Id);
                        }
                        SyncSelection();
                    };
                    cell = check;
                    break;
                case Col.Company:
                    cell = CompanyCell(customer);
                    break;
                case Col.Plan:
                    cell = PlanCell(customer);
                    break;
                case Col.Mrr:
                    cell = MrrCell(customer);
                    break;
                case Col.Seats:
                    cell = Figure(customer.Seats.ToString("N0", s_culture));
                    break;
                case Col.Health:
                    cell = HealthCell(customer);
                    break;
                case Col.Owner:
                    cell = OwnerCell(customer);
                    break;
                case Col.Seen:
                    cell = DemoUi.Line(LastSeen(customer.SeenHours), ArcDesignTokenKind.TextSecondary);
                    break;
                default:
                    cell = trigger = RowMenu(customer);
                    break;
            }
            cell.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        var tint = new Border { Opacity = 0.06, IsVisible = _selected.Contains(customer.Id) };
        ArcTokens.Bind(tint, BackgroundProperty, ArcDesignTokenKind.Accent);
        var row = new Border { Height = RowHeight, BorderThickness = new Thickness(0, 1, 0, 0), Child = new Panel { Children = { tint, grid } } };
        ArcTokens.Bind(row, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(row, BackgroundProperty, ArcDesignTokenKind.Surface);
        _rowParts.Add(new RowParts(customer, check, tint, trigger));
        return row;
    }

    private Control CompanyCell(DemoCustomer customer)
    {
        var name = DemoUi.Line(customer.Name, weight: FontWeight.Medium);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { name } };
        if (_width >= Breadth.Medium)
        {
            text.Children.Add(DemoUi.Line(customer.Domain, ArcDesignTokenKind.TextMuted, 12));
        }
        else
        {
            var inline = DemoUi.Line(PlanLine(customer), customer.Status == CustomerStatus.PastDue ? ArcDesignTokenKind.Danger : ArcDesignTokenKind.TextMuted, 12);
            text.Children.Add(inline);
        }
        if (_width == Breadth.Tiny)
        {
            return text;
        }
        var logo = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = Initials(customer.Name),
                FontSize = 12,
                FontWeight = FontWeight.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        ArcTokens.Bind(logo, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(logo, BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind((TextBlock)logo.Child, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var cell = new DockPanel();
        logo.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(logo, Avalonia.Controls.Dock.Left);
        cell.Children.Add(logo);
        cell.Children.Add(text);
        return cell;
    }

    private static string PlanLine(DemoCustomer customer) => customer.Status switch
    {
        CustomerStatus.Trial => $"{DemoData.Label(customer.Plan)} · {TrialNote(customer)}",
        CustomerStatus.PastDue => $"{DemoData.Label(customer.Plan)} · Past due",
        _ => DemoData.Label(customer.Plan)
    };

    private static string TrialNote(DemoCustomer customer) => $"Trial, {customer.TrialDays} day{(customer.TrialDays == 1 ? string.Empty : "s")} left";

    private static Control PlanCell(DemoCustomer customer)
    {
        var cell = new StackPanel { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Left };
        var badge = new Badge { Content = DemoData.Label(customer.Plan), Size = BadgeSize.Small, HorizontalAlignment = HorizontalAlignment.Left };
        cell.Children.Add(badge);
        if (customer.Status == CustomerStatus.Trial)
        {
            cell.Children.Add(DemoUi.Line(TrialNote(customer), ArcDesignTokenKind.TextSecondary, 12));
        }
        else if (customer.Status == CustomerStatus.PastDue)
        {
            cell.Children.Add(DemoUi.Line("Past due", ArcDesignTokenKind.Danger, 12));
        }
        return cell;
    }

    private static Control MrrCell(DemoCustomer customer)
    {
        if (customer.Status != CustomerStatus.Trial)
        {
            return Figure(Money(customer.Mrr));
        }
        var dash = Figure("–");
        ArcTokens.Bind(dash, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        AutomationProperties.SetName(dash, "No revenue during trial");
        return dash;
    }

    private static TextBlock Figure(string text)
    {
        var block = DemoUi.Line(text);
        block.HorizontalAlignment = HorizontalAlignment.Right;
        block.FontFeatures = FontFeatureCollection.Parse("tnum");
        return block;
    }

    private Control HealthCell(DemoCustomer customer)
    {
        var (icon, color) = customer.Health switch
        {
            CustomerHealth.Healthy => (ArcIcons.CircleCheck, ArcDesignTokenKind.Success),
            CustomerHealth.Attention => (ArcIcons.CircleAlert, ArcDesignTokenKind.Warning),
            _ => (ArcIcons.TriangleAlert, ArcDesignTokenKind.Danger)
        };
        var glyph = DemoUi.Glyph(icon, 16, color);
        glyph.VerticalAlignment = VerticalAlignment.Center;
        var label = DemoData.Label(customer.Health);
        if (_width <= Breadth.Narrow)
        {
            AutomationProperties.SetName(glyph, label);
            return glyph;
        }
        var text = DemoUi.Line(label, customer.Health == CustomerHealth.Risk ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextSecondary);
        text.VerticalAlignment = VerticalAlignment.Center;
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { glyph, text } };
    }

    private Control OwnerCell(DemoCustomer customer)
    {
        var name = DemoData.Person(customer.Owner).Name;
        var avatar = new Avatar { DisplayName = name, Source = DemoPhotos.For(name), Size = AvatarSize.Small, Width = 24, Height = 24, VerticalAlignment = VerticalAlignment.Center };
        if (_width < Breadth.Wide)
        {
            ToolTip.SetTip(avatar, name);
            AutomationProperties.SetName(avatar, name);
            return avatar;
        }
        var text = DemoUi.Line(name, ArcDesignTokenKind.TextSecondary);
        text.VerticalAlignment = VerticalAlignment.Center;
        var cell = new DockPanel();
        avatar.Margin = new Thickness(0, 0, 8, 0);
        DockPanel.SetDock(avatar, Avalonia.Controls.Dock.Left);
        cell.Children.Add(avatar);
        cell.Children.Add(text);
        return cell;
    }

    private Control RowMenu(DemoCustomer customer)
    {
        var trigger = new Button
        {
            Variant = ButtonVariant.Ghost,
            Size = ControlSize.Small,
            Width = 28,
            MinWidth = 28,
            Height = 28,
            Padding = new Thickness(0),
            Content = DemoUi.Glyph(ArcIcons.Ellipsis, 16)
        };
        AutomationProperties.SetName(trigger, $"Actions for {customer.Name}");
        var menu = new DropdownMenu { Trigger = trigger, Alignment = MenuAlignment.End, HorizontalAlignment = HorizontalAlignment.Center };
        menu.Items.Add(new MenuEntryLabel { Text = "Owner" });
        foreach (var owner in DemoData.CustomerOwners)
        {
            var name = DemoData.Person(owner).Name;
            var entry = new MenuEntry
            {
                Header = name,
                ToggleType = MenuEntryToggleType.Radio,
                GroupName = "owner",
                IsChecked = customer.Owner == owner,
                Icon = new Avatar { DisplayName = name, Source = DemoPhotos.For(name), Size = AvatarSize.Small }
            };
            entry.Click += (_, _) => Reassign([customer], owner);
            menu.Items.Add(entry);
        }
        menu.Items.Add(new MenuEntrySeparator());
        if (customer.Status == CustomerStatus.Trial)
        {
            var extend = new MenuEntry { Header = "Extend trial by 14 days", Icon = s_calendarPlus };
            extend.Click += (_, _) => ExtendTrial(customer);
            menu.Items.Add(extend);
        }
        var archive = new MenuEntry { Header = "Archive", Icon = ArcIcons.Archive, IsDestructive = true };
        archive.Click += (_, _) => Archive([customer]);
        menu.Items.Add(archive);
        return menu;
    }

    private static readonly Geometry s_userRound = ArcIcons.Parse("M7 8a5 5 0 1 0 10 0a5 5 0 1 0 -10 0z M20 21a8 8 0 0 0-16 0");

    private static readonly Geometry s_calendarPlus =
        ArcIcons.Parse("M16 18h6 M16 2v3 M19 15v6 M21 11.5V5a2 2 0 00-2-2H5a2 2 0 00-2 2v14a2 2 0 002 2h8.3 M3 9h18 M8 2v3");

    #endregion

    #region Selection and actions

    private void TogglePage()
    {
        var page = CurrentPage.Select(c => c.Id).ToList();
        if (page.Count > 0 && page.All(_selected.Contains))
        {
            _selected.ExceptWith(page);
        }
        else
        {
            _selected.UnionWith(page);
        }
        SyncSelection();
    }

    private void SelectAllInView()
    {
        _selected.UnionWith(_visible.Select(c => c.Id));
        SyncSelection();
    }

    /// <summary>Brings the checkboxes, row tints and the floating bar in line with the selection, without rebuilding rows.</summary>
    private void SyncSelection()
    {
        foreach (var part in _rowParts)
        {
            var selected = _selected.Contains(part.Customer.Id);
            part.Check.IsChecked = selected;
            part.Tint.IsVisible = selected;
        }
        var page = CurrentPage.Select(c => c.Id).ToList();
        var onPage = page.Count(_selected.Contains);
        _pageCheck.IsEnabled = page.Count > 0;
        _pageCheck.IsChecked = onPage == 0 ? false : onPage == page.Count ? true : null;
        AutomationProperties.SetName(_pageCheck, onPage == page.Count && onPage > 0 ? "Deselect customers on this page" : "Select customers on this page");

        var count = _selected.Count;
        var shown = count > 0;
        if (shown)
        {
            _dockCount.Text = $"{count} selected";
            _selectAll.IsVisible = onPage == page.Count && _visible.Count > count && Bounds.Width >= 560;
            _selectAllText.Text = $"Select all {_visible.Count}";
        }
        _dockSpace.IsVisible = shown;
        _dock.IsHitTestVisible = shown;
        _dock.Opacity = shown ? 1 : 0;
        _dock.RenderTransform = TransformOperations.Parse(shown ? "translateY(0px)" : "translateY(28px)");
    }

    /// <summary>
    /// Sets the floating bar over the bottom of the table: centred on it, at the bottom of the viewport while the table runs
    /// past it, and inside the room the table keeps free at its own bottom otherwise (uiarc.dev's sticky dock).
    /// </summary>
    public void PlaceDock(Visual layer, double layerHeight)
    {
        if (this.TranslatePoint(default, layer) is not { } origin)
        {
            return;
        }
        _dock.MaxWidth = Math.Max(0, Bounds.Width - 32);
        var x = origin.X + (Bounds.Width - _dockHost.Bounds.Width) / 2;
        var lift = Math.Max(0, layerHeight - (origin.Y + Bounds.Height));
        _dockHost.RenderTransform = new TranslateTransform(Math.Max(0, x), -lift);
        _dockHost.IsVisible = origin.Y < layerHeight - 60;
    }

    private void Export()
    {
        var count = _selected.Count;
        _exportContent.Content = DockLabel(ArcIcons.Check, "Exported");
        _exportTimer.Stop();
        _exportTimer.Start();
        AutomationProperties.SetName(_exportContent, $"Exported {count} customer{(count == 1 ? string.Empty : "s")} to CSV");
    }

    private void AssignSelected(string owner)
    {
        var customers = _all.Where(c => _selected.Contains(c.Id)).ToList();
        foreach (var customer in customers)
        {
            customer.Owner = owner;
        }
        _selected.Clear();
        Refresh(RowEntrance.None);
        Notify($"Assigned {customers.Count} customer{(customers.Count == 1 ? string.Empty : "s")} to {DemoData.Person(owner).Name}");
    }

    private void ArchiveSelected() => Archive(_all.Where(c => _selected.Contains(c.Id)).ToList());

    private void Reassign(IReadOnlyList<DemoCustomer> customers, string owner)
    {
        var changed = customers.Where(c => c.Owner != owner).ToList();
        if (changed.Count == 0)
        {
            return;
        }
        foreach (var customer in changed)
        {
            customer.Owner = owner;
        }
        Refresh(RowEntrance.None);
        Notify($"{changed[0].Name} is now owned by {DemoData.Person(owner).Name}");
    }

    private void ExtendTrial(DemoCustomer customer)
    {
        customer.TrialDays += 14;
        Refresh(RowEntrance.None);
        var date = DemoData.Now.Date.AddDays(customer.TrialDays).ToString("MMM d", s_culture);
        Notify($"Extended {customer.Name}’s trial to {date}");
    }

    private void Archive(IReadOnlyList<DemoCustomer> customers)
    {
        if (customers.Count == 0)
        {
            return;
        }
        var ids = customers.Select(c => c.Id).ToList();
        _archived.UnionWith(ids);
        _selected.ExceptWith(ids);
        Refresh(RowEntrance.None);
        var title = customers.Count == 1 ? $"Archived {customers[0].Name}" : $"Archived {customers.Count} customers";
        _host.Toast(new ToastOptions
        {
            Kind = ToastKind.Success,
            Title = title,
            Duration = TimeSpan.FromSeconds(6),
            Action = new ToastAction("Undo", toast =>
            {
                _archived.ExceptWith(ids);
                Refresh(RowEntrance.Fade);
                _host.UpdateToast(toast, new ToastUpdate
                {
                    Title = $"Restored {ids.Count} customer{(ids.Count == 1 ? string.Empty : "s")}",
                    RemoveAction = true,
                    Duration = TimeSpan.FromSeconds(4.2)
                });
            })
        });
    }

    private void Notify(string title) =>
        _host.Toast(new ToastOptions { Kind = ToastKind.Success, Title = title, Duration = TimeSpan.FromSeconds(4.2) });

    #endregion

    #region Formatting

    private static string Money(int value) => "$" + value.ToString("N0", s_culture);

    /// <summary>The first letters of the first two words that start with a letter ("Marlow &amp; Finch" gives "MF").</summary>
    private static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(word => char.IsLetter(word[0])).Take(2).Select(word => word[0]));

    /// <summary>"25 min ago", "7 hours ago", "Yesterday" or the calendar date, counting back from Sep 22.</summary>
    private static string LastSeen(double hours)
    {
        if (hours < 1)
        {
            return $"{Math.Max(1, (int)Math.Round(hours * 60, MidpointRounding.AwayFromZero))} min ago";
        }
        if (hours < 24)
        {
            var whole = (int)Math.Round(hours, MidpointRounding.AwayFromZero);
            return whole == 1 ? "1 hour ago" : $"{whole} hours ago";
        }
        if (hours < 48)
        {
            return "Yesterday";
        }
        return DemoData.Now.Date.AddDays(-Math.Floor(hours / 24)).ToString("MMM d", s_culture);
    }

    #endregion
}
