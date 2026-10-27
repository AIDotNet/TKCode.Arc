---
title: Image compare
summary: Drag a divider across two images to see what changed.
---

<Demo name="BeforeAndAfter" />

## When to use

`ImageCompare` puts two versions of the same picture on one frame and lets the reader drag a divider across them. The
reader decides how far to reveal the change, so the component fits any place where the change itself is the point: a
retouch, a cut-out, a redesign or a visual test. It sits inside a page, a review panel or an editor, next to the
controls that produced the change.

- **Background removal on a marketplace listing**: a seller sees the original photo and the cut-out side by side on the
  same frame before the listing goes live. The divider shows whether the product edges were kept.
- **Retouch approval in a photo studio tool**: a client drags the divider across a portrait to compare the original
  skin and the retouched skin, then approves or asks for another pass.
- **Staging in a property listing tool**: an agent shows the empty room and the staged version of the same photo, with
  the captions "Empty" and "Staged" so buyers see what the furniture changes.
- **Visual regression review in a QA or design tool**: a reviewer compares the approved screenshot with the new render
  of a page and drags across the header to see whether the spacing moved.

## When not to use

- **Browsing more than two pictures**: use `Carousel`, which moves between slides without comparing them.
- **Deciding on each picture in a queue**: use `CardStack`, which asks for a keep or pass on each item.
- **Splitting live app content into two panes**: use `ResizablePanels`. The divider there resizes real content, not a
  picture.
- **Three or more versions of one picture**: use `Tabs` to switch between versions, since a divider compares only two
  sides.
- **Comparing numbers or records**: use `Table`. A picture cannot show the difference between two values.

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

`ImageCompare` derives from Avalonia's `TemplatedControl`, not from a selecting control, so it has no items. Its two
sides are content: `Before` and `After` take any control or drawing, and `BeforeSource` and `AfterSource` can draw a
picture over them. The frame is sized by its width and `AspectRatio`, and the divider is drawn over both sides. The
before side is clipped at the divider, so dragging moves a clip and never re-lays out the content.

### Anatomy

1. **Frame**: the picture area, sized by its width and `AspectRatio` (width divided by height, default 1.5, which is
   3:2). Its corners use the theme's panel radius.
2. **After side**: the result, which fills the whole frame. `After` is sized to cover the frame.
3. **Before side**: the original, clipped at the divider. `Position` is the divider's place as a percentage from the
   left, or from the top when `Orientation` is vertical. The default is 50.
4. **Divider line**: a 2 px line with a faint 3 px edge, so it stays visible over light and dark photographs. In light
   mode the line is the raised surface color, and in dark mode it is the foreground color.
5. **Handle** (`Label` names it): a 40 px circle on the divider with an arrow on each side. While it is dragged it
   stretches into a 56 by 36 capsule. Its hit area reaches 8 px beyond the circle, and within 42 px of a frame edge it
   slows down.
6. **Captions**: `BeforeLabel` in the start corner and `AfterLabel` in the end corner, each in a frosted pill 12 px
   from the frame edge, set in the extra-small type size. The caption fades as the divider reaches it.
7. **Pictures** (`BeforeSource`, `AfterSource`): optional pictures that cover each side. A picture that fails to load
   leaves the content underneath visible.

### Labels and frame

`BeforeLabel` and `AfterLabel` caption the two sides. The defaults are "Before" and "After". Each caption fades as the
divider reaches it, so the caption under the divider never covers the detail the reader is looking at. `ShowLabels`
hides both captions. `AspectRatio` sets the frame width divided by its height, and the default is 1.5 (3 / 2). Choose
the aspect of the photographs so that the picture is not cropped heavily on either side.

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

Use the vertical layout when the change runs from top to bottom, such as a wide banner that was regraded or a page
whose header changed. Changing `Orientation` at run time swings the divider a quarter turn while the reveal follows it.

### Pictures

`BeforeSource` and `AfterSource` draw a picture over the content of each side. The picture loads in the background and
fades in from a soft blur. If it fails, the content underneath shows instead, so a page with a missing image still
compares something sensible. Set `Before` and `After` to a placeholder of the same color and crop as the picture, so
the frame looks correct while the picture loads.

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

`Position` is the divider position in percent from the left, or from the top when vertical. It is two-way and defaults
to 50. `PositionChanged` reports the committed value: once per whole percent while dragging, and again on release. Keys,
taps and double-clicks commit a value too.

```csharp
compare.PositionChanged += (_, e) => readout.Text = $"Position {e.Position:0.#}%";
compare.Position = 75;
```

### Binding to a view model

In an editor or a review tool, the view model holds the two pictures and the reveal position, so a saved position
comes back when the reviewer returns to the item. The control has no command: it reports its position through
`Position`, which is two-way.

```csharp
public interface IPhotoService
{
    string OriginalUrl { get; }

    string CutoutUrl { get; }
}

public partial class ListingPhotoViewModel : ObservableObject
{
    public ListingPhotoViewModel(IPhotoService photos)
    {
        OriginalPhoto = ImageSource.Parse(photos.OriginalUrl);
        CutoutPhoto = ImageSource.Parse(photos.CutoutUrl);
    }

    public ImageSource OriginalPhoto { get; }

    public ImageSource CutoutPhoto { get; }

    [ObservableProperty]
    private double _revealPercent = 50;

    [ObservableProperty]
    private string _status = "Drag the divider to compare the two versions.";
}
```

```xml
<StackPanel Spacing="12">
    <arc:ImageCompare Label="Listing photo, original and cut-out"
                      BeforeLabel="Original"
                      AfterLabel="Background removed"
                      BeforeSource="{Binding OriginalPhoto}"
                      AfterSource="{Binding CutoutPhoto}"
                      Position="{Binding RevealPercent, Mode=TwoWay}" />
    <TextBlock Text="{Binding Status}" />
</StackPanel>
```

- `Position` is written to the view model as the divider crosses each whole percent while dragging and again on
  release, so the saved value follows the reviewer's last position without a separate save step.
- A value set from the view model moves the divider to that position with a spring. Reduced motion places it directly.
- `PositionChanged` fires many times during a drag. Write analytics or a saved value on release, or throttle the
  handler, rather than on every event.
- `AfterShare` is read-only and gives the share of the after side in whole percent. Bind it to show "40% after" next to
  the frame when the wording helps the reviewer.

### Alignment and cropping

Both sides cover the frame, so the divider compares what is at the same place in each picture. If one side is cropped
differently, the comparison measures the crop, not the change. Use the same source image size and aspect for both
sides, and set `AspectRatio` to match those photographs. A photograph that is 3:2 in a 1.78 frame loses its top and
bottom on both sides equally, which keeps the reveal aligned. Two different crops of one photograph do not align at
all, so export the second version from the same frame.

### Using motion with restraint

The divider follows the pointer one to one, and it springs when a value comes from outside a drag. Motion here helps
the reader see where the change is. It distracts when the divider moves by itself, so never animate `Position` in a
loop, and avoid the orientation swing as a decoration. Use the swing only when the reader needs to switch between a
side-by-side and a stacked view. The small kick at an edge is feedback for a key press that has nowhere to go, so keep
it for the keyboard; reduced motion removes it along with the springs.

### Example: background removal on a marketplace listing

A seller tool that removes the background from a product photo before publishing. The original and the cut-out share
the same frame and the same product position, so the divider shows whether the edges of the bottle were kept and
whether the shadow was lost. The captions name each side by the step that changed it, not by "Before" and "After", so
the reviewer knows what the second picture is. The status line tells the reviewer what to do next. The shapes stand in
for a photograph so that the screenshot is self-contained; in the app, bind the two photographs as shown above.

![A product photo with the divider over an original and a background-removed version, with captions for each side](images/CutoutCompare.png)

```xml
<StackPanel Width="560" Spacing="16">
    <TextBlock Text="Listing photo, background removed"
               FontSize="16"
               FontWeight="Medium"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:ImageCompare Label="Listing photo, original and cut-out"
                      BeforeLabel="Original"
                      AfterLabel="Background removed"
                      Position="50">
        <arc:ImageCompare.Before>
            <Grid Background="#CFC6B6">
                <Border Height="90"
                        VerticalAlignment="Bottom"
                        Background="#A89C88" />
                <StackPanel HorizontalAlignment="Center"
                            VerticalAlignment="Center"
                            Margin="0,0,0,40">
                    <Border Width="64"
                            Height="26"
                            CornerRadius="6"
                            Background="#1E2B33" />
                    <Border Width="150"
                            Height="200"
                            CornerRadius="24"
                            Background="#2F4858" />
                </StackPanel>
            </Grid>
        </arc:ImageCompare.Before>
        <arc:ImageCompare.After>
            <Grid Background="#F6F4F0">
                <StackPanel HorizontalAlignment="Center"
                            VerticalAlignment="Center"
                            Margin="0,0,0,40">
                    <Border Width="64"
                            Height="26"
                            CornerRadius="6"
                            Background="#1E2B33" />
                    <Border Width="150"
                            Height="200"
                            CornerRadius="24"
                            Background="#2F4858" />
                </StackPanel>
            </Grid>
        </arc:ImageCompare.After>
    </arc:ImageCompare>
    <TextBlock Text="Drag the divider to compare the two versions."
               FontSize="13"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
</StackPanel>
```

### Content guidelines

- **Label** (the handle's accessible name): name the picture pair, such as "Listing photo, original and cut-out". A
  generic name such as "Compare" tells a screen reader nothing about the content.
- **Before and after captions**: name the state of each side, such as "Original" and "Background removed", or "Empty"
  and "Staged". The captions are the only text a sighted reader sees on the frame, so they must say what changed.
- **Status copy**: one sentence about what the reviewer should look for, such as "Check the bottle edges and the
  shadow".
- Keep captions to one or two words, so they fit the corner chips at every frame width.

| Instead of | Write |
|---|---|
| Before / After | Original / Background removed |
| Compare | Original and retouched |
| Image 1 / Image 2 | Front view, original / Front view, cut-out |
| Drag me! | Drag the divider to compare. |

### Best practices

**Do**

- Name each side with the change it shows through `BeforeLabel` and `AfterLabel`. A reviewer who sees "Original" and
  "Background removed" knows the question to ask without reading the page.
- Give each picture an accessible name with `AutomationProperties.Name`. The captions are visual only, so a screen
  reader would otherwise hear two unnamed pictures.
- Export both sides from the same frame, with the same size and aspect, so the reveal lines up exactly.
- Start the divider at the place where the change is most visible, such as near an edge of the product, so the reader
  sees the difference without a drag.

**Avoid**

- Comparing pictures with different framing or cropping. The divider then measures the composition, not the change.
- Looping the divider or moving it on a timer. The reader loses control of a comparison that they need to study.
- Hiding both captions with `ShowLabels="False"` when the pictures do not say what they are. A blank frame gives the
  reader no way to tell which side is which.
- Using the component for more than two versions. A second divider is not provided, so the reader cannot compare three
  pictures on one frame.
- Writing a log entry for every `PositionChanged` event during a drag. Log on release or throttle the handler.

### Common questions

**Why does the divider start in the middle?** `Position` defaults to 50. Set it in XAML or bind it to a saved value to
start elsewhere; a value set after the control loads moves the divider with a spring.

**Which key shows the before side in full?** `Home` sets `Position` to 100, so the before side covers the whole frame.
`End` sets it to 0, which shows the after side in full. `PageUp` and `PageDown` move the divider 10%.

**Why do the captions disappear near the divider?** Each caption fades as the divider reaches it, so the caption does
not sit over the detail the reader is comparing. The caption returns when the divider moves away.

**What happens when a picture fails to load?** The content under `BeforeSource` or `AfterSource` shows instead. If you
set `Before` or `After` to a placeholder of the same color, the frame still reads as a comparison.

**How do I switch between side by side and stacked?** Set `Orientation` to `Vertical` or `Horizontal`. Changing it at
run time swings the divider a quarter turn, and the position carries over.

## API reference

<!-- api: ImageCompare -->

`ImageComparePositionChangedEventArgs` is the event data of `PositionChanged`:

| Member | Meaning |
|---|---|
| `Position` | The committed divider position, in percent from the left (or the top). |

## Accessibility

- `Label` names the divider handle. It defaults to "Before and after".
- The handle is a focusable slider. Its value is the share of the after image, for example "40% after".
- Arrow keys move the divider 1%, Shift with an arrow moves it 10%, and PageUp and PageDown move it 10%.
- Home and End reveal the before or the after image in full, and a double-click on the handle returns it to the middle.
- The focus ring appears after a key is pressed, not when focus follows the pointer.
- The caption chips are visual only. Give each picture its own `AutomationProperties.Name`, so the two sides are named
  for a screen reader.

### Keyboard

The keys act when the handle has focus. Tab to the handle first; the frame itself does not take the arrow keys.

| Keys | Action |
|---|---|
| `Left` / `Right` | Move the divider 1% in a horizontal comparison; in a vertical comparison they move it in the opposite direction |
| `Up` / `Down` | Move the divider 1% in either orientation |
| `Shift` + arrow | Move the divider 10% |
| `PageUp` / `PageDown` | Move the divider 10% |
| `Home` / `End` | Reveal the before side in full, or the after side in full |

A key pressed at either end of the range gives the divider a small kick outward, so the reader sees that the limit has
been reached.

## Motion

- The divider follows the pointer one to one while you drag, gives resistance past the edges and stays where you
  release it. The handle stretches into a capsule while it is dragged.
- Captions fade as the divider reaches them. Keys that push past an edge give a short kick.
- Reduced motion places the divider and the handle directly, without springs or edge resistance. Off jumps to the
  end state at once.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Drag release | Settles where it is released, carrying the release speed | Placed directly | Immediate |
| Key, tap or double-click move | Springs to the new position | Placed directly | Immediate |
| Position set from code or a binding | Springs to the new position | Placed directly | Immediate |
| Key at an edge | Small kick outward | No kick | No kick |
| Orientation change | Swings a quarter turn while the reveal follows | Jumps to the new orientation | Immediate |

## Related

- `Carousel` for browsing a set of pictures or slides rather than comparing two.
- `CardStack` for going through items one at a time and deciding on each.
- `Slider` for choosing a value on a track without any picture.

| Component | Choose it when |
|---|---|
| `Carousel` | The reader flips through several pictures or slides |
| `CardStack` | Each picture needs a keep or pass decision |
| `Tabs` | Three or more versions of one picture are switched between |
| `ResizablePanels` | Two panes of live app content are resized against each other |
| `Slider` | A value is chosen on a track, with no picture |
| `Table` | Two sets of numbers or records are compared |
