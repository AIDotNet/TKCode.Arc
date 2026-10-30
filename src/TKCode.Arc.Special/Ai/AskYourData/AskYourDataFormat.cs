using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// The pure logic of <see cref="AskYourData"/>: number and time formats, the value scale and its ticks, how many X labels
/// fit, the readout sentence, the legend value, and the cuts the paced reveals step through.
/// </summary>
internal static class AskYourDataFormat
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    /// <summary>A whole number with en-US grouping: 18342 → "18,342".</summary>
    public static string Grouped(long value) => value.ToString("N0", s_enUs);

    /// <summary>The default value format: en-US grouping, up to two decimals only when the value has them.</summary>
    public static string Value(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "–";
        }
        var rounded = Math.Round(value, 2);
        return Math.Abs(rounded - Math.Round(rounded)) < 1e-9
            ? rounded.ToString("N0", s_enUs)
            : rounded.ToString("#,##0.##", s_enUs);
    }

    /// <summary>The query time: under a second in whole milliseconds ("214ms"), then seconds with one decimal ("1.2s").</summary>
    public static string Duration(double milliseconds)
    {
        if (milliseconds < 1000)
        {
            return Math.Max(0, Math.Round(milliseconds)).ToString("0", s_enUs) + "ms";
        }
        return (milliseconds / 1000).ToString("0.0", s_enUs) + "s";
    }

    /// <summary>The SQL meta: the dialect, then the rows and the time once known ("PostgreSQL · 48 rows · 214ms").</summary>
    public static string Meta(string? dialect, string? state, AskYourDataResult? result, AskYourDataLabels labels)
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(dialect))
        {
            parts.Add(dialect);
        }
        if (state is not null)
        {
            parts.Add(state);
        }
        else if (result is not null)
        {
            if (result.RowCount is { } rows)
            {
                parts.Add(labels.Rows(rows));
            }
            if (result.DurationMs is { } ms)
            {
                parts.Add(labels.Duration(ms));
            }
        }
        return string.Join(" · ", parts);
    }

    /// <summary>What follows a legend label, by the result's choice (sum when stacked, none otherwise).</summary>
    public static AskYourDataLegendValue LegendValueOf(AskYourDataResult result) =>
        result.LegendValue ?? (result.Stacked ? AskYourDataLegendValue.Sum : AskYourDataLegendValue.None);

    /// <summary>The legend's value for one series, or null when the result shows none.</summary>
    public static double? LegendNumber(AskYourDataResult result, AskYourDataSeries series)
    {
        switch (LegendValueOf(result))
        {
            case AskYourDataLegendValue.Sum:
                return series.Values.Sum();
            case AskYourDataLegendValue.Last:
                return series.Values.Count > 0 ? series.Values[^1] : null;
            default:
                return null;
        }
    }

    /// <summary>A series' value at a category, 0 when it has none there.</summary>
    public static double ValueAt(AskYourDataSeries series, int index) =>
        index >= 0 && index < series.Values.Count && double.IsFinite(series.Values[index]) ? series.Values[index] : 0;

    /// <summary>The sum of every series at a category.</summary>
    public static double TotalAt(AskYourDataResult result, int index)
    {
        var total = 0.0;
        foreach (var series in result.Series)
        {
            total += ValueAt(series, index);
        }
        return total;
    }

    /// <summary>The largest value the plot must hold: the tallest stack for stacked bars, the largest single value otherwise.</summary>
    public static double DataMaximum(AskYourDataResult result, bool stackedBars)
    {
        var max = 0.0;
        for (var i = 0; i < result.Categories.Count; i++)
        {
            if (stackedBars)
            {
                max = Math.Max(max, TotalAt(result, i));
            }
            else
            {
                foreach (var series in result.Series)
                {
                    max = Math.Max(max, ValueAt(series, i));
                }
            }
        }
        return max;
    }

    /// <summary>
    /// A "nice" value axis from 0: the top is the first tick at or above <paramref name="max"/> on a 1, 2, 2.5 or 5 step,
    /// with about <paramref name="count"/> intervals.
    /// </summary>
    public static (double Top, double Step) NiceScale(double max, int count = 4)
    {
        if (!(max > 0) || !double.IsFinite(max))
        {
            return (1, 0.25);
        }
        var raw = max / Math.Max(1, count);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var normalized = raw / magnitude;
        var step = normalized switch
        {
            <= 1 => 1.0,
            <= 2 => 2.0,
            <= 2.5 => 2.5,
            <= 5 => 5.0,
            _ => 10.0
        } * magnitude;
        var top = Math.Ceiling(max / step - 1e-9) * step;
        return (top, step);
    }

    /// <summary>
    /// Every how many categories an X label is drawn so labels keep <paramref name="minGap"/> apart (46px in Arc) at a
    /// band of <paramref name="band"/> pixels: 1 draws all.
    /// </summary>
    public static int LabelStride(double band, double minGap)
    {
        if (band <= 0)
        {
            return 1;
        }
        return Math.Max(1, (int)Math.Ceiling(minGap / band - 1e-9));
    }

    /// <summary>The readout of a category: "Aug 10: Free 61, Pro 26, Team 10, Enterprise 2, total 99".</summary>
    public static string Readout(AskYourDataResult result, int index, Func<double, string> format, AskYourDataLabels labels)
    {
        if (index < 0 || index >= result.Categories.Count)
        {
            return string.Empty;
        }
        var text = new StringBuilder(result.Categories[index]).Append(": ");
        for (var s = 0; s < result.Series.Count; s++)
        {
            if (s > 0)
            {
                text.Append(", ");
            }
            text.Append(result.Series[s].Label).Append(' ').Append(format(ValueAt(result.Series[s], index)));
        }
        if (result.Stacked && result.Series.Count > 1)
        {
            text.Append(", ").Append(labels.Total.ToLowerInvariant()).Append(' ').Append(format(TotalAt(result, index)));
        }
        return text.ToString();
    }

    /// <summary>
    /// Where each of the reveal's ticks may end, as character offsets after a whole token: words, numbers, quoted
    /// strings and single symbols each count as one, and the whitespace before a token travels with it. The last cut is
    /// the text's length.
    /// </summary>
    public static List<int> TokenEnds(string text)
    {
        var ends = new List<int>();
        var i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            if (i >= text.Length)
            {
                break;
            }
            var c = text[i];
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_' || text[i] == '.' && i + 1 < text.Length && char.IsDigit(text[i + 1])))
                {
                    i++;
                }
            }
            else if (c is '\'' or '"')
            {
                i++;
                while (i < text.Length && text[i] != c)
                {
                    i++;
                }
                i = Math.Min(text.Length, i + 1);
            }
            else
            {
                i++;
            }
            ends.Add(i);
        }
        if (ends.Count == 0 || ends[^1] != text.Length)
        {
            ends.Add(text.Length);
        }
        return ends;
    }

    /// <summary>Where each word of a sentence ends (the space before a word travels with it).</summary>
    public static List<int> WordEnds(string text)
    {
        var ends = new List<int>();
        var i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            if (i >= text.Length)
            {
                break;
            }
            while (i < text.Length && !char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            ends.Add(i);
        }
        if (ends.Count == 0 || ends[^1] != text.Length)
        {
            ends.Add(text.Length);
        }
        return ends;
    }
}
