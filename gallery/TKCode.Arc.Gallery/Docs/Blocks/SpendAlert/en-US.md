---
title: Spend alert
summary: A budget with spending alerts: what was spent against the budget and the period's pace, a meter with a tick for every alert, an editor for the budget and the alerts, and a banner when spending crosses one.
---

<Demo name="Preview" />

## When to use

`SpendAlert` keeps one budget honest. It shows what was spent in the period against the budget, how much is left and how
many days remain, and where the period will end at the current pace. A meter marks every alert the person set, for example
at 50%, 80% and 100% of the budget. Under it, the budget and the alerts can be edited: switch an alert on or off, move it,
remove it or add one. When spending rises past a switched-on alert, a banner explains what happened and the app is told
through an event, so it can send the matching notification.

- **Personal finance**: a monthly budget per category with notifications.
- **Team or cloud spend** with thresholds that page someone.
- **Any metered limit** where people choose their own warning points.

## When not to use

- **Many budgets at once**: use the Budget variance block, which compares departments on one scale.
- **Usage over time with a forecast curve**: use the Usage forecast block.
- **One-off warnings**: use `Alert` or a `Toast`.

## Usage

The block derives from Avalonia's `TemplatedControl`. Set `Title`, `Period`, `Budget`, `Spent`, and `DaysElapsed` with
`DaysInPeriod` for the pace. Alerts are `SpendThreshold`s with a `Share` of the budget (0.8 for 80%) and `IsEnabled`.

```xml
<arc:SpendAlert Title="Dining out" Period="November" Budget="600" Spent="412.80" DaysElapsed="18" DaysInPeriod="30">
    <arc:SpendThreshold Share="0.5" />
    <arc:SpendThreshold Share="0.8" />
    <arc:SpendThreshold Share="1" />
</arc:SpendAlert>
```

### Crossing an alert

Only a rise of `Spent` raises an alert: when the new amount reaches a switched-on alert that the old amount was under, the
highest such alert is shown in the banner, announced and reported by `ThresholdCrossed`. `AddSpending` adds a purchase.
The banner stays until Dismiss (or `DismissAlert`), or until spending, the budget or the alert moves back under it.
Editing an alert never raises one by itself.

### Status

`Status` reads OnTrack, AtRisk (nothing reached yet, but the pace ends over budget), Warning (an alert is reached) or Over
(spending is above the budget), and the pill beside the title follows it.

### The editor

The budget field writes `Budget`. Each alert row has a switch, the percentage and a remove button; Add alert adds one ten
points above the highest, up to `MaxThresholds` (5). Edits write back to the `SpendThreshold` objects and raise
`ThresholdsEdited`. `IsEditable="False"` hides the editor for a summary card.

<Demo name="ReadOnly" />

## API reference

<!-- api: SpendAlert -->

<!-- api: SpendThreshold -->

## Accessibility

- The meter is named with the spent amount, the budget and the share.
- Each alert's switch is named "Alert at 80%", its field "Alert share of budget, percent" and its remove button "Remove
  the 80% alert". After a removal the focus moves to the next row's remove button, or to Add alert.
- An assertive live region reads the banner when an alert is crossed.

## Motion

- The spent fill, the pace segment and the scale spring to new values on the smooth spring.
- The banner rises 6 px and fades in over 240 ms; its colors cross-fade when it turns from warning to danger.
- **Reduced motion**: the banner fades without rising and the meter moves at once. **Off** applies every change at once.

## Responsive behavior

- Below 520 px the budget field moves under the alerts heading and the pace line moves under the amount left.
- The meter fills the block's width; labels over the ticks stay inside it.

## Related

- The Budget variance block for budgets across departments.
- The Usage forecast block for a budget line over a billing cycle.
- The Wallet card block for the card the spending is on.
