using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class MetricExplorer
{
    #region Geometry (set by the theme from the token)

    internal static readonly StyledProperty<double> NarrowWidthProperty = AvaloniaProperty.Register<MetricExplorer, double>(nameof(NarrowWidth), 640);
    internal static readonly StyledProperty<double> CompactWidthProperty = AvaloniaProperty.Register<MetricExplorer, double>(nameof(CompactWidth), 440);
    internal static readonly StyledProperty<double> TwoColumnWidthProperty = AvaloniaProperty.Register<MetricExplorer, double>(nameof(TwoColumnWidth), 760);
    internal static readonly StyledProperty<double> SurfaceRadiusProperty = AvaloniaProperty.Register<MetricExplorer, double>(nameof(SurfaceRadius), 16);
    internal static readonly StyledProperty<double> CardGapProperty = AvaloniaProperty.Register<MetricExplorer, double>(nameof(CardGap), 12);
    internal static readonly StyledProperty<double> CompactCardGapProperty = AvaloniaProperty.Register<MetricExplorer, double>(nameof(CompactCardGap), 8);
    internal static readonly StyledProperty<Thickness> CardPaddingProperty = AvaloniaProperty.Register<MetricExplorer, Thickness>(nameof(CardPadding), new Thickness(20, 16));
    internal static readonly StyledProperty<Thickness> CompactCardPaddingProperty = AvaloniaProperty.Register<MetricExplorer, Thickness>(nameof(CompactCardPadding), new Thickness(16, 14));
    internal static readonly StyledProperty<double> SparklineHeightProperty = AvaloniaProperty.Register<MetricExplorer, double>(nameof(SparklineHeight), 28);
    internal static readonly StyledProperty<Thickness> PanelPaddingProperty = AvaloniaProperty.Register<MetricExplorer, Thickness>(nameof(PanelPadding), new Thickness(20, 16, 20, 20));
    internal static readonly StyledProperty<Thickness> NarrowPanelPaddingProperty = AvaloniaProperty.Register<MetricExplorer, Thickness>(nameof(NarrowPanelPadding), new Thickness(16));
    internal static readonly StyledProperty<Thickness> CompactPanelPaddingProperty = AvaloniaProperty.Register<MetricExplorer, Thickness>(nameof(CompactPanelPadding), new Thickness(14, 12, 14, 16));
    internal static readonly StyledProperty<double> TabRowHeightProperty = AvaloniaProperty.Register<MetricExplorer, double>(nameof(TabRowHeight), 32);
    internal static readonly StyledProperty<double> RowGapProperty = AvaloniaProperty.Register<MetricExplorer, double>(nameof(RowGap), 20);
    internal static readonly StyledProperty<double> StackedGapProperty = AvaloniaProperty.Register<MetricExplorer, double>(nameof(StackedGap), 16);
    internal static readonly StyledProperty<double> ChartMinHeightProperty = AvaloniaProperty.Register<MetricExplorer, double>(nameof(ChartMinHeight), 180);
    internal static readonly StyledProperty<double> ChartMaxHeightProperty = AvaloniaProperty.Register<MetricExplorer, double>(nameof(ChartMaxHeight), 240);
    internal static readonly StyledProperty<Thickness> PlotInsetProperty = AvaloniaProperty.Register<MetricExplorer, Thickness>(nameof(PlotInset), new Thickness(52, 8, 6, 28));
    internal static readonly StyledProperty<double> AxisLabelWidthProperty = AvaloniaProperty.Register<MetricExplorer, double>(nameof(AxisLabelWidth), 40);
    internal static readonly StyledProperty<double> LegendTopGapProperty = AvaloniaProperty.Register<MetricExplorer, double>(nameof(LegendTopGap), 20);
    internal static readonly StyledProperty<double> LegendBottomGapProperty = AvaloniaProperty.Register<MetricExplorer, double>(nameof(LegendBottomGap), 8);
    internal static readonly StyledProperty<double> DescriptionGapProperty = AvaloniaProperty.Register<MetricExplorer, double>(nameof(DescriptionGap), 12);

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }
    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
    internal double TwoColumnWidth { get => GetValue(TwoColumnWidthProperty); set => SetValue(TwoColumnWidthProperty, value); }
    internal double SurfaceRadius { get => GetValue(SurfaceRadiusProperty); set => SetValue(SurfaceRadiusProperty, value); }
    internal double CardGap { get => GetValue(CardGapProperty); set => SetValue(CardGapProperty, value); }
    internal double CompactCardGap { get => GetValue(CompactCardGapProperty); set => SetValue(CompactCardGapProperty, value); }
    internal Thickness CardPadding { get => GetValue(CardPaddingProperty); set => SetValue(CardPaddingProperty, value); }
    internal Thickness CompactCardPadding { get => GetValue(CompactCardPaddingProperty); set => SetValue(CompactCardPaddingProperty, value); }
    internal double SparklineHeight { get => GetValue(SparklineHeightProperty); set => SetValue(SparklineHeightProperty, value); }
    internal Thickness PanelPadding { get => GetValue(PanelPaddingProperty); set => SetValue(PanelPaddingProperty, value); }
    internal Thickness NarrowPanelPadding { get => GetValue(NarrowPanelPaddingProperty); set => SetValue(NarrowPanelPaddingProperty, value); }
    internal Thickness CompactPanelPadding { get => GetValue(CompactPanelPaddingProperty); set => SetValue(CompactPanelPaddingProperty, value); }
    internal double TabRowHeight { get => GetValue(TabRowHeightProperty); set => SetValue(TabRowHeightProperty, value); }
    internal double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }
    internal double StackedGap { get => GetValue(StackedGapProperty); set => SetValue(StackedGapProperty, value); }
    internal double ChartMinHeight { get => GetValue(ChartMinHeightProperty); set => SetValue(ChartMinHeightProperty, value); }
    internal double ChartMaxHeight { get => GetValue(ChartMaxHeightProperty); set => SetValue(ChartMaxHeightProperty, value); }
    internal Thickness PlotInset { get => GetValue(PlotInsetProperty); set => SetValue(PlotInsetProperty, value); }
    internal double AxisLabelWidth { get => GetValue(AxisLabelWidthProperty); set => SetValue(AxisLabelWidthProperty, value); }
    internal double LegendTopGap { get => GetValue(LegendTopGapProperty); set => SetValue(LegendTopGapProperty, value); }
    internal double LegendBottomGap { get => GetValue(LegendBottomGapProperty); set => SetValue(LegendBottomGapProperty, value); }
    internal double DescriptionGap { get => GetValue(DescriptionGapProperty); set => SetValue(DescriptionGapProperty, value); }

    private static readonly HashSet<AvaloniaProperty> s_geometryProperties =
    [
        NarrowWidthProperty, CompactWidthProperty, TwoColumnWidthProperty, SurfaceRadiusProperty, CardGapProperty, CompactCardGapProperty,
        CardPaddingProperty, CompactCardPaddingProperty, SparklineHeightProperty, PanelPaddingProperty, NarrowPanelPaddingProperty,
        CompactPanelPaddingProperty, TabRowHeightProperty, RowGapProperty, StackedGapProperty, ChartMinHeightProperty, ChartMaxHeightProperty,
        PlotInsetProperty, AxisLabelWidthProperty, LegendTopGapProperty, LegendBottomGapProperty, DescriptionGapProperty
    ];

    private MetricExplorerGeometry BuildGeometry() => new()
    {
        NarrowWidth = NarrowWidth,
        CompactWidth = CompactWidth,
        TwoColumnWidth = TwoColumnWidth,
        SurfaceRadius = SurfaceRadius,
        CardGap = CardGap,
        CompactCardGap = CompactCardGap,
        CardPadding = CardPadding,
        CompactCardPadding = CompactCardPadding,
        SparklineHeight = SparklineHeight,
        PanelPadding = PanelPadding,
        NarrowPanelPadding = NarrowPanelPadding,
        CompactPanelPadding = CompactPanelPadding,
        TabRowHeight = TabRowHeight,
        RowGap = RowGap,
        StackedGap = StackedGap,
        ChartMinHeight = ChartMinHeight,
        ChartMaxHeight = ChartMaxHeight,
        PlotInset = PlotInset,
        AxisLabelWidth = AxisLabelWidth,
        LegendTopGap = LegendTopGap,
        LegendBottomGap = LegendBottomGap,
        DescriptionGap = DescriptionGap
    };

    #endregion

    private MetricExplorerHeader? _header;
    private MetricExplorerStage? _stage;
    private AnalyticsAnnouncer? _announcer;
    private readonly List<ExplorerMetric> _metrics = new();
    private readonly List<ExplorerRange> _ranges = new();
    private MetricExplorerSeries[] _series = [];
    private INotifyCollectionChanged? _observedMetrics;
    private INotifyCollectionChanged? _observedRanges;
    private bool _dirty = true;
    private bool _openedByKeyboard;
    private bool _closedByUser;

    static MetricExplorer()
    {
        BlockFocusModality.Watch();
        FocusableProperty.OverrideDefaultValue<MetricExplorer>(false);
    }

    /// <summary>Creates the block.</summary>
    public MetricExplorer()
    {
        Metrics.CollectionChanged += OnMetricsCollectionChanged;
        Ranges.CollectionChanged += OnRangesCollectionChanged;
    }

    /// <summary>The stage (tests).</summary>
    internal MetricExplorerStage? Stage => _stage;

    /// <summary>The live region (tests).</summary>
    internal AnalyticsAnnouncer? Announcer => _announcer;

    /// <summary>The metrics in effect (<see cref="MetricsSource"/> or <see cref="Metrics"/>).</summary>
    public IReadOnlyList<ExplorerMetric> EffectiveMetrics
    {
        get
        {
            EnsureData();
            return _metrics;
        }
    }

    /// <summary>The ranges in effect (<see cref="RangesSource"/>, <see cref="Ranges"/>, or the three defaults).</summary>
    public IReadOnlyList<ExplorerRange> EffectiveRanges
    {
        get
        {
            EnsureData();
            return _ranges;
        }
    }

    /// <summary>Opens the chart on metric <paramref name="metricIndex"/>.</summary>
    public void Open(int metricIndex)
    {
        SetCurrentValue(SelectedMetricIndexProperty, metricIndex);
        SetCurrentValue(IsOpenProperty, true);
    }

    /// <summary>Closes the chart (the surface shrinks back into its card).</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    /// <summary>Clears the inspected day.</summary>
    public void ClearScrub() => _stage?.Panel.SetScrub(null);

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_stage is not null)
        {
            _stage.OpenRequested -= OnOpenRequested;
            _stage.CloseRequested -= OnCloseRequested;
            _stage.MetricChosen -= OnMetricChosen;
            _stage.RangeChosen -= OnRangeChosen;
            _stage.ScrubChanged -= OnScrubChanged;
        }
        _header = e.NameScope.Find<MetricExplorerHeader>(HeaderPart);
        _stage = e.NameScope.Find<MetricExplorerStage>(StagePart);
        UpdateHeader();
        _announcer = e.NameScope.Find<AnalyticsAnnouncer>(AnnouncerPart);
        if (_stage is not null)
        {
            _stage.OpenRequested += OnOpenRequested;
            _stage.CloseRequested += OnCloseRequested;
            _stage.MetricChosen += OnMetricChosen;
            _stage.RangeChosen += OnRangeChosen;
            _stage.ScrubChanged += OnScrubChanged;
            _stage.Geometry = BuildGeometry();
        }
        _dirty = true;
        Refresh(animate: false);
        UpdateBreakpoints(Bounds.Width);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateBreakpoints(e.NewSize.Width);
    }

    private void UpdateBreakpoints(double width)
    {
        if (width <= 0)
        {
            return;
        }
        PseudoClasses.Set(NarrowPseudoClass, width < NarrowWidth);
        PseudoClasses.Set(CompactPseudoClass, width <= CompactWidth);
        PseudoClasses.Set(TwoColumnPseudoClass, width < TwoColumnWidth);
    }

    #endregion

    #region Property changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == MetricsSourceProperty)
        {
            ObserveMetrics(change.GetNewValue<IEnumerable?>());
            OnDataChanged();
        }
        else if (property == RangesSourceProperty)
        {
            ObserveRanges(change.GetNewValue<IEnumerable?>());
            OnDataChanged();
        }
        else if (property == IsOpenProperty)
        {
            OnIsOpenChanged(change.GetNewValue<bool>());
        }
        else if (property == SelectedMetricIndexProperty)
        {
            OnMetricIndexChanged(change.GetOldValue<int>(), change.GetNewValue<int>());
        }
        else if (property == SelectedRangeIndexProperty)
        {
            OnRangeIndexChanged(change.GetOldValue<int>(), change.GetNewValue<int>());
        }
        else if (property == TitleProperty || property == CaptionProperty)
        {
            UpdateHeader();
        }
        else if (property == ShowPreviousPeriodProperty || property == ThisPeriodTextProperty || property == PreviousPeriodTextProperty ||
                 property == CloseTextProperty || property == TabsLabelProperty || property == RangeLabelProperty ||
                 property == ScrubLabelProperty || property == ScrubValueFormatProperty || property == SummaryFormatProperty ||
                 property == ScrubCompareFormatProperty || property == CardNameFormatProperty || property == PointsUnitProperty ||
                 property == UpTextProperty || property == DownTextProperty || property == FlatTextProperty)
        {
            Refresh(animate: false);
        }
        else if (s_geometryProperties.Contains(property))
        {
            if (_stage is not null)
            {
                _stage.Geometry = BuildGeometry();
            }
            UpdateBreakpoints(Bounds.Width);
        }
    }

    private void UpdateHeader()
    {
        if (_header is not null)
        {
            _header.IsVisible = Title is not null || Caption is not null;
        }
    }

    private void OnMetricsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnDataChanged();

    private void OnRangesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnDataChanged();

    private void ObserveMetrics(IEnumerable? source)
    {
        if (_observedMetrics is not null)
        {
            _observedMetrics.CollectionChanged -= OnMetricsCollectionChanged;
        }
        _observedMetrics = source as INotifyCollectionChanged;
        if (_observedMetrics is not null)
        {
            _observedMetrics.CollectionChanged += OnMetricsCollectionChanged;
        }
    }

    private void ObserveRanges(IEnumerable? source)
    {
        if (_observedRanges is not null)
        {
            _observedRanges.CollectionChanged -= OnRangesCollectionChanged;
        }
        _observedRanges = source as INotifyCollectionChanged;
        if (_observedRanges is not null)
        {
            _observedRanges.CollectionChanged += OnRangesCollectionChanged;
        }
    }

    private void OnDataChanged()
    {
        _dirty = true;
        Refresh(animate: false);
    }

    private void OnItemChanged(object? sender, EventArgs e)
    {
        _dirty = true;
        Refresh(animate: true);
    }

    private void EnsureData()
    {
        if (!_dirty)
        {
            return;
        }
        _dirty = false;
        foreach (var metric in _metrics)
        {
            metric.Changed -= OnItemChanged;
        }
        foreach (var range in _ranges)
        {
            range.Changed -= OnItemChanged;
        }
        _metrics.Clear();
        _ranges.Clear();
        foreach (var item in MetricsSource ?? Metrics)
        {
            if (item is ExplorerMetric metric)
            {
                _metrics.Add(metric);
                metric.Changed += OnItemChanged;
            }
        }
        foreach (var item in RangesSource ?? Ranges)
        {
            if (item is ExplorerRange range)
            {
                _ranges.Add(range);
                range.Changed += OnItemChanged;
            }
        }
        if (_ranges.Count == 0)
        {
            _ranges.AddRange(ExplorerRange.CreateDefaults());
        }
        _series = _metrics.Select(m => new MetricExplorerSeries(m)).ToArray();
    }

    #endregion

    #region State

    private int MetricIndex => _series.Length == 0 ? 0 : Math.Clamp(SelectedMetricIndex, 0, _series.Length - 1);

    private int RangeIndex => _ranges.Count == 0 ? 0 : Math.Clamp(SelectedRangeIndex, 0, _ranges.Count - 1);

    private bool EffectiveOpen => IsOpen && _series.Length > 0;

    private MetricExplorerTexts Texts() => new()
    {
        ThisPeriod = ThisPeriodText,
        PreviousPeriod = PreviousPeriodText,
        Close = CloseText,
        TabsLabel = TabsLabel,
        RangeLabel = RangeLabel,
        ScrubName = ScrubLabel,
        ScrubFormat = ScrubValueFormat,
        SummaryFormat = SummaryFormat,
        ScrubCompareFormat = ScrubCompareFormat,
        CardNameFormat = CardNameFormat,
        PointsUnit = PointsUnit,
        Up = UpText,
        Down = DownText,
        Flat = FlatText
    };

    private void Refresh(bool animate)
    {
        EnsureData();
        PseudoClasses.Set(OpenPseudoClass, EffectiveOpen);
        if (_stage is null)
        {
            return;
        }
        if (_series.Length == 0 || _ranges.Count == 0)
        {
            _stage.Configure(new MetricExplorerState { Series = [], Ranges = [], Texts = Texts() }, false, false);
            return;
        }
        var state = new MetricExplorerState
        {
            Series = _series,
            Ranges = _ranges.ToArray(),
            Metric = MetricIndex,
            Range = RangeIndex,
            ShowPrevious = ShowPreviousPeriod,
            Texts = Texts()
        };
        _stage.Configure(state, EffectiveOpen, animate);
    }

    private void OnIsOpenChanged(bool open)
    {
        if (open && _series.Length == 0)
        {
            return;
        }
        Refresh(animate: true);
        var index = MetricIndex;
        var metric = _series.Length > 0 ? _series[index].Source : null;
        if (open)
        {
            var args = new MetricExplorerMetricEventArgs(MetricOpenedEvent, index, metric);
            RaiseEvent(args);
            RunCommand(OpenedCommand, args);
            if (_openedByKeyboard)
            {
                // Focus can only land once the chart has been laid out.
                Dispatcher.UIThread.Post(() => _stage?.FocusTabs(), DispatcherPriority.Loaded);
            }
        }
        else
        {
            _stage?.Panel.SetScrub(null);
            SetScrub(null);
            var args = new MetricExplorerMetricEventArgs(MetricClosedEvent, index, metric);
            RaiseEvent(args);
            RunCommand(ClosedCommand, args);
            _announcer?.Announce(string.Format(CultureInfo.CurrentCulture, ClosedAnnouncementFormat, _series.Length));
            if (_closedByUser)
            {
                // Closed with a click: the card takes focus back without the keyboard ring.
                var method = BlockFocusModality.For(this);
                Dispatcher.UIThread.Post(() => _stage?.FocusCard(index, method), DispatcherPriority.Loaded);
            }
        }
        _openedByKeyboard = false;
        _closedByUser = false;
    }

    private void OnMetricIndexChanged(int oldIndex, int newIndex)
    {
        EnsureData();
        Refresh(animate: true);
        if (_series.Length == 0)
        {
            return;
        }
        var index = MetricIndex;
        RaiseEvent(new MetricExplorerMetricEventArgs(SelectedMetricChangedEvent, index, _series[index].Source));
        if (EffectiveOpen)
        {
            Announce();
        }
    }

    private void OnRangeIndexChanged(int oldIndex, int newIndex)
    {
        EnsureData();
        Refresh(animate: true);
        if (_ranges.Count == 0)
        {
            return;
        }
        var range = RangeIndex;
        var args = new MetricExplorerRangeEventArgs(SelectedRangeChangedEvent, oldIndex, range, _ranges[range]);
        RaiseEvent(args);
        RunCommand(RangeChangedCommand, args);
        if (EffectiveOpen)
        {
            Announce();
        }
    }

    private static void RunCommand(System.Windows.Input.ICommand? command, object parameter)
    {
        if (command is not null && command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    private void Announce()
    {
        if (_series.Length == 0 || _ranges.Count == 0)
        {
            return;
        }
        var series = _series[MetricIndex];
        var range = _ranges[RangeIndex];
        var change = series.Change(series.Latest, series.At(range.Days));
        var (direction, _) = series.Trend(change);
        var word = direction switch { AnalyticsDirection.Up => UpText, AnalyticsDirection.Down => DownText, _ => FlatText };
        var plain = !double.IsFinite(change)
            ? AnalyticsFormat.Missing
            : series.ChangeMode == ExplorerChangeMode.Points
                ? AnalyticsFormat.Points(Math.Abs(change), 1, false, PointsUnit)
                : AnalyticsFormat.Percent(Math.Abs(change), 1);
        _announcer?.Announce(string.Format(CultureInfo.CurrentCulture, AnnouncementFormat, series.Title, range.CardText,
            series.ValueText(series.Latest), word, plain, range.CompareText));
    }

    #endregion

    #region Stage events

    private void OnOpenRequested(int index, bool keyboard)
    {
        _openedByKeyboard = keyboard;
        SetCurrentValue(SelectedMetricIndexProperty, index);
        SetCurrentValue(IsOpenProperty, true);
    }

    private void OnCloseRequested()
    {
        _closedByUser = true;
        SetCurrentValue(IsOpenProperty, false);
    }

    private void OnMetricChosen(int index) => SetCurrentValue(SelectedMetricIndexProperty, index);

    private void OnRangeChosen(int index) => SetCurrentValue(SelectedRangeIndexProperty, index);

    private void OnScrubChanged(int? index) => SetScrub(index);

    private void SetScrub(int? index)
    {
        if (_scrubIndex == index)
        {
            return;
        }
        ScrubIndex = index;
        PseudoClasses.Set(ScrubbingPseudoClass, index is not null);
        if (_series.Length == 0 || _ranges.Count == 0)
        {
            return;
        }
        var series = _series[MetricIndex];
        var days = _ranges[RangeIndex].Days;
        if (index is { } i)
        {
            var k = days - 1 - i;
            var previous = series.At(k + days);
            RaiseEvent(new MetricExplorerScrubEventArgs(ScrubChangedEvent, i, series.DateAt(k), series.At(k), series.DateAt(k + days),
                double.IsFinite(previous) ? previous : null));
        }
        else
        {
            RaiseEvent(new MetricExplorerScrubEventArgs(ScrubChangedEvent, null, null, null, null, null));
        }
    }

    #endregion
}
