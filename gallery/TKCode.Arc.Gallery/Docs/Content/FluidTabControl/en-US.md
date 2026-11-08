---
title: Fluid tab control
summary: Tabs beside one scroll area that holds every tab's content in order. Picking a tab scrolls to its section, and scrolling selects the tab of the section in view.
---

<Demo name="Default" />

## When to use

`FluidTabControl` puts a list of tabs beside one scroll area, and the scroll area holds the content of every tab, one
section after another. Picking a tab scrolls smoothly to its section; scrolling the content selects the tab of the
section that reaches the top (or the middle) of the scroll area. Nothing is replaced, so the person can read straight
through and still jump between sections. This pattern is also called scroll-linked tabs or a scroll spy.

- **A settings page**: General, Appearance, Account, Notifications, Privacy and Advanced. A person who looks for one
  switch picks its tab; a person who checks everything scrolls once from top to bottom.
- **A long form split into chapters**: a customer application with Company, Contacts, Billing and Documents. The tab
  list shows how far the person is, and every field stays on one page, so nothing is lost when they move between parts.
- **A document or handbook**: chapters with a table of contents that follows the reading position.
- **A property editor**: the properties of a selected object grouped into Layout, Appearance and Behavior, with the
  groups one after another instead of behind separate tabs.

## When not to use

- **Views that should not be read together**, such as Overview, Activity and Settings of a record: use `Tabs`, which
  shows one panel at a time and keeps the others out of the way.
- **A few sections that the person opens one or several at a time**: use `Accordion`.
- **Steps that must be completed in order**: use `Stepper`. A person can jump to any section of a fluid tab control.
- **A long list of the same kind of item**, such as messages or search results: use a list with its own scrolling.
  Every section of a fluid tab control is created up front, so it suits a few dozen sections, not thousands of rows.
- **Navigation between pages of an app**: use a sidebar. A fluid tab control moves within one page.

## Usage

Items are `FluidTabItem` containers. `Header` (and an optional `Icon`) is the tab, and `Content` is the tab's section.
Give the control a height, or put it where its height is bounded (a grid row, a dock panel's fill): the sections
scroll inside it.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:FluidTabControl Height="360" AutomationProperties.Name="Settings">
        <arc:FluidTabItem Header="General">
            <StackPanel Spacing="12">
                <TextBlock Text="General" FontSize="18" FontWeight="SemiBold" />
                <arc:Switch Content="Open at login" IsChecked="True" />
                <arc:Switch Content="Check for updates automatically" />
            </StackPanel>
        </arc:FluidTabItem>
        <arc:FluidTabItem Header="Appearance">
            <StackPanel Spacing="12">
                <TextBlock Text="Appearance" FontSize="18" FontWeight="SemiBold" />
                <arc:Switch Content="Follow the system theme" IsChecked="True" />
            </StackPanel>
        </arc:FluidTabItem>
        <arc:FluidTabItem Header="Account">
            <arc:Input Label="Email" Text="noor@larkspur.co" />
        </arc:FluidTabItem>
    </arc:FluidTabControl>
</UserControl>
```

`FluidTabControl` derives from Avalonia's `SelectingItemsControl`, so `SelectedIndex`, `SelectedItem` and
`SelectionChanged` work as for any selecting list, and both properties bind two ways. The first tab is selected when the
control loads. Selecting a tab in any way (a click, the keyboard, a binding, code) scrolls its section into place, and
scrolling the content changes the selection without scrolling it back.

A `FluidTabItem` derives from `HeaderedContentControl`. The tab list shows its `Header` and `Icon`; its `Content` is
shown in the scroll area, so a control in it keeps its state while the person scrolls away and back. A disabled tab is
skipped by the arrow keys and cannot be selected; a hidden tab (`IsVisible="False"`) hides its section too.

### Anatomy

1. **Tab list**: 200 px wide (`TabListWidth`) with 8 px of padding, and a 1 px divider in the subtle border color on the
   side of the sections. It scrolls on its own when the tabs do not fit.
2. **Tab** (`FluidTabItem`): at least 36 px high, with 14 px by 8 px of padding and a 12 px radius. The label uses the
   small type size in medium weight and the muted text color. Hover fills the tab with the muted surface; the selected
   tab uses the strong accent color, and a disabled tab drops to 50% opacity.
3. **Icon**: before the header, 10 px from it. Any content works: a glyph, text, or a template's result.
4. **Selection highlight**: the subtle accent fill behind the selected tab. It glides between tabs.
5. **Side indicator**: a 3 px accent bar on the outer edge of the selected tab, inset 9 px from its top and bottom. It
   moves to the right edge when the list is on the right.
6. **Scroll area**: every section in item order, 40 px apart (`SectionSpacing`), with 24 px of padding
   (`ContentPadding`).
7. **Focus ring**: a 2 px ring around the focused tab.

### Placement and anchor

`TabPlacement` puts the tab list on the `Left` (the default) or the `Right`. `SnapAlignment` decides which line of the
scroll area counts: with `Top` (the default) a selected section scrolls to the top and the section at the top is the
current one; with `Center` a selected section scrolls to the middle and the section across the middle is the current
one. Both can change at any time without losing the selection or the scroll position.

<Demo name="Placement" />

Use `Top` for settings, forms and documents, where each section starts with a heading that the person reads first. Use
`Center` for sections that are shorter than the scroll area and read as a whole, such as cards or steps, so the
current one sits in the middle with its neighbours around it.

At the ends the rule is simple: scrolled to the very top, the first tab is selected; scrolled to the very bottom, the
last tab is selected, even when the last sections are too short to reach the anchor line. A section that cannot reach
the anchor line scrolls as far as it can, and its tab stays selected.

### Data-bound tabs

Set `ItemsSource`. `ItemTemplate` renders the tab, `IconTemplate` the tab's icon and `ContentTemplate` the section; all
three receive the same data item, so the tab and its section show the same object. Bind `SelectedItem` (or
`SelectedIndex`) two ways to keep the current section in a view model.

<Demo name="DataBound" />

```csharp
public sealed record HandbookChapter(string Number, string Title, string Subtitle, string Body);

public partial class HandbookViewModel : ObservableObject
{
    public ObservableCollection<HandbookChapter> Chapters { get; } = new();

    [ObservableProperty]
    private HandbookChapter? _currentChapter;
}
```

```xml
<arc:FluidTabControl ItemsSource="{Binding Chapters}"
                     SelectedItem="{Binding CurrentChapter, Mode=TwoWay}"
                     AutomationProperties.Name="Handbook chapters">
    <arc:FluidTabControl.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Title}" />
        </DataTemplate>
    </arc:FluidTabControl.ItemTemplate>
    <arc:FluidTabControl.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Body}" TextWrapping="Wrap" />
        </DataTemplate>
    </arc:FluidTabControl.ContentTemplate>
</arc:FluidTabControl>
```

Templates are typed (`x:DataType`) and use compiled bindings, so data-bound tabs work with trimming and Native AOT;
there is no member path that would be read by reflection. A data item that is a `FluidTabItem` is used as is.

### Smooth scrolling

Selecting a tab scrolls to its section over `AnimationDuration` (480 ms by default) with `EasingFunction` (Arc's
in-out curve by default). A duration of zero, `IsAnimationEnabled="False"` or reduced motion jumps straight to the
section. The demo above uses `AnimationDuration="0:0:0.6"` and `EasingFunction="CubicEaseOut"`.

While the scroll runs, the tabs it passes are not selected, so the selection goes straight to the tab that was picked.
Picking another tab during the scroll continues from the current position to the new section. Scrolling with the wheel,
the touchpad, the scroll bar or the keyboard stops the smooth scroll at once and hands control back to the person. When
a section changes height during the scroll (an image loads, a group expands), the scroll follows its new position.

```xml
<arc:FluidTabControl AnimationDuration="0:0:0.3"
                     EasingFunction="QuadraticEaseOut"
                     IsAnimationEnabled="True" />
```

### Adding and removing tabs

The sections always match the items. Add, remove, replace or move items in an `ObservableCollection`, or in `Items`,
and the scroll area follows; clearing the items clears the sections. When the selected tab is removed, another tab is
selected and its section is scrolled into place. Changing a `FluidTabItem`'s `Content` replaces its section, and hiding
the item hides its section.

<Demo name="DynamicItems" />

### Selecting in code

```csharp
settings.SelectTab(3);
settings.SelectionChanged += (_, _) =>
{
    status.Text = $"Reading: {settings.SelectedItem}";
};
```

`SelectTab` selects the tab at the index when it exists and is enabled, and returns `false` otherwise. Calling it for the
tab that is already selected scrolls its section back into place, which is useful after the person scrolled a little
away from it. Setting `SelectedIndex` or `SelectedItem` scrolls the same way, but does not check whether the tab is
enabled.

### Custom tabs and icons

`Header` and `Icon` take any content. Use `HeaderTemplate` and `IconTemplate` on a `FluidTabItem`, or `ItemTemplate`
and `IconTemplate` on the control for data items. A tab with an icon gets the `:has-icon` pseudo class; the control sets
`:left` or `:right` after its placement, so a style can change the tabs for each side.

```xml
<arc:FluidTabItem Header="Billing">
    <arc:FluidTabItem.Icon>
        <arc:ArcGlyph Data="{x:Static arc:ArcIcons.CreditCard}" Size="16" StrokeThickness="1.8" />
    </arc:FluidTabItem.Icon>
    <arc:FluidTabItem.HeaderTemplate>
        <DataTemplate>
            <StackPanel Spacing="2">
                <TextBlock Text="{Binding}" />
                <TextBlock Text="Plan and invoices" FontSize="12" FontWeight="Normal" />
            </StackPanel>
        </DataTemplate>
    </arc:FluidTabItem.HeaderTemplate>
    <TextBlock Text="Business plan, renews November 30, 2026." />
</arc:FluidTabItem>
```

### Data shape and performance

Every section is created when the control loads and stays alive, which is what lets the person scroll through all of
them. That suits settings pages and documents with up to a few dozen sections. For a long list of items in one section,
put a virtualized list inside the section with its own height, or use a list control instead.

The control reads the section positions only when it needs them (a selection, a scroll, a layout change during a smooth
scroll) and does not subscribe to layout events otherwise. Section heights can be anything and can change at any time.

### Example: account settings

![A settings page with a tab list on the left and the General and Appearance sections stacked on the right](images/AccountSettings.png)

```xml
<Border Width="720"
        Height="340"
        BorderThickness="1"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
        Background="{arc:ArcDesignTokenResource Surface}"
        ClipToBounds="True">
    <arc:FluidTabControl AutomationProperties.Name="Settings">
        <arc:FluidTabItem Header="General">
            <arc:FluidTabItem.Icon>
                <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Settings}" Size="16" StrokeThickness="1.8" />
            </arc:FluidTabItem.Icon>
            <StackPanel Spacing="12">
                <TextBlock Text="General" FontSize="18" FontWeight="SemiBold" />
                <TextBlock Text="Startup, language and updates." FontSize="14"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                <arc:Switch Content="Open at login" IsChecked="True" />
                <arc:Switch Content="Check for updates automatically" IsChecked="True" />
            </StackPanel>
        </arc:FluidTabItem>
        <arc:FluidTabItem Header="Appearance">
            <arc:FluidTabItem.Icon>
                <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Palette}" Size="16" StrokeThickness="1.8" />
            </arc:FluidTabItem.Icon>
            <StackPanel Spacing="12">
                <TextBlock Text="Appearance" FontSize="18" FontWeight="SemiBold" />
                <TextBlock Text="Theme, density and motion." FontSize="14"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                <arc:Switch Content="Follow the system theme" IsChecked="True" />
                <arc:Switch Content="Compact rows" />
            </StackPanel>
        </arc:FluidTabItem>
        <arc:FluidTabItem Header="Account">
            <arc:FluidTabItem.Icon>
                <arc:ArcGlyph Data="{x:Static arc:ArcIcons.User}" Size="16" StrokeThickness="1.8" />
            </arc:FluidTabItem.Icon>
            <arc:Input Label="Email" Text="noor@larkspur.co" />
        </arc:FluidTabItem>
        <arc:FluidTabItem Header="Privacy">
            <arc:FluidTabItem.Icon>
                <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Shield}" Size="16" StrokeThickness="1.8" />
            </arc:FluidTabItem.Icon>
            <arc:Switch Content="Send crash reports" IsChecked="True" />
        </arc:FluidTabItem>
    </arc:FluidTabControl>
</Border>
```

### Content guidelines

- **Tab label**: one or two words, a noun in sentence case that names the section: "Notifications", "Billing".
- **Section heading**: start each section with a heading that repeats the tab label, so the person who scrolled there
  knows where they are without looking at the list.
- **Order**: put the sections people visit most first, and the dangerous ones (deleting an account) last.

| Instead of | Write |
|---|---|
| Tab label "Misc" | Advanced |
| Tab label "NOTIFICATIONS" | Notifications |
| Section heading "Settings group 3" | Privacy |

### Best practices

**Do**

- Give every section a heading that matches its tab label.
- Bound the control's height, so that the sections scroll inside it rather than the whole page growing.
- Name the tab list with `AutomationProperties.Name`.
- Keep the number of tabs small enough to scan, about four to ten.

**Avoid**

- Nesting another scroll area of the same direction inside a section without a height of its own; the inner one takes
  the wheel and the selection stops following.
- Using it for views that should not be read together; use `Tabs`.
- Very short sections at the end that can never reach the anchor line; when you have several, prefer `Center`, or add
  bottom padding with `ContentPadding`.

### Common questions

**Why does the last tab get selected before its section reaches the top?** Scrolled to the very bottom, the last tab is
always selected, because a short last section can never reach the top line.

**Can the person scroll while the smooth scroll runs?** Yes. Any wheel, touchpad, scroll bar or key input in the scroll
area stops the smooth scroll and the selection follows the person's scrolling again.

**How do I keep the scroll from jumping when a selection comes from a view model?** It does not jump; it scrolls smoothly
like a click. Set `IsAnimationEnabled="False"` if you want it to jump.

## API reference

<!-- api: FluidTabControl -->

<!-- api: FluidTabItem -->

## Accessibility

- The tab list has the Tab role and each `FluidTabItem` the TabItem role with its selection state. Name the control with
  `AutomationProperties.Name`.
- Each tab is named by its header when the header is a string. Give other headers a name explicitly.
- Only the selected tab is in the tab order. Tab moves from the tab list into the sections, where every control is
  reachable in order.
- Keys pressed inside a section belong to the controls there; the arrow keys change tabs only while a tab has focus.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move into the tab list on the selected tab; out of the list into the sections |
| `Up` / `Down` | Select the previous or next enabled tab and scroll to its section; they stop at the ends |
| `Home` / `End` | Select the first or last enabled tab and scroll to its section |
| `Enter` / `Space` | Scroll to the focused tab's section again |
| Pointer press | Select the tab on press with the left button; touch selects on release |

## Motion

- Selecting a tab scrolls to its section over `AnimationDuration` with `EasingFunction`.
- The selection highlight and the side indicator glide to the selected tab on the morph spring.
- The tab list scrolls the selected tab into view on the smooth spring.
- Reduced motion jumps to the section and moves the highlight at once; Off does the same.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A tab is selected | The content scrolls to its section over 480 ms, in-out | Jumps | Jumps |
| The selection moves | The highlight and indicator glide on the morph spring | Move at once | Move at once |
| The selected tab is outside the list | The list scrolls on the smooth spring | Jumps | Jumps |
| Hover and selected tab | The label and fill colors crossfade | Immediate | Immediate |
| The person scrolls | The selection follows at once, with no animation | Same | Same |

## Responsive behavior

- `FluidTabControl` has no breakpoint. The tab list keeps its `TabListWidth` and the sections take the rest of the
  width, so on a narrow screen lower `TabListWidth` or hide the icons.
- On a phone-sized view, give the tab list about 120 px, or switch to `Tabs` above the content, which needs no side
  column.
- The tab list scrolls on its own when the tabs are taller than the control, and keeps the selected tab in view.
- Tabs are at least 36 px tall. A tap selects a tab as a click does, and a drag on the tab list scrolls it.

## Related

- `Tabs` for peer views shown one at a time.
- `Accordion` for sections that open in place.

| Component | Choose it when |
|---|---|
| `Tabs` | Each view replaces the others, and reading them together makes no sense |
| `Accordion` | The person opens one or a few sections at a time |
| `Stepper` | Sections must be completed in order |
| `ScrollArea` | You need the scroll area alone, without a tab list |
