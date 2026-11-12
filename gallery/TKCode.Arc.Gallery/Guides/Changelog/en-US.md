---
title: Changelog
---

# Changelog

All notable changes to TKCode.Arc, newest first. Versions follow [Semantic Versioning](https://semver.org/). From 1.0.0 on, breaking API changes come only with a new major version; earlier `-preview` versions could change the API
between releases. The same list is kept in `CHANGELOG.md` in the repository.

NuGet packages published with each release: `TKCode.Arc`, `TKCode.Arc.Core`, `TKCode.Arc.Foundation` (from 0.3.2), `TKCode.Arc.Fonts` and
`TKCode.Arc.Icons.Lucide`, `TKCode.Arc.Blocks`, `TKCode.Arc.AdvancedCharts`, `TKCode.Arc.Special`, `TKCode.Arc.Workflow`, `TKCode.Arc.Dock` (from 0.4.0-preview.2), `TKCode.Arc.Themes.Pixel`, `TKCode.Arc.Themes.Corners`, `TKCode.Arc.Mobile`, `TKCode.Arc.Terminal`, `TKCode.Arc.Audio`, `TKCode.Arc.Audio.Native`, `TKCode.Arc.Audio.Browser`, `TKCode.Arc.Templates` (from 0.4.0-preview.6), `TKCode.Arc.Pdf`, `TKCode.Arc.Pdf.Native` and `TKCode.Arc.Pdf.Browser` (from 1.0.0), `TKCode.Arc.Video`, `TKCode.Arc.Video.Native` and `TKCode.Arc.Video.Browser` (from 1.1.0-preview.1).

## 1.1.0-preview.5 (2026-11-12)

The Docks and tabs group of `TKCode.Arc.Special` is rebuilt (`OrbitMenu`, `LiquidTabBar`, `ShareSheet`, `ActionMorph`
and `MorphNav` change their API, with migrations below; `ToolDock` is new), `AvatarUpload` moves to the Editors group,
and feedback from apps (#107–#112) adds `WindowCaptionButtons`, settings page options, content and media tokens and
automatic sizing for `WaterfallPanel` and `TilePanel`. There are breaking changes from 1.1.0-preview.4 in those five
Special controls only. Install it with `--version 1.1.0-preview.5` (or `--prerelease`).

### Added

- **`WaterfallPanel` and `TilePanel` lay out items of one template by themselves**
  ([#112](https://github.com/AIDotNet/TKCode.Arc/issues/112), from [#86](https://github.com/AIDotNet/TKCode.Arc/issues/86)):
  `WaterfallPanel.ItemSizing="Varied"` gives each child a height (a width in rows) between `MinItemSize` and `MaxItemSize`
  instead of measuring its content, so cards built from one template still form a masonry wall; an unset bound follows
  the lane size. `TilePanel.IsAutoSpan` gives children that set no span 1×1, 1×2, 2×1 or 2×2, placed at the first free
  cell and keeping larger tiles apart; with `IsAutoFill` and `Rows` set, the automatic tiles are sized to cover the
  whole `Columns` × `Rows` grid. Both panels pick sizes from the child's index and a new `Seed` property, never from
  random values during layout: the same items, seed and grid always give the same result, and resizing never reshuffles
  it. The defaults (`Content` sizing, auto span off) keep today's layouts.
- **`InlineEdit.ReserveActionSpace`** ([#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)): `False` drops the
  66 px a single line keeps after its text for the save and cancel buttons, so the box hugs its text and a tag or badge
  can follow the name. At rest nothing is drawn past the frame (no pencil hint, saving spinner or result mark); while
  editing the buttons overlay what follows and the control draws above its later siblings in the same panel. The
  reserve does not come back while editing, so neither the text nor the tag jumps when the buttons appear. A multiline
  field keeps its 38 px. The default (`True`) is unchanged.
- **Content-width and media tokens** ([#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)):
  `ContentMaxWidthSM` / `MD` / `LG` / `XL` (640 / 760 / 880 / 1120) cap a page's content column (a reading column or
  a single form, a settings page, a document page, full-width sections), and `MediaSizeSM` / `MD` / `LG` / `XL`
  (64 / 128 / 192 / 256) size square media (a thumbnail in a row, a card preview, a QR code or cover art, a large image
  preview). Use them as `{arc:ArcDesignTokenResource ContentMaxWidthMD}` and override them like any ArcDesign token.
  They are appended to `ArcDesignTokenKind`, so the existing members keep their numbers.
- **`AvatarSize.LargePlus`** ([#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)): a 64 px avatar between
  `Large` (48) and `XLarge` (88), with `TypeLG` initials and a 13 px status dot set 2 px in from the corner. It is
  appended to the enum, so the existing members keep their values; `AvatarGroup` draws it as `Large`.
- **`SettingsSection.BadgeTone`** ([#109](https://github.com/AIDotNet/TKCode.Arc/issues/109), `TKCode.Arc.Blocks`):
  colors the section's navigation badge with a `BadgeTone` (`Warning` for "Needs attention", `Danger` for a failed
  check). `Neutral`, the default, keeps today's look.
- **`SettingsRow.Details` and `SettingsRow.Leading`** ([#109](https://github.com/AIDotNet/TKCode.Arc/issues/109),
  `TKCode.Arc.Blocks`): `Details` takes any content under the description, such as a status line, an error or a row of
  tags (text is small and muted). `Leading` takes any content in place of the icon tile, such as an `Avatar`; it keeps
  its own size and `LabelWidth` includes it. `Icon` is unchanged.
- **`SettingsPage.SelectedValue`** ([#109](https://github.com/AIDotNet/TKCode.Arc/issues/109), `TKCode.Arc.Blocks`):
  the new `SettingsSection.Value` identifies a section (an enum member or a route key), and the page's two-way
  `SelectedValue` is the selected section's `Value`, so the shown section binds to a view model property. Setting
  `SelectedValue` selects the matching section. A section without a `Value` reports itself, as before;
  `SelectedValueBinding` reads the value some other way.
- **Public item containers for `RadioGroup`, `RadioCards` and `ToggleGroup`**
  ([#109](https://github.com/AIDotNet/TKCode.Arc/issues/109)): `RadioGroupItem`, `RadioCard` and `ToggleGroupItem` (and
  their base `TogglesItem`, which only the library derives from) are public. Data-bound items work like Avalonia's
  `ListBoxItem`: a style on `arc|RadioGroupItem` or the group's `ItemContainerTheme` can bind an item's `Description`,
  `Icon`, `Meta`, `IsEnabled` and `AutomationProperties.Name`, so items no longer have to be `RadioOption` or
  `ToggleGroupOption` to get them. Each container has its own token (`RadioGroupItem.MinRowHeight`,
  `RadioCard.GlyphSize`, and `ToggleGroupItem.PressScale`, `PressDownDuration` and `PressUpDuration` for the press that
  was fixed before); the defaults keep today's look, and the existing `RadioGroup`, `RadioCards` and `ToggleGroup` tokens
  are unchanged.
- **`WindowCaptionButtons`** ([#107](https://github.com/AIDotNet/TKCode.Arc/issues/107)): minimize, maximize /
  restore and close as a control of their own, for a window whose content reaches the top (an `ArcWindow` with
  `IsTitleBarVisible="False"`). Put them anywhere in your own header: they drive the window they are in, show restore
  while it is maximized, follow `CanResize`, `CanMaximize` and `CanMinimize`, carry the system roles while they drive
  it (Snap Layouts on Windows 11), and keep the localized tooltips. `ShowMinimizeButton`, `ShowMaximizeButton` and
  `ShowCloseButton` hide a button, and `IsPreview` draws them without driving the window. `TitleBar` now draws its
  caption buttons with this control and gives up their roles while `CaptionButtons` is `System` or `None`, so the
  window keeps one set.
- **`PageHeader.IsTitleBar`** ([#107](https://github.com/AIDotNet/TKCode.Arc/issues/107), `TKCode.Arc.Blocks`):
  `True` makes the header, never the body, the window's title bar: dragging its empty part, title or description
  moves the window and a double click maximizes it, while the breadcrumbs, the compact title, the actions, the meta
  row and the tabs keep their clicks. On Windows the header is the system caption and those parts are user content.
  `False` (the default) changes nothing.
- **`ArcOverlayHost` in apps** ([#107](https://github.com/AIDotNet/TKCode.Arc/issues/107), `TKCode.Arc.Core`): the
  docs no longer say an app does not need it. Put the app's content in a host and the title bar beside it, and a
  dialog, drawer or sheet opened from the content covers the host only, never the title bar and its caption buttons
  (Window caption buttons page).
- **`AvatarUpload` checks files by their first bytes and saves encoded pictures**: the type of a picked, dropped or loaded
  file is decided from its first bytes before its name (a PDF renamed to `.png` is refused, a photo with an odd name is
  not), after the size check, and a read stops at `MaxFileSize` even when the stream cannot say how long it is. New
  `Accept` narrows the types (MIME types, `image/*` or extensions) and the picker follows it; `LoadAsync(byte[], name)`
  takes a pasted image or a camera capture. `AvatarUploadRequest` gains `Bytes`, `ContentType`, `SuggestedFileName` and
  `OpenStream()`: the saved square picture encoded by the new `OutputFormat` (`Auto` keeps a PNG, and a picture of unknown type, as PNG and saves
  other pictures as JPEG; `Png`, `Jpeg`) and `JpegQuality` (92), a JPEG on white. `DisplayName` shows initials while there is no
  picture, `AllowRemove` hides the Remove button, results and progress in quarters are announced through a live region, and
  a drag past an edge of the framing area resists and springs back (the saved crop never includes the stretch). Defaults
  are unchanged. Docs add the sample-photos and failures examples.

- **`ToolDock`** (`TKCode.Arc.Special`, Docks and tabs): a floating dock of tools or modes for canvas-style editors.
  The chosen tool sits on a tint that springs over from the last one, a name and key-hint label glides over the tool under the mouse or keyboard focus
  (never for touch), a group (`ToolDockItem.Items`) grows a tray of its members out of the dock inside one hairline
  outline, badges roll their count and leave at 0, and `CanReorder` lets people move tools by drag or Alt+Left/Right
  (`Reordered`). `SelectedValue` is two-way and `ItemChosen` reports every pick; the dock is one tab stop with the
  toolbar keyboard model.

### Changed

- **`TilePanel.IsAutoFill` also fills holes from the side and from below** ([#112](https://github.com/AIDotNet/TKCode.Arc/issues/112)):
  after tiles grow right and down, every hole still empty is offered to the tiles around it, so a neighbour on the
  right or below can grow over it too. Tiles still never move, overlap or shrink, and the span limits still hold.
- **`AvatarUpload` moved from `TKCode.Arc.Special` to `TKCode.Arc`** (Editors group, between `SignaturePad` and
  `FileDropzone` in the gallery): the namespace, the class and its API are unchanged, so code compiles as before, and
  `UseArcControls()` now registers it (`UseArcSpecial()` no longer does). Its docs page moved from Special › Inputs to
  Components › Editors.
- **Special › Navigation is now "Docks and tabs"** in the gallery, listed as Share sheet, Action morph, Morph nav,
  Tool dock, Liquid tab bar and Orbit menu, and `MagnifyDock` moved there from Playful. Source folders, namespaces and
  slugs are unchanged.
- **Breaking: `OrbitMenu` is a press-drag-release menu.** Pressing the trigger opens the arc; dragging toward an action
  and letting go fires it (heading toward an action is enough once the pointer is a little way out from the button),
  and a tap keeps the arc open for hover and click. A lens glides under the target, actions close to the pointer are
  drawn to it, the button's mark turns from three dots into a cross and, once an action runs, into a tick, and a pill
  names the target, says `CancelText` when the pointer is away from every action, then shows the action's
  `DoneText` for 1.4 s, announced when `Announces` is on. New: `Direction`, `Spread`, `CancelText`, `Announces`,
  `OrbitMenuAction.DoneText` and `Tone` (`OrbitMenuActionTone.Danger`), `OrbitMenuActionEventArgs.Item`. Migration:
  `StartAngle` / `SweepAngle` are replaced by `Direction` (the middle of the arc) and `Spread` (its width; the old
  default is `Direction="-90" Spread="180"`); `Glyph`, `ClosesOnInvoke` and `OrbitMenuAction.FocusRingBrush` are
  removed (the arc always folds and confirms, handle `ActionInvoked`; keyboard focus shows as the lens); `Radius`
  defaults to 104; the trigger is 56 px and actions 46 px; actions no longer have tooltips.
- **Breaking: `LiquidTabBar` selects with a lens.** The selection is a pill that travels to the chosen tab, taking its
  width on the way without stretching, and draws what it covers in reverse colors, so outline glyphs turn solid
  (`ActiveGlyph` to draw another); the old tab's label disappears right away and the new one appears when the lens gets
  there; the lens tips toward a hovered tab and can be dragged or flicked. New: `Labels` (`Active`, `Always`, `None`), `LensBackground`, `LensForeground`, and on
  `LiquidTabBarItem` `Value`, `Label`, `Glyph`, `ActiveGlyph`, `Badge` (its digits roll when it changes) and `BadgeLabel`. Migration:
  `Viscosity` is removed; give each item a `Glyph` and a `Label` (items without a glyph are text tabs);
  `SelectedValue` reports the item's `Value`; the mouse selects on release; Up and Down no longer move the selection;
  set `LensBackground` / `LensForeground` to `Accent` / `AccentForeground` to keep the accent lens; `ItemsPanel`,
  `Padding` and `CornerRadius` are no longer used.
- **Breaking: `ShareSheet` grows from its own button into a sharing panel.** One shape springs from the button into
  a panel with the item's title, its link and Copy, an access-level list, channel buttons with pending, done and failed
  states (`ChannelHandler`), and recent people whose avatars fly into a "Send to" row; Send runs `SendHandler`, shows an
  error and leaves the panel as it was if that fails, and on success shrinks to a confirmation card with Undo. Up to 520 px wide the panel
  rises from the bottom over a dimmed page and drags down to close (`SheetOnPhones`), and `Align` picks the edge that stays put. Migration:
  the trigger is no longer `Content` (set `Label` and `Glyph`); `ShowAt`, `Presentation`, `Subtitle`, `Actions`,
  `ActionInvoked`, `Shared`, `ClosesOnShare` and the header and copy label properties are removed (use `Title`,
  `Labels`, `Channels` with `ChannelHandler`, and `SendHandler` or `Sent`); `People` holds `ShareSheetPerson`;
  `LinkCopied` passes `ShareSheetLinkCopiedEventArgs`.
- **Breaking: `ActionMorph` goes from a button to a menu, a form and a check.** The 56 px button grows into a
  short menu of things to create (rows with a description and a letter shortcut, a gliding highlight), then into the picked action's
  form (one-line or multiline field, optional choices, submit with Enter or Ctrl/⌘+Enter), and folds back into the
  button with a drawn check; the plus returns after `ResetAfter` (1.6 s). `SubmitHandler` (or `Submitted`) saves, an
  empty submit shakes, a failed one keeps the draft with an error, each action remembers its unsent text, and `Corner` picks the
  way the surface grows. Migration: describe each panel as an `ActionMorphAction` in `Actions` (`Title`,
  `Placeholder`, `IsMultiline`, `Choices`, `SubmitLabel`, `SuccessLabel`, `EmptyHint`) instead of content; `Glyph`,
  `Variant`, `CollapsesOnOutsidePress`, `ShowsCloseButton` and `ActionMorph.Closes` are removed; the control takes
  56 × 56 in the layout and draws its surface over its neighbours, so place it last in a `Panel` at a corner.
- **Breaking: `MorphNav` is a site navigation bar.** One surface holds the brand, the items with a gliding highlight,
  search and one primary action; an item with links grows the bar into its panel (links in one or two columns, an
  optional feature such as `MorphNavStory` and a footer), a neighbour's panel slides in from its side, hover opens after
  60 ms and closes after 180 ms, search opens from the bar itself (suggestions on an empty query, at most six ranked
  results), `IsCompact` tightens the bar, and when the nav is narrower than `CollapseBelow` (720 px) the items collapse
  into a single Menu item. Migration:
  `MorphNavSection` is `MorphNavItem` (`Header` → `Label`, plus a `Value`); `MorphNavLink.Text` / `Glyph` / `Tag` are
  `Label` / `Icon` / `Href`; `Title` / `Mark` are `BrandName` / `BrandMark`; `CurrentLink` is `Current` (an item's
  `Value`); `LinkInvoked` is `Navigate` (`MorphNavNavigateEventArgs`); `IsOpen`, `Open()`, `Close()` and `Toggle()` are
  `Open(value)`, `ClosePanel()` and `OpenValue`; the nav's `Footer` moves to `MorphNavItem.Footer`; `PanelWidth` and
  `CloseLabel` are removed.

### Fixed

- **Avatar status dot size** ([#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)): the dot now follows the
  avatar's size as documented (8 px with a 1.5 px ring at `Small`, 10 px at `Medium`, 12 px at `Large`, 14 px at
  `XLarge`), and the Busy bar spans the dot at every size. Before, every avatar drew the 10 px medium dot: the old
  template's local values hid the size styles.
- **`InlineEdit` multiline reserve** ([#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)): a multiline field
  now keeps 38 px for its stacked save and cancel buttons, as documented, instead of 66 px, so its text wraps 28 px
  later. The template's own padding had hidden the multiline style.
- **`OrbitMenu` first opening**: the first time the menu opened, the actions showed at their places on the arc for a
  moment, dropped back into the trigger and fanned out again; they now leave the trigger once.

## 1.1.0-preview.4 (2026-11-11)

Feedback from apps moving to Arc (#102–#110): new options on `PageHeader`, `Dialog`, `CommandPalette`, `Avatar`,
`EmptyState`, `InlineEdit`, `CodeBlock` and `Tooltip`, and fixes to `Table`, `ToggleGroup`, `SettingsGroup`, Mobile
`ListGroup`, modal overlays, `Gauge` and `ArcWindow`. There are no breaking changes from 1.1.0-preview.3. Install it
with `--version 1.1.0-preview.4` (or `--prerelease`).

### Added

- **`PageHeader` for settings pages and side panes** ([#108](https://github.com/AIDotNet/TKCode.Arc/issues/108),
  `TKCode.Arc.Blocks`): `IsHeaderOnly="True"` shows only the header, with no tabs, rule or scrolling body, as a title
  and action bar on top of a page; `Size="Small"` is a compact header (a `TypeXL` title and tighter space around it);
  `PageHeaderAction.IsVisible="False"` hides an action's button and menu row instead of dimming them.
- **`Dialog.ShowHeader`** ([#108](https://github.com/AIDotNet/TKCode.Arc/issues/108)): `False` hides the title row
  (title, description, close button and rule) for a dialog that brings its own heading. `Title` stays the accessible
  name.
- **`CommandPalette` with results from the app** ([#108](https://github.com/AIDotNet/TKCode.Arc/issues/108),
  `TKCode.Arc.Blocks`): `IsFilteringEnabled="False"` on the palette or the dialog lists `ItemsSource` as given, for
  apps that filter and rank results themselves. `CommandPaletteDialog` now has `Query` (two-way) and `ActiveItem`,
  readable before the dialog first opens. A query set from code leaves the caret at its end.
- **More avatar presence** ([#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)): `AvatarStatus` gains `Busy`
  (danger color with a bar across the dot), `Away` (warning color) and `Connecting` (a hollow ring that breathes in
  opacity and rests with motion off). Each status is part of the avatar's accessible name.
- **`EmptyState.Size`** ([#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)): `Small` is a compact empty state
  for list panes and cards (a 32 px icon block, 16 px above and below, tighter gaps) at every window width.
- **`InlineEdit.Labels`** ([#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)): an `InlineEditLabels` object
  localizes everything a screen reader hears: the "Saving …" and "… saved" announcements, the save and cancel button
  names and the help text. The English defaults are unchanged.
- **Diff coloring in code** ([#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)): `CodeBlock`, Markdown fences
  and the rich text editor's code blocks with the `diff` or `patch` language color added lines in the success color,
  removed lines in the danger color and the file and hunk headers in the secondary text color. Before, those lines
  borrowed the number and string colors. `text` and `plaintext` are documented as plain text.
- **`Tooltip.ShowOnDisabled`** ([#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)): shows a tooltip over a
  disabled control, for example to say why a button cannot be used. A disabled control gets no pointer input, so the
  tooltip follows the pointer over its bounds. The Tooltip docs said this already worked; it did not.

### Fixed

- **`Gauge` on a small ring**: the number now scales with the ring (`Type3XL` at the largest size), so it stays
  inside a small dial, and its ink, not its line box, sits on the ring's center, so it no longer rides high.
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

## 1.1.0-preview.3 (2026-11-10)

A performance release: apps can cap how often continuous loops repaint. There are no breaking changes from
1.1.0-preview.2. Install it with `--version 1.1.0-preview.3` (or `--prerelease`).

### Added

- **`ArcMotion.MaxFrameRate`** (`TKCode.Arc.Core`): an inherited attached property that caps how many times a second
  every `LiveLoop` under an element steps (mascots, skeleton pulses, shimmers, live dots). Each step gets the time since
  the last one, so the motion keeps its speed, and skipped frames draw nothing. The default, 0, steps on every frame, as
  before. Set it on a small always-on window, e.g. `arc:ArcMotion.MaxFrameRate="24"`, to stop slow decorative motion
  from repainting the window at the display's full rate.

## 1.1.0-preview.2 (2026-11-10)

A fix for NativeAOT: apps that use `Sidebar` publish without trim warnings. There are no breaking changes from 1.1.0-preview.1. Install it with `--version 1.1.0-preview.2` (or `--prerelease`).

### Fixed

- **`Sidebar` under NativeAOT** ([#100](https://github.com/AIDotNet/TKCode.Arc/issues/100)): `TKCode.Arc.Blocks` no longer produces trim warnings (IL2026 / IL3053) when an app uses `Sidebar`. The new `LabelMemberBinding`, `KeyMemberBinding`, `IconMemberBinding`, `BadgeMemberBinding`, `IsEnabledMemberBinding` and `ChildrenMemberBinding` map `ItemsSource` data with compiled bindings, e.g. `LabelMemberBinding="{Binding Title, DataType={x:Type local:MenuNode}}"`. The string `*Member` paths keep working but use reflection; trimmed and NativeAOT apps should move to the `*MemberBinding` properties.

## 1.1.0-preview.1 (2026-11-10)

The first preview of 1.1.0: new controls in `TKCode.Arc`, new groups in `TKCode.Arc.Special`, 58 new blocks, the video packages on NuGet, and Avalonia updates within version 12 without a new Arc release. There are no breaking changes from 1.0.0. Install it with `--version 1.1.0-preview.1` (or `--prerelease`); the API added in this version may still change before 1.1.0.

### Added

- **Video on NuGet**: `TKCode.Arc.Video`, `TKCode.Arc.Video.Native` and `TKCode.Arc.Video.Browser` are published. `VideoPlayer` and `VideoSurface` play through Media Foundation, AVFoundation or GStreamer on desktop and the browser's video element in WebAssembly (`UseArcVideo()`, `ArcVideoNative.Use()`, `ArcVideoBrowser.UseAsync()`).
- **`TreeSelect`** ([#80](https://github.com/AIDotNet/TKCode.Arc/issues/80)): a select field over a tree that picks one node or checks several, with three-state checkboxes, a `CheckStrategy` for what is reported, a search that keeps the path to every match, chips, and virtualized rows for thousands of nodes.
- **`Sticky`** ([#84](https://github.com/AIDotNet/TKCode.Arc/issues/84)): attached properties that keep any element at the top of a scrolling page once it scrolls there, like CSS `position: sticky`, with an offset, an `IsStuck` state and a `StuckChanged` event.
- **`BackToTop`** ([#67](https://github.com/AIDotNet/TKCode.Arc/issues/67)): a round button that appears once a list or feed has scrolled past a threshold and smoothly scrolls it back, placed yourself or attached to a corner with `arc:BackToTop.IsAttached`.
- **More controls**: `Rating` (half stars, clearing, read-only, custom shapes), `ToggleGroup` and `SlotText`, `RangeHistogramSlider` and `RulerSlider`, `OnboardingChecklist`, and `SplitBar` (Charts), `MetricTicker` (Cards) and `PresenceStack` (Avatars).
- **Special components** (`TKCode.Arc.Special`, designed independently from their names alone) in new groups: Glass (`GlassCard`, `GlassMenu`, `GlassSegmented`, `GlassTabBar`), Navigation (`MorphNav`, `LiquidTabBar`, `OrbitMenu`, `ActionMorph`, `ShareSheet`), Photos (`ProductGallery`, `LightboxGallery`, `PhotoGrid`, `CoverFlow`), Data (`BookingPill`, `ActivityRings`, `TimeDial`, `DateReel`), Widgets (`KeycapKeyboard`), Inputs (`EmojiPicker`, `AvatarUpload`, `ConfirmPopover`, `UndoToast`), Publishing (`MegaMenuMorph`, `TocProgress`, `ShareAccess`), the explainers `DnsResolution`, `ModelTraining` and `BranchGraph`, and `SandText`, `TypewriterTerminal`, `PrizeWheel`, `MagnifyDock` and `ImageTrail`.
- **Blocks** (`TKCode.Arc.Blocks`): marketing sections (`HeroSection`, `FaqSection`, `ContactSection`, `StatsBand`, `BlogGrid`, `ComparisonTable`, `ChangelogFeed`, `LogoMarquee`, `HeroSignup`, `TeamShowcase`, `StudioPerspectives`, `ScrollStory`, `SpotlightGrid`, `FeatureBento`); AI (`AgentRun`, `ResearchRun`, `AiInlineEdit`, `AgentTasks`, `AgentApprovals`, `AgentRoutines`, `AgentOrchestrator`, `AgentBuilder`, `AgentGallery`, `McpServers`, `KnowledgeBase`, `EvalSuite`, `AiInsightCard`, `AiFeatureLaunch`, `AiSearchBar`, `AiSetupWizard`, `CatchUp`, `SmartScheduler`); support (`SupportConversation`, `SupportWidget`, `InboxTriage`, `FeedbackBoard`, `HelpCenter`, `FeatureVoting`, `ActivityFeed`); commerce and finance (`WalletCard`, `SpendAlert`, `InvoiceStudio`, `DeliveryTracker`, `ProductListing`, `ProductDetail`, `SeatMap`); analytics and projects (`CallInsights`, `LeadScoring`, `AccountHealth`, `GoalTracker`, `PivotBuilder`, `TaskBreakdown`); app shell and auth (`DocsSidebar`, `InboxSidebar`, `SelectionToolbar`, `TwoFactorSetup`, `OnboardingSurvey`, `MagicLinkSignIn`).
- **`Sidebar.ExpandedWidth`** (Blocks, [#92](https://github.com/AIDotNet/TKCode.Arc/issues/92)): the width of the open sidebar, which the collapse springs from and back to.

### Changed

- `GhostMascot`, `CubeMascot`, `AiMascot`, `LiquidTabBar`, `AiSpark` and `RadarChart` allocate far less per frame and look the same.

### Fixed

- The packages no longer pin Avalonia to one exact version ([#97](https://github.com/AIDotNet/TKCode.Arc/issues/97)): they accept Avalonia 12.1.5 or any later 12.x release, so an app can update Avalonia within version 12 without waiting for an Arc release.
- Loading indicators no longer freeze under reduced motion ([#93](https://github.com/AIDotNet/TKCode.Arc/issues/93)): spinners and running lines hold still and breathe in opacity instead.
- `Dialog`, `Drawer`, `BottomSheet` and `Tooltip` no longer show dark square corners below their rounded ones ([#75](https://github.com/AIDotNet/TKCode.Arc/issues/75)).
- `Sidebar` (Blocks) collapses again when resized, keeps centred header content centred, and its built-in text follows the Arc language ([#92](https://github.com/AIDotNet/TKCode.Arc/issues/92)). Set `ExpandedWidth` instead of `Width`.
- `MegaMenuMorph` no longer opens and closes over and over while the pointer rests on a trigger.
- `TextFilterBox` and `NumberFilterBox` no longer overlap or cut off their fields: a long label wraps, and in a narrow space the label moves above the fields.
- `Table` no longer throws "Invalid Arrange rectangle" in a compact phone layout narrower than the row's insets, and `ChatThread` bubbles follow the theme's corner style.
- The Windows gallery app starts again on Windows 10 builds before 21H2 (#76).

## 1.0.0 (2026-11-08)

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
- **`EnumSelector`** (`TKCode.Arc`; issue #56): lists an enum's members as a `RadioGroup`, or as `Checkbox`es for a `[Flags]` enum, with `SelectedEnum` binding two-way. Member data comes from a new Foundation generator for enums marked `[EnumMetadata]`, so nothing is reflected.
- **`FluidTabControl`, `FluidTabItem`** (`TKCode.Arc`; issue #60): scroll-linked tabs. Picking a tab scrolls smoothly to its section, and scrolling selects the tab of the section on the anchor line.
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

### Changed

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

## 0.4.0-preview.6 (2026-11-08)

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

## 0.4.0-preview.5 (2026-11-08)

Fourteen new Special components: Surfaces, Audio and video, and Loaders; Gallery pages open faster.

### Added

- **Special components › Surfaces** (`TKCode.Arc.Special`): `SheetStack` (drill-down panels that rise over one another with depth, drag down to close and become stacked dialogs on wide screens), `WalletStack` (payment cards in brushed metal, guilloche black, frosted glass and color that fan, cycle with a flick and lift out into a balance and recent activity), `ControlCenter` (quick settings tiles that morph into their details, a focus-session dial and rubber-band volume and text-size meters), `HoloCard` (a card that tilts in perspective toward the pointer under a holographic foil, glitter and glare and turns over), `CardToArticle` (story cards that morph into a full article and shrink back when pulled down) and `LinkUnfurl` (a composer that turns a pasted address into a link, shows it loading and unrolls it into a preview card).
- **Special components › Audio and video** (`TKCode.Arc.Special`): `VoiceOrb` (a voice-mode sphere that breathes, ripples, swirls and pulses, with `VoiceTranscript` live captions and `VoiceControls` call controls), `NowPlaying` (a mini player bar that morphs into a full player with a waveform scrubber, plus `NowPlayingLevels`) and `VoiceRecorder` (record into a live waveform, review and scrub, then send the take as a voice bubble). Audio is simulated and wired through properties and events, so the package takes no audio dependency.
- **Special components › Loaders** (`TKCode.Arc.Special`): `StretchRefresh` (pull-to-refresh by mouse, touch or wheel), `MorphLoader` (four strokes that morph between dots, bars, a ring and a square and fold into a check or a cross), `SkeletonMorph` with `MorphBlock` (skeletons that stretch into the loaded content block by block), `BlackHole` (a delete transition that spirals items into a singularity and spins them back out on undo) and `ChargeSession` (a live charging card with a draggable charge limit and a tapering charge curve).
- `TokenTextEditor.TryGetTokenBounds` returns a token chip's bounds in another visual's coordinates.

### Fixed

- Gallery: a docs page builds its demos when it first comes into view and screenshots are decoded when seen, so pages with many demos open in about half the time; the sidebar and top bar do less work while navigating.
- Gallery in the browser: a logo asked for twice at once no longer shows as initials, and docs text and screenshots are fetched once per session.

## 0.4.0-preview.4 (2026-11-07)

Lucide icons are compiled into each app on demand: an app ships only the icons it uses.

### Changed

- **Lucide icons per app** (`TKCode.Arc.Icons.Lucide`): the package no longer carries the path data of all 2155 icons; its assembly drops from 655 KB to 67 KB (gzip: 130 KB to 31 KB), and an app adds about 100 to 300 bytes per icon it uses. A source generator shipped in the package finds the icons in C# (`LucideIconKind.Bell`) and XAML (`Kind="Bell"`, `{lucide:LucideGeometry Bell}`, `{x:Static lucide:LucideIconKind.Bell}`, style setters) and compiles only those into the project. Nothing changes for icons written like this.
- **Icons chosen at run time must be declared** (breaking): an icon that comes from a binding, from data or from XAML loaded at run time is declared in the project file with `<LucideIcon Include="Bell;arrow-right" />` (PascalCase or Lucide's own names), or every icon is compiled in with `<LucideIconsIncludeAll>true</LucideIconsIncludeAll>`. An icon that is not compiled in draws nothing and logs a warning naming the line to add; `LucideIconData.Get` returns an empty string for it. New: `LucideIconData.IsRegistered` and `TryGet`.

### Added

- **Video** (new packages `TKCode.Arc.Video`, `TKCode.Arc.Video.Native`, `TKCode.Arc.Video.Browser`, in the repository and not yet on NuGet; `UseArcVideo()`): `VideoPlayer` plays a video with Arc controls over the picture (seek bar with buffered range, chapters and hover time, volume, speed, captions menu, full screen, auto-hiding bar, keyboard shortcuts, screen-reader value texts) and draws WebVTT / SRT captions itself; `VideoSurface` shows the picture alone. Frames are drawn inside the composition tree, so controls, rounded corners and transforms work over the video; on macOS and Windows they arrive as GPU images without a copy. Decoding uses the system player (Media Foundation, AVFoundation, GStreamer; `ArcVideoNative.Use()`) or an HTML video element in the browser (`ArcVideoBrowser.UseAsync()`); no codecs ship with the app. Without a backend the player shows a test picture. Built-in text in en-US, zh-CN and zh-TW. New gallery group "Video".
- **Build warnings for Lucide icons**: `ARCL002` marks each place where XAML binds `LucideIcon.Kind` (attribute, property element or style setter) while the project declares no icons, since the generator cannot see which icons a binding shows; `ARCL001` reports a `<LucideIcon Include>` name that is not a Lucide icon. Declaring icons or setting `LucideIconsIncludeAll` ends `ARCL002`; `<NoWarn>ARCL002</NoWarn>` silences it.

## 0.4.0-preview.3 (2026-11-06)

GanttTimeline for planning projects, `Sidebar` rows built from your own data, and fixes for the markup extensions and the ArcWindow demos.

### Added

- **GanttTimeline** (Blocks › Projects): a project timeline of phases and tasks on a calendar. Drag a bar to move it or an end to resize it (snapping to whole days, with undo and reset), dependency arrows turn red when a task starts before one it depends on has ended, phases fold into one summary bar, `Zoom` switches between day, week and month, and a today line marks where the plan stands. Edits are reported through `TasksChanged`. Keyboard: arrows move between bars, Alt+arrows move a task, Alt+Shift+arrows change its end. Bars spring to their snapped day; reduced motion jumps instead.
- **Sidebar `ItemsSource`**: builds the rows from your own data (a permission list, a menu the server returns) and follows it when it changes. `LabelMember`, `KeyMember`, `IconMember`, `BadgeMember`, `IsEnabledMember` and `ChildrenMember` map the fields, `TrailingTemplate` draws extra content at the end of a row with your object as `DataContext`, and your object is kept in `SidebarItem.Tag`. `SidebarItem` subclasses in `ItemsSource` are used as they are.

### Fixed

- **Markup extensions**: the generated token and text extensions (`ArcDesignTokenResource` and the localized text extensions) return `object` from `ProvideValue`, so they resolve in every property type.
- **Gallery**: the ArcWindow demos clip their title bar preview inside the rounded border instead of on the border itself.

## 0.4.0-preview.2 (2026-11-06)

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

## 0.4.0-preview.1 (2026-11-03)

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

## 0.3.2-preview.2 (2026-10-30)

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

## 0.3.2-preview.1 (2026-10-28)

Arc no longer depends on AtomUI. Every package now depends only on Avalonia and on the new MIT package `TKCode.Arc.Foundation`, so the whole dependency chain is under permissive licenses and closed-source applications can publish Arc as a single file or with NativeAOT (issue #25). The application entry point and the theme API change; the controls, their properties and the XAML (`{arc:ArcDesignTokenResource ...}`, `{arc:XxxTokenResource ...}`) do not. See *Migrating from 0.3.1 to 0.3.2* below.

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
2. Replace `UseAtomUI(...)` with `UseArc(...)` and rename `UseArc()` (the controls) to `UseArcControls()`. The other package calls keep their names and move into the `UseArc` callback.
3. Replace theme requests with `ArcTheme.Apply`. A change names only what changes; the rest of the state stays.
4. The `App` class may drop its `partial` keyword; nothing generates code into it any more.
5. With the .NET 11 SDK and `PublishAot`, set `<UseOptimizedPublish>false</UseOptimizedPublish>` in the application project.

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

- **Windows and overlays.** Without AtomUI's window theme, a plain Avalonia `Window` has no overlay layer unless its template has a `VisualLayerManager` with `EnableOverlayLayer="True"`. `ThemeSwitcher` needs one for its transition; without it, it switches directly.
- **AtomUI controls.** An application that also uses AtomUI controls no longer gets one shared theme for both. Stay on 0.3.1-preview.6 until those screens are moved to Arc.

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
