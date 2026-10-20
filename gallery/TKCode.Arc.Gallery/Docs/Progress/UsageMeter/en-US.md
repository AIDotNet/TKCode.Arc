---
title: Usage meter
summary: Show what fills an allowance and how close it is to the limit.
---

## When to use

- Storage, seats or API calls measured against a fixed allowance.
- A billing page that lists what uses the plan and how much of it is left.
- A plan that can be exceeded, where the overage must read without relying on color.

## When not to use

- Work that moves toward a finished state: use `Progress`.
- Categories with no limit, compared with each other: use `BarChart`.
- A single headline metric: use `Gauge` or `MetricCard`.

## Usage

Give the meter a `Label`, the `Limit` and the categories. Each category is a `UsageSegment` with an `Id`, a
`Label` and a `Value` in the same unit as the limit. Keep the `Id` stable so each category keeps its own motion
when the list changes.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:UsageMeter Label="Workspace storage" Limit="50" Unit="GB">
        <arc:UsageSegment Id="media" Label="Media" Value="14.2" />
        <arc:UsageSegment Id="docs" Label="Documents" Value="9.6" />
        <arc:UsageSegment Id="backups" Label="Backups" Value="6.1" />
        <arc:UsageSegment Id="other" Label="Other" Value="2.4" />
    </arc:UsageMeter>
</UserControl>
```

### Bound categories

`ItemsSource` takes precedence over the inline segments. Bind a collection of `UsageSegment` objects. The meter
observes the collection and each segment, so changing a `Value` re-flows the bar.

```xml
<arc:UsageMeter Label="Workspace storage"
                Limit="50"
                Unit="GB"
                ItemsSource="{Binding Categories}" />
```

```csharp
media.Value = 17.4; // the bar, total and badge update
```

### Almost full and over the limit

The badge shows the free amount. At 90% of the limit it changes to "Almost full" with a warning icon, and past the
limit it shows the overage. `WarnThreshold` changes the share that triggers the warning.

```xml
<arc:UsageMeter Label="API calls this month"
                Limit="100"
                Unit="K"
                WarnThreshold="0.8" />
```

### Units and decimals

`Unit` is shown after each amount. `Decimals` sets the digits after the decimal point. Use `Decimals="0"` for whole
counts such as seats.

```xml
<arc:UsageMeter Label="Seats" Limit="20" Decimals="0">
    <arc:UsageSegment Id="eng" Label="Engineering" Value="12" />
    <arc:UsageSegment Id="design" Label="Design" Value="6" />
    <arc:UsageSegment Id="sales" Label="Sales" Value="5" />
</arc:UsageMeter>
```

### Labels for the free and over amounts

`FreeLabel` names the remaining allowance in the legend, and `OverLabel` names the overage once the total is
past the limit.

```xml
<arc:UsageMeter Label="Projects"
                Limit="10"
                FreeLabel="Remaining"
                OverLabel="Above plan" />
```

### Status in code

Read `Status` for the current state (`Ok`, `Near` or `Over`), and `Total` for the sum of all categories.

```csharp
if (meter.Status == UsageMeterStatus.Over)
{
    ShowUpgradePrompt();
}
meter.ClearPin(); // clears a category pinned from the legend
```

## API reference

<!-- api: UsageMeter -->

<!-- api: UsageSegment -->

## Accessibility

- The meter is a group named by `Label`. The bar is an image whose name is a full text summary of every category,
  the total and the status.
- Status changes such as "Almost full" or a growing overage are announced politely.
- Each legend item is a toggle button named with its category and amount, for example "Media, 14.2 GB".
- The legend is one tab stop. Arrow keys move between items, Home and End go to the first and last item.
- Enter or Space pins the focused category, and Escape clears the pin.

## Motion

- The first time 40% of the meter is visible, the categories grow in from left to right, one after another.
- After that, each category slides to its new width when a value changes, and the numbers roll toward the new value.
- Past the limit, the overage hatch and the limit marker fade in.
- Hovering or focusing a category dims the others, and a press pins it.
- Reduced motion sets the values and the bar directly and swaps the text with a short fade.

## Related

- `Progress` when work moves toward completion and has no allowance.
- `BarChart` for categories without a limit, compared with each other.
- `Gauge` or `MetricCard` for one headline number.
