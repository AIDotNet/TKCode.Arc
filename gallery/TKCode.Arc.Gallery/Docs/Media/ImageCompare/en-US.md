---
title: Image compare
summary: Drag a divider across two images to see what changed.
---

<Demo name="BeforeAndAfter" />

## When to use

- Showing a before and after of the same picture, such as a retouch, a redesign or a cleanup.
- Comparing two results where the reader should decide how far to reveal the change.
- Top and bottom comparisons: set `Orientation` to `Vertical`.

## When not to use

- Browsing several pictures: use `Carousel`.
- Splitting live content, such as two panels of an app: use `ResizablePanels`.
- Comparing records side by side: use `SortableDataTable`.

## Usage

Both sides should use the same crop and size, so the reveal lines up.

```xml
<arc:ImageCompare Width="600" AspectRatio="1.5">
    <arc:ImageCompare.Before>
        <Border Background="#D9D2C5" />
    </arc:ImageCompare.Before>
    <arc:ImageCompare.After>
        <Border Background="#2F4858" />
    </arc:ImageCompare.After>
</arc:ImageCompare>
```

### Labels and frame

`BeforeLabel` and `AfterLabel` caption the two sides. Each caption fades as the divider reaches it. `ShowLabels`
hides both captions. `AspectRatio` sets the frame width divided by its height, and the default is 1.5 (3 / 2).

<Demo name="WideFrame" />

```xml
<arc:ImageCompare AspectRatio="1.7777" ShowLabels="False" Position="30" Label="Cleanup preview">
    <arc:ImageCompare.Before>
        <Border Background="#D9D2C5" />
    </arc:ImageCompare.Before>
    <arc:ImageCompare.After>
        <Border Background="#8AA399" />
    </arc:ImageCompare.After>
</arc:ImageCompare>
```

### Vertical comparison

<Demo name="Vertical" />

```xml
<arc:ImageCompare Orientation="Vertical" BeforeLabel="Original" AfterLabel="Graded">
    <arc:ImageCompare.Before>
        <Border Background="#D9D2C5" />
    </arc:ImageCompare.Before>
    <arc:ImageCompare.After>
        <Border Background="#2F4858" />
    </arc:ImageCompare.After>
</arc:ImageCompare>
```

Changing `Orientation` at run time swings the divider a quarter turn while the reveal follows it.

### Pictures

`BeforeSource` and `AfterSource` draw a picture over the content of each side. The picture loads in the
background and fades in from a soft blur. If it fails, the content underneath shows instead.

<Demo name="Photos" />

```csharp
var compare = new ImageCompare
{
    Before = new Border { Background = Brushes.Gray },
    After = new Border { Background = Brushes.SteelBlue },
    BeforeSource = ImageSource.Parse("https://example.com/before.jpg"),
    AfterSource = ImageSource.Parse("https://example.com/after.jpg")
};
```

### Position

`Position` is the divider position in percent from the left, or from the top when vertical. It is two-way and
defaults to 50. `PositionChanged` reports the committed value: once per whole percent while dragging, and again
on release.

```csharp
compare.PositionChanged += (_, e) => readout.Text = $"Position {e.Position:0.#}%";
compare.Position = 75;
```

## API reference

<!-- api: ImageCompare -->

## Accessibility

- `Label` names the divider handle. It defaults to "Before and after".
- The handle is a focusable slider. Its value is the share of the after image, for example "40% after".
- Arrow keys move the divider 1%, Shift with an arrow moves it 10%, and PageUp and PageDown move it 10%.
- Home and End reveal the after or the before image in full, and a double-click on the handle returns it to the middle.
- The focus ring appears after a key is pressed, not when focus follows the pointer.

## Motion

- The divider follows the pointer one to one while you drag, gives resistance past the edges and stays where you
  release it. The handle stretches into a capsule while it is dragged.
- Captions fade as the divider reaches them. Keys that push past an edge give a short kick.
- Reduced motion places the divider and the handle directly, without springs or edge resistance. Off jumps to the
  end state at once.

## Related

- `Carousel` for browsing a set of pictures or slides rather than comparing two.
- `CardStack` for going through items one at a time and deciding on each.
- `Slider` for choosing a value on a track without any picture.
