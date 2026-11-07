using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Number and money formats of <see cref="ChargeSession"/>: the culture from a locale name, whole numbers, one-decimal
/// energy and the cost in an ISO 4217 currency. The currency symbol is the culture's own when the code is the culture's
/// currency (de-DE + EUR → "9,57 €", en-CA + CAD → "$9.57"), else a short international symbol from a small table
/// (en-US + EUR → "€9.57", de-DE + USD → "9,57 $"), else the code itself; JPY and KRW have no decimals. A table rather
/// than a scan of every culture keeps it trimming-safe and correct with invariant globalization.
/// </summary>
internal static class ChargeSessionFormat
{
    private static readonly Dictionary<string, (string Symbol, int Digits)> s_currencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = ("$", 2), ["EUR"] = ("€", 2), ["GBP"] = ("£", 2), ["JPY"] = ("¥", 0), ["CNY"] = ("CN¥", 2),
        ["KRW"] = ("₩", 0), ["INR"] = ("₹", 2), ["CAD"] = ("CA$", 2), ["AUD"] = ("A$", 2), ["NZD"] = ("NZ$", 2),
        ["HKD"] = ("HK$", 2), ["TWD"] = ("NT$", 2), ["SGD"] = ("SGD", 2), ["MXN"] = ("MX$", 2), ["BRL"] = ("R$", 2),
        ["CHF"] = ("CHF", 2), ["SEK"] = ("SEK", 2), ["NOK"] = ("NOK", 2), ["DKK"] = ("DKK", 2), ["PLN"] = ("PLN", 2),
        ["ILS"] = ("₪", 2), ["VND"] = ("₫", 0), ["PHP"] = ("₱", 2), ["THB"] = ("THB", 2), ["ZAR"] = ("ZAR", 2),
    };

    private static readonly Dictionary<(string Culture, string Currency), NumberFormatInfo> s_formats = new();

    /// <summary>The culture for <paramref name="locale"/>; the invariant culture when the name is empty or unknown.</summary>
    public static CultureInfo Culture(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return CultureInfo.InvariantCulture;
        }
        try
        {
            return CultureInfo.GetCultureInfo(locale.Trim(), predefinedOnly: true);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
        catch (ArgumentException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    /// <summary>A whole number ("150").</summary>
    public static string Whole(double value, CultureInfo culture) =>
        Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", culture);

    /// <summary>The header's percentage number: the charge rounded down ("63" for 63.607).</summary>
    public static string ChargeNumber(double charge, CultureInfo culture) =>
        Math.Floor(charge + 1e-9).ToString("0", culture);

    /// <summary>One decimal ("22.8").</summary>
    public static string OneDecimal(double value, CultureInfo culture) =>
        Math.Round(value, 1, MidpointRounding.AwayFromZero).ToString("0.0", culture);

    /// <summary>The cost rounded to the currency's digits.</summary>
    public static decimal Cost(double energy, decimal rate, string? currency)
    {
        var amount = (decimal)Math.Max(0, energy) * rate;
        return Math.Round(amount, Digits(currency), MidpointRounding.AwayFromZero);
    }

    /// <summary><paramref name="amount"/> in <paramref name="currency"/> formatted for <paramref name="culture"/>.</summary>
    public static string Money(decimal amount, string? currency, CultureInfo culture) =>
        amount.ToString("C", MoneyFormat(currency, culture));

    /// <summary>Decimals of <paramref name="currency"/> (2 unless the table says otherwise).</summary>
    public static int Digits(string? currency) =>
        currency is not null && s_currencies.TryGetValue(currency, out var entry) ? entry.Digits : 2;

    private static NumberFormatInfo MoneyFormat(string? currency, CultureInfo culture)
    {
        var code = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        var key = (culture.Name, code);
        lock (s_formats)
        {
            if (s_formats.TryGetValue(key, out var cached))
            {
                return cached;
            }
            var format = (NumberFormatInfo)culture.NumberFormat.Clone();
            format.CurrencySymbol = Symbol(code, culture);
            format.CurrencyDecimalDigits = Digits(code);
            format = NumberFormatInfo.ReadOnly(format);
            s_formats[key] = format;
            return format;
        }
    }

    private static string Symbol(string code, CultureInfo culture)
    {
        if (!string.IsNullOrEmpty(culture.Name) && RegionCurrency(culture) == code)
        {
            var own = culture.NumberFormat.CurrencySymbol;
            if (!string.IsNullOrEmpty(own) && own != "¤")
            {
                return own;
            }
        }
        return s_currencies.TryGetValue(code, out var entry) ? entry.Symbol : code;
    }

    private static string? RegionCurrency(CultureInfo culture)
    {
        try
        {
            return culture.IsNeutralCulture ? null : new RegionInfo(culture.Name).ISOCurrencySymbol;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
