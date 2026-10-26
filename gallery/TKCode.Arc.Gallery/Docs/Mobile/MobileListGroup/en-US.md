---
title: Mobile list
summary: Grouped rows for settings and menus: icon, title, subtitle, a value or control at the end and a chevron.
---

<Demo name="Basic" />

## When to use

`ListGroup` and `ListItem` lay out a screen of rows: settings, menus and account screens, where each row is a title the person can tap.

- **Settings**: group related rows under a short `Header`, with a `Footer` that explains them.
- **A menu of destinations**: rows with an `Icon` and a chevron that open other screens.
- **A value or a switch at the end of a row**: put it in `Trailing`.

The live demo runs inside a phone frame, a desktop simulation; touch feedback on a real device has not been verified here.

## When not to use

- **Long or endless data**: a virtualized list is the right tool, and this one is not yet built.
- **Cards with images and several actions**: build them as their own layout.
- **A desktop menu**: use the desktop menu controls; rows here have no hover state.

## Usage

Put `ListItem`s in a `ListGroup` and the group in a scrolling `Page`. The item's content is its title. `ListItem` is a button, so give it a `Command` or handle `Click`; a finger on it shades the row.

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
           xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
           IsScrollable="True">
    <arcm:ListGroup Header="Account">
        <arcm:ListItem Icon="{x:Static arc:ArcIcons.User}" Content="Profile" Subtitle="Ada Lovelace" ShowChevron="True" />
        <arcm:ListItem Content="Weekly summary" Trailing="Sundays" ShowChevron="True" />
    </arcm:ListGroup>
</arcm:Page>
```

The last row of a group has no separator under it.

### Tokens

`ListItemToken` holds a row's geometry: `RowHeight` (48), `ContentPadding` (16 by 10), `IconExtent` (22), `IconGap` (12), `TrailingMargin`, `ChevronExtent` (16) and `SeparatorInset` (16). `ListGroupToken` holds the group's: `GroupPadding` (16 by 8), `CardBorderThickness` (1), `HeaderMargin` and `FooterMargin`. Colors, radii and type come from the ArcDesign tokens.

## API reference

<!-- api: MobileListGroup -->

<!-- api: MobileListItem -->

## Accessibility

A row is a button named by its title and subtitle. Give every row a title, so an icon is never its only name. Each row is at least 48 px tall, above the 44 px touch target.

## Motion

A finger on a row shades its background on the fast curve; with reduced motion the shade appears at once.

## Related

- `MobilePage`: a scrolling page holds the groups.
- `MobileSwitch`: the usual `Trailing` control for a setting.
- `MobileNavbar`: the top bar, with a large title for a root settings screen.
