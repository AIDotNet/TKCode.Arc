using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

public partial class SmallMultiples
{
    private readonly SmallMultiplesGrid _grid;
    private SegmentedControl? _sortSwitch;
    private SegmentedControl? _scaleSwitch;
    private Switch? _dimSwitch;
    private Panel? _gridHost;
    private Control? _focusRing;
    private TextBlock? _periodText;
    private TextBlock? _stateText;
    private TextBlock? _totalText;
    private LineChartsLiveRegion? _liveRegion;

    private List<SmallMultiplesRow> _rows = new();
    private List<SmallMultiplesPeriod> _periods = new();
    private (double Min, double Max) _shared = (0, 1);
    private double[] _sharedTicks = [];
    private INotifyCollectionChanged? _observedSeries;
    private INotifyCollectionChanged? _observedPeriods;
    private readonly List<AvaloniaObject> _observedItems = new();
    private bool _syncing;
    private bool _rebuildQueued;
    private double _containerWidth = double.NaN;

    public SmallMultiples()
    {
        _grid = new SmallMultiplesGrid(this);
        _grid.CrosshairChanged += OnCrosshairChanged;
        Series.CollectionChanged += (_, _) => OnDataChanged();
        Periods.CollectionChanged += (_, _) => OnDataChanged();
    }

    /// <summary>The trellis (tests).</summary>
    internal SmallMultiplesGrid Grid => _grid;

    /// <summary>The live region (tests).</summary>
    internal LineChartsLiveRegion? LiveRegion => _liveRegion;

    /// <summary>The rows in display order (tests).</summary>
    internal IReadOnlyList<SmallMultiplesRow> OrderedRows => _grid.Ordered.Select(p => p.Row).ToList();

    /// <summary>The shared y domain (tests).</summary>
    internal (double Min, double Max) SharedDomain => _shared;

    /// <summary>Moves the synced crosshair to <paramref name="index"/>, or hides it with null.</summary>
    public void SetCrosshairIndex(int? index) => _grid.SetCrosshair(index, SmallMultiplesCrosshairSource.Code);

    // Template -------------------------------------------------------------------------------------------------------------

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_sortSwitch is not null)
        {
            _sortSwitch.SelectionChanged -= OnSortSwitchChanged;
        }
        if (_scaleSwitch is not null)
        {
            _scaleSwitch.SelectionChanged -= OnScaleSwitchChanged;
        }
        if (_dimSwitch is not null)
        {
            _dimSwitch.IsCheckedChanged -= OnDimSwitchChanged;
        }
        _gridHost?.Children.Remove(_grid);

        _sortSwitch = e.NameScope.Find<SegmentedControl>(SortSwitchPart);
        _scaleSwitch = e.NameScope.Find<SegmentedControl>(ScaleSwitchPart);
        _dimSwitch = e.NameScope.Find<Switch>(DimSwitchPart);
        _gridHost = e.NameScope.Find<Panel>(GridHostPart);
        _focusRing = e.NameScope.Find<Control>(FocusRingPart);
        if (_focusRing is Border ring)
        {
            // The ring lines the bottom of the card, so only its bottom corners are rounded: the RadiusStep18 token (the
            // card's 20 less the ring's 2px inset) without its top corners, so the corner styles reach it too.
            ring.Bind(Border.CornerRadiusProperty, ring.GetResourceObservable(ArcDesignTokenKind.RadiusStep18, BottomCorners),
                BindingPriority.Template);
        }
        _periodText = e.NameScope.Find<TextBlock>(PeriodPart);
        _stateText = e.NameScope.Find<TextBlock>(StatePart);
        _totalText = e.NameScope.Find<TextBlock>(TotalPart);
        _liveRegion = e.NameScope.Find<LineChartsLiveRegion>(LiveRegionPart);

        _gridHost?.Children.Insert(0, _grid);
        UpdateOptions();
        if (_sortSwitch is not null)
        {
            _sortSwitch.SelectionChanged += OnSortSwitchChanged;
        }
        if (_scaleSwitch is not null)
        {
            _scaleSwitch.SelectionChanged += OnScaleSwitchChanged;
        }
        if (_dimSwitch is not null)
        {
            _dimSwitch.IsCheckedChanged += OnDimSwitchChanged;
        }
        UpdateGridTexts();
        _containerWidth = double.NaN;
        Rebuild(flip: false);
        OnGridFocusChanged();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (double.IsFinite(availableSize.Width))
        {
            ApplyContainerWidth(availableSize.Width - BorderThickness.Left - BorderThickness.Right);
        }
        return base.MeasureOverride(availableSize);
    }

    /// <summary>The block's own width picks the layout: four columns from 640, the phone layout below 480.</summary>
    private void ApplyContainerWidth(double width)
    {
        if (width == _containerWidth)
        {
            return;
        }
        _containerWidth = width;
        PseudoClasses.Set(CompactPseudoClass, width < SmallMultiplesMath.FourColumnWidth);
        PseudoClasses.Set(PhonePseudoClass, width < SmallMultiplesMath.PhoneWidth);
        _grid.SetContainerWidth(width);
    }

    // Property changes -----------------------------------------------------------------------------------------------------

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == SortProperty)
        {
            OnSortChanged();
        }
        else if (property == ScaleProperty)
        {
            OnScaleChanged();
        }
        else if (property == IsFlatDimmedProperty)
        {
            OnDimChanged();
        }
        else if (property == SeriesSourceProperty || property == PeriodsSourceProperty || property == GrowthLagProperty ||
                 property == FlatThresholdProperty)
        {
            OnDataChanged();
        }
        else if (property == ValueFormatterProperty || property == TickFormatterProperty || property == FlatTextProperty ||
                 property == CompareFormatProperty || property == UpFormatProperty || property == DownFormatProperty)
        {
            _grid.Text.Clear();
            foreach (var panel in _grid.Ordered)
            {
                panel.UpdateAutomationName();
            }
            _grid.InvalidatePanels();
            UpdateReadout();
        }
        else if (property == LatestSortTextProperty || property == GrowthSortTextProperty || property == NameSortTextProperty ||
                 property == IndependentScaleTextProperty || property == SharedScaleTextProperty || property == SortLabelProperty ||
                 property == ScaleLabelProperty)
        {
            UpdateOptions();
        }
        else if (property == LatestTextProperty || property == PositionFormatProperty)
        {
            UpdateReadout();
        }
        else if (property == PanelsLabelProperty || property == KeyboardHintProperty)
        {
            UpdateGridTexts();
        }
    }

    private void UpdateOptions()
    {
        _syncing = true;
        try
        {
            if (_sortSwitch is not null)
            {
                _sortSwitch.ItemsSource = new[]
                {
                    new SegmentedOption(SmallMultiplesSort.Latest, LatestSortText),
                    new SegmentedOption(SmallMultiplesSort.Growth, GrowthSortText),
                    new SegmentedOption(SmallMultiplesSort.Name, NameSortText),
                };
                _sortSwitch.SelectedIndex = (int)Sort;
                AutomationProperties.SetName(_sortSwitch, SortLabel);
            }
            if (_scaleSwitch is not null)
            {
                _scaleSwitch.ItemsSource = new[]
                {
                    new SegmentedOption(SmallMultiplesScale.Independent, IndependentScaleText),
                    new SegmentedOption(SmallMultiplesScale.Shared, SharedScaleText),
                };
                _scaleSwitch.SelectedIndex = (int)Scale;
                AutomationProperties.SetName(_scaleSwitch, ScaleLabel);
            }
            if (_dimSwitch is not null)
            {
                _dimSwitch.IsChecked = IsFlatDimmed;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void UpdateGridTexts()
    {
        AutomationProperties.SetName(_grid, PanelsLabel);
        AutomationProperties.SetHelpText(_grid, KeyboardHint);
    }

    private void OnSortSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _sortSwitch is { SelectedIndex: >= 0 } s)
        {
            SetCurrentValue(SortProperty, (SmallMultiplesSort)s.SelectedIndex);
        }
    }

    private void OnScaleSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _scaleSwitch is { SelectedIndex: >= 0 } s)
        {
            SetCurrentValue(ScaleProperty, (SmallMultiplesScale)s.SelectedIndex);
        }
    }

    private void OnDimSwitchChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_syncing && _dimSwitch is not null)
        {
            SetCurrentValue(IsFlatDimmedProperty, _dimSwitch.IsChecked == true);
        }
    }

    private void OnSortChanged()
    {
        UpdateOptions();
        var animate = IsLoaded;
        _grid.SetRows(SmallMultiplesMath.Sorted(_rows, Sort, CultureInfo.CurrentCulture), flip: animate);
        var label = Sort switch
        {
            SmallMultiplesSort.Growth => GrowthSortText,
            SmallMultiplesSort.Name => NameSortText,
            _ => LatestSortText,
        };
        if (IsLoaded)
        {
            _liveRegion?.Announce(string.Format(CultureInfo.CurrentCulture, SortAnnouncementFormat, label.ToLower(CultureInfo.CurrentCulture)));
        }
        RaiseEvent(new SmallMultiplesSortChangedEventArgs(SortChangedEvent, Sort));
    }

    private void OnScaleChanged()
    {
        UpdateOptions();
        ApplyScale(IsLoaded);
        if (IsLoaded)
        {
            _liveRegion?.Announce(Scale == SmallMultiplesScale.Shared ? SharedAnnouncement : IndependentAnnouncement);
        }
        RaiseEvent(new SmallMultiplesScaleChangedEventArgs(ScaleChangedEvent, Scale));
    }

    private void OnDimChanged()
    {
        UpdateOptions();
        ApplyDim(IsLoaded);
        if (IsLoaded)
        {
            _liveRegion?.Announce(IsFlatDimmed ? DimOnAnnouncement : DimOffAnnouncement);
        }
    }

    // Data -----------------------------------------------------------------------------------------------------------------

    private void OnDataChanged()
    {
        if (_rebuildQueued)
        {
            return;
        }
        // Several edits in a row (filling a list item by item) rebuild once.
        _rebuildQueued = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _rebuildQueued = false;
            Rebuild(flip: false);
        });
    }

    /// <summary>Rebuilds every row from the data at once (also called by tests to skip the dispatcher).</summary>
    internal void Rebuild(bool flip)
    {
        _rebuildQueued = false;
        ObserveSources();
        var series = SeriesSource is { } source ? source.OfType<SmallMultiplesSeries>().ToList() : Series.ToList();
        _periods = PeriodsSource is { } periods ? periods.OfType<SmallMultiplesPeriod>().ToList() : Periods.ToList();
        var count = _periods.Count > 0 ? _periods.Count : series.Select(s => s.Values?.Count ?? 0).DefaultIfEmpty(0).Max();
        _rows = SmallMultiplesMath.BuildRows(series, count, Math.Max(1, GrowthLag), Math.Max(0, FlatThreshold));
        _shared = SmallMultiplesMath.SharedDomain(_rows);
        _sharedTicks = TicksIn(_shared);
        var totals = SmallMultiplesMath.Totals(_rows, count);
        _grid.PeriodCount = count;
        _grid.Totals = totals;
        _grid.GhostDomain = SmallMultiplesMath.IndependentDomain(totals);
        PseudoClasses.Set(EmptyPseudoClass, _rows.Count == 0 || count == 0);
        foreach (var item in _observedItems)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
        _observedItems.Clear();
        _observedItems.AddRange(series);
        _observedItems.AddRange(_periods);
        foreach (var item in _observedItems)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }

        _grid.SetRows(SmallMultiplesMath.Sorted(_rows, Sort, CultureInfo.CurrentCulture), flip);
        if (_grid.CrosshairIndex is { } index && index >= count)
        {
            _grid.SetCrosshair(null, SmallMultiplesCrosshairSource.Code);
        }
        ApplyScale(animate: false);
        ApplyDim(animate: false);
        UpdateReadout();
    }

    private void ObserveSources()
    {
        var series = SeriesSource as INotifyCollectionChanged;
        if (!ReferenceEquals(series, _observedSeries))
        {
            if (_observedSeries is not null)
            {
                _observedSeries.CollectionChanged -= OnSourceCollectionChanged;
            }
            _observedSeries = series;
            if (series is not null)
            {
                series.CollectionChanged += OnSourceCollectionChanged;
            }
        }
        var periods = PeriodsSource as INotifyCollectionChanged;
        if (!ReferenceEquals(periods, _observedPeriods))
        {
            if (_observedPeriods is not null)
            {
                _observedPeriods.CollectionChanged -= OnSourceCollectionChanged;
            }
            _observedPeriods = periods;
            if (periods is not null)
            {
                periods.CollectionChanged += OnSourceCollectionChanged;
            }
        }
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnDataChanged();

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => OnDataChanged();

    private static double[] TicksIn((double Min, double Max) domain)
    {
        // Three ticks inside the domain, leaving out one that would sit on the plot's bottom edge.
        var floor = domain.Min + (domain.Max - domain.Min) * 0.04;
        return LineChartsFormat.NiceTicks(domain.Min, domain.Max, 3).Where(t => t > floor).ToArray();
    }

    private void ApplyScale(bool animate)
    {
        var shared = Scale == SmallMultiplesScale.Shared;
        foreach (var panel in _grid.Ordered)
        {
            var row = panel.Row;
            panel.SetTicks(TicksIn(row.Independent), _sharedTicks);
            var (min, max) = shared ? _shared : row.Independent;
            panel.SetDomain(min, max, animate);
            panel.SetScale(Scale, animate);
        }
    }

    private void ApplyDim(bool animate)
    {
        foreach (var panel in _grid.Ordered)
        {
            panel.SetDimmed(IsFlatDimmed && panel.Row.IsFlat, animate);
        }
    }

    // Readout and texts ----------------------------------------------------------------------------------------------------

    internal string FormatValueText(double value) =>
        !double.IsFinite(value) ? "–" : ValueFormatter is { } formatter ? formatter(value) : LineChartsFormat.ThousandsOfDollars(value);

    internal string FormatTickText(double value) =>
        TickFormatter is { } formatter ? formatter(value) : FormatValueText(value);

    internal string CompareText(SmallMultiplesRow row)
    {
        var label = row.CompareIndex < _periods.Count ? _periods[row.CompareIndex].ShortText : "";
        return string.Format(CultureInfo.CurrentCulture, CompareFormat, label);
    }

    internal string TrendWords(SmallMultiplesRow row)
    {
        if (!double.IsFinite(row.Growth))
        {
            return "";
        }
        if (row.IsFlat)
        {
            return FlatText;
        }
        var percent = Math.Abs(Math.Round(row.Growth * 100, 1, MidpointRounding.AwayFromZero))
            .ToString("0.0", LineChartsFormat.Culture) + "%";
        return string.Format(CultureInfo.CurrentCulture, row.Growth >= 0 ? UpFormat : DownFormat, percent, CompareText(row));
    }

    private double TotalAt(int index)
    {
        var sum = 0.0;
        var any = false;
        foreach (var row in _rows)
        {
            var v = row.ValueAt(index);
            if (double.IsFinite(v))
            {
                sum += v;
                any = true;
            }
        }
        return any ? sum : double.NaN;
    }

    private void UpdateReadout()
    {
        var count = _grid.PeriodCount;
        var index = _grid.CrosshairIndex ?? count - 1;
        if (_periodText is not null)
        {
            _periodText.Text = index >= 0 && index < _periods.Count ? _periods[index].LongText : "";
        }
        if (_stateText is not null)
        {
            _stateText.Text = _grid.CrosshairIndex is { } i
                ? string.Format(CultureInfo.CurrentCulture, PositionFormat, i + 1, count)
                : LatestText;
        }
        if (_totalText is not null)
        {
            _totalText.Text = index >= 0 ? FormatValueText(TotalAt(index)) : "";
        }
    }

    private void OnCrosshairChanged(int? index, SmallMultiplesCrosshairSource source)
    {
        CrosshairIndex = index;
        UpdateReadout();
        if (source == SmallMultiplesCrosshairSource.Keyboard && index is { } i)
        {
            var top = _rows.Where(r => double.IsFinite(r.ValueAt(i))).OrderByDescending(r => r.ValueAt(i)).FirstOrDefault();
            var period = i < _periods.Count ? _periods[i].LongText : (i + 1).ToString(CultureInfo.CurrentCulture);
            _liveRegion?.Announce(string.Format(CultureInfo.CurrentCulture, CrosshairAnnouncementFormat, period,
                FormatValueText(TotalAt(i)), top?.Name ?? "", top is null ? "" : FormatValueText(top.ValueAt(i))));
        }
        RaiseEvent(new SmallMultiplesIndexChangedEventArgs(CrosshairIndexChangedEvent, index));
    }

    /// <summary>Keeps the bottom corners of a corner radius token value (the focus ring's top edge is straight).</summary>
    private static object? BottomCorners(object? value)
        => value is CornerRadius radius ? new CornerRadius(0, 0, radius.BottomRight, radius.BottomLeft) : AvaloniaProperty.UnsetValue;

    /// <summary>Shows the shared focus ring around the panels while they have keyboard focus.</summary>
    internal void OnGridFocusChanged()
    {
        if (_focusRing is not null)
        {
            _focusRing.IsVisible = _grid.IsFocused && _grid.Classes.Contains(":focus-visible");
        }
    }
}
