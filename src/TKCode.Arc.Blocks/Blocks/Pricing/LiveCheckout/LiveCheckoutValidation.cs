using System.Text;
using System.Text.RegularExpressions;

namespace TKCode.Arc;

/// <summary>The fields of the checkout form, in form order (the order a failed submit searches for the first error).</summary>
internal enum LiveCheckoutField
{
    Email,
    Name,
    Address,
    City,
    PostalCode,
    CardNumber,
    Expiry,
    Cvc
}

/// <summary>Why a field is invalid.</summary>
internal enum LiveCheckoutFieldError
{
    None,
    EmailMissing,
    EmailInvalid,
    NameMissing,
    AddressMissing,
    CityMissing,
    PostalMissing,
    PostalMismatch,
    CardMissing,
    CardShort,
    CardInvalid,
    ExpiryMissing,
    ExpiryIncomplete,
    ExpiryMonth,
    ExpiryPast,
    CvcThree,
    CvcFour
}

/// <summary>
/// Field rules and input shaping of the checkout (Arc LiveCheckout, design reference; each rule as measured): email, name
/// (two characters), address (three), city, postal code by country, card number (grouping by network, Luhn), expiry
/// (zero padding and the slash typed for you, month and past checks) and the security code.
/// </summary>
internal static partial class LiveCheckoutValidation
{
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    public static LiveCheckoutFieldError Email(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return LiveCheckoutFieldError.EmailMissing;
        }
        return EmailRegex().IsMatch(text) ? LiveCheckoutFieldError.None : LiveCheckoutFieldError.EmailInvalid;
    }

    public static LiveCheckoutFieldError Name(string? value)
        => (value?.Trim().Length ?? 0) >= 2 ? LiveCheckoutFieldError.None : LiveCheckoutFieldError.NameMissing;

    public static LiveCheckoutFieldError Address(string? value)
        => (value?.Trim().Length ?? 0) >= 3 ? LiveCheckoutFieldError.None : LiveCheckoutFieldError.AddressMissing;

    public static LiveCheckoutFieldError City(string? value)
        => (value?.Trim().Length ?? 0) >= 1 ? LiveCheckoutFieldError.None : LiveCheckoutFieldError.CityMissing;

    public static LiveCheckoutFieldError Postal(string? value, LiveCheckoutCountry? country)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return LiveCheckoutFieldError.PostalMissing;
        }
        return country is null || country.MatchesPostal(text) ? LiveCheckoutFieldError.None : LiveCheckoutFieldError.PostalMismatch;
    }

    /// <summary>The digits of <paramref name="value"/>.</summary>
    public static string Digits(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c is >= '0' and <= '9')
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }

    /// <summary>The network of a card number from its first digits.</summary>
    public static LiveCheckoutCardBrand Brand(string? number)
    {
        var digits = Digits(number);
        if (digits.Length == 0)
        {
            return LiveCheckoutCardBrand.None;
        }
        if (digits[0] == '4')
        {
            return LiveCheckoutCardBrand.Novacard;
        }
        if (digits.StartsWith("34", StringComparison.Ordinal) || digits.StartsWith("37", StringComparison.Ordinal))
        {
            return LiveCheckoutCardBrand.Atlas;
        }
        if (digits[0] == '5')
        {
            return LiveCheckoutCardBrand.Meridian;
        }
        if (digits.Length >= 4 && int.TryParse(digits.AsSpan(0, 4), out var head) && head is >= 2221 and <= 2720)
        {
            return LiveCheckoutCardBrand.Meridian;
        }
        return LiveCheckoutCardBrand.None;
    }

    /// <summary>Digits a complete number of this network has.</summary>
    public static int CardLength(LiveCheckoutCardBrand brand) => brand == LiveCheckoutCardBrand.Atlas ? 15 : 16;

    /// <summary>Digits a complete security code of this network has.</summary>
    public static int CvcLength(LiveCheckoutCardBrand brand) => brand == LiveCheckoutCardBrand.Atlas ? 4 : 3;

    /// <summary>A card number grouped for its network (4-4-4-4, or 4-6-5 for Atlas), cut to its length.</summary>
    public static string FormatCard(string? value)
    {
        var digits = Digits(value);
        var brand = Brand(digits);
        var max = CardLength(brand);
        if (digits.Length > max)
        {
            digits = digits[..max];
        }
        ReadOnlySpan<int> groups = brand == LiveCheckoutCardBrand.Atlas ? [4, 6, 5] : [4, 4, 4, 4];
        var builder = new StringBuilder(digits.Length + 3);
        var at = 0;
        foreach (var size in groups)
        {
            if (at >= digits.Length)
            {
                break;
            }
            if (at > 0)
            {
                builder.Append(' ');
            }
            var take = Math.Min(size, digits.Length - at);
            builder.Append(digits, at, take);
            at += take;
        }
        return builder.ToString();
    }

    /// <summary>Luhn checksum of a digit string.</summary>
    public static bool Luhn(string digits)
    {
        var sum = 0;
        var doubled = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubled)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }
            sum += d;
            doubled = !doubled;
        }
        return digits.Length > 0 && sum % 10 == 0;
    }

    public static LiveCheckoutFieldError Card(string? value)
    {
        var digits = Digits(value);
        if (digits.Length == 0)
        {
            return LiveCheckoutFieldError.CardMissing;
        }
        if (digits.Length < CardLength(Brand(digits)))
        {
            return LiveCheckoutFieldError.CardShort;
        }
        return Luhn(digits) ? LiveCheckoutFieldError.None : LiveCheckoutFieldError.CardInvalid;
    }

    /// <summary>
    /// Shapes what was typed in the expiry field: digits only, at most four; a first digit of 2–9 becomes 0d; the slash is
    /// added after the month while typing forward and dropped with the month's last digit when deleting.
    /// </summary>
    public static string FormatExpiry(string? value, string? previous)
    {
        var digits = Digits(value);
        if (digits.Length > 4)
        {
            digits = digits[..4];
        }
        if (digits.Length == 1 && digits[0] >= '2')
        {
            digits = "0" + digits;
        }
        var deleting = (value?.Length ?? 0) < (previous?.Length ?? 0);
        if (digits.Length > 2)
        {
            return digits[..2] + "/" + digits[2..];
        }
        if (digits.Length == 2 && !deleting)
        {
            return digits + "/";
        }
        return digits;
    }

    public static LiveCheckoutFieldError Expiry(string? value, DateTime today)
    {
        var digits = Digits(value);
        if (digits.Length == 0)
        {
            return LiveCheckoutFieldError.ExpiryMissing;
        }
        if (digits.Length < 4)
        {
            return LiveCheckoutFieldError.ExpiryIncomplete;
        }
        var month = int.Parse(digits.AsSpan(0, 2));
        var year = 2000 + int.Parse(digits.AsSpan(2, 2));
        if (month is < 1 or > 12)
        {
            return LiveCheckoutFieldError.ExpiryMonth;
        }
        // The card works through the end of its month: 11/26 is still good during November 2026.
        return year * 12 + month < today.Year * 12 + today.Month ? LiveCheckoutFieldError.ExpiryPast : LiveCheckoutFieldError.None;
    }

    /// <summary>The security code: digits only, as many as the network uses.</summary>
    public static string FormatCvc(string? value, LiveCheckoutCardBrand brand)
    {
        var digits = Digits(value);
        var max = CvcLength(brand);
        return digits.Length > max ? digits[..max] : digits;
    }

    public static LiveCheckoutFieldError Cvc(string? value, LiveCheckoutCardBrand brand)
    {
        var length = Digits(value).Length;
        if (length == CvcLength(brand))
        {
            return LiveCheckoutFieldError.None;
        }
        return brand == LiveCheckoutCardBrand.Atlas ? LiveCheckoutFieldError.CvcFour : LiveCheckoutFieldError.CvcThree;
    }

    /// <summary>Seats typed in the seat box: digits only, clamped at once; an empty box reads as the minimum.</summary>
    public static int ParseSeats(string? value, int min, int max)
    {
        var digits = Digits(value);
        if (digits.Length == 0)
        {
            return min;
        }
        if (digits.Length > 6 || !int.TryParse(digits, out var seats))
        {
            return max;
        }
        return Math.Clamp(seats, min, max);
    }

    /// <summary>Where the caret goes after reshaping: after the same number of digits it followed before.</summary>
    public static int CaretAfterDigits(string formatted, int digitsBefore)
    {
        if (digitsBefore <= 0)
        {
            return 0;
        }
        var seen = 0;
        for (var i = 0; i < formatted.Length; i++)
        {
            if (char.IsAsciiDigit(formatted[i]))
            {
                seen++;
                if (seen == digitsBefore)
                {
                    var at = i + 1;
                    // Step over a separator the formatter put right after this digit (a group space or the expiry slash).
                    while (at < formatted.Length && !char.IsAsciiDigit(formatted[at]) && at == formatted.Length - 1)
                    {
                        at++;
                    }
                    return at;
                }
            }
        }
        return formatted.Length;
    }

    /// <summary>Digits in <paramref name="text"/> before <paramref name="caret"/>.</summary>
    public static int DigitsBefore(string? text, int caret)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        var count = 0;
        for (var i = 0; i < Math.Min(caret, text.Length); i++)
        {
            if (char.IsAsciiDigit(text[i]))
            {
                count++;
            }
        }
        return count;
    }
}
