using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Which view a <see cref="ModelCard"/> shows.</summary>
public enum ModelCardView
{
    /// <summary>The model's summary, modalities, limits, prices, cost estimate and benchmarks.</summary>
    Details,

    /// <summary>A side by side table against one rival.</summary>
    Compare
}

/// <summary>What a model takes in or can do. Combine the flags; the card always shows all seven chips.</summary>
[Flags]
public enum ModelCardModalities
{
    /// <summary>Nothing listed.</summary>
    None = 0,

    /// <summary>Text in and out.</summary>
    Text = 1,

    /// <summary>Images as input.</summary>
    Image = 2,

    /// <summary>PDF documents as input.</summary>
    Pdf = 4,

    /// <summary>Audio as input.</summary>
    Audio = 8,

    /// <summary>Video as input.</summary>
    Video = 16,

    /// <summary>Tool (function) calling.</summary>
    Tools = 32,

    /// <summary>Extended reasoning.</summary>
    Reasoning = 64
}

/// <summary>Per token prices of a model, per million tokens, in the card's <see cref="ModelCard.Currency"/>.</summary>
public sealed class ModelCardPricing
{
    /// <summary>The price of a million input tokens.</summary>
    public double Input { get; init; }

    /// <summary>The price of a million output tokens.</summary>
    public double Output { get; init; }

    /// <summary>The price of a million cached input tokens, or null when the model has none.</summary>
    public double? CachedInput { get; init; }
}

/// <summary>
/// One model a <see cref="ModelCard"/> describes or compares against. Every value but <see cref="Id"/> and
/// <see cref="Name"/> is optional; a missing one reads as "Not listed".
/// </summary>
public sealed class ModelCardModel
{
    /// <summary>The API model id, shown in the footer and copied by Copy model id.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The model's name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Who makes the model.</summary>
    public string? Provider { get; init; }

    /// <summary>
    /// The model's mark as a 24 × 24 stroked glyph (Lucide style). Null shows the first letter of the name in a tinted
    /// rounded square. Marks are decorative: the name is always written next to them.
    /// </summary>
    public Geometry? Mark { get; init; }

    /// <summary>The mark's color (and the tint of its square); null uses the accent.</summary>
    public Color? MarkColor { get; init; }

    /// <summary>One sentence of about 90 characters; the card reserves two lines (three on narrow cards) and clamps the rest.</summary>
    public string? Summary { get; init; }

    /// <summary>What the model takes in or can do.</summary>
    public ModelCardModalities Modalities { get; init; }

    /// <summary>The context window in tokens (1_000_000 reads as 1M).</summary>
    public long? ContextWindow { get; init; }

    /// <summary>The most tokens one response can hold.</summary>
    public long? MaxOutput { get; init; }

    /// <summary>The training cutoff as an ISO month such as "2026-03" (shown as Mar 2026); any other text is shown as is.</summary>
    public string? KnowledgeCutoff { get; init; }

    /// <summary>Prices per million tokens.</summary>
    public ModelCardPricing? Pricing { get; init; }

    /// <summary>Scores keyed by <see cref="ModelCardBenchmark.Id"/>.</summary>
    public IReadOnlyDictionary<string, double>? Scores { get; init; }

    /// <summary>The score for a benchmark, or null.</summary>
    public double? ScoreOf(string benchmarkId) =>
        Scores is not null && Scores.TryGetValue(benchmarkId, out var score) ? score : null;
}

/// <summary>A benchmark the card can show bars for.</summary>
public sealed class ModelCardBenchmark
{
    /// <summary>The key into <see cref="ModelCardModel.Scores"/>.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The full name, shown in the caption and the compare table.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>A short name for the switcher; null uses <see cref="Name"/>.</summary>
    public string? ShortName { get; init; }

    /// <summary>What it measures, after the name in the caption ("real GitHub issues resolved").</summary>
    public string? Description { get; init; }

    /// <summary>The score that fills a bar.</summary>
    public double Max { get; init; } = 100;

    /// <summary>What follows each score.</summary>
    public string Unit { get; init; } = "%";
}

/// <summary>Arguments of <see cref="ModelCard.ViewChanged"/>.</summary>
public sealed class ModelCardViewChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ModelCardViewChangedEventArgs(RoutedEvent routedEvent, ModelCardView view) : base(routedEvent)
    {
        View = view;
    }

    /// <summary>The view now shown.</summary>
    public ModelCardView View { get; }
}

/// <summary>Arguments of the card's events that carry an id: the rival picked, the benchmark chosen or the id copied.</summary>
public sealed class ModelCardIdEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ModelCardIdEventArgs(RoutedEvent routedEvent, string id) : base(routedEvent)
    {
        Id = id;
    }

    /// <summary>The id.</summary>
    public string Id { get; }
}

/// <summary>Arguments of <see cref="ModelCard.MonthlyTokensChanged"/>.</summary>
public sealed class ModelCardMonthlyTokensEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ModelCardMonthlyTokensEventArgs(RoutedEvent routedEvent, long tokens) : base(routedEvent)
    {
        Tokens = tokens;
    }

    /// <summary>The new monthly volume in tokens.</summary>
    public long Tokens { get; }
}

/// <summary>Arguments of <see cref="ModelCard.SelectRequested"/>.</summary>
public sealed class ModelCardModelEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ModelCardModelEventArgs(RoutedEvent routedEvent, ModelCardModel model) : base(routedEvent)
    {
        Model = model;
    }

    /// <summary>The model the card shows.</summary>
    public ModelCardModel Model { get; }
}
