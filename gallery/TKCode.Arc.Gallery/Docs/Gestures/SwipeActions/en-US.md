---
title: Swipe actions
summary: Reveal row actions with a swipe, or from the same actions in a menu.
---

<Demo name="Inbox" />

## When to use

- A touch first list of triage items, such as mail, notifications or tasks.
- One or two quick actions on each side of a row, such as Archive or Mark unread.
- Rows that can be removed with a full swipe, where the list closes the gap afterwards.

## When not to use

- A desktop table or a list used mainly with a mouse: use `ContextMenu` or `DropdownMenu`.
- Reordering rows by dragging them up and down: use `ReorderableList`.
- One dangerous action outside a list: use `HoldToConfirm`.

## Usage

`SwipeActions` is the list. Each item is a `SwipeActionsRow`, or a data item shown through `ItemTemplate`.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SwipeActions Label="Inbox" ItemsSource="{Binding Messages}">
        <arc:SwipeActions.LeadingActions>
            <arc:SwipeAction Label="Unread" Tone="Accent" KeepsRow="True" />
        </arc:SwipeActions.LeadingActions>
        <arc:SwipeActions.TrailingActions>
            <arc:SwipeAction Label="Archive" />
            <arc:SwipeAction Label="Delete" Tone="Danger" />
        </arc:SwipeActions.TrailingActions>
        <arc:SwipeActions.ItemTemplate>
            <DataTemplate>
                <TextBlock Text="{Binding Subject}" />
            </DataTemplate>
        </arc:SwipeActions.ItemTemplate>
    </arc:SwipeActions>
</UserControl>
```

Leading and trailing actions are shared by every row. The outermost action of a side is the one that a full swipe
runs.

### Actions that run

`Command` runs when the action is chosen, by a tap, a full swipe or the More actions menu. `Invoked` and
`ActionInvoked` tell you which row and item it was. `KeepsRow="True"` runs the action and returns the row to its
place instead of removing it, which suits toggles such as Unread.

```csharp
inbox.ActionInvoked += (_, e) =>
{
    if (!e.Action.KeepsRow)
    {
        messages.Remove((Message)e.Item!);
    }
};
```

Remove the data item in the handler. The row slides out first and the list closes the gap afterwards, so the
removal does not jump.

### Rows with their own actions

A `SwipeActionsRow` declares its own actions. `IsFullSwipeEnabled="False"` keeps the row from committing on a full
swipe, so the user must tap the revealed action.

<Demo name="OwnActions" />

### Resting open

`Open` shows a row's actions without a gesture. Only one row of a list is open at a time.

<Demo name="RestingOpen" />

`Close` puts an open row away, and `SwipeActions.CloseRows()` closes the open row of the list.

## API reference

<!-- api: SwipeActions -->

<!-- api: SwipeAction -->

<!-- api: SwipeActionsRow -->

## Accessibility

- The list has a name from `Label`, and each row is a list item. Each row has a More actions button named "More actions for" the row's `Label`.
- Mouse and keyboard users never need the gesture: Tab to the More actions button, press Enter or Space to open the menu, and move with the arrow keys.
- Escape closes the menu or puts away an open row. Pressing elsewhere also puts an open row away.
- The revealed actions are hidden from assistive technology and are not in the Tab order.
- When a row is removed from its menu, focus moves to the neighbouring row.

## Motion

- The row follows the pointer. Past its last stop it resists, so the row stretches a little and then returns.
- On release the row settles on the nearest stop with a spring that keeps the release speed. A quick flick
  counts as a swipe to the stop in its direction.
- A full swipe stretches the outermost action across the row, and the row slides away. The list then closes the gap.
- Reduced motion jumps the row to its stop and fades rows in and out without a height animation. Off motion moves
  everything at once.

## Related

- `ContextMenu` and `DropdownMenu` for row actions on desktop.
- `ReorderableList` for dragging rows into a new order.
- `HoldToConfirm` for a single destructive action outside a list.
