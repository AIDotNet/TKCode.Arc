---
title: Call insights
summary: A recorded sales call taken apart: a timeline with the sentiment curve, a lane per speaker, key moments and topics under a playhead you play, drag or step; a transcript that follows it; the talk-time split and the next steps.
---

<Demo name="Preview" />

## When to use

`CallInsights` is the page a sales team opens after a recorded call. It answers four questions at a glance: how the call
felt over time, who did the talking, what was discussed and what was agreed. The timeline puts the sentiment curve (green
above the neutral line, red below), one lane per speaker and the key moments on one time axis under a playhead; the
transcript beside it follows the playhead, so a moment on the timeline and the words behind it are always one click apart.

- **Reviewing a discovery or demo call.** See where the mood dropped, jump to the objection and read what was said.
- **Coaching.** The talk-time split, the longest monologue and the questions the rep asked show how balanced the call was.
- **Handing a deal over.** Topics and next steps tell the next person what was covered and what is owed.

## When not to use

- **Live calls.** The block reviews a recording; it does not stream speech or transcribe.
- **Audio playback.** The block has no audio of its own. Bind `Position` to your player when you have one.
- **Long meeting notes without speakers.** Use `Markdown` or a document view when there is no transcript to align.

## Usage

The block derives from `TemplatedControl`. Without data it shows an invented 24-minute discovery call. Give it your own
call through five collections: `Speakers` (key, name, role and whether they are on your side), `Utterances` (speaker key,
start, end, text and a sentiment from −1 to 1), `Moments`, `Topics` and `NextSteps`.

```xml
<arc:CallInsights Title="Renewal check-in" Subtitle="Thu, 6 min" IsFramed="False" />
```

```csharp
call.Speakers.Add(new CallInsightsSpeaker("jonas", "Jonas Weber", "Customer success", isHost: true));
call.Utterances.Add(new CallInsightsUtterance("jonas", TimeSpan.Zero, TimeSpan.FromSeconds(40), "How has the queue been?", 0.3));
call.NextStepToggled += (_, e) => tasks.SetDone(e.Step.Text, e.IsDone);
```

### Timeline and playhead

`Position` (two-way) is the playhead. A press or drag on the timeline moves it, a press on a moment mark snaps to that
moment, and a transcript line or a moment in the side list jumps to its start. Each of these raises `Seeked`. The play
button moves the playhead in real time at `PlaybackRate` (the speed button cycles 1×, 1.5× and 2×) and stops at the end;
`Play`, `Pause` and `Seek` do the same from code.

### Transcript, topics and next steps

`SearchText` keeps the lines that contain it and marks them under the lanes. Choosing a topic chip (`SelectedTopic`)
tints its stretches on the timeline. Ticking a next step writes `IsDone` and raises `NextStepToggled`. The talk-time split,
the longest monologue and the questions asked are worked out from the utterances (`TalkShares`). Every word the block
shows is in `Labels` (`CallInsightsLabels`).

<Demo name="OwnCall" />

## API reference

<!-- api: CallInsights -->

<!-- api: CallInsightsSpeaker -->

<!-- api: CallInsightsUtterance -->

<!-- api: CallInsightsMoment -->

<!-- api: CallInsightsTopic -->

<!-- api: CallInsightsNextStep -->

## Accessibility

- The timeline is a slider named "Call timeline": **←/→** move 5 s (**Shift**: 30 s), **Home/End** jump to the ends,
  **PageUp/PageDown** to the previous or next moment, and **Space** plays or pauses. Its value is the position in seconds.
- Transcript lines, moments and topic chips are buttons named by what they say; the chosen topic reports "selected".
- Next steps are checkboxes named by the step, with the owner and due date as their description.
- Seeks, topic choices and search counts are announced politely.

## Motion

- The sentiment curve draws in from the left the first time the block is shown (900 ms).
- Rows and chips tint in 160 ms on hover; the playhead moves by a transform, so playback does not redraw the curves.
- Reduced motion and Off show the curve at once and keep the tints without easing.

## Responsive behavior

- The block reads its own width. At `CompactWidth` (760 px) or less the side column moves under the transcript and the
  padding tightens from 24 to 16.
- The timeline always takes the full width; tick labels thin out so they stay at least 70 px apart.
- Long names and the hover readout end with an ellipsis instead of wrapping.

## Related

- `LineChart` and `Sparkline` for plain time series.
- `Timeline` and `ChatThread` for activity and messages.
- `Account health` and `Lead scoring`, the other sales blocks.
