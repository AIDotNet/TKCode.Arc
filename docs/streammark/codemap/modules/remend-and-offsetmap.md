---
module: remend-and-offsetmap
sources: ["src/TKCode.Arc.StreamMark/Core/Remend/**", "src/TKCode.Arc.StreamMark/Core/Parsing/OffsetMap.cs"]
commit: 44a8f5a
updated: 2026-10-16
---
# Remend 移植与源码偏移映射

把流式 LLM 输出的每个前缀交给 `Remend.Complete` 修补成可渲染的 Markdown（补全未闭合标记、丢掉无法补全的尾部片段），再用 `OffsetMap` 把修补后文本里的位置映射回原始源码，使块 key 与区间在流式与最终解析之间保持一致。
**不负责：** 冻结边界探测（`StreamSegmenter`，`src/TKCode.Arc.StreamMark/Core/Streaming/`）；Markdig 管线、组件白名单与块模型映射（`MarkdownProfile` / `MarkdigMapper`，`src/TKCode.Arc.StreamMark/Core/Parsing/`）；视图层渲染与选区（`src/TKCode.Arc.StreamMark/Avalonia/`）。

## Entry points
| Symbol | File | Role |
|---|---|---|
| `Remend.Complete(string, RemendOptions?)` | `src/TKCode.Arc.StreamMark/Core/Remend/Remend.cs:212` | 唯一公开入口：按优先级跑 handler 链，返回修补后的文本；对返回值再次调用得到相同结果 |
| `RemendOptions` | `src/TKCode.Arc.StreamMark/Core/Remend/Remend.cs:26` | 每个补全项的开关 + `LinkMode` + 自定义 `Handlers` |
| `RemendHandler` | `src/TKCode.Arc.StreamMark/Core/Remend/Remend.cs:21` | 自定义步骤：`Name` / `Func<string,string>` / `Priority`（默认 100） |
| `MarkdownProfileBuilder.AddRemendHandler` | `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdownProfile.cs:AddRemendHandler` | 插件注册点，最终由 `DocumentComposer` 合并进尾部选项 |
| `OffsetMap.Create(int, string, string)` | `src/TKCode.Arc.StreamMark/Core/Parsing/OffsetMap.cs:27` | 由 (raw, healed) 构建映射；`internal`，只在 Core 内使用 |
| `MarkdigMapper.MapDocument` | `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdigMapper.cs:MapDocument` | 消费方：`MapContext.Map` 把 Markdig 的 `Span` 转成源码区间 |

## Key types and functions
| Symbol | File | Role |
|---|---|---|
| `Remend.Complete` | `src/TKCode.Arc.StreamMark/Core/Remend/Remend.cs:212` | 先 `StripSingleTrailingSpace`，再按优先级跑 handler，最后再剥一次尾随单空格；整段包在 `ScanCache.Enter/Exit` 里 |
| `Remend.GetEnabledBuiltInHandlers` | `src/TKCode.Arc.StreamMark/Core/Remend/Remend.cs:143` | 内置 handler 的启用与顺序；优先级常量在 `src/TKCode.Arc.StreamMark/Core/Remend/Remend.cs:93-106` |
| `Remend.GetHandlers` | `src/TKCode.Arc.StreamMark/Core/Remend/Remend.cs:175` | 把自定义 handler 按优先级稳定插入（同优先级内置在前） |
| 内置优先级顺序 | `docs/streammark/streaming-rules.md:30` | 0 singleTilde → 5 comparisonOperators → 10 htmlTags → 15 setextHeadings → 20 links（提前返回）→ 30–42 强调 → 50 inlineCode → 60 strikethrough → 70 katex → 75 inlineKatex |
| `singleTilde` | `src/TKCode.Arc.StreamMark/Core/Remend/SingleTildeHandler.cs:54` | 转义词符间的单个 `~`；候选正则额外放行代理项，由求值器按整个码点判定 |
| `comparisonOperators` | `src/TKCode.Arc.StreamMark/Core/Remend/ComparisonOperatorHandler.cs:40` | 只按行首尝试 `\G` 锚定正则，逐行手动 replace，模拟 JS `gm` |
| `htmlTags` | `src/TKCode.Arc.StreamMark/Core/Remend/HtmlTagHandler.cs:19` | 去掉末尾未完成标签；从最左匹配继续右走，跳过代码 / 数学里的 `<` |
| `setextHeadings` | `src/TKCode.Arc.StreamMark/Core/Remend/SetextHeadingHandler.cs:38` | 末行为 `-`/`--`/`=`/`==` 且上一行有内容时追加 U+200B |
| `links`（唯一 earlyReturn） | `src/TKCode.Arc.StreamMark/Core/Remend/LinkImageHandler.cs:175` | 未完成链接 / 图片换成占位 URL；截断式修复最多循环 32 次（`LinkImageHandler.cs:172`） |
| 强调 30–42 | `src/TKCode.Arc.StreamMark/Core/Remend/EmphasisHandlers.cs` | `***` / `**` / `__` / `*` / `_` 各自按奇偶补全，跳过代码、数学、链接 URL、HTML 标签与词内标记 |
| `inlineCode` / `strikethrough` / `katex` | `src/TKCode.Arc.StreamMark/Core/Remend/InlineCodeHandler.cs:12`、`StrikethroughHandler.cs:16`、`KatexHandler.cs:69` | 分别补反引号（按开启 run 长度）、`~~`、`$$`（`KatexHandler.cs:73` 补单个 `$`） |
| `TextScan` + `Region` + `ScanCache` | `src/TKCode.Arc.StreamMark/Core/Remend/TextScan.cs:73`、`:41`、`:760` | 单遍把文本标成代码 / 正文区域，并按需惰性构建数学、链接 URL、HTML 标签掩码 |
| `RemendUtils` 查询层 | `src/TKCode.Arc.StreamMark/Core/Remend/RemendUtils.cs` | handler 共用的 `IsInsideCodeBlock` / `IsWithinMathBlock` / `IsWithinHtmlTag` / `FindMatchingOpeningBracket` 等 |
| `JsText` | `src/TKCode.Arc.StreamMark/Core/Remend/JsText.cs` | 复刻 TS 依赖的 JS 字符串语义：越界哨兵 `None = -1`、`WhitespaceClass`、`Trim/Substring/LastIndexOf` |
| `Patterns.MatchTrailing` | `src/TKCode.Arc.StreamMark/Core/Remend/Patterns.cs:69` | 只在最后一个分隔符之后的尾部切片上匹配，避免扫全文 |
| `OffsetMap` | `src/TKCode.Arc.StreamMark/Core/Parsing/OffsetMap.cs:9` | 双指针贪心对齐得到插入点集合与发散点，`ToSource`/`SourceEnd` 供 `MarkdigMapper` 使用 |

## Dependencies
- Uses: 无 Core 内模块依赖（`Remend/**` 只用 `System.Text.RegularExpressions`、`System.Buffers.ArrayPool`、`System.Globalization`）— `src/TKCode.Arc.StreamMark/Core/Remend/Remend.cs`、`src/TKCode.Arc.StreamMark/Core/Remend/TextScan.cs:3`；`OffsetMap` 不依赖任何模块 — `src/TKCode.Arc.StreamMark/Core/Parsing/OffsetMap.cs:1`
- Used by: `streaming` via 尾部补全与整篇解析 — `src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:Parse`（以及 `:46` 合并 profile 的 handler）
- Used by: `streaming` via 公开构造参数（未传时用 `RemendOptions.Default`）— `src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs:StreamingDocument`
- Used by: `parsing` via 块区间回写源码位置 — `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdigMapper.cs:204`、`:213`
- Used by: `plugins` via 注册点 — `src/TKCode.Arc.StreamMark/Core/Parsing/MarkdownProfile.cs:AddRemendHandler`（当前仓库内没有调用方，唯一消费点是 `DocumentComposer.cs:46`）

## State it owns
- 扫描缓存是**线程静态**的，作用域限定在一次 `Remend.Complete` 调用内：`ScanCache.Enter()` / `Exit()`（`src/TKCode.Arc.StreamMark/Core/Remend/Remend.cs:226`、`:248`）。状态：`[ThreadStatic] t_text` / `t_scan` / `t_depth` / `t_rented` — `src/TKCode.Arc.StreamMark/Core/Remend/TextScan.cs:762-765`。
- 单条目缓存按**引用**比较（`ReferenceEquals`，`TextScan.cs:778`），命中即复用整份 `TextScan`；未命中只是重扫，不影响结果。
- 区域 / 掩码缓冲：`t_depth > 0` 且 `length >= 256` 时从 `ArrayPool<byte>.Shared.Rent` 租借并记录（`TextScan.cs:789-799`），最外层 `Exit()` 统一 `Return` 并清空列表（`TextScan.cs:813-820`）。`TextScan.Regions` 的长度可能大于 `Length`（`TextScan.cs:93`）。
- `BuildLinkUrlMask` 额外租借一个 `closerFollows` 并在 `finally` 里归还（`TextScan.cs:667`、`:684`）。
- 不缓存、不租借的路径：不在 `Complete` 调用内（`t_depth == 0`，例如测试直接调工具函数）每次新建数组 — `TextScan.cs:774`、`:791`。
- 调用方**不得**跨 `Complete` 调用持有 `TextScan` 或 `Regions`，也不得写入这两个数组。

## Contracts it exposes
- `RemendOptions` 全部开关与默认值：除 `InlineKatex` 外默认 `true`，`InlineKatex` 默认 `false`（`src/TKCode.Arc.StreamMark/Core/Remend/Remend.cs:26-75`，注释说明单个 `$` 常是货币符号，`Remend.cs:49`）。
- `LinkMode.Protocol`（默认）/ `LinkMode.TextOnly` — `src/TKCode.Arc.StreamMark/Core/Remend/Remend.cs:6`；`TextOnly` 不提前返回，`Remend.cs:159` 解释原因（输出永远不会以占位标记结尾）。
- 占位 URL 常量：`Remend.IncompleteLinkUrl = "streammark:incomplete-link"`、`Remend.IncompleteImageUrl = "streammark:incomplete-image"`（`Remend.cs:84`、`:87`）；与 TS 的 `streamdown:` 仅方案名不同、长度相同（`src/TKCode.Arc.StreamMark/Core/Remend/DEVIATIONS.md:14`）。`LinkImageHandler` 与 `Remend` 各有一份私有后缀常量（`LinkImageHandler.cs:10-11`、`Remend.cs:89-90`）。
- 冻结片段与最终整篇解析的选项：`DocumentComposer.SettledOptions` 只保留 `SingleTilde` / `ComparisonOperators`，其余全为 `false`（`src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:21-34`）。
- 自定义 handler 两个注册点：运行期 `RemendOptions.Handlers`（`Remend.cs:74`），配置期 `MarkdownProfileBuilder.AddRemendHandler` → `MarkdownProfile.RemendHandlers`（`MarkdownProfile.cs:AddRemendHandler`）→ 构造 `DocumentComposer` 时与尾部选项合并（`DocumentComposer.cs:46`）。

## How to extend
- To add a remend handler: 在 `src/TKCode.Arc.StreamMark/Core/Remend/` 新增 handler 类（参照 `src/TKCode.Arc.StreamMark/Core/Remend/HtmlTagHandler.cs:19`），然后在 `src/TKCode.Arc.StreamMark/Core/Remend/Remend.cs` 里加优先级常量（`Remend.cs:93-106` 区块）、加 `RemendHandler` 静态字段（`Remend.cs:108-137`）、在 `GetEnabledBuiltInHandlers` 里 `Add(...)`（`Remend.cs:154-171`），最后在 `RemendOptions` 加对应开关（`Remend.cs:26`）。**漏掉 `Add(...)` 一行 → 新开关不起作用，handler 永远不跑**；漏掉 `RemendOptions` 字段 → 无法按需关闭。
- To put it in front of a built-in: 给比内置更小的优先级值（内置占 0–100，`Remend.cs:92`；测试用 `-1` 演示，`tests/TKCode.Arc.StreamMark.Tests/Core/Remend/RemendCustomHandlerTests.cs:58`）。要在冻结片段也生效，则必须**不要**把开关关进 `DocumentComposer.SettledOptions`（`DocumentComposer.cs:21`）；只改 `RemendOptions` 默认值不够。
- To let plugins add it: 调用 `MarkdownProfileBuilder.AddRemendHandler`（`MarkdownProfile.cs:AddRemendHandler`）；漏掉这一步插件 handler 不会进尾部（`DocumentComposer.cs:46` 是唯一合并点），`src/TKCode.Arc.StreamMark/Avalonia/Plugins/` 下目前没有调用方。
- To change how positions map back: 让 handler 只做「插入少量已知字符（`\`、U+200B）或改写 / 截断末尾」这两类操作（`OffsetMap.cs:5-7` 的前置假设），否则要同步改 `OffsetMap.Create` / `ToSource`（`OffsetMap.cs:27`、`:60`）。
- To cover a new function with fixtures: 在 `extract.mjs` 记录的 fixture 里新增，并在 `RemendFixtureTests.Invoke` 加 `fn` 分支，**漏掉分支会抛 `unknown fixture function`**（`tests/TKCode.Arc.StreamMark.Tests/Core/Remend/RemendFixtureTests.cs:172`）。

## Invariants and traps
- C# 线程静态缓存 vs TS 模块级缓存：TS 的单条目缓存keyed by 字符串值、每次结束清空；C# 用线程静态 + 引用比较，作用域限定一次调用（支持自定义 handler 里嵌套调用 `Complete`）。缓存未命中只重扫，结果不变 — see `src/TKCode.Arc.StreamMark/Core/Remend/DEVIATIONS.md:16`、`src/TKCode.Arc.StreamMark/Core/Remend/TextScan.cs:770`。
- 比较运算符用 `\G` + 手动逐行循环，替换 TS 的单条 `/^…/gm`：.NET `Multiline` 的 `^` 只跟在 `\n` 之后，循环按 JS 行终止符（`\n` `\r` U+2028 U+2029）推进 — see `src/TKCode.Arc.StreamMark/Core/Remend/DEVIATIONS.md:17`、`ComparisonOperatorHandler.cs:54`、`:27`。
- 单个波浪号：JS `u` 标志按码点看，`.NET` 字符类只看 UTF-16 单元，所以候选正则放行代理项、再由 `WordCodePointEndingAt/StartingAt` 按 `Rune` 判定 — see `src/TKCode.Arc.StreamMark/Core/Remend/DEVIATIONS.md:18`、`SingleTildeHandler.cs:22`、`:31`。
- JS 正则语义其余部分显式复刻：`$` 无 `m` 等价于 `\z`，JS `\s` 展开成 `JsText.WhitespaceClass`（含 U+FEFF、不含 U+0085），`\d` 用 `[0-9]`，`.` 排除四种 JS 行终止符 — see `src/TKCode.Arc.StreamMark/Core/Remend/Patterns.cs:3-5`、`JsText.cs:20`。
- `Complete` 幂等：文档注释承诺「对返回值再次调用会得到相同结果」（`Remend.cs:211`），末尾的多余空格被剥两次（`Remend.cs:222`、`:244`）。
- links 是唯一 `earlyReturn`：结果以占位后缀结尾时整条链立即结束，后面的 handler 与自定义 handler 都不跑 — see `Remend.cs:235`、`tests/TKCode.Arc.StreamMark.Tests/Core/Remend/RemendCustomHandlerTests.cs:127`。
- 补全只作用于**尾部**；冻结片段与最终整篇解析只做转义修正（singleTilde / comparisonOperators），否则已完整的文本会被"补全" — see `docs/streammark/streaming-rules.md:45`、`src/TKCode.Arc.StreamMark/Core/Streaming/DocumentComposer.cs:21`。
- 同优先级时顺序稳定且内置在前：插入循环用严格 `>` 比较 — see `Remend.cs:189`、`tests/TKCode.Arc.StreamMark.Tests/Core/Remend/RemendCustomHandlerTests.cs:84`。
- `OffsetMap` 只认「插入 `\` / U+200B」与「末尾改写」两种变化：一旦在中间遇到其他差异就置 `_divergence`，其后位置一律钳到原文末尾附近（`OffsetMap.cs:42`、`:52`、`:69`）。mid-document 重写会让中间块的区间塌缩到源末尾。
- 截断式链接修复有 32 次上限，防止病态嵌套尾部把补全变成二次复杂度（`LinkImageHandler.cs:172`、`:185`）。
- 缓存按引用比较：handler 返回一个**内容相同但实例不同**的字符串会强制重扫（各 handler 大量返回同一个 `text` 实例来避免这一点，例如 `HtmlTagHandler.cs:22`）。
- `Complete(null!)` 返回 null、`Complete("")` 返回空串（`Remend.cs:214`），TS 侧对 `null` / `123` 也是原样返回 — see `src/TKCode.Arc.StreamMark/Core/Remend/DEVIATIONS.md:15`。

## Tests
- 位置：`tests/TKCode.Arc.StreamMark.Tests/Core/Remend/` 三个文件 —— `RemendFixtureTests.cs`（数据驱动回放）、`RemendCustomHandlerTests.cs`（带函数 handler 的用例手工移植，`RemendCustomHandlerTests.cs:4`）、`RemendStreamingPropertyTests.cs`（TS 属性测试的种子化重写）。
- fixture：`tests/TKCode.Arc.StreamMark.Tests/Fixtures/remend/*.json`，29 个文件、每个对应一个原 `*.test.ts`（`RemendFixtureTests.cs:38` 断言这个数量）；由 `tools/streammark/remend-fixtures/extract.mjs` 生成，用法见 `tools/streammark/remend-fixtures/extract.mjs:9-11`，配套 `hooks.mjs` / `recorder.mjs` / `vitest-shim.mjs`；提取时把 `streamdown:incomplete-` 改写成 `streammark:incomplete-`（`extract.mjs:44`），属性测试只采样 600 次、穷举前缀扫描不封顶（`extract.mjs:34-35`、`:59`）。
- 命名模式：文件名 = 原测试文件名去掉 `.test.ts`（`extract.mjs:63`）；测试内每个 case 带 `fn`（如 `index.remend`）与 `args`，由 `RemendFixtureTests.Invoke` 的 `fn switch` 分发（`RemendFixtureTests.cs:155`）。
- 运行：整套 `dotnet test TKCode.Arc.slnx`（`README.md`）；只跑 remend：`dotnet test tests/TKCode.Arc.StreamMark.Tests/Core/ --filter "FullyQualifiedName~RemendFixtureTests"`；只跑某一个 fixture 文件要靠 Theory 显示名过滤（`Files` 由目录枚举生成，`RemendFixtureTests.cs:21`），例如 `--filter "DisplayName~bold"`（**未实际运行验证**）。
- 另有 `ConcurrentCallsMatchFixtures` 并发回放全部 `index.remend` 用例，专门守护线程静态缓存（`RemendFixtureTests.cs:65`）。

## Related flows
- [streaming-token-to-view](../flows/streaming-token-to-view.md)
- [stream-completion-reparse](../flows/stream-completion-reparse.md)

两个 flow 页面在本次快照的 `.agents/` 下尚不存在，(unverified)。
