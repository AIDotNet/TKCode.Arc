namespace TKCode.Arc;

/// <summary>
/// The built-in story of a <see cref="PromptCache"/>: a support assistant for a freight company with four prefix
/// segments and four questions, an invented model and sample prices. All of it is illustrative.
/// </summary>
public static class PromptCacheDefaults
{
    /// <summary>The default model name shown in the header.</summary>
    public const string ModelName = "Nimbus 4 Pro";

    /// <summary>The default time to live in seconds (five minutes).</summary>
    public const double Ttl = 300;

    /// <summary>The id of the segment the change toggle edits by default.</summary>
    public const string ChangeSegmentId = "rules";

    /// <summary>New copies of the default prefix: tool schemas, instructions, a knowledge base and the chat so far.</summary>
    public static IReadOnlyList<PromptCacheSegment> CreateSegments() =>
    [
        new("tools", "Tool schemas", 4_100),
        new("rules", "Instructions", 2_200),
        new("kb", "Knowledge base", 33_800),
        new("history", "Chat history", 5_400)
    ];

    /// <summary>New copies of the default requests: four questions of 160 to 420 tokens, 33 to 54 simulated seconds apart.</summary>
    public static IReadOnlyList<PromptCacheRequest> CreateRequests() =>
    [
        new("List the parcels held at customs today", 190),
        new("Draft a delay notice for the Pinewood order", 260, 38),
        new("Compare the two freight quotes in the sheet", 420, 33),
        new("Which depots are short on pallet wrap?", 160, 54)
    ];

    /// <summary>New default prices: $3.00 input, $3.75 cache write and $0.30 cache read per million tokens.</summary>
    public static PromptCachePricing CreatePricing() => new();
}
