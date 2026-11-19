---
module: core-streaming
sources: ["src/TKCode.Arc.StreamMark/Core/Streaming/**", "src/TKCode.Arc.StreamMark/Core/Pacing/StreamPacer.cs"]
commit: 44a8f5a
updated: 2026-10-16
---
# 核心流式引擎

把任意切分的 Markdown 追加流变成一串不可变快照：源码只追加，切分器在"之后再来什么文本都不会改变前文解析结果"的位置推进冻结边界，冻结片段只解析一次后永不重解析，只有尾部每次更新重做补全 + 解析，`Complete()` 时再整篇重解析一次。
**不负责：** Markdig 管线 / 组件白名单 / 块模型构造（`core-parsing-and-model`，`src/TKCode.Arc.StreamMark/Core/Parsing/`）；未闭合语法的补全与源码偏移映射（`remend-and-offsetmap`，`src/TKCode.Arc.StreamMark/Core/Remend/`、`src/TKCode.Arc.StreamMark/Core/Parsing/OffsetMap.cs`）；快照到控件的应用与文本整形缓存（`avalonia-text-rendering`）；虚拟化与滚动（`avalonia-transcript`）；`StreamPacer` 的推动者（调用方是 `src/TKCode.Arc.StreamMark/Avalonia/MarkdownStream.cs`，不在本模块 glob 内）。

## Entry points
| Symbol | File | Role |
|---|---|---|
| `StreamingDocument` | `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:16` | 唯一公开的门面：`Append` / `Complete` / `Reset` / `Load` / `LoadNow`，任意线程可调 |
| `StreamingDocument.Append`/`Complete`/`Reset` | `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:65`、`:84`、`:99` | 提交文本并唤醒后台工作者；`Complete` 之后 `Append` 抛 `InvalidOperationException`（`:75`） |
| `StreamingDocument.Latest` / `SnapshotReady` | `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:47`、`:50` | 取快照的两种方式：轮询 + 事件（事件在**后台线程**触发，注释要求处理函数尽快返回） |
| `StreamingDocument.Version` / `.Load` | `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs` | `Version`：最新提交的源码版本，`DocumentSnapshot.Version` 追上它才说明最新文本已解析完（视图用它丢弃刷新时的过期快照）。`Load` 是**原子**的（单次加锁清空 + 写入 + 标记完成），工作线程取不到「已清空但未完成」的中间状态 |
| `StreamingDocument.WhenIdle` | `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:186` | 测试与 `Load` 路径用的"已处理完"信号 |
| `DocumentSnapshot` | `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentSnapshot.cs:11` | 冻结块 + 尾部块的只读视图；`Version` / `Generation` / `SourceLength` / `IsComplete` |
| `SnapshotDiff.Compute` | `src/TKCode.Arc.StreamMark/Core/Streaming/SnapshotDiff.cs:24` | 把快照对比算成顺序可应用的 `DiffOp` 列表（这是渲染层的唯一增量入口） |
| `SnapshotEdit.Compute` | `src/TKCode.Arc.StreamMark/Core/Streaming/SnapshotEdit.cs` | 宿主整篇替换文本后的新旧快照对齐：公共前缀 / 后缀 + 中间按内容做最长公共子序列（块数乘积 > 100 万退回按位置）。输出仍是 Insert / Update / Remove（平移的块用 Update，所以**没有**新增 `DiffKind`），外加每个旧块的去向 `BlockFate`（`Unchanged` / `Shifted` / `Replaced` / `Deleted`）与 `MapKey`（旧源码偏移 → 新偏移） |
| `StreamPacer` | `src/TKCode.Arc.StreamMark/Core/Pacing/StreamPacer.cs:60` | 突发网络分片 → 平滑显示节奏的纯算法（无计时器，时间由调用方传入） |
| `DocumentComposer`（internal） | `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:15` | 后台工作线程独占的组装器：`Update` / `Finish` / `Reset` / `FrozenEnd` |
| `StreamSegmenter`（internal） | `src/TKCode.Arc.StreamMark/Core/Streaming/StreamSegmenter.cs:12` | 冻结边界扫描器 + 未闭合围栏代码块的快速路径探针 |

## Key types and functions
| Symbol | File | Role |
|---|---|---|
| `StreamingDocument.Process` | `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:255` | 一次处理的决策点：源码代变更→`Reset`；`version == _processedVersion` 或已结束→返回 null；`complete` 时 `from = 0` 走整篇 |
| `StreamingDocument.Run`/`Schedule`/`TakeWork` | `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:213`、`:199`、`:237` | 单工作者模型：解析中到达的追加只置脏标记，一轮结束后再处理**最新**文本，中间版本被跳过 |
| `StreamingDocument.LoadNow` | `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:126` | 调用线程同步解析完整文本（历史消息首次显示），通过预占 `_running` 把期间到达的追加留给后台 |
| `DocumentComposer.Update` | `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:62` | push-once 语义：`from` 必须 ≤ `FrozenEnd`，返回的 `text` 是 `[from, 末尾)`；新冻结区间只解析一次 |
| `DocumentComposer.Finish` | `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:94` | 整篇重解析、`Generation++`、冻结列表重置、`IsComplete = true` |
| `DocumentComposer.SettledOptions` | `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:21` | 冻结片段与整篇解析用的 remend 选项：只留转义修正（与 `remend-and-offsetmap` 重复，但这里决定谁被补全） |
| `DocumentComposer.OpenCodeBlock` | `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:118` | 尾部是未闭合围栏时手工按行构造 `CodeBlock{IsOpen=true}`，并复刻 remend 的"去末尾单空格"（`:122`） |
| `StreamSegmenter.Advance` | `src/TKCode.Arc.StreamMark/Core/Streaming/StreamSegmenter.cs:69` | 逐完整行扫描；未结束的最后一行只借 `IsDecisive` 提前提交边界，不改扫描状态 |
| `StreamSegmenter.TryCommit`/`CanCut` | `src/TKCode.Arc.StreamMark/Core/Streaming/StreamSegmenter.cs:260`、`:273` | 冻结的守卫：`SawDefinition`、下一行缩进 / `:` 开头 / 列表标记 / setext 下划线一律不切 |
| `StreamSegmenter.TryGetOpenFence` | `src/TKCode.Arc.StreamMark/Core/Streaming/StreamSegmenter.cs:111` | 仅当围栏是这一段（run）的第一行（`_fenceStartsRun`）且 `partialLine` 未构成闭合围栏时命中 |
| `StreamSegmenter.IsDecisive` | `src/TKCode.Arc.StreamMark/Core/Streaming/StreamSegmenter.cs:310` | 未结束行的首字符判断；`-*+[=`、数字需累积到 16 字符才敢判断 |
| `AppendOnlyList<T>` | `src/TKCode.Arc.StreamMark/Core/Streaming/AppendOnlyList.cs:7` | 256 元素分块的只追加列表；快照只需记住 (列表, 数量)，旧分块永不移动 |
| `SnapshotDiff.Compute` 起点 | `src/TKCode.Arc.StreamMark/Core/Streaming/SnapshotDiff.cs:28` | 同代且共享冻结列表时从 `FrozenCount` 之后开始比较 |
| `DiffOp` | `src/TKCode.Arc.StreamMark/Core/Streaming/SnapshotDiff.cs:16` | `Index` 是"正在变换的列表"中的位置，按顺序应用才得到新列表（语义见 `:12-15`） |
| `StreamPacer.Tick`/`Release`/`Push`/`Finish` | `src/TKCode.Arc.StreamMark/Core/Pacing/StreamPacer.cs:141`、`:214`、`:102`、`:124` | 速率 = max(滑动到达速率, 积压/目标落后)；`MaxLag` 兜底；结尾在 `MinDrain`–`MaxDrain` 内排空 |

## Dependencies
- Uses: `core-parsing-and-model` via import（`Markdown.Parse` + `MarkdigMapper.MapDocument` + `MarkdownProfile`）— `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:111`、`:44`、`:47`
- Uses: `remend-and-offsetmap` via import（`Remend.Complete` + `RemendOptions` + `OffsetMap.Create`）— `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:7`、`:113`、`:115`
- Uses: `core-parsing-and-model` via import（块模型 `MarkdownBlock` / `SourceRange` / `CodeBlock`、`MarkdownBlock.Key` 与 `ComputeFingerprint`）— `src/TKCode.Arc.StreamMark/Core/Streaming/SnapshotDiff.cs:1`、`:35`、`:38`；`src/TKCode.Arc.StreamMark/Core/Model/Blocks.cs:18`、`:23`
- Uses: `avalonia-plugin-system` 的产物 `MarkdownProfile.RemendHandlers`（构造时合并进尾部选项）、`MarkdownProfile.SegmenterContainers`（注入切分器的容器标签表）— `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:46`、`:47`；`src/TKCode.Arc.StreamMark/Core/Parsing/MarkdownProfile.cs:52`
- Used by: `avalonia-text-rendering` via import / event — `src/TKCode.Arc.StreamMark/Avalonia/MarkdownStream.cs:24`（`new StreamingDocument(profile)`）、`src/TKCode.Arc.StreamMark/Avalonia/Rendering/DocumentPresenter.cs:34`（`SnapshotDiff.Compute`）
- Used by: `avalonia-transcript` via import / event — `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:167`（订阅 `SnapshotReady`）、`:276`（应用 `SnapshotDiff`）
- Used by: `tests` via `InternalsVisibleTo`（测试可直接 new `internal` 的 `DocumentComposer` / `StreamSegmenter`）— `src/TKCode.Arc.StreamMark/Core/StreamMark.Core.csproj:9`、`:10`
- Used by: `core-parsing-and-model`（反向）：`Html.HtmlElements.Containers` 被拼进切分器容器表 — `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdownProfile.cs:54`
- 依赖关系是单向的：Core 不引用 Avalonia；`StreamPacer` 只依赖 `System.Text` 与 `System.Globalization` — `src/TKCode.Arc.StreamMark/Core/Pacing/StreamPacer.cs:1`、`:2`

## State it owns
- `StreamingDocument._source`（`StringBuilder`）+ `_version` / `_sourceGeneration` / `_completeRequested`：**唯一真相来源**，只在 `_sourceLock` 下改写，只在内存中，不落盘 — `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:18`、`:20`。
- `_dirty` / `_running` / `_idle` 由 `_gate` 保护，是"有没有待处理工作"的唯一真相 — `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:24-27`、`:237`。
- `_latest` 快照用 `Volatile.Read/Write` 发布；`SnapshotReady` 在后台线程触发 — `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:47`、`:230`、`:231`。
- **派生且不可写**：`DocumentComposer._frozen`（最新一代的冻结前缀）、`DocumentSnapshot` 的 `_frozen` / `_tail`。快照只能通过 `DocumentSnapshot.this[int]` / `Tail` / `Blocks` 读，**没有任何公开写入 API**；快照是带引用共享语义的，最后一个快照存活期间旧分块不会被 GC — `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentSnapshot.cs:50`、`:53`、`:13`。
- `StreamPacer` 自持 `_pending` / `_arrived` / `_revealed` / `_rate` / 到达队列 / 环形渐显记录；生命周期只到 `Flush` 为止，不跨流复用（`Finish` 后 `Push` 抛异常）— `src/TKCode.Arc.StreamMark/Core/Pacing/StreamPacer.cs:63-77`、`:108`。
- 换一代即一切重来：`Reset` 新建 `AppendOnlyList` 与 `StreamSegmenter` 并 `_generation++` — `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:52`；消费者只有比较 `Generation` 才知道冻结前缀不再可信 — `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentSnapshot.cs:34`。
- 与本次快照的 git 事实：`44a8f5a`（本模块页面标称的 commit）**没有**改 `src/TKCode.Arc.StreamMark/Core/Streaming/` 或 `Pacing/`，最后一次改动这两个目录的提交是 `ec4418a`；之后 `c2c3d5c` 只改 Avalonia 侧（`#pragma warning disable CS0618` 的整形缓存）— 见上方 frontmatter 与 `git log -- src/TKCode.Arc.StreamMark/Core/Streaming`。
- **TextRunCache 的失效是 Avalonia 侧的事，本模块不管**：`TextRunCache.InvalidateFrom` 按"段落起点"存整段整形结果，只从差异点失效会复用旧段落（连同段落结束），流式追加会变成硬换行，所以必须从差异点所在段落的起点失效 — see `src/TKCode.Arc.StreamMark/Avalonia/Rendering/TextSegmentView.cs:162`（回归提交 `c2c3d5c`，回归测试 `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/SmoothStreamWrapTests.cs`）。本模块只负责让同一块对象的内容尽可能少变。

## Contracts it exposes
- 无 IPC、无事件总线、无数据库、无配置文件、无设置键、无 CLI 参数。接口全是进程内 API：`StreamingDocument`（public）、`DocumentSnapshot` / `DiffOp` / `DiffKind` / `SnapshotDiff`（public）、`StreamPacer` / `StreamPacerOptions` / `PacingPreset`（public）；`DocumentComposer` / `StreamSegmenter` / `AppendOnlyList` 是 `internal`，只有 `StreamMark.Core.Tests` 与 `StreamMark.Benchmarks` 能看见（`src/TKCode.Arc.StreamMark/Core/StreamMark.Core.csproj:9`、`:10`）。
- 事件只有一个：`StreamingDocument.SnapshotReady : event Action<DocumentSnapshot>?` — `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:50`。
- 线程契约：`Append` / `Complete` / `Reset` / `Load` / `Length` / `GetText` / `Latest` / `WhenIdle` 任意线程可调；`SnapshotReady` 反过来——它在后台线程触发 — `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:8-14`、`:49`。`StreamPacer` 相反，`Push` 与 `Tick` 必须同一线程（通常是界面线程）— `src/TKCode.Arc.StreamMark/Core/Pacing/StreamPacer.cs:58`。
- 快照契约（消费者必须遵守）：同 `Generation` 内，后一快照的前 `FrozenCount` 个块与前一快照是**同一对象** — `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentSnapshot.cs:8`；跨代时 `SharesFrozenWith` 返回 false，`SnapshotDiff` 退化为全量比较 — `src/TKCode.Arc.StreamMark/Core/Streaming/SnapshotDiff.cs:28`、`:55`。
- `StreamPacerOptions` 全部键与默认值（`Preset = Balanced`、`MaxLag = 400ms`、`MinDrain = 180ms`、`MaxDrain = 520ms`、`SkipCompletedBlocks = false`、`BypassThreshold = 4096`、`RevealHistory = 256`）— `src/TKCode.Arc.StreamMark/Core/Pacing/StreamPacer.cs:17-49`。`GetRevealTime` 目前仓库内只有测试调用（`tests/TKCode.Arc.StreamMark.Tests/Core/Pacing/StreamPacerTests.cs:84`），生产路径未接线 (unverified)。

## How to extend
- To add 冻结边界规则（例如新容器语法）：改 `StreamSegmenter` 的 `InOpaqueBlock` / `ProcessLine` / `AnalyzeLine`（`src/TKCode.Arc.StreamMark/Core/Streaming/StreamSegmenter.cs:128`、`:130`、`:192`），再在 `docs/streammark/streaming-rules.md:3` 更新规则清单，最后在 `tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/SegmenterTests.cs` 加一条"不能过早冻结"的用例。**漏掉"不冻结"的守卫 → 冻结片段与整篇解析不再一致，`StreamingEquivalenceTests` 的 `AssertSameBlocks` 报块类型 / range / 指纹不符**（`tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/StreamingEquivalenceTests.cs:105`）。
- 原样组件：切分器有单独的 `HtmlKind.RawComponent`，只有以 `</name>` 结尾的行才结束（`StreamSegmenter.EndsWithClosingTag`，结束不能早于解析器）；设置 `RawRegions` 时还记录内容区间。`DocumentComposer.Parse` 先用一个独立的切分器扫描（容器表是 `MarkdownProfile.RawScanContainers`：只跳过 HTML 容器，白名单组件内部照样找）得到区间，`MaskRawComponents` 把区间内非空白字符换成 `x`（行首 `>` 保留）后再交给 remend / Markdig，`OffsetMap.Create(base, masked, healed, raw)` 让 `SourceText` 从未遮盖的原文取字。三处规则要一起改。
- To 让某个标签按"容器"处理（内部可含空行不冻结）：不是改切分器，而是让插件把它注册为组件，或加进 `Html.HtmlElements.Containers` — `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdownProfile.cs:52`、`:54`；漏掉这一步 → `<details>` 之类容器里的空行会被当成可切点，容器被拆成两半。
- To 加节奏预设：在 `PacingPreset` 与 `StreamPacerOptions.TargetLag` 的 switch 各加一项（`src/TKCode.Arc.StreamMark/Core/Pacing/StreamPacer.cs:7`、`:43`），再补 `tests/TKCode.Arc.StreamMark.Tests/Core/Pacing/StreamPacerTests.cs` 的节奏断言。只加枚举不改 `TargetLag` → 新预设落进 `_ =>` 分支，静默等同 `Balanced`。
- To 让流式代码块尾部走别的路径：`DocumentComposer.Update` 的 `TryGetOpenFence` 分支（`src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:81`）是唯一入口；对应探针是 `StreamSegmenter.TryGetOpenFence`（`:111`）。漏掉 `_fenceStartsRun` 条件 → 围栏出现在段落中间（如引用块之后）时也会命中快速路径，`IsOpen` 与解析路径不一致。
- To 加一种差异操作：`DiffKind`（`src/TKCode.Arc.StreamMark/Core/Streaming/SnapshotDiff.cs:5`）是 `byte` 枚举，`DocumentPresenter.Apply`（`src/TKCode.Arc.StreamMark/Avalonia/Rendering/DocumentPresenter.cs:35`）与 `TranscriptPanel`（`src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptPanel.cs:276`）各有一份 `switch`，**必须同步改三处**；漏改 → 新操作被静默忽略，控件与快照脱节。

## Invariants and traps
- **`SnapshotDiff` 对「宿主整篇替换」是错的工具**：它按源码起点（`Key`）归并，在前面增删文字会让后面所有块 `Key` 偏移，全部被当成删除再插入，视图和块状态都丢。整篇替换走 `SnapshotEdit`（按内容对齐）；两者的 `DiffOp` 语义相同，`DocumentPresenter` 共用同一段应用逻辑 — see `src/TKCode.Arc.StreamMark/Core/Streaming/SnapshotEdit.cs`、`tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/SnapshotEditTests.cs`（含随机编辑下「应用 ops 得到新文档」的属性测试）。
- **`SnapshotEdit` 的配对不能只按位置**：一次刷新里有两处编辑（例如开头增加、末尾删除）时前缀与后缀都对不上，中间若按位置硬配对，会把状态和选区配到不相干的块上 — `TwoSeparateEdits_*` 守这条。`Shifted` 的位移量是**每个块自己的**（`New.Key - Old.Key`），不是全局一个 delta。
- **冻结前缀永不重解析**：`DocumentComposer.Update` 只解析 `[oldFrozen, newFrozen)` 一次，之后的快照直接复用这些对象 — see `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:66`。因此冻结片段里**只保留与位置无关的 remend 转义修正**（singleTilde / comparisonOperators），否则已完整的文本会被"补全" — see `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:17`、`docs/streammark/streaming-rules.md:45`。
- **永远不冻结最后一个块**：`CanCut` 需要"下一行已到达"才能确认前一块结束 — see `src/TKCode.Arc.StreamMark/Core/Streaming/StreamSegmenter.cs:273`；测试 `LastBlock_IsNeverFrozen`（`tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/SegmenterTests.cs:27`）。
- **边界只前进**：`TryCommit` 有 `boundary <= FrozenEnd` 直接 return — see `src/TKCode.Arc.StreamMark/Core/Streaming/StreamSegmenter.cs:262`；`AppendOnlyList` 的"旧分块永不移动"是这条不变量的底层保证 — see `src/TKCode.Arc.StreamMark/Core/Streaming/AppendOnlyList.cs:3`。
- **一旦出现链接引用定义或脚注定义就停止冻结**（`SawDefinition`，`src/TKCode.Arc.StreamMark/Core/Streaming/StreamSegmenter.cs:41`、`:204`）：定义能改变前文解析结果，只能靠 `Complete()` 的整篇重解析生效；`ReferenceDefinitions_ResolveAfterFinish` 守这条 — `tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/StreamingEquivalenceTests.cs:90`。
- **围栏检测是保守的**：缩进或引用块内的围栏也当作围栏（`_fenceTopLevel` 只影响"闭合后能否冻结"）— see `src/TKCode.Arc.StreamMark/Core/Streaming/StreamSegmenter.cs:209`；宁可少冻结，也不在代码块中间切开。
- **`Update` 的前提是 `from <= FrozenEnd`**：`Advance` 里 `textStart > _scanPos` 直接抛 `ArgumentOutOfRangeException` — `src/TKCode.Arc.StreamMark/Core/Streaming/StreamSegmenter.cs:69`、`:71`。调用方必须先读 `FrozenEnd` 再切片（`tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/SnapshotDiffTests.cs:40` 就是这么做的）。
- **解析异常不打断流**：`Run` 吞掉异常、保留上一个快照、写 `Debug.WriteLine`，只有下一次 `Append` 才会再试 — see `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:222`。所以"界面停了"可能只是解析持续失败，不是丢 token。
- **`Generation` 必须比 `Key` 优先比较**：跨 `Reset`/`Complete` 代时同一 `Key` 不代表同一内容；`SnapshotDiff` 的起点计算依赖 `SharesFrozenWith`（`src/TKCode.Arc.StreamMark/Core/Streaming/SnapshotDiff.cs:28`、`src/TKCode.Arc.StreamMark/Core/Streaming/DocumentSnapshot.cs:55`），只比 `Key` 会漏掉整篇重解析带来的变化。
- **`Key` 是源码起点，不是身份**：`MarkdownBlock.Key => Range.Start`（`src/TKCode.Arc.StreamMark/Core/Model/Blocks.cs:18`）；差异"更新"的判定是 `Range` 或 `ComputeFingerprint()` 变化（`src/TKCode.Arc.StreamMark/Core/Streaming/SnapshotDiff.cs:38`），指纹故意不含 `Range` — see `src/TKCode.Arc.StreamMark/Core/Model/Blocks.cs:22`。
- **冻结本身不产生差异**：块从尾部转成冻结时对象不变，`SnapshotDiff` 不应为它发 Update — `tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/SnapshotDiffTests.cs:70`。
- **`Complete()` 之后的 `Append` 抛异常**，`Reset(text)` 是唯一恢复路径 — `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:75`、`:99`；`Load` 就是 `Reset + Complete`（`:116`）。
- **`LoadNow` 只能用在没有待处理工作的文档上**，否则抛 `InvalidOperationException`；它期间到达的追加只置脏 — `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:130`、`:136`。
- **`StreamPacer` 只在字素簇边界切分**（`StringInfo.GetNextTextElementLength`），未结束时末尾的高代理字符留到下一帧 — see `src/TKCode.Arc.StreamMark/Core/Pacing/StreamPacer.cs:221`、`:232`；CJK 逐字显示、emoji ZWJ 序列不被拆开（`tests/TKCode.Arc.StreamMark.Tests/Core/Pacing/StreamPacerTests.cs:50`）。
- **`MaxLag`（默认 400ms）是硬上限**：`Tick` 用到达队列把"早于 now-MaxLag 的字符"强制纳入本帧计数 — see `src/TKCode.Arc.StreamMark/Core/Pacing/StreamPacer.cs:164`。
- **`BypassThreshold` 会整段放出**（默认 4096 字符，`src/TKCode.Arc.StreamMark/Core/Pacing/StreamPacer.cs:151`）：一次性粘贴的大段文本不做逐字平滑，这不是 bug。
- **`SkipCompletedBlocks` 默认关闭**，开启后按最后一个 `\n\n` 直接放出完整块，只平滑最后一个未完成块 — `src/TKCode.Arc.StreamMark/Core/Pacing/StreamPacer.cs:31`、`:175`。
- 为什么要冻结：逐 token 整篇重解析在 20KB 回答上实测 1.17s / 2.3GB 分配，且无法复用控件 — see `docs/streammark/decisions.md:10`。

## Tests
- 位置：`tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/`（`StreamingEquivalenceTests.cs`、`SegmenterTests.cs`、`SnapshotDiffTests.cs`、`StreamingDocumentTests.cs`）与 `tests/TKCode.Arc.StreamMark.Tests/Core/Pacing/StreamPacerTests.cs`。
- 最关键的一条是属性测试：`EverySnapshot_MatchesFullParseOfPrefix`（`tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/StreamingEquivalenceTests.cs:35`），对 5 个 fixture × 分片 {1,3,7,32,500} 及固定种子随机分片，断言每一步快照与"当前前缀整篇解析"的块类型 / range / 指纹一致，且冻结块对象 `Assert.Same`；fixture 放在 `tests/TKCode.Arc.StreamMark.Tests/Fixtures/streaming/*.md`（6 个文件，见 `StreamingEquivalenceTests.cs:18`、`:143`）。
- `SegmenterTests.FrozenAfter` 逐字符推进切分器，专门覆盖"不能过早冻结"的陷阱；`Boundary_NeverMovesBackwards` 守单调性（`SegmenterTests.cs:9`、`:113`）。
- 命名模式：`<被测语义>_<期望行为>`（如 `OpenFenceAtRunStart_IsReportedForFastPath`、`ListFollowedByBlankAndAnotherItem_IsNotCut`）。
- 运行：整套 `dotnet test TKCode.Arc.slnx`（`README.md:91`）；只跑本模块：`dotnet test tests/TKCode.Arc.StreamMark.Tests/Core/ --filter "FullyQualifiedName~Streaming"`，单文件用类名过滤，例如 `--filter "FullyQualifiedName~SegmenterTests"`。以上两条过滤命令本次未实际运行 (unverified)。

## Related flows
- [streaming-token-to-view](../flows/streaming-token-to-view.md)
- [stream-completion-reparse](../flows/stream-completion-reparse.md)
- [transcript-virtualization-scroll](../flows/transcript-virtualization-scroll.md)
rtualization-scroll.md)
