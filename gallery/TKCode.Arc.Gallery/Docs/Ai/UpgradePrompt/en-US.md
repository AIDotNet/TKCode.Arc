---
title: Upgrade prompt
summary: An upgrade nudge for the moment someone hits a limit, with a usage meter, a rolling price with a monthly and yearly toggle, three concrete unlocks and a success state, in a card that never changes size.
---

<Demo name="Preview" />

## When to use

`UpgradePrompt` is the card an AI product shows the moment someone runs out: it says what was used, shows a usage meter
at the limit, offers one next plan with its price and what it unlocks, and lets the person upgrade or decide later.
Upgrading rolls into a success state with the new limits while the meter settles to the new limit.

- **A usage limit in an AI product**: credits, generations or seats run out and an action is blocked. Show the prompt in
  a dialog or popover anchored to the blocked control, or inline in place of the blocked result.
- **A paywall moment**: one next plan with a monthly and a yearly price, where the yearly saving is part of the pitch.
- **A conversion focused nudge that stays calm**: Maybe later leads to a quiet reminder with a way back, not a second
  pitch.

## When not to use

- **Comparing several plans side by side**: use a pricing table or a plan picker. The prompt offers exactly one plan.
- **Buying prepaid credits without a plan change**: use a credit top-up card.
- **Passive usage reporting on a billing page**: use a usage meter or `Progress`. The prompt is a nudge, not a report.
- **On page load**: show it when an action is actually blocked by the limit, not before.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:UpgradePrompt Used="50" Limit="50" ResetsOn="Nov 1" />
</UserControl>
```

`UpgradePrompt` is a `TemplatedControl` in the Special package. Register the package with `UseArcSpecial()` next to
`UseArcControls()`. The card fills its container up to 440px and follows its own width, never its content.

Your app gives the plan (`Plan`) and the usage (`Used`, `Limit`, `Unit`, `ResetsOn`). With no plan the card is empty:
"No plan to offer" in the header, no meter and no offer, and Upgrade and Maybe later turned off; the package ships no
sample plan. The previews pass the gallery's own plan: an invented product's Pro plan at $20 a month or $16 a month billed
yearly, with 2,000 AI credits, Claude Opus 5.5 and a priority queue. The models are real; the plan, its prices and limits
are the invented product's, not a model vendor's prices. With no `UpgradeHandler` Upgrade runs a 1.1s simulation that
charges nothing, and the success state says so.

### Anatomy

1. **Header**: a tinted tile with a glyph (a bolt in the offer, a check after the upgrade, a clock in the reminder), the
   title as a heading and a two line description.
2. **Usage meter**: "AI credits this month" with the counts ("50 / 50") on the right, a pill bar, and a status row with
   an icon and a label ("Limit reached", or "1,950 AI credits left") and the reset date or the plan's limit on the right.
3. **Price**: the plan's name over the rolling per month price and "/ month", with the billing toggle (Monthly, and
   Yearly with "Save 20%") on the right. Under it, one slot for the billing note ("$192 billed yearly") or an error.
4. **Unlocks**: "Pro unlocks" and three rows, each with a mark in a small tile and one line of text.
5. **Footer**: Maybe later at its natural width and Upgrade filling the rest. Continue in the success state, See Pro
   again in the reminder.

### States

| State | Shows | Buttons |
|---|---|---|
| `Offer` | The limit, the price with the toggle and the unlocks | Maybe later, Upgrade to Pro |
| `Processing` | The same, with the toggle off | Maybe later (off), Upgrading… with a spinner |
| `Error` | The error message under the price | Maybe later, Try again |
| `Success` | "You're on Pro", the meter at the new limit and the new limits | Continue |
| `Reminder` | "No problem", when the limit resets and a hint | See Pro again |

All states share one cell: the body panels and the footers are stacked and crossfade, so the card keeps the size of the
tallest state and the buttons stay in place. `State` is read only and bindable; `ErrorMessage` holds the failure's
message while the card is in `Error`.

### Your plan and unit

`Plan` takes an `UpgradePromptPlan` with a name, the monthly price, the per month price when billed yearly, a currency,
the new limit, the unlocks (`UpgradePromptFeature`: an id, a label and an optional mark with its color) and the new
limits (`UpgradePromptLimit`: a label, a value and an optional mark). The saving badge on Yearly and the yearly charge
come from the two prices. `Unit` names what is counted, and `Title` and `Description` replace the offer's words.

<Demo name="YourPlan" />

```xml
<arc:UpgradePrompt Used="3" Limit="3" Unit="seats" Interval="Monthly" Title="You've used all 3 seats">
    <arc:UpgradePrompt.Plan>
        <arc:UpgradePromptPlan Name="Team" MonthlyPrice="48" YearlyPrice="40" Currency="USD" Limit="25" />
    </arc:UpgradePrompt.Plan>
</arc:UpgradePrompt>
```

Marks are 24 × 24 stroked glyphs such as `ArcIcons` members. Use neutral glyphs or your own product's mark; the previews
use a sparkle beside the model's name rather than any vendor's logo.

### Running the upgrade

`UpgradeHandler` receives an `UpgradePromptSelection` (the plan, the interval, the amount charged today and the
currency) and a cancellation token. Complete the task once the plan is active; throw to show the exception's message
under the price with Try again. The token is cancelled when the prompt leaves the visual tree, and the prompt then
returns to the offer.

<Demo name="Checkout" />

```csharp
prompt.UpgradeHandler = async (selection, cancellationToken) =>
{
    var response = await http.PostAsJsonAsync("/api/billing/upgrade",
        new { plan = selection.Plan.Name, interval = selection.Interval.ToString() }, cancellationToken);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException("Your card was declined. Try another card.");
    }
};
prompt.Upgraded += (_, e) => workspace.Refresh();
prompt.Dismissed += (_, _) => nudges.Snooze("upgrade");
prompt.ContinueRequested += (_, e) =>
{
    e.Handled = true;
    dialog.Close();
    RetryBlockedAction();
};
```

`Upgraded` follows a successful upgrade, after the success state shows. `UpgradeFailed` carries the exception.
`Dismissed` follows Maybe later; record the snooze there. `ContinueRequested` follows Continue: set `Handled` to keep the
success state while you close the prompt; left unhandled, the prompt resets to the offer. `Interval` is two way and
`IntervalChanged` follows the toggle. `UpgradeAsync()`, `Dismiss()`, `ShowOffer()` and `Reset()` drive the same steps
from code.

The amount in the selection is for confirmation copy only. Charge the plan and interval on the server.

### Narrow containers

The card is fluid up to 440px. Below 420px of card width the padding drops from 24 to 20. When the price and the toggle
no longer fit side by side, the toggle moves under the price. Feature lines stay on one line and end in an ellipsis;
the buttons stay side by side at every width.

<Demo name="Narrow" />

### Reduced motion

Under reduced motion the bar and the price jump to their values and the crossfades become plain fades.

<Demo name="ReducedMotion" />

### Content guidelines

Keep the three unlocks concrete and tied to what the person was just doing: more of the thing they ran out of, the
better model, no waiting.

| Instead of | Write |
|---|---|
| Unlock premium features | 2,000 AI credits every month |
| Access to advanced AI | Claude Opus 5.5 for your hardest tasks |
| Enhanced performance | Priority queue, no waiting at peak times |

`Labels` takes an `UpgradePromptLabels` object with every word the card shows or announces. Sentences with values are
functions, so the word order can change in other languages:

```csharp
prompt.Labels = new UpgradePromptLabels
{
    MaybeLater = "Not now",
    TitleAtLimit = (limit, unit) => $"All {limit} {unit} used"
};
```

### Best practices

**Do**

- Show the prompt when an action is blocked by the limit, anchored to the control that was blocked.
- Snooze the nudge in `Dismissed` so it does not reappear on every blocked action.
- Handle `ContinueRequested` to close the prompt and retry the blocked action, so upgrading feels like it unblocked them.

**Avoid**

- Presenting a demo's prices as real ones. The previews' plan prices are made up for an invented product.
- Charging from the amount in the selection. Charge the plan and interval on the server.
- Listing more than three unlocks; the card reserves room for three.

### Common questions

**Why does the card not shrink after Maybe later?** Every state shares one cell, so the card keeps the size of its
tallest state and nothing around it moves.

**Can the meter show usage below the limit?** Yes. Below the limit the title says how many are left and the status
reads "5 AI credits left" with a gauge icon.

**What happens when the prompt is closed during an upgrade?** The handler's token is cancelled and the prompt returns to
the offer.

## API reference

<!-- api: UpgradePrompt -->

<!-- api: UpgradePromptPlan -->

<!-- api: UpgradePromptFeature -->

<!-- api: UpgradePromptLimit -->

<!-- api: UpgradePromptSelection -->

<!-- api: UpgradePromptLabels -->

<!-- api: UpgradePromptIntervalChangedEventArgs -->

<!-- api: UpgradePromptUpgradedEventArgs -->

<!-- api: UpgradePromptFailedEventArgs -->

The enums:

| Type | Values |
|---|---|
| `UpgradePromptInterval` | `Monthly`, `Yearly` |
| `UpgradePromptState` | `Offer`, `Processing`, `Error`, `Success`, `Reminder` |

## Accessibility

- The card is a group named "Upgrade to Pro", and its title is a heading, so it reads well inside a dialog or popover.
- The usage meter is a progress bar named with its value ("50 of 50 AI credits used"); its status is the help text.
- Status is an icon plus a label ("Limit reached", "1,950 AI credits left"), never color alone.
- A polite live region announces upgrading, success, errors and the reminder. Without an `UpgradeHandler` it says the
  upgrade is simulated and nothing is charged.
- The rolling price is hidden from assistive technology; the price group is named in plain text ("$16 a month, $192
  billed yearly").
- Inactive panels are inert and hidden: not hit-testable, skipped by Tab and not exposed, so focus never lands on a
  faded panel. When a state change hides the focused button, focus moves to the new state's main button.
- Upgrade stays focusable while the upgrade runs, so keyboard focus is not lost.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the billing toggle, Maybe later and Upgrade; Continue or See Pro again in the other states |
| `Left` / `Right` | Switch between Monthly and Yearly on the billing toggle |
| `Enter` / `Space` | Activate the focused button |

## Motion

- The usage bar fills once when the card scrolls into view, and settles to the new limit after the upgrade, on a 0.5s
  bounce-free spring. The fill is a pill moved by a translation and clipped to the track, so its rounded end never
  stretches.
- The price rolls digit by digit between monthly and yearly on a bounce-free 0.28s spring, in tabular figures, so
  nothing beside it shifts.
- The title, the description, the meter label, the counts, the status and the billing note crossfade: 100ms out, 160ms
  in with a 2px rise.
- The body panels and the footers fade the same way with a 4px rise; the new one starts as the old one clears.
- The billing toggle's thumb glides on its own spring and its "Save 20%" badge takes the success tint once Yearly is
  chosen.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Bar | Springs over 0.5s | Jumps | Jumps |
| Price | Rolls on a 0.28s spring | Jumps | Jumps |
| Texts | Crossfade with a 2px rise | Fade | Replaced |
| Panels and footers | Fade with a 4px rise | Fade | Replaced |

## Related

- `BillingToggle` and `BillingPrice` for a billing period switch on a pricing page.
- `Progress` for a single allowance with a label.
- `ContextWindow` for what fills a model's context window.

| Component | Choose it when |
|---|---|
| `BillingToggle` | You build a pricing page with several plans |
| `Progress` | You only report usage |
| `ContextWindow` | The limit is a model's context, not a plan |
