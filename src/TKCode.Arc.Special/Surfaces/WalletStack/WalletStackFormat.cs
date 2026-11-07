using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Money and card-number formats of the wallet stack (Arc Wallet stack, design reference: Intl currency style in the
/// stack's locale; "+" before money in and U+2212 before spending; four masked groups and the last four digits).
/// </summary>
internal sealed class WalletStackFormat
{
    /// <summary>The minus sign Arc uses for spending (U+2212), not the hyphen.</summary>
    public const string Minus = "−";

    /// <summary>The masked groups printed before the last four digits.</summary>
    public const string Mask = "•••• •••• ••••";

    // Symbols and minor units for common ISO 4217 codes, used when the culture's own region uses another currency
    // (Intl prints "€1,234.56" for EUR in en-US). Codes not listed fall back to the code itself.
    private static readonly Dictionary<string, (string Symbol, int Digits)> s_currencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = ("$", 2), ["EUR"] = ("€", 2), ["GBP"] = ("£", 2), ["JPY"] = ("¥", 0), ["CNY"] = ("CN¥", 2),
        ["TWD"] = ("NT$", 2), ["HKD"] = ("HK$", 2), ["CAD"] = ("CA$", 2), ["AUD"] = ("A$", 2), ["CHF"] = ("CHF", 2),
        ["KRW"] = ("₩", 0), ["INR"] = ("₹", 2), ["SEK"] = ("SEK", 2), ["NOK"] = ("NOK", 2), ["DKK"] = ("DKK", 2),
        ["BRL"] = ("R$", 2), ["MXN"] = ("MX$", 2), ["SGD"] = ("SGD", 2), ["NZD"] = ("NZ$", 2), ["PLN"] = ("PLN", 2),
    };

    private readonly NumberFormatInfo _format;

    public WalletStackFormat(string? currency, CultureInfo? culture)
    {
        Culture = culture ?? CultureInfo.GetCultureInfo("en-US");
        Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        _format = (NumberFormatInfo)Culture.NumberFormat.Clone();
        var (symbol, digits) = Resolve(Currency, Culture);
        _format.CurrencySymbol = symbol;
        _format.CurrencyDecimalDigits = digits;
        // Signs are written by Signed: format magnitudes with the culture's positive pattern only.
        _format.CurrencyNegativePattern = NegativeFor(_format.CurrencyPositivePattern);
    }

    public CultureInfo Culture { get; }

    public string Currency { get; }

    /// <summary>A balance in the currency style ("$2,914.35"; "2.914,35 €" in de-DE).</summary>
    public string Money(decimal value) => value.ToString("C", _format);

    /// <summary>A transaction amount: "+$42.00" for money in, "−$63.27" for spending.</summary>
    public string Signed(decimal value)
    {
        var magnitude = Math.Abs(value).ToString("C", _format);
        return value < 0 ? Minus + magnitude : "+" + magnitude;
    }

    /// <summary>A whole number in the culture (the header count).</summary>
    public string Integer(int value) => value.ToString(Culture);

    /// <summary>The printed card number: the masked groups, a space and the last four digits.</summary>
    public static string CardNumber(string lastFour) => Mask + " " + lastFour;

    private static (string Symbol, int Digits) Resolve(string currency, CultureInfo culture)
    {
        try
        {
            if (!culture.IsNeutralCulture && culture.Name.Length > 0)
            {
                var region = new RegionInfo(culture.Name);
                if (string.Equals(region.ISOCurrencySymbol, currency, StringComparison.OrdinalIgnoreCase))
                {
                    return (culture.NumberFormat.CurrencySymbol, culture.NumberFormat.CurrencyDecimalDigits);
                }
            }
        }
        catch (ArgumentException)
        {
            // Invariant globalization or an unknown region: use the table below.
        }
        return s_currencies.TryGetValue(currency, out var known) ? known : (currency, 2);
    }

    // The negative pattern that mirrors a positive pattern with a leading minus, so negatives never get parentheses.
    private static int NegativeFor(int positivePattern) => positivePattern switch
    {
        0 => 1, // $n → -$n
        1 => 5, // n$ → -n$
        2 => 9, // $ n → -$ n
        _ => 8, // n $ → -n $
    };
}
