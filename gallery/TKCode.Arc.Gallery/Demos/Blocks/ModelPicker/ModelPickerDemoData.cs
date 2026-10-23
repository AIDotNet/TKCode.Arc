using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>An invented catalog for the model picker demos: made-up providers with geometric marks and made-up models.</summary>
public static class ModelPickerDemoData
{
    /// <summary>The small print under the summary in the demos.</summary>
    public const string PriceNote = "Prices in USD per 1M tokens · demo values";

    private static readonly ModelEffort[] ThreeLevels = [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High];

    private static readonly ModelEffort[] FiveLevels =
        [ModelEffort.Low, ModelEffort.Medium, ModelEffort.High, ModelEffort.ExtraHigh, ModelEffort.Max];

    public static IReadOnlyList<AiModelProvider> Providers() =>
    [
        Provider("aurora", "Aurora Labs", "#D9822B", "M2 19a10 10 0 0 1 20 0h-3.2a6.8 6.8 0 0 0-13.6 0Z M7 19a5 5 0 0 1 10 0h-3a2 2 0 0 0-4 0Z"),
        Provider("cobalt", "Cobalt Research", "#3E63DD", "M12 2L22 12L12 22L2 12Z M12 5.2L5.2 12L12 18.8L18.8 12Z M9.5 12A2.5 2.5 0 1 0 14.5 12A2.5 2.5 0 1 0 9.5 12Z"),
        new AiModelProvider { Id = "halcyon", Name = "Halcyon", Mark = TwoCircles() },
        Provider("basalt", "Basalt AI", null, "M12 2L21 7L21 17L12 22L3 17L3 7Z M12 5L6 8.5L6 15.5L12 19L18 15.5L18 8.5Z"),
        Provider("driftwood", "Driftwood", "#12A594", "M2 9Q7 5 12 9T22 9L22 12Q17 8 12 12T2 12Z M2 15Q7 11 12 15T22 15L22 18Q17 14 12 18T2 18Z"),
        Provider("ember", "Ember Systems", "#E5484D", "M12 3.5L21 19.5L3 19.5Z"),
        Provider("fjord", "Fjord", null, "M4 10H8V20H4Z M10 4H14V20H10Z M16 13H20V20H16Z"),
        Provider("granite", "Granite Open", null, "M12 2A10 10 0 1 1 11.99 2Z M4.5 16.5L16.5 4.5L19.5 7.5L7.5 19.5Z")
    ];

    /// <summary>Four featured models first, then the rest of the catalog.</summary>
    public static IReadOnlyList<AiModel> Models() =>
    [
        Model("aurora-prime-3", "Aurora Prime 3", "aurora", "Deep work: refactors, research, long tasks", null,
              "A careful planner for multi-step coding and research; raise the effort when a task needs more thought.",
              800_000, 6m, 24m, FiveLevels, ModelEffort.Medium),
        Model("aurora-swift-3", "Aurora Swift 3", "aurora", "Quick and capable for daily chats", null,
              "Fast replies with solid tool use; the default for most conversations.",
              800_000, 1.5m, 6m, FiveLevels, ModelEffort.High),
        Model("cobalt-atlas-2", "Cobalt Atlas 2", "cobalt", "Reads charts, screenshots and PDFs", null,
              "Understands images, tables and scanned pages alongside text.",
              256_000, 3m, 12m, ThreeLevels, ModelEffort.Medium),
        Model("halcyon-wide-15", "Halcyon Wide 1.5", "halcyon", "Huge context for whole codebases", null,
              "Keeps an entire repository or a stack of contracts in view at once, at the cost of some speed.",
              3_000_000, 2.5m, 15m, ThreeLevels, ModelEffort.Medium),
        Model("aurora-summit-29", "Aurora Summit 2.9", "aurora", null, "Strongest",
              "The largest model in the lineup. It keeps thinking on for the hardest problems in research and engineering, so replies take longer and cost more.",
              800_000, 12m, 60m, FiveLevels, ModelEffort.High),
        Model("aurora-pocket-24", "Aurora Pocket 2.4", "aurora", null, "Fast",
              "A small model for quick lookups and short rewrites.",
              400_000, 0.4m, 1.6m, FiveLevels, ModelEffort.Medium),
        Model("cobalt-atlas-lite", "Cobalt Atlas Lite", "cobalt", null, "Fast",
              "A small model for tagging, extraction and replies that must feel instant.",
              128_000, 0.3m, 1.2m, null, null),
        Model("halcyon-flash-12", "Halcyon Flash 1.2", "halcyon", null, "Fast",
              "Very low latency with a long window, for streaming summaries.",
              1_000_000, 0.2m, 0.8m, ThreeLevels, ModelEffort.Low),
        Model("basalt-4", "Basalt 4", "basalt", null, null,
              "A steady general-purpose model with predictable output.",
              200_000, 2m, 8m, ThreeLevels, ModelEffort.Medium),
        Model("driftwood-coder-7", "Driftwood Coder 7", "driftwood", null, "Code",
              "Tuned for code review, refactors and test generation.",
              256_000, 1.8m, 7.2m, ThreeLevels, ModelEffort.Medium),
        Model("ember-large-3", "Ember Large 3", "ember", null, null,
              "A large model for long-form writing and analysis.",
              128_000, 4m, 16m, null, null),
        Model("fjord-reason-2", "Fjord Reason 2", "fjord", null, "New",
              "Works through problems step by step before it answers.",
              200_000, 5m, 20m, ThreeLevels, ModelEffort.High),
        Model("granite-70b", "Granite 70B", "granite", null, "Open weights",
              "An open-weights model you can run on your own hardware.",
              128_000, 0.6m, 0.6m, null, null)
    ];

    private static AiModel Model(string id, string name, string provider, string? blurb, string? tag, string description, long context,
                                 decimal input, decimal output, IReadOnlyList<ModelEffort>? levels, ModelEffort? defaultEffort) => new()
    {
        Id = id,
        Name = name,
        ProviderId = provider,
        Blurb = blurb,
        Tag = tag,
        Description = description,
        ContextTokens = context,
        MaxOutputTokens = context / 8,
        InputPrice = input,
        OutputPrice = output,
        IsSamplePrice = true,
        EffortLevels = levels,
        DefaultEffort = defaultEffort
    };

    private static AiModelProvider Provider(string id, string name, string? color, string mark) => new()
    {
        Id = id,
        Name = name,
        Mark = Geometry.Parse(mark),
        MarkColor = color is null ? null : new ImmutableSolidColorBrush(Color.Parse(color))
    };

    /// <summary>A multicolor mark: a drawing keeps its own colors in both themes.</summary>
    private static DrawingImage TwoCircles() => new(new DrawingGroup
    {
        Children =
        {
            new GeometryDrawing { Brush = new ImmutableSolidColorBrush(Color.Parse("#8E4EC6")), Geometry = Geometry.Parse("M3 12A6 6 0 1 1 15 12A6 6 0 1 1 3 12Z") },
            new GeometryDrawing { Brush = new ImmutableSolidColorBrush(Color.Parse("#D6409F")), Geometry = Geometry.Parse("M9 12A6 6 0 1 1 21 12A6 6 0 1 1 9 12Z") }
        }
    });
}
