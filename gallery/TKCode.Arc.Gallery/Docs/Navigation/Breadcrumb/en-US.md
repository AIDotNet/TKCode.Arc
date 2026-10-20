---
title: Breadcrumb
summary: Show where a page sits in a hierarchy.
---

## When to use

- Showing the path from the root of an area to the current page, such as Workspace, Settings, Billing.
- Client-side paths in a desktop app, such as the folder path of a file browser. Choosing a crumb navigates the
  app instead of a web address.
- Letting the user go back up the hierarchy in one click.

## When not to use

- Browsing a whole tree of items: use `TreeView`.
- Switching between sibling views of the same object: use `Tabs`.
- Moving through pages of one collection: use `Pagination`.

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

### Navigating with a click

Each crumb before the current page can be clicked. Handle `ItemClick` to react to the chosen crumb; `Index`
is its position in the path.

```csharp
breadcrumb.ItemClick += (_, e) =>
{
    status.Text = $"Navigate to {((BreadcrumbItem)e.Item!).Content} (index {e.Index})";
};
```

### Bound path

Set `ItemsSource` to a collection of the path. Use an `ObservableCollection<T>` so that the crumbs
change as the path grows and shrinks.

```csharp
var path = new ObservableCollection<string> { "Home", "Projects" };
breadcrumb.ItemsSource = path;
breadcrumb.ItemClick += (_, e) =>
{
    while (path.Count > e.Index + 1)
    {
        path.RemoveAt(path.Count - 1);
    }
};
```

Opening a folder adds a crumb with `path.Add(name)`. Choosing a crumb removes the crumbs after it, which
animates the removed crumbs out.

### Commands and navigation URIs

A crumb with `Command` runs it with `CommandParameter` and is shown as a button. A crumb with
`NavigateUri` behaves as a link.

```xml
<arc:BreadcrumbItem Content="Billing"
                    Command="{Binding OpenBillingCommand}"
                    CommandParameter="{Binding BillingId}" />
```

### Label

`Label` names the landmark for assistive technology. Set it when the breadcrumb is not the only
navigation landmark on the page.

```xml
<arc:Breadcrumb Label="Folder path" />
```

## API reference

<!-- api: Breadcrumb -->

## Accessibility

- The root is a navigation landmark named by `Label`. Its crumbs are a list with their position in the path.
- The last crumb is text with the current-page state. It is never a link and cannot take focus.
- Tab moves between the crumbs that are links or buttons. Enter follows the focused crumb.
- The chevron separators are hidden from assistive technology.

## Motion

- A new crumb slides in from the left and unblurs while its siblings make room with a smooth spring.
- A removed crumb moves 4px left and fades out quickly. The remaining crumbs make room.
- A path that does not change does not animate its layout.
- Reduced motion shows added and removed crumbs with a fade only, without movement. Off applies the change at
  once.

## Related

- `Tabs` for sibling views of one object.
- `TreeView` for browsing a whole hierarchy.
- `Pagination` for moving through pages of results.
