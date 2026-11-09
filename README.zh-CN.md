# TKCode.Arc

[English](https://github.com/AIDotNet/TKCode.Arc/blob/main/README.md) | 简体中文

[![NuGet](https://img.shields.io/nuget/v/TKCode.Arc?label=NuGet)](https://www.nuget.org/packages/TKCode.Arc)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](https://github.com/AIDotNet/TKCode.Arc/blob/main/LICENSE)

**把 [Arc](https://uiarc.dev) 的视觉语言和弹簧动效，做成原生 Avalonia 控件。**

TKCode.Arc 是面向 Avalonia 12 的 .NET 应用 UI 套件。它移植了 Arc 的 112 个免费组件，并补齐完整产品所需的其他部分：
页面级区块、电子表格式数据表格、工作流画布、IDE 式停靠布局、终端、PDF 查看器，以及音视频播放。

**[在线组件库与文档](https://docs.tkcode.si/)** · [更新日志](https://github.com/AIDotNet/TKCode.Arc/blob/main/CHANGELOG.md)
## 为什么选择 Arc

- **原生控件。** 每个控件都派生自 Avalonia 基类，且只使用 Avalonia 的公开 API。
- **统一的令牌体系。** 颜色、圆角、阴影和字体都来自设计令牌，因此明暗模式、8 种强调色预设和整套主题包都能作用于所有控件。
- **内置动效。** 组件由弹簧引擎驱动，整个应用可在 Full、Reduced、Off 三个级别之间切换。
- **支持 NativeAOT。** 库按兼容裁剪和 AOT 的方式构建。
- **三种语言的文档。** 300 多个组件和区块页面都带在线示例和自动生成的 API 表格，提供 English、简体中文和繁體中文版本。
- **方便 AI 助手读取。** 站点发布了 [`llms.txt`](https://docs.tkcode.si/llms.txt)、每个页面的 Markdown 版本和一份
  [Agent Skill](https://docs.tkcode.si/skills/tkcode-arc/SKILL.md)。

## 包含内容

### 组件

| 包 | 内容 |
|---|---|
| `TKCode.Arc` | 22 个分组共 112 个组件，目录结构与 Arc 侧边栏一致；另有布局面板和扩展控件，如支持流式渲染的 `Markdown`、`PropertyGrid`、`Cascader` 和 `TransferBox`。 |
| `TKCode.Arc.Blocks` | 页面级组合：AI 对话、定价与结账、账单、仪表盘、设置、团队管理等。 |
| `TKCode.Arc.AdvancedCharts` | `DataGrid`（区域选择、行内编辑、填充柄、剪贴板、分组、导出）、`Sunburst`、`SankeyFlow`、`FunnelChart`、`RadarChart`、`RealtimeStream` 和 `RaceBarChart`。 |
| `TKCode.Arc.Special` | AI 原理动画、指针特效、加载动画、背景表面和插画。 |
| `TKCode.Arc.Mobile` | 触控优先的控件，触控目标为 44px。 |

### 应用功能

| 包 | 内容 |
|---|---|
| `TKCode.Arc.Workflow` | 用于展示、编辑和运行步骤图的画布，支持平移、缩放、自动排列和撤销。 |
| `TKCode.Arc.Dock` | IDE 式标签组和可调整大小的分栏，支持键盘导航，布局可保存为 JSON。 |
| `TKCode.Arc.Terminal` | 基于 libghostty-vt 的 `TerminalView`，已内置原生库。 |
| `TKCode.Arc.Pdf`、`.Pdf.Native`、`.Pdf.Browser` | `PdfViewer`，支持缩略图、大纲、搜索、文本选择和链接，桌面端使用 PDFium，WebAssembly 使用 pdf.js。将在下一版本提供。 |
| `TKCode.Arc.Audio`、`.Audio.Native`、`.Audio.Browser` | `IAudioPlayer` 抽象，桌面端使用 miniaudio，WebAssembly 使用 HTML audio。 |
| `TKCode.Arc.Video`、`.Video.Native`、`.Video.Browser` | 基于各平台自带播放器的 `VideoPlayer` 和 `VideoSurface`。代码已在本仓库，尚未发布到 NuGet。 |

### 主题与资源

| 包 | 内容 |
|---|---|
| `TKCode.Arc.Themes.Pixel` | 像素风主题，只覆盖令牌。 |
| `TKCode.Arc.Themes.Corners` | 全局圆角风格：让所有控件使用直角或按比例缩放的圆角。 |
| `TKCode.Arc.Icons.Lucide` | 完整的 Lucide 图标集。源生成器只把项目用到的图标编译进去。 |
| `TKCode.Arc.Fonts` | Geist 和 Geist Mono，以及可选的像素主题字体。 |

### 基础与工具

| 包 | 内容 |
|---|---|
| `TKCode.Arc.Foundation` | 令牌、主题、语言和包注册运行时，只依赖 Avalonia。 |
| `TKCode.Arc.Core` | ArcDesign 令牌与调色板、动效引擎、共享基础组件和内置 Lucide 图标。 |
| `TKCode.Arc.Templates` | `dotnet new` 模板：起步应用、跨平台应用、6 个完整应用、控件库，以及页面、窗口、对话框和控件的项模板。 |

## 状态与兼容性

Arc 遵循[语义化版本](https://semver.org/)：从 1.0.0 起，破坏性 API 变更只在新的主版本中出现。各版本内容和迁移说明见
[CHANGELOG.md](https://github.com/AIDotNet/TKCode.Arc/blob/main/CHANGELOG.md)。

| | |
|---|---|
| **.NET** | `net8.0`、`net10.0` 和 `net11.0`。`TKCode.Arc.Terminal` 面向 `net10.0` 和 `net11.0`。 |
| **Avalonia** | Avalonia 12，在 [`build/Versions.props`](https://github.com/AIDotNet/TKCode.Arc/blob/main/build/Versions.props) 中锁定精确版本。 |
| **桌面** | Windows、macOS 和 Linux。 |
| **浏览器** | 组件库站点本身运行在 WebAssembly 上，音频、视频和 PDF 都有浏览器后端。 |
| **移动端** | `TKCode.Arc.Mobile` 目前在桌面的手机外框中预览，尚未在 Android 或 iOS 真机上验证。 |

## 仓库结构

| 路径 | 内容 |
|---|---|
| `src/` | 上述各个库及其源生成器。 |
| `gallery/` | 组件库与文档站点，包含桌面和浏览器宿主。 |
| `tests/` | 单元测试、Avalonia 无头测试和生成器快照测试，以及令牌和预设基线。 |
| `templates/` | `TKCode.Arc.Templates` 的源码。 |
| `native/` | 终端、音频和视频用到的原生封装与原生库。 |
| `tools/` | 图标、字体、文档截图、原生库和打包相关的生成器与脚本。 |

## 参与开发

构建需要 .NET 11 SDK（见 `global.json`）和 .NET 10 运行时，测试和组件库站点都运行在 .NET 10 上。

```bash
dotnet build TKCode.Arc.slnx
dotnet test tests/TKCode.Arc.Core.Tests
dotnet test tests/TKCode.Arc.Foundation.Tests
dotnet test tests/TKCode.Arc.Tests
dotnet run --project gallery/TKCode.Arc.Gallery.Desktop -f net10.0
```

PDFium 和 pdf.js 没有提交到仓库：`tools/pdf/sync-native.sh` 和 `tools/pdf/sync-pdfjs.sh` 会下载它们并校验校验和。缺少它们时，桌面版组件库的
PDF 示例会显示模拟文档，浏览器构建也没有 pdf.js 可导入。

项目规则和约定见 [AGENTS.md](https://github.com/AIDotNet/TKCode.Arc/blob/main/AGENTS.md)。

## 社区

欢迎加入 QQ 群 **Avalonia入门交流群**（群号 `922379598`）提问和交流。扫描二维码或搜索群号即可加入。

<img src="https://raw.githubusercontent.com/AIDotNet/TKCode.Arc/main/images/qq.jpg" alt="QQ 群二维码" width="260">

## 赞助

TKCode.Arc 主要由 [nextco.work](https://nextco.work/) 赞助，其提供低价、好用的 Claude、OpenAI 和 DeepSeek 模型服务。
感谢对本项目的支持。

## 许可证

MIT。Arc 只依赖 Avalonia（MIT）和自有的 MIT 包，闭源应用可以单文件或 NativeAOT 发布。Arc（MIT）、Lucide（ISC）、
字体（OFL）、StreamMark、TextMateSharp、Oniguruma 和 Markdig 的许可信息见
[THIRD-PARTY-NOTICES.md](https://github.com/AIDotNet/TKCode.Arc/blob/main/THIRD-PARTY-NOTICES.md)。
