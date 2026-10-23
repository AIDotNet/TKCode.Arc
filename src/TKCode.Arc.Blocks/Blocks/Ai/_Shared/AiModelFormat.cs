using System.Globalization;

namespace TKCode.Arc;

/// <summary>Formats the numbers the model parts print: context sizes and prices per million tokens.</summary>
public static class AiModelFormat
{
    /// <summary>A token count with a K or M suffix and no trailing zeros: 800000 → "800K", 3000000 → "3M", 1250000 → "1.25M", 512 → "512".</summary>
    /// <param name="tokens">The count.</param>
    /// <param name="culture">Number format; defaults to the invariant culture.</param>
    public static string Tokens(long tokens, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.InvariantCulture;
        return tokens switch
        {
            >= 1_000_000_000 => Scaled(tokens, 1_000_000_000d, "B", culture),
            >= 1_000_000 => Scaled(tokens, 1_000_000d, "M", culture),
            >= 1_000 => Scaled(tokens, 1_000d, "K", culture),
            _ => tokens.ToString("0", culture)
        };
    }

    /// <summary>A price with two decimals and a currency symbol in front: 4 → "$4.00".</summary>
    /// <param name="price">The amount.</param>
    /// <param name="symbol">The currency symbol; defaults to "$".</param>
    /// <param name="culture">Number format; defaults to the invariant culture.</param>
    public static string Price(decimal price, string symbol = "$", CultureInfo? culture = null)
        => symbol + price.ToString("0.00", culture ?? CultureInfo.InvariantCulture);

    private static string Scaled(long tokens, double unit, string suffix, CultureInfo culture)
        => (tokens / unit).ToString("0.##", culture) + suffix;
}
