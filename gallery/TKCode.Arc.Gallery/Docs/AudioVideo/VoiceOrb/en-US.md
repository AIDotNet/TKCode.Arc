---
title: Voice orb
summary: A glowing voice-mode sphere that breathes at rest, ripples while it listens, swirls while it thinks and pulses as it speaks, with live captions and call controls.
---

<Demo name="Preview" />

## When to use

`VoiceOrb` is the face of a voice conversation with an assistant. It is a fluid, marbled sphere that tells people at a
glance whose turn it is: it breathes slowly while nothing happens, swells with the user's voice and sends out ripples
while it listens, draws in with a bright band running around its rim while it thinks, and grows lumpier and pulses with
every word while it answers. Two companions complete a voice-mode screen: `VoiceTranscript` shows live captions, word by
word, and `VoiceControls` is the call bar with Start, Mute, a timer and End.

- **A hands-free assistant screen**: a kitchen display, a car dashboard, a help desk kiosk, a phone call with a bot.
- **A voice mode inside a chat app**: the user taps a button, the chat steps back and the orb takes over until the call
  ends.
- **Any place where people need to know whether they are being heard**: listening, thinking and speaking look clearly
  different, and the muted orb goes quiet and gray.

## When not to use

- **Recording a voice message to send**: use a recorder with a waveform and a review step; the orb shows a live
  conversation, not a take.
- **Text chat**: use a chat thread with streamed text. Captions show only the current turn.
- **A generic loading state**: use a spinner, a progress bar or a skeleton. The orb's motion means "the assistant is
  thinking about what you said", not "the page is loading".
- **Dictation into a text field**: use `DictationButton`, which writes into the field.

## Usage

The three controls live in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`. Each is
independent; a voice screen usually stacks them.

```xml
<StackPanel Spacing="14" MaxWidth="480">
    <arc:VoiceOrb State="{Binding State}" IsMuted="{Binding IsMuted}" Size="200" />
    <arc:VoiceTranscript Turn="{Binding Turn}" Speaker="{Binding Speaker}"
                         Text="{Binding Caption}" IsComplete="{Binding CaptionIsFinal}" />
    <arc:VoiceControls IsActive="{Binding InCall, Mode=TwoWay}" IsMuted="{Binding IsMuted, Mode=TwoWay}"
                       Elapsed="{Binding Elapsed}" />
</StackPanel>
```

The preview above is driven by `VoiceOrbSession`, a stand-in for a real voice backend that the package ships for demos
and prototypes. It plays an invented weekend-trip conversation in a loop with realistic timing: the user's words appear
0.9 s after listening starts and then every 150 ms plus 22 ms per character of the previous word, the orb thinks 0.7 s
after the last word for 1.5 s, the answer is spoken word by word (each word pulses the orb), and the next exchange starts
1.3 s after the answer ends. Tapping the orb does what the state invites: start the call, send the sentence now, cancel
the answer and keep talking, or interrupt the assistant. The orb button and the status line above it belong to the demo.

```csharp
var session = new VoiceOrbSession(VoiceOrbSamples.WeekendTrip);
var link = session.Attach(Orb, Transcript, Controls);   // dispose to disconnect
OrbButton.Click += (_, _) => session.Tap();
```

### Every state

<Demo name="States" />

| State | Body | Around it | Size (share of `Size`) |
|---|---|---|---|
| `Idle` | Slow marble flow | A faint halo; the orb breathes every 5.03 s | 0.302 ± 0.0085 |
| `Listening` | Swells with the input level | Two ripples travel out from the rim; the halo brightens | 0.31, up to about 0.34 when loud |
| `Listening`, muted | Gray, a little darker | No ripples, a dim halo | 0.295 |
| `Thinking` | Draws in; the marble twists into a swirl | A bright band runs clockwise around the rim | 0.275 |
| `Speaking` | Lumpier outline, pulses with the output level | The strongest halo | 0.31, up to about 0.34 |

Every change glides on springs (about 0.8 s, no overshoot); changing the state again mid-way keeps the speed, so a quick
listening, thinking and speaking cycle never jumps.

### Your own levels

<Demo name="Levels" />

The orb never opens a microphone. While listening it reads `InputLevel` (0 to 1) every frame, and while speaking
`OutputLevel`. Leave them unset (not a number) and the orb uses `LevelSource`, or a built-in simulated voice: a
deterministic pulse per syllable with breaths between phrases. For speech output you can also call `PulseOutput` once
per spoken word, as the session does: each pulse lifts the orb within a frame and fades over about a second.

```csharp
// A microphone meter from your audio stack, 0..1:
meter.LevelChanged += level => Orb.InputLevel = level;
// Or implement IVoiceOrbLevelSource to be read once per frame:
Orb.LevelSource = new MyLevels();
```

### Captions and the call bar

<Demo name="CallBar" />

`VoiceTranscript` splits `Text` on white space. Appending words animates only the new ones; any other change replaces
the line without entrances. To start a new turn, change `Turn` together with `Speaker` and the new `Text` (usually
empty): the old line leaves upward and the new one fades in. Assistant lines use the foreground color, user lines the
secondary text color, and system lines (hints, "Call ended after 1:07.") are a size smaller and muted. Two lines stay in
view; older lines slide out under the soft top edge.

`VoiceControls` keeps `IsActive` and `IsMuted` itself when the user presses its buttons (bind them two-way) and raises
`StartRequested`, `MuteChanged` and `EndRequested`. It does not count time: push `Elapsed` from your call clock.

### Colors

<Demo name="Tints" />

The orb follows the theme's accent. `Tint` replaces it for the body, the halo and the ripples; the light veins of the
marble take `Tint2`, or a lighter, softer shade derived from the tint (warm tints lean toward orange). In a dark theme the
same accent gives a pale, silvery orb with a bright glow.

### Anatomy

1. **Body**: a fluid two-color marble with a sphere's shading, a fresnel rim and a soft highlight at the upper left. Its
   outline is a 96-point curve with small drifting lobes.
2. **Halo**: the tint fading out from the rim, gone by 0.47 of `Size`; the rest of the square is room for it and for the
   ripples.
3. **Ripples** (listening): two thin rings born 4 px outside the rim that ease out to 19 px while fading; louder input
   makes them come faster.
4. **Band** (thinking): a bright arc about 60° wide that runs clockwise around the rim at 160° per second and spills a
   little past the edge.
5. **Captions**: up to two centred lines, 16/22.4 (system lines 14/19.6), at most 480 wide.
6. **Call bar**: a 50 px pill with Start (mic glyph and label), or Mute (40 px, pressed when muted), the m:ss timer and
   End (40 px, danger tint).

### Reduced motion

<Demo name="ReducedMotion" />

### Content guidelines

- Show captions in the words people said, not paraphrased; keep the assistant's sentences short so they fit two lines.
- Use a system line for hints and outcomes: "Tap the orb to start", "Call ended after 1:07.".
- Pair the orb with a short status word ("Listening", "Thinking") when the screen is used from a distance.
- Say plainly what is recorded. If nothing is stored, say so next to the controls.

### Best practices

- Drive `State` from your backend's real events: start listening when the microphone opens, think when the request is
  sent, speak when audio plays.
- Push real levels when you have them; the simulated voice is for prototypes and demos.
- Use `Size` 200 to 208 on a full voice screen and about 144 on narrow screens or inline.
- Make the orb a tap target only by wrapping it in a button with a name that says what the tap does.
- Mute must stop your microphone, not only gray the orb.

### Common questions

**Does the orb record audio?** No. The package never opens a microphone or a speaker; levels are numbers your app
pushes.

**Can I use a brand color?** Yes: set `Tint`, and `Tint2` for a second marble color.

**Why do captions not read every word aloud?** Screen readers hear a turn once, when `IsComplete` is set, instead of a
stream of fragments.

**Does it cost frames when hidden?** The frame loop pauses while the orb is off screen, hidden or below full motion.

## API reference

<!-- api: VoiceOrb -->

<!-- api: VoiceTranscript -->

<!-- api: VoiceControls -->

<!-- api: VoiceOrbLabels -->

<!-- api: VoiceControlsLabels -->

<!-- api: VoiceControlsMuteChangedEventArgs -->

<!-- api: IVoiceOrbLevelSource -->

<!-- api: VoiceOrbSimulatedLevels -->

<!-- api: VoiceOrbSession -->

<!-- api: VoiceOrbScript -->

<!-- api: VoiceOrbScriptTurn -->

| Type | Values |
|---|---|
| `VoiceOrbState` | `Idle`, `Listening`, `Thinking`, `Speaking` |
| `VoiceTranscriptSpeaker` | `Assistant`, `User`, `System` |

`VoiceOrbSamples.WeekendTrip` is the invented conversation the demos play.

## Accessibility

- The orb is an image named after its state ("Voice assistant is idle", "Listening", "Thinking", "Speaking", "Muted");
  set `Label` or `Labels` for your own words. It takes no focus.
- The animated caption words are hidden from assistive technology. A polite live region reads the whole turn once it is
  final (`IsComplete`): the user's sentence when it is sent, the answer when it is finished, system lines at once.
- The mute button is a toggle (pressed while muted) named "Mute microphone" or "Unmute microphone". The timer is named
  in words ("1 minute 5 seconds").
- Starting a call from the bar moves the focus to Mute; ending it moves the focus back to Start.
- Keyboard focus shows a 2 px ring: outside the round buttons, inside the edge of Start (the pill would clip it
  otherwise). Pointer focus shows none; hover styles apply to a mouse or pen only.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Before a call: Start. During one: Mute, then End (the timer is skipped) |
| `Enter` / `Space` | Press the focused button; on Mute, toggle the microphone |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| State change | Ten shape springs (k 55, c 17: ω 7.4, ζ 1.15), about 0.8 s | One still frame per state | One still frame per state |
| Breath, flow, ripples, band | Continuous | Still | Still |
| Level and word pulses | Swell in 1 to 3 frames, fall over about 0.5 s | Not shown | Not shown |
| New caption word | Rises 3 px out of a 4 px blur, 340 ms | Fades in 120 ms | Appears |
| More lines | The block springs up one line (ω 13.5) | Instant | Instant |
| New turn | Old line leaves 10 px up with a blur in 180 ms; new one rises 8 px over 700 ms | 120 ms fades | Replaced |
| Call bar swap | Width spring (start ω 11, ζ 0.93; end ω 10.6, ζ 0.9); groups cross with scale 0.92 and a 4 px blur (120 ms out, 160 ms in) | 120 ms fades, instant width | Instant |
| Mute glyph | Pops between mic and mic-off | Fades | Replaced |
| Button fills | 160 ms | Instant | Instant |

## Responsive behavior

- The orb keeps its `Size`; choose a smaller size for narrow screens. The halo needs the whole square, so do not clip it.
- Captions take the width they are given up to 480 and wrap with balanced lines; on a 320 px screen they still show two
  lines.
- The call bar is as wide as its content (166 px idle, 144 px live) and centres itself.

## Related

| Component | Choose it when |
|---|---|
| `VoiceOrb` | A live voice conversation needs a face, captions and call controls |
| `DictationButton` | Speech goes into a text field |
| `SpeechPlayer` | A written answer is read aloud with a player |
| `AiMascot` | The assistant needs a character rather than an abstract orb |
