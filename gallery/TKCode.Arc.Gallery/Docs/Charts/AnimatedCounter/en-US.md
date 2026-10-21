---
title: Animated counter
summary: Give changing totals a clear sense of movement.
---

<Demo name="RollingDigits" />

## When to use

- A number on its own that changes, such as a total, a price or a live count.
- A first-screen statistic that should count up from zero when it scrolls into view.
- A total in a dashboard that needs motion to show that it moved, without a card around it.

## When not to use

- A number that needs a card, a label and a change: use `MetricCard`, which uses this counter.
- Text that changes instead of a number: use `TextMorph`.
- A number that is the readout of a chart: the chart's headline already rolls its own value.

## Usage

`Value` takes the raw number, not a formatted string. `Label` names the number above it, and `Prefix` and `Suffix` add
text around it.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:AnimatedCounter Label="Revenue"
                         Prefix="$"
                         Value="{Binding Revenue}" />
</UserControl>
```

### Decimals and suffix

`Decimals` fixes the number of decimal places, so the number keeps the same width as it changes.

```xml
<arc:AnimatedCounter Label="Conversion" Value="{Binding Conversion}" Decimals="2" Suffix="%" />
```

### Updating the value

Set `Value` to the new total. Only the digit columns that changed turn, in the direction the number moved.

```csharp
revenue.Value += random.Next(100, 4000);
rate.Value = Math.Round(rate.Value + random.NextDouble(), 2);
```

### Reveal on view

`AnimateOnView` rolls every digit up from zero the first time 60% of the counter is visible.

<Demo name="RevealOnView" />

```xml
<arc:AnimatedCounter Label="Rolled up on first view" Value="{Binding Visitors}" AnimateOnView="True" />
```

### Culture

`CultureName` sets the culture for the grouping separators. It defaults to `en-US`, so the output matches on the server
and on the client.

```xml
<arc:AnimatedCounter Label="Users" Value="{Binding Users}" CultureName="de-DE" />
```

## API reference

<!-- api: AnimatedCounter -->

## Accessibility

- The counter is not focusable and defines no keyboard interaction.
- The full formatted text, with prefix, separators, decimals and suffix, is the name of the counter, so a reader gets the
  whole number at once.
- The rolling digits are hidden from assistive technology. The counter does not announce its changes by itself. To
  announce them, set `AutomationProperties.LiveSetting="Polite"` on the surrounding element.

## Motion

- Each digit column turns in the direction the whole number moved, and wraps between 9 and 0.
- When the number of digits changes, the new column and the separator slide in or out, and the width eases to fit. The
  neighboring content shifts slightly with it.
- With `AnimateOnView`, the roll starts once 60% of the counter is visible, and the digits start with a slight stagger.
- Reduced motion replaces the new value at once, without rolling. Off jumps to the final state.

## Related

- `MetricCard` for a number inside a card with its label and change.
- `Sparkline` for a number with its trend.
- `BarChart` and `LineChart` update their own headline values, so they do not need this counter.
- `TextMorph` for text that changes rather than a number.
