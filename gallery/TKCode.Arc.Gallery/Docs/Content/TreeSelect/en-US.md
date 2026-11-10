---
title: Tree select
summary: Pick one node or check several in a dropdown tree, with a search that keeps the path to every match and parents that check their whole branch.
---

<Demo name="Preview" />

## When to use

- **Files or folders** for a review, an upload target or a permission rule.
- **Departments, teams and people** in one field, where checking a team picks everyone in it.
- **Categories of any depth** when the person types to find a node instead of opening level after level.

## When not to use

- **A short, flat list**: use `Select` or `MultiSelect`.
- **Two to five fixed levels, one answer, siblings worth seeing**: use `Cascader`.
- **A tree that is the page's main content**: use `TreeView`.
- **Children loaded from a server on demand**: not in this version.

## Usage

`TreeSelect` derives from Avalonia's `TemplatedControl`. Give it the roots in `ItemsSource` and read each node with
delegates, the same shapes as `Cascader` and `TreeView`. Nothing is reflected, so it is safe for AOT and trimming.

```csharp
public sealed record FileNode(string Path, string Name, IReadOnlyList<FileNode>? Children = null, bool IsLocked = false)
{
    public bool IsFolder => Children is not null;
}

treeSelect.ItemsSource = files;
treeSelect.ChildrenSelector = node => ((FileNode)node).Children;
treeSelect.TextSelector = node => ((FileNode)node).Name;
treeSelect.ValueSelector = node => ((FileNode)node).Path;   // unique across the tree
treeSelect.IconSelector = node => ((FileNode)node).IsFolder ? ArcIcons.Folder : ArcIcons.File;
treeSelect.DisabledSelector = node => ((FileNode)node).IsLocked;
```

### Picking one node

`SelectionMode="Single"` (the default) picks a node and closes. `SelectedItem` and `SelectedValue` are two-way;
`ShowFullPath` shows the path joined by `PathSeparator`.

<Demo name="Single" />

### Leaves only

`LeafOnly="True"`: a click on a parent only opens or closes it.

<Demo name="LeafOnly" />

### Checking several nodes

`SelectionMode="Multiple"` adds a checkbox to every row and keeps the menu open. Checking a parent checks its enabled
descendants; a partly checked parent shows a dash. Disabled nodes keep their state. `CheckStrategy` sets what
`SelectedItems` and `SelectedValues` report:

| `CheckStrategy` | Reports |
|---|---|
| `Leaves` (default) | Checked leaves |
| `Parents` | A fully checked parent instead of its subtree |
| `All` | Every fully checked node |

<Demo name="CheckStrategy" />

`SelectedValues` is two-way: setting it checks those nodes (a parent's value checks its enabled descendants), and it then
holds what the strategy reports. Values set before the data arrives are applied when it does. Picked nodes show as
chips; past `MaxVisibleChips` (3) they fold into "+N".

### Searching

`IsSearchable` (true by default): typing in the open field filters the tree. Matches keep their ancestors, which open,
and the matched text is marked; a matched parent keeps its children. Clearing the search restores what was open.
`FilterPredicate` replaces the default case-insensitive contains match. `SearchText` is the typed text (two-way).

### Large trees

Rows are virtualized: only the rows on screen are built. `ExpandDepth` sets the levels open when the data arrives (`-1`
opens all).

<Demo name="LargeTree" />

## API reference

<!-- api: TreeSelect -->

<!-- api: TreeSelectSelectionChangedEventArgs -->

<!-- api: TreeSelectMode -->

<!-- api: TreeSelectCheckStrategy -->

## Accessibility

- The field is a combo box that expands and collapses; `Label` is its name, `Description` its help text. Its value is
  `SelectedText` (single) or "N selected" (multiple).
- Rows are tree items with expand / collapse, selection and, in multiple mode, a three-state toggle.
- Moving the highlight announces the node, its level and its check state.
- "No matches", "Clear selection" and the announcements follow the app language (en-US, zh-CN, zh-TW).

### Keyboard

| Key | When | Action |
|---|---|---|
| Enter, Space, Down, Up | Closed | Open on the selection |
| A character | Searchable | Search (opens the menu) |
| Delete, Backspace | Closed, something selected | Clear |
| Up, Down, Home, End, Page Up, Page Down | Open | Move the highlight |
| Right | Open | Open the parent, or move to its first child |
| Left | Open | Close the parent, or move to the parent |
| Enter | Open | Pick (single) or toggle the check (multiple) |
| Space | Open, search empty | Pick (single) or toggle the check (multiple) |
| Backspace | Open, multiple, search empty | Remove the last chip |
| Escape | Open | Clear the search, then close |

## Motion

- The menu opens and closes like `Cascader`'s; chips enter and leave like `MultiSelect`'s.
- A parent's rows grow in with a stagger and its chevron turns, as in `TreeView`; checks draw themselves and morph
  into the dash, as in `Checkbox`. The highlight glides between rows.
- With reduced motion the chevron does not turn and rows and checks change at once. Search results always appear at once.

## Responsive behavior

- No breakpoint. The field takes the width it is given (at least 200 px).
- The menu is as wide as the field and at least 240 px; rows trim long labels with an ellipsis.
- The tree scrolls within `MaxDropDownHeight` (320 px). The menu flips above the field when there is no room below.

## Related

- `Cascader`: one node, level by level.
- `MultiSelect`: several values from a flat list.
- `TreeView`: a tree on the page.
