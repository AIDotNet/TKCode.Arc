---
title: Account health
summary: One customer account's health: the score with its band and weekly trend, the renewal date and days left, open tickets, usage by metric, the risks, and recommended actions that project the score they win back.
---

<Demo name="Preview" />

## When to use

`AccountHealth` is one customer account on one card, for the person who owns the renewal. Three tiles answer the first
questions: how healthy the account is (the score, its band and how it moved over the weeks), when it renews and how many
days are left, and how many tickets are open. Under them, a usage chart switches between metrics, the risks are listed by
severity, and recommended actions can be ticked off to see the score they are expected to win back.

- **Renewal preparation.** See the trend, the open tickets and the risks before the call.
- **Weekly account reviews.** One card per account, the same layout every time.
- **Planning a save.** Tick the actions you will take and read the projected score.

## When not to use

- **Many accounts at once.** Use a `Table` of accounts and open this block for one.
- **Raw usage analysis.** Use `LineChart` or the Analytics explorers for deep dives.
- **Scores you cannot explain.** The block shows the score; compute it where you can show its inputs.

## Usage

The block derives from `TemplatedControl`. Without a health history it shows an invented account. Give it
`HealthHistory` (weekly scores, oldest first), `RenewalDate`, `Usage`, `Tickets`, `Risks` and `Actions`.

```xml
<arc:AccountHealth AccountName="Copperleaf Hotels" Subtitle="Scale plan" RenewalWarningDays="90" />
```

```csharp
account.HealthHistory.AddRange([58, 61, 63, 66, 70, 74, 79, 81]);
account.Usage.Add(new AccountHealthUsageMetric("Active properties", [12, 14, 18, 21, 26, 28], "properties") { Limit = 40 });
account.ActionToggled += (_, e) => plan.Save(e.Action, e.ProjectedScore);
```

### Score, renewal and actions

`Score` is the last value of the history; its band is healthy from 75, watch from 50 and at risk below. `DaysToRenewal`
counts from `Today` (or today); within `RenewalWarningDays` the line turns to the warning color. Ticking an action writes
`IsDone`, updates `ProjectedScore` (the score plus the impact of every done action, at most 100) and raises
`ActionToggled`. `SelectedMetric` (two-way) is the usage chart's metric. Words are in `Labels` (`AccountHealthLabels`).

<Demo name="OwnAccount" />

## API reference

<!-- api: AccountHealth -->

<!-- api: AccountHealthUsageMetric -->

<!-- api: AccountHealthTicket -->

<!-- api: AccountHealthRisk -->

<!-- api: AccountHealthAction -->

## Accessibility

- The account name is a level 2 heading; each section has a level 3 heading.
- Each tile reads as one line ("Health score 64, Watch, −18 in 11 weeks").
- The usage chart is the Arc `LineChart`, with its own summary and keyboard readout.
- Actions are checkboxes; the projected score is announced politely when it changes.

## Motion

- The health trend draws in from the left the first time (700 ms, Full motion only).
- The usage chart morphs between metrics as `LineChart` does.
- Reduced motion and Off show both at once.

## Responsive behavior

- At `CompactWidth` (720 px) or less the three tiles stack and the lists move under each other; the padding tightens to 16.
- The metric switch hides when there is only one metric.
- Long ticket titles and risk details wrap.

## Related

- `LineChart`, `Badge` and `Checkbox`, the controls the block is made of.
- `Metrics dashboard` and `KPI drilldown` for product-wide numbers.
- `Call insights` and `Lead scoring`.
