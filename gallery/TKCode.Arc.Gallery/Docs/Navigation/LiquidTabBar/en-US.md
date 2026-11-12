---
title: Liquid tab bar
summary: Glyph tabs for an app's main sections. A solid lens marks the chosen tab and travels to the next one, painting the tab beneath it in reverse colors so its outline glyph turns solid; it can also be dragged or flicked.
---

<Demo name="Preview" />

## When to use

`LiquidTabBar` switches between a few sections of equal rank, each named by a glyph and a short label. The selected tab
sits under a lens: a pill in the foreground color that is exactly as large as the tab. A new selection sends the lens
gliding over, and its width changes to the new tab's while it travels, without stretching. Under the lens the tab is
painted a second time with foreground and background swapped, which turns an outline glyph into a filled one and
flips a badge's colors. By default only the selected tab shows its label, and two labels never overlap: the previous
one is gone before the lens departs, and the next one appears when it lands.

- **The main areas of an app**: a bottom bar on a phone layout, or a top bar in a compact window.
- **A few sections that people revisit often**, where a glyph is enough once the label has been seen.
- **Sections with counts**, such as unread messages or due tasks, shown as badges on the glyphs.

## When not to use

- **Tabs that are just words**, each with a panel in the page: `Tabs` handles those and scrolls when there are many.
- **Choosing a view or a filter** (day, week, month): `SegmentedControl` is smaller.
- **A website's header with menus of links**: `MorphNav`.
- **More than five or six sections**: the bar does not scroll or wrap, and glyphs alone stop being clear.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:LiquidTabBar SelectedValue="{Binding Section}" AutomationProperties.Name="App sections">
        <arc:LiquidTabBarItem Value="feed" Label="Feed" Glyph="{x:Static arc:ArcIcons.House}" />
        <arc:LiquidTabBarItem Value="chat" Label="Chat" Glyph="{x:Static arc:ArcIcons.MessageCircle}"
                              Badge="2" BadgeLabel="2 new" />
        <arc:LiquidTabBarItem Value="me" Label="Me" Glyph="{x:Static arc:ArcIcons.User}" />
    </arc:LiquidTabBar>
</UserControl>
```

`LiquidTabBar` is in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`. Each tab is a
`LiquidTabBarItem` with a `Value`, a `Label` and a `Glyph` (a 24 × 24 stroked geometry such as the `ArcIcons`). The
bar is a selecting list: bind `SelectedValue` two-way to the value of the current section, or use `SelectedIndex` and
`SelectionChanged`. One tab is always selected while there are tabs, and a disabled tab is skipped.

The bar only switches; it does not hold the sections' content. Show the content for `SelectedValue` next to it, for
example in a `ContentControl` or an `ArcContentSwap`, as the preview does.

With `ItemsSource`, style `LiquidTabBarItem` to bind its `Label`, `Glyph` and `Badge` to the data item; a tab without
a `Label` falls back to the text of its content or its value. A tab without a `Glyph` is a text tab that always shows
its label. Keep the default items panel: the lens follows the bar's own layout.

### Labels

`Labels` decides where labels appear. `Active` (the default) opens only the selected tab; `Always` labels every tab
and makes the bar wider; `None` leaves the glyphs alone, for tight spaces. Screen readers hear the labels in every mode.
Changing the mode opens or closes the tabs on the same spring as a selection.

<Demo name="Labels" />

### Badges

`Badge` puts a count on a tab's glyph. When the count changes its digits roll to the new number, and at 0 the badge
shrinks away. `BadgeLabel` is what screen readers hear after the label (for example "8 unread"); without it they hear
the number.

<Demo name="Badges" />

### Active glyph

Inside the lens a tab draws its glyph filled in. Set `ActiveGlyph` to draw another shape there instead, such as an open
folder for a closed one.

<Demo name="ActiveGlyph" />

### Dragging and flicking

Drag from the selected tab: the lens follows the pointer, its front edge slightly ahead, and settles on the nearest
tab when released. A fast flick projects it further the faster it goes, and never leaves it on the tab it started
from; pulled beyond the outermost tabs, it gives way less and less, like a stretched band. While you drag,
the tabs keep their sizes; the selection changes when you let go. A short press without movement is an ordinary click.

### Best practices

**Do**

- Keep labels to one short word and the bar to five tabs or fewer.
- Set a `Label` on every tab, also with `Labels="None"`: it names the tab for screen readers.
- Give badges a `BadgeLabel` that says what the number counts.

**Avoid**

- Mixing tabs with and without glyphs in one bar.
- Changing the selection on a timer: the lens draws the eye every time it moves.

## API reference

<!-- api: LiquidTabBar -->

<!-- api: LiquidTabBarItem -->

## Accessibility

- The bar is a tab list, named "Sections" unless `AutomationProperties.Name` says otherwise; each tab is a tab with its
  selected state.
- A tab's name is its label, followed by its badge: `BadgeLabel` when set, otherwise the number.
- The reverse-color painting under the lens is paint only, not a second set of elements, so each tab is reported once.
- Keyboard focus shows a 2px ring around the tab; pointer use shows none.

### Keyboard interactions

| Key | Action |
|---|---|
| Tab | Moves focus to the selected tab; only the selected tab is in the tab order. |
| Right / Left | Focus and selection go together to the next or previous enabled tab; Right on the last goes to the first, Left on the first to the last. |
| Home / End | Jumps to the first or last enabled tab and selects it. |
| Enter or Space | Selects the focused tab. |

## Motion

- The lens moves on one spring (visual duration 0.42s, no bounce) and keeps its speed when the selection changes
  again mid-way. Its edges follow the tabs between them, so it is exactly one tab wide wherever it is.
- A tab widens and narrows on that same spring. The previous tab's label is gone within 50ms; the next tab's label
  fades in during the final tenth of its widening while it slides 4px into place.
- With the mouse over another tab, the lens's facing edge leans 14% of the way toward the neighbouring tab.
- A press draws the lens in by 2px; dragging leads with the front edge; badge digits roll on a smooth spring.
- Reduced motion places the lens and the labels at once, without gliding, leaning or sliding; Off does the same.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Lens | Glides on the spring | Jumps | Jumps |
| Tabs opening and closing | Spring | Jump | Jump |
| Lean toward a hovered tab | Yes | No | No |
| Glyph color on hover | 160ms | 120ms | Immediate |

## Responsive behavior

- A tab showing only its glyph is 44px wide and widens by its label when the label shows; the bar is as wide as its
  tabs and does not scroll. In a narrow spot, `Labels="None"` keeps it smallest.
- The bar's size is the size its tabs will settle at, so content around it does not move while the lens glides.
- With touch, a horizontal drag moves the lens and a vertical swipe still scrolls the page. Only the mouse makes the
  lens lean.

## Related

- `Tabs` for text tabs with their own panels.
- `SegmentedControl` for compact filters and view switches.
- `MorphNav` for site navigation with menus.

| Component | Choose it when |
|---|---|
| `LiquidTabBar` | A few sections with glyphs, switched often |
| `Tabs` | Text tabs, many tabs, or tabs with panels |
| `SegmentedControl` | A compact choice among a few views or filters |
