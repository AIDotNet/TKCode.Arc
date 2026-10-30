using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where the response a <see cref="GenerationStats"/> describes is.</summary>
public enum GenerationStatus
{
    /// <summary>Nothing has been sent yet: every value reads a dash.</summary>
    Idle,

    /// <summary>The request is out and no token has arrived: time to first token counts live.</summary>
    Waiting,

    /// <summary>Tokens are arriving: output, speed and cost count up and the sparkline draws.</summary>
    Streaming,

    /// <summary>The response finished: every value settles on its exact number ("Done in 4.18s").</summary>
    Done,

    /// <summary>The person stopped the response ("Stopped at 1.90s").</summary>
    Stopped,

    /// <summary>The request failed: "Failed", with the reason and Retry in a row under the stats.</summary>
    Error
}

/// <summary>How a <see cref="GenerationStats"/> is drawn.</summary>
public enum GenerationStatsVariant
{
    /// <summary>A bordered strip with four cells, for dev panels and run views.</summary>
    Card,

    /// <summary>One quiet wrapping line for the space under a chat message; the breakdown opens as a panel below it.</summary>
    Inline
}

/// <summary>The model that answered: its name as people know it, your key for its provider, its API id and, optionally, its mark.</summary>
public sealed class GenerationModel
{
    /// <summary>Creates an empty model; set <see cref="Name"/> before showing it.</summary>
    public GenerationModel()
    {
    }

    /// <summary>Creates a model.</summary>
    /// <param name="name">The name as people know it.</param>
    /// <param name="provider">Your key for the provider, written as given into the copied JSON; null leaves it out.</param>
    /// <param name="id">The API id that goes into the copied JSON.</param>
    public GenerationModel(string name, string? provider = null, string? id = null)
    {
        Name = name;
        Provider = provider;
        Id = id;
    }

    /// <summary>The name as people know it; the strip is named after it and its monogram is the name's first letter.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Your key for the provider, written as given into the copied JSON; null or empty leaves it out.</summary>
    public string? Provider { get; set; }

    /// <summary>The API id, written into the copied JSON.</summary>
    public string? Id { get; set; }

    /// <summary>A 24 × 24 stroked glyph drawn as the model's mark (pass your provider's mark here); null draws a monogram.</summary>
    public Geometry? Mark { get; set; }

    /// <summary>The mark's color; null uses the text color.</summary>
    public IBrush? MarkBrush { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// The token usage and timing of one response, all optional. Make a new one (or a copy with <c>with</c>) whenever a value
/// changes: update <see cref="OutputTokens"/> as chunks arrive, set <see cref="Ttft"/> on the first token and
/// <see cref="Duration"/> on the last. Speed and cost are derived unless you pass <see cref="TokensPerSecond"/> or
/// <see cref="CostUsd"/>.
/// </summary>
public sealed record GenerationUsage
{
    /// <summary>Input (prompt) tokens, cached ones included.</summary>
    public long? InputTokens { get; init; }

    /// <summary>The part of the input read from the prompt cache.</summary>
    public long? CachedTokens { get; init; }

    /// <summary>Output tokens so far, reasoning tokens not included.</summary>
    public long? OutputTokens { get; init; }

    /// <summary>Reasoning (thinking) tokens; they bill at the output rate.</summary>
    public long? ReasoningTokens { get; init; }

    /// <summary>Time to the first token, from when the request was sent.</summary>
    public TimeSpan? Ttft { get; init; }

    /// <summary>Total time, from when the request was sent to the last token.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>The speed your provider reports; null derives it from the output tokens and the generation time.</summary>
    public double? TokensPerSecond { get; init; }

    /// <summary>The cost your provider reports, in US dollars; null derives it from <see cref="GenerationStats.Pricing"/>.</summary>
    public double? CostUsd { get; init; }
}

/// <summary>Prices in US dollars per million tokens. Reasoning tokens bill at the output rate.</summary>
public sealed record GenerationPricing
{
    /// <summary>Creates prices.</summary>
    public GenerationPricing()
    {
    }

    /// <summary>Creates prices.</summary>
    /// <param name="input">Input, per million tokens.</param>
    /// <param name="output">Output, per million tokens.</param>
    /// <param name="cachedInput">Cached input, per million tokens; null bills cached tokens at the input rate.</param>
    public GenerationPricing(double input, double output, double? cachedInput = null)
    {
        Input = input;
        Output = output;
        CachedInput = cachedInput;
    }

    /// <summary>Input, per million tokens.</summary>
    public double Input { get; init; }

    /// <summary>Cached input, per million tokens; null bills cached tokens at the input rate.</summary>
    public double? CachedInput { get; init; }

    /// <summary>Output (and reasoning), per million tokens.</summary>
    public double Output { get; init; }
}

/// <summary>Data of <see cref="GenerationStats.DetailsToggled"/>.</summary>
public sealed class GenerationStatsDetailsToggledEventArgs : RoutedEventArgs
{
    /// <summary>Creates the event data.</summary>
    public GenerationStatsDetailsToggledEventArgs(RoutedEvent routedEvent, bool isExpanded) : base(routedEvent)
    {
        IsExpanded = isExpanded;
    }

    /// <summary>Whether the breakdown is now open.</summary>
    public bool IsExpanded { get; }
}
