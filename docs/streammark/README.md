# StreamMark

High-performance streaming Markdown rendering for [Avalonia](https://avaloniaui.net/), built for AI chat clients.

- **Streaming first**: stable blocks are frozen and parsed once; only the tail is re-parsed. Unfinished syntax (`**bold`, `` `code``, `[link](ht`, `$$`) is healed while streaming (C# port of vercel's remend).
- **Native, no WebView**: self-drawn text blocks, virtualized transcript, cross-message selection.
- **Scientific content**: KaTeX-grade LaTeX via the [RaTeX](https://github.com/erweixin/RaTeX) engine, Mermaid diagrams, TextMate syntax highlighting.
- **NativeAOT-safe**: zero trim/AOT warnings is a CI gate.

> Status: part of [TKCode.Arc](../../README.md) since 1.3.0 (moved in from its own repository after 0.1.2). Its packages ship with every Arc release and share `ArcVersion`. See [architecture.md](architecture.md); maintainers start at [agent-guide.md](agent-guide.md).

## Packages

Published with TKCode.Arc. The namespaces stay `StreamMark.*`, so `using StreamMark.Avalonia;` does not change; the assemblies are named after the packages.

| Package | Purpose |
|---|---|
| `TKCode.Arc.StreamMark` | The streaming engine (segmenter, remend, block model, snapshots/diff, pacer; `StreamMark.Core.*`) and the Avalonia controls and default theme (`StreamMark.Avalonia.*`) |
| `TKCode.Arc.StreamMark.TextMate` | Incremental syntax highlighting (`StreamMark.Highlighting.TextMate`) |
| `TKCode.Arc.StreamMark.RaTeX` | LaTeX math via RaTeX (ships the native library for win/linux/osx × x64/arm64) |
| `TKCode.Arc.StreamMark.Mermaid` | Mermaid diagrams |

`TKCode.Arc.StreamMark` depends on Avalonia and Markdig only: it does not need the Arc controls. An Arc app that wants Arc's look uses `TKCode.Arc.Markdown` (`UseArcMarkdown()`), whose `Markdown` and `ChatThread` controls are built on it and map the `Md.*` resources onto Arc's tokens.

Coming from the old `TKCode.StreamMark.*` packages: replace `TKCode.StreamMark.Core` and `TKCode.StreamMark.Avalonia` with `TKCode.Arc.StreamMark`, `TKCode.StreamMark.Highlighting.TextMate` with `TKCode.Arc.StreamMark.TextMate`, and `.RaTeX` / `.Mermaid` with `TKCode.Arc.StreamMark.RaTeX` / `.Mermaid`. XAML that names the assembly changes `assembly=StreamMark.Avalonia` to `assembly=TKCode.Arc.StreamMark`. `TKCode.StreamMark.Theme.AtomUI` has no successor: map the `Md.*` keys with `MarkdownResourceAlias` in the host (see [Theming](#theming)).

## Install

Requires Avalonia 12. The smallest useful set is the base package; add the others for highlighting, math or diagrams.

```bash
dotnet add package TKCode.Arc.StreamMark
dotnet add package TKCode.Arc.StreamMark.TextMate
dotnet add package TKCode.Arc.StreamMark.RaTeX
dotnet add package TKCode.Arc.StreamMark.Mermaid
```

The RaTeX package carries `ratex_ffi` under `runtimes/<rid>/native/`, and the host picks the library for its own RID; nothing has to be copied by hand.

Minimal app:

```csharp
using StreamMark.Avalonia;
using StreamMark.Avalonia.Extensions;
using StreamMark.Highlighting.TextMate;
using StreamMark.Mermaid;
using StreamMark.RaTeX;

public class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new StreamMarkTheme());
        // a host theme that maps Md.* goes after it
    }
}

// in the window
var services = new MarkdownServices
{
    Plugins = [new RaTeXPlugin(), new TextMatePlugin(), new MermaidPlugin()],
};
MarkdownServices.SetServices(this, services);
Content = new MarkdownView { Markdown = "# Hello **StreamMark**\n\n$E = mc^2$" };
```

## Avalonia usage

```csharp
// App: default look (a host theme that maps Md.* goes after it)
Styles.Add(new StreamMarkTheme());

// Window: rendering extensions shared by every Markdown view below it
var services = new MarkdownServices
{
    Plugins = [new RaTeXPlugin(), new TextMatePlugin(), new MermaidPlugin()],
};
MarkdownServices.SetServices(window, services);
// create documents from the services so plugin tags are parsed (same as passing services.Profile)
var entry = services.CreateEntry(markdown);

// Long chat transcript (virtualized, cross-message selection, stick-to-bottom)
var transcript = new TranscriptView { MaxContentWidth = 760 };
transcript.Entries.Add(new ContentEntry(userBubble));          // any control / data-templated item
var reply = services.CreateStream();                             // smoothing + fade-in + caret
transcript.Entries.Add(new MarkdownEntry(reply));
reply.Push(token);  /* … */  reply.Complete();

// Single document
var view = new MarkdownView { Markdown = "# Hello" };
```

Mermaid diagrams follow the host's light/dark theme and by default reproduce mermaid.js's own themes (`dark` / `default`).
Pick another look with `new MermaidPlugin(new MermaidDiagramRenderer { Theme = MermaidTheme.MermaidJsNeutral })`
(grayscale light theme) or `MermaidTheme.Native` (Mermaider's own palette derived from the host colors).

## Theming

Every color, font and radius is an `Md.*` resource (`MarkdownResourceKeys`), with light and dark defaults in
`StreamMarkTheme`. A host maps them onto its own tokens with `MarkdownResourceAlias`, not with brushes or
`DynamicResource`: the alias is resolved from the Markdown control itself, so tokens registered on the window or theme
scope are found (a brush in a resource dictionary looks up from the dictionary and comes out transparent).

```csharp
view.Resources[MarkdownResourceKeys.Foreground] = new MarkdownResourceAlias("MyApp.TextBrush");
view.Resources[MarkdownResourceKeys.CodeBackground] = new MarkdownResourceAlias("MyApp.SurfaceBrush");
```

A host theme added as `Styles` goes after `StreamMarkTheme`. Arc's own mapping is `Markdown.s_aliases` in
`src/TKCode.Arc.Markdown/Content/Markdown/Markdown.cs`.

## Components (whitelisted HTML tags)

Built in: `<details>/<summary>`, `<think>`, `<kbd>`, `<sub>`, `<sup>`, `<mark>`, `<u>`, `<cite href>`, `<Citation id="1"/>`, `<img src width height>`.
Plugins add their own: `builder.AddBlockComponent("callout", _ => new CalloutView())`, `builder.AddInlineComponent("badge", renderer)`.

## Raw HTML

Raw HTML in Markdown (README style) is converted into the block model, so it supports selection, copying, theming and streaming. The parser is a tolerant one written for this library.

- **Block elements**: `div p center section article header footer main aside figure h1–h6 table thead tbody tfoot tr th td ul ol li dl dt dd blockquote pre hr details summary`.
- **Inline elements**: `a img picture br strong b em i u ins s del strike code kbd samp tt var sub sup mark small big span font abbr cite dfn q time`.
- **Attributes**: `align`, `width`, `height`, `bgcolor`, and `<font color size>`.
- **`style` subset**: `text-align`, `color`, `background(-color)`, `font-size`, `font-weight`, `font-style`, `margin*`, `padding*`, `border`, `border-radius`, `width`, `max-width`, `display:none`. Values are clamped so content cannot break the layout.
- **HTML around Markdown**: HTML separated from Markdown by blank lines is merged into one box, for example `<div align="center">`, a blank line, Markdown, a blank line, `</div>`.
- **Unsupported elements**: HTML containing an unknown element (including `script`, `style`, `iframe`) is shown as source and never executed. Comments are hidden.
- **Author colours**: `MarkdownServices.HtmlColors` controls them. The default is `Readable`: author colours are used, but the theme colour replaces any colour without enough contrast against its background. `Exact` and `Ignore` are also available.

## Chat features

- **Link policy**: `MarkdownServices.Links` (a `LinkPolicy`).
  - By default only `https`, `http` and `mailto` links are allowed. Relative links and `#anchors` are passed to the host.
  - Blocked links (`javascript:`, `file:`, `data:`, unknown schemes) render as plain text.
  - `LinkClickedEventArgs.RequiresConfirmation` is set when `RequireConfirmation` is on, or when the link text looks like a different URL (for example `[https://github.com](https://evil.example)`).
  - `TextSegmentView.LinkHoveredEvent` reports the real URL, for showing in a status bar.
- **Citations**: set `MarkdownServices.Sources` (an `ISourceProvider`, for example `DictionarySourceProvider`).
  - Hovering `<Citation id="1"/>` shows a source card with the title, site and snippet; clicking it opens the URL.
  - An unhandled click on a citation or a `<cite href>` raises `LinkClicked`.
- **Code blocks**: `MarkdownServices.CodeBlocks` (a `CodeBlockOptions`).
  - `ShowLineNumbers`: numbering starts at `#L10` when the fence info gives a start line.
  - Collapsing: `CollapseAfterLines` (default 40) and `CollapsedLines` (default 20). Blocks that were streamed are never collapsed.
  - `ExpandText` and `CollapseText` set the labels.
  - Download: `ShowDownload` toggles the button. `Save` overrides the default save dialog. The suggested file name comes from the fence path or the language.
- **Diff**: ```` ```diff ```` and ```` ```patch ```` blocks support three views:
  - **Markdown** (default): looks like GitHub's ```` ```diff ````. The text is shown as is: `+` lines get a green background, `-` lines red, `@@` lines light blue, and file headers are grey. There are no line numbers or file bars. Syntax colours and intra-line change highlights are kept.
  - **Split**: the header button toggles it. Old is on the left and new on the right, with line numbers, per-file headers with `+N −M` stats, and paired changes. The choice is kept per block. Split falls back to the default view when the width is below `DiffSplitMinWidth` (600).
  - **Unified**: line numbers in two columns, chosen with `CodeBlocks.DiffMode = DiffViewMode.Unified`.
  - Diffs render while streaming. Copying gives the original diff text. The tooltip label comes from `CodeBlocks.DiffModeText`.

## Navigation, search and interaction

- **Outline and jumps**:
  - `TranscriptView.GetOutline()` and `MarkdownView.GetOutline()` return the headings as `OutlineItem(Target, Level, Text, Id)`. Pass `Target` to `ScrollToPoint`.
  - `TranscriptView.ScrollToPoint(TextPoint)` parses deferred entries if needed and places the target at about 30% from the top. `MarkdownView.ScrollToPoint` relies on the outer `ScrollViewer`.
  - In-document links (`#heading-id`, footnote references `#fn:…`) jump inside the document and are not passed to the host.
- **Source positions and reading anchors** (`MarkdownView`, block-level precision):
  - Offsets are UTF-16 code units, the same as `string` indexes and `SourceRange`: a CJK character counts as 1, a surrogate pair as 2, and `\r\n` as 2 (the text is not normalised). `SourceText.ToLocation` and `SourceText.ToOffset` convert to and from 0-based line and column with the same rules.
  - `ScrollToSourceOffset(offset)` scrolls to the deepest block that contains the offset: a list item, a paragraph inside a quote, or a code block as a whole. It returns a `SourceNavigationResult`; when nothing moved, the status says why: `NoDocument` (no snapshot applied yet), `OutOfRange`, or `LayoutPending` (try again next frame).
  - `GetSourceRange(TextPoint)` and `GetSelectionSourceRange()` map a rendered point or the selection back to the source range of the blocks it covers. `TextPoint.Offset` is an offset inside the segment's plain text, not a source offset, so text with emphasis or links never lines up with the source.
  - `CaptureAnchor()` and `RestoreAnchor(anchor)` save and restore the reading position: the character at the top of the outer `ScrollViewer` plus its pixel displacement. A width, font or theme change keeps the place. An anchor is tied to `DocumentId`, which changes whenever `Markdown`, `Document` or `Stream` is assigned; an anchor from another document returns `WrongDocument`. If the anchored block is gone, the view scrolls to the next block (`Approximate`).
- **Editor previews** (`MarkdownView.RefreshMarkdown(text)`): replaces the whole text without clearing the view, for a preview next to a source editor where any part of the text can change. Assigning `Markdown` still means "a new document" and resets selection and block state.
  - The old content stays on screen until the new text has been parsed in the background. Updates that arrive in quick succession (A, B, C) never show A or B once C is accepted.
  - The new and old snapshots are aligned by content. Blocks that did not change, or only moved because text was added or removed before them, are updated in place: their views, layout and block state (`BlockStateStore`, via `Remap`) are kept.
  - The selection and the reading position move with them. An endpoint inside a rewritten block stays on the paired new block and is clamped to the new text. An endpoint inside a deleted block moves to the start of the next surviving block (or the end of the document); if both ends land on the same point the selection is cleared.
  - `DocumentId` does not change, so reading anchors captured earlier stay valid. `RefreshMarkdown` throws when the text comes from `Document` or `Stream`.
- **Task checkboxes** (`MarkdownView`, off by default): task list checkboxes are read-only. Set `IsTaskInteractionEnabled = true` and a click on the box (or Space / Enter once it has focus) raises `TaskToggleRequested` instead of changing anything. StreamMark never edits the source or the snapshot: the host decides, edits its own text (with its own undo grouping and saving), and assigns the new text. A rejected request changes nothing on screen.
  - `TaskToggleRequestedEventArgs` carries `Marker` (the source range of `[ ]` / `[x]`), `IsChecked`, `RequestedChecked`, `DocumentId` and `Version`. `ApplyTo(source)` replaces only the character inside the brackets, so indentation and CRLF are kept; it returns `null` if the source no longer matches.
  - Requests are raised only once the document is complete, not while a stream is still running. If the host handles a request asynchronously, `view.IsCurrent(request)` tells whether the document and snapshot are still the ones the request was made on.
  - Clicking the task text still selects text. The list item exposes a `CheckBox` automation peer with `IToggleProvider`; `Toggle()` raises the same request.
- **Search**: `view.Search` is a `SearchModel`.
  - `Find(query, matchCase)` highlights every match, including content scrolled off-screen.
  - `Next()`, `Previous()` and `MoveTo(i)` jump to a match. `Clear()` removes the highlights.
  - Colours come from `Md.SearchHighlightBackground` and `Md.SearchCurrentBackground`.
- **Keyboard selection**: with a selection or caret, Shift+←/→ moves by character (Alt or Ctrl moves by word) across segments, Shift+↑/↓ moves by line, and Shift+Home/End goes to the segment start or end.
- **Context menu**: Copy, Copy as Markdown, Select All, and Find.
  - It uses `MenuFlyout`, so it relies on the host theme's templates.
  - The labels come from `MarkdownServices.MenuText`. Choosing "Find" raises `MarkdownContextMenu.FindRequestedEvent` so the host can show its find box.
  - Disable it with `ContextMenu = false`.
- **Fullscreen**:
  - Large or overflowing tables get a fullscreen button that opens a `BlockOverlay` at natural column widths.
  - Rendered diagrams open in the zoomable viewer.
- **Accessibility**:
  - Text segments, code and diffs expose Text automation peers whose name is the plain text. Headings carry their heading level.
  - Icon buttons are Buttons with Invoke support. Block views are Group, Table or List. Markdown and transcript views are Documents.
  - Avalonia has no public text pattern, so screen readers only get the name.

## Core usage

```csharp
var document = new StreamingDocument();
document.SnapshotReady += _ => RequestFrame();   // raised on a background thread

// network callback
document.Append(token);
// end of stream
document.Complete();

// on each UI frame
var next = document.Latest;
foreach (var op in SnapshotDiff.Compute(applied, next)) { /* insert / update / remove block views */ }
applied = next;
```

## Building

From the TKCode.Arc repository root:

```bash
tools/streammark/build-ratex.sh            # the RaTeX native library for this machine (needs Rust)
dotnet build src/TKCode.Arc.StreamMark
dotnet test tests/TKCode.Arc.StreamMark.Tests
dotnet publish tools/streammark/Gallery -c Release -r osx-arm64 -o artifacts/streammark-aot
artifacts/streammark-aot/TKCode.Arc.StreamMark.Gallery --smoke
```

Release builds of the native library for all six desktop RIDs: `tools/streammark/build-ratex.sh <rid>`, then
`tools/streammark/sync-native.sh` (details in [agent-guide.md](agent-guide.md)).

## License

MIT. See the repository's [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).
