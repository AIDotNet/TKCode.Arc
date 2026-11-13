using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace TKCode.Arc.A2UI;

/// <summary>Currency formatting by ISO 4217 code in the renderer's culture.</summary>
internal static class A2UICurrency
{
    private static readonly ConcurrentDictionary<string, string?> s_symbols = new(StringComparer.Ordinal);

    public static string Format(double value, string code, int? decimals, bool grouping, CultureInfo culture)
    {
        var info = (NumberFormatInfo)culture.NumberFormat.Clone();
        var symbol = Symbol(code, culture);
        info.CurrencySymbol = symbol ?? code + " ";
        if (decimals is { } d)
        {
            info.CurrencyDecimalDigits = Math.Clamp(d, 0, 20);
        }
        else if (code is "JPY" or "KRW" or "VND" or "CLP" or "ISK")
        {
            info.CurrencyDecimalDigits = 0;
        }
        else
        {
            info.CurrencyDecimalDigits = 2;
        }

        if (!grouping)
        {
            info.CurrencyGroupSeparator = string.Empty;
        }

        var text = value.ToString("C", info);
        return symbol is null ? text.Replace(code + "  ", code + " ", StringComparison.Ordinal) : text;
    }

    // The symbol of a currency: from the culture itself when it uses that currency, else from a culture of the same
    // language that does, else from any culture that does. Null in invariant mode or for unknown codes.
    private static string? Symbol(string code, CultureInfo culture)
    {
        if (code.Length != 3)
        {
            return null;
        }

        return s_symbols.GetOrAdd(culture.Name + "|" + code, _ =>
        {
            try
            {
                if (!string.IsNullOrEmpty(culture.Name) && new RegionInfo(culture.Name).ISOCurrencySymbol == code)
                {
                    return culture.NumberFormat.CurrencySymbol;
                }
            }
            catch (ArgumentException)
            {
            }

            string? any = null;
            foreach (var c in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                RegionInfo region;
                try
                {
                    region = new RegionInfo(c.Name);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (region.ISOCurrencySymbol != code)
                {
                    continue;
                }

                if (c.TwoLetterISOLanguageName == culture.TwoLetterISOLanguageName)
                {
                    return c.NumberFormat.CurrencySymbol;
                }

                any ??= code == "USD" ? "$" : c.NumberFormat.CurrencySymbol;
            }

            return any;
        });
    }
}

/// <summary>Dates: parsing ISO 8601 values and formatting with a Unicode TR35 pattern subset.</summary>
internal static class A2UIDateFormat
{
    /// <summary>
    /// Reads an ISO 8601 date, date-time or time. The wall-clock time is kept as written (an offset is not converted to
    /// local time). <paramref name="original"/> is the text as given, for the <c>ISO</c> format.
    /// </summary>
    public static bool TryParse(JsonNode? value, out DateTimeOffset date, out string? original)
    {
        date = default;
        original = null;
        if (A2UIJson.TryGetDouble(value, out var epoch) && A2UIJson.IsNumber(value))
        {
            try
            {
                date = DateTimeOffset.FromUnixTimeMilliseconds((long)epoch);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        var text = A2UIJson.AsString(value)?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        original = text;
        string[] formats =
        [
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", "yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mmK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF",
            "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd"
        ];
        if (DateTimeOffset.TryParseExact(text, formats, CultureInfo.InvariantCulture,
                                         DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out date))
        {
            return true;
        }

        if (TimeOnly.TryParseExact(text, ["HH:mm:ss", "HH:mm", "HH:mm:ss.FFFFFFF"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
        {
            date = new DateTimeOffset(new DateTime(2000, 1, 1).Add(time.ToTimeSpan()), TimeSpan.Zero);
            return true;
        }

        return false;
    }

    /// <summary>Formats with TR35 letters: y M L d E a h H k K m s S z Z X, quoted text, other characters as is.</summary>
    public static string Format(DateTimeOffset date, string pattern, CultureInfo culture)
    {
        var dtf = culture.DateTimeFormat;
        var sb = new StringBuilder();
        var i = 0;
        while (i < pattern.Length)
        {
            var c = pattern[i];
            if (c == '\'')
            {
                var end = pattern.IndexOf('\'', i + 1);
                if (end == i + 1)
                {
                    sb.Append('\'');
                    i += 2;
                    continue;
                }

                if (end < 0)
                {
                    end = pattern.Length;
                }

                sb.Append(pattern, i + 1, end - i - 1);
                i = end + 1;
                continue;
            }

            if (!char.IsAsciiLetter(c))
            {
                sb.Append(c);
                i++;
                continue;
            }

            var run = 1;
            while (i + run < pattern.Length && pattern[i + run] == c)
            {
                run++;
            }

            i += run;
            var hour12 = date.Hour % 12 == 0 ? 12 : date.Hour % 12;
            switch (c)
            {
                case 'y' or 'Y' or 'u':
                    sb.Append(run == 2 ? (date.Year % 100).ToString("00", CultureInfo.InvariantCulture) : date.Year.ToString(new string('0', Math.Max(run, 1)), CultureInfo.InvariantCulture));
                    break;
                case 'M' or 'L':
                    sb.Append(run switch
                    {
                        1 => date.Month.ToString(CultureInfo.InvariantCulture),
                        2 => date.Month.ToString("00", CultureInfo.InvariantCulture),
                        3 => dtf.AbbreviatedMonthNames[date.Month - 1],
                        4 => dtf.MonthNames[date.Month - 1],
                        _ => dtf.MonthNames[date.Month - 1][..1]
                    });
                    break;
                case 'd':
                    sb.Append(run == 1 ? date.Day.ToString(CultureInfo.InvariantCulture) : date.Day.ToString("00", CultureInfo.InvariantCulture));
                    break;
                case 'E' or 'e' or 'c':
                    sb.Append(run switch
                    {
                        <= 3 => dtf.AbbreviatedDayNames[(int)date.DayOfWeek],
                        4 => dtf.DayNames[(int)date.DayOfWeek],
                        _ => dtf.ShortestDayNames[(int)date.DayOfWeek]
                    });
                    break;
                case 'a':
                    sb.Append(date.Hour < 12 ? Designator(dtf.AMDesignator, "AM") : Designator(dtf.PMDesignator, "PM"));
                    break;
                case 'h':
                    sb.Append(hour12.ToString(run == 1 ? "0" : "00", CultureInfo.InvariantCulture));
                    break;
                case 'H':
                    sb.Append(date.Hour.ToString(run == 1 ? "0" : "00", CultureInfo.InvariantCulture));
                    break;
                case 'k':
                    sb.Append((date.Hour == 0 ? 24 : date.Hour).ToString(run == 1 ? "0" : "00", CultureInfo.InvariantCulture));
                    break;
                case 'K':
                    sb.Append((date.Hour % 12).ToString(run == 1 ? "0" : "00", CultureInfo.InvariantCulture));
                    break;
                case 'm':
                    sb.Append(date.Minute.ToString(run == 1 ? "0" : "00", CultureInfo.InvariantCulture));
                    break;
                case 's':
                    sb.Append(date.Second.ToString(run == 1 ? "0" : "00", CultureInfo.InvariantCulture));
                    break;
                case 'S':
                    var fraction = (date.Ticks % TimeSpan.TicksPerSecond).ToString("0000000", CultureInfo.InvariantCulture);
                    sb.Append(fraction[..Math.Min(run, 7)].PadRight(run, '0'));
                    break;
                case 'z' or 'Z' or 'X' or 'x' or 'O':
                    sb.Append(Offset(date.Offset, c, run));
                    break;
                default:
                    sb.Append(c, run);
                    break;
            }
        }

        return sb.ToString();
    }

    private static string Designator(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

    private static string Offset(TimeSpan offset, char letter, int run)
    {
        if (offset == TimeSpan.Zero && letter is 'X')
        {
            return "Z";
        }

        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        if (letter is 'z' or 'O')
        {
            return "GMT" + (abs == TimeSpan.Zero ? string.Empty : sign + abs.Hours.ToString(CultureInfo.InvariantCulture) + (abs.Minutes > 0 ? ":" + abs.Minutes.ToString("00", CultureInfo.InvariantCulture) : string.Empty));
        }

        var colon = run >= 3 ? ":" : string.Empty;
        return sign + abs.Hours.ToString("00", CultureInfo.InvariantCulture) + colon + abs.Minutes.ToString("00", CultureInfo.InvariantCulture);
    }
}

/// <summary>CLDR cardinal plural categories for common languages; other languages use the English rule.</summary>
internal static class A2UIPluralRules
{
    public static string Category(CultureInfo culture, double n)
    {
        var language = culture.TwoLetterISOLanguageName;
        var abs = Math.Abs(n);
        var isInteger = abs == Math.Floor(abs);
        var i = (long)Math.Floor(abs);
        switch (language)
        {
            case "zh" or "ja" or "ko" or "th" or "vi" or "id" or "ms":
                return "other";
            case "fr" or "pt" when language == "fr" || culture.Name == "pt-BR" || culture.Name == "pt":
                return i is 0 or 1 ? "one" : (isInteger && i != 0 && i % 1_000_000 == 0 ? "many" : "other");
            case "ru" or "uk" or "be":
                if (!isInteger)
                {
                    return "other";
                }

                if (i % 10 == 1 && i % 100 != 11)
                {
                    return "one";
                }

                if (i % 10 is >= 2 and <= 4 && i % 100 is < 12 or > 14)
                {
                    return "few";
                }

                return "many";
            case "pl":
                if (!isInteger)
                {
                    return "other";
                }

                if (i == 1)
                {
                    return "one";
                }

                if (i % 10 is >= 2 and <= 4 && i % 100 is < 12 or > 14)
                {
                    return "few";
                }

                return "many";
            case "cs" or "sk":
                if (!isInteger)
                {
                    return "many";
                }

                return i == 1 ? "one" : i is >= 2 and <= 4 ? "few" : "other";
            case "ar":
                if (!isInteger)
                {
                    return "other";
                }

                if (i == 0)
                {
                    return "zero";
                }

                if (i == 1)
                {
                    return "one";
                }

                if (i == 2)
                {
                    return "two";
                }

                if (i % 100 is >= 3 and <= 10)
                {
                    return "few";
                }

                return i % 100 is >= 11 and <= 99 ? "many" : "other";
            case "he":
                return i == 1 && isInteger ? "one" : i == 2 && isInteger ? "two" : "other";
            case "cy":
                return n switch
                {
                    0 => "zero",
                    1 => "one",
                    2 => "two",
                    3 => "few",
                    6 => "many",
                    _ => "other"
                };
            default:
                return isInteger && i == 1 ? "one" : "other";
        }
    }
}
