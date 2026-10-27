using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class CohortRetention
{
    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<CohortRetention, double>(nameof(NarrowWidth), 560);

    internal static readonly StyledProperty<double> StackedWidthProperty =
        AvaloniaProperty.Register<CohortRetention, double>(nameof(StackedWidth), 440);

    internal static readonly StyledProperty<double> MetricValueMinSizeProperty =
        AvaloniaProperty.Register<CohortRetention, double>(nameof(MetricValueMinSize), 22);

    internal static readonly StyledProperty<double> MetricValueMaxSizeProperty =
        AvaloniaProperty.Register<CohortRetention, double>(nameof(MetricValueMaxSize), 28);

    internal static readonly StyledProperty<double> MetricValueScaleProperty =
        AvaloniaProperty.Register<CohortRetention, double>(nameof(MetricValueScale), 0.03);

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    internal double StackedWidth { get => GetValue(StackedWidthProperty); set => SetValue(StackedWidthProperty, value); }

    internal double MetricValueMinSize { get => GetValue(MetricValueMinSizeProperty); set => SetValue(MetricValueMinSizeProperty, value); }

    internal double MetricValueMaxSize { get => GetValue(MetricValueMaxSizeProperty); set => SetValue(MetricValueMaxSizeProperty, value); }

    internal double MetricValueScale { get => GetValue(MetricValueScaleProperty); set => SetValue(MetricValueScaleProperty, value); }

    private CohortRetentionSplitPanel? _header;
    private SegmentedControl? _segmentSwitch;
    private CohortRetentionKpiRow? _metrics;
    private CohortRetentionSplitPanel? _toolbar;
    private CohortRetentionLegend? _legend;
    private SegmentedControl? _viewSwitch;
    private CohortRetentionPlot? _plot;
    private CohortRetentionDataTable? _table;
    private AnalyticsAnnouncer? _announcer;

    private readonly List<RetentionSegment> _segments = new();
    private INotifyCollectionChanged? _observedSource;
    private CohortRetentionModel _model = CohortRetentionModel.Empty;
    private RetentionSegment? _shownSegment;
    private bool _syncing;
    private bool _segmentsDirty = true;
    private bool _cursorByKeyboard;

    static CohortRetention()
    {
        FocusableProperty.OverrideDefaultValue<CohortRetention>(false);
    }

    /// <summary>Creates the block.</summary>
    public CohortRetention()
    {
        Segments.CollectionChanged += (_, _) => OnSegmentsChanged();
        PseudoClasses.Set(HeatmapPseudoClass, true);
    }

    /// <summary>The headline metrics (tests).</summary>
    internal CohortRetentionKpiRow? Metrics => _metrics;

    /// <summary>The plot (tests).</summary>
    internal CohortRetentionPlot? Plot => _plot;

    /// <summary>The legend (tests).</summary>
    internal CohortRetentionLegend? Legend => _legend;

    /// <summary>The live region (tests).</summary>
    internal AnalyticsAnnouncer? Announcer => _announcer;

    /// <summary>The screen reader table (tests).</summary>
    internal CohortRetentionDataTable? Table => _table;

    /// <summary>The segments in effect (<see cref="SegmentsSource"/> or <see cref="Segments"/>).</summary>
    public IReadOnlyList<RetentionSegment> EffectiveSegments
    {
        get
        {
            EnsureSegments();
            return _segments;
        }
    }

    /// <summary>Clears the cursor (and the card).</summary>
    public void ClearCursor() => SetCurrentValue(CursorCellProperty, null);

    /// <summary>Moves the keyboard focus to the chart (the cursor appears on the first cell when there is none).</summary>
    public void FocusChart()
    {
        if (_plot is null)
        {
            return;
        }
        _plot.Focus(Avalonia.Input.NavigationMethod.Tab);
        if (CursorCell is null && _model.First(HasAverage) is { } first)
        {
            _cursorByKeyboard = true;
            SetCurrentValue(CursorCellProperty, first);
        }
    }

    /// <summary>Selects the segment whose <see cref="RetentionSegment.Key"/> (or label) is <paramref name="key"/>; false when none matches.</summary>
    public bool SelectSegment(string key)
    {
        var match = EffectiveSegments.FirstOrDefault(s => s.Key == key) ?? EffectiveSegments.FirstOrDefault(s => s.Label == key);
        if (match is null)
        {
            return false;
        }
        SelectedSegment = match;
        return true;
    }

    private bool HasAverage => ShowAverageRow && _model.AverageLength > 0;

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_segmentSwitch is not null)
        {
            _segmentSwitch.SelectionChanged -= OnSegmentSwitchChanged;
        }
        if (_viewSwitch is not null)
        {
            _viewSwitch.SelectionChanged -= OnViewSwitchChanged;
        }
        if (_plot is not null)
        {
            _plot.CursorRequested -= OnCursorRequested;
            _plot.CellInvoked -= OnPlotCellInvoked;
            _plot.CompactChanged -= OnCompactChanged;
        }

        _header = e.NameScope.Find<CohortRetentionSplitPanel>(HeaderPart);
        _segmentSwitch = e.NameScope.Find<SegmentedControl>(SegmentSwitchPart);
        _metrics = e.NameScope.Find<CohortRetentionKpiRow>(MetricsPart);
        _toolbar = e.NameScope.Find<CohortRetentionSplitPanel>(ToolbarPart);
        _legend = e.NameScope.Find<CohortRetentionLegend>(LegendPart);
        _viewSwitch = e.NameScope.Find<SegmentedControl>(ViewSwitchPart);
        _plot = e.NameScope.Find<CohortRetentionPlot>(PlotPart);
        _table = e.NameScope.Find<CohortRetentionDataTable>(TablePart);
        _announcer = e.NameScope.Find<AnalyticsAnnouncer>(AnnouncerPart);

        if (_segmentSwitch is not null)
        {
            _segmentSwitch.SelectionChanged += OnSegmentSwitchChanged;
        }
        if (_viewSwitch is not null)
        {
            _viewSwitch.SelectionChanged += OnViewSwitchChanged;
        }
        if (_plot is not null)
        {
            _plot.CursorRequested += OnCursorRequested;
            _plot.CellInvoked += OnPlotCellInvoked;
            _plot.CompactChanged += OnCompactChanged;
        }
        _segmentsDirty = true;
        UpdateOptions();
        UpdateSwitches();
        _legend?.SetView(View, animate: false);
        _plot?.SetView(View, animate: false);
        Refresh(animate: false, announce: false);
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
        var narrow = width < NarrowWidth;
        var stacked = width < StackedWidth;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(StackedPseudoClass, stacked);
        if (_header is not null)
        {
            _header.IsWrapForced = narrow;
        }
        if (_toolbar is not null)
        {
            _toolbar.IsWrapForced = stacked;
            _toolbar.IsReversedWhenWrapped = stacked;
        }
        if (_metrics is not null)
        {
            _metrics.IsStacked = stacked;
            var size = Math.Clamp(Math.Round(width * MetricValueScale, 2), MetricValueMinSize, MetricValueMaxSize);
            _metrics.First.ValueSize = size;
            _metrics.Second.ValueSize = size;
            _metrics.Total.ValueSize = size;
        }
    }

    private void OnCompactChanged(bool compact) => PseudoClasses.Set(CompactPseudoClass, compact);

    #endregion

    #region Property changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == SegmentsSourceProperty)
        {
            ObserveSource(change.GetNewValue<IEnumerable?>());
            OnSegmentsChanged();
        }
        else if (property == SelectedSegmentProperty)
        {
            if (!_syncing)
            {
                Refresh(animate: true, announce: true);
            }
        }
        else if (property == ViewProperty)
        {
            OnViewChanged(change.GetOldValue<CohortRetentionView>(), change.GetNewValue<CohortRetentionView>());
        }
        else if (property == CursorCellProperty)
        {
            OnCursorChanged();
        }
        else if (property == MonthsProperty || property == FirstMetricMonthProperty || property == SecondMetricMonthProperty ||
                 property == CompareCohortCountProperty)
        {
            Refresh(animate: false, announce: false);
        }
        else if (property == ThresholdsProperty || property == RampMixProperty || property == ShowSizeColumnProperty ||
                 property == ShowAverageRowProperty || property == AverageLabelProperty || property == AxisTitleProperty ||
                 property == AxisTitleShortProperty || property == EmptyTextProperty || property == CountFormatterProperty ||
                 property == PercentFormatterProperty || property == LegendTitleProperty || property == AverageLegendTextProperty ||
                 property == CohortLegendTextProperty)
        {
            UpdateOptions();
            Refresh(animate: false, announce: false);
        }
        else if (property == HeatmapTextProperty || property == CurvesTextProperty || property == SegmentSwitchLabelProperty ||
                 property == ViewSwitchLabelProperty)
        {
            UpdateSwitches();
        }
        else if (property == DeltaFormatterProperty || property == MetricLabelFormatProperty || property == FirstMetricLabelProperty ||
                 property == SecondMetricLabelProperty || property == TotalLabelProperty || property == CompareFormatProperty ||
                 property == TotalContextFormatProperty)
        {
            UpdateMetrics();
        }
        else if (property == ChartHintProperty || property == TitleProperty || property == NotMeasuredTextProperty)
        {
            UpdateAccessibility();
        }
        else if (property.OwnerType == typeof(CohortRetention) && _plot?.CurrentCursor is not null &&
                 (property == CohortTitleFormatProperty || property == MonthFormatProperty || property == StillActiveTextProperty ||
                  property == OfFormatProperty || property == VsAverageTextProperty || property == WeightedOverTextProperty ||
                  property == CohortCountFormatProperty || property == OneCohortTextProperty))
        {
            _plot.RefreshCard();
        }
        else if (property == NarrowWidthProperty || property == StackedWidthProperty || property == MetricValueScaleProperty ||
                 property == MetricValueMinSizeProperty || property == MetricValueMaxSizeProperty)
        {
            UpdateBreakpoints(Bounds.Width);
        }
    }

    private void ObserveSource(IEnumerable? source)
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceCollectionChanged;
        }
        _observedSource = source as INotifyCollectionChanged;
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged += OnSourceCollectionChanged;
        }
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnSegmentsChanged();

    private void OnSegmentsChanged()
    {
        _segmentsDirty = true;
        UpdateSwitches();
        Refresh(animate: false, announce: false);
    }

    private void EnsureSegments()
    {
        if (!_segmentsDirty)
        {
            return;
        }
        _segmentsDirty = false;
        foreach (var segment in _segments)
        {
            segment.Changed -= OnSegmentDataChanged;
        }
        _segments.Clear();
        IEnumerable source = SegmentsSource ?? Segments;
        foreach (var item in source)
        {
            if (item is RetentionSegment segment)
            {
                _segments.Add(segment);
                segment.Changed += OnSegmentDataChanged;
            }
        }
    }

    private void OnSegmentDataChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _shownSegment))
        {
            Refresh(animate: true, announce: false);
        }
        UpdateSwitches();
    }

    #endregion

    #region Data

    private RetentionSegment? EffectiveSelection()
    {
        EnsureSegments();
        var selected = SelectedSegment;
        if (selected is not null && _segments.Contains(selected))
        {
            return selected;
        }
        return _segments.Count > 0 ? _segments[0] : null;
    }

    private void Refresh(bool animate, bool announce)
    {
        var segment = EffectiveSelection();
        if (!ReferenceEquals(segment, SelectedSegment))
        {
            _syncing = true;
            SetCurrentValue(SelectedSegmentProperty, segment);
            _syncing = false;
        }
        var oldSegment = _shownSegment;
        var segmentChanged = !ReferenceEquals(oldSegment, segment);
        _shownSegment = segment;
        _model = new CohortRetentionModel(segment?.Cohorts ?? (IReadOnlyList<RetentionCohort>)[], Months);
        PseudoClasses.Set(EmptyPseudoClass, _model.IsEmpty);
        FirstMetric = _model.AverageAt(FirstMetricMonth - 1);
        SecondMetric = _model.AverageAt(SecondMetricMonth - 1);
        TotalSize = _model.TotalSize;

        _plot?.SetData(_model, animate);
        UpdateMetrics();
        UpdateSwitchSelection();
        if (_viewSwitch is not null)
        {
            _viewSwitch.IsEnabled = !_model.IsEmpty;
        }
        if (_segmentSwitch is not null)
        {
            _segmentSwitch.IsEnabled = _segments.Count > 1;
        }

        if (CursorCell is { } cursor && !_model.Contains(cursor) || CursorCell is { IsAverage: true } && !HasAverage)
        {
            SetCurrentValue(CursorCellProperty, null);
        }
        else
        {
            _plot?.RefreshCard();
        }
        UpdateAccessibility();

        if (segmentChanged && oldSegment is not null)
        {
            RaiseEvent(new CohortRetentionSegmentChangedEventArgs(SegmentChangedEvent, oldSegment, segment));
            if (announce && segment is not null)
            {
                _announcer?.Announce(string.Format(CultureInfo.CurrentCulture, SegmentAnnouncementFormat,
                    segment.Description ?? segment.Label, FirstLabel(), MetricText(FirstMetric), SecondLabel(), MetricText(SecondMetric)));
            }
        }
    }

    private string FirstLabel() => FirstMetricLabel ?? string.Format(CultureInfo.CurrentCulture, MetricLabelFormat, FirstMetricMonth);

    private string SecondLabel() => SecondMetricLabel ?? string.Format(CultureInfo.CurrentCulture, MetricLabelFormat, SecondMetricMonth);

    private string MetricText(double? rate) => rate is { } r ? PercentText(r) : AnalyticsFormat.Missing;

    private string PercentText(double rate) => PercentFormatter?.Invoke(rate) ?? AnalyticsFormat.Percent(rate * 100);

    private string CountText(double count) => CountFormatter?.Invoke(count) ?? AnalyticsFormat.Number(count);

    private string DeltaText(double points) => DeltaFormatter?.Invoke(points) ?? AnalyticsFormat.Points(points, 1, signed: true);

    private void UpdateMetrics()
    {
        if (_metrics is null)
        {
            return;
        }
        var compare = string.Format(CultureInfo.CurrentCulture, CompareFormat, CompareCohortCount);
        Func<double, string>? percent = PercentFormatter is { } pf ? v => pf(v / 100) : null;
        SetMetric(_metrics.First, FirstLabel(), FirstMetricMonth, compare, percent);
        SetMetric(_metrics.Second, SecondLabel(), SecondMetricMonth, compare, percent);

        _metrics.Total.Label = TotalLabel;
        _metrics.Total.SetValue(_model.IsEmpty ? null : (double)_model.TotalSize, AnalyticsNumberFormat.Integer,
            CountFormatter is { } cf ? v => cf(v) : null);
        _metrics.Total.SetChange(null, null);
        _metrics.Total.SetContext(_model.Rows > 0
            ? string.Format(CultureInfo.CurrentCulture, TotalContextFormat, _model.Labels[0], _model.Labels[^1])
            : null);
    }

    private void SetMetric(CohortRetentionKpi kpi, string label, int month, string compare, Func<double, string>? percent)
    {
        kpi.Label = label;
        var value = _model.AverageAt(month - 1);
        kpi.SetValue(value * 100, AnalyticsNumberFormat.Percent(1), percent);
        var change = value is null ? null : _model.CompareChange(month - 1, CompareCohortCount);
        kpi.SetChange(change * 100, DeltaFormatter);
        kpi.SetContext(change is null ? null : compare);
    }

    private void UpdateOptions()
    {
        var options = new CohortRetentionPlotOptions
        {
            Thresholds = Thresholds is { Count: > 0 } t ? t : CohortRetentionPlotOptions.DefaultThresholds,
            RampMix = RampMix is { Count: > 0 } r ? r : AnalyticsColors.CohortSteps,
            ShowSizeColumn = ShowSizeColumn,
            ShowAverageRow = ShowAverageRow,
            AverageLabel = AverageLabel,
            AxisTitle = AxisTitle,
            AxisTitleShort = AxisTitleShort,
            EmptyText = EmptyText,
            Count = v => CountText(v),
            Percent = PercentText,
            Card = BuildCard
        };
        _plot?.SetOptions(options);
        _legend?.SetContent(LegendTitle, AverageLegendText, CohortLegendText, options.Thresholds, options.Weights());
    }

    private CohortRetentionCardContent? BuildCard(CohortCell cell)
    {
        if (!_model.Contains(cell))
        {
            return null;
        }
        var rate = _model.Rate(cell);
        var month = string.Format(CultureInfo.CurrentCulture, MonthFormat, cell.Month);
        var of = string.Format(CultureInfo.CurrentCulture, OfFormat, CountText(_model.RetainedAt(cell)), CountText(_model.SizeAt(cell)));
        if (cell.IsAverage)
        {
            var covered = _model.AverageCoverage[cell.MonthIndex];
            var cohorts = covered == 1 ? OneCohortText : string.Format(CultureInfo.CurrentCulture, CohortCountFormat, covered);
            return new CohortRetentionCardContent(AverageLabel, month, PercentText(rate), StillActiveText, of, WeightedOverText, cohorts,
                AnalyticsTone.Flat);
        }
        var title = string.Format(CultureInfo.CurrentCulture, CohortTitleFormat, _model.Labels[cell.Row]);
        var average = _model.AverageAt(cell.MonthIndex) ?? rate;
        var points = Math.Round((rate - average) * 1000, MidpointRounding.AwayFromZero) / 10;
        // Arc tints a difference of 0.0 as good; so do we (it is not below the average).
        var tone = points < 0 ? AnalyticsTone.Bad : AnalyticsTone.Good;
        return new CohortRetentionCardContent(title, month, PercentText(rate), StillActiveText, of, VsAverageText, DeltaText(points), tone);
    }

    #endregion

    #region Switches

    private void UpdateSwitches()
    {
        EnsureSegments();
        if (_segmentSwitch is not null)
        {
            _syncing = true;
            _segmentSwitch.ItemsSource = _segments.Select(s => new SegmentedOption(s, s.Label)).ToList();
            _segmentSwitch.IsVisible = _segments.Count > 1;
            AutomationProperties.SetName(_segmentSwitch, SegmentSwitchLabel);
            _syncing = false;
        }
        if (_viewSwitch is not null)
        {
            _syncing = true;
            _viewSwitch.ItemsSource = new List<SegmentedOption>
            {
                new(CohortRetentionView.Heatmap, HeatmapText),
                new(CohortRetentionView.Curves, CurvesText)
            };
            AutomationProperties.SetName(_viewSwitch, ViewSwitchLabel);
            _syncing = false;
        }
        UpdateSwitchSelection();
    }

    private void UpdateSwitchSelection()
    {
        _syncing = true;
        try
        {
            if (_segmentSwitch is not null)
            {
                var index = _shownSegment is null ? -1 : _segments.IndexOf(_shownSegment);
                if (_segmentSwitch.SelectedIndex != index)
                {
                    _segmentSwitch.SelectedIndex = index;
                }
            }
            if (_viewSwitch is not null)
            {
                var index = View == CohortRetentionView.Curves ? 1 : 0;
                if (_viewSwitch.SelectedIndex != index)
                {
                    _viewSwitch.SelectedIndex = index;
                }
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnSegmentSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _segmentSwitch is null)
        {
            return;
        }
        var index = _segmentSwitch.SelectedIndex;
        if (index >= 0 && index < _segments.Count)
        {
            SelectedSegment = _segments[index];
        }
    }

    private void OnViewSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _viewSwitch is null || _viewSwitch.SelectedIndex < 0)
        {
            return;
        }
        View = _viewSwitch.SelectedIndex == 1 ? CohortRetentionView.Curves : CohortRetentionView.Heatmap;
    }

    private void OnViewChanged(CohortRetentionView oldView, CohortRetentionView newView)
    {
        PseudoClasses.Set(HeatmapPseudoClass, newView == CohortRetentionView.Heatmap);
        PseudoClasses.Set(CurvesPseudoClass, newView == CohortRetentionView.Curves);
        _plot?.SetView(newView, animate: true);
        _legend?.SetView(newView, animate: true);
        UpdateSwitchSelection();
        UpdateAccessibility();
        _announcer?.Announce(newView == CohortRetentionView.Curves ? CurvesAnnouncement : HeatmapAnnouncement);
        RaiseEvent(new CohortRetentionViewChangedEventArgs(ViewChangedEvent, oldView, newView));
    }

    #endregion

    #region Cursor

    private void OnCursorRequested(CohortCell? cell, bool byKeyboard)
    {
        _cursorByKeyboard = byKeyboard;
        SetCurrentValue(CursorCellProperty, cell);
    }

    private void OnCursorChanged()
    {
        var cell = CursorCell;
        if (cell is { } c && (!_model.Contains(c) || c.IsAverage && !HasAverage))
        {
            SetCurrentValue(CursorCellProperty, null);
            return;
        }
        _plot?.SetCursor(cell);
        var args = CellArgs(CursorChangedEvent, cell);
        if (cell is null)
        {
            _announcer?.Clear();
        }
        else
        {
            _announcer?.Announce(CellAnnouncement(cell.Value));
        }
        _cursorByKeyboard = false;
        RaiseEvent(args);
    }

    private string CellAnnouncement(CohortCell cell)
    {
        var rate = PercentText(_model.Rate(cell));
        if (cell.IsAverage)
        {
            var covered = _model.AverageCoverage[cell.MonthIndex];
            var cohorts = covered == 1 ? OneCohortText : string.Format(CultureInfo.CurrentCulture, CohortCountFormat, covered);
            return string.Format(CultureInfo.CurrentCulture, AverageAnnouncementFormat, cohorts, cell.Month, rate);
        }
        return string.Format(CultureInfo.CurrentCulture, CellAnnouncementFormat, _model.Labels[cell.Row], cell.Month, rate,
            CountText(_model.RetainedAt(cell)), CountText(_model.SizeAt(cell)));
    }

    private CohortCellEventArgs CellArgs(Avalonia.Interactivity.RoutedEvent<CohortCellEventArgs> routedEvent, CohortCell? cell)
    {
        if (cell is not { } c || !_model.Contains(c))
        {
            return new CohortCellEventArgs(routedEvent, null, null, null, null, null, 0);
        }
        return new CohortCellEventArgs(routedEvent, c, c.IsAverage ? null : _model.Source(c.Row), _model.Rate(c), _model.RetainedAt(c),
            _model.SizeAt(c), c.IsAverage ? _model.AverageCoverage[c.MonthIndex] : 1);
    }

    private void OnPlotCellInvoked(CohortCell cell)
    {
        var args = CellArgs(CellInvokedEvent, cell);
        RaiseEvent(args);
        if (CellCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
    }

    #endregion

    #region Accessibility

    private void UpdateAccessibility()
    {
        if (_plot is not null)
        {
            AutomationProperties.SetName(_plot, Summary());
            AutomationProperties.SetHelpText(_plot, ChartHint);
        }
        if (_table is not null)
        {
            var rows = new List<string>();
            var cells = new List<string[]>();
            for (var r = 0; r < _model.Rows; r++)
            {
                rows.Add(_model.Labels[r]);
                cells.Add(RowCells(r, false));
            }
            if (HasAverage)
            {
                rows.Add(AverageLabel);
                cells.Add(RowCells(-1, true));
            }
            _table.Update(Title ?? Summary(), rows.ToArray(), cells.ToArray());
        }
    }

    private string[] RowCells(int row, bool average)
    {
        var list = new List<string>(_model.Months + 1)
        {
            CountText(average ? _model.TotalSize : _model.Sizes[row])
        };
        for (var m = 0; m < _model.Months; m++)
        {
            var cell = average ? CohortCell.Average(m) : new CohortCell(row, m);
            var month = string.Format(CultureInfo.CurrentCulture, MonthFormat, m + 1);
            list.Add(_model.Contains(cell) ? $"{month}: {PercentText(_model.Rate(cell))}" : $"{month}: {NotMeasuredText}");
        }
        return list.ToArray();
    }

    private string Summary()
    {
        if (_model.IsEmpty)
        {
            return EmptyText;
        }
        var view = View == CohortRetentionView.Curves ? CurvesText : HeatmapText;
        var segment = _shownSegment is { } s ? s.Description ?? s.Label : string.Empty;
        var text = $"{view}, {segment}: {_model.Labels[0]} to {_model.Labels[^1]}. {FirstLabel()} {MetricText(FirstMetric)}, " +
                   $"{SecondLabel()} {MetricText(SecondMetric)}.";
        return text;
    }

    #endregion
}
