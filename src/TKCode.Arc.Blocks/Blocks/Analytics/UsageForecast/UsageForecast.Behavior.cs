using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class UsageForecast
{
    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<UsageForecast, double>(nameof(CompactWidth), 560);
    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<UsageForecast, double>(nameof(NarrowWidth), 440);
    internal static readonly StyledProperty<double> HeadlineSizeProperty =
        AvaloniaProperty.Register<UsageForecast, double>(nameof(HeadlineSize), 22);
    internal static readonly StyledProperty<double> HeadlineMinSizeProperty =
        AvaloniaProperty.Register<UsageForecast, double>(nameof(HeadlineMinSize), 18);
    internal static readonly StyledProperty<double> HeadlineScaleProperty =
        AvaloniaProperty.Register<UsageForecast, double>(nameof(HeadlineScale), 0.03);
    internal static readonly StyledProperty<double> TileValueSizeProperty =
        AvaloniaProperty.Register<UsageForecast, double>(nameof(TileValueSize), 22);
    internal static readonly StyledProperty<double> CompactTileValueSizeProperty =
        AvaloniaProperty.Register<UsageForecast, double>(nameof(CompactTileValueSize), 18);

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }
    internal double HeadlineSize { get => GetValue(HeadlineSizeProperty); set => SetValue(HeadlineSizeProperty, value); }
    internal double HeadlineMinSize { get => GetValue(HeadlineMinSizeProperty); set => SetValue(HeadlineMinSizeProperty, value); }
    internal double HeadlineScale { get => GetValue(HeadlineScaleProperty); set => SetValue(HeadlineScaleProperty, value); }
    internal double TileValueSize { get => GetValue(TileValueSizeProperty); set => SetValue(TileValueSizeProperty, value); }
    internal double CompactTileValueSize { get => GetValue(CompactTileValueSizeProperty); set => SetValue(CompactTileValueSizeProperty, value); }

    private UsageForecastSplitPanel? _header;
    private SegmentedControl? _metricSwitch;
    private UsageWordText? _headline;
    private UsageWordText? _detail;
    private UsageKpiRow? _tiles;
    private UsageKpiItem? _used;
    private UsageKpiItem? _forecastTile;
    private UsageKpiItem? _cost;
    private UsageForecastPlot? _plot;
    private UsageBudgetHandle? _handle;
    private UsageAlertRow? _alerts;
    private UsageLegend? _legend;
    private UsageForecastDataTable? _table;
    private AnalyticsAnnouncer? _announcer;

    private readonly List<UsageMetric> _metrics = new();
    private readonly List<UsageAlertThreshold> _thresholds = new();
    private readonly Dictionary<UsageAlertThreshold, UsageAlertChip> _chips = new();
    private INotifyCollectionChanged? _observedSource;
    private UsageMetric? _metric;
    private UsageSeries _series = new(30, 0, default);
    private UsageAnalysis? _analysis;
    private bool _syncing;
    private bool _defaultsAdded;
    private bool _metricsDirty = true;
    private UsageBudgetMotion? _pendingMotion;
    private bool _ready;

    static UsageForecast()
    {
        FocusableProperty.OverrideDefaultValue<UsageForecast>(false);
    }

    /// <summary>Creates the block.</summary>
    public UsageForecast()
    {
        Metrics.CollectionChanged += (_, _) => OnMetricsChanged();
        AlertThresholds.CollectionChanged += (_, _) =>
        {
            if (!_syncing)
            {
                RebuildThresholds();
                if (_ready)
                {
                    UpdateAnalysis(false, UsageBudgetMotion.Jump);
                }
            }
        };
        PseudoClasses.Set(FramedPseudoClass, true);
    }

    /// <summary>The chart (tests).</summary>
    internal UsageForecastPlot? Plot => _plot;

    /// <summary>The budget handle (tests).</summary>
    internal UsageBudgetHandle? Handle => _handle;

    /// <summary>The headline (tests).</summary>
    internal UsageWordText? Headline => _headline;

    /// <summary>The line under the headline (tests).</summary>
    internal UsageWordText? Detail => _detail;

    /// <summary>The tiles (tests): used, forecast, cost.</summary>
    internal (UsageKpiItem? Used, UsageKpiItem? Forecast, UsageKpiItem? Cost) Tiles => (_used, _forecastTile, _cost);

    /// <summary>The chips by threshold (tests).</summary>
    internal IReadOnlyDictionary<UsageAlertThreshold, UsageAlertChip> Chips => _chips;

    /// <summary>The live region (tests).</summary>
    internal AnalyticsAnnouncer? Announcer => _announcer;

    /// <summary>The table for screen readers (tests).</summary>
    internal UsageForecastDataTable? Table => _table;

    /// <summary>The legend (tests).</summary>
    internal UsageLegend? Legend => _legend;

    /// <summary>The metric switch (tests).</summary>
    internal SegmentedControl? MetricSwitch => _metricSwitch;

    /// <summary>The series now shown (tests).</summary>
    internal UsageSeries Series => _series;

    /// <summary>What the numbers say about the budget now.</summary>
    internal UsageAnalysis? Analysis => _analysis;

    /// <summary>The metrics in effect (<see cref="MetricsSource"/> or <see cref="Metrics"/>).</summary>
    public IReadOnlyList<UsageMetric> EffectiveMetrics
    {
        get
        {
            EnsureMetrics();
            return _metrics;
        }
    }

    /// <summary>Moves the budget of the shown metric with a spring, as a binding or a key would.</summary>
    public void SetBudget(double value)
    {
        if (_metric is { } metric)
        {
            metric.Budget = Math.Clamp(value, metric.EffectiveBudgetMinimum, double.IsNaN(metric.BudgetMaximum) ? double.MaxValue : metric.BudgetMaximum);
        }
    }

    /// <summary>Moves the keyboard focus to the chart; the card appears on today.</summary>
    public void FocusChart() => _plot?.Focus(Avalonia.Input.NavigationMethod.Tab);

    /// <summary>Moves the keyboard focus to the budget line.</summary>
    public void FocusBudget() => _handle?.Focus(Avalonia.Input.NavigationMethod.Tab);

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_metricSwitch is not null)
        {
            _metricSwitch.SelectionChanged -= OnMetricSwitchChanged;
        }
        if (_plot is not null)
        {
            _plot.InspectRequested -= OnInspectRequested;
            _plot.BudgetEdited -= OnBudgetEdited;
        }
        _header = e.NameScope.Find<UsageForecastSplitPanel>(HeaderPart);
        _metricSwitch = e.NameScope.Find<SegmentedControl>(MetricSwitchPart);
        _headline = e.NameScope.Find<UsageWordText>(HeadlinePart);
        _detail = e.NameScope.Find<UsageWordText>(DetailPart);
        _tiles = e.NameScope.Find<UsageKpiRow>(TilesPart);
        _used = e.NameScope.Find<UsageKpiItem>(UsedPart);
        _forecastTile = e.NameScope.Find<UsageKpiItem>(ForecastPart);
        _cost = e.NameScope.Find<UsageKpiItem>(CostPart);
        _plot = e.NameScope.Find<UsageForecastPlot>(PlotPart);
        _handle = e.NameScope.Find<UsageBudgetHandle>(HandlePart);
        _alerts = e.NameScope.Find<UsageAlertRow>(AlertsPart);
        _legend = e.NameScope.Find<UsageLegend>(LegendPart);
        _table = e.NameScope.Find<UsageForecastDataTable>(TablePart);
        _announcer = e.NameScope.Find<AnalyticsAnnouncer>(AnnouncerPart);
        if (_metricSwitch is not null)
        {
            _metricSwitch.SelectionChanged += OnMetricSwitchChanged;
        }
        if (_plot is not null)
        {
            _plot.InspectRequested += OnInspectRequested;
            _plot.BudgetEdited += OnBudgetEdited;
            _plot.PanelProvider = PanelFor;
            _plot.Handle = _handle;
        }
        if (_handle is not null)
        {
            _handle.Plot = _plot;
        }
        _chips.Clear();
        _ready = true;
        _metricsDirty = true;
        Rebuild(animate: false, announce: false);
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
        var compact = width < CompactWidth;
        var narrow = width < NarrowWidth;
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        if (_header is not null)
        {
            _header.IsWrapForced = compact;
        }
        if (_tiles is not null)
        {
            _tiles.IsStacked = narrow;
            foreach (var tile in new[] { _used, _forecastTile, _cost })
            {
                if (tile is not null)
                {
                    tile.ValueSize = compact ? CompactTileValueSize : TileValueSize;
                }
            }
        }
        if (_alerts is not null)
        {
            _alerts.IsStacked = narrow;
        }
        var size = Math.Clamp(Math.Round(width * HeadlineScale, 2), HeadlineMinSize, HeadlineSize);
        foreach (var words in new[] { _headline })
        {
            if (words is not null)
            {
                words.FontSize = size;
                words.LineHeight = Math.Round(size * 1.3, 1);
            }
        }
    }

    #endregion

    #region Property changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == MetricsSourceProperty)
        {
            if (_observedSource is not null)
            {
                _observedSource.CollectionChanged -= OnSourceChanged;
            }
            _observedSource = change.GetNewValue<IEnumerable?>() as INotifyCollectionChanged;
            if (_observedSource is not null)
            {
                _observedSource.CollectionChanged += OnSourceChanged;
            }
            OnMetricsChanged();
        }
        else if (property == SelectedMetricProperty)
        {
            if (!_syncing && _ready)
            {
                SelectMetric(change.GetNewValue<UsageMetric?>(), animate: true);
            }
        }
        else if (property == SelectedMetricKeyProperty)
        {
            if (!_syncing && change.GetNewValue<string?>() is { } key && EffectiveMetrics.FirstOrDefault(m => m.Key == key) is { } match)
            {
                SelectedMetric = match;
            }
        }
        else if (property == PeriodStartProperty || property == PeriodDaysProperty || property == TodayIndexProperty ||
                 property == DateFormatProperty || property == LabelsProperty)
        {
            if (_ready)
            {
                Rebuild(animate: false, announce: false);
            }
        }
        else if (property == InspectedDayProperty)
        {
            var day = change.GetNewValue<int?>();
            _plot?.SetInspect(day ?? 0);
            UpdateSliderText();
            RaiseEvent(new UsageInspectedDayChangedEventArgs(InspectedDayChangedEvent, day));
        }
        else if (property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, change.GetNewValue<bool>());
        }
        else if (property == CompactWidthProperty || property == NarrowWidthProperty || property == HeadlineSizeProperty ||
                 property == HeadlineMinSizeProperty || property == HeadlineScaleProperty || property == TileValueSizeProperty ||
                 property == CompactTileValueSizeProperty)
        {
            UpdateBreakpoints(Bounds.Width);
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnMetricsChanged();

    private void OnMetricsChanged()
    {
        _metricsDirty = true;
        if (_ready)
        {
            Rebuild(animate: false, announce: false);
        }
    }

    private void EnsureMetrics()
    {
        if (!_metricsDirty)
        {
            return;
        }
        _metricsDirty = false;
        foreach (var m in _metrics)
        {
            m.PropertyChanged -= OnMetricPropertyChanged;
        }
        _metrics.Clear();
        if (MetricsSource is { } source)
        {
            _metrics.AddRange(source.OfType<UsageMetric>());
        }
        else
        {
            _metrics.AddRange(Metrics);
        }
        foreach (var m in _metrics)
        {
            m.PropertyChanged += OnMetricPropertyChanged;
        }
    }

    private void OnMetricPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender != _metric || !_ready)
        {
            return;
        }
        if (e.Property == UsageMetric.BudgetProperty)
        {
            var motion = _pendingMotion ?? UsageBudgetMotion.Key;
            _pendingMotion = null;
            UpdateAnalysis(animate: true, motion);
        }
        else if (e.Property == UsageMetric.PriceNoteProperty || e.Property == UsageMetric.UnitPriceProperty)
        {
            UpdateAnalysis(animate: true, UsageBudgetMotion.Key);
        }
        else if (e.Property == UsageMetric.KeyProperty)
        {
            // Nothing to redraw.
        }
        else
        {
            Rebuild(animate: true, announce: false);
        }
    }

    #endregion

    #region Rebuild

    private void SelectMetric(UsageMetric? metric, bool animate)
    {
        EnsureMetrics();
        metric ??= _metrics.FirstOrDefault();
        if (metric == _metric)
        {
            return;
        }
        Rebuild(animate, announce: true);
        RaiseEvent(new UsageMetricChangedEventArgs(MetricChangedEvent, _metric));
    }

    private void Rebuild(bool animate, bool announce)
    {
        if (!_ready)
        {
            return;
        }
        EnsureMetrics();
        var requested = SelectedMetric;
        var metric = requested is not null && _metrics.Contains(requested) ? requested : _metrics.FirstOrDefault();
        var changed = metric != _metric;
        _metric = metric;
        _syncing = true;
        try
        {
            if (SelectedMetric != metric)
            {
                SetCurrentValue(SelectedMetricProperty, metric);
            }
            SetCurrentValue(SelectedMetricKeyProperty, metric?.Key);
        }
        finally
        {
            _syncing = false;
        }
        EnsureThresholds();
        UpdateSwitch();
        if (metric is null)
        {
            return;
        }
        var labels = Labels;
        _series = UsageSeries.Build(PeriodDays, TodayIndex, metric.StartValue, metric.Readings, PeriodStart,
            metric.Forecast, metric.ForecastLow, metric.ForecastHigh);
        var dataMax = Math.Max(_series.Line.Max(), _series.Upper.Max());
        var budgetMax = double.IsNaN(metric.BudgetMaximum) ? Math.Max(metric.Budget, dataMax) : metric.BudgetMaximum;
        var (min, max) = UsageForecastEngine.Domain(metric.AxisMinimum, metric.AxisMaximum, dataMax, budgetMax);
        var data = new UsagePlotData
        {
            Series = _series,
            AxisMin = min,
            AxisMax = max,
            BudgetMin = Math.Max(min, metric.EffectiveBudgetMinimum),
            BudgetMax = Math.Min(max, double.IsNaN(metric.BudgetMaximum) ? max : metric.BudgetMaximum),
            BudgetStep = metric.BudgetStep > 0 ? metric.BudgetStep : 1,
            TickTexts = Enumerable.Range(0, 6).Select(i => TickText(metric, min + i * (max - min) / 5)).ToArray(),
            DayText = Day,
            ValueText = metric.FormatValue
        };
        if (_plot is not null)
        {
            _plot.Today = labels.TodayText;
            _plot.BudgetWord = labels.BudgetText;
            _plot.SetData(data, animate && changed, metric.Budget);
        }
        RebuildThresholds();
        UpdateAnalysis(animate && changed, animate && changed ? UsageBudgetMotion.Metric : UsageBudgetMotion.Jump);
        _legend?.SetTexts(labels.LegendMeasuredText, labels.LegendProjectedText, labels.LegendRangeText, labels.LegendOverText);
        UpdateTable(metric);
        UpdateAccessibility(metric);
        if (announce && _analysis is not null)
        {
            _announcer?.Announce($"{metric.Label}: {Spoken()}");
        }
    }

    private static string TickText(UsageMetric metric, double value)
    {
        if (Math.Abs(value) < 1e-9)
        {
            return "0";
        }
        var whole = Math.Abs(value - Math.Round(value)) < 1e-9;
        var text = AnalyticsFormat.Number(value, whole ? 0 : 1, false);
        return text + metric.UnitSeparator + metric.Unit;
    }

    private string Day(int day) => AnalyticsFormat.Date(_series.DateOf(Math.Clamp(day, 1, Math.Max(1, _series.Days))), DateFormat);

    private void UpdateSwitch()
    {
        if (_metricSwitch is null)
        {
            return;
        }
        _syncing = true;
        try
        {
            _metricSwitch.ItemsSource = _metrics.Select(m => new SegmentedOption(m, m.Label)).ToList();
            _metricSwitch.IsVisible = _metrics.Count > 1;
            AutomationProperties.SetName(_metricSwitch, Labels.MetricSwitchText);
            var index = _metric is null ? -1 : _metrics.IndexOf(_metric);
            if (_metricSwitch.SelectedIndex != index)
            {
                _metricSwitch.SelectedIndex = index;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnMetricSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _metricSwitch is null)
        {
            return;
        }
        var index = _metricSwitch.SelectedIndex;
        if (index >= 0 && index < _metrics.Count)
        {
            SelectedMetric = _metrics[index];
        }
    }

    #endregion

    #region Thresholds and chips

    private void EnsureThresholds()
    {
        if (_defaultsAdded || AlertThresholds.Count > 0)
        {
            return;
        }
        _defaultsAdded = true;
        _syncing = true;
        try
        {
            AlertThresholds.Add(new UsageAlertThreshold { Percent = 50, IsEnabled = false });
            AlertThresholds.Add(new UsageAlertThreshold { Percent = 80, IsEnabled = true });
            AlertThresholds.Add(new UsageAlertThreshold { Percent = 100, IsEnabled = true });
        }
        finally
        {
            _syncing = false;
        }
    }

    private void RebuildThresholds()
    {
        foreach (var t in _thresholds)
        {
            t.PropertyChanged -= OnThresholdPropertyChanged;
        }
        _thresholds.Clear();
        _thresholds.AddRange(AlertThresholds);
        foreach (var t in _thresholds)
        {
            t.PropertyChanged += OnThresholdPropertyChanged;
        }
        if (_alerts is null)
        {
            return;
        }
        foreach (var chip in _chips.Values)
        {
            chip.IsCheckedChanged -= OnChipChecked;
            _alerts.Children.Remove(chip);
        }
        _chips.Clear();
        foreach (var t in _thresholds)
        {
            var chip = new UsageAlertChip { PercentText = PercentText(t.Percent), IsChecked = t.IsEnabled, Tag = t };
            chip.IsCheckedChanged += OnChipChecked;
            _chips[t] = chip;
            _alerts.Children.Add(chip);
        }
    }

    private static string PercentText(double percent) => percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";

    private void OnChipChecked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is UsageAlertChip { Tag: UsageAlertThreshold t } chip && !_syncing)
        {
            t.IsEnabled = chip.IsChecked == true;
        }
    }

    private void OnThresholdPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not UsageAlertThreshold t || !_ready)
        {
            return;
        }
        if (e.Property == UsageAlertThreshold.IsEnabledProperty)
        {
            if (_chips.TryGetValue(t, out var chip) && chip.IsChecked != t.IsEnabled)
            {
                _syncing = true;
                chip.IsChecked = t.IsEnabled;
                _syncing = false;
            }
            PushThresholdsToPlot();
            RaiseEvent(new UsageAlertToggledEventArgs(AlertThresholdToggledEvent, t));
            _announcer?.Announce(string.Format(Labels.AlertAnnouncementFormat, PercentText(t.Percent), t.IsEnabled ? Labels.OnText : Labels.OffText));
        }
        else if (e.Property == UsageAlertThreshold.PercentProperty)
        {
            UpdateAnalysis(animate: true, UsageBudgetMotion.Key);
        }
    }

    private void PushThresholdsToPlot()
    {
        _plot?.SetThresholds(_thresholds.Select(t => t.Percent).ToArray(), _thresholds.Select(t => t.IsEnabled).ToArray(),
            _thresholds.Select(t => PercentText(t.Percent)).ToArray());
    }

    #endregion

    #region Analysis

    private static int DecimalsOf(UsageMetric metric, double value)
    {
        var s = metric.FormatPlain(value);
        var dot = s.IndexOf('.');
        return dot < 0 ? 0 : s.Length - dot - 1;
    }

    private void UpdateAnalysis(bool animate, UsageBudgetMotion motion)
    {
        if (_metric is not { } m || !_ready)
        {
            return;
        }
        var labels = Labels;
        var budget = m.Budget;
        var percents = _thresholds.Select(t => t.Percent).ToArray();
        var a = UsageForecastEngine.Analyze(_series, budget, m.UnitPrice, percents);
        _analysis = a;
        SetAndRaise(OutcomeProperty, ref _outcome, a.Outcome);
        var crossing = a.CrossingX;
        SetAndRaise(CrossingDayProperty, ref _crossingDay, crossing);
        var early = a.EarlyDay > 0 ? a.EarlyDay : (int?)null;
        var late = a.LateDay > 0 ? a.LateDay : (int?)null;
        SetAndRaise(CrossingEarlyProperty, ref _crossingEarly, early);
        SetAndRaise(CrossingLateProperty, ref _crossingLate, late);
        SetAndRaise(ProjectedTotalProperty, ref _projectedTotal, a.Projected);
        SetAndRaise(ProjectedLowProperty, ref _projectedLow, a.ProjectedLow);
        SetAndRaise(ProjectedHighProperty, ref _projectedHigh, a.ProjectedHigh);
        SetAndRaise(OverageCostProperty, ref _overageCost, a.OverageCost);
        PseudoClasses.Set(OverBudgetPseudoClass, a.Projected > budget);

        for (var i = 0; i < _thresholds.Count && i < a.Thresholds.Length; i++)
        {
            var s = a.Thresholds[i];
            var date = s.Day > 0 ? _series.DateOf(s.Day) : (DateOnly?)null;
            _thresholds[i].SetState(s.Status, date, s.Day);
            if (_chips.TryGetValue(_thresholds[i], out var chip))
            {
                var text = s.Status switch
                {
                    UsageAlertStatus.AlreadyPast => string.Format(labels.SinceFormat, Day(1)),
                    UsageAlertStatus.Reached => string.Format(labels.ReachedFormat, Day(s.Day)),
                    UsageAlertStatus.Expected => string.Format(labels.ExpectedFormat, Day(s.Day)),
                    _ => labels.NotExpectedText
                };
                chip.StatusText = text;
                AutomationProperties.SetName(chip, string.Format(labels.ChipNameFormat, PercentText(_thresholds[i].Percent), text.ToLowerInvariant()));
            }
        }

        var (headline, detail) = Compose(m, a);
        _headline?.SetText(headline, animate);
        _detail?.SetText(detail, animate);
        UpdateTiles(m, a, animate);

        if (_plot is not null)
        {
            _plot.BudgetValueText = m.FormatPlain(budget) + m.UnitSeparator + m.Unit;
            _plot.SetBudget(budget, motion);
            PushThresholdsToPlot();
            _plot.SetCrossingText(a.CrossingDay > 0 ? Day(a.CrossingDay) : null);
            _plot.RefreshPanel();
            _plot.InvalidateVisual();
        }
        if (_handle is not null)
        {
            AutomationProperties.SetItemStatus(_handle, m.FormatValue(budget));
        }
        UpdateSliderText();
        UpdateSummary(m, headline, detail);
    }

    private string DaysText(int days) => days == 1 ? Labels.OneDayText : string.Format(Labels.DaysFormat, days);

    private (string Headline, string Detail) Compose(UsageMetric m, UsageAnalysis a)
    {
        var l = Labels;
        var budget = m.Budget;
        var n = _series.Days;
        switch (a.Outcome)
        {
            case UsageOutcome.Passed:
            {
                var over = budget > 0 ? Math.Round((a.Used / budget - 1) * 100, MidpointRounding.AwayFromZero) : 0;
                return (string.Format(l.PassedHeadlineFormat, Day(a.CrossingDay)),
                    string.Format(l.PassedDetailFormat, over.ToString("0", CultureInfo.InvariantCulture), m.FormatValue(a.Projected), Day(n)));
            }
            case UsageOutcome.CrossesOnLastDay:
            {
                var detail = a.EarlyDay > 0 && a.EarlyDay < n ? string.Format(l.LastDayDetailFormat, Day(a.EarlyDay)) : l.LastDayPlainText;
                return (string.Format(l.CrossHeadlineFormat, Day(a.CrossingDay)), detail);
            }
            case UsageOutcome.WillCross:
            {
                var left = DaysText(n - a.CrossingDay);
                var early = a.EarlyDay > 0 ? a.EarlyDay : a.CrossingDay;
                var detail = a.LateDay > 0 && a.LateDay != early
                    ? string.Format(l.CrossDetailFormat, left, Day(early), Day(a.LateDay))
                    : string.Format(l.CrossDetailPlainFormat, left);
                return (string.Format(l.CrossHeadlineFormat, Day(a.CrossingDay)), detail);
            }
            default:
            {
                var share = budget > 0 ? Math.Round(a.Projected / budget * 100, MidpointRounding.AwayFromZero) : 0;
                var detail = string.Format(l.UnderDetailFormat, m.FormatValue(a.Projected), Day(n), share.ToString("0", CultureInfo.InvariantCulture));
                if (a.ProjectedHigh >= budget && a.EarlyDay > 0)
                {
                    detail += string.Format(l.UnderRiskFormat, Day(a.EarlyDay));
                }
                return (l.UnderHeadline, detail);
            }
        }
    }

    private string Spoken() => $"{_headline?.Text}. {_detail?.Text}";

    private void UpdateTiles(UsageMetric m, UsageAnalysis a, bool animate)
    {
        var l = Labels;
        var unit = m.UnitSeparator + m.Unit;
        if (_used is not null)
        {
            _used.Label = l.UsedLabel;
            _used.Small = string.Format(l.UsedSmallFormat, a.UsedPercent.ToString("0", CultureInfo.InvariantCulture));
            _used.Show(a.Used, AnalyticsNumberFormat.Number(DecimalsOf(m, a.Used)), unit);
        }
        if (_forecastTile is not null)
        {
            _forecastTile.Label = string.Format(l.ForecastLabelFormat, Day(_series.Days));
            _forecastTile.Small = string.Format(l.RangeSmallFormat, m.FormatPlain(a.ProjectedLow), m.FormatValue(a.ProjectedHigh));
            _forecastTile.Show(a.Projected, AnalyticsNumberFormat.Number(DecimalsOf(m, a.Projected)), unit);
        }
        if (_cost is not null)
        {
            _cost.IsVisible = m.UnitPrice > 0;
            _cost.Label = l.OverageLabel;
            _cost.Small = a.OverageCost > 0 ? m.PriceNote : l.UnderBudgetText;
            _cost.Show(a.OverageCost, AnalyticsNumberFormat.Currency(0), null);
            _cost.SetOver(a.OverageCost > 0, animate);
        }
    }

    #endregion

    #region Inspect, budget edits, accessibility

    private UsagePanelContent PanelFor(int day)
    {
        var m = _metric;
        if (m is null)
        {
            return default;
        }
        var l = Labels;
        var value = _series.Line[day];
        var tag = day == _series.Today ? l.TodayText : day < _series.Today ? l.MeasuredTag : l.ProjectedTag;
        string detail;
        if (day <= _series.Today)
        {
            var pct = m.Budget > 0 ? Math.Round(value / m.Budget * 100, MidpointRounding.AwayFromZero) : 0;
            detail = string.Format(l.PanelUsedFormat, pct.ToString("0", CultureInfo.InvariantCulture));
        }
        else
        {
            detail = string.Format(l.PanelRangeFormat, m.FormatPlain(_series.Lower[day]), m.FormatValue(_series.Upper[day]));
        }
        return new UsagePanelContent(Day(day), tag, m.FormatValue(value), detail);
    }

    private void OnInspectRequested(int? day, bool byKey)
    {
        if (InspectedDay != day)
        {
            InspectedDay = day;
        }
    }

    private void OnBudgetEdited(double value, bool committed)
    {
        if (_metric is not { } m)
        {
            return;
        }
        var old = m.Budget;
        PseudoClasses.Set(DraggingPseudoClass, !committed);
        if (Math.Abs(old - value) > 1e-9)
        {
            _pendingMotion = committed ? UsageBudgetMotion.Key : UsageBudgetMotion.Drag;
            m.Budget = value;
            _pendingMotion = null;
        }
        RaiseEvent(new UsageBudgetChangedEventArgs(BudgetChangedEvent, m, old, value, committed));
        if (committed)
        {
            if (BudgetCommittedCommand is { } command && command.CanExecute(value))
            {
                command.Execute(value);
            }
            _announcer?.Announce(string.Format(Labels.BudgetAnnouncementFormat, m.FormatValue(value)) + " " + Spoken());
        }
    }

    private void UpdateSliderText()
    {
        if (_plot is null || _metric is not { } m || _series.Days < 1)
        {
            return;
        }
        var l = Labels;
        var day = _plot.KeyDay;
        var content = PanelFor(day);
        var text = day <= _series.Today
            ? string.Format(l.SpokenMeasuredFormat, content.Date, content.Value, m.Budget > 0 ? Math.Round(_series.Line[day] / m.Budget * 100, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) : "0")
            : string.Format(l.SpokenProjectedFormat, content.Date, content.Value, m.FormatPlain(_series.Lower[day]), m.FormatValue(_series.Upper[day]));
        AutomationProperties.SetItemStatus(_plot, text);
    }

    private void UpdateSummary(UsageMetric m, string headline, string detail)
    {
        if (_plot is null)
        {
            return;
        }
        var l = Labels;
        AutomationProperties.SetName(_plot, string.Format(l.ChartSummaryFormat, m.Label, m.FormatValue(_series.Line[_series.Today]),
            Day(_series.Days), m.FormatValue(_series.Line[_series.Days]), m.FormatValue(m.Budget), $"{headline}. {detail}"));
        AutomationProperties.SetHelpText(_plot, l.DragHintText);
    }

    private void UpdateAccessibility(UsageMetric m)
    {
        var l = Labels;
        if (_handle is not null)
        {
            AutomationProperties.SetName(_handle, l.BudgetHandleName);
            AutomationProperties.SetHelpText(_handle, l.DragHintText);
        }
        if (_alerts is not null)
        {
            AutomationProperties.SetName(_alerts, l.AlertGroupText);
        }
        if (_legend is not null)
        {
            AutomationProperties.SetName(_legend, l.LegendText);
        }
    }

    private void UpdateTable(UsageMetric m)
    {
        if (_table is null)
        {
            return;
        }
        var l = Labels;
        var n = _series.Days;
        var headers = new string[n];
        var cells = new string[n][];
        for (var d = 1; d <= n; d++)
        {
            headers[d - 1] = Day(d) + (d > _series.Today ? l.TableProjectedSuffix : string.Empty);
            var range = d <= _series.Today ? l.TableMeasuredText : string.Format(l.TableRangeFormat, m.FormatValue(_series.Lower[d]), m.FormatValue(_series.Upper[d]));
            cells[d - 1] = [m.FormatValue(_series.Line[d]), range];
        }
        _table.Update(string.Format(l.TableCaptionFormat, m.Label, $"{Day(1)} to {Day(n)}"), headers, cells);
    }

    #endregion
}
