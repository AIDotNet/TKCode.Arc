# Changelog

All notable changes to TKCode.Arc are listed here. Versions follow [Semantic Versioning](https://semver.org/);
while the version carries a `-preview` suffix the API may still change between releases.

NuGet packages published with each release: `TKCode.Arc`, `TKCode.Arc.Core`, `TKCode.Arc.Fonts`
and `TKCode.Arc.Icons.Lucide`.

## 0.1.1-preview.1 - 2026-10-21

### Added

- **Components**: the Activity, Content, Editors, Illustrations, Media and SpecialInputs categories, plus
  ButtonGroup, SplitButton, Card, Reactions, SwipeActions and the Color, Date, DateRange and Time pickers.
- **Core**: the `MotionTimeline` and `LiveLoop` motion drivers, the `SharedElement` primitives, `ArcBlur` and
  `ArcViewport`.
- **Blocks** (`TKCode.Arc.Blocks`): the AppShell blocks (Sidebar, PageHeader, CommandPalette, SearchResults) and the
  Settings blocks (SettingsPage, SecuritySettings, BillingOverview, UsageBilling, CancelFlow, SettingsCommand).
- **AdvancedCharts** (`TKCode.Arc.AdvancedCharts`): a new project that starts with the DataGrid control.
- **Gallery**
  - A component's page is its docs page with live examples embedded by `<Demo name="..." />`; each demo is an
    `.axaml` file and its Code tab shows that source.
  - A Docs section in the sidebar, and Cmd/Ctrl+K to focus the search from anywhere in the window.
  - API tables generated from the XML docs, with zh-CN and zh-TW translations; shell text and docs pages in
    en-US, zh-CN and zh-TW.
  - Markdown rendering and code highlighting through StreamMark `0.1.0-alpha.23`, the first release that runs in
    the browser.
  - A WebAssembly host (`TKCode.Arc.Gallery.Browser`) and app icons for the Desktop and Browser hosts.
- **Distribution**
  - The Gallery WebAssembly image is built on GitHub and pushed to
    `ghcr.io/atomuicommunity/tkcode-arc-gallery`; it is served with brotli, pre-compressed files and browser caching.
  - Release builds of the Gallery desktop app: Windows and Linux (unsigned), macOS (signed and notarized).
  - NuGet packing and publishing from `ci.yml` with nuget.org Trusted Publishing.
- **Tools**: scripts for the API docs translation pipeline and `tools/branding/build_icons.py`.
- A Simplified Chinese README and a Sponsors section.

### Changed

- The per-category `_Shared` helpers are replaced by the shared Core primitives.
- Gallery pages moved from per-category `Pages/*.cs` registrations to `Docs/` pages and `Demos/` examples.
- `design/` is no longer tracked in git.
- XML `<summary>` text added to public properties and events on many controls; it feeds the Gallery API tables.

### Fixed

- Motion: a loop waiting out a pause could leave the renderer idle with its frame request pending; frames now keep
  coming.
- HoldToConfirm, ConfirmMorph (hold mode), the Tabs panel and the Carousel viewport changed a reused clip without
  invalidating the clipped visual, so the hold fill did not move and a growing panel or resized carousel kept a stale
  clip.
- Countdown: a wheel at rest is drawn as a plain glyph, which halves the frame time of a ticking face.
- Slider and ElasticSlider threw from `Math.Clamp` when XAML set `Minimum` above the current `Maximum`
  (for example `Minimum="360" Maximum="900"`).
- Tabs no longer scrolls the page around it when the selected tab changes; the tab list reveals the selected tab
  itself.
- Gallery Browser: native Skia is built for WebAssembly and trimming is skipped on publish.

## 0.1.0-preview.1 - 2026-10-20

First preview release.

[0.1.1-preview.1]: https://github.com/AtomUICommunity/TKCode.Arc/compare/v0.1.0-preview.1...v0.1.1-preview.1
[0.1.0-preview.1]: https://github.com/AtomUICommunity/TKCode.Arc/releases/tag/v0.1.0-preview.1
