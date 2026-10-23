---
title: Button group
summary: Related actions joined into one surface with hairline dividers: a hover highlight glides between segments, the pressed one answers in place, and an attached menu can close the row.
---

<Demo name="DocumentActions" />

## When to use

`ButtonGroup` joins two to five related actions on one object into a single surface with hairline dividers. Use it where
the actions belong together and act on the same thing: a document header, a zoom control, a formatting toolbar, a message
thread. One highlight glides under the pointer or the keyboard focus, so the person always sees which segment is active,
and an optional chevron segment holds the rarer actions without making the row longer.

- **Document actions in a file viewer**: Share, Duplicate and Archive on the header of a deck or a contract, with a
  chevron menu for Pin, Move to and Delete.
- **Zoom controls in a report or chart viewer**: Zoom out, a live value that shows 100% and resets the zoom, and Zoom in.
  The value sits between the two buttons, so the person reads the level where they change it.
- **Reply actions in a support inbox**: Reply, Reply all and Forward at the foot of a ticket thread. The three related
  actions stay together and read as one decision.
- **Formatting toolbar in an internal wiki or CMS editor**: Bold, Italic and Underline as icon-only segments above a text
  block. Familiar glyphs need no label, and the tooltip names each one.
- **Insert blocks in a vertical side toolbar**: Image, Table and Code block stacked in a column beside the editor, with the
  less common blocks in a menu at the end.

## When not to use

- **Options that stay selected, such as a view mode or a period**: use `SegmentedControl` from Toggles. A segment of a button
  group runs an action once and does not remember that it was chosen.
- **One primary action with a few close alternatives**: use `SplitButton`, which keeps the default action prominent and
  puts the alternatives in its menu.
- **Switching between views or panels**: use `Tabs` from Navigation, which shows which panel is open.
- **A long list of actions**: use `DropdownMenu` from Menus for the list, and keep only the few most used actions in the
  group. A row of ten segments collapses or scrolls and loses its meaning.
- **Floating, separate buttons in a tray**: use `FloatingButtonGroup`, which gives each button its own soft surface.

## Usage

Each segment is a `ButtonGroupItem`. Give the group a `Label`, which is its accessible name.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ButtonGroup Label="Document actions">
        <arc:ButtonGroupItem Label="Share" />
        <arc:ButtonGroupItem Label="Duplicate" />
        <arc:ButtonGroupItem Label="Archive" />
    </arc:ButtonGroup>
</UserControl>
```

`ButtonGroup` is an `ItemsControl`, so segments can be declared inline or generated from `ItemsSource`. Each
`ButtonGroupItem` derives from Avalonia's `Button`: `Click`, `Command`, `CommandParameter` and `IsEnabled` work on a
segment as they do on a button. The group itself adds the surface, the highlight, the dividers, the chevron menu and
the row layout.

Plain values in `ItemsSource` become segments with their text as the label. This suits a fixed set of choices, such as
export formats:

```xml
<arc:ButtonGroup Label="Export format" ItemsSource="{Binding ExportFormats}" />
```

### Anatomy

1. **Frame**: a 1 px border with the control radius on the surface colour. The group's `Label` names it
   for assistive technology and is not drawn.
2. **Segments**: 42 px high and at least 42 px wide inside the border, with 14 px inline padding. The icon and the label
   are 8 px apart. A `Small` group uses 34 px segments, 12 px padding and a 6 px gap.
3. **Dividers**: 1 px hairlines between neighbouring segments, inset 11 px from the top and bottom (9 px when `Small`). In
   a `Solid` group they are a mix of the page colour into the foreground.
4. **Highlight**: a surface-muted rounded rectangle, 3 px inside its segment with a 14 px corner radius. It glides to the
   pointer or to keyboard focus and deepens while a segment is pressed.
5. **Chevron segment**: 38 px wide and 42 px high, with a 16 px chevron. It appears only when `Menu` is
   set, and it turns over while the menu is open. A `Small` group uses 30 by 34 px.
6. **Status region**: a hidden polite region that announces a changed label. It has no visible text.

### Icons and icon-only segments

Set `Icon` to a 24 by 24 Lucide geometry, such as `ArcIcons.Share2`. The icon draws at 16 px beside the label. `IsIconOnly`
hides the label, which then becomes the tooltip and the accessible name, so an icon-only segment still has a clear name.

```xml
<arc:ButtonGroup Label="Zoom" Size="Small">
    <arc:ButtonGroupItem Label="Zoom out" IsIconOnly="True" />
    <arc:ButtonGroupItem Label="Zoom in" IsIconOnly="True" />
</arc:ButtonGroup>
```

Use icon-only segments for glyphs that everyone recognises, such as bold, zoom in and undo. For an action whose icon is
ambiguous, keep the label visible, so the segment says what it does.

### Live value and label change

`Value` shows content in place of the label, such as a zoom level. Text values use tabular figures, so the digits do not
shift when the value changes. A control value is shown as it is. `Label` stays the accessible name.

A label can also change after a press. List the other labels in `ReserveLabels` so the segment keeps the width of the
widest one and nothing next to it moves.

<Demo name="LiveValue" />

```csharp
var zoomValue = new ButtonGroupItem { Label = "Reset zoom", Value = "100%" };
zoomValue.Click += (_, _) => zoomValue.Value = "100%";

var share = new ButtonGroupItem { Label = "Share", ReserveLabels = ["Copied"] };
share.Click += async (_, _) =>
{
    share.Label = "Copied";
    await Task.Delay(1600);
    share.Label = "Share";
};
```

A changed label is announced once when it changes within one second of the press. The quiet return to the original label
is not announced, so the person hears "Copied" once and does not hear "Share" again.

### Attached menu

`Menu` adds a chevron segment at the end of the row. It takes a `MenuSurface`, and `MenuLabel` names the chevron (default
"More actions"). The menu opens six pixels below the chevron, aligned to the group's end, with a minimum width of 208 px.
`MenuItemClicked` reports the chosen row. Menu rows do not raise `ActionInvoked`, so handle them in `MenuItemClicked`.

```xml
<arc:ButtonGroup Label="Document actions" MenuLabel="More document actions">
    <arc:ButtonGroupItem Label="Share" />
    <arc:ButtonGroupItem Label="Archive" />
    <arc:ButtonGroup.Menu>
        <arc:MenuSurface>
            <arc:MenuEntry Header="Pin" />
            <arc:MenuEntry Header="Delete" IsDestructive="True" HasSeparatorBefore="True" />
        </arc:MenuSurface>
    </arc:ButtonGroup.Menu>
</arc:ButtonGroup>
```

Put the destructive action in the menu, behind the chevron, and separate it with `HasSeparatorBefore`. A row of segments
that delete things invites a slip; the menu adds one step without hiding the action.

### Variants, orientation and events

`Variant` is `Outline` by default: a bordered surface. `Variant="Solid"` uses the primary fill for the main actions of a
screen, and it is best used at most once per screen. `Orientation="Vertical"` stacks the segments in a column; the menu
then aligns to the start edge. `Size` is `Small` (36 px group, 34 px segments) or `Medium` (44 px, the default). `Large` is
treated as `Medium`.

`ActionInvoked` runs after a segment's own click handler and reports the segment in `Item` and the source object in
`Data`. For inline segments `Data` is the segment itself; for `ItemsSource` it is the data object.

<Demo name="Solid" />

<Demo name="Vertical" />

```csharp
document.ActionInvoked += (_, e) => log.Text = $"Action: {e.Item.Label}";
```

### Collapsing labels

When a horizontal row does not fit the width it is given, segments that have an icon drop their label and keep the icon.
The label moves to the tooltip and the accessible name. The labels come back once the width is enough for the full row
again, and the group remembers the width that forced the collapse, so it does not flicker between the two states. Segments
without an icon keep their labels. Vertical groups do not collapse.

If the row still does not fit after collapsing, it scrolls sideways with a hidden scroll bar. Set `IsLabelCollapseEnabled`
to `False` to keep the labels and accept the scroll instead.

<Demo name="CollapsingLabels" />

### Disabled

A segment with `IsDisabled="True"` stays focusable and reports itself as unavailable, so a keyboard user can find it and
hear that it is not available. Activation is swallowed and its content dims to 40 percent. Hovering over a disabled
segment shows no highlight. Set `IsEnabled` to `False` on the group to dim it as a whole and remove all its stops.

<Demo name="Disabled" />

### Binding to a view model

A button group has commands and sometimes an item list, so the view model owns the work and the group shows it. The
example below uses CommunityToolkit.Mvvm and an app service that performs the document actions.

```csharp
public interface IDocumentService
{
    Task OpenShareDialogAsync(string documentId);
    Task DuplicateAsync(string documentId);
    Task ArchiveAsync(string documentId);
}

public partial class DocumentViewModel : ObservableObject
{
    private readonly IDocumentService _documents;

    public DocumentViewModel(IDocumentService documents)
    {
        _documents = documents;
    }

    [ObservableProperty]
    private bool _isArchived;

    [RelayCommand]
    private Task ShareAsync() => _documents.OpenShareDialogAsync("doc_2026_0931");

    [RelayCommand]
    private Task DuplicateAsync() => _documents.DuplicateAsync("doc_2026_0931");

    [RelayCommand]
    private Task ArchiveAsync() => _documents.ArchiveAsync("doc_2026_0931");
}
```

```xml
<arc:ButtonGroup Label="Document actions" MenuLabel="More document actions">
    <arc:ButtonGroupItem Label="Share"
                         Icon="{x:Static arc:ArcIcons.Share2}"
                         Command="{Binding ShareCommand}" />
    <arc:ButtonGroupItem Label="Duplicate"
                         Icon="{x:Static arc:ArcIcons.Copy}"
                         Command="{Binding DuplicateCommand}" />
    <arc:ButtonGroupItem Label="Archive"
                         Icon="{x:Static arc:ArcIcons.Archive}"
                         Command="{Binding ArchiveCommand}"
                         IsDisabled="{Binding IsArchived}" />
</arc:ButtonGroup>
```

Every binding is one way. `Label`, `Icon`, `Command` and `IsDisabled` come from the view model, and the group writes
nothing back, because a button group has no selection to report. Bind `ItemsSource` one way too. `ActionInvoked` and
`MenuItemClicked` are the places to run code that belongs to the view, such as logging or closing a flyout. Compiled
bindings need `x:DataType` on the view; the examples leave it out for brevity.

### Example: document header in a file viewer

The header of a board deck in a document viewer. The title and the last edit line identify the document, and the group
on the right holds the three actions the owner uses most: Share, Duplicate and Archive. Pin and Delete live in the
chevron menu, so the header stays short and the destructive action is one step away from the common ones. Share has an
icon and a label because it is the most used action on the page, and the group is centred against the title block, so
the actions read as actions on this document rather than on the page.

![A document header with a title and a three-segment document actions group with a chevron menu](images/DocumentHeader.png)

```xml
<Grid Width="680" ColumnDefinitions="*,Auto">
    <StackPanel Spacing="4" VerticalAlignment="Center">
        <TextBlock Text="Q2 board deck"
                   FontSize="20"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Edited by Priya Nair, 12 minutes ago"
                   FontSize="13"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </StackPanel>
    <arc:ButtonGroup Grid.Column="1" Label="Document actions" MenuLabel="More document actions">
        <arc:ButtonGroupItem Label="Share" Icon="{x:Static arc:ArcIcons.Share2}" />
        <arc:ButtonGroupItem Label="Duplicate" Icon="{x:Static arc:ArcIcons.Copy}" />
        <arc:ButtonGroupItem Label="Archive" Icon="{x:Static arc:ArcIcons.Archive}" />
        <arc:ButtonGroup.Menu>
            <arc:MenuSurface>
                <arc:MenuEntry Header="Pin" Icon="{x:Static arc:ArcIcons.Bookmark}" />
                <arc:MenuEntry Header="Delete"
                               Icon="{x:Static arc:ArcIcons.Trash2}"
                               IsDestructive="True"
                               HasSeparatorBefore="True" />
            </arc:MenuSurface>
        </arc:ButtonGroup.Menu>
    </arc:ButtonGroup>
</Grid>
```

### Content guidelines

- **Segment label**: a short verb or noun for the action: "Share", "Duplicate", "Zoom in". Two words at most, in sentence
  case.
- **Group label**: a noun phrase that names the object or the set, such as "Document actions" or "Zoom". Assistive
  technology reads it first, so it should say what the group acts on.
- **Menu label**: name the scope of the menu: "More document actions", not "More".
- **Result word**: a single word for the confirmation, such as "Copied". It appears for a moment and then returns to the
  original label.
- **Live value**: the value alone, with its unit: "100%", not "Zoom is 100%".

| Instead of | Write |
|---|---|
| Click to share | Share |
| Actions | Document actions |
| More | More document actions |
| Zoom is 100% | 100% |
| Copy link to clipboard | Copy link |

### Best practices

**Do**

- Keep two to five segments in a group. More segments collapse to icons or scroll, and the person can no longer see the
  whole set at once.
- Give every group a `Label`, because it is the accessible name and is not drawn on screen.
- Give every icon-only segment a clear `Label`, because the label becomes its tooltip and its accessible name.
- Use `ReserveLabels` for a segment whose label changes, so the group does not resize when the label does.
- Move rare and destructive actions into the chevron menu, and keep the common actions in the row.

**Avoid**

- Using a button group as a selection control. A pressed segment does not stay selected, so people cannot tell which
  view is active. Use `SegmentedControl` for that.
- Using `Solid` on more than one group per screen. The primary fill is meant for the main actions of a screen, and several
  filled groups compete with each other and with the primary buttons.
- Relying on label collapse for a layout that must show the words. The labels collapse as soon as the row is narrower
  than its full width, so size the container for the labels you need.
- Putting a destructive action in the row beside common ones. A single misdirected press then deletes data.
- Placing a vertical group in a narrow column with long labels. Vertical groups never collapse, so the labels must fit
  the column as they are.

### Common questions

**Why do my labels disappear?** The row is narrower than its full width, so segments that have an icon collapsed to their
icons. Give the container more width, shorten the labels, or set `IsLabelCollapseEnabled` to `False` to keep the labels and
let the row scroll. Segments without an icon never collapse.

**How do I show a result on a segment, such as Copied, without moving the row?** Change `Label` in the click handler and
list the result word in `ReserveLabels`. The segment keeps the width of the widest label, and the change is announced once.

**Does a menu row raise `ActionInvoked`?** No. `ActionInvoked` is raised for segments only. Handle `MenuItemClicked` for
the rows of the chevron menu, and use `MenuEntryClickedEventArgs.Header` to tell them apart.

**Can a segment open a link instead of running code?** Yes. Set `NavigateUri` and the segment opens that address with the
platform launcher. It reports itself as a hyperlink to assistive technology.

**How do I disable one action but keep the others?** Set `IsDisabled` on that segment. The disabled segment stays focusable
and dims, so the person can still find it and learn that it is unavailable. Set `IsEnabled` on the group only when every
action must stop.

## API reference

<!-- api: ButtonGroup -->

Companion type used on this page:

| `ButtonGroupItem` property | Meaning |
|---|---|
| `Label` | The visible text and the accessible name. Becomes the tooltip when the segment is icon-only or collapsed. |
| `Icon` | A 24 by 24 Lucide geometry, drawn at 16 px. |
| `IsIconOnly` | Hides the label; the label becomes the name and the tooltip. |
| `Value` | Visible content in place of the label, such as a zoom level. |
| `ReserveLabels` | Other labels the segment can show; it keeps the width of the widest one. |
| `NavigateUri` | Opens the address with the platform launcher, and reports the segment as a hyperlink. |
| `IsDisabled` | Unavailable but focusable: activation is swallowed and the content dims. |

## Accessibility

- The group has the Group role and is named by `Label`.
- Every segment is a Tab stop, including a disabled one. Arrow keys move between segments along the orientation and wrap
  around. Home and End move to the first and last segment.
- Enter or Space activates a segment. On the menu segment, Enter, Space or ArrowDown opens the menu.
- Icon-only and collapsed segments use their label as the name and tooltip, and their icons are hidden.
- A label that changes shortly after a press is announced once. The state is never shown by colour alone.
- A disabled segment reports itself as unavailable, so the person hears that it exists and cannot be used.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Moves focus into the group and out of it. Every segment and the chevron is a stop. |
| `Right` / `Left` (`Down` / `Up` in a vertical group) | Moves to the next or previous segment and wraps at the ends. Mirrored in right-to-left layouts. |
| `Home` / `End` | Moves to the first or the last segment. |
| `Enter` / `Space` | Activates the focused segment. On the chevron, opens the menu. |
| `ArrowDown` on the chevron | Opens the menu and highlights its first row. |
| `Up` / `Down` in the menu | Moves between rows, wrapping at the ends and skipping disabled rows. |
| `Escape` in the menu | Closes the menu and returns focus to the chevron. |

## Motion

- One highlight glides to the pointer or the keyboard focus. The dividers beside it fade out as it passes.
- A pressed segment dips its content slightly and springs back. The group frame and its neighbours do not move.
- A segment whose label changes resizes on a spring, or crossfades when its width is reserved.
- The menu grows out of the group's edge and the chevron turns while it is open.
- Reduced motion jumps the highlight to its target, removes the press dip and the chevron turn, and crossfades the labels
  and the menu. Off moves everything to its final state at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Highlight moves to the pointer or focus | Glides on a snappy spring; fades in where it appears from outside the group | Jumps to the target | Immediate |
| Segment pressed | Content dips to 97 percent (90 for icon-only) and springs back; the highlight deepens at once | No dip; the highlight deepens | No dip |
| Label changes on a reserved segment | Crossfades in a fixed box | Crossfade | Immediate |
| Label changes on an unreserved segment | The segment width springs to the new label | Crossfade | Immediate |
| Menu opens | Grows from the group's edge with a short shift; the chevron turns | Crossfades; the chevron does not turn | Immediate |

## Related

- `SegmentedControl` for a choice that stays selected.
- `SplitButton` for one default action with a menu of alternatives.
- `FloatingButtonGroup` for separate soft buttons in a tray.
- `ExpandingButtonGroup` for compact icon buttons whose labels appear on focus.

| Component | Choose it when |
|---|---|
| `SegmentedControl` | The choice stays selected, such as a view mode |
| `SplitButton` | One default action is more important than its nearby alternatives |
| `Button` | A single action stands alone on the surface |
| `Tabs` | The segments switch between panels of content |
| `FloatingButtonGroup` | The actions are separate floating buttons rather than one joined surface |
| `ExpandingButtonGroup` | Compact icon buttons should reveal their labels on focus |
| `DropdownMenu` | The actions form a long list that does not fit in a row |
