---
title: Wallet stack
summary: A wallet of payment cards in real materials: fan the stack, flick through it and lift a card out to see its balance and recent activity.
---

<Demo name="Preview" />

## When to use

`WalletStack` shows a handful of cards the way a phone wallet does: a short stack with the front card on top, each card
drawn in its own material with a chip, a contactless mark, an embossed number and a light that follows the pointer.
Choosing a card lifts it to the top while the others tuck away below, and its balance and latest activity appear under
it. Putting it back is one press, one key or one pull downward.

- **A few payment cards, passes or memberships** where people open one at a time to see what is on it.
- **Wallet and banking screens** that want the physical card metaphor: real finishes, depth and a stack that can be
  thumbed through.
- **Account pickers with personality**: the open card is the selected account and `SelectedCard` tells you which.

## When not to use

- **Many accounts**: past five or six cards a stack hides too much. Use a list or a `DataGrid`.
- **Swipeable content that is not a card wallet**: use `CardStack`.
- **One card that grows in place**: use an expandable card.
- **Comparing balances side by side**: a stack shows one card at a time; use a table or a set of tiles.

## Usage

Give the stack its cards in order (the first is in front) and a label. Everything else has a default: US dollars in
en-US, the built-in fictional network mark and English texts.

```xml
<arc:WalletStack Label="Mira’s wallet" Cards="{Binding Cards}" SelectedCard="{Binding OpenCard}" />
```

```csharp
var cards = new List<WalletStackCard>
{
    new()
    {
        Id = "current", Issuer = "Larkspur", Product = "Current account", LastFour = "5306", Holder = "Mira Castell",
        Expires = "08/29", Material = WalletStackMaterial.Metal, Balance = 2914.35m, BalanceLabel = "Available",
        Transactions =
        {
            new() { Id = "t1", Merchant = "Pinecrest Power", Detail = "Electricity · This morning", Amount = -96.40m, Icon = WalletStackIcons.Zap },
        },
    },
};
```

### Anatomy

- **Header**: the label on the left; on the right the count ("1 of 4"), which gives way to an "All cards" button while a
  card is open.
- **Stage**: the cards, as wide as the wallet plus 12px on each side and 220px taller than a card. The front card sits
  low, every card behind it 5% smaller and a little higher; cards that were cycled past wait in a pocket at the bottom
  edge, showing only their top.
- **Card face**: issuer and product, an EMV chip, a contactless mark, the masked number with the last four digits,
  holder, "Valid thru" and expiry, and a network mark. Every detail is a fixed share of the card width, so a narrow
  wallet scales the whole face.
- **Activity**: under the open card, the balance with its label over a rule, then the three latest transactions with a
  round icon or initials, merchant, detail and a signed amount.

### Materials

`Material` picks the finish: `Metal` (the default, brushed with a turned grain and a soft glint), `Black` (fine grain
and an engraved rosette), `Glass` (frosted: the cards behind show through, blurred and a little more saturated) and
`Color` (any `Tint`, shaded from lighter at the top left to deeper at the bottom right). `Artwork` places your own
image under the finish; `NetworkMark` replaces the fictional mark and `ShowNetwork="False"` hides it.

### Gestures

- **Hover** with a mouse fans the stack upward; the hovered card rises 8px and its shadow deepens, and it leans up to 5
  degrees toward the pointer while a highlight follows it. Touch never fans or tilts.
- **Drag up or down** anywhere on a card to cycle. The front card follows the pointer one to one into the pocket while
  the next one comes forward; a drag starts after 8px, so a smaller wobble still counts as a press. A release looks 0.2
  seconds ahead at its speed and settles on the nearest card, never more than one away from where the drag began. Past
  the first or last card the stack stretches with resistance and springs back.
- **Click** any visible card to open it, even one behind the front card. While a card is open, click a tucked card to
  switch, or click the open card to put it back.
- **Pull the open card down** to put it back: it follows at about half the pointer's speed and closes once the
  projected position passes halfway; a flick downward closes it from higher up.

### Your own data and events

`PositionChanged` reports the card that comes to the front and `SelectedCardChanged` the card that opens (or `null`
when it goes back). Both properties are two-way, and `Next`, `Previous`, `GoTo`, `Open` and `Close` drive the stack
from code.

<Demo name="Events" />

### Currency, culture and labels

`Currency` (an ISO 4217 code) and `Culture` format every amount: balances in the currency style, transactions with "+"
for money in and a true minus sign for spending. `Labels` holds every text the stack shows or speaks; the sentences with
values are delegates, so a translation can reorder them.

<Demo name="Localized" />

### Narrow containers

The wallet is at most 296px wide and takes the width it is given below that. The card keeps the ID-1 proportions and the
stage stays 220px taller than a card.

<Demo name="Narrow" />

### Reduced motion

<Demo name="ReducedMotion" />

### Content guidelines

- Keep issuer names short (one or two words) and products to a few words; long text is cut with an ellipsis on the card.
- Write `BalanceLabel` as a phrase that reads after the amount's meaning: "Available", "Statement balance", "Stored
  value". It is lowercased in the card's spoken name.
- Use the transaction detail for "category or place · time", and use initials or a picture only for people.
- Use real expiry dates in "MM/YY" or leave `Expires` empty.

### Best practices

- Order cards by how often they are used: the first card is the one people see.
- Keep to four or five cards; deeper cards wait hidden behind the fourth.
- Pick materials that differ in lightness, so the edges in the stack stay distinct.
- Bind `SelectedCard` when other parts of the screen depend on the chosen card, instead of reading it on demand.

### Common questions

**Can a card change after it is shown?** The stack reads a card when it arrives. Replace the card in the list (an
`ObservableCollection` is watched) to show new data; the open card stays open when its `Id` is unchanged.

**Why does the balance count?** Opening a card counts its balance up from zero, and switching counts from the previous
card's amount, on the same spring that moves the cards. Under reduced motion the amount appears at once.

**Does the glass blur the page?** It blurs the other cards of the stack over the theme background. Anything else behind
the stage is not part of the frosted backdrop.

## API reference

<!-- api: WalletStack -->

<!-- api: WalletStackCard -->

<!-- api: WalletStackTransaction -->

<!-- api: WalletStackLabels -->

<!-- api: WalletStackSelectedCardChangedEventArgs -->

<!-- api: WalletStackPositionChangedEventArgs -->

<!-- api: WalletStackSamples -->

<!-- api: WalletStackIcons -->

| Type | Values |
|---|---|
| `WalletStackMaterial` | `Metal` (default), `Black`, `Glass`, `Color` |

## Accessibility

- The stage is a group named by `Label`, described by a keyboard hint. Only the front card (or the open card) is in the
  tab order.
- Each card is a button with an expanded state. Its name reads the issuer, product, last four digits, position and
  balance ("Larkspur Current account ending in 5306, 1 of 4, available $2,914.35"); the masked digits, chip and marks are
  not read.
- A polite live region announces "… in front, 2 of 4" when another card comes forward and "… open" when a card opens.
- The activity is a region named "{issuer} activity" with a list named "Recent activity on {issuer}"; it is hidden from
  assistive technology and input while no card is open.
- Keyboard focus draws a 2px ring 2px outside the card, tilted with it; pointer focus draws none.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Moves to the front card; with a card open, first to the All cards button, then to the open card |
| `Down` / `Right` | Brings the next card forward; while a card is open, opens the next card |
| `Up` / `Left` | Brings the previous card forward; while a card is open, opens the previous card |
| `Home` / `End` | The first or last card |
| `Enter` / `Space` | Opens the front card, or puts the open card back |
| `Escape` | Puts the open card back and keeps focus on it |

The arrows do not wrap around. Focus follows the card that comes forward.

## Motion

- Fanning, hover lift, tilt and light, cycling, opening and closing all move on one spring without overshoot (about 0.4
  s visual duration). Interruptions keep their speed: a card can be caught mid-flight and pressing a key again retargets
  the same motion.
- Opening raises the card to the top at 88% while the others tuck into a row at the bottom; the activity rises 16px and
  fades in over the last part of the opening. The rows fade in 80, 136 and 192ms after the body appears.
- The count's number rolls 6px up as it changes; the All cards button fades and grows in 40ms after the count fades out.
- A pressed card shrinks to 98.5% over 160ms.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Fan, lift, cycle, open | Springs | Instant | Instant |
| Tilt and light | Follow the pointer | Flat, light at rest | Flat, light at rest |
| Activity rows | Fade and rise, staggered | Fade, staggered | Shown |
| Balance | Counts up | Final amount | Final amount |
| Count and All cards | Roll and fade | Fade | Replaced |

## Responsive behavior

- `WalletStack` has no breakpoint: it is as wide as its slot up to 296 px, and every part of a card (number, chip,
  mark, spacing) scales with that width, so a 260 px wallet looks like a smaller copy of the full one.
- The stage under the stack is the card height plus room for the fan and the pocket, so leave the wallet its natural
  height rather than squeezing it.
- On touch there is no fan and no tilt (they follow a mouse): drag up or down to cycle cards, tap a card to open it and
  drag the open card down to put it back. The arrow keys, the card buttons and "All cards" do the same without a
  gesture.

## Related

- `CardStack` for swipeable content cards.
- `HoloCard` for a single card with a holographic foil that flips.

| Component | Choose it when |
|---|---|
| `WalletStack` | A few cards, one opened at a time into its balance and activity |
| `CardStack` | Content cards people swipe through |
| `HoloCard` | One collectible-style card that tilts and flips |
| `DataGrid` | Many accounts compared in columns |
