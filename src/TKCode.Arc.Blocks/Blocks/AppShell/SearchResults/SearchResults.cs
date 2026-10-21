using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A search results page for a workspace (an original TKCode design, after Algolia InstantSearch's search box, hits,
/// highlight, refinement list and stats). A large search field with a suggestions popup (recent and suggested
/// searches), a filters column (checkbox groups whose counts follow the query and the other filters, and a date range),
/// a results header with the count, the time it took and a sort switch, the active filters as removable chips, and the
/// results: people and documents in one list, matches marked in titles and in a snippet cut around the first match.
/// One cursor block glides between the results; Enter or a click opens a result in a detail dialog (a bottom sheet
/// on narrow screens). Below 560 the filters move behind a button into a sheet.
/// </summary>
/// <remarks>
/// <para>
/// Data: give <see cref="Items"/> or <see cref="ItemsSource"/> and, for filters, <see cref="Filters"/> (a
/// <see cref="SearchValueFilter"/> reads <see cref="SearchResultItem.Kind"/> or an entry of
/// <see cref="SearchResultItem.Attributes"/>; a <see cref="SearchDateFilter"/> reads <see cref="SearchResultItem.Date"/>).
/// The control matches, filters, counts and sorts the items itself. Set <see cref="IsFilteringEnabled"/> to false when a
/// server does that: the items are shown as given, the counts on the filter values are yours, and
/// <see cref="SearchChanged"/> tells you when to ask again.
/// </para>
/// <para>
/// Keys: "/" (or Ctrl/Cmd+F) focuses the field; ArrowDown leaves the field for the first result (or walks the
/// suggestions while they are open), ArrowUp from the first result returns; Home, End, PageUp, PageDown move the cursor;
/// Enter opens; Escape closes the suggestions, then the detail, then clears the query.
/// </para>
/// </remarks>
[TemplatePart(FieldPart, typeof(TextBox))]
[TemplatePart(FieldFramePart, typeof(Panel))]
[TemplatePart(SuggestionsPopupPart, typeof(ArcPopup))]
[TemplatePart(SuggestionSurfacePart, typeof(Border))]
[TemplatePart(SuggestionListPart, typeof(TemplatedControl))]
[TemplatePart(FiltersColumnPart, typeof(Border))]
[TemplatePart(FiltersPart, typeof(TemplatedControl))]
[TemplatePart(FiltersButtonPart, typeof(AvaloniaButton))]
[TemplatePart(CountPart, typeof(RollingDigits))]
[TemplatePart(CountTextPart, typeof(TextBlock))]
[TemplatePart(ElapsedPart, typeof(RollingDigits))]
[TemplatePart(StatusPart, typeof(TextBlock))]
[TemplatePart(SortPart, typeof(SegmentedControl))]
[TemplatePart(ChipsHostPart, typeof(SpringSizeDecorator))]
[TemplatePart(ChipsPart, typeof(TemplatedControl))]
[TemplatePart(ResultsScrollPart, typeof(ScrollViewer))]
[TemplatePart(ResultsAreaPart, typeof(Panel))]
[TemplatePart(ListHostPart, typeof(Panel))]
[TemplatePart(ListPart, typeof(StackPanel))]
[TemplatePart(CursorPart, typeof(Control))]
[TemplatePart(MorePart, typeof(AvaloniaButton))]
[TemplatePart(HintsPart, typeof(Control))]
[TemplatePart(ResultsPresencePart, typeof(PresenceHost))]
[TemplatePart(LoadingPresencePart, typeof(PresenceHost))]
[TemplatePart(EmptyPresencePart, typeof(PresenceHost))]
[TemplatePart(EmptyStatePart, typeof(EmptyState))]
[PseudoClasses(NarrowPseudoClass, LoadingPseudoClass, EmptyPseudoClass, NoFiltersPseudoClass, FilteredPseudoClass)]
public partial class SearchResults : TemplatedControl
{
    public const string FieldPart = "PART_Field";
    public const string FieldFramePart = "PART_FieldFrame";
    public const string SuggestionsPopupPart = "PART_SuggestionsPopup";
    public const string SuggestionSurfacePart = "PART_SuggestionSurface";
    public const string SuggestionListPart = "PART_SuggestionList";
    public const string FiltersColumnPart = "PART_FiltersColumn";
    public const string FiltersPart = "PART_Filters";
    public const string FiltersButtonPart = "PART_FiltersButton";
    public const string CountPart = "PART_Count";
    public const string CountTextPart = "PART_CountText";
    public const string ElapsedPart = "PART_Elapsed";
    public const string StatusPart = "PART_Status";
    public const string SortPart = "PART_Sort";
    public const string ChipsHostPart = "PART_ChipsHost";
    public const string ChipsPart = "PART_Chips";
    public const string ResultsScrollPart = "PART_ResultsScroll";
    public const string ResultsAreaPart = "PART_ResultsArea";
    public const string ListHostPart = "PART_ListHost";
    public const string ListPart = "PART_List";
    public const string CursorPart = "PART_Cursor";
    public const string MorePart = "PART_More";
    public const string HintsPart = "PART_Hints";
    public const string ResultsPresencePart = "PART_ResultsPresence";
    public const string LoadingPresencePart = "PART_LoadingPresence";
    public const string EmptyPresencePart = "PART_EmptyPresence";
    public const string EmptyStatePart = "PART_Empty";

    /// <summary>Set while the control is narrower than its breakpoint (560): the filters sit behind a button.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while <see cref="IsLoading"/> is true.</summary>
    public const string LoadingPseudoClass = ":loading";

    /// <summary>Set while nothing matches (the empty state shows).</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Set while <see cref="Filters"/> has no filter to show (no filters column).</summary>
    public const string NoFiltersPseudoClass = ":no-filters";

    /// <summary>Set while at least one filter narrows the results.</summary>
    public const string FilteredPseudoClass = ":filtered";

    #region 公共属性定义

    /// <summary>Items to search; when null the inline <see cref="Items"/> are used. Collection changes are followed.</summary>
    public static readonly StyledProperty<IEnumerable<SearchResultItem>?> ItemsSourceProperty =
        AvaloniaProperty.Register<SearchResults, IEnumerable<SearchResultItem>?>(nameof(ItemsSource));

    /// <summary>The text in the search field (two-way).</summary>
    public static readonly StyledProperty<string> QueryProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(Query), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How the results are ordered (two-way). Default <see cref="SearchSortOrder.BestMatch"/>.</summary>
    public static readonly StyledProperty<SearchSortOrder> SortOrderProperty =
        AvaloniaProperty.Register<SearchResults, SearchSortOrder>(nameof(SortOrder), SearchSortOrder.BestMatch, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How long the search took, shown after the count ("in 0.12 seconds"). Null shows nothing; the app measures it.</summary>
    public static readonly StyledProperty<TimeSpan?> ElapsedProperty =
        AvaloniaProperty.Register<SearchResults, TimeSpan?>(nameof(Elapsed));

    /// <summary>
    /// When true (default) the items are matched, filtered, counted and sorted here. Set it to false when a server
    /// searched: the items are shown as given, <see cref="SearchFilterValue.Count"/> is yours, and the query's terms are
    /// still marked.
    /// </summary>
    public static readonly StyledProperty<bool> IsFilteringEnabledProperty =
        AvaloniaProperty.Register<SearchResults, bool>(nameof(IsFilteringEnabled), true);

    /// <summary>Shows skeleton rows in place of the results (keeping the previous results' height).</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<SearchResults, bool>(nameof(IsLoading));

    /// <summary>Results listed first; more follow when the list is scrolled to its end or "Show more" is pressed. Default 15.</summary>
    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<SearchResults, int>(nameof(PageSize), 15);

    /// <summary>Whether the suggestions popup opens under the focused field. Default true.</summary>
    public static readonly StyledProperty<bool> IsSuggestionsEnabledProperty =
        AvaloniaProperty.Register<SearchResults, bool>(nameof(IsSuggestionsEnabled), true);

    /// <summary>Queries offered under "Suggested" in the popup and when nothing matches; choosing one fills the search field.</summary>
    public static readonly StyledProperty<IList<string>?> SuggestionsProperty =
        AvaloniaProperty.Register<SearchResults, IList<string>?>(nameof(Suggestions));

    /// <summary>How many recent searches are kept. Default 5.</summary>
    public static readonly StyledProperty<int> MaxRecentProperty =
        AvaloniaProperty.Register<SearchResults, int>(nameof(MaxRecent), 5);

    /// <summary>Whether Enter or a click opens the built-in detail. Turn it off when your app navigates on <see cref="ItemInvoked"/>.</summary>
    public static readonly StyledProperty<bool> IsDetailEnabledProperty =
        AvaloniaProperty.Register<SearchResults, bool>(nameof(IsDetailEnabled), true);

    /// <summary>The result whose detail is open, or null (two-way). Setting it opens that result's detail.</summary>
    public static readonly StyledProperty<SearchResultItem?> OpenedItemProperty =
        AvaloniaProperty.Register<SearchResults, SearchResultItem?>(nameof(OpenedItem), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Replaces the built-in content of the detail (everything between the header and the buttons); its data is the <see cref="SearchResultItem"/>.</summary>
    public static readonly StyledProperty<IDataTemplate?> DetailTemplateProperty =
        AvaloniaProperty.Register<SearchResults, IDataTemplate?>(nameof(DetailTemplate));

    /// <summary>Whether the keyboard hints show under the results. Default true.</summary>
    public static readonly StyledProperty<bool> IsHintsVisibleProperty =
        AvaloniaProperty.Register<SearchResults, bool>(nameof(IsHintsVisible), true);

    /// <summary>Names the search landmark and the search field.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(Label), "Search");

    /// <summary>Hint in the empty search field.</summary>
    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(PlaceholderText), "Search");

    /// <summary>The key hint on the field's keycap (the key that focuses it). Default "/".</summary>
    public static readonly StyledProperty<string> ShortcutTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(ShortcutText), "/");

    /// <summary>Executed with the opened <see cref="SearchResultItem"/>.</summary>
    public static readonly StyledProperty<ICommand?> InvokeCommandProperty =
        AvaloniaProperty.Register<SearchResults, ICommand?>(nameof(InvokeCommand));

    /// <summary>The result under the cursor, or null.</summary>
    public static readonly DirectProperty<SearchResults, SearchResultItem?> ActiveItemProperty =
        AvaloniaProperty.RegisterDirect<SearchResults, SearchResultItem?>(nameof(ActiveItem), o => o.ActiveItem);

    /// <summary>The number of results for the current query and filters.</summary>
    public static readonly DirectProperty<SearchResults, int> ResultCountProperty =
        AvaloniaProperty.RegisterDirect<SearchResults, int>(nameof(ResultCount), o => o.ResultCount);

    /// <summary>Raised when a result is opened with Enter or a click (before the detail shows; set Handled to skip the detail).</summary>
    public static readonly RoutedEvent<SearchResultInvokedEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<SearchResults, SearchResultInvokedEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when a button of the detail is pressed.</summary>
    public static readonly RoutedEvent<SearchResultActionEventArgs> DetailActionInvokedEvent =
        RoutedEvent.Register<SearchResults, SearchResultActionEventArgs>(nameof(DetailActionInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when the query, a filter or the sort order changed (ask your server again).</summary>
    public static readonly RoutedEvent<SearchChangedEventArgs> SearchChangedEvent =
        RoutedEvent.Register<SearchResults, SearchChangedEventArgs>(nameof(SearchChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a query is submitted: Enter in the field, or a suggestion chosen.</summary>
    public static readonly RoutedEvent<SearchQuerySubmittedEventArgs> QuerySubmittedEvent =
        RoutedEvent.Register<SearchResults, SearchQuerySubmittedEventArgs>(nameof(QuerySubmitted), RoutingStrategies.Bubble);

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable<SearchResultItem>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="QueryProperty"/>
    public string Query { get => GetValue(QueryProperty); set => SetValue(QueryProperty, value); }

    /// <inheritdoc cref="SortOrderProperty"/>
    public SearchSortOrder SortOrder { get => GetValue(SortOrderProperty); set => SetValue(SortOrderProperty, value); }

    /// <inheritdoc cref="ElapsedProperty"/>
    public TimeSpan? Elapsed { get => GetValue(ElapsedProperty); set => SetValue(ElapsedProperty, value); }

    /// <inheritdoc cref="IsFilteringEnabledProperty"/>
    public bool IsFilteringEnabled { get => GetValue(IsFilteringEnabledProperty); set => SetValue(IsFilteringEnabledProperty, value); }

    /// <inheritdoc cref="IsLoadingProperty"/>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <inheritdoc cref="PageSizeProperty"/>
    public int PageSize { get => GetValue(PageSizeProperty); set => SetValue(PageSizeProperty, value); }

    /// <inheritdoc cref="IsSuggestionsEnabledProperty"/>
    public bool IsSuggestionsEnabled { get => GetValue(IsSuggestionsEnabledProperty); set => SetValue(IsSuggestionsEnabledProperty, value); }

    /// <inheritdoc cref="SuggestionsProperty"/>
    public IList<string>? Suggestions { get => GetValue(SuggestionsProperty); set => SetValue(SuggestionsProperty, value); }

    /// <inheritdoc cref="MaxRecentProperty"/>
    public int MaxRecent { get => GetValue(MaxRecentProperty); set => SetValue(MaxRecentProperty, value); }

    /// <inheritdoc cref="IsDetailEnabledProperty"/>
    public bool IsDetailEnabled { get => GetValue(IsDetailEnabledProperty); set => SetValue(IsDetailEnabledProperty, value); }

    /// <inheritdoc cref="OpenedItemProperty"/>
    public SearchResultItem? OpenedItem { get => GetValue(OpenedItemProperty); set => SetValue(OpenedItemProperty, value); }

    /// <inheritdoc cref="DetailTemplateProperty"/>
    public IDataTemplate? DetailTemplate { get => GetValue(DetailTemplateProperty); set => SetValue(DetailTemplateProperty, value); }

    /// <inheritdoc cref="IsHintsVisibleProperty"/>
    public bool IsHintsVisible { get => GetValue(IsHintsVisibleProperty); set => SetValue(IsHintsVisibleProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="PlaceholderTextProperty"/>
    public string PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }

    /// <inheritdoc cref="ShortcutTextProperty"/>
    public string ShortcutText { get => GetValue(ShortcutTextProperty); set => SetValue(ShortcutTextProperty, value); }

    /// <inheritdoc cref="InvokeCommandProperty"/>
    public ICommand? InvokeCommand { get => GetValue(InvokeCommandProperty); set => SetValue(InvokeCommandProperty, value); }

    /// <inheritdoc cref="ActiveItemProperty"/>
    public SearchResultItem? ActiveItem { get => _activeItem; private set => SetAndRaise(ActiveItemProperty, ref _activeItem, value); }

    /// <inheritdoc cref="ResultCountProperty"/>
    public int ResultCount { get => _resultCount; private set => SetAndRaise(ResultCountProperty, ref _resultCount, value); }

    /// <summary>Inline items (used when <see cref="ItemsSource"/> is null).</summary>
    [Content]
    public AvaloniaList<SearchResultItem> Items { get; } = new();

    /// <summary>The filters of the column (and of the sheet on narrow screens). None: the results take the whole width.</summary>
    public AvaloniaList<SearchFilter> Filters { get; } = new();

    /// <summary>
    /// Recent searches, newest first. The control adds the query when one is submitted or a result is opened with it; seed
    /// it, bind it or save it as you like. At most <see cref="MaxRecent"/> are kept.
    /// </summary>
    public AvaloniaList<string> Recent { get; } = new();

    /// <summary>Raised when a result is opened with Enter or a click.</summary>
    public event EventHandler<SearchResultInvokedEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    /// <summary>Raised when "Open" or "Copy link" of the detail is pressed.</summary>
    public event EventHandler<SearchResultActionEventArgs>? DetailActionInvoked
    {
        add => AddHandler(DetailActionInvokedEvent, value);
        remove => RemoveHandler(DetailActionInvokedEvent, value);
    }

    /// <summary>Raised when the query, a filter or the sort order changed.</summary>
    public event EventHandler<SearchChangedEventArgs>? SearchChanged
    {
        add => AddHandler(SearchChangedEvent, value);
        remove => RemoveHandler(SearchChangedEvent, value);
    }

    /// <summary>Raised when a query is submitted: Enter in the field, or a suggestion chosen.</summary>
    public event EventHandler<SearchQuerySubmittedEventArgs>? QuerySubmitted
    {
        add => AddHandler(QuerySubmittedEvent, value);
        remove => RemoveHandler(QuerySubmittedEvent, value);
    }

    #endregion

    #region 文字属性

    /// <summary>The filters column title. Default "Filters".</summary>
    public static readonly StyledProperty<string> FiltersTitleProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(FiltersTitle), "Filters");

    /// <summary>The link that clears every filter (in the column and in the active filters bar). Default "Clear all".</summary>
    public static readonly StyledProperty<string> ClearAllTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(ClearAllText), "Clear all");

    /// <summary>The empty state's button that clears the filters. Default "Clear filters".</summary>
    public static readonly StyledProperty<string> ClearFiltersTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(ClearFiltersText), "Clear filters");

    /// <summary>Stats text for several results; {0} is the count (it rolls). Default "{0} results".</summary>
    public static readonly StyledProperty<string> ResultsFormatProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(ResultsFormat), "{0} results");

    /// <summary>Stats text for one result; {0} is the count. Default "{0} result".</summary>
    public static readonly StyledProperty<string> OneResultFormatProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(OneResultFormat), "{0} result");

    /// <summary>Stats text (and empty title without a query) when nothing matches. Default "No results".</summary>
    public static readonly StyledProperty<string> NoResultsTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(NoResultsText), "No results");

    /// <summary>Stats text while <see cref="IsLoading"/> is true. Default "Searching…".</summary>
    public static readonly StyledProperty<string> LoadingTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(LoadingText), "Searching…");

    /// <summary>The time after the count; {0} is the seconds. Default "in {0} seconds".</summary>
    public static readonly StyledProperty<string> ElapsedFormatProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(ElapsedFormat), "in {0} seconds");

    /// <summary>The "Best match" sort option.</summary>
    public static readonly StyledProperty<string> BestMatchTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(BestMatchText), "Best match");

    /// <summary>The "Newest" sort option.</summary>
    public static readonly StyledProperty<string> NewestTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(NewestText), "Newest");

    /// <summary>The "A to Z" sort option.</summary>
    public static readonly StyledProperty<string> TitleSortTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(TitleSortText), "A to Z");

    /// <summary>The group of recent searches in the popup. Default "Recent".</summary>
    public static readonly StyledProperty<string> RecentLabelProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(RecentLabel), "Recent");

    /// <summary>The group of suggested searches in the popup. Default "Suggested".</summary>
    public static readonly StyledProperty<string> SuggestedLabelProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(SuggestedLabel), "Suggested");

    /// <summary>The word before the suggestions of the empty state. Default "Try".</summary>
    public static readonly StyledProperty<string> TryTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(TryText), "Try");

    /// <summary>Empty state title; {0} is the query. Default "No results for “{0}”".</summary>
    public static readonly StyledProperty<string> EmptyTitleFormatProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(EmptyTitleFormat), "No results for “{0}”");

    /// <summary>Empty state description. Default "Check the spelling or try fewer words."</summary>
    public static readonly StyledProperty<string> EmptyDescriptionProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(EmptyDescription), "Check the spelling or try fewer words.");

    /// <summary>Empty state description while filters hide results. Default "The filters leave nothing. Remove one to see more."</summary>
    public static readonly StyledProperty<string> EmptyFilteredDescriptionProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(EmptyFilteredDescription), "The filters leave nothing. Remove one to see more.");

    /// <summary>Button under the listed results when more are left. Default "Show more".</summary>
    public static readonly StyledProperty<string> ShowMoreTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(ShowMoreText), "Show more");

    /// <summary>The kind and the location in a result's subtitle; {0} is the kind label, {1} the location. Default "{0} in {1}".</summary>
    public static readonly StyledProperty<string> KindLocationFormatProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(KindLocationFormat), "{0} in {1}");

    /// <summary>The key hint "to move". Default "to move,".</summary>
    public static readonly StyledProperty<string> HintMoveTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(HintMoveText), "to move,");

    /// <summary>The key hint "to open". Default "to open,".</summary>
    public static readonly StyledProperty<string> HintOpenTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(HintOpenText), "to open,");

    /// <summary>The key hint "to search". Default "to search".</summary>
    public static readonly StyledProperty<string> HintSearchTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(HintSearchText), "to search");

    /// <summary>The first row of the detail table: how often the query matches. Default "Matches".</summary>
    public static readonly StyledProperty<string> MatchesLabelProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(MatchesLabel), "Matches");

    /// <summary>The detail's primary button; {0} is the kind label in lower case. Default "Open {0}".</summary>
    public static readonly StyledProperty<string> OpenFormatProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(OpenFormat), "Open {0}");

    /// <summary>The detail's secondary button. Default "Copy link".</summary>
    public static readonly StyledProperty<string> CopyLinkTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(CopyLinkText), "Copy link");

    /// <summary>Names the close buttons of the detail and the filters sheet. Default "Close".</summary>
    public static readonly StyledProperty<string> CloseTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(CloseText), "Close");

    /// <summary>Names the clear button of the field. Default "Clear search".</summary>
    public static readonly StyledProperty<string> ClearSearchTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(ClearSearchText), "Clear search");

    /// <summary>The narrow layout's button that opens the filters. Default "Filters".</summary>
    public static readonly StyledProperty<string> FiltersButtonTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(FiltersButtonText), "Filters");

    /// <summary>The caption of a date filter without one; {0} lists its kinds ("docs and files"). Default "Applies to {0}".</summary>
    public static readonly StyledProperty<string> AppliesToFormatProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(AppliesToFormat), "Applies to {0}");

    /// <summary>The word between the last two kinds of the default caption. Default "and".</summary>
    public static readonly StyledProperty<string> AndTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(AndText), "and");

    /// <summary>Names the remove button of a filter chip. Default "Remove filter".</summary>
    public static readonly StyledProperty<string> RemoveFilterTextProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(RemoveFilterText), "Remove filter");

    /// <summary>The month and year format of a date range ("Sep 2025"). Default "MMM yyyy".</summary>
    public static readonly StyledProperty<string> DateFormatProperty =
        AvaloniaProperty.Register<SearchResults, string>(nameof(DateFormat), "MMM yyyy");

    /// <inheritdoc cref="FiltersTitleProperty"/>
    public string FiltersTitle { get => GetValue(FiltersTitleProperty); set => SetValue(FiltersTitleProperty, value); }

    /// <inheritdoc cref="ClearAllTextProperty"/>
    public string ClearAllText { get => GetValue(ClearAllTextProperty); set => SetValue(ClearAllTextProperty, value); }

    /// <inheritdoc cref="ClearFiltersTextProperty"/>
    public string ClearFiltersText { get => GetValue(ClearFiltersTextProperty); set => SetValue(ClearFiltersTextProperty, value); }

    /// <inheritdoc cref="ResultsFormatProperty"/>
    public string ResultsFormat { get => GetValue(ResultsFormatProperty); set => SetValue(ResultsFormatProperty, value); }

    /// <inheritdoc cref="OneResultFormatProperty"/>
    public string OneResultFormat { get => GetValue(OneResultFormatProperty); set => SetValue(OneResultFormatProperty, value); }

    /// <inheritdoc cref="NoResultsTextProperty"/>
    public string NoResultsText { get => GetValue(NoResultsTextProperty); set => SetValue(NoResultsTextProperty, value); }

    /// <inheritdoc cref="LoadingTextProperty"/>
    public string LoadingText { get => GetValue(LoadingTextProperty); set => SetValue(LoadingTextProperty, value); }

    /// <inheritdoc cref="ElapsedFormatProperty"/>
    public string ElapsedFormat { get => GetValue(ElapsedFormatProperty); set => SetValue(ElapsedFormatProperty, value); }

    /// <inheritdoc cref="BestMatchTextProperty"/>
    public string BestMatchText { get => GetValue(BestMatchTextProperty); set => SetValue(BestMatchTextProperty, value); }

    /// <inheritdoc cref="NewestTextProperty"/>
    public string NewestText { get => GetValue(NewestTextProperty); set => SetValue(NewestTextProperty, value); }

    /// <inheritdoc cref="TitleSortTextProperty"/>
    public string TitleSortText { get => GetValue(TitleSortTextProperty); set => SetValue(TitleSortTextProperty, value); }

    /// <inheritdoc cref="RecentLabelProperty"/>
    public string RecentLabel { get => GetValue(RecentLabelProperty); set => SetValue(RecentLabelProperty, value); }

    /// <inheritdoc cref="SuggestedLabelProperty"/>
    public string SuggestedLabel { get => GetValue(SuggestedLabelProperty); set => SetValue(SuggestedLabelProperty, value); }

    /// <inheritdoc cref="TryTextProperty"/>
    public string TryText { get => GetValue(TryTextProperty); set => SetValue(TryTextProperty, value); }

    /// <inheritdoc cref="EmptyTitleFormatProperty"/>
    public string EmptyTitleFormat { get => GetValue(EmptyTitleFormatProperty); set => SetValue(EmptyTitleFormatProperty, value); }

    /// <inheritdoc cref="EmptyDescriptionProperty"/>
    public string EmptyDescription { get => GetValue(EmptyDescriptionProperty); set => SetValue(EmptyDescriptionProperty, value); }

    /// <inheritdoc cref="EmptyFilteredDescriptionProperty"/>
    public string EmptyFilteredDescription { get => GetValue(EmptyFilteredDescriptionProperty); set => SetValue(EmptyFilteredDescriptionProperty, value); }

    /// <inheritdoc cref="ShowMoreTextProperty"/>
    public string ShowMoreText { get => GetValue(ShowMoreTextProperty); set => SetValue(ShowMoreTextProperty, value); }

    /// <inheritdoc cref="KindLocationFormatProperty"/>
    public string KindLocationFormat { get => GetValue(KindLocationFormatProperty); set => SetValue(KindLocationFormatProperty, value); }

    /// <inheritdoc cref="HintMoveTextProperty"/>
    public string HintMoveText { get => GetValue(HintMoveTextProperty); set => SetValue(HintMoveTextProperty, value); }

    /// <inheritdoc cref="HintOpenTextProperty"/>
    public string HintOpenText { get => GetValue(HintOpenTextProperty); set => SetValue(HintOpenTextProperty, value); }

    /// <inheritdoc cref="HintSearchTextProperty"/>
    public string HintSearchText { get => GetValue(HintSearchTextProperty); set => SetValue(HintSearchTextProperty, value); }

    /// <inheritdoc cref="MatchesLabelProperty"/>
    public string MatchesLabel { get => GetValue(MatchesLabelProperty); set => SetValue(MatchesLabelProperty, value); }

    /// <inheritdoc cref="OpenFormatProperty"/>
    public string OpenFormat { get => GetValue(OpenFormatProperty); set => SetValue(OpenFormatProperty, value); }

    /// <inheritdoc cref="CopyLinkTextProperty"/>
    public string CopyLinkText { get => GetValue(CopyLinkTextProperty); set => SetValue(CopyLinkTextProperty, value); }

    /// <inheritdoc cref="CloseTextProperty"/>
    public string CloseText { get => GetValue(CloseTextProperty); set => SetValue(CloseTextProperty, value); }

    /// <inheritdoc cref="ClearSearchTextProperty"/>
    public string ClearSearchText { get => GetValue(ClearSearchTextProperty); set => SetValue(ClearSearchTextProperty, value); }

    /// <inheritdoc cref="FiltersButtonTextProperty"/>
    public string FiltersButtonText { get => GetValue(FiltersButtonTextProperty); set => SetValue(FiltersButtonTextProperty, value); }

    /// <inheritdoc cref="AppliesToFormatProperty"/>
    public string AppliesToFormat { get => GetValue(AppliesToFormatProperty); set => SetValue(AppliesToFormatProperty, value); }

    /// <inheritdoc cref="AndTextProperty"/>
    public string AndText { get => GetValue(AndTextProperty); set => SetValue(AndTextProperty, value); }

    /// <inheritdoc cref="RemoveFilterTextProperty"/>
    public string RemoveFilterText { get => GetValue(RemoveFilterTextProperty); set => SetValue(RemoveFilterTextProperty, value); }

    /// <inheritdoc cref="DateFormatProperty"/>
    public string DateFormat { get => GetValue(DateFormatProperty); set => SetValue(DateFormatProperty, value); }

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

    internal static readonly StyledProperty<double> RowTextInsetProperty =
        AvaloniaProperty.Register<SearchResults, double>(nameof(RowTextInset), 96);

    internal static readonly StyledProperty<Thickness> SkeletonInsetProperty =
        AvaloniaProperty.Register<SearchResults, Thickness>(nameof(SkeletonInset), new Thickness(20, 14));

    internal static readonly StyledProperty<Thickness> ListMarginProperty =
        AvaloniaProperty.Register<SearchResults, Thickness>(nameof(ListMargin), new Thickness(-4, 12, -4, 0));

    internal static readonly StyledProperty<double> SkeletonGapProperty =
        AvaloniaProperty.Register<SearchResults, double>(nameof(SkeletonGap), 8);

    internal Thickness SkeletonInset { get => GetValue(SkeletonInsetProperty); set => SetValue(SkeletonInsetProperty, value); }
    internal Thickness ListMargin { get => GetValue(ListMarginProperty); set => SetValue(ListMarginProperty, value); }
    internal double SkeletonGap { get => GetValue(SkeletonGapProperty); set => SetValue(SkeletonGapProperty, value); }

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }
    internal int SnippetLead { get => GetValue(SnippetLeadProperty); set => SetValue(SnippetLeadProperty, value); }
    internal int SnippetMaxLength { get => GetValue(SnippetMaxLengthProperty); set => SetValue(SnippetMaxLengthProperty, value); }
    internal int SnippetMaxLines { get => GetValue(SnippetMaxLinesProperty); set => SetValue(SnippetMaxLinesProperty, value); }

    /// <summary>Horizontal space a row spends outside its snippet text (inset, avatar column, pane padding).</summary>
    internal double RowTextInset { get => GetValue(RowTextInsetProperty); set => SetValue(RowTextInsetProperty, value); }

    #endregion

    private SearchResultItem? _activeItem;
    private int _resultCount;
    private INotifyCollectionChanged? _observedSource;
    private bool _isNarrow;
    private bool _syncingText;
    private bool _keyboardInput;

    static SearchResults()
    {
        FocusableProperty.OverrideDefaultValue<SearchResults>(false);
        SearchResultsEngine.WarmUpCollation();
    }

    /// <summary>Creates an empty search results page.</summary>
    public SearchResults()
    {
        Items.CollectionChanged += OnItemsChanged;
        Filters.CollectionChanged += OnFiltersChanged;
        Recent.CollectionChanged += OnRecentChanged;
        _navigator = new ListKeyboardNavigator(() => _rows.Count, i => i >= 0 && i < _rows.Count && _rows[i].IsEnabled) { Wrap = false };
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Search);
        AutomationProperties.SetName(this, Label);
        AddHandler(KeyDownEvent, OnTunnelKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, OnTunnelPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        PseudoClasses.Set(NoFiltersPseudoClass, true);
        Update(reveal: false, resetCursor: true, recompile: true);
    }

    private IReadOnlyList<SearchResultItem> SourceItems => ItemsSource switch
    {
        null => Items,
        IReadOnlyList<SearchResultItem> list => list,
        { } items => items.ToList()
    };

    #region 模板

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Detach();
        base.OnApplyTemplate(e);
        _field = e.NameScope.Find<SearchResultsField>(FieldPart);
        _fieldFrame = e.NameScope.Find<Panel>(FieldFramePart);
        _popup = e.NameScope.Find<ArcPopup>(SuggestionsPopupPart);
        _suggestionSurface = e.NameScope.Find<Border>(SuggestionSurfacePart);
        _suggestionList = e.NameScope.Find<SearchResultsSuggestionList>(SuggestionListPart);
        _filtersColumn = e.NameScope.Find<Border>(FiltersColumnPart);
        _filtersView = e.NameScope.Find<SearchResultsFilters>(FiltersPart);
        _filtersButton = e.NameScope.Find<AvaloniaButton>(FiltersButtonPart);
        _count = e.NameScope.Find<RollingDigits>(CountPart);
        _countText = e.NameScope.Find<TextBlock>(CountTextPart);
        _elapsed = e.NameScope.Find<RollingDigits>(ElapsedPart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        _sort = e.NameScope.Find<SegmentedControl>(SortPart);
        _chipsHost = e.NameScope.Find<SpringSizeDecorator>(ChipsHostPart);
        _chipBar = e.NameScope.Find<SearchResultsChipBar>(ChipsPart);
        _resultsScroll = e.NameScope.Find<ScrollViewer>(ResultsScrollPart);
        _resultsArea = e.NameScope.Find<Panel>(ResultsAreaPart);
        _listHost = e.NameScope.Find<SearchResultsListHost>(ListHostPart);
        _list = e.NameScope.Find<StackPanel>(ListPart);
        _cursorView = e.NameScope.Find<SearchResultsCursor>(CursorPart);
        _moreButton = e.NameScope.Find<AvaloniaButton>(MorePart);
        _hints = e.NameScope.Find<Control>(HintsPart);
        _resultsPresence = e.NameScope.Find<PresenceHost>(ResultsPresencePart);
        _loadingPresence = e.NameScope.Find<PresenceHost>(LoadingPresencePart);
        _emptyPresence = e.NameScope.Find<PresenceHost>(EmptyPresencePart);
        _emptyState = e.NameScope.Find<EmptyState>(EmptyStatePart);

        AttachField();
        AttachFilters();
        AttachResults();
        if (_sort is not null)
        {
            _sort.ItemsSource = BuildSortOptions();
            _sort.SelectionChanged += OnSortSelectionChanged;
            SyncSort();
        }
        _cursor = -1;
        UpdateFieldTexts();
        PseudoClasses.Set(LoadingPseudoClass, IsLoading);
        Update(reveal: false, resetCursor: true, recompile: true);
    }

    private void Detach()
    {
        DetachField();
        DetachFilters();
        DetachResults();
        if (_sort is not null)
        {
            _sort.SelectionChanged -= OnSortSelectionChanged;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == QueryProperty)
        {
            if (_field is not null && !_syncingText && _field.Text != Query)
            {
                _field.Text = Query;
            }
            _pageLimit = Math.Max(1, PageSize);
            Update(reveal: true, resetCursor: true);
            RaiseSearchChanged(SearchChangeReason.Query);
        }
        else if (property == SortOrderProperty)
        {
            _pageLimit = Math.Max(1, PageSize);
            SyncSort();
            Update(reveal: true, resetCursor: true);
            RaiseSearchChanged(SearchChangeReason.Sort);
        }
        else if (property == ItemsSourceProperty)
        {
            ObserveSource();
            Update(reveal: true, resetCursor: true, recompile: true);
        }
        else if (property == ElapsedProperty)
        {
            UpdateStats();
        }
        else if (property == PageSizeProperty)
        {
            _pageLimit = Math.Max(1, PageSize);
            Update(reveal: false, resetCursor: false);
        }
        else if (property == IsFilteringEnabledProperty || property == SnippetLeadProperty || property == SnippetMaxLengthProperty ||
                 property == SnippetMaxLinesProperty || property == KindLocationFormatProperty)
        {
            Update(reveal: false, resetCursor: false, recompile: property == IsFilteringEnabledProperty);
        }
        else if (property == IsLoadingProperty)
        {
            ApplyLoading();
        }
        else if (property == ResultsFormatProperty || property == OneResultFormatProperty || property == NoResultsTextProperty ||
                 property == LoadingTextProperty || property == ElapsedFormatProperty)
        {
            UpdateStats();
            UpdateEmpty();
        }
        else if (property == SuggestionsProperty || property == TryTextProperty || property == EmptyTitleFormatProperty ||
                 property == EmptyDescriptionProperty || property == EmptyFilteredDescriptionProperty || property == ClearFiltersTextProperty)
        {
            UpdateEmpty();
            if (property == SuggestionsProperty && _suggestionsOpen)
            {
                RefreshSuggestions(keepOpen: true);
            }
        }
        else if (property == LabelProperty || property == PlaceholderTextProperty || property == ShortcutTextProperty ||
                 property == ClearSearchTextProperty || property == FiltersTitleProperty || property == ClearAllTextProperty ||
                 property == FiltersButtonTextProperty || property == RemoveFilterTextProperty || property == DateFormatProperty ||
                 property == CloseTextProperty || property == HintMoveTextProperty || property == HintOpenTextProperty ||
                 property == HintSearchTextProperty)
        {
            AutomationProperties.SetName(this, Label);
            UpdateFieldTexts();
        }
        else if (property == BestMatchTextProperty || property == NewestTextProperty || property == TitleSortTextProperty)
        {
            if (_sort is not null)
            {
                _sort.ItemsSource = BuildSortOptions();
                SyncSort();
            }
        }
        else if (property == AppliesToFormatProperty || property == AndTextProperty)
        {
            RebuildFilterViews(force: true);
        }
        else if (property == IsSuggestionsEnabledProperty || property == MaxRecentProperty)
        {
            if (property == MaxRecentProperty)
            {
                TrimRecent();
            }
            if (!IsSuggestionsEnabled)
            {
                CloseSuggestions();
            }
        }
        else if (property == OpenedItemProperty)
        {
            OnOpenedItemChanged(change.GetOldValue<SearchResultItem?>(), change.GetNewValue<SearchResultItem?>());
        }
        else if (property == NarrowWidthProperty)
        {
            UpdateNarrow(Bounds.Width);
        }
        else if (property == IsHintsVisibleProperty)
        {
            if (_hints is not null)
            {
                _hints.IsVisible = IsHintsVisible && !IsLoading && ResultCount > 0;
            }
        }
    }

    private void UpdateFieldTexts()
    {
        if (_field is not null)
        {
            AutomationProperties.SetName(_field, Label);
            _field.PlaceholderText = PlaceholderText;
            _field.ClearText = ClearSearchText;
            _field.ShortcutText = ShortcutText;
        }
        if (_filtersView is not null)
        {
            _filtersView.Title = FiltersTitle;
            _filtersView.ClearAllText = ClearAllText;
        }
        if (_chipBar is not null)
        {
            _chipBar.ClearAllText = ClearAllText;
            _chipBar.RemoveText = RemoveFilterText;
        }
        if (_sheetFilters is not null)
        {
            _sheetFilters.ClearAllText = ClearAllText;
        }
        if (_filtersSurface is not null)
        {
            _filtersSurface.Title = FiltersTitle;
            _filtersSurface.CloseText = CloseText;
        }
        if (_detailSurface is not null)
        {
            _detailSurface.CloseText = CloseText;
        }
        if (_filtersButton is not null)
        {
            AutomationProperties.SetName(_filtersButton, FiltersButtonText);
        }
        if (_listHost is not null)
        {
            AutomationProperties.SetName(_listHost, CountText(ResultCount));
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveSource();
        if (_watchedFilters.Count == 0 && Filters.Count > 0)
        {
            SubscribeFilterValues();
            Update(reveal: false, resetCursor: false, recompile: true);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceChanged;
            _observedSource = null;
        }
        CloseSuggestions();
        _detailPresence?.Close();
        _filtersPresence?.Close();
        UnsubscribeFilterValues();
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

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => Update(reveal: true, resetCursor: true, recompile: true);

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ItemsSource is null)
        {
            Update(reveal: true, resetCursor: true, recompile: true);
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
        if (!narrow)
        {
            _filtersPresence?.Close();
        }
        QueueCursor(SearchResultsCursorMove.Snap, scroll: false);
    }

    private void RaiseSearchChanged(SearchChangeReason reason) => RaiseEvent(new SearchChangedEventArgs(SearchChangedEvent, reason));

    #endregion

    #region 公共方法

    /// <summary>Runs the search again (after you change an item or a filter definition in place).</summary>
    public void Refresh() => Update(reveal: false, resetCursor: false, recompile: true);

    /// <summary>Moves keyboard focus into the search field, with the caret after the query.</summary>
    public void FocusSearch() => FocusSearch(NavigationMethod.Pointer, selectAll: false);

    /// <summary>Unticks every value and resets every date range.</summary>
    public void ClearFilters()
    {
        foreach (var filter in Filters)
        {
            filter.Clear();
        }
    }

    /// <summary>Opens the detail of <paramref name="item"/> (the same as setting <see cref="OpenedItem"/>).</summary>
    public void ShowDetail(SearchResultItem item) => OpenedItem = item;

    /// <summary>Closes the detail.</summary>
    public void CloseDetail() => OpenedItem = null;

    #endregion
}
