---
title: Tabs
summary: Switch between related content in the same context.
---

<Demo name="Default" />

## When to use

- Several views of the same object, such as Overview, Activity and Settings.
- Views that are peers: the user picks one at a time and each has its own panel.
- Long tab lists, which scroll horizontally with edge buttons.

## When not to use

- Choosing a compact value without a panel: use `SegmentedControl`.
- Content that is read in order as stacked sections: use `Accordion`.
- Moving through pages of results: use `Pagination`.

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

### Data-bound tabs

Set `ItemsSource`. `ItemTemplate` renders the trigger and `ContentTemplate` renders the panel of the
selected item. Read the selection through `SelectedItem` or `SelectionChanged`.

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

### Activation mode

`ActivationMode` controls what the arrow keys do. `Automatic` (the default) selects the tab that
receives focus. `Manual` only moves focus; Enter or Space selects the tab.

<Demo name="ManualActivation" />

### Selecting in code

```csharp
tabs.SelectTab(2);
tabs.SelectionChanged += (_, _) =>
{
    status.Text = $"Selected: {tabs.SelectedItem}";
};
```

`SelectTab` selects the tab at the index when it exists and is enabled, and returns `false` otherwise.

## API reference

<!-- api: Tabs -->

## Accessibility

- The list has the Tab role and each `TabItem` exposes its selection state. Give the list a name with
  `AutomationProperties.Name`.
- Only the selected tab is in the tab order. Arrow keys move between tabs, Home and End jump to the first and last.
- Tab moves from the list into the panel of the selected tab.
- Scroll buttons on an overflowing list are named by `ScrollLeftLabel` and `ScrollRightLabel`.
- The panel of a tab that is leaving is hidden from assistive technology and cannot take focus while it fades.

## Motion

- The selection pill glides between triggers on a morph spring.
- The new panel slides in 8px from the side of travel and fades in. The old panel stays in place, moves 6px back
  and fades out quickly.
- The panel area springs from the old panel height to the new one, so the content below moves with it.
- Reduced motion switches the pill at once and cross-fades the panels in place without height animation. Off
  switches everything at once.

## Related

- `SegmentedControl` for a compact choice of a value that has no panel.
- `Accordion` for stacked sections that are read in order.
- `Pagination` for moving through pages of one collection.
