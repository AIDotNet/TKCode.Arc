---
title: PDF viewer
summary: View a PDF with thumbnails, a table of contents, search, text selection, links and password prompts, rendered by PDFium on desktop and pdf.js in a browser.
---

<Demo name="Preview" />

## When to use

`PdfViewer` opens a PDF file inside your app. It adds a sidebar with thumbnails and the table of contents, search, text
selection, links and password prompts to `PageViewer`, and it shows loading and failure states of its own.

- **Contracts and invoices in a business tool**: a clerk reads a document, searches it for a name and copies a figure.
- **Manuals and reports in a desktop app**: a reader jumps through the table of contents and follows links between sections.
- **Generated files in a web app**: a user checks a PDF your server made without leaving the page.
- **Protected documents**: a reader types the password in the viewer, or your app supplies it.

## When not to use

- **Editing a PDF**: annotations, form filling, digital signatures, printing, view rotation and two-page spreads are not part of this version.
- **Scripts and XFA forms**: scripts in a document never run, and XFA forms are not supported.
- **Pictures or pages you render yourself**: use `PageViewer`, in the main package, with no native library.
- **Text that should reflow**: use `Markdown`. A PDF page has a fixed layout, and a reader on a phone has to zoom and pan it.

## Usage

Add `TKCode.Arc.Pdf` and one backend, register the package, and turn the backend on at startup:

```csharp
AppBuilder.Configure<App>()
    .UseArc(arc => arc.UseArcControls().UseArcPdf());

ArcPdfNative.Use();              // desktop: TKCode.Arc.Pdf.Native
await ArcPdfBrowser.UseAsync();  // browser: TKCode.Arc.Pdf.Browser
```

Set `Source` to an absolute address or a path. `http`, `https` and `avares` addresses work, and so does a file path. From
code, `PdfSource` also opens bytes and streams.

```xml
<arc:PdfViewer Source="avares://MyApp/Assets/report.pdf" Height="600" />
```

```csharp
viewer.Source = PdfSource.FromFile(path);
viewer.Source = PdfSource.FromBytes(bytes);
viewer.Source = PdfSource.FromStream(() => File.OpenRead(path));
viewer.DocumentOpened += (_, e) => status.Text = e.Document.Info.Title;
viewer.DocumentFailed += (_, e) => status.Text = e.Error.Kind.ToString();
```

`PdfViewer` derives from `PageViewer`, so zoom, layout, panning, `CurrentPageIndex` and the toolbar work the same way.
`Status` says where it is: `Empty`, `Loading`, `Ready`, `PasswordRequired` or `Failed`. While no document is on show, the
toolbar hides its page, zoom, sidebar and search controls, and an open sidebar or search bar comes back with the next document.

`Document` shows a `PdfDocument` you opened yourself. It stays yours and the viewer never disposes it; `Source` wins when
both are set. `SimulatedPdfDocument` gives generated pages for tests and design previews.

### Anatomy

1. **Toolbar**: the toolbar of `PageViewer`, with the sidebar button at the start and the search button at the end.
2. **Sidebar**: thumbnails and the table of contents, in two tabs. A document with no table of contents shows thumbnails only.
3. **Pages**: white paper, in light and dark themes alike. The viewer does not invert their colors.
4. **Search bar**: floats at the top right, with the count, previous and next, match case, whole word and close.
5. **State cover**: the spinner while opening, the reason after a failure, and the password form.

### Sidebar

`IsSidebarOpen` shows the sidebar and `SidebarMode` picks `Thumbnails` or `Outline`; both bind two-way. Thumbnails render on
demand from the same cache as the pages, and clicking a table of contents entry goes to its place.

<Demo name="Sidebar" />

### Search

`Ctrl` or `Cmd` + `F` opens the search bar. `SearchText` searches 300 ms after typing stops, from the current page on, and
wraps around. Each page reports as it finishes, so `SearchMatchCount` grows while `IsSearching` is true; the viewer goes to
the first match as soon as it has one. `IsMatchCase` and `IsWholeWord` narrow the search, and Chinese, Japanese and Korean
text has no whole-word matching. `SearchNext`, `SearchPrevious` and a two-way `CurrentSearchMatch` move between the matches.

<Demo name="Search" />

### Selecting text

Drag over text to select it, double-click for a word and triple-click for a line. Shift and click extends the selection,
which can run across pages. `SelectedText` has the text; `Ctrl` or `Cmd` + `C` and the context menu copy it, and
`CopySelection`, `SelectAll` and `ClearSelection` do the same from code. `IsTextSelectionEnabled` turns all of this off.

Text selection has no touch drag in this version: a finger scrolls the page, and a double tap selects a word.

### Links

Clicking a link raises `LinkActivated`; set `Handled` to do something else. Otherwise a link inside the document goes to its
page. An external link opens only for `http`, `https` and `mailto`; any other scheme, such as `file:` or `javascript:`, never
opens, because the document is untrusted. `ExternalLinkBehavior="None"` leaves external links to your handler.

```csharp
viewer.LinkActivated += (_, e) =>
{
    if (e.Link.Uri is { } address)
    {
        e.Handled = true;
        ConfirmAndOpen(address);
    }
};
```

### Locked documents

A document with a password shows a form, and a wrong password shakes it and says so. To answer from your app, handle
`PasswordRequested`: set `Password`, or `Cancel` to give up, and take a deferral to answer later. `IsRetry` tells you the last
password was wrong. The viewer keeps a password only while opening and never logs it.

The password of this sample is `arc-sample`:

<Demo name="Locked" />

```csharp
viewer.PasswordRequested += async (_, e) =>
{
    using var deferral = e.GetDeferral();
    e.Password = await vault.GetPasswordAsync();
};
```

### Pages only

`Pdf.Source` opens a PDF in a plain `PageViewer`, for pages and zoom with none of the tools above. There is no password form
or failure cover: `Pdf.Error` says why a document did not open, a needed password included.

<Demo name="PagesOnly" />

### Platforms and packages

| Package | Contents | Size | Entry point |
|---|---|---|---|
| `TKCode.Arc.Pdf` | `PdfViewer`, `Pdf.Source`, the backend interface | 0.3 MB | `UseArcPdf()` |
| `TKCode.Arc.Pdf.Native` | PDFium for win, linux and osx on x64 and arm64 | 22 MB package; about 7.5 MB in an app for one platform | `ArcPdfNative.Use()` |
| `TKCode.Arc.Pdf.Browser` | pdf.js, its worker and data files | 2 MB package; 4 MB on the server, fetched as needed | `await ArcPdfBrowser.UseAsync()` |

Both entry points return false and leave the backend as it was when they cannot load. Without a backend, a document fails
with `BackendUnavailable` and the viewer says which call is missing; it never shows made-up pages.

`ArcPdfBrowser.UseAsync` imports pdf.js from `_content/TKCode.Arc.Pdf.Browser/pdfjs/`, so the host has to serve the package's
static web assets, with a JavaScript type for `.mjs` files. Pass an address to serve them from somewhere else.

Limits to plan for:

- **Fonts**: a document without embedded Chinese, Japanese or Korean fonts depends on the fonts of the machine. On a Linux machine with no fonts, that text renders blank.
- **Fonts in a browser**: the Liberation fonts are not shipped in the browser package because of their license, so a document with non-embedded Helvetica or Arial falls back to a local font or the browser's sans-serif, with different widths.
- **Touch**: `IsTextSelectionEnabled` has no touch drag selection in this version.
- **Web addresses in text**: an address written as plain text counts as a link only with PDFium. In a browser only real link annotations are links.
- **Selection in a browser**: pdf.js reports runs of text, and the viewer splits each run in proportion to its width, so a selection can sit a little off the glyphs.
- **Untrusted files**: PDFium parses files in your process. Keep it up to date, and do not rely on it as a sandbox for files from strangers.
- **Leaving the tree**: a viewer that leaves the visual tree closes the document it opened from `Source` and opens it again when it comes back, and an encrypted one asks for its password again. Moving the viewer to another parent in one step keeps the document open.

### Content guidelines

- Give the viewer an accessible name with `AutomationProperties.Name` when your page knows the document's name.
- Give `PdfSource` a `DisplayName` when a document may have no title of its own. `Title` falls back to it.
- Say what to do next when `DocumentFailed` fires in a flow the reader cannot leave.

| Instead of | Write |
|---|---|
| Error | This file could not be opened |
| PDF document | Lease agreement |

### Best practices

**Do**

- Give the viewer a height, or a parent that does. In a parent with endless height it grows to the full column of pages.
- Turn the backend on before the first document opens.
- Handle `LinkActivated` when a link in a document should need the reader's confirmation.

**Avoid**

- Disposing a document the viewer opened from `Source`. It belongs to the viewer.
- Keeping a password in your view model after `PasswordRequested`.
- Treating the viewer as a security boundary. It checks link schemes and runs no scripts, but the file is still parsed in your process.

### Common questions

**Why does the viewer say no renderer is set up?** `ArcPdf.Backend` is still the default. Call `ArcPdfNative.Use()` or `await ArcPdfBrowser.UseAsync()`, and check that the package for the platform is referenced.

**Why does Chinese text show as blank?** The document does not embed its fonts and the machine has none that fit. See the limits above.

**Why does it ask for the password again?** The viewer left the visual tree, which closes the document and forgets the password.

**Why are the pages white in the dark theme?** Pages are paper. The viewer does not invert their colors.

## API reference

<!-- api: PdfViewer -->

The attached properties of `Pdf`, for a plain `PageViewer`:

<!-- api: Pdf -->

`PdfViewer` also has these methods:

| Method | Meaning |
|---|---|
| `SearchNext()`, `SearchPrevious()` | Go to the next or the previous match, around the ends. |
| `ClearSearch()` | Ends the search and empties `SearchText`. |
| `SelectAll()`, `ClearSelection()`, `CopySelection()` | Select the current page, clear the selection, or copy it. |

`PdfSource` says where a document comes from:

| Member | Meaning |
|---|---|
| `FromFile(path)` | A file. Not for a browser, which has no file system to speak of. |
| `FromBytes(data)` | Bytes in memory. |
| `FromStream(open)` | A seekable stream; `open` is called once per opening. |
| `FromUri(uri)` | `http` or `https`, or `avares` for an app resource. |
| `Parse(text)` | An absolute address or a path, which is what XAML uses. |
| `DisplayName` | The name to show when the document has no title. |

`PdfDocument` is an open document: an `IPageSource` with `Info`, `GetPageLabel`, `GetOutlineAsync`, `GetPageTextAsync` and
`GetLinksAsync`. `ArcPdf.Backend` is the `IPdfBackend` that opens documents; `PdfException.Kind` is a `PdfErrorKind`:
`BackendUnavailable`, `NotFound`, `Network`, `Corrupt`, `Unsupported`, `PasswordRequired` or `Aborted`.

## Accessibility

- The viewport is a document for assistive technology, named "Page 3 of 12" unless `AutomationProperties.Name` sets a name. Screen readers can read the current page's text through its help text; there is no full text pattern in this version.
- Every toolbar and search button has a name and a tooltip in the current language (English, Simplified Chinese, Traditional Chinese).
- The number of search results is announced politely.
- The password form takes focus when it appears, and Enter and Escape answer it.
- While no document is on show, the toolbar's controls are hidden and out of the tab order.

### Keyboard

The keys act while the viewport has focus.

| Keys | Action |
|---|---|
| Arrow keys | Scroll 40 units |
| `PageUp` / `PageDown` | Scroll one screen; in the `Single` layout, turn the page |
| `Home` / `End` | Go to the first or the last page |
| `Ctrl` or `Cmd` + `=` / `-` / `0` | Zoom in, zoom out, or go to 100% |
| Hold `Space` and drag | Pan |
| `Ctrl` or `Cmd` + `F` | Open search and focus it |
| `Enter` / `Shift` + `Enter`, `F3` / `Shift` + `F3` | Next or previous match |
| `Escape` | Close search; otherwise clear the selection |
| `Ctrl` or `Cmd` + `C` / `A` | Copy, or select the current page |
| `Enter` in the page number field | Go to that page, clamped to the range |

## Motion

- The sidebar opens by animating its width on a smooth spring; in a narrow viewer it slides in over the pages.
- The search bar fades in and drops into place.
- A wrong password shakes the form.
- Zoom and page moves work as in `PageViewer`. A sharper bitmap replaces the old one directly, so text does not flicker.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Sidebar opens or closes | Spring or slide | Fades | Immediate |
| Search bar opens or closes | Fades and drops | Fades | Immediate |
| Wrong password | Shakes | Error text only | Error text only |
| Toolbar zoom, `GoToPage` | Spring | Immediate | Immediate |

## Responsive behavior

- The viewer has no size of its own: give it a height, and it fills the width it gets.
- Below 640 wide the sidebar floats over the pages instead of taking width from them.
- Below 520 wide the toolbar keeps the page number field, the page count, the zoom controls and the two buttons at its ends.
- On touch a finger scrolls, a pinch zooms about the fingers, and a double tap selects a word. Nothing depends on hover.

## Related

- `PageViewer` for pictures or pages you render yourself.
- `VideoPlayer` for the same kind of package with a separate backend per platform.
- `Markdown` for documents that should reflow.

| Component | Choose it when |
|---|---|
| `PageViewer` | The pages are pictures, or your code renders them |
| `Markdown` | The text should reflow to the width |
| `ArcImage` | A single picture sits in a layout |
