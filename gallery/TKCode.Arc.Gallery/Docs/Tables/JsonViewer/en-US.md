---
title: JSON viewer
summary: A collapsible JSON tree with search, paging for long arrays, and copy value or path.
---

<Demo name="WebhookPayload" />

## When to use

`JsonViewer` shows a JSON document as a tree that people can open branch by branch. It replaces the raw text in
the places where engineers and support staff need to read a document, not edit it: a developer panel, a support
console, an integration log. The viewer owns the whole surface (a toolbar with search, the tree, a path bar),
so a page only supplies the JSON and places the control in its layout.

- **Webhook delivery inspection** in an integrations console: an `invoice.paid` or `order.updated` payload that a
  developer opens to check the fields a handler reads. With `ExpandDepth="2"` the root and `data` open, and people
  open only the branch they need, such as `data.customer`.
- **API response debugging** in an internal tool: the body of a `GET /v1/orders` call shown next to the request
  that produced it. Copying the path of a field (`root.items[2].sku`) gives the exact reference for a bug report.
- **Log and event detail** in an incident timeline or an audit view: a structured log entry whose `metadata` object
  holds fields that vary by event. Search finds the key or value across the whole entry.
- **Configuration review** in a settings or deployment page: a feature-flag document or a pipeline definition that
  an operator reads and copies a value from, without the risk of editing the source.
- **Large export previews** in a data tool: an array of several thousand records that loads in pages of 50 children,
  so the first screen appears at once and the rest is loaded on request.

## When not to use

- **The whole document as copyable source**, such as a snippet for a README or a support reply: use `CodeBlock`,
  which shows the text as one block with a single copy action.
- **Nested categories that people select**, such as folders, files or an organization chart: use `TreeView`, which
  is built for selection and does not parse JSON.
- **Tabular records of one shape**, such as a list of invoices people sort and filter by column: use
  `Table`. A JSON tree shows structure; a table compares rows.
- **Values that are not JSON**, such as arbitrary .NET objects: convert them to `JsonNode` or `JsonElement` first.
  The viewer reads only System.Text.Json data.
- **Editing a document**: the viewer is read-only. Use a `Textarea` or `RichTextEditor` when people change the text.

## Usage

Pass JSON text with `Json`. Use `Value` for a `JsonNode` or `Element` for a `JsonElement` when the data is already
parsed.

```xml
<arc:JsonViewer Json="{Binding ResponseJson}"
                ExpandDepth="2"
                MaxTreeHeight="320" />
```

`JsonViewer` derives from Avalonia's `TemplatedControl`. Its template holds a toolbar, a scrolling tree of rows, a
path bar and a parse error row, and it draws its own focus ring and row actions. The theme styles those parts; the
keyboard, search, paging and copy behavior belongs to the control itself.

### Anatomy

1. **Toolbar**: shown when `IsSearchable` is `True` or the document has more than one row. It holds the search pill,
   the match counter, the previous and next match buttons, and the expand all and collapse all buttons.
2. **Search pill** (`SearchPlaceholder`, `Query`): a 34 px high pill on the muted surface color with a search glyph
   and the placeholder "Search keys and values" by default.
3. **Match counter**: `1/3` for the current match of three, or `0/0` when nothing matches. It is a polite live
   region.
4. **Tree** (`MaxTreeHeight`): the rows scroll inside this height; the default is 420 px. Each row is 30 px high.
5. **Row**: the key (or the array index), a colon, and the value. Strings use the success color, numbers the accent
   color and booleans the warning color; keys use the foreground color. Branches show their item count in the
   muted color (`2 items`, `1 key`) and, while collapsed, a preview such as `{ name, email }`.
6. **Row actions**: two icon buttons at the right edge of a row, copy value and copy path. They appear on hover and
   on the current row. They are not in the tab order.
7. **Show more row**: at the end of a long branch, "Show 50 more" with the number of hidden children. It loads the
   next page of `PageSize` children.
8. **Parse error row** (`ParseError`): shown in place of the tree when `Json` is not valid JSON.
9. **Path bar** (`IsPathVisible`): the current row's path in the monospace font, its type label with the item count
   (`object, 5 keys`) and the copy path button (`IsCopyable`). The type label hides when the viewer is narrow.

### Loading data

Three properties load a document. Each one replaces the document of the others, so the last value set is the one
on screen.

| Property | Type | What the viewer does with it |
|---|---|---|
| `Json` | `string?` | Parses the text into a document. Invalid text sets `ParseError` and leaves the tree empty. |
| `Value` | `JsonNode?` | Serializes the node into a document when set. Later changes to the node do not show; set `Value` again. |
| `Element` | `JsonElement?` | Clones the element, so its `JsonDocument` can be disposed after the viewer receives it. |

`RootName` (default `root`) is the first segment of every path. `ExpandDepth` (default `1`) sets how many levels
open when data arrives: `1` opens the root only, `2` also opens the root's children.

If the text is not valid JSON, `ParseError` holds the message and the tree stays empty.

#### Parsed data

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

### Expansion

`ExpandDepth` sets the open levels at first. The default opens the root only. `ExpandedPaths` lists the open
branches; it is two-way, so a view model can save the open set and restore it later. `ExpandedPathsChanged` is
raised after the set changes.

```csharp
viewer.ExpandAll();
viewer.CollapseAll();
viewer.Expand("root.data.lines");
viewer.Collapse("root.data.customer");
```

Setting `Json`, `Value` or `Element` starts over: the open set is rebuilt from `ExpandDepth`, the search is run again
and the tree scrolls back to the top. To keep a person's place across a refresh, read `ExpandedPaths` before the
new document arrives and set it again afterwards.

```csharp
viewer.ExpandedPaths = ["root", "root.data", "root.data.lines"];
```


### Search

`IsSearchable` is on by default. `Query` filters the keys and primitive values, case-insensitively, opens the
branches that hold a match and reports the number of matches in `MatchCount`. The count stops at 2,000 matches.
`CurrentPath` moves to the first match when a query is entered, and `NextMatch` and `PreviousMatch` step through
the matches in document order.

<Demo name="Search" />

```xml
<arc:JsonViewer Json="{Binding Payload}"
                Query="{Binding SearchText, Mode=TwoWay}"
                SearchPlaceholder="Search keys and values" />
```

```csharp
viewer.NextMatch();
viewer.PreviousMatch();
```

A branch that the person closes while a search is active stays closed until the query changes. The viewer does not
reopen it on the next match step, so the person's choice wins over the search.

### Long arrays

`PageSize` sets how many children a branch shows at once; the default is 50, and the minimum is 1. A "Show more"
row loads the next page and reports how many children are still hidden. Pressing Enter or Space on the row loads
the next page, and `FocusPath` moves to any row by its path.

<Demo name="LongArrays" />

```xml
<arc:JsonViewer Json="{Binding LargeCatalog}" PageSize="50" />
```

Paging applies to arrays and objects alike. A page size of 50 keeps the first screen fast for a catalog export of
thousands of items. Raise `PageSize` only when people need to scan a whole page at once; a page of 500 rows is
rarely easier to read than ten pages of 50.

### Paths and copy

Every row has a path that describes where its value sits in the document. The path starts with `RootName` and
uses dots for identifier keys and brackets for everything else.

| Key in the JSON | Path |
|---|---|
| `data` | `root.data` |
| `$ok_1` | `root.$ok_1` (a key of letters, digits, `_` and `$` that does not start with a digit) |
| `order-id` | `root["order-id"]` |
| The second item of `lines` | `root.lines[1]` |

`IsPathVisible` shows the current row's path in the path bar, and `IsCopyable` enables the copy actions. Copying
a value gives the value's text: a string without its quotes, a branch as two-space indented JSON, and any other
value as its literal. Copying a path gives the path text.

```csharp
viewer.RootName = "payload";
viewer.Copied += (_, e) => status.Text = $"Copied {e.Kind} of {e.Path}";
viewer.CurrentRowChanged += (_, e) => current.Text = $"{e.Path} ({e.ValueType})";
```

`CurrentRowChanged` fires when a row becomes the current one, by keyboard or by click, and reports its path, value
and `JsonViewerValueType`. `Copied` reports the copy kind (`JsonViewerCopyKind.Value` or
`JsonViewerCopyKind.Path`), the path and the copied text. Use `Copied` for a status message or an audit entry.

### Narrow containers

At 420 px and below (`CompactWidth`), indentation tightens to 12 px, keys take a limited share of the row so the
values stay visible, and the type label in the path bar hides. No code is needed; the layout follows the width.

<Demo name="Narrow" />

### Data shape and performance

The viewer accepts JSON only: text, a `JsonNode` or a `JsonElement`. It does not read arbitrary CLR objects, which
keeps it safe for ahead-of-time compilation and trimming. Serialize your model with `System.Text.Json`, or build
the `JsonNode` tree by hand as shown above, before binding it.

- **Rows are created for the viewport.** The tree is a flat list of rows that the viewer realizes only where they
  are visible. A document with thousands of objects can be opened (`ExpandAll`) and still scroll smoothly; the
  paging limit keeps each branch small.
- **Search reads the whole document on the UI thread.** It walks every key and primitive value and stops reporting
  at 2,000 matches. For a payload of several megabytes, debounce the view model's `Query` (wait for a short pause
  in typing) before it reaches the viewer.
- **Values are shown as written.** The viewer does not format currency, dates or thousands separators; format
  them in your own label beside the viewer.

### Binding to a view model

In an MVVM app the view model holds the JSON text and the search query, and the viewer shows them. With
CommunityToolkit.Mvvm, an `ObservableObject` holds the state and the commands.

```csharp
public interface IWebhookService
{
    Task<string> GetLatestPayloadJsonAsync();
}

public partial class WebhookInspectorViewModel : ObservableObject
{
    private readonly IWebhookService _webhooks;

    public WebhookInspectorViewModel(IWebhookService webhooks)
    {
        _webhooks = webhooks;
    }

    [ObservableProperty]
    private string? _payloadJson;

    [ObservableProperty]
    private string? _query;

    [ObservableProperty]
    private string _status = "Select a value to inspect it.";

    [RelayCommand]
    private async Task LoadLatestAsync()
    {
        PayloadJson = await _webhooks.GetLatestPayloadJsonAsync();
    }
}
```

```xml
<StackPanel Spacing="12">
    <arc:Button Content="Load latest delivery"
                Command="{Binding LoadLatestCommand}" />
    <arc:JsonViewer Json="{Binding PayloadJson}"
                    Query="{Binding Query, Mode=TwoWay}"
                    ExpandDepth="2"
                    MaxTreeHeight="360" />
    <TextBlock Text="{Binding Status}" />
</StackPanel>
```

- `Json`, `Value`, `Element`, `RootName`, `ExpandDepth`, `PageSize`, `MaxTreeHeight`, `IsSearchable`, `IsCopyable`
  and `IsPathVisible` are one-way: the view model sets them and the viewer does not write them back.
- `Query` is two-way. Typing in the viewer's search box updates the view model, and the view model can clear the
  search by setting `Query` to an empty string.
- `ExpandedPaths` is two-way, as described in the expansion section. `CurrentPath` and `MatchCount` are read-only
  and update as the person moves and searches.
- `Copied` and `CurrentRowChanged` are events, not commands. Forward them from code behind to the view model:

```csharp
viewer.CurrentRowChanged += (_, e) => vm.Status = $"{e.Path} ({e.ValueType})";
viewer.Copied += (_, e) => vm.Status = $"Copied {(e.Kind == JsonViewerCopyKind.Path ? "path" : "value")} of {e.Path}";
```

### Example: Webhook delivery inspector

A developer opens a delivery from the integrations console. The panel names the event and its time above the tree.
The root and its first level are open, so the customer and the line items are visible at once, and the nested
`metadata` object stays closed until the developer opens it. The tree height is limited to 340 px so the panel fits
beside the delivery list.

![A webhook delivery payload with the root and its first level open and a search toolbar](images/WebhookInspector.png)

```xml
<StackPanel Width="680" Spacing="12">
    <TextBlock Text="Delivery payload"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="invoice.paid  ·  evt_1Q2w3E4r5T  ·  delivered 2026-04-12 09:14 UTC"
               FontSize="13"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    <arc:JsonViewer ExpandDepth="2" MaxTreeHeight="340">
        <arc:JsonViewer.Json>{
  "id": "evt_1Q2w3E4r5T",
  "type": "invoice.paid",
  "created": 1775985240,
  "livemode": false,
  "data": {
    "customer": {
      "id": "cus_9s8d7f6g",
      "name": "Ada Lovelace",
      "email": "ada@example.com"
    },
    "lines": [
      { "description": "Pro plan", "amount": 4800, "quantity": 1 },
      { "description": "Additional seats", "amount": 1200, "quantity": 4 }
    ],
    "total": 9600,
    "currency": "usd",
    "discount": null,
    "metadata": {
      "order-id": "A-1042",
      "note": "Renewed early to keep the founding-member price."
    }
  }
}</arc:JsonViewer.Json>
    </arc:JsonViewer>
</StackPanel>
```

The design decisions are in the data and the depth. `ExpandDepth="2"` opens the root and its direct children, so
the `data` object is open and the fields a developer checks first are one level down. The `customer`, `lines` and
`metadata` branches stay closed, each with a preview on its row (`{ id, name, email }`, `[ … ]`), so the tree reads
as a summary until someone asks for detail. The toolbar's search covers the common case of finding `customer` or
`amount` in a longer payload.

### Content guidelines

Most of the text in a viewer comes from the control (the placeholder, the counts, the copy messages). The text
around it, the heading and the status, is yours to write.

| Instead of | Write |
|---|---|
| JSON | Delivery payload, Response body or Event data |
| Output | Response from POST /v1/invoices |
| Search... | Search keys and values (the default placeholder) |
| Root | The name your team uses in its docs, set with `RootName`, such as `event` |
| Copied! | Copied value of root.data.customer (a status line you build from the `Copied` event) |

- Name the document by its source and event, so a support engineer knows which payload is on screen.
- Use sentence case for the heading and status text, and digits for counts in the text you write.

### Best practices

**Do**

- Set `ExpandDepth` to the level that answers the usual question, usually `2`. A deeper default makes every
  document look busy and hides the field people came for.
- Keep `PageSize` at its default of 50 for arrays from external services. Smaller pages keep each branch short
  enough to scan, and the "Show more" row says how many items remain.
- Debounce `Query` for payloads of several megabytes, because the search walks the whole document on the UI
  thread.

**Avoid**

- Binding `Value` to a `JsonNode` and then changing the node in place. The viewer copies the node when it is set,
  so the tree does not update; assign `Value` again with the new node.
- Calling `ExpandAll` on large documents by default. It opens every branch, and a payload with thousands of
  objects then shows thousands of rows to scan.
- Showing a payload with secrets or personal data in a shared screen without masking it first. The viewer shows
  every value it is given.

### Common questions

**The tree did not change after I edited the `JsonNode`.** `Value` serializes the node when you set it. Changes
to the node afterwards are not seen. Set `Value` again with the updated node, or build a new `JsonObject`.

**Which of `Json`, `Value` and `Element` should I use?** Use `Json` when your data arrives as text, such as an HTTP
response body. Use `Value` when you built or parsed a `JsonNode` in code. Use `Element` when you already hold a
`JsonElement`; the viewer clones it, so you can dispose the `JsonDocument` afterwards.

**Why does the tree collapse when I refresh the data?** Each new document starts from `ExpandDepth`, so the
branches a person opened are closed again. Save `ExpandedPaths` before the refresh and set it again after the new
document loads.

**How do I copy a value from the keyboard?** Select the row with the arrow keys, then press Ctrl+C (Cmd+C on
macOS) to copy its value, or Ctrl+Shift+C (Cmd+Shift+C) to copy its path. The row action buttons are not in the tab
order, so the keyboard shortcuts are the reliable path.

## API reference

<!-- api: JsonViewer -->

## Accessibility

- The tree follows the WAI-ARIA tree pattern: the rows panel is exposed as a tree, and each row exposes its level,
  its position in the set and its expanded or selected state.
- Only one row is in the tab order: the current row. Arrow keys, Home and End move the focus between rows.
- A row is read with its key and value, such as `id: evt_1`, or with its key, type and item count, such as
  `lines, array, 2 items`. A "Show more" row reads as `Show 50 more, 70 hidden`.
- The search box is named "Search JSON". The match counter is a polite live region, so each change to the count is
  announced without moving focus.
- Copy results are announced in a polite status region: "Copied value of root.customer" or "Copy failed". The
  path bar's copy button changes its name to "Copied" or "Copy failed".
- The row action buttons are painted by the control, are not in the tab order and are reached with Ctrl+C or
  Ctrl+Shift+C. The path bar copy button and the toolbar buttons are normal tab stops with names such as
  "Expand all", "Collapse all", "Previous match", "Next match" and "Copy path".
- Hover changes the row fill only; the current row is marked by the highlight and the path bar.

### Keyboard

| Keys | Action |
|---|---|
| `Up` / `Down` | Move to the previous or next visible row |
| `Home` / `End` | Move to the first row (the root) or the last visible row |
| `Right` | Open a closed branch; on an open branch, move to its first child |
| `Left` | Close an open branch; otherwise move to the parent row |
| `Enter` / `Space` | Open or close a branch; on a "Show more" row, load the next page |
| `Ctrl+C` / `Cmd+C` | Copy the current row's value |
| `Ctrl+Shift+C` / `Cmd+Shift+C` | Copy the current row's path |
| `/` | Focus the search box and select its text |
| `Enter` / `Shift+Enter` (in the search box) | Go to the next or previous match |
| `Escape` (in the search box) | Clear the query when it is not empty |
| `Down` (in the search box) | Move focus into the tree on the current row |
| `Tab` / `Shift+Tab` | Leave the tree or return to it; the tree is one tab stop |

## Motion

- Each row opens its height on a smooth spring when a branch expands. Loading a page and expanding all use the same
  motion.
- The current-row highlight glides between rows, and the tree scrolls to the row when it settles. A mouse wheel
  movement stops a scroll that is still running.
- The copy icon turns into a check mark or a cross with a small scale and blur.
- Reduced motion applies row changes at once and moves the highlight and scroll directly to their places. Off
  jumps straight to the final state.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A branch opens or closes, or a page loads | Each row opens its height on a smooth spring | Rows change at once | Immediate |
| The current row moves | The highlight glides between rows | The highlight moves directly | Immediate |
| The tree scrolls to the current row | Springs to where the row will sit once the rows settle | Jumps directly | Immediate |
| The tree has more than 400 visible rows | Rows change at once | Rows change at once | Immediate |

## Related

- `CodeBlock` shows the whole JSON as copyable source, with one copy action.
- `TreeView` is for nested records that people select rather than inspect.
- `Table` is for records of one shape that people sort and filter by column.
- `CopyButton` copies a single value outside the tree.

| Component | Choose it when |
|---|---|
| `CodeBlock` | The JSON is a snippet people copy as text, such as a sample request |
| `TreeView` | The nested items are folders, files or categories that people select |
| `Table` | The records share one shape and people compare them by column |
| `CopyButton` | A single value, such as an ID or a key, needs a copy action beside it |
