---
title: Magnify dock
summary: An app dock whose icons swell like a lens under the pointer, push their neighbours aside, show their names and bounce when launched.
---

<Demo name="Preview" />

## When to use

`MagnifyDock` is a row of app icons on a shelf. As the pointer moves along it, the icons near the pointer grow like a
magnifying lens, largest right under it, and the row widens to make room so nothing overlaps. The icon under the
pointer shows its name above it. Clicking an icon launches it: it hops a few times, smaller each time, and a dot under
it marks it as running.

- **Desktop-style app launchers** in a web desktop, a kiosk or a demo environment.
- **Portfolio and product pages** that list tools or projects as icons.
- **Playful toolbars** where a few favourite actions should feel tactile.

## When not to use

- **Dense toolbars and editors**: icons that move under the pointer are slower to hit. Use a plain toolbar.
- **Docking panes in an application layout**: that is `DockLayout`.
- **Long lists**: keep to about a dozen icons; more do not fit a phone.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MagnifyDock Label="Apps">
        <arc:MagnifyDockItem Label="Files" IsRunning="True">
            <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Folder}" Size="24" />
        </arc:MagnifyDockItem>
        <arc:MagnifyDockItem Label="Mail">
            <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Mail}" Size="24" />
        </arc:MagnifyDockItem>
    </arc:MagnifyDock>
</UserControl>
```

`MagnifyDock` is an `ItemsControl` in the Special package. Register the package with `UseArcSpecial()` next to
`UseArcControls()`. Each item is a `MagnifyDockItem`, a button whose content is the icon: write them inline, or bind
`ItemsSource` with an `ItemTemplate` for the icon and set each item's `Label` and `IsRunning` through
`ItemContainerTheme`. Give an item its own `Background` to tint its tile; by default tiles use the muted surface. The
dock centers itself and reserves the room its widest magnified row and the labels need, so the page never moves while
it swells.

### Size and lens

`ItemSize` is the side of an icon at rest (48 px) and `Gap` the space between icons (8 px). `Magnification` is the
scale of the icon right under the pointer (1.8; 1 turns the lens off) and `Reach` how far from the pointer icons still
grow (150 px). `ShelfPadding` is the space between the shelf's edge and the icons (8 px).

<Demo name="Launch" />

### Launching

A click, a tap, Enter or Space on an icon launches it: it bounces, `ItemLaunched` fires with its index, its container
and its data item, and with `MarksLaunched` on (the default) its `IsRunning` turns on. `Launch(index)` does the same
from code. Each item is a button, so `Command` and `Click` work on it too. Set `IsRunning` to false when the app closes.

### Best practices

**Do**

- Give every item a short `Label`; it is both the name above the icon and its accessible name.
- Use simple, high-contrast icons on tinted tiles so they read at 48 px and stay crisp at 1.8 times.

**Avoid**

- Putting the dock where it can be covered: the magnified icons and labels rise about 80 px above the shelf.
- Using it for actions people repeat quickly, such as formatting text.

### Common questions

**Does the dock move the page when it swells?** No. It reserves the widest row at its full height when it lays out, and
magnification only transforms the icons.

**Can I put a separator between groups?** Not yet. Use two docks side by side, or a wider `Gap`.

## API reference

<!-- api: MagnifyDock -->

<!-- api: MagnifyDockItem -->

<!-- api: MagnifyDockItemEventArgs -->

## Accessibility

- The dock is a toolbar named by `Label` ("Dock" by default). Each icon is a button named by its `Label`.
- The dock is one Tab stop. The arrow keys move between icons (wrapping at the ends), Home and End jump to the first
  and last, and the focused icon is magnified and shows its label as if the pointer were on it.
- Keyboard focus shows a focus ring around the icon, which grows with it; pointer focus shows none.
- The running dots are decoration; say in your own UI which apps are open if it matters.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Move focus into the dock, to the last focused icon |
| `Left` / `Right` | Move to the previous or next icon |
| `Home` / `End` | Move to the first or last icon |
| `Enter` / `Space` | Launch the focused icon |

## Motion

- Each icon's size follows a smooth bell around the pointer on its own spring (stiff and slightly underdamped), so a
  quick sweep ripples along the row and settles without a visible wobble.
- The row re-flows around the grown icons with the same gap and stays centred; the shelf widens with it.
- The label appears above the active icon, fading in over 120 ms and following the icon as it grows and hops.
- A launch hops the icon three times, to 62%, 30% and 12% of its height, 400 ms each.
- Magnification is each icon's render transform, written once per frame; the layout does not change. Frames stop when
  every icon is at rest and pause while the dock is out of view.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Pointer over the row | Icons swell like a lens | Icons keep their size | Icons keep their size |
| Label | Fades in over 120 ms | Fades in over 120 ms | Appears |
| Launch | Hops three times | Running dot only | Running dot only |

## Responsive behavior

- The dock is as wide as its icons at full magnification and centres itself; it does not wrap. On a phone, use fewer
  icons or a smaller `ItemSize`, about 40 px.
- Touch has no hover, so on touch screens the icons do not swell; a tap launches an icon as a click does.
- Labels rise above the dock inside its reserved height, so they are never cut off by the dock's own box.

## Related

- `DockLayout` arranges panes of an application, which is a different kind of dock.
- `SegmentedControl` switches between a few views in a compact row.

| Component | Choose it when |
|---|---|
| `MagnifyDock` | A few apps or tools should feel tactile to open |
| `DockLayout` | An application needs dockable, resizable panes |
| `SegmentedControl` | People switch between a few views |
