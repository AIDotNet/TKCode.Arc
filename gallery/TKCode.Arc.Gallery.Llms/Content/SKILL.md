---
name: {{skill-name}}
description: Builds and reviews Avalonia interfaces with TKCode.Arc, the Arc design language (uiarc.dev) as native Avalonia controls on AtomUI. Picks the right Arc component or block, installs and registers the packages, wires only documented properties, and applies Arc's rules for design tokens, motion, copy, accessibility and layout. Use when a project references a TKCode.Arc package, uses the XAML namespace https://github.com/AtomUICommunity/TKCode.Arc or calls UseArc(), or when asked to build or polish an Avalonia screen such as a settings page, form, dashboard, data table, dialog or command palette with Arc.
---

# TKCode.Arc

TKCode.Arc brings Arc to Avalonia: {{arc-count}} components in Arc's 22 groups, {{block-count}} page-level blocks, a data grid
and a streaming Markdown renderer. Every control derives from an Avalonia base type (`Input` is a `TextBox`, `Button`
is a `Button`), so binding, commands, styles and data validation work as usual, and every control reads one set of
design tokens. The quality bar is Arc's: a calm, neutral interface where type, spacing and one considered interaction
carry the character.

This skill is for TKCode.Arc {{version}} on Avalonia {{avalonia}} and AtomUI {{atomui}}. The documentation index is
{{base}}/llms.txt.

## Workflow

Copy this checklist and tick it off:

```
Arc task:
- [ ] 1. Find: an Arc component or block for each region of the screen (components.md)
- [ ] 2. Read: the page of every component you use, including "When not to use"
- [ ] 3. Install: packages added, UseArc() registered, Application class partial
- [ ] 4. Wire: documented properties and events only, bound to a view model
- [ ] 5. Style: tokens for every color, radius, size and font; Arc's motion, not your own
- [ ] 6. Review: run checklist.md, fix, and run it again until every line passes
```

**Find.** Start from [components.md](components.md) and pick by purpose; prefer a block when one covers the whole
region (a sidebar, a settings page, a command palette). Compose screens from Arc controls and Avalonia layout panels
(`Grid`, `StackPanel`, `WrapPanel`, `DockPanel`); do not rebuild a control Arc already has.

**Read.** Fetch each component's page: `{{base}}/components/<slug>.md` (blocks: `{{base}}/blocks/<slug>.md`). The
page says when to use the component and when not to, with the better choice for each nearby case; shows usage in XAML
and C# and the source of every live example; and ends with the API reference, the complete list of properties (type
and default), events and pseudo-classes. A name that is not in the API reference does not exist.

**Install.**

```bash
dotnet add package TKCode.Arc --version {{version}}
dotnet add package TKCode.Arc.Fonts --version {{version}}   # optional: Geist and Geist Mono
```

```csharp
AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .WithInterFont()    // Avalonia.Fonts.Inter
          .WithArcFonts()     // TKCode.Arc.Fonts
          .UseAtomUI(builder =>
          {
              builder.UseDesktopControls();
              builder.UseArc();
          });

public partial class App : Application { }   // partial: AtomUI's generator adds to it (ATOMUILOC008 otherwise)
```

XAML uses `xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"` for controls, token resources and markup
extensions. Blocks (`TKCode.Arc.Blocks`, `UseArcBlocks()`) and the DataGrid (`TKCode.Arc.AdvancedCharts`,
`UseArcAdvancedCharts()`) are not on NuGet yet: reference the projects from {{repository}}. Full steps:
{{base}}/docs/installation.md.

**Wire.** Bind two-way properties (`Text`, `IsChecked`, `SelectedItem`, `Value`) to settable view model properties and
only read the read-only ones. Fields are named by `Label`, explained by `Description` when there is a constraint, and
show errors through `ErrorMessage` or the bound property's data validation. Set values that come from background work
on the UI thread (`Dispatcher.UIThread`).

**Style.** Every color, radius, spacing, shadow and font size comes from a token: `{arc:ArcDesignTokenResource Name}`
in XAML, `ArcTokens.Bind(control, property, ArcDesignTokenKind.Name)` in code. The common ones:

| Need | Tokens |
|---|---|
| Surfaces | `Background` (page), `Surface` (cards, fields), `SurfaceMuted` (wells), `SurfaceRaised` (menus, popups) |
| Text | `Foreground` (titles, primary), `TextSecondary` (descriptions), `TextMuted` (hints only) |
| Lines | `Border`, `BorderStrong`, `BorderSubtle` |
| Accent | `Accent`, `AccentStrong`, `AccentSubtle`, `AccentForeground` (text on the accent) |
| Status | `Success`, `Warning`, `Danger` and their `…Subtle` tints |
| Shape | `RadiusControl` (fields, buttons), `RadiusPanel` (cards), `RadiusSurface` (large surfaces), `RadiusPill` |
| Spacing | `Space1` (4) to `Space24` (96), on a 4 px scale |
| Type | `TypeXS` (12), `TypeSM` (14), `TypeBase` (16), `TypeLG` (18) up to `Type5XL` (72) |
| Elevation | `ShadowResting`, `ShadowRaised`, `ShadowFloating` |

Every token, with themes and accents: {{base}}/docs/theming.md. Arc controls animate themselves; a control of your own
declares transitions under `^:motion-full` and `^:motion-reduced` with `arc:ArcMotion.Tracks="True"` and uses
`MotionValue<T>` only for what a theme cannot express: {{base}}/docs/motion.md.

## Core principles

1. **One purpose per screen.** One `Primary` button per surface; the accent marks the one action or state that matters
   and everything else stays neutral.
2. **Native first.** Use the Avalonia way (binding, commands, styles, data validation, the keyboard) through Arc's
   controls; the controls already follow it.
3. **Tokens, not values.** A screen built from tokens follows light and dark, the eight accents and token overrides
   with no extra work.
4. **Words that say what happens.** Sentence case everywhere; a button is a verb and an object ("Save changes",
   "Create invoice"); an error says how to fix the value.
5. **Motion explains a change.** Move what moved, fade what appeared; the end state reads the same under the Reduced
   and Off motion levels.
6. **Accessible by default.** Every field has a label, every icon-only button an accessible name, every state a word or
   an icon and not only a color.

## Never do

- WPF, WinUI or web APIs: `Visibility`, `DataTemplateSelector`, `x:Array`, `Trigger`, CSS. This is Avalonia.
- An Arc control without the `arc:` prefix (it silently becomes Avalonia's own `Button`, `Slider` or `TreeView`).
- A property, event or enum value that the API reference does not list.
- A hex color, a fixed corner radius or a font size in a view, or `#FFFFFF` for text on the accent (`AccentForeground`).
- `TextMuted` for information people must read.
- Hand-written animations, or `DispatcherTimer` loops, for a change an Arc control already animates.
- Status shown by color alone, or bounce on money, progress and errors.
- Title Case labels, "OK" or "Submit" buttons, exclamation marks in confirmations.
- Floating package versions (`0.*`), a second Avalonia version, or `InvariantGlobalization` (the inputs, pickers and
  charts format numbers and dates with culture data).

## Quick decisions

| The screen needs | Use |
|---|---|
| A single action | `Button`; `Variant="Primary"` once per surface, `Secondary` or `Ghost` beside it |
| An action whose result shows on the button (Saved, Sent) | `ActionButton` |
| One action with a few close alternatives | `SplitButton` |
| Related actions on one object | `ButtonGroup` |
| A destructive action that needs a deliberate press | `HoldToConfirm`, or `ConfirmMorph` for inline confirmation with undo |
| Single-line text with a label, hint and error | `Input`; long text `Textarea`; secrets `PasswordField` |
| A number, an amount of money, a phone number | `NumberField`, `MoneyInput`, `PhoneInput` |
| One choice from a list | `Select`; searchable `Combobox`; several `MultiSelect` |
| A setting that applies at once | `Switch` |
| One of a few views or periods | `SegmentedControl` |
| One of several options that need explaining | `RadioCards`, or `RadioGroup` for short options |
| A date, a range, a time | `DatePicker`, `DateRangePicker`, `TimePicker` |
| A decision that blocks the page | `Dialog`; focused side work `Drawer`; on phones `BottomSheet` |
| Contextual details next to a control | `Popover`; a preview on hover `HoverCard`; a short hint `Tooltip` |
| A short confirmation of finished work | `Toast`; several at once or app-wide `ToastStack` |
| A message that stays until someone acts | `Alert` |
| A number with context | `MetricCard`; a trend `Sparkline` or `LineChart`; part of a whole `DonutChart` |
| Records to compare and sort | `SortableDataTable`; spreadsheet editing `DataGrid`; nested items `TreeView` |
| Filters over a collection | `FilterToolbar`, or `ChipGroup` for a few facets |
| Content still loading | `Skeleton`; a known amount of work `Progress` |
| Nothing to show yet | `EmptyState` |
| Steps of a flow | `Stepper` |
| App navigation | `Sidebar` block; within a page `Tabs`; hierarchy `Breadcrumb` |
| A settings screen | `SettingsPage` block; find any setting `SettingsCommand` |
| Search and run any action from the keyboard | `CommandPalette` block |
| Rendered Markdown or streamed AI replies | `Markdown`; source code `CodeBlock` |

## Reference files

| File | Read it when |
|---|---|
| [components.md](components.md) | Choosing components: every component and block by group, with its class and purpose |
| [checklist.md](checklist.md) | Before you finish: the review checklist |
| {{base}}/docs/installation.md | Setting up a project, publishing, troubleshooting |
| {{base}}/docs/theming.md | Light and dark, accents, every design token, overriding tokens |
| {{base}}/docs/motion.md | Motion levels, springs, motion in your own controls |
| {{base}}/llms-full.txt | You need every page at once |

When these files are not next to this one, fetch them from {{skill}}/.

## Before you finish

Run [checklist.md](checklist.md) against the code, fix what fails and run it again. Then build: a XAML property that
does not exist fails at build time. Report the Arc properties you used and the page that documents each, so they are
quick to check.
