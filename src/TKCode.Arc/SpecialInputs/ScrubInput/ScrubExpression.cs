using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TKCode.Arc;

/// <summary>The result of <see cref="ScrubExpression.Evaluate"/>: the computed number and the unit it was typed with, if any.</summary>
/// <param name="Value">The finite result of the arithmetic.</param>
/// <param name="Unit">The unit typed after a number, as spelled in the <c>units</c> list (null when none was typed).</param>
public readonly record struct ScrubExpressionResult(double Value, string? Unit);

/// <summary>
/// The entry grammar of <see cref="ScrubInput"/> (Arc's <c>evaluateScrubExpression</c>): <c>+ - * /</c> and parentheses
/// over numbers that may carry a unit. A comma is a decimal point, <c>×</c> / <c>x</c> (before a number) multiply,
/// <c>÷</c> divides and <c>−</c> subtracts; <c>deg</c> and <c>°</c> are aliases. Anything else, an unknown unit or a
/// non-finite result gives null.
/// </summary>
public static partial class ScrubExpression
{
    // Deeper nesting than this is treated as junk instead of risking a stack overflow (Arc recurses without a bound).
    private const int MaxDepth = 256;

    [GeneratedRegex(@"[×xX](?=\s*[0-9(.])")]
    private static partial Regex TimesRegex();

    [GeneratedRegex(@"[0-9]*\.?[0-9]+(?:[eE][+-]?[0-9]+)?|[a-zA-Z%°]+|[-+*/()]|\S")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"^[0-9]*\.?[0-9]+")]
    private static partial Regex NumberStartRegex();

    [GeneratedRegex(@"^[a-zA-Z%°]+$")]
    private static partial Regex WordRegex();

    /// <summary>
    /// Evaluates <paramref name="text"/>. Returns null for an empty or malformed entry, an unknown unit, or a result
    /// that is not finite (division by zero).
    /// </summary>
    /// <param name="text">The typed entry, for example <c>12*2</c>, <c>(100-24)/2</c>, <c>50%</c> or <c>1,5em</c>.</param>
    /// <param name="units">Units an entry may carry; matching ignores case.</param>
    public static ScrubExpressionResult? Evaluate(string text, IReadOnlyList<string> units)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(units);
        var source = text.Trim().Replace(',', '.');
        source = TimesRegex().Replace(source, "*").Replace('÷', '/').Replace('−', '-');
        if (source.Length == 0)
        {
            return null;
        }
        var matches = TokenRegex().Matches(source);
        if (matches.Count == 0)
        {
            return null;
        }
        var tokens = new string[matches.Count];
        for (var i = 0; i < tokens.Length; i++)
        {
            tokens[i] = matches[i].Value;
        }
        var parser = new Parser(tokens, units);
        var value = parser.Sum(0);
        if (parser.Bad || parser.Index != tokens.Length || !double.IsFinite(value))
        {
            return null;
        }
        return new ScrubExpressionResult(value, parser.Unit);
    }

    private sealed class Parser
    {
        private readonly string[] _tokens;
        private readonly IReadOnlyList<string> _units;
        private readonly string[] _known;

        public Parser(string[] tokens, IReadOnlyList<string> units)
        {
            _tokens = tokens;
            _units = units;
            _known = new string[units.Count];
            for (var i = 0; i < units.Count; i++)
            {
                _known[i] = units[i].ToLowerInvariant();
            }
        }

        public int Index;
        public bool Bad;
        public string? Unit;

        private string? Peek() => Index < _tokens.Length ? _tokens[Index] : null;

        private string? Next()
        {
            var token = Index < _tokens.Length ? _tokens[Index] : null;
            Index++;
            return token;
        }

        private double Primary(int depth)
        {
            if (depth > MaxDepth)
            {
                Bad = true;
                return 0;
            }
            var token = Next();
            if (token == "(")
            {
                var inner = Sum(depth + 1);
                if (Next() != ")")
                {
                    Bad = true;
                }
                return inner;
            }
            if (token == "-")
            {
                return -Primary(depth + 1);
            }
            if (token == "+")
            {
                return Primary(depth + 1);
            }
            if (token is null || !NumberStartRegex().IsMatch(token))
            {
                Bad = true;
                return 0;
            }
            var number = double.Parse(token, NumberStyles.Float, CultureInfo.InvariantCulture);
            var next = Peek();
            if (next is not null && WordRegex().IsMatch(next))
            {
                Index++;
                var word = next.ToLowerInvariant();
                var alias = word == "°" ? "deg" : word == "deg" ? "°" : word;
                var found = Array.IndexOf(_known, word);
                if (found < 0)
                {
                    found = Array.IndexOf(_known, alias);
                }
                if (found < 0)
                {
                    Bad = true;
                }
                else
                {
                    Unit = _units[found];
                }
            }
            return number;
        }

        private double Product(int depth)
        {
            var left = Primary(depth);
            while (Peek() is "*" or "/")
            {
                var op = Next();
                var right = Primary(depth);
                left = op == "*" ? left * right : left / right;
            }
            return left;
        }

        public double Sum(int depth)
        {
            var left = Product(depth);
            while (Peek() is "+" or "-")
            {
                var op = Next();
                var right = Product(depth);
                left = op == "+" ? left + right : left - right;
            }
            return left;
        }
    }
}

/// <summary>Arc's number helpers for <see cref="ScrubInput"/>, with JavaScript number semantics.</summary>
internal static class ScrubInputNumber
{
    /// <summary>JavaScript <c>String(number)</c>: shortest round-trip digits, exponent form below 1e-6 and from 1e21.</summary>
    public static string ToJsString(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }
        if (double.IsInfinity(value))
        {
            return value > 0 ? "Infinity" : "-Infinity";
        }
        if (value == 0)
        {
            return "0";
        }
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var negative = text[0] == '-';
        if (negative)
        {
            text = text.Substring(1);
        }
        var exponent = 0;
        var e = text.IndexOfAny(['E', 'e']);
        var mantissa = text;
        if (e >= 0)
        {
            exponent = int.Parse(text.AsSpan(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            mantissa = text.Substring(0, e);
        }
        var dot = mantissa.IndexOf('.');
        var integral = dot >= 0 ? mantissa.Substring(0, dot) : mantissa;
        var fraction = dot >= 0 ? mantissa.Substring(dot + 1) : string.Empty;
        var all = integral + fraction;
        var trimmed = all.TrimStart('0');
        var n = integral.Length + exponent - (all.Length - trimmed.Length);
        var digits = trimmed.TrimEnd('0');
        var k = digits.Length;
        var builder = new StringBuilder(k + 8);
        if (negative)
        {
            builder.Append('-');
        }
        if (k <= n && n <= 21)
        {
            builder.Append(digits).Append('0', n - k);
        }
        else if (0 < n && n <= 21)
        {
            builder.Append(digits, 0, n).Append('.').Append(digits, n, k - n);
        }
        else if (-6 < n && n <= 0)
        {
            builder.Append("0.").Append('0', -n).Append(digits);
        }
        else
        {
            var exp = n - 1;
            builder.Append(digits[0]);
            if (k > 1)
            {
                builder.Append('.').Append(digits, 1, k - 1);
            }
            builder.Append('e').Append(exp < 0 ? '-' : '+').Append(Math.Abs(exp).ToString(CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }

    /// <summary>Arc's <c>decimalsOf</c>: decimal places of <c>String(value)</c> (the exponent for <c>1e-7</c>).</summary>
    public static int DecimalsOf(double value)
    {
        var text = ToJsString(value);
        var e = text.IndexOf("e-", StringComparison.Ordinal);
        if (e >= 0)
        {
            return int.Parse(text.AsSpan(e + 2), NumberStyles.None, CultureInfo.InvariantCulture);
        }
        var dot = text.IndexOf('.');
        return dot < 0 ? 0 : text.Length - dot - 1;
    }

    /// <summary>JavaScript <c>Math.round</c>: halves round toward positive infinity.</summary>
    public static double JsRound(double value)
    {
        if (!double.IsFinite(value))
        {
            return value;
        }
        var floor = Math.Floor(value);
        return value - floor >= 0.5 ? floor + 1 : floor;
    }

    /// <summary>Arc's <c>roundTo</c>: <c>Math.round(value * 10^places) / 10^places</c>.</summary>
    public static double RoundTo(double value, int places)
    {
        var factor = Math.Pow(10, places);
        if (!double.IsFinite(factor) || factor == 0)
        {
            return value;
        }
        return JsRound(value * factor) / factor;
    }

    /// <summary>Arc's <c>format</c>: the rounded value as JavaScript prints it, with "-0" shown as "0".</summary>
    public static string Format(double value, int places)
    {
        var text = ToJsString(RoundTo(value, places));
        return text == "-0" ? "0" : text;
    }
}
