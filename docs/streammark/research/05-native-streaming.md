# How native (non-DOM) UI stacks render streaming LLM markdown

Research for a high-performance streaming Markdown library for Avalonia/.NET (TCode agent client).
All findings below come from source cloned into
`<research-scratch>/src/<name>` (shallow clones, Oct 2026 HEADs).

| Stack | Repo / path read | Streaming model |
|---|---|---|
| Zed (Rust, GPUI) | zed-industries/zed `crates/markdown/src/markdown.rs`, `parser.rs`, `selection.rs`; `crates/acp_thread/src/acp_thread.rs` | Full reparse on a background thread, coalesced (1 in flight + dirty flag); reveal pacer outside the markdown view |
| Codex CLI (Rust, ratatui) | openai/codex `codex-rs/tui/src/markdown_stream.rs`, `streaming/{controller,render,chunking,commit_tick,table_holdback,code_fence,prose_preview}.rs` | Newline-gated commit, stable prefix + mutable last block, adaptive two-speed queue drain |
| Gemini CLI (TS, Ink) | google-gemini/gemini-cli `packages/cli/src/ui/utils/markdownUtilities.ts`, `hooks/useGeminiStream.ts` | Split the message at the last safe `\n\n` outside fences; push the prefix into `<Static>` |
| JetBrains intellij-markdown (Kotlin) | JetBrains/markdown `parser/StreamingMarkdownFile.kt`, `MarkdownParser.kt` | Parser-level `stableChildren` + `unstableTail`; only the tail is reparsed |
| Compose MP markdown (Kotlin) | mikepenz/multiplatform-markdown-renderer `model/StreamingMarkdownState.kt`, `compose/Markdown.kt` | Wraps the above; renders stable AST + unstable tail |
| Flutter gpt_markdown (Dart) | useval/gpt_markdown `lib/streaming/*`, `lib/plusparse/*` | Settled-split segment cache, inline hold-back, per-character reveal engine, lazy sliver |
| SwiftUI Textual (successor of MarkdownUI) | gonzalezreal/textual `Sources/Textual/**` | Not streaming-specific; good for selection architecture (layout collection + coordinator) |
| Vercel AI SDK | vercel/ai `packages/ai/src/generate-text/smooth-stream.ts` | `smoothStream` transform: word/line/regex/segmenter chunking + fixed delay |
| LobeHub | lobehub/lobehub `packages/fetch-sse/src/fetchSSE.ts` | rAF char queue, speed eases toward queue length |
| Streamdown/remend (web, reference only) | vercel/streamdown `packages/remend`, docs | Repair incomplete inline markdown; feature checklist |
| glamour (Go) | charmbracelet/glamour `glamour.go` | No streaming: `Close()` converts the whole buffer with goldmark |

Claude Code is closed source, so its renderer could not be checked from source. It is left out of this report and not guessed at.

---

## 1. Zed (GPUI): `crates/markdown`

### 1.1 Data model
- `Markdown` entity holds `source: SharedString` and `parsed_markdown: ParsedMarkdown`. `ParsedMarkdown` is a **flat event list** `Arc<[(Range<usize>, MarkdownEvent)]>` from pulldown-cmark (with `RootStart/RootEnd` markers that Zed adds), plus side tables keyed by **source offset**: `root_block_starts`, `heading_slugs`, `footnote_definitions`, `mermaid_diagrams: BTreeMap<usize,…>`, `html_blocks`, `languages_by_name`, `code_block_highlights: HashMap<EventIndex, ResolvedHighlights>`.
- Per-block UI state is also keyed by source offset: `copied_code_blocks`, `wrapped_code_blocks: HashSet<usize>`, `code_block_scroll_handles: BTreeMap<usize, ScrollHandle>`, `mermaid_views`. Offsets of earlier blocks never change in an append-only stream, so this state survives reparses. The code drops stale entries with `retain`.
- Parse options: tables, footnotes, strikethrough, tasklists, smart punctuation, heading attrs, GFM (alerts), super/subscript, metadata blocks. Math is explicitly **disabled** (`UNWANTED_OPTIONS = ENABLE_MATH | DEFINITION_LIST | WIKILINKS`).

### 1.2 Incremental reparse strategy: none. Zed coalesces full reparses instead.
```rust
pub fn append(&mut self, text: &str, cx: &mut Context<Self>) {
    self.source = SharedString::new(self.source.to_string() + text);
    self.parse(cx);
}
fn parse(&mut self, cx: &mut Context<Self>) {
    ...
    if self.pending_parse.is_some() {   // one parse in flight
        self.should_reparse = true;      // mark dirty, return
        return;
    }
    self.should_reparse = false;
    self.pending_parse = Some(self.start_background_parse(cx));
}
// start_background_parse: cx.background_spawn(parse + language lookup + data: image decode
//   + tree-sitter highlight of every code block) then on main thread:
//   this.parsed_markdown = parsed; this.pending_parse.take();
//   if this.should_reparse { this.parse(cx) }  cx.notify();
```
So the **whole document is reparsed** on a worker thread. While a parse runs, any number of appends collapse into one follow-up parse. The UI keeps showing the last good `ParsedMarkdown`. Parse latency never blocks a frame, and parse frequency adapts to how long a parse takes. This works because pulldown-cmark is very fast and agent messages are split into many `Markdown` entities (one per assistant chunk, thought, or tool output), so each source stays small.

### 1.3 Reveal pacing (in `acp_thread.rs`, not in the markdown crate)
```rust
const TASK_UPDATE_MS: u64 = 16;     // tick
const REVEAL_TARGET: f32 = 200.0;   // reveal whole pending backlog in ~200 ms
// on each incoming chunk:
buffer.cursor.pending_bytes += text_len;
buffer.bytes_to_reveal_per_tick =
    (pending_bytes as f32 / REVEAL_TARGET * TASK_UPDATE_MS as f32).ceil() as usize;
// every 16 ms: markdown.append(cursor.take(max_bytes = bytes_to_reveal_per_tick))  (char-boundary safe)
// flush_streaming_text() (reveal usize::MAX) whenever a new entry is pushed or the target changes
```
The raw content is stored in full in the thread model (`source_blocks`), and the Markdown view only receives the revealed prefix. Pacing is **constant-latency**: any backlog drains in about 200 ms. It is a separate layer from the renderer.

### 1.4 Rendering: one element, many shaped lines, one coordinate space
- `MarkdownElement::request_layout` walks the event list each frame and builds a div tree via `MarkdownElementBuilder` (`push_div/pop_div`, `push_text_style`, `push_text`). Inline text is accumulated into a `pending_line` (string + `TextRun`s + `SourceMapping{rendered_index, source_index}`). `flush_text()` emits one `StyledText` per paragraph-level run as a `RenderedLineElement`, and also pushes an `Rc<RenderedLine>` into `rendered_lines`.
- `RenderedText { lines, links, image_links, footnote_refs }` is the document-wide hit-test index. Each `RenderedLine` holds its `TextLayout`, `source_mappings`, `source_end`, and visible bounds. `source_index_for_position(point)` finds the line under the mouse, calls `layout.index_for_position`, and maps the result back to the **markdown source offset**.
```rust
fn source_index_for_rendered(mappings: &[SourceMapping], rendered_index: usize) -> Option<usize> {
    let mut last = None;
    for m in mappings { if m.rendered_index <= rendered_index { last = Some(m) } else { break } }
    last.map(|m| m.source_index + (rendered_index - m.rendered_index))
}
```
- **Cross-block selection**: `Selection { start, end, reversed, pending, mode: Character|Word|Line|All }` holds **source offsets**. Paint uses `highlights_for_line(line_source_range)` to draw the selection, search matches, and the active match per line. Selection crosses paragraphs, list items, and code blocks with no special casing, because every block shares the same source-offset coordinate space. Copy has two modes: `Copy` (rendered text) and `CopyAsMarkdown`, which uses `rebalanced_markdown_for_selection`. That function re-closes `**`, backticks, and link syntax for partial spans, and a single inline code span is copied as plain text.
- Selection survives reparse because source offsets are stable under append.

### 1.5 Code blocks
- Language resolution happens in the background parse: `language_for_name_or_extension(name)`, falling back to the first word of the info string. `FencedSrc(path)` resolves by file path, and an untagged fence uses `fallback_code_block_language`.
- Highlighting also runs in the background: `compute_code_block_highlights` concatenates all `Text` events of a block, calls `language.highlight_text_resolved` (tree-sitter) once, then slices the runs back per event. At render time, a highlight with `is_current()` false (theme changed) is recomputed for that text.
- Header and buttons: the code block is a `div().group("code_block")` with an absolute `h_flex` button row at the top right. The row holds a **wrap toggle** and a **copy** button, and each has visibility `Hidden | VisibleOnHover | AlwaysVisible`. The copy button shows a checkmark state (`copied_code_blocks`). Horizontal scroll uses a per-block `ScrollHandle` keyed by source offset, and is disabled while wrapped.
- Extensibility: `CodeBlockRenderer::Default{copy_button_visibility, wrap_button_visibility, border}` or `Custom { render: CodeBlockRenderFn, transform: Option<CodeBlockTransformFn> }`. The agent panel uses the custom form for its file-path headers and collapse controls. `CodeBlockKind::{Indented, Fenced, FencedLang(name), FencedSrc(PathRange)}`, where info strings like ```` ```rust path/to/file.rs#L10-20 ```` become clickable headers.
- Mermaid is rendered natively (`mermaid.rs`). Zoom re-raster is debounced (`MERMAID_ZOOM_DEBOUNCE`), there is a show-code/show-diagram toggle, and the cache is invalidated on theme change.
- Images: `data:` base64 is decoded during the background parse. URL images go through GPUI's async image loader.

### 1.6 Other performance tricks
- The agent conversation is a virtualized `gpui::list` (`ListState::new(0, Top, px(2048))` with overdraw), so only on-screen messages lay out. Each assistant chunk is its own `Markdown` entity, so streaming reparses only the active one.
- `refresh_windows()` is called only after a parse lands, not per token.
- `root_block_starts` plus `partition_point` give O(log n) "which top-level block is at this offset", used for autoscroll and `active_root_block` markers.

---

## 2. Codex CLI (ratatui): "commit when newline arrives"

### 2.1 Newline-gated collector (`markdown_stream.rs`)
```rust
pub fn push_delta(&mut self, delta: &str) { self.buffer.push_str(delta); }
pub fn commit_complete_source(&mut self) -> Option<Range<usize>> {
    let commit_end = self.buffer.rfind('\n').map(|i| i + 1)?;
    if commit_end <= self.committed_source_len { return None; }
    let r = self.committed_source_len..commit_end;
    self.committed_source_len = commit_end; Some(r)
}
pub fn committed_source(&self) -> &str { &self.buffer[..self.committed_source_len] }
pub fn pending_source(&self) -> &str  { &self.buffer[self.committed_source_len..] }
```
Only newline-terminated source enters the real render. The incomplete last line is shown through a separate, bounded **ProsePreview** (max 8 KB) that never advances the stable boundary. The preview is suppressed when the pending line starts with `` ` ``, `~`, `>`, indentation, or contains `|`, because those could become fences, quotes, nested code, or table rows. Unfinished link destinations are withheld.

### 2.2 Two-region model (`streaming/controller.rs`, `render.rs`)
- **Stable region**: rendered lines committed to terminal scrollback through an animation queue. It is immutable from then on.
- **Tail region**: a mutable "active cell", re-rendered on every commit.
- `StreamingRender` keeps `stable_source_len` / `stable_rendered_len`. Completed top-level blocks are rendered once and retained, and **only the final top-level block** is re-rendered:
  > "The final top-level block can still change meaning when another line arrives (list tightness, a setext heading, a fenced block, or a table). Earlier top-level blocks are rendered once and retained. Reference-style link definitions and inline visualization rewriting fall back to a full render because they can affect source-wide rendering state."
- Where the mutable tail begins = `min(table_start, mutable_fence_start, pending_math_start, bare_list_marker_line)`.
- **Table holdback** (`table_holdback.rs`): states are `None | PendingHeader{header_start} | Confirmed{table_start}`. A line that looks like a header is held until the delimiter row confirms or refutes it. Once confirmed, the table stays in the tail until its top-level block completes, because a new row can change every column width.
- **Open code fence fast path** (`code_fence.rs`): for a top-level, language-tagged, open fence, new committed lines are appended directly. A persistent syntect `(HighlightState, ParseState)` is kept, so each new line is highlighted incrementally with no re-render of the fence. Any line that *might* close the fence (conservative check) falls back to the canonical full-block render.
- Resize or theme change triggers `recompute()` (full re-render) and remaps the emitted-line count. Finalization re-renders the whole source and drops the stitched stream render.

### 2.3 Commit-tick animation and adaptive chunking (`chunking.rs`, `commit_tick.rs`)
- The tick is `COMMIT_ANIMATION_TICK = TARGET_FRAME_INTERVAL`, about 8.33 ms (120 fps cap).
- **Smooth mode** drains 1 queued line per tick. **CatchUp mode** drains the whole queue. The switch uses hysteresis:
```text
ENTER  catch-up: queued_lines >= 8   OR oldest_age >= 120 ms
EXIT   catch-up: queued_lines <= 2  AND oldest_age <= 40 ms, held for 250 ms
REENTER hold after exit: 250 ms  (bypassed if severe: >= 64 lines OR oldest >= 300 ms)
```
The policy depends only on queue depth and oldest-item age, not on which model or provider sent the text.

---

## 3. Gemini CLI (Ink): static/pending split
```ts
export const findLastSafeSplitPoint = (content: string) => {
  const enclosing = findEnclosingCodeBlockStart(content, content.length);
  if (enclosing !== -1) return enclosing;          // inside an open ``` → split before it
  // else last "\n\n" not inside a fence
  ...
  return content.length;                           // no split
};
// useGeminiStream: if split < len → addItem(before) into <Static/> history, keep `after` as pending item
```
This gives the same benefit as Codex's stable region. Finished parts move into a never-re-rendered `<Static>` list, so the cost per token stays proportional to the tail. The fence detection is naive: it counts backtick triples.

---

## 4. JetBrains intellij-markdown `StreamingMarkdownFile` (used by Compose MP renderer)
```kotlin
override fun append(chunk: CharSequence) {
    unstableText.append(chunk)
    val (tree, unstableStart) = parser.parseStreaming(MARKDOWN_FILE, unstableText, true, baseOffset = stableTextLength)
    val boundary = unstableStart - stableTextLength
    if (boundary > 0) {
        stableChildren += tree.children.filter { it.endOffset <= stableTextLength + boundary }
        stableTextLength += boundary
        unstableText.deleteRange(0, boundary)       // stable text is dropped from parser memory
        unstableTail = tree.children.filter { it.startOffset >= stableTextLength }
    } else unstableTail = tree.children
}
// boundary = keepTrailingCodeBlockOpen(keepTrailingListOpen(keepNodesWhole(
//              min(firstOpenMarker.startOffset, lastBlankLineEnd))))
```
The rules for where the unstable tail starts:
1. It starts no later than the start of the earliest **still-open block marker**, as tracked by the parser's own block state machine.
2. It starts no later than the end of the **last blank line**.
3. A boundary may not cut through a node (`keepNodesWhole`). This handles fenced code blocks that contain blank lines.
4. A trailing **list** stays open until a later block has settled (`2` is a paragraph until `2. ` arrives).
5. A trailing **indented code block** stays open, because it can continue after blank lines.

The Compose renderer (`StreamingMarkdownSuccess`) renders `stableAst.forEach` and then `unstableAstTail.forEach`. Compose skips recomposition of stable nodes because their identity does not change. The renderer also offers `rememberMarkdownState(retainState = true)` (keep old content visible while an async parse runs) and `LazyMarkdownSuccess` (LazyColumn per top-level block).

---

## 5. Flutter `gpt_markdown`: the most complete client-side streaming design found

### 5.1 Settled split (`streaming/stream_split.dart`)
```dart
// Offsets of blank lines outside fences / \[ \] math / <!-- --> comments,
// excluding blank lines that continue a list item (continuesAfterBlank).
// "Never settle the final construct: the next token may extend it."
if (candidates.length < 2) return 0;
return candidates[candidates.length - 2];
```
Everything before the split is rendered once and cached by its source text. Only the tail is re-parsed. Top-level segments are cached as separate `Text.rich` widgets, so "earlier segments keep their exact widget instances". A comment in the code warns against a subtle bug: an off-by-one in candidate detection makes the split jump **backwards**, so content settles and then unsettles.

### 5.2 Inline hold-back (`streaming/inline_hold.dart`)
The reveal head stops before unclosed inline constructs, so no character is ever shown and then restyled (`` `npm `` turning into a code chip, `**bold` closing):
- `proseDelimiterHold = 48` chars: maximum wait for `` ` ``, `*`, `_`, which also appear in normal prose. After that, the text is shown literally.
- `markupDelimiterHold = 160` chars: maximum wait for unambiguous markup: `\(`, `\[`, `<u>`, `[label](href)`.
- The check is a delimiter scan, not a second parse. Conservative is fine: holding costs a few ms of latency.
This replaces remend-style "auto-close" repair. Remend instead rewrites `**text` to `**text**` and turns `[text](url` into a placeholder link, which shows styled text early but can show a wrong partial link.

### 5.3 Reveal engine (`streaming/reveal_engine.dart`): pure arithmetic, no UI dependency
```dart
static const maxLagSeconds = 0.4;        // never lag more than 400 ms behind received text
static const fastForwardSeconds = 0.15;  // after stream end, flush remainder within 150 ms
static const fadeWindow = 64;            // ring buffer of per-char arrival stamps (power of 2)
bool tick(double dt, int target, double cps) {
  var speed = cps;
  final backlog = target - _revealed;
  if (backlog > 0) speed = max(speed, backlog / (_fastForwarding ? fastForwardSeconds : maxLagSeconds));
  _revealed = min(_revealed + speed * dt, max(target, _revealed));
  // stamp newly revealed chars spread across the frame → bursts fade in as a ramp
  ...
}
double progressFor(int index) => ((clock - stamps[index & mask]) / fadeSeconds).clamp(0,1);
```
Handled explicitly:
- **Shrink**: a construct completes and renders fewer characters. The head holds its position, and the vanished range is marked settled.
- **Replace**: regenerate or branch switch calls `reset()`.
- **Snap**: content that is not live streaming is shown with no animation.

Per frame, the renderer restyles at most 64 characters (opacity by `progressFor`) in the **single segment that contains the head**. Segments past the head are not built.

### 5.4 Other pieces
- `SliverGptMarkdown`: viewport-lazy segments. Segmentation is eager, AST parse and widget build are lazy, and reference definitions are collected document-wide.
- `blockGap()` is shared between the inter-segment gap and the settled/tail seam, so content does not move when the seam moves. This matters for any segmented renderer.
- Selection: wrap in Flutter `SelectionArea`. Each `Text.rich` participates through the framework's SelectionRegistrar, and non-text widgets (math, code) use `SelectableAdapter` with a `selectedText`. Limitation: lazy slivers only select mounted content.

---

## 6. SwiftUI: MarkdownUI → Textual
- MarkdownUI (cmark-gfm → block views) is in **maintenance mode**. Its author rewrote it as Textual because MarkdownUI had per-block `Text` views (no cross-block selection) and performance problems.
- Textual keeps SwiftUI's `Text` pipeline. It parses with Foundation `AttributedString(markdown:)` using `PresentationIntent`. `StructuredText` splits the attributed string into block runs and renders `ForEach(runs.indices, id: \.self)` → `Paragraph / Heading / CodeBlock / Table / BlockQuote / MathBlock`.
- **Selection architecture** (worth copying):
  - `TextLayoutCollection` aggregates the resolved `Text.Layout`s of all blocks.
  - `TextPosition` is an `IndexPath` (layout → line → run → slice) plus `Affinity`.
  - `TextSelectionModel.setLayoutCollection()` **reconciles the selected range into the new layout** when layouts rebuild, for example during streaming.
  - `TextSelectionCoordinator` ensures **only one active selection** across the subtree. A scrollable code block or table (an `Overflow` region) has its own local selection model, and selecting in one clears the others.
  - AppKit and UIKit interaction views draw the selection rects.
- Attachments (images, emoji, math) resolve asynchronously in `WithAttachments` via `.task(id: attributedString)` and are written back as attributes. Code highlighting runs Prism.js in JavaScriptCore inside an `actor CodeTokenizer`, off the main actor.
- There is no streaming-specific logic. Typical apps throttle updates and rely on SwiftUI diffing of the block list.

---

## 7. Smoothing algorithms compared

| Client | Unit | Clock | Rate law | Params |
|---|---|---|---|---|
| Zed | bytes (char-boundary) | 16 ms timer | `per_tick = ceil(pending / 200ms * 16ms)` → backlog drains in ~200 ms | REVEAL_TARGET 200 ms |
| Codex | rendered **lines** | ~8.3 ms frame | 1 line/tick, or whole queue under pressure (hysteresis) | enter 8 lines/120 ms, exit 2 lines/40 ms held 250 ms, severe 64/300 ms |
| gpt_markdown | characters (fractional) | Ticker (vsync) | `speed = max(cps, backlog / 0.4s)`; after end `backlog / 0.15s`; per-char fade 0.25 s | maxLag 0.4 s, ff 0.15 s, fadeWindow 64 |
| LobeHub | characters | rAF | `target = max(startSpeed, queueLen)`; `cur += (target-cur) * (|Δqueue|*0.0008 + 0.005)`; chars = floor(acc * cur / 1000) | START_ANIMATION_SPEED 10 cps |
| Vercel `smoothStream` | word (`/\S+\s+/`), line, regex, `Intl.Segmenter` (for CJK), or custom detector | `delay(delayInMs)` between chunks | fixed delay per chunk, default 10 ms; skipped when document is hidden; flush on non-text parts | delayInMs 10 |
| Streamdown | word spans | CSS animation | each newly mounted word fades in (150 ms) | fadeIn / blurIn |

What they agree on:
1. **Pace by bounded lag, not by fixed speed.** The display may trail the network by at most about 200–400 ms. Zed, gpt_markdown, and Codex (catch-up mode) all do this.
2. **Fast-forward at stream end.** Flush everything when a non-text event arrives (tool call, new entry, finish).
3. Keep the pacer **outside** the parser/renderer. It only decides how much of the received text is "visible".
4. Avoid splitting graphemes. CJK has no spaces, so word chunking needs a segmenter.

---

## 8. Cross-cutting features: library vs host

| Feature | Seen in | Belongs to |
|---|---|---|
| Code block header: language label, copy (with copied state), wrap toggle, horizontal scroll | Zed (copy+wrap, hover/always/hidden), Streamdown (copy+download), Textual | **Library** (default chrome) with a replaceable header template / `CodeBlockRenderer::Custom` |
| Collapse long code, line numbers | Zed agent panel (custom renderer), Streamdown | Library option |
| Run / Apply / insert-to-file, file-path headers (`lang path#L1-10`) | Zed `FencedSrc(PathRange)` + custom renderer | **Host** via info-string metadata exposed by the library |
| Syntax highlighting | Zed tree-sitter (background), Codex syntect (incremental per open fence), Textual Prism in actor, Streamdown Shiki lazy grammars | Library with a pluggable highlighter interface; must support incremental line-state for open fences |
| Tables: horizontal scroll, copy as Markdown/CSV/TSV/HTML, download | Streamdown, Textual Overflow table, Codex holdback | Library (scroll + copy); download is host |
| Task lists | All GFM parsers | Library (read-only checkbox) |
| Footnotes | Zed (`footnote_definitions`, click-to-jump) | Library |
| Citations `[1]` / source chips | gpt_markdown (`source_tag_style`), custom inline syntax | Library provides inline-extension hook; host supplies data and click behavior |
| Images: async/lazy load, data: URLs, failure fallback, download | Zed (background base64 decode), Textual (async attachment loaders), Streamdown | Library exposes an image-loader interface; host supplies caching/auth/network |
| Link safety: confirm dialog, safelist callback, scheme allowlist | Streamdown `linkSafety` + `onLinkCheck`; Zed routes clicks to the workspace | Library raises a `LinkClicked` event with a sanitized URL; the dialog is host |
| `<think>`/reasoning blocks | Zed renders thoughts as separate `Markdown` entities (`AssistantMessageChunk::Thought`) | **Host** (message model splits reasoning). Library may parse custom tags as a directive if inline in text |
| Tool-call cards | Zed: separate thread entries, not inside markdown | **Host** (message list), not markdown |
| Custom components / directives (`:::note`, `@mention`, `:emoji:`) | gpt_markdown `InlineMd/BlockMd`, Textual `SyntaxExtension`, Codex inline visualization directives | Library extension API (inline + block) |
| Mermaid | Zed native renderer with debounced zoom re-raster, code/diagram toggle; Codex replaces fence on close; Streamdown button | Library plugin, rendered only after the fence closes |
| Math | Textual (attachment), gpt_markdown (holds open formula), Codex (source preview until closed); Zed disables it | Library plugin; render after the delimiter closes, show source while open |
| Selection: cross-block, copy text vs copy markdown | Zed (source-offset model + rebalanced markdown), Textual (layout collection + coordinator), Flutter SelectionArea | **Library core** |
| Search highlights in message | Zed `set_search_highlights` (source ranges) | Library (ranges in source coordinates) |
| Streaming caret | Streamdown `caret` | Library option |
| Virtualization across messages | Zed `gpui::list`, gpt_markdown sliver | Host (message list); library supports block-level laziness for very long docs |

---

## 9. Key source links
- Zed markdown: https://github.com/zed-industries/zed/blob/main/crates/markdown/src/markdown.rs (parse ~L1204, Selection ~L1424, builder ~L3769, RenderedLine/SourceMapping ~L4427–4800)
- Zed reveal pacer: https://github.com/zed-industries/zed/blob/main/crates/acp_thread/src/acp_thread.rs (`StreamingTextBuffer`, `buffer_streaming_text`, `start_streaming_reveal`)
- Codex: https://github.com/openai/codex/tree/main/codex-rs/tui/src/streaming and `codex-rs/tui/src/markdown_stream.rs`
- Gemini CLI: https://github.com/google-gemini/gemini-cli/blob/main/packages/cli/src/ui/utils/markdownUtilities.ts
- JetBrains streaming parser: https://github.com/JetBrains/markdown/blob/master/src/commonMain/kotlin/org/intellij/markdown/parser/StreamingMarkdownFile.kt
- Compose: https://github.com/mikepenz/multiplatform-markdown-renderer/blob/main/multiplatform-markdown-renderer/src/commonMain/kotlin/com/mikepenz/markdown/model/StreamingMarkdownState.kt
- gpt_markdown: https://github.com/useval/gpt_markdown/tree/main/lib/streaming
- Textual: https://github.com/gonzalezreal/textual (Sources/Textual/Internal/TextInteraction)
- Vercel smoothStream: https://github.com/vercel/ai/blob/main/packages/ai/src/generate-text/smooth-stream.ts
- LobeHub smoothing: https://github.com/lobehub/lobehub/blob/main/packages/fetch-sse/src/fetchSSE.ts (`createSmoothMessage`)
- remend: https://github.com/vercel/streamdown/tree/main/packages/remend

---

## 10. Recommended architecture for Avalonia/.NET

1. **Three separate layers**:
   - (a) **Pacer**: a bounded-lag reveal (`speed = max(base, backlog/0.3s)`, fast-forward 150 ms on finish, char-boundary and grapheme safe). It outputs a "visible length" and should be optional.
   - (b) **Incremental document model**: append-only source, a settled boundary, and a mutable tail.
   - (c) **Renderer**: block controls cached by identity, plus one live tail.
2. **Settled boundary** combines the JetBrains and gpt_markdown rules. Take the last blank line outside an open fence, math block, or HTML comment, that is not a list continuation. Never settle the last top-level block. Keep a trailing list or indented code open. Never cut through a node. Fall back to a full reparse when reference link definitions appear (Codex rule). Parse only the tail with Markdig (`Markdown.Parse(tail)` with offsets rebased), on a background thread, coalesced Zed-style: one parse in flight plus a dirty flag, apply on the UI thread.
3. **Gate unstable syntax**:
   - Newline gating, or tail-only render of incomplete lines.
   - Table holdback until the delimiter row arrives.
   - Inline hold-back (48 / 160 char leash) instead of showing text and then restyling it.
   - Math and Mermaid show source until closed.
4. **Settled blocks are immutable**. Cache their Avalonia controls or visuals and their `TextLayout`s keyed by source start offset. Re-layout them only on width or theme change.
5. **Open code fence fast path**: keep the TextMateSharp `IStateStack` for the last line and tokenize only new lines. Treat a possible closing fence as a structural change.
6. **Selection in source-offset space** (Zed): each text run carries `SourceMapping[]`. A document-level hit-test index maps point → source offset. Selection is `(start, end)` source offsets painted per line. Copy offers plain text or rebalanced markdown. A coordinator (Textual) gives scrollable code and table regions their own local selection, and only one selection is active at a time.
7. Keep the **per-block UI state** (copied, wrapped, collapsed, scroll offset, mermaid zoom) in a map keyed by block source offset, so it survives reparses.
8. Make **tool cards, reasoning, and message virtualization** host concerns. The library provides extension points: a code-block header template, inline and block directive parsers, an image loader, a link-click event, and a highlighter interface.
