---
title: Wallet card
summary: A payment card as it sits in a wallet: a drawn card face with an invented network mark, the balance and the month's spending against its limit, recent transactions, a freeze that frosts the card and details that reveal and copy the number.
---

<Demo name="Preview" />

## When to use

`WalletCard` shows one payment card the way a banking or wallet app does. On the left is the card itself, drawn in code: a
finish, a chip, a contactless mark, an invented network mark (a disc inside a tilted ring), the masked number, the holder
and the expiry. Beside it are the money questions people open the card for: how much is available, how much of this
month's limit is spent, and what the last few purchases were. Two actions sit under the card. Freeze stops new purchases
and covers the card with frost; Show details reveals the full number and the security code for a while and offers to copy
the number.

- **A card screen in a wallet or banking app**, one card at a time.
- **Virtual cards** whose number people copy into a checkout.
- **Lost-card flows**: freeze first, decide later.

## When not to use

- **A list of many cards**: show a row of small faces and open this block for the chosen one.
- **Entering a new card**: use a form with `Input` fields (the Live checkout block has one).
- **Full statements**: the activity list shows the latest few rows; use `Table` for history with filters and paging.

## Usage

The block derives from Avalonia's `TemplatedControl`. Set the card (`HolderName`, `CardNumber`, `Expiry`,
`SecurityCode`, `Finish`), the money (`Balance`, `MonthlyLimit`, `SpentThisMonth`, `CurrencySymbol`) and fill
`Transactions` with `WalletTransaction`s, newest first. `WalletCardSamples.Apply` fills a block with the preview's invented
cardholder.

```xml
<arc:WalletCard HolderName="Ines Moreau" CardNumber="4929 1820 5531 4821" Expiry="08/29" SecurityCode="317"
                Balance="2481.56" MonthlyLimit="3000" SpentThisMonth="1240.18">
    <arc:WalletTransaction Merchant="Lantern Coffee" Amount="6.40" Category="Cafes" />
</arc:WalletCard>
```

### Freezing

The freeze button calls `SetFrozenAsync` with the state it asks for and waits for it: the button shows a spinner and the
card stays as it is until your service answers. Return true and the frost settles over the face, the frozen note appears
and `FrozenChanged` is raised; return false (or throw) and the card stays, with "The card could not be changed. Try
again." announced. Without a handler the change applies at once. Setting `IsFrozen` from code applies it directly.

```csharp
card.SetFrozenAsync = async (frozen, cancellation) =>
    await cards.SetFrozenAsync(cardId, frozen, cancellation);
```

### Details

Show details swaps the masked number for the full one, shows the CVC and a Copy number button, and hides them again after
`DetailsTimeout` (30 seconds; zero keeps them until hidden). Escape hides them too.

### Finishes and limits

`Finish` picks Midnight, Ocean, Coral, Forest or Accent (the theme's accent). The meter turns to the danger color once
spending passes the limit, and `MonthlyLimit="0"` hides it.

<Demo name="Finishes" />

## API reference

<!-- api: WalletCard -->

<!-- api: WalletTransaction -->

## Accessibility

- The face is named with the network, the holder, the last four digits and whether it is frozen.
- Freeze and Show details are buttons whose names follow their state ("Unfreeze", "Hide details").
- Each activity row is a button named with the merchant, the signed amount, the date and a pending or declined status;
  **Up**, **Down**, **Home** and **End** move between rows, **Enter** or **Space** raises `TransactionInvoked`.
- A polite live region announces freezing, unfreezing, a failed change and the details being shown or hidden.

## Motion

- The frost fades in over 480 ms and out the same way; the full number and CVC cross-fade with the masked number (160 ms
  out, 240 ms in).
- The limit meter springs to a new share on the smooth spring.
- **Reduced motion**: the frost and details change in a 120 ms fade and the meter moves at once. **Off** applies every
  change at once.

## Responsive behavior

- The block reads its own width. Below 680 px the card sits above the balance and activity instead of beside them.
- The face keeps the 1.586 card ratio and grows up to 360 px wide.
- Long merchant names end with an ellipsis; amounts never wrap.

## Related

- The Credit top-up block for buying usage credits with a saved card.
- The Spend alert block for budgets and alerts on top of card spending.
- `CopyButton`, `Button` and `Badge`, the controls the block uses.
