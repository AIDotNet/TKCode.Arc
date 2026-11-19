---
module: avalonia-text-rendering
sources: ["src/TKCode.Arc.StreamMark/Avalonia/Rendering/**", "src/TKCode.Arc.StreamMark/Avalonia/MarkdownView.cs", "src/TKCode.Arc.StreamMark/Avalonia/MarkdownStream.cs", "src/TKCode.Arc.StreamMark/Avalonia/MarkdownResourceKeys.cs", "src/TKCode.Arc.StreamMark/Avalonia/MarkdownResourceAlias.cs", "src/TKCode.Arc.StreamMark/Avalonia/Themes/**"]
commit: 44a8f5a
updated: 2026-10-16
---
# Avalonia 块视图与文本自绘层

把 Core 的块模型与文档快照画成 Avalonia 控件：每个顶层块一个自绘 `BlockView`，每个文本片段一个 `SegmentLayout`（逐行 `TextLine`，流式追加只重排末尾几行；行内样式走自定义 `ITextSource`，公式 / 图片 / 复选框是 `DrawableTextRun`），`DocumentPresenter` 按快照差异增删改这些视图，主题色由 `MarkdownStyle` 一次性解析成资源快照下发。

**不负责：** Markdown 解析与块模型（`core-parsing-and-model`，`src/TKCode.Arc.StreamMark/Core/Model/`、`src/TKCode.Arc.StreamMark/Core/Parsing/`）；文档快照与差异算法（`core-streaming`，`src/TKCode.Arc.StreamMark/Core/Streaming/SnapshotDiff.cs:SnapshotDiff.Compute`）；选区坐标、命中与复制（`avalonia-selection`，`src/TKCode.Arc.StreamMark/Avalonia/Selection/`）；会话虚拟化与行回收（`avalonia-transcript`，`src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs`）；图片的取数 / 解码 / 缓存（`avalonia-images`，`src/TKCode.Arc.StreamMark/Avalonia/Images/ImagePipeline.cs`，本层只用 `Rendering/ImageSlot.cs` 持有句柄）；插件注册与块视图注册表（`avalonia-plugin-system`，`src/TKCode.Arc.StreamMark/Avalonia/Plugins/PluginRegistry.cs`）；RaTeX / TextMate / Mermaid 的具体实现（`avalonia-extensions`）与 AtomUI 令牌（`theme-and-styles`）。

## Entry points
| Symbol | File | Role |
|---|---|---|
| `MarkdownView` | `src/TKCode.Arc.StreamMark/Avalonia/MarkdownView.cs:16` | 单文档控件（非虚拟化）：`Markdown` / `Document` / `Stream` 三个输入属性，持有 `DocumentPresenter` 与 `SelectionController` |
| `MarkdownView.ApplyLatest` | `src/TKCode.Arc.StreamMark/Avalonia/MarkdownView.cs:159` | 把 `EffectiveDocument.Latest` 交给 presenter；动画帧回调、测试与外部同步刷新都走这里 |
| `DocumentPresenter.Apply` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/DocumentPresenter.cs:30` | 应用一份 `DocumentSnapshot`：按 `SnapshotDiff` 插入 / 更新 / 删除顶层视图，维护末尾片段标记 |
| `BlockViewFactory.Create` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/BlockViewFactory.cs:8` | 块模型 → 块视图：插件组件 / 插件围栏视图优先，其余按内建分支 |
| `TextSegmentView.Set` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:92` | 设置片段内容与外观；内容只追加且样式不变时保留差异点之前的整形结果 |
| `TextSegmentView`（`Render`） | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs` | 片段自身只画：透明命中区域 → 查找高亮 / 选区矩形 → 流式光标；文字与删除线由各行的 `SegmentLineVisual`（`RenderLine`）绘制 |
| `SegmentLayout` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/SegmentLayout.cs` | 逐行排版：`Format(source, …, from, cache)` 从差异点所在行的前一行起用 `TextFormatter.FormatLine` 重排，返回第一个被替换的行号；命中测试（`HitTestPoint` / `HitTestTextPosition` / `HitTestTextRange`）与 `TextLayout` 同名同义；两端对齐整段交给 `TextLayout` 后接管其行 |
| `SegmentLineVisual` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/SegmentLineVisual.cs` | 一行一个非输入元素的 `Visual`，只在该行变化时重新录制；`ClipRight` 让渐显部分交给渐显层 |
| `TailFade` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/TailFade.cs` | 末尾逐字渐显：排版变化时（`ArrangeOverride` / 末尾状态变化 / 结束定时器）计算渐显字形与各批矩形，经 `CompositionCustomVisual.SendHandlerMessage` 交给渲染线程的 `FadeHandler`，渲染线程逐帧按批次不透明度绘制 |
| `MarkdownStyle.Resolve` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:66` | 从控件可见资源一次性解析不可变样式快照 |
| `MarkdownStream` | `src/TKCode.Arc.StreamMark/Avalonia/MarkdownStream.cs:13` | 网络分片 → `StreamPacer` → `StreamingDocument`，并记录渐显时间；任意线程可 `Push` / `Complete` |
| `MarkdownResourceKeys` | `src/TKCode.Arc.StreamMark/Avalonia/MarkdownResourceKeys.cs:7` | 全部 `Md.*` 资源键的声明处 |
| `StreamMarkTheme` | `src/TKCode.Arc.StreamMark/Avalonia/Themes/StreamMarkTheme.axaml:1` | 默认主题字典（Light / Dark 两套画刷 + 等宽字体 / 间距 / 圆角） |

## Key types and functions
| Symbol | File | Role |
|---|---|---|
| `BlockView` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/BlockView.cs:13` | 块视图基类：`Update(context, block, topKey, ref segment)`（`internal`，:25）→ `OnUpdate`；`LastSegment()` / `Segments()` 供末尾标记与选区命中；`KeyFor` 按阅读顺序分配片段号（:43） |
| `StackBlockView` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/BlockView.cs:61` | 纵向排布的容器视图：`UpdateChildren` 按位置复用子视图（类型相同则 `Update`，否则替换） |
| `TextBlockView` / `ThematicBreakBlockView` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/BlockView.cs:159` / `:225` | 段落 / 标题 / HTML 块（一个 `TextSegmentView`，H1/H2 下方画分隔线）；分隔线（固定高 17） |
| `CodeBlockView` + `CodeTextView` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/CodeBlockView.cs:22` / `:256` | 代码块头（语言标题、插件操作、换行、复制）+ 逐行排版缓存的代码区；`SetChunk`（:73）只显示某一窗口的行，偏移保持全局 |
| `CodeBlockState` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/CodeBlockState.cs:11` | 块共享状态：代码、行起点、增量高亮会话；`For(context, blockKey, language)`（:48）从 `BlockStateStore` 取，语言 / 高亮器 / 明暗变化时重建 |
| `TableBlockView` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/TableBlockView.cs:17` | 自定义列宽布局（不用 `Grid`），列宽自然宽度 → 按比例收缩 → 横向滚动；悬停时在网格上方的操作条（`ActionBarHeight`）显示“复制为 Markdown” / 全屏，按钮不进单元格 |
| `MathBlockView` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MathBlockView.cs:15` | 块级公式，`ISelectableSegment` 自身即片段；排版失败时保留上一次成功结果，闭合后仍失败则退回源码 |
| `DiagramBlockView` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/DiagramBlockView.cs:17` | 图表（如 Mermaid）后台渲染 + 静态 LRU 缓存 + 图表 / 源码切换，渲染前与失败时显示源码；挂上可视树后才渲染，主题的半透明颜色叠到真实底色上再传给渲染器；图表按宽度适配、高度只受 6000px 安全上限约束 |
| `HtmlBoxBlockView` + `HtmlPaint` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/HtmlBoxBlockView.cs:11` / `HtmlPaint.cs:9` | HTML 盒子：外边距 / 边框 / 背景 / 宽度，文字样式经 `RenderContext.WithHtml` 下传；`HtmlPaint` 处理作者颜色的对比度 |
| `QuoteBlockView` / `AlertBlockView` / `ListBlockView` / `ListItemBlockView` / `FootnotesBlockView` / `DefinitionListBlockView` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/ContainerViews.cs:12` / `:29` / `:100` / `:201` / `:286` / `:330` | 引用、GitHub 提示块、列表（项目符号按嵌套深度）、列表项（符号 / 复选框与首行基线对齐；启用任务交互时复选框可点击、可聚焦，见 `RenderContext.TaskToggle`）、脚注（转成有序 `ListBlock`）、定义列表 |
| `RenderContext` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/RenderContext.cs:12` | 一个文档共享的渲染上下文：`Style` / `Services` / `Selection` / `Entry` / `Streaming` / `State` / `ImageBaseUri` / `Inherited` / `TaskToggle`（internal：宿主启用任务交互时设置，为 null 则复选框只读） |
| `StreamingState` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/StreamingState.cs:7` | 渐显与光标状态：`RecordReveal`（:29）记录每批文本的显示时间，`GetFadingBatches` 返回仍在渐显的各批（距末尾字符数、长度、时间），`GetFadingTail` 返回字符数与首末不透明度；`_trimmed` 记录已移出队列批次的终点（队列第一批的起点） |
| `InlineTextSource`（`TextSegmentView` 私有嵌套） | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:687` | `ITextSource` 实现：按 span / 对象切分 run、解析字重字形与回退字体、生成 `DrawableTextRun` |
| `DrawableTextRun` 家族 | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/InlineRuns.cs:18` / `:34` / `:69` / `:135` | 行内公式、独立排版的文字对象（公式源码 / 图片替代文字）、行内图片（骨架 / 标签 / 缩放）、任务复选框 |
| `IconButton` + `MarkdownIcons` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/IconButton.cs:12` / `:121` | 自绘小图标按钮（不依赖宿主 Button 模板）与 16×16 线图标 |
| `DocumentPresenter` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/DocumentPresenter.cs:10` | `internal`；`_views`（顶层视图列表）、`Applied`（已应用快照）、`_tail`（末尾片段） |
| `ImageSlot` | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/ImageSlot.cs:12` | 一个视图中一张图片的状态与缓存句柄（`Ensure` / `Retry` / `Approve` / `Dispose`） |

## Dependencies
- Uses: `core-parsing-and-model` via 块类型（`ParagraphBlock` / `CodeBlock` / `TableBlock` / `HtmlBoxBlock`…）与 `InlineContent` / `InlineStyle` / `InlineObject` — `src/TKCode.Arc.StreamMark/Avalonia/Rendering/BlockViewFactory.cs:91`
- Uses: `core-streaming` via `DocumentSnapshot` + `SnapshotDiff.Compute`（`DiffKind.Insert/Update/Remove`）与 `DocumentSnapshot.IsComplete` — `src/TKCode.Arc.StreamMark/Avalonia/Rendering/DocumentPresenter.cs:34`、`src/TKCode.Arc.StreamMark/Core/Streaming/SnapshotDiff.cs:16`
- Uses: `core-streaming`（节奏控制器）via `StreamPacer` 与 `StreamingDocument.Append/Complete` — `src/TKCode.Arc.StreamMark/Avalonia/MarkdownStream.cs:22`、`:55`
- Uses: `avalonia-selection` via `SelectionModel` / `SelectionController` / `ISelectableSegment` / `SegmentKey` — `src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:438`、`src/TKCode.Arc.StreamMark/Avalonia/Selection/TextPoint.cs:33`
- Uses: `avalonia-images` via `ImagePipeline.Ensure` / 句柄租约 — `src/TKCode.Arc.StreamMark/Avalonia/Rendering/ImageSlot.cs:41`、`src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:393`
- Uses: `avalonia-plugin-system` via `context.Plugins`（`TryGetBlockComponent` / `FindFenceView` / `FindInlineComponent` / `BlockActions` / `Highlighter` / `Math`）与 `BlockStateStore` — `src/TKCode.Arc.StreamMark/Avalonia/Rendering/BlockViewFactory.cs:13`、`src/TKCode.Arc.StreamMark/Avalonia/Plugins/BlockStateStore.cs:8`
- Uses: `avalonia-extensions` via `IMathRenderer.Layout`、`ISyntaxHighlighter`（`CodeBlockState`）、`IDiagramRenderer.RenderAsync` — `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MathBlockView.cs:40`、`src/TKCode.Arc.StreamMark/Avalonia/Rendering/DiagramBlockView.cs:114`
- Uses: `core-highlighting`（`HighlightToken` / `HighlightThemeKind`）— `src/TKCode.Arc.StreamMark/Avalonia/Rendering/CodeBlockView.cs:419`、`src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:43`
- Used by: `avalonia-transcript` via `BlockViewFactory.Create/CanReuse`、`MarkdownStyle.Resolve`、`CodeBlockView.SetChunk`、`TextSegmentView.IsDocumentTail` 与 `view.CurrentContext` — `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:495`、`:488`、`:231`、`:782`、`:460`
- Used by: 宿主主题 via 覆盖 `Md.*` 资源并把值写成 `MarkdownResourceAlias(宿主键)` — `src/TKCode.Arc.Markdown/Content/Markdown/Markdown.cs:s_aliases`
- Used by: `tests` via `TextSegmentView.UseRunCache` / `Content` / `Layout`、`MarkdownView.Snapshot` / `ApplyLatest`、`HeadlessApp` — `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/RunCacheBenchTests.cs:16`、`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/MarkdownViewSnapshotTests.cs:31`

## State it owns
- `TextSegmentView._runCache`（`TextSegmentView.cs:32`）：Avalonia 12 的 `TextRunCache`（`[Unstable]`，编译期是 CS0618），**只在本文件**使用，四处都在 `#pragma warning disable CS0618` 内（:30、:154、:290、:300、:361）。失效规则：样式 / 上下文变化或非追加时 `Invalidate()`（:157）；只追加时按 `FirstDifference`（:181）算差异点，再从**差异点所在段落的起点**`InvalidateFrom(paragraphStart)`（:164）；可用宽度变化且片段含宽度相关对象（图片 / 组件）时整体 `Invalidate()`（:288）；`OnResourceLoaded`（图片等异步资源就绪，:359）整体 `Invalidate()`。`TextSegmentView.UseRunCache`（:36，`internal`）可整体关掉缓存供测试 / 基准对比。
- `TextSegmentView._layout` / `_layoutWidth`：派生排版对象，宽度或对齐变化时 `Dispose` 后重建；`_fullRebuild` / `_runCacheWidth`（:322 / :325）决定“本次用到的图片”列表是否完整，只有整体重建时才 `ReleaseUnusedImages()`（:312）。
- `DocumentPresenter._views` / `Applied` / `Context` / `_tail`（`DocumentPresenter.cs:12-15`）：视图列表是 `MarkdownView` 视觉子级的权威副本，`Applied` 是下一次 `SnapshotDiff.Compute` 的基准；`_tail` 是当前带 `IsDocumentTail` 标记的片段。
- `MarkdownStyle`（`MarkdownStyle.cs:15`）：不可变样式快照，只读；主题变体、`ResourcesChanged`、`Services` / `ImageBaseUri` 变化时由 `MarkdownView` 置空 `_context` 并重建（`MarkdownView.cs:64`、`:44`、`:172`），是宿主资源的**派生副本**，不反向写回。
- `StreamingState`（`StreamingState.cs:7`）：渐显时间队列（加锁），由 `MarkdownStream` 在界面线程写入（`MarkdownStream.cs:52`、`:99`），由文档末尾片段读取；`MarkdownStream.Complete` 时 `Document.Complete()` 并 `IsActive = false`（`MarkdownStream.cs:114`）。
- `CodeBlockState`（`CodeBlockState.cs:11`）：代码、行起点索引、高亮会话与结果，存在 `BlockStateStore`（键 `"code.state"`，`CodeBlockState.cs:52`），进程内后台任务计算、界面线程发布（:142）；视图回收重建、长代码分段都复用它。`BlockStateStore` 还保存 `code.wrap`（`CodeBlockView.cs:26`）与 `diagram.code`（`DiagramBlockView.cs:43`）。
- 静态缓存：`DiagramCache`（`DiagramBlockView.cs`，32 项 LRU，键 = 渲染器身份 + 语言 + 明暗 + 颜色 + 字体 + 代码）、`BrushCache`（`CodeBlockView.cs:621`，按 ARGB）、`HtmlPaint.s_brushes`（`HtmlPaint.cs:11`，按 `CssColor`）。
- 视图内的图片句柄：`TextSegmentView._images` / `_usedImages`（`TextSegmentView.cs:369` / `:379`），离开可视树时全部 `Dispose` 并重新排版（:239）。
- 计时 / 帧状态：`TailFade._settle`（渐显结束后恢复行的完整绘制的一次性定时器）、`MarkdownStream` 的显示帧调度（`SetFrameSource` + 100ms 计时器兜底）、`MarkdownView._frameRequested`（快照就绪合帧，`MarkdownView.cs:140`）、`CodeBlockView._copiedTimer`（复制后的图标回退，:158）、`DiagramBlockView._cts`（取消上一次渲染，:83）。时钟是 `StreamClock.Now`（`InlineRuns.cs:10`，基于 `Stopwatch`）。
- `MarkdownView._ownDocument`（`MarkdownView.cs:33`）：只设置 `Markdown` 时自建的 `StreamingDocument`，设置 `Stream` / `Document` 时置空。

## Contracts it exposes
- `Md.*` 资源键：声明在 `MarkdownResourceKeys`（`MarkdownResourceKeys.cs:9-31`），共 20 个；默认值在 `MarkdownStyle.Resolve` 的 `Get<T>(key, fallback)`（`MarkdownStyle.cs:70`）与 `Themes/StreamMarkTheme.axaml` 的 Light / Dark 字典（:9、:22，`Md.FontFamily` / `Md.FontSize` / `Md.LineHeight` 故意不设，回落到控件继承的 `TextElement` 值，`MarkdownStyle.cs:98`）。值为 `MarkdownResourceAlias` 时从控件自身再查目标键，最多 4 层（:77）；值为 `Color` 时自动转 `ImmutableSolidColorBrush`（:88）。
- `MarkdownView` 输入：`Markdown`（静态文本，内部 `Load` 一份自建文档，`MarkdownView.cs:101`）、`Document`（外部 `StreamingDocument`）、`Stream`（`MarkdownStream`，优先，`MarkdownView.cs:99`）。三者任一变化都重置上下文、清空 presenter 与块状态、清空选区（:101）。
- `MarkdownStream` 线程约定：`Push` / `Complete` / `Flush` 可在任意线程调用，内部 `Dispatcher.UIThread.Post`（`MarkdownStream.cs:42`、`:64`、`:83`）；`_pacer` 与文档只在界面线程推进；`Completed` 在流（含平滑积压）全部结束时触发（:119）。
- `BlockView.Update` 契约（`BlockView.cs:25`）：`internal`，只由 `DocumentPresenter` / `StackBlockView.UpdateChildren` 调用；必须按阅读顺序对每个文本片段调用一次 `KeyFor(context, topKey, ref segment)`，顺序要与 `Selection/BlockText.Segments` 的遍历一致（`BlockView.cs:11` 注释、`Selection/BlockText.cs:32`）；`LastSegment()` 必须返回深度优先的最后一个片段（否则渐显 / 光标不显示）。
- `DocumentPresenter` 的回调契约：`insert(index, view)` / `remove(index, view)` 的 `index` 是**视图列表中的位置**，`MarkdownView` 用它同步 `LogicalChildren` / `VisualChildren`（`MarkdownView.cs:50`）；`Apply` 返回“有无变化”，实际 `MarkdownView` 未使用返回值。
- `TextSegmentView`：`Content`（:53）、`Layout`（:59）、`Key`（:48）、`IsDocumentTail`（:51）、`LinkClicked` / `ImageClicked` / `ComponentClicked` 冒泡事件（:62 / :72 / :82）。
- `BlockViewFactory.CanReuse`（`BlockViewFactory.cs:40`）：类型一致即可复用；最近一次上下文变化（`contextChanged`）会触发 `DocumentPresenter.Restyle`（`DocumentPresenter.cs:83`），例如新增图表渲染器后代码块要变成图表视图。

## How to extend
- To add a 新的块视图（新块类型或新的内建渲染方式）：先在 `BlockViewFactory.ViewTypeFor`（`BlockViewFactory.cs:91`）加分支并在 `CreateBuiltIn`（:22）映射到新视图类，再在 `CanReuseBuiltIn`（:56）加 `view switch` 分支（用于复用 / 类型切换），最后按 `BlockView`（`BlockView.cs:13`）实现 `OnUpdate` / `MeasureOverride` / `ArrangeOverride` / `Render`，容器类视图继承 `StackBlockView`（:61）复用 `UpdateChildren`。漏掉 `CanReuseBuiltIn` 的后果：`CanReuse` 落到 `_ => false`，每次快照更新都重建视图——输入焦点、块状态与滚动位置丢失（`BlockStateStore` 只能保住 `Get/Set` 过的东西），并且 `DocumentPresenter.Restyle` 每帧替换全部视图。若新块类型要参与选区，还要同步 `Selection/BlockText.Segments` 的遍历分支与其顺序一致。
- To add 插件贡献的块视图（HTML 组件 / 按语言替换的围栏视图）：走 `context.Plugins.TryGetBlockComponent` / `FindFenceView`（`BlockViewFactory.cs:13`、`:15`），视图的 `Update` 复用规则在 `CanReuse` 的组件 / 围栏分支（:43、:47）；`ComponentBlock` 无注册时回落到 `Plugins.ComponentContainerView`。
- To add 一个新的 `Md.*` 主题资源键：① 在 `MarkdownResourceKeys`（`MarkdownResourceKeys.cs:7`）加常量；② 在 `MarkdownStyle` 加 `required` 属性（`MarkdownStyle.cs:17-37`）并在 `Resolve` 里 `Get<T>(键, 默认值)`（:95-121，需要 `Color→IBrush` 自动转换就用 `IBrush` 类型参数）；③ 在 `Themes/StreamMarkTheme.axaml` 的 Light（:9）与 Dark（:22）字典各加一条（不随明暗变化的键放主题字典之外，如 `Md.BlockSpacing` :37）；④ 在宿主映射里（Arc：`src/TKCode.Arc.Markdown/Content/Markdown/Markdown.cs:s_aliases`）选择性地把值写成 `MarkdownResourceAlias`，并加测试。漏掉 ③ 的后果：`TryFindResource` 失败、静默用 `MarkdownStyle` 里的硬编码回退值（`MarkdownStyle.cs:72`），宿主改了资源也不生效。测试模板见 `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/StyleKeysTests.cs:12`（断言回退值与覆盖生效）。
- To add 一种新的行内对象渲染：在 `InlineTextSource.CreateObjectRun`（`TextSegmentView.cs:867`）加分支，新 run 继承 `DrawableTextRun`（参照 `InlineRuns.cs:18-167`），并让 `FirstDifference` 的 `Objects` 比较能识别它的偏移变化（:204）。

## Invariants and traps
- **整篇替换（`MarkdownView.RefreshMarkdown`）的应用顺序不能换**：先 `SnapshotEdit.Compute` → `BlockStateStore.Remap`（视图 `Update` 时按**新** key 读状态）→ 选区搬迁（`SelectionRemap`）→ `DocumentPresenter.ApplyEdit` → 等 `LayoutUpdated` 后恢复阅读锚点。刷新期间 `ApplyLatest` 在 `Latest.Version < _refreshVersion` 时直接返回（保持旧画面），所以 A → B → C 之间的快照不会上屏；`Markdown` 赋值仍走清空路径并递增 `DocumentId` — see `src/TKCode.Arc.StreamMark/Avalonia/MarkdownView.cs`（`RefreshMarkdown` / `ApplyEdited`）、`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/RefreshMarkdownTests.cs`。
- **任务复选框只发「请求」，不改状态**：`ListItemBlockView` 命中复选框（绘制时记下的 `_taskBox`，外扩 3px）或聚焦后按空格 / 回车，调用 `RenderContext.TaskToggle(item)`，由 `MarkdownView.RequestTaskToggle` 触发 `TaskToggleRequested`；勾选状态永远只由快照决定，宿主不改原文就不会变。复选框一带要画透明填充才会命中（没有绘制内容的区域不参与命中）。只有 `IsTaskInteractionEnabled` 且快照 `IsComplete` 时才发请求；`BlockOverlay` 的副本上下文故意不带 `TaskToggle`（只读）；`TranscriptPanel` 暂未接入 — see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/ContainerViews.cs`（`ListItemBlockView`）、`src/TKCode.Arc.StreamMark/Avalonia/MarkdownView.cs`（`RequestTaskToggle`）、`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/TaskInteractionTests.cs`。
- `_runCache.InvalidateFrom(i)` 必须传入**差异点所在段落的起点**，不是差异点本身 — 缓存以换行符分隔的段落为单位、按段落起点存放整段的整形结果，从差异点失效会复用旧段落（连同段末换行），流式每次追加都变成一次硬换行；回归测试 `SmoothStreamWrapTests` — see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:164`。
- `TextRunCache` 是 Avalonia 12 的 `[Unstable]` 预览 API — 只允许在 `TextSegmentView` 内使用，并用 `#pragma warning disable CS0618` 限定范围；换别的文件用要重新评估（同一快照里 `TextLayout` 也是预览重载）— see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:30`。
- 中文粗体的回退字体必须按**本段字的字重 / 字形**自行解析 — Avalonia 为缺字找回退字体时优先复用“上一段”的回退字体，不检查字重，且 `Typeface(..., Bold)` 在只有中粗（600）的字族上会落回常规字重；所以 `CreateCharacters`（:762）先判主字体是否覆盖字符，缺字才 `TryMatchCharacter`，`RealFace` 只在 `FontFamily.FamilyTypefaces` 列出的**真实字面**里选（粗体取离请求最近的 ≥600 字重，其次最粗的 ≥500，再没有就用常规；回退字体一律用正体，不请求斜体）。**不要用 `Typeface(…, 字重).GlyphTypeface` 去试探不存在的字重 / 字形**：Avalonia 12 会合成粗体 / 斜体，合成时把整份字体文件复制三份常驻内存（Apple Color Emoji 约 600MB，回归测试 `InlineStyleProbeTests.FallbackRuns_UseRealFaces_NotSyntheticCopies`）— see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:759`。
- 回退字体必须与取 run 的起点和匹配历史无关 — 逐行排版（`SegmentLayout`）从行首重新取 run，所以 `CreateCharacters` 按整段缺字文字的第一个字（向前找到 span / 内嵌对象边界或主字体能覆盖的字为止）匹配回退字体，而不是按行首的字（Windows 上“。”与“首”可能匹配到不同字体）；Avalonia 的 `TryMatchCharacter` 又会优先复用之前匹配到的字体（匹配过韩文后“。”会落到 Apple SD Gothic Neo），所以 `MatchFallback` 用静态字典记住每个字第一次的结果。任何一处依赖起点或历史，流式结果都会与整段排版不同（回归测试 `IncrementalLayoutTests.StreamedMixedScripts_MatchFullLayout`）— see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:MatchFallback`。 已知风险：缓存的是 Avalonia 某一刻的结果，`FontCollectionBase.TryMatchInCache` 会优先返回已加载过的字体，第一次匹配若恰好拿到按字重拆开的字体族（Windows 上的“Yu Gothic UI Semibold”），之后就一直用它，渲染成粗体 + 合成斜体（2026-10-17 由测试里探测 GlyphTypeface 触发，2026-10-19 由表格用例的粗体中文表头把该族缓存好后再次触发）。`RealFace` 因此不再请求斜体：Windows 上该族的 `FamilyTypefaces` 会列出并不存在的斜体字面，请求它会让 Avalonia 取同族已缓存的最近字面合成斜体并缓存在这个族里，连正体请求也可能取到它；事后加载检查（`Simulated`）本身就会创建这份合成字面，所以也不可行。不要在渲染路径或测试里按字重 / 字形探测 `GlyphTypeface`；彻底消除历史依赖需要按文字类别的确定回退表（`IFontManagerImpl` 在引用程序集里不可访问）。
- 删除线自绘，不用 `TextDecorations.Strikethrough` — 部分字体（含无头环境的回退字体）缺少删除线度量，装饰线不显示；`DrawStrikethrough`（:469）按每段 `HitTestTextRange` 在 54% 高度画线 — see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:966`。
- 只有文档末尾片段（`IsDocumentTail`，由 `DocumentPresenter.UpdateTail` 或 `TranscriptPanel` 设置）才渐显并绘制光标 — 片段不再是末尾时，已开始的渐显由 `TailFade` 的定时器在结束后清理；`UpdateTail` 每次 `Apply` 对尾部 `InvalidateVisual()` 只重录片段自身的小块绘制（光标在流结束后消失靠它） — see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/DocumentPresenter.cs:102`。
- 渐显层（渲染线程）只能画字形与矩形：内嵌对象、带装饰线（链接下划线）或 RTL 的文字段不参与渐显，渐显起点移到其后（`TailFade.SkipUnfadeable`）；行视觉的矩形裁剪与渐显层的批次矩形在同一横坐标衔接，改其中一边要同步另一边 — see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/TailFade.cs`。
- 片段的命中测试靠 `Render` 里的透明矩形：行视觉不是输入元素，命中测试会跳过它们及其子树；去掉透明矩形会让链接、图片点击与选区落点失效。
- 固定行高会把整行裁到该高度 — 行内有更高的对象（图片、大公式）时 `SegmentLayout` 只把这一行用自然行高重新排版 — see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/SegmentLayout.cs`（`Overflows`）。
- 上下文（`RenderContext`）的身份就是“变化”信号 — 样式 / 服务 / 主题变化必须**新建** `RenderContext` 实例（`MarkdownView.CreateContext`，`MarkdownView.cs:169`），`BlockView.Update` 与 `TextSegmentView.Set` 都靠 `ReferenceEquals` 判断 `contextChanged`；原地修改样式对象不会触发任何重排或 `Restyle` — see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/BlockView.cs:27`、`src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs:11`。
- 片段编号必须在构建期按阅读顺序分配，并与 `BlockText.Segments` 的遍历顺序一致 — 选区坐标是 `(Entry, BlockKey, Segment, Offset)`，顺序错位会造成选区串段 — see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/BlockView.cs:43`、`src/TKCode.Arc.StreamMark/Avalonia/Selection/TextPoint.cs:33`。
- `KeyFor` 每次调用都会 `segment++`，因此目标块类型变化（例如代码块 → 图表）时不能“两边都先建 key 再选分支”，否则片段号错位 — see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/BlockView.cs:43`。
- 代码块分段的横向滚动各自独立（已知限制）— `CodeTextView._offsetX` 是每个视图自己的状态，`SetChunk` 只切窗口不共享偏移 — see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/CodeBlockView.cs:73`。
- `CodeBlockState` 只在界面线程访问（`BlockStateStore` 的约定），后台 `RunHighlight` 回投时必须校验 `ReferenceEquals(session, _session)`，否则重建后的块会收到过期着色 — see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/CodeBlockState.cs:142`。
- 图片句柄是借用关系：`ReleaseUnusedImages` 只在 `_fullRebuild`（或关缓存）时执行，部分复用缓存的排版不释放，离开可视树则全部 `Dispose` 并重排 — see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:311`、`:239`。
- 代码块的 `CodeTextView` 是“整个代码块”的选区片段，`Segments()` 只返回它；`DiagramBlockView` 显示图表时 `Segments()` 返回空（图表不可选） — see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/DiagramBlockView.cs:237`。

## Tests
- 无头宿主：`tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:24`。`HeadlessApp.BuildAvaloniaApp`（:26）用 `AppBuilder.Configure<TestApp>().UseSkia().UseHarfBuzz().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })`，`TestApp.Initialize`（:15）设 `RequestedThemeVariant = Dark` 并加载 Fluent 主题 + `StreamMarkTheme`，程序集级 `[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]`（:8）。
- 用法：`HeadlessApp.Run(async () => {...})`（:55）在界面线程跑异步测试体；需要返回值的用 `HeadlessApp.Session.Dispatch(..., CancellationToken.None)`（`HeadlessApp.Session` :34）；`HeadlessApp.Settle(rounds = 5)`（:65）跑 `Dispatcher.UIThread.RunJobs()` + `AvaloniaHeadlessPlatform.ForceRenderTimerTick()` + `await Task.Delay(20)` 让异步更新落定；`HeadlessApp.Fixture(name)`（:76）读 Core 测试的输出 fixture（csproj 把 `tests/TKCode.Arc.StreamMark.Tests/Fixtures/streaming/*.md` 复制到输出目录）。
- 截图 / 快照：`HeadlessApp.Save(topLevel, name)`（:44）写 PNG；目录取环境变量 `STREAMMARK_SNAPSHOT_DIR`，默认测试输出目录下的 `snapshots/`（:37；CI 设为 `${{ github.workspace }}/artifacts/snapshots`，`.github/workflows/ci.yml:78`）。快照是给人看的产物，测试只断言结构（例如控件类型、`Bounds.Height`）。
- 命名模式：测试在 `tests/TKCode.Arc.StreamMark.Tests/Avalonia/`（子目录 `Rendering/`、`Transcript/`、`Images/`、`Math/`、`Mermaid/`、`Plugins/`、`Highlighting/`，另有根下的 `ThemeSnapshotTests.cs`），xUnit v3 + `[Fact]` / `[Theory]`，类名与被测组件同名，测试整体挂 `HeadlessApp.Run`。
- 本模块最相关的用例：`Rendering/MarkdownViewSnapshotTests.cs`（混合文档渲染各块类型、流式只更新尾部且冻结视图不被重建）、`Rendering/InlineStyleProbeTests.cs`（行内样式 + 中文粗体字重回归）、`Rendering/SelectionAndFadeTests.cs`（跨块选区复制、渐显与光标）、`Rendering/StyleKeysTests.cs`（`Md.*` 回退与覆盖）、`Rendering/RunCacheBenchTests.cs`（缓存开关下的排版耗时对比，断言只查内容正确）、`Rendering/HtmlRenderingTests.cs`、`Transcript/SmoothStreamWrapTests.cs`（硬换行回归）、`Rendering/IncrementalLayoutTests.cs`（逐行排版与整段排版逐步一致、混排回退字体与起点 / 历史无关、追加成本不随段落长度增长、渐显在合成层并按时结束）。
- 运行：仓库口径是 `dotnet test TKCode.Arc.slnx`（`README.md:91`；CI 用同样命令）。只跑一个文件里的测试可用 `dotnet test tests/TKCode.Arc.StreamMark.Tests/Avalonia/StreamMark.Avalonia.Tests.csproj --filter "FullyQualifiedName~SmoothStreamWrapTests"`——仓库脚本与 CI 里没有这种调用（unverified，本次未运行）。

## Related flows
- [streaming-token-to-view](../flows/streaming-token-to-view.md)
- [stream-completion-reparse](../flows/stream-completion-reparse.md)
- [theme-resolution](../flows/theme-resolution.md)
- [raw-html-render](../flows/raw-html-render.md)
- [text-selection-copy](../flows/text-selection-copy.md)
