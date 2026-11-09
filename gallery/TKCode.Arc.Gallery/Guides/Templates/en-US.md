---
title: Templates
---

# Templates

`TKCode.Arc.Templates` adds `dotnet new` templates for apps built with TKCode.Arc and for the pieces you add to them. A
new app builds and runs as soon as it is created. It references the Arc and Avalonia versions released with the
templates and has Arc's theme, the Inter and Geist fonts, Lucide icons and light/dark switching already in place.

```bash
dotnet new install TKCode.Arc.Templates
dotnet new tkcode-arc -n MyApp --layout sidebar
cd MyApp
dotnet run
```

Visual Studio and Rider list the templates in their New Project dialogs (**TKCode.Arc App**, **TKCode.Arc
Cross-platform App**) with the same options as check boxes and lists.

## Templates

| Template | Short name | Creates |
|---|---|---|
| TKCode.Arc App | `tkcode-arc` | A desktop app (Windows, macOS, Linux) |
| TKCode.Arc Cross-platform App | `tkcode-arc-xplat` | The same app as a shared project, with a desktop host and a browser (WebAssembly) host |
| TKCode.Arc Admin App | `tkcode-arc-admin` | A finished SaaS admin app: sign-in, dashboard, customers, team, billing, settings |
| TKCode.Arc AI Chat App | `tkcode-arc-ai-chat` | A finished AI chat client: conversations, streamed Markdown replies, models, attachments |
| TKCode.Arc Developer Tool | `tkcode-arc-devtool` | A finished IDE-style tool: file explorer, editor tabs, properties, build output, saved layout |
| `tkcode-arc-workflow` | A step palette, an editable `WorkflowCanvas` (drag to connect, Delete, undo and redo, Arrange) with a minimap and zoom controls, an inspector for the selected step, and Run, which walks the steps. The workflow is kept in `workflow.json`; Import and Export use the same JSON. |
| `tkcode-arc-projects` | A sidebar over a `KanbanBoard` (drag cards, quick add, card details), a `GanttTimeline`, a `WeekCalendar` and a `ReleaseReadiness` checklist. |
| `tkcode-arc-analytics` | A page switch over `KpiDrilldown`, `RevenueExplorer`, `CohortRetention`, `UsageForecast` and `UptimeStatus`. |
| TKCode.Arc Workflow Editor | `tkcode-arc-workflow` | A finished flow editor: step palette, editable canvas, inspector, run, JSON import and export |
| TKCode.Arc Project Planner | `tkcode-arc-projects` | A finished planning app: Kanban board, Gantt timeline, week calendar, release checklist |
| TKCode.Arc Analytics Dashboard | `tkcode-arc-analytics` | A finished dashboard: KPI drill-down, revenue, cohort retention, usage forecast, service status |
| TKCode.Arc Component Library | `tkcode-arc-lib` | A control library on Arc: an example control with its token and theme, tests and a sample app, ready to pack |
| TKCode.Arc Page | `tkcode-arc-page` | `Pages/<Name>Page.axaml`, optionally with a view model |
| TKCode.Arc Window | `tkcode-arc-window` | `Views/<Name>Window.axaml`: a secondary window with a footer for its buttons |
| TKCode.Arc Dialog | `tkcode-arc-dialog` | `Dialogs/<Name>Dialog.axaml`: a form shown in an Arc `Dialog`, with an awaitable `ShowAsync` |
| TKCode.Arc Control | `tkcode-arc-control` | `Controls/<Name>/`: a templated control, its design token class and its theme file |

## Layouts

`--layout` picks the window's structure. Every layout puts its content in `Views/MainView.axaml`, which the desktop
window shows and which a browser shows without a window.

| Layout | What you get |
|---|---|
| `blank` (default) | A top bar with the app name and a theme switch over one page with a welcome card. |
| `sidebar` | The collapsible `Sidebar` from `TKCode.Arc.Blocks` with icons, a 64 px icon rail (Cmd+B / Ctrl+B) and three pages: Home, Projects (a list with a "New project" action) and Settings (appearance and accent). |
| `header` | The same three pages, switched from a `SegmentedControl` in the top bar. |
| `dock` | An IDE-style layout from `TKCode.Arc.Dock`: tool panes, document tabs and output panes that you drag to dock, auto-hide or float, with a "Reset layout" action. |

## Options

| Option | Values | Default | Description |
|---|---|---|---|
| `--layout` | `blank`, `sidebar`, `header`, `dock` | `blank` | The window layout (see above). |
| `--mvvm` | `none`, `toolkit` | `none` | `toolkit`: view models built with CommunityToolkit.Mvvm (source-generated properties and commands) and compiled bindings. |
| `--di` | | off | Creates the view models and services with Microsoft.Extensions.DependencyInjection (`Services/AppServices.cs`). Implies `--mvvm toolkit`. |
| `--settings` | `true`, `false` | `true` | Saves the appearance, the accent, the window's size and position and the layout's state (folded sidebar, dock panes, language) and restores them on the next start. |
| `--i18n` | | off | Keeps the UI text in `Localization/*.xlf` (English, 简体中文, 繁體中文) and adds a language switch. |
| `--ui-language` | `en-US`, `zh-CN`, `zh-TW` | `en-US` | The language of the UI text. With `--i18n`, the language the app starts in. |
| `--tests` | | off | Adds a test project that starts the app on Avalonia's headless platform, and a solution. |
| `--custom-titlebar` | | off | Draws Arc's title bar (`ArcWindow`) instead of the system's, with room for your own controls in it. |
| `-f`, `--framework` | `net10.0`, `net8.0`, `net11.0` | `net10.0` | The target framework. `net11.0` needs the .NET 11 SDK; the cross-platform template has `net10.0` only. |
| `--accent` | `Neutral`, `Violet`, `Blue`, `Green`, `Amber`, `Orange`, `Coral`, `Rose` | `Neutral` | The accent color the app starts with. |
| `--no-restore` | | | Skips the restore after the project is created. |

The options combine freely, for example:

```bash
dotnet new tkcode-arc -n Studio --layout dock --mvvm toolkit --tests
dotnet new tkcode-arc -n Console --layout header --i18n --ui-language zh-CN --custom-titlebar
```

## What you get

```text
MyApp/
  MyApp.csproj        TKCode.Arc, TKCode.Arc.Fonts, TKCode.Arc.Icons.Lucide, Avalonia.Desktop, Avalonia.Fonts.Inter
                      (+ TKCode.Arc.Blocks, TKCode.Arc.Dock, CommunityToolkit.Mvvm ... as the options need them)
  Program.cs          the desktop entry point
  AppSetup.cs         UseArcApp(): the fonts, UseArc(...) with Arc's packages, the languages and the start-up theme
  App.cs              creates the main window (on the desktop) or the main view (in a browser); no App.axaml
  Views/              MainWindow (the desktop window) and MainView (the layout)
  Navigation/         sidebar and header layouts: AppPages (the page list) and PageHost (shows the current page)
  Pages/, Models/     sidebar and header layouts: the sample pages and their sample data
  ViewModels/         --mvvm toolkit
  Services/           --di
  Settings/           --settings: AppSettings (JSON, generated serializer, trimming and AOT safe)
  Localization/       --i18n: AppLangResourceKind, en-US.xlf, zh-CN.xlf, zh-TW.xlf, AppLanguages
```

With `--tests` the app moves to `MyApp/MyApp/`, the tests are in `MyApp/MyApp.Tests/` and `MyApp/MyApp.slnx` holds
both. `dotnet test` in the solution folder runs them; they keep the settings in memory, so they never touch your own.

Only the Lucide icons the app uses are compiled in. Declare icons chosen at run time in the project file, for example
`<LucideIcon Include="Bell;ArrowRight" />`.

## Cross-platform app

`tkcode-arc-xplat` has the same layouts and options and creates a solution:

```text
MyApp/
  MyApp.slnx
  MyApp/              the app: views, view models, AppSetup.cs (a library; everything you write goes here)
  MyApp.Desktop/      the desktop host
  MyApp.Browser/      the browser host (WebAssembly): index.html, main.js, and a Chinese font when the UI has Chinese text
```

```bash
dotnet workload install wasm-tools-net10    # once; with the .NET 10 SDK: wasm-tools
dotnet new tkcode-arc-xplat -n MyApp --layout sidebar
cd MyApp
dotnet run --project MyApp.Desktop
dotnet run --project MyApp.Browser          # then open the address it prints
```

In the browser the settings are kept in the page's local storage. A browser gives the app no system fonts, so when the
UI shows Chinese the browser host includes a Noto Sans SC subset (SIL Open Font License) and uses it for Chinese text.

## Start from a finished app

The scenario templates create an app that already does something: real screens built from Arc's blocks, with sample data behind service interfaces. MVVM (CommunityToolkit.Mvvm), dependency injection and saved preferences are always on. The UI is in English, because the blocks' built-in text is English (each block takes its own `Labels` to change it).

```bash
dotnet new tkcode-arc-admin -n Backoffice
dotnet new tkcode-arc-ai-chat -n Assistant --tests
dotnet new tkcode-arc-devtool -n Studio --custom-titlebar
```

| Template | What it has |
|---|---|
| `tkcode-arc-admin` | A sign-in screen (`LoginSplit`; the sample accounts' password is `demo`), then a sidebar with a dashboard (`MetricCard`, `LineChart`), customers (`Table` with search, a status filter, paging and a detail `Drawer`), team (`TeamMembers`, `RolesPermissions`), billing (`BillingOverview`) and settings (`SettingsPage`). The top bar has a command palette (Cmd+K / Ctrl+K) and notifications. |
| `tkcode-arc-ai-chat` | A conversation list beside `AiChat`: streamed Markdown replies with reasoning, attachments, a model menu and suggestions. `SampleResponder` streams prepared replies without any model; its comment shows a responder over `IChatClient` from `Microsoft.Extensions.AI`. Conversations are kept in `conversations.json` beside the settings. |
| `tkcode-arc-devtool` | A `DockLayout` with a file explorer (`TreeView`), editor tabs (Cmd+S / Ctrl+S saves), a `PropertyGrid`, build output and problems, a Run button, a command palette and "Reset layout". The open files come back with the saved layout. |

They take `--tests`, `--custom-titlebar`, `-f`, `--accent` and `--no-restore`, which work as they do for `tkcode-arc`. To make one your own, replace the sample services in `SampleData/`: the pages talk only to the interfaces in `Services/`, and `Services/AppServices.cs` registers what answers them.

The developer tool has no terminal pane. `TKCode.Arc.Terminal` is a terminal emulator with no process behind it: you connect it to a pty yourself, so add it once your app has a process to talk to.

## Write a control library

`tkcode-arc-lib` creates a library of your own controls built on Arc:

```bash
dotnet new tkcode-arc-lib -n Acme.Controls
cd Acme.Controls
dotnet test
dotnet run --project samples/Acme.Controls.Sample
dotnet pack src/Acme.Controls -c Release -o artifacts
```

`src/<name>/` holds an example control (`StatTile`) with its design token class and theme, and a `Use<Name>()` method for `UseArc(...)` that registers everything the Foundation generator found. `tests/` runs the controls on Avalonia's headless platform, and `samples/` is a desktop app that shows them. `--tests false` and `--sample false` leave those projects out. The example token's identity starts with the library's name (`AcmeControlsStatTile`), because an identity must be unique across every package an app registers, Arc's included.

## Add to an app

Run the item templates in the project folder, after the project has been restored (they read its `RootNamespace`;
pass `--namespace` otherwise):

```bash
dotnet new tkcode-arc-page -n Reports                 # Pages/ReportsPage.axaml
dotnet new tkcode-arc-page -n Reports --view-model    # ... and ViewModels/ReportsPageViewModel.cs
dotnet new tkcode-arc-window -n About                 # Views/AboutWindow.axaml
dotnet new tkcode-arc-dialog -n Rename                # Dialogs/RenameDialog.axaml
dotnet new tkcode-arc-control -n StatusTile           # Controls/StatusTile/: StatusTile.cs, StatusTileToken.cs, Themes/StatusTileTheme.axaml
```

- **Page**: to show it, add it to `Navigation/AppPages.cs` and give the navigation an entry with the same key. The
  comment at the top of the page's `.axaml.cs` has both lines.
- **Dialog**: `if (await RenameDialog.ShowAsync(this, "Draft") is { } result) { ... }` from any control in the window;
  null means it was cancelled.
- **Control**: Arc loads a control's theme and publishes its tokens once the app's own assembly is registered in
  `UseArc(...)`. `AppSetup.cs` has the line commented out (with `--i18n` it is already active); uncomment it when you
  add the first control. The token class name without `Token` must not repeat a name Arc uses (`Card`, `Badge`, ...):
  set `[ControlDesignToken(Identity = "...")]` if it does.

## Uninstall

```bash
dotnet new uninstall TKCode.Arc.Templates
```
