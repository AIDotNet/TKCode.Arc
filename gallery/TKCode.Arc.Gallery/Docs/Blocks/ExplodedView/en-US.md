---
title: Exploded view
summary: A screen that comes apart as you scroll: its layers tip back, separate with labels, take turns in focus with a caption each, and close into one screen again.
---

<Demo name="Preview" />

## When to use

`ExplodedView` is a section for a launch or marketing page that shows how a screen is built. A stack of equally sized
layers, one picture each, starts as one flat screen. As the reader scrolls, the stack tips back and turns, the layers
separate in depth and a label draws in beside each one. Then the layers take turns in focus: the others dim, the focused
one takes the accent and the caption under the stage explains it. At the end the layers close up and the screen stands up
again. A row of stop buttons jumps straight to the whole screen, to any layer, or to the screen put back together.

- **A design system or UI kit page**, where the layers are the tokens, the grid, the components and the overlays of one
  sample screen, and each caption says what that layer is responsible for.
- **A product launch**, walking through a redesigned screen from its background to its floating alerts, one claim per layer.
- **Hardware with a screen**, such as a watch or a car display, where the layers are the glass, the interface and the
  sensors behind them.
- **A case study**, showing how a team split a busy screen into calm layers with one job each.

## When not to use

- **Content people need to read or act on.** The stage is a picture: it takes no input and screen readers skip it (the
  layers are listed in text instead). Put real controls on the page itself.
- **A sequence of steps someone has to complete.** Use `Stepper` for progress through a task.
- **A set of slides or images to browse.** Use `Carousel`, which keeps one item in view and pages with buttons and swipes.
- **A long article with a few illustrations.** Scroll-driven sections ask for attention; one per page reads best.
- **More than eight layers.** The labels stack up on the right and the stops get crowded; split the story in two sections.

## Usage

Add one `ExplodedLayer` per layer, from the bottom up. Each layer's `Content` is any control, laid out at `LayerSize`
(280 × 620 by default, a phone screen). `Name` is the label's first line, the caption title and the stop button's name;
`Note` is the label's second line; `Body` is the caption shown while the layer is in focus. `StartCaption` and
`EndCaption` are the captions before the stack opens and after it closes.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ExplodedView Height="760"
                      Title="Made of layers"
                      Description="Every layer does one job, and together they stay quiet.">
        <arc:ExplodedView.StartCaption>
            <arc:ExplodedCaption Title="A single screen" Body="Scroll to lift it apart, layer by layer." />
        </arc:ExplodedView.StartCaption>
        <arc:ExplodedLayer Name="Base" Note="Two neutral tokens" Kind="Plate"
                           Body="One quiet base layer under everything else.">
            <Panel />
        </arc:ExplodedLayer>
        <arc:ExplodedLayer Name="Content" Note="Fixed-width numbers"
                           Body="The numbers, the chart and the list.">
            <TextBlock Margin="16,52" Text="38.6 km" FontSize="34" />
        </arc:ExplodedLayer>
    </arc:ExplodedView>
</UserControl>
```

`ExplodedView` derives from Avalonia's `TemplatedControl`. The scene is a function of `Progress`, from 0 (the whole
screen) to 1 (put back together), which follows the scroll position; `SmoothedProgress` is that value after a soft,
overdamped spring and is what the stage draws. `CurrentStop` is 0 for the whole screen, 1 to N for the layers and N + 1 for
the screen put back together, and `StopChanged` is raised whenever it changes. The control sets the pseudo-classes
`:exploded`, `:wide`, `:compact`, `:tall-stage`, `:framed` and `:region-focus` for styling.

### Anatomy

1. **Intro** (`Title`, `Description`): the title in the display face, from 28 to 52 px as the control widens, and one or two
   sentences under it.
2. **Stage**: the layers, drawn in perspective. A `Plate` layer is opaque with a 1 px border; a `Sheet` layer shows only
   its content and, while the stack is apart, a dashed outline; a `Grid` layer is a sheet whose content fades in with the
   separation, for guides that exist only in the exploded view. All layers share `LayerCornerRadius`.
3. **Labels**: one per layer on the right, a dot on the layer's right edge (`AnchorFromBottom` above its bottom), a 24 px
   leader, the name and the note. They draw in from the bottom layer up as the stack opens.
4. **Caption**: the current stop's title and body. Its area is as tall as the tallest caption, so the page never jumps.
5. **Stop buttons**: one 36 × 44 button per stop with a tick; the current tick is thicker and takes the accent.
6. **Frame** (`IsFramed`): a 1 px border, the surface radius and a muted fill. It is the scroll region in container mode.

### Scroll modes

`ScrollMode` decides where the progress comes from:

- `Container` (default): the control scrolls inside its own frame. The frame holds an empty track `Length` frames tall
  (4 by default) and the stage stays pinned while the track passes. Touch and the scroll keys scroll it natively and a
  wheel notch moves 100 px, as in a browser; at either end the frame keeps the wheel instead of scrolling the page. The
  frame's height comes from `Height`, or 88% of the window between 620 and 860 px.
- `Page`: the control is a track `Length` viewports tall inside the page's own `ScrollViewer`, and its stage stays pinned to
  the viewport while the track passes. Turn the frame off for this mode.
- `External`: nothing scrolls. Set `Progress` yourself, from a slider, a timeline or your own scroll position.

<Demo name="PageScroll" />

### Driving the progress

In external mode `Progress` is the only input; bind it or set it, and the stage follows on its spring. `GoToStop(n)`
glides to a stop in any mode, and `Reset()` returns to the whole screen. `GetStopName(n)` and `GetStopCaption(n)` give
the texts of a stop, for example to show them elsewhere on the page.

<Demo name="SliderProgress" />

```csharp
view.StopChanged += (_, e) =>
{
    // e.Stop: 0 whole screen, 1..N a layer, N + 1 put back together. e.Layer is null at the ends.
    analytics.Track("exploded-stop", e.Stop);
};
```

### Your own layers

Layers are plain controls, so any picture works: a card, a dashboard, a device. Change `LayerSize` to their design size
and `LayerCornerRadius` to their corner. The fit keeps the exploded stack and its labels inside the stage at any size.

<Demo name="CustomLayers" />

### Narrow containers

The layout follows the control's own width, not the window's. From 880 px the intro and the caption sit in a 380 px column
beside a full-height stage. From 560 to 879 px the intro is on top and the caption sits beside the stop buttons. Below
560 px the stop buttons share one row above the caption, the labels show names only, and when the stage is taller than wide
the stack turns less and spreads further (`TallMaxTilt`, `TallMaxTurn`, `TallLayerGap`) so the labels still fit.

<Demo name="Narrow" />

### Reduced motion

With reduced motion the stack does not tip or separate gradually: the two ends show the flat screen and every layer stop
shows the stack fully apart. Stops follow the scroll position at once, captions and colors switch without fading, and stop
buttons jump instead of gliding.

<Demo name="ReducedMotion" />

### Tuning the scene

`MaxTilt` (60°), `MaxTurn` (−40°) and `LayerGap` (136 px) set the fully exploded pose; `Perspective` (2400) sets how
strong the perspective is; `DimmedLayerOpacity` (0.5) sets how far the other layers fade while one is in focus. The
progress timeline is fixed: the stack tips over 0.04 to 0.22, separates over 0.08 to 0.28, the layer stops share 0.28 to
0.80 evenly, and the closing mirrors the opening.

## API reference

<!-- api: ExplodedView -->

<!-- api: ExplodedLayer -->

<!-- api: ExplodedCaption -->

## Accessibility

- In container mode the frame is a focusable scroll region named from `Title` with `RegionLabelFormat` ("… Scroll to take it
  apart, one layer at a time."). A keyboard focus ring shows 2 px outside the frame; pointer focus shows none.
- The stage and its labels are hidden from assistive technology. A hidden list named `LayerListLabel` reads every layer as
  "name: body", from the bottom up.
- The stop buttons are a group named `StopsLabel`. Each button is named after its stop (`AssembledText`, a layer's name,
  `ReassembledText`), and the current one reports `CurrentStopText` as its status. The current tick is thicker as well as
  colored, so it does not rely on color alone.
- The caption is announced politely whenever the stop changes.

### Keyboard

| Keys | Where | Action |
|---|---|---|
| `ArrowUp` / `ArrowDown` | Frame | Scroll 40 px |
| `PageUp` / `PageDown`, `Shift+Space` / `Space` | Frame | Scroll the frame's height less 40 px |
| `Home` / `End` | Frame | Scroll to the start or the end |
| `ArrowLeft` / `ArrowRight` | Frame | Nothing |
| `Tab` | Frame, stop buttons | Move from the frame to each stop button in turn |
| `Enter` or `Space` | Stop button | Scroll to that stop; focus stays on the button |

## Motion

- The stage follows the progress on an overdamped spring (stiffness 330, damping 60): it starts within a frame of the
  scroll, reaches half way in about 130 ms and settles in about 750 ms, with no overshoot. Reversing mid-way keeps the
  speed, so scrolling back and forth stays smooth.
- Opening, the stack tips back and turns first, then the layers separate and the labels draw in from the bottom up; closing,
  the layers join first and the labels disappear from the top down, then the screen stands up.
- When the stop changes, the old caption leaves upwards in 180 ms while the new one rises 10 px into place in 480 ms. The
  layers' dimming eases over 480 ms; outlines, label colors and ticks change color over 240 ms; the new tick thickens on
  a springy 580 ms curve. A plate's border and an outline's dash switch at once.
- A stop button glides the scroll position there, longer for longer distances (at most 700 ms).
- Nothing animates at rest.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Scroll | The stage follows on its spring | Flat at the ends, fully apart at every layer | Same as reduced |
| Stop changes | Caption crossfade, dimming and colors ease | Everything switches at once | Immediate |
| Stop button | Glides to the stop | Jumps | Jumps |

## Related

- `Stepper` for progress through the steps of a task.
- `Carousel` for browsing slides with buttons and swipes.
- `ScrollArea` for a plain scrolling region with Arc's scroll bars.
- `SegmentedControl` and `Avatar`, which the sample screen is built from.

| Component | Choose it when |
|---|---|
| `Stepper` | People complete a task step by step |
| `Carousel` | Several pictures or slides are browsed one at a time |
| `Tabs` | Each part of the story is a view of its own, chosen by name |
| `ScrollArea` | The content just needs to scroll |
