---
title: Dropdown menu
summary: A focused list of actions anchored to a trigger.
---

<Demo name="Actions" />

## When to use

A dropdown menu puts a short group of commands behind a labelled trigger. The trigger is a visible control with a
name, so people can find the actions without a gesture, and the menu closes after a choice so the page stays in
place. It fits the places where an object or a view has several commands, and the commands do not deserve buttons of
their own.

- **Row actions in a data table**: Rename, Duplicate, Archive and Delete behind the more button of an invoice or
  customer row in a `Table`. Each row gets one trigger, and the destructive row sits last behind a divider.
- **Page header actions in an admin console**: Export, Share and Print for a report page. The page keeps one visible
  entry point for the secondary commands, and the primary action stays a button beside it.
- **Sort and view options in a list toolbar**: Sort by Newest, Oldest or Name as radio rows, and View options as
  check rows, on the toolbar of an order list. The trigger label shows the current choice, such as "Sort: Newest".
- **Display settings that people flip in one visit**: Show sidebar, Show minimap and Wrap lines as check rows that stay
  open, so several options change without reopening the menu.
- **Export and share in a billing or reporting page**: Export CSV, Export PDF and Send to accounting in one group,
  with the destructive or irreversible action last.

## When not to use

- **A default action with a few alternatives**: use `SplitButton`. The default runs on the main button and the menu
  holds the less common choices, so the primary action is one click away.
- **Right-click actions on a piece of content**: use `ContextMenu`, which opens at the pointer on the content itself
  and has no visible trigger.
- **The account menu behind an avatar, with identity and theme**: use `UserMenu`, which carries the profile header,
  the theme switch and sign out.
- **A value chosen from a list in a form**: use `Select` or `Combobox`. They hold the value, a label and a validation
  state, which a menu does not.
- **Options that are always visible and compared side by side**: use `SegmentedControl` for a few exclusive views, or a
  `Checkbox` group for independent options. A menu hides the options until it opens.

## Usage

`DropdownMenu` derives from Avalonia's `TemplatedControl`. Its built-in trigger is an Arc `Button` (Secondary, Small),
and its rows live in a `MenuSurface` that it creates and owns. The control adds the label morph, the chevron, the
keyboard model and the motion, and the rows are the same `MenuEntry` items that the other Arc menus use. Declare the
rows inline, and set `Label` to the text on the trigger.

Declare the rows inline. Use `MenuEntry` for a command, `MenuEntrySeparator` for a divider and `MenuEntryLabel` for a
group heading.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DropdownMenu Label="Actions">
        <arc:MenuEntry Header="Rename" />
        <arc:MenuEntry Header="Duplicate" />
        <arc:MenuEntry Header="Delete"
                       IsDestructive="True"
                       HasSeparatorBefore="True" />
    </arc:DropdownMenu>
</UserControl>
```

Set an icon from code with `ArcIcons`, a 24x24 Lucide geometry.

```csharp
var rename = (MenuEntry)menu.Items[0];
rename.Icon = ArcIcons.Pencil;
```

### Anatomy

1. **Trigger** (the built-in `arc:Button`, or the control in `Trigger`): 36 px tall, the Secondary small button. Hover
   and the open state change the border to the strong border color; the open state also uses the muted surface color.
   The trigger never scales when pressed.
2. **Leading icon** (`Icon`): a 16 px glyph in the secondary text color. It is hidden when `Icon` is null.
3. **Label** (`Label`): the trigger text, truncated with an ellipsis within its maximum width. Changing it morphs the
   trigger: the new label rises in while the trigger width springs to fit.
4. **Chevron**: 15 px with a 1.8 px stroke, in the muted text color. It turns 180 degrees while the menu is open.
5. **Menu panel**: at least 192 px wide (12 rem) and placed 6 px away from the trigger. `Alignment` sets the edge it
   lines up with; the default is `End`. `Side` sets the edge it opens from: `Bottom` by default, or `Top`, `Left`
   or `Right`. The menu flips to another side when there is no room.
6. **Rows** (`MenuEntry`): 36 px tall in the Dropdown variant. A row shows its icon, its header, an optional shortcut
   hint at the end, and a check or a dot for toggle rows.
7. **Dividers and group labels** (`HasSeparatorBefore`, `MenuEntrySeparator`, `MenuEntryLabel`): a line above a row,
   or a non-selectable heading that groups the rows below it.

Keep the built-in trigger for a standard action group. Replace it with `Trigger` when the trigger must be an icon
button, a row "more" button or a control that already exists on the page; the menu then anchors to that control.

### Choosing the trigger, side and alignment

- Use the built-in trigger with a text `Label` when the group needs a name, such as "Actions" or "Sort: Newest". The
  visible text tells a keyboard user what the menu does before it opens.
- Use `Trigger` with a ghost button and an ellipsis glyph for a row "more" menu. `Label` names only the built-in
  trigger, so set `AutomationProperties.Name` on the custom button to name the row it belongs to.
- Keep `Side` at `Bottom` for toolbars at the top of a page. Use `Top` for a control at the bottom of a window, and
  `Right` or `Left` only for a control at the edge of a sidebar.
- Keep `Alignment` at `End` for a trigger at the right of a toolbar, so the menu grows back toward the content. Use
  `Start` for a trigger at the left edge.

### Handling a selection

`ItemClicked` reports the activated row. The menu closes after the event unless the row sets `StaysOpenOnClick`.

```csharp
menu.ItemClicked += (_, e) => status.Text = $"Selected: {e.Header}";
```

Use one handler for a group that shares a purpose, and read `e.Entry` to tell the rows apart. Use the row's own `Click`
event when one row has its own behavior. The data item behind a generated row is in `e.Item`.

### Live label and radio rows

Set `Label` from code to change the trigger text. Radio rows with the same `GroupName` are mutually exclusive, and
choosing one closes the menu. Check rows (`ToggleType="CheckBox"`) with `StaysOpenOnClick="True"` stay open so
several options can be flipped in one visit.

<Demo name="LabelAndToggles" />

Choose a radio row when exactly one option applies, such as a sort order: the trigger label then states the current
choice. Choose a check row when each option is independent, such as which columns to show. Keep a radio group to
about five options; a longer choice belongs in a `Select`.

### Data-driven rows and a custom trigger

`ItemsSource` creates one row per item. `Trigger` replaces the built-in button with any control. The menu anchors to
that control, and a `Button` opens it on click.

<Demo name="CustomTrigger" />

```xml
<arc:DropdownMenu ItemsSource="{Binding FileActions}" Side="Bottom">
    <arc:DropdownMenu.Trigger>
        <Button Content="..." />
    </arc:DropdownMenu.Trigger>
</arc:DropdownMenu>
```

Data-driven rows take an `ItemTemplate` for their header when the items are not `MenuEntry` objects. The item behind
each row is reported in `ItemClicked` as `e.Item`, so the handler can run the command for that item.

### Opening from code

```csharp
menu.Open(focusFirst: true);
menu.Close();
```

`Open(true)` opens the menu with its first row highlighted, as Enter or ArrowDown does on the trigger. `IsOpen` is
two-way, so a view model can open the menu by setting it. A menu that starts open with `IsOpen` set in XAML does not
take focus.

### MenuSurface

`MenuSurface` is the reusable surface behind every Arc menu. Open it from code against any anchor. Its rows, its
keyboard model and its motion are the same as the dropdown's, but it has no trigger of its own.

<Demo name="MenuSurface" />

Use a `MenuSurface` declared as a resource when several controls share one menu, and pass the anchor in
`Open(anchor)`. A bare surface does not react to the anchor's clicks; attach it with `DropdownMenu.Menu` when the
control should toggle the menu itself.

### Binding to a view model

A dropdown usually changes the page it sits on. The view model owns the label, the choice and the commands; the
menu shows them and reports the choice back. Bind the label and the items in XAML, and set the commands in code.

```csharp
public interface IExportService
{
    Task StartCsvAsync(string filter);
}

public partial class InvoiceListViewModel : ObservableObject
{
    private readonly IExportService _exports;

    public InvoiceListViewModel(IExportService exports) => _exports = exports;

    [ObservableProperty]
    private string _sortLabel = "Sort: Newest";

    [ObservableProperty]
    private string _filter = "status:open";

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        // Start the export for the current filter.
        await _exports.StartCsvAsync(Filter);
    }
}
```

```xml
<arc:DropdownMenu Label="{Binding SortLabel}"
                  Icon="{x:Static arc:ArcIcons.ArrowUpDown}"
                  Alignment="Start">
    <arc:MenuEntryLabel Text="Sort by" />
    <arc:MenuEntry Header="Newest" ToggleType="Radio" GroupName="sort" IsChecked="True" />
    <arc:MenuEntry Header="Oldest" ToggleType="Radio" GroupName="sort" />
</arc:DropdownMenu>
```

```csharp
menu.Items.Add(new MenuEntry { Header = "Export CSV", Command = viewModel.ExportCsvCommand });
menu.ItemClicked += (_, e) => viewModel.SortLabel = $"Sort: {e.Header}";
```

- `Label` is bound one-way; the view model changes it and the trigger morphs. Use `ItemClicked` to write the choice
  back, because a radio row's `IsChecked` is changed by the menu itself.
- A row's `Command` runs when the row is activated, and the row is disabled while the command cannot execute. Use
  `[RelayCommand]` from CommunityToolkit.Mvvm and its `CanExecute` to disable a row that does not apply.
- Set `Command` in code for rows that are created in code. Rows declared inline in XAML are part of the menu, so keep
  their logic in `ItemClicked`.

### Focus and dismissal

The trigger is the menu's anchor and the place focus returns to. A keyboard opening puts the highlight on a row, and a
pointer opening leaves the highlight off. The menu closes on Escape or Tab (focus returns to the trigger), on a choice
that does not stay open, on an outside press, and when the trigger leaves the visual tree.

Rows that change state keep the menu open only when they say so. A check row that people flip several times sets
`StaysOpenOnClick="True"`. A radio row closes the menu, because a choice is final. A dropdown holds a short list: its
rows are not virtualized, so a list of several hundred items belongs in a `Combobox` or on a page with search.

### Example: invoice list page header

The invoice page header has one primary action, a visible Export menu and a Sort menu that states the current order in
its label. Export groups the file formats and puts the irreversible action last behind a divider. The Export trigger
names the action in its text, so people read what the menu does before they open it. Commands run from the view model,
and the labels follow the state.

```xml
<DockPanel>
    <TextBlock DockPanel.Dock="Left"
               Text="Invoices"
               FontSize="22"
               FontWeight="SemiBold"
               VerticalAlignment="Center"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:DropdownMenu Label="Sort: Newest"
                          Icon="{x:Static arc:ArcIcons.ArrowUpDown}"
                          Alignment="End">
            <arc:MenuEntryLabel Text="Sort by" />
            <arc:MenuEntry Header="Newest" ToggleType="Radio" GroupName="sort" IsChecked="True" />
            <arc:MenuEntry Header="Oldest" ToggleType="Radio" GroupName="sort" />
            <arc:MenuEntry Header="Amount" ToggleType="Radio" GroupName="sort" />
        </arc:DropdownMenu>
        <arc:DropdownMenu Label="Export" Icon="{x:Static arc:ArcIcons.Download}">
            <arc:MenuEntry Header="Export CSV" />
            <arc:MenuEntry Header="Export PDF" />
            <arc:MenuEntry Header="Send to accounting" />
            <arc:MenuEntry Header="Void selected invoices"
                           Icon="{x:Static arc:ArcIcons.Trash2}"
                           IsDestructive="True"
                           HasSeparatorBefore="True" />
        </arc:DropdownMenu>
    </StackPanel>
</DockPanel>
```

### Content guidelines

A trigger label is a short phrase that says what the menu does or what it currently shows. Each row is a verb and its
object, or a choice in a group that has a heading.

- Use one verb or noun for a menu that acts, such as "Export" or "Actions". Use "Name: value" when the label shows
  a state, such as "Sort: Newest".
- Write rows in sentence case, with the verb first: "Export CSV", not "CSV export".
- Name the object in a destructive row: "Void selected invoices", "Delete file".
- Keep a trigger label under about 18 characters; the trigger is a toolbar control and a long label pushes the toolbar.
- Use a `MenuEntryLabel` heading for each group of radio rows, so the group has a name.

| Instead of | Write |
|---|---|
| Sort | Sort: Newest |
| More options | Actions |
| Export data | Export CSV |
| Delete | Delete file |
| Click to change view | View |

### Best practices

**Do**

- Keep a group to two to seven rows. A longer list needs search, which a menu does not provide.
- Put the destructive row last with `HasSeparatorBefore="True"`, and name what it removes in its header.
- Use radio rows for one choice that applies to the whole page, and check rows for independent options.
- Keep the trigger label in step with the state, so the page shows the current sort or view before the menu opens.
- Give a custom trigger the same name as its action with `AutomationProperties.Name`, because `Label` names only the
  built-in trigger.

**Avoid**

- Putting a form value in a dropdown. A value that feeds a form needs a `Select` or a `Combobox`, which hold the value,
  the label and the validation state.
- Hiding the only route to an action. A primary action belongs on a visible button, because a menu is one extra step.
- Changing the rows while the menu is open. The rows are read at open; update the list before the trigger opens it.
- Using a menu for two actions. Two actions read better as two buttons or a `ButtonGroup`.
- Relying on a color to flag the destructive row. Its header names the object, so the meaning survives without color.

### Common questions

**Why does my check row close the menu?** A row closes the menu after a choice unless it sets `StaysOpenOnClick="True"`.
Add that attribute to every check row that people might flip several times.

**How do I change the trigger text when a choice is made?** Set `Label` in the `ItemClicked` handler, or bind it to a
view model property. The new label rises in with the morph motion while the width springs to fit.

**Can I open the menu from code?** Yes. Call `Open()` to show it under the trigger, or `Open(focusFirst: true)` to start
with the first row highlighted. Both work once the trigger is part of a window.

**How do I use a custom control as the trigger?** Set `Trigger` to any control. The menu anchors to that control, and a
`Button` toggles it on click. For a control that already exists on the page, attach the menu with `DropdownMenu.Menu`
instead.

**Which side does the menu open on when there is no room?** It flips to the other side. `Side` is the preferred edge,
and the menu moves when the window does not have room for it.

## API reference

<!-- api: DropdownMenu -->

<!-- api: MenuEntry -->

## Accessibility

- The trigger is announced with its `Label` and reports whether the menu is expanded.
- Rows have the menu item role. Typeahead moves to a row by its first letters, and disabled rows are skipped.
- Enter, Space or ArrowDown on the trigger opens the menu on the first row. Escape closes it and returns focus to the
  trigger.
- A destructive row is marked by its colour only, so its `Header` should say what will be deleted.
- Icons and the highlight are hidden from assistive technology.
- Name the trigger with a `Label` that says what the menu does, such as "Sort: Newest" or "Export". A custom,
  text-free trigger needs its own `AutomationProperties.Name`, because `Label` names only the built-in trigger.

### Keyboard

| Keys | Action |
|---|---|
| `Enter`, `Space` or `ArrowDown` on the trigger | Opens the menu with the first row highlighted |
| `ArrowUp` on the trigger | Opens the menu with the last row highlighted |
| Click or tap on the trigger | Toggles the menu; the menu opens with no row highlighted |
| `ArrowDown` / `ArrowUp` | Move the highlight to the next or previous row; the list loops and skips disabled rows |
| `Home` / `End` | Move the highlight to the first or last row |
| Letters | Typeahead moves to a row whose text starts with the typed letters |
| `Enter` / `Space` in the menu | Activates the highlighted row, then closes the menu unless the row stays open |
| `Escape` / `Tab` | Closes the menu and returns focus to the trigger |

## Motion

- The menu grows from the trigger edge on a spring with a short fade. Closing is quicker and smaller.
- One highlight glides between rows for the pointer and jumps for the keyboard. Rows fade in with a short stagger.
- A new `Label` rises in with a soft blur while the trigger width adjusts to it. The chevron turns when the menu opens.
- Reduced motion removes the scale and the glide and keeps a quick fade. Off shows the menu at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Menu opens | The menu grows from the trigger edge, from a 5 px offset and 0.97 scale, on a 580 ms spring with a 160 ms fade | Quick fade, no scale | Immediate |
| Menu closes | Quicker and smaller: 130 ms, half the offset, scaling to 0.985 | Quick fade | Immediate |
| Highlight moves | The pointer highlight glides on a snappy spring; the keyboard highlight jumps | The highlight jumps | Immediate |
| Rows appear | Each row fades in over 240 ms, staggered 35 ms apart | Quick fade | Immediate |
| `Label` changes | The new label rises 0.3 em with a soft blur and the trigger width springs to fit | Instant fade | Immediate |
| Chevron | Turns 180 degrees on the spring as the menu opens | Turns at once | Immediate |

## Related

- `ContextMenu` for secondary actions on a selected object, opened by right-click.
- `SplitButton` when one action is the default and the menu holds the alternatives.
- `UserMenu` for the account entry point in an app's top bar.
- `MenuSurface` is the shared menu popup, for anchoring a menu to any control.

| Component | Choose it when |
|---|---|
| `ContextMenu` | The actions belong to one object and should open at the pointer |
| `SplitButton` | One action is the default and the menu holds the alternatives |
| `ButtonGroup` | The commands are a row of related buttons rather than a hidden list |
| `UserMenu` | The menu is the account behind the avatar, with identity and theme |
| `Select` / `Combobox` | The menu picks a value that feeds a form |
| `MenuSurface` | You need a menu on a control that is not a dropdown trigger |
