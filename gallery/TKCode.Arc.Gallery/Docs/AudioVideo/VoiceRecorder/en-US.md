---
title: Voice recorder
summary: A voice note composer: record into a live waveform, review and scrub the take, then send it into the thread as a voice bubble.
---

<Demo name="Preview" />

## When to use

`VoiceRecorder` is the voice-message corner of a chat: a thread of voice bubbles above a composer with one round mic
button. A tap stretches the mic into a recording bar with a breathing dot, a running timer and a waveform that scrolls in
from the right. Stop turns the same bar into a review where the take plays back and can be scrubbed; send carries the bar
up into the thread, where it lands as your newest bubble. The trash button, or Escape, folds it back into the mic.

- **Async voice replies in a conversation**: support chats, team threads, a client and a contractor trading quick notes.
- **Places where typing is slow**: on a phone, at a counter, on site with gloves on.
- **Reviewing before sending**: people hear and see the take first, and nothing is sent by accident.
- **Threads that mix both directions**: incoming bubbles sit on the left with the sender's avatar, yours on the right.

## When not to use

- **Music, podcasts or long recordings**: use a media player with a full scrubber and queue; a bubble suits short notes.
- **Confirming a destructive action by holding a button**: use a hold-to-confirm control.
- **Showing only how long something has been running**: use a timer or stopwatch, not a recorder.
- **Live dictation into a text field**: use `DictationButton`, which turns speech into words as you talk.
- **Reading a written reply aloud**: use `SpeechPlayer`.

## Usage

Give the recorder its thread and handle the take. With nothing set, the thread is empty and the composer works the same.
The recording is simulated: every take gets a made-up but steady voice, so the control runs without a microphone. Wire
your audio capture through `MessageSent` and `LevelSource` when you need real sound.

```xml
<arc:VoiceRecorder x:Name="Recorder"
                   Height="408"
                   Label="Voice messages with Tomas Novak" />
```

```csharp
Recorder.Messages = VoiceRecorderSamples.Thread();
Recorder.MessageSent += (_, e) => store.Save(e.Message);
Recorder.Cancelled += (_, e) => log.Info($"Discarded {e.Message.Duration}");
```

`VoiceRecorder` derives from Avalonia's `TemplatedControl` and lives in the Special package: register it with
`UseArcSpecial()` next to `UseArcControls()`. The preview above is the reference configuration with invented people: a
416 by 408 card with a header (a drawn initials avatar, the contact's name, the topic and a reset button), a thread of
three messages (theirs, yours, theirs, 14, 6 and 9 seconds long) and the default 60 second limit. The header and the reset
button belong to the demo; the recorder only offers a `Header` slot for them.

### Messages

A thread is a list of `VoiceRecorderMessage` records, oldest first. A message with `From` (or `Avatar`) is incoming: it
sits on the left with a 28px avatar (the picture, or the initials of `From`) in a muted bubble with a dark play button.
A message without either is yours: right-aligned in a dark bubble with a light play button. `Meta` is the small line under
the bubble, such as the time it was sent.

```csharp
var message = new VoiceRecorderMessage
{
    Id = "fence-1",
    From = "Tomas Novak",
    Duration = TimeSpan.FromSeconds(14),
    Peaks = VoiceRecorderPeaks.Generate(seed: 1, seconds: 14),
    Meta = "10:12 AM",
};
```

`Peaks` are the bar heights from 0 to 1, where 0.12 reads as silence. A bubble has 14 bars plus 1.3 per second, between
18 and 36 (`VoiceRecorderPeaks.BarCount`), so longer messages get wider bubbles up to about 20 seconds. Use
`VoiceRecorderPeaks.Resample` to turn your own level series into that many bars (each bar takes the loudest sample of its
stretch), or `VoiceRecorderPeaks.Generate` for a steady made-up waveform.

Sent takes are appended to `Messages` (a read-only list is replaced by a copy with the take). To keep the thread across
sessions, save the message from `MessageSent` and pass it back next time. `ResetMessages` replaces the whole thread.

### Your own events

<Demo name="Events" />

`MessageSent` and `Cancelled` both carry the take as a `VoiceRecorderMessage`: its length, its bars and a generated id.
The sent one also carries `SentMeta` ("Just now" by default) and is already in the thread when the event arrives.
`Phase` tells where the composer is (`Idle`, `Recording`, `Review`) and can be bound. `MaxDuration` ends a take by itself
and opens the review; this demo stops at 15 seconds and scrolls a longer thread.

You can also drive the recorder from code: `StartRecording`, `StopRecording`, `Send`, `Discard`, and `PlayMessage` or
`PauseMessage` with a message id.

### Recording and review

- **Recording**: the bar shows a rose dot that breathes every 1.2 seconds, the time in tenths (0:02.4) and the live
  waveform. A new bar enters at the right every 90ms and the row slides left at a steady pace; bars fade towards the left
  end. The stop button's accessible name keeps the time ("Stop recording at 0:02").
- **Review**: the bar keeps its size and crossfades to a play button, 40 bars of the take and its length. Play fills the
  bars from the left while the time counts up; pause or the end returns the time to the length. A press anywhere on the
  bars jumps there without starting playback.
- **Send**: the bar becomes a bubble at the end of the thread, rising from the list's lower edge while the list glides to
  the bottom; the mic springs back in.

Playback is one sound at a time: starting a bubble pauses any other bubble and the review, and recording pauses all of
them.

### Levels from your microphone

<Demo name="CustomLabels" />

`LevelSource` is called once for every 90ms sample with the take number and the sample index; return the input level from
0 to 1. Without it, the recorder uses `VoiceRecorderPeaks.Levels` with the take number as the seed (the number of
messages plus one), so a discarded take recorded again shows the same voice. This demo also rewords the text through
`Labels` and changes `SentMeta`.

### Narrow containers

<Demo name="Narrow" />

The recorder has no breakpoints: it follows the width it gets. The bar takes what the trash and the send button leave,
bubble bars narrow when a bubble is squeezed, and the review shows fewer bars when even 1.5px bars do not fit. `Plain`
drops the card's border, corners and fill for a phone-width column.

### Reduced motion

<Demo name="ReducedMotion" />

### Anatomy

1. **Header** (optional): any content above a hairline; the preview puts an avatar, a name, a topic and a reset button there.
2. **Thread**: voice bubbles, bottom-aligned, scrolling without a visible scroll bar.
3. **Bubble**: a 44px capsule with a 32px play button, the waveform (played part solid, the rest at 28%) and the time.
4. **Meta line**: 12px muted text under a bubble.
5. **Composer**: the hint and the 48px mic at rest; the trash, the bar and stop (or send) while recording or reviewing.
6. **Status region**: a hidden polite live region for announcements.

### Content guidelines

- Keep the hint to one short line that says what a tap does. It truncates in narrow containers.
- Use real times or relative dates in `Meta`, and keep them short ("10:16 AM", "Yesterday").
- Name the other person in `Label` ("Voice messages with Tomas Novak"): it is the recorder's accessible name.
- Write `SentMeta` as a state, not an instruction ("Just now", "Sent", "Delivered").

### Best practices

- Save the sent message right away; the control keeps it only in `Messages`.
- Pick a `MaxDuration` that suits the conversation: one minute fits most voice notes.
- Give the recorder a fixed height (or a parent that limits it) so the thread scrolls instead of growing.
- Use `Plain` inside panels and phone layouts that already have their own frame.
- When you wire real audio, keep `LevelSource` cheap: it is called on the UI thread every 90ms.

### Common questions

**Does it record sound?** No. The package has no microphone or audio dependency. The levels and the take are simulated;
connect your capture to `LevelSource` and `MessageSent`.

**Why does the same take look the same after I discard it?** The simulated voice is seeded by the take number, which only
changes when a take is sent. Supply `LevelSource` for live levels.

**Can two messages play at once?** No. Starting one pauses the other.

**How do I clear the thread?** Call `ResetMessages` with an empty list, or set `Messages`.

## API reference

<!-- api: VoiceRecorder -->

<!-- api: VoiceRecorderMessage -->

<!-- api: VoiceRecorderLabels -->

<!-- api: VoiceRecorderMessageEventArgs -->

<!-- api: VoiceRecorderPeaks -->

<!-- api: VoiceRecorderSamples -->

The enums and the delegate:

| Type | Values |
|---|---|
| `VoiceRecorderPhase` | `Idle`, `Recording`, `Review` |
| `VoiceRecorderVariant` | `Card` (default), `Plain` |
| `VoiceRecorderLevelSource` | `double (int take, int sample)`: the level of one 90ms sample, 0 to 1 |

## Accessibility

- The recorder is named by `Label`; the thread is named "Messages".
- Every button has a name: "Record voice note", "Stop recording at 0:04" (updated every second), "Discard voice note",
  "Send voice note", "Play preview" or "Pause preview", and on each bubble "Play voice message from Tomas Novak, 0:14" or
  "Play voice message, 0:06" (Pause while it plays).
- A polite live region says "Recording", "Recorded 0:04. Play it back or send it.", "Voice note sent" and "Voice note
  discarded". Playback and reset are not announced.
- Waveforms, the timer and the dot are decorative and hidden from assistive technology; the button names carry the times.
- Keyboard focus shows a 2px ring 2px outside the round buttons; pointer focus draws none.
- Focus follows the composer: the mic hands focus to stop, stop to send, and send or discard back to the mic. A pointer
  press keeps focus inside the recorder too (without a ring), so Escape keeps working.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the header, the bubbles' play buttons, then the composer: the mic at rest; the trash and stop while recording; the trash, play preview and send while reviewing |
| `Enter` / `Space` | Press the focused button |
| `Escape` | While recording or reviewing, from anywhere inside the recorder: discard the take and return focus to the mic |

The waveforms are not focusable; seek with a pointer press.

## Motion

- The mic stretches into the bar (and the bar folds back) on one critically damped spring, about half a second to look
  settled; both edges move together and the content rides at the bar's left end, clipped by it. A fold that starts while
  the bar is still stretching keeps its speed, so it overshoots a touch before it turns.
- The trash and stop pop in from 70% with a quicker spring as the bar opens; on the way out they shrink and fade in 120ms.
- The hint and the bar's rows trade places with a 2px blur: out in 120ms, in over 240ms after 50ms.
- Live bars travel left at about 55 pixels a second and grow over about 110ms.
- On send the bubble grows out of the bar on the same spring as the mic while the list glides to the bottom in about
  130ms; the mic comes back from 80% on a slower spring.
- Round play buttons shrink to 92% while pressed.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Mic to bar, bar to mic, bar to bubble | Spring | Instant | Instant |
| Buttons appearing | Scale and fade | Fade | Instant |
| Hint and bar rows | Blur and fade, 50ms delay | Blur and fade, no delay | Instant |
| Live waveform | Glides, bars grow | Steps one place per sample | Steps one place per sample |
| List after a send | Glides | Jumps | Jumps |
| Record dot | Breathes | Breathes | Still |

## Responsive behavior

- `VoiceRecorder` has no breakpoints; it follows the width its parent gives it, like the reference.
- The bar is the composer row less 104 px (the trash, the send button and two gaps); at 416 px that is 282 px.
- Bubbles keep their natural width while it fits; squeezed, their 3 px bars narrow first.
- The review's 40 bars share the bar's room at 1.5 to 3 px each; below that, fewer bars are drawn instead of cutting off
  the end.
- Height comes from the parent: give the recorder a fixed height so the thread scrolls.

## Related

- `DictationButton` for speech typed into a field as you talk.
- `SpeechPlayer` for reading a written reply aloud.

| Component | Choose it when |
|---|---|
| `VoiceRecorder` | People trade short voice notes in a thread |
| `DictationButton` | Speech should become text |
| `SpeechPlayer` | A written reply should be read aloud |
