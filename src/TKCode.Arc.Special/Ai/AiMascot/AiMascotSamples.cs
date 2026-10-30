namespace TKCode.Arc;

/// <summary>
/// The self-playing tour an <see cref="AiMascot"/> plays when <see cref="AiMascot.Status"/> is not set (the docs demo):
/// every state in the order a real reply goes through them, each long enough to show its motion, then around again.
/// </summary>
internal static class AiMascotSamples
{
    /// <summary>The states of the tour and how long each is shown (seconds; writing shows two writing cycles).</summary>
    public static readonly (AiMascotStatus Status, double Seconds)[] Tour =
    [
        (AiMascotStatus.Idle, 3.4),
        (AiMascotStatus.Listening, 3.0),
        (AiMascotStatus.Thinking, 3.6),
        (AiMascotStatus.Writing, 5.0),
        (AiMascotStatus.Done, 2.6),
        (AiMascotStatus.Error, 2.4)
    ];
}
