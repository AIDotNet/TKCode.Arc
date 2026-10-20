using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// A vertical activity feed grouped by day, newest first (Arc's <c>Timeline</c>), for project history, audit logs and
/// deploy streams. Day labels stay pinned while their updates scroll, the connecting line draws itself as rows come into
/// view, rows with a <see cref="TimelineEvent.Detail"/> expand in place, and new updates slide in at the top while the
/// rest glide down. ↑ / ↓ / Home / End move between expandable rows; Enter or Space expands one.
/// </summary>
/// <remarks>
/// Add <see cref="TimelineEvent"/>s with <see cref="ItemsControl.ItemsSource"/> or as inline items, in any order. The
/// list is virtualized: only the rows in view (plus a small buffer) exist, so long histories stay fast. Set
/// <see cref="Layoutable.MaxHeight"/> to scroll inside the timeline (the bottom edge feathers while more waits below);
/// without it the feed grows with the page and reveals as the page scrolls. Advance <see cref="Now"/> about once a
/// minute to keep relative times fresh: only the time labels change.
/// </remarks>
[TemplatePart(ScrollerPart, typeof(ScrollViewer))]
[TemplatePart(ListPart, typeof(Control))]
[TemplatePart(StickyPart, typeof(Control))]
[TemplatePart(LiveRegionPart, typeof(Control))]
[PseudoClasses(ScrollsPseudoClass, NarrowPseudoClass)]
public class Timeline : ItemsControl
{
    public const string ScrollerPart = "PART_Scroller";
    public const string ListPart = "PART_List";
    public const string StickyPart = "PART_Sticky";
    public const string LiveRegionPart = "PART_LiveRegion";
    public const string ScrollsPseudoClass = ":scrolls";
    public const string NarrowPseudoClass = ":narrow";

    #region Properties

    /// <summary>
    /// Reference time for relative labels ("9m") and day groups ("Today"). Pass a ticking clock (about once a minute) to
    /// keep labels fresh; the default (<see cref="DateTimeOffset.MinValue"/>) reads the system clock when labels are built.
    /// </summary>
    public static readonly StyledProperty<DateTimeOffset> NowProperty =
        AvaloniaProperty.Register<Timeline, DateTimeOffset>(nameof(Now));

    /// <summary>Accessible name of the feed (Arc's required <c>label</c>).</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<Timeline, string>(nameof(Label), "Activity");

    /// <summary>Time zone for day groups and clock times. Fixed (UTC) by default, like Arc, so every viewer agrees.</summary>
    public static readonly StyledProperty<TimeZoneInfo> TimeZoneProperty =
        AvaloniaProperty.Register<Timeline, TimeZoneInfo>(nameof(TimeZone), TimeZoneInfo.Utc);

    /// <summary>Culture of day headings and clock times; null uses en-US (Arc's default locale).</summary>
    public static readonly StyledProperty<CultureInfo?> CultureProperty =
        AvaloniaProperty.Register<Timeline, CultureInfo?>(nameof(Culture));

    /// <summary>Scroll back to the top when a new newest update arrives while the feed is scrolled down.</summary>
    public static readonly StyledProperty<bool> IsScrollToNewEnabledProperty =
        AvaloniaProperty.Register<Timeline, bool>(nameof(IsScrollToNewEnabled), true);

    /// <summary>Ids of the expanded rows (Arc's <c>defaultExpanded</c>, two-way).</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> ExpandedIdsProperty =
        AvaloniaProperty.Register<Timeline, IReadOnlyList<string>?>(nameof(ExpandedIds), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Heading level of the day labels (2 to 6; default 3).</summary>
    public static readonly StyledProperty<int> HeadingLevelProperty =
        AvaloniaProperty.Register<Timeline, int>(nameof(HeadingLevel), 3, coerce: (_, value) => Math.Clamp(value, 2, 6));

    /// <summary>
    /// Background of the day labels, which cover the updates scrolling under them (Arc's <c>--timeline-surface</c>,
    /// Surface by default). Set it to the surface the feed sits on.
    /// </summary>
    public static readonly StyledProperty<IBrush?> HeaderBackgroundProperty =
        AvaloniaProperty.Register<Timeline, IBrush?>(nameof(HeaderBackground));

    /// <summary>A row opened or closed by the user (click, keyboard, assistive technology or the methods).</summary>
    public static readonly RoutedEvent<TimelineExpandedChangedEventArgs> ExpandedChangedEvent =
        RoutedEvent.Register<Timeline, TimelineExpandedChangedEventArgs>(nameof(ExpandedChanged), RoutingStrategies.Bubble);

    internal static readonly StyledProperty<double> DayGapProperty =
        AvaloniaProperty.Register<Timeline, double>(nameof(DayGap), 12);

    internal static readonly StyledProperty<double> MarkerSizeProperty =
        AvaloniaProperty.Register<Timeline, double>(nameof(MarkerSize), 28);

    internal static readonly StyledProperty<double> MarkerGlyphSizeProperty =
        AvaloniaProperty.Register<Timeline, double>(nameof(MarkerGlyphSize), 14);

    internal static readonly StyledProperty<double> MarkerGlyphStrokeProperty =
        AvaloniaProperty.Register<Timeline, double>(nameof(MarkerGlyphStroke), 2);

    internal static readonly StyledProperty<double> FeatherHeightProperty =
        AvaloniaProperty.Register<Timeline, double>(nameof(FeatherHeight), 36);

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<Timeline, double>(nameof(NarrowWidth), 420);

    /// <inheritdoc cref="NowProperty"/>
    public DateTimeOffset Now { get => GetValue(NowProperty); set => SetValue(NowProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="TimeZoneProperty"/>
    public TimeZoneInfo TimeZone { get => GetValue(TimeZoneProperty); set => SetValue(TimeZoneProperty, value); }

    /// <inheritdoc cref="CultureProperty"/>
    public CultureInfo? Culture { get => GetValue(CultureProperty); set => SetValue(CultureProperty, value); }

    /// <inheritdoc cref="IsScrollToNewEnabledProperty"/>
    public bool IsScrollToNewEnabled { get => GetValue(IsScrollToNewEnabledProperty); set => SetValue(IsScrollToNewEnabledProperty, value); }

    /// <inheritdoc cref="ExpandedIdsProperty"/>
    public IReadOnlyList<string>? ExpandedIds { get => GetValue(ExpandedIdsProperty); set => SetValue(ExpandedIdsProperty, value); }

    /// <inheritdoc cref="HeadingLevelProperty"/>
    public int HeadingLevel { get => GetValue(HeadingLevelProperty); set => SetValue(HeadingLevelProperty, value); }

    /// <inheritdoc cref="HeaderBackgroundProperty"/>
    public IBrush? HeaderBackground { get => GetValue(HeaderBackgroundProperty); set => SetValue(HeaderBackgroundProperty, value); }

    /// <summary>A row opened or closed by the user (click, keyboard, assistive technology or the methods).</summary>
    public event EventHandler<TimelineExpandedChangedEventArgs>? ExpandedChanged
    {
        add => AddHandler(ExpandedChangedEvent, value);
        remove => RemoveHandler(ExpandedChangedEvent, value);
    }

    internal double DayGap { get => GetValue(DayGapProperty); set => SetValue(DayGapProperty, value); }
    internal double MarkerSize { get => GetValue(MarkerSizeProperty); set => SetValue(MarkerSizeProperty, value); }
    internal double MarkerGlyphSize { get => GetValue(MarkerGlyphSizeProperty); set => SetValue(MarkerGlyphSizeProperty, value); }
    internal double MarkerGlyphStroke { get => GetValue(MarkerGlyphStrokeProperty); set => SetValue(MarkerGlyphStrokeProperty, value); }
    internal double FeatherHeight { get => GetValue(FeatherHeightProperty); set => SetValue(FeatherHeightProperty, value); }
    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    #endregion

    private const double RevealStep = 0.09;
    private const double RevealCap = 0.45;
    private const double RevealThreshold = 0.2;

    private readonly ObservableCollection<TimelineRowModel> _rows = new();
    private readonly Dictionary<string, TimelineRowModel> _byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<DateOnly, TimelineDayModel> _days = new();
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private readonly List<(int Index, Control Container)> _realized = new();
    private readonly TranslateTransform _stickyShift = new();
    private readonly Dictionary<int, IBrush> _featherMasks = new();
    private double _featherMaskHeight = -1;

    private ScrollViewer? _scroller;
    private TimelineList? _list;
    private TimelineDayHeader? _sticky;
    private ActivityLiveRegion? _liveRegion;
    private Panel? _panel;
    private Rect _viewport;
    private Rect _listViewport;
    private bool _hasViewport;
    private bool _baselined;
    private bool _syncingExpanded;
    private object? _source;
    private bool _isNarrow;
    private string? _newestId;
    private double _nextReveal;
    private MotionValue<double>? _feather;
    private bool _featherOn;
    private MotionValue<double>? _scrollTop;
    private string? _announcement;

    static Timeline()
    {
        AutomationProperties.LandmarkTypeProperty.OverrideDefaultValue<Timeline>(AutomationLandmarkType.Region);
        FocusableProperty.OverrideDefaultValue<Timeline>(false);
    }

    /// <summary>Creates an empty timeline.</summary>
    public Timeline()
    {
        ItemsView.CollectionChanged += OnEventsChanged;
        AutomationProperties.SetName(this, Label);
    }

    #region Public API

    /// <summary>Whether the row of <paramref name="id"/> is expanded.</summary>
    public bool IsExpanded(string id) => _expanded.Contains(id);

    /// <summary>Opens the row of <paramref name="id"/> (rows without a detail stay closed).</summary>
    public void Expand(string id) => SetExpanded(id, true, raise: true);

    /// <summary>Closes the row of <paramref name="id"/>.</summary>
    public void Collapse(string id) => SetExpanded(id, false, raise: true);

    /// <summary>Opens or closes the row of <paramref name="id"/>.</summary>
    public void Toggle(string id) => SetExpanded(id, !_expanded.Contains(id), raise: true);

    /// <summary>Scrolls the feed back to its newest update (smoothly under full motion).</summary>
    public void ScrollToTop(bool smooth = true)
    {
        if (_scroller is null || _scroller.Offset.Y < 1)
        {
            return;
        }
        _scrollTop ??= CreateScrollValue();
        if (!smooth || !IsLoaded || !ArcMotion.IsFull(this))
        {
            _scrollTop.Stop();
            _scroller.Offset = _scroller.Offset.WithY(0);
            return;
        }
        _scrollTop.Jump(_scroller.Offset.Y);
        _ = _scrollTop.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Considered, ArcEasings.InOut));
    }

    /// <summary>Brings the event of <paramref name="id"/> into view.</summary>
    public void ScrollIntoView(string id)
    {
        if (_list is not null && _byKey.TryGetValue("ev:" + id, out var model))
        {
            var index = _rows.IndexOf(model);
            if (index >= 0)
            {
                _list.ScrollIntoView(index);
            }
        }
    }

    #endregion

    #region Internals for parts and tests

    internal bool IsNarrow => _isNarrow;

    internal IReadOnlyList<TimelineRowModel> Rows => _rows;

    internal TimelineList? List => _list;

    internal TimelineDayHeader? StickyHeader => _sticky;

    internal ScrollViewer? Scroller => _scroller;

    internal ActivityLiveRegion? LiveRegion => _liveRegion;

    internal double FeatherValue => _feather?.Value ?? 0;

    /// <summary>The realized containers, by index (tests).</summary>
    internal IEnumerable<Control> RealizedContainers => _list?.GetRealizedContainers() ?? Array.Empty<Control>();

    internal DateTimeOffset EffectiveNow => Now == default ? DateTimeOffset.Now : Now;

    private CultureInfo EffectiveCulture => Culture ?? ActivityTime.DefaultCulture;

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_scroller is not null)
        {
            _scroller.ScrollChanged -= OnScrollChanged;
        }
        if (_list is not null)
        {
            _list.Owner = null;
            _list.ItemsSource = null;
            _list.EffectiveViewportChanged -= OnListViewportChanged;
        }
        _scroller = e.NameScope.Find<ScrollViewer>(ScrollerPart);
        _list = e.NameScope.Find<TimelineList>(ListPart);
        _hasViewport = false;
        // Subscribed before the list's first layout: a listener added later is only told about changes, so a page that
        // has already settled (a timeline shown in a scrolling host) would never learn its viewport.
        if (_list is not null)
        {
            _list.EffectiveViewportChanged += OnListViewportChanged;
        }
        _sticky = e.NameScope.Find<TimelineDayHeader>(StickyPart);
        _liveRegion = e.NameScope.Find<ActivityLiveRegion>(LiveRegionPart);
        if (_scroller is not null)
        {
            _scroller.ScrollChanged += OnScrollChanged;
        }
        if (_sticky is not null)
        {
            _sticky.IsOverlay = true;
            _sticky.Owner = this;
            _sticky.RenderTransform = _stickyShift;
            _sticky.Opacity = 0;
            _sticky.IsHitTestVisible = false;
            _sticky.Background = HeaderBackground;
        }
        if (_list is not null)
        {
            _list.Owner = this;
            if (_rows.Count == 0)
            {
                Rebuild();
            }
            _list.ItemsSource = _rows;
        }
        UpdateScrollMode();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NowProperty)
        {
            RefreshLabels(animate: true);
        }
        else if (change.Property == TimeZoneProperty || change.Property == CultureProperty)
        {
            // Days regroup in place: nothing is new, so nothing slides in or is announced.
            Rebuild(rebaseline: true);
            RefreshLabels(animate: false);
        }
        else if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, Label);
        }
        else if (change.Property == ExpandedIdsProperty && !_syncingExpanded)
        {
            SyncExpandedFromProperty();
        }
        else if (change.Property == HeadingLevelProperty)
        {
            RefreshLabels(animate: false);
        }
        else if (change.Property == HeaderBackgroundProperty)
        {
            foreach (var header in RealizedContainers.OfType<TimelineDayHeader>())
            {
                header.Background = HeaderBackground;
            }
            if (_sticky is not null)
            {
                _sticky.Background = HeaderBackground;
            }
        }
        else if (change.Property == MaxHeightProperty)
        {
            UpdateScrollMode();
            InvalidateMeasure();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        LayoutUpdated -= OnLayoutUpdated;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // Updates present when the feed first shows are not fresh; later ones slide in and are announced.
        _baselined = true;
        _newestId = NewestId();
    }

    /// <summary>Without a max height the feed grows with the page (its scroller gets all the height it asks for).</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var scrolls = !double.IsPositiveInfinity(MaxHeight);
        return base.MeasureOverride(scrolls ? availableSize : availableSize.WithHeight(double.PositiveInfinity));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var narrow = finalSize.Width > 0 && finalSize.Width < NarrowWidth;
        if (narrow != _isNarrow)
        {
            _isNarrow = narrow;
            PseudoClasses.Set(NarrowPseudoClass, narrow);
            foreach (var row in RealizedContainers.OfType<TimelineRow>())
            {
                row.Refresh();
            }
        }
        return base.ArrangeOverride(finalSize);
    }

    private void UpdateScrollMode()
    {
        var scrolls = !double.IsPositiveInfinity(MaxHeight);
        PseudoClasses.Set(ScrollsPseudoClass, scrolls);
        if (!scrolls && _scroller is not null)
        {
            _feather?.Jump(0);
            _featherOn = false;
            _scroller.OpacityMask = null;
        }
    }

    #region Rows

    /// <summary>
    /// Added events are fresh (they slide in and are announced); a reset (a new <see cref="ItemsControl.ItemsSource"/>)
    /// shows the new list as it is, like a first render.
    /// </summary>
    private void OnEventsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Rebuild(rebaseline: e.Action == NotifyCollectionChangedAction.Reset);

    /// <summary>
    /// Sorts the events newest first, groups them by day and patches the flattened rows in place: surviving rows keep
    /// their models (and containers), new ones are inserted where they belong, removed ones leave with an exit.
    /// </summary>
    private void Rebuild(bool rebaseline = false)
    {
        // A new source shows as it is, like a first render (its items are not fresh and not announced).
        if (!ReferenceEquals(_source, ItemsSource))
        {
            _source = ItemsSource;
            rebaseline = true;
        }
        var fresh = _baselined && !rebaseline;
        var zone = TimeZone;
        var events = new List<(TimelineEvent Item, int Index)>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var position = 0;
        foreach (var item in ItemsView)
        {
            if (item is TimelineEvent ev && ids.Add(ev.Id))
            {
                events.Add((ev, position));
            }
            position++;
        }
        events.Sort(static (a, b) =>
        {
            var byTime = b.Item.At.CompareTo(a.Item.At);
            return byTime != 0 ? byTime : a.Index.CompareTo(b.Index);
        });

        var animate = fresh && IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        var enterUntil = Stopwatch.GetTimestamp() + TimelineRow.EnterWindow;
        var next = new List<TimelineRowModel>(events.Count + 8);
        var added = new List<TimelineEvent>();
        TimelineDayModel? day = null;
        TimelineEventModel? lastOfDay = null;
        var count = 0;
        foreach (var (item, _) in events)
        {
            var date = ActivityTime.DayOf(item.At, zone);
            if (day is null || day.Day != date)
            {
                if (day is not null)
                {
                    day.Count = count;
                    lastOfDay!.IsLastOfDay = true;
                }
                var dayKey = $"day:{date:yyyy-MM-dd}";
                if (_byKey.TryGetValue(dayKey, out var existingDay) && existingDay is TimelineDayModel known)
                {
                    day = known;
                    day.Leaving = false;
                }
                else
                {
                    day = new TimelineDayModel(date) { Fresh = fresh, EnterUntil = fresh ? enterUntil : 0 };
                }
                day.Day = date;
                day.IsFirst = next.Count == 0;
                day.Newest = item.At;
                next.Add(day);
                count = 0;
            }
            var key = "ev:" + item.Id;
            TimelineEventModel model;
            if (_byKey.TryGetValue(key, out var existing) && existing is TimelineEventModel reused)
            {
                model = reused;
                model.Item = item;
                model.Leaving = false;
            }
            else
            {
                model = new TimelineEventModel(item) { Fresh = fresh, EnterUntil = fresh ? enterUntil : 0 };
                if (fresh)
                {
                    added.Add(item);
                }
            }
            model.Day = date;
            model.IsLastOfDay = false;
            next.Add(model);
            lastOfDay = model;
            count++;
        }
        if (day is not null)
        {
            day.Count = count;
            lastOfDay!.IsLastOfDay = true;
        }

        // Rows that are gone leave in place when their container is showing; others drop at once.
        var nextKeys = new HashSet<TimelineRowModel>(next);
        var leaving = new List<(TimelineRowModel Model, Control Container)>();
        if (animate && _list is not null)
        {
            for (var i = 0; i < _rows.Count; i++)
            {
                var old = _rows[i];
                if (nextKeys.Contains(old) || _list.ContainerFromItem(old) is not { IsVisible: true } container)
                {
                    continue;
                }
                var pos = 0;
                for (var k = i - 1; k >= 0; k--)
                {
                    var at = next.IndexOf(_rows[k]);
                    if (at >= 0)
                    {
                        pos = at + 1;
                        break;
                    }
                }
                next.Insert(pos, old);
                nextKeys.Add(old);
                if (!old.Leaving)
                {
                    old.Leaving = true;
                    leaving.Add((old, container));
                }
            }
        }

        Patch(next);
        _byKey.Clear();
        foreach (var row in _rows)
        {
            _byKey[row.Key] = row;
        }
        Reindex();

        var level = ArcMotion.GetEffectiveLevel(this);
        foreach (var (model, container) in leaving)
        {
            switch (container)
            {
                case TimelineRow row:
                    row.PlayExit(level);
                    break;
                case TimelineDayHeader header:
                    header.PlayExit(level);
                    break;
            }
        }

        RefreshRealized();
        if (added.Count > 0)
        {
            Announce(added);
        }
        var newest = NewestId();
        if (fresh && newest != _newestId)
        {
            _newestId = newest;
            if (IsScrollToNewEnabled)
            {
                ScrollToTop(smooth: true);
            }
        }
        else
        {
            _newestId = newest;
        }
    }

    private string? NewestId()
    {
        TimelineEventModel? newest = null;
        foreach (var row in _rows)
        {
            if (row is TimelineEventModel { Leaving: false } ev && (newest is null || ev.Item.At > newest.Item.At))
            {
                newest = ev;
            }
        }
        return newest?.Item.Id;
    }

    private void Patch(List<TimelineRowModel> desired)
    {
        var keep = new HashSet<TimelineRowModel>(desired);
        for (var i = _rows.Count - 1; i >= 0; i--)
        {
            if (!keep.Contains(_rows[i]))
            {
                _rows.RemoveAt(i);
            }
        }
        for (var i = 0; i < desired.Count; i++)
        {
            if (i < _rows.Count && ReferenceEquals(_rows[i], desired[i]))
            {
                continue;
            }
            var j = -1;
            for (var k = i + 1; k < _rows.Count; k++)
            {
                if (ReferenceEquals(_rows[k], desired[i]))
                {
                    j = k;
                    break;
                }
            }
            if (j >= 0)
            {
                _rows.Move(j, i);
            }
            else
            {
                _rows.Insert(i, desired[i]);
            }
        }
    }

    /// <summary>A leaving row finished its exit: it leaves the list.</summary>
    internal void RemoveLeaving(TimelineRowModel model)
    {
        if (!model.Leaving)
        {
            return;
        }
        var index = _rows.IndexOf(model);
        if (index < 0)
        {
            return;
        }
        _rows.RemoveAt(index);
        _byKey.Remove(model.Key);
        Reindex();
    }

    /// <summary>Records each day label's index and the next label's index (the pinned label reads them on scroll).</summary>
    private void Reindex()
    {
        _days.Clear();
        TimelineDayModel? previous = null;
        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] is TimelineDayModel day)
            {
                day.Index = i;
                day.NextIndex = _rows.Count;
                if (previous is not null)
                {
                    previous.NextIndex = i;
                }
                previous = day;
                _days[day.Day] = day;
            }
        }
    }

    private void Announce(List<TimelineEvent> added)
    {
        var parts = added.Select(static e => string.IsNullOrEmpty(e.Actor) ? e.Title : $"{e.Actor} {e.Title}");
        _announcement = $"New update: {string.Join(". ", parts)}";
        _liveRegion?.Announce(_announcement);
    }

    /// <summary>The last announcement (tests).</summary>
    internal string? Announcement => _announcement;

    /// <summary>Prepares a container for a row (called by the list).</summary>
    internal void PrepareRow(Control container, TimelineRowModel? model)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        switch (container)
        {
            case TimelineRow row when model is TimelineEventModel ev:
                row.Owner = this;
                row.Prepare(ev);
                break;
            case TimelineDayHeader header when model is TimelineDayModel day:
                header.Owner = this;
                header.Model = day;
                header.Background = HeaderBackground;
                header.Set(DayLabel(day), day.Count, silent: true, HeadingLevel);
                if (day.Fresh && day.EnterUntil > Stopwatch.GetTimestamp() && level != ArcMotionLevel.Off)
                {
                    header.PlayEnter(level);
                }
                else
                {
                    header.ShowAtRest();
                }
                day.EnterUntil = 0;
                break;
        }
    }

    /// <summary>Releases a container (called by the list).</summary>
    internal void ClearRow(Control container)
    {
        switch (container)
        {
            case TimelineRow row:
                if (row.Model is { Leaving: true } leaving)
                {
                    // Scrolled away mid-exit: it leaves at once (after this layout pass).
                    Dispatcher.UIThread.Post(() => RemoveLeaving(leaving));
                }
                row.Clear();
                break;
            case TimelineDayHeader header:
                if (header.Model is { Leaving: true } leavingDay)
                {
                    Dispatcher.UIThread.Post(() => RemoveLeaving(leavingDay));
                }
                header.Model = null;
                break;
        }
    }

    /// <summary>Re-reads counts, labels and last-of-day flags into the realized containers (after a data change).</summary>
    private void RefreshRealized()
    {
        foreach (var container in RealizedContainers)
        {
            switch (container)
            {
                case TimelineRow { Model: not null } row:
                    row.Refresh();
                    break;
                case TimelineDayHeader { Model: { } day } header:
                    header.Set(DayLabel(day), day.Count, silent: false, HeadingLevel);
                    break;
            }
        }
        if (_sticky?.Model is { } stickyDay)
        {
            _sticky.Set(DayLabel(stickyDay), stickyDay.Count, silent: false, HeadingLevel);
        }
    }

    /// <summary>
    /// <see cref="Now"/>, the time zone or the culture changed: only the time labels and day labels of the rows in view
    /// change (rows are never rebuilt); rows that scroll in later read the new values when they are prepared.
    /// </summary>
    private void RefreshLabels(bool animate)
    {
        foreach (var container in RealizedContainers)
        {
            switch (container)
            {
                case TimelineRow { Model: { } model } row:
                    var (label, full) = TimeLabels(model);
                    row.SetTime(label, full, animate);
                    break;
                case TimelineDayHeader { Model: { } day } header:
                    header.Set(DayLabel(day), day.Count, silent: !animate, HeadingLevel);
                    break;
            }
        }
        if (_sticky?.Model is { } stickyDay)
        {
            _sticky.Set(DayLabel(stickyDay), stickyDay.Count, silent: true, HeadingLevel);
        }
    }

    /// <summary>Arc's day label: "Today", "Yesterday", or the weekday with month and day ("Monday, October 19").</summary>
    internal string DayLabel(TimelineDayModel day)
    {
        var now = EffectiveNow;
        var zone = TimeZone;
        if (day.Day == ActivityTime.DayOf(now, zone))
        {
            return "Today";
        }
        if (day.Day == ActivityTime.DayOf(now.AddHours(-24), zone))
        {
            return "Yesterday";
        }
        return ActivityTime.WeekdayMonthDay(day.Newest, zone, EffectiveCulture);
    }

    /// <summary>
    /// Arc's time label: within twelve hours today, relative ("Now", "9m", "2h"; read as "9 minutes ago, …"); else the
    /// clock time. The full text gives the weekday, date and time.
    /// </summary>
    internal (string Label, string Full) TimeLabels(TimelineEventModel model)
    {
        var now = EffectiveNow;
        var zone = TimeZone;
        var culture = EffectiveCulture;
        var at = model.Item.At;
        var full = ActivityTime.Full(at, zone, culture);
        var today = model.Day == ActivityTime.DayOf(now, zone) && now - at < TimeSpan.FromHours(12);
        if (today)
        {
            var relative = ActivityTime.Relative(at, now);
            return (relative.Short, $"{relative.Long}, {full}");
        }
        return (ActivityTime.Clock(at, zone, culture), full);
    }

    #endregion

    #region Expansion

    private void SetExpanded(string id, bool expanded, bool raise)
    {
        if (string.IsNullOrEmpty(id) || _expanded.Contains(id) == expanded)
        {
            return;
        }
        var model = _byKey.TryGetValue("ev:" + id, out var found) ? found as TimelineEventModel : null;
        if (expanded && model is not null && !model.Item.HasDetail)
        {
            return;
        }
        if (expanded)
        {
            _expanded.Add(id);
        }
        else
        {
            _expanded.Remove(id);
        }
        _syncingExpanded = true;
        try
        {
            SetCurrentValue(ExpandedIdsProperty, _expanded.ToArray());
        }
        finally
        {
            _syncingExpanded = false;
        }
        if (model is not null && _list?.ContainerFromItem(model) is TimelineRow row)
        {
            row.SetExpanded(expanded, animate: true);
        }
        if (raise && model is not null)
        {
            RaiseEvent(new TimelineExpandedChangedEventArgs(ExpandedChangedEvent, model.Item, expanded));
        }
    }

    private void SyncExpandedFromProperty()
    {
        var wanted = new HashSet<string>(ExpandedIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        var changed = new List<string>();
        foreach (var id in _expanded)
        {
            if (!wanted.Contains(id))
            {
                changed.Add(id);
            }
        }
        foreach (var id in wanted)
        {
            if (!_expanded.Contains(id))
            {
                changed.Add(id);
            }
        }
        _expanded.Clear();
        _expanded.UnionWith(wanted);
        foreach (var id in changed)
        {
            if (_byKey.TryGetValue("ev:" + id, out var model) && _list?.ContainerFromItem(model) is TimelineRow row)
            {
                row.SetExpanded(_expanded.Contains(id), animate: true);
            }
        }
    }

    #endregion

    #region Keyboard

    /// <summary>↑ / ↓ move between expandable rows, Home / End jump to the first / last (Arc's roving triggers).</summary>
    internal void HandleListKey(KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || e.Key is not (Key.Up or Key.Down or Key.Home or Key.End))
        {
            return;
        }
        if (e.Source is not Visual source || source.FindAncestorOfType<TimelineTrigger>(includeSelf: true) is not { Row.Model: { } model })
        {
            return;
        }
        var index = _rows.IndexOf(model);
        if (index < 0)
        {
            return;
        }
        e.Handled = true;
        var target = e.Key switch
        {
            Key.Home => FindExpandable(0, 1),
            Key.End => FindExpandable(_rows.Count - 1, -1),
            Key.Down => FindExpandable(index + 1, 1),
            _ => FindExpandable(index - 1, -1)
        };
        if (target >= 0 && target != index)
        {
            FocusRow(target);
        }
    }

    private int FindExpandable(int from, int step)
    {
        for (var i = from; i >= 0 && i < _rows.Count; i += step)
        {
            if (_rows[i] is TimelineEventModel { Leaving: false } ev && ev.Item.HasDetail)
            {
                return i;
            }
        }
        return -1;
    }

    private void FocusRow(int index)
    {
        if (_list is null)
        {
            return;
        }
        _list.ScrollIntoView(index);
        if (_list.ContainerFromIndex(index) is TimelineRow { Trigger: { } trigger })
        {
            trigger.Focus(NavigationMethod.Directional);
            trigger.BringIntoView();
        }
    }

    #endregion

    #region Viewport: reveal, sticky day label, feather

    private void OnLayoutUpdated(object? sender, EventArgs e) => SyncViewport();

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => SyncViewport();

    /// <summary>The visible part of the list (in the list's coordinates), from its own and every outer scroller.</summary>
    private void OnListViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        var viewport = e.EffectiveViewport;
        if (viewport.Width <= 0 && viewport.Height <= 0)
        {
            _hasViewport = false;
            return;
        }
        _listViewport = viewport;
        _hasViewport = double.IsFinite(viewport.Y) && double.IsFinite(viewport.Height);
        SyncViewport();
    }

    private void HookPanel() => _panel = _list?.ItemsPanelRoot;

    /// <summary>
    /// Runs after layout and on scroll: reveals rows that came into view (staggered), pins the current day's label at
    /// the top of the visible part of the feed and lets the next day push it up (Arc's <c>position: sticky</c>), and
    /// feathers the bottom edge while more waits below. Only transforms, opacity and text change here.
    /// </summary>
    private void SyncViewport()
    {
        HookPanel();
        if (_panel is null || _list is null || !_hasViewport)
        {
            return;
        }
        // Rows are laid out in the panel: move the list's viewport into the panel's coordinates.
        var offset = _panel.TranslatePoint(default, _list) ?? default;
        _viewport = _listViewport.Translate(-(Vector)offset);
        _realized.Clear();
        foreach (var container in _list.GetRealizedContainers())
        {
            if (container.IsVisible)
            {
                var index = _list.IndexFromContainer(container);
                if (index >= 0)
                {
                    _realized.Add((index, container));
                }
            }
        }
        _realized.Sort(static (a, b) => a.Index.CompareTo(b.Index));
        var top = _viewport.Top;
        var bottom = _viewport.Bottom;

        // Reveal: once per event, when at least 20% of the row is visible.
        foreach (var (_, container) in _realized)
        {
            if (container is TimelineRow { Model: { Revealed: false } } row)
            {
                var b = row.Bounds;
                var visible = Math.Min(b.Bottom, bottom) - Math.Max(b.Top, top);
                if (b.Height > 0 && visible >= RevealThreshold * b.Height)
                {
                    row.Reveal(Schedule());
                }
            }
        }

        UpdateSticky(top);
        UpdateFeather();
    }

    /// <summary>Rows revealed together are spaced 90ms apart (at most 450ms out), so the line reads as drawing down.</summary>
    private TimeSpan Schedule()
    {
        if (!ArcMotion.IsFull(this))
        {
            return TimeSpan.Zero;
        }
        var current = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        var start = Math.Min(Math.Max(current, _nextReveal), current + RevealCap);
        _nextReveal = start + RevealStep;
        return TimeSpan.FromSeconds(start - current);
    }

    private void UpdateSticky(double top)
    {
        if (_sticky is null || _realized.Count == 0)
        {
            HideSticky();
            return;
        }
        // The first row still (partly) below the top edge decides the day.
        Control? first = null;
        TimelineRowModel? firstModel = null;
        foreach (var (index, container) in _realized)
        {
            if (container.Bounds.Bottom > top + 0.5)
            {
                first = container;
                firstModel = index < _rows.Count ? _rows[index] : null;
                break;
            }
        }
        if (first is null || firstModel is null)
        {
            HideSticky();
            return;
        }
        var day = firstModel as TimelineDayModel ?? (_days.TryGetValue(firstModel.Day, out var owning) ? owning : null);
        if (day is null)
        {
            HideSticky();
            return;
        }
        var dayIndex = day.Index;
        if (_list!.ContainerFromIndex(dayIndex) is { IsVisible: true } own && own.Bounds.Top >= top - 0.5)
        {
            // The label sits in its natural place.
            HideSticky();
            return;
        }

        // The section ends where the next day's label starts (less the day gap), or at the end of the feed.
        var sectionEnd = double.PositiveInfinity;
        var nextIndex = day.NextIndex;
        if (nextIndex < _rows.Count)
        {
            if (_list.ContainerFromIndex(nextIndex) is { IsVisible: true } next)
            {
                sectionEnd = next.Bounds.Top - DayGap;
            }
        }
        else if (_list.ContainerFromIndex(_rows.Count - 1) is { IsVisible: true } last)
        {
            sectionEnd = last.Bounds.Bottom - DayGap;
        }

        if (!ReferenceEquals(_sticky.Model, day))
        {
            _sticky.Model = day;
            _sticky.Set(DayLabel(day), day.Count, silent: true, HeadingLevel);
        }
        else if (_sticky.CountValue != day.Count)
        {
            _sticky.Set(DayLabel(day), day.Count, silent: false, HeadingLevel);
        }
        var height = _sticky.Bounds.Height > 0 ? _sticky.Bounds.Height : _sticky.DesiredSize.Height;
        var y = top + Math.Min(0, sectionEnd - top - height);
        var origin = _panel!.TranslatePoint(default, _sticky.GetVisualParent() ?? _sticky) ?? default;
        _stickyShift.Y = origin.Y + y;
        _sticky.Opacity = 1;
        // Like Arc's sticky label, the pinned label covers (and takes the clicks of) what scrolls under it.
        _sticky.IsHitTestVisible = true;
    }

    private void HideSticky()
    {
        if (_sticky is not null && _sticky.Opacity != 0)
        {
            _sticky.Opacity = 0;
            _sticky.IsHitTestVisible = false;
        }
    }

    /// <summary>While more updates wait below, the last visible row fades into the bottom edge (36px).</summary>
    private void UpdateFeather()
    {
        if (_scroller is null || double.IsPositiveInfinity(MaxHeight))
        {
            return;
        }
        var more = _scroller.Extent.Height - _scroller.Viewport.Height - _scroller.Offset.Y > 2;
        if (more == _featherOn)
        {
            return;
        }
        _featherOn = more;
        _feather ??= CreateFeatherValue();
        var target = more ? FeatherHeight : 0;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level == ArcMotionLevel.Off)
        {
            _feather.Jump(target);
            ApplyFeather(target);
            return;
        }
        var duration = level == ArcMotionLevel.Full ? ArcMotionPresets.Standard : ArcMotionPresets.Instant;
        _ = _feather.AnimateTo(target, MotionSpec.Tween(duration, ArcEasings.Standard));
    }

    private MotionValue<double> CreateFeatherValue()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += ApplyFeather;
        return value;
    }

    private void ApplyFeather(double feather)
    {
        if (_scroller is null)
        {
            return;
        }
        var height = _scroller.Bounds.Height;
        var px = (int)Math.Round(Math.Clamp(feather, 0, FeatherHeight));
        if (px <= 0 || height <= 0)
        {
            _scroller.OpacityMask = null;
            return;
        }
        if (!height.Equals(_featherMaskHeight))
        {
            _featherMasks.Clear();
            _featherMaskHeight = height;
        }
        if (!_featherMasks.TryGetValue(px, out var mask))
        {
            var stop = Math.Clamp(1 - px / height, 0, 1);
            mask = new ImmutableLinearGradientBrush(
                [new ImmutableGradientStop(0, Colors.Black), new ImmutableGradientStop(stop, Colors.Black), new ImmutableGradientStop(1, Colors.Transparent)],
                startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
                endPoint: new RelativePoint(0, 1, RelativeUnit.Relative));
            _featherMasks[px] = mask;
        }
        _scroller.OpacityMask = mask;
    }

    private MotionValue<double> CreateScrollValue()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += y =>
        {
            if (_scroller is not null)
            {
                _scroller.Offset = _scroller.Offset.WithY(Math.Max(0, y));
            }
        };
        return value;
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new TimelineAutomationPeer(this);

    /// <summary>A named region (Arc's <c>role="region"</c> with <c>aria-label</c>).</summary>
    private sealed class TimelineAutomationPeer(Timeline owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => ((Timeline)Owner).Label;
    }
}
