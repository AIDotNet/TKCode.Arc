---
title: Page header
summary: A project page header that folds into a compact bar as you scroll, with tabs whose counts roll.
---

<Demo name="Preview" />

## When to use

- The top of a project, repository or record page: where the page sits, what it is, its status, and the views of it.
- Pages whose body scrolls and whose title should stay in reach as a compact bar.
- Pages with a few actions on the record, such as Follow, Share and one primary action.

## When not to use

- App-wide navigation between pages: use `Sidebar`.
- Switching views inside a card or a panel: use `Tabs`.
- A dialog or a settings form with its own title: use the title of `Dialog` or a plain heading.

## Usage

`PageHeader` is the whole page frame. It shows the header and, below it, the scrolling body of the selected tab. Give
it a fixed height (or put it in a row that has one) so the body can scroll. Each `PageHeaderTab` has a `Header`, an
optional `Count`, and its body as `Content`.

```xml
<arc:PageHeader Title="Checkout redesign"
                Status="On track"
                Description="Rebuilding mobile checkout around saved payment methods."
                Height="480">
    <arc:PageHeaderTab Header="Overview">
        <TextBlock Text="Progress, milestones and recent activity." />
    </arc:PageHeaderTab>
    <arc:PageHeaderTab Header="Issues" Count="9">
        <TextBlock Text="Open issues." />
    </arc:PageHeaderTab>
</arc:PageHeader>
```

### Title and tabs

A header needs only a title and tabs. Without breadcrumbs, status or actions the header row stays empty until the
header condenses and shows the compact title.

<Demo name="Minimal" />

### Breadcrumbs, status and meta

`Breadcrumbs` takes the path to the page, root first. The last item is the current page. Choosing another crumb raises
`BreadcrumbClick` with the item and its index. `Status` adds a badge with a dot beside the title, in the `StatusTone`
color (success by default). `Meta` takes any content for the row under the description, such as members, an owner and
a date. Its text is extra small and muted.

```xml
<arc:PageHeader Title="Checkout redesign" Status="At risk" StatusTone="Warning">
    <arc:PageHeader.Meta>
        <TextBlock Text="Led by Emma Collins" />
    </arc:PageHeader.Meta>
</arc:PageHeader>
```

```csharp
header.Breadcrumbs = new[] { "Northline", "Projects", "Checkout redesign" };
header.BreadcrumbClick += (_, e) => router.Open(e.Index);
```

### Actions

`Actions` are small secondary buttons in the header row. `MoreActions` always live in the "More actions" menu.
`PrimaryAction` is a small primary button at the end of the row. Each `PageHeaderAction` has a `Label`, an `Icon` (a
Lucide geometry such as `ArcIcons.Bell`), and a `Command` or a `Click` event. `PageHeader.ActionInvoked` reports every
action in one place.

When the breadcrumbs would get less than 240 pixels, the secondary actions fold into the top of the "More actions"
menu. They come back once there are 272 pixels again, so the header does not flicker at the threshold. Only the width
of the header is watched, so a label that grows (Follow to Following) never folds the button you just pressed.

<Demo name="FoldingActions" />

Change `Label` and `Icon` to update a button in place. Set `MenuLabel` and `MenuIcon` when the menu row should read
differently. `IsLoading` shows a spinner and ignores clicks.

```csharp
follow.Click += (_, _) =>
{
    following = !following;
    follow.Label = following ? "Following" : "Follow";
    follow.MenuLabel = following ? "Unfollow" : "Follow";
};
share.IsLoading = true;
```

### Condensing

When the body scrolls past 16 pixels, the header folds into a compact bar with the title and the status. It opens
again only when the body is back within 4 pixels of the top. `IsCondensed` tells you the state. Choosing the compact
title calls `ScrollToTop()`, which scrolls back and moves focus to the title. The header condenses only when the body
is long enough to stay scrolled once the header is smaller.

Switching tabs while the header is condensed shows the top of the new tab and keeps the compact bar. `RevealTab(index)`
does the same from code, for example after creating an item in another tab.

### Data items

Bind `ItemsSource` to your own items. `ItemTemplate` renders the tab label and `ContentTemplate` the body. `SelectedIndex`
and `SelectedItem` can be bound in both directions.

<Demo name="DataItems" />

### Narrow widths

The layout follows the width of the control, not the window. Below 560 pixels the padding gets smaller, the root crumb
and the compact badge hide, the title steps down a size, and the far edge of the tab row fades. Below 460 pixels the
primary action shows only its icon.

## API reference

<!-- api: PageHeader -->

<!-- api: PageHeaderTab -->

<!-- api: PageHeaderAction -->

## Accessibility

- The control is a region named by `Title`. The title is a level 2 heading.
- The breadcrumbs are a navigation landmark. The current page is marked and is not a link.
- The tab row is named by `TabsLabel`. Left and Right move between tabs and select them, looping at the ends. Home and
  End go to the first and last tab. Only the selected tab is in the Tab order.
- The overflow button is named by `MoreActionsLabel`. The menu supports the arrow keys, Home, End, typing to jump,
  Enter, and Escape to close and return focus.
- The compact title is named "Title, back to top" (`BackToTopLabel`). Hidden parts of the header leave the Tab order
  and ignore the pointer.
- Below 460 pixels the primary button keeps its label as its accessible name.

## Motion

- Condensing: the intro closes its height on a smooth spring. The title shrinks to 62% toward its top left corner and
  fades with a slight blur. The breadcrumbs lift away and the compact title rises in from a soft blur.
- Folding actions: the secondary actions close their width on a smooth spring and fade. Only the right edge is clipped,
  so they fold under the overflow button.
- Tabs: the indicator glides between tabs on a morph spring. Counts roll digit by digit. The new body slides in 12
  pixels from the side of travel while the old one fades out.
- Reduced motion keeps only the fades. Heights and widths change at once and the body crossfades. Off applies every
  change at once.

## Related

- `Breadcrumb` for a path on its own.
- `Tabs` for views inside a card or a panel.
- `Badge` for status labels.
- `DropdownMenu` for menus of actions.
