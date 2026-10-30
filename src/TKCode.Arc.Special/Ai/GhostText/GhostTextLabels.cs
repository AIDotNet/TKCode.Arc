namespace TKCode.Arc;

/// <summary>
/// Every word a <see cref="GhostText"/> shows or says, in English by default: the chip in each state, the key hints, the
/// keys description read with the field and the polite announcements. Replace the whole object to localize or reword;
/// sentences that carry text are functions so word order can change.
/// </summary>
public sealed class GhostTextLabels
{
    /// <summary>The English defaults.</summary>
    public static GhostTextLabels Default { get; } = new();

    /// <summary>The model name the idle chip shows when <see cref="GhostText.Model"/> is not set.</summary>
    public string DefaultModelName { get; init; } = "Autocomplete";

    /// <summary>The chip while a request waits for its first words (it shimmers).</summary>
    public string Thinking { get; init; } = "Thinking";

    /// <summary>The words after the Tab key cap when a suggestion is ready ("Tab to accept").</summary>
    public string ToAccept { get; init; } = "to accept";

    /// <summary>The ready chip on touch screens, where there is no Tab key.</summary>
    public string TapToAccept { get; init; } = "Tap to accept";

    /// <summary>The chip right after a suggestion was accepted.</summary>
    public string Accepted { get; init; } = "Accepted";

    /// <summary>The chip when the suggest function failed.</summary>
    public string NoSuggestion { get; init; } = "No suggestion";

    /// <summary>The action in the failed chip.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The Tab key cap.</summary>
    public string TabKey { get; init; } = "Tab";

    /// <summary>The Escape key cap.</summary>
    public string EscapeKey { get; init; } = "Esc";

    /// <summary>The modifier of the word key cap on macOS.</summary>
    public string MacWordModifier { get; init; } = "⌘";

    /// <summary>The modifier of the word key cap on other systems.</summary>
    public string WordModifier { get; init; } = "Ctrl";

    /// <summary>The words after the word key cap ("⌘ → word").</summary>
    public string WordHint { get; init; } = "word";

    /// <summary>The words after the Escape key cap ("Esc dismiss").</summary>
    public string DismissHint { get; init; } = "dismiss";

    /// <summary>The chip's accessible name while a suggestion is ready.</summary>
    public string AcceptSuggestion { get; init; } = "Accept suggestion";

    /// <summary>The chip's accessible name when the request failed.</summary>
    public string RetrySuggestion { get; init; } = "Retry suggestion";

    /// <summary>The chip's accessible name otherwise; the argument is the model's name.</summary>
    public Func<string, string> ModelChip { get; init; } = static model => $"Suggestions by {model}";

    /// <summary>The field's description of the keys, read by screen readers.</summary>
    public string KeysDescription { get; init; } =
        "Suggestions appear after the text as you type. Press Tab to accept, Command or Control plus Right Arrow to accept one word, Escape to dismiss.";

    /// <summary>The announcement of a finished suggestion; the argument is its text.</summary>
    public Func<string, string> SuggestionAnnouncement { get; init; } =
        static suggestion => $"Suggestion: {suggestion}. Press Tab to accept.";

    /// <summary>The announcement after Tab or a tap on the chip.</summary>
    public string AcceptedAnnouncement { get; init; } = "Suggestion accepted";

    /// <summary>The announcement after Escape.</summary>
    public string DismissedAnnouncement { get; init; } = "Suggestion dismissed";

    /// <summary>The announcement when the suggest function failed.</summary>
    public string ErrorAnnouncement { get; init; } = "Couldn't get a suggestion";
}
