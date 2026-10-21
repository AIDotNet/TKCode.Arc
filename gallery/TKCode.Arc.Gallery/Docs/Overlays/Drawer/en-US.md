---
title: Drawer
summary: A temporary side surface for focused work.
---

<Demo name="Sides" />

## When to use

A `Drawer` is a modal panel that slides in from one edge of the window and keeps the page behind it visible at its
edge. It fits focused work that depends on the current view: the person filters a list and sees the result change, or
inspects one record without losing their place in the table. It closes with a button, Escape, a press on the scrim or a
drag toward the edge, so it is easy to leave and return to the same position on the page.

- **Filters on a list in an admin console**: a receivables list keeps its rows visible while the person sets status,
  date range and owner in a right-hand drawer, then applies or resets the filters.
- **Invoice or order details**: a support agent selects an invoice and reads its line items, payment history and
  notes in a drawer, then closes it and picks the next row without a page change.
- **Settings for the selected item**: a product editor opens a left drawer with the variant and pricing settings for
  the item under edit, so the catalogue stays in view.
- **Forms too long for a dialog**: a vendor onboarding form with bank details and tax fields is more than a short
  panel can hold, but the person still needs the list of vendors behind it.
- **Mobile and narrow windows**: on a narrow window a drawer nearly fills the width and keeps its header as a
  drag handle, so the person can dismiss it with a flick.

## When not to use

- **Short decisions and confirmations** such as deleting a project: use `Dialog`, which is centred and puts the
  consequence and the buttons in one panel.
- **Mobile-first panels that rest at several heights**, such as delivery options or a share sheet: use `BottomSheet`,
  which stops at a peek height and expands in steps.
- **Small content anchored to a control without a scrim**, such as a column chooser: use `Popover`.
- **A list of commands** that act on the current selection: use `DropdownMenu` or `ContextMenu`.
- **Navigation between sections of an app**: use `Tabs` for sections of one page, or a sidebar for the application's
  main areas. A drawer that holds navigation hides the destinations behind a dismissible surface.

## Usage

A `Drawer` derives from `ModalOverlay`, which itself is a `ContentControl`. Its `Content` is the scrolling body,
`Title` and `Description` form the header, `Actions` is the footer, `Side` chooses the edge and `IsOpen` shows or
hides the panel. The control takes no space where it is declared; while `IsOpen` is true, its panel slides in on the
window's overlay layer above a lightly blurred scrim. Declare it anywhere and bind `IsOpen`.

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

Choose `Right` for filters and details beside a list on a wide window, because the list stays in view on the left.
`Left` suits content the person browses before the list, such as a category tree. `Top` and `Bottom` span the full width; the
bottom drawer shows a grabber to signal that it can be dragged away. Use one edge per kind of task, so people learn
where the panel for filters or details will open.

### Anatomy

1. **Header** (`Title`, `Description`): the header is the drag handle. Its padding is 24 px (20 px when narrow), with a
   1 px border under it. The title uses the large type size at medium weight; the description uses the small type
   size in the secondary text color, 8 px under the title and at most 512 px wide.
2. **Close button** (`ShowCloseButton`, `CloseButtonName`): a 32 px button with the control radius and a 1 px border,
   aligned to the title's first line. Its fill is the muted surface color at 65% opacity. It is always present and
   is named "Close drawer" by default.
3. **Grabber**: shown only on a `Bottom` drawer. It is a 36 × 4 px pill in the border-strong color at 75% opacity,
   9 px below the top of the header.
4. **Body** (`Content`): 24 px padding (20 px when narrow). The body scrolls while the header stays in place.
5. **Actions** (`Actions`): right aligned, 24 px below the body. It is hidden when `Actions` is null.
6. **Panel**: on a `Left` or `Right` edge it is at most 480 px wide (30 rem) and never wider than the window minus a
   16 px gutter (12 px when narrow). On a `Top` or `Bottom` edge it spans the full width and is at most
   512 px tall, or the window height when that is smaller. The corners on the inner side have a 22 px radius
   (18 px when narrow), and the hairline border is drawn only on the inner edges.
7. **Scrim**: a near-black dim layer at 38% opacity, lighter than the one under a `Dialog`, with a 4 px background
   blur. The blur is turned off when the reduce-transparency setting is on.

A window narrower than 40 rem (640 px) makes the panel narrow: it takes almost the full width, and its corners and
padding tighten.

### Long content

The body scrolls while the header stays in place. Put the full content in the drawer; there is no need to size it.
A drawer taller than the window scrolls its body, so the title, the description and the close button stay in view
while the person reads through the list of line items.

<Demo name="LongContent" />

```xml
<arc:Drawer Title="INV-2041" Description="Paid on 12 March by Ada Lovelace." IsOpen="{Binding ShowRecord}">
    <ItemsControl ItemsSource="{Binding LineItems}" />
</arc:Drawer>
```

For a list of several hundred rows, page the data or use a control that virtualizes its items inside the body. The
body is an ordinary scroll area, so every item placed in it is measured and arranged.

### Actions

Put buttons in `Actions`. A button with `arc:Dialog.Result` closes the drawer with that value, the same as in a
`Dialog`. A filter drawer usually offers a secondary "Reset" that returns `false` and a primary "Apply" that returns
`true`, so the caller reads one result after the drawer closes.

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

`Closed` reports why the drawer closed through `OverlayClosedEventArgs.Reason`: `CloseButton`, `Escape`,
`PressOutside`, `Gesture`, `Action` or `Programmatic`. `Closing` is raised before a user dismissal and can be
cancelled with `e.Cancel`, for example while a form in the drawer has unsaved changes.

```csharp
bool hasUnsavedChanges = true; // set by the form when a field changes
drawer.Closing += (_, e) =>
{
    if (e.Reason is DismissReason.PressOutside or DismissReason.Gesture && hasUnsavedChanges)
    {
        e.Cancel = true;
    }
};
drawer.Closed += (_, e) => log.Text = $"Closed by {e.Reason}";
```

### Dragging to close

The header is a drag handle. A drag toward the edge follows the pointer. A drag away from the edge is resisted with a
square-root curve, so the panel moves a little and springs back. On release, the drawer closes when it has been
dragged past a third of its size, or when a flick toward the edge is faster than 500 px/s. Otherwise it springs back
with the snappy spring. A press on a button or a text box in the header is a click, not a drag, so the close button
keeps working. Dragging is disabled under reduced motion and when motion is off, so closing never depends on a gesture.

### Binding to a view model

`IsOpen` is two-way by default, so a close by Escape, the scrim or a drag writes `false` back to the view model. The
drawer's `Title` and `Description` are one-way and change while the drawer is open with a soft blur, which is useful
when the drawer shows the record that the list has just selected.

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class ReceivablesViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isDetailOpen;

    [ObservableProperty]
    private Invoice? _selectedInvoice;

    [RelayCommand]
    private void OpenInvoice(Invoice invoice)
    {
        SelectedInvoice = invoice;
        IsDetailOpen = true;
    }

    [RelayCommand]
    private async Task MarkPaidAsync()
    {
        await _invoices.MarkPaidAsync(SelectedInvoice!.Id);
        IsDetailOpen = false;
    }
}
```

```xml
<arc:Drawer Title="{Binding SelectedInvoice.Number}"
            Description="{Binding SelectedInvoice.Summary}"
            Side="Right"
            IsOpen="{Binding IsDetailOpen}">
    <ItemsControl ItemsSource="{Binding SelectedInvoice.LineItems}" />
    <arc:Drawer.Actions>
        <arc:Button Content="Mark as paid" Command="{Binding MarkPaidCommand}" />
    </arc:Drawer.Actions>
</arc:Drawer>
```

### Example: invoice details from the receivables list

A finance team reviews overdue invoices in a table on a wide window. Selecting a row opens a right drawer that shows
the invoice number as its title, the customer and due date as the description, and the line items in the body. The
drawer does not cover the table: the person can compare the next invoice with the previous one by closing the drawer
and selecting another row. The actions are "Send reminder" in the secondary variant and "Mark as paid" in the primary
variant, and the view model closes the drawer after the payment is recorded, so a failed call leaves it open with
the invoice visible.

```xml
<arc:Drawer Title="INV-2041"
            Description="Northwind Traders, due 30 September 2026, 21 days overdue."
            Side="Right"
            IsOpen="{Binding IsDetailOpen}">
    <StackPanel Spacing="12">
        <TextBlock Text="Design review, 12 hours at 180 EUR" />
        <TextBlock Text="Prototype build, 20 hours at 150 EUR" />
        <TextBlock Text="Travel and accommodation, 1 item at 640 EUR" />
        <TextBlock Text="Total due: EUR 5,800.00"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
    </StackPanel>
    <arc:Drawer.Actions>
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Button Variant="Secondary" Content="Send reminder" />
            <arc:Button Content="Mark as paid" Command="{Binding MarkPaidCommand}" />
        </StackPanel>
    </arc:Drawer.Actions>
</arc:Drawer>
```

### Content guidelines

The header names the object or the task: "Filters" for a filter drawer, the invoice number for a record. The
description explains the scope in one sentence, and its numbers are digits. Action buttons use a verb and an object,
and a filter drawer uses "Apply filters" rather than "OK".

| Instead of | Write |
|---|---|
| Options | Filters |
| INV2041 details | INV-2041 |
| Submit | Send reminder |
| OK | Apply filters |
| Click the X | Close drawer |
| Details (13 items) | Line items (13) |

Keep the title stable while the drawer is open. A title that changes on every interaction makes the header flicker
through blurred copy, so change it only when the selected object changes.

### Best practices

**Do**

- Keep the list visible behind the drawer when the drawer is about the list. The context lets the person compare the
  rows and see the effect of a filter immediately.
- Choose the edge by the task: `Right` for details and filters on wide windows, `Bottom` for a short panel on a narrow
  window with a grabber to drag away.
- Give the drawer a `Title`. It is the panel's accessible name, and the header is the drag handle, so an empty header
  is hard to recognize.
- Keep the close button and the Escape key working; the drag is an extra way to leave, not the only one.

**Avoid**

- Putting a short yes or no decision in a drawer. A `Dialog` centres the question, and a drawer hides the consequence
  at the edge of the window.
- Nesting a drawer inside a drawer. The stack of scrims makes the page hard to read, and only the topmost panel
  answers Escape.
- Navigation in a drawer that people must reach often. The drawer closes on a press on the scrim, so frequent
  destinations belong in a persistent sidebar.
- Long text in the header. Keep titles to a few words and descriptions to one sentence; the description stops at 512 px.
- Placing a horizontal scroll inside the body. The body scrolls vertically; wide tables should be narrowed or
  restructured to fit the panel.

### Common questions

**Why did my drawer close when I released the header?** The release was past a third of the panel or a flick faster
than 500 px/s toward the edge. Handle `Closing` with the `Gesture` reason and cancel it when the form has unsaved
changes.

**How do I open the drawer from a button without code-behind?** Bind the button's command to the drawer's
`OpenCommand` through its name, for example `Command="{Binding #RecordDrawer.OpenCommand}"`, or bind `IsOpen` in the
view model.

**Why is my drawer's body not scrolling?** The body scrolls when its content is taller than the panel. Remove a nested
`ScrollViewer` inside the body: the body already scrolls its whole content, and a second scroll area splits the scroll.

**Does a drawer on the bottom edge show a grabber?** Yes. The grabber is part of the bottom panel only; top, left and right drawers do not show it, because their drag direction is clear from the edge.

**How do I keep the drawer open after a failed save?** Cancel the close in `Closing` or do not set `IsOpen` to `false`
until the save succeeds. The drawer stays open with its content, so the error can appear next to the field.

## API reference

<!-- api: Drawer -->

The side of a drawer is the companion enum `DrawerSide`, with the members `Left`, `Top`, `Right` and `Bottom`. The
default is `Right`.

## Accessibility

- The panel takes its name from `Title` and its help text from `Description`.
- Focus moves into the drawer when it opens, Tab and Shift+Tab stay inside it, and focus returns to the opener on close.
- Escape closes the drawer when `CloseOnEscape` is true. The close button is always present and is named "Close drawer"
  by default; change it with `CloseButtonName`.
- Dragging is optional. Under reduced motion it is off, so closing never depends on a gesture.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move to the next or previous control inside the drawer, wrapping at the ends |
| `Enter` or `Space` on a button | Press the focused button; a button with `arc:Dialog.Result` closes the drawer with that result |
| `Escape` | Close with reason `Escape`, unless `CloseOnEscape` is false or `Closing` cancels it |

The drawer does not move by keyboard. Dragging the header is a pointer or touch action only, so the close button and
Escape are the keyboard paths out.

## Motion

- The panel slides in from its edge on a smooth spring while the scrim fades in.
- Drag the header toward the edge to follow the pointer. Past a third of the panel, or on a flick faster than 500px/s,
  the drawer closes. Otherwise it springs back.
- A click, Escape or a press on the scrim closes the drawer with a short tween, and the shadow fades out over the last
  part of the travel. A flick closes it on a spring that keeps the release velocity.
- Reduced motion turns dragging off and replaces the slide with a short fade. Off shows or removes the drawer at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Opening | Panel slides in from its edge on the smooth spring; the scrim fades in | Short fade | Immediate |
| Closing by button, Escape or scrim | Standard tween out; the shadow fades in the last part of the travel | Short fade | Immediate |
| Closing by flick | Spring that keeps the release velocity | Short fade | Immediate |
| Header drag | Follows the pointer toward the edge; resists the other way | Drag disabled | Drag disabled |
| Title or description changes | The new copy rises in with a soft blur | Instant fade | Immediate |

## Related

- `Dialog` for decisions that must interrupt the page.
- `BottomSheet` for mobile-first panels that rest at a peek height and expand.
- `Popover` for small interactive content anchored to a control.

| Component | Choose it when |
|---|---|
| `Dialog` | The question is short and must be answered before anything else |
| `BottomSheet` | The task is mobile-first and rests at a peek height before it expands |
| `Popover` | The content is small, anchored to one control and does not need a scrim |
| `Tabs` | Sections of one page switch in place and no panel should cover the page |
| `ResizablePanels` | The list and the detail are both permanent parts of the page and the person resizes them |
| `ExpandableCard` | A record expands inside the page and the rows around it should move |
