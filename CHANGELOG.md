# Changelog

All notable changes to TKCode.Arc are listed here. Versions follow [Semantic Versioning](https://semver.org/);
while the version carries a `-preview` suffix the API may still change between releases.

NuGet packages published with each release: `TKCode.Arc`, `TKCode.Arc.Core`, `TKCode.Arc.Fonts`,
`TKCode.Arc.Icons.Lucide`, `TKCode.Arc.Blocks`, `TKCode.Arc.AdvancedCharts` and `TKCode.Arc.Special`.

## Unreleased

### Fixed

- **SiteFooter**: a footer was never released once it had been shown. With the default content it listened to the
  static sample columns, so every footer stayed in memory with its whole page. A footer now listens to its columns and
  links only while it is in a visual tree, and catches up with what changed while it was away.
- **SemanticZoom**: a map kept listening to its `Organization` after it left the window, so an organization that
  outlives the map (a view model's, or shared sample data) kept every map that had shown it, with its caches (about
  30 MB per visit of the gallery page). The map and its panel now listen only while attached and rebuild on return if
  the organization changed meanwhile.
- **AgentStatus**: the pill kept listening to a host's `Simulation` and `Steps` after it left the window; it now
  listens only while attached, and clearing `Steps` releases the old steps too. The gallery demos dispose their
  simulations when the page goes.
- **Menus**: a sheet (`MenuSurfaceVariant.Sheet`, such as `UserMenu` in a narrow window) closed by its popup (a click
  outside, the window losing focus, the anchor leaving) left its scrim over the window, swallowing every click; the
  scrim now fades out. A closed sheet also stops listening to the window's size, which kept the menu and its anchor's
  page in memory.
- **ChatThread**: the reaction picker no longer writes debug lines (with a stack trace on every close) to the console.
- **Gallery**: a page whose last focused element had lost the focus stayed in memory after the visitor moved on; the
  shell now takes the focus before it replaces a page. Visiting every page three times now keeps no old page in memory
  (before, the managed heap grew by about 44 MB per round).
- **Controls kept alive by the data they show**: these controls listened, for their whole life, to collections, items,
  commands or helpers the app hands them, which usually outlive the view. A view model or a shared list therefore kept
  every control that had shown it, with its page, and the dead controls kept reacting to every change. They now listen
  only while they are in a visual tree and catch up with what changed while they were away:
  - `TKCode.Arc`: `TreeView`, `ChatThread` (messages and `TypingParticipantIds`), `Markdown` (`Stream`), `FilterToolbar`
    (`ActiveFilters`), the charts (`Series` values and `ItemsSource`), `Gauge` (`Thresholds`), `Slider` and
    `ElasticSlider` (`Marks`), `Reactions` (`Options`), `BottomSheet` (`Detents`), `TokenTextEditor` (a shared
    `TokenRenderer`), `ButtonGroup` and its menu button (`Menu`), and `SwipeActions` (a row that left kept handlers on
    the shared actions).
  - `TKCode.Arc.AdvancedCharts`: `DataGrid` (its rows and their items once it has left the window, and column renderers
    shared from resources; `Columns.Clear()` now lets the columns go).
  - `TKCode.Arc.Blocks`: `AiChat`, `AiAnswer`, the reply text and answer document sources, `ArenaPromptForm`,
    `PromptTestCases`, the AI panel tables, `KanbanBoard` (lane heads and the views of deleted cards were kept by their
    avatars), `ProjectBoard`, `AvailabilityPicker`, `ReleaseReadiness`, `WeekCalendar`, `LayoutMorph`, `RevenueGlobe`,
    `CustomerGalaxy`, `SecuritySettings`, `UsageBilling`, `SettingsCommand`, `SiteHeader`, `SiteFaceStack`,
    `CtaSetupCard` and `PageHeader` actions (`Command`).
  - `TKCode.Arc.Special`: the five explainers, `AgentPlan` (and a suspended `AgentPlanSimulation`) and `ContextWindow`.
    `AgentTimelineLiveDriver` keeps running until it is stopped, as before; its docs now say to stop or dispose it when
    the timeline goes away.
- **AiChat**: a reply asked for while the chat is out of a window (`Send`, `Retry` or an edit from code) no longer
  streams unseen; it waits, thinking, and starts when the chat is shown, as leaving the window already stopped replies.
- **SemanticZoom**: the baked avatar bitmaps are disposed when the cache is cleared (a theme change, an overflow) and
  when the map leaves, instead of waiting for the finalizer.
- **AI answers**: a code block taken out while it was highlighting now releases its highlighting session.
- **DataGrid**: the block requests of a `DataSource` dispose their cancellation sources.

## 0.3.0-preview.4 - 2026-10-24

### Added

- **Special components › AI** (`TKCode.Arc.Special`, original designs that take Arc's Pro behaviour and motion as a
  reference): `AgentStatus` (live status pill that opens into the recent steps, with a built-in `AgentRunSimulation`),
  `ToolCall` and `ToolCallGroup` (tool call rows with input and output, inline approval and retry, a transcript
  simulation), `AgentPlan` (editable, reorderable draft plan that becomes a live checklist), `AgentTimeline` (replay of a
  multi-agent run on one time axis, with handoffs, span cards, pinning, scrubbing and a live mode) and `ContextWindow`
  (200-cell token budget grid with a compaction threshold, legend and send / attach / compact actions). Docs and demos in
  English, 简体中文 and 繁體中文.
- **Blocks › Headers and footers** (ported from Arc's free blocks): `SiteHeader` (sticky header that turns solid on
  scroll, gliding current-link marker, mega menu panels whose height springs and whose content slides in from the side
  you moved toward, mobile menu sheet), `SiteFooter` (link columns with a validating newsletter, a minimal row, and a
  large fading arch mark), `CtaSection` (centered, split beside a setup card that completes itself, dismissible
  banner) and `NewsletterSignup` (email signup framed by a stack of past issues; subscribing drops the next issue onto
  the front).
- **Blocks**: the model arena, the eighth block of the AI group (original design): a blind test of two models. One prompt goes
  to two anonymous models, both answers stream in on one shared clock in whole words whatever the real speeds, and the
  vote reveals the models. Each answer shows time to first token, speed and cost measured on the raw stream, a failed
  answer keeps its text and offers a retry of that model alone, and the standings rank the models by win rate and replay the
  change the first time they are opened after a vote. `ModelArena` takes `Contestants`, `Prompts` and an `IArenaRunner`; in a
  narrow container the answers become tabs.
- **Blocks**: document extraction, the ninth and last block of the AI group (original design): turns a document into typed data
  and shows where each value came from. A quiet viewer draws the document on white paper beside an editable form; while a
  model reads, the fields fill in one by one, the text each value came from is tinted, and pointing at a field or its text lights
  both and draws a curved link. Values below a confidence threshold carry the model's reason and a "Looks right" button, a
  value that does not fit its type is null and blocks "Accept all", and the JSON and Schema tabs show the typed result and the
  schema sent to the model. `DocumentExtraction` takes `Samples` (document plus schema) and an `IDocumentExtractor`.
- **AnnouncementBar**: `DismissalDuration` lets a remembered dismissal expire (the gallery's sponsor banner comes back
  after a day); `null`, the default, keeps the bar hidden for good, as before.

### Changed

- **Gallery**: the language has its own menu in the top bar instead of a row in the Appearance popover. The first start
  picks English, 简体中文 or 繁體中文 from the system's language (the browser's preferred languages on the web), and a
  language chosen in the menu is remembered for the next start (local storage on the web, a small file in the local
  application data folder on the desktop).
- **Gallery**: a Friends section and page on the home page, a sponsor banner, a placeholder while a docs page loads, and a
  page that opens at the top and stays there until you scroll.

### Fixed

- **Combobox** (#9): a focused field drew a dark 3px ring around a dark border. Arc's focused combobox only turns its border
  to `BorderStrong`; its halo is transparent, so the ring is gone.
- **Gallery** (#8, #10): the examples stage no longer clips a view to its bounds, which cut off the left side of the inline edit
  frame and of the focus rings that sit a few pixels outside a control.
- **Gallery** (#11): a NativeAOT publish of the desktop gallery (`-p:PublishAot=true`) showed "This page could not be loaded"
  on every docs page, and no screenshots. The page text and the screenshots are resource-only assemblies that Avalonia
  finds by name, and NativeAOT keeps no metadata for an assembly whose types nothing uses; the desktop host now roots
  both (`TrimmerRootAssembly`).

## 0.3.0-preview.3 - 2026-10-23

### Fixed

- **Select**: the option highlight was square. Its corner radius was only copied to the glide indicator when the
  property changed, and the theme sets the default (14), which never raises a change; it is now applied when the list is
  created.
- **Select**: with the Full motion level the menu faded out after a pick, over the text below the field, although the
  documented behavior is to close at once. `ArcPopup` has a new `IsExitInstant` (Select sets it): at Full the popup
  closes without an exit motion; Reduced still fades out.
- **Combobox, MorphSelect**: clicking the input on macOS could end the app with an `NSInvalidArgumentException`.
  Opening the list wrote an empty text into the accessibility live region, and Avalonia.Native raises a notification with
  a nil value for it. The announcer now only receives messages.

## 0.3.0-preview.2 - 2026-10-23

### Added

- **Special components (Illustrations)**: five animated AI explainers in the new package `TKCode.Arc.Special`
  (`UseArcSpecial()`), each a reusable, data-driven card with three-language docs, examples and tests: `AgentLoop` (a
  head with a gradient trail circles gather context, take action and verify; context blocks stack up, tool calls drop
  out of the ring and resolve, checks pass or send the loop round again), `MixtureOfExperts` (a router scores every
  expert for each token, packets travel to the top-k experts within their capacity, a load balance bar and an active
  parameter readout; hover, focus or pin a token or an expert to inspect it), `SpeculativeDecoding` (a seeded draft
  model guesses ahead, the target checks the guesses in one pass, accepted tokens fly into the answer; a draft length
  slider and a race against the target alone), `PromptCache` (requests share a cached prefix: cache write, hits, a
  partial hit after an edit, a time to live that drains and expires, cost and first-token savings) and `ReasoningTree`
  (beam search over thoughts with seeded scores, pruning and the winning path, in a four-column tree or an outline on
  narrow cards). They share the public base `Explainer` (a story clock with play, pause, step, restart, speed,
  autoplay when seen, looping and reduced-motion behavior) and the data type `ExplainerModel`. Every number is computed
  from the data (costs, scores, acceptance, routing, capacity); sample data and model names are invented.
- **Blocks (Projects)**: five project blocks in `TKCode.Arc.Blocks`, each a reusable, data-driven control with
  three-language docs, examples and tests: `WeekCalendar` (week, work week, day, three-day and agenda views with a mini
  month, color-coded calendars and time zones; drag on the grid to create, move and resize events in 15-minute steps,
  edit them in popovers and undo the last change), `AvailabilityPicker` (a group meeting time picker: day cards with a
  gliding highlight, per-person free and busy marks, a booking step), `ProjectBoard` (a compact sprint board whose
  cards fly to the next stage, with an All work / My work filter and a sprint line), `ReleaseReadiness` (a checklist
  that unlocks an asynchronous release action, with progress, owners, success and failure states) and `KanbanBoard`
  (physical drag and drop with lift, tilt and a drop spring, keyboard and touch dragging, swimlanes by person, live
  work-in-progress limits, quick add and a details drawer). People are `ProjectPerson`s with generated avatars; shared
  parts (FLIP for custom panels, a thin progress line, a live-region announcer) are internal.
- **Blocks (Showpieces)**: six self-drawn showpiece blocks in `TKCode.Arc.Blocks`, each a reusable, data-driven control
  with three-language docs, examples and tests: `ActivityTerrain` (a week of hourly activity as an orbit-and-zoom 3D
  terrain that presses flat into a heat map), `RevenueGlobe` (a live payments map on a dotted globe, with a day timeline
  to replay or scrub; its land comes from a 0.5° Natural Earth mask), `CustomerGalaxy` (2,400 accounts as a turning galaxy that regroups into
  clusters, with totals beside it), `SemanticZoom` (an organization map you zoom from teams to projects to one task, with
  breadcrumbs, a minimap and a guided tour), `LayoutMorph` (one set of cards in six views, from a grid to a 3D ring and a helix,
  each card flying to its new place on its own spring) and `ExplodedView` (a screen that comes apart into labelled
  layers as you scroll). Shared parts are internal: projection and plane homographies, orbit and 2D cameras, inertia,
  a self-drawn stage, zoom controls, a hover card, rolling numbers and a text cache.
- **Core**: `RollingDigitsFalloff.None`, for rolling digits that are only clipped and never fade.
- **Core**: `ArcSurface.Elevation`, an inherited attached property (`Base` / `Raised`). Dialogs, drawers, bottom
  sheets, popovers and menus set it to `Raised`, so their content knows it sits on `SurfaceRaised`.
- **Button**: the `:raised` pseudo-class on a raised surface. A ghost button there fills with
  `GhostRaisedHoverBackground` on hover and press (the foreground mixed into `SurfaceRaised`, 7% dark and 5% light):
  `SurfaceMuted` is only a few steps off `SurfaceRaised` (`#242424` against `#1F1F1F` in dark), so the hover nearly
  vanished in dialogs.

- **Gallery**: the docs site serves files for AI assistants, generated from the docs pages when the image is built
  (`gallery/TKCode.Arc.Gallery.Llms`): `https://docs.tkcode.si/llms.txt`, an index of every guide, component and block;
  `llms-full.txt` with every page in one file; every page as Markdown at its address plus `.md` (such as
  `/components/button.md`), with the Simplified and Traditional Chinese pages under `/zh-CN/` and `/zh-TW/`; and an
  agent skill under `/skills/tkcode-arc/` (`SKILL.md`, a component catalog and a review checklist). The AI and MCP
  guide shows how to use them.

- **Core**: motion building blocks for the AI blocks: the springs `ArcSpring.Quick` (0.25) and `Settle` (0.33); the
  `ArcContentSwap` modes `Label`, `Crossfade` and `Lift` with `EnterDuration`, `ExitDuration`, `EnterDelay`,
  `EnterOffset` and `MeasuresExiting`; `GlidingIndicator.FadeDuration` and a tween `MoveTo`; `SpringSizeDecorator.IsHeld`
  and `CapsToAvailable`; and the new `ArcRing`, a circular progress ring that can trail its value. Text helpers:
  `WordDiff` (a word-level diff that keeps `{{ name }}` markers whole), `WordPacer` (streaming text released in whole
  words on a 34 ms clock) and `TextRangeDecoration` with `DecoratedTextBlock` (per-line rounded backgrounds, strikethrough
  and underline on character ranges, repainted without relayout). 23 more Lucide glyphs in `ArcIcons`.
- **Blocks**: the Model picker, the first block of the AI group (original design, not published to NuGet with the
  other Blocks yet): a pill that shows the chosen model and its effort, a menu with featured models, an effort setting
  and the other models by provider (flyouts, or pages in a narrow window), and a summary with a confirmation after
  each change. `ModelPicker` is built from `ModelPill`, `ModelMenu` and `ModelSummary`; models are `AiModel` and
  `AiModelProvider` objects.
- **Blocks**: Agent memory, the second block of the AI group (original design): a card that shows what an assistant
  remembers, with a capacity meter, two switches, filters by group, a search that highlights hits, in-place editing,
  forgetting with a few seconds to undo, a pill that files a just-noted memory, and a field that adds one by hand and
  sorts it into a group. Memories are `MemoryItem` objects in `AgentMemory.Memories` (or a list you give as
  `MemoriesSource`); `CommitAsync` saves a change first and rolls it back in place when it fails, `ClassifyAsync` sorts
  new memories, and every word comes from `AgentMemoryLabels`.
- **Blocks**: the AI composer, the third block of the AI group (original design): the prompt box of an AI app with a text
  area that grows from one to eight lines, "/" commands and "@" mentions that become tokens, attachments picked from the
  plus menu, pasted or dropped that upload in place (long pastes become attachments), tool toggles, a model pill with an
  effort row, a character counter with a limit, and a send button that turns into a stop button with a progress ring.
  `AiComposer` reports a send as an `AiComposerMessage` (text, tokens, ready attachments, model, effort, tools); the host
  starts the reply and sets `IsGenerating`. `ModelPill` gains a toolbar appearance and `ModelMenu` an inline effort row.
- **Special inputs**: `TokenTextEditor`, a multi-line editor whose tokens (commands, mentions, variables) act as single
  units, with configurable triggers that report a query for your own list. `MentionInput` is now built on it with no
  change in behavior.
- **Blocks**: the AI chat, the fourth block of the AI group (original design): a chat with an assistant that fills its
  container, with a conversation menu, a model button and a new chat button on top, a thread of turns and a single-row
  message box. Replies stream in as Markdown in whole words, with collapsible reasoning, tool chips that open to their
  searches or queries, numbered citations that preview their source, and actions to copy, retry, rate and switch between
  versions; editing a message or retrying a reply makes a new version of the turn. `AiChat` asks an `IAiChatResponder`
  (a stream of events) or waits for your code to fill the `AiChatReply`; the thread follows new text at the bottom with a
  jump button when scrolled away. `AiComposer` gains an inline layout (`Layout = Inline`), `ModelPill` a header
  appearance and `ModelMenu` a simple layout.
- **Overlays and Content**: `HoverCard` opens on keyboard focus after `OpenDelay` with `OpensOnFocus`, closes on Escape
  without moving focus, has `CardPadding` and is exposed as a tooltip; `Markdown` gets `RevealFadeDuration`,
  `RevealFadeInReducedMotion` and `IsPaced` for hosts that pace the streamed text themselves.
- **Blocks**: the AI answer, the fifth block of the AI group (original design): a page that answers one question like an
  article, with a reasoning row, source cards, an answer streamed in whole words with headings, lists, a table, code and
  numbered citations, and three-way lighting between a citation, the sentence it supports and its source card. Under the
  answer are actions (copy with sources, answer again with a model submenu, thumbs with a reasons list, share link, a
  version pager), the model line, related questions and a follow-up box. `AiAnswer` asks an `IAiAnswerResponder` or shows a
  finished answer; asking again replaces the answer in place and keeps the versions of the question.
- **Blocks**: the AI side panel and the prompt studio, the sixth and seventh blocks of the AI group (original design).
  `AiSidePanel` is an assistant beside a page the person reads: it reads the whole page, answers about it or about a
  selection, and proposes changes as a word diff (`AiDiffText`, `AiDiffTable`) that the person applies, discards or undoes
  from an Undo pill; it becomes a bottom sheet on narrow widths. `PromptStudio` edits a prompt template with coloured
  variable chips, runs it on saved test cases through a `PromptRunHandler`, and compares, promotes and undoes versions,
  with version and model menus, variable fields, a Run button and an output panel. `TokenTextEditor` gains `TokenParser`
  and `TokenRenderer` for custom inline tokens.

### Changed

- **Gallery**: the Illustrations group (Data flow, Payment flow, Multi-region failover, Vector search and the five new
  explainers) is listed in the sidebar's Special components section after Blocks, as on uiarc.dev; its pages keep their
  `/components/<slug>` addresses (`GalleryCatalog.Special`).
- **Sidebar**: `ResizeMode` now defaults to `Overlay`: the layout width changes at once (the page is laid out once) and
  only the pane springs. Bind the page's `RenderTransform` to `PageShift` to let it glide along, or set
  `ResizeMode="Push"` for the previous behavior (the width springs and the page is laid out on every frame).
- **Performance**: the motion scheduler advances each driver once per frame and raises `Changed` once; retargeting a
  `MotionValue` to the same target with the same spec no longer restarts it; a `LiveLoop` listens for layout and viewport
  changes only while it is wanted and its owner is visible (a hidden spinner in every Button no longer costs anything on
  each layout pass); the modal backdrop blur, `ArcContentSwap` and other animated blurs use cached `ArcBlur` effects;
  `SpringSizeDecorator` lays out only when the size changes by a device pixel; the DataGrid search box waits for a pause
  in typing, searches cell by cell without building a text per row, and rows appended to the source are added without
  reloading the grid.
- **Performance, ActivityTerrain**: the bars are drawn as anti-aliased quads (each face a rectangle through a projective
  matrix, pushed out 0.3px so neighboring faces still meet without a seam) instead of a new path and a 0.6px seam stroke per
  face, and faces too thin to see (sides in the heat map, bars of zero height) are skipped. A frame while the terrain turns,
  switches metric or changes view costs about a fifth of the GPU time it did, and the UI thread allocates no geometry per
  face (410 KB a frame to 34 KB while orbiting).
- **Breaking, repository move**: the repository now lives at `AIDotNet/TKCode.Arc` (it was `AtomUICommunity/TKCode.Arc`),
  and the XAML namespace follows it. Change `xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"` to
  `xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"`, and the Lucide icons namespace from
  `https://github.com/AtomUICommunity/TKCode.Arc/lucide` to `https://github.com/AIDotNet/TKCode.Arc/lucide`. The old
  addresses no longer resolve to any type.
- **Gallery**: the WebAssembly site downloads about 1.5 MB less before its first frame (12.0 MB to 10.5 MB, brotli). The
  docs pages and guides in all three languages were most of the gallery assembly; they now live in the resource-only
  `TKCode.Arc.Gallery.Content`, which the site leaves out and serves as files, fetching a page when it is shown. The
  desktop gallery reads them as resources and calls `WithGalleryContent()`.
- **Performance (Showpieces, Core)**: self-drawn labels, captions and rolling numbers no longer lay their text out again
  on every frame (Avalonia's `FormattedText` re-shapes its text each time it is drawn). `ActivityTerrain`, `LayoutMorph`,
  `SemanticZoom` and the Showpieces' panel numbers draw cached text layouts in the caller's color, one layout for every
  color and fade, and Core's `RollingDigits` keeps its glyphs as layouts too (a new `Foreground` no longer lays them out
  again). Same pixels; less garbage per frame (a LayoutMorph view change: 86 KB to 17 KB on the UI thread).

### Fixed

- **Tabs, PageHeader, Breadcrumb**: a data template given to these controls (`ContentTemplate`, `ItemTemplate`) is no longer built
  with `null`. The panel and body hosts attached the template before the content and kept it while clearing the content, and
  a breadcrumb item's width reserve did the same, so a template that read its data (`item.Title`) threw a
  `NullReferenceException` the first time a tab, a body or a breadcrumb was shown.
- **Core**: `ArcContentSwap` no longer builds its `ContentTemplate` for null content. Setting `Content` to `null` (or
  setting the template while there was no content) built a user's template with `null`, so a template that reads its
  data threw; the template is now only attached while there is content.
- **DataGrid**: a column's `EditTemplate` is no longer built with a `null` context when the edit ends. Clicking away from
  a custom editor (the Health slider in the custom-cells example) cleared the editor's content while the template was
  still attached, and the template threw a `NullReferenceException` that closed the app.
- **Core**: an `ArcContentSwap` that left the window in the middle of a change came back half transparent. A stopped
  `MotionValue` stayed in the scheduler and was snapped back to the value it had stopped at once its owner was detached;
  it now only snaps while it is still animating.
- **Core**: anchored popups (menus, selects, tooltips, hover cards, popovers) drew their floating shadow with square
  corners again since 0.3.0-preview.1, so a faint square corner showed past the surface's rounded ones. The shadow
  follows the surface's corner radius again, scaled with `ArcZoom.Scale`.
- **Sidebar**: after the sidebar was collapsed to the rail and opened again, its header, filter box and rows kept the
  width of the first frame of the opening spring: content was pushed past the pane's right edge (by most of the rail's
  width with `ResizeMode="Overlay"`, by a pixel with `Push`).
- **Motion**: `ArcBrushTransition` mixes solid colors with premultiplied alpha. It was Avalonia's `BrushTransition`, a
  plain ARGB mix, and `Transparent` is transparent white, so fading a fill in from or out to `Transparent` (ghost
  buttons, menu rows) passed through a translucent light gray that flashed on dark surfaces. Other brushes still switch
  halfway. The class now derives from `InterpolatingTransitionBase<IBrush?>` instead of `BrushTransition`.
- **TagInput, Textarea, TimePicker**: a focused field drew a heavy dark 3px ring around its border. Arc's halo is
  `--focus-ring: transparent` (light and dark), so only the border turns `Foreground`. `FieldFrame` no longer draws the
  halo, and `HasFocusRing` and the `FieldFrameToken` `FocusRing*` values are removed.
- **LayoutMorph**: pictures are drawn from copies made at the size they are shown on screen (rounded up in steps of 1.25,
  rebuilt only when that step changes, never larger than the picture; the open detail draws the picture itself). Fifteen
  640 × 800 pictures took 30.7 MB of GPU textures, more than Avalonia's 28 MB budget, so they were uploaded again on every
  frame: a resting grid now repaints in about a third of the time at 2x, and pictures shown small no longer alias. On a GPU
  compositor the carousel's cards are drawn from bitmaps while the ring turns or is dragged (about half the frame); they
  are vectors again half a second after it rests. A bitmap picture drawn into again keeps its old copy until `Image` changes.

## 0.3.0-preview.1 - 2026-10-22

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

## 0.2.0-preview.1 - 2026-10-21

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

[0.3.0-preview.4]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.0-preview.3...v0.3.0-preview.4
[0.3.0-preview.3]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.0-preview.2...v0.3.0-preview.3
[0.3.0-preview.2]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.0-preview.1...v0.3.0-preview.2
[0.3.0-preview.1]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.2.0-preview.1...v0.3.0-preview.1
[0.2.0-preview.1]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.1.1-preview.1...v0.2.0-preview.1
[0.1.1-preview.1]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.1.0-preview.1...v0.1.1-preview.1
[0.1.0-preview.1]: https://github.com/AIDotNet/TKCode.Arc/releases/tag/v0.1.0-preview.1
