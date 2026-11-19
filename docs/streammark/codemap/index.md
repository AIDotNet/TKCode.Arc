---
commit: a72b840
dirty: true
updated: 2026-10-16
---
# 代码地图

这是什么：对上面这个提交时点的代码的描述，用来快速找到代码与依赖。
它与代码不一致时以代码为准，并在同一次改动里修正页面。
规则在 `docs/streammark/agent-guide.md`；原因与历史在 `docs/streammark/memory/`。

> 页面写作跨越 `44a8f5a` → `cd7a2a8`（不停有并发提交），各页 frontmatter 里的 `commit` 是它实际核对过的版本。本仓库当时仍在频繁推进（差异视图、链接策略、引用卡片正在加入），行号可能已经漂移——按符号名找代码。
> 本文件记录的工作树在写地图时是脏的（`native/ratex` 子模块指针与几处未提交改动）。

## 架构
[architecture.md](architecture.md) — 线程模型、分层与依赖方向、不可跨越的边界、新代码归属表。

## 模块
| 模块 | 职责 | 关键入口 | 页面 |
|---|---|---|---|
| core-streaming | 只追加源码缓冲、冻结边界切分、快照与差异、节奏控制 | `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs` › `StreamingDocument` | [core-streaming](modules/core-streaming.md) |
| remend-and-offsetmap | 未完成 Markdown 修复（vercel remend 的 C# 移植）与原始源码偏移映射 | `src/TKCode.Arc.StreamMark/Core/Remend/Remend.cs` › `Remend.Complete` | [remend-and-offsetmap](modules/remend-and-offsetmap.md) |
| core-parsing-and-model | 文档方言、Markdig → 块模型映射、白名单组件、容错 HTML 解析与 CSS 子集 | `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdownProfile.cs` › `MarkdownProfile` | [core-parsing-and-model](modules/core-parsing-and-model.md) |
| avalonia-text-rendering | 自绘块视图、样式解析、差异应用与流式渐显 | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/DocumentPresenter.cs` › `DocumentPresenter` | [avalonia-text-rendering](modules/avalonia-text-rendering.md) |
| avalonia-transcript | 会话虚拟化面板：行模型、行高实测、滚动锚定、贴底 | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs` › `TranscriptPanel` | [avalonia-transcript](modules/avalonia-transcript.md) |
| avalonia-selection | 与视图无关的选区模型、指针/键盘手势、纯文本与源码导出 | `src/TKCode.Arc.StreamMark/Avalonia/Selection/SelectionController.cs` › `SelectionController` | [avalonia-selection](modules/avalonia-selection.md) |
| avalonia-images | 图片获取策略、解码档位、内存与磁盘缓存、占位与查看器 | `src/TKCode.Arc.StreamMark/Avalonia/Images/ImagePipeline.cs` › `ImagePipeline` | [avalonia-images](modules/avalonia-images.md) |
| avalonia-plugin-system | 插件注册表、服务下发、内置组件、块状态存储 | `src/TKCode.Arc.StreamMark/Avalonia/Plugins/PluginRegistry.cs` › `PluginRegistry` | [avalonia-plugin-system](modules/avalonia-plugin-system.md) |
| avalonia-extensions | 三个渲染扩展包：TextMate 高亮、RaTeX 公式、Mermaid 图表 | `src/TKCode.Arc.StreamMark.RaTeX/RaTeXPlugin.cs` › `RaTeXPlugin` | [avalonia-extensions](modules/avalonia-extensions.md) |
| theme-and-styles | `Md.*` 资源键、默认主题、样式快照解析、宿主令牌（Arc 的 `Markdown`） | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs` › `MarkdownStyle.Resolve` | [theme-and-styles](modules/theme-and-styles.md) |
| native-ratex | RaTeX 的 pin、本地补丁、原生库构建脚本与产物布局 | `tools/streammark/build-ratex.sh`、`src/TKCode.Arc.StreamMark.RaTeX/RaTeXNative.cs` | [native-ratex](modules/native-ratex.md) |
| build-and-packaging | 统一构建属性、中央包版本、NativeAOT 门禁、本地打包与 CI | `Directory.Build.props`、`.github/workflows/ci.yml` | [build-and-packaging](modules/build-and-packaging.md) |
| tests | 两个测试项目：无头 Avalonia 宿主、fixture 语料、回归守卫 | `tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs` › `HeadlessApp` | [tests](modules/tests.md) |
| gallery-and-benchmarks | 示例程序（兼 NativeAOT 冒烟）与 BenchmarkDotNet 基准 | `tools/streammark/Gallery/SmokeTest.cs` › `SmokeTest` | [gallery-and-benchmarks](modules/gallery-and-benchmarks.md) |

## 流程
| 流程 | 触发 | 模块 | 页面 |
|---|---|---|---|
| 流式 token 到视图 | 任意线程 `Append`（通常经 `MarkdownStream.Push` + `StreamPacer`） | core-streaming、remend-and-offsetmap、core-parsing-and-model、avalonia-text-rendering、avalonia-transcript、avalonia-plugin-system、avalonia-extensions | [streaming-token-to-view](flows/streaming-token-to-view.md) |
| 流结束整篇重解析 | `StreamingDocument.Complete()` | core-streaming、remend-and-offsetmap、core-parsing-and-model、avalonia-text-rendering、avalonia-transcript | [stream-completion-reparse](flows/stream-completion-reparse.md) |
| 插件注册 | 宿主构造 `MarkdownServices` 并 `SetServices`，用 `services.Profile` 建文档 | avalonia-plugin-system、core-parsing-and-model、avalonia-text-rendering、avalonia-extensions、theme-and-styles | [plugin-registration](flows/plugin-registration.md) |
| 原始 HTML 渲染 | 文档中出现 HTML 块或行内 HTML（可跨空行夹在 Markdown 之间） | core-parsing-and-model、avalonia-text-rendering、core-streaming、avalonia-images、theme-and-styles | [raw-html-render](flows/raw-html-render.md) |
| 选区与复制 | 用户在 `MarkdownView` / `TranscriptView` 上按下指针 | avalonia-selection、avalonia-transcript、avalonia-text-rendering、core-parsing-and-model | [text-selection-copy](flows/text-selection-copy.md) |
| 会话虚拟化与滚动 | `Entries` 变化、快照到达、用户滚动或改窗口宽度 | avalonia-transcript、core-streaming、avalonia-text-rendering、avalonia-selection、avalonia-images | [transcript-virtualization-scroll](flows/transcript-virtualization-scroll.md) |
| 主题解析与下发 | 控件挂载、`ResourcesChanged`、明暗切换、宿主换回主题 | theme-and-styles、avalonia-text-rendering、avalonia-transcript、avalonia-plugin-system | [theme-resolution](flows/theme-resolution.md) |

## 领域
[domain.md](domain.md) — 业务概念到代码、存储与界面的映射表。

## 尚未绘制
- `docs/` — 设计文档与研究笔记（[docs/streammark/architecture.md](../../docs/streammark/architecture.md)、[docs/streammark/decisions.md](../../docs/streammark/decisions.md)、`docs/streammark/research/`），它们是叙述性资料，不是代码模块。
- `tools/streammark/remend-fixtures/` — 从上游 TypeScript 测试录制 fixture 的 Node 脚本；由 [tests](modules/tests.md) 页按用法记录，没有单独页面。
- 第三方与许可文件（`LICENSE`、`THIRD-PARTY-NOTICES.md`）。
