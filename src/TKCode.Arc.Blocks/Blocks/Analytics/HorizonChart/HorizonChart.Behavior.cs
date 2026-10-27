using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

public partial class HorizonChart
{
    private HorizonSplitPanel? _header;
    private HorizonSplitPanel? _controls;
    private Control? _sortGroup;
    private Control? _bandsGroup;
    private TextBlock? _sortLabel;
    private TextBlock? _bandsLabel;
    private SegmentedControl? _sortSwitch;
    private SegmentedControl? _bandsSwitch;
    private TextBlock? _date;
    private TextBlock? _status;
    private HorizonLegend? _legend;
    private HorizonPlot? _plot;
    private HorizonDataTable? _table;
    private AnalyticsAnnouncer? _announcer;

    private string[] _names = [];
    private double[][] _rows = [];
    private HorizonDay[] _dayItems = [];
    private double _extent = HorizonEngine.ExtentStep * 3;
    private bool _syncing;
    private bool _dayByKeyboard;
    private bool _ready;
    private readonly List<HorizonSeries> _hooked = new();
    private INotifyCollectionChanged? _sourceNotifier;

    internal HorizonPlot? PlotPartControl => _plot;

    internal HorizonLegend? LegendPartControl => _legend;

    internal HorizonDataTable? TablePartControl => _table;

    internal AnalyticsAnnouncer? AnnouncerPartControl => _announcer;

    internal double CurrentExtent => _extent;

    internal int DayCount => _dayItems.Length;

    /// <summary>Whether any row is drawn.</summary>
    public bool HasRows => _names.Length > 0 && _dayItems.Length > 0;

    public HorizonChart()
    {
        Series.CollectionChanged += OnSeriesCollectionChanged;
    }

    /// <summary>Puts the crosshair on <paramref name="index"/> (null hides it and shows the latest day).</summary>
    public void SelectDay(int? index) => SelectedDayIndex = index;

    /// <summary>Hides the crosshair; the readout returns to the latest day.</summary>
    public void ClearSelection() => SelectedDayIndex = null;

    private IEnumerable<HorizonSeries> EffectiveSeries
        => SeriesSource is { } source ? source.OfType<HorizonSeries>() : Series;

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_sortSwitch is not null)
        {
            _sortSwitch.SelectionChanged -= OnSortSwitchChanged;
        }
        if (_bandsSwitch is not null)
        {
            _bandsSwitch.SelectionChanged -= OnBandsSwitchChanged;
        }
        if (_plot is not null)
        {
            _plot.DayRequested -= OnDayRequested;
        }

        _header = e.NameScope.Find<HorizonSplitPanel>(HeaderPart);
        _controls = e.NameScope.Find<HorizonSplitPanel>(ControlsPart);
        _sortGroup = e.NameScope.Find<Control>(SortGroupPart);
        _bandsGroup = e.NameScope.Find<Control>(BandsGroupPart);
        _sortLabel = e.NameScope.Find<TextBlock>(SortLabelPart);
        _bandsLabel = e.NameScope.Find<TextBlock>(BandsLabelPart);
        _sortSwitch = e.NameScope.Find<SegmentedControl>(SortSwitchPart);
        _bandsSwitch = e.NameScope.Find<SegmentedControl>(BandsSwitchPart);
        _date = e.NameScope.Find<TextBlock>(DatePart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        _legend = e.NameScope.Find<HorizonLegend>(LegendPart);
        _plot = e.NameScope.Find<HorizonPlot>(PlotPart);
        _table = e.NameScope.Find<HorizonDataTable>(TablePart);
        _announcer = e.NameScope.Find<AnalyticsAnnouncer>(AnnouncerPart);

        if (_sortSwitch is not null)
        {
            _sortSwitch.SelectionChanged += OnSortSwitchChanged;
        }
        if (_bandsSwitch is not null)
        {
            _bandsSwitch.SelectionChanged += OnBandsSwitchChanged;
        }
        if (_plot is not null)
        {
            _plot.DayRequested += OnDayRequested;
        }
        _ready = true;
        HookSource();
        UpdateChrome();
        Rebuild();
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
        var inner = width - BorderThickness.Left - BorderThickness.Right;
        var tier = HorizonEngine.TierFor(inner, _plot?.MediumWidth ?? HorizonEngine.MediumWidth, _plot?.CompactWidth ?? HorizonEngine.CompactWidth);
        PseudoClasses.Set(CompactPseudoClass, tier == HorizonTier.Compact);
        PseudoClasses.Set(MediumPseudoClass, tier == HorizonTier.Medium);
    }

    #endregion

    #region Property changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (!_ready)
        {
            return;
        }
        if (property == SeriesSourceProperty)
        {
            HookSource();
            Rebuild();
        }
        else if (property == DaysProperty || property == TicksProperty || property == ExtentProperty)
        {
            Rebuild();
        }
        else if (property == ValueFormatterProperty || property == LabelsProperty || property == BaselineLabelProperty)
        {
            UpdateChrome();
            _plot?.SetFormatter(ValueFormatter);
            _plot?.SetEmptyText(Labels.EmptyText);
            UpdateLegend();
            UpdateTable();
            UpdateReadout();
            UpdateAutomation();
        }
        else if (property == AboveColorProperty || property == BelowColorProperty)
        {
            _plot?.SetColors(AboveColor, BelowColor);
            _legend?.SetColors(AboveColor, BelowColor);
        }
        else if (property == TitleProperty)
        {
            UpdateTable();
            UpdateAutomation();
        }
        else if (property == IsSortSwitchVisibleProperty || property == IsBandSwitchVisibleProperty || property == IsLegendVisibleProperty)
        {
            UpdateChrome();
        }
        else if (property == SortProperty)
        {
            ApplySort(change.GetNewValue<HorizonSort>());
        }
        else if (property == BandsProperty)
        {
            ApplyBands(change.GetNewValue<int>());
        }
        else if (property == SelectedDayIndexProperty)
        {
            ApplyDay(change.GetOldValue<int?>(), change.GetNewValue<int?>());
        }
    }

    private void ApplySort(HorizonSort sort)
    {
        SyncSwitches();
        _plot?.SetSort(sort, animate: true);
        UpdateTable();
        _announcer?.Announce(sort switch
        {
            HorizonSort.Name => Labels.SortedByNameText,
            HorizonSort.Latest => Labels.SortedByLatestText,
            _ => Labels.SortedByPeakText
        });
        RaiseEvent(new HorizonSortChangedEventArgs(SortChangedEvent, this, sort));
        if (SortCommand is { } command && command.CanExecute(sort))
        {
            command.Execute(sort);
        }
    }

    private void ApplyBands(int bands)
    {
        SyncSwitches();
        _plot?.SetBands(bands, animate: true);
        _legend?.SetBands(bands, animate: true);
        UpdateLegend();
        _announcer?.Announce(bands >= 2
            ? string.Format(Labels.BandsAnnouncementFormat, bands, FormatMagnitude(_extent / bands))
            : string.Format(Labels.OneBandAnnouncementFormat, FormatMagnitude(_extent)));
        RaiseEvent(new HorizonBandsChangedEventArgs(BandsChangedEvent, this, bands));
        if (BandsCommand is { } command && command.CanExecute(bands))
        {
            command.Execute(bands);
        }
    }

    private void ApplyDay(int? oldIndex, int? index)
    {
        if (index is { } i && (i < 0 || i >= _dayItems.Length))
        {
            // Out of range: clamp to the days, or clear when there are none.
            SetCurrentValue(SelectedDayIndexProperty, _dayItems.Length == 0 ? null : Math.Clamp(i, 0, _dayItems.Length - 1));
            return;
        }
        _plot?.SetDay(index);
        PseudoClasses.Set(ScrubbingPseudoClass, index is not null);
        UpdateReadout();
        if (index is { } day && _dayByKeyboard)
        {
            _announcer?.Announce(DayAnnouncement(day));
        }
        if (oldIndex == index)
        {
            return;
        }
        RaiseEvent(new HorizonDayChangedEventArgs(DayChangedEvent, this, index, index is { } d && d < _dayItems.Length ? _dayItems[d] : null));
        if (DayCommand is { } command && command.CanExecute(index))
        {
            command.Execute(index);
        }
    }

    private void OnDayRequested(int? day, bool byKeyboard)
    {
        _dayByKeyboard = byKeyboard;
        try
        {
            SetCurrentValue(SelectedDayIndexProperty, day);
        }
        finally
        {
            _dayByKeyboard = false;
        }
    }

    private string DayAnnouncement(int day)
    {
        var (high, low) = HorizonEngine.Extremes(_rows, day);
        if (high < 0 || low < 0)
        {
            return _dayItems[day].LongText;
        }
        return string.Format(Labels.DayAnnouncementFormat, _dayItems[day].LongText, _names[high], FormatValue(_rows[high][day]), _names[low],
            FormatValue(_rows[low][day]));
    }

    #endregion

    #region Data

    private void HookSource()
    {
        if (_sourceNotifier is not null)
        {
            _sourceNotifier.CollectionChanged -= OnSeriesCollectionChanged;
            _sourceNotifier = null;
        }
        if (SeriesSource is INotifyCollectionChanged notifier)
        {
            _sourceNotifier = notifier;
            notifier.CollectionChanged += OnSeriesCollectionChanged;
        }
    }

    private void OnSeriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_ready)
        {
            Rebuild();
        }
    }

    private void Rehook(List<HorizonSeries> series)
    {
        foreach (var old in _hooked)
        {
            old.PropertyChanged -= OnSeriesAvaloniaPropertyChanged;
        }
        _hooked.Clear();
        foreach (var item in series)
        {
            item.PropertyChanged += OnSeriesAvaloniaPropertyChanged;
            _hooked.Add(item);
        }
    }

    private void OnSeriesAvaloniaPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_ready && (e.Property == HorizonSeries.NameProperty || e.Property == HorizonSeries.ValuesProperty))
        {
            Rebuild();
        }
    }

    /// <summary>Reads the rows and days again and redraws everything without motion.</summary>
    private void Rebuild()
    {
        var series = EffectiveSeries.ToList();
        Rehook(series);
        var count = series.Count == 0 ? 0 : series.Max(s => s.Values.Count);
        var days = Days is { Count: > 0 } list ? list.ToArray() : [];
        if (days.Length == 0 && count > 0)
        {
            days = new HorizonDay[count];
            for (var i = 0; i < count; i++)
            {
                days[i] = new HorizonDay($"Day {i + 1}", $"Day {i + 1}");
            }
        }
        _dayItems = days;
        _names = series.Select(s => s.Name).ToArray();
        _rows = new double[series.Count][];
        for (var r = 0; r < series.Count; r++)
        {
            var row = new double[days.Length];
            Array.Fill(row, double.NaN);
            var values = series[r].Values;
            for (var d = 0; d < days.Length && d < values.Count; d++)
            {
                row[d] = values[d];
            }
            _rows[r] = row;
        }
        _extent = double.IsNaN(Extent) || Extent <= 0 ? HorizonEngine.AutoExtent(HorizonEngine.MaxAbs(_rows)) : Extent;
        var ticks = Ticks ?? (IReadOnlyList<HorizonTick>)HorizonEngine.MonthTicks(_dayItems);
        PseudoClasses.Set(EmptyPseudoClass, !HasRows);
        if (SelectedDayIndex is { } selected && selected >= _dayItems.Length)
        {
            SetCurrentValue(SelectedDayIndexProperty, _dayItems.Length == 0 ? null : _dayItems.Length - 1);
        }
        if (_plot is not null)
        {
            _plot.SetFormatter(ValueFormatter);
            _plot.SetEmptyText(Labels.EmptyText);
            _plot.SetColors(AboveColor, BelowColor);
            _plot.SetRows(_names, _rows, _dayItems.Length, _extent, ticks, Sort);
            _plot.SetBands(Bands, animate: false);
            _plot.SetDay(SelectedDayIndex);
        }
        _legend?.SetColors(AboveColor, BelowColor);
        _legend?.SetBands(Bands, animate: false);
        UpdateLegend();
        UpdateTable();
        UpdateReadout();
        UpdateAutomation();
        SyncSwitches();
    }

    #endregion

    #region Texts

    private string Baseline => BaselineLabel ?? Labels.BaselineText;

    private string FormatValue(double value)
        => ValueFormatter is { } format ? format(value) : AnalyticsFormat.Percent(value, 0, signed: true);

    private string FormatMagnitude(double value)
    {
        if (ValueFormatter is { } format)
        {
            return format(value);
        }
        return AnalyticsFormat.Percent(value, Math.Abs(value - Math.Round(value)) < 1e-9 ? 0 : 1);
    }

    private void UpdateReadout()
    {
        var index = SelectedDayIndex is { } s && s >= 0 && s < _dayItems.Length ? s : _dayItems.Length - 1;
        if (_date is not null)
        {
            _date.Text = index >= 0 ? _dayItems[index].LongText : string.Empty;
        }
        if (_status is not null)
        {
            _status.Text = SelectedDayIndex is not null && index >= 0 ? Labels.SelectedDayText : Labels.LatestDayText;
        }
    }

    private void UpdateLegend()
    {
        if (_legend is null)
        {
            return;
        }
        var labels = Labels;
        var bands = Bands;
        var caption = bands >= 2
            ? string.Format(labels.CaptionFormat, bands, FormatMagnitude(_extent / bands), Baseline)
            : string.Format(labels.CaptionOneBandFormat, FormatMagnitude(_extent), Baseline);
        var start = FormatValue(-_extent);
        var end = FormatValue(_extent);
        _legend.SetTexts(start, labels.BelowText, "0", labels.AboveText, end, caption);
        var perSide = bands == 1 ? labels.OneBandText : string.Format(labels.BandCountFormat, bands);
        AutomationProperties.SetName(_legend, string.Format(labels.ScaleDescriptionFormat, start, end, Baseline, perSide));
    }

    private void UpdateChrome()
    {
        var labels = Labels;
        if (_sortGroup is not null)
        {
            _sortGroup.IsVisible = IsSortSwitchVisible;
        }
        if (_bandsGroup is not null)
        {
            _bandsGroup.IsVisible = IsBandSwitchVisible;
        }
        if (_controls is not null)
        {
            _controls.IsVisible = IsSortSwitchVisible || IsBandSwitchVisible;
        }
        if (_legend is not null)
        {
            _legend.IsVisible = IsLegendVisible;
        }
        if (_sortLabel is not null)
        {
            _sortLabel.Text = labels.SortText;
        }
        if (_bandsLabel is not null)
        {
            _bandsLabel.Text = labels.BandsText;
        }
        if (_sortSwitch is not null)
        {
            _syncing = true;
            _sortSwitch.ItemsSource = new List<SegmentedOption>
            {
                new(HorizonSort.Name, labels.NameText),
                new(HorizonSort.Peak, labels.PeakText),
                new(HorizonSort.Latest, labels.LatestText)
            };
            AutomationProperties.SetName(_sortSwitch, labels.SortGroupText);
            _syncing = false;
        }
        if (_bandsSwitch is not null)
        {
            _syncing = true;
            _bandsSwitch.ItemsSource = new List<SegmentedOption>
            {
                new(1, "1"),
                new(2, "2"),
                new(3, "3"),
                new(4, "4")
            };
            AutomationProperties.SetName(_bandsSwitch, labels.BandsGroupText);
            _syncing = false;
        }
        SyncSwitches();
    }

    private void SyncSwitches()
    {
        _syncing = true;
        try
        {
            if (_sortSwitch is not null && _sortSwitch.SelectedIndex != (int)Sort)
            {
                _sortSwitch.SelectedIndex = (int)Sort;
            }
            if (_bandsSwitch is not null && _bandsSwitch.SelectedIndex != Bands - 1)
            {
                _bandsSwitch.SelectedIndex = Bands - 1;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void UpdateAutomation()
    {
        if (_plot is null)
        {
            return;
        }
        AutomationProperties.SetName(_plot, string.Format(Labels.ChartNameFormat, Title ?? string.Empty, _names.Length));
        AutomationProperties.SetHelpText(_plot, Labels.KeyboardHint);
    }

    private void UpdateTable()
    {
        if (_table is null || _plot is null)
        {
            return;
        }
        var order = _plot.RowOrder;
        var headers = new string[order.Count];
        var cells = new string[order.Count][];
        var labels = Labels;
        for (var slot = 0; slot < order.Count; slot++)
        {
            var row = order[slot];
            headers[slot] = _names[row];
            var values = _rows[row];
            var high = -1;
            var low = -1;
            for (var d = 0; d < values.Length; d++)
            {
                if (double.IsNaN(values[d]))
                {
                    continue;
                }
                if (high < 0 || values[d] > values[high])
                {
                    high = d;
                }
                if (low < 0 || values[d] < values[low])
                {
                    low = d;
                }
            }
            var last = values.Length - 1;
            cells[slot] =
            [
                Cell(labels.LatestColumnText, last),
                Cell(labels.HighestColumnText, high),
                Cell(labels.LowestColumnText, low)
            ];

            string Cell(string column, int day) => day < 0 || double.IsNaN(values[day])
                ? column + ": " + AnalyticsFormat.Missing
                : string.Format(labels.TableCellFormat, column, FormatValue(values[day]), _dayItems[day].ShortText);
        }
        _table.Update(string.Format(labels.TableCaptionFormat, Title ?? string.Empty, Baseline), headers, cells);
    }

    #endregion

    #region Switches

    private void OnSortSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _sortSwitch is null || _sortSwitch.SelectedIndex < 0)
        {
            return;
        }
        SetCurrentValue(SortProperty, (HorizonSort)_sortSwitch.SelectedIndex);
    }

    private void OnBandsSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _bandsSwitch is null || _bandsSwitch.SelectedIndex < 0)
        {
            return;
        }
        SetCurrentValue(BandsProperty, _bandsSwitch.SelectedIndex + 1);
    }

    #endregion
}
