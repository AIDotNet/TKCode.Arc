---
title: Cancel flow
summary: A considerate cancellation: ask why, offer a fitting alternative, show what ends, hold to confirm, and allow a quick return.
---

<Demo name="Flow" />

## When to use

- The cancel button on a billing or plan page, where leaving should take a moment of thought but never feel like a trap.
- Subscriptions where a discount, a pause or a conversation often fits the reason better than leaving.
- Any irreversible plan change that needs a clear summary of what ends and when, and a deliberate confirmation.

## When not to use

- Removing a single item or a seat: use `ConfirmMorph` or `HoldToConfirm` in place.
- Downgrading to another paid plan: show the plans side by side with `RadioCards` or a pricing table.
- Cases where the law or your terms require one-click cancellation: keep the offer out of the way (set no offers), or link straight to the confirmation your terms describe.

## Usage

`CancelFlow` is one card. The first step asks why, with a `RadioGroup` of reasons; once a reason is chosen, an
optional feedback field opens under the list. Continue then shows the reason's `Offer` (or `DefaultOffer`), and from
there, or directly when there is no offer, the confirmation step lists `Losses` and asks the person to hold a button.
Holding to the end moves to the canceled step and raises `CancellationConfirmed`; accepting the offer moves to the
retained step and raises `OfferAccepted`.

The preview gives two reasons their own offer ("Take 40% off for 3 months" and "Pause instead"), and the others fall
back to the default offer. `AccessEndsOn` (the end of the paid period) and `PlanName` fill in the sentences.

```csharp
flow.AccessEndsOn = new DateTime(2026, 11, 1);

flow.CancellationConfirmed += (_, e) => billing.Cancel(e.Reason.Title, e.Feedback);
flow.OfferAccepted += (_, e) => billing.Apply(e.Offer.Title);
flow.KeepPlanRequested += (_, _) => page.CloseCancelFlow();
```

### Without an offer

When neither the chosen reason nor `DefaultOffer` has an offer, the flow goes from the reason straight to the
confirmation, and the step bar has two segments instead of three. With `Reasons` left empty the flow uses the five
reasons in `Labels.DefaultReasons`. `SelectedReason` and `Feedback` are two-way; this demo starts with a reason that
asks a follow-up question, so the field reads "What were you missing?".

<Demo name="NoOffer" />

### In a dialog

Set `Appearance` to `Plain` to drop the card's frame, shadow and padding when the flow sits inside a `Dialog`. The
dialog closes on `KeepPlanRequested`, which both "Keep my plan" and "Done" raise. "Reactivate plan" raises
`ReactivateRequested` and then starts the flow over; mark the event handled to keep the canceled step on screen. An
offer without an `Icon` shows a gift.

<Demo name="InDialog" />

### Text and other languages

Every built-in string is in `CancelFlowLabels`. Replace the whole object to reword or translate the flow; dates follow
`Labels.Culture`.

```csharp
flow.Labels = new CancelFlowLabels
{
    Culture = CultureInfo.GetCultureInfo("nl-NL"),
    ReasonTitle = "Waarom zeg je op?",
    Continue = "Doorgaan",
    KeepPlan = "Abonnement houden",
    HoldToCancel = "Houd vast om op te zeggen",
};
```

## API reference

<!-- api: CancelFlow -->

`CancelFlowReason`

| Property | Type | Description |
|---|---|---|
| `Title` | `string` | The reason as the person would say it, such as "Too expensive". |
| `Description` | `string?` | A short line under the title. |
| `Offer` | `CancelFlowOffer?` | The offer shown after this reason; null falls back to `DefaultOffer`. |
| `AsksForDetails` | `bool` | When true, the feedback field asks `DetailsPrompt` instead of the general question. The answer stays optional. |
| `DetailsPrompt` | `string?` | The follow-up question, such as "What were you missing?"; null uses `Labels.DetailsPrompt`. |

`CancelFlowOffer`

| Property | Type | Description |
|---|---|---|
| `Title` | `string` | What is offered, such as "Take 40% off for 3 months". |
| `Description` | `string?` | What the offer means for the plan and the bill. |
| `AcceptLabel` | `string?` | The accept button, such as "Claim 40% off"; null uses `Labels.AcceptOffer`. |
| `Icon` | `Geometry?` | A 24×24 stroke icon for the tile above the title; null shows a gift. |
| `AcceptedTitle` | `string?` | The title of the retained step, such as "40% off is on"; null uses `Labels.RetainedTitle`. |
| `AcceptedDescription` | `string?` | The sentence under it; null uses `Labels.RetainedDescription`. |

`CancelFlowLabels` (all `init`; functions build sentences from the plan name and the formatted date)

| Property | Default |
|---|---|
| `Culture` | en-US |
| `StepCounter` | "Step {n} of {total}" |
| `Back` | "Back" |
| `ReasonTitle` / `ReasonDescription` | "Why are you canceling?" / "Your answer helps us improve {plan}." |
| `FeedbackLabel` / `DetailsPrompt` / `FeedbackPlaceholder` | "Anything else we should know?" / "Tell us a little more" / "Optional" |
| `KeepPlan` / `Continue` | "Keep my plan" / "Continue" |
| `AcceptOffer` / `ContinueCanceling` | "Accept offer" / "Continue canceling" |
| `ConfirmTitle` / `AccessUntil` / `AfterThat` | "Cancel your {plan} plan?" / "You keep access until {date}." / "After that:" |
| `LossesName` / `HoldToCancel` / `HoldConfirmed` | "What ends" / "Hold to cancel plan" / "Canceled" |
| `CanceledTitle` / `CanceledDescription` | "Your plan is canceled" / "You can keep using {plan} until {date}. We sent a confirmation to your email." |
| `Reactivate` | "Reactivate plan" |
| `RetainedTitle` / `RetainedDescription` / `Done` | "{offer} is on" / "Thanks for staying. You'll see the change on your next invoice." / "Done" |
| `DefaultReasons` | Too expensive; Not using it enough; Missing a feature I need; Switching to another tool; Something else |

## Accessibility

- After every step change, focus moves to the new step's title, which is announced politely. Titles take focus but are not tab stops, so Tab goes on to the step's first control.
- The reasons are a radio group named after the step title. Arrow keys move between reasons, and the group is a single tab stop.
- Enter runs the step's main action: Continue (once a reason is chosen), the offer's accept button, or Done. On an unchosen reason Enter chooses it first, and in the feedback field Enter adds a line (Ctrl+Enter or Cmd+Enter continues). An Enter that is still held from the previous step is ignored.
- Alt+Left goes back from the offer and the confirmation. The round back button is named "Back" and has a tooltip.
- The cancellation needs a deliberate hold: press and hold the button, or focus it and hold Space or Enter. Its help text says so.
- The collapsed feedback field is out of the tab order until a reason is chosen.

## Motion

- Steps slide 12px in the direction of travel (forward from the right, back from the left) with a short fade, and the card's height follows the new step on a spring.
- The feedback field opens and closes on a height spring. The step bar's segments cross-fade to their new colors, and the back button fades in and out as its space collapses.
- On the last step the check draws itself in about half a second.
- Reduced motion keeps only the fades: steps cross-fade in place, segment colors change quickly and the check is drawn at once. Off changes everything in place.

## Related

- The other Settings blocks: `SettingsPage`, `SettingsCommand`, `SecuritySettings`, `BillingOverview` (its "Cancel plan" button is a natural way in) and `UsageBilling`.
- `RadioGroup`, `Textarea`, `HoldToConfirm`, `Button` and `Dialog`, which the flow is built from.
- `ConfirmMorph` for a lighter in-place confirmation of smaller destructive actions.
