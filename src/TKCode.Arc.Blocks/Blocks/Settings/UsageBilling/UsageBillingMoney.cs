using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Currency shapes for the parts of <see cref="UsageBilling"/> that cannot take a finished string: the rolling counter
/// needs the symbol, the code and the number of decimals apart, and rates need more decimals than a bill. Both are read
/// from <see cref="SettingsFormat.Money"/>, so every Settings block keeps one currency table.
/// </summary>
internal static class UsageBillingMoney
{
    /// <summary>Text before and after the number, and the minor-unit digits (<c>$</c>, empty, 2; empty, <c> CHF</c>, 2; <c>¥</c>, empty, 0).</summary>
    public readonly record struct Shape(string Prefix, string Suffix, int Decimals);

    public static Shape ShapeOf(string? currency, CultureInfo culture)
    {
        var sample = SettingsFormat.Money(0m, currency, culture);
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
        if (first < 0)
        {
            return new Shape(string.Empty, string.Empty, 2);
        }
        var number = sample.Substring(first, last - first + 1);
        var separator = culture.NumberFormat.NumberDecimalSeparator;
        var at = number.IndexOf(separator, StringComparison.Ordinal);
        var decimals = at < 0 ? 0 : number.Length - at - separator.Length;
        return new Shape(sample[..first], sample[(last + 1)..], decimals);
    }

    /// <summary>A unit price with as many decimals as it needs (up to four): <c>$0.40</c>, <c>$0.008</c>.</summary>
    public static string Rate(decimal price, string? currency, CultureInfo culture)
    {
        var shape = ShapeOf(currency, culture);
        var format = shape.Decimals == 0 ? "#,0.####" : "#,0.00##";
        return shape.Prefix + price.ToString(format, culture) + shape.Suffix;
    }
}
