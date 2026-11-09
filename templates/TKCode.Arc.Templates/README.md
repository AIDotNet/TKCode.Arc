# TKCode.Arc.Templates

`dotnet new` templates for [TKCode.Arc](https://github.com/AIDotNet/TKCode.Arc), the Arc-style component library for
Avalonia: app templates that build and run as soon as they are created, and item templates for what you add to them. An
app references the TKCode.Arc packages released with this template version and sets up Arc's theme, the Inter and Geist
fonts, Lucide icons, `AppBuilder.UseArc(...)`, light/dark switching and saved preferences.

| Template | Short name | Creates |
|---|---|---|
| TKCode.Arc App | `tkcode-arc` | A desktop app (Windows, macOS, Linux) |
| TKCode.Arc Cross-platform App | `tkcode-arc-xplat` | The same app as a shared project with a desktop and a browser (WebAssembly) host |
| TKCode.Arc Admin App | `tkcode-arc-admin` | A finished SaaS admin app: sign-in, dashboard, customers, team, billing, settings |
| TKCode.Arc AI Chat App | `tkcode-arc-ai-chat` | A finished AI chat client: conversations, streaming Markdown replies, models, attachments |
| TKCode.Arc Developer Tool | `tkcode-arc-devtool` | A finished IDE-style tool: explorer, editor tabs, properties, build output, saved layout |
| TKCode.Arc Workflow Editor | `tkcode-arc-workflow` | A finished flow editor: step palette, editable canvas, inspector, run, JSON import and export |
| TKCode.Arc Project Planner | `tkcode-arc-projects` | A finished planning app: Kanban board, Gantt timeline, week calendar, release checklist |
| TKCode.Arc Analytics Dashboard | `tkcode-arc-analytics` | A finished dashboard: KPI drill-down, revenue, cohort retention, usage forecast, service status |
| TKCode.Arc Component Library | `tkcode-arc-lib` | A control library on Arc: an example control with its token and theme, tests, a sample app, ready to pack |
| TKCode.Arc Page | `tkcode-arc-page` | `Pages/<Name>Page.axaml(.cs)`, optionally with a view model |
| TKCode.Arc Window | `tkcode-arc-window` | `Views/<Name>Window.axaml(.cs)`: a secondary window with a footer |
| TKCode.Arc Dialog | `tkcode-arc-dialog` | `Dialogs/<Name>Dialog.axaml(.cs)`: a form in an Arc `Dialog` with an awaitable `ShowAsync` |
| TKCode.Arc Control | `tkcode-arc-control` | `Controls/<Name>/`: a templated control, its design token class and its theme file |

## Install

```bash
dotnet new install TKCode.Arc.Templates
```

Visual Studio and Rider list the app templates in their New Project dialogs under their names (**TKCode.Arc App**,
**TKCode.Arc Admin App**, ...).

## Create an app

```bash
dotnet new tkcode-arc -n MyApp                                   # blank layout
dotnet new tkcode-arc -n MyApp --layout sidebar --mvvm toolkit   # sidebar layout with view models
dotnet new tkcode-arc -n MyApp --layout dock --tests             # dock layout, with a test project
cd MyApp
dotnet run
```

| Option | Values | Default | Description |
|---|---|---|---|
| `--layout` | `blank`, `sidebar`, `header`, `dock` | `blank` | The window layout (see below). |
| `--mvvm` | `none`, `toolkit` | `none` | `toolkit`: view models with [CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm) (source-generated properties and commands) and compiled bindings. |
| `--di` | | off | View models and services from Microsoft.Extensions.DependencyInjection (`Services/AppServices.cs`). Implies `--mvvm toolkit`. |
| `--settings` | `true`, `false` | `true` | Saves the appearance, the accent, the window's size and position and the layout's state (folded sidebar, dock panes, language). |
| `--i18n` | | off | UI text in `Localization/*.xlf` (English, 简体中文, 繁體中文) with a language switch. |
| `--ui-language` | `en-US`, `zh-CN`, `zh-TW` | `en-US` | The language of the UI text; with `--i18n`, the language the app starts in. |
| `--tests` | | off | A headless test project (xunit.v3, Avalonia.Headless.XUnit) and a solution: the app goes to `<name>/`, the tests to `<name>.Tests/`. |
| `--custom-titlebar` | | off | Arc's title bar (`ArcWindow`) instead of the system's. |
| `-f`, `--framework` | `net10.0`, `net8.0`, `net11.0` | `net10.0` | The target framework. `net11.0` needs the .NET 11 SDK; `tkcode-arc-xplat` has `net10.0` only. |
| `--accent` | `Neutral`, `Violet`, `Blue`, `Green`, `Amber`, `Orange`, `Coral`, `Rose` | `Neutral` | The accent color the app starts with. |
| `--no-restore` | | | Skips the restore after the project is created. |

### Layouts

Every layout puts its content in `Views/MainView.axaml`; `Views/MainWindow.axaml` is the desktop window around it.

- **blank**: a top bar with the app name and a `ThemeSwitcher`, over one page with a welcome card.
- **sidebar**: the `Sidebar` from `TKCode.Arc.Blocks` with Lucide icons, the selected-page highlight and a collapse to a
  64 px icon rail (from the top bar's toggle, or Cmd+B / Ctrl+B in the sidebar). Three sample pages (Home, Projects,
  Settings) switch in place; Projects lists sample data with a "New project" action, and Settings chooses the
  appearance and the accent through `ArcTheme.Apply`.
- **header**: the same three pages, switched from a `SegmentedControl` in the top bar.
- **dock**: an IDE-style layout from `TKCode.Arc.Dock` (tool panes, document tabs, output panes; drag to dock, auto-hide
  or float) with a "Reset layout" action; the layout is saved with the other preferences.

### Cross-platform

```bash
dotnet workload install wasm-tools-net10    # once; with the .NET 10 SDK: wasm-tools
dotnet new tkcode-arc-xplat -n MyApp --layout sidebar
cd MyApp
dotnet run --project MyApp.Desktop
dotnet run --project MyApp.Browser
```

`MyApp/` holds the app (a library: views, view models, `AppSetup.cs`), `MyApp.Desktop/` and `MyApp.Browser/` start it.
In the browser the settings live in the page's local storage, and when the UI shows Chinese the host brings a Noto Sans SC
subset (SIL Open Font License) because a browser gives the canvas no system fonts.

## What you get

```text
MyApp/
  MyApp.csproj        TKCode.Arc, TKCode.Arc.Fonts, TKCode.Arc.Icons.Lucide, Avalonia.Desktop and Avalonia.Fonts.Inter
                      (+ TKCode.Arc.Blocks, TKCode.Arc.Dock, CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection)
  Program.cs          the desktop entry point
  AppSetup.cs         UseArcApp(): fonts, UseArc(...) with the packages, the languages and the start-up theme
  App.cs              creates the main window (desktop) or the main view (browser); Arc needs no App.axaml
  Views/              MainWindow and MainView
  Navigation/         sidebar and header layouts: AppPages (the page list) and PageHost (shows the current page)
  Models/, Pages/     sidebar and header layouts: the sample data and pages
  ViewModels/         --mvvm toolkit
  Services/           --di
  Settings/           --settings: AppSettings and its stores (System.Text.Json source generation)
  Localization/       --i18n: AppLangResourceKind, en-US.xlf, zh-CN.xlf, zh-TW.xlf, AppLanguages
```

With `--mvvm toolkit` the navigation's selected key and the page shown are bound to `MainViewModel`, and
`Navigation/AppPages.cs` pairs each page with its view model in code, so nothing is looked up by type name (no reflection;
the app can be trimmed). Only the Lucide icons the project uses are compiled in; declare icons chosen at run time in the
project file, for example `<LucideIcon Include="Bell;ArrowRight" />`.

## Start from a finished app

The scenario templates create an app that already does something: real screens built from Arc's blocks, with sample
data behind service interfaces. MVVM (CommunityToolkit.Mvvm), dependency injection and saved preferences are always on;
the UI is English (Arc's blocks have English text, set per block through their `Labels`).

```bash
dotnet new tkcode-arc-admin -n Backoffice
dotnet new tkcode-arc-ai-chat -n Assistant --tests
dotnet new tkcode-arc-devtool -n Studio --custom-titlebar
dotnet new tkcode-arc-workflow -n Flows
dotnet new tkcode-arc-projects -n Planner
dotnet new tkcode-arc-analytics -n Insights
```

| Template | What it has |
|---|---|
| `tkcode-arc-admin` | A sign-in screen (`LoginSplit`; the sample accounts' password is `demo`), then a sidebar with a dashboard (`MetricCard`, `LineChart`), customers (`Table` with search, a status filter, paging and a detail `Drawer`), team (`TeamMembers`, `RolesPermissions`), billing (`BillingOverview`) and settings (`SettingsPage`); a command palette (Cmd+K / Ctrl+K) and notifications in the top bar. |
| `tkcode-arc-ai-chat` | A conversation list beside `AiChat`: streamed Markdown replies with reasoning, attachments, a model menu and suggestions. `SampleResponder` streams prepared replies without any model; its comment shows a responder over `Microsoft.Extensions.AI`'s `IChatClient`. The conversations are saved in `conversations.json` beside the settings. |
| `tkcode-arc-workflow` | A step palette, an editable `WorkflowCanvas` (drag to connect, Delete, undo and redo, Arrange) with a minimap and zoom, an inspector for the selected step and Run, which walks the steps. The workflow is saved in `workflow.json` beside the settings; Import and Export read and write the same JSON. |
| `tkcode-arc-projects` | A sidebar with a `KanbanBoard` (drag cards, quick add, card details), a `GanttTimeline`, a `WeekCalendar` and a `ReleaseReadiness` checklist. |
| `tkcode-arc-analytics` | A page switch over `KpiDrilldown`, `RevenueExplorer`, `CohortRetention`, `UsageForecast` and `UptimeStatus`. |
| `tkcode-arc-devtool` | A `DockLayout` with a file explorer (`TreeView`), editor tabs (Cmd+S / Ctrl+S saves), a `PropertyGrid`, build output and problems, Run, a command palette and "Reset layout". The open files come back with the saved layout. |

Options: `--tests`, `--custom-titlebar`, `-f` (`net10.0`, `net8.0`, `net11.0`), `--accent`, `--no-restore`, as for
`tkcode-arc`. To make it your own, replace the sample services in `SampleData/` with yours: the pages talk to the
interfaces in `Services/`, and `Services/AppServices.cs` registers what answers them.

The developer tool has no terminal: `TKCode.Arc.Terminal` is a terminal emulator without a process behind it (you
connect a pty yourself), so a terminal pane would show nothing to type into. Add it to the Output pane's group once
your app has a process to talk to.

## Write a control library

```bash
dotnet new tkcode-arc-lib -n Acme.Controls
cd Acme.Controls
dotnet test
dotnet run --project samples/Acme.Controls.Sample
dotnet pack src/Acme.Controls -c Release -o artifacts
```

`src/<name>/` is the library: an example control (`StatTile`) with its design token class and its theme, and
`Use<Name>()` for `UseArc(...)`, which registers everything the Foundation generator found. `tests/` starts the
controls on Avalonia's headless platform; `samples/` is a desktop app that shows them. `--tests false` and
`--sample false` leave those out; `-f` picks `net10.0`, `net8.0` or `net11.0`. The example token's identity has the
library's name in front (`AcmeControlsStatTile`): an identity must be unique among every package an app registers,
Arc's included.

## Add to an app

Run the item templates in the project folder, after the project has been restored (they read its `RootNamespace`; pass
`--namespace` otherwise):

```bash
dotnet new tkcode-arc-page -n Reports                 # Pages/ReportsPage.axaml(.cs)
dotnet new tkcode-arc-page -n Reports --view-model    # ... and ViewModels/ReportsPageViewModel.cs
dotnet new tkcode-arc-window -n About                 # Views/AboutWindow.axaml(.cs)
dotnet new tkcode-arc-dialog -n Rename                # Dialogs/RenameDialog.axaml(.cs)
dotnet new tkcode-arc-control -n StatusTile           # Controls/StatusTile/: StatusTile.cs, StatusTileToken.cs, Themes/StatusTileTheme.axaml
```

- **Page**: to show it, add it to `Navigation/AppPages.cs` and give the navigation an entry with the same key; the
  comment at the top of the page's `.axaml.cs` has both lines.
- **Dialog**: `if (await RenameDialog.ShowAsync(this, "Draft") is { } result) { ... }` from any control in the window;
  null means cancelled.
- **Control**: Arc loads a control's theme and publishes its tokens once the app's assembly is registered in
  `UseArc(...)`. `AppSetup.cs` has the line commented out (active with `--i18n`); uncomment it when you add the first
  control. The token class name (without `Token`) must not repeat a name Arc uses (`Card`, `Badge`, ...): set
  `[ControlDesignToken(Identity = "...")]` if it does.

## Uninstall

```bash
dotnet new uninstall TKCode.Arc.Templates
```

## Working on the templates

- `content/_shared/App/` is the app the app templates create: `App.cs`, `Common/` (`AppSetup.cs`, the main window),
  `Layouts/<Layout>/`, `Layouts/Navigation/` (shared by sidebar and header) and `Features/<Option>/`. `content/_shared/Tests/`
  and `content/_shared/Solution/` are the test project and the solution. The two templates take them through `../_shared/`
  sources; `content/ArcStarterApp/` and `content/ArcXplatApp/` hold only their own project files and hosts.
- `.template.config/template.json` of every app template is written by `tools/templates/write_app_templates.py`: edit that, not the
  JSON. The UI text is in `strings.json` (every language); `tools/templates/sync_strings.py` turns it into the
  `@@Key@@` / `@@cs:Key@@` / `@@kind:Key@@` token symbols and the `--i18n` catalog (`Features/I18n/Localization/`).
  Write UI text in template files as tokens, and run both scripts after a change.
- `ArcStarterApp` is the source name that `-n` replaces; `ArcDisplayName` is the name as typed (the window title).
  Packing writes the repository's Arc and Avalonia versions (`build/Versions.props`) and the versions in this package's
  project file into the project files in place of `__ARC_VERSION__`, `__AVALONIA_VERSION__`, `__MVVM_VERSION__`, ...
- The scenario templates are `content/ArcAdminApp/`, `ArcAiChatApp/`, `ArcDevtoolApp/`, `ArcWorkflowApp/`,
  `ArcProjectsApp/` and `ArcAnalyticsApp/`: each holds only its
  `App/` (views, view models, services, sample data) and `Tests/`; the project file and `Program.cs` come from
  `content/ArcStarterApp/`, the rest from `_shared/`.
- `content/ArcLibrary/` is the component library template; its `.template.config` is written by hand.
- The item templates are `content/ArcPage`, `ArcWindow`, `ArcDialog` and `ArcControl`.

`tools/templates/verify_templates.py` packs the packages the templates need, creates every case (a set of options plus
the item templates), compares the created files with `tests/Baselines/templates/`, builds them with warnings as errors,
runs their tests and starts them headless (pages, theme and language switching, saved preferences, the item templates'
output). Run it after changing a template; `--update` accepts a changed file list, `--skip-browser` leaves out the
cases that need the WebAssembly workload.

```bash
dotnet pack templates/TKCode.Arc.Templates -c Release -o artifacts/feed
dotnet new install artifacts/feed/TKCode.Arc.Templates.<version>.nupkg
```
