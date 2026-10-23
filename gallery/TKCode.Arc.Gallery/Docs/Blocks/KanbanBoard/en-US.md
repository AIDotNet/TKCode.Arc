---
title: Kanban board
summary: A team board whose cards lift, tilt with the pointer and spring into the slot that opens for them, with swimlanes by person, live work-in-progress limits, quick add and a details drawer.
---

<Demo name="Preview" />

## When to use

`KanbanBoard` is a complete board for work that moves through a fixed set of stages and whose order inside a stage
matters. Cards are picked up with the mouse, a pen, a finger (after a short hold) or the keyboard; while a card is lifted
a slot opens where it will land, the cards around it make room, and the column counts already show where the card would
end up. Columns can carry a limit that turns the column amber when it is exceeded.

- **A team's issue or task board.** Load the columns, the people and the cards from your tracker, and save the ordered
  list that `CardsChanged` hands over after every move or edit.
- **Pipelines with a manual order.** A sales pipeline, a hiring funnel or a content calendar where the card at the top of
  a stage is the next one to handle.
- **Boards that need a view per person.** `GroupBy="Assignee"` gives every person a swimlane with the same columns;
  dropping a card in another lane hands it over.

## When not to use

- **Work planned on dates.** Use a week calendar or a timeline when the question is *when*, not *in which stage*.
- **Read-only status summaries.** A table or a list with a status column is easier to scan and to sort; set
  `IsReadOnly` only for a shared view of a board people also edit.
- **Hundreds of cards in one column.** Every card is a live element so that it can glide; the board is comfortable up to a
  few hundred cards in total. Page or filter large backlogs, or use `DataGrid`.
- **Free-form arrangements.** Cards always snap into ordered columns; use a canvas for sticky-note style layouts.

## Usage

### Columns, people, labels and cards

The board takes four lists. `Columns` are the stages in order; give a column a `Limit` to warn when it holds more cards.
`People` are `ProjectPerson` objects: they draw the avatars, the swimlanes and the owner menu (without a picture they get
generated initials). `Labels` give cards a colored dot and a name. `Cards` is one flat list: the order of the list is the
order inside every column, which is also how you persist it.

A card refers to its column, owner and label by key (`ColumnKey`, `AssigneeKey`, `LabelKey`), so it can come straight
from a tracker's JSON. A card whose column is not on the board is not shown; in swimlanes, cards whose owner is not among
the people get a trailing "Unassigned" lane.

```xml
<arc:KanbanBoard Title="Sprint 18" Height="640">
    <arc:KanbanBoard.Columns>
        <arc:KanbanColumn Key="todo" Name="To do" />
        <arc:KanbanColumn Key="doing" Name="Doing" Limit="3" />
        <arc:KanbanColumn Key="done" Name="Done" />
    </arc:KanbanBoard.Columns>
    <arc:KanbanBoard.People>
        <arc:ProjectPerson Key="ines" Name="Ines Duarte" />
        <arc:ProjectPerson Key="tom" Name="Tomasz Bielski" />
    </arc:KanbanBoard.People>
    <arc:KanbanCard Key="SP-41" Title="Export the audit log as CSV" ColumnKey="todo" AssigneeKey="ines" Points="3" />
    <arc:KanbanCard Key="SP-42" Title="Rate limit the sign-in form" ColumnKey="doing" AssigneeKey="tom" Points="2" />
</arc:KanbanBoard>
```

Bind `CardsSource` to a collection your view model owns. A writable list (an `ObservableCollection` or an
`AvaloniaList`) is reordered in place on every drop and every new card, so it always matches the board; a read-only
collection is left alone and the board keeps its own order. Changes made from outside, such as a new list or a card whose
`ColumnKey` changed, are applied together and the cards glide to their new places.

### Saving changes

`CardsChanged` fires after every move, edit, checklist change and new card. Its `Reason` says what happened and `Cards` is
a snapshot of the whole list in board order: store it as it is. `CardMoved` adds the details of a move: the columns and
owners before and after, the card's position in the list before and after, and `ChangeSource` (pointer, touch, keyboard,
the drawer, or a method call).

```csharp
var saved = new List<(string Key, string Column, string? Owner)>();
board.CardsChanged += (_, e) =>
{
    // Store the snapshot as it is: its order is the order inside every column.
    saved = e.Cards.Select(c => (c.Key, c.ColumnKey, c.AssigneeKey)).ToList();
};
board.CardMoved += (_, e) => Debug.WriteLine($"{e.Card.Key}: {e.FromColumn?.Name} → {e.ToColumn?.Name}");
```

<Demo name="Controlled" />

### Swimlanes

`GroupBy` switches between one row of columns and one swimlane per person; the switch in the toolbar does the same, and
every card flies to its new place. Lane heads stay at the left edge while the board scrolls sideways; the column heads stay
at the top while it scrolls down. Column counts and limits always count every lane; each lane head counts its own cards.
Dropping a card in another person's lane changes its `AssigneeKey`.

<Demo name="Swimlanes" />

### Work-in-progress limits

A column is over its limit only when it holds more cards than `Limit` (a full column is fine). Its count turns amber, an
"Above limit" chip appears and its cells take a warm tint; the slot of a card about to be dropped there says so too. The
counts follow a dragged card live, so the warning shows before the drop. The board only warns: handle `CardMoving` and
set `Cancel` to send the card back when the limit must hold. `ColumnLimitStateChanged` reports a column going over or
coming back.

```csharp
board.CardMoving += (_, e) => e.Cancel = e.IsTargetOverLimit && e.From.ColumnKey != e.To.ColumnKey;
```

<Demo name="WipLimits" />

### Quick add and the details drawer

The "+" in every column head and the Add card row under every column open a composer at the end of the column (in
swimlanes, in the lane of `CurrentUser`, by default the first person). Enter adds the card and keeps the composer open for
the next one, Shift+Enter breaks the line and Escape closes it. New cards get the next number after the highest id on the
board ("FLD-244" → "FLD-245"); set `CardKeyFactory` to make your own ids, and handle `CardCreating` to change or refuse a
card before it is added.

Clicking a card, or Enter on a focused card, raises `CardInvoked` and opens the details drawer: the title, the status, the
owner, the label, the due date, the estimate, notes and a checklist. Edits apply to the card as they are typed. Changing
the status keeps the card's place in the list, so it lands on top of a later column or at the bottom of an earlier one. Set
`OpenedCard` (or call `OpenCard`) to open the drawer from code, and set `Handled` in `CardInvoked` or turn off
`IsDetailsEnabled` to show your own view instead.

### Narrow containers and read-only boards

The board reads its own width. Columns share the width down to `MinColumnWidth` (251) and the board then scrolls sideways.
At `CompactWidth` (560) and below, columns are `CompactColumnWidth` (235) wide, the paddings tighten, and the board snaps
to the nearest column after a sideways scroll, with the first column flush against the left edge. The drawer is never wider
than the board minus 16, so it covers a narrow board, and its status options wrap two by two.

<Demo name="Narrow" />

`IsReadOnly` keeps the cards where they are: no dragging, no quick add and no edits, while the drawer still opens to show a
card. `IsDragEnabled` and `IsQuickAddEnabled` turn the two features off one at a time.

<Demo name="ReadOnly" />

### Texts

Every word the board shows or announces is in `Strings`, a `KanbanBoardLabels` object with English defaults. Texts with
values are functions, so the word order can change in a translation.

```csharp
board.Strings = new KanbanBoardLabels
{
    StatusView = "Statut",
    LanesView = "Par personne",
    AddCard = "Ajouter une carte",
    CardsWord = n => n == 1 ? "carte" : "cartes",
    Dropped = (title, column) => $"{title} déposée dans {column}."
};
```

## API reference

<!-- api: KanbanBoard -->

<!-- api: KanbanColumn -->

<!-- api: KanbanCard -->

<!-- api: KanbanLabel -->

<!-- api: KanbanChecklistItem -->

<!-- api: KanbanBoardLabels -->

<!-- api: KanbanCardsChangedEventArgs -->

<!-- api: KanbanCardMovedEventArgs -->

## Accessibility

- **Keyboard.** Tab reaches the view switch, then every "+", then each column's cards from top to bottom followed by its
  Add card row. Enter on a card opens its details. Space lifts it: Up and Down move it inside its column, Left and Right to
  the neighbouring column at the same position, Shift with Up or Down to the neighbouring swimlane; Space or Enter places
  it and Escape puts it back. Moving the focus away from a lifted card also puts it back.
- Every pick up, move and drop is announced at once ("Signature pad on the handover form lifted. Building, place 1."),
  including the person when a card changes lanes. New cards, status changes from the drawer and columns going over their
  limit are announced politely.
- Cards are buttons named "title, id, owner", described by their label, due date, checklist progress and estimate.
- Limits are shown in words as well as color: the count reads "4 of 3 allowed" and the chip and the slot say the limit is
  exceeded.
- The drawer is modal inside the board: focus moves to its close button, Tab stays inside it, and Escape, the close
  button or a click on the dimmed board closes it and returns the focus to the card. Escape in the owner menu closes only
  the menu. The text fields show a focus ring. Closing the composer returns the focus to the button that opened it.
- The view switch and the drawer's status are radio groups with one tab stop; the arrow keys change the choice.

## Motion

- **Lifting**: a card lifts after the pointer has moved more than `DragThreshold` (5px), or after a touch has rested for
  `TouchHoldDelay` (250ms); a quicker swipe scrolls the board. The card follows the pointer from the spot it was grabbed,
  grows to 1.035 and tilts with the horizontal speed (0.0064° per px/s, at most 7°) on a quick spring; when the pointer
  rests the tilt settles back to level. It is drawn above the whole board, so it can ride over the column heads and the
  title without being cut off.
- **Making room**: the slot appears where the card was and fades in; the cards around it, and the slot itself, glide on
  the layout spring (critically damped, about 0.6s) and keep their speed when the target changes again.
- **Dropping**: the card rides a small spring (stiffness 690, damping 51) into its slot, starting with the speed it was
  thrown at, and follows the slot if it is still gliding; its shadow and border ease back over 240 and 160ms. Escape flies it
  home the same way.
- **Edges**: near an edge of the board (64px) it scrolls on both axes, faster the closer the pointer, up to 1200px/s, and
  the slot keeps up with the content moving under a resting pointer.
- **Switching views, keyboard moves and drawer changes** move every affected card on the layout spring. The drawer slides
  in on the same spring while the board dims over 240ms; the composer grows and shrinks with it. Counts roll.
- Reduced motion keeps the slot, the counts and short fades but drops the tilt and every flight: cards land at once. Off
  applies every change at once.

## Related

- `ReorderableList` for reordering a single list.
- `ProjectPerson` and the other Projects blocks for the people on the board.
- `SegmentedControl`, `DropdownMenu` and `RollingDigits`, the pieces the board's toolbar, owner menu and counts follow.
