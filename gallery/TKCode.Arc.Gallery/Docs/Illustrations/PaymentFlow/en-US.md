---
title: Payment flow
summary: A card payment from checkout through tokenization, authorization and the issuer's decision to settlement.
---

<Demo name="CardPayment" />

## When to use

- Explaining what happens between a customer pressing "Pay" and the merchant receiving funds.
- Checkout and help pages that need to show why a payment can be approved or declined.
- Docs for a payment integration, with the amount, currency and merchant shown as configured.

## When not to use

- Entering a payment amount: use `NumberField`, and a form for the card details themselves.
- Plotting totals over time: use `BarChart` or `LineChart` from the Charts category.

## Usage

The story plays one payment after another. The caption under the diagram describes the current step, and the
merchant balance grows with every approved payment.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:PaymentFlow />
</UserControl>
```

### Amount, currency and merchant

`Amount` is a `decimal` (default 42) and `Currency` is an ISO code (default USD). `MerchantName` names the
merchant at checkout and `CardLast4` shows the last four digits of the card. Common currency codes are shown
with their symbol, and others with the code.

<Demo name="AmountAndMerchant" />

### Declines

`DeclineEvery` declines every N-th payment (default 4). Set it to 0 to approve every payment.

```xml
<arc:PaymentFlow DeclineEvery="0" />
```

### Labels

`Labels` takes a `PaymentFlowLabels` object with every visible word of the diagram, including the badges
(`Approved`, `Declined`, `Paid`) and the step captions.

```csharp
var payment = new PaymentFlow
{
    Amount = 19.00m,
    Labels = new PaymentFlowLabels { Merchant = "Shop", Issuer = "Issuer", Approved = "OK", Declined = "Refused" }
};
```

### Reacting to decisions

`PaymentCompleted` is raised when the issuer decides a payment, with `Approved` and `Amount` in the event
arguments. `PaymentCount` and `Balance` give the decided payments and the merchant balance.

```csharp
payment.PaymentCompleted += (_, e) =>
    log.Text = $"{(e.Approved ? "Approved" : "Declined")} {e.Amount:0.00} · balance {payment.Balance:0.00}";
```

### Playback

`IsPlaying` plays and pauses the story, `Speed` scales it (0.25 to 4), and `Restart()` starts over from the
first step. `Step`, `StepCount` and `StepChanged` follow the seven steps of one payment.

```xml
<arc:PaymentFlow Speed="0.5" IsPlaying="False" />
```

### Reduced motion

Under reduced motion the illustration is a still frame of the approved path.

<Demo name="ReducedMotion" />

## API reference

<!-- api: PaymentFlow -->

<!-- api: PaymentFlowLabels -->

## Accessibility

- `Label` is the accessible name, and the current caption is exposed as the help text.
- The illustration is focusable and has a 2px focus ring.
- Space or Enter plays and pauses the story, and R starts it over.
- Caption changes are not announced, so the story does not repeat itself to screen reader users.

## Motion

- The amount rises above the customer, card details travel to the gateway, and the request crosses the card network
  to the issuing bank.
- The issuer's decision shows as a green Approved or red Declined badge, and the response returns on the other lane.
- An approved payment settles along a dashed line to the merchant account, and the balance readout increases.
- Reduced motion and Off show the approved path as a still frame with the Approved and Paid badges. The play button
  is hidden.

## Related

- `DataFlow` for the pipeline that stores events after a payment.
- `MultiRegionFailover` for keeping the payment service available when a region fails.
- `Stepper` for a fixed sequence of steps that the user moves through.
