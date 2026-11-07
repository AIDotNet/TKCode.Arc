# TKCode.Arc.Templates

`dotnet new` templates for [TKCode.Arc](https://github.com/AIDotNet/TKCode.Arc), the Arc-style component library for
Avalonia. The project builds and runs as soon as it is created. It references the TKCode.Arc packages released with this
template version and sets up Arc's theme, the Inter and Geist fonts, Lucide icons, `AppBuilder.UseArc(...)` and light/dark
switching.

## Install

```bash
dotnet new install TKCode.Arc.Templates
```

Visual Studio and Rider list installed templates in their New Project dialogs as **TKCode.Arc App**.

## Create an app

```bash
dotnet new tkcode-arc -n MyApp                      # blank layout
dotnet new tkcode-arc -n MyApp --layout sidebar     # sidebar layout
cd MyApp
dotnet run
```

| Option | Values | Default | Description |
|---|---|---|---|
| `--layout` | `blank`, `sidebar` | `blank` | The window layout (see below). |
| `-f`, `--framework` | `net10.0`, `net8.0`, `net11.0` | `net10.0` | The target framework. `net11.0` needs the .NET 11 SDK. |
| `--accent` | `Neutral`, `Violet`, `Blue`, `Green`, `Amber`, `Orange`, `Coral`, `Rose` | `Neutral` | The accent color the app starts with. |
| `--no-restore` | | | Skips the restore after the project is created. |

### Layouts

- **blank**: a top bar with the app name and a `ThemeSwitcher`, over one page with a welcome card.
- **sidebar**: the `Sidebar` from `TKCode.Arc.Blocks` with Lucide icons, the selected-page highlight and a collapse to a
  64 px icon rail (from the top bar's toggle, or Cmd+B / Ctrl+B in the sidebar). A top bar shows the page title and a
  `ThemeSwitcher`. Three sample pages (Home, Projects, Settings) switch in place, and Settings chooses light, dark or system
  appearance through `ArcTheme.Apply`.

## What you get

```text
MyApp/
  MyApp.csproj        TKCode.Arc, TKCode.Arc.Fonts, TKCode.Arc.Icons.Lucide (+ TKCode.Arc.Blocks for the sidebar),
                      Avalonia.Desktop and Avalonia.Fonts.Inter
  Program.cs          AppBuilder: fonts, UseArc(...) with the packages and the start-up theme
  App.cs              creates the main window (Arc needs no App.axaml)
  Views/MainWindow.axaml(.cs)
  Navigation/         sidebar layout: AppPages (the page list) and PageHost (shows the current page)
  Pages/              sidebar layout: HomePage, ProjectsPage, SettingsPage
```

To add a page to the sidebar layout, create it in `Pages/`, add it to `Navigation/AppPages.cs`, and add an
`arc:SidebarItem` with the same `Key` in `Views/MainWindow.axaml`.

Only the Lucide icons the project uses are compiled in. Declare icons chosen at run time in the project file, for example
`<LucideIcon Include="Bell;ArrowRight" />`.

## Uninstall

```bash
dotnet new uninstall TKCode.Arc.Templates
```

## Working on the templates

The template lives in `content/ArcStarterApp/`. `ArcStarterApp` is the source name that `-n` replaces, and `ArcDisplayName`
is replaced with the name as typed (the window title). Packing writes the repository's Arc and Avalonia versions
(`build/Versions.props`) into the project file in place of `__ARC_VERSION__` and `__AVALONIA_VERSION__`. Each layout's own
files are under `Layouts/<Layout>/`; files shared by the layouts that navigate between pages are under `Layouts/Navigation/`.
To add a layout (for example `header` or `dock`), add a folder under `Layouts/`, a choice to the `layout` symbol and a
`sources` entry with its condition in `.template.config/template.json`.

```bash
dotnet pack templates/TKCode.Arc.Templates -c Release -o artifacts/feed
dotnet new install artifacts/feed/TKCode.Arc.Templates.<version>.nupkg
```
