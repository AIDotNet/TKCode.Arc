---
title: Dot grid
summary: A field of dots that swells away from the pointer like a lens and sends a ripple out from every click.
---

<Demo name="Preview" />

## When to use

`DotGrid` fills an area with a quiet grid of dots that reacts to the pointer. Around the pointer the dots swell outward
like a lens and take the accent color; a click or a tap sends a ring of motion across the field. Content you put
inside sits on top of the dots and keeps working as usual.

- **Landing page heroes**: give a headline some life without a video or a heavy illustration.
- **Sign-in panels, empty states and countdowns**: a background people can play with while they wait.
- **Section breaks**: a band between two parts of a long page that invites a poke.

## When not to use

- **Behind dense text, tables or charts**: moving dots fight with reading. Keep the field behind a headline and a few
  actions.
- **When nothing should react**: a static dot pattern drawn with a brush costs less.
- **Several on one screen**: one reactive background per view is plenty.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DotGrid Height="420">
        <TextBlock Text="Built to be poked at" HorizontalAlignment="Center" VerticalAlignment="Center" />
    </arc:DotGrid>
</UserControl>
```

`DotGrid` is a `ContentControl` in the Special package. Register the package with `UseArcSpecial()` next to
`UseArc()`. Give the control a height, or let its content size it; it is at least 240px high. The field has no
background of its own: set `Background`, `BorderBrush`, `BorderThickness` and `CornerRadius` to frame it, and the dots
are clipped to that rounded box.

### As a hero

Put the headline and the actions inside. The field listens to the pointer across the whole control, over the content
too, so the lens keeps following while people move toward a button.

<Demo name="Hero" />

### Density, lens and colors

`Gap` is the distance between dots (22px by default, never less than 6). Lower values give a denser field: a 720 by
420 field has about 660 dots at the default gap and about 1,600 at 14. `DotSize` is the radius of a resting dot.
`Radius` is how far the lens reaches and `Strength` how far it pushes the dots at its strongest point, a third of the
way out from the pointer. `DotBrush` and `ActiveDotBrush` set the resting and the lit color; they default to the strong
border and the accent tokens, so the field follows the theme and the accent.

<Demo name="Colors" />

```xml
<arc:DotGrid Height="300"
             Gap="18"
             Radius="200"
             Strength="24"
             IsFadeEnabled="False"
             IsRippleEnabled="False"
             ActiveDotBrush="{arc:ArcDesignTokenResource Series2}" />
```

`IsFadeEnabled` fades the dots out toward the edges (on by default), which keeps the field from ending in a hard line.
`IsRippleEnabled` turns the click ripples off.

### Ripples from code

`Ripple(point)` sends a ripple from a point in the control's coordinates, as a click there would. Use it to answer
something that happened elsewhere, such as a form being sent.

```csharp
field.Ripple(new Point(field.Bounds.Width / 2, field.Bounds.Height / 2));
```

### Best practices

**Do**

- Keep the content short and centred: a headline, a sentence and one or two actions.
- Use a larger `Gap` (26 to 32) for full-width heroes and slower devices.
- Match the brand with `ActiveDotBrush` rather than the resting color; the lit dots carry the color.

**Avoid**

- Very small gaps on very large areas. Every dot is simulated, so the cost grows with the dot count.
- Strengths much above the gap: dots then cross each other and the lens loses its shape.

### Common questions

**Do buttons inside still work?** Yes. The field is drawn under the content and never takes the pointer from it; it
only watches the pointer pass.

**Why don't the dots move?** Reduced motion keeps every dot in place and only lights the dots near the pointer.
With motion off the field is still.

## API reference

<!-- api: DotGrid -->

## Accessibility

- The field is decoration. It is not focusable and does not appear in the automation tree; the content keeps its own
  names and roles and stays in the reading order.
- Give the control `AutomationProperties.Name` only when the area itself means something; it then reads as a group.
- Under reduced motion the dots never move. The lens only lights the dots near the pointer, and clicks do not ripple.

## Motion

- Every dot follows its own lightly damped spring toward the offset the lens and the ripples ask of it, so the field
  wobbles a little and settles instead of snapping.
- The lens follows the pointer with a short lag and fades in when the pointer arrives and out when it leaves. Its push
  peaks a third of the way out from the pointer and falls to nothing at its rim; the dots it lights grow and take the
  active color.
- A ripple travels outward at 620px per second and fades over 1.5 seconds. Up to six ripples can overlap; a seventh
  replaces the oldest.
- On touch screens the lens appears only while a finger is down, and every tap ripples.
- Frames run only while something moves, and pause while the field is scrolled out of view.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Lens | Pushes and lights dots | Lights dots | None |
| Ripples | Travel across the field | None | None |

## Related

- `GravityWell` bends a grid of tiles around the pointer instead of a field of dots.
- `InkFluid` fills a hero with ink that the pointer stirs.

| Component | Choose it when |
|---|---|
| `DotGrid` | You want a light, geometric background with a lens |
| `GravityWell` | The grid itself is content: photos, avatars or logos |
| `InkFluid` | You want a soft, organic background |
