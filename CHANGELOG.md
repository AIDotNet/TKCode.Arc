# Changelog

All notable changes to TKCode.Arc are listed here. Versions follow [Semantic Versioning](https://semver.org/);
while the version carries a `-preview` suffix the API may still change between releases.

NuGet packages published with each release: `TKCode.Arc`, `TKCode.Arc.Core`, `TKCode.Arc.Foundation` (from 0.3.2),
`TKCode.Arc.Fonts`, `TKCode.Arc.Icons.Lucide`, `TKCode.Arc.Blocks`, `TKCode.Arc.AdvancedCharts`, `TKCode.Arc.Special`,
`TKCode.Arc.Workflow`, `TKCode.Arc.Themes.Pixel`, `TKCode.Arc.Themes.Corners`, `TKCode.Arc.Mobile`, `TKCode.Arc.Terminal`, `TKCode.Arc.Audio`,
`TKCode.Arc.Audio.Native` and `TKCode.Arc.Audio.Browser`.

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
- Arc no longer shares its theme with AtomUI controls; applications that mix the two stay on 0.3.2-preview.1.
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

## 0.3.2-preview.1 - 2026-10-28

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

[0.3.2-preview.1]: https://github.com/AIDotNet/TKCode.Arc/compare/v0.3.1-preview.5...v0.3.2-preview.1
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
