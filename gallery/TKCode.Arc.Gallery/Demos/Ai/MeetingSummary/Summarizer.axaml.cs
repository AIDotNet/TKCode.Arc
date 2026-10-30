using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>The notes from an async streaming delegate: snapshots that grow, and a failure on every other try.</summary>
public partial class MeetingSummarySummarizerDemo : UserControl
{
    private int _tries;

    public MeetingSummarySummarizerDemo()
    {
        InitializeComponent();
        // The call's header shows while the notes are summarized; each snapshot then takes its place.
        Card.Meeting = Header();
        Card.Model = MeetingSummaryDemoData.Model;
        Card.Summarizer = SummarizeAsync;
    }

    /// <summary>A stand-in for your model call: each yield is the notes so far.</summary>
    private async IAsyncEnumerable<MeetingSummaryData> SummarizeAsync([EnumeratorCancellation] CancellationToken token)
    {
        var attempt = ++_tries;
        await Task.Delay(1100, token);
        if (attempt % 2 == 0)
        {
            throw new InvalidOperationException("The model timed out after 30 seconds.");
        }
        var notes = Copy(Header());
        notes.Summary.Add(new("s1", "Escalations doubled after the sync change, mostly from accounts with more than one workspace.",
            new("ada", new TimeSpan(0, 1, 50), "It's almost all multi-workspace accounts. Single workspace is fine.")));
        yield return notes;

        await Task.Delay(400, token);
        notes = Copy(notes);
        notes.Decisions.Add(new("d1", "Roll the sync change back for multi-workspace accounts until the fix ships.",
            new("luis", new TimeSpan(0, 9, 12), "Let's flag it off for multi-workspace accounts today.")));
        yield return notes;

        await Task.Delay(400, token);
        notes = Copy(notes);
        notes.ActionItems.Add(new("a1", "Turn the sync flag off for multi-workspace accounts", "luis", new DateOnly(2026, 10, 27),
            new("luis", new TimeSpan(0, 10, 3), "I'll turn the flag off right after this.")));
        notes.ActionItems.Add(new("a2", "Reply to the open escalations with the workaround", "ada", new DateOnly(2026, 10, 27),
            new("ada", new TimeSpan(0, 15, 40), "I'll answer the open tickets with the workaround tonight.")));
        yield return notes;
    }

    private static MeetingSummaryData Header() => new()
    {
        Title = "Support escalation review",
        StartsAt = new DateTime(2026, 10, 26, 16, 0, 0),
        DurationMinutes = 22,
        TranscriptWords = 2950,
        Attendees = [new("ada", "Ada Brooks", "Support"), new("luis", "Luis Ortega", "Engineering")],
    };

    private static MeetingSummaryData Copy(MeetingSummaryData notes) => new()
    {
        Title = notes.Title,
        StartsAt = notes.StartsAt,
        DurationMinutes = notes.DurationMinutes,
        TranscriptWords = notes.TranscriptWords,
        Attendees = notes.Attendees,
        Summary = [.. notes.Summary],
        Decisions = [.. notes.Decisions],
        ActionItems = [.. notes.ActionItems],
    };
}
