---
title: AI insight card
summary: A dashboard card where an assistant explains why a metric moved: the change, the chart with the stretch it means, likely causes you can select, and one suggested next step.
---

<Demo name="Preview" />

## When to use

`AiInsightCard` sits on a dashboard next to the numbers it talks about. It answers the question people ask when a metric
jumps or drops: what moved, why, and what to do now. The card shows the metric's value and its change, the chart the
explanation refers to with the changed stretch in a band, a short written summary, the likely causes ranked by how much of
the change each explains, and one suggested next step with a button. Selecting a cause moves the band to the days that
cause covers, so the words and the chart always point at the same thing.

- **Anomalies on a dashboard.** A drop in active users, a spike in cost, a fall in conversion: the card turns the alert into
  an explanation someone can act on.
- **Weekly reviews.** One card per metric that moved, written by your analysis job and read in the meeting.
- **Monitoring with a human in the loop.** The suggestion's button starts the fix (a rollback, a ticket), so the person
  decides and the system does the work.

## When not to use

- **Plain numbers.** When nothing needs explaining, use `MetricCard` or a chart on its own.
- **Long reports.** The card holds two or three sentences and a few causes. Link to a full report for more.
- **Live chat about data.** For questions and follow-ups, use the `AI chat` or `AI side panel` blocks.

## Usage

The block derives from Avalonia's `TemplatedControl`. Without any data it shows an invented sample
(`AiInsightCardSamples`); set the properties to show your own.

```xml
<arc:AiInsightCard Title="Weekly active users"
                   Value="18,420"
                   Change="-12.4"
                   PeriodText="vs. the 14 days before"
                   Summary="Active users fell 12.4% over the last five days, almost all of it on Android."
                   NextStep="Roll Android back to 4.1.3 for magic-link sign-ins." />
```

```csharp
card.Points = values;               // oldest first
card.PointLabels = days;            // "Nov 4", one per point
card.ComparisonPoints = lastPeriod; // dashed line, or null
card.FocusRange = new AiInsightRange(9, 13);
card.Causes =
[
    new() { Title = "Android 4.2 release", Detail = "Sign-ins on Android fell 31%.", Share = 0.64, Range = new(9, 13) },
    new() { Title = "Regional holiday", Share = 0.09, Range = new(9, 10) },
];
card.ActionInvoked += (_, e) => rollouts.Rollback("android", e.NextStep);
```

### Anatomy

- **Eyebrow and thumbs**: a small "AI insight" label and thumbs up and down (`Feedback`, two-way; pressing the same thumb
  again takes the answer back).
- **Metric**: `Title`, `Value`, the change pill from `Change` and `PeriodText`. `IsIncreaseGood` decides the tone: a rise is
  green by default; set it to false for costs and error rates.
- **Readout and chart**: the line of `Points` over a soft area, the dashed `ComparisonPoints`, and a band over `FocusRange`
  (or the selected cause's `Range`) inside which the line takes the change's color. The readout above names the picked
  point, or the last one.
- **Summary, causes, next step**: `Summary`; `Causes` with a share bar and percentage each; `NextStep` with the button
  (`IsActionVisible`, `ActionCommand`, `ActionInvoked`).
- **Footnote**: `Footnote`, for where the data came from and when the insight was written.

### Another metric

<Demo name="CostRise" />

### While it is written

Set `IsAnalyzing` while your job writes the insight: the text gives way to a shimmering line and placeholder lines, and the
chart stays. When it turns false again the summary is announced.

<Demo name="Analyzing" />

### Words

Every word is in `Labels` (`AiInsightCardLabels`): the headings, the button, the analyzing line, the screen reader names
and the formats of the change, the shares and the readout. `ValueFormatter` formats the values in the readout.

## API reference

<!-- api: AiInsightCard -->

<!-- api: AiInsightCause -->

<!-- api: AiInsightRange -->

<!-- api: AiInsightCardLabels -->

## Accessibility

- The card is a named region; the metric's name is a level 2 heading and the section names are level 3 headings.
- The chart is one tab stop, read as an image named after the metric, with the first and last values as its help text.
  **Left** and **Right** pick points, **Home** and **End** jump to the ends and **Escape** goes back to the last point;
  each point picked by keyboard is read out ("Nov 4: 17,980, before 20,110"). Hovering with the pointer is silent.
- Each cause is a toggle named with its title and share ("Android 4.2 release, 64% of the change") and described by its
  evidence. **Up** and **Down** move between causes, **Space** or **Enter** selects one.
- The thumbs are named "Helpful" and "Not helpful" and report "selected"; an answer is acknowledged politely.
- The analyzing line and the finished summary are announced politely.

## Motion

- The line draws in from the left the first time the card shows (480 ms, standard ease).
- Selecting a cause glides the band to its stretch on a snappy spring; the colors inside the band follow it.
- Rows and thumbs fade their colors in 160 ms; the change pill crossfades its tone.
- Reduced motion shows the whole line at once and moves the band without a spring. Off applies every change at once.

## Responsive behavior

- The card is at most 640 wide and fills narrower containers. At 420 or less (`:compact`) its padding tightens from 20 to
  16.
- The value, the change pill and the period wrap onto a second line when they do not fit.
- The chart keeps its height and redraws to the new width; the share bars keep their size and the titles shorten with an
  ellipsis.
- Nothing depends on hover: tapping a cause selects it and tapping the chart picks a point.

## Related

- `MetricCard` and `LineChart` for numbers and charts without an explanation.
- `AI answer` for a longer, cited answer to one question.
- `Metrics dashboard` and `KPI drilldown`, the Analytics blocks this card sits beside.
