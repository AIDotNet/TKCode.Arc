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
            if (char.IsDigit(c))
            {
                parts.Add(new FormattedPart(FormattedPartKind.Digit, c.ToString()));
            }
            else if (Matches(formatted, i, format.NumberGroupSeparator) || Matches(formatted, i, format.CurrencyGroupSeparator))
            {
                parts.Add(new FormattedPart(FormattedPartKind.Group, c.ToString()));
            }
            else if (Matches(formatted, i, format.NumberDecimalSeparator) || Matches(formatted, i, format.CurrencyDecimalSeparator))
            {
                parts.Add(new FormattedPart(FormattedPartKind.Decimal, c.ToString()));
            }
            else
            {
                parts.Add(new FormattedPart(FormattedPartKind.Literal, c.ToString()));
            }
        }
        return parts;
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

    private static bool Matches(string text, int index, string token)
        => token.Length == 1 && text[index] == token[0];
}
