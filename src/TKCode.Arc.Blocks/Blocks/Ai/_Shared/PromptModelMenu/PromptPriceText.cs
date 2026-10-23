using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The prices of the studio's model menu, per million tokens, in the invariant culture: whole amounts without decimals
/// ("$2", "$10"), others with two decimals or as many as needed up to four ("$0.10", "$3.50", "$0.125"); the pair is
/// "input · output" ("$2 · $10"). Text only, no currency conversion.
/// </summary>
internal static class PromptPriceText
{
    /// <summary>The separator of the pair.</summary>
    public const string Separator = " · ";

    /// <summary>One price with a "$" in front.</summary>
    public static string Format(decimal price) =>
        "$" + (decimal.Truncate(price) == price
            ? price.ToString("0", CultureInfo.InvariantCulture)
            : price.ToString("0.00##", CultureInfo.InvariantCulture));

    /// <summary>
    /// The pair shown in a row, or null when the model has neither price; a missing one shows as "—". The format of one
    /// price can be replaced (<paramref name="format"/>).
    /// </summary>
    public static string? Pair(decimal? input, decimal? output, Func<decimal, string>? format = null)
    {
        if (input is null && output is null)
        {
            return null;
        }
        format ??= Format;
        return (input is { } i ? format(i) : "—") + Separator + (output is { } o ? format(o) : "—");
    }
}
