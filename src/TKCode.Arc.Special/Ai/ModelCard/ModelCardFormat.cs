using System.Globalization;

namespace TKCode.Arc;

/// <summary>A number the card rolls: the value, how many decimals it shows and the text around it.</summary>
internal readonly record struct ModelCardNumberValue(double Value, int Decimals, string? Prefix, string? Suffix)
{
    /// <summary>The number as it reads ("$3.00", "1M", "82.1%").</summary>
    public string Text => Prefix + Value.ToString("N" + Decimals, CultureInfo.GetCultureInfo("en-US")) + Suffix;
}

/// <summary>How a compare row decides which value is better and how it writes the margin.</summary>
internal enum ModelCardCompareKind
{
    /// <summary>Token counts: more is better; the margin is a percentage below 2x, then a multiple ("+25%", "2.5x").</summary>
    Count,

    /// <summary>Prices and costs: less is better; "−20%" below 2x, then "2x".</summary>
    Price,

    /// <summary>Benchmark scores: more is better; the margin is the difference ("+2.3").</summary>
    Score,

    /// <summary>Knowledge cutoff: later is better; the margin is in months ("+3 mo").</summary>
    Month
}

/// <summary>The winner of one compare row and the margin it wins by.</summary>
internal readonly record struct ModelCardVerdict(int Winner, string Margin)
{
    /// <summary>Neither value is better (equal, or one is missing).</summary>
    public static ModelCardVerdict Even => new(0, string.Empty);
}

/// <summary>Pure formatting and arithmetic of <see cref="ModelCard"/>: token counts, months, prices, volume stops, ranks and margins.</summary>
internal static class ModelCardFormat
{
    private static readonly CultureInfo s_en = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The monthly volumes the slider steps through.</summary>
    public static readonly long[] VolumeStops =
    [
        1_000_000, 5_000_000, 10_000_000, 25_000_000, 50_000_000, 100_000_000, 250_000_000, 500_000_000, 1_000_000_000
    ];

    /// <summary>About how many pages a million tokens fill (1M tokens is about 1,500 pages).</summary>
    public const double PagesPerToken = 0.0015;

    /// <summary>The stop nearest to <paramref name="tokens"/> (nearest on a log scale, since the stops grow geometrically).</summary>
    public static int SnapIndex(long tokens)
    {
        if (tokens <= VolumeStops[0])
        {
            return 0;
        }
        var log = Math.Log(tokens);
        var best = 0;
        var distance = double.MaxValue;
        for (var i = 0; i < VolumeStops.Length; i++)
        {
            var d = Math.Abs(Math.Log(VolumeStops[i]) - log);
            if (d < distance)
            {
                distance = d;
                best = i;
            }
        }
        return best;
    }

    /// <summary>A token count as a rolling number: 1_000_000 → 1 "M", 64_000 → 64 "K", 1_050_000 → 1.05 "M".</summary>
    public static ModelCardNumberValue Tokens(long tokens)
    {
        var (scaled, suffix) = tokens switch
        {
            >= 1_000_000_000 => (tokens / 1e9, "B"),
            >= 1_000_000 => (tokens / 1e6, "M"),
            >= 1_000 => (tokens / 1e3, "K"),
            _ => ((double)tokens, string.Empty)
        };
        var decimals = 0;
        while (decimals < 2 && Math.Abs(scaled - Math.Round(scaled, decimals)) > 1e-9)
        {
            decimals++;
        }
        return new ModelCardNumberValue(Math.Round(scaled, decimals), decimals, null, suffix);
    }

    /// <summary>A token count as text ("1M", "64K").</summary>
    public static string TokensText(long tokens) => Tokens(tokens).Text;

    /// <summary>An ISO month ("2026-03") as "Mar 2026"; anything else as is.</summary>
    public static string Month(string? cutoff, string notListed)
    {
        if (string.IsNullOrWhiteSpace(cutoff))
        {
            return notListed;
        }
        return TryMonth(cutoff, out var month) ? month.ToString("MMM yyyy", s_en) : cutoff;
    }

    /// <summary>Parses "yyyy-MM".</summary>
    public static bool TryMonth(string? cutoff, out DateTime month) =>
        DateTime.TryParseExact(cutoff?.Trim(), "yyyy-MM", s_en, DateTimeStyles.None, out month);

    /// <summary>The symbol written before prices in a currency (an unknown code is written with a space: "CHF 3.00").</summary>
    public static string CurrencyPrefix(string? currency) => (currency ?? "USD").ToUpperInvariant() switch
    {
        "USD" => "$",
        "EUR" => "€",
        "GBP" => "£",
        "JPY" => "¥",
        "CNY" => "¥",
        "INR" => "₹",
        "KRW" => "₩",
        var code => code + " "
    };

    /// <summary>A price per million tokens: two decimals, three when it needs them ("$0.075").</summary>
    public static ModelCardNumberValue Price(double price, string? currency)
    {
        var decimals = Math.Abs(price * 100 - Math.Round(price * 100)) > 1e-6 ? 3 : 2;
        return new ModelCardNumberValue(Math.Round(price, decimals), decimals, CurrencyPrefix(currency), null);
    }

    /// <summary>The estimated cost of a month: two decimals below 10,000, none above.</summary>
    public static ModelCardNumberValue Cost(double cost, string? currency, string? suffix = null)
    {
        var decimals = cost >= 10_000 ? 0 : 2;
        return new ModelCardNumberValue(Math.Round(cost, decimals), decimals, CurrencyPrefix(currency), suffix);
    }

    /// <summary>
    /// The cost of <paramref name="tokens"/> a month when <paramref name="inputShare"/> of them is billed at the input price
    /// and the rest at the output price; null without prices.
    /// </summary>
    public static double? MonthlyCost(ModelCardModel? model, long tokens, double inputShare)
    {
        if (model?.Pricing is not { } pricing)
        {
            return null;
        }
        var share = Math.Clamp(double.IsFinite(inputShare) ? inputShare : 0.75, 0, 1);
        var millions = tokens / 1e6;
        return millions * (share * pricing.Input + (1 - share) * pricing.Output);
    }

    /// <summary>About how many pages a context window holds.</summary>
    public static double Pages(long tokens) => Math.Round(tokens * PagesPerToken);

    /// <summary>A score with its unit ("82.1%").</summary>
    public static ModelCardNumberValue Score(double score, ModelCardBenchmark benchmark) =>
        new(Math.Round(score, 1), 1, null, benchmark.Unit);

    /// <summary>
    /// The rank of <paramref name="score"/> among <paramref name="all"/> (1 is the best; ties share the better rank).
    /// </summary>
    public static int Rank(double score, IReadOnlyList<double> all)
    {
        var better = 0;
        foreach (var other in all)
        {
            if (other > score + 1e-9)
            {
                better++;
            }
        }
        return better + 1;
    }

    /// <summary>A difference of scores ("2.3", "12").</summary>
    public static string Points(double difference) => Math.Round(Math.Abs(difference), 1).ToString("0.#", s_en);

    /// <summary>Which of two values is better (1 the first, 2 the second, 0 neither) and the margin it wins by.</summary>
    public static ModelCardVerdict Compare(double? first, double? second, ModelCardCompareKind kind)
    {
        if (first is not { } a || second is not { } b || Math.Abs(a - b) < 1e-9)
        {
            return ModelCardVerdict.Even;
        }
        var lowerWins = kind == ModelCardCompareKind.Price;
        var firstWins = lowerWins ? a < b : a > b;
        var better = firstWins ? a : b;
        var worse = firstWins ? b : a;
        return new ModelCardVerdict(firstWins ? 1 : 2, Margin(better, worse, kind));
    }

    /// <summary>The margin a better value wins by: "+2.3", "−20%", "+25%", "2x", "+3 mo".</summary>
    public static string Margin(double better, double worse, ModelCardCompareKind kind)
    {
        switch (kind)
        {
            case ModelCardCompareKind.Score:
                return "+" + Points(better - worse);
            case ModelCardCompareKind.Month:
                return "+" + Math.Round(Math.Abs(better - worse)).ToString("0", s_en) + " mo";
            case ModelCardCompareKind.Price:
                if (better <= 0)
                {
                    return worse > 0 ? "−100%" : string.Empty;
                }
                if (worse / better >= 2)
                {
                    return Multiple(worse / better);
                }
                return "−" + Math.Round((worse - better) / worse * 100).ToString("0", s_en) + "%";
            default:
                if (worse <= 0)
                {
                    return string.Empty;
                }
                if (better / worse >= 2)
                {
                    return Multiple(better / worse);
                }
                return "+" + Math.Round((better - worse) / worse * 100).ToString("0", s_en) + "%";
        }
    }

    /// <summary>A ratio as "2x", "2.5x", "12x".</summary>
    private static string Multiple(double ratio) =>
        (ratio >= 10 ? Math.Round(ratio) : Math.Round(ratio, 1)).ToString("0.#", s_en) + "x";

    /// <summary>A month as a number (for the cutoff margin), or null when it is not an ISO month.</summary>
    public static double? MonthNumber(string? cutoff) =>
        TryMonth(cutoff, out var month) ? month.Year * 12 + month.Month - 1 : null;
}
