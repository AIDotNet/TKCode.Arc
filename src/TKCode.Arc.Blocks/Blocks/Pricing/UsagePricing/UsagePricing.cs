using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using TKCode.Arc.DesignTokens;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A pricing calculator that picks the plan: two stepped sliders (seats and monthly usage) and a monthly / yearly switch
/// drive a breakdown and a recommendation card. The recommended plan is the first whose seat and usage limits both cover
/// the selection; the card morphs the plan name, says why (four reasons), rolls the monthly estimate, reflows the plan's
/// features and offers a trial, which turns itself off when the plan changes. From 780px wide the card has its own column.
/// </summary>
/// <example><code>
/// &lt;arc:UsagePricing SeatIndex="10" UsageIndex="14" BillingPeriod="Yearly" Currency="EUR" /&gt;
/// </code></example>
[TemplatePart(LayoutPart, typeof(UsagePricingLayout))]
[TemplatePart(SeatSliderPart, typeof(Slider))]
[TemplatePart(UsageSliderPart, typeof(Slider))]
[TemplatePart(PeriodPart, typeof(SegmentedControl))]
[TemplatePart(ActionPart, typeof(Button))]
[PseudoClasses(StackedPseudoClass, YearlyPseudoClass, TrialPseudoClass)]
public class UsagePricing : TemplatedControl
{
    public const string LayoutPart = "PART_Layout";
    public const string TitlePart = "PART_Title";
    public const string DescriptionPart = "PART_Description";
    public const string SeatSliderPart = "PART_SeatSlider";
    public const string UsageSliderPart = "PART_UsageSlider";
    public const string PeriodPart = "PART_Period";
    public const string SavingMarkPart = "PART_SavingMark";
    public const string SavingBeforePart = "PART_SavingBefore";
    public const string SavingPart = "PART_Saving";
    public const string SavingAfterPart = "PART_SavingAfter";
    public const string BreakdownTitlePart = "PART_BreakdownTitle";
    public const string SeatsRowBeforePart = "PART_SeatsRowBefore";
    public const string SeatsRowPlanPart = "PART_SeatsRowPlan";
    public const string SeatsRowAfterPart = "PART_SeatsRowAfter";
    public const string SeatsDetailPart = "PART_SeatsDetail";
    public const string SeatsCostPart = "PART_SeatsCost";
    public const string UsageRowPart = "PART_UsageRow";
    public const string UsageDetailPart = "PART_UsageDetail";
    public const string UsageValuePart = "PART_UsageValue";
    public const string YearlyRowPart = "PART_YearlyRow";
    public const string YearlyContentPart = "PART_YearlyContent";
    public const string YearlyLabelPart = "PART_YearlyLabel";
    public const string YearlyDetailPart = "PART_YearlyDetail";
    public const string YearlyOffPart = "PART_YearlyOff";
    public const string TotalLabelPart = "PART_TotalLabel";
    public const string TotalDetailPart = "PART_TotalDetail";
    public const string TotalPart = "PART_Total";
    public const string CardPart = "PART_Card";
    public const string MeterPart = "PART_Meter";
    public const string PlanNamePart = "PART_PlanName";
    public const string ReasonPart = "PART_Reason";
    public const string PricePart = "PART_Price";
    public const string PerMonthPart = "PART_PerMonth";
    public const string BilledPart = "PART_Billed";
    public const string FeaturesPart = "PART_Features";
    public const string ActionPart = "PART_Action";
    public const string NotePart = "PART_Note";
    public const string AnnouncerPart = "PART_Announcer";

    public const string StackedPseudoClass = ":stacked";
    public const string YearlyPseudoClass = ":yearly";
    public const string TrialPseudoClass = ":trial";

    #region Properties

    /// <summary>
    /// The plans from the smallest to the largest. The first plan whose limits cover the seats and the usage is
    /// recommended. Inline XAML plans land here; while the list is empty the block shows the three sample plans of
    /// <see cref="UsagePricingSamples"/>.
    /// </summary>
    public static readonly DirectProperty<UsagePricing, IList<UsagePricingPlan>> PlansProperty =
        AvaloniaProperty.RegisterDirect<UsagePricing, IList<UsagePricingPlan>>(nameof(Plans), o => o.Plans, (o, v) => o.Plans = v);

    /// <summary>The seat counts the seat slider steps through, smallest first; stops sit evenly along the track.</summary>
    public static readonly StyledProperty<IReadOnlyList<int>> SeatStopsProperty =
        AvaloniaProperty.Register<UsagePricing, IReadOnlyList<int>>(nameof(SeatStops), UsagePricingSamples.SeatStops);

    /// <summary>The monthly usage amounts the usage slider steps through, smallest first; stops sit evenly along the track.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>> UsageStopsProperty =
        AvaloniaProperty.Register<UsagePricing, IReadOnlyList<double>>(nameof(UsageStops), UsagePricingSamples.UsageStops);

    /// <summary>The selected seat stop (an index into <see cref="SeatStops"/>). Two-way: the slider edits it.</summary>
    public static readonly StyledProperty<int> SeatIndexProperty =
        AvaloniaProperty.Register<UsagePricing, int>(nameof(SeatIndex), UsagePricingSamples.DefaultSeatIndex,
            defaultBindingMode: BindingMode.TwoWay, coerce: CoerceSeatIndex);

    /// <summary>The selected usage stop (an index into <see cref="UsageStops"/>). Two-way: the slider edits it.</summary>
    public static readonly StyledProperty<int> UsageIndexProperty =
        AvaloniaProperty.Register<UsagePricing, int>(nameof(UsageIndex), UsagePricingSamples.DefaultUsageIndex,
            defaultBindingMode: BindingMode.TwoWay, coerce: CoerceUsageIndex);

    /// <summary>Monthly or yearly billing. Two-way: the switch edits it.</summary>
    public static readonly StyledProperty<UsagePricingPeriod> BillingPeriodProperty =
        AvaloniaProperty.Register<UsagePricing, UsagePricingPeriod>(nameof(BillingPeriod), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>ISO 4217 code of every amount ("USD" by default). Amounts are whole: no minor units.</summary>
    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<UsagePricing, string>(nameof(Currency), "USD");

    /// <summary>The usage a plan's <see cref="UsagePricingPlan.OveragePrice"/> buys (1,000,000 by default: "per million").</summary>
    public static readonly StyledProperty<double> OverageUnitProperty =
        AvaloniaProperty.Register<UsagePricing, double>(nameof(OverageUnit), 1_000_000);

    /// <summary>Formats usage for the readout, the marks, the reason and the breakdown (default: 100k, 1.5M, 300M).</summary>
    public static readonly StyledProperty<Func<double, string>?> UsageFormatterProperty =
        AvaloniaProperty.Register<UsagePricing, Func<double, string>?>(nameof(UsageFormatter));

    /// <summary>
    /// Whether the trial of the recommended plan is on. Two-way: the button turns it on and the undo link off; it turns
    /// itself off when the recommended plan changes.
    /// </summary>
    public static readonly StyledProperty<bool> IsTrialStartedProperty =
        AvaloniaProperty.Register<UsagePricing, bool>(nameof(IsTrialStarted), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Called when someone starts the trial (your sign-up flow); the button shows its spinner until the task ends. A thrown
    /// exception (other than a cancellation) leaves the trial off and raises <see cref="TrialFailed"/>. Null (the default)
    /// turns the trial on at once.
    /// </summary>
    public static readonly StyledProperty<Func<UsagePricingTrialRequest, CancellationToken, Task>?> TrialHandlerProperty =
        AvaloniaProperty.Register<UsagePricing, Func<UsagePricingTrialRequest, CancellationToken, Task>?>(nameof(TrialHandler));

    /// <summary>The built-in text and its culture; replace the object to localize the block.</summary>
    public static readonly StyledProperty<UsagePricingLabels> LabelsProperty =
        AvaloniaProperty.Register<UsagePricing, UsagePricingLabels>(nameof(Labels), new UsagePricingLabels());

    /// <summary>The seats on the seat slider.</summary>
    public static readonly DirectProperty<UsagePricing, int> SeatsProperty =
        AvaloniaProperty.RegisterDirect<UsagePricing, int>(nameof(Seats), o => o.Seats);

    /// <summary>The monthly usage on the usage slider.</summary>
    public static readonly DirectProperty<UsagePricing, double> UsageProperty =
        AvaloniaProperty.RegisterDirect<UsagePricing, double>(nameof(Usage), o => o.Usage);

    /// <summary>The recommended plan (null without plans).</summary>
    public static readonly DirectProperty<UsagePricing, UsagePricingPlan?> RecommendedPlanProperty =
        AvaloniaProperty.RegisterDirect<UsagePricing, UsagePricingPlan?>(nameof(RecommendedPlan), o => o.RecommendedPlan);

    /// <summary>Why the plan is recommended.</summary>
    public static readonly DirectProperty<UsagePricing, UsagePricingReason> ReasonProperty =
        AvaloniaProperty.RegisterDirect<UsagePricing, UsagePricingReason>(nameof(Reason), o => o.Reason);

    /// <summary>The monthly estimate: seats + usage beyond the plan − the yearly discount under yearly billing.</summary>
    public static readonly DirectProperty<UsagePricing, decimal> EstimatedTotalProperty =
        AvaloniaProperty.RegisterDirect<UsagePricing, decimal>(nameof(EstimatedTotal), o => o.EstimatedTotal);

    /// <summary>What a year of yearly billing saves on the seats (shown under either period).</summary>
    public static readonly DirectProperty<UsagePricing, decimal> YearlySavingProperty =
        AvaloniaProperty.RegisterDirect<UsagePricing, decimal>(nameof(YearlySaving), o => o.YearlySaving);

    /// <summary>True while <see cref="TrialHandler"/> runs.</summary>
    public static readonly DirectProperty<UsagePricing, bool> IsTrialBusyProperty =
        AvaloniaProperty.RegisterDirect<UsagePricing, bool>(nameof(IsTrialBusy), o => o.IsTrialBusy);

    /// <summary>Raised when the recommended plan (or only its reason) changes.</summary>
    public static readonly RoutedEvent<UsagePricingRecommendationChangedEventArgs> RecommendationChangedEvent =
        RoutedEvent.Register<UsagePricing, UsagePricingRecommendationChangedEventArgs>(nameof(RecommendationChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the seats, the usage or the billing period change.</summary>
    public static readonly RoutedEvent<UsagePricingSelectionEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<UsagePricing, UsagePricingSelectionEventArgs>(nameof(SelectionChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the trial turns on (after <see cref="TrialHandler"/> succeeds).</summary>
    public static readonly RoutedEvent<UsagePricingSelectionEventArgs> TrialStartedEvent =
        RoutedEvent.Register<UsagePricing, UsagePricingSelectionEventArgs>(nameof(TrialStarted), RoutingStrategies.Bubble);

    /// <summary>Raised when the trial turns off: undone, or reset because the recommended plan changed.</summary>
    public static readonly RoutedEvent<UsagePricingSelectionEventArgs> TrialUndoneEvent =
        RoutedEvent.Register<UsagePricing, UsagePricingSelectionEventArgs>(nameof(TrialUndone), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="TrialHandler"/> throws; the trial stays off.</summary>
    public static readonly RoutedEvent<UsagePricingSelectionEventArgs> TrialFailedEvent =
        RoutedEvent.Register<UsagePricing, UsagePricingSelectionEventArgs>(nameof(TrialFailed), RoutingStrategies.Bubble);

    /// <inheritdoc cref="PlansProperty"/>
    [Content]
    public IList<UsagePricingPlan> Plans
    {
        get => _plans;
        set
        {
            var next = value ?? new AvaloniaList<UsagePricingPlan>();
            var old = _plans;
            if (SetAndRaise(PlansProperty, ref _plans, next))
            {
                Unwatch(old);
                if (_watching)
                {
                    Watch(next);
                }
                Refresh(animate: true);
            }
        }
    }

    /// <inheritdoc cref="SeatStopsProperty"/>
    public IReadOnlyList<int> SeatStops { get => GetValue(SeatStopsProperty); set => SetValue(SeatStopsProperty, value); }

    /// <inheritdoc cref="UsageStopsProperty"/>
    public IReadOnlyList<double> UsageStops { get => GetValue(UsageStopsProperty); set => SetValue(UsageStopsProperty, value); }

    /// <inheritdoc cref="SeatIndexProperty"/>
    public int SeatIndex { get => GetValue(SeatIndexProperty); set => SetValue(SeatIndexProperty, value); }

    /// <inheritdoc cref="UsageIndexProperty"/>
    public int UsageIndex { get => GetValue(UsageIndexProperty); set => SetValue(UsageIndexProperty, value); }

    /// <inheritdoc cref="BillingPeriodProperty"/>
    public UsagePricingPeriod BillingPeriod { get => GetValue(BillingPeriodProperty); set => SetValue(BillingPeriodProperty, value); }

    /// <inheritdoc cref="CurrencyProperty"/>
    public string Currency { get => GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }

    /// <inheritdoc cref="OverageUnitProperty"/>
    public double OverageUnit { get => GetValue(OverageUnitProperty); set => SetValue(OverageUnitProperty, value); }

    /// <inheritdoc cref="UsageFormatterProperty"/>
    public Func<double, string>? UsageFormatter { get => GetValue(UsageFormatterProperty); set => SetValue(UsageFormatterProperty, value); }

    /// <inheritdoc cref="IsTrialStartedProperty"/>
    public bool IsTrialStarted { get => GetValue(IsTrialStartedProperty); set => SetValue(IsTrialStartedProperty, value); }

    /// <inheritdoc cref="TrialHandlerProperty"/>
    public Func<UsagePricingTrialRequest, CancellationToken, Task>? TrialHandler
    {
        get => GetValue(TrialHandlerProperty);
        set => SetValue(TrialHandlerProperty, value);
    }

    /// <inheritdoc cref="LabelsProperty"/>
    public UsagePricingLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="SeatsProperty"/>
    public int Seats { get => _seats; private set => SetAndRaise(SeatsProperty, ref _seats, value); }

    /// <inheritdoc cref="UsageProperty"/>
    public double Usage { get => _usage; private set => SetAndRaise(UsageProperty, ref _usage, value); }

    /// <inheritdoc cref="RecommendedPlanProperty"/>
    public UsagePricingPlan? RecommendedPlan { get => _recommended; private set => SetAndRaise(RecommendedPlanProperty, ref _recommended, value); }

    /// <inheritdoc cref="ReasonProperty"/>
    public UsagePricingReason Reason { get => _reason; private set => SetAndRaise(ReasonProperty, ref _reason, value); }

    /// <inheritdoc cref="EstimatedTotalProperty"/>
    public decimal EstimatedTotal { get => _total; private set => SetAndRaise(EstimatedTotalProperty, ref _total, value); }

    /// <inheritdoc cref="YearlySavingProperty"/>
    public decimal YearlySaving { get => _saving; private set => SetAndRaise(YearlySavingProperty, ref _saving, value); }

    /// <inheritdoc cref="IsTrialBusyProperty"/>
    public bool IsTrialBusy { get => _busy; private set => SetAndRaise(IsTrialBusyProperty, ref _busy, value); }

    /// <inheritdoc cref="RecommendationChangedEvent"/>
    public event EventHandler<UsagePricingRecommendationChangedEventArgs>? RecommendationChanged
    {
        add => AddHandler(RecommendationChangedEvent, value);
        remove => RemoveHandler(RecommendationChangedEvent, value);
    }

    /// <inheritdoc cref="SelectionChangedEvent"/>
    public event EventHandler<UsagePricingSelectionEventArgs>? SelectionChanged
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    /// <inheritdoc cref="TrialStartedEvent"/>
    public event EventHandler<UsagePricingSelectionEventArgs>? TrialStarted
    {
        add => AddHandler(TrialStartedEvent, value);
        remove => RemoveHandler(TrialStartedEvent, value);
    }

    /// <inheritdoc cref="TrialUndoneEvent"/>
    public event EventHandler<UsagePricingSelectionEventArgs>? TrialUndone
    {
        add => AddHandler(TrialUndoneEvent, value);
        remove => RemoveHandler(TrialUndoneEvent, value);
    }

    /// <inheritdoc cref="TrialFailedEvent"/>
    public event EventHandler<UsagePricingSelectionEventArgs>? TrialFailed
    {
        add => AddHandler(TrialFailedEvent, value);
        remove => RemoveHandler(TrialFailedEvent, value);
    }

    #endregion

    private IList<UsagePricingPlan> _plans;
    private List<UsagePricingPlan>? _samplePlans;
    private int _seats;
    private double _usage;
    private UsagePricingPlan? _recommended;
    private UsagePricingReason _reason;
    private decimal _total;
    private decimal _saving;
    private bool _busy;
    private bool _watching;
    private readonly List<UsagePricingPlan> _watchedPlans = new();
    private INotifyCollectionChanged? _watchedList;

    private UsagePricingLayout? _layout;
    private TextBlock? _title;
    private TextBlock? _description;
    private Slider? _seatSlider;
    private Slider? _usageSlider;
    private SegmentedControl? _period;
    private UsagePricingSavingMark? _savingMark;
    private TextBlock? _savingBefore;
    private UsagePricingDigits? _savingDigits;
    private TextBlock? _savingAfter;
    private TextBlock? _breakdownTitle;
    private TextBlock? _seatsRowBefore;
    private TextMorph? _seatsRowPlan;
    private TextBlock? _seatsRowAfter;
    private TextBlock? _seatsDetail;
    private UsagePricingDigits? _seatsCost;
    private TextBlock? _usageRow;
    private TextBlock? _usageDetail;
    private ArcContentSwap? _usageValue;
    private SpringSizeDecorator? _yearlyRow;
    private Control? _yearlyContent;
    private TextBlock? _yearlyLabel;
    private TextBlock? _yearlyDetail;
    private UsagePricingDigits? _yearlyOff;
    private TextBlock? _totalLabel;
    private TextBlock? _totalDetail;
    private UsagePricingDigits? _totalDigits;
    private Control? _card;
    private UsagePricingMeter? _meter;
    private TextMorph? _planName;
    private ArcContentSwap? _reasonSwap;
    private UsagePricingDigits? _price;
    private TextBlock? _perMonth;
    private ArcContentSwap? _billed;
    private UsagePricingFeatureList? _features;
    private Button? _action;
    private ArcContentSwap? _note;
    private SiteAnnouncer? _announcer;

    // Content swapped in and out of the card's swaps; built once so a swap only runs when the content really changes.
    private readonly UsagePricingDigits _usageCost = new();
    private readonly TextBlock _bundled = new();
    private readonly StackPanel _billedYearly = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _billedBefore = new();
    private readonly UsagePricingDigits _billedDigits = new();
    private readonly TextBlock _billedAfter = new();
    private readonly StackPanel _trialLabel = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
    private readonly StrokeDrawGlyph _trialCheck = new() { Data = ArcIcons.Check, Size = 16, StrokeThickness = 1.75, Progress = 1 };
    private readonly TextBlock _trialText = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _trialNote = new() { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _trialNoteText = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly UsagePricingUndoButton _undo = new();

    private MotionValue<double>? _yearlyFade;
    private UsagePricingQuote _quote;
    private bool _hasQuote;
    private string? _trialPlan;
    private CancellationTokenSource? _trialRun;
    private bool _syncingSliders;
    private bool _keyboardInput;
    private string? _lastAnnouncement;

    static UsagePricing()
    {
        ClipToBoundsProperty.OverrideDefaultValue<UsagePricing>(false);
        FocusableProperty.OverrideDefaultValue<UsagePricing>(false);
    }

    /// <summary>Creates the block with the sample plans, stops and wording.</summary>
    public UsagePricing()
    {
        _plans = new AvaloniaList<UsagePricingPlan>();
        _billedYearly.Children.Add(_billedBefore);
        _billedYearly.Children.Add(_billedDigits);
        _billedYearly.Children.Add(_billedAfter);
        _trialLabel.Children.Add(_trialCheck);
        _trialLabel.Children.Add(_trialText);
        _trialNote.Children.Add(_trialNoteText);
        _trialNote.Children.Add(_undo);
        _undo.Click += (_, _) => UndoTrial(fromUndoLink: true);
        ArcTokens.Bind(_bundled, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Region);
        AddHandler(KeyDownEvent, (_, _) => _keyboardInput = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboardInput = false, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    #region Coercion

    private static int CoerceSeatIndex(AvaloniaObject o, int value)
        => o is UsagePricing p ? Math.Clamp(value, 0, Math.Max(0, (p.SeatStops?.Count ?? 1) - 1)) : value;

    private static int CoerceUsageIndex(AvaloniaObject o, int value)
        => o is UsagePricing p ? Math.Clamp(value, 0, Math.Max(0, (p.UsageStops?.Count ?? 1) - 1)) : value;

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_seatSlider is not null)
        {
            _seatSlider.PropertyChanged -= OnSliderChanged;
        }
        if (_usageSlider is not null)
        {
            _usageSlider.PropertyChanged -= OnSliderChanged;
        }
        if (_period is not null)
        {
            _period.SelectionChanged -= OnPeriodSelected;
        }
        if (_action is not null)
        {
            _action.Click -= OnActionClick;
        }
        if (_layout is not null)
        {
            _layout.StackedChanged -= OnStackedChanged;
        }
        if (_billed is not null)
        {
            _billed.Content = null;
        }
        if (_usageValue is not null)
        {
            _usageValue.Content = null;
        }
        if (_note is not null)
        {
            _note.Content = null;
        }
        if (_action is not null)
        {
            _action.Content = null;
        }
        base.OnApplyTemplate(e);
        var n = e.NameScope;
        _layout = n.Find<UsagePricingLayout>(LayoutPart);
        _title = n.Find<TextBlock>(TitlePart);
        _description = n.Find<TextBlock>(DescriptionPart);
        _seatSlider = n.Find<Slider>(SeatSliderPart);
        _usageSlider = n.Find<Slider>(UsageSliderPart);
        _period = n.Find<SegmentedControl>(PeriodPart);
        _savingMark = n.Find<UsagePricingSavingMark>(SavingMarkPart);
        _savingBefore = n.Find<TextBlock>(SavingBeforePart);
        _savingDigits = n.Find<UsagePricingDigits>(SavingPart);
        _savingAfter = n.Find<TextBlock>(SavingAfterPart);
        _breakdownTitle = n.Find<TextBlock>(BreakdownTitlePart);
        _seatsRowBefore = n.Find<TextBlock>(SeatsRowBeforePart);
        _seatsRowPlan = n.Find<TextMorph>(SeatsRowPlanPart);
        _seatsRowAfter = n.Find<TextBlock>(SeatsRowAfterPart);
        _seatsDetail = n.Find<TextBlock>(SeatsDetailPart);
        _seatsCost = n.Find<UsagePricingDigits>(SeatsCostPart);
        _usageRow = n.Find<TextBlock>(UsageRowPart);
        _usageDetail = n.Find<TextBlock>(UsageDetailPart);
        _usageValue = n.Find<ArcContentSwap>(UsageValuePart);
        _yearlyRow = n.Find<SpringSizeDecorator>(YearlyRowPart);
        _yearlyContent = n.Find<Control>(YearlyContentPart);
        _yearlyLabel = n.Find<TextBlock>(YearlyLabelPart);
        _yearlyDetail = n.Find<TextBlock>(YearlyDetailPart);
        _yearlyOff = n.Find<UsagePricingDigits>(YearlyOffPart);
        _totalLabel = n.Find<TextBlock>(TotalLabelPart);
        _totalDetail = n.Find<TextBlock>(TotalDetailPart);
        _totalDigits = n.Find<UsagePricingDigits>(TotalPart);
        _card = n.Find<Control>(CardPart);
        _meter = n.Find<UsagePricingMeter>(MeterPart);
        _planName = n.Find<TextMorph>(PlanNamePart);
        _reasonSwap = n.Find<ArcContentSwap>(ReasonPart);
        _price = n.Find<UsagePricingDigits>(PricePart);
        _perMonth = n.Find<TextBlock>(PerMonthPart);
        _billed = n.Find<ArcContentSwap>(BilledPart);
        _features = n.Find<UsagePricingFeatureList>(FeaturesPart);
        _action = n.Find<Button>(ActionPart);
        _note = n.Find<ArcContentSwap>(NotePart);
        _announcer = n.Find<SiteAnnouncer>(AnnouncerPart);
        _yearlyFade = null;

        if (_seatSlider is not null)
        {
            _seatSlider.PropertyChanged += OnSliderChanged;
        }
        if (_usageSlider is not null)
        {
            _usageSlider.PropertyChanged += OnSliderChanged;
        }
        if (_period is not null)
        {
            _period.SelectionChanged += OnPeriodSelected;
        }
        if (_action is not null)
        {
            _action.Click += OnActionClick;
        }
        if (_layout is not null)
        {
            _layout.StackedChanged += OnStackedChanged;
            PseudoClasses.Set(StackedPseudoClass, _layout.IsStacked);
        }
        _hasQuote = false;
        ApplyLabels();
        Refresh(animate: false);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _watching = true;
        Watch(_plans);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watching = false;
        Unwatch(_plans);
        _trialRun?.Cancel();
    }

    private void OnStackedChanged(object? sender, bool stacked) => PseudoClasses.Set(StackedPseudoClass, stacked);

    #endregion

    #region Observation

    private void Watch(IList<UsagePricingPlan> plans)
    {
        Unwatch(plans);
        if (plans is INotifyCollectionChanged list)
        {
            _watchedList = list;
            list.CollectionChanged += OnPlansChanged;
        }
        foreach (var plan in plans)
        {
            plan.PropertyChanged += OnPlanChanged;
            plan.Features.CollectionChanged += OnFeaturesChanged;
            _watchedPlans.Add(plan);
        }
    }

    private void Unwatch(IList<UsagePricingPlan>? plans)
    {
        if (_watchedList is not null)
        {
            _watchedList.CollectionChanged -= OnPlansChanged;
            _watchedList = null;
        }
        foreach (var plan in _watchedPlans)
        {
            plan.PropertyChanged -= OnPlanChanged;
            plan.Features.CollectionChanged -= OnFeaturesChanged;
        }
        _watchedPlans.Clear();
    }

    private void OnPlansChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Watch(_plans);
        Refresh(animate: true);
    }

    private void OnPlanChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Refresh(animate: true);

    private void OnFeaturesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh(animate: true);

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SeatStopsProperty)
        {
            CoerceValue(SeatIndexProperty);
            Refresh(animate: true);
        }
        else if (change.Property == UsageStopsProperty)
        {
            CoerceValue(UsageIndexProperty);
            Refresh(animate: true);
        }
        else if (change.Property == SeatIndexProperty || change.Property == UsageIndexProperty || change.Property == BillingPeriodProperty)
        {
            Refresh(animate: true);
            RaiseSelection(SelectionChangedEvent);
        }
        else if (change.Property == CurrencyProperty || change.Property == OverageUnitProperty || change.Property == UsageFormatterProperty)
        {
            Refresh(animate: true);
        }
        else if (change.Property == LabelsProperty)
        {
            ApplyLabels();
            _hasQuote = false;
            Refresh(animate: false);
        }
        else if (change.Property == IsTrialStartedProperty)
        {
            var on = change.GetNewValue<bool>();
            _trialPlan = on ? RecommendedPlan?.Identity : null;
            UpdateTrial(animate: true);
        }
    }

    #region Formatting

    private CultureInfo Culture => Labels.Culture;

    /// <summary>The plans in use: <see cref="Plans"/>, or the sample plans while it is empty.</summary>
    internal IList<UsagePricingPlan> EffectivePlans => _plans.Count > 0 ? _plans : _samplePlans ??= UsagePricingSamples.CreatePlans();

    private string FormatUsage(double value) => UsageFormatter?.Invoke(value) ?? UsagePricingMath.FormatUsage(value, Culture);

    private string FormatSeats(int seats) => seats.ToString("N0", Culture);

    private string Money(decimal amount) => UsagePricingMath.Money(amount, Currency, Culture);

    private string Rate(decimal amount) => UsagePricingMath.Rate(amount, Currency, Culture);

    internal string ReasonText(UsagePricingQuote quote)
    {
        var plans = EffectivePlans;
        var labels = Labels;
        if (quote.PlanIndex < 0 || quote.PlanIndex >= plans.Count)
        {
            return string.Empty;
        }
        if (quote.Reason == UsagePricingReason.WithinLimits)
        {
            var plan = plans[quote.PlanIndex];
            return labels.ReasonWithin(plan.MaxSeats is { } s ? FormatSeats(s) : "∞", plan.MaxUsage is { } u ? FormatUsage(u) : "∞");
        }
        var previous = plans[quote.PlanIndex - 1];
        var seats = previous.MaxSeats is { } ps ? FormatSeats(ps) : string.Empty;
        var usage = previous.MaxUsage is { } pu ? FormatUsage(pu) : string.Empty;
        return quote.Reason switch
        {
            UsagePricingReason.SeatsAbove => labels.ReasonSeats(seats),
            UsagePricingReason.UsageAbove => labels.ReasonUsage(usage),
            _ => labels.ReasonBoth(seats, usage)
        };
    }

    #endregion

    private void ApplyLabels()
    {
        var labels = Labels;
        SetText(_title, labels.Title);
        SetText(_description, labels.Description);
        AutomationProperties.SetName(this, labels.Title);
        if (_seatSlider is not null)
        {
            _seatSlider.Label = labels.SeatsLabel;
        }
        if (_usageSlider is not null)
        {
            _usageSlider.Label = labels.UsageLabel;
        }
        if (_period is not null)
        {
            AutomationProperties.SetName(_period, labels.BillingPeriodName);
            _period.ItemsSource = new[]
            {
                new SegmentedOption(UsagePricingPeriod.Monthly, labels.Monthly),
                new SegmentedOption(UsagePricingPeriod.Yearly, labels.Yearly)
            };
            _period.SelectedIndex = BillingPeriod == UsagePricingPeriod.Yearly ? 1 : 0;
        }
        var (savingBefore, savingAfter) = UsagePricingMath.SplitAround(labels.Saving);
        SetText(_savingBefore, savingBefore);
        SetText(_savingAfter, savingAfter ?? string.Empty);
        SetText(_breakdownTitle, labels.BreakdownTitle);
        var (seatsBefore, seatsAfter) = UsagePricingMath.SplitAround(labels.SeatsRow);
        SetText(_seatsRowBefore, seatsBefore);
        SetText(_seatsRowAfter, seatsAfter ?? string.Empty);
        SetText(_usageRow, labels.UsageRow);
        _bundled.Text = labels.BundledValue;
        SetText(_yearlyLabel, labels.YearlyRow);
        SetText(_totalLabel, labels.TotalRow);
        SetText(_totalDetail, labels.TotalDetail);
        SetText(_perMonth, labels.PerMonth);
        var (billedBefore, billedAfter) = UsagePricingMath.SplitAround(labels.BilledYearly);
        _billedBefore.Text = billedBefore;
        _billedAfter.Text = billedAfter ?? string.Empty;
        _trialText.Text = labels.TrialStarted;
        _trialNoteText.Text = labels.TrialStartedNote;
        _undo.Content = labels.Undo;
        AutomationProperties.SetName(_undo, labels.Undo);
        Func<decimal, string> money = Money;
        foreach (var digits in new[] { _savingDigits, _seatsCost, _yearlyOff, _totalDigits, _price, _usageCost, _billedDigits })
        {
            if (digits is not null)
            {
                digits.Format = money;
            }
        }
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block is not null)
        {
            block.Text = text;
            block.IsVisible = !string.IsNullOrEmpty(text);
        }
    }

    /// <summary>Recomputes the estimate and pushes it to the template; <paramref name="animate"/> false jumps.</summary>
    private void Refresh(bool animate)
    {
        var seatStops = SeatStops ?? Array.Empty<int>();
        var usageStops = UsageStops ?? Array.Empty<double>();
        var seats = seatStops.Count == 0 ? 0 : seatStops[Math.Clamp(SeatIndex, 0, seatStops.Count - 1)];
        var usage = usageStops.Count == 0 ? 0 : usageStops[Math.Clamp(UsageIndex, 0, usageStops.Count - 1)];
        var plans = EffectivePlans;
        var terms = new UsagePricingTerms[plans.Count];
        for (var i = 0; i < terms.Length; i++)
        {
            terms[i] = UsagePricingTerms.Of(plans[i]);
        }
        var yearly = BillingPeriod == UsagePricingPeriod.Yearly;
        var quote = UsagePricingMath.Quote(terms, seats, usage, yearly, OverageUnit);
        var previous = _hasQuote ? _quote : (UsagePricingQuote?)null;
        var oldPlan = RecommendedPlan;
        var plan = quote.PlanIndex >= 0 ? plans[quote.PlanIndex] : null;
        _quote = quote;
        _hasQuote = true;

        Seats = seats;
        Usage = usage;
        RecommendedPlan = plan;
        Reason = quote.Reason;
        EstimatedTotal = quote.Total;
        YearlySaving = quote.YearlySaving;
        PseudoClasses.Set(YearlyPseudoClass, yearly);

        animate = animate && IsLoaded;
        SyncSliders(seatStops, usageStops, terms);
        if (_period is not null && _period.SelectedIndex != (yearly ? 1 : 0))
        {
            _period.SelectedIndex = yearly ? 1 : 0;
        }
        if (_savingMark is not null)
        {
            _savingMark.IsApplied = yearly;
        }
        SetDigits(_savingDigits, quote.YearlySaving, animate);

        // Breakdown.
        var labels = Labels;
        var planName = plan?.Name ?? string.Empty;
        if (_seatsRowPlan is not null)
        {
            _seatsRowPlan.Text = planName;
        }
        SetText(_seatsDetail, labels.SeatsDetail(FormatSeats(seats), Rate(plan?.SeatPrice ?? 0)));
        SetDigits(_seatsCost, quote.SeatCost, animate);
        SetDigits(_usageCost, quote.UsageCost, animate);
        if (quote.Overage > 0 && plan is not null)
        {
            SetText(_usageDetail, labels.UsageOver(FormatUsage(usage), FormatUsage(quote.Overage), Rate(plan.OveragePrice)));
            SetSwap(_usageValue, _usageCost, animate);
        }
        else
        {
            SetText(_usageDetail, labels.UsageBundled(FormatUsage(usage), FormatUsage(plan?.IncludedUsage ?? 0)));
            SetSwap(_usageValue, _bundled, animate);
        }
        SetText(_yearlyDetail, labels.YearlyDetail(FormatSeats(seats), Rate(plan?.YearlySeatDiscount ?? 0)));
        SetDigits(_yearlyOff, -quote.YearlyOff, animate);
        SetYearlyRow(yearly, animate);
        SetDigits(_totalDigits, quote.Total, animate);

        // Card.
        if (_meter is not null)
        {
            _meter.SetNames(plans.Select(p => p.Name).ToList());
            _meter.CurrentIndex = quote.PlanIndex;
        }
        if (_planName is not null)
        {
            _planName.Text = planName;
        }
        if (_card is not null)
        {
            AutomationProperties.SetName(_card, planName);
        }
        var reason = ReasonText(quote);
        SetSwap(_reasonSwap, reason, animate);
        SetDigits(_price, quote.Total, animate);
        SetDigits(_billedDigits, quote.SeatsPerYear, animate);
        SetSwap(_billed, yearly ? _billedYearly : labels.BilledMonthly, animate);
        if (_features is not null)
        {
            var features = new List<IReadOnlyList<UsagePricingFeature>>(plans.Count);
            foreach (var p in plans)
            {
                features.Add(p.Features);
            }
            _features.Update(UsagePricingMath.FeatureLines(features, quote.PlanIndex), animate);
            AutomationProperties.SetName(_features, labels.IncludedIn(planName));
        }

        // The trial belongs to a plan: another plan turns it off (Arc).
        if (IsTrialStarted && _trialPlan is not null && plan?.Identity != _trialPlan)
        {
            _trialRun?.Cancel();
            SetCurrentValue(IsTrialStartedProperty, false);
            RaiseSelection(TrialUndoneEvent);
        }
        else
        {
            UpdateTrial(animate);
        }

        if (previous is { } before && (before.PlanIndex != quote.PlanIndex || before.Reason != quote.Reason || !ReferenceEquals(oldPlan, plan)))
        {
            RaiseEvent(new UsagePricingRecommendationChangedEventArgs(RecommendationChangedEvent, oldPlan, plan, quote.Reason));
            var text = labels.Announcement(planName, reason);
            if (text != _lastAnnouncement)
            {
                _announcer?.Announce(text);
            }
            _lastAnnouncement = text;
        }
        else if (previous is null)
        {
            _lastAnnouncement = labels.Announcement(planName, reason);
        }
    }

    private static void SetDigits(UsagePricingDigits? digits, decimal value, bool animate)
    {
        if (digits is null)
        {
            return;
        }
        if (!animate)
        {
            digits.IsInstant = true;
            digits.Value = value;
            digits.IsInstant = false;
        }
        else
        {
            digits.Value = value;
        }
    }

    private static void SetSwap(ArcContentSwap? swap, object content, bool animate)
    {
        if (swap is null || Equals(swap.Content, content))
        {
            return;
        }
        if (!animate)
        {
            var mode = swap.Mode;
            swap.Mode = ContentSwapMode.None;
            swap.Content = content;
            swap.Mode = mode;
        }
        else
        {
            swap.Content = content;
        }
    }

    /// <summary>
    /// The discount row opens to its height on the Smooth spring and fades in over 240ms (enter curve, 50ms late); it
    /// fades out over 120ms while closing. The height is measured, so a decorator springs it; the fade is code because
    /// enter and exit differ.
    /// </summary>
    private void SetYearlyRow(bool yearly, bool animate)
    {
        if (_yearlyRow is null || _yearlyContent is null)
        {
            return;
        }
        _yearlyRow.IsExpanded = yearly;
        var level = ArcMotion.GetEffectiveLevel(this);
        _yearlyFade ??= CreateFade(_yearlyContent);
        if (!animate || level == ArcMotionLevel.Off)
        {
            _yearlyFade.Jump(yearly ? 1 : 0);
            return;
        }
        if (_yearlyFade.Target == (yearly ? 1 : 0))
        {
            return;
        }
        _ = _yearlyFade.AnimateTo(yearly ? 1 : 0, yearly
            ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, TimeSpan.FromMilliseconds(50))
            : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
    }

    private MotionValue<double> CreateFade(Control target)
    {
        var value = new MotionValue<double>(this, target.Opacity, MotionVectors.Double);
        value.Changed += v => target.Opacity = v;
        return value;
    }

    private void SyncSliders(IReadOnlyList<int> seatStops, IReadOnlyList<double> usageStops, IReadOnlyList<UsagePricingTerms> terms)
    {
        _syncingSliders = true;
        try
        {
            if (_seatSlider is not null)
            {
                _seatSlider.Maximum = Math.Max(0, seatStops.Count - 1);
                _seatSlider.Value = SeatIndex;
                _seatSlider.ValueFormatter = i => seatStops.Count == 0 ? string.Empty : FormatSeats(seatStops[Math.Clamp((int)Math.Round(i), 0, seatStops.Count - 1)]);
                SetMarks(_seatSlider, seatStops, terms, v => v, p => p.MaxSeats);
            }
            if (_usageSlider is not null)
            {
                _usageSlider.Maximum = Math.Max(0, usageStops.Count - 1);
                _usageSlider.Value = UsageIndex;
                _usageSlider.ValueFormatter = i => usageStops.Count == 0 ? string.Empty : FormatUsage(usageStops[Math.Clamp((int)Math.Round(i), 0, usageStops.Count - 1)]);
                SetMarks(_usageSlider, usageStops, terms, v => v, p => p.MaxUsage);
            }
        }
        finally
        {
            _syncingSliders = false;
        }
    }

    private void SetMarks<T>(Slider slider, IReadOnlyList<T> stops, IReadOnlyList<UsagePricingTerms> terms, Func<T, double> value,
        Func<UsagePricingTerms, double?> limit)
    {
        var wanted = new List<(double Index, string Name)>();
        for (var i = 1; i < terms.Count; i++)
        {
            var index = UsagePricingMath.MarkIndex(stops, limit(terms[i - 1]), value);
            if (index >= 0)
            {
                wanted.Add((index, EffectivePlans[i].Name));
            }
        }
        var marks = slider.Marks;
        var same = marks.Count == wanted.Count;
        for (var i = 0; same && i < wanted.Count; i++)
        {
            same = marks[i].Value == wanted[i].Index && marks[i].Label == wanted[i].Name;
        }
        if (same)
        {
            return;
        }
        slider.Marks = new AvaloniaList<SliderMark>(wanted.Select(w => new SliderMark(w.Index, w.Name)));
    }

    private void OnSliderChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_syncingSliders || e.Property != RangeBase.ValueProperty || sender is not Slider slider)
        {
            return;
        }
        var index = (int)Math.Round(slider.Value);
        if (ReferenceEquals(slider, _seatSlider))
        {
            SetCurrentValue(SeatIndexProperty, index);
        }
        else if (ReferenceEquals(slider, _usageSlider))
        {
            SetCurrentValue(UsageIndexProperty, index);
        }
    }

    private void OnPeriodSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_period is null || _period.SelectedIndex < 0)
        {
            return;
        }
        SetCurrentValue(BillingPeriodProperty, _period.SelectedIndex == 1 ? UsagePricingPeriod.Yearly : UsagePricingPeriod.Monthly);
    }

    #region Trial

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (!IsTrialStarted && !IsTrialBusy)
        {
            _ = StartTrialAsync();
        }
    }

    /// <summary>
    /// Starts the trial of the recommended plan, as the button does: runs <see cref="TrialHandler"/> when set, then turns
    /// <see cref="IsTrialStarted"/> on. Returns false when nothing started (no plan, already on, busy, failed or cancelled).
    /// </summary>
    public async Task<bool> StartTrialAsync()
    {
        var plan = RecommendedPlan;
        if (plan is null || IsTrialStarted || IsTrialBusy)
        {
            return false;
        }
        var handler = TrialHandler;
        if (handler is not null)
        {
            _trialRun?.Cancel();
            var run = _trialRun = new CancellationTokenSource();
            IsTrialBusy = true;
            UpdateTrial(animate: true);
            try
            {
                await handler(new UsagePricingTrialRequest(plan, Seats, Usage, BillingPeriod, EstimatedTotal), run.Token);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception)
            {
                RaiseSelection(TrialFailedEvent);
                return false;
            }
            finally
            {
                if (ReferenceEquals(_trialRun, run))
                {
                    _trialRun = null;
                    IsTrialBusy = false;
                    UpdateTrial(animate: true);
                }
                run.Dispose();
            }
            if (run.IsCancellationRequested || !ReferenceEquals(RecommendedPlan, plan))
            {
                return false;
            }
        }
        _trialPlan = plan.Identity;
        SetCurrentValue(IsTrialStartedProperty, true);
        RaiseSelection(TrialStartedEvent);
        return true;
    }

    /// <summary>Turns the trial off again (the undo link); keyboard focus returns to the button.</summary>
    public void UndoTrial() => UndoTrial(fromUndoLink: false);

    private void UndoTrial(bool fromUndoLink)
    {
        _trialRun?.Cancel();
        if (!IsTrialStarted)
        {
            return;
        }
        var hadFocus = fromUndoLink || _undo.IsFocused;
        SetCurrentValue(IsTrialStartedProperty, false);
        RaiseSelection(TrialUndoneEvent);
        if (hadFocus)
        {
            // Arc drops focus to the page when the link unmounts; hand it to the button instead.
            _action?.Focus(_keyboardInput ? NavigationMethod.Tab : NavigationMethod.Pointer);
        }
    }

    private void UpdateTrial(bool animate)
    {
        var labels = Labels;
        var on = IsTrialStarted;
        PseudoClasses.Set(TrialPseudoClass, on);
        animate = animate && IsLoaded;
        if (_action is not null)
        {
            _action.Variant = on ? ButtonVariant.Secondary : ButtonVariant.Primary;
            _action.IsLoading = IsTrialBusy;
            object label = on ? _trialLabel : labels.StartWith(RecommendedPlan?.Name ?? string.Empty);
            if (!Equals(_action.Content, label))
            {
                var wasOn = ReferenceEquals(_action.Content, _trialLabel);
                _action.Content = label;
                if (on && !wasOn && animate)
                {
                    _trialCheck.Progress = 0;
                    _ = _trialCheck.Play(TimeSpan.FromMilliseconds(350), ArcEasings.Enter, TimeSpan.FromMilliseconds(68));
                }
            }
            AutomationProperties.SetName(_action, on ? labels.TrialStarted : labels.StartWith(RecommendedPlan?.Name ?? string.Empty));
            AutomationProperties.SetHelpText(_action, on ? labels.TrialStartedNote : labels.TrialNote);
        }
        SetSwap(_note, on ? _trialNote : labels.TrialNote, animate);
    }

    #endregion

    private void RaiseSelection(RoutedEvent<UsagePricingSelectionEventArgs> routedEvent)
        => RaiseEvent(new UsagePricingSelectionEventArgs(routedEvent, RecommendedPlan, Seats, Usage, BillingPeriod, EstimatedTotal));
}

/// <summary>The text button that undoes the trial: underlined, in the foreground color (its look is in the block's theme file).</summary>
internal sealed class UsagePricingUndoButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(UsagePricingUndoButton);
}
