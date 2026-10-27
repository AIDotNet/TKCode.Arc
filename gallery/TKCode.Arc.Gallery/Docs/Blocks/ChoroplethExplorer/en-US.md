---
title: Choropleth explorer
summary: A map shaded by a metric in quantile steps with a linked ranked list; choose a region to fly into it and rank its counties, switch the metric to sweep new colours across the map, or change the country.
---

<Demo name="Preview" />

## When to use

`ChoroplethExplorer` answers two questions on one screen: where a number is high or low, and why a region has the value it
has. Every region of an area is shaded by the chosen metric, the same regions are ranked beside the map, and choosing one
flies the camera into it to show the next level down, re-binned and re-ranked.

- **Revenue, customers or usage by geography.** States, provinces or regions shaded by a total, with the leaders and the
  long tail in the list.
- **Change over a period.** A diverging metric splits at zero: declines in one hue, gains in another, so a growth map reads
  at a glance.
- **Coverage of a market.** A share per region (signed accounts out of target accounts) with the regions that have no data
  shown as such once you zoom in.

## When not to use

- **Exact comparisons.** Colour steps group values; for precise differences between a few regions use a bar chart or a
  table. The ranked list helps, but it is not the main reading.
- **Non-geographic categories.** Shading only makes sense when position carries meaning. Use a treemap or a ranked bar list
  for product lines or teams.
- **Point data.** Stores, offices or events belong on a map with markers, not on shaded areas.
- **A map you cannot ship.** The block draws outlines you provide; it contains no map of its own.

## Usage

The block derives from Avalonia's `TemplatedControl`. You give it one or more `ChoroplethArea`s (countries), each with its
`ChoroplethRegion`s, and the `ChoroplethMetric`s to shade by. A region brings its outline as SVG path data in the area's view
box (975 × 610 by default), its bounding box (optional: it saves parsing to place the camera), a value per metric and,
optionally, its own subregions in the same view box. The outlines are parsed once, when they are first drawn.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ChoroplethExplorer Description="Fiscal year ending March 31, 2027">
        <arc:ChoroplethExplorer.Metrics>
            <arc:ChoroplethMetric Key="revenue" Label="Revenue"
                                  HeadlineLabel="Annual recurring revenue"
                                  LegendCaption="Shaded by annual recurring revenue" />
            <arc:ChoroplethMetric Key="growth" Label="Growth" Format="Change" Scale="Diverging"
                                  Aggregate="WeightedAverage" />
        </arc:ChoroplethExplorer.Metrics>
        <arc:ChoroplethArea Key="us" Name="United States" RegionNoun="states" RegionNounSingular="state"
                            SubregionNoun="counties" SubregionNounSingular="county" />
    </arc:ChoroplethExplorer>
</UserControl>
```

```csharp
var california = new ChoroplethRegion("CA", "California", pathData, new Rect(18.7, 160, 143.9, 247));
california.SetMetricValue("revenue", 9_400_000);
california.SetMetricValue("growth", 4.2);   // percent units: +4.2%
area.Regions.Add(california);
```

The demos on this page read public-domain outlines (US Census Bureau cartographic boundary files for the United States,
Natural Earth admin-1 for the other countries) from text files that ship with the gallery; every value is invented.

### Anatomy

- **Header**: the title (`Title`, the area's `Title`, or "Revenue by state" from `ChoroplethLabels.TitleFormat`), the
  `Description`, the country switch (only with more than one area, each option with its region count) and the metric switch.
- **Summary**: the area's or the opened region's name, the headline number (the sum, or the weighted average of the
  regions, or the opened region's own value) and the metric's `HeadlineLabel`. While zoomed, a button leads back out.
- **Map**: every region shaded in its step; pointing outlines a region and shows a card with its rank, every metric
  (the current one stands out) and what choosing it does.
- **Legend**: one step per bin with the bin edges between them, a no-data step when subregions lack values, and a caption
  that names the step under the pointer ("10 states $120K to $230K").
- **Ranked list**: rank, name, value and a bar per region; a chevron marks regions that open into subregions. Pointing at a
  row outlines its region, and pointing at the map finds the row.

### Bins and colours

A sequential metric is cut into `BinCount` (5) steps at the quantiles of the values in view; a diverging metric is split at
zero first and each side is cut at its own quantiles, with the steps shared in proportion to how many values fall on each
side. Every edge is rounded to two significant digits, and a value equal to an edge belongs to the step above it. The bins
are recomputed for each level: after zooming in, the counties have steps of their own.

The positive hue is the accent, or a blue when the accent is grey; the negative hue is an amber opposite it; regions without
a value use the border colour. Set `PositiveColor`, `NegativeColor` and `NoDataColor` to use your own.

<Demo name="CustomLook" />

### Zooming in

Choosing a region (on the map or in the list) sets `OpenRegionKey`, raises `RegionOpened` and flies the camera so the region
fills 80% of the map (never more than 12×). The other regions stay as faded context and can be chosen to fly straight to
them. With subregions the region gives way to them, the legend and the list re-bin and re-rank, and choosing a subregion
pins it (`PinnedSubregionKey`, `SubregionPinned`). Without subregions the list says there is no finer detail and offers the
way back. `ZoomTo(key)` and `ZoomOut()` do the same from code.

<Demo name="Events" />

### One area, a diverging metric

With a single area the country switch goes away. `SelectedMetricKey` picks the metric to start with; here a growth metric
that splits at zero.

<Demo name="SingleArea" />

### Card look and narrow containers

`IsCardChrome` draws the block as a bordered card. The block reads its own width: from 820 the list stands beside the map
and is exactly as tall as the map column; below that it goes under the map (400 high, 360 below 560), the controls move under
the title, and below 560 the padding tightens, the headline shrinks and the legend fills the width.

<Demo name="Card" />

### Words

Every word besides the data comes from `Labels`, a `ChoroplethLabels` with English defaults: the counts ("51 states", with
the area's singular and plural nouns), "Ranked by revenue", the legend's step names, the hover card's hints, the back
buttons, the empty panel and every announcement. Metrics bring their own `Label`, `Noun`, `HeadlineLabel`, `LegendCaption`,
`CurrencySymbol`, and optional `Formatter` and `TickFormatter`.

## API reference

<!-- api: ChoroplethExplorer -->

<!-- api: ChoroplethArea -->

<!-- api: ChoroplethRegion -->

<!-- api: ChoroplethMetric -->

<!-- api: ChoroplethLabels -->

<!-- api: ChoroplethValueFormat -->

<!-- api: ChoroplethScale -->

<!-- api: ChoroplethAggregate -->

<!-- api: ChoroplethRegionOpenedEventArgs -->

<!-- api: ChoroplethSubregionPinnedEventArgs -->

<!-- api: ChoroplethSelectionChangedEventArgs -->

## Accessibility

- **Tab** reaches the country switch, the metric switch (one stop; the arrows move the selection), the back button while
  zoomed, each legend step, and the list (one stop, on the last row you were on).
- In the list, **↑** and **↓** move one row and stop at the ends, **Home** and **End** jump to the first and last row; the
  focused row is outlined on the map and scrolled into view.
- **Enter** or **Space** on a region's row zooms in and moves the focus to the first row of its subregions (or to the back
  button when there are none); on a subregion's row it pins or releases it.
- **Enter** or **Space** on a legend step pins it; again releases it.
- **Escape** releases a pinned legend step first, then zooms back out and returns the focus to the region's row.
- The map is an image named "Map of United States shaded by revenue." with help text pointing to the list; the list carries
  the same data and is the way to explore by keyboard or screen reader.
- Each row is named by its rank, name and value ("1st, California, $9.8M. Open 58 counties"); negative values are read as
  "minus 4.8%". Subregion rows are toggle buttons that report whether they are pinned.
- Each legend step is a toggle button named by its count and range ("10 states $120K to $230K").
- A polite live region announces zooming in ("California, 58 counties ranked by revenue. Press Escape to zoom out."),
  zooming out, a new metric with its leader, a new country, and a pinned subregion with its value and rank.
- Values carry their sign and a true minus; the direction never depends on colour alone.
- Focus rings are 2px, outside the element, and only show for the keyboard.

## Motion

- **Metric switch**: each region's colour moves over 480 ms after a delay that grows from west to east (its centre's x over
  the map's width × 260 ms), so the new shading sweeps across the map in under 750 ms. The rows slide to their new ranks on a
  critically damped spring and every bar grows or shrinks over 580 ms; the texts cross-fade in 240 ms; the headline jumps.
- **Zooming in or out**: the camera's scale and position share one critically damped spring (about 800 ms, and it can turn
  around mid-flight). The other regions fade to 26% in 240 ms; the subregions fade in over 480 ms from 140 ms, so they
  appear while the camera still flies; leaving, they fade out in 160 ms. The headline counts to the new value in 480 ms.
- **Pointing**: the hover card fades in 160 ms and follows the pointer; the list glides to centre the region's row. On the
  legend, the lit step grows to 1.6× its height and the other regions fade to 14% and the other rows to 32% in 240 ms.
- **Changing the country**: the old map fades out in 180 ms and the new one fades in from west to east; the list fades out
  and back in.
- Reduced motion keeps only the text cross-fades: colours, camera, rows, bars and the hover card change at once. Off applies
  every change at once.

## Related

- `SegmentedControl` and `MorphSelect`, the switches in the header.
- `Metric explorer` and `Revenue explorer`, the other Analytics blocks that break a number down.
- `Revenue globe` for a 3D view of revenue by location.
