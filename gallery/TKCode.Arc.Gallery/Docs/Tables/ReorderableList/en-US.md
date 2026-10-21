---
title: Reorderable list
summary: Organize items with direct drag and drop.
---

<Demo name="Priorities" />

## When to use

- A short list whose order the user decides, such as priorities or a playlist.
- Lists where dragging and the keyboard must both work for the same job.
- Each row keeps its own text. Only the grip starts a drag, so the text stays selectable.

## When not to use

- The order comes from a column the user can sort: use `SortableDataTable`.
- Rows need swipe actions rather than reordering: use `SwipeActions`.
- Long lists. Rows are not virtualized, so keep the list short or split it.

## Usage

Each row shows a grip. Dragging the grip moves the row. The list reports the new order in `Reordered`.

```xml
<arc:ReorderableList Label="Priorities"
                     Width="520"
                     ItemsSource="{Binding Priorities}" />
```

`ItemsSource` takes a `ReorderableListEntry` list with an `Id`, a `Title` and an optional `Detail`.

```csharp
var priorities = new ObservableCollection<ReorderableListEntry>
{
    new("pricing", "Ship pricing page", "Marketing - due Friday"),
    new("onboarding", "Review onboarding flow", "Design - 3 open comments"),
    new("billing", "Migrate billing webhooks", "Platform")
};
var list = new ReorderableList { ItemsSource = priorities };
```

### Reacting to a move

`Reordered` gives the item, its old index and its new index. When `ItemsSource` is an `ObservableCollection`
and `MovesItems` is `True` (the default), the list moves the item in the collection itself. Set `MovesItems`
to `False` to keep the collection unchanged and save the order yourself.

```csharp
list.Reordered += (_, e) => status.Text =
    $"Moved {((ReorderableListEntry)e.Item!).Title} from {e.OldIndex + 1} to {e.NewIndex + 1}";

list.MoveItem(0, 2);   // move the first row to the third place
```

### Custom item template

`ItemTemplate` replaces the default title and detail. `ItemTitleSelector` gives the title used in the
accessible name and in announcements.

<Demo name="CustomItemTemplate" />

```csharp
var colors = new ReorderableList
{
    ItemsSource = new ObservableCollection<string> { "Coral", "Saffron", "Mint" },
    ItemTemplate = new FuncDataTemplate<string>((name, _) => new TextBlock { Text = name }),
    ItemTitleSelector = item => item.ToString() ?? string.Empty
};
```

### Length and scrolling

Put the list in a `ScrollViewer` when it is taller than the space you give it. Dragging a row to the edge of
the scrolling area scrolls it.

<Demo name="AutoScroll" />

```xml
<ScrollViewer Height="340">
    <arc:ReorderableList ItemsSource="{Binding Tracks}" />
</ScrollViewer>
```

## API reference

<!-- api: ReorderableList -->

<!-- api: ReorderableListEntry -->

## Accessibility

- Each grip is a button. Its name gives the item title and its position, and the name updates after each move.
- Focus a grip and press Up or Down to move its row one place. Focus stays on the moved row's grip.
- Space picks a row up, arrow keys move it, Space drops it and Escape cancels the move.
- Only the grip is focusable for dragging. The row text is a normal selectable text area.
- Set `Label` to name the list for assistive technology. The default is "Reorder items".

## Motion

- A row lifts slightly while its grip is pressed. It settles back with a short spring when released.
- While dragging, the row follows the pointer and the rows it passes slide aside on springs.
- The position number rolls in the direction the row travelled.
- Reduced motion keeps the pointer follow, but the row lands at once and does not lift. Off behaves like Reduced.

## Related

- `SortableDataTable` when the order comes from column values, not from a person's choice.
- `SwipeActions` for quick actions on a row without reordering it.
