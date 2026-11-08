---
title: Live checkout
summary: A subscription checkout whose summary recalculates with every change to plan, billing period, add-ons and details, then moves to the centre and unfolds into a receipt.
---

<Demo name="Preview" />

## When to use

`LiveCheckout` is a one-page checkout for a paid sign-up or an upgrade of a subscription. The form on the left asks for
everything in one pass: express wallets, the plan and the billing period, the number of seats, add-ons, billing details
and a card. The order summary on the right answers "what will I pay?" while the customer is still deciding: every change
recalculates the rows, the coupon, the tax for the billing country and the total, and each amount rolls digit by digit to
its new value. When the payment goes through, the form steps aside and the summary glides to the centre and becomes the
receipt.

- **Self-serve sign-up for a seat-based product.** A team picks a plan, sets how many people it is for and pays without
  talking to sales.
- **Upgrades from a free tier.** Start the checkout with the plan the customer clicked and the seats they already use;
  they only add a card.
- **Plans with optional add-ons and regional tax.** Add-ons appear and leave as rows of the summary, and the tax row
  follows the country (VAT, GST, sales tax or none).

## When not to use

- **Physical goods with a cart and shipping.** Use a cart (Cart drawer) and a checkout made for addresses, delivery
  options and shipping costs.
- **Only the total, no payment.** When the payment happens elsewhere and you only need a summary with a promo code, use
  Checkout summary.
- **Choosing between plans.** Before the customer has decided, compare the plans side by side with Plan comparison or
  Pricing calculator, then open the checkout with the chosen plan.
- **Card data you would have to store yourself.** The card fields here are plain text boxes, fine for a preview or a
  sandbox. In production put your payment provider's hosted card fields in their place, or let the express wallets carry
  the card.

## Usage

The block derives from Avalonia's `TemplatedControl`. Without any setting it shows the sample catalogue (three plans,
three add-ons, six countries, three fictional wallets) and simulates both the coupon check and the payment, so you can
drop it on a page and try it. For a real checkout, give it your catalogue and two handlers.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:LiveCheckout x:Name="Checkout"
                      Currency="EUR"
                      SelectedPlanId="studio"
                      Seats="3"
                      Billing="Yearly"
                      CountryCode="FR" />
</UserControl>
```

```csharp
Checkout.Plans =
[
    new LiveCheckoutPlan { Id = "sketch", Name = "Sketch", MonthlySeatPrice = 9m, Blurb = "Boards and comments" },
    new LiveCheckoutPlan { Id = "studio", Name = "Studio", MonthlySeatPrice = 19m, Blurb = "Shared libraries" },
];
Checkout.Addons = [new LiveCheckoutAddon { Id = "review", Name = "Client review", MonthlyPrice = 29m }];

// Your server checks the code and charges the order.
Checkout.ValidateCouponAsync = (code, ct) => api.CheckCouponAsync(code, ct);
Checkout.PayAsync = async (order, ct) =>
{
    var charge = await api.ChargeAsync(order, ct);          // throw to show a message above Pay
    return new LiveCheckoutReceipt { OrderNumber = charge.Number };
};
Checkout.Paid += (_, e) => analytics.Track("checkout_paid", e.Order.Totals.Total);
```

<Demo name="Handlers" />

### Anatomy

- **Express wallets**: up to three buttons in a row (44 px, in the wallet's own colors, not the theme's), then a line
  "Or pay with card". An empty `Wallets` list hides the row and the line.
- **Plan**: the heading with the billing switch (Monthly / Yearly with the yearly discount in green), then one card per
  plan with its name, the price per seat and month (rolling when the period changes) and a short line. The selected card
  carries a ring that glides from card to card.
- **Seats**: a card with a − / + stepper around a number box.
- **Add-ons**: a list of check box rows with a name, a line and the monthly price.
- **Billing details**: Email, Name on card, Country (a `Select`), Address, City and Postal code. Valid fields show a green
  check at the end; invalid fields show a message under them.
- **Card**: the heading with "Use test card" (shown while `TestCard` is set), then Card number, Expiry and CVC. When the
  card number is recognised the network's name fades in at the end of its label.
- **Pay**: a 50 px button reading "Pay" and the total, a payment error above it when one happened, and a security line
  under it (`LiveCheckoutLabels.SecureNote`; empty hides it).
- **Order summary**: a card with one row for the plan ("8 seats × $22.40 × 12 months") and one per ticked add-on, the
  coupon slot (Add coupon, the code field, or the applied coupon), Subtotal, an optional discount row, the tax row and the
  total.
- **Receipt**: the same card after payment, with a check badge, a heading, a sentence about the plan, the rows and totals,
  and under a dashed rule the order number, the payment method, the date and the receipt email, then "Receipt PDF" and
  "Start over".

### How the numbers are calculated

All amounts are `decimal` in the major unit of `Currency` and are formatted with the culture of `Labels.Culture`.

| Value | Monthly | Yearly (`YearlyDiscount` d, 0.2 by default) |
| --- | --- | --- |
| Seat price on a plan card | `MonthlySeatPrice` | `MonthlySeatPrice × (1 − d)`, rounded to cents |
| Plan row | `seats × MonthlySeatPrice` | `seats × seat price × 12` |
| Add-on row | `MonthlyPrice` | `MonthlyPrice × 12 × (1 − d)` |

Then: **Subtotal** is the sum of the rows; **Discount** is the subtotal times the coupon's percent, rounded to cents;
**Tax** is `(subtotal − discount) × TaxRate` of the country, rounded to cents (a rate of 0 reads "Not charged"); the
**total** is `subtotal − discount + tax`. With the samples, Crew for 8 seats billed yearly with Priority care in the
Netherlands is 2,150.40 + 432.00 = 2,582.40, plus 21% VAT of 542.30, so 3,124.70 is due today.

The tax row is named after the country and its rate, "VAT 21%" or "GST 5%", with trailing zeros dropped ("8.1%"). Seats do
not change add-on prices. `Totals` and `Lines` always hold what is on screen, and `TotalsChanged` reports each change.

Treat these numbers as a preview. Recalculate the order on your server before you charge it, from the ids in
`LiveCheckoutOrder`, not from its amounts.

### Checks before a payment

A field is checked once it has been edited and left, and from then on with every keystroke; a field you leave without
typing is not marked. Pay (or Enter in any field) checks everything at once, shows every message and moves the focus to
the first field that needs attention, in form order. Nothing is charged until all fields pass.

| Field | Rule |
| --- | --- |
| Email | Required; looks like `name@company.com`. |
| Name on card | At least two characters. |
| Address | At least three characters. |
| City | Required. |
| Postal code | Required; must match the country's `PostalPattern` in full, otherwise its `PostalHint` is shown. Changing the country checks a touched postal code again at once. |
| Card number | Required; grouped as you type (4-4-4-4, or 4-6-5 for 15-digit cards); long enough for its network; passes the Luhn checksum. |
| Expiry | MM/YY. A first digit from 2 to 9 becomes "0N/"; the slash is added for you; the month must be 01–12 and not in the past (the current month is fine). |
| CVC | Three digits, or four for 15-digit cards. |

The card networks are fictional and recognised by their first digits: Novacard (4), Meridian (51–55, 2221–2720) and Atlas
(34, 37; fifteen digits and a four-digit code). Rename them with `LiveCheckoutLabels.BrandName`.

### Coupons

"Add coupon" turns into a code field and an Apply button (the field takes the focus and turns what you type into capitals).
Enter or Apply calls `ValidateCouponAsync` while Apply shows a spinner; Escape closes the field. A refused code shows the
result's `Message` (or `LiveCheckoutLabels.CouponNotFound`) under the field. An accepted code becomes a chip with the code
and "15% off", a "Discount SPRING15" row slides into the totals and the focus moves to the chip's remove button. Removing
the chip brings "Add coupon" back and rolls the totals back. Without a handler the samples accept SPRING15 (15%) and CREW10
(10%) after 480 ms. A handler that throws counts as a refusal with the exception's message.

### Paying

`PayAsync` receives a `LiveCheckoutOrder` with the plan, seats, period, add-ons, coupon, billing details, payment method,
the rows and the totals. While it runs `IsProcessing` is true: the pay button reads "Charging card…" with a spinner, and
the form is disabled. Return a `LiveCheckoutReceipt` (or null) to succeed; anything you leave null is filled in (six
random digits as the order number, the clock's date, "Novacard ending 1111", the email field). Throw to fail: the
exception's message appears above the pay button and stays until the next edit, and `PaymentFailed` is raised.

An express wallet skips the field checks and calls `PayAsync` with `PaymentMethod` set to `Wallet` and the wallet's id;
its button shows the spinner instead of the pay button. Without a handler the block simulates a payment: 1.5 s for a
card, 1.1 s for a wallet, and the card `4000 0000 0000 9995` (`LiveCheckoutSamples.DeclinedCardNumber`) is declined.

After a success `IsPaid` is true, `Receipt` holds what the receipt shows and `Paid` is raised. "Receipt PDF" raises
`ReceiptRequested` (produce or download the document there) and its label turns to "Downloaded". "Start over", or
`Reset()`, puts every value back as the block was first shown and returns to the form. `SubmitAsync()` does what the pay
button does and returns whether the payment went through; `FillTestCard()` does what "Use test card" does.

### Other words, currencies and countries

Every visible text and every accessible name is a property of `LiveCheckoutLabels` with an English default; sentences
with numbers are functions (`PlanDetail`, `ReceiptSentence`, `CouponNotFound`). Its `Culture` formats amounts and the
receipt date. Set `Currency` to any ISO 4217 code; give each country its tax name, rate and postal rule in `Countries`.

<Demo name="Labels" />

### Content guidelines

- Keep plan names to one word and the line under them to one short phrase; three plans fit the row at every width.
- Write add-on lines as the benefit ("Answers within four hours"), not as a feature list.
- Write postal hints as an example of the right shape ("Use the A1A 1A1 format"), not as a rule.
- Payment errors should say what to do next ("Try a different card"), not the code your provider returned.

### Best practices

- Open the checkout with the plan and seats the customer already chose elsewhere; the form is long enough without
  repeating a decision.
- Keep `Currency` and the countries' tax rules in step with what your server will charge, so the preview never disagrees
  with the invoice.
- Use `TestCard` and the express wallets in sandboxes only when they help; set `TestCard` to null in production.
- Put the block in a scroll view on small screens. The summary stays at the top of the view while the form scrolls in two
  columns, and the receipt is brought into view when it appears.

### Common questions

**Why does the receipt not show the coupon slot?** A paid order cannot take a coupon any more; the slot leaves the summary
in the frame the receipt appears, and the applied discount stays as a row.

**Can I keep my own form fields?** The block is a complete flow. For a different set of fields build your own form and use
Checkout summary next to it.

**Does the block send anything anywhere?** No. Everything outside the block goes through `ValidateCouponAsync` and
`PayAsync`; without them it only simulates.

## API reference

<!-- api: LiveCheckout -->

<!-- api: LiveCheckoutPlan -->

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | Stable id, matched by `SelectedPlanId`. |
| `Name` | `string` | Plan name on the card and in the summary. |
| `MonthlySeatPrice` | `decimal` | Price of one seat for one month. |
| `Blurb` | `string?` | One short line under the price; null hides it. |

<!-- api: LiveCheckoutAddon -->

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | Stable id, listed in `SelectedAddonIds` while chosen. |
| `Name` | `string` | Add-on name. |
| `Description` | `string?` | One short line under the name. |
| `MonthlyPrice` | `decimal` | Flat price for one month. |

<!-- api: LiveCheckoutCountry -->

| Property | Type | Description |
|---|---|---|
| `Code` | `string` | ISO 3166 code, matched by `CountryCode`. |
| `Name` | `string` | Name in the country list. |
| `TaxRate` | `decimal` | 0 to 1 (0.21 for 21%); 0 reads "Not charged". |
| `TaxLabel` | `string?` | Tax name ("VAT", "GST"); empty uses `TaxFallback`. |
| `PostalPattern` | `string?` | Pattern a postal code must match in full; null accepts any. |
| `PostalHint` | `string?` | Message for a code that does not match. |

<!-- api: LiveCheckoutWallet -->

| Property | Type | Description |
|---|---|---|
| `Id` / `Name` | `string` | Id reported in `WalletId`; name used as accessible name and on the receipt. |
| `MarkText` / `Glyph` | `string? / Geometry?` | Word mark and a small mark in a 24-unit box. |
| `IsGlyphFilled` / `IsMarkItalic` | `bool` | Fill the glyph instead of stroking it; set the word mark in italics. |
| `Background` / `Foreground` / `BorderColor` | `Color` | The button's own colors in light themes. |
| `DarkBackground` / `DarkForeground` | `Color?` | Colors in dark themes; null keeps the light ones. |

<!-- api: LiveCheckoutTestCard -->

<!-- api: LiveCheckoutCouponResult -->

| Property | Type | Description |
|---|---|---|
| `IsValid` | `bool` | True when the code is accepted. |
| `Code` | `string` | The code as shown. |
| `Percent` | `decimal` | Discount in percent (0–100). |
| `Message` | `string?` | Why it was refused; null uses `CouponNotFound`. |

<!-- api: LiveCheckoutOrder -->

| Property | Type | Description |
|---|---|---|
| `PlanId`, `Seats`, `Billing`, `AddonIds` |  | What was chosen. |
| `CouponCode`, `CouponPercent` | `string?, decimal` | The applied coupon. |
| `CountryCode`, `Email`, `CardholderName`, `Address`, `City`, `PostalCode` | `string` | The billing details. |
| `PaymentMethod`, `WalletId` |  | Card, or the wallet that was pressed. |
| `CardNumber`, `Expiry`, `Cvc`, `CardBrand` |  | The card (card payments only). |
| `Lines`, `Totals`, `Currency` |  | The rows and amounts that were shown. |

<!-- api: LiveCheckoutReceipt -->

| Property | Type | Description |
|---|---|---|
| `OrderNumber` | `string?` | Null shows six random digits. |
| `PaidAt` | `DateTimeOffset?` | Null uses `Clock`. |
| `PaymentText` | `string?` | Null builds "Novacard ending 1111" or the wallet name. |
| `Email` | `string?` | Null uses the email field. |

<!-- api: LiveCheckoutLine -->

<!-- api: LiveCheckoutTotalsChangedEventArgs -->

<!-- api: LiveCheckoutCouponEventArgs -->

<!-- api: LiveCheckoutPaidEventArgs -->

<!-- api: LiveCheckoutPaymentFailedEventArgs -->

<!-- api: LiveCheckoutLabels -->

## Accessibility

- Plans and the billing period are radio groups. Each add-on is a real check box. The seat box, every field and every
  button have names from `LiveCheckoutLabels`; a field with an error is described by its message.
- Every amount is read as its full text ("$3,124.70"), never digit by digit; the rolling strips are hidden from screen
  readers.
- The processing state, an applied or removed coupon and "Downloaded" are announced politely; a payment error is announced
  at once.
- When a check fails the focus moves to the first field that needs attention. After a payment the focus moves to the
  receipt heading, so a screen reader starts reading the receipt. After a coupon is applied it moves to the chip's remove
  button, after Escape back to "Add coupon", and after "Start over" to the first wallet (or the selected plan): it is
  never dropped.
- Focus rings show for the keyboard only: 2 px, 2 px outside buttons and plan cards. Text fields mark focus with a darker
  edge instead.

### Keyboard

| Keys | Action |
| --- | --- |
| Tab / Shift+Tab | Wallets, Monthly, Yearly, the selected plan, the seat stepper, the add-ons, the billing fields, "Use test card", the card fields, Pay, then the coupon slot. |
| Arrow keys | On a plan: move to and select the next or previous plan, wrapping at the ends. On the billing switch: switch period. |
| Up / Down | In the seat box: one seat more or less (with Shift, ten). Our addition. |
| Enter | In a form field: pay (after the checks). In the code field: apply the coupon. |
| Escape | In the code field: close it and return to "Add coupon". |
| Space | Toggle the focused add-on; press the focused button. |

## Motion

- **Amounts** roll per digit: each digit runs straight from the old number to the new one (no wrapping round) on a
  critically damped spring of about a quarter second; all digits start together. A new leading digit appears at once and
  the amount's width changes with it.
- **Plan ring and billing pill** glide to the new choice on a smooth, critically damped spring (about 0.4 s).
- **Summary rows**, the discount row, field messages and the payment error open and close their height while they fade, on
  the same smooth spring.
- **Valid checks** pop in from 0.6 scale; the add-on tick draws itself; the pay label and the "Downloaded" label swap with
  a short rise out of a blur.
- **The receipt**: the summary glides from where it was to the centre of the block (a translation only; its new size
  applies at once), its background and shadow change over 240 ms, the receipt heading fades in over 240 ms while it rises
  8 px from 75 ms, the details below the totals rise 12 px as one block from 176 ms, and the badge's check draws over
  470 ms from 320 ms. At the same moment the form fades out and moves 24 px left over 240 ms on the exit curve. In one
  column the folded summary unfolds instead (480 ms height, 240 ms fade). If the page was scrolled, the receipt is brought
  into view first, so the glide is one continuous move.
- **Start over** brings the form back at once and glides the summary back to its column on the same spring.
- Reduced motion and Off make every change at once: amounts, rows, the receipt and the form. Payments take the same time
  and the focus moves the same way.

<Demo name="ReducedMotion" />

## Responsive behavior

- The block reads its own width, not the window's. From 760 px (`:narrow` below) the form and a 380 px summary sit side by
  side with a 32 px gap, and the summary stays at the top of a scroll view while the form scrolls.
- Below 760 px the summary moves above the form and folds into a bar reading "Show order summary" with the total; the bar
  opens it. The receipt takes the full width.
- Below 520 px (`:stacked`) wallets, plans and billing fields stack in one column; Expiry and CVC stay side by side, and
  each plan card puts its price on the right.
- Hover only tints; nothing depends on it on a touch screen.

<Demo name="Narrow" />

## Related

- Checkout summary, for a summary with a promo code next to your own form.
- Plan comparison and Pricing calculator, to choose a plan before the checkout.
- Cart drawer, for physical goods.
- `Input`, `Select` and `Checkbox`, the controls the fields are built from.
