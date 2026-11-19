# .NET Markdown parsing + code highlighting for a streaming, NativeAOT Avalonia 12 chat renderer

Date: 2026-10-16. All facts below come from source checkouts (git HEAD as of 2026-10-13..16), NuGet metadata, GitHub releases, and **benchmarks I ran locally** (macOS arm64, .NET SDK 10.0.301, `PublishAot=true`, osx-arm64 native binary). Benchmark sources are in `<research-scratch>/bench*`.

---

## 0. TL;DR

| Topic | Finding |
|---|---|
| Parser choice | **Markdig 1.4.0** (2026-09-20, BSD-2). No serious .NET alternative. CommonMark.NET last released 2017-02 (0.15.1), effectively dead. |
| AOT | Markdig 1.4.0 + TextMateSharp 2.0.4 + TextMateSharp.Grammars 2.0.4 + ColorCode.HTML 2.0.15 → **0 IL/AOT warnings** with `PublishAot`, `TrimmerSingleWarn=false`, on both SDK 10.0.301 and 11-preview; native binary runs correctly. Markdig does *not* set `IsAotCompatible` in its csproj, but its docs state it is designed for AOT/trimming and the only reflection is `typeof(Markdown).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()` (trim-safe). |
| Incremental parsing | Markdig has **none** (no incremental API, no reuse of prior parse; `Markdown.Parse` takes `string` only). Must be done by us: committed blocks + mutable tail. |
| Full reparse cost (AOT) | 20 KB doc, chat-ish extension set: **~0.37–0.44 ms, ~0.8–0.9 MB allocated per parse**. Linear ~18 µs/KB. Streaming a 20 KB answer in 4-char tokens with full reparse each token = **1.17 s CPU + 2.3 GB allocated** total. Tail-only = **5 ms + 11 MB** total (~230× cheaper). |
| Math | `UseMathematics()` supports **only `$…$` and `$$…$$`**. `\(…\)` and `\[…\]` are NOT recognized (verified). Need a custom parser. |
| Tables | 1.4.0 added `PipeTableOptions.UseGfmRules` → a **block-level** `GfmPipeTableParser` (strict GFM, verified against cmark-gfm). Default pipe tables are detected in the *inline* phase. |
| Highlighting | **TextMateSharp 2.0.4** (MIT, VS Code grammars, per-line state stack ⇒ perfect for streaming). AOT-clean; native `libonigwrap` 1.0.11 ships win x86/x64/arm64, linux x64/arm64 (glibc/musl/bionic), osx x64/arm64, maccatalyst, wasm(static). **No ios / android RIDs.** Steady 5–85 µs/line; cold first use of big grammar (C#) ≈ **365 ms** → must pre-warm off UI thread. |

---

## 1. Markdig

### 1.1 Version / project status
- NuGet: `Markdig` latest **1.4.0** (GitHub release 2026-09-20). Previous: 1.3.2 (2026-06-18), 1.3.0 (06-13), 1.2.0 (05-12), 1.1.3 (04-20), **1.0.0 (2026-02-22)**.
  - 1.0.0 notes: "Remove obsolete APIs (PR #926)", "Markdig 1.0 (PR #925)".
  - 1.4.0 notes: "Add strict GFM pipe table parsing mode", several emphasis/list/pipe-table fixes.
  - https://github.com/xoofx/markdig/releases
- TFMs: `net462; netstandard2.0; netstandard2.1; net8.0; net10.0` (`src/Markdig/Markdig.targets`). `AllowUnsafeBlocks=true`, `LangVersion=preview`.
- Docs site: https://xoofx.github.io/markdig (sources in repo `site/docs/…`, incl. `advanced/block-parsers.md`, `inline-parsers.md`, `parser-authoring-api.md`, `performance.md`, `ast.md`).
- CommonMark compliance: readme claims **600+ tests passing from CommonMark spec 0.31.2**. GFM: fenced code; pipe tables (permissive default, strict GFM via `UseGfmRules`); task lists; strikethrough via EmphasisExtras; autolinks via AutoLinks extension (not the exact GFM "extended autolink" spec but close); alerts (`[!NOTE]` …).

### 1.2 AST model
- `Markdig.Syntax.MarkdownObject` (base) → `Block` / `Inline`.
  - Blocks: `ContainerBlock` (`MarkdownDocument`, `ListBlock`, `ListItemBlock`, `QuoteBlock`, `Table`, `TableRow`, `TableCell`, `CustomContainer`, `FootnoteGroup`, `LinkReferenceDefinitionGroup`, `AlertBlock`…) and `LeafBlock` (`ParagraphBlock`, `HeadingBlock`, `CodeBlock` → `FencedCodeBlock`, `HtmlBlock`, `ThematicBreakBlock`, `MathBlock` (is a FencedCodeBlock), `LinkReferenceDefinition`…).
  - `LeafBlock.Lines` (`StringLineGroup` — slices of the original string), `LeafBlock.Inline` (`ContainerInline` root).
  - Inlines: `ContainerInline` (`EmphasisInline`, `LinkInline`), `LeafInline` (`LiteralInline`, `CodeInline`, `AutolinkInline`, `HtmlInline`, `LineBreakInline`, `HtmlEntityInline`, `MathInline`, `TaskList`, `PipeTableDelimiterInline` (transient)…).
- Location: every `MarkdownObject` has `SourceSpan Span` (`Start`, `End` **inclusive** char offsets into the parsed string, `Length`, `IsEmpty`), plus `int Line` (0-based) and `int Column`. `UsePreciseSourceLocation()` makes inline spans precise.
- Typed per-node data: `SetData/GetData`, `DataKey<T>` (1.x API) — handy to hang our view-model / control cache on nodes.
- Traversal: `document.Descendants<T>()`.
- `FencedCodeBlock.Info`, `.Arguments`, `.FencedChar`, `.OpeningFencedCharCount`, `.ClosingFencedCharCount` (0 ⇒ fence not yet closed — useful streaming signal; verified).
- Quirk (verified): an **unclosed** `FencedCodeBlock` reports `Span` covering only the opening line (`span[0,3]` for "```python\nprint(1)"). LiveMarkdown.Avalonia ships a `UseCodeBlockSpanFixer()` for exactly this. Use `Lines` instead of `Span` for content.
- Thread safety (docs `performance.md`): "`MarkdownPipeline` is immutable and thread-safe after `Build()`". Parsing uses pooled processors. The resulting `MarkdownDocument` is a mutable tree, not thread-safe — parse on a worker, hand off ownership to the UI thread, never share mutably.

### 1.3 Extensions relevant to chat
`MarkdownPipelineBuilder` extension methods (`MarkdownExtensions.cs`):
- `UsePipeTables(PipeTableOptions?)` — options incl. `RequireHeaderSeparator` (default true), **`UseGfmRules`** (new in 1.4.0: block parser `GfmPipeTableParser` + `RowParser` inserted before `ParagraphBlockParser`; "GFM cell boundaries precede all inline parsing").
- `UseGridTables()` (Pandoc grid tables — not needed for LLM output).
- `UseTaskLists()` → `TaskList` inline (`Checked`).
- `UseFootnotes()` → `Footnote`, `FootnoteGroup`, `FootnoteLink`.
- `UseEmphasisExtras(EmphasisExtraOptions)` — `Strikethrough` `~~`, `Subscript` `~`, `Superscript` `^`, `Inserted` `++`, `Marked` `==`. For chat: `Strikethrough` only (single `~` subscript collides with "~5 minutes").
- `UseAutoLinks()` — `http://`, `https://`, `ftp://`, `mailto:`, `www.`.
- `UseMathematics()` — `MathInlineParser` (opening char `$`) and `MathBlockParser` (opening char `$`). **Exact syntax: `$…$` inline (no newline, opening `$` must be preceded by whitespace/punctuation, no space right after `$`), `$$…$$` inline-display, and `$$` fenced block.** Verified: `\(y\)` and `\[\n z\n\]` are parsed as plain paragraphs. LLMs (OpenAI/DeepSeek/Qwen) emit `\(..\)`/`\[..\]` frequently → write custom `BackslashMathInlineParser` (opening char `\`, inserted before `EscapeInlineParser`) and `BackslashMathBlockParser` (LiveMarkdown.Avalonia does exactly this; see its `LiveMarkdown.Avalonia.Math/MathExtension.cs` and `BackslashMathBlockParserTests`).
- `UseAlertBlocks()` (GitHub `[!NOTE]`), `UseCustomContainers()` (`:::name` fenced divs → good host for directive-style custom blocks), `UseGenericAttributes()`, `UseDiagrams()` (only affects HTML rendering: mermaid stays a `FencedCodeBlock` with `Info=="mermaid"` in the AST), `UseAutoIdentifiers()`, `UseCjkFriendlyEmphasis()` (1.1.0; relevant for Chinese UI: fixes `**中文**。` emphasis-flanking issues), `UseYamlFrontMatter()`, `UseSoftlineBreakAsHardlineBreak()`, `DisableHtml()`.
- `UseAdvancedExtensions()` = most of the above except Emoji, SoftLine-as-Hardline, Bootstrap, YAML, JiraLinks, SmartyPants.

### 1.4 Writing custom parsers (API)
Two-phase: block (`BlockProcessor` line-by-line, `BlockParser.TryOpen/TryContinue/Close`, `BlockState.None/Skip/Continue/ContinueDiscard/Break/BreakDiscard`) then inline (`InlineProcessor`, `InlineParser.Match(InlineProcessor, ref StringSlice)`, `IPostInlineProcessor`). Dispatch is by `OpeningCharacters` (char map, no regex).

```csharp
// Directive / think-like fenced container:  :::think … :::   or  <think> … </think>
public sealed class ThinkBlock : ContainerBlock { public ThinkBlock(BlockParser p) : base(p) {} public bool Closed; }

public sealed class ThinkBlockParser : BlockParser
{
    public ThinkBlockParser() => OpeningCharacters = ['<'];
    public override BlockState TryOpen(BlockProcessor processor)
    {
        if (processor.IsCodeIndent) return BlockState.None;
        var line = processor.Line;
        if (!line.AsSpan().TrimEnd().SequenceEqual("<think>")) return BlockState.None;
        processor.NewBlocks.Push(new ThinkBlock(this) {
            Span = new SourceSpan(line.Start, line.End), Line = processor.LineIndex, Column = processor.Column });
        return BlockState.ContinueDiscard;               // swallow the opener
    }
    public override BlockState TryContinue(BlockProcessor processor, Block block)
    {
        if (processor.Line.AsSpan().Trim().SequenceEqual("</think>"))
        { ((ThinkBlock)block).Closed = true; block.UpdateSpanEnd(processor.Line.End); return BlockState.BreakDiscard; }
        return BlockState.Continue;                      // children (paragraphs, code…) parsed normally
    }
}
// register: builder.BlockParsers.Insert(0, new ThinkBlockParser());  (must precede HtmlBlockParser)
```
Notes: block parsers for containers return `Continue` and let child parsers run; `FencedBlockParserBase<T>` gives fence logic for free (used by `CustomContainerParser`, `MathBlockParser`). Inline parsers: set `processor.Inline = new MyInline{…}` and advance `slice`. Typical extension:

```csharp
public sealed class ChatExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder p)
    {
        if (!p.BlockParsers.Contains<ThinkBlockParser>()) p.BlockParsers.Insert(0, new ThinkBlockParser());
        if (!p.InlineParsers.Contains<BackslashMathInlineParser>())
            p.InlineParsers.InsertBefore<EscapeInlineParser>(new BackslashMathInlineParser());
    }
    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer) { } // we don't use HtmlRenderer
}
builder.Use<ChatExtension>();   // or builder.Extensions.AddIfNotAlready(new ChatExtension())
```
Why a dedicated `<think>` parser (or better, stripping it before Markdig): **verified** that `<think>\nreasoning\n\nmore\n</think>` becomes an `HtmlBlock` (type 7, `NonInterruptingBlock`) that ends at the first blank line, and `</think>` then becomes inline HTML inside the following paragraph. Recommended: handle `<think>`/reasoning at the stream layer (most APIs deliver reasoning in a separate channel anyway: Anthropic `thinking` blocks, OpenAI Responses `reasoning`, DeepSeek `reasoning_content`) and only fall back to a parser for inline `<think>` text.

Mermaid / other diagrams: no parser needed — handle `FencedCodeBlock` with `Info == "mermaid"` in our renderer.

### 1.5 Performance (my measurements, NativeAOT, osx-arm64, M-series)
Synthetic 20,323-char doc (headings, paragraphs with bold/code/links/`$x^2$`, nested list + task item, 3-col pipe table, C# fenced code, blockquote; repeated).

| Pipeline | ms / parse | alloc / parse |
|---|---|---|
| CommonMark only | 0.195–0.226 | 365 KB |
| + pipe tables (default, inline-phase) | 0.324 | 832 KB |
| + pipe tables `UseGfmRules=true` (block) | 0.294 | 752 KB |
| chat set (GFM tables, strikethrough, tasklists, autolinks, math, footnotes, alerts) | **0.376** | 789 KB |
| `UseAdvancedExtensions()` | 0.414–0.435 | 910 KB |

Most other extensions add < 10% each. Scaling (advanced, AOT): 300 chars 7 µs, 1 KB 16 µs, 2 KB 36 µs, 5 KB 86 µs, 10 KB 181 µs, 20 KB 358 µs ⇒ **linear, ~18 µs/KB, ~45 B alloc per input char**. (JIT before tier-up was 2–6× slower: first ~50 parses ≈2.6 ms — irrelevant for AOT.)

Streaming simulation (20 KB answer, 5,080 tokens of 4 chars):
- full reparse every token: **1,173 ms CPU, 2,330 MB allocated** (average parse 0.23 ms, rising to 0.42 ms at the end).
- tail-only reparse (from last committed boundary): **5 ms CPU, 11 MB allocated**.
- At realistic 50–150 tok/s, full reparse at the end of a 20 KB answer is ~2–6% of one core and 45–135 MB/s gen0 garbage; parsing alone is affordable with throttling (parse at most once per frame / 16–33 ms, as LiveMarkdown does with its coalescing `ParsePendingChangesAsync`), but the **real cost is downstream**: a fresh AST each time destroys node identity → full diff or full control rebuild. Tail-only keeps committed AST nodes (and their Avalonia controls) stable for free.

### 1.6 AOT / trimming
- Repo search: no `IsAotCompatible`/`IsTrimmable` in Markdig's csproj/targets. Reflection usage: only `Markdown.Version` (`Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()` — trim-safe). Docs `performance.md` section "AOT and trimming compatibility": "Markdig is designed to be compatible with Native AOT and IL trimming… Do not use `Activator.CreateInstance`…".
- Verified: test app with Markdig 1.4.0 + TextMateSharp 2.0.4 + Grammars 2.0.4 + ColorCode.HTML 2.0.15, `PublishAot=true`, `TrimmerSingleWarn=false` → **0 warnings**, binary 10.9 MB (+ 531 KB `libonigwrap.dylib`), runs correctly.
- Recommendation: still add `<TrimmerRootAssembly>`-free build and keep `IsAotCompatible=true` on *our* assemblies so analyzers flag our own reflection.

### 1.7 Incremental / partial parsing
- None. `Markdown.Parse(string, MarkdownPipeline?, MarkdownParserContext?)` and `MarkdownParser.Parse(string, …)` — whole-string only, no span/StringBuilder overload, no resume state. The `BlockProcessor` is internal-ish/pooled and closes all blocks at end of input; you cannot snapshot "open block stack" and continue later.
- Also note: no `ReadOnlyMemory<char>` input — tail parsing requires `string.Substring` (cheap for small tails) or keep the tail in its own string.

---

## 2. Alternatives

| Library | Status | Notes |
|---|---|---|
| **CommonMark.NET** (`CommonMark.NET` 0.15.1) | Last release **2017-02-20** | Port of cmark; spec 0.27-era; unmaintained. Markdig reuses its `EntityHelper`. Not an option. |
| **Markdown.ColorCode** 3.0.1 (2025-08-30) | Active-ish | Markdig extension that renders fenced code via ColorCode into **HTML**. Useless for a native Avalonia renderer except as reference. |
| **ColorCode.Core / ColorCode.HTML** 2.0.15 | Last release **2023-07-14** | Regex-based, ~20 languages, HTML/RTF formatters. AOT-clean (0 warnings). 6-line C# block: 50 µs (AOT). Not line-state-incremental, few languages, stale. |
| **ConsoleInk.Net** 0.1.7 (2026-05-20) | Small | The only other .NET "streaming markdown" package found; renders to ANSI console, CommonMark subset; reference links defined later render literally. Not reusable. |
| **LiveMarkdown.Avalonia** 2.4.3 (2026-09-19, Apache-2.0, Avalonia 12) | Active | Not a parser — a renderer on top of Markdig + TextMateSharp + CSharpMath + Mermaider. Streaming strategy (read `src/LiveMarkdown.Avalonia/Utilities/MarkdownUpdateProducer.cs`): **full `Markdown.Parse` of the whole snapshot on `Task.Run`, coalescing intermediate versions**, then diff the new document against the rendered tree. Supports only `Append`/`Clear`. Good prior art (and its BackslashMath parsers / CodeBlockSpanFixer), but we want stricter AOT and tail-only parsing. |
| Others (MdXaml, Markdown.Avalonia) | — | Older renderers using their own regex parsers or Markdig; no incremental support. |

No maintained .NET parser offers incremental/streaming CommonMark. (Outside .NET: `streaming-markdown` (JS, thetarnav), Vercel `streamdown` (remark + "remend" auto-closing of unterminated syntax), tree-sitter-markdown (incremental but native, two grammars, not CommonMark-exact).)

### 2.1 How hard is a hand-written incremental block parser?
CommonMark §"Appendix: A parsing strategy": phase 1 builds the block tree line by line (open-block stack; each line: match continuations of open containers, then open new blocks, then add text to the tip leaf); phase 2 parses inlines per leaf using the doc-wide reference map. Because phase 1 is a line-driven state machine, a **resumable** block parser is conceptually easy (keep the open-block stack, feed only new complete lines), *but*:
- Rewriting all CommonMark block rules (list item indentation, lazy continuation, 7 HTML block kinds, tabs-to-columns, setext/paragraph interplay, link ref defs at paragraph start) + GFM tables + extensions to spec-level correctness is weeks of work and ~650 spec examples to pass; Markdig already does this at 18 µs/KB.
- Inline phase cannot be incremental per leaf anyway (delimiter-run algorithm is per-leaf, cheap).
- Therefore: **don't write a parser; write a block *segmenter*** that decides where it is safe to cut, and let Markdig parse the uncommitted tail.

---

## 3. Streaming strategy: committed blocks + mutable tail

### 3.1 Model
```
full text  = [ committed region | tail region ]
committed  = list of top-level Markdig blocks (AST + Avalonia controls), never re-parsed during the stream
tail       = text from `commitOffset` to end; re-parsed with Markdig on each (throttled) update
```
After each tail parse: top-level blocks `0..n-2` of the tail document may be promoted to committed if the cut after them is *stable* (rules below); block `n-1` (and anything the rules exclude) stays in the tail. Spans of tail blocks are relative to `commitOffset` — store the base offset alongside (don't mutate Markdig spans, or do it once at promotion).

At **stream end**: run one full `Markdown.Parse` of the complete text (≤0.5 ms for 20 KB) and reconcile (diff by top-level block kind + source text hash). This fixes all the global, non-local effects (reference definitions, footnotes, list looseness edge cases) cheaply and once.

### 3.2 When is a cut "stable"? (CommonMark facts → rules)
A cut between top-level blocks A and B (at a line start) is stable iff no future text can change how A was parsed. Conditions:
1. **Blank line between A and B.** A paragraph can only be extended by a following non-blank line (continuation, lazy continuation inside quotes/lists, or a **setext underline** `===`/`---` turning it into a heading). After a blank line the paragraph is closed. Without a blank line (e.g. heading directly followed by paragraph, or a thematic break), cut is still stable if A is a single-line block that cannot absorb further lines: ATX heading, thematic break, closed fenced code, closed `$$` math block.
2. **A is not a `ListBlock` while B could still be absorbed.** A list survives blank lines: `- a\n- b\n\n- c` makes the whole list loose (verified `IsLoose=True`) and `- a\n\n  para` adds a paragraph to item 1 (verified). So a list is only stable once a *non-blank line with indentation < the list item content column* that is not a list marker of the same type has been seen (i.e., B exists and is a non-list block at column 0, after a blank line). Same for `QuoteBlock` only w.r.t. lazy continuation (blank line ends a quote — verified lazy continuation `> quote\nlazy` stays in quote).
3. **Fenced code / `$$` math / custom containers (`:::`) / our think block**: open until the closing fence. Check `FencedCodeBlock.ClosingFencedCharCount > 0` (or `MathBlock` equivalent). Once B exists after it, Markdig has closed it, but if the closing fence was missing the block runs to end-of-text, so it is always the last block → never committed until closed.
4. **Indented code blocks** may contain blank lines and continue after them → treat like lists (stable only when followed by a non-indented, non-blank line).
5. **HTML blocks**: types 1–5 (`<pre|script|style|textarea`, `<!--`, `<?`, `<!X`, `<![CDATA[`) end only at their specific terminator; types 6–7 end at a blank line. Treat like fences: commit only when B exists.
6. **Tables**: header row arrives before the delimiter row — until `|---|---|` is complete the header is a paragraph (verified: `| a | b |` and `| a | b |\n|---` are `ParagraphBlock`; `|---|---|` → `Table`). A table ends at a blank line (or a non-table line in GFM mode). Since tables are always the *tail* block while growing, this only causes visual flicker (paragraph → table), not correctness issues. Optional UX: if the tail paragraph's last line starts with `|`, render it as "pending table".
7. **Link reference definitions and footnote definitions** are document-global and may appear *after* their uses (`see [foo]` becomes a link only once `[foo]: http://x` exists — verified). They do not change block structure, only inline resolution in committed blocks. Options: (a) ignore during stream, fix at end-of-stream full parse (recommended; LLMs rarely use them, except footnotes in some "research" answers); (b) on detecting a new `LinkReferenceDefinition`/`Footnote` in the tail, re-run inline-only on committed paragraphs containing `[`. Also pass accumulated definitions into tail parses: copy committed `LinkReferenceDefinition`s into the tail doc via `MarkdownParserContext`/`DocumentProcessed` hook, or simplest: re-parse tail with the definitions text prepended (cheap, definitions produce no visible blocks).
8. **Setext flicker inside the tail**: verified that `Title text\n-` (a single `-` just arrived) is a level-2 `HeadingBlock`; it will become a list (`- item`) a few tokens later. Same for `===`, `` ` `` → fence, `|` → table. Mitigation: **hold back the last incomplete line** (no trailing `\n`) for block-structure decisions when it consists only of `-`, `=`, `` ` ``, `~`, `|`, `:`, spaces, or a list marker; render it as plain text appended to the previous paragraph, or just delay by one token.
9. **Unclosed inline syntax** in the tail (`this is **bold`) parses as literal `**` (verified) — flicker when closed. Optional "remend"-style pre-pass (as Vercel streamdown does): temporarily append matching closers (`**`, `` ` ``, `$`, `]()`) to a *copy* of the tail before parsing, only for display.
10. Top-level only: never cut inside a container (list/quote); the unit of commitment is a top-level child of `MarkdownDocument`.

Rule-of-thumb implementation: after parsing the tail, iterate top-level blocks `b[i]`, `i < n-1`; promote `b[i]` if `IsSelfClosing(b[i]) || (BlankLineBetween(b[i], b[i+1]) && !(b[i] is ListBlock or CodeBlock{indented} or QuoteBlock && b[i+1] could continue it))`. Stop at the first non-promotable block (commit must be a prefix). Advance `commitOffset` to `b[i+1]` start line.

### 3.3 Cost quantified
- Tail is typically 1 paragraph / 1 growing code block / 1 table: 0.2–5 KB ⇒ 7–90 µs per parse, ~10–200 KB alloc. 
- Pathological tail: a single 20 KB fenced code block never closes until the end → tail-only degenerates to full reparse of that block (~0.2 ms each, cheap for code since there are no inlines). Better: special-case an open fenced code block as the tail — don't call Markdig at all, just append lines to the code view and tokenize new lines with TextMate (state per line).
- Same for one giant list (LLMs love long bullet lists). Acceptable (lists are rarely > 5 KB), or allow committing list items `0..k-2` of a top-level list visually while still re-parsing the list (only re-render the last item).

---

## 4. Code syntax highlighting

### 4.1 TextMateSharp
- NuGet `TextMateSharp` / `TextMateSharp.Grammars` **2.0.4** (2026-06-04; 2.0.3 2026-02-03), MIT, by Daniel Peñalba; port of `microsoft/vscode-textmate` via `eclipse/tm4e`. https://github.com/danipen/TextMateSharp
- Targets `netstandard2.0`; grammars/themes are embedded resources loaded with `Assembly.GetManifestResourceStream` (AOT-fine); JSON parsed by an internal **SimpleJSON** parser (no System.Text.Json reflection). Verified 0 AOT warnings + runs under NativeAOT.
- Regex engine: **Oniguruma via `Onigwrap` 1.0.11** (MIT, Aikawa Yataro, P/Invoke to `libonigwrap`). Native assets in the package: `win-x64`, `win-x86`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `linux-bionic-x64/arm64`, `osx-x64`, `osx-arm64`, `maccatalyst-x64/arm64`, and static `libonigwrap.a` for browser-wasm (emscripten 2.0.23/3.1.x). **No `ios-*` or `android-*` RIDs.** For desktop NativeAOT the `.dylib/.so/.dll` is copied next to the executable (verified); single-file static linking would need our own static lib + `DirectPInvoke`.
- Grammars bundled: 50+ (C#, C/C++, Java, JS/TS, Python, Rust, Go, Swift, Dart, PHP, SQL, HTML, CSS/SCSS/Less, JSON, XML, YAML, Markdown, LaTeX, Lua, PowerShell, Bash, F#, VB…). Themes: Dark+, Light+, One Dark, Monokai, Dracula, Solarized, GitHub-ish VS Light/Dark, High Contrast, etc. (`ThemeName` enum). Cross-grammar injections not supported (so embedded languages in Markdown/HTML partially lose highlighting — irrelevant for fenced blocks).
- API:
```csharp
var options  = new RegistryOptions(ThemeName.DarkPlus);
var registry = new TextMateSharp.Registry.Registry(options);
IGrammar g   = registry.LoadGrammar(options.GetScopeByLanguageId("csharp")); // or GetScopeByExtension(".cs")
Theme theme  = registry.GetTheme();
IStateStack? state = null;                        // per line: keep the END state of each line
foreach (var line in lines) {
    ITokenizeLineResult r = g.TokenizeLine(line, state, TimeSpan.FromMilliseconds(50));
    foreach (IToken t in r.Tokens)                 // t.StartIndex, t.EndIndex, t.Scopes
        foreach (var rule in theme.Match(t.Scopes)) { /* theme.GetColor(rule.foreground), rule.fontStyle */ }
    state = r.RuleStack;
}
// Faster binary form: ITokenizeLineResult2 TokenizeLine2(LineText, IStateStack, TimeSpan) → int[] Tokens
// (pairs of startIndex + packed metadata incl. foreground color id / font style) — no scope strings.
```
- `IStateStack` implements `Equals` — the key to incremental highlighting: AvaloniaEdit/TMModel re-tokenizes from the first changed line and **stops as soon as a line's new end state equals its old end state** (`TMModel.UpdateTokensInRange`, line ~650: `r.EndState.Equals(endStateLine.State)`).

Measured (NativeAOT, arm64), lines of typical code, state carried across lines:

| Grammar | load | first block (cold regex compile) | steady µs/line | alloc B/line |
|---|---|---|---|---|
| csharp | 2.2 ms | **365 ms** | 72 | 6.7 K |
| typescript | 2.0 ms | 51 ms | 63 | 7.3 K |
| python | 1.3 ms | 4.7 ms | 55 | 5.9 K |
| shellscript | 55 ms | 38 ms | 17 | 3.3 K |
| json | 0.1 ms | 0.2 ms | 5.7 | 4.4 K |
`TokenizeLine2` ≈ 10% faster than `TokenizeLine` (C#).

Implications for streaming:
- A streaming code block only tokenizes **new complete lines** using the stored end state of the previous line, plus re-tokenizes the current partial last line on each token (≤ ~100 µs). A 200-line block costs ~15 ms total over the whole stream instead of 200×200/2 line tokenizations.
- Cold grammar start is the dominant cost (C# 365 ms!). Load the registry once (singleton, background), lazily load grammars on a worker thread when the opening fence `` ```lang `` arrives (we know the language before the first code line), and optionally pre-warm the top ~8 languages at startup on a low-priority thread. Render plain monospace text until tokens are ready, then swap colors (no layout change).
- Thread safety: one `IGrammar` instance should not be used concurrently from multiple threads without care (TMModel uses one tokenizer thread per model); use a single dedicated highlighting worker (Channel<T>) and post results to the UI thread.
- Use a timeout per line (`TimeSpan`) — pathological regexes on very long lines (minified JS) exist; AvaloniaEdit uses `MAX_LEN_TO_TOKENIZE` and 3000 ms.

### 4.2 AvaloniaEdit integration (reference)
- `AvaloniaEdit.TextMate` 12.0.0 (Avalonia 12; TFMs net8.0/net10.0). `TextMate.InstallTextMate(editor, registryOptions)` → `Installation` with `SetGrammar(scope)`, `SetTheme(IRawTheme)`, `TryGetThemeColor(key, out string)`. Internals: `TextEditorModel : AbstractLineList` (adapts `TextDocument` lines; `DocumentSnapshot`), `TMModel` (TextMateSharp; background `TokenizerThread`, invalidation ranges, state-equality early stop), `TextMateColoringTransformer` (`GenericLineTransformer` → applies foreground/background/font style per visual line, listens to `ModelTokensChanged`).
- For chat bubbles, a full `TextEditor` per code block is heavy (and AvaloniaEdit's AOT status should be checked separately). Better: reuse the same idea in a light control — keep `List<(string line, IStateStack endState, int[] tokens)>`, render with `TextLayout`/`SelectableTextBlock` inlines or a custom `Control` drawing `FormattedText`/`GlyphRun`s per line, virtualize long blocks.

### 4.3 Other options
- **ColorCode-Universal** (`ColorCode.Core` 2.0.15, 2023): .NET `Regex`-based, ~20 languages, no per-line state API (whole-block colorize), HTML/RTF formatters only. Under NativeAOT, `RegexOptions.Compiled` is ignored → interpreter. 50 µs for a 6-line C# block → whole-block recolorize on each token gets quadratic for long blocks. Viable as a no-native-dependency fallback (e.g. iOS/Android where onigwrap is missing), not as primary.
- Write grammar-free lexers for top languages: high effort, not recommended.
- Tree-sitter via C# bindings: native per-platform libs per grammar, much heavier packaging; no clear .NET AOT-ready package.

---

## 5. Recommendations
1. **Parser = Markdig 1.4.0**, pipeline built once (static, thread-safe): `UsePipeTables(new PipeTableOptions { UseGfmRules = true })`, `UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)`, `UseTaskLists()`, `UseAutoLinks()`, `UseMathematics()`, `UseFootnotes()`, `UseAlertBlocks()`, `UseCjkFriendlyEmphasis()`, `UseCustomContainers()` (directives), optional `UsePreciseSourceLocation()`, plus our `ChatExtension` (backslash math `\(..\)`/`\[..\]`, think/directive blocks). Probably `DisableHtml()` or a sanitizing HTML policy (we don't render HTML anyway).
2. **Streaming = committed top-level blocks + tail reparse** with the stability rules in §3.2, special fast path for an open fenced code block (no Markdig; append lines + TextMate), last-incomplete-line hold-back for setext/fence/table ambiguities, optional closer-completion ("remend") for display, and **one full reparse + reconcile at stream end**. Throttle tail parses to ≤ 1 per frame and run them off the UI thread; parsing is so cheap (µs) that running on the UI thread is also acceptable for the tail.
3. **Our AST → view model layer**: never bind Avalonia controls to Markdig objects directly across reparses; convert each committed top-level block once into an immutable render model keyed by (kind, source hash), and diff only the tail block(s).
4. **Highlighting = TextMateSharp 2.0.4 (+Grammars)** on a single background worker, per-line `IStateStack` caching, `TokenizeLine2` + theme color map, lazy grammar load at fence-open, prewarm common grammars, per-line timeout. ColorCode as fallback where `libonigwrap` is unavailable (iOS/Android) — or ship our own onigwrap builds for those RIDs.
5. **AOT gate**: keep a CI job publishing a NativeAOT sample with `TrimmerSingleWarn=false` and `TreatWarningsAsErrors` for IL2xxx/IL3xxx; current dependency set produces 0 warnings.

## Sources
- Markdig repo/docs: https://github.com/xoofx/markdig (HEAD e068f06, 2026-10-15), https://xoofx.github.io/markdig, `site/docs/advanced/{block-parsers,parser-authoring-api,performance}.md`, `site/docs/extensions/mathematics.md`, `src/Markdig/Extensions/Mathematics/*.cs`, `src/Markdig/Extensions/Tables/GfmPipeTableParser.cs`
- Markdig releases: https://github.com/xoofx/markdig/releases/tag/1.4.0 , https://github.com/xoofx/markdig/releases/tag/1.0.0 , https://www.nuget.org/packages/Markdig
- TextMateSharp: https://github.com/danipen/TextMateSharp (HEAD e0db45b, 2026-10-13), `src/TextMateSharp/Model/TMModel.cs`; https://www.nuget.org/packages/TextMateSharp
- Onigwrap: https://github.com/aikawayataro/Onigwrap , https://www.nuget.org/packages/Onigwrap/1.0.11
- AvaloniaEdit.TextMate: https://github.com/AvaloniaUI/AvaloniaEdit/tree/master/src/AvaloniaEdit.TextMate
- LiveMarkdown.Avalonia: https://github.com/DearVa/LiveMarkdown.Avalonia (`Utilities/MarkdownUpdateProducer.cs`)
- CommonMark.NET: https://www.nuget.org/packages/CommonMark.NET (0.15.1, 2017-02-20)
- ColorCode-Universal: https://github.com/CommunityToolkit/ColorCode-Universal , https://www.nuget.org/packages/ColorCode.Core
- Markdown.ColorCode: https://github.com/wbaldoumas/markdown-colorcode
- ConsoleInk.Net: https://www.nuget.org/packages/consoleink.net
- CommonMark spec 0.31.2 + parsing strategy appendix: https://spec.commonmark.org/0.31.2/#appendix-a-parsing-strategy
- Benchmarks/probes: `<research-scratch>/bench` (stream sim, TextMate, ColorCode), `bench2` (per-extension cost, scaling), `bench3` (TextMate per grammar), `bench4` (streaming-pitfall probes)
