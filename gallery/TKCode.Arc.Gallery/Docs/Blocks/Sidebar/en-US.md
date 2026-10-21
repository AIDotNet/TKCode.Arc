---
title: Sidebar
summary: Workspace navigation with sections, nested pages, counts and a gliding selection marker; collapses to icons.
---

<Demo name="Preview" />

## When to use

- The main navigation of an app with more than a handful of top-level pages, such as a workspace, a dashboard or an
  admin tool.
- Pages that fall into groups, with one level of sub-pages (projects, reports, settings areas).
- Navigation that should stay on screen but take less room on demand: the sidebar folds into a 64px icon rail.

## When not to use

- Two to five views of the same object: use `Tabs`.
- Steps of a sequence or a path through a hierarchy: use `Stepper` or `Breadcrumb`.
- A deep tree of folders and files: use `TreeView`. The sidebar shows one level of nested pages.
- Actions rather than places: use `DropdownMenu` or `CommandPalette`.

## Usage

Put `SidebarSection` elements inside the sidebar and `SidebarItem` elements inside the sections. A section without a
`Header` has no heading, which suits the first group (Home, Inbox). Give every page a `Key` and bind `SelectedKey` to
your route. The sidebar takes its width from the theme (260 open, 64 as a rail), so place it in a `DockPanel` or an
`Auto` grid column and do not set `Width`.

```xml
<DockPanel xmlns="https://github.com/avaloniaui"
           xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
           xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"
           Height="420">
    <arc:Sidebar DockPanel.Dock="Left" SelectedKey="home" IsFilterVisible="True">
        <arc:SidebarSection>
            <arc:SidebarItem Key="home" Label="Home" Icon="{x:Static arc:ArcIcons.House}" />
            <arc:SidebarItem Key="inbox" Label="Inbox" Icon="{x:Static arc:ArcIcons.Inbox}" Badge="4" />
        </arc:SidebarSection>
        <arc:SidebarSection Header="Workspace">
            <arc:SidebarItem Key="projects" Label="Projects" Icon="{x:Static arc:ArcIcons.Folder}" IsExpanded="True">
                <arc:SidebarItem Key="projects/atlas" Label="Atlas" />
                <arc:SidebarItem Key="projects/beacon" Label="Beacon" />
            </arc:SidebarItem>
        </arc:SidebarSection>
    </arc:Sidebar>
    <TextBlock Margin="24" Text="Page content" />
</DockPanel>
```

### Sections and nested pages

An item with child items is a parent. It is never selected: choosing it opens or closes its pages, which hang from a
thin guide line. `IsExpanded` is two-way. Set `IsCollapsible` on a section with a `Header` to let people fold the
whole section away; its `IsExpanded` is two-way as well. Only one level of nesting is shown.

`Badge` puts a short count at the end of a row. `IsEnabled="False"` dims a page and the arrow keys skip it. `Icon`
takes a 24 by 24 Lucide geometry, such as an `ArcIcons` glyph, or any control.

### Selection and routing

`SelectedItem` and `SelectedKey` are two-way and stay in step. Setting `SelectedKey` before the pages exist is fine:
the sidebar selects the page once it appears. `ItemInvoked` fires every time a page is chosen, also when it was
already selected, and `SelectCommand` runs with the item as its parameter. Each item can carry its own `Command` and
`Tag` as well.

<Demo name="DataBinding" />

```csharp
sidebar.ItemInvoked += (_, e) => router.Navigate(e.Item.Key!);
```

### Icon rail

`IsCollapsed` folds the sidebar into a 64px rail; it is two-way. The collapse button in the header row, the
`ToggleCollapsed()` method and Ctrl+B (⌘B on macOS) with focus in the sidebar do the same. On the rail every page
shows a tooltip on the right, a count becomes a dot on the icon, section headings become thin lines, the filter becomes
a search button that opens the sidebar, and a parent opens its pages in a menu. A page without an icon shows its first
letter.

The header and footer keep the layout they have when the sidebar is open and are clipped to the rail, so a logo or an
avatar at their leading edge stays visible. Use the `:collapsed` pseudo-class to fade the rest, as the preview does.
While the collapse button is visible, the header gives way to it on the rail.

<Demo name="Rail" />

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <UserControl.Styles>
        <Style Selector="arc|Sidebar:collapsed TextBlock.rail-fade">
            <Setter Property="Opacity" Value="0" />
        </Style>
    </UserControl.Styles>
    <arc:Sidebar IsCollapsed="True" Height="320" />
</UserControl>
```

### Filter

`IsFilterVisible` shows a filter box under the header. Pages whose label contains the text stay, a parent with a
matching page opens, and sections without matches hide. Escape clears the box, ↓ moves to the first page and Enter
chooses it. `FilterText` is two-way, and `NoMatchesText` replaces "No matches".

### Live data

The sidebar follows its data: change `Badge`, `Label`, `Icon` or `IsEnabled` on an item, or add and remove items and
sections, and the rows update in place.

<Demo name="LiveUpdates" />

### Keyboard

The list of pages is one tab stop. It starts on the selected page.

| Key | Action |
|---|---|
| ↑ / ↓ | Move to the previous or next visible row |
| Home / End | Move to the first or last row |
| → | Open a parent, or move into its pages when it is open; on the rail, open its menu |
| ← | Close a parent, or move from a page to its parent or section heading |
| Enter / Space | Choose a page, or open and close a parent or a section |
| Letters | Jump to the next page that starts with the typed text |
| Ctrl+B / ⌘B | Collapse or expand the sidebar |

## API reference

<!-- api: Sidebar -->

<!-- api: SidebarSection -->

<!-- api: SidebarItem -->

## Accessibility

- The sidebar is a navigation landmark named by `Label` ("Sidebar" by default).
- The pages form a tree. Each row is named by its label and count ("Inbox, 12"), reports whether it is selected, and a
  parent reports whether it is expanded.
- A collapsible section heading is a disclosure button; other headings are text.
- The collapse button and the rail's search button are named by `CollapseButtonText`, `ExpandButtonText` and
  `FilterPlaceholder`. Override them to localize.
- Keyboard focus shows a 2px focus ring. Disabled pages are dimmed and skipped.

## Motion

- The selection marker, a muted block with an accent bar, glides between rows on a snappy spring. When the selected
  page folds away inside its parent, the marker rests on the parent.
- Parents and collapsible sections open and close on a smooth height spring while the chevron turns.
- Collapsing to the rail springs the width (smooth). Labels, counts and headings fade out quickly while icons stay in
  place, and counts turn into dots.
- Reduced motion changes the width and heights at once and moves the marker at once. Text still fades.
- Off applies every change at once.

## Related

- `Drawer` to host the sidebar on narrow windows, where a fixed sidebar would take too much room.
- `Tabs` to switch between views of one page.
- `TreeView` for deep folder trees.
- `CommandPalette` for keyboard search across pages and actions.
- `UserMenu` or `DropdownMenu` in the header and footer, for a workspace switcher or an account menu.
