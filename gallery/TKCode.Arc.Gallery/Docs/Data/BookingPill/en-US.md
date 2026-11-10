---
title: Booking pill
summary: A compact booking bar for place, dates and guests. Each segment opens its own picker, and the pill widens around the open one while a raised chip glides under it.
---

<Demo name="Preview" />

## When to use

`BookingPill` puts a whole booking search in one rounded bar: where, when and who, then a search button. Each segment
opens its own picker under the pill: a list of places you can filter, a month where the stay is drawn as a band from
check-in to check-out, and steppers for adults, children and infants. While a picker is open the pill reshapes around
its segment: that segment widens, a raised chip glides under it, and the search button grows to show its word. Picking a
place moves on to the dates, and picking the check-out moves on to the guests.

- **Stays and rentals**: rooms, cabins, flats.
- **Tables and experiences**: a place, a day and a party size.
- **Any search with three short facets** that people fill in one after another.

## When not to use

- **Many filters**: price, type and amenities belong in a filter panel or a sheet.
- **Free-text search**: a single search field is simpler.
- **Very narrow screens**: under about 480 px the segments get tight; a full-screen flow fits better on a phone.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:BookingPill>
        <arc:BookingPlace Name="Lisbon" Detail="Old town by the river" />
        <arc:BookingPlace Name="Kyoto" Detail="Temples and quiet lanes" />
    </arc:BookingPill>
</UserControl>
```

`BookingPill` is in the Special package; register it with `UseArcSpecial()` next to `UseArcControls()`. Put the places
it offers in it as `BookingPlace` items. `Place`, `CheckIn`, `CheckOut`, `Adults`, `Children` and `Infants` hold the
choices and can be bound both ways; `OpenPart` says which picker is open, and `Open()` and `Close()` do the same from
code. The search button raises `SearchRequested` with everything the pill holds.

### Filled in, with your own words

Every value can be set up front, and every word can be replaced: the segment labels, the empty values ("Anywhere",
"Add dates", "Add guests"), the formats for guests and nights, and the picker texts. `MinDate` (today by default) fades
earlier days, `MaxGuests` and `MaxInfants` cap the steppers, and `CultureName` sets the dates and month names.

<Demo name="Prefilled" />

### Best practices

**Do**

- Offer a short list of likely places; the field above filters it as people type.
- Keep the segment labels to one word, like "Where", "When" and "Who".
- Handle `SearchRequested` to run the search; the values are also in the properties.

**Avoid**

- Opening a picker on page load: let people start where they want.
- More than three segments; the pill is built for three.

## API reference

<!-- api: BookingPill -->

<!-- api: BookingPlace -->

## Accessibility

- Each segment is a button read as its label and value, such as "Where, Lisbon". The search button is named by
  `SearchText`.
- Opening a segment from the keyboard moves focus into its picker: the place field, the month, or the first + button.
  Escape closes the picker and puts focus back on the segment.
- A polite status line reads the segment that opened, the picked days ("Saturday, March 14, 2026, check-in") and guest
  counts.
- Pointer focus draws no ring; keyboard focus shows the focus ring around the segment, the search button or the day.

### Keyboard interactions

| Key | Action |
|---|---|
| Tab | Moves between the segments, the search button and the open picker. |
| Enter or Space | Opens or closes a segment's picker; picks a place or a day. |
| Down / Up | In the place picker, moves between the field and the places. |
| Arrow keys | In the month, move the day cursor; Page Up and Page Down change the month. |
| Home / End | In the month, go to the start or the end of the week. |
| Escape | Closes the picker. |

## Motion

- Opening a segment springs the widths of all three segments and the chip under the open one together, so the pill
  reshapes in one motion; switching segments glides the chip across.
- The pill's background darkens a step while a picker is open, and the search button's width springs to fit its word.
- The picker enters under the pill and crossfades its content when another segment opens.
- Guest counts roll.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Open a segment | Widths and chip spring | Jump | Jump |
| Switch segments | Chip glides, picker crossfades | Jump | Jump |
| Search button | Width springs to its word | Jumps | Jumps |
| Guest count | Rolls | Jumps | Jumps |

## Responsive behavior

- The pill fills the width it is given; the three segments share it and their values end with an ellipsis when tight.
- Pickers are 340 px wide and float over the page under the pill: the place picker on the left, the dates in the middle
  and the guests on the right, flipping above the pill when there is no room below.
- A press anywhere outside the pill and the picker closes it.

## Related

- `DateRangePicker` picks a range in a field of its own.
- `SearchField` is a plain search box.

| Component | Choose it when |
|---|---|
| `BookingPill` | Place, dates and guests in one compact search |
| `DateRangePicker` | Only a range of dates |
| `SearchField` | A single text search |
