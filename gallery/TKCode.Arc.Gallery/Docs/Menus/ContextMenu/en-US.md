---
title: Context menu
summary: Secondary actions kept close to the selected object.
---

<Demo name="FileTile" />

## When to use

A context menu keeps the secondary actions for one object next to that object. A file tile, a dashboard card or a
row in a table shows its content, and the actions appear at the pointer or under the target when someone asks for
them. The primary actions stay in the page's toolbar; the context menu only holds what people do to one item at a
time.

- **Files and assets in a shared folder**: Copy link, Rename, Move to folder and Move to trash on a file tile in a
  workspace library. The people who manage files learn the gesture quickly, and the toolbar stays for upload and
  new folder.
- **Cards in an analytics dashboard**: Duplicate chart, Export CSV and Remove from dashboard on a metric card such as
  Revenue by region. A card is not a button, so a menu adds actions without a visible menu button on every card.
- **Records in a billing or order console**: Copy invoice ID, Resend receipt and Void invoice on a row of an invoice
  list. The row keeps its click target for opening the record, and the menu is for the less common actions.
- **Developer and log tools**: Copy value, Copy path and Filter by this key on a node of a JSON or log viewer, where
  the object is the data and the actions are about the data.
- **Short, flat lists with one destructive row**: a few actions on one object, with the destructive row last and set
  apart by a separator.

## When not to use

- **Actions that people must find without a right-click or a tap**: use `DropdownMenu` with a visible trigger. A menu
  that opens on a gesture is easy to miss, and keyboard and screen reader users need a labelled control to start from.
- **Touch-first lists that reveal actions by a swipe**: use `SwipeActions`, which matches the swipe that phone users
  expect on a row.
- **A target that needs its own click**: use a `Button` beside it. A plain click or tap on the wrapped content also
  opens the menu, so a card that opens its record on click should not be wrapped in a `ContextMenu`.
- **A default action with a few alternatives**: use `SplitButton`. It keeps the default action one click away and
  puts the alternatives in its menu.
- **A list of choices that feeds a form**: use `Select`, `Combobox` or `RadioGroup`. They carry a value, a label and
  a validation state, and a menu does not.
- **Account settings and the sign-out action**: use `UserMenu`, which sits behind the avatar in the top bar.

## Usage

`ContextMenu` derives from Avalonia's `ContentControl`. The content is the target: any single element, which keeps its
own look and its own layout. The control adds a menu surface (a `MenuSurface` in the Context variant), the row
collection `Items`, and the wiring that opens the menu on a right-click, on the ContextMenu key, on Shift+F10 and on a
plain click or tap. Because it is a `ContentControl`, the usual content properties (`Content`, `ContentTemplate`,
`Padding`) apply to the target.

Wrap the target in `ContextMenu` and set its `Label`. The content is the target. Add the rows from code to `Items`. In
the snippet below, `file` is the `ContextMenu`.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ContextMenu Label="Actions for Q3 report.pdf">
        <Border Width="220" Height="120" Padding="16" CornerRadius="18" BorderThickness="1">
            <TextBlock Text="Q3 report.pdf" />
        </Border>
    </arc:ContextMenu>
</UserControl>
```

```csharp
file.Items.Add(new MenuEntry { Header = "Copy link", Icon = ArcIcons.Copy });
file.Items.Add(new MenuEntry { Header = "Delete file", Icon = ArcIcons.Trash2, IsDestructive = true });
```

The same rows can be declared in XAML under `<arc:ContextMenu.Items>`, which is how the demo on this page is written.

### Anatomy

1. **Target** (`Content`): the element the menu belongs to. The theme sets its background to transparent and removes
   the focus adorner, so the menu is the only response to a press. Give the target its own visible shape (a border,
   a background and a corner radius), as the demo does, so people see what they act on.
2. **Menu panel** (`Menu`): the surface that holds the rows, announced with the `Label`. In the Context variant, rows
   are 34 px tall, the panel is at least 208 px (13 rem) wide, and it is never taller than 300 px; a longer list
   scrolls inside the panel.
3. **Icon slot** (`Icon` on a `MenuEntry`): a 24 by 24 Lucide geometry drawn as a 16 px stroke glyph. A row without an
   icon shows a check when `IsChecked` is true and a quiet ellipsis when it is not, so every row keeps the same
   left edge.
4. **Label** (`Header`): the row text. Long labels end in an ellipsis instead of wrapping. The header is also the
   text that typeahead matches.
5. **Shortcut hint** (`InputGestureText` or `InputGesture`): quiet text at the end of the row. It is display only; the
   app binds the key itself.
6. **Destructive row** (`IsDestructive`): the text and icon take the danger color, and the highlight is danger-tinted
   when the row is highlighted.
7. **Separator and group label** (`HasSeparatorBefore`, `MenuEntrySeparator`, `MenuEntryLabel`): a divider line above
   a row, or a non-selectable heading that groups the rows below it.
8. **Highlight**: one shared highlight covers the row under the pointer or the keyboard position. Rows do not draw
   their own hover fill.

### Handling a selection

Activating a row raises the row's `Click` event, runs its `Command`, raises the menu's `ItemClicked` and then closes
the menu, unless the row sets `StaysOpenOnClick`. `ItemClicked` reports the activated row in `Entry`, the data item
behind it in `Item`, and its `Header`. The menu's own `Opened` and `Closed` events fire around the open and close
motion.

```csharp
file.ItemClicked += (_, e) => status.Text = $"Q3 report.pdf: {e.Header}";
```

Use `ItemClicked` when one handler serves every row, and the row's `Click` event when one row needs its own work.
Keep the handler short. The menu closes after it returns, so long work belongs in a command or an asynchronous
method that the handler starts.

### Checked rows

A row without an icon shows a check when `IsChecked` is true. Set `ToggleType="CheckBox"` when the row is a switch:
activation flips `IsChecked` for you and the row shows a check. The row in the example below is a plain row that
the handler toggles itself.

```csharp
var pinned = new MenuEntry { Header = "Pinned", IsChecked = true };
pinned.Click += (_, _) => pinned.IsChecked = !pinned.IsChecked;
image.Items.Add(pinned);
```

Use either the handler or `ToggleType`, not both, or the value flips twice. A toggle row that should stay open while
several options are changed sets `StaysOpenOnClick="True"`.

### Data-driven rows

`ItemsSource` creates one row per item. `MenuEntry` instances are used as they are; other data needs an
`ItemTemplate`, which renders the header of each generated row. Bind the collection on the `ContextMenu` itself, which
sits in the page's tree, and read the data item in `ItemClicked`.

```xml
<arc:ContextMenu Label="File actions" ItemsSource="{Binding FileActions}">
    <arc:ContextMenu.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Name}" />
        </DataTemplate>
    </arc:ContextMenu.ItemTemplate>
    <Border Width="220" Height="96" CornerRadius="18" BorderThickness="1" />
</arc:ContextMenu>
```

### Attaching a menu to any control

`arc:ContextMenu.Menu` gives any control a right-click menu without wrapping it. The attached surface becomes a
Context menu unless its `Variant` was set, and it closes on scroll or resize unless `ClosesOnViewportChange` was set.
Use this form when the element already has a template or a parent that you do not want to change.

<Demo name="Attached" />

```xml
<Border Width="220" Height="96" CornerRadius="18" BorderThickness="1">
    <arc:ContextMenu.Menu>
        <arc:MenuSurface>
            <arc:MenuEntry Header="New folder" Icon="{x:Static arc:ArcIcons.Folder}" />
            <arc:MenuEntry Header="Paste" Icon="{x:Static arc:ArcIcons.Copy}" InputGestureText="⌘V" />
            <arc:MenuEntry Header="Refresh" Icon="{x:Static arc:ArcIcons.RefreshCw}" />
        </arc:MenuSurface>
    </arc:ContextMenu.Menu>
    <TextBlock Text="Right-click anywhere here" HorizontalAlignment="Center" VerticalAlignment="Center" />
</Border>
```

### Submenus

Nest `MenuEntry` elements inside a row to give it a submenu, such as New or Sort by. A row with a submenu shows a
chevron and opens it beside the menu on a click, on ArrowRight, Enter or Space, or when the pointer rests on it.
Choosing a row in a submenu closes the whole menu, and `ItemClicked` reports it once. For `ItemsSource` data, set
`ChildrenSelector` to return the children of an item.

<Demo name="Submenus" />

```xml
<arc:MenuSurface>
    <arc:MenuEntry Header="New">
        <arc:MenuEntry Header="Folder" />
        <arc:MenuEntry Header="Document" />
    </arc:MenuEntry>
    <arc:MenuEntry Header="Sort by">
        <arc:MenuEntry Header="Name" ToggleType="Radio" GroupName="sort" IsChecked="True" />
        <arc:MenuEntry Header="Date modified" ToggleType="Radio" GroupName="sort" />
    </arc:MenuEntry>
</arc:MenuSurface>
```

### Opening and closing from code

`Open` shows the menu under the target. Pass a point in target coordinates to open it at a position, for example at
the place the person clicked in a canvas. `Close` closes it without moving focus. `IsOpen` is two-way, so a view model
can open the menu by setting it.

```csharp
file.Open(); // under the target
file.Open(new Point(120, 40), MenuOpenFocus.First); // at a point, with the first row highlighted
file.Close();
```

The menu opens from `IsOpen` only once the target is attached to a window. Setting it to `True` before that does
nothing visible, so set it after the page loads.

### Binding to a view model

A context menu usually acts on an item of a collection, so the view model owns the items and the commands, and the
handler routes the activated row to the command. The menu shows the items; it does not run them.

```csharp
public sealed record FileAction(string Name, bool IsDestructive);

public partial class FileListViewModel : ObservableObject
{
    public ObservableCollection<FileAction> FileActions { get; } =
    [
        new FileAction("Copy link", IsDestructive: false),
        new FileAction("Move to trash", IsDestructive: true)
    ];

    [RelayCommand]
    private void RunFileAction(FileAction action)
    {
        // Run the action for the file that this menu belongs to.
    }
}
```

```csharp
file.ItemClicked += (_, e) =>
{
    if (e.Item is FileAction action)
    {
        viewModel.RunFileActionCommand.Execute(action);
    }
};
```

Bind `ItemsSource` in XAML (see Data-driven rows) and route the activated item through `ItemClicked`, as above. For a
row declared inline that needs a command, set `Command` on the `MenuEntry` in code when you create it. The view model
uses `ObservableObject` and `[RelayCommand]` from CommunityToolkit.Mvvm and holds no UI types.

### Focus and dismissal

The menu opens where the person acted. A right-click opens it at the pointer. A click or tap on the target, and the
ContextMenu key or Shift+F10 on a focused target, open it under the target, 12 px in from its left edge and 8 px
below its bottom edge. A pointer opens the menu with focus on the surface and no highlighted row. The keyboard opens
put the highlight on the first row.

The menu closes rather than follows. It closes on an outside press, on any scroll of an ancestor scroll viewer, on a
window resize, and when the target leaves the visual tree. A primary press on the target while the menu is open
closes it, and the click that follows does not reopen it. Escape and Tab close the menu and return focus to the target.
When the menu is near a window edge, Avalonia's placement flips it to the other side.

### Example: revenue dashboard card

A dashboard card carries four actions, and the destructive one is last behind a separator. The card keeps its own
border and its title, so the menu stays on the object and the page's toolbar keeps the page-level actions. The label
names the chart, because a screen reader announces only the target's name. The Pin to home row is a toggle, so its
checked state is announced to assistive technology. The shortcut hint is display only; the app must bind the key
itself.

```xml
<arc:ContextMenu Label="Actions for Revenue by region">
    <arc:ContextMenu.Items>
        <arc:MenuEntry Header="Duplicate chart"
                       Icon="{x:Static arc:ArcIcons.Copy}"
                       InputGestureText="⌘D" />
        <arc:MenuEntry Header="Export CSV" Icon="{x:Static arc:ArcIcons.Download}" />
        <arc:MenuEntry Header="Pin to home"
                       ToggleType="CheckBox"
                       IsChecked="True" />
        <arc:MenuEntry Header="Remove from dashboard"
                       Icon="{x:Static arc:ArcIcons.Trash2}"
                       IsDestructive="True"
                       HasSeparatorBefore="True" />
    </arc:ContextMenu.Items>
    <Border Width="300"
            Padding="16"
            CornerRadius="16"
            BorderThickness="1"
            Background="{arc:ArcDesignTokenResource SurfaceMuted}"
            BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}">
        <StackPanel Spacing="6">
            <TextBlock Text="Revenue by region"
                       FontSize="14"
                       FontWeight="Medium"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="$1.84M"
                       FontSize="26"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="+12.6% against Q2 2026"
                       FontSize="12"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
    </Border>
</arc:ContextMenu>
```

### Content guidelines

Rows are short commands, and the menu is read at a glance, so write each row as a verb and its object. Keep the
`Label` specific, because it is the only name a screen reader gives the target.

- Write row labels in sentence case, with a verb first: "Copy link", not "Link".
- Name the object in a destructive row, so the person knows what goes: "Move to trash" for a file, "Remove from
  dashboard" for a card.
- Keep a row under about 24 characters. A longer label ends in an ellipsis, so the important words go first.
- Do not end a row with a colon or a full stop, and do not put a shortcut in the label text; use `InputGestureText`.
- Use the product's own words for the action, such as "Void invoice" when the product voids invoices.

| Instead of | Write |
|---|---|
| Link | Copy link |
| Delete | Delete file |
| Actions | Actions for Q3 report.pdf |
| Remove | Move to trash |
| Click here to export | Export CSV |
| Duplicate (Ctrl+D) | Duplicate chart, with the shortcut in `InputGestureText` |

### Best practices

**Do**

- Keep the list to three to seven rows. A longer list belongs in a `DropdownMenu` on a visible trigger or on a page.
- Give the target a visible shape, such as a border, a background or a corner radius. The target has no outline of
  its own, so the menu is the only feedback and people need to see what they act on.
- Put the destructive row last and set `HasSeparatorBefore="True"`, so it is not hit by accident.
- Offer the important actions from a visible control as well. A right-click, a tap on the target and Shift+F10 are not
  discoverable, and touch and keyboard users need a labelled way in.
- Name the target with `Label` after the object, so a screen reader announces what the actions belong to.

**Avoid**

- Wrapping a target that needs its own click. A plain click or tap also opens the menu, so the target's action and the
  menu compete for the same press. Set `OpensOnClick` to `False` when the target only needs the right-click menu.
- Long labels that rely on wrapping. Rows end long labels in an ellipsis, so the meaning can be cut off.
- Marking a destructive row by color alone. The row is marked by its danger color only, so its `Header` must say what
  will be removed.
- Expecting the menu to follow the target while the page scrolls. It closes on scroll, and the person reopens it.
- Putting the only route to an action in the menu. Keep one visible route for the actions that matter most.

### Common questions

**How do I stop a left click from opening the menu?** `OpensOnClick` controls the plain click and tap. Set it to
`False` on the `ContextMenu` to keep the right-click, the ContextMenu key and Shift+F10. The default is `True`.

**Avalonia also has a `ContextMenu` type. How do I tell them apart?** Both namespaces define a type called
`ContextMenu`. When a file uses both `Avalonia.Controls` and `TKCode.Arc`, add an alias
such as `using ArcContextMenu = TKCode.Arc.ContextMenu;` and use the alias in code. In XAML the `arc:` prefix already
selects the Arc type.

**How do I give a control a menu without wrapping it?** Set `arc:ContextMenu.Menu` to a `MenuSurface` on the control
(see Attaching a menu to any control). The surface takes the Context variant unless you set its variant yourself.

**Can I open the menu at the place the person clicked?** Yes. Call `Open` with a point in the target's coordinates, for
example `file.Open(e.GetPosition(file))` from a pointer handler. Without a point, the menu opens under the target.

**Why does the menu show a check on one row but not the others?** A row without an icon shows a check when `IsChecked`
is true, and an ellipsis when it is not, so the rows keep one left edge. A row with an icon shows the icon instead.
Set `ToggleType="CheckBox"` on a row whose state should flip on activation.

## API reference

<!-- api: ContextMenu -->

<!-- api: MenuEntry -->

## Accessibility

- The target is focusable and the menu is announced with the `Label`, which names both the target and the menu.
- The target exposes a menu that can be expanded and collapsed, so assistive technology reports whether it is open.
- Rows have the menu item role. Enter and Space choose a row, and Escape closes the menu and returns focus to the
  target.
- Shift+F10 and the ContextMenu key open the menu under a focused target, with the first row highlighted.
- A checked row without an icon shows a check, but on its own the checked state is not announced. Rows with
  `ToggleType="CheckBox"` or `ToggleType="Radio"` expose their state to assistive technology, so use a toggle type
  for a row whose state matters, or put the state in the `Header`.
- Destructive rows are marked by color only, so name what will be removed in the `Header`.
- Keep the `Label` specific to the object, such as "Actions for Q3 report.pdf". It is the only name a screen reader
  gives the target.
- A row with a submenu shows a chevron and reports whether its submenu is expanded; assistive technology can
  expand and collapse it.

### Keyboard

A focused target opens the menu from the keyboard. Once the menu is open, the rows follow the menu keyboard model.

| Keys | Action |
|---|---|
| ContextMenu key, `Shift+F10` | Opens the menu under the focused target, with the first row highlighted |
| `ArrowDown` / `ArrowUp` | Moves the highlight to the next or previous row; the list loops and skips disabled rows |
| `Home` / `End` | Moves the highlight to the first or last row |
| Letters | Typeahead moves to a row whose text starts with the typed letters |
| `Enter` / `Space` | Activates the highlighted row, then closes the menu unless the row stays open |
| `Escape` / `Tab` | Closes the menu and returns focus to the target |
| `ArrowRight`, `Enter` or `Space` on a row with a submenu | Opens the submenu with its first row highlighted |
| `ArrowLeft` or `Escape` in a submenu | Closes that submenu only and returns to its row; `Tab` closes every level |

## Motion

- Right-click makes the menu grow from the pointer on a quick spring with a short fade. Rows fade in with a short stagger.
- A click, tap or Shift+F10 opens the menu under the target with the same motion.
- Scrolling or resizing closes the menu. It does not move to follow the target.
- Reduced motion removes the scale and the glide and keeps a quick fade. Off shows and hides the menu at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Right-click opens | The menu grows from the pointer, from 0.96 to full size on a snappy spring, with a 160 ms fade | Quick fade, no scale | Immediate |
| Click, tap or Shift+F10 opens | The menu grows out of the point under the target, with the same spring and fade | Quick fade | Immediate |
| Menu closes | The menu scales to 0.98 and fades in 120 ms | Quick fade | Immediate |
| Highlight moves | The pointer highlight glides on a snappy spring; the keyboard highlight jumps | The highlight jumps | Immediate |
| Rows appear | Each row fades in over 240 ms, staggered 35 ms apart up to the fifth row | Quick fade | Immediate |
| Submenu opens | 100 ms after the pointer rests on its row, the submenu grows from the side of the menu on the same snappy spring | Quick fade | Immediate |
| Row pressed | The row tints; it does not scale | The row tints | Immediate |

## Related

- `DropdownMenu` when the actions need a visible trigger that people can find.
- `SwipeActions` for touch-first rows that reveal actions by a swipe.
- `SplitButton` when one action is the default and the menu holds the alternatives.
- `MenuSurface` is the shared menu popup, for anchoring a menu to any control.

| Component | Choose it when |
|---|---|
| `DropdownMenu` | The actions need a labelled trigger that people can see and find |
| `SwipeActions` | Touch-first rows reveal their actions by a swipe |
| `SplitButton` | One action is the default and the menu holds the alternatives |
| `UserMenu` | The menu belongs to the account behind the avatar in the top bar |
| `Dialog` | A destructive row must be confirmed before anything is removed |
| `MenuSurface` | You need a menu on a control without the right-click gesture, or from code |
