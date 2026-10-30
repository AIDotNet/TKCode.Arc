---
title: Sticker peel
summary: A surface of stickers with a real peel. Hovering lifts a corner, pulling folds the sticker back with its paper backing showing, and letting go sticks it down where you dropped it.
---

<Demo name="Preview" />

## When to use

`StickerPeel` is a surface you can decorate. Each sticker has a white die cut border; hovering lifts its nearest
corner, pulling folds it back along a real fold, and pulling far enough peels it off so you can carry it somewhere
else. It works with the keyboard and with touch, and it tells you the layout so you can save it.

- **Profiles and team walls**: a corner of the product people decorate themselves, with badges they earned or picked.
- **A launch or a campaign page**: a playful object that rewards anyone who reaches out and touches it.
- **Collections**: an achievement shelf or a sticker book that fills up over time.

## When not to use

- **Ranking or ordering things**: position here is decoration, not data; use a reorderable list.
- **Text people have to read**: a sticker holds a mark or a word, not a paragraph.
- **Busy tools and forms**: it invites play, which is the wrong mood where people are trying to finish a task.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:StickerPeel Height="320" Label="Laptop lid">
        <arc:StickerPeel.Stickers>
            <arc:StickerPeelItem Id="ship" Label="Ship it sticker" Shape="Pill" X="0.3" Y="0.35" Rotate="-6">
                <Border Width="112" Height="40" Background="#ffd84d">
                    <TextBlock Text="Ship it" HorizontalAlignment="Center" VerticalAlignment="Center" />
                </Border>
            </arc:StickerPeelItem>
        </arc:StickerPeel.Stickers>
    </arc:StickerPeel>
</UserControl>
```

`StickerPeel` is a `ContentControl` in the Special package. Register the package with `UseArcSpecial()` next to
`UseArcControls()`. Give it a height; it is at least 280px high. Its `Content` is drawn on the surface under the
stickers, and the stickers are clipped to the surface's rounded box.

### Stickers

Each `StickerPeelItem` has a stable `Id`, a `Label` for assistive technology, the printed `Content`, a starting centre
(`X` and `Y`, each from 0 to 1 across the surface), an optional `Rotate` in degrees and a `Shape`. Give the content a
fixed size: the white die cut border and the outline are added for you. Keep it under about a dozen stickers so the
surface stays readable.

| Shape | Outline | Where it peels from |
|---|---|---|
| `Circle` | The smaller side is the diameter | The point of the rim nearest the pointer |
| `Rounded` | A rectangle with rounded corners | The nearest corner, along its diagonal |
| `Pill` | A rectangle with fully rounded ends | The nearest corner, along its diagonal |

<Demo name="Badges" />

### Saving the arrangement

Placements are fractions of the surface, so the arrangement scales with its container and survives resizes.
`PositionsChanged` is raised once a sticker settles somewhere new, with the placement of every sticker. Keep them to
save the layout, and pass them back as `Positions`. Changing `Positions` from outside, such as a reset, glides each
sticker to its new spot. `Reset()` puts every sticker back where its item starts.

<Demo name="Saved" />

```xml
<arc:StickerPeel Positions="{Binding Positions, Mode=TwoWay}" />
```

### Using the keyboard

Tab moves between stickers. Enter or Space picks the focused sticker up, and again sticks it down where it is. While
carrying, the arrow keys move it by 10px, or 40px with Shift; Escape puts it back where it was picked up. Leaving a
sticker you are carrying with Tab sticks it down.

### Best practices

**Do**

- Choose it for one playful, tactile moment: a profile page, a launch page, a team wall or a badge shelf.
- Give every sticker a fixed-size face and a meaningful `Label`.
- Store `PositionsChanged` output to persist the arrangement.

**Avoid**

- Using it where precise placement or order matters.
- Putting text people must read on a sticker's back: a folded corner only shows plain paper.

### Common questions

**Can I peel it on a touch screen?** Yes: a drag on a sticker peels it the same way. Swipes that start outside a
sticker still scroll the page.

**Why does the sticker just follow my pointer?** Reduced motion skips the peel and the hover lift: pressing picks the
sticker up directly and it follows the pointer 1:1.

## API reference

<!-- api: StickerPeel -->

## Accessibility

- The surface is a group named by `Label`; each sticker is a button with the role description "sticker", its own
  label, and a toggled state while carried.
- Each sticker's help text explains the keyboard path, and a polite status region announces peeled off, picked up,
  stuck down and put back.
- The artwork carries no meaning of its own; the label does.
- Pointer focus draws no ring; keyboard focus shows the shared focus ring. The lift and shadow of a carried sticker
  show which one is active.

### Keyboard interactions

| Key | Action |
|---|---|
| Tab | Moves between stickers. Leaving a sticker you are carrying sticks it down. |
| Enter or Space | Picks the focused sticker up, or sticks it down where it is. |
| Arrow keys | Moves a sticker you are carrying by 10px, or 40px with Shift. |
| Escape | Puts a carried sticker back where it was picked up. |

## Motion

- The peel is a real fold: the pulled part is mirrored across the fold line, showing a plain paper backing with one
  soft shade at the bend and a light shadow that always falls downward. The fold may lean at most 35 degrees, so it is
  always a short, clean corner.
- Circles peel from the point of the rim nearest the pointer; rounded shapes and pills peel from a corner along its
  diagonal.
- Hovering with a mouse lifts the nearest corner a few pixels. Moving to another corner lays the first one flat
  before lifting the next.
- All springs are critically damped: the paper trails the finger slightly and settles without overshoot, bounce or
  swing.
- A sticker comes off once the fold passes the middle, sooner on a quick tug, or once the hand has clearly carried it
  away. It then rises 3% with a soft shadow and follows the hand at its own angle.
- Dropping lands it exactly where it is on screen and the shadow fades in 200ms. Grabbing it again while it settles
  catches it in place.
- Carried stickers stretch against the surface edges with growing resistance and are clamped inside on drop,
  accounting for their angle.
- Frames run only while a sticker moves, and pause while the surface is out of view. Each frame writes the pose of a
  sticker (a transform) and the geometry of its fold; nothing is laid out.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Hover lift | Lifts the nearest corner | None | None |
| Peel | Folds the corner back | None; pressing picks the sticker up | None; pressing picks the sticker up |
| Carry | Rises and casts a shadow, trailing the hand | Follows the pointer 1:1 | Follows the pointer 1:1 |

## Related

- `DotGrid` and `GravityWell` are other playful pointer surfaces.
- `ReorderableList` is the choice when order carries meaning.

| Component | Choose it when |
|---|---|
| `StickerPeel` | People arrange marks for fun and the layout is theirs |
| `ReorderableList` | The order of the items matters |
| `GravityWell` | A grid of pictures should react to the pointer |
