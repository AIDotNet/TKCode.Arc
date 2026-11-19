---
module: avalonia-selection
sources: ["src/TKCode.Arc.StreamMark/Avalonia/Selection/**"]
commit: 44a8f5a
updated: 2026-10-16
---
# 选区模块（文本选择与复制）

用一组与视图无关的坐标 `(消息 Entry, 顶层块 key, 片段序号, 片段内偏移)` 表示选区，由 `SelectionController` 处理指针与键盘交互，复制时**从块模型重新取文本**（`BlockText`），而不是从已实例化的视图取。因此被虚拟化回收的块仍在选区内、仍可复制。

**不负责：** 文本排版与命中几何（`TextSegmentView`、`CodeTextView`、`MathBlockView` 在 `avalonia-text-rendering`）；会话行摊平、实例化与滚动（`TranscriptPanel` 在 `avalonia-transcript`）；块模型与源码区间（`MarkdownBlock.Key` 在 `core-parsing-and-model`）；选区高亮的实际绘制（各片段视图自己按 `SelectionModel.GetSegmentRange` 画矩形）。

## Entry points
| Symbol | File | Role |
|---|---|---|
| `SelectionController` | src/TKCode.Arc.StreamMark/Avalonia/Selection/SelectionController.cs:41 | 全部入口。构造时向宿主控件挂 PointerPressed / PointerMoved / PointerReleased / KeyDown；对外提供 `SelectAll` / `CopyAsync` / `GetSelectedText` / `GetSelectedMarkdown` |
| `SelectionModel` | src/TKCode.Arc.StreamMark/Avalonia/Selection/SelectionModel.cs:7 | 只存 anchor / active 两个 `TextPoint` 与 `Changed` 事件；视图据此重绘高亮 |
| `ISelectionHost` | src/TKCode.Arc.StreamMark/Avalonia/Selection/SelectionController.cs:13 | 宿主契约：`RealizedSegments` / `EntryCount` / `GetSnapshot` / `GetSource` / `OnSelectionDrag` / `Plugins` |
| `ISelectableSegment` | src/TKCode.Arc.StreamMark/Avalonia/Selection/ISelectableSegment.cs:7 | 片段视图契约：`Key` / `Text` / `View` / `HitTest` / `GetCharacterBounds` |
| `BlockText` | src/TKCode.Arc.StreamMark/Avalonia/Selection/BlockText.cs:11 | 唯一"块模型 → 片段列表/纯文本"的提取器，复制与视图编号共用 |
| `DocumentNavigation.ResolveSourceOffset` / `.SourceRangeOf` | src/TKCode.Arc.StreamMark/Avalonia/Selection/DocumentNavigation.cs:71、:107 | 源码偏移 ↔ 渲染位置的块级换算（纯函数，输入是快照）。偏移 → `SourceTarget`（含 `TextPoint` 与块区间）；`TextPoint` → 承载该片段的最深块的源码区间 |
| `MarkdownView.ScrollToSourceOffset` / `.GetSourceRange` / `.GetSelectionSourceRange` | src/TKCode.Arc.StreamMark/Avalonia/MarkdownView.cs:257、:276、:280 | 宿主入口：按源码偏移滚动（返回 `SourceNavigationResult`，状态说明为什么没动）、渲染点 / 选区反查源码区间 |
| `MarkdownView.CaptureAnchor` / `.RestoreAnchor` / `.DocumentId` | src/TKCode.Arc.StreamMark/Avalonia/MarkdownView.cs:295、:328、:110 | 阅读锚点：外层 `ScrollViewer` 可视区顶部的字符 + 像素位移；绑定视图级 `DocumentId`，跨文档不恢复 |

## Key types and functions
| Symbol | File | Role |
|---|---|---|
| `TextPoint` | Selection/TextPoint.cs:8 | 选区坐标 `(Entry, BlockKey, Segment, Offset)`，`IComparable` 字典序即阅读顺序 |
| `SegmentKey` / `SegmentKey.At` | Selection/TextPoint.cs:33 / :35 | 片段身份；`At(offset)` 生成该偏移处的 `TextPoint` |
| `SelectionModel.Set` / `.Extend` / `.Clear` | Selection/SelectionModel.cs:35 / :46 / :60 | 修改选区，变化时触发一次 `Changed`（值未变则不触发） |
| `SelectionModel.Range` / `.IsEmpty` | Selection/SelectionModel.cs:21 / :15 | 派生：anchor/active 归一化为 `(Start, End)`；`IsEmpty` 含"anchor==active"（即单击无选中） |
| `SelectionModel.GetSegmentRange` | Selection/SelectionModel.cs:72 | 派生查询：某片段内的 `[start,end)` 字符区间，供视图画高亮；`Segment < 0` 直接返回 null |
| `SelectionController.OnPressed` | Selection/SelectionController.cs:58 | 单击定位 / 双击选词 / 三击选段 / Shift 扩展的判定 |
| `SelectionController.OnMoved` / `.OnReleased` | Selection/SelectionController.cs:99 / :113 | 拖拽扩展（含自动滚动回调）与指针捕获释放 |
| `SelectionController.OnKeyDown` | Selection/SelectionController.cs:123 | ⌘C / ⌘⇧C 复制、⌘A 全选、Esc 清除 |
| `SelectionController.FindPoint` | Selection/SelectionController.cs:269 | 宿主坐标 → `(片段, TextPoint)`：先 `InputHitTest` 找祖先片段，否则取纵向最近片段 |
| `SelectionController.WordAt` | Selection/SelectionController.cs:332 | 双击选词：字母/数字/`_` 连续段；落在非词字符上只选 1 个字符 |
| `SelectionController.SelectAll` | Selection/SelectionController.cs:144 | 从第一个非空条目第一个块的 (0,0) 选到最后一个非空条目末块末片段的末尾 |
| `SelectionController.GetSelectedText` | Selection/SelectionController.cs:179 | 纯文本导出：块间 `\n\n`，块内片段用 `Segment.Separator`，整段选中时加 `Prefix` |
| `SelectionController.GetSelectedMarkdown` | Selection/SelectionController.cs:232 | Markdown 源码导出：取选区覆盖的**完整块**的 `Range`，多消息间 `\n\n`，末尾 `TrimEnd` |
| `BlockText.Segments` / `.Collect` | Selection/BlockText.cs:32 / :39 | 按块类型展开片段（视图编号顺序的定义处） |
| `BlockText.PlainText` | Selection/BlockText.cs:146 | 行内纯文本：U+FFFC 占位替换为 `$latex$` / 图片 alt / `[x]` / 组件文本 |

## Dependencies
- Uses: `core-parsing-and-model` via `DocumentSnapshot`、`MarkdownBlock.Key`（= `Range.Start`）、`InlineContent`、`InlineObject` — `Selection/BlockText.cs:32`，`Selection/SelectionController.cs:179`，`src/TKCode.Arc.StreamMark/Core/Model/Blocks.cs:18`
- Uses: `avalonia-text-rendering` via `ISelectableSegment` 的实现与 `BlockView.Segments()` — `Rendering/BlockView.cs:41`、`Rendering/TextSegmentView.cs:19`、`Rendering/CodeBlockView.cs:256`、`Rendering/MathBlockView.cs:15`、`Rendering/TableBlockView.cs:223`、`Rendering/ContainerViews.cs:155`
- Uses: `avalonia-transcript` via `ISelectionHost` 的实现，含拖拽自动滚动 — `Transcript/TranscriptPanel.cs:924`、`:904`、`:897`
- Uses: plugins 通过 `ISelectionHost.Plugins` 取行内组件纯文本 — `Selection/SelectionController.cs:229` → `Plugins/BuiltInComponentsPlugin.cs:233`、`Plugins/IMarkdownPlugin.cs:114`
- Used by: `avalonia-transcript` via `TranscriptView.Selection` / `.SelectionController` 转发到面板 — `Transcript/TranscriptView.cs:30`、`:32`
- Used by: 单文档视图 via `MarkdownView : ISelectionHost` — `MarkdownView.cs:63`、`:235`、`:241`
- Used by: `avalonia-text-rendering` via `RenderContext.Selection` 与高亮绘制 — `Rendering/RenderContext.cs:38`、`Rendering/TextSegmentView.cs:438`、`Rendering/CodeBlockView.cs:471`、`Rendering/MathBlockView.cs:96`
- Used by: 折叠块视图 via `BlockText.Segments(summary).Count` 让编号对齐 — `Plugins/BuiltInComponentsPlugin.cs:89`

## State it owns
- 选区本体只有两个字段：`SelectionModel._anchor` / `_active`（`Selection/SelectionModel.cs:9-10`），两个 `TextPoint` 都是值类型，**不含任何视图或 `SegmentKey` 引用**。这是"虚拟化回收后选区仍有效"的根据。
- 坐标语义：`Entry` = 会话中第几条消息（`RenderContext.Entry`，`Rendering/RenderContext.cs:41`）；`BlockKey` = 顶层块在源码中的起始偏移（`StreamMark.Core/Model/Blocks.cs:18`）；`Segment` = 块内第几个文本片段；`Offset` = 片段内字符偏移（`Selection/TextPoint.cs:8`）。
- 稳定性来源：`BlockKey` 取自源码偏移，而源码只追加（`Selection/TextPoint.cs:6` 注释），所以流式重解析后同一块的 key 不变；`Entry` 在消息插入/删除后会漂移，`TranscriptPanel` 在序号变化时重建 `RenderContext` 来兜底（`Transcript/TranscriptPanel.cs:190-196`）。
- 派生数据（不持久化，随时可重算）：`Range`、`IsEmpty`、`GetSegmentRange`；以及复制结果文本。
- 被虚拟化移出的块如何仍可复制：`GetSelectedText` / `GetSelectedMarkdown` 遍历的是 `_host.GetSnapshot(entry)`，不是 `RealizedSegments()`（`Selection/SelectionController.cs:189`、`:241`）。`TranscriptPanel.GetSnapshot` 对尚未解析的按需条目会就地 `EnsureParsed()` 并返回 `Latest`（`Transcript/TranscriptPanel.cs:911-918`）；已回收（存在但未实例化）的行本来就在 `state.Applied` 里。
- 片段身份 `SegmentKey` 由 `BlockView.KeyFor` 在每次 `Update` 时按阅读顺序自增分配（`Rendering/BlockView.cs:43`），编号本身不持久化，靠"与 `BlockText` 遍历顺序一致"这条契约在每帧重建。

## Contracts it exposes
- `ISelectableSegment`（`Selection/ISelectableSegment.cs:7`）：`Key`（片段身份）、`Text`（片段文本，`HitTest` 的偏移域与 `BlockText.Segment.Length` 相同，U+FFFC 占位由复制端替换，见 `:11` 注释）、`View`（坐标换算用）、`HitTest(Point)→int`、`GetCharacterBounds(int)→Rect`。`BlockView.Segments()` 必须按阅读顺序、深度优先地返回本块全部片段（`Rendering/BlockView.cs:41`；容器实现见 `Rendering/ContainerViews.cs:155`、`Rendering/TableBlockView.cs:223`）；`DiagramBlockView` 在显示图而非源码时返回空（`Rendering/DiagramBlockView.cs:237`），`CollapsibleBlockView` 折叠时只返回标题（`Plugins/BuiltInComponentsPlugin.cs:204`）。
- `ISelectionHost`（`Selection/SelectionController.cs:13`）：`RealizedSegments()` 只需给"已实例化"的片段，供 `FindPoint` 的最近片段回退用（`:293`）；`GetSnapshot(entry)` 必须能返回未实例化条的完整块模型（这是跨虚拟化复制的前提）；`OnSelectionDrag(hostPoint)` 有默认空实现（`:31`），面板覆写为滚动。
- `BlockText` 的隐式契约：`Segment.Length` 与视图 `Text.Length` 相等；`Collect` 的遍历顺序必须与块视图分配编号的顺序一致（`Selection/BlockText.cs:6-9` 注释）。折叠块标题无 `summary` 时用负编号 `-1` 绕过（`Plugins/BuiltInComponentsPlugin.cs:73`，`Selection/SelectionModel.cs:75` 忽略负编号）。 插件组件可以登记自己的收集器（`ComponentTextCollector`），所以视图侧的调用（`SelectionController`、`SearchModel`、`SelectionRemap.Map`、`DocumentNavigation.*`、折叠块的 `CountSegments`）一律传当前注册表；不传注册表的重载只按默认规则收集，测试与无插件场景用它。
- 剪贴板格式：只用 `clipboard.SetTextAsync(string)`（`Selection/SelectionController.cs:174`）——没有 HTML/自定义 MIME 格式。两种文本形态：纯文本（公式还原为 `$latex$`、表格单元格用制表符、列表项带 `- ` / `1. ` / `[x] ` 前缀，`Selection/BlockText.cs:107-112`、`Selection/SelectionController.cs:215-219`）与 Markdown 源码（完整块的原文，`Selection/SelectionController.cs:254-263`）。

## How to extend
- To add a 新块类型成为可选中/可复制：实现 `ISelectableSegment`（或让块视图的 `Segments()` 返回内部的 `TextSegmentView`）→ 在 `BlockView.OnUpdate` 里用 `KeyFor(context, topKey, ref segment)` 按阅读顺序分配编号（`Rendering/BlockView.cs:43`）→ 在 `BlockText.Collect` 加同顺序、同片段数的 `case`（`Selection/BlockText.cs:39`）。缺 `BlockText` 这一步的症状：该块能选中、能画高亮，但复制时缺失它的文字，并且**后续块的全部片段文本会整体前移错位**（复制由 `new SegmentKey(entry, block.Key, i)` 按下标重算，`Selection/SelectionController.cs:204`）。
- To add 新片段类型（一个块里多出一种可选文本，如表格单元格之外的子片段）：先决定它在阅读顺序中的位置，再同时改两处：视图侧分配编号的顺序（`Rendering/BlockView.cs:43` 调用点）与 `BlockText.Collect` 对应分支的 `list.Add` 次序（`Selection/BlockText.cs:39`）；`Segment.Separator` / `Prefix` 决定复制时片段间与整段选中时的拼接（`Selection/BlockText.cs:17`）。两侧顺序不一致不会报错，只会静默复制出别人的文本。
- To add 新复制格式（如 HTML、CSV）：在 `GetSelectedText` / `GetSelectedMarkdown` 旁加 `GetSelectedXxx()`，再在 `CopyAsync` 的选择处加分支（`Selection/SelectionController.cs:171`），最后在 `OnKeyDown` 里加触发键（`Selection/SelectionController.cs:123`，注意先做平台热键匹配再做 Shift 判定）。缺 `OnKeyDown` 一步的症状：方法存在但用户无法触达；缺 `CopyAsync` 分支的症状：按键触发后仍复制纯文本。
- To add 插件行内组件的纯文本：由插件实现 `IInlineComponentRenderer.ToPlainText`（`Plugins/IMarkdownPlugin.cs:114`，示例 `Plugins/BuiltInComponentsPlugin.cs:233`），本模块通过 `Selection/SelectionController.cs:229` 取用，无需改本模块。
- To change 拖拽时自动滚动的速度/边界：改 `TranscriptPanel.OnSelectionDrag`（`Transcript/TranscriptPanel.cs:924`），本模块只负责调用（`Selection/SelectionController.cs:106`）。

## Invariants and traps
- **`TextPoint.Offset` 不是源码偏移**：它是片段纯文本里的下标（粗体、链接的标记不在里面）。源码位置只有 `BlockKey`（块起点）与块的 `Range` 是真的，所以反查只给块级区间（`SourceRangeOf`），不要把片段偏移加到 `BlockKey` 上冒充源码偏移 — see `Selection/DocumentNavigation.cs:107`。
- **文档被整篇替换后选区的去向（`SelectionRemap`）**：平移的块精确平移；被替换的块映射到配对的新块并把片段序号 / 偏移钳到新文本范围内；被删除的块退到后面第一个存活块的开头（没有则文档末尾）；两端落到同一点则清空（原本就是插入点的保留）。选区坐标只存值，所以搬迁只是重写 `BlockKey` — see `Selection/SelectionRemap.cs`、`MarkdownView.RemapSelection`。
- **阅读锚点的身份是 `MarkdownView.DocumentId`，不是快照的 `Generation`**：`Generation` 在 `Complete()` 时也会递增（`DocumentComposer.Finish`），同一份流式源码完成前后 `Generation` 不同。`DocumentId` 只在 `Markdown` / `Document` / `Stream` 被赋值时递增；外部对已绑定的 `StreamingDocument` 直接 `Reset` 它察觉不到，此时锚点按块 `Key` 找不到会退到最近的块（`Approximate`）— see `MarkdownView.cs:110`、`:328`。
- 源码导航与锚点恢复在版面未稳定（`IsMeasureValid` / `IsArrangeValid` 为假）时直接返回 `LayoutPending` 而不滚动，不要改成"尽力而为"——位置会按旧版面算错 — see `MarkdownView.cs`（`IsLayoutReady`）。
- 选区坐标绝不持有视图引用 — 否则虚拟化回收后选区指向已丢弃的控件 — see `Selection/SelectionModel.cs:9`、`Selection/SelectionController.cs:189`。
- `GetSelectedText` 的片段区间按**片段下标**从 `BlockText` 重算，不依赖任何视图是否实例化；而视图侧编号只在 `Update` 时分配，因此"顺序一致"是唯一粘合剂 — see `Selection/SelectionController.cs:204`、`Rendering/BlockView.cs:43`。
- 指针判定顺序：`ClickCount == 2` 选词、`>= 3` 选整段、默认分支才看 Shift 扩展，否则 anchor=active 定位（`Selection/SelectionController.cs:72-91`）。注意 `case 2` 里的 `WordAt` 调用在 `switch` 分支内声明，`default` 分支不会执行它；拖拽只在 `ClickCount == 1` 时成立（`:92`），双击/三击不会进入拖拽状态。
- Shift 单击要求 anchor 已存在（`Selection/SelectionController.cs:82`）；且 `OnPressed` 在 `FindPoint` 返回 null 时会先 `Clear()`（`:66-70`），所以点到完全没有片段的空白且无最近片段回退时，Shift 扩展会退化为清空。首击的 anchor=`active` 使 `IsEmpty` 为真，所以"点一下再 Shift 点"才有可见效果。
- ⌘C 与 ⌘⇧C 的差异只在 `asMarkdown` 这一个参数：`OnKeyDown` 用 `HotkeyConfiguration.Copy` 匹配（`:125-126`），命中后 `asMarkdown = e.KeyModifiers.HasFlag(KeyModifiers.Shift)`（`:131`）→ `GetSelectedMarkdown()` 而不是 `GetSelectedText()`（`:171`）。两者产出差异很大：纯文本保留制表符/列表前缀，Markdown 给的是选区覆盖的**完整块**源码，部分选中的段落也会整段带出来（`Selection/SelectionController.cs:232`；对应测试 `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/TranscriptViewTests.cs:129`）。
- `GetSelectedText` 跳过"空区间"片段：`e == s && segment.Length > 0` 时 continue（`Selection/SelectionController.cs:208`），因此选区端点在一段末尾与下一段开头重合时不会插入多余分隔符。
- 被实例化但**被窗口化的代码块**仍是全量可复制：`CodeTextView.Text` 返回整个代码块文本（`Rendering/CodeBlockView.cs:282`），选中整块时复制的正是全量（测试 `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/LongCodeTests.cs:69-70`）。屏幕只画窗口内的行（`:471` 的 `_lineStarts` 窗口）。
- 图片占位字符：`BlockText.PlainText` 把 U+FFFC 换成 `$latex$` / 图片 `Alt` / `[x]` / 组件文本（`Selection/BlockText.cs:167-174`）。而"图片在 `[![…](…)](…)` 中应带链接区间、点击打开链接而非图片查看器"的逻辑**不在本模块**，在 `avalonia-text-rendering`：`TextSegmentView.OnPointerReleased` 先判 `Loaded when LinkAt(position) >= 0` 再发 `LinkClicked`（`Rendering/TextSegmentView.cs:652-657`），只有无链接时才走 `ImageViewer.Show`（`:659-666`）。本模块无关，但 `TextSegmentView` 也刻意在选区非空时拒绝链接/图片点击，避免拖拽选中被当成点击（`Rendering/TextSegmentView.cs:637`）。
- `SelectAll` 依赖"第一个/最后一个非空快照"：条目快照为空（`Count == 0`）会被跳过（`Selection/SelectionController.cs:148`、`:154`），完全没有内容的文档不会改变选区（早先的选区保留）。
- `GetSelectedMarkdown` 只取完整顶层块的源码区间（`Selection/SelectionController.cs:246-255`），因此对嵌套内容不会裁到子块级别；`start/end` 初值 `int.MaxValue/-1` 保证无重叠块时返回空串（`:245`、`:257`）。
- 折叠块标题参与选区的条件：只有存在 `<summary>` 子块时标题才占一个正编号片段，否则用 `-1` 且被 `GetSegmentRange` 忽略（`Plugins/BuiltInComponentsPlugin.cs:73`、`Selection/SelectionModel.cs:75`），与 `BlockText.Collect` 的 `ComponentBlock` 分支（summary 优先）配对（`Selection/BlockText.cs:86-96`）。
- 拖拽期间指针超出视口才会滚动，且每帧调用一次、单次最多 ±40px（`Selection/SelectionController.cs:106`、`Transcript/TranscriptPanel.cs:924-934`）；滚动导致的行回收不影响选区，但 `FindPoint` 的最近片段回退要求片段 `IsEffectivelyVisible`（`Selection/SelectionController.cs:295`），所以滚出视口的片段不会命中。

## Tests
测试位置：`tests/TKCode.Arc.StreamMark.Tests/Avalonia/`。与本模块直接相关的用例：
- `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/SelectionAndFadeTests.cs:14` `ProgrammaticSelection_AcrossBlocks_IsPaintedAndCopied` — 跨块选区 + `GetSelectedText` + `SelectAll` + `GetSelectedMarkdown`（端点断言含 `- item one`、`1\t2`）。
- `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/TranscriptViewTests.cs:108` `Selection_SpansMessages_AndCopiesVirtualizedRows` — 跨消息、多数行未实例化的复制（`:123` 用 `TextPoint(last, int.MaxValue, 0, 0)` 作末端）。
- `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/DeferredEntryTests.cs:47` — 全选跨过尚未解析的按需条目。
- `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/LongCodeTests.cs:69` — 分段虚拟化的超长代码块仍能整块/按行复制。
- `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/HtmlRenderingTests.cs:146`、`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Plugins/PluginSystemTests.cs:156` — 全选后的纯文本（HTML 与插件行内组件文本）。
命名模式：类名 `<主题>Tests`，方法名用下划线分词（`<场景>_<断言>`），选区用例都走 `HeadlessApp.Run` + `HeadlessApp.Settle`（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs`），并直接调 `view.ApplyLatest()` 再读 `Snapshot`。
只跑一个文件：仓库 README 只给出整库命令 `dotnet test TKCode.Arc.slnx`（`README.md:91`）；按用例过滤可用 `dotnet test TKCode.Arc.slnx --filter "FullyQualifiedName~SelectionAndFadeTests"`（此 `--filter` 用法未在仓库文档或 CI 中验证）。

## Related flows
- [text-selection-copy](../flows/text-selection-copy.md)
- [transcript-virtualization-scroll](../flows/transcript-virtualization-scroll.md)
