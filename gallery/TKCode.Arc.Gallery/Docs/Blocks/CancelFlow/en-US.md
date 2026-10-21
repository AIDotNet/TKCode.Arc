---
title: Cancel flow
summary: A considerate cancellation: ask why, offer a fitting alternative, show what ends, hold to confirm, and allow a quick return.
---

<Demo name="Flow" />

## When to use

`CancelFlow` is the card that opens when a person presses "Cancel plan". It sits on a billing or plan page, or in a
dialog launched from one, and turns a cancellation into four short steps: the reason, an offer that fits that reason,
what ends and when, and a confirmation the person must hold. The host decides what each step does; the flow only
collects the answers and reports them.

- **The cancel action on a team plan page**: an admin presses "Cancel plan" on the Team plan and the flow asks why,
  so the team can learn whether price, usage or a missing feature drove the decision.
- **Subscriptions where an offer often fits better than leaving**: a discount for a price complaint or a pause for
  low usage. The offer appears only after the reason that it answers, so it does not distract people who have already
  decided.
- **Irreversible plan changes that need a clear summary**: the confirmation lists what stops working (read-only
  projects, deleted history, lost custom domains) and the date when access ends, before the person can confirm.
- **Account settings that must offer a way back**: after a confirmed cancellation the canceled step offers
  "Reactivate plan", so the change is easy to undo without contacting support.
- **Flows that must be recorded**: the events carry the chosen reason and the free-text feedback, so the answers can
  go to your analytics or your support queue.

## When not to use

- **Removing one item or one seat in place**: use `ConfirmMorph` or `HoldToConfirm` next to the item. A full flow is
  too heavy for a small change that the person can reverse.
- **Moving to another paid plan**: show the options side by side with `RadioCards` or a pricing table. The flow is for
  leaving, and a downgrade should not be framed as a cancellation.
- **Cases where the law or your terms require one-click cancellation**: keep the offer out of the way by leaving
  `DefaultOffer` null and not giving reasons an `Offer`, or link straight to the confirmation your terms describe.
- **A lighter confirmation of a small destructive action**: use `ConfirmMorph`, which asks once in place and needs no
  reasons or offers.
- **Collecting a long free-form survey**: use `Textarea` inside your own form. The flow asks one optional question
  after the reason and does not paginate a survey.

## Usage

```csharp
flow.AccessEndsOn = new DateTime(2026, 11, 1);

flow.CancellationConfirmed += (_, e) => billing.Cancel(e.Reason.Title, e.Feedback);
flow.OfferAccepted += (_, e) => billing.Apply(e.Offer.Title);
flow.KeepPlanRequested += (_, _) => page.CloseCancelFlow();
```

`CancelFlow` derives from Avalonia's `TemplatedControl`. It is one card that holds the step bar, the step content and the
buttons, and it swaps the content between steps. Each step's controls are built once and reused. The flow is built from
`RadioGroup`, `Textarea`, `HoldToConfirm` and `Button`, so it inherits their focus rules and keyboard behavior. The preview gives two reasons their own offer ("Take 40% off for 3 months" and "Pause instead"), and the
other reasons fall back to the default offer. `PlanName` and `AccessEndsOn` fill in the sentences.

### Anatomy

The card has 28 px of inner padding (20 by 22 px below 440 px wide, `NarrowWidth`) and a preferred width of 520 px. From top to bottom:

1. **Step bar** (`StepStorePart`): three segments for the path Reason, Offer and Confirm, or two segments when the
   path has no offer. The segments are 4 px high with a 6 px gap. The current segment uses the accent color, finished
   segments use the foreground color at 30 percent, and the rest use the subtle border color. The step counter
   "Step {n} of {total}" sits to the right of the back button.
2. **Back button** (`BackButtonPart`): a 32 px round button named "Back". It appears on the offer and confirmation steps.
   Alt+Left does the same.
3. **Step title and description**: 28 px title line
   height, with the description 6 px below it. The title takes focus after each step change, but it is not a tab stop.
4. **Reason list**: a `RadioGroup` in which each row has a title and an optional description. Below it, the feedback
   `Textarea` opens with a height spring after a reason is chosen. It is optional and out of the tab
   order while it is collapsed.
5. **Offer tile**: a 52 px rounded tile (16 px radius) with a 24 px icon, the offer title and its description. The
   accept button and "Continue canceling" follow.
6. **Losses box**: a panel with 16 by 14 px padding and a 16 px radius, listing each loss with a
   10 px gap between lines. It is hidden when `Losses` is empty.
7. **Hold button**: a `HoldToConfirm` in the danger color labeled "Hold to cancel plan", with "Keep my
   plan" next to it.
8. **Result mark**: a 56 px mark with a 28 px check glyph, the result title and its sentence, shown on the
   canceled and retained steps. The text is limited to 380 px wide.

The action row starts 28 px below the content. Below 440 px, the buttons stack and the secondary button moves under the
primary one.

### Choosing reasons and offers

Write a reason as the person would say it, and give each one an offer only when a concrete alternative exists. A
reason without an offer goes straight to the confirmation, so an offer that does not match the reason makes the flow
look like a sales step. Use `DefaultOffer` for a general alternative such as a call with your team, and keep it for
reasons that have no specific answer.

Set `AsksForDetails` on a reason when a follow-up question makes the feedback useful, such as "What were you missing?"
for a missing feature. The answer is still optional, and the question replaces the general prompt only after that
reason is chosen.

### Without an offer

When neither the chosen reason nor `DefaultOffer` has an offer, the flow goes from the reason straight to the
confirmation, and the step bar has two segments instead of three. With `Reasons` left empty, the flow uses the five
reasons in `Labels.DefaultReasons`. The demo below starts with a reason that asks a follow-up question, so the field
reads "What were you missing?".

<Demo name="NoOffer" />

### In a dialog

Set `Appearance` to `Plain` to drop the card's frame, shadow and padding when the flow sits inside a `Dialog`. The
dialog closes on `KeepPlanRequested`, which both "Keep my plan" and "Done" raise. "Reactivate plan" raises
`ReactivateRequested` and then starts the flow over; mark the event handled to keep the canceled step on screen. An
offer without an `Icon` shows a gift.

<Demo name="InDialog" />

### Binding to a view model

The flow keeps the answers, and the view model keeps the business rules. In an MVVM app with CommunityToolkit.Mvvm,
bind the reasons, the losses and the plan facts, and handle the outcomes in the view model. The billing service is
the app's own placeholder.

```csharp
public partial class CancelPlanViewModel : ObservableObject
{
    private readonly IBillingService _billing;

    public CancelPlanViewModel(IBillingService billing)
    {
        _billing = billing;
    }

    [ObservableProperty]
    private string? _planName = "Team";

    [ObservableProperty]
    private DateTime? _accessEndsOn = new DateTime(2026, 11, 1);

    [ObservableProperty]
    private CancelFlowReason? _selectedReason;

    [ObservableProperty]
    private string? _feedback;

    public ObservableCollection<CancelFlowReason> Reasons { get; } = new();

    public ObservableCollection<string> Losses { get; } = new();

    [RelayCommand]
    private async Task CancelConfirmedAsync(CancelFlowCancelEventArgs e)
    {
        await _billing.CancelAtPeriodEndAsync(e.Reason.Title, e.Feedback);
    }
}
```

```xml
<arc:CancelFlow PlanName="{Binding PlanName}"
                AccessEndsOn="{Binding AccessEndsOn}"
                Reasons="{Binding Reasons}"
                Losses="{Binding Losses}"
                SelectedReason="{Binding SelectedReason, Mode=TwoWay}"
                Feedback="{Binding Feedback, Mode=TwoWay}" />
```

`SelectedReason` and `Feedback` are two-way, so the view model sees the answers as the person gives them. `PlanName`,
`AccessEndsOn`, `Reasons`, `Losses`, `DefaultOffer` and `Labels` are read when they change. The flow has no commands:
connect `CancellationConfirmed`, `OfferAccepted` and `KeepPlanRequested` in code-behind and forward them to the view
model. Read `Step` (read-only) when you need to know where the person is.

### Focus and dismissal

After every step change, focus moves to the new step's title, and the title is announced politely. Keep the flow in
its dialog or card until the person leaves it. Close the dialog on `KeepPlanRequested` only; `OfferAccepted` moves the flow to the retained step, and the person still needs "Done" to
leave it. Call `Reset()` to start over from the first step, which clears the reason and the feedback. `GoBack()`
returns `false` on the reason and retained steps, where nothing precedes them.

### Text and other languages

Every built-in string is in `CancelFlowLabels`. Replace the whole object to reword or translate the flow; dates follow
`Labels.Culture`. The defaults are English, such as "Why are you canceling?" and "Hold to cancel plan". The table in
the API reference lists every default.

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

Sentences that contain the plan name or a date are built by the labels from the plan name and the formatted date, so
translate the whole sentence, not its pieces. Offer titles and reasons are your content, so translate them in your own
data.

### Example: Team plan cancellation

An admin cancels the Team plan from account settings. The flow shows three reasons; the first two have no matching offer
yet, so the default offer is "Talk to us first". The plan name and the access end date come from the plan data, and the
step bar shows three segments because the path can include an offer. The first step is shown with "Continue" disabled, which
is the state the person sees before choosing a reason.

The design decision here is to ask before offering. A reason with an offer appears only after it is chosen, so the
offer answers the reason rather than interrupting it. The Continue button stays disabled until a reason is chosen, so
the flow never records an empty answer.

![The first step of a Team plan cancellation, before a reason is chosen](images/TeamCancellation.png)

```xml
<StackPanel Width="560">
    <arc:CancelFlow PlanName="Team" AccessEndsOn="2026-11-01">
    <arc:CancelFlow.Reasons>
        <arc:CancelFlowReason Title="Too expensive"
                              Description="The price is more than it is worth to us right now." />
        <arc:CancelFlowReason Title="Not using it enough"
                              Description="We do not open it often enough to keep paying for it." />
        <arc:CancelFlowReason Title="Missing a feature I need"
                              Description="Something we rely on is not there yet."
                              AsksForDetails="True" />
    </arc:CancelFlow.Reasons>
    <arc:CancelFlow.DefaultOffer>
        <arc:CancelFlowOffer Title="Talk to us first"
                             Description="Fifteen minutes with our team is often enough to fix what is not working."
                             AcceptLabel="Book a 15-minute call" />
    </arc:CancelFlow.DefaultOffer>
</arc:CancelFlow>
</StackPanel>
```

### Content guidelines

Write the flow as a conversation with the person who is leaving. Be direct about what ends, and do not guilt or
pressure them. The defaults already follow these rules.

- Use the plan's name in every sentence that refers to it, such as "Cancel your Team plan?", so the person knows
  which subscription is affected.
- Give the access end date with the month name, as in "You keep access until Nov 1, 2026." Do not write "end of the
  month" when the date is known.
- Write reasons in the person's own words, short enough for one line: "Too expensive", not "Price sensitivity".
- Write offer titles as the benefit with its size, such as "Take 40% off for 3 months", and the accept button as the
  action, such as "Claim 40% off".
- Write the hold button as a verb phrase with the object: "Hold to cancel plan". Do not write "Confirm" alone, because
  the person must hold the control, and the label should say what happens.
- Keep the losses in plain terms that name the consequence: "Shared projects become read-only for all 6 members".

| Instead of | Write |
|---|---|
| Are you sure? | Cancel your Team plan? |
| Click to confirm cancellation | Hold to cancel plan |
| We are sorry to see you go | Your plan is canceled |
| Reason: other | Something else |
| Offer expires soon | Take 40% off for 3 months |

### Best practices

**Do**

- Set `AccessEndsOn` from the billing period, so the confirmation and the result show the real end date. An empty date
  makes the flow promise access that the server may not give.
- Show the offer only when it is real and can be applied. The flow moves to the retained step as soon as the person
  accepts, without waiting for your code, so apply the offer reliably in `OfferAccepted`.
- Record the reason and the feedback with `CancellationConfirmed`, even if the cancellation later fails, so the
  support team sees what the person said.
- Keep `Losses` to the consequences that apply to this account, and leave it empty when nothing is lost, rather than
  listing generic warnings.
- Use `Appearance="Plain"` inside a `Dialog`, so the dialog does not draw a second frame around the flow.

**Avoid**

- Hiding the way out. Keep "Keep my plan" visible on the reason and confirmation steps; a flow that traps the person
  leads to chargebacks and complaints.
- Offers for reasons they do not match. A discount after "Missing a feature I need" reads as a sales attempt and
  lowers the trust in the rest of the flow.
- Changing the plan in `CancellationConfirmed` before your server has confirmed it. Mark the plan as canceled only
  after the server accepts the request.
- Removing the hold. The hold is what separates a deliberate cancellation from an accidental click, so replacing it
  with a single button defeats the purpose of the step.
- Translating the labels but leaving the reason titles and offers in English. The flow then mixes two languages on one
  card, so translate your content with the labels.

### Common questions

**Why is Continue disabled on the first step?** The flow needs a reason before it can move on, so `SelectedReason` must
be set. Choose a row with the arrow keys, or set `SelectedReason` in code.

**Why does the step bar show two segments?** The path has no offer: the chosen reason has none, and `DefaultOffer` is
null. Set `DefaultOffer` or give the reason an `Offer` to add the third segment. Before a reason is chosen, the bar
counts the path as having an offer when any reason has one.

**Why did my "Reactivate plan" button restart the flow?** The flow calls `Reset()` after `ReactivateRequested` unless a
handler marks the event handled. Set `e.Handled = true` in the handler to keep the canceled step on screen while you
restore the plan.

**How do I start the flow again from the first step?** Call `Reset()`. It clears the reason and the feedback and returns
to the first step. `GoBack()` goes back one step from the offer or confirmation, and returns `false` on the other steps.

**Does the flow cancel the subscription?** No. It reports `CancellationConfirmed` with the reason and feedback, and your
code calls the billing API. Update `Losses`, `AccessEndsOn` or the page's plan state when the server answers.

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

`CancelFlowStep`

| Member | Shown when |
|---|---|
| `Reason` | The first step: the reason and the optional feedback |
| `Offer` | The offer for the chosen reason or `DefaultOffer` |
| `Confirm` | What ends, with the hold button |
| `Canceled` | The end of a confirmed cancellation, with "Reactivate plan" |
| `Retained` | The end of a flow where the offer was accepted and the plan stays |

## Accessibility

- After every step change, focus moves to the new step's title, which is announced politely. Titles take focus but are
  not tab stops, so Tab goes on to the step's first control.
- The reasons are a radio group named after the step title ("Why are you canceling?"). The arrow keys move between
  reasons, and the group is a single tab stop.
- The cancellation needs a deliberate hold: press and hold the button, or focus it and hold Space or Enter. Its help text
  says so.
- The round back button is named "Back" and has a tooltip. The collapsed feedback field is out of the tab order until a
  reason is chosen.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the step's controls in order |
| `Up` / `Down` | Move between reasons in the radio group |
| `Enter` | Press the step's main action: Continue, the offer's accept button or Done. On an unchosen reason, Enter chooses it first |
| `Enter` in the feedback field | Adds a line |
| `Ctrl+Enter` / `Cmd+Enter` in the feedback field | Continue |
| `Alt+Left` | Go back from the offer and the confirmation |
| Hold `Space` or `Enter` on the hold button | Confirms the cancellation (`HoldToConfirm` behavior) |

An Enter key still held from the previous step does not press the next step's button.

## Motion

- Steps slide 12 px in the direction of travel (forward from the right, back from the left) with a short fade, and the
  card's height follows the new step on a spring.
- The feedback field opens and closes on a height spring. The step bar's segments cross-fade to their new colors, and
  the back button fades in and out as its space collapses.
- On the last step the check draws itself in about half a second.
- Reduced motion keeps only the fades: steps cross-fade in place, segment colors change quickly and the check is drawn
  at once. Off changes everything in place.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Step changes | Slides 12 px with a fade; the card height springs to the new step | Steps cross-fade in place | Immediate |
| Feedback field opens or closes | Height opens on a spring | Shown or hidden without the height spring | Immediate |
| Step bar segment changes | Colors cross-fade | Colors change quickly | Immediate |
| Back button appears or leaves | Fades while its space collapses | Fades in place | Immediate |
| Result check on the last step | Draws in over about half a second | Drawn at once | Drawn at once |

## Related

- The other Settings blocks: `SettingsPage`, `SettingsCommand`, `SecuritySettings`, `BillingOverview` (its "Cancel plan"
  button is a natural way in) and `UsageBilling`.
- `RadioGroup`, `Textarea`, `HoldToConfirm`, `Button` and `Dialog`, which the flow is built from.
- `ConfirmMorph` for a lighter in-place confirmation of smaller destructive actions.

| Component | Choose it when |
|---|---|
| `BillingOverview` | The cancel button lives on the billing page, and the flow opens from `CancelPlanRequested` |
| `ConfirmMorph` | A small destructive action needs one confirmation in place |
| `HoldToConfirm` | A single irreversible action needs a deliberate hold, without reasons or offers |
| `RadioCards` | The person chooses between plans or options rather than leaving |
| `Dialog` | The flow runs in a modal window, with `Appearance="Plain"` |
| `Textarea` | A free-form answer is needed outside a cancellation flow |
