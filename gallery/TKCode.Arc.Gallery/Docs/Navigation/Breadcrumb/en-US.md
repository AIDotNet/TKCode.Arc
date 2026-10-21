---
title: Breadcrumb
summary: Show where a page sits in a hierarchy.
---

<Demo name="Default" />

## When to use

`Breadcrumb` shows the path from the root of an area down to the page the person is on, such as Workspace, Settings
and Billing. It sits above the page heading, where it answers "where am I?" and gives one-click exits back up the
hierarchy. In a desktop application it can also show a client-side path, such as the folder path of a file browser,
where choosing a crumb changes the view instead of an address.

- **A billing section in an admin console**: the path Acme Analytics, Billing, Invoices, inv_2026_0412 tells an
  administrator which workspace and section they are in, and each parent crumb returns to the list above it.
- **A file browser or asset library in a desktop app**: one crumb per folder (Home, Projects, Q3 Report). Choosing a
  crumb removes the crumbs after it, and the folder list updates to match the shorter path.
- **A deep settings tree**: Organization, Security, Single sign-on. People often arrive at a settings page from a
  search result or a link, so the parents show the way back.
- **Developer tools and incident records**: Platform, Incidents, INC-2026-114 shows the team and product that own a
  record, which matters when the same ID exists in several places.
- **Long hierarchies on narrow windows**: with `MaxVisibleItems`, the root and the last few levels stay visible while
  the middle collapses into one overflow button, so a deep path fits a sidebar-width column.

## When not to use

- **Browsing a whole tree of items with expandable nodes**: use `TreeView`, which shows the siblings at each level and
  keeps the branches open.
- **Switching between sibling views of the same object**, such as Overview, Activity and Settings: use `Tabs`. The
  crumbs are places people go back to, not panels they switch between.
- **Moving through pages of one collection**: use `Pagination`, which knows the page count and the current page
  number.
- **Steps the person completes in order**, such as Cart, Shipping, Payment and Review: use `Stepper`, which shows
  progress and the state of each step.
- **Choosing one value from a list**, such as a folder or a project in a form: use `Select` or `Combobox`. A breadcrumb
  does not hold a value the form submits.

## Usage

Each crumb is a `BreadcrumbItem`. The last crumb is the current page: it is plain text and never a link.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Breadcrumb>
        <arc:BreadcrumbItem Content="Workspace" />
        <arc:BreadcrumbItem Content="Settings" />
        <arc:BreadcrumbItem Content="Billing" />
    </arc:Breadcrumb>
</UserControl>
```

`Breadcrumb` derives from Avalonia's `ItemsControl`, so `Items`, `ItemsSource` and `ItemTemplate` behave as they do for
any list. Every item is wrapped in a `BreadcrumbItem`, a `ContentControl`, so its label is `Content`. The panel lays the
crumbs out in a row and wraps them onto a new line when the path is wider than the space. The breadcrumb itself never
takes focus; only the crumbs that can be chosen do.

### Anatomy

1. **Landmark** (`Breadcrumb`, `Label`): a navigation landmark named by `Label`, which defaults to "Breadcrumb". Its
   crumbs form a list, and each one is announced with its position in the path.
2. **Separator**: a 16 px chevron in the border-strong color, drawn with a 1.75 stroke. It sits in front of every crumb
   except the first, so the root has no chevron. A 6 px gap separates the chevron from its label.
3. **Label** (`BreadcrumbItem.Content`): the small type size in the muted text color, with 2 px horizontal and 4 px
   vertical padding inside a frame with a 7 px radius. Neighboring crumbs are 6 px apart.
4. **Hover underline**: a 1 px line under the label of an interactive crumb. It appears on hover at 45% opacity.
5. **Current crumb** (`IsCurrent`): the last crumb. It uses the accent strong color and medium weight, and it is not
   interactive. Each crumb reserves the width of its label in medium weight, so becoming the current crumb never moves
   the crumbs before it.
6. **Overflow button**: when `MaxVisibleItems` collapses the path, the first hidden position becomes an ellipsis button
   named by `OverflowLabel`.
7. **Focus ring**: a 2 px ring in the focus color, drawn 4 px outside the label frame. It appears only for keyboard
   focus.
8. **Wrapped lines**: 2 px between lines when the path wraps.

### Navigating with a click

Each crumb before the current page can be chosen with the pointer, `Enter` or `Space`. Handle `ItemClick` to react to
the chosen crumb. `Index` is its position in the path, where 0 is the root, and `Item` is the data item (or the
`BreadcrumbItem` written inline).

```csharp
breadcrumb.ItemClick += (_, e) =>
{
    status.Text = $"Navigate to {((BreadcrumbItem)e.Item!).Content} (index {e.Index})";
};
```

The `ItemClick` event runs first. Then the crumb's `Command` runs with its `CommandParameter`, and then its
`NavigateUri` is opened. Handle the event for in-app navigation, because it gets the index and the item in one place;
use the other two only when the crumb itself owns the action.

### Bound path

Set `ItemsSource` to a collection of the path. Use an `ObservableCollection<T>` so that the crumbs change as the path
grows and shrinks.

<Demo name="ClientSidePath" />

Opening a folder adds a crumb with `path.Add(name)`. Choosing a crumb removes the crumbs after it, which animates the
removed crumbs out. A crumb added to the end slides in from the left, and the crumbs that stay in place glide to make
room. A crumb that keeps its label at the same index does not play its entry animation again.

### Commands and navigation URIs

A crumb with `Command` runs it with `CommandParameter`, when `CanExecute` returns true, and is presented as a button.
A crumb with `NavigateUri` is presented as a link, and the address opens through the top level's `Launcher`, which
hands it to the platform. Every crumb that can be chosen is announced as a button, and a crumb with a `NavigateUri`
is announced as a link. For a desktop application's own views, prefer `ItemClick` or a command, so that the view
changes inside the window rather than in a browser.

```xml
<arc:BreadcrumbItem Content="Billing"
                    Command="{Binding OpenBillingCommand}"
                    CommandParameter="{Binding BillingId}" />
```

A crumb with `IsClickable="False"` is plain text even when it is not the current page. Use it for a segment that is
a label only, such as a year group that has no page of its own.

### Label

`Label` names the landmark for assistive technology. Set it when the breadcrumb is not the only navigation landmark on
the page, so that a screen reader user can tell the two apart. The default is "Breadcrumb".

```xml
<arc:Breadcrumb Label="Folder path" />
```

### Collapsed path

`MaxVisibleItems` folds the middle of a long path into an overflow menu. It is off by default, as in Arc, where the
path is never collapsed. When the path is longer than `MaxVisibleItems`, the root stays visible, the first hidden
position becomes an ellipsis button, and the last `MaxVisibleItems − 1` crumbs stay visible. The button opens a menu
that lists the hidden crumbs in path order. Choosing an entry raises `ItemClick` with the entry's index in the path.

<Demo name="CollapsedPath" />

The menu is a light-dismiss popup below the button. It is opened and closed in code with `OpenOverflow()` and
`CloseOverflow()`, and `IsOverflowOpen` reports whether it is showing. The menu moves focus with `Up`, `Down`, `Home`
and `End`, closes on `Escape` and returns focus to the button, and closes after a choice has been made.

### Data shape and performance

A breadcrumb describes a path, so keep its items to the levels that matter. Each item is one `BreadcrumbItem`
container, and the panel measures and arranges all of them; the control does not virtualize its items. A path of a
few levels is the intended use. For a deep hierarchy, shorten the labels and use `MaxVisibleItems` instead of
building a long path that the window must wrap.

Labels should be text. A crumb whose content is a control keeps its place without a reserved width for the medium
weight, and the overflow menu shows the control's automation name rather than the control itself.

### Binding to a view model

A file browser keeps the path in its view model and binds the breadcrumb to it. With CommunityToolkit.Mvvm, the path is
an `ObservableCollection<string>` that the view model changes, and the breadcrumb only displays it.

```csharp
public partial class FolderBrowserViewModel : ObservableObject
{
    public ObservableCollection<string> Path { get; } = new() { "Home", "Projects", "Acme Analytics" };

    public void OpenFolder(string name)
    {
        Path.Add(name);
        // Load the listing for the new path here.
    }

    public void NavigateTo(int index)
    {
        while (Path.Count > index + 1)
        {
            Path.RemoveAt(Path.Count - 1);
        }
    }

    [RelayCommand]
    private void GoUp()
    {
        if (Path.Count > 1)
        {
            Path.RemoveAt(Path.Count - 1);
        }
    }
}
```

```xml
<StackPanel Spacing="12">
    <arc:Breadcrumb ItemsSource="{Binding Path}" Label="Folder path" />
    <arc:Button Content="Up one level" Variant="Ghost" Command="{Binding GoUpCommand}" />
</StackPanel>
```

```csharp
breadcrumb.ItemClick += (_, e) => viewModel.NavigateTo(e.Index);
```

The breadcrumb never writes to the collection. A crumb choice is a request, so
the view model decides whether the navigation happens, truncates the path with `NavigateTo`, and loads the new
listing. The `Path` collection is the only source of truth; do not keep a second copy of the current folder name.

### Example: billing section header

A page header in an admin console for one invoice. The path says where the invoice sits, the heading names it, and the
two buttons act on it. The current crumb is the invoice ID, which is text and not a link, so the person sees the
location without a link that leads back to the same page. The label "Invoice location" distinguishes this landmark from
the navigation in the sidebar.

![A billing page header with an invoice breadcrumb, a heading and two actions](images/InvoiceHeader.png)

```xml
<StackPanel Width="640" Spacing="12">
    <arc:Breadcrumb Label="Invoice location">
        <arc:BreadcrumbItem Content="Acme Analytics" />
        <arc:BreadcrumbItem Content="Billing" />
        <arc:BreadcrumbItem Content="Invoices" />
        <arc:BreadcrumbItem Content="inv_2026_0412" />
    </arc:Breadcrumb>
    <TextBlock Text="Invoice inv_2026_0412"
               FontSize="24"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="Northwind Trading, issued April 3, 2026. Paid on April 9, 2026."
               FontSize="14"
               TextWrapping="Wrap"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    <StackPanel Orientation="Horizontal" Spacing="8">
        <arc:Button Content="Download PDF" Variant="Secondary" />
        <arc:Button Content="Send receipt" />
    </StackPanel>
</StackPanel>
```

### Content guidelines

- **Crumb label**: the name of the place as the person knows it, in title case when it is a proper name ("Acme
  Analytics") and sentence case for a generic section ("Billing"). Keep each label to one or two words so the path
  stays on one line.
- **Current crumb**: the name of the page, not a sentence. It repeats the heading, so keep the two identical.
- **Label (landmark)**: a short noun phrase that names the path, such as "Invoice location" or "Folder path". Avoid
  "Breadcrumb" in translated interfaces; name the content.
- **Overflow label**: say what the button reveals ("Show hidden path"), not what it is made of.

| Instead of | Write |
|---|---|
| Landmark label "Breadcrumb" | Invoice location |
| Current crumb as a link to the same page | The current page name, without a link |
| Overflow label "More" | Show hidden path |
| Crumb "Go to the billing overview page" | Billing |
| Crumb "Current page: Invoices" | Invoices |

### Best practices

**Do**

- Put the breadcrumb above the page heading, and make the heading and the current crumb the same name, so the path and
  the title agree.
- Set `Label` on every breadcrumb when a page has more than one navigation landmark, so assistive technology users can
  tell them apart.
- Use `ItemClick` for in-app navigation, so the view model decides how the navigation happens and the crumb stays a
  simple request.
- Set `MaxVisibleItems` for paths that can grow past what a column holds, and test the path at the narrowest width the
  page supports.

**Avoid**

- Using a breadcrumb for sibling views or steps. People expect the crumbs to lead up the hierarchy, and sibling views
  or steps send them sideways or forward, which confuses the path.
- Linking the current page. The last crumb is the page the person is on; a link to it reloads the same view and makes
  the current state harder to read.
- Building the path from strings in code-behind and then copying it into the view. Bind the path to one collection so
  that the crumbs and the content change together.
- Putting controls or long sentences into the crumbs. A crumb does not wrap its own label, so a sentence makes one crumb
  much wider than the rest and pushes the path onto a second line.

### Common questions

**Why does the current crumb not respond to a click?** The last crumb is the current page, so it is not interactive,
has no hover underline and is not in the tab order. Its `IsCurrent` value follows the position: whichever crumb is last
in the path is current.

**How do I stop a middle crumb from being a link?** Set `IsClickable="False"` on that `BreadcrumbItem`. It stays in the
path and its label stays visible, but it is plain text and cannot be chosen.

**What do I read to know which crumb was chosen, and how do I get the data item?** Read `e.Index` and `e.Item` in the
`ItemClick` handler. For an `ItemsSource` path, `Item` is the data object; for inline crumbs it is the `BreadcrumbItem`.

**Does the overflow menu close when the path changes?** Yes. Changing the items or `MaxVisibleItems` closes the menu,
so a path update that arrives while the menu is open does not leave a stale list on screen.

**Can the path show more than one line?** Yes. The crumbs wrap onto a new line when the path is wider than the space,
with 2 px between the lines. Prefer `MaxVisibleItems` when the path can get long, because a wrapped path is harder to
scan.

## API reference

<!-- api: Breadcrumb -->

## Accessibility

- The root is a navigation landmark named by `Label`, which defaults to "Breadcrumb". Its crumbs form a list, and each
  one is announced with its position and the size of the path, for example "2 of 4".
- The current crumb is announced as the current page. It is text, not a button or a link, and it cannot take focus.
- A crumb with a `NavigateUri` is announced as a link; a crumb that can be chosen otherwise is announced as a button.
- The chevron separators are hidden from assistive technology.
- The overflow button is named by `OverflowLabel`, which defaults to "Show hidden path". The hidden crumbs appear in its
  menu with their own names.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move between the crumbs that can be chosen, and the overflow button; the current crumb is skipped |
| `Left` / `Right` | Move focus to the previous or next crumb that can be chosen |
| `Enter` / `Space` | Choose the focused crumb, which raises `ItemClick` and runs its command or opens its address |
| `Down` / `Up` | In the overflow menu, move to the next or previous entry, wrapping at the ends |
| `Home` / `End` | In the overflow menu, move to the first or last entry |
| `Enter` / `Space` in the overflow menu | Choose the entry; the menu closes and focus returns to the button |
| `Escape` | In the overflow menu, close it and return focus to the overflow button |

A pointer press and release on a crumb chooses it, when the release happens inside the crumb.

## Motion

- A new crumb slides in from the left and unblurs while its siblings make room with a smooth spring.
- A removed crumb moves 4px left and fades out quickly. The remaining crumbs make room.
- A path that does not change does not animate its layout.
- Reduced motion and Off apply added and removed crumbs at once. The slide and the exit run only at full motion.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A crumb is added at the end | Slides in 8 px from the left and unblurs over the standard duration | Shown at once | Shown at once |
| A crumb is removed | Moves 4 px left, fades and blurs out over 120 ms | Removed at once | Removed at once |
| Crumbs after a change | Glide to their new positions on the smooth spring | Jump to their new positions | Jump to their new positions |
| Hover, focus, current | The label color crossfades | Immediate | Immediate |

## Related

- `Tabs` for sibling views of one object.
- `TreeView` for browsing a whole hierarchy.
- `Pagination` for moving through pages of results.

| Component | Choose it when |
|---|---|
| `Tabs` | The views are peers of the same object, and each has its own panel |
| `TreeView` | People browse a hierarchy by expanding and collapsing nodes |
| `Pagination` | The results are one collection split into pages |
| `Stepper` | The person completes steps in a fixed order |
| `PageHeader` (block) | A page needs a heading, a breadcrumb and actions in one standard header |
| `Select` / `Combobox` | The person picks one value from a list in a form |
