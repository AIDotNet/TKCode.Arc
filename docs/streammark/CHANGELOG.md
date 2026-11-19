# StreamMark changelog (standalone releases)

The releases of StreamMark as `TKCode.StreamMark.*`, before it moved into TKCode.Arc. From then on its packages are
`TKCode.Arc.StreamMark*`, versioned and recorded with Arc in the repository's [CHANGELOG.md](../../CHANGELOG.md).

## 0.1.2

The last standalone release; the source that moved into Arc is this release's code (commit `58c24ea`).

### Added
- Plugins: `MarkdownServices.CreateDocument` / `CreateStream` / `CreateEntry` build documents with the plugins' profile;
  `MarkdownView` reparses its own text when the services' profile changes and reports a `ProfileMismatch` once for host
  documents built with another profile. `MarkdownProfileBuilder.Build` returns the shared `Default` when nothing
  changes the syntax, so profiles compare by reference.
- Plugin failures throw `PluginConfigurationException` naming the plugin; replaced registrations are recorded in
  `PluginRegistry.Diagnostics`. Lookup tables are frozen; diagram renderers from plugins win over
  `MarkdownServices.Diagrams`.
- Block actions target code, tables and diagrams (`BlockActionTargets`), drawn by a shared `BlockActionBar`.
- Components can register their copy text (`ComponentTextCollector`).
- Raw-content components: `ComponentSpec.Content = Raw` keeps a component's body verbatim in `ComponentBlock.Raw`;
  `BlockComponentOptions` adds `Content`, `WhileStreaming` and `Text`.
- `BlockView.RunInBackground` dedupes by key and cancels stale work.

## 0.1.1 — 2026-10-27

### Fixed
- Selecting inline code (and `<kbd>` / `<mark>` runs) now shows the selection color over the run's background. The background used to be drawn on top of the selection, so selected code looked different from the surrounding selected text.

## 0.1.0 — 2026-10-24

The first stable release.
