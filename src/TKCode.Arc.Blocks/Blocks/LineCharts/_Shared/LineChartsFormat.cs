using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The default number and time formats of the Line charts blocks (US English, like the rest of the blocks' defaults) and
/// the "nice" axis ticks both blocks share.
/// </summary>
internal static class LineChartsFormat
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The true minus sign (U+2212) used before negative numbers.</summary>
    public const string Minus = "−";

    /// <summary>
    /// A value in thousands of US dollars, compact: under a thousand as whole thousands ("$412k"), from a thousand as
    /// millions with up to two decimals and no trailing zeros ("$1.71M", "$1M").
    /// </summary>
    public static string ThousandsOfDollars(double thousands)
    {
        if (!double.IsFinite(thousands))
        {
            return "";
        }
        var sign = thousands < 0 ? Minus : "";
        var a = Math.Abs(thousands);
        if (Math.Round(a) < 1000)
        {
            return sign + "$" + Math.Round(a).ToString("0", Culture) + "k";
        }
        return sign + "$" + (a / 1000).ToString("0.##", Culture) + "M";
    }

    /// <summary>A signed percentage with one decimal and a true minus sign ("+19.9%", "−11.9%"); <paramref name="share"/> 0.199 is 19.9%.</summary>
    public static string SignedPercent(double share)
    {
        var rounded = Math.Round(share * 100, 1, MidpointRounding.AwayFromZero);
        var text = Math.Abs(rounded).ToString("0.0", Culture) + "%";
        return (rounded < 0 ? Minus : "+") + text;
    }

    /// <summary>A whole count with group separators ("2,316").</summary>
    public static string Count(double value) =>
        (value < 0 ? Minus : "") + Math.Abs(Math.Round(value)).ToString("#,0", Culture);

    /// <summary>A compact axis count ("0", "2k", "1.5k", "3M").</summary>
    public static string CompactCount(double value)
    {
        var a = Math.Abs(value);
        var sign = value < 0 ? Minus : "";
        if (a >= 1_000_000)
        {
            return sign + (a / 1_000_000).ToString("0.#", Culture) + "M";
        }
        if (a >= 1000)
        {
            return sign + (a / 1000).ToString("0.#", Culture) + "k";
        }
        return sign + a.ToString("0.#", Culture);
    }

    /// <summary>A time of day from minutes after midnight ("9:40 am", "12:05 pm"); minutes past a day wrap.</summary>
    public static string Clock(double minutes)
    {
        var total = (int)Math.Floor(minutes + 1e-6);
        total = ((total % 1440) + 1440) % 1440;
        var hour = total / 60;
        var minute = total % 60;
        var h12 = hour % 12 == 0 ? 12 : hour % 12;
        return h12.ToString(Culture) + ":" + minute.ToString("00", Culture) + (hour < 12 ? " am" : " pm");
    }

    /// <summary>A whole hour from minutes after midnight ("6 am", "12 pm", "12 am").</summary>
    public static string Hour(double minutes)
    {
        var total = (int)Math.Round(minutes);
        total = ((total % 1440) + 1440) % 1440;
        var hour = total / 60;
        var h12 = hour % 12 == 0 ? 12 : hour % 12;
        return h12.ToString(Culture) + (hour < 12 ? " am" : " pm");
    }

    /// <summary>
    /// Ticks inside [<paramref name="min"/>, <paramref name="max"/>] on a 1, 2 or 5 × 10ⁿ step chosen so that about
    /// <paramref name="target"/> fall inside. Empty when the range is empty or not finite.
    /// </summary>
    public static double[] NiceTicks(double min, double max, int target)
    {
        if (!double.IsFinite(min) || !double.IsFinite(max) || max <= min || target < 1)
        {
            return [];
        }
        var step = NiceStep((max - min) / target);
        var first = Math.Ceiling(min / step - 1e-9) * step;
        var ticks = new List<double>(target + 2);
        for (var v = first; v <= max + step * 1e-9; v += step)
        {
            // Snap away float noise so 0.30000000000000004 reads as 0.3 and 0 is exactly 0.
            ticks.Add(Math.Round(v / step) * step);
            if (ticks.Count > 64)
            {
                break;
            }
        }
        return ticks.ToArray();
    }

    /// <summary>
    /// The 1, 2 or 5 × 10ⁿ step closest to <paramref name="rough"/> on a log scale (the mantissa rounds up past √2, √10
    /// and √50), the usual axis rule.
    /// </summary>
    public static double NiceStep(double rough)
    {
        if (!(rough > 0) || !double.IsFinite(rough))
        {
            return 1;
        }
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var residual = rough / magnitude;
        var nice = residual >= Math.Sqrt(50) ? 10 : residual >= Math.Sqrt(10) ? 5 : residual >= Math.Sqrt(2) ? 2 : 1;
        return nice * magnitude;
    }

    /// <summary>The step and the top of a zero-based axis that holds <paramref name="max"/> with about <paramref name="target"/> steps.</summary>
    public static (double Step, double Top) NiceZeroAxis(double max, int target)
    {
        if (!(max > 0) || !double.IsFinite(max))
        {
            return (1, 1);
        }
        var step = NiceStep(max / Math.Max(1, target));
        return (step, Math.Ceiling(max / step - 1e-9) * step);
    }
}
