---
title: Mobile sheet
summary: A bottom sheet that rises over a page with a scrim and can rest at several heights, closed by tapping the scrim, pulling the handle down or flicking it.
---

<Demo name="Basic" />

<Demo name="Detents" />

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

### Detents

Set `Detents` (fractions of the sheet's height, parsed from text such as `"0.4, 0.9"`) and the sheet rests at several heights: a peek, then the full view. It opens at `InitialDetentIndex` (0, the lowest). `DetentIndex` says where it rests and `DetentChanged` is raised when it comes to rest on another one. `SnapTo(index)`, `Expand()` and `Collapse()` move an open sheet from code.

```xml
<arcm:Sheet Title="Nearby" Detents="0.4, 0.9" InitialDetentIndex="0">
    ...
</arcm:Sheet>
```

```csharp
nearby.DetentChanged += (_, e) => Log($"{e.Index}: {e.Fraction}");
```

A pull follows the finger one to one and stretches like a rubber band past the tallest detent. On release the sheet looks 0.2 s ahead at the finger's speed and rests on the nearest detent, or closes when that is nearest to the bottom; a flick faster than 500 px/s always moves at least one detent. A single value is a fixed height. The content scrolls only at the tallest detent: below it a finger or the wheel raises the sheet instead, and at the top of the content a pull down moves the sheet again. Without `Detents` the sheet keeps its single content-sized height.

### Bottom safe area

The sheet does not read the platform. Give it the space under the content with `SafeAreaBottom` (a number in px), for example bound to the page's safe area:

```xml
<arcm:Page x:Name="Screen">
    <arcm:Page.Overlay>
        <arcm:Sheet SafeAreaBottom="{Binding SafeArea.Bottom, ElementName=Screen}" Title="Share">
            ...
        </arcm:Sheet>
    </arcm:Page.Overlay>
</arcm:Page>
```

### Tokens

`SheetToken` holds `ScrimColor` (the scrim at full opacity, different in light and dark), `PanelRadius` (16 on the top corners), `GrabberWidth` (36), `GrabberHeight` (5) and `HandlePadding`. Colors, radii and type come from the ArcDesign tokens.

## API reference

<!-- api: MobileSheet -->

## Accessibility

Escape closes a dismissible sheet. Give the sheet a `Title`: it is the first text in the handle area. Opening moves the focus to the panel. Tapping the scrim is a shortcut, not the only way: keep a visible way to close inside the sheet when it holds a form. The handle area drags with any pointer, and the content drags the sheet by touch only, so a mouse wheel or a scroll bar never moves it. With detents, `DetentIndex` and `DetentChanged` tell the app which height the sheet rests at.

## Motion

The panel's position is a code-driven motion value: a drag writes it on every move, and on release the finger's speed goes into a spring (0.4 s smooth to open, 0.3 s to close), which a transition declared in the theme cannot do. The scrim's opacity follows the panel's position each frame: full at the tallest detent, a little lighter at the lowest of several, clear when closed. Moving between detents is one spring from wherever the panel is, so a new tap or drag interrupts it without a seam. With reduced motion the panel jumps to the detent instead of gliding.

## Related

- `MobilePage`: the `Overlay` slot holds the sheet.
- `MobileListGroup`: the usual content of an action sheet.
- `MobileButton`: the usual way to open it.
- `MobileActionSheet`: a ready-made list of actions with a Cancel row.
