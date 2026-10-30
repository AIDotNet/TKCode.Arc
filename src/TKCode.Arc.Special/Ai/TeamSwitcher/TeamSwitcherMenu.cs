using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Which view the menu shows.</summary>
internal enum TeamSwitcherView
{
    /// <summary>Search, the workspaces and the two actions.</summary>
    List,

    /// <summary>The inline Create workspace form.</summary>
    Create,

    /// <summary>The inline Join with invite form.</summary>
    Join
}

/// <summary>
/// The panel inside a <see cref="TeamSwitcher"/>'s popup. The list view has a search field that keeps focus the whole time
/// (a combobox that owns the list: the highlighted row is announced through the field's help text), the workspaces (six
/// 48px rows, scrolling beyond without a visible scrollbar) and the Create workspace and Join with invite actions; one
/// highlight glides behind whichever row is highlighted, from the keyboard or the pointer. Create and join are inline
/// forms that crossfade in place of the list; the panel's height never changes.
/// </summary>
[TemplatePart(SurfacePart, typeof(Border))]
[TemplatePart(ViewsPart, typeof(Panel))]
[TemplatePart(ListViewPart, typeof(Panel))]
[TemplatePart(SearchPart, typeof(Input))]
[TemplatePart(SectionPart, typeof(TextBlock))]
[TemplatePart(ListAreaPart, typeof(Panel))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(RowsPart, typeof(StackPanel))]
[TemplatePart(EmptyPart, typeof(Control))]
[TemplatePart(EmptyTextPart, typeof(TextBlock))]
[TemplatePart(EmptyHintPart, typeof(TextBlock))]
[TemplatePart(CreateRowPart, typeof(TeamSwitcherActionRow))]
[TemplatePart(JoinRowPart, typeof(TeamSwitcherActionRow))]
[TemplatePart(HighlightPart, typeof(GlidingIndicator))]
[TemplatePart(CreateViewPart, typeof(Control))]
[TemplatePart(CreateBackPart, typeof(AvaloniaButton))]
[TemplatePart(CreateTitlePart, typeof(TextBlock))]
[TemplatePart(NameInputPart, typeof(Input))]
[TemplatePart(CreateErrorPart, typeof(Control))]
[TemplatePart(CreateErrorTextPart, typeof(TextBlock))]
[TemplatePart(CreateCancelPart, typeof(Button))]
[TemplatePart(CreateSubmitPart, typeof(Button))]
[TemplatePart(JoinViewPart, typeof(Control))]
[TemplatePart(JoinBackPart, typeof(AvaloniaButton))]
[TemplatePart(JoinTitlePart, typeof(TextBlock))]
[TemplatePart(InviteInputPart, typeof(Input))]
[TemplatePart(JoinErrorPart, typeof(Control))]
[TemplatePart(JoinErrorTextPart, typeof(TextBlock))]
[TemplatePart(JoinCancelPart, typeof(Button))]
[TemplatePart(JoinSubmitPart, typeof(Button))]
internal sealed class TeamSwitcherMenu : TemplatedControl
{
    internal const string SurfacePart = "PART_Surface";
    internal const string ViewsPart = "PART_Views";
    internal const string ListViewPart = "PART_ListView";
    internal const string SearchPart = "PART_Search";
    internal const string SectionPart = "PART_Section";
    internal const string ListAreaPart = "PART_ListArea";
    internal const string ScrollPart = "PART_Scroll";
    internal const string RowsPart = "PART_Rows";
    internal const string EmptyPart = "PART_Empty";
    internal const string EmptyTextPart = "PART_EmptyText";
    internal const string EmptyHintPart = "PART_EmptyHint";
    internal const string CreateRowPart = "PART_CreateRow";
    internal const string JoinRowPart = "PART_JoinRow";
    internal const string HighlightPart = "PART_Highlight";
    internal const string CreateViewPart = "PART_CreateView";
    internal const string CreateBackPart = "PART_CreateBack";
    internal const string CreateTitlePart = "PART_CreateTitle";
    internal const string NameInputPart = "PART_NameInput";
    internal const string CreateErrorPart = "PART_CreateError";
    internal const string CreateErrorTextPart = "PART_CreateErrorText";
    internal const string CreateCancelPart = "PART_CreateCancel";
    internal const string CreateSubmitPart = "PART_CreateSubmit";
    internal const string JoinViewPart = "PART_JoinView";
    internal const string JoinBackPart = "PART_JoinBack";
    internal const string JoinTitlePart = "PART_JoinTitle";
    internal const string InviteInputPart = "PART_InviteInput";
    internal const string JoinErrorPart = "PART_JoinError";
    internal const string JoinErrorTextPart = "PART_JoinErrorText";
    internal const string JoinCancelPart = "PART_JoinCancel";
    internal const string JoinSubmitPart = "PART_JoinSubmit";

    /// <summary>The panel's width (the trigger's width).</summary>
    public static readonly StyledProperty<double> SurfaceWidthProperty =
        AvaloniaProperty.Register<TeamSwitcherMenu, double>(nameof(SurfaceWidth), double.NaN);

    /// <summary>A workspace row's height (token).</summary>
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<TeamSwitcherMenu, double>(nameof(RowHeight), 48);

    /// <summary>How many rows show before the list scrolls (token).</summary>
    public static readonly StyledProperty<int> VisibleRowsProperty =
        AvaloniaProperty.Register<TeamSwitcherMenu, int>(nameof(VisibleRows), 6);

    /// <summary>How far the list and the create and join views shift sideways as they crossfade (token).</summary>
    public static readonly StyledProperty<double> ViewShiftProperty =
        AvaloniaProperty.Register<TeamSwitcherMenu, double>(nameof(ViewShift), 6);

    /// <summary>The highlight glides on a bounce-free spring that settles in about 0.24s.</summary>
    internal static readonly SpringSpec GlideSpring = SpringSpec.FromVisual(0.24, 0);

    private static readonly bool s_mac = OperatingSystem.IsMacOS() || OperatingSystem.IsIOS();

    /// <summary>The list area is never shorter than this many rows (the forms need the room).</summary>
    internal const int MinListRows = 3;

    private readonly List<TeamSwitcherRow> _rows = [];
    private readonly List<Control> _items = [];
    private readonly TeamSwitcherViewMotion _views;
    private readonly ListKeyboardNavigator _navigator;
    private TeamSwitcher? _owner;
    private Panel? _listView;
    private Input? _search;
    private TextBlock? _section;
    private Panel? _listArea;
    private ScrollViewer? _scroll;
    private StackPanel? _rowsPanel;
    private Control? _empty;
    private TextBlock? _emptyText;
    private TextBlock? _emptyHint;
    private TeamSwitcherActionRow? _createRow;
    private TeamSwitcherActionRow? _joinRow;
    private GlidingIndicator? _highlight;
    private Control? _createView;
    private AvaloniaButton? _createBack;
    private TextBlock? _createTitle;
    private Input? _nameInput;
    private Control? _createError;
    private TextBlock? _createErrorText;
    private Button? _createCancel;
    private Button? _createSubmit;
    private Control? _joinView;
    private AvaloniaButton? _joinBack;
    private TextBlock? _joinTitle;
    private Input? _inviteInput;
    private Control? _joinError;
    private TextBlock? _joinErrorText;
    private Button? _joinCancel;
    private Button? _joinSubmit;
    private int _index = -1;
    private bool _keyboardScroll;
    private bool _placePosted;
    private TeamSwitcherView _view;
    private CancellationTokenSource? _formCts;

    public TeamSwitcherMenu()
    {
        _views = new TeamSwitcherViewMotion(this);
        _navigator = new ListKeyboardNavigator(() => _items.Count, _ => true) { Wrap = true, PageSize = 5 };
    }

    /// <inheritdoc cref="SurfaceWidthProperty"/>
    public double SurfaceWidth { get => GetValue(SurfaceWidthProperty); set => SetValue(SurfaceWidthProperty, value); }

    /// <inheritdoc cref="RowHeightProperty"/>
    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    /// <inheritdoc cref="VisibleRowsProperty"/>
    public int VisibleRows { get => GetValue(VisibleRowsProperty); set => SetValue(VisibleRowsProperty, value); }

    /// <inheritdoc cref="ViewShiftProperty"/>
    public double ViewShift { get => GetValue(ViewShiftProperty); set => SetValue(ViewShiftProperty, value); }

    internal TeamSwitcherView View => _view;

    internal int HighlightIndex => _index;

    /// <summary>The highlighted row or action, or null.</summary>
    internal Control? Highlighted => _index >= 0 && _index < _items.Count ? _items[_index] : null;

    /// <summary>Every workspace row, filtered out or not.</summary>
    internal IReadOnlyList<TeamSwitcherRow> Rows => _rows;

    /// <summary>The rows and actions the keyboard moves through, in order.</summary>
    internal IReadOnlyList<Control> Items => _items;

    internal Input? Search => _search;

    internal Input? NameInput => _nameInput;

    internal Input? InviteInput => _inviteInput;

    /// <summary>The error under the open form's field, or null.</summary>
    internal string? FormError => _view switch
    {
        TeamSwitcherView.Create when _createError is { IsVisible: true } => _createErrorText?.Text,
        TeamSwitcherView.Join when _joinError is { IsVisible: true } => _joinErrorText?.Text,
        _ => null
    };

    internal Button? CreateSubmit => _createSubmit;

    internal Button? JoinSubmit => _joinSubmit;

    internal Control? CreateView => _createView;

    internal Control? JoinView => _joinView;

    internal Panel? ListView => _listView;

    internal Control? Empty => _empty;

    internal GlidingIndicator? Highlight => _highlight;

    internal TeamSwitcherActionRow? CreateRow => _createRow;

    internal TeamSwitcherActionRow? JoinRow => _joinRow;

    /// <summary>Whether a create or join request is running.</summary>
    internal bool IsSubmitting => _formCts is not null;

    private TeamSwitcherLabels Labels => _owner?.Labels ?? TeamSwitcherLabels.Default;

    internal void Attach(TeamSwitcher owner) => _owner = owner;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        var scope = e.NameScope;
        _listView = scope.Find<Panel>(ListViewPart);
        _search = scope.Find<Input>(SearchPart);
        _section = scope.Find<TextBlock>(SectionPart);
        _listArea = scope.Find<Panel>(ListAreaPart);
        _scroll = scope.Find<ScrollViewer>(ScrollPart);
        _rowsPanel = scope.Find<StackPanel>(RowsPart);
        _empty = scope.Find<Control>(EmptyPart);
        _emptyText = scope.Find<TextBlock>(EmptyTextPart);
        _emptyHint = scope.Find<TextBlock>(EmptyHintPart);
        _createRow = scope.Find<TeamSwitcherActionRow>(CreateRowPart);
        _joinRow = scope.Find<TeamSwitcherActionRow>(JoinRowPart);
        _highlight = scope.Find<GlidingIndicator>(HighlightPart);
        _createView = scope.Find<Control>(CreateViewPart);
        _createBack = scope.Find<AvaloniaButton>(CreateBackPart);
        _createTitle = scope.Find<TextBlock>(CreateTitlePart);
        _nameInput = scope.Find<Input>(NameInputPart);
        _createError = scope.Find<Control>(CreateErrorPart);
        _createErrorText = scope.Find<TextBlock>(CreateErrorTextPart);
        _createCancel = scope.Find<Button>(CreateCancelPart);
        _createSubmit = scope.Find<Button>(CreateSubmitPart);
        _joinView = scope.Find<Control>(JoinViewPart);
        _joinBack = scope.Find<AvaloniaButton>(JoinBackPart);
        _joinTitle = scope.Find<TextBlock>(JoinTitlePart);
        _inviteInput = scope.Find<Input>(InviteInputPart);
        _joinError = scope.Find<Control>(JoinErrorPart);
        _joinErrorText = scope.Find<TextBlock>(JoinErrorTextPart);
        foreach (var text in new[] { _createErrorText, _joinErrorText })
        {
            if (text is not null)
            {
                AutomationProperties.SetLiveSetting(text, AutomationLiveSetting.Polite);
            }
        }
        _joinCancel = scope.Find<Button>(JoinCancelPart);
        _joinSubmit = scope.Find<Button>(JoinSubmitPart);
        if (_search is not null)
        {
            _search.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
            _search.PropertyChanged += OnSearchPropertyChanged;
        }
        if (_scroll is not null)
        {
            _scroll.ScrollChanged += OnScrollChanged;
        }
        if (_createRow is not null)
        {
            _createRow.Click += OnCreateRowClick;
            _createRow.PointerEntered += OnItemPointerEntered;
        }
        if (_joinRow is not null)
        {
            _joinRow.Click += OnJoinRowClick;
            _joinRow.PointerEntered += OnItemPointerEntered;
        }
        if (_listView is not null)
        {
            _listView.AddHandler(KeyDownEvent, OnListViewKeyDown, RoutingStrategies.Tunnel);
        }
        if (_createView is not null)
        {
            _createView.AddHandler(KeyDownEvent, OnFormKeyDown, RoutingStrategies.Tunnel);
            _createView.IsVisible = false;
        }
        if (_joinView is not null)
        {
            _joinView.AddHandler(KeyDownEvent, OnFormKeyDown, RoutingStrategies.Tunnel);
            _joinView.IsVisible = false;
        }
        Hook(_createBack, OnBack);
        Hook(_joinBack, OnBack);
        Hook(_createCancel, OnBack);
        Hook(_joinCancel, OnBack);
        Hook(_createSubmit, OnSubmit);
        Hook(_joinSubmit, OnSubmit);
        _view = TeamSwitcherView.List;
        ApplyLabels();
        Rebuild();
    }

    private static void Hook(AvaloniaButton? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click += handler;
        }
    }

    private static void Unhook(AvaloniaButton? button, EventHandler<RoutedEventArgs> handler)
    {
        if (button is not null)
        {
            button.Click -= handler;
        }
    }

    private void Unhook()
    {
        if (_search is not null)
        {
            _search.RemoveHandler(KeyDownEvent, OnSearchKeyDown);
            _search.PropertyChanged -= OnSearchPropertyChanged;
        }
        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
        }
        if (_createRow is not null)
        {
            _createRow.Click -= OnCreateRowClick;
            _createRow.PointerEntered -= OnItemPointerEntered;
        }
        if (_joinRow is not null)
        {
            _joinRow.Click -= OnJoinRowClick;
            _joinRow.PointerEntered -= OnItemPointerEntered;
        }
        _listView?.RemoveHandler(KeyDownEvent, OnListViewKeyDown);
        _createView?.RemoveHandler(KeyDownEvent, OnFormKeyDown);
        _joinView?.RemoveHandler(KeyDownEvent, OnFormKeyDown);
        Unhook(_createBack, OnBack);
        Unhook(_joinBack, OnBack);
        Unhook(_createCancel, OnBack);
        Unhook(_joinCancel, OnBack);
        Unhook(_createSubmit, OnSubmit);
        Unhook(_joinSubmit, OnSubmit);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RowHeightProperty || change.Property == VisibleRowsProperty)
        {
            SizeList();
        }
        else if (change.Property == ViewShiftProperty)
        {
            _views.Shift = ViewShift;
        }
    }

    /// <summary>Puts every word in place (after the labels changed).</summary>
    internal void ApplyLabels()
    {
        var labels = Labels;
        if (_search is not null)
        {
            _search.Placeholder = labels.Search;
            AutomationProperties.SetName(_search, labels.Search);
        }
        if (_section is not null)
        {
            _section.Text = labels.Workspaces;
        }
        if (_rowsPanel is not null)
        {
            AutomationProperties.SetName(_rowsPanel, labels.ListName);
        }
        if (_emptyHint is not null)
        {
            _emptyHint.Text = labels.NoMatchesHint;
        }
        if (_createRow is not null)
        {
            _createRow.Text = labels.CreateWorkspace;
            AutomationProperties.SetName(_createRow, labels.CreateWorkspace);
        }
        if (_joinRow is not null)
        {
            _joinRow.Text = labels.JoinWorkspace;
            AutomationProperties.SetName(_joinRow, labels.JoinWorkspace);
        }
        if (_createTitle is not null)
        {
            _createTitle.Text = labels.CreateWorkspace;
        }
        if (_joinTitle is not null)
        {
            _joinTitle.Text = labels.JoinWorkspace;
        }
        foreach (var back in new[] { _createBack, _joinBack })
        {
            if (back is not null)
            {
                AutomationProperties.SetName(back, labels.Back);
                Tooltip.SetTip(back, labels.Back);
            }
        }
        if (_nameInput is not null)
        {
            _nameInput.Label = labels.NameLabel;
            _nameInput.Placeholder = labels.NamePlaceholder;
            _nameInput.Description = labels.NameDescription;
        }
        if (_inviteInput is not null)
        {
            _inviteInput.Label = labels.InviteLabel;
            _inviteInput.Placeholder = labels.InvitePlaceholder;
            _inviteInput.Description = _owner is { JoinHandler: null, SimulatedInvites: { Count: > 0 } invites }
                ? labels.SampleInvites(TeamSwitcherLogic.InviteCodes(invites))
                : labels.InviteDescription;
        }
        if (_createCancel is not null)
        {
            _createCancel.Content = labels.Cancel;
        }
        if (_joinCancel is not null)
        {
            _joinCancel.Content = labels.Cancel;
        }
        if (_createSubmit is not null)
        {
            _createSubmit.Content = labels.Create;
        }
        if (_joinSubmit is not null)
        {
            _joinSubmit.Content = labels.Join;
        }
    }

    /// <summary>Builds one row per workspace (after the list changed), then filters again.</summary>
    internal void Rebuild()
    {
        if (_rowsPanel is null || _owner is null)
        {
            return;
        }
        foreach (var row in _rows)
        {
            row.Click -= OnRowClick;
            row.PointerEntered -= OnItemPointerEntered;
        }
        _rows.Clear();
        _rowsPanel.Children.Clear();
        var workspaces = _owner.EffectiveWorkspaces;
        for (var i = 0; i < workspaces.Count; i++)
        {
            var row = new TeamSwitcherRow(workspaces[i], i);
            row.Click += OnRowClick;
            row.PointerEntered += OnItemPointerEntered;
            _rows.Add(row);
            _rowsPanel.Children.Add(row);
        }
        RefreshRows();
        SizeList();
        Refilter(keepHighlight: true);
    }

    /// <summary>Brings each row's texts and its check in line with the workspace and the active id.</summary>
    internal void RefreshRows()
    {
        if (_owner is null)
        {
            return;
        }
        var labels = Labels;
        var current = _owner.CurrentWorkspace?.Id;
        foreach (var row in _rows)
        {
            var w = row.Workspace!;
            var planLine = labels.PlanLine(w.Plan, w.Members);
            row.PlanLine = planLine;
            row.Role = string.IsNullOrEmpty(w.Role) ? null : w.Role;
            var n = row.Index + 1;
            row.Shortcut = n <= 9 ? labels.Shortcut(n, s_mac) : null;
            row.IsCurrent = w.Id == current;
            var parts = new List<string> { w.Name };
            if (planLine.Length > 0)
            {
                parts.Add(planLine);
            }
            if (row.Role is { } role)
            {
                parts.Add(role);
            }
            if (n <= 9)
            {
                parts.Add(labels.ShortcutName(n, s_mac));
            }
            if (row.IsCurrent)
            {
                parts.Add(labels.Current);
            }
            AutomationProperties.SetName(row, string.Join(", ", parts));
        }
        if (_index >= 0)
        {
            AnnounceHighlight();
        }
    }

    /// <summary>The list area holds up to <see cref="VisibleRows"/> rows; filtering never changes its height.</summary>
    private void SizeList()
    {
        if (_listArea is null)
        {
            return;
        }
        // At least three rows tall even for one or two workspaces, so the create and join forms (with an error line)
        // fit in the panel without it growing when they open.
        var visible = Math.Max(MinListRows, VisibleRows);
        var count = Math.Clamp(_rows.Count, MinListRows, visible);
        _listArea.Height = count * RowHeight;
    }

    /// <summary>Resets the menu for an open: the list view, an empty search, the highlight on the active workspace.</summary>
    internal void Reset()
    {
        CancelForm();
        _view = TeamSwitcherView.List;
        if (_listView is not null)
        {
            _listView.IsEnabled = true;
            _listView.IsHitTestVisible = true;
            _views.Set(_listView, shown: true);
        }
        foreach (var view in new[] { _createView, _joinView })
        {
            if (view is not null)
            {
                _views.Set(view, shown: false);
                view.IsVisible = false;
            }
        }
        _highlight?.Hide(animate: false);
        _index = -1;
        if (_search is not null && !string.IsNullOrEmpty(_search.Text))
        {
            _search.Text = string.Empty;
        }
        ApplyLabels();
        RefreshRows();
        Refilter(keepHighlight: false);
        _scroll?.ScrollToHome();
        var current = _owner?.CurrentWorkspace;
        var start = _items.FindIndex(i => i is TeamSwitcherRow r && ReferenceEquals(r.Workspace, current));
        MoveTo(start >= 0 ? start : 0, animate: false);
    }

    /// <summary>Puts focus in the search field (or the open form's field).</summary>
    internal void FocusField()
    {
        var target = _view switch
        {
            TeamSwitcherView.Create => _nameInput,
            TeamSwitcherView.Join => _inviteInput,
            _ => _search
        };
        target?.Focus(NavigationMethod.Tab);
    }

    /// <summary>Whether focus is inside the panel.</summary>
    internal bool HasFocusWithin => IsKeyboardFocusWithin;

    /// <summary>Called when the popup has closed: stops a running form request and hides the highlight.</summary>
    internal void OnClosed()
    {
        CancelForm();
        _highlight?.Hide(animate: false);
    }

    private void OnSearchPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextBox.TextProperty)
        {
            Refilter(keepHighlight: false);
        }
    }

    private void Refilter(bool keepHighlight)
    {
        if (_owner is null || _createRow is null || _joinRow is null)
        {
            return;
        }
        var previous = Highlighted;
        var hadHighlight = _index >= 0;
        var query = _search?.Text ?? string.Empty;
        var matches = TeamSwitcherLogic.Filter(_owner.EffectiveWorkspaces, query);
        var visible = new HashSet<int>(matches);
        _items.Clear();
        foreach (var row in _rows)
        {
            row.IsVisible = visible.Contains(row.Index);
            if (row.IsVisible)
            {
                _items.Add(row);
            }
        }
        _items.Add(_createRow);
        _items.Add(_joinRow);
        var none = matches.Count == 0;
        if (_empty is not null)
        {
            _empty.IsVisible = none;
        }
        if (_scroll is not null)
        {
            _scroll.IsVisible = !none;
        }
        if (_emptyText is not null)
        {
            // With no workspaces at all (and nothing typed) the list just says so.
            _emptyText.Text = query.Trim().Length > 0 ? Labels.NoMatches(query.Trim()) : Labels.NoWorkspace;
        }
        if (_emptyHint is not null)
        {
            _emptyHint.IsVisible = query.Trim().Length > 0;
        }
        _index = -1;
        if (keepHighlight && previous is not null && _items.IndexOf(previous) is >= 0 and var kept)
        {
            MoveTo(kept, animate: false);
            return;
        }
        if (hadHighlight)
        {
            // A new search starts on its first match (or on Create workspace when nothing matches), even when the
            // highlight has not been placed yet (the rows were not laid out) or sat on a row scrolled out of view.
            MoveTo(0, animate: true);
        }
    }

    /// <summary>Highlights item <paramref name="index"/> (a row or an action); -1 hides the highlight.</summary>
    internal void MoveTo(int index, bool animate, bool scroll = true)
    {
        _index = index < _items.Count ? index : -1;
        if (_index < 0)
        {
            _highlight?.Hide(animate);
            if (_search is not null)
            {
                AutomationProperties.SetHelpText(_search, null);
            }
            return;
        }
        AnnounceHighlight();
        if (scroll && _items[_index] is TeamSwitcherRow row && _scroll is not null)
        {
            _keyboardScroll = true;
            row.BringIntoView();
        }
        PlaceHighlight(animate);
    }

    /// <summary>The search field points at the highlighted row (Arc: aria-activedescendant) through its help text.</summary>
    private void AnnounceHighlight()
    {
        if (_search is not null && Highlighted is { } item)
        {
            AutomationProperties.SetHelpText(_search, AutomationProperties.GetName(item));
        }
    }

    private void PlaceHighlight(bool animate)
    {
        if (_highlight is null || _listView is null || Highlighted is not { } item)
        {
            return;
        }
        if (item.Bounds.Width <= 0 || item.TranslatePoint(default, _listView) is not { } origin)
        {
            // Not laid out yet (just opened or just filtered): place it once the rows have been arranged.
            if (!_placePosted)
            {
                _placePosted = true;
                Dispatcher.UIThread.Post(() =>
                {
                    _placePosted = false;
                    if (Highlighted is { Bounds.Width: > 0 })
                    {
                        PlaceHighlight(animate: false);
                    }
                }, DispatcherPriority.Loaded);
            }
            return;
        }
        var rect = new Rect(origin, item.Bounds.Size);
        if (item is TeamSwitcherRow && _listArea is not null && _listArea.TranslatePoint(default, _listView) is { } area)
        {
            // A row that is partly scrolled out keeps the highlight inside the list.
            rect = rect.Intersect(new Rect(area, _listArea.Bounds.Size));
            if (rect.Height <= 0)
            {
                _highlight.Hide(animate);
                return;
            }
        }
        _highlight.MoveTo(rect, GlideSpring, animate);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        var animate = _keyboardScroll;
        _keyboardScroll = false;
        PlaceHighlight(animate);
    }

    private void OnItemPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Control item && e.Pointer.Type != PointerType.Touch && _items.IndexOf(item) is >= 0 and var index && index != _index)
        {
            MoveTo(index, animate: true, scroll: false);
        }
    }

    private void OnRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is TeamSwitcherRow { Workspace: { } workspace })
        {
            _owner?.Choose(workspace);
        }
    }

    private void OnCreateRowClick(object? sender, RoutedEventArgs e) => OpenForm(TeamSwitcherView.Create);

    private void OnJoinRowClick(object? sender, RoutedEventArgs e) => OpenForm(TeamSwitcherView.Join);

    /// <summary>Activates the highlighted item: switches, or opens Create workspace or Join with invite.</summary>
    internal void Activate()
    {
        switch (Highlighted)
        {
            case TeamSwitcherRow { Workspace: { } workspace }:
                _owner?.Choose(workspace);
                break;
            case TeamSwitcherActionRow action when ReferenceEquals(action, _createRow):
                OpenForm(TeamSwitcherView.Create);
                break;
            case TeamSwitcherActionRow action when ReferenceEquals(action, _joinRow):
                OpenForm(TeamSwitcherView.Join);
                break;
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift))
        {
            return;
        }
        var empty = string.IsNullOrEmpty(_search?.Text);
        switch (e.Key)
        {
            case Key.Down:
            case Key.Up:
            case Key.PageDown:
            case Key.PageUp:
            {
                if (_index < 0)
                {
                    MoveTo(e.Key is Key.Up or Key.PageUp ? _items.Count - 1 : 0, animate: true);
                }
                else if (_navigator.Navigate(_index, e.Key) is { } next)
                {
                    MoveTo(next, animate: true);
                }
                e.Handled = true;
                return;
            }
            case Key.Home when empty:
            case Key.End when empty:
                if (_navigator.Navigate(_index, e.Key) is { } edge)
                {
                    MoveTo(edge, animate: true);
                }
                e.Handled = true;
                return;
            case Key.Enter:
                Activate();
                e.Handled = true;
                return;
            case Key.Escape:
                if (!empty)
                {
                    _search!.Text = string.Empty;
                }
                else
                {
                    _owner?.CloseMenu(returnFocus: true);
                }
                e.Handled = true;
                return;
        }
    }

    /// <summary>Tab leaves the list view (its only stop is the search field): the menu closes and the trigger gets focus.</summary>
    private void OnListViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && _view == TeamSwitcherView.List)
        {
            e.Handled = true;
            _owner?.CloseMenu(returnFocus: true);
        }
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                Back();
                return;
            case Key.Enter when e.Source is TextBox:
                e.Handled = true;
                Submit();
                return;
            case Key.Tab:
            {
                // Tabbing past the last control (or before the first) leaves the menu.
                var view = _view == TeamSwitcherView.Create ? _createView : _joinView;
                var first = _view == TeamSwitcherView.Create ? _createBack : _joinBack;
                var last = _view == TeamSwitcherView.Create ? _createSubmit : _joinSubmit;
                var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
                if (view is not null && ((shift && HasFocus(first)) || (!shift && HasFocus(last))))
                {
                    e.Handled = true;
                    _owner?.CloseMenu(returnFocus: true);
                }
                return;
            }
        }
    }

    private static bool HasFocus(Control? control) => control is not null && control.IsFocused;

    /// <summary>Opens the create or join form; Create starts from the search text.</summary>
    internal void OpenForm(TeamSwitcherView view)
    {
        if (_listView is null || _view != TeamSwitcherView.List)
        {
            return;
        }
        var target = view == TeamSwitcherView.Create ? _createView : _joinView;
        var input = view == TeamSwitcherView.Create ? _nameInput : _inviteInput;
        var submit = view == TeamSwitcherView.Create ? _createSubmit : _joinSubmit;
        if (target is null || input is null)
        {
            return;
        }
        ApplyLabels();
        input.Text = view == TeamSwitcherView.Create ? (_search?.Text ?? string.Empty).Trim() : string.Empty;
        SetFormError(view, null);
        input.IsEnabled = true;
        if (submit is not null)
        {
            submit.IsLoading = false;
        }
        _view = view;
        _listView.IsEnabled = false;
        _listView.IsHitTestVisible = false;
        _views.Swap(_listView, target, direction: 1, hide: _ => { });
        Dispatcher.UIThread.Post(() =>
        {
            if (_view == view)
            {
                input.Focus(NavigationMethod.Tab);
                input.SelectAll();
            }
        }, DispatcherPriority.Loaded);
    }

    private void OnBack(object? sender, RoutedEventArgs e) => Back();

    /// <summary>Goes back from the form to the list (Escape, Back, Cancel); a running request is abandoned.</summary>
    internal void Back()
    {
        if (_view == TeamSwitcherView.List || _listView is null)
        {
            return;
        }
        CancelForm();
        var from = _view == TeamSwitcherView.Create ? _createView : _joinView;
        _view = TeamSwitcherView.List;
        _listView.IsEnabled = true;
        _listView.IsHitTestVisible = true;
        _views.Swap(from, _listView, direction: -1, hide: view => view.IsVisible = false);
        Dispatcher.UIThread.Post(() =>
        {
            if (_view == TeamSwitcherView.List)
            {
                _search?.Focus(NavigationMethod.Tab);
                PlaceHighlight(animate: false);
            }
        }, DispatcherPriority.Loaded);
    }

    private void OnSubmit(object? sender, RoutedEventArgs e) => Submit();

    /// <summary>Sends the open form: the app's delegate (or the sample) creates or joins, then the switcher moves there.</summary>
    internal async void Submit()
    {
        if (_owner is null || _view == TeamSwitcherView.List || _formCts is not null)
        {
            return;
        }
        var create = _view == TeamSwitcherView.Create;
        var input = create ? _nameInput : _inviteInput;
        var submit = create ? _createSubmit : _joinSubmit;
        if (input is null)
        {
            return;
        }
        var text = input.Text ?? string.Empty;
        if (text.Trim().Length == 0)
        {
            SetFormError(_view, create ? Labels.NameRequired : Labels.InviteRequired);
            return;
        }
        var cts = new CancellationTokenSource();
        _formCts = cts;
        SetFormError(_view, null);
        if (submit is not null)
        {
            submit.IsLoading = true;
        }
        try
        {
            var kind = create ? TeamSwitcherAddKind.Created : TeamSwitcherAddKind.Joined;
            var (workspace, simulated) = await _owner.AddAsync(kind, text.Trim(), cts.Token);
            if (!ReferenceEquals(_formCts, cts))
            {
                return;
            }
            _formCts = null;
            if (submit is not null)
            {
                submit.IsLoading = false;
            }
            _owner.Added(workspace, kind, simulated);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            if (!ReferenceEquals(_formCts, cts))
            {
                return;
            }
            _formCts = null;
            if (submit is not null)
            {
                submit.IsLoading = false;
            }
            SetFormError(create ? TeamSwitcherView.Create : TeamSwitcherView.Join,
                string.IsNullOrWhiteSpace(error.Message) ? Labels.GenericError : error.Message);
            input.Focus(NavigationMethod.Tab);
        }
    }

    /// <summary>
    /// Shows <paramref name="message"/> under the form's field with an alert icon (not color alone) and marks the field
    /// invalid; null clears both. The field's help text carries the message for screen readers.
    /// </summary>
    private void SetFormError(TeamSwitcherView view, string? message)
    {
        var create = view == TeamSwitcherView.Create;
        var input = create ? _nameInput : _inviteInput;
        var line = create ? _createError : _joinError;
        var text = create ? _createErrorText : _joinErrorText;
        var shown = !string.IsNullOrEmpty(message);
        if (text is not null)
        {
            text.Text = message;
        }
        if (line is not null)
        {
            line.IsVisible = shown;
        }
        if (input is not null)
        {
            input.IsInvalid = shown;
            // The field's own format: its description, then the error.
            var description = input.Description;
            AutomationProperties.SetHelpText(input, !shown ? description ?? string.Empty
                : string.IsNullOrEmpty(description) ? message : description + " " + message);
        }
    }

    private void CancelForm()
    {
        if (_formCts is { } cts)
        {
            _formCts = null;
            cts.Cancel();
        }
        if (_createSubmit is not null)
        {
            _createSubmit.IsLoading = false;
        }
        if (_joinSubmit is not null)
        {
            _joinSubmit.IsLoading = false;
        }
    }
}
