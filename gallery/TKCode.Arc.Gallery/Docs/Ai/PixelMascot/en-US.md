---
title: Pixel mascot
summary: A pixel-art robot that shows what an AI assistant is doing. It idles, thinks, types, celebrates and errors in frame-stepped sprite animations, and stays crisp at every integer size from 24px to 192px.
---

<Demo name="Preview" />

## When to use

`PixelMascot` is a small pixel-art robot on a 24 × 24 grid that carries the mood of an assistant at work. Each status
has its own sprite animation on a fixed 40ms clock, and a status change morphs from the current pose one pixel at a
time.

- **A friendly generating indicator**: next to an assistant reply or in a chat composer, at scale 2 or 3.
- **Empty states and onboarding**: a small character makes an AI feature feel approachable, at scale 4 to 8.
- **Image or canvas generation**: a mascot in the corner shows thinking, drawing and done.
- **Products with a playful or retro voice** that still want precise, calm motion.

## When not to use

- **Detailed agent progress** with steps and timings: use `AgentStatus` or `AgentTimeline`.
- **Dense tables or toolbars**, where a 24px character is noise: use a status dot or `ArcSpinner`.
- **Serious or regulated contexts** (finance, health, legal), where a character feels flippant: use `TextShimmer`.
- **Showing the model's reasoning**: use `ReasoningBlock`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <StackPanel Spacing="16">
        <!-- Inline next to a streaming reply -->
        <arc:PixelMascot Status="Thinking" Scale="2" AssistantName="Atlas" Announce="False" />
        <!-- Empty state or composer, self playing -->
        <arc:PixelMascot Scale="6" />
    </StackPanel>
</UserControl>
```

`PixelMascot` is a `Control` in the Special package (register it with `UseArcSpecial()`). It is a fixed square of 24
times `Scale` device-independent pixels and never reflows; pick a smaller scale on narrow layouts. It sits on a text
line (left aligned, vertically centered), so at scale 1 and 2 it lines up with the words beside it.

### Statuses

Set `Status` to one of five values. Each has its own shape as well as its own motion, so the state never depends on
color alone:

1. **Idle**: the robot bobs on a 2s cycle, blinks and glances to the side on an 8s cycle.
2. **Thinking**: a thought bubble steps in at the top right and cycles three dots every 1.12s while the eyes look up at
   it and one hand rises to the chin.
3. **Writing**: a keyboard steps up from below, the hands take turns on the keys and a bar on the chest fills one
   stroke at a time, eight strokes every 1.6s, while the eyes scan along it and return.
4. **Done**: happy eyes, a jump with both arms up that holds at the top for 120ms and settles without a rebound, and
   two sparkles that twinkle above the head.
5. **Error**: crossed eyes in the danger color on a tinted screen, a red antenna light and one shake.

Map your own lifecycle to them: a request sent is `Thinking`, the first token is `Writing`, finished is `Done`, a
failure is `Error`, and settle back to `Idle` a couple of seconds after done.

<Demo name="Lifecycle" />

### The built-in loop

Leave `Status` null and the mascot plays its own loop through every state: idle, thinking, writing, done, idle,
thinking, error. `ShownStatus` reads the status it is showing and `ShownStatusChanged` reports each change with the old
and the new status. To keep several mascots in step, put one `PixelMascotLoop` in resources and bind their `Status` to
its `Status`; `IsEnabled="False"` holds the current step, and `Advance()` and `Reset()` move it by hand.

<Demo name="InStep" />

### Sizes

`Scale` is an integer from 1 to 8: every sprite pixel is that many device-independent pixels, so 1 renders 24px, 2
renders 48px, 4 renders 96px (the default) and 8 renders 192px. Values outside the range clamp. Each pixel is drawn as a
rectangle snapped to whole device pixels at the window's render scaling, without antialiasing, so the art stays crisp
at every scale and on every display.

<Demo name="Sizes" />

### Next to text

At scale 1 and 2 the mascot fits on a line of text. Pair it with a `TextShimmer` label when people need to know exactly
what is happening: the mascot carries the mood, the text carries the detail. When the label already says what is
going on, set `Announce="False"` so screen readers do not hear it twice.

<Demo name="Inline" />

### Accent

The palette follows the theme neutrals (the outline, the shell, the dark screen and the floor shadow) and
`AccentBrush`, which is the theme's accent unless you set it. The accent lights the antenna, the chest light, the typed
bar and the sparkles; the eyes glow in a lighter shade of it on the dark screen. It looks right with Arc's near-neutral
default accent too.

<Demo name="Accent" />

### Reduced motion

Under reduced motion the mascot holds a clear resting pose for each status: open eyes, the bubble with three dots, the
keyboard, happy eyes or crossed eyes. The built-in loop still moves from status to status.

<Demo name="ReducedMotion" />

## API reference

<!-- api: PixelMascot -->

<!-- api: PixelMascotLoop -->

<!-- api: PixelMascotLabels -->

<!-- api: PixelMascotStatusChangedEventArgs -->

| Type | Values |
|---|---|
| `PixelMascotStatus` | `Idle`, `Thinking`, `Writing`, `Done`, `Error` |

## Accessibility

- The sprite is one image whose name follows the status: "Assistant is thinking", with `AssistantName` filled in.
  `Label` replaces the generated name, and `Labels` rewords or translates every status.
- Status changes are announced through a polite live region when `Status` is set by the app. The built-in loop stays
  quiet. Set `Announce` to false when the surrounding UI already reports progress, to avoid double announcements.
- Status is never color alone: each state has its own shape (the thought bubble, the keyboard, happy eyes, crossed
  eyes) and the name carries the words.
- The error state uses the danger token for the crossed eyes on a tinted screen, so it reads in light and dark themes
  and with any accent.

### Keyboard

The mascot is decorative status, not a control: it takes no focus and has no keyboard interaction.

| Keys | Action |
|---|---|
| `Tab` | Moves past the mascot; it is never focused |

## Motion

- Stepped on purpose: the sprite advances on a fixed 40ms clock and moves in whole pixels. Timings are authored as
  curves in steps: the done jump rises two pixels in one step, holds at the top for 120ms and settles a pixel at a time
  without a rebound.
- Status changes never cut: lids close before the eye shape changes and open after, eyes slide one pixel per step, arms
  pass through the neighbouring poses, the thought bubble grows from a dot to a small bubble to the full one, and the
  keyboard steps in and out a row at a time from wherever it is. A change in the middle reverses from the current
  frame; the chest bar backspaces one stroke per step.
- Loops are seamless by construction: idle bobs on a 2s cycle with blinks and a glance on an 8s cycle, thinking cycles
  three dots every 1.12s, writing types an eight-stroke bar every 1.6s while the eyes scan and return.
- One control draws the grid with merged rectangles per row; a step that changes no pixel skips the redraw, so it
  repaints about 25 times a second at most and never measures or arranges again.
- The frame loop pauses off screen and while `IsPaused` is set (the current frame stays).

| | Full | Reduced | Off |
|---|---|---|---|
| Sprite | Frame-stepped animation | Resting pose of each status | Resting pose of each status |
| Status change | Morphs one pixel per step | Switches pose | Switches pose |
| Built-in loop | Cycles | Cycles, one resting pose per step | Holds the current status |

## Related

| Component | Choose it when |
|---|---|
| `AgentStatus` | You need the agent's current step and elapsed time, not a mood |
| `TextShimmer` | A calm label is enough, or alongside the mascot for the detail |
| `ReasoningBlock` | You show what the model is thinking, not just that it thinks |
| `ArcSpinner` | Space is tight and a character would be noise |
