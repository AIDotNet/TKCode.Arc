using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>How a <see cref="UsagePricing"/> estimate is billed.</summary>
public enum UsagePricingPeriod
{
    /// <summary>Seats are paid month by month, without the yearly discount.</summary>
    Monthly,

    /// <summary>Seats are paid for a year up front; every seat gets the plan's <see cref="UsagePricingPlan.YearlySeatDiscount"/>.</summary>
    Yearly
}

/// <summary>Why <see cref="UsagePricing"/> recommends its plan: the reason line under the plan name.</summary>
public enum UsagePricingReason
{
    /// <summary>The first plan: the seats and the usage fit inside its limits.</summary>
    WithinLimits,

    /// <summary>The seats go past the previous plan's seat limit; the usage does not.</summary>
    SeatsAbove,

    /// <summary>The usage goes past the previous plan's usage limit; the seats do not.</summary>
    UsageAbove,

    /// <summary>Both the seats and the usage go past the previous plan's limits.</summary>
    BothAbove
}

/// <summary>
/// One line of a plan's feature list in <see cref="UsagePricing"/>. Lines of different plans with the same
/// <see cref="Key"/> are the same feature: when the recommendation moves between plans the line stays in place and only
/// its value changes.
/// </summary>
public sealed class UsagePricingFeature
{
    /// <summary>Creates an empty feature (set the properties).</summary>
    public UsagePricingFeature()
    {
    }

    /// <summary>Creates a feature with its key, label and optional value.</summary>
    public UsagePricingFeature(string key, string label, string? value = null)
    {
        Key = key;
        Label = label;
        Value = value;
    }

    /// <summary>Stable identity across plans ("history", "sso").</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>What the feature is ("Event history").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>How much of it the plan gives ("30 days"); null shows no value on the right.</summary>
    public string? Value { get; set; }
}

/// <summary>
/// A plan <see cref="UsagePricing"/> can recommend: its seat price, the usage it bundles, the price of usage beyond that,
/// the yearly seat discount, the limits up to which it is recommended, and its feature list. Changes to these properties
/// update the block.
/// </summary>
public class UsagePricingPlan : AvaloniaObject
{
    /// <summary>Stable identity of the plan (the trial and the feature list follow it). Defaults to <see cref="Name"/>.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<UsagePricingPlan, string?>(nameof(Key));

    /// <summary>The plan's name, shown on the card, the meter, the seats row and the button.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<UsagePricingPlan, string>(nameof(Name), string.Empty);

    /// <summary>Price of one seat for one month.</summary>
    public static readonly StyledProperty<decimal> SeatPriceProperty =
        AvaloniaProperty.Register<UsagePricingPlan, decimal>(nameof(SeatPrice));

    /// <summary>Usage the plan bundles each month, in the units of <see cref="UsagePricing.UsageStops"/>.</summary>
    public static readonly StyledProperty<double> IncludedUsageProperty =
        AvaloniaProperty.Register<UsagePricingPlan, double>(nameof(IncludedUsage));

    /// <summary>Price of each <see cref="UsagePricing.OverageUnit"/> of usage beyond <see cref="IncludedUsage"/>.</summary>
    public static readonly StyledProperty<decimal> OveragePriceProperty =
        AvaloniaProperty.Register<UsagePricingPlan, decimal>(nameof(OveragePrice));

    /// <summary>What yearly billing takes off each seat's monthly price.</summary>
    public static readonly StyledProperty<decimal> YearlySeatDiscountProperty =
        AvaloniaProperty.Register<UsagePricingPlan, decimal>(nameof(YearlySeatDiscount));

    /// <summary>The most seats the plan is recommended for; null means no limit (the last plan).</summary>
    public static readonly StyledProperty<int?> MaxSeatsProperty =
        AvaloniaProperty.Register<UsagePricingPlan, int?>(nameof(MaxSeats));

    /// <summary>The most monthly usage the plan is recommended for; null means no limit (the last plan).</summary>
    public static readonly StyledProperty<double?> MaxUsageProperty =
        AvaloniaProperty.Register<UsagePricingPlan, double?>(nameof(MaxUsage));

    private AvaloniaList<UsagePricingFeature> _features = new();

    /// <summary>Creates an empty plan (set the properties).</summary>
    public UsagePricingPlan()
    {
    }

    /// <summary>Creates a plan with its name and prices.</summary>
    public UsagePricingPlan(string name, decimal seatPrice, decimal yearlySeatDiscount, double includedUsage, decimal overagePrice,
        int? maxSeats, double? maxUsage)
    {
        Name = name;
        SeatPrice = seatPrice;
        YearlySeatDiscount = yearlySeatDiscount;
        IncludedUsage = includedUsage;
        OveragePrice = overagePrice;
        MaxSeats = maxSeats;
        MaxUsage = maxUsage;
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="SeatPriceProperty"/>
    public decimal SeatPrice { get => GetValue(SeatPriceProperty); set => SetValue(SeatPriceProperty, value); }

    /// <inheritdoc cref="IncludedUsageProperty"/>
    public double IncludedUsage { get => GetValue(IncludedUsageProperty); set => SetValue(IncludedUsageProperty, value); }

    /// <inheritdoc cref="OveragePriceProperty"/>
    public decimal OveragePrice { get => GetValue(OveragePriceProperty); set => SetValue(OveragePriceProperty, value); }

    /// <inheritdoc cref="YearlySeatDiscountProperty"/>
    public decimal YearlySeatDiscount { get => GetValue(YearlySeatDiscountProperty); set => SetValue(YearlySeatDiscountProperty, value); }

    /// <inheritdoc cref="MaxSeatsProperty"/>
    public int? MaxSeats { get => GetValue(MaxSeatsProperty); set => SetValue(MaxSeatsProperty, value); }

    /// <inheritdoc cref="MaxUsageProperty"/>
    public double? MaxUsage { get => GetValue(MaxUsageProperty); set => SetValue(MaxUsageProperty, value); }

    /// <summary>
    /// The plan's features in display order. Features new in this plan (keys the previous plan does not have) are listed
    /// first and drawn in the strong color; the rest follow in the previous plan's order. Inline XAML items land here.
    /// </summary>
    [Content]
    public AvaloniaList<UsagePricingFeature> Features
    {
        get => _features;
        set => _features = value ?? new AvaloniaList<UsagePricingFeature>();
    }

    /// <summary>The identity the block uses: <see cref="Key"/>, or <see cref="Name"/> when no key is set.</summary>
    internal string Identity => string.IsNullOrEmpty(Key) ? Name : Key!;

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>Arguments of <see cref="UsagePricing.RecommendationChanged"/>.</summary>
public sealed class UsagePricingRecommendationChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public UsagePricingRecommendationChangedEventArgs(RoutedEvent routedEvent, UsagePricingPlan? oldPlan, UsagePricingPlan? newPlan,
        UsagePricingReason reason) : base(routedEvent)
    {
        OldPlan = oldPlan;
        NewPlan = newPlan;
        Reason = reason;
    }

    /// <summary>The plan recommended before (null the first time).</summary>
    public UsagePricingPlan? OldPlan { get; }

    /// <summary>The plan recommended now.</summary>
    public UsagePricingPlan? NewPlan { get; }

    /// <summary>Why the new plan is recommended.</summary>
    public UsagePricingReason Reason { get; }
}

/// <summary>Arguments of <see cref="UsagePricing.SelectionChanged"/>, <see cref="UsagePricing.TrialStarted"/> and <see cref="UsagePricing.TrialUndone"/>.</summary>
public sealed class UsagePricingSelectionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public UsagePricingSelectionEventArgs(RoutedEvent routedEvent, UsagePricingPlan? plan, int seats, double usage,
        UsagePricingPeriod period, decimal total) : base(routedEvent)
    {
        Plan = plan;
        Seats = seats;
        Usage = usage;
        Period = period;
        Total = total;
    }

    /// <summary>The recommended plan.</summary>
    public UsagePricingPlan? Plan { get; }

    /// <summary>The seats on the seat slider.</summary>
    public int Seats { get; }

    /// <summary>The monthly usage on the usage slider.</summary>
    public double Usage { get; }

    /// <summary>The billing period.</summary>
    public UsagePricingPeriod Period { get; }

    /// <summary>The estimated monthly total.</summary>
    public decimal Total { get; }
}

/// <summary>What <see cref="UsagePricing.TrialHandler"/> is asked to start.</summary>
/// <param name="Plan">The recommended plan.</param>
/// <param name="Seats">The seats on the seat slider.</param>
/// <param name="Usage">The monthly usage on the usage slider.</param>
/// <param name="Period">The billing period.</param>
/// <param name="Total">The estimated monthly total.</param>
public sealed record UsagePricingTrialRequest(UsagePricingPlan Plan, int Seats, double Usage, UsagePricingPeriod Period, decimal Total);
