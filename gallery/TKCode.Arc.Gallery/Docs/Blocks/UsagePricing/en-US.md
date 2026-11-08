---
title: Usage pricing
summary: A pricing calculator that picks the plan for you: drag seats and monthly traffic and the plan name morphs, the estimate counts to the new total and the call to action follows.
---

<Demo name="Preview" />

## When to use

`UsagePricing` is the "what will I pay" section of a pricing page for a product billed per seat with a metered part on
top. People set two numbers they already know, how many seats and roughly how much monthly usage, and the block does the
rest: it picks the plan that fits, explains why in one line, shows the monthly estimate and breaks it down. The call to
action always names the recommended plan.

- **Seat plus usage pricing.** API platforms, analytics, messaging, storage: a price per seat, an amount of usage
  bundled with each plan and a flat rate for usage beyond it.
- **Plans separated by limits.** Each plan is recommended up to a number of seats and an amount of usage. The block picks
  the first plan whose limits cover both values, so a team that grows past either limit sees the next plan.
- **Monthly versus annual.** The switch shows what annual billing saves on seats, adds a discount row to the breakdown
  and rolls the estimate down.

## When not to use

- **Fixed tiers compared side by side.** When people should compare plans feature by feature, use the Plan comparison block.
- **Forecasts for existing customers.** A customer who already pays wants their own usage projected; use a usage forecast
  or `UsageBilling` on the billing page.
- **Pricing with more than two drivers.** The block has exactly two sliders. Add-ons, regions or support tiers belong
  in a configurator or a checkout.
- **Cheapest-plan search.** The recommendation follows each plan's limits, not the lowest total. If your plans overlap so
  that a bigger plan can be cheaper, set the limits accordingly or present the plans in a comparison instead.

## Usage

The block derives from Avalonia's `TemplatedControl`. Without any configuration it shows the sample: three plans (Spark,
Studio and Fleet), 23 seat stops and 27 monthly call stops, billed in US dollars.

```xml
<arc:UsagePricing SeatIndex="10" UsageIndex="14" BillingPeriod="Monthly" />
```

Your own plans go in `Plans`, smallest first. Each `UsagePricingPlan` has a seat price, the usage it bundles, the price of
each `OverageUnit` of usage beyond that, the yearly discount per seat, the limits it is recommended up to and its
features. The last plan usually has no limits.

```xml
<arc:UsagePricing Currency="EUR">
    <arc:UsagePricingPlan Name="Solo" SeatPrice="9" YearlySeatDiscount="1"
                          IncludedUsage="1000000" OveragePrice="6" MaxSeats="3" MaxUsage="2000000">
        <arc:UsagePricingFeature Key="history" Label="Event history" Value="7 days" />
    </arc:UsagePricingPlan>
    <arc:UsagePricingPlan Name="Team" SeatPrice="19" YearlySeatDiscount="3"
                          IncludedUsage="10000000" OveragePrice="3">
        <arc:UsagePricingFeature Key="sso" Label="Single sign-on" />
        <arc:UsagePricingFeature Key="history" Label="Event history" Value="90 days" />
    </arc:UsagePricingPlan>
</arc:UsagePricing>
```

The sliders step through `SeatStops` and `UsageStops`. Stops sit evenly along the track whatever their values, so a list
that grows by a factor at each step gives a logarithmic feel. `SeatIndex` and `UsageIndex` are the selected stops and
bind two-way; `Seats` and `Usage` read the values.

### Anatomy

1. **Heading and description**: `Labels.Title` and `Labels.Description`.
2. **Seat slider and usage slider**: Arc `Slider`s with the value at the top right, a bubble on the thumb while it is
   dragged or focused, and a labeled mark where each plan after the first begins.
3. **Billing switch**: a `SegmentedControl` with Monthly and Annual, and the yearly saving next to it. Once annual
   billing applies, a check grows in front of the saving and the text turns green.
4. **Breakdown**: the seats row ("Studio seats", 16 × $26), the usage row (bundled, or the extra usage and its rate),
   the annual discount row while billed yearly, and the estimated total.
5. **Recommendation card**: a meter with one segment per plan, the plan name, the reason, the monthly estimate, the
   billing note, the feature list and the trial button with its note.

### The recommendation and its reason

The recommended plan is the first plan whose `MaxSeats` and `MaxUsage` both cover the selection (a null limit covers
everything). The reason line compares the selection with the previous plan's limits and has four forms:

| Reason | When | Default text |
|---|---|---|
| `WithinLimits` | The first plan | "Fits up to 4 seats and 1.5M calls a month" |
| `SeatsAbove` | Only the seats pass the previous plan | "Suggested beyond 36 seats" |
| `UsageAbove` | Only the usage passes the previous plan | "Suggested beyond 20M calls a month" |
| `BothAbove` | Both pass the previous plan | "Suggested beyond 4 seats and 1.5M calls" |

The reason can change while the plan stays, for example from 4 to 5 seats inside Studio; then only the sentence swaps.
`RecommendationChanged` fires for both kinds of change.

### The estimate

| Value | Formula |
|---|---|
| Seats row | seats × seat price |
| Usage row | max(0, usage − bundled) ÷ `OverageUnit` × overage price; "Bundled" when nothing is over |
| Annual discount | seats × yearly discount per seat (shown and subtracted only when billed yearly) |
| Estimated total and card price | seats row + usage row − annual discount |
| Yearly saving | seats × yearly discount × 12 (seats only; usage plays no part) |
| Annual billing note | seats × (seat price − discount) × 12 |

Amounts are whole, use the culture of `Labels.Culture` and the currency of `Currency`, and negative amounts carry a real
minus sign ("−$80").

### Own data and events

<Demo name="Live" />

The demo prices a storage product per editor and per 100 GB (`OverageUnit = 100`, `UsageFormatter` writes GB and TB),
replaces the wording and starts trials through a simulated sign-up that refuses the largest plan. `SelectionChanged`,
`RecommendationChanged`, `TrialStarted`, `TrialUndone` and `TrialFailed` are routed events with typed arguments.

### The trial

The button reads "Try {plan}". Pressing it turns `IsTrialStarted` on: the button becomes secondary with a check and
"Trial is on", and the note under it becomes "Trial is on. Undo". The trial belongs to the plan it was started on:
moving the sliders within that plan keeps it, and a different recommendation turns it off. Undo turns it off and gives
the keyboard focus back to the button.

Set `TrialHandler` to call your sign-up flow. The button shows its spinner while the task runs; the trial turns on when it
completes, and stays off (with `TrialFailed`) when it throws.

```csharp
pricing.TrialHandler = async (request, cancellation) =>
    await api.StartTrialAsync(request.Plan.Key, request.Seats, request.Usage, cancellation);
```

`StartTrialAsync()` and `UndoTrial()` do the same from code.

### Narrow containers

<Demo name="Narrow" />

From 780px wide the card has its own 392px column on the right. Narrower, the heading shrinks and the card moves between
the controls and the breakdown.

### Currency, culture and wording

<Demo name="Localized" />

Every sentence is an entry of `UsagePricingLabels`. Sentences that carry a rolling amount or the morphing plan name
("{amount} saved per year", "{plan} seats", "{amount} per year for seats, calls monthly") may place the value anywhere:
the block splits the sentence around it.

### Reduced motion

<Demo name="ReducedMotion" />

### Content guidelines

- Name plans with short words; the name appears on the card, in the meter, in the seats row and in the button.
- Keep the reason sentences short enough for one line at 392px.
- List the features that change between plans. Lines new in a plan come first and are drawn strong; lines inherited
  from the previous plan follow in its order and are drawn softer.
- Write stops people recognize: round team sizes and usage amounts that match your plans' limits.

### Best practices

- Make each plan's limits meet the next plan's: the first stop past a limit is where the next plan's mark sits.
- Use the same unit for `IncludedUsage`, `MaxUsage`, the usage stops and `OverageUnit`.
- Keep the estimate honest: the total is before tax, as the total row says.

### Common questions

**Why is a bigger plan recommended when a smaller one would be cheaper?** The recommendation follows the limits you set,
not the price. Raise a plan's limits if it should be recommended longer.

**Can the sliders start somewhere else?** Set `SeatIndex` and `UsageIndex`, or bind them to your view model.

**Can I send people to a sign-up page instead of starting a trial in place?** Set `TrialHandler` to open your sign-up
flow; the trial turns on when it returns and stays off when it throws.

## API reference

<!-- api: UsagePricing -->

### UsagePricingPlan

<!-- api: UsagePricingPlan -->

### UsagePricingFeature

<!-- api: UsagePricingFeature -->

### UsagePricingPeriod

<!-- api: UsagePricingPeriod -->

### UsagePricingReason

<!-- api: UsagePricingReason -->

### UsagePricingRecommendationChangedEventArgs

<!-- api: UsagePricingRecommendationChangedEventArgs -->

### UsagePricingSelectionEventArgs

<!-- api: UsagePricingSelectionEventArgs -->

### UsagePricingTrialRequest

<!-- api: UsagePricingTrialRequest -->

### UsagePricingLabels

<!-- api: UsagePricingLabels -->

### UsagePricingSamples

<!-- api: UsagePricingSamples -->

## Accessibility

- The block is a region named by its heading; the card is a complementary landmark named by the plan.
- Each slider is named by its label and reads the formatted value ("16", "12M"), with the stop index as its value.
- The billing switch is named "Billing period"; each segment reads its label and whether it is selected.
- The feature list is named "Included in {plan}"; each line reads its label and value. The meter is hidden from
  assistive technology because the plan name says the same.
- A polite live region says "{plan}. {reason}." whenever the plan or the reason changes.
- The trial button is described by the note under it. After Undo the focus returns to the button.

### Keyboard

| Key | Action |
|---|---|
| Tab | Seat slider, usage slider, billing switch, trial button, then Undo while the trial is on |
| Left / Down, Right / Up | One stop down or up |
| Page Down / Page Up | Three stops down or up |
| Home / End | First or last stop |
| Left / Right on the switch | Monthly or Annual, selected at once |
| Enter / Space | Start the trial, or undo it |

## Motion

| Element | Motion |
|---|---|
| Slider thumb | The slider's own spring; the value and bubble digits roll with an eased fade |
| Amounts | Each digit turns through the digits in between on the Smooth spring, fading and blurring up to 2px away from the line; new digits and separators grow in |
| Plan name, "{plan} seats", feature values | Text morph: kept letters slide, new letters rise in, old letters fall away |
| Reason, billing note, trial note, usage value | Old text fades up over 120ms, new text rises 6px over 200ms |
| Meter | Bars fill from the left on a soft Smooth spring; the current name turns strong over 240ms |
| Feature list | Kept lines slide to their new place, new lines fade in from 8px above over 240ms, leaving lines fade in place over 120ms; the list's height follows on a spring |
| Annual billing | The switch's pill glides, the saving's check slot opens on the Smooth spring and its stroke draws over 350ms, the text turns green over 240ms, the discount row opens and fades in |
| Trial | The button changes color, its label swaps with a drawn check and its width follows on the Smooth spring |

Reduced motion keeps the colors and the short fades of new lines and swapped sentences; numbers, morphs, the list's
slide and height and the meter jump. With motion off everything jumps.

## Responsive behavior

The block uses its own width, not the window's. From 780px it lays out two columns (the card is 392px); below, it stacks
heading, controls, card and breakdown, with a 28px heading and tighter card padding. The billing row wraps when the saving
does not fit beside the switch. The block has no maximum width: give it the width your page allows.

## Related

- Plan comparison: fixed plans compared feature by feature.
- Pricing calculator: plan cards priced by a seat slider.
- `UsageBilling`: the current period's usage for existing customers.
- `Slider`, `SegmentedControl`, `TextMorph`: the controls the block is built from.
