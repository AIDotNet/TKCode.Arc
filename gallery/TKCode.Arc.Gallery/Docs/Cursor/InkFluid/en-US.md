---
title: Ink fluid
summary: A fluid simulation behind your content: the pointer stirs soft ink that swirls and fades, and a slow stir keeps it moving when nobody does.
---

<Demo name="Preview" />

## When to use

`InkFluid` runs a small fluid simulation behind its content. Moving the pointer stirs in soft ink tinted with the
accent color; the ink curls, spreads, turns neutral as it ages and fades after a couple of seconds. When nobody is
moving, a slow stir keeps a little ink drifting so the area never looks frozen.

- **Landing heroes and sign-in screens**: a calm, living background without a video.
- **Empty states and waiting screens**: something gentle to play with while there is nothing else to show.

## When not to use

- **Dense product surfaces** such as tables, forms and dashboards: movement behind data distracts.
- **More than one per page**: each instance runs its own simulation.
- **Where text must keep full contrast at all times**: the ink is faint, but it does pass under the content.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:InkFluid MinHeight="440" Background="{arc:ArcDesignTokenResource Surface}">
        <TextBlock Text="Calm until you move" HorizontalAlignment="Center" VerticalAlignment="Center" />
    </arc:InkFluid>
</UserControl>
```

`InkFluid` is a `ContentControl` in the Special package. Register the package with `UseArcSpecial()` next to
`UseArc()`. The ink is drawn on transparent pixels over the control's own background, so give it a `Background`, and a
`CornerRadius` and border if it should look like a card; the ink is clipped to that rounded box. The control is at
least 240px high.

The ink takes its colors from the theme: neutral ink uses the foreground token and tinted ink the accent token, so it
follows light and dark themes and the accent switcher. Set `InkBrush` or `AccentInkBrush` to override either.

### Tuning the ink

`Intensity` (0.5) sets how dark the ink gets, `Tint` (0.8) how much of the fresh ink takes the accent color, and
`Force` (0.5) how hard the pointer stirs; all three run from 0 to 1. `IsAmbient` turns the slow stir on or off; it
starts 1.2 seconds after the last pointer move. `IsInteractive` lets the pointer stir the ink at all.

<Demo name="Tuning" />

```xml
<arc:InkFluid Intensity="0.8" Tint="1" Force="0.7" IsAmbient="False" />
```

### Quality

`Quality` picks the size of the simulation. The velocity grid has 96, 128 or 192 cells on the short side for `Low`,
`Medium` (the default) and `High`, and the ink is drawn at twice that resolution and scaled up smoothly. Larger grids
show finer curls and cost more; `Low` suits small areas and slower devices.

### A waiting screen

With no tint, low intensity and the low grid, the ink becomes a quiet backdrop for an empty state.

<Demo name="EmptyState" />

### Best practices

**Do**

- Keep the content in the middle and short; the ink reads best around it.
- Make buttons and links inside the content as usual: the ink never takes the pointer from them.

**Avoid**

- Raising `Intensity` near 1 behind small text.
- Placing the control where it is resized constantly: every new size rebuilds the simulation.

### Common questions

**Does it need a GPU?** No. The simulation runs on the CPU on every platform Avalonia supports and draws one picture
per frame.

**Why does it look still?** Under reduced motion the control draws one composed frame of the slow stir and stops.

## API reference

<!-- api: InkFluid -->

The quality values:

| Type | Values |
|---|---|
| `InkFluidQuality` | `Low`, `Medium`, `High` |

## Accessibility

- The ink is decoration. It is not focusable, takes no pointer hits and does not appear in the automation tree; the
  content stays fully readable and interactive.
- The ink's coverage is capped low, so text over it keeps its contrast in both themes.
- Under reduced motion the control shows one still frame and runs no animation.

## Motion

- The fluid is a stable-fluids simulation: the velocity field carries itself and the ink along (semi-Lagrangian
  advection), vorticity confinement keeps small swirls alive, and a 20-step pressure solve keeps the flow from
  bunching up. The picture is lit softly from the ink's slope, so wisps look rounded rather than flat.
- The pointer's speed drives the stir, and the amount of ink grows with it: slow moves leave faint trails, quick
  flicks bloom.
- Ink fades over a couple of seconds. Tinted ink hands its color to the neutral share as it spreads, so fresh strokes
  carry the accent and old wisps turn grey.
- On touch screens a finger stirs while it is down; vertical scrolling keeps working.
- The simulation pauses while the control is scrolled out of view or hidden and continues where it left off. Resizes
  wait 120ms for the size to settle before the simulation is rebuilt.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Ink | Live simulation | One still frame | One still frame |

## Related

- `DotGrid` is a lighter, geometric background that reacts to the pointer.
- `GravityWell` makes a grid of pictures react instead of the background.

| Component | Choose it when |
|---|---|
| `InkFluid` | You want a soft, organic, always-moving background |
| `DotGrid` | You want something lighter and more precise |
| `GravityWell` | The content itself should react |
