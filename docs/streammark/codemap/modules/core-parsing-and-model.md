---
module: core-parsing-and-model
sources: ["src/TKCode.Arc.StreamMark/Core/Parsing/**", "src/TKCode.Arc.StreamMark/Core/Model/**", "src/TKCode.Arc.StreamMark/Core/Html/**"]
commit: 44a8f5a
updated: 2026-10-16
---
# 核心解析、块模型与原始 HTML

把 Markdown 文本（已被 remend 修补过的版本）经 Markdig 解析后转换成不可变的块模型：`MarkdigMapper` 负责 Markdig 树 → `MarkdownBlock`，`HtmlGroup`/`HtmlTreeBuilder`/`HtmlMapper` 负责原始 HTML 的容错解析与拼接到块模型，`CssStyle` 提供内联 style 与旧式表现属性的子集。方言本身（Markdig 管线、白名单组件、插件映射、remend 处理器）由 `MarkdownProfile` 打包并不可变化。
**不负责：** 冻结边界探测与流式组装（`StreamSegmenter` / `DocumentComposer`，`src/TKCode.Arc.StreamMark/Core/Streaming/`，见 `core-streaming`）；未完成语法的修补（`Remend.Complete`，`src/TKCode.Arc.StreamMark/Core/Remend/`，见 `remend-and-offsetmap`）；块视图布局与绘制、行内渲染（`src/TKCode.Arc.StreamMark/Avalonia/Rendering/`，见 `avalonia-text-rendering`）；插件注册表装配与组件视图工厂（`src/TKCode.Arc.StreamMark/Avalonia/Plugins/`，见 `avalonia-plugin-system`）；图片的实际下载/缓存（`src/TKCode.Arc.StreamMark/Avalonia/Images/`，本模块只产出 `InlineObjectKind.Image`）；代码高亮（`src/TKCode.Arc.StreamMark/Core/Highlighting/`）。

## Entry points
| Symbol | File | Role |
|---|---|---|
| `MarkdownProfile.Default` | `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdownProfile.cs:Default` | 默认方言静态单例，首次访问即构建；不传 profile 时的兜底（`DocumentComposer.cs:44`、`StreamingDocument.cs:39`） |
| `MarkdownProfileBuilder` | `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdownProfile.cs:MarkdownProfileBuilder` | 唯一的方言装配点：`AddMarkdigExtension` / `AddComponent` / `MapBlock<T>` / `MapInline<T>` / `AddRemendHandler` / `Build` |
| `ComponentSpec` + `ComponentKind` + `ComponentContent` | `src/TKCode.Arc.StreamMark/Core/Parsing/ComponentSpec.cs:ComponentSpec` | 白名单 HTML 组件的声明（块级 / 行内成对 / 自闭合），插件在此扩标签；块级组件的 `Content` 为 `Markdown`（默认）或 `Raw`（内容原样保留） |
| `StreamMarkPipeline.Default` | `src/TKCode.Arc.StreamMark/Core/Parsing/StreamMarkPipeline.cs:Default` | `MarkdownProfile.Default.Pipeline` 的别名；仓库内唯一调用方是基准测试（`tools/streammark/Benchmarks/StreamingBenchmarks.cs:28`） |
| `BackslashMathExtension` | `src/TKCode.Arc.StreamMark/Core/Parsing/BackslashMathExtension.cs:BackslashMathExtension` | 公开扩展：`\(…\)` / `\[…\]` 数学定界符 |
| `MarkdigMapper.MapDocument` | `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdigMapper.cs:MapDocument` | `internal`，整篇 Markdig 文档 → 块模型；`DocumentComposer.Parse` 是唯一生产调用方（`DocumentComposer.cs:115`） |
| `OffsetMap.Create` / `ToSource` | `src/TKCode.Arc.StreamMark/Core/Parsing/OffsetMap.cs:Create` | `internal`，修补后位置 → 原始源码位置，块区间与 key 都经它落回原文 |
| `SourceText.ToLocation` / `ToOffset` | `src/TKCode.Arc.StreamMark/Core/Model/SourceText.cs:SourceText` | public，源码偏移 ↔ 行 / 列（0 起始）的约定：偏移 = UTF-16 下标，`\r\n` 占 2 个单位且不归一化，换行符为 `\n` / `\r\n` / 单独 `\r`；与 `SourceRange` 同一套单位 |
| `HtmlElements` | `src/TKCode.Arc.StreamMark/Core/Html/HtmlTree.cs:HtmlElements` | 支持元素集合（Block / Inline / Void / Containers），是"未知元素按源码显示"的判定依据 |
| `HtmlTreeBuilder` / `HtmlMapper` | `src/TKCode.Arc.StreamMark/Core/Html/HtmlTree.cs:HtmlTreeBuilder`、`src/TKCode.Arc.StreamMark/Core/Html/HtmlMapper.cs:HtmlMapper` | public，但当前只有 `MarkdigMapper` 调用（`MarkdigMapper.cs:86`、`:125`）；测试直接驱动构建器（`tests/TKCode.Arc.StreamMark.Tests/Core/Html/HtmlTests.cs:190`） |
| `CssStyle` | `src/TKCode.Arc.StreamMark/Core/Html/CssStyle.cs:CssStyle` | public，`FromAttributes` / `Apply` / `TryParseLength` / `TryParseColor` / `ImageSize`；渲染层也读它的钳制常量（`TextSegmentView.cs:929`） |

## Key types and functions
| Symbol | File | Role |
|---|---|---|
| `MarkdownBlock` | `src/TKCode.Arc.StreamMark/Core/Model/Blocks.cs:MarkdownBlock` | 全部块的基类：`Key => Range.Start`（顶层块的身份）、`ComputeFingerprint()` 用于差异比较（消费点 `src/TKCode.Arc.StreamMark/Core/Streaming/SnapshotDiff.cs:38`） |
| 块类型族 | `src/TKCode.Arc.StreamMark/Core/Model/Blocks.cs` | `ParagraphBlock` / `HeadingBlock` / `CodeBlock` / `MathBlock` / `QuoteBlock` / `AlertBlock` / `ListBlock` / `TableBlock` / `ThematicBreakBlock` / `HtmlBlock` / `HtmlBoxBlock` / `FootnotesBlock` / `DefinitionListBlock` / `ComponentBlock` / `CustomContainerBlock` |
| `InlineContent` + `InlineSpan` | `src/TKCode.Arc.StreamMark/Core/Model/InlineContent.cs:InlineContent`、`:InlineSpan` | 扁平文本 + 不重叠样式区间；`Links` / `Components` / `TextStyles` 三张表由 `LinkIndex` / `ComponentIndex` / `TextStyleIndex` 引用 |
| `InlineObject` | `src/TKCode.Arc.StreamMark/Core/Model/InlineContent.cs:InlineObject` | 嵌在文本中的对象（数学 / 图片 / 任务框 / 自闭合组件），以 U+FFFC `Placeholder` 占位，`Offset` 是占位字符位置 |
| `HtmlBoxStyle` / `HtmlTextStyle` / `CssColor` / `Edges` | `src/TKCode.Arc.StreamMark/Core/Model/HtmlStyles.cs:HtmlBoxStyle`、`:HtmlTextStyle` | 盒子（外边距 / 内边距 / 背景 / 边框 / 宽度）与可继承文字样式；`HtmlTextStyle.Over` 是继承与覆盖的唯一实现 |
| `Fingerprint` | `src/TKCode.Arc.StreamMark/Core/Model/Fingerprint.cs:Fingerprint` | 64 位 FNV-1a，`MarkdownBlock.AddContent` 各实现往里喂字段 |
| `MarkdigMapper.Map` | `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdigMapper.cs:Map` | Markdig 块节点的 `switch` 映射；插件 `BlockMappers` 先跑（`:165`），返回 `null` 表示不映射 |
| `MarkdigMapper.MapSiblings` + `HtmlGroup` | `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdigMapper.cs:MapSiblings`、`:HtmlGroup` | 把 `HtmlBlock` 与夹在其间的 Markdown 兄弟块合并成一个块；含未知元素时整组退回原样 |
| `MarkdigMapper.InlineBuilder` | `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdigMapper.cs:InlineBuilder` | 深度优先遍历行内树，产出扁平文本；同时是 `InlineMapContext` 的插件接口实现（`:646-659`） |
| `HtmlTreeBuilder.Feed` | `src/TKCode.Arc.StreamMark/Core/Html/HtmlTree.cs:Feed` | 容错 HTML 词法 + 树构建，可分多次喂；处理注释 / `<! …>` / 半截标签 / `script` 等原始文本元素，并按 HTML 规则隐式闭合 |
| `HtmlMapper.Block` | `src/TKCode.Arc.StreamMark/Core/Html/HtmlMapper.cs:Block` | HTML 树 → 块：标题 / 列表 / 表格 / 引用 / `pre` / `hr` / `details` / `dl` 复用既有块，其余成 `HtmlBoxBlock` |
| `HtmlInlineBuilder` | `src/TKCode.Arc.StreamMark/Core/Html/HtmlMapper.cs:HtmlInlineBuilder` | HTML 行内节点 → `InlineContent`：空白折叠、`StyleFor` 样式、`<a>` / `<img>` / `<picture>` / `<q>` 特例 |
| `CssStyle.FromAttributes` / `Apply` | `src/TKCode.Arc.StreamMark/Core/Html/CssStyle.cs:FromAttributes`、`:Apply` | 表现属性优先于 `style`（`FromAttributes` 先属性后 `Apply`）；`Apply` 的 `switch` 就是 CSS 子集的全部范围 |
| `HtmlTagReader.TryRead` | `src/TKCode.Arc.StreamMark/Core/Parsing/ComponentSpec.cs:HtmlTagReader` | 极简标签读取（名称 / 属性 / 引号 / 自闭合），块解析器、行内解析器、HTML 树构建器三处共用 |

## Dependencies
- Uses: `remend-and-offsetmap` via 同程序集直接调用 — `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdigMapper.cs:448` 比较 `Remend.Remend.IncompleteLinkUrl` 决定链接是否按"未完成"渲染；`OffsetMap` 本身不依赖 remend
- Uses: `core-streaming` via 无（方向相反）— 本模块不引用 `Streaming/**`；`MarkdownProfile.SegmenterContainers`（`MarkdownProfile.cs:SegmenterContainers`）是给切分器用的输出，不是输入
- Uses: 同程序集 `Model` 与 `Parsing` 之间的循环命名空间引用 — `src/TKCode.Arc.StreamMark/Core/Html/HtmlTree.cs:120` 调用 `Parsing.HtmlTagReader`，而 `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdigMapper.cs:125`、`MarkdownProfile.cs:54` 调用 `Html.*`（同一程序集，编译无碍，但拆文件时要注意）
- Used by: `core-streaming` via 同程序集直接调用 — `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:Parse`（`Remend.Complete` → `Markdown.Parse` → `MarkdigMapper.MapDocument`）、`:47`（`SegmenterContainers`）、`:118`（直接构造 `Model.CodeBlock`）
- Used by: `avalonia-plugin-system` via 同程序集直接调用 — `src/TKCode.Arc.StreamMark/Avalonia/Plugins/IMarkdownPlugin.cs:Profile`（暴露 `MarkdownProfileBuilder`）、`:AddBlockComponent` / `:AddInlineComponent` 自动 `Profile.AddComponent(new ComponentSpec(...))`、`src/TKCode.Arc.StreamMark/Avalonia/Plugins/PluginRegistry.cs:32` 调 `Build()`
- Used by: `avalonia-text-rendering` via 消费块模型 / 直接调用 — `src/TKCode.Arc.StreamMark/Avalonia/Rendering/BlockViewFactory.cs:Create`（`ComponentBlock` → 插件工厂）、`src/TKCode.Arc.StreamMark/Avalonia/Rendering/HtmlBoxBlockView.cs:OnUpdate`（读 `HtmlBoxBlock`）、`src/TKCode.Arc.StreamMark/Avalonia/Rendering/RenderContext.cs:WithHtml`、`src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:929`（读 `CssStyle.MinFontSize/MaxFontSize`）
- Used by: `tests` via 同程序集 + `InternalsVisibleTo` — `src/TKCode.Arc.StreamMark/Core/StreamMark.Core.csproj` 对 `StreamMark.Core.Tests` 开放 internal，故 `DocumentComposer.Parse` / `MarkdigMapper` / `OffsetMap` 可直接测

## State it owns
- 唯一的全局可变状态是 `MarkdownProfile.Default` 静态单例（`MarkdownProfile.cs:Default`），构造后不可变；其余 `MarkdownProfile` 实例由调用方持有（`PluginRegistry.Profile`、`StreamingDocument.Profile`）。
- `MarkdownProfile._segmenterContainers` 是**无锁惰性字段**（`MarkdownProfile.cs:52-57`）：并发构造文档（多线程各自 `new DocumentComposer`）时可能重复计算，且 `ImmutableArray<T>` 赋值不是原子操作 —— 理论上有撕裂读风险，代码里没有任何同步（未验证，无测试覆盖）。
- `ComponentTable` 每个 profile 一份，只读字典 + `BlockNames`（Markdown 内容）+ `RawBlockNames`（原样内容）（`ComponentSpec.cs:ComponentTable`）。
- `HtmlTreeBuilder` 的开放元素栈 `_open`（`HtmlTree.cs:66`）是**每次 `new` 一份**的临时状态，`MarkdigMapper.HtmlGroup`（`MarkdigMapper.cs:86`）为每组兄弟块新建一个；不跨解析复用。
- 源码文本本身不在这里：真相是 `StreamingDocument._source`（`src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:20`）；块模型是派生物，`DocumentSnapshot` 持有冻结前缀 + 尾部（`src/TKCode.Arc.StreamMark/Core/Streaming/DocumentSnapshot.cs:11`）。
- 没有缓存、没有持久化、没有静态可变表（除了上面的惰性字段）。

## Contracts it exposes
- `ComponentKind` 三种语义，逐一对应解析器行为：`Block`（行首开始标签、内容按 Markdown、可跨空行可嵌套）、`InlinePair`、`SelfClosing` — `src/TKCode.Arc.StreamMark/Core/Parsing/ComponentSpec.cs:6-14`。
- 内置白名单固定 11 个：`details` / `summary` / `think`（Block）、`kbd` / `sub` / `sup` / `mark` / `u` / `cite`（InlinePair）、`citation` / `img`（SelfClosing）— `ComponentSpec.cs:20-33`；其中 `kbd|sub|sup|mark|u` 不产生组件，而是映射为 `InlineStyle`（`ComponentSpec.cs:StyleFor`，消费点 `MarkdigMapper.cs:481`）。
- 原样组件（`ComponentContent.Raw`）：解析成叶子块 `HtmlRawComponentBlock`（`HtmlComponentExtension.cs`，`ProcessInlines = false`，续行先 `GoToColumn(ColumnBeforeIndent)` 保留缩进），映射为 `ComponentBlock { Raw, IsOpen }`、`Children` 为空（`MarkdigMapper.cs:MapRawComponent`）。`Raw` 逐行经 `OffsetMap.SourceText` 从原文取，看不到 remend 的转义 / 补全与解析前的遮盖。结束标签必须在行尾；不支持同名嵌套。所有块级组件都带 `IsOpen`（没见到自己的结束标签，包括随外层块结束的情况），两个新字段都在指纹里。
- 重名覆盖：后登记的 `ComponentSpec` 覆盖先登记的（`MarkdownProfile.cs:79`）；标签名一律不区分大小写（`ComponentSpec.cs:50`）。
- HTML 支持元素表（单一来源）：`Block` / `Inline` / `Void` / `Containers` 四个集合 —— `HtmlTree.cs:12`、`:19`、`:25`、`:29`；`IsKnown` = Block ∪ Inline（`HtmlTree.cs:32`）。`Containers` 只用于切分器（未闭合就不冻结），不参与渲染判断。
- 管线注册顺序（`MarkdownProfile.cs:105-128`）：先 `Use…` 系列（PipeTables+GFM 规则、仅 Strikethrough 的 EmphasisExtras、TaskLists、AutoLinks、Footnotes、AlertBlocks、DefinitionLists、CustomContainers、Mathematics、CjkFriendlyEmphasis、EmojiAndSmiley(无颜文字)、AutoIdentifiers(GitHub)、`BackslashMathExtension`），再 `HtmlComponentExtension`（`:122`），最后按登记顺序 append 插件扩展（`:123-126`）。`StreamMarkPipeline.cs:9-11` 是"只开删除线"的唯一解释处。
- 映射契约：`MapBlock<T>` 返回 `null` 交给后续/内置映射（`MarkdownProfile.cs:85`）；`MapInline<T>` 返回 `true` 表示已消费该节点（`:92`）；插件映射生效顺序在全部内置 `switch` 之前（`MarkdigMapper.cs:165`），行内亦然（`:403`）。
- 块身份契约：顶层块 `Key = Range.Start`，源码只追加所以跨重解析稳定（`Blocks.cs:Key`）；`ComputeFingerprint` **不含** `Range`（`Blocks.cs:ComputeFingerprint`）；同一 `Generation` 内前一快照的冻结前缀在同一对象里（`DocumentSnapshot.cs:55`）。
- `InlineContent` 契约：`Text` 是扁平字符串，`Spans` 不重叠、按起点排序（`InlineContent.cs:98-106` 的类注释）；`InlineObject.Placeholder = '￼'`（U+FFFC，`InlineContent.cs:95`）；`TextStyles` / `Components` 缺省为空数组（`:111`、`:114`）。
- CSS 子集（`CssStyle.Apply` 的 `switch` 即全集）：`text-align`、`color`、`background`/`background-color`、`font-size`、`font-weight`、`font-style`、`margin` 及四向、`padding` 及四向、`border` / `border-color` / `border-width` / `border-radius`、`width` / `max-width`（`width` / `max-width` 对 `img` 不生效）— `CssStyle.cs:77-201`。表现属性：`align`、`center` 标签、`bgcolor`、`width`、`<font color size>` — `CssStyle.cs:25-51`。数值钳制：间距 `MaxSpacing=200`、字号 8–64、边框宽 0–8、圆角 0–64、宽度 ≤4096/≤100%（`CssStyle.cs:12-14`、`:157`、`:184`、`:309`）。
- HTML 元素特例：`hidden` 属性或 `style` 内 `display:none` → 整棵子树不渲染（`HtmlMapper.cs:IsHidden`、`HtmlInlineBuilder.cs:323`）；`<pre><code class="language-x">` → `CodeBlock` 并取语言（`HtmlMapper.cs:Code`、`:LanguageOf`）；`<q>` 补中文引号（`HtmlMapper.cs:377`）；`<small>`/`<big>` 默认 `FontScale` 0.85 / 1.2（`HtmlMapper.cs:356-363`）；HTML 中的连续空白折叠且 CJK 换行不加空格（`HtmlMapper.cs:AppendCollapsed`、`:IsWide`）。

## How to extend
- **新增白名单组件（ComponentSpec + 解析器顺序 + 映射 + 视图）：** ① 在 `ComponentSpec.BuiltIn` 加一项（`ComponentSpec.cs:BuiltIn`），或让插件走 `MarkdownProfileBuilder.AddComponent`（`MarkdownProfile.cs:77`）；② 若插件用 `MarkdownPluginBuilder.AddBlockComponent` / `AddInlineComponent`（`IMarkdownPlugin.cs:66`、`:77`）会自动补 ①；③ 块级不用写映射，`HtmlComponentBlock` → `ComponentBlock` 已有分支（`MarkdigMapper.cs:188`），行内成对若想变成"样式"而非组件，必须在 `ComponentSpec.StyleFor` 加分支（`ComponentSpec.cs:StyleFor`）；④ 视图：块级 `BlockComponents`、行内 `IInlineComponentRenderer`（`IMarkdownPlugin.cs:105`），内置实现见 `BuiltInComponentsPlugin.Configure`（`src/TKCode.Arc.StreamMark/Avalonia/Plugins/BuiltInComponentsPlugin.cs:Configure`）。**缺少 ④ 时块级组件仍解析成功，但渲染为默认容器 `ComponentContainerView`（`BlockViewFactory.cs:13`），行内自闭合组件渲染为空、成对组件只剩默认样式**。解析器顺序本身不用改：`HtmlComponentBlockParser` 先于 `HtmlBlockParser`、`HtmlComponentInlineParser` 先于 `AutolinkInlineParser` 是针对整张白名单表生效的（`HtmlComponentExtension.cs:22`、`:26`）。参考测试 `tests/TKCode.Arc.StreamMark.Tests/Core/Parsing/ComponentTests.cs:PluginComponentAndMappers_AreApplied`。
- **新增块映射：** 首选插件侧 `MapBlock<TNode>`（`MarkdownProfile.cs:85`）——它拿到的 `MapContext` 提供 `MapChildren` / `MapInlines` / `RangeOf`（`MarkdownProfile.cs:145-149`），返回的块若 `Range` 为 `default`，`Map` 会自动补上（`MarkdigMapper.cs:169`）。要改内置行为则动 `MarkdigMapper.Map` 的 `switch`（`MarkdigMapper.cs:172`）。**漏掉 `MapBlock<T>` 注册、又没在 switch 加分支时，自定义节点会落到兜底分支**：`ContainerBlock` → `QuoteBlock`，`LeafBlock` → `ParagraphBlock`（`MarkdigMapper.cs:191-192`），自定义语义静默丢失且看起来"能渲染"。
- **新增行内映射：** `MapInline<TNode>`（`MarkdownProfile.cs:92`）在 `InlineBuilder.Visit` 里对所有插件映射器轮询、命中即 return（`MarkdigMapper.cs:403-413`）；新行内样式要加 `InlineStyle` 位（`InlineContent.cs:7`），新对象要加 `InlineObjectKind`（`InlineContent.cs:38`）——渲染层按 `Kind` / `Value` 分派（`TextSegmentView.cs:898`）。**只加 `MapInline` 不写视图分支时，对象以占位字符进文本但不显示**。
- **CSS 子集新增属性：** 在 `CssStyle.Apply` 的 `switch` 加 `case`（`CssStyle.cs:77`），值一律经 `TryParseLength` / `TryParseColor` / `Spacing` 并钳制；样式字段加到 `HtmlBoxStyle` 或 `HtmlTextStyle`（`HtmlStyles.cs:28`、`:58`），继承类属性同时要在 `HtmlTextStyle.Over` 里接上（`HtmlStyles.cs:Over`），否则嵌套时静默丢值。最后在渲染层消费（盒子：`HtmlBoxBlockView`；行内：`RenderContext.WithHtml` + `TextSegmentView` 的 `TextStyles` 查表，`TextSegmentView.cs:920`）。image 专用尺寸走 `CssStyle.ImageSize`（`CssStyle.cs:ImageSize`），它把 style 中的宽高规范化为 `width`/`height` 属性（`HtmlMapper.cs:AddImage`）。

## Invariants and traps
- **解析器插入顺序是白名单机制本身** — `HtmlComponentBlockParser` 必须在 `HtmlBlockParser` 之前、`HtmlComponentInlineParser` 必须在 `AutolinkInlineParser` 之前，否则 `<details>` 之类会先被 CommonMark 第 6/7 类 HTML 块或 autolink 吃掉 — see `src/TKCode.Arc.StreamMark/Core/Parsing/HtmlComponentExtension.cs:22`、`:26`。两处都用 `Contains<T>()` 守卫，重复注册是安全的（`:20`、`:24`）。
- **插件扩展只能 append 在最后** — `MarkdownProfileBuilder.Build` 先加 `HtmlComponentExtension`，再按登记顺序加 `_extensions`（`MarkdownProfile.cs:122-126`），所以插件无法用 `AddMarkdigExtension` 把自己插到内置解析器之前，需要更高优先级时必须自己在 `Setup` 里 `Insert`（参照 `BackslashMathExtension.cs:21`、`:26`）。
- **反斜杠数学必须排在转义解析器之前** — 否则 `\(` 被当成转义的 `(`；块解析器插在 `BlockParsers[0]`、行内解析器插在 `InlineParsers[0]`，注释里写明了原因 — see `src/TKCode.Arc.StreamMark/Core/Parsing/BackslashMathExtension.cs:23`、`:26`。
- **`CanInterrupt` 的调用时机：Markdig 对每个候选块解析器都会问一次（即使当前块不是段落），所以不能在里面做重活，也不能只判断"当前行是不是标签"** — `HtmlComponentBlockParser.CanInterrupt` 先放行非段落块，只在段落里要求"是块级组件开始标签"才允许打断（`HtmlComponentExtension.cs:75-77`）；`BackslashMathBlockParser.CanInterrupt` 直接复用 `IsOpeningLine`（`BackslashMathExtension.cs:107`、`:133`）。返回 `false` 的效果是：段落中的该行不会被这个解析器切成新块。Markdig 内部的具体触发点未在本仓库中验证 (unverified)。
- **未知元素让整组按源码显示，且是"整组"而不是"那一个"** — `HtmlGroup.Finish` 一旦 `Tree.HasUnknownElements` 就把整组的 `_fallback`（各 `HtmlBlock` 与已映射的 Markdown 块）原样输出（`MarkdigMapper.cs:118-124`）；`HasUnknownElements` 只在 `Open` / `Close` 遇到 `HtmlElements` 之外的标签时置位（`HtmlTree.cs:182`、`:255`）。后果：一个未知标签会让同组里正常渲染的 `<div>` 一起变回源码。
- **`script` / `style` 绝不执行，也绝不内联** — 它们不在 `HtmlElements.Block` / `Inline`（`HtmlTree.cs:12`、`:19`），因此走"未知元素 → 按源码显示"；`HtmlTreeBuilder.Feed` 把它们的内容当原始文本吞掉（`IsRawText` 含 `script`/`style`/`textarea`/`title`，`HtmlTree.cs:132-140`、`:157`）。渲染层没有任何 WebView / iframe / 脚本执行路径：`src/` 下搜不到 `WebView`、`iframe`（(unverified) 仅 grep 层面确认，无编译期保证）。测试断言见 `tests/TKCode.Arc.StreamMark.Tests/Core/Html/HtmlTests.cs:UnknownElements_KeepSource_CommentsHidden`。
- **注释不显示，纯注释组被丢弃** — 构建器跳过 `<!-- … -->`（`HtmlTree.cs:102-109`），`HtmlGroup.Finish` 在"树里没有元素且全是段落"时用 `IsCommentOnly` 过滤掉注释块（`MarkdigMapper.cs:126-131`、`:146`）。
- **偏移必须指原始源码，而 `OffsetMap` 只认两类差异** — 插入 `\` / U+200B，以及末尾改写；中途遇到其它差异就置 `_divergence`，其后位置一律钳到原文末尾附近（`OffsetMap.cs:42`、`:52`）。所有 `Range` 都经 `RangeOf` 换算（`MarkdigMapper.cs:RangeOf`），其中未闭合围栏块被特判为"延伸到被解析文本末尾"（`:201-205`，因为 Markdig 给的 `Span` 只覆盖开头一行）。
- **流式半截标签被丢弃而非按源码显示** — `Feed` 遇到 `TryRead` 失败且本行再没有 `>` 时，把剩余文本整段丢掉等下一批（`HtmlTree.cs:144-150`）；这是 `Streaming_PartialHtml_NeverShowsRawTags` 守护的性质（`tests/TKCode.Arc.StreamMark.Tests/Core/Html/HtmlTests.cs:202`）。
- **一行只有自闭合/行内组件时，块解析器主动交回段落解析器** — 否则 CommonMark 会把它当第 7 类 HTML 块按原文显示（`HtmlComponentExtension.cs:94-99`）。
- **任务复选框从段落文本里"提升 + 摘除"** — 映射器把首个行内对象识别为 `TaskCheckbox` 后提升为 `ListItemBlock.Task`，并同步平移/裁剪文本、span 与后续对象偏移（`MarkdigMapper.cs:241-244`、`:StripLeadingObject`）；改动这个逻辑必须同时处理占位字符后的那个空格。
- **任务标记的源码区间 `ListItemBlock.TaskMarker`** — 来自 Markdig `TaskList` 行内节点的 `Span`，经 `OffsetMap` 回写成源码偏移（`MarkdigMapper.MapList`），共 3 个字符（`[ ]` / `[x]` / `[X]`），不是任务项时长度为 0。它进了 `ComputeFingerprint`，但记的是**相对列表项起点**的位置：列表整体平移指纹不变，标记在项内位置变了（项目符号后多空格）指纹才变；漏进指纹的话，完成后标记区间变化不会产生 Update，视图会拿着旧区间向宿主发请求 — `tests/TKCode.Arc.StreamMark.Tests/Core/Parsing/TaskMarkerTests.cs`。
- **多个顶层 HTML 元素合并成一个块时用 `Tag="#fragment"`** — 顶层块的 `Key` 是源码起点、必须唯一，所以不能把第一个子元素的 tag 当身份（`MarkdigMapper.cs:139-142`、`Model/Blocks.cs:HtmlBoxBlock`）。
- **软换行两侧都是 CJK 时不插空格** — 两处独立实现，Markdown 侧 `InlineBuilder.SoftBreak`（`MarkdigMapper.cs:567-573`）、HTML 侧 `HtmlInlineBuilder.AppendCollapsed`（`HtmlMapper.cs:469-507`）；字符区间是复制的，改一处不会同步另一处。
- **属性除了读值不要依赖哈希** — `ComponentAttributes.GetHashCode` 只返回 `Items.Length`（`InlineContent.cs:79`），`Equals` 才是逐项比较（`:77`）；拿它当字典键会严重退化。
- **`HtmlBoxBlockView` 的父级对齐是继承来的** — 盒子宽度小于可用宽度时按 `Context.Inherited.Align` 摆放（`src/TKCode.Arc.StreamMark/Avalonia/Rendering/HtmlBoxBlockView.cs:MeasureOverride`），所以 `CssStyle` 里把 `Align` 显式写成 `TextAlign.Inherit` 的写法（`CssStyle.cs:25` 的 `and not TextAlign.Inherit`、`HtmlMapper.cs:364`）是有意义的，不是死代码。
- **`MarkdownProfile.SegmenterContainers` 是 HTML 与组件两个来源的并集** — 白名单块级组件 ∪ `HtmlElements.Containers`（`MarkdownProfile.cs:52-55`），切分器据此不在容器内部冻结（`src/TKCode.Arc.StreamMark/Core/Streaming/StreamSegmenter.cs:415`）；新增一个"能包住 Markdown 的容器元素"时要同时改 `HtmlElements.Block` 和 `Containers`，否则流式下会在容器内部冻结导致内容被切碎。

## Tests
- 位置：`tests/TKCode.Arc.StreamMark.Tests/Core/Parsing/MapperTests.cs`（Markdig → 块模型）、`tests/TKCode.Arc.StreamMark.Tests/Core/Parsing/ComponentTests.cs`（组件解析、插件注册、切分容器）、`tests/TKCode.Arc.StreamMark.Tests/Core/Html/HtmlTests.cs`（HTML 树、CSS、HTML 与 Markdown 拼接）；Avalonia 侧有端到端 DOM 断言 `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/HtmlRenderingTests.cs`。
- 命名/写法：每个文件有私有 `Parse(string)` 辅助方法，直接调 `internal` 的 `new DocumentComposer(profile).Parse(markdown, 0, RemendOptions.Default)`（`MapperTests.cs:10`、`ComponentTests.cs:11`、`HtmlTests.cs:11`）——这就是把解析入口当单测面的惯用写法；`HtmlTests.cs` 另有 `Flatten` 递归展开块树（`HtmlTests.cs:14`）。
- 运行：整套 `dotnet test TKCode.Arc.slnx`（`README.md:91`）。只跑一个文件用 `dotnet test tests/TKCode.Arc.StreamMark.Tests/Core/ --filter "FullyQualifiedName~MapperTests"`（同 `HtmlTests` / `ComponentTests`）；该过滤命令未在本仓库文档中出现，未实际运行验证 (unverified)。
- 守护性测试值得注意的两个：`Streaming_PartialHtml_NeverShowsRawTags` 逐字符前缀扫描全文（`HtmlTests.cs:202`），`Segmenter_DoesNotFreezeInsideContainer` 验证容器内不冻结（`ComponentTests.cs:101`）。

## Related flows
- [streaming-token-to-view](../flows/streaming-token-to-view.md)
- [stream-completion-reparse](../flows/stream-completion-reparse.md)
- [raw-html-render](../flows/raw-html-render.md)
- [plugin-registration](../flows/plugin-registration.md)

四个 flow 都经过本模块（流式与完成时的解析都在 `DocumentComposer.Parse` / `Finish`，`src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:111`、`:94`）。但 `docs/streammark/codemap/flows/` 目录在本次快照中不存在，四个页面均无法打开 (unverified)。
