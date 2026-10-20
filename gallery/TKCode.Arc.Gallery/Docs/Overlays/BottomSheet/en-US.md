---
title: Bottom sheet
summary: A sheet that rests at a peek or full height and follows your finger.
---

## When to use

- Secondary, mobile-first tasks such as trip details, filters or share options.
- Content that shows a peek first and expands to nearly full height.
- Map or media views where the user drags the sheet between heights.

## When not to use

- Decisions that must interrupt the whole screen: use `Dialog`.
- Side panels on wide layouts that keep the page visible: use `Drawer`.
- Small content anchored to a control: use `Popover`.

## Usage

A `BottomSheet` rises from the bottom edge and rests at one or more heights, called detents. The default detents are
0.45 and 0.92 of the window height.

### Basic sheet

```xml
<arc:BottomSheet Title="Trip to Lisbon"
                 Description="3 nights · 12–15 June"
                 IsOpen="{Binding ShowTrip}">
    <StackPanel Spacing="12">
        <TextBlock Text="Fri · Arrive 14:05 at LIS, taxi to Alfama" />
        <TextBlock Text="Sat · Tram 28 and the castle" />
    </StackPanel>
</arc:BottomSheet>
```

### Detents

`Detents` lists the resting heights as fractions of the window height. Use `BottomSheetDetents.Parse` to set them
from a string, and `InitialDetentIndex` to choose the detent the sheet opens at. A single detent is a fixed height.

```csharp
share.Detents = BottomSheetDetents.Parse("0.3, 0.6, 0.95");
share.InitialDetentIndex = 1;
```

### Opening and moving between detents

`OpenCommand` opens the sheet from a button. `Expand`, `Collapse` and `SnapTo` move it between detents, and
`DetentChanged` reports each resting position.

```csharp
openTrip.Command = trip.OpenCommand;

trip.DetentChanged += (_, e) => detent.Text = $"Resting at {e.Fraction:P0}";
trip.Expand();
```

### Long content

The content is not virtualized. Keep long lists paged, and let the sheet scroll the content once it is fully open.

```xml
<arc:BottomSheet Title="Itinerary" IsOpen="{Binding ShowItinerary}">
    <ItemsControl ItemsSource="{Binding Items}" />
</arc:BottomSheet>
```

### Closing

`Closed` reports why the sheet closed, such as `Gesture` for a fling or a drag down, `PressOutside` or `Escape`.

```csharp
trip.Closed += (_, e) => detent.Text = $"Closed by {e.Reason}";
```

## API reference

<!-- api: BottomSheet -->

<!-- api: BottomSheetDetents -->

## Accessibility

- The sheet takes its name from `Title` and its help text from `Description`. Focus starts on the sheet itself.
- The grabber is a button that exposes its expanded state. Its name is "Expand sheet" or "Collapse sheet".
- On the grabber, ArrowUp and ArrowDown move one detent, Home and End go to the highest and lowest detents, and Enter or
  Space toggles between the lowest and highest.
- Changes of detent are announced politely. Escape closes the sheet. Its close button is named "Close" by default; set
  `CloseButtonName` to change it.

## Motion

- One motion value drives the sheet. A drag follows the pointer, and past the tallest detent the sheet stretches with
  a rubber band of up to 120px.
- On release the sheet settles on the nearest detent, or closes when it is dragged down. A flick faster than 320px/s
  always moves it at least one detent.
- The dim behind the sheet depends on its position. At the lowest detent it keeps 78% of the full dim, and it fades
  as the sheet moves down.
- Reduced motion makes detents jump and fades the sheet in and out. Off shows or removes the sheet at once.

## Related

- `Drawer` for a side panel on wide layouts.
- `Dialog` for a decision that must be answered before anything else.
- `Popover` for small content anchored to a control.
