using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Money and count text of <see cref="CreditTopUp"/>: amounts in an ISO 4217 currency laid out the way the culture writes
/// money (<c>$48.00</c> in en-US, <c>48,00 €</c> in de-DE), and the pieces the rolling price needs (text before and after
/// the number, minor-unit digits).
/// </summary>
internal static class CreditTopUpMoney
{
    /// <summary>Text before and after the number and the minor-unit digits of a currency in a culture.</summary>
    public readonly record struct Shape(string Prefix, string Suffix, int Decimals);

    /// <summary>The symbol of the common currencies; other codes are written as the code.</summary>
    public static string Symbol(string? currency) => Code(currency) switch
    {
        "USD" => "$",
        "EUR" => "€",
        "GBP" => "£",
        "JPY" => "¥",
        "CNY" => "¥",
        "INR" => "₹",
        "KRW" => "₩",
        var code => code
    };

    /// <summary>Minor-unit digits of a currency (0 for JPY and KRW, else 2).</summary>
    public static int MinorDigits(string? currency) => Code(currency) is "JPY" or "KRW" ? 0 : 2;

    private static string Code(string? currency)
        => string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();

    private static NumberFormatInfo FormatFor(string? currency, CultureInfo culture, int decimals)
    {
        var format = (NumberFormatInfo)culture.NumberFormat.Clone();
        var symbol = Symbol(currency);
        // A code written as text needs a space between it and the number, whatever the culture's pattern.
        if (symbol.Length > 1 && symbol.All(char.IsLetter))
        {
            format.CurrencyPositivePattern = format.CurrencyPositivePattern is 0 or 2 ? 2 : 3;
        }
        format.CurrencySymbol = symbol;
        format.CurrencyDecimalDigits = decimals;
        return format;
    }

    /// <summary><paramref name="amount"/> with its minor units: <c>$48.00</c>, <c>48,00 €</c>, <c>¥1,200</c>.</summary>
    public static string Format(decimal amount, string? currency, CultureInfo culture)
    {
        var decimals = MinorDigits(currency);
        return amount.ToString("C" + decimals, FormatFor(currency, culture, decimals));
    }

    /// <summary>Like <see cref="Format"/>, but whole amounts drop their minor units: <c>$48</c>, <c>$12.50</c>.</summary>
    public static string FormatShort(decimal amount, string? currency, CultureInfo culture)
    {
        var decimals = decimal.Truncate(amount) == amount ? 0 : MinorDigits(currency);
        return amount.ToString("C" + decimals, FormatFor(currency, culture, decimals));
    }

    /// <summary>The pieces around the number of <see cref="Format"/> (for a price whose digits roll).</summary>
    public static Shape ShapeOf(string? currency, CultureInfo culture)
    {
        var decimals = MinorDigits(currency);
        var sample = Format(0m, currency, culture);
        var first = -1;
        var last = -1;
        for (var i = 0; i < sample.Length; i++)
        {
            if (char.IsDigit(sample[i]))
            {
                if (first < 0)
                {
                    first = i;
                }
                last = i;
            }
        }
        return first < 0
            ? new Shape(string.Empty, string.Empty, decimals)
            : new Shape(sample[..first], sample[(last + 1)..], decimals);
    }

    /// <summary>A whole count with the culture's group separators: <c>6,600</c>, <c>6.600</c>.</summary>
    public static string Count(long value, CultureInfo culture) => value.ToString("N0", culture);

    /// <summary>A rate with as many decimals as it needs (whole amounts drop the minor units): <c>$8</c>, <c>$0.50</c>.</summary>
    public static string Rate(decimal amount, string? currency, CultureInfo culture) => FormatShort(amount, currency, culture);
}
