---
title: Introduction
---

# Introduction

TKCode.Arc brings [Arc](https://uiarc.dev) to Avalonia: the same component groups, the same visual language and the
same spring motion, built as native Avalonia controls on top of [AtomUI](https://github.com/AtomUI/AtomUI). It ports
the free Arc components (110 components in 22 groups) and adds page-level blocks, a spreadsheet-style data grid and a
streaming Markdown renderer, so a desktop product can be assembled from one consistent kit instead of a patchwork of
styles.

This gallery is the library's documentation. Every component page is written from the component's source, shows live
examples you can interact with, and lists the generated API reference.

![A billing overview built only from Arc components: metric cards, a status badge, a form field and actions](images/ProductOverview.png)

```xml
<StackPanel Width="760" Spacing="20">
    <Grid ColumnDefinitions="*,Auto">
        <StackPanel Spacing="4">
            <TextBlock Text="Acme Analytics"
                       FontSize="22"
                       FontWeight="Medium"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Workspace overview, October 2026"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="8" VerticalAlignment="Center">
            <arc:Badge Content="Pro plan" Tone="Success" />
            <arc:Button Content="Export report" Variant="Secondary" />
            <arc:Button Content="Invite members" />
        </StackPanel>
    </Grid>
    <UniformGrid Columns="3">
        <arc:MetricCard Label="Monthly revenue" Prefix="$" Value="48210" Change="+6.4%" Context="vs last month" Margin="0,0,8,0" />
        <arc:MetricCard Label="Active accounts" Value="1284" Change="+42" Context="Across 3 regions" Margin="4,0" />
        <arc:MetricCard Label="Churn" Suffix="%" Value="1.8" Decimals="1" Change="-0.3 pts" Context="Lower is better" Margin="8,0,0,0" />
    </UniformGrid>
    <arc:Input Label="Billing email"
               Text="billing@acme.com"
               Description="Invoices and payment receipts are sent here." />
</StackPanel>
```

## What is in the library

| Part | What it gives you |
|---|---|
| Components | 110 controls in Arc's 22 groups: buttons, gestures, menus, text fields, special inputs, selects, toggles, sliders, pickers, editors, navigation, expand, overlays, messages, progress, avatars, cards, charts, tables, activity, media and text effects. |
| Blocks | Page-level compositions for application shells and settings: Sidebar, PageHeader, CommandPalette, SearchResults, NotificationCenter, EmptyStates, SettingsPage, SettingsCommand, SecuritySettings, BillingOverview, UsageBilling and CancelFlow. |
| AdvancedCharts | `DataGrid`, a spreadsheet-style grid with sorting, filtering, editing, selection statistics, undo and export. |
| Illustrations | Animated explainer scenes (DataFlow, PaymentFlow, MultiRegionFailover, VectorSearch) for landing and onboarding pages. |
| Content | A streaming `Markdown` control and a highlighted `CodeBlock`, for documentation and AI chat replies. |
| Icons and fonts | 187 Lucide glyphs built into the core, the full Lucide set (2155 icons) as a separate package, and Geist and Geist Mono. |

### Component groups

| Group | Count | Typical use |
|---|---|---|
| Buttons | 10 | Primary and secondary actions, split and grouped actions, copy, confirm, theme switching |
| Gestures | 3 | Hold to confirm, emoji reactions, swipe actions on list rows |
| Menus | 3 | Context menus, dropdown menus, the account menu |
| Text fields | 7 | Single-line and multi-line input, passwords, search, in-place editing |
| Special inputs | 7 | Numbers, money, phone numbers, tags, mentions, scrubbing, keyboard shortcuts |
| Selects | 5 | Single and multiple choice from lists, searchable choice, chips |
| Toggles | 6 | Switches, checkboxes, radio groups and cards, segmented controls, billing period toggles |
| Sliders | 2 | Ranges and values, with an elastic variant |
| Pickers | 5 | Calendar, dates, date ranges, times, colors |
| Editors | 3 | Rich text, file drop zones, signatures |
| Navigation | 3 | Tabs, breadcrumbs, pagination |
| Expand | 5 | Accordions, expandable cards and text, resizable panels, scroll areas |
| Overlays | 6 | Dialogs, drawers, bottom sheets, popovers, hover cards, tooltips |
| Messages | 5 | Alerts, toasts and toast stacks, announcement bars, cookie consent |
| Progress | 5 | Progress bars, skeletons, steppers, countdowns, usage meters |
| Avatars | 3 | Avatars, avatar groups, badges |
| Cards | 3 | Content cards, metric cards, empty states |
| Charts | 13 | Line, bar, donut, gauge, sparkline, treemap, heatmap and more |
| Tables | 6 | Sortable tables, filter toolbars, tree views, reorderable lists, JSON and code viewers |
| Activity | 3 | Chat threads, comment threads, timelines |
| Media | 3 | Carousels, card stacks, image comparison |
| Text effects | 4 | Text reveal, morph, shimmer, in-view titles |

## Design principles

**Arc fidelity.** Sizes, radii, colors, easing curves and spring constants come from Arc's own tokens, so a screen
built with TKCode.Arc reads like Arc on the web. Where Avalonia differs from the browser (focus, keyboard, overlays,
accessibility), the behavior is mapped to the native desktop equivalent rather than imitated.

**Native Avalonia controls.** Every control derives from an Avalonia base type: `Input` is a `TextBox`, `Button` is a
`Button`, `Select` and `MultiSelect` are `SelectingItemsControl`s. Data binding, commands, styles, data validation and
the keyboard work the way an Avalonia developer expects.

**Tokens, not hard-coded values.** Colors, radii, shadows, type and motion come from the ArcDesign tokens. Switching
between light and dark, or choosing one of eight accents, restyles every control at once. See **Theming**.

**Motion with restraint.** Springs and timings follow Arc's motion system, and every animation has three levels
(Full, Reduced and Off) that you can set per window or per container. See **Motion**.

**Accessible by default.** Fields are named by their labels, errors are announced through live regions, every
interactive control has a keyboard model, and each page documents what assistive technology reads.

**Ready for AOT.** The library uses no reflection and is trimming- and NativeAOT-compatible, so desktop apps can ship
as small, fast-starting single files.

## Packages

| Package | Contents | Where |
|---|---|---|
| `TKCode.Arc` | The components (`UseArc()`), the streaming `Markdown` control and `CodeBlock`. Brings in `TKCode.Arc.Core`. | NuGet |
| `TKCode.Arc.Core` | ArcDesign tokens and palette, the spring motion engine, shared primitives, 187 built-in Lucide glyphs. | NuGet |
| `TKCode.Arc.Fonts` | Geist and Geist Mono (`WithArcFonts()`). | NuGet |
| `TKCode.Arc.Icons.Lucide` | The full Lucide set (2155 icons): the `LucideIcon` control and the `LucideGeometry` markup extension. | NuGet |
| `TKCode.Arc.Blocks` | Page-level blocks (`UseArcBlocks()`). | Build from the repository |
| `TKCode.Arc.AdvancedCharts` | The `DataGrid` (`UseArcAdvancedCharts()`). | Build from the repository |

**Installation** walks through adding them to an application.

## Requirements and platforms

- **.NET**: the packages target `net10.0` and `net11.0`.
- **Avalonia and AtomUI**: Avalonia `12.1.4` and AtomUI `6.2.7`, pinned to exact versions. Use the same versions in
  your application.
- **Platforms**: desktop on Windows, macOS and Linux. The gallery also runs in the browser through its WebAssembly
  host.
- **Rendering of text and code**: Markdown and syntax highlighting use StreamMark, which the `TKCode.Arc` package
  brings in.

## Using this gallery

- **Sidebar.** Docs (these guides), Components (the 22 Arc groups plus Charts' DataGrid, Content and Illustrations) and
  Blocks. `Cmd+K` or `Ctrl+K` focuses the search from anywhere; the All, Ready and Planned filter narrows the list.
- **Component pages.** The header shows the package and its install command. The preview at the top is live. The
  sections follow the same order on every page: when to use, when not to use, usage (with live examples, business
  examples and guidelines), the API reference generated from the XML documentation, accessibility, motion and related
  components.
- **Examples.** Each live example has a Preview tab and a Code tab with its `.axaml` and code-behind. Pictures of
  larger business screens show the exact XAML that produced them, and follow the light or dark appearance.
- **Copy page.** Copies the whole page as Markdown with every example's source included, ready to paste into an issue,
  a design review or an AI assistant.
- **Appearance.** The Appearance panel switches between light and dark, chooses one of the eight accents, sets the
  motion level and changes the language (English, 简体中文, 繁體中文). Everything you see, including the pictures,
  follows these settings.

## Status and versioning

TKCode.Arc is in preview. Versions follow [Semantic Versioning](https://semver.org/); while a version carries a
`-preview` suffix, the API may still change between releases, and each release lists breaking changes in the
**Changelog**. Pin the exact version in your project and read the changelog before upgrading.

## License

TKCode.Arc is released under the MIT license. It ports Arc (MIT) and uses Lucide (ISC), the Geist fonts (OFL),
StreamMark, TextMateSharp, Oniguruma and Markdig, and depends on AtomUI (LGPL-3.0). `THIRD-PARTY-NOTICES.md` in the
repository lists them all.

## Next steps

- **Installation**: add the packages and show your first Arc screen.
- **Theming**: light and dark, accents and design tokens.
- **Motion**: motion levels and writing motion for your own controls.
- **AI and MCP**: working with AI assistants on an Arc codebase.
