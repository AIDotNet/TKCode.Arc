---
title: Color picker
summary: A swatch that grows into a picker with format morphing, eyedropper, saved swatches, and contrast readout.
---

<Demo name="ColorField" />

## When to use

- A theme editor, a brand setting or a design tool that needs a color with hex, RGB, HSL or OKLCH input.
- A color with transparency, where the alpha channel is part of the value.
- A color that is checked against a background, with a WCAG contrast readout.
- A short list of saved colors that the user builds up.

## When not to use

- A few fixed colors: use `SegmentedControl` or `RadioGroup`.
- A plain hex text value with no picker: use `Input`.
- Small preset color labels that filter a list: use `ChipGroup`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ColorPicker Label="Accent"
                     Color="#2F6BFF"
                     Format="Hex" />
</UserControl>
```

### Value and format

`Color` is an Avalonia `Color` and is two-way. `Format` sets the format shown in the text field, and the user
can change it in the panel. `HexValue` is read-only and gives the color as `#RRGGBB` or `#RRGGBBAA`, the order
used by CSS.

```xml
<arc:ColorPicker Label="Brand" Color="#E5484D" Format="Oklch" />
```

```csharp
picker.ColorChanged += (_, e) => status.Text = $"Color: {e.Hex}";
```

### Saved swatches

`Swatches` is a list of `ColorSwatch` records. A swatch is saved from the panel, and each change creates a new
list, so bind to the collection or handle `SwatchesChanged`. `MaxSwatches` is 7 by default; when the list is
full, the oldest swatch is dropped.

<Demo name="FormatsAndSavedSwatches" />

### Contrast readout

`ContrastBackground` is the color the WCAG readout compares the value with. The panel shows the ratio and the
grade.

<Demo name="Contrast" />

### Error and disabled state

A non-empty `ErrorMessage` marks the field invalid. `IsEnabled="False"` disables the picker.

```xml
<arc:ColorPicker Label="Locked" IsEnabled="False" />
```

## API reference

<!-- api: ColorPicker -->

<!-- api: ColorSwatch -->

## Accessibility

- `Label` names the trigger, and the panel is named after the label. The trigger exposes its expanded state.
- The saturation and brightness area is a two-dimensional slider. Its value reports both saturation and brightness.
  The hue and opacity sliders report their angle and percentage.
- The arrow keys move the handles, and Shift or PageUp and PageDown move them in steps of ten. Home and End go to
  the minimum and maximum.
- In the text field, Enter applies the value. An invalid value shakes the field and is announced with its error.
  Escape discards a draft first, and a second Escape closes the panel and returns the focus to the trigger.
- The contrast ratio and its grade are given as text. The saved swatches form a list in which Delete removes the
  selected swatch and Alt with the arrow keys moves it.

## Motion

- The panel is revealed from the swatch: the clip grows from the trigger's corners to the full panel, with a shadow
  that follows the clip.
- The handle follows the pointer with a quick spring, and it tightens while it is dragged.
- The format button morphs the text between HEX, RGB, HSL and OKLCH. The contrast ratio counts to its new value.
- An invalid entry shakes the field. A new swatch pops in, and a moved swatch settles into place.
- Reduced motion jumps the handles, fades the panel in place of the clip, and drops the format morph and the shake.
  Off jumps to the end state.

## Related

- `SegmentedControl` or `RadioGroup` for a few fixed colors.
- `Input` for plain text values.
- `ChipGroup` for small preset labels.
