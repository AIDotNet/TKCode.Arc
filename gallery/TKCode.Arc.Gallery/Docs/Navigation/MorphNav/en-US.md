---
title: Morph nav
summary: A website's top bar in which sections, their link panels and search share one reshaping surface; it slims down once the page scrolls and gathers its sections into a single menu when narrow.
---

<Demo name="Preview" />

## When to use

`MorphNav` heads a product or marketing website. From left to right it carries your brand, a handful of sections, a
search button and a single call to action. None of these open a window of their own: pointing at a section stretches
the bar down into that section's links, sliding over to a neighbour reshapes the same surface for the neighbour's
links, and search takes over the bar itself rather than popping up a dialog.

- **Websites with a small number of sections**, each leading to several pages, such as a product site.
- **Search that should stay part of the header** rather than opening on top of the page.
- **A single action you always want within reach**, like starting a trial, kept at the bar's far end.

## When not to use

- **Views of the same page**: `Tabs` keeps them side by side.
- **Top-level areas of an app** rather than a website: `LiquidTabBar` suits that better.
- **Commands rather than destinations**: a `DropdownMenu` holds those.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MorphNav BrandName="Lantern" ActionLabel="Get started" Current="platform">
        <arc:MorphNavItem Value="platform" Label="Platform">
            <arc:MorphNavLink Label="Notebooks" Description="Notes and photos in one place" Href="/notebooks" />
            <arc:MorphNavLink Label="Maps" Description="Pin every note to a place" Href="/maps" />
        </arc:MorphNavItem>
        <arc:MorphNavItem Value="pricing" Label="Pricing" Href="/pricing" />
        <arc:MorphNav.SearchEntries>
            <arc:MorphNavSearchEntry Label="Maps" Group="Platform" Href="/maps" />
        </arc:MorphNav.SearchEntries>
    </arc:MorphNav>
</UserControl>
```

`MorphNav` is in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`. Put
`MorphNavItem` elements inside it, one per section. Give an item `MorphNavLink` children or a `Feature` and it gets a
panel; leave both out and the item is a link by itself. Each link takes a `Label`, an `Href`, and optionally a
`Description` and an `Icon`. Up to three links sit in a single column and four or more are split into two; `Columns`
overrides that.

Beside the links, `Feature` holds a card: a `MorphNavStory` shows a picture, a title and a line under it, and any other
content is shown as it is. `Footer` adds a line under the links behind a hairline; a `MorphNavLink` there shows as a
short link with an arrow. The panel closes as soon as any link or button inside it is used.

`BrandName` and `BrandMark` make up the brand button that leads the bar, and `ActionLabel` adds the call-to-action
button that ends it, which raises `ActionInvoked` and runs `ActionCommand`. Search appears once `SearchEntries` has
entries.

### Going places

Whatever leads somewhere raises `Navigate`: a panel link, a footer link, a feature card, a search result, an item
without a panel, or the brand. `e.Label` and `e.Href` describe it, `e.Section` is the `Value` of the item it belongs to,
and `e.Destination` is the object that was chosen. Route there in the handler, and set `Current` to the section you are
in: that item reads in the foreground color and is announced as the current page.

<Demo name="Current" />

```csharp
nav.Navigate += (_, e) =>
{
    router.Go(e.Href);
    nav.Current = e.Section;
};
```

`Open(value)`, `ClosePanel()`, `OpenSearch()` and `CloseSearch()` drive the bar from code; `OpenValue` and
`IsSearchOpen` report what is open.

### Search

Opening search turns the bar into a search field: the items fade and shrink out while the field takes over the same
surface, and the results appear below it. Before anything is typed, the first five entries are offered as suggestions;
as you type it ranks labels that start with the query first, then words of a label, then labels that contain it, then
descriptions, groups and `Keywords`. No more than six results are listed, each with its `Group` at the end. Arrow keys
move the active result and Enter opens it.

### Slimmer after scrolling

`IsCompact` slims the bar from 56 to 46 px on a spring and tucks the brand name away. Set it from your page's
scroll position, so the bar takes less room once the reader moves on.

<Demo name="TightenOnScroll" />

```csharp
page.ScrollChanged += (_, _) => nav.IsCompact = page.Offset.Y > 24;
```

### Folding into a menu

When the nav itself is narrower than `CollapseBelow` (720 px unless you change it), its sections are replaced by a
single Menu item. That item's panel gathers the links of every section under the section's name, followed by the
items that have no panel. `IsCollapsed` tells you when it has folded.

<Demo name="Folded" />

### Best practices

**Do**

- Keep to three to five sections, and a handful of links in each panel.
- Give links a short description when their names alone are vague.
- Keep `Current` in step with the page, so the bar always says where you are.

**Avoid**

- More than one primary action: the bar has room for one.
- Putting the nav inside a parent that clips: panels grow over the page below the bar.

## API reference

<!-- api: MorphNav -->

## Accessibility

- The nav is a navigation landmark named by `Label` ("Main" by default).
- Items with a panel report whether they are expanded; plain items, links and cards are links. The current item is
  announced as the current page.
- An open panel is exposed as a region that takes its item's name. The search field is a combo box that controls the
  results list; the line above the results ("Suggested", "6 results", "No results for …") sits in a polite live
  region, so screen readers hear how many matched.
- Keyboard focus shows the shared focus ring; the highlight follows keyboard focus along the bar. Pointer use shows no
  ring.
- Pressing anywhere beyond the nav dismisses an open panel or search; a panel also closes once focus moves out of the
  nav.

### Keyboard interactions

| Key | Action |
|---|---|
| Left / Right | Steps along the items and wraps at either end; if a panel is open, it switches to the newly focused item's panel. |
| Home / End | Moves to the first or last item. |
| Down, Enter or Space, on an item that has a panel | Shows the panel with focus on its top link; when the panel is already showing, takes focus into it. |
| Down / Up in a panel | Steps through the panel's links; from the top link, Up goes back to the item. |
| Down / Up in search | Changes which result is highlighted. |
| Enter in search | Goes to the highlighted result. |
| Escape | Dismisses the open panel or search and puts focus back on the control that opened it. |

## Motion

- A spring resizes the surface to fit each panel: growing overshoots slightly, shrinking settles without overshoot.
  Meanwhile the rounding changes from the bar's pill shape to the panel's corners and the shadow deepens.
- The first panel opened from the bar drops slightly into place while sharpening out of a blur. Switching to another
  section moves the contents sideways: the incoming panel enters from the side of the new item and the outgoing one
  exits toward the other side, fading. A highlight slides along the bar to the open item, or else to the one under the
  pointer or keyboard focus.
- A panel waits for 60 ms of hovering before it opens and lingers 180 ms after the pointer leaves the nav, so a pointer
  passing over a gap does not make it blink closed. Only a mouse hovers; on touch, a tap opens or closes a panel.
- When search opens, the bar's contents blur, shrink and fade while the search field expands within the same surface.
  Switching compact mode on or off resizes the bar on a spring.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Surface size and corners | Spring | Jumps | Jumps |
| Panels and search | Fade, blur and travel | Short fade | Swap |
| Highlight | Glides | Jumps | Jumps |
| Compact | Spring | Jumps, name fades | Jumps |

## Responsive behavior

- Panels top out at 700 px wide and search at 544 px, and neither gets wider than the nav minus 16 px.
- A nav narrower than `CollapseBelow` (720 px) swaps its sections for one menu and shows the brand's mark without the
  name.
- In windows under 420 px wide, link panels keep to a single column and the folded menu leaves out glyphs.

## Related

- `Tabs` for views within a single page.
- `LiquidTabBar` for the main areas of an app.
- `DropdownMenu` for a list of commands.

| Component | Choose it when |
|---|---|
| `MorphNav` | A site's top bar needs sections with panels, search and one action |
| `Tabs` | A few views of one page should always be visible |
| `LiquidTabBar` | An app has a few main areas to move between |
