---
title: Changelog
---

# Changelog

All notable changes to TKCode.Arc, newest first. Versions follow [Semantic Versioning](https://semver.org/). While a
version carries a `-preview` suffix, the API may still change between releases; read the notes before upgrading and
keep the version pinned in your project. The same list is kept in `CHANGELOG.md` in the repository.

NuGet packages published with each release: `TKCode.Arc`, `TKCode.Arc.Core`, `TKCode.Arc.Fonts` and
`TKCode.Arc.Icons.Lucide`, `TKCode.Arc.Blocks`, `TKCode.Arc.AdvancedCharts` and `TKCode.Arc.Special`.

## 0.3.0-preview.4 (2026-10-24)

### Added

- **Special components › AI**: five agent UI components in `TKCode.Arc.Special`: `AgentStatus` (a live status pill that
  opens into the recent steps), `ToolCall` and `ToolCallGroup` (tool call rows with input and output, approval and
  retry), `AgentPlan` (an editable draft plan that becomes a live checklist), `AgentTimeline` (a replay of a multi-agent
  run on one time axis) and `ContextWindow` (a 200-cell token budget grid with a compaction threshold).
- **Blocks › Headers and footers**: `SiteHeader`, `SiteFooter`, `CtaSection` and `NewsletterSignup`, ported from Arc's
  free blocks.
- **Blocks › AI**: the model arena (`ModelArena`, a blind test of two models with streamed answers, a vote and
  standings) and document extraction (`DocumentExtraction`, a document beside a form that fills in as the model reads,
  with links from each value to its source text). All nine AI blocks are now available.
- **AnnouncementBar**: `DismissalDuration` lets a remembered dismissal expire.

### Changed

- **Gallery**: the language has its own menu in the top bar, starts from the system language and is remembered. The
  home page has a Friends section, and a docs page shows a placeholder while it loads.

### Fixed

- **Combobox** (#9): a focused field no longer draws a dark ring; only its border turns `BorderStrong`, as in Arc.
- **Gallery** (#8, #10): the examples no longer cut off the left side of the inline edit frame and of focus rings.
- **Gallery** (#11): a NativeAOT publish of the desktop gallery loads its docs pages and screenshots again.

## 0.3.0-preview.3 (2026-10-23)

### Fixed

- **Select**: the option highlight was square; it now takes the option corner radius of the theme (14).
- **Select**: with the Full motion level the menu faded out after a pick, over the text below the field. It now closes at
  once, as documented; `ArcPopup` has a new `IsExitInstant` for this. Reduced still fades out.
- **Combobox, MorphSelect**: clicking the input on macOS could end the app with an `NSInvalidArgumentException`. The
  accessibility live region no longer receives an empty text.

## 0.3.0-preview.2 (2026-10-23)

### Added

- **Special components**: five animated AI explainers in the new package `TKCode.Arc.Special` (`UseArcSpecial()`):
  `AgentLoop`, `MixtureOfExperts`, `SpeculativeDecoding`, `PromptCache` and `ReasoningTree`, built on the shared
  `Explainer` base. Every number is computed from the data.
- **Blocks**: five Projects blocks (`WeekCalendar`, `AvailabilityPicker`, `ProjectBoard`, `ReleaseReadiness`,
  `KanbanBoard`), six Showpieces (`ActivityTerrain`, `RevenueGlobe`, `CustomerGalaxy`, `SemanticZoom`, `LayoutMorph`,
  `ExplodedView`) and the first AI blocks (Model picker, Agent memory, AI composer, AI chat, AI answer, plus the side
  panel and prompt studio).
- **Core**: `ArcSurface.Elevation` (dialogs, drawers, popovers and menus content knows it sits on a raised surface),
  `RollingDigitsFalloff.None`, new springs, `ArcContentSwap` modes, `ArcRing`, `WordDiff`, `WordPacer`,
  `DecoratedTextBlock` and 23 more Lucide glyphs.
- **Button**: a ghost button on a raised surface now has a visible hover and press fill.
- **Special inputs**: `TokenTextEditor`, a multi-line editor whose tokens act as single units; `MentionInput` is built
  on it.
- **Gallery**: the docs site serves `llms.txt`, `llms-full.txt`, every page as Markdown and an agent skill for AI
  assistants.

### Changed

- **Breaking, repository move**: the repository is now `AIDotNet/TKCode.Arc` and the XAML namespace follows it. Change
  `xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"` to `xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"`
  (and the Lucide namespace likewise).
- **Sidebar**: `ResizeMode` now defaults to `Overlay`; set `ResizeMode="Push"` for the previous behavior.
- **Performance**: the motion scheduler advances each driver once per frame, hidden loops cost nothing, cached blur
  effects and text layouts, and a much cheaper ActivityTerrain and LayoutMorph.
- **Gallery**: the WebAssembly site downloads about 1.5 MB less before its first frame.

### Fixed

- **Tabs**, **PageHeader**, **Breadcrumb**, **ArcContentSwap**, **DataGrid**: templates are no longer built with a
  `null` data context.
- **Core**: a content swap that left the window mid-change no longer comes back half transparent; anchored popups draw
  their shadow with rounded corners again.
- **Sidebar**: content no longer keeps the first-frame width after the sidebar opens from the rail.
- **Motion**: fading a fill from or out to `Transparent` no longer flashes a gray (`ArcBrushTransition` mixes with
  premultiplied alpha).
- **TagInput**, **Textarea**, **TimePicker**: the heavy focus ring is gone; `FieldFrameToken` `FocusRing*` values are
  removed.

The full list is in `CHANGELOG.md` in the repository.

## 0.3.0-preview.1 (2026-10-22)

### Added

- **Core**: `ArcZoom.Scale`, an inherited attached property for apps that scale their window content. Anchored popups
  (menus, selects, tooltips, hover cards, popovers) draw in the window's overlay layer, outside the scaled container, so
  they could not follow the zoom; they now lay out at the inherited scale. `ArcPopup.Surface` returns the surface that
  was assigned to `Child` (the popup now hosts it in a scaling element, so `Child` itself is that element).
- **ExpandableContent**: the ExpandableText clamp, toggle and motion around any content, such as selectable text, a
  read-only editor or rendered Markdown. The clamp ends on a line of the content's first visible text (`TextBlock`,
  `SelectableTextBlock`, or a control that implements the new `IExpandableLineSource`); other content clamps at
  `Lines × LineHeight`. `ExpandableText` and `ExpandableContent` share the new `ExpandableClamp` base class.
- **ExpandableText**, **ExpandableContent**: `IsFadeEnabled` turns off the fade over the last line.

### Changed

- AtomUI is pinned to 6.2.8; 0.2.0-preview.1 was built on 6.2.7.
- **ExpandableText**: while collapsed, the text is laid out only to the line after the clamp, and in full when it opens.
  A 600-line text measured about 1ms collapsed instead of about 135ms. Text with inline controls is still laid out in
  full, so a link below the clamp can take focus. The clamp now ends on the text's own line boundaries instead of
  `Lines × LineHeight`.
- **ExpandableText**: `Lines`, `IsExpanded`, `MoreLabel`, `LessLabel`, `LineHeight`, `IsOverflowing` and
  `ExpandedChanged` are now declared on the new `ExpandableClamp` base class. Code that uses them through
  `ExpandableText` is unchanged.

### Fixed

- **ExpandableText**: a change of `IsExpanded` that arrives with a new data context, for example in a recycled item
  template, no longer animates or scrolls the page.
- **Switch**, **Checkbox**: a value that arrives with a new data context, for example when a detail view is reused for
  the next item, shows at once; the thumb no longer slides and stretches, and the check mark no longer draws in, as if
  the user had clicked.
- **Progress**: a `Value` change that leaves the whole percentage unchanged no longer restarts the spring or updates the
  readout and the accessibility status. A download that reports hundreds of times a second now costs one update per
  percent.
- **SegmentedControl** and the other gliding-indicator toggles: a hidden control no longer places its indicator on
  every layout pass of the window; it is placed when the control is shown again.
- **GlidingIndicator**: the outline pen is kept between frames instead of being created on every render.
- TreeView and JsonViewer: a second change to the rows before the next layout pass, while rows from the first change were
  still closing, threw `IndexOutOfRangeException` in `RowTop`. The panel now refreshes its row geometry as soon as the
  rows change.

## 0.2.0-preview.1 (2026-10-21)

### Added

- **Targets**: the libraries also build for `net8.0`, next to `net10.0` and `net11.0`. The net8.0 build needs StreamMark
  `0.1.0-alpha.25`, which adds net8.0 to its packages.
- **Markdown** (`TKCode.Arc`): an outline and find. `GetOutline`, `ScrollToPoint`, `ScrollToAnchor`, `Find` and
  `GetSelectedText`, with the `IsDocumentReady`, `MatchCount`, `CurrentMatchIndex` and `HasSelection` properties and the
  `DocumentReady` and `SearchChanged` events. A host can build an outline sidebar, a find bar and a copy action around the
  document.
- **RichTextEditor** (`TKCode.Arc`): `TrySetMarkdown` imports Markdown and returns a `RichTextImportReport` that lists what
  the editor could not keep (`RichTextImportIssueKind`: heading depth, fence info strings, tilde fences, tables and so on).
- **DataGrid** (`TKCode.Arc.AdvancedCharts`, in the repository, not on NuGet yet): cell renderers (badge, progress, link,
  icon text, checkbox, actions), validation rules (required, range, max length, pattern), conditional format rules, a
  `CellInvoked` event, a context menu event, CSV export options, and filter and layout state.
- **Blocks** (`TKCode.Arc.Blocks`, in the repository, not on NuGet yet): the command palette dialog; search results with
  value and date filters, details and actions; sidebar collapse and resize modes; the reworked notification center.

### Changed

- AtomUI is pinned to 6.2.7; 0.1.1-preview.1 was built on 6.2.6. The AtomUI dependency moves with the Arc version.
- DataGrid CSV export neutralizes spreadsheet formulas by default (`NeutralizeCsvFormulas`), and a paste larger than
  `MaxPasteCells` (1,000,000 cells by default) is refused.
- On .NET 8 and 9 the DataGrid's natural text sort uses a built-in comparer; on .NET 10 and later it uses
  `CompareOptions.NumericOrdering`, as before.

### Fixed

- `LiveLoop.Start` returns early when it is already delivering frames, so a caller that starts the loop on every pointer
  move no longer re-reads the gates each time.

## 0.1.1-preview.1 (2026-10-21)

### Added

- **Components**: the Activity, Content, Editors, Illustrations, Media and SpecialInputs categories, plus ButtonGroup,
  SplitButton, Card, Reactions, SwipeActions and the Color, Date, DateRange and Time pickers.
- **Core**: the `MotionTimeline` and `LiveLoop` motion drivers, the `SharedElement` primitives, `ArcBlur` and
  `ArcViewport`.
- **Blocks** (`TKCode.Arc.Blocks`): the AppShell blocks (Sidebar, PageHeader, CommandPalette, SearchResults) and the
  Settings blocks (SettingsPage, SecuritySettings, BillingOverview, UsageBilling, CancelFlow, SettingsCommand).
- **AdvancedCharts** (`TKCode.Arc.AdvancedCharts`): a new project that starts with the DataGrid control.
- **Gallery**
  - A component's page is its docs page with live examples; each example is an `.axaml` file, and its Code tab shows
    that source.
  - A Docs section in the sidebar, and `Cmd+K` or `Ctrl+K` to focus the search from anywhere in the window.
  - API tables generated from the XML documentation, with zh-CN and zh-TW translations; shell text and docs pages in
    en-US, zh-CN and zh-TW.
  - Markdown rendering and code highlighting through StreamMark `0.1.0-alpha.23`, the first release that runs in the
    browser.
  - A WebAssembly host (`TKCode.Arc.Gallery.Browser`) and app icons for the desktop and browser hosts.
- **Distribution**
  - The gallery's WebAssembly image is built on GitHub and published as
    `ghcr.io/atomuicommunity/tkcode-arc-gallery`, served with brotli, pre-compressed files and browser caching.
  - Release builds of the gallery desktop app: Windows and Linux (unsigned), macOS (signed and notarized).
  - NuGet packing and publishing from CI with nuget.org Trusted Publishing.
- **Tools**: scripts for the API docs translation pipeline and the icon builder.
- A Simplified Chinese README and a Sponsors section.

### Changed

- The per-category shared helpers are replaced by the shared Core primitives.
- Gallery pages moved from per-category C# pages to Markdown docs pages and `.axaml` examples.
- XML `<summary>` text added to public properties and events on many controls; it feeds the gallery's API tables.

### Fixed

- Motion: a loop waiting out a pause could leave the renderer idle with its frame request pending; frames now keep
  coming.
- HoldToConfirm, ConfirmMorph (hold mode), the Tabs panel and the Carousel viewport changed a reused clip without
  invalidating the clipped visual, so the hold fill did not move and a growing panel or resized carousel kept a stale
  clip.
- Countdown: a wheel at rest is drawn as a plain glyph, which halves the frame time of a ticking face.
- Slider and ElasticSlider threw from `Math.Clamp` when XAML set `Minimum` above the current `Maximum` (for example
  `Minimum="360" Maximum="900"`).
- Tabs no longer scrolls the page around it when the selected tab changes; the tab list reveals the selected tab
  itself.
- Gallery browser host: native Skia is built for WebAssembly, and trimming is skipped on publish.

### Upgrading from 0.1.0-preview.1

- Update all TKCode.Arc packages together, to the same version.
- The new categories and controls need no setup beyond `UseArc()`. Blocks and the DataGrid need their own
  registration (`UseArcBlocks()`, `UseArcAdvancedCharts()`) and project references, as described in **Installation**.

## 0.1.0-preview.1 (2026-10-20)

First preview release.
