---
title: Glass tab bar
summary: A floating bottom tab bar of frosted glass for app shells, with a selection lens that glides and stretches between tabs.
---

<Demo name="Preview" />

## When to use

`GlassTabBar` is the main navigation of a mobile-style app shell: a pill of frosted glass that floats a little above the
bottom edge, over the content, with three to five destinations. The content keeps scrolling under it and shows through
blurred, so the bar never cuts the page off. Each tab is an icon over a short label, with an optional badge. The selected
tab sits in a lens of brighter glass that glides to the next tab with its leading edge first, stretching on the way, and
the selected icon takes the accent color. Press and drag the lens to scrub across the tabs.

- **The root navigation of a phone app**: Home, Search, Saved, Profile.
- **A compact app shell in a narrow window or a phone preview**.
- **A media or map app** where the content should run under the bar to the bottom edge.

## When not to use

- **Wide desktop layouts**: use a sidebar or `Tabs` at the top.
- **More than five destinations**: move the rest into a "More" page or use a sidebar.
- **Switching views inside one page**: use `GlassSegmented` or `Tabs`.
- **Actions**: a tab goes somewhere; put actions in a toolbar or a floating button.

## Usage

Place the bar over the page at the bottom, and set `arc:GlassBackdrop.Source` to the scrolling content so the bar blurs
the part of it that passes underneath. Leave room at the end of the content (about the bar's height plus its margin) so
the last item can scroll clear of the bar.

```xml
<Panel arc:GlassBackdrop.Source="{Binding #Feed}">
    <ScrollViewer>
        <StackPanel Name="Feed" Margin="16,16,16,110"><!-- the page --></StackPanel>
    </ScrollViewer>
    <arc:GlassTabBar Margin="0,0,0,18">
        <arc:GlassTab Content="Home" Icon="{x:Static arc:ArcIcons.House}" />
        <arc:GlassTab Content="Search" Icon="{x:Static arc:ArcIcons.Search}" />
        <arc:GlassTab Content="Saved" Icon="{x:Static arc:ArcIcons.Bookmark}" Badge="" />
        <arc:GlassTab Content="Profile" Icon="{x:Static arc:ArcIcons.User}" />
    </arc:GlassTabBar>
</Panel>
```

One tab is always selected; bind `SelectedIndex` or handle `SelectionChanged` to switch the page. `GlassTabBar` derives
from Avalonia's `SelectingItemsControl` and lives in the Special package: register it with `UseArcSpecial()` next to
`UseArc()`. When the content scrolls the copy under the bar is taken again, because the content moves against the bar.
See the Glass card page for how the frost is made with public Avalonia APIs.

### Icons only and badges

`ShowLabels="False"` gives a lower bar of icons; the labels stay the tabs' accessible names. `Badge` puts a mark on a
tab's icon: an empty string is a dot ("something new"), any other text a small count ("3", "9+"), null nothing.

<Demo name="IconsOnly" />

### Gestures

| Input | Effect |
|---|---|
| Click or tap a tab | Select it; the lens glides over |
| Press the selected tab | The lens swells |
| Drag from the selected tab | The lens follows the finger across the bar |
| Release after a drag | Select the tab nearest to the lens |

### Content guidelines

- Use one-word labels and clear outline icons from the same set.
- Badge only what needs attention; a dot for news, a count for things waiting.
- Keep the same tabs in the same order on every page.

## API reference

<!-- api: GlassTabBar -->

<!-- api: GlassTab -->

## Accessibility

- The bar is a tab list and each tab a tab with its label as name and its selected state; a badge is read after the name
  ("Saved, new", "Inbox, 3").
- Only the selected tab is in the Tab order; the arrow keys move the selection and the focus together.
- Keyboard focus shows a 2px ring around the focused tab; the pointer draws none.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Into the bar (the selected tab) and out of it |
| `Right` / `Down` | Next tab, wrapping (left in right-to-left layouts) |
| `Left` / `Up` | Previous tab, wrapping |
| `Home` / `End` | First or last tab |

## Motion

- The lens's leading edge moves on a spring of about 0.28 s with a little give and its trailing edge on a calmer one of
  about 0.46 s, so it stretches toward the new tab and settles to its width.
- Pressed, the lens swells by 3px on every side; dragged, it follows the pointer on a tight spring.
- Icons and labels change color over 160ms.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Selection changes | Lens glides and stretches | Lens fades out and in at the new tab | Lens moves at once |
| Press and drag | Lens swells and follows | Lens follows without swelling | Lens follows without swelling |
| Sheen under the pointer | Follows the pointer | None | None |

## Responsive behavior

- Tabs share the bar's width equally, each at least 64px; the bar is as wide as its tabs and centered. In a very narrow
  shell, set `HorizontalAlignment="Stretch"` and a side margin so the tabs share the full width.
- Long labels end with an ellipsis rather than widening the tab.
- Resizing puts the lens straight onto its tab without animating.

## Related

- `Tabs` for views inside a page, on wide layouts.
- `GlassSegmented` for switching views on glass.
- `GlassCard` and `GlassMenu` for other glass surfaces.

| Component | Choose it when |
|---|---|
| `GlassTabBar` | The root navigation of a phone-style app shell |
| `GlassSegmented` | A few views or modes inside one screen |
| `Tabs` | Views inside a page on a desktop layout |
