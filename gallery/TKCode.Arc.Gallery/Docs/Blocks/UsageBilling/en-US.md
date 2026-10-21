---
title: Usage billing
summary: A metered billing period: spend so far, a projected total, daily spend, overage per meter and a spending limit.
---

<Demo name="Period" />

## When to use

Usage billing is the billing page of a plan that charges for consumption on top of a base fee. It answers the two
questions people ask before an invoice arrives: what has this period cost so far, and where is it heading? It sits
on an account or billing page, above the invoice history, and it is the place where an owner sets a spending cap.

- **Usage-based API plans**: a developer platform that charges $0.40 per 10,000 calls above an included million. The
  overage of each meter shows as it accrues, so an owner sees a surprise coming before the invoice.
- **Storage and build minutes**: a cloud product that bills storage in GB and CI minutes beyond the plan's allowance.
  Each meter keeps its own unit, so "3,420 min" and "182 GB" read correctly side by side.
- **Spending caps for a team**: an owner limits the period to $250. The summary turns to "Near limit" at 80% and to
  "Limit reached" at the limit, and the amount can be edited where it is shown.
- **Daily spend review**: a finance page that draws the amount spent each day, so a spike on one day is visible
  against the average.
- **Live usage**: an account page where usage events arrive while it is open. The totals, the projection and the bars
  follow each change without a reload.

## When not to use

- **Plans without metered usage**: use `BillingOverview` for the plan, the payment method and the invoices.
- **A quota without prices**, such as storage used against a plan's space or seats left: use `UsageMeter` or
  `Progress`, which show a share of a limit and do not price the overage.
- **Usage across many periods**: use `BarChart` or `LineChart` on their own for a trend. This block shows one period.
- **A list of past invoices**: use `SortableDataTable`, which sorts and filters rows, and keep this block for the
  period in progress.

## Usage

Set the period, the base fee and the meters. Each `UsageBillingMeter` says how much was used, how much the plan
includes and what the rest costs: `UnitPrice` pays for `PriceUnit` units, so `$0.40` per 10,000 calls is
`UnitPrice="0.40" PriceUnit="10000"`. The block adds the meters' costs into `MeteredTotal`, adds `BaseFee` for
`CurrentTotal` and projects `ProjectedTotal` from the share of the period that has begun. Meters written inline land in
`Meters`, the content property.

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

`UsageBilling` derives from `TemplatedControl`. Its cards are the shared parts of the Settings block: a `SettingsGroup`
for each card and a `SettingsRow` for the limit switch and each meter row. That keeps it in line with the settings
pages around it, and the same theme and motion apply.

`DailySpend` lists the amount spent on each day, starting with the first day of the period. Days after today are not
drawn, and without values the daily spend card is hidden. Pass a list that raises collection changes, such as an
`AvaloniaList<double>`, and the chart follows it.

```csharp
billing.DailySpend = new AvaloniaList<double> { 4.97, 4.82, 2.79, 2.41, 5.74 };
```

### Anatomy

1. **Summary card**: the title from `Labels.PeriodTitle`, the period's dates and the days left
   in the card's description, and the status badge in the header actions when the limit is near or reached.
2. **Spent so far**: the amount spent, in the largest type size of the card, with the label
   `SpentSoFar` above it. It rolls to each new total.
3. **Projection**: `Projected` in the small type size, under the amount, in the secondary text color.
4. **Limit column**: the label
   `SpendingLimit`, the share of the limit in medium weight, a 16 px bar with an 8 px track and a tick at
   `AlertThreshold`, and the caption `LimitProgress`. With no limit it shows `NoLimit` and a "Set a limit" button.
5. **Daily spend card**: a `BarChart` with a 160 px plot, hidden when `DailySpend` is null or empty.
6. **Usage card**: one row per meter with its icon tile, name, usage line (`MeterUsage`) and a
   charge on the right: the overage amount with its rate, or "Included". Under each row sits a 6 px bar, filled to the
   included part in the accent color and to the overage in the warning color. The footer shows `UsageTotal` and
   the metered total.
7. **Spending limit card**: a `SettingsRow` with the switch and its description, then a 320 px
   wide `MoneyInput` (Small) indented 50 px to line up with the row text. The note from `LimitNote` sits under it.

In the summary, the spend and the limit sit side by side with a 40 px gap. Below `NarrowWidth` (600 px by default),
the limit column moves under the spend and starts 24 px lower.

### Near and over the limit

The limit progress compares `CurrentTotal` with `SpendLimit`. It turns to the warning color once the spend passes
`AlertThreshold` (80% by default, where the tick sits) and to the danger color once it reaches the limit. A badge beside
the title says "Near limit" or "Limit reached", and `LimitStatus` reports the same state. Use the status for your own
logic, such as showing an alert in the navigation. This example has no `DailySpend`, so the daily spend card is hidden.

<Demo name="NearLimit" />

### Live usage

A meter announces its changes, so the block updates as usage comes in: the amount spent rolls to the new total, and the
projection, the meter's bar, its charge and the limit progress follow. Here each click adds 50,000 API calls, which cost
$2.00 past the included million, and adds the same amount to today's bar.

<Demo name="Live" />

### Data shape and performance

Keep the usage counts as `double` and the money as `decimal`: `Used` and `Included` count units, while `BaseFee`,
`UnitPrice` and `SpendLimit` are amounts. The block does its arithmetic in `decimal`, so the totals are exact to the cent.
A unit price keeps as many decimals as it needs, up to four, so `$0.008` per minute shows as written.

Each meter is an `INotifyPropertyChanged` model. Changing `Used` raises the computed `Overage` and `Cost` and updates the
totals, that meter's charge and its bar; the other rows keep their values. Adding or removing a meter from `Meters` builds
or removes only that row and then recalculates the totals. Update the meters from your usage feed at a rate a person can read;
a value that changes many times a second cannot be followed.

The chart takes the days from the first of the period through today, so a period that has just started draws a few
bars.

### Spending limit

`IsSpendLimitEnabled` and `SpendLimit` are two-way: the switch and the amount field in the last card edit them.
`SpendLimitChanged` reports each change with the old and new amount and whether the limit is on. While someone types
in the field, the change is reported once, when the field loses focus or Enter is pressed. Without a limit, the summary
offers "Set a limit", which turns the limit on and moves focus to the amount.

Turning the limit off keeps the amount. The editor collapses, and the amount stays in `SpendLimit` for the next time the
switch is turned on, so a person does not retype a cap they set last month.

```csharp
billing.SpendLimitChanged += async (_, e) =>
{
    await api.SaveSpendingLimitAsync(e.IsEnabled ? e.NewLimit : null);
};
```

### Binding to a view model

The block is a display and editing surface for a period. The view model owns the period's values, the meters and the
save, and the block binds to them. `Meters` and `DailySpend` are settable, so a view model can supply them directly.

```csharp
public partial class UsageBillingViewModel : ObservableObject
{
    private readonly IBillingApi _api;

    public UsageBillingViewModel(IBillingApi api) => _api = api;

    [ObservableProperty]
    private string _currency = "USD";

    [ObservableProperty]
    private decimal _baseFee = 90m;

    [ObservableProperty]
    private decimal? _spendLimit = 250m;

    [ObservableProperty]
    private bool _isSpendLimitEnabled = true;

    [ObservableProperty]
    private IList<double>? _dailySpend;

    public ObservableCollection<UsageBillingMeter> Meters { get; } = new();

    public async Task SaveSpendingLimitAsync(decimal? limit) => await _api.SaveSpendingLimitAsync(limit);
}
```

```xml
<arc:UsageBilling Currency="{Binding Currency}"
                  BaseFee="{Binding BaseFee}"
                  Meters="{Binding Meters}"
                  DailySpend="{Binding DailySpend}"
                  SpendLimit="{Binding SpendLimit, Mode=TwoWay}"
                  IsSpendLimitEnabled="{Binding IsSpendLimitEnabled, Mode=TwoWay}" />
```

- `SpendLimit` and `IsSpendLimitEnabled` are the two-way properties. Bind them so the view model always holds the
  value the person sees.
- Save from `SpendLimitChanged`, not from the binding alone. The event reports the old and new amount and whether the
  limit is on, and it reports a typed amount once, when the field lets go.
- `BaseFee`, `Currency`, `PeriodStart` and `PeriodEnd` are one-way inputs. The block recalculates when they change.
- `Clock` is not a styled property, so set it in code, not in the binding. See "Today and the projection".

### Today and the projection

"Today" comes from `Clock`, a `TimeProvider` (the system clock by default). It sets the days left, the days drawn and
the projection: `BaseFee + MeteredTotal × days in the period ÷ days begun`. On Oct 20 of a 31-day period the
$12.96 of usage so far projects to $20.09, for $110.09 with the base fee. The projection assumes usage keeps its pace,
and the share of days is kept between one day's share and 1. Tests and previews pass a fixed clock.

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

### Example: October usage for a developer platform

The billing page of a developer platform in the middle of its October period. The $90 base fee is fixed. API calls are
past the included million, so they cost money; storage is inside its allowance, so it costs nothing; build minutes are
a little past theirs. The owners cap the bill at $250, and the limit stays open because it is on. Every amount, bar and
the projection come from the three meters, so the figures agree with each other.

![October usage for a developer platform: spend, projection, meters and the spending limit](images/OctoberUsage.png)

```xml
<arc:UsageBilling Width="640"
                  PeriodStart="2026-10-01"
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
    <arc:UsageBillingMeter Name="Storage"
                           Unit="GB"
                           Used="182"
                           Included="250"
                           UnitPrice="0.10"
                           Icon="{x:Static arc:ArcIcons.Database}" />
    <arc:UsageBillingMeter Name="Build minutes"
                           Unit="min"
                           Used="3420"
                           Included="3000"
                           UnitPrice="0.008"
                           Icon="{x:Static arc:ArcIcons.Timer}" />
</arc:UsageBilling>
```

### Content guidelines

- **Meter name**: a noun in sentence case that names what is measured, such as "API calls", "Storage" or "Build
  minutes". The unit comes from `Unit` and follows the plan's own term: "calls", "GB", "min".
- **Period title**: keep "This billing period" and let the description carry the dates. A month name in the title
  goes stale when the period changes.
- **Limit copy**: "Spending limit" names the setting, and the description states the effect ("Cap the total bill for
  one billing period."). Say what happens at each threshold in `LimitNote`, with the same percentages as
  `AlertThreshold`.
- **Amounts and counts**: let the block format them through `Culture` and `Currency`. Do not pre-format amounts as
  text, because a text value cannot follow a culture change.

| Instead of | Write |
|---|---|
| Bill total | Spent so far |
| Limit | Spending limit |
| Click here to set a limit | Set a limit |
| 240000 over | 240K over (the block formats it) |
| Estimated bill | Projected $110.09 |

### Best practices

**Do**

- Set `Clock` in previews and tests. The days left, the drawn days and the projection all depend on today, so a fixed
  clock keeps screenshots and tests stable.
- Turn the limit off with `IsSpendLimitEnabled` rather than clearing the amount. The amount stays for the next time the
  switch is turned on.
- Keep `Currency` and `Labels.Culture` together. The currency sets the symbol and the culture sets the separators, so
  set both for one locale.
- Use `LimitStatus` for alerts elsewhere in the app. It is the same state the badge shows, so an alert and the card
  never disagree.

**Avoid**

- Presenting the projection as a bill. It assumes usage keeps its pace, so label it "Projected", as the default does.
- Enforcing the limit in the UI alone. The block shows the state and raises `SpendLimitChanged`; it does not stop usage.
  Your service must enforce the cap and read the same amount.
- Mixing periods in one block. It shows one period from `PeriodStart` to `PeriodEnd`; a second period needs its own
  block or a trend chart.
- Rounding the usage counts before they reach the meter. The overage and the charge come from the exact counts, and
  rounding can move a count across the included amount.

### Common questions

**Why does the projection differ from my own estimate?** The projection is `BaseFee` plus the metered total scaled by
the share of the period's days that have begun by `Clock`. On Oct 20 of a 31-day period, $12.96 of usage projects to
$20.09 of usage, or $110.09 with the base fee. Check `Clock` first.

**Why is the daily spend card missing?** A null or empty `DailySpend` hides the card. The card also draws only the days
through today, so a period that has just started shows a short chart.

**Which amount does the limit compare against?** `CurrentTotal`, which is the base fee plus the metered total, not the
usage alone. A $250 limit on a $90 base fee leaves $160 of usage before it is reached.

**Does the block stop usage at the limit?** No. The block displays `LimitStatus` and raises `SpendLimitChanged`. Your
billing service enforces the cap, and the note under the field describes the behavior you implement.

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
- The limit progress is a progress bar named like "Spending limit: $102.96 of $250.00 limit". The "Near limit" and
  "Limit reached" badge is a polite live region, so the change is announced.
- Each meter bar is a progress bar named with the meter, its usage and the overage, such as "API calls: 1.2M of 1M
  calls included, 240K over". The charge reads with its rate, such as "$9.60, $0.40 / 10K calls".
- The switch takes the setting's header as its name and the description as its help text. The amount field is labeled
  "Limit amount" and described by the note under it.
- The block takes no focus of its own; its controls do. Tab order follows the cards from top to bottom.
- While the limit is off, the editor is collapsed. Its field stays in the tree, so the block removes it from the tab
  order until the limit is on.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the limit switch, the amount field (only while the limit is on), the "Set a limit" button and the daily spend chart, in card order |
| `Enter` in the amount field | Ends the edit and reports the new limit |
| `Enter` on "Set a limit" | Turns the limit on and moves focus to the amount field |
| Arrow keys on the chart | Move between days; Home and End go to the first and last day |
| `Page Up` / `Page Down` on the chart | Move a week |
| `Escape` on the chart | Returns the headline to the average |

The chart takes focus. Its accessible summary names the highest and lowest day, so a person hears the range before
they move through the days.

## Motion

- The amount spent rolls digit by digit to a new total.
- Meter bars and the limit progress spring to their new lengths, and the limit's color crossfades when it passes the alert threshold or the limit.
- The status badge pops in and out beside the title.
- The limit editor opens and closes in height on a spring when the switch changes.
- The chart's bars grow in a wave the first time the chart is seen and morph when the values change.
- Reduced motion keeps only the color fades: bars and the editor take their new size at once. Off changes everything at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The amount spent changes | Digits roll to the new total | Changes at once | Immediate |
| A meter bar or the limit progress changes length | Springs to the new length | Takes the new length at once | Immediate |
| The limit crosses the alert threshold or the limit | Color crossfades (standard) | Color crossfades (fast) | Immediate |
| The limit editor opens or closes | Height springs | Takes its new height at once | Immediate |
| The daily spend chart appears or changes | Bars grow in a wave, then morph | Changes at once | Immediate |

## Related

- The other Settings blocks: `SettingsPage`, `SettingsCommand`, `SecuritySettings`, `BillingOverview` and `CancelFlow`.
- `SettingsGroup` and `SettingsRow`, the shared parts every card here is built from (described on the `SettingsPage` page).
- `BarChart` for the daily spend, `AnimatedCounter` for the amount spent, `MoneyInput` and `Switch` for the limit, and `Badge` for its status.
- `UsageMeter` and `Progress` for quotas without prices.

| Component | Choose it when |
|---|---|
| `BillingOverview` | The page shows the plan, the payment method and the invoices, with or without usage |
| `UsageMeter` / `Progress` | A quota has a limit but no price, such as seats or storage space |
| `BarChart` / `LineChart` | The trend over several periods matters more than one period's total |
| `MoneyInput` | A single amount is entered on its own, outside the spending limit card |
| `SortableDataTable` | Past invoices or usage lines need sorting and filtering |
| `CancelFlow` | An owner wants to end the plan, with its reasons and offer step |
