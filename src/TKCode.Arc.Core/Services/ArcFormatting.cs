using System.Globalization;

namespace TKCode.Arc.Primitives;

/// <summary>Kind of a formatted part (Intl formatToParts equivalent).</summary>
public enum FormattedPartKind
{
    Digit,
    Group,
    Decimal,
    Literal
}

public readonly record struct FormattedPart(FormattedPartKind Kind, string Text);

/// <summary>S11: number formatting split into parts, so only digits roll and symbols swap.</summary>
public static class ArcFormatting
{
    public static IReadOnlyList<FormattedPart> ToParts(string formatted, CultureInfo? culture = null)
    {
        var format = (culture ?? CultureInfo.CurrentCulture).NumberFormat;
        var parts = new List<FormattedPart>(formatted.Length);
        for (var i = 0; i < formatted.Length; i++)
        {
            var c = formatted[i];
            parts.Add(new FormattedPart(Classify(c, format), c.ToString()));
        }
        return parts;
    }

    /// <summary>The kind of one character of a formatted number (what <see cref="ToParts"/> assigns it), without allocating.</summary>
    internal static FormattedPartKind Classify(char c, NumberFormatInfo format)
    {
        if (char.IsDigit(c))
        {
            return FormattedPartKind.Digit;
        }
        if (Matches(c, format.NumberGroupSeparator) || Matches(c, format.CurrencyGroupSeparator))
        {
            return FormattedPartKind.Group;
        }
        if (Matches(c, format.NumberDecimalSeparator) || Matches(c, format.CurrencyDecimalSeparator))
        {
            return FormattedPartKind.Decimal;
        }
        return FormattedPartKind.Literal;
    }

    /// <summary>Compact notation: 1.2K, 3.4M, 5.6B.</summary>
    public static string Compact(double value, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var abs = Math.Abs(value);
        return abs switch
        {
            >= 1_000_000_000 => (value / 1_000_000_000).ToString("0.#", culture) + "B",
            >= 1_000_000 => (value / 1_000_000).ToString("0.#", culture) + "M",
            >= 1_000 => (value / 1_000).ToString("0.#", culture) + "K",
            _ => value.ToString("0.#", culture)
        };
    }

    /// <summary>
    /// A size in bytes with binary units (#148): <c>512 B</c>, <c>13 KB</c> (whole kilobytes), <c>4.2 MB</c>, <c>1.5 GB</c>.
    /// Negative sizes read as 0.
    /// </summary>
    /// <param name="bytes">The size.</param>
    /// <param name="culture">The culture for the number; null for the invariant culture, as file lists usually show.</param>
    public static string Bytes(long bytes, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.InvariantCulture;
        const double kilo = 1024;
        const double mega = kilo * 1024;
        const double giga = mega * 1024;
        bytes = Math.Max(0, bytes);
        return bytes switch
        {
            < 1024 => bytes.ToString(culture) + " B",
            < 1024 * 1024 => Math.Round(bytes / kilo, MidpointRounding.AwayFromZero).ToString(culture) + " KB",
            < 1024L * 1024 * 1024 => (bytes / mega).ToString("0.0", culture) + " MB",
            _ => (bytes / giga).ToString("0.0", culture) + " GB"
        };
    }

    private static bool Matches(char c, string token) => token.Length == 1 && c == token[0];
}
