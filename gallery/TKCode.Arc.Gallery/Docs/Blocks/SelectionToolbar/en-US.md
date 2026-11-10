---
title: Selection toolbar
summary: A floating bar that rises over a list or table while items are selected, with the count, select all, bulk actions and a way to clear the selection.
---

<Demo name="Preview" />

## When to use

`SelectionToolbar` wraps a list or a table and adds a floating bar for whatever is selected in it. Put the list in
`Content` and keep `SelectedCount` in step with its selection. While anything is selected, a pill rises from the
bottom edge with the count, "Select all" for the rest, the bulk actions and a clear button; it sinks away when the
selection empties. Destructive actions are drawn in the danger color and placed last.

- **File lists and tables** where people check several rows and archive, move or delete them at once.
- **Inboxes and ticket queues** where many items get the same label, owner or status.
- **Admin tables** where a selection leads to an export or a bulk change.

## When not to use

- **One action on one item**: put the action on the row, or in a `ContextMenu`.
- **Actions that do not depend on a selection**: use a toolbar in the page header (`PageHeader`).
- **A long list of actions**: keep the bar to four or five; move the rest into a menu on the page.

## Usage

```csharp
toolbar.TotalCount = files.Count;
list.SelectionChanged += (_, _) => toolbar.SelectedCount = list.SelectedItems.Count;
toolbar.SelectAllRequested += (_, _) => list.SelectAll();
toolbar.Cleared += (_, _) => list.UnselectAll();
toolbar.ActionInvoked += (_, e) => Run(e.Action.Key, e.SelectedCount);
```

The clear button and Escape set `SelectedCount` to 0 and raise `Cleared`; deselect your rows there. Each action can
also carry a `Command`, which receives the selected count. Without `Actions` the bar offers Archive, Move, Share and
Delete.

<Demo name="CustomActions" />

## API reference

<!-- api: SelectionToolbar -->

## Accessibility

- The bar is a toolbar named "Bulk actions". Tab reaches it; Left and Right move between its buttons and Home and End
  jump to the ends.
- Escape anywhere inside the control clears the selection; focus returns to the list when it was on the bar.
- The count is read out politely whenever it changes. The clear button is named "Clear selection".

## Motion

- The bar rises 16 px while fading in and sinks while fading out. The count rolls up when it grows and down when it
  shrinks.
- Reduced motion keeps only the fades; Off shows and hides the bar at once.

## Responsive behavior

- The bar is centered at the bottom with 16 px on each side and 18 px below. Leave room under the last row (padding
  at the bottom of the list) so the bar does not cover it.
- Below `CompactWidth` (560 px) the actions keep only their icons, with their labels as tooltips.

## Related

- `FloatingButtonGroup` for a floating tray of tools that does not depend on a selection.
- `Checkbox` for the row selection and `DataGrid` for large tables.
- `InboxSidebar`, whose mail lists often use this bar.
