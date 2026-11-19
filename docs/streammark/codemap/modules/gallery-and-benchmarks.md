---
module: gallery-and-benchmarks
sources: ["samples/**", "bench/**"]
commit: e01b99a
updated: 2026-10-16
---
# 示例程序与基准

`tools/streammark/Gallery` 是一个 Avalonia 桌面演示程序：一个窗口、一个会话视图、一条模拟流式回答，同时它也是仓库唯一的 NativeAOT 冒烟测试（`--smoke` 分支，靠退出码当门禁）。`tools/streammark/Benchmarks` 是独立的 BenchmarkDotNet 项目，只做“整篇重解析 vs StreamMark 冻结前缀”和 58k 尾部 remend 两组对比。
**不负责：** 渲染控件与插件系统本身（`avalonia-plugin-system`、`avalonia-text-rendering`、`avalonia-transcript`），流式引擎与节奏控制（`core-streaming`），AOT 发布参数 / IL 警告门禁 / CI 任务编排（`build-and-packaging`，`.github/workflows/ci.yml`），原生库怎么编与怎么被复制到输出目录（`native-ratex`、`Directory.Build.targets`），记录下来的实测数字（`docs/streammark/benchmarks.md`）。

## Entry points
| Symbol | File | Role |
|---|---|---|
| `Program.Main` | `tools/streammark/Gallery/Program.cs:20` | 唯一的进程入口：`args.Contains("--smoke")` 时直接 `return SmokeTest.Run(args)`（`:22-25`），否则走 `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`（`:26`）；返回 `int` 而不是 `void`，就是为了让 `--smoke` 能把退出码带给 CI |
| `SmokeTest.Run` | `tools/streammark/Gallery/SmokeTest.cs:21` | `--smoke` 的实现：逐项 `Check`（`ratex.*`、`stream.*`、`ui.*` 共 14 个可能的名字）+ `SMOKE OK` / `SMOKE FAILED (n)` 输出，`return failures == 0 ? 0 : 1`（`:75`） |
| `Program.BuildAvaloniaApp` | `tools/streammark/Gallery/Program.cs:29` | `AppBuilder.Configure<GalleryApp>().UsePlatformDetect()`；冒烟测试不用它，自己另起一套 Skia + HarfBuzz + Headless 的 `AppBuilder`（`tools/streammark/Gallery/SmokeTest.cs:80-84`） |
| `Program`（隐式 `Main`） | `tools/streammark/Benchmarks/Program.cs:3` | 基准项目全部入口：一行 `BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args)`，参数（`--filter`、`--job`、`--exporters`）原样透传给 BenchmarkDotNet |
| `GalleryApp` | `tools/streammark/Gallery/Program.cs:55` | 应用级初始化：固定 `ThemeVariant.Dark`（`:59`），挂 `FluentTheme` + `StreamMarkTheme`（`:60-61`），桌面生命周期下把 `MainWindow` 设为 `GalleryWindow`（`:68`） |
| `GalleryWindow` | `tools/streammark/Gallery/Program.cs:75` | 演示窗口本体：装配 `MarkdownServices`、一个 `TranscriptView`（`:87`）、一条静态示例 + 一个“模拟流式回答”按钮（`:91-96`） |
| `GalleryWindow.SimulateAsync` | `tools/streammark/Gallery/Program.cs:115` | 用固定种子 `new Random(7)`（`:122`）把 `showcase.md` 切成 2–23 字符的随机分片推入 `MarkdownStream`，`delayMs` 控制每片之间的等待；冒烟测试以 `delayMs: 0` 复用它（`tools/streammark/Gallery/SmokeTest.cs:88`） |
| `StreamingBenchmarks` | `tools/streammark/Benchmarks/StreamingBenchmarks.cs:14` | 20KB 聊天式文档、按 4 字符一个 token 流完的两组对比（`[MemoryDiagnoser]`，`:13`） |
| `RemendBenchmarks` | `tools/streammark/Benchmarks/StreamingBenchmarks.cs:48` | 单方法 `Heal58kTail`，量 `Remend.Complete` 在 58k 尾部块上的耗时与分配（`:61`） |

## Key types and functions
| Symbol | File | Role |
|---|---|---|
| `Program.CreateServices` | `tools/streammark/Gallery/Program.cs:33` | 演示用的插件集合：`RaTeXPlugin` + `TextMatePlugin(prewarm: [...])` + `MermaidPlugin`（`:39-41`）；插件是唯一“改动演示能力”的开关 |
| `Program.Sample` | `tools/streammark/Gallery/Program.cs:46` | `GetManifestResourceStream($"Samples.{name}")` 读嵌入资源，找不到直接抛 `InvalidOperationException`（`:49`），并把 `\r\n` 归一成 `\n`（`:51`） |
| `GalleryWindow` 构造函数 | `tools/streammark/Gallery/Program.cs:77` | `MarkdownServices.SetServices(this, services)` + `Profile = services.Profile`（`:84-85`），再把 `Profile` 传给 `MarkdownEntry.FromText`（`:89`）和 `MarkdownStream(profile: Profile)`（`:118`）——窗口、静态文档、流式文档三者共用同一套方言 |
| `MarkdownServices.Profile` | `src/TKCode.Arc.StreamMark/Avalonia/Extensions/MarkdownServices.cs:90` | 演示取方言的唯一来源（`Registry.Profile`）；插件改了却忘了传给文档，插件标签就会退化成普通文本 |
| `MarkdownEntry.FromText` | `src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptEntry.cs:25` | 静态示例的挂载方式：内部 `new StreamingDocument(profile)` 后调 `Load`（`:27-28`），即后台解析、不是同步的 `LoadNow` |
| `MarkdownStream` | `src/TKCode.Arc.StreamMark/Avalonia/MarkdownStream.cs:13` | 流式回答的挂载方式：`Document`（`:29`）+ `StreamingState`（`:31`），`Push` 内部走 `Dispatcher.UIThread.Post` + `StreamPacer`（`:42-55`） |
| `SmokeTest.Check`（本地函数） | `tools/streammark/Gallery/SmokeTest.cs:24` | 打印 `PASS/FAIL <name> — <detail>` 并累加 `failures`（`:26-31`）；所有检查项都经它 |
| `SmokeTest.RenderTranscript` | `tools/streammark/Gallery/SmokeTest.cs:78` | 无头渲染段：`SetupWithoutStarting()` 后 `new GalleryWindow { Width = 1000, Height = 2400 }`、`Show()`、跑流、滚回顶部等 Mermaid、截图；`--out=<path>` 时把帧存成 PNG（`:119-125`） |
| `SmokeTest.Pump` | `tools/streammark/Gallery/SmokeTest.cs:128` | 无头时钟推进：`Dispatcher.UIThread.RunJobs()` + `AvaloniaHeadlessPlatform.ForceRenderTimerTick()` + `Thread.Sleep(16)`，所有等待循环都靠它 |
| `BenchmarkDocuments.Chat` | `tools/streammark/Benchmarks/StreamingBenchmarks.cs:66` | 输入生成：把一个含标题 / 段落 / 列表 / 代码块 / 表格 / 引用的 raw string 单元反复拼接再 `ToString(0, targetLength)` 截断（`:95-99`）；纯内存，不落盘 |
| `StreamingBenchmarks.FullReparsePerToken` | `tools/streammark/Benchmarks/StreamingBenchmarks.cs:22` | 基线：每个 token 做一次 `Remend.Complete` + `Markdown.Parse(healed, StreamMarkPipeline.Default)`（`Baseline = true`） |
| `StreamingBenchmarks.ComposerPerToken` | `tools/streammark/Benchmarks/StreamingBenchmarks.cs:34` | 对照：`new DocumentComposer()` + 每步 `composer.Update(_document[from..i], from, i)`，最后 `Finish`；`DocumentComposer` 是 `internal`，基准项目能直接用是因为 `src/TKCode.Arc.StreamMark/Core/StreamMark.Core.csproj:10` 对 `StreamMark.Benchmarks` 开了 `InternalsVisibleTo` |
| `RemendBenchmarks.Setup` | `tools/streammark/Benchmarks/StreamingBenchmarks.cs:53` | 构造约 58k 的尾部块：800 次重复后截到 58_000 再追加未闭合的 `" **unclosed"`（`:56-57`） |

## Dependencies
- Uses: `avalonia-plugin-system` via `MarkdownServices`（插件集合 + 方言）—— `tools/streammark/Gallery/Program.cs:35`、`:84`、`src/TKCode.Arc.StreamMark/Avalonia/Extensions/MarkdownServices.cs:90`。
- Uses: `avalonia-transcript` via `TranscriptView` / `TranscriptPanel` / `MarkdownEntry` / `ContentEntry` —— `tools/streammark/Gallery/Program.cs:87-89`、`tools/streammark/Gallery/SmokeTest.cs:109-110`。
- Uses: `core-streaming` via `MarkdownStream`、`StreamingDocument`、`StreamPacer`、`SnapshotDiff` —— `tools/streammark/Gallery/Program.cs:118-133`、`tools/streammark/Gallery/SmokeTest.cs:41-62`、`tools/streammark/Benchmarks/StreamingBenchmarks.cs:36-43`。
- Uses: `native-ratex` via `RaTeXEngine.Layout`（会落到原生 C ABI；冒烟必须让 `libratex_ffi.*` 躺在二进制旁边）—— `tools/streammark/Gallery/SmokeTest.cs:33`、`:36`，`src/TKCode.Arc.StreamMark.RaTeX/RaTeXEngine.cs:22`。
- Uses: `remend-and-offsetmap` via `Remend.Complete` —— `tools/streammark/Benchmarks/StreamingBenchmarks.cs:27`、`:61`。
- Uses: `build-and-packaging` via `PublishAot`、目标框架与 IL 警告即错误 —— `tools/streammark/Gallery/StreamMark.Gallery.csproj:7`、`.github/workflows/ci.yml:111`、`:112`。
- Used by: `build-and-packaging` via CI 的 `aot-smoke` 与 `bench` 任务 —— `.github/workflows/ci.yml:121`（发布后跑 `--smoke`）、`:133`（`dotnet run -c Release --project tools/streammark/Benchmarks -- --filter '*' --exporters github`）。
- Used by: `native-ratex` / `build-and-packaging`（反向）via `artifacts/native/<rid>/` 的复制步骤 —— `.github/workflows/ci.yml:118`、`Directory.Build.targets:13`（非可打包项目按 RID 把原生库平铺到输出根，所以本地 `dotnet run` 前需要先跑过 `tools/streammark/build-ratex.sh`）。
- 项目引用（`tools/streammark/Gallery/StreamMark.Gallery.csproj:17-23`）：Core、Avalonia、RaTeX、Highlighting.TextMate、Mermaid 五个；基准项目只引用 Core（`tools/streammark/Benchmarks/StreamMark.Benchmarks.csproj:11`），因此基准里出现不了任何 UI 类型。

## State it owns
- **示例文档是嵌入资源**：`Samples\*.md` 以 `LogicalName="Samples.%(Filename)%(Extension)"` 嵌入（`tools/streammark/Gallery/StreamMark.Gallery.csproj:15`），所以 `Samples/showcase.md` 的逻辑名是 `Samples.showcase.md`，`Program.Sample("showcase.md")` 拼的就是这个字符串（`tools/streammark/Gallery/Program.cs:48`）。当前 `Samples/` 下只有 `showcase.md`。
- **示例文档同时被演示和冒烟消费**：`GalleryWindow` 拿它做静态展示与流式模拟（`tools/streammark/Gallery/Program.cs:89`、`:121`），冒烟测试复用同一个 `GalleryWindow`，所以文档里少一种块类型会直接让某条 `ui.*` 检查失去意义（`tools/streammark/Gallery/SmokeTest.cs:111-115`）。
- **`--out=` 的 PNG**：仅当传入 `--out=<path>` 时才写文件（`tools/streammark/Gallery/SmokeTest.cs:119-124`）；CI 不传，所以 CI 不产出截图。
- **基准输入全部在内存里**：`BenchmarkDocuments.Chat` 现场拼字符串（`tools/streammark/Benchmarks/StreamingBenchmarks.cs:66`），`RemendBenchmarks` 现场拼 58k 段落（`:53`），没有任何临时文件。
- **基准产物目录**：`BenchmarkDotNet.Artifacts/`（`results/` 下是 `-report-github.md` / `.csv` / `.html`），被 `.gitignore:6` 忽略；CI 的 `bench` 任务把它上传为 artifact（`.github/workflows/ci.yml:137`）。
- 除了窗口与文档对象，本模块**没有任何持久化状态**：没有配置文件、缓存目录或数据库。

## Contracts it exposes
- **`--smoke` 的语义**：命令行里出现 `--smoke` 就完全不启动 UI 生命周期，改为跑检查并返回退出码（`tools/streammark/Gallery/Program.cs:22`）。`0` = 全部 PASS，`1` = 至少一项 FAIL（`tools/streammark/Gallery/SmokeTest.cs:75`）；失败明细以 `FAIL <name> — <detail>` 打在 stdout（`:26`）。除 `--smoke` 与 `--out=<path>` 外的参数无人消费。
- **它验证哪些子系统**（全部在 `SmokeTest.Run` 内）：
  - 原生 RaTeX 调用能否成功、能否报错：`ratex.layout`（`SmokeTest.cs:34`）、`ratex.error`（`:37`）—— 覆盖“原生库不在二进制旁”这类打包事故。
  - Core 的 AOT 路径：`stream.complete` / `stream.blocks` / `stream.diff`（`:60-62`），用 `StreamPacer` → `StreamingDocument` → `SnapshotDiff` 跑一遍 5 块的 markdown（`:40`），并断言 `final.Count == 5`、第 1 块的行内对象数、代码块语言、表格块类型。
  - 界面渲染：`ui.stream.complete`（`:95`）、`ui.virtualized`（`:110`，断言 `RealizedCount > 0 && RealizedCount < RowCount`）、`ui.math`（`:111`）、`ui.diagram`（`:112`，必须 `IsShowingDiagram` 且高度 > 80）、`ui.code`（`:113`）、`ui.components`（`:114`，`CollapsibleBlockView` 即 `showcase.md` 的 `<details>`）、`ui.html`（`:115`，`HtmlBoxBlockView` 至少 2 个）、`ui.frame`（`:118`，能取到渲染帧）。这段整体包在 try/catch 里，异常转成一条 `ui.render` FAIL（`:69-72`）。
  - 未知参数、多余参数一律忽略，不会报错。
- **基准的参数与 filter 用法**：入口只是 `BenchmarkSwitcher.Run(args)`（`tools/streammark/Benchmarks/Program.cs:3`），因此 `--filter '*'`（跑全部类）、`--job short`（ShortRun，少迭代）、`--exporters github`（CI 用）都是 BenchmarkDotNet 自己的开关，本模块不解析。类名可作 filter：`--filter *StreamingBenchmarks*` / `--filter *RemendBenchmarks*`。项目设了 `OutputType=Exe` + `Optimize=true`（`tools/streammark/Benchmarks/StreamMark.Benchmarks.csproj:3`、`:5`），但**必须 `-c Release`**（`docs/streammark/benchmarks.md:3`、`.github/workflows/ci.yml:133`）。
- **嵌入资源命名约定**：`LogicalName` 由 `%(Filename)%(Extension)` 生成并加 `Samples.` 前缀（`tools/streammark/Gallery/StreamMark.Gallery.csproj:15`），所以从代码读的名字永远是 `"<文件名含扩展名>"`。新增示例 markdown **不需要改 csproj**（通配 `Samples\*.md`），但**必须**在调用处用同名参数（`Program.Sample("新名字.md")`）。

## How to extend
- **新增一个示例文档**：把 `xxx.md` 丢进 `tools/streammark/Gallery/Samples/`（不需要改 csproj，`tools/streammark/Gallery/StreamMark.Gallery.csproj:15` 是通配的），然后在 `GalleryWindow` 构造函数里照 `:88-89` 追加一条 `Transcript.Entries.Add(MarkdownEntry.FromText(Program.Sample("xxx.md"), Profile))`。若还想让它进冒烟测试，得再让 `SmokeTest.RenderTranscript` 去遍历它（当前冒烟只认 `GalleryWindow` 里已经挂上的条目，`tools/streammark/Gallery/SmokeTest.cs:90`）。**漏掉第二步 → 文档嵌进去了但没人读，字面一切正常、界面上看不到。**改名时必须同步 `Program.Sample` 的参数字符串，否则 `GetManifestResourceStream` 返回 null 直接抛 `InvalidOperationException`（`tools/streammark/Gallery/Program.cs:49`）。
- **新增一项基准**：在 `tools/streammark/Benchmarks/StreamingBenchmarks.cs`（或同项目下新文件）加一个带 `[MemoryDiagnoser]` / `[Benchmark]` 的类，用 `[GlobalSetup]` 现场造输入（照 `StreamingBenchmarks.Setup`）。要对比就加 `Baseline = true`（`:21`）与 `Description`；需要 Core 的 internal 类型（如 `DocumentComposer`）已由 `src/TKCode.Arc.StreamMark/Core/StreamMark.Core.csproj:10` 放行。**漏掉 `[GlobalSetup]` 建输入 → 基准跑在空字符串上，得出“零耗时”的假结论**；**漏掉 `-c Release` → 结果是无意义的 Debug 数字**。不需要注册到任何列表，`--filter '*'` 会自动发现新类。
- **给冒烟测试增加一项检查**：在 `SmokeTest.Run` 里调 `Check("<名字>", <条件>, <detail>)`（`tools/streammark/Gallery/SmokeTest.cs:24`）；涉及界面时把检查放进 `RenderTranscript`，因为那里才有已 `Show()` 的窗口与已跑完的流（`:86-115`）。**依赖时间/渲染的断言必须放在 `RenderTranscript` 的 `Pump()` 等待循环之后**（`:100-108`、`:128-133`），否则检查会在视图树建好之前执行，表现为随机 FAIL。新增 `ui.*` 检查要注意它会拉高“冒烟通过”所要求的渲染能力，任何一项 FAIL 都会让 `--smoke` 返回 1、CI 的 `aot-smoke` 任务红（`.github/workflows/ci.yml:121`）。
- **给演示加一个插件**：只在 `Program.CreateServices` 的 `Plugins` 列表里加（`tools/streammark/Gallery/Program.cs:37-42`），不要把插件塞进 `MarkdownServices` 之外的路径——`GalleryWindow` 已经把 `services.Profile` 同时喂给静态与流式文档（`:85`、`:89`、`:118`）。
- **让冒烟覆盖新的原生 / AOT 风险点**：直接在 `SmokeTest.Run` 里加一段调用 + `Check`，不要再建一个 `--smoke-xxx` 参数——CI 只认 `--smoke` 一个开关（`.github/workflows/ci.yml:121`）。

## Invariants and traps
- **Gallery 是 NativeAOT 发布目标**：`PublishAot=true` 写在示例项目里（`tools/streammark/Gallery/StreamMark.Gallery.csproj:7`，注释 `:6` 说明它“兼作冒烟测试”），所以这个模块里的任何代码都在裁剪 / AOT 分析之下，`IL2026` 一类诊断已是错误（`Directory.Build.props:14`）。示例里能用 `new Random(7)` / 反射式的嵌入资源读取，是因为 `GetManifestResourceStream` 与其它写法已被验证干净；**在演示代码里随手引入反射或动态代码会直接打断 CI 构建** — see `tools/streammark/Gallery/StreamMark.Gallery.csproj:PublishAot`。
- **CI 要求 0 个 IL 警告，用日志 grep 卡，不靠 MSBuild 属性**：`dotnet publish tools/streammark/Gallery -c Release -r <rid> -o artifacts/aot` 的输出被 `tee` 到 `publish.log`，然后 `grep -E "warning IL[0-9]+" publish.log` 命中即 `::error::` + `exit 1`（`.github/workflows/ci.yml:111-114`）。加代码后本地应照这条命令自查 — see `.github/workflows/ci.yml:aot-smoke`。
- **`--smoke` 的退出码就是门禁**：CI 最后一步是 `./artifacts/aot/StreamMark.Gallery --smoke`，非零即任务失败（`.github/workflows/ci.yml:121`）。`SmokeTest.Run` 只有在 `failures == 0` 时返回 0（`tools/streammark/Gallery/SmokeTest.cs:75`），所以**任何一条 `Check` 变红都会挡住发布**；反过来说，往 `Check` 里塞了恒真的条件就等于没测 — see `tools/streammark/Gallery/SmokeTest.cs:Run`。
- **冒烟运行需要把原生库复制到二进制旁边**：`RaTeXEngine.Layout` 会 P/Invoke 原生库（`src/TKCode.Arc.StreamMark.RaTeX/RaTeXEngine.cs:22`），所以 publish 后有专门一步 `cp artifacts/native/<rid>/* artifacts/aot/`（`.github/workflows/ci.yml:116-118`），而不是指望 publish 自己带上。本地等价做法是 `dotnet publish` 到项目默认 publish 目录（`README.md:109`），或先跑 `tools/streammark/build-ratex.sh` 让 `Directory.Build.targets:13` 的复制项生效。**漏掉这一步 → 冒烟在 `ratex.layout` 那条就 FAIL（原生库加载失败），而不是给出有用的错误** — see `.github/workflows/ci.yml:Copy native library next to the binary`。
- **冒烟测试必须能在无头 / 无窗口环境跑完**：它不依赖 `Program.BuildAvaloniaApp`（那个用 `UsePlatformDetect()`，在 CI runner 上没有显示服务），而是在 `RenderTranscript` 里自建 `AppBuilder.Configure<GalleryApp>().UseSkia().UseHarfBuzz().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })`（`tools/streammark/Gallery/SmokeTest.cs:80-84`）。`UseHeadlessDrawing = false` 是必须的，否则 `CaptureRenderedFrame()` 拿不到帧、`ui.frame` 会 FAIL（`:117-118`）。窗口尺寸被硬调成 `1000×2400`（`:86`）就是为了让虚拟化仍然生效（`ui.virtualized` 断言 `RealizedCount < RowCount`，`:110`）——**把高度改成能装下全部内容的值会让这条断言失去意义** — see `tools/streammark/Gallery/SmokeTest.cs:RenderTranscript`。
- **等待循环有 30 秒死线**：`deadline = DateTime.UtcNow.AddSeconds(30)`（`tools/streammark/Gallery/SmokeTest.cs:89`）被流等待与图表等待共用（`:91`、`:100`）。超时不会抛异常，只会让后面的检查以 FAIL 收场；`Pump()` 里带 `Thread.Sleep(16)`（`:132`），所以整段是真实时间驱动的，慢机器上会更接近死线 — see `tools/streammark/Gallery/SmokeTest.cs:Pump`。
- **流谓词断言靠固定种子**：模拟分片用 `new Random(7)`（`tools/streammark/Gallery/Program.cs:122`），冒烟里又断言 `final.Count == 5` 与具体块类型（`tools/streammark/Gallery/SmokeTest.cs:61`）。改 `showcase.md` 的块结构（例如加一张表、加一个代码块）会同时改动静态展示与这条断言的期望值 — see `tools/streammark/Gallery/SmokeTest.cs:stream.blocks`。
- **演示窗口与冒烟窗口共用 `GalleryWindow`，构造即建服务**：`GalleryWindow` 构造函数里就 `CreateServices()` 并 `SetServices`（`tools/streammark/Gallery/Program.cs:83-84`），`TextMatePlugin` 还带 5 个语法的 `prewarm`（`:40`）。冒烟里 `SetupWithoutStarting()` 是进程级一次性操作（`tools/streammark/Gallery/SmokeTest.cs:80-84`），**重复配置 `AppBuilder` 或重复构造窗口会撞上已初始化的 Avalonia 单例** — see `tools/streammark/Gallery/SmokeTest.cs:RenderTranscript`。
- **基准只记录量级，不能当性能门禁**：项目没有阈值断言，`docs/streammark/benchmarks.md:3` 用的就是 `--job short`（ShortRun），记录里 `Full reparse per token` 的 Error 达到 1,197 ms、`Heal58kTail` 的 Error 347 µs，误差与均值同量级（`BenchmarkDotNet.Artifacts/results/StreamMark.Benchmarks.StreamingBenchmarks-report-github.md`、`...RemendBenchmarks-report-github.md`）。**别用单次结果判断回归，也别把 bench 接进 CI 的必要路径**——CI 里它只在 `workflow_dispatch && inputs.bench` 时跑（`.github/workflows/ci.yml:124`） — see `docs/streammark/benchmarks.md:ShortRun`。
- **`docs/streammark/benchmarks.md` 里的视图层与扩展数字不是这个基准项目产出的**：视图层数字来自无头测试 `VirtualizationScaleTests`（`docs/streammark/benchmarks.md:26`、`:43`），扩展数字标着“NativeAOT，子代理实测”（`:34`），而 `tools/streammark/Benchmarks` 只引用 Core、根本看不到 UI 类型（`tools/streammark/Benchmarks/StreamMark.Benchmarks.csproj:11`）。想让基准覆盖渲染层，得先解决这个引用缺口 — see `tools/streammark/Benchmarks/StreamMark.Benchmarks.csproj:ProjectReference`。

## Tests
- 本模块**没有单元测试**：`samples/` 与 `bench/` 下没有测试工程，`dotnet test TKCode.Arc.slnx` 不会触及它们（`TKCode.Arc.slnx` 里 `samples/` 与 `bench/` 只作为项目列出）。
- `--smoke` 就是本模块的测试：CI 的 `aot-smoke` 任务在 `linux-x64` / `osx-arm64` / `win-x64` 三个 RID 上 `dotnet publish` → grep IL 警告 → 复制原生库 → `./artifacts/aot/StreamMark.Gallery --smoke`（`.github/workflows/ci.yml:87-121`）。本地等价：`dotnet publish tools/streammark/Gallery -c Release -r <rid>` 后运行 publish 目录里的二进制并加 `--smoke`（`README.md:109`）；想留一张渲染图就再加 `--out=<path.png>`（`tools/streammark/Gallery/SmokeTest.cs:119`）。
- 基准：`dotnet run -c Release --project tools/streammark/Benchmarks -- --filter '*' --job short`（`docs/streammark/benchmarks.md:3`）；CI 版本多了 `--exporters github` 并上传 `BenchmarkDotNet.Artifacts/results/`（`.github/workflows/ci.yml:133-137`），且只在手动触发且勾选 `bench` 时运行。
- 本次快照未运行任何构建、测试或基准命令；上表中的数值来自仓库里已有的记录文件与 `docs/streammark/benchmarks.md` (unverified)。

## Related flows
None —— `docs/streammark/codemap/flows/` 目录在本次快照中不存在（`docs/streammark/codemap/` 下只有 `modules/`），因此没有可打开的流程页；本模块参与的实际流程（token → 视图、流结束整篇重解析）由 `avalonia-transcript` / `core-streaming` 描述。
