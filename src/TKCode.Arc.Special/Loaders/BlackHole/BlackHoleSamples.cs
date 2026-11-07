namespace TKCode.Arc;

/// <summary>An invented mailbox conversation for the <see cref="BlackHole"/> demos.</summary>
/// <param name="Id">Stable id of the conversation.</param>
/// <param name="Sender">Who wrote it (two words).</param>
/// <param name="Subject">Its subject line.</param>
/// <param name="Time">When it arrived, as shown ("10:05", "Yesterday").</param>
/// <param name="Hues">Two or three hues (degrees) the demo blends into the sender's avatar.</param>
public sealed record BlackHoleSampleMessage(string Id, string Sender, string Subject, string Time, IReadOnlyList<double> Hues);

/// <summary>
/// Invented sample data for the <see cref="BlackHole"/> demos: a five-conversation mailbox (fictional senders and
/// subjects, newest first), the two conversations the preview starts with selected, and the demo's own sentences.
/// </summary>
public static class BlackHoleSamples
{
    /// <summary>Title of the demo card.</summary>
    public const string Title = "Mailbox";

    /// <summary>The five conversations, newest first.</summary>
    public static IReadOnlyList<BlackHoleSampleMessage> Messages { get; } =
    [
        new("m1", "Lena Hart", "Draft for the launch notes", "10:05", [262, 28, 150]),
        new("m2", "Owen Price", "Monthly storage summary", "9:47", [160, 340, 10]),
        new("m3", "Priya Nair", "Sync shifted to Friday", "9:02", [130, 310]),
        new("m4", "Tomas Berg", "Receipt 4471 is available", "Yesterday", [285, 70, 300]),
        new("m5", "Mei Tanaka", "Roadmap review outline", "Yesterday", [48, 215, 230]),
    ];

    /// <summary>Ids of the conversations the preview starts with selected (the second and the fourth).</summary>
    public static IReadOnlyList<string> InitiallySelected { get; } = ["m2", "m4"];

    /// <summary>The delete button's label for <paramref name="count"/> selected conversations.</summary>
    public static string DeleteLabel(int count) => $"Delete {count}";

    /// <summary>The undo pill's sentence after <paramref name="count"/> conversations were removed.</summary>
    public static string RemovedLabel(int count) => count == 1 ? "1 message removed" : $"{count} messages removed";

    /// <summary>The undo button's label.</summary>
    public const string Undo = "Undo";
}
