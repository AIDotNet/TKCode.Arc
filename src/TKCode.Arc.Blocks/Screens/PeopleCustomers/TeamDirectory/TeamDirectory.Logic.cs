using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(CountPart, typeof(TextBlock))]
[TemplatePart(SearchFramePart, typeof(Border))]
[TemplatePart(SearchInputPart, typeof(TeamDirectorySearchInput))]
[TemplatePart(ClearSearchPart, typeof(TeamDirectoryClearButton))]
[TemplatePart(FilterBarPart, typeof(Control))]
[TemplatePart(FiltersPart, typeof(TeamDirectoryFilterStrip))]
[TemplatePart(BodyPart, typeof(TeamDirectoryBody))]
[TemplatePart(ListPart, typeof(TeamDirectoryList))]
[TemplatePart(EmptyPart, typeof(Control))]
[TemplatePart(NoMatchPart, typeof(TextBlock))]
[TemplatePart(ClearFiltersPart, typeof(TeamDirectoryLinkButton))]
[TemplatePart(DetailPart, typeof(TeamDirectoryDetailHost))]
[PseudoClasses(FramedPseudoClass, NarrowPseudoClass, StackedPseudoClass, CompactPseudoClass, EmptyResultsPseudoClass)]
public partial class TeamDirectory
{
    public const string TitlePart = "PART_Title";
    public const string CountPart = "PART_Count";
    public const string SearchFramePart = "PART_SearchFrame";
    public const string SearchInputPart = "PART_SearchInput";
    public const string ClearSearchPart = "PART_ClearSearch";
    public const string FilterBarPart = "PART_FilterBar";
    public const string FiltersPart = "PART_Filters";
    public const string BodyPart = "PART_Body";
    public const string ListPart = "PART_List";
    public const string EmptyPart = "PART_Empty";
    public const string NoMatchPart = "PART_NoMatch";
    public const string ClearFiltersPart = "PART_ClearFilters";
    public const string DetailPart = "PART_Detail";

    /// <summary>Set while <see cref="IsFramed"/> is true: the bordered card.</summary>
    public const string FramedPseudoClass = ":framed";

    /// <summary>Set while the block is 640 wide or less: tighter profile; between 501 and 640 the copy button shows only its glyph.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the block is 500 wide or less: one column, the profile under the list.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set while the block is 400 wide or less: the search box under the title, team counts hidden.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while nobody matches the team filter and the search.</summary>
    public const string EmptyResultsPseudoClass = ":empty-results";

    #region Token-driven internals

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<TeamDirectory, double>(nameof(NarrowWidth), 640);

    internal static readonly StyledProperty<double> StackedWidthProperty =
        AvaloniaProperty.Register<TeamDirectory, double>(nameof(StackedWidth), 500);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<TeamDirectory, double>(nameof(CompactWidth), 400);

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    internal double StackedWidth { get => GetValue(StackedWidthProperty); set => SetValue(StackedWidthProperty, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    #endregion

    private readonly List<TeamDirectoryPerson> _people = new();
    private readonly HashSet<TeamDirectoryPerson> _watched = new();
    private readonly Dictionary<TeamDirectoryPerson, TeamDirectoryRow> _rows = new();
    private List<TeamDirectoryPerson> _visible = new();
    private List<string> _teams = new();
    private INotifyCollectionChanged? _observedSource;

    private TextBlock? _title;
    private TextBlock? _count;
    private Border? _searchFrame;
    private TeamDirectorySearchInput? _searchInput;
    private TeamDirectoryClearButton? _clearSearch;
    private Control? _filterBar;
    private TeamDirectoryFilterStrip? _filters;
    private TeamDirectoryList? _list;
    private Control? _empty;
    private TextBlock? _noMatch;
    private TeamDirectoryLinkButton? _clearFilters;
    private TeamDirectoryDetailHost? _detail;

    private CancellationTokenSource? _lifetime;
    private bool _refreshing;
    private bool _syncingSearch;

    static TeamDirectory()
    {
        FocusableProperty.OverrideDefaultValue<TeamDirectory>(false);
    }

    public TeamDirectory()
    {
        People.CollectionChanged += (_, _) =>
        {
            if (PeopleSource is null)
            {
                ReloadPeople();
            }
        };
        Teams.CollectionChanged += (_, _) => Refresh(animate: true);
        PseudoClasses.Set(FramedPseudoClass, true);
    }

    /// <summary>The people the block holds now (from <see cref="PeopleSource"/> or <see cref="People"/>), in order.</summary>
    public IReadOnlyList<TeamDirectoryPerson> EffectivePeople => _people;

    /// <summary>The people who match the team filter and the search, in order.</summary>
    public IReadOnlyList<TeamDirectoryPerson> VisiblePeople => _visible;

    /// <summary>The teams of the filter after "All", in order.</summary>
    public IReadOnlyList<string> EffectiveTeams => _teams;

    internal TeamDirectoryList? List => _list;

    internal TeamDirectoryFilterStrip? Filters => _filters;

    internal TeamDirectoryDetailHost? Detail => _detail;

    internal TeamDirectorySearchInput? SearchInput => _searchInput;

    internal TeamDirectoryClearButton? ClearSearchButton => _clearSearch;

    internal TeamDirectoryLinkButton? ClearFiltersButton => _clearFilters;

    internal Control? EmptyMessage => _empty;

    internal TextBlock? NoMatchText => _noMatch;

    internal TextBlock? CountText => _count;

    #region Public methods

    /// <summary>Shows the profile of the person with this key (as if picked, but without <see cref="PersonSelected"/>); null goes back to the first match.</summary>
    public void SelectPerson(string? key) => SetCurrentValue(SelectedPersonIdProperty, key);

    /// <summary>Shows the profile of <paramref name="person"/> (without <see cref="PersonSelected"/>).</summary>
    public void SelectPerson(TeamDirectoryPerson person) => SelectPerson(person.Key);

    /// <summary>Empties the search and shows every team again (the no-match message's button); the picked person is kept.</summary>
    public void ClearFilters()
    {
        SetCurrentValue(SearchTextProperty, string.Empty);
        SetCurrentValue(SelectedTeamProperty, null);
    }

    /// <summary>Moves the keyboard focus into the search box; false when it is hidden or not on screen.</summary>
    public bool FocusSearch(NavigationMethod method = NavigationMethod.Tab) => _searchInput?.Focus(method) == true;

    /// <summary>Copies the shown person's email address as the copy button does; false when nobody shows or the copy failed.</summary>
    public Task<bool> CopyEmailAsync()
    {
        if (_detail?.CurrentProfile is { } profile)
        {
            return CopyFromProfileAsync(profile);
        }
        return DisplayedPerson is { } person ? CopyAsyncCore(person, null) : Task.FromResult(false);
    }

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        Unhook();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _title = scope.Find<TextBlock>(TitlePart);
        _count = scope.Find<TextBlock>(CountPart);
        _searchFrame = scope.Find<Border>(SearchFramePart);
        _searchInput = scope.Find<TeamDirectorySearchInput>(SearchInputPart);
        _clearSearch = scope.Find<TeamDirectoryClearButton>(ClearSearchPart);
        _filterBar = scope.Find<Control>(FilterBarPart);
        _filters = scope.Find<TeamDirectoryFilterStrip>(FiltersPart);
        _list = scope.Find<TeamDirectoryList>(ListPart);
        _empty = scope.Find<Control>(EmptyPart);
        _noMatch = scope.Find<TextBlock>(NoMatchPart);
        _clearFilters = scope.Find<TeamDirectoryLinkButton>(ClearFiltersPart);
        _detail = scope.Find<TeamDirectoryDetailHost>(DetailPart);

        if (_searchInput is not null)
        {
            _searchInput.Text = SearchText;
            _searchInput.TextChanged += OnSearchInputChanged;
            _searchInput.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
        }
        if (_searchFrame is not null)
        {
            _searchFrame.PointerPressed += OnSearchFramePressed;
        }
        if (_clearSearch is not null)
        {
            _clearSearch.Click += OnClearSearchClick;
        }
        if (_clearFilters is not null)
        {
            _clearFilters.Click += OnClearFiltersClick;
        }
        if (_list is not null)
        {
            _list.RowRemoved += OnRowRemoved;
        }
        _rows.Clear();
        _teams = new List<string>();
        UpdateVisibility();
        ApplyLabels();
        Refresh(animate: false);
    }

    private void Unhook()
    {
        if (_searchInput is not null)
        {
            _searchInput.TextChanged -= OnSearchInputChanged;
            _searchInput.RemoveHandler(KeyDownEvent, OnSearchKeyDown);
        }
        if (_searchFrame is not null)
        {
            _searchFrame.PointerPressed -= OnSearchFramePressed;
        }
        if (_clearSearch is not null)
        {
            _clearSearch.Click -= OnClearSearchClick;
        }
        if (_clearFilters is not null)
        {
            _clearFilters.Click -= OnClearFiltersClick;
        }
        if (_list is not null)
        {
            _list.RowRemoved -= OnRowRemoved;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _lifetime = new CancellationTokenSource();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _lifetime?.Cancel();
        _lifetime?.Dispose();
        _lifetime = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var p = change.Property;
        if (p == SelectedTeamProperty || p == SelectedPersonIdProperty || p == SearchFilterProperty)
        {
            Refresh(animate: true);
        }
        else if (p == SearchTextProperty)
        {
            SyncSearchInput();
            Refresh(animate: true);
        }
        else if (p == PeopleSourceProperty)
        {
            ObserveSource(change.GetNewValue<System.Collections.IEnumerable?>());
            ReloadPeople();
        }
        else if (p == LabelsProperty)
        {
            ApplyLabels();
            Refresh(animate: false);
        }
        else if (p == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
        }
        else if (p == IsSearchVisibleProperty || p == IsFilterVisibleProperty)
        {
            UpdateVisibility();
        }
        else if (p == CopyResetDelayProperty && _detail is not null)
        {
            foreach (var profile in _detail.Children.OfType<TeamDirectoryProfile>())
            {
                profile.CopyResetDelay = CopyResetDelay;
            }
        }
        else if (p == NarrowWidthProperty || p == StackedWidthProperty || p == CompactWidthProperty)
        {
            UpdateBreakpoints(Bounds.Width);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
    }

    /// <summary>Sets the width classes from the block's own width (Arc's container queries).</summary>
    private void UpdateBreakpoints(double width)
    {
        if (!(width > 0))
        {
            return;
        }
        PseudoClasses.Set(NarrowPseudoClass, width <= NarrowWidth);
        PseudoClasses.Set(StackedPseudoClass, width <= StackedWidth);
        PseudoClasses.Set(CompactPseudoClass, width <= CompactWidth);
    }

    private void UpdateVisibility()
    {
        if (_searchFrame is not null)
        {
            _searchFrame.IsVisible = IsSearchVisible;
        }
        if (_filterBar is not null)
        {
            _filterBar.IsVisible = IsFilterVisible;
        }
    }

    #region People

    private void ObserveSource(System.Collections.IEnumerable? source)
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceChanged;
        }
        _observedSource = source as INotifyCollectionChanged;
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged += OnSourceChanged;
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => ReloadPeople();

    private void ReloadPeople()
    {
        _people.Clear();
        if (PeopleSource is { } source)
        {
            _people.AddRange(source.OfType<TeamDirectoryPerson>());
        }
        else
        {
            _people.AddRange(People);
        }
        foreach (var person in _watched.ToList())
        {
            if (!_people.Contains(person))
            {
                person.PropertyChanged -= OnPersonChanged;
                _watched.Remove(person);
            }
        }
        foreach (var person in _people)
        {
            if (_watched.Add(person))
            {
                person.PropertyChanged += OnPersonChanged;
            }
        }
        Refresh(animate: true);
    }

    private void OnPersonChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not TeamDirectoryPerson person)
        {
            return;
        }
        var labels = Labels;
        if (_rows.TryGetValue(person, out var row))
        {
            row.Refresh(labels);
        }
        if (_detail is not null)
        {
            foreach (var profile in _detail.Children.OfType<TeamDirectoryProfile>())
            {
                if (ReferenceEquals(profile.Person, person))
                {
                    profile.Refresh(labels);
                }
            }
        }
        Refresh(animate: true);
    }

    #endregion

    #region Refresh

    /// <summary>Recomputes the matches and the shown person and brings every part up to date.</summary>
    private void Refresh(bool animate)
    {
        if (_refreshing)
        {
            return;
        }
        _refreshing = true;
        try
        {
            animate = animate && IsLoaded;
            var visible = TeamDirectoryQuery.Visible(_people, SelectedTeam, SearchText, SearchFilter);
            var displayed = TeamDirectoryQuery.Displayed(visible, SelectedPersonId);
            _visible = visible;
            if (_count is not null)
            {
                _count.Text = _people.Count.ToString(CultureInfo.CurrentCulture);
            }
            SyncFilters(animate);
            SyncRows(visible, displayed, animate);
            var empty = visible.Count == 0;
            PseudoClasses.Set(EmptyResultsPseudoClass, empty);
            if (_empty is not null)
            {
                _empty.IsVisible = empty;
            }
            if (_noMatch is not null)
            {
                _noMatch.Text = Labels.NoMatches(SearchText ?? string.Empty);
            }
            if (_clearSearch is not null)
            {
                _clearSearch.IsVisible = !string.IsNullOrEmpty(SearchText);
            }
            SyncDetail(displayed, animate);
            MatchCount = visible.Count;
            DisplayedPerson = displayed;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void SyncFilters(bool animate)
    {
        if (_filters is null)
        {
            return;
        }
        var teams = TeamDirectoryQuery.Teams(_people, Teams);
        var labels = Labels;
        if (!teams.SequenceEqual(_teams) || !_filters.Buttons.Any())
        {
            _teams = teams;
            var buttons = new List<TeamDirectoryFilterButton>();
            buttons.Add(CreateFilterButton(null));
            foreach (var team in teams)
            {
                buttons.Add(CreateFilterButton(team));
            }
            _filters.SetButtons(buttons);
        }
        TeamDirectoryFilterButton? active = null;
        foreach (var button in _filters.Buttons)
        {
            var label = button.Team ?? labels.AllTeams;
            var count = TeamDirectoryQuery.Count(_people, button.Team);
            button.Label = label;
            button.CountText = count.ToString(CultureInfo.CurrentCulture);
            AutomationProperties.SetName(button, labels.FilterButtonName(label, count));
            if (string.IsNullOrEmpty(SelectedTeam) ? button.Team is null : button.Team == SelectedTeam)
            {
                active = button;
            }
        }
        _filters.Select(active, animate);
    }

    private TeamDirectoryFilterButton CreateFilterButton(string? team)
    {
        var button = new TeamDirectoryFilterButton(team);
        button.Click += OnFilterClick;
        return button;
    }

    private void SyncRows(List<TeamDirectoryPerson> visible, TeamDirectoryPerson? displayed, bool animate)
    {
        if (_list is null)
        {
            return;
        }
        var rows = new List<TeamDirectoryRow>(visible.Count);
        TeamDirectoryRow? highlighted = null;
        foreach (var person in visible)
        {
            if (!_rows.TryGetValue(person, out var row))
            {
                row = new TeamDirectoryRow(person);
                row.Click += OnRowClick;
                row.Refresh(Labels);
                _rows[person] = row;
            }
            row.IsChecked = ReferenceEquals(person, displayed);
            if (ReferenceEquals(person, displayed))
            {
                highlighted = row;
            }
            rows.Add(row);
        }
        foreach (var pair in _rows)
        {
            if (!visible.Contains(pair.Key))
            {
                pair.Value.IsChecked = false;
            }
        }
        _list.Update(rows, highlighted, animate);
    }

    private void OnRowRemoved(object? sender, TeamDirectoryRow row)
    {
        if (_rows.TryGetValue(row.Person, out var pooled) && ReferenceEquals(pooled, row))
        {
            _rows.Remove(row.Person);
            row.Click -= OnRowClick;
        }
    }

    private void SyncDetail(TeamDirectoryPerson? displayed, bool animate)
    {
        if (_detail is null)
        {
            return;
        }
        var labels = Labels;
        if (displayed is null)
        {
            if (_detail.Current is TeamDirectoryNoProfile none)
            {
                none.Title = labels.NoneTitle;
                none.Hint = labels.NoneHint;
                return;
            }
            _detail.Show(new TeamDirectoryNoProfile { Title = labels.NoneTitle, Hint = labels.NoneHint }, animate);
            return;
        }
        if (_detail.CurrentProfile is { } current && ReferenceEquals(current.Person, displayed))
        {
            return;
        }
        var profile = new TeamDirectoryProfile(displayed, labels) { CopyResetDelay = CopyResetDelay };
        profile.CopyClicked += OnProfileCopyClicked;
        profile.EmailClicked += OnProfileEmailClicked;
        _detail.Show(profile, animate);
    }

    private void ApplyLabels()
    {
        var labels = Labels;
        if (_title is not null)
        {
            _title.Text = labels.Title;
        }
        if (_searchInput is not null)
        {
            _searchInput.PlaceholderText = labels.SearchPlaceholder;
            AutomationProperties.SetName(_searchInput, labels.SearchName);
        }
        if (_clearSearch is not null)
        {
            AutomationProperties.SetName(_clearSearch, labels.ClearSearchName);
        }
        if (_filters is not null)
        {
            AutomationProperties.SetName(_filters, labels.FilterGroupName);
        }
        if (_list is not null)
        {
            AutomationProperties.SetName(_list, labels.ListName);
        }
        if (_clearFilters is not null)
        {
            _clearFilters.Text = labels.ClearFilters;
            AutomationProperties.SetName(_clearFilters, labels.ClearFilters);
        }
        foreach (var row in _rows.Values)
        {
            row.Refresh(labels);
        }
        if (_detail is not null)
        {
            foreach (var profile in _detail.Children.OfType<TeamDirectoryProfile>())
            {
                profile.Refresh(labels);
            }
        }
    }

    #endregion

    #region Input

    private void OnFilterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is TeamDirectoryFilterButton button)
        {
            e.Handled = true;
            SetCurrentValue(SelectedTeamProperty, button.Team);
        }
    }

    private void OnRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not TeamDirectoryRow { IsLeaving: false } row)
        {
            return;
        }
        e.Handled = true;
        var person = row.Person;
        var previous = DisplayedPerson;
        var selected = SelectedPersonId;
        // Picking the person already picked changes nothing (Arc). With no pick yet, the first person counts as picked.
        var current = string.IsNullOrEmpty(selected) ? (_people.Count > 0 ? _people[0].Key : null) : selected;
        if (person.Key == current && ReferenceEquals(previous, person))
        {
            return;
        }
        SetCurrentValue(SelectedPersonIdProperty, person.Key);
        RaiseEvent(new TeamDirectoryPersonSelectedEventArgs(PersonSelectedEvent, this, person, previous));
        var command = PersonSelectedCommand;
        if (command is not null && command.CanExecute(person))
        {
            command.Execute(person);
        }
    }

    private void OnSearchInputChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingSearch || _searchInput is null)
        {
            return;
        }
        SetCurrentValue(SearchTextProperty, _searchInput.Text ?? string.Empty);
    }

    private void SyncSearchInput()
    {
        if (_searchInput is null || (_searchInput.Text ?? string.Empty) == (SearchText ?? string.Empty))
        {
            return;
        }
        _syncingSearch = true;
        try
        {
            _searchInput.Text = SearchText;
            _searchInput.CaretIndex = SearchText?.Length ?? 0;
        }
        finally
        {
            _syncingSearch = false;
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        // Escape empties the search and keeps the focus there; with nothing typed it is left to the page.
        if (e.Key != Key.Escape || e.Handled || string.IsNullOrEmpty(SearchText) || _searchInput is { IsComposing: true })
        {
            return;
        }
        e.Handled = true;
        SetCurrentValue(SearchTextProperty, string.Empty);
    }

    private void OnSearchFramePressed(object? sender, PointerPressedEventArgs e)
    {
        if (_searchInput is not null && !_searchInput.IsKeyboardFocusWithin && e.Source is not TeamDirectoryClearButton)
        {
            _searchInput.Focus(NavigationMethod.Pointer);
        }
    }

    private void OnClearSearchClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SetCurrentValue(SearchTextProperty, string.Empty);
        // The button leaves under the pointer: the focus goes back to the input (Arc).
        _searchInput?.Focus(NavigationMethod.Pointer);
    }

    private void OnClearFiltersClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var hadFocus = _clearFilters?.IsKeyboardFocusWithin == true;
        ClearFilters();
        // The button disappears with the message; rather than dropping the focus, put it in the search box.
        if (hadFocus)
        {
            _searchInput?.Focus(NavigationMethod.Tab);
        }
    }

    #endregion

    #region Copy and email

    private void OnProfileCopyClicked(object? sender, EventArgs e)
    {
        if (sender is TeamDirectoryProfile profile)
        {
            _ = CopyFromProfileAsync(profile);
        }
    }

    private Task<bool> CopyFromProfileAsync(TeamDirectoryProfile profile) => CopyAsyncCore(profile.Person, profile);

    private async Task<bool> CopyAsyncCore(TeamDirectoryPerson person, TeamDirectoryProfile? profile)
    {
        var email = person.Email;
        if (string.IsNullOrEmpty(email))
        {
            return false;
        }
        bool ok;
        Exception? error = null;
        try
        {
            if (CopyAsync is { } copy)
            {
                await copy(email, _lifetime?.Token ?? CancellationToken.None);
                ok = true;
            }
            else
            {
                ok = await ArcClipboard.CopyTextAsync(this, email);
            }
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            ok = false;
            error = ex;
        }
        if (profile is not null && !profile.IsLeaving)
        {
            profile.ShowCopyResult(ok);
        }
        RaiseEvent(new TeamDirectoryEmailCopiedEventArgs(EmailCopiedEvent, this, person, email, ok, error));
        return ok;
    }

    private void OnProfileEmailClicked(object? sender, EventArgs e)
    {
        if (sender is not TeamDirectoryProfile { Person: var person } || string.IsNullOrEmpty(person.Email))
        {
            return;
        }
        var args = new TeamDirectoryPersonEventArgs(EmailLinkClickedEvent, this, person);
        RaiseEvent(args);
        if (args.Handled)
        {
            return;
        }
        if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher
            && Uri.TryCreate("mailto:" + person.Email, UriKind.Absolute, out var uri))
        {
            _ = launcher.LaunchUriAsync(uri);
        }
    }

    #endregion
}
