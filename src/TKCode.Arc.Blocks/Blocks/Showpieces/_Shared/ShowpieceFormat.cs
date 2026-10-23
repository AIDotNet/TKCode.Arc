using System.Globalization;

namespace TKCode.Arc;

/// <summary>A compact amount split for rolling digits: <c>Scaled</c> shown with <c>Decimals</c> and a unit (K, M, B).</summary>
/// <param name="Scaled">The value divided by the unit (6,050,000 → 6.05).</param>
/// <param name="Decimals">Fraction digits to show (2 for M and B, 0 for K and plain).</param>
/// <param name="Unit">"B", "M", "K" or empty.</param>
internal readonly record struct ShowpieceCompactParts(double Scaled, int Decimals, string Unit);

/// <summary>
/// Number formats of the Showpiece panels (Arc formats with en-US; pass a culture to change separators). Compact
/// currency follows Customer galaxy: millions with two decimals ($6.05M, $2.00M), thousands with none ($870K), smaller
/// amounts in whole units ($990); a thousands value that rounds to 1,000K is shown as $1.00M. Billions ($1.20B) follow
/// the millions rule (not observed; an extension).
/// </summary>
internal static class ShowpieceFormat
{
    /// <summary>Arc's formatting culture.</summary>
    public static readonly CultureInfo DefaultCulture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Shown for a missing value (NaN or infinity).</summary>
    public const string Missing = "—";

    /// <summary>The compact split of an amount (sign kept on <see cref="ShowpieceCompactParts.Scaled"/>).</summary>
    public static ShowpieceCompactParts CompactParts(double value)
    {
        var abs = Math.Abs(value);
        if (abs >= 999_995_000)
        {
            return new ShowpieceCompactParts(value / 1_000_000_000, 2, "B");
        }
        if (abs >= 999_500)
        {
            return new ShowpieceCompactParts(value / 1_000_000, 2, "M");
        }
        if (abs >= 999.5)
        {
            return new ShowpieceCompactParts(value / 1_000, 0, "K");
        }
        return new ShowpieceCompactParts(value, 0, string.Empty);
    }

    /// <summary>Compact currency: $6.05M, $870K, $990, −$1.20M.</summary>
    public static string CompactCurrency(double value, string symbol = "$", CultureInfo? culture = null)
    {
        if (!double.IsFinite(value))
        {
            return Missing;
        }
        culture ??= DefaultCulture;
        var parts = CompactParts(value);
        var number = Math.Abs(parts.Scaled).ToString("F" + parts.Decimals, culture);
        var negative = value < 0 && Math.Round(Math.Abs(parts.Scaled), parts.Decimals) > 0;
        return (negative ? culture.NumberFormat.NegativeSign : string.Empty) + symbol + number + parts.Unit;
    }

    /// <summary>Grouped number: 1,687 (or 1,687.5 with one decimal).</summary>
    public static string Grouped(double value, int decimals = 0, CultureInfo? culture = null)
        => double.IsFinite(value) ? value.ToString("N" + Math.Max(0, decimals), culture ?? DefaultCulture) : Missing;

    /// <summary>Grouped currency in whole units by default: $2,512,340.</summary>
    public static string Currency(double value, int decimals = 0, string symbol = "$", CultureInfo? culture = null)
    {
        if (!double.IsFinite(value))
        {
            return Missing;
        }
        culture ??= DefaultCulture;
        var number = Math.Abs(value).ToString("N" + Math.Max(0, decimals), culture);
        var negative = value < 0 && Math.Round(Math.Abs(value), Math.Max(0, decimals)) > 0;
        return (negative ? culture.NumberFormat.NegativeSign : string.Empty) + symbol + number;
    }

    /// <summary>
    /// A fraction as a percentage: 0.123 → "12%". With <paramref name="signed"/>, non-zero values carry their sign
    /// ("+8.6%", "-3.2%"); a value that rounds to zero has none.
    /// </summary>
    public static string Percent(double fraction, int decimals = 0, bool signed = false, CultureInfo? culture = null)
    {
        if (!double.IsFinite(fraction))
        {
            return Missing;
        }
        culture ??= DefaultCulture;
        decimals = Math.Max(0, decimals);
        var percent = fraction * 100;
        var rounded = Math.Round(Math.Abs(percent), decimals, MidpointRounding.AwayFromZero);
        var text = rounded.ToString("N" + decimals, culture);
        if (rounded == 0)
        {
            return text + "%";
        }
        var sign = percent < 0 ? culture.NumberFormat.NegativeSign : signed ? culture.NumberFormat.PositiveSign : string.Empty;
        return sign + text + "%";
    }

    /// <summary>The zoom readout: one decimal under 10 ("1.0×", "2.4×"), whole numbers from 10 ("53×").</summary>
    public static string ZoomReadout(double zoom, CultureInfo? culture = null)
    {
        if (!double.IsFinite(zoom))
        {
            return Missing;
        }
        culture ??= DefaultCulture;
        return (zoom < 10 ? zoom.ToString("F1", culture) : Math.Round(zoom).ToString("F0", culture)) + "×";
    }
}
