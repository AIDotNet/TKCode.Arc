# StreamMark 代理指南

## 这份文件管什么

StreamMark：面向 AI 对话场景的流式 Markdown 渲染库，Avalonia 12，NativeAOT 安全、不使用 WebView，原生库覆盖六个桌面 RID（win/linux/osx × x64/arm64）。2026-11-19 从独立仓库（AtomUICommunity/StreamMark，最后一个版本 0.1.2，提交 `f911c50`）并入 TKCode.Arc，作为随 Arc 发布的独立库（与 `TKCode.Arc.Terminal` 同样处理：保留 `StreamMark.*` 命名空间，不依赖 Foundation / Core，没有 `UseArc` 入口，Arc 的 Token、主题与注册规则不适用）。

| 包 | 源码 | 命名空间 |
|---|---|---|
| `TKCode.Arc.StreamMark` | `src/TKCode.Arc.StreamMark/`（`Core/` + `Avalonia/`） | `StreamMark.Core.*`、`StreamMark.Avalonia.*` |
| `TKCode.Arc.StreamMark.TextMate` | `src/TKCode.Arc.StreamMark.TextMate/` | `StreamMark.Highlighting.TextMate` |
| `TKCode.Arc.StreamMark.RaTeX` | `src/TKCode.Arc.StreamMark.RaTeX/` | `StreamMark.RaTeX` |
| `TKCode.Arc.StreamMark.Mermaid` | `src/TKCode.Arc.StreamMark.Mermaid/` | `StreamMark.Mermaid` |

测试在 `tests/TKCode.Arc.StreamMark.Tests/`（`Core/`、`Avalonia/`、共用的 `Fixtures/`），演示 / 冒烟、基准与 CPU 测量在 `tools/streammark/`，RaTeX 的 pin 与补丁在 `native/ratex/`。

本文件是 StreamMark 专属的硬约束与验证方式，仓库根目录的 `AGENTS.md` 的通用规则同样适用（大括号、无反射、PublicAPI 文件、CHANGELOG、不擅自提交）。代码「是什么」在 `docs/streammark/codemap/`，理由与历史在 `docs/streammark/memory/`，叙述性设计在 `docs/streammark/`（数据流水线见 [architecture.md](architecture.md)，冻结与 remend 规则见 [streaming-rules.md](streaming-rules.md)，决策与被否决的方案见 [decisions.md](decisions.md)）。codemap 与 memory 写于并入之前，里面的行号和部分构建描述（独立仓库的 `Directory.Build.*`、`pack-local.sh`、子模块）已过时；与代码不一致时以代码为准。

关键入口：`StreamingDocument`（`src/TKCode.Arc.StreamMark/Core/Streaming/StreamingDocument.cs`）、`MarkdownView` / `MarkdownStream`（`src/TKCode.Arc.StreamMark/Avalonia/MarkdownView.cs`、`MarkdownStream.cs`）、`TranscriptView`（`src/TKCode.Arc.StreamMark/Avalonia/Transcript/TranscriptView.cs`）、`MarkdownServices`（`src/TKCode.Arc.StreamMark/Avalonia/Extensions/MarkdownServices.cs`）。

## 命令

| 目的 | 命令 | 说明 |
|---|---|---|
| 构建 | `dotnet build src/TKCode.Arc.StreamMark`（或某个插件包） | 基础包与 TextMate 目标 `net8.0;net10.0;net11.0`，RaTeX 与 Mermaid 目标 `net10.0;net11.0` |
| 原生库（公式必需） | `tools/streammark/build-ratex.sh` | 按 `native/ratex/ratex-upstream.json` 的提交检出到 `artifacts/src/ratex-*`、打补丁、`cargo build`，输出 `artifacts/native/<本机 RID>/`。需要 Rust。Windows：`tools/streammark/build-ratex.ps1` |
| 发布用原生库 | `tools/streammark/build-ratex.sh <rid>` → `tools/streammark/sync-native.sh` | osx-* 用 `cargo build --target`，linux-* / win-* 用 cargo-zigbuild（Linux 的 glibc 下限 2.28）；需要对应的 rustup 目标。sync 把库复制到 `src/TKCode.Arc.StreamMark.RaTeX/runtimes/<rid>/native/`（不提交） |
| 测试 | `dotnet test tests/TKCode.Arc.StreamMark.Tests` | 公式测试需要先跑 `build-ratex.sh`（`ArcStreamMarkNative=true` 让项目从 `artifacts/native/<rid>/` 复制库）。单用例 `--filter "FullyQualifiedName~<名字>"` |
| AOT 冒烟（CI 门禁） | `dotnet publish tools/streammark/Gallery -c Release -r <rid> -o artifacts/streammark-aot`，publish 日志里 0 个 `warning IL####`；再运行 `artifacts/streammark-aot/TKCode.Arc.StreamMark.Gallery --smoke` | 退出码即结论（0 = 全部通过）；缺原生库时在 `ratex.*` 检查失败 |
| 基准 | `dotnet run -c Release --project tools/streammark/Benchmarks -- --filter '*'` | ShortRun，只看量级；结果记录在 [benchmarks.md](benchmarks.md) |
| 流式 CPU 基准（真实窗口） | `[ROUNDS=n] tools/streammark/cpu-bench.sh [rid]` | NativeAOT 发布 `tools/streammark/CpuHarness`，把场景矩阵交错跑 n 轮（默认 3），每次运行与各场景有效轮次的中位数追加到 `artifacts/cpu-bench.txt`；会打开窗口，运行期间保持可见。窗口被遮挡时系统停止渲染、CPU 骤降，这样的运行标为 `INVALID` 且不计入中位数；跨轮次比较看 `cpu/frame`（每渲染一帧的进程 CPU）。改动渲染、排版、节奏或会话面板后跑一次并与 [benchmarks.md](benchmarks.md) 对比 |
| 测试截图 | 图片写到 `STREAMMARK_SNAPSHOT_DIR`，未设置时用测试输出目录下的 `snapshots/` | 只用于人工查看，没有基线比对 |
| 升级 RaTeX | 改 `native/ratex/ratex-upstream.json` 的 `commit`，重新复制该提交的 `fonts/*.ttf` 与 `licenses/*` 到 `src/TKCode.Arc.StreamMark.RaTeX/Fonts/`，确认 `native/ratex/patches/` 仍能打上（上游已包含则删除） | |

打包随 Arc 一起：版本是 `build/Versions.props` 的 `ArcVersion`，CI 的 Pack 作业打全部包。

## 架构与边界

- 依赖方向单向：`Core/` → Markdig；`Avalonia/` → `Core/`；插件包 → 基础包。**`Core/` 不得引用 Avalonia（两层在同一程序集里，编译器不再拦，`CoreLayerTests` 检查源码）；基础包不得引用任何插件包，也不得引用 Arc 的 Foundation / Core / 控件包**——扩展一律经 `Plugins/`、`Extensions/` 的接口注册。完整图与归属表见 [docs/streammark/codemap/architecture.md](docs/streammark/codemap/architecture.md)。
- 解析只在工作线程：`Append` / `Complete` / `Reset` 可在任意线程调用，界面线程每帧最多取一次 `Latest` 并应用一次差异（`SnapshotReady` 在后台线程触发，必须回投界面线程）。
- 唯一真相是只追加的源码缓冲；块与选区的身份都是「原始源码偏移」。块模型、快照、会话行模型都是派生数据，不得当作真相写回。
- 视觉样式的真相是 Avalonia 资源里的 `Md.*` 键；`MarkdownStyle` 是解析后的不可变快照，只有 `MarkdownView` 与 `TranscriptPanel` 持有并下发。
- 失败被吞掉是既有行为：解析异常保留旧快照（表现为界面停止更新），高亮异常被丢弃。排查「界面不动」时先看这两处，不要先怀疑视图层。

## 改动规则

- 块的身份是 `MarkdownBlock.Key`（源码起始偏移）。给块模型加字段时必须同时进 `ComputeFingerprint`（`src/TKCode.Arc.StreamMark/Core/Model/Blocks.cs`），否则内容变化在完成后不会产生 Update。
- `DiffKind` 新增成员要同步三处 `switch`：`SnapshotDiff`、`DocumentPresenter.Apply`、`TranscriptPanel`（`ApplyAll`）。漏改会让新操作被静默忽略。
- 冻结片段与 `Complete()` 的整篇解析只用位置无关的转义修正（remend 的 singleTilde / comparisonOperators）；末尾补全只作用于尾部。在冻结片段上启用补全会让流式结果与最终结果不一致。
- 新增 remend handler：同时加进 `Remend` 的启用列表与（插件场景）`MarkdownProfileBuilder.AddRemendHandler`，否则开关存在但永不执行。
- 新增白名单 HTML 组件要动四处：`ComponentSpec` 注册、解析器插入顺序（`HtmlComponentBlockParser` 必须在 `HtmlBlockParser` 之前，行内组件解析器在 `AutolinkInlineParser` 之前）、映射器、块或行内视图。未知元素整组按源码显示且绝不执行（含 `script` / `style` / `iframe`）。
- 行内 HTML 的文字样式有两条独立实现（`MarkdigMapper` 里的标签栈与 `HtmlMapper.HtmlInlineBuilder`）；加样式必须两处都改。
- 新增块视图：`BlockViewFactory` 的 `ViewTypeFor` / `CreateBuiltIn` / `CanReuseBuiltIn` 三个分支都要加，漏第三个会让视图在每次快照更新时被重建。片段编号必须与 `BlockText.Segments` 的收集顺序一致，否则选区与复制错位。
- 插件的块组件视图除子块外还显示自己的文本（如属性里的标题）时，用 `AddBlockComponent(name, factory, text)` 登记复制文本（`ComponentTextCollector`），并保证片段个数与视图消耗的片段号一致。`BlockText.Segments` / `DocumentNavigation` / `SelectionRemap` 在视图侧调用时必须传当前注册表（`MarkdownServices.GetServices(this).Registry` 或 `ISelectionHost.Plugins`），漏传会让这类组件的选区、复制、搜索与跳转错位。
- 原样组件（`ComponentSpec.Content = ComponentContent.Raw`，插件经 `BlockComponentOptions.Content` 登记）的结束规则有三处实现，必须一致：`HtmlComponentBlockParser` 的原样分支（结束标签在行尾）、`StreamSegmenter.EndsWithClosingTag`（切分器结束得不能比解析器早）、`DocumentComposer.MaskRawComponents`（解析前把内容换成等长占位，remend 才不会在里面补全或转义）。`ComponentBlock.Raw` 经 `OffsetMap.SourceText` 从原文逐行取，不要改成取修补后的文本。
- 组件视图按 `PluginRegistry.FindBlockComponent(component)` 选择（`WhileStreaming = false` 的组件在 `IsOpen` 时是默认容器），复制文本也按它选；改其中一边必须同时改 `BlockViewFactory.Create` / `CanReuse` 与 `BlockText`。插件视图重写 `OnAttachedToVisualTree` / `OnDetachedFromVisualTree` 时必须调 base，否则 `BlockView.RunInBackground` 离开可视树时不取消、重新挂上时不重跑。
- 新增 `Md.*` 键：`MarkdownResourceKeys` 常量 + `MarkdownStyle` 的 required 属性与解析 + `Themes/StreamMarkTheme.axaml` 的 Light/Dark 字典 + 消费点（+ 宿主的映射，例如 `TKCode.Arc.Markdown` 的 `Markdown.s_aliases`）。键名写错或别名解析失败不会报错，只会静默回落。
- 主题里写 `MarkdownResourceAlias`，不要写画刷或 `DynamicResource`：宿主 token 在窗口 / 主题作用域，从字典位置向上查找找不到会让文字变透明。宿主主题要注册在 `StreamMarkTheme` 之后。
- 文档必须用服务的方言创建：`services.CreateStream()` / `CreateEntry(text)` / `CreateDocument()`，或手动传 `services.Profile`；否则插件注册的标签不会被解析（视图拿到新上下文时经 `MarkdownServices.CheckProfile` 报告一次 `ProfileMismatch`）。`MarkdownView` 的自建文档在服务的方言变化时按新方言重建，设置顺序无关。插件都不改语法时 `Profile` 就是 `MarkdownProfile.Default`（`MarkdownProfileBuilder.Build` 对未改动的构建返回共享实例），方言按引用比较。
- 插件 `Configure` 只登记委托与工厂（NativeAOT，禁反射）；`PluginRegistry` 首次访问即固化，之后修改 `Plugins` 无效。同名组件后登记者覆盖先登记者是设计内行为。
- 选区坐标只存值（`Entry` / `BlockKey` / `Segment` / `Offset`），不得持有视图引用；复制必须经 `ISelectionHost.GetSnapshot(entry)` 取模型，否则被虚拟化回收的块复制不到。
- `TextRunCache` 是 Avalonia 不稳定 API，只在 `TextSegmentView` / `SegmentLayout` 内用 `#pragma warning disable CS0618` 限定；`InvalidateFrom` 的失效位置必须是差异点所在段落的起点，否则每次追加都会变成一次硬换行。
- 文本片段按行排版（`SegmentLayout`）、按行录制（每行一个 `SegmentLineVisual`）：内容变化必须经 `TextSegmentView.Set` 算出差异点再 `InvalidateLayout(from)`，只从差异点所在行的前一行重排；直接整段失效会让流式长段落退回 O(段落长度) 每帧。增量结果必须与整段排版逐行一致（`IncrementalLayoutTests`）。
- 末尾渐显在渲染线程（`TailFade` 的 `CompositionCustomVisualHandler`）：渐显层只能画字形与矩形，含装饰线或内嵌对象的文字不参与渐显；不要再加每帧 `RequestAnimationFrame` → `InvalidateVisual` 的循环。
- NativeAOT：正则一律 `[GeneratedRegex]`、JSON 一律源生成、主题用 `x:Class` 编译加载、不用反射绑定。裁剪 / AOT 警告按 `build/ProjectDefaults.props` 里 StreamMark 包的列表视为错误，CI 另外要求发布日志里 0 个 IL 警告。
- 包版本只写在根目录的 `Directory.Packages.props`（开启了传递依赖固定）。Svg.Skia / Svg.Animation 必须保持 5.1.1、其托管依赖 5.2.3（见那里的注释与 [[svgia-version-pin]]）。新包的引用要同步 `tests/TKCode.Arc.Tests/Architecture/PackageReferenceTests.cs`；公开 API 进 `PublicAPI.Unshipped.txt`。
- 原生库有两条分发路径：NuGet 包的 `runtimes/<rid>/native/`（`tools/streammark/sync-native.sh`）与仓库内的 `artifacts/native/<rid>/`（`build/Arc.Repository.targets` 按目标 RID 复制给设了 `ArcStreamMarkNative=true` 的项目）。改任一条都要顾及另一条。
- 改动本文档提到的命令、边界或联动步骤时，在同一次改动里更新它；改动模块的入口、契约或扩展步骤时，同一次改动里更新对应的 `docs/streammark/codemap/` 页面。

## 冲突处理

先服从更高优先级的指令与任务本身的范围；在范围内保持既有不变量、使用最小 diff。两者无法同时满足时先说明冲突再动手。任何规则都不构成顺手重构无关代码、整文件重排或未经要求提交 / 推送的许可。

## 代码地图

详细地图在 `docs/streammark/codemap/`（生成时 StreamMark 仓库 HEAD 是 `9b0e08b`；地图写于并入 Arc 之前，路径已改成新位置，行号与构建细节可能落后——不一致时以代码为准并顺手修页面）。动手前先读对应页面：

- 改动解析、切分、快照、节奏 → `docs/streammark/codemap/modules/core-streaming.md`、`modules/remend-and-offsetmap.md`
- 改动 Markdig 方言、白名单组件、HTML/CSS 映射 → `docs/streammark/codemap/modules/core-parsing-and-model.md`
- 改动块视图、样式解析、流式渐显 → `docs/streammark/codemap/modules/avalonia-text-rendering.md`
- 改动会话行模型、虚拟化、滚动 → `docs/streammark/codemap/modules/avalonia-transcript.md`
- 改动选区或复制格式 → `docs/streammark/codemap/modules/avalonia-selection.md`
- 改动图片加载、缓存、策略 → `docs/streammark/codemap/modules/avalonia-images.md`
- 加插件、组件视图、围栏视图 → `docs/streammark/codemap/flows/plugin-registration.md`
- 改动主题、`Md.*` 键 → `docs/streammark/codemap/flows/theme-resolution.md`
- 改动构建、包版本、CI、原生库 → `docs/streammark/codemap/modules/build-and-packaging.md`、`modules/native-ratex.md`
- 不确定新代码放哪里 → `docs/streammark/codemap/architecture.md` 的归属表
- 遇到不认识的业务名词 → `docs/streammark/codemap/domain.md`

## 记忆

`docs/streammark/memory/` 记录代码里看不到的上下文：被纠正的做法、决定及其理由、陷阱、外部资源。它是背景资料，规则在本文件。

**开始任务前：** 读 `docs/streammark/memory/MEMORY.md`，打开与任务相关的条目。条目描述的是它写下的那个时点：依赖它引用的路径、符号或命令之前先核实。与代码或本文件冲突时按代码与本文件执行，并修正或删除该条目。

**事情发生时写，不要留到任务结束：**
- 用户纠正了你的做法，或确认了一个非显然的做法 → `feedback`
- 做出了影响后续改动的决定、说明了理由，或否决了某个方案 → `project`
- 发现了代码里难以看出、本文件又没写的陷阱 → `project`
- 用户提到外部资源（看板、工单、文档、监控） → `reference`

**不要记录：** 代码、git 历史、本文件或 `docs/streammark/codemap/` 已经写明的东西；一次性的任务状态；密钥或个人数据；未经验证的猜测。

**怎么写：** 先更新已有条目再新建；一个事实一个文件，格式见 `docs/streammark/memory/README.md`；用绝对日期；给出证据（`path:Symbol`、提交或链接）；同步 `MEMORY.md` 的那一行；发现条目是错的就删掉。个人习惯不写在这里。

**升格：** 某个 `feedback` 条目被再次确认且对所有人成立时，建议把它写进本文件——不要自己改这里的规则。记忆的改动会出现在 diff 里，除非被要求不要单独提交它们。
