namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The responses the GenerationStats demos play: real models with their vendors' list prices from
/// <see cref="AiModelCatalog"/> (checked on <see cref="AiModelCatalog.CheckedOn"/>); the token counts, timings and the
/// failure are invented.
/// </summary>
internal static class GenerationStatsDemoData
{
    /// <summary>The footnote under the breakdown of a simulated strip.</summary>
    public static string SimulatedNote { get; } =
        $"Simulated runs: the tokens and timings are invented. Rates are each vendor's list price, checked {AiModelCatalog.CheckedOn}.";

    /// <summary>The footnote of the demos fed by hand.</summary>
    public static string ListPriceNote { get; } =
        $"Rates are the vendor's list prices, checked {AiModelCatalog.CheckedOn}.";

    /// <summary>The model of the demos fed by hand.</summary>
    public static GenerationModel Sonnet { get; } = ModelOf(AiModelCatalog.ClaudeSonnet55);

    /// <summary>Its list prices.</summary>
    public static GenerationPricing SonnetPricing { get; } = PricingOf(AiModelCatalog.ClaudeSonnet55);

    /// <summary>Four runs: three settle, the last one fails once and then succeeds when retried.</summary>
    public static IReadOnlyList<GenerationStatsRun> Runs { get; } =
    [
        Run(AiModelCatalog.ClaudeSonnet55, input: 3_682, cached: 2_816, output: 284, reasoning: 0, ttftMs: 412, peak: 96),
        Run(AiModelCatalog.Gpt61Sol, input: 5_120, cached: 4_096, output: 412, reasoning: 768, ttftMs: 1_180, peak: 132),
        Run(AiModelCatalog.Gemini38Flash, input: 2_048, cached: 0, output: 356, reasoning: 0, ttftMs: 286, peak: 214),
        Run(AiModelCatalog.MistralMedium35, input: 1_890, cached: 0, output: 318, reasoning: 0, ttftMs: 520, peak: 72) with
        {
            FailAtTokens = 120,
            ErrorText = "The connection dropped before the response finished.",
        },
    ];

    /// <summary>The strip's model from a catalog entry: its name, the provider key and the API id.</summary>
    public static GenerationModel ModelOf(AiModelInfo model) => new(model.Name, model.Provider.Key, model.ApiId);

    /// <summary>The catalog's list prices (US dollars per million tokens).</summary>
    public static GenerationPricing PricingOf(AiModelInfo model) =>
        new(model.InputPerMTok, model.OutputPerMTok, model.CachedInputPerMTok);

    private static GenerationStatsRun Run(AiModelInfo model, long input, long cached, long output, long reasoning, double ttftMs,
        double peak) => new()
    {
        Model = ModelOf(model),
        Pricing = PricingOf(model),
        InputTokens = input,
        CachedTokens = cached,
        OutputTokens = output,
        ReasoningTokens = reasoning,
        TimeToFirstToken = TimeSpan.FromMilliseconds(ttftMs),
        PeakTokensPerSecond = peak,
    };
}
