---
title: Mobile dialog
summary: A centered alert or confirmation dialog with a title, a message and up to three actions, over a scrim.
---

<Demo name="Basic" />

## When to use

`Dialog` stops the person for one decision that cannot wait: confirm a deletion, accept or refuse a permission, pick one of a few ways to continue.

- **A confirmation** before something that cannot be undone, with the destructive action marked in the Danger color.
- **An alert** that must be read before anything else happens.
- **A short choice** of two or three actions with no extra content.

The live demo runs inside a phone frame, a desktop simulation; touch feedback on a real device has not been verified here.

## When not to use

- **A list of choices or a form**: use `Sheet`, which rises from the bottom where a thumb reaches it.
- **A message that needs no answer**: use `Toast`.
- **Anything the person may want to undo afterwards**: do the thing and offer undo in a toast instead of asking first.

## Usage

Put the dialog in the page's `Overlay` slot and open it with `IsOpen`. The action buttons are the dialog's items; use `Button`s.

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <arcm:Dialog Title="Delete this photo?"
                     Message="This cannot be undone."
                     IsOpen="{Binding ConfirmDelete}">
            <arcm:Button Content="Cancel" Variant="Tinted" />
            <arcm:Button Content="Delete" arcm:Dialog.IsDestructive="True" />
        </arcm:Dialog>
    </arcm:Page.Overlay>
</arcm:Page>
```

`IsOpen` binds two ways. Pressing an action button runs its own `Click` handler or `Command` and then closes the dialog; set `CloseOnAction="False"` to close it yourself, for example after a request finishes.

Two actions share a row when each fits its half of the panel; three actions, or labels too wide for the row, stack at the full width. The panel is at most 320 wide, with 24 kept free at each edge.

By default only an action closes the dialog. Set `IsDismissible="True"` to let a tap on the scrim and Escape close it as well, which suits a dialog with a harmless way out.

`Dialog.IsDestructive="True"` on a button draws it with the Danger color: a filled button becomes a Danger pill, a tinted one Danger text on a soft pill, a plain one Danger text.

### Several overlays on one page

`Page.Overlay` holds one element. To use a dialog together with a sheet or a toast host, put them in a `Grid` in that slot, the toast last so it shows above the others, as the demo does with two dialogs.

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <Grid>
            <arcm:Sheet Title="Share" />
            <arcm:Dialog Title="Delete?" />
            <arcm:Toast />
        </Grid>
    </arcm:Page.Overlay>
</arcm:Page>
```

Each overlay is transparent to touches while it is closed.

### Tokens

`DialogToken` holds `ScrimColor` (the scrim at full opacity, different in light and dark), `PanelMaxWidth` (320), `PanelMargin` (24) and `PanelPadding`. Colors, radii and type come from the ArcDesign tokens.

## API reference

<!-- api: MobileDialog -->

## Accessibility

Escape closes a dismissible dialog. Opening moves the focus to the dialog. Give it a `Title` that states the decision as a question and name the actions with verbs ("Delete", "Cancel"), not "Yes" and "No". The buttons are at least 44 by 44. A dialog that cannot be dismissed always has a way out: include a cancel action.

## Motion

The scrim fades in while the panel scales up from 0.94 and fades in on a snappy spring, one value driving both. Closing is faster, 0.16 s on an exit curve with no overshoot, and a dialog closed while it is still appearing reverses from where it is. It is driven from code because the dialog must keep catching touches until its exit has landed, which a theme transition cannot signal. With reduced motion the panel only fades, without scaling, in 0.12 s; with motion off it appears and disappears at once.

## Related

- `MobilePage`: the `Overlay` slot holds the dialog.
- `MobileButton`: the actions, and the usual way to open it.
- `MobileSheet`: for choices and forms.
- `MobileToast`: for messages that need no answer.
