---
title: Mobile checkbox
summary: A 22 px rounded checkbox in a 44 px row, with a check, an indeterminate dash and no hover state.
---

<Demo name="Basic" />

## When to use

`Checkbox` lets a person pick any number of options, or agree to something, in a form or a list.

- **Several choices at once**: filters, interests, items to include.
- **A single agreement**: "Remember this device", "I accept the terms".
- **Select all**: with `IsThreeState`, a parent row shows a dash when only some of its children are selected.

The live demo runs inside a phone frame, a desktop simulation; touch feedback on a real device has not been verified here.

## When not to use

- **A setting that takes effect at once**: use `Switch`.
- **Exactly one of several options**: use `Segmented` or a list of rows with a check.
- **A command**: use `Button`.

## Usage

`Checkbox` derives from Avalonia's `ToggleButton`, so `IsChecked`, `IsThreeState`, `Click` and binding work as they do there. The label is the content, and the whole row is the touch target.

```xml
<arcm:Checkbox xmlns="https://github.com/avaloniaui"
               xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
               Content="Remember this device"
               IsChecked="{Binding Remember}" />
```

Set `IsThreeState` and bind `IsChecked` to a nullable value for the indeterminate state. A finger on the row dims it; there is no hover state.

### Tokens

`CheckboxToken` holds the geometry: `TouchTarget` (44), `BoxSize` (22), `BoxRadius` (6), `BoxBorderWidth` (1.5), `CheckExtent` (16), `LabelMargin`, `PressedOpacity` (0.6) and `DisabledOpacity` (0.5). Colors come from the ArcDesign tokens.

## API reference

<!-- api: MobileCheckbox -->

## Accessibility

The row is a toggle button named by its label, so always give it content; a box with no label has no name. The row is at least 44 px high, and the box is not the only target.

## Motion

The fill and the check change on the fast curve, and at once with reduced motion.

## Related

- `MobileSwitch`: for a setting that takes effect at once.
- `MobileListGroup`: rows that can hold a checkbox in their trailing content.
- `MobileButton`: the usual way to submit a form.
