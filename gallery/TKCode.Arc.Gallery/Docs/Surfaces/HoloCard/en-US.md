---
title: Holo card
summary: A collectible card drawn in perspective that follows the pointer with a shifting rainbow sheen, then turns over to reveal a second side.
---

<Demo name="Preview" />

## When to use

`HoloCard` turns one card into the object on the page: a membership card, a conference badge, an achievement or a gift
card that is revealed. Under the pointer it leans in perspective, the corner under the pointer sinking away, while a
rainbow foil, a sprinkle of glitter and a soft glare slide across its face with the light, and a floating shadow leans
the other way. With a back, a click turns it over half a turn, so the card has two sides to read.

- **A club pass or loyalty tier** handed over the moment someone joins or moves up a level.
- **An event badge or ticket** with the holder on the front and the room and time on the back.
- **An achievement or reward** that deserves a moment of delight, such as a badge earned or a tier reached.
- **Unwrapping a gift card**: the front invites a turn, the back shows the value or the code.

## When not to use

- **Several cards in a list or grid**: each card blends three light layers every frame it moves. Keep it to one or two
  on screen; use `Card` for collections.
- **Cards that hold controls**: the faces are pictures under a projection and do not take input. Put buttons and links
  next to the card, not on it.
- **A stack of payment cards to browse**: use `WalletStack`.
- **Plain content that only needs a lift on hover**: use `Card`; the tilt and light are decoration and draw attention.

## Usage

Give the card a `Front` and, when it should turn over, a `Back`. Both take any content, stretched over the whole card and
clipped to its rounded corners. The card is always 5:7. Without a width it is as wide as its slot up to 300, so a
narrow container gets a smaller card; set `Width` to fix it.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:HoloCard Width="290" Label="Mira Okafor, Lumen Society patron pass">
        <arc:HoloCard.Front>
            <Grid Background="#0D0D0D"><!-- art, name, stats --></Grid>
        </arc:HoloCard.Front>
        <arc:HoloCard.Back>
            <Grid Background="#0D0D0D"><!-- mark, title, note --></Grid>
        </arc:HoloCard.Back>
    </arc:HoloCard>
</UserControl>
```

`HoloCard` derives from Avalonia's `TemplatedControl`. It lives in the Special package: register it with
`UseArcSpecial()` next to `UseArc()`.

### Anatomy

1. **Floating shadow**: a soft dark box inset from the card, offset against the tilt; it does not turn with the card, so
   a spinning card shows the page between its edges.
2. **Card**: the front and the back faces, each with the content first and then three light layers blended into it:
   - the **foil**, a diagonal rainbow of pink, peach, mint, sky and lilac bands that brightens the content (color dodge),
     follows the light and turns its hue as the light moves;
   - the **glitter**, a fine scatter of white points around the light that drifts against the pointer (front only);
   - the **glare**, a soft white highlight at the light that fades to a slight shade at the far corners.
3. **Edge light**: a 1px line of white at 14% just inside the card's edge.
4. **Focus ring**: 2px, 2px outside the card, leaning with it; only for keyboard focus.

### Turning over

A click or a tap on the right half spins the card a further half turn to the right; on the left half it spins back to
the left. Enter and Space spin it to the right. Turns add up: right, right, left goes 0°, 180°, 360°, 180°. A second
press during a spin re-aims the same spring, so the card carries on with the speed it has instead of starting again.

`IsFlipped` says which side faces the viewer and is two-way: bind it to keep a side in your own state, or set it to turn
the card from code. `FlippedChanged` reports every turn made by a click, a tap, the keyboard or `Flip`, with the side
that caused it; setting `IsFlipped` yourself does not raise it.

<Demo name="Controlled" />

```csharp
card.FlippedChanged += (_, e) => log.Add($"{(e.IsFlipped ? "Back" : "Front")} ({e.Side})");
card.Flip();                         // to the right
card.Flip(HoloCardFlipSide.Left);    // back to the left
```

### A badge that only tilts

Without a `Back` the card is a tilting picture: it does not flip, is not focusable and is not a button. `MaxTilt` sets
the largest lean in degrees (14 by default, 0 turns tilting off), and `Intensity` scales the foil, glitter and glare
together from 0 to 1. On a light card stock the layers read stronger, so half the intensity looks balanced.

<Demo name="Badge" />

### Reduced motion

When the app or the system asks for reduced motion, the card stays flat and still: no tilt, no lift, no moving light and
no glitter. Turning over cross-fades the two faces over 240ms instead of spinning. With motion off the faces swap at
once.

<Demo name="ReducedMotion" />

### Gestures

| Input | Effect |
|---|---|
| Mouse or pen over the card | Lean toward the pointer (up to `MaxTilt`), lift to 1.035, full light |
| Mouse or pen leaves | Lean, lift and light settle back together |
| Click | Turn over: right half to the right, left half back to the left |
| Finger down and drag | Lean under the finger, without the hover lift |
| Tap (moves less than 10px) | Turn over by the half it lands on |

### Content guidelines

- Design each face for the whole card at 5:7: the content is stretched over the card and clipped to its 22px corners.
- Give each face its own opaque background. The light layers brighten what is under them, so a dark or saturated stock
  shows the foil best; on a light stock lower `Intensity` to about 0.5.
- Keep text large and short: the faces are projected, and a name, a role and two figures read well while they lean.
- Use your own marks and generated art; the samples here use a fictional stargazing society, an invented observer and a drawn sunset.
- Set `Label` to a sentence that names the card, such as "Mira Okafor, Lumen Society patron pass". Without one the
  card reads the text of the side facing the viewer.

### Best practices

- Show one card, two at most, at a time. Each moving card rebakes its light layers and blends them into its content.
- Leave room around the card for the lean, the lift and the shadow: about 40px below and 16px at the sides.
- Put the card's actions (Add to wallet, Share) outside it, near the card.
- Keep the back as useful as the front: the room and time on a badge, the code on a gift card.

### Common questions

**Can the card follow the device's tilt on a phone?** Not yet: Avalonia does not expose the device's orientation, so on
touch screens the card leans under the finger while it is down.

**Why do clicks on a button inside the card not work?** The faces are drawn under a projection and do not take input.
Place controls outside the card.

**Can I start on the back?** Yes: set `IsFlipped="True"`. The card shows the back from the first frame, without a spin.

**Does a hidden face cost anything?** The face turned away is hidden, so it is neither drawn nor read out.

## API reference

<!-- api: HoloCard -->

<!-- api: HoloCardFlippedEventArgs -->

<!-- api: HoloCardFlipSide -->

## Accessibility

- With a back the card is a toggle button with the role description "flip card" (`RoleDescription`); its pressed state
  follows `IsFlipped` and changes as soon as it is pressed, not when the spin ends.
- Its name is `Label`, or the text of the side facing the viewer. The face turned away, the shadow and the light layers
  are not in the accessibility tree.
- Without a back the card is a named group and is not focusable.
- Keyboard focus shows a 2px ring 2px outside the card that leans with it; pointer focus draws none.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move to the card (only when it has a back) |
| `Enter` / `Space` | Turn the card over to the right |

## Motion

- The lean follows the pointer on a spring without overshoot (about 0.48 s to settle); the light, the lift and the
  shadow follow on a slightly softer spring, so the light trails the lean a little.
- The shadow moves 0.9px against every degree of lean and strengthens from 28% to 40% while hovered.
- A turn is a spring of about 0.6 s with a hint of overshoot. Halfway through, the foil sweeps across and its hue turns
  by up to 90° while the light rises to full, then settles back as the other side arrives.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Pointer over the card | Lean, lift, moving light | None | None |
| Turn over | Half-turn spin | Faces cross-fade (240ms) | Faces swap |
| Glitter | Shown and drifting | Hidden | Hidden |

## Responsive behavior

- `HoloCard` has no breakpoint: it takes the width of its slot up to 300 px and keeps its card proportions, so it
  shrinks with a narrow column. Set `Width` (the gallery uses 290) when it sits in a wide layout.
- On touch the card tilts under the finger while it is pressed and settles when it is lifted; a tap (moving 10 px or
  less) turns it over. Device orientation does not drive the tilt.
- Turning the card over is also a button action (Enter or Space, or `IsFlipped`), so the back is reachable without a
  pointer.

## Related

- `Card` for content cards with a quiet hover lift.
- `WalletStack` for a stack of payment cards with real materials.
- `CardStack` for a pile of cards to swipe through.

| Component | Choose it when |
|---|---|
| `HoloCard` | One card should feel like a physical object with two sides |
| `Card` | Content cards in a list or grid |
| `WalletStack` | Several payment cards to browse and open |
