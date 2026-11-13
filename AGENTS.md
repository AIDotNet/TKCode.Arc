# TKCode.Arc agent guide

TKCode.Arc is an Arc-style (uiarc.dev) component library for Avalonia 12.1.5. Every package depends only on Avalonia and the in-house MIT `TKCode.Arc.Foundation` (token runtime, theme manager, package registration, localization; design/architecture/foundation.md).
Design source of truth: `design/` (start at `design/overview.md`).

## Layout

- `src/TKCode.Arc.Foundation` (+ `TKCode.Arc.Foundation.Generators`, shipped as its analyzer): token and theme runtime (`ArcThemeManager`, `ArcTheme`, presets), `AppBuilder.UseArc(...)` / `ArcBuilder`, language runtime (`TKCode.Arc.Localization`), generators for tokens, theme files and `Localization/*.xlf`.
- `src/TKCode.Arc.Core`: ArcDesign tokens, palette, motion engine (`TKCode.Arc.Motion`), shared primitives (`TKCode.Arc.Primitives`), Lucide glyphs.
- `src/TKCode.Arc`: controls, organized as `<Category>/<Component>/` (category = Arc sidebar group).
- `tests/`: `TKCode.Arc.Core.Tests` (pure), `TKCode.Arc.Tests` (headless Avalonia; the tests that use gallery demos are in ArcGallery), `TKCode.Arc.Foundation.Tests` (runtime + generator snapshots), `TKCode.Arc.BaseTheme.Tests` (base theme in an app with no other theme), `TKCode.Arc.Mobile.Tests`; `tests/Baselines/` holds the token and preset snapshots.
- The gallery and documentation site is its own repository, [AIDotNet/ArcGallery](https://github.com/AIDotNet/ArcGallery), checked out next to this one as `../ArcGallery` (its projects find this checkout through `ArcRoot`; section 'Gallery' below). It holds the shell, the docs pages (`Docs/<Category>/<Component>/<lang>.md`), the demos, the screenshots, the generators that build the API tables from this repository's XML docs, the desktop and browser hosts, `tools/docshots` and the tests that go through the demos (`tests/TKCode.Arc.Gallery.Tests`). Design notes: `design/engineering/gallery-app.md`.
- `tools/lucide/generate_lucide.py`: regenerates Lucide glyph geometry: Core's `ArcIcons.g.cs`, the `LucideIconKind` enum of `TKCode.Arc.Icons.Lucide` and the path data of `TKCode.Arc.Icons.Lucide.Generators` (`LucidePaths.txt`). That generator ships in the Icons.Lucide package and compiles only the icons a project uses into it (C#, XAML, `<LucideIcon Include="..." />` items, or `LucideIconsIncludeAll`); repository projects opt in with `ArcLucideGenerator=true`.

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
- Every Arc component needs gallery docs pages `Docs/<Category>/<Component>/{en-US,zh-CN,zh-TW}.md` in the ArcGallery repository (`../ArcGallery/gallery/TKCode.Arc.Gallery/`, checked by its `GalleryDocsTests`; design/engineering/gallery-app.md section 6.6), and its public properties and events need XML `<summary>` text here (they feed the API tables).
- Every new component or block gets the gallery's "New" badge: its catalog entry carries `AddedOn: "yyyy-MM-dd"` (the day it joins the gallery; the badge shows for `GalleryCatalog.NewWindowDays`). Arc components: the entry in ArcGallery's `Catalog/GalleryCatalog.g.cs` (keep it when regenerating); Extras, Special and Workflow: the entry in `GalleryCatalog.cs`; Blocks and Mobile: the `s_addedOn` table in `GalleryCatalog.cs` (checked by `GalleryNewBadgeTests`).
- Versions follow [Semantic Versioning](https://semver.org/): from 1.0.0 on, breaking API changes come only with a new major version. Record each release's changes and migration notes in `CHANGELOG.md`. A `-preview` suffix only allows changing members that no published package contains yet; for members that have shipped, preview included, the deprecation rule below applies.
- Public API is never removed or renamed in one step. The old member stays, marked `[Obsolete("Use X instead (deprecated in 1.Y.Z).")]` and forwarding to the new one, and is listed in `CHANGELOG.md` with its migration. It may be deleted only in a new major version, and only after it has carried `[Obsolete]` for at least two stable releases. This covers properties, events, methods, types, enum members and token names, and also changing a default, a parameter or what an event means. Code here and in ArcGallery moves to the new API in the same change and builds without CS0618, and a test keeps each obsolete member's forwarding working until it is removed.
- Item classes that a control lists (rows, options, actions: `SidebarItem`, `SegmentedOption`, ...) and controls with a selected state follow `design/engineering/implementation-guide.md` section 8: `IsEnabled` and `IsVisible` on every such item class (default `true`), `SelectionChanged` / `ValueChanged` for changes and `ItemInvoked` for "the user chose it".
- Releases go through one or more `X.Y.Z-preview.N` versions and end with a stable `X.Y.Z` of the same number; a new version number starts only after the previous one has shipped stable.
- Do not commit without an explicit request.

## Verify

Do not run the full test suites locally (a full `TKCode.Arc.Tests` run takes 8 minutes or more, and much longer when several builds share the machine); CI runs them. Build what you touched and run only the tests for the code you changed:

```bash
dotnet build src/TKCode.Arc -c Debug                    # or the package you changed
dotnet test tests/TKCode.Arc.Tests --filter "FullyQualifiedName~<Component>Tests"
```

## Gallery

A component change that shows in the gallery (docs, demos, catalog, screenshots) is a second change in `../ArcGallery`, with its own build and tests (see its `AGENTS.md`):

```bash
cd ../ArcGallery
dotnet build gallery/TKCode.Arc.Gallery.Desktop         # no GDOC warnings
dotnet test tests/TKCode.Arc.Gallery.Tests --filter "FullyQualifiedName~GalleryDocsTests|FullyQualifiedName~GalleryDemosTests|FullyQualifiedName~GalleryNewBadgeTests|FullyQualifiedName~GallerySmokeTests.Catalog_Lists_All_Components"
```

The gallery builds against this checkout (and CI builds it against `main`), so an API change that breaks a demo has to land in both repositories; the libraries' `InternalsVisibleTo` list includes `TKCode.Arc.Gallery.Tests` for the tests that go through the demos.

Run a whole suite only when asked, or for a change that reaches across the library (Core, Foundation, generators, the base theme).
