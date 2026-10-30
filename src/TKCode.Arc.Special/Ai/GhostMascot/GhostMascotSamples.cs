namespace TKCode.Arc;

/// <summary>
/// The self-play sequence a <see cref="GhostMascot"/> runs while its status is unset (the docs showcase): idle 3s,
/// listening 2.6s, thinking 3.2s, writing 4.4s, done 2.6s, error 2.4s, then again.
/// </summary>
internal static class GhostMascotSamples
{
    private static readonly (GhostMascotStatus Status, double Seconds)[] s_sequence =
    [
        (GhostMascotStatus.Idle, 3.0),
        (GhostMascotStatus.Listening, 2.6),
        (GhostMascotStatus.Thinking, 3.2),
        (GhostMascotStatus.Writing, 4.4),
        (GhostMascotStatus.Done, 2.6),
        (GhostMascotStatus.Error, 2.4)
    ];

    /// <summary>The number of steps in the sequence.</summary>
    public static int Count => s_sequence.Length;

    /// <summary>The status of step <paramref name="index"/> (wrapping).</summary>
    public static GhostMascotStatus StatusAt(int index) => s_sequence[Wrap(index)].Status;

    /// <summary>How long step <paramref name="index"/> lasts (s, wrapping).</summary>
    public static double SecondsAt(int index) => s_sequence[Wrap(index)].Seconds;

    private static int Wrap(int index) => ((index % s_sequence.Length) + s_sequence.Length) % s_sequence.Length;
}
