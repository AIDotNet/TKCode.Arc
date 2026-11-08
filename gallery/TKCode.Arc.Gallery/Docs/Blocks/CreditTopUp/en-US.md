---
title: Credit top-up
summary: A card for buying usage credits: the balance with a two-week usage chart and days left, preset packs with bonus badges, a custom amount slider with a live price, auto top-up with a threshold, the saved payment card, and a new balance that rolls in like an odometer.
---

<Demo name="Preview" />

## When to use

`CreditTopUp` sells prepaid credits (AI tokens, API calls, render minutes) in one card. It answers the three questions
people have when they open a billing page: how much is left, how long it lasts, and what a refill costs. The balance sits
next to two weeks of usage and an estimate of the days left; three packs and a slider pick the amount; auto top-up and the
saved card sit right above the checkout, so one click buys and the new balance rolls in like an odometer.

- **A credits section of a billing page**, where people also manage auto top-up and their card.
- **An "out of credits" dialog or drawer.** The burn rate and a one-click refill belong together when work has stopped.
- **Prepaid plans with volume bonuses.** Every price, badge and total comes from one `CreditTopUpPricing`, so the bonus
  tiers you sell are the ones the card shows.

## When not to use

- **Subscription changes** with seats, plans and billing periods: use a plan comparison or a checkout block.
- **Collecting a new card number.** The card shows saved cards only; take a new card with your payment provider's own
  form, then pass the saved card in.
- **Read-only usage reports.** Use `Usage billing` or a chart; this card is built around buying.

## Usage

The block derives from Avalonia's `TemplatedControl`. With no properties it shows a sample workspace at 1,260 credits and
simulates a 1.2 second charge, so you can try it before wiring anything. In an app, bind the balance (two-way), the daily
usage and the saved cards, and give it a `PurchaseHandler` that charges through your backend:

```xml
<arc:CreditTopUp Balance="{Binding Credits}"
                 Usage="{Binding DailyUsage}"
                 PaymentMethods="{Binding Cards}"
                 IsAutoTopUpEnabled="{Binding AutoTopUp}"
                 AutoTopUpThreshold="{Binding AutoTopUpThreshold}" />
```

```csharp
public interface IBilling
{
    Task BuyCreditsAsync(CreditTopUpOrder order, CancellationToken cancellation); // throws with a short message on failure
    void SaveAutoTopUp(bool enabled, long threshold, long credits);
    void SetDefaultCard(string cardId);
}

// order: Credits, Bonus, Total, Price, Currency, PaymentMethodId.
card.PurchaseHandler = (order, cancellation) => billing.BuyCreditsAsync(order, cancellation);
card.AutoTopUpChanged += (_, e) => billing.SaveAutoTopUp(e.Settings.IsEnabled, e.Settings.Threshold, e.Settings.Credits);
card.PaymentMethodChanged += (_, e) => billing.SetDefaultCard(e.PaymentMethod.Id);
```

Price orders on the server with the same arithmetic: `CreditTopUp.Quote(credits, pricing)` returns the credits, the
bonus, the total and the price, exactly as the card shows them.

### Anatomy

- **Balance**: a small title with the health badge ("Low balance" with an amber warning glyph, or "On track" with a green
  check), the balance in large tabular figures followed by "credits", and the days left at the current daily average.
- **Usage chart**: one bar per day, the last one (today) in the accent color. Pointing at a day turns its bar to the text
  color, dims the others and reads the day out under the chart ("Nov 14: 529", "Today: 362").
- **Top up**: the title with the rate ("$8 per 1,000"), three packs (amount, price and a green bonus badge when one
  applies) and the custom amount slider with its rolling readout.
- **Auto refill**: a switch, and the thresholds as a pill choice. The row stays in place while auto top-up is off,
  dimmed and out of reach.
- **Saved card**: a small network mark, the brand with the last four digits, the expiry and a Change button that steps
  through the saved cards.
- **Checkout**: the total credits (bonus included) with the bonus line under it, the price on the right, the buy button
  and a one-line status.

### Amount, packs and bonus

There is one amount, `SelectedCredits` (two-way). A pack is a shortcut for one value: pressing it moves the slider there,
and dragging the slider onto a pack's value selects that pack. The selection ring glides between packs, and fades in place
when the amount lands on a pack from elsewhere or leaves every pack. Amounts are kept inside `Range` on its step (500 to
30,000 by 500 by default); PageUp and PageDown move a tenth of the range (3,000).

The bonus comes from the highest tier at or below the amount (`CreditTopUpBonusTier`, for example +10% from 6,000). Below
every tier the line under the total says where the bonus starts. The rate per 1,000 is the plain unit price: bonuses add
credits, they do not change the price.

### Balance, days left and health

The days-left line divides the balance by the average of the **full** days in `Usage` and rounds down: today is still
running, so it is left out of the average, and "4.8 days" reads as 4. `Health` is running low whenever the balance is below
`AutoTopUpThreshold`, even while auto top-up is off; picking another threshold re-judges it at once. Leave `Usage` empty to
hide the chart and the days-left line.

### Buying

The order (amount, bonus, total, price and card) is fixed when the button is pressed. While the charge runs the button
shows only a spinner and ignores further presses but keeps the keyboard focus, and the status line names the card being
charged. The packs, the slider and the settings stay usable; a change applies to the next purchase. When the handler
finishes, the balance rolls up by the total, the days left and the health badge update, the button reads "Added" with a
check mark and the status line confirms the credits. After 2.4 seconds both return to rest. `Purchased` reports the order
and the new balance; `PurchaseAsync()` does what the button does.

Without a `PurchaseHandler` the card simulates the charge and the status line says nothing was charged. Removing the card
from the window cancels a charge in flight through the handler's token.

### Failure and retry

When the handler throws, the status line shows the exception's message in the danger color with an alert glyph, the
button reads "Retry", and `PurchaseFailed` reports the order and the message. The card keeps its size. Pressing Retry
charges the current selection again; changing the amount or the card first puts the button back to rest.

<Demo name="Declined" />

### A balance that changes on its own

The balance is yours to change: while usage drains it, set `Balance` and the figure rolls down to the new value; a
purchase still rolls it up. A change that arrives in the middle of a roll is planned again from where each digit is
drawn, so fast updates never leave a digit half shown.

<Demo name="Draining" />

### Narrow containers

The card is fluid up to 480 wide and reads its own width. At 448 or less the chart moves under the balance and stretches
across, with its label on the right; at 416 or less every side padding tightens from 24 to 16. The three packs always
stay in one row.

<Demo name="Narrow" />

### Currency, culture and wording

Prices use the ISO currency of `Pricing` laid out the way `CultureName` writes money ("48,00 €" in de-DE), and every
count uses the culture's separators. Every visible text and accessible name is in `CreditTopUpLabels`; sentences take
the formatted numbers as `{0}`, `{1}`, and where a number rolls (the total, the bonus line) the words around `{0}` stay
still.

<Demo name="Euro" />

## API reference

<!-- api: CreditTopUp -->

<!-- api: CreditTopUpPricing -->

<!-- api: CreditTopUpRange -->

<!-- api: CreditTopUpPaymentMethod -->

<!-- api: CreditTopUpLabels -->

<!-- api: CreditTopUpPhase -->

<!-- api: CreditTopUpHealth -->

<!-- api: CreditTopUpCardBrand -->

## Accessibility

- **Tab** moves through the selected pack (the first when none is selected), the slider, the auto refill switch, the
  selected threshold (only while auto refill is on), Change and the buy button. The chart is not a tab stop.
- **Arrow keys** move and select within the packs and within the thresholds, wrapping at the ends; **Home** and **End**
  jump to the first and last.
- On the slider, **Left** and **Right** step by 500, **PageUp** and **PageDown** by 3,000, **Home** and **End** go to the
  ends.
- **Space** flips the switch; **Enter** or **Space** presses Change and the buy button.
- The card is a region named "Buy credits" (`CardName`).
- Packs and thresholds read as radio buttons: "6,000 credits, $48, 10% bonus". The slider is named by its title and
  reads its value as "6,000 credits". The switch is named by the auto refill title.
- The chart is one image with a spoken summary ("Daily usage over the past 14 days, averaging 440 credits a day."); the
  readout under it is for pointer users and is not read.
- Rolling figures are read as their final value, never digit by digit.
- The status line is a polite live region: charging, the result, errors and settings notices are announced.
- Health is an icon and a word, never color alone.
- Keyboard focus shows a 2px ring outside the focused element; pointer focus shows none.

## Motion

- **Packs, thresholds and slider thumb** glide on a quick, bounce-free spring (about 0.25 s). The pack ring fades in place
  (170 ms) when a selection appears or goes away.
- **Checkout figures** roll each digit one way over 240 ms on Arc's standard curve (.22, 1, .36, 1): up when the value
  grows, down when it shrinks.
- **The balance** rolls like an odometer over 900 ms on the same curve: the highest digit that changes takes the short
  way and every digit below it one extra lap, so it reads like a counter. New leading digits do not roll: they open their
  width on the quick spring and fade in over 100 ms.
- **Text changes** (health, days left, chart readout, card, status, button label) crossfade: the old text fades out in
  100 ms while the new one fades in over 160 ms and rises 2 px.
- **Pointing at a day** dims the other bars to 55% in 160 ms. Pressing a pack scales it to 0.98 in 120 ms.
- Reduced motion places the figures, the ring, the pill and the thumb at once and keeps short fades. Off applies every
  change at once.

## Responsive behavior

- The card is at most 480 wide and follows its container below that; breakpoints use the card's own width, never the
  window's.
- At 448 or less (`:narrow`) the chart moves under the balance, stretches and its bars drop to 32 high.
- At 416 or less (`:compact`) the side paddings tighten from 24 to 16.
- The status line shortens with an ellipsis rather than wrapping, and the card keeps the same height through every
  purchase state.

## Related

- `Usage billing` and `Billing overview`, the other billing blocks.
- `Slider`, `Switch` and `Button`, the controls the card is built from.
- `Plan comparison` and `Pricing calculator` in the same group.
