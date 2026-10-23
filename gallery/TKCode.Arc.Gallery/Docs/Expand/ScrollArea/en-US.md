---
title: Scroll area
summary: A native scroll container with thin overlay scrollbars and edge fades that appear only when content overflows.
---

<Demo name="Vertical" />

## When to use

`ScrollArea` gives a fixed-size region its own scrolling, so a panel can hold more content than its space without
changing the layout of the page around it. It sits inside a shell, a side panel or a dialog, where the frame is
set and the content inside it grows with the data.

- **Side panels and inspectors**: the activity timeline of a customer record or the line items of an order, beside
  a list that must stay visible. The panel keeps its height and the timeline scrolls inside it.
- **Horizontal strips of cards or tags**: a row of plan cards on a billing page or a row of filter chips above a
  table. `Horizontal` with a mandatory snap keeps each card aligned when the person scrolls with the wheel or the trackpad.
- **Feeds that load more**: a CRM activity feed or a delivery log that fetches the next page when the bottom edge
  reports that nothing is left beyond it. `EdgesChanged` supplies that signal.
- **Payloads and logs in developer tools**: a webhook request body or a deployment log in a fixed-height panel,
  where the person reads from the top and the fade shows that more text follows.
- **Option lists in a sheet or a popover**: a long list of regions or currencies that does not fit the overlay.

## When not to use

- **Paging through items one at a time**: use `Carousel`, which moves between slides and shows position.
- **Long text that should be shortened**: use `ExpandableText`, which clamps to a few lines and offers Read more.
- **Tabular data with columns and sorting**: use `SortableDataTable`, or `DataGrid` for large data sets. A `ScrollArea`
  around a plain `StackPanel` measures every row, so a table of thousands of rows is slow.
- **A tree of nested items with keyboard navigation**: use `TreeView`, which handles expansion and arrow-key
  movement between levels.
- **Choosing one value from a long list**: use `Select` or `Combobox`, which own the list, its filter and its
  keyboard model.

## Usage

Set the axes with `Orientation`. The default is `Vertical`.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ScrollArea Width="360"
                    Height="260"
                    Label="Release notes"
                    CornerRadius="18">
        <StackPanel Spacing="0">
            <TextBlock Text="v2.4 · Overlay scrollbars" Margin="16,12" />
            <TextBlock Text="v2.3 · Faster cold starts" Margin="16,12" />
            <TextBlock Text="v2.2 · Keyboard shortcuts" Margin="16,12" />
        </StackPanel>
    </arc:ScrollArea>
</UserControl>
```

`ScrollArea` derives from Avalonia's `ScrollViewer`, so the offset, extent and viewport, the snap point properties,
touch inertia, `ScrollChanged` and the scroll automation pattern all work as they do on a scroll viewer. Arc
replaces the native bars with overlay tracks, adds the edge fades and changes the default focus and scrollbar
behavior. The area clips its content to its bounds, so the content never paints outside the frame.

`Label` names the scroll area. A named area is announced as a region. Without a label, the content around the
area must explain what it holds.

### Anatomy

1. **Content** (`Content`): the child you scroll. It keeps its natural size; the area's `Width` and `Height` set the
   visible window.
2. **Edge fades**: a 28 px gradient at each edge (`FadeLength`) that dims the content where it is cut off. A fade
   appears only where content continues past that edge, and its length equals the hidden content, up to the maximum.
3. **Vertical track**: an 11 px hit area (the `TrackThickness` token) around a 5 px pill, 3 px from the trailing
   edge. Under the pointer or while dragged the pill grows to 7 px and moves to 2 px from the edge.
4. **Thumb color**: the foreground color at 30% opacity, rising to 46% while the pointer is over the track.
   The thumb is never shorter than 28 px and its position follows the offset.
5. **Horizontal track**: the same pill along the bottom edge, 11 px tall.
6. **Corner**: with `Both`, each track stops 13 px short of the corner, so the two tracks never cross.
7. **Focus ring**: a 2 px ring in the focus color, drawn inside the area so the clipping does not cut it. It shows
   only for keyboard focus.

The tracks and fades are absent when nothing overflows. A short area has no fade, no track and no `:overflow`
pseudo-class, so a list that grows or shrinks switches the cues on and off with its content.

### Axes and overlay bars

Choose the axes first, then the bar behavior.

- `Vertical` is the default for lists, feeds, logs and side panels. The horizontal bars are hidden.
- `Horizontal` is for strips of cards, tags or chips. The vertical wheel scrolls it sideways (see below).
- `Both` is for grids and wide tables that scroll in two directions. Use it sparingly, because two tracks and two
  fades compete with the content for attention.

Changing `Orientation` at run time updates the native bar visibility and the track margins, so the axes can follow a
view mode.

`OverlayScrollbarMode` decides when the bars show. `Auto` (the default) shows them while the content scrolls or the
mouse hovers over the area, and keeps them for `HideDelay` after the scrolling stops. Hover reveals the bars only
for mouse input, so a touch screen shows them only while the person is scrolling. `Always` keeps them visible
whenever the content overflows. Use `Always` for a feed, a log or a payload where the person must see that more
content exists, and `Auto` for a decorative or secondary panel.

<Demo name="BothAxes" />

### Horizontal strip

A vertical wheel scrolls a horizontal area sideways while it can, then hands the wheel back to the page. Set
`HorizontalSnapPointsType` to make the items snap.

<Demo name="HorizontalStrip" />

The wheel conversion is controlled by `IsWheelToHorizontalEnabled` (default `True`). It applies only when the wheel
movement is mostly vertical, when `Ctrl` is not held and when the strip has room in the direction of the wheel.
Each wheel step moves the strip about 50 px. Set `HorizontalSnapPointsType="Mandatory"` for cards that must land
on an edge, and `None` (the default) for free scrolling.

### Edge fades and hide delay

`FadeLength` sets the length of each edge fade in pixels (default 28). Set it to `0` to turn the fades off.
`HideDelay` sets how long the bars stay visible after scrolling stops (default 900 ms).

```xml
<arc:ScrollArea FadeLength="0"
                HideDelay="0:0:1.5" />
```

### Driving it from code

`ScrollArea` has no value to bind, so it is driven from code. `Edges` is the current state of the four edges and
`EdgesChanged` reports each change. `ScrollBy` and `ScrollToOffset` scroll the area. Under full motion they animate
on a spring. Under Reduced or Off motion they jump to the target.

```csharp
area.EdgesChanged += (_, e) =>
{
    if (!e.Edges.Bottom)
    {
        LoadMoreItems();
    }
};
```

`EdgesChanged` is raised only when an edge changes between having and not having content beyond it, so it is the
right trigger for a load-more request. Read `Edges` when the state is needed at other times.

```csharp
area.ScrollBy(new Vector(0, 240));
area.ScrollToOffset(new Vector(0, 0));
```

`ScrollBy` and `ScrollToOffset` clamp the target to the content, so a large delta stops at the end. To bring a
specific item into view, call `BringIntoView()` on the item, which is the Avalonia method every control has.

### Example: activity feed beside a customer record

A customer record in a CRM with a fixed-height activity panel. The header names the account and the sort order, and
the feed scrolls inside its frame. The bar is set to `Always` because the feed is long and the person must see that
older events follow. The fade at the bottom edge confirms the same thing without a bar, and the top edge has no fade
because the feed starts at the newest event.

![An activity feed in a fixed panel with an always visible scrollbar and a fade at the bottom edge](images/ActivityFeed.png)

```xml
<StackPanel Width="560" Spacing="12">
    <TextBlock Text="Account activity"
               FontSize="16"
               FontWeight="Medium"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="Northwind · latest first"
               FontSize="13"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    <arc:ScrollArea Height="260"
                    Label="Account activity"
                    OverlayScrollbarMode="Always"
                    CornerRadius="12"
                    Background="{arc:ArcDesignTokenResource SurfaceMuted}">
        <StackPanel>
            <Border Padding="16,12"
                    BorderThickness="0,0,0,1"
                    BorderBrush="{arc:ArcDesignTokenResource Border}">
                <StackPanel Spacing="4">
                    <TextBlock Text="Oct 20, 2026  ·  Billing"
                               FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Text="Invoice INV-2026-0412 paid, 4,800 USD"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                </StackPanel>
            </Border>
            <Border Padding="16,12"
                    BorderThickness="0,0,0,1"
                    BorderBrush="{arc:ArcDesignTokenResource Border}">
                <StackPanel Spacing="4">
                    <TextBlock Text="Oct 18, 2026  ·  Seats"
                               FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Text="Seat count raised from 40 to 48"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                </StackPanel>
            </Border>
            <Border Padding="16,12"
                    BorderThickness="0,0,0,1"
                    BorderBrush="{arc:ArcDesignTokenResource Border}">
                <StackPanel Spacing="4">
                    <TextBlock Text="Oct 16, 2026  ·  Integrations"
                               FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Text="Webhook endpoint updated by Lena Ortiz"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                </StackPanel>
            </Border>
            <Border Padding="16,12"
                    BorderThickness="0,0,0,1"
                    BorderBrush="{arc:ArcDesignTokenResource Border}">
                <StackPanel Spacing="4">
                    <TextBlock Text="Oct 12, 2026  ·  Renewal"
                               FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Text="Renewal quote for 12 months sent to finance"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                </StackPanel>
            </Border>
            <Border Padding="16,12">
                <StackPanel Spacing="4">
                    <TextBlock Text="Oct 09, 2026  ·  Support"
                               FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Text="Ticket 5521 resolved after a token rotation"
                               FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                </StackPanel>
            </Border>
        </StackPanel>
    </arc:ScrollArea>
</StackPanel>
```

### Content guidelines

- **Label**: a noun phrase that names what the region holds, in sentence case: "Account activity", "Release notes".
  A screen reader announces it before the content, so "Scrollable area" tells the person nothing.
- **Headers and dates**: keep the header outside the area when it must stay visible while the content scrolls. Write
  dates as they appear everywhere else in the product, and write counts as digits.
- **Overflow cues**: do not add text such as "Scroll for more". The fade and the bar already say so, and the text
  takes space from the content.

| Instead of | Write |
|---|---|
| Label="Scrollable area" | Label="Account activity" |
| Label="list" | Label="Release notes" |
| Scroll for more below | (no text: the fade and the bar show it) |

### Best practices

**Do**

- Give every area a `Label` when the content is worth finding by a screen reader, because a named area is announced
  as a region.
- Give the area a bounded size from its parent. Scrolling needs a viewport smaller than the content; a viewport that
  grows with its content has nothing to scroll.
- Use `OverlayScrollbarMode="Always"` for feeds, logs and payloads where the person must see that more exists.
- Use `EdgesChanged` for load-more triggers instead of watching offsets, because it fires once per change, not on
  every scroll step.

**Avoid**

- Nesting two areas that scroll in the same direction. The wheel leaves the inner area only at its edge, so the
  outer area moves in unexpected jumps. Nest only when the axes differ.
- Relying on `Auto` bars on a touch-first screen. Hover reveals the bars only for a mouse, so a touch user sees them
  only while scrolling, and an idle panel gives no sign that it scrolls.
- Setting `FadeLength="0"` on a feed. Without the fade a cut line looks like the end of the content, so the person
  stops scrolling.
- Putting thousands of rows in a plain `StackPanel` inside the area. The area measures all of them, which is slow for
  large lists; use a table or a list control that manages its own rows.
- Wrapping a whole page. The page layout should own the scrolling of the screen, so that its header and footer stay
  in place; an area around the whole screen traps the scroll.

### Common questions

**Why does the keyboard not scroll my area?** Keys reach the area only when the area itself has focus. A focused
`TextBox` or `ListBox` inside the area handles the arrow keys first. Move focus to the area with `Tab` or by
clicking its background.

**Why does the scrollbar not show on my touch screen?** In `Auto` mode, hover reveals the bars only for mouse input.
Touch shows them while scrolling and hides them after `HideDelay`. Set `OverlayScrollbarMode="Always"` when the bar
is a required cue.

**Why does the wheel leave a horizontal strip at its end?** That is intended. At either end the strip has no room
left in the wheel's direction, so the wheel goes to the page, and the page scrolls.

**How do I keep the view at the bottom of a log when new lines arrive?** Call `ScrollToOffset` with a large `Y` after
you append a line, for example `area.ScrollToOffset(new Vector(0, area.Extent.Height));`, which clamps to the end,
but only when the person was already at the bottom. Otherwise the scroll would pull them away from the line they
are reading.

## API reference

<!-- api: ScrollArea -->

| Type | Member | Meaning |
|---|---|---|
| `ScrollAreaOrientation` | `Vertical`, `Horizontal`, `Both` | The axes the area scrolls. |
| `ScrollAreaScrollbarMode` | `Auto`, `Always` | When the overlay bars show. `Auto` shows them while scrolling or hovering. |
| `ScrollAreaEdges` | `Top`, `Bottom`, `Left`, `Right` | `True` when content continues beyond that edge. |
| `ScrollAreaEdgesChangedEventArgs` | `Edges` | The new edge state. |

## Accessibility

- `Label` sets the accessible name of the area, and a named area is announced as a region. Without a label, the
  surrounding content must explain its purpose.
- The area is focusable and is in the tab order. Arrow keys, Page Up and Page Down, Home, End and Space scroll it
  natively.
- Scrolling keeps the `ScrollViewer` automation pattern. The overlay bars are visual only, so a screen reader user
  scrolls through the content and does not operate the bars.
- Under Reduced motion, the track page jumps instead of animating.

### Keyboard

The keys apply when the area itself has focus. `Shift` changes the direction of `Space` only.

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move focus to or away from the area |
| `Up` / `Down` | Scroll a vertical area by 40 px |
| `Left` / `Right` | Scroll a horizontal area by 40 px |
| `Page Up` / `Page Down` | Scroll by 90% of the viewport along the main axis |
| `Space` / `Shift+Space` | Scroll forward or back by 90% of the viewport |
| `Home` / `End` | Scroll to the start or end of the axis. In `Both`, they move the vertical axis |

## Motion

- Bars fade in while the content scrolls or the pointer hovers over the area. They stay for `HideDelay` (900 ms)
  after scrolling stops, then fade out over 480 ms.
- The thumb gets thicker under the pointer, and it can be dragged. Dragging a thumb turns off snapping until the
  drag ends.
- Each edge fade grows with the first pixels of scrolling and is never longer than the hidden content.
- Reduced motion removes the bar fade and makes track paging instant.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Bars appear | Fade in over the fast duration | Appear at once | Appear at once |
| Bars leave | Fade out over 480 ms, after `HideDelay` | Hide at once | Hide at once |
| Thumb under the pointer | Thickens from 5 to 7 px and darkens over the fast duration | Changes at once | Changes at once |
| Key, code or track scroll | Smooth spring toward the target | Jumps to the target | Jumps to the target |
| Edge fades | Length follows the hidden content as the person scrolls | Same | Same |

## Related

- `Carousel` pages through items one at a time, with position shown.
- `ExpandableText` clamps a single block of text to a few lines instead of scrolling it.
- `ResizablePanels` splits space between panes; put a `ScrollArea` inside a pane when that pane can overflow.

| Component | Choose it when |
|---|---|
| `Carousel` | People move between items one at a time and need a position indicator |
| `ExpandableText` | One block of text is long and should show a few lines with Read more |
| `ResizablePanels` | The person needs to decide how much room each pane gets |
| `SortableDataTable` / `DataGrid` | The content is rows and columns that people sort or scan |
| `TreeView` | Items nest and people move between levels with the keyboard |
