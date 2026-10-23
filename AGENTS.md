# TKCode.Arc agent guide

TKCode.Arc is an Arc-style (uiarc.dev) component library for Avalonia built on AtomUI 6.2.8.
Design source of truth: `design/` (start at `design/overview.md`).

## Layout

- `src/TKCode.Arc.Core`: ArcDesign tokens, palette, motion engine (`TKCode.Arc.Motion`), shared primitives (`TKCode.Arc.Primitives`), Lucide glyphs.
- `src/TKCode.Arc`: controls, organized as `<Category>/<Component>/` (category = Arc sidebar group).
- `tests/`: `TKCode.Arc.Core.Tests` (pure), `TKCode.Arc.Tests` (headless Avalonia).
- `gallery/`: gallery shell (home page, routes, top bar) and pages; `TKCode.Arc.Gallery.Shots` holds the docs screenshots and `TKCode.Arc.Gallery.Content` the pages' Markdown as resources (the Browser host serves both as files and fetches a page's text when it is shown, through `GalleryTexts`; section 6.11). A component's page is its docs page `Docs/<Category>/<Component>/<lang>.md` with live examples embedded by `<Demo name="Name" />`, each written as `Demos/<Category>/<Component>/<Name>.axaml` (plus `.axaml.cs` only when it needs logic); the demo's Code tab shows those files (sections 4 and 6.7). `TKCode.Arc.Gallery.Generators` turns `<!-- api: Type -->` into API tables from the TKCode.Arc XML docs, collects the demos and checks translations (design/engineering/gallery-app.md section 6). Shell text comes from `Localization/*.xlf` via `GalleryText` (hosts call `UseArcGallery()`; section 5.1). The sidebar's Docs section (Introduction, Installation, Theming, Motion, AI and MCP, Changelog) is `Guides/<Page>/<lang>.md`, read at run time by `GalleryGuides` (section 6.9). `TKCode.Arc.Gallery.Llms` writes the web site's files for AI assistants from the same pages (`llms.txt`, `llms-full.txt`, every page as Markdown, the agent skill; hand-written text in its `Content/`; section 6.10).
- `tools/lucide/generate_lucide.py`: regenerates Lucide glyph geometry.
- `tools/docshots`: checks a docs page without a build (`check`), captures its screenshots (`shoot`: each `![caption](images/Name.png)` line plus the `xml` block under it, light and dark, as lossless WebP files `images/Name.webp`; install `cwebp` for small files) and renders a gallery page (`page`); see design/engineering/gallery-app.md section 6.8.

## Implementation handbook

Read `design/engineering/implementation-guide.md` before writing a component: file layout, token rules, theme and motion patterns, the Core primitive catalog, performance and API rules, tests and known pitfalls.

## Non-negotiable rules

- Controls derive from Avalonia base types; only public AtomUI APIs are used (design/architecture/atomui-integration.md).
- Class names have no prefix; flat namespace `TKCode.Arc`; xmlns `https://github.com/AIDotNet/TKCode.Arc`.
- Every public control has a `[ControlDesignToken]` `<Control>Token` in the same namespace with at least one property (an empty token crashes at runtime) and no name from `design/engineering/reserved-token-names.md`; Semantic Part paths start with `arc.`.
- Colors, radii, shadows and fonts come from `{arc:ArcDesignTokenResource ...}`; control tokens hold geometry and accent-independent derived colors only.
- Themes live in `<Component>/Themes/<Component>Theme.axaml`; never create `*Themes.axaml` aggregates.
- Transitions are declared in ControlTheme setters under `^:motion-full` / `^:motion-reduced` (set `arc:ArcMotion.Tracks="True"`); code-driven motion uses `MotionValue<T>` and must say why ControlTheme cannot express it.
- C# control-flow bodies always use braces. No reflection; AOT and trimming safe.
- Every Arc component needs gallery docs pages `gallery/TKCode.Arc.Gallery/Docs/<Category>/<Component>/{en-US,zh-CN,zh-TW}.md` (checked by `GalleryDocsTests`; design/engineering/gallery-app.md section 6.6), and its public properties and events need XML `<summary>` text (they feed the API tables).
- Do not commit without an explicit request.

## Verify

```bash
dotnet build TKCode.Arc.slnx
dotnet test tests/TKCode.Arc.Core.Tests
dotnet test tests/TKCode.Arc.Tests
```
