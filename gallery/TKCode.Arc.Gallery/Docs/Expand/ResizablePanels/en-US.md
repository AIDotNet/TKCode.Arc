---
title: Resizable panels
summary: Trade space between panes by dragging the divider between them.
---

<Demo name="Default" />

## When to use

- Editor, mail or dashboard layouts where the user needs to decide how much space each pane gets.
- Side panes that can be collapsed to free space for the main pane.
- A layout that should be saved and restored between sessions.

## When not to use

- Panes that cover the page as an overlay: use `Drawer`.
- Showing one pane at a time: use `Tabs`.
- Stacking panes vertically: Arc supports horizontal splits only. Use a layout container instead.
- A narrow screen where the panes should stack: use `Tabs` and let the page change its layout.

## Usage

Add one `ResizablePanel` per pane to `Panels`. Each pane has a `DefaultSize` share. The shares are relative and do
not need to add up to 100. The group needs a definite height from its parent.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ResizablePanels Label="Mail layout" Height="320">
        <arc:ResizablePanel Id="folders"
                            Label="Folders"
                            DefaultSize="22"
                            MinSize="140"
                            IsCollapsible="True">
            <TextBlock Text="Inbox, Drafts, Sent, Archive" />
        </arc:ResizablePanel>
        <arc:ResizablePanel Id="messages"
                            Label="Messages"
                            DefaultSize="33"
                            MinSize="200">
            <TextBlock Text="42 conversations" />
        </arc:ResizablePanel>
        <arc:ResizablePanel Id="reading"
                            Label="Reading pane"
                            DefaultSize="45"
                            MinSize="220"
                            IsCollapsible="True">
            <TextBlock Text="Quarterly review" />
        </arc:ResizablePanel>
    </arc:ResizablePanels>
</UserControl>
```

`Label` names the group for assistive technology. Each pane's `Label` names its divider.

### Collapsed by default

Set `IsCollapsed="True"` on a collapsible pane to start it hidden. Its divider shows a tab that restores the pane.

<Demo name="CollapsedAndPersisted" />

### Limits

`MinSize` is the width in pixels where a pane starts to resist. `MaxSize` caps the width and is unlimited by default.

```xml
<arc:ResizablePanel Id="code" Label="Code" DefaultSize="50" MinSize="240" MaxSize="800" />
```

### Saving and restoring the layout

`Sizes` holds each pane's share in percent and is two-way. Store it when the user finishes a resize, and set it
again to restore the layout. `LayoutChanged` is raised after a resize settles.

```csharp
panels.LayoutChanged += (_, e) =>
{
    settings.PaneSizes = e.Sizes.ToArray();
};

panels.Sizes = settings.PaneSizes;
```

### Resetting

`Reset` returns the panes to their default shares, fitted to the current width.

```csharp
panels.Reset();
```

## API reference

<!-- api: ResizablePanels -->

<!-- api: ResizablePanel -->

## Accessibility

- Each divider is focusable. Its value is the pane's share, and it reports a hidden pane with the hint to press Enter to show it.
- The group is announced as a group named by its `Label`. A collapsed pane is hidden from assistive technology.
- Left and Right arrows resize the divider by a small step. Shift with an arrow resizes by a large step.
- Home and End move the divider to its smallest and largest position. Enter hides or restores a collapsible neighbor.
- Escape during a pointer drag returns the divider to where the drag started.

## Motion

- Dragging follows the pointer directly. Past a pane's minimum, the divider resists with a short rubber band.
- On release, the panes settle on a spring that keeps the pointer's speed. Dragging a collapsible pane well past its minimum,
  or flicking it toward the edge, collapses it.
- Keyboard resizing uses a snappy spring.
- A restore tab pops in when a pane is collapsed, and its content fades out as the pane shrinks.
- Reduced motion moves the panes straight to their target, and the restore tab only fades in.

## Related

- `Drawer` for a pane that covers the page as an overlay.
- `Tabs` for one pane at a time, or for a narrow layout.
- `ScrollArea` for content that overflows a pane.
