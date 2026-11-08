using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>How a <see cref="PricingCalculator"/> bills: every month, or once a year at the yearly seat price.</summary>
public enum PricingCalculatorBilling
{
    /// <summary>Billed every month at <see cref="PricingCalculatorPlan.MonthlySeatPrice"/>.</summary>
    Monthly,

    /// <summary>Billed once a year at <see cref="PricingCalculatorPlan.YearlySeatPrice"/> (shown per seat per month).</summary>
    Yearly
}

/// <summary>Where the trial button of a <see cref="PricingCalculator"/> stands.</summary>
public enum PricingCalculatorTrialState
{
    /// <summary>The button offers the trial of the selected plan.</summary>
    Idle,

    /// <summary>The trial request is running; the button shows a spinner and ignores clicks.</summary>
    Starting,

    /// <summary>The trial has started; the button says so for <see cref="PricingCalculator.TrialSuccessDuration"/>.</summary>
    Started
}

/// <summary>What a <see cref="PricingCalculatorFeature"/> line shows.</summary>
public enum PricingCalculatorFeatureKind
{
    /// <summary>Its <see cref="PricingCalculatorFeature.Text"/> as written.</summary>
    Text,

    /// <summary>
    /// An amount that grows with the seats: <see cref="PricingCalculatorFeature.PerSeatAmount"/> × seats, put into the
    /// <c>{0}</c> of <see cref="PricingCalculatorFeature.Text"/> ("{0} GB file storage").
    /// </summary>
    PerSeat,

    /// <summary>
    /// The plan's volume discount, shown only while one applies; the discount (as money) goes into the <c>{0}</c> of
    /// <see cref="PricingCalculatorFeature.Text"/>, or into <see cref="PricingCalculatorLabels.VolumeDiscount"/> when the text is empty.
    /// </summary>
    VolumeDiscount
}

/// <summary>A price cut per seat that applies from a number of seats on (the largest tier reached wins).</summary>
public sealed class PricingCalculatorVolumeTier
{
    /// <summary>Creates an empty tier (set the properties).</summary>
    public PricingCalculatorVolumeTier()
    {
    }

    /// <summary>Creates a tier: from <paramref name="minSeats"/> seats on, each seat costs <paramref name="seatDiscount"/> less.</summary>
    public PricingCalculatorVolumeTier(int minSeats, decimal seatDiscount)
    {
        MinSeats = minSeats;
        SeatDiscount = seatDiscount;
    }

    /// <summary>The smallest seat count the tier applies to.</summary>
    public int MinSeats { get; set; }

    /// <summary>How much less each seat costs per month, in both billing periods.</summary>
    public decimal SeatDiscount { get; set; }
}

/// <summary>One line of a plan's feature list; it can depend on the seats and the billing period.</summary>
public sealed class PricingCalculatorFeature
{
    /// <summary>Creates an empty line (set the properties).</summary>
    public PricingCalculatorFeature()
    {
    }

    /// <summary>Creates a plain line.</summary>
    public PricingCalculatorFeature(string text)
    {
        Text = text;
    }

    /// <summary>The text; <c>{0}</c> takes the amount of <see cref="PricingCalculatorFeatureKind.PerSeat"/> and volume lines.</summary>
    public string? Text { get; set; }

    /// <summary>What the line shows (plain text by default).</summary>
    public PricingCalculatorFeatureKind Kind { get; set; }

    /// <summary>For <see cref="PricingCalculatorFeatureKind.PerSeat"/> lines: the amount each seat adds.</summary>
    public decimal PerSeatAmount { get; set; }

    /// <summary>The line appears from this many seats on (0: always).</summary>
    public int MinSeats { get; set; }

    /// <summary>True: the line appears only with yearly billing.</summary>
    public bool IsYearlyOnly { get; set; }

    /// <summary>
    /// Optional: computes the text from the seats and the billing period; null hides the line. Wins over the other settings.
    /// </summary>
    public Func<int, PricingCalculatorBilling, string?>? TextFactory { get; set; }
}

/// <summary>
/// One plan of a <see cref="PricingCalculator"/>: its name and line, monthly and yearly seat prices, an optional seat cap,
/// volume tiers, the seat count from which it is recommended, and its feature lines. Changes show at once.
/// </summary>
public sealed class PricingCalculatorPlan : AvaloniaObject
{
    /// <summary>A stable key (picked plan, shared badge); defaults to the name when empty.</summary>
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<PricingCalculatorPlan, string>(nameof(Id), string.Empty);

    /// <summary>The plan's name: the card title and the totals' first line.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<PricingCalculatorPlan, string>(nameof(Name), string.Empty);

    /// <summary>One line under the name.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<PricingCalculatorPlan, string?>(nameof(Description));

    /// <summary>Price of one seat for one month with monthly billing.</summary>
    public static readonly StyledProperty<decimal> MonthlySeatPriceProperty =
        AvaloniaProperty.Register<PricingCalculatorPlan, decimal>(nameof(MonthlySeatPrice));

    /// <summary>
    /// Price of one seat for one month with yearly billing; null: the monthly price less
    /// <see cref="PricingCalculator.YearlyDiscount"/>.
    /// </summary>
    public static readonly StyledProperty<decimal?> YearlySeatPriceProperty =
        AvaloniaProperty.Register<PricingCalculatorPlan, decimal?>(nameof(YearlySeatPrice));

    /// <summary>The most seats the plan covers; above it the card offers to go back to this many seats. Null: no cap.</summary>
    public static readonly StyledProperty<int?> MaxSeatsProperty =
        AvaloniaProperty.Register<PricingCalculatorPlan, int?>(nameof(MaxSeats));

    /// <summary>
    /// The plan is recommended from this many seats on, until the next plan's threshold (the plan with the largest
    /// threshold reached wins; the first plan when none is reached).
    /// </summary>
    public static readonly StyledProperty<int> RecommendFromSeatsProperty =
        AvaloniaProperty.Register<PricingCalculatorPlan, int>(nameof(RecommendFromSeats));

    private readonly AvaloniaList<PricingCalculatorVolumeTier> _volumeTiers = new();
    private readonly AvaloniaList<PricingCalculatorFeature> _features = new();

    /// <summary>Creates an empty plan.</summary>
    public PricingCalculatorPlan()
    {
        _volumeTiers.CollectionChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        _features.CollectionChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc cref="IdProperty"/>
    public string Id
    {
        get => GetValue(IdProperty);
        set => SetValue(IdProperty, value);
    }

    /// <inheritdoc cref="NameProperty"/>
    public string Name
    {
        get => GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="MonthlySeatPriceProperty"/>
    public decimal MonthlySeatPrice
    {
        get => GetValue(MonthlySeatPriceProperty);
        set => SetValue(MonthlySeatPriceProperty, value);
    }

    /// <inheritdoc cref="YearlySeatPriceProperty"/>
    public decimal? YearlySeatPrice
    {
        get => GetValue(YearlySeatPriceProperty);
        set => SetValue(YearlySeatPriceProperty, value);
    }

    /// <inheritdoc cref="MaxSeatsProperty"/>
    public int? MaxSeats
    {
        get => GetValue(MaxSeatsProperty);
        set => SetValue(MaxSeatsProperty, value);
    }

    /// <inheritdoc cref="RecommendFromSeatsProperty"/>
    public int RecommendFromSeats
    {
        get => GetValue(RecommendFromSeatsProperty);
        set => SetValue(RecommendFromSeatsProperty, value);
    }

    /// <summary>Price cuts per seat from a number of seats on.</summary>
    public IList<PricingCalculatorVolumeTier> VolumeTiers => _volumeTiers;

    /// <summary>The feature lines, in order. Lines written inline in XAML land here.</summary>
    [Content]
    public IList<PricingCalculatorFeature> Features => _features;

    /// <summary>The key used for this plan: <see cref="Id"/>, or the name when the id is empty.</summary>
    internal string Key => string.IsNullOrEmpty(Id) ? Name : Id;

    /// <summary>Raised when a property, a tier or a feature line changes.</summary>
    internal event EventHandler? Changed;

    /// <summary>Tells the calculator to read the plan again (after changing a tier or a feature line in place).</summary>
    public void Refresh() => Changed?.Invoke(this, EventArgs.Empty);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>What a trial request carries: the plan, the seats, the billing period and the total shown.</summary>
/// <param name="Plan">The selected plan.</param>
/// <param name="Seats">The seat count.</param>
/// <param name="Billing">The billing period.</param>
/// <param name="Total">The total for one billing period (a year with yearly billing).</param>
public sealed record PricingCalculatorTrialRequest(PricingCalculatorPlan Plan, int Seats, PricingCalculatorBilling Billing, decimal Total);

/// <summary>Arguments of <see cref="PricingCalculator.PlanPicked"/>.</summary>
public sealed class PricingCalculatorPlanPickedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PricingCalculatorPlanPickedEventArgs(RoutedEvent routedEvent, PricingCalculatorPlan plan, int seats) : base(routedEvent)
    {
        Plan = plan;
        Seats = seats;
    }

    /// <summary>The plan that was picked.</summary>
    public PricingCalculatorPlan Plan { get; }

    /// <summary>The seat count after the pick (a capped plan's button also moves the seats).</summary>
    public int Seats { get; }
}

/// <summary>Arguments of <see cref="PricingCalculator.SeatsChanged"/>.</summary>
public sealed class PricingCalculatorSeatsChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PricingCalculatorSeatsChangedEventArgs(RoutedEvent routedEvent, int oldSeats, int newSeats) : base(routedEvent)
    {
        OldSeats = oldSeats;
        NewSeats = newSeats;
    }

    /// <summary>The seat count before the change.</summary>
    public int OldSeats { get; }

    /// <summary>The seat count after the change.</summary>
    public int NewSeats { get; }
}

/// <summary>Arguments of <see cref="PricingCalculator.BillingChanged"/>.</summary>
public sealed class PricingCalculatorBillingChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PricingCalculatorBillingChangedEventArgs(RoutedEvent routedEvent, PricingCalculatorBilling oldBilling, PricingCalculatorBilling newBilling)
        : base(routedEvent)
    {
        OldBilling = oldBilling;
        NewBilling = newBilling;
    }

    /// <summary>The billing period before the change.</summary>
    public PricingCalculatorBilling OldBilling { get; }

    /// <summary>The billing period after the change.</summary>
    public PricingCalculatorBilling NewBilling { get; }
}

/// <summary>Arguments of <see cref="PricingCalculator.TrialStarted"/>.</summary>
public sealed class PricingCalculatorTrialStartedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PricingCalculatorTrialStartedEventArgs(RoutedEvent routedEvent, PricingCalculatorTrialRequest request) : base(routedEvent)
    {
        Request = request;
    }

    /// <summary>The plan, seats, billing period and total the trial started with.</summary>
    public PricingCalculatorTrialRequest Request { get; }
}
