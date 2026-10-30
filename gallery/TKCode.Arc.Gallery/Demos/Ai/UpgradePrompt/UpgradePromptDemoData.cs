using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The offer the upgrade prompt demos show: an invented product's Pro plan at $20 a month or $16 a month billed yearly,
/// with 2,000 AI credits, a priority queue and access to real models from <see cref="AiModelCatalog"/>. The plan, its
/// prices and limits belong to the invented product; they are not a model vendor's prices. Demo content only; the model
/// marks are Lucide glyphs, not the vendors' logos.
/// </summary>
internal static class UpgradePromptDemoData
{
    /// <summary>The mark for Claude Opus 5.5: Lucide <c>sparkles</c> in violet.</summary>
    private static readonly IBrush s_topModel = new ImmutableSolidColorBrush(Color.FromRgb(0x7C, 0x5C, 0xE6));

    /// <summary>The mark for image input with Gemini 4 Argon: Lucide <c>eye</c> in teal.</summary>
    private static readonly IBrush s_vision = new ImmutableSolidColorBrush(Color.FromRgb(0x0E, 0x95, 0x8A));

    /// <summary>The Pro plan the demos offer (shared: the prompt only reads it).</summary>
    public static UpgradePromptPlan ProPlan { get; } = new()
    {
        Name = "Pro",
        MonthlyPrice = 20,
        YearlyPrice = 16,
        Currency = "USD",
        Limit = 2000,
        Features =
        [
            new UpgradePromptFeature("credits", "2,000 AI credits every month", ArcIcons.Zap),
            new UpgradePromptFeature("model", $"{AiModelCatalog.ClaudeOpus55.Name} for your hardest tasks", ArcIcons.Sparkles, s_topModel),
            new UpgradePromptFeature("queue", "Priority queue, no waiting at peak times", ArcIcons.Timer)
        ],
        Limits =
        [
            new UpgradePromptLimit("AI credits", "2,000 a month"),
            new UpgradePromptLimit("Models", AiModelCatalog.ClaudeOpus55.Name, ArcIcons.Sparkles, s_topModel),
            new UpgradePromptLimit("Image input", AiModelCatalog.Gemini4Argon.Name, ArcIcons.Eye, s_vision),
            new UpgradePromptLimit("Queue", "Priority")
        ]
    };

    /// <summary>The Team plan's shared-workspace unlock, naming a real model.</summary>
    public static string SharedWorkspace { get; } = $"Shared {AiModelCatalog.ClaudeOpus55.Name} workspace";
}
