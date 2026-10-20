---
title: Expanding button group
summary: Icon buttons in a compact group: the one you point at or focus grows to reveal its label while its neighbours slide aside, and an action confirms in place.
---

## When to use

- Three to six related actions on a message, file or card, such as Reply, Archive and Delete.
- Compact icon actions where each action needs a visible label only while it is the focus.
- An action that should confirm in place with a past tense word, such as Archive to Archived.

## When not to use

- A choice that stays selected: use `SegmentedControl` from Toggles.
- One or two actions with enough room: use a labelled `Button`.
- More than about six actions: use `DropdownMenu` from Menus.
- Icon actions that need labels all the time: use `FloatingButtonGroup` or `ButtonGroup`.

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

### Icons and the resting action

Set `Icon` to a 16 by 16 Lucide geometry in code. The group shows one label at rest: the first enabled action by
default. `DefaultExpandedItem` picks another one.

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

### Confirmation

`DoneLabel` is the past tense word shown after the action. Leave it out for actions that open something. The
confirmation stays for `ConfirmDuration`, which is 1.4 seconds by default.

```xml
<arc:ExpandingButtonGroupItem Label="Save" DoneLabel="Saved" />
```

### Async action

`AsyncAction` returns a `Task<bool>`. The item is busy until the task ends. Returning `false`, or throwing, skips the
confirmation. Without an async action, `Click` and `Command` count as success.

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

### Tone and disabled items

`Tone="Danger"` colors an expanded destructive action in the danger color. A disabled item stays focusable and still
reveals its label, but it does not run.

```xml
<arc:ExpandingButtonGroup Label="File actions" Size="Small">
    <arc:ExpandingButtonGroupItem Label="Download" DoneLabel="Downloaded" />
    <arc:ExpandingButtonGroupItem Label="Lock" IsDisabled="True" />
    <arc:ExpandingButtonGroupItem Label="Delete" Tone="Danger" DoneLabel="Deleted" />
</arc:ExpandingButtonGroup>
```

### Events

`ActionInvoked` runs before the item's own `AsyncAction`. `e.Item` is the action that runs.

```csharp
message.ActionInvoked += (_, e) => log.Text = $"Action: {e.Item.Label}";
```

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

## Motion

- The focused or hovered action grows on a spring to show its label, and its neighbours slide aside.
- The label slides out from behind the icon with a short fade and a slight blur, and tucks back faster.
- On confirmation the icon draws a check and the label changes to the done word, which rises in while the old word moves out.
- Pointer movement must travel a few pixels into a neighbour before the label moves, so the group does not jump under a
  still pointer.
- Reduced motion changes the label and the width at once with a quick fade, and the check appears without drawing. Off
  makes the same change without any transition.

## Related

- `FloatingButtonGroup` for separate soft buttons whose labels stay visible in a tray.
- `ButtonGroup` for related actions joined into one surface with dividers.
- `ActionButton` for one asynchronous submit with pending and success on the button.
- `DropdownMenu` for a longer list of commands.
