using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Every tool call of one assistant turn in one quiet card (Arc Tool call group, design reference): a header with the
/// tools' overlapping marks, "Used 4 tools" (or "Using 3 tools" with a shimmer while the agent works, or "Waiting for
/// approval") and, on the right, "1 failed" or the total duration; below it, one <see cref="ToolCall"/> row per call.
/// <para>
/// Append calls to <see cref="Items"/> (or an observable <see cref="ItemsSource"/>) as the agent starts them and change
/// them in place: a new row grows in from zero height, a new tool's mark joins the stack on a spring. Opening the header
/// staggers the rows in. While the agent works the group stays open; 450ms after every call finished it folds itself away
/// (<see cref="AutoCollapse"/>) unless the person opened or closed the group or a row. A call that needs a decision (a
/// failure, an approval) opens a folded group. An empty group takes no space; its first call grows it in. With neither
/// <see cref="Items"/> nor <see cref="ItemsSource"/> it shows four finished sample calls.
/// </para>
/// <para>
/// Allow, Deny and Retry bubble up from the rows as <see cref="ApproveRequested"/>, <see cref="DenyRequested"/> and
/// <see cref="RetryRequested"/>; see <see cref="ToolCall"/> for what happens when nothing handles them.
/// </para>
/// </summary>
[TemplatePart(HeaderPart, typeof(ToolCallHeadButton))]
[TemplatePart(SlotPart, typeof(ToolCallCollapsible))]
[TemplatePart(ListPart, typeof(ToolCallCollapsible))]
[TemplatePart(RowsPart, typeof(StackPanel))]
[PseudoClasses(ExpandedPseudoClass, ActivePseudoClass, WaitingPseudoClass, FailedPseudoClass, EmptyPseudoClass, HeadHoverPseudoClass)]
public class ToolCallGroup : TemplatedControl
{
    internal const string HeaderPart = "PART_Header";
    internal const string SlotPart = "PART_Slot";
    internal const string ListPart = "PART_List";
    internal const string RowsPart = "PART_Rows";

    internal const string ExpandedPseudoClass = ":expanded";
    internal const string ActivePseudoClass = ":active";
    internal const string WaitingPseudoClass = ":waiting";
    internal const string FailedPseudoClass = ":failed";
    internal const string EmptyPseudoClass = ":empty";
    internal const string HeadHoverPseudoClass = ":head-hover";

    /// <summary>The rows enter 20ms apart when the group opens.</summary>
    internal static readonly TimeSpan StaggerStep = TimeSpan.FromMilliseconds(20);

    /// <summary>Each entering row settles 6px down onto its place.</summary>
    internal const double SettleOffset = 6;

    /// <summary>The settle's spring (measured ω ≈ 19.3, ζ ≈ 0.96).</summary>
    internal static readonly SpringSpec SettleSpring = SpringSpec.FromVisual(0.27, 0.04);

    /// <summary>The entering row's fade.</summary>
    internal static readonly TimeSpan EntranceFade = ArcMotionPresets.Standard;

    /// <summary>An observable list of calls that wins over <see cref="Items"/>.</summary>
    public static readonly StyledProperty<IEnumerable<ToolCallItem>?> ItemsSourceProperty =
        AvaloniaProperty.Register<ToolCallGroup, IEnumerable<ToolCallItem>?>(nameof(ItemsSource));

    /// <summary>Whether the agent is still working in this turn, so the header keeps saying "Using" between calls; null
    /// (default) means "while any call runs".</summary>
    public static readonly StyledProperty<bool?> IsActiveProperty =
        AvaloniaProperty.Register<ToolCallGroup, bool?>(nameof(IsActive));

    /// <summary>Whether the list is open (two-way). Until it is set, the group opens while any call runs, waits or failed,
    /// and a finished transcript starts folded.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ToolCallGroup, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Folds the list <see cref="AutoCollapseDelay"/> after every call finished, so the answer reads right under
    /// "Used 4 tools"; skipped once the person opened or closed the group or any row.</summary>
    public static readonly StyledProperty<bool> AutoCollapseProperty =
        AvaloniaProperty.Register<ToolCallGroup, bool>(nameof(AutoCollapse), true);

    /// <summary>The wait before the group folds itself (450ms).</summary>
    public static readonly StyledProperty<TimeSpan> AutoCollapseDelayProperty =
        AvaloniaProperty.Register<ToolCallGroup, TimeSpan>(nameof(AutoCollapseDelay), TimeSpan.FromMilliseconds(450));

    /// <summary>Every word the group and its rows show or say.</summary>
    public static readonly StyledProperty<ToolCallLabels> LabelsProperty =
        AvaloniaProperty.Register<ToolCallGroup, ToolCallLabels>(nameof(Labels), ToolCallLabels.Default);

    /// <summary>A custom result view for every successful call (see <see cref="ToolCall.OutputTemplate"/>).</summary>
    public static readonly StyledProperty<IDataTemplate?> OutputTemplateProperty =
        AvaloniaProperty.Register<ToolCallGroup, IDataTemplate?>(nameof(OutputTemplate));

    /// <summary>The sum of the successful calls' durations (shown once nothing runs or waits).</summary>
    public static readonly DirectProperty<ToolCallGroup, TimeSpan> TotalDurationProperty =
        AvaloniaProperty.RegisterDirect<ToolCallGroup, TimeSpan>(nameof(TotalDuration), o => o.TotalDuration);

    /// <summary>The number of failed calls.</summary>
    public static readonly DirectProperty<ToolCallGroup, int> FailedCountProperty =
        AvaloniaProperty.RegisterDirect<ToolCallGroup, int>(nameof(FailedCount), o => o.FailedCount);

    /// <summary>The header as read: the label and the meta ("Used 3 tools 1 failed").</summary>
    public static readonly DirectProperty<ToolCallGroup, string> HeaderTextProperty =
        AvaloniaProperty.RegisterDirect<ToolCallGroup, string>(nameof(HeaderText), o => o.HeaderText);

    /// <summary>Raised when the list opens or closes, by the person, the app or the group itself (see
    /// <see cref="ToolCallExpandedChangedEventArgs.IsAutomatic"/>).</summary>
    public static readonly RoutedEvent<ToolCallExpandedChangedEventArgs> ExpandedChangedEvent =
        RoutedEvent.Register<ToolCallGroup, ToolCallExpandedChangedEventArgs>(nameof(ExpandedChanged), RoutingStrategies.Direct);

    // Theme colors the header meta is mixed from.
    internal static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<ToolCallGroup, IBrush?>(nameof(DangerBrush));

    internal static readonly StyledProperty<IBrush?> SecondaryBrushProperty =
        AvaloniaProperty.Register<ToolCallGroup, IBrush?>(nameof(SecondaryBrush));

    internal static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<ToolCallGroup, IBrush?>(nameof(MutedBrush));

    private sealed class Row
    {
        public required ToolCallItem Item { get; set; }
        public required ToolCall Call { get; init; }
        public required ToolCallCollapsible Box { get; init; }
        public TranslateTransform? Offset { get; set; }
        public MotionValue<double>? Y { get; set; }
        public MotionValue<double>? Fade { get; set; }
    }

    private readonly Dictionary<ToolCallItem, Row> _rows = new();
    private readonly Dictionary<ToolCallItem, ToolCallStatus> _statuses = new();
    private readonly List<ToolCallItem> _items = new();
    private IReadOnlyList<ToolCallItem>? _sample;
    private INotifyCollectionChanged? _observedList;
    private ToolCallHeadButton? _header;
    private ToolCallCollapsible? _slot;
    private ToolCallCollapsible? _list;
    private StackPanel? _rowsPanel;
    private ToolCallMarkStack? _stack;
    private ToolCallText? _label;
    private ToolCallText? _meta;
    private ITimer? _collapseTimer;
    private TimeSpan _totalDuration;
    private int _failedCount;
    private string _headerText = string.Empty;
    private bool _touched;
    private bool _appSetExpanded;
    private bool _settingExpanded;
    private bool _automatic;
    private bool _wasSettled;
    private bool _wasEmpty = true;
    private bool _attached;

    static ToolCallGroup()
    {
        FocusableProperty.OverrideDefaultValue<ToolCallGroup>(false);
    }

    /// <summary>Creates an empty group (showing the sample calls until it gets calls).</summary>
    public ToolCallGroup()
    {
        Items.CollectionChanged += OnItemsCollectionChanged;
        PseudoClasses.Set(EmptyPseudoClass, true);
    }

    /// <summary>The calls in order. Add, remove and change them as the agent works; <see cref="ItemsSource"/> wins when set.</summary>
    [Content]
    public AvaloniaList<ToolCallItem> Items { get; } = new();

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable<ToolCallItem>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="IsActiveProperty"/>
    public bool? IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="AutoCollapseProperty"/>
    public bool AutoCollapse { get => GetValue(AutoCollapseProperty); set => SetValue(AutoCollapseProperty, value); }

    /// <inheritdoc cref="AutoCollapseDelayProperty"/>
    public TimeSpan AutoCollapseDelay { get => GetValue(AutoCollapseDelayProperty); set => SetValue(AutoCollapseDelayProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ToolCallLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="OutputTemplateProperty"/>
    public IDataTemplate? OutputTemplate { get => GetValue(OutputTemplateProperty); set => SetValue(OutputTemplateProperty, value); }

    /// <inheritdoc cref="TotalDurationProperty"/>
    public TimeSpan TotalDuration
    {
        get => _totalDuration;
        private set => SetAndRaise(TotalDurationProperty, ref _totalDuration, value);
    }

    /// <inheritdoc cref="FailedCountProperty"/>
    public int FailedCount
    {
        get => _failedCount;
        private set => SetAndRaise(FailedCountProperty, ref _failedCount, value);
    }

    /// <inheritdoc cref="HeaderTextProperty"/>
    public string HeaderText
    {
        get => _headerText;
        private set => SetAndRaise(HeaderTextProperty, ref _headerText, value);
    }

    /// <summary>Raised when a row's Allow is pressed (it bubbles from the row).</summary>
    public event EventHandler<ToolCallActionEventArgs>? ApproveRequested
    {
        add => AddHandler(ToolCall.ApproveRequestedEvent, value);
        remove => RemoveHandler(ToolCall.ApproveRequestedEvent, value);
    }

    /// <summary>Raised when a row's Deny is pressed (it bubbles from the row).</summary>
    public event EventHandler<ToolCallActionEventArgs>? DenyRequested
    {
        add => AddHandler(ToolCall.DenyRequestedEvent, value);
        remove => RemoveHandler(ToolCall.DenyRequestedEvent, value);
    }

    /// <summary>Raised when a row's Retry is pressed (it bubbles from the row).</summary>
    public event EventHandler<ToolCallActionEventArgs>? RetryRequested
    {
        add => AddHandler(ToolCall.RetryRequestedEvent, value);
        remove => RemoveHandler(ToolCall.RetryRequestedEvent, value);
    }

    /// <inheritdoc cref="ExpandedChangedEvent"/>
    public event EventHandler<ToolCallExpandedChangedEventArgs>? ExpandedChanged
    {
        add => AddHandler(ExpandedChangedEvent, value);
        remove => RemoveHandler(ExpandedChangedEvent, value);
    }

    internal IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }

    internal IBrush? SecondaryBrush { get => GetValue(SecondaryBrushProperty); set => SetValue(SecondaryBrushProperty, value); }

    internal IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    /// <summary>The clock of the auto collapse and of the rows' sample simulation (tests replace it).</summary>
    internal TimeProvider Time { get; set; } = TimeProvider.System;

    /// <summary>The calls shown (the sample when the group has none).</summary>
    internal IReadOnlyList<ToolCallItem> Shown => _items;

    /// <summary>The rows in order.</summary>
    internal IEnumerable<ToolCall> RowControls => _items.Select(i => _rows.TryGetValue(i, out var row) ? row.Call : null).OfType<ToolCall>();

    /// <summary>The row of <paramref name="item"/>.</summary>
    internal ToolCall? RowOf(ToolCallItem item) => _rows.TryGetValue(item, out var row) ? row.Call : null;

    /// <summary>The row's grow-in box of <paramref name="item"/>.</summary>
    internal ToolCallCollapsible? RowBoxOf(ToolCallItem item) => _rows.TryGetValue(item, out var row) ? row.Box : null;

    /// <summary>The header button, the list box and the mark stack.</summary>
    internal ToolCallHeadButton? Header => _header;

    internal ToolCallCollapsible? List => _list;

    internal ToolCallCollapsible? Slot => _slot;

    internal ToolCallMarkStack? Stack => _stack;

    internal ToolCallText? Label => _label;

    internal ToolCallText? MetaLabel => _meta;

    /// <summary>Whether the person has opened or closed the group or a row in this run.</summary>
    internal bool IsTouched => _touched;

    /// <summary>Whether the auto collapse is waiting.</summary>
    internal bool IsCollapsePending => _collapseTimer is not null;

    /// <summary>Opens or closes the list as the person would.</summary>
    internal void ToggleByUser()
    {
        _touched = true;
        CancelCollapse();
        SetExpanded(!IsExpanded, automatic: false);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_header is not null)
        {
            _header.Click -= OnHeaderClick;
            _header.PropertyChanged -= OnHeaderPropertyChanged;
        }
        _rowsPanel?.Children.Clear();
        var scope = e.NameScope;
        _header = scope.Find<ToolCallHeadButton>(HeaderPart);
        _slot = scope.Find<ToolCallCollapsible>(SlotPart);
        _list = scope.Find<ToolCallCollapsible>(ListPart);
        _rowsPanel = scope.Find<StackPanel>(RowsPart);
        _stack = scope.Find<ToolCallMarkStack>("PART_Stack");
        _label = scope.Find<ToolCallText>("PART_Label");
        _meta = scope.Find<ToolCallText>("PART_Meta");
        if (_header is not null)
        {
            _header.Click += OnHeaderClick;
            _header.PropertyChanged += OnHeaderPropertyChanged;
            _header.IsExpanded = IsExpanded;
        }
        if (_slot is not null)
        {
            _slot.FadeInEasing = ToolCallCollapsible.EaseOut;
            _slot.AnimatesClose = false;
        }
        if (_list is not null)
        {
            _list.CrossClipOverflow = 0;
        }
        if (_rowsPanel is not null)
        {
            foreach (var item in _items)
            {
                if (_rows.TryGetValue(item, out var row))
                {
                    _rowsPanel.Children.Add(row.Box);
                }
            }
        }
        if (_slot is not null && _items.Count > 0)
        {
            _slot.OpenInstantly();
        }
        if (_list is not null && IsExpanded)
        {
            _list.OpenInstantly();
        }
        _stack?.SetMarks(_items, animate: false);
        RefreshHeader();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        ObserveList(ItemsSource as INotifyCollectionChanged);
        Sync(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        ObserveList(null);
        foreach (var item in _statuses.Keys.ToList())
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
        _statuses.Clear();
        CancelCollapse();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            if (_attached)
            {
                ObserveList(change.GetNewValue<IEnumerable<ToolCallItem>?>() as INotifyCollectionChanged);
                Sync(animate: false);
            }
        }
        else if (change.Property == IsExpandedProperty)
        {
            var open = change.GetNewValue<bool>();
            if (!_settingExpanded)
            {
                _appSetExpanded = true;
            }
            PseudoClasses.Set(ExpandedPseudoClass, open);
            if (_header is not null)
            {
                _header.IsExpanded = open;
            }
            if (!open)
            {
                CancelCollapse();
            }
            OpenList(open);
            RaiseEvent(new ToolCallExpandedChangedEventArgs(ExpandedChangedEvent, null, open, _automatic));
        }
        else if (change.Property == IsActiveProperty)
        {
            RefreshHeader();
            CheckSettled();
        }
        else if (change.Property == LabelsProperty || change.Property == OutputTemplateProperty)
        {
            foreach (var row in _rows.Values)
            {
                Configure(row.Call);
            }
            RefreshHeader();
        }
        else if (change.Property == DangerBrushProperty || change.Property == SecondaryBrushProperty ||
                 change.Property == MutedBrushProperty)
        {
            RefreshHeader();
        }
    }

    private void ObserveList(INotifyCollectionChanged? list)
    {
        if (ReferenceEquals(list, _observedList))
        {
            return;
        }
        if (_observedList is not null)
        {
            _observedList.CollectionChanged -= OnSourceChanged;
        }
        _observedList = list;
        if (list is not null)
        {
            list.CollectionChanged += OnSourceChanged;
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (ItemsSource is null && _attached)
        {
            Sync(animate: true);
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => Sync(animate: true);

    /// <summary>The calls to show: the source, the items, or the finished sample.</summary>
    private IEnumerable<ToolCallItem> Source()
    {
        if (ItemsSource is { } source)
        {
            return source;
        }
        if (Items.Count > 0)
        {
            return Items;
        }
        return _sample ??= ToolCallSamples.SettledCalls();
    }

    private bool CanAnimate => IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;

    /// <summary>Brings the rows in line with the calls; with <paramref name="animate"/>, a call appended to an open,
    /// non-empty group grows in.</summary>
    private void Sync(bool animate)
    {
        var next = Source().ToList();
        var wasEmpty = _items.Count == 0;
        // Rows and item observation for calls that left. A row whose id comes back on a new item object is kept (with its
        // open state), as Arc keys rows by id.
        var byId = new Dictionary<string, Row>(StringComparer.Ordinal);
        foreach (var item in _items.Except(next).ToList())
        {
            if (_rows.Remove(item, out var row))
            {
                if (!string.IsNullOrEmpty(item.Id) && next.Exists(n => n.Id == item.Id && !_rows.ContainsKey(n)))
                {
                    byId[item.Id] = row;
                }
                else
                {
                    _rowsPanel?.Children.Remove(row.Box);
                    row.Call.UserToggled -= OnRowToggled;
                }
            }
            if (_statuses.Remove(item))
            {
                item.PropertyChanged -= OnItemPropertyChanged;
            }
        }
        var becameEmpty = next.Count == 0 && !wasEmpty;
        var becameFilled = next.Count > 0 && wasEmpty;
        if (becameEmpty || (becameFilled && _wasEmpty))
        {
            // A new run: forget what the person did and how the group was opened.
            _touched = false;
            _wasSettled = false;
            CancelCollapse();
        }
        var grow = animate && !wasEmpty && CanAnimate && IsExpanded && _list?.IsMounted == true;
        _items.Clear();
        _items.AddRange(next);
        for (var i = 0; i < next.Count; i++)
        {
            var item = next[i];
            if (!_statuses.ContainsKey(item))
            {
                _statuses[item] = item.Status;
                if (_attached)
                {
                    item.PropertyChanged += OnItemPropertyChanged;
                }
            }
            if (!_rows.TryGetValue(item, out var row) && byId.Remove(item.Id, out var kept))
            {
                row = kept;
                row.Item = item;
                row.Call.Item = item;
                _rows[item] = row;
            }
            if (row is null)
            {
                row = CreateRow(item);
                _rows[item] = row;
                if (grow)
                {
                    row.Box.IsExpanded = true;
                }
                else
                {
                    row.Box.OpenInstantly();
                }
            }
            if (_rowsPanel is not null)
            {
                var at = _rowsPanel.Children.IndexOf(row.Box);
                if (at != i)
                {
                    if (at >= 0)
                    {
                        _rowsPanel.Children.RemoveAt(at);
                    }
                    _rowsPanel.Children.Insert(Math.Min(i, _rowsPanel.Children.Count), row.Box);
                }
            }
        }
        foreach (var orphan in byId.Values)
        {
            _rowsPanel?.Children.Remove(orphan.Box);
            orphan.Call.UserToggled -= OnRowToggled;
        }
        PseudoClasses.Set(EmptyPseudoClass, next.Count == 0);
        if (becameFilled)
        {
            if (!_appSetExpanded)
            {
                SetExpanded(ToolCallFormat.OpensByDefault(next, IsActive), automatic: true);
            }
            // The list is part of the group growing in: no separate motion.
            if (IsExpanded)
            {
                _list?.OpenInstantly();
            }
            if (_slot is not null)
            {
                if (animate && CanAnimate)
                {
                    _slot.IsExpanded = true;
                }
                else
                {
                    _slot.OpenInstantly();
                }
            }
        }
        else if (becameEmpty)
        {
            _slot?.SetCurrentValue(ToolCallCollapsible.IsExpandedProperty, false);
            if (!_appSetExpanded)
            {
                SetExpanded(false, automatic: true);
            }
        }
        _wasEmpty = next.Count == 0;
        _stack?.SetMarks(next, animate && !becameFilled);
        RefreshHeader();
        CheckDecisions();
        CheckSettled();
    }

    private Row CreateRow(ToolCallItem item)
    {
        var call = new ToolCall { Item = item, IsFlush = true };
        Configure(call);
        call.UserToggled += OnRowToggled;
        var box = new ToolCallCollapsible
        {
            Content = call,
            FadeInEasing = ToolCallCollapsible.EaseOut,
            FadeInDelay = TimeSpan.FromMilliseconds(60),
            AnimatesClose = false
        };
        return new Row { Item = item, Call = call, Box = box };
    }

    private void Configure(ToolCall call)
    {
        call.Labels = Labels;
        call.OutputTemplate = OutputTemplate;
        call.Time = Time;
    }

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not ToolCallItem item)
        {
            return;
        }
        if (e.Property == ToolCallItem.ToolNameProperty || e.Property == ToolCallItem.ToolMarkProperty ||
            e.Property == ToolCallItem.ToolMarkBrushProperty)
        {
            _stack?.SetMarks(_items, animate: true);
            return;
        }
        if (e.Property != ToolCallItem.StatusProperty && e.Property != ToolCallItem.DurationProperty)
        {
            return;
        }
        RefreshHeader();
        CheckDecisions();
        CheckSettled();
    }

    /// <summary>A call that newly needs a decision opens a folded group.</summary>
    private void CheckDecisions()
    {
        var opens = false;
        foreach (var item in _items)
        {
            var was = _statuses.TryGetValue(item, out var old) ? old : item.Status;
            if (ToolCallFormat.NeedsDecision(item.Status) && (!ToolCallFormat.NeedsDecision(was) || was != item.Status))
            {
                opens = true;
            }
            _statuses[item] = item.Status;
        }
        if (opens && !IsExpanded && _items.Count > 0)
        {
            SetExpanded(true, automatic: true);
        }
    }

    /// <summary>Starts the auto collapse when every call has just finished; anything that runs again cancels it.</summary>
    private void CheckSettled()
    {
        var settled = ToolCallFormat.IsSettled(_items, IsActive);
        if (!settled)
        {
            _wasSettled = false;
            CancelCollapse();
            return;
        }
        if (_wasSettled)
        {
            return;
        }
        _wasSettled = true;
        if (!AutoCollapse || _touched || !IsExpanded || !_attached)
        {
            return;
        }
        CancelCollapse();
        _collapseTimer = ToolCallTimer.After(Time, AutoCollapseDelay, () =>
        {
            _collapseTimer = null;
            if (!_touched && IsExpanded && ToolCallFormat.IsSettled(_items, IsActive))
            {
                SetExpanded(false, automatic: true);
            }
        });
    }

    private void CancelCollapse()
    {
        _collapseTimer?.Dispose();
        _collapseTimer = null;
    }

    private void SetExpanded(bool open, bool automatic)
    {
        if (IsExpanded == open)
        {
            return;
        }
        _settingExpanded = true;
        _automatic = automatic;
        SetCurrentValue(IsExpandedProperty, open);
        _automatic = false;
        _settingExpanded = false;
    }

    /// <summary>Opens or closes the list; opening a loaded group staggers its rows in (20ms apart, a 6px settle).</summary>
    private void OpenList(bool open)
    {
        if (_list is null)
        {
            return;
        }
        var stagger = open && CanAnimate && !_list.IsMounted && _slot?.IsMounted == true && _items.Count > 0;
        _list.IsExpanded = open;
        if (!stagger)
        {
            return;
        }
        for (var i = 0; i < _items.Count; i++)
        {
            if (_rows.TryGetValue(_items[i], out var row))
            {
                Enter(row, TimeSpan.FromTicks(StaggerStep.Ticks * i));
            }
        }
    }

    private void Enter(Row row, TimeSpan delay)
    {
        var box = row.Box;
        row.Offset ??= new TranslateTransform();
        box.RenderTransform = row.Offset;
        if (row.Y is null)
        {
            row.Y = new MotionValue<double>(box, 0, MotionVectors.Double);
            var offset = row.Offset;
            row.Y.Changed += y => offset.Y = Math.Abs(y) < 0.13 ? 0 : y;
        }
        if (row.Fade is null)
        {
            row.Fade = new MotionValue<double>(box, 1, MotionVectors.Double);
            row.Fade.Changed += o => box.Opacity = Math.Clamp(o, 0, 1);
        }
        row.Y.Jump(-SettleOffset);
        row.Offset.Y = -SettleOffset;
        row.Fade.Jump(0);
        box.Opacity = 0;
        _ = row.Y.AnimateTo(0, MotionSpec.Spring(SettleSpring, delay));
        _ = row.Fade.AnimateTo(1, MotionSpec.Tween(EntranceFade, ToolCallCollapsible.EaseOut, delay));
    }

    private void RefreshHeader()
    {
        var labels = Labels;
        var header = ToolCallFormat.Header(_items, IsActive, labels);
        if (_label is not null)
        {
            _label.Text = header.Label;
            _label.IsShimmering = header.Shimmers;
        }
        if (_meta is not null)
        {
            // The meta keeps its last brush while it fades out; a new text takes the brush of its kind.
            _meta.Foreground = header.MetaIsFailure ? FailureBrush() : MutedBrush;
            _meta.Text = header.Meta ?? string.Empty;
        }
        TotalDuration = ToolCallFormat.TotalDuration(_items);
        FailedCount = ToolCallFormat.FailedCount(_items);
        HeaderText = header.Meta is null ? header.Label : header.Label + " " + header.Meta;
        if (_header is not null)
        {
            AutomationProperties.SetName(_header, HeaderText);
        }
        PseudoClasses.Set(ActivePseudoClass, ToolCallFormat.IsActive(_items, IsActive));
        PseudoClasses.Set(WaitingPseudoClass, _items.Any(i => i.Status == ToolCallStatus.Approval));
        PseudoClasses.Set(FailedPseudoClass, FailedCount > 0);
    }

    private IBrush? _failure;
    private IBrush? _failureDanger;
    private IBrush? _failureSecondary;

    /// <summary>"1 failed": Danger mixed 85% into TextSecondary.</summary>
    private IBrush? FailureBrush()
    {
        if (DangerBrush is not ISolidColorBrush danger || SecondaryBrush is not ISolidColorBrush secondary)
        {
            return DangerBrush;
        }
        if (_failure is null || !ReferenceEquals(_failureDanger, DangerBrush) || !ReferenceEquals(_failureSecondary, SecondaryBrush))
        {
            _failureDanger = DangerBrush;
            _failureSecondary = SecondaryBrush;
            _failure = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Oklch.MixOklab(secondary.Color, danger.Color, 0.85));
        }
        return _failure;
    }

    private void OnRowToggled(object? sender, EventArgs e)
    {
        _touched = true;
        CancelCollapse();
    }

    private void OnHeaderClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ToggleByUser();
    }

    private void OnHeaderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsPointerOverProperty)
        {
            PseudoClasses.Set(HeadHoverPseudoClass, e.GetNewValue<bool>());
        }
    }
}
