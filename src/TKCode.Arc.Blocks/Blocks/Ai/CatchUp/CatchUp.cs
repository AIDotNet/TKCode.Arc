using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A "while you were away" summary: a short written recap of what changed, then the items grouped into highlights,
/// mentions and decisions, each with who, where and when, an unread dot and a button to mark it read. A filter shows one
/// group at a time with its unread count; opening an item raises <see cref="ItemInvoked"/> with its link and marks it
/// read; "Mark all as read" clears everything, and the recap gives way to "You are all caught up."
/// <code>
/// &lt;arc:CatchUp Since="Since Friday, 6:10 PM" Summary="{Binding Recap}" Items="{Binding Changes}" /&gt;
/// </code>
/// With no data set it shows its invented sample (<see cref="CatchUpSamples"/>).
/// </summary>
[TemplatePart(ListPart, typeof(Panel))]
[TemplatePart(FilterPart, typeof(SegmentedControl))]
[TemplatePart(MarkAllPart, typeof(Button))]
[PseudoClasses(CaughtUpPseudoClass, CompactPseudoClass)]
public class CatchUp : TemplatedControl
{
    public const string ListPart = "PART_List";
    public const string FilterPart = "PART_Filter";
    public const string MarkAllPart = "PART_MarkAll";

    public const string CaughtUpPseudoClass = ":caught-up";
    public const string CompactPseudoClass = ":compact";

    #region Properties

    /// <summary>When the person left, already formatted ("Since Friday, 6:10 PM"); null leaves it out.</summary>
    public static readonly StyledProperty<string?> SinceProperty =
        AvaloniaProperty.Register<CatchUp, string?>(nameof(Since), CatchUpSamples.Since);

    /// <summary>The written recap of what changed (two or three sentences); null hides it.</summary>
    public static readonly StyledProperty<string?> SummaryProperty =
        AvaloniaProperty.Register<CatchUp, string?>(nameof(Summary), CatchUpSamples.Summary);

    /// <summary>What happened, in any order; the card groups them by <see cref="CatchUpItem.Kind"/> and keeps the order within a group.</summary>
    public static readonly StyledProperty<IReadOnlyList<CatchUpItem>?> ItemsProperty =
        AvaloniaProperty.Register<CatchUp, IReadOnlyList<CatchUpItem>?>(nameof(Items), CatchUpSamples.Items());

    /// <summary>Which group is listed (two-way).</summary>
    public static readonly StyledProperty<CatchUpFilter> FilterProperty =
        AvaloniaProperty.Register<CatchUp, CatchUpFilter>(nameof(Filter), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether opening an item marks it read.</summary>
    public static readonly StyledProperty<bool> MarkReadOnOpenProperty =
        AvaloniaProperty.Register<CatchUp, bool>(nameof(MarkReadOnOpen), true);

    /// <summary>Every word the card shows or reads out, with English defaults.</summary>
    public static readonly StyledProperty<CatchUpLabels> LabelsProperty =
        AvaloniaProperty.Register<CatchUp, CatchUpLabels>(nameof(Labels), new CatchUpLabels());

    /// <summary>How many items are unread.</summary>
    public static readonly DirectProperty<CatchUp, int> UnreadCountProperty =
        AvaloniaProperty.RegisterDirect<CatchUp, int>(nameof(UnreadCount), o => o.UnreadCount);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<CatchUp, double>(nameof(CompactWidth), 440);

    /// <inheritdoc cref="SinceProperty"/>
    public string? Since { get => GetValue(SinceProperty); set => SetValue(SinceProperty, value); }

    /// <inheritdoc cref="SummaryProperty"/>
    public string? Summary { get => GetValue(SummaryProperty); set => SetValue(SummaryProperty, value); }

    /// <inheritdoc cref="ItemsProperty"/>
    public IReadOnlyList<CatchUpItem>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    /// <inheritdoc cref="FilterProperty"/>
    public CatchUpFilter Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <inheritdoc cref="MarkReadOnOpenProperty"/>
    public bool MarkReadOnOpen { get => GetValue(MarkReadOnOpenProperty); set => SetValue(MarkReadOnOpenProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public CatchUpLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="UnreadCountProperty"/>
    public int UnreadCount { get => _unreadCount; private set => SetAndRaise(UnreadCountProperty, ref _unreadCount, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    #endregion

    #region Events

    /// <summary>Raised when an item is opened (clicked, or Enter / Space on it).</summary>
    public static readonly RoutedEvent<CatchUpItemEventArgs> ItemInvokedEvent =
        RoutedEvent.Register<CatchUp, CatchUpItemEventArgs>(nameof(ItemInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when items are marked read or unread, by the person or by code.</summary>
    public static readonly RoutedEvent<CatchUpReadEventArgs> ItemsReadEvent =
        RoutedEvent.Register<CatchUp, CatchUpReadEventArgs>(nameof(ItemsRead), RoutingStrategies.Bubble);

    /// <summary>Raised when an item is opened (clicked, or Enter / Space on it).</summary>
    public event EventHandler<CatchUpItemEventArgs>? ItemInvoked
    {
        add => AddHandler(ItemInvokedEvent, value);
        remove => RemoveHandler(ItemInvokedEvent, value);
    }

    /// <summary>Raised when items are marked read or unread, by the person or by code.</summary>
    public event EventHandler<CatchUpReadEventArgs>? ItemsRead
    {
        add => AddHandler(ItemsReadEvent, value);
        remove => RemoveHandler(ItemsReadEvent, value);
    }

    #endregion

    private readonly HashSet<string> _read = new(StringComparer.Ordinal);
    private readonly List<CatchUpRow> _rows = new();
    private int _unreadCount;
    private Panel? _list;
    private SegmentedControl? _filter;
    private Button? _markAll;
    private TextBlock? _title;
    private TextBlock? _unreadText;
    private ArcContentSwap? _recap;
    private AiAnnouncer? _announcer;
    private bool _syncingFilter;

    static CatchUp()
    {
        FocusableProperty.OverrideDefaultValue<CatchUp>(false);
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<CatchUp>(AutomationLandmarkType.Region);
    }

    public CatchUp()
    {
        ResetReadState();
    }

    internal IReadOnlyList<CatchUpRow> Rows => _rows;

    internal string? Announcement => _announcer?.Announcement;

    #region Public actions

    /// <summary>Whether <paramref name="item"/> is read.</summary>
    /// <param name="item">One of <see cref="Items"/>.</param>
    public bool IsRead(CatchUpItem item) => _read.Contains(item.Id);

    /// <summary>Marks <paramref name="item"/> read or unread.</summary>
    /// <param name="item">One of <see cref="Items"/>.</param>
    /// <param name="isRead">True for read.</param>
    public void SetRead(CatchUpItem item, bool isRead)
    {
        if (isRead ? !_read.Add(item.Id) : !_read.Remove(item.Id))
        {
            return;
        }
        Refresh();
        RaiseEvent(new CatchUpReadEventArgs(ItemsReadEvent, [item], isRead));
    }

    /// <summary>Marks every item read.</summary>
    public void MarkAllRead()
    {
        var changed = (Items ?? []).Where(i => _read.Add(i.Id)).ToArray();
        if (changed.Length == 0)
        {
            return;
        }
        Refresh();
        _announcer?.Announce(Labels.CaughtUp);
        RaiseEvent(new CatchUpReadEventArgs(ItemsReadEvent, changed, true));
    }

    /// <summary>Opens <paramref name="item"/>: raises <see cref="ItemInvoked"/> and, with <see cref="MarkReadOnOpen"/>, marks it read.</summary>
    /// <param name="item">One of <see cref="Items"/>.</param>
    public void Open(CatchUpItem item)
    {
        RaiseEvent(new CatchUpItemEventArgs(ItemInvokedEvent, item));
        if (MarkReadOnOpen)
        {
            SetRead(item, true);
        }
    }

    #endregion

    private void ResetReadState()
    {
        _read.Clear();
        foreach (var item in Items ?? [])
        {
            if (item.IsRead)
            {
                _read.Add(item.Id);
            }
        }
        UnreadCount = (Items ?? []).Count(i => !_read.Contains(i.Id));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_filter is not null)
        {
            _filter.SelectionChanged -= OnFilterChanged;
        }
        if (_markAll is not null)
        {
            _markAll.Click -= OnMarkAllClick;
        }
        _list = e.NameScope.Find<Panel>(ListPart);
        _filter = e.NameScope.Find<SegmentedControl>(FilterPart);
        _markAll = e.NameScope.Find<Button>(MarkAllPart);
        _title = e.NameScope.Find<TextBlock>("PART_Title");
        _unreadText = e.NameScope.Find<TextBlock>("PART_Unread");
        _recap = e.NameScope.Find<ArcContentSwap>("PART_Recap");
        _announcer = e.NameScope.Find<AiAnnouncer>("PART_Announcer");
        if (_filter is not null)
        {
            _filter.SelectionChanged += OnFilterChanged;
        }
        if (_markAll is not null)
        {
            _markAll.Click += OnMarkAllClick;
        }
        BuildFilters();
        BuildList();
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty)
        {
            ResetReadState();
            BuildList();
            Refresh();
        }
        else if (change.Property == FilterProperty)
        {
            SyncFilter();
            BuildList();
            Refresh();
        }
        else if (change.Property == SummaryProperty)
        {
            Refresh();
        }
        else if (change.Property == LabelsProperty)
        {
            BuildFilters();
            BuildList();
            Refresh();
        }
        else if (change.Property == BoundsProperty || change.Property == CompactWidthProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, Bounds.Width > 0 && Bounds.Width <= CompactWidth);
        }
    }

    private static CatchUpKind? KindOf(CatchUpFilter filter) => filter switch
    {
        CatchUpFilter.Highlights => CatchUpKind.Highlight,
        CatchUpFilter.Mentions => CatchUpKind.Mention,
        CatchUpFilter.Decisions => CatchUpKind.Decision,
        _ => null,
    };

    private string GroupName(CatchUpKind kind)
        => Labels.Groups is { } groups && (int)kind < groups.Count ? groups[(int)kind] : kind.ToString();

    private void BuildFilters()
    {
        if (_filter is null)
        {
            return;
        }
        _syncingFilter = true;
        try
        {
            _filter.Items.Clear();
            foreach (var filter in Enum.GetValues<CatchUpFilter>())
            {
                _filter.Items.Add(new SegmentedOption(filter, FilterText(filter)));
            }
            _filter.SelectedValue = Filter;
            AutomationProperties.SetName(_filter, Labels.Title);
        }
        finally
        {
            _syncingFilter = false;
        }
    }

    private string FilterText(CatchUpFilter filter)
    {
        var labels = Labels;
        var kind = KindOf(filter);
        var unread = (Items ?? []).Count(i => (kind is null || i.Kind == kind) && !_read.Contains(i.Id));
        return labels.Filter(kind is { } k ? GroupName(k) : labels.All, unread);
    }

    private void SyncFilter()
    {
        if (_filter is null)
        {
            return;
        }
        _syncingFilter = true;
        try
        {
            _filter.SelectedValue = Filter;
        }
        finally
        {
            _syncingFilter = false;
        }
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncingFilter && _filter?.SelectedValue is CatchUpFilter filter)
        {
            SetCurrentValue(FilterProperty, filter);
        }
    }

    private void OnMarkAllClick(object? sender, RoutedEventArgs e) => MarkAllRead();

    private void BuildList()
    {
        if (_list is null)
        {
            return;
        }
        foreach (var row in _rows)
        {
            row.Open -= OnRowOpen;
            row.Toggle -= OnRowToggle;
            row.KeyDown -= OnRowKeyDown;
        }
        _list.Children.Clear();
        _rows.Clear();
        var only = KindOf(Filter);
        var items = Items ?? [];
        foreach (var kind in Enum.GetValues<CatchUpKind>())
        {
            if (only is { } k && k != kind)
            {
                continue;
            }
            var group = items.Where(i => i.Kind == kind).ToArray();
            if (group.Length == 0)
            {
                continue;
            }
            var heading = new TextBlock { Text = GroupName(kind), Classes = { "catchup-heading" } };
            AutomationProperties.SetHeadingLevel(heading, 3);
            _list.Children.Add(heading);
            foreach (var item in group)
            {
                var row = new CatchUpRow(item);
                row.Open += OnRowOpen;
                row.Toggle += OnRowToggle;
                row.KeyDown += OnRowKeyDown;
                _rows.Add(row);
                _list.Children.Add(row);
            }
        }
        if (_rows.Count == 0)
        {
            _list.Children.Add(new TextBlock { Text = Labels.Empty, Classes = { "catchup-empty" } });
        }
    }

    private void Refresh()
    {
        var labels = Labels;
        UnreadCount = (Items ?? []).Count(i => !_read.Contains(i.Id));
        var caughtUp = UnreadCount == 0;
        PseudoClasses.Set(CaughtUpPseudoClass, caughtUp);
        if (_title is not null)
        {
            _title.Text = labels.Title;
        }
        if (_unreadText is not null)
        {
            _unreadText.Text = caughtUp ? Since : string.IsNullOrEmpty(Since) ? labels.UnreadCount(UnreadCount) : $"{Since} · {labels.UnreadCount(UnreadCount)}";
        }
        if (_markAll is not null)
        {
            _markAll.Content = labels.MarkAllRead;
            _markAll.IsEnabled = !caughtUp;
        }
        if (_recap is not null)
        {
            var text = caughtUp ? labels.CaughtUp : Summary;
            if (_recap.Content is not CatchUpRecap recap || recap.Text != text || recap.IsCaughtUp != caughtUp)
            {
                _recap.Content = string.IsNullOrWhiteSpace(text) ? null : new CatchUpRecap { Text = text, IsCaughtUp = caughtUp };
            }
        }
        foreach (var row in _rows)
        {
            row.IsUnread = !_read.Contains(row.Item.Id);
            row.UpdateNames(labels);
        }
        if (_filter is not null)
        {
            var index = 0;
            foreach (var option in _filter.Items.OfType<SegmentedOption>())
            {
                option.Label = FilterText((CatchUpFilter)index++);
            }
        }
    }

    private void OnRowOpen(object? sender, EventArgs e)
    {
        if (sender is CatchUpRow row)
        {
            Open(row.Item);
        }
    }

    private void OnRowToggle(object? sender, EventArgs e)
    {
        if (sender is CatchUpRow row)
        {
            var read = !_read.Contains(row.Item.Id);
            SetRead(row.Item, read);
        }
    }

    // Up and Down move between the items like a list; Tab still visits each item and its read button.
    private void OnRowKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not CatchUpRow row || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var index = _rows.IndexOf(row);
        var next = e.Key switch
        {
            Key.Down => Math.Min(_rows.Count - 1, index + 1),
            Key.Up => Math.Max(0, index - 1),
            Key.Home => 0,
            Key.End => _rows.Count - 1,
            _ => -1,
        };
        if (next >= 0)
        {
            _rows[next].FocusOpen();
            e.Handled = true;
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CatchUpPeer(this);

    private sealed class CatchUpPeer(CatchUp owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore()
            => base.GetNameCore() is { Length: > 0 } name ? name : ((CatchUp)Owner).Labels.Title;
    }
}
