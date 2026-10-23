using System.Globalization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(SubtitlePart, typeof(TextBlock))]
[TemplatePart(MeterPart, typeof(MemoryMeter))]
[TemplatePart(SavedSettingPart, typeof(AgentMemorySettingRow))]
[TemplatePart(HistorySettingPart, typeof(AgentMemorySettingRow))]
[TemplatePart(FiltersPart, typeof(PillFilterBar))]
[TemplatePart(SearchPart, typeof(AgentMemorySearchBox))]
[TemplatePart(PillHostPart, typeof(AgentMemoryPresence))]
[TemplatePart(PillPart, typeof(MemoryCapturedPill))]
[TemplatePart(ListHostPart, typeof(Border))]
[TemplatePart(ListPart, typeof(ScrollArea))]
[TemplatePart(GroupsPart, typeof(StackPanel))]
[TemplatePart(EmptySearchPart, typeof(AgentMemoryPresence))]
[TemplatePart(NoMatchesPart, typeof(TextBlock))]
[TemplatePart(ClearSearchPart, typeof(AgentMemoryButton))]
[TemplatePart(AddFieldPart, typeof(AgentMemoryAddField))]
[TemplatePart(AnnouncerPart, typeof(AiAnnouncer))]
[PseudoClasses(CompactPseudoClass, PausedPseudoClass, NoMeterPseudoClass, NoAddPseudoClass)]
public partial class AgentMemory
{
    public const string TitlePart = "PART_Title";
    public const string SubtitlePart = "PART_Subtitle";
    public const string MeterPart = "PART_Meter";
    public const string SavedSettingPart = "PART_SavedSetting";
    public const string HistorySettingPart = "PART_HistorySetting";
    public const string FiltersPart = "PART_Filters";
    public const string SearchPart = "PART_Search";
    public const string PillHostPart = "PART_PillHost";
    public const string PillPart = "PART_Pill";
    public const string ListHostPart = "PART_ListHost";
    public const string ListPart = "PART_List";
    public const string GroupsPart = "PART_Groups";
    public const string EmptySearchPart = "PART_EmptySearch";
    public const string NoMatchesPart = "PART_NoMatches";
    public const string ClearSearchPart = "PART_ClearSearch";
    public const string AddFieldPart = "PART_AddField";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the control is narrower than the compact width (440): tighter paddings, scrolling filters.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while saved memories are off: the list is dimmed and the captured pill gives way to the paused notice.</summary>
    public const string PausedPseudoClass = ":paused";

    /// <summary>Set while <see cref="Capacity"/> is zero or less (no meter).</summary>
    public const string NoMeterPseudoClass = ":no-meter";

    /// <summary>Set while <see cref="IsAddEnabled"/> is false (no add field).</summary>
    public const string NoAddPseudoClass = ":no-add";

    #region Token-driven internals

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AgentMemory, double>(nameof(CompactWidth), 440);

    internal static readonly StyledProperty<TimeSpan> CapturedHoldProperty =
        AvaloniaProperty.Register<AgentMemory, TimeSpan>(nameof(CapturedHold), TimeSpan.FromMilliseconds(1800));

    internal static readonly StyledProperty<TimeSpan> AddedHoldProperty =
        AvaloniaProperty.Register<AgentMemory, TimeSpan>(nameof(AddedHold), TimeSpan.FromMilliseconds(2000));

    internal static readonly StyledProperty<TimeSpan> ArrivalDurationProperty =
        AvaloniaProperty.Register<AgentMemory, TimeSpan>(nameof(ArrivalDuration), TimeSpan.FromMilliseconds(1600));

    internal static readonly StyledProperty<TimeSpan> SimulatedSaveProperty =
        AvaloniaProperty.Register<AgentMemory, TimeSpan>(nameof(SimulatedSave), TimeSpan.FromMilliseconds(380));

    internal static readonly StyledProperty<TimeSpan> SimulatedSortProperty =
        AvaloniaProperty.Register<AgentMemory, TimeSpan>(nameof(SimulatedSort), TimeSpan.FromMilliseconds(650));

    internal static readonly StyledProperty<double> ListFadeTopProperty =
        AvaloniaProperty.Register<AgentMemory, double>(nameof(ListFadeTop), 20);

    internal static readonly StyledProperty<double> ListFadeBottomProperty =
        AvaloniaProperty.Register<AgentMemory, double>(nameof(ListFadeBottom), 36);

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
    internal TimeSpan CapturedHold { get => GetValue(CapturedHoldProperty); set => SetValue(CapturedHoldProperty, value); }
    internal TimeSpan AddedHold { get => GetValue(AddedHoldProperty); set => SetValue(AddedHoldProperty, value); }
    internal TimeSpan ArrivalDuration { get => GetValue(ArrivalDurationProperty); set => SetValue(ArrivalDurationProperty, value); }
    internal TimeSpan SimulatedSave { get => GetValue(SimulatedSaveProperty); set => SetValue(SimulatedSaveProperty, value); }
    internal TimeSpan SimulatedSort { get => GetValue(SimulatedSortProperty); set => SetValue(SimulatedSortProperty, value); }
    internal double ListFadeTop { get => GetValue(ListFadeTopProperty); set => SetValue(ListFadeTopProperty, value); }
    internal double ListFadeBottom { get => GetValue(ListFadeBottomProperty); set => SetValue(ListFadeBottomProperty, value); }

    #endregion

    #region Seams (tests)

    /// <summary>Waits for the simulated save and sort, the captured pill's hold and the "Added to" hold (tests replace it).</summary>
    internal Func<TimeSpan, CancellationToken, Task> DelayAsync { get; set; } = static (delay, token) => Task.Delay(delay, token);

    /// <summary>The clock of the undo countdowns (tests give their own).</summary>
    internal UndoClock UndoClock { get; set; } = UndoClock.Shared;

    /// <summary>The time a new memory is dated with (tests give their own).</summary>
    internal TimeProvider Time { get; set; } = TimeProvider.System;

    #endregion

    private TextBlock? _titleText;
    private TextBlock? _subtitleText;
    private MemoryMeter? _meter;
    private AgentMemorySettingRow? _savedSetting;
    private AgentMemorySettingRow? _historySetting;
    private PillFilterBar? _filters;
    private AgentMemorySearchBox? _search;
    private AgentMemoryPresence? _pillHost;
    private MemoryCapturedPill? _pill;
    private Border? _listHost;
    private ScrollArea? _list;
    private StackPanel? _groupsPanel;
    private AgentMemoryPresence? _emptySearch;
    private TextBlock? _noMatches;
    private AgentMemoryButton? _clearSearch;
    private AgentMemoryAddField? _addField;
    private AiAnnouncer? _announcer;

    private bool _isCompact;
    private bool _keyboardMode;
    private bool _touch;
    private bool _attached;
    private CancellationTokenSource _lifetime = new();
    private (bool Top, bool Bottom, double Height) _fadeState;
    private LinearGradientBrush? _fadeTemplate;

    public AgentMemory()
    {
        // Where the last input came from decides whether a focus move shows the keyboard ring (and a tooltip).
        AddHandler(KeyDownEvent, OnAnyKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnAnyPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        Memories.CollectionChanged += OnOwnMemoriesChanged;
        Groups.CollectionChanged += OnGroupsChanged;
        Captured.CollectionChanged += OnOwnCapturedChanged;
    }

    static AgentMemory()
    {
        FocusableProperty.OverrideDefaultValue<AgentMemory>(false);
    }

    /// <summary>True while the control is narrower than the compact width.</summary>
    internal bool IsCompact => _isCompact;

    /// <summary>The live region's last announcement (tests).</summary>
    internal string? LastAnnouncement => _announcer?.Announcement;

    internal PillFilterBar? FilterBar => _filters;
    internal AgentMemorySearchBox? SearchBox => _search;
    internal MemoryCapturedPill? CapturedPill => _pill;
    internal AgentMemoryPresence? PillHost => _pillHost;
    internal MemoryMeter? Meter => _meter;
    internal AgentMemorySettingRow? SavedSettingRow => _savedSetting;
    internal AgentMemorySettingRow? HistorySettingRow => _historySetting;
    internal AgentMemoryAddField? AddField => _addField;
    internal ScrollArea? List => _list;
    internal Border? ListHost => _listHost;
    internal AgentMemoryPresence? EmptySearch => _emptySearch;
    internal AgentMemoryButton? ClearSearchButton => _clearSearch;
    internal TextBlock? NoMatchesText => _noMatches;
    internal TextBlock? TitleText => _titleText;
    internal TextBlock? SubtitleText => _subtitleText;

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        UnhookParts();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _titleText = scope.Find<TextBlock>(TitlePart);
        _subtitleText = scope.Find<TextBlock>(SubtitlePart);
        _meter = scope.Find<MemoryMeter>(MeterPart);
        _savedSetting = scope.Find<AgentMemorySettingRow>(SavedSettingPart);
        _historySetting = scope.Find<AgentMemorySettingRow>(HistorySettingPart);
        _filters = scope.Find<PillFilterBar>(FiltersPart);
        _search = scope.Find<AgentMemorySearchBox>(SearchPart);
        _pillHost = scope.Find<AgentMemoryPresence>(PillHostPart);
        _pill = scope.Find<MemoryCapturedPill>(PillPart);
        _listHost = scope.Find<Border>(ListHostPart);
        _list = scope.Find<ScrollArea>(ListPart);
        _groupsPanel = scope.Find<StackPanel>(GroupsPart);
        _emptySearch = scope.Find<AgentMemoryPresence>(EmptySearchPart);
        _noMatches = scope.Find<TextBlock>(NoMatchesPart);
        _clearSearch = scope.Find<AgentMemoryButton>(ClearSearchPart);
        _addField = scope.Find<AgentMemoryAddField>(AddFieldPart);
        _announcer = scope.Find<AiAnnouncer>(AnnouncerPart);
        HookParts();

        // A new template starts over: views are made again, in their current state and without motion.
        foreach (var entry in _entries.Values)
        {
            entry.Row = null;
            entry.View = null;
        }
        _groupViews.Clear();
        _listSynced = false;
        _pillHost?.Set(false);
        _emptySearch?.Set(false);
        ApplyLabels();
        RebuildGroups();
        SyncEntries();
        RefreshList(animate: false);
        UpdatePill(animate: false);
        UpdateSearchParts();
        UpdateAddStatus();
        UpdatePseudoClasses();
        UpdateCompact(Bounds.Width);
    }

    private void HookParts()
    {
        if (_savedSetting is not null)
        {
            _savedSetting.Toggled += OnSavedToggled;
        }
        if (_historySetting is not null)
        {
            _historySetting.Toggled += OnHistoryToggled;
        }
        if (_filters is not null)
        {
            _filters.PropertyChanged += OnFiltersPropertyChanged;
        }
        if (_search is not null)
        {
            _search.OpenRequested += OnSearchOpenRequested;
            _search.CloseRequested += OnSearchCloseRequested;
            _search.PropertyChanged += OnSearchPropertyChanged;
        }
        if (_pill is not null)
        {
            _pill.Click += OnPillClick;
        }
        if (_list is not null)
        {
            _list.EdgesChanged += OnListEdgesChanged;
            _list.SizeChanged += OnListSizeChanged;
        }
        if (_clearSearch is not null)
        {
            _clearSearch.Click += OnClearSearchClick;
        }
        if (_addField is not null)
        {
            _addField.Submitted += OnAddSubmitted;
            _addField.PropertyChanged += OnAddFieldPropertyChanged;
        }
    }

    private void UnhookParts()
    {
        if (_savedSetting is not null)
        {
            _savedSetting.Toggled -= OnSavedToggled;
        }
        if (_historySetting is not null)
        {
            _historySetting.Toggled -= OnHistoryToggled;
        }
        if (_filters is not null)
        {
            _filters.PropertyChanged -= OnFiltersPropertyChanged;
        }
        if (_search is not null)
        {
            _search.OpenRequested -= OnSearchOpenRequested;
            _search.CloseRequested -= OnSearchCloseRequested;
            _search.PropertyChanged -= OnSearchPropertyChanged;
        }
        if (_pill is not null)
        {
            _pill.Click -= OnPillClick;
        }
        if (_list is not null)
        {
            _list.EdgesChanged -= OnListEdgesChanged;
            _list.SizeChanged -= OnListSizeChanged;
        }
        if (_clearSearch is not null)
        {
            _clearSearch.Click -= OnClearSearchClick;
        }
        if (_addField is not null)
        {
            _addField.Submitted -= OnAddSubmitted;
            _addField.PropertyChanged -= OnAddFieldPropertyChanged;
        }
        _groupsPanel?.Children.Clear();
    }

    /// <summary>Every word on screen and every accessible name, from <see cref="Labels"/> and the assistant's name.</summary>
    private void ApplyLabels()
    {
        var labels = Labels;
        var name = AssistantName;
        if (_titleText is not null)
        {
            _titleText.Text = Title ?? labels.Title;
        }
        if (_subtitleText is not null)
        {
            _subtitleText.Text = Subtitle ?? labels.Subtitle(name);
        }
        if (_meter is not null)
        {
            _meter.FullText = labels.MeterFull;
            _meter.AccessibleName = labels.MeterName;
            _meter.ValueText = labels.MeterValueText;
        }
        if (_savedSetting is not null)
        {
            _savedSetting.Title = labels.SavedMemoriesTitle;
        }
        if (_historySetting is not null)
        {
            _historySetting.Title = labels.ChatHistoryTitle;
        }
        UpdateSettings();
        if (_filters is not null)
        {
            _filters.AccessibleName = labels.FilterName;
        }
        if (_search is not null)
        {
            _search.PlaceholderText = labels.SearchPlaceholder;
            _search.SearchName = labels.Search;
            _search.CloseName = labels.CloseSearch;
        }
        if (_clearSearch is not null)
        {
            _clearSearch.Content = labels.ClearSearch;
        }
        if (_list is not null)
        {
            AutomationProperties.SetName(_list, labels.SectionName);
        }
        if (_addField is not null)
        {
            _addField.PlaceholderText = labels.AddPlaceholder;
            _addField.AddText = labels.Add;
        }
        if (_pill is not null)
        {
            _pill.Title = labels.Captured;
            _pill.PausedText = labels.PausedNotice(name);
        }
        foreach (var entry in _entries.Values)
        {
            if (entry.View is { } view)
            {
                UpdateView(entry, view);
            }
            if (entry.Editor is { } editor)
            {
                ApplyEditorLabels(entry, editor);
            }
        }
        UpdateMeter();
    }

    private void UpdateSettings()
    {
        var labels = Labels;
        var name = AssistantName;
        if (_savedSetting is not null)
        {
            _savedSetting.IsChecked = IsSavedMemoriesEnabled;
            _savedSetting.Explanation = IsSavedMemoriesEnabled ? labels.SavedMemoriesOn(name) : labels.SavedMemoriesOff(name);
        }
        if (_historySetting is not null)
        {
            _historySetting.IsChecked = IsChatHistoryEnabled;
            _historySetting.Explanation = IsChatHistoryEnabled ? labels.ChatHistoryOn(name) : labels.ChatHistoryOff(name);
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(PausedPseudoClass, !IsSavedMemoriesEnabled);
        PseudoClasses.Set(NoMeterPseudoClass, Capacity <= 0);
        PseudoClasses.Set(NoAddPseudoClass, !IsAddEnabled);
    }

    #endregion

    #region Lifetime

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        if (_lifetime.IsCancellationRequested)
        {
            _lifetime.Dispose();
            _lifetime = new CancellationTokenSource();
        }
        ObserveSources();
        SyncEntries();
        RefreshList(animate: false);
        UpdatePill(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        // Pending saves, sorts and holds end silently: the hooks get a cancelled token.
        _lifetime.Cancel();
        ObserveSources();
        UnwatchAll();
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.WidthChanged)
        {
            UpdateCompact(e.NewSize.Width);
        }
    }

    /// <summary>The narrow layout applies while the control (the card's container) is narrower than the compact width.</summary>
    private void UpdateCompact(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var compact = width < CompactWidth - 0.01;
        if (compact == _isCompact)
        {
            return;
        }
        _isCompact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
    }

    #endregion

    #region Input source, touch and focus

    private void OnAnyKeyDown(object? sender, KeyEventArgs e) => _keyboardMode = true;

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _keyboardMode = false;
        SetTouch(e.Pointer.Type == PointerType.Touch);
    }

    private void OnAnyPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_touch && e.Pointer.Type == PointerType.Mouse)
        {
            SetTouch(false);
        }
    }

    /// <summary>
    /// Touch has no hover: once a touch is seen in the card, every row shows its source line and buttons at rest, until a
    /// mouse moves over the card again (the rule for Arc's "always visible on touch").
    /// </summary>
    private void SetTouch(bool touch)
    {
        if (touch == _touch)
        {
            return;
        }
        _touch = touch;
        foreach (var entry in _entries.Values)
        {
            if (entry.View is { } view)
            {
                view.IsRevealed = touch;
            }
        }
    }

    /// <summary>How a focus move shows: with the keyboard ring after a key, without it after a pointer.</summary>
    private NavigationMethod FocusMethod => _keyboardMode ? NavigationMethod.Tab : NavigationMethod.Pointer;

    private Visual? FocusedVisual => TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;

    /// <summary>Whether the focus is on <paramref name="root"/> or inside it.</summary>
    private bool IsFocusWithin(Visual? root)
    {
        if (root is null || FocusedVisual is not { } focused)
        {
            return false;
        }
        return ReferenceEquals(root, focused) || root.IsVisualAncestorOf(focused);
    }

    private static bool TryFocus(Control? target, NavigationMethod method) =>
        target is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true, Focusable: true } && target.IsAttachedToVisualTree() &&
        target.Focus(method);

    /// <summary>
    /// Focuses the control <paramref name="target"/> returns, now if it can take the focus, else once the layout has
    /// run (a row that has just been put on screen has no template yet).
    /// </summary>
    private void FocusSoon(Func<Control?> target, NavigationMethod method)
    {
        if (TryFocus(target(), method))
        {
            return;
        }
        Dispatcher.UIThread.Post(() => TryFocus(target(), method), DispatcherPriority.Loaded);
    }

    /// <summary>The selected filter pill, or the search input while the search covers the pills.</summary>
    private void FocusFilters(NavigationMethod method)
    {
        if (IsSearchOpen && _search is not null)
        {
            _search.FocusInput(method);
            return;
        }
        _filters?.FocusSelected();
    }

    #endregion

    #region Meter, edge fades and announcements

    /// <summary>Characters in use: every memory except the ones forgotten (in their undo window or leaving).</summary>
    internal int UsedCharacters
    {
        get
        {
            var used = 0;
            foreach (var entry in _ordered)
            {
                if (entry.Mode is EntryMode.Reading or EntryMode.Editing)
                {
                    used += entry.Memory.Text?.Length ?? 0;
                }
            }
            return used;
        }
    }

    private void UpdateMeter()
    {
        PseudoClasses.Set(NoMeterPseudoClass, Capacity <= 0);
        if (_meter is null)
        {
            return;
        }
        _meter.Capacity = Math.Max(0, Capacity);
        _meter.WarningRatio = WarningRatio;
        _meter.Used = UsedCharacters;
    }

    private void OnListEdgesChanged(object? sender, ScrollAreaEdgesChangedEventArgs e) => UpdateListFades();

    private void OnListSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateListFades();

    /// <summary>
    /// The list fades out 20px at the top while scrolled down and 36px at the bottom while more follows, switched on
    /// and off without a transition (Arc's data attributes). A new immutable mask is published only when the state or
    /// the list's height changes, so scrolling allocates nothing.
    /// </summary>
    private void UpdateListFades()
    {
        if (_list is null || _listHost is null)
        {
            return;
        }
        var edges = _list.Edges;
        var height = _list.Bounds.Height;
        var state = (edges.Top, edges.Bottom, height);
        if (state == _fadeState)
        {
            return;
        }
        _fadeState = state;
        if ((!edges.Top && !edges.Bottom) || height <= 0)
        {
            _listHost.OpacityMask = null;
            return;
        }
        if (_fadeTemplate is null)
        {
            _fadeTemplate = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative)
            };
            for (var i = 0; i < 4; i++)
            {
                _fadeTemplate.GradientStops.Add(new GradientStop(Colors.Black, 0));
            }
        }
        var stops = _fadeTemplate.GradientStops;
        stops[0].Color = edges.Top ? Colors.Transparent : Colors.Black;
        stops[0].Offset = 0;
        stops[1].Offset = edges.Top ? Math.Clamp(ListFadeTop / height, 0, 0.5) : 0;
        stops[2].Offset = edges.Bottom ? Math.Clamp(1 - ListFadeBottom / height, 0.5, 1) : 1;
        stops[3].Color = edges.Bottom ? Colors.Transparent : Colors.Black;
        stops[3].Offset = 1;
        _listHost.OpacityMask = new ImmutableLinearGradientBrush(_fadeTemplate);
    }

    /// <summary>Says <paramref name="text"/> through the polite live region.</summary>
    private void Announce(string? text) => _announcer?.Announce(text);

    private static string Count(int value) => value.ToString(CultureInfo.CurrentCulture);

    #endregion
}
