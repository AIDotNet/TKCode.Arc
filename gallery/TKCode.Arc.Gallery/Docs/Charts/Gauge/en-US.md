---
title: Gauge
summary: Show a value against a known range.
---

<Demo name="Thresholds" />

## When to use

- One value against a known range, such as disk usage out of a quota or a health score.
- A value that has states, where threshold bands name the state, such as Healthy, Filling up and Critical.
- A reading that fits a dashboard tile and needs a percentage and a short detail line.

## When not to use

- Progress of a task that is running or finishing: use `Progress`.
- A plan or quota with a limit that the user is consuming: use `UsageMeter`.
- A value that changes over time: use `LineChart` or `Sparkline`.

## Usage

`Value` is the reading. `Minimum` and `Maximum` set the range, which is 0 to 100 by default. `Detail` adds a line
under the reading, such as the amount in absolute units.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Gauge Label="Disk usage"
               Value="72"
               Detail="360 of 500 GB">
        <arc:Gauge.Thresholds>
            <arc:GaugeThreshold From="0" Tone="Success" Label="Healthy" />
            <arc:GaugeThreshold From="60" Tone="Warning" Label="Filling up" />
            <arc:GaugeThreshold From="85" Tone="Danger" Label="Critical" />
        </arc:Gauge.Thresholds>
    </arc:Gauge>
</UserControl>
```

### Thresholds

A threshold starts at `From` and holds until the next one. The highest band the value reaches sets the color and shows
its `Label`. The band is chosen by the value on screen, so the label changes as the count crosses a band.

```csharp
disk.Thresholds.Add(new GaugeThreshold(0, GaugeTone.Success, "Healthy"));
disk.Thresholds.Add(new GaugeThreshold(60, GaugeTone.Warning, "Filling up"));
disk.Thresholds.Add(new GaugeThreshold(85, GaugeTone.Danger, "Critical"));
```

### Updating the reading

Set `Value` and `Detail` together, so the number and its detail stay in step.

```csharp
disk.Value = Math.Round(used / 5.0);
disk.Detail = $"{used} of 500 GB";
```

### Custom range

`Minimum` and `Maximum` map any range onto the ring. The number reads as a percentage of that range.

<Demo name="CustomRange" />

### Tone without thresholds

Without thresholds the ring uses `Tone`: `Accent`, `Success`, `Warning` or `Danger`.

<Demo name="Tones" />

## API reference

<!-- api: Gauge -->

<!-- api: GaugeThreshold -->

## Accessibility

- `Label` names the gauge, and the value is exposed as a read-only range with the minimum and the maximum.
- The value text includes the current band label, so the state is read with the number.
- States are named in text by the threshold labels. Color is never the only sign of a state.
- The gauge is read only and defines no keyboard interaction.
- The ring and the animated reading are hidden from assistive technology.

## Motion

- Once, when the gauge is half in view, the ring fills to its value on a smooth spring without overshoot.
- A new value moves the ring to its new length, and the percentage counts to the new number at the same time.
- When the count crosses a threshold, the color and the band label switch at that moment, following the count.
- Reduced motion puts the ring and the number at the target value at once. Off behaves the same as Reduced.

## Related

- `Progress` for the completion of a task.
- `UsageMeter` for a quota that the user consumes over a period.
- `DonutChart` for parts of a whole rather than one value against a range.
- `MetricCard` for a single value with its change and a trend.
