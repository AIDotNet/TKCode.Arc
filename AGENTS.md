# TKCode.Arc agent guide

TKCode.Arc is an Arc-style (uiarc.dev) component library for Avalonia built on AtomUI 6.2.6.
Design source of truth: `design/` (start at `design/overview.md`).

## Layout

- `src/TKCode.Arc.Core`: ArcDesign tokens, palette, motion engine (`TKCode.Arc.Motion`), shared primitives (`TKCode.Arc.Primitives`), Lucide glyphs.
- `src/TKCode.Arc`: controls, organized as `<Category>/<Component>/` (category = Arc sidebar group).
- `tests/`: `TKCode.Arc.Core.Tests` (pure), `TKCode.Arc.Tests` (headless Avalonia).
- `gallery/`: gallery shell and pages; each category registers its pages in `Pages/<Category>/<Category>Pages.cs` (partial `Register<Category>()`).
- `tools/lucide/generate_lucide.py`: regenerates Lucide glyph geometry.

## Implementation handbook

Read `design/engineering/implementation-guide.md` before writing a component: file layout, token rules, theme and motion patterns, the Core primitive catalog, performance and API rules, tests and known pitfalls.

## Non-negotiable rules

- Controls derive from Avalonia base types; only public AtomUI APIs are used (design/architecture/atomui-integration.md).
- Class names have no prefix; flat namespace `TKCode.Arc`; xmlns `https://github.com/AtomUICommunity/TKCode.Arc`.
- Every public control has a `[ControlDesignToken]` `<Control>Token` in the same namespace with at least one property (an empty token crashes at runtime) and no name from `design/engineering/reserved-token-names.md`; Semantic Part paths start with `arc.`.
- Colors, radii, shadows and fonts come from `{arc:ArcDesignTokenResource ...}`; control tokens hold geometry and accent-independent derived colors only.
- Themes live in `<Component>/Themes/<Component>Theme.axaml`; never create `*Themes.axaml` aggregates.
- Transitions are declared in ControlTheme setters under `^:motion-full` / `^:motion-reduced` (set `arc:ArcMotion.Tracks="True"`); code-driven motion uses `MotionValue<T>` and must say why ControlTheme cannot express it.
- C# control-flow bodies always use braces. No reflection; AOT and trimming safe.
- Do not commit without an explicit request.

## Verify

```bash
dotnet build TKCode.Arc.slnx
dotnet test tests/TKCode.Arc.Core.Tests
dotnet test tests/TKCode.Arc.Tests
```
