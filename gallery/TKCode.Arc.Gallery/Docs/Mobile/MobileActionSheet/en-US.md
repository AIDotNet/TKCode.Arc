---
title: Mobile action sheet
summary: A bottom list of actions with an optional destructive row and a separate Cancel row, closed by choosing a row, tapping the scrim or pulling it down.
---

<Demo name="Basic" />

## When to use

`ActionSheet` is the mobile answer to a menu: a short list of actions on one subject that rises from the bottom edge.

- **Actions on an item**: share, add to an album, delete.
- **A choice that needs a way out**: the Cancel row is always under the list, apart from it, in easy reach of the thumb.
- **A destructive action** that should be easy to find but hard to hit by accident: mark the row `IsDestructive`.

The live demo runs inside a phone frame, a desktop simulation; touch feedback and the drag on a real device have not been verified here.

## When not to use

- **More than about six actions**: use a `Sheet` with a `ListGroup`, which can scroll and group.
- **A form or a task with several steps**: use a `Sheet`.
- **A confirmation of one action**: use a dialog.

## Usage

Put the action sheet in the page's `Overlay` slot and open it with `IsOpen`. Its content is the list of `ActionSheetItem` rows. Give each row a `Command`, or handle `ActionInvoked` once for the whole sheet; choosing a row closes the sheet.

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <arcm:ActionSheet Title="Photo" Message="IMG_2041.jpg" IsOpen="{Binding ShowOptions}">
            <arcm:ActionSheetItem Content="Share" Command="{Binding ShareCommand}" />
            <arcm:ActionSheetItem Content="Add to album" />
            <arcm:ActionSheetItem Content="Delete photo" IsDestructive="True" Command="{Binding DeleteCommand}" />
        </arcm:ActionSheet>
    </arcm:Page.Overlay>
</arcm:Page>
```

```csharp
void OnAction(object? sender, ActionSheetActionEventArgs e) => Log((string)e.Action.Content!);
```

`Title` and `Message` are optional and sit above the first row. `CancelText` is the label of the Cancel row (`"Cancel"`); set it to null to hide the row. Cancel closes the sheet without raising `ActionInvoked`. An action sheet is a `Sheet`: it closes on a scrim tap, a pull down or Escape (set `IsDismissible="False"` to stop that), and `SafeAreaBottom` keeps the Cancel row clear of the home indicator (see `MobileSheet`).

### Tokens

`ActionSheetToken` holds `PanelPadding` (the space around the two cards), `CancelMargin` (the gap above the Cancel card) and `HeaderPadding`; `ActionSheetItemToken` holds `RowHeight` (56) and `ContentPadding`. Colors, radii and type come from the ArcDesign tokens: actions use the accent color, destructive ones the danger color.

## API reference

<!-- api: MobileActionSheet -->

## Accessibility

Rows are buttons, at least 56 px high, so they are easy to hit and reachable with Tab and Enter or Space. Escape closes a dismissible action sheet and opening moves the focus to the panel. A destructive row is colored but also named by its label, so do not rely on color alone: say "Delete photo", not "Delete". Tapping the scrim is a shortcut; the Cancel row is the visible way out.

## Motion

The panel rises and leaves by the same code-driven motion value as `MobileSheet`: a drag writes the position on every move, a release hands the finger's speed to a spring (0.4 s smooth to open, 0.3 s to close), and the scrim follows the position each frame; a transition declared in the theme cannot follow a finger. Rows are rows, not buttons on a surface, so they do not scale: a finger on one shades it with a short fade of the background. With reduced motion the panel jumps and the shade changes at once.

## Related

- `MobileSheet`: the panel it is built on, for content that is not a list of actions.
- `MobilePage`: the `Overlay` slot holds the action sheet.
- `MobileButton`: the usual way to open it.
