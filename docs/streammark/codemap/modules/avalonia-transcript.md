---
module: avalonia-transcript
sources: ["src/TKCode.Arc.StreamMark/Avalonia/Transcript/**"]
commit: 44a8f5a
updated: 2026-10-16
---
# avalonia-transcript（会话记录：摊平行 + 自研虚拟化）

把长会话里所有消息摊平成一张行列表（每条消息的每个 Markdown 顶层块一行、每条宿主内容一行），只实例化视口上下各 600px 内的行，未实例化的行用估算高度参与滚动，并在实测高度或插入/删除行之后做滚动锚定与贴底跟随。
**不负责：** Markdown 解析、快照与差异（`core-streaming`，`StreamingDocument` / `SnapshotDiff`）；块视图本身的渲染与块模型 → 视图映射（`avalonia-text-rendering`，`BlockViewFactory` / `BlockView`）；选区坐标与复制（`avalonia-selection`，`SelectionModel` / `SelectionController`）；样式解析与 `Md.*` 资源（`Rendering/MarkdownStyle.cs`）；节奏控制与渐显时间记录（`StreamMark.Avalonia/MarkdownStream.cs`、`StreamMark.Core/Pacing`）。

## Entry points
| Symbol | File | Role |
|---|---|---|
| `TranscriptView` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptView.cs:13` | 对外控件：`ScrollViewer` + `TranscriptPanel`，沿用宿主 `ScrollViewer` 主题（`StyleKeyOverride` 第 24 行）；禁用横向滚动条（第 20 行） |
| `TranscriptView.Entries` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptView.cs:26` | `ObservableCollection<TranscriptEntry>`，宿主往里加消息；构造时 `Panel.SetEntries(Entries)`（第 18 行） |
| `TranscriptView.Panel` / `Selection` / `IsPinnedToBottom` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptView.cs:28,30,34` | 透传面板的状态与选区 |
| `TranscriptView.ScrollToEnd()` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptView.cs:37` | `new` 隐藏基类方法，转调 `TranscriptPanel.ScrollToEnd` |
| `TranscriptPanel` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:29` | `Panel, ILogicalScrollable, ISelectionHost`：行模型、虚拟化、锚定、贴底、选区宿主 |
| `TranscriptPanel.ApplyLatest()` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:243` | 把各文档最新快照应用到行（测试可同步调用） |
| `TranscriptPanel.ScrollToEnd()` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:800` | 置 `_pinned = true` 并请求重新测量 |
| `TranscriptEntry` / `MarkdownEntry` / `ContentEntry` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptEntry.cs:8,11,68` | 条目类型 |
| `MarkdownEntry.FromText` / `.FromTextDeferred` / `.EnsureParsed` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptEntry.cs:25,36,48` | 静态消息：立即解析 / 延迟解析 / 强制解析 |

## Key types and functions
| Symbol | File | Role |
|---|---|---|
| `EntryState` (private) | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:31` | 一条消息的状态：已应用快照 `Applied`、`RenderContext`、行 `Rows`、每个顶层块占的行数 `Slots`、块状态 `BlockStateStore`、宿主内容控件缓存 `ContentView` |
| `Row` (private) | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:43` | 一行：所属状态、块、分段序号 `Chunk/ChunkCount`、高度 `Height`、是否已实测 `Measured`、缓存纵坐标 `Top` |
| `SyncEntries()` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:151` | 按引用复用旧 `EntryState`（订阅/退订 `SnapshotReady`），重建 `_entries`，并在 `Entry` 序号变化时丢弃 `RenderContext`（第 189–196 行） |
| `ApplyAll()` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:249` | 用 `SnapshotDiff.Compute` 把差异翻译成行的增删改；按 `ChunkCount` 决定一个块占几行；占位行被真实行取代时先清空状态 |
| `SnapshotReady` → `RequestFrame` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:202,204` | 后台快照通知经 `Dispatcher.UIThread.Post` + `TopLevel.RequestAnimationFrame` 合并，每帧最多应用一次 |
| `CaptureAnchor()` / `RestoreAnchor()` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:355,372` | 记下视口内第一行及其相对视口位置（`PendingAnchor`，第 80 行），行结构变化后把它挪回原位 |
| `ParseVisibleDeferred(double)` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:395` | 视口 ±600px 内尚未解析的延迟条目就地在界面线程解析并展开，最多 8 轮 |
| `Estimate(MarkdownBlock, RenderContext)` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:526` | 按块类型的粗略高度估算（未测量行、延迟条目占位行） |
| `EstimateText(string)` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:424` | 延迟条目的占位行高：按每行字符数（宽度 / 字号 × 0.62）与空行估算 |
| `ChunkCount` / `ApplyChunk` / `Estimate(Row,…)` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:467,484,513` | 超长代码块（> 200 行，`ChunkThresholdLines` 第 463 行）按 128 行（`LinesPerChunk` 第 465 行）拆成多行并逐段估算 |
| `EnsureRows()` / `EnsureTops()` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:548,576` | 重建 `_rows` 扁平列表；自顶向下累加 `Top` 与总高度（行间距：同块分段 0、同消息块间 `BlockSpacing`、消息间 `EntrySpacing`） |
| `FirstRowAt(double)` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:599` | 在已排序的 `Top` 上二分，取第一个底边越过给定 y 的行 |
| `Realize()` / `Unrealize()` / `BuildContent()` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:618,657,645` | 实例化一行（块视图经 `BlockViewFactory.Create`）、回收、宿主内容控件或数据模板构建 |
| `MeasureOverride` / `ArrangeOverride` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:669,752` | 宽度变化时作废全部实测高度；最多 3 轮"实例化 → 测量 → 更新 Top → 恢复锚点"；按 `MaxContentWidth` 居中排布 |
| `UpdateTail()` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:764` | 只有最后一个流式条目的最后一段被标为 `IsDocumentTail`（渐显与光标，见 `Rendering/TextSegmentView.cs:446`） |

## Dependencies
- Uses: `core-streaming` via `StreamingDocument.Latest` / `SnapshotReady` / `LoadNow` / `GetText` — `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:259`（`markdown.Document.Latest`）、`TranscriptEntry.cs:52`（`Document.LoadNow`）、`TranscriptPanel.cs:922`（`GetText`）
- Uses: `core-streaming` via `SnapshotDiff.Compute` 的 `Insert/Update/Remove` 语义（`Index` 是"正在变换的列表"中的位置） — `src/TKCode.Arc.StreamMark/Core/Streaming/SnapshotDiff.cs:18`，调用点 `TranscriptPanel.cs:276`
- Uses: `avalonia-text-rendering` via `BlockViewFactory.Create` / `CanReuse`、`BlockView.Update` / `Segments` / `LastSegment`、`RenderContext`、`MarkdownStyle.Resolve` — `TranscriptPanel.cs:495,503,633,231`
- Uses: `avalonia-text-rendering` via 块状态存储（`RenderContext.State`，每消息一份 `EntryState.State`） — `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:39,237`
- Uses: `avalonia-selection` via `ISelectionHost` 的 6 个成员与 `SelectionController` — `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:893-934`，接口定义 `src/TKCode.Arc.StreamMark/Avalonia/Selection/SelectionController.cs:13`
- Uses: `avalonia-extensions` via `MarkdownServices.GetServices` / `GetImageBaseUri` 进入 `RenderContext` — `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:231-237`
- Uses: `MarkdownStream`（流式条目构造） — `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptEntry.cs:20`
- Used by: `samples` / 宿主应用 — 本快照下**没有任何** `samples/**` 或 `bench/**` 引用 `TranscriptView` / `TranscriptPanel`（已在整个仓库内检索，无匹配）；目前唯一使用者是测试与 `TranscriptView` 自身
- Used by: `tests` via `TranscriptView` / `TranscriptPanel` 的公开成员（`Entries`、`Panel.ApplyLatest`、`RowCount`、`RealizedCount`、`IsPinnedToBottom`、`Offset`、`Extent`、`Viewport`） — `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/TranscriptViewTests.cs:43-68`

## State it owns
- 行模型：`_rows` 是所有条目 `EntryState.Rows` 的扁平只读视图（`EnsureRows`，`TranscriptPanel.cs:548-574`）。每行一个 `Row`；Markdown 的每个顶层块至少一行（`ApplyAll` 按 `SnapshotDiff` 的 Insert/Update/Remove 增删行，第 283-347 行），超长代码块拆成 `ChunkCount` 行；`ContentEntry` 恒为一行（第 560-563 行）；未解析的 `MarkdownEntry` 是一行占位（`Block == null`，第 565-569 行）。
- 行高：未实测的行用估算（`Row.Height`，Insert 时 `Estimate`，第 291 行；`ContentEntry` 固定 60，第 562 行；延迟条目 `EstimateText`，第 568 行），实例化后由 `view.DesiredSize.Height` 覆盖（第 713-725 行）。真实高度是权威值，估算只在"从未进过视口"的行上存在。
- 派生副本：`Row.Top`、`_extent`（`EnsureTops` 第 595 行）、`_viewport`（测量末尾第 745 行）、`EntryState.Slots`（快照块 → 行数）、`EntryState.Applied`（= 上次应用的 `Document.Latest`）、`EntryState.Context`（`RenderContext.Entry` 是消息序号，会随顺序变化作废重建）。
- 虚拟化窗口：`Overscan = 600`（第 55 行），实例化区间 `[offset - 600, offset + height + 600]`（第 700-704 行）；回收 = `_realized` 中本轮未保留的行（第 727-730 行）。
- 滚动锚定状态：`_pendingAnchor`（`PendingAnchor(Row, Delta, EntryDelta)`，第 80 行），由 `CaptureAnchor` 记录、`RestoreAnchor` 消费；用户直接改变 `Offset` 或 `SetOffsetInternal` 时清空（第 839、880 行）。
- 贴底状态：`_pinned`（初始 `true`，第 72 行）。`Offset` setter 里 `y >= max - 2` 即视为贴底（第 838 行）；`ScrollToEnd()` 只置位（第 800 行）。
- 其它：`_offset`、`_width`、`_rowsDirty` / `_topsDirty` + `_topsDirtyFrom`（只从第一处变化的行重算 `Top`；`Row.Index` 在 `EnsureRows` 时更新）、`EntryState.RefreshedContext`（快照与上下文都没变的条目每帧直接跳过）、`_lastScrollInfo`（滚动信息不变时不发 `ScrollInvalidated`）、`_frameRequested`、`_settingOffset`（防止 `ScrollInvalidated` 回写重入）、`_tail`（当前渐显尾段）、`Selection` 与 `_selection`。

## Contracts it exposes
- `TranscriptView.Entries`：`ObservableCollection<TranscriptEntry>`，可直接 `Add` / `Insert(0, …)` / `Remove`（测试中就这么用，`DeferredEntryTests.cs:82`）。面板监听 `CollectionChanged`（`SetEntries` 第 133-146 行），并把集合按 `IReadOnlyList` 保存；条目按**引用**身份复用状态（`TranscriptEntry` 未重写 `Equals`），因此同一个条目对象从集合里移除再放回会丢失 `EntryState`（挂起的快照订阅被退订、块状态清空）。
- `MarkdownEntry`：`Document` + 可选 `State`（流式渐显）+ `Tag`（宿主数据）。构造后应把快照交给面板——面板自己订阅 `Document.SnapshotReady`（`SyncEntries` 第 167 行），宿主不需要手动通知。
- 延迟条目（deferred）：`MarkdownEntry.FromTextDeferred(text, profile, tag)` 只保存文本，`IsParsed == false`、`DeferredText` 为原文；`EnsureParsed()` 用 `Interlocked.Exchange` 保证只解析一次（`TranscriptEntry.cs:48-54`）；解析走 `StreamingDocument.LoadNow`（同步、界面线程、要求文档无挂起工作，`StreamingDocument.cs:126`）。约定：语义等价于 `FromText`，只是解析时机推迟到"第一次需要它的行、或选区/复制跨过它"；解析前按字符数与换行估算高度，解析后占位行被真实行替换（`ApplyAll` 第 260-268 行）。
- `ContentEntry(object content)`：`content` 是 `Control` 就直接用，否则用 `this.FindDataTemplate(content).Build(content)`，都没有时退化为 `TextBlock{Text = content.ToString()}`（`BuildContent` 第 645-655 行）。整条内容占一行，不参与分段。
- 面板的 `ILogicalScrollable` 对外行为：`IsLogicalScrollEnabled => true`；`ScrollSize = (0, 48)`（每行 48px）、`PageScrollSize = (0, viewport.Height - 48)`；`Extent` / `Viewport` 在测量阶段更新；写 `Offset` 会被夹到 `[0, extent - viewport]`，相等（< 0.01）时直接返回，否则同时更新 `_pinned`、清锚点、请求测量；`_settingOffset` 期间忽略回写（第 822-842 行）。`BringIntoView` 只在**已实例化**的行里查找目标控件并滚动，未命中返回 `false`（第 846-869 行）；`GetControlInDirection` 恒返回 `null`（无键盘逐行导航，第 871 行）。`ScrollInvalidated` 在测量末尾与 `SetOffsetInternal` 后各触发一次（第 748、885 行）。
- 选区宿主：`ISelectionHost.HostControl = this`、`EntryCount = _entries.Count`、`RealizedSegments()` 只枚举 `_rows` 中已实例化的 `BlockView`（第 897-898 行）——所以被回收的块不在命中集里，但 `GetSnapshot` / `GetSource` 仍能从模型给出文本；跨到未解析条目时 `GetSnapshot` 会就地 `EnsureParsed()` 并请求下一帧（第 904-919 行）。

## How to extend
- To add a 新的宿主条目类型（例如"工具调用卡片"）：先在 `TranscriptEntry.cs` 加 `TranscriptEntry` 子类（照 `ContentEntry`，`TranscriptEntry.cs:68`），再在 `TranscriptPanel.EnsureRows()` 给它建行并给初始高度（`TranscriptPanel.cs:548-574`，现有分支只认 `ContentEntry` 与未解析的 `MarkdownEntry`），最后在 `Realize()` 里构建控件（`TranscriptPanel.cs:618-643`），并在 `Unrealize()` 里决定是否像 `ContentView` 那样缓存控件以保住内部状态（`TranscriptPanel.cs:657-667`）。若新条目自带文档（像 `MarkdownEntry`），还要在 `SyncEntries()` 订阅/退订 `SnapshotReady`（`TranscriptPanel.cs:165-177`）并在 `ApplyAll()` 里跳过非文档条目（`TranscriptPanel.cs:254`）。**漏掉 `EnsureRows` 的那一步**的表现是：条目进了 `Entries`、`_entries` 里也有状态，但它 `state.Rows.Count == 0`，于是 `_rows` 里没有任何一行——面板既不显示它，`RowCount` 也不含它（`_rows.AddRange(state.Rows)`，第 570 行）。
- To add 一种影响行高的块类型（估算与实测必须同步的三处）：① `Estimate(MarkdownBlock, RenderContext)` 的 `switch` 加分支，用内容长度 / 行数乘上 `MarkdownStyle` 的行高（`TranscriptPanel.cs:526-546`）；② 若该块视图把高度放在**自身 Measure 之外**的装饰上（像 `CodeBlockView` 的 `HeaderHeight = 34` / `Padding = 14`，`CodeBlockView.cs:24-25`），还要同步 `Estimate(Row, context)` 里对应的 `34` / `14`（`TranscriptPanel.cs:513-521`）——这两个魔数是 `CodeBlockView.MeasureOverride`（`CodeBlockView.cs:168-178`）的重复；③ 若新块类型也要分段，改 `ChunkCount`（`TranscriptPanel.cs:467`，目前只认 `CodeBlock`）并在 `ApplyChunk`（`TranscriptPanel.cs:484`，目前只 `SetChunk` 到 `CodeBlockView`）里下发窗口，否则每段都会自己渲染整块。实测一侧不需要在面板里加代码：行的真实高度永远来自 `view.DesiredSize.Height`（`TranscriptPanel.cs:720`）。
- To add 一种"延迟加载"的宿主条目：只要求 `EnsureRows` 产出占位行、并在该行被实例化前用 `ParseVisibleDeferred` 的同类机制替换（`TranscriptPanel.cs:395-421` 是模板，注意 8 轮上限与外层 `ApplyAll` + `EnsureRows` + `EnsureTops` + `RestoreAnchor` 的组合）。

## Invariants and traps
- **变高行不能交给 `VirtualizingStackPanel`** — 它假设项高一致，变高项会让滚动条在测量时跳动；本模块因此自己实现 `ILogicalScrollable`，用自算的 `Extent`/`Offset` 驱动宿主的 `ScrollViewer`（`TranscriptPanel.cs:29`、`EnsureTops` 第 595 行、`Extent` 第 818 行） — 决策记录见 `docs/streammark/decisions.md:12`；`TranscriptView` 只是把面板当作 `ScrollViewer.Content`（`TranscriptView.cs:17-19`）。
- **视口上方插入内容必须保持可见内容不动** — `SyncEntries` 先 `CaptureAnchor()` 再重建（`TranscriptPanel.cs:153`），`MeasureOverride` 每轮结束后按锚点回写 `_offset`（第 737-740 行）；`RestoreAnchor` 分两种情形：锚点行还在原位（第 385 行）或锚点行已被替换成真实行（延迟条目展开，第 390 行，改用条目顶部）。测试：`DeferredEntryTests.InsertAboveViewport_KeepsVisibleContentInPlace`（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/DeferredEntryTests.cs:57`）。注意锚点只在**非贴底**时记录（`CaptureAnchor` 第 357 行提前返回），贴底时靠 `_offset = extent - height` 重新贴底。
- **行高估算错误会表现为跳变，陈旧行高会表现为重叠** — 估算只影响从未进过视口的行；一旦该行进入 ±600px 就会实测并改写 `Row.Height`，`_topsDirty` 使 `EnsureTops` 重算全部 `Top` 与 `Extent`，滚动条拇指长度随之变化（用户看到的是"滚过一段后滚动条跳一下"）。因此宽度变化时必须作废全部实测高度（第 674-682 行）。反向的陷阱是"实测有效但记录过期"：宿主内容控件自行展开/折叠时布局管理器先单独重测它，此时 `view.IsMeasureValid` 为真、`row.Measured` 也为真，所以代码**总是**拿 `DesiredSize.Height` 与 `row.Height` 比较（第 718-725 行，注释在第 718-719 行）——漏掉这一步的表现是后续行与展开内容重叠。测试：`TranscriptViewTests.ContentEntry_That_Grows_PushesFollowingRowsDown`（`TranscriptViewTests.cs:142`）。另一处同步点是估算用的 `_width` 在第一次测量前是 720 的兜底值（第 427、529 行）。
- **块状态在分段与视图回收后重建仍保留** — `CodeBlockState.For(context, topKey, language)` 把状态写进 `RenderContext.State`（`Rendering/CodeBlockState.cs:48-61`），而这个存储是 `EntryState.State`，每条消息一份、随 `EntryState` 一起跨行增删存活（`TranscriptPanel.cs:39`）。键是 `(顶层块 key, 状态名)`，源码只追加所以 key 稳定（`Plugins/BlockStateStore.cs:3-8`）。因此：代码块的换行开关 `code.wrap`（写 `CodeBlockView.cs:47`、读 `CodeBlockView.cs:113`）在分段视图之间共享；`CodeTextView.OnAttachedToVisualTree` 会在重新挂载时 `ApplyWindow()` 取回回收期间产生的着色结果（`CodeBlockView.cs:591-604`）。反例警告：块状态属于 `EntryState`，把条目对象从 `Entries` 移除再放回去会得到新的 `EntryState`，状态（展开/折叠、换行）随之丢失。
- 分段代码块的**相邻分段之间不额外加间距**，块与块的间距也只在同一消息内用 `BlockSpacing`、跨消息用 `EntrySpacing`（`EnsureTops` 第 586-591 行）。分段序号变化（`DiffKind.Update`）时只增删末尾的行、其余行原地更新，但即使段数不变也会 `_topsDirty = true`（第 328-333 行），因为块内容变化可能改变行高。
- 只有**最后一个** `MarkdownEntry { State: not null }` 的最后一段被标记为文档尾（`UpdateTail` 第 764-791 行），流式光标与渐显只在那里渲染（`Rendering/TextSegmentView.cs:446`）；若该尾行恰好被回收，就没有光标。
- `RenderContext.Entry` 是"消息在 `_entries` 中的序号"，也是选区坐标的第一维；`SyncEntries` 一旦改变顺序就丢弃上下文、下一帧重建（`TranscriptPanel.cs:189-196`），`TextPoint` 的比较序（`Selection/TextPoint.cs:8-24`）依赖它。
- `ApplyAll` 会为**所有**条目的所有顶层块建 `Row`，只有控件是窗口化的（10 万块 = 10 万 `Row`，`VirtualizationScaleTests.cs:32-33`）；`EnsureTops` 一次是 O(行数)，任何 `_topsDirty` 都会全量重算。
- `MaxContentWidth` 只影响内容列的居中宽度，`Extent.Width` 始终是完整可用宽度（`ArrangeOverride` 第 752-761 行、`MeasureOverride` 第 746 行）。
- `SetEntries` 会退订旧的 `INotifyCollectionChanged` 并订阅新的（`TranscriptPanel.cs:133-146`）；传同一个集合两次是幂等的，传新集合会整体重同步。

## Tests
- 位置：`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/`，5 个文件：`TranscriptViewTests.cs`（贴底、流式、跨消息选区、宿主内容增高）、`VirtualizationScaleTests.cs`（10 万块 + 追加）、`DeferredEntryTests.cs`（延迟解析、视口上方插入）、`LongCodeTests.cs`（3000 行代码分 24 段、跨分段复制、跳转耗时）、`SmoothStreamWrapTests.cs`（平滑渐显不产生硬换行，回归）。
- 命名模式：类名 `<主题>Tests`，方法用"行为_期望"的句子（`LongTranscript_RealizesOnlyVisibleRows_AndStartsPinnedToBottom`）；测试体一律包在 `await HeadlessApp.Run(async () => { … })` 里在界面线程执行（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:55`），用 `HeadlessApp.Settle()` 推进帧，`HeadlessApp.Save(window, "…")` 落截图（目录由 `STREAMMARK_SNAPSHOT_DIR` 决定，`HeadlessApp.cs:36-49`）；程序集级禁用了并行（`HeadlessApp.cs:8`）。
- 只跑一个文件/类：`dotnet test TKCode.Arc.slnx --filter "FullyQualifiedName~Transcript.VirtualizationScaleTests"`（同理把类名换成 `DeferredEntryTests` / `LongCodeTests`）；整个模块 `dotnet test TKCode.Arc.slnx --filter "FullyQualifiedName~Tests.Transcript"`。仓库的既有命令是 `dotnet test TKCode.Arc.slnx`（`README.md:91`、`.github/workflows/ci.yml:76`）。
- 性能断言以 CI 环境变量分层（`LongCodeTests.cs:52-56`：中位数 + 宽松最大值），机器繁忙不该让测试红。

## Related flows
- [transcript-virtualization-scroll](../flows/transcript-virtualization-scroll.md)
- [streaming-token-to-view](../flows/streaming-token-to-view.md)
- [text-selection-copy](../flows/text-selection-copy.md)

（同目录下已有 `avalonia-text-rendering.md`、`avalonia-selection.md`；`docs/streammark/codemap/flows/` 目录尚未出现，上面三个 flow 页面按约定引用，尚未编写：(unverified)。）
