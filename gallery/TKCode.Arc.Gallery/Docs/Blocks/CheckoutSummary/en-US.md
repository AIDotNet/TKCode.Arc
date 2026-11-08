---
title: Checkout summary
summary: An order summary where an accepted promo code turns into a removable chip, discount and tax lines slide in, totals roll to the new amount and the pay button walks through processing to paid.
---

<Demo name="Preview" />

## When to use

`CheckoutSummary` is the last card before someone pays for one product. It shows what they are buying, every amount
that leads to the total, a field for a promo code and the button that pays. Everything moves when it changes: an
accepted code becomes a chip and its discount line slides into the breakdown, the tax is recomputed, the total rolls to
the new amount digit by digit and the pay button reads the new price.

- **Single-product checkout.** A plan, a pass, a license or a one-time purchase where the order is already decided and
  only the code and the payment are left.
- **Upgrade confirmations.** The step after a pricing page: the plan the person picked, its yearly discount, tax and one
  clear button.
- **Embedded payment steps.** The summary column of a checkout page whose address and card fields live elsewhere; the
  card's pay button starts the host's own payment call.

## When not to use

- **Collecting the address and card.** The summary does not ask for payment details; put those fields in a form of
  their own and keep this card beside it.
- **Several products or quantities.** Use `CartDrawer` when people add, remove and count items.
- **Bills for an existing subscription.** Use `BillingOverview` for invoices, payment methods and the next charge.

## Usage

The block derives from Avalonia's `TemplatedControl` and is driven by data: an `Item` (name, description, price and
period), automatic `Adjustments` written as children, an optional `Tax`, the `Currency` (ISO code) and the `CultureName`
used to format amounts. The promo code field appears once you give it a `CouponValidator`; Pay runs `PayAsync`.

```xml
<arc:CheckoutSummary x:Name="Summary" Width="440">
    <arc:CheckoutSummary.Item>
        <arc:CheckoutSummaryItem Name="Sketchbook Pro, annual"
                                 Description="Infinite canvases and backups for one maker"
                                 Price="120"
                                 Period="billed yearly" />
    </arc:CheckoutSummary.Item>
    <arc:CheckoutSummaryLine Id="annual" Label="Annual plan" Note="Three months free" Amount="-30" />
    <arc:CheckoutSummary.Tax>
        <arc:CheckoutSummaryTax Label="Sales tax" Rate="0.075" />
    </arc:CheckoutSummary.Tax>
</arc:CheckoutSummary>
```

```csharp
Summary.CouponValidator = async (code, token) =>
{
    var answer = await api.CheckPromoCodeAsync(code, token);
    return answer.Ok
        ? CheckoutSummaryCouponResult.ValidPercent(answer.Code, answer.Percent, answer.Label)
        : CheckoutSummaryCouponResult.Invalid(answer.Message);
};
Summary.PayAsync = token => api.ChargeAsync(Summary.Total, token);
```

`CheckoutSummarySamples` holds the content of the preview and a simulated backend (a 700 ms code check that accepts
HELLO15 and FRIEND5, a 1400 ms payment that can be told to fail). Replace both with your own calls.

### Anatomy

- **Title**: "Order summary" by default (`Title`), also the card's accessible name.
- **Item row**: the name on one line (cut with an ellipsis), a description that wraps, the price and the period on the
  right, and an optional 44 px `Media` cell before the name.
- **Promo code**: a field with Apply that becomes an "applied" chip with a remove button; refusals open an error line
  under it.
- **Breakdown**: Subtotal, each adjustment in order, the promo code line, then the tax. Negative amounts are discounts
  and show in green.
- **Total**: a rolling number in the display face.
- **Pay button** with a lock, then the secure note ("Encrypted payment") and an optional `Footnote`.

### The arithmetic

The subtotal is the item price. Adjustments are added to it (the adjusted subtotal never drops below zero). A percent
code applies to the adjusted subtotal, not to the list price; a fixed code takes its amount off, never more than what is
left. Tax is the rate times what remains, rounded to the cent before it is added. With the sample data: 120 − 30 = 90,
tax 6.75, total 96.75; with HELLO15 the code takes 13.50, tax 5.74, total 82.24.

All amounts on the card use the same number of decimals: none when every amount is whole ($50), otherwise two
($96.75). Discounts start with a true minus sign (−$30.00). The symbol and separators follow `Currency` and
`CultureName`; currencies without a symbol in the table show their code.

### Promo codes

Typed letters turn into capitals as you type (`UppercaseCoupon`). Apply, or Enter in the field, sends the trimmed text to
`CouponValidator`; a blank field does nothing. While the check runs, Apply shows a spinner and ignores presses.

- **Accepted**: the field fades out while the chip grows in, the code's line slides into the breakdown, tax and total
  update and the focus moves to the chip's remove button. `CouponApplied` is raised.
- **Refused**: the area shakes, the field's border turns rose and the message opens under it; the focus stays in the
  field and typing clears the error. `CouponRejected` carries the message. An exception from the validator counts as a
  refusal with the labels' generic message.
- **Removed**: the chip fades out, an empty field comes back with the focus, the line slides away and the total rolls
  back. `CouponRemoved` is raised.

`ApplyCouponAsync` and `RemoveCoupon` do the same from code; `AppliedCoupon` and `CouponDiscount` tell you what is in use.

### Paying

Pay raises `PayRequested` (set `Cancel` to stop), moves `PayState` to `Processing` and awaits `PayAsync`. Completing
moves to `Paid`, which is final: the button turns green, shows a check and stays disabled. Throwing moves to `Error`: an
error line opens under the button (`PayErrorMessage`, or the labels' generic text) and the button reads "Try again",
which starts a new attempt. While processing and once paid, the promo code is locked so the charge and the shown total
cannot drift apart. `PayStateChanged` reports every change with the exception, if any.

Without `PayAsync`, Pay only raises `PayRequested` and shows Processing; set `PayState` yourself when your payment form
or redirect returns, and call `ResetPayState` to start over. Use the switch in the preview to see the failure path.

<Demo name="Events" />

### States

`PayState` can be set directly, which is how the cards below show processing, paid and a failed payment. The first one
also has a media cell and no promo code field.

<Demo name="States" />

### Narrow containers

The card grows to 440 px and fills narrower containers. The description wraps under the name, the price column stays on
one line and the chip keeps the code and the remove button in view.

<Demo name="Narrow" />

### Other words, currencies and motion

Every visible and spoken text is in `Labels` (a `CheckoutSummaryLabels`); entries with an amount are functions that get
the formatted value. This example formats euros with German number rules, renames every label, leaves the tax rate out
(the tax line then reads "Added at the next step" and the total excludes it) and runs at reduced motion.

<Demo name="CustomLabels" />

### Content guidelines

- Keep the item name short enough for one line; put the detail in the description.
- Name adjustments by what they are ("Annual plan", "Referral credit") and use the note for the why ("Three months
  free").
- Write refusal messages that say what to do next ("Try HELLO15"), not just that the code failed.
- Keep the pay label to the verb and the amount; the button already says it is secure.

## API reference

<!-- api: CheckoutSummary -->

<!-- api: CheckoutSummaryItem -->

<!-- api: CheckoutSummaryLine -->

<!-- api: CheckoutSummaryTax -->

<!-- api: CheckoutSummaryCouponResult -->

<!-- api: CheckoutSummaryLabels -->

<!-- api: CheckoutSummaryCouponEventArgs -->

<!-- api: CheckoutSummaryPayRequestedEventArgs -->

<!-- api: CheckoutSummaryPayStateChangedEventArgs -->

<!-- api: CheckoutSummarySamples -->

## Accessibility

- The card is named by its `Title`. The promo field is named "Promo code"; after a refusal the message is its help text
  and the error line is announced assertively.
- While a code is checked, Apply is named "Checking code". The chip's remove button is named "Remove code HELLO15".
- After an accepted code the focus moves to the remove button, so the keyboard user is not dropped at the top of the
  window; after removing the code it returns to the field.
- The pay button is a polite live region named by its current label, so "Processing", "Paid" and "Try again" are read
  out. A failed payment's error line is announced assertively.
- The rolling total is named with the full amount ("Total $96.75") so a screen reader never reads digits mid-roll.
- Keyboard focus rings are 2 px, 2 px outside Apply, the remove button and Pay; the field shows focus with a stronger
  border instead. A pointer press draws no ring.

### Keyboard

| Keys | Action |
| --- | --- |
| Enter | In the promo field: applies the code (a blank field does nothing). |
| Enter / Space | On Apply, the remove button or Pay: presses it. Removing returns the focus to the field. |
| Tab / Shift+Tab | Field, Apply, Pay without a code; remove button, Pay with one. |

## Motion

- **Lines**: a line that comes or goes springs its height on a critically damped spring of 0.4 s while it fades over
  240 ms (ease-out), so the text is readable before the line has fully opened. The lines below and the card's bottom
  follow the height.
- **Code accepted**: the field fades out in 160 ms; the chip grows from 0.97 and fades in on the same 0.4 s spring.
- **Code removed**: the chip fades out in 160 ms; the field grows from 0.98 and fades in over 240 ms on the enter curve.
- **Code refused**: the area shakes sideways (0, −6, 6, −4, 3, 0 px over 360 ms); the error line opens on the spring
  and the border color changes over 160 ms.
- **Total**: every digit is a wheel; all wheels turn in the direction the amount moved, together, on the same spring, so
  a wheel with many steps spins faster and all land at once. Glyphs fade with their distance from the line.
- **Pay label**: any change (a new total, Processing, Paid) swaps upward: the old label rises 8 px and fades in 160 ms
  while the new one rises from 8 px below over 240 ms. Paid changes the color to green over 160 ms.
- **Presses**: Apply scales to 0.97 and Pay to 0.98 over 160 ms; Pay darkens slightly under a mouse pointer.
- **Spinners** turn once every 700 ms.
- Reduced motion: no shake, no scaling; lines, the error line and the chip appear at once, the total jumps, labels
  crossfade over 240 ms and spinners slow to 1.6 s per turn. Off changes everything at once.

## Responsive behavior

- The card is at most 440 px wide (`MaxWidth`) and fills narrower containers.
- Its padding follows the viewport width like the reference: 5% of the viewport between 20 and 28 px (28 px from a
  560 px window up, 20 px on phones).
- Nothing else changes with width: the description wraps, the total keeps its size, and the chip's "applied" word gives
  up room first.

## Related

- `CartDrawer` for several items with quantities and a checkout button.
- `BillingOverview` and `UsageBilling` for existing subscriptions.
- `AnimatedCounter` and `RollingDigits` for rolling numbers on their own.
