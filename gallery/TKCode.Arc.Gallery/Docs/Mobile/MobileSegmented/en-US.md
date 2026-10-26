---
title: Mobile segmented control
summary: A short row of segments that share the width, one selected, for switching between views of the same content.
---

<Demo name="Basic" />

## When to use

`Segmented` switches between a few views of one thing: a time range, a sort order, a list and a map of the same places.

- **Two to four options** with short labels, all visible at once.
- **Views of the same content**: the content below changes, the screen does not.

The live demo runs inside a phone frame, a desktop simulation; touch feedback on a real device has not been verified here.

## When not to use

- **Top-level areas of an app**: use `Tabbar`.
- **More than four options**, or long labels: use a list or a sheet.
- **Several choices at once**: use checkboxes.

## Usage

Put `SegmentedItem`s in the control and set `SelectedIndex`. `Segmented` derives from Avalonia's `ListBox`, so `SelectedItem`, `SelectionChanged` and binding to `ItemsSource` work as they do there.

```xml
<arcm:Segmented xmlns="https://github.com/avaloniaui"
                xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
                SelectedIndex="1">
    <arcm:SegmentedItem Content="Day" />
    <arcm:SegmentedItem Content="Week" />
    <arcm:SegmentedItem Content="Month" />
</arcm:Segmented>
```

The segments share the width equally. The selected one is raised on a Surface fill with an outline and uses the foreground color; the others use the secondary text color. A finger on an unselected segment dims it; there is no hover state.

### Tokens

`SegmentedToken` holds the track's geometry: `TrackPadding` (2) and `TrackRadius` (10). `SegmentedItemToken` holds a segment's: `ItemHeight` (36), `ItemPadding`, `ItemRadius` (8) and `PressedOpacity` (0.6). With the padding, the control is 40 high. Colors and type come from the ArcDesign tokens.

## API reference

<!-- api: MobileSegmented -->

<!-- api: MobileSegmentedItem -->

## Accessibility

Segments are selectable list items named by their labels. Keep the labels short and distinct. A segment is 36 px high and wide, as the width is shared, so with four options a narrow phone still gives each a usable target.

## Motion

The fill, outline and text color change on the fast curve, and at once with reduced motion.

## Related

- `MobileTabbar`: for top-level areas.
- `MobileNavbar`: the segmented control often sits under it.
- `MobilePage`: the screen it lives on.
