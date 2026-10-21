---
title: Tabs
summary: Switch between related content in the same context.
---

<Demo name="Default" />

## When to use

`Tabs` splits one object's screen into peer views, such as Overview, Activity and Settings on a customer record. The
person picks one view at a time, and each view has its own panel below the tab list. The tab list stays in place while
the panel changes, so the person keeps their bearings within the same record, order or workspace.

- **A customer record in a CRM**: Overview, Orders, Invoices and Notes are views of one account. The account header
  stays above the tabs, and the selected tab decides which list fills the panel.
- **A workspace or project page**: Overview, Activity and Settings. Each view is peer to the others, so none of them is
  a step in a sequence.
- **Filtering the same data by a state**: All, Open, Overdue and Paid tabs over one invoice list, where each tab shows
  its count in the label.
- **Long lists of views in a desktop tool**: a mail client's folders (Inbox, Drafts, Sent, Scheduled) with edge scroll
  buttons when the window is narrow. The selected folder is always scrolled into view.

## When not to use

- **Choosing a compact value without a panel**, such as a day, week or month range: use `SegmentedControl`. The
  options have no content of their own, so a tab panel adds nothing.
- **Content that is read in order as stacked sections**, such as the terms of a checkout: use `Accordion`, where each
  section opens in place and the person can read several at once.
- **Moving through pages of results**: use `Pagination`. Tabs switch views, not pages of one collection.
- **Showing where a page sits in a hierarchy**: use `Breadcrumb`. Crumbs lead up a path, while tabs are peers.
- **Steps that must be completed in order**, such as Shipping, Payment and Review: use `Stepper`. A person cannot skip
  ahead in a checkout the way they can switch tabs.

## Usage

Items are `TabItem` containers. The header is the trigger and the content is the panel.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Tabs AutomationProperties.Name="Workspace">
        <arc:TabItem Header="Overview">
            <TextBlock Text="Revenue is up 12% this quarter." />
        </arc:TabItem>
        <arc:TabItem Header="Activity">
            <TextBlock Text="Maya merged Billing v2 two hours ago." />
        </arc:TabItem>
        <arc:TabItem Header="Settings">
            <CheckBox Content="Email digests" IsChecked="True" />
        </arc:TabItem>
        <arc:TabItem Header="Archived" IsEnabled="False" />
    </arc:Tabs>
</UserControl>
```

`Tabs` derives from Avalonia's `SelectingItemsControl`, so selection works as it does for any selecting list:
`SelectedIndex`, `SelectedItem` and `SelectionChanged` are the same, and the first tab is selected when the control
loads. The tab list itself takes no focus; each `TabItem` does. A `TabItem` derives from `HeaderedContentControl`, so
`Header` is its trigger and `Content` is its panel. A disabled tab is skipped by the arrow keys and cannot be selected.

### Anatomy

1. **Tab list** (`Tabs`): a 1 px border in the border color on the muted surface, with the control radius. It clips its
   contents and has 4 px of padding inside the frame.
2. **Selection pill**: the surface color with a 1 px border and the resting shadow. It sits behind the selected trigger
   and glides between triggers.
3. **Trigger** (`TabItem.Header`): at least 88 px wide and 36 px high, with 12 px of horizontal padding and a 15 px
   radius. Its label uses the small type size in medium weight and the muted text color. Hover moves it to the main
   text color, the selected tab uses the strong accent color, and a disabled tab drops to 50% opacity.
4. **Gap**: 4 px between triggers.
5. **Edge scroll buttons** (`ScrollLeftLabel`, `ScrollRightLabel`): 33 px wide, shown only when the list is wider than
   the frame, with a 17 px chevron. The button on a side is enabled only when there are tabs hidden on that side.
6. **Edge fade**: a 35 px fade on each side that has hidden tabs.
7. **Panel**: the content of the selected tab, 16 px below the list, in the secondary text color at the small type size
   with a 19.6 px line height.
8. **Focus rings**: a 2 px ring around the focused trigger, and a ring around the panel when the panel takes focus.

### Data-bound tabs

Set `ItemsSource`. `ItemTemplate` renders the trigger and `ContentTemplate` renders the panel of the selected item.
Read the selection through `SelectedItem` or `SelectionChanged`.

<Demo name="Overflow" />

```xml
<arc:Tabs ItemsSource="{Binding Folders}"
          SelectedItem="{Binding CurrentFolder}"
          MaxWidth="460"
          HorizontalAlignment="Left">
    <arc:Tabs.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Name}" />
        </DataTemplate>
    </arc:Tabs.ItemTemplate>
    <arc:Tabs.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Summary}" />
        </DataTemplate>
    </arc:Tabs.ContentTemplate>
</arc:Tabs>
```

A data item that is not a `TabItem` becomes the panel content directly, and `ContentTemplate` renders it.

### Binding to a view model

A customer record keeps its sections in a view model. The tabs show the sections, the selected section is a two-way
property, and the view model loads the data for the section that the person opens. With CommunityToolkit.Mvvm, the
view model is an `ObservableObject` with an `ObservableCollection<T>` of sections and an `[ObservableProperty]`
for the selection.

```csharp
public sealed record CustomerSection(string Title, string Summary);

public partial class CustomerViewModel : ObservableObject
{
    public ObservableCollection<CustomerSection> Sections { get; } = new()
    {
        new("Overview", "Account owner Maya Chen. Business plan, renews November 30, 2026."),
        new("Orders", "12 orders in the last 90 days. The latest was placed April 8, 2026."),
        new("Invoices", "2 open invoices. One is 6 days overdue."),
        new("Notes", "Asked for a quote for 40 more seats on March 2, 2026.")
    };

    [ObservableProperty]
    private CustomerSection? _selectedSection;

    partial void OnSelectedSectionChanged(CustomerSection? value)
    {
        // Load the data for the section the person opened, not for every section up front.
    }
}
```

```xml
<arc:Tabs ItemsSource="{Binding Sections}"
          SelectedItem="{Binding SelectedSection, Mode=TwoWay}"
          AutomationProperties.Name="Customer sections">
    <arc:Tabs.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Title}" />
        </DataTemplate>
    </arc:Tabs.ItemTemplate>
    <arc:Tabs.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Summary}" TextWrapping="Wrap" />
        </DataTemplate>
    </arc:Tabs.ContentTemplate>
</arc:Tabs>
```

`SelectedItem` is the value to bind two ways: it is the section the person chose, and a change from the view model
(for example, opening the Invoices section from a notification) selects the tab. Use `SelectionChanged` in code behind
when the view must react to the change itself, such as to save the scroll position of the previous section. Do not
bind `SelectedIndex` for the same purpose: an index shifts when a section is added or removed, while the item stays
the same.

### Activation mode

`ActivationMode` controls what the arrow keys do. `Automatic` (the default) selects the tab that
receives focus. `Manual` only moves focus; Enter or Space selects the tab.

<Demo name="ManualActivation" />

Use `Automatic` when the panels are cheap to show and the person is browsing: moving through the tabs with the arrow keys
previews each panel. Use `Manual` when a panel is expensive to show, when the person would lose work by previewing a
panel they did not mean to open, or when the tabs filter a list that reloads on every selection. Manual mode also
keeps the arrow keys from changing the data while the person is still deciding.

### Selecting in code

```csharp
tabs.SelectTab(2);
tabs.SelectionChanged += (_, _) =>
{
    status.Text = $"Selected: {tabs.SelectedItem}";
};
```

`SelectTab` selects the tab at the index when it exists and is enabled, and returns `false` otherwise.
`SelectedIndex` does not check whether the tab is enabled, so use `SelectTab` when the target might be disabled, such
as a tab that a permission check turns off.

### Long tab lists

A tab list wider than its frame scrolls horizontally. The edge buttons appear only while there is something to scroll
to, and the list reveals the selected or focused tab on the smooth spring. Clicking an edge button moves the list by
about three quarters of its visible width. The list keeps the selected tab
visible when the selection changes from code, so a tab deep in a long list is found without a search.

Give tabs short labels that fit the list at the narrowest width the page supports, and move the rest of the
information into the panel. A list of ten folders is usable with edge buttons; a list of fifteen with long labels is
better as a `Select` or a sidebar, because the person then has to scroll for every choice.

### Data shape and performance

Only the selected tab's content is shown. When the selection changes, the panel host replaces the content it shows and
keeps the leaving panel for the length of its fade. Put heavy loading behind the selection, for example in the view
model's selection handler, rather than loading every section when the page opens. Keep the tab count small: a tab list
of about five to seven is easy to scan, and a longer list needs a different structure.

### Example: customer record tabs

A customer record in a CRM. The account name and its plan stay above the tabs, so the context does not change when the
person moves between Overview, Orders, Invoices and Notes. The Overview panel is a short list of facts in two columns,
with the label in the muted color and the value in the main text color. The tab labels are nouns that match the lists
they open, and the first tab is selected because it is the view people open most.

![A customer record header with four tabs and the Overview facts in the panel](images/CustomerTabs.png)

```xml
<StackPanel Width="680" Spacing="16">
    <StackPanel Spacing="4">
        <TextBlock Text="Northwind Trading"
                   FontSize="22"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Business plan, renews November 30, 2026"
                   FontSize="14"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
    <arc:Tabs AutomationProperties.Name="Customer sections">
        <arc:TabItem Header="Overview">
            <StackPanel Spacing="12">
                <Grid ColumnDefinitions="180,*">
                    <TextBlock Text="Account owner" FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Grid.Column="1" Text="Maya Chen" FontSize="14" />
                </Grid>
                <Grid ColumnDefinitions="180,*">
                    <TextBlock Text="Plan" FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Grid.Column="1" Text="Business, billed annually" FontSize="14" />
                </Grid>
                <Grid ColumnDefinitions="180,*">
                    <TextBlock Text="Renewal date" FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Grid.Column="1" Text="November 30, 2026" FontSize="14" />
                </Grid>
                <Grid ColumnDefinitions="180,*">
                    <TextBlock Text="Open invoices" FontSize="14"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                    <TextBlock Grid.Column="1" Text="2, one 6 days overdue" FontSize="14" />
                </Grid>
            </StackPanel>
        </arc:TabItem>
        <arc:TabItem Header="Orders">
            <TextBlock Text="12 orders in the last 90 days." FontSize="14" />
        </arc:TabItem>
        <arc:TabItem Header="Invoices">
            <TextBlock Text="inv_2026_0412 and inv_2026_0399 are open." FontSize="14" />
        </arc:TabItem>
        <arc:TabItem Header="Notes">
            <TextBlock Text="Asked for a quote for 40 more seats on March 2, 2026." FontSize="14" />
        </arc:TabItem>
    </arc:Tabs>
</StackPanel>
```

### Content guidelines

- **Tab label**: one or two words, a noun in sentence case that names the list it opens: "Invoices", "Notes". Keep the
  labels parallel, so that the list reads as one set of views.
- **Count in a label**: add a count only when the count changes what the person does next, such as "Open (2)". A count
  that never changes adds noise to every label.
- **Panel text**: start with the state or the number ("2 open invoices", "No notes yet"), not with an instruction to
  look at a tab.
- **Disabled tab**: when a tab is disabled, say why nearby, for example in the panel of the tab next to it or in the
  page header, so that the person knows whether the view is missing or unavailable to them.

| Instead of | Write |
|---|---|
| Tab label "Misc" | Notes |
| Tab label "Stuff about the customer" | Account |
| Panel text "Click the tab above for more" | No open invoices. |
| Tab label "INVOICES" | Invoices |

### Best practices

**Do**

- Keep the labels short and the list within the narrowest width the page supports. Labels hidden by scrolling are harder
  to find than labels that are visible at once.
- Keep the shared context (the record name, its status, the primary action) above the tabs, so it stays in view while
  the person switches views.
- Bind `SelectedItem` two ways and name the list with `AutomationProperties.Name`. The view model keeps the selection,
  and assistive technology announces the list by its name.
- Use `Manual` activation when a panel is expensive to show, so that arrow keys move focus without loading each view.

**Avoid**

- Using tabs for steps. People expect tabs to be free to visit in any order, and a step that must come first needs a
  `Stepper` or a sequence of pages.
- Nesting tabs inside tabs. Two levels of peer selection make the person lose track of which level they are on, and the
  inner list scrolls inside the outer one.
- Putting the page's primary action in one tab only. The reason the person came to the record is usually visible in
  every view, not hidden in one of them.
- Disabling a tab without a reason on the page. A disabled tab with no explanation looks broken.

### Common questions

**Why does selecting a tab not scroll my page?** The tab list reveals the selected tab inside its own viewport and does
not bring the whole control into view. A tab set below the fold stays where the person scrolled, so the page does not
jump when a tab is chosen.

**How do I open a particular tab when the page loads?** Set `SelectedIndex` or `SelectedItem` before or after the
control loads. If the tab could be disabled, call `SelectTab(index)` instead, which returns `false` for a disabled or
missing tab so that you can fall back to the first enabled one.

**Why do the arrow keys not select a tab in Manual mode?** In `Manual` mode the arrow keys only move focus. Press Enter or
Space on the focused tab to select it.

**Can I style the tab list for overflow?** Yes. `Tabs` sets the `:overflow` pseudo class when the list is wider than its
frame, and `:scrolled-start` and `:scrolled-end` when tabs are hidden on each side. Use them in a style selector to
change the edge treatment without changing the control's template.

## API reference

<!-- api: Tabs -->

## Accessibility

- The list has the Tab role and each `TabItem` exposes its selection state. Give the list a name with
  `AutomationProperties.Name`.
- Each `TabItem` is named by its header when the header is a string. Give other headers a name explicitly.
- Only the selected tab is in the tab order. Arrow keys move between tabs, Home and End jump to the first and last.
- Tab moves from the list into the panel of the selected tab.
- Scroll buttons on an overflowing list are named by `ScrollLeftLabel` and `ScrollRightLabel`, which default to
  "Scroll tabs left" and "Scroll tabs right".
- The panel of a tab that is leaving is hidden from assistive technology and cannot take focus while it fades.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move into the tab list on the selected tab, or on the last tab that had focus; out of the list into the panel |
| `Left` / `Right` | Move to the previous or next enabled tab, wrapping at the ends; in `Automatic` mode the tab is also selected |
| `Home` / `End` | Move to the first or last enabled tab; in `Automatic` mode the tab is also selected |
| `Enter` / `Space` | Select the focused tab. In `Manual` mode this is how the keyboard selects; a pointer press selects in both modes. |
| Pointer press | Select the tab on press with the left button; touch selects on release |

A disabled tab is skipped by the arrow keys, Home and End, and it cannot be selected by a press.

## Motion

- The selection pill glides between triggers on a morph spring.
- The new panel slides in 8px from the side of travel and fades in. The old panel stays in place, moves 6px back
  and fades out quickly.
- The panel area springs from the old panel height to the new one, so the content below moves with it.
- Reduced motion switches the pill at once and cross-fades the panels in place without height animation. Off
  switches everything at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The selection moves | The pill glides between the triggers on the morph spring | The pill switches at once | Immediate |
| The new panel appears | Slides in 8 px from the side of travel and fades in over the standard duration | Cross-fades in place over 120 ms | Immediate |
| The old panel leaves | Stays in place, moves 6 px back and fades out over 120 ms | Fades out over 100 ms | Immediate |
| The panel area changes height | Springs from the old height to the new one, moving the content below | No height animation | Immediate |
| The list scrolls to a tab | Scrolls on the smooth spring | Jumps | Jumps |
| Hover and selected trigger | The label color crossfades | Immediate | Immediate |

## Related

- `SegmentedControl` for a compact choice of a value that has no panel.
- `Accordion` for stacked sections that are read in order.
- `Pagination` for moving through pages of one collection.

| Component | Choose it when |
|---|---|
| `SegmentedControl` | The choice is a value, such as a range or a view mode, with no panel of its own |
| `Accordion` | Stacked sections are read in order, and several may be open at once |
| `Breadcrumb` | The page sits in a hierarchy and the person needs the path back up |
| `Pagination` | The views are pages of one collection, not peer views |
| `Stepper` | The person completes steps in a fixed order |
| `Select` | The list of views is long, and a single choice in a form is enough |
