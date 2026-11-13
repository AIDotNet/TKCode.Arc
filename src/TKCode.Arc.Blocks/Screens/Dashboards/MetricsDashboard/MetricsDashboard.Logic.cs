using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;

namespace TKCode.Arc;

[TemplatePart(HeaderPart, typeof(MetricsDashboardHeaderPanel))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(SubtitlePart, typeof(TextBlock))]
[TemplatePart(RangePart, typeof(AnalyticsSegmentSwitch))]
[TemplatePart(ExportPart, typeof(MetricsDashboardExportButton))]
[TemplatePart(TabsPart, typeof(MetricsDashboardTabStrip))]
[TemplatePart(LegendTextPart, typeof(TextBlock))]
[TemplatePart(PreviousLegendPart, typeof(Control))]
[TemplatePart(PreviousLegendTextPart, typeof(TextBlock))]
[TemplatePart(ComparePart, typeof(MetricsDashboardCompareBox))]
[TemplatePart(PlotPart, typeof(MetricsDashboardPlot))]
[TemplatePart(ListsPart, typeof(MetricsDashboardSplitPanel))]
[TemplatePart(PrimarySectionPart, typeof(Control))]
[TemplatePart(PrimaryTitlePart, typeof(TextBlock))]
[TemplatePart(PrimaryHeaderPart, typeof(TextBlock))]
[TemplatePart(PrimaryRowsPart, typeof(StackPanel))]
[TemplatePart(SecondarySectionPart, typeof(Control))]
[TemplatePart(SecondaryTitlePart, typeof(TextBlock))]
[TemplatePart(SecondaryHeaderPart, typeof(TextBlock))]
[TemplatePart(SecondaryRowsPart, typeof(StackPanel))]
[TemplatePart(AnnouncerPart, typeof(AnalyticsAnnouncer))]
[PseudoClasses(MediumPseudoClass, SmallPseudoClass, ComparePseudoClass, ExportedPseudoClass)]
public partial class MetricsDashboard
{
    #region Template parts and internal properties

    internal const string HeaderPart = "PART_Header";
    internal const string TitlePart = "PART_Title";
    internal const string SubtitlePart = "PART_Subtitle";
    internal const string RangePart = "PART_Range";
    internal const string ExportPart = "PART_Export";
    internal const string TabsPart = "PART_Tabs";
    internal const string LegendTextPart = "PART_LegendText";
    internal const string PreviousLegendPart = "PART_PreviousLegend";
    internal const string PreviousLegendTextPart = "PART_PreviousLegendText";
    internal const string ComparePart = "PART_Compare";
    internal const string PlotPart = "PART_Plot";
    internal const string ListsPart = "PART_Lists";
    internal const string PrimarySectionPart = "PART_PrimarySection";
    internal const string PrimaryTitlePart = "PART_PrimaryTitle";
    internal const string PrimaryHeaderPart = "PART_PrimaryHeader";
    internal const string PrimaryRowsPart = "PART_PrimaryRows";
    internal const string SecondarySectionPart = "PART_SecondarySection";
    internal const string SecondaryTitlePart = "PART_SecondaryTitle";
    internal const string SecondaryHeaderPart = "PART_SecondaryHeader";
    internal const string SecondaryRowsPart = "PART_SecondaryRows";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string MediumPseudoClass = ":md";
    internal const string SmallPseudoClass = ":sm";
    internal const string ComparePseudoClass = ":compare";
    internal const string ExportedPseudoClass = ":exported";

    internal static readonly StyledProperty<double> MediumWidthProperty =
        AvaloniaProperty.Register<MetricsDashboard, double>(nameof(MediumWidth), 640);

    internal static readonly StyledProperty<double> SmallWidthProperty =
        AvaloniaProperty.Register<MetricsDashboard, double>(nameof(SmallWidth), 460);

    internal static readonly StyledProperty<Thickness> TabPaddingProperty =
        AvaloniaProperty.Register<MetricsDashboard, Thickness>(nameof(TabPadding), new Thickness(16, 14));

    internal static readonly StyledProperty<Thickness> FirstTabPaddingProperty =
        AvaloniaProperty.Register<MetricsDashboard, Thickness>(nameof(FirstTabPadding), new Thickness(20, 14, 16, 14));

    internal static readonly StyledProperty<Thickness> CompactTabPaddingProperty =
        AvaloniaProperty.Register<MetricsDashboard, Thickness>(nameof(CompactTabPadding), new Thickness(16, 12));

    internal static readonly StyledProperty<double> TabValueSizeProperty =
        AvaloniaProperty.Register<MetricsDashboard, double>(nameof(TabValueSize), 22);

    internal static readonly StyledProperty<double> SmallTabValueSizeProperty =
        AvaloniaProperty.Register<MetricsDashboard, double>(nameof(SmallTabValueSize), 18);

    internal double MediumWidth { get => GetValue(MediumWidthProperty); set => SetValue(MediumWidthProperty, value); }

    internal double SmallWidth { get => GetValue(SmallWidthProperty); set => SetValue(SmallWidthProperty, value); }

    internal Thickness TabPadding { get => GetValue(TabPaddingProperty); set => SetValue(TabPaddingProperty, value); }

    internal Thickness FirstTabPadding { get => GetValue(FirstTabPaddingProperty); set => SetValue(FirstTabPaddingProperty, value); }

    internal Thickness CompactTabPadding { get => GetValue(CompactTabPaddingProperty); set => SetValue(CompactTabPaddingProperty, value); }

    internal double TabValueSize { get => GetValue(TabValueSizeProperty); set => SetValue(TabValueSizeProperty, value); }

    internal double SmallTabValueSize { get => GetValue(SmallTabValueSizeProperty); set => SetValue(SmallTabValueSizeProperty, value); }

    #endregion

    private readonly List<MetricsDashboardMetric> _metrics = new();
    private readonly List<MetricsDashboardItem> _primary = new();
    private readonly List<MetricsDashboardItem> _secondary = new();
    private readonly HashSet<AvaloniaObject> _watched = new();
    private readonly Dictionary<string, INotifyCollectionChanged> _observed = new();
    private MetricsDashboardSeries[] _series = Array.Empty<MetricsDashboardSeries>();

    private MetricsDashboardHeaderPanel? _header;
    private TextBlock? _title;
    private TextBlock? _subtitle;
    private AnalyticsSegmentSwitch? _range;
    private MetricsDashboardExportButton? _export;
    private MetricsDashboardTabStrip? _tabs;
    private TextBlock? _legendText;
    private Control? _previousLegend;
    private TextBlock? _previousLegendText;
    private MetricsDashboardCompareBox? _compare;
    private MetricsDashboardPlot? _plot;
    private MetricsDashboardSplitPanel? _lists;
    private Control? _primarySection;
    private TextBlock? _primaryTitle;
    private TextBlock? _primaryHeader;
    private StackPanel? _primaryRows;
    private Control? _secondarySection;
    private TextBlock? _secondaryTitle;
    private TextBlock? _secondaryHeader;
    private StackPanel? _secondaryRows;
    private AnalyticsAnnouncer? _announcer;
    private DispatcherTimer? _exportTimer;
    private bool _syncingCompare;
    private bool _syncingRange;

    static MetricsDashboard()
    {
        FocusableProperty.OverrideDefaultValue<MetricsDashboard>(false);
    }

    /// <summary>Creates the block.</summary>
    public MetricsDashboard()
    {
        Metrics.CollectionChanged += (_, _) => OnDataChanged(nameof(MetricsSource));
        PrimaryItems.CollectionChanged += (_, _) => OnDataChanged(nameof(PrimaryItemsSource));
        SecondaryItems.CollectionChanged += (_, _) => OnDataChanged(nameof(SecondaryItemsSource));
        Ranges.CollectionChanged += (_, _) =>
        {
            UpdateRangeItems();
            Refresh(false);
        };
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    /// <summary>The metrics shown now (from <see cref="MetricsSource"/> or <see cref="Metrics"/>), in order.</summary>
    public IReadOnlyList<MetricsDashboardMetric> EffectiveMetrics => _metrics;

    /// <summary>The selected metric, or null.</summary>
    public MetricsDashboardMetric? SelectedMetric =>
        SelectedMetricIndex >= 0 && SelectedMetricIndex < _metrics.Count ? _metrics[SelectedMetricIndex] : null;

    internal MetricsDashboardPlot? Plot => _plot;

    internal MetricsDashboardTabStrip? TabStrip => _tabs;

    internal AnalyticsSegmentSwitch? RangeSwitch => _range;

    internal MetricsDashboardExportButton? ExportButton => _export;

    internal MetricsDashboardCompareBox? CompareBox => _compare;

    internal MetricsDashboardHeaderPanel? Header => _header;

    internal AnalyticsAnnouncer? Announcer => _announcer;

    internal IReadOnlyList<AnalyticsRankedRow> PrimaryRows => _primaryRows?.Children.OfType<AnalyticsRankedRow>().ToList() ?? [];

    internal IReadOnlyList<AnalyticsRankedRow> SecondaryRows => _secondaryRows?.Children.OfType<AnalyticsRankedRow>().ToList() ?? [];

    internal IReadOnlyList<MetricsDashboardTab> Tabs => _tabs?.Tabs.ToList() ?? [];

    /// <summary>The series of the selected metric for the selected range (null without metrics).</summary>
    internal MetricsDashboardSeries? SelectedSeries =>
        SelectedMetricIndex >= 0 && SelectedMetricIndex < _series.Length ? _series[SelectedMetricIndex] : null;

    #region Public methods

    /// <summary>Inspects a point of the chart as the pointer or the arrow keys do (−1 clears).</summary>
    public void InspectPoint(int index) => _plot?.Inspect(index);

    /// <summary>Ends the inspection.</summary>
    public void ClearInspection() => _plot?.Inspect(-1);

    /// <summary>
    /// Does what the Export button does: runs <see cref="ExportCommand"/> (when it can execute), raises
    /// <see cref="ExportRequested"/> and shows the confirmation for <see cref="ExportConfirmDuration"/>. Returns false when
    /// the command refused.
    /// </summary>
    public bool Export()
    {
        var index = SelectedMetricIndex;
        var end = EffectiveEndDate;
        var days = Math.Max(1, SelectedRange);
        var request = new MetricsDashboardExportRequest(days, end.AddDays(-(days - 1)), end, SelectedMetric,
            SelectedMetric is null ? -1 : index, IsCompareEnabled);
        var command = ExportCommand;
        if (command is not null)
        {
            if (!command.CanExecute(request))
            {
                return false;
            }
            command.Execute(request);
        }
        RaiseEvent(new MetricsDashboardExportEventArgs(ExportRequestedEvent, this, request));
        ShowExportConfirmation();
        return true;
    }

    #endregion

    private DateOnly EffectiveEndDate => EndDate ?? DateOnly.FromDateTime(DateTime.Today);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Detach();
        var scope = e.NameScope;
        _header = scope.Find<MetricsDashboardHeaderPanel>(HeaderPart);
        _title = scope.Find<TextBlock>(TitlePart);
        _subtitle = scope.Find<TextBlock>(SubtitlePart);
        _range = scope.Find<AnalyticsSegmentSwitch>(RangePart);
        _export = scope.Find<MetricsDashboardExportButton>(ExportPart);
        _tabs = scope.Find<MetricsDashboardTabStrip>(TabsPart);
        _legendText = scope.Find<TextBlock>(LegendTextPart);
        _previousLegend = scope.Find<Control>(PreviousLegendPart);
        _previousLegendText = scope.Find<TextBlock>(PreviousLegendTextPart);
        _compare = scope.Find<MetricsDashboardCompareBox>(ComparePart);
        _plot = scope.Find<MetricsDashboardPlot>(PlotPart);
        _lists = scope.Find<MetricsDashboardSplitPanel>(ListsPart);
        _primarySection = scope.Find<Control>(PrimarySectionPart);
        _primaryTitle = scope.Find<TextBlock>(PrimaryTitlePart);
        _primaryHeader = scope.Find<TextBlock>(PrimaryHeaderPart);
        _primaryRows = scope.Find<StackPanel>(PrimaryRowsPart);
        _secondarySection = scope.Find<Control>(SecondarySectionPart);
        _secondaryTitle = scope.Find<TextBlock>(SecondaryTitlePart);
        _secondaryHeader = scope.Find<TextBlock>(SecondaryHeaderPart);
        _secondaryRows = scope.Find<StackPanel>(SecondaryRowsPart);
        _announcer = scope.Find<AnalyticsAnnouncer>(AnnouncerPart);

        if (_range is not null)
        {
            _range.Look = AnalyticsSegmentLook.Small;
            _range.SelectionChanged += OnRangeSwitched;
        }
        if (_export is not null)
        {
            _export.Click += OnExportClick;
        }
        if (_compare is not null)
        {
            _compare.IsCheckedChanged += OnCompareToggled;
        }
        if (_plot is not null)
        {
            _plot.InspectionChanged += OnInspectionChanged;
        }

        UpdateTexts();
        UpdateRangeItems();
        RebuildMetrics();
        RebuildLists();
        SyncCompare(false);
        ApplySize(Bounds.Width);
        Refresh(false);
    }

    private void Detach()
    {
        if (_range is not null)
        {
            _range.SelectionChanged -= OnRangeSwitched;
        }
        if (_export is not null)
        {
            _export.Click -= OnExportClick;
        }
        if (_compare is not null)
        {
            _compare.IsCheckedChanged -= OnCompareToggled;
        }
        if (_plot is not null)
        {
            _plot.InspectionChanged -= OnInspectionChanged;
        }
        if (_tabs is not null)
        {
            foreach (var tab in _tabs.Tabs.ToList())
            {
                tab.Click -= OnTabClick;
                tab.KeyDown -= OnTabKeyDown;
                _tabs.Children.Remove(tab);
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ObserveSources();
        WatchItems();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var observed in _observed.Values)
        {
            observed.CollectionChanged -= OnSourceCollectionChanged;
        }
        _observed.Clear();
        foreach (var item in _watched)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
        _watched.Clear();
        _exportTimer?.Stop();
        if (IsExportConfirmed)
        {
            SetExportConfirmed(false);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        ApplySize(e.NewSize.Width);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == SelectedRangeProperty)
        {
            SyncRangeSwitch();
            Refresh(true);
            RaiseEvent(new MetricsDashboardRangeChangedEventArgs(RangeChangedEvent, this, change.GetOldValue<int>(), change.GetNewValue<int>()));
        }
        else if (property == SelectedMetricIndexProperty)
        {
            Refresh(true, metricOnly: true);
            RaiseEvent(new MetricsDashboardMetricChangedEventArgs(SelectedMetricChangedEvent, this, change.GetOldValue<int>(),
                change.GetNewValue<int>(), SelectedMetric));
        }
        else if (property == IsCompareEnabledProperty)
        {
            SyncCompare(true);
            RaiseEvent(new MetricsDashboardCompareChangedEventArgs(CompareChangedEvent, this, IsCompareEnabled));
        }
        else if (property == MetricsSourceProperty || property == PrimaryItemsSourceProperty || property == SecondaryItemsSourceProperty)
        {
            ObserveSources();
            OnDataChanged(property.Name);
        }
        else if (property == WeeklyThresholdProperty || property == EndDateProperty || property == DateFormatProperty ||
                 property == WeekTitleFormatProperty || property == PartialWeekTitleFormatProperty || property == PreviousValueFormatProperty)
        {
            Refresh(false);
        }
        else if (property == RangeLabelFormatProperty || property == RangeNameFormatProperty)
        {
            UpdateRangeItems();
        }
        else if (property == ListValueFormatterProperty)
        {
            RebuildLists();
            Refresh(false);
        }
        else if (property == MediumWidthProperty || property == SmallWidthProperty || property == TabPaddingProperty ||
                 property == FirstTabPaddingProperty || property == CompactTabPaddingProperty || property == TabValueSizeProperty ||
                 property == SmallTabValueSizeProperty)
        {
            ApplySize(Bounds.Width, force: true);
        }
        else if (property.OwnerType == typeof(MetricsDashboard) && property != ActiveIndexProperty && property != LayoutSizeProperty &&
                 property != IsExportConfirmedProperty && property != ExportCommandProperty && property != ExportConfirmDurationProperty)
        {
            UpdateTexts();
            UpdateChartTexts();
            UpdateTabNames();
        }
    }

    #region Data

    private void OnDataChanged(string sourceName)
    {
        if (sourceName == nameof(MetricsSource))
        {
            RebuildMetrics();
        }
        else
        {
            RebuildLists();
        }
        WatchItems();
        Refresh(false);
    }

    private void ObserveSources()
    {
        foreach (var observed in _observed.Values)
        {
            observed.CollectionChanged -= OnSourceCollectionChanged;
        }
        _observed.Clear();
        if (VisualRoot is null)
        {
            return;
        }
        Observe(nameof(MetricsSource), MetricsSource);
        Observe(nameof(PrimaryItemsSource), PrimaryItemsSource);
        Observe(nameof(SecondaryItemsSource), SecondaryItemsSource);

        void Observe(string name, IEnumerable? source)
        {
            if (source is INotifyCollectionChanged notify)
            {
                notify.CollectionChanged += OnSourceCollectionChanged;
                _observed[name] = notify;
            }
        }
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var (name, observed) in _observed)
        {
            if (ReferenceEquals(observed, sender))
            {
                OnDataChanged(name);
                return;
            }
        }
    }

    private void WatchItems()
    {
        var current = new HashSet<AvaloniaObject>();
        if (VisualRoot is not null)
        {
            current.UnionWith(_metrics);
            current.UnionWith(_primary);
            current.UnionWith(_secondary);
        }
        foreach (var gone in _watched.Where(w => !current.Contains(w)).ToArray())
        {
            gone.PropertyChanged -= OnItemPropertyChanged;
            _watched.Remove(gone);
        }
        foreach (var item in current)
        {
            if (_watched.Add(item))
            {
                item.PropertyChanged += OnItemPropertyChanged;
            }
        }
    }

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is MetricsDashboardItem && e.Property == MetricsDashboardItem.LabelProperty)
        {
            RebuildLists();
        }
        Refresh(true);
    }

    private static List<T> Collect<T>(IEnumerable? source, IEnumerable<T> fallback)
    {
        var list = new List<T>();
        foreach (var item in source ?? fallback)
        {
            if (item is T typed && !list.Contains(typed))
            {
                list.Add(typed);
            }
        }
        return list;
    }

    private void RebuildMetrics()
    {
        _metrics.Clear();
        _metrics.AddRange(Collect(MetricsSource, Metrics));
        if (_tabs is null)
        {
            return;
        }
        var tabs = _tabs.Tabs.ToList();
        for (var i = tabs.Count; i < _metrics.Count; i++)
        {
            var tab = new MetricsDashboardTab { Index = i };
            tab.Tile.Delta.TextWeight = Avalonia.Media.FontWeight.Normal;
            AutomationProperties.SetAccessibilityView(tab.Tile, AccessibilityView.Raw);
            tab.Click += OnTabClick;
            tab.KeyDown += OnTabKeyDown;
            tab.SelectRequested = index => SetCurrentValue(SelectedMetricIndexProperty, index);
            _tabs.Children.Add(tab);
        }
        for (var i = tabs.Count - 1; i >= _metrics.Count; i--)
        {
            tabs[i].Click -= OnTabClick;
            tabs[i].KeyDown -= OnTabKeyDown;
            _tabs.Children.Remove(tabs[i]);
        }
        ApplySize(Bounds.Width, force: true);
    }

    private void RebuildLists()
    {
        _primary.Clear();
        _primary.AddRange(Collect(PrimaryItemsSource, PrimaryItems));
        _secondary.Clear();
        _secondary.AddRange(Collect(SecondaryItemsSource, SecondaryItems));
        Fill(_primaryRows, _primary);
        Fill(_secondaryRows, _secondary);
        if (_primarySection is not null)
        {
            _primarySection.IsVisible = _primary.Count > 0;
        }
        if (_secondarySection is not null)
        {
            _secondarySection.IsVisible = _secondary.Count > 0;
        }

        void Fill(StackPanel? panel, List<MetricsDashboardItem> items)
        {
            if (panel is null)
            {
                return;
            }
            while (panel.Children.Count > items.Count)
            {
                panel.Children.RemoveAt(panel.Children.Count - 1);
            }
            while (panel.Children.Count < items.Count)
            {
                panel.Children.Add(new AnalyticsRankedRow());
            }
            for (var i = 0; i < items.Count; i++)
            {
                var row = (AnalyticsRankedRow)panel.Children[i];
                row.Label = items[i].Label;
                row.ValueText.Formatter = ListValueFormatter;
            }
        }
    }

    #endregion

    #region Refresh

    private int RangeIndex => Ranges.IndexOf(SelectedRange);

    /// <summary>
    /// Recomputes every series for the selected range and pushes the numbers out: tabs count to their totals, list values
    /// count, the chart cross-fades (when <paramref name="animate"/>). A metric switch only redraws the chart.
    /// </summary>
    private void Refresh(bool animate, bool metricOnly = false)
    {
        var days = Math.Max(1, SelectedRange);
        var end = EffectiveEndDate;
        if (!metricOnly || _series.Length != _metrics.Count)
        {
            _series = new MetricsDashboardSeries[_metrics.Count];
            for (var i = 0; i < _metrics.Count; i++)
            {
                var m = _metrics[i];
                _series[i] = MetricsDashboardEngine.Build(m.Kind, m.Daily, m.Numerators, m.Denominators, days, end, WeeklyThreshold);
            }
            UpdateTabs();
            UpdateLists();
        }
        UpdateSelection();
        UpdateTexts();
        if (_plot is not null && SelectedSeries is { } series && SelectedMetric is { } metric)
        {
            _plot.SetScene(BuildScene(metric, series), animate);
        }
        UpdateChartTexts();
    }

    private MetricsDashboardScene BuildScene(MetricsDashboardMetric metric, MetricsDashboardSeries series)
    {
        var kind = metric.Kind;
        var axis = metric.AxisFormatter;
        var step = series.Ticks.Step;
        var axisLabels = series.Ticks.Ticks.Select(v => axis?.Invoke(v) ?? MetricsDashboardEngine.FormatAxis(kind, v, step)).ToArray();
        var pattern = DateFormat;
        var xLabels = series.Starts.Select(d => AnalyticsFormat.Date(d, pattern)).ToArray();
        var formatter = metric.ValueFormatter;
        Func<double, string> value = v => formatter?.Invoke(v) ?? MetricsDashboardEngine.FormatValue(kind, v);
        var weekFormat = WeekTitleFormat;
        var partialFormat = PartialWeekTitleFormat;
        Func<int, string> title = i =>
        {
            var date = AnalyticsFormat.Date(series.Starts[i], pattern);
            if (!series.IsWeekly)
            {
                return date;
            }
            return series.Spans[i] < 7 ? string.Format(partialFormat, date, series.Spans[i]) : string.Format(weekFormat, date);
        };
        return new MetricsDashboardScene(series, axisLabels, xLabels, title, value, PreviousValueFormat);
    }

    private void UpdateTabs()
    {
        if (_tabs is null)
        {
            return;
        }
        var tabs = _tabs.Tabs.ToList();
        for (var i = 0; i < tabs.Count && i < _metrics.Count; i++)
        {
            var metric = _metrics[i];
            var series = _series[i];
            var tile = tabs[i].Tile;
            var formatter = metric.ValueFormatter;
            var kind = metric.Kind;
            tile.Label = metric.Label;
            tile.Formatter = formatter ?? (v => MetricsDashboardEngine.FormatValue(kind, v));
            tile.LowerIsBetter = !metric.HigherIsBetter;
            tile.ChangeFormat = kind == MetricsDashboardMetricKind.Ratio ? AnalyticsNumberFormat.Points(1) : AnalyticsNumberFormat.Percent(1);
            tile.Value = series.Total;
            tile.Change = series.Change;
        }
        ApplyDeltaFormatters();
        UpdateTabNames();
    }

    /// <summary>The tile rewrites its change text on every property change; a metric's own delta formatter goes on top.</summary>
    private void ApplyDeltaFormatters()
    {
        if (_tabs is null)
        {
            return;
        }
        foreach (var tab in _tabs.Tabs)
        {
            if (tab.Index < _metrics.Count && tab.Index < _series.Length && _series[tab.Index].Change is { } change &&
                _metrics[tab.Index].DeltaFormatter is { } formatter)
            {
                tab.Tile.Delta.Text = formatter(Math.Abs(change));
            }
        }
    }

    private void UpdateTabNames()
    {
        if (_tabs is null)
        {
            return;
        }
        foreach (var tab in _tabs.Tabs)
        {
            var tile = tab.Tile;
            var name = $"{tile.Label}, {tile.CountingValue.FinalText}";
            if (tile.Change is not null)
            {
                name += $", {tile.Delta.SpokenText} {ChangeContextText}";
            }
            AutomationProperties.SetName(tab, name);
        }
        if (_tabs is not null)
        {
            AutomationProperties.SetName(_tabs, MetricGroupName);
        }
    }

    private void UpdateLists()
    {
        var rangeIndex = RangeIndex;
        Push(_primaryRows, _primary);
        Push(_secondaryRows, _secondary);

        void Push(StackPanel? panel, List<MetricsDashboardItem> items)
        {
            if (panel is null)
            {
                return;
            }
            var first = items.Count > 0 ? items[0].ValueAt(rangeIndex) : 0;
            for (var i = 0; i < items.Count && i < panel.Children.Count; i++)
            {
                var row = (AnalyticsRankedRow)panel.Children[i];
                var value = items[i].ValueAt(rangeIndex);
                row.Value = value;
                // Arc: the share of the first row (the first row is always full), not of the total.
                row.Share = first > 0 ? Math.Clamp(value / first, 0, 1) : 0;
                var text = ListValueFormatter?.Invoke(value) ?? AnalyticsFormat.Number(value);
                AutomationProperties.SetName(row, $"{items[i].Label}, {text}");
            }
        }
    }

    private void UpdateSelection()
    {
        if (_tabs is null)
        {
            return;
        }
        var selected = SelectedMetricIndex;
        _tabs.SelectedIndex = selected;
        foreach (var tab in _tabs.Tabs)
        {
            tab.IsSelected = tab.Index == selected;
            // One tab stop for the whole strip: the selected tab (the first one when nothing is selected).
            tab.IsTabStop = tab.Index == selected || ((selected < 0 || selected >= _metrics.Count) && tab.Index == 0);
        }
    }

    private void UpdateTexts()
    {
        if (_title is not null)
        {
            _title.Text = Title;
            _title.IsVisible = !string.IsNullOrEmpty(Title);
        }
        if (_subtitle is not null)
        {
            var text = Subtitle ?? (string.IsNullOrEmpty(Source)
                ? string.Format(SubtitleRangeFormat, SelectedRange)
                : string.Format(SubtitleFormat, Source, SelectedRange));
            _subtitle.Text = text;
            _subtitle.IsVisible = !string.IsNullOrEmpty(text);
        }
        if (_range is not null)
        {
            AutomationProperties.SetName(_range, RangeGroupName);
        }
        if (_export is not null)
        {
            _export.Text = ExportText;
            _export.ConfirmedText = ExportedText;
            _export.IsVisible = IsExportVisible;
        }
        if (_compare is not null)
        {
            _compare.Content = CompareText;
            _compare.IsVisible = IsCompareVisible;
            AutomationProperties.SetName(_compare, CompareText);
        }
        if (_previousLegendText is not null)
        {
            _previousLegendText.Text = PreviousLegendText;
        }
        SetListHeader(_primaryTitle, _primaryHeader, _primarySection, PrimaryListTitle);
        SetListHeader(_secondaryTitle, _secondaryHeader, _secondarySection, SecondaryListTitle);
        AutomationProperties.SetName(this, Title);

        void SetListHeader(TextBlock? title, TextBlock? header, Control? section, string? text)
        {
            if (title is not null)
            {
                title.Text = text;
            }
            if (header is not null)
            {
                header.Text = ListValueHeader;
            }
            if (section is not null)
            {
                AutomationProperties.SetName(section, text);
            }
        }
    }

    private void UpdateChartTexts()
    {
        var metric = SelectedMetric;
        if (_legendText is not null)
        {
            _legendText.Text = metric?.Label;
        }
        if (_plot is not null)
        {
            AutomationProperties.SetName(_plot, metric is null ? null : string.Format(ChartNameFormat, metric.Label, SelectedRange));
            AutomationProperties.SetHelpText(_plot, ChartHelpText);
        }
    }

    private void UpdateRangeItems()
    {
        if (_range is null)
        {
            return;
        }
        _syncingRange = true;
        _range.Items = Ranges.Select(d => string.Format(RangeLabelFormat, d)).ToArray();
        _range.AccessibleNames = Ranges.Select(d => string.Format(RangeNameFormat, d)).ToArray();
        _syncingRange = false;
        SyncRangeSwitch();
    }

    private void SyncRangeSwitch()
    {
        if (_range is null)
        {
            return;
        }
        _syncingRange = true;
        _range.SelectedIndex = RangeIndex;
        _syncingRange = false;
    }

    private void SyncCompare(bool animate)
    {
        PseudoClasses.Set(ComparePseudoClass, IsCompareEnabled);
        if (_compare is not null && _compare.IsChecked != IsCompareEnabled)
        {
            _syncingCompare = true;
            _compare.IsChecked = IsCompareEnabled;
            _syncingCompare = false;
        }
        if (_previousLegend is not null)
        {
            _previousLegend.IsVisible = IsCompareEnabled;
        }
        _plot?.SetCompare(IsCompareEnabled, animate);
        if (_plot is not null && ActiveIndex >= 0)
        {
            _announcer?.Announce(_plot.DescribePoint(ActiveIndex));
        }
    }

    #endregion

    #region Layout size

    private void ApplySize(double width, bool force = false)
    {
        if (width <= 0)
        {
            width = double.PositiveInfinity;
        }
        var size = width < SmallWidth ? MetricsDashboardSize.Small : width < MediumWidth ? MetricsDashboardSize.Medium : MetricsDashboardSize.Large;
        if (size == LayoutSize && !force && _tabs is not null && _tabs.Columns == (size == MetricsDashboardSize.Large ? 4 : 2))
        {
            return;
        }
        LayoutSize = size;
        var large = size == MetricsDashboardSize.Large;
        var small = size == MetricsDashboardSize.Small;
        PseudoClasses.Set(MediumPseudoClass, size == MetricsDashboardSize.Medium);
        PseudoClasses.Set(SmallPseudoClass, small);
        if (_tabs is not null)
        {
            _tabs.Columns = large ? 4 : 2;
            foreach (var tab in _tabs.Tabs)
            {
                tab.Padding = !large ? CompactTabPadding : tab.Index == 0 ? FirstTabPadding : TabPadding;
                tab.Tile.ValueFontSize = small ? SmallTabValueSize : TabValueSize;
            }
            ApplyDeltaFormatters();
        }
        if (_lists is not null)
        {
            _lists.IsStacked = !large;
        }
        if (_header is not null)
        {
            _header.StretchActions = small;
        }
        if (_range is not null)
        {
            _range.IsStretched = small;
            _range.HorizontalAlignment = small ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        }
        if (_plot is not null)
        {
            _plot.MaxLabels = MetricsDashboardEngine.MaxLabels(size);
        }
    }

    #endregion

    #region Interaction

    private void OnRangeSwitched(object? sender, AnalyticsSegmentChangedEventArgs e)
    {
        if (_syncingRange || e.NewIndex < 0 || e.NewIndex >= Ranges.Count)
        {
            return;
        }
        SetCurrentValue(SelectedRangeProperty, Ranges[e.NewIndex]);
    }

    private void OnTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MetricsDashboardTab tab)
        {
            SetCurrentValue(SelectedMetricIndexProperty, tab.Index);
        }
    }

    private void OnTabKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not MetricsDashboardTab tab || _tabs is null)
        {
            return;
        }
        var count = _metrics.Count;
        int? next = e.Key switch
        {
            Key.Left => (tab.Index - 1 + count) % count,
            Key.Right => (tab.Index + 1) % count,
            Key.Home => 0,
            Key.End => count - 1,
            _ => null
        };
        if (next is not { } index || count == 0)
        {
            return;
        }
        e.Handled = true;
        SetCurrentValue(SelectedMetricIndexProperty, index);
        _tabs.Tabs.FirstOrDefault(t => t.Index == index)?.Focus(NavigationMethod.Directional);
    }

    private void OnCompareToggled(object? sender, RoutedEventArgs e)
    {
        if (_syncingCompare || _compare is null)
        {
            return;
        }
        SetCurrentValue(IsCompareEnabledProperty, _compare.IsChecked == true);
    }

    private void OnInspectionChanged(int index, bool byKeyboard)
    {
        ActiveIndex = index;
        if (index < 0 || SelectedSeries is not { } series || index >= series.Count)
        {
            return;
        }
        double? previous = IsCompareEnabled && series.Previous is { } before ? before[index] : null;
        RaiseEvent(new MetricsDashboardPointEventArgs(PointInspectedEvent, this, index, series.Starts[index], series.Spans[index],
            series.Values[index], previous));
        _announcer?.Announce(_plot?.DescribePoint(index));
    }

    private void OnExportClick(object? sender, RoutedEventArgs e) => Export();

    private void ShowExportConfirmation()
    {
        SetExportConfirmed(true);
        _announcer?.Announce(ExportedText);
        _exportTimer?.Stop();
        _exportTimer = new DispatcherTimer { Interval = ExportConfirmDuration };
        _exportTimer.Tick += (_, _) =>
        {
            _exportTimer?.Stop();
            SetExportConfirmed(false);
        };
        _exportTimer.Start();
    }

    private void SetExportConfirmed(bool confirmed)
    {
        IsExportConfirmed = confirmed;
        PseudoClasses.Set(ExportedPseudoClass, confirmed);
        if (_export is not null)
        {
            _export.IsConfirmed = confirmed;
        }
    }

    #endregion
}
