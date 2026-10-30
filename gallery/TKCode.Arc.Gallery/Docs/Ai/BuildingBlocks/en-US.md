---
title: Building blocks
summary: A loading visual for AI app builders: rounded blocks snap into a tiny app layout with no bounce, fill with content and pick up the accent while the label steps from Laying out to Ready.
---

<Demo name="Preview" />

## When to use

`BuildingBlocks` shows an app being assembled while an AI generates it. Rounded blocks for a nav bar, a hero card, a
chart, an avatar row, a settings row and a button fly in and snap into a tiny app layout with no bounce. They land as
tinted placeholders, fill with real-looking content, then pick up the tint in a styling pass, while the status line steps
through Laying out, Adding components, Styling and Ready.

- **Generative UI products** that need a loading state showing an app, a page or a dashboard being built.
- **An assistant reply that will return a UI**, while the code or the layout streams in.
- **An empty state or an onboarding step** that explains an AI builder at a glance.

## When not to use

- **Plain text answers**: use a shimmering label or a thinking indicator instead.
- **Image generation**: reveal the image itself.
- **Real progress with known steps and logs**: use `AgentStatus` or `AgentPlan`, so people see what actually happens.
- **Placeholders that mirror the real layout of a page**: use `Skeleton`.

## Usage

Map your request to `Status`: `Building` while it runs, `Done` when the result arrives, `Idle` before the first prompt. A
`Done` that arrives mid-build fast forwards what is left and settles on Ready. Leave `Status` unset to play the built-in
simulated build, which loops with a short hold on Ready.

```csharp
blocks.Status = BuildingBlocksStatus.Building;
var result = await GenerateAsync(prompt);
blocks.Status = BuildingBlocksStatus.Done;
```

### Driven by the app

<Demo name="HostDriven" />

`Duration` is one build, from the first block to the end of styling, split evenly over the three phases (4.8 seconds by
default, never under 2.4). Set it near your typical generation time. While `Status` is `Building` and `IsLooping` is on,
a long generation keeps re-assembling instead of freezing; with `IsLooping` off it holds on Styling.

`AllowsReplay` shows Replay on Ready. It raises `ReplayRequested`: start the generation again and set `Status` back to
`Building`. `PhaseChanged` reports every phase the status line shows, for analytics or to keep other UI in step, and
`Phase` holds the current one.

### Anatomy

1. **Frame**: the mini app on a muted canvas, with a dashed slot for every block.
2. **Blocks**: a nav bar, a hero card with a product photo, a chart, an avatar row, a settings row with a switch and a
   button.
3. **Status line**: a mark (four small blocks while building, a check on Ready), the phase label and Replay. Its height is
   fixed, so a new label or Replay never moves anything.

### Sizes

<Demo name="Sizes" />

`Size` sets the width the frame grows to: `Small` 280px, `Medium` 400px (the default) and `Large` 560px. `StageWidth`
takes any width in pixels instead. The frame never grows past its container, and the layout, the radii and the strokes
scale with the width, so it stays in proportion from 240px to 880px. Below 320px the status line uses 12px type.

### Tone and tint

<Demo name="Tones" />

`Tone` picks the tint for the placeholders, the button, the chart, the switch and the status mark: `Accent` (the default)
or `Neutral`, which uses the foreground color. `Tint` takes any color instead, for a brand or a per-product tint.

### Play once

<Demo name="PlayOnce" />

Without a status, `IsLooping="False"` plays the simulated build once, rests on Ready and offers Replay, which builds it
again.

### Without the status line

<Demo name="NoLabel" />

`IsLabelVisible="False"` hides the status line when the surrounding UI already says what is happening.

### Your own words

`Labels` holds every word. Name the thing being built, or translate:

```csharp
blocks.Labels = new BuildingBlocksLabels
{
    Ready = "Your dashboard is ready",
    Replay = "Build again",
};
```

### Reduced motion

<Demo name="ReducedMotion" />

With reduced motion the placeholders, the content and the styling arrive as plain crossfades, nothing travels and the
build plays once. With motion off every step changes at once. The phases still advance: they are information, not
decoration.

### Content guidelines

- Name the result in the Ready label ("Your CRM is ready") rather than repeating "Ready".
- Keep the simulated build for demos and empty states; in a real product, drive `Status` from the request.
- Do not pretend to report progress: the phases are a pace, not a measurement.

### Best practices

- Set `Duration` close to your usual generation time so Ready lines up with the result.
- Use one instance per pending result; on a page that shows many, prefer `Small`.
- Put it where the result will appear (a reply bubble, a canvas, an empty state) so the layout hands over to it.

### Common questions

**What if the result arrives early?** Set `Status` to `Done`: every block lands, fills and takes the tint at once, with
short transitions, and the label says Ready.

**Does it run when it is off screen?** No. The schedule and the status mark pause while the frame is scrolled away, hidden
or in a hidden window, and resume where they were.

## API reference

<!-- api: BuildingBlocks -->

<!-- api: BuildingBlocksLabels -->

<!-- api: BuildingBlocksPhaseChangedEventArgs -->

## Accessibility

- The frame is decorative and hidden from assistive technology; the phase is announced from a polite live region, so
  screen readers hear Laying out, Adding components, Styling and Ready.
- The simulated build says so ("Simulated build, nothing was generated" as help text), and while it loops its label is not
  announced on every loop.
- Ready pairs a check with the label, so the state never relies on color alone.
- Replay is an Arc `Button` with a visible label.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Reaches Replay when it shows on Ready; the frame takes no focus |
| Enter, Space | Presses Replay |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Blocks arrive | Fly in from just outside their slots and a little smaller on a 0.52s critically damped spring (no overshoot), 40ms apart, all within 200ms of the first | Fade in over 240ms in place | At once |
| Placeholders fill | Cross to neutral content over 320ms, 60ms apart; bars grow from their left edge and the chart line draws in over 600ms | 240ms crossfade | At once |
| Styling pass | Tint, photos and portraits fade in over 400ms, 50ms apart; the switch slides once | 240ms crossfade; the switch changes sides without travel | At once |
| Phase label | Old label fades out in 100ms, the new one fades in over 160ms with a 2px rise, in a fixed line | 120ms crossfade | At once |
| Replay | Fades in on Ready | Fades in | At once |
| Status mark | A light runs round four blocks, 300ms each | One block fills per phase | One block fills per phase |
| Loop | The finished layout fades out in place over 320ms, resets out of sight and builds again over the same dashed slots | Plays once | Plays once |

Every change starts from what is on screen, so a status change mid-build never cuts. The schedule and the mark pause
while the frame is off screen or hidden.

## Related

- `AgentStatus` for the live status of an agent run with real steps.
- `AgentPlan` for a plan an agent follows.
- `Skeleton` for placeholders that mirror a page's real layout.
