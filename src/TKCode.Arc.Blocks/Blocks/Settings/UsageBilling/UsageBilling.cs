using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A metered billing period on one page: what has been spent so far and the projected total, the spend per day, each
/// meter's usage against what the plan includes (with the overage priced), and a spending limit that can be turned on
/// and set in place. The totals follow the meters: change <see cref="UsageBillingMeter.Used"/> and the amounts, bars and
/// projection update. "Today" comes from <see cref="Clock"/>.
/// </summary>
/// <example><code>
/// &lt;arc:UsageBilling PeriodStart="2026-10-01" PeriodEnd="2026-10-31" BaseFee="90" SpendLimit="250" IsSpendLimitEnabled="True"&gt;
///     &lt;arc:UsageBillingMeter Name="API calls" Unit="calls" Used="1240000" Included="1000000" UnitPrice="0.40" PriceUnit="10000" /&gt;
/// &lt;/arc:UsageBilling&gt;
/// </code></example>
[TemplatePart(SummaryGroupPart, typeof(SettingsGroup))]
[TemplatePart(SpentPart, typeof(AnimatedCounter))]
[TemplatePart(ChartPart, typeof(BarChart))]
[TemplatePart(UsageGroupPart, typeof(SettingsGroup))]
[TemplatePart(LimitSwitchPart, typeof(Switch))]
[TemplatePart(LimitEditorPart, typeof(SpringSizeDecorator))]
[TemplatePart(LimitInputPart, typeof(MoneyInput))]
[PseudoClasses(NarrowPseudoClass, NoLimitPseudoClass, LimitNearPseudoClass, LimitReachedPseudoClass)]
public class UsageBilling : TemplatedControl
{
    public const string SummaryGroupPart = "PART_SummaryGroup";
    public const string StatusPart = "PART_Status";
    public const string StatusBadgePart = "PART_StatusBadge";
    public const string SpentPart = "PART_Spent";
    public const string ProjectedPart = "PART_Projected";
    public const string LimitLabelPart = "PART_LimitLabel";
    public const string LimitPercentPart = "PART_LimitPercent";
    public const string LimitBarPart = "PART_LimitBar";
    public const string LimitCaptionPart = "PART_LimitCaption";
    public const string SetLimitPart = "PART_SetLimit";
    public const string DailyGroupPart = "PART_DailyGroup";
    public const string ChartPart = "PART_Chart";
    public const string UsageGroupPart = "PART_UsageGroup";
    public const string UsageTotalLabelPart = "PART_UsageTotalLabel";
    public const string UsageTotalPart = "PART_UsageTotal";
    public const string LimitRowPart = "PART_LimitRow";
    public const string LimitSwitchPart = "PART_LimitSwitch";
    public const string LimitEditorPart = "PART_LimitEditor";
    public const string LimitInputPart = "PART_LimitInput";

    public const string NarrowPseudoClass = ":narrow";
    public const string NoLimitPseudoClass = ":no-limit";
    public const string LimitNearPseudoClass = ":limit-near";
    public const string LimitReachedPseudoClass = ":limit-reached";

    /// <summary>The Lucide "gauge" icon of the limit setting.</summary>
    private static readonly Geometry s_gauge = ArcIcons.Parse("M12 14l4-4M3.34 19a10 10 0 1 1 17.32 0");

    #region Properties

    /// <summary>The first day of the billing period; by default the first day of the <see cref="Clock"/>'s month.</summary>
    public static readonly StyledProperty<DateTime> PeriodStartProperty =
        AvaloniaProperty.Register<UsageBilling, DateTime>(nameof(PeriodStart));

    /// <summary>The last day of the billing period (inclusive); by default the last day of the start's month.</summary>
    public static readonly StyledProperty<DateTime> PeriodEndProperty =
        AvaloniaProperty.Register<UsageBilling, DateTime>(nameof(PeriodEnd));

    /// <summary>ISO 4217 code of every amount ("USD" by default).</summary>
    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<UsageBilling, string>(nameof(Currency), "USD");

    /// <summary>The fixed charge of the period (the plan price), added to the metered charges.</summary>
    public static readonly StyledProperty<decimal> BaseFeeProperty =
        AvaloniaProperty.Register<UsageBilling, decimal>(nameof(BaseFee));

    /// <summary>The meters of the period, in display order. Meters written inline in XAML land here.</summary>
    public static readonly DirectProperty<UsageBilling, IList<UsageBillingMeter>> MetersProperty =
        AvaloniaProperty.RegisterDirect<UsageBilling, IList<UsageBillingMeter>>(nameof(Meters), o => o.Meters, (o, v) => o.Meters = v);

    /// <summary>
    /// The amount spent on each day of the period, starting with the first day. Days after today are not drawn; a null or
    /// empty list hides the daily spend card. A list that raises collection changes redraws as it changes.
    /// </summary>
    public static readonly StyledProperty<IList<double>?> DailySpendProperty =
        AvaloniaProperty.Register<UsageBilling, IList<double>?>(nameof(DailySpend));

    /// <summary>The spending limit for the period, in <see cref="Currency"/>. Two-way: the limit field edits it.</summary>
    public static readonly StyledProperty<decimal?> SpendLimitProperty =
        AvaloniaProperty.Register<UsageBilling, decimal?>(nameof(SpendLimit), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the spending limit applies. Two-way: the limit switch edits it.</summary>
    public static readonly StyledProperty<bool> IsSpendLimitEnabledProperty =
        AvaloniaProperty.Register<UsageBilling, bool>(nameof(IsSpendLimitEnabled), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The share of the limit (0–1, default 0.8) at which the progress turns to the warning color and the tick sits.</summary>
    public static readonly StyledProperty<double> AlertThresholdProperty =
        AvaloniaProperty.Register<UsageBilling, double>(nameof(AlertThreshold), 0.8, coerce: CoerceThreshold);

    /// <summary>The built-in text and its culture; replace the object to localize the block.</summary>
    public static readonly StyledProperty<UsageBillingLabels> LabelsProperty =
        AvaloniaProperty.Register<UsageBilling, UsageBillingLabels>(nameof(Labels), new UsageBillingLabels());

    /// <summary>The sum of every meter's <see cref="UsageBillingMeter.Cost"/>.</summary>
    public static readonly DirectProperty<UsageBilling, decimal> MeteredTotalProperty =
        AvaloniaProperty.RegisterDirect<UsageBilling, decimal>(nameof(MeteredTotal), o => o.MeteredTotal);

    /// <summary>What the period has cost so far: <see cref="BaseFee"/> + <see cref="MeteredTotal"/>.</summary>
    public static readonly DirectProperty<UsageBilling, decimal> CurrentTotalProperty =
        AvaloniaProperty.RegisterDirect<UsageBilling, decimal>(nameof(CurrentTotal), o => o.CurrentTotal);

    /// <summary>
    /// The period's expected total if usage keeps its pace: <see cref="BaseFee"/> + <see cref="MeteredTotal"/> ÷ the share
    /// of the period's days that have begun by <see cref="Clock"/>'s today (kept between one day's share and 1).
    /// </summary>
    public static readonly DirectProperty<UsageBilling, decimal> ProjectedTotalProperty =
        AvaloniaProperty.RegisterDirect<UsageBilling, decimal>(nameof(ProjectedTotal), o => o.ProjectedTotal);

    /// <summary>Where <see cref="CurrentTotal"/> stands against the spending limit and its alert threshold.</summary>
    public static readonly DirectProperty<UsageBilling, UsageBillingLimitStatus> LimitStatusProperty =
        AvaloniaProperty.RegisterDirect<UsageBilling, UsageBillingLimitStatus>(nameof(LimitStatus), o => o.LimitStatus);

    /// <summary>Block width below which the spend and the limit stack (theme token).</summary>
    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<UsageBilling, double>(nameof(NarrowWidth), 600);

    /// <summary>
    /// Raised when the spending limit changes or is turned on or off. While someone types in the limit field the change
    /// is reported once, when the field loses focus or Enter is pressed.
    /// </summary>
    public static readonly RoutedEvent<UsageBillingLimitChangedEventArgs> SpendLimitChangedEvent =
        RoutedEvent.Register<UsageBilling, UsageBillingLimitChangedEventArgs>(nameof(SpendLimitChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="PeriodStartProperty"/>
    public DateTime PeriodStart
    {
        get => GetValue(PeriodStartProperty);
        set => SetValue(PeriodStartProperty, value);
    }

    /// <inheritdoc cref="PeriodEndProperty"/>
    public DateTime PeriodEnd
    {
        get => GetValue(PeriodEndProperty);
        set => SetValue(PeriodEndProperty, value);
    }

    /// <inheritdoc cref="CurrencyProperty"/>
    public string Currency
    {
        get => GetValue(CurrencyProperty);
        set => SetValue(CurrencyProperty, value);
    }

    /// <inheritdoc cref="BaseFeeProperty"/>
    public decimal BaseFee
    {
        get => GetValue(BaseFeeProperty);
        set => SetValue(BaseFeeProperty, value);
    }

    /// <inheritdoc cref="MetersProperty"/>
    [Content]
    public IList<UsageBillingMeter> Meters
    {
        get => _meters;
        set
        {
            var owns = value is null;
            if (SetAndRaise(MetersProperty, ref _meters, value ?? new AvaloniaList<UsageBillingMeter>()))
            {
                _ownsMeters = owns;
                WatchMeters();
                SyncMeters();
            }
        }
    }

    /// <inheritdoc cref="DailySpendProperty"/>
    public IList<double>? DailySpend
    {
        get => GetValue(DailySpendProperty);
        set => SetValue(DailySpendProperty, value);
    }

    /// <inheritdoc cref="SpendLimitProperty"/>
    public decimal? SpendLimit
    {
        get => GetValue(SpendLimitProperty);
        set => SetValue(SpendLimitProperty, value);
    }

    /// <inheritdoc cref="IsSpendLimitEnabledProperty"/>
    public bool IsSpendLimitEnabled
    {
        get => GetValue(IsSpendLimitEnabledProperty);
        set => SetValue(IsSpendLimitEnabledProperty, value);
    }

    /// <inheritdoc cref="AlertThresholdProperty"/>
    public double AlertThreshold
    {
        get => GetValue(AlertThresholdProperty);
        set => SetValue(AlertThresholdProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public UsageBillingLabels Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <inheritdoc cref="MeteredTotalProperty"/>
    public decimal MeteredTotal
    {
        get => _meteredTotal;
        private set => SetAndRaise(MeteredTotalProperty, ref _meteredTotal, value);
    }

    /// <inheritdoc cref="CurrentTotalProperty"/>
    public decimal CurrentTotal
    {
        get => _currentTotal;
        private set => SetAndRaise(CurrentTotalProperty, ref _currentTotal, value);
    }

    /// <inheritdoc cref="ProjectedTotalProperty"/>
    public decimal ProjectedTotal
    {
        get => _projectedTotal;
        private set => SetAndRaise(ProjectedTotalProperty, ref _projectedTotal, value);
    }

    /// <inheritdoc cref="LimitStatusProperty"/>
    public UsageBillingLimitStatus LimitStatus
    {
        get => _limitStatus;
        private set => SetAndRaise(LimitStatusProperty, ref _limitStatus, value);
    }

    internal double NarrowWidth
    {
        get => GetValue(NarrowWidthProperty);
        set => SetValue(NarrowWidthProperty, value);
    }

    /// <summary>Where "today" comes from (projection, days left, which days are drawn); <see cref="TimeProvider.System"/> by default.</summary>
    public TimeProvider Clock
    {
        get => _clock;
        set
        {
            _clock = value ?? TimeProvider.System;
            Refresh();
            RefreshChart();
            ScheduleDayChange();
        }
    }

    /// <inheritdoc cref="SpendLimitChangedEvent"/>
    public event EventHandler<UsageBillingLimitChangedEventArgs>? SpendLimitChanged
    {
        add => AddHandler(SpendLimitChangedEvent, value);
        remove => RemoveHandler(SpendLimitChangedEvent, value);
    }

    #endregion

    /// <summary>One meter's row in the usage card: the shared settings row, its charge and the bar under it.</summary>
    private sealed class MeterView
    {
        public required UsageBillingMeter Meter { get; init; }
        public required SettingsRow Row { get; init; }
        public required UsageBillingMeterCharge Charge { get; init; }
        public required UsageBillingBar Bar { get; init; }
    }

    /// <summary>The effective period and how far into it today is.</summary>
    private readonly record struct Span(DateTime Start, DateTime End, DateTime Today, int TotalDays, int ElapsedDays)
    {
        public int DaysLeft => (End - Today).Days;
    }

    private IList<UsageBillingMeter> _meters = new AvaloniaList<UsageBillingMeter>();
    private bool _ownsMeters = true;
    private INotifyCollectionChanged? _watchedMeters;
    // Meters and days a host hands in can outlive the block (a view model's): the block follows them, and each meter's
    // changes, only while it is in a visual tree, and reads them again when it comes back. Its own meter list is always
    // followed (the totals keep up with meters added before the block is shown).
    private bool _watching;
    private readonly List<MeterView> _views = new();
    private readonly HashSet<UsageBillingMeter> _observed = new();
    private TimeProvider _clock = TimeProvider.System;
    private ITimer? _dayTimer;
    private INotifyCollectionChanged? _observedDaily;
    private decimal _meteredTotal;
    private decimal _currentTotal;
    private decimal _projectedTotal;
    private UsageBillingLimitStatus _limitStatus;
    private bool _isNarrow;
    private bool _limitEditPending;
    private decimal? _limitEditStart;
    private ChartPoint[] _points = [];
    private double _chartScale = 100;

    private SettingsGroup? _summaryGroup;
    private PresenceHost? _status;
    private Badge? _statusBadge;
    private AnimatedCounter? _spent;
    private TextBlock? _projected;
    private TextBlock? _limitLabel;
    private TextBlock? _limitPercent;
    private UsageBillingBar? _limitBar;
    private TextBlock? _limitCaption;
    private AvaloniaButton? _setLimit;
    private SettingsGroup? _dailyGroup;
    private BarChart? _chart;
    private SettingsGroup? _usageGroup;
    private TextBlock? _usageTotalLabel;
    private TextBlock? _usageTotal;
    private SettingsRow? _limitRow;
    private SpringSizeDecorator? _limitEditor;
    private MoneyInput? _limitInput;

    static UsageBilling()
    {
        FocusableProperty.OverrideDefaultValue<UsageBilling>(false);
    }

    public UsageBilling()
    {
        WatchMeters();
        UpdateLimitPseudoClasses();
    }

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_setLimit is not null)
        {
            _setLimit.Click -= OnSetLimitClick;
        }
        if (_limitInput is not null)
        {
            _limitInput.PropertyChanged -= OnLimitInputPropertyChanged;
            _limitInput.RemoveHandler(KeyDownEvent, OnLimitInputKeyDown);
        }
        _usageGroup?.Items.Clear();
        base.OnApplyTemplate(e);

        var scope = e.NameScope;
        _summaryGroup = scope.Find<SettingsGroup>(SummaryGroupPart);
        _status = scope.Find<PresenceHost>(StatusPart);
        _statusBadge = scope.Find<Badge>(StatusBadgePart);
        _spent = scope.Find<AnimatedCounter>(SpentPart);
        _projected = scope.Find<TextBlock>(ProjectedPart);
        _limitLabel = scope.Find<TextBlock>(LimitLabelPart);
        _limitPercent = scope.Find<TextBlock>(LimitPercentPart);
        _limitBar = scope.Find<UsageBillingBar>(LimitBarPart);
        _limitCaption = scope.Find<TextBlock>(LimitCaptionPart);
        _setLimit = scope.Find<AvaloniaButton>(SetLimitPart);
        _dailyGroup = scope.Find<SettingsGroup>(DailyGroupPart);
        _chart = scope.Find<BarChart>(ChartPart);
        _usageGroup = scope.Find<SettingsGroup>(UsageGroupPart);
        _usageTotalLabel = scope.Find<TextBlock>(UsageTotalLabelPart);
        _usageTotal = scope.Find<TextBlock>(UsageTotalPart);
        _limitRow = scope.Find<SettingsRow>(LimitRowPart);
        _limitEditor = scope.Find<SpringSizeDecorator>(LimitEditorPart);
        _limitInput = scope.Find<MoneyInput>(LimitInputPart);

        if (_setLimit is not null)
        {
            _setLimit.Click += OnSetLimitClick;
        }
        if (_limitInput is not null)
        {
            _limitInput.PropertyChanged += OnLimitInputPropertyChanged;
            _limitInput.AddHandler(KeyDownEvent, OnLimitInputKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
            // A limit is set once and adjusted rarely: no quick add chips, the field stays a calm single line.
            _limitInput.QuickAddAmounts = Array.Empty<decimal>();
        }
        if (_limitRow is not null)
        {
            _limitRow.Icon = s_gauge;
        }
        UpdateEditorNavigation();
        if (_chart is not null)
        {
            _chart.ValueFormatter = FormatChartValue;
            _chart.TickFormatter = FormatChartTick;
            _chart.XLabelFormatter = FormatDayLabel;
            _chart.XAxisFormatter = FormatDayAxis;
        }
        if (_usageGroup is not null)
        {
            foreach (var view in _views)
            {
                _usageGroup.Items.Add(view.Row);
                _usageGroup.Items.Add(view.Bar);
            }
        }
        _points = [];
        Refresh();
        RefreshChart();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The width is given by the parent, not by the content, so switching layouts here cannot oscillate.
        if (!double.IsInfinity(availableSize.Width))
        {
            var narrow = availableSize.Width < NarrowWidth;
            if (narrow != _isNarrow)
            {
                _isNarrow = narrow;
                PseudoClasses.Set(NarrowPseudoClass, narrow);
            }
        }
        return base.MeasureOverride(availableSize);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watching = true;
        WatchMeters();
        WatchDaily();
        // What changed while the block was away: SyncMeters follows the meters again, updates their rows and refreshes.
        SyncMeters();
        RefreshChart();
        ScheduleDayChange();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        FlushLimitEdit();
        _dayTimer?.Dispose();
        _dayTimer = null;
        _watching = false;
        WatchMeters();
        WatchDaily();
        foreach (var meter in _observed)
        {
            meter.PropertyChanged -= OnMeterPropertyChanged;
        }
        _observed.Clear();
    }

    private void WatchMeters()
    {
        var wanted = _ownsMeters || _watching ? _meters as INotifyCollectionChanged : null;
        if (ReferenceEquals(wanted, _watchedMeters))
        {
            return;
        }
        if (_watchedMeters is not null)
        {
            _watchedMeters.CollectionChanged -= OnMetersChanged;
        }
        _watchedMeters = wanted;
        if (_watchedMeters is not null)
        {
            _watchedMeters.CollectionChanged += OnMetersChanged;
        }
    }

    private void WatchDaily()
    {
        if (_observedDaily is not null)
        {
            _observedDaily.CollectionChanged -= OnDailySpendChanged;
        }
        _observedDaily = _watching ? DailySpend as INotifyCollectionChanged : null;
        if (_observedDaily is not null)
        {
            _observedDaily.CollectionChanged += OnDailySpendChanged;
        }
    }

    #endregion

    #region Changes

    private static double CoerceThreshold(AvaloniaObject owner, double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0.8;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == SpendLimitProperty)
        {
            Refresh();
            OnSpendLimitChanged(change.GetOldValue<decimal?>());
        }
        else if (property == IsSpendLimitEnabledProperty)
        {
            Refresh();
            UpdateEditorNavigation();
            OnLimitEnabledChanged();
        }
        else if (property == BaseFeeProperty || property == AlertThresholdProperty || property == CurrencyProperty)
        {
            Refresh();
            if (property == CurrencyProperty)
            {
                UpdateMeterViews();
                RefreshChart();
            }
        }
        else if (property == PeriodStartProperty || property == PeriodEndProperty)
        {
            Refresh();
            RefreshChart();
        }
        else if (property == LabelsProperty)
        {
            Refresh();
            UpdateMeterViews();
            RefreshChart();
        }
        else if (property == DailySpendProperty)
        {
            WatchDaily();
            RefreshChart();
        }
        else if (property == NarrowWidthProperty)
        {
            InvalidateMeasure();
        }
    }

    private void OnDailySpendChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshChart();

    private void OnMetersChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncMeters();

    private void OnMeterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not UsageBillingMeter meter)
        {
            return;
        }
        foreach (var view in _views)
        {
            if (ReferenceEquals(view.Meter, meter))
            {
                UpdateMeterView(view);
            }
        }
        Refresh();
    }

    /// <summary>
    /// Follows <see cref="Meters"/>: observes each meter once, reuses the row of a meter that stays, builds rows for new
    /// meters and lays them out in list order.
    /// </summary>
    private void SyncMeters()
    {
        var current = new HashSet<UsageBillingMeter>();
        foreach (var meter in _meters)
        {
            if (meter is not null)
            {
                current.Add(meter);
            }
        }
        foreach (var gone in _observed.Where(m => !current.Contains(m)).ToList())
        {
            gone.PropertyChanged -= OnMeterPropertyChanged;
            _observed.Remove(gone);
        }
        if (_watching)
        {
            foreach (var meter in current)
            {
                if (_observed.Add(meter))
                {
                    meter.PropertyChanged += OnMeterPropertyChanged;
                }
            }
        }

        var spare = new List<MeterView>(_views);
        var next = new List<MeterView>(_meters.Count);
        foreach (var meter in _meters)
        {
            if (meter is null)
            {
                continue;
            }
            var index = spare.FindIndex(v => ReferenceEquals(v.Meter, meter));
            MeterView view;
            if (index >= 0)
            {
                view = spare[index];
                spare.RemoveAt(index);
            }
            else
            {
                view = CreateMeterView(meter);
            }
            UpdateMeterView(view);
            next.Add(view);
        }
        var sameOrder = next.Count == _views.Count && !next.Where((v, i) => !ReferenceEquals(v, _views[i])).Any();
        _views.Clear();
        _views.AddRange(next);
        if (!sameOrder && _usageGroup is not null)
        {
            _usageGroup.Items.Clear();
            foreach (var view in _views)
            {
                _usageGroup.Items.Add(view.Row);
                _usageGroup.Items.Add(view.Bar);
            }
        }
        Refresh();
    }

    private static MeterView CreateMeterView(UsageBillingMeter meter)
    {
        var charge = new UsageBillingMeterCharge();
        // Never stacks: the charge is short and reads best on the right, beside the meter's name.
        var row = new SettingsRow { Content = charge, StackWidth = 0 };
        var bar = new UsageBillingBar();
        bar.Classes.Add("meter");
        return new MeterView { Meter = meter, Row = row, Charge = charge, Bar = bar };
    }

    private void UpdateMeterViews()
    {
        foreach (var view in _views)
        {
            UpdateMeterView(view);
        }
    }

    private void UpdateMeterView(MeterView view)
    {
        var meter = view.Meter;
        var labels = Labels;
        var culture = labels.Culture;
        var used = SettingsFormat.Compact(meter.Used, culture);
        var description = meter.Included > 0
            ? labels.MeterUsage(used, SettingsFormat.Compact(meter.Included, culture), meter.Unit)
            : labels.MeterUsageOnly(used, meter.Unit);
        view.Row.Header = meter.Name;
        view.Row.Description = description;
        view.Row.Icon = meter.Icon;
        view.Bar.Classes.Set("indented", meter.Icon is not null);

        var cost = meter.Cost;
        var included = cost <= 0;
        var amount = included ? labels.Included : SettingsFormat.Money(cost, Currency, culture);
        var per = meter.PriceUnit == 1
            ? meter.Unit
            : $"{SettingsFormat.Compact(meter.PriceUnit, culture)} {meter.Unit}".TrimEnd();
        var rate = meter.UnitPrice > 0 ? labels.Rate(UsageBillingMoney.Rate(meter.UnitPrice, Currency, culture), per) : null;
        view.Charge.Amount = amount;
        view.Charge.Rate = rate;
        view.Charge.IsIncluded = included;
        AutomationProperties.SetName(view.Charge, rate is null ? amount : $"{amount}, {rate}");

        // The bar spans max(Used, Included): the included part in the accent, the overage in the warning color. A meter
        // that includes nothing has nothing to measure against, so it shows no bar.
        view.Bar.IsVisible = meter.Included > 0;
        var span = Math.Max(meter.Used, meter.Included);
        var fill = span > 0 ? meter.Used / span : 0;
        var split = meter.Overage > 0 && span > 0 ? meter.Included / span : 1;
        view.Bar.Show(fill, split, double.NaN);
        var barName = meter.Overage > 0
            ? $"{meter.Name}: {description}, {labels.Overage(SettingsFormat.Compact(meter.Overage, culture))}"
            : $"{meter.Name}: {description}";
        AutomationProperties.SetName(view.Bar, barName);
    }

    #endregion

    #region Totals and summary

    private Span CurrentSpan()
    {
        var today = _clock.GetLocalNow().Date;
        var start = PeriodStart == default ? new DateTime(today.Year, today.Month, 1) : PeriodStart.Date;
        var end = PeriodEnd == default || PeriodEnd.Date < start ? start.AddMonths(1).AddDays(-1) : PeriodEnd.Date;
        var total = (end - start).Days + 1;
        var elapsed = Math.Clamp((today - start).Days + 1, 1, total);
        return new Span(start, end, today, total, elapsed);
    }

    /// <summary>Recomputes the totals, the projection and the limit status, then the summary and footer text.</summary>
    private void Refresh()
    {
        decimal metered = 0;
        foreach (var meter in _meters)
        {
            if (meter is not null)
            {
                metered += meter.Cost;
            }
        }
        var span = CurrentSpan();
        var baseFee = BaseFee;
        MeteredTotal = metered;
        CurrentTotal = baseFee + metered;
        ProjectedTotal = baseFee + metered * span.TotalDays / span.ElapsedDays;

        var limit = EffectiveLimit;
        var current = CurrentTotal;
        LimitStatus = limit is not { } l
            ? UsageBillingLimitStatus.None
            : current >= l
                ? UsageBillingLimitStatus.Reached
                : (double)current >= AlertThreshold * (double)l
                    ? UsageBillingLimitStatus.Near
                    : UsageBillingLimitStatus.Ok;
        UpdateLimitPseudoClasses();
        UpdateSummary(span);
    }

    /// <summary>The limit amount when the limit is on and positive.</summary>
    private decimal? EffectiveLimit => IsSpendLimitEnabled && SpendLimit is { } limit && limit > 0 ? limit : null;

    private void UpdateLimitPseudoClasses()
    {
        var status = _limitStatus;
        PseudoClasses.Set(NoLimitPseudoClass, status == UsageBillingLimitStatus.None);
        PseudoClasses.Set(LimitNearPseudoClass, status == UsageBillingLimitStatus.Near);
        PseudoClasses.Set(LimitReachedPseudoClass, status == UsageBillingLimitStatus.Reached);
    }

    private string Money(decimal amount) => SettingsFormat.Money(amount, Currency, Labels.Culture);

    private void UpdateSummary(Span span)
    {
        var labels = Labels;
        var culture = labels.Culture;
        if (_summaryGroup is not null)
        {
            var left = span.Today > span.End ? labels.PeriodEnded : labels.DaysLeft(span.DaysLeft);
            _summaryGroup.Header = labels.PeriodTitle;
            _summaryGroup.Description = $"{RangeText(span.Start, span.End, culture)} · {left}";
        }
        if (_spent is not null)
        {
            var shape = UsageBillingMoney.ShapeOf(Currency, culture);
            _spent.Label = labels.SpentSoFar;
            _spent.Prefix = shape.Prefix.Length > 0 ? shape.Prefix : null;
            _spent.Suffix = shape.Suffix.Length > 0 ? shape.Suffix : null;
            _spent.Decimals = shape.Decimals;
            _spent.CultureName = culture.Name;
            _spent.Value = (double)CurrentTotal;
        }
        if (_projected is not null)
        {
            _projected.Text = labels.Projected(Money(ProjectedTotal));
        }

        var status = _limitStatus;
        if (_status is not null)
        {
            _status.IsPresent = status is UsageBillingLimitStatus.Near or UsageBillingLimitStatus.Reached;
        }
        if (_statusBadge is not null && status is UsageBillingLimitStatus.Near or UsageBillingLimitStatus.Reached)
        {
            var reached = status == UsageBillingLimitStatus.Reached;
            _statusBadge.Tone = reached ? BadgeTone.Danger : BadgeTone.Warning;
            _statusBadge.Content = reached ? labels.LimitReached : labels.NearLimit;
        }

        if (_limitLabel is not null)
        {
            _limitLabel.Text = labels.SpendingLimit;
        }
        if (_setLimit is not null)
        {
            _setLimit.Content = labels.SetLimit;
        }
        var limit = EffectiveLimit;
        if (limit is { } l)
        {
            var share = (double)(CurrentTotal / l);
            if (_limitPercent is not null)
            {
                _limitPercent.Text = Math.Min(share, 9.99).ToString("P0", culture);
            }
            if (_limitCaption is not null)
            {
                _limitCaption.Text = labels.LimitProgress(Money(CurrentTotal), Money(l));
            }
            if (_limitBar is not null)
            {
                _limitBar.Show(share, 1, AlertThreshold is > 0 and < 1 ? AlertThreshold : double.NaN);
                AutomationProperties.SetName(_limitBar,
                    $"{labels.SpendingLimit}: {labels.LimitProgress(Money(CurrentTotal), Money(l))}");
            }
        }
        else if (_limitCaption is not null)
        {
            _limitCaption.Text = labels.NoLimit;
        }

        if (_usageGroup is not null)
        {
            _usageGroup.Header = labels.Usage;
            _usageGroup.IsVisible = _views.Count > 0;
        }
        if (_usageTotalLabel is not null)
        {
            _usageTotalLabel.Text = labels.UsageTotal;
        }
        if (_usageTotal is not null)
        {
            _usageTotal.Text = Money(MeteredTotal);
        }
        if (_limitRow is not null)
        {
            _limitRow.Header = labels.SpendingLimit;
            _limitRow.Description = labels.SpendingLimitDescription;
        }
        if (_limitInput is not null)
        {
            _limitInput.Label = labels.LimitAmount;
            _limitInput.Description = labels.LimitNote((int)Math.Round(AlertThreshold * 100));
            _limitInput.Culture = culture;
            var code = string.IsNullOrWhiteSpace(Currency) ? "USD" : Currency.Trim().ToUpperInvariant();
            if (!string.Equals(_limitInput.Currency, code, StringComparison.Ordinal) || _limitInput.Currencies.Count != 1)
            {
                _limitInput.Currencies = Array.AsReadOnly(new[] { code });
                _limitInput.Currency = code;
            }
        }
    }

    /// <summary>"Oct 1 – Oct 31, 2026", or with both years when the period crosses a year.</summary>
    private static string RangeText(DateTime start, DateTime end, CultureInfo culture) =>
        start.Year == end.Year
            ? $"{SettingsFormat.ShortDate(start, culture)} – {SettingsFormat.Date(end, culture)}"
            : $"{SettingsFormat.Date(start, culture)} – {SettingsFormat.Date(end, culture)}";

    #endregion

    #region Daily spend

    /// <summary>
    /// Feeds the chart the days from the first through today, dated, in minor units (cents): the chart rounds its average
    /// to a whole number before formatting it, so whole minor units keep the average exact to the cent.
    /// </summary>
    private void RefreshChart()
    {
        if (_chart is null)
        {
            return;
        }
        var labels = Labels;
        var span = CurrentSpan();
        var days = DailySpend;
        var today = span.Today < span.Start ? 0 : Math.Min((span.Today - span.Start).Days + 1, span.TotalDays);
        var count = days is null ? 0 : Math.Min(days.Count, today);
        var scale = Math.Pow(10, UsageBillingMoney.ShapeOf(Currency, labels.Culture).Decimals);
        if (_points.Length != count || scale != _chartScale || !SamePoints(days!, span.Start, count, scale))
        {
            var points = new ChartPoint[count];
            for (var i = 0; i < count; i++)
            {
                points[i] = new ChartPoint(span.Start.AddDays(i), MinorUnits(days![i], scale));
            }
            _points = points;
            _chartScale = scale;
            _chart.ItemsSource = points;
        }
        _chart.Label = labels.DailySpend;
        _chart.AverageLabel = labels.DailyAverage;
        _chart.ValueLabel = labels.DaySpend;
        _chart.CategoryLabel = labels.Day;
        _chart.Period = count > 0 ? RangeText(span.Start, span.Start.AddDays(count - 1), labels.Culture) : string.Empty;
        if (_dailyGroup is not null)
        {
            _dailyGroup.IsVisible = count > 0;
        }
    }

    private static double MinorUnits(double value, double scale) =>
        double.IsFinite(value) ? Math.Round(Math.Max(0, value) * scale) : 0;

    private bool SamePoints(IList<double> days, DateTime start, int count, double scale)
    {
        for (var i = 0; i < count; i++)
        {
            var point = _points[i];
            if (point.X is not DateTime date || date != start.AddDays(i) || point.Value != MinorUnits(days[i], scale))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// A chart value (minor units) as money: <c>$7.45</c>. The average (index -1) also labels the reference line in the
    /// narrow value gutter, so from 100 up it drops the cents: <c>$123</c>.
    /// </summary>
    private string FormatChartValue(ChartValueContext context)
    {
        var amount = FromMinorUnits(context.Value);
        return context.Index < 0 && Math.Abs(amount) >= 100
            ? SettingsFormat.MoneyShort(Math.Round(amount), Currency, Labels.Culture)
            : SettingsFormat.Money(amount, Currency, Labels.Culture);
    }

    /// <summary>An axis tick (minor units) as money without empty cents: <c>$4</c>.</summary>
    private string FormatChartTick(double value) =>
        SettingsFormat.MoneyShort(FromMinorUnits(value), Currency, Labels.Culture);

    private decimal FromMinorUnits(double value) =>
        double.IsFinite(value) && _chartScale > 0 ? (decimal)value / (decimal)_chartScale : 0m;

    private string FormatDayLabel(object? x) => x is DateTime date ? date.ToString("ddd, MMM d", Labels.Culture) : x?.ToString() ?? string.Empty;

    private string? FormatDayAxis(object? x) => x is DateTime date ? SettingsFormat.ShortDate(date, Labels.Culture) : x?.ToString();

    #endregion

    #region Spending limit

    private void OnSpendLimitChanged(decimal? oldLimit)
    {
        if (!IsInitialized)
        {
            return;
        }
        // Typing reports once per edit: the first value of the edit is kept until the field lets go.
        if (_limitInput is { IsKeyboardFocusWithin: true })
        {
            if (!_limitEditPending)
            {
                _limitEditPending = true;
                _limitEditStart = oldLimit;
            }
            return;
        }
        RaiseLimitChanged(oldLimit, SpendLimit);
    }

    private void OnLimitEnabledChanged()
    {
        if (!IsInitialized)
        {
            return;
        }
        FlushLimitEdit();
        RaiseLimitChanged(SpendLimit, SpendLimit);
    }

    /// <summary>A collapsed editor keeps its field (it only has no height), so Tab must skip it while the limit is off.</summary>
    private void UpdateEditorNavigation()
    {
        if (_limitEditor is not null)
        {
            KeyboardNavigation.SetTabNavigation(_limitEditor,
                IsSpendLimitEnabled ? KeyboardNavigationMode.Continue : KeyboardNavigationMode.None);
        }
    }

    private void FlushLimitEdit()
    {
        if (!_limitEditPending)
        {
            return;
        }
        _limitEditPending = false;
        if (_limitEditStart != SpendLimit)
        {
            RaiseLimitChanged(_limitEditStart, SpendLimit);
        }
    }

    private void RaiseLimitChanged(decimal? oldLimit, decimal? newLimit) =>
        RaiseEvent(new UsageBillingLimitChangedEventArgs(SpendLimitChangedEvent, this, oldLimit, newLimit, IsSpendLimitEnabled));

    private void OnLimitInputPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsKeyboardFocusWithinProperty && e.GetNewValue<bool>() == false)
        {
            FlushLimitEdit();
        }
    }

    private void OnLimitInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FlushLimitEdit();
        }
    }

    /// <summary>"Set a limit" in the summary turns the limit on and moves focus to the amount.</summary>
    private void OnSetLimitClick(object? sender, RoutedEventArgs e)
    {
        SetCurrentValue(IsSpendLimitEnabledProperty, true);
        Dispatcher.UIThread.Post(() => _limitInput?.Focus(NavigationMethod.Tab), DispatcherPriority.Input);
    }

    #endregion

    #region Day change

    /// <summary>Refreshes the projection, the days left and the drawn days just after the clock's next midnight.</summary>
    private void ScheduleDayChange()
    {
        _dayTimer?.Dispose();
        _dayTimer = null;
        if (VisualRoot is null)
        {
            return;
        }
        var now = _clock.GetLocalNow();
        var due = now.Date.AddDays(1) - now.DateTime + TimeSpan.FromSeconds(1);
        _dayTimer = _clock.CreateTimer(_ => Dispatcher.UIThread.Post(OnDayChanged), null, due, Timeout.InfiniteTimeSpan);
    }

    private void OnDayChanged()
    {
        Refresh();
        RefreshChart();
        ScheduleDayChange();
    }

    #endregion
}
