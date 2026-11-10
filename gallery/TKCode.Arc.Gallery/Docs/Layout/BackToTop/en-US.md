---
title: Back to top
summary: A round button that appears once a long list, log or feed has scrolled past a threshold and smoothly scrolls it back to the top.
---

<Demo name="Preview" />

## When to use

`BackToTop` watches a scroll container and shows a small round button once the reader has scrolled further down than
`Threshold` (200 px by default). Pressing it scrolls the content smoothly back to the top; the button fades away again on
the way up.

- **Long lists and feeds** where the reader goes deep and wants the start again: search results, posts, notifications.
- **Logs and message streams** that grow while people read them.
- **Long settings or documentation pages** in a single scroll area.

## When not to use

- **Short content** that fits in one or two screens: a button adds nothing there. (It stays hidden anyway when the content
  cannot scroll.)
- **Jumping to sections**: use a table of contents or anchors rather than a single "top" button.
- **Chat threads** that should stay pinned to the newest message at the bottom: those scroll the other way.

## Usage

There are two ways to use it. Either put a `BackToTop` over the scroll container yourself and point `Target` at it, or set
`BackToTop.IsAttached` on the container and let the library place the button in a corner of its viewport.

`Target` can be a `ScrollViewer` (`ScrollArea` is one) or a control whose template holds one (`Timeline`, `Table`,
`TreeView`, or a `ListBox` your application's theme gives a scrolling template): the button uses the control itself when it
is a `ScrollViewer`, otherwise the first `ScrollViewer` inside it once its template is applied. Only the vertical offset counts: the button shows while the content
can scroll and is scrolled further than `Threshold`, and hides when the content gets shorter or the viewport taller.

### Over your own container

Put the button in the same `Grid` cell as the container, after it, and place it with the usual alignment and `Margin`. The
button does not scroll with the content because it is not inside it.

```xml
<Grid xmlns="https://github.com/avaloniaui"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <ScrollViewer x:Name="Log">
        <StackPanel />
    </ScrollViewer>
    <arc:BackToTop Target="{Binding #Log}"
                   HorizontalAlignment="Right"
                   VerticalAlignment="Bottom"
                   Margin="16" />
</Grid>
```

The Preview above does this with a request log. When the smooth scroll arrives at the top the button raises
`ScrolledToTop`, which the demo uses to update its caption; `ScrollToTop()` does the same as pressing the button.

### Attached to a container

`arc:BackToTop.IsAttached="True"` on the container adds the button for you. It goes into the container's adorner layer, so
it stays put while the content scrolls and does not change the container's layout. `BackToTop.Placement` picks the corner
of the viewport (`BottomRight` by default) and `BackToTop.Inset` the distance from its edges (16 px). Setting
`IsAttached` back to false, or removing the container from the window, removes the button. Here it is attached to a
`Timeline`, which keeps its `ScrollViewer` in its template.

<Demo name="Attached" />

### Threshold and placement

`Threshold` (or `BackToTop.Threshold` on a container) is how far the content must be scrolled before the button appears;
0 shows it as soon as the content leaves the top. Pick a corner that does not cover the content's own controls.

<Demo name="ThresholdAndPlacement" />

### Size and label

`Size` makes the circle 36 (`Small`), 44 (`Medium`, the default) or 50 (`Large`) pixels across. `Label` replaces the
built-in name and tooltip "Back to top", which otherwise follows the application's language.

```xml
<arc:BackToTop xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
               Size="Small"
               Label="Back to the newest entries" />
```

## API reference

<!-- api: BackToTop -->

## Accessibility

- The button's accessible name and tooltip are `Label`, or the localized "Back to top".
- Enter and Space press it, like any button; the focus ring shows for keyboard focus only.
- While hidden the button takes no pointer input and is not in the Tab order. If it had focus when it hides, focus stays
  where it is.
- Focus stays on the button after it scrolls the content back, so pressing it again does nothing until the reader scrolls
  down once more.

## Motion

- Appearing and hiding: under full motion the button fades in, grows from 0.86 to full size and rises 8 px with a snappy
  spring, and reverses on the way out. Reduced motion only fades. The first frame never animates.
- Scrolling back: under full motion the offset eases from where it is to the top in 480 ms (in-out); under reduced motion
  and Off it jumps there. If the reader scrolls with the wheel, the scroll bar or the keyboard while it is moving, it stops
  and leaves the offset where they put it, and `ScrolledToTop` is not raised.

## Responsive behavior

- The button follows the size of the viewport it watches: when the window gets taller and the content no longer scrolls,
  it hides.
- The attached button stays in its corner of the viewport as the container resizes.
- On narrow screens prefer `Size="Small"` and a smaller `Inset` so the button covers less of the content.

## Related

- `ScrollArea`: the scroll container with overlay scroll bars and edge fades, which works as a target.
- `CyclicPanel`: a list that loops without end and so has no top to go back to.
