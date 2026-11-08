using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The checkout's price formulas (Arc LiveCheckout, design reference; every formula checked against at least two observed
/// values): seat prices, summary lines, coupon discount, tax by country and the total. Amounts round to cents half away
/// from zero, like the reference.
/// </summary>
internal static class LiveCheckoutMath
{
    /// <summary>Cents, half away from zero.</summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>One seat for one month: the monthly price, or the yearly price after the discount (12 → 9.60 at 20%).</summary>
    public static decimal SeatPrice(decimal monthly, LiveCheckoutBilling billing, decimal yearlyDiscount)
        => billing == LiveCheckoutBilling.Yearly ? Round2(monthly * (1 - Clamp01(yearlyDiscount))) : monthly;

    /// <summary>The plan row: seats × seat price × 12 (yearly) or × 1 (monthly).</summary>
    public static decimal PlanAmount(decimal monthly, int seats, LiveCheckoutBilling billing, decimal yearlyDiscount)
        => Round2(seats * SeatPrice(monthly, billing, yearlyDiscount) * Months(billing));

    /// <summary>An add-on row: price × 12 × (1 − discount) yearly, the price monthly (49 → 470.40 at 20%).</summary>
    public static decimal AddonAmount(decimal monthly, LiveCheckoutBilling billing, decimal yearlyDiscount)
        => billing == LiveCheckoutBilling.Yearly ? Round2(monthly * 12 * (1 - Clamp01(yearlyDiscount))) : monthly;

    /// <summary>Months one charge covers.</summary>
    public static int Months(LiveCheckoutBilling billing) => billing == LiveCheckoutBilling.Yearly ? 12 : 1;

    /// <summary>Subtotal, coupon, tax and total of <paramref name="lines"/>.</summary>
    public static LiveCheckoutTotals Totals(IEnumerable<decimal> lines, decimal couponPercent, decimal taxRate)
    {
        var subtotal = 0m;
        foreach (var line in lines)
        {
            subtotal += line;
        }
        var discount = Round2(subtotal * Math.Clamp(couponPercent, 0, 100) / 100m);
        var tax = Round2((subtotal - discount) * Math.Max(0, taxRate));
        return new LiveCheckoutTotals(subtotal, discount, tax, subtotal - discount + tax);
    }

    /// <summary>
    /// The summary rows for a choice of plan, seats, billing and add-ons. Add-on rows follow the plan row in the order of
    /// <paramref name="addons"/> (list order, not the order they were ticked).
    /// </summary>
    public static List<LiveCheckoutLine> Lines(LiveCheckoutPlan? plan, int seats, LiveCheckoutBilling billing, decimal yearlyDiscount,
        IEnumerable<LiveCheckoutAddon> addons, ICollection<string> selected, LiveCheckoutLabels labels, string currency, CultureInfo culture)
    {
        var lines = new List<LiveCheckoutLine>();
        if (plan is not null)
        {
            var seat = SeatPrice(plan.MonthlySeatPrice, billing, yearlyDiscount);
            var detail = labels.PlanDetail(seats, SettingsFormat.Money(seat, currency, culture), Months(billing));
            lines.Add(new LiveCheckoutLine(LiveCheckoutLineKind.Plan, plan.Id, labels.PlanLine(plan.Name), detail,
                PlanAmount(plan.MonthlySeatPrice, seats, billing, yearlyDiscount)));
        }
        foreach (var addon in addons)
        {
            if (!selected.Contains(addon.Id))
            {
                continue;
            }
            lines.Add(new LiveCheckoutLine(LiveCheckoutLineKind.Addon, addon.Id, addon.Name,
                billing == LiveCheckoutBilling.Yearly ? labels.BilledYearly : labels.BilledMonthly,
                AddonAmount(addon.MonthlyPrice, billing, yearlyDiscount)));
        }
        return lines;
    }

    /// <summary>A rate as a percent without trailing zeros: 0.19 → "19%", 0.081 → "8.1%".</summary>
    public static string Percent(decimal rate, CultureInfo culture)
        => (rate * 100m).ToString("0.###", culture) + "%";

    /// <summary>The tax row's name: "VAT 21%", or the fallback name without a rate when nothing is charged.</summary>
    public static string TaxName(LiveCheckoutCountry? country, LiveCheckoutLabels labels, CultureInfo culture)
    {
        if (country is null || country.TaxRate <= 0)
        {
            return labels.TaxFallback;
        }
        var name = string.IsNullOrWhiteSpace(country.TaxLabel) ? labels.TaxFallback : country.TaxLabel!;
        return labels.TaxLine(name, Percent(country.TaxRate, culture));
    }

    /// <summary>The yearly discount as shown on the billing switch: 0.2 → "−20%"; null when there is none.</summary>
    public static string? DiscountBadge(decimal yearlyDiscount, CultureInfo culture)
        => yearlyDiscount <= 0 ? null : "−" + Percent(Clamp01(yearlyDiscount), culture);

    private static decimal Clamp01(decimal value) => Math.Clamp(value, 0, 1);
}
