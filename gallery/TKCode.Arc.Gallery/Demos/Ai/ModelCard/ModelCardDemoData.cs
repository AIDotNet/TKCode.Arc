namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// Real models for the ModelCard demos, read from <see cref="AiModelCatalog"/>: names, ids, list prices, limits, input types
/// and the benchmark scores each vendor publishes, checked on <see cref="AiModelCatalog.CheckedOn"/>. Code, Science and Shell
/// are SWE-bench Verified, GPQA Diamond and Terminal-Bench; each Terminal-Bench version is its own benchmark, so only
/// scores of one version are ranked against each other. A score a vendor does not publish is left out and reads as not
/// published. The summaries are neutral sentences made from the catalog, not the vendors' words. Marks are monograms.
/// </summary>
internal static class ModelCardDemoData
{
    private const string SweBenchId = "swe-bench-verified";
    private const string GpqaId = "gpqa-diamond";
    private const string TerminalBenchId = "terminal-bench-";

    /// <summary>The footnote of the compare table.</summary>
    public static string Note { get; } =
        $"List prices (standard tier) and vendor-published scores, checked {AiModelCatalog.CheckedOn}.";

    /// <summary>A missing value reads as not published, never as zero.</summary>
    public static ModelCardLabels Labels { get; } = new() { None = "Not published", NotListed = "Not published" };

    /// <summary>Frontier models that all publish a Terminal-Bench 4.0 score (the Shell tab).</summary>
    public static IReadOnlyList<ModelCardModel> Frontier { get; } =
        [.. new[] { AiModelCatalog.ClaudeOpus55, AiModelCatalog.Gpt6Astra, AiModelCatalog.Gemini4Argon, AiModelCatalog.Grok47 }.Select(ModelOf)];

    /// <summary>The benchmarks of <see cref="Frontier"/>: SWE-bench Verified, GPQA Diamond and Terminal-Bench 4.0.</summary>
    public static IReadOnlyList<ModelCardBenchmark> FrontierBenchmarks { get; } =
        [SweBench(), Gpqa(), TerminalBench("Terminal-Bench 4.0")];

    /// <summary>Models that publish GPQA Diamond (the Science tab) and, for two of them, Terminal-Bench 2.1.</summary>
    public static IReadOnlyList<ModelCardModel> OpenWeights { get; } =
        [.. new[] { AiModelCatalog.DeepSeekV4Pro, AiModelCatalog.Qwen38Max, AiModelCatalog.Qwen37Plus, AiModelCatalog.MistralSmall4 }.Select(ModelOf)];

    /// <summary>The benchmarks of <see cref="OpenWeights"/>: SWE-bench Verified, GPQA Diamond and Terminal-Bench 2.1.</summary>
    public static IReadOnlyList<ModelCardBenchmark> OpenWeightsBenchmarks { get; } =
        [SweBench(), Gpqa(), TerminalBench("Terminal-Bench 2.1")];

    /// <summary>The model most demos open on.</summary>
    public static ModelCardModel Opus { get; } = Frontier[0];

    /// <summary>A card model from a catalog entry; only the scores the vendor publishes are filled in.</summary>
    public static ModelCardModel ModelOf(AiModelInfo model)
    {
        var scores = new Dictionary<string, double>();
        if (model.SweBenchVerified is { } swe)
        {
            scores[SweBenchId] = swe.Value;
        }
        if (model.GpqaDiamond is { } gpqa)
        {
            scores[GpqaId] = gpqa.Value;
        }
        if (model.TerminalBench is { } terminal)
        {
            scores[TerminalBenchId + VersionOf(terminal.Version)] = terminal.Value;
        }
        return new ModelCardModel
        {
            Id = model.ApiId,
            Name = model.Name,
            Provider = model.Provider.Name,
            Summary = SummaryOf(model),
            Modalities = ModalitiesOf(model),
            ContextWindow = model.ContextWindow,
            MaxOutput = model.MaxOutputTokens,
            KnowledgeCutoff = model.KnowledgeCutoff,
            Pricing = new ModelCardPricing
            {
                Input = model.InputPerMTok,
                Output = model.OutputPerMTok,
                CachedInput = model.CachedInputPerMTok,
            },
            Scores = scores,
        };
    }

    private static ModelCardBenchmark SweBench() =>
        new() { Id = SweBenchId, Name = "SWE-bench Verified", ShortName = "Code", Description = "real GitHub issues resolved" };

    private static ModelCardBenchmark Gpqa() =>
        new() { Id = GpqaId, Name = "GPQA Diamond", ShortName = "Science", Description = "graduate level science questions" };

    private static ModelCardBenchmark TerminalBench(string version) =>
        new() { Id = TerminalBenchId + VersionOf(version), Name = version, ShortName = "Shell", Description = "tasks done in a terminal" };

    /// <summary>"Terminal-Bench 4.0" → "4.0".</summary>
    private static string VersionOf(string version) => version[(version.LastIndexOf(' ') + 1)..];

    private static ModelCardModalities ModalitiesOf(AiModelInfo model)
    {
        var flags = ModelCardModalities.None;
        foreach (var input in model.InputModalities)
        {
            flags |= input switch
            {
                "text" => ModelCardModalities.Text,
                "image" => ModelCardModalities.Image,
                "pdf" => ModelCardModalities.Pdf,
                "audio" => ModelCardModalities.Audio,
                "video" => ModelCardModalities.Video,
                _ => ModelCardModalities.None,
            };
        }
        if (model.SupportsTools)
        {
            flags |= ModelCardModalities.Tools;
        }
        if (model.SupportsReasoning)
        {
            flags |= ModelCardModalities.Reasoning;
        }
        return flags;
    }

    /// <summary>A neutral sentence from the catalog: who released it and when, and what it takes in.</summary>
    private static string SummaryOf(AiModelInfo model)
    {
        var who = model.ReleaseDate is { } date ? $"Released by {model.Provider.Name} on {date}" : $"A {model.Provider.Name} model";
        var inputs = model.InputModalities.Select(m => m switch
        {
            "pdf" => "PDFs",
            "image" => "images",
            "video" => "video",
            "audio" => "audio",
            _ => m,
        }).ToList();
        var takes = inputs.Count > 1 ? string.Join(", ", inputs.Take(inputs.Count - 1)) + " and " + inputs[^1] : inputs.FirstOrDefault() ?? "text";
        return $"{who}; takes {takes}.";
    }
}
