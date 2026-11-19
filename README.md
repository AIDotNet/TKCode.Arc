# TKCode.Arc

English | [简体中文](https://github.com/AIDotNet/TKCode.Arc/blob/main/README.zh-CN.md)

[![NuGet](https://img.shields.io/nuget/v/TKCode.Arc?label=NuGet)](https://www.nuget.org/packages/TKCode.Arc)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](https://github.com/AIDotNet/TKCode.Arc/blob/main/LICENSE)

**The visual language and spring motion of [Arc](https://uiarc.dev), as native Avalonia controls.**

TKCode.Arc is a UI kit for .NET applications on Avalonia 12. It ports Arc's 112 free components and adds what a full
product needs around them: page-level blocks and whole screens, a spreadsheet-style data grid, a workflow canvas,
IDE-style docking, a terminal, a PDF viewer, and audio and video playback.

**[Live gallery and documentation](https://docs.tkcode.si/)** · [Changelog](https://github.com/AIDotNet/TKCode.Arc/blob/main/CHANGELOG.md)

## Why Arc

- **Native controls.** Every control derives from Avalonia base types and uses public Avalonia APIs only.
- **One token system.** Colors, radii, shadows and fonts come from design tokens, so light and dark, the eight accent
  presets and whole-theme packages apply across every control.
- **Motion built in.** A spring engine drives the components, with Full, Reduced and Off levels for the whole app.
- **Ready for NativeAOT.** The libraries are built as trimming and AOT compatible.
- **Documented in three languages.** Each of the 400+ component and block pages has live examples and a generated API
  table, in English, 简体中文 and 繁體中文.
- **Readable by AI assistants.** The site publishes [`llms.txt`](https://docs.tkcode.si/llms.txt), every page as
  Markdown and an [agent skill](https://docs.tkcode.si/skills/tkcode-arc/SKILL.md).

## Quick start

Start from a template:

```bash
dotnet new install TKCode.Arc.Templates
dotnet new tkcode-arc -n MyApp          # add --layout sidebar|header|dock, --mvvm toolkit, --i18n, --tests ...
cd MyApp
dotnet run
```

Or add Arc to an existing Avalonia 12 app:

```bash
dotnet add package TKCode.Arc
```

```csharp
using TKCode.Arc;

public static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
              .UsePlatformDetect()
              .UseArc(arc => arc
                  .UseArcControls()   // Arc's controls, themes and design tokens
                  .WithTheme(t => t.Appearance(ArcAppearance.System).Accent(ArcAccentPreset.Neutral)));
```

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Button Content="Save" />
</Window>
```

Each further package registers itself in the same `UseArc(...)` call: `UseArcBlocks()`, `UseArcSpecial()`,
`UseArcAdvancedCharts()`, `UseArcMobile()`, `UseArcWorkflow()`, `UseArcDock()`, `UseArcRibbon()`, `UseArcPdf()`, `UseArcVideo()`,
`UseArcPixelTheme()` and `UseArcCorners()`. Change the theme at run time with
`ArcTheme.Apply(t => t.Appearance(...).Accent(...))`.

## What's inside

All 26 packages are released together on one version; keep them on the same one.

### Components

| Package | Contents |
|---|---|
| `TKCode.Arc` | The components in Arc's 22 sidebar groups except `ChatThread`, plus layout panels and extras such as `Form`, `TreeSelect`, `PropertyGrid`, `Cascader` and `TransferBox`. No Markdown or highlighting dependency. |
| `TKCode.Arc.Markdown` | `Markdown` (with streaming) and `ChatThread` / `ChatComposer`, rendered by StreamMark. Register with `UseArcMarkdown()`. |
| `TKCode.Arc.Highlighting` | TextMate syntax highlighting for `CodeBlock`, the rich text editor's code blocks and Markdown's fenced code; without it code is plain. Register with `UseArcHighlighting()`; set `ArcHighlightLanguages` (for example `csharp;json`) to keep only those grammars in a trimmed app. |
| `TKCode.Arc.Blocks` | Page-level compositions grouped as on uiarc.dev (AI, Agents, auth, settings, commerce, marketing, support and more), and whole screens: dashboards, projects, calendars, people and customers, inbox and support, commerce. |
| `TKCode.Arc.AdvancedCharts` | `DataGrid` (range selection, inline editing, fill handle, clipboard, grouping, export), `Sunburst`, `SankeyFlow`, `FunnelChart`, `RadarChart`, `RealtimeStream`, `RaceBarChart` and `WindRoseChart`. |
| `TKCode.Arc.Special` | Animated AI explainers, glass, docks and tabs, galleries, data widgets, type effects, cursor effects, playful controls, loaders, surfaces and illustrations. |
| `TKCode.Arc.Mobile` | Touch-first controls with 44px touch targets. |

### Application features

| Package | Contents |
|---|---|
| `TKCode.Arc.Workflow` | A canvas for showing, editing and running graphs of steps, with pan, zoom, auto-arrange and undo. |
| `TKCode.Arc.Dock` | IDE-style tab groups and resizable splits, with keyboard navigation and layouts saved as JSON. |
| `TKCode.Arc.Ribbon` | An Office-style ribbon: tabs over grouped commands that shrink and collapse into drop-downs as the window narrows, and a quick access toolbar. |
| `TKCode.Arc.Terminal` | `TerminalView` over libghostty-vt, with the native libraries bundled for Windows, macOS, Linux, Android and iOS. |
| `TKCode.Arc.Pdf`, `.Pdf.Native`, `.Pdf.Browser` | `PdfViewer` with thumbnails, outline, search, text selection and links, using PDFium on desktop and pdf.js on WebAssembly. |
| `TKCode.Arc.Audio`, `.Audio.Native`, `.Audio.Browser` | The `IAudioPlayer` abstraction, with miniaudio on desktop and HTML audio on WebAssembly. |
| `TKCode.Arc.Video`, `.Video.Native`, `.Video.Browser` | `VideoPlayer` and `VideoSurface` over each platform's own player (Media Foundation, AVFoundation, GStreamer on desktop, and the browser's video element on WebAssembly). |
| `TKCode.Arc.Graphics3D`, `.Graphics3D.WebGpu`, `.Graphics3D.Controls`, `.Graphics3D.Browser` (preview) | A 3D engine for .NET with no UI dependency (scene graph, node-based PBR materials, shadows, image-based lighting, animation, glTF, ray casting, post-processing) and its Avalonia controls `Viewport3D` and `ModelViewer`, with a WebGL 2 backend for WebAssembly. Preview packages released with the other packages: the API may change. Verified on macOS, on Windows (an NVIDIA card), on Linux (X11, Mesa's software OpenGL) and in headless Chromium with software WebGL so far; browsers on real graphics hardware are untested. |

### Theming and assets

| Package | Contents |
|---|---|
| `TKCode.Arc.Themes.Pixel` | A pixel-art theme, in token overrides only. |
| `TKCode.Arc.Themes.Corners` | A global corner style: square or scaled corners for every control. |
| `TKCode.Arc.Icons.Lucide` | The full Lucide set. A source generator compiles in only the icons a project uses. |
| `TKCode.Arc.Fonts` | Geist and Geist Mono, plus optional pixel fonts for the pixel theme. |

### Foundation and tooling

| Package | Contents |
|---|---|
| `TKCode.Arc.Foundation` | The token, theme, language and package registration runtime. Depends on Avalonia only. |
| `TKCode.Arc.Core` | ArcDesign tokens and palette, the motion engine, shared primitives and built-in Lucide glyphs. |
| `TKCode.Arc.Templates` | `dotnet new` templates: a starter app, a cross-platform app, six finished apps (admin, AI chat, developer tool, workflow editor, project planner, analytics dashboard), a control library, and item templates for a page, window, dialog and control. |

## Status and compatibility

Arc follows [Semantic Versioning](https://semver.org/): from 1.0.0 on, breaking API changes come only with a new major
version, and a public member is never removed or renamed in one step (it stays as `[Obsolete]` for at least two stable
releases). [CHANGELOG.md](https://github.com/AIDotNet/TKCode.Arc/blob/main/CHANGELOG.md) lists each release and its
migration notes.

| | |
|---|---|
| **.NET** | `net8.0`, `net10.0` and `net11.0`. `TKCode.Arc.Terminal` targets `net10.0` and `net11.0`. |
| **Avalonia** | Avalonia 12.1.5 or any later 12.x release (`[12.1.5, 13.0.0)`), so apps can update Avalonia without waiting for an Arc release. |
| **Desktop** | Windows, macOS and Linux. |
| **Browser** | The gallery itself runs on WebAssembly, and audio, video and PDF have browser backends. |
| **Mobile** | `TKCode.Arc.Mobile` is previewed in a desktop phone frame and is not yet verified on Android or iOS devices. `TKCode.Arc.Terminal` carries libghostty-vt for Android (arm64, x64 and arm, API 29 or later) and iOS (devices and the simulator, as an xcframework the iOS SDK embeds), with or without NativeAOT; `tools/dev/TerminalMobileSample` checks it on the iOS simulator and an Android emulator. |

## Repository

| Path | Contents |
|---|---|
| `src/` | The libraries above and their source generators. `TKCode.Arc.A2UI`, an A2UI 1.0 renderer, is in development and not yet on NuGet. |
| `tests/` | Unit, headless Avalonia and generator snapshot tests, with token and preset baselines. |
| `templates/` | The source of `TKCode.Arc.Templates`. |
| `native/` | Native shims and libraries for the terminal, audio and video. |
| `tools/` | Generators and scripts for icons, native libraries, templates, docs screenshots and packaging. |
| `design/` | The design notes and decisions, starting at `design/overview.md`. |

The gallery and documentation site, with its desktop and browser hosts, the docs pages and the demos, is a separate
repository: [AIDotNet/ArcGallery](https://github.com/AIDotNet/ArcGallery). It builds against a checkout of this one.

## Working on the library

Building needs the .NET 11 SDK (see `global.json`) and the .NET 10 runtime, which the tests run on.

```bash
dotnet build TKCode.Arc.slnx
dotnet test tests/TKCode.Arc.Core.Tests
dotnet test tests/TKCode.Arc.Foundation.Tests
dotnet test tests/TKCode.Arc.Tests --filter "FullyQualifiedName~ButtonTests"   # Blocks, Special and the other packages have their own test projects
```

To run the gallery, clone [ArcGallery](https://github.com/AIDotNet/ArcGallery) next to this repository as `../ArcGallery`
(or anywhere, with `ARC_ROOT` pointing at this checkout) and run `dotnet run --project gallery/TKCode.Arc.Gallery.Desktop -f net10.0` there.

PDFium and pdf.js are not committed: `tools/pdf/sync-native.sh` and `tools/pdf/sync-pdfjs.sh` download them and check their
checksums. Without them the desktop gallery shows a simulated document in the PDF demos, and a browser build has no pdf.js to
import.

Project rules and conventions are in [AGENTS.md](https://github.com/AIDotNet/TKCode.Arc/blob/main/AGENTS.md), and the
guide to writing a component is [design/engineering/implementation-guide.md](https://github.com/AIDotNet/TKCode.Arc/blob/main/design/engineering/implementation-guide.md).

## Community

Ask questions and talk about Avalonia in the QQ group **Avalonia入门交流群** (group number `922379598`). Scan the code or search for the number.

<img src="https://raw.githubusercontent.com/AIDotNet/TKCode.Arc/main/images/qq.jpg" alt="QQ group QR code" width="260">

## Sponsors

TKCode.Arc is mainly sponsored by [nextco.work](https://nextco.work/), which provides affordable, easy-to-use access to
Claude, OpenAI and DeepSeek models. Thank you for supporting the project.

## License

MIT. Arc depends only on Avalonia (MIT) and MIT packages of its own, so closed-source applications can ship it in a
single file or a NativeAOT build. [THIRD-PARTY-NOTICES.md](https://github.com/AIDotNet/TKCode.Arc/blob/main/THIRD-PARTY-NOTICES.md)
covers Arc (MIT), Lucide (ISC), fonts (OFL), StreamMark, TextMateSharp, Oniguruma and Markdig.
