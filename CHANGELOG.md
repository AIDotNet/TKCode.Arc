# Changelog

All notable changes to TKCode.Arc are listed here. Versions follow [Semantic Versioning](https://semver.org/);
from 1.0.0 on, breaking API changes come only with a new major version. Earlier `-preview` versions could change the API between releases.

NuGet packages published with each release: `TKCode.Arc`, `TKCode.Arc.Core`, `TKCode.Arc.Foundation` (from 0.3.2),
`TKCode.Arc.Fonts`, `TKCode.Arc.Icons.Lucide`, `TKCode.Arc.Blocks`, `TKCode.Arc.AdvancedCharts`, `TKCode.Arc.Special`,
`TKCode.Arc.Workflow`, `TKCode.Arc.Dock` (from 0.4.0-preview.2), `TKCode.Arc.Themes.Pixel`, `TKCode.Arc.Themes.Corners`, `TKCode.Arc.Mobile`, `TKCode.Arc.Terminal`, `TKCode.Arc.Audio`,
`TKCode.Arc.Audio.Native`, `TKCode.Arc.Audio.Browser`, `TKCode.Arc.Templates` (from 0.4.0-preview.6), `TKCode.Arc.Pdf`, `TKCode.Arc.Pdf.Native` and `TKCode.Arc.Pdf.Browser` (from 1.0.0), `TKCode.Arc.Video`, `TKCode.Arc.Video.Native` and `TKCode.Arc.Video.Browser` (from 1.1.0-preview.1).

## Unreleased

### Fixed

- **`ArcWindow` top edge on Windows** ([#102](https://github.com/AIDotNet/TKCode.Arc/issues/102)): DWM draws no
  frame line on the top edge of a window whose client area extends into the title bar, so the window now draws it in the
  `Border` color while it is in its normal state on Windows (`:top-edge` pseudo-class). All four edges match; a style on
  `ArcWindow:top-edge /template/ Border#PART_Root` can remove it.
- **Table after a source change without layout** ([#103](https://github.com/AIDotNet/TKCode.Arc/issues/103)): a
  `Table` whose `ItemsSource` changed while it was hidden or off the visual tree no longer throws
  `ArgumentOutOfRangeException` when the theme changes (for example when the page is shown again). Rows wait for the
  next layout before they read the new source.
- **`ListGroup` with a `ListItem` template** ([#104](https://github.com/AIDotNet/TKCode.Arc/issues/104),
  `TKCode.Arc.Mobile`): when the `ItemTemplate`'s root is an `arcm:ListItem`, that row is the container instead of
  being wrapped in a second row, so `Title`, `Subtitle`, `Command` and the other row properties can be bound in the
  template.
- **`ToggleGroup` with a custom `SelectedValueBinding`** ([#105](https://github.com/AIDotNet/TKCode.Arc/issues/105)):
  `SelectedValues`, `SelectedValue` and the `ValueChanged` event report the value the binding selects, not the item,
  and a two-way bound `SelectedValue` is no longer overwritten when the group is shown.
- **`SettingsGroup` rows from `ItemsSource`** ([#106](https://github.com/AIDotNet/TKCode.Arc/issues/106),
  `TKCode.Arc.Blocks`): the first generated row no longer draws a hairline above it, and a `ColumnSpan` set on a
  templated row applies on the first layout.
- **Modal content in the logical tree** ([#108](https://github.com/AIDotNet/TKCode.Arc/issues/108)): the content of a
  `Dialog`, `Drawer` or `BottomSheet` appears once in `GetLogicalDescendants()`, under the overlay, instead of twice.

## 1.1.0-preview.3 - 2026-11-10

A performance release: apps can cap how often continuous loops repaint. There are no breaking changes from
1.1.0-preview.2. Install it with `--version 1.1.0-preview.3` (or `--prerelease`).

### Added

- **`ArcMotion.MaxFrameRate`** (`TKCode.Arc.Core`): an inherited attached property that caps how many times a second
  every `LiveLoop` under an element steps (mascots, skeleton pulses, shimmers, live dots). Each step gets the time since
  the last one, so the motion keeps its speed, and skipped frames draw nothing. The default, 0, steps on every frame, as
  before. Set it on a small always-on window, e.g. `arc:ArcMotion.MaxFrameRate="24"`, to stop slow decorative motion
  from repainting the window at the display's full rate.

## 1.1.0-preview.2 - 2026-11-10

A fix for NativeAOT: apps that use `Sidebar` publish without trim warnings. There are no breaking changes from
1.1.0-preview.1. Install it with `--version 1.1.0-preview.2` (or `--prerelease`).

### Fixed

- **Sidebar under NativeAOT** ([#100](https://github.com/AIDotNet/TKCode.Arc/issues/100)): `TKCode.Arc.Blocks` no
  longer produces trim analysis warnings (IL2026 / IL3053) when an app uses `Sidebar`. New `LabelMemberBinding`,
  `KeyMemberBinding`, `IconMemberBinding`, `BadgeMemberBinding`, `IsEnabledMemberBinding` and `ChildrenMemberBinding`
  map `ItemsSource` data with compiled bindings, e.g.
  `LabelMemberBinding="{Binding Title, DataType={x:Type local:MenuNode}}"`.
  The string `*Member` paths keep working but resolve with reflection; trimmed and NativeAOT apps should move to the
  `*MemberBinding` properties.

## 1.1.0-preview.1 - 2026-11-10

The first preview of 1.1.0: TreeSelect, Sticky, Rating, BackToTop and new sliders in `TKCode.Arc`, glass, navigation, photo and data components in `TKCode.Arc.Special`, 58 new blocks, the video packages on NuGet, and Avalonia updates within version 12 without a new Arc release. There are no breaking changes from 1.0.0. Install it with `--version 1.1.0-preview.1` (or `--prerelease`); the API added in this version may still change before 1.1.0.

### Added

- `TKCode.Arc.Video`, `TKCode.Arc.Video.Native` and `TKCode.Arc.Video.Browser` are published to NuGet: `VideoPlayer` and `VideoSurface` over Media Foundation, AVFoundation or GStreamer on desktop and the browser's video element in WebAssembly (`UseArcVideo()`, `ArcVideoNative.Use()`, `ArcVideoBrowser.UseAsync()`).
- `TreeSelect`: a select field over a tree that picks one node or checks several, with three-state checkboxes that check whole branches, a `CheckStrategy` for what is reported (leaves, parents or all), a search typed in the field that keeps the path to every match, chips, and virtualized rows for thousands of nodes (#80).
- `Sticky`: attached properties that keep any element of a scrolling page at the top once it scrolls there, like CSS `position: sticky`, with an offset, an `IsStuck` state to style and a `StuckChanged` event; it never leaves its parent, so group headers push each other away (#84).
- `Rating`: a row of stars for a score, with half stars, clearing, a read-only display, a rolling word for the value and custom shapes (`Icon`, `EmptyIcon`).
- `ToggleGroup` and `SlotText`: a flat row of toggle buttons, with one gliding fill in single mode and per-option fills that join between pressed neighbours in multiple mode; and text and numbers that spin into their new value on staggered reels.
- `RangeHistogramSlider` and `RulerSlider`: a two-thumb range over a histogram of the data, with lit bars cut at each thumb and release snapping to bar edges; and a tape of ticks under a fixed needle, with flick momentum, tick snapping, rubber-banded ends and a rolling readout.
- `OnboardingChecklist`: a getting started checklist with a progress ring and rolling count, steps that run an async action or are checked by hand, inline errors with retry, a completed group that reopens steps and a hideable body.
- Blocks `HeroSection`, `FaqSection`, `ContactSection` and `StatsBand`: three SaaS heroes over a mesh plus a layout for your own content; FAQs as an accordion, a topic rail or a search that highlights matches; a validated contact form with channels and offices; and headline numbers that count up with a small proving picture.
- Blocks `BlogGrid`, `ComparisonTable`, `ChangelogFeed` and `LogoMarquee`: a blog index with a filter, pagination and an in-place reader; an us versus them table with a pinned header and a stacked phone view; release notes to filter and open in place; and a continuously moving row of marks with a pause control.
- `TKCode.Arc.Special` gains components designed independently from their names alone (no Arc Pro pages, docs, source or assets were used), in new groups:
  - Glass: `GlassCard`, `GlassMenu`, `GlassSegmented` and `GlassTabBar`, frosted surfaces that blur a chosen backdrop (`arc:GlassBackdrop.Source`), with opaque fallbacks under reduced transparency.
  - Navigation: `MorphNav`, `LiquidTabBar`, `OrbitMenu`, `ActionMorph` and `ShareSheet`.
  - Photos: `ProductGallery`, `LightboxGallery`, `PhotoGrid` and `CoverFlow`.
  - Data: `BookingPill`, `ActivityRings`, `TimeDial` and `DateReel`; Widgets: `KeycapKeyboard`.
  - Inputs: `EmojiPicker` (Unicode 16.0 emoji with CLDR names), `AvatarUpload`, `ConfirmPopover` and `UndoToast`.
  - Publishing: `MegaMenuMorph`, `TocProgress` and `ShareAccess`.
  - The explainers `DnsResolution`, `ModelTraining` and `BranchGraph`.
  - Playful and Cursor: `SandText`, `TypewriterTerminal`, `PrizeWheel`, `MagnifyDock` and `ImageTrail`.
- `SplitBar` (Charts), `MetricTicker` (Cards) and `PresenceStack` (Avatars) in `TKCode.Arc`, designed the same independent way, in the groups where Arc lists them.
- `TKCode.Arc.Blocks` gains blocks designed the same independent way:
  - AI: `AgentRun`, `ResearchRun`, `AiInlineEdit`, `AgentTasks`, `AgentApprovals`, `AgentRoutines`, `AgentOrchestrator`, `AgentBuilder`, `AgentGallery`, `McpServers`, `KnowledgeBase`, `EvalSuite`, `AiInsightCard`, `AiFeatureLaunch`, `AiSearchBar`, `AiSetupWizard`, `CatchUp` and `SmartScheduler`.
  - Support and content: `SupportConversation`, `SupportWidget`, `InboxTriage`, `FeedbackBoard`, `HelpCenter`, `FeatureVoting` and `ActivityFeed`.
  - Commerce and finance: `WalletCard`, `SpendAlert`, `InvoiceStudio`, `DeliveryTracker`, `ProductListing`, `ProductDetail` and `SeatMap`.
  - Analytics, people and projects: `CallInsights`, `LeadScoring`, `AccountHealth`, `GoalTracker`, `PivotBuilder` and `TaskBreakdown`.
  - Marketing: `HeroSignup`, `TeamShowcase`, `StudioPerspectives`, `ScrollStory`, `SpotlightGrid` and `FeatureBento`.
  - App shell, auth and settings: `DocsSidebar`, `InboxSidebar`, `SelectionToolbar`, `TwoFactorSetup` (with a QR encoder and TOTP check of its own), `OnboardingSurvey` and `MagicLinkSignIn`.
- `BackToTop` (issue #67): a round button that appears once a list, log or feed has scrolled past a threshold and smoothly scrolls it back to the top, placed over the container yourself or added to a corner of its viewport with `arc:BackToTop.IsAttached`.
- `Sidebar.ExpandedWidth` (Blocks): the width of the open sidebar (default 240), which the collapse springs from and back to ([#92](https://github.com/AIDotNet/TKCode.Arc/issues/92)).

### Changed

- `GhostMascot`, `CubeMascot`, `AiMascot`, `LiquidTabBar`, `AiSpark` and `RadarChart` allocate far less per frame and look the same: moving paths are rebuilt once per drawn frame instead of being edited point by point (a ghost's frame step went from about 6 KB to none, a cube's from about 10 KB to none), their brushes and pens are immutable, and an `AiMascot` resting in the error state redraws only when it blinks.

### Fixed

- The packages no longer pin Avalonia to one exact version ([#97](https://github.com/AIDotNet/TKCode.Arc/issues/97)): they depend on Avalonia 12.1.5 or any later 12.x release (`[12.1.5, 13.0.0)`), so an app can update Avalonia within version 12 without waiting for an Arc release.
- `MegaMenuMorph` no longer opens and closes over and over while the pointer rests on a trigger: its panel's popup no longer turns on light dismiss, whose transparent layer covered the bar and took the pointer from the trigger. A press outside the bar and the panel still closes it.
- The Windows gallery app starts again on Windows 10 builds before 21H2 or without recent updates: it no longer opts in to CET shadow stacks (`CETCompat` false; #76).
- `Dialog`, `Drawer` and `BottomSheet` panels and `Tooltip` bubbles (the `ArcWindow` title bar buttons' included) no longer show dark square corners below their rounded ones ([#75](https://github.com/AIDotNet/TKCode.Arc/issues/75)): the panels clipped their own floating shadow to a rectangle, and popups in the overlay layer had it clipped by the FluentTheme or SimpleTheme popup host.
- `Table` no longer throws "Invalid Arrange rectangle" when its compact phone layout is narrower than the row's insets: hosted cells get zero width instead of a negative one.
- `ChatThread` message bubbles now follow the theme's corner style (square, scaled) instead of keeping fixed 18 and 6 px corners.
- `Sidebar` (Blocks) collapses again when it is made narrower or wider, and its header and rows lay out at its actual width, so centred header content stays centred ([#92](https://github.com/AIDotNet/TKCode.Arc/issues/92)). Set the new `ExpandedWidth` instead of `Width`: a `Width` set on the sidebar fixes it at that width.
- `Sidebar` (Blocks) built-in text follows the Arc language ([#92](https://github.com/AIDotNet/TKCode.Arc/issues/92)): the collapse button's tooltip and name, the filter placeholder, the no-matches text, the landmark name and the "unread" note now come in English, Simplified Chinese and Traditional Chinese and change with `ArcLanguage.Set`; a value set on `CollapseButtonText`, `ExpandButtonText`, `FilterPlaceholder`, `NoMatchesText`, `Label` or `UnreadText` still wins.
- `TextFilterBox` and `NumberFilterBox` no longer overlap or cut off their fields: a long label or description wraps, and in a narrow space the label moves above the fields. The operator menu names each operator next to its symbol.
- Loading indicators no longer freeze under reduced motion ([#93](https://github.com/AIDotNet/TKCode.Arc/issues/93)): `MorphLoader`, the loading spinner of `Button` and every other control that uses the Core `ArcSpinner`, the `StretchRefresh` running line, the `ChargeSession` packets and the tool, plan and generation spinners hold still and breathe in opacity (1 → 0.45 → 1 every 1.8 s, the new Core `ArcBusyPulse`), and `Skeleton` and `SkeletonMorph` keep their opacity pulse. Nothing travels, turns, scales or blurs; full motion is unchanged, and with motion off they stay still.

## 1.0.0 - 2026-11-08

The first stable release of TKCode.Arc: Arc's visual language and spring motion as native Avalonia 12 controls, with page-level blocks, a data grid, a workflow canvas, docking, a terminal, a PDF viewer and audio playback around them. It targets `net8.0`, `net10.0` and `net11.0` on Avalonia 12.1.4 (`TKCode.Arc.Terminal`: `net10.0` and `net11.0`).

### Stability

From this version on, Arc follows Semantic Versioning without a `-preview` suffix: breaking API changes come only with a new major version, and the changelog lists them. All packages are released together on one version, so keep them on the same one. Installing no longer needs `--prerelease`.

### Packages

These 21 packages are published to NuGet: `TKCode.Arc`, `TKCode.Arc.Core`, `TKCode.Arc.Foundation`, `TKCode.Arc.Fonts`, `TKCode.Arc.Icons.Lucide`, `TKCode.Arc.Blocks`, `TKCode.Arc.AdvancedCharts`, `TKCode.Arc.Special`, `TKCode.Arc.Workflow`, `TKCode.Arc.Dock`, `TKCode.Arc.Terminal`, `TKCode.Arc.Audio`, `TKCode.Arc.Audio.Native`, `TKCode.Arc.Audio.Browser`, `TKCode.Arc.Pdf`, `TKCode.Arc.Pdf.Native`, `TKCode.Arc.Pdf.Browser`, `TKCode.Arc.Themes.Pixel`, `TKCode.Arc.Themes.Corners`, `TKCode.Arc.Mobile`, `TKCode.Arc.Templates`.

### Upgrading from 0.4.0-preview.6

An API comparison of the 11 packages that also shipped in 0.4.0-preview.6 and have a `net10.0` build (`TKCode.Arc`, `.Core`, `.Foundation`, `.Blocks`, `.AdvancedCharts`, `.Special`, `.Workflow`, `.Dock`, `.Mobile`, `.Terminal`, `.Audio`) found no breaking changes. One thing can still affect you: many parts that are not always shown are no longer in the control templates and are created in code on first use. A custom `ControlTheme` or code that looks up one of those `PART_` names should handle it being absent. Remove `--prerelease` from your install commands.

### Added

- **Gallery as a product site**: the home page now introduces TKCode without showing components or projects: what it does, its size in numbers, what you can build, how it works, three commands to start, where it runs and its AI support, over a live dot field. Two new pages hold the rest: `/components` (components, blocks, effects and mobile, by tab, with a filter) and `/projects` (StreamMark, Jaunt and Spyglass); StreamMark also has `/streammark`. The top bar is now Components, Open source and Docs, with icon buttons. The sponsor banner is unchanged.
- **Gallery on the web and on the desktop**: the site says it runs in WebAssembly and that its performance does not represent the desktop app, and offers the newest release's files with the visitor's system first. The desktop app has a settings menu with Appearance, Language and About (version, update check, license, source and third-party notices), and its home page shows the update state instead of the recommendation.
- **Gallery on phones**: below 760 px the sidebar opens as a drawer, the docs pages and the home page use a phone's margins, and sections stack.
- **Special components › Playful** (`TKCode.Arc.Special`, an original design that takes Arc's Pro behaviour and motion as reference): `TicketTear`, an event ticket whose stub tears along the perforation hole by hole, following the hand, then falls away. Enter, Space or `IsTorn` finishes the tear on its own.
- **PDF** (new packages `TKCode.Arc.Pdf`, `TKCode.Arc.Pdf.Native` and `TKCode.Arc.Pdf.Browser`; issue #51): `PdfViewer` opens a PDF from a file, bytes, a stream or an address, with thumbnails and a table of contents in a sidebar, search with counts and highlights, text selection and copy, links (external ones only for `http`, `https` and `mailto`), a password form or a `PasswordRequested` event, and loading and failure states. `arc:Pdf.Source` shows only the pages in a plain `PageViewer`. PDFium renders on desktop (`ArcPdfNative.Use()`, in-process, x64 and arm64 for Windows, Linux and macOS) and pdf.js in a browser (`await ArcPdfBrowser.UseAsync()`). Without a backend a document fails with a hint; pages are never faked. Know the limits: documents without embedded Chinese, Japanese or Korean fonts depend on the machine's fonts, there is no touch drag selection, plain-text web addresses are links only with PDFium, and the Liberation fonts are not in the browser package. Docs and demos in English, 简体中文 and 繁體中文.
- **`PageViewer`** (`TKCode.Arc`, Media; issue #51): zoom, pan and page through pictures (`ItemsSource`) or pages rendered on demand (`IPageSource`), continuous or one page at a time, with fit modes, a toolbar and `PageLayer` for your own drawing and pointer input on the pages. One self-drawn surface draws only the visible pages. Docs and demos in English, 简体中文 and 繁體中文.
- **Templates** (new package `TKCode.Arc.Templates`): `dotnet new install TKCode.Arc.Templates`, then `dotnet new tkcode-arc` for a desktop app or `dotnet new tkcode-arc-xplat` for the desktop and the browser (WebAssembly). Four layouts (`--layout blank|sidebar|header|dock`) and options for MVVM with CommunityToolkit.Mvvm (`--mvvm toolkit`), dependency injection (`--di`), saved preferences (`--settings`, on by default: the appearance, the accent, the window placement and the layout's state come back on the next start), localization (`--i18n`: English, 简体中文 and 繁體中文 in `.xlf` files with a language switch; `--ui-language` picks the text's language), a headless test project (`--tests`), Arc's title bar (`--custom-titlebar`), `--framework` and `--accent`. Item templates add a page (`tkcode-arc-page`), a window (`tkcode-arc-window`), a dialog with an awaitable result (`tkcode-arc-dialog`) and an Arc-style control with its token class and theme file (`tkcode-arc-control`). Three more templates start from a finished app built from Arc's blocks, with sample data behind service interfaces, MVVM and dependency injection: `tkcode-arc-admin` (sign-in, a dashboard, a customer table with search, filters, paging and a detail drawer, team and roles, billing, settings, a command palette), `tkcode-arc-ai-chat` (a conversation list beside `AiChat` with streamed replies from a sample responder you replace with your model client, conversations kept between runs) and `tkcode-arc-devtool` (a dock layout with a file explorer, editor tabs, a property grid, build output and problems, the open files restored with the layout). Also `tkcode-arc-workflow` (a step palette, an editable workflow canvas with undo, a minimap and an inspector, a simulated run, JSON import and export), `tkcode-arc-projects` (Kanban board, Gantt timeline, week calendar, release checklist) and `tkcode-arc-analytics` (KPI drill-down, revenue, cohort retention, usage forecast, service status). `tkcode-arc-lib` starts a control library of your own: an example control with its design tokens and theme, a `Use<Name>()` registration, headless tests and a sample app, ready for `dotnet pack`. The new **Templates** guide lists them all; the Installation guide gains a “Start from a template” section.
- **`EnumSelector`** (`TKCode.Arc`, Extras; issue #56): lists the members of an enum as choices, a `RadioGroup` for a
  plain enum and `Checkbox`es for a `[Flags]` enum whose checked members combine bit by bit (composite members such as
  `ReadWrite` check their parts and show a dash when partly set, a zero member such as `None` clears the rest, and every
  integer underlying type is handled). `SelectedEnum` binds two-way; `DisplayMode` (`Auto`, `Radio`, `Check`),
  `UseDisplayNames`, `Rows` / `Columns` / spacing or a custom `ItemsPanel`, and `ItemTemplate`. The members, their
  `[Description]` / `[Display(Name)]` texts and the `[Flags]` marker come from a new Foundation generator for enums marked
  `[EnumMetadata]` (or `[assembly: EnumMetadata(typeof(...))]`), registered with `EnumTypes`, so nothing is reflected
  (AOT and trimming safe); `EnumMetadata(LanguageCatalog = ...)` takes the texts from a language catalog and follows the
  application language. New diagnostics ARCF033–ARCF037. Docs and demos in English, 简体中文 and 繁體中文.
- `FluidTabControl` and `FluidTabItem` (`TKCode.Arc`, issue #60): scroll-linked tabs. A tab list on the left or right
  beside one scroll area that stacks the content of every tab; picking a tab scrolls smoothly to its section
  (`AnimationDuration`, `EasingFunction`, `IsAnimationEnabled`), scrolling selects the tab of the section on the anchor
  line (`SnapAlignment` Top or Center), with two-way `SelectedItem` / `SelectedIndex`, header, icon and content
  templates, keyboard navigation and live add, remove and move. Docs and demos in English, 简体中文 and 繁體中文.
- **Advanced charts** (`TKCode.Arc.AdvancedCharts`): `Sunburst`, `SankeyFlow`, `FunnelChart`, `RadarChart` (issue #66: polygon grid, adjustable fill opacity), `RealtimeStream` and `RaceBarChart`. Docs and demos in English, 简体中文 and 繁體中文.
- **Layout panels** (`TKCode.Arc`, new Layout group): `WaterfallPanel` (cards of different heights flow into equal columns), `TilePanel` (start-screen tiles that span rows and columns and take the first free place) and `CyclicPanel` (a list that loops without a seam).
- **`TransferBox`**: move items between two lists, one by one, as a selection or all at once, with select all, search and the bound collections edited in place.
- **`NumberFilterBox`, `TextFilterBox`**: one filter condition each. The number box has a checkbox, an operator and a value with an optional tolerance; the text box has contains, starts with, ends with, equals, a regular expression and case sensitivity.
- **Gallery community section**: the home page and the READMEs show the QQ group's QR code and number, which can be copied.

### Changed

- **Performance across the library**: controls create parts that are not showing (focus rings, Info badge layers, select menus and more) on first use, so a form with many controls builds much less. Themes select on pseudo-classes instead of property selectors, labels painted every frame reuse a shaped `TextLayout`, tables keep shaped cell text, `TerminalView` caches rendered rows, and the motion engine, content swap, modal presence, rolling digits, palette and token lookups allocate less. These changes are meant to leave motion and appearance as they were.
- **Pictures**: `ArcImageHeader` reads a picture's size from its file header, so a picture much larger than its slot decodes straight to the slot's size.
- **`GanttTimeline`**: zooming 60 tasks allocates about 100 KB per frame instead of 1.8 MB, and rows, bars and arrows outside the view are not drawn.
- **Gallery startup in the browser**: audio and video modules load only on pages with a player, a visitor reading Chinese gets the font ahead of the first frame, API descriptions load per language when first shown, and the sidebar is built when a page first needs it.
- **Gallery**: new desktop icons, and the About page shows its preview badge only on preview builds.
- The `tkcode-arc-lib` template starts at version `0.1.0` instead of `0.1.0-preview.1`.
- Depends on Avalonia 12.1.5 and StreamMark 0.1.2.

### Fixed

- **`ThemeSwitcher`** (issue #74): the new theme no longer flashes before the transition starts. The window's snapshot now covers it from the moment the theme begins to apply, in all four variants.
- **`Tooltip`**: a control passed as the tip is shown itself, not its type name.
- **`GanttTimeline`** (#72): the chart no longer covers the toolbar and footer in a 560 high block. Scroll bars on both axes stay in step with the chart, the today line runs under the bars, arrows route correctly over one-day gaps and to folded phases, and hover highlights the row, bar and arrows.
- **`SankeyFlow`**: the list of labels that particles avoid no longer grows while the overlay repaints alone.
- **Advanced chart labels**: `RaceBarChart` labels fade instead of overlapping when a bar is passed, `SankeyFlow` particles stay off text, `RadarChart` inner rings stay visible on a muted stage, and `Sunburst` legend shares always show one decimal.
- **`InstallSnippet`**: highlighting could hang forever when its continuation never ran on the dispatcher.
- **`InkFluid`**: the simulation is paced so the UI thread can go idle.
- **Templates** (#70): fixed layout problems in the generated `admin`, `devtool`, `workflow`, `ai-chat`, `projects`, `sidebar` and `header` apps, such as clipped text, misaligned table columns and stretched top bar buttons.
- **Gallery in the browser**: Chinese input methods can compose on macOS browsers, where letters used to land in the text box and end the composition. This was checked with simulated composition, not with a real macOS input method.
- **Video** (in this repository, not on NuGet yet): Windows playback no longer stops when nothing pulls frames, such as a minimized window, and `VideoSurface` reads Media Foundation textures the right way up. `Stop`, embedded text tracks and iOS and Android backends were added.
- **`TextReveal`, `InViewTitle`**: Chinese, Japanese and Korean text wraps between characters. A title without spaces was cut off at the edge of a narrow column.
- **`dotnet new install TKCode.Arc.Templates` failed for 0.4.0-preview.6**: the package was released with its template files moved but not the `template.json` that points at them, so it could not be installed (`MV012: Source ... does not exist`). Use this version or later.
- **Lucide icons**: 32 icons whose path starts with a move followed directly by a line (`UserCog`, `FileCog`, `ServerCog`, `Coins`, `Watch`, `ZapOff`, ...) threw "Invalid double value" when first shown; the generator read `16.53.923` as one number. A test now parses every icon's path.

### Known limitations

- `TKCode.Arc.Mobile` is previewed in a desktop phone frame and is not yet verified on Android or iOS devices.
- `TKCode.Arc.Video`, `.Video.Native` and `.Video.Browser` stay in the repository and are not published to NuGet, until Windows and real devices are verified.
- `PdfViewer`: documents without embedded Chinese, Japanese or Korean fonts depend on the machine's fonts, there is no touch drag selection, plain-text web addresses are links only with PDFium, and the Liberation fonts are not in the browser package.

## 0.4.0-preview.6 - 2026-11-08

Eight Pricing blocks, a `dotnet new` template package, SettingsGroup columns and a browser-native video presentation.

### Added

- **Blocks › Pricing** (`TKCode.Arc.Blocks`): `PlanComparison` (an Arc port: plan cards with a monthly or yearly switch that rolls every price, a highlighted plan and a feature matrix) and seven original designs that take Arc's Pro behaviour as a reference: `PricingCalculator` (plan cards priced by a seat slider and a billing switch, with a recommended badge that glides to the plan that fits), `UsagePricing` (drag seats and traffic and the plan name morphs while the estimate counts to the new total), `CartDrawer` (items fly into the cart, a rolling count and a side drawer with steppers, a free-shipping bar and checkout), `CheckoutSummary` (a promo code that turns into a chip, sliding discount and tax lines, rolling totals and a pay button that walks to paid), `LiveCheckout` (a subscription checkout whose summary recalculates live and unfolds into a receipt), `SeatManager` (an avatar row, a seat meter, a stepper with prorated charges, a confirm step and invites you can resend or revoke with undo) and `CreditTopUp` (a balance with a usage chart, preset packs, a custom amount slider, auto top-up and a new balance that rolls in like an odometer). Docs and demos in English, 简体中文 and 繁體中文.
- **Templates** (new package `TKCode.Arc.Templates`): `dotnet new install TKCode.Arc.Templates`, then `dotnet new tkcode-arc` with `--layout blank|sidebar`, `--mvvm none|toolkit` (view models with CommunityToolkit.Mvvm and compiled bindings), `--settings` (on by default: the appearance, the accent, the window placement and the folded sidebar come back on the next start), `--framework` and `--accent`. Item templates add a page (`tkcode-arc-page`, optionally with a view model), a window (`tkcode-arc-window`), a dialog with an awaitable result (`tkcode-arc-dialog`) and an Arc-style control with its token class and theme file (`tkcode-arc-control`). The Installation guide gains a “Start from a template” section.
- `SettingsGroup` lays rows out in a fixed or responsive number of columns (`Columns`, `MinColumnWidth`, `ColumnSpacing`, `RowSpacing`, attached `ColumnSpan`) and shares `LabelWidth`, `ContentAlignment` and `ContentSpacing` with its rows, which can override them. Defaults keep the single-column layout.
- `VideoPlayer.BrowserPresentation="Native"` shows the browser's own video element instead of the canvas: no copies, and every format the browser plays (HLS in Safari, for example). The control bar then sits below the picture and the captions are the browser's.
- `VideoPlayer` system full screen also works in the browser (Fullscreen API), and leaving full screen through the window manager or Escape leaves the player's full screen too.

### Fixed

- Video on Linux (GStreamer): events reach .NET in the order they happened, and a full event batch drops progress events instead of state, error or end events.
- Video (native backends): loading or disposing while a frame is read on another thread no longer frees the frame under the reader, and an error of the previous media keeps its message.

## 0.4.0-preview.5 - 2026-11-08

Fourteen new Special components: Surfaces, Audio and video, and Loaders; Gallery pages open faster.

### Added

- **Special components › Surfaces** (`TKCode.Arc.Special`, original designs that take Arc's Pro behaviour and motion as
  a reference): `SheetStack` (drill-down panels that rise over one another with depth, drag down to close and become
  stacked dialogs on wide screens), `WalletStack` (payment cards in brushed metal, guilloche black, frosted glass and
  color that fan, cycle with a flick and lift out into a balance and recent activity), `ControlCenter` (quick settings
  tiles that morph into their details, a focus-session dial and rubber-band volume and text-size meters), `HoloCard`
  (a card that tilts in perspective toward the pointer under a holographic foil, glitter and glare and turns over),
  `CardToArticle` (story cards that morph into a full article and shrink back when pulled down) and `LinkUnfurl` (a
  composer that turns a pasted address into a link, shows it loading and unrolls it into a preview card that folds back
  or is removed). Docs and demos in English, 简体中文 and 繁體中文.
- **Special components › Audio and video** (`TKCode.Arc.Special`, original designs that take Arc's Pro behaviour and
  motion as a reference): `VoiceOrb` (a voice-mode sphere that breathes when idle, ripples while listening, swirls while
  thinking and pulses while speaking, with `VoiceTranscript` live captions and `VoiceControls` call controls),
  `NowPlaying` (a mini player bar that morphs into a full player with a waveform scrubber, directional artwork skips and
  pull-down to close, plus `NowPlayingLevels`) and `VoiceRecorder` (record into a live waveform, review and scrub, then
  send the take into the thread as a voice bubble). Audio levels, playback and recording are simulated and wired through
  properties and events, so the package takes no audio dependency. Docs and demos in English, 简体中文 and 繁體中文.
- **Special components › Loaders** (`TKCode.Arc.Special`, original designs that take Arc's Pro behaviour and motion as
  a reference): `StretchRefresh` (a feed you pull down by mouse, touch or wheel: rows spread apart, a line stretches
  until it is time to let go, then runs while new rows settle in), `MorphLoader` (four strokes that morph between dots,
  bars, a ring and a square on springs and fold into a check or a cross), `SkeletonMorph` with `MorphBlock` (skeletons
  that stretch into the loaded content block by block while the card eases to its new height), `BlackHole` (a delete
  transition that spirals the chosen items into a singularity while the rest of the content is lensed around it, and
  spins them back out on undo; drawn on the CPU from a snapshot of the content) and `ChargeSession` (a live charging
  card with energy flowing along the cable into the battery, a draggable charge limit, a tapering charge curve and live
  power, energy, time and cost). Docs and demos in English, 简体中文 and 繁體中文.
- `TokenTextEditor.TryGetTokenBounds` returns a token chip's bounds in another visual's coordinates.

### Fixed

- Gallery: a docs page builds its preview with the page and every other demo when it first comes into view, and a
  screenshot is fetched and decoded when it is seen (pages with many demos open in about half the time; the home
  page's live cards and exploded view were built at once although they were meant to wait). Clicking the open page's
  row keeps the page, Up / Down held down in the sidebar build only the page they stop on, and the top bar refits from
  its current level instead of trying every level on each measure.
- Gallery in the browser: a logo asked for twice at once no longer shows as initials for the rest of the session, and
  docs text and screenshots are fetched once per session.

## 0.4.0-preview.4 - 2026-11-07

Lucide icons are compiled into each app on demand: an app ships only the icons it uses.

### Changed

- **Lucide icons per app** (`TKCode.Arc.Icons.Lucide`): the package no longer carries the path data of all 2155 icons;
  its assembly drops from 655 KB to 67 KB (gzip: 130 KB to 31 KB), and an app adds about 100 to 300 bytes per icon it
  uses. A source generator shipped in the package finds the icons in C# (`LucideIconKind.Bell`) and XAML
  (`Kind="Bell"`, `{lucide:LucideGeometry Bell}`, `{x:Static lucide:LucideIconKind.Bell}`, style setters) and compiles
  only those into the project. Nothing changes for icons written like this.
- **Icons chosen at run time must be declared** (breaking): an icon that comes from a binding, from data or from
  XAML loaded at run time is declared in the project file with `<LucideIcon Include="Bell;arrow-right" />`
  (PascalCase or Lucide's own names), or every icon is compiled in with
  `<LucideIconsIncludeAll>true</LucideIconsIncludeAll>`. An icon that is not compiled in draws nothing and logs a
  warning naming the line to add; `LucideIconData.Get` returns an empty string for it. New: `LucideIconData.IsRegistered`
  and `TryGet`.

### Added

- **Video** (new packages `TKCode.Arc.Video`, `TKCode.Arc.Video.Native`, `TKCode.Arc.Video.Browser`, in the repository and not yet on NuGet; `UseArcVideo()`):
  `VideoPlayer` plays a video with Arc controls over the picture (seek bar with buffered range, chapters and hover
  time, volume, speed, captions menu, full screen, auto-hiding bar, keyboard shortcuts, screen-reader value texts) and
  draws WebVTT / SRT captions itself; `VideoSurface` shows the picture alone. Frames are drawn inside the composition
  tree, so controls, rounded corners and transforms work over the video; on macOS and Windows they arrive as GPU
  images without a copy. Decoding uses the system player (Media Foundation, AVFoundation, GStreamer; `ArcVideoNative.Use()`)
  or an HTML video element in the browser (`ArcVideoBrowser.UseAsync()`); no codecs ship with the app. Without a backend
  the player shows a test picture. Built-in text in en-US, zh-CN and zh-TW. New gallery group "Video".
- **Build warnings for Lucide icons**: `ARCL002` marks each place where XAML binds `LucideIcon.Kind` (attribute,
  property element or style setter) while the project declares no icons, since the generator cannot see which icons a
  binding shows; `ARCL001` reports a `<LucideIcon Include>` name that is not a Lucide icon. Declaring icons or setting
  `LucideIconsIncludeAll` ends `ARCL002`; `<NoWarn>ARCL002</NoWarn>` silences it.
- **Core: `ArcViewportHost` and the window-sized `ArcViewport`**: a region that acts as the window for what it holds, for
  narrow-viewport previews (a phone outline in a docs page or a design tool). Modals opened inside mount on it (it is an
  `ArcOverlayHost`), and anchored popups flip and slide to stay within it. `ArcViewport.GetSize`, `GetBounds`, `Observe`
  and `IsCompact` report the nearest host's size, or the window's client size when there is none; `ArcViewport.CompactWidth`
  (640 by default) is the width below which a viewport counts as compact.
- **Gallery: Mobile preview and Fullscreen**: every component page's Preview has a Fit / Mobile switch. Mobile moves the
  running demo onto a phone whose 390 × 844 viewport is its window, keeping its state and focus; the outline morphs from
  the stage. The Fullscreen button grows the Preview over the whole window (Escape closes it), with the phone fitted to
  the window's height in Mobile.

- **Core: `ArcShrinkRow`**: a horizontal row like a horizontal `StackPanel` whose one marked child
  (`ArcShrinkRow.IsShrinkable`) gets only the width left by the others, so its text can end with an ellipsis or wrap.

### Fixed

- **Controls inside a viewport host or a zoomed area**: menus, pickers, toasts, cookie banners, the user menu's sheet and
  the AppShell and AI blocks now size and place themselves by their viewport (`ArcViewport`) instead of the window, so
  a phone-sized `ArcViewportHost` gets the phone's behavior (the user menu becomes a sheet across it, toasts hang from
  its bottom edge). Placement in a zoomed area (`ArcZoom.Scale`) compares window distances and panel sizes in the same
  units: the color, date range, morph select and phone input panels no longer trim their shadow or shift too early.
- **Text in rows at narrow widths**: titles, names and notes next to an icon or a badge in about 25 components
  (DataGrid headers, Dock tabs, the title bar, Sidebar, Kanban, team and AI blocks) now end with an ellipsis or wrap
  instead of being cut off; they sat in horizontal stacks, which never let their trimming or wrapping apply. The meeting
  summary credit, the LLM cost note and the uptime summary wrap or trim as well. Budget variance moves its period switch
  under the title when stacked, and segment switches no longer cut a label wider than an equal share.
- **GanttTimeline follows the Corners presets**: its frame, zoom switch and pill radii come from the radius steps; the
  `FrameRadius`, `SwitchRadius`, `SwitchButtonRadius` and `PillRadius` tokens are removed.

## 0.4.0-preview.3 - 2026-11-06

GanttTimeline for planning projects, `Sidebar` rows built from your own data, and fixes for the markup extensions and the ArcWindow demos.

### Added

- **GanttTimeline** (Blocks › Projects): a project timeline of phases and tasks on a calendar. Drag a bar to move it or an end to resize it (snapping to whole days, with undo and reset), dependency arrows turn red when a task starts before one it depends on has ended, phases fold into one summary bar, `Zoom` switches between day, week and month, and a today line marks where the plan stands. Edits are reported through `TasksChanged`. Keyboard: arrows move between bars, Alt+arrows move a task, Alt+Shift+arrows change its end. Bars spring to their snapped day; reduced motion jumps instead.
- **Sidebar `ItemsSource`**: builds the rows from your own data (a permission list, a menu the server returns) and follows it when it changes. `LabelMember`, `KeyMember`, `IconMember`, `BadgeMember`, `IsEnabledMember` and `ChildrenMember` map the fields, `TrailingTemplate` draws extra content at the end of a row with your object as `DataContext`, and your object is kept in `SidebarItem.Tag`. `SidebarItem` subclasses in `ItemsSource` are used as they are.

### Fixed

- **Markup extensions**: the generated token and text extensions (`ArcDesignTokenResource` and the localized text extensions) return `object` from `ProvideValue`, so they resolve in every property type.
- **Gallery**: the ArcWindow demos clip their title bar preview inside the rounded border instead of on the border itself.

## 0.4.0-preview.2 - 2026-11-06

A Dock layout in the new package `TKCode.Arc.Dock`, `ArcWindow` with an Arc title bar, vertical `ResizablePanels`, lighter controls and fixes for popups inside popups and for the base theme (issues #35, #38, #39 and #40).

### Added

- **Dock layout** (new package `TKCode.Arc.Dock`, `UseArcDock()`; issue #35): an IDE-style layout of panes (`DockPane`) in tab groups, nested in horizontal and vertical splits whose dividers are `ResizablePanels`, around a central document area. Write the tree in XAML (`DockSplit`, `DockGroup`, `DockDocumentArea`) or build a `DockWorkspace` in code; the model tidies itself (emptied groups go, one-child splits collapse, same-direction splits merge). Panes move with `Move(pane, DockTarget)` or the tab menu (Close, Close others, Close all, Move to › New group left/right/above/below, Dock to › Left/Right/Top/Bottom), keep their content instance when they move, and close from the tab, the menu or the keyboard (`PaneClosing` can cancel). Tabs scroll and list themselves in an overflow menu when they do not fit; document tabs show a close button and a modified dot. Keyboard: arrows, Home and End between tabs, Ctrl/⌘+Shift+arrows to reorder, Delete and Ctrl+F4/⌘+W to close, Shift+F10 for the menu. `SaveLayout` and `LoadLayout` write and read the layout as JSON (`tkcode-arc-dock`, version 1, hand-written reader and writer, no reflection). Built-in text in en-US, zh-CN and zh-TW. New gallery group "Dock".
- **Dock layout: drag and drop, floating**: drag a tab along its strip to reorder it (the other tabs spring aside) or away from it to dock it: a ghost follows the pointer, a compass over the group under the pointer joins or splits it, a ring over the document area docks beside all documents, edge buttons dock along the whole layout, a line in a tab strip shows where the tab goes, and the preview glides between targets. Drag a strip's empty part to move the whole group; Escape cancels. `ShowDockIndicators="False"` docks by the pointer's place in a group without the compass. Panes float with `Float`, the tab menu (Float) or by dropping them outside the layout, and dock back with `Dock` (to where they were), the menu, a double click on the title bar or by dragging their window onto the compass. `FloatingMode`: `Auto` (owned windows in classic desktop apps, floating cards inside the layout elsewhere, such as the browser), `Window`, `Overlay`, `Disabled`. Floating windows follow `Workspace.Floats` both ways and come back with a saved layout, pulled onto a visible screen; closing one closes its panes that can close and docks the others. On Wayland, windows open where the system puts them and dock back from the menu. New `FloatActiveCommand`.
- **Dock layout: auto hide, item sources, pane switcher, maximize**: `AutoHide` (tab menu, Shift+Escape) moves a tool to a strip on the layout edge nearest its group; its strip button slides it out over the content as a card (as wide as it was docked, at most 70% of the layout, inner edge resizable), and a click outside, Escape or focus leaving slides it back; `Pin` (the card's pin, the menu, `Dock`) returns it to its group. Auto-hidden panes are saved with the layout and can be declared in XAML through `DockWorkspace.LeftHidden` / `RightHidden` / `TopHidden` / `BottomHidden`. `DocumentsSource` / `ToolsSource` with `DocumentTemplate` / `ToolTemplate` make one pane per item (`DataContext` = item, optional `IDockItem` for id and title, `DockPane.IdOf`): adding an item opens its pane, removing it closes it, closing a tab removes the item from a writable list, and saved layouts restore these panes by id. Ctrl+Tab opens a pane switcher in most-recently-used order (release Ctrl to switch). A double click on a tab strip's empty part (or Maximize in the menu) makes a group fill the layout until restored. New `AutoHideActiveCommand`, `ShowPaneSwitcherCommand`, `ToggleMaximizeCommand`.
- **ArcWindow** (Extras): a desktop window with an Arc title bar, like WPF's `WindowChrome` (issue #38). The client area extends into the decorations and a `TitleBar` is drawn at the top in the theme's colors, with `LeftContent`, `CenterContent` (centered on the whole bar while it fits) and `RightContent` for your own controls. Dragging the bar moves the window, a double click maximizes or restores it, and the edges resize it. On Windows the bar is the system caption (system menu, Aero Snap) and its maximize button opens Windows 11 Snap Layouts; on macOS the traffic lights stay. `CaptionButtons` (`Auto`, `Drawn`, `System`, `None`) picks who draws minimize, maximize and close, and the bar's `Height` sets the title bar height the window asks the system for. Caption button tooltips are localized (en-US, zh-CN, zh-TW).

### Changed

- **ResizablePanels**: `Orientation` (`Horizontal`, the default, or `Vertical`), `Gap` (the space between panes, 1 by default) and panes added or removed at run time, which keep the sizes of the panes that stay (the others give up room in proportion). New `Vertical` example. The Dock layout uses it for its splits.
- **Controls**: rarely used template parts are built when first needed: the keyboard focus ring (Button, Checkbox, Switch, TabItem, Select and others), the Tabs scroll buttons and the Slider value bubbles. Button states map to pseudo-classes. A list of 500 buttons allocates 52.7 MB instead of 61.7 MB and keeps 35.5 MB instead of 41.7 MB. Text fields, selects, sliders and toggles get small motion and token updates.
- **Gallery (desktop)**: the gallery window is an `ArcWindow`; its own title bar class is gone.

### Fixed

- **Popups**: a `Select`, tooltip or other popup opened inside an `ArcPopup` dialog (a native popup window) no longer throws "Unable to create IPopupImpl and no overlay layer is found"; it opens as a native popup window (issue #40).
- **Base theme**: an application `ControlTheme` for one built-in type (for example `PopupRoot`) no longer turns off the whole base theme; only the types the app styles itself are skipped (issue #39).
- **Gallery (WebAssembly)**: switching pages no longer freezes the site; the WebAssembly host is published without ahead-of-time compilation by default (`-p:ArcAot=true` turns it on).

## 0.4.0-preview.1 - 2026-11-03

PropertyGrid for editing the properties of objects, multiline Table cells, Select and ColorPicker sizes, loading packages into a running app and a fix for DataGrid exports in the browser (issues #30, #34, #36 and #37).

### Added

- **PropertyGrid** (Extras): edits the properties of one or more objects in rows of label and editor, like the properties palette of a CAD or modeling tool (issue #30). Mark a class `[PropertyGridModel]` and the Foundation generator registers its properties with typed delegates at compile time (no reflection; AOT and trimming safe), reading `[Category]`, `[DisplayName]`, `[Description]`, `[ReadOnly]`, `[Browsable]` and the new `[PropertyGridOptions]`. Other types are registered in code with `PropertyGridTypes.Register<T>`, and Avalonia objects are described by their registered properties. Built-in editors are Arc's small fields (`Input`, `NumberField`, `Switch`, `Select`, `ColorPicker`, `DatePicker`); `Editors` adds custom ones by key, property name or type. Rows group by category, filter by search, show the selected row's description, expand nested values (structs are written back to their owner) and lists (with add and remove buttons), follow `INotifyPropertyChanged`, and edit several objects at once (`SelectedObjects`, with mixed values). Rows are `PropertyGridItem` controls with pseudo-classes for styling. New generator diagnostics ARCF030–ARCF032.
- **Table**: multiline cells (issue #37). `DataTableColumn.TextWrapping` (default `NoWrap`, one line with an ellipsis) and `MaxLines` let a column show line breaks and wrap long text; each row is as tall as its tallest wrapped cell, with 52 px (64 px in the compact layout) as the minimum. `CellTemplate` columns with `TextWrapping="Wrap"` grow the row to the template. Tables without a wrapping column keep the uniform row height and cost nothing extra; with one, wrapped text is measured only when data, font or column width changes. New `Multiline cells` example.
- **Select** and **ColorPicker**: `Size` (`Small`, `Medium`, `Large` for Select; `Small` and `Medium` for ColorPicker), so they line up with small text fields. The default stays Medium.
- **Foundation**: `ArcRuntime.UsePackages(arc => ...)` enables packages in a running application, for example the package of an assembly that a browser app downloads when a page first needs it (issue #34). The new packages are set up as at startup: their token identities, control themes, language catalogs, presets and commit callbacks are registered, and the applied theme is published again with their tokens, theme scopes included. Packages that are already enabled are skipped, and a conflict or a missing translation throws before anything changes.

### Changed

- **Gallery (WebAssembly)**: the Special components (`TKCode.Arc.Special`) are downloaded when the first Special page opens instead of at startup: the home page downloads 1.2 MB less (brotli), 22.2 MB before (issue #34). The gallery also stops running the static constructor of every documented control at startup: the API tables look up their types and properties only when a page shows them.

### Fixed

- **DataGrid**: exporting to xlsx or CSV in the browser no longer ends with an empty file and "export failed" (issue #36). The browser's save-file stream accepts only asynchronous writes, so the export is built in memory and then written with `WriteAsync`.

## 0.3.2-preview.2 - 2026-10-30

Nested submenus for the menus, row templates for TreeView, bindable inline options and a fix for building on Windows (issues #28, #29, #31 and #32).

### Added

- **TreeView**: `ItemTemplate` lays out the label area of each row with your own content, such as a name with its type, a status dot or a tooltip, while the tree keeps drawing the row, its fills, indent, chevron and icon and stays virtualized (issue #28). `DisabledSelector` and `TreeViewItem.IsDisabled` show nodes that cannot be selected; keyboard focus still reaches them, so a disabled folder still opens. `RowHeight` sets the height of every row, for example for two-line content.
- **Menus**: nested submenus for `DropdownMenu`, `ContextMenu` and any `MenuSurface` (issue #29). Nest `MenuEntry` elements inside a row (`MenuEntry.Items`, or `MenuEntry.ItemsSource`), or set `ChildrenSelector` for `ItemsSource` data. A row with a submenu shows a chevron and opens the submenu beside the menu on a click, ArrowRight, Enter or Space, or after the pointer rests on it for 100 ms; the pointer can cross other rows on its way into it. ArrowLeft or Escape close one level, Tab closes them all, and choosing a row at any level closes the whole menu and raises `ItemClicked` once. Submenus open on the left when there is no room on the right and keep that direction at deeper levels. New members: `MenuEntry.Items`, `MenuEntry.ItemsSource`, `MenuEntry.HasSubmenu`, the `:submenu-open` pseudo-class, and `ChildrenSelector` on `MenuSurface`, `DropdownMenu` and `ContextMenu`.
- **Core**: `SubmenuSafeZone` (`TKCode.Arc.Primitives`), the pointer path toward an open submenu, shared by the menus and `ModelMenu`.

### Changed

- **Breaking: inline option types are bindable.** `SegmentedOption`, `RadioOption`, `RadioCardOption`, `BillingToggleOption`, `FilterField`, `FilterOption`, `ReactionOption`, `SliderMark`, `GaugeThreshold`, `ActionSwapState`, `Announcement`, `ChatParticipant` and `HoverCardStat` now derive from `AvaloniaObject`, and their properties are styled properties. Options written inline in XAML take `{Binding ...}` and `{DynamicResource ...}` (for example a localized label), and the control that shows them follows later changes (issue #32). Constructors, object initializers and property names are unchanged, but code compiled against the old types must be rebuilt, and these objects must be created and changed on the UI thread. `FilterChip` stays an immutable value type.
- `BillingToggle.DefaultOptions` returns new options on each read, so toggles no longer share them.
- **Menus**: `MenuSurface.ItemClicked` is raised on the outermost surface for rows of every submenu level, so `DropdownMenu.ItemClicked` and `ContextMenu.ItemClicked` report nested rows too.

### Fixed

- **Build on Windows**: projects under `src/` count as libraries again on Windows, so `dotnet pack` produces packages and the XML documentation (the gallery's API tables) is written; generated sources left on disk (`GeneratedFiles/`) are never compiled a second time, and each target framework writes its own `GeneratedFiles/<tfm>` folder (issue #31). CI also builds on Windows.
- **Corners preset**: the analytics segment switches, KPI drilldown, metrics dashboard, small multiples, team members, source citations and gravity well take their radii from the radius tokens, so `t.Corners(...)` squares or scales them like the rest.
- **Usage forecast**: with motion off the chart no longer keeps a frame loop running after a change.

## 0.3.2-preview.1 - 2026-10-28

Arc no longer depends on AtomUI. Every package now depends only on Avalonia and on the new MIT package
`TKCode.Arc.Foundation`, so the whole dependency chain is under permissive licenses and closed-source applications can
publish Arc as a single file or with NativeAOT (issue #25). The application entry point and the theme API change; the
controls, their properties and the XAML (`{arc:ArcDesignTokenResource ...}`, `{arc:XxxTokenResource ...}`) do not. See
*Migrating from 0.3.1 to 0.3.2* below.

### Added

- **Foundation** (new package `TKCode.Arc.Foundation`): the theme manager (`ArcThemeManager`, `ArcTheme`,
  `ArcThemeState`, `ArcThemeBuilder`), theme presets (`ArcThemePreset`), local themes (`ArcThemeScope`), package
  registration (`AppBuilder.UseArc(...)`, `ArcBuilder`), languages (`ArcLanguage`, `ArcLanguageManager`) and a source
  generator for tokens, theme files and language catalogs. No reflection; trimming and NativeAOT safe. A control's
  theme loads the first time a control of that type is used.
- **ArcImage** and `ImageSource` (Core): background image loading (http, `avares://`, files, `data:`, bitmaps) with a
  shared cache, on desktop and in the browser.
- **ArcOverlayHost** (Core): a panel with its own overlay layer, so dialogs, drawers and sheets opened inside it stay
  within it.
- **Base theme** (Core): Arc's own themes for the Avalonia controls its templates use (`ScrollViewer`, `ScrollBar`,
  `ItemsControl`, `PathIcon`, `TextSelectionHandle` and the popup hosts). It steps aside when the application uses
  `FluentTheme` or `SimpleTheme`.
- **Scroll bars** look like `ScrollArea`: thin, shown while the pointer is over the view or while it scrolls, hidden
  at rest. `ScrollViewer.AllowAutoHide="False"` keeps them visible.
- **Corners theme**: `UseArcCorners()` and `t.Corners(style)`; ready-made `ArcCornerPresets.Square` and
  `ArcCornerPresets.Half`.
- **Menus**: long menus scroll inside `MaxDropDownHeight`.
- **Gallery**: the desktop window draws its own title bar (logo, title, pin on top; window buttons on Windows and
  Linux).

### Changed

- **Breaking: entry point.** `AppBuilder.UseAtomUI(b => b.UseDesktopControls().UseArc())` is now
  `AppBuilder.UseArc(arc => arc.UseArcControls())`. The `Application` class no longer needs to be `partial`.
- **Breaking: theme API.** `ThemeConfigBuilder`, `ThemeAlgorithm`, `IThemeManager.ApplyThemeAsync` and `WithArc(...)`
  are replaced by `ArcTheme.Apply(t => ...)`, `ArcThemeManager.Apply` / `ApplyAsync` and `WithTheme(...)` in `UseArc`.
- **Breaking: Pixel and Corners** are theme presets: `t.Pixel()` and `t.Corners(style)`. `ArcPixelTheme`,
  `ArcPixelScope`, `WithArcCorners` and `ArcCornerScope` are removed; a preset skips the packages the application has
  not registered.
- **Breaking: languages** move to `TKCode.Arc.Localization` (`ArcLanguage`, `ArcLanguageManager`); `UseLanguages` is
  on `ArcBuilder`.
- **Breaking: images and popups.** `AsyncImage` is replaced by `ArcImage` (`IsLoaded` is now `IsImageLoaded`).
  `ArcPopup` derives from Avalonia's `Popup`; `OpenMotion` and `CloseMotion` are `ArcPopupMotion`,
  `AdjustPlacementAnchor` lost its `ref flipX, ref flipY` parameters, and `IsOpen` stays true until the exit motion
  finishes. `ArcPopupFrame` replaces `BaseMotionActor`, and `ArcOverlayHost` replaces `ScopeAwareOverlayLayerPanel`.
- **Breaking: mobile token identities** are `MobileButton`, `MobileCheckbox`, `MobileDialog`, `MobileSwitch` and
  `MobileToast`, so they no longer share names with the desktop controls.
- Arc no longer shares its theme with AtomUI controls; applications that mix the two stay on 0.3.1-preview.6.
- **Performance**: `Button` builds its loading spinner the first time it loads; `Select` values and field messages shape their text only when needed.

### Fixed

- **ToastStack**: a toast dismissed while the stack resumes its timers no longer breaks the resume.
- **DataGrid**: the search box of the filter list is drawn again (it had no template).
- **PasswordField**: long strength labels no longer overlap the strength bar (#23).
- **Gallery**: the text box of the env variables event log is drawn again (it had no template).
- **Popups**: menus, selects and popovers draw their whole shadow again; the popup host clipped it, so their corners looked square.
- **Gallery**: the swipe actions inbox demo has its header, a live count and avatars, like Arc's.

### Migrating from 0.3.1 to 0.3.2

1. Remove every `AtomUI.*` package reference and `using AtomUI...;` line; Arc brings in what it needs.
2. Replace `UseAtomUI(...)` with `UseArc(...)` and rename `UseArc()` (the controls) to `UseArcControls()`. The other
   package calls keep their names and move into the `UseArc` callback.
3. Replace theme requests with `ArcTheme.Apply`. A change names only what changes; the rest of the state stays.
4. The `App` class may drop its `partial` keyword; nothing generates code into it any more.
5. With the .NET 11 SDK and `PublishAot`, set `<UseOptimizedPublish>false</UseOptimizedPublish>` in the application
   project.

```csharp
// 0.3.1
AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .UseAtomUI(builder =>
          {
              builder.UseDesktopControls();
              builder.UseArc();
              builder.UseArcBlocks();
              builder.UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN]);
          });

// 0.3.2
AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .UseArc(arc => arc
              .UseArcControls()
              .UseArcBlocks()
              .UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN])
              .WithTheme(t => t.Appearance(ArcAppearance.System)));
```

```csharp
// 0.3.1
var builder = new ThemeConfigBuilder()
    .WithAlgorithms(ThemeAlgorithm.Default, ThemeAlgorithm.Dark)
    .WithArc(arc => arc.Accent(ArcAccentPreset.Blue).MotionMode(ArcMotionLevel.Reduced));
await manager.ApplyThemeAsync(new ThemeRequest(IThemeManager.DEFAULT_THEME_ID, builder.Build(),
                                               ThemeTransitionReason.UserRequest));

// 0.3.2
ArcTheme.Apply(t => t
    .Appearance(ArcAppearance.Dark)
    .Accent(ArcAccentPreset.Blue)
    .MotionLevel(ArcMotionLevel.Reduced));
```

| 0.3.1 (AtomUI) | 0.3.2 |
|---|---|
| `AppBuilder.UseAtomUI(b => ...)`, `UseDesktopControls()` | `AppBuilder.UseArc(arc => ...)` |
| `UseArc()` (the controls) | `UseArcControls()` |
| `UseArcCore`, `UseArcBlocks`, `UseArcAdvancedCharts`, `UseArcSpecial`, `UseArcWorkflow`, `UseArcMobile`, `UseArcPixelTheme`, `UseArcGallery` on `IAtomUIBuilder` | The same names on `ArcBuilder`; new `UseArcCorners()` |
| `builder.UseLanguages(...)` | `arc.UseLanguages(...)`, same arguments |
| `partial class App`, `ApplicationStartup.Attach` | Not needed |
| `ThemeConfigBuilder`, `WithAlgorithms(ThemeAlgorithm.Dark)` | `t.Appearance(ArcAppearance.Dark)` (or `Light`, `System`) |
| `IThemeManager.ApplyThemeAsync(new ThemeRequest(...))` | `ArcTheme.Apply(t => ...)`, `ArcThemeManager.Current.Apply` / `ApplyAsync` (await it; never block on it on the UI thread) |
| `WithArc(a => a.Accent(preset or color))` | `t.Accent(preset or color)` |
| `WithArc(a => a.Token(name, value))` | `t.Token(name, value)` |
| `WithArc(a => a.ControlToken(catalog, control, name, value))` | `t.Token(identity, name, value)` |
| `WithArc(a => a.MotionMode(level))` | `t.MotionLevel(level)` |
| `WithArcCorners(style, scope)`, `arc.Corners(style, scope)`, `ArcCornerScope` | `t.Corners(style)`, `ArcCornerPresets` |
| `ArcPixelTheme.CreateRequest(...)`, `WithArcPixel(...)`, `ArcPixelScope` | `t.Pixel()`, `ArcPixelPresets`; back to Arc with `t.RemovePreset(ArcPixelPresets.Slot)` |
| `using AtomUI.Localization;` | `using TKCode.Arc.Localization;` |
| `Application.Current.GetLanguageManager()`, `ILanguageManager` | `ArcLanguageManager.Current` |
| `GetLocalizer().Get(kind)`, `Format(kind, args)` | `XxxLangResources.Get(kind)`, `XxxLangResources.Format(kind, args)` |
| `LanguageChanged`, `ChangeLanguage(tag)` | `Changed`, `Set(tag)` (or `ArcLanguage.Changed`, `ArcLanguage.Set`) |
| `FormattingCulture`, `CurrentLanguage` | `ArcLanguage.Culture`, `ArcLanguage.Current` |
| Mobile token overrides by catalog | Identities `MobileButton`, `MobileCheckbox`, `MobileDialog`, `MobileSwitch`, `MobileToast` |
| `AtomUI.Controls.ImageSource`, `AsyncImage`, `IsLoaded` | `TKCode.Arc.ImageSource`, `ArcImage`, `IsImageLoaded` |
| `ArcPopup` on AtomUI's `Popup`, `BaseMotionActor` | `ArcPopup` on Avalonia's `Popup`, `ArcPopupFrame`; `OpenMotion` / `CloseMotion` are `ArcPopupMotion` |
| `AdjustPlacementAnchor(placement, ref flipX, ref flipY)` | `AdjustPlacementAnchor(placement)` |
| `ScopeAwareOverlayLayerPanel` | `ArcOverlayHost` |
| AtomUI's themes for `ScrollViewer`, `ScrollBar`, `ItemsControl` and the popup hosts | Arc's base theme; scroll bars hide at rest (`ScrollViewer.AllowAutoHide="False"` keeps them) |

- **Windows and overlays.** Without AtomUI's window theme, a plain Avalonia `Window` has no overlay layer unless its
  template has a `VisualLayerManager` with `EnableOverlayLayer="True"`. `ThemeSwitcher` needs one for its transition;
  without it, it switches directly.
- **AtomUI controls.** An application that also uses AtomUI controls no longer gets one shared theme for both. Stay on
  0.3.1-preview.6 until those screens are moved to Arc.

## 0.3.1-preview.6 - 2026-10-28

### Added

- **Cascader** (Extras): pick one node of a hierarchy level by level, such as province, city and district (#22). The
  field, menu surface, gliding highlight and clear button match `Select`; a parent opens its children in a column to
  the right and the menu width springs to its new size. Data is read through delegates (`ChildrenSelector`,
  `TextSelector`, `ValueSelector`, `DisabledSelector`, `IsLeafSelector`), so nothing is reflected. `SelectedItem`,
  `SelectedValue` and `SelectedPath` stay in step, and a value written before the data arrives is kept and resolved
  when it does. Options: `LeafOnly`, `ExpandTrigger` (click or hover), `ShowFullPath`, `PathSeparator`, `IsClearable`,
  `IsReadOnly`, `ColumnWidth`. Full keyboard support, with a Gallery page and six demos.

## 0.3.1-preview.5 - 2026-10-28

### Added

- **Audio** (new packages `TKCode.Arc.Audio`, `TKCode.Arc.Audio.Native` and `TKCode.Arc.Audio.Browser`): the
  `IAudioPlayer` abstraction behind the media blocks with a silent simulated backend by default, desktop playback
  through a miniaudio shim (`ArcAudioNative.Use()`, native libraries under `runtimes/<rid>/native`) and WebAssembly
  playback through an HTML audio element (`ArcAudioBrowser.UseAsync()`).
- **Blocks**: `MediaPlayer` (cover art, playlist, seek bar, previous and next, volume with mute; compact and flat
  variants) and `FileUpload` (drop zone, file list, per-file progress and status, validation, single-file mode).
- **Special**: a new Cursor group with `DotGrid`, `GravityWell` and `InkFluid` pointer effects.
- **Gallery**: the Northstar dashboard demo at `/demo`, a copy of uiarc.dev/demo with its eleven pages (Overview,
  Analytics, Customers, Projects, Team, Activity, Status, Integrations, Developers, Billing, Settings) and the sign-in
  page, built from Arc components and blocks; open it from the home page's "Live demo" button or the top menu's
  "Demo". `tools/docshots` gains `route <address> <out.png>` to render any gallery address.

### Fixed

- **ArcContentSwap**: showing a control again after it was swapped out no longer throws "already has a visual
  parent" (switching the Webhooks inspector between Payload and Headers crashed the app).
- **Charts**: the value-axis tick next to the average line stays hidden, so "Avg 44" no longer overlaps "40".
- **Webhooks**: Payload and Headers each open at their first line instead of the other tab's scroll position.

## 0.3.1-preview.4 - 2026-10-27

### Added

- **Terminal** (new package `TKCode.Arc.Terminal`, net10.0 and net11.0): the `TerminalView` control and a managed facade
  over libghostty-vt (VT parsing, screen state, selection, input encoding), moved here from the TKCode app. The native
  library ships under `runtimes/<rid>/native`; the binaries are not committed, `tools/terminal/sync-native.sh` copies
  them in from the TKCode build before packing. The namespaces stay `TKCode.Terminal.Core` and `TKCode.Terminal.Avalonia`.

## 0.3.1-preview.3 - 2026-10-27

### Added

- **Blocks**: about 25 new blocks in Analytics (`MetricExplorer`, `MetricsDashboard`, `KpiDrilldown`, `CohortRetention`,
  `MrrWaterfall`, `RevenueExplorer`, `UsageForecast`, `UptimeStatus`, `HorizonChart`, `ConnectedScatter`,
  `JourneyFlow`, `ChoroplethExplorer`), Developer (`ApiKeys`, `Webhooks`, `Integrations`, `EnvVariables`), People
  (`TeamMembers`, `TeamDirectory`, `RolesPermissions`, `InvitePeople`, `ProfilePage`), Finance (`BudgetVariance`,
  `MarketTerminal`) and LineCharts (`LineReplay`, `SmallMultiples`), each with Gallery pages, demos and tests.
- **RichTextEditor** and **ActionSwap** controls, with Gallery pages and demos.
- **Mobile**: `Dialog`, `Toast`, `ActionSheet`, `Toolbar`, `PullToRefresh`, `Radio` and `IconButton`, keyboard
  avoidance, and Arc motion for the Mobile controls.
- **Gallery**: the Desktop app checks for updates and sets the macOS Dock icon; the Browser host can load map data over HTTP.

### Changed

- **Table**, **Editors**, **Selects** and **Toggles** are reworked; the Table sources moved within `Tables/`.

### Fixed

- **TimePicker**, **FilterMenu**: the empty area of a row ignores pointer input (#20); **Combobox**: clearing no longer opens the list.
- **Password**: the show/hide choice stays consistent with the input state (#21).
- **Gallery**: a duplicated IME commit in the sidebar search box on Windows.

## 0.3.1-preview.2 - 2026-10-26

### Added

- **Corners theme** (new package `TKCode.Arc.Themes.Corners`, `WithArcCorners(style, scope)`): a global corner style
  made of token overrides only, square or scaled corners for every Arc control, with colors, borders, shadows and
  motion unchanged. The Gallery's Appearance panel and the Theming guide cover it.
- **Mobile**: `ListGroup` / `ListItem`, `Sheet`, `TextField` and `Searchbar`; `Navbar` gets a large-title mode and
  `Button` more variants. Gallery pages and demos for `MobileListGroup`, `MobileNavigationStack` (including tabs with
  stacks) and `MobileSheet`.
- **Core tokens**: further design tokens and `ArcTokens` helpers so themes can bind corner radii.

### Changed

- **Pixel theme**: the radii and theme resources are regenerated from the shared corner table.
- **Blocks**: `WeekCalendar` draws its head and grid edges with the new edge tokens.

### Fixed

- **Stepper**: the glow of the current step is no longer clipped (#19).

## 0.3.1-preview.1 - 2026-10-26

### Added

- **Mobile** (new package `TKCode.Arc.Mobile`, call `UseArcMobile()`): the first touch-first controls, `Button`
  (press state, 44 x 44 touch target, no hover), `Switch`, `Page` (header, content and footer inside the device's safe
  area), `Navbar` and `Tabbar` / `TabbarItem`. The package depends on `TKCode.Arc.Core` only and has its own namespace
  `TKCode.Arc.Mobile`, so its `Button` and `Switch` can sit next to the desktop ones. It has not been verified on
  Android or iOS devices yet, and it still pulls `Avalonia.Desktop` and `AtomUI.Desktop.Controls` in through
  `TKCode.Arc.Core`.
- **Gallery › Mobile**: a Mobile section with a phone-frame preview: iPhone 15, Pixel, small phone and tablet,
  portrait and landscape, a status bar, a simulated safe area and a soft-keyboard placeholder. It is a desktop
  simulation, not a device.
- **Pixel theme** (new package `TKCode.Arc.Themes.Pixel`, call `UseArcPixelTheme()`): square corners, hard shadows, a
  hand-tuned limited palette in light and dark (`ArcPixelTheme.CreateRequest(dark)`, `ArcPixelScope` picks which
  packages it covers), plus the Silkscreen and VT323 pixel fonts (SIL OFL) in `TKCode.Arc.Fonts`
  (`WithArcPixelFonts()`). The Gallery's Appearance panel has a Style switch, Arc or Pixel.
- **Tables › TreeTable**: a table with nested rows, tri-state selection, hierarchy-preserving sort, lazy-loaded
  children and treegrid keyboard navigation. The Gallery marks new components with a New badge for 60 days.
- **Core tokens**: `EdgeThickness`, `FocusEdgeThickness`, 30 `RadiusStep*` and 8 `EdgeThickness*` design tokens, so a
  theme can change corner radii and border widths; every default equals the value it replaces. `ArcTokens.BindCornerRadius`
  and `ArcPixelRendering` (aliased edges, nearest-neighbour bitmaps).

### Changed

- **Gallery** (web): the WebAssembly host is published without ahead-of-time compilation again, because the compiled
  build crashed at startup; `-p:ArcAot=true` turns it back on. Its icon URLs are versioned so browsers drop the old
  favicon.

### Fixed

- **Carousel**: a slide whose timer had already run out no longer advances the rotation again without yielding; it
  starts a fresh interval.

## 0.3.0-preview.5 - 2026-10-24

### Added

- **Blocks › Auth** (`TKCode.Arc.Blocks`): `SignIn`, `SignupForm`, `LoginSplit`, `LoginCentered`, `LoginImmersive`,
  `OtpInput`, `OnboardingIllustration` and `MultiStepForm`: sign-in and sign-up flows with providers, one-time codes,
  validation and success states, an illustrated onboarding and a multi-step form. Docs and demos in English, 简体中文
  and 繁體中文.
- **Workflow canvas** (new package `TKCode.Arc.Workflow`, call `UseArcWorkflow()`; the look and the run animation follow
  the workflow graph of Arc's free Hero section block): `WorkflowCanvas` shows a `WorkflowGraph` of steps and
  connections on a dotted canvas and plays its runs. Set each step's state as your run goes: when a step finishes, the
  data draws along its edges in the accent before the next step shows as running, a skipped branch turns dashed and
  fades, and every step reports its result and time. Steps without a position are arranged in ranks, top to bottom or
  left to right; the whole workflow shows at actual size and shrinks like a screenshot when it does not fit.
  `WorkflowNodeCard` (one row, or two rows when compact) also works on its own. Pan with a drag or the wheel, zoom with
  Ctrl / ⌘ + wheel, a pinch or the + / − keys, or with the `WorkflowZoomControls` bar; edges can be curves, right angles
  or straight lines; a workflow the canvas arranged glides into place when it changes, and switches to Arc's phone
  layout under 520 wide. Arrow keys move between steps (Alt follows the connections), Enter opens a step
  (`NodeActivated`), and screen readers hear failures and the end of a run. Built-in text in English, 简体中文 and
  繁體中文; docs and demos in all three. With `IsEditable` people drag steps (they snap to the grid), select
  several with Shift + drag, move the selection with the keyboard and delete it; `IsLocked` steps stay put, and
  `GraphEdited` reports each finished edit. People connect steps by dragging from an output to another step (a
  step can have named ports, such as a condition's "Yes" and "No"), move or remove an edge by its end, select and
  delete edges, and connect from the keyboard with C. Each connection is checked (no loops, matching `DataType`s,
  `MaxConnections`); `ConnectionRequested` adds your own rules, and `ConnectionDroppedOnCanvas` tells you where a
  connection was let go on the empty canvas. `WorkflowGraph.History` undoes and redoes the edits made on the canvas
  (Ctrl / ⌘ + Z), and `History.BeginGroup()` makes your own changes one undoable edit. Copy, cut, paste and duplicate
  steps with the usual keys (the clipboard carries them as JSON between apps). `NodeTemplates` gives the cards of a
  `Kind` of step their own content (`WorkflowNodeTemplate`), with inputs and switches that keep their own keys.
  `WorkflowMinimap` shows the whole workflow with the part on screen; a workflow of 500 steps stays responsive, and
  only the cards near the view are built once a workflow has more than 300 steps. Toolbar buttons bind to the canvas's
  commands (`UndoCommand`, `DeleteSelectionCommand`, `ZoomInCommand`, …), which enable and disable themselves.
- **Core**: `PanZoomTransform` and `WheelRouter` (pan-and-zoom view math and wheel, trackpad and pinch routing) and
  `LayeredGraphLayout` (a deterministic layered layout for directed graphs, about 2 ms for 500 nodes).

### Changed

- **Gallery** (web): the published site is compiled ahead of time to WebAssembly as well as trimmed. On a CPU four times
  slower than an M5, opening an Accordion row on its docs page cost the main thread 6.7 ms a frame and dropped about six
  frames; compiled, it costs 3.0 ms and drops none. The first visit downloads more (26 MB instead of 13.5 MB, brotli) and
  the image takes about 12 minutes longer to build; `-p:RunAOTCompilation=false` publishes without it.

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
- **Accordion** and **ExpandableCard**: their dividers are filled strips instead of one-sided borders. Avalonia draws a
  border with an uneven thickness as a path (two outlines and a boolean exclude) and builds it again on every size
  change, which happened every frame while a row or a card opened. The look is unchanged, pixel for pixel.
- **Markdown** and the AI answers now use StreamMark `0.1.0` (was `0.1.0-alpha.25`). A Markdown view that had left
  the window stayed subscribed to its document, so a document or stream that outlives the view (a chat message
  the app keeps) kept the view and its page in memory. Code blocks and tables also stop rebuilding their pens and
  outlines on every frame while content above them opens.

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

[1.0.0]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.4.0-preview.6...v1.0.0
[0.4.0-preview.6]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.4.0-preview.5...v0.4.0-preview.6
[0.4.0-preview.5]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.4.0-preview.4...v0.4.0-preview.5
[0.4.0-preview.4]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.4.0-preview.3...v0.4.0-preview.4
[0.4.0-preview.3]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.4.0-preview.2...v0.4.0-preview.3
[0.4.0-preview.2]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.4.0-preview.1...v0.4.0-preview.2
[0.4.0-preview.1]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.2-preview.2...v0.4.0-preview.1
[0.3.2-preview.2]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.2-preview.1...v0.3.2-preview.2
[0.3.2-preview.1]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.1-preview.6...v0.3.2-preview.1
[0.3.1-preview.6]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.1-preview.5...v0.3.1-preview.6
[0.3.1-preview.5]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.1-preview.4...v0.3.1-preview.5
[0.3.1-preview.4]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.1-preview.3...v0.3.1-preview.4
[0.3.1-preview.3]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.1-preview.2...v0.3.1-preview.3
[0.3.1-preview.2]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.1-preview.1...v0.3.1-preview.2
[0.3.1-preview.1]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.0-preview.5...v0.3.1-preview.1
[0.3.0-preview.5]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.0-preview.4...v0.3.0-preview.5
[0.3.0-preview.4]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.0-preview.3...v0.3.0-preview.4
[0.3.0-preview.3]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.0-preview.2...v0.3.0-preview.3
[0.3.0-preview.2]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.0-preview.1...v0.3.0-preview.2
[0.3.0-preview.1]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.2.0-preview.1...v0.3.0-preview.1
[0.2.0-preview.1]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.1.1-preview.1...v0.2.0-preview.1
[0.1.1-preview.1]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.1.0-preview.1...v0.1.1-preview.1
[0.1.0-preview.1]: https://github.com/AIDotNet/TKCode.Arc/releases/tag/v0.1.0-preview.1
