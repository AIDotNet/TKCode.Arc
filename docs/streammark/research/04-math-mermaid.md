# Math / Chemistry / Mermaid rendering for an Avalonia 12 / .NET 10 NativeAOT streaming Markdown library

Research date: 2026-10-16. Everything below comes from repository sources, release metadata, NuGet indexes, or experiments run locally on macOS arm64 with .NET SDK 10.0.301. Statements marked "(assessment)" are my judgement, not measurements.

Local artifacts:
- Cloned sources: `<research-scratch>/src/{CSharpMath,xaml-math,RaTeX,mermaider,LiveMarkdown.Avalonia,Avalonia,zed}`
- CSharpMath NativeAOT probe: `<research-scratch>/csm-aot` (plus coverage probe `<research-scratch>/csm2`)
- RaTeX CLI test: `<research-scratch>/ratex-cli`
- Mermaider + Svg.Skia NativeAOT probe: `<research-scratch>/mmd-aot`

---

## 1. CSharpMath (verybadcat/CSharpMath)

- Repo: https://github.com/verybadcat/CSharpMath. 456 stars, last push 2026-08-31, MIT. It is a port of iosMath.
- Releases:
  - **v1.0.0-pre.2 (2026-08-15)**: "Update to Avalonia 12 (#259)", `\underbrace`/`\underbracket`.
  - v1.0.0-pre.1 (2026-02-13): all packages target net10.0 or netstandard2.0. Adds CSharpMath.Maui, CSharpMath.Uno, and CSharpMath.VectSharp (SVG/PDF output). `CSharpMath.Editor` is merged into the core package.
  - v0.5.2 (2025-12-11): removes `Enum.GetValues` for trim compatibility (#227).
  - The last stable package on NuGet is 0.5.1 (2021).
- NuGet: `CSharpMath.Avalonia` versions run up to `1.0.0-pre.2`. The repo pins `<AvaloniaVersion>12.0.4</AvaloniaVersion>` in `Directory.Build.props`. `CSharpMath.Avalonia.csproj` targets `net10.0`. A community fork, `Sylinko.CSharpMath.Avalonia 12.0.0`, is used by LiveMarkdown.Avalonia, which pre-dates the official Avalonia 12 support.
- **Rendering backend in Avalonia: no SkiaSharp.**
  - `CSharpMath.Avalonia/AvaloniaCanvas.cs` implements `ICanvas` directly on Avalonia `DrawingContext` with `DrawLine`, `FillRectangle`, `PushTransform`, and `AvaloniaPath`/StreamGeometry.
  - Glyph outlines come from the embedded **Typography (LayoutFarm)** font reader. It is compiled into `CSharpMath.Rendering` (netstandard2.0, `AllowUnsafeBlocks`) through a git submodule.
  - Glyphs are drawn as filled paths, not as text.
  - SkiaSharp is used only by the separate `CSharpMath.SkiaSharp` front end.
- **Fonts:** `CSharpMath.Rendering/Reference Fonts/latinmodern-math.otf`, `AMS-Capital-Blackboard-Bold.otf`, and `cyrillic-modern-nmr10.otf` are embedded resources. Layout uses the **OpenType MATH table** of Latin Modern Math (`BackEnd/MathTable.cs`).
  - Open issues #191, #107, #319–#330 (Aug 2026) plan to precompute font data. Today every bundled typeface is parsed at runtime and retained in memory.
  - Issue #323 says to "Validate generated fonts for package, trimming, and AOT", which is still open.
- **NativeAOT (measured).** A console app referencing `CSharpMath.Avalonia 1.0.0-pre.2`, published with `PublishAot=true` on osx-arm64:
  - **0 trim/AOT warnings**, 3.2 MB binary.
  - It ran correctly.
  - First use (font parse) took 15.5 ms. 1000 parse+layout(Measure) calls took **40.5 ms (~40 µs per formula)**.
  - Caveat: the probe only used `MathPainter.Measure`. It did not draw through Avalonia, so Avalonia was mostly trimmed away.
- **Coverage (measured): significantly below KaTeX for typical LLM output.**
  - Parser source (`CSharpMath/Atom/LaTeXParser.cs` `ReadTable`): the supported environments are only `matrix pmatrix bmatrix Bmatrix vmatrix Vmatrix array eqalign split aligned displaylines gather eqnarray cases`.
  - `aligned` must have exactly 2 columns. `array` vertical rules are "currently unsupported".
  - Probe of about 120 common commands. **Failed with "Invalid command" or "Unknown environment":**
    - Fractions and decorations: `\dfrac`, `\tfrac`, `\overset`, `\underset`, `\stackrel`, `\xrightarrow`, `\boxed`, `\cancel`, `\phantom`, `\overrightarrow`, `\overleftarrow`, `\overbrace`.
    - Delimiters and sizing: `\middle`, `\big`, `\Big`, `\bigl`, `\bigg`, `\lVert`, `\rVert`, `\lbrack`, `\rbrack`.
    - Fonts and text: `\mathscr`, `\boldsymbol`, `\textbf`, `\textit`, `\textrm`, `\mbox`, `\textcolor`, `\hspace`.
    - Operators and symbols: `\operatorname*`, `\mathop`, `\coloneqq`, `\lnot`, `\prime` ("won't be supported"), `\pmod`, `\substack`.
    - Structure: `\tag`, `\hline`, `\ce`.
    - Environments: `align`, `equation`, `alignat`, `gathered`, `smallmatrix`, `dcases`, `rcases`, and `aligned` with more than 2 columns.
  - **Worked:** `\frac`, `\sqrt[n]`, `\binom`, `\sum\limits`, `\int`, `\lim`, `\mathbb`, `\mathcal`, `\mathfrak`, `\mathbf`, `\mathrm`, `\bm`, `\text{…中文}`, `\operatorname`, `\hat`, `\widehat`, `\vec`, `\overline`, `\underbrace`, `\color`, `pmatrix`, `bmatrix`, `cases`, `aligned` (2 columns), `gather`, `split`, `\left…\right`, `{a \over b}`, and Greek letters and common relations.
  - Roughly 60–65% of an "LLM-typical" command sample works. No mhchem.
- **Error handling (measured):** `Painter.LaTeX = s` returns `ErrorMessage` with a caret position. It never throws.
  - `\frac{1}{` gives "Missing closing brace (pos 9)".
  - `\begin{pmatrix} a & b \\` gives "Missing \end for \begin{pmatrix}".
  - `\left(` gives "Missing \right…".
  - `x^` is silently accepted.
  - With `DisplayErrorInline=true` it paints the error text in red. LiveMarkdown sets it to false and shows the raw source instead.
- **Baseline:** `Painter.Measure()` returns `RectangleF(0, -Ascent, Width, Ascent+Descent)`, so the ascent and descent needed for baseline alignment are available.
- **Verdict (assessment):** Fully managed, AOT-clean, fast, and Avalonia 12-ready (pre-release). The coverage gaps are the blocker for "KaTeX parity". A macro pre-rewriter could cover some gaps (`\dfrac` → `{\displaystyle\frac..}`, `\boldsymbol` → `\bm`, `\textbf{x}` → `\mathbf{\text{x}}`), but sizing delimiters, `\tag`, `align`, `\middle`, `\overset` and mhchem need real engine work.

## 2. XAML-Math (WpfMath / AvaloniaMath) — ForNeVeR/xaml-math

- Repo: https://github.com/ForNeVeR/xaml-math. 720 stars, MIT, active dependency bumps (last push 2026-10-14). **The last release is v2.1.0 (2023-07-15)**, which moved AvaloniaMath to Avalonia 11. The unreleased 3.0.0 only drops net6/7.
- **There is no Avalonia 12 support:** `Directory.Packages.props` pins `Avalonia` to `[11.0.0,12.0.0)`. AvaloniaMath targets net462/netstandard2.0/net8.0.
- Rendering:
  - It is a JLaTeXMath port. It uses TeX fonts `cmex10/cmmi10/cmr10/cmsy10/jlm_msam10.ttf` and draws with `DrawingContext.DrawGlyphRun` (`AvaloniaElementRenderer.cs`).
  - Symbol and formula tables are XML resources parsed at startup (`TexSymbols.xml`, `PredefinedTexFormulas.xml`, …).
- **AOT hazard:** `XamlMath.Shared/TexPredefinedFormulaParser.cs:147` does `typeof(TexFormulaHelper).GetMethod(methodName, argTypes)!.Invoke(...)`. The method names come from XML, so reflection is driven by data, which the trimmer cannot see.
- Coverage: it supports `align` (#381) and `\cancel`. It has historically had weaker coverage than KaTeX. Avalonia-specific features are open help-wanted issues: Geometry rendering, SVG/PNG converters, ErrorTemplate.
- **Verdict:** Not viable without forking it for Avalonia 12 and removing reflection.

## 3. Other math engines

### 3a. RaTeX (erweixin/RaTeX), Rust, the strongest candidate
- Repo: https://github.com/erweixin/RaTeX. 1.5k stars, MIT, very active (push 2026-10-16). Latest release **v0.1.14 (2026-07-28)**; the release cadence is about monthly (0.1.11 → 0.1.14 between May and July).
- Claims ">99.5% KaTeX syntax coverage". It includes:
  - **mhchem** (`\ce`, `\pu`, a Rust port of the mhchem state machine).
  - Equation numbering (`\tag`, `\nonumber`, auto-numbered `align`/`gather`/`equation`), bussproofs, and `\slashed`.
  - Known gaps: `\includegraphics` and the `\html*` attributes.
  - It uses KaTeX golden tests against KaTeX reference PNGs.
- Pipeline: lexer → parser → layout → **DisplayList**. The crates are `ratex-ffi`, `ratex-svg`, `ratex-render` (tiny-skia PNG), `ratex-pdf`, `ratex-wasm`, and `ratex-unicode-font` (system CJK fallback).
- **C ABI** (`crates/ratex-ffi/include/ratex.h`, builds `cdylib` and `staticlib`):
  - `RatexResult ratex_parse_and_layout(const char* latex, const RatexOptions* opts)` with `display_mode` 0/1 and an optional color.
  - `ratex_free_display_list`, and a thread-local `ratex_get_last_error`. It is documented as **thread-safe**.
  - The output is **JSON DisplayList in em units**: `width`, `height` (ascent), `depth`, and items `GlyphPath{x,y,scale,font:"Main-Regular",char_code,color}`, `Line`, `Rect`, and `Path{MoveTo/LineTo/QuadTo/CubicTo/Close}`. The baseline is at `y = height`.
  - The schema is versioned (`docs/DISPLAYLIST_JSON_PROTOCOL.md`).
- This maps directly onto Avalonia (assessment):
  - Embed the 20 KaTeX TTFs (`fonts/`, 540 KB, OFL) via `EmbeddedFontCollection` and `FontManager.AddFontCollection` (both exist in Avalonia 12 `src/Avalonia.Base/Media`).
  - Draw `GlyphPath` as `GlyphRun`s, and `Line`/`Rect`/`Path` as rectangles and StreamGeometry.
  - Decode the JSON with System.Text.Json source generation (AOT-safe) and P/Invoke with `[LibraryImport]` (AOT-safe).
  - You could also static-link with NativeAOT `DirectPInvoke` + `NativeLibrary`.
- **Measured with the official v0.1.14 `render-svg` CLI (aarch64-apple-darwin, 3.4 MB including embedded fonts):**
  - Every command that CSharpMath rejected rendered OK: `\dfrac \tfrac \overset \xrightarrow \boxed \middle align equation \ce{H2SO4 + 2NaOH -> …} \mathscr \boldsymbol \textbf \Big \lVert \underset \stackrel \operatorname* smallmatrix gathered \hline \overrightarrow \overbrace \hspace \coloneqq \prime \textcolor dcases \cancel \phantom \tag \pmod \substack`, and `\text{中文}` through the system Unicode font fallback.
  - The visual output matches KaTeX style (KaTeX fonts).
  - **1000 formulas (parse + layout + SVG serialization + file writes) took 0.22 s, under 0.2 ms each.**
  - Partial input gives clean errors with positions: `\frac{1}{` → "Unexpected end of input in a macro argument" (pos 9); `\begin{pmatrix} a & b \\` → "Expected & or \\ or \cr or \end, got 'EOF'"; `\left(` → "Expected '\right'"; `x^` → "Expected group after '^'"; `\foo` → "Undefined control sequence".
- Costs and risks:
  - **No prebuilt desktop FFI library is published.** Release assets are CLIs, `RaTeX.xcframework`, an Android AAR, and JVM jars.
  - You must build `ratex-ffi` yourself for win-x64/arm64, linux-x64/arm64 and osx-x64/arm64 in CI with a Rust toolchain, then ship it in `runtimes/{rid}/native`.
  - Native size is probably about 2–3 MB per RID (assessment: the CLI including SVG and fonts is 3.4 MB).
  - It is a 0.x project led by one main author, though the pace is high.
  - JSON round-trip overhead is small compared with drawing.

### 3b. MicroTeX (NanoMichael/MicroTeX), C++
- https://github.com/NanoMichael/MicroTeX. 674 stars, MIT. **The last commit on both `master` and `openmath` was 2024-08-06**, so it is stalled.
- It needs a platform graphics backend: Cairo/GTK, Qt, GDI+, Android. The `openmath` branch uses OTF MATH fonts.
- P/Invoke would require writing a C shim plus a graphics callback layer.
- **Verdict:** inferior to RaTeX on maintenance, coverage and ABI cleanliness.

### 3c. JS engine route (Jint + KaTeX/MathJax)
- Jint (https://github.com/sebastienros/jint) v4.17.0 (2026-10-07). `Jint.csproj` sets `IsAotCompatible=true` for net7+.
  - Jint's own issue #3305 says the AOT diagnostics an embedder sees are "115 -> 67, and 58 of those are a floor".
  - Issue #3299 says "five interop shapes throw NotSupportedException" under NativeAOT.
  - Pure JS execution works; CLR interop has known gaps.
- **KaTeX (v0.19.0, 2026-10-01) does not output SVG.** It produces HTML+CSS and/or MathML, so it needs a DOM and CSS layout engine to display. That is useless without a browser.
- **MathJax 4.1.3** can emit SVG through its liteDOM adaptor, but:
  - It is a large JS bundle running in a tree-walking interpreter.
  - Then SVG must be parsed and rendered with Svg.Skia.
- **Verdict (assessment):** slowest, largest, and the most moving parts. Reject.

### 3d. Other Rust options
- **pulldown-latex** (https://github.com/carloskiki/pulldown-latex, 73 stars): outputs MathML only, about 95% of KaTeX features. It still needs a MathML layout engine, so reject.
- **Typst math via native lib + mitex** (https://github.com/mitex-rs/mitex, LaTeX→Typst converter, 0.2.7): it would embed the whole Typst compiler plus fonts per formula. Heavy and the semantics are indirect (assessment). Reject.

### 3e. What Zed does
- Zed (cloned 2026-10-15) **does not render math**. In `crates/markdown/src/parser.rs`, `Options::ENABLE_MATH` is in `UNWANTED_OPTIONS`, and `InlineMath`/`DisplayMath` events are ignored.
- For Mermaid it uses **merman** (Cargo `merman = "=0.8.0-alpha.5"`). See section 4.

### 3f. Existing Avalonia Markdown packages that render math
- `LiveMarkdown.Avalonia.Math 2.4.3` uses `Sylinko.CSharpMath.Avalonia`. `MathInlineNode` wraps a `MathView` in an `InlineUIContainer`, and on `ErrorMessage` it hides the view and shows the raw LaTeX with an "Error" class.
- `MarkView.Avalonia.Math`, `CodeWF.Markdown.Math` and `CodexGui.Markdown.Plugin.Math` are all CSharpMath-based.
- So the entire Avalonia ecosystem currently inherits CSharpMath's coverage gaps.

## 4. Mermaid without a browser

| Option | Language | Status | Integration | Notes |
|---|---|---|---|---|
| **Mermaider** ([nullean/mermaider](https://github.com/nullean/mermaider)) | Pure C# | NuGet `Mermaider 0.15.1`, MIT, 51 stars, pushed 2026-10-06; 36k downloads | Direct NuGet; `MermaidRenderer.RenderSvg/Parse/RenderAscii` | Own parser, Sugiyama layout and orthogonal router; claims 24 diagram types; "every public API proven NativeAOT in CI" |
| **merman** ([Latias94/merman](https://github.com/Latias94/merman)) | Rust | v0.9.0 (2026-10-12), Apache-2.0, 599 stars; **used by Zed** (PR [#57644](https://github.com/zed-industries/zed/pull/57644), merged 2026-05-27) | `merman-ffi` C ABI ("native ABI 3"); **no prebuilt C SDK**, build from source; also uniffi, Swift, Android, Flutter | Strives for Mermaid.js fidelity (pinned upstream fixtures). Zed needed a large SVG post-processing pass: "merman emits very accurate SVGs, but with borderline unusable CSS", then rasterized via usvg/resvg |
| mermaid-rs-renderer / mmdr ([1jehuang](https://github.com/1jehuang/mermaid-rs-renderer)) | Rust | v0.3.1 (2026-07), 1.8k stars | Rust lib/CLI only | "Under active early development… may not yet match mermaid-cli"; about 1.5–2.5 ms per diagram as a library |
| selkie ([btucker/selkie](https://github.com/btucker/selkie)) | Rust | 31 stars, "still actively under development" | Rust/WASM | Built by coding agents; immature |
| beautiful-mermaid ([lukilabs](https://github.com/lukilabs/beautiful-mermaid)) | TypeScript | 11k stars; 6 diagram types | Would need Jint | The source of Mermaider's design-token/theming idea; not worth a JS engine |
| D2 / Kroki / PlantUML | Go / server | — | — | D2 is a different language. Kroki is a server, so ruled out. |

**Measured, Mermaider 0.15.1 + Svg.Skia 5.2.3, NativeAOT osx-arm64:**
- **0 AOT warnings.** The binary is 19.5 MB plus libSkiaSharp/libHarfBuzzSharp.
- First render took 4.8 ms, then about **0.11 ms per flowchart**.
- Partial input (`graph TD\n A[Start --> `, a truncated sequence) **did not throw**; it produced partial output.
- **Problem:** the SVG uses `color-mix(in srgb, var(--fg) N%, var(--bg))` (30 occurrences) and `var(--…)` (83). Svg.Skia supports `var()` (issue #235 closed) but **not `color-mix()`**. The rasterized PNG shows black node fills and **missing edge strokes**.
- You need either:
  - (a) a small post-processor that resolves the `--_*` tokens in the `<style>` block to literal colors (easy; the grammar is fixed, per Mermaider's `DESIGN.md`), or
  - (b) native drawing from Mermaider's layout models.
- LiveMarkdown.Avalonia.Mermaid does (b): about 9k lines of Avalonia renderers (`MermaidPresenter`, `SequenceRenderer`, `ErRenderer`, …). It needs `IgnoresAccessChecksTo Mermaider` because `FlowchartParser`, `SequenceParser` and `SequenceLayout` are `internal`. That is fragile.
- Mermaider measures text with heuristic char-width tables (`src/Mermaider/Text/CharWidths.cs`), not real fonts, so label boxes may not match the Avalonia font exactly.

**Candid feasibility (assessment):**
- Mermaider via SVG plus a color-resolve pass plus Svg.Skia (`Svg.Controls.Skia.Avalonia`, `IsAotCompatible=true`) is the cheapest route. It is fully managed and AOT-proven, and quality is good for flowchart, sequence, class, state and ER.
- Ask upstream for a "resolved colors" option, or contribute one.
- merman is the fidelity upgrade path. It costs a Rust CI matrix and an SVG cleanup layer, as Zed found.
- Expect imperfect parity with mermaid.js on exotic diagram types in every option.

## 5. Streaming integration concerns

1. **Unclosed delimiters.**
   - Treat `$…$`/`\(...\)` inline math as plain or dimmed text until the closing delimiter arrives. Most streamdown-style implementations do this.
   - For `$$`/`\[`/fenced `math` blocks:
     - Re-render on each chunk, throttled to 50–100 ms. Keep the **last successful layout** visible when the new parse fails.
     - Both engines fail fast with an error position (measured above), so failures are cheap.
     - Optional: an "auto-closer" that appends missing `}`, `\right.` and `\end{env}` from a simple brace/env stack for progressive preview.
   - Mermaid should render only after the fence closes, or debounced. Mermaider happily renders truncated input, which flickers.
2. **Caching.**
   - RaTeX output is in **em units and independent of font size**, so cache by `(latex, displayMode)`, using a bounded LRU keyed by the string or a 64-bit hash with string equality.
   - Apply size at draw time. Apply theme color at draw time by passing a sentinel color and replacing it, so explicit `\color` survives.
   - CSharpMath layout is font-size dependent (`FontSize` is set before layout), so include size in its key.
3. **Off-UI-thread layout.**
   - RaTeX is documented thread-safe, so run it in `Task.Run` and build an immutable draw list.
   - CSharpMath Painter instances are not designed for sharing. Use one per task. Thread-safety of its global font cache was not verified.
   - Create Avalonia geometry/GlyphRun objects on first render on the UI thread, or cache per formula.
4. **Inline baseline (verified in Avalonia source, 2026-10-09).**
   - `src/Avalonia.Controls/Documents/InlineRun.cs`, `EmbeddedControlRun.Baseline`, returns `Control.DesiredSize.Height` (the bottom edge sits on the text baseline). The exception is when the child has the attached **`TextBlock.BaselineOffset`** set to a non-zero value.
   - So set `TextBlock.BaselineOffset = ascent * fontSize + Margin.Top` on the math control. RaTeX gives this as `height`; CSharpMath as `-Measure().Y`.
   - Zero means "unset", so use a tiny epsilon for zero-ascent formulas.
5. **Selection/copy.**
   - `InlineUIContainer.AppendText` contributes **U+FFFC** to `SelectableTextBlock`'s text.
   - Handle `SelectableTextBlock.CopyingToClipboard` (it exists in Avalonia 12) to substitute each U+FFFC with `$latex$` from an index map. For display blocks, provide a "copy LaTeX" action.
6. **Theming:** foreground brush from the theme for default-colored items, font size from the inherited `FontSize`, and horizontal `ScrollViewer` for wide display math.

## 6. Ranked recommendation

**Math**
1. **RaTeX through its C ABI, rendered natively in Avalonia.**
   - It has KaTeX-level coverage and mhchem, is fast, thread-safe and em-based, and is AOT-safe via `LibraryImport` and STJ source gen.
   - Cost: build a 6-RID Rust matrix in CI and write about 300–500 lines for the drawer.
2. **CSharpMath 1.0.0-pre.2** as a pure-managed fallback or a first milestone.
   - Measured: 0 AOT warnings, about 40 µs per formula, Avalonia 12.
   - It needs a macro pre-rewriter. Accept that it is not at KaTeX parity: `align`, `\Big`, `\tag`, `\overset`, `\boldsymbol` and mhchem are missing.
3. Rejected: XAML-Math (no Avalonia 12, reflection), MicroTeX (stalled), Jint+MathJax (heavy and slow; KaTeX has no SVG), pulldown-latex (MathML only), Typst/mitex (heavy).

**Mermaid**
1. **Mermaider (pure .NET, AOT-proven) → SVG → color-mix resolver → Svg.Skia.**
2. merman via `merman-ffi` as the fidelity upgrade. Zed uses it.
3. A native Avalonia drawer on Mermaider models (the LiveMarkdown approach) only if you are willing to own the internals-access hack.

## Sources
- CSharpMath: https://github.com/verybadcat/CSharpMath, releases https://github.com/verybadcat/CSharpMath/releases, issues #191 #323 #227
- CSharpMath.Avalonia on NuGet: https://www.nuget.org/packages/CSharpMath.Avalonia
- XAML-Math: https://github.com/ForNeVeR/xaml-math, NuGet https://www.nuget.org/packages/AvaloniaMath
- RaTeX: https://github.com/erweixin/RaTeX (ratex.h, docs/binding-architecture.md, docs/DISPLAYLIST_JSON_PROTOCOL.md), release v0.1.14
- MicroTeX: https://github.com/NanoMichael/MicroTeX
- Jint: https://github.com/sebastienros/jint (issues #3305, #3299)
- KaTeX: https://github.com/KaTeX/KaTeX; MathJax: https://github.com/mathjax/MathJax-src
- pulldown-latex: https://github.com/carloskiki/pulldown-latex; mitex: https://github.com/mitex-rs/mitex
- Mermaider: https://github.com/nullean/mermaider, https://www.nuget.org/packages/Mermaider
- merman: https://github.com/Latias94/merman (crates/merman-ffi/README.md); Zed PR https://github.com/zed-industries/zed/pull/57644
- mmdr: https://github.com/1jehuang/mermaid-rs-renderer; selkie: https://github.com/btucker/selkie; beautiful-mermaid: https://github.com/lukilabs/beautiful-mermaid
- Svg.Skia: https://github.com/wieslawsoltes/Svg.Skia (site/articles/advanced/trimming-aot-and-nativeaot.md, issue #235)
- LiveMarkdown.Avalonia: https://github.com/DearVa/LiveMarkdown.Avalonia (Math and Mermaid extension source)
- Avalonia source: `src/Avalonia.Controls/Documents/InlineRun.cs`, `InlineUIContainer.cs`, `SelectableTextBlock.cs`, `src/Avalonia.Base/Media/Fonts/EmbeddedFontCollection.cs`
