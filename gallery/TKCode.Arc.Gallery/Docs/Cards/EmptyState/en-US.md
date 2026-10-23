---
title: Empty state
summary: A useful next step when there is nothing to show yet.
---

<Demo name="FirstUse" />

## When to use

`EmptyState` fills the place where a list, a folder or a result set would be when it has nothing to show. It
explains why the area is empty and offers the one next step that makes sense, so the person is never left at a
blank panel. It is a centered block of an icon, a title, one or two sentences and an optional action, and it sits
inside the same surface as the content it replaces.

- **First use of a list** in an admin console or a SaaS product: "No projects yet" with a primary "New project"
  button, shown where the project table will appear once the first project exists.
- **Zero results from a search or a filter** in an orders, invoices or customers list: "No results for
  'quarterly plan'" with "Clear search" and "Browse all" actions, so the person can recover without retyping.
- **A finished step that leaves nothing to list**, such as a payouts page after every invoice is paid: "All invoices
  are paid" with the next scheduled date in the description and no action at all.
- **A state that changes in place**: a team page that shows "No members yet" until the first invitation is accepted,
  then "Invitation sent" with a "Resend invite" action, without a page change.
- **Empty inbox or activity feeds** in a developer tool or a CRM: "No deployments in the last 7 days" with a link to the
  deployment history, so the empty area explains the time window it covers.

## When not to use

- **The data is still loading**: use `Skeleton`, which keeps the shape of the content. An empty state while a request
  is in flight tells people the wrong thing.
- **A failed request or an error on a page that already has content**: use `Alert`, placed above the content, so the
  data that did load stays visible.
- **A grid item that has media, an owner and footer actions**: use `Card`. An empty state is not a tile and should not
  look like one.
- **A confirmation that something was saved or changed**: use `Toast`, which does not replace the content on the
  page.
- **A large, multi-scene illustration with tabs**: use the `EmptyStates` block, which is a ready-made set of scenes.
  Use `EmptyState` when one message belongs to one place in your page.

## Usage

`Title` and `Description` carry the message, and `Action` adds the call to action. `Label` names the region for
assistive technology. The default icon is a folder.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:EmptyState Label="Projects"
                    Title="No projects yet"
                    Description="Projects keep your files, people and conversations in one place.">
        <arc:EmptyState.Action>
            <arc:Button Content="New project" Variant="Primary" />
        </arc:EmptyState.Action>
    </arc:EmptyState>
</UserControl>
```

`EmptyState` derives from Avalonia's `TemplatedControl`. It has no value, selection or items, so it raises no events.
Its only interactive parts are the buttons you place in `Action`, which keep their own focus, press and command
behavior.

### Anatomy

1. **Icon block** (fed by `Icon`): a 48 px square with a 1 px border in the border color, the muted
   surface color and the panel radius. The glyph is 24 px, drawn with a 1.5 stroke in the secondary text color.
2. **Title** (`Title`): 16 px, medium weight, 22.4 px line height (1.4), in the foreground color, centered. It is a
   heading at level 3. It sits 20 px below the icon block.
3. **Description** (`Description`): 14 px, 19.6 px line height, in the secondary text color, centered. It sits 8 px
   below the title and is capped at 288 px wide (18 rem), so it wraps to a short measure.
4. **Action** (`Action`): a row centered 20 px below the description. It is hidden until `Action` has a value.
5. **Padding**: 20 px on each side. The vertical padding follows the window width: 8% of it, kept between 32 px and
   48 px.
6. **Region**: the whole control is a region landmark. Its accessible name is `Label`.

The title and description are balanced: when the copy wraps, the lines are narrowed to an even length instead of
leaving one short last line, so a two-line description looks centered.

### Choosing the copy and the icon

The icon and the copy say why the area is empty. Choose the state first, then write the copy and pick the action
that moves the person forward.

| State | Icon | Title | Action |
|---|---|---|---|
| First use, nothing exists yet | Default folder, or the feature's own icon | "No projects yet" | The primary creation action |
| Search or filter returned nothing | `ArcIcons.Search` | "No results for 'quarterly plan'" | Clear the filter, or browse all |
| Work is done, nothing to act on | `ArcIcons.Check` | "All invoices are paid" | None, or a link to history |
| A change succeeded in place | `ArcIcons.Check` | "Project created" | Usually none; remove the old action |

The default folder suits a list that has no items yet. A search icon tells the person the list exists and their
query filtered it out. Keep one icon per state, and do not decorate the block with a different icon on every visit.

### Icon

`Icon` takes a control, or a 24 by 24 Lucide `Geometry` such as one of the `ArcIcons` glyphs. Passing a geometry
draws it at 24 px with the 1.5 stroke, which is the Arc look. Pass a control when you need a different size or a
composed image. Set `Icon` to `null` to return to the default folder.

```xml
<arc:EmptyState Title="No results"
                Description="Check the spelling or try a broader term."
                Icon="{Binding SearchIcon}" />
```

```csharp
empty.Icon = ArcIcons.Search;
```

When the icon changes, the old one is replaced by the new one with a pop. Setting the same icon again leaves it still,
so a view model can assign the icon on every refresh without an animation on each refresh.

### Action row

`Action` takes one button, or a panel with several buttons. The row is centered below the description. Use one
primary action for the way forward. When the state needs a recovery action too, put a secondary action beside it in
a `StackPanel` and keep the primary button first, so it is the first stop in the tab order.

<Demo name="ZeroResults" />

```xml
<arc:EmptyState Icon="{x:Static arc:ArcIcons.Search}"
                Title="No results for “quarterly plan”"
                Description="Check the spelling or try a broader term.">
    <arc:EmptyState.Action>
        <StackPanel Orientation="Horizontal" Spacing="12">
            <arc:Button Content="Clear search" Variant="Secondary" />
            <arc:Button Content="Browse all" Variant="Ghost" />
        </StackPanel>
    </arc:EmptyState.Action>
</arc:EmptyState>
```

Give each button a verb and an object, such as "Clear search" or "New project". A button that repeats the title
("Projects") tells the person nothing.

### Driving it from data

Most empty states depend on a count, a filter or a load result. Keep the decision in the view model and let the
`EmptyState` show the state that matches. The control is shown only when the list is empty, so the list and the
empty state never compete for the same space.

```csharp
public partial class ProjectsViewModel : ObservableObject
{
    public ObservableCollection<ProjectRow> Projects { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    public bool HasProjects => Projects.Count > 0;

    public string EmptyTitle => string.IsNullOrWhiteSpace(SearchText)
        ? "No projects yet"
        : $"No results for “{SearchText}”";

    public string EmptyDescription => string.IsNullOrWhiteSpace(SearchText)
        ? "Projects keep your files, people and conversations in one place."
        : "Check the spelling or try a broader term.";

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private async Task CreateProjectAsync()
    {
        await _projects.CreateAsync();
    }
}
```

```xml
<Grid>
    <ItemsControl ItemsSource="{Binding Projects}" IsVisible="{Binding HasProjects}" />
    <arc:EmptyState IsVisible="{Binding !HasProjects}"
                    Label="Projects"
                    Title="{Binding EmptyTitle}"
                    Description="{Binding EmptyDescription}">
        <arc:EmptyState.Action>
            <StackPanel Orientation="Horizontal" Spacing="12">
                <arc:Button Content="New project" Variant="Primary"
                            Command="{Binding CreateProjectCommand}" />
                <arc:Button Content="Clear search" Variant="Ghost"
                            Command="{Binding ClearSearchCommand}" />
            </StackPanel>
        </arc:EmptyState.Action>
    </arc:EmptyState>
</Grid>
```

- `Title`, `Description` and `Icon` are one-way: the view model decides the copy and the icon, and the control never
  writes them back.
- Compute the copy in the view model so the wording for each state lives in one place and can be tested.
- Show `EmptyState` only after the load has returned zero items. While the request runs, show `Skeleton` in the list's
  place instead.
- `Action` takes an object, so bind the buttons' commands inside the action panel, as shown above.

### Changing state

Keep the control mounted and change its properties. The title and description roll to the new copy, and the height
springs to fit it. Set `Action` to `null` to remove the buttons. The `:has-action` pseudo class follows the action,
so a style can change the spacing when there is no button.

```csharp
empty.Icon = ArcIcons.Check;
empty.Title = "Project created";
empty.Description = "Invite your team or start adding files whenever you're ready.";
empty.Action = null;
```

Changing the properties of one mounted control is better than replacing the control, because the icon block and the
copy keep their place and the change reads as the same area updating.

### Layout and sizing

The control has no fixed width. It fills the space its parent gives it and centers its contents, so the same control
works in a narrow side panel and in a wide content area. Its copy is limited to 288 px, so a long description stays
in a readable measure on a wide page.

Put the control in the space the content would use. A table area, a list area and a grid area all work, provided the
surrounding surface has the same background and border as the content. The demos use a surface panel with a border
and a 26 px radius; use the same panel in your page so the empty state reads as part of it.

Keep the title short. It wraps and balances like the description, so a long title does not break the block, but a
sentence-length title belongs in the description.

### Example: empty projects list in a workspace

A projects page in an admin console for a team that has not created its first project. The surface matches the
list it stands in for, the folder icon says what the area holds, the title names the state, the description tells
the person what projects give them, and the one primary action moves them forward. Nothing on the page competes with
the button.

![An empty projects list with a folder icon, a title, a description and a primary action](images/ProjectsEmpty.png)

```xml
<Border Width="680"
        CornerRadius="26"
        BorderThickness="1"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}">
    <arc:EmptyState Label="Projects"
                    Title="No projects yet"
                    Description="Projects keep your files, people and conversations in one place.">
        <arc:EmptyState.Action>
            <arc:Button Content="New project" Variant="Primary" />
        </arc:EmptyState.Action>
    </arc:EmptyState>
</Border>
```

### Content guidelines

- **Title**: a short statement of the state in sentence case, without an exclamation mark. "No projects yet",
  "No results for 'invoice'", "All invoices are paid".
- **Description**: one or two sentences that say why the area is empty and what the person can do. Name the next step
  when there is one; say when the list will update when there is not. Write numbers as digits ("last 7 days").
- **Action**: a verb and an object, such as "New project", "Clear search" or "Browse all". Use one primary action;
  secondary actions are ghost or secondary buttons.
- **Tone**: calm and factual. Do not apologize, joke or celebrate with punctuation.

| Instead of | Write |
|---|---|
| Oops, nothing here! | No projects yet |
| Sorry, we found zero results. | No results for 'quarterly plan' |
| Empty | No deployments in the last 7 days |
| Submit | Create project |

### Best practices

**Do**

- Say why the area is empty, in the title or the first sentence of the description, so the person does not have to
  guess whether the page is broken.
- Offer a recovery action when the empty state comes from a filter or search, such as "Clear search", because it is
  the fastest way out of a dead end.
- Keep one icon and one primary action per state, so the block reads the same way every time it appears.
- Show the empty state only after the load has returned no data. Use `Skeleton` while the request runs.

**Avoid**

- Using an empty state for an error. An error needs the cause and a retry, which the empty state has no place for;
  use `Alert` above the content.
- Placing two equal primary buttons in the action row. The person has to choose between them with no guidance.
- Decorative illustrations on every screen. A single folder or search glyph keeps the block quiet, and repeated
  illustrations make the state feel like a marketing page.
- A generic "No projects yet" after a search. When a filter caused the empty result, say so, so the person knows to
  change the filter rather than create something.

### Common questions

**Why does the default icon look like a folder?** The default is `ArcIcons.Folder`, drawn at 24 px with a 1.5 stroke.
It suits the general case of a list with no items. Set `Icon` to a specific glyph for other states.

**How do I remove the button when the state no longer needs one?** Set `Action` to `null`. The action row is hidden and
the `:has-action` class is removed, so the block shrinks back to the copy.

**Does the control take keyboard focus?** No. `EmptyState` is not focusable, so Tab skips the block and goes to the
buttons in `Action`, if there are any.

**Why does the icon not animate on every update?** The icon block settles in once, the first time the control appears,
then stays. Changing the icon later pops only the new glyph, and setting the same icon again does nothing.

**Can I change the padding or the width?** The padding is set by the theme: 20 px horizontally and a vertical padding
between 32 px and 48 px that follows the window width. Set `Width` or `MaxWidth` on the control or on its parent to
constrain the block; the description already has its own 288 px measure.

## API reference

<!-- api: EmptyState -->

## Accessibility

- The control is a region landmark named by `Label`. Set `Label` on each empty state on a page, so a screen reader
  can announce which list is empty.
- The title is a level 3 heading. The description follows it in the reading order, and the action buttons come last.
- The icon is not exposed to assistive technology. The text carries the meaning, so the title must say the state
  without the icon.
- Buttons in `Action` keep their own focus and keyboard behavior.
- `EmptyState` takes no keyboard focus of its own. Tab moves past the block to the buttons in `Action`, in the order
  they appear, and Enter or Space presses the focused button.

## Motion

- A new title or description rises in from below with a soft blur, and the old text lifts away.
- The text block changes height on a smooth spring to fit the new copy.
- A new `Icon` pops in, and the icon block fades and scales in the first time it appears.
- Reduced motion fades the text and changes the height at once. The icon block does not animate in. Off applies every
  change at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Title or description changes | Text rises in with a soft blur, old text lifts away | Text fades | Immediate |
| Copy changes height | Springs to fit the new copy | Jumps to the new height | Immediate |
| Icon block first appears | Settles in from 92% scale and fades in over 480 ms | No entry animation | Immediate |

## Related

- `Skeleton` while the content is still loading.
- `Alert` for an error or a notice on a page that already shows content.
- `Card` for a grid item with media, an owner and actions.
- `EmptyStates` (block) for a tabbed set of ready-made empty-state scenes.

| Component | Choose it when |
|---|---|
| `Skeleton` | The content is loading and its shape is known |
| `Alert` | Something went wrong or needs attention on a page with content |
| `Toast` | A result is confirmed and nothing on the page needs replacing |
| `Card` | The item is a tile with media, an owner and actions |
| `EmptyStates` block | You want a tabbed illustration with several scenes |
