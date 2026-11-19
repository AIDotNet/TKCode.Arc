# StreamMark 架构

StreamMark 是面向 AI 对话场景的高性能流式 Markdown 渲染库：Avalonia 12 / .NET 10，全程 NativeAOT 安全，不依赖 WebView。

## 数据流水线

```
网络 token ──▶ ① StreamPacer（节奏控制） ──▶ ② StreamingDocument.Append（只追加的源码缓冲）
                                                   │  后台单线程，合并请求
                                                   ▼
                                     ③ StreamSegmenter：在原始源码上逐行扫描，找安全冻结边界
                                                   │
             新冻结片段：remend（仅转义）+ Markdig，解析一次，永不再动 ◀──┤
                                                   ▼
                                     ④ 尾部：remend（完整补全）+ Markdig；
                                        尾部只是未闭合代码块时走快速路径，不调用解析器
                                                   ▼
                                     ⑤ MarkdigMapper → 不可变块模型（key = 源码起始偏移）
                                                   ▼
                                     ⑥ DocumentSnapshot（冻结前缀共享 + 尾部）
                                                   │  界面线程每帧最多取一次 Latest
                                                   ▼
                                     ⑦ SnapshotDiff：跳过共享前缀，按 key 归并 → Insert / Update / Remove
                                                   ▼
                                     ⑧ 视图层：块视图（自绘文本 / 代码 / 表格 / 公式 / 图表 / 容器）
                                        放在 MarkdownView（单文档）或 TranscriptPanel（虚拟化会话）中
```

流结束（`Complete()`）时整篇重解析一次并开启新的一代，修正跨片段的引用定义、脚注和列表松紧；差异比对保证内容没变的块不会重建。

## 分层与包

并入 TKCode.Arc 之后（2026-11-19），Core 与 Avalonia 两层合成一个包，命名空间不变：

| 包（Arc 仓库的项目） | 命名空间 | 依赖 | 职责 |
|---|---|---|---|
| `TKCode.Arc.StreamMark`（`Core/`） | `StreamMark.Core.*` | Markdig | 源码缓冲、remend 移植、切分器、块模型、快照与差异、节奏控制器。不引用 Avalonia 类型，全部可单元测试 |
| `TKCode.Arc.StreamMark`（`Avalonia/`） | `StreamMark.Avalonia.*` | Avalonia | 自绘块视图、`TranscriptPanel` 虚拟化面板、`SelectionModel`、默认主题（`Md.*` 资源键） |
| `TKCode.Arc.StreamMark.TextMate` | `StreamMark.Highlighting.TextMate` | TextMateSharp | `ISyntaxHighlighter` 实现：按行缓存语法状态，只着色新到达的行 |
| `TKCode.Arc.StreamMark.RaTeX` | `StreamMark.RaTeX` | RaTeX 原生库 | `[LibraryImport]` 调用 RaTeX C ABI，源生成 JSON 解析显示列表，KaTeX 字体嵌入 |
| `TKCode.Arc.StreamMark.Mermaid` | `StreamMark.Mermaid` | Mermaider、Svg.Skia | 代码块闭合后后台渲染 Mermaid |

宿主主题不在这里：Arc 的 `Markdown` 控件（`TKCode.Arc.Markdown`）把 `Md.*` 指向 Arc Token。原来的 `StreamMark.Theme.AtomUI` 已删除。

## 关键不变量

1. **源码只追加**（`Reset()` 开启新的一代）。块的 key 是它在原始源码中的起始偏移，因此跨重解析稳定。
2. **冻结边界单调前进**，冻结的块对象在同一代内永不改变；快照共享冻结前缀，差异比对直接跳过。
3. **冻结片段独立解析的结果与整篇解析一致**（引用定义除外，见 streaming-rules.md）。测试 `StreamingEquivalenceTests` 在每一步都验证这一点。
4. **偏移永远指原始源码**：remend 会插入转义字符，`OffsetMap` 把修补后文本中的位置映射回原始位置。
5. **界面线程不做解析**：解析在后台单线程合并执行，界面层每帧最多应用一次差异。
6. **宿主整篇替换文本（编辑器预览）不改变只追加的约定**：`MarkdownView.RefreshMarkdown` 仍是 `Load`（新一代），但视图不清空：用 `SnapshotEdit` 把新旧两份快照按内容对齐，前面增删文字让后面的块整体平移时，视图原地更新到新的源码位置，块状态、选区与阅读位置按对齐结果搬过去。`SnapshotDiff` 只按源码起点归并，对这种情况会把后面所有块当成删除再插入，所以刷新另走 `SnapshotEdit`。
7. **阅读锚点与源码位置的身份是视图级的 `DocumentId`**，不是快照的 `Generation`（`Complete()` 时也会递增）。块级精度：`TextPoint.Offset` 是片段纯文本偏移，不是源码偏移，源码位置只到块的 `Range`。

## 线程模型

- `StreamingDocument.Append/Complete/Reset` 可在任意线程调用。
- 解析在线程池上的单个逻辑工作者中执行；进行中有新文本只打脏标记，完成后再处理最新文本（中间版本跳过）。
- `SnapshotReady` 在工作线程触发；`Latest` 任意线程可读。
- `StreamPacer` 非线程安全，应在界面线程的动画帧回调中驱动：`MarkdownStream` 由显示它的视图交给窗口（`SetFrameSource`），之后随显示帧推进；未挂载或窗口不出帧时退回 16ms 计时器，保证流总能结束。

## NativeAOT

- 所有库项目 `IsAotCompatible=true`，裁剪 / AOT 警告视为错误。
- 正则一律 `[GeneratedRegex]`；JSON 一律源生成；主题以 `x:Class` 编译加载；不使用反射绑定。
- CI 的 `aot-smoke` 任务发布 Gallery 并运行 `--smoke`，要求 0 个 IL 警告。

## 视图层（StreamMark.Avalonia）

| 组件 | 说明 |
|---|---|
| `TextSegmentView` | 一个文本片段 = 一个 `SegmentLayout`（逐行的 `TextLine`，与 `TextLayout` 结果一致）。自定义 `ITextSource` 提供行内样式；公式、图片、任务复选框是 `DrawableTextRun`（基线对齐）。流式追加只从差异点所在行的前一行重新排版，每行一个 `SegmentLineVisual` 单独录制，所以只有变化的行重新录制、产生脏区。片段自身只画可命中区域、选区 / 查找高亮与流式光标；删除线随行自绘 |
| `TailFade` | 文档末尾片段的逐字渐显，在渲染线程完成：界面线程在排版变化时把渐显中的字形与各批出现时间交给合成层的自定义视觉（`CompositionCustomVisualHandler`），所在行视觉用矩形裁掉渐显部分；之后每帧只由渲染线程重绘这一小块，界面线程不参与。渐显结束后恢复行的完整绘制 |
| 块视图 | `TextBlockView`（段落 / 标题 / HTML）、`CodeBlockView`（逐行排版缓存 + 后台增量高亮 + 复制 / 换行）、`TableBlockView`（自定义列宽布局，不用 `Grid`）、`MathBlockView`（保留上一次成功排版）、`DiagramBlockView`（闭合后后台渲染、缓存、图表 / 源码切换）、列表 / 引用 / 提示块 / 容器 / 脚注 / 定义列表 |
| `DocumentPresenter` | 把快照差异应用到顶层块视图：同类型复用，类型变化才替换；`ApplyEdit` 应用 `SnapshotEdit`（宿主整篇替换文本） |
| `MarkdownView` | 单文档、非虚拟化、高度随内容；`Markdown` / `Document` / `Stream` 三种输入 |
| `TranscriptPanel` / `TranscriptView` | 会话：所有消息摊平成行（Markdown 顶层块各一行，宿主内容各一行），只实例化视口 ±600px 内的行；按块类型估算未测量行高；滚动锚定；贴底跟随；`MaxContentWidth` 居中列 |
| `SelectionModel` / `SelectionController` | 选区坐标 `(消息, 顶层块 key, 片段, 偏移)`，与视图无关；单击 / 拖拽（自动滚动）/ 双击选词 / 三击选段 / Shift 扩展；⌘C 纯文本、⌘⇧C Markdown 源码、⌘A 全选；被虚拟化移出的块也能复制 |
| `MarkdownStream` | 节奏控制器 + 流式文档 + 渐显时间记录，任意线程 `Push` / `Complete` |
| `MarkdownServices` | 可继承附加属性：`IMathRenderer`、`ISyntaxHighlighter`、`IDiagramRenderer`、`IImageLoader` |

### 扩展实现

| 包 | 实现 |
|---|---|
| `StreamMark.RaTeX` | `RaTeXMathRenderer`：C ABI 排版（线程安全、LRU 缓存含错误），显示列表在界面线程转为 `GlyphRun` + 几何；KaTeX 字体从嵌入资源加载为私有 `GlyphTypeface`；默认颜色用哨兵色区分，保留 `\color`；CJK / emoji 回退系统字体 |
| `StreamMark.Highlighting.TextMate` | `TextMateSyntaxHighlighter`：每个明暗主题一个 Registry + 锁（TextMateSharp 2.0.4 非线程安全）；会话按行缓存语法状态，追加只着色新行，中间修改在结束状态一致处停止；冷启动超时行重试 |
| `StreamMark.Mermaid` | `MermaidDiagramRenderer`：Mermaider → SVG → 按配色方案（默认复刻 mermaid.js 官方主题）重涂子图与连线标签 → 解析 `var()` / `color-mix()` 为字面颜色 → Svg.Skia `SKPicture` → 自定义绘制操作在 Avalonia 的 Skia 画布上矢量回放；传入的前景 / 底色先由 `DiagramBlockView` 叠到真实底色上 |

### 主题解析

`MarkdownStyle.Resolve(control)` 一次性读取 `Md.*` 资源：值为 `MarkdownResourceAlias` 时从控件本身查找目标键（宿主 token 可能只在窗口 / 主题作用域可见），`Color` 自动转画刷；未设置 `Md.FontFamily` / `Md.FontSize` 时沿用控件继承的 `TextElement` 字体，行高取字号 1.6 倍。主题变体或资源变化时重新解析并下发到所有视图。

## 插件体系（第二阶段）

```
IMarkdownPlugin.Configure(MarkdownPluginBuilder)
   ├─ Profile（Core）：AddComponent(ComponentSpec) / AddMarkdigExtension / MapBlock<T> / MapInline<T> / AddRemendHandler
   ├─ UseMath / UseHighlighter / AddDiagramRenderer
   ├─ AddBlockComponent(name, factory)        块级 HTML 组件视图
   ├─ AddInlineComponent(name, renderer)      行内组件（自闭合 → DrawableTextRun；成对 → 样式 + 点击）
   ├─ AddFenceView(language, factory)         按语言替换代码块视图（默认闭合后）
   └─ AddBlockAction(descriptor)              代码块头部操作
MarkdownServices { Plugins = [...] } → Registry（不可变）→ Profile（文档解析用）
```

- **组件语法只用白名单 HTML 标签**。`HtmlComponentBlockParser` 排在 `HtmlBlockParser` 之前，块级组件是 `ContainerBlock`，内容按 Markdown 解析，可跨空行、同名嵌套；以行内组件开头的行交给段落解析器（否则会被当作第 7 类 HTML 块）。`HtmlComponentInlineParser` 排在 `AutolinkInlineParser` 之前，成对组件是未闭合的 `ContainerInline`，遇到结束标签时闭合。
- **流式**：未闭合的块级组件在文档末尾自然结束，未闭合的行内成对组件包住其后的全部文字，写了一半的标签由 remend 去掉，因此不需要额外的补全处理器。切分器把块级组件当作不透明容器（`HtmlKind.Container`，带嵌套深度），内部不冻结。
- **模型**：`ComponentBlock(Name, Attributes, Children)`；`InlineObjectKind.Component`（自闭合）；成对组件用 `InlineSpan.ComponentIndex` 指向 `InlineContent.Components`。内置的 `kbd / sub / sup / mark / u` 直接映射为样式，`img` 映射为图片对象。
- **内置组件**（`BuiltInComponentsPlugin`，总是启用，可被同名注册覆盖）：`<details>/<summary>`、`<think>` 折叠块，`<Citation id/>` 引用标签，`<cite href>` 引用文字。
- **块状态**：`BlockStateStore` 每个文档一份，按 (块 key, 状态名) 保存折叠、换行、图表 / 源码切换等，视图回收或重建后保留。
- **文档方言**：宿主创建文档时用 `services.CreateStream()` / `CreateEntry(text)` / `CreateDocument()`（或手动传 `services.Profile`），否则插件注册的标签不会被解析；方言不匹配时 `MarkdownServices.Diagnostic` 报告一次。`MarkdownView` 自建的文档在服务变化时自动换方言。插件配置失败抛出带插件名的 `PluginConfigurationException`，覆盖记录在 `PluginRegistry.Diagnostics`。

## 原始 HTML（阶段 3.5）

1. **解析与映射**：`MarkdigMapper.MapSiblings` 把 Markdig 的 `HtmlBlock`，连同夹在其中的 Markdown 兄弟块，交给 `HtmlTreeBuilder`；构建器容错，支持隐式闭合和分多次输入。`HtmlMapper` 再把树转换为块。
   - 标题、列表、表格、引用、代码、分隔线、`details`、定义列表复用已有的块；
   - 盒子元素映射为 `HtmlBoxBlock(Tag, HtmlBoxStyle, HtmlTextStyle, Children)`；
   - 行内内容由 `HtmlInlineBuilder` 生成；空白按 HTML 规则折叠，CJK 字符之间的换行不变成空格。
2. **CSS**：`CssStyle` 解析表现属性和 `style` 子集，结果限制在合理范围内。行内的 CSS 写入 `InlineContent.TextStyles`，由 `InlineSpan.TextStyleIndex` 引用。
3. **Markdown 段落中的行内 HTML**（`<strong>…</strong>` 可以跨软换行）：`InlineBuilder` 维护一个标签栈，栈中的样式、链接和文字样式作用于其间所有文字。
4. **渲染**：
   - `HtmlBoxBlockView` 负责外边距、边框、内边距、背景、圆角和宽度；窄于可用宽度时按外层对齐方式摆放。
   - 它通过 `RenderContext.WithHtml` 把文字样式和背景传给子块；子块上下文会缓存，避免每次更新都重新排版。
   - `TextSegmentView.Set` 应用继承的对齐、字号、粗细和颜色；`PropertiesFor` 应用行内文字样式；`TableBlockView` 在外层居中时整体居中。
   - 作者颜色经 `HtmlPaint` 按 `MarkdownServices.HtmlColors` 处理。
