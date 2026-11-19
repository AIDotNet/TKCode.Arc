# Streaming Markdown rendering on Avalonia 12 / .NET 10: research report

Date: 2026-10-16. Sources were read from shallow clones in
`<research-scratch>/src/`
(Avalonia at tag `12.1.4`, the latest release, 2026-10-09). Avalonia release notes for 12.0.0 and 12.1.0 are saved next to this file as `avalonia-release-12.0.0.md` and `avalonia-release-12.1.0.md`.

---

## 0. TL;DR

* **No existing library meets all of our requirements**: Avalonia 12, streaming, cross-block selection, virtualization inside a huge message, AOT-clean, and restylable by a host theme.
  * **LiveMarkdown.Avalonia** (DearVa) is the most complete streaming renderer. It uses one control per block, Avalonia `Inline` trees, and a full Markdig reparse that runs off the UI thread and is coalesced. Cross-block selection is coordinated over many `SelectableTextBlock`s. It has no virtualization.
  * **hjc4869/AvaloniaMarkdownRenderer** has the best streaming and virtualization architecture: an incremental block parser, a frozen prefix, a keyed diff, a Fenwick-tree virtualizing panel, and one `TextLayout` per block. It has no cross-block selection and uses its own parser instead of Markdig.
  * **Markdown.Avalonia** (whistyun) has the most users. It rebuilds the whole tree on every change, has its own regex parser, and is only at a 12.0 alpha. It is a poor fit for streaming.
* **Avalonia has no built-in cross-control text selection.** There is no `RichTextBlock` or `FlowDocument`. Every library builds a selection coordinator above per-block controls, or uses a single custom control.
* **`SelectableTextBlock` is costly for our use**:
  * Any change to `SelectionStart`/`SelectionEnd` calls `InvalidateTextLayout()`, which reshapes the text and invalidates measure.
  * Any change to `Inlines` drops the whole `TextRunCache` and rebuilds all `TextRun`s.
  * Drag-selecting across 200 blocks therefore reshapes 200 paragraphs per pointer move.
* **Recommended architecture**:
  * Parsing:
    * Messages are parsed independently.
    * Messages that have finished streaming are parsed once and frozen.
    * The streaming message is reparsed with Markdig, but only from the last stable top-level block, off the UI thread and coalesced.
  * Rendering:
    * A flat list of block models.
    * Our own virtualizing panel with a Fenwick height cache that implements `ILogicalScrollable`.
    * Text blocks are lightweight custom `Control`s that own one `TextLayout` (built with `textStyleOverrides`) and draw selection as rectangles, so selection changes never reshape text.
    * One document-level selection model in source/offset space, in the style of Zed's markdown crate.
  * Fonts, colors and spacing come from `DynamicResource` keys declared in a `ControlTheme`, so AtomUI can remap them to its tokens.

---

## 1. Existing renderers

### 1.1 LiveMarkdown.Avalonia (DearVa)

https://github.com/DearVa/LiveMarkdown.Avalonia · Apache-2.0 · 179★ · last push 2026-09-19 · NuGet `LiveMarkdown.Avalonia` 2.4.3 · targets net8/net10, **Avalonia 12.0.0**, Markdig 1.1.2, TextMateSharp. Active, with a single main maintainer.

**Data flow**
- Source: `ObservableStringBuilder`, which supports only Append and Clear and raises `Changed(StartIndex, Length, NewLength, Version)`.
- `MarkdownUpdateProducer` (`Utilities/MarkdownUpdateProducer.cs`) does the parsing:
  - It merges pending changes into one dirty range.
  - It runs `Task.Run(() => Markdown.Parse(snapshot.Text, pipeline))` over the **whole document** on the thread pool.
  - It drops stale results using a generation counter and the version number.
  - It publishes a `MarkdownDocumentUpdate.Full` or `.Incremental(previous, document, change)`.
  - A single parse loop means the parse rate adapts itself: while one parse runs, new changes simply widen `pendingChange`.
- `MarkdownRenderer.DocumentUpdate` setter, which runs on the UI thread, calls `documentNode.Update(...)`.

**Diffing the visual tree**
- Each Markdig object maps to a persistent `MarkdownNode` (`Nodes/MarkdownNode.cs`), which records its last `SourceSpan`.
- `IsDirty` returns true if the span changed or if the change range overlaps the span:
  ```csharp
  return !span.Equals(markdownObject.Span) || span.End >= change.StartIndex && change.StartIndex + change.Length > span.Start;
  ```
- `ContainerBlockNode.UpdateCore` walks children by index:
  - A clean node is skipped.
  - A node whose `Update` returns `false` (type mismatch) is replaced.
  - New nodes are appended and extra nodes are trimmed.
  - The same index-based walk is used for inlines in `InlinesNode`.
- When text is appended at the end, only the tail block and its tail inlines get dirty, so already-built controls are reused. `LiteralInlineNode` just sets `run.Text = literal.Content.ToString()`.
- Each block is an Avalonia control:
  - Containers are `StackPanel`.
  - Paragraphs and headings are `MarkdownTextBlock : SelectableTextBlock`, holding an `InlineCollection` of `Run`/`Span`/`InlineUIContainer`.
  - Code blocks are templated `CodeBlock` controls.
  - Tables and lists use custom grids (`MarkdownTableGrid`, `MarkdownListGrid`).
- Node types are registered through `MarkdownNode.Register<TNode>()` and factories (`MarkdownNodeFactory<TNode> where TNode : new()`). To find the handled Markdig type, the factory uses `typeof(TNode).BaseType.GetGenericArguments()[0]`. This is reflection, but it is trim-safe in practice because `TNode` is statically rooted.

**Lesson from issue #21**: streaming tables into Avalonia's built-in `Grid` crashed with `ArgumentOutOfRangeException` in `Grid.MeasureCellsGroup` when children changed during layout. The maintainer replaced `Grid` with custom panels. **Avoid `Grid` for streamed content; write simple purpose-built panels.**

**Selection** (`Controls/MarkdownRenderer.Input.cs`, 1123 lines, plus `MarkdownTextBlock.cs`, 2168 lines)
- Pointer handling is centralized on the `MarkdownRenderer`.
- On press it caches every visible `MarkdownTextBlock` in the selection scope (`_activeScopeBlocks`).
- Positions are stored as `(MarkdownTextBlock Block, int Offset)` anchor and focus.
- `UpdateSelectionRange` then calls `block.SetCurrentValue(SelectionStartProperty/SelectionEndProperty)` on **every block in the scope**. Each of those calls triggers a full `InvalidateTextLayout()` (see §2.2).
- Nested text blocks (inside `InlineUIContainer`) are ordered through placeholder indices. Ordering otherwise falls back to the block's index in the array.
- `MarkdownTextBlock.IsSelectionScope` (an attached property) lets several renderers, for example multiple chat messages, share one selection scope.
- `MarkdownTextBlock` overrides `CreateTextLayout`:
  - It builds its own `ITextSource`, so the selection foreground does not break bold or italic.
  - It uses `[UnsafeAccessor(Name = "set_LineSpacing")]` to reach an internal setter. That is AOT-OK, but it is fragile across Avalonia versions.
  - It paints highlight backgrounds itself in `RenderTextLayout`.
- Selection auto-scroll uses a `DispatcherTimer`.
- Text search (`MarkdownRenderer.TextSearch.cs`) works on a `MarkdownTextProjection`, so it can search off-screen text.

**Performance assessment**
- Good for one or a few messages.
- Each token costs O(document) for the Markdig parse (off the UI thread), O(blocks) for the index walk on the UI thread, and a full reshape of the tail paragraph.
- **No virtualization**: all blocks are realized controls.
- In a long transcript of N renderers, everything stays realized unless the host virtualizes messages.

**AOT**: the demo desktop app has `<PublishAot>true</PublishAot>`. The library does not declare `IsAotCompatible`. It uses compiled bindings (`AvaloniaUseCompiledBindingsByDefault`).

**Theming**:
- `Styles.axaml` contains `Style Selector="md|MarkdownTextBlock"` plus class selectors (`.ParagraphBlock`, `.Literal`, …), each with `{DynamicResource FontSizeM}`, `BorderColor`, and similar.
- The resource keys are generic and unprefixed (`ForegroundColor`, `BorderColor`, `FontSizeM`) and are likely to collide with host keys.
- It uses `Style`s rather than `ControlTheme`s.

### 1.2 hjc4869/AvaloniaMarkdownRenderer ("AvaloniaMarkdown")

https://github.com/hjc4869/AvaloniaMarkdownRenderer · 2★ · pushed 2026-07-30 · Avalonia 12.1.1 · its own GFM parser (no Markdig). Self-described as "AI slop designed to render AI slop", but `docs/ARCHITECTURE.md` and `docs/INCREMENTAL-MODEL.md` describe a careful design. **This is the best design reference for the streaming core.**

- **TextBuffer**: an append-only `char[]` from `ArrayPool`. All later stages use `(start, length)` spans.
- **BlockParser**: a CommonMark line state machine that touches **only the open path**. It has two modes:
  - `ProcessCommittedLine(span)` for a line ending in `\n`.
  - `ProcessSpeculativeLine(span)` for the trailing partial line. Mutations are recorded in an undo journal and rolled back with `RollbackSpeculative()` before the next append.
- Rolled-back nodes are **recycled by creation order** so their `BlockId` stays the same. A stable ID keeps the same control, selection and focus.
- **Streaming emphasis**: in an open block, dangling `**bol` is auto-closed and marked `InlineStyle.Provisional`. This avoids a literal→bold reflow, the same idea as Vercel's `remend`.
- **DocumentFlattener**: a pre-order projection into a **flat** `FlatBlock` list.
  - List and quote nesting become `IndentLevel`/`Marker`/`QuoteDepth` fields.
  - Large code blocks are split into chunks of 128 lines (`CodeBlockChunkLines`) that share one `CodeBlockState`.
  - The flat list is what makes virtualization inside one message possible.
- **FrozenBlockList and MarkdownSnapshot**:
  - Closed blocks are promoted into an append-only chunked list.
  - A snapshot is a frozen prefix (shared by reference) plus a small volatile tail.
  - The single-writer, many-reader design needs no lock.
- **BlockDiffEngine**: starts at `min(prev.StableCount, cur.StableCount)` and does a keyed prefix/suffix diff. It emits `AppendBlock`/`InsertBlock`/`ReplaceBlock`/`UpdateInline`/`RemoveBlock`/`FinalizeBlock`. One token produces one `UpdateInline`.
- **MarkdownVirtualizingPanel : Panel, ILogicalScrollable**:
  - `BlockHeightCache` is a Fenwick tree with `PrefixSum(i)` and `FindIndex(y)`, both O(log n).
  - Height estimates for unmeasured blocks are **frozen at insert**, so later estimate changes cannot move visible content.
  - Overscan is 240 px, with per-kind recycle pools.
- **Text**: one `TextLayout` per block, built with `textStyleOverrides: IReadOnlyList<ValueSpan<TextRunProperties>>` from a flat run table (`Rendering/InlineTextRenderer.cs`). Links are hit-tested with `TextLayout.HitTestPoint` and mapped back to the run table. No `Inline` objects are used.
- **Frame throttling**: snapshots arrive on a worker thread and a 16 ms `DispatcherTimer` applies the latest one. `DeferUpdatesWhileScrolling` waits 120 ms after user scroll (capped at 400 ms). Content-driven `ScrollChanged` events are ignored.
- **Stick to bottom**: `if (AutoScrollToEnd && wasAtEnd) Dispatcher.UIThread.Post(ScrollToEnd, DispatcherPriority.Loaded)`, plus one retry after layout because the extent is only exact after measure.
- **Benchmarks** (README): append latency stays flat at about 1–4 µs per token from 1.6k to 268k blocks. Memory is about 930 B per block.
- **Gaps**:
  - No cross-block selection; only code segments support selection.
  - No syntax highlighting.
  - No link reference definitions.
  - Its own parser carries a CommonMark-compliance risk.

### 1.3 Markdown.Avalonia (whistyun)

https://github.com/whistyun/Markdown.Avalonia · MIT · 416★ · last push 2026-07 · 53 open issues · NuGet stable is 11.0.3; **Avalonia 12 support is only `12.0.0-a3`**.

- **Parser**: its own regex parser, ported from Markdown.Xaml (`Markdown.Avalonia.Tight/Parsers/BlockParser.cs`). Markdig is used only by the HTML plugin.
- **Updates**: `MarkdownScrollViewer.Markdown` setter → `UpdateMarkdown()` → `_engine.TransformElement(Markdown)`. That is a **full rebuild on every change**.
  - A `ChatAISetup` plugin exists, but it only switches off some syntax extensions and enables `EnablePreRenderingCodeBlock`.
- **Inline layout**: its own `ColorTextBlock.Avalonia/CTextBlock : Control`, with its own inline types (`CRun`, `CSpan`, `CHyperlink`, …). It measures and draws inlines itself instead of using `TextBlock`.
- **Selection**: `ColorDocument.Avalonia` has `DocumentElement` with `Select(Point from, Point to)` and `UnSelect()`. Selection is geometric, driven from the scroll viewer.
- **Open issues**: #174 duplicate text on copy, #109 selection wrong when scrolled, #136/#125 text not selectable, #173 AOT dark-mode code rendering, #122 NativeAOT crash (closed), #149 stutter with Transitions (closed).
- **Verdict**: not suitable for token streaming. The CTextBlock idea (custom inline layout in one control) is worth noting.

### 1.4 Others found (`gh search repos "avalonia markdown"`)

| Repo | Notes |
|---|---|
| wieslawsoltes/ProMarkdown (14★, Avalonia 12.0.5, active) | Built on Markdig with `UsePreciseSourceLocation`. Renders into an `InlineCollection` for a `MarkdownTextBlock : SelectableTextBlock`. Has a 1961-line `MarkdownDocumentSelection` that uses `ConditionalWeakTable`s for segment registration. Oriented toward editing and plugins, not streaming. |
| LilaMorgen/LM.Markdown.Avalonia (6★, **Avalonia 11.3**) | `AppendMarkdown` incremental append. `CrossBlockSelectionHandler` intercepts pointer events at the ScrollViewer and selects across ordered `SelectableTextBlock`s, with a source-span map so the Markdown source can be copied. Same coordinator pattern as LiveMarkdown. |
| dotnet9/CodeWF.Markdown (Avalonia 12.1.3, Semi) | Viewer, editor and exporter. Not streaming-focused. |
| LITTOMA/Avalonia.MarkdownViewer (Avalonia 11.3) | Simple Markdig→controls renderer. |
| RYCBStudio/FluentAvalonia.MarkdownRenderer | Small. |

### 1.5 WPF references

- **MdXaml** (whistyun, 339★, last commit 2026-03): the WPF sibling of Markdown.Avalonia. Produces a `FlowDocument`.
- **Markdig.Wpf** (Kryptos-FR, 178★, **dormant since 2024-03**): `WpfRenderer : RendererBase` pushes a stack of `FlowDocument` → `Paragraph`/`List`/`Table` elements, in the classic Markdig renderer pattern.
- **Takeaway**: WPF gets cross-paragraph selection for free from `FlowDocument` with `RichTextBox` or `FlowDocumentScrollViewer`, because one control owns the text container (`TextPointer`). Avalonia has no equivalent, so we have to build that "single owner of selection" ourselves.

### 1.6 Non-.NET references worth copying from

- **Zed `crates/markdown`**:
  - Selection is a single `Selection { start, end }` in **source-index space**. One `Markdown` entity owns it, and rendered text maps source index to position.
  - Copy can produce text or Markdown (`context_menu_selected_markdown`).
  - Parsing is a background task coalesced with `pending_parse` and `should_reparse`. This is the same pattern as LiveMarkdown.
- **Vercel streamdown / `remend`**: "self-healing" Markdown that auto-closes `**`, `` ` ``, `~~`, `$$`, and unfinished links (`[text](url` → placeholder) before parsing. It also splits the source into blocks (`parse-blocks.tsx`) so finished blocks can be memoized.

---

## 2. The Avalonia 12 text stack

### 2.1 APIs (Avalonia.Base/Media/TextFormatting)

- **`TextLayout`** has two kinds of constructor:
  - `(string text, Typeface, fontSize, IBrush foreground, TextAlignment, TextWrapping, TextTrimming?, TextDecorationCollection?, FlowDirection, maxWidth, maxHeight, lineHeight, letterSpacing, maxLines, IReadOnlyList<ValueSpan<TextRunProperties>>? textStyleOverrides, TextRunCache? textRunCache = null)`
  - `(ITextSource, TextParagraphProperties, TextTrimming?, maxWidth, maxHeight, maxLines, TextRunCache? = null)`
- **`TextLayout` members**: `Draw(DrawingContext, Point)`, `HitTestPoint(in Point)` → `TextHitTestResult`, `HitTestTextPosition(int)`, `HitTestTextRange(int start, int length)` → `IEnumerable<Rect>`, `GetLineIndexFromCharacterIndex`, `TextLines`, `Width`, `WidthIncludingTrailingWhitespace`, `Height`, and `Dispose()`.
- **`TextFormatter.Current.FormatLine(ITextSource, firstIndex, paragraphWidth, TextParagraphProperties, TextLineBreak?)`** gives `TextLine`. `TextLine` exposes `GetCharacterHitFromDistance`, `GetDistanceFromCharacterHit`, `GetTextBounds(first, len)`, `Draw`, `Collapse`, and `Justify`. Use this for line-level control, such as virtualizing lines inside a huge paragraph or code block.
- **`DrawableTextRun`** (abstract `Size`, `Baseline`, `Draw`) embeds custom drawn objects in a line, such as inline-code chips, math, and checkboxes, without the control overhead of `InlineUIContainer`. 12.0 fixed `DrawableRun.Baseline` handling (#19983).
- **`GenericTextRunProperties(typeface, fontSize, decorations, foreground, backgroundBrush?, baselineAlignment, cultureInfo?, fontFeatures?)`**: in 12.0 the constructors were merged and `FontFeatureCollection` became the last optional parameter (a breaking change). `TextParagraphProperties.LineSpacing` is still `internal` (only Avalonia.Controls sets it).
- **New in 12.1, marked `[Unstable]`: `TextRunCache`**. It caches shaped runs and bidi results **per paragraph** (keyed by text-source index) and has `Invalidate()` and `InvalidateFrom(int textSourceIndex)`.
  - Within one control, it lets a width-only change skip reshaping.
  - With `InvalidateFrom(lastParagraphStart)`, appending to a multi-paragraph layout (code block lines, for example) reshapes only the last paragraph.
  - `TextBlock` uses it internally: `_textRunCache ??= new TextRunCache()`.
- **12.0 text notes**:
  - Shaping is a separate subsystem. With an explicit `UseSkia` you also need `.UseHarfBuzz()`, otherwise you get "No text shaping system configured".
  - `TextOptions` was introduced (#20107), and `RenderOptions.TextRenderingMode` is now `TextOptions.TextRenderingMode`.
  - `LetterSpacing` moved to the inherited attached property `TextElement.LetterSpacing`.
  - `TextPresenter` measures with `WidthIncludingTrailingWhitespace` (#21067).
- **12.1 text notes**:
  - Reworked fallback itemization (#21631).
  - Fixed `SelectableTextBlock` selection offset with `InlineUIContainer` (#21646).
  - Fixed the selection brush not updating (#21480).
  - "Composition-aware geometries and drawing change detection" (#21682).
  - The render-data opcode stream (#21366) reduces per-draw allocations.
  - Per-visual AABB hit testing (#21310).

### 2.2 Costs of TextBlock, SelectableTextBlock and Inlines (verified in source, `Avalonia.Controls/TextBlock.cs`, `SelectableTextBlock.cs`)

- **Inline changes**: `InlineCollection.Invalidated` → `TextBlock.InvalidateTextLayout()`:
  ```csharp
  _textRunCache?.Invalidate(); _textRuns = null; DisposeTextLayout(); InvalidateVisual(); InvalidateMeasure();
  ```
  Any `Run.Text` change (`Run.cs:83` → `InlineHost?.Invalidate()`) therefore discards **all** shaped runs. The next measure rebuilds the `TextRun` list (`EnsureTextRuns` walks every inline through `BuildTextRun`) and reshapes the whole paragraph. That is O(paragraph length) per token. It is acceptable for short paragraphs, but each token creates garbage: `Run` objects, `List<TextRun>`, and shaped buffers.
- **Selection changes**: in `SelectableTextBlock.OnPropertyChanged`, a change to `SelectionStart` or `SelectionEnd` calls `InvalidateTextLayout()`, which **reshapes and remeasures**. The reason is that `CreateTextLayout` bakes the selection foreground into `textStyleOverrides`. A cross-block drag over many blocks therefore reshapes every affected block on every pointer move. LiveMarkdown pays this cost.
- **Any measure invalidation disposes the layout**: `OnMeasureInvalidated()` → `DisposeTextLayout()`.
- **`InlineUIContainer`**: a real child control, measured through `MeasureEmbeddedControls` on every measure. If its size changes, the layout is disposed again.

**Implication**: in our own text control:
- Keep the `TextLayout` (and a `TextRunCache`) across selection changes.
- Draw the selection background as rectangles from `HitTestTextRange`.
- Draw the selection foreground only if we really need it. Most chat UIs (VS Code, Zed, browsers in dark themes) just use a translucent highlight with no foreground swap.
- Rebuild the layout only when the content or width changes. For the streaming tail, rebuild only the last block.

### 2.3 Cross-control selection

- Avalonia has **no built-in mechanism**. Search found no `RichTextBlock`, `FlowDocument` or `TextContainer`. `SelectableTextBlock` is limited to one control, and `TextBox` is limited to one control.
- **Pattern A, the coordinator** (LiveMarkdown, LM.Markdown, ProMarkdown):
  - Pointer events are intercepted at the root.
  - Blocks are found and ordered by visual order.
  - `SelectionStart`/`SelectionEnd` are set on each block.
  - Works with stock controls, but is expensive (§2.2) and hard to combine with virtualization, because blocks outside the realized set have no control.
- **Pattern B, a selection model in document space** (Zed, VS Code, and our recommendation):
  - `DocumentPosition = (BlockKey, int offsetInBlockText)`, or a global offset into the concatenated rendered-text projection.
  - One `SelectionModel { Anchor, Focus }` per transcript scope.
  - Each realized block asks the model "what range of my text is selected?" in `Render`, then draws rectangles from `TextLayout.HitTestTextRange`.
  - Unrealized blocks need nothing.
  - Copy walks the **model**, not the controls, so it works even when the selection spans virtualized-away blocks. LiveMarkdown's `MarkdownTextProjection` is a similar idea for search.
  - Hit testing:
    1. The panel finds the block at Y with a Fenwick `FindIndex(y)`.
    2. The block finds the character with `TextLayout.HitTestPoint(pt).TextPosition`.
    3. If the point lies between blocks, snap to the nearest block edge.
  - Auto-scroll while dragging near an edge uses `TopLevel.RequestAnimationFrame` or a timer.
- **Pattern C, a single giant custom control** that lays out and draws all blocks itself, in the style of `CTextBlock`, but for the whole document:
  - It makes selection trivial.
  - It loses templated sub-controls (copy buttons on code blocks, images, tables with scroll) unless you mix in children anyway.
  - **Pattern B with lightweight per-block controls inside one virtualizing panel gives most of C's benefits.**

---

## 3. Virtualization for long chat transcripts

### 3.1 Stock options

- **`VirtualizingStackPanel`** (`Avalonia.Controls/VirtualizingStackPanel.cs`):
  - Estimates unrealized sizes with a running average, `_lastEstimatedElementSizeU` (default 25, recomputed by `EstimateElementSizeU()` from realized items).
  - With very different heights (a one-line user message next to a 3000-px answer), the extent and scrollbar jump, and scrolling up drifts. This is a long-standing complaint.
  - PR #20993 ("content virtualization and advanced scroll position calculation supporting varying item sizes") tried to fix the estimate. It was **closed unmerged on 2026-08-24**.
  - Issue #20259 (VirtualizingDataTemplate, container-level virtualization) and PR #22392 ("Avoid recycling in-range elements when VirtualizingStackPanel reaches the list start", still open) show this area is still in flux.
  - VSP does register realized elements as `IScrollAnchorProvider.RegisterAnchorCandidate`.
- **`ItemsRepeater`**: a separate package, `Avalonia.Controls.ItemsRepeater`, version 12.0.0 is published. It has a WinUI-style `VirtualizingLayout`/`StackLayout` and also estimates sizes by average. Writing a custom `VirtualizingLayout` is possible, but the API is heavier than writing a panel.
- **Scroll anchoring**:
  - `ScrollContentPresenter : IScrollAnchorProvider` (`ArrangeWithAnchoring`) keeps the anchor element's position when content above it changes size.
  - It only does this when `Offset > 0` (`isAnchoring = Offset.X >= EdgeDetectionTolerance || Offset.Y >= …`).
  - It is the browser `overflow-anchor` model: it keeps the content you are reading stable, **but it does not provide "stick to bottom"**. Sticking to the bottom while streaming has to be done by hand.

### 3.2 Recommended design (two levels, one panel)

- **Flatten the transcript into one virtualized list of block items**, in the style of hjc4869:
  - Row types: message header/chrome, flattened markdown blocks, code-block chunks, tool-call cards, and so on.
  - Do not use "one item per message plus a nested non-virtualized renderer".
  - A single 30k-token answer then virtualizes inside itself. Code blocks longer than N lines are split into segments that share horizontal scroll and selection state.
- **`TranscriptPanel : Panel, ILogicalScrollable`**:
  - Heights live in a Fenwick tree for `PrefixSum` and `FindIndex`.
  - Unmeasured height = per-kind estimate (for example `lines × lineHeight` from the character count and the last known width). The estimate is frozen at insert and replaced by the real height after the first measure.
  - Realize the viewport plus overscan, with per-kind recycle pools.
  - When a block above the viewport changes height, shift `Offset` by the delta in the same layout pass (self-anchoring). This is reliable because we own the offsets, so `ScrollContentPresenter` anchoring is not needed.
  - **Width change**: all measured heights become stale. Re-measure visible blocks, mark the others dirty, and keep a stable anchor (first visible block plus intra-block offset).
- **Stick to bottom**:
  - Keep `_pinnedToEnd`, true when the user is at the bottom (offset ≥ extent − viewport − ε) and has not scrolled up.
  - After applying an update, do the following in the same layout pass, inside `ArrangeOverride`/`MeasureOverride`:
    1. Set `Offset = extent − viewport`.
    2. Call `RaiseScrollInvalidated`.
  - Doing it in the same pass avoids hjc4869's `Post(…, Loaded)` plus retry and the one-frame jitter.
  - Treat only real user input (wheel, scrollbar drag, keyboard) as "unpin". Ignore content-driven `ScrollChanged`, as hjc4869 notes.
- **Streaming while the user is scrolled up**:
  - Content grows below the viewport, so nothing moves.
  - If content above changes (rare in append-only chat), self-anchoring keeps the view stable.

---

## 4. Render pipeline costs

- **Layout propagation** (`Layoutable.cs`, `LayoutManager.cs`):
  - `InvalidateMeasure()` queues only the control itself.
  - A parent is re-measured only if the child's `DesiredSize` actually changes (`ChildDesiredSizeChanged`).
  - Appending a token that does not wrap to a new line keeps the paragraph height, so no ancestor layout happens. Only the block re-measures plus `InvalidateVisual`.
  - When the height changes, the chain goes up to the panel. A `StackPanel` parent then re-measures and re-arranges **all** its children (cheap early-outs, but still O(n)) → our Fenwick panel only needs `O(log n)` for the update plus arranging the realized children.
- **Avoid a full transcript layout per token**:
  - Finished messages are frozen. Their block controls keep valid measures, and virtualization keeps them out of the tree.
  - Only the tail block of the streaming message is touched per frame.
- **Frame throttling**:
  - Rendering is driven by `MediaContext` on the UI thread at `DispatcherPriority.Render`, with `BeforeRender`/`Render`/`AfterRender`/`UiThreadRender`/`Loaded` lower than `Input`. Layout runs inside the render pass.
  - Options:
    - (a) A `DispatcherTimer` at about 16 ms, as hjc4869 does.
    - (b) **`TopLevel.RequestAnimationFrame(Action<TimeSpan>)`**, a one-shot callback per frame driven by the compositor clock (`MediaContext.Clock`). Request it only when something is pending. This idles to zero when nothing is streaming and follows the display refresh rate. 12.1 added support for the maximum refresh rate on Windows (#21643).
    - (c) `Compositor.RequestCompositionUpdate(Action)`.
  - **Recommendation**: the network or parser thread produces snapshots, a `volatile` latest-snapshot slot holds the newest one, and `RequestAnimationFrame` applies at most one diff per frame and never queues backlog.
  - Also pause applying updates for about 100 ms during an active user scroll or selection drag, with a maximum deferral, as hjc4869 does.
  - 12.0 note: `DispatcherTimer` and `AvaloniaSynchronizationContext` now bind to the **current** dispatcher. Library code should use `AvaloniaObject.Dispatcher`/`Dispatcher.CurrentDispatcher`. `Dispatcher.InvokeAsync` now captures `ExecutionContext`.
- **Fade-in of new text**:
  - Per-control composition: `ElementComposition.GetElementVisual(control)` gives a `CompositionVisual`. You can set `ImplicitAnimations` with `compositor.CreateImplicitAnimationCollection()` and `CreateScalarKeyFrameAnimation` on `Opacity`/`Offset`. Avalonia 12 added a `Translation` property for `CompositionVisual` (#20836) and fixed `CompositionAnimation` (#20936).
  - Composition runs on the render thread with no UI-thread cost. It works well for **whole new blocks** (fade or slide in a new paragraph or card) and for smooth scroll-to-bottom offsets.
  - **Glyph-level fade** for the newest characters inside a paragraph cannot be done with composition, because a block is one visual. Options:
    - (1) In `Render`, draw the tail range with an alpha brush driven by a per-frame `RequestAnimationFrame` + `InvalidateVisual()`. This is cheap, with no re-measure and only that block re-recorded. Store the per-character birth time in the block and draw `TextLayout` twice, or use `PushOpacityMask` with a gradient that covers only the tail line.
    - (2) `CompositionCustomVisualHandler` (`compositor.CreateCustomVisual(handler)`) for render-thread animation of custom drawing, which is more complex.
  - Recommendation: option (1) with a short fade of about 150–250 ms, which can be turned off.
  - 12.0 note: style-triggered animations no longer tick while their control is hidden (`Animation.PlaybackBehavior`).

---

## 5. NativeAOT, trimming and theming for a reusable library

### 5.1 AOT and trimming (Avalonia 12)

- Avalonia's own assemblies set `IsTrimmable=true` and `IsAotCompatible=true` (`build/TrimmingEnable.props`). Our library should set `<IsAotCompatible>true</IsAotCompatible>`, which also turns on trim, single-file and AOT analyzers. Verify by AOT-publishing a sample app (0 IL warnings) and running it.
- **Compiled bindings are the default** in 12 (`AvaloniaUseCompiledBindingsByDefault=true`). XAML `{Binding}` maps to `CompiledBinding` and needs `x:DataType`. In code, the `Binding` class is now always `ReflectionBinding`, which is not AOT-safe. Use `CompiledBinding.Create(...)` (AOT-compatible since #20776/#20996) or avoid bindings altogether.
  - **For a rendering control library, avoid bindings in hot paths entirely.** Set properties directly and use `TemplateBinding`, which is compiled, in `ControlTheme` templates.
- **Binding plugins were removed** (`BindingPlugins`, `IPropertyAccessorPlugin`). `IBinding` became `BindingBase`. `BindingPriority.TemplatedParent` became `BindingPriority.Template`.
- **Not AOT-safe at runtime**: `new StyleInclude(uri)`, `new ResourceInclude(uri)`, and `AvaloniaXamlLoader.Load(Uri)` are all marked `[RequiresUnreferencedCode]`. **AOT-safe**: XAML-compiled `x:Class` files loaded with `AvaloniaXamlLoader.Load(this)`, which the XamlIl compiler rewrites. `<StyleInclude Source="avares://…">` written inside XAML is compiled away and is fine.
  - Ship the theme as an `x:Class` type that hosts can add without URIs: `public class MarkdownTheme : Styles { public MarkdownTheme() => AvaloniaXamlLoader.Load(this); }`.
- **Markdig** (`Markdig.targets`: net462, ns2.0, ns2.1, net8, net10) declares no AOT annotations. Its only reflection is `System.Reflection` in `Markdown.cs` for the version. LiveMarkdown's AOT demo shows it works. Version 1.4.0 is current on NuGet.
- **Other AOT notes**:
  - Avoid `Activator.CreateInstance` and type scanning for extension registration. Use generic `Register<TNode>() where TNode : new()` or delegate factories.
  - `UnsafeAccessor` for Avalonia internals is AOT-OK but couples us to private members. LiveMarkdown uses it for `set_LineSpacing`, because `TextParagraphProperties.LineSpacing` is still `internal` in 12.1.4. To avoid it, use `LineHeight` instead, or add the spacing ourselves between `TextLine`s when we format lines.
  - TextMateSharp loads grammars from embedded JSON. Check its AOT status separately, or make highlighting a pluggable `ISyntaxHighlighter`.

### 5.2 Theming surface so AtomUI can restyle

- **Use `ControlTheme` per templated control** (code block chrome, table, blockquote, image, task checkbox). Put them in a `ResourceDictionary` keyed by `{x:Type md:CodeBlockView}`. Hosts can override them by key with `BasedOn`, or by setting `Theme=`. Avalonia's Fluent and Simple themes and AtomUI all work this way.
- **Lightweight drawn blocks** (paragraphs, headings, list markers, quote bars) are not templated. Expose `StyledProperty`s (`Foreground`, `FontFamily`, `FontSize`, `LineHeight`, `HeadingFontSizes`, `CodeFontFamily`, `InlineCodeBackground`, `LinkForeground`, `SelectionBrush`, `QuoteBarBrush`, `BlockSpacing`, …) on the host control (`MarkdownView`/`TranscriptView`) with `inherits: true` where it makes sense. Set their defaults in the library's ControlTheme with `{DynamicResource MdKeys.X}`.
  - Blocks read them once into an immutable `MarkdownRenderStyle` snapshot, so the render path does not look up properties per run.
  - When the theme changes, rebuild the snapshot, re-layout the visible blocks, and mark the rest dirty.
- **Resource keys**:
  - Namespace every key with a prefix (for example `Md.Foreground`, `Md.CodeBackground`) and publish them as `public static class MarkdownResourceKeys { public const string Foreground = "Md.Foreground"; … }`. LiveMarkdown's unprefixed `ForegroundColor`/`BorderColor`/`FontSizeM` would collide with host keys.
  - The host (AtomUI) supplies a `ResourceDictionary` that maps `Md.*` to its tokens. AtomUI 6.2.5 has a design-token system (`AtomUI.Theme.DesignTokens`, `ControlTokenResourceKey`, `SharedToken…`).
  - Alternatively, the host sets the inherited properties from its own token bindings.
  - Provide light and dark defaults with `ThemeVariant`-keyed `ResourceDictionary.ThemeDictionaries`.
- **Mark parts with style classes and pseudo-classes** (`:streaming`, `:selected`, `.h1`…`.h6`) on templated parts. Do not rely on `Style` selectors over thousands of `Run`s; LiveMarkdown's `.Literal` class on each `Run` is costly.

---

## 6. Recommended architecture for our library

1. **Source model**
   - `MarkdownSource` is append-oriented, with `Append`, `Replace`, `Complete` and a version number. Each chat message has its own source, so finished messages are parsed once and cached.
2. **Parsing**
   - Use Markdig with `UsePreciseSourceLocation` and the same extensions as GitHub. **Incremental by tail**:
     - Keep the source offset of the last top-level block whose end is followed by a blank line, outside any fence, list or quote (the "stable boundary").
     - Reparse only `source[boundary..]` on a background worker, with coalescing and a generation counter, as LiveMarkdown and Zed do.
     - Blocks before the boundary are frozen.
     - Link reference definitions defined later can change earlier blocks. LLM output rarely uses them, so do a full reparse on `Complete`.
   - Apply streaming self-healing on the tail only: auto-close emphasis, code spans and fences in the style of remend/hjc4869 `Provisional`.
3. **Flattening**
   - The AST becomes immutable `BlockModel` records with stable keys `(messageId, blockOrdinal)`, built off the UI thread. Fields:
     - `Kind`
     - `Text` (the rendered string)
     - `InlineRun[]` (style, link and code ranges)
     - indent, list and quote depth
     - code language and segments
     - table model
   - The diff is keyed prefix/suffix from the frozen count.
4. **Views**
   - Text-like blocks use `MdTextBlockView : Control`:
     - One `TextLayout` built with `textStyleOverrides` (or a custom `ITextSource` + `DrawableTextRun` for inline-code chips) and its own `TextRunCache`.
     - `Render` draws, in order: block chrome, selection rectangles (`HitTestTextRange`), search and highlight rectangles, the text, then the fade-in tail.
     - Links are hit-tested through the run table.
   - Templated views (code-block header with copy button, image, table with horizontal scroll) use `ControlTheme`. **Do not use `Grid`**; use custom panels.
5. **Container**: `TranscriptPanel : Panel, ILogicalScrollable` with a Fenwick height cache, frozen estimates, per-kind pools, self-anchoring, a pinned-to-bottom mode, and deferral during user scroll or drag.
6. **Selection**:
   - `SelectionModel` in `(blockKey, offset)` space, owned by the transcript.
   - Pointer capture and auto-scroll happen at the panel.
   - Blocks only paint.
   - Copy walks the models and can produce plain text or the Markdown source, using source spans.
   - Double-click selects a word with `TextLayout` caret navigation; triple-click selects the block.
   - Code segments map their line/column positions into the same model.
7. **Frame loop**: the worker publishes the latest snapshot, `TopLevel.RequestAnimationFrame` applies one diff per frame, and the panel measures and arranges only realized and dirty blocks.
8. **AOT and theming**: `IsAotCompatible=true`, no reflection bindings, XAML through `x:Class`, extension factories through generic `new()`, and namespaced resource keys plus a `ControlTheme`-based theme class.

### Tradeoffs

| Choice | Pro | Con |
|---|---|---|
| Inline trees (LiveMarkdown) | Fast to build; uses Avalonia styling and `InlineUIContainer` | Full reshape and garbage per token; selection change reshapes; one control per block, no virtualization |
| One `TextLayout` per block (hjc4869 and ours) | One shaping pass; cheap selection painting; easy to virtualize | We write hit testing, link hover, accessibility (`AutomationPeer`) and embedded objects ourselves |
| Markdig full or tail reparse | Spec-correct GFM and its extension ecosystem | O(tail) per frame, not O(token); needs a stable-boundary heuristic |
| Own incremental parser (hjc4869) | O(token), with identity stability through journal rollback | Large compliance and maintenance burden |
| Stock VSP/ItemsRepeater | Less code | Average-size estimates make the scrollbar jump with mixed heights; no stick-to-bottom; one message cannot be virtualized internally |
| Custom Fenwick panel | Exact offsets, self-anchoring, virtualization inside messages | We own keyboard focus, `BringIntoView` and accessibility |
