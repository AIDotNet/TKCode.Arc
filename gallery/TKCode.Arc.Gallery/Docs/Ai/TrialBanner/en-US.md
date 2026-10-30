---
title: Trial banner
summary: A trial status banner where a ring counts down the days left, the copy names what the workspace loses, and the upgrade path is one click away, as a slim banner, a sidebar card or a header pill.
---

<Demo name="Preview" />

## When to use

`TrialBanner` tells a workspace on a free trial how long it has, what it loses when the trial ends and how to keep it.
A ring counts down the days, the copy names what goes away ("SAML SSO and 12 seats") and Add payment method is one click
away. In the last days it shifts to a warning tone and names the end day; once the trial has ended it shows a lock and
what the workspace lost; once a payment method is on file it shows a check.

- **A SaaS app with a free trial** that needs a quiet, always visible upgrade path.
- **Telling a workspace what it loses** when the trial ends, and nudging harder in the last days.
- **Showing an expired trial** without blocking the app.

## When not to use

- **A hard paywall** that blocks a feature: use a dialog or an upgrade screen.
- **Generic announcements or release notes**: use a plain banner or a toast.
- **Usage limits** (credits, seats): use a usage meter.

## Usage

Pass the days left from your billing provider, or the end date, the plan, the workspace, what it would lose and your
checkout. With neither `DaysLeft` nor `EndsAt` there is no trial to show: the banner reads "No trial in progress" over an
empty ring, without actions (the preview's "No trial" option). `Plan`, `Workspace`, `LossSummary` and `Features` start
empty; the package ships no sample trial. The previews pass the gallery's invented one: a Pro trial of the Northwind
workspace that would lose SAML SSO and 12 seats.

```csharp
banner.DaysLeft = trial.DaysLeft;            // or: banner.EndsAt = trial.EndsAt;
banner.EndsAt = trial.EndsAt;                // for the date in the copy
banner.AddPaymentHandler = async cancellation =>
{
    // Open checkout or the billing portal; return once a payment method is on file, throw if it failed.
    await billing.AddPaymentMethodAsync(cancellation);
};
banner.ComparePlansRequested += (_, e) => { e.Handled = true; OpenPlansPage(); };
banner.Snoozed += (_, e) => settings.TrialBannerSnoozedUntil = e.Until;
banner.Dismissed += (_, _) => settings.TrialBannerDismissed = true;
```

### Days and dates

`DaysLeft` wins when you set it: the banner then never reads the clock for the days, so it shows exactly what your billing
provider says (0 or less is the expired state). Leave it null and set `EndsAt` to have the banner count the local calendar
days from today; it counts again at local midnight. `TimeProvider` is where "today" comes from (the system clock by
default); pass your own in tests. `EndsAt` also gives the date in the copy; without it the date is today plus the days
left.

`WarnAt` (3) is how many of the last days take the warning tone. `TrialDays` (14) is the trial's length: the ring shows the
days left of it.

### Variants

<Demo name="Card" />

`Variant` picks the shape. `Banner` is a slim row for the top of an app. `Card` is a sidebar card, up to 340px wide,
with the list of what the workspace loses: pass three or four `TrialFeature`s in `Features` (a name, an optional muted
detail and a 14px mark). `Pill` is a compact header pill that opens the card in a popover.

<Demo name="Pill" />

### Checkout

<Demo name="Checkout" />

`AddPaymentHandler` opens your checkout. While it runs the button shows a spinner and the copy reads "Opening checkout…";
when it returns the banner shows the upgraded state and sets `IsUpgraded`; when it throws the copy says checkout did not
open and the button reads Try again. The token is cancelled if the banner leaves the window. Without a handler the call is
simulated (900ms).

Compare plans raises `ComparePlansRequested` and, in the upgraded state, Manage billing raises `ManageBillingRequested`.
Set `Handled`; an unhandled request shows a short simulated note in place of the copy.

### Dismiss and snooze

Dismiss raises `Dismissed`; the snooze menu offers until tomorrow, for three days, or until the warning starts, and raises
`Snoozed` with the option and `Until`, the start of that local day. Persist both yourself and stop rendering the banner
until the snooze ends. Meanwhile the banner folds into the trial pill (`IsCollapsed` turns true); the pill opens it again.
`CollapseTo="None"` renders nothing instead. `IsDismissible` and `IsSnoozable` hide the buttons.

### Narrow containers

<Demo name="Narrow" />

The banner reads its own width: under 600px the actions move under the copy, aligned with its left edge, and the second
line may take two lines (both are reserved, so nothing shifts). It holds up at 360px without horizontal scroll.

### Your own words

`Labels` holds every word the banner shows or says. Lines that carry values are format functions:

```csharp
banner.Labels = new TrialBannerLabels
{
    Days = days => days == 1 ? "1 Tag" : $"{days} Tage",
    ActiveTitle = (days, plan) => $"Noch {days} in Ihrer {plan}-Testphase",
    AddPayment = "Zahlungsmethode hinzufügen",
};
```

### Content guidelines

- Keep `LossSummary` to a few words ("SAML SSO and 12 seats") so the banner stays on one line.
- Keep `Features` to three or four rows: these are the reasons to pay, not a feature table.
- Name the plan the workspace is trying (`Plan`) and the workspace (`Workspace`); both appear in the copy.

### Best practices

- Put the banner above the app content, the card at the bottom of a sidebar, the pill in a dense header.
- Pass the days or the end date from your billing provider (for example the subscription's trial end).
- Persist dismiss and snooze, then stop rendering the banner until the snooze ends.

## API reference

<!-- api: TrialBanner -->

<!-- api: TrialFeature -->

<!-- api: TrialSnoozedEventArgs -->

<!-- api: TrialBannerLabels -->

## Accessibility

- The banner and the card are a named region ("Pro trial"); the ring is decorative and the days are always in the text.
- A polite live region announces billing, snooze and dismiss results, and says when billing was simulated.
- Status never relies on color: the warning tone also names the end day in the copy, an ended trial shows a lock and an
  upgraded one a check.
- After dismiss or snooze, focus moves to the trial pill; opening the banner again moves focus to the primary action.

### Keyboard

| Keys | Action |
|---|---|
| Tab | Moves through Compare plans, Add payment method, Snooze and Dismiss |
| Enter, Space | Runs the focused action, opens the snooze menu or opens the pill's card |
| Arrow Up, Arrow Down | Move through the snooze options |
| Escape | Closes the snooze menu or the pill's popover |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Days change | The ring's arc springs to its new length with no bounce (0.24s) | At once | At once |
| Tone changes | The ring's color follows in 160ms | At once | At once |
| Copy, days and icons change | One crossfade in a shared cell: old out in 100ms, new in over 160ms with a 2px rise | At once | At once |
| A button label changes | The width morphs without bounce | At once | At once |
| The pill's label changes | Its width springs to the measured text | At once | At once |
| Dismiss, snooze, reopen | The surface springs between the banner and the pill (0.33s, no bounce); the faces crossfade | At once | At once |

No timers or loops run while the banner is idle: only the simulated billing call and the short simulated note use a
timeout.

## Related

- `ContextWindow` for showing how much of a budget is used.
- `AgentStatus` for a live status pill.
