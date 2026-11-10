---
title: Activity rings
summary: Concentric rings for daily goals. Each ring fills on a spring, closes at its goal and keeps going round past it, with a legend whose numbers roll.
---

<Demo name="Preview" />

## When to use

`ActivityRings` shows a few daily goals at once as rings inside each other, outer ring first. A ring fills clockwise
from 12 o'clock and is closed when its goal is met; past the goal it keeps going round, the new lap lying over the first
with a shade under its head, so "more than done" is visible at a glance. Changes spring on from where the ring is, and the
legend beside the rings names each goal and rolls its value.

- **Daily targets**: steps, active minutes, hours standing, water, pages read.
- **Streak and habit screens**: a row of small rings shows a week at a glance.
- **Quotas with stretch**: a sales or learning goal that is often beaten, where the extra matters.

## When not to use

- **Exact comparisons**: rings are read by feel; use a bar chart when people compare values.
- **More than four goals**: inner rings get small and hard to tell apart. Use a list of progress bars.
- **A single value**: a plain progress ring or `Gauge` says it more simply.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ActivityRings Label="Today's goals">
        <arc:ActivityRing Label="Move" Value="380" Goal="520" Unit="kcal" />
        <arc:ActivityRing Label="Exercise" Value="22" Goal="30" Unit="min" />
        <arc:ActivityRing Label="Stand" Value="9" Goal="12" Unit="hr" />
    </arc:ActivityRings>
</UserControl>
```

`ActivityRings` is in the Special package; register it with `UseArcSpecial()` next to `UseArcControls()`. Put one
`ActivityRing` per goal in it. A ring is full when `Value` reaches `Goal`, and `Progress` says how far round it is (1.4
is a full ring and 40 percent more). A ring without a `Brush` takes the theme's series colors in order.

### Past the goal

A ring keeps going round past its goal, up to four laps. The lap on top casts a shade a little ahead of its head, so the
overlap reads even when the colors are the same.

<Demo name="Overflow" />

### Small rings without a legend

Set `IsLegendVisible` to false and give the rings a small `Diameter` and `RingThickness` for a compact view, such as a
week of days. `GoalReached` is raised when a ring's value reaches its goal after the rings were shown, and `Replay()`
fills the rings from empty again.

<Demo name="Week" />

### Best practices

**Do**

- Keep to two, three or four goals, with short names such as "Move".
- Give `Label` a name for the whole view, like "Today's goals": it is what assistive technology reads first.
- Use `Unit` so the legend reads "380 / 520 kcal".

**Avoid**

- Rings for values that have no natural goal.
- Colors that are close to each other: neighbouring rings must stay easy to tell apart.

## API reference

<!-- api: ActivityRings -->

<!-- api: ActivityRing -->

## Accessibility

- The rings are one element named by `Label`. Its help text reads every goal: "Move: 380 of 520 kcal, 73 percent."
- The legend rows are drawn for the eye only and are not read one by one.
- When a ring reaches its goal, a polite status line says so, for example "Move goal reached" (`GoalReachedText`).
- The rings are not interactive and take no focus.

## Motion

- On first view the rings fill from empty one after another, outer ring first, 90 ms apart, on a spring that settles in
  about 0.9 s with a small bounce. The legend's numbers roll up at the same time.
- A later change springs on from where the ring is, keeping its speed when it changes again mid-way.
- Frames run only while a spring moves; the arc is updated in place and a frame allocates nothing.

| Change | Full | Reduced | Off |
|---|---|---|---|
| First view | Rings fill one after another | Values show at once | Values show at once |
| New value | Springs to the new value | Jumps | Jumps |
| Legend number | Rolls | Jumps | Jumps |

## Responsive behavior

- The rings take `Diameter` when there is room and shrink to fit a smaller space; the ring thickness stays the same.
- Below 340 px (`SideLegendMinWidth`) the legend moves under the rings; above it sits beside them.
- The legend's names end with an ellipsis rather than wrapping.

## Related

- `Gauge` and `DonutChart` show one value or shares of a whole.
- `UsageMeter` is a bar for a quota with a limit.

| Component | Choose it when |
|---|---|
| `ActivityRings` | A few daily goals, where going past the goal counts |
| `Gauge` | One value on a scale |
| `UsageMeter` | One quota with a hard limit |
