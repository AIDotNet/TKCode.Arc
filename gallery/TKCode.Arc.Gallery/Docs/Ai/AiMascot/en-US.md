---
title: AI mascot
summary: A small assistant character that shows what the AI is doing: it breathes when idle, leans in to listen, watches its dot orbit while thinking, reads along as it writes, squints when done and shakes once on an error.
---

<Demo name="Preview" />

## When to use

`AiMascot` gives an assistant a face that shows what it is doing. It is a soft pebble with two pill eyes, a quiet
shade on its lower right and a small companion dot. Each of its six states is a continuous motion with no loop seam,
and each one also changes the face, so the state reads at 24px.

- **Reply headers and composers**: a 24 to 32px mascot next to the assistant's name thinks while you wait for the first
  token and reads along while the reply streams.
- **Side panels**: a 40 to 64px mascot in the header of an assistant panel.
- **Empty states**: a 96 to 160px mascot that breathes, blinks and watches the pointer anywhere in the window.
- **Image canvases and long jobs**: something friendly and alive while the model works, instead of a spinner.

## When not to use

- **People need the steps, tools and elapsed time of a run**: use `AgentStatus`. The mascot shows a mood, not progress.
- **Dense interfaces**: a plain shimmering "Thinking" line distracts less than a character.
- **Known progress**: use a progress bar when you know the percentage.

## Usage

```xml
<arc:AiMascot Status="Thinking" Size="24" AssistantName="Atlas" FollowPointer="Never" />
```

`AiMascot` is a `TemplatedControl` in the Special package (register it with `UseArcSpecial()`). Map your assistant's
phases to `Status`: the microphone is open → `Listening`, waiting for the first token → `Thinking`, streaming →
`Writing`, finished → `Done`, failed → `Error`, otherwise `Idle`. Keep thinking short and switch to writing on the
first token, so the mascot reads what it writes.

Leave `Status` unset and the mascot plays through every state on a loop (idle, listening, thinking, writing, done,
error), raising `StatusChanged` each time it moves on. That is for showcases; in a product, always set `Status`.

### Anatomy

1. **Body**: a soft pebble, anchored at its bottom center. It breathes, floats, leans and tilts around that point.
2. **Form shade**: a quiet crescent along the lower right that gives the body volume.
3. **Eyes**: two pill shapes that blink, glance, widen, look up or down, close into a line and turn into a happy squint.
4. **Brows**: only in the error state, with their inner ends raised.
5. **Companion dot**: floats beside the head, pulses like a voice level, orbits the head, writes, droops, and turns into
   the done sparkle.
6. **Ground**: a soft contact shadow and the line the dot writes, drawn only at 56px and up.

### States

| Status | Face | Body | Dot |
|---|---|---|---|
| `Idle` | Blinks every 2.4 to 5.2s and glances around | Breathes and floats | Drifts beside the head |
| `Listening` | Wider eyes | Leans in | Pulses like a voice level |
| `Thinking` | Looks up and follows the dot | Tilts the head | Orbits the head, passing behind and in front |
| `Writing` | Looks down and reads along | Leans toward the pen | Writes a line, then lifts and returns |
| `Done` | Happy squint | Lifts a little | Shrinks while a sparkle grows in its place |
| `Error` | Worried brows, smaller eyes | Shakes once | Droops and dims |

<Demo name="States" />

### In a reply header

At 24px next to the assistant's name. The demo simulates the phases: thinking, writing while the words stream, then
done. `Announce` is on because this mascot carries the status.

<Demo name="ReplyHeader" />

### Sizes

`Size` is the width and height in px, from 24 (inline) to 160 (empty state); the default is 96. Below 56 a compact
drawing takes over: tighter framing, larger eyes, dot and sparkle, and no ground. The mascot is a fixed square that
never follows its container, and the drawing stays crisp at any scale.

<Demo name="Sizes" />

### Tones and colors

`Tone` picks the body color from the theme: `Accent` (the accent with accent-foreground eyes), `Ink` (the foreground
with background eyes) or `Soft` (a muted surface with a hairline border and foreground eyes). `BodyColor` overrides
the tone; the eyes then become a dark mix of it unless `EyeColor` is set. `DotColor` changes the companion dot and the
sparkle.

<Demo name="Tones" />

### Following the pointer

The eyes follow the pointer while it is over the mascot (`FollowPointer="Hover"`, the default), anywhere in the window
(`Page`, good for empty states) or never (`Never`). Touch is ignored. Thinking and writing keep their own gaze.

<Demo name="EmptyState" />

### Companion and ground

`ShowCompanion="False"` hides the dot and the sparkle. `ShowGround="False"` hides the contact shadow and the written
line; below 56px they are never drawn.

### Pausing

`IsPaused` freezes the mascot on the frame on screen. It also pauses by itself when it is scrolled out of view,
hidden or detached, and resumes on the same frame instead of jumping.

### Reduced motion

<Demo name="ReducedMotion" />

Under reduced motion each state is a still, representative pose (eyes up with the dot over the head, half a written
line, the squint and the sparkle, the brows), changes switch in place, and there are no loops, glances, blinks or
shakes. Pointer following is off. The tour of a mascot without `Status` still moves from state to state.

### Common questions

**Why does the mascot not move in a screenshot test?** Tests and screenshots usually run with motion off; every state
then shows its still pose.

**Can I use several mascots on one page?** Yes. Each runs its own clock, pauses off screen and has its own blink and
glance rhythm. Turn `Announce` on for one of them at most.

## API reference

<!-- api: AiMascot -->

<!-- api: AiMascotLabels -->

<!-- api: AiMascotStatusChangedEventArgs -->

The enums:

| Type | Values |
|---|---|
| `AiMascotStatus` | `Idle`, `Listening`, `Thinking`, `Writing`, `Done`, `Error` |
| `AiMascotTone` | `Accent`, `Ink`, `Soft` |
| `AiMascotPointerFollow` | `Hover`, `Page`, `Never` |

## Accessibility

- The drawing is an image whose name is built from the assistant's name and the state, for example "Atlas, thinking".
  It updates with every state. `Label` replaces the whole name; `AccessibleLabel` reads the name in use.
- With `Announce`, state changes are read through a polite live region outside the image. Use it on one mascot per
  view so screen readers do not hear duplicates.
- The state never rests on motion alone: each state changes the face (wide eyes, eyes up, eyes down, the happy squint,
  worried brows) and the name. Show a text status next to it where the state matters.
- Pointer following ignores touch and is off under reduced motion.

### Keyboard

| Keys | Action |
|---|---|
| None | The mascot is a status image and takes no focus. Pair it with a real control when people act on what it shows. |

## Motion

- Every state is a continuous function of time (sums of slow sines, an orbit angle, a writing cycle whose start and
  end meet at zero length), so loops never restart and have no seam.
- A status change blends the old pose into the new one over about 420ms, without a bounce. Each state's weight rides a
  critically damped spring, so a change in the middle of a blend starts from the frame on screen and keeps its speed.
  The first paint is the still pose, and the loop eases out of it over the same 420ms.
- The eyes ride their own critically damped spring: quick saccades when idle, a soft follow while thinking, tight
  tracking while writing. Nothing bounces.
- Done passes the eyes through a closed line into the happy squint while the dot shrinks and the sparkle grows in its
  place. Error shakes once, dying down in about 0.7s.
- The dot is drawn behind and in front of the body and switches only at the ends of its orbit, where it is clear of
  the body. While it crosses the crown in front, it fades into the eye color so it stays visible on the body.
- Blinks come every 2.4 to 5.2s, sometimes twice, and take 160ms.
- The clock runs only while the mascot is shown; off screen it stops and resumes on the same frame. Under reduced
  motion each state shows a still pose and nothing loops.

## Related

- `AgentStatus` for the steps, tools and elapsed time of a running agent.
- `ToolCall` for one tool call in an agent transcript.
- `ContextWindow` for what fills a model's context window.
