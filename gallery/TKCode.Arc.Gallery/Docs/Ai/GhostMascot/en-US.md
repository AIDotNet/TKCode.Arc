---
title: Ghost mascot
summary: A floating ghost mascot whose hem ripples while it hovers: it blinks when idle, listens, thinks under a swirl, writes with a tiny pen, sparkles when done and wobbles on an error.
---

<Demo name="Preview" />

## When to use

`GhostMascot` gives an assistant a light, friendly face that shows what it is doing. A soft sheet floats with a
rippling hem, a quiet form shade and two simple eyes, and every state changes the face and its marks, so it still reads
at 24px.

- **In a reply header or a composer**, next to the assistant's name, while it thinks and writes.
- **Over an image canvas** while a picture generates.
- **In an empty state**, where a 96 to 160px ghost gives the AI feature an identity.
- **Instead of a spinner**, when generation should feel alive.

## When not to use

- **Steps, tools and elapsed time** of a run: use `AgentStatus`, not just a mood.
- **Dense interfaces** where a character would distract: a plain shimmering "Thinking" label is enough.
- **Known progress** with a percentage: use a progress bar.

## Usage

Map the assistant's real phases to `Status`: microphone open is `Listening`, waiting for the first token is
`Thinking`, streaming is `Writing`, finished is `Done`, failed is `Error`, otherwise `Idle`. Keep thinking short in real
use and switch to writing on the first token.

```xml
<arc:GhostMascot Size="24" Status="{Binding Phase}" AssistantName="Atlas" Announce="True" />
```

```csharp
ghost.Status = GhostMascotStatus.Thinking; // request sent
ghost.Status = GhostMascotStatus.Writing;  // first token
ghost.Status = GhostMascotStatus.Done;     // last token
```

Leave `Status` unset and the ghost plays through every state on a loop. That is meant for showcases;
`EffectiveStatus` reports the state it shows and `StatusChanged` is raised at each step, so a caption or a control can
follow it.

### Anatomy

1. **Sheet**: a domed head, slightly flared sides and a hem of two traveling waves with rounded corners.
2. **Form shade**: a darker, translucent tone of the sheet on its lower right, clipped to the sheet.
3. **Eyes**: two ovals that glance, blink, widen, look up, follow the pen, turn into happy arcs or droop.
4. **Marks**: the thinking swirl, the writing pen and its line, the done sparkle (and a small smile).
5. **Ground**: a soft floating shadow, from 56px up.

### States

<Demo name="States" />

| Status | Pose and face | Marks |
|---|---|---|
| `Idle` | Floats, blinks every few seconds and glances around | None |
| `Listening` | Comes forward, wide eyes looking at you, a small head tilt | None |
| `Thinking` | Drifts up and turns translucent, eyes drift up | A swirl that draws in from its outer end and erases behind itself |
| `Writing` | Leans toward the line, eyes on the pen tip | A tiny pen writes a line word by word, then glides back as it erases |
| `Done` | Lifts, happy eyes and a small smile | A sparkle grows with a quarter turn, then twinkles |
| `Error` | Wobbles once, sinks, droopy eyes | None |

### In a reply header

<Demo name="ReplyHeader" />

Use 24 to 32px inline in a reply header or a composer. Below 56px the compact drawing takes over: the ghost is framed
tighter, the eyes are bigger and the swirl, pen and line are heavier, and there is no shadow, so every state still reads.

### Sizes

<Demo name="Sizes" />

`Size` sets the width and height (default 96): 24 to 32 inline, 40 to 64 in a panel or on an image canvas, 96 to 160 in
an empty state. The ghost is a fixed square, so it never reflows its parent, and it is vector art that stays crisp at
any scale.

### Color

<Demo name="Tones" />

`Tone` picks the sheet color from the theme: `Accent` (default) with accent-foreground eyes and follows the theme
accent, including Arc's near-neutral grey; `Sheet` is the classic pale ghost, a raised surface with a hairline and
foreground eyes whose marks keep the accent; `Ink` uses the foreground with background-colored eyes. `SheetBrush` sets
any color and the eyes become a dark mix of it unless `EyeBrush` is set. `MarkBrush`, `ShadeBrush` and `GroundBrush`
restyle the marks, the form shade and the ground shadow.

### Empty state

<Demo name="EmptyState" />

A large idle ghost in the `Sheet` tone gives an empty screen a calm, friendly face.

### On an image canvas

<Demo name="Canvas" />

A 64px ghost in the corner of a canvas shows that a picture is generating. Turn `ShowGround` off where the ghost does
not stand on a surface. `IsPaused` freezes it on the current frame; resuming continues from the same frame.

### Content guidelines

- Set `AssistantName` to the assistant's name so the label reads well ("Atlas, thinking").
- Show a text status next to the ghost wherever the state matters; the ghost adds character, the words carry meaning.

### Best practices

- Turn `Announce` on for the one ghost that carries status, not for decorative copies.
- Pair the ghost with real controls such as Stop when people act on what it shows; the ghost takes no input.
- Leave `Status` unset only for showcases.

### Common questions

**Does a ghost off screen cost anything?** No. Its frame loop pauses while it is off screen or hidden, and its clock only
runs while it is visible, so it resumes on the same frame instead of jumping.

**Can I restyle it?** Yes: `SheetBrush`, `EyeBrush`, `MarkBrush`, `ShadeBrush` and `GroundBrush` take any solid color.

## API reference

<!-- api: GhostMascot -->

<!-- api: GhostMascotLabels -->

<!-- api: GhostMascotStatusChangedEventArgs -->

## Accessibility

- The ghost presents as an image with a label built from `AssistantName` and the status, for example "Atlas,
  thinking", which updates with every state. `AccessibleName` replaces the whole label; every word is in
  `GhostMascotLabels`.
- With `Announce` on, the ghost is also a polite live region, so status changes are read once. Use it on one ghost per
  view so screen readers do not hear duplicates.
- Status is never carried by color or motion alone: each state changes the face and its marks (wide eyes, eyes up with
  a swirl, a pen and a line, happy eyes with a sparkle, droopy eyes) and the label.

### Keyboard

| Keys | Action |
|---|---|
| None | The ghost is a status image and takes no focus. Pair it with a real control when people act on what it shows. |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Float and hem | A slow float; two traveling waves ripple the hem, which trails the float: the sheet stretches on the way up and the hem lifts and flares on the way down | Still | Still |
| Idle | Blinks every 2.4 to 5.2s (160ms) and glances around with quick saccades | Still, eyes centered | Still |
| Thinking | The swirl draws in from its outer end and erases behind itself, empty when its cycle wraps; the eyes drift up slowly | A half drawn swirl | A half drawn swirl |
| Writing | The pen writes the line word by word, lifting between words, then glides back while the line erases; the eyes track the tip | Half a written line | Half a written line |
| Done | The sparkle grows in 320ms with an ease out and a quarter turn, then twinkles slowly | The sparkle | The sparkle |
| Error | One wobble that decays to rest in about 1.2s, softened at the start so it never kicks | The droop | The droop |
| Status change | Every pose value blends from the frame on screen over 400ms with an ease in and out (keeping its velocity); the eyes ride a critically damped spring | Switches in place | Switches in place |

Every loop is a function of a phase that only grows: the float, the two hem waves, the swirl and the writing cycle. Rates
blend by integrating into those phases, so speed changes never jump and nothing restarts. One frame loop per ghost moves
the sheet's path points in place with cached brushes and pens; it pauses off screen and when hidden. Under reduced motion
the ghost repaints only when the status changes.

## Related

- `AgentStatus` for the steps, tools and elapsed time of an agent run.
- `ToolCall` for the tool calls an agent makes.
- `AgentPlan` for the plan an agent writes before it acts.
