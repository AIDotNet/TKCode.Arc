---
title: Stats band
summary: Headline numbers that count up in view, each with a tiny visual that proves it and a context line on hover, plain or in a hairline grid.
---

<Demo name="Preview" />

## When to use

A few specific numbers can back a claim better than a paragraph. `StatsBand` sets three or four of them side by side, under
a hero or between two sections, and gives each one a small picture that proves it.

- **A marketing page that backs a claim**: uptime, speed, how many people use the product.
- **An about or investor page**: headline numbers with a short note on each.

## When not to use

- **Live product metrics with trends**: use `MetricCard` or `MetricsDashboard`.
- **More than four numbers**: split them over two bands, or use a table.
- **Numbers you cannot stand behind**: round, unsourced numbers read as made up.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:StatsBand Layout="Divided" Title="Quick and steady">
        <arc:StatsBand.Stats>
            <arc:StatsBandStat Value="38" Suffix="ms" Label="Median response" Detail="At the edge" />
            <arc:StatsBandStat Value="99.98" Decimals="2" Suffix="%" Label="Uptime" Detail="Over 90 days" />
            <arc:StatsBandStat Value="2340000" Notation="Compact" Decimals="1" Label="Deploys" Detail="This year" />
        </arc:StatsBand.Stats>
    </arc:StatsBand>
</UserControl>
```

`StatsBand` derives from `TemplatedControl`. With an empty `Stats` list it shows sample numbers, so replace them before shipping.
`Layout` is `Plain` (the stats float on whitespace) or `Divided` (a hairline grid ruled above and below).

### A stat

| Property | What it does |
|---|---|
| `Value`, `Decimals` | The final number and its digits after the point |
| `Prefix`, `Suffix` | Text around the number. It is drawn smaller and muted, so put units here: %, ms, +, /5 |
| `Notation` | `Compact` writes 2.3M and counts inside that unit, 0.0M to 2.3M |
| `Label`, `Detail` | What it measures, and a short supporting line |
| `Context` | A line that replaces `Detail` while the stat is hovered or focused, such as "Up from 41%" |
| `Visual` | A small picture, below |

`Locale` sets the digit separators (`en-US` by default), and compact units are always K, M, B and T.

<Demo name="OwnNumbers" />

### Pictures

Each `Visual` is optional, but if any stat has one, the row is kept in every stat so that the numbers line up.

| Type | Data | Reads as |
|---|---|---|
| `StatsBandTrend` | `Values`, oldest first | A sparkline with a dot on its last value |
| `StatsBandUptime` | `Days`, in percent | One bar per day. A day with downtime is visibly shorter and inked |
| `StatsBandDistribution` | `Bins`, `Maximum`, `Marker` | A histogram with the bars below the marker inked |
| `StatsBandMap` | `Points`, as longitude and latitude | A dotted world with a dot lit per place, snapped to the nearest land |

Hovering or focusing a stat gives its picture the accent while the others stay neutral.

<Demo name="Numbers" />

### Best practices

**Do**

- Use specific numbers: 99.98% says more than "almost always".
- Pick a picture that proves the number, and a context line that compares it.
- Keep labels to a few words, so that the columns line up.

**Avoid**

- Mixing stats with and without context lines without a reason. People learn to hover from the first one.
- More than one band per page.

## API reference

<!-- api: StatsBand -->

<!-- api: StatsBandStat -->

<!-- api: StatsBandTrend -->

<!-- api: StatsBandUptime -->

<!-- api: StatsBandDistribution -->

<!-- api: StatsBandMap -->

## Accessibility

- The band is a region named by `Title`, or by `AccessibleLabel` ("Key numbers") without one.
- Each stat is named by its final value, its label, its detail and its context. The counting digits are not announced.
- A stat with a `Context` takes focus, so that a keyboard or a tap can reveal the line. The pictures are decoration.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the stats that have a context line. Focus reveals the line |

## Motion

- The first time 40% of the band is in view, each number counts from zero with a strong ease out over `CountDuration` (1.6 s),
  and each stat rises 8 px and fades in. Both are 90 ms apart.
- Digits have one width, and the final value reserves its space, so nothing shifts while they count.
- Once a number has mostly landed, its picture draws in: lines and maps wipe in from the left over 1.1 s, bars grow from
  the baseline in a 420 ms sweep, and the trend's end dot settles last.
- In `Divided`, the top and bottom rules draw across in 900 ms, and the hairlines grow between the stats in 700 ms.
- Hovering crossfades the detail line to the context line in 240 ms, with 4 px of travel.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Entrance | Counts, rises and draws | Final values at once | Final values at once |
| Hover | Crossfade with travel, accent mix | Line swaps at once, accent mix in 240 ms | Everything at once |

Call `Replay` to play the entrance again.

## Responsive behavior

- `StatsBand` follows its own width, not the window's. Numbers scale between 40 and 52 px.
- One row of up to four stats. At 800 px or less (`:stacked`) they take two columns, or one for a band of three, so no stat is left over.
- At 520 px or less (`:phone`) there is one stat per row with its picture beside the number.
- Hairlines follow the grid: vertical between neighbours, horizontal between rows.
- Touch: a tap focuses a stat, which reveals its context line.

## Related

| Component | Choose it when |
|---|---|
| `StatsBand` | A few proven numbers on a marketing page |
| `MetricCard` | One number with context inside a product |
| `AnimatedCounter` | One total that changes |
| `MetricsDashboard` | Live metrics with charts |
