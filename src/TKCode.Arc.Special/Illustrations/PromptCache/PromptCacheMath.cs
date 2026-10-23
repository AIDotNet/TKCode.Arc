using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// The arithmetic of <see cref="PromptCache"/>, free of UI so it can be tested against the measured numbers: the outcome of
/// a request (write, hit or partial hit) and its cost and first-token time, the round's summary statistics, the number
/// formats, the time-to-live countdown and the ring's follow.
/// </summary>
internal static class PromptCacheMath
{
    /// <summary>Remaining seconds at or under which the ring turns to the warning color.</summary>
    public const double LowSeconds = 60;

    /// <summary>The ring's display value moves toward its target with this time constant (≈ 21% of the distance per 60 Hz frame).</summary>
    public const double RingFollowSeconds = 0.068;

    /// <summary>The default first-token model: a fixed overhead, prefill of new tokens and a much faster load of cached ones.</summary>
    public static double DefaultFirstToken(int processedTokens, int cachedTokens)
        => 0.40 + processedTokens / 45_000.0 + cachedTokens / 600_000.0;

    /// <summary>The sum of <paramref name="tokens"/> from <paramref name="from"/> (inclusive) to <paramref name="to"/> (exclusive).</summary>
    public static int Sum(IReadOnlyList<int> tokens, int from = 0, int to = int.MaxValue)
    {
        var sum = 0;
        for (var i = Math.Max(0, from); i < Math.Min(to, tokens.Count); i++)
        {
            sum += Math.Max(0, tokens[i]);
        }
        return sum;
    }

    /// <summary>
    /// The kind of a request and where its match breaks: a write when the cache is cold, a partial hit at
    /// <paramref name="changeIndex"/> when a change is armed (a write when it is the first segment), a hit otherwise.
    /// </summary>
    public static (PromptCacheRequestKind Kind, int? Break) Classify(bool warm, bool armed, int changeIndex, int segmentCount)
    {
        if (!warm)
        {
            return (PromptCacheRequestKind.Write, null);
        }
        if (armed && changeIndex >= 0 && changeIndex < segmentCount)
        {
            return changeIndex == 0 ? (PromptCacheRequestKind.Write, null) : (PromptCacheRequestKind.Partial, changeIndex);
        }
        return (PromptCacheRequestKind.Hit, null);
    }

    /// <summary>Computes the outcome of one request.</summary>
    /// <param name="segments">The prefix segments' token counts, in prompt order.</param>
    /// <param name="messageTokens">The new message's token count.</param>
    /// <param name="kind">Write, hit or partial hit (from <see cref="Classify"/>).</param>
    /// <param name="breakIndex">The first changed segment of a partial hit.</param>
    /// <param name="pricing">Dollars per million tokens.</param>
    /// <param name="firstToken">The first-token estimator (seconds from processed and cached tokens).</param>
    public static PromptCacheResult Compute(IReadOnlyList<int> segments, int messageTokens, PromptCacheRequestKind kind,
        int? breakIndex, PromptCachePricing pricing, PromptCacheFirstTokenEstimator firstToken)
    {
        var prefix = Sum(segments);
        var message = Math.Max(0, messageTokens);
        var input = pricing.InputPrice / 1e6;
        var write = pricing.CacheWritePrice / 1e6;
        var read = pricing.CacheReadPrice / 1e6;
        int cached, written;
        switch (kind)
        {
            case PromptCacheRequestKind.Hit:
                cached = prefix;
                written = 0;
                break;
            case PromptCacheRequestKind.Partial when breakIndex is { } k:
                cached = Sum(segments, 0, k);
                written = prefix - cached;
                break;
            default:
                cached = 0;
                written = prefix;
                break;
        }
        var processed = written + message;
        return new PromptCacheResult
        {
            Kind = kind,
            BreakIndex = kind == PromptCacheRequestKind.Partial ? breakIndex : null,
            CachedTokens = cached,
            WrittenTokens = written,
            ProcessedTokens = processed,
            Cost = cached * read + written * write + message * input,
            UncachedCost = (prefix + message) * input,
            FirstToken = Safe(firstToken(processed, cached)),
            UncachedFirstToken = Safe(firstToken(prefix + message, 0))
        };
    }

    private static double Safe(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;

    /// <summary>
    /// The cost summary of the requests so far: the totals with and without the cache, the rounded percentage between them
    /// and whether the cached total is higher (the first write costs more than an uncached request).
    /// </summary>
    public static (double Cost, double Uncached, int Percent, bool Higher) CostSummary(IEnumerable<PromptCacheResult> results)
    {
        double cost = 0, uncached = 0;
        foreach (var r in results)
        {
            cost += r.Cost;
            uncached += r.UncachedCost;
        }
        if (uncached <= 0)
        {
            return (cost, uncached, 0, false);
        }
        var ratio = cost / uncached;
        return (cost, uncached, (int)Math.Round(Math.Abs(1 - ratio) * 100, MidpointRounding.AwayFromZero), ratio > 1);
    }

    /// <summary>
    /// The first-token summary over cache hits only (writes and partial hits are left out): how many hits, their average
    /// first token with and without the cache, and the speedup (0 without hits).
    /// </summary>
    public static (int Hits, double FirstToken, double Uncached, double Speedup) SpeedSummary(IEnumerable<PromptCacheResult> results)
    {
        var hits = 0;
        double first = 0, uncached = 0;
        foreach (var r in results)
        {
            if (r.Kind != PromptCacheRequestKind.Hit)
            {
                continue;
            }
            hits++;
            first += r.FirstToken;
            uncached += r.UncachedFirstToken;
        }
        if (hits == 0)
        {
            return (0, 0, 0, 0);
        }
        first /= hits;
        uncached /= hits;
        return (hits, first, uncached, first > 0 ? uncached / first : 0);
    }

    /// <summary>A token count: whole numbers under 1,000, then one decimal with K (or M), dropping ".0" (6K, 48.6K, 240).</summary>
    public static string FormatTokens(double tokens)
    {
        var value = Math.Max(0, tokens);
        var whole = Math.Round(value, MidpointRounding.AwayFromZero);
        if (whole < 1000)
        {
            return whole.ToString("0", CultureInfo.InvariantCulture);
        }
        var thousands = Math.Round(value / 100, MidpointRounding.AwayFromZero) / 10;
        if (thousands < 1000)
        {
            return thousands.ToString("0.#", CultureInfo.InvariantCulture) + "K";
        }
        var millions = Math.Round(value / 100_000, MidpointRounding.AwayFromZero) / 10;
        return millions.ToString("0.#", CultureInfo.InvariantCulture) + "M";
    }

    /// <summary>A cost: "$" and four decimals.</summary>
    public static string FormatCost(double dollars) => "$" + Math.Max(0, dollars).ToString("0.0000", CultureInfo.InvariantCulture);

    /// <summary>A price per million tokens: "$" and two decimals.</summary>
    public static string FormatPrice(double dollars) => "$" + Math.Max(0, dollars).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Seconds: two decimals and "s".</summary>
    public static string FormatSeconds(double seconds) => Math.Max(0, seconds).ToString("0.00", CultureInfo.InvariantCulture) + "s";

    /// <summary>A speedup: one decimal and "x".</summary>
    public static string FormatSpeedup(double ratio) => Math.Max(0, ratio).ToString("0.0", CultureInfo.InvariantCulture) + "x";

    /// <summary>A percentage: a whole number and "%".</summary>
    public static string FormatPercent(double percent)
        => Math.Round(Math.Max(0, percent), MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>A clock reading m:ss of whole <paramref name="seconds"/>.</summary>
    public static string FormatClock(double seconds)
    {
        var total = (int)Math.Max(0, Math.Floor(seconds + 1e-9));
        return (total / 60).ToString(CultureInfo.InvariantCulture) + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>The ring and countdown's remaining whole seconds: ceil(ttl − elapsed), kept within 0 and the time to live.</summary>
    public static int RemainingSeconds(double elapsed, double ttl)
    {
        if (!(ttl > 0))
        {
            return 0;
        }
        var remaining = Math.Ceiling(ttl - Math.Max(0, elapsed) - 1e-9);
        return (int)Math.Clamp(remaining, 0, Math.Ceiling(ttl));
    }

    /// <summary>The share of the time to live that has passed (0 = just used, 1 = expired).</summary>
    public static double Drained(double elapsed, double ttl) => ttl > 0 ? Math.Clamp(elapsed / ttl, 0, 1) : 1;

    /// <summary>True when at most <see cref="LowSeconds"/> remain.</summary>
    public static bool IsLow(double elapsed, double ttl) => RemainingSeconds(elapsed, ttl) <= LowSeconds;

    /// <summary>The ring's display value after <paramref name="dt"/> story seconds of following <paramref name="target"/>.</summary>
    public static double Follow(double value, double target, double dt)
        => dt <= 0 ? value : value + (target - value) * (1 - Math.Exp(-dt / RingFollowSeconds));

    /// <summary>A time to live as words for the header ("5-minute cache", "90-second cache", "1-hour cache").</summary>
    public static string DescribeTtl(double ttl)
    {
        var seconds = (int)Math.Round(Math.Max(0, ttl));
        if (seconds >= 3600 && seconds % 3600 == 0)
        {
            return (seconds / 3600).ToString(CultureInfo.InvariantCulture) + "-hour";
        }
        if (seconds >= 60 && seconds % 60 == 0)
        {
            return (seconds / 60).ToString(CultureInfo.InvariantCulture) + "-minute";
        }
        return seconds.ToString(CultureInfo.InvariantCulture) + "-second";
    }

    /// <summary>A time to live as a duration in a sentence ("5 minutes", "90 seconds", "1 hour").</summary>
    public static string DurationWords(double ttl)
    {
        var seconds = (int)Math.Round(Math.Max(0, ttl));
        if (seconds >= 3600 && seconds % 3600 == 0)
        {
            var h = seconds / 3600;
            return h == 1 ? "1 hour" : h.ToString(CultureInfo.InvariantCulture) + " hours";
        }
        if (seconds >= 60 && seconds % 60 == 0)
        {
            var m = seconds / 60;
            return m == 1 ? "1 minute" : m.ToString(CultureInfo.InvariantCulture) + " minutes";
        }
        return seconds == 1 ? "1 second" : seconds.ToString(CultureInfo.InvariantCulture) + " seconds";
    }

    /// <summary>A price ratio for captions ("0.1", "1.25").</summary>
    public static string FormatRatio(double ratio) => Math.Max(0, ratio).ToString("0.##", CultureInfo.InvariantCulture);
}
