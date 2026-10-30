namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="GenerationStats"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; the parts that hold a number or a name are format functions that receive it already formatted.
/// </summary>
public sealed class GenerationStatsLabels
{
    /// <summary>The English defaults.</summary>
    public static GenerationStatsLabels Default { get; } = new();

    /// <summary>The name of the strip, from the model's name: "{model} stats".</summary>
    public Func<string, string> Group { get; init; } = static model => $"{model} stats";

    /// <summary>The name shown when no model is given.</summary>
    public string UnknownModel { get; init; } = "Model";

    /// <summary>The status before the request is sent.</summary>
    public string Idle { get; init; } = "Idle";

    /// <summary>The status until the first token.</summary>
    public string Waiting { get; init; } = "Waiting";

    /// <summary>The status while tokens arrive.</summary>
    public string Streaming { get; init; } = "Streaming";

    /// <summary>The status of a finished response, from its total time: "Done in 4.18s".</summary>
    public Func<string, string> DoneIn { get; init; } = static time => $"Done in {time}";

    /// <summary>The status of a finished response whose time is not known.</summary>
    public string Done { get; init; } = "Done";

    /// <summary>The status of a stopped response, from its total time: "Stopped at 1.90s".</summary>
    public Func<string, string> StoppedAt { get; init; } = static time => $"Stopped at {time}";

    /// <summary>The status of a stopped response whose time is not known.</summary>
    public string Stopped { get; init; } = "Stopped";

    /// <summary>The status of a failed request.</summary>
    public string Failed { get; init; } = "Failed";

    /// <summary>The reason shown when a request failed without one.</summary>
    public string DefaultError { get; init; } = "The request failed.";

    /// <summary>The label of the first cell.</summary>
    public string FirstToken { get; init; } = "First token";

    /// <summary>The label of the second cell.</summary>
    public string Speed { get; init; } = "Speed";

    /// <summary>The label of the third cell.</summary>
    public string Output { get; init; } = "Output";

    /// <summary>The label of the fourth cell.</summary>
    public string Cost { get; init; } = "Cost";

    /// <summary>The unit after a speed: "88 tok/s".</summary>
    public string SpeedUnit { get; init; } = " tok/s";

    /// <summary>The words after the time to first token on the inline line: "412ms to first token".</summary>
    public string ToFirstToken { get; init; } = " to first token";

    /// <summary>The words after the output tokens on the inline line: "284 tokens".</summary>
    public string TokensUnit { get; init; } = " tokens";

    /// <summary>The symbol before a cost.</summary>
    public string Currency { get; init; } = "$";

    /// <summary>What a value without data reads.</summary>
    public string NoValue { get; init; } = "–";

    /// <summary>The Details button.</summary>
    public string Details { get; init; } = "Details";

    /// <summary>The name of the Details button while the breakdown is closed.</summary>
    public string ShowDetails { get; init; } = "Show details";

    /// <summary>The name of the Details button while the breakdown is open.</summary>
    public string HideDetails { get; init; } = "Hide details";

    /// <summary>The Retry button of a failed request.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The heading of the latency bar.</summary>
    public string Latency { get; init; } = "Latency";

    /// <summary>The first part of the latency bar: the wait for the first token.</summary>
    public string LatencyFirstToken { get; init; } = "First token";

    /// <summary>The second part of the latency bar: the time spent generating.</summary>
    public string Generation { get; init; } = "Generation";

    /// <summary>The whole time, in the latency legend and the table's last row.</summary>
    public string Total { get; init; } = "Total";

    /// <summary>The caption of the token table.</summary>
    public string TokenUsage { get; init; } = "Token usage";

    /// <summary>The table's first column header (the row names).</summary>
    public string TypeColumn { get; init; } = "Type";

    /// <summary>The table's token count column.</summary>
    public string TokensColumn { get; init; } = "Tokens";

    /// <summary>The table's rate column (hidden without prices and below 440px).</summary>
    public string RateColumn { get; init; } = "Rate";

    /// <summary>The table's cost column.</summary>
    public string CostColumn { get; init; } = "Cost";

    /// <summary>The input row (not cached).</summary>
    public string InputRow { get; init; } = "Input";

    /// <summary>The cached input row.</summary>
    public string CachedRow { get; init; } = "Cached input";

    /// <summary>The output row.</summary>
    public string OutputRow { get; init; } = "Output";

    /// <summary>The reasoning row.</summary>
    public string ReasoningRow { get; init; } = "Reasoning";

    /// <summary>A rate, from the formatted price per million tokens: "$3.00 / M".</summary>
    public Func<string, string> Rate { get; init; } = static price => $"{price} / M";

    /// <summary>A rate read aloud: "$3.00 per million tokens".</summary>
    public Func<string, string> RateSpoken { get; init; } = static price => $"{price} per million tokens";

    /// <summary>The name of the copy button.</summary>
    public string CopyJson { get; init; } = "Copy stats as JSON";

    /// <summary>A cell read aloud, from its label and value: "First token, 412ms".</summary>
    public Func<string, string, string> Cell { get; init; } = static (label, value) => $"{label}, {value}";

    /// <summary>
    /// The settled outcome read by the polite status region, from the model, the time, the output tokens, the speed and the
    /// cost (each formatted, or null when unknown): "{model} done in 4.18s, 284 output tokens at 88 tokens per
    /// second, $0.0077".
    /// </summary>
    public Func<string, string?, string?, string?, string?, string> DoneAnnouncement { get; init; } =
        static (model, time, tokens, speed, cost) =>
        {
            var text = time is null ? $"{model} done" : $"{model} done in {time}";
            if (tokens is not null)
            {
                text += $", {tokens} output tokens";
                if (speed is not null)
                {
                    text += $" at {speed} tokens per second";
                }
            }
            if (cost is not null)
            {
                text += $", {cost}";
            }
            return text;
        };

    /// <summary>A stop read aloud, from the model and the time: "{model} stopped at 1.90s".</summary>
    public Func<string, string?, string> StoppedAnnouncement { get; init; } =
        static (model, time) => time is null ? $"{model} stopped" : $"{model} stopped at {time}";

    /// <summary>A failure read aloud, from the model and the reason: "{model} failed: …".</summary>
    public Func<string, string, string> FailedAnnouncement { get; init; } = static (model, reason) => $"{model} failed: {reason}";

    /// <summary>Said once by the self-playing preview.</summary>
    public string SimulatedAnnouncement { get; init; } = "Generation stats preview. The runs shown are simulated.";

    /// <summary>The footnote of the self-playing preview's breakdown.</summary>
    public string SimulatedNote { get; init; } = "Simulated run. Nothing is sent anywhere.";
}
