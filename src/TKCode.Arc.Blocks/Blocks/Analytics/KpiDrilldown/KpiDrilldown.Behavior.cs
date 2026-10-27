using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class KpiDrilldown
{
    private Grid? _header;
    private TextBlock? _title;
    private KpiCompareButton? _compare;
    private AnalyticsSegmentSwitch? _ranges;
    private KpiDrilldownStage? _stage;
    private KpiCardGrid? _grid;
    private TextBlock? _detailTitle;
    private KpiCloseButton? _close;
    private AnalyticsRollingNumber? _detailValue;
    private TextBlock? _detailPlainValue;
    private KpiDelta? _detailDelta;
    private Control? _versus;
    private TextBlock? _versusBefore;
    private AnalyticsRollingNumber? _versusValue;
    private TextBlock? _versusAfter;
    private Control? _body;
    private TextBlock? _description;
    private KpiTrendChart? _chart;
    private AnalyticsTextSwap? _legendCurrent;
    private Control? _legendPriorItem;
    private AnalyticsTextSwap? _legendPrior;
    private Control? _breakdown;
    private TextBlock? _breakdownTitle;
    private AnalyticsSegmentSwitch? _dimensions;
    private KpiBreakdownTable? _table;
    private AnalyticsAnnouncer? _announcer;

    private readonly List<KpiCard> _cards = new();
    private readonly List<KpiMetric> _observed = new();
    private INotifyCollectionChanged? _observedSource;
    private IReadOnlyList<KpiMetric> _metrics = [];
    private TopLevel? _escapeHost;
    private bool _keyboardMode;
    private bool _syncing;
    private bool _refreshQueued;
    private bool _detailShown;
    private int _columns = -1;
    private bool? _compact;

    static KpiDrilldown()
    {
        FocusableProperty.OverrideDefaultValue<KpiDrilldown>(false);
    }

    public KpiDrilldown()
    {
        Metrics.CollectionChanged += (_, _) => OnMetricsChanged();
        Dimensions.CollectionChanged += (_, _) => QueueRefresh();
        AddHandler(KeyDownEvent, (_, _) => _keyboardMode = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboardMode = false, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    #region Internals for tests

    internal KpiDrilldownStage? Stage => _stage;

    internal IReadOnlyList<KpiCard> Cards => _cards;

    internal KpiTrendChart? Chart => _chart;

    internal KpiBreakdownTable? Table => _table;

    internal KpiCompareButton? CompareButton => _compare;

    internal AnalyticsSegmentSwitch? RangeSwitch => _ranges;

    internal AnalyticsSegmentSwitch? DimensionSwitch => _dimensions;

    internal KpiCloseButton? CloseButton => _close;

    internal AnalyticsAnnouncer? Announcer => _announcer;

    internal AnalyticsRollingNumber? DetailValue => _detailValue;

    internal KpiDelta? DetailDelta => _detailDelta;

    internal AnalyticsTextSwap? LegendCurrent => _legendCurrent;

    internal AnalyticsTextSwap? LegendPrior => _legendPrior;

    internal string VersusText => _versusBefore?.Text + (_versusValue is { IsVisible: true } v ? v.FullText : string.Empty) + _versusAfter?.Text;

    internal int ColumnCount => _columns;

    #endregion

    #region Public methods

    /// <summary>Opens the detail view of the metric with <paramref name="key"/>, like choosing its card.</summary>
    public void Open(string key) => SetCurrentValue(SelectedKeyProperty, key);

    /// <summary>Folds the detail view back into its card, like Escape.</summary>
    public void Close() => SetCurrentValue(SelectedKeyProperty, null);

    /// <summary>Re-reads every metric (after changing series in place without assigning them again).</summary>
    public void Refresh() => ApplyData(animate: true);

    #endregion

    // Template -------------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _header = e.NameScope.Find<Grid>(HeaderPart);
        _title = e.NameScope.Find<TextBlock>(TitlePart);
        _compare = e.NameScope.Find<KpiCompareButton>(CompareButtonPart);
        _ranges = e.NameScope.Find<AnalyticsSegmentSwitch>(RangeSwitchPart);
        _stage = e.NameScope.Find<KpiDrilldownStage>(StagePart);
        _grid = e.NameScope.Find<KpiCardGrid>(GridPart);
        _detailTitle = e.NameScope.Find<TextBlock>(DetailTitlePart);
        _close = e.NameScope.Find<KpiCloseButton>(CloseButtonPart);
        _detailValue = e.NameScope.Find<AnalyticsRollingNumber>(DetailValuePart);
        _detailPlainValue = e.NameScope.Find<TextBlock>(DetailPlainValuePart);
        _detailDelta = e.NameScope.Find<KpiDelta>(DetailDeltaPart);
        _versus = e.NameScope.Find<Control>(VersusPart);
        _versusBefore = e.NameScope.Find<TextBlock>(VersusBeforePart);
        _versusValue = e.NameScope.Find<AnalyticsRollingNumber>(VersusValuePart);
        _versusAfter = e.NameScope.Find<TextBlock>(VersusAfterPart);
        _body = e.NameScope.Find<Control>(BodyPart);
        _description = e.NameScope.Find<TextBlock>(DescriptionPart);
        _chart = e.NameScope.Find<KpiTrendChart>(ChartPart);
        _legendCurrent = e.NameScope.Find<AnalyticsTextSwap>(LegendCurrentPart);
        _legendPriorItem = e.NameScope.Find<Control>(LegendPriorItemPart);
        _legendPrior = e.NameScope.Find<AnalyticsTextSwap>(LegendPriorPart);
        _breakdown = e.NameScope.Find<Control>(BreakdownPart);
        _breakdownTitle = e.NameScope.Find<TextBlock>(BreakdownTitlePart);
        _dimensions = e.NameScope.Find<AnalyticsSegmentSwitch>(DimensionSwitchPart);
        _table = e.NameScope.Find<KpiBreakdownTable>(TablePart);
        _announcer = e.NameScope.Find<AnalyticsAnnouncer>(AnnouncerPart);

        foreach (var swap in new[] { _legendCurrent, _legendPrior })
        {
            if (swap is not null)
            {
                swap.Swap = AnalyticsSwapStyle.Sequential;
            }
        }
        if (_compare is not null)
        {
            _compare.IsCheckedChanged += OnCompareToggled;
        }
        if (_ranges is not null)
        {
            _ranges.Look = AnalyticsSegmentLook.Compact;
            _ranges.SelectionChanged += OnRangeSwitch;
        }
        if (_dimensions is not null)
        {
            _dimensions.Look = AnalyticsSegmentLook.Compact;
            _dimensions.SelectionChanged += OnDimensionSwitch;
        }
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
        }
        if (_chart is not null)
        {
            _chart.CursorMoved += OnCursorMoved;
        }
        if (_stage is not null)
        {
            _stage.Closed += OnPanelClosed;
            var parts = new List<Visual>();
            if (_body is not null)
            {
                parts.Add(_body);
            }
            if (_versus is not null)
            {
                parts.Add(_versus);
            }
            _stage.BodyParts = parts;
        }

        _detailShown = false;
        ResolveMetrics();
        RebuildCards();
        SyncChrome();
        ApplyData(animate: false);
        if (Bounds.Width > 0)
        {
            UpdateLayoutState(Bounds.Width);
        }
        if (FindMetric(SelectedKey) is { } metric)
        {
            ShowDetail(metric, instant: true);
        }
    }

    private void Unhook()
    {
        if (_compare is not null)
        {
            _compare.IsCheckedChanged -= OnCompareToggled;
        }
        if (_ranges is not null)
        {
            _ranges.SelectionChanged -= OnRangeSwitch;
        }
        if (_dimensions is not null)
        {
            _dimensions.SelectionChanged -= OnDimensionSwitch;
        }
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        if (_chart is not null)
        {
            _chart.CursorMoved -= OnCursorMoved;
        }
        if (_stage is not null)
        {
            _stage.Closed -= OnPanelClosed;
            _stage.Reset();
        }
        foreach (var card in _cards)
        {
            card.Click -= OnCardClick;
            card.PartsReady -= OnCardPartsReady;
        }
        _cards.Clear();
        _columns = -1;
        _compact = null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (IsDetailOpen)
        {
            HookEscape();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        UnhookEscape();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            UpdateLayoutState(change.GetNewValue<Rect>().Width);
        }
        else if (change.Property == RangeDaysProperty)
        {
            SyncChrome();
            ApplyData(animate: true);
            RaiseEvent(new KpiRangeChangedEventArgs(RangeChangedEvent, change.GetOldValue<int>(), change.GetNewValue<int>()));
        }
        else if (change.Property == IsCompareEnabledProperty)
        {
            SyncChrome();
            _chart?.SetCompare(IsCompareEnabled, animate: true);
            PseudoClasses.Set(":compare-off", !IsCompareEnabled);
            RaiseEvent(new KpiCompareChangedEventArgs(CompareChangedEvent, IsCompareEnabled));
        }
        else if (change.Property == SelectedKeyProperty)
        {
            OnSelectedKeyChanged(change.GetOldValue<string?>(), change.GetNewValue<string?>());
        }
        else if (change.Property == SelectedDimensionKeyProperty)
        {
            SyncDimensions();
            ApplyBreakdown(animate: true);
            RaiseEvent(new KpiSelectionChangedEventArgs(DimensionChangedEvent, change.GetOldValue<string?>(), change.GetNewValue<string?>()));
        }
        else if (change.Property == MetricsSourceProperty)
        {
            OnMetricsChanged();
        }
        else if (change.Property == TitleProperty || change.Property == LabelsProperty || change.Property == RangesProperty)
        {
            SyncChrome();
            ApplyData(animate: false);
        }
        else if (change.Property == EndDateProperty || change.Property == DateFormatProperty)
        {
            ApplyData(animate: false);
        }
    }

    // Data -----------------------------------------------------------------------------------------------------------------

    private void OnMetricsChanged()
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceChanged;
            _observedSource = null;
        }
        if (MetricsSource is INotifyCollectionChanged observable)
        {
            _observedSource = observable;
            observable.CollectionChanged += OnSourceChanged;
        }
        ResolveMetrics();
        RebuildCards();
        ApplyData(animate: false);
        if (SelectedKey is { } key && FindMetric(key) is null && IsDetailOpen)
        {
            Close();
        }
    }

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnMetricsChanged();

    private void ResolveMetrics()
    {
        foreach (var metric in _observed)
        {
            metric.Changed -= OnMetricChanged;
        }
        _observed.Clear();
        var list = new List<KpiMetric>();
        if (MetricsSource is IEnumerable source)
        {
            foreach (var item in source)
            {
                if (item is KpiMetric metric)
                {
                    list.Add(metric);
                }
            }
        }
        else
        {
            list.AddRange(Metrics);
        }
        foreach (var metric in list)
        {
            metric.Changed += OnMetricChanged;
            _observed.Add(metric);
        }
        _metrics = list;
    }

    private void OnMetricChanged(object? sender, EventArgs e) => QueueRefresh();

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
            SyncDimensions();
            ApplyData(animate: true);
        }, DispatcherPriority.Background);
    }

    private KpiMetric? FindMetric(string? key)
    {
        if (key is null)
        {
            return null;
        }
        foreach (var metric in _metrics)
        {
            if (string.Equals(metric.Key, key, StringComparison.Ordinal))
            {
                return metric;
            }
        }
        return null;
    }

    private DateOnly End => EndDate ?? DateOnly.FromDateTime(DateTime.Today);

    private int Days => Math.Max(1, RangeDays);

    private KpiPeriod PeriodOf(KpiMetric metric) => KpiDrilldownMath.Period(metric.Values, Days, metric.Kind, End);

    private void RebuildCards()
    {
        if (_grid is null)
        {
            return;
        }
        while (_cards.Count > _metrics.Count)
        {
            var card = _cards[^1];
            card.Click -= OnCardClick;
            card.PartsReady -= OnCardPartsReady;
            _grid.Children.Remove(card);
            _cards.RemoveAt(_cards.Count - 1);
        }
        while (_cards.Count < _metrics.Count)
        {
            var card = new KpiCard { Index = _cards.Count, IsCompact = _compact == true };
            card.Click += OnCardClick;
            card.PartsReady += OnCardPartsReady;
            _cards.Add(card);
            _grid.Children.Add(card);
        }
        for (var i = 0; i < _cards.Count; i++)
        {
            _cards[i].Index = i;
        }
    }

    private void OnCardPartsReady(object? sender, EventArgs e)
    {
        if (sender is KpiCard card && card.Index < _metrics.Count)
        {
            ApplyCard(card, _metrics[card.Index], animate: false);
        }
    }

    /// <summary>Fills every card (and the open detail view) for the current range.</summary>
    private void ApplyData(bool animate)
    {
        for (var i = 0; i < _cards.Count && i < _metrics.Count; i++)
        {
            var card = _cards[i];
            ApplyCard(card, _metrics[i], animate && ReferenceEquals(card.Metric, _metrics[i]));
        }
        if (SelectedMetric is { } metric && _detailShown)
        {
            ApplyDetail(metric, animate);
        }
    }

    private void ApplyCard(KpiCard card, KpiMetric metric, bool animate)
    {
        var period = PeriodOf(metric);
        var sampled = new double[KpiDrilldownMath.SparklinePoints];
        KpiDrilldownMath.Resample(period.Current, sampled);
        KpiDrilldownMath.Normalize(sampled, sampled);
        card.Show(metric, period, sampled, animate, CardName(metric, period));
    }

    private string CardName(KpiMetric metric, KpiPeriod period)
    {
        var labels = Labels;
        return Format(labels.CardFormat, metric.Label, metric.FormatValue(period.Last), ChangeWords(metric, period.Change), Days);
    }

    /// <summary>"up 3.3%", "down 0.2 pts", "unchanged".</summary>
    private string ChangeWords(KpiMetric metric, double? change)
    {
        var labels = Labels;
        if (change is not { } value)
        {
            return labels.UnchangedText;
        }
        var format = metric.ChangeFormat with { Signed = false };
        var tone = AnalyticsFormat.Tone(value, metric.IsLowerBetter, format.Decimals);
        if (tone == AnalyticsTone.Flat)
        {
            return labels.UnchangedText;
        }
        return $"{(value > 0 ? labels.UpText : labels.DownText)} {format.Format(Math.Abs(value))}";
    }

    private static string Format(string pattern, params object?[] args) => string.Format(CultureInfo.CurrentCulture, pattern, args);

    // Detail ---------------------------------------------------------------------------------------------------------------

    private void ApplyDetail(KpiMetric metric, bool animate)
    {
        var period = PeriodOf(metric);
        var labels = Labels;
        var days = Days;
        if (_detailTitle is not null)
        {
            _detailTitle.Text = metric.Label;
        }
        var plain = metric.ValueFormatter is not null;
        if (_detailValue is not null)
        {
            _detailValue.IsVisible = !plain;
            _detailValue.IsInstant = !animate;
            _detailValue.Format = metric.NumberFormat;
            _detailValue.Amount = double.IsFinite(period.Last) ? period.Last : 0;
            _detailValue.IsInstant = false;
        }
        if (_detailPlainValue is not null)
        {
            _detailPlainValue.IsVisible = plain;
            _detailPlainValue.Text = plain ? metric.FormatValue(period.Last) : null;
        }
        _detailDelta?.Show(period.Change, metric.ChangeFormat, metric.IsLowerBetter, animate);
        if (_detailDelta is not null)
        {
            AutomationProperties.SetName(_detailDelta, $"{ChangeWords(metric, period.Change)} {Format(labels.AgainstFormat, days)}");
        }
        ApplyVersus(metric, period, days, animate);
        if (_description is not null)
        {
            _description.Text = metric.Description;
            _description.IsVisible = !string.IsNullOrEmpty(metric.Description);
        }
        if (_chart is not null)
        {
            _chart.SetData(period, metric.FormatValue, DateFormat, labels.PeriodEarlierFormat, IsCompareEnabled, animate);
            AutomationProperties.SetName(_chart, Format(labels.ChartFormat, metric.Label, days,
                metric.FormatValue(period.Current[0]), metric.FormatValue(period.Last)));
        }
        SetSwap(_legendCurrent, Format(labels.CurrentPeriodFormat, days), animate);
        SetSwap(_legendPrior, Format(labels.PriorPeriodFormat, days), animate);
        if (_legendPriorItem is not null)
        {
            _legendPriorItem.IsVisible = period.HasPrevious;
        }
        ApplyBreakdown(animate);
    }

    private void ApplyVersus(KpiMetric metric, KpiPeriod period, int days, bool animate)
    {
        if (_versus is null)
        {
            return;
        }
        _versus.IsVisible = double.IsFinite(period.PriorLast);
        var pattern = Labels.VersusFormat;
        var at = pattern.IndexOf("{0}", StringComparison.Ordinal);
        var rolls = metric.ValueFormatter is null && at >= 0 && _versusValue is not null;
        if (!rolls)
        {
            if (_versusBefore is not null)
            {
                _versusBefore.Text = Format(pattern, metric.FormatValue(period.PriorLast), days);
            }
            if (_versusValue is not null)
            {
                _versusValue.IsVisible = false;
            }
            if (_versusAfter is not null)
            {
                _versusAfter.Text = null;
            }
            return;
        }
        if (_versusBefore is not null)
        {
            _versusBefore.Text = Format(pattern[..at].Replace("{1}", "{0}"), days);
        }
        if (_versusAfter is not null)
        {
            _versusAfter.Text = Format(pattern[(at + 3)..].Replace("{1}", "{0}"), days);
        }
        var value = _versusValue!;
        value.IsVisible = true;
        value.IsInstant = !animate;
        value.Format = metric.NumberFormat;
        value.Amount = double.IsFinite(period.PriorLast) ? period.PriorLast : 0;
        value.IsInstant = false;
    }

    private static void SetSwap(AnalyticsTextSwap? swap, string text, bool animate)
    {
        if (swap is null)
        {
            return;
        }
        if (!animate)
        {
            swap.Swap = AnalyticsSwapStyle.Instant;
        }
        swap.Text = text;
        swap.Swap = AnalyticsSwapStyle.Sequential;
    }

    private KpiDimension? CurrentDimension
    {
        get
        {
            if (Dimensions.Count == 0)
            {
                return null;
            }
            foreach (var dimension in Dimensions)
            {
                if (string.Equals(dimension.Key, SelectedDimensionKey, StringComparison.Ordinal))
                {
                    return dimension;
                }
            }
            return Dimensions[0];
        }
    }

    private void ApplyBreakdown(bool animate)
    {
        if (SelectedMetric is not { } metric || _breakdown is null || _table is null)
        {
            return;
        }
        var dimension = CurrentDimension;
        _breakdown.IsVisible = dimension is not null;
        if (dimension is null)
        {
            return;
        }
        var labels = Labels;
        if (_breakdownTitle is not null)
        {
            _breakdownTitle.Text = labels.BreakdownText;
        }
        AutomationProperties.SetName(_breakdown, Format(labels.SectionFormat, metric.Label, dimension.Label));
        _table.SetHeadings(dimension.Label, labels.ShareText, labels.ValueText, labels.ChangeText, animate);
        _table.SetRows(KpiDrilldownMath.Breakdown(metric.BreakdownFor(dimension.Key), Days, metric.Kind), metric, animate);
    }

    // Chrome ---------------------------------------------------------------------------------------------------------------

    private void SyncChrome()
    {
        _syncing = true;
        try
        {
            var labels = Labels;
            if (_title is not null)
            {
                _title.Text = Title;
            }
            if (_compare is not null)
            {
                _compare.IsChecked = IsCompareEnabled;
                _compare.Content = labels.CompareText;
                AutomationProperties.SetName(_compare, labels.CompareText);
            }
            if (_ranges is not null)
            {
                var ranges = Ranges ?? [];
                var items = ranges.Select(d => Format(labels.RangeFormat, d)).ToArray();
                if (_ranges.Items is null || !_ranges.Items.SequenceEqual(items))
                {
                    _ranges.Items = items;
                    _ranges.AccessibleNames = ranges.Select(d => Format(labels.RangeSpokenFormat, d)).ToArray();
                }
                AutomationProperties.SetName(_ranges, labels.RangeGroupText);
                _ranges.SelectedIndex = IndexOf(ranges, RangeDays);
            }
            if (_close is not null)
            {
                AutomationProperties.SetName(_close, labels.CloseText);
            }
            PseudoClasses.Set(":compare-off", !IsCompareEnabled);
            SyncDimensions();
        }
        finally
        {
            _syncing = false;
        }
    }

    private static int IndexOf(IReadOnlyList<int> ranges, int days)
    {
        for (var i = 0; i < ranges.Count; i++)
        {
            if (ranges[i] == days)
            {
                return i;
            }
        }
        return -1;
    }

    private void SyncDimensions()
    {
        if (_dimensions is null)
        {
            return;
        }
        var wasSyncing = _syncing;
        _syncing = true;
        try
        {
            var items = Dimensions.Select(d => d.Label).ToArray();
            if (_dimensions.Items is null || !_dimensions.Items.SequenceEqual(items))
            {
                _dimensions.Items = items;
            }
            AutomationProperties.SetName(_dimensions, Labels.BreakdownGroupText);
            var current = CurrentDimension;
            _dimensions.SelectedIndex = current is null ? -1 : Dimensions.IndexOf(current);
        }
        finally
        {
            _syncing = wasSyncing;
        }
    }

    private void OnRangeSwitch(object? sender, AnalyticsSegmentChangedEventArgs e)
    {
        if (_syncing || e.NewIndex < 0 || Ranges is not { } ranges || e.NewIndex >= ranges.Count)
        {
            return;
        }
        SetCurrentValue(RangeDaysProperty, ranges[e.NewIndex]);
    }

    private void OnDimensionSwitch(object? sender, AnalyticsSegmentChangedEventArgs e)
    {
        if (_syncing || e.NewIndex < 0 || e.NewIndex >= Dimensions.Count)
        {
            return;
        }
        SetCurrentValue(SelectedDimensionKeyProperty, Dimensions[e.NewIndex].Key);
    }

    private void OnCompareToggled(object? sender, RoutedEventArgs e)
    {
        if (_syncing || _compare is null)
        {
            return;
        }
        SetCurrentValue(IsCompareEnabledProperty, _compare.IsChecked == true);
    }

    private void OnCursorMoved(int index, bool byKeyboard)
    {
        if (!byKeyboard || index < 0 || _chart is null || SelectedMetric is null)
        {
            return;
        }
        var content = _chart.ContentFor(_chart.CursorDay);
        var text = Format(Labels.CursorFormat, content.Title, content.Value);
        if (content.Rows is { Count: > 0 } rows)
        {
            text += ", " + rows[0].Label;
        }
        _announcer?.Announce(text);
    }

    // Layout ---------------------------------------------------------------------------------------------------------------

    private void UpdateLayoutState(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var columns = KpiDrilldownMath.Columns(width);
        var compact = KpiDrilldownMath.IsCompact(width);
        if (columns == _columns && compact == _compact)
        {
            return;
        }
        _columns = columns;
        _compact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(TwoColumnPseudoClass, columns == 2);
        if (_grid is not null)
        {
            _grid.Columns = columns;
        }
        foreach (var card in _cards)
        {
            card.IsCompact = compact;
        }
        if (_compare is not null)
        {
            _compare.IsIconOnly = compact;
        }
        if (_table is not null)
        {
            _table.IsCompact = compact;
        }
    }

    // Open / close ---------------------------------------------------------------------------------------------------------

    private void OnCardClick(object? sender, RoutedEventArgs e)
    {
        if (sender is KpiCard { Metric: { } metric })
        {
            Open(metric.Key);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnSelectedKeyChanged(string? oldKey, string? newKey)
    {
        var metric = FindMetric(newKey);
        if (metric is not null)
        {
            ShowDetail(metric, instant: false);
        }
        else
        {
            HideDetail();
        }
        RaiseEvent(new KpiSelectionChangedEventArgs(SelectedChangedEvent, oldKey, metric?.Key));
        if (SelectedCommand is { } command && command.CanExecute(metric))
        {
            command.Execute(metric);
        }
    }

    private void ShowDetail(KpiMetric metric, bool instant)
    {
        var switching = _detailShown && !ReferenceEquals(SelectedMetric, metric);
        SelectedMetric = metric;
        IsDetailOpen = true;
        PseudoClasses.Set(OpenPseudoClass, true);
        _detailShown = true;
        _chart?.ClearCursor();
        ApplyDetail(metric, animate: false);
        var index = IndexOfMetric(metric);
        if (_stage is not null && index >= 0 && index < _cards.Count)
        {
            _stage.Open(_cards[index], instant || switching);
        }
        foreach (var card in _cards)
        {
            card.IsTabStop = false;
            AutomationProperties.SetAccessibilityView(card, AccessibilityView.Raw);
        }
        if (_grid is not null)
        {
            _grid.IsHitTestVisible = false;
        }
        HookEscape();
        if (!instant)
        {
            var keyboard = _keyboardMode;
            Dispatcher.UIThread.Post(() =>
            {
                if (IsDetailOpen && _close is { IsEffectivelyVisible: true } close)
                {
                    close.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private int IndexOfMetric(KpiMetric metric)
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

    private void HideDetail()
    {
        if (!IsDetailOpen)
        {
            return;
        }
        var focusWasInside = IsFocusInside();
        var source = _stage?.Source;
        _chart?.ClearCursor();
        foreach (var card in _cards)
        {
            card.IsTabStop = true;
            card.ClearValue(AutomationProperties.AccessibilityViewProperty);
        }
        if (_grid is not null)
        {
            _grid.IsHitTestVisible = true;
        }
        UnhookEscape();
        PseudoClasses.Set(OpenPseudoClass, false);
        if (_stage is not null && _stage.Phase == KpiPanelPhase.Open)
        {
            _stage.Close();
        }
        else
        {
            OnPanelClosed(this, EventArgs.Empty);
        }
        if (focusWasInside && source is not null)
        {
            source.Focus(_keyboardMode ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
    }

    private bool IsFocusInside()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        return focused is null || ReferenceEquals(focused, this) || this.IsVisualAncestorOf(focused);
    }

    private void OnPanelClosed(object? sender, EventArgs e)
    {
        if (SelectedKey is not null)
        {
            return;
        }
        _detailShown = false;
        IsDetailOpen = false;
        SelectedMetric = null;
    }

    private void HookEscape()
    {
        if (_escapeHost is not null)
        {
            return;
        }
        _escapeHost = TopLevel.GetTopLevel(this);
        _escapeHost?.AddHandler(KeyDownEvent, OnEscape, RoutingStrategies.Bubble);
    }

    private void UnhookEscape()
    {
        _escapeHost?.RemoveHandler(KeyDownEvent, OnEscape);
        _escapeHost = null;
    }

    private void OnEscape(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None || SelectedKey is null)
        {
            return;
        }
        e.Handled = true;
        _keyboardMode = true;
        Close();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    private sealed class GroupPeer(KpiDrilldown owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => owner.Title;
    }
}
