---
title: Cart drawer
summary: A product grid whose items fly into the cart button, a count that rolls, and a side drawer with quantity steppers, a free-shipping progress bar, a rolling subtotal and checkout.
---

<Demo name="Preview" />

## When to use

`CartDrawer` is a small store on one block: a grid of products, a cart button with a count, and a cart that slides in
from the side of the block. Adding a product sends a copy of its picture along an arc into the cart button; when it
lands, the count rolls up by one and the button bumps. The drawer lets people check what they picked before they pay:
quantities, a bar toward free shipping, a promo code, the totals and a checkout button that turns into a confirmation.

- **Product listings** where people add several items and review them together before checkout.
- **Stores with a free-shipping threshold**: the bar and the sentence above it say how much is missing, and the shipping
  line reads "Free" as soon as the subtotal reaches the threshold.
- **Promo codes before checkout**: an accepted code turns into a green chip and a discount line slides into the totals.
- **Showing where an item went**: the flight connects the press on a card with the count in the corner, which a toast
  cannot do.

## When not to use

- **A full checkout** with address, delivery and payment steps: use a multi-step form (`MultiStepForm`) after the cart.
- **Only the order summary** and its totals: use the Checkout summary block.
- **A side panel that is not a cart**: use `Drawer`, which covers the window and holds any content.
- **Hundreds of products**: the grid is not virtualized. Show a page of products per block, or put the products in your own
  list and call `Add` from it.

## Usage

The block derives from Avalonia's `TemplatedControl`. Products are `CartDrawerProduct`s, `AvaloniaObject`s with a name,
an optional detail line, a price, a picture and `MaxQuantity` (9 by default). Fill `Products` in XAML or bind
`ProductsSource`; a product with `InitialQuantity` starts in the cart. Amounts are `decimal`s in `Currency` (ISO 4217),
formatted with `Labels.Culture` and `AmountDecimals` places (0 by default: whole units).

```xml
<arc:CartDrawer Title="Fresh from the workshop"
                Height="760"
                FreeShippingThreshold="240"
                ShippingFee="9">
    <arc:CartDrawerProduct Name="Fluted water jug" Detail="Recycled glass, 0.9 L" Price="58" InitialQuantity="1" />
    <arc:CartDrawerProduct Name="Speckled mugs" Detail="Pair, reactive glaze" Price="42" />
</arc:CartDrawer>
```

`CartDrawerSamples` holds the preview's invented store: `CreateProducts()` (six products with drawn pictures),
`CreateLabels()` (the default text plus the promo hint), the code check and the order desk.

### Anatomy

- **Header**: `Title` on the left; the cart button on the right with a bag, "Cart" and the count badge. The badge counts
  an item when its flight lands, not when the button is pressed.
- **Grid**: three columns of cards (two at 640 px and below). Each card has a 4:5 picture, the name and price, the detail
  line and a full-width "Add to cart" button that reads "Added" for 1.4 seconds after a press.
- **Drawer**: opens inside the block over a scrim, 400 px wide and 8 px from the block's edges. It has a header with the
  rolling item count and a close button; a free-shipping card; the rows; and a footer with the promo code, the totals and
  the checkout button.
- **Row**: thumbnail, name, detail, a quantity stepper (the minus is a bin at quantity 1), the line price and a ×.
- **Receipt**: after checkout, a disc with a check, the thank-you line, a sentence and the Items, Paid and Arrives rows,
  with "Continue shopping" in the footer.

### The cart

`Lines` lists the rows in the order they were added, and `ItemCount`, `Subtotal`, `Discount`, `Shipping` and `Total`
follow every change. Shipping is free once the subtotal **before** the discount reaches `FreeShippingThreshold`, so a code
never takes free shipping away. The discount is the subtotal times the code's percentage, rounded to `AmountDecimals` places
with halves away from zero ($64 at 10% takes off $6, $186 takes off $19). Set the threshold to 0 to hide the
free-shipping card and ship everything free.

`Add`, `SetQuantity`, `Remove` and `Clear` change the cart from code, and `Open` and `Close` (or the two-way `IsOpen`)
move the drawer. `ItemAdded`, `QuantityChanged` and `ItemRemoved` report each change with the product and the old and new
quantity. A row cannot go above its product's `MaxQuantity`: the stepper's plus and the card's button rest at the cap, and
`Add` returns false without a flight or an announcement.

### Removing rows

The × and the stepper's bin remove a row at once. With a pointer, a row can also be swiped left: a red layer with "Remove"
shows behind it as it moves, turns a step deeper past 110 px, and a release past that point (or a quick flick) slides the
row out. A shorter swipe springs back. Either way the row fades and collapses so the rows below move up. Swiping only
starts on a mostly horizontal drag, so a vertical drag still scrolls the list. Set `IsSwipeToRemoveEnabled` to false to
keep only the buttons.

### Promo codes

Type a code and press Apply or Enter. `ValidatePromoAsync` checks it: return a `CartDrawerPromo` to apply it, or null to
refuse it. A refused code shakes the field, turns its border red and replaces the hint with "SPRING doesn't match an
offer"; the next edit clears the error. An accepted code becomes a chip with the code, its percentage and a Remove button,
and a Discount line opens in the totals. `ApplyPromoAsync` and `RemovePromo` do the same from code; `PromoApplied`,
`PromoRejected` and `PromoRemoved` report them. `IsPromoVisible="False"` hides the field.

```csharp
store.ValidatePromoAsync = async (code, cancellation) =>
{
    var offer = await offers.FindAsync(code, cancellation);
    return offer is null ? null : new CartDrawerPromo(offer.Code, offer.PercentOff);
};
```

### Checkout

Checkout calls `PlaceOrderAsync` with a `CartDrawerOrder` (the lines, the promo and every total) and shows the receipt it
returns. While it runs, `State` is `Placing`: the button shows a spinner and "Placing order" and keeps the focus, the rows
and the promo code are locked, and the drawer cannot be closed. Then the button turns into the confirmation disc, the
check draws itself and the receipt appears. If the task throws, the drawer goes back to the cart unchanged. Closing the
receipt, with Continue shopping or any other way, starts a new, empty cart. `OrderPlaced` carries the order and the
receipt.

```csharp
store.PlaceOrderAsync = async (order, cancellation) =>
{
    var confirmation = await checkout.PayAsync(order.Lines, order.Total, cancellation);
    return new CartDrawerReceipt($"Thanks, {confirmation.FirstName}", confirmation.Summary,
        order.ItemCount, order.Total, confirmation.DeliveryWindow);
};
```

### Your own store

Every text is in `CartDrawerLabels`; sentences with numbers are functions that receive the formatted amount or count.
This tea shop uses pounds with pence, its own code check and order desk, and writes the block's events to a log.

<Demo name="OwnStore" />

### Narrow containers

The block reads its own width, not the window's. At 640 px or less the grid has two columns and tighter padding. At
380 px or less the cart button keeps only its bag and count, each card puts its price under the name, and the drawer fills
the block 4 px from its edges with smaller thumbnails.

<Demo name="Narrow" />

### Reduced motion

With reduced motion nothing flies and the badge counts at once, the drawer fades in place instead of sliding, rows cannot
be swiped (the × still removes them) and numbers change without rolling. Text, focus and announcements stay the same.

<Demo name="ReducedMotion" />

## API reference

<!-- api: CartDrawer -->

<!-- api: CartDrawerProduct -->

<!-- api: CartDrawerLine -->

<!-- api: CartDrawerPromo -->

<!-- api: CartDrawerOrder -->

<!-- api: CartDrawerReceipt -->

<!-- api: CartDrawerState -->

<!-- api: CartDrawerItemEventArgs -->

<!-- api: CartDrawerPromoEventArgs -->

<!-- api: CartDrawerOrderPlacedEventArgs -->

## Accessibility

- The cart button is named with the count ("Cart, 2 items"); the name changes when a flight lands. **Enter** or **Space**
  opens the drawer.
- When the drawer opens, the focus moves to its close button. **Tab** and **Shift+Tab** cycle inside the drawer and never
  reach the shop behind it, and the shop does not take pointer input.
- **Escape**, the close button and a press on the scrim close the drawer, and the focus goes back to the cart button.
  While an order is being placed they do nothing.
- Each card's button is named "Add Fluted water jug to cart". The stepper is a group named "Fluted water jug quantity";
  its buttons are named "Decrease …" or "Remove …" (at quantity 1) and "Increase …", and the × is "Remove …". Swiping has
  these buttons as its equivalent.
- After a row is removed, the focus moves to the same button on the next row (or the previous one, or "Browse products"
  when the cart is empty). After a code is applied it moves to the chip's Remove, and after the code is removed back to
  the field. On the receipt it moves to "Continue shopping".
- Rolling numbers have their value as their accessible name. The shipping bar is named "Progress to free shipping" with
  the amount reached as its help text.
- A polite live region announces an added product, a removed row, an applied or removed code and the placed order
  ("Order placed. 2 items, $134."). Quantity changes and refused codes are not announced: the field is marked invalid and
  the error replaces the hint.
- The checkout button keeps its focus while the order is placed and reports "Placing order" as its name. With an empty
  cart it is disabled and dimmed.

### Keyboard

| Key | Where | Action |
|---|---|---|
| Enter, Space | Cart button | Open the drawer |
| Tab, Shift+Tab | Drawer | Move between the drawer's controls (cycles inside the drawer) |
| Escape | Drawer | Close the drawer (not while placing the order) |
| Enter | Promo field | Apply the code |
| Enter, Space | Any button | Press it |

## Motion

- **Fly to cart**: one progress value runs 780 ms on `cubic-bezier(.3, 0, .2, 1)`. A copy of the picture follows a
  quadratic Bézier from the picture's center to the bag's center, with the control point 55% of the way across and 30 px
  above the higher end; it shrinks to 26 × 26 by 80% of the way, leans up to −10° half way, fades over the last 14% and
  lifts its shadow with sin(πp). The picture on the card stays where it is. Several flights can be in the air at once.
- **Landing**: the badge's digit rolls up (down when the count falls) on the smooth spring, fading with distance; the
  button bumps 1 → 1.09 → 0.98 → 1 in about 430 ms while a 1 px accent ring grows to 1.45 and fades from 0.45.
- **Add button**: the label crosses to "Added" (100 ms out, 240 ms in rising 2 px) while its width follows on the smooth
  spring, and returns after 1.4 seconds.
- **Drawer**: slides in from 104% of its width on the smooth spring and out on the settle spring; the scrim fades in
  280 ms.
- **Numbers**: the count, line prices, totals and the checkout amount roll digit by digit on the smooth spring. The
  free-shipping bar grows on the smooth spring and turns green in 480 ms; the truck turns green in 240 ms. The
  free-shipping sentence and the shipping value swap by lifting the old text 5.6 px as it fades (160 ms) while the new one
  rises into place (240 ms).
- **Rows**: a removed row fades in 160 ms and its height collapses on the smooth spring after 77 ms. A swiped row slides
  out with its release speed at the same time; a short swipe springs back on the settle spring.
- **Promo code**: a refused code shakes the field (0, −6, 5, −3, 2, 0 px over 380 ms). An accepted code cross-fades the
  field into the chip, which grows from 0.96, while the footer's height and the Discount line follow on the smooth spring.
- **Checkout**: the label swaps to the spinner and "Placing order". On success the button's bounds and corner radius
  spring into the 64 px disc above the receipt (visual duration 0.6 s, no bounce), the check draws itself in 250 ms after
  400 ms, the cart fades out in 150 ms and the receipt fades in after 130 ms.
- **Reduced motion**: nothing flies and the badge counts at once; no bump or ripple; the drawer fades in 180 ms instead of
  sliding; rows cannot be swiped; numbers and bars change without springs; swaps keep a 120 ms cross-fade. **Off** applies
  every change at once.

## Responsive behavior

- The breakpoints read the block's width: 640 px or less (`:compact`) and 380 px or less (`:narrow`).
- Give the block a height (or put it in a container that limits it). The shop scrolls inside the block and the drawer is
  as tall as the block minus its insets, with its rows scrolling between the fixed header and footer.
- The drawer is 400 px wide; in a block too narrow for that it fills the block's width minus the insets.
- Hover only changes colors. Every action works with a tap; swiping works with touch, a pen or a mouse.

## Related

- `Drawer` for a window-level side panel.
- The Checkout summary block for an order summary with promo codes and a pay button.
- `AnimatedCounter` for a single rolling number.
- `Button`, `Input` and `Progress`, the controls the drawer's parts follow.
