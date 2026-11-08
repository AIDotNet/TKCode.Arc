---
title: Radar chart
summary: Compare several profiles on one web of measures: swap a profile and its shape morphs, sweep the axes and each value reads out.
---

<Demo name="TeamSkills" />

## When to use

`RadarChart` shows how a few things balance across the same set of measures. Each profile is one closed shape, so the
reader sees at a glance where one is strong and another is thin, and sweeps the axes to read exact values.

- **Skill assessments**, such as interview panel scores for a few candidates or team capability reviews.
- **Product comparisons**, where three options are scored on battery, storage, price and other measures with their own
  scales.
- **Before and after reviews**, such as a service's reliability, cost and speed before and after a migration.
- **Balance checks**, where the shape itself is the message: a round shape is balanced, a spiked one is specialised.

The chart reads best with up to three profiles and five to eight axes.

## When not to use

- **More than about four profiles**: the shapes hide each other. Use `BarChart` with groups, or one small chart per profile.
- **Exact ranking on one measure**: lengths at different angles are hard to compare. Use `BarChart`.
- **Change over time**: use `LineChart`, where the x axis is time.
- **Parts of a whole**: use `DonutChart`. A radar's axes are separate measures and do not add up.
- **Measures with a natural order or that can go below zero**: the axes form a loop starting at zero in the centre, so a
  sequence or a signed value is misread. Use `SlopeChart` or `BarChart`.

## Usage

Give the chart axes and series, inline or bound. `Axes` and `Series` start as empty lists you can add to in XAML, and
accept any `IList` from a view model. An observable list is followed.

<Demo name="PolygonGrid" />

### Anatomy

1. **Web**: `Rings` rings (circles or polygons, see `GridShape`) and one spoke per axis.
2. **Axis labels**: at the end of each spoke, clockwise from the top in the order of `Axes`.
3. **Shapes**: one closed outline with a fill per series. Colors follow series order, the first from the accent color,
   and each series has its own line pattern.
4. **Markers**: on the active axis, one marker per profile.
5. **Tooltip**: beyond the axis label and always inside the chart, listing each profile with its line key, name and value.
6. **Legend**: each profile's line key and name, under the web.

### Axes and series

A `RadarAxis` has a `Key`, a `Label` and an optional `Maximum`. A `RadarSeries` has a `Key`, a `Label` and `Values`, one
`RadarValue` for each axis, with `Axis` holding the axis key.

| Type | Member | Description |
|---|---|---|
| `RadarAxis` | `Key` | Stable identity, used by values and `ActiveAxis` |
| `RadarAxis` | `Label` | Text at the end of the axis |
| `RadarAxis` | `Maximum` | The value at the outer ring of this axis; when null the chart's `Maximum` applies |
| `RadarSeries` | `Key` | Stable identity, used to match shapes between updates |
| `RadarSeries` | `Label` | Name in the legend and the tooltip |
| `RadarSeries` | `Values` | The `RadarValue` items |
| `RadarValue` | `Axis` | Key of the axis |
| `RadarValue` | `Value` | The value; NaN reads as missing |

From code, a series takes a dictionary keyed by axis:

```csharp
chart.Axes = [new RadarAxis("speed", "Speed"), new RadarAxis("cost", "Cost", 500), new RadarAxis("reach", "Reach")];
chart.Series = [new RadarSeries("north", "North", new Dictionary<string, double> { ["speed"] = 70, ["cost"] = 320, ["reach"] = 55 })];
```

### Scales, missing values and clamping

Each axis runs from 0 at the centre to its maximum at the outer ring: `RadarAxis.Maximum` when it is set, otherwise the
chart's `Maximum` (100 by default). A value above the maximum draws on the outer ring and a value below zero draws at the
centre; the tooltip still reads the value that was given. A missing or NaN value draws at the centre and reads as an em
dash in the tooltip.

### Several products with their own scales

Give each axis the `Maximum` that suits its measure and use `ValueFormatter`, which gets the value and its axis, to put
the right unit on each.

<Demo name="ProductCompare" />

### Swapping a profile

Replace `Series` to show another profile. A series that keeps its `Key` morphs into its new values. A series with a new
key takes over the shape of the series it replaced in that position, so swapping the second profile of a comparison
morphs it. A series with nowhere to come from grows out of the centre.

```csharp
chart.Series = [maya, selectedCandidate];
```

### Grid shape, rings and fill

`GridShape` draws circles (the default) or polygons with a corner on each axis, which suit charts with few axes. `Rings`
sets how many rings the web has and `FillOpacity` how strong each shape's fill is.

```xml
<arc:RadarChart Label="Coverage" GridShape="Polygon" Rings="5" FillOpacity="0.3" Series="{Binding Regions}" Axes="{Binding Measures}" />
```

### Reading an axis

`ActiveAxis` is the key of the axis being read, and it is two-way. `ActiveAxisChanged` reports each change with the `Key`
and the `Axis`, both null when the reading is cleared. A key that matches no axis shows no reading.

```csharp
chart.ActiveAxisChanged += (_, e) => readout.Text = e.Axis?.Label ?? "Sweep the axes to compare";
```

### Size and legend

`Size` is the largest diameter of the web, 320 by default. The web shrinks to fit its box, and the chart takes the width it
is given and centres the web. Set `IsLegendVisible` to false when the page names the profiles another way. Below three
axes or without series the chart shows `EmptyLabel`.

### Binding to a view model

```xml
<arc:RadarChart Label="Team skills"
                Axes="{Binding Skills}"
                Series="{Binding Members}"
                ActiveAxis="{Binding SelectedSkill, Mode=TwoWay}" />
```

- `Skills` and `Members` can be arrays, lists or observable collections of `RadarAxis` and `RadarSeries`.
- Changing a `RadarValue` in place is not observed; replace the series or the list to update the chart.

### Data shape and performance

Axis and series keys must be unique; a later duplicate is ignored. The chart draws itself in one control: during a morph,
geometry moves in place and nothing is laid out again, so keeping the chart on screen while data changes is cheap.

### Content guidelines

- **Label**: name what is compared, such as "Staff engineer panel scores".
- **Axis labels**: one or two short words. A longer label is trimmed, and below 360 wide it wraps to two lines.
- **Series labels**: short names; they share a tooltip row with the value.

### Best practices

**Do**

- Keep to three profiles and five to eight axes.
- Put related measures next to each other, since neighbours shape the outline.
- State the scale in the label or the formatter when it is not obvious.

**Avoid**

- Comparing areas: a shape's area depends on the axis order, so read the values.
- Mixing measures where higher is better with ones where lower is better.

### Common questions

**Why does my new profile not morph?** It morphs when the series it replaces is gone and the new one takes its position in
`Series`. If you insert it in another position, it grows from the centre.

**Can a value be negative?** It reads correctly, but the shape stops at the centre, since each axis starts from zero.

## API reference

<!-- api: RadarChart -->

<!-- api: RadarAxis -->

<!-- api: RadarSeries -->

<!-- api: RadarValue -->

## Accessibility

- The web is a focusable group. Its automation name is the chart's `Label` with the key instructions, and while an axis is
  being read it announces the axis and each profile's value, such as "Mentoring: Maya 64, Theo 83".
- With no reading, the help text summarises every profile's value on every axis.
- Profiles are told apart by name and line key as well as by color. Each legend entry is a tab stop named after the
  profile and its line pattern.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Focus the web and read the first axis, then move through the legend entries. A focused entry brings its shape forward |
| `Right` / `Down` | Read the next axis clockwise |
| `Left` / `Up` | Read the previous axis |
| `Home` / `End` | Read the first or the last axis |
| `Escape` | Clear the reading |

## Motion

- Swapping a profile morphs its shape on a gently bouncing spring.
- On first view the web unfolds from the centre.
- Markers and the tooltip glide between axes. Markers travel along each outline.
- Hovering or focusing a legend entry brings its shape forward and fades the others.
- Reduced motion swaps shapes and moves markers and the tooltip immediately, and the first view is a short fade.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Profile replaced | Morph on a bouncing spring | Immediate | Immediate |
| First view | Unfolds from the centre | Short fade | Immediate |
| Active axis changes | Markers and tooltip glide | Immediate | Immediate |

## Responsive behavior

- The web shrinks to fit the width and the height it is given, and never grows past `Size`.
- Below 360 wide the label ring tightens and axis labels wrap to two lines.
- The tooltip stays inside the chart at every width; when there is no room beyond the label, it moves beside it.
- A touch press reads the axis under the finger, and a drag sweeps the others. The legend wraps onto more lines.

## Related

| Component | Choose it when |
|---|---|
| `BarChart` | Values on one measure should be ranked exactly |
| `LineChart` | The x axis is time |
| `SlopeChart` | Two moments should be compared across several items |
| `DonutChart` | The values are parts of one whole |
| `Gauge` | One value against its range is the whole story |
