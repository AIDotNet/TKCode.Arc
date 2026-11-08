namespace TKCode.Arc;

/// <summary>The amounts of a checkout summary, all in the currency's minor unit precision.</summary>
/// <param name="Subtotal">The item price.</param>
/// <param name="Adjusted">Subtotal plus every automatic line (never below zero).</param>
/// <param name="Coupon">What the promo code takes off (positive, at most <paramref name="Adjusted"/>).</param>
/// <param name="TaxBase">Adjusted minus the code, never below zero.</param>
/// <param name="Tax">Tax on the base, rounded before it is added; zero when there is no rate.</param>
/// <param name="Total">Base plus tax.</param>
internal readonly record struct CheckoutSummaryTotals(
    decimal Subtotal, decimal Adjusted, decimal Coupon, decimal TaxBase, decimal Tax, decimal Total);

/// <summary>
/// The summary's arithmetic (Arc CheckoutSummary（设计参考）, notes 2b): the code applies to the adjusted subtotal, the
/// base never goes below zero, the tax is rounded to the minor unit first and then added.
/// </summary>
internal static class CheckoutSummaryMath
{
    /// <summary>Computes every amount of the summary.</summary>
    /// <param name="price">Item price (subtotal).</param>
    /// <param name="adjustments">Signed automatic lines.</param>
    /// <param name="coupon">The applied code, or null.</param>
    /// <param name="taxRate">Tax rate as a fraction, or null for no tax amount.</param>
    /// <param name="minorDigits">Precision of the currency (2, or 0 for JPY).</param>
    public static CheckoutSummaryTotals Compute(
        decimal price, ReadOnlySpan<decimal> adjustments, CheckoutSummaryCouponResult? coupon, decimal? taxRate, int minorDigits = 2)
    {
        var adjusted = price;
        foreach (var amount in adjustments)
        {
            adjusted += amount;
        }
        adjusted = Math.Max(0m, adjusted);
        var off = 0m;
        if (coupon is { IsValid: true })
        {
            off = coupon.Percent is { } percent ? Round(adjusted * percent, minorDigits) : coupon.Amount;
            off = Math.Min(Math.Max(0m, off), adjusted);
        }
        var taxBase = Math.Max(0m, adjusted - off);
        var tax = taxRate is { } rate ? Round(taxBase * rate, minorDigits) : 0m;
        return new CheckoutSummaryTotals(price, adjusted, off, taxBase, tax, taxBase + tax);
    }

    /// <summary>Rounds half away from zero, like the reference's <c>Math.round</c> on positive amounts.</summary>
    public static decimal Round(decimal value, int digits) => Math.Round(value, digits, MidpointRounding.AwayFromZero);
}
