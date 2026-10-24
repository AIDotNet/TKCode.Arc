using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A settings command palette: one search field that finds any setting and changes it in place. Toggles flip, choices
/// cycle through their options and actions run, all without leaving the keyboard; focus stays in the search field
/// while the arrow keys move a highlight that glides between rows. A notice in the footer confirms each change.
/// <para>
/// Write the settings inline (<see cref="SettingsCommandToggle"/>, <see cref="SettingsCommandChoice"/>,
/// <see cref="SettingsCommandAction"/>) or bind <see cref="Items"/>. Their values are styled properties, so
/// <c>IsChecked</c> and <c>SelectedOption</c> bind two-way and the rows follow changes made elsewhere.
/// </para>
/// </summary>
[TemplatePart(SearchBoxPart, typeof(TextBox))]
[TemplatePart(ResultsPart, typeof(Panel))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
[TemplatePart(NoticePart, typeof(ArcContentSwap))]
[PseudoClasses(EmptyPseudoClass, NoResultsPseudoClass, ToggleActivePseudoClass, ChoiceActivePseudoClass, ActionActivePseudoClass)]
public class SettingsCommand : TemplatedControl
{
    public const string SearchBoxPart = "PART_SearchBox";
    public const string ResultsPart = "PART_Results";
    public const string ScrollPart = "PART_Scroll";
    public const string HighlightPart = "PART_Highlight";
    public const string NoticePart = "PART_Notice";

    /// <summary>Set while no setting is on show.</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Set while the query matches nothing (the no-results message shows).</summary>
    public const string NoResultsPseudoClass = ":no-results";

    /// <summary>Set while the active row is a toggle (the footer reads "Toggle").</summary>
    public const string ToggleActivePseudoClass = ":toggle-active";

    /// <summary>Set while the active row is a choice (the footer reads "Change").</summary>
    public const string ChoiceActivePseudoClass = ":choice-active";

    /// <summary>Set while the active row is an action (the footer reads "Open").</summary>
    public const string ActionActivePseudoClass = ":action-active";

    private static readonly TimeSpan NoticeDuration = TimeSpan.FromSeconds(2.5);
    private const int PageStep = 5;

    #region Properties and events

    /// <summary>The settings to search, in the order they are listed when the query is empty.</summary>
    public static readonly DirectProperty<SettingsCommand, IList<SettingsCommandItem>> ItemsProperty =
        AvaloniaProperty.RegisterDirect<SettingsCommand, IList<SettingsCommandItem>>(nameof(Items), o => o.Items, (o, v) => o.Items = v);

    /// <summary>The text in the search field (two-way).</summary>
    public static readonly StyledProperty<string> QueryProperty =
        AvaloniaProperty.Register<SettingsCommand, string>(nameof(Query), string.Empty, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, value) => value ?? string.Empty);

    /// <summary>The highlighted setting that Enter acts on, or null when nothing is on show.</summary>
    public static readonly DirectProperty<SettingsCommand, SettingsCommandItem?> ActiveItemProperty =
        AvaloniaProperty.RegisterDirect<SettingsCommand, SettingsCommandItem?>(nameof(ActiveItem), o => o.ActiveItem);

    /// <summary>The tallest the result list gets before it scrolls.</summary>
    public static readonly StyledProperty<double> ResultsMaxHeightProperty =
        AvaloniaProperty.Register<SettingsCommand, double>(nameof(ResultsMaxHeight), 360);

    /// <summary>Whether the palette draws its own panel (surface, border, radius and floating shadow). Turn it off inside a Dialog or another surface.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<SettingsCommand, bool>(nameof(IsFramed), true);

    /// <summary>The built-in text; replace the whole object to localize the palette.</summary>
    public static readonly StyledProperty<SettingsCommandLabels> LabelsProperty =
        AvaloniaProperty.Register<SettingsCommand, SettingsCommandLabels>(nameof(Labels), new SettingsCommandLabels(),
            coerce: (_, value) => value ?? new SettingsCommandLabels());

    /// <summary>Raised when the user changes a toggle or a choice (not when the app sets the value).</summary>
    public static readonly RoutedEvent<SettingsCommandChangedEventArgs> SettingChangedEvent =
        RoutedEvent.Register<SettingsCommand, SettingsCommandChangedEventArgs>(nameof(SettingChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when an action is invoked with Enter or a click, after its command ran.</summary>
    public static readonly RoutedEvent<SettingsCommandItemEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<SettingsCommand, SettingsCommandItemEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    /// <summary>
    /// Raised when Escape is pressed with an empty query: close the palette. Mark it handled so a surrounding Dialog does
    /// not handle the same Escape again.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> CloseRequestedEvent =
        RoutedEvent.Register<SettingsCommand, RoutedEventArgs>(nameof(CloseRequested), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ItemsProperty"/>
    [Content]
    public IList<SettingsCommandItem> Items
    {
        get => _items;
        set
        {
            var owns = value is null;
            value ??= new AvaloniaList<SettingsCommandItem>();
            if (SetAndRaise(ItemsProperty, ref _items, value))
            {
                _ownsItems = owns;
                WatchItems();
                SyncRows();
                Refilter(resetActive: false);
            }
        }
    }

    /// <inheritdoc cref="QueryProperty"/>
    public string Query
    {
        get => GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    /// <inheritdoc cref="ActiveItemProperty"/>
    public SettingsCommandItem? ActiveItem
    {
        get => _activeItem;
        private set => SetAndRaise(ActiveItemProperty, ref _activeItem, value);
    }

    /// <inheritdoc cref="ResultsMaxHeightProperty"/>
    public double ResultsMaxHeight
    {
        get => GetValue(ResultsMaxHeightProperty);
        set => SetValue(ResultsMaxHeightProperty, value);
    }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed
    {
        get => GetValue(IsFramedProperty);
        set => SetValue(IsFramedProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public SettingsCommandLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <inheritdoc cref="SettingChangedEvent"/>
    public event EventHandler<SettingsCommandChangedEventArgs>? SettingChanged
    {
        add => AddHandler(SettingChangedEvent, value);
        remove => RemoveHandler(SettingChangedEvent, value);
    }

    /// <inheritdoc cref="ItemInvokedEvent"/>
    public event EventHandler<SettingsCommandItemEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    /// <inheritdoc cref="CloseRequestedEvent"/>
    public event EventHandler<RoutedEventArgs>? CloseRequested
    {
        add => AddHandler(CloseRequestedEvent, value);
        remove => RemoveHandler(CloseRequestedEvent, value);
    }

    #endregion

    private readonly Dictionary<SettingsCommandItem, SettingsCommandRow> _rows = new();
    private readonly Dictionary<string, SettingsCommandGroupHeader> _headers = new(StringComparer.Ordinal);
    private readonly List<SettingsCommandRow> _visibleRows = new();
    private readonly List<Control> _order = new();
    private readonly List<Match> _matches = new();
    private readonly List<GroupBucket> _groups = new();
    private readonly PausableTimer _noticeTimer;
    private IList<SettingsCommandItem> _items;
    private bool _ownsItems = true;
    private INotifyCollectionChanged? _watchedItems;
    private readonly HashSet<SettingsCommandItem> _followed = new();
    // The items, and a list the host hands in, can outlive the palette (a view model's, a static list): the palette
    // follows them only while it is in a visual tree and reads them again when it comes back. Its own list is always
    // followed.
    private bool _watching;
    private SettingsCommandItem? _activeItem;
    private SettingsCommandRow? _active;
    private TextBox? _searchBox;
    private SettingsCommandResults? _results;
    private ScrollViewer? _scroll;
    private GlidingIndicator? _highlight;
    private ArcContentSwap? _notice;
    private TextBlock? _emptyTitle;
    private INameScope? _scope;
    private Rect? _highlightTarget;
    private bool _glidePending;
    private bool _scrollPending;
    private Point? _lastPointer;
    private int _stamp;
    private string[] _tokens = [];

    static SettingsCommand()
    {
        FocusableProperty.OverrideDefaultValue<SettingsCommand>(false);
    }

    /// <summary>Creates an empty palette.</summary>
    public SettingsCommand()
    {
        _items = new AvaloniaList<SettingsCommandItem>();
        WatchItems();
        _noticeTimer = new PausableTimer(NoticeDuration, HideNotice);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    /// <summary>The result rows on show, top to bottom (tests).</summary>
    internal IReadOnlyList<SettingsCommandRow> VisibleRows => _visibleRows;

    /// <summary>The row of <paramref name="item"/> (tests).</summary>
    internal SettingsCommandRow? RowOf(SettingsCommandItem item) => _rows.GetValueOrDefault(item);

    /// <summary>The current change notice, or null (tests).</summary>
    internal string? NoticeText => _notice?.Content as string;

    /// <summary>The search field (tests).</summary>
    internal TextBox? SearchBox => _searchBox;

    /// <summary>The headers and rows on show, top to bottom (tests).</summary>
    internal IReadOnlyList<Control> ResultOrder => (IReadOnlyList<Control>?)_results?.Order ?? _order;

    /// <summary>Moves keyboard focus to the search field.</summary>
    public void FocusSearch()
    {
        _searchBox?.Focus();
    }

    /// <summary>Clears the query, which lists every setting again.</summary>
    public void Clear()
    {
        SetCurrentValue(QueryProperty, string.Empty);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_results is not null)
        {
            _results.Arranged = null;
            _results.PointerMoved -= OnResultsPointerMoved;
            foreach (var row in _rows.Values)
            {
                _results.Children.Remove(row);
            }
            foreach (var header in _headers.Values)
            {
                _results.Children.Remove(header);
            }
        }
        base.OnApplyTemplate(e);
        _searchBox = e.NameScope.Find<TextBox>(SearchBoxPart);
        _results = e.NameScope.Find<SettingsCommandResults>(ResultsPart);
        _scroll = e.NameScope.Find<ScrollViewer>(ScrollPart);
        _highlight = e.NameScope.Find<GlidingIndicator>(HighlightPart);
        _notice = e.NameScope.Find<ArcContentSwap>(NoticePart);
        _emptyTitle = e.NameScope.Find<TextBlock>("PART_EmptyTitle");
        _scope = e.NameScope;
        _highlightTarget = null;
        if (_results is not null)
        {
            _results.Arranged = OnResultsArranged;
            _results.PointerMoved += OnResultsPointerMoved;
            foreach (var header in _headers.Values)
            {
                _results.Children.Add(header);
            }
            foreach (var row in _rows.Values)
            {
                _results.Children.Add(row);
            }
        }
        ApplyLabels();
        Refilter(resetActive: false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == QueryProperty)
        {
            Refilter(resetActive: true);
            _scroll?.SetCurrentValue(ScrollViewer.OffsetProperty, default(Vector));
        }
        else if (change.Property == LabelsProperty)
        {
            foreach (var row in _rows.Values)
            {
                row.ApplyLabels();
            }
            ApplyLabels();
            Refilter(resetActive: false);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watching = true;
        WatchItems();
        // What changed while the palette was away: SyncRows follows the list and the items again, every row reads its item.
        SyncRows();
        foreach (var row in _rows.Values)
        {
            row.Refresh(null);
        }
        Refilter(resetActive: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A notice belongs to the moment it was shown: a palette that comes back (a dialog reopened) starts without one.
        _noticeTimer.Stop();
        HideNotice();
        _watching = false;
        WatchItems();
        foreach (var item in _followed)
        {
            item.Changed -= OnItemChanged;
        }
        _followed.Clear();
    }

    private void WatchItems()
    {
        var wanted = _ownsItems || _watching ? _items as INotifyCollectionChanged : null;
        if (ReferenceEquals(wanted, _watchedItems))
        {
            return;
        }
        if (_watchedItems is not null)
        {
            _watchedItems.CollectionChanged -= OnItemsCollectionChanged;
        }
        _watchedItems = wanted;
        if (_watchedItems is not null)
        {
            _watchedItems.CollectionChanged += OnItemsCollectionChanged;
        }
    }

    #region Items and rows

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncRows();
        Refilter(resetActive: false);
    }

    /// <summary>Creates a row for every new item and drops the rows of removed ones.</summary>
    private void SyncRows()
    {
        if (_rows.Count > 0)
        {
            var present = new HashSet<SettingsCommandItem>(_items);
            List<SettingsCommandItem>? removed = null;
            foreach (var item in _rows.Keys)
            {
                if (!present.Contains(item))
                {
                    (removed ??= new List<SettingsCommandItem>()).Add(item);
                }
            }
            if (removed is not null)
            {
                foreach (var item in removed)
                {
                    var row = _rows[item];
                    if (_followed.Remove(item))
                    {
                        item.Changed -= OnItemChanged;
                    }
                    _results?.Children.Remove(row);
                    _rows.Remove(item);
                    if (ReferenceEquals(row, _active))
                    {
                        _active = null;
                    }
                }
            }
        }
        foreach (var item in _items)
        {
            if (item is null || _rows.ContainsKey(item))
            {
                continue;
            }
            var row = new SettingsCommandRow(this, item) { IsVisible = false };
            _rows.Add(item, row);
            _results?.Children.Add(row);
        }
        if (_watching)
        {
            foreach (var item in _rows.Keys)
            {
                if (_followed.Add(item))
                {
                    item.Changed += OnItemChanged;
                }
            }
        }
    }

    private void OnItemChanged(SettingsCommandItem item, AvaloniaProperty? property)
    {
        if (!_rows.TryGetValue(item, out var row))
        {
            return;
        }
        row.Refresh(property);
        if (property == SettingsCommandItem.HeaderProperty || property == SettingsCommandItem.DescriptionProperty ||
            property == SettingsCommandItem.GroupProperty || property == SettingsCommandItem.KeywordsProperty ||
            property == SettingsCommandItem.IsEnabledProperty)
        {
            Refilter(resetActive: false);
        }
        else if (ReferenceEquals(row, _active))
        {
            UpdateActiveHelp();
        }
    }

    #endregion

    #region Search

    private readonly record struct Match(SettingsCommandRow Row, int Rank, int Index);

    private sealed class GroupBucket
    {
        public string Key = string.Empty;
        public int BestRank;
        public int FirstIndex;
        public readonly List<Match> Matches = new();
    }

    /// <summary>
    /// Filters and orders the rows for the current query: every query word must appear in the header, description, group
    /// or keywords; rows rank by how the header matches and keep their declared order on ties; groups follow their best row.
    /// </summary>
    private void Refilter(bool resetActive)
    {
        var query = (Query ?? string.Empty).Trim();
        _tokens = SettingsCommandMatcher.Tokenize(query);
        var stamp = ++_stamp;
        _matches.Clear();
        var grouped = false;
        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            if (item is null || !_rows.TryGetValue(item, out var row) || row.Stamp == stamp)
            {
                continue;
            }
            row.Stamp = stamp;
            grouped |= !string.IsNullOrEmpty(item.Group);
            if (SettingsCommandMatcher.Matches(item, _tokens))
            {
                _matches.Add(new Match(row, SettingsCommandMatcher.Rank(item, query, _tokens), i));
            }
        }

        // Group the matches; groups keep the order of their first item, then sort by their best rank.
        foreach (var bucket in _groups)
        {
            bucket.Matches.Clear();
        }
        var used = 0;
        foreach (var match in _matches)
        {
            var key = grouped ? match.Row.Item.Group ?? string.Empty : string.Empty;
            GroupBucket? bucket = null;
            for (var g = 0; g < used; g++)
            {
                if (_groups[g].Key == key)
                {
                    bucket = _groups[g];
                    break;
                }
            }
            if (bucket is null)
            {
                if (used == _groups.Count)
                {
                    _groups.Add(new GroupBucket());
                }
                bucket = _groups[used++];
                bucket.Key = key;
                bucket.BestRank = match.Rank;
                bucket.FirstIndex = match.Index;
            }
            bucket.BestRank = Math.Min(bucket.BestRank, match.Rank);
            bucket.Matches.Add(match);
        }
        var buckets = _groups.GetRange(0, used);
        buckets.Sort(static (a, b) => a.BestRank != b.BestRank ? a.BestRank.CompareTo(b.BestRank) : a.FirstIndex.CompareTo(b.FirstIndex));

        _order.Clear();
        _visibleRows.Clear();
        foreach (var bucket in buckets)
        {
            bucket.Matches.Sort(static (a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : a.Index.CompareTo(b.Index));
            SettingsCommandGroupHeader? header = null;
            if (grouped)
            {
                header = HeaderFor(bucket.Key);
                header.ShownStamp = stamp;
                _order.Add(header);
            }
            var first = true;
            foreach (var match in bucket.Matches)
            {
                match.Row.ShownStamp = stamp;
                match.Row.LeadingHeader = first ? header : null;
                first = false;
                match.Row.SetTokens(_tokens);
                _order.Add(match.Row);
                _visibleRows.Add(match.Row);
            }
        }

        foreach (var row in _rows.Values)
        {
            row.IsVisible = row.ShownStamp == stamp;
        }
        foreach (var header in _headers.Values)
        {
            header.IsVisible = header.ShownStamp == stamp;
        }
        _results?.SetOrder(_order);

        var empty = _visibleRows.Count == 0;
        PseudoClasses.Set(EmptyPseudoClass, empty);
        PseudoClasses.Set(NoResultsPseudoClass, empty && query.Length > 0);
        if (_emptyTitle is not null)
        {
            _emptyTitle.Text = query.Length > 0 ? Labels.NoResults(query) : null;
        }

        if (resetActive || _active is null || !_active.IsVisible || !_active.Item.IsEnabled)
        {
            SetActive(FirstEnabled(), glide: false, scroll: false);
        }
        else
        {
            // Same active row; it may have moved.
            _glidePending = false;
            UpdateHints();
            UpdateActiveHelp();
        }
    }

    private SettingsCommandGroupHeader HeaderFor(string key)
    {
        if (!_headers.TryGetValue(key, out var header))
        {
            header = new SettingsCommandGroupHeader(key) { IsVisible = false };
            _headers.Add(key, header);
            _results?.Children.Add(header);
        }
        header.Text = key.Length == 0 ? Labels.Ungrouped : key;
        return header;
    }

    private SettingsCommandRow? FirstEnabled()
    {
        foreach (var row in _visibleRows)
        {
            if (row.Item.IsEnabled)
            {
                return row;
            }
        }
        return null;
    }

    #endregion

    #region Active row, highlight and scrolling

    private void SetActive(SettingsCommandRow? row, bool glide, bool scroll)
    {
        if (!ReferenceEquals(row, _active))
        {
            _active?.SetActive(false);
            _active = row;
            row?.SetActive(true);
            ActiveItem = row?.Item;
            UpdateHints();
            UpdateActiveHelp();
            MoveHighlight(glide);
        }
        else if (row is null)
        {
            MoveHighlight(glide);
        }
        if (scroll)
        {
            ScrollActiveIntoView();
        }
    }

    private bool IsLayoutCurrent => _results is { IsMeasureValid: true, IsArrangeValid: true };

    private void MoveHighlight(bool glide)
    {
        if (_highlight is null)
        {
            return;
        }
        if (_active is null)
        {
            _highlightTarget = null;
            _glidePending = false;
            _highlight.Hide(glide);
            return;
        }
        if (!IsLayoutCurrent)
        {
            // The rows are about to move: place the highlight after the next arrange.
            _glidePending = glide;
            return;
        }
        var target = _active.Bounds;
        _highlightTarget = target;
        _highlight.MoveTo(target, glide, ArcSpring.Snappy);
    }

    private void OnResultsArranged()
    {
        if (_highlight is null)
        {
            return;
        }
        if (_active is null || !_active.IsVisible)
        {
            if (_highlightTarget is not null)
            {
                _highlightTarget = null;
                _highlight.Hide(false);
            }
        }
        else
        {
            var target = _active.Bounds;
            if (_highlightTarget != target)
            {
                _highlightTarget = target;
                _highlight.MoveTo(target, _glidePending, ArcSpring.Snappy);
            }
        }
        _glidePending = false;
        if (_scrollPending)
        {
            _scrollPending = false;
            Dispatcher.UIThread.Post(ScrollActiveIntoView, DispatcherPriority.Render);
        }
    }

    /// <summary>Scrolls the result list so the active row (and its group title when it is the first of its group) shows.</summary>
    private void ScrollActiveIntoView()
    {
        if (_active is not { } row || _scroll is null || _results is null)
        {
            return;
        }
        if (!IsLayoutCurrent)
        {
            _scrollPending = true;
            return;
        }
        var origin = _results.Bounds.Y;
        var viewport = _scroll.Viewport.Height;
        var extent = _scroll.Extent.Height;
        if (viewport <= 0 || extent <= viewport)
        {
            return;
        }
        var index = _visibleRows.IndexOf(row);
        // Keep some room past the edge fades, and reach the very ends for the first and last rows.
        const double room = 16;
        var top = (row.LeadingHeader is { IsVisible: true } header ? header.Bounds.Y : row.Bounds.Y) + origin;
        top = index == 0 ? 0 : top - room;
        var bottom = index == _visibleRows.Count - 1 ? extent : row.Bounds.Bottom + origin + room;
        var offset = _scroll.Offset.Y;
        double target;
        if (top < offset)
        {
            target = top;
        }
        else if (bottom > offset + viewport)
        {
            target = bottom - viewport;
        }
        else
        {
            return;
        }
        target = Math.Clamp(target, 0, extent - viewport);
        if (_scroll is ScrollArea area)
        {
            area.ScrollToOffset(new Vector(_scroll.Offset.X, target));
        }
        else
        {
            _scroll.Offset = new Vector(_scroll.Offset.X, target);
        }
    }

    private void OnResultsPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch || _results is null)
        {
            return;
        }
        // Only real pointer movement moves the highlight: rows scrolling under a resting pointer do not.
        var root = TopLevel.GetTopLevel(this);
        var screen = root is null ? e.GetPosition(this) : e.GetPosition(root);
        if (_lastPointer == screen)
        {
            return;
        }
        _lastPointer = screen;
        var point = e.GetPosition(_results);
        foreach (var row in _visibleRows)
        {
            if (row.Bounds.Contains(point))
            {
                if (row.Item.IsEnabled && !ReferenceEquals(row, _active))
                {
                    SetActive(row, glide: true, scroll: false);
                }
                return;
            }
        }
    }

    internal void OnRowPressed(SettingsCommandRow row)
    {
        if (row.Item.IsEnabled && !ReferenceEquals(row, _active))
        {
            SetActive(row, glide: true, scroll: false);
        }
    }

    /// <summary>
    /// Nothing in the palette but the search field takes focus: a press on a row, an arrow or the footer keeps (or puts)
    /// the keyboard in the search field.
    /// </summary>
    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_searchBox is { IsFocused: false })
        {
            FocusSearch();
        }
    }

    private void UpdateHints()
    {
        PseudoClasses.Set(ToggleActivePseudoClass, _active?.Item is SettingsCommandToggle);
        PseudoClasses.Set(ChoiceActivePseudoClass, _active?.Item is SettingsCommandChoice);
        PseudoClasses.Set(ActionActivePseudoClass, _active?.Item is SettingsCommandAction);
    }

    /// <summary>The search field's help text follows the active row ("Dark mode, on").</summary>
    private void UpdateActiveHelp()
    {
        if (_searchBox is null)
        {
            return;
        }
        string? help = null;
        if (_active is { } row)
        {
            var header = row.Item.Header ?? string.Empty;
            var state = row.StateText(Labels);
            help = string.IsNullOrEmpty(state) ? header : Labels.RowState(header, state);
        }
        AutomationProperties.SetHelpText(_searchBox, help);
    }

    #endregion

    #region Keyboard and actions

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }
        var modifiers = e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta);
        switch (e.Key)
        {
            case Key.Down:
                Move(1);
                e.Handled = true;
                break;
            case Key.Up:
                Move(-1);
                e.Handled = true;
                break;
            case Key.PageDown:
                Move(PageStep);
                e.Handled = true;
                break;
            case Key.PageUp:
                Move(-PageStep);
                e.Handled = true;
                break;
            case Key.Enter when modifiers == KeyModifiers.None:
                if (_active is { } active)
                {
                    InvokeRow(active);
                    e.Handled = true;
                }
                break;
            case Key.Right or Key.Left when e.KeyModifiers == KeyModifiers.None && _active is { Item: SettingsCommandChoice choice } choiceRow:
                StepChoice(choiceRow, choice, e.Key == Key.Right ? 1 : -1, wrap: false);
                e.Handled = true;
                break;
            case Key.Escape when e.KeyModifiers == KeyModifiers.None:
                if (!string.IsNullOrEmpty(Query))
                {
                    Clear();
                    e.Handled = true;
                }
                else
                {
                    var args = new RoutedEventArgs(CloseRequestedEvent, this);
                    RaiseEvent(args);
                    e.Handled = args.Handled;
                }
                break;
        }
    }

    /// <summary>Moves the highlight <paramref name="delta"/> enabled rows down (negative: up), without wrapping.</summary>
    private void Move(int delta)
    {
        var current = _active is null ? -1 : _visibleRows.IndexOf(_active);
        if (current < 0)
        {
            SetActive(delta > 0 ? FirstEnabled() : LastEnabled(), glide: true, scroll: true);
            return;
        }
        var step = Math.Sign(delta);
        var remaining = Math.Abs(delta);
        var target = current;
        for (var i = current + step; i >= 0 && i < _visibleRows.Count && remaining > 0; i += step)
        {
            if (_visibleRows[i].Item.IsEnabled)
            {
                target = i;
                remaining--;
            }
        }
        SetActive(_visibleRows[target], glide: true, scroll: true);
    }

    private SettingsCommandRow? LastEnabled()
    {
        for (var i = _visibleRows.Count - 1; i >= 0; i--)
        {
            if (_visibleRows[i].Item.IsEnabled)
            {
                return _visibleRows[i];
            }
        }
        return null;
    }

    /// <summary>Acts on a row like Enter: flips a toggle, moves a choice to its next option, runs an action.</summary>
    internal void InvokeRow(SettingsCommandRow row)
    {
        if (!row.Item.IsEnabled)
        {
            return;
        }
        switch (row.Item)
        {
            case SettingsCommandToggle toggle:
                var value = !toggle.IsChecked;
                toggle.SetCurrentValue(SettingsCommandToggle.IsCheckedProperty, value);
                var header = toggle.Header ?? string.Empty;
                ShowNotice(value ? Labels.TurnedOn(header) : Labels.TurnedOff(header));
                RaiseEvent(new SettingsCommandChangedEventArgs(SettingChangedEvent, toggle, value));
                break;
            case SettingsCommandChoice choice:
                StepChoice(row, choice, 1, wrap: true);
                break;
            case SettingsCommandAction action:
                var parameter = action.CommandParameter;
                if (action.Command is { } command)
                {
                    if (!command.CanExecute(parameter))
                    {
                        return;
                    }
                    command.Execute(parameter);
                }
                RaiseEvent(new SettingsCommandItemEventArgs(ItemInvokedEvent, action));
                break;
        }
    }

    /// <summary>Moves a choice <paramref name="delta"/> options on; at the ends it wraps or stays.</summary>
    internal void StepChoice(SettingsCommandRow row, SettingsCommandChoice choice, int delta, bool wrap)
    {
        if (!choice.IsEnabled)
        {
            return;
        }
        if (!ReferenceEquals(row, _active) && row.IsVisible)
        {
            SetActive(row, glide: true, scroll: false);
        }
        var options = choice.Options;
        if (options.Count == 0)
        {
            return;
        }
        var index = choice.SelectedIndex;
        int next;
        if (index < 0)
        {
            next = delta > 0 ? 0 : options.Count - 1;
        }
        else if (wrap)
        {
            next = ((index + delta) % options.Count + options.Count) % options.Count;
        }
        else
        {
            next = Math.Clamp(index + delta, 0, options.Count - 1);
        }
        if (next == index)
        {
            return;
        }
        var value = options[next];
        row.PendingDirection = delta;
        choice.SetCurrentValue(SettingsCommandChoice.SelectedOptionProperty, value);
        ShowNotice(Labels.ChangedTo(choice.Header ?? string.Empty, value));
        RaiseEvent(new SettingsCommandChangedEventArgs(SettingChangedEvent, choice, value));
    }

    #endregion

    #region Notice and labels

    private void ShowNotice(string text)
    {
        if (_notice is null)
        {
            return;
        }
        _notice.Content = text;
        AutomationProperties.SetName(_notice, text);
        _noticeTimer.Start();
    }

    private void HideNotice()
    {
        if (_notice is null)
        {
            return;
        }
        _notice.Content = null;
        AutomationProperties.SetName(_notice, null);
    }

    private void ApplyLabels()
    {
        var labels = Labels;
        if (_searchBox is not null)
        {
            _searchBox.PlaceholderText = labels.Placeholder;
            AutomationProperties.SetName(_searchBox, labels.SearchName);
        }
        if (_results is not null)
        {
            AutomationProperties.SetName(_results, labels.ResultsName);
        }
        if (_scope is not { } scope)
        {
            return;
        }
        SetText(scope, "PART_EmptyHint", labels.NoResultsHint);
        SetText(scope, "PART_NavigateText", labels.Navigate);
        SetText(scope, "PART_ToggleText", labels.Toggle);
        SetText(scope, "PART_ChangeText", labels.Change);
        SetText(scope, "PART_OpenText", labels.Open);
        SetText(scope, "PART_CloseText", labels.Close);
        if (scope.Find<SettingsCommandKeyCap>("PART_EscapeCap") is { } escape)
        {
            escape.Text = labels.EscapeKey;
        }
        if (scope.Find<SettingsCommandKeyCap>("PART_CloseCap") is { } close)
        {
            close.Text = labels.EscapeKey;
        }
    }

    private static void SetText(INameScope scope, string name, string text)
    {
        if (scope.Find<TextBlock>(name) is { } block)
        {
            block.Text = text;
        }
    }

    #endregion
}
