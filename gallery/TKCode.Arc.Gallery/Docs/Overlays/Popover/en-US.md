---
title: Popover
summary: A small anchored surface for contextual information.
---

<Demo name="Share" />

## When to use

`Popover` holds a small piece of interactive content next to the control that opens it: share settings, a filter form
on a list toolbar, a picker built from your own controls. It is anchored to its trigger, so the reader keeps the
context of the page, and it is non-modal by default, so the rest of the page stays usable while it is open.

- **Share and access settings on a document or record.** A Share button in a document header opens a panel with the
  link, a copy control and a permission choice, without taking the reader to a settings page.
- **Filters on a list toolbar.** A Status and date-range filter on an order or invoice list opens from a Filter button,
  applies its values and closes, so the table stays in view while the reader adjusts the criteria.
- **Column or view options in a data table.** A Columns button that lists the optional columns as check boxes, so the
  reader can show or hide fields without leaving the table.
- **Code-driven panels on a canvas or a diagram.** An app that opens a small editor next to a selected node calls
  `ShowAt` with that node's control, so the panel follows the selection.

## When not to use

- **A one-line label on hover or focus:** use `Tooltip`. A popover is opened by a click and holds controls; a tooltip is
  read-only text that appears on hover.
- **A read-only preview of a person or a link:** use `HoverCard`, which opens on hover and never takes focus.
- **A decision that must be answered before the user continues:** use `Dialog`. A popover can be dismissed by a press
  outside it, so it cannot hold a confirmation such as deleting a workspace.
- **A list of commands only:** use `DropdownMenu`, which gives arrow-key navigation and menu semantics for actions.
- **A long form or a multi-step flow:** use a page, a `Drawer` or a `Dialog`. A popover is sized for a few fields; a
  form with many fields pushes the page underneath off screen.

## Usage

A `Popover` is a `ContentControl`. Its `Content` is the panel, and its `Trigger` is the control that opens the panel.
The panel is shown in a `PopoverSurface` that is hosted in a popup, so it is not part of the layout and it can
extend past the edge of the trigger. The trigger and the panel are linked: the panel is measured against the trigger,
so the trigger answers presses with its color only and does not scale.

### Anatomy

1. **Trigger** (`Trigger`): the control that opens the panel. It stays in the page at its own size; the popover adds
   no layout of its own around it.
2. **Panel surface**: a `PopoverSurface` with the raised surface color (`SurfaceRaised`), the foreground text color, a
   1 px border in the border color (`Border`) and the panel corner radius (`RadiusPanel`). It is 16 px inside on every
   side, is at least 192 px (12 rem) and at most 352 px (22 rem) wide, and uses the body font at the small type size
   (`TypeSM`). The floating shadow is drawn by the popup.
3. **Content** (`Content`): the panel's contents. A string is shown as wrapping text in the surface's default text
   template; any other control is shown as it is.
4. **Gap** (`Offset`): 6 px between the trigger and the panel by default, and the panel's entry motion starts from this
   distance.

### Trigger and content

Set `Trigger` to the control that opens the panel. The content of the popover is the panel.

```xml
<arc:Popover>
    <arc:Popover.Trigger>
        <arc:Button Variant="Secondary" Content="Share" />
    </arc:Popover.Trigger>
    <StackPanel Spacing="8">
        <TextBlock Text="Share link" FontWeight="Medium" />
        <TextBlock Text="Anyone with the link can view." />
    </StackPanel>
</arc:Popover>
```

Activating the trigger toggles the popover: a click, a tap or the keyboard activation of a button opens it, and the
same activation closes it again. A non-button trigger opens the popover on `Enter` or `Space`.

### Attached to a control

`arc:Popover.Attach` adds a popover to any control. A click on that control toggles the panel. Use it when the control
is already the button the reader expects, and the panel does not need a separate trigger.

```xml
<arc:Button Content="Filters">
    <arc:Popover.Attach>
        <arc:Popover Placement="Top">
            <TextBlock Text="Opens on the top side." />
        </arc:Popover>
    </arc:Popover.Attach>
</arc:Button>
```

While an attached popover is open, it joins the logical tree of its target, so styles and the `DataContext` of the
target apply inside the panel. When it closes, it leaves that tree again.

### Placement and alignment

`Placement` is the preferred side: `Top`, `Bottom` (the default), `Left` or `Right`. The panel flips to another side
when the preferred side does not fit in the window, and its opening motion follows the side it actually landed on.
`Alignment` places the panel along the trigger edge: `Start` (the default), `Center` or `End`. `Offset` is the gap to
the trigger in pixels; the default is 6. Changing `Placement`, `Alignment` or `Offset` while the popover is open moves
the panel at once.

<Demo name="Sides" />

```xml
<arc:Popover Placement="Bottom" Alignment="Center" Offset="8">
    <arc:Popover.Trigger>
        <arc:Button Variant="Ghost" Content="Filter" />
    </arc:Popover.Trigger>
    <TextBlock Text="Status: any" />
</arc:Popover>
```

Pick `Bottom` with `Start` for a toolbar button at the left of a list, so the panel lines up with the button's left
edge. Use `Center` for a control in the middle of a row and `End` for a button at the right edge of the page, so the
panel does not run past the window. The default 6 px gap is the Arc look.

### Opening from code

`ShowAt` anchors the popover to any control in the window, so one popover can serve several triggers or a node that
the user selects. `IsOpen` is two-way, and `Opened` and `Closed` report the state changes. Calling `ShowAt` while the
popover is open with a different anchor closes it and reopens it at the new anchor. `Close` closes it from code, and
`OpenCommand` and `CloseCommand` expose the same two actions as `ICommand` for buttons and bindings.

<Demo name="FromCode" />

```csharp
share.ShowAt(shareButton);
share.Closed += (_, e) => status.Text = $"Closed: {e.Reason}";
```

`Closed` carries a `Reason`: `Escape` when the reader pressed Escape, `PressOutside` when they pressed outside the
panel, and `Programmatic` when the trigger toggled it, the code called `Close` or the value was set to false.

### Modal popover

`IsModal` set to true swallows presses outside the panel, so they only close it and do not reach the control
underneath. A press outside is swallowed: it closes the popover and does not reach the control underneath. Use it for a short
choice that the reader must make or dismiss before clicking elsewhere, and use `Dialog` when the content needs a
confirmation or its own scrim.

```xml
<arc:Popover IsOpen="{Binding ShowPicker}" IsModal="True">
    <TextBlock Text="Pick one option first." />
</arc:Popover>
```

### Small form

A popover can hold a small form, such as a filter. The page stays usable while it is open.

<Demo name="SmallForm" />

### Binding to a view model

Bind `IsOpen` when the view model decides whether the panel is open, for example when a toolbar command should open a
filter panel. `IsOpen` is two-way, so a press outside the panel also updates the view model.

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class OrdersViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isFilterOpen;

    [ObservableProperty]
    private string? _status;

    [RelayCommand]
    private void ApplyFilters()
    {
        IsFilterOpen = false;
        // Reload the orders with the selected status and dates.
    }
}
```

```xml
<arc:Button Content="Filter" Variant="Ghost">
    <arc:Popover.Attach>
        <arc:Popover IsOpen="{Binding IsFilterOpen}" Placement="Bottom" Alignment="Start">
            <StackPanel Width="240" Spacing="12">
                <TextBlock Text="Status" FontWeight="Medium" />
                <arc:Button Content="Apply filters" Command="{Binding ApplyFiltersCommand}" />
            </StackPanel>
        </arc:Popover>
    </arc:Popover.Attach>
</arc:Button>
```

The attached popover toggles on click, so the reader opens and closes the panel with the button. Keep `IsOpen` as the
one source of the state, and avoid setting it from a binding and from code at the same time.

### Focus and dismissal

Focus moves into the panel when it opens: to the first focusable control inside it, or to the panel itself when it has
none. When the popover closes, focus returns to the trigger in these cases:

- **Escape** closes the popover and always returns focus to the trigger.
- **A press outside** returns focus to the trigger only when focus was inside the panel. A press that lands on another
  control keeps focus where the reader put it.
- **Closing from code** returns focus to the trigger when focus was inside the panel.

The panel cycles `Tab` among its own controls, so the reader does not Tab out of the panel into the page underneath
without first closing it. A popover nested inside a `Dialog` works: focus can enter the popover, and a press outside the
popover closes only the popover while the dialog stays open.

### Example: filter panel on an order list

A toolbar in a back-office console, above a table of orders. The Filter button opens a small form with a status choice
and two date fields, so the reader narrows the list and sees the table update behind the panel. The panel has one
primary action and a text action, and the panel is 240 px wide, so the reader sees the table behind it.
The primary button reads "Apply filters", a verb with its object, so the action is clear before the reader clicks it.

```xml
<StackPanel Width="560" Spacing="12">
    <arc:Popover Placement="Bottom" Alignment="Start">
        <arc:Popover.Trigger>
            <arc:Button Variant="Secondary" Content="Filter orders" />
        </arc:Popover.Trigger>
        <StackPanel Width="240" Spacing="12"
                    AutomationProperties.Name="Filter orders">
            <TextBlock Text="Status"
                       FontWeight="Medium"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <arc:Select Placeholder="Any status" />
            <TextBlock Text="Created"
                       FontWeight="Medium"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <arc:Input Placeholder="From" />
            <arc:Input Placeholder="To" />
            <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
                <arc:Button Content="Reset" Variant="Ghost" />
                <arc:Button Content="Apply filters" />
            </StackPanel>
        </StackPanel>
    </arc:Popover>
</StackPanel>
```

### Content guidelines

- **Trigger text**: a verb and an object when the panel changes data ("Filter orders", "Share report"), or the name of
  the setting when it opens a view ("Columns"). A plain "More" hides what the panel does.
- **Panel heading**: a short noun phrase in sentence case, such as "Share link". Give the popover an
  `AutomationProperties.Name` when the content has no heading, so the panel is announced by name.
- **Button text in the panel**: a verb and an object for the primary action ("Apply filters", "Copy link"), and a
  plain verb for the dismissing action ("Reset", "Cancel").
- **Field labels**: visible and short, as in any form; a popover is not a place for placeholder-only fields.
- **Helper copy**: one sentence that states the effect of a setting, such as "Anyone with the link can view."

| Instead of | Write |
|---|---|
| More | Filter orders |
| Click here to share this document | Share link |
| Submit | Apply filters |
| Options for Column Display | Columns |
| Please select a status from the list below. | Status |

### Best practices

**Do**

- Keep the panel to a few controls that a reader can act on in one pass. Two fields and one primary button fit well;
  more content belongs in a `Dialog` or a page.
- Give every popover a name, either a heading inside the panel or `AutomationProperties.Name` on it, so the panel is
  announced when focus moves into it.
- Put the most important action first in the panel, and keep the dismissing action beside it, aligned to the right.
- Use `Alignment="End"` for a trigger at the right edge of the page, so the panel stays inside the window.
- Close the popover after an action that completes in it, so the reader sees the result on the page.

**Avoid**

- A confirmation or a destructive action inside a popover. A press outside closes it without a choice, so the reader can
  lose the input; use `Dialog` for anything that must be confirmed.
- Long forms or scrolling content. The panel is sized for a small amount of content, and a tall panel covers the
  table or the page it was opened from.
- Opening a popover from a `HoverCard` or a `Tooltip` trigger. Hover only shows a preview; a click should open a panel
  that holds controls.
- Setting `IsOpen` from two places at once, such as a binding and a `ShowAt` call that fight over the same state.
- Using a popover as a navigation menu. A list of commands belongs in `DropdownMenu`, which gives arrow keys and menu
  semantics.

### Common questions

**What happens when I set `IsOpen` to true before the popover is in the window?** The popover records the request and
opens once it is attached to the visual tree, so a view model can set the state before the view is shown.

**Why does the trigger not scale when I press it?** The panel is measured against the trigger, so a scale on press would
move the anchor while the panel is open. Arc buttons used as a popover trigger show the press as a color change only.

**How do I close the popover from a button inside it?** Call `Close()` in code, or bind the button to a view model
command that sets `IsOpen` to false. Either way, focus returns to the trigger when it was inside the panel.

**Does a press inside the panel close it?** No. Only Escape, a press outside the panel, the trigger toggle, `Close` and
setting `IsOpen` to false close it. A press inside the panel reaches its controls as usual.

**Can I attach the popover to something that is not a button?** Yes. Any control can take `arc:Popover.Attach`. A click
toggles the panel; for a control that is not a button, `Enter` and `Space` open it from the keyboard.

## API reference

<!-- api: Popover -->

## Accessibility

- Enter and Space on the trigger open the popover. Escape closes it and returns focus to the trigger.
- Focus moves into the panel when it opens. A press outside closes the popover and returns focus to the trigger when
  focus was inside it.
- Give the popover an `AutomationProperties.Name` when its content has no heading. That name labels the panel.
- Non-modal popovers do not trap focus. The panel cycles `Tab` among its own controls, which keeps the reader inside
  the panel until it closes.
- The trigger exposes whether the popover is expanded: its item status is `Expanded` while it is open and `Collapsed`
  while it is closed.

### Keyboard

| Keys | Action |
|---|---|
| `Enter` or `Space` on a non-button trigger | Opens the popover and moves focus into the panel |
| `Tab` / `Shift+Tab` inside the panel | Move between the panel's controls; focus cycles within the panel |
| `Escape` | Closes the popover and returns focus to the trigger |
| Press outside the panel | Closes the popover; focus returns to the trigger when it was inside the panel |

## Motion

- The panel starts 5px toward its trigger at a scale of 0.97, fades in quickly and settles on a spring from the side it
  landed on.
- Closing takes about 160ms at half the travel. An open and a close reverse each other in the middle of the motion.
- The trigger changes color when pressed but does not scale, because the panel is measured against it.
- Reduced motion removes the movement and scaling and keeps only a short fade. Off shows or removes the panel at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Panel opens | Starts 5px toward the trigger at 0.97, fades in quickly, and settles on a spring | Short fade only | Shown at once |
| Panel closes | Fades out with half the travel in about 160ms | Short fade only | Removed at once |
| Open interrupted by a close | Reverses from the current position | Short fade | Immediate |
| Trigger pressed | Color change only; no scale | Color change only | Color change only |

## Related

- `Tooltip` for a one-line label on hover or focus.
- `HoverCard` for a read-only preview that opens on hover.
- `Dialog` for content that must be dealt with before the page can be used.
- `DropdownMenu` for a list of commands.

| Component | Choose it when |
|---|---|
| `Tooltip` | A one-line label on hover or focus is enough, and nothing in it is clickable |
| `HoverCard` | A read-only preview of a person or link should open on hover, without taking focus |
| `Dialog` | The reader must confirm or finish something before they continue |
| `DropdownMenu` | The content is a list of commands with arrow-key navigation |
| `Drawer` | The content is a longer panel that slides in from an edge of the window |
