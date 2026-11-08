---
title: Plan comparison
summary: Plan cards with a monthly or yearly switch that rolls every price to the new period, a highlighted plan, and a feature matrix that shows what each tier adds.
---

<Demo name="Preview" />

## When to use

Plan comparison puts two or three plans side by side so people can see what changes between them and what each one
costs. Switching the billing period counts every price to its new amount, choosing a plan tints its column, and one
switch hides the features every plan shares, so only the differences are left.

- **A pricing page with two or three tiers**: each plan gets a column with its price per seat and a select button, and
  each feature row shows what every tier includes.
- **Monthly and yearly prices**: the billing switch counts each price to the other period, and the note under it gives
  the largest yearly saving, worked out from your prices.
- **Long feature lists**: "Show differences only" collapses the rows every plan shares, and the count beside the filter
  says how many features are still listed.
- **An upgrade screen inside an app**: select a plan for the person who is signed in and listen for the selection to
  move them on to checkout.

## When not to use

- **The price depends on seats or traffic**: use a calculator, such as `UsagePricing` or `PricingCalculator`, which
  works the price out from what someone drags.
- **Only a billing toggle**: use a `SegmentedControl` on its own.
- **Downgrade or cancellation**: use `CancelFlow`, which answers a reason with an offer.
- **More than three or four plans**: the columns get too narrow to read. Show the plans as cards and link to a full
  comparison table.

## Usage

With no plans and features, the block shows the sample offering from Arc's preview: a Team and a Studio plan and eight
features. Give it your own through `Plans` and `Features` (or bind `PlansSource` and `FeaturesSource`, which win when
set). Each feature lists one value per plan, in the order of the plans:

```xml
<arc:PlanComparison Currency="EUR" Billing="{Binding Billing}" SelectedPlan="{Binding Plan}">
    <arc:PlanComparison.Plans>
        <arc:PlanComparisonPlan Name="Notebook" Description="For one field researcher" MonthlyPrice="8" YearlyPrice="6" />
        <arc:PlanComparisonPlan Name="Station" Description="For a survey crew" MonthlyPrice="19" YearlyPrice="15" />
    </arc:PlanComparison.Plans>
    <arc:PlanComparison.Features>
        <arc:PlanComparisonFeature Label="Offline maps" IsShared="True">
            <x:String>Included</x:String>
            <x:String>Included</x:String>
        </arc:PlanComparisonFeature>
        <arc:PlanComparisonFeature Label="Observers">
            <x:String>1 person</x:String>
            <x:String>Up to 12</x:String>
        </arc:PlanComparisonFeature>
    </arc:PlanComparison.Features>
</arc:PlanComparison>
```

### Your own plans and an event log

Three plans priced in euros, starting with only the differences listed. `SelectedPlanChanged` reports every change of
the selection (`IsUserAction` tells a button press from the app), `BillingChanged` every change of the period, and
`SelectCommand` runs when someone presses a select button.

<Demo name="OwnPlans" />

### The block's own card

By default the block draws a card: a border, the panel radius, a surface and lines between its sections, at most
1060 px wide and centered. Set `IsFramed="False"` to set it flush into a page section, as Arc's preview does.

<Demo name="Card" />

### Values

A value equal to `Labels.Included` ("Included") gets a green check; a value equal to `Labels.NotIncluded`
("Not included") is muted. Any other text is shown as written. Mark a feature `IsShared` when every plan offers the
same, so the filter can hide it.

### Text, culture and currency

Every string lives in `PlanComparisonLabels`: replace the whole object to translate the block. Sentences with a number
or a plan name are functions. Prices use `Labels.Culture` and the ISO code in `Currency`; whole prices show without
cents, and prices with cents count in cents.

<Demo name="Localized" />

### Narrow containers

<Demo name="Narrow" />

### Reduced motion

<Demo name="ReducedMotion" />

### Content guidelines

- Keep plan names to one word and descriptions to a few words, so the price and the button line up across columns.
- Write values that compare: "30 days" against "Unlimited" reads faster than two sentences.
- Put the features that differ most near the top; shared features can stay, the filter hides them.
- Say in the footnote what the price covers: currency, per seat, tax and how yearly plans are billed.

### Best practices

- Bind `SelectedPlan` two-way and start checkout from `SelectedPlanChanged` or `SelectCommand`; pressing the selected
  plan's button again clears the choice.
- Keep two to three plans. Every plan adds a column that must fit beside the feature names.
- Keep the yearly prices per month. The note works out "save up to N%" from your monthly and yearly prices.

## API reference

<!-- api: PlanComparison -->

<!-- api: PlanComparisonPlan -->

<!-- api: PlanComparisonFeature -->

<!-- api: PlanComparisonSelectionChangedEventArgs -->

<!-- api: PlanComparisonBillingChangedEventArgs -->

## Accessibility

- The matrix is named after its plans ("Team and Studio plan comparison"). Each plan header is named "Team plan", each
  value is named with its plan ("Team: 30 days"), and each row with its feature.
- Prices are a polite live region whose name is always the final price, so a screen reader never reads the numbers a
  price counts through.
- The chosen plan is announced politely ("Team selected"). The select button keeps the name "Select Team" and reports
  "Selected" as its status.
- The billing switch is named "Billing period" and the filter switch "Show differences only".

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the billing switch, the filter switch and the select buttons |
| `Left` / `Right` | Choose the billing period in the billing switch |
| `Space` | Turn the filter on or off |
| `Enter` / `Space` | Select a plan, or clear it when it is already selected |

Keyboard focus shows a 2 px ring around the focused control; pointer focus does not.

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| Billing switches | Each price counts to the new amount in 240 ms on the enter curve | Jumps | Jumps |
| Feature count changes | Counts in 240 ms | Jumps | Jumps |
| A plan is selected | The column tints in 240 ms; the rail appears in place | Tint and rail at once | At once |
| The selection moves | The rail glides on Arc's responsive spring (stiffness 520, damping 44) | Moves at once | Moves at once |
| Rows hide | Height collapses on the smooth spring (0.4 s, no bounce), fading out in 160 ms | Gone at once | Gone at once |
| Rows return | Height opens on the smooth spring, fading in over 240 ms after 40 ms | Back at once | Back at once |

A count that is interrupted continues from the number on screen.

## Responsive behavior

- The layout follows the block's own width, not the window's. At 720 px or less (`:compact`) the billing switch stacks
  under the heading and the gutters tighten to 20 px.
- At 540 px or less (`:narrow`) the feature column goes: each feature name sits above its values, which stay under their
  plan columns, and the heading and prices get smaller.
- At 340 px or less (`:tiny`) the padding of plans and values tightens to 12 and 16 px so both plans still fit side by
  side.

## Related

| Component | Choose it when |
|---|---|
| `PlanComparison` | People weigh a few fixed plans side by side |
| `UsagePricing` | The plan follows from seats and traffic |
| `PricingCalculator` | Prices depend on a seat count and roll as it changes |
| `SegmentedControl` | You only need the billing toggle |
| `CancelFlow` | Someone is downgrading or cancelling |
