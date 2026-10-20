---
title: JSON viewer
summary: A collapsible JSON tree with search, paging for long arrays, and copy value or path.
---

## When to use

- Inspecting an API response, a webhook payload, a log entry or a configuration file.
- Large documents where people search for a key or value and open only the branches they need.
- Developer tools and debug panels that show data from a service.

## When not to use

- Showing a whole JSON document as copyable source: use `CodeBlock`.
- Folders, files or other nested categories that the user selects: use `TreeView`.
- Values that are not JSON, such as arbitrary .NET objects: convert them to `JsonNode` or `JsonElement` first.

## Usage

Pass JSON text with `Json`. Use `Value` for a `JsonNode` or `Element` for a `JsonElement` when the data is
already parsed.

```xml
<arc:JsonViewer Json="{Binding ResponseJson}"
                ExpandDepth="2"
                MaxTreeHeight="320" />
```

### Parsed data

When the data is already a `JsonNode`, bind `Value`. Build `JsonNode` values with the `JsonNode` overloads in
code, which stay AOT safe.

```csharp
var items = new JsonArray();
items.Add((JsonNode)new JsonObject { ["id"] = 1, ["sku"] = "SKU-0001", ["inStock"] = true });

viewer.Value = new JsonObject
{
    ["count"] = 1,
    ["items"] = items
};
```

If the text is not valid JSON, `ParseError` holds the message.

### Expansion

`ExpandDepth` sets how many levels open at first. The default opens the root only. `ExpandedPaths` lists the
open branches and can be read and set in code.

```csharp
viewer.ExpandAll();
viewer.CollapseAll();
viewer.Expand("root.data.lines");
viewer.Collapse("root.data.customer");
```

### Search

`IsSearchable` is on by default. `Query` filters the keys and values, opens the branches that hold a match and
reports the number of matches in `MatchCount`.

```xml
<arc:JsonViewer Json="{Binding Payload}"
                Query="{Binding SearchText, Mode=TwoWay}"
                SearchPlaceholder="Search keys and values" />
```

```csharp
viewer.NextMatch();
viewer.PreviousMatch();
```

### Long arrays

`PageSize` sets how many children a branch shows at once. A "Show more" row loads the next page.

```xml
<arc:JsonViewer Json="{Binding LargeCatalog}" PageSize="50" />
```

### Paths and copy

`RootName` is the prefix of every path, such as `root.users[0].name`. `IsPathVisible` shows the path bar, and
`IsCopyable` enables copy. `CurrentRowChanged` reports the row that has the focus, and `Copied` reports a value
or path copy.

```csharp
viewer.RootName = "payload";
viewer.Copied += (_, e) => status.Text = $"Copied {e.Kind} of {e.Path}";
viewer.CurrentRowChanged += (_, e) => current.Text = $"{e.Path} ({e.ValueType})";
```

## API reference

<!-- api: JsonViewer -->

## Accessibility

- The tree follows the WAI-ARIA tree pattern: each row exposes its level, its position in the set and its expanded or selected state.
- Only one row is in the Tab order. A row is read with its key and value, or with its type and item count.
- Arrow keys, Home and End move through the rows. Enter or Space opens a branch or loads the next page.
- Slash focuses the search box. Enter and Shift + Enter step through the matches, and Escape clears the search.
- The match count is in a polite region. Copy results are announced in the status region.
- Row copy buttons are not in the Tab order. Use Ctrl/Cmd + C to copy the current value, with Shift to copy the path.

## Motion

- Each row opens its height on a smooth spring when a branch expands. Loading a page and expanding all use the same motion.
- The current-row highlight glides between rows, and the tree scrolls to the row when it settles.
- The copy icon turns into a check mark or a cross with a small scale and blur.
- Reduced motion applies row changes at once and moves the highlight and scroll directly to their places. Off jumps straight to the final state.

## Related

- `TreeView` for nested records that people select rather than inspect.
- `CodeBlock` to show the whole JSON as copyable source.
- `CopyButton` to copy a single value outside the tree.
