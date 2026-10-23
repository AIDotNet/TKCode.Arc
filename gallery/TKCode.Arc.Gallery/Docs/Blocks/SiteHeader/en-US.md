---
title: Site header
summary: A sticky website header that turns solid on scroll, with a gliding active link, mega menu panels, and a mobile sheet.
---

<Demo name="Preview" />

## When to use

`SiteHeader` is the top bar of a marketing or documentation site. It holds the brand, the main links and one or two
actions, stays at the top of the page while people scroll, and opens a panel of grouped links when a menu is too big
for one row.

- **A product site with several sections**: Product and Resources open panels with described links and a feature card;
  Pricing and Customers are plain links.
- **A documentation site**: the `Simple` layout puts three or four links beside the brand, with the current section
  marked by a line under it.
- **A site with few links and a calm look**: the `Centered` layout puts the links in a quiet capsule in the middle, with
  the current link as a raised pill.
- **A narrow host**: below `CollapseWidth` (760 by default) the links fold into a menu sheet. The block follows its own
  width, so a header in a narrow pane folds even when the window is wide.

## When not to use

- **Navigation inside an application**: use `Sidebar` or `Tabs`. The header is for pages people arrive at, not for
  switching between tools they already use.
- **A page title with breadcrumbs and actions**: use `PageHeader`.
- **A single menu of commands**: use `DropdownMenu`.

## Usage

```xml
<ScrollViewer>
    <StackPanel>
        <arc:SiteHeader Current="product" />
        <!-- the page -->
    </StackPanel>
</ScrollViewer>
```

Without items the header shows Arc's example set (Product, Resources, Pricing, Customers), so a bare `SiteHeader` looks
like the Arc preview. Put the header inside the `ScrollViewer` it should stick to, or set `ScrollSource` when it
overlays one. Avalonia has no `position: sticky`: the header follows the scroll offset and stays at the top of the
viewport. The page content can start under the header (a negative top margin of the header's height) so a hero slides
beneath it.

### Items, links and the feature card

```xml
<arc:SiteHeader>
    <arc:SiteHeaderItem Value="guides" Label="Guides">
        <arc:SiteHeaderLink Label="Quick start" Description="Install and run in five minutes" Href="https://example.com/start" />
        <arc:SiteHeaderLink Label="Theming" Description="Tune color, radius and motion" />
        <arc:SiteHeaderItem.Feature>
            <arc:SiteHeaderFeature Title="What's new" Description="The spring release." Href="https://example.com/new" />
        </arc:SiteHeaderItem.Feature>
    </arc:SiteHeaderItem>
    <arc:SiteHeaderItem Value="pricing" Label="Pricing" Href="https://example.com/pricing" />
</arc:SiteHeader>
```

`Items` is an `AvaloniaList<SiteHeaderItem>`; `ItemsSource` wins when it is set and is observed when it raises collection
changes. An item with `Links` opens a panel in the `Mega` variant and is a plain link in the others. `Value` is the
item's stable name: `Current` and `SiteHeaderNavigateEventArgs.Section` use it. `SiteHeader.CreateExampleItems()` returns
Arc's items as a starting point. The feature card's `Image` is any `IImage`; it is shown 16:10 and cropped to fill, and
`ImageDescription` is what it shows. Without an image the card shows only its text.

<Demo name="Simple" />

### Choosing a destination

Every destination (an item, a panel link, the feature card, the brand, an action) raises the bubbling `Navigate` event
and runs `NavigateCommand` with the same `SiteHeaderNavigateEventArgs`. `Kind` says what was chosen, `Section` names the
item it belongs to and `Link`, `Feature` or `Item` carry the data objects. Choosing something inside an item also sets
`Current` to that item and closes the panel and the sheet. When nobody sets `Handled` and the destination has an
absolute http, https or mailto `Href`, the header opens it with the platform launcher.

```csharp
header.Navigate += (_, e) =>
{
    router.GoTo(e.Href ?? e.Label);
    e.Handled = true; // do not launch the address
};
```

<Demo name="Events" />

### Scrolling and solid state

The bar is transparent at the top of the page. Once the scroll offset passes `ScrollThreshold` (8) it turns solid: a
background, a bottom border and a resting shadow. It turns transparent again only below half the threshold, so it never
flickers at the edge. `IsScrolled` reports the state. The bar is also solid while a panel or the menu sheet is open.
Set `IsSticky` to false to let the header scroll away.

### Mega menu panels

Hovering a trigger with a mouse opens its panel after 70 ms; moving to another trigger switches at once. The panel
closes 180 ms after the pointer leaves the bar and the panel. Clicking a trigger toggles its panel. Touch and pen taps
never open a panel by hovering. The panel's height springs to the height of the current face, growing and shrinking on
different springs. When you move to a neighbouring item the new content slides in from the side you moved toward and
the old content slides away. Use `OpenPanel(value)` and `ClosePanel()` from code; `OpenItemValue` reports the open item.

### Narrow layouts and the menu sheet

Below `CollapseWidth` the links, the secondary action and the panels give way to a menu button. The sheet drops from the
bar: groups expand one at a time, plain links sit in rows, and the actions come last in two equal columns. A scrim
covers the page and closes the sheet when pressed, and the page does not scroll while the sheet is open. Use
`OpenMenu()`, `CloseMenu()` and the two-way `IsMenuOpen` from code.

<Demo name="Narrow" />

### Content guidelines

- Keep the bar to four or five links. Move the rest into a panel.
- Describe a panel link in one short line that says what is there, not that it is a link.
- One primary action. The secondary action is for people who already have an account.

## API reference

<!-- api: SiteHeader -->

<!-- api: SiteHeaderItem -->

<!-- api: SiteHeaderLink -->

<!-- api: SiteHeaderFeature -->

<!-- api: SiteHeaderNavigateEventArgs -->

## Accessibility

- The links are a navigation landmark named by `Label`. The sheet is a second navigation landmark with the same name.
- A trigger reports whether its panel is expanded; the current link reports that it is selected. The panel is a region
  named by its item. The menu button's name changes between `OpenMenuLabel` and `CloseMenuLabel` and it reports
  whether the sheet is open; a group row reports whether it is expanded.
- Keyboard focus shows a 2 px ring; pointer focus shows none. Hover effects apply only to a mouse or pen.
- Every text is a property, so a localized site can replace "Get Arc", "Sign in", the menu labels and the landmark name.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the brand, the links, the actions and the menu button in order |
| `Left` / `Right` | In the links: move focus to the previous or next link, wrapping at the ends; while a panel is open the panel follows to the new link (or closes on a plain link) |
| `Down` on a trigger | Open its panel and focus the first link |
| `Up` / `Down` / `Home` / `End` in a panel | Move between the panel's links and the feature card; `Up` on the first one closes the panel and returns to the trigger |
| `Enter` / `Space` | Choose the focused link, or toggle a trigger or a menu group |
| `Escape` | Close the panel and return to its trigger; otherwise close the sheet and return to the menu button |

## Motion

| Change | Full | Reduced | Off |
|---|---|---|---|
| The bar turns solid | Background, border and shadow fade in 240 ms | At once | At once |
| The hover block | Appears on the first link, glides between links, vanishes when the pointer leaves | At once | At once |
| The current marker | Glides between links on a spring | At once | At once |
| A panel opens | Fades in 160 ms while it settles from 6 px above and a slight scale | Appears | Appears |
| A panel closes | Fades out in 180 ms, moving up 4 px | 120 ms fade | At once |
| The panel height | Springs to the face's height; growing and shrinking use different springs | At once | At once |
| Panel content changes | New content slides 36 px in through a short blur; old content slides out and fades in 120 ms | Fade | At once |
| The menu icon | Menu and close swap with a quarter turn and a small scale | Fade | At once |
| The sheet | Drops from the bar on a spring; rows rise in one after another; groups open on a spring | At once | At once |

## Related

- `PageHeader` for the title block of an application page.
- `Sidebar` for navigation inside an application.
- The site footer for the matching bottom of a marketing page.
