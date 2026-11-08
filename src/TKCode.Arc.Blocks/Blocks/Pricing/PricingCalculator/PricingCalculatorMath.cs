using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The pricing rules of <see cref="PricingCalculator"/>, as pure functions of the plans, the seats and the billing period
/// (Arc's pricing-calculator rules, design reference, notes section 2b):
/// <list type="bullet">
/// <item>volume discount d(s): the largest tier with <c>MinSeats ≤ s</c>, else 0;</item>
/// <item>seat price per month: monthly <c>M − d</c>, yearly <c>Y − d</c> (Y = the yearly price, or M less the yearly
/// discount; the volume discount is not reduced by the yearly factor);</item>
/// <item>plan total: monthly <c>m·s</c>, yearly <c>y·12·s</c>;</item>
/// <item>yearly savings: <c>(M − Y)·12·s</c> (the volume discount cancels out, so both periods show the same amount);</item>
/// <item>recommended: the plan with the largest <c>RecommendFromSeats ≤ s</c> (a seat count between two thresholds
/// stays on the lower plan), the first plan when none is reached;</item>
/// <item>available: no cap or <c>s ≤ cap</c>; selected = the picked plan while available, else the recommended one.</item>
/// </list>
/// </summary>
internal static class PricingCalculatorMath
{
    /// <summary>Months in a yearly bill.</summary>
    public const int MonthsPerYear = 12;

    /// <summary>The per-seat discount of the largest tier the seats reach (0 when none).</summary>
    public static decimal VolumeDiscount(PricingCalculatorPlan plan, int seats)
    {
        var best = 0m;
        var bestMin = int.MinValue;
        foreach (var tier in plan.VolumeTiers)
        {
            if (seats >= tier.MinSeats && tier.MinSeats >= bestMin)
            {
                bestMin = tier.MinSeats;
                best = tier.SeatDiscount;
            }
        }
        return best;
    }

    /// <summary>The list price of a seat per month before volume discounts.</summary>
    public static decimal ListSeatPrice(PricingCalculatorPlan plan, PricingCalculatorBilling billing, double yearlyDiscount)
    {
        if (billing == PricingCalculatorBilling.Monthly)
        {
            return plan.MonthlySeatPrice;
        }
        if (plan.YearlySeatPrice is { } yearly)
        {
            return yearly;
        }
        var factor = 1m - (decimal)Math.Clamp(yearlyDiscount, 0, 1);
        return Math.Round(plan.MonthlySeatPrice * factor, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>What one seat costs per month for <paramref name="seats"/> seats (never below zero).</summary>
    public static decimal SeatPrice(PricingCalculatorPlan plan, PricingCalculatorBilling billing, int seats, double yearlyDiscount)
        => Math.Max(0m, ListSeatPrice(plan, billing, yearlyDiscount) - VolumeDiscount(plan, seats));

    /// <summary>What one billing period costs: a month with monthly billing, a year with yearly billing.</summary>
    public static decimal PlanTotal(PricingCalculatorPlan plan, PricingCalculatorBilling billing, int seats, double yearlyDiscount)
    {
        var seat = SeatPrice(plan, billing, seats, yearlyDiscount);
        return billing == PricingCalculatorBilling.Monthly ? seat * seats : seat * MonthsPerYear * seats;
    }

    /// <summary>What yearly billing saves over monthly billing in a year (never below zero).</summary>
    public static decimal YearlySavings(PricingCalculatorPlan plan, int seats, double yearlyDiscount)
    {
        var monthly = SeatPrice(plan, PricingCalculatorBilling.Monthly, seats, yearlyDiscount);
        var yearly = SeatPrice(plan, PricingCalculatorBilling.Yearly, seats, yearlyDiscount);
        return Math.Max(0m, (monthly - yearly) * MonthsPerYear * seats);
    }

    /// <summary>True when the plan covers <paramref name="seats"/> seats.</summary>
    public static bool IsAvailable(PricingCalculatorPlan plan, int seats) => plan.MaxSeats is not { } cap || seats <= cap;

    /// <summary>
    /// The recommended plan: the one with the largest <see cref="PricingCalculatorPlan.RecommendFromSeats"/> reached
    /// (the earlier plan wins a tie); the first plan when no threshold is reached; null without plans.
    /// </summary>
    public static PricingCalculatorPlan? Recommend(IReadOnlyList<PricingCalculatorPlan> plans, int seats)
    {
        PricingCalculatorPlan? best = null;
        foreach (var plan in plans)
        {
            if (plan.RecommendFromSeats <= seats && (best is null || plan.RecommendFromSeats > best.RecommendFromSeats))
            {
                best = plan;
            }
        }
        return best ?? (plans.Count > 0 ? plans[0] : null);
    }

    /// <summary>The selected plan: the picked one while it covers the seats, otherwise the recommended one.</summary>
    public static PricingCalculatorPlan? Select(IReadOnlyList<PricingCalculatorPlan> plans, string? pickedKey, int seats,
        PricingCalculatorPlan? recommended)
    {
        if (!string.IsNullOrEmpty(pickedKey))
        {
            foreach (var plan in plans)
            {
                if (plan.Key == pickedKey)
                {
                    return IsAvailable(plan, seats) ? plan : recommended;
                }
            }
        }
        return recommended;
    }

    /// <summary>The index of the stop closest to <paramref name="seats"/> (the lower one on a tie; 0 without stops).</summary>
    public static int NearestStopIndex(IReadOnlyList<int> stops, int seats)
    {
        var best = 0;
        var distance = long.MaxValue;
        for (var i = 0; i < stops.Count; i++)
        {
            var d = Math.Abs((long)stops[i] - seats);
            if (d < distance)
            {
                distance = d;
                best = i;
            }
        }
        return best;
    }

    /// <summary>
    /// Where a plan's mark sits under the slider: the first stop at which it is recommended (the first plan always at 0);
    /// −1 when it is never recommended on these stops.
    /// </summary>
    public static int MarkIndex(IReadOnlyList<PricingCalculatorPlan> plans, IReadOnlyList<int> stops, PricingCalculatorPlan plan)
    {
        for (var i = 0; i < stops.Count; i++)
        {
            if (ReferenceEquals(Recommend(plans, stops[i]), plan))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>The slider index for a page step: Arc's native range moves by 10% of the range, rounded (±2 of 23 stops).</summary>
    public static int PageStep(int stopCount) => Math.Max(1, (int)Math.Round((stopCount - 1) * 0.1, MidpointRounding.AwayFromZero));

    /// <summary>The yearly discount as a whole percent (0.2 ⇒ 20).</summary>
    public static int DiscountPercent(double yearlyDiscount) => (int)Math.Round(Math.Clamp(yearlyDiscount, 0, 1) * 100, MidpointRounding.AwayFromZero);

    /// <summary>One visible feature line: a stable key (its index in the plan) and its text.</summary>
    public readonly record struct FeatureLine(string Key, string Text);

    /// <summary>The feature lines a plan shows for these seats and this billing period, in order.</summary>
    public static List<FeatureLine> Features(PricingCalculatorPlan plan, int seats, PricingCalculatorBilling billing,
        string currency, PricingCalculatorLabels labels)
    {
        var lines = new List<FeatureLine>(plan.Features.Count);
        var culture = labels.Culture;
        for (var i = 0; i < plan.Features.Count; i++)
        {
            var feature = plan.Features[i];
            var text = FeatureText(feature, plan, seats, billing, currency, labels, culture);
            if (!string.IsNullOrEmpty(text))
            {
                lines.Add(new FeatureLine("f" + i.ToString(CultureInfo.InvariantCulture), text));
            }
        }
        return lines;
    }

    private static string? FeatureText(PricingCalculatorFeature feature, PricingCalculatorPlan plan, int seats,
        PricingCalculatorBilling billing, string currency, PricingCalculatorLabels labels, CultureInfo culture)
    {
        if (feature.TextFactory is { } factory)
        {
            return factory(seats, billing);
        }
        if (seats < feature.MinSeats || (feature.IsYearlyOnly && billing != PricingCalculatorBilling.Yearly))
        {
            return null;
        }
        switch (feature.Kind)
        {
            case PricingCalculatorFeatureKind.PerSeat:
                {
                    var amount = (feature.PerSeatAmount * seats).ToString("#,0.##", culture);
                    return Fill(feature.Text, amount);
                }
            case PricingCalculatorFeatureKind.VolumeDiscount:
                {
                    var discount = VolumeDiscount(plan, seats);
                    if (discount <= 0)
                    {
                        return null;
                    }
                    var money = Money(discount, currency, culture);
                    return string.IsNullOrEmpty(feature.Text) ? labels.VolumeDiscount(money) : Fill(feature.Text, money);
                }
            default:
                return feature.Text;
        }
    }

    private static string Fill(string? format, string value)
        => string.IsNullOrEmpty(format) ? value : format.Replace("{0}", value, StringComparison.Ordinal);

    /// <summary>An amount as money: whole amounts without minor units (<c>$4,800</c>), others with two (<c>$12.50</c>).</summary>
    public static string Money(decimal amount, string? currency, CultureInfo culture)
        => SettingsFormat.MoneyShort(amount, currency, culture);

    /// <summary>A seat count as text (grouped digits).</summary>
    public static string Count(int seats, CultureInfo culture) => seats.ToString("N0", culture);

    /// <summary>The text around the number of a money amount, and its decimals, for rolling digits.</summary>
    public readonly record struct MoneyShape(string Prefix, string Suffix, int Decimals);

    /// <summary>Splits <see cref="Money"/>'s text of <paramref name="amount"/> into prefix, number and suffix shape.</summary>
    public static MoneyShape ShapeOf(decimal amount, string? currency, CultureInfo culture)
    {
        var text = Money(amount, currency, culture);
        var first = -1;
        var last = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsDigit(text[i]))
            {
                if (first < 0)
                {
                    first = i;
                }
                last = i;
            }
        }
        if (first < 0)
        {
            return new MoneyShape(string.Empty, string.Empty, 0);
        }
        var number = text.Substring(first, last - first + 1);
        var separator = culture.NumberFormat.NumberDecimalSeparator;
        var at = number.IndexOf(separator, StringComparison.Ordinal);
        var decimals = at < 0 ? 0 : number.Length - at - separator.Length;
        var prefix = text[..first];
        if (amount < 0 && prefix.StartsWith('-'))
        {
            prefix = prefix[1..];
        }
        return new MoneyShape(prefix, text[(last + 1)..], decimals);
    }
}
