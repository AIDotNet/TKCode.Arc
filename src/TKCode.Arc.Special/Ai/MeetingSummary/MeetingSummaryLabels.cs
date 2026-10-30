using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="MeetingSummary"/> shows or says, in English by default. Replace the whole object to localize or
/// reword; counts, dates and names are format functions.
/// </summary>
public sealed class MeetingSummaryLabels
{
    private static readonly CultureInfo s_english = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The English defaults.</summary>
    public static MeetingSummaryLabels Default { get; } = new();

    /// <summary>The Summary section's heading.</summary>
    public string Summary { get; init; } = "Summary";

    /// <summary>The Decisions section's heading.</summary>
    public string Decisions { get; init; } = "Decisions";

    /// <summary>The Action items section's heading.</summary>
    public string ActionItems { get; init; } = "Action items";

    /// <summary>The shimmering label of the summarizing pass.</summary>
    public string Summarizing { get; init; } = "Summarizing transcript";

    /// <summary>The error state's title.</summary>
    public string ErrorTitle { get; init; } = "Couldn't summarize this meeting";

    /// <summary>The error state's message when no <see cref="MeetingSummary.ErrorText"/> is given.</summary>
    public string ErrorFallback { get; init; } = "Something went wrong while reading the transcript.";

    /// <summary>The error state's button.</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>The no transcript state's title.</summary>
    public string EmptyTitle { get; init; } = "No transcript to summarize";

    /// <summary>The no transcript state's text.</summary>
    public string EmptyText { get; init; } = "Nobody spoke long enough to take notes. Recordings shorter than a minute are skipped.";

    /// <summary>The name of the regenerate button.</summary>
    public string Regenerate { get; init; } = "Regenerate summary";

    /// <summary>The name of the copy button.</summary>
    public string Copy { get; init; } = "Copy notes";

    /// <summary>The footer credit with the model's name ("Summarized by" and the name).</summary>
    public Func<string, string> SummarizedBy { get; init; } = model => "Summarized by " + model;

    /// <summary>The transcript's size in the footer ("4,812 words").</summary>
    public Func<int, string> Words { get; init; } = words => words.ToString("N0", s_english) + " words";

    /// <summary>The call's length in the header ("34 min", "1 h 05 min").</summary>
    public Func<int, string> Duration { get; init; } = minutes => minutes < 60
        ? minutes.ToString(s_english) + " min"
        : (minutes / 60).ToString(s_english) + " h " + (minutes % 60).ToString("00", s_english) + " min";

    /// <summary>The start time in the header ("Tue, Oct 28 · 2:00 PM").</summary>
    public Func<DateTime, string> StartsAt { get; init; } = at =>
        at.ToString("ddd, MMM d", s_english) + " · " + at.ToString("h:mm tt", s_english);

    /// <summary>A due date under an action item ("Due Oct 31").</summary>
    public Func<DateOnly, string> Due { get; init; } = due => "Due " + due.ToString("MMM d", s_english);

    /// <summary>The name of the attendee stack.</summary>
    public string Attendees { get; init; } = "Attendees";

    /// <summary>A timestamp button's name ("Show quote at 12:04").</summary>
    public Func<string, string> ShowQuote { get; init; } = time => "Show quote at " + time;

    /// <summary>The quote card's speaker line when the speaker is unknown.</summary>
    public string UnknownSpeaker { get; init; } = "Speaker";

    /// <summary>A checkbox's name, from the action item's text.</summary>
    public Func<string, string> Check { get; init; } = text => "Done: " + text;

    /// <summary>A send button's name ("Send “Draft the pricing page” to Issues").</summary>
    public Func<string, string, string> SendTo { get; init; } = (text, target) => "Send “" + text + "” to " + target;

    /// <summary>A send button's tooltip ("Send to Issues").</summary>
    public Func<string, string> SendToShort { get; init; } = target => "Send to " + target;

    /// <summary>The sent chip when the send returned no reference.</summary>
    public string Sent { get; init; } = "Sent";

    /// <summary>The label of a failed send.</summary>
    public string Retry { get; init; } = "Retry";

    /// <summary>Announced when the pass starts.</summary>
    public string AnnounceSummarizing { get; init; } = "Summarizing the meeting.";

    /// <summary>Announced when the notes are ready (points, decisions, action items).</summary>
    public Func<int, int, int, string> AnnounceReady { get; init; } = (points, decisions, items) =>
        $"Summary ready: {points} {(points == 1 ? "point" : "points")}, {decisions} {(decisions == 1 ? "decision" : "decisions")}, {items} action {(items == 1 ? "item" : "items")}.";

    /// <summary>Announced when the notes are empty.</summary>
    public string AnnounceEmpty { get; init; } = "No transcript to summarize.";

    /// <summary>Announced when summarizing failed.</summary>
    public string AnnounceError { get; init; } = "Couldn't summarize this meeting.";

    /// <summary>Announced when an item is checked or unchecked (text, done).</summary>
    public Func<string, bool, string> AnnounceChecked { get; init; } = (text, done) =>
        (done ? "Done: " : "Not done: ") + text;

    /// <summary>Announced when an item was sent (target, reference or null).</summary>
    public Func<string, string?, string> AnnounceSent { get; init; } = (target, reference) =>
        reference is null ? "Sent to " + target + "." : "Sent to " + target + " as " + reference + ".";

    /// <summary>Added to the sent announcement when the card only simulated the send.</summary>
    public string AnnounceSimulated { get; init; } = "Simulated, nothing was sent.";

    /// <summary>Announced when a send failed.</summary>
    public Func<string, string> AnnounceFailed { get; init; } = target => "Couldn't send to " + target + ".";
}
