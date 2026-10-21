---
title: 更新日志
source-hash: ce993bc90d50
---

# 更新日志

TKCode.Arc 的所有重要变更，最新的在前。版本遵循[语义化版本](https://semver.org/)。版本带有 `-preview` 后缀期间，API 在各发布版本之间仍可能变化；升级前请阅读说明，并在项目中固定版本号。同样的列表也保存在仓库的 `CHANGELOG.md` 中。

每次发布附带的 NuGet 包：`TKCode.Arc`、`TKCode.Arc.Core`、`TKCode.Arc.Fonts` 和 `TKCode.Arc.Icons.Lucide`。`TKCode.Arc.Blocks` 与 `TKCode.Arc.AdvancedCharts` 从仓库构建。

## 0.1.1-preview.1 (2026-10-21)

### 新增

- **组件**：Activity、Content、Editors、Illustrations、Media 和 SpecialInputs 分类，以及 ButtonGroup、SplitButton、Card、Reactions、SwipeActions，还有 Color、Date、DateRange 和 Time 选取器。
- **Core**：`MotionTimeline` 与 `LiveLoop` 动效驱动器、`SharedElement` 基础组件、`ArcBlur` 与 `ArcViewport`。
- **Blocks**（`TKCode.Arc.Blocks`）：AppShell 区块（Sidebar、PageHeader、CommandPalette、SearchResults）以及设置区块（SettingsPage、SecuritySettings、BillingOverview、UsageBilling、CancelFlow、SettingsCommand）。
- **AdvancedCharts**（`TKCode.Arc.AdvancedCharts`）：一个新项目，首先提供 DataGrid 控件。
- **图库**
  - 元件页面即其带实时示例的文档页面；每个示例都是一个 `.axaml` 文件，其“代码”标签页显示该源码。
  - 侧边栏新增文档区，按 `Cmd+K` 或 `Ctrl+K` 可在窗口任何位置聚焦搜索。
  - 从 XML 文档生成的 API 表格，附 zh-CN 与 zh-TW 翻译；外壳文本与文档页面提供 en-US、zh-CN 和 zh-TW 版本。
  - 通过 StreamMark `0.1.0-alpha.23` 实现 Markdown 渲染与代码高亮，这是首个可在浏览器中运行的版本。
  - WebAssembly 宿主（`TKCode.Arc.Gallery.Browser`），以及桌面和浏览器宿主的应用图标。
- **发布**
  - 图库的 WebAssembly 镜像在 GitHub 上构建，以 `ghcr.io/atomuicommunity/tkcode-arc-gallery` 发布，并采用 brotli、预压缩文件和浏览器缓存提供服务。
  - 图库桌面应用的发布构建：Windows 与 Linux（未签名），macOS（已签名并完成公证）。
  - 通过 nuget.org 可信发布，从 CI 打包并发布 NuGet 包。
- **工具**：API 文档翻译流程的脚本，以及图标构建器。
- 简体中文版 README 与赞助者部分。

### 变更

- 各分类的共享辅助代码由 Core 的共享基础组件取代。
- 图库页面从按分类编写的 C# 页面迁移为 Markdown 文档页面与 `.axaml` 示例。
- 为许多控件的公共属性和事件补充了 XML `<summary>` 文本；它们会填充图库的 API 表格。

### 修复

- 动效：等待暂停的循环可能让渲染器空闲且帧请求挂起；现在帧会持续产生。
- HoldToConfirm、ConfirmMorph（长按模式）、Tabs 面板与 Carousel 视口在复用裁剪区域时没有使被裁剪的视觉元素失效，因此长按填充不会移动，且增长中的面板或调整大小后的轮播会保留过期的裁剪区域。
- Countdown：静止的滚轮会被绘制为普通字形，使滴答作响的表盘帧时间减半。
- 当 XAML 将 `Minimum` 设得高于当前 `Maximum` 时（例如 `Minimum="360" Maximum="900"`），Slider 与 ElasticSlider 会因 `Math.Clamp` 抛出异常。
- 切换选中的标签页时，Tabs 不再滚动其所在的页面；标签列表会自行显示选中的标签页。
- 图库浏览器宿主：为 WebAssembly 构建原生 Skia，发布时跳过裁剪。

### 从 0.1.0-preview.1 升级

- 请将所有 TKCode.Arc 包同时更新到相同版本。
- 新增的分类与控件除 `UseArc()` 外无需额外设置。区块与 DataGrid 需要各自的注册（`UseArcBlocks()`、`UseArcAdvancedCharts()`）以及项目引用，详见**安装**。

## 0.1.0-preview.1 (2026-10-20)

首个预览版本。
