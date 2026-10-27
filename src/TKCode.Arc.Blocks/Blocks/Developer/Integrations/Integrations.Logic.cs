using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(SubtitlePart, typeof(TextBlock))]
[TemplatePart(SearchPart, typeof(SearchField))]
[TemplatePart(FiltersPart, typeof(IntegrationFilterBar))]
[TemplatePart(RowsPart, typeof(DeveloperListPanel))]
[TemplatePart(EmptyPart, typeof(ArcContentSwap))]
[TemplatePart(AnnouncerPart, typeof(DeveloperAnnouncer))]
[TemplatePart(DrawerPart, typeof(Drawer))]
[PseudoClasses(NarrowPseudoClass, TightPseudoClass, CompactPseudoClass, FramedPseudoClass)]
public partial class Integrations
{
    internal const string TitlePart = "PART_Title";
    internal const string SubtitlePart = "PART_Subtitle";
    internal const string SearchPart = "PART_Search";
    internal const string FiltersPart = "PART_Filters";
    internal const string RowsPart = "PART_Rows";
    internal const string EmptyPart = "PART_Empty";
    internal const string AnnouncerPart = "PART_Announcer";
    internal const string DrawerPart = "PART_Drawer";
    internal const string NarrowPseudoClass = ":narrow";
    internal const string TightPseudoClass = ":tight";
    internal const string CompactPseudoClass = ":compact";
    internal const string FramedPseudoClass = ":framed";

    private readonly IntegrationSheet _sheet;
    private readonly HashSet<IntegrationItem> _tracked = new();
    private readonly Dictionary<IntegrationItem, CancellationTokenSource> _pending = new();
    private readonly Dictionary<IntegrationItem, int> _attempts = new();
    private readonly Dictionary<IntegrationItem, Exception?> _failures = new();
    private readonly HashSet<IntegrationItem> _disconnecting = new();
    private List<IntegrationItem> _visible = new();
    private INotifyCollectionChanged? _observedSource;
    private TextBlock? _title;
    private TextBlock? _subtitle;
    private SearchField? _search;
    private IntegrationFilterBar? _filters;
    private DeveloperListPanel? _rows;
    private ArcContentSwap? _empty;
    private IntegrationEmpty? _emptyView;
    private DeveloperAnnouncer? _announcer;
    private Drawer? _drawer;
    private Control? _drawerTrigger;
    private bool _syncingSearch;
    private bool _isNarrow;
    private bool _isTight;

    public Integrations()
    {
        _sheet = new IntegrationSheet { Owner = this };
        Items.CollectionChanged += OnItemsChanged;
        Categories.CollectionChanged += OnCategoriesChanged;
        SyncOptions.CollectionChanged += OnSyncOptionsChanged;
        PseudoClasses.Set(FramedPseudoClass, true);
    }

    /// <summary>The sync options as a list (the sheet reads them).</summary>
    internal IReadOnlyList<IntegrationSyncOption> EffectiveSyncOptions => SyncOptions;

    /// <summary>The apps shown now, in order (tests).</summary>
    internal IReadOnlyList<IntegrationItem> VisibleItems => _visible;

    /// <summary>The drawer's body (tests).</summary>
    internal IntegrationSheet Sheet => _sheet;

    /// <summary>The live region (tests and demos read its history).</summary>
    internal DeveloperAnnouncer? Announcer => _announcer;

    internal IntegrationFilterBar? FilterBar => _filters;

    internal DeveloperListPanel? RowsPanel => _rows;

    internal SearchField? SearchBox => _search;

    internal TextBlock? TitleBlock => _title;

    internal Drawer? DrawerPanel => _drawer;

    internal IntegrationEmpty? EmptyView => _visible.Count == 0 ? _emptyView : null;

    /// <summary>The row of a shown app, or null.</summary>
    internal IntegrationRow? RowFor(IntegrationItem item) => _rows?.ContainerFor(item) as IntegrationRow;

    #region Public methods

    /// <summary>
    /// Connects <paramref name="item"/> as if its Connect pill were pressed (no effect while it connects or is connected).
    /// Completes with true once it is connected, false when it failed, was cancelled or did not start.
    /// </summary>
    public Task<bool> ConnectAsync(IntegrationItem item) => RunConnectAsync(item, null);

    /// <summary>Cancels a running connect of <paramref name="item"/>; it returns to idle.</summary>
    public void CancelConnect(IntegrationItem item)
    {
        if (_pending.TryGetValue(item, out var cts))
        {
            cts.Cancel();
        }
    }

    /// <summary>Disconnects <paramref name="item"/> without the hold (runs <see cref="DisconnectAction"/>, no settle delay).</summary>
    public Task<bool> DisconnectAsync(IntegrationItem item) => RunDisconnectAsync(item, fromHold: false);

    /// <summary>Opens the drawer of <paramref name="item"/>.</summary>
    public void OpenConfiguration(IntegrationItem item) => OpenConfiguration(item, null);

    /// <summary>Closes the drawer.</summary>
    public void CloseConfiguration() => _drawer?.Close();

    /// <summary>Empties the search (the list shows the checked filter again).</summary>
    public void ClearSearch() => SetCurrentValue(SearchTextProperty, string.Empty);

    /// <summary>Moves the keyboard focus to the search field.</summary>
    public bool FocusSearch() => _search?.Focus(NavigationMethod.Tab) == true;

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_search is not null)
        {
            _search.TextChanged -= OnSearchTextChanged;
        }
        if (_filters is not null)
        {
            _filters.SelectionRequested -= OnFilterRequested;
        }
        if (_rows is not null)
        {
            _rows.ItemPrepared -= OnRowPrepared;
            _rows.Items = null;
        }
        if (_drawer is not null)
        {
            _drawer.Closed -= OnDrawerClosed;
            _drawer.Content = null;
        }
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _subtitle = e.NameScope.Find<TextBlock>(SubtitlePart);
        _search = e.NameScope.Find<SearchField>(SearchPart);
        _filters = e.NameScope.Find<IntegrationFilterBar>(FiltersPart);
        _rows = e.NameScope.Find<DeveloperListPanel>(RowsPart);
        _empty = e.NameScope.Find<ArcContentSwap>(EmptyPart);
        _announcer = e.NameScope.Find<DeveloperAnnouncer>(AnnouncerPart);
        _drawer = e.NameScope.Find<Drawer>(DrawerPart);
        if (_search is not null)
        {
            _search.Text = SearchText;
            _search.TextChanged += OnSearchTextChanged;
        }
        if (_filters is not null)
        {
            _filters.SelectionRequested += OnFilterRequested;
        }
        if (_rows is not null)
        {
            // The list frame's height follows on the outer SpringSizeDecorator (one spring for rows and empty state).
            _rows.Motion = DeveloperListMotion.Filter with { Container = null };
            _rows.ItemTemplate = new FuncDataTemplate<IntegrationItem>((item, _) => CreateRow(item));
            _rows.ItemPrepared += OnRowPrepared;
        }
        if (_drawer is not null)
        {
            _drawer.Content = _sheet;
            _drawer.Closed += OnDrawerClosed;
        }
        _visible = new List<IntegrationItem>();
        UpdateTexts();
        Track();
        Recompute(RecomputeReason.Data);
        if (OpenItem is { } open)
        {
            _sheet.Show(open);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Leaving the window cancels running connects (they return to idle).
        foreach (var cts in _pending.Values.ToList())
        {
            cts.Cancel();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            if (_observedSource is not null)
            {
                _observedSource.CollectionChanged -= OnItemsChanged;
            }
            _observedSource = ItemsSource as INotifyCollectionChanged;
            if (_observedSource is not null)
            {
                _observedSource.CollectionChanged += OnItemsChanged;
            }
            Track();
            Recompute(RecomputeReason.Data);
        }
        else if (change.Property == SelectedFilterProperty)
        {
            var old = change.GetOldValue<string>() ?? AllFilter;
            Recompute(RecomputeReason.Filter);
            RaiseEvent(new IntegrationFilterChangedEventArgs(FilterChangedEvent, this, old, SelectedFilter, VisibleCount));
        }
        else if (change.Property == SearchTextProperty)
        {
            if (_search is not null && !_syncingSearch && _search.Text != SearchText)
            {
                _syncingSearch = true;
                _search.Text = SearchText;
                _syncingSearch = false;
            }
            Recompute(RecomputeReason.Search);
            RaiseEvent(new IntegrationSearchChangedEventArgs(SearchChangedEvent, this, SearchText ?? string.Empty, VisibleCount));
        }
        else if (change.Property == ShowConnectedFilterProperty)
        {
            Recompute(RecomputeReason.Data);
        }
        else if (change.Property == LabelsProperty || change.Property == TitleProperty || change.Property == SubtitleProperty)
        {
            UpdateTexts();
            UpdateRows();
            UpdateFilters();
            UpdateEmpty();
            _sheet.Refresh(animate: false);
        }
        else if (change.Property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
        }
        else if (change.Property == NarrowWidthProperty || change.Property == TightWidthProperty || change.Property == CompactWidthProperty)
        {
            UpdateBreakpoints(Bounds.Width);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
    }

    /// <summary>Sets :narrow, :tight and :compact from the control's own width, with 1px of hysteresis at each threshold.</summary>
    private void UpdateBreakpoints(double width)
    {
        if (!(width > 0))
        {
            return;
        }
        _isNarrow = _isNarrow ? width < NarrowWidth + 1 : width < NarrowWidth;
        _isTight = _isTight ? width <= TightWidth + 1 : width <= TightWidth;
        var wasTight = _isTight;
        var compact = IsCompact ? width <= CompactWidth + 1 : width <= CompactWidth;
        PseudoClasses.Set(NarrowPseudoClass, _isNarrow);
        PseudoClasses.Set(TightPseudoClass, _isTight);
        PseudoClasses.Set(CompactPseudoClass, compact);
        if (compact != IsCompact)
        {
            IsCompact = compact;
            if (_filters is not null)
            {
                _filters.IsCompact = compact;
            }
            UpdateRows();
        }
        else if (wasTight != _isTight)
        {
            UpdateRows();
        }
    }

    #region Data

    private IEnumerable<IntegrationItem> EffectiveItems =>
        ItemsSource is { } source ? source.OfType<IntegrationItem>() : Items;

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ReferenceEquals(sender, Items) && ItemsSource is not null)
        {
            return;
        }
        Track();
        if (OpenItem is { } open && !EffectiveItems.Contains(open))
        {
            CloseConfiguration();
        }
        Recompute(RecomputeReason.Data);
    }

    private void OnCategoriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (IntegrationCategory category in e.OldItems)
            {
                category.PropertyChanged -= OnCategoryChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (IntegrationCategory category in e.NewItems)
            {
                category.PropertyChanged += OnCategoryChanged;
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var category in Categories)
            {
                category.PropertyChanged -= OnCategoryChanged;
                category.PropertyChanged += OnCategoryChanged;
            }
        }
        Recompute(RecomputeReason.Data);
    }

    private void OnCategoryChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Recompute(RecomputeReason.Data);

    private void OnSyncOptionsChanged(object? sender, NotifyCollectionChangedEventArgs e) => _sheet.Refresh(animate: false);

    private void Track()
    {
        var now = new HashSet<IntegrationItem>(EffectiveItems);
        foreach (var gone in _tracked.Where(i => !now.Contains(i)).ToList())
        {
            gone.PropertyChanged -= OnTrackedItemChanged;
            _tracked.Remove(gone);
            CancelConnect(gone);
        }
        foreach (var item in now)
        {
            if (_tracked.Add(item))
            {
                item.PropertyChanged += OnTrackedItemChanged;
            }
        }
    }

    private void OnTrackedItemChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not IntegrationItem item)
        {
            return;
        }
        if (e.Property == IntegrationItem.StatusProperty)
        {
            _failures.Remove(item, out var failure);
            var oldStatus = (IntegrationStatus)e.OldValue!;
            var newStatus = (IntegrationStatus)e.NewValue!;
            Recompute(RecomputeReason.Data);
            RaiseEvent(new IntegrationStatusChangedEventArgs(StatusChangedEvent, this, item, oldStatus, newStatus,
                newStatus == IntegrationStatus.Error ? failure : null));
        }
        else if (e.Property == IntegrationItem.NameProperty || e.Property == IntegrationItem.CategoryProperty ||
                 e.Property == IntegrationItem.DescriptionProperty)
        {
            Recompute(RecomputeReason.Data);
            if (ReferenceEquals(item, OpenItem))
            {
                UpdateDrawerHeader(item);
            }
        }
    }

    private string? CategoryLabel(string? key)
    {
        if (key is null)
        {
            return null;
        }
        foreach (var category in Categories)
        {
            if (category.Key == key)
            {
                return category.Label;
            }
        }
        return null;
    }

    /// <summary>The filter that applies: the selected one when it exists, else All.</summary>
    private string EffectiveFilter
    {
        get
        {
            var filter = SelectedFilter;
            if (filter == ConnectedFilter)
            {
                return ShowConnectedFilter ? filter : AllFilter;
            }
            if (filter == AllFilter || string.IsNullOrEmpty(filter))
            {
                return AllFilter;
            }
            return Categories.Any(c => c.Key == filter) ? filter : AllFilter;
        }
    }

    private enum RecomputeReason
    {
        Data,
        Filter,
        Search
    }

    private void Recompute(RecomputeReason reason)
    {
        var items = EffectiveItems.ToList();
        var filter = EffectiveFilter;
        var next = IntegrationsQuery.Visible(items, filter, SearchText, CategoryLabel);
        var refocus = FindFocusTargetForLeavingRow(next);
        _visible = next;
        if (_rows is not null)
        {
            _rows.Items = next;
        }
        UpdateRows();
        if (refocus is { } target)
        {
            if (target is IntegrationItem survivor && RowFor(survivor) is { } row)
            {
                row.FocusFirst(NavigationMethod.Tab);
            }
            else
            {
                _filters?.FocusSelected(NavigationMethod.Tab);
            }
        }
        VisibleCount = next.Count;
        ConnectedCount = IntegrationsQuery.Count(items, ConnectedFilter);
        UpdateFilters();
        UpdateEmpty();
    }

    /// <summary>
    /// When the focus is in a row about to leave, where it goes after the change: the next row that stays, else the previous
    /// one, else the filters (a sentinel). Null when nothing needs to move.
    /// </summary>
    private object? FindFocusTargetForLeavingRow(List<IntegrationItem> next)
    {
        if (_rows is null)
        {
            return null;
        }
        var index = -1;
        for (var i = 0; i < _visible.Count; i++)
        {
            if (RowFor(_visible[i]) is { IsKeyboardFocusWithin: true })
            {
                index = i;
                break;
            }
        }
        if (index < 0 || next.Contains(_visible[index]))
        {
            return null;
        }
        for (var i = index + 1; i < _visible.Count; i++)
        {
            if (next.Contains(_visible[i]))
            {
                return _visible[i];
            }
        }
        for (var i = index - 1; i >= 0; i--)
        {
            if (next.Contains(_visible[i]))
            {
                return _visible[i];
            }
        }
        return this;
    }

    private void UpdateTexts()
    {
        if (_title is not null)
        {
            _title.Text = Title ?? Labels.Title;
        }
        if (_subtitle is not null)
        {
            var subtitle = Subtitle ?? Labels.Subtitle;
            _subtitle.Text = subtitle;
            _subtitle.IsVisible = !string.IsNullOrEmpty(subtitle);
        }
        if (_search is not null)
        {
            _search.Placeholder = Labels.SearchPlaceholder;
            _search.Label = Labels.SearchName;
            Avalonia.Automation.AutomationProperties.SetName(_search, Labels.SearchName);
        }
        if (_filters is not null)
        {
            Avalonia.Automation.AutomationProperties.SetName(_filters, Labels.FilterGroupName);
        }
        if (_rows is not null)
        {
            Avalonia.Automation.AutomationProperties.SetName(_rows, Labels.ListName);
        }
    }

    private Control CreateRow(IntegrationItem? item)
    {
        var row = new IntegrationRow { Owner = this, Labels = Labels, IsCompact = IsCompact, IsTight = _isTight };
        if (item is not null)
        {
            row.CategoryText = CategoryLabel(item.Category);
            row.Item = item;
        }
        return row;
    }

    private void OnRowPrepared(object? sender, DeveloperListItemEventArgs e)
    {
        if (e.Visual is IntegrationRow row && e.Item is IntegrationItem item)
        {
            row.ShowsDivider = _visible.IndexOf(item) > 0;
        }
    }

    private void UpdateRows()
    {
        if (_rows is null)
        {
            return;
        }
        for (var i = 0; i < _visible.Count; i++)
        {
            if (RowFor(_visible[i]) is { } row)
            {
                row.Labels = Labels;
                row.IsCompact = IsCompact;
                row.IsTight = _isTight;
                row.CategoryText = CategoryLabel(_visible[i].Category);
                row.ShowsDivider = i > 0;
            }
        }
    }

    private void UpdateFilters()
    {
        if (_filters is null)
        {
            return;
        }
        var items = EffectiveItems.ToList();
        var choices = new List<IntegrationFilterChoice>
        {
            new(AllFilter, Labels.FilterAll, items.Count),
        };
        if (ShowConnectedFilter)
        {
            choices.Add(new IntegrationFilterChoice(ConnectedFilter, Labels.FilterConnected, IntegrationsQuery.Count(items, ConnectedFilter)));
        }
        foreach (var category in Categories)
        {
            choices.Add(new IntegrationFilterChoice(category.Key, category.Label, IntegrationsQuery.Count(items, category.Key)));
        }
        _filters.IsCompact = IsCompact;
        _filters.Update(choices, EffectiveFilter, Labels.FilterName);
    }

    private void UpdateEmpty()
    {
        if (_empty is null)
        {
            return;
        }
        if (_visible.Count > 0)
        {
            _empty.Content = null;
            return;
        }
        if (_emptyView is null)
        {
            _emptyView = new IntegrationEmpty();
            _emptyView.ActionInvoked += OnEmptyAction;
        }
        var query = IntegrationsQuery.Normalize(SearchText);
        var filter = EffectiveFilter;
        var labels = Labels;
        if (query.Length > 0)
        {
            _emptyView.Kind = IntegrationEmptyKind.Search;
            _emptyView.Title = labels.EmptySearchTitle((SearchText ?? string.Empty).Trim());
            _emptyView.Hint = labels.EmptySearchHint;
            _emptyView.ActionText = labels.ClearSearch;
        }
        else if (filter == ConnectedFilter)
        {
            _emptyView.Kind = IntegrationEmptyKind.Connected;
            _emptyView.Title = labels.EmptyConnectedTitle;
            _emptyView.Hint = labels.EmptyConnectedHint;
            _emptyView.ActionText = labels.BrowseAll;
        }
        else
        {
            _emptyView.Kind = IntegrationEmptyKind.Category;
            _emptyView.Title = labels.EmptyCategoryTitle;
            _emptyView.Hint = null;
            _emptyView.ActionText = filter == AllFilter ? null : labels.BrowseAll;
        }
        _empty.Content = _emptyView;
    }

    private void OnEmptyAction(object? sender, EventArgs e)
    {
        if (_emptyView is null)
        {
            return;
        }
        if (_emptyView.Kind == IntegrationEmptyKind.Search)
        {
            // Arc puts the focus back in the field after clearing it.
            ClearSearch();
            _search?.Focus(NavigationMethod.Tab);
            Announce(Labels.AnnounceShown(VisibleCount));
            return;
        }
        SelectFilterFromUser(AllFilter);
        _filters?.FocusSelected(NavigationMethod.Tab);
    }

    #endregion

    #region Search and filters

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingSearch || _search is null)
        {
            return;
        }
        var text = _search.Text ?? string.Empty;
        if (text == SearchText)
        {
            return;
        }
        _syncingSearch = true;
        try
        {
            SetCurrentValue(SearchTextProperty, text);
        }
        finally
        {
            _syncingSearch = false;
        }
        var query = IntegrationsQuery.Normalize(text);
        Announce(query.Length > 0 ? Labels.AnnounceMatches(VisibleCount, query) : Labels.AnnounceShown(VisibleCount));
    }

    private void OnFilterRequested(object? sender, string key) => SelectFilterFromUser(key);

    private void SelectFilterFromUser(string key)
    {
        if (key == SelectedFilter)
        {
            return;
        }
        SetCurrentValue(SelectedFilterProperty, key);
        var label = key == AllFilter || key == ConnectedFilter ? null : CategoryLabel(key);
        Announce(label is null ? Labels.AnnounceShown(VisibleCount) : Labels.AnnounceShownIn(VisibleCount, label));
    }

    private void Announce(string text) => _announcer?.Announce(text);

    #endregion

    #region Connect and disconnect

    /// <summary>A pill was pressed (row or drawer).</summary>
    internal void ConnectFromUser(IntegrationItem item, Control? origin) => _ = RunConnectAsync(item, origin);

    private async Task<bool> RunConnectAsync(IntegrationItem item, Control? origin)
    {
        if (item.Status is IntegrationStatus.Connecting or IntegrationStatus.Connected)
        {
            return false;
        }
        var attempt = (_attempts.TryGetValue(item, out var previous) ? previous : 0) + 1;
        var request = new IntegrationConnectRequestedEventArgs(ConnectRequestedEvent, this, item, attempt);
        RaiseEvent(request);
        if (request.Cancel)
        {
            return false;
        }
        if (ConnectCommand is { } command)
        {
            if (!command.CanExecute(item))
            {
                return false;
            }
            command.Execute(item);
        }
        _attempts[item] = attempt;
        var cts = new CancellationTokenSource();
        _pending[item] = cts;
        item.SetCurrentValue(IntegrationItem.ErrorTextProperty, null);
        item.SetCurrentValue(IntegrationItem.StatusProperty, IntegrationStatus.Connecting);
        Announce(Labels.AnnounceConnecting(item.Name));

        IntegrationConnectResult? result = null;
        Exception? error = null;
        var cancelled = false;
        try
        {
            if (ConnectAction is { } action)
            {
                result = await action(new IntegrationConnectRequest(item, attempt), cts.Token);
            }
            else
            {
                if (SimulatedConnectDuration > TimeSpan.Zero)
                {
                    await Task.Delay(SimulatedConnectDuration, cts.Token);
                }
                result = IntegrationConnectResult.Connected();
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            cancelled = true;
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            if (_pending.TryGetValue(item, out var current) && ReferenceEquals(current, cts))
            {
                _pending.Remove(item);
            }
            cts.Dispose();
        }

        if (item.Status != IntegrationStatus.Connecting)
        {
            // Changed by code while it ran: leave it as it is.
            return false;
        }
        if (cancelled)
        {
            item.SetCurrentValue(IntegrationItem.StatusProperty, IntegrationStatus.Idle);
            return false;
        }
        var focus = CaptureConnectFocus(item, origin);
        if (error is not null || result is { IsSuccess: false })
        {
            var text = error is not null ? FormatError(error) : result!.ErrorText;
            item.SetCurrentValue(IntegrationItem.ErrorTextProperty, string.IsNullOrWhiteSpace(text) ? null : text);
            _failures[item] = error;
            item.SetCurrentValue(IntegrationItem.StatusProperty, IntegrationStatus.Error);
            Announce(Labels.AnnounceFailed(item.Name));
            return false;
        }
        item.SetCurrentValue(IntegrationItem.ConnectedByProperty, result?.ConnectedBy);
        item.SetCurrentValue(IntegrationItem.ConnectedAtProperty, result?.ConnectedAt ?? DateTimeOffset.Now);
        item.SetCurrentValue(IntegrationItem.IsConnectedByCurrentUserProperty, result?.ConnectedBy is null);
        if (item.SyncFrequency is null && SyncOptions.Count > 0)
        {
            item.SetCurrentValue(IntegrationItem.SyncFrequencyProperty, SyncOptions[0].Key);
        }
        item.SetCurrentValue(IntegrationItem.StatusProperty, IntegrationStatus.Connected);
        Announce(Labels.AnnounceConnected(item.Name));
        RestoreConnectFocus(item, focus);
        return true;
    }

    private string? FormatError(Exception error)
    {
        if (ErrorFormatter is { } formatter)
        {
            try
            {
                return formatter(error);
            }
            catch (Exception)
            {
                return null;
            }
        }
        return string.IsNullOrWhiteSpace(error.Message) ? null : error.Message;
    }

    private enum ConnectFocus
    {
        None,
        Row,
        Sheet
    }

    private readonly record struct FocusCapture(ConnectFocus Where, NavigationMethod Method);

    private FocusCapture CaptureConnectFocus(IntegrationItem item, Control? origin)
    {
        static NavigationMethod MethodOf(Control control) =>
            control.Classes.Contains(":focus-visible") ? NavigationMethod.Tab : NavigationMethod.Pointer;

        if (_sheet.Pill is { IsFocused: true } sheetPill && ReferenceEquals(_sheet.Item, item))
        {
            return new FocusCapture(ConnectFocus.Sheet, MethodOf(sheetPill));
        }
        if (RowFor(item)?.Pill is { IsFocused: true } rowPill)
        {
            return new FocusCapture(ConnectFocus.Row, MethodOf(rowPill));
        }
        return new FocusCapture(ConnectFocus.None, NavigationMethod.Unspecified);
    }

    private void RestoreConnectFocus(IntegrationItem item, FocusCapture focus)
    {
        // Arc moves the focus from the removed Connect button to the new settings button; the drawer's pill turns into a
        // status label, so the focus goes on to the settings that just opened.
        switch (focus.Where)
        {
            case ConnectFocus.Row when RowFor(item) is { } row:
                row.FocusAfterConnect?.Focus(focus.Method);
                break;
            case ConnectFocus.Sheet:
                _sheet.FocusSettings(focus.Method);
                break;
        }
    }

    /// <summary>The drawer's hold completed.</summary>
    internal void DisconnectFromHold(IntegrationItem item) => _ = RunDisconnectAsync(item, fromHold: true);

    private async Task<bool> RunDisconnectAsync(IntegrationItem item, bool fromHold)
    {
        if (item.Status != IntegrationStatus.Connected || !_disconnecting.Add(item))
        {
            if (fromHold && ReferenceEquals(_sheet.Item, item))
            {
                _sheet.ResetHold();
            }
            return false;
        }
        try
        {
            if (DisconnectAction is { } action)
            {
                await action(item, CancellationToken.None);
            }
            if (fromHold && DisconnectSettleDelay > TimeSpan.Zero)
            {
                // Let "Unlinked" show before the drawer returns to idle.
                await Task.Delay(DisconnectSettleDelay);
            }
        }
        catch (Exception)
        {
            _disconnecting.Remove(item);
            if (ReferenceEquals(_sheet.Item, item))
            {
                _sheet.ResetHold();
            }
            Announce(Labels.AnnounceDisconnectFailed(item.Name));
            return false;
        }
        _disconnecting.Remove(item);
        if (item.Status != IntegrationStatus.Connected)
        {
            return false;
        }
        var sheetHadFocus = OpenItem == item && _sheet.IsKeyboardFocusWithin;
        var sheetMethod = sheetHadFocus && _sheet.Hold is { } hold && hold.Classes.Contains(":focus-visible")
            ? NavigationMethod.Tab
            : NavigationMethod.Pointer;
        var row = RowFor(item);
        var configureHadFocus = row?.ConfigureButton is { IsFocused: true };
        item.SetCurrentValue(IntegrationItem.ConnectedByProperty, null);
        item.SetCurrentValue(IntegrationItem.ConnectedAtProperty, null);
        item.SetCurrentValue(IntegrationItem.IsConnectedByCurrentUserProperty, false);
        item.SetCurrentValue(IntegrationItem.StatusProperty, IntegrationStatus.Idle);
        DisconnectCommand?.Execute(item);
        Announce(Labels.AnnounceDisconnected(item.Name));
        if (sheetHadFocus)
        {
            // Arc: the focus lands on the drawer's Connect pill.
            _sheet.Pill?.Focus(sheetMethod);
        }
        else if (configureHadFocus && RowFor(item) is { } stillThere)
        {
            stillThere.Pill?.Focus(NavigationMethod.Tab);
        }
        return true;
    }

    /// <summary>The drawer's frequency menu changed.</summary>
    internal void ChangeFrequency(IntegrationItem item, IntegrationSyncOption option)
    {
        var old = item.SyncFrequency;
        if (old == option.Key)
        {
            return;
        }
        item.SetCurrentValue(IntegrationItem.SyncFrequencyProperty, option.Key);
        RaiseEvent(new IntegrationSyncFrequencyChangedEventArgs(SyncFrequencyChangedEvent, this, item, old, option.Key));
        Announce(Labels.AnnounceSync(item.Name, option.Phrase ?? option.Label));
    }

    #endregion

    #region Drawer

    /// <summary>Opens the drawer for <paramref name="item"/>; the focus returns to <paramref name="trigger"/> when it closes.</summary>
    internal void OpenConfiguration(IntegrationItem item, Control? trigger)
    {
        _drawerTrigger = trigger ?? TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        OpenItem = item;
        _sheet.Show(item);
        UpdateDrawerHeader(item);
        if (_drawer is { } drawer)
        {
            if (drawer.IsOpen)
            {
                return;
            }
            drawer.Open();
        }
        RaiseEvent(new IntegrationEventArgs(ConfigurationOpenedEvent, this, item));
    }

    private void UpdateDrawerHeader(IntegrationItem item)
    {
        if (_drawer is null)
        {
            return;
        }
        _drawer.Title = item.Name;
        _drawer.Description = item.Description;
    }

    private void OnDrawerClosed(object? sender, EventArgs e)
    {
        var item = OpenItem;
        OpenItem = null;
        if (item is not null)
        {
            RaiseEvent(new IntegrationEventArgs(ConfigurationClosedEvent, this, item));
        }
        var trigger = _drawerTrigger;
        _drawerTrigger = null;
        Dispatcher.UIThread.Post(() => RestoreFocusAfterDrawer(trigger, item));
    }

    /// <summary>
    /// After the drawer closed: when the focus did not land anywhere real (its trigger is gone, as when the app left the
    /// Connected filter), it goes to the trigger's row if the app is still listed, else to the heading (Arc's h2 fallback).
    /// </summary>
    private void RestoreFocusAfterDrawer(Control? trigger, IntegrationItem? item)
    {
        if (TopLevel.GetTopLevel(this) is not { FocusManager: { } focusManager })
        {
            return;
        }
        var focused = focusManager.GetFocusedElement() as Visual;
        var surface = _drawer?.Surface;
        var lost = focused is null || !focused.IsAttachedToVisualTree() ||
                   (surface is not null && (ReferenceEquals(focused, surface) || surface.IsVisualAncestorOf(focused)));
        if (!lost)
        {
            return;
        }
        if (trigger is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } && trigger.IsAttachedToVisualTree() &&
            trigger.Focus(NavigationMethod.Tab))
        {
            return;
        }
        if (item is not null && RowFor(item) is { } row && row.FocusFirst(NavigationMethod.Tab))
        {
            return;
        }
        _title?.Focus(NavigationMethod.Tab);
    }

    #endregion
}
