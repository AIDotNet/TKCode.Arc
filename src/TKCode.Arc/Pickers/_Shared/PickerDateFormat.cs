using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// Date formatting for the pickers. .NET has no <c>Intl.DateTimeFormat.formatToParts</c> or <c>formatRange</c>, so
/// patterns are tokenized here: a field token (day, month, year...) is a part that can roll, everything else is a
/// literal. The default "medium" pattern is the culture's long date without the weekday and with the abbreviated
/// month (en-US "MMM d, yyyy" → "Oct 20, 2026"; zh-CN "yyyy年M月d日"), matching Arc's
/// <c>{ month: "short", day: "numeric", year: "numeric" }</c>.
/// </summary>
internal static class PickerDateFormat
{
    /// <summary>A pattern token: a field (<see cref="Field"/> repeated <see cref="Length"/> times) or a literal.</summary>
    internal readonly record struct Token(char Field, int Length, string Literal)
    {
        public bool IsField => Field != '\0';
    }

    private static readonly ConcurrentDictionary<string, Token[]> s_tokens = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, string> s_medium = new(StringComparer.Ordinal);

    private const string FieldLetters = "dfFghHKmMstyz";

    /// <summary>Splits a .NET custom date pattern into fields and literals (quotes and escapes become literals).</summary>
    public static Token[] Tokenize(string pattern) => s_tokens.GetOrAdd(pattern, static p =>
    {
        var tokens = new List<Token>();
        var literal = new StringBuilder();
        void Flush()
        {
            if (literal.Length > 0)
            {
                tokens.Add(new Token('\0', 0, literal.ToString()));
                literal.Clear();
            }
        }
        var i = 0;
        while (i < p.Length)
        {
            var c = p[i];
            if (c is '\'' or '"')
            {
                var end = p.IndexOf(c, i + 1);
                if (end < 0)
                {
                    end = p.Length;
                }
                literal.Append(p, i + 1, end - i - 1);
                i = end + 1;
                continue;
            }
            if (c == '\\' && i + 1 < p.Length)
            {
                literal.Append(p[i + 1]);
                i += 2;
                continue;
            }
            if (c == '%' && i + 1 < p.Length)
            {
                i++;
                continue;
            }
            if (FieldLetters.Contains(c))
            {
                Flush();
                var run = 1;
                while (i + run < p.Length && p[i + run] == c)
                {
                    run++;
                }
                tokens.Add(new Token(c, run, string.Empty));
                i += run;
                continue;
            }
            literal.Append(c);
            i++;
        }
        Flush();
        return tokens.ToArray();
    });

    /// <summary>The culture's long date without the weekday, with the abbreviated month and an unpadded day.</summary>
    public static string MediumPattern(CultureInfo culture) => s_medium.GetOrAdd(culture.Name, static (_, c) =>
    {
        var tokens = Tokenize(c.DateTimeFormat.LongDatePattern).ToList();
        // Drop the weekday with the separator that ties it to the date.
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i] is not { Field: 'd', Length: >= 3 })
            {
                continue;
            }
            tokens.RemoveAt(i);
            // A pure separator (", ", " ") goes with the weekday; a literal that belongs to a field ("日", "일 ") stays, trimmed.
            if (i < tokens.Count && !tokens[i].IsField)
            {
                var after = tokens[i].Literal;
                if (IsSeparator(after) || i == 0)
                {
                    tokens.RemoveAt(i);
                }
                else
                {
                    tokens[i] = tokens[i] with { Literal = after.TrimStart() };
                }
            }
            else if (i > 0 && !tokens[i - 1].IsField)
            {
                var before = tokens[i - 1].Literal;
                if (IsSeparator(before))
                {
                    tokens.RemoveAt(i - 1);
                }
                else
                {
                    tokens[i - 1] = tokens[i - 1] with { Literal = before.TrimEnd() };
                }
            }
            i--;
        }
        var builder = new StringBuilder();
        foreach (var token in tokens)
        {
            if (!token.IsField)
            {
                builder.Append('\'').Append(token.Literal.Replace("'", "\\'")).Append('\'');
                continue;
            }
            var length = token.Field switch
            {
                'M' when token.Length >= 3 => 3,
                'd' => 1,
                _ => token.Length
            };
            builder.Append(token.Field, length);
        }
        var pattern = builder.ToString();
        return pattern.Contains('d') && pattern.Contains('M') && pattern.Contains('y') ? pattern : "MMM d, yyyy";
    }, culture);

    private static bool IsSeparator(string literal) => literal.All(c => char.IsWhiteSpace(c) || c is ',' or '.' or '-' or '/' or '،');

    /// <summary>The pattern to use: <paramref name="custom"/> when set, else the culture's medium pattern.</summary>
    public static string Effective(string? custom, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(custom) ? MediumPattern(culture) : custom;

    /// <summary>Formats <paramref name="date"/> as rolling parts: fields roll, literals stay.</summary>
    public static void FormatParts(DateOnly date, string pattern, CultureInfo culture, List<PickerRollSegment> parts)
    {
        parts.Clear();
        var tokens = Tokenize(pattern);
        var hasDay = tokens.Any(t => t is { Field: 'd', Length: <= 2 });
        foreach (var token in tokens)
        {
            if (!token.IsField)
            {
                if (token.Literal.Length > 0)
                {
                    parts.Add(new PickerRollSegment(token.Literal, Rolls: false));
                }
                continue;
            }
            parts.Add(new PickerRollSegment(FormatField(date, token, culture, hasDay)));
        }
    }

    public static string Format(DateOnly date, string pattern, CultureInfo culture)
    {
        var tokens = Tokenize(pattern);
        var hasDay = tokens.Any(t => t is { Field: 'd', Length: <= 2 });
        var builder = new StringBuilder();
        foreach (var token in tokens)
        {
            builder.Append(token.IsField ? FormatField(date, token, culture, hasDay) : token.Literal);
        }
        return builder.ToString();
    }

    private static string FormatField(DateOnly date, Token token, CultureInfo culture, bool hasDay)
    {
        var info = culture.DateTimeFormat;
        var calendar = info.Calendar;
        var time = date.ToDateTime(TimeOnly.MinValue);
        switch (token.Field)
        {
            case 'd' when token.Length <= 2:
            {
                var day = calendar.GetDayOfMonth(time);
                return token.Length == 1 ? day.ToString(culture) : day.ToString("00", culture);
            }
            case 'M':
            {
                var month = calendar.GetMonth(time);
                return token.Length switch
                {
                    1 => month.ToString(culture),
                    2 => month.ToString("00", culture),
                    3 => Name(hasDay ? info.AbbreviatedMonthGenitiveNames : info.AbbreviatedMonthNames, info.AbbreviatedMonthNames, month),
                    _ => Name(hasDay ? info.MonthGenitiveNames : info.MonthNames, info.MonthNames, month)
                };
            }
            case 'y':
            {
                var year = calendar.GetYear(time);
                return token.Length switch
                {
                    1 => (year % 100).ToString(culture),
                    2 => (year % 100).ToString("00", culture),
                    _ => year.ToString(new string('0', token.Length), culture)
                };
            }
            default:
                return time.ToString(token.Length == 1 ? "%" + token.Field : new string(token.Field, token.Length), culture);
        }
    }

    private static string Name(string[] preferred, string[] fallback, int month)
    {
        var name = month - 1 < preferred.Length ? preferred[month - 1] : string.Empty;
        return string.IsNullOrEmpty(name) ? fallback[month - 1] : name;
    }

    /// <summary>
    /// Reads typed text: the display pattern first, then the culture's short and long patterns, then a lenient parse.
    /// </summary>
    public static bool TryParse(string? text, string pattern, CultureInfo culture, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        var trimmed = text.Trim();
        var info = culture.DateTimeFormat;
        string[] exact = [pattern, info.ShortDatePattern, info.LongDatePattern, MediumPattern(culture), "yyyy-MM-dd"];
        const DateTimeStyles styles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault;
        if (DateTime.TryParseExact(trimmed, exact, culture, styles, out var parsed) ||
            DateTime.TryParse(trimmed, culture, DateTimeStyles.AllowWhiteSpaces, out parsed))
        {
            date = DateOnly.FromDateTime(parsed);
            return true;
        }
        return false;
    }

    /// <summary>
    /// A range in the medium pattern with shared fields collapsed (Intl <c>formatRange</c>): "Oct 1 – 20, 2026",
    /// "Sep 28 – Oct 4, 2026", "Dec 28, 2025 – Jan 3, 2026".
    /// </summary>
    public static string FormatRange(DateOnly start, DateOnly end, CultureInfo culture)
    {
        var pattern = MediumPattern(culture);
        if (start == end)
        {
            return Format(start, pattern, culture);
        }
        const string dash = " – ";
        var tokens = Tokenize(pattern);
        var yearIndex = Array.FindIndex(tokens, t => t.Field == 'y');
        var monthIndex = Array.FindIndex(tokens, t => t.Field == 'M');
        var dayIndex = Array.FindIndex(tokens, t => t is { Field: 'd', Length: <= 2 });
        if (start.Year != end.Year || yearIndex < 0 || monthIndex < 0 || dayIndex < 0)
        {
            return Format(start, pattern, culture) + dash + Format(end, pattern, culture);
        }
        var yearFirst = yearIndex < monthIndex;
        if (start.Month == end.Month)
        {
            if (dayIndex > monthIndex)
            {
                // Month before day: "Oct 1 – 20, 2026", "2026年10月1 – 20日".
                return Join(start, tokens, culture, 0, dayIndex) + dash + Join(end, tokens, culture, dayIndex, tokens.Length - 1);
            }
            // Day first: "1. – 20. Okt. 2026".
            var literalAfterDay = dayIndex + 1 < tokens.Length && !tokens[dayIndex + 1].IsField ? dayIndex + 1 : dayIndex;
            return Join(start, tokens, culture, 0, literalAfterDay).TrimEnd() + dash + Format(end, pattern, culture);
        }
        if (!yearFirst)
        {
            // Drop the start's year with the separator before it: "Sep 28 – Oct 4, 2026".
            var from = yearIndex > 0 && !tokens[yearIndex - 1].IsField ? yearIndex - 1 : yearIndex;
            return Join(start, tokens, culture, 0, from - 1) + Join(start, tokens, culture, yearIndex + 1, tokens.Length - 1) + dash +
                   Format(end, pattern, culture);
        }
        // Year first: "2026年9月28日 – 10月4日".
        var to = yearIndex + 1 < tokens.Length && !tokens[yearIndex + 1].IsField ? yearIndex + 1 : yearIndex;
        return Format(start, pattern, culture) + dash + Join(end, tokens, culture, 0, yearIndex - 1) + Join(end, tokens, culture, to + 1, tokens.Length - 1);
    }

    private static string Join(DateOnly date, Token[] tokens, CultureInfo culture, int from, int to)
    {
        var builder = new StringBuilder();
        var hasDay = tokens.Any(t => t is { Field: 'd', Length: <= 2 });
        for (var i = Math.Max(0, from); i <= to && i < tokens.Length; i++)
        {
            var token = tokens[i];
            builder.Append(token.IsField ? FormatField(date, token, culture, hasDay) : token.Literal);
        }
        return builder.ToString();
    }
}
