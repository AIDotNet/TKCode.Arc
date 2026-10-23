using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// Reads the text of a value the way the document writes it ("1,400.00", "NL70 ZZTE 0417 2635 98") into the typed value of
/// its field (spec 2.1 <c>parseValue</c>, 4.1 invalid values). An empty text is valid and has no value; anything that does
/// not fit the type is an error of a kind the caller turns into a label text.
/// </summary>
/// <remarks>
/// Rules by type:
/// <list type="bullet">
/// <item>Text: the trimmed text.</item>
/// <item>Date: exactly <c>YYYY-MM-DD</c> and a real date, as a <see cref="DateOnly"/>; "16 Oct" and "2026-02-30" are errors.</item>
/// <item>Money and Number: currency symbols, spaces, apostrophes and a three-letter code before or after the amount are
/// dropped and a leading minus is kept; then the decimal separator is decided: the last <c>.</c> or <c>,</c> is the decimal
/// point when it is followed by one or two digits, when the other separator comes before it, or when it is the only
/// separator and cannot be a thousands separator (a leading "0", or not three digits after it); every other separator
/// must group thousands (three digits after each; Indian two-digit groups are allowed in front). The value is a
/// <see cref="decimal"/> that keeps its digits ("640.00" stays 640.00).</item>
/// <item>Integer: read like Number and then it must be whole, as a <see cref="long"/>.</item>
/// <item>Currency: exactly three upper-case letters A–Z.</item>
/// <item>Iban: spaces removed and upper-cased; two letters, two check digits (02–98) and 11 to 30 letters or digits, and
/// the mod-97 check gives 1; the value is written without spaces.</item>
/// </list>
/// </remarks>
internal static class ExtractionValueParser
{
    /// <summary>Why a text does not fit its type.</summary>
    internal enum ErrorKind
    {
        /// <summary>It fits.</summary>
        None,

        /// <summary>Not a <c>YYYY-MM-DD</c> date.</summary>
        Date,

        /// <summary>Not a number (amounts, numbers and integers).</summary>
        Number,

        /// <summary>Not a valid IBAN.</summary>
        Iban,

        /// <summary>Not a three-letter currency code.</summary>
        Currency
    }

    /// <summary>The typed value (null when the text is empty or does not fit) and the kind of error.</summary>
    internal readonly record struct ParseResult(object? Value, ErrorKind Error)
    {
        /// <summary>Whether the text fits the type.</summary>
        public bool IsValid => Error == ErrorKind.None;
    }

    /// <summary>The longest IBAN (ISO 13616).</summary>
    private const int MaxIbanLength = 34;

    /// <summary>The shortest IBAN in use (Norway).</summary>
    private const int MinIbanLength = 15;

    /// <summary>Reads <paramref name="text"/> as a value of <paramref name="type"/>.</summary>
    public static ParseResult Parse(ExtractionFieldType type, string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return default;
        }
        switch (type)
        {
            case ExtractionFieldType.Date:
                return TryParseDate(trimmed, out var date) ? new ParseResult(date, ErrorKind.None) : new ParseResult(null, ErrorKind.Date);
            case ExtractionFieldType.Money:
            case ExtractionFieldType.Number:
                return TryParseNumber(trimmed, out var number) ? new ParseResult(number, ErrorKind.None) : new ParseResult(null, ErrorKind.Number);
            case ExtractionFieldType.Integer:
                return TryParseNumber(trimmed, out var whole) && decimal.Truncate(whole) == whole && whole >= long.MinValue && whole <= long.MaxValue
                    ? new ParseResult((long)whole, ErrorKind.None)
                    : new ParseResult(null, ErrorKind.Number);
            case ExtractionFieldType.Currency:
                return IsCurrencyCode(trimmed) ? new ParseResult(trimmed, ErrorKind.None) : new ParseResult(null, ErrorKind.Currency);
            case ExtractionFieldType.Iban:
                return TryParseIban(trimmed, out var iban) ? new ParseResult(iban, ErrorKind.None) : new ParseResult(null, ErrorKind.Iban);
            default:
                return new ParseResult(trimmed, ErrorKind.None);
        }
    }

    /// <summary>The value as it goes into the typed output: a date as ISO text, numbers as they are, text as text.</summary>
    public static object? ToOutput(object? value) => value switch
    {
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        _ => value
    };

    private static bool TryParseDate(string text, out DateOnly date)
    {
        date = default;
        if (text.Length != 10 || text[4] != '-' || text[7] != '-')
        {
            return false;
        }
        for (var i = 0; i < text.Length; i++)
        {
            if (i != 4 && i != 7 && !IsAsciiDigit(text[i]))
            {
                return false;
            }
        }
        var year = Digits(text, 0, 4);
        var month = Digits(text, 5, 2);
        var day = Digits(text, 8, 2);
        if (year < 1 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }
        date = new DateOnly(year, month, day);
        return true;
    }

    private static bool TryParseNumber(string text, out decimal value)
    {
        value = 0;
        // Keep digits, separators and the sign; drop currency symbols, spaces and apostrophes (thousands in Switzerland).
        var kept = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c) || c == '\'' || c == '’' || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.CurrencySymbol)
            {
                continue;
            }
            kept.Append(c == '−' ? '-' : c);
        }
        var s = StripCurrencyCode(kept.ToString());
        var negative = false;
        if (s.Length > 0 && (s[0] == '-' || s[0] == '+'))
        {
            negative = s[0] == '-';
            s = StripCurrencyCode(s[1..]);
        }
        if (s.Length == 0)
        {
            return false;
        }
        foreach (var c in s)
        {
            if (!IsAsciiDigit(c) && c != '.' && c != ',')
            {
                return false;
            }
        }
        var decimalAt = DecimalSeparatorIndex(s);
        var integer = decimalAt >= 0 ? s[..decimalAt] : s;
        var fraction = decimalAt >= 0 ? s[(decimalAt + 1)..] : string.Empty;
        if (decimalAt >= 0 && fraction.Length == 0)
        {
            return false;
        }
        if (!TryUngroup(integer, out var digits))
        {
            return false;
        }
        var normalized = (negative ? "-" : string.Empty) + (digits.Length == 0 ? "0" : digits) + (fraction.Length > 0 ? "." + fraction : string.Empty);
        return decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Where the decimal point is in digits and separators, or -1 when every separator groups thousands.</summary>
    private static int DecimalSeparatorIndex(string s)
    {
        var last = s.LastIndexOfAny(['.', ',']);
        if (last < 0)
        {
            return -1;
        }
        var separator = s[last];
        var other = separator == '.' ? ',' : '.';
        if (s.IndexOf(separator) != last)
        {
            // The same separator twice: it groups thousands (a decimal point appears once).
            return -1;
        }
        if (s.IndexOf(other) >= 0)
        {
            // "1.400,50": the last of two different separators is the decimal point.
            return last;
        }
        var after = s.Length - last - 1;
        if (after is 1 or 2)
        {
            return last;
        }
        // A single separator with three digits after it groups thousands ("1,400") unless what comes before cannot start
        // a group ("0.125" or ".125"); any other count of digits makes it a decimal point ("3.14159", "12.").
        var before = last;
        return after == 3 && before is >= 1 and <= 3 && s[0] != '0' ? -1 : last;
    }

    /// <summary>Removes thousands separators from the integer part; false when the groups are malformed.</summary>
    private static bool TryUngroup(string integer, out string digits)
    {
        digits = integer;
        if (integer.IndexOfAny(['.', ',']) < 0)
        {
            return true;
        }
        var groups = integer.Split('.', ',');
        if (groups[0].Length is < 1 or > 3)
        {
            return false;
        }
        for (var i = 1; i < groups.Length; i++)
        {
            // The last group has three digits; groups before it may have two (1,23,456) or three.
            var ok = i == groups.Length - 1 ? groups[i].Length == 3 : groups[i].Length is 2 or 3;
            if (!ok)
            {
                return false;
            }
        }
        digits = string.Concat(groups);
        return true;
    }

    /// <summary>Drops a three-letter code written before or after the amount ("EUR 2,686.20", "24.00 EUR").</summary>
    private static string StripCurrencyCode(string s)
    {
        if (s.Length > 3 && IsAsciiLetter(s[0]) && IsAsciiLetter(s[1]) && IsAsciiLetter(s[2]) && !IsAsciiLetter(s[3]))
        {
            s = s[3..];
        }
        var n = s.Length;
        if (n > 3 && IsAsciiLetter(s[n - 1]) && IsAsciiLetter(s[n - 2]) && IsAsciiLetter(s[n - 3]) && !IsAsciiLetter(s[n - 4]))
        {
            s = s[..(n - 3)];
        }
        return s;
    }

    private static bool IsCurrencyCode(string text) =>
        text.Length == 3 && text[0] is >= 'A' and <= 'Z' && text[1] is >= 'A' and <= 'Z' && text[2] is >= 'A' and <= 'Z';

    private static bool TryParseIban(string text, out string iban)
    {
        var compact = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (!char.IsWhiteSpace(c))
            {
                compact.Append(char.ToUpperInvariant(c));
            }
        }
        iban = compact.ToString();
        if (iban.Length is < MinIbanLength or > MaxIbanLength || !IsAsciiUpper(iban[0]) || !IsAsciiUpper(iban[1]) ||
            !IsAsciiDigit(iban[2]) || !IsAsciiDigit(iban[3]))
        {
            return false;
        }
        var check = Digits(iban, 2, 2);
        if (check is < 2 or > 98)
        {
            return false;
        }
        for (var i = 4; i < iban.Length; i++)
        {
            if (!IsAsciiUpper(iban[i]) && !IsAsciiDigit(iban[i]))
            {
                return false;
            }
        }
        // ISO 7064 mod 97-10: the first four characters move to the end, letters become 10..35, the number mod 97 is 1.
        var remainder = 0;
        for (var i = 0; i < iban.Length; i++)
        {
            var c = iban[(i + 4) % iban.Length];
            remainder = IsAsciiDigit(c) ? (remainder * 10 + (c - '0')) % 97 : (remainder * 100 + (c - 'A' + 10)) % 97;
        }
        return remainder == 1;
    }

    private static int Digits(string text, int start, int count)
    {
        var value = 0;
        for (var i = start; i < start + count; i++)
        {
            value = value * 10 + (text[i] - '0');
        }
        return value;
    }

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    private static bool IsAsciiUpper(char c) => c is >= 'A' and <= 'Z';

    private static bool IsAsciiLetter(char c) => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
