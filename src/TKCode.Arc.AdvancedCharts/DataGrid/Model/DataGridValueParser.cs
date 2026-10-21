using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// Reads values: normalizes row data (<see cref="Normalize"/>) and parses typed or pasted text
/// (<see cref="TryParse"/>). Pure functions, usable on any thread.
/// </summary>
public static class DataGridValueParser
{
    private static readonly string[] s_englishMonths =
        ["january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december"];

    /// <summary>
    /// Brings a raw row value into the grid's value space: a string, a <see cref="decimal"/>, a <see cref="DateOnly"/>
    /// or null. Numeric columns read any .NET number, date columns read <see cref="DateOnly"/>, <see cref="DateTime"/>,
    /// <see cref="DateTimeOffset"/> and ISO day strings ("2026-01-31"); empty strings become null.
    /// </summary>
    public static object? Normalize(object? raw, DataGridColumnType type)
    {
        switch (raw)
        {
            case null:
                return null;
            case string text:
                if (text.Length == 0)
                {
                    return null;
                }
                if (type == DataGridColumnType.Date && TryParseIsoDay(text, out var iso))
                {
                    return iso;
                }
                return text;
        }
        if (type is DataGridColumnType.Number or DataGridColumnType.Currency or DataGridColumnType.Percent)
        {
            if (TryToDecimal(raw, out var number))
            {
                return number;
            }
        }
        if (type == DataGridColumnType.Date)
        {
            switch (raw)
            {
                case DateOnly day:
                    return day;
                case DateTime time:
                    return DateOnly.FromDateTime(time);
                case DateTimeOffset offset:
                    return DateOnly.FromDateTime(offset.DateTime);
            }
        }
        return raw switch
        {
            decimal or DateOnly => raw,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => raw.ToString()
        };
    }

    /// <summary>Converts any .NET number to a decimal (NaN, infinities and out-of-range values fail).</summary>
    public static bool TryToDecimal(object? raw, out decimal value)
    {
        value = 0;
        try
        {
            switch (raw)
            {
                case decimal d:
                    value = d;
                    return true;
                case int i:
                    value = i;
                    return true;
                case long l:
                    value = l;
                    return true;
                case short s:
                    value = s;
                    return true;
                case byte b:
                    value = b;
                    return true;
                case sbyte sb:
                    value = sb;
                    return true;
                case ushort us:
                    value = us;
                    return true;
                case uint ui:
                    value = ui;
                    return true;
                case ulong ul:
                    value = ul;
                    return true;
                case double dbl when double.IsFinite(dbl):
                    value = (decimal)dbl;
                    return true;
                case float f when float.IsFinite(f):
                    value = (decimal)f;
                    return true;
                case Half h when Half.IsFinite(h):
                    value = (decimal)(float)h;
                    return true;
            }
        }
        catch (OverflowException)
        {
        }
        return false;
    }

    /// <summary>
    /// Parses typed or pasted <paramref name="text"/> for a column. Empty text gives null. Numbers accept the culture's
    /// separators, currency symbols, a trailing %, spaces, a true minus sign and accounting parentheses. Days accept
    /// ISO forms (2026-01-31, 2026/1/31), month names (Jan 31 2026, 31 January 2026, 2026年1月31日) and refuse
    /// all-number forms such as 01/02/2026 that read differently by country. Select values must match one of
    /// <paramref name="options"/> (ignoring case) when there are options.
    /// </summary>
    public static bool TryParse(string? text, DataGridCellFormat format, CultureInfo? culture, IReadOnlyList<string>? options, out object? value)
    {
        culture ??= CultureInfo.CurrentCulture;
        value = null;
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }
        switch (format.Type)
        {
            case DataGridColumnType.Date:
                if (TryParseDate(trimmed, culture, out var day))
                {
                    value = day;
                    return true;
                }
                return false;
            case DataGridColumnType.Select:
                if (options is { Count: > 0 })
                {
                    foreach (var option in options)
                    {
                        if (string.Equals(option, trimmed, StringComparison.CurrentCultureIgnoreCase))
                        {
                            value = option;
                            return true;
                        }
                    }
                    return false;
                }
                value = trimmed;
                return true;
            case DataGridColumnType.Number:
            case DataGridColumnType.Currency:
            case DataGridColumnType.Percent:
                if (TryParseNumber(trimmed, culture, format.CurrencySymbol, out var number))
                {
                    value = number;
                    return true;
                }
                return false;
            default:
                value = trimmed;
                return true;
        }
    }

    /// <summary>Parses a number the way <see cref="TryParse"/> does for numeric columns.</summary>
    public static bool TryParseNumber(string? text, CultureInfo? culture, string? currencySymbol, out decimal value)
    {
        culture ??= CultureInfo.CurrentCulture;
        value = 0;
        var cleaned = CleanNumber(text, culture, currencySymbol);
        if (cleaned.Length == 0)
        {
            return false;
        }
        var negative = false;
        if (cleaned.Length > 2 && cleaned[0] == '(' && cleaned[^1] == ')')
        {
            negative = true;
            cleaned = cleaned[1..^1].Trim();
        }
        const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands |
                                    NumberStyles.AllowExponent | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
        if (!decimal.TryParse(cleaned, styles, culture, out value) &&
            !decimal.TryParse(cleaned, styles, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }
        if (negative)
        {
            value = -value;
        }
        return true;
    }

    /// <summary>Strips currency symbols, percent signs and spaces, and turns the true minus sign into the culture's.</summary>
    internal static string CleanNumber(string? text, CultureInfo culture, string? currencySymbol)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }
        var builder = new StringBuilder(text.Length);
        var s = text.Trim();
        if (!string.IsNullOrEmpty(currencySymbol))
        {
            s = s.Replace(currencySymbol, string.Empty, StringComparison.Ordinal);
        }
        var cultureCurrency = culture.NumberFormat.CurrencySymbol;
        if (!string.IsNullOrEmpty(cultureCurrency))
        {
            s = s.Replace(cultureCurrency, string.Empty, StringComparison.Ordinal);
        }
        var negative = culture.NumberFormat.NegativeSign;
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '$':
                case '%':
                case '‰':
                case '¥':
                case '€':
                case '£':
                    continue;
                case '−':
                case '–':
                    builder.Append(negative);
                    continue;
            }
            if (char.IsWhiteSpace(ch))
            {
                // Spaces are group separators in some cultures (fr: U+202F); keep them only when the culture uses them.
                if (culture.NumberFormat.NumberGroupSeparator.Contains(ch))
                {
                    builder.Append(ch);
                }
                continue;
            }
            builder.Append(ch);
        }
        return builder.ToString().Trim();
    }

    /// <summary>Parses a day the way <see cref="TryParse"/> does for date columns.</summary>
    public static bool TryParseDate(string? text, CultureInfo? culture, out DateOnly date)
    {
        culture ??= CultureInfo.CurrentCulture;
        date = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        var s = text.Trim();
        if (TryParseIsoDay(s, out date))
        {
            return true;
        }
        if (TryParseCjk(s, out date))
        {
            return true;
        }
        // Month-name forms: "Jan 31 2026", "Jan 31, 2026", "31 January 2026", "31. Januar 2026", "janv. 31 2026".
        var tokens = s.ToLowerInvariant().Replace(",", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.TrimEnd('.'))
            .Where(t => t.Length > 0)
            .ToArray();
        if (tokens.Length != 3)
        {
            return false;
        }
        int month;
        int day;
        if ((month = MonthOf(tokens[0], culture)) > 0 && int.TryParse(tokens[1], NumberStyles.None, CultureInfo.InvariantCulture, out day) &&
            int.TryParse(tokens[2], NumberStyles.None, CultureInfo.InvariantCulture, out var year) && tokens[2].Length == 4)
        {
            return TryMake(year, month, day, out date);
        }
        if (int.TryParse(tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out day) && (month = MonthOf(tokens[1], culture)) > 0 &&
            int.TryParse(tokens[2], NumberStyles.None, CultureInfo.InvariantCulture, out year) && tokens[2].Length == 4)
        {
            return TryMake(year, month, day, out date);
        }
        return false;
    }

    /// <summary>Reads "2026-01-31", "2026/1/31" or "2026.1.31" (year first, so it never reads differently by country).</summary>
    public static bool TryParseIsoDay(string? text, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        var s = text.AsSpan().Trim();
        var parts = new int[3];
        var part = 0;
        var digits = 0;
        var value = 0;
        char separator = '\0';
        foreach (var ch in s)
        {
            if (ch is >= '0' and <= '9')
            {
                value = value * 10 + (ch - '0');
                digits++;
                if (digits > (part == 0 ? 4 : 2))
                {
                    return false;
                }
                continue;
            }
            if (ch is not ('-' or '/' or '.') || digits == 0 || part >= 2 || (separator != '\0' && ch != separator))
            {
                return false;
            }
            if (part == 0 && digits != 4)
            {
                return false;
            }
            separator = ch;
            parts[part++] = value;
            value = 0;
            digits = 0;
        }
        if (part != 2 || digits == 0)
        {
            return false;
        }
        parts[2] = value;
        return TryMake(parts[0], parts[1], parts[2], out date);
    }

    private static bool TryParseCjk(string s, out DateOnly date)
    {
        date = default;
        // 2026年1月31日 / 2026年1月31 / 2026년 1월 31일
        var normalized = s.Replace('년', '年').Replace('월', '月').Replace('일', '日').Replace(" ", string.Empty);
        var yearEnd = normalized.IndexOf('年');
        var monthEnd = normalized.IndexOf('月');
        if (yearEnd != 4 || monthEnd <= yearEnd + 1)
        {
            return false;
        }
        var dayText = normalized[(monthEnd + 1)..].TrimEnd('日');
        return int.TryParse(normalized.AsSpan(0, yearEnd), NumberStyles.None, CultureInfo.InvariantCulture, out var year) &&
               int.TryParse(normalized.AsSpan(yearEnd + 1, monthEnd - yearEnd - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var month) &&
               int.TryParse(dayText, NumberStyles.None, CultureInfo.InvariantCulture, out var day) &&
               TryMake(year, month, day, out date);
    }

    private static int MonthOf(string token, CultureInfo culture)
    {
        if (token.Length < 3)
        {
            return 0;
        }
        for (var i = 0; i < 12; i++)
        {
            if (s_englishMonths[i].StartsWith(token, StringComparison.Ordinal))
            {
                return i + 1;
            }
        }
        var names = culture.DateTimeFormat;
        for (var i = 0; i < 12; i++)
        {
            if (Matches(names.MonthNames[i], token, culture) || Matches(names.AbbreviatedMonthNames[i], token, culture) ||
                Matches(names.MonthGenitiveNames[i], token, culture) || Matches(names.AbbreviatedMonthGenitiveNames[i], token, culture))
            {
                return i + 1;
            }
        }
        return 0;
    }

    private static bool Matches(string name, string token, CultureInfo culture) =>
        name.Length > 0 && string.Compare(name.TrimEnd('.'), token, culture, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0;

    private static bool TryMake(int year, int month, int day, out DateOnly date)
    {
        date = default;
        if (year is < 1 or > 9999 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }
        date = new DateOnly(year, month, day);
        return true;
    }
}
