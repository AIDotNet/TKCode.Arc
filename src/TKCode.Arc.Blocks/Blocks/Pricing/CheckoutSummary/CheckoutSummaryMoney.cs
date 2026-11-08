using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Money text for <see cref="CheckoutSummary"/> (a candidate for a shared Pricing helper): an ISO currency code and a
/// culture give the symbol, its side and the separators; a summary shows every amount with the same number of decimals
/// (none when all of them are whole, otherwise the currency's minor digits); negative amounts start with U+2212 before
/// the symbol (<c>−$24.00</c>).
/// </summary>
internal static class CheckoutSummaryMoney
{
    /// <summary>The minus sign used for discounts (U+2212, not a hyphen).</summary>
    public const string Minus = "−";

    /// <summary>How an amount is drawn around its number: text before, text after, and the decimals.</summary>
    public readonly record struct Shape(string Prefix, string Suffix, int Decimals);

    /// <summary>Digits after the decimal point the currency uses (0 for JPY and KRW, otherwise 2).</summary>
    public static int MinorDigits(string? currency) => Code(currency) is "JPY" or "KRW" or "VND" or "CLP" or "ISK" ? 0 : 2;

    /// <summary>
    /// The decimals a whole summary uses: 0 when every amount is whole (or the currency has no minor unit), otherwise
    /// the currency's minor digits.
    /// </summary>
    public static int DecimalsFor(string? currency, ReadOnlySpan<decimal> amounts)
    {
        var minor = MinorDigits(currency);
        if (minor == 0)
        {
            return 0;
        }
        foreach (var amount in amounts)
        {
            if (decimal.Truncate(amount) != amount)
            {
                return minor;
            }
        }
        return 0;
    }

    /// <summary>The symbol for <paramref name="currency"/>: the culture's own when the culture uses that currency, a short table otherwise, the code as a last resort.</summary>
    public static string Symbol(string? currency, CultureInfo culture)
    {
        var code = Code(currency);
        if (CultureUses(culture, code))
        {
            return culture.NumberFormat.CurrencySymbol;
        }
        return code switch
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
            "NZD" => "NZ$",
            "HKD" => "HK$",
            "TWD" => "NT$",
            _ => code
        };
    }

    /// <summary>Prefix, suffix and decimals of an amount, for parts that draw the number apart (the rolling total).</summary>
    public static Shape ShapeOf(string? currency, CultureInfo culture, int decimals)
    {
        var symbol = Symbol(currency, culture);
        // A code (CHF, SEK) always reads better with a space; symbols follow the culture's pattern.
        var spaced = symbol.Length >= 3 && symbol.All(char.IsLetter);
        return culture.NumberFormat.CurrencyPositivePattern switch
        {
            1 => new Shape(string.Empty, spaced ? " " + symbol : symbol, decimals),
            2 => new Shape(symbol + " ", string.Empty, decimals),
            3 => new Shape(string.Empty, " " + symbol, decimals),
            _ => new Shape(spaced ? symbol + " " : symbol, string.Empty, decimals)
        };
    }

    /// <summary>
    /// <paramref name="amount"/> with <paramref name="decimals"/> decimals: <c>$96.00</c>, <c>−$24.00</c>,
    /// <c>12,50 €</c> (de-DE), <c>¥1,200</c>.
    /// </summary>
    public static string Format(decimal amount, string? currency, CultureInfo culture, int decimals)
    {
        var shape = ShapeOf(currency, culture, decimals);
        var rounded = Math.Round(Math.Abs(amount), decimals, MidpointRounding.AwayFromZero);
        var number = rounded.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), culture);
        var sign = amount < 0 && rounded != 0 ? Minus : string.Empty;
        return sign + shape.Prefix + number + shape.Suffix;
    }

    /// <summary>A rate as a percentage with at most one decimal: 0.075 → <c>7.5%</c>, 0.2 → <c>20%</c>.</summary>
    public static string Percent(decimal rate, CultureInfo culture)
        => (rate * 100m).ToString("0.#", culture) + culture.NumberFormat.PercentSymbol;

    private static string Code(string? currency)
        => string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();

    private static bool CultureUses(CultureInfo culture, string code)
    {
        if (culture.IsNeutralCulture || string.IsNullOrEmpty(culture.Name))
        {
            return false;
        }
        try
        {
            return string.Equals(new RegionInfo(culture.Name).ISOCurrencySymbol, code, StringComparison.Ordinal)
                && !string.IsNullOrEmpty(culture.NumberFormat.CurrencySymbol)
                && culture.NumberFormat.CurrencySymbol != "¤";
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
