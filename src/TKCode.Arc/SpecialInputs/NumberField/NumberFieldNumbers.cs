using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// The number model of <see cref="NumberField"/>, ported from Arc's <c>number-field.tsx</c>: precision, grid snapping,
/// stepping from off-grid values, formatting with a fraction-digit range, and lenient parsing of drafts.
/// </summary>
internal static class NumberFieldNumbers
{
    /// <summary>Arc's default maximum, <c>Number.MAX_SAFE_INTEGER</c>.</summary>
    public const decimal MaxSafeInteger = 9007199254740991m;

    private const decimal GridEpsilon = 0.0000001m;

    /// <summary>Significant fraction digits of a value (JS <c>String(value).split(".")[1].length</c>): 0.50 → 1.</summary>
    public static int DecimalsOf(decimal value)
    {
        value = Math.Abs(value);
        var scale = (int)value.Scale;
        while (scale > 0 && value == Math.Round(value, scale - 1))
        {
            scale--;
        }
        return scale;
    }

    /// <summary>Rounds to <paramref name="decimals"/> fraction digits (half away from zero); never returns negative zero.</summary>
    public static decimal Round(decimal value, int decimals)
    {
        var rounded = Math.Round(value, Math.Clamp(decimals, 0, 28), MidpointRounding.AwayFromZero);
        return rounded == 0m ? 0m : rounded;
    }

    /// <summary>The nearest grid line from <paramref name="origin"/> (JS <c>Math.round</c>: halves round up).</summary>
    public static decimal Snap(decimal value, decimal origin, decimal step, int decimals)
    {
        try
        {
            var index = Math.Floor((value - origin) / step + 0.5m);
            return Round(origin + index * step, decimals);
        }
        catch (OverflowException)
        {
            return value;
        }
    }

    /// <summary>
    /// Arc's <c>stepFrom</c>: an off-grid value first moves to the next grid line in the direction of travel, then whole
    /// steps from there.
    /// </summary>
    public static decimal StepFrom(decimal from, long steps, decimal origin, decimal step, int decimals)
    {
        try
        {
            var index = (from - origin) / step;
            var line = steps > 0 ? Math.Floor(index + GridEpsilon) : Math.Ceiling(index - GridEpsilon);
            return Round(origin + (line + steps) * step, decimals);
        }
        catch (OverflowException)
        {
            return steps > 0 ? decimal.MaxValue : decimal.MinValue;
        }
    }

    /// <summary>Formats like <c>Intl.NumberFormat</c> with a fraction-digit range and optional grouping.</summary>
    public static string Format(decimal value, CultureInfo culture, int minFraction, int maxFraction, bool grouping)
    {
        maxFraction = Math.Clamp(maxFraction, 0, 28);
        minFraction = Math.Clamp(minFraction, 0, maxFraction);
        var rounded = Round(value, maxFraction);
        var fraction = Math.Clamp(DecimalsOf(rounded), minFraction, maxFraction);
        return rounded.ToString((grouping ? "N" : "F") + fraction.ToString(CultureInfo.InvariantCulture), culture);
    }

    /// <summary>Number of fraction digits shown in <paramref name="formatted"/>.</summary>
    public static int FractionDigitsIn(string formatted, string decimalSeparator)
    {
        var index = string.IsNullOrEmpty(decimalSeparator) ? -1 : formatted.IndexOf(decimalSeparator, StringComparison.Ordinal);
        if (index < 0)
        {
            return 0;
        }
        var count = 0;
        for (var i = index + decimalSeparator.Length; i < formatted.Length; i++)
        {
            if (char.IsAsciiDigit(formatted[i]))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Arc's draft parser: drop group separators, read the first decimal separator as a point, keep digits, points and
    /// minus signs, and accept the result only when it holds a digit and is a finite number.
    /// </summary>
    public static decimal? Parse(string? text, string groupSeparator, string decimalSeparator)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        var source = string.IsNullOrEmpty(groupSeparator) ? text : text.Replace(groupSeparator, string.Empty, StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(decimalSeparator))
        {
            var at = source.IndexOf(decimalSeparator, StringComparison.Ordinal);
            if (at >= 0)
            {
                source = string.Concat(source.AsSpan(0, at), ".", source.AsSpan(at + decimalSeparator.Length));
            }
        }
        var builder = new StringBuilder(source.Length);
        var hasDigit = false;
        foreach (var c in source)
        {
            if (char.IsAsciiDigit(c))
            {
                hasDigit = true;
                builder.Append(c);
            }
            else if (c is '.' or '-')
            {
                builder.Append(c);
            }
        }
        if (!hasDigit)
        {
            return null;
        }
        var normalized = builder.ToString();
        const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (decimal.TryParse(normalized, styles, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed == 0m ? 0m : parsed;
        }
        // Beyond decimal's range a JS number is still finite; it clamps on commit, so saturate.
        if (double.TryParse(normalized, styles, CultureInfo.InvariantCulture, out var wide) && double.IsFinite(wide))
        {
            return wide > 0 ? decimal.MaxValue : decimal.MinValue;
        }
        return null;
    }

    /// <summary>Keeps the characters a draft may hold: digits, the culture's separators, and a minus when allowed.</summary>
    public static string Filter(string text, string groupSeparator, string decimalSeparator, bool allowDecimal, bool allowMinus)
    {
        StringBuilder? builder = null;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var allowed = char.IsAsciiDigit(c)
                          || groupSeparator.Contains(c)
                          || (allowDecimal && decimalSeparator.Contains(c))
                          || (allowMinus && c == '-');
            if (allowed)
            {
                builder?.Append(c);
            }
            else if (builder is null)
            {
                builder = new StringBuilder(text.Length);
                builder.Append(text, 0, i);
            }
        }
        return builder?.ToString() ?? text;
    }
}
