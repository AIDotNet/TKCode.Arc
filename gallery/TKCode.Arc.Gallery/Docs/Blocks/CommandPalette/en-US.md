---
title: Command palette
summary: A complete keyboard driven action surface with search, grouped results, and shortcuts.
---

<Demo name="Preview" />

## When to use

- Product-wide actions and navigation that people reach from the keyboard.
- Apps with many commands spread over menus and pages, where typing a word is faster than browsing.
- A launcher that also teaches the keyboard shortcut of each command.

## When not to use

- Searching documents or records with snippets and filters: use `SearchResults`.
- A search field in a header that shows a few matches: use `ExpandingSearch`.
- Choosing a value for a form: use `Combobox`.
- A few actions on one object: use `DropdownMenu`.

## Usage

The palette is a search field over a grouped list of commands. Add `CommandPaletteItem` children or set
`ItemsSource`. Typing filters the list, and the arrow keys and Enter run a command without the mouse.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:CommandPalette MaxWidth="560">
        <arc:CommandPaletteItem Id="dashboard" Label="Go to dashboard" Group="Navigation" Shortcut="G D" />
        <arc:CommandPaletteItem Id="new-project" Label="Create project" Description="Start from a blank board" Group="Actions" />
        <arc:CommandPaletteItem Id="theme" Label="Switch theme" Group="Settings" />
    </arc:CommandPalette>
</UserControl>
```

### Commands

Each `CommandPaletteItem` has a `Label` and an optional `Description`, `Icon`, `Shortcut` and `Group`. Commands are
listed under their group, in the order the groups first appear. A command without a group goes under `DefaultGroup`
("Actions"). `Icon` takes a 24 by 24 Lucide geometry such as an `ArcIcons` glyph, or any control; the default is the
Lucide Command glyph. Set `IsEnabled` to false to show a command that cannot run right now.

The search looks at the label, the description, the group and `Keywords`, without regard to case. Give each command a
stable `Id` so its row keeps its place when the list changes.

When the palette is 480 pixels wide or less, it hides the shortcut caps and tightens its padding. This one is 400 pixels
wide.

<Demo name="InlineItems" />

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:generic="using:System.Collections.Generic"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:CommandPalette>
        <arc:CommandPaletteItem Id="theme" Label="Switch theme" Group="Settings" Shortcut="T">
            <arc:CommandPaletteItem.Keywords>
                <generic:List x:TypeArguments="x:String">
                    <x:String>dark mode</x:String>
                    <x:String>appearance</x:String>
                </generic:List>
            </arc:CommandPaletteItem.Keywords>
        </arc:CommandPaletteItem>
    </arc:CommandPalette>
</UserControl>
```

### Running a command

Enter runs the highlighted command. After typing, with nothing highlighted, Enter runs the top match. A click runs the
command under the pointer. The palette then clears the query, keeps the focus in the field and raises `ItemSelected`.
It also runs `SelectCommand` with the chosen item, and the item's own `Command` with its `CommandParameter`.

```csharp
palette.ItemSelected += (_, e) => Navigate(e.Item.Id);
```

### Data binding

Bind `ItemsSource` to a collection of `CommandPaletteItem` objects. The palette follows changes to the collection and
to the items. Rows that stay move to their new place, new rows fade in, and the frame changes height. Bind
`SelectCommand` to run a view model command with the chosen item.

<Demo name="DataBinding" />

```xml
<arc:CommandPalette ItemsSource="{Binding Commands}"
                    SelectCommand="{Binding RunCommand}" />
```

### Opening on demand

To open the palette over the page, put it in an overlay. Set `AutoFocus` so the field takes the focus when the palette
appears, and `ShowCloseButton` to show the "Esc" button. Escape in an empty field and the "Esc" button raise
`CloseRequested`; close the overlay there, and after `ItemSelected`. This demo uses the Core `ModalPresence`, which adds
the scrim, keeps the focus inside and returns it to the button that opened it. The palette brings its own entrance, so
the modal only fades.

<Demo name="OnDemand" />

```csharp
var palette = new CommandPalette { ItemsSource = commands, AutoFocus = true, ShowCloseButton = true, MaxWidth = 560 };
var modal = new ModalPresence(palette, anchor: this) { Entrance = ModalEntrance.Fade };
palette.CloseRequested += (_, _) => modal.Close();
palette.ItemSelected += (_, e) => modal.Close(DismissReason.Action);
modal.Open();
```

### Keyboard shortcut

Cmd+K on macOS, or Ctrl+K elsewhere, moves the focus to the field from anywhere in the window. The palette handles the
key before the window does, so it wins over other Cmd/Ctrl+K handlers while it is on screen. When a page shows more
than one palette, set `IsShortcutEnabled` to false on all but one. `ShortcutHint` changes the key cap at the end of the
search row.

```xml
<arc:CommandPalette IsShortcutEnabled="False" />
<arc:CommandPalette ShortcutHint="Ctrl K" />
```

### Text

All text has an English default. `PlaceholderText` is the hint in the empty field and `Label` names the field.
`EmptyTitle` and `EmptyDescription` fill the empty state. `ClearButtonLabel`, `CloseButtonLabel` and `ResultsLabel`
name the clear button, the close button and the list for assistive technology.

```xml
<arc:CommandPalette PlaceholderText="Jump to a page"
                    EmptyTitle="No pages found"
                    EmptyDescription="Check the spelling or try another word." />
```

## API reference

<!-- api: CommandPalette -->

<!-- api: CommandPaletteItem -->

## Accessibility

- The search field is named by `Label`. The result list is named by `ResultsLabel`, and each row is named by its
  label and description.
- The highlighted row reports itself as selected, and the field's help text is its label.
- ArrowDown and ArrowUp move the highlight and stop at the first and last command. Home and End jump to them. Disabled
  commands are skipped.
- Enter runs the highlighted command, or the top match after typing. Escape clears the query, and in an empty field it
  raises `CloseRequested`.
- Cmd/Ctrl+K focuses the field. The clear and "Esc" buttons can be reached with Tab and show a focus ring.
- The empty state is plain text, so screen readers read it.

## Motion

- The palette enters with a fade and rises 6 pixels from a slightly smaller scale.
- One highlight moves between rows: a short slide for the arrow keys and a quick spring for the pointer. It fades in
  where it first appears and fades out when nothing is highlighted.
- While you type, rows that stay glide to their new place, rows that leave fade out, and new rows fade in. The frame
  height follows the list on a smooth spring. When rows would jump more than six places, they move at once.
- The results fade out before the empty state rises in, so the frame changes height once.
- The clear button pops in from a smaller scale out of a soft blur.
- Reduced motion keeps only the fades. Off shows every change at once.

## Related

- `SearchResults` for a full search page with filters and snippets.
- `ExpandingSearch` for a compact search field in a header.
- `DropdownMenu` for the actions of one object.
- `Combobox` for choosing a value in a form.
