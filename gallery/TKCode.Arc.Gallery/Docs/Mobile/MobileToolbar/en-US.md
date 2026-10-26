---
title: Mobile toolbar
summary: The 44 px bottom action bar of a page: plain actions that share the width, or leading, center and trailing slots.
---

<Demo name="Basic" />

## When to use

`Toolbar` is the bar of actions that belong to the current screen, at the bottom where the thumb reaches. It acts on what the person is looking at, where `Tabbar` moves between areas.

- **Actions on one item**: share, edit, favorite and delete under a photo or a document.
- **A selection**: "Move", "Copy" and "Delete" once rows are selected, with the count in the middle.
- **A composer or a form**: a leading "Cancel" and a trailing "Send".

The live demos run inside a phone frame, a desktop simulation. How the bar sits above the home indicator on iOS and Android has not been verified; the page's safe area, set by the frame here, keeps it clear.

## When not to use

- **Moving between top-level areas**: use `Tabbar`.
- **Actions at the top of a screen**: use the slots of `Navbar`.
- **More than about five actions**: move the rest into a `Sheet`.

## Usage

Put the toolbar in a page's `Footer`. Items are plain `arcm:Button`s with `Variant="Plain"`, so the bar stays quiet; they share the width equally and each is at least 44 px high.

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Footer>
        <arcm:Toolbar>
            <arcm:Button Variant="Plain" Content="Share" />
            <arcm:Button Variant="Plain" Content="Edit" />
            <arcm:Button Variant="Plain" Content="Delete" />
        </arcm:Toolbar>
    </arcm:Page.Footer>
</arcm:Page>
```

### Slots

`Leading`, `Center` and `Trailing` place content at the start, the middle and the end of the bar instead of spreading items.

<Demo name="Slots" />

```xml
<arcm:Toolbar>
    <arcm:Toolbar.Leading>
        <arcm:Button Variant="Plain" Content="Cancel" />
    </arcm:Toolbar.Leading>
    <arcm:Toolbar.Trailing>
        <arcm:Button Variant="Plain" Content="Send" />
    </arcm:Toolbar.Trailing>
</arcm:Toolbar>
```

### Above a tab bar, shown and hidden

Stack the toolbar and a `Tabbar` in one `StackPanel` in `Footer`. `IsOpen` shows and hides the toolbar with a slide and a fade; bind it to the state that decides whether there is anything to act on.

<Demo name="WithTabbar" />

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Footer>
        <StackPanel>
            <arcm:Toolbar IsOpen="{Binding HasSelection}">
                <arcm:Button Variant="Plain" Content="Delete" />
            </arcm:Toolbar>
            <arcm:Tabbar SelectedIndex="0" />
        </StackPanel>
    </arcm:Page.Footer>
</arcm:Page>
```

A closed toolbar keeps its place in the layout so the content above does not jump. Set `IsVisible` to `False` to remove it.

### Tokens

`ToolbarToken` holds the geometry: `BarHeight` (44, hairline included), `BarPadding` (8 at the sides) and `BarBorderThickness` (a 1 px hairline above the bar). Colors and type come from the ArcDesign tokens.

## API reference

<!-- api: MobileToolbar -->

## Accessibility

Each action is a normal button with a 44 px touch target; give icon-only actions an accessible name. A closed toolbar does not catch touches.

## Motion

Closing slides the bar down by its own height while it fades out, in a short tween; opening slides it back up on a snappy spring. Toggling `IsOpen` again while it moves turns it around from where it is. The first frame has no motion, and under reduced motion the bar jumps. The buttons press and release as `MobileButton` does.

## Related

- `MobilePage`: puts the toolbar in its `Footer`.
- `MobileButton`: the usual content, with `Variant="Plain"`.
- `MobileTabbar`: the bar for moving between areas, often just below the toolbar.
- `MobileSheet`: for the actions that do not fit.
