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

    private static bool Matches(char c, string token) => token.Length == 1 && c == token[0];
}
