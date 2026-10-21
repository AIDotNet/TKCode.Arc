---
title: Metric card
summary: A compact summary for a number that needs context.
---

<Demo name="Default" />

## When to use

- A single number with a label and the context that explains it, such as uptime, orders or latency.
- A number that updates live, where the digits roll toward the direction of the change.
- A number that should show a change chip, such as "+12%", next to its context.

## When not to use

- The trend matters more than the latest value: use `Sparkline`.
- A number without a card frame, such as a figure inside running text: use `AnimatedCounter`.
- A card with media, an owner and actions: use `Card`.

## Usage

`Label`, `Value` and `Context` are the usual parts. `Change` is optional; without it the chip is hidden.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MetricCard Label="Uptime"
                    Value="99.98"
                    Decimals="2"
                    Suffix="%"
                    Context="Last 30 days"
                    Change="+0.02%"
                    Width="280" />
</UserControl>
```

### Change chip

A `Change` that starts with "+" is shown as an up change, and one that starts with "-" or "−" as a down change. Read
the direction back from `ChangeDirection`. Set `IsChangeGlyphVisible` to add a trend icon inside the chip.

<Demo name="PlainNumber" />

```xml
<arc:MetricCard Label="p95 latency"
                Value="182"
                Suffix=" ms"
                Context="vs yesterday"
                Change="−8%"
                IsChangeGlyphVisible="True"
                Width="280" />
```

### Data binding

Bind the value and the text to view-model properties. Each update rolls the digits to the new value.

```xml
<arc:MetricCard Label="Orders"
                Value="{Binding OrderCount}"
                Context="{Binding OrderContext}"
                Change="{Binding OrderChange}"
                Width="280" />
```

### Trend line

`Sparkline` takes a list of values and draws a small trend line next to the number. The line is drawn once when the
card first appears, and redrawn when the data changes.

```csharp
orders.Sparkline = new List<double> { 820, 910, 870, 990, 1040, 1010, 1204 };
```

### Narrow windows

In a window that is 380px wide or less, the card uses tighter padding and moves the chip under the label. No code is
needed for this.

## API reference

<!-- api: MetricCard -->

## Accessibility

- The card is a group named by `Label`. Its help text is the change and the context.
- The number is read by the counter. The change chip is plain text and states its direction with "+" or "−", so the
  direction does not depend on color.
- A value change is not announced by itself. If you need an announcement, set `AutomationProperties.LiveSetting` on
  the card or on a container around it.

## Motion

- The number rolls from zero the first time the card scrolls into view, then turns toward each new value.
- The label, context and chip text roll in from the side the number moved toward.
- The chip width adjusts to the new text on a spring, and the chip scales and fades in or out.
- The trend line draws in over 0.9s the first time it is shown, and cross-fades when the data changes.
- Reduced motion jumps the number to its new value and fades the text. The chip and trend line change without motion.
  Off applies every change at once.

## Related

- `AnimatedCounter` for a bare number that needs no card frame.
- `Sparkline` when the trend matters more than the latest value.
- `Card` for a group of content and actions around a number.
