---
module: tests
sources: ["tests/**"]
commit: e01b99a
updated: 2026-10-16
---
# 测试项目

两个 xunit.v3 测试项目：`StreamMark.Core.Tests` 是纯逻辑测试，靠从 vercel/streamdown 录制的 JSON golden fixture 回放 remend，并对流式快照做属性测试；`StreamMark.Avalonia.Tests` 用 `Avalonia.Headless` + Skia **真实渲染**，通过 `HeadlessApp` 把控件显示在无头窗口里，断言控件树 / 布局 / 选区，并把 PNG 写到截图目录供人查看。
**不负责：** 生产代码（`src/**`，见各业务模块页面）；基准测试（`tools/streammark/Benchmarks`）；Gallery 的 AOT 冒烟（`.github/workflows/ci.yml:121` 的 `--smoke`）；fixture 的生成脚本本身（`tools/streammark/remend-fixtures/extract.mjs`，本模块只消费它的产物）。

## Entry points
| Symbol | File | Role |
|---|---|---|
| `HeadlessApp.BuildAvaloniaApp` | `tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:26` | `AppBuilder`：`UseSkia` + `UseHarfBuzz` + `UseHeadless(UseHeadlessDrawing = false)` |
| `HeadlessApp.Session` | `tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:34` | 全测试进程共享的 `HeadlessUnitTestSession`（`Lazy<>` 单例） |
| `HeadlessApp.Run` | `tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:55` | 在界面线程上跑异步测试体并**等它结束**（`Dispatch` 里返回 `true` 走 `Func<TResult>` 重载） |
| `HeadlessApp.Settle` | `tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:65` | `RunJobs` + `ForceRenderTimerTick` + `Task.Delay(20)`，重复 `rounds` 轮，让异步更新落定 |
| `HeadlessApp.Fixture` | `tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:76` | 读 `AppContext.BaseDirectory/Fixtures/<name>` 并把 `\r\n` 归一成 `\n` |
| `HeadlessApp.Save` / `SnapshotPath` / `Capture` | `tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:44` / `:37` / `:79` | 截图并写 PNG；目录 = `STREAMMARK_SNAPSHOT_DIR` 或输出目录下 `snapshots/`；`CaptureRenderedFrame()` 为 null 时抛异常 |
| `TestApp` | `tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:13` | 测试用 `Application`：暗色变体 + `FluentTheme` + `StreamMarkTheme` |
| `StreamingEquivalenceTests.Cases` | `tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/StreamingEquivalenceTests.cs:15` | `TheoryData<string,int>`：5 个 markdown fixture × 分片 {1,3,7,32,500} + 4 个固定种子随机分片 |
| `RemendFixtureTests.Files` | `tests/TKCode.Arc.StreamMark.Tests/Core/Remend/RemendFixtureTests.cs:21` | 扫 `Fixtures/remend/*.json` 得到 theory 数据 |

## Key types and functions
| Symbol | File | Role |
|---|---|---|
| `HeadlessApp.Run`/`Settle` 的调用范式 | `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/MarkdownViewSnapshotTests.cs:13` | `Show()`：建 `Window` → `Show()` → `Settle()` → `view.ApplyLatest()` → `Settle()`；所有渲染类测试都是这个骨架 |
| `MarkdownViewSnapshotTests.Streaming_UpdatesOnlyTail_AndReusesFrozenViews` | `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/MarkdownViewSnapshotTests.cs:45` | 每 9 字符 `Append` + `await document.WhenIdle()` + `ApplyLatest()`，用 `Assert.Same` 断言第一段在整个流式过程中只创建一次 |
| `StreamingEquivalenceTests.EverySnapshot_MatchesFullParseOfPrefix` | `tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/StreamingEquivalenceTests.cs:35` | 核心属性：分片驱动 `DocumentComposer.Update`，每步与"当前前缀整篇解析"逐块比类型 / `Range` / `ComputeFingerprint()`，并 `Assert.Same` 已冻结块 |
| `StreamingEquivalenceTests.AssertSameBlocks` | `tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/StreamingEquivalenceTests.cs:105` | 一致性断言的唯一实现（块数、类型、range、指纹），失败信息带上下文 |
| `StreamingEquivalenceTests.Split` / `Fixture` | `tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/StreamingEquivalenceTests.cs:123` / `:143` | `chunk <= 0` 时用固定种子 `Random(-chunk*7919)` 产生 1–24 的随机分片；`Fixture` 被同项目另外 3 个测试类复用（`SnapshotDiffTests.cs:33`、`SegmenterTests.cs:115`、`StreamingDocumentTests.cs:12`） |
| `SnapshotDiffTests.Apply` / `AppliedOps_ReproduceEverySnapshot` | `tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/SnapshotDiffTests.cs:9` / `:31` | 把 `SnapshotDiff.Compute` 的 ops 顺序应用回上一快照，断言每步重现 `Key` + 指纹 |
| `RemendFixtureTests.Fixture` / `Invoke` / `Matches` | `tests/TKCode.Arc.StreamMark.Tests/Core/Remend/RemendFixtureTests.cs:43` / `:150` / `:176` | 按 `fn` 字段分发到 `Remend.Complete` / `RemendUtils.*` / `Patterns.*`，逐 case 比较 P/Λ/`string[]` 值 |
| `RemendFixtureTests.ConcurrentCallsMatchFixtures` | `tests/TKCode.Arc.StreamMark.Tests/Core/Remend/RemendFixtureTests.cs:65` | 8 线程并发重放全部用例（`Parallel.For`），抓线程静态扫描缓存的串扰 |
| `RemendFixtureTests.EveryTestFileHasFixtures` | `tests/TKCode.Arc.StreamMark.Tests/Core/Remend/RemendFixtureTests.cs:35` | 钉死 fixture 文件数必须等于 29 |
| `RemendStreamingPropertyTests.CheckStreamingSafe` | `tests/TKCode.Arc.StreamMark.Tests/Core/Remend/RemendStreamingPropertyTests.cs:143` | 三个性质：代码节点不被改动、作者文本的丢失不超过 `MaxLoss`（`:54`）、补全幂等；oracle 是 Markdig 整篇解析（`:77`、`:81`） |
| `RemendCustomHandlerTests` | `tests/TKCode.Arc.StreamMark.Tests/Core/Remend/RemendCustomHandlerTests.cs:11` | 带 C# 委托 handler 的调用无法录成 JSON，手工移植（文件头注释说明） |
| `InlineStyleProbeTests.InlineStyles_Render` | `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/InlineStyleProbeTests.cs:13` | 从 `TextLine.TextRuns` 取真实 `ShapedTextRun` 检查 CJK 粗体字面；系统只有一个字重（`available <= regularFace.Weight`）时不判失败 |
| `ImageTestSupport`（`TestPng`/`FakeImage`/`TestImageServer`/`ManualTimeProvider`/`TempDir`） | `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Images/ImageTestSupport.cs:11` / `:112` / `:128` / `:97` / `:246` | 图片测试的整套替身：自造可解码 PNG、可观察 `Dispose` 的假图、本地 `HttpListener` 服务器、手动时钟、临时目录 |
| `VirtualizationScaleTests.HundredThousandBlocks_ScrollAndAppend_StayCheap` | `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/VirtualizationScaleTests.cs:13` | 10 万块规模：`RealizedCount` 上限、跳转耗时上限、贴底 |
| `RunCacheBenchTests.StreamingParagraph_LayoutCost_WithAndWithoutRunCache` | `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/RunCacheBenchTests.cs:52` | 开 / 关整形缓存各测一遍排版耗时（只 `WriteLine` 数字），断言只检查文本正确性 |

## Dependencies
- Uses: `core-streaming` via `InternalsVisibleTo` + 项目引用 — 测试可直接 `new DocumentComposer()` / `new StreamSegmenter()`，`tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/StreamingEquivalenceTests.cs:38`；放行声明在 `src/TKCode.Arc.StreamMark/Core/StreamMark.Core.csproj:9`、`:10`
- Uses: `remend-and-offsetmap` via JSON fixture 回放 — `tests/TKCode.Arc.StreamMark.Tests/Core/Remend/RemendFixtureTests.cs:19`（目录）、`:157`（`Remend.Complete`）
- Uses: `core-parsing-and-model` via `DocumentComposer(profile).Parse` — `tests/TKCode.Arc.StreamMark.Tests/Core/Parsing/ComponentTests.cs:12`；块模型断言 `tests/TKCode.Arc.StreamMark.Tests/Core/Parsing/MapperTests.cs:14`
- Uses: `avalonia-text-rendering` / `avalonia-transcript` / `avalonia-images` / `avalonia-plugin-system` / `avalonia-selection` / `avalonia-extensions` via `ProjectReference` + `InternalsVisibleTo("StreamMark.Avalonia.Tests")` — `tests/TKCode.Arc.StreamMark.Tests/Avalonia/StreamMark.Avalonia.Tests.csproj:16`、`src/TKCode.Arc.StreamMark/Avalonia/Images/AssemblyInfo.cs:3`
- Uses: 三个可选子包 via `ProjectReference` — `tests/TKCode.Arc.StreamMark.Tests/Avalonia/StreamMark.Avalonia.Tests.csproj:17`（RaTeX）、`:18`（Highlighting.TextMate）、`:19`（Mermaid）；放行声明 `src/TKCode.Arc.StreamMark.TextMate/StreamMark.Highlighting.TextMate.csproj:14`、`src/TKCode.Arc.StreamMark.Mermaid/StreamMark.Mermaid.csproj:24`
- Uses: `build-and-packaging` via `Directory.Build.targets`（非 packable 项目按 RID 把 `artifacts/native/<rid>/ratex_ffi.*` 复制进测试输出）与 `Directory.Packages.props:24`（xunit.v3 3.2.2 / Avalonia 12.1.4 集中版本）
- Used by: `build-and-packaging` via CI 步骤 — `.github/workflows/ci.yml:76`（`dotnet test TKCode.Arc.slnx -c Release --no-build`），`build-test` 依赖 `native-ratex`（`:53`、`:20`）
- Used by: 其他模块的文档 — 各页面在 `## Tests` 段落引用具体测试文件
- Used by: 没有代码引用测试项目：`TKCode.Arc.slnx` 只把它们列在 `/tests/` 文件夹下

## State it owns
- **测试进程级禁止并行**：`[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]` — `tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:8`。整个 Avalonia 测试程序集串行跑。
- **共享无头会话**：`Lazy<HeadlessUnitTestSession> Shared`，`Session` 属性取同一实例 — `tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:32`、`:34`；`Run` 每次都 `Dispatch` 到这一条界面线程（`:57`）。
- **截图输出目录**：`STREAMMARK_SNAPSHOT_DIR`，未设置时 `AppContext.BaseDirectory/snapshots`（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:39`）；CI 把它指到 `artifacts/snapshots` 并作为 artifact 上传 — `.github/workflows/ci.yml:78`、`:82`。目录会被 `Directory.CreateDirectory` 自动建（`:40`）。本机现存的 30 个 PNG 在 `tests/TKCode.Arc.StreamMark.Tests/Avalonia/bin/Debug/net10.0/snapshots/`。
- **PNG 只是产物，没有基线比对**：仓库内没有 `xunit.runner.json`、没有 approval/图像 diff 依赖（搜索 `baseline|Verify(|ImageDiff` 只命中数学基线坐标，如 `tests/TKCode.Arc.StreamMark.Tests/Avalonia/Math/RaTeXMathRendererTests.cs:201`）。断言全部是结构 / 数值，PNG 给人看。
- **fixture 复制方式（两个项目不一样）**：Core 侧 `Fixtures\**\*` 整体 `PreserveNewest` 保留目录层级（`tests/TKCode.Arc.StreamMark.Tests/Core/StreamMark.Core.Tests.csproj:17`），所以稳到 `Fixtures/remend/*.json` 与 `Fixtures/streaming/*.md`；Avalonia 侧**跨项目 glob** 只取 `streaming\*.md` 并用 `Link="Fixtures\%(Filename)%(Extension)"` 摊平一层（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/StreamMark.Avalonia.Tests.csproj:22`），因此 `HeadlessApp.Fixture("chat-mixed.md")` 传裸文件名（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/MarkdownViewSnapshotTests.cs:29`）。
- **`InternalsVisibleTo` 让 Core 内部类型可测**：`StreamMark.Core` → `StreamMark.Core.Tests`、`StreamMark.Benchmarks`（`src/TKCode.Arc.StreamMark/Core/StreamMark.Core.csproj:9`、`:10`）；Avalonia 侧只开了 `Images` 程序集级（`src/TKCode.Arc.StreamMark/Avalonia/Images/AssemblyInfo.cs:3`）。
- **测试项目是可执行程序**：`<OutputType>Exe</OutputType>`（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/StreamMark.Avalonia.Tests.csproj:3`），入口由 xunit.v3 生成：`tests/TKCode.Arc.StreamMark.Tests/Avalonia/obj/Debug/net10.0/XunitAutoGeneratedEntryPoint.cs:8`（非 `--server` 参数时走 `InProc.SystemConsole.ConsoleRunner`）。
- **全局警告配置**：两个项目都 `<NoWarn>$(NoWarn);xUnit1051</NoWarn>`（`tests/TKCode.Arc.StreamMark.Tests/Core/StreamMark.Core.Tests.csproj:5`），根 `Directory.Build.props` 把 `IsPackable`/AOT 分析/XML 文档限定在 `src/` 下，所以测试项目不生成文档文件。

## Contracts it exposes
- **命名**：`<被测语义>_<期望行为>`，例如 `TypingIntoLastParagraph_ProducesSingleUpdate`（`tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/SnapshotDiffTests.cs:57`）、`LastBlock_IsNeverFrozen`（`tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/SegmenterTests.cs:27`）、`DeferredEntries_ParseOnlyNearViewport`（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/DeferredEntryTests.cs:17`）。纯逻辑测试用 `PascalCase.Member` 命名；带下划线的便宜读版本出现在图片缓存测试里（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Images/ImageCacheTests.cs:76` `Clear_defers_disposal_of_leased_images`）。
- **`[Fact]` 写在哪一层**：需要无头渲染的测试写成 `public async Task X()`，**函数体第一句就是 `await HeadlessApp.Run(async () => { ... })`**（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/LongCodeTests.cs:13`、`:15`）。不要把 `async` lambda 直接交给 `HeadlessApp.Session.Dispatch`——`tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:52-54` 的注释写明它会匹配 `Func<TResult>` 重载，外层任务在测试体完成前就返回。仓库里唯一直接用 `Dispatch` 的是 `ThemeSnapshotTests.DefaultTheme_ResolvesResources_AndRenders`，它返回 `Task.CompletedTask` 并同步断言（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/ThemeSnapshotTests.cs:14-27`）。
- **参数化**：数据在编译期就用 `[Theory]` + `[InlineData]`（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Mermaid/MermaidRendererTests.cs:97-103`）；数据来自文件系统用 `[MemberData(nameof(...))]` 指一个 `TheoryData<T>` 静态属性（`tests/TKCode.Arc.StreamMark.Tests/Core/Remend/RemendFixtureTests.cs:21`、`tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/StreamingEquivalenceTests.cs:15`）。
- **断言数据来自哪里**：remend 的行为来自 JSON fixture 的 `expected` 字段（不是手写常量）；流式行为来自 `tests/TKCode.Arc.StreamMark.Tests/Fixtures/streaming/*.md` 原文对照整篇解析；Avalonia 渲染断言来自实际控件树 / `TextLine.TextRuns` / `SelectionController.GetSelectedText()`（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/SelectionAndFadeTests.cs:31`）。
- **只跑单个文件 / 单个用例**：仓库里没有现成写法。xunit 通用写法是 `dotnet test tests/TKCode.Arc.StreamMark.Tests/Core/ --filter "FullyQualifiedName~StreamingEquivalenceTests"`（单类）或 `--filter "FullyQualifiedName~StreamingEquivalenceTests.EverySnapshot_MatchesFullParseOfPrefix"`（单用例）；Avalonia 侧同理换项目路径 (unverified，本次未执行)。

## How to extend
- **新增 remend fixture**：先在 remend 上游跑 `node tools/streammark/remend-fixtures/extract.mjs <pkgDir> <depsDir> <outDir>`（用法与依赖见 `tools/streammark/remend-fixtures/extract.mjs:9-11`，它会断言 TS 套件自身通过，`:75`），把生成的 `<name>.json` 放进 `tests/TKCode.Arc.StreamMark.Tests/Fixtures/remend/`，**然后改 `RemendFixtureTests.EveryTestFileHasFixtures` 里的 29**（`tests/TKCode.Arc.StreamMark.Tests/Core/Remend/RemendFixtureTests.cs:35-38`）。漏掉第二步 → 该断言失败，即使回放全过。若新 fixture 引入了新的 `fn` 名，还要在 `Invoke` 的 switch 里加分支（`:150-173`），否则每个 case 都报 `unknown fixture function`；新的 `linkMode` / 选项名同理在 `ParseOptions` 里加（`:99-134`）。
- **新增 markdown fixture**：把 `<name>.md` 放到 `tests/TKCode.Arc.StreamMark.Tests/Fixtures/streaming/`。Core 侧靠 `Fixtures\**\*` 自动进输出目录（`tests/TKCode.Arc.StreamMark.Tests/Core/StreamMark.Core.Tests.csproj:17`）；Avalonia 侧靠跨项目 glob 自动摊平复制（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/StreamMark.Avalonia.Tests.csproj:22`），两端因此**同时**能看到这个文件（`HeadlessApp.Fixture` 与 `StreamingEquivalenceTests.Fixture`）。但**必须把它加进 `StreamingEquivalenceTests.Cases`**（`tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/StreamingEquivalenceTests.cs:18`），否则它只是一个被复制的死文件，没有任何等价性断言覆盖；只在 Avalonia 侧用则直接 `HeadlessApp.Fixture("<name>.md")` 即可（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/TranscriptViewTests.cs:46`）。
- **新增需要真实渲染的测试**：新建 `tests/TKCode.Arc.StreamMark.Tests/Avalonia/<Area>/<Thing>Tests.cs`，复制 `HeadlessApp.Run` + `Window.Show()` + `Settle()` + `ApplyLatest()` + `Settle()` 骨架（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/MarkdownViewSnapshotTests.cs:13-22`），断言后可选 `HeadlessApp.Save(window, "<name>")` 出图。若要用内部类型，先确认目标程序集有 `InternalsVisibleTo("StreamMark.Avalonia.Tests")`——目前只有 `Images/AssemblyInfo.cs`、TextMate、Mermaid 三处，`StreamMark.Avalonia` 其余部分没有；漏掉会被编译期可访问性报错挡住（不是运行时症状）。
- **新增会改全局静态状态的测试**：必须像 `RunCacheBenchTests` 那样 `try/finally` 复位（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/RunCacheBenchTests.cs:16`、`:47`），因为程序集禁并行但共享同一进程与 `Session`，泄漏的静态开关会污染后跑的测试。
- **加一条性能上限断言**：参考 `LongCodeTests` 的写法——先 warm-up，再取 `Skip(1).Order()` 后比较中位数与最大值，并用 `Environment.GetEnvironmentVariable("CI")` 放宽阈值（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/LongCodeTests.cs:53-58`，提交 e01b99a 就是这么改的）。只断言最大值会在共享 CI 机器上偶发失败。

## Invariants and traps
- **无头测试必须走 `HeadlessApp.Run` + `Settle`**：`Run` 用 `Dispatch(async () => { await body(); return true; }, ...)` 强制走 `Func<Task<bool>>` 重载，从而真正等测试体结束 — `tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:52-61`。把 async lambda 直接交给 `Dispatch` 会被当成 `Func<TResult>`（返回的是未完成的 `Task`，外层立刻返回），测试提前绿。
- **`UseHeadlessDrawing = false` 才能截图** — `tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:30`；`Capture` 依赖 `TopLevel.CaptureRenderedFrame()`，拿不到帧直接抛 `InvalidOperationException("No frame rendered.")`（`:79-80`）。
- **测试禁并行** — `tests/TKCode.Arc.StreamMark.Tests/Avalonia/HeadlessApp.cs:8`；原因是共享 `HeadlessUnitTestSession` 与唯一的界面线程（`:32`）。Core 侧没有这条，靠 `Parallel` 内部并发（`tests/TKCode.Arc.StreamMark.Tests/Core/Remend/RemendFixtureTests.cs:84`）来测并发安全。
- **共享会话 ⇒ 状态会跨测试残留**：`AppBuilder` 只建一次；`RunCacheBenchTests` 直接改静态 `TextSegmentView.UseRunCache`（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/RunCacheBenchTests.cs:16`），`MarkdownViewSnapshotTests` 每例都新开 / 关闭 `Window`（`:17`、`:40`）。新测试不要假设"干净的应用状态"。
- **回归守卫（文件名 → 它守住的回归）**：
  - `SmoothStreamWrapTests.SmoothReveal_WrapsAsOneParagraph` → 平滑渐显时每次放出的边界不能变成硬换行；守住 `TextRunCache` 必须从**段落起点**失效而不是从差异点失效（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/SmoothStreamWrapTests.cs:11`、`:40`；修复提交 c2c3d5c，`docs/streammark/decisions.md:41`）。
  - `StreamingEquivalenceTests.EverySnapshot_MatchesFullParseOfPrefix` → 冻结片段与整篇解析一致；任何"过早冻结"的切分器改动都会在这里以块类型 / range / 指纹不符炸出来（`tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/StreamingEquivalenceTests.cs:105`）。
  - `SnapshotDiffTests.FreezingABlock_DoesNotProduceAnUpdateForIt` → 块从尾部转冻结时不能产生 Update（`tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/SnapshotDiffTests.cs:70-80`）。
  - `InlineStyleProbeTests.InlineStyles_Render` → 中文段落里的粗体必须用粗字重的回退字体；**只有系统确实装了更粗的字面时才会失败**，否则只比较 `available <= regularFace.Weight`（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/InlineStyleProbeTests.cs:34-44`，提交 c3ae90e）。
  - `LongCodeTests.ThreeThousandLineCode_IsChunked_AndCopiesExactly` → 长代码块被切成多段（断言 3000 行 → 24 段 + 前后两段 = 26 行）、跳转排版不能退化成整块排版、跨分段的选区复制精确（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/LongCodeTests.cs:38`、`:57-58`、`:76`）。
  - `VirtualizationScaleTests` → 10 万块下 `RealizedCount` 与滚动耗时（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/VirtualizationScaleTests.cs:33`、`:48`）。
  - `TranscriptViewTests.StreamingEntry_StaysPinned_AndOlderContentDoesNotMove` → 贴底与"用户上滚后视口不动"（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Transcript/TranscriptViewTests.cs:73`）。
  - `RemendFixtureTests.ConcurrentCallsMatchFixtures` → 线程静态扫描缓存的互不干扰（`tests/TKCode.Arc.StreamMark.Tests/Core/Remend/RemendFixtureTests.cs:63`）。
- **`SegmenterTests.FrozenAfter` 是逐字符推进的**（`tests/TKCode.Arc.StreamMark.Tests/Core/Streaming/SegmenterTests.cs:9-16`）：这些"不能过早冻结"的坑无法用整段一次性喂进去重现，改测试时不要把它简化成一次性调用。
- **`StreamingEquivalenceTests.Fixture` 是三个测试类共用的**（`SnapshotDiffTests.cs:33`、`SegmenterTests.cs:115`、`StreamingDocumentTests.cs:12`）：改它的路径或换行归一化会同时影响这几个类。
- **`RemendFixtureTests.EveryTestFileHasFixtures` 把 fixture 数量钉成 29**（`:38`）：新增或删除 `Fixtures/remend/*.json` 都必须同步改这个数字，否则测试红但回放本身没问题。
- **`RemendStreamingPropertyTests` 是种子化的，不是随机的**：`Seed = 20231031`（`:56`）加 `Seed+1..+4`，失败可复现；`MaxLoss = 最长 atom + 2`（`:54`）是"允许丢多少作者文本"的唯一上界，放宽它等于放宽补全的正确性契约。
- **`RemendCustomHandlerTests` 手工维护**：带 C# 委托的调用无法录成 JSON（`tests/TKCode.Arc.StreamMark.Tests/Core/Remend/RemendCustomHandlerTests.cs:3-4`），改 handler 语义时 JSON 回放不会替你发现回归。
- **索引 / 顺序敏感**：`SelectionAndFadeTests` 直接 `snapshot[1]`、`snapshot[3]`（`tests/TKCode.Arc.StreamMark.Tests/Avalonia/Rendering/SelectionAndFadeTests.cs:28-29`），`LongCodeTests` 取 `snapshot[1].Key`（`:70`）——它们隐式依赖 markdown 里块的顺序与数量，改测试用的 markdown 字符串会连带改这些下标。
- **依赖本机产物**：`RaTeXEngineTests` 需要已跑过 `tools/streammark/build-ratex.sh` 的原生库（`tests/TKCode.Arc.StreamMark.Tests/Core/RaTeX/RaTeXEngineTests.cs:6`）；CI 里 `build-test` 必须 `needs: native-ratex` 并先下载对应 RID 的 artifact（`.github/workflows/ci.yml:53`、`:70-73`）。Avalonia 侧 `RaTeXMathRendererTests` / `MermaidRendererTests` / `TextMateHighlighterTests` 同理依赖真实渲染与语法包。
- **测试通过 `ProjectReference` 而非 NuGet 引用库**（两个 csproj 的 `ItemGroup`）：改 `src/` 立刻影响测试，反之测试不能靠锁版本规避行为变化。

## Tests
本模块自身就是测试，没有更外层的测试。运行方式（配置中确认的）：整套 `dotnet test TKCode.Arc.slnx`（`README.md:91`），CI 用的是 `dotnet build TKCode.Arc.slnx -c Release` 后 `dotnet test TKCode.Arc.slnx -c Release --no-build`（`.github/workflows/ci.yml:74`、`:76`）。单项目：`dotnet test tests/TKCode.Arc.StreamMark.Tests/Core/` / `dotnet test tests/TKCode.Arc.StreamMark.Tests/Avalonia/`（未实际执行）。单文件 / 单用例：xunit 通用 `--filter "FullyQualifiedName~<Class>"` 或 `--filter "FullyQualifiedName~<Class>.<Method>"` (unverified，仓库内没有现成写法或脚本可抄)。版本来自 `Directory.Packages.props:24`：`Microsoft.NET.Test.Sdk` 18.11.0、`xunit.v3` 3.2.2、`xunit.runner.visualstudio` 3.1.5。**本次没有运行任何测试或构建**，以上命令均取自配置而非执行结果。

## Related flows
- [streaming-token-to-view](../flows/streaming-token-to-view.md)
- [stream-completion-reparse](../flows/stream-completion-reparse.md)
- [text-selection-copy](../flows/text-selection-copy.md)
- [raw-html-render](../flows/raw-html-render.md)
