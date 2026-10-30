using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TKCode.Arc;

/// <summary>A number as the counting cells draw it: the value, its decimals and the text around it.</summary>
internal readonly record struct GenerationNumber(double Value, int Decimals, string? Prefix, string? Suffix)
{
    /// <summary>The number as text, as <see cref="TKCode.Arc.Primitives.RollingDigits"/> draws it (en-US grouping).</summary>
    public string Text => $"{Prefix}{Value.ToString("N" + Decimals, CultureInfo.InvariantCulture)}{Suffix}";
}

/// <summary>What one response cost, row by row of the breakdown table, in US dollars (null when unknown).</summary>
internal readonly record struct GenerationCostRows(double? Input, double? Cached, double? Output, double? Reasoning, double? Total);

/// <summary>
/// The pure logic of <see cref="GenerationStats"/>: how times, speeds, token counts, prices and costs are written, how
/// speed and cost are derived, the smoothing and compression of the speed series, and the copied JSON. All text is
/// invariant (en-US digits and grouping) and all rounding is half away from zero. Pure functions.
/// </summary>
internal static class GenerationStatsFormat
{
    private static readonly CultureInfo s_invariant = CultureInfo.InvariantCulture;

    /// <summary>The most speed readings the sparkline keeps; at the cap pairs merge and the interval doubles.</summary>
    public const int MaxSpeedSamples = 48;

    /// <summary>How often the speed is read for the sparkline while streaming.</summary>
    public static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(160);

    /// <summary>The weight of a new reading in the smoothed speed (an exponential moving average).</summary>
    public const double Smoothing = 0.35;

    /// <summary>
    /// A latency: whole milliseconds under one second ("412ms"), seconds with two decimals under 100s ("4.18s"), whole
    /// seconds after that ("125s").
    /// </summary>
    public static GenerationNumber Latency(double milliseconds)
    {
        var ms = Math.Max(0, milliseconds);
        var whole = Math.Round(ms, MidpointRounding.AwayFromZero);
        if (whole < 1000)
        {
            return new GenerationNumber(whole, 0, null, "ms");
        }
        var seconds = ms / 1000;
        return Math.Round(seconds, 2, MidpointRounding.AwayFromZero) < 100
            ? new GenerationNumber(Math.Round(seconds, 2, MidpointRounding.AwayFromZero), 2, null, "s")
            : new GenerationNumber(Math.Round(seconds, MidpointRounding.AwayFromZero), 0, null, "s");
    }

    /// <summary>A latency as text: "412ms", "4.18s".</summary>
    public static string LatencyText(double milliseconds) => Latency(milliseconds).Text;

    /// <summary>Whole tokens per second (the unit is added by the labels).</summary>
    public static GenerationNumber Speed(double tokensPerSecond, string? unit) =>
        new(Math.Round(Math.Max(0, tokensPerSecond), MidpointRounding.AwayFromZero), 0, null, unit);

    /// <summary>A speed as text without its unit: "88".</summary>
    public static string SpeedText(double tokensPerSecond) => Speed(tokensPerSecond, null).Text;

    /// <summary>A token count with grouping.</summary>
    public static GenerationNumber Tokens(long tokens, string? unit) => new(Math.Max(0, tokens), 0, null, unit);

    /// <summary>A token count as text: "1,284".</summary>
    public static string TokensText(long tokens) => Math.Max(0, tokens).ToString("N0", s_invariant);

    /// <summary>Four decimals under a dollar ("$0.0077"), two from a dollar on ("$1.24").</summary>
    public static int CostDecimals(double cost) => Math.Abs(cost) < 1 ? 4 : 2;

    /// <summary>A cost.</summary>
    public static GenerationNumber Cost(double cost, string currency)
    {
        var decimals = CostDecimals(cost);
        return new GenerationNumber(Math.Round(Math.Max(0, cost), decimals, MidpointRounding.AwayFromZero), decimals, currency, null);
    }

    /// <summary>A cost as text: "$0.0077".</summary>
    public static string CostText(double cost, string currency) => Cost(cost, currency).Text;

    /// <summary>A price per million tokens: two decimals ("$3.00"), three under ten cents ("$0.075").</summary>
    public static string PriceText(double perMillion, string currency)
    {
        var decimals = perMillion is > 0 and < 0.1 ? 3 : 2;
        return currency + Math.Round(Math.Max(0, perMillion), decimals, MidpointRounding.AwayFromZero).ToString("N" + decimals, s_invariant);
    }

    /// <summary>
    /// The speed of a finished or measured response: output tokens over the generation time (the total less the wait for
    /// the first token). Null without tokens or with less than 50ms of generation.
    /// </summary>
    public static double? DeriveSpeed(long? outputTokens, double? ttftMs, double? totalMs)
    {
        if (outputTokens is not { } tokens || totalMs is not { } total)
        {
            return null;
        }
        var generation = total - (ttftMs ?? 0);
        return generation >= 50 ? tokens / (generation / 1000) : null;
    }

    /// <summary>
    /// The cost of each row: uncached input at the input rate, cached input at the cached rate (the input rate when none),
    /// output and reasoning at the output rate. The total is <paramref name="reportedTotal"/> when the provider reports it.
    /// </summary>
    public static GenerationCostRows CostRows(GenerationUsage usage, GenerationPricing? pricing, double? reportedTotal = null)
    {
        if (pricing is null)
        {
            return new GenerationCostRows(null, null, null, null, reportedTotal);
        }
        var cached = Math.Max(0, usage.CachedTokens ?? 0);
        var input = usage.InputTokens is { } all ? Math.Max(0, all - cached) : (long?)null;
        double? Bill(long? tokens, double rate) => tokens is { } t ? t * rate / 1_000_000 : null;
        var inputCost = Bill(input, pricing.Input);
        var cachedCost = Bill(usage.CachedTokens is null ? null : cached, pricing.CachedInput ?? pricing.Input);
        var outputCost = Bill(usage.OutputTokens, pricing.Output);
        var reasoningCost = Bill(usage.ReasoningTokens, pricing.Output);
        double? total = reportedTotal;
        if (total is null && (inputCost ?? cachedCost ?? outputCost ?? reasoningCost) is not null)
        {
            total = (inputCost ?? 0) + (cachedCost ?? 0) + (outputCost ?? 0) + (reasoningCost ?? 0);
        }
        return new GenerationCostRows(inputCost, cachedCost, outputCost, reasoningCost, total);
    }

    /// <summary>The total cost: the reported one, else the one the prices give, else null.</summary>
    public static double? TotalCost(GenerationUsage usage, GenerationPricing? pricing) =>
        usage.CostUsd ?? CostRows(usage, pricing).Total;

    /// <summary>All tokens: input (cached included), output and reasoning; null when none is known.</summary>
    public static long? TotalTokens(GenerationUsage usage) =>
        usage.InputTokens is null && usage.OutputTokens is null && usage.ReasoningTokens is null
            ? null
            : (usage.InputTokens ?? 0) + (usage.OutputTokens ?? 0) + (usage.ReasoningTokens ?? 0);

    /// <summary>The next smoothed speed: the first reading as it is, then an exponential moving average.</summary>
    public static double Smooth(double? previous, double reading) =>
        previous is { } p ? p + Smoothing * (reading - p) : reading;

    /// <summary>
    /// Adds a reading to the series. At <see cref="MaxSpeedSamples"/> neighbouring pairs merge into their mean, so the
    /// series keeps the whole run's shape in half the points; returns true when it did (the caller doubles its interval).
    /// </summary>
    public static bool AddSample(List<double> samples, double reading)
    {
        samples.Add(reading);
        if (samples.Count < MaxSpeedSamples)
        {
            return false;
        }
        var merged = samples.Count / 2;
        for (var i = 0; i < merged; i++)
        {
            samples[i] = (samples[2 * i] + samples[2 * i + 1]) / 2;
        }
        if (samples.Count % 2 == 1)
        {
            samples[merged] = samples[^1];
            merged++;
        }
        samples.RemoveRange(merged, samples.Count - merged);
        return true;
    }

    /// <summary>The status as it is written in the copied JSON ("done").</summary>
    public static string StatusKey(GenerationStatus status) => status switch
    {
        GenerationStatus.Waiting => "waiting",
        GenerationStatus.Streaming => "streaming",
        GenerationStatus.Done => "done",
        GenerationStatus.Stopped => "stopped",
        GenerationStatus.Error => "error",
        _ => "idle",
    };

    /// <summary>
    /// The stats as indented JSON: the model (name, id, provider), the status, the usage, the times in milliseconds, the
    /// speed, the cost and the prices. Unknown values are left out. Written with <see cref="Utf8JsonWriter"/> (no reflection).
    /// </summary>
    public static string Json(GenerationModel? model, GenerationStatus status, GenerationUsage usage, GenerationPricing? pricing,
        double? ttftMs, double? durationMs, double? tokensPerSecond, double? costUsd, string? error)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            if (model is not null)
            {
                writer.WriteStartObject("model");
                writer.WriteString("name", model.Name);
                if (model.Id is { } id)
                {
                    writer.WriteString("id", id);
                }
                if (!string.IsNullOrWhiteSpace(model.Provider))
                {
                    writer.WriteString("provider", model.Provider);
                }
                writer.WriteEndObject();
            }
            writer.WriteString("status", StatusKey(status));
            if (status == GenerationStatus.Error && !string.IsNullOrEmpty(error))
            {
                writer.WriteString("error", error);
            }
            writer.WriteStartObject("usage");
            WriteNumber(writer, "inputTokens", usage.InputTokens);
            WriteNumber(writer, "cachedTokens", usage.CachedTokens);
            WriteNumber(writer, "outputTokens", usage.OutputTokens);
            WriteNumber(writer, "reasoningTokens", usage.ReasoningTokens);
            writer.WriteEndObject();
            WriteNumber(writer, "ttftMs", ttftMs is { } t ? Math.Round(t, MidpointRounding.AwayFromZero) : null);
            WriteNumber(writer, "durationMs", durationMs is { } d ? Math.Round(d, MidpointRounding.AwayFromZero) : null);
            WriteNumber(writer, "tokensPerSecond", tokensPerSecond is { } s ? Math.Round(s, 1, MidpointRounding.AwayFromZero) : null);
            WriteNumber(writer, "costUsd", costUsd is { } c ? Math.Round(c, 6, MidpointRounding.AwayFromZero) : null);
            if (pricing is not null)
            {
                writer.WriteStartObject("pricing");
                writer.WriteNumber("input", pricing.Input);
                if (pricing.CachedInput is { } cached)
                {
                    writer.WriteNumber("cachedInput", cached);
                }
                writer.WriteNumber("output", pricing.Output);
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteNumber(Utf8JsonWriter writer, string name, long? value)
    {
        if (value is { } v)
        {
            writer.WriteNumber(name, v);
        }
    }

    private static void WriteNumber(Utf8JsonWriter writer, string name, double? value)
    {
        if (value is { } v)
        {
            writer.WriteNumber(name, v);
        }
    }
}
