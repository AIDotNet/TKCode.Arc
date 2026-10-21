using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The filter syntax of the grid's filter row. Pure functions, usable on any thread.
/// <list type="bullet">
/// <item>Text: <c>abc</c> contains (ignoring case and accents), <c>=abc</c> equals, <c>!abc</c> does not contain.</item>
/// <item>Numbers: <c>&gt;100</c>, <c>&gt;=100</c>, <c>&lt;5</c>, <c>&lt;=5</c>, <c>=12</c>, <c>12</c>, <c>!=0</c>, <c>&lt;&gt;0</c>,
/// or a range <c>10..50</c>, <c>10 to 50</c>, <c>10–50</c>.</item>
/// <item>Days: a day (<c>2026-01-31</c>), a month (<c>2026-03</c>) or a year (<c>2026</c>), optionally after an operator,
/// or a range <c>2026-01-01..2026-03-31</c>.</item>
/// </list>
/// </summary>
public static class DataGridFilterExpression
{
    private static readonly string[] s_operators = [">=", "<=", "!=", "<>", ">", "<", "="];

    /// <summary>
    /// Parses a number expression. Returns false when it cannot be read; <paramref name="predicate"/> is null for an
    /// empty expression (no filter).
    /// </summary>
    public static bool TryParseNumber(string? text, CultureInfo? culture, out Func<decimal, bool>? predicate)
    {
        culture ??= CultureInfo.CurrentCulture;
        predicate = null;
        var s = (text ?? string.Empty).Trim();
        if (s.Length == 0)
        {
            return true;
        }
        if (SplitRange(s, out var left, out var right))
        {
            if (!DataGridValueParser.TryParseNumber(left, culture, null, out var a) ||
                !DataGridValueParser.TryParseNumber(right, culture, null, out var b))
            {
                return false;
            }
            var low = Math.Min(a, b);
            var high = Math.Max(a, b);
            predicate = v => v >= low && v <= high;
            return true;
        }
        var op = ReadOperator(ref s);
        if (!DataGridValueParser.TryParseNumber(s, culture, null, out var n))
        {
            return false;
        }
        predicate = op switch
        {
            ">" => v => v > n,
            ">=" => v => v >= n,
            "<" => v => v < n,
            "<=" => v => v <= n,
            "!=" or "<>" => v => v != n,
            _ => v => v == n
        };
        return true;
    }

    /// <summary>
    /// Parses a day expression: a day, a month or a year compares by its whole span (<c>&gt; 2026-03</c> is after March).
    /// Returns false when it cannot be read; <paramref name="predicate"/> is null for an empty expression.
    /// </summary>
    public static bool TryParseDate(string? text, CultureInfo? culture, out Func<DateOnly, bool>? predicate)
    {
        culture ??= CultureInfo.CurrentCulture;
        predicate = null;
        var s = (text ?? string.Empty).Trim();
        if (s.Length == 0)
        {
            return true;
        }
        if (SplitRange(s, out var left, out var right))
        {
            if (!TryReadSpan(left, culture, out var a) || !TryReadSpan(right, culture, out var b))
            {
                return false;
            }
            var (first, last) = a.First <= b.First ? (a.First, b.Last) : (b.First, a.Last);
            predicate = d => d >= first && d <= last;
            return true;
        }
        var op = ReadOperator(ref s);
        if (!TryReadSpan(s, culture, out var span))
        {
            return false;
        }
        var (lo, hi) = span;
        predicate = op switch
        {
            ">" => d => d > hi,
            ">=" => d => d >= lo,
            "<" => d => d < lo,
            "<=" => d => d <= hi,
            "!=" or "<>" => d => d < lo || d > hi,
            _ => d => d >= lo && d <= hi
        };
        return true;
    }

    /// <summary>
    /// A text predicate: contains by default, <c>=</c> for an exact match, <c>!</c> to exclude; case and accents are
    /// ignored. Null for an empty expression.
    /// </summary>
    public static Func<string, bool>? CreateText(string? text, CultureInfo? culture)
    {
        culture ??= CultureInfo.CurrentCulture;
        var s = (text ?? string.Empty).Trim();
        if (s.Length == 0)
        {
            return null;
        }
        var exclude = s[0] == '!';
        var exact = s[0] == '=';
        var needle = exclude || exact ? s[1..].Trim() : s;
        if (needle.Length == 0)
        {
            return null;
        }
        var compare = culture.CompareInfo;
        const CompareOptions options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreWidth |
                                       CompareOptions.IgnoreKanaType;
        if (exact)
        {
            return exclude
                ? v => compare.Compare(v, needle, options) != 0
                : v => compare.Compare(v, needle, options) == 0;
        }
        return exclude
            ? v => compare.IndexOf(v, needle, options) < 0
            : v => compare.IndexOf(v, needle, options) >= 0;
    }

    /// <summary>True when <paramref name="filter"/> narrows a column of <paramref name="type"/> (an unreadable expression does not).</summary>
    public static bool IsActive(DataGridColumnType type, DataGridFilter? filter, CultureInfo? culture = null)
    {
        if (filter is null)
        {
            return false;
        }
        if (filter.Values is not null)
        {
            return true;
        }
        var text = filter.Expression ?? string.Empty;
        return type switch
        {
            DataGridColumnType.Number or DataGridColumnType.Currency or DataGridColumnType.Percent =>
                TryParseNumber(text, culture, out var n) && n is not null,
            DataGridColumnType.Date => TryParseDate(text, culture, out var d) && d is not null,
            _ => CreateText(text, culture) is not null
        };
    }

    /// <summary>True when an expression of a number or date column cannot be read (the filter input is marked invalid).</summary>
    public static bool IsInvalid(DataGridColumnType type, string? expression, CultureInfo? culture = null) => type switch
    {
        DataGridColumnType.Number or DataGridColumnType.Currency or DataGridColumnType.Percent => !TryParseNumber(expression, culture, out _),
        DataGridColumnType.Date => !TryParseDate(expression, culture, out _),
        _ => false
    };

    private static string ReadOperator(ref string s)
    {
        foreach (var op in s_operators)
        {
            if (s.StartsWith(op, StringComparison.Ordinal))
            {
                s = s[op.Length..].Trim();
                return op;
            }
        }
        return string.Empty;
    }

    private static bool SplitRange(string s, out string left, out string right)
    {
        left = right = string.Empty;
        foreach (var separator in new[] { "..", "–", "—" })
        {
            var at = s.IndexOf(separator, 1, StringComparison.Ordinal);
            if (at > 0)
            {
                left = s[..at].Trim();
                right = s[(at + separator.Length)..].Trim();
                return left.Length > 0 && right.Length > 0;
            }
        }
        var to = s.IndexOf(" to ", StringComparison.OrdinalIgnoreCase);
        if (to > 0)
        {
            left = s[..to].Trim();
            right = s[(to + 4)..].Trim();
            return left.Length > 0 && right.Length > 0;
        }
        return false;
    }

    /// <summary>A day, a month (2026-03) or a year (2026) as its first and last day.</summary>
    private static bool TryReadSpan(string text, CultureInfo culture, out (DateOnly First, DateOnly Last) span)
    {
        span = default;
        var s = text.Trim();
        if (s.Length == 4 && int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var year) && year >= 1)
        {
            span = (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
            return true;
        }
        var cut = s.IndexOfAny(['-', '/']);
        if (cut == 4 && s.IndexOfAny(['-', '/'], cut + 1) < 0 &&
            int.TryParse(s.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out year) &&
            int.TryParse(s.AsSpan(5), NumberStyles.None, CultureInfo.InvariantCulture, out var month) &&
            year >= 1 && month is >= 1 and <= 12 && s.Length - 5 is 1 or 2)
        {
            span = (new DateOnly(year, month, 1), new DateOnly(year, month, DateTime.DaysInMonth(year, month)));
            return true;
        }
        if (DataGridValueParser.TryParseDate(s, culture, out var day))
        {
            span = (day, day);
            return true;
        }
        return false;
    }
}
