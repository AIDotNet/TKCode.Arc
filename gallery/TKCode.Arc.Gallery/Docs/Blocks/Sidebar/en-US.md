---
title: Sidebar
summary: Workspace navigation with sections, nested pages, counts and a gliding selection marker; collapses to icons.
---

<Demo name="Preview" />

## When to use

The sidebar is the main navigation of an application that has more pages than a header can hold. It sits at the left
edge of the window for the whole session, so people learn where each page lives and return to it without searching.

- **The main menu of a SaaS admin console**: Home, Inbox, Reports, Billing and Settings for a workspace. Top-level
  places go in the first section without a heading, and areas with their own pages go in headed sections below.
- **Projects and areas in a work tool**: a project list with one level of pages under each project (Atlas, Beacon),
  where a parent row opens or closes its pages instead of showing a view of its own.
- **Mailboxes and docs sites**: folders with counts and colored labels, or a tree of guides that a filter narrows as
  people type.
- **Account and billing areas**: a Billing section with Invoices and Plans, next to a Usage page with a count of new
  events. The count stays visible while the person works in another area.
- **Dense internal tools with many pages**: an operations console where the filter finds a page by its first letters,
  and the icon rail keeps the pages reachable when the window is narrow.
- **Navigation that should stay on screen but take less room on demand**: the sidebar folds into a 64 px icon rail,
  and each page keeps a tooltip and a menu, so the content keeps its width.

## When not to use

- **Two to five views of the same object**, such as Overview, Activity and Settings of one order: use `Tabs`, which
  sit above the content and keep the object in view.
- **Steps of a sequence or a path through a hierarchy**: use `Stepper` for a wizard and `Breadcrumb` for the place of
  a page in a deep structure.
- **A deep tree of folders and files**: use `TreeView`. The sidebar shows one level of nested pages; a tree of files
  needs expanding and collapsing at every depth.
- **Actions rather than places**: use `DropdownMenu` for a menu of commands, or `CommandPalette` for keyboard search
  across pages and actions. A few actions that belong to the list itself ("New project", "More") may sit in it as
  action rows, but a sidebar row should mostly open a page.
- **The signed-in person and workspace switching**: use `UserMenu` in the sidebar's header or footer for the account
  menu, and keep the sidebar itself for pages.

## Usage

Put `SidebarSection` elements inside the sidebar and `SidebarItem` elements inside the sections. A section without a
`Header` has no heading, which suits the first group (Home, Inbox). Give every page a `Key` and bind `SelectedKey` to
your route. The sidebar takes its width from the theme (240 open, 64 as a rail), so place it in a `DockPanel` or an
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

`Sidebar` derives from Avalonia's `TemplatedControl`. It is not focusable itself: the list of rows inside it takes the
focus, so the whole navigation is one tab stop. Its content is the `Sections` collection (XAML content); each
`SidebarSection` holds its pages in `Items`, and a `SidebarItem` holds its child pages in its own `Items`. The model is
deliberately small: two levels of pages, one selected page, and everything else is a count, an icon or a flag.

The selected page gets a muted block behind it and an accent-colored icon, and the block glides from row to row. The
preview at the top of the page is the app rail layout: a logo and a two-line title in the header, the page's own top
bar toggling the sidebar, and the signed-in person in the footer. The layouts below show three more.

### Anatomy

1. **Header** (`Header`): content at the top of the sidebar, typically a logo with a title or a workspace switcher. It
   has 12 px side margins and shares its row with the collapse button. On the rail it gives way to the collapse button;
   without the button it stays, clipped to the rail.
2. **Collapse button** (`IsCollapseButtonVisible`, `CollapseButtonPlacement`, `CollapseButtonText`): a 40 px wide ghost
   icon button with the PanelLeft glyph. It sits at the end of the header row, or beside the filter box. It is shown by
   default.
3. **Filter** (`IsFilterVisible`, `FilterText`, `FilterPlaceholder`): a 32 px high search field under the header with
   12 px side margins. On the rail it becomes a 40 px search button.
4. **Section heading** (`SidebarSection.Header`): a 28 px row with a small text in the muted text color, with 10 px
   above it after the first section. A collapsible heading ends in a 12 px chevron. A section without a `Header` shows
   no heading; after the first section it leaves a 16 px gap instead.
5. **Page row** (`SidebarItem`): 32 px high with a 10 px corner radius and 12 px side padding. It holds a 16 px icon
   (`Icon`) in the muted color, 10 px before the label (`Label`, 13 px, secondary text color) and an optional count at
   the end (`Badge`). Rows are 1 px apart.
6. **Selection block**: a muted wash of the foreground color behind the selected row, with the label at full contrast
   and the icon in the accent color. There is no accent bar. It is one shared element that glides between rows.
7. **Unread dot and action row**: `IsUnread` adds a 6 px accent dot at the end of the row and makes the label stronger.
   An `IsAction` row is muted and is never selected.
8. **Nested page**: a child row is 30 px high, indented 26 px under its parent and hangs from a 1 px guide line in the
   subtle border color. Children of children are not shown.
9. **Parent chevron**: a 14 px chevron at the end of a parent row. It points right when the parent is closed and
   turns down when it is open.
10. **Footer** (`Footer`): content at the bottom, typically the signed-in person, under a 1 px separator. It has 12 px
    side margins and keeps its open-width layout on the rail, clipped to the rail's width.

The pane is a hair darker than the page next to it (a mix of the surface and the muted surface color) with a 1 px
border on the right. The open sidebar is 240 px wide and the rail is 64 px; both come from the theme. `Background`,
`BorderThickness` and `CornerRadius` are ordinary properties, so a sidebar can also float as a rounded card.

### Layouts

The sidebar is one control; the layout around it comes from what you put in its header, filter and footer, from the
frame you give it and from the page next to it. Four layouts show the range, and each one collapses to the rail. The
preview at the top of the page is the first, **App rail**: a header with a logo and a two-line title, no collapse
button of its own (`IsCollapseButtonVisible="False"`) because the page's top bar has a toggle that calls
`ToggleCollapsed()`, and a user block in the footer.

**Workspace.** The sidebar floats as an inset card: `CornerRadius` and `BorderThickness` give it its own rounded
border. The header holds a workspace switcher, and the collapse button sits beside the filter box
(`CollapseButtonPlacement="Filter"`). Pages use colored dots as icons, `IsUnread` marks a project with news, and an
action row ends the list. The footer carries a storage meter (a `Progress`) and the signed-in person (an `Avatar` with a
status dot).

<Demo name="Workspace" />

**Inbox.** A full-width Compose button in the header (on the rail only its icon stays), folders with counts, a
collapsible Labels section with colored dots and a muted "More" row (`IsAction` with a `TrailingIcon`). The page's own
top bar toggles the sidebar.

<Demo name="Inbox" />

**Docs.** A title with a version button in the header and the filter box with the collapse button beside it. The pages
are a tree of groups with icons and a chevron; typing in the filter narrows the tree live and opens the groups that
match. The page beside it is an article with an "On this page" outline: the demo's code-behind watches the scroll
position and glides a 2 px accent bar to the active heading with a `GlidingIndicator`.

<Demo name="Docs" />

All four layouts use `ResizeMode="Overlay"` (see "The page next to the sidebar"). The sidebar's own settings for the
Docs layout are short:

```xml
<arc:Sidebar x:Name="Nav"
             IsFilterVisible="True"
             CollapseButtonPlacement="Filter"
             FilterPlaceholder="Search docs"
             ResizeMode="Overlay">
    <arc:Sidebar.Header>
        <TextBlock Text="Tidewater docs" />
    </arc:Sidebar.Header>
    <arc:SidebarSection>
        <arc:SidebarItem Key="start" Label="Getting started" Icon="{x:Static arc:ArcIcons.Sparkles}" IsExpanded="True">
            <arc:SidebarItem Key="start/introduction" Label="Introduction" />
            <arc:SidebarItem Key="start/quickstart" Label="Quickstart" />
        </arc:SidebarItem>
    </arc:SidebarSection>
</arc:Sidebar>
```

### Sections and nested pages

A `SidebarItem` with child items is a parent. A parent is never selected: choosing it opens or closes its pages, which
hang from a thin guide line. A parent that also needs a landing page should get a child for that page (for example
"Overview" under "Reports"). The `IsExpanded` flag of a parent is two-way. Set `IsCollapsible` on a `SidebarSection`
with a `Header` to let people fold the whole section away; its title then ends in a small chevron, and its own
`IsExpanded` is two-way too. A section that is not collapsible always shows its pages. Only one level of nesting is
shown.

`IsEnabled="False"` dims a page and the arrow keys skip it. A disabled parent cannot be opened either. Each item can
also carry a `Tag` and a `Command` with a `CommandParameter`.

### Counts, unread dots, actions and icons

`Badge` puts a short count at the end of a row, such as "12" for unread messages; a `null` badge shows nothing.
`IsUnread="True"` shows a small accent dot there instead, makes the label stronger and adds "unread" to the row's
accessible name. Use a count when the number matters and the dot when "something is new" is enough. On the rail both
become a dot on the icon.

`Icon` takes a 24 by 24 Lucide geometry, such as an `ArcIcons` glyph, or any control that fits the 16 px icon slot. A
small `Ellipse` makes a colored dot icon that lines up with the glyphs of the other rows. A control icon can sit in one
row only, so create one per item. `TrailingIcon` puts an icon right after the label, in the label's muted color; it
suits a hint such as the chevron of a "More" row.

`IsAction="True"` turns a row into an action: muted and never selected. Choosing it raises `ItemInvoked` and runs its
commands, but `SelectedItem` stays where it was. Use it for "New project" or "More".

```xml
<arc:Sidebar xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SidebarSection Header="Projects">
        <arc:SidebarItem Key="projects/spring" Label="Spring launch">
            <arc:SidebarItem.Icon>
                <Ellipse Width="8" Height="8" Fill="{arc:ArcDesignTokenResource Series1}" />
            </arc:SidebarItem.Icon>
        </arc:SidebarItem>
        <arc:SidebarItem Key="projects/partner" Label="Partner kit" IsUnread="True" />
        <arc:SidebarItem Key="projects/new" Label="New project" IsAction="True" Icon="{x:Static arc:ArcIcons.Plus}" />
        <arc:SidebarItem Key="projects/more" Label="More" IsAction="True" TrailingIcon="{x:Static arc:ArcIcons.ChevronRight}" />
    </arc:SidebarSection>
</arc:Sidebar>
```

### Header, filter and footer

`Header` takes any content: a logo with a two-line title, a workspace switcher, a version chip, a full-width button.
`Footer` sits under a thin separator and takes the signed-in person, a storage meter, or both. Both keep the layout they
have when the sidebar is open and are clipped to the rail, so a logo or an avatar at their leading edge stays visible.
Use the `:collapsed` pseudo-class to fade the rest, as the layouts above do.

`IsFilterVisible` shows the filter box under the header (see "Filter"). The collapse button normally sits at the end of
the header row; `CollapseButtonPlacement="Filter"` puts it beside the filter box instead, as in the Workspace and Docs
layouts. Without a visible filter the button stays in the header row. On the rail it always moves to the top.
`IsCollapseButtonVisible="False"` removes it when the page has its own toggle, as in the app rail and the inbox.

### Selection and routing

`SelectedItem` and `SelectedKey` are two-way and stay in step. `SelectedKey` matches the `Key` of a page and is the
right property to bind to a route, because a route is a string. Setting `SelectedKey` before the pages exist is fine:
the sidebar selects the page once it appears, so a view model can set the route first. A key that matches no page, or
the key of a parent or an action row, selects nothing and keeps its value.

`ItemInvoked` is a routed event that fires every time a page or an action is chosen: by click, by Enter or Space, or
from a menu on the rail. It fires even when the page was already selected, which a binding to `SelectedKey` cannot tell
you. Use it for navigation that must happen on each choice, such as scrolling a page back to the top. After the event
the item's own `Command` runs, then `SelectCommand` with the item as its parameter, each only when it can execute.

<Demo name="DataBinding" />

```csharp
sidebar.ItemInvoked += (_, e) => router.Navigate(e.Item.Key!);
```

### Binding to a view model

The sidebar has no `ItemsSource`: its sections are XAML content, so the rows are written in XAML or, for data that
changes, added and removed in code. The view model holds the route and the collapsed state; the sidebar binds to them
with two-way bindings and runs a command for each choice.

```csharp
public partial class WorkspaceShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    public WorkspaceShellViewModel(INavigationService navigation) => _navigation = navigation;

    [ObservableProperty]
    private string? _selectedKey = "home";

    [ObservableProperty]
    private bool _isNavigationCollapsed;

    [RelayCommand]
    private void OpenPage(SidebarItem item)
    {
        // Runs on every choice, including a page that is already selected.
        _navigation.Navigate(item.Key!);
    }
}
```

```xml
<arc:Sidebar SelectedKey="{Binding SelectedKey, Mode=TwoWay}"
             IsCollapsed="{Binding IsNavigationCollapsed, Mode=TwoWay}"
             SelectCommand="{Binding OpenPageCommand}"
             Label="Main navigation"
             IsFilterVisible="True" />
```

- `SelectedKey` and `IsCollapsed` are two-way, so a route change from elsewhere in the app (a link in a page, a
  back button) selects the matching row, and the rail button writes back to the view model.
- Run navigation side effects in `SelectCommand` or `ItemInvoked`, not in a handler on `SelectedKey`. The selection
  also changes when the route is set from code, and that is not always a choice a person made.
- Set `IsNavigationCollapsed` from your own window width check when the window narrows; the sidebar does not watch the
  window for you.
- When data drives the menu, build the sections in code and keep a reference to the item whose count changes:

```csharp
var inbox = new SidebarItem { Key = "inbox", Label = "Inbox", Icon = ArcIcons.Inbox };
sidebar.Sections.Add(new SidebarSection
{
    Items = { new SidebarItem { Key = "home", Label = "Home", Icon = ArcIcons.House }, inbox }
});

// The row updates in place when the count changes: no rebuild and no lost focus.
inbox.Badge = unreadCount > 0 ? unreadCount.ToString(CultureInfo.InvariantCulture) : null;
```

### Icon rail

`IsCollapsed` folds the sidebar into a 64 px rail; it is two-way, so a button anywhere in your page can drive it with a
binding or a call to `ToggleCollapsed()`. The collapse button in the sidebar and Cmd+B (Ctrl+B on Windows and Linux)
with focus inside the sidebar do the same, even while the filter has focus.

On the rail every page shows a tooltip on its right with its label. A count or an unread mark becomes a 6 px dot on the
icon, section headings become thin separator lines, the filter becomes the search button that opens the sidebar and
puts the caret in the filter, and a parent opens its pages in a menu to its right. The menu lists the parent's label,
then its pages as radio items: the selected page is checked, disabled pages are disabled, and a count appears as the
shortcut text. A page without an icon shows its first letter.

The header and footer keep the layout they have when the sidebar is open and are clipped to the rail, so a logo or an
avatar at their leading edge stays visible. Use the `:collapsed` pseudo-class to fade the rest, as the layouts do.
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

### The page next to the sidebar

By default (`ResizeMode="Push"`) the sidebar's layout width springs, so the page beside it is laid out again on every
frame. That works in any host and is fine for light pages. For a page with a lot of text or many controls set
`ResizeMode="Overlay"`: the layout width then changes at once, so the page is laid out one time, and only the pane
springs, drawn above the page. Bind the `RenderTransform` of the page to `PageShift` and the page glides along with the
pane's edge instead of jumping.

```xml
<DockPanel xmlns="https://github.com/avaloniaui"
           xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
           xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"
           ClipToBounds="True">
    <arc:Sidebar x:Name="Nav" DockPanel.Dock="Left" ResizeMode="Overlay" />
    <Grid RenderTransform="{Binding #Nav.PageShift}">
        <TextBlock Text="Page content" />
    </Grid>
</DockPanel>
```

`PageShift` is a read-only transform that stays at zero in `Push` mode. The page slides by up to the difference between
the open and the rail width, so give the frame around it a clip (`ClipToBounds="True"`), as the layouts above do. If
you leave the transform out, the page still lays out correctly; it jumps to its new position instead of gliding.

### Filter

`IsFilterVisible` shows a filter box under the header. The filter matches each page's `Label` as a case-insensitive
substring, ignoring leading and trailing spaces; action rows are matched too. A page that matches stays; a parent with
a matching child opens; a section without a matching page hides. When nothing matches, the sidebar shows `NoMatchesText`
in place of the rows. `FilterText` is two-way, and `FilterPlaceholder` names the box.

The filter is a search field: Escape clears it, Down moves focus to the first page left, and Enter chooses the first
page left. The filter only decides which rows are shown, so the selected page stays selected while it is filtered out.
The filter is not applied on the rail.

### Data shape and performance

Keep the model to what navigation needs: a `Key` that is unique across the sidebar (`SelectedKey` finds the first
matching page, and a parent's key is never matched), a short `Label`, one icon and an optional count. Pages with more
than one level belong in a `TreeView`.

- **Batched and incremental.** Changes to `Sections` and to the item collections are batched: the rows are brought in
  step once, after the current batch, so adding several pages in one pass costs one update. Only what changed is
  touched: new pages get new rows, removed pages lose theirs, and the rows of pages that stay keep their controls, their
  focus and their selection.
- **Cheap property changes.** A change to `Badge`, `IsUnread`, `IsAction`, `Icon` or `TrailingIcon` updates that row
  only; a change to `Label` or `IsEnabled` also refreshes the filter and the disabled pages.
- **Closed parents stay out of layout.** A parent or a collapsible section that has closed and settled leaves layout
  and rendering altogether, so a tree with many closed parents is cheap to lay out and draw.
- **Off screen, nothing is held.** While the sidebar is not on screen it holds no rows and no subscriptions to your items,
  so shared or static items do not keep it alive; the rows are built again when it returns.
- **The width spring.** In `Push` mode every frame of the collapse lays the page out again; in `Overlay` mode the page
  is laid out once and only the pane's arrangement changes (see "The page next to the sidebar").

The rows are real controls, so the sidebar suits a few dozen pages; a larger catalog should be grouped or searched with
`CommandPalette`.

### Live data

The sidebar follows its data: change `Badge`, `IsUnread`, `Label`, `Icon` or `IsEnabled` on an item, or add, move and
remove items and sections, and only the rows concerned change. A new page makes its section grow on the same spring as
opening it.

<Demo name="LiveUpdates" />

```csharp
// Each line touches only the rows concerned: the other rows keep their controls, focus and selection.
inbox.Badge = "3";
inbox.IsUnread = true;
pinned.Items.Add(new SidebarItem { Key = "pinned/roadmap", Label = "Roadmap", Icon = ArcIcons.Flag });
pinned.Items.RemoveAt(0);
```

### Example: admin console navigation

A navigation for a SaaS analytics workspace. The unread count sits on Inbox, Billing folds away for finance users, a
project with news carries an unread dot, and an action row ends the project list. The header names the workspace and the
footer the signed-in person, so people always know which account they are in.

![An admin console navigation with a selected report, a collapsible billing section and a project list](images/AdminNavigation.png)

```xml
<DockPanel Width="720" Height="600">
    <arc:Sidebar DockPanel.Dock="Left"
                 SelectedKey="reports"
                 IsFilterVisible="True"
                 CollapseButtonPlacement="Filter"
                 FilterPlaceholder="Search">
        <arc:Sidebar.Header>
            <StackPanel Spacing="2" Margin="6,0,0,0">
                <TextBlock Text="Acme Analytics"
                           FontSize="{arc:ArcDesignTokenResource TypeSM}"
                           FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Production workspace"
                           FontSize="{arc:ArcDesignTokenResource TypeXS}"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </StackPanel>
        </arc:Sidebar.Header>
        <arc:SidebarSection>
            <arc:SidebarItem Key="home"
                             Label="Home"
                             Icon="{x:Static arc:ArcIcons.House}" />
            <arc:SidebarItem Key="inbox"
                             Label="Inbox"
                             Icon="{x:Static arc:ArcIcons.Inbox}"
                             Badge="12" />
            <arc:SidebarItem Key="reports"
                             Label="Reports"
                             Icon="{x:Static arc:ArcIcons.ChartBar}" />
        </arc:SidebarSection>
        <arc:SidebarSection Header="Billing" IsCollapsible="True">
            <arc:SidebarItem Key="usage"
                             Label="Usage"
                             Icon="{x:Static arc:ArcIcons.Activity}" />
            <arc:SidebarItem Key="invoices"
                             Label="Invoices"
                             Icon="{x:Static arc:ArcIcons.CreditCard}" />
        </arc:SidebarSection>
        <arc:SidebarSection Header="Workspace">
            <arc:SidebarItem Key="projects"
                             Label="Projects"
                             Icon="{x:Static arc:ArcIcons.Folder}"
                             IsExpanded="True">
                <arc:SidebarItem Key="projects/atlas" Label="Atlas" />
                <arc:SidebarItem Key="projects/beacon" Label="Beacon" IsUnread="True" />
            </arc:SidebarItem>
            <arc:SidebarItem Key="team"
                             Label="Team"
                             Icon="{x:Static arc:ArcIcons.Users}" />
            <arc:SidebarItem Key="new-project"
                             Label="New project"
                             IsAction="True"
                             Icon="{x:Static arc:ArcIcons.Plus}" />
        </arc:SidebarSection>
        <arc:Sidebar.Footer>
            <StackPanel Orientation="Horizontal" Spacing="10" Margin="6,0,0,0">
                <arc:Avatar DisplayName="Maya Chen" Size="Small" Status="Online" />
                <StackPanel VerticalAlignment="Center">
                    <TextBlock Text="Maya Chen"
                               FontSize="{arc:ArcDesignTokenResource TypeSM}"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="Owner"
                               FontSize="{arc:ArcDesignTokenResource TypeXS}"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
            </StackPanel>
        </arc:Sidebar.Footer>
    </arc:Sidebar>
    <StackPanel Margin="32,28" Spacing="6">
        <TextBlock Text="Reports"
                   FontSize="{arc:ArcDesignTokenResource TypeXL}"
                   FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Revenue by product, last 30 days"
                   FontSize="{arc:ArcDesignTokenResource TypeSM}"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
</DockPanel>
```

Keep the keys stable: a key such as `projects/atlas` is what a bookmark or a deep link stores, so renaming the label
is safe and changing the key is not.

### Content guidelines

- **Label**: a short noun or noun phrase in sentence case, such as "Invoices" or "Team members". Use the words the page
  itself uses in its heading, so the row and the page title agree.
- **Action row**: a verb and its object in sentence case, such as "New project". Everything else in the sidebar is a
  noun, so the verb tells people the row does something instead of opening a page.
- **Section heading**: a group name in sentence case, without a verb. Leave the heading out when the group has one
  obvious purpose, as Home and Inbox do.
- **Count**: digits only, and a count only for things that wait for the person: unread messages, open alerts, pending
  reviews. A count that never changes is a label. When the number does not matter, use the unread dot.

| Instead of | Write |
|---|---|
| Home Page | Home |
| Click here to see your invoices | Invoices |
| Reports (NEW!!) | Reports |
| Team & Permissions Management | Team members |
| Stuff you can change | Settings |
| Add a new project to your workspace | New project |

### Best practices

**Do**

- Put the places people visit most in the first section, where it has no heading; reserve headed sections for groups
  with a clear name. The first section is the first thing the eye reads.
- Bind `SelectedKey` to the route and keep the keys stable, so a deep link, a bookmark and the selected row agree.
- Show a count or an unread dot only for work that waits for the person, and update it in place; a mark that grows
  stale teaches people to ignore it.
- Set `ResizeMode="Overlay"`, bind the page's `RenderTransform` to `PageShift` and clip the frame when the page next to
  the sidebar has a lot of content.
- Fade what you put in the header and footer with the `:collapsed` pseudo-class, and keep a logo or an avatar at the
  leading edge so something recognizable stays on the rail.

**Avoid**

- Making a parent the only way to reach a page. A parent opens and closes but is never selected, so a parent with no
  landing page sends people to a dead end. Add a child for the landing page instead.
- Using action rows for more than a place or two. "New project" and "More" suit the list they end; "Log out" belongs in
  a menu or a button, because a row that changes the page makes people expect a place.
- Nesting more than one level, or building a folder tree in it. Only one level of children is shown, and a deeper tree
  is hard to scan and to navigate with the arrow keys.
- Long labels. The label shares a 216 px row with the icon and the count, the rail tooltip shows the same text, and the
  filter matches it, so a short, distinct label is easier to find in every mode.
- Setting `Width` on the sidebar. The theme sets the open and rail widths, and the collapse springs between them; a
  local width overrides both and stops the collapse from changing the width.
- Sharing one control between rows as an icon. A control can sit in one row only; give each item its own, or use a
  geometry.

### Common questions

**Why does choosing a parent not open a page?** A parent is never selected. Choosing it opens or closes its pages.
Give the parent a child page for its landing view, and bind `SelectedKey` to that child's key.

**How do I hide a page instead of disabling it?** Remove the item from its parent's or section's `Items`. The rows
update in place, and the page reappears when you add it back. `IsEnabled="False"` keeps the page visible, dimmed, and
out of the arrow key order.

**What happens when `SelectedKey` names a page that does not exist?** Nothing is selected, and the key keeps its value.
When a page with that key is added later, the sidebar selects it. This lets a route set at startup work before the
navigation data has loaded.

**Can an action row be selected?** No. An `IsAction` row raises `ItemInvoked` and runs its commands, but `SelectedItem`
and `SelectedKey` stay where they were, so the page the person is on keeps its marker.

**Why does the page next to the sidebar stutter while it collapses?** In the default `Push` mode the page is laid out
on every frame of the width spring. Set `ResizeMode="Overlay"` and bind the page's `RenderTransform` to `PageShift`.

**Why does Cmd+B or Ctrl+B not collapse the sidebar?** The shortcut works only while focus is inside the sidebar, on
one of its rows or in its filter. Click a row first, or bind `IsCollapsed` to a toggle button elsewhere in the window.

## API reference

<!-- api: Sidebar -->

<!-- api: SidebarSection -->

<!-- api: SidebarItem -->

## Accessibility

- The sidebar is a navigation landmark named by `Label` ("Sidebar" by default). Use a specific name such as "Main
  navigation" when a page has more than one navigation region.
- The pages form a tree. Each row is named by its label, its count and "unread" when it has `IsUnread` ("Inbox, 12"),
  reports whether it is selected, and a parent reports whether it is expanded. `UnreadText` localizes the word.
- A collapsible section heading is a disclosure button; other headings are text and are not in the tab order.
- The collapse buttons and the rail's search button are named by `CollapseButtonText`, `ExpandButtonText` and
  `FilterPlaceholder`. Override them to localize. A button that is hidden (the one in the header while the other sits
  beside the filter) takes no focus.
- On the rail each page keeps its label as a tooltip, so people who use a pointer can still read the names of the
  icons. A parent's menu opens from the keyboard with Right or Enter and lists its pages as radio items.
- Keyboard focus shows a 2 px focus ring around the row. Disabled pages are dimmed and skipped by the arrow keys.
- Do not rely on color alone: the unread dot is paired with a stronger label and the word "unread" in the accessible
  name, and a count is part of the name.

### Keyboard

The sidebar follows the WAI-ARIA tree model for navigation. The list is one tab stop, and it starts on the selected
page, or on the first page when none is selected.

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Enter the list on the focused or selected row, or leave it |
| `Up` / `Down` | Move to the previous or next visible row; disabled pages are skipped |
| `Home` / `End` | Move to the first or last visible row |
| `Page Up` / `Page Down` | Move by about one visible page of rows |
| `Right` | Open a parent, or move into its first page when it is open; on the rail, open its menu |
| `Left` | Close an open parent, or move from a child to its parent; from a page of a collapsible section, go up to its heading |
| `Enter` / `Space` | Choose a page or an action; open or close a parent, or fold a collapsible section heading |
| Letters | Jump to the next visible page whose label starts with the typed text |
| `Cmd+B` (`Ctrl+B` on Windows and Linux) | Collapse or expand the sidebar while focus is inside it |
| `Down`, `Enter`, `Escape` in the filter | Move to the first page; choose the first page left; clear the filter |

## Motion

- The selection block, a muted wash with no bar, glides between rows on a snappy spring; the icon of the selected page
  changes to the accent color. When the selected page folds away inside its parent, the block rests on the parent.
- Parents and collapsible sections open and close on a smooth height spring while the chevron turns on a snappy one. A
  closed one leaves layout and rendering altogether once it has settled. A new page makes its section grow on the same
  spring.
- Collapsing to the rail springs the width (smooth). The text of every row (label, trailing icon, count, unread dot and
  chevron) fades out together while icons stay in place, and counts and unread marks turn into dots on the icons. In
  `Overlay` mode the spring runs on the pane alone and keeps its speed when you toggle again in the middle of it; in
  `Push` mode it restarts from the current width.
- Reduced motion changes the width and the heights at once, turns the chevrons at once and moves the selection block
  at once. The text of the rows, the header, the filter box and the search button still fade quickly.
- Off applies every change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The selection block moves | Glides between rows on a snappy spring | Jumps to the row | Jumps to the row |
| A parent or collapsible section opens or closes | Height springs (smooth) and the chevron turns (snappy) | Height and chevron change at once | Immediate |
| A page is added to a section | The section grows on the height spring (smooth) | Grows at once | Immediate |
| The sidebar collapses or expands | Width springs (smooth); in `Overlay` mode only the pane springs | Width changes at once | Immediate |
| The text of a row and the section headings on the rail | Fade quickly (fast) | Fade quickly (fast) | Immediate |
| The header, the filter box and the search button on the rail | Fade quickly (fast) | Fade quickly (fast) | Immediate |
| Row hover and press | Background and text color crossfade (fast) | Change at once | Immediate |

## Related

- `Drawer` hosts the sidebar on narrow windows, where a fixed sidebar would take too much room.
- `Tabs` to switch between views of one page.
- `TreeView` for deep folder trees.
- `CommandPalette` for keyboard search across pages and actions.
- `UserMenu` or `DropdownMenu` in the header and footer, for a workspace switcher or an account menu.

| Component | Choose it when |
|---|---|
| `Tabs` | Two to five views share one object and its header stays in view |
| `Breadcrumb` / `Stepper` | The place in a hierarchy or the position in a sequence is the point |
| `TreeView` | The pages or files nest more than one level deep |
| `CommandPalette` | People search for a page or an action by name rather than browse |
| `Drawer` | The window is too narrow for a fixed sidebar and the navigation should slide in |
| `UserMenu` | The signed-in person needs a menu of account actions in the header or footer |
