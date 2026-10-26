---
title: Mobile radio
summary: A 22 px radio in a 44 px row whose accent dot springs in, one choice per group, with arrow-key navigation and no hover state.
---

<Demo name="Basic" />

## When to use

`Radio` lets a person pick exactly one option out of a short list, in a form or a settings screen.

- **One of a few options**: delivery speed, a payment method, a sort order.
- **Options that need a label each**: every row has its own text, so the choices read like a list.
- **A choice that is confirmed later**: the radio shows the pick; a button submits it.

The live demo runs inside a phone frame, a desktop simulation; touch feedback on a real device has not been verified here.

## When not to use

- **A short row of views of the same content**: use `Segmented`.
- **Any number of options**: use `Checkbox`.
- **A setting that takes effect at once**: use `Switch`.

## Usage

`Radio` derives from Avalonia's `RadioButton`, so `IsChecked`, `GroupName`, `Click` and binding work as they do there. Radios that share a `GroupName` are mutually exclusive; put the radios of one group in the same panel. The label is the content, and the whole row is the touch target. The design is a `RadioButton` with group names rather than a container with a selected value: a radio can sit in any row layout, and the choice binds with `IsChecked` on each item.

```xml
<StackPanel xmlns="https://github.com/avaloniaui"
            xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Radio GroupName="delivery" Content="Standard, free" IsChecked="True" />
    <arcm:Radio GroupName="delivery" Content="Express, 2 days" />
</StackPanel>
```

With the focus on a radio, the arrow keys move the choice to the previous or next enabled radio of the same group among its siblings, and wrap around. A finger on the row dims it; there is no hover state.

### Tokens

`RadioToken` holds the geometry: `TouchTarget` (44), `CircleSize` (22), `CircleBorderWidth` (1.5), `DotSize` (10), `LabelMargin`, `PressedOpacity` (0.6) and `DisabledOpacity` (0.5). Colors come from the ArcDesign tokens.

## API reference

<!-- api: MobileRadio -->

## Accessibility

The row is a radio button named by its label, so always give it content; a radio with no label has no name. The row is at least 44 px high, and the circle is not the only target. The arrow keys move within a group, and the checked state is exposed to assistive technology as it is for any `RadioButton`.

## Motion

The dot springs in from a small scale with a fade and goes out the same way; because it is a spring, a choice that flips half-way keeps its speed. The outline changes color on the fast curve. With reduced motion, the dot and the outline change at once.

## Related

- `MobileCheckbox`: for any number of choices.
- `MobileSegmented`: for a short row of views.
- `MobileListGroup`: rows that can hold a radio in their trailing content.
