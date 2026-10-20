---
title: Segmented control
summary: Switch between a small set of related views.
---

## When to use

- Two to five short views of the same content, such as day, week and month.
- A layout or mode switch that takes effect at once.
- A row of options whose labels are short enough to read at a glance.

## When not to use

- Options that need a description or a vertical layout: use `RadioGroup`.
- Options that need a price, a meta text or an icon: use `RadioCards`.
- A single on or off setting: use `Switch`.
- A choice with many options: use `Select`.

## Usage

Each `SegmentedOption` takes a value and a label. The control selects the segment with the value `SelectedValue`.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SegmentedControl AutomationProperties.Name="Range" SelectedValue="week">
        <arc:SegmentedOption Value="day" Label="Day" />
        <arc:SegmentedOption Value="week" Label="Week" />
        <arc:SegmentedOption Value="month" Label="Month" />
    </arc:SegmentedControl>
</UserControl>
```

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

### Accessory

`Accessory` is content shown after the label, such as a count.

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

### Binding

`SelectedValue` is two-way. Bind it to the view model property that holds the current view.

```xml
<arc:SegmentedControl ItemsSource="{Binding Ranges}"
                      SelectedValue="{Binding SelectedRange}"
                      AutomationProperties.Name="Range" />
```

### Intent

`ItemIntent` is raised when the pointer or the focus reaches a segment, before the segment is chosen. Use it for a
preview, such as a hint. Do not use it to change the view.

```csharp
segmented.ItemIntent += (_, e) => hint.Text = $"Intent: {e.Value}";
segmented.SelectionChanged += (_, _) => ShowView(segmented.SelectedValue);
```

## API reference

<!-- api: SegmentedControl -->
<!-- api: SegmentedOption -->

## Accessibility

- `AutomationProperties.Name` names the group. Set it on every control that has no visible header.
- Each segment is a button that reports its pressed state. The sliding pill is hidden from assistive technology.
- Tab enters the control at the selected segment, or the first segment when none is selected. A second Tab leaves it.
- Arrow keys select and focus the next or previous segment and wrap around. Home and End select the first and last segment.
- Keyboard focus draws a 2px outline inside the segment, so the scroll area does not clip it. Pointer focus does not.

## Motion

- One pill glides under the selected segment on a morph spring. The pill sits beneath the labels and scrolls with them.
- Segments never scale or change weight. Only the pill moves, and the label color changes quickly.
- When the segments overflow, the row scrolls to keep the selected segment in view, with a short ease.
- Reduced motion moves the pill at once and removes the color transition. Off does the same.

## Related

- `RadioGroup` for options that need descriptions or a vertical layout.
- `BillingToggle` for a monthly or yearly billing period with savings notes.
- `Switch` for a single setting that takes effect at once.
