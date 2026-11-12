---
title: Share sheet
summary: Sharing in place: the Share button stretches into a card to copy the item's link, set who may open it, post it to a channel or send it to a few people, and shrinks into a confirmation once sent.
---

<Demo name="Preview" />

## When to use

`ShareSheet` is a Share button for a toolbar or a page header. Pressing it grows the button itself into a share panel:
one shape stretches from the pill into a card, so the panel reads as the button opening up rather than a new window.
The panel names the item, shows its link with a copy button and the access level the link carries, offers a few
channels to post to, and lists the people you share with most. Picking a person flies their avatar up into a chip, and
Send hands the list to your app. When the send succeeds the card shrinks to a short confirmation with Undo and Done.
On a phone-sized screen the same panel rises from the bottom as a sheet instead.

- **An item people pass around again and again**, such as a document, a board or a design, with the button in that
  item's own header.
- **One stop for the link and the people**: copying it, deciding who may open it and sending it to colleagues all happen
  in the same card.
- **Two or three posting targets**, such as email or a team chat, where each button reports its own progress and
  outcome.

## When not to use

- **Copying the address is the whole job**: put a copy button beside the link.
- **Per-person roles, or looking people up by address**: that is a form, so open a dialog for it.
- **A list of destinations with nobody to pick**: a popover or a dropdown menu does it with less.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ShareSheet Title="Quarterly review" Link="https://example.com/doc/quarterly-review" Access="team">
        <arc:ShareSheet.AccessLevels>
            <arc:ShareSheetAccess Value="private" Label="Private" Description="Only the people you add"
                                  Glyph="{x:Static arc:ArcIcons.Lock}" />
            <arc:ShareSheetAccess Value="team" Label="Your team" Description="Everyone on the team can comment"
                                  Glyph="{x:Static arc:ArcIcons.Users}" />
        </arc:ShareSheet.AccessLevels>
        <arc:ShareSheet.Channels>
            <arc:ShareSheetChannel Id="email" Label="Email" DoneLabel="Sent" Glyph="{x:Static arc:ArcIcons.Mail}" />
        </arc:ShareSheet.Channels>
        <arc:ShareSheet.People>
            <arc:ShareSheetPerson Id="ada" Name="Ada Moreno" />
            <arc:ShareSheetPerson Id="ravi" Name="Ravi Kapoor" />
        </arc:ShareSheet.People>
    </arc:ShareSheet>
</UserControl>
```

`ShareSheet` is in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`. Layout space goes
to the Share button alone, because the panel is drawn on the overlay layer on top of the page. Keep some space free
beneath the button, or, close to an edge, pick an `Align` that opens away from it. `IsOpen` (two-way), `Open()` and
`Close()` control it from code.

- `Title` names the item, and the panel's heading shows it as Share “Title”. `Link` is the URL: it gets a row of its
  own, and Copy places it on the clipboard (null drops the row).
- `AccessLevels` holds `ShareSheetAccess` items (`Value`, `Label`, `Description`, `Glyph`). `Access` is the chosen
  value, two-way; it starts at the first level.
- `Channels` holds `ShareSheetChannel` items (`Id`, `Label`, `DoneLabel`, `Glyph`). After a post the button swaps
  `Label` for `DoneLabel` for two seconds, so pick words of similar length.
- `People` holds `ShareSheetPerson` items (`Id`, `Name`, `Avatar`). Without an `Avatar` picture the sheet draws a soft
  gradient seeded by the id, so a person always looks the same. `SelectedPeople` lists who is picked; `Pick` and
  `Unpick` change it from code.

### Sending and posting

Give the sheet your server calls and it handles the waiting. `SendHandler` receives the people and the access level;
the Send button spins while it runs. If it throws, an error line appears over the button and the panel stays open with
the people still picked; when it completes, `Sent` is raised and the panel folds into the confirmation. Undo on the
confirmation goes back to the panel with the same people and raises `SendUndone`, so your app can take the send back.
`ChannelHandler` works the same way for one channel button: it spins, then reads the channel's `DoneLabel`, or Failed
when the handler throws.

```csharp
sheet.SendHandler = async (submission, token) =>
    await api.ShareAsync(submission.People.Select(p => p.Id), submission.Access?.Value, token);
sheet.ChannelHandler = (channel, token) => api.PostAsync(channel.Id, token);
```

<Demo name="Channels" />

### Where the panel grows

`Align` sets the anchor the panel opens from. With `End` (the default) the button's top-right corner stays fixed and the
panel extends to the left and downward; `Start` fixes the top-left corner and extends to the right and downward;
`Center` fixes the button's center, so the panel expands and collapses symmetrically. A panel that would cross the
window's edge is shifted back inside.

<Demo name="Align" />

### People only, and the phone sheet

Rows appear only for what you set: without `Link`, `AccessLevels` or `Channels` the matching row is left out. When the
viewport is 520 px wide or narrower (the phone preview counts), the panel becomes a sheet docked to the bottom edge,
with the page behind it dimmed; set `SheetOnPhones="False"` to keep the floating panel at those widths as well.

<Demo name="People" />

### Best practices

**Do**

- Offer in `People` only the few people this item usually goes to, most likely first.
- Return the task from your handlers so the sheet can show progress, errors and results.
- Use short channel names and done words of a similar length.

**Avoid**

- Loading a whole address book into `People`: the row is for quick picks, not for looking people up.
- More than three or four channels: the buttons share one row.

## API reference

<!-- api: ShareSheet -->

## Accessibility

- The Share button exposes the panel's expanded state. Focus follows the flow: it lands on Copy (or the first control
  there is) when the panel opens, on Done after a send, and back on the Share button when everything closes. Pressing
  somewhere else on the page dismisses the panel, and focus stays with whatever was pressed.
- The access list is a list of options; the chosen one is marked selected and is the list's only tab stop, and the
  arrow keys move through the options.
- Each recent person is a toggle button carrying their full name, and every chip offers a Remove button that names the
  person it removes.
- Screen readers hear each step through a polite live region: the link being copied, a person added or removed, a
  channel's outcome and the names a share reached. A send that fails also gets an alert line.

### Keyboard interactions

| Key | Action |
|---|---|
| Enter or Space | On the Share button, opens the panel; anywhere else, activates the focused control. |
| Tab | Moves through the close button, Copy, the access row, the channels, the people, the chips and Send. |
| Up, Down, Left, Right | With the access list open, step through its options; after the last one they start again at the first. |
| Home / End | Move to the first or last access option. |
| Escape | If the access list is open, closes it; otherwise collapses the panel into the Share button. |

## Motion

- Share button, panel and post-send confirmation are drawn as one surface; whenever it changes face, a spring carries
  its size and rounding to the new shape. The contents swap with a brief blur: what leaves shrinks slightly as it fades, what
  arrives grows in from 94%. From the panel to the confirmation and back, the contents slide sideways instead.
- Picking someone sends a copy of their avatar travelling to the new chip. It is drawn above the panel, so the edge of
  the chips row cannot cut it off. A removed chip fades, and the chips behind it shift over to fill the space.
- The number on the Send button ticks up or down as people are added or removed. Pressing Send before anyone is chosen
  gives the recipients row a short, stiff shake.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Open, confirm, fold | Size spring, blur and scale crossfade | Size jumps, faces fade | Instant |
| Panel ↔ confirmation | Faces slide 32 px and blur across | Faces fade | Instant |
| Pick a person | Avatar flies to its chip | Chip fades in | Chip appears |
| Count in Send | Digits roll | Swap | Swap |
| Send with nobody picked | Row nudges | Nothing moves | Nothing moves |
| Phone sheet | Slides up, follows the grabber | Fades in | Appears |

## Responsive behavior

- Widths: 384 px for the panel and 304 px for the confirmation, each capped at the viewport width minus 32 px, which
  still leaves room on a 320 px screen.
- Up to 520 px of viewport width the panel is a bottom sheet inset 8 px from the sides and the bottom. Drag the grabber
  down past 120 px, or flick it down, to close it; a shorter drag springs back. Escape, a tap on the dimmed page and Done
  close it too.
- Hover tints follow a mouse or a pen only; a touch never leaves a hover state behind.

## Related

| Component | Choose it when |
|---|---|
| `ShareSheet` | One item is shared by link, to channels and to a few people |
| `ShareAccess` | People and their roles are managed in a dialog |
| `CopyButton` | Copying the link is all that is needed |
| `ActionMorph` | A button grows into a small inline form for another action |
