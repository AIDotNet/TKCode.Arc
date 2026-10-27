using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Number formatting shared by the Finance blocks, fixed to en-US digits so every machine renders the same text: money
/// shortened to k and M, signed amounts and shares with a true minus sign, prices and volumes.
/// </summary>
internal static class FinanceFormat
{
    /// <summary>The minus sign used for negative amounts and shares (U+2212, as wide as the plus sign).</summary>
    public const string Minus = "−";

    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>"$4.01M", "$993.6k", "$820": millions with two decimals, thousands with one, smaller amounts whole.</summary>
    public static string Compact(double amount, string currency)
    {
        var value = Math.Abs(amount);
        var sign = amount < 0 && Math.Round(value) > 0 ? Minus : string.Empty;
        // Rounding can carry a value into the next unit (999.96k would read "1000.0k").
        if (Math.Round(value / 100) * 100 >= 999_950)
        {
            return sign + currency + (value / 1_000_000).ToString("0.00", Culture) + "M";
        }
        if (Math.Round(value) >= 1000)
        {
            return sign + currency + (value / 1000).ToString("0.0", Culture) + "k";
        }
        return sign + currency + Math.Round(value).ToString("0", Culture);
    }

    /// <summary>"+$48.9k" / "−$3.9k": <see cref="Compact"/> with the sign always written.</summary>
    public static string SignedCompact(double amount, string currency)
        => (amount < 0 ? Minus : "+") + Compact(Math.Abs(amount), currency);

    /// <summary>"+1.2%" / "−0.3%": a share (0.012) as a signed percentage with <paramref name="decimals"/> digits.</summary>
    public static string SignedPercent(double share, int decimals = 1)
    {
        var text = Math.Abs(share * 100).ToString("F" + decimals, Culture);
        return (share < 0 && text.Any(c => c is > '0' and <= '9') ? Minus : "+") + text + "%";
    }

    /// <summary>"1.2%": a share as an unsigned percentage.</summary>
    public static string Percent(double share, int decimals = 1) => Math.Abs(share * 100).ToString("F" + decimals, Culture) + "%";

    /// <summary>"$1,845.30": money with cents and thousands separators.</summary>
    public static string Money(double amount, string currency)
        => (amount < 0 ? Minus : string.Empty) + currency + Math.Abs(amount).ToString("N2", Culture);

    /// <summary>"184.53": a price with two decimals and no separators below 10,000.</summary>
    public static string Price(double price) => price.ToString(Math.Abs(price) >= 10_000 ? "N2" : "F2", Culture);

    /// <summary>"+1.56" / "−0.42": a signed price change.</summary>
    public static string SignedPrice(double change) => (change < 0 ? Minus : "+") + Math.Abs(change).ToString("F2", Culture);

    /// <summary>"139M", "12.4M", "850K", "1.2B": a share volume.</summary>
    public static string Volume(double volume)
    {
        var value = Math.Abs(volume);
        if (value >= 1e9)
        {
            return (value / 1e9).ToString("0.0", Culture) + "B";
        }
        if (value >= 1e6)
        {
            return (value / 1e6).ToString(value >= 1e8 ? "0" : "0.0", Culture) + "M";
        }
        if (value >= 1e3)
        {
            return (value / 1e3).ToString("0", Culture) + "K";
        }
        return value.ToString("0", Culture);
    }

    /// <summary>"10,000": a whole number with thousands separators.</summary>
    public static string Count(double value) => value.ToString("N0", Culture);
}
