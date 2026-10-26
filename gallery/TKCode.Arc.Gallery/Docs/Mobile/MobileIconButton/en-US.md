---
title: Mobile icon button
summary: A 44 by 44 icon-only button with plain, tinted, filled and floating variants and a spring press state.
---

<Demo name="Basic" />

## When to use

`IconButton` is a button that shows only an icon: in a bar, on a card, or floating over content.

- **Bar actions**: search, share, more. The default `Plain` variant is an accent icon with no background.
- **A secondary action on a card or row**: `Tinted`.
- **The main action of a view**: `Filled`, or `Fab` when it floats over scrolling content.

The live demo runs inside a phone frame, a desktop simulation; touch feedback on a real device has not been verified here.

## When not to use

- **An action that needs a word to be understood**: use `Button` with text.
- **A choice between options**: use `Segmented`, `Radio` or `Checkbox`.
- **Several equal floating actions**: a floating button is one main action; use a `Sheet` for more.

## Usage

`IconButton` derives from Avalonia's `Button`, so `Click`, `Command` and binding work as they do there. Set `Icon` to a geometry and `Variant` to pick the look. The button is 44 by 44.

```xml
<arcm:IconButton xmlns="https://github.com/avaloniaui"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                 xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
                 Icon="{x:Static arc:ArcIcons.Share}"
                 Variant="Filled"
                 AutomationProperties.Name="Share" />
```

### Floating action button

`Variant="Fab"` makes a 56 px accent circle with the floating shadow of the ArcDesign tokens. Put it over the content, for example in the `Page` overlay slot or a `Panel` aligned to the bottom right.

<Demo name="Fab" />

### Tokens

`IconButtonToken` holds the geometry: `TouchTarget` (44), `FabSize` (56), `IconExtent` (22), `FabIconExtent` (24), `PressedOpacity` (0.85) and `DisabledOpacity` (0.5). Colors and the shadow come from the ArcDesign tokens.

## API reference

<!-- api: MobileIconButton -->

## Accessibility

An icon has no text, so always set `AutomationProperties.Name` to say what the button does ("Search", "New message"); without it a screen reader announces nothing useful. The button is 44 by 44 at least, and the floating one is 56.

## Motion

Pressing scales the button to 0.97 (the floating one to 0.92) on a snappy spring, with no delay on the way down, and dims it a little; letting go springs back. A press that is released half-way keeps its speed. With reduced motion the button does not scale, it only dims.

## Related

- `MobileButton`: a button with text.
- `MobileNavbar`: where plain icon buttons go in the bar.
- `MobileSheet`: for more actions than one floating button can hold.
