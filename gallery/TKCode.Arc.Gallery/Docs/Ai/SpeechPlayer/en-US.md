---
title: Speech player
summary: A read-aloud player for AI replies: Read aloud turns into a compact player with play, a scrubbable progress bar, speed and a voice picker, and a highlight follows the spoken word through the reply.
---

<Demo name="Preview" />

## When to use

`SpeechPlayer` reads an AI reply out loud. The reply sits above its action row; Read aloud turns the row into a compact
player of the same height, with play and pause, the elapsed time, a thin progress bar you can scrub, the total time, a
speed button, a voice picker and Stop reading. While it plays, one rounded highlight follows the spoken word through
the text, karaoke style, and the words still to come stay dimmed.

- **Chat and answer surfaces** where people want a reply read out and the spoken word followed in the text.
- **Accessibility and hands-free reading** of long answers, summaries or briefings.
- **Previewing text to speech voices and speeds** against real product copy.

## When not to use

- **Turning speech into text** in a composer: use a dictation control.
- **Podcasts or long recordings** with no transcript to highlight: use a plain audio player.
- **Text that is not exactly what is spoken**: strip Markdown and formatting first, or the highlight drifts.

## Usage

Set `Text` to the reply and `Synthesize` to a call to your text to speech service. It receives a `SpeechRequest` (the
text, the voice and a cancellation token) and returns a `SpeechAudio`: the playback as an `ISpeechAudio`, the word
timings and the length. The player drives the playback (play, pause, seek, rate) and reads its position on every frame,
so your audio is the clock.

```csharp
player.Text = message.Text;
player.Synthesize = async request =>
{
    var speech = await tts.SpeakAsync(request.Text, request.Voice.Id, request.CancellationToken);
    return new SpeechAudio
    {
        Audio = new MyAudioAdapter(speech.Stream),   // implements ISpeechAudio
        Timings = speech.Words                       // one start and end per word, or null
            .Select(w => new SpeechWordTiming(w.Start, w.End))
            .ToList(),
    };
};
```

Each whitespace separated word of `Text` is one highlight step, and blank lines start new paragraphs, so a provider's
per-word alignment maps one to one. Without timings, the player estimates them from the text and fits them to the
audio's length (`SpeechSimulation.EstimateTimings`, `SpeechSimulation.FitTimings`). Without `Synthesize` the player is a
timed simulation that plays no sound. Without `Text` (and without a simulation, below) the player is empty: no reply,
and Read aloud waits disabled (the Empty tab above). Without `Voices` the picker offers one voice with an empty id, your
service's default, named by `SpeechPlayerLabels.DefaultVoice`.

### Anatomy

1. **Author line**: a 16px mark and the model's name (`AuthorName`, `AuthorMark`); hidden when there is no author.
2. **Reply**: the text in paragraphs. While reading, the spoken word sits on an accent highlight, spoken words are in
   full color and the words to come are dimmed.
3. **Dock**: one 32px slot that holds either the action row (Read aloud, then Copy, Good response and Bad response, or
   your `Actions`) or the player.
4. **Player**: play, elapsed time, progress bar, total time, speed, voice and Stop reading.

### States

| State | The player shows |
|---|---|
| `Idle` | The action row with Read aloud |
| `Preparing` | A spinner in the play button and a shimmering "Preparing Ava's voice" |
| `Playing` | Pause, the moving bar and the highlight following the words |
| `Paused` | Play; the highlight rests on the word |
| `Finished` | Play again; the whole reply in full color |
| `Failed` | Retry, an alert icon and "Couldn't generate audio" with the reason your service threw |

Picking another voice while reading aborts the request for the old voice, fetches the new one and resumes from the word
being spoken. Speech is kept per voice, so going back to a voice, or reading again after Stop reading, starts at once.

### In a chat column

<Demo name="Plain" />

`Variant="Plain"` drops the border and the padding so the reply sits in the transcript. `Actions` replaces Copy, Good
response and Bad response with your own buttons; `ShowsDefaultActions="False"` leaves Read aloud alone. `Voices` sets
the picker's voices, each with an id, a name and one short line of description.

### Events

<Demo name="Events" />

`Played`, `Paused`, `Ended` and `Seeked` carry the time and the length on the 1× timeline, the word index, the voice
and the speed. `WordChanged` adds the word, for a caption or analytics. `Seeked` is raised once when a drag on the bar
ends, not on every move. `VoiceChanged`, `SpeedChanged` and `Closed` complete the set. `SelectedVoiceId` and `Speed`
are two-way.

```csharp
player.WordChanged += (_, e) => caption.Text = e.Word;
player.Played += (_, e) => analytics.Track("tts_play", e.Voice.Id, e.Speed);
```

### Driving it from code

`Open()` does what Read aloud does, `Play()`, `Pause()`, `TogglePlayback()`, `Seek(TimeSpan)`, `SeekToWord(int)`,
`Retry()` and `Close()` do what their buttons do. `State`, `IsOpen`, `CurrentWordIndex`, `Position` and `Duration` say
where it is.

### Simulated speech

For a demo, leave `Text` unset and give the player a `SpeechSimulation` with your own reply, author and voices. It reads
once the player is in view and, with full motion, reads again with the other of its first two voices after a short
pause, until someone interacts. The app's own `Text` always wins, and setting `Simulation` to null stops it. The
preview's reply and voices are invented; its author is a real model, Claude Sonnet 5.5.

```csharp
player.Simulation = new SpeechSimulation
{
    Text = "Lisbon in early May is a lovely choice. Mornings start near 16°C…",
    AuthorName = "Claude Sonnet 5.5",
    Voices = [new SpeechVoice("ava", "Ava", "Warm and conversational"), new SpeechVoice("leo", "Leo", "Calm, lower register")],
};
```

`SpeechSimulation.SynthesizeAsync` is the stand-in behind every player without `Synthesize`: it waits like a network
round trip (640ms) and answers with estimated timings and no audio. Pass `Fail = true` to see the failed state, or a longer `Delay` to see
Preparing. Keep `IsSimulated` on while you use it, so the voice menu and the announcements say that no audio plays.

```csharp
player.Synthesize = request => SpeechSimulation.SynthesizeAsync(request,
    new SpeechSimulationOptions { Delay = TimeSpan.FromSeconds(2) });
```

### Your own words

`Labels` holds every word the player shows or says. Replace the object to localize or reword:

```csharp
player.Labels = new SpeechPlayerLabels
{
    ReadAloud = "Vorlesen",
    StopReading = "Vorlesen beenden",
    PreparingVoice = voice => $"Stimme {voice} wird vorbereitet",
};
```

### Narrow containers and touch

The card fills its container up to 640px. Below 440px the total time steps aside so the bar keeps its room; the elapsed
time, speed, voice and Stop reading stay. After a touch, the dock grows to 36px so every control meets the touch target
size.

### Best practices

- Pass exactly the text that is spoken: the number of words must match the timings.
- Throw from `Synthesize` with a short, human message; it is shown next to "Couldn't generate audio".
- Honor the request's cancellation token: a new voice, Stop reading or new text cancels it.
- Use the plain variant inside a transcript and pass actions that match the rest of the message row.

### Common questions

**Does it play sound by itself?** No. `TKCode.Arc.Special` has no audio dependency; your `ISpeechAudio` plays the
sound. Without it the player keeps time on its own and plays nothing.

**What if my provider has no word timings?** Return the audio alone. The timings are estimated from the text and
fitted to the audio's length.

## API reference

<!-- api: SpeechPlayer -->

<!-- api: SpeechVoice -->

<!-- api: SpeechAudio -->

<!-- api: ISpeechAudio -->

<!-- api: SpeechRequest -->

<!-- api: SpeechWordTiming -->

<!-- api: SpeechPlaybackEventArgs -->

<!-- api: SpeechWordEventArgs -->

<!-- api: SpeechVoiceChangedEventArgs -->

<!-- api: SpeechSpeedChangedEventArgs -->

<!-- api: SpeechSimulation -->

<!-- api: SpeechSimulationOptions -->

<!-- api: SpeechPlayerLabels -->

## Accessibility

- The player is a labelled group. The play button's name follows its state: Play, Pause, Play again, Retry or
  Preparing audio (it keeps focus while a request is in flight but does nothing).
- The progress bar is a slider whose value text reads "0:04 of 0:22", updated once a second rather than every frame.
- A polite status region says what changed: reading with Ava at 1× (simulated, no audio plays), paused at 0:12,
  switching to Leo, finished, the failure, speed changes and feedback.
- Status never relies on color: a failure pairs an alert icon with "Couldn't generate audio" and the reason; dimmed
  words to come keep body contrast and are only a progress cue.
- Read aloud is described by the reply. The voice menu has radio rows and opens on the selected voice.
- The hidden half of the dock leaves the tree once it has faded, so Tab never lands on a control you cannot see.
  Pointer focus draws no ring; keyboard focus draws Arc's focus ring.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through Read aloud and the actions, or, while reading, through play, the progress bar, speed, voice and Stop reading |
| Enter, Space | On Read aloud, opens the player and starts reading with focus on play; on play, plays or pauses; on Stop reading, closes the player and returns focus to Read aloud |
| Arrow Left, Arrow Right | On the progress bar, 2 seconds back or forward |
| Page Down, Page Up | On the progress bar, 10 seconds back or forward |
| Home, End | On the progress bar, the start or the end |
| Space, K | On the progress bar, play or pause |
| Arrow Up, Arrow Down | In the voice menu, move between voices; Enter picks one and Escape closes the menu |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Read aloud and Stop reading | The row fades out over 100ms, the other fades in over 160ms and rises 2px, in the same 32px slot | 120ms crossfade | At once |
| The highlight moves along a line | Glides in 150ms at 1×, 100ms at 1.5×, 75ms at 2× | Moves at once | At once |
| The highlight reaches a new line | Cuts to it | Cuts to it | Cuts to it |
| A word is spoken | Fades from dimmed to full color as the highlight lands | Fades | At once |
| Reading starts or stops | The dimming and the highlight fade over 160ms | Fade | At once |
| Progress bar | Fill and thumb follow every frame; hover, focus and drag thicken the track and scale the thumb in over 160ms | Thickens at once | At once |
| Play, pause, play again, retry, spinner | Swap in place with a bounce-free scale (0.26s) and a 160ms fade | 120ms fade | At once |
| Speed and voice labels | Crossfade over hidden copies of every option, so the buttons never resize | 120ms fade | At once |
| Preparing label | A calm shimmer | Still | Still |
| A simulation | Loops through two voices until someone interacts | Reads once | Reads once |

The clock runs at every motion level: reading is content, not decoration. A resize re-measures the highlight without
animating it.

## Related

- `AgentStatus` for the live status of an agent while it works on the reply.
- `ToolCall` for the tools an agent called while answering.
- `CopyButton` for the copy action in a message row.
