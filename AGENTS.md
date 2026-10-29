# TKCode.Arc agent guide

TKCode.Arc is an Arc-style (uiarc.dev) component library for Avalonia 12.1.4. Every package depends only on Avalonia and the in-house MIT `TKCode.Arc.Foundation` (token runtime, theme manager, package registration, localization; design/architecture/foundation.md).
Design source of truth: `design/` (start at `design/overview.md`).

## Layout

- `src/TKCode.Arc.Foundation` (+ `TKCode.Arc.Foundation.Generators`, shipped as its analyzer): token and theme runtime (`ArcThemeManager`, `ArcTheme`, presets), `AppBuilder.UseArc(...)` / `ArcBuilder`, language runtime (`TKCode.Arc.Localization`), generators for tokens, theme files and `Localization/*.xlf`.
- `src/TKCode.Arc.Core`: ArcDesign tokens, palette, motion engine (`TKCode.Arc.Motion`), shared primitives (`TKCode.Arc.Primitives`), Lucide glyphs.
- `src/TKCode.Arc`: controls, organized as `<Category>/<Component>/` (category = Arc sidebar group).
- `tests/`: `TKCode.Arc.Core.Tests` (pure), `TKCode.Arc.Tests` (headless Avalonia), `TKCode.Arc.Foundation.Tests` (runtime + generator snapshots), `TKCode.Arc.BaseTheme.Tests` (base theme in an app with no other theme), `TKCode.Arc.Mobile.Tests`; `tests/Baselines/` holds the token and preset snapshots.
- `gallery/`: gallery shell (home page, routes, top bar) and pages; `TKCode.Arc.Gallery.Shots` holds the docs screenshots and `TKCode.Arc.Gallery.Content` the pages' Markdown as resources (the Browser host serves both as files and fetches a page's text when it is shown, through `GalleryTexts`; section 6.11). A component's page is its docs page `Docs/<Category>/<Component>/<lang>.md` with live examples embedded by `<Demo name="Name" />`, each written as `Demos/<Category>/<Component>/<Name>.axaml` (plus `.axaml.cs` only when it needs logic); the demo's Code tab shows those files (sections 4 and 6.7). `TKCode.Arc.Gallery.Generators` turns `<!-- api: Type -->` into API tables from the TKCode.Arc XML docs, collects the demos and checks translations (design/engineering/gallery-app.md section 6). Shell text comes from `Localization/*.xlf` via `GalleryText` (hosts call `UseArcGallery()`; section 5.1). The sidebar's Docs section (Introduction, Installation, Theming, Motion, AI and MCP, Changelog) is `Guides/<Page>/<lang>.md`, read at run time by `GalleryGuides` (section 6.9). `TKCode.Arc.Gallery.Llms` writes the web site's files for AI assistants from the same pages (`llms.txt`, `llms-full.txt`, every page as Markdown, the agent skill; hand-written text in its `Content/`; section 6.10).
- `tools/lucide/generate_lucide.py`: regenerates Lucide glyph geometry.
- `tools/docshots`: checks a docs page without a build (`check`), captures its screenshots (`shoot`: each `![caption](images/Name.png)` line plus the `xml` block under it, light and dark, as lossless WebP files `images/Name.webp`; install `cwebp` for small files) and renders a gallery page (`page`); see design/engineering/gallery-app.md section 6.8.

## Implementation handbook

Read `design/engineering/implementation-guide.md` before writing a component: file layout, token rules, theme and motion patterns, the Core primitive catalog, performance and API rules, tests and known pitfalls.

## Non-negotiable rules

- Controls derive from Avalonia base types; only public Avalonia APIs are used. No AtomUI code or packages, and none of its source is consulted (clean room; design/decisions/0031-replace-atomui.md).
- Class names have no prefix; flat namespace `TKCode.Arc`; xmlns `https://github.com/AIDotNet/TKCode.Arc`.
- Every public control has a `[ControlDesignToken]` `<Control>Token` in the same namespace with at least one property (ARCF003). Its identity (class name minus `Token`, or `[ControlDesignToken(Identity = "...")]`) is unique across all Arc packages, Mobile included (Mobile uses `MobileButton`, `MobileSwitch`, ...; design/engineering/reserved-token-names.md). Token property types are limited to those in foundation.md section 3.4 (ARCF001).
- Colors, radii, shadows and fonts come from `{arc:ArcDesignTokenResource ...}`; control tokens hold geometry and accent-independent derived colors only.
- Themes live in `<Component>/Themes/<Component>Theme.axaml` with `x:Class="<RootNamespace>.<FileName>" x:ClassModifier="internal"` (flat, e.g. `TKCode.Arc.ButtonTheme`, `TKCode.Arc.Mobile.PageTheme`; the generator supplies the class and loads the file on first use; ARCF010–014). A `ControlTheme` key is defined by one file only (ARCF013). Never create `*Themes.axaml` aggregates.
- Avalonia built-ins inside templates (`ScrollViewer`, `ScrollBar`, `ItemsControl`, `PathIcon`, `TextSelectionHandle`, popup hosts) get their theme from Core's base theme (`src/TKCode.Arc.Core/Theme/Base/`), which steps aside when the app uses Fluent or Simple. Other built-ins (e.g. `TextBox`) have no template: use the Arc control. Scroll bars auto-hide; set `ScrollViewer.AllowAutoHide="False"` to keep them.
- Theme changes go through `ArcTheme.Apply(t => ...)` / `ArcThemeManager.Apply`; never block on `ArcThemeManager.ApplyAsync` from the UI thread (its publish step needs the UI thread, so `.Wait()` / `.Result` deadlocks; await it or call `Apply`).
- Transitions are declared in ControlTheme setters under `^:motion-full` / `^:motion-reduced` (set `arc:ArcMotion.Tracks="True"`); code-driven motion uses `MotionValue<T>` and must say why ControlTheme cannot express it.
- C# control-flow bodies always use braces. No reflection; AOT and trimming safe.
- Every Arc component needs gallery docs pages `gallery/TKCode.Arc.Gallery/Docs/<Category>/<Component>/{en-US,zh-CN,zh-TW}.md` (checked by `GalleryDocsTests`; design/engineering/gallery-app.md section 6.6), and its public properties and events need XML `<summary>` text (they feed the API tables).
- Every new component or block gets the gallery's "New" badge: its catalog entry carries `AddedOn: "yyyy-MM-dd"` (the day it joins the gallery; the badge shows for `GalleryCatalog.NewWindowDays`). Arc components: the entry in `gallery/TKCode.Arc.Gallery/Catalog/GalleryCatalog.g.cs` (keep it when regenerating); Extras, Special and Workflow: the entry in `GalleryCatalog.cs`; Blocks and Mobile: the `s_addedOn` table in `GalleryCatalog.cs` (checked by `GalleryNewBadgeTests`).
- Do not commit without an explicit request.

## Verify

```bash
dotnet build TKCode.Arc.slnx
dotnet test tests/TKCode.Arc.Core.Tests
dotnet test tests/TKCode.Arc.Foundation.Tests
dotnet test tests/TKCode.Arc.Tests
```
