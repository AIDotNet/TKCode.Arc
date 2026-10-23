---
title: Morph select
summary: A select whose trigger grows into the list, with a gliding highlight and type-ahead.
---

<Demo name="GroupedWithSearch" />

## When to use

`MorphSelect` is a single-value picker for a known list that lives in a form or a property panel. The closed trigger is
a pill that is as wide as its label, and opening it grows the same shape into the option list, so the person sees the
list come out of the value they are changing. It fits places where the choice matters and the options are known:
settings screens, detail panels and toolbars, where a plain drop-down looks too heavy.

- **Deployment settings in a developer console**: a release picks its environment (Production, Staging, Preview) and its
  region from groups of cloud regions. Grouped headings such as "Americas" and "Europe" let people find a region without
  reading the whole list.
- **Property panels in a project tracker**: a task's status, priority and assignee are picked from short known lists.
  The compact pills sit in a row above the description, and each one opens in place.
- **Owner and team pickers in a CRM detail panel**: an account owner is chosen from the sales team. With more than eight
  options the search field appears on its own, so a long team list stays usable.
- **Compact status and priority toolbars**: a board or a table toolbar shows the current filter as a pill with an icon.
  Hiding the label keeps the toolbar short, and the trigger keeps the label as its accessible name.
- **Fintech and billing settings**: an invoice currency, a billing cycle or a tax region is a known list with a short meta
  value per option, such as a rate or an offset, shown beside the name in the list.

## When not to use

- **Free text or a long list that comes from a remote source**: use `Combobox`, which searches as the person types and
  keeps the typed text as the query.
- **Choosing several values**: use `MultiSelect`, which keeps each chosen value as a token and stays open between picks.
- **Running actions rather than choosing a value**: use `DropdownMenu`, whose items perform commands such as Rename or
  Archive.
- **Three or four options that all fit on screen**: use `SegmentedControl` or `ChipGroup`, where every option is visible
  and needs no opening step.
- **A plain labelled field in a dense form where a closed list is enough**: use `Select`, which is the classic field
  with a fixed list.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MorphSelect Label="Status" SelectedIndex="1">
        <arc:SelectItem Content="Backlog" />
        <arc:SelectItem Content="In progress" />
        <arc:SelectItem Content="In review" />
        <arc:SelectItem Content="Done" />
        <arc:SelectItem Content="Canceled" IsEnabled="False" />
    </arc:MorphSelect>
</UserControl>
```

`MorphSelect` derives from Avalonia's `SelectingItemsControl`, so `ItemsSource`, `Items`, `SelectedItem`, `SelectedIndex`,
`SelectedValue`, `Selection` and `SelectionChanged` behave as they do in a list box. The control adds the trigger, the
morphing surface, the label flight, search and the grouped listbox model. `SelectItem` supplies each option, and
`SelectOption` does the same in code.

### Anatomy

1. **Label** (`Label`): the small type size at medium weight in the secondary text color, 8 px above the trigger. It is
   the accessible name of the trigger and of its list, whether or not it is drawn.
2. **Trigger**: a pill 44 px high with a 1 px border, 14 px of padding before the value and 36 px kept for the chevron.
   Its corner radius is 22 px, so the ends are fully round. The width follows the selected label, plus 50 px for the
   padding and the chevron, and it is capped at 320 px.
3. **Value or placeholder**: the selected label in the foreground color. While nothing is selected, `Placeholder` shows
   in the muted color; its default is "Select".
4. **Chevron**: 12 px from the trigger's right edge.
5. **Surface** (the open state): the trigger grows into a panel `PanelWidth` wide (272 px by default, never narrower
   than the trigger). The panel has a 6 px inset around the list, and the list is at most `MaxListHeight` tall (296 px by
   default), scrolling beyond it.
6. **Option rows**: 36 px high. A group heading (`Group`) is inserted above each run of options that share a group, and
   each heading is 28 px high.
7. **Highlight**: a soft tint of the foreground at 6.5%, which glides between options as the person moves through
   the list.
8. **Search field** (when search is on): replaces the value inside the open trigger. Its clear button uses a foreground
   tint at 8%.
9. **Focus ring**: a 2 px ring 4 px outside the trigger, drawn while the trigger has focus.

### Choosing the selection mode and width

The trigger is a single choice, and its width comes from the label, so a long label makes a wide pill. Set `PanelWidth`
when the option text is long and the list should be wider than the default 272 px. Set `PanelAlignment` to `End` when
the trigger sits at the right edge of a toolbar, so the panel opens toward the empty space and not off the window.

```xml
<arc:MorphSelect Label="Priority" PanelAlignment="End" PanelWidth="220">
    <arc:SelectItem Content="Urgent" />
    <arc:SelectItem Content="High" />
    <arc:SelectItem Content="Medium" />
    <arc:SelectItem Content="Low" />
    <arc:SelectItem Content="No priority" />
</arc:MorphSelect>
```

### Groups, icons and meta

Set `Group`, `Icon` and `Meta` on each option. A group title is inserted above each run of options that share a
group, so keep the options of one group together in the source. `Icon` takes a Lucide geometry or an image. `Meta` is a
short trailing detail, such as an offset, a rate or a count, shown in a monospaced figure style.

```csharp
var zone = new MorphSelect
{
    Label = "Time zone",
    SelectedValue = "London",
    ItemsSource = cities.Select(c => new SelectOption(c.City, c.City)
    {
        Group = c.Region,
        Meta = c.Utc,
        Keywords = c.Zone,
        Icon = ArcIcons.Globe
    }).ToArray()
};
zone.SelectionChanged += (_, _) => Console.WriteLine(zone.SelectedValue);
```

Use a group when the options fall into families a person already thinks in, such as regions, plans or teams. Do not add
a group with a single option, because the heading then costs more space than the option.

### Compact toolbar

Hide the label with `IsLabelVisible="False"`. The trigger keeps the `Label` as its accessible name, so the pill stays
compact and a screen reader still names it.

<Demo name="CompactToolbar" />

```xml
<arc:MorphSelect Label="Status" IsLabelVisible="False" />
<arc:MorphSelect Label="Priority" PanelAlignment="End" PanelWidth="220"
                 ItemsSource="{Binding Priorities}" />
```

Hide the label only when the pill's value explains itself, such as a status or a priority in a toolbar row. In a form
where the value could be read several ways, keep the label visible above the trigger.

### Search

`SearchMode` decides whether the open surface has a search field. `Auto`, the default, adds one above eight options.
`On` always adds it, and `Off` never does. `SearchPlaceholder` sets its hint, and it defaults to the selected label
or "Search". Typing a letter on the closed trigger jumps to a matching option and opens the list. Once the list is open
with search on, the typed text goes to the search field, and a match is searched by label, meta and keywords.

```xml
<arc:MorphSelect Label="Owner"
                 SearchMode="On"
                 SearchPlaceholder="Find a person"
                 ItemsSource="{Binding Owners}" />
```

Turn search on for any list of more than about eight people, projects or accounts. Keep `Auto` for short lists that
should not show a field, and use `Off` when the list is ordered by a rank that a search would hide.

### Binding to a view model

With CommunityToolkit.Mvvm, the view model owns the value and the list, and the morph select edits the value through a
binding. The label of the value is read-only and shown in summaries.

```csharp
public partial class DeploymentSettingsViewModel : ObservableObject
{
    public IReadOnlyList<SelectOption> Regions { get; }

    [ObservableProperty]
    private string? _regionId = "us-east-1";

    [ObservableProperty]
    private bool _isRegionOpen;

    public DeploymentSettingsViewModel(ICloudCatalog catalog)
    {
        Regions = catalog.Regions()
            .Select(r => new SelectOption(r.Id, r.Name) { Group = r.Area, Meta = r.Code })
            .ToArray();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _deployments.SetRegionAsync(RegionId);
    }
}
```

```xml
<StackPanel Spacing="16" HorizontalAlignment="Left">
    <arc:MorphSelect Label="Region"
                     ItemsSource="{Binding Regions}"
                     SelectedValue="{Binding RegionId, Mode=TwoWay}"
                     IsOpen="{Binding IsRegionOpen, Mode=TwoWay}" />
    <arc:Button Content="Save region" Command="{Binding SaveCommand}" />
</StackPanel>
```

- `SelectedValue` is two-way and holds the stored identifier, so the view model receives the choice. Use the value, not
  the index, so reloading the list does not change the selection.
- `IsOpen` is two-way. Bind it when the view model must close the list before it navigates, or when a change elsewhere
  should open it.
- `SelectedText` is read-only and holds the label of the selection. Show it in a summary line rather than binding a
  view model property back to it.
- Use `SelectionChanged` for side effects that must run on every pick, and keep the command for the save.

### Validation

The trigger has no message row of its own. Place a `TextBlock` below the field and bind it to the view model's error,
using the danger text color for the message. Validate when the list closes, not while the person is moving the
highlight, so the message does not change under the pointer.

| Rule | When to check | Where it shows |
|---|---|---|
| A value is required | On save, or when the list closes with no value | A message below the trigger, with focus moved to it |
| The region is still offered | On save, because an option can be disabled later | A message below the trigger that names the region |
| The choice is allowed for the account | On the server response | The same message row, cleared when the value changes |

### Example: deployment settings panel

A deployment settings panel in a developer console. The environment and the region are closed triggers with a visible
label, and the environment has one disabled option that explains why it cannot be used. The screenshot shows the
resting state: the trigger pills, their labels and the placeholder of the field that has no value yet.

![A deployment settings panel with environment and region pills and an owner placeholder](images/DeploymentSettings.png)

```xml
<StackPanel Width="560" Spacing="20">
    <TextBlock Text="Deployment settings"
               FontSize="18"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:MorphSelect Label="Environment" SelectedIndex="1">
        <arc:SelectItem Content="Production" />
        <arc:SelectItem Content="Staging" />
        <arc:SelectItem Content="Preview" />
        <arc:SelectItem Content="Archived" IsEnabled="False" />
    </arc:MorphSelect>
    <arc:MorphSelect Label="Region" SelectedIndex="2">
        <arc:SelectItem Content="US East (Virginia)" Group="Americas" Meta="us-east-1" />
        <arc:SelectItem Content="US West (Oregon)" Group="Americas" Meta="us-west-2" />
        <arc:SelectItem Content="EU West (Dublin)" Group="Europe" Meta="eu-west-1" />
        <arc:SelectItem Content="EU Central (Frankfurt)" Group="Europe" Meta="eu-central-1" />
        <arc:SelectItem Content="Asia Pacific (Tokyo)" Group="Asia Pacific" Meta="ap-northeast-1" />
    </arc:MorphSelect>
    <arc:MorphSelect Label="Release owner" Placeholder="Choose an owner">
        <arc:SelectItem Content="Priya Raman" />
        <arc:SelectItem Content="Marcus Hale" />
        <arc:SelectItem Content="Elena Ruiz" />
    </arc:MorphSelect>
    <TextBlock Text="Changes apply to the next deployment."
               FontSize="12"
               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
</StackPanel>
```

### Content guidelines

- **Label**: a noun naming the property, in sentence case, such as "Environment" or "Region". The label names the
  trigger for assistive technology even when it is hidden.
- **Placeholder**: an instruction that completes the label, such as "Choose an owner". The default "Select" is fine for
  a generic field.
- **Option labels**: the name people use in their work, such as "US East (Virginia)" or "Production". Put the
  identifier in `Meta` and the aliases in `Keywords`.
- **Group headings**: a plural noun for the family, such as "Americas" or "Europe".
- **Search placeholder**: name what to search for, such as "Find a person" or "Find a region".

| Instead of | Write |
|---|---|
| Select... | Choose an owner |
| ENV | Environment |
| Region (required) | Region |
| Misc | Other regions |
| Click here to pick | Choose a region |

### Best practices

**Do**

- Keep the label visible in forms, and hide it only in toolbars where the value explains the control by itself.
- Bind `SelectedValue` and keep the stored identifier in the view model, so renaming an option does not change the saved
  choice.
- Give each group at least two options, so the headings help a person scan the list.
- Turn search on for any list of more than about eight options that people know by name.

**Avoid**

- Using the pill for actions such as Rename or Archive. Those belong in `DropdownMenu`, where the items run commands.
- Long labels in a narrow toolbar. The trigger grows with its label up to 320 px, and that pushes the other toolbar items
  aside.
- Disabling an option without a reason in its label or in the surrounding copy. A person who cannot choose "Archived"
  needs to know why.
- Re-sorting the options while the list is open. The highlight stays on its index, so the person can pick the wrong item.
- Using the chevron alone as the signal that a field is interactive. Keep the label and the pill shape visible.

### Common questions

**Why does the pill grow when I pick a longer value?** The trigger is as wide as its label plus the padding and the
chevron, up to 320 px, so the pill fits the value. Give the toolbar a fixed slot if it must not move as the value
changes.

**How do I open the list on the last option?** Press `End` on the closed trigger, which opens the list on the last
option. `Home` opens it on the first. Enter, Space, `Down` and `Up` open it on the selected option.

**Does the search field show when I set `SearchMode` to `Off`?** No. `Off` never shows it, even for a long list, and
typing on the closed trigger still jumps to a matching option.

**How do I keep the list from covering the page edge?** Set `PanelAlignment` to `End` so the surface grows to the left
of the trigger. The surface keeps its position relative to the trigger and does not flip to another side.

**Can the list show options from a server?** Load the options in the view model and assign `ItemsSource` when they
arrive. The selection is matched by value, so keep the identifiers stable across loads.

## API reference

<!-- api: MorphSelect -->

<!-- api: SelectItem -->

## Accessibility

- `Label` names the trigger, even when `IsLabelVisible` is `False`. The list is named by the same label.
- The trigger is a combo box with a listbox popup. While the list is open, the expanded state is exposed and the
  highlighted option is announced.
- Arrows move the highlight and skip disabled options. `Page Up` and `Page Down` move eight options at a time, and `Home`
  and `End` go to the first and last option.
- `Enter` or `Space` picks the highlighted option. `Alt+Up` picks it and closes the list. `Escape` closes the list and
  returns focus to the trigger.
- Option groups are named by their headings. When search is on, the number of results is announced politely, and the
  search field takes over the combo box role while the list is open.

### Keyboard

| Keys | Action |
|---|---|
| `Enter`, `Space`, `Down`, `Up` on the closed trigger | Open the list on the selected option |
| `Home` / `End` on the closed trigger | Open the list on the first or last option |
| `Down` / `Up` with the list open | Move the highlight to the next or previous enabled option; no wrap at the ends |
| `Page Down` / `Page Up` | Move the highlight eight options at a time |
| `Home` / `End` with the list open | Move the highlight to the first or last option |
| `Enter` or `Space` with the list open | Pick the highlighted option and close the list |
| `Alt+Up` | Pick the highlighted option and close the list |
| Printable characters | Jump to the first matching option; with search on, type into the search field |
| `Escape` | Close the list without picking and return focus to the trigger |
| `Tab` | Close the list without picking |

## Motion

- The trigger grows into the list. Its corners reshape from the pill to the panel corner, and the panel settles with a
  small bounce when it opens and with no overshoot when it closes.
- The highlight glides between options.
- The chosen label flies from the list back into the trigger, and the trigger width settles to the new label.
- The value in the trigger rolls in the direction of the list, so the change reads as the same value moving.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The list opens | The trigger grows into the panel with a small bounce; corners reshape | The size jumps to the open shape with a short fade | Immediate |
| The list closes | The panel shrinks back to the pill with no overshoot | The size jumps to the closed shape with a short fade | Immediate |
| The highlight moves | It glides to the next option | Jumps to the option | Immediate |
| An option is picked | The label flies into the trigger and the width settles | A short fade | Immediate |
| Search takes over the trigger | The search field fades in with a subtle blur | A short fade | Immediate |

## Related

- `Select` for a plain labelled choice field with a fixed list.
- `Combobox` for a searchable single choice that people type into.
- `MultiSelect` when several values are chosen.
- `DropdownMenu` when the menu runs actions rather than choosing a value.

| Component | Choose it when |
|---|---|
| `Select` | A classic labelled field with a fixed list fits the form |
| `Combobox` | People type to find a value, or the list is long and remote |
| `MultiSelect` | Several values are chosen and each stays visible as a token |
| `DropdownMenu` | The menu runs commands such as Rename or Archive |
| `SegmentedControl` | Two to four options should all be visible at once |
| `ChipGroup` | A few facets are switched often and every option stays visible |
