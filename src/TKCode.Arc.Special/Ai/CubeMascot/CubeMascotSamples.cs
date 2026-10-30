namespace TKCode.Arc;

/// <summary>
/// The self-play sequence a <see cref="CubeMascot"/> runs when its status is not set (for showcases): idle 2.6s,
/// thinking 3.9s (three quarter turns), writing 4.32s (twelve lines), done 2.6s, error 1.8s, then again.
/// </summary>
internal static class CubeMascotSamples
{
    private static readonly (CubeMascotStatus Status, double Seconds)[] s_sequence =
    [
        (CubeMascotStatus.Idle, 2.6),
        (CubeMascotStatus.Thinking, 3.9),
        (CubeMascotStatus.Writing, 4.32),
        (CubeMascotStatus.Done, 2.6),
        (CubeMascotStatus.Error, 1.8)
    ];

    /// <summary>The number of steps in the sequence.</summary>
    public static int Count => s_sequence.Length;

    /// <summary>The status of step <paramref name="index"/> (wrapping).</summary>
    public static CubeMascotStatus StatusAt(int index) => s_sequence[Wrap(index)].Status;

    /// <summary>How long step <paramref name="index"/> lasts (s, wrapping).</summary>
    public static double SecondsAt(int index) => s_sequence[Wrap(index)].Seconds;

    private static int Wrap(int index) => ((index % s_sequence.Length) + s_sequence.Length) % s_sequence.Length;
}
