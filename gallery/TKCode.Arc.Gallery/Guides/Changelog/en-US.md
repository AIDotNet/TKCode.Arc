---
title: Changelog
---

# Changelog

All notable changes to TKCode.Arc, newest first. Versions follow [Semantic Versioning](https://semver.org/). While a
version carries a `-preview` suffix, the API may still change between releases; read the notes before upgrading and
keep the version pinned in your project. The same list is kept in `CHANGELOG.md` in the repository.

NuGet packages published with each release: `TKCode.Arc`, `TKCode.Arc.Core`, `TKCode.Arc.Foundation` (from 0.4), `TKCode.Arc.Fonts` and
`TKCode.Arc.Icons.Lucide`, `TKCode.Arc.Blocks`, `TKCode.Arc.AdvancedCharts`, `TKCode.Arc.Special`, `TKCode.Arc.Workflow`, `TKCode.Arc.Themes.Pixel`, `TKCode.Arc.Themes.Corners`, `TKCode.Arc.Mobile`, `TKCode.Arc.Terminal`, `TKCode.Arc.Audio`, `TKCode.Arc.Audio.Native` and `TKCode.Arc.Audio.Browser`.

## 0.4.0-preview.1 (unreleased)

Arc no longer depends on AtomUI. Every package now depends only on Avalonia and on the new MIT package `TKCode.Arc.Foundation`, so the whole dependency chain is under permissive licenses and closed-source applications can publish Arc as a single file or with NativeAOT (issue #25). The application entry point and the theme API change; the controls, their properties and the XAML (`{arc:ArcDesignTokenResource ...}`, `{arc:XxxTokenResource ...}`) do not. See *Migrating from 0.3 to 0.4* below.

### Added

- **Foundation** (new package `TKCode.Arc.Foundation`): the theme manager (`ArcThemeManager`, `ArcTheme`, `ArcThemeState`, `ArcThemeBuilder`), theme presets (`ArcThemePreset`), local themes (`ArcThemeScope`), package registration (`AppBuilder.UseArc(...)`, `ArcBuilder`), languages (`ArcLanguage`, `ArcLanguageManager`) and a source generator for tokens, theme files and language catalogs. No reflection; trimming and NativeAOT safe. A control's theme loads the first time a control of that type is used.
- **ArcImage** and `ImageSource` (Core): background image loading (http, `avares://`, files, `data:`, bitmaps) with a shared cache, on desktop and in the browser.
- **ArcOverlayHost** (Core): a panel with its own overlay layer, so dialogs, drawers and sheets opened inside it stay within it.
- **Base theme** (Core): Arc's own themes for the Avalonia controls its templates use (`ScrollViewer`, `ScrollBar`, `ItemsControl`, `PathIcon`, `TextSelectionHandle` and the popup hosts). It steps aside when the application uses `FluentTheme` or `SimpleTheme`.
- **Scroll bars** look like `ScrollArea`: thin, shown while the pointer is over the view or while it scrolls, hidden at rest. `ScrollViewer.AllowAutoHide="False"` keeps them visible.
- **Corners theme**: `UseArcCorners()` and `t.Corners(style)`; ready-made `ArcCornerPresets.Square` and `ArcCornerPresets.Half`.
- **Menus**: long menus scroll inside `MaxDropDownHeight`.
- **Gallery**: the desktop window draws its own title bar (logo, title, pin on top; window buttons on Windows and Linux).

### Changed

- **Breaking: entry point.** `AppBuilder.UseAtomUI(b => b.UseDesktopControls().UseArc())` is now `AppBuilder.UseArc(arc => arc.UseArcControls())`. The `Application` class no longer needs to be `partial`.
- **Breaking: theme API.** `ThemeConfigBuilder`, `ThemeAlgorithm`, `IThemeManager.ApplyThemeAsync` and `WithArc(...)` are replaced by `ArcTheme.Apply(t => ...)`, `ArcThemeManager.Apply` / `ApplyAsync` and `WithTheme(...)` in `UseArc`.
- **Breaking: Pixel and Corners** are theme presets: `t.Pixel()` and `t.Corners(style)`. `ArcPixelTheme`, `ArcPixelScope`, `WithArcCorners` and `ArcCornerScope` are removed; a preset skips the packages the application has not registered.
- **Breaking: languages** move to `TKCode.Arc.Localization` (`ArcLanguage`, `ArcLanguageManager`); `UseLanguages` is on `ArcBuilder`.
- **Breaking: images and popups.** `AsyncImage` is replaced by `ArcImage` (`IsLoaded` is now `IsImageLoaded`). `ArcPopup` derives from Avalonia's `Popup`; `OpenMotion` and `CloseMotion` are `ArcPopupMotion`, `AdjustPlacementAnchor` lost its `ref flipX, ref flipY` parameters, and `IsOpen` stays true until the exit motion finishes. `ArcPopupFrame` replaces `BaseMotionActor`, and `ArcOverlayHost` replaces `ScopeAwareOverlayLayerPanel`.
- **Breaking: mobile token identities** are `MobileButton`, `MobileCheckbox`, `MobileDialog`, `MobileSwitch` and `MobileToast`, so they no longer share names with the desktop controls.
- Arc no longer shares its theme with AtomUI controls; applications that mix the two stay on 0.3.x.

### Fixed

- **ToastStack**: a toast dismissed while the stack resumes its timers no longer breaks the resume.
- **DataGrid**: the search box of the filter list is drawn again (it had no template).
- **PasswordField**: long strength labels no longer overlap the strength bar (#23).
- **Gallery**: the text box of the env variables event log is drawn again (it had no template).

### Migrating from 0.3 to 0.4

1. Remove every `AtomUI.*` package reference and `using AtomUI...;` line; Arc brings in what it needs.
2. Replace `UseAtomUI(...)` with `UseArc(...)` and rename `UseArc()` (the controls) to `UseArcControls()`. The other package calls keep their names and move into the `UseArc` callback.
3. Replace theme requests with `ArcTheme.Apply`. A change names only what changes; the rest of the state stays.
4. The `App` class may drop its `partial` keyword; nothing generates code into it any more.
5. With the .NET 11 SDK and `PublishAot`, set `<UseOptimizedPublish>false</UseOptimizedPublish>` in the application project.

```csharp
// 0.3
AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .UseAtomUI(builder =>
          {
              builder.UseDesktopControls();
              builder.UseArc();
              builder.UseArcBlocks();
              builder.UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN]);
          });

// 0.4
AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .UseArc(arc => arc
              .UseArcControls()
              .UseArcBlocks()
              .UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN])
              .WithTheme(t => t.Appearance(ArcAppearance.System)));
```

```csharp
// 0.3
var builder = new ThemeConfigBuilder()
    .WithAlgorithms(ThemeAlgorithm.Default, ThemeAlgorithm.Dark)
    .WithArc(arc => arc.Accent(ArcAccentPreset.Blue).MotionMode(ArcMotionLevel.Reduced));
await manager.ApplyThemeAsync(new ThemeRequest(IThemeManager.DEFAULT_THEME_ID, builder.Build(),
                                               ThemeTransitionReason.UserRequest));

// 0.4
ArcTheme.Apply(t => t
    .Appearance(ArcAppearance.Dark)
    .Accent(ArcAccentPreset.Blue)
    .MotionLevel(ArcMotionLevel.Reduced));
```

| 0.3 (AtomUI) | 0.4 |
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

- **Windows and overlays.** Without AtomUI's window theme, a plain Avalonia `Window` has no overlay layer unless its template has a `VisualLayerManager` with `EnableOverlayLayer="True"`. `ThemeSwitcher` needs one for its transition; without it, it switches directly.
- **AtomUI controls.** An application that also uses AtomUI controls no longer gets one shared theme for both. Stay on 0.3.x until those screens are moved to Arc.

## 0.3.1-preview.6 (2026-10-28)

### Added

- **Cascader** (Extras): pick one node of a hierarchy level by level, such as province, city and district. The field, menu, gliding highlight and clear button match `Select`; a parent opens its children in a column to the right and the menu width springs to its new size. Data is read through delegates (`ChildrenSelector`, `TextSelector`, `ValueSelector`), so nothing is reflected. `SelectedItem`, `SelectedValue` and `SelectedPath` stay in step, and a value written before the data arrives is kept and resolved when it does. Options: `LeafOnly`, `ExpandTrigger`, `ShowFullPath`, `PathSeparator`, `IsClearable`, `IsReadOnly`; full keyboard support.

## 0.3.1-preview.5 (2026-10-28)

### Added

- **Audio** (new packages `TKCode.Arc.Audio`, `TKCode.Arc.Audio.Native` and `TKCode.Arc.Audio.Browser`): the `IAudioPlayer` abstraction behind the media blocks, silent by default, with desktop playback through miniaudio (`ArcAudioNative.Use()`) and WebAssembly playback through an HTML audio element (`ArcAudioBrowser.UseAsync()`).
- **Blocks**: `MediaPlayer` (cover art, playlist, seek bar, previous and next, volume with mute) and `FileUpload` (drop zone, file list, progress, validation).
- **Special**: a Cursor group with the `DotGrid`, `GravityWell` and `InkFluid` pointer effects.
- **Gallery**: the Northstar dashboard demo at `/demo`, a copy of uiarc.dev/demo with eleven pages and a sign-in page; open it from "Live demo" on the home page or "Demo" in the top menu.

### Fixed

- **ArcContentSwap**: showing a control again after it was swapped out no longer throws (switching the Webhooks inspector between Payload and Headers crashed the app).
- **Charts**: the axis tick next to the average line stays hidden, so the average label no longer overlaps it.
- **Webhooks**: Payload and Headers each open at their first line.

## 0.3.1-preview.4 (2026-10-27)

### Added

- **Terminal** (new package `TKCode.Arc.Terminal`, net10.0 and net11.0): the `TerminalView` control and a managed facade over libghostty-vt (VT parsing, screen state, selection, input encoding). The native library ships under `runtimes/<rid>/native` for Windows, macOS and Linux.

## 0.3.1-preview.3 (2026-10-27)

### Added

- **Blocks**: about 25 new blocks in Analytics, Developer, People, Finance and LineCharts (API keys, webhooks, integrations, env variables, team and roles, metric explorer, MRR waterfall, cohort retention, choropleth, market terminal and more), with Gallery pages, demos and tests.
- **RichTextEditor** and **ActionSwap** controls.
- **Mobile**: `Dialog`, `Toast`, `ActionSheet`, `Toolbar`, `PullToRefresh`, `Radio` and `IconButton`, keyboard avoidance and Arc motion.
- **Gallery**: the Desktop app checks for updates and sets the macOS Dock icon; the Browser host loads map data over HTTP.

### Changed

- **Table**, **Editors**, **Selects** and **Toggles** are reworked.

### Fixed

- **TimePicker**, **FilterMenu**: the empty area of a row ignores pointer input (#20); **Combobox**: clearing no longer opens the list.
- **Password**: the show/hide choice stays consistent with the input state (#21).
- **Gallery**: a duplicated IME commit in the sidebar search box on Windows.

## 0.3.1-preview.2 (2026-10-26)

### Added

- **Corners theme**: the new `TKCode.Arc.Themes.Corners` package (`WithArcCorners(style, scope)`): square or scaled corners for every Arc control through token overrides only; colors, borders, shadows and motion are unchanged.
- **Mobile**: `ListGroup` / `ListItem`, `Sheet`, `TextField` and `Searchbar`, a large-title `Navbar` and more `Button` variants, with Gallery pages and demos for list groups, navigation stacks and sheets.
- **Core**: more design tokens and `ArcTokens` helpers for binding corner radii.

### Changed

- **Pixel theme**: radii and theme resources are regenerated from the shared corner table.
- **Blocks**: `WeekCalendar` draws its edges with the new edge tokens.

### Fixed

- **Stepper**: the glow of the current step is no longer clipped (#19).

## 0.3.1-preview.1 (2026-10-26)

### Added

- **Mobile**: the new `TKCode.Arc.Mobile` package (`UseArcMobile()`) with the first touch-first controls: `Button` (press state, 44 x 44 touch target), `Switch`, `Page` (header, content and footer inside the safe area), `Navbar` and `Tabbar`. It depends on `TKCode.Arc.Core` only and has its own namespace. It has not been verified on Android or iOS devices yet, and it still pulls `Avalonia.Desktop` and `AtomUI.Desktop.Controls` in through `TKCode.Arc.Core`.
- **Gallery › Mobile**: a Mobile section with a phone-frame preview (iPhone 15, Pixel, small phone, tablet, portrait and landscape, status bar, simulated safe area, soft-keyboard placeholder). It is a desktop simulation, not a device.
- **Pixel theme**: the new `TKCode.Arc.Themes.Pixel` package (`UseArcPixelTheme()`): square corners, hard shadows and a limited palette in light and dark, with the Silkscreen and VT323 pixel fonts (SIL OFL) in `TKCode.Arc.Fonts` (`WithArcPixelFonts()`). The Gallery's Appearance panel has a Style switch, Arc or Pixel.
- **Tables › TreeTable**: a table with nested rows, tri-state selection, hierarchy-preserving sort, lazy-loaded children and treegrid keyboard navigation. The Gallery marks new components with a New badge for 60 days.
- **Core**: design tokens for border widths and corner radii (`EdgeThickness`, `FocusEdgeThickness`, `RadiusStep*`, `EdgeThickness*`) so a theme can change them; every default equals the value it replaces.

### Changed

- **Gallery** (web): the WebAssembly host is published without ahead-of-time compilation again, because the compiled build crashed at startup. Its icon URLs are versioned so browsers drop the old favicon.

### Fixed

- **Carousel**: a slide whose timer had already run out no longer advances the rotation again without yielding; it starts a fresh interval.

## 0.3.0-preview.5 (2026-10-24)

### Added

- **Workflow canvas**: the new `TKCode.Arc.Workflow` package (`UseArcWorkflow()`): `WorkflowCanvas` shows a graph of steps and connections and plays its runs, with pan and zoom, editing, connections, undo and redo, copy and paste, node templates, a minimap and layout of 500 steps in about 2 ms. Also `WorkflowNodeCard` and `WorkflowZoomControls`.
- **Blocks › Auth**: `SignIn`, `SignupForm`, `LoginSplit`, `LoginCentered`, `LoginImmersive`, `OtpInput`, `OnboardingIllustration` and `MultiStepForm`.
- **Core**: `PanZoomTransform`, `WheelRouter` and `LayeredGraphLayout`.

### Changed

- **Gallery** (web): the site is compiled ahead of time to WebAssembly, which smooths animations on slow CPUs; the first visit downloads more.

### Fixed

- **Memory**: many controls (`TreeView`, `ChatThread`, `Markdown`, the charts, `DataGrid`, the AI, Projects and Special blocks and more) listened to the data they show for their whole life and kept their page in memory. They now listen only while in a visual tree. `SiteFooter`, `SemanticZoom`, `AgentStatus` and the Gallery pages no longer leak either.
- **Menus**: a closed sheet no longer leaves its scrim over the window.
- **ChatThread**: the reaction picker no longer writes debug lines to the console.
- **Accordion** and **ExpandableCard**: cheaper dividers while opening, with an unchanged look.
- **Markdown** and the AI answers use StreamMark `0.1.0`.

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
