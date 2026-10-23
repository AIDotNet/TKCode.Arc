---
title: Segmented control
summary: Switch between a small set of related views.
---

<Demo name="ViewSwitcher" />

## When to use

A segmented control is a row of two to five short choices where exactly one is active, and choosing one changes the
view at once. It sits in the header of a page or a panel, above the content it controls, and it keeps the choice
visible: the person always sees every option and which one is showing. Use it when the options are views of the same
data rather than separate pages, so the content swaps in place and the control stays where it is.

- **Time range on an analytics dashboard**: Day, Week, Month and Quarter switch the revenue chart and its totals. The
  chart redraws for the chosen range, and the control keeps the range visible while it does.
- **Status filter on an incident or ticket list**: All, Open and Resolved narrow the same list. An accessory shows the
  count of open items, so the person sees the size of each filter before choosing it.
- **Layout or density switch in a data table**: Compact and Comfortable change the row height of the grid that is
  already on screen, which is a setting that takes effect at once.
- **View mode on a shared workspace**: Board, List and Calendar show the same tasks in three arrangements, with the
  current arrangement kept as the selected segment.
- **Environment or account switch in an admin console**: Production, Staging and Sandbox change which data the page
  reads. The selected segment states the context that every number on the page depends on.

## When not to use

- **Options that need a description, a vertical layout or a longer label**: use `RadioGroup`, which gives each option
  a row with room for a description and wraps the text.
- **Options that need a price, a meta line or an icon on a card**: use `RadioCards`, which lays each option out as a
  selectable card.
- **A single on or off setting**: use `Switch`. A segmented control with two segments reads as a choice between
  views, not as a setting.
- **A choice with many options, or options that do not fit a short row**: use `Select`, which holds a long list in one
  field.
- **Switching between whole panels of content with their own tabs**: use `Tabs`, which is made for content areas and
  their navigation rather than for a row of view modes.

## Usage

Each `SegmentedOption` takes a value and a label. The control selects the segment with the value `SelectedValue`.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SegmentedControl AutomationProperties.Name="Range" SelectedValue="week">
        <arc:SegmentedOption Value="day" Label="Day" />
        <arc:SegmentedOption Value="week" Label="Week" />
        <arc:SegmentedOption Value="month" Label="Month" />
    </arc:SegmentedControl>
</UserControl>
```

`SegmentedControl` derives from Avalonia's `SelectingItemsControl`, so `SelectedIndex`, `SelectedItem`,
`SelectedValue` and `SelectionChanged` behave as they do in any Avalonia selecting control. The control adds the
pill that glides between segments, the arrow-key model, the overflow scrolling and the `ItemIntent` event.

### Anatomy

1. **Frame**: a 1 px border with the control radius on the muted surface color. The segments scroll inside it, so the
   frame keeps its shape when the segments overflow.
2. **Track**: a 3 px inset on every side, with 2 px between segments.
3. **Selection pill**: a single rounded shape on the surface color with a 1 px border and the resting shadow. It sits
   under the labels and moves to the selected segment.
4. **Segment**: at least the small Arc control height (36 px in the default theme), with 12 px of padding on each side
   and a 14 px corner radius. Segments never shrink and never wrap their labels.
5. **Label** (`Label`): medium weight in the small type size. It uses the muted text color; it changes to the
   foreground color while the segment is hovered, pressed or selected.
6. **Accessory** (`SegmentedOption.Accessory`): optional content after the label, separated by a 6 px gap. It is
   hidden when `Accessory` is empty.
7. **Focus ring**: a 2 px ring drawn inside the focused segment. The scroll area would clip an outline drawn outside,
   so the ring stays within the segment.

With the default metrics and no accessory, the control is 44 px tall: a 36 px segment, the 3 px track inset above and
below it, and the 1 px frame.

### Choosing the variant

Use the control's default metrics for every view switch on a page, so all switches look the same. The control has no
size property: its height comes from the segment and the track, and its font follows the Arc body type. Plan the
number of segments against the width of the header. Two to five short segments fit a header comfortably; six or more
labels can overflow a header-width row, and then a `Select` holds the choice better.

Put the control at the top of the content it controls, aligned to the start of the page. The control aligns to the
left by default, so set `HorizontalAlignment="Center"` only when it is the only element in a centered header.

### Items from code

Bind `ItemsSource` to a list of options. The control also accepts plain strings, and `SelectedIndex` selects a segment
by position.

```csharp
var sections = new SegmentedControl
{
    MaxWidth = 300,
    ItemsSource = new[] { "Overview", "Activity", "Deployments", "Analytics", "Logs", "Settings" },
    SelectedIndex = 0
};
```

A plain string is its own value, so `SelectedValue` returns the string itself. Use `SegmentedOption` when the value and
the label differ, for example when the value is an enum or an ID and the label is for people to read.

### Accessory

`Accessory` is content shown after the label, such as a count. It accepts any content, so a count can be a
`TextBlock` in a small badge.

<Demo name="AccessoryAndIntent" />

```xml
<arc:SegmentedControl SelectedValue="all">
    <arc:SegmentedOption Value="all" Label="All" />
    <arc:SegmentedOption Value="unread" Label="Unread">
        <arc:SegmentedOption.Accessory>
            <TextBlock Text="3" />
        </arc:SegmentedOption.Accessory>
    </arc:SegmentedOption>
    <arc:SegmentedOption Value="archived" Label="Archived" IsDisabled="True" />
</arc:SegmentedControl>
```

Keep accessories to a number or a short status mark. The accessory sits inside the segment, so a long label or a wide
badge makes the segment wider and pushes its neighbors. Update the number in the accessory when the data changes; the
segment does not need to be rebuilt.

### Binding to a view model

With CommunityToolkit.Mvvm, the view model holds the list of options and the selected value, and the control shows
them. `SelectedValue` is two-way, so the view model reads the choice made by the pointer or the keyboard.

```csharp
public partial class ActivityViewModel : ObservableObject
{
    public ObservableCollection<SegmentedOption> Ranges { get; } = new()
    {
        new SegmentedOption("day", "Day"),
        new SegmentedOption("week", "Week"),
        new SegmentedOption("month", "Month")
    };

    [ObservableProperty]
    private string? _selectedRange = "week";
}
```

```xml
<arc:SegmentedControl ItemsSource="{Binding Ranges}"
                      SelectedValue="{Binding SelectedRange}"
                      AutomationProperties.Name="Range" />
```

- `SelectedValue` is two-way. Bind it to the property that holds the current view, and let the view model decide what
  the view shows from that value.
- `ItemsSource` takes an `ObservableCollection<SegmentedOption>`, so the options can change while the page is open.
- Use `SelectionChanged` when a change must load data, and keep the loading in a command or a service, not in the
  control.
- `AutomationProperties.Name` names the group. Set it in XAML, because the control has no header of its own.

### Intent

`ItemIntent` is raised when the pointer or the focus reaches a segment, before the segment is chosen. Use it for a
preview, such as a hint. Do not use it to change the view.

```csharp
segmented.ItemIntent += (_, e) => hint.Text = $"Intent: {e.Value}";
segmented.SelectionChanged += (_, _) => ShowView(segmented.SelectedValue);
```

The event carries the segment's `Item` and its `Value`. A mouse that enters a segment raises it, and so does a keyboard
focus move. Touch and pen input do not raise it on hover, but they select the segment when tapped, which then raises
`SelectionChanged`. Preload the content for a segment in `ItemIntent` when loading takes time, so the view appears
without a wait after the choice.

### Overflow

When the options are wider than the control, the row scrolls inside itself. The scroll bar is hidden, and a 20 px fade
marks each edge that has more content. The selected segment is always scrolled fully into view with 20 px of room
beside it, so it clears the fade.

<Demo name="Overflow" />

Overflow keeps the control usable on a narrow panel, but it hides options from view. Choose the number of segments so
that the row fits the widest layout the page supports. When a segment is off screen, the person may not see that it
exists, so use `Select` for a longer set.

### Example: incident status filter

A status filter in an incident list for a payments platform. The three segments are short nouns, and the Open
segment carries a count, so the person sees how many items each choice holds before selecting it. The list below the
control shows the filtered rows, and the heading gives the scope the list covers.

![An incident list with a status filter that has Open selected and a count of two](images/IncidentFilter.png)

```xml
<StackPanel Width="680" Spacing="16">
    <StackPanel Spacing="4">
        <TextBlock Text="Incidents"
                   FontSize="18"
                   FontWeight="SemiBold"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Payments API, last 7 days"
                   FontSize="13"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
    <arc:SegmentedControl AutomationProperties.Name="Incident status" SelectedValue="open">
        <arc:SegmentedOption Value="all" Label="All" />
        <arc:SegmentedOption Value="open" Label="Open">
            <arc:SegmentedOption.Accessory>
                <Border Padding="6,0"
                        CornerRadius="999"
                        VerticalAlignment="Center"
                        Background="{arc:ArcDesignTokenResource Surface}">
                    <TextBlock Text="2"
                               FontSize="11"
                               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
                </Border>
            </arc:SegmentedOption.Accessory>
        </arc:SegmentedOption>
        <arc:SegmentedOption Value="resolved" Label="Resolved" />
    </arc:SegmentedControl>
    <StackPanel Spacing="8">
        <Border Padding="16,12"
                BorderBrush="{arc:ArcDesignTokenResource Border}"
                BorderThickness="1"
                CornerRadius="{arc:ArcDesignTokenResource RadiusControl}">
            <Grid ColumnDefinitions="*,Auto">
                <StackPanel Spacing="2">
                    <TextBlock Text="Checkout latency above 900 ms"
                               FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="inc_2026_0412 · Sev 2 · Started 20 October 2026, 09:14 UTC"
                               FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
                <TextBlock Grid.Column="1"
                           Text="Investigating"
                           FontSize="12"
                           VerticalAlignment="Center"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </Grid>
        </Border>
        <Border Padding="16,12"
                BorderBrush="{arc:ArcDesignTokenResource Border}"
                BorderThickness="1"
                CornerRadius="{arc:ArcDesignTokenResource RadiusControl}">
            <Grid ColumnDefinitions="*,Auto">
                <StackPanel Spacing="2">
                    <TextBlock Text="Webhook deliveries delayed in the EU region"
                               FontWeight="Medium"
                               Foreground="{arc:ArcDesignTokenResource Foreground}" />
                    <TextBlock Text="inc_2026_0415 · Sev 3 · Started 21 October 2026, 06:40 UTC"
                               FontSize="12"
                               Foreground="{arc:ArcDesignTokenResource TextMuted}" />
                </StackPanel>
                <TextBlock Grid.Column="1"
                           Text="Monitoring"
                           FontSize="12"
                           VerticalAlignment="Center"
                           Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            </Grid>
        </Border>
    </StackPanel>
</StackPanel>
```

### Content guidelines

- **Label**: one or two words that name the view or the status: "Day", "Open", "Compact". A segment never wraps, so a
  long label widens the whole control. Use sentence case.
- **Accessory**: a count in digits, or a short mark. Do not put a sentence in an accessory.
- **Group name**: `AutomationProperties.Name` names the group for assistive technology. Write it as a noun: "Range",
  "Incident status", "View".
- **Segment order**: put the shortest span or the most common view first, and keep the same order on every page that
  uses the same control.

| Instead of | Write |
|---|---|
| Show me the data for the last day | Day |
| Open issues (12 of them) | Open with the accessory 12 |
| Click here to view resolved items | Resolved |
| VIEW MODE | View |

### Best practices

**Do**

- Keep each segment a view of the same content, so the switch changes the data and not the page structure.
- Keep the selected value in the view model and update the content from it, so the control and the content cannot
  disagree after a reload.
- Use the same order and labels for the same choice on every page, so the person learns them once.
- Name the group with `AutomationProperties.Name` whenever no visible header says what the control chooses.

**Avoid**

- Using the control for a navigation between pages. A page change belongs in the navigation, and the segmented control
  keeps its choice on the current page.
- Doing expensive work in `ItemIntent`. It is not throttled, so a pointer that sweeps across the row raises it for every
  segment it passes.
- Putting disabled segments in the middle of the row without a reason. A disabled segment is still visible, and the
  person needs to know why it cannot be chosen.
- Labels that vary in length within one control. Equal-length labels keep the pill's width even and make the row easier
  to scan.

### Common questions

**Why does the control not fill its container?** It aligns left and sizes to its segments. Set `Width` for a fixed
width, or `HorizontalAlignment="Center"` to center it in a header. When the segments do not fit the width, the row
scrolls.

**How do I select a segment in code?** Set `SelectedValue` for a value, or `SelectedIndex` for a position. When the
segments overflow, the selected segment is scrolled into view as it is selected.

**Does `ItemIntent` fire before a mouse click?** Yes. A mouse raises `ItemIntent` when it enters the segment, so the
preview happens before the click chooses the segment and raises `SelectionChanged`. Touch input does not raise it on
hover.

**Can I bind a list of plain strings?** Yes. `ItemsSource` accepts strings, and `SelectedValue` returns the string
itself. Use `SegmentedOption` when the value must differ from the text, such as an enum or an ID.

## API reference

<!-- api: SegmentedControl -->
<!-- api: SegmentedOption -->

## Accessibility

- `AutomationProperties.Name` names the group. Set it on every control that has no visible header.
- Each segment is a button that reports its pressed state. The sliding pill is hidden from assistive technology.
- Tab enters the control at the selected segment, or the first segment when none is selected. A second Tab leaves it.
- Arrow keys select and focus the next or previous segment and wrap around. Home and End select the first and last
  segment. Disabled segments are skipped.
- Keyboard focus draws a 2px outline inside the segment, so the scroll area does not clip it. Pointer focus does not.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Enters the control at the selected segment, or the first segment when none is selected; the next `Tab` leaves it |
| `Right` / `Down` | Selects and focuses the next segment, wrapping from the last to the first |
| `Left` / `Up` | Selects and focuses the previous segment, wrapping from the first to the last |
| `Home` / `End` | Selects and focuses the first or last segment |
| `Space` / `Enter` | Selects the focused segment |

Focus raises `ItemIntent` for each segment it reaches. The arrow keys then select that segment, so the preview and the
change come from the same movement.

## Motion

- One pill glides under the selected segment on a morph spring. The pill sits beneath the labels and scrolls with them.
- Segments never scale or change weight. Only the pill moves, and the label color changes quickly.
- When the segments overflow, the row scrolls to keep the selected segment in view, with a short ease. The first
  placement after the control loads is immediate.
- Reduced motion moves the pill at once and removes the color transition. Off does the same.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Selection moves | The pill glides on a morph spring | The pill moves at once | The pill moves at once |
| Label color (hover, press, selected) | Crossfades on the fast tween | Immediate | Immediate |
| Overflow scrolls to the selected segment | Eases over the standard tween after the first layout | Jumps | Jumps |
| Edge fade appears or disappears | Follows the scroll position | Follows the scroll position | Follows the scroll position |
| First placement after load | Immediate | Immediate | Immediate |

## Related

- `RadioGroup` for options that need descriptions or a vertical layout.
- `BillingToggle` for a monthly or yearly billing period with savings notes.
- `Switch` for a single setting that takes effect at once.

| Component | Choose it when |
|---|---|
| `RadioGroup` | Options need a description each, or a vertical list suits the form |
| `RadioCards` | Options need a price, a meta line or an icon on a card |
| `Tabs` | Each option opens a whole panel of content with its own navigation |
| `Select` | The options are many, or they do not fit a short row |
| `Switch` | One setting takes effect at once and has no alternative values |
| `BillingToggle` | The choice is a monthly or yearly billing period with savings notes |
