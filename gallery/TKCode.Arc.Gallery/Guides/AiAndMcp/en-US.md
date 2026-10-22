---
title: AI and MCP
---

# AI and MCP

AI coding assistants work well with TKCode.Arc, because everything they need is written down in plain text: every
component has a Markdown page written from its source, every public property and event has an XML description, and
the conventions are consistent across the library. What makes the difference between generated code that compiles and
code that invents properties is the context the assistant is given. This page shows where that context lives, how to
hand it over, and how to review what comes back. The documentation site publishes the same knowledge for assistants
that fetch it themselves: an `llms.txt` index, every page as Markdown, and an agent skill.

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
| `llms.txt` | https://docs.tkcode.si/llms.txt | The first thing to give an assistant: install steps, rules, and every page with a one-line summary and a link to its Markdown. |
| Pages as Markdown | A page's address plus `.md`, such as https://docs.tkcode.si/components/button.md (translations under `/zh-CN/` and `/zh-TW/`) | One component, block or guide in full, with example source and API reference, without a clone. |
| Agent skill | https://docs.tkcode.si/skills/tkcode-arc/SKILL.md | Coding agents that load skills: workflow, rules, component choices and a review checklist. |
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

## llms.txt and Markdown pages

The gallery at https://docs.tkcode.si is an Avalonia WebAssembly app that draws to a canvas, so an assistant that
fetches a page's address gets no text. The site therefore publishes the same pages as plain text, generated from the
docs pages each time it is published:

| Address | Contents |
|---|---|
| `/llms.txt` | The index, in the llms.txt format: what the library is, how to install it, rules for agents, and every guide, component and block with a one-line summary and a link to its Markdown. |
| `/llms-full.txt` | Every English page in one file, for assistants with a large context window. |
| `/<section>/<slug>.md` | One page: its address plus `.md`, such as `/docs/theming.md`, `/components/button.md` or `/blocks/sidebar.md`. A component page is what **Copy page** copies, under a header with its package and install command. |
| `/zh-CN/…`, `/zh-TW/…` | The same pages in Simplified and Traditional Chinese, such as `/zh-CN/components/button.md`. |

For an assistant that can fetch web pages, one sentence is enough:

```text
Before writing UI code with TKCode.Arc, read https://docs.tkcode.si/llms.txt, then fetch the Markdown page of every
component you use and use only the properties and events in its API reference.
```

## Agent skill

A skill is a folder of instructions that a coding agent loads when a task needs it. The TKCode.Arc skill holds the
workflow, the rules on this page, a table of which component fits which need, and two reference files:

| File | Contents |
|---|---|
| `SKILL.md` | The workflow (find, read, install, wire, style, review), the rules, the common tokens and quick component decisions. |
| `components.md` | Every component and block by group, with its class, its purpose and a link to its page. |
| `checklist.md` | The review checklist an agent runs before it finishes a screen. |

To install it for Claude Code in a project, download the three files into `.claude/skills/tkcode-arc/`:

```bash
mkdir -p .claude/skills/tkcode-arc
for file in SKILL.md components.md checklist.md; do
  curl -fsSL "https://docs.tkcode.si/skills/tkcode-arc/$file" -o ".claude/skills/tkcode-arc/$file"
done
```

Put the folder in `~/.claude/skills/` instead to use it in every project; other agents that support skills read the
same layout from their own skills folder. The files name the TKCode.Arc version they describe, so download them again
after an upgrade.

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
- Component docs: https://docs.tkcode.si/llms.txt (every page as Markdown, such as
  https://docs.tkcode.si/components/button.md)
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

The skill's `checklist.md` is a longer form of this list, written for an agent to run on its own code.

## MCP

The Model Context Protocol (MCP) lets an assistant read resources and call tools from a server, instead of relying on
what was pasted into the conversation.

TKCode.Arc does not ship its own MCP server yet. Until it does, three setups give an assistant the documentation
directly:

- **Assistants that fetch web pages**: start them at https://docs.tkcode.si/llms.txt (see above).
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
