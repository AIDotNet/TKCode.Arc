---
title: AI spark
summary: An animated spark mark for AI status: four tapered rays and four lighter diagonals breathe at rest, ripple while thinking, pulse like a heartbeat while writing, then gather and settle when done.
---

<Demo name="Preview" />

## When to use

`AiSpark` is a brand-quality mark that shows an AI at work: four tapered primary rays and four lighter diagonal rays
around a soft core. Each status has its own motion, and every change blends from the pose the mark is already in, so
states never hard cut.

- **Above a streaming reply**, to show that the assistant is thinking or writing.
- **Inside a send or generate button** that turns into a working indicator in a composer.
- **Over an image or canvas tool** that needs a calm, branded working mark while a render runs.
- **Empty states and onboarding**, where a 64 to 96px hero mark gives the AI feature an identity.

## When not to use

- **Determinate progress** with a known percentage: use a progress bar.
- **Multi step agent runs** where each step needs its own state and detail: use `AgentStatus` or `AgentPlan`.
- **Plain text loading lines** with no mark: use a shimmering label instead.

## Usage

Map your stream's lifecycle to `Status`: request sent or tool running is `Thinking`, first token until the last is
`Generating`, finished is `Done`, failed or aborted is `Error`. Return to `Idle` when the composer is ready again.

```xml
<arc:AiSpark Variant="Pill" Status="{Binding Phase}" />
```

```csharp
spark.Status = AiSparkStatus.Thinking;   // request sent
spark.Status = AiSparkStatus.Generating; // first token
spark.Status = AiSparkStatus.Done;       // last token
```

Leave `Status` unset and the spark plays through idle, thinking, writing and done on its own. That is meant for demos
and marketing pages; `EffectiveStatus` reports the step it is showing.

### Anatomy

1. **Primary rays**: four tapered rays on the axes, in the mark's color.
2. **Diagonals**: four shorter, lighter rays between them; under 18px they become dots.
3. **Core**: a soft circle in the lighter tone.
4. **Particles**: two dots orbiting just outside the rays while writing, from 40px up.
5. **Label** (pill only) and **tile** (avatar only).

### States

| Status | Motion | Still pose (reduced motion) |
|---|---|---|
| `Idle` | A 6.4s breath of the whole mark while one diagonal at a time reaches out | Rest |
| `Thinking` | One crest ripples around the ring every 1.9s; the mark turns at 26 degrees per second | A crest at the top |
| `Generating` | A lub dub heartbeat every 1.04s, axes firing in sequence; 46 degrees per second | Fuller rays and core |
| `Done` | The rays gather toward the center, then settle at rest; the spin glides home | Rest |
| `Error` | One small decaying shake; shorter rays, dimmer diagonals, a muted danger tone | Short rays, danger tone |

### Pill

<Demo name="Pill" />

`Variant="Pill"` adds a status label (Ready, Thinking, Writing, Done, Couldn't finish) in a quiet bordered capsule 28px
high. Labels crossfade and the capsule eases to the new width; while thinking or writing the label shimmers. `Label`
replaces the built-in word with specific progress such as "Searching 4 sources"; keep it to one or two words.

### Avatar

<Demo name="Avatar" />

`Variant="Avatar"` sets the mark in a round, lightly tinted tile for message avatars and icon buttons. `Size` sets the
tile (32 by default) and the mark is 56% of it.

### Inline in text

<Demo name="Inline" />

Set `Size` to the text size and `Tone="Inherit"` so the mark takes the color of the text or button around it.

### Sizes

<Demo name="Sizes" />

The mark is crisp from 12px to 120px and beyond. Ray weight is hinted on a log scale by the rendered size: small marks
get heavier rays, and under 18px the diagonals become dots. The mark has a fixed footprint in every status, so it never
moves the layout around it.

### Color

<Demo name="Tones" />

`Tone` picks the semantic color: `Accent` (default), `Foreground`, `Muted` or `Inherit`. `MarkBrush` overrides it with
any color. The diagonals and the core always use a lighter tone of the same color, and an error eases to a muted danger
tone whatever the color.

### Hero mark

<Demo name="Hero" />

A 64 to 96px idle mark gives an empty state an identity. `Twinkle="False"` makes the idle mark fully still.

### Speed and pause

<Demo name="Playback" />

`Speed` multiplies every loop and spin speed. `IsPaused` freezes the mark in place; resuming continues from the same
phase.

### Content guidelines

- Pill labels are one or two words that say what is happening ("Searching 4 sources", "Drawing").
- Use one animated spark per active generation. For many rows, show a still mark (`Status="Idle"`, `Twinkle="False"`)
  and animate only the active row.

### Best practices

- Put controls such as Stop next to the spark; the spark itself takes no input.
- Set `Announce="False"` on decorative copies so screen readers hear one voice.

### Common questions

**Can I pass a CSS length such as 1em?** Set `Size` to the font size of the surrounding text instead.

**Does a spark off screen cost anything?** No. The loop runs only while the mark moves and is on screen, and stops
entirely once a resting status has settled.

## API reference

<!-- api: AiSpark -->

<!-- api: AiSparkLabels -->

## Accessibility

- With `Announce` on and a status set, the spark is a polite live region: a status change is announced once, as "AI is
  thinking" for the mark or as the label text for the pill.
- With `Announce="False"`, and while it plays on its own, it presents as an image with an accessible name, so
  decorative copies stay quiet.
- `AccessibleName` replaces the name; every word is in `AiSparkLabels`.
- Status is never color alone: each status has its own shape and motion, an error shortens the rays and dims the
  diagonals, and the pill adds a text label.

### Keyboard

| Keys | Action |
|---|---|
| None | The spark is a status indicator and takes no focus. Put controls such as Stop next to it. |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Idle | 6.4s breath, one diagonal reaching out at a time | Still | Still |
| Thinking | Crest around the ring every 1.9s, 26 degrees per second spin | Still pose | Still pose |
| Generating | Heartbeat every 1.04s, 46 degrees per second spin, two orbiting particles (40px and up) | Still pose | Still pose |
| Done | Rays gather in 170ms, ease back to rest over 510ms without overshoot; the spin glides home on an ease-out whose speed only falls | Still pose | Still pose |
| Error | One decaying shake over 460ms; the tone eases to muted danger over 320ms | Still pose, tone over 200ms | At once |
| Status change | Blends over about 480ms from the current pose and velocity; an interrupted blend is the next one's start | 200ms blend | At once |
| Pill label | Old label fades out over 100ms, the new one fades in over 160ms and rises 2px while the width eases | Short crossfade | At once |

One frame loop per spark moves eight ray paths, the core and the transforms in place and allocates nothing. It pauses
while the spark is off screen or hidden and stops entirely once a resting status has settled. The gather and the shake
always finish smoothly, even if the status changes again.

## Related

- `AgentStatus` for the live status of a whole agent run.
- `ToolCall` for the tool calls an agent makes.
- `AgentPlan` for the plan an agent writes before it acts.
