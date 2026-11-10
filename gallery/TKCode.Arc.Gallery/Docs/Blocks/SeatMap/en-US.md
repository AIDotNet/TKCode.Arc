---
title: Seat map
summary: Choose seats on a map of a venue or a cabin: sections colored by price tier, taken seats, a legend with prices, keyboard and pointer picking up to a limit, and a summary with fees and a total.
---

<Demo name="Preview" />

## When to use

`SeatMap` lets people choose seats on a plan: a hall with a stage, or a cabin with an aisle. Seats are drawn by section in
the color of their price tier, sold seats are hollow, and the legend lists the tiers with their prices. Seats are chosen with
the pointer or the keyboard, up to a limit, and the summary lists them with the fees and the total before Continue.

- **Ticketing** for concerts, theaters and events.
- **Seat choice** for flights, trains and buses.
- **Room or desk booking** laid out in rows.

## When not to use

- **General admission**: a quantity field is enough.
- **Free-form floor plans** with tables and shapes: use a drawing or a diagram.

## Usage

The block derives from Avalonia's `TemplatedControl`. Fill `Tiers` with `SeatMapTier`s and `Sections` with
`SeatMapSection`s, and set `TakenSeats` to the ids of sold seats ("ORC-C-12": section, row letter, number).

```csharp
map.Tiers.Add(new SeatMapTier { Id = "premium", Name = "Premium", Price = 120, ColorIndex = 1 });
map.Sections.Add(new SeatMapSection
{
    Id = "ORC", Name = "Orchestra", TierId = "premium", Rows = 5, SeatsPerRow = 16, X = 4, Y = 1, Curve = 1.2, AisleAfter = [8]
});
```

### Layout

A section's rows are lettered from `FirstRow` and its seats numbered from 1. `X` and `Y` place it in map units (a seat is
about one unit wide), `Curve` bows its rows toward the stage and `AisleAfter` opens an aisle after the given seat numbers.
The map scales to fit the block.

### Choosing seats

`ToggleSeat` (or a press, or **Enter** on the cursor) chooses or releases a seat; taken seats cannot be chosen and no more
than `MaxSelection` at once ("You can choose up to 6 seats."). `SelectedSeats` and `Total` (prices plus `ServiceFee` per
seat) follow, `SelectionChanged` reports every change and Continue raises `ContinueRequested`.

### A cabin

The same block draws a cabin: each side of the aisle is a section, its rows are seat letters and its seat numbers the
cabin rows. The labels name seats as "Seat 12C".

<Demo name="Cabin" />

## API reference

<!-- api: SeatMap -->

<!-- api: SeatMapSection -->

<!-- api: SeatMapTier -->

<!-- api: SeatMapSeat -->

## Accessibility

- The map is one focusable control named "Seat map" with help text for the keys. As the cursor moves, its name becomes the
  seat with its price and state ("Orchestra · Row C · Seat 12, $120, available").
- **Arrow keys** move to the nearest seat in that direction, **Home** and **End** to the first and last seat, **Enter** or
  **Space** choose or release it. The cursor has a focus ring only for keyboard focus.
- Each chosen seat in the summary has a remove button named "Remove Orchestra · Row C · Seat 12".
- A polite live region announces each choice and the limit.

## Motion

- Seat states change at once; the readout follows the pointer or the cursor. The map has no ongoing animation.

## Responsive behavior

- Below 780 px the summary moves under the map.
- Seats scale with the block's width, up to 28 px per seat.

## Related

- The Seat manager block, which counts team seats in a plan (not places on a map).
- The Checkout summary block for the payment step after Continue.
