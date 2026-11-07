---
title: Property grid
summary: Edit the properties of one or more objects in rows of label and editor, grouped by category, with search, nested values and lists.
---

<Demo name="Basic" />

## When to use

`PropertyGrid` edits the properties of objects the way the properties palette of a design or modeling tool does: each
property is a row with its name on the left and an editor on the right, rows are grouped by category, and the selected
row's description shows underneath. Use it when the set of properties comes from the object itself rather than from a
form you lay out by hand.

- **The properties palette of a CAD or modeling tool**: select an arc and edit its layer, color, center and radius; select
  several shapes and edit what they share.
- **Settings and configuration editors**: a settings class with categories and descriptions becomes an editor without
  writing a form.
- **Visual designers**: edit the Avalonia properties of a control on a canvas.

## When not to use

- **A form people fill in once**, such as sign-up or checkout: lay out `Input`, `Select` and the other fields yourself,
  with labels, help text and validation where they belong.
- **Many records with the same fields**: use a `Table` or the `DataGrid`, which compare values across rows.
- **A handful of options**: a few `Switch` or `Select` fields in a `Card` read better than a grid.

## Usage

Mark the class with `[PropertyGridModel]` and give the grid an object. The Foundation generator (shipped with
`TKCode.Arc.Foundation`) reads the class at compile time and registers every public property with typed read and write
delegates, so nothing is reflected at run time and the grid works with Native AOT and trimming.

```csharp
using System.ComponentModel;
using TKCode.Arc;

[PropertyGridModel]
public sealed class Arc
{
    [Category("General"), Description("The layer the shape belongs to.")]
    public string Layer { get; set; } = "Walls";

    [Category("General"), DisplayName("Line type")]
    public LineType LineType { get; set; }

    [Category("Geometry"), PropertyGridOptions(Minimum = 0, FractionDigits = 4)]
    public double Radius { get; set; } = 1624.1491;

    [Category("Geometry")]
    public double Area => Math.PI * Radius * Radius;   // no setter: read-only
}

properties.SelectedObject = new Arc();
```

The grid reads `[Category]`, `[DisplayName]`, `[Description]`, `[ReadOnly]` and `[Browsable(false)]` from
`System.ComponentModel`, and `[Display(Name, GroupName, Description)]` from DataAnnotations. `[PropertyGridOptions]` adds
the grid's own options: `Order`, the number editor's `Minimum`, `Maximum`, `Step` and `FractionDigits`, an `EditorKey`
for a custom editor, style `Classes` for the row, and `Expand`.

Each type gets an editor from Arc's fields at the small size: `Input` for text, `NumberField` for numbers, `Switch` for
`bool`, `Select` for enums (a member's `[Description]` is its text), `ColorPicker` for `Color` and `DatePicker` for
`DateOnly`, `DateTime` and `DateTimeOffset`. A read-only property shows its value as text. Text commits on Enter or when
focus leaves the field, and Escape puts the value back. When the object raises `PropertyChanged`, the rows read it again,
so values computed from others (the area above) follow every edit; call `Refresh()` for objects that raise nothing.

### Several objects

`SelectedObjects` edits several objects at once. The rows are the properties they share (same name and type), a value
they disagree on shows `MixedValueText` ("Varies"), and an edit writes every object. A selector above the grid, as in a
CAD palette, can narrow the selection to one kind of shape.

<Demo name="MultiSelect" />

### Nested values and lists

A property whose type is also marked `[PropertyGridModel]` (or registered in code) has a chevron and expands into its own
rows. A struct is copied, changed and written back to its owner, up to the selected object, so a `Point` property edits
like any other. A list (`IList`, arrays included) expands into `[0]`, `[1]`, … rows; items that are models expand
further. When the list can grow and the generator can create an item (a value type, a string, or a class with a public
parameterless constructor), the list's row has an add button and each item a remove button.

<Demo name="Lists" />

### Custom editors and styles

`Editors` replaces the built-in editor for some rows. A `PropertyGridEditor` matches by `EditorKey` first (set with
`[PropertyGridOptions(EditorKey = "...")]`), then by `PropertyName`, then by `PropertyType`. Its template's data context
is the `PropertyGridRow`: bind to `Value`, `IsReadOnly`, `IsMixed` and `DisplayText`.

Rows are `PropertyGridItem` controls. Style them by pseudo-class (`:readonly`, `:mixed`, `:expanded`, `:nested`,
`:list-item`, `:invalid`, `:selected`), by `PropertyName`, or by the classes `[PropertyGridOptions(Classes = "...")]` adds.

<Demo name="CustomEditors" />

### Registering types in code

Types you cannot mark, such as `System.Numerics.Vector3` or types from another library, are registered once at startup.
`Set` writes a class; `Update` returns the changed copy of a struct or an immutable type, which the grid writes back.

```csharp
PropertyGridTypes.Register<Vector3>(t =>
{
    t.Property("X", v => v.X).Update((v, x) => { v.X = x; return v; });
    t.Property("Y", v => v.Y).Update((v, y) => { v.Y = y; return v; });
    t.Property("Z", v => v.Z).Update((v, z) => { v.Z = z; return v; });
    t.Summary(v => $"{v.X:0.##}, {v.Y:0.##}, {v.Z:0.##}");
});
```

### Avalonia controls

An Avalonia object needs no registration: its styled and direct properties are listed, grouped by the type that declares
each one. `PropertyFilter` keeps the ones you want to show.

<Demo name="Controls" />

### Layout options

`IsCategorized` switches between category headers and an alphabetical list, `IsSearchVisible` and `IsDescriptionVisible`
show or hide the search field and the description area, and `IsReadOnly` shows every value as text. Drag the line between
the labels and the editors to change `LabelWidth`.

<Demo name="Options" />

### Changes and errors

`PropertyValueChanged` is raised after every edit, with the row, the value each object had before and the value written
(after an item was added or removed, the list's row and the list). When a setter throws, or text cannot be converted, the
value stays as it was, the row turns `:invalid` and `PropertyGridRow.ErrorMessage` says why.

```csharp
properties.PropertyValueChanged += (_, e) =>
    undo.Push(e.Row.Path, e.OldValues, e.NewValue);
```

## API reference

<!-- api: PropertyGrid -->

<!-- api: PropertyGridItem -->

<!-- api: PropertyGridCategoryItem -->

<!-- api: PropertyGridEditor -->

<!-- api: PropertyGridOptionsAttribute -->

## Accessibility

- Every row is named by its label and described by the property's description; an expandable row reports whether it is
  expanded, and every editor is named by the row's label.
- The add and remove buttons of lists are named by `AddItemText` and `RemoveItemText`.
- A failed write is shown by the danger color of the label and by a tooltip with `ErrorMessage`, not by color alone.

### Keyboard

| Key | Action |
|---|---|
| Tab | Moves through rows, headers and editors in order. |
| ArrowDown / ArrowUp | On a row or header: the next or previous one. |
| Home / End | On a row or header: the first or last one. |
| ArrowRight | Expands a row or category; on an expanded row, moves to its first child. |
| ArrowLeft | Collapses a row or category; on a collapsed row, moves to its parent or category. |
| Enter / F2 | On a row: moves into its editor. On a header, Enter and Space collapse or expand it. |
| Escape | In an editor: puts back the value being typed, then returns to the row. |

## Motion

- The chevrons of rows and categories turn with Arc's spring when they open or close; hover and selection fills fade in
  120 ms.
- The editors keep their own motion (the number field's digits, the select's menu, the color picker's panel).
- With reduced motion the chevrons turn without the spring; with motion off nothing animates.

## Responsive behavior

- `PropertyGrid` has no breakpoint. Its label column is `LabelWidth` (140 px by default) and the editor takes the rest;
  the label column never leaves the editor less than 96 px, and it never gets narrower than 64 px.
- It measures its own width (`Bounds.Width`), not the window's, and its `MinWidth` is 240 px.
- Labels and values are cut with an ellipsis (`CharacterEllipsis`), horizontal scrolling is off, and the description
  panel shows at most two lines (`MaxLines`), so a long value never pushes the grid sideways.
- Rows are 40 px tall (`RowHeight`), and nothing needs hover, but the add and remove buttons on a collection row are 28
  px squares (`ActionSize`). On a phone, lower `LabelWidth` (down to 64 px) to give the editor more room.

## Related

- `Input`, `NumberField`, `Switch`, `Select`, `ColorPicker` and `DatePicker`: the built-in editors.
- `TreeTable` and `TreeView`: show a hierarchy of records rather than the properties of one.
- `Accordion`: groups content under headers that open and close, like the grid's categories.
