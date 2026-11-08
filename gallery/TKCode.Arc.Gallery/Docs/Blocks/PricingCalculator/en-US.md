---
title: Pricing calculator
summary: Plan cards priced by a seat slider and a billing switch: prices roll digit by digit as you drag, and the recommended badge glides to the plan that fits the team.
---

<Demo name="Preview" />

## When to use

`PricingCalculator` is a pricing section for products sold per seat. A slider sets the team size, a switch sets monthly
or yearly billing, and three plan cards show what each plan costs for that team: the price of one seat per month, the
total for the billing period and the features the plan includes at that size. A row at the bottom sums up the plan in
use, shows what yearly billing saves and offers a trial. Every number rolls to its new value digit by digit while the
slider moves, the "Best fit" badge glides to the plan that suits the team, and a frame follows the plan in use.

- **Seat-based SaaS pricing.** Per-seat prices with a yearly discount, volume discounts for big teams and a small plan
  with a seat cap.
- **A pricing page that answers "what would we pay?".** People see their own number instead of reading a table, and the
  recommendation tells them where to start.
- **An upgrade screen inside the product.** Start the slider at the team's current size and pick their plan, so the
  page shows what changes when the team grows.

## When not to use

- **Prices that depend on usage, not on seats.** Requests, storage or minutes are better shown with a usage estimate
  (see Usage pricing).
- **Feature-by-feature comparison.** When people compare dozens of features across plans, use a comparison table (see
  Plan comparison) and keep this block for the price.
- **Only a monthly / yearly choice.** A single price with a period switch needs only `BillingToggle` and `BillingPrice`.
- **Checkout.** The block ends with a trial button. Payment details, taxes and invoices belong to a checkout flow.

## Usage

With no settings the block shows its sample content: 23 seat stops from 1 to 300, three plans and a simulated trial. For
your product, give it your plans and stops.

```xml
<arc:PricingCalculator Title="Plans for every crew" Currency="USD" Billing="Yearly">
    <arc:PricingCalculatorPlan Id="crew" Name="Crew" Description="Dispatch and job notes for small teams"
                               MonthlySeatPrice="18" YearlySeatPrice="15" MaxSeats="16" RecommendFromSeats="1">
        <arc:PricingCalculatorFeature Text="Job board and map" />
        <arc:PricingCalculatorFeature Kind="PerSeat" PerSeatAmount="50" Text="{}{0} photo uploads a month" />
    </arc:PricingCalculatorPlan>
    <arc:PricingCalculatorPlan Id="fleet" Name="Fleet" MonthlySeatPrice="30" YearlySeatPrice="24" RecommendFromSeats="12">
        <arc:PricingCalculatorFeature Text="Everything in Crew" />
        <arc:PricingCalculatorFeature Kind="VolumeDiscount" />
        <arc:PricingCalculatorFeature Text="Quarterly route review" IsYearlyOnly="True" />
    </arc:PricingCalculatorPlan>
</arc:PricingCalculator>
```

```csharp
calculator.SeatStops = [1, 2, 3, 5, 8, 12, 16, 24, 32, 48, 64, 100, 150, 250, 500];
calculator.SetSeats(12);
calculator.TrialHandler = async (request, cancellation) =>
    await api.StartTrialAsync(request.Plan.Id, request.Seats, request.Billing, cancellation);
```

### Anatomy

- **Header.** `Title` and `Description`, centred; hide them with `IsHeaderVisible="False"` when the page has its own.
- **Controls.** "Team size" with round minus and plus buttons around the seat count, the slider with a mark for each
  plan where it starts being recommended, and the billing switch with the yearly discount next to "Annual".
- **Plan cards.** Name and the "Best fit" badge, a line of description, the seat price with "per seat / per month", the
  card total, the plan button and the feature list.
- **Selection frame.** A thin outline around the plan in use. It is a layer above the cards and glides between them.
- **Totals.** The plan and seats, the total with its period, the seat price line, the yearly savings (or, while billed
  monthly, a button that switches to yearly billing) and the trial button.

### How prices are worked out

Every rule is data, so the same block prices any seat-based product:

- The seat price per month is the plan's `MonthlySeatPrice`, or with yearly billing its `YearlySeatPrice`. A plan
  without a yearly price takes the monthly price less `YearlyDiscount` (20% by default).
- `VolumeTiers` take an amount off every seat from a number of seats on; the largest tier reached applies, in both
  billing periods.
- The card total is the seat price times the seats, and times 12 with yearly billing.
- The savings are what yearly billing saves in a year: (monthly price − yearly price) × 12 × seats. The volume discount
  applies to both prices, so the savings are the same in both periods.
- The recommended plan is the one with the largest `RecommendFromSeats` the seats reach. A seat count between two
  thresholds stays on the lower plan. Replace the rule with `RecommendPlan` when you need another one.

The computed values are available as `Seats`, `RecommendedPlan`, `SelectedPlan`, `Total` and `YearlySavings`.

### Picking a plan and seat caps

Until someone picks a plan, the plan in use is the recommended one, and it follows the slider. A plan picked with its
button stays in use however the slider moves (`PickedPlanId` is two-way). A plan with `MaxSeats` cannot be used above
its cap: its card dims its description and features, replaces the price with a short note ("Solo covers up to 8 seats",
"You chose 20 seats.") and its button offers to go back to the cap. Above the cap the plan in use falls back to the
recommendation, but the pick is kept: move back under the cap and the picked plan is in use again.

### Feature lines

`PricingCalculatorFeature` lines can follow the seats and the billing period:

- `Kind="PerSeat"` multiplies `PerSeatAmount` by the seats and puts the result into the `{0}` of `Text`
  ("500 GB file storage").
- `Kind="VolumeDiscount"` shows the plan's current volume discount, only while one applies.
- `MinSeats` shows a line from a number of seats on; `IsYearlyOnly` shows it only with yearly billing.
- `TextFactory` computes the text yourself; return null to hide the line.

Lines that appear open from zero height and fade in; lines that go fade out and close. Every card grows with its list.

### Your backend, events and the trial

`TrialHandler` receives the plan, the seats, the billing period and the total, and returns whether the trial started.
While it runs the trial button shows a spinner and ignores clicks; once it started, the button says "Trial is live" for
`TrialSuccessDuration` (2.8 s) and a screen reader hears which trial started. Without a handler the block waits 1.1 s
and succeeds, like the preview. `PlanPicked`, `SeatsChanged`, `BillingChanged` and `TrialStarted` report what people do;
`PlanPickedCommand` and `TrialStartedCommand` are the command versions.

<Demo name="Backend" />

### Narrow containers

The block reads its own width, not the window's. At 860 px or less the cards stack, and the cards not in use fold to
their name, price and button; the trial button moves under the totals. At 560 px or less the billing switch spans the
width and the totals stack.

<Demo name="Narrow" />

### Currency, culture and wording

`Currency` takes an ISO 4217 code and `Labels.Culture` formats every amount and count. Every sentence is in
`PricingCalculatorLabels`; sentences with a rolling amount, such as the savings line, are format strings with `{0}`.

<Demo name="Localized" />

### Reduced motion

With reduced motion the numbers, the frame, the badge and the billing thumb jump to their new place, folds and new rows
open at once, and text changes keep a short fade.

<Demo name="ReducedMotion" />

## API reference

<!-- api: PricingCalculator -->

<!-- api: PricingCalculatorPlan -->

<!-- api: PricingCalculatorFeature -->

<!-- api: PricingCalculatorVolumeTier -->

<!-- api: PricingCalculatorLabels -->

<!-- api: PricingCalculatorTrialRequest -->

<!-- api: PricingCalculatorPlanPickedEventArgs -->

<!-- api: PricingCalculatorSeatsChangedEventArgs -->

<!-- api: PricingCalculatorBillingChangedEventArgs -->

<!-- api: PricingCalculatorTrialStartedEventArgs -->

## Accessibility

- The slider is a slider to assistive technology, named "Team size", with the seat count ("20 seats") as its help text.
- The step buttons are named "Remove a seat" and "Add a seat"; the billing options are radio buttons in a group named
  "Billing period", the yearly one including its discount in its name.
- Each card is named after its plan, its feature list is named "Studio features", and every plan button is named after
  what it does ("Pick Scale", "Switch to 8 seats").
- Rolling numbers are drawn, but their full text ("$4,800") is their accessible name.
- A polite live region announces a picked plan ("Studio picked"), a change of billing and a started (or failed) trial.
  Moving the slider is not announced; the slider's own value is.
- Choosing "Switch to 8 seats" keeps focus on that button, which then reads "Current pick".

### Keyboard

| Key | Action |
| --- | --- |
| Tab | Minus, plus, slider, the chosen billing option, each plan button, the yearly offer (while billed monthly), the trial button. |
| ← / ↓, → / ↑ | On the slider: one stop down or up. On the billing switch: choose the other option and move focus to it. |
| Page Down / Page Up | On the slider: two stops down or up (10% of the range). |
| Home / End | On the slider: the first or the last stop. |
| Enter / Space | Press the focused button. |

Keyboard focus shows a 2 px ring outside the focused control; pointer focus shows none.

## Motion

| Element | Motion |
| --- | --- |
| Numbers | Every digit is a wheel. When the number grows, each wheel turns forward (new − old) mod 10 steps, through the digits in between; when it shrinks, backward. All wheels share the Smooth spring with no stagger. A glyph one step away fades to zero and blurs by 2 px. A new change carries on from where the wheels are, keeping their speed. |
| New digits | A new column (and its separator) grows from zero width and fades in on the Smooth spring; a column that goes shrinks and fades. |
| Selection frame | Follows the plan in use on a critically damped spring (visual duration 0.5 s). It chases the card's current size, so while cards fold and open it follows them. |
| Best fit badge | Starts where the old badge was and springs into place on the same spring, keeping its speed if the recommendation moves again. |
| Billing thumb | Moves and resizes under the chosen option on the Settle spring. |
| Text changes | The new text rises 0.35em while fading in (240 ms); the old text rises 0.3em while fading out (160 ms). |
| Feature rows and folds | Height on the Smooth spring; opacity 240 ms. |
| Plan marks | The recommended plan's mark turns to the text color in 240 ms. |

Reduced motion: numbers, the frame, the badge and the thumb jump, heights change at once, text changes only fade.

## Responsive behavior

- **Over 860 px:** three equal columns, every card as tall as the tallest; the billing switch sits to the right of the
  slider; the totals are one row.
- **860 px or less:** one column; cards not in use fold to name, price and button; the trial button spans the width
  under the totals.
- **About 600 px:** the billing switch moves under the slider.
- **560 px or less:** smaller title, tighter padding, the billing switch spans the width, the totals stack.
- **360 px or less:** the yearly offer and the savings line may wrap.

## Related

- Plan comparison: features of every plan in a table.
- Usage pricing: prices that follow usage instead of seats.
- `BillingToggle` and `BillingPrice`: a period switch and a price that rolls.
- `AnimatedCounter`: a single rolling number.
