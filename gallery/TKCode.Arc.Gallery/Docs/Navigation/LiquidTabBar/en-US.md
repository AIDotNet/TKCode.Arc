---
title: Liquid tab bar
summary: Tabs on a track whose selection is a drop of color. It stretches toward the new tab, thins at its waist, then gathers itself and settles.
---

<Demo name="Preview" />

## When to use

`LiquidTabBar` is a row of tabs on a rounded track, with the selected tab sitting on a drop of the accent color. When
the selection moves, the drop behaves like a liquid rather than a sliding block: its front reaches for the new tab
first, the drop stretches between the two, thins and pinches in the middle while it is long, and then its back catches
up and it rounds off under the new tab.

- **Switching views** of one screen: a feed's sections, a dashboard's periods, a profile's tabs.
- **Bottom bars on phones**, with a glyph per tab.
- **Playful products** where a little character in the controls fits the brand.

## When not to use

- **Many tabs or long labels**: the bar does not scroll; use `Tabs`, which scrolls and fades its edges.
- **Tabs with their own panels**: the bar only selects; pair it with your own content or use `Tabs`.
- **Serious, dense tools** where the motion would distract: a plain segmented control is calmer.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:LiquidTabBar SelectedIndex="0">
        <arc:LiquidTabBarItem Content="Overview" />
        <arc:LiquidTabBarItem Content="Activity" />
        <arc:LiquidTabBarItem Content="Settings" />
    </arc:LiquidTabBar>
</UserControl>
```

`LiquidTabBar` is in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`. Write
`LiquidTabBarItem` elements, or bind `ItemsSource` to your data (each item shows its string, or `ItemTemplate`). An
item can have a `Glyph` before its label. One tab is always selected while there are tabs; read and set it with
`SelectedIndex` or `SelectedItem`, and listen to `SelectionChanged`. A disabled item is skipped.

The tabs sit in a horizontal stack by default. For tabs of equal width, set `ItemsPanel` to a one-row `UniformGrid`
and stretch the bar.

### How liquid

`Viscosity`, from 0 to 1, sets how far the back of the drop lags behind its front: at 0 the drop barely stretches, at
1 it draws out long before it gathers. The default is 0.5.

<Demo name="Viscosity" />

### Best practices

**Do**

- Keep labels to one or two short words, and to five tabs or fewer.
- Use glyphs on phone bottom bars, with the label still set for assistive technology.

**Avoid**

- Changing `SelectedIndex` on a timer: the drop draws the eye every time it moves.
- Mixing tabs with and without glyphs in one bar.

## API reference

<!-- api: LiquidTabBar -->

<!-- api: LiquidTabBarItem -->

## Accessibility

- The bar is announced as a tab list and each item as a tab, named by its text label, with its selected state.
- Only the selected tab is in the tab order; the arrow keys move between tabs and select as they go.
- Keyboard focus shows the shared focus ring around the tab; pointer use shows none.

### Keyboard interactions

| Key | Action |
|---|---|
| Tab | Moves focus to the selected tab. |
| Right or Down | Selects the next enabled tab, wrapping round to the first. |
| Left or Up | Selects the previous enabled tab, wrapping round to the last. |
| Home / End | Selects the first or last enabled tab. |
| Enter or Space | Selects the focused tab. |

## Motion

- The drop's two ends move on two springs. The end facing the new tab is quick and lightly springy; the other is slower
  by `Viscosity`. The distance between them sets how much the drop thins (up to 22% of its height) and how deep its
  waist pinches.
- A selection made while the drop is still moving carries on from where it is, with its speed.
- The tab labels change color over 240ms.
- The drop is one shape whose points move in place, so a frame allocates nothing and nothing is laid out.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Drop | Stretches and settles | Jumps to the new tab | Jumps to the new tab |
| Label color | 240ms | 120ms | Immediate |

## Responsive behavior

- The bar is as wide as its tabs. It does not scroll or wrap, so keep the tabs few on narrow screens.
- A one-row `UniformGrid` as `ItemsPanel` with a stretched bar gives a full-width bottom bar on a phone.
- When the bar is resized the drop follows its tab at once, without motion.

## Related

- `Tabs` scrolls, fades its edges and shows each tab's panel.
- `MorphNav` keeps a site's navigation in one pill.

| Component | Choose it when |
|---|---|
| `LiquidTabBar` | A few views, chosen with a little character |
| `Tabs` | Many tabs, or tabs with their own panels |
| `MorphNav` | Site navigation should stay out of the way |
