---
title: Usage billing
summary: A metered billing period: spend so far, a projected total, daily spend, overage per meter and a spending limit.
---

<Demo name="Period" />

## When to use

- The billing page of a plan that charges for usage on top of a base fee, such as API calls, storage or build minutes.
- Showing where the current period is heading before the invoice arrives: what was spent, the projected total and the spend per day.
- Letting owners cap the bill with a spending limit, next to the numbers that explain it.

## When not to use

- Plans without metered usage: use `BillingOverview` for the plan, the payment method and the invoices.
- A single quota without prices, such as storage used of a plan's space: use `UsageMeter` or `Progress`.
- Usage across many periods: use `BarChart` or `LineChart` on their own.

## Usage

Set the period, the base fee and the meters. Each `UsageBillingMeter` says how much was used, how much the plan
includes and what the rest costs: `UnitPrice` pays for `PriceUnit` units, so `$0.40` per 10,000 calls is
`UnitPrice="0.40" PriceUnit="10000"`. The block adds the meters' costs into `MeteredTotal`, adds `BaseFee` for
`CurrentTotal` and projects `ProjectedTotal` from the share of the period that has begun. Meters written inline land in
`Meters`.

```xml
<arc:UsageBilling PeriodStart="2026-10-01"
                  PeriodEnd="2026-10-31"
                  BaseFee="90"
                  SpendLimit="250"
                  IsSpendLimitEnabled="True">
    <arc:UsageBillingMeter Name="API calls"
                           Unit="calls"
                           Used="1240000"
                           Included="1000000"
                           UnitPrice="0.40"
                           PriceUnit="10000"
                           Icon="{x:Static arc:ArcIcons.Activity}" />
    <arc:UsageBillingMeter Name="Storage" Unit="GB" Used="182" Included="250" UnitPrice="0.10" />
</arc:UsageBilling>
```

`DailySpend` lists the amount spent on each day, starting with the first day of the period. Days after today are not
drawn, and without values the daily spend card is hidden. Pass a list that raises collection changes, such as an
`AvaloniaList<double>`, and the chart follows it.

```csharp
billing.DailySpend = new AvaloniaList<double> { 4.97, 4.82, 2.79, 2.41, 5.74 };
```

### Near and over the limit

The limit progress compares `CurrentTotal` with `SpendLimit`. It turns to the warning color once the spend passes
`AlertThreshold` (80% by default, where the tick sits) and to the danger color once it reaches the limit. A badge beside
the title says "Near limit" or "Limit reached", and `LimitStatus` reports the same state. This example has no
`DailySpend`, so the daily spend card is hidden.

<Demo name="NearLimit" />

### Live usage

A meter announces its changes, so the block updates as usage comes in: the amount spent rolls to the new total, and the
projection, the meter's bar, its charge and the limit progress follow. Here each click adds 50,000 API calls, which cost
$2.00 past the included million, and adds the same amount to today's bar.

<Demo name="Live" />

### Spending limit

`IsSpendLimitEnabled` and `SpendLimit` are two-way: the switch and the amount field in the last card edit them.
`SpendLimitChanged` reports each change with the old and new amount and whether the limit is on. While someone types
in the field, the change is reported once, when the field loses focus or Enter is pressed. Without a limit, the summary
offers "Set a limit", which turns the limit on and moves focus to the amount.

```csharp
billing.SpendLimitChanged += async (_, e) =>
{
    await api.SaveSpendingLimitAsync(e.IsEnabled ? e.NewLimit : null);
};
```

### Today and the projection

"Today" comes from `Clock`, a `TimeProvider` (the system clock by default). It sets the days left, the days drawn and
the projection: `BaseFee + MeteredTotal × days in the period ÷ days begun`. On Oct 20 of a 31-day period the
$12.96 of usage so far projects to $20.09, for $110.09 with the base fee. Tests and previews pass a fixed clock.

```csharp
sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

billing.Clock = new FixedClock(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
```

### Currency and text

`Currency` is an ISO 4217 code. Amounts show `$`, `€`, `£` or `¥` before the number, and other codes after it, such as
`12.96 CHF`. All built-in text is in `Labels`; replace the object to translate it. `Labels.Culture` formats the dates,
amounts and counts.

```csharp
billing.Currency = "EUR";
billing.Labels = new UsageBillingLabels
{
    Culture = CultureInfo.GetCultureInfo("nl-NL"),
    PeriodTitle = "Deze factuurperiode",
    SpentSoFar = "Tot nu toe besteed",
    Projected = amount => $"Verwacht {amount}",
};
```

## API reference

<!-- api: UsageBilling -->

### UsageBillingMeter

A plain model (`INotifyPropertyChanged`) that can be written in XAML. Changing any value raises `PropertyChanged`, and the
block recalculates.

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | What is measured, such as "API calls"; the row's header. |
| `Unit` | `string` | The unit after the amounts, such as "calls", "GB" or "min". |
| `Used` | `double` | The amount used so far this period. |
| `Included` | `double` | The amount the plan includes at no extra cost. With 0 the row shows the usage without a bar. |
| `UnitPrice` | `decimal` | The price of `PriceUnit` units beyond the included amount. |
| `PriceUnit` | `double` | How many units `UnitPrice` pays for (default 1). |
| `Icon` | `Geometry?` | A 24×24 stroke icon shown in the row's tile. |
| `Overage` | `double` | *Read-only.* `max(0, Used − Included)`. |
| `Cost` | `decimal` | *Read-only.* `Overage ÷ PriceUnit × UnitPrice`. |

### UsageBillingLimitChangedEventArgs

| Property | Type | Description |
|---|---|---|
| `OldLimit` | `decimal?` | The limit amount before the change. |
| `NewLimit` | `decimal?` | The limit amount after the change. |
| `IsEnabled` | `bool` | Whether the limit is on after the change. |

### UsageBillingLimitStatus

| Value | Meaning |
|---|---|
| `None` | No limit applies: it is off or has no positive amount. |
| `Ok` | The spend is below the alert threshold. |
| `Near` | The spend has passed the alert threshold but not the limit. |
| `Reached` | The spend has reached or passed the limit. |

### UsageBillingLabels

Every entry has an English default. Entries with parameters receive formatted text.

| Property | Default |
|---|---|
| `Culture` | en-US |
| `PeriodTitle` | "This billing period" |
| `DaysLeft` | days ⇒ "11 days left", "1 day left", "Last day" |
| `PeriodEnded` | "Period ended" |
| `SpentSoFar` | "Spent so far" |
| `Projected` | amount ⇒ "Projected $110.09" |
| `SpendingLimit` | "Spending limit" |
| `LimitProgress` | (current, limit) ⇒ "$102.96 of $250.00 limit" |
| `NoLimit` | "No spending limit" |
| `SetLimit` | "Set a limit" |
| `NearLimit` / `LimitReached` | "Near limit" / "Limit reached" |
| `DailySpend` | "Daily spend" (the chart's accessible name) |
| `DailyAverage` / `DaySpend` | "Average daily spend" / "Spent" (the chart headline at rest and while a day is read) |
| `Day` | "Date" (the chart's data table) |
| `Usage` | "Usage" |
| `MeterUsage` | (used, included, unit) ⇒ "1.2M of 1M calls included" |
| `MeterUsageOnly` | (used, unit) ⇒ "3.4K min" (a meter without an included amount) |
| `Overage` | amount ⇒ "240K over" (in a meter bar's accessible name) |
| `Included` | "Included" |
| `Rate` | (price, per) ⇒ "$0.40 / 10K calls" |
| `UsageTotal` | "Usage charges so far" |
| `SpendingLimitDescription` | "Cap the total bill for one billing period." |
| `LimitAmount` | "Limit amount" |
| `LimitNote` | percent ⇒ "We email the owners at 80% and pause usage-based features at 100%." |

## Accessibility

- Each card is a `SettingsGroup`. The amount spent reads with its label, such as "Spent so far: $102.96".
- The limit progress is a progress bar named like "Spending limit: $102.96 of $250.00 limit". The "Near limit" and "Limit reached" badge is a polite live region, so the change is announced.
- Each meter bar is a progress bar named with the meter, its usage and the overage, such as "API calls: 1.2M of 1M calls included, 240K over". The charge reads with its rate, such as "$9.60, $0.40 / 10K calls".
- The daily spend chart takes focus. The arrow keys move between days, Page Up and Page Down move a week, Home and End go to the first and last day, and Escape returns to the average. Its accessible summary names the highest and lowest day.
- The switch takes the setting's header as its name and the description as its help text. The amount field is labeled "Limit amount" and described by the note under it.
- "Set a limit" turns the limit on and moves focus to the amount field. Tab order follows the cards from top to bottom.

## Motion

- The amount spent rolls digit by digit to a new total.
- Meter bars and the limit progress spring to their new lengths, and the limit's color crossfades when it passes the alert threshold or the limit.
- The status badge pops in and out beside the title.
- The limit editor opens and closes in height on a spring when the switch changes.
- The chart's bars grow in a wave the first time the chart is seen and morph when the values change.
- Reduced motion keeps only the color fades: bars and the editor take their new size at once. Off changes everything at once.

## Related

- The other Settings blocks: `SettingsPage`, `SettingsCommand`, `SecuritySettings`, `BillingOverview` and `CancelFlow`.
- `SettingsGroup` and `SettingsRow`, the shared parts every card here is built from (described on the `SettingsPage` page).
- `BarChart` for the daily spend, `AnimatedCounter` for the amount spent, `MoneyInput` and `Switch` for the limit, and `Badge` for its status.
- `UsageMeter` and `Progress` for quotas without prices.
