# StreamMark changelog (standalone releases)

The releases of StreamMark as `TKCode.StreamMark.*`, before it moved into TKCode.Arc. From then on its packages are
`TKCode.Arc.StreamMark*`, versioned and recorded with Arc in the repository's [CHANGELOG.md](../../CHANGELOG.md).

## 0.1.2 — 2026-11-08

The last standalone release (commit `f911c50`); this is the code that moved into TKCode.Arc.

### Added
- Raw-content components: `ComponentSpec.Content = ComponentContent.Raw` (plugins: `BlockComponentOptions.Content`) keeps a component's body verbatim in `ComponentBlock.Raw`, for example `<tool-call>{json}</tool-call>`. The body never triggers tail completion or Markdown parsing, and every block component reports `IsOpen` until its closing tag arrives.
- `BlockComponentOptions.WhileStreaming` shows a plugin's view only after the component closes, and `BlockComponentOptions.Text` registers the text used for copy, selection, search and navigation.
- `MarkdownServices.CreateDocument`, `CreateStream` and `CreateEntry` build documents with the plugins' profile.
- Block actions can target code, tables and diagrams (`BlockActionTargets`).
- `BlockView.RunInBackground` runs a view's background work: it dedupes by key, cancels stale work, drops stale results and restarts after re-attaching.
- `PluginConfigurationException` names the plugin that failed (the failure is cached, not retried), and `PluginRegistry.Diagnostics` records replaced registrations.

### Changed
- `MarkdownView` reparses its own text when the services' profile changes, and reports a `ProfileMismatch` once for host documents built with another profile.
- Diagram renderers from plugins now win over `MarkdownServices.Diagrams`, as math and highlighting renderers already did.

### Performance
- Open fenced code blocks reuse the lines they have already assembled, so each streaming update only appends new lines. In `StreamDocBench` long-code, updates drop from 37.9 to 14.5 ms (4-char tokens) and from 4.78 to 1.83 ms (32-char tokens), with 33% less allocation.
- Transcript rows sit on a layer, so following the tail moves the layer instead of re-recording every row near the viewport. Position changes of realized rows while streaming 40 paragraphs drop from 1147 to 0.
- Plugin lookup tables are frozen dictionaries, and rendered diagrams are kept in a thread-safe LRU cache.

## 0.1.1 — 2026-10-27

### Fixed
- Selecting inline code (and `<kbd>` / `<mark>` runs) now shows the selection color over the run's background. The background used to be drawn on top of the selection, so selected code looked different from the surrounding selected text.

## 0.1.0 — 2026-10-24

The first stable release.
