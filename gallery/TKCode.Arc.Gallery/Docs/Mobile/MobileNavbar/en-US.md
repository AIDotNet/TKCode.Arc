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

### Back button

Set `ShowBack` to show a plain back button (a chevron and `BackText`, usually the previous screen's title) before `Leading`, and give it a `BackCommand`. Bar actions in `Trailing` should use `Variant="Plain"` so the title stays the focus.

```xml
<arcm:Navbar Title="Settings" ShowBack="True" BackText="Home" BackCommand="{Binding GoBack}" />
```


### Large title

Set `LargeTitle` on a root screen to show the title again as a large heading under the bar. Put the navbar in the `Header` of a `Page` with `IsScrollable="True"` and it collapses by itself as the content scrolls. Outside a scrolling page, drive `CollapseProgress` (0 to 1) yourself from a scroll offset divided by `LargeTitleExtent` (52): the large row shrinks and the bar's own title fades in, so the title is never shown twice at full size.

<Demo name="LargeTitle" />

```xml
<arcm:Page IsScrollable="True">
    <arcm:Page.Header>
        <arcm:Navbar Title="Photos" LargeTitle="True" />
    </arcm:Page.Header>
    <!-- long content -->
</arcm:Page>
```

### Tokens

`NavbarToken` holds the geometry: `BarHeight` (44, hairline included), `BarPadding` (8 at the sides), `SlotMinWidth` (44), `BarBorderThickness` (a 1 px hairline under the bar), `LargeTitleFontSize` (30) and `LargeTitleMargin`. Colors and type come from the ArcDesign tokens.

## API reference

<!-- api: MobileNavbar -->

## Accessibility

The title is plain text; give every screen one. The slot buttons are normal buttons with a 44 px touch target. Give a back button a name that says where it goes.

## Motion

A changed title rises out of a soft blur while the old one lifts away (P2, `ArcContentSwap` blur swap); the first title and reduced motion do not animate. The large title follows the scroll offset one to one and has no animation of its own. The buttons in the slots press and release as `MobileButton` does.

## Related

- `MobilePage`: puts the navbar in its `Header`.
- `MobileButton`: the usual content of the slots.
- `MobileTabbar`: the bottom bar.
