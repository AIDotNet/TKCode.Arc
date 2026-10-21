---
title: Tooltip
summary: Short supporting text for unfamiliar controls.
---

<Demo name="Sides" />

## When to use

- A one-line label for an icon-only button or a toolbar item.
- The full text of a label that is truncated on the page.
- Short help that is not essential, and that a keyboard user reaches by focusing the control.

## When not to use

- Links, buttons or inputs: use `Popover`. Tooltip content cannot be interacted with.
- A rich preview of a person or a link: use `HoverCard`.
- Information that touch users must see: show it on the page, not in a tooltip.

## Usage

A tooltip is an attached property. Set `arc:Tooltip.Tip` on any control; the text appears on hover or keyboard focus.

### Basic

```xml
<arc:Button Content="Copy" arc:Tooltip.Tip="Copy link" />
```

### Side

`arc:Tooltip.Side` sets the side of the trigger the bubble opens on: `Top` (the default), `Bottom`, `Left` or `Right`.

```xml
<arc:Button Variant="Secondary" Content="Share" arc:Tooltip.Tip="Share with your team"
            arc:Tooltip.Side="Bottom" />
```

### Binding the text

`Tip` accepts any object. Bind it when the text comes from the view model.

```xml
<arc:Button Content="Save" arc:Tooltip.Tip="{Binding SaveHint}" />
```

### Changing the text and the delay

Change the text in code while the tooltip is open. Strings crossfade and the bubble resizes to fit. Set
`ShowDelay` to change the wait before the first tooltip opens; the default is 250ms.

<Demo name="LiveText" />

```csharp
Tooltip.SetTip(save, "Saved just now");
Tooltip.SetShowDelay(save, TimeSpan.FromMilliseconds(400));
```

## API reference

<!-- api: Tooltip -->

## Accessibility

- While the tooltip is open, its text is written to the trigger's help text.
- A tooltip is not a substitute for a name. An icon-only trigger still needs its own `AutomationProperties.Name`.
- A tooltip opens on hover and on keyboard focus, and Escape closes it.
- The bubble contains no interactive elements, so keyboard users can read it without losing focus.

## Motion

- The first tooltip waits 250ms, then rises 3px from its trigger while it scales in from 0.97.
- When the pointer moves to a neighbouring trigger, or the last tooltip closed less than 300ms ago, the next tooltip
  opens at once with a fade only.
- When the text changes while the tooltip is open, the new words rise in with a soft blur and the bubble springs to
  its new size.
- Closing is an instant fade with the bubble shrinking to 0.98.
- Reduced motion removes the movement and keeps a short fade. Off shows or removes the tooltip at once.

## Related

- `Popover` for interactive content anchored to a control.
- `HoverCard` for a rich, read-only preview of a person or a link.
