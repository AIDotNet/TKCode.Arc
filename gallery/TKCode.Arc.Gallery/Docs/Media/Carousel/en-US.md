---
title: Carousel
summary: Browse a row of slides by dragging, flicking, or arrowing through them.
---

<Demo name="Slides" />

## When to use

- Browsing a small set of similar items, such as suites, offers or photos.
- Showing that more items exist: the neighbours at each side hint at the rest of the row.
- Rotating through featured content with an optional autoplay.

## When not to use

- Deciding on each item in turn: use `CardStack`.
- Comparing two images: use `ImageCompare`.
- Long lists of records that the reader scans or sorts: use `SortableDataTable`.

## Usage

Each child element becomes one slide. `Label` names the carousel for assistive technology.

```xml
<arc:Carousel Label="From the Harbour suites">
    <Border Height="200" Background="#2F4858" CornerRadius="12" />
    <Border Height="200" Background="#D9D2C5" CornerRadius="12" />
    <Border Height="200" Background="#8AA399" CornerRadius="12" />
</arc:Carousel>
```

### Bound items

Use `ItemsSource` with an `ItemTemplate` when the slides come from data.

```xml
<arc:Carousel Label="Harbour suites" ItemsSource="{Binding Suites}">
    <arc:Carousel.ItemTemplate>
        <DataTemplate>
            <Border Height="200" Background="#2F4858" CornerRadius="12">
                <TextBlock Text="{Binding Name}" Margin="18" />
            </Border>
        </DataTemplate>
    </arc:Carousel.ItemTemplate>
</arc:Carousel>
```

### Slide size and labels

Slides are `SlideWidthRatio` of the carousel width (0.8 by default), capped at `MaxSlideWidth` (340). `SlideGap`
sets the space between slides. `SlideLabelFormatter` builds the text that is announced for each slide.

```csharp
carousel.SlideLabelFormatter = (index, count) => $"Archive photo {index + 1} of {count}";
```

### Autoplay

Set `Interval` to add a play control. `IsAutoPlay` starts rotation, and the control toggles it. Rotation moves
forward and returns to the first slide after the last one. The indicator drains while a slide is on show.

<Demo name="Rotation" />

```xml
<arc:Carousel Label="Seasonal offers" Interval="0:0:4" IsAutoPlay="True">
    <Border Height="200" Background="#2F4858" CornerRadius="12" />
    <Border Height="200" Background="#D9D2C5" CornerRadius="12" />
</arc:Carousel>
```

### Code-driven navigation

```csharp
carousel.SelectionChanged += (_, _) => readout.Text = $"Slide {carousel.SelectedIndex + 1} of {count}";

carousel.Next();
carousel.Previous();
carousel.GoTo(3);
```

### Long carousels

With 120 slides, only the slides around the current position are realized.

<Demo name="LongCarousels" />

### Photos

Slides can show `AsyncImage` photos over a drawn scene, so they still look complete offline.

<Demo name="Photos" />

## API reference

<!-- api: Carousel -->

## Accessibility

- `Label` names the carousel. Slides are announced as "2 of 5" through `SlideLabelFormatter` when the selection changes.
- Left and right arrows move one slide. Home and End go to the first and the last slide.
- Tab moves through the slides, the play control, the indicator and the previous and next buttons.
- Slides that are not active are removed from the focus order and from the automation tree.
- Autoplay pauses while the pointer is over the carousel, while keyboard focus is inside it, while a drag runs,
  when it scrolls out of view and when the window is minimized. Autoplay does not announce each slide.

## Motion

- Slides follow the pointer while you drag and carry momentum into a snap to the nearest slide. A keyboard or button
  move slides in the direction of travel.
- Neighbours shrink and dim with their distance from the active slide.
- The indicator stretches into a pill that follows the active slide, and the pill drains while autoplay waits.
- Reduced motion jumps straight to the target slide and stops autoplay. Off jumps to the end state at once.

## Related

- `CardStack` when each item needs a decision, not only a look.
- `ImageCompare` for a before and after of one picture.
- `Tabs` for switching between panels that are not a row of similar items.
