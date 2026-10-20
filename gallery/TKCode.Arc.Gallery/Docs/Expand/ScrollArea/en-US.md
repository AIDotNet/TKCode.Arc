---
title: Scroll area
summary: A native scroll container with thin overlay scrollbars and edge fades that appear only when content overflows.
---

## When to use

- Content that can overflow a panel, a side bar or a popup, where the page layout must stay fixed.
- A horizontal strip of cards or tags that scrolls with the wheel and snaps to its items.
- A region that needs to report whether more content exists beyond an edge, for example to load more items.

## When not to use

- Paging through items one at a time: use `Carousel`.
- Wrapping a whole page: let the page or a `ScrollViewer` own the scrolling.
- Large data sets in a grid: use a table control that virtualizes its rows.

## Usage

Set the axes with `Orientation`. The default is `Vertical`.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
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

`Label` names the scroll area. A named area is announced as a region.

### Horizontal strip

A vertical wheel scrolls a horizontal area sideways while it can, then hands the wheel back to the page. Set
`HorizontalSnapPointsType` to make the items snap.

```xml
<arc:ScrollArea Orientation="Horizontal"
                HorizontalSnapPointsType="Mandatory"
                Label="Cards">
    <StackPanel Orientation="Horizontal" Spacing="12" Margin="16">
        <Border Width="160" Height="100" CornerRadius="18" Background="#F4F4F5" />
        <Border Width="160" Height="100" CornerRadius="18" Background="#F4F4F5" />
        <Border Width="160" Height="100" CornerRadius="18" Background="#F4F4F5" />
    </StackPanel>
</arc:ScrollArea>
```

### Both axes and scrollbar visibility

Use `Both` for a grid that scrolls in two directions. `OverlayScrollbarMode="Always"` keeps the bars visible while
the content overflows. The default `Auto` shows them only while scrolling or hovering.

```xml
<arc:ScrollArea Orientation="Both"
                OverlayScrollbarMode="Always"
                Height="240" />
```

### Edge fades and hide delay

`FadeLength` sets the length of each edge fade in pixels (default 28). Set it to `0` to turn the fades off.
`HideDelay` sets how long the bars stay visible after scrolling stops (default 900 ms).

```xml
<arc:ScrollArea FadeLength="0"
                HideDelay="0:0:1.5" />
```

### Reacting to edges

`EdgesChanged` is raised only when an edge changes between having and not having content beyond it. The `Edges`
property holds the current state.

```csharp
area.EdgesChanged += (_, e) =>
{
    if (!e.Edges.Bottom)
    {
        LoadMoreItems();
    }
};
```

### Scrolling from code

`ScrollBy` and `ScrollToOffset` scroll the area. Under full motion they animate on a spring; under Reduced or Off
motion they jump to the target.

```csharp
area.ScrollBy(new Vector(0, 240));
area.ScrollToOffset(new Vector(0, 0));
```

## API reference

<!-- api: ScrollArea -->

## Accessibility

- `Label` sets the accessible name of the area. Without a label, the surrounding content must explain its purpose.
- The area is focusable. Arrow keys, Page Up and Page Down, Home, End and Space scroll it natively.
- Scrolling keeps the `ScrollViewer` automation pattern. The overlay bars are visual only.
- Under Reduced motion, the track page jumps instead of animating.

## Motion

- Bars fade in while the content scrolls or the pointer hovers over the area. They fade out 900 ms after scrolling stops.
- The thumb gets thicker under the pointer, and it can be dragged.
- Each edge fade grows with the first pixels of scrolling and is never longer than the hidden content.
- Reduced motion removes the bar fade and makes track paging instant.

## Related

- `Carousel` pages through items one at a time.
- `ExpandableText` clamps a single block of text to a few lines instead of scrolling it.
- `ResizablePanels` splits space between panes; put a `ScrollArea` inside a pane when that pane can overflow.
