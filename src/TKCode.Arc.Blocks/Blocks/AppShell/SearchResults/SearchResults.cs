using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// A search results page for a workspace (an original TKCode design, after Algolia InstantSearch's search box, hits,
/// highlight, refinement list and stats). A large search field, facet tabs for the kinds of content with live counts
/// that roll as you type, a stats line, and the results: grouped by kind under All (a few per group with "See all"),
/// or one kind at a time with "Show more". Every term of the query must match; matches are marked in titles and in a
/// snippet window cut around the first match. One cursor block glides between the results: ArrowDown leaves the
/// field for the list, arrows, Home, End and the page keys move it, Enter opens a result. Set
/// <see cref="IsFilteringEnabled"/> to false when a server searches (counts and highlights still follow the query),
/// and <see cref="IsLoading"/> while it does.
/// </summary>
[TemplatePart(SearchFieldPart, typeof(SearchField))]
[TemplatePart(FacetScrollPart, typeof(ScrollViewer))]
[TemplatePart(FacetsPart, typeof(StackPanel))]
[TemplatePart(FacetSelectionPart, typeof(GlidingIndicator))]
[TemplatePart(StatsPart, typeof(ArcContentSwap))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[TemplatePart(ResultsScrollPart, typeof(ScrollViewer))]
[TemplatePart(ResultsAreaPart, typeof(Panel))]
[TemplatePart(ListHostPart, typeof(Panel))]
[TemplatePart(ListPart, typeof(StackPanel))]
[TemplatePart(CursorPart, typeof(Control))]
[TemplatePart(ResultsPresencePart, typeof(PresenceHost))]
[TemplatePart(LoadingPresencePart, typeof(PresenceHost))]
[TemplatePart(EmptyPresencePart, typeof(PresenceHost))]
[TemplatePart(EmptyStatePart, typeof(EmptyState))]
[PseudoClasses(NarrowPseudoClass, LoadingPseudoClass, EmptyPseudoClass)]
public class SearchResults : TemplatedControl
{
    public const string SearchFieldPart = "PART_SearchField";
    public const string FacetScrollPart = "PART_FacetScroll";
    public const string FacetsPart = "PART_Facets";
    public const string FacetSelectionPart = "PART_FacetSelection";
    public const string StatsPart = "PART_Stats";
    public const string StatusPart = "PART_Status";
    public const string ResultsScrollPart = "PART_ResultsScroll";
    public const string ResultsAreaPart = "PART_ResultsArea";
    public const string ListHostPart = "PART_ListHost";
    public const string ListPart = "PART_List";
    public const string CursorPart = "PART_Cursor";
    public const string ResultsPresencePart = "PART_ResultsPresence";
    public const string LoadingPresencePart = "PART_LoadingPresence";
    public const string EmptyPresencePart = "PART_EmptyPresence";
    public const string EmptyStatePart = "PART_Empty";

    /// <summary>Set while the control is narrower than its breakpoint (560): tighter frame, meta folded into the location line.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while <see cref="IsLoading"/> is true.</summary>
    public const string LoadingPseudoClass = ":loading";

    /// <summary>Set while the current facet has no results (the empty state shows).</summary>
    public const string EmptyPseudoClass = ":empty";

    #region 公共属性定义

    /// <summary>Items to search; when null the inline <see cref="Items"/> are used. Collection changes are followed.</summary>
    public static readonly StyledProperty<IEnumerable<SearchResultItem>?> ItemsSourceProperty =
        AvaloniaProperty.Register<SearchResults, IEnumerable<SearchResultItem>?>(nameof(ItemsSource));

    /// <summary>The text in the search field (two-way).</summary>
    public static readonly StyledProperty<string> QueryProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(Query), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Hint in the empty search field.</summary>
    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(PlaceholderText), "Search");

    /// <summary>Names the search landmark and the search field.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(Label), "Search");

    /// <summary>The current facet: a <see cref="SearchResultItem.Kind"/>, or null for All (two-way).</summary>
    public static readonly StyledProperty<string?> SelectedKindProperty =
        AvaloniaProperty.Register<SearchResults, string?>(nameof(SelectedKind), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Label of the facet that shows every kind.</summary>
    public static readonly StyledProperty<string> AllLabelProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(AllLabel), "All");

    /// <summary>Facet and group label of items without a <see cref="SearchResultItem.Kind"/>.</summary>
    public static readonly StyledProperty<string> OtherLabelProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(OtherLabel), "Other");

    /// <summary>
    /// When true (default) the items are matched and ranked here. Set it to false when a server searched: the items
    /// are shown as given and counted per kind, and the query's terms are still marked.
    /// </summary>
    public static readonly StyledProperty<bool> IsFilteringEnabledProperty =
        AvaloniaProperty.Register<SearchResults, bool>(nameof(IsFilteringEnabled), true);

    /// <summary>Shows skeleton rows in place of the results (keeping the previous results' height).</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<SearchResults, bool>(nameof(IsLoading));

    /// <summary>Results shown per group under All before "See all N"; at least 1. Default 3.</summary>
    public static readonly StyledProperty<int> GroupPreviewCountProperty =
        AvaloniaProperty.Register<SearchResults, int>(nameof(GroupPreviewCount), 3);

    /// <summary>Results first shown for a single facet; "Show more" adds as many again. Default 20.</summary>
    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<SearchResults, int>(nameof(PageSize), 20);

    /// <summary>Queries offered when nothing matches; choosing one fills the search field.</summary>
    public static readonly StyledProperty<IList<string>?> SuggestionsProperty =
        AvaloniaProperty.Register<SearchResults, IList<string>?>(nameof(Suggestions));

    /// <summary>The word before the suggestions. Default "Try".</summary>
    public static readonly StyledProperty<string> SuggestionsLabelProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(SuggestionsLabel), "Try");

    /// <summary>Stats text for several results; {0} is the count. Default "{0} results".</summary>
    public static readonly StyledProperty<string> ResultsFormatProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(ResultsFormat), "{0} results");

    /// <summary>Stats text for one result. Default "1 result".</summary>
    public static readonly StyledProperty<string> OneResultTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(OneResultText), "1 result");

    /// <summary>Stats text (and empty title without a query) when nothing matches. Default "No results".</summary>
    public static readonly StyledProperty<string> NoResultsTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(NoResultsText), "No results");

    /// <summary>Stats text while <see cref="IsLoading"/> is true. Default "Searching…".</summary>
    public static readonly StyledProperty<string> LoadingTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(LoadingText), "Searching…");

    /// <summary>Empty state title; {0} is the query. Default "No results for “{0}”".</summary>
    public static readonly StyledProperty<string> EmptyTitleFormatProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(EmptyTitleFormat), "No results for “{0}”");

    /// <summary>Empty state description when no facet has results.</summary>
    public static readonly StyledProperty<string> EmptyDescriptionProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(EmptyDescription), "Check the spelling or try fewer words.");

    /// <summary>
    /// Empty state description when the current facet is empty but another is not; {0} is the count text
    /// ("4 results") and {1} the kind with the most results. Default "{0} in {1}.".
    /// </summary>
    public static readonly StyledProperty<string> OtherFacetFormatProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(OtherFacetFormat), "{0} in {1}.");

    /// <summary>Button in the empty state that switches back to All. Default "Search all".</summary>
    public static readonly StyledProperty<string> SearchAllTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(SearchAllText), "Search all");

    /// <summary>Link under a group in All; {0} is the count and {1} the kind. Default "See all {0} {1}".</summary>
    public static readonly StyledProperty<string> SeeAllFormatProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(SeeAllFormat), "See all {0} {1}");

    /// <summary>Button under a single facet's results when more are left. Default "Show more".</summary>
    public static readonly StyledProperty<string> ShowMoreTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(ShowMoreText), "Show more");

    /// <summary>Executed with the opened <see cref="SearchResultItem"/>.</summary>
    public static readonly StyledProperty<ICommand?> InvokeCommandProperty =
        AvaloniaProperty.Register<SearchResults, ICommand?>(nameof(InvokeCommand));

    /// <summary>The result under the cursor, or null.</summary>
    public static readonly DirectProperty<SearchResults, SearchResultItem?> ActiveItemProperty =
        AvaloniaProperty.RegisterDirect<SearchResults, SearchResultItem?>(nameof(ActiveItem), o => o.ActiveItem);

    /// <summary>The number of results in the current facet for the current query.</summary>
    public static readonly DirectProperty<SearchResults, int> ResultCountProperty =
        AvaloniaProperty.RegisterDirect<SearchResults, int>(nameof(ResultCount), o => o.ResultCount);

    /// <summary>Raised when a result is opened with Enter or a click.</summary>
    public static readonly RoutedEvent<SearchResultInvokedEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<SearchResults, SearchResultInvokedEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable<SearchResultItem>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="QueryProperty"/>
    public string Query { get => GetValue(QueryProperty); set => SetValue(QueryProperty, value); }

    /// <inheritdoc cref="PlaceholderTextProperty"/>
    public string PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="SelectedKindProperty"/>
    public string? SelectedKind { get => GetValue(SelectedKindProperty); set => SetValue(SelectedKindProperty, value); }

    /// <inheritdoc cref="AllLabelProperty"/>
    public string AllLabel { get => GetValue(AllLabelProperty); set => SetValue(AllLabelProperty, value); }

    /// <inheritdoc cref="OtherLabelProperty"/>
    public string OtherLabel { get => GetValue(OtherLabelProperty); set => SetValue(OtherLabelProperty, value); }

    /// <inheritdoc cref="IsFilteringEnabledProperty"/>
    public bool IsFilteringEnabled { get => GetValue(IsFilteringEnabledProperty); set => SetValue(IsFilteringEnabledProperty, value); }

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <inheritdoc cref="GroupPreviewCountProperty"/>
    public int GroupPreviewCount { get => GetValue(GroupPreviewCountProperty); set => SetValue(GroupPreviewCountProperty, value); }

    /// <inheritdoc cref="PageSizeProperty"/>
    public int PageSize { get => GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }

    /// <inheritdoc cref="SuggestionsProperty"/>
    public IList<string>? Suggestions { get => GetValue(SuggestionsProperty); set => SetValue(SuggestionsProperty, value); }

    /// <inheritdoc cref="SuggestionsLabelProperty"/>
    public string SuggestionsLabel { get => GetValue(SuggestionsLabelProperty); set => SetValue(SuggestionsLabelProperty, value); }

    /// <inheritdoc cref="ResultsFormatProperty"/>
    public string ResultsFormat { get => GetValue(ResultsFormatProperty); set => SetValue(ResultsFormatProperty, value); }

    /// <inheritdoc cref="OneResultTextProperty"/>
    public string OneResultText { get => GetValue(OneResultTextProperty); set => SetValue(OneResultTextProperty, value); }

    /// <inheritdoc cref="NoResultsTextProperty"/>
    public string NoResultsText { get => GetValue(NoResultsTextProperty); set => SetValue(NoResultsTextProperty, value); }

    /// <inheritdoc cref="LoadingTextProperty"/>
    public string LoadingText { get => GetValue(LoadingTextProperty); set => SetValue(LoadingTextProperty, value); }

    /// <inheritdoc cref="EmptyTitleFormatProperty"/>
    public string EmptyTitleFormat { get => GetValue(EmptyTitleFormatProperty); set => SetValue(EmptyTitleFormatProperty, value); }

    /// <inheritdoc cref="EmptyDescriptionProperty"/>
    public string EmptyDescription { get => GetValue(EmptyDescriptionProperty); set => SetValue(EmptyDescriptionProperty, value); }

    /// <inheritdoc cref="OtherFacetFormatProperty"/>
    public string OtherFacetFormat { get => GetValue(OtherFacetFormatProperty); set => SetValue(OtherFacetFormatProperty, value); }

    /// <inheritdoc cref="SearchAllTextProperty"/>
    public string SearchAllText { get => GetValue(SearchAllTextProperty); set => SetValue(SearchAllTextProperty, value); }

    /// <inheritdoc cref="SeeAllFormatProperty"/>
    public string SeeAllFormat { get => GetValue(SeeAllFormatProperty); set => SetValue(SeeAllFormatProperty, value); }

    /// <inheritdoc cref="ShowMoreTextProperty"/>
    public string ShowMoreText { get => GetValue(ShowMoreTextProperty); set => SetValue(ShowMoreTextProperty, value); }

    /// <inheritdoc cref="InvokeCommandProperty"/>
    public ICommand? InvokeCommand { get => GetValue(InvokeCommandProperty); set => SetValue(InvokeCommandProperty, value); }

    /// <inheritdoc cref="ActiveItemProperty"/>
    public SearchResultItem? ActiveItem { get => _activeItem; private set => SetAndRaise(ActiveItemProperty, ref _activeItem, value); }

    /// <inheritdoc cref="ResultCountProperty"/>
    public int ResultCount { get => _resultCount; private set => SetAndRaise(ResultCountProperty, ref _resultCount, value); }

    /// <summary>Inline items (used when <see cref="ItemsSource"/> is null).</summary>
    [Content]
    public AvaloniaList<SearchResultItem> Items { get; } = new();

    /// <summary>Raised when a result is opened with Enter or a click.</summary>
    public event EventHandler<SearchResultInvokedEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    #endregion

    #region 内部属性定义（由主题设置）

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<SearchResults, double>(nameof(NarrowWidth), 560);

    internal static readonly StyledProperty<int> SnippetLeadProperty =
        AvaloniaProperty.Register<SearchResults, int>(nameof(SnippetLead), 36);

    internal static readonly StyledProperty<int> SnippetMaxLengthProperty =
        AvaloniaProperty.Register<SearchResults, int>(nameof(SnippetMaxLength), 220);

    internal static readonly StyledProperty<int> SnippetMaxLinesProperty =
        AvaloniaProperty.Register<SearchResults, int>(nameof(SnippetMaxLines), 2);

    internal static readonly StyledProperty<double> FacetEdgeFadeProperty =
        AvaloniaProperty.Register<SearchResults, double>(nameof(FacetEdgeFade), 20);

    internal static readonly StyledProperty<Thickness> MoreMarginProperty =
        AvaloniaProperty.Register<SearchResults, Thickness>(nameof(MoreMargin), new Thickness(44, 0, 0, 4));

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }
    internal int SnippetLead { get => GetValue(SnippetLeadProperty); set => SetValue(SnippetLeadProperty, value); }
    internal int SnippetMaxLength { get => GetValue(SnippetMaxLengthProperty); set => SetValue(SnippetMaxLengthProperty, value); }
    internal int SnippetMaxLines { get => GetValue(SnippetMaxLinesProperty); set => SetValue(SnippetMaxLinesProperty, value); }
    internal double FacetEdgeFade { get => GetValue(FacetEdgeFadeProperty); set => SetValue(FacetEdgeFadeProperty, value); }
    internal Thickness MoreMargin { get => GetValue(MoreMarginProperty); set => SetValue(MoreMarginProperty, value); }

    #endregion

    /// <summary>A facet: All (null key) or one kind, with the hits it holds for the query.</summary>
    private sealed class FacetModel
    {
        public string? Key;
        public string Label = string.Empty;
        public int Count;
    }

    /// <summary>A "See all" / "Show more" button and the wrapper that carries its reveal lift.</summary>
    private sealed class MoreLink
    {
        public required Border Host { get; init; }
        public required Button Button { get; init; }
        public required TextBlock Text { get; init; }
        public string? FacetKey { get; set; }
    }

    private const double RevealStaggerMs = 35;
    private const double RevealDurationMs = 240;
    private const int RevealMaxStagger = 7;
    private const double RevealLift = 4;
    private static readonly Easing s_linear = new LinearEasing();

    private SearchResultItem? _activeItem;
    private int _resultCount;

    // Search state, reused between keystrokes.
    private readonly List<string> _terms = new();
    private readonly List<SearchResultsHit> _hits = new();
    private readonly List<int> _hitFacets = new();
    private readonly List<FacetModel> _facets = new();
    private readonly Dictionary<string, int> _facetIndex = new(StringComparer.Ordinal);
    private readonly List<SearchMatchRange> _titleRanges = new();
    private readonly List<SearchMatchRange> _snippetRanges = new();
    private int _selectedFacet;
    private int _pageLimit = 20;
    private int _snippetBudget = 160;
    private bool _budgetQueued;

    // Visual pools: rows, headings, links and facets are reused, never re-templated per keystroke.
    private readonly List<SearchResultRow> _rows = new();
    private readonly List<SearchResultRow> _rowPool = new();
    private readonly List<SearchResultGroupHeader> _headerPool = new();
    private readonly List<MoreLink> _morePool = new();
    private readonly List<SearchResultsFacet> _facetPool = new();
    private int _usedRows;
    private int _usedHeaders;
    private int _usedMore;
    private int _facetControlCount;
    private HashSet<SearchResultItem> _shownItems = new();
    private HashSet<SearchResultItem> _previousItems = new();
    private HashSet<string> _shownGroups = new(StringComparer.Ordinal);
    private HashSet<string> _previousGroups = new(StringComparer.Ordinal);
    private bool _hasShowMore;

    // Template parts.
    private SearchField? _searchField;
    private ScrollViewer? _facetScroll;
    private StackPanel? _facetPanel;
    private GlidingIndicator? _facetSelection;
    private ArcContentSwap? _stats;
    private TextBlock? _status;
    private ScrollViewer? _resultsScroll;
    private Panel? _resultsArea;
    private SearchResultsListHost? _listHost;
    private StackPanel? _list;
    private SearchResultsCursor? _cursorView;
    private PresenceHost? _resultsPresence;
    private PresenceHost? _loadingPresence;
    private PresenceHost? _emptyPresence;
    private EmptyState? _emptyState;

    // Empty state actions.
    private StackPanel? _emptyActions;
    private Button? _searchAllButton;
    private WrapPanel? _suggestionRow;
    private TextBlock? _suggestionLabel;
    private readonly List<Button> _suggestionPool = new();

    // Cursor and facet selection.
    private int _cursor = -1;
    private SearchResultsCursorMove _cursorMove;
    private bool _cursorScroll;
    private bool _cursorQueued;
    private bool _facetQueued;
    private bool _facetPlaced;
    private bool _facetScrollPending;
    private Point _lastPointer = new(double.NaN, double.NaN);
    private bool _keyboardInput;
    private readonly ListKeyboardNavigator _navigator;

    // Reveal: one clock drives the staggered fade and rise of new rows.
    private readonly List<Control> _revealing = new();
    private MotionValue<double>? _revealClock;
    private bool _revealLift;

    private bool _isNarrow;
    private INotifyCollectionChanged? _observedSource;
    private bool _syncingText;
    private (double Start, double End) _fadeStops = (-1, -1);

    static SearchResults()
    {
        FocusableProperty.OverrideDefaultValue<SearchResults>(false);
    }

    public SearchResults()
    {
        Items.CollectionChanged += OnItemsChanged;
        _navigator = new ListKeyboardNavigator(() => _rows.Count, i => i >= 0 && i < _rows.Count && _rows[i].IsEnabled) { Wrap = false };
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Search);
        AutomationProperties.SetName(this, Label);
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, OnTunnelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        Update(reveal: false, resetCursor: true);
    }

    #region 测试与诊断

    /// <summary>The shown results, in order (the cursor's stops).</summary>
    internal IReadOnlyList<SearchResultItem> VisibleItems
    {
        get
        {
            var list = new List<SearchResultItem>(_rows.Count);
            foreach (var row in _rows)
            {
                if (row.Item is { } item)
                {
                    list.Add(item);
                }
            }
            return list;
        }
    }

    /// <summary>The shown rows (tests).</summary>
    internal IReadOnlyList<SearchResultRow> Rows => _rows;

    /// <summary>The facets as (key, label, count), All first (tests).</summary>
    internal IReadOnlyList<(string? Key, string Label, int Count)> Facets
        => _facets.Select(f => (f.Key, f.Label, f.Count)).ToList();

    /// <summary>Index of the cursor row, or -1.</summary>
    internal int CursorIndex => _cursor;

    /// <summary>The facet controls (tests).</summary>
    internal IReadOnlyList<SearchResultsFacet> FacetControls => _facetPool.Take(_facetControlCount).ToList();

    /// <summary>The list's children in order: headings, rows and link hosts (tests).</summary>
    internal IReadOnlyList<Control> ListChildren => _list?.Children.ToList() ?? new List<Control>();

    /// <summary>The cursor block (tests).</summary>
    internal SearchResultsCursor? CursorView => _cursorView;

    /// <summary>The search field (tests).</summary>
    internal SearchField? SearchFieldControl => _searchField;

    /// <summary>The list that takes focus (tests).</summary>
    internal SearchResultsListHost? ListHostControl => _listHost;

    /// <summary>The empty state (tests).</summary>
    internal EmptyState? EmptyStateControl => _emptyState;

    /// <summary>The empty state's "Search all" button and suggestion chips (tests).</summary>
    internal Button? SearchAllButton => _searchAllButton;

    internal IReadOnlyList<Button> SuggestionButtons => _suggestionPool.Where(b => b.IsVisible).ToList();

    /// <summary>The "See all" / "Show more" buttons currently shown (tests).</summary>
    internal IReadOnlyList<Button> MoreButtons => _morePool.Take(_usedMore).Select(m => m.Button).ToList();

    /// <summary>The stats text (tests).</summary>
    internal string StatsText => _status?.Text ?? string.Empty;

    #endregion

    private IReadOnlyList<SearchResultItem> SourceItems => ItemsSource switch
    {
        null => Items,
        IReadOnlyList<SearchResultItem> list => list,
        { } items => items.ToList()
    };

    private string KeyOf(SearchResultItem item) => string.IsNullOrEmpty(item.Kind) ? OtherLabel : item.Kind;

    #region 模板

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Detach();
        base.OnApplyTemplate(e);
        _searchField = e.NameScope.Find<SearchField>(SearchFieldPart);
        _facetScroll = e.NameScope.Find<ScrollViewer>(FacetScrollPart);
        _facetPanel = e.NameScope.Find<StackPanel>(FacetsPart);
        _facetSelection = e.NameScope.Find<GlidingIndicator>(FacetSelectionPart);
        _stats = e.NameScope.Find<ArcContentSwap>(StatsPart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        _resultsScroll = e.NameScope.Find<ScrollViewer>(ResultsScrollPart);
        _resultsArea = e.NameScope.Find<Panel>(ResultsAreaPart);
        _listHost = e.NameScope.Find<SearchResultsListHost>(ListHostPart);
        _list = e.NameScope.Find<StackPanel>(ListPart);
        _cursorView = e.NameScope.Find<SearchResultsCursor>(CursorPart);
        _resultsPresence = e.NameScope.Find<PresenceHost>(ResultsPresencePart);
        _loadingPresence = e.NameScope.Find<PresenceHost>(LoadingPresencePart);
        _emptyPresence = e.NameScope.Find<PresenceHost>(EmptyPresencePart);
        _emptyState = e.NameScope.Find<EmptyState>(EmptyStatePart);

        if (_searchField is not null)
        {
            _searchField.Text = Query;
            _searchField.PropertyChanged += OnFieldPropertyChanged;
            _searchField.AddHandler(KeyDownEvent, OnFieldKeyDown, RoutingStrategies.Tunnel);
        }
        if (_facetPanel is not null)
        {
            _facetPanel.Children.Clear();
            _facetControlCount = 0;
            _facetPanel.KeyDown += OnFacetKeyDown;
            _facetPanel.SizeChanged += OnFacetLayoutChanged;
            AutomationProperties.SetName(_facetPanel, Label);
        }
        if (_facetScroll is not null)
        {
            _facetScroll.ScrollChanged += OnFacetScrollChanged;
        }
        if (_listHost is not null)
        {
            _listHost.KeyDown += OnListKeyDown;
            _listHost.TextInput += OnListTextInput;
            _listHost.GotFocus += OnListGotFocus;
            _listHost.KeyboardFocusChanged += OnListKeyboardFocusChanged;
            _listHost.SizeChanged += OnListSizeChanged;
        }
        if (_list is not null)
        {
            _list.Children.Clear();
        }
        _rows.Clear();
        _cursor = -1;
        _facetPlaced = false;
        UpdateFieldTexts();
        PseudoClasses.Set(LoadingPseudoClass, IsLoading);
        Update(reveal: false, resetCursor: true);
    }

    private void Detach()
    {
        if (_searchField is not null)
        {
            _searchField.PropertyChanged -= OnFieldPropertyChanged;
            _searchField.RemoveHandler(KeyDownEvent, OnFieldKeyDown);
        }
        if (_facetPanel is not null)
        {
            _facetPanel.KeyDown -= OnFacetKeyDown;
            _facetPanel.SizeChanged -= OnFacetLayoutChanged;
            _facetPanel.Children.Clear();
        }
        if (_facetScroll is not null)
        {
            _facetScroll.ScrollChanged -= OnFacetScrollChanged;
        }
        if (_listHost is not null)
        {
            _listHost.KeyDown -= OnListKeyDown;
            _listHost.TextInput -= OnListTextInput;
            _listHost.GotFocus -= OnListGotFocus;
            _listHost.KeyboardFocusChanged -= OnListKeyboardFocusChanged;
            _listHost.SizeChanged -= OnListSizeChanged;
        }
        if (_list is not null)
        {
            ResetReveal();
            foreach (var row in _rows)
            {
                row.Release();
            }
            _list.Children.Clear();
        }
        if (_emptyState is not null)
        {
            _emptyState.Action = null;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == QueryProperty)
        {
            if (_searchField is not null && !_syncingText && _searchField.Text != Query)
            {
                _searchField.Text = Query;
            }
            _pageLimit = Math.Max(1, PageSize);
            Update(reveal: true, resetCursor: true);
        }
        else if (property == SelectedKindProperty)
        {
            _pageLimit = Math.Max(1, PageSize);
            _facetScrollPending = true;
            Update(reveal: true, resetCursor: true);
        }
        else if (property == ItemsSourceProperty)
        {
            ObserveSource();
            Update(reveal: true, resetCursor: true);
        }
        else if (property == PageSizeProperty)
        {
            _pageLimit = Math.Max(1, PageSize);
            Update(reveal: false, resetCursor: false);
        }
        else if (property == IsFilteringEnabledProperty || property == GroupPreviewCountProperty || property == AllLabelProperty ||
                 property == OtherLabelProperty || property == SeeAllFormatProperty || property == ShowMoreTextProperty ||
                 property == SnippetLeadProperty || property == SnippetMaxLengthProperty || property == SnippetMaxLinesProperty)
        {
            Update(reveal: false, resetCursor: false);
        }
        else if (property == IsLoadingProperty)
        {
            ApplyLoading();
        }
        else if (property == ResultsFormatProperty || property == OneResultTextProperty || property == NoResultsTextProperty ||
                 property == LoadingTextProperty)
        {
            UpdateStats();
            UpdateEmpty();
        }
        else if (property == SuggestionsProperty || property == SuggestionsLabelProperty || property == EmptyTitleFormatProperty ||
                 property == EmptyDescriptionProperty || property == OtherFacetFormatProperty || property == SearchAllTextProperty)
        {
            UpdateEmpty();
        }
        else if (property == LabelProperty || property == PlaceholderTextProperty)
        {
            AutomationProperties.SetName(this, Label);
            UpdateFieldTexts();
        }
        else if (property == NarrowWidthProperty)
        {
            UpdateNarrow(Bounds.Width);
        }
        else if (property == MoreMarginProperty)
        {
            foreach (var link in _morePool)
            {
                link.Host.Margin = MoreMargin;
            }
        }
    }

    private void UpdateFieldTexts()
    {
        if (_searchField is not null)
        {
            _searchField.Label = Label;
            _searchField.Placeholder = PlaceholderText;
        }
        if (_facetPanel is not null)
        {
            AutomationProperties.SetName(_facetPanel, Label);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveSource();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceChanged;
            _observedSource = null;
        }
    }

    private void ObserveSource()
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceChanged;
            _observedSource = null;
        }
        if (ItemsSource is INotifyCollectionChanged observable && VisualRoot is not null)
        {
            _observedSource = observable;
            observable.CollectionChanged += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Update(reveal: true, resetCursor: true);

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ItemsSource is null)
        {
            Update(reveal: true, resetCursor: true);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateNarrow(e.NewSize.Width);
    }

    private void UpdateNarrow(double width)
    {
        var narrow = width > 0 && width < NarrowWidth;
        if (narrow == _isNarrow)
        {
            return;
        }
        _isNarrow = narrow;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        foreach (var row in _rows)
        {
            row.SetCompact(narrow);
        }
        QueueCursor(SearchResultsCursorMove.Snap, scroll: false);
    }

    #endregion

    #region 搜索与列表

    /// <summary>Runs the search again (after you change an item in place).</summary>
    public void Refresh() => Update(reveal: false, resetCursor: false);

    /// <summary>Moves keyboard focus into the search field, with the caret after the query.</summary>
    public void FocusSearch() => FocusSearch(NavigationMethod.Pointer, selectAll: false);

    private void FocusSearch(NavigationMethod method, bool selectAll)
    {
        if (_searchField is null)
        {
            return;
        }
        _searchField.Focus(method);
        if (selectAll)
        {
            _searchField.SelectAll();
        }
        else
        {
            _searchField.CaretIndex = _searchField.Text?.Length ?? 0;
            _searchField.ClearSelection();
        }
    }

    private void Update(bool reveal, bool resetCursor)
    {
        var source = SourceItems;
        SearchResultsMatcher.Tokenize(Query, _terms);
        SearchResultsMatcher.Search(source, _terms, IsFilteringEnabled, _hits);
        BuildFacets(source);
        _hitFacets.Clear();
        foreach (var hit in _hits)
        {
            var index = _facetIndex.TryGetValue(KeyOf(hit.Item), out var found) ? found : 0;
            _hitFacets.Add(index);
            if (index > 0)
            {
                _facets[index].Count++;
            }
        }
        _facets[0].Count = _hits.Count;
        _selectedFacet = SelectedKind is { } kind && _facetIndex.TryGetValue(kind, out var selected) ? selected : 0;
        ResultCount = _facets[_selectedFacet].Count;
        SyncFacetControls();
        BuildList(reveal, resetCursor);
        UpdateStats();
        UpdateStates();
    }

    private void BuildFacets(IReadOnlyList<SearchResultItem> source)
    {
        _facetIndex.Clear();
        var count = 0;
        RentFacet(count++, null, AllLabel);
        foreach (var item in source)
        {
            var key = KeyOf(item);
            if (!_facetIndex.ContainsKey(key))
            {
                _facetIndex[key] = count;
                RentFacet(count++, key, key);
            }
        }
        if (_facets.Count > count)
        {
            _facets.RemoveRange(count, _facets.Count - count);
        }
    }

    private void RentFacet(int index, string? key, string label)
    {
        if (index >= _facets.Count)
        {
            _facets.Add(new FacetModel());
        }
        var facet = _facets[index];
        facet.Key = key;
        facet.Label = label;
        facet.Count = 0;
    }

    private void SyncFacetControls()
    {
        if (_facetPanel is null)
        {
            return;
        }
        var same = _facetControlCount == _facets.Count;
        for (var i = 0; same && i < _facets.Count; i++)
        {
            same = string.Equals(_facetPool[i].Key, _facets[i].Key, StringComparison.Ordinal);
        }
        if (!same)
        {
            var focused = _facetPanel.IsKeyboardFocusWithin;
            _facetPanel.Children.Clear();
            for (var i = 0; i < _facets.Count; i++)
            {
                if (i >= _facetPool.Count)
                {
                    var created = new SearchResultsFacet();
                    created.Activated += OnFacetActivated;
                    created.SizeChanged += OnFacetLayoutChanged;
                    _facetPool.Add(created);
                }
                var facet = _facetPool[i];
                facet.Key = _facets[i].Key;
                facet.Index = i;
                _facetPanel.Children.Add(facet);
            }
            _facetControlCount = _facets.Count;
            _facetPlaced = false;
            if (focused)
            {
                Dispatcher.UIThread.Post(() => _facetPool[Math.Min(_selectedFacet, _facetControlCount - 1)].Focus(NavigationMethod.Directional),
                    DispatcherPriority.Loaded);
            }
        }
        for (var i = 0; i < _facetControlCount; i++)
        {
            var model = _facets[i];
            var facet = _facetPool[i];
            facet.Label = model.Label;
            facet.Count = model.Count;
            facet.IsDimmed = model.Count == 0;
            facet.IsSelected = i == _selectedFacet;
        }
        QueueFacetSelection();
    }

    private void BuildList(bool reveal, bool resetCursor)
    {
        if (_list is null)
        {
            return;
        }
        _snippetBudget = MeasureSnippetBudget();
        var keep = resetCursor ? null : ActiveItem;
        var listFocused = _listHost?.IsKeyboardFocusWithin == true;
        ResetReveal();
        if (_cursor >= 0 && _cursor < _rows.Count)
        {
            _rows[_cursor].SetActive(false);
        }
        foreach (var row in _rows)
        {
            row.Release();
        }
        _rows.Clear();
        _list.Children.Clear();
        _usedRows = 0;
        _usedHeaders = 0;
        _usedMore = 0;
        (_previousItems, _shownItems) = (_shownItems, _previousItems);
        (_previousGroups, _shownGroups) = (_shownGroups, _previousGroups);
        _shownItems.Clear();
        _shownGroups.Clear();
        var hadShowMore = _hasShowMore;
        _hasShowMore = false;

        if (_selectedFacet == 0)
        {
            var preview = Math.Max(1, GroupPreviewCount);
            for (var f = 1; f < _facets.Count; f++)
            {
                var facet = _facets[f];
                if (facet.Count == 0)
                {
                    continue;
                }
                var key = facet.Key!;
                var newGroup = !_previousGroups.Contains(key);
                _shownGroups.Add(key);
                var header = RentHeader();
                header.Key = key;
                header.Label = facet.Label;
                header.CountText = facet.Count.ToString("N0", CultureInfo.CurrentCulture);
                AddChild(header, reveal && newGroup);
                var shown = 0;
                for (var h = 0; h < _hits.Count && shown < preview; h++)
                {
                    if (_hitFacets[h] == f)
                    {
                        AddRow(_hits[h].Item, facet.Label, reveal);
                        shown++;
                    }
                }
                if (facet.Count > shown)
                {
                    var link = RentMore();
                    link.FacetKey = key;
                    link.Text.Text = Format(SeeAllFormat, facet.Count.ToString("N0", CultureInfo.CurrentCulture), facet.Label);
                    AddChild(link.Host, reveal && newGroup);
                }
            }
        }
        else
        {
            var facet = _facets[_selectedFacet];
            var limit = Math.Max(1, _pageLimit);
            var shown = 0;
            var more = false;
            for (var h = 0; h < _hits.Count; h++)
            {
                if (_hitFacets[h] != _selectedFacet)
                {
                    continue;
                }
                if (shown == limit)
                {
                    more = true;
                    break;
                }
                AddRow(_hits[h].Item, facet.Label, reveal);
                shown++;
            }
            if (more)
            {
                var link = RentMore();
                link.FacetKey = null;
                link.Text.Text = ShowMoreText;
                AddChild(link.Host, reveal && !hadShowMore);
                _hasShowMore = true;
            }
        }
        if (_listHost is not null)
        {
            AutomationProperties.SetName(_listHost, CountText(ResultCount));
        }
        if (reveal)
        {
            StartReveal();
        }
        else
        {
            _revealing.Clear();
        }

        // The cursor stays on its result when the list only grew or relabelled; a new search starts without one
        // (or on the first result while the list has keyboard focus).
        var target = -1;
        if (keep is not null)
        {
            target = IndexOfRow(keep);
        }
        else if (listFocused)
        {
            target = FirstEnabledRow();
        }
        _cursor = -1;
        if (target >= 0)
        {
            SetCursor(target, SearchResultsCursorMove.Snap, scroll: false);
        }
        else
        {
            ActiveItem = null;
            if (listFocused && _rows.Count == 0)
            {
                FocusSearch(NavigationMethod.Tab, selectAll: false);
            }
            QueueCursor(SearchResultsCursorMove.Snap, scroll: false);
        }
    }

    /// <summary>
    /// Characters the snippet lines hold at the list's width (about 0.55em per character), so a snippet whose first
    /// match is already visible is shown from its start.
    /// </summary>
    private int MeasureSnippetBudget()
    {
        var width = _list?.Bounds.Width ?? 0;
        if (width <= 0)
        {
            width = Bounds.Width > 0 ? Bounds.Width - Padding.Left - Padding.Right : 640;
        }
        // Row inset (12 + 12) and the icon block with its gap (32 + 12).
        var textWidth = Math.Max(120, width - 68);
        var perLine = textWidth / Math.Max(1, FontSize * 0.55);
        var lines = Math.Max(1, SnippetMaxLines);
        return Math.Max(16, (int)(perLine * lines) - 8 * lines);
    }

    private void AddRow(SearchResultItem item, string kindLabel, bool reveal)
    {
        var row = RentRow();
        SearchResultsMatcher.FindRanges(item.Title.AsSpan(), _terms, _titleRanges);
        string? snippet = null;
        if (!string.IsNullOrEmpty(item.Snippet))
        {
            snippet = SearchResultsMatcher.Window(item.Snippet, _terms, SnippetLead, _snippetBudget,
                Math.Max(SnippetMaxLength, _snippetBudget + 24), _snippetRanges);
        }
        else
        {
            _snippetRanges.Clear();
        }
        row.SetData(item, kindLabel, _titleRanges, snippet, _snippetRanges);
        row.SetCompact(_isNarrow);
        row.Index = _rows.Count;
        _rows.Add(row);
        AddChild(row, reveal && !_previousItems.Contains(item));
        _shownItems.Add(item);
    }

    private void AddChild(Control child, bool reveal)
    {
        _list!.Children.Add(child);
        if (reveal)
        {
            _revealing.Add(child);
        }
    }

    private SearchResultRow RentRow()
    {
        if (_usedRows >= _rowPool.Count)
        {
            var created = new SearchResultRow();
            created.PointerMoved += OnRowPointerMoved;
            created.PointerReleased += OnRowPointerReleased;
            _rowPool.Add(created);
        }
        return _rowPool[_usedRows++];
    }

    private SearchResultGroupHeader RentHeader()
    {
        if (_usedHeaders >= _headerPool.Count)
        {
            _headerPool.Add(new SearchResultGroupHeader());
        }
        var header = _headerPool[_usedHeaders++];
        ResetLift(header);
        return header;
    }

    private MoreLink RentMore()
    {
        if (_usedMore >= _morePool.Count)
        {
            var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            var glyph = new ArcGlyph { Data = ArcIcons.ArrowRight, Size = 14, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center };
            var button = new Button
            {
                Variant = ButtonVariant.Ghost,
                Size = ControlSize.Small,
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { text, glyph } }
            };
            ArcTokens.Bind(button, TemplatedControl.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextSecondary);
            var host = new Border
            {
                Child = button,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = MoreMargin,
                RenderTransform = new TranslateTransform()
            };
            var link = new MoreLink { Host = host, Button = button, Text = text };
            button.Click += (_, e) =>
            {
                e.Handled = true;
                OnMoreClick(link);
            };
            _morePool.Add(link);
        }
        var result = _morePool[_usedMore++];
        ResetLift(result.Host);
        return result;
    }

    private static void ResetLift(Control control)
    {
        control.Opacity = 1;
        if (control.RenderTransform is TranslateTransform lift)
        {
            lift.Y = 0;
        }
    }

    private void OnMoreClick(MoreLink link)
    {
        var method = _keyboardInput ? NavigationMethod.Directional : NavigationMethod.Pointer;
        if (link.FacetKey is { } key)
        {
            // "See all N Docs": switch to that facet and continue in its list.
            SetCurrentValue(SelectedKindProperty, key);
            FocusList(FirstEnabledRow(), method);
            return;
        }
        var firstNew = _rows.Count;
        _pageLimit += Math.Max(1, PageSize);
        Update(reveal: true, resetCursor: false);
        FocusList(firstNew < _rows.Count ? firstNew : FirstEnabledRow(), method);
    }

    private int IndexOfRow(SearchResultItem item)
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            if (ReferenceEquals(_rows[i].Item, item))
            {
                return i;
            }
        }
        return -1;
    }

    private int FirstEnabledRow()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].IsEnabled)
            {
                return i;
            }
        }
        return -1;
    }

    private static string Format(string format, object? a, object? b = null)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, a, b);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    private string CountText(int count) => count switch
    {
        0 => NoResultsText,
        1 => OneResultText,
        _ => Format(ResultsFormat, count.ToString("N0", CultureInfo.CurrentCulture))
    };

    private void UpdateStats()
    {
        var text = IsLoading ? LoadingText : CountText(ResultCount);
        if (_stats is not null && !Equals(_stats.Content, text))
        {
            _stats.Content = text;
        }
        if (_status is not null && _status.Text != text)
        {
            _status.Text = text;
        }
    }

    #endregion

    #region 加载与空状态

    private void ApplyLoading()
    {
        PseudoClasses.Set(LoadingPseudoClass, IsLoading);
        if (IsLoading)
        {
            // Skeleton rows take the place of the previous results without changing the height.
            if (_resultsArea is not null && _listHost is not null && _resultsPresence?.IsPresent == true)
            {
                _resultsArea.MinHeight = _listHost.Bounds.Height;
            }
            if (_listHost?.IsKeyboardFocusWithin == true)
            {
                FocusSearch(NavigationMethod.Tab, selectAll: false);
            }
            SetCursor(-1, SearchResultsCursorMove.Snap, scroll: false);
        }
        else
        {
            if (_resultsArea is not null)
            {
                _resultsArea.MinHeight = 0;
            }
            // Whatever arrived while loading is new: it all fades in.
            _shownItems.Clear();
            _shownGroups.Clear();
            _hasShowMore = false;
            Update(reveal: true, resetCursor: true);
            return;
        }
        UpdateStats();
        UpdateStates();
    }

    private void UpdateStates()
    {
        var loading = IsLoading;
        var empty = !loading && ResultCount == 0;
        PseudoClasses.Set(EmptyPseudoClass, empty);
        if (_loadingPresence is not null)
        {
            _loadingPresence.IsPresent = loading;
        }
        if (_resultsPresence is not null)
        {
            _resultsPresence.IsPresent = !loading && !empty;
        }
        if (_emptyPresence is not null)
        {
            _emptyPresence.IsPresent = empty;
        }
        if (empty)
        {
            UpdateEmpty();
        }
    }

    private void UpdateEmpty()
    {
        if (_emptyState is null)
        {
            return;
        }
        var query = (Query ?? string.Empty).Trim();
        _emptyState.Title = query.Length == 0 ? NoResultsText : Format(EmptyTitleFormat, query);
        _emptyState.Label = _emptyState.Title;
        // The current facet is empty but others are not: say where the results are and offer All.
        FacetModel? best = null;
        if (_selectedFacet != 0 && _facets[0].Count > 0)
        {
            for (var i = 1; i < _facets.Count; i++)
            {
                if (_facets[i].Count > 0 && (best is null || _facets[i].Count > best.Count))
                {
                    best = _facets[i];
                }
            }
        }
        _emptyState.Description = best is null ? EmptyDescription : Format(OtherFacetFormat, CountText(best.Count), best.Label);
        EnsureEmptyActions();
        _searchAllButton!.Content = SearchAllText;
        _searchAllButton.IsVisible = best is not null;
        var suggestions = Suggestions;
        var count = suggestions?.Count ?? 0;
        _suggestionLabel!.Text = SuggestionsLabel;
        for (var i = 0; i < count; i++)
        {
            if (i >= _suggestionPool.Count)
            {
                var chip = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Small };
                chip.Click += OnSuggestionClick;
                _suggestionPool.Add(chip);
                _suggestionRow!.Children.Add(chip);
            }
            var button = _suggestionPool[i];
            button.Content = suggestions![i];
            button.Tag = suggestions[i];
            button.IsVisible = true;
        }
        for (var i = count; i < _suggestionPool.Count; i++)
        {
            _suggestionPool[i].IsVisible = false;
        }
        _suggestionRow!.IsVisible = count > 0;
        var hasActions = best is not null || count > 0;
        if (hasActions && _emptyState.Action != _emptyActions)
        {
            _emptyState.Action = _emptyActions;
        }
        else if (!hasActions && _emptyState.Action is not null)
        {
            _emptyState.Action = null;
        }
    }

    private void EnsureEmptyActions()
    {
        if (_emptyActions is not null)
        {
            return;
        }
        _searchAllButton = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Center };
        _searchAllButton.Click += (_, e) =>
        {
            e.Handled = true;
            SetCurrentValue(SelectedKindProperty, null);
            FocusSearch(_keyboardInput ? NavigationMethod.Tab : NavigationMethod.Pointer, selectAll: false);
        };
        _suggestionLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 2, 0) };
        ArcTokens.Bind(_suggestionLabel, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_suggestionLabel, TextBlock.FontSizeProperty, DesignTokens.ArcDesignTokenKind.TypeSM);
        _suggestionRow = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            ItemSpacing = 8,
            LineSpacing = 8,
            Children = { _suggestionLabel }
        };
        _emptyActions = new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { _searchAllButton, _suggestionRow }
        };
    }

    private void OnSuggestionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string suggestion })
        {
            e.Handled = true;
            SetCurrentValue(QueryProperty, suggestion);
            FocusSearch(_keyboardInput ? NavigationMethod.Tab : NavigationMethod.Pointer, selectAll: false);
        }
    }

    #endregion

    #region 进场

    /// <summary>
    /// New results fade in and rise 4px, 35ms apart (at most 8 steps); results that stay put do not move. One clock
    /// drives every row (code-driven: the rows are created at run time with per-index delays, which a ControlTheme
    /// cannot express). Reduced motion only fades; Off shows them at once.
    /// </summary>
    private void StartReveal()
    {
        if (_revealing.Count == 0)
        {
            return;
        }
        var level = IsLoaded ? ArcMotion.GetEffectiveLevel(this) : ArcMotionLevel.Off;
        if (level == ArcMotionLevel.Off)
        {
            _revealing.Clear();
            return;
        }
        _revealLift = level == ArcMotionLevel.Full;
        if (_revealClock is null)
        {
            _revealClock = new MotionValue<double>(this, 0, MotionVectors.Double);
            _revealClock.Changed += ApplyReveal;
        }
        _revealClock.Jump(0);
        var total = RevealMaxStagger * RevealStaggerMs + RevealDurationMs;
        _ = _revealClock.AnimateTo(total, MotionSpec.Tween(TimeSpan.FromMilliseconds(total), s_linear));
    }

    private void ApplyReveal(double elapsed)
    {
        for (var i = 0; i < _revealing.Count; i++)
        {
            var child = _revealing[i];
            var local = Math.Clamp((elapsed - Math.Min(i, RevealMaxStagger) * RevealStaggerMs) / RevealDurationMs, 0, 1);
            var eased = ArcEasings.Enter.Ease(local);
            child.Opacity = eased;
            if (child.RenderTransform is TranslateTransform lift)
            {
                lift.Y = _revealLift ? RevealLift * (1 - eased) : 0;
            }
        }
    }

    private void ResetReveal()
    {
        _revealClock?.Stop();
        foreach (var child in _revealing)
        {
            ResetLift(child);
        }
        _revealing.Clear();
    }

    #endregion

    #region 光标

    private void SetCursor(int index, SearchResultsCursorMove move, bool scroll)
    {
        if (index >= _rows.Count)
        {
            index = _rows.Count - 1;
        }
        if (index < 0)
        {
            index = -1;
        }
        if (_cursor >= 0 && _cursor < _rows.Count)
        {
            _rows[_cursor].SetActive(false);
        }
        _cursor = index;
        var row = index >= 0 ? _rows[index] : null;
        row?.SetActive(true);
        ActiveItem = row?.Item;
        QueueCursor(move, scroll);
    }

    private void QueueCursor(SearchResultsCursorMove move, bool scroll)
    {
        _cursorMove = move;
        _cursorScroll |= scroll;
        if (_cursorQueued)
        {
            return;
        }
        _cursorQueued = true;
        // Rows need a layout pass before their position is known.
        Dispatcher.UIThread.Post(PlaceCursor, DispatcherPriority.Loaded);
    }

    private void PlaceCursor()
    {
        _cursorQueued = false;
        var scroll = _cursorScroll;
        _cursorScroll = false;
        if (_cursorView is null || _list is null)
        {
            return;
        }
        if (_cursor < 0 || _cursor >= _rows.Count || IsLoading)
        {
            _cursorView.Hide();
            return;
        }
        var row = _rows[_cursor];
        if (row.Bounds.Width <= 0 || row.Bounds.Height <= 0)
        {
            _cursorView.Hide(animate: false);
            return;
        }
        // Layout bounds, not the row's reveal lift: the cursor lands where the row settles.
        _cursorView.MoveTo(new Rect(row.Bounds.Position + _list.Bounds.Position, row.Bounds.Size), _cursorMove);
        if (scroll)
        {
            ScrollRowIntoView(row);
        }
    }

    /// <summary>
    /// Scrolls the results just enough to show <paramref name="row"/> with a little room (nearest). Done by hand rather
    /// than with BringIntoView, which would also scroll the page around the control when the row is already visible.
    /// </summary>
    private void ScrollRowIntoView(SearchResultRow row)
    {
        if (_resultsScroll is not { } viewer || _resultsArea is null || row.TranslatePoint(default, _resultsArea) is not { } at)
        {
            return;
        }
        const double room = 12;
        var viewport = viewer.Viewport.Height;
        var offset = viewer.Offset.Y;
        var max = Math.Max(0, viewer.Extent.Height - viewport);
        if (viewport <= 0 || max <= 0)
        {
            return;
        }
        var top = at.Y - room;
        var bottom = at.Y + row.Bounds.Height + room;
        var target = top < offset ? top : bottom > offset + viewport ? bottom - viewport : offset;
        target = Math.Clamp(target, 0, max);
        if (Math.Abs(target - offset) > 0.5)
        {
            viewer.Offset = new Vector(viewer.Offset.X, target);
        }
    }

    private void OnListSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // Snippet windows depend on how much text a line holds.
        if (!_budgetQueued && Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 0.5 && Math.Abs(MeasureSnippetBudget() - _snippetBudget) > 8)
        {
            _budgetQueued = true;
            Dispatcher.UIThread.Post(() =>
            {
                _budgetQueued = false;
                Update(reveal: false, resetCursor: false);
            }, DispatcherPriority.Background);
        }
        if (_cursor >= 0)
        {
            QueueCursor(SearchResultsCursorMove.Snap, scroll: false);
        }
    }

    private void OnListKeyboardFocusChanged(object? sender, EventArgs e)
    {
        if (_cursorView is not null && _listHost is not null)
        {
            _cursorView.IsRingVisible = _listHost.HasKeyboardFocus;
        }
    }

    private void OnListGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.Source == _listHost && _cursor < 0 && FirstEnabledRow() is var first and >= 0)
        {
            SetCursor(first, SearchResultsCursorMove.Key, scroll: true);
        }
    }

    private void FocusList(int index, NavigationMethod method)
    {
        if (_listHost is null || index < 0)
        {
            return;
        }
        SetCursor(index, SearchResultsCursorMove.Key, scroll: true);
        _listHost.Focus(method);
    }

    private void OnRowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not SearchResultRow { Item: not null } row || e.Pointer.Type == PointerType.Touch || IsLoading)
        {
            return;
        }
        // Rows sliding under a still pointer (scrolling, new results) do not move the cursor.
        var point = e.GetPosition(this);
        if (point == _lastPointer)
        {
            return;
        }
        _lastPointer = point;
        if (row.Index != _cursor && row.IsEnabled)
        {
            SetCursor(row.Index, SearchResultsCursorMove.Pointer, scroll: false);
        }
    }

    private void OnRowPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is SearchResultRow { Item: { } item } row && e.InitialPressMouseButton == MouseButton.Left &&
            new Rect(row.Bounds.Size).Contains(e.GetPosition(row)))
        {
            e.Handled = true;
            Invoke(item);
        }
    }

    private void Invoke(SearchResultItem item)
    {
        if (!item.IsEnabled)
        {
            return;
        }
        RaiseEvent(new SearchResultInvokedEventArgs(ItemInvokedEvent, item));
        if (InvokeCommand is { } command && command.CanExecute(item))
        {
            command.Execute(item);
        }
    }

    #endregion

    #region 键盘

    private void OnTunnelKeyDown(object? sender, KeyEventArgs e) => _keyboardInput = true;

    private void OnTunnelPointerPressed(object? sender, PointerPressedEventArgs e) => _keyboardInput = false;

    private void OnFieldPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != AvaloniaTextBox.TextProperty || _searchField is null)
        {
            return;
        }
        var text = _searchField.Text ?? string.Empty;
        if (Query != text)
        {
            _syncingText = true;
            try
            {
                SetCurrentValue(QueryProperty, text);
            }
            finally
            {
                _syncingText = false;
            }
        }
    }

    private void OnFieldKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || IsLoading)
        {
            return;
        }
        if (e.Key == Key.Down)
        {
            var first = FirstEnabledRow();
            if (first >= 0)
            {
                e.Handled = true;
                FocusList(first, NavigationMethod.Directional);
            }
        }
        else if (e.Key == Key.Enter)
        {
            var target = _cursor >= 0 && _rows[_cursor].IsEnabled ? _cursor : FirstEnabledRow();
            if (target >= 0 && _rows[target].Item is { } item)
            {
                e.Handled = true;
                Invoke(item);
            }
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source != _listHost || e.Handled || IsLoading)
        {
            return;
        }
        var first = FirstEnabledRow();
        switch (e.Key)
        {
            case Key.Up when _cursor <= first:
                // Above the first result: back to the field.
                e.Handled = true;
                SetCursor(-1, SearchResultsCursorMove.Key, scroll: false);
                FocusSearch(NavigationMethod.Tab, selectAll: false);
                return;
            case Key.Enter or Key.Space:
                e.Handled = true;
                if (_cursor >= 0 && _rows[_cursor].Item is { } item)
                {
                    Invoke(item);
                }
                return;
            case Key.Escape:
                e.Handled = true;
                FocusSearch(NavigationMethod.Tab, selectAll: false);
                return;
        }
        _navigator.PageSize = PageStep();
        if (_navigator.Navigate(_cursor, e.Key) is { } next && next >= 0)
        {
            e.Handled = true;
            if (next != _cursor)
            {
                SetCursor(next, SearchResultsCursorMove.Key, scroll: true);
            }
        }
    }

    /// <summary>Rows that fit in the results viewport (PageUp / PageDown step).</summary>
    private int PageStep()
    {
        if (_resultsScroll is null || _rows.Count == 0)
        {
            return 5;
        }
        var height = _rows[Math.Max(0, _cursor)].Bounds.Height;
        return height > 0 ? Math.Max(1, (int)(_resultsScroll.Viewport.Height / height) - 1) : 5;
    }

    private void OnListTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text) || _searchField is null)
        {
            return;
        }
        e.Handled = true;
        if (e.Text == "/")
        {
            FocusSearch(NavigationMethod.Tab, selectAll: false);
            return;
        }
        if (char.IsControl(e.Text[0]) || string.IsNullOrWhiteSpace(e.Text))
        {
            e.Handled = false;
            return;
        }
        // Typing in the list keeps refining the query: focus returns to the field first, so the new results start
        // without a cursor, and the caret follows the added text.
        FocusSearch(NavigationMethod.Tab, selectAll: false);
        SetCurrentValue(QueryProperty, Query + e.Text);
        FocusSearch(NavigationMethod.Tab, selectAll: false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && (e.KeyModifiers & KeyModifiers.Alt) == 0;
        if (e.Key == Key.F && command)
        {
            e.Handled = true;
            FocusSearch(NavigationMethod.Tab, selectAll: true);
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || e.Text != "/" || _searchField is null || IsInField(e.Source))
        {
            return;
        }
        e.Handled = true;
        FocusSearch(NavigationMethod.Tab, selectAll: false);
    }

    private bool IsInField(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (ReferenceEquals(visual, _searchField))
            {
                return true;
            }
            if (ReferenceEquals(visual, this))
            {
                return false;
            }
        }
        return false;
    }

    #endregion

    #region 分面

    private void OnFacetActivated(object? sender, EventArgs e)
    {
        if (sender is SearchResultsFacet facet)
        {
            SelectFacet(facet.Index, focus: false);
        }
    }

    private void SelectFacet(int index, bool focus)
    {
        if (index < 0 || index >= _facetControlCount)
        {
            return;
        }
        SetCurrentValue(SelectedKindProperty, _facets[index].Key);
        if (focus)
        {
            _facetPool[index].Focus(NavigationMethod.Directional);
        }
    }

    private void OnFacetKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is not SearchResultsFacet facet || e.KeyModifiers != KeyModifiers.None || _facetControlCount == 0)
        {
            return;
        }
        var count = _facetControlCount;
        int? target = e.Key switch
        {
            Key.Left => (facet.Index - 1 + count) % count,
            Key.Right => (facet.Index + 1) % count,
            Key.Home => 0,
            Key.End => count - 1,
            _ => null
        };
        if (target is { } index)
        {
            e.Handled = true;
            SelectFacet(index, focus: true);
        }
        else if (e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            SelectFacet(facet.Index, focus: false);
        }
        else if (e.Key == Key.Down && FirstEnabledRow() is var first and >= 0 && !IsLoading)
        {
            e.Handled = true;
            FocusList(first, NavigationMethod.Directional);
        }
    }

    private void OnFacetLayoutChanged(object? sender, SizeChangedEventArgs e) => QueueFacetSelection();

    private void OnFacetScrollChanged(object? sender, ScrollChangedEventArgs e) => UpdateFacetFade();

    private void QueueFacetSelection()
    {
        if (_facetQueued)
        {
            return;
        }
        _facetQueued = true;
        Dispatcher.UIThread.Post(PlaceFacetSelection, DispatcherPriority.Loaded);
    }

    /// <summary>The selected facet's block glides on the morph spring (first placement lands in place).</summary>
    private void PlaceFacetSelection()
    {
        _facetQueued = false;
        if (_facetSelection is null || _facetPanel is null || _selectedFacet >= _facetControlCount)
        {
            return;
        }
        var facet = _facetPool[_selectedFacet];
        if (facet.Bounds.Width <= 0)
        {
            return;
        }
        var rect = new Rect(facet.Bounds.Position + _facetPanel.Bounds.Position, facet.Bounds.Size);
        _facetSelection.MoveTo(rect, animate: _facetPlaced);
        _facetPlaced = true;
        if (_facetScrollPending)
        {
            _facetScrollPending = false;
            ScrollFacetIntoView(facet);
        }
        UpdateFacetFade();
    }

    /// <summary>Keeps the selected facet fully visible in a scrolling facet row, clear of the edge fade.</summary>
    private void ScrollFacetIntoView(SearchResultsFacet facet)
    {
        if (_facetScroll is not { } viewer || _facetPanel is null || facet.TranslatePoint(default, _facetPanel) is not { } at)
        {
            return;
        }
        var viewport = viewer.Viewport.Width;
        var max = Math.Max(0, viewer.Extent.Width - viewport);
        if (viewport <= 0 || max <= 0)
        {
            return;
        }
        // The facet panel sits inside the track inset; content coordinates add its offset.
        var x = at.X + _facetPanel.Bounds.X + ((_facetPanel.GetVisualParent() as Visual)?.Bounds.X ?? 0);
        var room = FacetEdgeFade;
        var offset = viewer.Offset.X;
        var start = x - room;
        var end = x + facet.Bounds.Width + room - viewport;
        var target = Math.Clamp(offset > start ? start : offset < end ? end : offset, 0, max);
        if (Math.Abs(target - offset) > 0.5)
        {
            viewer.Offset = new Vector(target, viewer.Offset.Y);
        }
    }

    /// <summary>A facet row wider than the column scrolls; its edges fade only on the side with more to see.</summary>
    private void UpdateFacetFade()
    {
        if (_facetScroll is not { } viewer)
        {
            return;
        }
        var width = viewer.Viewport.Width;
        var rest = viewer.Extent.Width - width - viewer.Offset.X;
        var start = viewer.Offset.X > 1 ? FacetEdgeFade : 0;
        var end = rest > 1 ? FacetEdgeFade : 0;
        if (width <= 0 || (start <= 0 && end <= 0))
        {
            if (viewer.OpacityMask is not null)
            {
                viewer.OpacityMask = null;
            }
            _fadeStops = (-1, -1);
            return;
        }
        var stops = (Math.Clamp(start / width, 0, 0.5), Math.Clamp(1 - end / width, 0.5, 1));
        if (stops == _fadeStops)
        {
            return;
        }
        _fadeStops = stops;
        // A new brush per change: the compositor does not always see edits to a live gradient.
        viewer.OpacityMask = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Colors.Transparent, 0),
                new GradientStop(Colors.Black, stops.Item1),
                new GradientStop(Colors.Black, stops.Item2),
                new GradientStop(Colors.Transparent, 1)
            }
        };
    }

    #endregion
}
