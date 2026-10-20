---
title: Drawer
summary: A temporary side surface for focused work.
---

## When to use

- Filters, settings or record details that should keep the page in context.
- Forms too long for a `Dialog` that should not leave the current view.
- Panels the user may dismiss by dragging the header toward the edge.

## When not to use

- Short decisions and confirmations: use `Dialog`.
- Mobile-first panels that rest at several heights: use `BottomSheet`.
- Small content anchored to a control, without a scrim: use `Popover`.

## Usage

A `Drawer` is a modal panel that slides in from one edge. Declare it anywhere and bind `IsOpen`.

### Edge

`Side` sets the edge the panel slides in from: `Left`, `Right`, `Top` or `Bottom`. The default is `Right`.

```xml
<arc:Drawer Title="Filters"
            Description="Narrow the list without leaving it."
            Side="Left"
            IsOpen="{Binding ShowFilters}">
    <StackPanel Spacing="16">
        <CheckBox Content="Only open invoices" />
        <CheckBox Content="Include drafts" />
    </StackPanel>
</arc:Drawer>
```

### Long content

The body scrolls while the header stays in place. Put the full content in the drawer; there is no need to size it.

```xml
<arc:Drawer Title="INV-2041" Description="Paid on 12 March by Ada Lovelace." IsOpen="{Binding ShowRecord}">
    <ItemsControl ItemsSource="{Binding LineItems}" />
</arc:Drawer>
```

### Actions

Put buttons in `Actions`. A button with `arc:Dialog.Result` closes the drawer with that value, the same as in a
`Dialog`.

```xml
<arc:Drawer Title="Filters" IsOpen="{Binding ShowFilters}">
    <arc:Drawer.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary" Content="Reset" arc:Dialog.Result="{x:False}" />
            <arc:Button Content="Apply" arc:Dialog.Result="{x:True}" />
        </StackPanel>
    </arc:Drawer.Actions>
</arc:Drawer>
```

### Opening from code

Set `Side` and `IsOpen` together when the same drawer serves several edges. `OpenCommand` opens the drawer from a
button without setting `IsOpen`.

```csharp
drawer.Side = DrawerSide.Bottom;
drawer.IsOpen = true;

openDetails.Command = details.OpenCommand;
```

### Closing reason

`Closed` reports why the drawer closed: `CloseButton`, `Escape`, `PressOutside`, `Gesture`, `Action` or `Programmatic`.

```csharp
drawer.Closed += (_, e) => log.Text = $"Closed by {e.Reason}";
```

## API reference

<!-- api: Drawer -->

## Accessibility

- The panel takes its name from `Title` and its help text from `Description`.
- Focus moves into the drawer when it opens, Tab and Shift+Tab stay inside it, and focus returns to the opener on close.
- Escape closes the drawer. The close button is always present and is named "Close drawer" by default.
- Dragging is optional. Under reduced motion it is off, so closing never depends on a gesture.

## Motion

- The panel slides in from its edge on a smooth spring while the scrim fades in.
- Drag the header toward the edge to follow the pointer. Past a third of the panel, or on a flick faster than 500px/s,
  the drawer closes. Otherwise it springs back.
- A click, Escape or a press on the scrim closes the drawer with a short tween, and the shadow fades out at the end.
- Reduced motion turns dragging off and replaces the slide with a short fade. Off shows or removes the drawer at once.

## Related

- `Dialog` for decisions that must interrupt the page.
- `BottomSheet` for mobile-first panels that rest at a peek height and expand.
- `Popover` for small interactive content anchored to a control.
