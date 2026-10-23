namespace TKCode.Arc;

/// <summary>
/// The built-in sample data of <see cref="SpeculativeDecoding"/>, invented for this library: the default answer (a
/// maintenance notice of 22 tokens, with its ease values and the draft's wrong guesses) and a recorded run of a code
/// completion to replay. With <see cref="Seed"/> and a draft length of 4 the default answer takes 6 passes: 24 drafted,
/// 16 accepted, 2.6× faster, 3.7 tokens per pass; the speedup peaks at a draft length of 4.
/// </summary>
public static class SpeculativeDecodingSamples
{
    /// <summary>The default seed: with <see cref="Tokens"/> it gives the run described above.</summary>
    public const int Seed = 25;

    /// <summary>
    /// The default answer: "Tomorrow at 7, the east wing switches to backup power for 25 minutes, while crews replace two
    /// old panels." Numbers and rare words are hard to guess; short function words and punctuation are easy.
    /// </summary>
    public static IReadOnlyList<DecodeToken> Tokens { get; } =
    [
        new("Tomorrow", 0.97),
        new(" at", 0.97),
        new(" 7", 0.1, " 9"),
        new(",", 0.97),
        new(" the", 0.97),
        new(" east", 0.15, " north"),
        new(" wing", 0.85, " side"),
        new(" switches", 0.8, " moves"),
        new(" to", 0.97),
        new(" backup", 0.8, " main"),
        new(" power", 0.9, " lights"),
        new(" for", 0.95),
        new(" 25", 0.6, " 30"),
        new(" minutes", 0.95),
        new(",", 0.97),
        new(" while", 0.7, " and"),
        new(" crews", 0.15, " we"),
        new(" replace", 0.85, " check"),
        new(" two", 0.6, " the"),
        new(" old", 0.8, " main"),
        new(" panels", 0.9, " fuses"),
        new(".", 0.99)
    ];

    /// <summary>
    /// A recorded run of a code completion to replay with <see cref="SpeculativeDecoding.Rounds"/>: six rounds that write
    /// <c>const total = items.reduce((sum, item) =&gt; sum + item.price * item.qty, 0);</c> in 24 tokens (23 drafted,
    /// 18 accepted).
    /// </summary>
    public static IReadOnlyList<DecodeRound> CodeCompletion => new DecodeRound[]
    {
        new(["const", " total", " =", " items"], 4, "."),
        new(["reduce", "((", "acc", ","], 2, "sum"),
        new([",", " row", ")", " =>"], 1, " item"),
        new([")", " =>", " sum", " +"], 4, " item"),
        new([".", "price", " *", " item"], 4, "."),
        new(["qty", ",", " 0"], 3, ");")
    };
}
