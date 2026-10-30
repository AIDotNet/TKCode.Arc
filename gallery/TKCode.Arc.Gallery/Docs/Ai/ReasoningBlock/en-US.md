---
title: Reasoning block
summary: The collapsible thinking block of an AI reply: a card whose label counts the seconds while the model thinks, with the latest reasoning streaming under it, and the full reasoning as steps on a quiet rail once it has finished.
---

<Demo name="Preview" />

## When to use

`ReasoningBlock` is the "thinking" part of an AI reply. While the model thinks, a shimmering "Thinking" label counts the
seconds and the newest words of its reasoning stream under it, two lines tall. When it finishes, the card folds to
"Thought for 7s" with a chevron, and a click opens the whole reasoning as steps with headings, with a footer that says
how it ended and a button that copies it. The answer stays the focus; the reasoning is one click away.

- **Chat replies from a model that reasons**, above the answer.
- **Agent logs** where each turn starts with what the model worked out.
- **Restoring past messages** where the reasoning stays folded with its original thinking time.
- **Audit and review views** where people read how a result was reached.

## When not to use

- **A tool the agent called**: use `ToolCall`; a reasoning block holds text, not an action with input and output.
- **The agent's plan**: use `AgentPlan`; a plan is a checklist the person can edit, reasoning is a record.
- **A one line busy indicator** with no reasoning to show.
- **Content the model must not reveal**: only pass reasoning you want people to read.

## Usage

Set `Status` and pass the reasoning as one growing string in `Reasoning`: blank lines split paragraphs, and a line
written as `**Heading**` or `## Heading` starts a new step. A heading that is still streaming in is held back, so raw
asterisks never show. With no data the block reads "Thought process" and has nothing to open.

```csharp
// While streaming: set the growing text, then flip the status when the answer starts.
block.Reasoning = thinkingText;
block.Status = done ? ReasoningStatus.Done : ReasoningStatus.Thinking;

// A finished message from history.
block.Reasoning = message.Reasoning;
block.Elapsed = TimeSpan.FromSeconds(message.ThinkingSeconds);
```

Or give it steps of your own and append to their `Text` as the model streams:

```xml
<arc:ReasoningBlock Elapsed="0:0:07">
    <arc:ReasoningStep Heading="Check what is open"
                       Text="Two reconciliations are unmatched and one export has not finished." />
</arc:ReasoningBlock>
```

The seconds are counted by the block while it thinks and kept when it ends. Set `Elapsed` yourself when your service
reports the time. A finished block with no time reads "Thought process".

### Anatomy

1. **Head**: the whole top row (44px) is one button: the mark (18px, the accent), the label and its time, Stop or Retry when
   they apply, and a chevron at the far end.
2. **Preview**: while thinking and closed, the newest words of the latest step, two lines at most, muted.
3. **Steps**: when open, each step is a heading over its text beside a rail with a hollow dot.
4. **Footer**: a check and "Done", or "Stopped", with a button that copies the reasoning.

### States

The tabs above show a block live, finished, interrupted and with no data; the statuses read:

| Status | Head reads | Open part |
|---|---|---|
| `Thinking` | A shimmering "Thinking for 3s", optionally a Stop button | The steps so far, streaming |
| `Done` | "Thought for 7s" | The steps, then "Done" and Copy |
| `Stopped` | "Stopped after 4s" | What streamed, then "Stopped" and Copy |
| `Error` | An alert and "Thinking interrupted", with Retry | The steps, then the reason in `ErrorText` |

### Stopping

<Demo name="Stop" />

`AllowsStop` shows a Stop button while the model thinks. It raises `StopRequested`: abort your request, then set
`Status` to `Stopped`. A failed block shows Retry and raises `RetryRequested`. The demo plays a simulation, which Stop
ends on its own.

### Plain

<Demo name="Plain" />

`Variant="Plain"` drops the border and the side padding so the block sits flush inside a message bubble.

### Simulated model

For a demo or a prototype without a model, give the block a `ReasoningBlockSimulation` with the reasoning to play. The
block thinks, counts the seconds and streams the text three words every 45ms, then finishes; it plays only while it has
no reasoning, steps, status or time of its own, starts the simulation over each time it takes it up, and pauses it off
screen. Stop ends it as Stopped, `Restart()` plays it again, and `Finished` tells you when it is done. Your own data
always wins: set `Reasoning` and the simulation steps aside.

```xml
<arc:ReasoningBlock AllowsStop="True">
    <arc:ReasoningBlock.Simulation>
        <arc:ReasoningBlockSimulation Reasoning="The export job is still running, so the close waits for it." />
    </arc:ReasoningBlock.Simulation>
</arc:ReasoningBlock>
```

```csharp
// Or from code, with a reasoning you recorded.
block.Simulation = new ReasoningBlockSimulation { Reasoning = recordedReasoning };
```

### Your own words

`Labels` holds every word the block shows or says. Replace the object to localize or reword:

```csharp
block.Labels = new ReasoningBlockLabels
{
    Thinking = "Reflexion",
    Thought = "Réfléchi",
    Duration = seconds => $"pendant {seconds}s",
};
```

`Mark` replaces the sparkle with your own 24 × 24 stroked glyph, and `MarkBrush` its color.

### Reduced motion

With reduced motion the label does not shimmer, the chevron and the panels change at once and text trades places with a
short fade. With motion off everything changes at once. The seconds still count: they are information, not decoration.

### Content guidelines

- Headings are a few words and name what the model did ("Check what is open"), not what it felt.
- A step is a short paragraph. Split long reasoning into steps instead of one wall of text.
- Time reads "for 7s" or "for 1m 5s"; a stopped block reads "after 4s".

### Best practices

- Put the block above the answer and keep it closed once the answer arrives.
- Stream by setting the growing string, or by appending to `ReasoningStep.Text`; do not rebuild the list on every token.
- Pass `ErrorText` with a failure so the person sees why it stopped, next to Retry.

### Common questions

**Can the person open it while it thinks?** Yes. The preview folds away and the steps stream in place.

**How do I show a fixed time?** Set `Elapsed`; the block then shows that time and does not count.

## API reference

<!-- api: ReasoningBlock -->

<!-- api: ReasoningStep -->

<!-- api: ReasoningBlockSimulation -->

<!-- api: ReasoningBlockLabels -->

## Accessibility

- The head is a button that reports expanded or collapsed; its name reads the label and time, then what pressing does
  ("Thought for 7s, Show reasoning").
- A stopped or failed block says so in the head, not only by color: an alert icon and "Thinking interrupted".
- Keyboard focus shows a 2px ring inside the head; pointer focus draws none.
- Every word is in `ReasoningBlockLabels`.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves to the head, then to Stop or Retry when shown, then to Copy inside the open block |
| Enter, Space | Opens or closes the steps from the head; on a button, presses it |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Label while thinking | Shimmer, one pass each 1.8s | Still | Still |
| Label changes to "Thought" | Old text fades out over 100ms, new text fades in over 160ms and rises 2px | Fades over 130ms | At once |
| Preview appears or leaves | Height springs (0.26s, no bounce), text fades over 160ms | At once | At once |
| A line wraps in the preview | The text glides up on a 0.24s spring instead of jumping | At once | At once |
| Steps open or close | The same 0.26s height spring, content fades over 160ms | At once | At once |
| A step arrives while open | Appended, fades in on its own; nothing else moves | At once | At once |
| Chevron | Turns on a spring | At once | At once |

The head is 44px high in every state and the preview is always two lines tall, so a Stop or Retry button appearing, or a
line streaming in, never changes the block's height.

## Related

- `ToolCall` for the tool calls an agent makes.
- `AgentPlan` for the plan an agent writes before it acts.
- `AgentStatus` for the live status of a whole run.
