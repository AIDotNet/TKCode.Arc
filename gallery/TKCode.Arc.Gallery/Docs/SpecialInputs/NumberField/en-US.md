---
title: Number field
summary: Enter a bounded number with clear increment controls.
---

## When to use

- Exact numbers with a range, such as seats, quantities or prices.
- Values that users step up and down, with a unit or prefix that travels with the number.
- Scrubbing a value by dragging its label, when `IsScrubEnabled` is on.

## When not to use

- Approximate values where position matters more than the exact number: use `Slider`.
- Digit strings such as phone numbers or codes: use `Input`.
- Currency amounts with a currency code: use `MoneyInput`.
- Canvas-style property panels that scrub many values: use `ScrubInput`.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:NumberField Label="Seats"
                     Minimum="1"
                     Maximum="10"
                     Value="3"
                     Description="Up to 10 per team." />
</UserControl>
```

### Prefix, suffix and step

`Step` sets the increment, and its precision sets the default fraction digits. `SuffixFormatter` lets the unit
follow the value, such as singular and plural.

```xml
<arc:NumberField Label="Price" Prefix="$" Step="0.5" Maximum="999" Value="24.5" />
<arc:NumberField Label="Temperature" Minimum="-20" Maximum="40" Value="21" Suffix="°C" />
```

```csharp
var seats = new NumberField
{
    Label = "Seats",
    Minimum = 1,
    Maximum = 10,
    Value = 3,
    SuffixFormatter = n => n == 1 ? " seat" : " seats"
};
```

A minimum below zero accepts the minus sign while the user types.

### Sizes

`Size` sets the width and the stepper size: `Small` is 164px wide, `Medium` (the default) is 196px and `Large` is 228px.

```xml
<arc:NumberField Label="Small" Size="Small" Value="2" Maximum="99" />
<arc:NumberField Label="Large" Size="Large" Value="48" Maximum="99" />
```

### Limit hint and errors

`IsLimitHintEnabled` shows a note when a limit is reached. `LimitHintFormatter` writes the note. An
`ErrorMessage` marks the field invalid.

```xml
<arc:NumberField Label="Quantity" Value="0" Maximum="5" ErrorMessage="Add at least one item." />
```

```csharp
var guests = new NumberField
{
    Label = "Guests",
    Minimum = 1,
    Maximum = 8,
    Value = 8,
    LimitHintFormatter = (edge, limit) => edge == NumberFieldLimitEdge.Maximum
        ? $"Room holds {limit}"
        : $"At least {limit}"
};
```

### Binding and events

`Value` is two-way. `ValueChanged` reports the old value, the new value and the `ChangeSource`.

```xml
<arc:NumberField Label="Items" Minimum="0" Maximum="50" Value="{Binding ItemCount, Mode=TwoWay}" />
```

```csharp
field.ValueChanged += (_, e) =>
{
    Console.WriteLine($"{e.OldValue} -> {e.NewValue} ({e.ChangeSource})");
};
```

## API reference

<!-- api: NumberField -->

## Accessibility

- `Label` names the field. With `IsLabelVisible="False"` the label is hidden but still names it.
- The field is a spin button. Its value text includes the prefix and suffix, and the stepper buttons are named
  "Increase <label>" and "Decrease <label>".
- ArrowUp and ArrowDown step, Shift or PageUp and PageDown take the large step, and Home and End go to the limits.
- Changes are announced politely. A clamp announces the limit it reached.
- The focus ring appears for keyboard focus only.

## Motion

- The digits roll like an odometer in the direction of the change. Held steppers repeat and speed up.
- A press at a limit stretches the value back, shakes the stepper and shows a short limit note.
- Reduced motion sets the value at once and flashes the limit in the warning color. Off motion does the same.

## Related

- `Slider` when the position on a range matters more than the exact number.
- `Input` for digit strings that have no stepper.
- `MoneyInput` for currency amounts, and `ScrubInput` for dense property panels.
