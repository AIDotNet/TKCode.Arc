namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// Real models for the LlmCostCalculator demos with their vendors' list prices from <see cref="AiModelCatalog"/> (standard
/// tier, US dollars per 1M tokens, checked on <see cref="AiModelCatalog.CheckedOn"/>), and invented workload presets. Marks
/// are left out, so each row shows its provider's initial.
/// </summary>
internal static class LlmCostCalculatorDemoData
{
    private static readonly AiModelInfo[] s_compared =
    [
        AiModelCatalog.ClaudeHaiku55, AiModelCatalog.ClaudeSonnet55, AiModelCatalog.Gpt61Terra, AiModelCatalog.Gpt6Astra,
        AiModelCatalog.Gemini38Flash, AiModelCatalog.Grok47, AiModelCatalog.MistralMedium35, AiModelCatalog.DeepSeekV4Pro,
    ];

    private static readonly AiModelInfo[] s_approved =
    [
        AiModelCatalog.Gpt61Sol, AiModelCatalog.ClaudeSonnet55, AiModelCatalog.Qwen38Max, AiModelCatalog.MistralLarge3,
    ];

    /// <summary>Eight models from seven vendors.</summary>
    public static IList<LlmModelPrice> Models { get; } = [.. s_compared.Select(PriceOf)];

    /// <summary>A team's short list of approved models, for the demo with its own presets.</summary>
    public static IList<LlmModelPrice> ApprovedModels { get; } = [.. s_approved.Select(PriceOf)];

    /// <summary>The line under the heading.</summary>
    public static string Description { get; } = $"List prices per 1M tokens, checked {AiModelCatalog.CheckedOn}";

    /// <summary>The fine print: the prices are list prices on the checked day, not a quote.</summary>
    public static string Note { get; } = NoteFor(s_compared, null);

    /// <summary>The fine print of the business-days demo.</summary>
    public static string BusinessDaysNote { get; } = NoteFor(s_compared, "22 billable days a month");

    /// <summary>The fine print of the approved-models demo.</summary>
    public static string ApprovedNote { get; } = NoteFor(s_approved, null);

    /// <summary>Chat, RAG, Agent and Batch: invented workloads.</summary>
    public static IList<LlmUsagePreset> Presets { get; } =
    [
        new("chat", "Chat", new LlmUsage(2_000, 500, 10_000, false)),
        new("rag", "RAG", new LlmUsage(16_000, 500, 5_000, true)),
        new("agent", "Agent", new LlmUsage(32_000, 1_000, 2_500, true)),
        new("batch", "Batch", new LlmUsage(4_000, 1_000, 100_000, false)),
    ];

    /// <summary>A team's own workloads.</summary>
    public static IList<LlmUsagePreset> TeamPresets { get; } =
    [
        new("support", "Support", new LlmUsage(6_000, 400, 20_000, true)),
        new("search", "Search", new LlmUsage(16_000, 250, 50_000, true)),
        new("reports", "Reports", new LlmUsage(64_000, 4_000, 500, false)),
    ];

    /// <summary>A catalog model as a row: its API id, name, provider and list prices.</summary>
    public static LlmModelPrice PriceOf(AiModelInfo model) =>
        new(model.ApiId, model.Name, model.Provider.Name, model.InputPerMTok, model.OutputPerMTok, model.CachedInputPerMTok);

    /// <summary>
    /// "Vendors' list prices (standard tier, short prompts), checked …, not a quote", naming the models whose price is
    /// introductory (the catalog's price note says so).
    /// </summary>
    private static string NoteFor(IEnumerable<AiModelInfo> models, string? extra)
    {
        var note = $"Vendors' list prices (standard tier, short prompts), checked {AiModelCatalog.CheckedOn}, not a quote.";
        var introductory = models.Where(m => m.PriceTierNote?.StartsWith("Introductory", StringComparison.Ordinal) == true)
            .Select(m => m.Name).ToList();
        if (introductory.Count > 0)
        {
            note += $" Introductory price: {string.Join(", ", introductory)}.";
        }
        if (extra is not null)
        {
            note += $" {extra}.";
        }
        return note + " Calculated on this device, nothing was sent.";
    }
}
