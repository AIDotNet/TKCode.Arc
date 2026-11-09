# TKCode.Arc

English | [简体中文](https://github.com/AIDotNet/TKCode.Arc/blob/main/README.zh-CN.md)

[![NuGet](https://img.shields.io/nuget/v/TKCode.Arc?label=NuGet)](https://www.nuget.org/packages/TKCode.Arc)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](https://github.com/AIDotNet/TKCode.Arc/blob/main/LICENSE)

**The visual language and spring motion of [Arc](https://uiarc.dev), as native Avalonia controls.**

TKCode.Arc is a UI kit for .NET applications on Avalonia 12. It ports Arc's 112 free components and adds what a full
product needs around them: page-level blocks, a spreadsheet-style data grid, a workflow canvas, IDE-style docking, a
terminal, a PDF viewer, and audio and video playback.

**[Live gallery and documentation](https://docs.tkcode.si/)** · [Changelog](https://github.com/AIDotNet/TKCode.Arc/blob/main/CHANGELOG.md)
## Why Arc

- **Native controls.** Every control derives from Avalonia base types and uses public Avalonia APIs only.
- **One token system.** Colors, radii, shadows and fonts come from design tokens, so light and dark, the eight accent
  presets and whole-theme packages apply across every control.
- **Motion built in.** A spring engine drives the components, with Full, Reduced and Off levels for the whole app.
- **Ready for NativeAOT.** The libraries are built as trimming and AOT compatible.
- **Documented in three languages.** Each of the 300+ component and block pages has live examples and a generated API
  table, in English, 简体中文 and 繁體中文.
- **Readable by AI assistants.** The site publishes [`llms.txt`](https://docs.tkcode.si/llms.txt), every page as
  Markdown and an [agent skill](https://docs.tkcode.si/skills/tkcode-arc/SKILL.md).

## What's inside

### Components

| Package | Contents |
|---|---|
| `TKCode.Arc` | The 112 components in 22 groups, organized like Arc's sidebar, plus layout panels and extras such as `Markdown` (with streaming), `PropertyGrid`, `Cascader` and `TransferBox`. |
| `TKCode.Arc.Blocks` | Page-level compositions: AI chat, pricing and checkout, billing, dashboards, settings, team management and more. |
| `TKCode.Arc.AdvancedCharts` | `DataGrid` (range selection, inline editing, fill handle, clipboard, grouping, export), `Sunburst`, `SankeyFlow`, `FunnelChart`, `RadarChart`, `RealtimeStream` and `RaceBarChart`. |
| `TKCode.Arc.Special` | Animated AI explainers, pointer effects, loaders, surfaces and illustrations. |
| `TKCode.Arc.Mobile` | Touch-first controls with 44px touch targets. |

### Application features

| Package | Contents |
|---|---|
| `TKCode.Arc.Workflow` | A canvas for showing, editing and running graphs of steps, with pan, zoom, auto-arrange and undo. |
| `TKCode.Arc.Dock` | IDE-style tab groups and resizable splits, with keyboard navigation and layouts saved as JSON. |
| `TKCode.Arc.Terminal` | `TerminalView` over libghostty-vt, with the native libraries bundled. |
| `TKCode.Arc.Pdf`, `.Pdf.Native`, `.Pdf.Browser` | `PdfViewer` with thumbnails, outline, search, text selection and links, using PDFium on desktop and pdf.js on WebAssembly. New in the next version. |
| `TKCode.Arc.Audio`, `.Audio.Native`, `.Audio.Browser` | The `IAudioPlayer` abstraction, with miniaudio on desktop and HTML audio on WebAssembly. |
| `TKCode.Arc.Video`, `.Video.Native`, `.Video.Browser` | `VideoPlayer` and `VideoSurface` over each platform's own player. |

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
| `TKCode.Arc.Templates` | `dotnet new` templates: a starter app, a cross-platform app, six finished apps, a control library, and item templates for a page, window, dialog and control. |

## Status and compatibility

Arc follows [Semantic Versioning](https://semver.org/): from 1.0.0 on, breaking API changes come only with a new major version. [CHANGELOG.md](https://github.com/AIDotNet/TKCode.Arc/blob/main/CHANGELOG.md)
lists each release and its migration notes.

| | |
|---|---|
| **.NET** | `net8.0`, `net10.0` and `net11.0`. `TKCode.Arc.Terminal` targets `net10.0` and `net11.0`. |
| **Avalonia** | Avalonia 12, pinned to an exact version in [`build/Versions.props`](https://github.com/AIDotNet/TKCode.Arc/blob/main/build/Versions.props). |
| **Desktop** | Windows, macOS and Linux. |
| **Browser** | The gallery itself runs on WebAssembly, and audio, video and PDF have browser backends. |
| **Mobile** | `TKCode.Arc.Mobile` is previewed in a desktop phone frame and is not yet verified on Android or iOS devices. |

## Repository

| Path | Contents |
|---|---|
| `src/` | The libraries above and their source generators. |
| `gallery/` | The gallery and documentation site, with desktop and browser hosts. |
| `tests/` | Unit, headless Avalonia and generator snapshot tests, with token and preset baselines. |
| `templates/` | The source of `TKCode.Arc.Templates`. |
| `native/` | Native shims and libraries for the terminal, audio and video. |
| `tools/` | Generators and scripts for icons, fonts, docs screenshots, native libraries and packaging. |

## Working on the library

Building needs the .NET 11 SDK (see `global.json`) and the .NET 10 runtime, which the tests and the gallery run on.

```bash
dotnet build TKCode.Arc.slnx
dotnet test tests/TKCode.Arc.Core.Tests
dotnet test tests/TKCode.Arc.Foundation.Tests
dotnet test tests/TKCode.Arc.Tests
dotnet run --project gallery/TKCode.Arc.Gallery.Desktop -f net10.0
```

PDFium and pdf.js are not committed: `tools/pdf/sync-native.sh` and `tools/pdf/sync-pdfjs.sh` download them and check their
checksums. Without them the desktop gallery shows a simulated document in the PDF demos, and a browser build has no pdf.js to
import.

Project rules and conventions are in [AGENTS.md](https://github.com/AIDotNet/TKCode.Arc/blob/main/AGENTS.md).

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
