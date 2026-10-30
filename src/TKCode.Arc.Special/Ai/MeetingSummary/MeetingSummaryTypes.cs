using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where the notes of a <see cref="MeetingSummary"/> are.</summary>
public enum MeetingSummaryStatus
{
    /// <summary>The transcript is being summarized: a shimmering label in place of the notes.</summary>
    Summarizing,

    /// <summary>The notes are ready (with empty lists the card shows its no transcript state).</summary>
    Ready,

    /// <summary>Summarizing failed: an error title, the message in <see cref="MeetingSummary.ErrorText"/> and Try again.</summary>
    Error
}

/// <summary>Where a send of an action item is.</summary>
internal enum MeetingSummarySendState
{
    /// <summary>Not sent: the target's mark.</summary>
    Idle,

    /// <summary>Being created: a spinner.</summary>
    Sending,

    /// <summary>Created: a chip with the target's mark, the reference and a check.</summary>
    Sent,

    /// <summary>The send failed: an alert icon and Retry.</summary>
    Failed
}

/// <summary>One person on the call.</summary>
public sealed class MeetingAttendee
{
    /// <summary>Creates an empty attendee.</summary>
    public MeetingAttendee()
    {
    }

    /// <summary>Creates an attendee with an id and a full name.</summary>
    public MeetingAttendee(string id, string name, string? role = null)
    {
        Id = id;
        Name = name;
        Role = role;
    }

    /// <summary>The id quotes and action items refer to.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The full name ("Maya Lindqvist"); the avatar shows its initials.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>An optional role or team shown in the quote card ("Design").</summary>
    public string? Role { get; set; }

    /// <summary>An optional portrait; without one the avatar shows initials.</summary>
    public ImageSource? Portrait { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>The transcript passage a point came from.</summary>
public sealed class MeetingSummaryQuote
{
    /// <summary>Creates an empty quote.</summary>
    public MeetingSummaryQuote()
    {
    }

    /// <summary>Creates a quote: who said it, when (from the start of the recording) and what.</summary>
    public MeetingSummaryQuote(string speakerId, TimeSpan at, string text)
    {
        SpeakerId = speakerId;
        At = at;
        Text = text;
    }

    /// <summary>The <see cref="MeetingAttendee.Id"/> of the speaker.</summary>
    public string SpeakerId { get; set; } = string.Empty;

    /// <summary>When it was said, from the start of the recording.</summary>
    public TimeSpan At { get; set; }

    /// <summary>The words as transcribed.</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>One line of the Summary or Decisions list.</summary>
public class MeetingSummaryPoint
{
    /// <summary>Creates an empty point.</summary>
    public MeetingSummaryPoint()
    {
    }

    /// <summary>Creates a point with an id, its text and optionally its quote.</summary>
    public MeetingSummaryPoint(string id, string text, MeetingSummaryQuote? quote = null)
    {
        Id = id;
        Text = text;
        Quote = quote;
    }

    /// <summary>A stable id (the card keeps checked and sent state by it).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The line's text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The transcript passage it came from; null shows no timestamp.</summary>
    public MeetingSummaryQuote? Quote { get; set; }

    /// <inheritdoc />
    public override string ToString() => Text;
}

/// <summary>One task from the call: an owner, a due date and whether it is done.</summary>
public sealed class MeetingActionItem : MeetingSummaryPoint
{
    /// <summary>Creates an empty action item.</summary>
    public MeetingActionItem()
    {
    }

    /// <summary>Creates an action item.</summary>
    public MeetingActionItem(string id, string text, string? ownerId = null, DateOnly? due = null, MeetingSummaryQuote? quote = null)
        : base(id, text, quote)
    {
        OwnerId = ownerId;
        Due = due;
    }

    /// <summary>The <see cref="MeetingAttendee.Id"/> of the owner, or null.</summary>
    public string? OwnerId { get; set; }

    /// <summary>The due date, or null.</summary>
    public DateOnly? Due { get; set; }

    /// <summary>Whether it starts checked off. The card keeps the checked state itself after that.</summary>
    public bool IsDone { get; set; }
}

/// <summary>
/// The notes of one call: what it was, who was on it, and the Summary, Decisions and Action items lists. Map your
/// model's output to it; keep <see cref="MeetingSummaryQuote.At"/> as the time from the start of the recording.
/// </summary>
public sealed class MeetingSummaryData
{
    /// <summary>The meeting's title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The meeting platform's name, read before the date ("Video call"). Empty hides it.</summary>
    public string Platform { get; set; } = "Video call";

    /// <summary>The platform's mark: a 24 × 24 stroked glyph; null draws the video glyph. Pass your own mark here.</summary>
    public Geometry? PlatformMark { get; set; }

    /// <summary>When it started, as local wall time (printed as given; convert time zones before).</summary>
    public DateTime? StartsAt { get; set; }

    /// <summary>The length of the call in minutes.</summary>
    public int DurationMinutes { get; set; }

    /// <summary>How many words the transcript had (shown in the footer; 0 hides it).</summary>
    public int TranscriptWords { get; set; }

    /// <summary>The people on the call.</summary>
    public List<MeetingAttendee> Attendees { get; set; } = [];

    /// <summary>The Summary list.</summary>
    public List<MeetingSummaryPoint> Summary { get; set; } = [];

    /// <summary>The Decisions list.</summary>
    public List<MeetingSummaryPoint> Decisions { get; set; } = [];

    /// <summary>The Action items list.</summary>
    public List<MeetingActionItem> ActionItems { get; set; } = [];

    /// <summary>True when there are no notes at all (the no transcript state).</summary>
    public bool IsEmpty => Summary.Count == 0 && Decisions.Count == 0 && ActionItems.Count == 0;

    /// <summary>The attendee with <paramref name="id"/>, or null.</summary>
    public MeetingAttendee? Attendee(string? id)
    {
        if (id is null)
        {
            return null;
        }
        foreach (var attendee in Attendees)
        {
            if (attendee.Id == id)
            {
                return attendee;
            }
        }
        return null;
    }
}

/// <summary>
/// Somewhere an action item can be sent: an issue tracker, a notes app. A name and a 24 × 24 stroked mark; the defaults
/// are <see cref="Issues"/> and <see cref="Docs"/>. Pass your own mark for a real product.
/// </summary>
public sealed class MeetingSummarySendTarget
{
    /// <summary>Creates a target.</summary>
    public MeetingSummarySendTarget(string id, string name, Geometry? mark = null)
    {
        Id = id;
        Name = name;
        Mark = mark;
    }

    /// <summary>A generic issue tracker (the circle-dot glyph).</summary>
    public static MeetingSummarySendTarget Issues { get; } = new("issues", "Issues", ArcIcons.CircleDot);

    /// <summary>A generic notes or docs app (the file-text glyph).</summary>
    public static MeetingSummarySendTarget Docs { get; } = new("docs", "Docs", ArcIcons.FileText);

    /// <summary>A stable id ("issues").</summary>
    public string Id { get; }

    /// <summary>The name in labels ("Send to Issues").</summary>
    public string Name { get; }

    /// <summary>The mark; null draws the send glyph.</summary>
    public Geometry? Mark { get; }

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>What a send created: a reference shown in the sent chip ("ISS-142") and an optional link.</summary>
public sealed class MeetingSummarySendResult
{
    /// <summary>Creates a result.</summary>
    public MeetingSummarySendResult(string? reference = null, Uri? url = null)
    {
        Reference = reference;
        Url = url;
    }

    /// <summary>The issue key or page title; null shows "Sent".</summary>
    public string? Reference { get; }

    /// <summary>Where the created item lives; the sent chip opens it.</summary>
    public Uri? Url { get; }
}

/// <summary>Arguments of <see cref="MeetingSummary.SendRequested"/>.</summary>
public sealed class MeetingSummarySendEventArgs : RoutedEventArgs
{
    internal MeetingSummarySendEventArgs(RoutedEvent routedEvent, MeetingActionItem item, MeetingSummarySendTarget target,
        CancellationToken cancellationToken)
        : base(routedEvent)
    {
        Item = item;
        Target = target;
        CancellationToken = cancellationToken;
    }

    /// <summary>The action item to send.</summary>
    public MeetingActionItem Item { get; }

    /// <summary>Where to send it.</summary>
    public MeetingSummarySendTarget Target { get; }

    /// <summary>Cancelled when the card is removed or the notes are regenerated; pass it on to your request.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// The send: set it to your request's task before the handler returns. A result shows the sent chip, a fault shows
    /// Retry. Left null, the card simulates the send and announces that nothing was sent.
    /// </summary>
    public Task<MeetingSummarySendResult?>? Completion { get; set; }
}

/// <summary>Arguments of <see cref="MeetingSummary.ActionItemChanged"/>.</summary>
public sealed class MeetingActionItemChangedEventArgs : RoutedEventArgs
{
    internal MeetingActionItemChangedEventArgs(RoutedEvent routedEvent, MeetingActionItem item, bool isDone)
        : base(routedEvent)
    {
        Item = item;
        IsDone = isDone;
    }

    /// <summary>The action item that was checked or unchecked.</summary>
    public MeetingActionItem Item { get; }

    /// <summary>Whether it is now done.</summary>
    public bool IsDone { get; }
}

/// <summary>Arguments of <see cref="MeetingSummary.ActionItemSent"/>.</summary>
public sealed class MeetingSummarySentEventArgs : RoutedEventArgs
{
    internal MeetingSummarySentEventArgs(RoutedEvent routedEvent, MeetingActionItem item, MeetingSummarySendTarget target,
        MeetingSummarySendResult? result, bool isSimulated)
        : base(routedEvent)
    {
        Item = item;
        Target = target;
        Result = result;
        IsSimulated = isSimulated;
    }

    /// <summary>The action item that was sent.</summary>
    public MeetingActionItem Item { get; }

    /// <summary>Where it went.</summary>
    public MeetingSummarySendTarget Target { get; }

    /// <summary>What the send returned, or null.</summary>
    public MeetingSummarySendResult? Result { get; }

    /// <summary>True when no handler sent it and the card only simulated the send.</summary>
    public bool IsSimulated { get; }
}
