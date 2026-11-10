---
title: Glass menu
summary: A menu on frosted glass whose highlight glides from row to row under the pointer, with a soft glow that follows it along the row.
---

<Demo name="Preview" />

## When to use

`GlassMenu` is a list of commands on frosted glass: the menu blurs and tints what lies behind it, so it floats over a
picture, a canvas or a colorful page without hiding it completely. One highlight of brighter glass marks the row under
the pointer or the keyboard focus. It glides from row to row instead of jumping, and a soft glow inside it follows the
pointer along the row, so the menu feels like one piece of glass catching the light. It can sit in the page or open as
a popup from a button, closing when a command runs.

- **Actions on a canvas or a photo**: arrange, group, lock or share an object in an editor.
- **A context menu over media**: save, copy or open a picture or a clip.
- **A short "more" menu** in a glass toolbar or on a glass card.

## When not to use

- **Long or nested menus**: the glass suits a handful of commands. Use `DropdownMenu` for submenus, checkable items and
  long lists.
- **Choosing a value for a form**: use `Select`; a menu runs commands.
- **Menus on plain backgrounds**: without anything to blur, use `DropdownMenu` or `ContextMenu`.

## Usage

Write the rows inline: `GlassMenuItem` for commands (an optional `Icon`, the label and an optional `Shortcut` hint) and
`GlassMenuSeparator` between groups. A `GlassMenuItem` is a `Button`, so it takes a `Command` and raises `Click`; the
menu also raises `ItemInvoked` for every command that runs.

```xml
<Panel arc:GlassBackdrop.Source="{Binding #Scene}">
    <Border Name="Scene" Background="#2B3A55" /><!-- your picture or page -->
    <arc:GlassMenu>
        <arc:GlassMenuItem Content="Duplicate" Icon="{x:Static arc:ArcIcons.Copy}" Shortcut="Ctrl+D" />
        <arc:GlassMenuSeparator />
        <arc:GlassMenuItem Content="Move to trash" Icon="{x:Static arc:ArcIcons.Trash2}" IsDestructive="True" />
    </arc:GlassMenu>
</Panel>
```

`GlassMenu` derives from Avalonia's `ItemsControl` and lives in the Special package: register it with `UseArcSpecial()`
next to `UseArc()`. The frost works as for every glass surface: see the Glass card page for how it is made with public
Avalonia APIs and when to set `arc:GlassBackdrop.IsLive`.

### As a popup

Attach the menu to a button with `arc:GlassMenu.Menu`. A click opens it below the button; Down, Enter or Space open it
with the first command focused. It closes when a command runs, on Escape (the focus goes back to the button) or on a
click outside. Call `Open(anchor)`, `Close()` or `Toggle(anchor)` to drive a menu that is not in the page from code;
`Placement` sets the side it opens on.

<Demo name="Popup" />

```xml
<arc:Button Content="Arrange">
    <arc:GlassMenu.Menu>
        <arc:GlassMenu>
            <arc:GlassMenuItem Content="Bring to front" Shortcut="Ctrl+]" />
            <arc:GlassMenuItem Content="Send to back" Shortcut="Ctrl+[" />
        </arc:GlassMenu>
    </arc:GlassMenu.Menu>
</arc:Button>
```

### Rows from data

Any items from `ItemsSource` become rows with their text. `ItemInvoked` hands back the data item in `Item` and the row in
`Container`.

<Demo name="Data" />

### Content guidelines

- Start labels with a verb and keep them short: "Duplicate", "Move to trash".
- Use icons for every row or for none, so the labels line up.
- Put destructive commands last, after a separator, and mark them with `IsDestructive`.
- Shortcut hints are only labels; register the shortcuts themselves with `HotKey` or key bindings.

## API reference

<!-- api: GlassMenu -->

<!-- api: GlassMenuItem -->

<!-- api: GlassMenuSeparator -->

<!-- api: GlassMenuItemInvokedEventArgs -->

## Accessibility

- The menu is a menu and each command a menu item named by its label; a `Shortcut` is reported as its accelerator key.
  Separators are not read as content.
- The highlight follows the keyboard focus, so it doubles as the focus indicator; disabled rows are skipped.
- Opening a popup from the keyboard focuses the first command; closing it with Escape or a command returns the focus to
  the button, with its keyboard ring.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Into the menu (first command) and out of it |
| `Down` / `Up` | Next or previous command, wrapping |
| `Home` / `End` | First or last command |
| `Enter` / `Space` | Run the focused command |
| A letter | The next command that starts with it |
| `Escape` | Close a popup menu and return to its button |
| `Down`, `Enter` or `Space` on the button | Open the menu with the first command focused |

## Motion

- The highlight glides between rows on a snappy spring (about 0.26 s, a slight settle); it fades in over 160ms where it
  first lands and fades out when the pointer and the focus leave.
- The glow follows the pointer along the row on a smooth spring and fades in over 160ms.
- A popup menu rises 4px and grows from 97% as it fades in.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Highlight to another row | Glides | Moves at once | Moves at once |
| Highlight appears or leaves | Fades | Fades | At once |
| Pointer glow and sheen | Follow the pointer | None | None |

## Responsive behavior

- The menu is at least 220px wide and as wide as its longest row; long labels end with an ellipsis when the menu is
  given less room.
- A popup menu flips to the other side of its button when there is no room below.
- On touch, a tap runs the command; there is no hover highlight.

## Related

- `DropdownMenu` and `ContextMenu` for full menus with submenus and checkable items.
- `GlassCard`, `GlassSegmented` and `GlassTabBar` for other glass surfaces.

| Component | Choose it when |
|---|---|
| `GlassMenu` | A short menu should float over a picture or a canvas |
| `DropdownMenu` | A full menu on a plain page |
| `ContextMenu` | Commands on right-click |
