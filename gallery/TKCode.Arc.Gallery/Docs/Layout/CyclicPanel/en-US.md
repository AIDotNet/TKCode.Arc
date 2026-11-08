---
title: Cyclic panel
summary: A list that loops without a seam: after the last item comes the first, scrolled by wheel, touchpad or code, with the visible items reported.
---

<Demo name="Preview" />

## When to use

`CyclicPanel` shows its children as an endless loop: scrolling past the last child brings back the first, in both
directions, with no jump and no copies of your data. The viewport holds a fixed number of equal slots.

- **Wheel pickers** for hours, minutes, months or other values that wrap.
- **Rotating strips** of cards, media or shortcuts.
- **Timelines and day strips** that cycle through a week.

## When not to use

- **A list with a real start and end**: use a `ScrollViewer` or `ScrollArea`.
- **Paged slides with pagination dots and autoplay**: use `Carousel`.
- **Thousands of items**: the panel creates every child; it does not virtualize.

## Usage

`CyclicPanel` derives from Avalonia's `Panel` and scrolls itself, so it does not need a `ScrollViewer`. It splits its length
into `VisibleItemCount` slots (5 by default) with `Spacing` (0) between them; a 400 px tall panel with five slots gives every
item 80 px. Children stretch across the panel.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:CyclicPanel Orientation="Vertical" VisibleItemCount="5" Height="400">
        <TextBlock Text="Item 0" />
        <TextBlock Text="Item 1" />
        <TextBlock Text="Item 2" />
        <TextBlock Text="Item 3" />
        <TextBlock Text="Item 4" />
        <TextBlock Text="Item 5" />
    </arc:CyclicPanel>
</UserControl>
```

The scroll position is `Offset`, in items: 2.5 means the leading edge is halfway through the third item. At rest it is always
wrapped into the range from 0 up to the number of items, so it never grows however long the panel scrolls.

Each child is placed once, at the position nearest the viewport. A control cannot be in two places, so the loop is seamless
when there are at least `VisibleItemCount` + 1 visible children. With fewer, every child is still shown once and the
remaining slots stay empty. Collapsed children (`IsVisible="False"`) are left out of the loop.

### Horizontal

`Orientation="Horizontal"` lays the slots out left to right. Touchpads scroll it sideways, and a vertical wheel turn moves it
too.

<Demo name="Horizontal" />

### Visible item count

Changing `VisibleItemCount` resizes the slots; the scroll position stays where it was.

<Demo name="VisibleItemCount" />

### Scrolling from code

- `ScrollToIndex(index)` brings the child to the leading edge (top or left).
- `ScrollToIndexCentered(index)` brings it to the middle slot; with an even `VisibleItemCount` it is centred between the two
  middle slots.
- `ScrollNext()` and `ScrollPrevious()` move by one item, `ScrollBy(items)` by any number. Calls made while a scroll is
  running add to its target, so quick repeated calls are never lost.

Every scroll takes the short way around the loop: from item 0 to item 7 of eight goes back one item, not forward seven.
The index methods return `false` and do nothing for an index outside the children or for a collapsed child. Pass
`animated: false` to jump.

<Demo name="ScrollToIndex" />

### Wheel and touchpad

A mouse wheel notch moves one item. A touchpad or smooth wheel follows the fingers, and when they stop the panel settles on
the nearest item (`SnapsToItems`, true by default). `IsWheelEnabled="False"` leaves the wheel to the parent.

### Visible items

`VisibleItemIndices` lists the child indexes in the viewport in display order, leading edge first; an item counts when any
part of its slot is inside the panel, so a half-scrolled panel reports one more. It is an Avalonia property and changes only
when the list changes, so bind to it or watch it to preload images or track the current item.

```csharp
panel.PropertyChanged += (_, e) =>
{
    if (e.Property == CyclicPanel.VisibleItemIndicesProperty)
    {
        Preload(panel.VisibleItemIndices);
    }
};
```

### Animation

Scrolls from code and wheel notches glide for `AnimationDuration` (240 ms) along `AnimationEasing` (Arc's standard ease-out).
`IsAnimationEnabled="False"` makes them jump. `AnimationEasing` takes any Avalonia easing, such as `CubicEaseInOut`, or
cubic Bézier control points.

<Demo name="Animation" />

### In an ItemsControl

As an `ItemsPanel` the panel loops the item containers; indexes are item indexes. Items added to or removed from an
`ObservableCollection` join or leave the loop, and the position stays inside it.

<Demo name="ItemsControl" />

## API reference

<!-- api: CyclicPanel -->

## Accessibility

- The panel adds no semantics of its own and no keyboard handling; it scrolls with the wheel, the touchpad and its methods.
  For a keyboard-driven picker, call `ScrollNext()` and `ScrollPrevious()` from your own key handling (for example Up and
  Down on the focused item).
- Items scrolled out of view are still children and stay in the Tab order; give interactive items their own accessible
  names (`AutomationProperties.Name`).

## Motion

- Scrolls glide on the motion engine's tween for `AnimationDuration` with `AnimationEasing`, taking the short way around the
  loop; a new scroll starts from where the running one is.
- A touchpad scroll follows the fingers directly, then glides to the nearest item.
- With reduced motion, or motion off, every scroll jumps: a glide moves the content, and reduced motion removes movement.

## Responsive behavior

- The slots always share the panel's length, so items grow and shrink with it; the number of slots stays `VisibleItemCount`.
- When the length is unbounded (for example in a `StackPanel` along the scroll direction) every slot is as long as the
  longest item. Give the panel a size, or put it in a `Grid` cell.

## Related

- `Carousel`: paged slides with dots, arrows and autoplay.
- `WaterfallPanel`: equal columns of children with free heights.
- `TilePanel`: a grid of tiles that span rows and columns.
