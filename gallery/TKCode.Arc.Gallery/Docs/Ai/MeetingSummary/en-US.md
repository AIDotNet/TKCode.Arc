---
title: Meeting summary
summary: AI notes for a call: the platform, attendees and length, then Summary, Decisions and Action items that stream in, each line linked to its transcript quote and each task ready to check off or send to a tracker.
---

<Demo name="Preview" />

## When to use

`MeetingSummary` is the card an AI notetaker leaves after a call. The header names the platform, the date, the length and
the people on the call. A short summarizing pass gives way to three sections that stream in a few words at a time:
Summary, Decisions and Action items. Every line keeps a timestamp, and hovering the line (or focusing its timestamp)
shows the transcript quote it came from, so readers can check a point against what was actually said. Action items can
be checked off and sent to an issue tracker or a docs app.

- **Notes after a call** in a CRM, a recruiting tool, a meeting recorder or a team workspace.
- **Turning a transcript into decisions and owned tasks** that people push to their tracker.
- **Any summary that should be checkable** against its source, line by line.

## When not to use

- **Live captions or a running transcript** during the call: show the transcript itself as it arrives.
- **A general task list** with no meeting behind it: use rows of `Checkbox` in your own list.
- **Long documents with many sections**: render them as a document page instead of a card.

## Usage

Give the card the notes in `Meeting` and drive `Status` yourself: `Summarizing` while your request runs, `Ready` when it
resolves (the notes then stream in), `Error` with `ErrorText` when it fails. Leave `Status` unset and the card plays its
own short summarizing pass when it scrolls into view. With no `Meeting`, no `Summarizer` and no `Simulation` the card
starts on its no transcript state ("No transcript to summarize") with no header details.

```csharp
card.Meeting = new MeetingSummaryData
{
    Title = "Onboarding flow review",
    Platform = "Video call",
    StartsAt = new DateTime(2026, 10, 27, 10, 30, 0),
    DurationMinutes = 48,
    Attendees = [new("ines", "Ines Duarte", "Design"), new("kofi", "Kofi Mensah")],
    Summary = [new("s1", "Most new teams stall on the invite step.",
        new MeetingSummaryQuote("ines", TimeSpan.FromSeconds(252), "Seven of the ten teams got stuck on invites."))],
    ActionItems = [new MeetingActionItem("a1", "Move the invite step after project creation", "kofi",
        new DateOnly(2026, 11, 3))],
};
card.Status = MeetingSummaryStatus.Summarizing;
// ... when your request resolves:
card.Status = MeetingSummaryStatus.Ready;
```

`Quote.At` is the time from the start of the recording; the card shows it as `4:12`. `StartsAt` is printed as given, so
convert time zones before you pass it.

### Simulation

To show the card without a model (a demo, a design review, an onboarding tour), give it a `MeetingSummarySimulation`:
the notes, the model to credit and how simulated sends are numbered. When the card scrolls into view it plays its
summarizing pass, streams the notes in and, unless someone uses the card first, ticks off the second action item and
sends the first one; sends are simulated and say that nothing was sent. Your own `Meeting` or `Summarizer` wins over the
simulation, and setting `Simulation` to null stops it and shows the no transcript state.

```csharp
card.Simulation = new MeetingSummarySimulation
{
    Meeting = notes,
    Model = new AgentModel("Qwen3.8-Max", ArcIcons.Sparkles),
    ReferenceFormats = new Dictionary<string, string> { ["issues"] = "ISS-{0}", ["docs"] = "Notes {0}" },
    FirstReferenceNumber = 142,
};
```

The preview above plays a simulation whose call is the gallery's own demo data.

### Your meeting

<Demo name="YourMeeting" />

This card's data, status and sends belong to the app. Regenerate in the footer and Try again in the error state raise
`RegenerateRequested`; a card that drives its own status also replays its pass.

### Streaming from your model

<Demo name="Summarizer" />

`Summarizer` is an async streaming delegate. When the card drives its own status it calls it on first view and on
Regenerate, shows the summarizing pass until the first snapshot, then streams every snapshot you yield (each one is the
notes so far). Text that still matches keeps streaming, and an exception shows the error state with its message. Set
`Meeting` to the call's header so the title and attendees show while it summarizes.

```csharp
card.Meeting = header; // title, date and attendees, no notes yet
card.Summarizer = SummarizeAsync;

async IAsyncEnumerable<MeetingSummaryData> SummarizeAsync([EnumeratorCancellation] CancellationToken token)
{
    await foreach (var notes in myModel.StreamNotesAsync(transcript, token))
    {
        yield return notes;
    }
}
```

### Sending action items

Each action item has one send button per entry in `SendTargets`: a generic `Issues` and `Docs` by default (pass your own
`MeetingSummarySendTarget` with your product's mark, or an empty list to hide the buttons). Pressing one raises
`SendRequested`; set `Completion` to your request and pass the token on. The button shows a spinner, then a chip with the
reference you return ("ENG-31") and a check, or an alert and Retry when the task faults. Without a handler the send is
simulated and the live region says that nothing was sent; its chip reads "Sent", or the reference the simulation's
`ReferenceFormats` gives.

```csharp
card.SendRequested += (_, e) => e.Completion = CreateIssueAsync(e.Item, e.Target, e.CancellationToken);

async Task<MeetingSummarySendResult?> CreateIssueAsync(MeetingActionItem item, MeetingSummarySendTarget target,
    CancellationToken token)
{
    var issue = await tracker.CreateAsync(item.Text, token);
    return new MeetingSummarySendResult(issue.Key, issue.Url);
}
```

`ActionItemSent` follows a successful send. The token is cancelled when the card is removed or regenerated, and the
button returns to its mark. A sent chip with a `Url` opens it.

### Checking items off

Checking an item raises `ActionItemChanged` with the item and its new state; the card keeps the checked state itself.
An item whose `IsDone` is true starts checked.

### Transcript quotes

Hover any line for a short moment and its quote opens under it (it flips above when the window runs out of room).
While a quote is open, moving to another line switches at once. Focusing a timestamp with the keyboard shows its quote;
Enter, Space or a tap pins it, and Escape or a press elsewhere closes it.

### States

| Status | Body |
|---|---|
| `Summarizing` | A shimmering "Summarizing transcript" over placeholder lines |
| `Ready` | Summary, Decisions and Action items (sections with no lines are left out) |
| `Ready`, empty lists | "No transcript to summarize" |
| `Error` | "Couldn't summarize this meeting", `ErrorText` and Try again |

### Narrow cards

<Demo name="Narrow" />

The card fills its container up to 640px and holds down to about 360px. Below 460px the attendee stack moves under the
date; below 520px owners show their first name so the meta under each item stays on one line (screen readers still
hear the full name). `AutoPlay="False"` starts on the finished notes.

### Your own words

`Labels` holds every word the card shows or says. Replace the object to localize or reword:

```csharp
card.Labels = new MeetingSummaryLabels
{
    Summary = "Résumé",
    ActionItems = "Actions",
    Due = due => "Pour le " + due.ToString("d MMM"),
};
```

`Model` sets the model credited in the footer (an `AgentModel` with a name and a mark); null hides the credit.
`MeetingSummaryData.PlatformMark` replaces the video glyph with your platform's mark.

### Reduced motion

With reduced motion the notes appear whole instead of streaming, the body and send slots change size at once, and the
checkbox, strikethrough and quote show their end states without drawing or sliding. Every announcement still happens.
The summarizing pass waits until the card is on screen.

### Content guidelines

- A summary line is one sentence about what happened; a decision says what was decided, not who argued.
- Action items start with a verb and name one owner.
- Keep each quote to the sentence or two that backs the line.

### Common questions

**Can I show notes that are already done?** Set `Status` to `Ready` (or `AutoPlay` to false) and the notes show whole.

**Who keeps the checked state?** The card. `ActionItemChanged` tells you about each change; `IsDone` seeds the first state.

## API reference

<!-- api: MeetingSummary -->

<!-- api: MeetingSummaryData -->

<!-- api: MeetingActionItem -->

<!-- api: MeetingSummarySendTarget -->

<!-- api: MeetingSummaryLabels -->

<!-- api: MeetingSummarySimulation -->

## Accessibility

- The card is a group named after the meeting, with the title as a level 2 heading and each section as a level 3 heading.
- Timestamps are buttons that report expanded while their quote is open; the quote's text is their help text.
- Each checkbox is a button with the checkbox role and its checked state, and each checkbox and send button is named with
  the action item's text, so a list of them still reads clearly.
- The 16px checkbox has a 32px hit area, and the keyboard focus ring hugs the square.
- A polite live region announces summarizing, the finished summary, checked items and sends, and says "Simulated,
  nothing was sent" when no handler sent it.
- Status never relies on color: a failed send shows an alert icon with Retry, a sent item shows the target's mark, its
  reference and a check.
- The platform name is read before the date, and owners shown by first name keep their full name for screen readers.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through the attendees, each timestamp, checkbox and send button, then copy and regenerate |
| Focus on a timestamp | Shows the transcript quote for that line |
| Enter, Space | On a timestamp, pins or closes its quote; on a checkbox, toggles it; on a send button, sends the item |
| Escape | Closes an open quote |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Summarizing | About one second, the label shimmers | Same, no shimmer | Same, no shimmer |
| Notes arrive | 3 to 8 words every 34ms, each chunk fades in (about 1.5s in all) | Whole at once | Whole at once |
| Body between states | Height springs (0.24s, no bounce), the new state fades in | At once | At once |
| Owner, date, buttons | Keep their space while the text streams, fade in once it is whole | At once | At once |
| Check an item | Fill grows in (160ms), check draws on (200ms ease-out), a 1px strikethrough sweeps across every wrapped line, text eases to muted | End state at once | End state at once |
| Uncheck | The same in reverse, faster (120ms) | At once | At once |
| Press a checkbox | The square scales to 0.9, never the row | Same | Same |
| Send | Mark, spinner and sent chip crossfade while the slot springs to its width (0.25s, no bounce) | Short crossfade, size at once | At once |
| Quote | Fades in over 160ms rising 3px after a short hover; switches between lines at once | Fades | At once |

## Related

- `ToolCall` for the tool calls an agent makes.
- `AgentStatus` for the live status of a running agent.
- `Checkbox` for a plain checklist.
- `Avatar` and `AvatarGroup` for people.
- `CopyButton` for copying a value with confirmation.
