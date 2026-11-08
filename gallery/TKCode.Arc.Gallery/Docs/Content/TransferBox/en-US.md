---
title: Transfer box
summary: Move items between two lists, one by one, as a selection or all at once, with select all, search and the bound collections edited in place.
---

<Demo name="Basic" />

## When to use

`TransferBox` puts two lists side by side, the items that are available and the items that are chosen, with buttons
between them that move a selection from one list to the other. Both lists stay visible, so people see what they have
picked and what is left, and they can pick many items at once with the usual list selection keys. Moving edits the two
bound collections themselves, so the view model always holds the current result.

- **Role and permission assignment**: an admin console grants permissions to a role. The available permissions sit on
  the left, the granted ones on the right, and a row template shows a description under each name.
- **Project and team membership**: a project settings page assigns people to a project. With search on, a manager finds
  "Grace" in a team of two hundred without scrolling.
- **Visible columns of a table**: a "Customize columns" dialog moves columns between Hidden and Visible. The visible
  collection is the one the table reads, so a move takes effect as soon as the dialog applies it.
- **Resource grouping**: an operations tool moves servers or regions into a deployment group, often many at once with
  Shift or Ctrl+A.
- **Settings that pick a subset of a known list**: notification channels, synced folders or enabled plug-ins, where the
  full list is long but finite.

## When not to use

- **A few options that fit on one line**: use `ChipGroup`, which shows every option as a chip and needs no second list.
- **A subset chosen inside a form field**: use `MultiSelect`, which keeps the choice in a compact field and opens a
  list only when needed.
- **Yes or no answers that each carry their own label and validation**: use `Checkbox` rows.
- **The order of the chosen items matters**: use `ReorderableList` for the chosen items, because the transfer box adds
  moved items at the end and does not reorder them.
- **A hierarchy, such as folders or departments**: use `TreeView` with check boxes; the transfer box lists flat items.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TransferBox SourceHeader="Available languages"
                     TargetHeader="Selected languages"
                     SourceItems="{Binding AvailableLanguages}"
                     TargetItems="{Binding SelectedLanguages}"
                     Height="340" />
</UserControl>
```

`SourceItems` is the left list and `TargetItems` the right one. Select rows in a list and press the arrow button that
points to the other list, press Enter, or double-click a row. The selected items leave one collection and are added to
the end of the other, in the order they had in the list they left. The same object references move; nothing is
copied.

### Anatomy

1. **Panels**: two surfaces with a 1 px border, the control radius (`RadiusControl`, 18 px) and the resting shadow.
   Each panel is at least 180 px wide (`PanelMinWidth`); both share the width that is left.
2. **Header**: a select-all box, the title (`SourceHeader`, `TargetHeader`, medium weight) and the count. The count
   reads the number of rows, or "selected/rows" while a selection exists.
3. **Search field** (`IsSearchable`): a small `SearchField` under the header that filters the rows of its list.
4. **Rows**: 36 px tall at least, with a 12 px corner and 6 px of inset from the panel edge. Hover fills the row with
   the muted surface; a selected row takes the subtle accent fill and a check in the strong accent color, so the state
   never depends on color alone.
5. **Empty state**: centered muted text when a list has no rows (`SourceEmptyContent`, `TargetEmptyContent`), or
   `NoMatchesText` when the search text hides every row.
6. **Move buttons**: square secondary buttons, 36 px, between the panels. "Move all" buttons appear above and below
   them with `IsMoveAllVisible`. A button is disabled while it has nothing to move.

### Selecting and moving

Each list selects like a list box: a click selects a row, Ctrl+click adds or removes a row, Shift+click selects a
range and Ctrl+A selects the whole list. The select-all box in the header selects every row, shows a dash while only
some rows are selected, and clears the selection when all rows were selected. Set `SelectionMode="Single"` when only
one item should move at a time; the select-all boxes are then hidden.

```xml
<arc:TransferBox SourceHeader="Hidden columns"
                 TargetHeader="Visible columns"
                 SelectionMode="Single"
                 IsSelectAllVisible="False" />
```

Rows that stay keep their selection when other rows move, and the list keeps its scroll position.

### Binding to a view model

The box edits the bound collections, so the view model owns the data and reads the result at any time. Use an
`ObservableCollection<T>` (or any modifiable `IList` that reports its changes) for each side. Changes the view model
makes, such as loading items or removing one, show in the lists at once.

```csharp
public partial class LanguagesViewModel : ObservableObject
{
    public ObservableCollection<string> AvailableLanguages { get; } =
        ["C#", "C++", "Java", "JavaScript", "Python", "Rust"];

    public ObservableCollection<string> SelectedLanguages { get; } = ["Go", "TypeScript"];

    [RelayCommand]
    private Task SaveAsync() => _settings.SaveLanguagesAsync(SelectedLanguages.ToArray());
}
```

Rules for the collections:

- Both must be set, modifiable (not read-only, not fixed-size) and different objects. Otherwise the move buttons are
  disabled, the commands cannot execute, and a direct call to `MoveToTarget` throws `InvalidOperationException` with
  the reason.
- A move removes items by position, last first, so equal values elsewhere in the list (two "A" strings, or records that
  compare equal) are never taken by mistake.
- If the target refuses an item, for example an `int` added to a collection of strings, the box takes back what it
  added and puts every item back where it was before the exception is rethrown. A move never leaves an item in neither
  list.
- A `List<T>` without change notifications works too: the box refreshes both lists after its own moves, but it cannot
  see changes made to such a list elsewhere.

### Objects, text and templates

Items can be any objects. `ItemTextSelector` gives each item its text: the default row shows it, the search filter
matches it, and screen readers read it. It is a delegate, so it works without reflection and under trimming.

```csharp
members.ItemTextSelector = item => ((TeamMember)item!).Name;
```

For richer rows set `ItemTemplate`; both lists use it. Declare `x:DataType` on the template so its bindings are
compiled, and keep `ItemTextSelector` set for search and accessibility.

<Demo name="ItemTemplate" />

### Search and moving everything

`IsSearchable` adds a search field to each list. The text is matched anywhere in the item text, ignoring case, and is
two-way bindable through `SourceSearchText` and `TargetSearchText`. `IsMoveAllVisible` adds the "move all" buttons;
they move every row the list shows, so with a search they move only the matching rows.

<Demo name="Search" />

```xml
<arc:TransferBox SourceHeader="Team"
                 TargetHeader="Project members"
                 IsSearchable="True"
                 IsMoveAllVisible="True"
                 SearchPlaceholder="Search people"
                 SourceSearchText="{Binding MemberQuery}"
                 TargetEmptyContent="No one is on the project yet" />
```

### Commands, methods and events

The box exposes its moves as commands, so buttons, menu items or key bindings of your own can drive it. Their state
follows the selection and the collections, so a bound button enables and disables itself.

| Command | Method | Moves |
|---|---|---|
| `MoveToTargetCommand` | `MoveToTarget()` | The selected rows of the left list |
| `MoveToSourceCommand` | `MoveToSource()` | The selected rows of the right list |
| `MoveAllToTargetCommand` | `MoveAllToTarget()` | Every row the left list shows |
| `MoveAllToSourceCommand` | `MoveAllToSource()` | Every row the right list shows |

The methods return the number of items moved. `GetSelectedItems`, `SelectAll` and `ClearSelection` read and change the
selection of one side, and `SourceSelectionCount`, `TargetSelectionCount`, `CanMoveToTarget` and `CanMoveToSource` can
be bound. `ItemsTransferred` is raised once per move with the `Direction` and the moved `Items`, however many there
were.

<Demo name="Commands" />

```csharp
box.ItemsTransferred += (_, e) =>
{
    var verb = e.Direction == TransferDirection.ToTarget ? "Granted" : "Revoked";
    _audit.Write($"{verb}: {string.Join(", ", e.Items)}");
};
```

### Data shape and performance

Each list is a virtualizing list box, so only the rows in view are built and a list of thousands of items scrolls
smoothly. A search walks the whole collection once per change of the text. Moves edit the collections in place and the
lists update row by row; they do not rebuild.

### Content guidelines

- **Titles**: name both sides by what they hold, as nouns in sentence case: "Available permissions" and "Granted to
  editors", not "Left" and "Right" or "Source" and "Target".
- **Empty states**: say what an empty list means for the task, such as "No one is on the project yet", rather than only
  "No data".
- **Search placeholder**: name what is searched, such as "Search people".
- **Labels of your own buttons**: a verb and an object, such as "Show column" or "Grant permission".

| Instead of | Write |
|---|---|
| Source | Available permissions |
| Target | Granted to editors |
| No data | No one is on the project yet |
| Search... | Search people |
| Move | Grant permission |

### Best practices

**Do**

- Bind both sides to collections the view model owns, and save from the view model, not from the control.
- Set `ItemTextSelector` for object items, even with an `ItemTemplate`, so search and screen readers have text.
- Turn on search when a list can hold more items than fit in the panel.
- Give the box a height, or let a parent give it one; the lists scroll inside it.

**Avoid**

- Read-only collections or arrays: the box cannot move items in them, and the buttons stay disabled.
- Binding both sides to the same collection.
- A transfer box for two or three options; a `ChipGroup` or check boxes show them with less effort.
- Relying on the order of the chosen items; moved items are added at the end.

### Common questions

**Why are the move buttons disabled?** A button needs a selection in its list (or rows, for "move all"), the box must be
enabled, and both collections must be set, modifiable and different. Check `CanMoveToTarget` and `CanMoveToSource`.

**Can I insert moved items in sorted order?** The box adds them at the end. Handle `ItemsTransferred` and sort the
collection in the view model if the order matters.

**Does it support `DisplayMemberPath`?** No. String paths need reflection; use `ItemTextSelector` for text and an
`ItemTemplate` with `x:DataType` for compiled bindings.

**Can a move be repeated while the button is held?** No. Each press moves the current selection once; use Ctrl+A or the
"move all" buttons for many items.

## API reference

<!-- api: TransferBox -->

## Accessibility

- Each list is a list box named by its header text (`SourceHeader`, `TargetHeader`), and each row is a list item whose
  name is the item text from `ItemTextSelector` or the template's text.
- The select-all boxes are check boxes named "Select all: " followed by the list title, with a mixed state while some
  rows are selected.
- The move buttons have accessible names and tooltips from `MoveToTargetLabel`, `MoveToSourceLabel`,
  `MoveAllToTargetLabel` and `MoveAllToSourceLabel`; set them when your titles are not "available" and "selected".
- A selected row shows a check as well as the accent fill, so selection does not rely on color.
- A row focused from the keyboard shows the focus ring in the focus-ring color.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move between the select-all box, the search field, the list and the move buttons |
| `Up` / `Down`, `Home` / `End`, `PageUp` / `PageDown` | Move through the rows of a list |
| `Shift` + arrow keys | Extend the selection |
| `Ctrl+A` | Select every row of the list |
| `Enter` | Move the selected rows to the other list |
| `Space` or `Enter` on a button | Run the move |

## Motion

- Row hover and selection fills cross-fade, and the check fades in when a row is selected.
- Moved rows leave and join the lists at once; the lists do not slide.
- Buttons press and release with the Arc button's spring.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Hover or selection fill | 160 ms fade | 120 ms fade | Immediate |
| Selected check | 160 ms fade | Immediate | Immediate |
| Rows moved | Immediate | Immediate | Immediate |

## Responsive behavior

- The two panels share the width the box has, each at least 180 px, with the move buttons between them. Put the box in
  a container at least about 440 px wide; on a narrower screen, show one list per step (a list and a "Choose" sheet)
  instead.
- The box is 320 px tall unless you set `Height` or a parent stretches it; the lists scroll inside their panels.
- Long item text is cut with an ellipsis inside the row.
- No action depends on hover: tapping selects a row and a double tap moves it.

## Related

- `MultiSelect` for a subset chosen inside a form field, and `ChipGroup` for a few options shown at once.
- `ReorderableList` when the order of the chosen items matters, and `TreeView` for choices in a hierarchy.

| Component | Choose it when |
|---|---|
| `MultiSelect` | The chosen values must fit in a compact field |
| `ChipGroup` | A few options that fit on one or two lines |
| `Checkbox` | Each option is a yes or no answer in a form |
| `ReorderableList` | People arrange the chosen items in order |
| `TreeView` | The items form a hierarchy |
