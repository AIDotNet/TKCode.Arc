---
title: Glass card
summary: A card of frosted glass that blurs what lies behind it, with a soft edge light and a sheen that follows the pointer or a tilt signal.
---

<Demo name="Preview" />

## When to use

`GlassCard` is a content card made of frosted glass, for places where the background is part of the design: a photo, an
illustration, a map or a colorful hero area. The card shows a blurred, tinted copy of what lies behind it, so the
background stays readable as color and shape while the text on the card stays sharp. A thin edge light runs along the
card, bright at the top left and fading to a faint shade at the bottom right, so the card reads as a raised pane even on
a busy picture. A soft sheen follows the pointer across the glass and brightens the edge near it; with `MaxTilt` the card
also leans toward the pointer. The same light can follow any other signal through `Tilt`, such as a device's orientation.

- **A summary over a hero picture**: tonight's weather over a coastline, a trip over a city map.
- **A floating panel on an illustrated page**: a sign-in card, a plan picker or a short notice.
- **A focal card in a showcase**: a product, a pass or a feature you want to feel like an object.

## When not to use

- **Plain pages on a solid background**: there is nothing to blur. Use `Card`, which is lighter and clearer.
- **Long text or dense forms**: whatever shows through the glass lowers the contrast. Keep the text short and large, or
  use an opaque surface.
- **Many cards in a grid**: each card draws the backdrop again under itself and blurs it. Use one or two at a time.
- **A collectible card with foil and a flip**: use `HoloCard`.

## Usage

Set `arc:GlassBackdrop.Source` on a container to the layer behind the glass. Every glass surface inside that container
shows a blurred copy of the part of that layer it covers. The source must be a layer behind the card (a sibling or one of
its children), not an ancestor of the card.

```xml
<Panel arc:GlassBackdrop.Source="{Binding #Scene}">
    <Border Name="Scene" Background="#2B3A55" /><!-- your picture or page -->
    <arc:GlassCard Width="320" HorizontalAlignment="Center" VerticalAlignment="Center">
        <TextBlock Text="Evening swim" />
    </arc:GlassCard>
</Panel>
```

`GlassCard` derives from Avalonia's `ContentControl` and lives in the Special package: register it with
`UseArcSpecial()` next to `UseArc()`.

### How the frost is made

Avalonia applies effects (such as `BlurEffect`) to an element's own drawing only; there is no public filter for whatever
is behind an element. The glass therefore paints the backdrop again under itself: a `VisualBrush` of the source, placed
where the source lies relative to the card, blurred by a `BlurEffect` on that layer only and clipped to the card's corners.
The tint, the glaze, the sheen and the edge light are drawn over it, and the content over all of that. Everything goes
through public Avalonia APIs.

The copy is a picture of the source taken when the glass draws. It is taken again when the card moves or resizes against
the source and when the theme changes. When the source changes on its own (a video, a live chart, an animated
illustration), set `arc:GlassBackdrop.IsLive="True"` so the copy is redrawn every frame while the card is on screen.

Without a source the card is a translucent tinted pane with no blur: what lies behind shows through sharp, so its tint is
stronger. Under the ArcDesign `ReduceTransparency` token the card is the opaque raised surface with the ordinary border.

### Tilt and light

`MaxTilt` (0 by default, up to 20 degrees) lets the card lean toward the pointer in perspective, the edge under the
pointer tipping away. `SheenIntensity` scales the sheen and the edge glow from 0 to 1. On touch screens the light
follows the finger while it is down.

`Tilt` takes the light from another source while the pointer is elsewhere: a vector from -1 to 1 on each axis, where
(0, 0) is at rest. Feed it a device's orientation, a scroll position or a slider.

<Demo name="TiltSignal" />

### Without a backdrop

A card without `arc:GlassBackdrop.Source` is still glass: a translucent, more strongly tinted pane with the same edge
light and sheen, but no blur.

<Demo name="NoBackdrop" />

### Content guidelines

- Keep the text short and give it weight: a title, one or two lines and an action or two.
- Pick a backdrop with large soft shapes. Fine detail and small text behind the card blur into noise.
- Check both themes: the light theme's glass is milky, the dark theme's smoky.

### Common questions

**Why does my source show nothing?** The source must be in the same window and must not contain the card. A card inside
the source would have to paint itself while it paints the source, so the glass falls back to the tinted pane.

**Does it work in a popup?** Yes, when the popup lives in the window's overlay layer (Arc popups do by default). A popup
in its own native window cannot see the window behind it and shows the tinted pane.

## API reference

<!-- api: GlassCard -->

<!-- api: GlassBackdrop -->

## Accessibility

- The card is a group; give it `AutomationProperties.Name` when it stands for one thing ("Tonight's swim forecast").
  Its content is read as usual; the glass layers are not in the accessibility tree.
- The card is not focusable; the controls inside it are.
- The glass lowers the contrast of whatever text sits on it. Keep text on the card at least 14px, prefer medium or
  semibold weights, and test it on the actual backdrop in both themes.
- The ArcDesign `ReduceTransparency` setting turns every glass surface into an opaque one.

## Motion

- The sheen follows the pointer on a smooth spring (about 0.35 s, no overshoot), fades in over 240ms when the pointer
  arrives and out over 480ms when it leaves.
- The lean follows on a smooth spring of about 0.45 s, without overshoot.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Pointer over the card | Sheen and edge glow follow, lean (with `MaxTilt`) | None | None |
| `Tilt` changes | Sheen and lean follow | None | None |

## Responsive behavior

- The card has no breakpoint: it takes the width it is given; set `Width` or `MaxWidth` in wide layouts.
- On touch the sheen and the lean follow the finger while it is down and settle when it lifts.
- The frost is lined up with the source after every layout pass, so resizing the window keeps the copy in place.

## Related

- `Card` for content cards on plain surfaces.
- `HoloCard` for a collectible card with foil, glitter and a flip.
- `GlassMenu`, `GlassSegmented` and `GlassTabBar` for other glass surfaces that share the same material.

| Component | Choose it when |
|---|---|
| `GlassCard` | A card should float over a picture or a colorful background |
| `Card` | Content cards on a plain page |
| `HoloCard` | One card should feel like a physical collectible |
