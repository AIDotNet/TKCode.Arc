---
title: Mobile navbar
summary: The 44 px top bar of a page: a leading slot, a centered title and a trailing slot for actions.
---

<Demo name="Basic" />

## When to use

`Navbar` is the top bar of a mobile screen. It shows where the person is (the `Title`), and offers the way back (`Leading`) and the screen's actions (`Trailing`).

- **A pushed screen**: a back button in `Leading`, the screen's name as the title and a "Done" or "Edit" button in `Trailing`.
- **A root screen with one action**: only a title and a button at the end.
- **A plain title bar**: only a title, with empty slots that keep it centered.

The live demos run inside a phone frame, a desktop simulation. How the bar sits under a real status bar or notch on Android and iOS has not been verified; the page's safe area, set by the frame here, keeps it clear of them.

## When not to use

- **Navigation between top-level areas**: use `Tabbar`, which stays at the bottom where the thumb reaches.
- **A desktop header with menus and search**: use the desktop header blocks and controls.
- **A toolbar with many actions**: a navbar has room for one or two touch targets at each end.

## Usage

Put the navbar in a page's `Header`. Both slots take any content, usually `arcm:Button`s, and keep a minimum width of 44 so a button stays touchable.

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Header>
        <arcm:Navbar Title="Settings">
            <arcm:Navbar.Leading>
                <arcm:Button Content="Back" />
            </arcm:Navbar.Leading>
            <arcm:Navbar.Trailing>
                <arcm:Button Content="Done" />
            </arcm:Navbar.Trailing>
        </arcm:Navbar>
    </arcm:Page.Header>
</arcm:Page>
```

### Title only

Without slot content the title stays centered.

<Demo name="TitleOnly" />

### Tokens

`NavbarToken` holds the geometry: `BarHeight` (44), `BarPadding` (8 at the sides), `SlotMinWidth` (44) and `BarBorderThickness` (a 1 px hairline under the bar). Colors and type come from the ArcDesign tokens.

## API reference

<!-- api: MobileNavbar -->

## Accessibility

The title is plain text; give every screen one. The slot buttons are normal buttons with a 44 px touch target. Give a back button a name that says where it goes.

## Motion

The bar has no motion of its own; the buttons in its slots press and release as `MobileButton` does.

## Related

- `MobilePage`: puts the navbar in its `Header`.
- `MobileButton`: the usual content of the slots.
- `MobileTabbar`: the bottom bar.
