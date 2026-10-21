---
title: Metric card
summary: A compact summary for a number that needs context.
---

<Demo name="Default" />

## When to use

`MetricCard` is the tile that answers "how are we doing" on a dashboard. It holds one number, the label that names
it, the comparison that gives it meaning and an optional change chip, in a surface that sits in a row with other
metrics. A row of them is the top of most operational and business pages: the first thing a person reads before
they open a table or a chart.

- **KPI rows on a SaaS overview page**: monthly recurring revenue, active accounts and churn, each with "vs last
  month" as its context and a chip showing the change.
- **Uptime and service health** in an operations console: "Uptime 99.98%" with "Last 30 days", so the number is read
  against the period it covers.
- **Live counts on an order or support dashboard**: open orders or the ticket backlog, where the digits roll as new
  work arrives and the context explains the time window.
- **Latency and throughput** in a developer tool or an API portal: a p95 value in milliseconds, compared with
  yesterday, with a trend line when the direction over the last few points matters.
- **Usage and billing summaries** in a fintech or billing area: a month-to-date total or an average balance, shown
  next to the period it refers to.

## When not to use

- **The trend matters more than the latest value**, such as a week of daily sign-ups read as a shape: use `Sparkline`
  or `LineChart`, which give the whole series room.
- **A bare number inside running text or a heading**, with no frame and no context: use `AnimatedCounter`.
- **A quota or a progress toward a limit**, such as seats used out of 50: use `UsageMeter`, which draws the fraction.
- **A card with media, an owner and actions**: use `Card`. A metric card holds a number, not a tile of content.
- **Many values to compare by attributes**, such as revenue per region and per plan: use `SortableDataTable` or
  `DataGrid`, where each row can carry its own columns.
- **A time to reach a deadline**, such as a renewal or a trial end: use `Countdown`, which counts down on its own.

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

`MetricCard` derives from Avalonia's `TemplatedControl`. It is a display control: it is not focusable, has no
press behavior and raises no events. Its number is an `AnimatedCounter` inside its template, so the rolling digits,
the decimals and the prefix and suffix are the counter's behavior, and the card adds the label, the chip and the
trend line around it. Set the value from code or a binding and the card updates in place.

### Anatomy

1. **Label** (`Label`): 14 px in the secondary text color on the top row, on the left. It truncates with an ellipsis
   when the card is narrow, and it is the card's accessible name.
2. **Change chip** (`Change`): a pill on the top row, on the right, with 8 px side and 3 px vertical padding. It
   holds the change text and, when `IsChangeGlyphVisible` is set, a 12 px trend glyph. It is hidden while `Change` is
   empty.
3. **Value** (`Value`, with `Prefix`, `Suffix` and `Decimals`): the number in the large counter, bottom-aligned. The
   `Decimals` default is 0. `Prefix` sits before the digits, such as "$", and `Suffix` after them, such as "%" or " ms".
4. **Trend line** (`Sparkline`): a 96 by 36 px line on the same row as the value, at its bottom edge. It appears only
   with two or more points.
5. **Context** (`Context`): 14 px in the muted text color, 16 px below the value row. It says what the number is
   compared with: "vs last week", "Last 30 days".
6. **Surface**: 24 px padding, a 1 px border on the surface color, the surface radius and the resting shadow. The top
   row sits 32 px above the value row.

### Value, prefix, suffix and decimals

The number is a `double`, so one property covers integers, percentages and averages. Choose the decimals for the
meaning of the number, not for the width of the card: a 99.98% uptime needs two decimals, an order count needs none.

```xml
<arc:MetricCard Label="Revenue this month"
                Value="48210"
                Prefix="$"
                Decimals="0"
                Context="vs last month"
                Change="+6.4%"
                Width="280" />
```

- Use `Prefix` for a currency symbol and `Suffix` for a unit that follows the number. Keep the unit short; a long
  suffix pushes the digits to the left and makes the value row feel unstable.
- Use one decimal place for a percentage that moves by fractions ("1.8%") and two for an availability figure that
  people compare to a service-level target ("99.98%").
- Do not put the unit in `Label` when the value carries it. "Latency" with a suffix of " ms" reads once; "Latency (ms)"
  with a suffix of " ms" reads twice.

### Change chip

A `Change` that starts with "+" is an up change, and one that starts with "-" or "−" is a down change. Any other
text is a neutral change. The direction is read back from `ChangeDirection`, and the chip shows its color from it.

| `Change` text | `ChangeDirection` | Chip look |
|---|---|---|
| "+12%" | `Up` | Success tint behind success-colored text |
| "−8%" or "-8%" | `Down` | Danger tint behind danger-colored text |
| "flat", "0%" or empty text | `None` | Outlined neutral chip |

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

Write the sign in the text. The color is a second cue, and the sign is the first, so the direction is clear to people
who do not see color differences and to screen readers. `IsChangeGlyphVisible` adds a trend arrow to a signed chip;
it is off by default.

The chip colors the direction, not whether the direction is good. A drop in latency is an improvement, but a "−8%"
chip still uses the danger tint. Explain the meaning in `Context` ("lower is better"), or show the change as a plain
neutral value when the good direction is not the same as the sign. Set `Change` to `null` to hide the chip; it fades
out.

### Trend line

`Sparkline` takes a list of values and draws a small trend line beside the number. The line draws itself in the first
time it comes into view, and it redraws when the data changes. It needs at least two points: with one value or none,
the line is hidden. The line takes the color of the change direction: accent when there is no change, success for an
up change and danger for a down change.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"
             xmlns:generic="using:System.Collections.Generic">
    <arc:MetricCard Label="Orders" Value="1204" Context="vs last week" Change="+12%" Width="280">
        <arc:MetricCard.Sparkline>
            <generic:List x:TypeArguments="x:Double">
                <x:Double>820</x:Double>
                <x:Double>910</x:Double>
                <x:Double>1204</x:Double>
            </generic:List>
        </arc:MetricCard.Sparkline>
    </arc:MetricCard>
</UserControl>
```

```csharp
orders.Sparkline = new List<double> { 820, 910, 870, 990, 1040, 1010, 1204 };
```

Pass a short series, such as the last seven or thirty daily values, so the line shows the shape of the recent
movement. A series of hundreds of points makes the 96 px box a noisy scribble; use `LineChart` for that.

### Binding to a view model

A metrics row binds each card to a property on the dashboard view model. With CommunityToolkit.Mvvm, the view model
raises property changes and the cards update in place, with the digits rolling to the new value.

```csharp
public partial class OverviewViewModel : ObservableObject
{
    [ObservableProperty]
    private double _orderCount;

    [ObservableProperty]
    private string? _orderContext;

    [ObservableProperty]
    private string? _orderChange;

    [ObservableProperty]
    private IList<double>? _orderTrend;

    public void ShowOrders(int openCount, int previousCount, IList<double> dailyCounts)
    {
        OrderCount = openCount;
        OrderContext = "vs last week";
        OrderChange = openCount >= previousCount
            ? $"+{openCount - previousCount}"
            : $"−{previousCount - openCount}";
        OrderTrend = dailyCounts;
    }
}
```

```xml
<arc:MetricCard Label="Orders"
                Value="{Binding OrderCount}"
                Context="{Binding OrderContext}"
                Change="{Binding OrderChange}"
                Sparkline="{Binding OrderTrend}"
                Width="280" />
```

- Bind `Value` one way. The card never writes it back.
- Update `Context`, `Change` and `Value` together in one method (`ShowOrders` above), so the card shows one consistent state.
- Compiled bindings need `x:DataType` on the view. The examples leave it out for brevity.

Each update rolls the digits to the new value and the copy rolls in from the direction the number moved, so an update
arriving every few seconds keeps the row in constant motion. For a live feed, update the view model on a fixed
interval, such as once every 30 seconds.

### Narrow windows

In a window that is 380 px wide or less, the card uses tighter padding (16 px), the top row sits 24 px above the
value row, and the change chip moves under the label with 12 px of space. The rule reads the window width, not the card
width, so every card in the window switches to the narrow layout together. No code is needed for this.

### Example: monthly overview row

An overview page for a SaaS admin console at the start of the month. The three metrics answer the questions people
ask first: how much revenue came in, how many accounts are retained and how many are active. Each card names its
period in the context, and the change chip shows the direction in the text. The churn card shows a down change in the
danger tint. A drop in churn is good news, so its context says "lower is better", which is the rule from the chip
section: the color follows the sign, and the words carry the meaning.

![Three metric cards for October: revenue, churn and active accounts](images/MonthlyOverview.png)

```xml
<StackPanel Width="740" Spacing="16">
    <TextBlock Text="Overview, October 2026" FontSize="18" FontWeight="SemiBold" />
    <WrapPanel ItemSpacing="12" LineSpacing="12">
        <arc:MetricCard Label="Revenue"
                        Value="48210"
                        Prefix="$"
                        Decimals="0"
                        Context="vs last month"
                        Change="+6.4%"
                        Width="236"
                        VerticalAlignment="Top" />
        <arc:MetricCard Label="Churn"
                        Value="1.8"
                        Decimals="1"
                        Suffix="%"
                        Context="Lower is better"
                        Change="−0.3 pts"
                        Width="236"
                        VerticalAlignment="Top" />
        <arc:MetricCard Label="Active accounts"
                        Value="1284"
                        Context="Across 3 regions"
                        Change="+42"
                        Width="236"
                        VerticalAlignment="Top" />
    </WrapPanel>
</StackPanel>
```

### Content guidelines

- **Label**: a short noun phrase in sentence case that names the measure: "Revenue", "Uptime", "p95 latency". It is
  the accessible name, so it must make sense without the value.
- **Value and unit**: write the unit once, either in `Prefix` or `Suffix`, and do not repeat it in the label. Write
  numbers as digits.
- **Change**: a sign and a number with its unit: "+6.4%", "−0.3 pts", "+42". Use the minus sign "−" for a down
  change; the card reads both "−" and "-".
- **Context**: the period or the comparison, in lower case after the number: "vs last month", "Last 30 days". Say
  what good looks like when the sign alone does not tell it: "Lower is better".

| Instead of | Write |
|---|---|
| Revenue ($) | Revenue, with a `Prefix` of "$" |
| Up 6 percent! | +6.4% |
| Churn going down (good) | −0.3 pts, lower is better |
| Data from last month | vs last month |
| Latency (ms) with suffix " ms" | Latency with suffix " ms" |

### Best practices

**Do**

- Put the period in `Context` on every card, so the number is never read without its window.
- Keep the cards in a row at one width, so the values align and the eye can compare them.
- Explain a change whose good direction differs from its sign, in `Context`.
- Update the view model at a steady interval for live data. The card settles between updates, and the eye reads the
  change instead of the motion.

**Avoid**

- Showing more than four or five cards in one row. Beyond that, the numbers stop being the first thing a person reads.
- A decimal count that changes between cards in the same row, which makes the row look unfinished.
- Writing the change into `Context` ("+6.4% vs last month") when `Change` already carries it. The text repeats and the
  chip duplicates the sentence.
- Long `Value` text, such as a formatted string with words. The counter is for numbers; a word such as "Healthy"
  belongs in a status element.

### Common questions

**The chip shows a danger color for a good change. How do I fix that?** The chip colors by the sign of `Change`, not by
whether the change is good. Put the meaning in `Context` ("Lower is better") and keep the sign honest. The color
follows the sign by design, so the card does not need a second property to flip it.

**How do I show a change without the color?** Write the text without a sign (for example "Stable" or "No change"),
which makes the direction `None` and the chip neutral. The card shows the chip with an outline and no tint.

**Why does the number start at zero?** The first time the card scrolls into view, the counter rolls from zero to the
value. After that, each update rolls from the previous value.

**Why is the chip hidden when `Change` is empty?** An empty `Change` removes the chip so the top row shows only the
label. The chip fades out.

**Can a person edit the value or open the metric?** No. The card is display-only, takes no focus and raises no events.
To open a detail view, place the card in a control that your page owns and handles the press there.

## API reference

<!-- api: MetricCard -->

## Accessibility

- The card is a group named by `Label`. Its help text is the change and the context, such as "+12% vs last week",
  or the context alone when there is no change.
- The number is read by the counter. The change chip is plain text and states its direction with "+" or "−", so the
  direction does not depend on color.
- A value change is not announced by itself. If you need an announcement, set `AutomationProperties.LiveSetting` on
  the card or on a container around it.
- The card takes no keyboard focus. Tab skips it, so a row of metrics is not a row of stops.
- Give every card a `Label` that stands on its own. The label is what a screen reader reads first, so "Uptime" is
  better than "Metric 1" and better than the value alone.

## Motion

- The number rolls from zero the first time the card scrolls into view, then turns toward each new value.
- The label, context and chip text roll in from the side the number moved toward.
- The chip width adjusts to the new text on a spring, and the chip scales and fades in or out.
- The trend line draws in over 0.9s the first time it is shown, and cross-fades when the data changes.
- Reduced motion jumps the number to its new value and fades the text. The chip and trend line change without motion.
  Off applies every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Number changes | Rolls digit by digit, up when it grows, down when it shrinks | Jumps to the new value | Immediate |
| Label, context or chip text changes | Rolls in from the side the number moved toward | Instant fade | Immediate |
| Chip appears or disappears | Scales from 96% and fades, on a snappy spring | No motion | Immediate |
| Chip width | Springs to fit the new text | No motion | Immediate |
| Trend line first shown | Draws in over 0.9 s | Shown at once | Immediate |
| Trend line data changes | Cross-fades | No motion | Immediate |

## Related

- `AnimatedCounter` for a bare number that needs no card frame.
- `Sparkline` when the trend matters more than the latest value.
- `Card` for a group of content and actions around a number.
- `UsageMeter` for a value measured against a limit.

| Component | Choose it when |
|---|---|
| `AnimatedCounter` | The number sits in running text or a heading, without a card |
| `Sparkline` | The shape of the series is the message |
| `LineChart` | The series needs axes, several lines or a longer history |
| `UsageMeter` | The value is a fraction of a limit, such as seats used |
| `Card` | The tile holds media, an owner or actions beside the number |
| `Countdown` | The number is a time left until a deadline |
| `SortableDataTable` | The values are rows that need sorting and comparison by attribute |
