---
title: Sticky
summary: Keep any element of a scrolling page at the top once it scrolls there, like CSS sticky: toolbars stay in view and group headers push each other away.
---

<Demo name="Preview" />

## When to use

`Sticky` is a set of attached properties, not a control: put `arc:Sticky.IsEnabled="True"` on any element inside a
`ScrollViewer` and it scrolls normally until it reaches the top of the viewport, then stays there, like CSS
`position: sticky; top: 0`.

- **Toolbars and filter bars** that should stay in reach on a long page.
- **Section headers** in a grouped list, where each header holds the top until the next group pushes it away.
- **Summaries** such as a cart total that follows the reader down a form.

## When not to use

- **The element should never scroll at all**: put it outside the `ScrollViewer`, for example in a `DockPanel` above it.
- **Horizontal scrolling**: `Sticky` follows the vertical offset only; columns that stay put while scrolling sideways
  need a table with frozen columns.
- **The element already has a `RenderTransform`**: `Sticky` moves it with a render translation of its own (see below).

## Usage

Set the attached properties on an element in the scrolling content. Nothing else is needed: no placeholder, no change
to the `ScrollViewer`.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <ScrollViewer Height="300">
        <StackPanel>
            <TextBlock Text="Title" Margin="16" />
            <Border arc:Sticky.IsEnabled="True" Background="White" Padding="16,8">
                <TextBlock Text="Toolbar" />
            </Border>
            <TextBlock Text="Long content" Height="1000" />
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

How it works:

1. The element listens to its effective viewport (the part of it that the `ScrollViewer` shows). Once the viewport's top
   edge passes the element, the element is translated down by the difference, so it stays at the top.
2. Only the element's `RenderTransform` changes, never the layout: the content keeps its height and nothing below it
   jumps.
3. The element never leaves its parent. When the parent's bottom edge reaches it, it scrolls away with the parent.
4. Content that loads later or changes size above or around the element is picked up on the next layout pass.

### Styling the stuck state

`Sticky.IsStuck` is true while the element is held at the top. Select it to add a background, an edge or a shadow:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <UserControl.Styles>
        <Style Selector="Border.toolbar[(arc|Sticky.IsStuck)=True]">
            <Setter Property="BoxShadow" Value="{arc:ArcDesignTokenResource ShadowResting}" />
        </Style>
    </UserControl.Styles>
    <!-- ... -->
</UserControl>
```

Give the element an opaque background, since the content scrolls underneath it. The `StuckChanged` event (bubbling,
with `StickyStuckChangedEventArgs.IsStuck`) is raised on the element whenever `IsStuck` changes; in code use
`Sticky.AddStuckChangedHandler` and `Sticky.RemoveStuckChangedHandler`.

### Grouped lists

Put each header and its rows in their own panel. A header sticks only while its group is in view, so the next group's
header pushes it away as it arrives, as in a contact list.

<Demo name="GroupedList" />

### Offset

`Sticky.Offset` keeps a gap between the viewport's top edge and the stuck element, for example to let a floating pill
breathe or to stop under another bar.

<Demo name="Offset" />

### Limitations

- **RenderTransform**: the element must not have a `RenderTransform` of its own. If it has one when `Sticky.IsEnabled`
  is set, `Sticky` leaves the element alone and logs a warning. Wrap the element in a `Border` and make the wrapper
  sticky instead.
- **ZIndex**: while stuck the element's `ZIndex` is raised to `Sticky.ZIndex` (1 by default) so that it draws over the
  content scrolling under it, and restored afterwards. `ZIndex` only orders siblings in the same panel, so a sticky
  element nested deep inside one panel can still be covered by a later sibling panel's content. Keep the sticky element
  a direct child of the scrolling content, or give the panel that contains it a `ZIndex`.

## API reference

<!-- api: Sticky -->

## Accessibility

- `Sticky` only moves the element on screen. The reading and Tab order stay those of the content, and the element's
  controls keep working while it is stuck.
- A stuck bar covers the content scrolling under it. When keyboard focus moves to a control under the bar, the
  `ScrollViewer` brings it into view at the top, where the bar may hide it; leave room with `Sticky.Offset` or a top
  margin on the content where this matters.

## Motion

- Sticking follows the scroll position directly and has no animation of its own.
- Changes you attach to `IsStuck` (a shadow, a background) can fade: declare their transitions under `:motion-full`
  with `arc:ArcMotion.Tracks="True"` on the element, as the Preview does, so they turn off with reduced motion.

## Responsive behavior

- The element keeps its own width and layout; only its vertical position changes while stuck.
- When the viewport or the content changes size, the position is worked out again on the next layout pass.

## Related

- `ScrollArea`: a scrolling region in Arc's style.
- `TocProgress`: a table of contents that follows the reader down a page.
