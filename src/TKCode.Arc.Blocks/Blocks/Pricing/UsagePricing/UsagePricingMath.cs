using System.Globalization;

namespace TKCode.Arc;

/// <summary>A plan as the formulas see it (no Avalonia object, so the math is testable on its own).</summary>
internal readonly record struct UsagePricingTerms(decimal SeatPrice, decimal YearlySeatDiscount, double IncludedUsage,
    decimal OveragePrice, int? MaxSeats, double? MaxUsage)
{
    public static UsagePricingTerms Of(UsagePricingPlan plan) => new(plan.SeatPrice, plan.YearlySeatDiscount, plan.IncludedUsage,
        plan.OveragePrice, plan.MaxSeats, plan.MaxUsage);
}

/// <summary>Everything the block shows for one selection.</summary>
internal readonly record struct UsagePricingQuote(
    int PlanIndex,
    UsagePricingReason Reason,
    int Seats,
    double Usage,
    decimal SeatCost,
    double Overage,
    decimal UsageCost,
    decimal YearlyOff,
    decimal Total,
    decimal YearlySaving,
    decimal SeatsPerYear);

/// <summary>A line of the feature list for the recommended plan.</summary>
internal readonly record struct UsagePricingFeatureLine(string Key, string Label, string? Value, bool IsNew);

/// <summary>
/// The formulas of <see cref="UsagePricing"/> (notes 2b.3–2b.5): the recommendation, its reason, the costs, the yearly
/// discount and saving, the slider marks, the usage and money formats and the feature list order. Pure functions.
/// </summary>
internal static class UsagePricingMath
{
    /// <summary>U+2212, the minus sign of negative amounts ("−$72").</summary>
    public const string Minus = "−";

    /// <summary>
    /// The first plan whose limits cover both values (a null limit covers everything); the last plan when none does;
    /// −1 without plans. Price plays no part: the cheapest plan is not looked for.
    /// </summary>
    public static int Recommend(IReadOnlyList<UsagePricingTerms> plans, int seats, double usage)
    {
        for (var i = 0; i < plans.Count; i++)
        {
            var p = plans[i];
            if ((p.MaxSeats is null || seats <= p.MaxSeats) && (p.MaxUsage is null || usage <= p.MaxUsage))
            {
                return i;
            }
        }
        return plans.Count - 1;
    }

    /// <summary>Why plan <paramref name="index"/> is recommended, comparing the values with the previous plan's limits.</summary>
    public static UsagePricingReason Reason(IReadOnlyList<UsagePricingTerms> plans, int index, int seats, double usage)
    {
        if (index <= 0 || index >= plans.Count)
        {
            return UsagePricingReason.WithinLimits;
        }
        var previous = plans[index - 1];
        var seatsAbove = previous.MaxSeats is { } maxSeats && seats > maxSeats;
        var usageAbove = previous.MaxUsage is { } maxUsage && usage > maxUsage;
        return (seatsAbove, usageAbove) switch
        {
            (true, true) => UsagePricingReason.BothAbove,
            (true, false) => UsagePricingReason.SeatsAbove,
            (false, true) => UsagePricingReason.UsageAbove,
            // Not above either limit yet not covered: only possible with a gap in the limits; call it both.
            _ => UsagePricingReason.BothAbove
        };
    }

    /// <summary>Seats × seat price.</summary>
    public static decimal SeatCost(int seats, decimal seatPrice) => seats * seatPrice;

    /// <summary>Usage beyond what the plan includes (never negative).</summary>
    public static double Overage(double usage, double included) => Math.Max(0, usage - included);

    /// <summary>Overage ÷ unit × price per unit.</summary>
    public static decimal UsageCost(double overage, decimal pricePerUnit, double unit)
        => unit > 0 && overage > 0 ? (decimal)overage / (decimal)unit * pricePerUnit : 0m;

    /// <summary>Seats × yearly discount per seat (the monthly amount yearly billing takes off).</summary>
    public static decimal YearlyOff(int seats, decimal discount) => seats * discount;

    /// <summary>Seat cost + usage cost − the yearly discount when billed yearly.</summary>
    public static decimal Total(decimal seatCost, decimal usageCost, decimal yearlyOff, bool yearly)
        => seatCost + usageCost - (yearly ? yearlyOff : 0m);

    /// <summary>Seats × discount × 12: what a year of yearly billing saves (seats only; usage plays no part).</summary>
    public static decimal YearlySaving(int seats, decimal discount) => seats * discount * 12;

    /// <summary>Seats × (price − discount) × 12: the seats' yearly charge under yearly billing.</summary>
    public static decimal SeatsPerYear(int seats, decimal seatPrice, decimal discount) => seats * (seatPrice - discount) * 12;

    /// <summary>The whole estimate for one selection.</summary>
    public static UsagePricingQuote Quote(IReadOnlyList<UsagePricingTerms> plans, int seats, double usage, bool yearly, double overageUnit)
    {
        var index = Recommend(plans, seats, usage);
        if (index < 0)
        {
            return new UsagePricingQuote(-1, UsagePricingReason.WithinLimits, seats, usage, 0, 0, 0, 0, 0, 0, 0);
        }
        var plan = plans[index];
        var seatCost = SeatCost(seats, plan.SeatPrice);
        var overage = Overage(usage, plan.IncludedUsage);
        var usageCost = UsageCost(overage, plan.OveragePrice, overageUnit);
        var off = YearlyOff(seats, plan.YearlySeatDiscount);
        return new UsagePricingQuote(index, Reason(plans, index, seats, usage), seats, usage, seatCost, overage, usageCost, off,
            Total(seatCost, usageCost, off, yearly), YearlySaving(seats, plan.YearlySeatDiscount),
            SeatsPerYear(seats, plan.SeatPrice, plan.YearlySeatDiscount));
    }

    /// <summary>
    /// The stop index where a plan (not the first) starts on a slider: the first stop past the
    /// previous plan's limit; −1 when the previous plan has no limit or no stop passes it.
    /// </summary>
    public static int MarkIndex<T>(IReadOnlyList<T> stops, double? previousLimit, Func<T, double> value)
    {
        if (previousLimit is not { } limit)
        {
            return -1;
        }
        for (var i = 0; i < stops.Count; i++)
        {
            if (value(stops[i]) > limit)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Where a stop sits along the track (0–1): stops are spaced evenly by index, not by value.</summary>
    public static double StopFraction(int index, int count) => count <= 1 ? 0 : (double)index / (count - 1);

    /// <summary>The stop nearest to <paramref name="target"/> (for setting a value that is not a stop).</summary>
    public static int NearestStop<T>(IReadOnlyList<T> stops, double target, Func<T, double> value)
    {
        var best = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < stops.Count; i++)
        {
            var d = Math.Abs(value(stops[i]) - target);
            if (d < bestDistance)
            {
                best = i;
                bestDistance = d;
            }
        }
        return best;
    }

    /// <summary>
    /// Usage in thousands, millions or billions with at most two decimals and no trailing zeros: 100k, 750k, 1.5M, 12.5M,
    /// 300M, 1.25B; below a thousand the plain number.
    /// </summary>
    public static string FormatUsage(double value, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.InvariantCulture;
        var abs = Math.Abs(value);
        var (scaled, unit) = abs switch
        {
            >= 1e9 => (value / 1e9, "B"),
            >= 1e6 => (value / 1e6, "M"),
            >= 1e3 => (value / 1e3, "k"),
            _ => (value, string.Empty)
        };
        return Math.Round(scaled, 2).ToString("0.##", culture) + unit;
    }

    /// <summary>The symbol written for an ISO 4217 code, or null when the code is written after the number.</summary>
    public static string? CurrencySymbol(string? currency) => Normalize(currency) switch
    {
        "USD" => "$",
        "EUR" => "€",
        "GBP" => "£",
        "JPY" => "¥",
        "CNY" => "¥",
        "INR" => "₹",
        "KRW" => "₩",
        "AUD" => "A$",
        "CAD" => "CA$",
        _ => null
    };

    private static string Normalize(string? currency) => string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();

    /// <summary>The text before and after the number of an amount ("$", "" or "", " CHF").</summary>
    public static (string Prefix, string Suffix) MoneyShape(string? currency)
    {
        var symbol = CurrencySymbol(currency);
        return symbol is null ? (string.Empty, " " + Normalize(currency)) : (symbol, string.Empty);
    }

    /// <summary>
    /// A whole amount with group separators and no minor units ("$1,710", "1.710 CHF" in de-CH); negative amounts use
    /// U+2212 ("−$72"). Rates use <see cref="Rate"/>.
    /// </summary>
    public static string Money(decimal amount, string? currency, CultureInfo culture)
    {
        var (prefix, suffix) = MoneyShape(currency);
        var number = Math.Round(Math.Abs(amount), 0, MidpointRounding.AwayFromZero).ToString("N0", culture);
        var sign = amount < 0 && Math.Round(amount, 0, MidpointRounding.AwayFromZero) != 0 ? Minus : string.Empty;
        return sign + prefix + number + suffix;
    }

    /// <summary>A unit price with the decimals it needs (up to two): "$5", "$0.40".</summary>
    public static string Rate(decimal price, string? currency, CultureInfo culture)
    {
        var (prefix, suffix) = MoneyShape(currency);
        var format = decimal.Truncate(price) == price ? "N0" : "N2";
        return prefix + price.ToString(format, culture) + suffix;
    }

    /// <summary>
    /// The feature list of plan <paramref name="index"/>: lines whose key the previous plan lacks come first (new, in the
    /// plan's order), then the inherited lines in the order the previous plan shows them. Every line of the first plan is new.
    /// </summary>
    public static List<UsagePricingFeatureLine> FeatureLines(IReadOnlyList<IReadOnlyList<UsagePricingFeature>> plans, int index)
    {
        var lines = new List<UsagePricingFeatureLine>();
        if (index < 0 || index >= plans.Count)
        {
            return lines;
        }
        var own = plans[index];
        if (index == 0)
        {
            foreach (var f in own)
            {
                lines.Add(new UsagePricingFeatureLine(f.Key, f.Label, f.Value, true));
            }
            return lines;
        }
        var previous = FeatureLines(plans, index - 1);
        var previousOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < previous.Count; i++)
        {
            previousOrder.TryAdd(previous[i].Key, i);
        }
        var inherited = new List<(int Order, UsagePricingFeatureLine Line)>();
        foreach (var f in own)
        {
            if (previousOrder.TryGetValue(f.Key, out var order))
            {
                inherited.Add((order, new UsagePricingFeatureLine(f.Key, f.Label, f.Value, false)));
            }
            else
            {
                lines.Add(new UsagePricingFeatureLine(f.Key, f.Label, f.Value, true));
            }
        }
        inherited.Sort((a, b) => a.Order.CompareTo(b.Order));
        foreach (var (_, line) in inherited)
        {
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>
    /// Splits a sentence built by a label function around the value it was given, so the value can be drawn by a
    /// counter or a morph: ("Save ", " a year") for "Save {0} a year". A label that drops the value gives (text, null).
    /// </summary>
    public static (string Before, string? After) SplitAround(Func<string, string> label)
    {
        const string marker = "\u0001";
        var text = label(marker);
        var at = text.IndexOf(marker, StringComparison.Ordinal);
        return at < 0 ? (text, null) : (text[..at], text[(at + marker.Length)..]);
    }
}
