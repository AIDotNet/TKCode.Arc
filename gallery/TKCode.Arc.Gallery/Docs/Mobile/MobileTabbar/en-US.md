---
title: Mobile tabbar
summary: The bottom tab bar of a page: icon-over-label tabs that share the width, with the selected one highlighted.
---

<Demo name="Basic" />

## When to use

`Tabbar` is the bottom navigation of a mobile screen: a few top-level areas the person switches between with a thumb.

- **Top-level areas of an app**: Home, Search, Alerts and Profile, always in reach.
- **Two to five destinations**: the items share the width equally, so keep the label short.

Each `TabbarItem` shows an `Icon` above its label. The live demo runs inside a phone frame, a desktop simulation; touch feedback on a real device has not been verified here.

## When not to use

- **Views of one object**: use a segmented control or tabs inside the screen.
- **More than five destinations**: move the rest into a "More" screen or a menu.
- **A desktop sidebar**: use `Sidebar` from the desktop package.

## Usage

Put `TabbarItem`s in the bar, and set `SelectedIndex` (or `SelectedItem`) to the open one. `Tabbar` derives from Avalonia's `ListBox`, so selection, `SelectionChanged` and binding to `ItemsSource` work as they do there. Use the bar as a page's `Footer`.

```xml
<arcm:Tabbar xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
             SelectedIndex="0">
    <arcm:TabbarItem Content="Home" Icon="{x:Static arc:ArcIcons.House}" />
    <arcm:TabbarItem Content="Search" Icon="{x:Static arc:ArcIcons.Search}" />
    <arcm:TabbarItem Content="Profile" Icon="{x:Static arc:ArcIcons.User}" />
</arcm:Tabbar>
```

The selected item uses the foreground color and the others the muted text color. A finger on an item dims it; there is no hover state.

### Tokens

`TabbarToken` holds the bar's geometry: `BarHeight` (49) and `BarBorderThickness` (a 1 px hairline above it). `TabbarItemToken` holds an item's: `TouchTarget` (44), `IconExtent` (24), `IconGap` (2), `PressedOpacity` (0.6) and `ItemPadding` (4). Colors come from the ArcDesign tokens.

## API reference

<!-- api: MobileTabbar -->

<!-- api: MobileTabbarItem -->

## Accessibility

Items are selectable list items with a label, so they are named by their content. Keep the labels, so the icon is never the only name. Each item is at least 44 px tall.

## Motion

One accent pill glides between the items on a spring (P7, `GlidingIndicator`): it fades in where it lands the first time, snaps instead of gliding when the bar is resized, and jumps with reduced motion. The icon and label colors change on the fast curve. A finger dims the item and shrinks its content to 0.96 on a snappy spring (P1). With reduced motion nothing scales and everything switches at once.

## Related

- `MobilePage`: puts the tabbar in its `Footer`.
- `MobileNavbar`: the top bar.
