using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// Parsers and value conversions used by the generated token writers and override parsers. Every parser is culture
/// invariant, never throws, and uses no reflection. Enums are parsed by generated switches, not here.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ArcTokenValueParser
{
    /// <summary>Converts a color token to the resource value it is published as (an immutable solid color brush).</summary>
    /// <param name="color">The color.</param>
    public static object Brush(Color color) => new ImmutableSolidColorBrush(color);

    /// <summary>Parses <c>#RGB</c>, <c>#ARGB</c>, <c>#RRGGBB</c>, <c>#AARRGGBB</c> or a known color name.</summary>
    public static bool TryParseColor(string text, out Color value) => Color.TryParse(text.Trim(), out value);

    /// <summary>Parses an invariant-culture number.</summary>
    public static bool TryParseDouble(string text, out double value)
        => double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    /// <summary>Parses an invariant-culture integer.</summary>
    public static bool TryParseInt32(string text, out int value)
        => int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <summary>Parses <c>true</c> or <c>false</c> (any case).</summary>
    public static bool TryParseBoolean(string text, out bool value) => bool.TryParse(text.Trim(), out value);

    /// <summary>Parses a thickness: one, two or four numbers (<c>8</c>, <c>12,0</c>, <c>1,2,3,4</c>).</summary>
    public static bool TryParseThickness(string text, out Thickness value)
    {
        try
        {
            value = Thickness.Parse(text.Trim());
            return true;
        }
        catch (FormatException)
        {
        }
        catch (ArgumentException)
        {
        }
        value = default;
        return false;
    }

    /// <summary>Parses a corner radius: one or four numbers.</summary>
    public static bool TryParseCornerRadius(string text, out CornerRadius value)
    {
        try
        {
            value = CornerRadius.Parse(text.Trim());
            return true;
        }
        catch (FormatException)
        {
        }
        catch (ArgumentException)
        {
        }
        value = default;
        return false;
    }

    /// <summary>Parses box shadows in Avalonia syntax (<c>0 4 12 0 #20000000, inset 0 1 0 0 #10FFFFFF</c>), or <c>none</c>.</summary>
    public static bool TryParseBoxShadows(string text, out BoxShadows value)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0 || string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase))
        {
            value = default;
            return true;
        }
        try
        {
            value = BoxShadows.Parse(trimmed);
            return true;
        }
        catch (FormatException)
        {
        }
        catch (ArgumentException)
        {
        }
        catch (IndexOutOfRangeException)
        {
        }
        value = default;
        return false;
    }

    /// <summary>Parses a font family list (<c>Inter, Segoe UI</c>) or a resource font (<c>avares://…#Name</c>).</summary>
    public static bool TryParseFontFamily(string text, out FontFamily value)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            value = FontFamily.Default;
            return false;
        }
        try
        {
            value = FontFamily.Parse(trimmed);
            return true;
        }
        catch (FormatException)
        {
        }
        catch (ArgumentException)
        {
        }
        value = FontFamily.Default;
        return false;
    }

    /// <summary>
    /// Parses a duration: <c>120ms</c>, <c>0.2s</c>, a bare number of milliseconds (<c>120</c>), or a
    /// <see cref="TimeSpan"/> in invariant format (<c>00:00:00.120</c>).
    /// </summary>
    public static bool TryParseTimeSpan(string text, out TimeSpan value)
    {
        var trimmed = text.Trim();
        if (trimmed.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
        {
            if (TryParseDouble(trimmed.Substring(0, trimmed.Length - 2), out var ms) && ms >= 0)
            {
                value = TimeSpan.FromMilliseconds(ms);
                return true;
            }
        }
        else if (trimmed.EndsWith("s", StringComparison.OrdinalIgnoreCase))
        {
            if (TryParseDouble(trimmed.Substring(0, trimmed.Length - 1), out var s) && s >= 0)
            {
                value = TimeSpan.FromSeconds(s);
                return true;
            }
        }
        else if (trimmed.IndexOf(':') < 0)
        {
            if (TryParseDouble(trimmed, out var bare) && bare >= 0)
            {
                value = TimeSpan.FromMilliseconds(bare);
                return true;
            }
        }
        else if (TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Parses an easing: a cubic bezier <c>x1,y1,x2,y2</c> (CSS <c>cubic-bezier</c> numbers) or the name of an Avalonia
    /// easing (<c>Linear</c>, <c>CubicEaseOut</c>, … with or without the <c>Easing</c> suffix).
    /// </summary>
    public static bool TryParseEasing(string text, out Easing value)
    {
        var trimmed = text.Trim();
        if (trimmed.IndexOf(',') >= 0)
        {
            var parts = trimmed.Split(',');
            if (parts.Length == 4
                && TryParseDouble(parts[0], out var x1) && TryParseDouble(parts[1], out var y1)
                && TryParseDouble(parts[2], out var x2) && TryParseDouble(parts[3], out var y2)
                && x1 is >= 0 and <= 1 && x2 is >= 0 and <= 1)
            {
                value = new SplineEasing(new KeySpline(x1, y1, x2, y2));
                return true;
            }
            value = new LinearEasing();
            return false;
        }
        var name = trimmed.EndsWith("Easing", StringComparison.OrdinalIgnoreCase)
            ? trimmed.Substring(0, trimmed.Length - "Easing".Length)
            : trimmed;
        Easing? easing = name.ToUpperInvariant() switch
        {
            "LINEAR" => new LinearEasing(),
            "QUADRATICEASEIN" => new QuadraticEaseIn(),
            "QUADRATICEASEOUT" => new QuadraticEaseOut(),
            "QUADRATICEASEINOUT" => new QuadraticEaseInOut(),
            "CUBICEASEIN" => new CubicEaseIn(),
            "CUBICEASEOUT" => new CubicEaseOut(),
            "CUBICEASEINOUT" => new CubicEaseInOut(),
            "QUARTICEASEIN" => new QuarticEaseIn(),
            "QUARTICEASEOUT" => new QuarticEaseOut(),
            "QUARTICEASEINOUT" => new QuarticEaseInOut(),
            "SINEEASEIN" => new SineEaseIn(),
            "SINEEASEOUT" => new SineEaseOut(),
            "SINEEASEINOUT" => new SineEaseInOut(),
            "EXPONENTIALEASEIN" => new ExponentialEaseIn(),
            "EXPONENTIALEASEOUT" => new ExponentialEaseOut(),
            "EXPONENTIALEASEINOUT" => new ExponentialEaseInOut(),
            _ => null
        };
        value = easing ?? new LinearEasing();
        return easing is not null;
    }
}
