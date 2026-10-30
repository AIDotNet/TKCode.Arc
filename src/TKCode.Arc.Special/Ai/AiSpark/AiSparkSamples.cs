namespace TKCode.Arc;

/// <summary>
/// The self-play sequence an <see cref="AiSpark"/> runs when its status is not set (for demos and marketing pages):
/// idle 2.4s, thinking 3.6s, writing 4.2s, done 2.4s, then again.
/// </summary>
internal static class AiSparkSamples
{
    private static readonly (AiSparkStatus Status, double Seconds)[] s_sequence =
    [
        (AiSparkStatus.Idle, 2.4),
        (AiSparkStatus.Thinking, 3.6),
        (AiSparkStatus.Generating, 4.2),
        (AiSparkStatus.Done, 2.4)
    ];

    /// <summary>The number of steps in the sequence.</summary>
    public static int Count => s_sequence.Length;

    /// <summary>The status of step <paramref name="index"/> (wrapping).</summary>
    public static AiSparkStatus StatusAt(int index) => s_sequence[Wrap(index)].Status;

    /// <summary>How long step <paramref name="index"/> lasts (s, wrapping).</summary>
    public static double SecondsAt(int index) => s_sequence[Wrap(index)].Seconds;

    private static int Wrap(int index) => ((index % s_sequence.Length) + s_sequence.Length) % s_sequence.Length;
}
