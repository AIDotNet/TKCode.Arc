---
title: Glass segmented
summary: A segmented control on frosted glass with a selection lens that stretches as it glides between segments and can be dragged.
---

<Demo name="Preview" />

## When to use

`GlassSegmented` switches between two to five related views or modes on a frosted glass track that floats over a
picture or a colorful header. The chosen segment sits in a lens of brighter glass. When the choice changes the lens
glides over: its leading edge runs ahead and the trailing edge follows, so it stretches on the way and draws together as
it lands, like a drop of liquid. Press the selected segment and the lens swells under the finger; drag it and it follows,
choosing the segment it is released over.

- **A range over a chart or a map**: Day, Week, Month, Year.
- **A view switch on a media header**: Photos and Albums, Map and List.
- **A mode on a glass toolbar**: Light, Dark and Screen in a viewer.

## When not to use

- **On a plain page**: use `SegmentedControl`, which is lighter.
- **More than five options, or long labels**: use `Tabs` or `Select`.
- **Turning one thing on and off**: use `Switch`.
- **Top-level navigation of an app**: use `GlassTabBar` or `Tabs`.

## Usage

Write the segments inline as `GlassSegment`s (a label and an optional `Icon`), or give the control `ItemsSource`. One
segment is always selected; read or bind `SelectedIndex` or `SelectedItem`, and react to `SelectionChanged`.

```xml
<Panel arc:GlassBackdrop.Source="{Binding #Scene}">
    <Border Name="Scene" Background="#2B3A55" /><!-- your picture or page -->
    <arc:GlassSegmented SelectedIndex="1" HorizontalAlignment="Center">
        <arc:GlassSegment Content="Day" />
        <arc:GlassSegment Content="Week" />
        <arc:GlassSegment Content="Month" />
    </arc:GlassSegmented>
</Panel>
```

`GlassSegmented` derives from Avalonia's `SelectingItemsControl` and lives in the Special package: register it with
`UseArcSpecial()` next to `UseArc()`. The frost works as for every glass surface: see the Glass card page for how it is
made with public Avalonia APIs.

### Icons and disabled segments

`Icon` takes any 24×24 stroke geometry, such as the `ArcIcons` set. A disabled segment cannot be chosen; the arrow keys
skip it and the lens never lands on it.

<Demo name="Icons" />

### Options from data

Items from `ItemsSource` are shown as their text.

<Demo name="Bound" />

### Gestures

| Input | Effect |
|---|---|
| Click or tap a segment | Select it; the lens glides over |
| Press the selected segment | The lens swells |
| Drag from the selected segment | The lens follows along the track, changing width between segments of different sizes |
| Release after a drag | Select the segment nearest to the lens |

### Content guidelines

- Keep labels to one short word each and give the segments a similar length.
- Use icons for every segment or for none.
- Put the control over a calm part of the backdrop; busy detail under the track makes the labels harder to read.

## API reference

<!-- api: GlassSegmented -->

<!-- api: GlassSegment -->

## Accessibility

- The control is a tab list and each segment a tab with its label as name and its selected state; screen readers can
  select a segment directly.
- Only the selected segment is in the Tab order; the arrow keys move the selection and the focus together.
- Keyboard focus shows a 2px ring around the focused segment; the pointer draws none.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Into the control (the selected segment) and out of it |
| `Right` / `Down` | Next segment, wrapping (left in right-to-left layouts) |
| `Left` / `Up` | Previous segment, wrapping |
| `Home` / `End` | First or last segment |

## Motion

- The leading edge of the lens moves on a spring of about 0.28 s with a little give, the trailing edge on a calmer one of
  about 0.46 s, so the lens stretches toward the new segment and settles to its width.
- Pressed, the lens swells by 2px on every side on a snappy spring; while dragged it follows the pointer on a tight
  spring of about 0.18 s.
- Labels change color over 160ms.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Selection changes | Lens glides and stretches | Lens fades out and in at the new segment | Lens moves at once |
| Press and drag | Lens swells and follows | Lens follows without swelling | Lens follows without swelling |
| Label color | 160ms | 120ms | At once |

## Responsive behavior

- The control is as wide as its segments; it does not scroll. Keep it to five short options, or switch to `Select` in
  narrow layouts.
- Resizing puts the lens straight onto its segment without animating.
- Touch works like the mouse: tap to select, press and drag the selected segment to move the lens.

## Related

- `SegmentedControl` for the same choice on a plain surface.
- `GlassTabBar` for app navigation on glass.
- `GlassCard` and `GlassMenu` for other glass surfaces.

| Component | Choose it when |
|---|---|
| `GlassSegmented` | A small set of views or modes over a picture or a colorful header |
| `SegmentedControl` | The same choice on a plain page |
| `Tabs` | Many views, or views with longer names |
