using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

public partial class RevenueExplorer
{
    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<RevenueExplorer, double>(nameof(NarrowWidth), 560);

    internal static readonly StyledProperty<double> StackedWidthProperty =
        AvaloniaProperty.Register<RevenueExplorer, double>(nameof(StackedWidth), 440);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<RevenueExplorer, double>(nameof(CompactWidth), 600);

    internal static readonly StyledProperty<double> MetricValueMinSizeProperty =
        AvaloniaProperty.Register<RevenueExplorer, double>(nameof(MetricValueMinSize), 22);

    internal static readonly StyledProperty<double> MetricValueMaxSizeProperty =
        AvaloniaProperty.Register<RevenueExplorer, double>(nameof(MetricValueMaxSize), 28);

    internal static readonly StyledProperty<double> MetricValueScaleProperty =
        AvaloniaProperty.Register<RevenueExplorer, double>(nameof(MetricValueScale), 0.03);

    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    internal double StackedWidth { get => GetValue(StackedWidthProperty); set => SetValue(StackedWidthProperty, value); }

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    internal double MetricValueMinSize { get => GetValue(MetricValueMinSizeProperty); set => SetValue(MetricValueMinSizeProperty, value); }

    internal double MetricValueMaxSize { get => GetValue(MetricValueMaxSizeProperty); set => SetValue(MetricValueMaxSizeProperty, value); }

    internal double MetricValueScale { get => GetValue(MetricValueScaleProperty); set => SetValue(MetricValueScaleProperty, value); }

    private RevenueSplitPanel? _header;
    private TextBlock? _titleText;
    private AnalyticsTextSwap? _subtitle;
    private AnalyticsSegmentSwitch? _presetSwitch;
    private RevenueKpiRow? _metrics;
    private RevenueSplitPanel? _toolbar;
    private RevenueSeriesChip? _newChip;
    private RevenueSeriesChip? _expansionChip;
    private RevenueSeriesChip? _churnChip;
    private RevenueCompareSwitch? _compareSwitch;
    private RevenueChartView? _chart;
    private RevenueOverviewView? _overview;
    private RevenueDataTable? _table;
    private AnalyticsAnnouncer? _announcer;
    private RevenueViewport? _viewport;

    private readonly List<RevenueMonth> _months = new();
    private readonly List<RevenuePresetItem> _presets = new();
    private INotifyCollectionChanged? _observedSource;
    private RevenueData _data = RevenueData.Empty;
    private CultureInfo _culture = AnalyticsFormat.DefaultCulture;
    private bool _monthsDirty = true;
    private bool _syncing;
    private bool _scrubByKeyboard;
    private int _start;
    private int _end;

    private readonly record struct RevenuePresetItem(int Months, string Label, string? AccessibleName);

    static RevenueExplorer()
    {
        FocusableProperty.OverrideDefaultValue<RevenueExplorer>(false);
    }

    /// <summary>Creates the block.</summary>
    public RevenueExplorer()
    {
        Months.CollectionChanged += (_, _) => OnMonthsChanged();
        Presets.CollectionChanged += (_, _) => OnPresetsChanged();
    }

    /// <summary>The chart (tests).</summary>
    internal RevenueChartView? Chart => _chart;

    /// <summary>The overview strip (tests).</summary>
    internal RevenueOverviewView? Overview => _overview;

    /// <summary>The range buttons (tests).</summary>
    internal AnalyticsSegmentSwitch? PresetSwitch => _presetSwitch;

    /// <summary>The headline numbers (tests).</summary>
    internal RevenueKpiRow? Metrics => _metrics;

    /// <summary>The subtitle (tests).</summary>
    internal AnalyticsTextSwap? Subtitle => _subtitle;

    /// <summary>The series chips: new, expansion, churn (tests).</summary>
    internal RevenueSeriesChip?[] Chips => [_newChip, _expansionChip, _churnChip];

    /// <summary>The compare switch (tests).</summary>
    internal RevenueCompareSwitch? CompareSwitch => _compareSwitch;

    /// <summary>The live region (tests).</summary>
    internal AnalyticsAnnouncer? Announcer => _announcer;

    /// <summary>The screen reader table (tests).</summary>
    internal RevenueDataTable? Table => _table;

    /// <summary>The shared window of chart and overview (tests).</summary>
    internal RevenueViewport? Viewport => _viewport;

    /// <summary>The toolbar (tests).</summary>
    internal RevenueSplitPanel? Toolbar => _toolbar;

    /// <summary>The header (tests).</summary>
    internal RevenueSplitPanel? Header => _header;

    /// <summary>The months in effect (<see cref="MonthsSource"/> or <see cref="Months"/>).</summary>
    public IReadOnlyList<RevenueMonth> EffectiveMonths
    {
        get
        {
            EnsureMonths();
            return _months;
        }
    }

    /// <summary>
    /// Selects months <paramref name="start"/> to <paramref name="end"/> (indexes, included). The range is clamped to the data
    /// and to <see cref="MinRangeMonths"/>; the chart, the axes and the numbers slide there when <paramref name="animate"/> is true.
    /// </summary>
    public void SetRange(int start, int end, bool animate = true) => ApplyRange(start, end, RevenueRangeSource.Api, animate, false);

    /// <summary>Selects the newest <paramref name="months"/> months (0 or more than there are: all of them), as a preset button does.</summary>
    public void ApplyPreset(int months)
    {
        var (s, e) = (_chart is null ? new RevenueData(EffectiveMonths, OpeningMrr) : _data).PresetRange(months);
        ApplyRange(s, e, RevenueRangeSource.Preset, true, false);
    }

    /// <summary>Closes the crosshair and the readout.</summary>
    public void ClearScrub() => SetCurrentValue(ScrubIndexProperty, null);

    /// <summary>Moves the keyboard focus to the chart (its readout opens on the last month of the range).</summary>
    public void FocusChart() => _chart?.Focus(Avalonia.Input.NavigationMethod.Tab);

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _header = e.NameScope.Find<RevenueSplitPanel>(HeaderPart);
        _titleText = e.NameScope.Find<TextBlock>(TitlePart);
        _subtitle = e.NameScope.Find<AnalyticsTextSwap>(SubtitlePart);
        if (_subtitle is not null)
        {
            _subtitle.Swap = AnalyticsSwapStyle.RiseEven;
        }
        _presetSwitch = e.NameScope.Find<AnalyticsSegmentSwitch>(PresetsPart);
        _metrics = e.NameScope.Find<RevenueKpiRow>(MetricsPart);
        _toolbar = e.NameScope.Find<RevenueSplitPanel>(ToolbarPart);
        _newChip = e.NameScope.Find<RevenueSeriesChip>(NewChipPart);
        _expansionChip = e.NameScope.Find<RevenueSeriesChip>(ExpansionChipPart);
        _churnChip = e.NameScope.Find<RevenueSeriesChip>(ChurnChipPart);
        _compareSwitch = e.NameScope.Find<RevenueCompareSwitch>(CompareSwitchPart);
        _chart = e.NameScope.Find<RevenueChartView>(PlotPart);
        _overview = e.NameScope.Find<RevenueOverviewView>(OverviewPart);
        _table = e.NameScope.Find<RevenueDataTable>(TablePart);
        _announcer = e.NameScope.Find<AnalyticsAnnouncer>(AnnouncerPart);

        _viewport = new RevenueViewport(this);
        if (_chart is not null)
        {
            _chart.Viewport = _viewport;
            _chart.ScrubRequested += OnScrubRequested;
        }
        if (_overview is not null)
        {
            _overview.Viewport = _viewport;
            _overview.RangeRequested += OnOverviewRangeRequested;
            _overview.DraggingChanged += OnDraggingChanged;
        }
        if (_presetSwitch is not null)
        {
            _presetSwitch.Look = AnalyticsSegmentLook.Pill;
            _presetSwitch.SelectionChanged += OnPresetSelectionChanged;
        }
        HookToggle(_newChip, RevenueSeries.New);
        HookToggle(_expansionChip, RevenueSeries.Expansion);
        HookToggle(_churnChip, RevenueSeries.Churn);
        if (_compareSwitch is not null)
        {
            _compareSwitch.Toggled += OnCompareToggled;
        }
        _monthsDirty = true;
        ApplyAll();
        UpdateBreakpoints(Bounds.Width);
    }

    private void Unhook()
    {
        if (_chart is not null)
        {
            _chart.ScrubRequested -= OnScrubRequested;
        }
        if (_overview is not null)
        {
            _overview.RangeRequested -= OnOverviewRangeRequested;
            _overview.DraggingChanged -= OnDraggingChanged;
        }
        if (_presetSwitch is not null)
        {
            _presetSwitch.SelectionChanged -= OnPresetSelectionChanged;
        }
        if (_compareSwitch is not null)
        {
            _compareSwitch.Toggled -= OnCompareToggled;
        }
        foreach (var (chip, handler) in _chipHandlers)
        {
            chip.Toggled -= handler;
        }
        _chipHandlers.Clear();
    }

    private readonly List<(RevenueSeriesChip Chip, Action<bool> Handler)> _chipHandlers = new();

    private void HookToggle(RevenueSeriesChip? chip, RevenueSeries series)
    {
        if (chip is null)
        {
            return;
        }
        Action<bool> handler = on => OnChipToggled(series, on);
        chip.Toggled += handler;
        _chipHandlers.Add((chip, handler));
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
        var tight = width < NarrowWidth;
        var stacked = width < StackedWidth;
        var inner = width - Padding.Left - Padding.Right - BorderThickness.Left - BorderThickness.Right;
        PseudoClasses.Set(TightPseudoClass, tight);
        PseudoClasses.Set(StackedPseudoClass, stacked);
        PseudoClasses.Set(CompactPseudoClass, inner < CompactWidth);
        if (_header is not null)
        {
            _header.IsWrapForced = stacked;
        }
        if (_presetSwitch is not null)
        {
            _presetSwitch.IsStretched = stacked;
        }
        foreach (var chip in new[] { _newChip, _expansionChip, _churnChip })
        {
            if (chip is not null)
            {
                chip.IsCompact = stacked;
            }
        }
        if (_metrics is not null)
        {
            _metrics.IsStacked = stacked;
            var size = Math.Clamp(Math.Round(width * MetricValueScale, 2), MetricValueMinSize, MetricValueMaxSize);
            _metrics.Mrr.ValueSize = size;
            _metrics.NetNew.ValueSize = size;
            _metrics.Churn.ValueSize = size;
        }
    }

    #endregion

    #region Property changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == MonthsSourceProperty)
        {
            ObserveSource(change.GetNewValue<IEnumerable?>());
            OnMonthsChanged();
        }
        else if (property == OpeningMrrProperty || property == CurrencySymbolProperty || property == CultureNameProperty ||
                 property == DefaultRangeMonthsProperty || property == MinRangeMonthsProperty)
        {
            ApplyAll();
        }
        else if (property == RangeStartProperty || property == RangeEndProperty)
        {
            if (!_syncing && _chart is not null)
            {
                ApplyRange(RangeStart, RangeEnd, RevenueRangeSource.Api, true, false);
            }
        }
        else if (property == IsNewVisibleProperty || property == IsExpansionVisibleProperty || property == IsChurnVisibleProperty)
        {
            OnSeriesChanged(property == IsNewVisibleProperty ? RevenueSeries.New : property == IsExpansionVisibleProperty ? RevenueSeries.Expansion : RevenueSeries.Churn);
        }
        else if (property == IsCompareEnabledProperty)
        {
            OnCompareChanged();
        }
        else if (property == ScrubIndexProperty)
        {
            OnScrubChanged(change.GetNewValue<int?>());
        }
        else if (property == TitleProperty)
        {
            UpdateTitle();
        }
        else if (property.OwnerType == typeof(RevenueExplorer) && IsTextProperty(property))
        {
            UpdateTexts();
        }
        else if (property == NarrowWidthProperty || property == StackedWidthProperty || property == CompactWidthProperty ||
                 property == MetricValueScaleProperty || property == MetricValueMinSizeProperty || property == MetricValueMaxSizeProperty)
        {
            UpdateBreakpoints(Bounds.Width);
        }
    }

    private static bool IsTextProperty(AvaloniaProperty property) =>
        property == NewLabelProperty || property == ExpansionLabelProperty || property == ChurnLabelProperty ||
        property == NetNewLabelProperty || property == MrrLabelProperty || property == NetNewKpiLabelProperty ||
        property == ChurnRateLabelProperty || property == CompareTextProperty || property == EmptyTextProperty ||
        property == SinceFormatProperty || property == VersusFormatProperty || property == PreviousRowFormatProperty ||
        property == PresetGroupLabelProperty || property == PresetNameFormatProperty || property == AllPresetNameFormatProperty ||
        property == SeriesGroupLabelProperty || property == PlotLabelProperty || property == WindowLabelProperty ||
        property == StartHandleLabelProperty || property == EndHandleLabelProperty || property == TableCaptionFormatProperty ||
        property == ChartSummaryFormatProperty || property == RangeAnnouncementFormatProperty || property == ScrubFormatProperty ||
        property == ValueFormatterProperty || property == AxisFormatterProperty || property == MonthFormatterProperty ||
        property == MonthYearFormatterProperty || property == RangeFormatterProperty;

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

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnMonthsChanged();

    private void OnMonthsChanged()
    {
        _monthsDirty = true;
        ApplyAll();
    }

    private void OnPresetsChanged()
    {
        UpdatePresetItems();
        UpdatePresetSelection();
    }

    private void OnMonthDataChanged(object? sender, EventArgs e)
    {
        // A month's numbers changed in place: rebuild (jump) with the same range.
        _monthsDirty = false;
        ApplyAll();
    }

    private void EnsureMonths()
    {
        if (!_monthsDirty)
        {
            return;
        }
        _monthsDirty = false;
        foreach (var month in _months)
        {
            month.Changed -= OnMonthDataChanged;
        }
        _months.Clear();
        IEnumerable source = MonthsSource ?? Months;
        foreach (var item in source)
        {
            if (item is RevenueMonth month)
            {
                _months.Add(month);
                month.Changed += OnMonthDataChanged;
            }
        }
    }

    #endregion

    #region Formatting

    private string Symbol => CurrencySymbol ?? string.Empty;

    private string FormatValue(double v) => ValueFormatter is { } f ? f(v) : v == 0 ? Symbol + "0" : AnalyticsFormat.Compact(v, 3, Symbol);

    private string FormatAxis(double v) => AxisFormatter is { } f ? f(v) : v == 0 ? Symbol + "0" : AnalyticsFormat.Compact(v, 3, Symbol, false, true);

    private string FormatMonth(DateOnly m) => MonthFormatter is { } f ? f(m) : RevenueData.DefaultMonthLabel(m, _culture);

    private string FormatMonthYear(DateOnly m) => MonthYearFormatter is { } f ? f(m) : RevenueData.DefaultMonthYear(m, _culture);

    private string FormatRange(DateOnly a, DateOnly b) => RangeFormatter is { } f ? f(a, b) : RevenueData.DefaultRange(a, b, _culture);

    private string RangeText(int start, int end)
    {
        if (_data.Count == 0)
        {
            return string.Empty;
        }
        start = Math.Clamp(start, 0, _data.Count - 1);
        end = Math.Clamp(end, 0, _data.Count - 1);
        return start == end ? FormatMonthYear(_data.Months[start]) : FormatRange(_data.Months[start], _data.Months[end]);
    }

    private RevenueChartOptions BuildChartOptions() => new()
    {
        Axis = FormatAxis,
        Value = FormatValue,
        Month = FormatMonth,
        MonthYear = FormatMonthYear,
        NewLabel = NewLabel,
        ExpansionLabel = ExpansionLabel,
        ChurnLabel = ChurnLabel,
        NetNewLabel = NetNewLabel,
        EmptyText = EmptyText,
        PreviousRowFormat = PreviousRowFormat,
        ScrubFormat = ScrubFormat
    };

    #endregion

    #region Applying state

    private (int Start, int End) ResolveRange()
    {
        if (_data.Count == 0)
        {
            return (0, 0);
        }
        var s = RangeStart;
        var e = RangeEnd;
        if (s < 0 || e < 0)
        {
            (s, e) = _data.PresetRange(DefaultRangeMonths);
        }
        return _data.ClampRange(s, e, MinRangeMonths);
    }

    /// <summary>Rebuilds everything from the properties (new data, culture, opening revenue): nothing animates.</summary>
    private void ApplyAll()
    {
        if (_chart is null || _overview is null)
        {
            return;
        }
        EnsureMonths();
        try
        {
            _culture = CultureInfo.GetCultureInfo(CultureName);
        }
        catch (CultureNotFoundException)
        {
            _culture = AnalyticsFormat.DefaultCulture;
        }
        _data = new RevenueData(_months, OpeningMrr);
        (_start, _end) = ResolveRange();
        if (_data.Count > 0)
        {
            SyncRangeProperties();
        }
        _chart.SetOptions(BuildChartOptions());
        _chart.SetData(_data, _start, _end);
        _chart.SetSeries(IsNewVisible, IsExpansionVisible, IsChurnVisible, animate: false);
        _chart.SetCompare(IsCompareEnabled, animate: false);
        _chart.SetScrub(ScrubIndex, byPointer: true);
        _overview.SetData(_data);
        _overview.SetRange(_start, _end);
        _overview.MinMonths = MinRangeMonths;
        _viewport?.SetRange(_start, _end, animate: false);
        SyncToggles();
        UpdateTitle();
        UpdateTexts();
        UpdatePseudoClasses();
    }

    private void SyncRangeProperties()
    {
        _syncing = true;
        SetCurrentValue(RangeStartProperty, _start);
        SetCurrentValue(RangeEndProperty, _end);
        _syncing = false;
    }

    private void SyncToggles()
    {
        if (_newChip is not null)
        {
            _newChip.IsOn = IsNewVisible;
        }
        if (_expansionChip is not null)
        {
            _expansionChip.IsOn = IsExpansionVisible;
        }
        if (_churnChip is not null)
        {
            _churnChip.IsOn = IsChurnVisible;
        }
        if (_compareSwitch is not null)
        {
            _compareSwitch.IsOn = IsCompareEnabled;
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(ComparePseudoClass, IsCompareEnabled);
        PseudoClasses.Set(EmptySeriesPseudoClass, !IsNewVisible && !IsExpansionVisible && !IsChurnVisible);
    }

    private void ApplyRange(int start, int end, RevenueRangeSource source, bool animate, bool force)
    {
        if (_chart is null || _overview is null)
        {
            // Before the template: only the properties take the range (the first layout applies it).
            var early = new RevenueData(EffectiveMonths, OpeningMrr);
            (start, end) = early.ClampRange(start, end, MinRangeMonths);
            if (early.Count > 0)
            {
                _syncing = true;
                SetCurrentValue(RangeStartProperty, start);
                SetCurrentValue(RangeEndProperty, end);
                _syncing = false;
            }
            return;
        }
        if (_data.Count == 0)
        {
            return;
        }
        (start, end) = _data.ClampRange(start, end, MinRangeMonths);
        var changed = start != _start || end != _end;
        if (!changed && !force)
        {
            SyncRangeProperties();
            return;
        }
        _start = start;
        _end = end;
        SyncRangeProperties();
        if (_viewport is { IsDragging: false })
        {
            _viewport.SetRange(start, end, animate);
        }
        _chart.SetRange(start, end, animate);
        _overview.SetRange(start, end);
        UpdateRangeTexts(animate);
        if (!changed)
        {
            return;
        }
        var args = new RevenueRangeChangedEventArgs(RangeChangedEvent, start, end, source);
        RaiseEvent(args);
        if (RangeCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
        if (source == RevenueRangeSource.Keyboard)
        {
            _announcer?.Announce(string.Format(_culture, RangeAnnouncementFormat, RangeText(start, end)));
        }
    }

    private void OnOverviewRangeRequested(int start, int end, RevenueRangeSource source) => ApplyRange(start, end, source, true, false);

    private void OnDraggingChanged(bool dragging) => PseudoClasses.Set(DraggingPseudoClass, dragging);

    private void OnPresetSelectionChanged(object? sender, AnalyticsSegmentChangedEventArgs e)
    {
        if (_syncing || !e.ByUser || e.NewIndex < 0 || e.NewIndex >= _presets.Count)
        {
            return;
        }
        var (s, end) = _data.PresetRange(_presets[e.NewIndex].Months);
        ApplyRange(s, end, RevenueRangeSource.Preset, true, false);
    }

    private void OnChipToggled(RevenueSeries series, bool on)
    {
        switch (series)
        {
            case RevenueSeries.New:
                SetCurrentValue(IsNewVisibleProperty, on);
                break;
            case RevenueSeries.Expansion:
                SetCurrentValue(IsExpansionVisibleProperty, on);
                break;
            default:
                SetCurrentValue(IsChurnVisibleProperty, on);
                break;
        }
    }

    private void OnSeriesChanged(RevenueSeries series)
    {
        if (_chart is null)
        {
            return;
        }
        SyncToggles();
        _chart.SetSeries(IsNewVisible, IsExpansionVisible, IsChurnVisible, animate: true);
        UpdatePseudoClasses();
        var on = series switch { RevenueSeries.New => IsNewVisible, RevenueSeries.Expansion => IsExpansionVisible, _ => IsChurnVisible };
        RaiseEvent(new RevenueSeriesChangedEventArgs(SeriesVisibilityChangedEvent, series, on));
    }

    private void OnCompareToggled(bool on) => SetCurrentValue(IsCompareEnabledProperty, on);

    private void OnCompareChanged()
    {
        if (_chart is null)
        {
            return;
        }
        SyncToggles();
        _chart.SetCompare(IsCompareEnabled, animate: true);
        UpdatePseudoClasses();
        RaiseEvent(new RevenueCompareChangedEventArgs(CompareChangedEvent, IsCompareEnabled));
    }

    private void OnScrubRequested(int? index, bool byKeyboard)
    {
        _scrubByKeyboard = byKeyboard;
        SetCurrentValue(ScrubIndexProperty, index);
    }

    private void OnScrubChanged(int? index)
    {
        if (_chart is null)
        {
            return;
        }
        var months = EffectiveMonths;
        if (index is { } i && (i < 0 || i >= months.Count))
        {
            index = null;
        }
        _chart.SetScrub(index, byPointer: !_scrubByKeyboard);
        if (index is { } shown && _scrubByKeyboard && _announcer is not null)
        {
            _announcer.Announce(_chart.SpokenText(shown));
        }
        _scrubByKeyboard = false;
        RaiseEvent(new RevenueScrubChangedEventArgs(ScrubChangedEvent, index, index is { } k && k < months.Count ? months[k] : null));
    }

    #endregion

    #region Texts and numbers

    private void UpdateTitle()
    {
        if (_titleText is not null)
        {
            _titleText.Text = Title;
        }
    }

    /// <summary>Texts that come from properties (labels, formats, names): set once and when they change, then the numbers.</summary>
    private void UpdateTexts()
    {
        if (_chart is null || _overview is null)
        {
            return;
        }
        _chart.SetOptions(BuildChartOptions());
        AutomationProperties.SetName(_chart, PlotLabel);
        if (_newChip is not null)
        {
            _newChip.Text = NewLabel;
        }
        if (_expansionChip is not null)
        {
            _expansionChip.Text = ExpansionLabel;
        }
        if (_churnChip is not null)
        {
            _churnChip.Text = ChurnLabel;
        }
        if (_compareSwitch is not null)
        {
            _compareSwitch.Text = CompareText;
        }
        if (_presetSwitch is not null)
        {
            AutomationProperties.SetName(_presetSwitch, PresetGroupLabel);
        }
        if (_newChip?.Parent is Control group)
        {
            AutomationProperties.SetName(group, SeriesGroupLabel);
        }
        _overview.RangeText = RangeText;
        _overview.YearText = m => FormatYear(m);
        AutomationProperties.SetName(_overview.Thumbs[0], WindowLabel);
        AutomationProperties.SetName(_overview.Thumbs[1], StartHandleLabel);
        AutomationProperties.SetName(_overview.Thumbs[2], EndHandleLabel);
        if (_metrics is not null)
        {
            _metrics.Mrr.Label = MrrLabel;
            _metrics.NetNew.Label = NetNewKpiLabel;
            _metrics.Churn.Label = ChurnRateLabel;
        }
        UpdatePresetItems();
        UpdateRangeTexts(animate: false);
    }

    private string FormatYear(DateOnly m) => m.Year.ToString(_culture);

    private IReadOnlyList<RevenuePresetItem> BuildPresetItems()
    {
        _presets.Clear();
        if (Presets.Count == 0)
        {
            foreach (var months in new[] { 3, 6, 12, 0 })
            {
                _presets.Add(new RevenuePresetItem(months, months == 0 ? "All" : $"{months}M", null));
            }
        }
        else
        {
            foreach (var preset in Presets)
            {
                _presets.Add(new RevenuePresetItem(preset.Months, preset.Label ?? (preset.Months <= 0 ? "All" : $"{preset.Months}M"), preset.AccessibleName));
            }
        }
        return _presets;
    }

    private void UpdatePresetItems()
    {
        if (_presetSwitch is null)
        {
            return;
        }
        var items = BuildPresetItems();
        _presetSwitch.Items = items.Select(p => p.Label).ToArray();
        _presetSwitch.AccessibleNames = items.Select(p =>
            p.AccessibleName ?? (p.Months <= 0
                ? string.Format(_culture, AllPresetNameFormat, _data.Count)
                : string.Format(_culture, PresetNameFormat, p.Months))).ToArray();
        UpdatePresetSelection();
    }

    private void UpdatePresetSelection()
    {
        if (_presetSwitch is null)
        {
            return;
        }
        var index = -1;
        for (var i = 0; i < _presets.Count && _data.Count > 0; i++)
        {
            if (_data.PresetRange(_presets[i].Months) == (_start, _end))
            {
                index = i;
                break;
            }
        }
        _syncing = true;
        _presetSwitch.SelectedIndex = index;
        _syncing = false;
    }

    private void UpdateRangeTexts(bool animate)
    {
        if (_chart is null)
        {
            return;
        }
        var text = RangeText(_start, _end);
        if (_subtitle is not null)
        {
            _subtitle.Text = text;
        }
        UpdatePresetSelection();
        UpdateMetrics();
        UpdateTable(text);
        if (_data.Count > 0)
        {
            var stats = _data.Stats(_start, _end);
            AutomationProperties.SetHelpText(_chart, string.Format(_culture, ChartSummaryFormat, text, MrrLabel, FormatValue(stats.EndMrr),
                FormatValue(stats.AverageNetNew)));
        }
    }

    private void UpdateMetrics()
    {
        if (_metrics is null)
        {
            return;
        }
        if (_data.Count == 0)
        {
            _metrics.Mrr.SetValue(null, default);
            _metrics.NetNew.SetValue(null, default);
            _metrics.Churn.SetValue(null, default);
            _metrics.Mrr.SetChange(null, default, false, null);
            _metrics.NetNew.SetChange(null, default, false, null);
            _metrics.Churn.SetChange(null, default, false, null);
            return;
        }
        var stats = _data.Stats(_start, _end);
        var length = _end - _start + 1;
        var versus = string.Format(_culture, VersusFormat, length);
        var since = string.Format(_culture, SinceFormat, FormatMonthYear(_data.Months[_start].AddMonths(-1)));
        var money = AnalyticsNumberFormat.Compact(3, Symbol);
        var percent = AnalyticsNumberFormat.Percent(1, signed: true);
        _metrics.Mrr.SetValue(stats.EndMrr, money);
        _metrics.Mrr.SetChange(stats.MrrChange * 100, percent, false, since);
        _metrics.NetNew.SetValue(stats.AverageNetNew, money);
        _metrics.NetNew.SetChange(stats.NetNewChange * 100, percent, false, versus);
        _metrics.Churn.SetValue(stats.ChurnRate, AnalyticsNumberFormat.Percent(2));
        _metrics.Churn.SetChange(stats.ChurnChange, AnalyticsNumberFormat.Points(2, signed: true), true, versus);
    }

    private void UpdateTable(string rangeText)
    {
        if (_table is null)
        {
            return;
        }
        var caption = string.Format(_culture, TableCaptionFormat, rangeText);
        if (_data.Count == 0)
        {
            _table.Update(caption, [], []);
            return;
        }
        var count = _end - _start + 1;
        var headers = new string[count];
        var cells = new string[count][];
        for (var k = 0; k < count; k++)
        {
            var i = _start + k;
            headers[k] = FormatMonthYear(_data.Months[i]);
            cells[k] =
            [
                $"{NewLabel}: {FormatValue(_data.New[i])}",
                $"{ExpansionLabel}: {FormatValue(_data.Expansion[i])}",
                $"{ChurnLabel}: {FormatValue(_data.Churn[i])}",
                $"{NetNewLabel}: {FormatValue(_data.Net[i])}",
                $"{MrrLabel}: {FormatValue(_data.Mrr[i])}"
            ];
        }
        _table.Update(caption, headers, cells);
    }

    #endregion
}
