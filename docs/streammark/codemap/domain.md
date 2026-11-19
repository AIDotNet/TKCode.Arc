# 领域概念

StreamMark 没有数据库、没有网络 API、没有配置文件：所有状态都在进程内，唯一落盘的东西是图片磁盘缓存与测试截图。下表把业务名词映射到代码、界面与流程。

| 概念 | 一句话定义 | 定义位置 | 持久化 | 出现界面 | 相关流程 |
|---|---|---|---|---|---|
| 流式文档 | 只追加的源码缓冲 + 后台单工作者 + 快照发布 | `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs` › `StreamingDocument` | 无（进程内） | 所有视图的输入 | [streaming-token-to-view](flows/streaming-token-to-view.md)、[stream-completion-reparse](flows/stream-completion-reparse.md) |
| 冻结边界 | 之后再来什么文本都不会改变前文解析结果的安全切点，只前进 | `src/TKCode.Arc.StreamMark/Core/Streaming/StreamSegmenter.cs` › `StreamSegmenter.FrozenEnd` | 无 | 不可见（表现为「已完成的块不再重排」） | [streaming-token-to-view](flows/streaming-token-to-view.md) |
| 快照与代 | 冻结前缀共享 + 尾部的不可变视图；`Complete()` / `Reset()` 开启新的一代 | `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentSnapshot.cs` › `DocumentSnapshot.Generation`、`SharesFrozenWith` | 无 | 差异应用后反映为块视图变化 | [stream-completion-reparse](flows/stream-completion-reparse.md) |
| 块 key | 块在原始源码中的起始偏移，跨重解析与代稳定 | `src/TKCode.Arc.StreamMark/Core/Model/Blocks.cs` › `MarkdownBlock.Key` | 无 | 键与渲染指纹决定视图复用与块状态 | [streaming-token-to-view](flows/streaming-token-to-view.md) |
| 渲染指纹 | 64 位 FNV-1a 内容指纹，代替深比较（不含 Range） | `src/TKCode.Arc.StreamMark/Core/Model/Fingerprint.cs` › `Fingerprint`、`Blocks.cs` › `ComputeFingerprint` | 无 | 指纹不变则不发 Update | [stream-completion-reparse](flows/stream-completion-reparse.md) |
| 差异操作 | 两个快照之间的 Insert / Update / Remove（按 key 归并） | `src/TKCode.Arc.StreamMark/Core/Streaming/SnapshotDiff.cs` › `DiffKind`、`DiffOp` | 无 | 由 `DocumentPresenter` / `TranscriptPanel` 消费 | [streaming-token-to-view](flows/streaming-token-to-view.md) |
| remend | 未完成 Markdown 的修复器；尾部完整补全，冻结片段与整篇只做转义修正 | `src/TKCode.Arc.StreamMark/Core/Remend/Remend.cs` › `Remend.Complete` | 无 | 表现为「流式中的 `**粗体` 立刻有样式」 | [streaming-token-to-view](flows/streaming-token-to-view.md) |
| 偏移映射 | 把转义修正后文本中的位置映射回原始源码 | `src/TKCode.Arc.StreamMark/Core/Parsing/OffsetMap.cs` › `OffsetMap.Create` | 无 | 保证流式与最终解析的块 key 一致 | [streaming-token-to-view](flows/streaming-token-to-view.md) |
| 文档方言 | 不可变的 Markdig 管线 + 白名单组件 + 映射器 + remend 处理器 | `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdownProfile.cs` › `MarkdownProfile` | 无 | 宿主用 `services.Profile` 创建文档 | [plugin-registration](flows/plugin-registration.md) |
| 白名单组件 | 可被当作组件解析的 HTML 标签及其种类（块 / 行内成对 / 自闭合） | `src/TKCode.Arc.StreamMark/Core/Parsing/ComponentSpec.cs` › `ComponentSpec`、`ComponentKind` | 无 | `<details>`、`<think>`、`<Citation>`、插件自定义标签 | [plugin-registration](flows/plugin-registration.md) |
| HTML 拼接组 | 一个 HTML 块连同夹在其中的 Markdown 兄弟块合并成的块；key 取第一个 HTML 块起点 | `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdigMapper.cs` › `MapSiblings` | 无 | README 风格居中对齐块 | [raw-html-render](flows/raw-html-render.md) |
| HTML 盒子块 | 携带盒子样式（外边距/边框/背景/圆角/宽度）+ 可继承文字样式 + 子块 | `src/TKCode.Arc.StreamMark/Core/Model/Blocks.cs` › `HtmlBoxBlock` | 无 | `HtmlBoxBlockView` | [raw-html-render](flows/raw-html-render.md) |
| 行内内容 | 一个文本片段的文字 + 三张索引表（链接 / 组件 / 文字样式），对象用 U+FFFC 占位 | `src/TKCode.Arc.StreamMark/Core/Model/InlineContent.cs` › `InlineContent`、`InlineObject` | 无 | `TextSegmentView` 与图片 / 公式 / 复选框运行 | [raw-html-render](flows/raw-html-render.md) |
| 节奏控制 | 把到达速率平滑成显示速率（滑动速率 + 积压/目标落后 + MaxLag 上限 + 字素簇对齐） | `src/TKCode.Arc.StreamMark/Core/Pacing/StreamPacer.cs` › `StreamPacer` | 无 | 逐字渐显的手感 | [streaming-token-to-view](flows/streaming-token-to-view.md) |
| 块状态 | 每个文档一份、按 (块 key, 状态名) 保存的界面状态（折叠、换行、图表/源码、diff 模式） | `src/TKCode.Arc.StreamMark/Avalonia/Plugins/BlockStateStore.cs` › `BlockStateStore` | 无（随文档重建清空） | 折叠、换行、图表/源码切换跨回收存活 | [transcript-virtualization-scroll](flows/transcript-virtualization-scroll.md) |
| 代码块共享状态 | 行分段 + 增量高亮会话 + 复制/换行等状态，供回收后重建的视图共用 | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/CodeBlockState.cs` › `CodeBlockState` | 无 | `CodeBlockView` | [streaming-token-to-view](flows/streaming-token-to-view.md) |
| 选区坐标 | 与视图无关的选择位置 `(消息, 顶层块 key, 片段, 偏移)` | `src/TKCode.Arc.StreamMark/Avalonia/Selection/TextPoint.cs` › `TextPoint`、`SelectionModel` | 无 | 高亮、⌘C / ⌘⇧C | [text-selection-copy](flows/text-selection-copy.md) |
| 片段 | 块内可选中/可复制的单位（内容 / 源码 / 分隔符 / 前缀），编号在构建期按阅读顺序分配 | `src/TKCode.Arc.StreamMark/Avalonia/Selection/BlockText.cs` › `BlockText.Segment`、`TextPoint.cs` › `SegmentKey` | 无 | 选区边界与复制拼接 | [text-selection-copy](flows/text-selection-copy.md) |
| 会话行 | 会话中一个可虚拟化的单位：Markdown 顶层块一行，宿主内容控件一行 | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs` › `Row`、`EntryState` | 无 | `TranscriptPanel` 的 Extent / 行实例化 | [transcript-virtualization-scroll](flows/transcript-virtualization-scroll.md) |
| 代码分段 | 超过 200 行的代码块按 128 行一段拆成多行；横向滚动各段独立（已知限制） | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs` › `Chunk`、`ChunkThresholdLines` | 无 | 会话中的长代码块 | [transcript-virtualization-scroll](flows/transcript-virtualization-scroll.md) |
| 延迟条目 | 先占位、解析推迟到接近视口时才做的条目 | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptEntry.cs` › `FromTextDeferred`、`EnsureParsed` | 无 | 会话历史加载 | [transcript-virtualization-scroll](flows/transcript-virtualization-scroll.md) |
| 图片租约 | 引用计数句柄：缓存淘汰不等于释放，最后一个句柄释放才 Dispose 位图 | `src/TKCode.Arc.StreamMark/Avalonia/Images/ImageMemoryCache.cs` › `ImageLease` | 内存缓存（进程内） | 图片占位符与查看器 | [raw-html-render](flows/raw-html-render.md) |
| 解码档位 | 按显示宽度 × 缩放向上取整到 256 像素档，用于缓存键与内存上限 | `src/TKCode.Arc.StreamMark/Avalonia/Images/ImageDecoder.cs` › 档位计算 | 无 | 图片清晰度与内存占用 | — |
| 图片同意三态 | 远程图片需要用户同意：待同意 / 已同意 / 被阻止 | `src/TKCode.Arc.StreamMark/Avalonia/Images/ImageLoadResult.cs` › `ImageLoadResult` | 无 | 占位符上的操作按钮 | — |
| 插件注册表 | 不可变注册表：方言 + 服务（公式/高亮/图表）+ 组件/围栏视图 + 块操作 | `src/TKCode.Arc.StreamMark/Avalonia/Plugins/PluginRegistry.cs` › `PluginRegistry` | 无 | 解析与视图创建的查找表 | [plugin-registration](flows/plugin-registration.md) |
| 样式快照 | 从 `Md.*` 资源解析出的不可变 `MarkdownStyle`（含别名解析与缺省推导） | `src/TKCode.Arc.StreamMark/Avalonia/Rendering/MarkdownStyle.cs` › `MarkdownStyle` | 无（来自 Avalonia 资源） | 所有块视图的字体、颜色、间距 | [theme-resolution](flows/theme-resolution.md) |
| 资源别名 | 只保存「目标资源键」的间接层，让宿主 token 在控件自身作用域被解析 | `src/TKCode.Arc.StreamMark/Avalonia/MarkdownResourceAlias.cs` › `MarkdownResourceAlias` | 无 | 宿主令牌下发 | [theme-resolution](flows/theme-resolution.md) |
| 作者颜色模式 | HTML 作者颜色的处理：可读（对比度不足换主题色）/ 精确 / 忽略 | `src/TKCode.Arc.StreamMark/Avalonia/Extensions/MarkdownServices.cs` › `HtmlColorMode`、`Rendering/HtmlPaint.cs` | 无 | 原始 HTML 渲染 | [raw-html-render](flows/raw-html-render.md) |
| 显示列表协议 | RaTeX 原生库返回的排版结果（em 单位、Y 向下、基线 y=height，扁平条目便于前向兼容） | `src/TKCode.Arc.StreamMark.RaTeX/DisplayList.cs` › `DisplayList` | 无 | 公式块 | — |
| 语言别名 | 代码围栏语言到 TextMate 语法的三级解析（别名表 → language id → 扩展名） | `src/TKCode.Arc.StreamMark.TextMate/LanguageAliases.cs` › `LanguageAliases` | 无 | 代码块着色 | — |
