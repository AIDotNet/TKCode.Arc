---
title: Floating button group
summary: Separate soft buttons in a quiet tray, with one shared highlight that morphs from button to button as you move, and a pressed state that settles in place.
---

<Demo name="MutedTray" />

## When to use

`FloatingButtonGroup` puts the actions for a surface that floats over other content in one small tray: a canvas, a
document, a chart or a map. It keeps those actions close to the content they act on, so the page header stays free
for navigation and the surface stays the focus of the screen.

- **Canvas actions in a whiteboard or design tool**: Undo, Redo, Share and Present float over the board. Icon-only
  buttons with shortcut hints keep the tray narrow, and the tooltips carry the names.
- **A selection toolbar over a document**: Bold, Italic and Link appear over the text a person selected in an incident
  postmortem or a help-center article. The tray reads as a layer above the text.
- **A vertical tool rail beside a diagram or map**: Select, Pen and Eraser stack in a column at the edge of the
  canvas. The tooltips open to the right, away from the drawing.
- **A zoom control on an analytics chart**: `ItemsSource` turns a list of zoom levels (Fit, 50%, 100%) into buttons, so
  the list lives in the view model and the tray only shows it.
- **A mode that stays on while a view is open**: Present in a dashboard editor, or Focus in a long document, toggles
  with `IsChecked` and keeps a quiet tint while it is on.

## When not to use

- **A choice that stays selected, such as a text alignment or a view mode**: use `SegmentedControl`, which shows one
  chosen segment as the answer to a question. A single on or off mode can use `IsChecked` on an item.
- **Too many actions for one row**: use `DropdownMenu` for the overflow, or move the rare actions into the menu of
  the surface. A tray that wraps or scrolls is hard to scan.
- **Related actions joined in a page header or a form footer**: use `ButtonGroup`, which joins the buttons into one
  bordered surface with dividers.
- **Compact icon actions on a card or a message, where labels should appear on focus**: use `ExpandingButtonGroup`.
- **A single action**: use `Button`. A tray with one button only adds a surface around it.

## Usage

Each button is a `FloatingButtonGroupItem`. Give the tray a `Label`, which becomes its accessible name, such as
"Board actions" or "Text formatting".

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:FloatingButtonGroup Label="Board actions">
        <arc:FloatingButtonGroupItem Label="Undo" Shortcut="⌘Z" IsIconOnly="True" />
        <arc:FloatingButtonGroupItem Label="Redo" Shortcut="⇧⌘Z" IsIconOnly="True" />
        <arc:FloatingButtonGroupSeparator />
        <arc:FloatingButtonGroupItem Label="Share" />
    </arc:FloatingButtonGroup>
</UserControl>
```

`FloatingButtonGroup` derives from Avalonia's `ItemsControl`, so `Items`, `ItemsSource`, `ItemsPanel` and the
selection-free item model work as they do for any items control. Each `FloatingButtonGroupItem` derives from
Avalonia's `Button`: `Click`, `Command`, `CommandParameter`, `IsEnabled` and the keyboard activation all behave as they
do on a button. The tray adds the highlight, the keyboard model, the layout (row or column), the sizes and the surface
variants. `FloatingButtonGroupSeparator` is a thin rule between two groups of buttons.

### Anatomy

1. **Tray** (`FloatingButtonGroup`): a rounded surface with a 1 px edge. Its corner radius is 18 px, and its padding
   is 4 px on each side. `Size="Small"` shrinks both to 15 px and 3 px.
2. **Highlight**: one rounded shape behind the buttons, drawn in the highlight color (the foreground at 7%, 9% in dark
   mode). It is the only element that moves between buttons. Its corner radius is 13 px, or 11 px when small.
3. **Item** (`FloatingButtonGroupItem`): a button 36 px high (30 px when small) with a 12 px inset on each side (10 px
   when small). An icon-only item is a square of the same height. Its corner radius is 13 px, or 11 px when small.
4. **Icon** (`Icon`): a 16 px Lucide glyph with a 1.75 px stroke, drawn 6 px before the label.
5. **Label** (`Label`): the text of the item, in the tray's secondary text color with medium weight. It is hidden when
   `IsIconOnly` is true.
6. **Separator** (`FloatingButtonGroupSeparator`): a 1 px rule, 18 px long (15 px when small), with 2 px of space on
   each side. In a column it is a horizontal rule of the same length. Its color is the foreground at 12%, set by
   `RuleBrush` in the theme.
7. **Shortcut hint** (`Shortcut`): not drawn on the button. It appears in the item's tooltip, after the label.

The surface comes in two variants. `Muted` (the default) is a translucent tint of the foreground color, at 4.5% in
light mode and 6% in dark mode, with a 3% edge. It reads on a muted background without a shadow. `Floating` uses the
raised surface color, the standard border color and the floating shadow, so it lifts above the content it covers.

### Variant and size

Choose the variant by the layer the tray sits on. A tray beside the content uses `Muted`. A tray over content, such
as a selection toolbar, can use `Floating` to show that it is a separate layer.

```xml
<arc:FloatingButtonGroup Label="Selection actions" Variant="Floating">
    <arc:FloatingButtonGroupItem Label="Comment" Icon="{x:Static arc:ArcIcons.MessageSquare}" />
</arc:FloatingButtonGroup>
```

`Size` takes `Medium` (the default, 36 px buttons) or `Small` (30 px buttons); `Large` is treated as `Medium`. Use
`Small` in dense areas such as a table toolbar or a chart corner, and `Medium` for a canvas, where the buttons are the
main tool and need the larger hit area.

### Icons and icon-only items

Set `Icon` to a 24 by 24 Lucide geometry in code or with `{x:Static arc:ArcIcons.Name}` in XAML. The icon is drawn
at 16 px. `IsIconOnly` on an item hides its label, and setting `IsIconOnly` on the tray makes every item icon only.
An item's own setting wins over the tray's, so a single text button can stay in an icon-only tray. Set
`IsIconOnly` on the tray only when every action has an icon that people already know, such as Bold, Undo or Link.

Icon-only items and items with a `Shortcut` show their label in a tooltip. An icon-only item still needs a `Label`,
because it is the accessible name. `IsIconOnly` has no effect on an item that has no
`Icon`: such an item keeps its text.

<Demo name="IconOnly" />

```csharp
bold.Icon = ArcIcons.Bold;
link.Icon = ArcIcons.Link;
```

### Label change and reserved width

A label can change to confirm an action, for example from "Share" to "Copied" after the link is on the clipboard. Set
`ReserveLabels` to every other label the item can show. The button then keeps the width of the widest label, so the
buttons next to it do not move when the text changes. Without reserved labels, the highlight follows the new width.

```csharp
var share = new FloatingButtonGroupItem { Label = "Share", ReserveLabels = ["Copied"] };
share.Click += async (_, _) =>
{
    share.Label = "Copied";
    await Task.Delay(1600);
    share.Label = "Share";
};
```

Keep confirmation labels to one or two words, and restore the original after a short pause (the demo uses 1.6
seconds).

### Items from data and vertical layout

`ItemsSource` turns plain data into buttons. Each item's label is the text of the object (`ToString()`), unless the
item container has a label already. `Orientation="Vertical"` stacks the buttons in a column, and the tooltips then
open to the right by default. Set `TooltipPlacement` to change the side for every item: `Top`, `Bottom`, `Left` or
`Right`. A row uses `Top` by default.

<Demo name="FromItemsSource" />

<Demo name="Vertical" />

```xml
<arc:FloatingButtonGroup Label="Zoom" Size="Small" ItemsSource="{Binding ZoomLevels}" />

<arc:FloatingButtonGroup Label="Tools" Orientation="Vertical" IsIconOnly="True">
    <arc:FloatingButtonGroupItem Label="Select" Shortcut="V" />
    <arc:FloatingButtonGroupItem Label="Pen" Shortcut="P" />
</arc:FloatingButtonGroup>
```

A vertical tray moves with the Up and Down arrow keys, and the separator turns into a horizontal rule.

### Checked items

`IsChecked` turns an item into a mode that stays on: Present, Focus, Show grid. A checked item keeps a quiet tint
behind the button (the control-on color at 12%, 16% in dark mode), and it reports its state to assistive technology
as On or Off. `IsChecked` set to `null` (the default) makes a plain action with no state.

```xml
<arc:FloatingButtonGroupItem Label="Present"
                             Icon="{x:Static arc:ArcIcons.Monitor}"
                             IsChecked="{Binding IsPresenting, Mode=TwoWay}" />
```

Do not use `IsChecked` for a choice among several options; use `SegmentedControl` for that.

### Shortcut hints and tooltips

`Shortcut` is a hint, not a binding. The string appears in the tooltip, and `AutomationProperties.HelpText` carries
it for screen readers. The group never handles the key itself. Register the key on the window or the view, and run the
same command the button runs, so the shortcut works when the tray has no focus.

```csharp
KeyBindings.Add(new KeyBinding
{
    Gesture = new KeyGesture(Key.Z, KeyModifiers.Control),
    Command = vm.UndoCommand
});
```

Tooltips open above a row and to the right of a column, unless `TooltipPlacement` says otherwise.

### Events

`ActionInvoked` runs after the item's own `Click` handler and its `Command`. `e.Item` is the button that ran, and
`e.Data` is the source: the button itself for inline items, or the data object for an `ItemsSource`. Handle it to
route every action of a tray in one place; keep per-button behavior in `Click` or `Command`.

```csharp
board.ActionInvoked += (_, e) => log.Text = $"Action: {e.Item.Label}";
```

For an `ItemsSource` tray, read the data object from `e.Data`:

```csharp
zoom.ActionInvoked += (_, e) => viewModel.Zoom = (string)e.Data!;
```

### Binding to a view model

A board editor exposes its actions as commands and keeps the presentation state as properties. The tray binds to them
directly, and the highlight, the pressed state and the keyboard model stay in the control.

```csharp
public partial class BoardViewModel : ObservableObject
{
    private readonly IBoardHistory _history;

    public BoardViewModel(IBoardHistory history)
    {
        _history = history;
        ZoomLevels = new ObservableCollection<string> { "Fit", "50%", "100%", "200%" };
    }

    [ObservableProperty]
    private bool _isPresenting;

    [ObservableProperty]
    private string _zoom = "Fit";

    public ObservableCollection<string> ZoomLevels { get; }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => _history.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => _history.Redo();

    private bool CanUndo => _history.CanUndo;

    private bool CanRedo => _history.CanRedo;
}
```

```xml
<Grid RowDefinitions="Auto,*">
    <arc:FloatingButtonGroup Grid.Row="0" Label="Board actions" HorizontalAlignment="Center">
        <arc:FloatingButtonGroupItem Label="Undo"
                                     Icon="{x:Static arc:ArcIcons.Undo2}"
                                     IsIconOnly="True"
                                     Shortcut="⌘Z"
                                     Command="{Binding UndoCommand}" />
        <arc:FloatingButtonGroupItem Label="Redo"
                                     Icon="{x:Static arc:ArcIcons.Redo2}"
                                     IsIconOnly="True"
                                     Shortcut="⇧⌘Z"
                                     Command="{Binding RedoCommand}" />
        <arc:FloatingButtonGroupSeparator />
        <arc:FloatingButtonGroupItem Label="Present"
                                     Icon="{x:Static arc:ArcIcons.Monitor}"
                                     IsChecked="{Binding IsPresenting, Mode=TwoWay}" />
    </arc:FloatingButtonGroup>
    <arc:FloatingButtonGroup Grid.Row="1" Label="Zoom" Size="Small"
                             HorizontalAlignment="Left"
                             ItemsSource="{Binding ZoomLevels}" />
</Grid>
```

- `IsChecked` is two-way: the item writes its state to `IsPresenting`, and the view model can change it too.
- `Command` drives the action items. When `CanExecute` is false, the item is disabled: it fades to 50% opacity and
  leaves the Tab and arrow-key path.
- The zoom tray is one-way; its choice reaches the view model through `ActionInvoked`, as shown above.

### Example: selection toolbar over a postmortem draft

A tray sits above a paragraph of an incident postmortem. The person selected the sentence about the
certificate rotation, and the tray offers the inline formatting and the comment action. The formatting items are
icon only, because people know Bold, Italic and Link by their icons, and each carries a shortcut hint in its tooltip.
Comment and Quote keep their labels, since their icons are less universal. The tray is left-aligned over the text, so
it reads as attached to the selection.

![A floating selection toolbar with formatting and comment actions above a postmortem paragraph](images/SelectionToolbar.png)

```xml
<StackPanel Width="600" Spacing="14">
    <arc:FloatingButtonGroup Label="Selection actions"
                             HorizontalAlignment="Left"
                             Margin="24,0,0,0">
        <arc:FloatingButtonGroupItem Label="Bold"
                                     Icon="{x:Static arc:ArcIcons.Bold}"
                                     IsIconOnly="True"
                                     Shortcut="⌘B" />
        <arc:FloatingButtonGroupItem Label="Italic"
                                     Icon="{x:Static arc:ArcIcons.Italic}"
                                     IsIconOnly="True"
                                     Shortcut="⌘I" />
        <arc:FloatingButtonGroupItem Label="Link"
                                     Icon="{x:Static arc:ArcIcons.Link}"
                                     IsIconOnly="True"
                                     Shortcut="⌘K" />
        <arc:FloatingButtonGroupSeparator />
        <arc:FloatingButtonGroupItem Label="Comment"
                                     Icon="{x:Static arc:ArcIcons.MessageSquare}" />
        <arc:FloatingButtonGroupItem Label="Quote"
                                     Icon="{x:Static arc:ArcIcons.Quote}" />
    </arc:FloatingButtonGroup>
    <Border Padding="24"
            Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
        <StackPanel Spacing="10">
            <TextBlock Text="Postmortem: checkout outage on March 12"
                       FontSize="18"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="A certificate rotation on the payment gateway stopped card authorizations for 47 minutes. About 1,200 orders were retried after service returned."
                       TextWrapping="Wrap"
                       LineHeight="22"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Owner: Priya Raman, Platform team. Follow-up due April 3, 2026."
                       FontSize="13"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
    </Border>
</StackPanel>
```

### Content guidelines

- **Label**: one or two words in sentence case, a verb or a noun the person can act on: "Undo", "Share", "Present".
  The tray's `Label` is a noun for the group, such as "Board actions" or "Zoom".
- **Icon-only labels**: the label is the accessible name, so it must say the full action ("Bold", "Insert link"), not the
  icon's shape.
- **Shortcut hints**: show the key the platform uses, with the symbols as in the examples (⌘Z, ⇧⌘Z). Write the hint
  as the key combination only; the tooltip already carries the action name.
- **Confirmation labels**: a single short word ("Copied") that matches the action. Keep the reserved label short so the
  button width stays small.

| Instead of | Write |
|---|---|
| Click here to undo the last change | Undo |
| share | Share |
| Ctrl + Z | ⌘Z (or the platform's key, as the app runs) |
| Copied to your clipboard! | Copied |

### Best practices

**Do**

- Put the tray where the content it acts on is: over the selection, beside the canvas, or at the top of the board.
  People look for actions near their work.
- Use `Floating` only over content. A tray on the page background looks like a raised panel with nothing to lift it
  from.
- Give every icon-only item a `Label` and a `Shortcut` where one exists. The label gives the accessible name, and the
  shortcut teaches people the keyboard path over time.
- Bind commands to the items so the disabled state comes from the view model. A disabled item stays visible.
- Reserve the labels of confirmations up front, so the neighbouring buttons do not move.

**Avoid**

- Using the tray for a choice that stays selected. A tray of view modes hides which one is current; use
  `SegmentedControl`.
- Putting more than six or seven actions in one tray. Past that, the tray is hard to scan and the overflow belongs in
  a `DropdownMenu`.
- Handling the shortcut only in the tooltip. The group does not bind keys, so a shortcut that is shown but not
  registered teaches people a key that does nothing.
- Mixing icon-only and labelled items in one row without a reason. Mixed widths make the row uneven; set `IsIconOnly`
  on the tray for a consistent row, and use item-level settings only for the exceptions.

### Common questions

**Why does my icon-only item show its label as text?** `IsIconOnly` takes effect only when the item has an `Icon`. An
item without an icon always shows its label, so add the icon or remove `IsIconOnly`.

**Why does the highlight stay on a button after I move the pointer out?** While the pointer is in the tray it owns the
highlight. When the pointer leaves, the highlight returns to the button with keyboard focus if there is one, and fades
out otherwise.

**Which setting wins, the item's `IsIconOnly` or the tray's?** The item's own value, when it is not `null`.

## API reference

<!-- api: FloatingButtonGroup -->

### Item and event arguments

`FloatingButtonGroupItem` is the button of a tray. It adds these properties to the inherited `Button` members:

| Property | Type | Meaning |
|---|---|---|
| `Label` | `string` | Visible text, and the accessible name when only the icon shows. |
| `Icon` | `Geometry?` | A 16 px Lucide glyph, for example `ArcIcons.Undo2`. |
| `IsIconOnly` | `bool?` | Shows only the icon. `null` follows the tray's `IsIconOnly`. |
| `IsChecked` | `bool?` | A mode that switches on. `null` is a plain action. |
| `Shortcut` | `string?` | A shortcut hint shown in the tooltip. The tray does not bind the key. |
| `ReserveLabels` | `IReadOnlyList<string>?` | Other labels the item can show; the button keeps the widest width. |

`FloatingButtonGroupActionEventArgs` (the argument of `ActionInvoked`) exposes `Item` (the button that ran) and `Data`
(the source object). `FloatingButtonGroupVariant` has two values: `Muted` and `Floating`.

## Accessibility

- The tray has the toolbar role and takes its name from `Label`. It reports its orientation.
- The tray is a single Tab stop. Focus returns to the last button that had keyboard focus.
- Each item is named by its label, including icon-only items. The shortcut hint is exposed as help text.
- An item with `IsChecked` reports a toggle state of On or Off. Changing the value announces the new state.
- Separators are announced as separators, so the grouping is available without the visuals.
- Disabled items stay readable and are skipped by the arrow keys. They keep their label, so people still learn that the
  action exists.
- The shared highlight shows only for pointer hover or keyboard focus, so it never marks a button that a person did
  not reach.

### Keyboard

The tray is one stop in the Tab order. Arrow keys move the highlight between the buttons.

| Keys | Action |
|---|---|
| `Tab` | Enters the tray at the button that had focus last, or the first enabled button. The next `Tab` leaves the tray. |
| `Left` / `Right` (row) or `Up` / `Down` (column) | Moves to the next or previous enabled button. The movement wraps at both ends. |
| `Home` / `End` | Moves to the first or last enabled button. |
| `Enter` / `Space` | Runs the focused button: its `Click` and `Command`, then `ActionInvoked`. |

## Motion

- One highlight glides to the button under the pointer or the keyboard focus. It holds still when the pointer crosses
  a gap or a separator, so the highlight never flickers between buttons.
- When the tray first receives a target, the highlight lands in place and grows from 94% to 100%. When the pointer
  leaves the tray, the highlight fades out.
- A pressed button darkens the highlight and the highlight contracts slightly. The button itself does not scale.
- Label changes crossfade in place. A button with `ReserveLabels` keeps its width; otherwise the highlight follows the
  new width.
- Reduced motion moves the highlight to its target with a short fade and crossfades the labels without a shift. Off
  puts everything in its final place at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Highlight moves to another button | The highlight glides in position and size on a morph spring | Jumps to the target with a short fade | Immediate |
| Highlight appears on the first target | Grows from 94% to 100% on a snappy spring | Immediate | Immediate |
| Button is pressed | The highlight darkens and contracts to 96.5% on a snappy spring | Immediate | Immediate |
| Label changes | Crossfades in place with a short upward move and a soft blur | Crossfades without a shift | Immediate |

## Related

- `ButtonGroup` for related actions joined into one surface with dividers.
- `ExpandingButtonGroup` for compact icon actions whose labels expand on focus.
- `SegmentedControl` for a choice that stays selected.
- `Button` for a single action.

| Component | Choose it when |
|---|---|
| `ButtonGroup` | Related actions belong on one bordered surface in a page header or a form footer |
| `ExpandingButtonGroup` | Compact icon actions on a card or a message should reveal their labels on focus |
| `SegmentedControl` | A choice stays selected, such as a view mode, an alignment or a period |
| `DropdownMenu` | Actions overflow the tray and can wait behind one trigger |
| `SplitButton` | One primary action has alternatives in a menu |
| `Button` | There is one action with no state |
