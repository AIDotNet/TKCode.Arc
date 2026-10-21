using System.Globalization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class SearchResults
{
    private const double RevealStaggerMs = 35;
    private const double RevealDurationMs = 240;
    private const int RevealMaxStagger = 3;
    private const int RevealMaxRows = 4;
    private const double RevealLift = 4;
    private static readonly Easing s_linear = new LinearEasing();

    // Search state, reused between keystrokes.
    private readonly SearchResultsEngine _engine = new();
    private readonly List<string> _terms = new();
    private int _pageLimit = 15;
    private int _snippetBudget = 160;
    private bool _budgetQueued;
    private bool _hasMore;

    // Visual pool: rows are reused, never re-templated per keystroke. _rows is the shown prefix of the pool.
    private readonly List<SearchResultRow> _rows = new();
    private readonly List<SearchResultRow> _rowPool = new();
    private HashSet<SearchResultItem> _shownItems = new();
    private HashSet<SearchResultItem> _previousItems = new();

    // Template parts.
    private ScrollViewer? _resultsScroll;
    private Panel? _resultsArea;
    private SearchResultsListHost? _listHost;
    private StackPanel? _list;
    private SearchResultsCursor? _cursorView;
    private AvaloniaButton? _moreButton;
    private Control? _hints;
    private PresenceHost? _resultsPresence;
    private PresenceHost? _loadingPresence;
    private PresenceHost? _emptyPresence;
    private EmptyState? _emptyState;
    private RollingDigits? _count;
    private TextBlock? _countText;
    private RollingDigits? _elapsed;
    private TextBlock? _status;
    private SegmentedControl? _sort;

    // Empty state actions.
    private StackPanel? _emptyActions;
    private AvaloniaButton? _clearFiltersButton;
    private WrapPanel? _suggestionRow;
    private TextBlock? _suggestionLabel;
    private readonly List<AvaloniaButton> _suggestionPool = new();

    // Cursor.
    private int _cursor = -1;
    private SearchResultsCursorMove _cursorMove;
    private bool _cursorScroll;
    private bool _cursorQueued;
    private Point _lastPointer = new(double.NaN, double.NaN);
    private readonly ListKeyboardNavigator _navigator;

    // Reveal: one clock drives the staggered fade and rise of new rows.
    private readonly List<Control> _revealing = new();
    private MotionValue<double>? _revealClock;
    private bool _revealLift;

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

    /// <summary>All results for the query and filters, in order (including the ones not listed yet).</summary>
    internal IReadOnlyList<SearchResultItem> AllResults => _engine.Hits.Select(h => h.Item).ToList();

    /// <summary>The engine (tests).</summary>
    internal SearchResultsEngine Engine => _engine;

    /// <summary>The shown rows (tests).</summary>
    internal IReadOnlyList<SearchResultRow> Rows => _rows;

    /// <summary>Index of the cursor row, or -1.</summary>
    internal int CursorIndex => _cursor;

    /// <summary>The cursor block (tests).</summary>
    internal SearchResultsCursor? CursorView => _cursorView;

    /// <summary>The search field (tests).</summary>
    internal SearchResultsField? SearchFieldControl => _field;

    /// <summary>The list that takes focus (tests).</summary>
    internal SearchResultsListHost? ListHostControl => _listHost;

    /// <summary>The empty state (tests).</summary>
    internal EmptyState? EmptyStateControl => _emptyState;

    /// <summary>The empty state's "Clear filters" button and suggestion chips (tests).</summary>
    internal AvaloniaButton? ClearFiltersButton => _clearFiltersButton;

    internal IReadOnlyList<AvaloniaButton> SuggestionButtons => _suggestionPool.Where(b => b.IsVisible).ToList();

    /// <summary>The "Show more" button (tests).</summary>
    internal AvaloniaButton? MoreButton => _moreButton;

    /// <summary>The count, elapsed time and sort parts (tests).</summary>
    internal RollingDigits? CountDigits => _count;

    internal RollingDigits? ElapsedDigits => _elapsed;

    internal TextBlock? CountTextBlock => _countText;

    internal SegmentedControl? SortControl => _sort;

    /// <summary>The stats text as assistive technology reads it (tests).</summary>
    internal string StatsText => _status?.Text ?? string.Empty;

    /// <summary>The pane's footer (tests).</summary>
    internal Control? HintsControl => _hints;

    #endregion

    private void AttachResults()
    {
        if (_listHost is not null)
        {
            _listHost.KeyDown += OnListKeyDown;
            _listHost.TextInput += OnListTextInput;
            _listHost.GotFocus += OnListGotFocus;
            _listHost.KeyboardFocusChanged += OnListKeyboardFocusChanged;
            _listHost.SizeChanged += OnListSizeChanged;
        }
        if (_resultsScroll is not null)
        {
            _resultsScroll.ScrollChanged += OnResultsScrolled;
        }
        if (_moreButton is not null)
        {
            _moreButton.Click += OnMoreClick;
        }
        if (_list is not null)
        {
            _list.Children.Clear();
        }
        _rows.Clear();
    }

    private void DetachResults()
    {
        if (_listHost is not null)
        {
            _listHost.KeyDown -= OnListKeyDown;
            _listHost.TextInput -= OnListTextInput;
            _listHost.GotFocus -= OnListGotFocus;
            _listHost.KeyboardFocusChanged -= OnListKeyboardFocusChanged;
            _listHost.SizeChanged -= OnListSizeChanged;
        }
        if (_resultsScroll is not null)
        {
            _resultsScroll.ScrollChanged -= OnResultsScrolled;
        }
        if (_moreButton is not null)
        {
            _moreButton.Click -= OnMoreClick;
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

    #region 搜索与列表

    /// <summary>
    /// Runs the search and rebuilds what depends on it. A keystroke costs the matching (the previous matches when the
    /// query only grew), the filter pass with the counts, and re-marking the listed rows that changed.
    /// </summary>
    private void Update(bool reveal, bool resetCursor, bool recompile = false)
    {
        if (_compiling)
        {
            return;
        }
        var source = SourceItems;
        if (recompile || _plansDirty)
        {
            _compiling = true;
            try
            {
                _engine.Compile(source, Filters);
            }
            finally
            {
                _compiling = false;
            }
            _plansDirty = false;
            SubscribeFilterValues();
            RebuildFilterViews(force: false);
        }
        _engine.ReadSelections();
        _engine.RunQuery(source, Query, _terms, IsFilteringEnabled);
        _engine.RunFilters(IsFilteringEnabled, count: IsFilteringEnabled, SortOrder, _terms.Count > 0);
        WriteCounts();
        ResultCount = _engine.Hits.Count;
        BuildList(reveal, resetCursor);
        UpdateStats();
        UpdateStates();
        UpdateChips();
    }

    /// <summary>Copies the counts the engine computed onto the filter values (changed ones only).</summary>
    private void WriteCounts()
    {
        if (!IsFilteringEnabled)
        {
            return;
        }
        foreach (var plan in _engine.Plans)
        {
            if (plan.Values is not { } filter)
            {
                continue;
            }
            var count = Math.Min(plan.Counts.Length, filter.Values.Count);
            for (var v = 0; v < count; v++)
            {
                var value = filter.Values[v];
                if (value.Count != plan.Counts[v])
                {
                    value.Count = plan.Counts[v];
                }
            }
        }
    }

    private void BuildList(bool reveal, bool resetCursor)
    {
        if (_list is null)
        {
            return;
        }
        _snippetBudget = MeasureSnippetBudget();
        var keep = resetCursor ? null : ActiveItem;
        if (resetCursor && _resultsScroll is { Offset.Y: > 0 } viewer)
        {
            // A new search starts at the top.
            viewer.Offset = default;
        }
        var listFocused = _listHost?.IsKeyboardFocusWithin == true;
        var hits = _engine.Hits;
        var shown = Math.Min(hits.Count, Math.Max(1, _pageLimit));
        ResetReveal();
        if (_cursor >= 0 && _cursor < _rows.Count)
        {
            _rows[_cursor].SetActive(false);
        }
        (_previousItems, _shownItems) = (_shownItems, _previousItems);
        _shownItems.Clear();
        var window = new SnippetWindow(SnippetLead, _snippetBudget, Math.Max(SnippetMaxLength, _snippetBudget + 24));
        var query = Query ?? string.Empty;

        for (var i = 0; i < shown; i++)
        {
            var item = hits[i].Item;
            SearchResultRow row;
            if (i < _rows.Count)
            {
                row = _rows[i];
            }
            else
            {
                row = RentRow();
                _rows.Add(row);
                _list.Children.Add(row);
            }
            row.Index = i;
            if (!row.Shows(item, query, _snippetBudget))
            {
                row.SetData(item, SubtitleOf(item), KindLabelOf(item), query, _terms, window);
            }
            _shownItems.Add(item);
            if (reveal && _revealing.Count < RevealMaxRows && !_previousItems.Contains(item))
            {
                _revealing.Add(row);
            }
        }
        while (_rows.Count > shown)
        {
            var last = _rows[^1];
            _rows.RemoveAt(_rows.Count - 1);
            last.Release();
            _list.Children.Remove(last);
        }
        _hasMore = hits.Count > shown;
        if (_moreButton is not null)
        {
            _moreButton.IsVisible = _hasMore;
            _moreButton.Content = ShowMoreText;
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

        // The cursor stays on its result when the list only grew or relabelled; a new search puts it on the first
        // result, the one Enter opens.
        var target = keep is not null ? IndexOfRow(keep) : -1;
        if (target < 0 && !IsLoading)
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

    /// <summary>Appends the next page of results to the list (the search itself is unchanged).</summary>
    private void GrowList()
    {
        if (!_hasMore)
        {
            return;
        }
        _pageLimit += Math.Max(1, PageSize);
        BuildList(reveal: false, resetCursor: false);
        UpdateStates();
    }

    private string KindLabelOf(SearchResultItem item)
        => !string.IsNullOrEmpty(item.KindLabel) ? item.KindLabel : item.Kind ?? string.Empty;

    /// <summary>The line under a title: the item's own subtitle, else "Doc in Engineering / Handbook".</summary>
    internal string SubtitleOf(SearchResultItem item)
    {
        if (!string.IsNullOrEmpty(item.Subtitle))
        {
            return item.Subtitle;
        }
        var kind = KindLabelOf(item);
        var location = item.Location ?? string.Empty;
        if (kind.Length > 0 && location.Length > 0)
        {
            return Format(KindLocationFormat, kind, location);
        }
        return location.Length > 0 ? location : kind;
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
            width = Bounds.Width > 0 ? Bounds.Width * 0.6 : 640;
        }
        var textWidth = Math.Max(120, width - (_list is { Bounds.Width: > 0 } ? RowTextInset : 0));
        var perLine = textWidth / Math.Max(1, FontSize * 0.55);
        var lines = Math.Max(1, SnippetMaxLines);
        return Math.Max(16, (int)(perLine * lines) - 8 * lines);
    }

    private SearchResultRow RentRow()
    {
        if (_rows.Count < _rowPool.Count)
        {
            return _rowPool[_rows.Count];
        }
        var created = new SearchResultRow();
        created.PointerMoved += OnRowPointerMoved;
        created.PointerReleased += OnRowPointerReleased;
        _rowPool.Add(created);
        return created;
    }

    private void OnMoreClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var first = _rows.Count;
        GrowList();
        FocusList(first < _rows.Count ? first : FirstEnabledRow(), _keyboardInput ? NavigationMethod.Directional : NavigationMethod.Pointer);
    }

    private bool _growQueued;

    private bool IsNearEnd()
        => _hasMore && _resultsScroll is { } viewer && viewer.Viewport.Height > 0 && viewer.Offset.Y + viewer.Viewport.Height >= viewer.Extent.Height - 240;

    private void OnResultsScrolled(object? sender, ScrollChangedEventArgs e)
    {
        if (!_growQueued && IsNearEnd())
        {
            // Loading from inside the scroll notification would change the extent mid-pass; and by the time it runs a new
            // search may have reset the page, so the position is looked at again.
            _growQueued = true;
            Dispatcher.UIThread.Post(() =>
            {
                _growQueued = false;
                if (IsNearEnd())
                {
                    GrowList();
                }
            }, DispatcherPriority.Background);
        }
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

    private string CountText(int count)
    {
        if (count == 0)
        {
            return NoResultsText;
        }
        return Format(count == 1 ? OneResultFormat : ResultsFormat, count.ToString("N0", CultureInfo.CurrentCulture));
    }

    /// <summary>Splits a "{0} results" style format around the placeholder.</summary>
    private static (string Prefix, string Suffix) SplitFormat(string format)
    {
        var at = format.IndexOf("{0}", StringComparison.Ordinal);
        return at < 0 ? (format, string.Empty) : (format[..at], format[(at + 3)..]);
    }

    private void UpdateStats()
    {
        var count = ResultCount;
        var loading = IsLoading;
        var digits = !loading && count > 0;
        var text = loading ? LoadingText : CountText(count);
        if (_count is not null)
        {
            var (prefix, suffix) = SplitFormat(count == 1 ? OneResultFormat : ResultsFormat);
            if (digits)
            {
                _count.Prefix = prefix;
                _count.Suffix = suffix;
                _count.Value = count;
            }
            _count.IsVisible = digits;
        }
        if (_countText is not null)
        {
            _countText.IsVisible = !digits;
            _countText.Text = loading ? LoadingText : NoResultsText;
        }
        var showElapsed = digits && Elapsed is not null;
        if (_elapsed is not null)
        {
            if (showElapsed)
            {
                var (prefix, suffix) = SplitFormat(ElapsedFormat);
                _elapsed.Prefix = prefix;
                _elapsed.Suffix = suffix;
                _elapsed.Value = Math.Round(Elapsed!.Value.TotalSeconds, 2);
            }
            _elapsed.IsVisible = showElapsed;
        }
        if (showElapsed)
        {
            text += " " + Format(ElapsedFormat, Elapsed!.Value.TotalSeconds.ToString("0.00", CultureInfo.CurrentCulture));
        }
        if (_status is not null && _status.Text != text)
        {
            _status.Text = text;
        }
        if (_hints is not null)
        {
            _hints.IsVisible = IsHintsVisible && digits;
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
            Update(reveal: true, resetCursor: true);
            return;
        }
        UpdateStats();
        UpdateStates();
    }

    /// <summary>The skeleton rows, built the first time loading starts (a hidden skeleton would still animate and lay out).</summary>
    private void EnsureSkeleton()
    {
        if (_loadingPresence is null || _loadingPresence.Child is not null)
        {
            return;
        }
        var rows = new StackPanel { Spacing = SkeletonGap, Margin = ListMargin };
        for (var i = 0; i < 3; i++)
        {
            rows.Children.Add(new Skeleton { Lines = 3, IsAvatarVisible = true, Margin = SkeletonInset });
        }
        _loadingPresence.Child = rows;
    }

    private void UpdateStates()
    {
        var loading = IsLoading;
        if (loading)
        {
            EnsureSkeleton();
        }
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
        var filtered = HasActiveFilters;
        _emptyState.Title = query.Length == 0 ? NoResultsText : Format(EmptyTitleFormat, query);
        _emptyState.Label = _emptyState.Title;
        _emptyState.Description = filtered ? EmptyFilteredDescription : EmptyDescription;
        EnsureEmptyActions();
        _clearFiltersButton!.Content = ClearFiltersText;
        _clearFiltersButton.IsVisible = filtered;
        var suggestions = Suggestions;
        var count = suggestions?.Count ?? 0;
        _suggestionLabel!.Text = TryText;
        for (var i = 0; i < count; i++)
        {
            if (i >= _suggestionPool.Count)
            {
                var chip = new AvaloniaButton();
                chip.Classes.Add("suggestion");
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
        var hasActions = filtered || count > 0;
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
        _clearFiltersButton = new Button { Variant = ButtonVariant.Secondary, Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Center };
        _clearFiltersButton.Click += (_, e) =>
        {
            e.Handled = true;
            ClearFilters();
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
            Children = { _clearFiltersButton, _suggestionRow }
        };
    }

    private void OnSuggestionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is AvaloniaButton { Tag: string suggestion })
        {
            e.Handled = true;
            SetCurrentValue(QueryProperty, suggestion);
            SubmitQuery(suggestion);
            FocusSearch(_keyboardInput ? NavigationMethod.Tab : NavigationMethod.Pointer, selectAll: false);
        }
    }

    #endregion

    #region 进场

    /// <summary>
    /// Results that were not listed before fade in and rise 4px, 35ms apart (at most six rows, five steps); rows that stay
    /// put do not move, so typing that only narrows the list animates nothing. One clock drives every row
    /// (code-driven: the rows are pooled and the delays depend on how many are new, which a ControlTheme cannot
    /// express). Reduced motion only fades; Off shows them at once.
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
            child.Opacity = 1;
            if (child.RenderTransform is TranslateTransform lift)
            {
                lift.Y = 0;
            }
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
                BuildList(reveal: false, resetCursor: false);
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
        SubmitQuery(Query, raise: false);
        var args = new SearchResultInvokedEventArgs(ItemInvokedEvent, item);
        RaiseEvent(args);
        if (InvokeCommand is { } command && command.CanExecute(item))
        {
            command.Execute(item);
        }
        if (!args.Handled && IsDetailEnabled)
        {
            OpenedItem = item;
        }
    }

    #endregion

    #region 结果列表键盘

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
            case Key.PageDown or Key.Down when _hasMore && _cursor == _rows.Count - 1:
                // At the last listed result: the next page follows.
                GrowList();
                break;
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
        // The average row, so Page Up undoes Page Down whatever the rows' heights are.
        var height = _list is { Bounds.Height: > 0 } ? _list.Bounds.Height / _rows.Count : _rows[Math.Max(0, _cursor)].Bounds.Height;
        return height > 0 ? Math.Max(1, (int)(_resultsScroll.Viewport.Height / height) - 1) : 5;
    }

    private void OnListTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text) || _field is null)
        {
            return;
        }
        e.Handled = true;
        if (e.Text == "/")
        {
            FocusSearch(NavigationMethod.Tab, selectAll: true);
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

    #endregion
}
