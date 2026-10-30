namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="ToneTuner"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; counts and announcements are format functions.
/// </summary>
public sealed class ToneTunerLabels
{
    /// <summary>The English defaults.</summary>
    public static ToneTunerLabels Default { get; } = new();

    /// <summary>The name of the voice chip group.</summary>
    public string Voice { get; init; } = "Voice";

    /// <summary>The formality slider's label.</summary>
    public string Formality { get; init; } = "Formality";

    /// <summary>The detail slider's label.</summary>
    public string Detail { get; init; } = "Detail";

    /// <summary>The three formality levels, spoken and shown instead of a number.</summary>
    public IReadOnlyList<string> FormalityLevels { get; init; } = ["Casual", "Balanced", "Formal"];

    /// <summary>The three detail levels, spoken and shown instead of a number.</summary>
    public IReadOnlyList<string> DetailLevels { get; init; } = ["Brief", "Standard", "Detailed"];

    /// <summary>The name of each voice (the default chips' text and the status settings).</summary>
    public Func<ToneVoice, string> VoiceName { get; init; } = voice => voice switch
    {
        ToneVoice.Friendly => "Friendly",
        ToneVoice.Confident => "Confident",
        ToneVoice.Empathetic => "Empathetic",
        _ => "Neutral",
    };

    /// <summary>The Changes switch.</summary>
    public string Changes { get; init; } = "Changes";

    /// <summary>The Copy button.</summary>
    public string Copy { get; init; } = "Copy";

    /// <summary>The Copy button after it copied.</summary>
    public string Copied { get; init; } = "Copied";

    /// <summary>The status while the original draft shows.</summary>
    public string OriginalDraft { get; init; } = "Original draft";

    /// <summary>The status while a rewrite runs, from the model's name.</summary>
    public Func<string, string> Rewriting { get; init; } = model => string.IsNullOrWhiteSpace(model) ? "Rewriting" : "Rewriting with " + model;

    /// <summary>The status once a rewrite landed.</summary>
    public string Rewritten { get; init; } = "Rewritten";

    /// <summary>The status when a rewrite failed.</summary>
    public string Failed { get; init; } = "Couldn't rewrite";

    /// <summary>The Retry button next to a failed status.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The header's count of words that differ from the original.</summary>
    public Func<int, string> ChangedWords { get; init; } = count => count == 1 ? "1 word changed" : count + " words changed";

    /// <summary>The settings after the status ("Formal · Detailed · Friendly"); the voice is left out while neutral.</summary>
    public Func<ToneSettings, ToneTunerLabels, string> Settings { get; init; } = (value, labels) =>
    {
        var text = labels.FormalityLevels[value.F] + " · " + labels.DetailLevels[value.D];
        return value.Voice == ToneVoice.Neutral ? text : text + " · " + labels.VoiceName(value.Voice);
    };

    /// <summary>What the live region says when a rewrite lands: the settings, and whether it was simulated.</summary>
    public Func<string, bool, string> Landed { get; init; } = (settings, simulated) =>
        simulated ? "Rewritten: " + settings + ". Sample rewrite, nothing was sent." : "Rewritten: " + settings + ".";

    /// <summary>What the live region says when the original draft comes back.</summary>
    public string BackToOriginal { get; init; } = "Showing the original draft.";

    /// <summary>What the live region says when a rewrite fails, from the error's message (may be empty).</summary>
    public Func<string, string> FailedAnnouncement { get; init; } = reason =>
        string.IsNullOrWhiteSpace(reason) ? "Couldn't rewrite. Press Retry to try again." : "Couldn't rewrite: " + reason + " Press Retry to try again.";
}
