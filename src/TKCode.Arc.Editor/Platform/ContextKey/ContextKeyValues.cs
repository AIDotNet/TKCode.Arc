// Adaptation of the JavaScript value semantics that vscode's context keys rely on (src/vs/platform/contextkey/common/
// contextkey.ts and src/vs/base/common/objects.ts: equals, commit b57d5c9e), MIT license.
//
// A context key value is an object?: null (JavaScript's undefined; JavaScript's null is not told apart from it), bool,
// a number (any CLR numeric type, read as a double), string, an array (any System.Collections.IList) or a record (any
// System.Collections.IDictionary). Upstream evaluates expressions with JavaScript's operators; these helpers give the
// same answers for those values:
//  - IsTruthy is `!!value`;
//  - LooseEquals is `==` (null only equals null; a bool or a string compared with a number becomes a number; an array
//    or record compared with a primitive becomes its string, an array's elements joined with commas);
//  - StrictEquals is `===` (arrays and records by reference) and SameValueZero is what Array.prototype.includes uses;
//  - DeepEquals is objects.equals (arrays element by element, records key by key);
//  - ToJsString is String(value) (null is "undefined"; numbers use JavaScript's shortest round-trip form);
//  - ToNumber is Number(value) and ParseFloat is parseFloat(value);
//  - LessThan is `<` (two strings compare by UTF-16 code units, anything else as numbers).

using System.Collections;
using System.Globalization;
using System.Text;

namespace TKCode.Arc.Editor.Platform;

/// <summary>JavaScript's operators over context key values.</summary>
internal static class ContextKeyValues
{
    /// <summary>Whether <paramref name="value"/> is a number, and which.</summary>
    public static bool TryGetNumber(object? value, out double number)
    {
        switch (value)
        {
            case double d:
                number = d;
                return true;
            case int i:
                number = i;
                return true;
            case long l:
                number = l;
                return true;
            case float f:
                number = f;
                return true;
            case decimal m:
                number = (double)m;
                return true;
            case short s:
                number = s;
                return true;
            case byte b:
                number = b;
                return true;
            case uint ui:
                number = ui;
                return true;
            case ulong ul:
                number = ul;
                return true;
            case ushort us:
                number = us;
                return true;
            case sbyte sb:
                number = sb;
                return true;
            default:
                number = double.NaN;
                return false;
        }
    }

    private static bool IsObject(object? value) => value is IList or IDictionary;

    /// <summary><c>!!value</c>.</summary>
    public static bool IsTruthy(object? value)
    {
        switch (value)
        {
            case null:
                return false;
            case bool b:
                return b;
            case string s:
                return s.Length > 0;
        }
        if (TryGetNumber(value, out var n))
        {
            return n != 0 && !double.IsNaN(n);
        }
        return true;
    }

    /// <summary><c>a === b</c>.</summary>
    public static bool StrictEquals(object? a, object? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }
        if (TryGetNumber(a, out var na))
        {
            return TryGetNumber(b, out var nb) && na == nb;
        }
        if (a is string sa)
        {
            return b is string sb && string.Equals(sa, sb, StringComparison.Ordinal);
        }
        if (a is bool ba)
        {
            return b is bool bb && ba == bb;
        }
        if (IsObject(a))
        {
            return ReferenceEquals(a, b);
        }
        return a.Equals(b);
    }

    /// <summary>SameValueZero, the comparison of <c>Array.prototype.includes</c>: <c>===</c>, except that NaN equals NaN.</summary>
    public static bool SameValueZero(object? a, object? b)
    {
        if (TryGetNumber(a, out var na) && TryGetNumber(b, out var nb) && double.IsNaN(na) && double.IsNaN(nb))
        {
            return true;
        }
        return StrictEquals(a, b);
    }

    /// <summary><c>a == b</c>.</summary>
    public static bool LooseEquals(object? a, object? b)
    {
        while (true)
        {
            if (a is null || b is null)
            {
                return a is null && b is null;
            }

            var aIsNumber = TryGetNumber(a, out var na);
            var bIsNumber = TryGetNumber(b, out var nb);
            if (aIsNumber && bIsNumber)
            {
                return na == nb;
            }
            if (a is string sa && b is string sb)
            {
                return string.Equals(sa, sb, StringComparison.Ordinal);
            }
            if (a is bool ba && b is bool bb)
            {
                return ba == bb;
            }
            if (IsObject(a) && IsObject(b))
            {
                return ReferenceEquals(a, b);
            }

            // different types: booleans become numbers, objects become primitives, strings become numbers
            if (a is bool ab)
            {
                a = ab ? 1d : 0d;
                continue;
            }
            if (b is bool bb2)
            {
                b = bb2 ? 1d : 0d;
                continue;
            }
            if (IsObject(a))
            {
                a = ToJsString(a);
                continue;
            }
            if (IsObject(b))
            {
                b = ToJsString(b);
                continue;
            }
            if (aIsNumber && b is string)
            {
                return na == ToNumber(b);
            }
            if (bIsNumber && a is string)
            {
                return ToNumber(a) == nb;
            }
            return a.Equals(b);
        }
    }

    /// <summary><c>objects.equals(one, other)</c>: a deep comparison of arrays and records.</summary>
    public static bool DeepEquals(object? one, object? other)
    {
        if (StrictEquals(one, other))
        {
            return true;
        }
        if (one is null || other is null)
        {
            return false;
        }
        if (one is IList listOne)
        {
            if (other is not IList listOther || listOne.Count != listOther.Count)
            {
                return false;
            }
            for (var i = 0; i < listOne.Count; i++)
            {
                if (!DeepEquals(listOne[i], listOther[i]))
                {
                    return false;
                }
            }
            return true;
        }
        if (one is IDictionary dictOne)
        {
            if (other is not IDictionary dictOther || other is IList || dictOne.Count != dictOther.Count)
            {
                return false;
            }
            foreach (DictionaryEntry entry in dictOne)
            {
                if (!dictOther.Contains(entry.Key) || !DeepEquals(entry.Value, dictOther[entry.Key]))
                {
                    return false;
                }
            }
            return true;
        }
        return false;
    }

    /// <summary><c>a &lt; b</c>.</summary>
    public static bool LessThan(object? a, object? b)
    {
        var pa = ToPrimitive(a);
        var pb = ToPrimitive(b);
        if (pa is string sa && pb is string sb)
        {
            return string.CompareOrdinal(sa, sb) < 0;
        }
        return ToNumber(pa) < ToNumber(pb);
    }

    private static object? ToPrimitive(object? value) => IsObject(value) ? ToJsString(value) : value;

    /// <summary><c>String(value)</c>.</summary>
    public static string ToJsString(object? value)
    {
        switch (value)
        {
            case null:
                return "undefined";
            case string s:
                return s;
            case bool b:
                return b ? "true" : "false";
            case IList list:
            {
                var builder = new StringBuilder();
                for (var i = 0; i < list.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }
                    if (list[i] is { } element)
                    {
                        builder.Append(ToJsString(element));
                    }
                }
                return builder.ToString();
            }
            case IDictionary:
                return "[object Object]";
        }
        if (TryGetNumber(value, out var n))
        {
            return NumberToString(n);
        }
        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }

    /// <summary>A number the way JavaScript prints it.</summary>
    public static string NumberToString(double n)
    {
        if (double.IsNaN(n))
        {
            return "NaN";
        }
        if (double.IsPositiveInfinity(n))
        {
            return "Infinity";
        }
        if (double.IsNegativeInfinity(n))
        {
            return "-Infinity";
        }
        if (n == 0)
        {
            return "0";
        }

        var abs = Math.Abs(n);
        if (abs >= 1e-6 && abs < 1e21)
        {
            // shortest round-trip digits without an exponent
            var text = n.ToString("R", CultureInfo.InvariantCulture);
            if (!text.Contains('E', StringComparison.Ordinal))
            {
                return text;
            }
            return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var dec)
                ? dec.ToString(CultureInfo.InvariantCulture)
                : text;
        }

        var exp = n.ToString("R", CultureInfo.InvariantCulture);
        var e = exp.IndexOf('E', StringComparison.Ordinal);
        if (e < 0)
        {
            return exp;
        }
        var mantissa = exp.Substring(0, e);
        var exponent = int.Parse(exp.AsSpan(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        return mantissa + "e" + (exponent >= 0 ? "+" : "-") + Math.Abs(exponent).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary><c>Number(value)</c>.</summary>
    public static double ToNumber(object? value)
    {
        switch (value)
        {
            case null:
                return double.NaN;
            case bool b:
                return b ? 1 : 0;
            case string s:
                return StringToNumber(s);
        }
        if (TryGetNumber(value, out var n))
        {
            return n;
        }
        return StringToNumber(ToJsString(value));
    }

    private static double StringToNumber(string s)
    {
        var text = s.AsSpan().Trim(JsWhitespace);
        if (text.Length == 0)
        {
            return 0;
        }
        if (text.Length > 2 && text[0] == '0')
        {
            var radix = text[1] switch
            {
                'x' or 'X' => 16,
                'o' or 'O' => 8,
                'b' or 'B' => 2,
                _ => 0,
            };
            if (radix != 0)
            {
                double result = 0;
                foreach (var ch in text[2..])
                {
                    var digit = HexDigitValue(ch);
                    if (digit < 0 || digit >= radix)
                    {
                        return double.NaN;
                    }
                    result = result * radix + digit;
                }
                return result;
            }
        }
        var length = DecimalLiteralLength(text);
        return length == text.Length ? ParseDecimalLiteral(text) : double.NaN;
    }

    /// <summary><c>parseFloat(value)</c>: the longest decimal literal at the start of <c>String(value)</c>.</summary>
    public static double ParseFloat(object? value)
    {
        if (TryGetNumber(value, out var n))
        {
            return n;
        }
        var text = ToJsString(value).AsSpan().TrimStart(JsWhitespace);
        var length = DecimalLiteralLength(text);
        return length == 0 ? double.NaN : ParseDecimalLiteral(text[..length]);
    }

    // JavaScript's WhiteSpace and LineTerminator characters (what String.prototype.trim removes).
    private const string JsWhitespace =
        "\t\n\v\f\r \u00A0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF";

    private static int HexDigitValue(char ch)
    {
        return ch switch
        {
            >= '0' and <= '9' => ch - '0',
            >= 'a' and <= 'f' => ch - 'a' + 10,
            >= 'A' and <= 'F' => ch - 'A' + 10,
            _ => -1,
        };
    }

    // The length of the StrDecimalLiteral at the start of text: [+-] (Infinity | digits [. digits] [e [+-] digits] | . digits ...).
    private static int DecimalLiteralLength(ReadOnlySpan<char> text)
    {
        var i = 0;
        if (i < text.Length && text[i] is '+' or '-')
        {
            i++;
        }
        if (text[i..].StartsWith("Infinity", StringComparison.Ordinal))
        {
            return i + "Infinity".Length;
        }

        var digits = 0;
        while (i < text.Length && char.IsAsciiDigit(text[i]))
        {
            i++;
            digits++;
        }
        if (i < text.Length && text[i] == '.')
        {
            var afterDot = i + 1;
            var fraction = 0;
            while (afterDot < text.Length && char.IsAsciiDigit(text[afterDot]))
            {
                afterDot++;
                fraction++;
            }
            if (digits + fraction > 0)
            {
                i = afterDot;
                digits += fraction;
            }
        }
        if (digits == 0)
        {
            return 0;
        }
        if (i < text.Length && text[i] is 'e' or 'E')
        {
            var j = i + 1;
            if (j < text.Length && text[j] is '+' or '-')
            {
                j++;
            }
            var expDigits = 0;
            while (j < text.Length && char.IsAsciiDigit(text[j]))
            {
                j++;
                expDigits++;
            }
            if (expDigits > 0)
            {
                i = j;
            }
        }
        return i;
    }

    private static double ParseDecimalLiteral(ReadOnlySpan<char> literal)
    {
        if (literal.EndsWith("Infinity", StringComparison.Ordinal))
        {
            return literal[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
        }
        return double.Parse(literal, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
