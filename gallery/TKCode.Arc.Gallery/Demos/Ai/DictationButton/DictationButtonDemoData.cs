namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// What the DictationButton demos dictate and show: three short invented prompts, each with a misheard word the simulated
/// recognizer corrects, and the composer's model label, a real model from <see cref="AiModelCatalog"/> (the model the
/// message goes to, not a speech model). Demo content only: nothing is recorded or sent.
/// </summary>
internal static class DictationButtonDemoData
{
    /// <summary>The field's placeholder.</summary>
    public const string Placeholder = "Message Claude";

    /// <summary>The model label: the chat model the composer sends to.</summary>
    public static DictationModel Model { get; } = new(AiModelCatalog.ClaudeSonnet55.Name);

    /// <summary>The scripts the simulated voice speaks, one per dictation, in turn.</summary>
    public static IReadOnlyList<IReadOnlyList<DictationPhrase>> Scripts { get; } =
    [
        [
            new("Draft a short reply to the design team", "Draft a short reply to the desk team"),
            new("thanking them for the new onboarding screens,"),
            new("and ask if the empty states can ship on Friday.", "and ask if the empty states can ship on fry day"),
        ],
        [
            new("Summarize the last three support tickets about billing", "Summarize the last free support tickets about billing"),
            new("and list anything that still needs a refund."),
        ],
        [
            new("Plan a two day offsite for eight people in March,", "Plan a two day offsite for eight people in much"),
            new("with one evening free and a budget under four thousand."),
        ],
    ];
}
