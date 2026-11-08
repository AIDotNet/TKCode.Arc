---
title: Page viewer
summary: Zoom, pan and page through pictures or pages rendered on demand.
---

<Demo name="Overview" />

## When to use

`PageViewer` shows a set of pages and lets the reader move between them, zoom in and pan around. The pages are pictures
from a collection, or pages an `IPageSource` renders when they come into view, such as a scanned contract or the pages
of a report. Only the visible pages are drawn, so a document of ten thousand pages costs no more to scroll than a short
one.

- **Scanned documents in a records tool**: a clerk reads a scanned form page by page and zooms in on a signature.
- **Proofs in a publishing tool**: an editor flips through the pages of a layout at full size before it goes to print.
- **Reports rendered by a server**: an analyst scrolls through a long report whose pages arrive as images when they are
  needed.
- **Annotating pages**: a reviewer clicks a page to leave a mark, drawn by a `PageLayer` that stays on the page through
  zooming and scrolling.

## When not to use

- **A row of pictures to browse**: use `Carousel`, which moves between slides by dragging.
- **Two versions of one picture**: use `ImageCompare`, which compares them under a divider.
- **One picture in a layout**: use `ArcImage`. A page viewer brings a toolbar and a scrolling canvas that a single
  picture does not need.
- **Text that should reflow**: use `Markdown`. A page has a fixed layout, and a reader on a phone has to zoom and pan it.
- **Reading a PDF file**: use `PdfViewer` from `TKCode.Arc.Pdf`, which adds thumbnails, a table of contents, search, text
  selection, links and password prompts. For the pages alone, set `Pdf.Source` on a `PageViewer`.

## Usage

Give the viewer a height and a collection of pictures. `ItemsSource` takes `Bitmap` objects, `ImageSource` objects and
addresses as strings or `Uri` objects, and it follows the collection when it implements `INotifyCollectionChanged`.

```xml
<arc:PageViewer Height="600" FitMode="Page" />
```

```csharp
viewer.ItemsSource = new[]
{
    "https://example.com/scan-1.png",
    "https://example.com/scan-2.png"
};
viewer.CurrentPageChanged += (_, e) => status.Text = $"Page {e.NewIndex + 1}";
```

`PageViewer` derives from Avalonia's `TemplatedControl`. It draws its pages itself on one surface inside a scroll viewer,
so pages are not controls and there is nothing to template per page.

### Anatomy

1. **Toolbar**: the page number field and the page count, previous and next page, zoom out, the zoom menu, zoom in, and
   the layout and pan toggles. `ToolbarStart` and `ToolbarEnd` add your own content at the two ends, and
   `IsToolbarVisible` hides it. While there are no pages it shows only your content at the two ends.
2. **Canvas**: the surface behind the pages, in the muted surface color.
3. **Pages**: white paper with a resting shadow, in light and dark themes alike. A page without a bitmap shows a still
   ring until its bitmap arrives.
4. **Layers** (`Layers`): your own drawing and pointer input on top of each page.

### Layout

`LayoutMode` is `Continuous` by default, with all pages in one scrolling column. `Single` shows only the current page;
the toolbar, `NextPage`, `PreviousPage` and the page keys swap it.

<Demo name="SinglePage" />

`CurrentPageIndex` is the page at the middle of the viewport. It is two-way and starts from 0, and setting it jumps to the
page. `GoToPage` does the same and lets you turn the animation off.

### Fit and zoom

`FitMode` keeps the zoom fitted to the viewport and works it out again when the viewport changes. `Width`, the default,
fills the width with the widest page. `Height` fills the height with the tallest page, and `Page` keeps a whole page in
view. In the `Single` layout they measure the current page.

<Demo name="FitWidth" />

`Zoom` is 1 for a page at its own size, clamped to `MinZoom` and `MaxZoom`. A value set from outside turns `FitMode` to
`None`, as do the toolbar and the zoom keys. `ZoomIn` and `ZoomOut` move through the steps 10, 25, 50, 75, 100, 125, 150,
200, 300, 400, 600 and 800%.

```csharp
viewer.ZoomIn();
viewer.ResetZoom();
viewer.FitMode = PageFitMode.Page;
```

### Panning

`InteractionMode` says what a drag over the pages does. `Select`, the default, leaves it to the layers. `Pan` moves the
view. In any mode the middle mouse button and a drag with Space held down pan as well, and a finger pans through the scroll
viewer.

<Demo name="PanMode" />

### Rendering on demand

Implement `IPageSource` when pages should not all sit in memory. `RenderAsync` runs for the visible pages and one page on
each side, nearest to the middle first. It is called on the UI thread and may finish on any thread. Honor the cancellation
token, because a page that leaves the view is cancelled. `TargetSize` is the pixel size of the whole page, which is the
page size times the zoom times the screen scale.

<Demo name="OnDemand" />

```csharp
public sealed class ReportPages : IPageSource
{
    public int PageCount => 240;

    public event EventHandler? PagesChanged;

    public Size GetPageSize(int pageIndex) => new(612, 792);

    public async ValueTask<PageImage?> RenderAsync(PageRenderRequest request, CancellationToken cancellationToken)
    {
        var bitmap = await Task.Run(() => Draw(request), cancellationToken);
        return new PageImage(bitmap);
    }
}
```

- Bitmaps with `OwnsBitmap` set to true belong to the viewer, which disposes them after evicting them. `RenderCacheBytes`
  limits how many bytes it keeps (192 MB by default); what visible pages draw is never evicted.
- While the zoom moves the viewer asks for nothing and stretches what it has. 120 ms after the zoom rests it asks for a
  sharp bitmap and swaps it in.
- A page above 16 megapixels gets a capped bitmap for the whole page, and then a second request with `Region` set for the
  part around the viewport, 120 ms after scrolling rests. Draw only that rectangle of the target size.
- Returning null leaves the placeholder on the page, and so does throwing an exception. The viewer does not ask for that
  bitmap again until `PagesChanged` fires.
- Raise `PagesChanged` on the UI thread when the page count or a page size changes. The viewer drops its bitmaps and reads
  the sizes again.

### Layers

A `PageLayer` draws over each visible page, clipped to it, and takes pointer input. Layers draw in list order; pointer
events go from the last layer to the first, and `Handled` stops them. A handled press keeps the drag for the layer.

<Demo name="Marks" />

### Moving to a place

`ScrollIntoView` brings a part of a page on screen by the shortest way and does nothing when it is already visible, which
suits search results and link targets. `PageHitTest` turns a point of the control into a page and a point on that page.

```csharp
viewer.ScrollIntoView(4, new Rect(40, 300, 200, 24));

if (viewer.PageHitTest(e.GetPosition(viewer), out var page, out var point))
{
    status.Text = $"Page {page + 1} at {point.X:0}, {point.Y:0}";
}
```

### Deriving from it

`PageViewer` is not sealed. A derived viewer in another assembly reuses the template through a control theme with
`BasedOn="{StaticResource {x:Type arc:PageViewer}}"`, which is how `PdfViewer` does it, and fills these protected members.

| Member | Use |
|---|---|
| `LeadingPane` | A side panel to the left of the pages, under the toolbar |
| `ContentOverlay` | Content over the pages, such as a floating search bar |
| `OnViewerKeyDown` | Sees keys first; return true to take a key |
| `OnPageSourceChanged`, `OnPagesChanged` | The source was replaced, or its pages changed |
| `GetPageBitmap`, `OnPageBitmapReady`, `CancelPageBitmapRequests` | Bitmaps from the same cache for other views, such as thumbnails |
| `EffectivePageSource` | The source on show |

### Content guidelines

- Give the viewer an accessible name with `AutomationProperties.Name` when the document has one, such as "Lease
  agreement". Without one it reads "Page 3 of 12".
- Put a short document title in `ToolbarStart` when the page around the viewer does not already show it.

| Instead of | Write |
|---|---|
| Document | Lease agreement |
| Image 1 of 6 | Page 1 of 6 |
| Viewer | Scanned invoice 4471 |

### Best practices

**Do**

- Give the viewer a height, or a parent that does. In a parent that gives it endless height it grows to the full column of
  pages and draws them all.
- Report page sizes that match the bitmaps, so a page does not change shape when its bitmap arrives.
- Stop work when the cancellation token fires. A reader scrolling quickly leaves many pages behind.

**Avoid**

- Rendering at a fixed size. `TargetSize` is what the screen needs at this zoom, and a smaller bitmap looks soft.
- Disposing a bitmap you returned with `OwnsBitmap` set to true. The viewer may still be drawing it.
- Keeping the bitmap from `GetPageBitmap`. Ask again each time you draw, because it may have been evicted.

### Common questions

**Why does a page look soft for a moment after zooming?** The viewer stretches the bitmap it has while the zoom moves. The
sharp one replaces it 120 ms after the zoom rests.

**Why does the current page not change as I scroll a little?** It is the page at the middle of the viewport. A jump keeps its
page current until the view moves again, even when another page ends up at the middle, as with the last page of a
document.

**Why do pictures not get sharper at very high zoom?** A picture has only its own pixels, and `ImagePageSource` has no detail
layer. An `IPageSource` that can draw at any size stays sharp.

**Why are the pages white in the dark theme?** Pages are paper. The viewer does not invert their colors.

## API reference

<!-- api: PageViewer -->

`PageChangedEventArgs` is the event data of `CurrentPageChanged`:

| Member | Meaning |
|---|---|
| `OldIndex` | The page that was current. |
| `NewIndex` | The page that is current now. |

`PageViewer` also has these methods:

| Method | Meaning |
|---|---|
| `GoToPage(index, animate)` | Goes to a page, clamped to the range. |
| `NextPage()`, `PreviousPage()` | Go to the next or the previous page. |
| `ZoomIn()`, `ZoomOut()` | Go to the next or the previous zoom step about the viewport center. |
| `ResetZoom()` | Goes to 100% about the viewport center. |
| `ScrollIntoView(pageIndex, pageRegion, animate)` | Brings a part of a page on screen; nothing moves if it already is. |
| `PageHitTest(pointInViewer, out pageIndex, out pointInPage)` | Finds the page under a point of the control; false over the canvas or the toolbar. |

`IPageSource` is the source of pages on demand, and `ImagePageSource` is the one behind `ItemsSource`:

| Member | Meaning |
|---|---|
| `PageCount` | How many pages there are. |
| `GetPageSize(pageIndex)` | A page's size in page DIPs: at zoom 1 it shows at this size. |
| `RenderAsync(request, cancellationToken)` | Renders a page or a part of it; returns a `PageImage`, or null when it has no content for now. |
| `PagesChanged` | The page count or a page size changed; raised on the UI thread. |

`PageRenderRequest` has `PageIndex`, `TargetSize` (the pixels of the whole page) and `Region` (null for the whole page,
otherwise a rectangle of the target size). `PageImage` has `Bitmap` and `OwnsBitmap`.

`PageLayer` draws over the pages:

| Member | Meaning |
|---|---|
| `Render(context, page)` | Draws on one visible page. `page` has `PageIndex`, `PageBounds` and `Scale`. |
| `OnPointerPressed(e)`, `OnPointerMoved(e)`, `OnPointerReleased(e)` | Pointer input on a page. `e` has `PageIndex`, `PointInPage`, `Modifiers`, `ClickCount`, `PointerType` and `Handled`. A finger scrolls the view unless a layer handles its press, so check `PointerType` before taking a drag. |
| `OnPointerDragCancelled()` | A drag the layer held ended without a release: the capture was lost or the viewer left the tree. Undo what the drag started. |
| `GetCursor(pageIndex, pointInPage)` | The cursor at a point; null leaves it to the layers below. |
| `Invalidate()` | Asks for the layers to be drawn again. |

## Accessibility

- The viewport is a document for assistive technology. Its name is "Page 3 of 12" unless `AutomationProperties.Name` sets
  one, and the value pattern reads the same text. It updates when the current page changes.
- The viewport takes keyboard focus and shows a focus ring when it gets focus from the keyboard.
- Every toolbar button has a name and a tooltip in the current language (English, Simplified Chinese, Traditional Chinese).

### Keyboard

The keys act while the viewport has focus. The zoom keys work while the toolbar has focus too.

| Keys | Action |
|---|---|
| Arrow keys | Scroll 40 units |
| `PageUp` / `PageDown` | Scroll one screen; in the `Single` layout, turn the page |
| `Home` / `End` | Go to the first or the last page |
| `Ctrl` or `Cmd` + `=` / `-` / `0` | Zoom in, zoom out, or go to 100% |
| Hold `Space` and drag | Pan |
| `Enter` in the page number field | Go to that page, clamped to the range |

## Motion

- The toolbar and the zoom keys move the zoom on a snappy spring about the viewport center. `Zoom` holds the target at once.
- `GoToPage` and `ScrollIntoView` move the view on a smooth spring. `CurrentPageIndex` holds the target while it moves, and any
  scrolling by the reader stops the spring.
- The wheel and a pinch follow the hand with no animation. A sharper bitmap replaces the old one directly, so text does not
  flicker.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Toolbar or key zoom | Spring | Immediate | Immediate |
| `GoToPage`, `ScrollIntoView` | Spring | Immediate | Immediate |
| Wheel or pinch zoom | Follows the hand | Follows the hand | Follows the hand |
| Toolbar button background | Eases | Immediate | None |

## Responsive behavior

- The viewer has no size of its own: give it a height, and it fills the width it gets.
- Below 640 wide the canvas padding around the pages shrinks from 24 to 8, and `FitMode` gives the extra room to the page.
- Below 520 wide the toolbar keeps only the page number field, the page count and the zoom controls.
- On touch a finger scrolls and a pinch zooms about the fingers. Nothing depends on hover.

## Related

- `PdfViewer` for a PDF file, with a sidebar, search, selection and links.
- `Carousel` for a row of pictures or slides, one in view at a time.
- `ImageCompare` for two versions of one picture under a divider.
- `ArcImage` for one picture in a layout.

| Component | Choose it when |
|---|---|
| `PdfViewer` | The document is a PDF file |
| `Carousel` | The reader flips through slides by dragging |
| `ImageCompare` | Two versions of one picture are compared |
| `ArcImage` | A single picture sits in a layout |
| `Markdown` | The text should reflow to the width |
