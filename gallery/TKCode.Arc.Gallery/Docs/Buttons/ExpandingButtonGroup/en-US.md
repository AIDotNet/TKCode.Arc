---
title: Expanding button group
summary: Icon buttons in a compact group: the one you point at or focus grows to reveal its label while its neighbours slide aside, and an action confirms in place.
---

<Demo name="MessageActions" />

## When to use

`ExpandingButtonGroup` puts three to six related actions in one compact pill. One action always shows its label, and the
one the pointer or the keyboard reaches takes that label over while its neighbours slide aside. It fits a surface where
space is tight and the actions are familiar, so people learn the icons quickly and the labels appear only when needed.

- **Message and comment actions** in a support inbox or a team chat: Reply, Archive and Delete under a message, where the
  active label names the action and the Archive confirmation reads "Archived" in place.
- **File actions** in a document list or a file browser: Download, Share and Delete for the selected file, with a
  `Small` size that matches the dense rows of a table.
- **Card actions** on a dashboard or a project board: Like, Comment and Publish on a post or a task card, where one action
  is the most likely next step and is expanded at rest.
- **Save and confirm flows** in an editor toolbar, where a Save action can confirm with "Saved" for a moment before the
  label returns to rest, so the person knows the change was stored.

## When not to use

- **A choice that stays selected**, such as a view switcher between List, Board and Calendar: use `SegmentedControl` from
  Toggles, which shows the selection and keeps it.
- **One or two actions with enough room**: use a labelled `Button`, since the labels never need to be discovered.
- **More than about six actions**: use `DropdownMenu` from Menus, which lists them with their labels.
- **Icon actions that need labels all the time**, such as a shared toolbar that new users must read: use
  `FloatingButtonGroup` or `ButtonGroup`, whose labels stay visible.

## Usage

Each action is an `ExpandingButtonGroupItem`. The group needs a `Label`, which is its accessible name.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ExpandingButtonGroup Label="Message actions">
        <arc:ExpandingButtonGroupItem Label="Reply" />
        <arc:ExpandingButtonGroupItem Label="Send" DoneLabel="Sent" />
        <arc:ExpandingButtonGroupItem Label="Archive" DoneLabel="Archived" />
    </arc:ExpandingButtonGroup>
</UserControl>
```

`ExpandingButtonGroup` derives from `ItemsControl`, so its items are the `ExpandingButtonGroupItem` containers. Each item
derives from Avalonia's `Button`: `Command`, `Click` and `IsEnabled` behave as they do on a button, and the group adds the
expansion, the confirmation, the busy state and the keyboard model on top.

### Anatomy

1. **Pill** (`Label` names it): a 1 px border on the surface color with the pill radius and a 3 px inset. The actions sit
   2 px apart. `Label` is the toolbar's name and is not drawn.
2. **Collapsed action**: a square 36 px in the Medium size (28 px in `Small`) with a 16 px icon (14 px in `Small`). The
   icon starts 10 px in from the left edge (7 px in `Small`).
3. **Expanded label**: the label follows the icon after a 7 px gap (5 px in `Small`) and ends with a 14 px inset (11 px in
   `Small`). The expanded action fills with the muted surface color and the text uses the foreground color.
4. **Danger tone** (`Tone="Danger"`): while expanded, the text uses the danger color on a danger tint of 10% (14% in dark
   mode) over the surface.
5. **Confirmation** (`DoneLabel`): the icon becomes a check and the label becomes the done word on a success tint of 10%
   (14% in dark mode) over the surface. The done word reserves the same space as the longer of the two words.
6. **Focus ring**: a ring that appears only for keyboard focus.
7. **Disabled action** (`IsDisabled`): dimmed, with the label in the muted text color. It stays focusable and still
   expands, so people can see what it would do.

### Icons and the resting action

Set `Icon` to a 16 by 16 Lucide geometry in code or with `x:Static`. The group shows one label at rest: the first enabled
action by default. `DefaultExpandedItem` picks another one, and when that item is disabled or not in the group, the
group falls back to the first enabled action.

<Demo name="RestingAction" />

```csharp
reply.Icon = ArcIcons.Reply;
archive.Icon = ArcIcons.Archive;

var comment = new ExpandingButtonGroupItem { Label = "Comment", Icon = ArcIcons.MessageCircle };
var card = new ExpandingButtonGroup
{
    Label = "Card actions",
    DefaultExpandedItem = comment
};
```

Choose the resting action with the most likely next step. On a card that asks for a review, the review action rests open
so the purpose of the card is visible without a pointer.

### Confirmation

`DoneLabel` is the past tense word shown after the action. Leave it out for actions that open something. The
confirmation stays for `ConfirmDuration`, which is 1.4 seconds by default, and then the item collapses unless the pointer
or the keyboard is still on it. The group announces the done word politely, and a repeated action announces it again.

```xml
<arc:ExpandingButtonGroupItem Label="Save" DoneLabel="Saved" />
```

### Async action

`AsyncAction` returns a `Task<bool>`. The item is busy until the task ends, its icon pulses in the motion levels that
allow it, and repeated activation is ignored. Returning `false`, or throwing, skips the confirmation. Without an async
action, `Click` and `Command` count as success.

```csharp
var archive = new ExpandingButtonGroupItem
{
    Label = "Archive",
    Icon = ArcIcons.Archive,
    DoneLabel = "Archived",
    AsyncAction = async () =>
    {
        await archiveService.ArchiveAsync();
        return true;
    }
};
```

### Size and tone

`Size` sets the group. `Medium` (the default) uses 36 px actions with 16 px icons. `Small` uses 28 px actions with 14 px
icons and a smaller label size, which suits a dense table row. `Large` is treated as `Medium`.

`Tone="Danger"` colors an expanded destructive action in the danger color. A disabled item stays focusable and still
reveals its label, but it does not run.

<Demo name="Small" />

### Events

`ActionInvoked` runs before the item's own `AsyncAction`, and it is raised for every action that runs. `e.Item` is the
action that runs. `e.Data` is the item itself for inline items, and the data object for an `ItemsSource`.

```csharp
message.ActionInvoked += (_, e) => log.Text = $"Action: {e.Item.Label}";
```

### Binding to a view model

The action set is usually fixed for a page, so define the items in XAML and bind their commands and async actions to the
view model. The view model owns the work; the group only reports the state.

```csharp
public interface ITicketService
{
    Task<bool> ArchiveAsync(string ticketId);

    Task<bool> DeleteAsync(string ticketId);
}

public partial class TicketViewModel : ObservableObject
{
    private readonly ITicketService _tickets;

    public TicketViewModel(ITicketService tickets)
    {
        _tickets = tickets;
    }

    [ObservableProperty]
    private string _ticketId = "tkt_2026_0412";

    public Func<Task<bool>> ArchiveAction => () => _tickets.ArchiveAsync(TicketId);

    public Func<Task<bool>> DeleteAction => () => _tickets.DeleteAsync(TicketId);

    [RelayCommand]
    private void Reply()
    {
        // Open the reply composer for TicketId.
    }
}
```

```xml
<arc:ExpandingButtonGroup Label="Ticket actions">
    <arc:ExpandingButtonGroupItem Label="Reply"
                                  Icon="{x:Static arc:ArcIcons.Reply}"
                                  Command="{Binding ReplyCommand}" />
    <arc:ExpandingButtonGroupItem Label="Archive"
                                  Icon="{x:Static arc:ArcIcons.Archive}"
                                  DoneLabel="Archived"
                                  AsyncAction="{Binding ArchiveAction}" />
    <arc:ExpandingButtonGroupItem Label="Delete"
                                  Icon="{x:Static arc:ArcIcons.Trash2}"
                                  DoneLabel="Deleted"
                                  Tone="Danger"
                                  AsyncAction="{Binding DeleteAction}" />
</arc:ExpandingButtonGroup>
```

- Nothing here is two-way. The items are configured once, and `ExpandedItem`, `IsExpanded`, `IsDone` and `IsBusy` are
  read-only properties that report state for styling or tests.
- `Command` runs as the action and counts as success, so a command that cannot fail needs no `AsyncAction`.
- A command that returns a result belongs in `AsyncAction`, because only the `Task<bool>` decides whether the
  confirmation shows.

### Pointer, touch and keyboard

The group expands the action the person is working with, and it follows the input that is in use.

- **Mouse**: the label moves to the action under the pointer. The pointer must travel 4 px into a neighbour before the
  expansion moves, so an action does not take over when it slides under a still pointer. When the pointer leaves the
  group, the expansion returns to the confirmed action, the focused action or the resting action.
- **Touch**: there is no hover. The first tap expands the action, and the second tap runs it. An action that is already
  expanded runs at once. A tap outside the group collapses a label that a first tap opened.
- **Keyboard**: focus expands the focused action. The group is a single Tab stop, and the arrow keys move between
  actions, so the expansion always follows focus.

### Layout and width

Each action has a fixed height, and its collapsed width is a square. The expanded width is the icon, the gap and the
longer of `Label` and `DoneLabel`. The label slot reserves that longer word from the start, so a confirmation does not
change the width. The row grows and shrinks as the expanded action changes, and its neighbours slide to make room.

Place the group at its start edge, and leave room for the widest expansion. For five Medium actions with short labels,
reserve about 260 to 280 px. A group that is laid out in a large container that reflows often should be placed on its
own row, because the width changes as the expansion moves.

### Example: ticket actions in a support inbox

A support agent reads a message on a ticket, and the group sits under the message. Reply rests open because it is the
most likely next step. Archive confirms with "Archived" after its task completes, and Delete is in the danger tone so its
expanded state is clearly different. The group is left aligned under the text it acts on.

![A support ticket message with an expanded Reply action and two collapsed actions](images/TicketActions.png)

```xml
<StackPanel Width="600" Spacing="14">
    <Border Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            BorderThickness="1"
            CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
            Padding="20">
        <StackPanel Spacing="12">
            <StackPanel Orientation="Horizontal" Spacing="8">
                <TextBlock Text="Maya Chen"
                           FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="21 Oct 2026, 09:14"
                           FontSize="{arc:ArcDesignTokenResource TypeSM}"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}"
                           VerticalAlignment="Center" />
            </StackPanel>
            <TextBlock Text="The March invoice still shows the old seat count. Can you check it before the 28th?"
                       TextWrapping="Wrap"
                       FontSize="{arc:ArcDesignTokenResource TypeBase}"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <arc:ExpandingButtonGroup Label="Ticket actions" Margin="0,4,0,0">
                <arc:ExpandingButtonGroupItem Label="Reply"
                                              Icon="{x:Static arc:ArcIcons.Reply}" />
                <arc:ExpandingButtonGroupItem Label="Archive"
                                              Icon="{x:Static arc:ArcIcons.Archive}"
                                              DoneLabel="Archived" />
                <arc:ExpandingButtonGroupItem Label="Delete"
                                              Icon="{x:Static arc:ArcIcons.Trash2}"
                                              DoneLabel="Deleted"
                                              Tone="Danger" />
            </arc:ExpandingButtonGroup>
        </StackPanel>
    </Border>
</StackPanel>
```

The message and the sender set the context, so the icons alone are enough for the three actions. The reply label is
visible because it is the resting action, which tells the agent what the group offers before any pointer moves.

### Content guidelines

- **Label**: a short verb in sentence case, such as "Reply", "Archive" or "Save". The label is the accessible name even
  when it is collapsed, so it must read clearly on its own.
- **Done label**: the past tense of the verb, one word, such as "Archived" or "Sent". It appears for about a second and
  a half, so it must be understood at a glance.
- **Icon**: choose a glyph that people already associate with the action, and keep it the same across the product.

| Instead of | Write |
|---|---|
| Archive this message | Archive |
| Done! | Archived |
| Click to send | Send |
| Mark as saved | Saved |

### Best practices

**Do**

- Put the most likely next action first and let it rest open, so the group explains itself before any pointer moves.
- Use the `Danger` tone for destructive actions, so the expanded state signals the consequence.
- Give every item an `AsyncAction` when the work can fail. A `false` result or an exception skips the confirmation, so a
  failed archive never claims success.
- Keep three to six actions. Fewer than three is better as buttons; more than six makes the expansions hard to scan.

**Avoid**

- Putting selection controls in the group. A group that remembers a choice is a `SegmentedControl`, and mixing the two
  confuses the expansion with the selection.
- Long labels. A label that is much longer than its icon widens the group on every expansion, so the row moves more than
  the person expects.
- Disabling the item the person needs most without explanation. A disabled item stays focusable and expands, but people
  still need the reason nearby, in the surrounding text.
- Relying on the done word alone for a change that matters. The check and the word last about 1.4 seconds, so confirm
  important results in the page as well.

### Common questions

**Why does the first enabled action show its label at rest?** That is the default resting action. A disabled first item is
skipped, and `DefaultExpandedItem` overrides the choice. If the item is not in the group, the group falls back to the
first enabled action.

**Why did my action run without a confirmation?** It has no `DoneLabel`, or its `AsyncAction` returned `false`, or it
threw. Only a `true` result from a task with a `DoneLabel` shows the confirmation.

**Can I run an action from code, such as a keyboard shortcut?** Call `ActivateAsync(item)` on the group. It runs the
action as a press would, and it does nothing for a disabled, busy or confirming item.

**Does the group collapse after a confirmation?** Yes, after `ConfirmDuration`, unless the pointer or the keyboard focus
is still on the item. A repeated action on the same item shows its done word again.

## API reference

<!-- api: ExpandingButtonGroup -->

<!-- api: ExpandingButtonGroupItem -->

## Accessibility

- The group has the ToolBar role, named by `Label`. The full label is the name even while the label is collapsed.
- The group is a single Tab stop. Left and Right arrow keys move between actions and wrap. Home and End move to the
  first and last action.
- Enter and Space run the focused action. Repeated activation is ignored while the action is busy.
- A disabled action keeps keyboard focus and reports as unavailable.
- A confirmation word is announced politely, and a repeated confirmation is announced again.
- The collapsed label is hidden from assistive technology, so the name is read once, not twice.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Enters the group on its last focused action, or the resting action; a second Tab leaves the group |
| `Left` / `Right` | Moves to the previous or next action and expands it; wraps at either end |
| `Home` / `End` | Moves to the first or last action |
| `Enter` / `Space` | Runs the focused action; ignored while the action is busy |

## Motion

- The focused or hovered action grows on a spring to show its label, and its neighbours slide aside.
- The label slides out from behind the icon with a short fade and a slight blur, and tucks back faster.
- On confirmation the icon draws a check and the label changes to the done word, which rises in while the old word moves out.
- Pointer movement must travel a few pixels into a neighbour before the label moves, so the group does not jump under a
  still pointer.
- Reduced motion changes the label and the width at once with a quick fade, and the check appears without drawing. Off
  makes the same change without any transition.
- A busy action pulses its icon. Reduced motion holds the icon at 60% opacity instead of pulsing.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Expansion | The width springs between the icon and the icon plus label; neighbours slide | The label and width change at once with a quick fade | Immediate |
| Label | Slides out from behind the icon with a blur: 240 ms with a short delay in, 120 ms out | Quick fade | Immediate |
| Confirmation | The check draws in 240 ms; the old word moves up and out as the new word rises in | The check appears without drawing; the word fades | Immediate |

## Related

- `FloatingButtonGroup` for separate soft buttons whose labels stay visible in a tray.
- `ButtonGroup` for related actions joined into one surface with dividers.
- `ActionButton` for one asynchronous submit with pending and success on the button.
- `DropdownMenu` for a longer list of commands.

| Component | Choose it when |
|---|---|
| `SegmentedControl` | One option stays selected, such as a view or a filter |
| `Button` | One or two actions have room for their labels |
| `ButtonGroup` | Related actions need visible labels joined into one surface |
| `FloatingButtonGroup` | Separate soft buttons with labels that stay visible in a tray |
| `ActionButton` | One submit needs its own pending and success states |
| `DropdownMenu` | There are more than about six commands, or the commands need full text |
