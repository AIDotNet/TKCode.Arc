using System.Globalization;
using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// The colour steps of one level of the map (all states, or the counties of one state) for one metric: the bin edges
/// (rounded to two significant digits, ascending), each bin's alpha and side, and how many regions fall in each.
/// </summary>
internal sealed class ChoroplethBinning
{
    public static readonly ChoroplethBinning Empty = new(ChoroplethScale.Sequential, [], [], 0, [], 0);

    public ChoroplethBinning(ChoroplethScale scale, double[] edges, double[] alphas, int negativeBins, int[] counts, int noDataCount)
    {
        Scale = scale;
        Edges = edges;
        Alphas = alphas;
        NegativeBins = negativeBins;
        Counts = counts;
        NoDataCount = noDataCount;
    }

    public ChoroplethScale Scale { get; }

    /// <summary>The <see cref="BinCount"/> − 1 inner edges; a value equal to an edge belongs to the bin above it.</summary>
    public double[] Edges { get; }

    /// <summary>The alpha of each bin before compositing (sequential .16 … .94; diverging .86 .46 | .2 .42 .66 .9).</summary>
    public double[] Alphas { get; }

    /// <summary>How many of the bins (from the first) are negative (diverging only).</summary>
    public int NegativeBins { get; }

    /// <summary>Regions per bin.</summary>
    public int[] Counts { get; }

    /// <summary>Regions without a value.</summary>
    public int NoDataCount { get; }

    public int BinCount => Alphas.Length;

    /// <summary>The bin of <paramref name="value"/>: how many edges are at or below it; −1 for no data.</summary>
    public int BinOf(double? value)
    {
        if (value is not { } v || BinCount == 0)
        {
            return -1;
        }
        var bin = 0;
        foreach (var edge in Edges)
        {
            if (v >= edge)
            {
                bin++;
            }
        }
        return Math.Min(bin, BinCount - 1);
    }

    public bool IsNegative(int bin) => bin >= 0 && bin < NegativeBins;

    /// <summary>The lower edge of <paramref name="bin"/> (null for the first).</summary>
    public double? Lower(int bin) => bin > 0 && bin - 1 < Edges.Length ? Edges[bin - 1] : null;

    /// <summary>The upper edge of <paramref name="bin"/> (null for the last).</summary>
    public double? Upper(int bin) => bin >= 0 && bin < Edges.Length ? Edges[bin] : null;
}

/// <summary>A region's place in the ranked list.</summary>
internal readonly record struct ChoroplethRank(int Index, double? Value, int Rank);

/// <summary>
/// The pure arithmetic of <see cref="ChoroplethExplorer"/>, measured from the reference (notes sections 0, 3.4, 4.1–4.3):
/// linear-interpolation quantiles (d3.quantile, R type 7) rounded to two significant digits, the diverging split at zero,
/// the bin alphas and their composite opacity, the camera's target, the sweep delay, ranking, aggregation and formatting.
/// </summary>
internal static class ChoroplethMath
{
    /// <summary>The camera fills 80% of the view with the opened region's box.</summary>
    public const double CameraFill = 0.8;

    /// <summary>The camera never zooms in further than 12× (small regions: Rhode Island, Washington DC).</summary>
    public const double MaxCameraScale = 12;

    /// <summary>The longest delay of the west-to-east sweep (a region's delay is its centre's x over the width × 260ms).</summary>
    public static readonly TimeSpan SweepSpan = TimeSpan.FromMilliseconds(260);

    /// <summary>Sequential alphas for five bins (measured).</summary>
    public static readonly double[] SequentialAlphas5 = [0.16, 0.34, 0.54, 0.74, 0.94];

    /// <summary>Positive alphas of the diverging scale for four bins (measured).</summary>
    public static readonly double[] PositiveAlphas4 = [0.2, 0.42, 0.66, 0.9];

    /// <summary>Negative alphas of the diverging scale for two bins, farthest from zero first (measured).</summary>
    public static readonly double[] NegativeAlphas2 = [0.86, 0.46];

    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Linear-interpolation quantile of an ascending array (d3.quantile, R type 7).</summary>
    public static double Quantile(ReadOnlySpan<double> sorted, double p)
    {
        if (sorted.Length == 0)
        {
            return double.NaN;
        }
        if (sorted.Length == 1)
        {
            return sorted[0];
        }
        var h = (sorted.Length - 1) * Math.Clamp(p, 0, 1);
        var lo = (int)Math.Floor(h);
        var hi = Math.Min(lo + 1, sorted.Length - 1);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (h - lo);
    }

    /// <summary>Rounds to <paramref name="digits"/> significant digits (10.3 → 10, 13.8 → 14, 20.975 → 21, 0.0456 → 0.046).</summary>
    public static double RoundSignificant(double value, int digits = 2)
    {
        if (value == 0 || !double.IsFinite(value))
        {
            return value;
        }
        var magnitude = (int)Math.Floor(Math.Log10(Math.Abs(value)));
        var factor = Math.Pow(10, digits - 1 - magnitude);
        return Math.Round(value * factor, MidpointRounding.AwayFromZero) / factor;
    }

    /// <summary>Evenly spaced alphas from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static double[] EvenAlphas(int count, double from, double to)
    {
        var result = new double[Math.Max(0, count)];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = count == 1 ? (from + to) / 2 : from + (to - from) * i / (count - 1);
        }
        return result;
    }

    /// <summary>The composite opacity of an alpha step drawn over the surface: a + .06(1 − a) in light, a + .16(1 − a) in dark.</summary>
    public static double ComposeOpacity(double alpha, bool dark) => alpha + (dark ? 0.16 : 0.06) * (1 - alpha);

    /// <summary>
    /// Bins the values of one level. Sequential: <paramref name="binCount"/> bins split at the quantiles 1/n … (n−1)/n of all
    /// values. Diverging: n + 1 bins shared by the negative and the non-negative values in proportion to their counts (at
    /// least one each when both exist; measured 13 / 38 of 51 → 2 / 4), each side split at its own quantiles, with zero as
    /// the edge between the sides. Every edge is rounded to two significant digits.
    /// </summary>
    public static ChoroplethBinning Bin(IReadOnlyList<double?> values, ChoroplethScale scale, int binCount = 5)
    {
        binCount = Math.Clamp(binCount, 2, 9);
        var present = new List<double>(values.Count);
        var noData = 0;
        foreach (var value in values)
        {
            if (value is { } v && double.IsFinite(v))
            {
                present.Add(v);
            }
            else
            {
                noData++;
            }
        }
        present.Sort();
        var sorted = present.ToArray();
        double[] edges;
        double[] alphas;
        var negativeBins = 0;
        if (scale == ChoroplethScale.Sequential || sorted.Length == 0)
        {
            edges = new double[binCount - 1];
            for (var k = 1; k < binCount; k++)
            {
                edges[k - 1] = RoundSignificant(Quantile(sorted, (double)k / binCount));
            }
            alphas = binCount == 5 ? (double[])SequentialAlphas5.Clone() : EvenAlphas(binCount, 0.16, 0.94);
        }
        else
        {
            var total = binCount + 1;
            var negatives = sorted.TakeWhile(v => v < 0).ToArray();
            var positives = sorted.Skip(negatives.Length).ToArray();
            if (negatives.Length == 0)
            {
                negativeBins = 0;
            }
            else if (positives.Length == 0)
            {
                negativeBins = total;
            }
            else
            {
                var share = (int)Math.Round(total * (double)negatives.Length / sorted.Length, MidpointRounding.AwayFromZero);
                negativeBins = Math.Clamp(share, 1, total - 1);
            }
            var positiveBins = total - negativeBins;
            var list = new List<double>(total - 1);
            for (var k = 1; k < negativeBins; k++)
            {
                list.Add(RoundSignificant(Quantile(negatives, (double)k / negativeBins)));
            }
            if (negativeBins > 0 && positiveBins > 0)
            {
                list.Add(0);
            }
            for (var k = 1; k < positiveBins; k++)
            {
                list.Add(RoundSignificant(Quantile(positives, (double)k / positiveBins)));
            }
            edges = list.ToArray();
            var negativeAlphas = negativeBins == 2 ? NegativeAlphas2 : EvenAlphas(negativeBins, 0.86, 0.46);
            var positiveAlphas = positiveBins == 4 ? PositiveAlphas4 : EvenAlphas(positiveBins, 0.2, 0.9);
            alphas = [.. negativeAlphas, .. positiveAlphas];
        }
        var binning = new ChoroplethBinning(scale, edges, alphas, negativeBins, new int[alphas.Length], noData);
        foreach (var v in sorted)
        {
            binning.Counts[binning.BinOf(v)]++;
        }
        return binning;
    }

    /// <summary>Indices of <paramref name="values"/> by value, highest first; regions without a value last; ties by name.</summary>
    public static int[] RankOrder(IReadOnlyList<double?> values, IReadOnlyList<string> names)
    {
        var order = Enumerable.Range(0, values.Count).ToArray();
        Array.Sort(order, (a, b) =>
        {
            var va = values[a];
            var vb = values[b];
            if (va is null || vb is null)
            {
                return va is null && vb is null ? string.CompareOrdinal(names[a], names[b]) : va is null ? 1 : -1;
            }
            var c = vb.Value.CompareTo(va.Value);
            return c != 0 ? c : string.CompareOrdinal(names[a], names[b]);
        });
        return order;
    }

    /// <summary>The headline of a whole level: the sum, or the average weighted by <paramref name="weights"/> (equal weights when there are none).</summary>
    public static double? Aggregate(IReadOnlyList<double?> values, IReadOnlyList<double?>? weights, ChoroplethAggregate kind)
    {
        double sum = 0, weightSum = 0;
        var any = false;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not { } v)
            {
                continue;
            }
            any = true;
            if (kind == ChoroplethAggregate.Sum)
            {
                sum += v;
                continue;
            }
            var w = weights is not null && i < weights.Count && weights[i] is { } given ? Math.Max(0, given) : 1;
            sum += v * w;
            weightSum += w;
        }
        if (!any)
        {
            return null;
        }
        if (kind == ChoroplethAggregate.Sum)
        {
            return sum;
        }
        return weightSum > 0 ? sum / weightSum : null;
    }

    /// <summary>
    /// The camera that brings <paramref name="box"/> into the middle of a <paramref name="viewBox"/>-sized view: scale
    /// 0.8 × min(W / w, H / h), at most 12, and a translation that puts the box's centre at the view's centre
    /// (California in the 975 × 610 Albers USA box: 1.9757).
    /// </summary>
    public static (double Scale, double X, double Y) CameraFor(Rect box, Size viewBox)
    {
        if (box.Width <= 0 || box.Height <= 0)
        {
            return (1, 0, 0);
        }
        var scale = Math.Min(MaxCameraScale, CameraFill * Math.Min(viewBox.Width / box.Width, viewBox.Height / box.Height));
        var center = box.Center;
        return (scale, viewBox.Width / 2 - scale * center.X, viewBox.Height / 2 - scale * center.Y);
    }

    /// <summary>The sweep position of a region: its box's centre x over the view's width, clamped to 0…1.</summary>
    public static double Wave(Rect box, double viewWidth) =>
        viewWidth > 0 ? Math.Clamp(box.Center.X / viewWidth, 0, 1) : 0;

    /// <summary>The sweep delay of a region (wave × 260ms).</summary>
    public static TimeSpan SweepDelay(double wave) => TimeSpan.FromMilliseconds(Math.Clamp(wave, 0, 1) * SweepSpan.TotalMilliseconds);

    /// <summary>Writes a value: "$9.8M", "$903K", "$4.4K", "$950"; "+4.3%" / "−4.8%"; "17.5%"; "1,250".</summary>
    public static string FormatValue(double value, ChoroplethValueFormat format, string symbol)
    {
        if (!double.IsFinite(value))
        {
            return AnalyticsFormat.Missing;
        }
        switch (format)
        {
            case ChoroplethValueFormat.Currency:
            {
                var abs = Math.Abs(value);
                if (abs >= 999_950)
                {
                    return AnalyticsFormat.CompactFixed(value, 1, symbol, upperCaseThousands: true);
                }
                if (abs >= 9_999.5)
                {
                    return AnalyticsFormat.CompactFixed(value, 0, symbol, upperCaseThousands: true);
                }
                if (abs >= 999.5)
                {
                    return AnalyticsFormat.CompactFixed(value, 1, symbol, upperCaseThousands: true);
                }
                return AnalyticsFormat.Currency(value, 0, symbol);
            }
            case ChoroplethValueFormat.Change:
                return AnalyticsFormat.Percent(value, 1, signed: true);
            case ChoroplethValueFormat.Percent:
                return AnalyticsFormat.Percent(value, 1);
            default:
                return AnalyticsFormat.Number(value);
        }
    }

    /// <summary>Writes a legend tick (already two significant digits): "$120K", "$1.6M"; "+4.3%", "0%", "−4.8%", "+21%"; "4.6%".</summary>
    public static string FormatTick(double value, ChoroplethValueFormat format, string symbol)
    {
        if (!double.IsFinite(value))
        {
            return AnalyticsFormat.Missing;
        }
        var rounded = RoundSignificant(value);
        var decimals = DecimalsOf(rounded);
        return format switch
        {
            ChoroplethValueFormat.Currency => AnalyticsFormat.Compact(rounded, 2, symbol, upperCaseThousands: true, trimZeros: true),
            ChoroplethValueFormat.Change => AnalyticsFormat.Percent(rounded, decimals, signed: true),
            ChoroplethValueFormat.Percent => AnalyticsFormat.Percent(rounded, decimals),
            _ => AnalyticsFormat.Number(rounded, decimals),
        };
    }

    /// <summary>Fraction digits a two-significant-digit number needs (21 → 0, 4.3 → 1, 0.45 → 2).</summary>
    public static int DecimalsOf(double value)
    {
        var abs = Math.Abs(value);
        for (var d = 0; d < 6; d++)
        {
            if (Math.Abs(Math.Round(abs, d) - abs) < 1e-9 * Math.Max(1, abs))
            {
                return d;
            }
        }
        return 6;
    }

    /// <summary>Text for screen readers: a true minus sign is read as <paramref name="minusWord"/> ("minus 13.6%").</summary>
    public static string Spoken(string text, string minusWord) =>
        text.Contains(AnalyticsFormat.Minus, StringComparison.Ordinal) ? text.Replace(AnalyticsFormat.Minus, minusWord + " ", StringComparison.Ordinal) : text;

    /// <summary>string.Format with the en-US culture that never throws on a bad pattern.</summary>
    public static string Format(string pattern, params object?[] args)
    {
        try
        {
            return string.Format(s_culture, pattern, args);
        }
        catch (FormatException)
        {
            return pattern;
        }
    }
}
