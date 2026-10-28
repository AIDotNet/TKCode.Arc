---
title: Cascader
summary: Pick one node of a hierarchy level by level, such as province, city and district, with a field and menu that match Select.
---

<Demo name="Basic" />

## When to use

`Cascader` picks one node of a hierarchy. Opening it shows the first level; choosing a parent opens its children in a
column to the right, and the last choice is a leaf. The field then shows the whole path. Use it when the levels are
fixed, there are two to five of them, and the person gains from seeing the siblings at each level.

- **Province, city and district** in an address form: the three levels are one answer, and the full path is what you store.
- **Product categories** in a catalog editor: a category such as "Electronics / Phones / Smartphones".
- **An organization chart position**: pick a department and a team in one field.

## When not to use

- **Deep trees or levels with thousands of nodes**: use a `TreeView` in a `Popover`, or a `Combobox` that searches.
- **A single level**: use `Select` or `Combobox`.
- **Several values at once**: a multi-select version is not part of `Cascader`.
- **Searching across levels or loading children on demand**: not in this version.

## Usage

`Cascader` derives from Avalonia's `TemplatedControl`. Give it the root nodes in `ItemsSource` and tell it how to read a
node with delegates: `ChildrenSelector` (the same shape as `TreeView`'s), `TextSelector` and `ValueSelector`. Nothing is
reflected, so it is safe for AOT and trimming.

```csharp
public sealed record Region(string Code, string Name, IReadOnlyList<Region>? Children = null);

cascader.ItemsSource = regions;
cascader.ChildrenSelector = node => ((Region)node).Children;
cascader.TextSelector = node => ((Region)node).Name;
cascader.ValueSelector = node => ((Region)node).Code;
```

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Cascader Label="Region"
                  Description="Pick a province, a city and a district."
                  Placeholder="Select a district"
                  Width="320" />
</UserControl>
```

A node without children, or one that `IsLeafSelector` calls a leaf, is a leaf. The field, the menu surface, the gliding
highlight and the clear button are the ones of `Select` and `Combobox`.

### Choosing parents

By default only leaves can be picked: clicking a parent opens its children. Set `LeafOnly` to `False` to let any level
be picked. A click on a parent then selects it and keeps the menu open, so the person can still go deeper.

<Demo name="ParentSelectable" />

### What the field shows

`ShowFullPath` (true by default) shows the whole path, joined by `PathSeparator` (" / "). Set it to `False` to show only the
selected node. `SelectedText` is the text the field shows.

<Demo name="LeafName" />

### Opening on hover

`ExpandTrigger` decides how a parent opens its children. `Click` (the default) opens on a click or the keyboard; `Hover`
also opens while the pointer rests on the parent.

<Demo name="HoverExpand" />

### Binding to a view model

`SelectedItem`, `SelectedValue` and `SelectedPath` always agree, and the first two are two-way. Values are compared with
`Equals` on what `ValueSelector` returns, so two nodes with the same name are told apart by their values. **A value must be
unique across the whole tree**: a code that starts at 01 under every province is not unique. If two nodes share a value,
the first one wins, and a debug build writes a warning.

A value that the data does not contain yet is kept. While the data is loading the field shows the placeholder and
`SelectedValue` stays as written; when `ItemsSource` or the collection raises its change, the control finds the node and
fills in `SelectedItem` and `SelectedPath`. Replacing the data with a set that lacks the node drops the item and the
path but keeps the value, so a view model's value is never wiped by a reload.

<Demo name="Binding" />

Children are watched when the root collection raises `CollectionChanged`; changes in the collections below it show when the
menu is next opened.

### Disabled and read-only

`DisabledSelector` marks nodes that are shown but cannot be picked or opened. `IsReadOnly` lets the field be focused and
opened to look, but not changed or cleared. `IsEnabled="False"` disables the field as a whole.

<Demo name="Disabled" />

### Clearing

While a value is set, `IsClearable` (true by default) shows a clear button; clicking it clears the value without
opening the menu. `Clear()` does the same from code, and Delete or Backspace on the focused field too.

## API reference

<!-- api: Cascader -->

<!-- api: CascaderSelectionChangedEventArgs -->

<!-- api: CascaderExpandTrigger -->

## Accessibility

- The field is exposed as a combo box that expands and collapses, and its value is `SelectedText`. `Label` is the accessible name and `Description` the help text.
- Moving the highlight announces the node, its level and the number of nodes in the column.
- Closing the menu returns focus to the field.

### Keyboard

| Key | When | Action |
|---|---|---|
| Enter, Space, Down, Up | Menu closed | Open the menu on the selected path |
| Delete, Backspace | Menu closed, value set | Clear the value |
| Up, Down | Menu open | Move in the column, skipping disabled nodes |
| Home, End, Page Up, Page Down | Menu open | Jump in the column |
| Right | Menu open | Open the highlighted parent and move into its first child |
| Left | Menu open | Back to the previous column, with its parent highlighted |
| Enter, Space | Menu open | Pick a leaf; open a parent (or pick it when `LeafOnly` is false) |
| Escape | Menu open | Close without a change |
| Tab | Menu open | Close without a change and move on |
| A letter | Menu open | Jump to the next node in the column that starts with it |

## Motion

- The menu opens and closes like `Select`'s.
- When a column comes or goes, the menu's width springs to its new size, and a running spring is redirected instead of
  waiting. A new column fades in while it slides 8 px in from the left. When the menu opens, every column of the selected
  path is laid out at once, with no column-by-column animation.
- The highlight glides between rows, and the field's text rolls when the choice changes.
- With reduced motion the width does not animate and a new column only fades in; with motion off everything is immediate.

## Related

- `Select`: a single level of options.
- `Combobox`: a single level that you search by typing.
- `TreeView`: a whole tree on the page.
