using System.Collections;
using System.Collections.Specialized;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc;

public partial class ChoroplethExplorer
{
    /// <summary>The block's width from which the map and the list stand side by side (notes 8: 820.8 two columns, 819.9 one).</summary>
    internal const double WideWidth = 820;

    /// <summary>The block's width under which the compact (phone) layout applies (notes 8: 559.8 / 561.6).</summary>
    internal const double CompactWidth = 560;

    /// <summary>The header keeps its controls beside the title from this inner width.</summary>
    internal const double HeaderInlineWidth = 720;

    /// <summary>A region's opacity when it is context around an opened region (.26) and when a legend step dims it (.14).</summary>
    internal const double ContextOpacity = 0.26;

    internal const double DimmedOpacity = 0.14;

    private static readonly AnalyticsSwapStyle CrossFade = new()
    {
        ExitDuration = ArcMotionPresets.Standard,
        ExitEasing = AnalyticsEasings.Enter,
        EnterDuration = ArcMotionPresets.Standard,
        EnterEasing = AnalyticsEasings.Enter,
    };

    private Grid? _header;
    private Grid? _controls;
    private StackPanel? _titleBlock;
    private AnalyticsTextSwap? _title;
    private TextBlock? _description;
    private MorphSelect? _areaSwitch;
    private SegmentedControl? _metricSwitch;
    private ChoroplethBodyPanel? _body;
    private AnalyticsTextSwap? _summaryName;
    private AnalyticsCountUp? _headline;
    private AnalyticsTextSwap? _headlineLabel;
    private PresenceHost? _backPresence;
    private ChoroplethBackButton? _back;
    private ChoroplethMapView? _map;
    private ChoroplethLegend? _legend;
    private AnalyticsTextSwap? _legendCaption;
    private AnalyticsTextSwap? _sideCount;
    private AnalyticsTextSwap? _sideNote;
    private ScrollViewer? _scroller;
    private ChoroplethRankList? _list;
    private StackPanel? _empty;
    private TextBlock? _emptyTitle;
    private TextBlock? _emptyText;
    private ChoroplethBackButton? _emptyBack;
    private AnalyticsAnnouncer? _announcer;

    private readonly AnalyticsPalette _palette = new();
    private readonly List<ChoroplethArea> _observedAreas = new();
    private readonly List<ChoroplethRegion> _observedRegions = new();
    private INotifyCollectionChanged? _observedSource;
    private IReadOnlyList<ChoroplethMetric> _metrics = [];
    private Level? _top;
    private Level? _sub;
    private ChoroplethRegion? _mapHover;
    private bool _mapHoverIsSub;
    private ChoroplethRegion? _listHover;
    private ChoroplethRegion? _listFocus;
    private int? _hoverBin;
    private bool _syncing;
    private bool _refreshQueued;
    private double _width = double.NaN;
    private int _layout = -1;
    private ChoroplethArea? _shownArea;
    private ChoroplethMetric? _shownMetric;
    private bool _keyboardMode;
    private Color _positive;
    private Color _negative;
    private Color _noData;
    private bool _dark;

    static ChoroplethExplorer()
    {
        FocusableProperty.OverrideDefaultValue<ChoroplethExplorer>(false);
    }

    public ChoroplethExplorer()
    {
        Areas.CollectionChanged += OnAreasChanged;
        Metrics.CollectionChanged += OnMetricsChanged;
        ActualThemeVariantChanged += (_, _) => Rebuild(Motion.Data);
        AddHandler(KeyDownEvent, OnKeyDownBubble, RoutingStrategies.Bubble, handledEventsToo: false);
        AddHandler(KeyDownEvent, (_, _) => _keyboardMode = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboardMode = false, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    #region Internals for tests

    internal ChoroplethMapView? Map => _map;

    internal ChoroplethLegend? Legend => _legend;

    internal ChoroplethRankList? List => _list;

    internal AnalyticsAnnouncer? Announcer => _announcer;

    internal MorphSelect? AreaSwitch => _areaSwitch;

    internal SegmentedControl? MetricSwitch => _metricSwitch;

    internal ChoroplethBackButton? BackButton => _back;

    internal StackPanel? EmptyPanel => _empty;

    internal TextBlock? EmptyTitle => _emptyTitle;

    internal AnalyticsCountUp? Headline => _headline;

    internal AnalyticsTextSwap? LegendCaptionText => _legendCaption;

    internal AnalyticsTextSwap? SideCountText => _sideCount;

    internal AnalyticsTextSwap? TitleText => _title;

    internal ChoroplethBinning? TopBinning => _top?.Binning;

    internal ChoroplethBinning? SubBinning => _sub?.Binning;

    internal Color PositiveRamp => _positive;

    internal Color NegativeRamp => _negative;

    internal int? HoveredBin => _hoverBin;

    #endregion

    #region Public methods

    /// <summary>Flies the camera to the region with <paramref name="regionKey"/> (null zooms out), like choosing it.</summary>
    public void ZoomTo(string? regionKey) => SetCurrentValue(OpenRegionKeyProperty, regionKey);

    /// <summary>Zooms back out to the whole area.</summary>
    public void ZoomOut() => SetCurrentValue(OpenRegionKeyProperty, null);

    /// <summary>Releases the pinned legend step and the pinned subregion.</summary>
    public void ClearPins()
    {
        SetCurrentValue(PinnedBinIndexProperty, null);
        SetCurrentValue(PinnedSubregionKeyProperty, null);
    }

    /// <summary>Re-reads every value and outline (after changing regions in place without the change events).</summary>
    public void Refresh() => Rebuild(Motion.Data);

    #endregion

    // Template -------------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _header = e.NameScope.Find<Grid>(HeaderPart);
        _controls = e.NameScope.Find<Grid>(ControlsPart);
        _titleBlock = e.NameScope.Find<StackPanel>("PART_TitleBlock");
        _title = e.NameScope.Find<AnalyticsTextSwap>(TitlePart);
        _description = e.NameScope.Find<TextBlock>("PART_Description");
        _areaSwitch = e.NameScope.Find<MorphSelect>(AreaSwitchPart);
        _metricSwitch = e.NameScope.Find<SegmentedControl>(MetricSwitchPart);
        _body = e.NameScope.Find<ChoroplethBodyPanel>(BodyPart);
        _summaryName = e.NameScope.Find<AnalyticsTextSwap>(SummaryNamePart);
        _headline = e.NameScope.Find<AnalyticsCountUp>(HeadlinePart);
        _headlineLabel = e.NameScope.Find<AnalyticsTextSwap>(HeadlineLabelPart);
        _backPresence = e.NameScope.Find<PresenceHost>(BackPresencePart);
        _back = e.NameScope.Find<ChoroplethBackButton>(BackPart);
        _map = e.NameScope.Find<ChoroplethMapView>(MapPart);
        _legend = e.NameScope.Find<ChoroplethLegend>(LegendPart);
        _legendCaption = e.NameScope.Find<AnalyticsTextSwap>(LegendCaptionPart);
        _sideCount = e.NameScope.Find<AnalyticsTextSwap>(SideCountPart);
        _sideNote = e.NameScope.Find<AnalyticsTextSwap>(SideNotePart);
        _scroller = e.NameScope.Find<ScrollViewer>(ScrollerPart);
        _list = e.NameScope.Find<ChoroplethRankList>(ListPart);
        _empty = e.NameScope.Find<StackPanel>(EmptyPart);
        _emptyTitle = e.NameScope.Find<TextBlock>(EmptyTitlePart);
        _emptyText = e.NameScope.Find<TextBlock>(EmptyTextPart);
        _emptyBack = e.NameScope.Find<ChoroplethBackButton>(EmptyBackPart);
        _announcer = e.NameScope.Find<AnalyticsAnnouncer>(AnnouncerPart);

        foreach (var swap in new[] { _title, _summaryName, _headlineLabel, _legendCaption, _sideCount, _sideNote })
        {
            if (swap is not null)
            {
                swap.Swap = CrossFade;
            }
        }
        if (_map is not null)
        {
            _map.HoverChanged += OnMapHover;
            _map.Invoked += OnMapInvoked;
            _map.TipProvider = BuildTip;
        }
        if (_legend is not null)
        {
            _legend.StepHovered += OnLegendHover;
            _legend.StepToggled += OnLegendToggled;
        }
        if (_list is not null)
        {
            _list.Scroller = _scroller;
            _list.RowHovered += OnRowHovered;
            _list.RowFocused += OnRowFocused;
            _list.RowInvoked += OnRowInvoked;
        }
        if (_metricSwitch is not null)
        {
            _metricSwitch.SelectionChanged += OnMetricSwitchChanged;
        }
        if (_areaSwitch is not null)
        {
            _areaSwitch.SelectionChanged += OnAreaSwitchChanged;
            _areaSwitch.IsLabelVisible = false;
            _areaSwitch.PanelWidth = 248;
            _areaSwitch.PanelAlignment = MorphSelectAlignment.End;
        }
        if (_back is not null)
        {
            _back.Click += OnBackClick;
        }
        if (_emptyBack is not null)
        {
            _emptyBack.Click += OnBackClick;
        }
        _width = double.NaN;
        _layout = -1;
        _shownArea = null;
        _shownMetric = null;
        PseudoClasses.Set(CardPseudoClass, IsCardChrome);
        Rebuild(Motion.Initial);
    }

    private void Unhook()
    {
        if (_map is not null)
        {
            _map.HoverChanged -= OnMapHover;
            _map.Invoked -= OnMapInvoked;
            _map.TipProvider = null;
        }
        if (_legend is not null)
        {
            _legend.StepHovered -= OnLegendHover;
            _legend.StepToggled -= OnLegendToggled;
        }
        if (_list is not null)
        {
            _list.Scroller = null;
            _list.RowHovered -= OnRowHovered;
            _list.RowFocused -= OnRowFocused;
            _list.RowInvoked -= OnRowInvoked;
        }
        if (_metricSwitch is not null)
        {
            _metricSwitch.SelectionChanged -= OnMetricSwitchChanged;
        }
        if (_areaSwitch is not null)
        {
            _areaSwitch.SelectionChanged -= OnAreaSwitchChanged;
        }
        if (_back is not null)
        {
            _back.Click -= OnBackClick;
        }
        if (_emptyBack is not null)
        {
            _emptyBack.Click -= OnBackClick;
        }
    }

    // Layout ----------------------------------------------------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsFinite(availableSize.Width))
        {
            ApplyWidth(availableSize.Width);
        }
        return base.MeasureOverride(availableSize);
    }

    /// <summary>Picks the layout from the block's own width (notes 8).</summary>
    private void ApplyWidth(double width)
    {
        if (width == _width)
        {
            return;
        }
        _width = width;
        var wide = width >= WideWidth;
        var compact = width < CompactWidth;
        PseudoClasses.Set(WidePseudoClass, wide);
        PseudoClasses.Set(CompactPseudoClass, compact);
        var inner = width - (compact ? 32 : 40);
        var layout = inner >= HeaderInlineWidth ? 0 : compact ? 2 : 1;
        if (_body is not null)
        {
            _body.IsWide = wide;
        }
        if (_scroller is not null)
        {
            _scroller.Height = wide ? double.NaN : compact ? 360 : 400;
        }
        if (_legend is not null)
        {
            _legend.FillsWidth = compact;
            _legend.HorizontalAlignment = compact ? Avalonia.Layout.HorizontalAlignment.Stretch : Avalonia.Layout.HorizontalAlignment.Left;
            _legend.InvalidateMeasure();
        }
        if (layout != _layout)
        {
            _layout = layout;
            ApplyHeaderLayout(layout);
        }
    }

    /// <summary>0: controls beside the title; 1: under it (metrics left, area right); 2: stacked (metrics, then area).</summary>
    private void ApplyHeaderLayout(int layout)
    {
        if (_controls is null || _metricSwitch is null || _areaSwitch is null)
        {
            return;
        }
        if (layout == 0)
        {
            Grid.SetRow(_controls, 0);
            Grid.SetColumn(_controls, 1);
            Grid.SetColumnSpan(_controls, 1);
            _controls.Margin = new Thickness(24, 0, 0, 0);
            _controls.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
            Place(_areaSwitch, 0, 0, 1, new Thickness(0, 0, 8, 0), Avalonia.Layout.HorizontalAlignment.Left);
            Place(_metricSwitch, 0, 2, 1, default, Avalonia.Layout.HorizontalAlignment.Left);
        }
        else
        {
            Grid.SetRow(_controls, 1);
            Grid.SetColumn(_controls, 0);
            Grid.SetColumnSpan(_controls, 2);
            _controls.Margin = new Thickness(0, 12, 0, 0);
            _controls.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            if (layout == 1)
            {
                Place(_metricSwitch, 0, 0, 1, default, Avalonia.Layout.HorizontalAlignment.Left);
                Place(_areaSwitch, 0, 2, 1, new Thickness(8, 0, 0, 0), Avalonia.Layout.HorizontalAlignment.Right);
            }
            else
            {
                Place(_metricSwitch, 0, 0, 3, default, Avalonia.Layout.HorizontalAlignment.Left);
                Place(_areaSwitch, 1, 0, 3, new Thickness(0, 8, 0, 0), Avalonia.Layout.HorizontalAlignment.Left);
            }
        }
    }

    private static void Place(Control control, int row, int column, int span, Thickness margin, Avalonia.Layout.HorizontalAlignment alignment)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, span);
        control.Margin = margin;
        control.HorizontalAlignment = alignment;
    }

    // Property changes ----------------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == AreasSourceProperty)
        {
            ObserveSource(change.GetNewValue<IEnumerable?>());
            Rebuild(Motion.Data);
        }
        else if (property == SelectedAreaKeyProperty)
        {
            if (!_syncing)
            {
                OnAreaKeyChanged(change.GetOldValue<string?>());
            }
        }
        else if (property == SelectedMetricKeyProperty)
        {
            if (!_syncing)
            {
                OnMetricKeyChanged(change.GetOldValue<string?>());
            }
        }
        else if (property == OpenRegionKeyProperty)
        {
            if (!_syncing)
            {
                OnOpenKeyChanged();
            }
        }
        else if (property == PinnedSubregionKeyProperty)
        {
            if (!_syncing)
            {
                OnPinnedSubChanged(true);
            }
        }
        else if (property == PinnedBinIndexProperty)
        {
            ApplyEmphasis(true);
        }
        else if (property == IsCardChromeProperty)
        {
            PseudoClasses.Set(CardPseudoClass, IsCardChrome);
        }
        else if (property == PositiveColorProperty || property == NegativeColorProperty || property == NoDataColorProperty)
        {
            Rebuild(Motion.Plain);
        }
        else if (property == TitleProperty || property == DescriptionProperty || property == LabelsProperty)
        {
            Rebuild(Motion.Data);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ResourcesChanged += OnResourcesChanged;
        Rebuild(Motion.Data);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ResourcesChanged -= OnResourcesChanged;
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e)
    {
        if (this.IsAttachedToVisualTree())
        {
            Rebuild(Motion.Data);
        }
    }

    // Collections -------------------------------------------------------------------------------------------------------------

    private void OnAreasChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (AreasSource is null)
        {
            QueueRefresh();
        }
    }

    private void OnMetricsChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueRefresh();

    private void ObserveSource(IEnumerable? source)
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

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueRefresh();

    private void OnObservedChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueRefresh();

    private void OnRegionChanged(ChoroplethRegion region) => QueueRefresh();

    /// <summary>Collects changes of one frame (many SetMetricValue calls) into one re-bin.</summary>
    private void QueueRefresh()
    {
        if (_refreshQueued)
        {
            return;
        }
        _refreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshQueued = false;
            Rebuild(Motion.Data);
        }, DispatcherPriority.Background);
    }

    private IReadOnlyList<ChoroplethArea> EffectiveAreas()
    {
        if (AreasSource is { } source)
        {
            return source.OfType<ChoroplethArea>().ToList();
        }
        return Areas;
    }

    /// <summary>Follows the collections and values of the areas in use, so a change re-bins.</summary>
    private void ObserveAreas(IReadOnlyList<ChoroplethArea> areas, ChoroplethArea? area)
    {
        foreach (var observed in _observedAreas)
        {
            observed.Regions.CollectionChanged -= OnObservedChanged;
            observed.Metrics.CollectionChanged -= OnObservedChanged;
        }
        _observedAreas.Clear();
        foreach (var region in _observedRegions)
        {
            region.Changed -= OnRegionChanged;
            region.Subregions.CollectionChanged -= OnObservedChanged;
        }
        _observedRegions.Clear();
        foreach (var a in areas)
        {
            a.Regions.CollectionChanged += OnObservedChanged;
            a.Metrics.CollectionChanged += OnObservedChanged;
            _observedAreas.Add(a);
        }
        if (area is null)
        {
            return;
        }
        foreach (var region in area.Regions)
        {
            region.Changed += OnRegionChanged;
            region.Subregions.CollectionChanged += OnObservedChanged;
            _observedRegions.Add(region);
            foreach (var sub in region.Subregions)
            {
                sub.Changed += OnRegionChanged;
                _observedRegions.Add(sub);
            }
        }
    }

    // State changes -------------------------------------------------------------------------------------------------------------

    private void OnAreaKeyChanged(string? oldKey)
    {
        var before = _shownArea;
        _syncing = true;
        try
        {
            SetCurrentValue(OpenRegionKeyProperty, null);
            SetCurrentValue(PinnedSubregionKeyProperty, null);
            SetCurrentValue(PinnedBinIndexProperty, null);
        }
        finally
        {
            _syncing = false;
        }
        _hoverBin = null;
        Rebuild(Motion.Area);
        if (!ReferenceEquals(before, _shownArea))
        {
            RaiseEvent(new ChoroplethSelectionChangedEventArgs(AreaChangedEvent, oldKey, _shownArea?.Key));
            if (_shownArea is { } area && _top is { } top && _shownMetric is { } metric)
            {
                Announce(Labels.AreaAnnouncementFormat, area.Name, CountPhrase(top.Regions.Count, area, false), metric.EffectiveNoun);
            }
        }
    }

    private void OnMetricKeyChanged(string? oldKey)
    {
        var before = _shownMetric;
        _syncing = true;
        try
        {
            SetCurrentValue(PinnedBinIndexProperty, null);
        }
        finally
        {
            _syncing = false;
        }
        _hoverBin = null;
        Rebuild(Motion.Metric);
        if (!ReferenceEquals(before, _shownMetric))
        {
            RaiseEvent(new ChoroplethSelectionChangedEventArgs(MetricChangedEvent, oldKey, _shownMetric?.Key));
            var level = _sub ?? _top;
            if (level is not null && _shownMetric is { } metric && level.Order.Length > 0 && level.Values[level.Order[0]] is { } v)
            {
                Announce(Labels.MetricAnnouncementFormat, metric.EffectiveNoun, level.Regions[level.Order[0]].Name, Spoken(metric.FormatValue(v)));
            }
        }
    }

    private void OnOpenKeyChanged()
    {
        var before = OpenRegion;
        // Arc drops the focus to the page when the rows it was on go away; with the keyboard we move it to the new rows.
        var restoreFocus = _keyboardMode && (IsFocusInside(_list) || IsFocusInside(_empty) || IsFocusInside(_backPresence) || IsFocusInside(_map));
        _syncing = true;
        try
        {
            SetCurrentValue(PinnedSubregionKeyProperty, null);
            SetCurrentValue(PinnedBinIndexProperty, null);
        }
        finally
        {
            _syncing = false;
        }
        _hoverBin = null;
        Rebuild(Motion.Zoom);
        var now = OpenRegion;
        if (ReferenceEquals(before, now))
        {
            return;
        }
        var args = new ChoroplethRegionOpenedEventArgs(RegionOpenedEvent, _shownArea, now);
        RaiseEvent(args);
        if (RegionOpenedCommand is { } command && command.CanExecute(now))
        {
            command.Execute(now);
        }
        if (_shownArea is not { } area || _shownMetric is not { } metric)
        {
            return;
        }
        if (now is null)
        {
            Announce(Labels.ZoomOutFormat, area.RegionNoun, metric.EffectiveNoun);
            if (restoreFocus)
            {
                FocusListSoon(before);
            }
        }
        else
        {
            if (now.HasSubregions)
            {
                Announce(Labels.ZoomInFormat, now.Name, CountPhrase(now.Subregions.Count, area, true), metric.EffectiveNoun);
            }
            else
            {
                Announce(Labels.ZoomInEmptyFormat, now.Name, area.SubregionNounSingular);
            }
            if (restoreFocus)
            {
                if (now.HasSubregions)
                {
                    FocusListSoon(null);
                }
                else
                {
                    Dispatcher.UIThread.Post(() => _emptyBack?.Focus(NavigationMethod.Directional), DispatcherPriority.Background);
                }
            }
        }
    }

    private void FocusListSoon(ChoroplethRegion? region)
    {
        // The rows are rebuilt when the old ones have faded out (180ms); focus once they exist.
        var attempts = 0;
        void TryFocus()
        {
            if (_list is null)
            {
                return;
            }
            var ready = region is null ? _list.Rows.Count > 0 : _list.RowOf(region) is not null;
            if (ready)
            {
                _list.FocusRow(region);
            }
            else if (attempts++ < 60)
            {
                DispatcherTimer.RunOnce(TryFocus, TimeSpan.FromMilliseconds(16));
            }
        }
        Dispatcher.UIThread.Post(TryFocus, DispatcherPriority.Background);
    }

    private static bool IsFocusInside(Visual? visual)
    {
        if (visual is null || TopLevel.GetTopLevel(visual)?.FocusManager?.GetFocusedElement() is not Visual focused)
        {
            return false;
        }
        return ReferenceEquals(focused, visual) || visual.IsVisualAncestorOf(focused);
    }

    private void OnPinnedSubChanged(bool announce)
    {
        var sub = _sub;
        var pinned = sub is not null && PinnedSubregionKey is { } key ? sub.Regions.FirstOrDefault(r => r.Key == key) : null;
        _list?.SetPinned(pinned);
        UpdateOutline();
        RaiseEvent(new ChoroplethSubregionPinnedEventArgs(SubregionPinnedEvent, OpenRegion, pinned));
        if (SubregionPinnedCommand is { } command && command.CanExecute(pinned))
        {
            command.Execute(pinned);
        }
        if (announce && pinned is not null && sub is not null && _shownMetric is { } metric)
        {
            var index = IndexIn(sub, pinned);
            var value = index >= 0 ? sub.Values[index] : null;
            Announce(Labels.PinnedFormat, pinned.Name, value is { } v ? Spoken(metric.FormatValue(v)) : AnalyticsFormat.Missing,
                ChoroplethMath.Format(Labels.RankFormat, AnalyticsFormat.Ordinal(sub.RankOf[index]), sub.Regions.Count));
        }
    }

    // Input handlers ----------------------------------------------------------------------------------------------------------

    private void OnMetricSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _metricSwitch is null)
        {
            return;
        }
        var index = _metricSwitch.SelectedIndex;
        if (index >= 0 && index < _metrics.Count)
        {
            SetCurrentValue(SelectedMetricKeyProperty, _metrics[index].Key);
        }
    }

    private void OnAreaSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _areaSwitch is null)
        {
            return;
        }
        var areas = EffectiveAreas();
        var index = _areaSwitch.SelectedIndex;
        if (index >= 0 && index < areas.Count)
        {
            SetCurrentValue(SelectedAreaKeyProperty, areas[index].Key);
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e) => ZoomOut();

    private void OnMapHover(ChoroplethRegion? region, bool isSub)
    {
        _mapHover = region;
        _mapHoverIsSub = isSub;
        // The map points at a row of the list's level only (counties while zoomed, states otherwise).
        var inList = region is not null && (_sub is not null ? isSub : !isSub);
        _list?.SetActive(inList ? region : _listHover, inList);
        UpdateOutline();
    }

    private void OnMapInvoked(ChoroplethRegion region, bool isSub)
    {
        if (isSub)
        {
            TogglePin(region);
            return;
        }
        if (!ReferenceEquals(region, OpenRegion))
        {
            SetCurrentValue(OpenRegionKeyProperty, region.Key);
        }
    }

    private void OnRowHovered(ChoroplethRegion? region)
    {
        _listHover = region;
        if (_mapHover is null)
        {
            _list?.SetActive(region, false);
        }
        UpdateOutline();
    }

    private void OnRowFocused(ChoroplethRegion? region)
    {
        _listFocus = region;
        UpdateOutline();
    }

    private void OnRowInvoked(ChoroplethRegion region)
    {
        if (_sub is not null)
        {
            TogglePin(region);
            return;
        }
        SetCurrentValue(OpenRegionKeyProperty, region.Key);
    }

    private void TogglePin(ChoroplethRegion region)
    {
        var key = PinnedSubregionKey == region.Key ? null : region.Key;
        _syncing = true;
        try
        {
            SetCurrentValue(PinnedSubregionKeyProperty, key);
        }
        finally
        {
            _syncing = false;
        }
        OnPinnedSubChanged(true);
    }

    private void OnLegendHover(int? bin)
    {
        if (_hoverBin == bin)
        {
            return;
        }
        _hoverBin = bin;
        ApplyEmphasis(true);
    }

    private void OnLegendToggled(int bin) => SetCurrentValue(PinnedBinIndexProperty, PinnedBinIndex == bin ? null : bin);

    private void OnKeyDownBubble(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Escape || _areaSwitch?.IsOpen == true)
        {
            return;
        }
        if (PinnedBinIndex is not null)
        {
            SetCurrentValue(PinnedBinIndexProperty, null);
            e.Handled = true;
        }
        else if (OpenRegion is not null)
        {
            ZoomOut();
            e.Handled = true;
        }
    }

    // Rebuild ---------------------------------------------------------------------------------------------------------------------

    /// <summary>Why the block is rebuilt: picks how the map, the legend and the list move.</summary>
    private enum Motion
    {
        Initial,
        Data,
        Plain,
        Metric,
        Area,
        Zoom,
    }

    /// <summary>One level of the map: its regions, their values, bins and ranks.</summary>
    private sealed class Level
    {
        public required IReadOnlyList<ChoroplethRegion> Regions { get; init; }
        public required double?[] Values { get; init; }
        public required ChoroplethBinning Binning { get; init; }
        public required int[] Bins { get; init; }
        public required int[] Order { get; init; }
        public required int[] RankOf { get; init; }
    }

    private static Level Compute(IReadOnlyList<ChoroplethRegion> regions, ChoroplethMetric metric)
    {
        var values = new double?[regions.Count];
        var names = new string[regions.Count];
        for (var i = 0; i < regions.Count; i++)
        {
            values[i] = regions[i].GetMetricValue(metric.Key);
            names[i] = regions[i].Name;
        }
        var binning = ChoroplethMath.Bin(values, metric.Scale, metric.BinCount);
        var bins = new int[regions.Count];
        for (var i = 0; i < bins.Length; i++)
        {
            bins[i] = binning.BinOf(values[i]);
        }
        var order = ChoroplethMath.RankOrder(values, names);
        var rankOf = new int[regions.Count];
        for (var r = 0; r < order.Length; r++)
        {
            rankOf[order[r]] = r + 1;
        }
        return new Level { Regions = regions, Values = values, Binning = binning, Bins = bins, Order = order, RankOf = rankOf };
    }

    private static int IndexIn(Level level, ChoroplethRegion region)
    {
        for (var i = 0; i < level.Regions.Count; i++)
        {
            if (ReferenceEquals(level.Regions[i], region))
            {
                return i;
            }
        }
        return -1;
    }

    private void Rebuild(Motion motion)
    {
        if (_map is null)
        {
            return;
        }
        var areas = EffectiveAreas();
        var area = areas.FirstOrDefault(a => a.Key == SelectedAreaKey) ?? (areas.Count > 0 ? areas[0] : null);
        _metrics = area is { Metrics.Count: > 0 } ? area.Metrics : Metrics;
        var metric = _metrics.FirstOrDefault(m => m.Key == SelectedMetricKey) ?? (_metrics.Count > 0 ? _metrics[0] : null);
        var open = area is not null && OpenRegionKey is { } openKey ? area.Regions.FirstOrDefault(r => r.Key == openKey) : null;
        ObserveAreas(areas, area);

        var areaChanged = !ReferenceEquals(area, _shownArea);
        var metricChanged = !ReferenceEquals(metric, _shownMetric);
        var formatChanged = areaChanged || metricChanged;
        CurrentArea = area;
        CurrentMetric = metric;
        OpenRegion = open;
        PseudoClasses.Set(ZoomedPseudoClass, open is not null);

        ResolveColors();
        SyncSwitches(areas, area, metric);
        var labels = Labels;

        if (area is null || metric is null)
        {
            _top = null;
            _sub = null;
            _map.SetArea(new Size(975, 610), [], false);
            _list?.SetItems([], ChoroplethListChange.Jump);
            _legend?.SetBins([], [], [], [], false);
            _shownArea = area;
            _shownMetric = metric;
            return;
        }

        _top = Compute(area.Regions, metric);
        _sub = open is { HasSubregions: true } ? Compute(open.Subregions, metric) : null;

        // Map ---------------------------------------------------------------------------------------------------------
        if (areaChanged || motion is Motion.Initial || !_map.Shows(area.Regions))
        {
            _map.SetArea(area.ViewBox, area.Regions, motion == Motion.Area && _shownArea is not null);
        }
        var topMotion = motion switch
        {
            Motion.Metric => ChoroplethFillMotion.Sweep,
            Motion.Plain or Motion.Data when !areaChanged => ChoroplethFillMotion.Plain,
            _ => ChoroplethFillMotion.Jump,
        };
        for (var i = 0; i < _top.Regions.Count; i++)
        {
            var (color, fill) = StyleOf(_top.Binning, _top.Bins[i]);
            _map.SetFill(i, color, fill, topMotion);
        }
        _map.SetSubregions(open is { HasSubregions: true } ? open : null, open?.Subregions, motion == Motion.Zoom);
        if (_sub is { } sub)
        {
            var subMotion = motion == Motion.Metric ? ChoroplethFillMotion.Sweep : motion == Motion.Zoom ? ChoroplethFillMotion.Jump : topMotion;
            for (var i = 0; i < sub.Regions.Count; i++)
            {
                var (color, fill) = StyleOf(sub.Binning, sub.Bins[i]);
                _map.SetSubFill(i, color, fill, subMotion);
            }
        }
        if (motion is Motion.Zoom or Motion.Initial or Motion.Area || areaChanged)
        {
            _map.SetCamera(open?.ResolveBounds(), motion == Motion.Zoom);
        }
        AutomationProperties.SetName(_map, ChoroplethMath.Format(labels.MapNameFormat, open?.Name ?? area.Name, metric.EffectiveNoun));
        AutomationProperties.SetHelpText(_map, labels.MapHelpText);

        // Legend --------------------------------------------------------------------------------------------------------
        var level = _sub ?? _top;
        var levelIsSub = _sub is not null;
        if (_legend is not null)
        {
            var binning = level.Binning;
            var colors = new Color[binning.BinCount];
            var opacities = new double[binning.BinCount];
            var names = new string[binning.BinCount];
            for (var b = 0; b < binning.BinCount; b++)
            {
                (colors[b], opacities[b]) = StyleOf(binning, b);
                names[b] = BinName(binning, b, metric, area, levelIsSub);
            }
            var ticks = binning.Edges.Select(metric.FormatTick).ToArray();
            _legend.SetBins(colors, opacities, ticks, names, motion is Motion.Metric or Motion.Plain or Motion.Data);
            _legend.SetNoData(binning.NoDataCount > 0, _noData, labels.NoDataText,
                ChoroplethMath.Format(labels.LegendNoDataFormat, CountPhrase(binning.NoDataCount, area, levelIsSub)));
            AutomationProperties.SetName(_legend, labels.LegendName);
        }

        // List ------------------------------------------------------------------------------------------------------------
        var showEmpty = open is not null && !open.HasSubregions;
        if (_empty is not null)
        {
            _empty.IsVisible = showEmpty;
        }
        if (_scroller is not null)
        {
            _scroller.IsVisible = !showEmpty;
        }
        if (_list is not null)
        {
            _list.SetColors(_positive, _negative);
            var change = motion switch
            {
                Motion.Metric => ChoroplethListChange.Reorder,
                Motion.Area or Motion.Zoom => ChoroplethListChange.Replace,
                Motion.Data or Motion.Plain when !areaChanged => ChoroplethListChange.Reorder,
                _ => ChoroplethListChange.Jump,
            };
            if (!showEmpty)
            {
                _list.SetItems(BuildItems(level, metric, area, levelIsSub), change);
            }
            var pinned = _sub is not null && PinnedSubregionKey is { } pk ? _sub.Regions.FirstOrDefault(r => r.Key == pk) : null;
            _list.SetPinned(pinned);
            AutomationProperties.SetName(_list, ChoroplethMath.Format(labels.RankedByFormat, metric.EffectiveNoun));
        }
        if (showEmpty && open is not null)
        {
            var index = IndexIn(_top, open);
            var rankPhrase = ChoroplethMath.Format(labels.RankFormat, AnalyticsFormat.Ordinal(_top.RankOf[index]), CountPhrase(_top.Regions.Count, area, false));
            var value = _top.Values[index] is { } ov ? metric.FormatValue(ov) : AnalyticsFormat.Missing;
            var lead = open.EmptyDetailText ?? area.EmptyDetailText;
            var sentence = ChoroplethMath.Format(labels.NoDetailFormat, open.Name, rankPhrase, metric.EffectiveNoun, value);
            if (_emptyTitle is not null)
            {
                _emptyTitle.Text = ChoroplethMath.Format(labels.NoDetailTitleFormat, area.SubregionNounSingular, open.Name);
            }
            if (_emptyText is not null)
            {
                _emptyText.Text = string.IsNullOrWhiteSpace(lead) ? sentence : lead.Trim() + " " + sentence;
            }
        }

        // Texts ------------------------------------------------------------------------------------------------------------
        var title = Title ?? area.Title ?? ChoroplethMath.Format(labels.TitleFormat, _metrics.Count > 0 ? _metrics[0].Label : metric.Label, area.RegionNounSingular);
        if (_title is not null)
        {
            _title.Text = title;
        }
        if (_description is not null)
        {
            _description.Text = Description;
            _description.IsVisible = !string.IsNullOrEmpty(Description);
        }
        if (_summaryName is not null)
        {
            _summaryName.Text = open?.Name ?? area.Name;
        }
        if (_headlineLabel is not null)
        {
            _headlineLabel.Text = metric.HeadlineLabel ?? metric.Label;
        }
        if (_headline is not null)
        {
            double? headline = open is not null
                ? _top.Values[IndexIn(_top, open)]
                : ChoroplethMath.Aggregate(_top.Values, WeightsFor(area.Regions), metric.Aggregate);
            var format = metric;
            _headline.Formatter = v => format.FormatValue(v);
            if (formatChanged || motion is Motion.Initial)
            {
                // A different format (metric or currency) replaces the number instead of counting (notes 6).
                var duration = _headline.Duration;
                _headline.Duration = TimeSpan.Zero;
                _headline.Value = headline ?? double.NaN;
                _headline.Duration = duration;
            }
            else
            {
                _headline.Value = headline ?? double.NaN;
            }
        }
        if (_sideCount is not null)
        {
            _sideCount.Text = showEmpty && open is not null ? open.Name : CountPhrase(level.Regions.Count, area, levelIsSub);
        }
        if (_sideNote is not null)
        {
            _sideNote.Text = ChoroplethMath.Format(labels.RankedByFormat, metric.EffectiveNoun);
        }
        if (_back is not null)
        {
            _back.Content = ChoroplethMath.Format(labels.BackFormat, area.RegionNoun);
        }
        if (_emptyBack is not null)
        {
            _emptyBack.Content = ChoroplethMath.Format(labels.BackToAllFormat, area.RegionNoun);
        }
        if (_backPresence is not null)
        {
            _backPresence.IsPresent = open is not null;
        }
        if (_metricSwitch is not null)
        {
            AutomationProperties.SetName(_metricSwitch, labels.MetricSwitchName);
        }
        if (_areaSwitch is not null)
        {
            _areaSwitch.Label = labels.AreaSwitchName;
        }

        _shownArea = area;
        _shownMetric = metric;
        _map.RefreshTip();
        ApplyEmphasis(motion is not (Motion.Initial or Motion.Area));
        UpdateOutline();
    }

    private IReadOnlyList<double?> WeightsFor(IReadOnlyList<ChoroplethRegion> regions)
    {
        if (_metrics.Count == 0)
        {
            return [];
        }
        var key = _metrics[0].Key;
        return regions.Select(r => r.GetMetricValue(key)).ToArray();
    }

    private List<ChoroplethRankItem> BuildItems(Level level, ChoroplethMetric metric, ChoroplethArea area, bool isSub)
    {
        var labels = Labels;
        var max = 0.0;
        foreach (var v in level.Values)
        {
            if (v is { } value)
            {
                max = Math.Max(max, Math.Abs(value));
            }
        }
        var items = new List<ChoroplethRankItem>(level.Regions.Count);
        foreach (var index in level.Order)
        {
            var region = level.Regions[index];
            var value = level.Values[index];
            var rank = level.RankOf[index];
            var valueText = value is { } v ? metric.FormatValue(v) : AnalyticsFormat.Missing;
            var name = ChoroplethMath.Format(labels.RowFormat, AnalyticsFormat.Ordinal(rank), region.Name, Spoken(valueText));
            if (!isSub)
            {
                name += ". " + (region.HasSubregions
                    ? ChoroplethMath.Format(labels.RowOpenFormat, CountPhrase(region.Subregions.Count, area, true))
                    : labels.RowZoomText);
            }
            items.Add(new ChoroplethRankItem
            {
                Region = region,
                Name = region.Name,
                ValueText = valueText,
                RankText = rank.ToString(AnalyticsFormat.DefaultCulture),
                Rank = rank,
                Fraction = value is { } f && max > 0 ? Math.Abs(f) / max : 0,
                IsNegative = value is < 0,
                HasChevron = !isSub && region.HasSubregions,
                AutomationName = name,
                Bin = level.Bins[index],
            });
        }
        return items;
    }

    private string CountPhrase(int count, ChoroplethArea area, bool sub) =>
        ChoroplethMath.Format(Labels.CountFormat, count.ToString("N0", AnalyticsFormat.DefaultCulture),
            sub ? area.SubregionWord(count) : area.RegionWord(count));

    private string BinName(ChoroplethBinning binning, int bin, ChoroplethMetric metric, ChoroplethArea area, bool sub)
    {
        var labels = Labels;
        var count = CountPhrase(binning.Counts[bin], area, sub);
        var lower = binning.Lower(bin);
        var upper = binning.Upper(bin);
        if (lower is null && upper is { } u)
        {
            return Spoken(ChoroplethMath.Format(labels.LegendBelowFormat, count, metric.FormatTick(u)));
        }
        if (upper is null && lower is { } l)
        {
            return Spoken(ChoroplethMath.Format(labels.LegendAboveFormat, count, metric.FormatTick(l)));
        }
        if (lower is { } lo && upper is { } hi)
        {
            return Spoken(ChoroplethMath.Format(labels.LegendRangeFormat, count, metric.FormatTick(lo), metric.FormatTick(hi)));
        }
        return count;
    }

    private string Spoken(string text) => ChoroplethMath.Spoken(text, Labels.MinusWord);

    private (Color Color, double Fill) StyleOf(ChoroplethBinning binning, int bin)
    {
        if (bin < 0 || bin >= binning.BinCount)
        {
            return (_noData, 1);
        }
        var color = binning.IsNegative(bin) ? _negative : _positive;
        return (color, ChoroplethMath.ComposeOpacity(binning.Alphas[bin], _dark));
    }

    /// <summary>
    /// The ramp's hues (notes 3.4): the accent when it has colour (chroma above .02), else oklch(.58 .15 250) in light and
    /// oklch(.73 .15 250) in dark; the negative hue opposite it (measured oklch(.72 .18 74) light, oklch(.665 .21 65) dark
    /// for the default; otherwise L + .14 clamped to .45–.76, chroma .18, hue + 184); no data is the border colour.
    /// </summary>
    private void ResolveColors()
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }
        _palette.Refresh(this);
        _dark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        var accent = _palette.Accent;
        var (_, chroma, _) = Oklch.FromColor(accent);
        var fromAccent = chroma > 0.02;
        _positive = PositiveColor ?? (fromAccent ? accent : _dark ? Oklch.ToColor(0.73, 0.15, 250) : Oklch.ToColor(0.58, 0.15, 250));
        if (NegativeColor is { } negative)
        {
            _negative = negative;
        }
        else if (PositiveColor is null && !fromAccent)
        {
            _negative = _dark ? Oklch.ToColor(0.665, 0.21, 65) : Oklch.ToColor(0.72, 0.18, 74);
        }
        else
        {
            var (pl, _, ph) = Oklch.FromColor(_positive);
            _negative = Oklch.ToColor(Math.Clamp(pl + 0.14, 0.45, 0.76), 0.18, (ph + 184) % 360);
        }
        _noData = NoDataColor ?? _palette.Border;
    }

    private void SyncSwitches(IReadOnlyList<ChoroplethArea> areas, ChoroplethArea? area, ChoroplethMetric? metric)
    {
        _syncing = true;
        try
        {
            if (_metricSwitch is not null)
            {
                var keys = _metrics.Select(m => m.Key + "\u0001" + m.Label).ToArray();
                var current = _metricSwitch.Items.OfType<SegmentedOption>().Select(o => o.Value + "\u0001" + o.Label).ToArray();
                if (!keys.SequenceEqual(current))
                {
                    _metricSwitch.Items.Clear();
                    foreach (var m in _metrics)
                    {
                        _metricSwitch.Items.Add(new SegmentedOption(m.Key, m.Label));
                    }
                }
                _metricSwitch.SelectedIndex = metric is null ? -1 : IndexOfMetric(metric);
                _metricSwitch.IsVisible = _metrics.Count > 1;
            }
            if (_areaSwitch is not null)
            {
                var signature = areas.Select(a => a.Key + "\u0001" + a.Name + "\u0001" + a.Regions.Count).ToArray();
                var shown = _areaSwitch.Items.OfType<SelectItem>().Select(i => i.Value + "\u0001" + i.Content + "\u0001" + i.Meta).ToArray();
                if (!signature.SequenceEqual(shown))
                {
                    _areaSwitch.Items.Clear();
                    foreach (var a in areas)
                    {
                        _areaSwitch.Items.Add(new SelectItem
                        {
                            Content = a.Name,
                            Value = a.Key,
                            Meta = a.Regions.Count.ToString(AnalyticsFormat.DefaultCulture),
                        });
                    }
                }
                _areaSwitch.SelectedIndex = area is null ? -1 : IndexOfArea(areas, area);
                _areaSwitch.IsVisible = areas.Count > 1;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private int IndexOfMetric(ChoroplethMetric metric)
    {
        for (var i = 0; i < _metrics.Count; i++)
        {
            if (ReferenceEquals(_metrics[i], metric))
            {
                return i;
            }
        }
        return -1;
    }

    private static int IndexOfArea(IReadOnlyList<ChoroplethArea> areas, ChoroplethArea area)
    {
        for (var i = 0; i < areas.Count; i++)
        {
            if (ReferenceEquals(areas[i], area))
            {
                return i;
            }
        }
        return -1;
    }

    // Emphasis -----------------------------------------------------------------------------------------------------------------

    /// <summary>The lit legend step: the pinned one, else the one under the pointer.</summary>
    private int? ActiveBin => PinnedBinIndex ?? _hoverBin;

    /// <summary>
    /// Opacities from the zoom and the lit legend step (notes 4.5): zoomed, the other regions are context (.26) and the opened
    /// one hides when its subregions show; a lit step keeps its regions and dims every other one to .14 (context regions too);
    /// rows outside it fade to .32. The caption under the legend names the lit step.
    /// </summary>
    private void ApplyEmphasis(bool animate)
    {
        if (_map is null || _top is null)
        {
            return;
        }
        var active = ActiveBin;
        var open = OpenRegion;
        var sub = _sub;
        for (var i = 0; i < _top.Regions.Count; i++)
        {
            var region = _top.Regions[i];
            double opacity;
            if (open is null)
            {
                opacity = active is { } a && _top.Bins[i] != a ? DimmedOpacity : 1;
            }
            else if (ReferenceEquals(region, open))
            {
                opacity = open.HasSubregions ? 0 : active is { } a && sub is null && _top.Bins[i] != a ? DimmedOpacity : 1;
            }
            else if (active is not null && (sub is not null || _top.Bins[i] != active))
            {
                opacity = DimmedOpacity;
            }
            else
            {
                opacity = ContextOpacity;
            }
            _map.SetOpacity(i, opacity, animate);
        }
        if (sub is not null)
        {
            for (var i = 0; i < sub.Regions.Count; i++)
            {
                _map.SetSubOpacity(i, active is { } a && sub.Bins[i] != a ? DimmedOpacity : 1, animate);
            }
        }
        _list?.SetLitBin(active);
        _legend?.SetState(active, PinnedBinIndex);
        if (_legendCaption is not null && _shownMetric is { } metric && _shownArea is { } area)
        {
            var level = sub ?? _top;
            if (active is { } bin)
            {
                _legendCaption.Text = bin == NoDataBinIndex
                    ? ChoroplethMath.Format(Labels.LegendNoDataFormat, CountPhrase(level.Binning.NoDataCount, area, sub is not null))
                    : bin < level.Binning.BinCount
                        ? BinCaption(level.Binning, bin, metric, area, sub is not null)
                        : metric.LegendCaption ?? string.Empty;
            }
            else
            {
                _legendCaption.Text = metric.LegendCaption ?? string.Empty;
            }
        }
    }

    /// <summary>The caption of a lit step as Arc writes it on screen ("10 states $120K to $230K"), with the true minus sign.</summary>
    private string BinCaption(ChoroplethBinning binning, int bin, ChoroplethMetric metric, ChoroplethArea area, bool sub)
    {
        var labels = Labels;
        var count = CountPhrase(binning.Counts[bin], area, sub);
        var lower = binning.Lower(bin);
        var upper = binning.Upper(bin);
        if (lower is null && upper is { } u)
        {
            return ChoroplethMath.Format(labels.LegendBelowFormat, count, metric.FormatTick(u));
        }
        if (upper is null && lower is { } l)
        {
            return ChoroplethMath.Format(labels.LegendAboveFormat, count, metric.FormatTick(l));
        }
        if (lower is { } lo && upper is { } hi)
        {
            return ChoroplethMath.Format(labels.LegendRangeFormat, count, metric.FormatTick(lo), metric.FormatTick(hi));
        }
        return count;
    }

    /// <summary>The outline without a card: the row under the pointer, else the focused row, else the pinned subregion.</summary>
    private void UpdateOutline()
    {
        if (_map is null)
        {
            return;
        }
        var pinned = _sub is not null && PinnedSubregionKey is { } key ? _sub.Regions.FirstOrDefault(r => r.Key == key) : null;
        _map.SetOutline(_listHover ?? _listFocus ?? pinned);
    }

    // Hover card -------------------------------------------------------------------------------------------------------------------

    private ChoroplethTip? BuildTip(ChoroplethRegion region, bool isSub)
    {
        var level = isSub ? _sub : _top;
        if (level is null || _shownArea is not { } area || _shownMetric is not { } current)
        {
            return null;
        }
        var index = IndexIn(level, region);
        if (index < 0)
        {
            return null;
        }
        var labels = Labels;
        var rows = new List<ChoroplethTipRow>(_metrics.Count);
        foreach (var metric in _metrics)
        {
            var value = region.GetMetricValue(metric.Key);
            rows.Add(new ChoroplethTipRow(metric.Label, value is { } v ? metric.FormatValue(v) : AnalyticsFormat.Missing, ReferenceEquals(metric, current)));
        }
        string? hint = null;
        if (!isSub && !ReferenceEquals(region, OpenRegion))
        {
            hint = region.HasSubregions
                ? ChoroplethMath.Format(labels.OpenHintFormat, CountPhrase(region.Subregions.Count, area, true))
                : labels.ZoomHintText;
        }
        var rank = ChoroplethMath.Format(labels.RankFormat, AnalyticsFormat.Ordinal(level.RankOf[index]), level.Regions.Count);
        return new ChoroplethTip(region.Name, rank, rows, hint);
    }

    // Announcements ------------------------------------------------------------------------------------------------------------------

    private void Announce(string format, params object?[] args) => _announcer?.Announce(ChoroplethMath.Format(format, args));
}
