using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TKCode.Arc;

/// <summary>One character column of the painted amount: keyed by place value so 9 → 10 keeps the ones column.</summary>
/// <param name="Rank">Column key and order: <c>i&lt;p&gt;</c> = -2p, <c>g&lt;p&gt;</c> = -2p + 1, <c>d</c> = 1000, <c>f&lt;n&gt;</c> = 1001 + n.</param>
/// <param name="Char">The character shown.</param>
/// <param name="Ghost">Missing cents and the "0" of an empty amount wait as faint ghosts that hold their width.</param>
internal readonly record struct MoneyInputPart(int Rank, char Char, bool Ghost);

/// <summary>
/// Arc's draft model ported from <c>money-input.tsx</c>: a draft is the amount as typed in a canonical form (digits with
/// an optional ".", such as "1234.5"); the display adds grouping and the culture's decimal; the caret is restored after
/// the same count of significant characters.
/// </summary>
internal static partial class MoneyInputDraft
{
    /// <summary>At most 12 integer digits (Arc <c>MAX_INTEGER_DIGITS</c>).</summary>
    public const int MaxIntegerDigits = 12;

    public static decimal Pow10(int digits)
    {
        var result = 1m;
        for (var i = 0; i < digits; i++)
        {
            result *= 10;
        }
        return result;
    }

    /// <summary>Arc <c>toMinor</c>: the draft in minor units, null when empty.</summary>
    public static decimal? ToMinor(string draft, int digits)
    {
        if (string.IsNullOrEmpty(draft))
        {
            return null;
        }
        var dot = draft.IndexOf('.');
        var whole = dot < 0 ? draft : draft[..dot];
        var fraction = dot < 0 ? string.Empty : draft[(dot + 1)..];
        var wholeValue = whole.Length == 0 ? 0m : decimal.Parse(whole, NumberStyles.None, CultureInfo.InvariantCulture);
        var padded = fraction.PadRight(digits, '0')[..digits];
        var fractionValue = padded.Length == 0 ? 0m : decimal.Parse(padded, NumberStyles.None, CultureInfo.InvariantCulture);
        return wholeValue * Pow10(digits) + fractionValue;
    }

    /// <summary>Arc <c>fromMinor</c>: a settled draft from minor units ("" for null); the sign is dropped.</summary>
    public static string FromMinor(decimal? minor, int digits)
    {
        if (minor is not { } value)
        {
            return string.Empty;
        }
        var text = Math.Round(Math.Abs(value), 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)
                       .PadLeft(digits + 1, '0');
        return digits > 0 ? $"{text[..^digits]}.{text[^digits..]}" : text;
    }

    /// <summary>Arc <c>settle</c>: fraction padded to the currency, leading zeros gone.</summary>
    public static string Settle(string draft, int digits) => string.IsNullOrEmpty(draft) ? string.Empty : FromMinor(ToMinor(draft, digits), digits);

    /// <summary>Arc <c>convertDraft</c>: carries an amount across currencies with different minor units, keeping the major amount.</summary>
    public static string Convert(string draft, int digits)
    {
        if (string.IsNullOrEmpty(draft))
        {
            return string.Empty;
        }
        var dot = draft.IndexOf('.');
        var whole = dot < 0 ? draft : draft[..dot];
        if (digits == 0)
        {
            return whole.Length > 0 ? whole : "0";
        }
        if (dot < 0)
        {
            return whole;
        }
        var fraction = draft[(dot + 1)..];
        return $"{whole}.{(fraction.Length > digits ? fraction[..digits] : fraction)}";
    }

    /// <summary>Arc <c>displayOf</c>: the draft with three-digit groups and the culture's decimal.</summary>
    public static string Display(string draft, char group, char dec)
    {
        if (string.IsNullOrEmpty(draft))
        {
            return string.Empty;
        }
        var dot = draft.IndexOf('.');
        var whole = dot < 0 ? draft : draft[..dot];
        var builder = new StringBuilder(draft.Length + whole.Length / 3 + 1);
        for (var i = 0; i < whole.Length; i++)
        {
            builder.Append(whole[i]);
            var place = whole.Length - 1 - i;
            if (place > 0 && place % 3 == 0)
            {
                builder.Append(group);
            }
        }
        if (dot >= 0)
        {
            builder.Append(dec).Append(draft, dot + 1, draft.Length - dot - 1);
        }
        return builder.ToString();
    }

    /// <summary>Arc <c>partsOf</c>: the painted columns, ghosts included.</summary>
    public static void Parts(string draft, int digits, char group, char dec, List<MoneyInputPart> into)
    {
        into.Clear();
        var dot = string.IsNullOrEmpty(draft) ? -1 : draft.IndexOf('.');
        var whole = string.IsNullOrEmpty(draft) ? string.Empty : dot < 0 ? draft : draft[..dot];
        string? fraction = dot < 0 ? null : draft[(dot + 1)..];
        var ghostInt = whole.Length == 0;
        var intDigits = ghostInt ? "0" : whole;
        for (var i = 0; i < intDigits.Length; i++)
        {
            var place = intDigits.Length - 1 - i;
            into.Add(new MoneyInputPart(-2 * place, intDigits[i], ghostInt));
            if (place > 0 && place % 3 == 0)
            {
                into.Add(new MoneyInputPart(-2 * place + 1, group, ghostInt));
            }
        }
        if (digits > 0)
        {
            into.Add(new MoneyInputPart(1000, dec, fraction is null));
            for (var i = 0; i < digits; i++)
            {
                var has = fraction is not null && i < fraction.Length;
                into.Add(new MoneyInputPart(1001 + i, has ? fraction![i] : '0', !has));
            }
        }
    }

    /// <summary>Arc <c>isSignificant</c>: digits and the decimal count toward the caret position.</summary>
    public static bool IsSignificant(char c, char dec) => char.IsAsciiDigit(c) || c == dec;

    /// <summary>Arc <c>positionAfter</c>: the text index right after the <paramref name="count"/>th significant character.</summary>
    public static int PositionAfter(string text, int count, char dec)
    {
        if (count <= 0)
        {
            return 0;
        }
        var seen = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (IsSignificant(text[i], dec) && ++seen == count)
            {
                return i + 1;
            }
        }
        return text.Length;
    }

    /// <summary>
    /// Arc <c>sanitize</c>: reads what the editor holds into a draft and counts the digits and decimal before the caret,
    /// so the caret can return to the same spot. Null when the change is refused (too many integer or fraction digits).
    /// </summary>
    public static (string Draft, int Before)? Sanitize(string raw, int caret, char group, char dec, int digits)
    {
        var builder = new StringBuilder(raw.Length);
        var before = 0;
        var dot = false;
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            char kept;
            if (char.IsAsciiDigit(c))
            {
                kept = c;
            }
            else if ((c == dec || (c == '.' && group != '.')) && digits > 0 && !dot)
            {
                kept = '.';
                dot = true;
            }
            else
            {
                continue;
            }
            builder.Append(kept);
            if (i < caret)
            {
                before++;
            }
        }
        var output = builder.ToString();
        var dotAt = output.IndexOf('.');
        var whole = dotAt < 0 ? output : output[..dotAt];
        string? fraction = dotAt < 0 ? null : output[(dotAt + 1)..];
        var trimmed = whole.TrimStart('0');
        var stripped = whole.Length - trimmed.Length;
        whole = trimmed;
        var shift = -Math.Min(stripped, before);
        if (fraction is not null && whole.Length == 0)
        {
            whole = "0";
            shift += 1;
        }
        if (fraction is null && whole.Length == 0 && stripped > 0)
        {
            whole = "0";
            shift += 1;
        }
        if (fraction is not null && fraction.Length > digits)
        {
            return null;
        }
        if (whole.Length > MaxIntegerDigits)
        {
            return null;
        }
        return (fraction is not null ? $"{whole}.{fraction}" : whole, Math.Max(0, before + shift));
    }

    /// <summary>A refused keystroke keeps the caret where it was: one significant character before where the edit left it.</summary>
    public static int RefusedCaret(string raw, int caret, char dec)
    {
        var count = 0;
        for (var i = 0; i < Math.Min(caret, raw.Length); i++)
        {
            if (IsSignificant(raw[i], dec) || raw[i] == '.')
            {
                count++;
            }
        }
        return Math.Max(0, count - 1);
    }

    /// <summary>
    /// Arc's paste parsing: "$1,234.56", "1.234,56 €", "1234.5". A final separator followed by one or two digits is the
    /// decimal; the rest are groups. Returns the canonical pasted amount ("1234.56") or null when it holds no digit.
    /// </summary>
    public static string? ParsePaste(string text, int digits)
    {
        text = text.Trim();
        if (text.Length == 0)
        {
            return null;
        }
        var match = FinalDecimal().Match(text);
        var wholeSource = match.Success ? text[..match.Index] : text;
        var whole = new StringBuilder(wholeSource.Length);
        foreach (var c in wholeSource)
        {
            if (char.IsAsciiDigit(c))
            {
                whole.Append(c);
            }
        }
        var pasted = match.Success && digits > 0 ? $"{whole}.{match.Groups[1].Value}" : whole.ToString();
        foreach (var c in pasted)
        {
            if (char.IsAsciiDigit(c))
            {
                return pasted;
            }
        }
        return null;
    }

    [GeneratedRegex(@"[.,]([0-9]{1,2})\s*[^0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex FinalDecimal();
}
