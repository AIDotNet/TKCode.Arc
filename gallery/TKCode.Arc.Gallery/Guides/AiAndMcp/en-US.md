---
title: AI and MCP
---

# AI and MCP

AI coding assistants work well with TKCode.Arc, because everything they need is written down in plain text: every
component has a Markdown page written from its source, every public property and event has an XML description, and
the conventions are consistent across the library. What makes the difference between generated code that compiles and
code that invents properties is the context the assistant is given. This page shows where that context lives, how to
hand it over, and how to review what comes back.

## What an assistant should know first

Most mistakes assistants make with this library come from a few wrong assumptions. State these up front, in a project
instruction file or at the start of a conversation:

| Fact | Why it matters |
|---|---|
| This is Avalonia 12 with AtomUI 6.2.8, not WPF, WinUI or HTML. | Assistants otherwise mix in WPF properties (`Visibility`, `DataTemplateSelector`) or CSS ideas. |
| Arc controls live in `xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"`. | Without the prefix, `Button` resolves to Avalonia's own button and loses Arc's look. |
| The app calls `UseArc()` inside `UseAtomUI(...)`, and the `Application` class is `partial`. | Missing either produces unstyled controls or a build error. |
| Colors, radii and fonts come from `{arc:ArcDesignTokenResource ...}` or `ArcTokens`. | Hard-coded hex values break dark mode and accents. |
| Motion is declared under `^:motion-full` and `^:motion-reduced` and respects `ArcMotion.Level`. | Hand-written animations otherwise ignore reduced motion. |
| Only documented properties exist. | The component pages and API tables list them; anything else is invented. |

## Where the knowledge lives

| Source | Location | Best for |
|---|---|---|
| Component pages | `gallery/TKCode.Arc.Gallery/Docs/<Category>/<Component>/en-US.md` (also `zh-CN.md`, `zh-TW.md`) | When to use a component, usage, binding, validation, accessibility, keyboard and motion. |
| Live examples | `gallery/TKCode.Arc.Gallery/Demos/<Category>/<Component>/*.axaml` | Working XAML for each example on the pages. |
| API descriptions | The XML documentation shipped with the packages | Every public property, event and type, as IntelliSense shows it and as the API tables list it. |
| This gallery | The Docs section and every component page | Reading in context, with live examples. |
| Repository conventions | `AGENTS.md` at the repository root | Contributors and assistants working on the library itself. |

### Copy page

Every component page has **Copy page** at the top. It copies the entire page as Markdown, including the source of
every live example (the `.axaml` and its code-behind) and the generated API table. Paste it into the conversation when
you ask for help with a component: the assistant then works from the real API and real examples instead of from
memory.

For a screen that uses several components, copy each of their pages, or point the assistant at the Markdown files in
the repository when it can read files directly.

## A project instruction file

Assistants such as Claude Code, Cursor and GitHub Copilot read an instruction file from the repository (for example
`AGENTS.md`, `CLAUDE.md` or `.github/copilot-instructions.md`). A section like this one, adapted to your app, prevents
most of the mistakes above:

```text
## UI library: TKCode.Arc

- UI is Avalonia 12.1.4 + AtomUI 6.2.8 + TKCode.Arc. Do not use WPF or WinUI APIs.
- Arc controls use xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc" (arc:Button, arc:Input, ...).
- Only use properties, events and enum values that the component's docs page or API table lists.
- Colors, radii, spacing and fonts: {arc:ArcDesignTokenResource Name} in XAML, ArcTokens.Bind in code.
  Never hard-code a color.
- Forms: arc:Input / arc:Select / arc:Switch with Label and Description; errors through ErrorMessage
  or data validation on the bound property.
- Motion: Arc controls animate themselves. For custom controls, declare transitions under ^:motion-full
  and ^:motion-reduced with arc:ArcMotion.Tracks="True".
- View models use CommunityToolkit.Mvvm ([ObservableProperty], [RelayCommand]).
- Component docs: <path to a clone>/gallery/TKCode.Arc.Gallery/Docs/<Category>/<Component>/en-US.md
```

## Prompts that work

Give the assistant the business goal, the component pages, and the constraints. Compare:

| Instead of | Write |
|---|---|
| "Make a settings page." | "Build a workspace settings view with arc:Input for the name and billing email, arc:Select for the currency and arc:Switch for the weekly digest. Use the attached Input, Select and Switch pages. Bind to a WorkspaceSettingsViewModel with ObservableValidator; validate the email on lost focus." |
| "Add a chart." | "Show daily active users for the last 30 days with arc:LineChart, following the attached LineChart page. Data comes from IAnalyticsService.GetDailyActiveUsersAsync(); format numbers with the current culture." |
| "Make it look nicer." | "Align this form with the Input page's content guidelines: sentence-case labels, a description for each field that has a constraint, and error messages that say how to fix the value." |

Ask for one screen or one component at a time, and ask the assistant to list the properties it used and where each is
documented. That list is quick to check against the API tables.

## Reviewing generated code

Before accepting code an assistant wrote, check:

- **Names.** Every `arc:` element, property, event and enum value exists in the component's API table. A XAML
  property that does not exist fails at build time; an event handler with the wrong argument type fails later.
- **Tokens.** No hex colors or fixed corner radii in views; tokens instead.
- **Binding modes.** Two-way properties (`Text`, `IsChecked`, `SelectedItem`, `Value`) are bound to settable view model
  properties; read-only ones (`EffectiveErrorMessage`, `SelectedValues`) are only read.
- **Accessibility.** Every field has a `Label` (visible, or hidden with `IsLabelVisible="False"` only when something
  else on screen names it); icon-only buttons have an accessible name.
- **Motion.** No hand-written animations where a control already animates, and custom transitions under the motion
  pseudo-classes.
- **Threading.** Values that come from background work are set on the UI thread (`Dispatcher.UIThread`).

## MCP

The Model Context Protocol (MCP) lets an assistant read resources and call tools from a server, instead of relying on
what was pasted into the conversation.

TKCode.Arc does not ship its own MCP server yet. Until it does, two setups give an assistant the documentation
directly:

- **Assistants that read files** (Claude Code, Cursor and similar): clone the repository next to your project, or add
  it to the workspace, and point the assistant at `gallery/TKCode.Arc.Gallery/Docs` and `.../Demos`.
- **Clients that only use MCP**: run a file system MCP server over the docs folder. With the reference server from the
  MCP project, a client configuration looks like this:

```json
{
  "mcpServers": {
    "tkcode-arc-docs": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "/path/to/AtomUI.Arc/gallery/TKCode.Arc.Gallery/Docs"]
    }
  }
}
```

The assistant can then list the component folders and read the page for each component it uses. Keep the clone on
the same version as the packages in your application, so the docs describe the API you compile against.
