---
title: Scrub input
summary: A Figma-style number field: drag the label to scrub, or type values, units, and arithmetic.
---

## When to use

- Property inspectors with position, size, rotation and opacity values.
- Dense settings where users both type exact values and drag to adjust them.
- Numeric values with a unit that the user can change, such as `px` or `%`.

## When not to use

- A plain form number with a stepper: use `NumberField`.
- A value picked from a range where position matters more than the number: use `Slider`.
- Currency amounts: use `MoneyInput`.
- A single text value with no numeric meaning: use `Input`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ScrubInput Label="X" Value="120" Unit="px" />
</UserControl>
```

The label is the drag handle. Drag it sideways to change the value, or click the field to type.

### Limits and wrapping

`Minimum` and `Maximum` clamp the value. Past a limit the value stretches back with a rubber-band effect.
`IsWrapping` makes the value wrap around the limits instead; it needs both limits to be finite.

```xml
<arc:ScrubInput Label="W" Value="320" Minimum="0" Unit="px" ResetValue="320" />
<arc:ScrubInput Label="O" Value="80" Minimum="0" Maximum="100" Unit="%" ResetValue="100" />
```

```csharp
var rotation = new ScrubInput
{
    Label = "R",
    Value = 15,
    Minimum = 0,
    Maximum = 360,
    IsWrapping = true,
    IsTrackVisible = false,
    Unit = "°",
    ResetValue = 0
};
```

`ResetValue` is where a double click on the label returns. `IsTrackVisible` defaults to showing the track when both
limits are finite.

### Steps

`Step` is the change per arrow key. `FineStep` (Alt) and `CoarseStep` (Shift) default to one tenth and ten times
`Step`. `PixelsPerStep` sets how many pixels of dragging make one step.

```xml
<arc:ScrubInput Label="Gap" Value="1.5" Step="0.1" Minimum="0" Unit="em" />
```

### Multiple units

With several `Units` the unit becomes a button that cycles through them. Typing a unit, such as `50%`, switches to it.
An unknown unit is rejected.

```csharp
var width = new ScrubInput
{
    Label = "Width",
    Value = 240,
    Minimum = 0,
    Units = ["px", "%", "em"]
};
```

### Sizes

`Size` sets the height: `Small` is 30px and `Medium` (the default) is 36px.

```xml
<arc:ScrubInput Label="Small" Size="Small" Value="12" Unit="px" />
<arc:ScrubInput Label="Medium" Value="12" Unit="px" />
```

### Binding and events

`Value` is two-way. `ValueChanged` fires for every change, and `ValueCommitted` fires once a change made in the field
settles. Both report the old value, the new value and the `ChangeSource`.

```xml
<arc:ScrubInput Label="Opacity" Value="{Binding Opacity, Mode=TwoWay}" Minimum="0" Maximum="100" Unit="%" />
```

```csharp
amount.ValueCommitted += (_, e) =>
{
    Console.WriteLine($"{e.OldValue} -> {e.NewValue} {e.Unit} ({e.ChangeSource})");
};
```

## API reference

<!-- api: ScrubInput -->

## Accessibility

- The field is a spin button. Its value text reads the value with its unit, and percentages and angles are spoken as such.
- The label is a real label: a click focuses the field and selects its text. ArrowUp and ArrowDown step, Enter commits
  what was typed, and Escape restores the last value or cancels a drag.
- An invalid entry sets the invalid state for a short time and keeps the old value. The message is announced.
- The unit button is named "Unit: <unit>. Change unit."

## Motion

- Past a limit the field stretches with resistance and springs back on release, and the digits lean with the stretch.
- An invalid entry or a step at a limit shakes the field once.
- A unit change rises in with a small blur. The track fill follows the pointer while dragging and glides to the new
  value when typed or stepped.
- Reduced motion removes the stretch, the shake and the glide. The unit fades in place. Off motion jumps to the end state.

## Related

- `NumberField` for a form number with a stepper and a limit note.
- `Slider` when the position on a range matters more than the exact value.
- `MoneyInput` for currency values.
