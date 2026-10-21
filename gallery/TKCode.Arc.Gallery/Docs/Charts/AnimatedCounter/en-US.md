---
title: Animated counter
summary: Give changing totals a clear sense of movement.
---

<Demo name="RollingDigits" />

## When to use

`AnimatedCounter` shows one number on its own, large enough to read at a glance, and rolls its digits to the new value
when the number changes. It works like the odometer of a car: only the digit columns that changed turn, so a reader can
see that a figure moved and by roughly how much, without the page flashing or the number jumping to a new width.

- **Headline totals on a billing overview**: monthly recurring revenue or the number of active subscriptions at the top
  of a SaaS admin console. The roll tells the finance team that the figure refreshed, even when they are not looking at
  the chart below it.
- **Live counters in an operations console**: open incidents, queued jobs or signed-in sessions that change while the
  page is open. Set the new value when the data arrives, and the digits move from the old value to the new one.
- **Statistics that count up on first view**: a landing page or a quarterly report where "128,400 teams" should roll up
  from zero as the reader scrolls to it, with `AnimateOnView` doing the work.
- **Balances and prices in fintech and checkout**: a wallet balance that changes after a payment, or a running total in
  a cart. Fix `Decimals` so the number keeps its width as it changes and the layout does not shift.
- **Rates and percentages in a growth dashboard**: a conversion rate or a retention figure with `Suffix="%"`, shown
  beside the total it belongs to, so the reader sees the headline and the rate together.

## When not to use

- **A number that needs a card, a label and a change indicator**: use `MetricCard`, which places a counter like this one
  inside a card with its trend.
- **Text that changes instead of a number**: use `TextMorph`, which animates words and phrases rather than digit columns.
- **The headline of a chart**: `BarChart`, `LineChart` and the other chart controls update their own headline values, so
  a separate counter next to a chart repeats the same number.
- **Many numbers in rows**: use `SortableDataTable`. Each counter animates its own digit columns, which suits a few
  headline numbers, not a column of values that people scan and sort.
- **A value that a person types or edits**: use `NumberField` or `MoneyInput`. A counter only displays a number; it has no
  input, no parsing and no validation.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:AnimatedCounter Label="Revenue"
                         Prefix="$"
                         Value="{Binding Revenue}" />
</UserControl>
```

`AnimatedCounter` derives from Avalonia's `TemplatedControl`. Its template is a label above a `RollingDigits` primitive
from the Arc core, which does the digit work. The counter adds the label, the affixes and the reveal on first view, and
its theme sets the font, size, weight and foreground of the number, so you rarely need to set those yourself. Because
it is a template, you can restyle the counter with a `ControlTheme` in the same way as any other Arc control.

`Value` takes the raw number, not a formatted string. The counter formats it with `Decimals`, `CultureName`, `Prefix`
and `Suffix`, so the same value can show "$48,250" with en-US or "48.250" with de-DE, without changing the data.

### Anatomy

1. **Label** (`Label`): a line above the number, in the muted text color, in the extra-small type size with regular
   weight. It sits 8 px above the digits. It is hidden when `Label` is null, and it swaps with a short blur when its text
   changes.
2. **Digits** (`Value`): the number itself, drawn by `RollingDigits` in the display font family at the 3XL type size with
   medium weight, in the foreground color. The counter is left aligned by default.
3. **Prefix** (`Prefix`): text before the digits, in the same style, such as a currency sign. It is part of the rolling
   text, so it moves with the number and keeps its position when the width changes.
4. **Suffix** (`Suffix`): text after the digits, such as a percent sign or a unit. Like the prefix, it is part of the
   rolling text.

The counter has no border, background or padding of its own. Put it in a card, a row or a header and let the layout
supply the surface, so it fits the page it sits on.

### Decimals and suffix

`Decimals` fixes the number of digits after the decimal point (default 0). Negative values count as 0. Fix it to the
precision the reader needs: a conversion rate shows two decimals on every update, so the width stays the same and the
digits roll in place.

```xml
<arc:AnimatedCounter Label="Conversion" Value="{Binding Conversion}" Decimals="2" Suffix="%" />
```

Use `Suffix` for a unit or a symbol that follows the number, and keep it short. A longer phrase belongs in the label,
where it can wrap and where it does not roll with the digits.

### Updating the value

Set `Value` to the new total. Only the digit columns that changed turn, in the direction the number moved, so an
increase reads as an upward roll and a decrease as a downward roll.

```csharp
revenue.Value += random.Next(100, 4000);
rate.Value = Math.Round(rate.Value + random.NextDouble(), 2);
```

Keep the value in the unit the reader sees. Pass 48250 for "$48,250" and let `Prefix` add the sign, rather than
passing a formatted string, which the counter cannot roll.

### Reveal on view

`AnimateOnView` rolls every digit up from zero the first time 60% of the counter is visible. After that the counter
shows its value and updates as usual, so a figure that changes later only rolls from its old value to the new one.

<Demo name="RevealOnView" />

```xml
<arc:AnimatedCounter Label="Rolled up on first view" Value="{Binding Visitors}" AnimateOnView="True" />
```

Use the reveal for a statistic that is the reason for a section, such as the number of customers on a landing page. It
is wasted on a counter in a toolbar that the reader sees constantly, where the roll would only repeat on every page
load.

### Driving it from data

The counter displays what it is given, so the data source owns the number, the refresh schedule and the rounding. The
view model holds the raw value and the counter binds to it one way.

```csharp
public sealed record BillingSummary(double Mrr, double Subscriptions, double Conversion);

public interface IBillingReports
{
    Task<BillingSummary> GetSummaryAsync(DateOnly asOf);
}

public partial class BillingOverviewViewModel : ObservableObject
{
    private readonly IBillingReports _reports;

    public BillingOverviewViewModel(IBillingReports reports) => _reports = reports;

    [ObservableProperty]
    private double _recurringRevenue;

    [ObservableProperty]
    private double _activeSubscriptions;

    [ObservableProperty]
    private double _conversionPercent;

    public async Task RefreshAsync()
    {
        var summary = await _reports.GetSummaryAsync(DateOnly.FromDateTime(DateTime.Today));
        RecurringRevenue = summary.Mrr;
        ActiveSubscriptions = summary.Subscriptions;
        ConversionPercent = Math.Round(summary.Conversion * 100, 2);
    }
}
```

```xml
<StackPanel Orientation="Horizontal" Spacing="32">
    <arc:AnimatedCounter Label="Monthly recurring revenue" Value="{Binding RecurringRevenue}" Prefix="$" />
    <arc:AnimatedCounter Label="Active subscriptions" Value="{Binding ActiveSubscriptions}" />
    <arc:AnimatedCounter Label="Conversion" Value="{Binding ConversionPercent}" Decimals="2" Suffix="%" />
</StackPanel>
```

- Round the value in the view model when the business rule needs a rounded number, because the counter only rounds
  for display. The reader then sees exactly the number your rule produced.
- Refresh on a schedule that matches the data, such as once per page load or once every 30 seconds for a live count.
  A counter that changes several times a second is hard to read, so batch the updates.
- Keep the source of the number in the view model, where tests can read it, and let the counter only display it.

### Formatting and culture

`CultureName` sets the culture for the grouping separator and the decimal separator. It defaults to `en-US`, so the
output matches on the server and on the client, which matters for a number that appears in an exported report. Set it to
the reader's culture when the page is localized, and keep the default for fixed reports.

```xml
<arc:AnimatedCounter Label="Users" Value="{Binding Users}" CultureName="de-DE" />
```

The culture changes only the separators and the digits the counter draws. The prefix and the suffix are text you supply,
so write a currency sign for the culture you use, such as "€" after the number for German readers, and check that the
sign and the number order match the culture.

### Using motion with restraint

The roll is a good signal when a number changed because of an event the reader cares about: a payment arrived, a session
started, a rate moved. It is a distraction when a number changes on a timer and nothing depends on it. If several
counters roll at once on the same screen, the eye has to pick among them, so animate the one that matters most and
leave the rest as static numbers. Motion on the first view should happen once per page, not on every tab switch.

Reduced motion replaces each new value at once, so the figure stays readable for readers who turn off animation. The
information is the same; only the roll is removed.

### Example: billing overview headline figures

The header of a billing overview in an admin console for October 2026. Three figures sit side by side: the revenue, the
active subscriptions and the conversion rate. The revenue carries a currency prefix, the rate has two decimals and a
percent suffix, and each figure has a short label above it. The counters are the headline; the detail lives in the
tables below, which is why the counters are kept to three.

![Three headline figures for a billing overview: revenue, subscriptions and conversion](images/BillingFigures.png)

```xml
<StackPanel Width="620" Spacing="16">
    <TextBlock Text="Billing overview, October 2026"
               FontSize="14"
               FontWeight="Medium"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <StackPanel Orientation="Horizontal" Spacing="32">
        <arc:AnimatedCounter Label="Recurring revenue" Value="48250" Prefix="$" />
        <arc:AnimatedCounter Label="Active subscriptions" Value="1284" />
        <arc:AnimatedCounter Label="Conversion" Value="3.42" Decimals="2" Suffix="%" />
    </StackPanel>
    <TextBlock Text="Updated 4 minutes ago"
               FontSize="12"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
</StackPanel>
```

### Content guidelines

- **Label**: a short noun phrase in sentence case that names the measure, such as "Recurring revenue" or "Active
  subscriptions". The label is the context for the number, so do not leave it out on a headline counter.
- **Prefix and suffix**: a symbol or a short unit, "$", "€", "%", "ms". Put a longer unit in the label.
- **Decimals**: the precision of the business rule, not of the source data. Money usually shows 0 or 2 decimals,
  and a rate usually shows 1 or 2.
- **Period**: name the period in the label or in a caption near the counter, such as "Last 30 days" or "as of Oct 21,
  2026", because the counter shows a value without a date.

| Instead of | Write |
|---|---|
| Revenue number | Recurring revenue |
| $ 48250 | $48,250 (use `Prefix="$"` and a formatted value) |
| Conv. | Conversion, last 30 days |
| Users (count) | Active subscriptions |
| 3.4212 % | 3.42% (use `Decimals="2"` and `Suffix="%"`) |

### Best practices

**Do**

- Fix `Decimals` to the precision of the figure. A width that changes between updates makes the neighboring content
  shift, which reads as a layout bug.
- Keep one counter per headline figure and put the detail in a table or a chart. Three or four counters in a row is the
  limit that a reader can scan.
- Give every counter a `Label`. The label is the accessible name's first part, so a screen reader hears what the number
  is, not only the number.
- Set `AnimateOnView` only on statistics that are the reason for the section, and leave it off for values that change
  on every refresh.

**Avoid**

- Passing a formatted string in `Value`. The counter needs a number to roll; "48,250" is not a value it can animate, and
  the grouping is applied for you.
- Rolling a counter on a timer that runs faster than the data changes. Constant motion without a new fact trains
  readers to ignore the number.
- Putting a counter in a dense table column or a list of many rows. Each counter animates its own digit columns, so a
  long list of them costs more than the reader gets from the motion.
- Using a counter as an input. It has no keyboard focus and no way for the reader to change the value; use an input
  control for values people edit.

### Common questions

**Why does the counter start from zero the first time?** Only `AnimateOnView` starts from zero. The counter arms itself
and rolls every digit up from zero when 60% of it becomes visible for the first time. Without `AnimateOnView` the
first value appears at once, and later changes roll from the old value to the new one.

**Why does `AnimateOnView` not run again when I scroll back?** The reveal runs once per counter. After the first time
the counter stays at its value, so a figure that changes later rolls only for the change, not for the reveal.

**How do I make the number announced to screen readers?** The counter does not announce its changes. Set
`AutomationProperties.LiveSetting="Polite"` on the element that contains the counter, so a change is read after the
current speech. Use it only for a number that changes because of an event the reader needs to know about.

**Why does the number look wrong for my locale?** The counter formats with `CultureName`, which defaults to `en-US`.
Set it to the reader's culture, and write the currency sign and its position yourself, since the prefix and suffix are
plain text.

**Can I round the value inside the counter?** Only for display. `Decimals` controls how many digits the counter shows
and it rounds the shown digits, but the stored `Value` is unchanged. Round in your data code when a business rule
needs the rounded number itself.

## API reference

<!-- api: AnimatedCounter -->

## Accessibility

- The counter is not focusable and defines no keyboard interaction, so it takes no keys.
- The full formatted text, with prefix, separators, decimals and suffix, is the name of the counter, so a reader gets the
  whole number at once. With a `Label`, the name reads as "Label: value".
- The rolling digits are hidden from assistive technology. The counter does not announce its changes by itself. To
  announce them, set `AutomationProperties.LiveSetting="Polite"` on the surrounding element.
- The label is visible text above the number, so the number has a visible context for every reader, not only for those
  who hear the name.

## Motion

- Each digit column turns in the direction the whole number moved, and wraps between 9 and 0.
- When the number of digits changes, the new column and the separator slide in or out, and the width eases to fit. The
  neighboring content shifts slightly with it.
- With `AnimateOnView`, the roll starts once 60% of the counter is visible, and the digits start with a slight stagger.
- Reduced motion replaces the new value at once, without rolling. Off jumps to the final state.

| Change | Full | Reduced | Off |
|---|---|---|---|
| `Value` changes | Digit columns turn in the direction of the change | The new digits appear at once | Immediate |
| Number of digits changes | The new column and separator slide in, the width eases to fit | Immediate | Immediate |
| First view with `AnimateOnView` | Digits roll up from zero, with a slight stagger | The final value appears at once | Immediate |

## Related

- `MetricCard` for a number inside a card with its label and change.
- `Sparkline` for a number with its trend.
- `BarChart` and `LineChart` update their own headline values, so they do not need this counter.
- `TextMorph` for text that changes rather than a number.

| Component | Choose it when |
|---|---|
| `MetricCard` | The number needs a card, a title and a change indicator |
| `Sparkline` | The number needs a compact trend next to it |
| `TextMorph` | The changing content is a word or a phrase, not a number |
| `NumberField` / `MoneyInput` | The person types or edits the number |
| `SortableDataTable` | Many numbers are shown in rows that people scan and sort |
