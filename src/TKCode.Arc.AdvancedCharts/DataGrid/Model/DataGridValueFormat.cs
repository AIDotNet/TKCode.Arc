using System.Globalization;

namespace TKCode.Arc;

/// <summary>How a column shows and reads its values: the type plus its number and date settings.</summary>
/// <param name="Type">The column type.</param>
/// <param name="Decimals">Decimal places of numeric types.</param>
/// <param name="CurrencySymbol">Symbol of currency values.</param>
/// <param name="DateFormat">.NET date format; null uses <see cref="DataGridValueFormat.DefaultDatePattern"/>.</param>
public readonly record struct DataGridCellFormat(DataGridColumnType Type, int Decimals = 0, string CurrencySymbol = "$", string? DateFormat = null)
{
    /// <summary>True for Number, Currency and Percent.</summary>
    public bool IsNumeric => Type is DataGridColumnType.Number or DataGridColumnType.Currency or DataGridColumnType.Percent;
}

/// <summary>A total shortened for display: the number to show, its decimals and the unit after it (K, M, B, 万, 亿).</summary>
public readonly record struct DataGridCompactNumber(decimal Value, int Decimals, string Unit);

/// <summary>
/// Display text of grid values. Pure functions, usable on any thread: numbers use the culture's grouping and decimal
/// separators and a true minus sign (U+2212), currency puts the column's symbol before the digits, percent appends
/// the culture's percent sign, and days use the column's date format.
/// </summary>
public static class DataGridValueFormat
{
    /// <summary>The minus sign used for negative numbers (U+2212).</summary>
    public const string MinusSign = "−";

    /// <summary>Display text of a normalized value (a string, a decimal, a DateOnly or null).</summary>
    public static string Format(object? value, DataGridCellFormat format, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        switch (value)
        {
            case null:
                return string.Empty;
            case string text:
                return text;
            case decimal number:
                return format.IsNumeric ? FormatNumber(number, format, culture) : number.ToString(culture);
            case DateOnly date:
                return format.Type == DataGridColumnType.Date
                    ? FormatDate(date, format.DateFormat, culture)
                    : date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            case bool flag:
                return format.Type == DataGridColumnType.Boolean ? FormatBoolean(flag, culture) : flag ? bool.TrueString : bool.FalseString;
            case IFormattable formattable:
                return formattable.ToString(null, culture);
            default:
                return value.ToString() ?? string.Empty;
        }
    }

    /// <summary>The words of a boolean value for display: 是 and 否 in Chinese cultures, Yes and No otherwise.</summary>
    public static string FormatBoolean(bool value, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        return culture.TwoLetterISOLanguageName == "zh" ? value ? "是" : "否" : value ? "Yes" : "No";
    }

    /// <summary>A number with the format's decimals, sign, currency symbol or percent sign.</summary>
    public static string FormatNumber(decimal value, DataGridCellFormat format, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var decimals = Math.Clamp(format.Decimals, 0, 20);
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        var digits = Math.Abs(rounded).ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), culture);
        var sign = rounded < 0 ? MinusSign : string.Empty;
        return format.Type switch
        {
            DataGridColumnType.Currency => sign + format.CurrencySymbol + digits,
            DataGridColumnType.Percent => sign + digits + culture.NumberFormat.PercentSymbol,
            _ => sign + digits
        };
    }

    /// <summary>A day in <paramref name="pattern"/>, or in <see cref="DefaultDatePattern"/> when it is null.</summary>
    public static string FormatDate(DateOnly date, string? pattern, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        try
        {
            return date.ToString(string.IsNullOrEmpty(pattern) ? DefaultDatePattern(culture) : pattern, culture);
        }
        catch (FormatException)
        {
            return date.ToString(DefaultDatePattern(culture), culture);
        }
    }

    /// <summary>
    /// The default day format of a culture: the month name with the day and year in the culture's order, such as
    /// "Jan 31, 2026" (English), "2026年1月31日" (Chinese, Japanese) or "31 janv. 2026".
    /// </summary>
    public static string DefaultDatePattern(CultureInfo culture)
    {
        switch (culture.TwoLetterISOLanguageName)
        {
            case "en":
                return "MMM d, yyyy";
            case "zh":
            case "ja":
                return "yyyy年M月d日";
            case "ko":
                return "yyyy. M. d.";
            case "de":
                return "d. MMM yyyy";
            case "iv":
                return "yyyy-MM-dd";
            default:
                return "d MMM yyyy";
        }
    }

    /// <summary>
    /// Shortens a total for the totals row: B, M (one decimal) and K (no decimals) from 100,000 up; Chinese cultures use
    /// 亿 (one decimal), 万 (no decimals from one million, one decimal below) or their traditional forms 億 and 萬.
    /// Smaller values keep at most one decimal.
    /// </summary>
    public static DataGridCompactNumber FormatCompact(decimal value, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var abs = Math.Abs(value);
        if (culture.TwoLetterISOLanguageName == "zh")
        {
            var traditional = IsTraditionalChinese(culture);
            if (abs >= 100_000_000m)
            {
                return new DataGridCompactNumber(Math.Round(value / 100_000_000m, 1, MidpointRounding.AwayFromZero), 1, traditional ? "億" : "亿");
            }
            if (abs >= 1_000_000m)
            {
                return new DataGridCompactNumber(Math.Round(value / 10_000m, 0, MidpointRounding.AwayFromZero), 0, traditional ? "萬" : "万");
            }
            if (abs >= 100_000m)
            {
                return new DataGridCompactNumber(Math.Round(value / 10_000m, 1, MidpointRounding.AwayFromZero), 1, traditional ? "萬" : "万");
            }
        }
        else
        {
            if (abs >= 1_000_000_000m)
            {
                return new DataGridCompactNumber(Math.Round(value / 1_000_000_000m, 1, MidpointRounding.AwayFromZero), 1, "B");
            }
            if (abs >= 1_000_000m)
            {
                return new DataGridCompactNumber(Math.Round(value / 1_000_000m, 1, MidpointRounding.AwayFromZero), 1, "M");
            }
            if (abs >= 100_000m)
            {
                return new DataGridCompactNumber(Math.Round(value / 1_000m, 0, MidpointRounding.AwayFromZero), 0, "K");
            }
        }
        var small = Math.Round(value, 1, MidpointRounding.AwayFromZero);
        return new DataGridCompactNumber(small, small == decimal.Truncate(small) ? 0 : 1, string.Empty);
    }

    private static bool IsTraditionalChinese(CultureInfo culture)
    {
        for (var c = culture; !string.IsNullOrEmpty(c.Name); c = c.Parent)
        {
            if (c.Name is "zh-Hant" or "zh-TW" or "zh-HK" or "zh-MO")
            {
                return true;
            }
            if (c.Name is "zh-Hans" or "zh-CN" or "zh-SG")
            {
                return false;
            }
        }
        return false;
    }

    /// <summary>The address of a column (0 is A, 26 is AA), as in spreadsheets.</summary>
    public static string ColumnLetters(int index)
    {
        var n = index + 1;
        Span<char> buffer = stackalloc char[8];
        var position = buffer.Length;
        while (n > 0)
        {
            buffer[--position] = (char)('A' + (n - 1) % 26);
            n = (n - 1) / 26;
        }
        return new string(buffer[position..]);
    }
}
