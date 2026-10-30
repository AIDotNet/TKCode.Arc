namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="CodeSuggestion"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; texts that carry a name or a count are format functions.
/// </summary>
public sealed class CodeSuggestionLabels
{
    /// <summary>The English defaults.</summary>
    public static CodeSuggestionLabels Default { get; } = new();

    /// <summary>The card's accessible name, from the instruction: "Code suggestion: Add retry with backoff".</summary>
    public Func<string, string> CardName { get; init; } = instruction => "Code suggestion: " + instruction;

    /// <summary>The code area's accessible name, from the file name.</summary>
    public Func<string, string> CodeName { get; init; } = file => file;

    /// <summary>The status while the model reads the file: "Reading fetch-invoice.ts".</summary>
    public Func<string, string> Reading { get; init; } = file => "Reading " + file;

    /// <summary>The status while the hunks stream in.</summary>
    public string Writing { get; init; } = "Writing changes";

    /// <summary>The status while hunks wait for a decision: "3 changes to review".</summary>
    public Func<int, string> ToReview { get; init; } = count => count == 1 ? "1 change to review" : count + " changes to review";

    /// <summary>The status once every hunk is decided and some were kept, from the accepted and total counts.</summary>
    public Func<int, int, string> Applied { get; init; } = (accepted, total) => accepted == total
        ? total == 1 ? "Applied 1 change" : "Applied all " + total + " changes"
        : "Applied " + accepted + " of " + total + " changes";

    /// <summary>The status once every hunk was rejected, from the total.</summary>
    public Func<int, string> RejectedAll { get; init; } = total => total == 1 ? "Rejected the change" : "Rejected all " + total + " changes";

    /// <summary>The status of a finished suggestion with no hunks.</summary>
    public string NoChanges { get; init; } = "No changes";

    /// <summary>The Accept button under a hunk.</summary>
    public string Accept { get; init; } = "Accept";

    /// <summary>The Reject button under a hunk.</summary>
    public string Reject { get; init; } = "Reject";

    /// <summary>The Accept button's accessible name, from the hunk's summary: "Accept Retry 429 and 5xx with backoff".</summary>
    public Func<string, string> AcceptHunk { get; init; } = summary => "Accept " + summary;

    /// <summary>The Reject button's accessible name, from the hunk's summary.</summary>
    public Func<string, string> RejectHunk { get; init; } = summary => "Reject " + summary;

    /// <summary>What a hunk without a summary is called, from its position: "change 2".</summary>
    public Func<int, string> Change { get; init; } = number => "change " + number;

    /// <summary>The footer's Accept all.</summary>
    public string AcceptAll { get; init; } = "Accept all";

    /// <summary>The footer's Reject all.</summary>
    public string RejectAll { get; init; } = "Reject all";

    /// <summary>The shortcut hint on Accept all on macOS.</summary>
    public string AcceptAllHintMac { get; init; } = "⌘↵";

    /// <summary>The shortcut hint on Accept all elsewhere.</summary>
    public string AcceptAllHint { get; init; } = "Ctrl ↵";

    /// <summary>The Stop button while the model reads or writes.</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The Regenerate button once the review is done.</summary>
    public string Regenerate { get; init; } = "Regenerate";

    /// <summary>The Retry button of a failed suggestion.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>The hidden word before a pending removed line.</summary>
    public string Removed { get; init; } = "Removed:";

    /// <summary>The hidden word before a pending added line.</summary>
    public string Added { get; init; } = "Added:";

    /// <summary>The name of the tab's counts, from the lines added and removed: "4 lines added, 1 removed".</summary>
    public Func<int, int, string> Counts { get; init; } = (added, removed) =>
        (added == 1 ? "1 line added, " : added + " lines added, ") + removed + " removed";

    /// <summary>Said after a hunk is accepted, from its summary: "Accepted: Add retry limits.".</summary>
    public Func<string, string> AcceptedAnnouncement { get; init; } = summary => "Accepted: " + summary + ".";

    /// <summary>Said after a hunk is rejected, from its summary.</summary>
    public Func<string, string> RejectedAnnouncement { get; init; } = summary => "Rejected: " + summary + ".";

    /// <summary>Added to the result when the built-in sample finishes.</summary>
    public string Simulated { get; init; } = "Simulated, nothing was written to disk.";
}
