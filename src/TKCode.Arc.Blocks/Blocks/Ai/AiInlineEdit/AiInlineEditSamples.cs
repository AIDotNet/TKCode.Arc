namespace TKCode.Arc;

/// <summary>
/// Invented sample data for <see cref="AiInlineEdit"/>: a paragraph of a made-up product update and a simple stand-in
/// rewriter that works without a model.
/// </summary>
public static class AiInlineEditSamples
{
    /// <summary>The sample paragraph.</summary>
    public const string Text =
        "Starting next week, the Lanterns team will basically be moving all of the nightly exports over to the new " +
        "scheduler. In order to make this transition really smooth, we are going to utilize a short freeze on Thursday, " +
        "and we would really appreciate it if everyone could just avoid starting very large exports during that window. " +
        "If anything breaks, please reach out in the #exports channel.";

    /// <summary>The quick instructions offered under the field.</summary>
    public static IReadOnlyList<string> Suggestions() => ["Make it shorter", "Make it friendlier", "Fix grammar", "More formal"];

    /// <summary>How long the stand-in rewriter takes (0.9 seconds).</summary>
    public static readonly TimeSpan SimulatedDelay = TimeSpan.FromMilliseconds(900);

    private static readonly (string From, string To)[] Plain =
    [
        ("In order to ", "To "), ("in order to ", "to "), ("utilize", "use"), ("basically ", ""), ("really ", ""), ("just ", ""),
        ("very ", ""), ("all of the ", "all "), ("we are going to", "we will"), ("would really appreciate it if everyone could", "ask everyone to"),
        ("are going to", "will")
    ];

    private static readonly (string From, string To)[] Friendly =
    [
        ("please reach out", "just give us a shout"), ("we would really appreciate it if everyone could just avoid", "it would help a lot if you held off on"),
        ("utilize", "use"), ("In order to", "To"), ("short freeze", "quick pause")
    ];

    private static readonly (string From, string To)[] Formal =
    [
        ("basically ", ""), ("really ", ""), ("just ", ""), ("we would really appreciate it if everyone could", "we ask that all teams"),
        ("reach out", "write"), ("If anything breaks", "Should any issue arise"), ("Starting next week", "From next week")
    ];

    /// <summary>
    /// A stand-in rewrite of <paramref name="request"/> with plain word swaps: "shorter" and "grammar" drop filler words,
    /// "friendlier" and "formal" change the tone. Any other instruction is treated as "shorter".
    /// </summary>
    public static string Rewrite(AiInlineEditRequest request)
    {
        var instruction = request.Instruction.ToLowerInvariant();
        var rules = instruction.Contains("friend") ? Friendly : instruction.Contains("formal") ? Formal : Plain;
        var text = request.Selection;
        foreach (var (from, to) in rules)
        {
            text = text.Replace(from, to, StringComparison.Ordinal);
        }
        if (text.Length > 0 && char.IsLower(text[0]) && request.Start == 0)
        {
            text = char.ToUpperInvariant(text[0]) + text[1..];
        }
        return text;
    }

    /// <summary>The stand-in rewriter as a handler: waits <see cref="SimulatedDelay"/>, then returns <see cref="Rewrite"/>.</summary>
    public static async Task<string> RewriteAsync(AiInlineEditRequest request, CancellationToken cancellation)
    {
        await Task.Delay(SimulatedDelay, cancellation);
        return Rewrite(request);
    }
}
