---
title: Mobile sheet
summary: A bottom sheet that rises over a page with a scrim, closed by tapping the scrim, pulling the handle down or flicking it.
---

<Demo name="Basic" />

## When to use

`Sheet` is the mobile answer to a popup: a panel that rises from the bottom edge, holds a short task and goes away.

- **Choices and actions**: share options, a list of actions, a filter.
- **A short form or detail** that should not leave the screen the person is on.
- **Anything a desktop app would put in a dropdown or a menu**: on a phone a sheet is easier to reach and to hit.

The live demo runs inside a phone frame, a desktop simulation; touch feedback and the drag on a real device have not been verified here.

## When not to use

- **A step in a flow with its own back stack**: that is a screen, not a sheet.
- **A short confirmation**: use a dialog.
- **Content taller than the screen**: the sheet stops at 90 percent of the page and scrolls; a long task wants a full screen.

## Usage

Put the sheet in the page's `Overlay` slot, which covers the header and the footer, and open it with `IsOpen`. The sheet sizes to its content; the person closes it by tapping the scrim, pulling the handle down, flicking it or pressing Escape.

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <arcm:Sheet Title="Share" IsOpen="{Binding ShowShare}">
            <arcm:ListGroup>
                <arcm:ListItem Content="Send to a person" ShowChevron="True" />
                <arcm:ListItem Content="Copy link" />
            </arcm:ListGroup>
        </arcm:Sheet>
    </arcm:Page.Overlay>
</arcm:Page>
```

`IsOpen` binds two ways, so a gesture that closes the sheet writes `false` back. Set `IsDismissible="False"` for a sheet that only a button can close; it then resists a downward pull like a rubber band.

A pull follows the finger one to one. On release the sheet looks ahead 0.2 s at the finger's speed: past the middle it closes, and a downward flick faster than 500 px/s closes it from anywhere. Otherwise it springs back to its open position.

The bottom safe area is not accounted for yet: on a phone with a home indicator, add the space to the sheet's `Padding`.

### Tokens

`SheetToken` holds `ScrimColor` (the scrim at full opacity, different in light and dark), `PanelRadius` (16 on the top corners), `GrabberWidth` (36), `GrabberHeight` (5) and `HandlePadding`. Colors, radii and type come from the ArcDesign tokens.

## API reference

<!-- api: MobileSheet -->

## Accessibility

Escape closes a dismissible sheet. Give the sheet a `Title`: it is the first text in the handle area. Opening moves the focus to the panel. Tapping the scrim is a shortcut, not the only way: keep a visible way to close inside the sheet when it holds a form. The handle area is the only part that drags, so scrolling the content never moves the sheet.

## Motion

The panel's position is a code-driven motion value: a drag writes it on every move, and on release the finger's speed goes into a spring (0.4 s smooth to open, 0.3 s to close), which a transition declared in the theme cannot do. The scrim's opacity follows the panel's position. With reduced motion the panel jumps instead of gliding.

## Related

- `MobilePage`: the `Overlay` slot holds the sheet.
- `MobileListGroup`: the usual content of an action sheet.
- `MobileButton`: the usual way to open it.
