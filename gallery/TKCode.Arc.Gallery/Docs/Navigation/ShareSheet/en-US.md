---
title: Share sheet
summary: Share one item. Copy its link, pick a person or a destination, or run an action, in a sheet from the bottom on phones and a popover on desktop.
---

<Demo name="Preview" />

## When to use

`ShareSheet` gathers every way to pass one item on in a single place. At the top is the item's link with a copy
button; below it a strip of the people you share with most, each a soft circle with their initials; then a strip of
destinations such as email, messages or a team space; and last a few actions such as embedding or downloading. On a
phone it rises from the bottom as a sheet that can be dragged away; on a wider screen it opens as a popover under the
button that asked for it. The sheet only reports what was picked; your app does the sending.

- **Sharing a document, a photo set or a page** with people and apps.
- **Invites**: send a link to a few people or to a calendar.
- **Exports** that sit next to sharing: embed code, download, print.

## When not to use

- **Managing who has access** (roles, permissions, link settings): that needs a settings dialog.
- **A single destination**: a plain "Copy link" button is quicker.
- **Long contact lists**: the strips show your frequent people; searching everyone belongs in a picker.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ShareSheet Link="https://example.com/a/autumn-hike" Subtitle="Autumn hike photos">
        <arc:Button Content="Share" Variant="Secondary" />
        <arc:ShareSheet.People>
            <arc:ShareSheetTarget Name="Mira Okafor" />
            <arc:ShareSheetTarget Name="Tomas Reyes" />
        </arc:ShareSheet.People>
        <arc:ShareSheet.Destinations>
            <arc:ShareSheetTarget Name="Email" Glyph="{x:Static arc:ArcIcons.Mail}" />
        </arc:ShareSheet.Destinations>
        <arc:ShareSheet.Actions>
            <arc:ShareSheetAction Label="Download a copy" Glyph="{x:Static arc:ArcIcons.Download}" />
        </arc:ShareSheet.Actions>
    </arc:ShareSheet>
</UserControl>
```

`ShareSheet` is in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`. Its content is
the trigger: a button there opens the sheet. `Open()`, `Close()`, `ShowAt(anchor)` and the two-way `IsOpen` control
it from code.

`People` and `Destinations` are lists of `ShareSheetTarget`: a `Name`, an optional `Detail` read by assistive
technology, an optional `Glyph` (drawn instead of initials) and a `Tag` for your own value. `Actions` is a list of
`ShareSheetAction` with a `Label`, a `Glyph` and a `Command`. `Link` adds the link row with its copy button; leave it
null to hide the row. Groups with nothing in them are not shown.

### Knowing what was picked

`Shared` is raised with the target in `e.Target` and whether it is a person in `e.IsPerson`, and the sheet closes
unless `ClosesOnShare` is false. `ActionInvoked` is raised for an action before its command runs. `LinkCopied` is
raised after the copy button has put the link on the clipboard.

### Where it opens

`Presentation` is `Auto` by default: a bottom sheet on a viewport narrower than 640 px, a popover otherwise.
`Popover` and `Sheet` choose one always.

<Demo name="Sheet" />

### Best practices

**Do**

- Show a handful of the people this person shares with most, in that order.
- Set `Subtitle` to the item's name, so it is clear what is being shared.
- Confirm a share in your app after `Shared`, for example with a toast.

**Avoid**

- More than about eight people or destinations: the strips scroll, but the first few are what people see.
- Using `Actions` for things unrelated to the item.

## API reference

<!-- api: ShareSheet -->

## Accessibility

- The trigger reports whether the sheet is expanded. The popover or the sheet takes focus when it opens (the copy
  button, else the first person) and gives it back to the trigger when it closes.
- People and destinations are buttons named by `Name`, with `Detail` as help text; actions are menu items named by
  `Label`. All can be invoked by assistive technology.
- Each strip and the list of actions is one stop in the tab order; the arrow keys move inside it. Keyboard focus shows
  a ring inside the tile or row.

### Keyboard interactions

| Key | Action |
|---|---|
| Tab | Moves between the copy button, the people, the destinations and the actions. |
| Left / Right | Moves along a strip of people or destinations. |
| Up / Down | Moves through the actions. |
| Home / End | Moves to the first or last item of the group. |
| Enter or Space | Picks the focused person, destination or action. |
| Escape | Closes the sheet and returns focus to the trigger. |

## Motion

- As a popover it comes from the trigger's side with a short fade and a settling spring; as a sheet it rises from the
  bottom of the window and can be flung down to close. Both are the library's own popover and bottom sheet.
- The copy button swaps its label to "Copied" with a check, then back.
- Tiles and rows draw themselves; hovering and focusing only repaint them.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Popover | Fades and settles from the trigger | Fades | Appears |
| Sheet | Rises, follows drags | Fades | Appears |
| Copy label | Blur swap | Crossfade | Swap |

## Responsive behavior

- Below 640 px of viewport width (the phone preview included) the sheet rises from the bottom and spans the width; its
  height fits the content, up to 92% of the window.
- As a popover the panel is 320 px wide.
- The strips scroll sideways when they hold more than fits, and the focused tile is scrolled into view.

## Related

- `Popover` and `BottomSheet` are the two surfaces the sheet opens in.
- `CopyButton` is the copy button of the link row.

| Component | Choose it when |
|---|---|
| `ShareSheet` | One item can go to people, apps or a link |
| `CopyButton` | Copying the link is the only way to share |
| `Dialog` | Sharing needs access settings or a message |
