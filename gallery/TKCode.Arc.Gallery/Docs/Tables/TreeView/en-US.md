---
title: Tree view
summary: Navigate nested folders and structured content.
---

<Demo name="FileTree" />

## When to use

- Folders, files and other nested categories that people open and select.
- Navigation where the selection drives content shown next to the tree, such as a `CodeBlock`.
- Large hierarchies. Rows are pooled and only the rows near the viewport are created.

## When not to use

- A few collapsible sections: use `Accordion`.
- The current location in a page hierarchy: use `Breadcrumb`.
- The keys and values of one JSON document: use `JsonViewer`.
- A flat list that the user reorders: use `ReorderableList`.

## Usage

Nodes can be declared inline in XAML. A `TreeViewItem` with child items is a folder.

```xml
<arc:TreeView MinWidth="260" ShowIndentGuides="True">
    <arc:TreeView.Items>
        <arc:TreeViewItem Header="src" IsExpanded="True">
            <arc:TreeViewItem.Items>
                <arc:TreeViewItem Header="components">
                    <arc:TreeViewItem.Items>
                        <arc:TreeViewItem Header="button.tsx" />
                        <arc:TreeViewItem Header="tree-view.tsx" />
                    </arc:TreeViewItem.Items>
                </arc:TreeViewItem>
                <arc:TreeViewItem Header="lib" />
            </arc:TreeViewItem.Items>
        </arc:TreeViewItem>
        <arc:TreeViewItem Header="README.md" />
    </arc:TreeView.Items>
</arc:TreeView>
```

### Data with a children selector

Bind `ItemsSource` and set the selectors from code. `ChildrenSelector` returns the children of a node, and
a node with children is a folder.

<Demo name="DataWithChildrenSelector" />

```csharp
var tree = new TreeView
{
    ItemsSource = nodes,
    ChildrenSelector = item => ((FileNode)item).Children,
    HeaderSelector = item => ((FileNode)item).Name,
    Width = 300
};
tree.SelectionChanged += (_, e) => selected.Text = $"SelectedItem: {e.NewItem}";
tree.SelectedItem = nodes[0].Children![2];   // expands the ancestors of the node
```

`SelectedItem` is two-way, so it can be bound to a view model:

```xml
<arc:TreeView ItemsSource="{Binding Files}"
              SelectedItem="{Binding SelectedFile, Mode=TwoWay}" />
```

### Icons

`Icon` on `TreeViewItem` takes a `Geometry`. `ShowIcons` turns icons on or off for the whole tree, and
`IconSelector` supplies icons for data nodes.

```csharp
tree.ShowIcons = true;
tree.IconSelector = item => ((FileNode)item).Children is null ? fileGlyph : folderGlyph;
```

### Expanding and collapsing from code

```csharp
tree.ExpandAll();
tree.CollapseAll();
tree.ToggleExpanded(folderNode);
tree.ScrollIntoView(fileNode);
tree.FocusItem(fileNode);
```

### Events

`ItemInvoked` fires when a row is activated by a click or by Enter. `ItemExpanded` and `ItemCollapsed`
report folders that open and close.

```csharp
tree.ItemInvoked += (_, e) => OpenFile(e.Item);
tree.ItemExpanded += (_, e) => log.Text = $"Opened {e.Item}";
```

### Thousands of nodes

The tree has 40 folders with 250 files each. Rows are virtualized and pooled, and only the rows near the viewport
animate.

<Demo name="ThousandsOfNodes" />

## API reference

<!-- api: TreeView -->

<!-- api: TreeViewItem -->

## Accessibility

- The tree has the name "File tree" by default. Set `AutomationProperties.Name` to name it for your content.
- Rows expose tree item roles with their level, position in the set, expanded state and selected state.
- Only the focused row is in the Tab order. Arrow keys, Home and End, PageUp and PageDown move between rows.
- Right arrow opens a folder or enters its first child. Left arrow closes a folder or moves to its parent.
- Enter or Space selects the row and toggles a folder. Typing a name moves to the next row that starts with it.
- The focus ring is drawn for keyboard focus only. Icons and indent guides are not announced.

## Motion

- Opening a folder grows its space on a spring, and the new rows stagger in from the left.
- The selection highlight glides from row to row, and the folder arrow turns with a snappy spring.
- Closing a folder collapses its rows smoothly.
- Reduced motion shows and hides rows at once. Off jumps straight to the final state.

## Related

- `Accordion` for a few sections of content that open and close.
- `Breadcrumb` to show where the current page sits in a hierarchy.
- `JsonViewer` for nested data that people inspect, with search and copy.
- `CodeBlock` to show the source of the file selected in the tree.
