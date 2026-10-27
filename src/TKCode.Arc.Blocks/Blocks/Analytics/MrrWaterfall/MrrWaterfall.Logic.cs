using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class MrrWaterfall
{
    internal static readonly StyledProperty<double> StackedWidthProperty =
        AvaloniaProperty.Register<MrrWaterfall, double>(nameof(StackedWidth), 800);

    internal static readonly StyledProperty<double> CompactSummaryWidthProperty =
        AvaloniaProperty.Register<MrrWaterfall, double>(nameof(CompactSummaryWidth), 560);

    internal static readonly StyledProperty<double> HeadlineValueSizeProperty =
        AvaloniaProperty.Register<MrrWaterfall, double>(nameof(HeadlineValueSize), 28);

    internal static readonly StyledProperty<double> CompactHeadlineValueSizeProperty =
        AvaloniaProperty.Register<MrrWaterfall, double>(nameof(CompactHeadlineValueSize), 22);

    internal static readonly StyledProperty<double> StatValueSizeProperty =
        AvaloniaProperty.Register<MrrWaterfall, double>(nameof(StatValueSize), 18);

    internal static readonly StyledProperty<double> CompactStatValueSizeProperty =
        AvaloniaProperty.Register<MrrWaterfall, double>(nameof(CompactStatValueSize), 16);

    internal double StackedWidth { get => GetValue(StackedWidthProperty); set => SetValue(StackedWidthProperty, value); }

    internal double CompactSummaryWidth { get => GetValue(CompactSummaryWidthProperty); set => SetValue(CompactSummaryWidthProperty, value); }

    internal double HeadlineValueSize { get => GetValue(HeadlineValueSizeProperty); set => SetValue(HeadlineValueSizeProperty, value); }

    internal double CompactHeadlineValueSize { get => GetValue(CompactHeadlineValueSizeProperty); set => SetValue(CompactHeadlineValueSizeProperty, value); }

    internal double StatValueSize { get => GetValue(StatValueSizeProperty); set => SetValue(StatValueSizeProperty, value); }

    internal double CompactStatValueSize { get => GetValue(CompactStatValueSizeProperty); set => SetValue(CompactStatValueSizeProperty, value); }

    private MrrWaterfallSplitPanel? _header;
    private TextBlock? _titleBlock;
    private AnalyticsTextSwap? _subtitle;
    private SegmentedControl? _periodSwitch;
    private SegmentedControl? _valueSwitch;
    private MrrWaterfallSplitPanel? _summary;
    private MrrWaterfallHeadline? _headline;
    private MrrWaterfallStats? _stats;
    private MrrWaterfallBody? _body;
    private MrrWaterfallPlot? _plot;
    private MrrWaterfallPanel? _panel;
    private MrrWaterfallDataTable? _table;
    private AnalyticsAnnouncer? _announcer;

    private readonly List<MrrPeriod> _periods = new();
    private INotifyCollectionChanged? _observedSource;
    private MrrWaterfallLabels? _observedLabels;
    private MrrPeriod? _shown;
    private bool _periodsDirty = true;
    private bool _syncing;
    private bool _hasShown;
    private int _panelIndex = -2;
    private bool _panelPreview;
    private MrrFrame _frame = MrrFrame.Empty;

    static MrrWaterfall()
    {
        FocusableProperty.OverrideDefaultValue<MrrWaterfall>(false);
    }

    /// <summary>Creates the block.</summary>
    public MrrWaterfall()
    {
        Periods.CollectionChanged += (_, _) => OnPeriodsChanged();
        ObserveLabels(Labels);
    }

    /// <summary>The plot (tests).</summary>
    internal MrrWaterfallPlot? Plot => _plot;

    /// <summary>The side panel (tests).</summary>
    internal MrrWaterfallPanel? Panel => _panel;

    /// <summary>The headline (tests).</summary>
    internal MrrWaterfallHeadline? Headline => _headline;

    /// <summary>The statistics (tests).</summary>
    internal MrrWaterfallStats? Stats => _stats;

    /// <summary>The live region (tests).</summary>
    internal AnalyticsAnnouncer? Announcer => _announcer;

    /// <summary>The screen reader table (tests).</summary>
    internal MrrWaterfallDataTable? Table => _table;

    /// <summary>The switch of periods (tests).</summary>
    internal SegmentedControl? PeriodSwitch => _periodSwitch;

    /// <summary>The switch of units (tests).</summary>
    internal SegmentedControl? ValueSwitch => _valueSwitch;

    /// <summary>The periods in effect (<see cref="PeriodsSource"/> or <see cref="Periods"/>).</summary>
    public IReadOnlyList<MrrPeriod> EffectivePeriods
    {
        get
        {
            EnsurePeriods();
            return _periods;
        }
    }

    /// <summary>Selects the bar <paramref name="index"/> (0 is the start bar); false when there is no such bar.</summary>
    public bool Select(int index)
    {
        if (index < 0 || index >= _frame.BarCount)
        {
            return false;
        }
        SelectedIndex = index;
        return true;
    }

    /// <summary>Shows the period whose <see cref="MrrPeriod.Key"/> (or label) is <paramref name="key"/>; false when none matches.</summary>
    public bool SelectPeriod(string key)
    {
        var match = EffectivePeriods.FirstOrDefault(p => p.EffectiveKey == key) ?? EffectivePeriods.FirstOrDefault(p => p.Label == key);
        if (match is null)
        {
            return false;
        }
        SelectedPeriod = match;
        return true;
    }

    /// <summary>Moves the keyboard focus to the chart.</summary>
    public void FocusChart() => _plot?.Focus(Avalonia.Input.NavigationMethod.Tab);

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_periodSwitch is not null)
        {
            _periodSwitch.SelectionChanged -= OnPeriodSwitchChanged;
        }
        if (_valueSwitch is not null)
        {
            _valueSwitch.SelectionChanged -= OnValueSwitchChanged;
        }
        if (_plot is not null)
        {
            _plot.SelectionRequested -= OnSelectionRequested;
            _plot.HoverRequested -= OnHoverRequested;
            _plot.CompactChanged -= OnCompactChanged;
        }

        _header = e.NameScope.Find<MrrWaterfallSplitPanel>(HeaderPart);
        _titleBlock = e.NameScope.Find<TextBlock>(TitlePart);
        _subtitle = e.NameScope.Find<AnalyticsTextSwap>(SubtitlePart);
        _periodSwitch = e.NameScope.Find<SegmentedControl>(PeriodSwitchPart);
        _valueSwitch = e.NameScope.Find<SegmentedControl>(ValueSwitchPart);
        _summary = e.NameScope.Find<MrrWaterfallSplitPanel>(SummaryPart);
        _headline = e.NameScope.Find<MrrWaterfallHeadline>(HeadlinePart);
        _stats = e.NameScope.Find<MrrWaterfallStats>(StatsPart);
        _body = e.NameScope.Find<MrrWaterfallBody>(BodyPart);
        _plot = e.NameScope.Find<MrrWaterfallPlot>(PlotPart);
        _panel = e.NameScope.Find<MrrWaterfallPanel>(PanelPart);
        _table = e.NameScope.Find<MrrWaterfallDataTable>(TablePart);
        _announcer = e.NameScope.Find<AnalyticsAnnouncer>(AnnouncerPart);

        if (_periodSwitch is not null)
        {
            _periodSwitch.SelectionChanged += OnPeriodSwitchChanged;
        }
        if (_valueSwitch is not null)
        {
            _valueSwitch.SelectionChanged += OnValueSwitchChanged;
        }
        if (_plot is not null)
        {
            _plot.SelectionRequested += OnSelectionRequested;
            _plot.HoverRequested += OnHoverRequested;
            _plot.CompactChanged += OnCompactChanged;
        }
        _periodsDirty = true;
        _panelIndex = -2;
        _hasShown = false;
        UpdateSwitches();
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
        var content = width - Padding.Left - Padding.Right - BorderThickness.Left - BorderThickness.Right;
        var stacked = content < StackedWidth;
        var compact = content < CompactSummaryWidth;
        PseudoClasses.Set(StackedPseudoClass, stacked);
        PseudoClasses.Set(CompactSummaryPseudoClass, compact);
        if (_body is not null)
        {
            _body.IsStacked = stacked;
        }
        if (_panel is not null)
        {
            _panel.IsStacked = stacked;
        }
        if (_summary is not null)
        {
            _summary.StretchTrail = compact;
        }
        if (_stats is not null)
        {
            _stats.IsStretched = compact;
            _stats.SetValueSize(compact ? CompactStatValueSize : StatValueSize);
        }
        if (_headline is not null)
        {
            _headline.ValueSize = compact ? CompactHeadlineValueSize : HeadlineValueSize;
        }
    }

    private void OnCompactChanged(bool compact)
    {
        PseudoClasses.Set(CompactAxisPseudoClass, compact);
        var tiny = _plot is { } plot && _frame.BarCount > 0 && MrrWaterfallEngine.IsTinyBand(MrrWaterfallEngine.Band(plot.Bounds.Width, _frame.BarCount));
        PseudoClasses.Set(TinyAxisPseudoClass, tiny);
        if (_frame.BarCount > 0 && _plot is { } p && p.IsCompactBand != _compactLabels)
        {
            RefreshPlotFormats();
        }
    }

    private bool _compactLabels;

    #endregion

    #region Property changes

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == PeriodsSourceProperty)
        {
            ObserveSource(change.GetNewValue<IEnumerable?>());
            OnPeriodsChanged();
        }
        else if (property == SelectedPeriodProperty)
        {
            if (!_syncing)
            {
                Refresh(animate: true, announce: true);
            }
        }
        else if (property == ValueModeProperty)
        {
            if (!_syncing)
            {
                var (oldMode, newMode) = (change.GetOldValue<MrrValueMode>(), change.GetNewValue<MrrValueMode>());
                Refresh(animate: true, announce: false);
                UpdateSwitchSelection();
                RaiseEvent(new MrrValueModeChangedEventArgs(ValueModeChangedEvent, oldMode, newMode));
            }
        }
        else if (property == SelectedIndexProperty)
        {
            if (!_syncing)
            {
                OnSelectedIndexChanged(change.GetOldValue<int>(), change.GetNewValue<int>());
            }
        }
        else if (property == LabelsProperty)
        {
            ObserveLabels(change.GetNewValue<MrrWaterfallLabels>());
            OnLabelsChanged(this, EventArgs.Empty);
        }
        else if (property == TitleProperty)
        {
            UpdateAccessibility();
        }
        else if (property == ScopeProperty)
        {
            UpdateSubtitle();
        }
        else if (property == CurrencySymbolProperty || property == CurrencyFormatterProperty || property == CompactFormatterProperty ||
                 property == PercentFormatterProperty || property == MaxNamedAccountsProperty)
        {
            Refresh(animate: false, announce: false);
        }
        else if (property == IsValueModeSwitchVisibleProperty || property == IsPeriodSwitchVisibleProperty)
        {
            UpdateSwitchVisibility();
        }
        else if (property == StackedWidthProperty || property == CompactSummaryWidthProperty || property == HeadlineValueSizeProperty ||
                 property == CompactHeadlineValueSizeProperty || property == StatValueSizeProperty || property == CompactStatValueSizeProperty)
        {
            UpdateBreakpoints(Bounds.Width);
        }
    }

    private void ObserveLabels(MrrWaterfallLabels? labels)
    {
        if (_observedLabels is not null)
        {
            _observedLabels.Changed -= OnLabelsChanged;
        }
        _observedLabels = labels;
        if (labels is not null)
        {
            labels.Changed += OnLabelsChanged;
        }
    }

    private void OnLabelsChanged(object? sender, EventArgs e)
    {
        UpdateSwitches();
        _panelIndex = -2;
        Refresh(animate: false, announce: false);
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

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnPeriodsChanged();

    private void OnPeriodsChanged()
    {
        _periodsDirty = true;
        UpdateSwitches();
        Refresh(animate: false, announce: false);
    }

    private void EnsurePeriods()
    {
        if (!_periodsDirty)
        {
            return;
        }
        _periodsDirty = false;
        foreach (var period in _periods)
        {
            period.Changed -= OnPeriodDataChanged;
        }
        _periods.Clear();
        IEnumerable source = PeriodsSource ?? Periods;
        foreach (var item in source)
        {
            if (item is MrrPeriod period)
            {
                _periods.Add(period);
                period.Changed += OnPeriodDataChanged;
            }
        }
    }

    private void OnPeriodDataChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _shown))
        {
            Refresh(animate: true, announce: false);
        }
        UpdateSwitches();
    }

    #endregion

    #region Data

    private MrrPeriod? EffectivePeriod()
    {
        EnsurePeriods();
        var selected = SelectedPeriod;
        if (selected is not null && _periods.Contains(selected))
        {
            return selected;
        }
        return _periods.Count > 0 ? _periods[^1] : null;
    }

    private void Refresh(bool animate, bool announce)
    {
        var period = EffectivePeriod();
        if (!ReferenceEquals(period, SelectedPeriod))
        {
            _syncing = true;
            SetCurrentValue(SelectedPeriodProperty, period);
            _syncing = false;
        }
        var oldPeriod = _shown;
        var periodChanged = !ReferenceEquals(oldPeriod, period);
        _shown = period;
        PseudoClasses.Set(EmptyPseudoClass, period is null);
        _frame = MrrFrame.Build(period, ValueMode);
        var maxIndex = Math.Max(0, _frame.BarCount - 1);
        // With no data there is nothing to clamp to: keep what was asked for until periods arrive.
        if (_frame.BarCount > 0 && SelectedIndex > maxIndex)
        {
            _syncing = true;
            SetCurrentValue(SelectedIndexProperty, maxIndex);
            _syncing = false;
        }
        var animated = animate && _hasShown;
        _hasShown = true;

        EndingMrr = _frame.EndingMrr;
        NetChange = _frame.NetChange;
        NetChangeRatio = _frame.NetChangeRatio;
        NetRevenueRetention = _frame.NetRevenueRetention;
        QuickRatio = _frame.QuickRatio;
        LogoChurn = _frame.LogoChurn;

        _compactLabels = _plot?.IsCompactBand ?? false;
        if (_plot is not null)
        {
            _plot.SetContent(_frame.PlotContent(Labels), PlotFormats(), animated);
            _plot.SetSelected(Math.Min(SelectedIndex, maxIndex), animated);
        }
        UpdateSummary();
        UpdateSubtitle();
        UpdateSwitchSelection();
        UpdateSwitchVisibility();
        UpdatePanel(animated, force: true);
        UpdateAccessibility();

        if (periodChanged && oldPeriod is not null)
        {
            RaiseEvent(new MrrPeriodChangedEventArgs(PeriodChangedEvent, oldPeriod, period));
            if (PeriodCommand is { } command)
            {
                var args = new MrrPeriodChangedEventArgs(PeriodChangedEvent, oldPeriod, period);
                if (command.CanExecute(args))
                {
                    command.Execute(args);
                }
            }
            if (announce && period is not null)
            {
                _announcer?.Announce(PeriodAnnouncement(period));
            }
        }
    }

    private void RefreshPlotFormats()
    {
        _compactLabels = _plot?.IsCompactBand ?? false;
        _plot?.SetFormats(PlotFormats());
    }

    private string SymbolFor(bool compactBand) => compactBand ? string.Empty : CurrencySymbol;

    private MrrPlotFormats PlotFormats()
    {
        var symbol = CurrencySymbol;
        var percent = ValueMode == MrrValueMode.PercentOfStart && _frame.Start > 0;
        var compactFormatter = CompactFormatter;
        var percentFormatter = PercentFormatter;
        return new MrrPlotFormats
        {
            Tick = (value, step) =>
            {
                if (percent)
                {
                    return percentFormatter?.Invoke(value) ?? AnalyticsFormat.Percent(value, AnalyticsTicks.DecimalsFor(step));
                }
                if (step >= 1000)
                {
                    var thousands = step / 1000;
                    var decimals = Math.Abs(thousands - Math.Round(thousands)) < 1e-9 ? 0 : 1;
                    return AnalyticsFormat.CompactFixed(value, decimals, symbol);
                }
                return AnalyticsFormat.Currency(value, AnalyticsTicks.DecimalsFor(step), symbol);
            },
            Total = (value, compact) => percent
                ? percentFormatter?.Invoke(value) ?? AnalyticsFormat.Percent(value, 1)
                : compactFormatter?.Invoke(value) ?? AnalyticsFormat.CompactFixed(value, 1, SymbolFor(compact)),
            Step = (value, compact) => percent
                ? percentFormatter?.Invoke(value) ?? AnalyticsFormat.Percent(value, 1, signed: true)
                : compactFormatter?.Invoke(value) ?? AnalyticsFormat.CompactFixed(value, 1, SymbolFor(compact), signed: true)
        };
    }

    private string CurrencyText(double value, bool signed)
    {
        if (CurrencyFormatter is { } formatter)
        {
            var text = formatter(value);
            return signed && value > 0 && !text.StartsWith('+') ? "+" + text : text;
        }
        return AnalyticsFormat.Currency(value, 0, CurrencySymbol, signed);
    }

    private string PercentText(double percentUnits, int decimals, bool signed)
    {
        if (PercentFormatter is { } formatter)
        {
            var text = formatter(percentUnits);
            return signed && percentUnits > 0 && !text.StartsWith('+') ? "+" + text : text;
        }
        return AnalyticsFormat.Percent(percentUnits, decimals, signed);
    }

    /// <summary>A bar's amount as text in the current unit (the name screen readers hear, the table).</summary>
    private string AmountText(int bar)
    {
        var f = _frame;
        var total = bar == 0 || bar == f.BarCount - 1;
        var value = total ? (bar == 0 ? f.Levels[0] : f.Levels[^1]) : f.Signed(bar - 1);
        if (f.IsPercent)
        {
            return PercentText(value, 1, !total);
        }
        return CurrencyText(value, !total);
    }

    private string BarTitle(int bar)
    {
        var f = _frame;
        if (bar == 0)
        {
            return Labels.StartTitle;
        }
        if (bar == f.BarCount - 1)
        {
            return Labels.EndTitle;
        }
        return f.Period!.Movements[bar - 1].Label;
    }

    private string BarShortName(int bar)
    {
        var f = _frame;
        if (bar == 0)
        {
            return Labels.StartBar;
        }
        if (bar == f.BarCount - 1)
        {
            return Labels.EndBar;
        }
        var m = f.Period!.Movements[bar - 1];
        return m.ShortLabel ?? m.Label;
    }

    private string CountText(int bar)
    {
        var f = _frame;
        var period = f.Period!;
        if (bar == 0)
        {
            return $"{period.StartingAccounts.ToString("N0", CultureInfo.CurrentCulture)} {period.StartingCountNoun ?? Labels.StartingNoun}";
        }
        if (bar == f.BarCount - 1)
        {
            return $"{period.EndingAccounts.ToString("N0", CultureInfo.CurrentCulture)} {period.EndingCountNoun ?? Labels.EndingNoun}";
        }
        var m = period.Movements[bar - 1];
        return $"{m.AccountCount.ToString("N0", CultureInfo.CurrentCulture)} {m.CountNoun ?? Labels.AccountsNoun}";
    }

    /// <summary>The name of a bar for screen readers: "New, +$55,800, 187 new accounts".</summary>
    internal string BarName(int bar) => _frame.Period is null || bar < 0 || bar >= _frame.BarCount
        ? string.Empty
        : string.Format(CultureInfo.CurrentCulture, Labels.BarNameFormat, bar == 0 || bar == _frame.BarCount - 1 ? BarTitle(bar) : BarShortName(bar),
            AmountText(bar), CountText(bar));

    private string PeriodAnnouncement(MrrPeriod period)
    {
        var up = _frame.NetChange >= 0;
        var ratio = _frame.NetChangeRatio is { } r ? PercentText(Math.Abs(r) * 100, 1, false) : string.Empty;
        return string.Format(CultureInfo.CurrentCulture, Labels.PeriodAnnouncementFormat, period.LongLabel ?? period.Label,
            CurrencyText(_frame.Start, false), CurrencyText(_frame.EndingMrr, false), up ? Labels.UpWord : Labels.DownWord, ratio).TrimEnd();
    }

    private void UpdateSummary()
    {
        var f = _frame;
        var period = f.Period;
        if (_headline is not null)
        {
            if (period is null)
            {
                _headline.Set(string.Empty, 0, AnalyticsNumberFormat.Currency(0, CurrencySymbol), string.Empty, AnalyticsDirection.Flat, AnalyticsTone.Flat, string.Empty);
            }
            else
            {
                var amount = AnalyticsFormat.Currency(f.NetChange, 0, CurrencySymbol, signed: true);
                var text = f.NetChangeRatio is { } ratio ? $"{amount} {AnalyticsFormat.Percent(ratio * 100, 1, signed: true)}" : amount;
                var rounded = Math.Round(f.NetChange, MidpointRounding.AwayFromZero);
                var direction = rounded > 0 ? AnalyticsDirection.Up : rounded < 0 ? AnalyticsDirection.Down : AnalyticsDirection.Flat;
                var tone = rounded > 0 ? AnalyticsTone.Good : rounded < 0 ? AnalyticsTone.Bad : AnalyticsTone.Flat;
                var since = period.StartDateText is { } date ? string.Format(CultureInfo.CurrentCulture, Labels.SinceFormat, date) : string.Empty;
                var label = period.EndDateText is { } end ? string.Format(CultureInfo.CurrentCulture, Labels.EndingFormat, end) : Labels.EndTitle;
                _headline.Set(label, f.EndingMrr, AnalyticsNumberFormat.Currency(0, CurrencySymbol), text, direction, tone, since);
            }
        }
        if (_stats is not null)
        {
            _stats.Retention.Set(Labels.RetentionLabel, f.NetRevenueRetention * 100, AnalyticsNumberFormat.Percent(1));
            _stats.QuickRatio.Set(Labels.QuickRatioLabel, f.QuickRatio, AnalyticsNumberFormat.Number(2));
            _stats.LogoChurn.Set(Labels.LogoChurnLabel, f.LogoChurn * 100, AnalyticsNumberFormat.Percent(1));
        }
    }

    private void UpdateSubtitle()
    {
        if (_subtitle is null)
        {
            return;
        }
        var caption = _shown?.Caption;
        var scope = Scope;
        _subtitle.Text = (scope, caption) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{scope} · {caption}",
            ({ Length: > 0 }, _) => scope,
            (_, { Length: > 0 }) => caption,
            _ => null
        };
        _subtitle.IsVisible = _subtitle.Text is not null;
    }

    #endregion

    #region Switches

    private void UpdateSwitches()
    {
        EnsurePeriods();
        _syncing = true;
        try
        {
            if (_periodSwitch is not null)
            {
                _periodSwitch.ItemsSource = _periods.Select(p => new SegmentedOption(p, p.Label)).ToList();
                AutomationProperties.SetName(_periodSwitch, Labels.PeriodSwitch);
            }
            if (_valueSwitch is not null)
            {
                _valueSwitch.ItemsSource = new List<SegmentedOption>
                {
                    new(MrrValueMode.Currency, Labels.CurrencyOption),
                    new(MrrValueMode.PercentOfStart, Labels.PercentOption)
                };
                AutomationProperties.SetName(_valueSwitch, Labels.ValueSwitch);
            }
        }
        finally
        {
            _syncing = false;
        }
        UpdateSwitchSelection();
        UpdateSwitchVisibility();
    }

    private void UpdateSwitchSelection()
    {
        _syncing = true;
        try
        {
            if (_periodSwitch is not null)
            {
                var index = _shown is null ? -1 : _periods.IndexOf(_shown);
                if (_periodSwitch.SelectedIndex != index)
                {
                    _periodSwitch.SelectedIndex = index;
                }
            }
            if (_valueSwitch is not null)
            {
                var index = ValueMode == MrrValueMode.PercentOfStart ? 1 : 0;
                if (_valueSwitch.SelectedIndex != index)
                {
                    _valueSwitch.SelectedIndex = index;
                }
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void UpdateSwitchVisibility()
    {
        if (_periodSwitch is not null)
        {
            _periodSwitch.IsVisible = IsPeriodSwitchVisible && _periods.Count > 1;
        }
        if (_valueSwitch is not null)
        {
            _valueSwitch.IsVisible = IsValueModeSwitchVisible;
        }
    }

    private void OnPeriodSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _periodSwitch is null)
        {
            return;
        }
        var index = _periodSwitch.SelectedIndex;
        if (index >= 0 && index < _periods.Count)
        {
            SelectedPeriod = _periods[index];
        }
    }

    private void OnValueSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _valueSwitch is null || _valueSwitch.SelectedIndex < 0)
        {
            return;
        }
        ValueMode = _valueSwitch.SelectedIndex == 1 ? MrrValueMode.PercentOfStart : MrrValueMode.Currency;
    }

    #endregion

    #region Selection and the panel

    private void OnSelectionRequested(int index, bool byKeyboard) => SetCurrentValue(SelectedIndexProperty, index);

    private void OnHoverRequested(int index)
    {
        HoveredIndex = index;
        _plot?.SetHover(index);
        UpdatePanel(animate: true, force: false);
    }

    private void OnSelectedIndexChanged(int oldIndex, int newIndex)
    {
        var max = Math.Max(0, _frame.BarCount - 1);
        var clamped = _frame.BarCount > 0 ? Math.Min(newIndex, max) : newIndex;
        if (clamped != newIndex)
        {
            _syncing = true;
            SetCurrentValue(SelectedIndexProperty, clamped);
            _syncing = false;
        }
        _plot?.SetSelected(clamped, animate: true);
        UpdatePanel(animate: true, force: false);
        if (_frame.Period is null)
        {
            return;
        }
        _announcer?.Announce(BarName(clamped));
        var movement = clamped > 0 && clamped < _frame.BarCount - 1 ? _frame.Period.Movements[clamped - 1] : null;
        var args = new MrrSelectionChangedEventArgs(SelectionChangedEvent, oldIndex, clamped, movement, BarShortName(clamped));
        RaiseEvent(args);
        if (SelectionCommand is { } command && command.CanExecute(args))
        {
            command.Execute(args);
        }
    }

    private void UpdatePanel(bool animate, bool force)
    {
        if (_panel is null)
        {
            return;
        }
        var active = HoveredIndex >= 0 ? HoveredIndex : SelectedIndex;
        active = Math.Min(active, Math.Max(0, _frame.BarCount - 1));
        var preview = HoveredIndex >= 0 && HoveredIndex != SelectedIndex;
        if (!force && active == _panelIndex && preview == _panelPreview)
        {
            return;
        }
        _panelIndex = active;
        _panelPreview = preview;
        _panel.SetContent(BuildPanel(active, preview), animate);
    }

    private MrrPanelContent BuildPanel(int bar, bool preview)
    {
        var f = _frame;
        if (f.Period is not { } period || f.BarCount == 0)
        {
            return new MrrPanelContent { Hint = Labels.EmptyText };
        }
        var percent = ValueMode == MrrValueMode.PercentOfStart && f.Start > 0;
        var scale = percent ? 100 / f.Start : 1;
        var hint = preview ? Labels.HoverHint : Labels.Hint;
        var title = BarTitle(bar);
        var total = bar == 0 || bar == f.BarCount - 1;
        var restName = total ? string.Empty : period.Movements[bar - 1].RestLabel ?? Labels.RestLabel;
        string RestDetail(int n) => n == 1 ? Labels.OneAccountText : string.Format(CultureInfo.CurrentCulture, Labels.AccountCountFormat, n);

        IReadOnlyList<MrrAccount> accounts;
        double amount;
        int accountCount;
        MrrTone tone;
        var sign = 1;
        if (bar == 0)
        {
            accounts = period.StartingBreakdown;
            amount = f.Start;
            accountCount = period.StartingAccounts;
            tone = MrrTone.Total;
        }
        else if (bar == f.BarCount - 1)
        {
            accounts = period.EndingBreakdown;
            amount = f.EndingMrr;
            accountCount = period.EndingAccounts;
            tone = MrrTone.Total;
        }
        else
        {
            var m = period.Movements[bar - 1];
            accounts = m.Accounts;
            amount = Math.Abs(m.Amount);
            accountCount = m.AccountCount;
            tone = m.IsGrowth ? MrrTone.Growth : MrrTone.Decrease;
            sign = m.IsGrowth ? 1 : -1;
        }
        var rows = MrrWaterfallEngine.Rows(accounts, amount, accountCount, total ? int.MaxValue : MaxNamedAccounts, !total,
            "__rest", restName, RestDetail);
        if (total)
        {
            // Tier rows without a detail say how many accounts they hold.
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row.Detail is null && row.AccountCount > 0)
                {
                    rows[i] = row with { Detail = RestDetail(row.AccountCount) };
                }
            }
        }
        var signed = !total;
        var format = percent
            ? AnalyticsNumberFormat.Percent(2, signed)
            : AnalyticsNumberFormat.Currency(0, CurrencySymbol, signed);
        var (count, noun) = CountParts(bar);
        return new MrrPanelContent
        {
            Title = title,
            Total = amount * scale * sign,
            Format = format,
            Count = count,
            CountNoun = noun,
            Tone = tone,
            Rows = rows,
            Sign = sign,
            Scale = scale,
            Hint = hint,
            Name = string.Format(CultureInfo.CurrentCulture, Labels.PanelNameFormat, title.ToLower(CultureInfo.CurrentCulture))
        };
    }

    private (int Count, string Noun) CountParts(int bar)
    {
        var period = _frame.Period!;
        if (bar == 0)
        {
            return (period.StartingAccounts, period.StartingCountNoun ?? Labels.StartingNoun);
        }
        if (bar == _frame.BarCount - 1)
        {
            return (period.EndingAccounts, period.EndingCountNoun ?? Labels.EndingNoun);
        }
        var m = period.Movements[bar - 1];
        return (m.AccountCount, m.CountNoun ?? Labels.AccountsNoun);
    }

    #endregion

    #region Accessibility

    private void UpdateAccessibility()
    {
        var f = _frame;
        if (_plot is not null)
        {
            AutomationProperties.SetName(_plot, Summary());
            AutomationProperties.SetHelpText(_plot, Labels.ChartHint);
        }
        if (_table is null)
        {
            return;
        }
        if (f.Period is null)
        {
            _table.Update(Title ?? Labels.EmptyText, [], []);
            return;
        }
        var headers = Labels.TableHeaders.Split(',');
        var rows = new List<string>();
        var cells = new List<string[]>();
        rows.Add(headers.Length > 0 ? headers[0] : string.Empty);
        cells.Add(headers.Skip(1).ToArray());
        for (var i = 0; i < f.BarCount; i++)
        {
            var total = i == 0 || i == f.BarCount - 1;
            var value = total ? (i == 0 ? f.Levels[0] : f.Levels[^1]) : f.Signed(i - 1);
            var share = f.Start > 0 ? AnalyticsFormat.Percent(f.Money(value) / f.Start * 100, 1, !total) : AnalyticsFormat.Missing;
            rows.Add(total ? BarTitle(i) : BarShortName(i));
            cells.Add([AmountText(i), share, CountText(i)]);
        }
        _table.Update(f.Period.LongLabel ?? f.Period.Label, rows.ToArray(), cells.ToArray());
    }

    private string Summary()
    {
        var f = _frame;
        if (f.Period is null)
        {
            return Labels.EmptyText;
        }
        var steps = string.Join(", ", Enumerable.Range(1, f.BarCount - 2).Select(i => $"{BarShortName(i)} {AmountText(i)}"));
        return string.Format(CultureInfo.CurrentCulture, Labels.SummaryFormat, f.Period.LongLabel ?? f.Period.Label, CurrencyText(f.Start, false),
            steps, CurrencyText(f.EndingMrr, false));
    }

    #endregion
}

/// <summary>The numbers of the shown period in the current unit: running levels, bar names and the headline ratios.</summary>
internal sealed class MrrFrame
{
    public static readonly MrrFrame Empty = new();

    private double[] _signed = [];

    public MrrPeriod? Period { get; private init; }

    public MrrValueMode Mode { get; private init; }

    /// <summary>The running levels in the unit shown (currency, or percent of the start).</summary>
    public double[] Levels { get; private init; } = [];

    public double Start { get; private init; }

    /// <summary>Shown units per currency unit: 100 / start in percent mode, else 1.</summary>
    public double Factor { get; private init; } = 1;

    /// <summary>True when the levels are percent of the start.</summary>
    public bool IsPercent => Factor != 1;

    /// <summary>A value in the unit shown back as currency.</summary>
    public double Money(double shown) => shown / Factor;

    public double EndingMrr { get; private init; }

    public double NetChange { get; private init; }

    public double? NetChangeRatio { get; private init; }

    public double? NetRevenueRetention { get; private init; }

    public double? QuickRatio { get; private init; }

    public double? LogoChurn { get; private init; }

    public int BarCount => Period is null ? 0 : Period.Movements.Count + 2;

    /// <summary>The signed amount of movement <paramref name="i"/> in the unit shown.</summary>
    public double Signed(int i) => _signed[i];

    public static MrrFrame Build(MrrPeriod? period, MrrValueMode mode)
    {
        if (period is null)
        {
            return Empty;
        }
        var movements = period.Movements;
        var amounts = new double[movements.Count];
        double added = 0, lost = 0, expansion = 0, contraction = 0, churn = 0;
        var churnAccounts = 0;
        for (var i = 0; i < amounts.Length; i++)
        {
            var m = movements[i];
            amounts[i] = m.SignedAmount;
            switch (m.Kind)
            {
                case MrrMovementKind.Expansion:
                    expansion += Math.Abs(m.Amount);
                    added += Math.Abs(m.Amount);
                    break;
                case MrrMovementKind.Contraction:
                    contraction += Math.Abs(m.Amount);
                    lost += Math.Abs(m.Amount);
                    break;
                case MrrMovementKind.Churn:
                    churn += Math.Abs(m.Amount);
                    lost += Math.Abs(m.Amount);
                    churnAccounts += m.AccountCount;
                    break;
                default:
                    added += Math.Abs(m.Amount);
                    break;
            }
        }
        var levels = new double[amounts.Length + 1];
        MrrWaterfallEngine.Levels(period.StartingMrr, amounts, levels);
        var end = levels[^1];
        var percent = mode == MrrValueMode.PercentOfStart && period.StartingMrr > 0;
        var factor = percent ? 100 / period.StartingMrr : 1;
        var shown = new double[levels.Length];
        for (var i = 0; i < levels.Length; i++)
        {
            shown[i] = levels[i] * factor;
        }
        var signed = new double[amounts.Length];
        for (var i = 0; i < signed.Length; i++)
        {
            signed[i] = amounts[i] * factor;
        }
        return new MrrFrame
        {
            Period = period,
            Mode = mode,
            Factor = factor,
            Levels = shown,
            _signed = signed,
            Start = period.StartingMrr,
            EndingMrr = end,
            NetChange = end - period.StartingMrr,
            NetChangeRatio = period.StartingMrr > 0 ? (end - period.StartingMrr) / period.StartingMrr : null,
            NetRevenueRetention = MrrWaterfallEngine.NetRevenueRetention(period.StartingMrr, expansion, contraction, churn),
            QuickRatio = MrrWaterfallEngine.QuickRatio(added, lost),
            LogoChurn = MrrWaterfallEngine.LogoChurn(churnAccounts, period.StartingAccounts)
        };
    }

    /// <summary>What the plot draws for this frame.</summary>
    public MrrPlotContent PlotContent(MrrWaterfallLabels labels)
    {
        if (Period is null)
        {
            return new MrrPlotContent();
        }
        var movements = Period.Movements;
        var count = BarCount;
        var names = new string[count];
        var compact = new string[count];
        var decrease = new bool[count];
        names[0] = labels.StartBar;
        compact[0] = string.Empty;
        names[count - 1] = labels.EndBar;
        compact[count - 1] = string.Empty;
        for (var i = 0; i < movements.Count; i++)
        {
            var m = movements[i];
            names[i + 1] = m.ShortLabel ?? m.Label;
            compact[i + 1] = m.CompactLabel ?? m.ShortLabel ?? m.Label;
            decrease[i + 1] = !m.IsGrowth;
        }
        return new MrrPlotContent
        {
            Names = names,
            CompactNames = compact,
            Decrease = decrease,
            Levels = Levels,
            Ticks = MrrWaterfallEngine.Ticks(Levels)
        };
    }
}
