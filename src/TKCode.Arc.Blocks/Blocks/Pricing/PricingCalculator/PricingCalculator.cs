using TKCode.Arc.DesignTokens;
using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Metadata;
using Avalonia.Threading;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A seat-priced plan picker: a seat slider with step buttons and a billing switch above the plan cards, and a totals row
/// with the selected plan's price, the yearly savings and a trial button. Every number rolls digit by digit as the seats
/// or the billing change; the recommended badge glides to the plan that fits the team, and the selection frame follows
/// the plan in use. A plan the user picks stays selected while it covers the seats; above its cap the selection falls
/// back to the recommendation and the capped card offers to go back to its seat limit. All plans, stops, prices and
/// rules are data: <see cref="Plans"/>, <see cref="SeatStops"/>, the plans' volume tiers, caps and thresholds.
/// </summary>
/// <example><code>
/// &lt;arc:PricingCalculator SeatIndex="11" Billing="Yearly" Currency="USD" /&gt;
/// </code></example>
[TemplatePart(ControlsPanelPart, typeof(PricingCalculatorControlsPanel))]
[TemplatePart(MinusPart, typeof(AvaloniaButton))]
[TemplatePart(PlusPart, typeof(AvaloniaButton))]
[TemplatePart(SeatValuePart, typeof(PricingCalculatorDigits))]
[TemplatePart(SeatUnitPart, typeof(PricingCalculatorSwapText))]
[TemplatePart(SliderPart, typeof(PricingCalculatorSeatSlider))]
[TemplatePart(MarksPart, typeof(PricingCalculatorSeatMarks))]
[TemplatePart(BillingPart, typeof(PricingCalculatorBillingSwitch))]
[TemplatePart(PlansPart, typeof(PricingCalculatorPlansPanel))]
[TemplatePart(TotalsPanelPart, typeof(PricingCalculatorTotalsPanel))]
[TemplatePart(TotalsPlanPart, typeof(PricingCalculatorSwapText))]
[TemplatePart(TotalPart, typeof(PricingCalculatorDigits))]
[TemplatePart(TotalUnitPart, typeof(PricingCalculatorSwapText))]
[TemplatePart(RatePart, typeof(PricingCalculatorSwapText))]
[TemplatePart(SavingsPart, typeof(PricingCalculatorSwapPanel))]
[TemplatePart(SavingsAmountPart, typeof(PricingCalculatorDigits))]
[TemplatePart(SwitchYearlyPart, typeof(AvaloniaButton))]
[TemplatePart(TrialPart, typeof(Button))]
[TemplatePart(AnnouncerPart, typeof(AuthAnnouncer))]
[PseudoClasses(StackedPseudoClass, CompactPseudoClass, NarrowPseudoClass, PanelsPseudoClass)]
public class PricingCalculator : TemplatedControl
{
    public const string ControlsPanelPart = "PART_ControlsPanel";
    public const string TeamSizePart = "PART_TeamSize";
    public const string MinusPart = "PART_Minus";
    public const string PlusPart = "PART_Plus";
    public const string SeatValuePart = "PART_SeatValue";
    public const string SeatUnitPart = "PART_SeatUnit";
    public const string SliderPart = "PART_Slider";
    public const string MarksPart = "PART_Marks";
    public const string BillingPart = "PART_Billing";
    public const string PlansPart = "PART_Plans";
    public const string TotalsPanelPart = "PART_TotalsPanel";
    public const string TotalsPlanPart = "PART_TotalsPlan";
    public const string TotalPart = "PART_Total";
    public const string TotalUnitPart = "PART_TotalUnit";
    public const string RatePart = "PART_Rate";
    public const string SavingsPart = "PART_Savings";
    public const string SavingsBeforePart = "PART_SavingsBefore";
    public const string SavingsAmountPart = "PART_SavingsAmount";
    public const string SavingsAfterPart = "PART_SavingsAfter";
    public const string SwitchYearlyPart = "PART_SwitchYearly";
    public const string TrialPart = "PART_Trial";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while the block is 860px wide or less: the cards stack.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set while the block is 560px wide or less.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the block is 360px wide or less.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while <see cref="ShowsPanels"/> is true.</summary>
    public const string PanelsPseudoClass = ":panels";

    #region Properties

    /// <summary>The heading above the controls.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PricingCalculator, string?>(nameof(Title), "Pick a plan that grows with you");

    /// <summary>The line under the heading.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<PricingCalculator, string?>(nameof(Description),
            "Slide to your headcount. Every plan starts with a 14 day free trial.");

    /// <summary>Whether the heading and its line show (true by default).</summary>
    public static readonly StyledProperty<bool> IsHeaderVisibleProperty =
        AvaloniaProperty.Register<PricingCalculator, bool>(nameof(IsHeaderVisible), true);

    /// <summary>
    /// Whether the controls and the totals sit on muted rounded panels (the look on a documentation page); false (default)
    /// keeps them flat on the page.
    /// </summary>
    public static readonly StyledProperty<bool> ShowsPanelsProperty =
        AvaloniaProperty.Register<PricingCalculator, bool>(nameof(ShowsPanels));

    /// <summary>The plans, in display order (three is the design target; two to four fit). Plans written inline in XAML land here.</summary>
    public static readonly DirectProperty<PricingCalculator, IList<PricingCalculatorPlan>> PlansProperty =
        AvaloniaProperty.RegisterDirect<PricingCalculator, IList<PricingCalculatorPlan>>(nameof(Plans), o => o.Plans);

    /// <summary>A source of plans that wins over <see cref="Plans"/>; a collection that raises changes is followed.</summary>
    public static readonly StyledProperty<IEnumerable<PricingCalculatorPlan>?> PlansSourceProperty =
        AvaloniaProperty.Register<PricingCalculator, IEnumerable<PricingCalculatorPlan>?>(nameof(PlansSource));

    /// <summary>
    /// The seat counts the slider stops at, ascending, evenly spaced along the track; null uses
    /// the 23 sample stops from 1 to 300.
    /// </summary>
    public static readonly StyledProperty<IReadOnlyList<int>?> SeatStopsProperty =
        AvaloniaProperty.Register<PricingCalculator, IReadOnlyList<int>?>(nameof(SeatStops));

    /// <summary>The index of the current seat stop (two-way; 11 = 20 seats on the sample stops).</summary>
    public static readonly StyledProperty<int> SeatIndexProperty =
        AvaloniaProperty.Register<PricingCalculator, int>(nameof(SeatIndex), 11, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The billing period (two-way; yearly by default).</summary>
    public static readonly StyledProperty<PricingCalculatorBilling> BillingProperty =
        AvaloniaProperty.Register<PricingCalculator, PricingCalculatorBilling>(nameof(Billing), PricingCalculatorBilling.Yearly,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The <see cref="PricingCalculatorPlan.Id"/> of the plan the user picked (two-way); null follows the recommendation.
    /// The pick is remembered while its plan's cap is exceeded and applies again once the seats fit.
    /// </summary>
    public static readonly StyledProperty<string?> PickedPlanIdProperty =
        AvaloniaProperty.Register<PricingCalculator, string?>(nameof(PickedPlanId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The share yearly billing takes off (0.2 by default): the yearly price of plans without one, and the note after the
    /// yearly option.
    /// </summary>
    public static readonly StyledProperty<double> YearlyDiscountProperty =
        AvaloniaProperty.Register<PricingCalculator, double>(nameof(YearlyDiscount), 0.2);

    /// <summary>ISO 4217 code of every amount ("USD" by default).</summary>
    public static readonly StyledProperty<string> CurrencyProperty =
        AvaloniaProperty.Register<PricingCalculator, string>(nameof(Currency), "USD");

    /// <summary>
    /// Optional: chooses the recommended plan for a seat count from the plans; null uses each plan's
    /// <see cref="PricingCalculatorPlan.RecommendFromSeats"/> (the largest threshold reached wins).
    /// </summary>
    public static readonly StyledProperty<Func<int, IReadOnlyList<PricingCalculatorPlan>, PricingCalculatorPlan?>?> RecommendPlanProperty =
        AvaloniaProperty.Register<PricingCalculator, Func<int, IReadOnlyList<PricingCalculatorPlan>, PricingCalculatorPlan?>?>(nameof(RecommendPlan));

    /// <summary>
    /// Starts a trial on your backend; true when it started. Null uses <see cref="PricingCalculatorSamples.SimulatedTrial"/>
    /// (1.1 s, always succeeds). The trial button shows a spinner while the task runs.
    /// </summary>
    public static readonly StyledProperty<Func<PricingCalculatorTrialRequest, CancellationToken, Task<bool>>?> TrialHandlerProperty =
        AvaloniaProperty.Register<PricingCalculator, Func<PricingCalculatorTrialRequest, CancellationToken, Task<bool>>?>(nameof(TrialHandler));

    /// <summary>How long the trial button says the trial started before it offers it again (2.8 s).</summary>
    public static readonly StyledProperty<TimeSpan> TrialSuccessDurationProperty =
        AvaloniaProperty.Register<PricingCalculator, TimeSpan>(nameof(TrialSuccessDuration), TimeSpan.FromMilliseconds(2800));

    /// <summary>The built-in text and its culture; replace the object to localize the block.</summary>
    public static readonly StyledProperty<PricingCalculatorLabels> LabelsProperty =
        AvaloniaProperty.Register<PricingCalculator, PricingCalculatorLabels>(nameof(Labels), new PricingCalculatorLabels());

    /// <summary>Invoked with the <see cref="PricingCalculatorPlan"/> when the user picks a plan.</summary>
    public static readonly StyledProperty<ICommand?> PlanPickedCommandProperty =
        AvaloniaProperty.Register<PricingCalculator, ICommand?>(nameof(PlanPickedCommand));

    /// <summary>Invoked with the <see cref="PricingCalculatorTrialRequest"/> once a trial started.</summary>
    public static readonly StyledProperty<ICommand?> TrialStartedCommandProperty =
        AvaloniaProperty.Register<PricingCalculator, ICommand?>(nameof(TrialStartedCommand));

    /// <summary>The current seat count (the stop at <see cref="SeatIndex"/>).</summary>
    public static readonly DirectProperty<PricingCalculator, int> SeatsProperty =
        AvaloniaProperty.RegisterDirect<PricingCalculator, int>(nameof(Seats), o => o.Seats);

    /// <summary>The plan recommended for the current seats.</summary>
    public static readonly DirectProperty<PricingCalculator, PricingCalculatorPlan?> RecommendedPlanProperty =
        AvaloniaProperty.RegisterDirect<PricingCalculator, PricingCalculatorPlan?>(nameof(RecommendedPlan), o => o.RecommendedPlan);

    /// <summary>The plan in use: the picked plan while it covers the seats, otherwise the recommended one.</summary>
    public static readonly DirectProperty<PricingCalculator, PricingCalculatorPlan?> SelectedPlanProperty =
        AvaloniaProperty.RegisterDirect<PricingCalculator, PricingCalculatorPlan?>(nameof(SelectedPlan), o => o.SelectedPlan);

    /// <summary>What the selected plan costs for one billing period (a year with yearly billing).</summary>
    public static readonly DirectProperty<PricingCalculator, decimal> TotalProperty =
        AvaloniaProperty.RegisterDirect<PricingCalculator, decimal>(nameof(Total), o => o.Total);

    /// <summary>What yearly billing saves on the selected plan in a year.</summary>
    public static readonly DirectProperty<PricingCalculator, decimal> YearlySavingsProperty =
        AvaloniaProperty.RegisterDirect<PricingCalculator, decimal>(nameof(YearlySavings), o => o.YearlySavings);

    /// <summary>Where the trial button stands.</summary>
    public static readonly DirectProperty<PricingCalculator, PricingCalculatorTrialState> TrialStateProperty =
        AvaloniaProperty.RegisterDirect<PricingCalculator, PricingCalculatorTrialState>(nameof(TrialState), o => o.TrialState);

    /// <summary>Block width at or below which the cards stack (theme token).</summary>
    internal static readonly StyledProperty<double> StackedWidthProperty =
        AvaloniaProperty.Register<PricingCalculator, double>(nameof(StackedWidth), 860);

    /// <summary>Block width at or below which the compact layout applies (theme token).</summary>
    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<PricingCalculator, double>(nameof(CompactWidth), 560);

    /// <summary>Block width at or below which the narrow layout applies (theme token).</summary>
    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<PricingCalculator, double>(nameof(NarrowWidth), 360);

    /// <summary>Raised when the user picks a plan (a card's button, including a capped card's "back to the cap" button).</summary>
    public static readonly RoutedEvent<PricingCalculatorPlanPickedEventArgs> PlanPickedEvent =
        RoutedEvent.Register<PricingCalculator, PricingCalculatorPlanPickedEventArgs>(nameof(PlanPicked), RoutingStrategies.Bubble);

    /// <summary>Raised when the seat count changes.</summary>
    public static readonly RoutedEvent<PricingCalculatorSeatsChangedEventArgs> SeatsChangedEvent =
        RoutedEvent.Register<PricingCalculator, PricingCalculatorSeatsChangedEventArgs>(nameof(SeatsChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the billing period changes.</summary>
    public static readonly RoutedEvent<PricingCalculatorBillingChangedEventArgs> BillingChangedEvent =
        RoutedEvent.Register<PricingCalculator, PricingCalculatorBillingChangedEventArgs>(nameof(BillingChanged), RoutingStrategies.Bubble);

    /// <summary>Raised once a trial started.</summary>
    public static readonly RoutedEvent<PricingCalculatorTrialStartedEventArgs> TrialStartedEvent =
        RoutedEvent.Register<PricingCalculator, PricingCalculatorTrialStartedEventArgs>(nameof(TrialStarted), RoutingStrategies.Bubble);

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="IsHeaderVisibleProperty"/>
    public bool IsHeaderVisible { get => GetValue(IsHeaderVisibleProperty); set => SetValue(IsHeaderVisibleProperty, value); }

    /// <inheritdoc cref="ShowsPanelsProperty"/>
    public bool ShowsPanels { get => GetValue(ShowsPanelsProperty); set => SetValue(ShowsPanelsProperty, value); }

    /// <inheritdoc cref="PlansProperty"/>
    [Content]
    public IList<PricingCalculatorPlan> Plans
    {
        get => _plans;
        set
        {
            var list = value ?? new AvaloniaList<PricingCalculatorPlan>();
            if (SetAndRaise(PlansProperty, ref _plans, list))
            {
                WatchCollection(ref _watchedPlans, list as INotifyCollectionChanged);
                RebuildCards();
            }
        }
    }

    /// <inheritdoc cref="PlansSourceProperty"/>
    public IEnumerable<PricingCalculatorPlan>? PlansSource { get => GetValue(PlansSourceProperty); set => SetValue(PlansSourceProperty, value); }

    /// <inheritdoc cref="SeatStopsProperty"/>
    public IReadOnlyList<int>? SeatStops { get => GetValue(SeatStopsProperty); set => SetValue(SeatStopsProperty, value); }

    /// <inheritdoc cref="SeatIndexProperty"/>
    public int SeatIndex { get => GetValue(SeatIndexProperty); set => SetValue(SeatIndexProperty, value); }

    /// <inheritdoc cref="BillingProperty"/>
    public PricingCalculatorBilling Billing { get => GetValue(BillingProperty); set => SetValue(BillingProperty, value); }

    /// <inheritdoc cref="PickedPlanIdProperty"/>
    public string? PickedPlanId { get => GetValue(PickedPlanIdProperty); set => SetValue(PickedPlanIdProperty, value); }

    /// <inheritdoc cref="YearlyDiscountProperty"/>
    public double YearlyDiscount { get => GetValue(YearlyDiscountProperty); set => SetValue(YearlyDiscountProperty, value); }

    /// <inheritdoc cref="CurrencyProperty"/>
    public string Currency { get => GetValue(CurrencyProperty); set => SetValue(CurrencyProperty, value); }

    /// <inheritdoc cref="RecommendPlanProperty"/>
    public Func<int, IReadOnlyList<PricingCalculatorPlan>, PricingCalculatorPlan?>? RecommendPlan
    {
        get => GetValue(RecommendPlanProperty);
        set => SetValue(RecommendPlanProperty, value);
    }

    /// <inheritdoc cref="TrialHandlerProperty"/>
    public Func<PricingCalculatorTrialRequest, CancellationToken, Task<bool>>? TrialHandler
    {
        get => GetValue(TrialHandlerProperty);
        set => SetValue(TrialHandlerProperty, value);
    }

    /// <inheritdoc cref="TrialSuccessDurationProperty"/>
    public TimeSpan TrialSuccessDuration { get => GetValue(TrialSuccessDurationProperty); set => SetValue(TrialSuccessDurationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public PricingCalculatorLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="PlanPickedCommandProperty"/>
    public ICommand? PlanPickedCommand { get => GetValue(PlanPickedCommandProperty); set => SetValue(PlanPickedCommandProperty, value); }

    /// <inheritdoc cref="TrialStartedCommandProperty"/>
    public ICommand? TrialStartedCommand { get => GetValue(TrialStartedCommandProperty); set => SetValue(TrialStartedCommandProperty, value); }

    /// <inheritdoc cref="SeatsProperty"/>
    public int Seats { get => _seats; private set => SetAndRaise(SeatsProperty, ref _seats, value); }

    /// <inheritdoc cref="RecommendedPlanProperty"/>
    public PricingCalculatorPlan? RecommendedPlan { get => _recommended; private set => SetAndRaise(RecommendedPlanProperty, ref _recommended, value); }

    /// <inheritdoc cref="SelectedPlanProperty"/>
    public PricingCalculatorPlan? SelectedPlan { get => _selected; private set => SetAndRaise(SelectedPlanProperty, ref _selected, value); }

    /// <inheritdoc cref="TotalProperty"/>
    public decimal Total { get => _total; private set => SetAndRaise(TotalProperty, ref _total, value); }

    /// <inheritdoc cref="YearlySavingsProperty"/>
    public decimal YearlySavings { get => _savings; private set => SetAndRaise(YearlySavingsProperty, ref _savings, value); }

    /// <inheritdoc cref="TrialStateProperty"/>
    public PricingCalculatorTrialState TrialState { get => _trialState; private set => SetAndRaise(TrialStateProperty, ref _trialState, value); }

    internal double StackedWidth { get => GetValue(StackedWidthProperty); set => SetValue(StackedWidthProperty, value); }
    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    /// <inheritdoc cref="PlanPickedEvent"/>
    public event EventHandler<PricingCalculatorPlanPickedEventArgs>? PlanPicked
    {
        add => AddHandler(PlanPickedEvent, value);
        remove => RemoveHandler(PlanPickedEvent, value);
    }

    /// <inheritdoc cref="SeatsChangedEvent"/>
    public event EventHandler<PricingCalculatorSeatsChangedEventArgs>? SeatsChanged
    {
        add => AddHandler(SeatsChangedEvent, value);
        remove => RemoveHandler(SeatsChangedEvent, value);
    }

    /// <inheritdoc cref="BillingChangedEvent"/>
    public event EventHandler<PricingCalculatorBillingChangedEventArgs>? BillingChanged
    {
        add => AddHandler(BillingChangedEvent, value);
        remove => RemoveHandler(BillingChangedEvent, value);
    }

    /// <inheritdoc cref="TrialStartedEvent"/>
    public event EventHandler<PricingCalculatorTrialStartedEventArgs>? TrialStarted
    {
        add => AddHandler(TrialStartedEvent, value);
        remove => RemoveHandler(TrialStartedEvent, value);
    }

    #endregion

    private IList<PricingCalculatorPlan> _plans = new AvaloniaList<PricingCalculatorPlan>();
    private INotifyCollectionChanged? _watchedPlans;
    private INotifyCollectionChanged? _watchedSource;
    private AvaloniaList<PricingCalculatorPlan>? _samplePlans;
    private readonly List<PricingCalculatorPlan> _resolved = new();
    private readonly List<PricingCalculatorCard> _cards = new();
    private readonly HashSet<PricingCalculatorPlan> _watchedItems = new();
    private int _seats;
    private PricingCalculatorPlan? _recommended;
    private PricingCalculatorPlan? _selected;
    private decimal _total;
    private decimal _savings;
    private PricingCalculatorTrialState _trialState;
    private CancellationTokenSource? _trialCts;
    private bool _refreshPosted;

    private PricingCalculatorControlsPanel? _controlsPanel;
    private TextBlock? _teamSize;
    private AvaloniaButton? _minus;
    private AvaloniaButton? _plus;
    private PricingCalculatorDigits? _seatValue;
    private PricingCalculatorSwapText? _seatUnit;
    private PricingCalculatorSeatSlider? _slider;
    private PricingCalculatorSeatMarks? _marks;
    private PricingCalculatorBillingSwitch? _billing;
    private PricingCalculatorPlansPanel? _plansPanel;
    private PricingCalculatorTotalsPanel? _totalsPanel;
    private PricingCalculatorSwapText? _totalsPlan;
    private PricingCalculatorDigits? _totalDigits;
    private PricingCalculatorSwapText? _totalUnit;
    private PricingCalculatorSwapText? _rate;
    private PricingCalculatorSwapPanel? _savingsPanel;
    private TextBlock? _savingsBefore;
    private PricingCalculatorDigits? _savingsAmount;
    private TextBlock? _savingsAfter;
    private AvaloniaButton? _switchYearly;
    private Button? _trial;
    private AuthAnnouncer? _announcer;

    /// <summary>Waits between trial steps; tests replace it to step the clock.</summary>
    internal Func<TimeSpan, CancellationToken, Task> DelayAsync { get; set; } = Task.Delay;

    /// <summary>Announcements so far (tests read them).</summary>
    internal IReadOnlyList<string> Announcements => _announcer?.History ?? (IReadOnlyList<string>)Array.Empty<string>();

    /// <summary>The cards, in order (tests read them).</summary>
    internal IReadOnlyList<PricingCalculatorCard> Cards => _cards;

    /// <summary>The plans in use (own, source or samples).</summary>
    internal IReadOnlyList<PricingCalculatorPlan> ResolvedPlans => _resolved;

    /// <summary>The seat stops in use.</summary>
    internal IReadOnlyList<int> ResolvedStops => SeatStops is { Count: > 0 } stops ? stops : PricingCalculatorSamples.SeatStops;

    internal PricingCalculatorSeatSlider? Slider => _slider;
    internal PricingCalculatorBillingSwitch? BillingSwitch => _billing;
    internal PricingCalculatorPlansPanel? PlansPanel => _plansPanel;
    internal AvaloniaButton? MinusButton => _minus;
    internal AvaloniaButton? PlusButton => _plus;
    internal Button? TrialButton => _trial;
    internal AvaloniaButton? SwitchYearlyButton => _switchYearly;
    internal PricingCalculatorDigits? TotalDigits => _totalDigits;
    internal PricingCalculatorSwapPanel? SavingsPanel => _savingsPanel;
    internal PricingCalculatorSeatMarks? Marks => _marks;
    internal PricingCalculatorTotalsPanel? TotalsPanel => _totalsPanel;
    internal PricingCalculatorControlsPanel? ControlsPanel => _controlsPanel;

    public PricingCalculator()
    {
        WatchCollection(ref _watchedPlans, _plans as INotifyCollectionChanged);
    }

    #region Public methods

    /// <summary>Moves the slider to the stop closest to <paramref name="seats"/>.</summary>
    public void SetSeats(int seats) => SetCurrentValue(SeatIndexProperty, PricingCalculatorMath.NearestStopIndex(ResolvedStops, seats));

    /// <summary>
    /// Picks the plan with this id, as its button does: a plan above its cap also moves the seats back to the cap.
    /// Raises <see cref="PlanPicked"/> and announces the pick.
    /// </summary>
    public void PickPlan(string planId)
    {
        foreach (var plan in _resolved)
        {
            if (plan.Key == planId)
            {
                Pick(plan);
                return;
            }
        }
    }

    /// <summary>
    /// Starts the trial of the selected plan, as the trial button does: ignored while a request runs or the started state
    /// shows. Returns true when the trial started.
    /// </summary>
    public async Task<bool> StartTrialAsync()
    {
        if (TrialState != PricingCalculatorTrialState.Idle || SelectedPlan is not { } plan)
        {
            return false;
        }
        var request = new PricingCalculatorTrialRequest(plan, Seats, Billing, Total);
        _trialCts?.Cancel();
        var cts = new CancellationTokenSource();
        _trialCts = cts;
        TrialState = PricingCalculatorTrialState.Starting;
        Refresh();
        bool started;
        try
        {
            var handler = TrialHandler ?? PricingCalculatorSamples.SimulatedTrial(DelayAsync);
            started = await handler(request, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            started = false;
        }
        if (cts.IsCancellationRequested)
        {
            return false;
        }
        var labels = Labels;
        if (!started)
        {
            TrialState = PricingCalculatorTrialState.Idle;
            Refresh();
            _announcer?.Announce(labels.AnnounceTrialFailed);
            return false;
        }
        TrialState = PricingCalculatorTrialState.Started;
        Refresh();
        _announcer?.Announce(labels.AnnounceTrial(plan.Name, labels.SeatCount(PricingCalculatorMath.Count(request.Seats, labels.Culture), request.Seats)));
        RaiseEvent(new PricingCalculatorTrialStartedEventArgs(TrialStartedEvent, request));
        if (TrialStartedCommand is { } command && command.CanExecute(request))
        {
            command.Execute(request);
        }
        _ = ResetTrialAfterAsync(cts);
        return true;
    }

    private async Task ResetTrialAfterAsync(CancellationTokenSource cts)
    {
        try
        {
            await DelayAsync(TrialSuccessDuration, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        if (!cts.IsCancellationRequested && ReferenceEquals(cts, _trialCts))
        {
            TrialState = PricingCalculatorTrialState.Idle;
            Refresh();
        }
    }

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        var scope = e.NameScope;
        _controlsPanel = scope.Find<PricingCalculatorControlsPanel>(ControlsPanelPart);
        _teamSize = scope.Find<TextBlock>(TeamSizePart);
        _minus = scope.Find<AvaloniaButton>(MinusPart);
        _plus = scope.Find<AvaloniaButton>(PlusPart);
        _seatValue = scope.Find<PricingCalculatorDigits>(SeatValuePart);
        _seatUnit = scope.Find<PricingCalculatorSwapText>(SeatUnitPart);
        _slider = scope.Find<PricingCalculatorSeatSlider>(SliderPart);
        _marks = scope.Find<PricingCalculatorSeatMarks>(MarksPart);
        _billing = scope.Find<PricingCalculatorBillingSwitch>(BillingPart);
        _plansPanel = scope.Find<PricingCalculatorPlansPanel>(PlansPart);
        _totalsPanel = scope.Find<PricingCalculatorTotalsPanel>(TotalsPanelPart);
        _totalsPlan = scope.Find<PricingCalculatorSwapText>(TotalsPlanPart);
        _totalDigits = scope.Find<PricingCalculatorDigits>(TotalPart);
        _totalUnit = scope.Find<PricingCalculatorSwapText>(TotalUnitPart);
        _rate = scope.Find<PricingCalculatorSwapText>(RatePart);
        _savingsPanel = scope.Find<PricingCalculatorSwapPanel>(SavingsPart);
        _savingsBefore = scope.Find<TextBlock>(SavingsBeforePart);
        _savingsAmount = scope.Find<PricingCalculatorDigits>(SavingsAmountPart);
        _savingsAfter = scope.Find<TextBlock>(SavingsAfterPart);
        _switchYearly = scope.Find<AvaloniaButton>(SwitchYearlyPart);
        _trial = scope.Find<Button>(TrialPart);
        _announcer = scope.Find<AuthAnnouncer>(AnnouncerPart);

        if (_minus is not null)
        {
            _minus.Click += OnMinusClick;
        }
        if (_plus is not null)
        {
            _plus.Click += OnPlusClick;
        }
        if (_slider is not null)
        {
            _slider.PropertyChanged += OnSliderPropertyChanged;
        }
        if (_billing is not null)
        {
            _billing.PropertyChanged += OnBillingPropertyChanged;
        }
        if (_switchYearly is not null)
        {
            _switchYearly.Click += OnSwitchYearlyClick;
        }
        if (_trial is not null)
        {
            _trial.Click += OnTrialClick;
        }
        if (_plansPanel is not null)
        {
            ArcTokens.Bind(_plansPanel.Frame, GlidingIndicator.StrokeProperty, ArcDesignTokenKind.Accent);
            _plansPanel.Frame.BindCornerRadius(ArcDesignTokenKind.RadiusPanel);
        }
        RebuildCards();
        UpdateLayoutMode(Bounds.Width);
    }

    private void Unhook()
    {
        if (_minus is not null)
        {
            _minus.Click -= OnMinusClick;
        }
        if (_plus is not null)
        {
            _plus.Click -= OnPlusClick;
        }
        if (_slider is not null)
        {
            _slider.PropertyChanged -= OnSliderPropertyChanged;
        }
        if (_billing is not null)
        {
            _billing.PropertyChanged -= OnBillingPropertyChanged;
        }
        if (_switchYearly is not null)
        {
            _switchYearly.Click -= OnSwitchYearlyClick;
        }
        if (_trial is not null)
        {
            _trial.Click -= OnTrialClick;
        }
        foreach (var card in _cards)
        {
            card.ActionInvoked -= OnCardAction;
        }
        _plansPanel?.Children.RemoveAll(_cards);
    }

    private void OnMinusClick(object? sender, RoutedEventArgs e) => StepSeats(-1);

    private void OnPlusClick(object? sender, RoutedEventArgs e) => StepSeats(1);

    private void StepSeats(int delta)
        => SetCurrentValue(SeatIndexProperty, Math.Clamp(SeatIndex + delta, 0, Math.Max(0, ResolvedStops.Count - 1)));

    private void OnSliderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == RangeBase.ValueProperty && _slider is not null && _slider.Index != SeatIndex)
        {
            SetCurrentValue(SeatIndexProperty, _slider.Index);
        }
    }

    private void OnBillingPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == PricingCalculatorBillingSwitch.IsYearlyProperty && _billing is not null)
        {
            var billing = _billing.IsYearly ? PricingCalculatorBilling.Yearly : PricingCalculatorBilling.Monthly;
            if (billing != Billing)
            {
                SetCurrentValue(BillingProperty, billing);
            }
        }
    }

    private void OnSwitchYearlyClick(object? sender, RoutedEventArgs e) => SetCurrentValue(BillingProperty, PricingCalculatorBilling.Yearly);

    private void OnTrialClick(object? sender, RoutedEventArgs e) => _ = StartTrialAsync();

    private void OnCardAction(object? sender, RoutedEventArgs e)
    {
        if (sender is PricingCalculatorCard card)
        {
            Pick(card.Plan);
        }
    }

    private void Pick(PricingCalculatorPlan plan)
    {
        if (!PricingCalculatorMath.IsAvailable(plan, Seats) && plan.MaxSeats is { } cap)
        {
            // Back to the largest stop the plan covers.
            var stops = ResolvedStops;
            var index = 0;
            for (var i = 0; i < stops.Count; i++)
            {
                if (stops[i] <= cap)
                {
                    index = i;
                }
            }
            SetCurrentValue(SeatIndexProperty, index);
        }
        SetCurrentValue(PickedPlanIdProperty, plan.Key);
        Refresh();
        _announcer?.Announce(Labels.AnnouncePicked(plan.Name));
        RaiseEvent(new PricingCalculatorPlanPickedEventArgs(PlanPickedEvent, plan, Seats));
        if (PlanPickedCommand is { } command && command.CanExecute(plan))
        {
            command.Execute(plan);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SeatIndexProperty)
        {
            var old = Seats;
            Refresh();
            if (old != Seats)
            {
                RaiseEvent(new PricingCalculatorSeatsChangedEventArgs(SeatsChangedEvent, old, Seats));
            }
        }
        else if (change.Property == BillingProperty)
        {
            Refresh();
            var billing = change.GetNewValue<PricingCalculatorBilling>();
            _announcer?.Announce(billing == PricingCalculatorBilling.Yearly ? Labels.AnnounceYearly : Labels.AnnounceMonthly);
            RaiseEvent(new PricingCalculatorBillingChangedEventArgs(BillingChangedEvent, change.GetOldValue<PricingCalculatorBilling>(), billing));
        }
        else if (change.Property == PlansSourceProperty)
        {
            WatchCollection(ref _watchedSource, PlansSource as INotifyCollectionChanged);
            RebuildCards();
        }
        else if (change.Property == SeatStopsProperty)
        {
            RebuildMarks();
            Refresh();
        }
        else if (change.Property == PickedPlanIdProperty || change.Property == YearlyDiscountProperty ||
                 change.Property == CurrencyProperty || change.Property == RecommendPlanProperty || change.Property == LabelsProperty)
        {
            if (change.Property == LabelsProperty || change.Property == RecommendPlanProperty)
            {
                RebuildMarks();
            }
            Refresh();
        }
        else if (change.Property == ShowsPanelsProperty)
        {
            PseudoClasses.Set(PanelsPseudoClass, ShowsPanels);
        }
        else if (change.Property == StackedWidthProperty || change.Property == CompactWidthProperty || change.Property == NarrowWidthProperty)
        {
            UpdateLayoutMode(Bounds.Width);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateLayoutMode(e.NewSize.Width);
    }

    private void UpdateLayoutMode(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var stacked = width <= StackedWidth;
        var compact = width <= CompactWidth;
        PseudoClasses.Set(StackedPseudoClass, stacked);
        PseudoClasses.Set(CompactPseudoClass, compact);
        PseudoClasses.Set(NarrowPseudoClass, width <= NarrowWidth);
        if (_plansPanel is not null && _plansPanel.IsStacked != stacked)
        {
            _plansPanel.IsStacked = stacked;
            _plansPanel.InvalidateMeasure();
        }
        if (_totalsPanel is not null)
        {
            _totalsPanel.Mode = compact ? PricingCalculatorTotalsMode.Column : stacked ? PricingCalculatorTotalsMode.TwoRows : PricingCalculatorTotalsMode.Row;
        }
        if (_billing is not null)
        {
            _billing.IsUniform = compact;
        }
        foreach (var card in _cards)
        {
            card.IsCompact = compact;
            card.IsCollapsed = stacked && !ReferenceEquals(card.Plan, SelectedPlan);
        }
    }

    private void WatchCollection(ref INotifyCollectionChanged? field, INotifyCollectionChanged? next)
    {
        if (field is not null)
        {
            field.CollectionChanged -= OnPlansCollectionChanged;
        }
        field = next;
        if (field is not null)
        {
            field.CollectionChanged += OnPlansCollectionChanged;
        }
    }

    private void OnPlansCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildCards();

    private void OnPlanChanged(object? sender, EventArgs e)
    {
        // Several properties of a plan often change together: refresh once.
        if (_refreshPosted)
        {
            return;
        }
        _refreshPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshPosted = false;
            RebuildMarks();
            Refresh();
        });
    }

    private void ResolvePlans()
    {
        foreach (var plan in _watchedItems)
        {
            plan.Changed -= OnPlanChanged;
        }
        _watchedItems.Clear();
        _resolved.Clear();
        IEnumerable<PricingCalculatorPlan> source = PlansSource ?? (_plans.Count > 0 ? _plans : _samplePlans ??= PricingCalculatorSamples.CreatePlans());
        foreach (var plan in source)
        {
            _resolved.Add(plan);
            if (_watchedItems.Add(plan))
            {
                plan.Changed += OnPlanChanged;
            }
        }
    }

    private void RebuildCards()
    {
        ResolvePlans();
        if (_plansPanel is null)
        {
            Refresh();
            return;
        }
        foreach (var card in _cards)
        {
            card.ActionInvoked -= OnCardAction;
            _plansPanel.Children.Remove(card);
        }
        _cards.Clear();
        _plansPanel.Selected = null;
        _plansPanel.Recommended = null;
        foreach (var plan in _resolved)
        {
            var card = new PricingCalculatorCard(plan);
            card.ActionInvoked += OnCardAction;
            _cards.Add(card);
            _plansPanel.Children.Insert(_cards.Count - 1, card);
        }
        RebuildMarks();
        Refresh();
        UpdateLayoutMode(Bounds.Width);
    }

    private PricingCalculatorPlan? Recommend(int seats)
        => RecommendPlan is { } custom ? custom(seats, _resolved) : PricingCalculatorMath.Recommend(_resolved, seats);

    private void RebuildMarks()
    {
        if (_marks is null)
        {
            return;
        }
        _marks.Children.Clear();
        var stops = ResolvedStops;
        _marks.Maximum = Math.Max(1, stops.Count - 1);
        var first = true;
        foreach (var plan in _resolved)
        {
            var index = -1;
            for (var i = 0; i < stops.Count; i++)
            {
                if (ReferenceEquals(Recommend(stops[i]), plan))
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
            {
                continue;
            }
            _marks.Children.Add(new PricingCalculatorMarkLabel { Text = plan.Name, Index = index, HasTick = !first, Tag = plan });
            first = false;
        }
    }

    /// <summary>Recomputes every derived value and pushes it to the parts.</summary>
    private void Refresh()
    {
        var stops = ResolvedStops;
        var index = Math.Clamp(SeatIndex, 0, Math.Max(0, stops.Count - 1));
        var seats = stops.Count > 0 ? stops[index] : 1;
        var billing = Billing;
        var labels = Labels;
        var culture = labels.Culture;
        var currency = Currency;
        var discount = YearlyDiscount;
        var recommended = Recommend(seats);
        var selected = PricingCalculatorMath.Select(_resolved, PickedPlanId, seats, recommended);
        Seats = seats;
        RecommendedPlan = recommended;
        SelectedPlan = selected;
        Total = selected is null ? 0 : PricingCalculatorMath.PlanTotal(selected, billing, seats, discount);
        YearlySavings = selected is null ? 0 : PricingCalculatorMath.YearlySavings(selected, seats, discount);
        var seatCount = labels.SeatCount(PricingCalculatorMath.Count(seats, culture), seats);

        // Seat controls.
        if (_seatValue is not null)
        {
            _seatValue.Culture = culture;
            _seatValue.Value = seats;
        }
        if (_seatUnit is not null)
        {
            _seatUnit.Text = labels.SeatUnit(seats);
        }
        if (_slider is not null)
        {
            _slider.Maximum = Math.Max(0, stops.Count - 1);
            _slider.Value = index;
            AutomationProperties.SetName(_slider, labels.TeamSize);
            AutomationProperties.SetHelpText(_slider, seatCount);
        }
        if (_teamSize is not null)
        {
            _teamSize.Text = labels.TeamSize;
        }
        if (_minus is not null)
        {
            AutomationProperties.SetName(_minus, labels.RemoveSeat);
        }
        if (_plus is not null)
        {
            AutomationProperties.SetName(_plus, labels.AddSeat);
        }
        if (_marks is not null)
        {
            foreach (var child in _marks.Children)
            {
                if (child is PricingCalculatorMarkLabel mark)
                {
                    mark.IsOn = ReferenceEquals(mark.Tag, recommended);
                }
            }
        }
        if (_billing is not null)
        {
            _billing.IsYearly = billing == PricingCalculatorBilling.Yearly;
            _billing.MonthlyText = labels.Monthly;
            _billing.YearlyText = labels.Yearly;
            _billing.YearlyNote = labels.YearlyNote(PricingCalculatorMath.DiscountPercent(discount));
            AutomationProperties.SetName(_billing, labels.BillingPeriod);
        }

        // Cards.
        foreach (var card in _cards)
        {
            var plan = card.Plan;
            var available = PricingCalculatorMath.IsAvailable(plan, seats);
            var isSelected = ReferenceEquals(plan, selected);
            var price = PricingCalculatorMath.SeatPrice(plan, billing, seats, discount);
            var total = PricingCalculatorMath.PlanTotal(plan, billing, seats, discount);
            var priceShape = PricingCalculatorMath.ShapeOf(price, currency, culture);
            var totalShape = PricingCalculatorMath.ShapeOf(total, currency, culture);
            card.PlanName = plan.Name;
            card.Description = plan.Description;
            card.BadgeText = labels.Recommended;
            card.Culture = culture;
            card.MoneyPrefix = priceShape.Prefix;
            card.MoneySuffix = priceShape.Suffix;
            card.PriceDecimals = priceShape.Decimals;
            card.TotalDecimals = totalShape.Decimals;
            card.PriceValue = price;
            card.TotalValue = total;
            card.PerSeat = labels.PerSeat;
            card.PerMonth = labels.PerMonth;
            card.TotalCaption = billing == PricingCalculatorBilling.Yearly ? labels.CardTotalYearly(seatCount) : labels.CardTotalMonthly(seatCount);
            if (plan.MaxSeats is { } cap)
            {
                var capText = labels.SeatCount(PricingCalculatorMath.Count(cap, culture), cap);
                card.LimitTitle = labels.LimitTitle(plan.Name, capText);
                card.LimitDetail = labels.LimitDetail(seatCount);
                card.SetAction(isSelected ? labels.Selected : available ? labels.Pick(plan.Name) : labels.UseCap(capText), isSelected);
            }
            else
            {
                card.SetAction(isSelected ? labels.Selected : labels.Pick(plan.Name), isSelected);
            }
            card.AreaIndex = available ? 0 : 1;
            card.IsUnavailable = !available;
            card.IsSelected = isSelected;
            card.IsCollapsed = PseudoClasses.Contains(StackedPseudoClass) && !isSelected;
            card.SetLines(PricingCalculatorMath.Features(plan, seats, billing, currency, labels), labels.PlanFeatures(plan.Name));
        }
        if (_plansPanel is not null)
        {
            _plansPanel.Selected = _cards.FirstOrDefault(c => ReferenceEquals(c.Plan, selected));
            _plansPanel.Recommended = _cards.FirstOrDefault(c => ReferenceEquals(c.Plan, recommended));
        }

        // Totals.
        if (selected is not null)
        {
            var seatPrice = PricingCalculatorMath.SeatPrice(selected, billing, seats, discount);
            var priceText = PricingCalculatorMath.Money(seatPrice, currency, culture);
            if (_totalsPlan is not null)
            {
                _totalsPlan.Text = labels.TotalsPlan(selected.Name, seatCount);
            }
            if (_totalDigits is not null)
            {
                var shape = PricingCalculatorMath.ShapeOf(Total, currency, culture);
                _totalDigits.Culture = culture;
                _totalDigits.Prefix = shape.Prefix;
                _totalDigits.Suffix = shape.Suffix;
                _totalDigits.Decimals = shape.Decimals;
                _totalDigits.Value = Total;
            }
            if (_totalUnit is not null)
            {
                _totalUnit.Text = billing == PricingCalculatorBilling.Yearly ? labels.PerYear : labels.PerMonthTotal;
            }
            if (_rate is not null)
            {
                _rate.Text = billing == PricingCalculatorBilling.Yearly ? labels.RateYearly(priceText) : labels.RateMonthly(priceText);
            }
            var savingsText = PricingCalculatorMath.Money(YearlySavings, currency, culture);
            if (_savingsAmount is not null)
            {
                var shape = PricingCalculatorMath.ShapeOf(YearlySavings, currency, culture);
                _savingsAmount.Culture = culture;
                _savingsAmount.Prefix = shape.Prefix;
                _savingsAmount.Suffix = shape.Suffix;
                _savingsAmount.Decimals = shape.Decimals;
                _savingsAmount.Value = YearlySavings;
            }
            var format = labels.SavingsFormat;
            var at = format.IndexOf("{0}", StringComparison.Ordinal);
            if (_savingsBefore is not null)
            {
                _savingsBefore.Text = at < 0 ? format : format[..at];
            }
            if (_savingsAfter is not null)
            {
                _savingsAfter.Text = at < 0 ? string.Empty : format[(at + 3)..];
            }
            if (_savingsPanel is not null)
            {
                _savingsPanel.SelectedIndex = YearlySavings <= 0 ? 2 : billing == PricingCalculatorBilling.Yearly ? 0 : 1;
                AutomationProperties.SetName(_savingsPanel, at < 0 ? format : format.Replace("{0}", savingsText, StringComparison.Ordinal));
            }
            if (_switchYearly is not null)
            {
                var text = labels.SwitchToYearly(savingsText);
                _switchYearly.Content = text;
                AutomationProperties.SetName(_switchYearly, text);
            }
            if (_trial is not null)
            {
                _trial.IsLoading = TrialState == PricingCalculatorTrialState.Starting;
                if (TrialState == PricingCalculatorTrialState.Started)
                {
                    if (_trial.Tag as string != "started")
                    {
                        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                        row.Children.Add(new ArcGlyph { Data = ArcIcons.Check, Size = 16, VerticalAlignment = VerticalAlignment.Center });
                        row.Children.Add(new TextBlock { Text = labels.TrialStarted, VerticalAlignment = VerticalAlignment.Center });
                        _trial.Content = row;
                        _trial.Tag = "started";
                        AutomationProperties.SetName(_trial, labels.TrialStarted);
                    }
                }
                else
                {
                    var text = labels.StartTrial(selected.Name);
                    if (_trial.Tag as string != text)
                    {
                        _trial.Content = text;
                        _trial.Tag = text;
                        AutomationProperties.SetName(_trial, text);
                    }
                }
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _trialCts?.Cancel();
        _trialCts = null;
        if (TrialState != PricingCalculatorTrialState.Idle)
        {
            TrialState = PricingCalculatorTrialState.Idle;
            Refresh();
        }
    }
}
