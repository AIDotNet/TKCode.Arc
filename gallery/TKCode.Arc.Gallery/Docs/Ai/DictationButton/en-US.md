---
title: Dictation button
summary: A chat composer with voice input: the mic springs into a recording pill with a live waveform, a timer, cancel and Insert, and words stream into the field as you speak, muted until the recognizer settles them.
---

<Demo name="Preview" />

## When to use

`DictationButton` is a composer for people who would rather talk than type. Pressing the mic springs it leftward into
a recording pill: cancel, a live waveform, the elapsed time and Insert, which takes the mic's place so the next action
sits where the pointer already is. Words stream into the field while the person speaks, the recognizer's guesses muted
until they settle. Insert appends the text and hands the focus back to the field; Enter sends.

- **Chat and agent composers** on touch devices, for long prompts, or for people who find typing hard.
- **Notes, comments and support replies** where dictating saves time and the person still edits before sending.
- **Any field that already sends** and needs a voice entry point with a clear listening state: use `DictationControl`.

## When not to use

- **Full duplex voice conversations** with spoken replies; use a voice agent pattern with call controls instead.
- **Recording audio to keep** (voice memos): dictation inserts text and discards the audio.
- **Command palettes** where one spoken command should run at once; confirm hands-free from `SpeechEnded` only if you
  are sure.

## Usage

TKCode.Arc records nothing itself. Speech comes from an `IDictationRecognizer` that you write for your platform or
speech service; it reports the input level and the words, and the composer does the rest. Without one (and without a
simulation, below) the composer is a plain field: no model label, no placeholder, and pressing the mic says dictation
is not available (`DictationErrorCode.Unsupported`).

```xml
<arc:DictationButton Placeholder="Message" MaxDuration="60" />
```

```csharp
composer.SendRequested += (_, e) =>
{
    // The send button spins until the task completes; the field clears when it succeeds.
    e.Completion = chat.SendAsync(e.Text);
};

composer.Recognizer = new MyPlatformRecognizer();
```

`Text` is two-way: typing, an inserted dictation and the clear after a send all change it. Without a
`SendRequested` handler the send is simulated. `Model` is the label at the start of the toolbar (null, the default, hides
it); keep it honest and pass the model the composer actually sends to. The preview shows a real model, Claude Sonnet 5.5,
as the model its message would go to; the dictated words are invented.

### Anatomy

1. **Card**: a Surface card with a 1px border that fills its container.
2. **Field**: 16/24 text, one to six lines, then it scrolls and keeps the newest words in view. Live words follow the
   text: settled words in the text color, the recognizer's guess in a muted tone.
3. **Toolbar** (36px): the label (the model, the keyboard hints while listening, or an error), the dictation slot and
   the accent send button.
4. **Recording pill**: 216px, 36px high with 4px padding: the 28px cancel, the waveform, the timer and the 28px Insert.

### States

| Status | The slot shows | The toolbar label |
|---|---|---|
| `Idle` | The mic | The model |
| `Starting` | The pill, the bars resting as dots | The keyboard hints |
| `Listening` | The pill with the live waveform and the timer | The keyboard hints |
| `Processing` | The pill, Insert spinning ("Transcribing") | The model |
| `Denied` | Try again with a mic-off icon | "Microphone blocked in system settings" |
| `Error` | Try again with an alert icon | A short reason |

`MaxDuration` (120 seconds by default, 0 for no limit) stops listening and inserts on its own.

### Your own recognizer

<Demo name="YourRecognizer" />

The contract is small. `StartAsync` completes once audio flows, or throws `DictationException` with `NotAllowed` for a
blocked microphone. While it runs, report through the handlers; they may be called from any thread. Cancelling the token
means the person cancelled: release the microphone. `StopAsync` returns the final text.

```csharp
public sealed class MyPlatformRecognizer : IDictationRecognizer
{
    public async Task<IDictationSession> StartAsync(DictationHandlers handlers, CancellationToken cancellationToken)
    {
        var engine = await SpeechEngine.OpenAsync(cancellationToken)
            ?? throw new DictationException(DictationErrorCode.NotAllowed);
        engine.Result += (final, interim) => handlers.OnTranscript(final, interim);
        engine.Level += level => handlers.OnLevel(level);         // 0 to 1, as often as you like
        engine.UtteranceEnded += () => handlers.OnSpeechEnd();
        engine.Failed += code => handlers.OnError(code);
        cancellationToken.Register(engine.Dispose);
        return engine;                                            // an IDictationSession: StopAsync returns the text
    }
}
```

For demos and design reviews, set `Simulation` to a `SimulatedDictationRecognizer` instead: it never touches the
microphone and speaks your own `Scripts`, one per dictation, each a list of `DictationPhrase`s with an optional misheard
`Guess` that it streams first and then corrects. `Deny` rejects every start like a blocked microphone; `StartDelay`,
`FinalizeDelay` and `WordInterval` set its pace. With no scripts the voice stays silent. A `Recognizer` always wins over
the simulation, and setting `Simulation` to null stops it. Give each control its own.

```csharp
composer.Simulation = new SimulatedDictationRecognizer
{
    Scripts =
    [
        [
            new DictationPhrase("Draft a short reply to the design team", "Draft a short reply to the desk team"),
            new DictationPhrase("and ask if the empty states can ship on Friday."),
        ],
    ],
};
```

### Only the control

<Demo name="ControlOnly" />

`DictationControl` is the mic and its pill without a field, for a composer you already have. Render
`TranscriptChanged` yourself with the interim words muted, append the text of `Committed`, and drop the live words on
`Cancelled`. `SpeechEnded` tells you the recognizer heard the end of an utterance. The headless state machine behind
both, `DictationController`, is public too.

### A blocked microphone

<Demo name="Blocked" />

When the recognizer reports `NotAllowed`, the slot springs to Try again with a mic-off icon and the toolbar says where
to fix it. Other errors show an alert icon and a short reason. Try again starts over; Escape dismisses it.

### Narrow columns

<Demo name="Narrow" />

The breakpoints follow the card's own width, not the window: the keyboard hints hide under 460px, error copy shortens
under 400px, and at 360px the toolbar label gives way while recording. The pill keeps its 216px, so nothing reflows
while the person speaks.

### Reduced motion

<Demo name="ReducedMotion" />

With reduced motion the pill and the field change size at once, words appear without fading and the waveform stops
travelling: its bars pulse in place with the voice. With motion off the bars rest as dots.

### Autoplay

With a `Simulation` and no `Recognizer` the composer plays its scripts on its own (listen, insert, send) until the first
pointer, key or focus inside the card, and pauses while it is off screen. Set `AutoPlay="False"` to turn it off; it is
for docs and demos, not production.

### Your own words

`Labels` holds every word the composer shows or says, including the errors and the announcements:

```csharp
composer.Labels = new DictationButtonLabels
{
    Dictate = "Dicter",
    Insert = "Insérer",
    Error = code => code == DictationErrorCode.NotAllowed ? "Micro bloqué dans les réglages" : "La dictée a échoué",
};
```

### Best practices

- Say in your product copy what to do when the microphone is blocked; the composer already says where to fix it.
- Keep the label honest: pass the model the composer sends to, or null.
- Do not ship autoplay; it is for docs and demos.

## API reference

<!-- api: DictationButton -->

<!-- api: DictationControl -->

<!-- api: SimulatedDictationRecognizer -->

<!-- api: DictationButtonLabels -->

## Accessibility

- A polite live region announces listening (and that the simulated recognizer records nothing), transcribing, the
  inserted word count, cancel, send and every error with how to fix it. Streaming words are not announced.
- Status is never color alone: a blocked microphone shows a mic-off icon and its message, other errors an alert icon and
  a short reason; Try again keeps the same icon.
- Only the visible face of the slot takes the pointer and Tab. Focus follows the action: the mic moves it to Insert,
  Insert back to the field at the end of the text, cancel back to the mic, a failed start to Try again.
- The elapsed time uses tabular numerals in a reserved width; the waveform is decorative and hidden from assistive tech.
- The mic, cancel, Insert and send are icon buttons with names and tooltips (Dictate, Cancel, Insert or Transcribing,
  Send).
- Interim words use a tone between TextMuted and TextSecondary and settle to the text color, so the difference reads in
  both themes without relying on hue.

### Keyboard

| Keys | Action |
|---|---|
| Enter, Space | On the mic: start dictating; the focus moves to Insert |
| Enter | While listening: stop and insert, from anywhere inside the card |
| Escape | While dictating: cancel and discard; on Try again: dismiss the error |
| Enter | In the field: send. Shift+Enter adds a line |
| Tab | Moves through cancel, Insert, then send; hidden faces of the slot are skipped |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Mic to pill | The width springs leftward to 216px (critically damped, 0.24s, no bounce) | At once | At once |
| Pill to Try again | The width springs to the label's measured width | At once | At once |
| Faces of the slot | Crossfade: out 100ms, in 160ms | Crossfade | At once |
| Toolbar label | Old out 100ms, new in 160ms rising 2px | Short fade | At once |
| Insert while transcribing | The check crossfades into a spinner (160ms) | At once | At once |
| A word arrives | Fades in over 140ms; a corrected guess refades only the words that changed | At once | At once |
| The field grows | Springs to each new line | At once | At once |
| Waveform | A sample enters on the right every 60ms and each bar eases toward it every frame; on stop the bars settle to dots | Bars pulse in place with the voice | Dots |

The waveform's frame loop stops once the bars have settled and pauses while the card is off screen or hidden. Frames
allocate nothing: the levels live in a fixed ring buffer and the bars are drawn by one control.

## Related

- `AiComposer` for a full prompt box with attachments, tools and a model menu.
- `ToolCall` and `AgentStatus` for what the agent does with the message.
- `Tooltip` for the names of the icon buttons.
