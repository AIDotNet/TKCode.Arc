using TKCode.Arc.DesignTokens;
using System.Collections.Specialized;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

public partial class AgentGallery
{
    private Panel? _host;
    private TextBlock? _title;
    private TextBlock? _subtitle;
    private SearchField? _search;
    private PillFilterBar? _categories;
    private TextBlock? _count;
    private SegmentedControl? _sort;
    private AgentGalleryGrid? _grid;
    private Control? _empty;
    private TextBlock? _emptyTitle;
    private TextBlock? _emptyHint;
    private Button? _clearSearch;
    private Button? _showAll;
    private Control? _detail;
    private Button? _back;
    private AgentGalleryMark? _detailMark;
    private TextBlock? _detailName;
    private TextBlock? _detailMaker;
    private Control? _detailVerified;
    private TextBlock? _detailStats;
    private Button? _detailInstall;
    private Control? _detailInstalled;
    private Button? _uninstall;
    private Control? _detailError;
    private TextBlock? _detailErrorText;
    private TextBlock? _description;
    private TextBlock? _abilitiesHeading;
    private Panel? _abilities;
    private TextBlock? _permissionsHeading;
    private Panel? _permissions;
    private TextBlock? _promptsHeading;
    private Panel? _prompts;

    private readonly AiAnnouncer _announcer = new();
    private readonly List<AgentGalleryCard> _cards = new();
    private List<AgentGalleryItem> _shown = new();
    private readonly Dictionary<string, CancellationTokenSource> _installing = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _failures = new(StringComparer.Ordinal);
    private readonly HashSet<string> _uninstalling = new(StringComparer.Ordinal);
    private (string Id, string Message)? _uninstallError;
    private INotifyCollectionChanged? _observedAgents;
    private MotionValue<double>? _fade;
    private int _current;
    private bool _keyboardMode;
    private bool _syncing;
    private string? _shownDetailId;

    static AgentGallery()
    {
        FocusableProperty.OverrideDefaultValue<AgentGallery>(false);
    }

    #region Read-only views (tests and tools)

    /// <summary>The agents on screen, in order.</summary>
    internal IReadOnlyList<AgentGalleryItem> Shown => _shown;

    /// <summary>The cards on screen, in order.</summary>
    internal IReadOnlyList<AgentGalleryCard> ShownCards => _cards.Take(_shown.Count).ToList();

    /// <summary>The card showing <paramref name="agentId"/>, or null.</summary>
    internal AgentGalleryCard? CardOf(string agentId)
    {
        var index = _shown.FindIndex(a => a.Id == agentId);
        return index >= 0 && index < _cards.Count ? _cards[index] : null;
    }

    internal SearchField? SearchBox => _search;
    internal PillFilterBar? CategoryBar => _categories;
    internal SegmentedControl? SortSwitch => _sort;
    internal AgentGalleryGrid? CardGrid => _grid;
    internal Control? EmptyView => _empty;
    internal TextBlock? EmptyTitle => _emptyTitle;
    internal Button? ClearSearchButton => _clearSearch;
    internal Button? ShowAllButton => _showAll;
    internal TextBlock? CountText => _count;
    internal TextBlock? TitleText => _title;
    internal TextBlock? SubtitleText => _subtitle;
    internal Control? DetailView => _detail;
    internal Button? BackButton => _back;
    internal TextBlock? DetailName => _detailName;
    internal TextBlock? DetailStats => _detailStats;
    internal Button? DetailInstallButton => _detailInstall;
    internal Control? DetailInstalledBadge => _detailInstalled;
    internal Button? UninstallButton => _uninstall;
    internal Control? DetailError => _detailError;
    internal TextBlock? DetailErrorText => _detailErrorText;
    internal Panel? AbilitiesList => _abilities;
    internal Panel? PermissionsList => _permissions;
    internal Panel? PromptsList => _prompts;

    /// <summary>The last polite announcement.</summary>
    internal string? LastAnnouncement => _announcer.Announcement;

    #endregion

    #region Public operations

    /// <summary>Where <paramref name="agentId"/> stands: available, installing, installed or failed.</summary>
    /// <param name="agentId">The agent's id.</param>
    public AgentGalleryInstallState GetInstallState(string agentId)
    {
        if (_installing.ContainsKey(agentId))
        {
            return AgentGalleryInstallState.Installing;
        }
        if (InstalledIds.Contains(agentId))
        {
            return AgentGalleryInstallState.Installed;
        }
        return _failures.ContainsKey(agentId) ? AgentGalleryInstallState.Failed : AgentGalleryInstallState.Available;
    }

    /// <summary>Opens the detail view of <paramref name="agentId"/>; the focus moves to its back button.</summary>
    /// <param name="agentId">The agent's id; an id the gallery does not have is ignored.</param>
    public void Open(string agentId)
    {
        if (FindAgent(agentId) is null)
        {
            return;
        }
        SetCurrentValue(OpenAgentIdProperty, agentId);
        FocusSoon(() => _back, FocusMethod);
    }

    /// <summary>Closes the detail view; the focus returns to the agent's card.</summary>
    public void CloseDetail()
    {
        var id = OpenAgentId;
        if (id is null)
        {
            return;
        }
        SetCurrentValue(OpenAgentIdProperty, null);
        var index = _shown.FindIndex(a => a.Id == id);
        if (index >= 0)
        {
            SetCurrent(index);
            FocusSoon(() => index < _cards.Count ? _cards[index] : null, FocusMethod);
        }
        else
        {
            FocusSoon(() => _search, FocusMethod);
        }
    }

    /// <summary>Clears the search; the category stays.</summary>
    public void ClearSearch() => SetCurrentValue(SearchTextProperty, null);

    /// <summary>
    /// Installs <paramref name="agentId"/> through <see cref="InstallAsync"/> (or the simulated install). Nothing happens
    /// for an unknown id, an installed agent or one already installing.
    /// </summary>
    /// <param name="agentId">The agent's id.</param>
    public async Task Install(string agentId)
    {
        if (FindAgent(agentId) is not { } agent || _installing.ContainsKey(agentId) || InstalledIds.Contains(agentId))
        {
            return;
        }
        var cancellation = new CancellationTokenSource();
        _installing[agentId] = cancellation;
        _failures.Remove(agentId);
        UpdateState(agentId);
        var labels = Labels;
        _announcer.Announce(labels.InstallingAnnouncement(agent.Name));
        try
        {
            if (InstallAsync is { } install)
            {
                await install(agent, cancellation.Token);
            }
            else
            {
                await Delay(AgentGallerySamples.SimulatedInstallTime, cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Finish();
            UpdateState(agentId);
            return;
        }
        catch (Exception exception)
        {
            Finish();
            var message = string.IsNullOrWhiteSpace(exception.Message) ? labels.InstallFailedFallback : exception.Message;
            _failures[agentId] = message;
            UpdateState(agentId);
            RaiseEvent(new AgentGalleryInstallFailedEventArgs(InstallFailedEvent, agent, message));
            _announcer.Announce(labels.FailedAnnouncement(agent.Name, message));
            return;
        }
        Finish();
        var detailHadFocus = HasFocus(_detailInstall);
        SetCurrentValue(InstalledIdsProperty, InstalledIds.Append(agentId).ToArray());
        UpdateState(agentId);
        RaiseEvent(new AgentGalleryAgentEventArgs(AgentInstalledEvent, agent));
        _announcer.Announce(labels.InstalledAnnouncement(agent.Name));
        if (detailHadFocus)
        {
            FocusSoon(() => _uninstall, FocusMethod);
        }

        void Finish()
        {
            if (_installing.TryGetValue(agentId, out var running) && ReferenceEquals(running, cancellation))
            {
                _installing.Remove(agentId);
            }
            cancellation.Dispose();
        }
    }

    /// <summary>
    /// Removes <paramref name="agentId"/> through <see cref="UninstallAsync"/> (or at once). Nothing happens for an agent
    /// that is not installed.
    /// </summary>
    /// <param name="agentId">The agent's id.</param>
    public async Task Uninstall(string agentId)
    {
        if (FindAgent(agentId) is not { } agent || !InstalledIds.Contains(agentId) || !_uninstalling.Add(agentId))
        {
            return;
        }
        _uninstallError = null;
        UpdateState(agentId);
        try
        {
            if (UninstallAsync is { } uninstall)
            {
                await uninstall(agent, CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            _uninstalling.Remove(agentId);
            var message = string.IsNullOrWhiteSpace(exception.Message) ? Labels.InstallFailedFallback : exception.Message;
            _uninstallError = (agentId, message);
            UpdateState(agentId);
            _announcer.Announce(message);
            return;
        }
        _uninstalling.Remove(agentId);
        var detailHadFocus = HasFocus(_uninstall);
        SetCurrentValue(InstalledIdsProperty, InstalledIds.Where(id => id != agentId).ToArray());
        UpdateState(agentId);
        RaiseEvent(new AgentGalleryAgentEventArgs(AgentUninstalledEvent, agent));
        _announcer.Announce(Labels.UninstalledAnnouncement(agent.Name));
        if (detailHadFocus)
        {
            FocusSoon(() => _detailInstall, FocusMethod);
        }
    }

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unwire();
        var scope = e.NameScope;
        _host = scope.Find<Panel>(HostPart);
        _title = scope.Find<TextBlock>(TitlePart);
        _subtitle = scope.Find<TextBlock>(SubtitlePart);
        _search = scope.Find<SearchField>(SearchPart);
        _categories = scope.Find<PillFilterBar>(CategoriesPart);
        _count = scope.Find<TextBlock>(CountPart);
        _sort = scope.Find<SegmentedControl>(SortPart);
        _grid = scope.Find<AgentGalleryGrid>(GridPart);
        _empty = scope.Find<Control>(EmptyPart);
        _emptyTitle = scope.Find<TextBlock>(EmptyTitlePart);
        _emptyHint = scope.Find<TextBlock>(EmptyHintPart);
        _clearSearch = scope.Find<Button>(ClearSearchPart);
        _showAll = scope.Find<Button>(ShowAllPart);
        _detail = scope.Find<Control>(DetailPart);
        _back = scope.Find<Button>(BackPart);
        _detailMark = scope.Find<AgentGalleryMark>(DetailMarkPart);
        _detailName = scope.Find<TextBlock>(DetailNamePart);
        _detailMaker = scope.Find<TextBlock>(DetailMakerPart);
        _detailVerified = scope.Find<Control>(DetailVerifiedPart);
        _detailStats = scope.Find<TextBlock>(DetailStatsPart);
        _detailInstall = scope.Find<Button>(DetailInstallPart);
        _detailInstalled = scope.Find<Control>(DetailInstalledPart);
        _uninstall = scope.Find<Button>(UninstallPart);
        _detailError = scope.Find<Control>(DetailErrorPart);
        _detailErrorText = scope.Find<TextBlock>(DetailErrorTextPart);
        _description = scope.Find<TextBlock>(DescriptionPart);
        _abilitiesHeading = scope.Find<TextBlock>(AbilitiesHeadingPart);
        _abilities = scope.Find<Panel>(AbilitiesPart);
        _permissionsHeading = scope.Find<TextBlock>(PermissionsHeadingPart);
        _permissions = scope.Find<Panel>(PermissionsPart);
        _promptsHeading = scope.Find<TextBlock>(PromptsHeadingPart);
        _prompts = scope.Find<Panel>(PromptsPart);
        Wire();
        _cards.Clear();
        _fade = null;
        _shownDetailId = null;
        ApplyTexts();
        Refresh(announce: false);
        ApplyDetail(raiseOpened: false);
    }

    private void Wire()
    {
        _host?.Children.Add(_announcer);
        if (_search is not null)
        {
            _search.Text = SearchText;
            _search.PropertyChanged += OnSearchPropertyChanged;
            _search.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
        }
        if (_categories is not null)
        {
            _categories.Options = CategoryOptions();
            _categories.SelectedKey = SelectedCategoryKey;
            _categories.PropertyChanged += OnCategoriesPropertyChanged;
        }
        if (_sort is not null)
        {
            _sort.ItemsSource = SortOptions();
            _sort.SelectedIndex = (int)Sort;
            _sort.SelectionChanged += OnSortSelectionChanged;
        }
        if (_clearSearch is not null)
        {
            _clearSearch.Click += OnClearSearchClick;
        }
        if (_showAll is not null)
        {
            _showAll.Click += OnShowAllClick;
        }
        if (_back is not null)
        {
            _back.Click += OnBackClick;
        }
        if (_detailInstall is not null)
        {
            _detailInstall.Click += OnDetailInstallClick;
        }
        if (_uninstall is not null)
        {
            _uninstall.Click += OnUninstallClick;
        }
    }

    private void Unwire()
    {
        _host?.Children.Remove(_announcer);
        if (_search is not null)
        {
            _search.PropertyChanged -= OnSearchPropertyChanged;
            _search.RemoveHandler(KeyDownEvent, OnSearchKeyDown);
        }
        if (_categories is not null)
        {
            _categories.PropertyChanged -= OnCategoriesPropertyChanged;
        }
        if (_sort is not null)
        {
            _sort.SelectionChanged -= OnSortSelectionChanged;
        }
        if (_clearSearch is not null)
        {
            _clearSearch.Click -= OnClearSearchClick;
        }
        if (_showAll is not null)
        {
            _showAll.Click -= OnShowAllClick;
        }
        if (_back is not null)
        {
            _back.Click -= OnBackClick;
        }
        if (_detailInstall is not null)
        {
            _detailInstall.Click -= OnDetailInstallClick;
        }
        if (_uninstall is not null)
        {
            _uninstall.Click -= OnUninstallClick;
        }
        foreach (var card in _cards)
        {
            card.RemoveHandler(AgentGalleryCard.ActivatedEvent, OnCardActivated);
            card.RemoveHandler(AgentGalleryCard.InstallRequestedEvent, OnCardInstallRequested);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AddHandler(KeyDownEvent, OnAnyKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        RemoveHandler(KeyDownEvent, OnAnyKeyDown);
        RemoveHandler(PointerPressedEvent, OnAnyPointerPressed);
        foreach (var running in _installing.Values.ToArray())
        {
            running.Cancel();
        }
    }

    private void OnAnyKeyDown(object? sender, KeyEventArgs e) => _keyboardMode = true;

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e) => _keyboardMode = false;

    private IList<PillFilterOption> CategoryOptions()
    {
        var options = new List<PillFilterOption> { new(null, Labels.AllCategories) };
        foreach (var category in Categories)
        {
            options.Add(new PillFilterOption(category.Key, category.Label));
        }
        return options;
    }

    private SegmentedOption[] SortOptions()
    {
        var labels = Labels;
        return
        [
            new SegmentedOption(AgentGallerySort.Popular, labels.SortPopular),
            new SegmentedOption(AgentGallerySort.Newest, labels.SortNewest),
            new SegmentedOption(AgentGallerySort.TopRated, labels.SortTopRated)
        ];
    }

    private void ApplyTexts()
    {
        var labels = Labels;
        if (_title is not null)
        {
            _title.Text = Title ?? labels.Title;
        }
        if (_subtitle is not null)
        {
            var subtitle = Subtitle ?? labels.Subtitle;
            _subtitle.Text = subtitle;
            _subtitle.IsVisible = !string.IsNullOrEmpty(subtitle);
        }
        if (_search is not null)
        {
            _search.Placeholder = labels.SearchPlaceholder;
            AutomationProperties.SetName(_search, labels.SearchPlaceholder);
        }
        if (_categories is not null)
        {
            _categories.AccessibleName = labels.CategoriesName;
        }
        if (_sort is not null)
        {
            AutomationProperties.SetName(_sort, labels.SortName);
        }
        if (_grid is not null)
        {
            AutomationProperties.SetName(_grid, labels.GridName);
        }
        if (_clearSearch is not null)
        {
            _clearSearch.Content = labels.ClearSearch;
        }
        if (_showAll is not null)
        {
            _showAll.Content = labels.ShowAll;
        }
        if (_back is not null)
        {
            _back.Content = labels.Back;
            AutomationProperties.SetName(_back, labels.Back);
        }
        if (_uninstall is not null)
        {
            _uninstall.Content = labels.Uninstall;
        }
        if (_detailInstalled is ContentControl installed)
        {
            installed.Content = labels.Installed;
        }
        if (_abilitiesHeading is not null)
        {
            _abilitiesHeading.Text = labels.AbilitiesHeading;
        }
        if (_permissionsHeading is not null)
        {
            _permissionsHeading.Text = labels.PermissionsHeading;
        }
        if (_promptsHeading is not null)
        {
            _promptsHeading.Text = labels.PromptsHeading;
        }
    }

    #endregion

    #region Property changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == AgentsProperty)
        {
            ObserveAgents(change.GetOldValue<IReadOnlyList<AgentGalleryItem>?>(), change.GetNewValue<IReadOnlyList<AgentGalleryItem>?>());
            Refresh(announce: false);
            ApplyDetail(raiseOpened: false);
        }
        else if (property == CategoriesProperty)
        {
            if (_categories is not null)
            {
                _categories.Options = CategoryOptions();
            }
            Refresh(announce: false);
            ApplyDetail(raiseOpened: false);
        }
        else if (property == InstalledIdsProperty)
        {
            foreach (var card in ShownCards)
            {
                if (card.Agent is { } agent)
                {
                    card.SetState(GetInstallState(agent.Id));
                }
            }
            UpdateDetailState();
        }
        else if (property == SelectedCategoryKeyProperty)
        {
            if (_categories is not null && !_syncing)
            {
                _categories.SelectedKey = change.GetNewValue<string?>();
            }
            Refresh(announce: true);
        }
        else if (property == SearchTextProperty)
        {
            if (_search is not null && !_syncing)
            {
                _search.Text = change.GetNewValue<string?>();
            }
            if (OpenAgentId is not null)
            {
                // Typing a search while an agent is open goes back to the cards (the focus stays in the field).
                SetCurrentValue(OpenAgentIdProperty, null);
            }
            Refresh(announce: true);
        }
        else if (property == SortProperty)
        {
            if (_sort is not null && !_syncing)
            {
                _sort.SelectedIndex = (int)change.GetNewValue<AgentGallerySort>();
            }
            Refresh(announce: true);
        }
        else if (property == OpenAgentIdProperty)
        {
            ApplyDetail(raiseOpened: true);
        }
        else if (property == TitleProperty || property == SubtitleProperty)
        {
            ApplyTexts();
        }
        else if (property == LabelsProperty || property == CultureNameProperty)
        {
            ApplyTexts();
            if (_categories is not null)
            {
                _categories.Options = CategoryOptions();
            }
            if (_sort is not null)
            {
                _syncing = true;
                try
                {
                    _sort.ItemsSource = SortOptions();
                    _sort.SelectedIndex = (int)Sort;
                }
                finally
                {
                    _syncing = false;
                }
            }
            Refresh(announce: false);
            _shownDetailId = null;
            ApplyDetail(raiseOpened: false);
        }
        else if (property == BoundsProperty || property == CompactWidthProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, Bounds.Width > 0 && Bounds.Width < CompactWidth);
        }
    }

    private void ObserveAgents(IReadOnlyList<AgentGalleryItem>? previous, IReadOnlyList<AgentGalleryItem>? next)
    {
        if (_observedAgents is not null)
        {
            _observedAgents.CollectionChanged -= OnAgentsCollectionChanged;
            _observedAgents = null;
        }
        _ = previous;
        if (next is INotifyCollectionChanged observable)
        {
            _observedAgents = observable;
            observable.CollectionChanged += OnAgentsCollectionChanged;
        }
    }

    private void OnAgentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Refresh(announce: false);
        _shownDetailId = null;
        ApplyDetail(raiseOpened: false);
    }

    private void OnSearchPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || _syncing)
        {
            return;
        }
        Sync(() => SetCurrentValue(SearchTextProperty, _search!.Text));
    }

    private void OnCategoriesPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != PillFilterBar.SelectedKeyProperty || _syncing)
        {
            return;
        }
        Sync(() => SetCurrentValue(SelectedCategoryKeyProperty, _categories!.SelectedKey));
    }

    private void OnSortSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _sort is null || _sort.SelectedIndex < 0)
        {
            return;
        }
        Sync(() => SetCurrentValue(SortProperty, (AgentGallerySort)_sort.SelectedIndex));
    }

    private void Sync(Action action)
    {
        _syncing = true;
        try
        {
            action();
        }
        finally
        {
            _syncing = false;
        }
    }

    #endregion

    #region The cards

    private string? NormalizedQuery => string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();

    private AgentGalleryItem? FindAgent(string? id)
    {
        if (id is null)
        {
            return null;
        }
        foreach (var agent in Agents)
        {
            if (agent.Id == id)
            {
                return agent;
            }
        }
        return null;
    }

    /// <summary>Filters, sorts and repaints the cards (reusing them), the count and the empty state.</summary>
    private void Refresh(bool announce)
    {
        var query = NormalizedQuery;
        var labels = Labels;
        var previousFirst = _shown.Count > 0 ? _shown[0] : null;
        var previousCount = _shown.Count;
        _shown = AgentGalleryQuery.Apply(Agents, Categories, SelectedCategoryKey, query, Sort);
        ResultCount = _shown.Count;
        PseudoClasses.Set(EmptyPseudoClass, _shown.Count == 0);
        if (_grid is not null)
        {
            var culture = Culture;
            for (var i = 0; i < _shown.Count; i++)
            {
                var card = CardAt(i);
                card.Show(_shown[i], GetInstallState(_shown[i].Id), query, labels, culture);
                card.IsVisible = true;
            }
            for (var i = _shown.Count; i < _cards.Count; i++)
            {
                _cards[i].IsVisible = false;
                _cards[i].IsCurrent = false;
            }
            _grid.IsVisible = _shown.Count > 0;
        }
        SetCurrent(Math.Clamp(_current, 0, Math.Max(0, _shown.Count - 1)));
        if (_count is not null)
        {
            _count.Text = labels.ResultCount(_shown.Count);
        }
        if (_empty is not null)
        {
            _empty.IsVisible = _shown.Count == 0;
        }
        if (_emptyTitle is not null)
        {
            _emptyTitle.Text = query is not null ? labels.NoMatches(query) : labels.EmptyCategory;
        }
        if (_emptyHint is not null)
        {
            _emptyHint.Text = labels.EmptyHint;
            _emptyHint.IsVisible = query is not null && SelectedCategoryKey is not null;
        }
        if (_clearSearch is not null)
        {
            _clearSearch.IsVisible = query is not null;
        }
        if (_showAll is not null)
        {
            _showAll.IsVisible = SelectedCategoryKey is not null;
        }
        if (announce)
        {
            _announcer.Announce(labels.ResultsAnnouncement(_shown.Count, query));
            var changed = previousCount != _shown.Count || (_shown.Count > 0 && !ReferenceEquals(previousFirst, _shown[0]));
            if (changed)
            {
                FadeResults();
            }
        }
    }

    private AgentGalleryCard CardAt(int index)
    {
        while (_cards.Count <= index)
        {
            var card = new AgentGalleryCard();
            card.AddHandler(AgentGalleryCard.ActivatedEvent, OnCardActivated);
            card.AddHandler(AgentGalleryCard.InstallRequestedEvent, OnCardInstallRequested);
            _cards.Add(card);
            _grid!.Children.Add(card);
        }
        return _cards[index];
    }

    /// <summary>Makes card <paramref name="index"/> the grid's one tab stop.</summary>
    private void SetCurrent(int index)
    {
        _current = index;
        for (var i = 0; i < _cards.Count; i++)
        {
            _cards[i].IsCurrent = i == index && i < _shown.Count;
        }
    }

    /// <summary>The cards come back from a faint opacity after the filter, search or sort changed them (opacity only).</summary>
    private void FadeResults()
    {
        if (_grid is not { } grid || !grid.IsAttachedToVisualTree())
        {
            return;
        }
        if (_fade is null)
        {
            _fade = new MotionValue<double>(grid, 1, MotionVectors.Double);
            _fade.Changed += value => grid.Opacity = value;
        }
        _fade.Jump(ResultsFadeFrom);
        _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
    }

    private void UpdateState(string agentId)
    {
        CardOf(agentId)?.SetState(GetInstallState(agentId));
        UpdateDetailState();
    }

    private void OnCardActivated(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is AgentGalleryCard { Agent: { } agent } card)
        {
            SetCurrent(_cards.IndexOf(card));
            Open(agent.Id);
        }
    }

    private void OnCardInstallRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not AgentGalleryCard { Agent: { } agent } card)
        {
            return;
        }
        SetCurrent(_cards.IndexOf(card));
        if (GetInstallState(agent.Id) == AgentGalleryInstallState.Installed)
        {
            Open(agent.Id);
        }
        else
        {
            _ = Install(agent.Id);
        }
    }

    private void OnClearSearchClick(object? sender, RoutedEventArgs e)
    {
        ClearSearch();
        FocusSoon(() => _search, FocusMethod);
    }

    private void OnShowAllClick(object? sender, RoutedEventArgs e)
    {
        SetCurrentValue(SelectedCategoryKeyProperty, null);
        FocusSoon(() => _shown.Count > 0 ? _cards[0] : _search, FocusMethod);
    }

    #endregion

    #region Keyboard

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        if (e.Key == Key.Escape && OpenAgentId is not null)
        {
            e.Handled = true;
            CloseDetail();
            return;
        }
        if (e.Source is AgentGalleryCard card && OpenAgentId is null)
        {
            var index = _cards.IndexOf(card);
            var target = AgentGalleryQuery.MoveIndex(index, _shown.Count, _grid?.Columns ?? 1, e.Key);
            if (target is { } next)
            {
                e.Handled = true;
                SetCurrent(next);
                _cards[next].Focus(NavigationMethod.Directional);
                _cards[next].BringIntoView();
            }
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && e.KeyModifiers == KeyModifiers.None && OpenAgentId is null && _shown.Count > 0)
        {
            e.Handled = true;
            var index = Math.Clamp(_current, 0, _shown.Count - 1);
            SetCurrent(index);
            _cards[index].Focus(NavigationMethod.Directional);
        }
    }

    private NavigationMethod FocusMethod => _keyboardMode ? NavigationMethod.Tab : NavigationMethod.Pointer;

    private bool HasFocus(Control? control)
        => control is not null && TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused &&
           (ReferenceEquals(focused, control) || control.IsVisualAncestorOf(focused));

    private static bool TryFocus(Control? target, NavigationMethod method) =>
        target is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true, Focusable: true } && target.IsAttachedToVisualTree() &&
        target.Focus(method);

    private void FocusSoon(Func<Control?> target, NavigationMethod method)
    {
        if (TryFocus(target(), method))
        {
            return;
        }
        Dispatcher.UIThread.Post(() => TryFocus(target(), method), DispatcherPriority.Loaded);
    }

    #endregion

    #region Detail

    private void OnBackClick(object? sender, RoutedEventArgs e) => CloseDetail();

    private void OnDetailInstallClick(object? sender, RoutedEventArgs e)
    {
        if (_shownDetailId is { } id)
        {
            _ = Install(id);
        }
    }

    private void OnUninstallClick(object? sender, RoutedEventArgs e)
    {
        if (_shownDetailId is { } id)
        {
            _ = Uninstall(id);
        }
    }

    /// <summary>Shows the detail view of <see cref="OpenAgentId"/> (or the grid) and fills it when the agent changed.</summary>
    private void ApplyDetail(bool raiseOpened)
    {
        var agent = FindAgent(OpenAgentId);
        PseudoClasses.Set(DetailPseudoClass, agent is not null);
        if (agent is null)
        {
            _shownDetailId = null;
            return;
        }
        var isNew = _shownDetailId != agent.Id;
        if (isNew)
        {
            if (_detail is not null)
            {
                FillDetail(agent);
            }
            _shownDetailId = agent.Id;
        }
        if (_uninstallError is { } error && error.Id != agent.Id)
        {
            _uninstallError = null;
        }
        UpdateDetailState();
        if (raiseOpened && isNew)
        {
            RaiseEvent(new AgentGalleryAgentEventArgs(AgentOpenedEvent, agent));
        }
    }

    private void FillDetail(AgentGalleryItem agent)
    {
        var labels = Labels;
        var culture = Culture;
        _detailMark?.SetAgent(agent);
        if (_detailName is not null)
        {
            _detailName.Text = agent.Name;
        }
        if (_detailMaker is not null)
        {
            _detailMaker.Text = agent.Maker is { Length: > 0 } maker ? labels.ByMaker(maker) : null;
            _detailMaker.IsVisible = agent.Maker is { Length: > 0 };
        }
        if (_detailVerified is not null)
        {
            _detailVerified.IsVisible = agent.IsVerified && agent.Maker is { Length: > 0 };
            AutomationProperties.SetName(_detailVerified, labels.Verified);
            Tooltip.SetTip(_detailVerified, labels.Verified);
        }
        if (_detailStats is not null)
        {
            var parts = new List<string> { AgentGalleryFormat.Rating(agent, labels, culture), labels.Installs(AgentGalleryFormat.Compact(agent.Installs, culture)) };
            var category = Categories.FirstOrDefault(c => c.Key == agent.CategoryKey);
            if (category is not null)
            {
                parts.Add(category.Label);
            }
            if (agent.AddedOn != default)
            {
                parts.Add(labels.AddedOn(AgentGalleryFormat.Date(agent.AddedOn, culture)));
            }
            _detailStats.Text = string.Join(" · ", parts);
            if (agent.RatingCount > 0 && agent.Rating > 0)
            {
                parts[0] = labels.RatingName(agent.Rating, agent.RatingCount);
            }
            AutomationProperties.SetName(_detailStats, string.Join(", ", parts));
        }
        if (_description is not null)
        {
            _description.Text = string.IsNullOrWhiteSpace(agent.Description) ? agent.Pitch : agent.Description;
        }
        FillAbilities(agent);
        FillPermissions(agent, labels);
        FillPrompts(agent);
    }

    private void FillAbilities(AgentGalleryItem agent)
    {
        if (_abilities is null)
        {
            return;
        }
        _abilities.Children.Clear();
        foreach (var ability in agent.Abilities)
        {
            _abilities.Children.Add(Row(ArcIcons.Check, ArcDesignTokenKind.Success, new TextBlock { Text = ability, TextWrapping = TextWrapping.Wrap }));
        }
        _abilities.IsVisible = agent.Abilities.Count > 0;
        if (_abilitiesHeading is not null)
        {
            _abilitiesHeading.IsVisible = agent.Abilities.Count > 0;
        }
    }

    private void FillPermissions(AgentGalleryItem agent, AgentGalleryLabels labels)
    {
        if (_permissions is null)
        {
            return;
        }
        _permissions.Children.Clear();
        if (agent.Permissions.Count == 0)
        {
            var none = new TextBlock { Text = labels.NoPermissions, TextWrapping = TextWrapping.Wrap };
            ArcTokens.Bind(none, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            _permissions.Children.Add(none);
            return;
        }
        foreach (var permission in agent.Permissions)
        {
            var text = new StackPanel { Spacing = 2 };
            var title = new WrapPanel();
            var name = new TextBlock { Text = permission.Name, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 8, 0) };
            ArcTokens.Bind(name, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
            title.Children.Add(name);
            if (permission.IsSensitive)
            {
                var word = new TextBlock { Text = labels.Sensitive };
                ArcTokens.Bind(word, TextBlock.ForegroundProperty, ArcDesignTokenKind.Warning);
                ArcTokens.Bind(word, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
                title.Children.Add(word);
            }
            text.Children.Add(title);
            if (!string.IsNullOrWhiteSpace(permission.Detail))
            {
                var detail = new TextBlock { Text = permission.Detail, TextWrapping = TextWrapping.Wrap };
                ArcTokens.Bind(detail, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
                text.Children.Add(detail);
            }
            var row = permission.IsSensitive
                ? Row(ArcIcons.TriangleAlert, ArcDesignTokenKind.Warning, text)
                : Row(ArcIcons.Shield, ArcDesignTokenKind.TextMuted, text);
            AutomationProperties.SetName(row, permission.IsSensitive ? $"{permission.Name}, {labels.Sensitive}" : permission.Name);
            _permissions.Children.Add(row);
        }
    }

    private void FillPrompts(AgentGalleryItem agent)
    {
        if (_prompts is null)
        {
            return;
        }
        _prompts.Children.Clear();
        foreach (var prompt in agent.SamplePrompts)
        {
            var button = new Button
            {
                Content = prompt,
                Variant = ButtonVariant.Secondary,
                Size = ControlSize.Small,
                Margin = new Thickness(0, 0, 8, 8)
            };
            AutomationProperties.SetName(button, prompt);
            button.Click += (_, _) => RaiseEvent(new AgentGalleryPromptEventArgs(PromptChosenEvent, agent, prompt));
            _prompts.Children.Add(button);
        }
        _prompts.IsVisible = agent.SamplePrompts.Count > 0;
        if (_promptsHeading is not null)
        {
            _promptsHeading.IsVisible = agent.SamplePrompts.Count > 0;
        }
    }

    /// <summary>A list row: a 16px glyph in <paramref name="tone"/> before <paramref name="content"/>.</summary>
    private static Grid Row(Geometry glyph, ArcDesignTokenKind tone, Control content)
    {
        var icon = new ArcGlyph
        {
            Data = glyph,
            Size = 16,
            StrokeThickness = 2,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 10, 0)
        };
        ArcTokens.Bind(icon, ArcGlyph.ForegroundProperty, tone);
        AutomationProperties.SetAccessibilityView(icon, AccessibilityView.Raw);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(content, 1);
        row.Children.Add(icon);
        row.Children.Add(content);
        return row;
    }

    private void UpdateDetailState()
    {
        if (FindAgent(_shownDetailId) is not { } agent)
        {
            return;
        }
        var labels = Labels;
        var state = GetInstallState(agent.Id);
        if (_detailInstall is not null)
        {
            _detailInstall.IsVisible = state != AgentGalleryInstallState.Installed;
            _detailInstall.IsLoading = state == AgentGalleryInstallState.Installing;
            _detailInstall.Content = state switch
            {
                AgentGalleryInstallState.Installing => labels.Installing,
                AgentGalleryInstallState.Failed => labels.Retry,
                _ => labels.Install
            };
            AutomationProperties.SetName(_detailInstall, labels.InstallName(agent.Name));
        }
        if (_detailInstalled is not null)
        {
            _detailInstalled.IsVisible = state == AgentGalleryInstallState.Installed;
        }
        if (_uninstall is not null)
        {
            _uninstall.IsVisible = state == AgentGalleryInstallState.Installed;
            _uninstall.IsLoading = _uninstalling.Contains(agent.Id);
            AutomationProperties.SetName(_uninstall, labels.UninstallName(agent.Name));
        }
        var message = state == AgentGalleryInstallState.Failed && _failures.TryGetValue(agent.Id, out var failure)
            ? failure
            : _uninstallError is { } error && error.Id == agent.Id ? error.Message : null;
        if (_detailError is not null)
        {
            _detailError.IsVisible = message is not null;
        }
        if (_detailErrorText is not null)
        {
            _detailErrorText.Text = message;
        }
    }

    #endregion
}

/// <summary>The filter, search, order and keyboard steps of <see cref="AgentGallery"/>, as pure functions.</summary>
internal static class AgentGalleryQuery
{
    /// <summary>The agents in <paramref name="category"/> (null: all) that match <paramref name="query"/> (null: all), in <paramref name="sort"/> order.</summary>
    public static List<AgentGalleryItem> Apply(IReadOnlyList<AgentGalleryItem> agents, IReadOnlyList<AgentGalleryCategory> categories,
        string? category, string? query, AgentGallerySort sort)
    {
        var result = new List<AgentGalleryItem>(agents.Count);
        foreach (var agent in agents)
        {
            if (category is not null && agent.CategoryKey != category)
            {
                continue;
            }
            if (query is not null && !Matches(agent, query, categories))
            {
                continue;
            }
            result.Add(agent);
        }
        Comparison<AgentGalleryItem> byName = (a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
        Comparison<AgentGalleryItem> order = sort switch
        {
            AgentGallerySort.Newest => (a, b) => b.AddedOn.CompareTo(a.AddedOn) is var c && c != 0 ? c : byName(a, b),
            AgentGallerySort.TopRated => (a, b) =>
            {
                var c = Score(b).CompareTo(Score(a));
                if (c == 0)
                {
                    c = b.RatingCount.CompareTo(a.RatingCount);
                }
                return c != 0 ? c : byName(a, b);
            },
            _ => (a, b) => b.Installs.CompareTo(a.Installs) is var c && c != 0 ? c : byName(a, b)
        };
        // A stable sort, so equal agents keep the order they were given in.
        var indexed = result.Select((agent, index) => (agent, index)).ToList();
        indexed.Sort((x, y) => order(x.agent, y.agent) is var c && c != 0 ? c : x.index.CompareTo(y.index));
        return indexed.Select(x => x.agent).ToList();

        static double Score(AgentGalleryItem agent) => agent.RatingCount > 0 ? agent.Rating : -1;
    }

    /// <summary>Whether <paramref name="query"/> occurs in the agent's name, maker, pitch, a tag or its category's name (ignoring case).</summary>
    public static bool Matches(AgentGalleryItem agent, string query, IReadOnlyList<AgentGalleryCategory> categories)
    {
        const StringComparison ignore = StringComparison.CurrentCultureIgnoreCase;
        if (agent.Name.Contains(query, ignore) || (agent.Maker?.Contains(query, ignore) ?? false) || agent.Pitch.Contains(query, ignore))
        {
            return true;
        }
        foreach (var tag in agent.Tags)
        {
            if (tag.Contains(query, ignore))
            {
                return true;
            }
        }
        foreach (var category in categories)
        {
            if (category.Key == agent.CategoryKey && category.Label.Contains(query, ignore))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The card a key moves to from <paramref name="index"/> in a grid of <paramref name="count"/> cards and
    /// <paramref name="columns"/> columns, or null when the key does not move (or there is nowhere to go).
    /// Left and Right step without wrapping, Up and Down step a row (Down from the row above a short last row lands
    /// on the last card), Home and End go to the first and last card.
    /// </summary>
    public static int? MoveIndex(int index, int count, int columns, Key key)
    {
        if (index < 0 || count == 0)
        {
            return null;
        }
        columns = Math.Max(1, columns);
        int target;
        switch (key)
        {
            case Key.Left:
                target = index - 1;
                break;
            case Key.Right:
                target = index + 1;
                break;
            case Key.Up:
                target = index - columns;
                break;
            case Key.Down:
                target = index + columns;
                if (target >= count && index / columns < (count - 1) / columns)
                {
                    target = count - 1;
                }
                break;
            case Key.Home:
                target = 0;
                break;
            case Key.End:
                target = count - 1;
                break;
            default:
                return null;
        }
        if (target < 0 || target >= count || target == index)
        {
            return target == index && key is Key.Home or Key.End ? index : null;
        }
        return target;
    }
}
