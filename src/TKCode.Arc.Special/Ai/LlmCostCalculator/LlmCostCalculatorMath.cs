using System.Globalization;

namespace TKCode.Arc;

/// <summary>A money figure split for <see cref="TKCode.Arc.Primitives.RollingDigits"/>: the number, its decimals and a compact suffix.</summary>
internal readonly record struct LlmCostFigure(double Value, int Decimals, string Suffix)
{
    /// <summary>The figure as text, for names and announcements ("$1,234.56", "$420K").</summary>
    public string Text => "$" + Value.ToString("N" + Decimals, CultureInfo.GetCultureInfo("en-US")) + Suffix;
}

/// <summary>The arithmetic, the slider stops, the ranking and the money formats of <see cref="LlmCostCalculator"/>.</summary>
internal static class LlmCostCalculatorMath
{
    /// <summary>Input tokens per request, 250 to 200,000.</summary>
    public static readonly int[] InputStops = [250, 500, 1_000, 2_000, 4_000, 8_000, 16_000, 32_000, 64_000, 128_000, 200_000];

    /// <summary>Output tokens per request, 100 to 16,000.</summary>
    public static readonly int[] OutputStops = [100, 250, 500, 1_000, 2_000, 4_000, 8_000, 16_000];

    /// <summary>Requests a day, 100 to 1,000,000.</summary>
    public static readonly int[] RequestStops =
        [100, 250, 500, 1_000, 2_500, 5_000, 10_000, 25_000, 50_000, 100_000, 250_000, 500_000, 1_000_000];

    /// <summary>Monthly costs at or above this switch to compact figures ($420K, $4.2M).</summary>
    public const double CompactFrom = 100_000;

    private static readonly CultureInfo s_en = CultureInfo.GetCultureInfo("en-US");

    /// <summary>What <paramref name="model"/> costs a month for <paramref name="usage"/>.</summary>
    public static LlmCost Compute(LlmModelPrice model, LlmUsage usage, double cacheHitRate, double daysPerMonth)
    {
        var rate = double.IsFinite(cacheHitRate) ? Math.Clamp(cacheHitRate, 0, 1) : 0;
        var days = double.IsFinite(daysPerMonth) ? Math.Max(0, daysPerMonth) : 0;
        var requests = Math.Max(0, (double)usage.RequestsPerDay) * days;
        var inputTokens = Math.Max(0, (double)usage.InputTokens) * requests;
        var outputTokens = Math.Max(0, (double)usage.OutputTokens) * requests;
        var share = usage.Cached ? rate : 0;
        var cachedTokens = inputTokens * share;
        var input = (inputTokens - cachedTokens) / 1_000_000 * model.Input;
        var cached = cachedTokens / 1_000_000 * (model.CachedInput ?? model.Input);
        var output = outputTokens / 1_000_000 * model.Output;
        var total = input + cached + output;
        return new LlmCost(input, cached, output, total, requests > 0 ? total / requests : 0);
    }

    /// <summary>The index of the stop nearest to <paramref name="value"/> (the first of two equally near).</summary>
    public static int NearestStop(int[] stops, int value)
    {
        var best = 0;
        for (var i = 1; i < stops.Length; i++)
        {
            if (Math.Abs(stops[i] - (double)value) < Math.Abs(stops[best] - (double)value))
            {
                best = i;
            }
        }
        return best;
    }

    /// <summary>The order of the models by monthly cost, lowest first; ties keep the models' order.</summary>
    public static int[] Rank(IReadOnlyList<LlmCost> costs)
    {
        var order = new int[costs.Count];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }
        // A stable sort: equal totals keep the order the app passed.
        return [.. order.OrderBy(i => costs[i].Total).ThenBy(i => i)];
    }

    /// <summary>A row's monthly cost: compact from $100,000, whole dollars from $100, cents below.</summary>
    public static LlmCostFigure Monthly(double value)
    {
        value = Math.Max(0, value);
        if (value >= 1_000_000_000)
        {
            return new LlmCostFigure(Round(value / 1_000_000_000, 1), 1, "B");
        }
        if (value >= 1_000_000)
        {
            return new LlmCostFigure(Round(value / 1_000_000, 1), 1, "M");
        }
        if (value >= CompactFrom)
        {
            return new LlmCostFigure(Math.Round(value / 1_000, MidpointRounding.AwayFromZero), 0, "K");
        }
        return value >= 100
            ? new LlmCostFigure(Math.Round(value, MidpointRounding.AwayFromZero), 0, string.Empty)
            : new LlmCostFigure(Round(value, 2), 2, string.Empty);
    }

    /// <summary>The total: every digit, with cents.</summary>
    public static LlmCostFigure Total(double value) => new(Round(Math.Max(0, value), 2), 2, string.Empty);

    /// <summary>The cost of one request: four decimals under a dollar, cents from there.</summary>
    public static LlmCostFigure PerRequest(double value)
    {
        value = Math.Max(0, value);
        return value < 1 ? new LlmCostFigure(Round(value, 4), 4, string.Empty) : new LlmCostFigure(Round(value, 2), 2, string.Empty);
    }

    /// <summary>A price per 1M tokens: "$3", "$0.30", "$0.025".</summary>
    public static string Price(double value)
    {
        value = Math.Max(0, value);
        var decimals = value == Math.Round(value) && value >= 1 ? 0 : value * 100 == Math.Round(value * 100) ? 2 : 3;
        return "$" + value.ToString("N" + decimals, s_en);
    }

    /// <summary>A whole count with group separators ("16,000").</summary>
    public static string Count(double value) => Math.Round(value).ToString("N0", s_en);

    private static double Round(double value, int decimals) => Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}
