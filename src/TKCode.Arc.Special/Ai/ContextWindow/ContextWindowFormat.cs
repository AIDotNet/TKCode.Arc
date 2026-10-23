using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The number formats of <see cref="ContextWindow"/> (measured from the reference): token counts as "996", "4.1K", "2K",
/// "509K" or "1M"; legend percentages with one decimal below 10% ("0.4%", "1.0%") and whole above ("10%", "93%"); the
/// header percentage whole, and above 100 when the window overflows. All rounding is half away from zero (as JavaScript's
/// <c>Math.round</c> for positive values), never banker's rounding.
/// </summary>
internal static class ContextWindowFormat
{
    /// <summary>A token count: below 1,000 whole, below 10K one decimal of K without a trailing ".0", below 1M whole K, then M.</summary>
    public static string Tokens(double value)
    {
        var key = TokensKey(value);
        return key switch
        {
            < KiloTenths => key.ToString(CultureInfo.InvariantCulture),
            < Kilo => Tenths(key - KiloTenths) + "K",
            < Mega => (key - Kilo).ToString(CultureInfo.InvariantCulture) + "K",
            _ => Tenths(key - Mega) + "M"
        };
    }

    // Display keys: one integer per distinct text, so callers can skip formatting while the shown text is the same.
    private const long KiloTenths = 1L << 40;
    private const long Kilo = 2L << 40;
    private const long Mega = 3L << 40;

    /// <summary>A number that is equal for two values exactly when <see cref="Tokens"/> shows the same text for them.</summary>
    public static long TokensKey(double value)
    {
        var v = double.IsFinite(value) && value > 0 ? value : 0;
        var whole = Round(v);
        if (whole < 1000)
        {
            return whole;
        }
        var tenths = Round(v / 100);
        if (tenths < 100)
        {
            return KiloTenths + tenths;
        }
        var kilo = Round(v / 1000);
        if (kilo < 1000)
        {
            return Kilo + kilo;
        }
        return Mega + Round(v / 100_000);
    }

    /// <summary>
    /// A legend percentage of <paramref name="tokens"/> in <paramref name="window"/>: one decimal (kept even when it is 0)
    /// below 10%, whole from 10%. Each row rounds on its own, so the rows need not add up to exactly 100%.
    /// </summary>
    public static string LegendPercent(double tokens, double window)
    {
        var key = LegendPercentKey(tokens, window);
        return key < 100
            ? Tenths(key, keepZero: true) + "%"
            : (key - 100 + 10).ToString(CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>Equal for two values exactly when <see cref="LegendPercent"/> shows the same text.</summary>
    public static long LegendPercentKey(double tokens, double window)
    {
        var p = Percent(tokens, window);
        var tenths = Round(p * 10);
        if (tenths < 100)
        {
            return tenths;
        }
        // Whole percents from 10: keys 100 (10%), 101 (11%) and so on.
        return 100 + Math.Max(0, Round(p) - 10);
    }

    /// <summary>The header percentage of the window used: a whole number that can pass 100.</summary>
    public static long HeaderPercent(double used, double window) => Round(Percent(used, window));

    /// <summary>The header percentage as text ("43%").</summary>
    public static string HeaderPercentText(double used, double window) =>
        HeaderPercent(used, window).ToString(CultureInfo.InvariantCulture) + "%";

    /// <summary>The threshold as a whole percent ("80").</summary>
    public static int ThresholdPercent(double threshold) => (int)Round(threshold * 100);

    private static double Percent(double part, double whole) =>
        whole > 0 && double.IsFinite(part) && part > 0 ? part / whole * 100 : 0;

    /// <summary>Half away from zero, as a whole number.</summary>
    public static long Round(double value) => (long)Math.Round(value, MidpointRounding.AwayFromZero);

    private static string Tenths(long tenths, bool keepZero = false)
    {
        var whole = tenths / 10;
        var fraction = tenths % 10;
        if (fraction == 0 && !keepZero)
        {
            return whole.ToString(CultureInfo.InvariantCulture);
        }
        return whole.ToString(CultureInfo.InvariantCulture) + "." + fraction.ToString(CultureInfo.InvariantCulture);
    }
}
