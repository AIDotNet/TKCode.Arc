namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="LlmCostCalculator"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; counts, prices and sentences are format functions. Money reaches them already formatted ("$1,234.56").
/// </summary>
public sealed class LlmCostCalculatorLabels
{
    /// <summary>The English defaults.</summary>
    public static LlmCostCalculatorLabels Default { get; } = new();

    /// <summary>The heading, unless <see cref="LlmCostCalculator.Title"/> is set.</summary>
    public string Title { get; init; } = "Estimated monthly cost";

    /// <summary>The line under the heading, unless <see cref="LlmCostCalculator.Description"/> is set.</summary>
    public string Description { get; init; } = "Prices per 1M tokens";

    /// <summary>The fine print, unless <see cref="LlmCostCalculator.Note"/> is set.</summary>
    public string Note { get; init; } = "An estimate, not a quote. Calculated on this device, nothing was sent.";

    /// <summary>The unit after the large total.</summary>
    public string PerMonth { get; init; } = "/mo";

    /// <summary>The name of the presets row.</summary>
    public string Workload { get; init; } = "Workload";

    /// <summary>The segment that lights up when the sliders match no preset.</summary>
    public string Custom { get; init; } = "Custom";

    /// <summary>The input tokens per request slider.</summary>
    public string InputTokens { get; init; } = "Input tokens";

    /// <summary>The output tokens per request slider.</summary>
    public string OutputTokens { get; init; } = "Output tokens";

    /// <summary>The requests slider.</summary>
    public string RequestsPerDay { get; init; } = "Requests per day";

    /// <summary>A slider's value, shown and spoken: "16,000".</summary>
    public Func<int, string> Count { get; init; } = value => LlmCostCalculatorMath.Count(value);

    /// <summary>The cache switch.</summary>
    public string CachedInput { get; init; } = "Cached input";

    /// <summary>The note beside the cache switch, from the cache hit rate (0 to 1): "60% at the cache rate".</summary>
    public Func<double, string> CacheShare { get; init; } = rate => $"{Math.Round(rate * 100)}% at the cache rate";

    /// <summary>The name of the model list.</summary>
    public string ModelList { get; init; } = "Models ranked by monthly cost, lowest first";

    /// <summary>
    /// A row's name, from the model, its provider, its rank, the number of models and its monthly cost:
    /// "{name}, {provider}, rank 2 of 8, $1,234 a month".
    /// </summary>
    public Func<string, string, int, int, string, string> Row { get; init; } =
        (name, provider, rank, count, cost) => provider.Length > 0
            ? $"{name}, {provider}, rank {rank} of {count}, {cost} a month"
            : $"{name}, rank {rank} of {count}, {cost} a month";

    /// <summary>The model under the total: "{name} · {provider}".</summary>
    public Func<string, string, string> Selected { get; init; } =
        (name, provider) => provider.Length > 0 ? $"{name} · {provider}" : name;

    /// <summary>The breakdown: uncached input.</summary>
    public string Input { get; init; } = "Input";

    /// <summary>The breakdown: input billed at the cache rate.</summary>
    public string Cached { get; init; } = "Cached";

    /// <summary>The breakdown: output.</summary>
    public string Output { get; init; } = "Output";

    /// <summary>The breakdown: one request.</summary>
    public string PerRequest { get; init; } = "Per request";

    /// <summary>
    /// The selected model's price line, from its input, cached input (null when it has no cache price) and output prices:
    /// "$3 in · $0.30 cached · $15 out per 1M tokens".
    /// </summary>
    public Func<string, string?, string, string> PriceLine { get; init; } =
        (input, cached, output) => cached is null
            ? $"{input} in · {output} out per 1M tokens"
            : $"{input} in · {cached} cached · {output} out per 1M tokens";

    /// <summary>The name of the list area while prices load.</summary>
    public string Loading { get; init; } = "Loading prices";

    /// <summary>The error state's heading.</summary>
    public string ErrorTitle { get; init; } = "Prices did not load";

    /// <summary>The error state's button.</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>The empty state's heading.</summary>
    public string EmptyTitle { get; init; } = "No models to compare";

    /// <summary>The empty state's line.</summary>
    public string EmptyText { get; init; } = "Add models with their prices to see them ranked.";

    /// <summary>
    /// What the status region says once the numbers settle, from the model, its monthly cost, its rank and the number of
    /// models: "{name} costs $1,234.56 a month, rank 2 of 8."
    /// </summary>
    public Func<string, string, int, int, string> Status { get; init; } =
        (name, cost, rank, count) => $"{name} costs {cost} a month, rank {rank} of {count}.";
}
