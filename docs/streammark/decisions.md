# 设计决策记录

2026-10-16，基于 [research/](research/) 中的五路调研确定。

| 主题 | 决定 | 被否决的方案及理由 |
|---|---|---|
| 仓库与命名 | 独立仓库 StreamMark，中立命名；TCode 通过 NuGet 引用。2026-11-19 并入 TKCode.Arc 仓库：包名 `TKCode.Arc.StreamMark`（Core + Avalonia）与 `.TextMate` / `.RaTeX` / `.Mermaid` 插件包，命名空间保持 `StreamMark.*` | 以 AtomUI 命名（会限制通用性）；Streamdown.Avalonia（易被误认为 Vercel 官方移植） |
| 平台 | 只做桌面：win / linux / osx × x64 / arm64 | 移动端：TextMateSharp 的 Oniguruma 没有 iOS / Android 构建，暂不投入 |
| 解析器 | Markdig 1.4（AOT 0 警告实测） | 自研完整增量解析器（CommonMark 兼容成本数周）；CommonMark.NET（2017 年后停更） |
| 流式策略 | 已稳定前缀冻结 + 只重解析尾部；流结束整篇重解析对齐 | 每个 token 整篇重解析（20KB 回答流完 1.17s / 2.3GB 分配，且无法复用控件） |
| 未闭合语法 | 全部自动补全：移植 vercel remend，只作用于尾部 | 暂缓显示（Codex 式，最稳但样式出现晚）；混合策略 |
| 渲染 | 每块一个 `TextLayout` 自绘 + 自研 `ILogicalScrollable` 虚拟化面板 + 源码偏移选区 | `SelectableTextBlock` 控件树（每次文本或选区变化整段重排，跨块选区代价高）；`VirtualizingStackPanel`（变高项滚动条跳动） |
| 数学 | RaTeX（Rust）经 C ABI 调用，KaTeX 级覆盖 + mhchem | CSharpMath（实测仅约 60–65% 常见命令）；XAML-Math（不支持 Avalonia 12，不 AOT 安全）；Jint + KaTeX（AOT 不可行） |
| Mermaid | Mermaider → SVG（替换 `color-mix`）→ Svg.Skia | merman（需自建 C SDK）；浏览器方案 |
| 代码高亮 | TextMateSharp，按行增量 | AvaloniaEdit 整个编辑器（每个代码块太重）；ColorCode（只能整块着色） |
| 与宿主主题的关系 | 核心只依赖 Avalonia，`Md.*` 资源键；宿主自己映射（Arc 的 `Markdown` 控件把 `Md.*` 指向 Arc Token）。原来的 `StreamMark.Theme.AtomUI` 在并入 Arc 时删除（Arc 不引用 AtomUI） | 直接依赖某个 UI 库（绑定版本，其他宿主无法使用） |
| v1 范围 | 完整 Markdown、数学、Mermaid、逐字渐显 + 输出平滑、跨消息选区 | — |

## 实施中追加的决定

- **强调扩展只启用删除线**。上标 `^`、下标 `~`、插入 `++`、高亮 `==` 在大模型输出的普通文本里误触发太多（"C++ 和 C++"、"a == b 且 c == d"、"约 ~5 到 ~10"），不启用。
- **冻结片段与最终整篇解析只使用 remend 的转义修正**（单波浪号、比较运算符），不做末尾补全：已完整的文本不应被"补全"；只有尾部使用完整补全。
- **偏移映射**：remend 插入的转义字符会让解析偏移漂移，`OffsetMap` 把所有区间映射回原始源码，保证块 key 在流式与最终解析之间一致。
- **remend 的占位 URL** 使用 `streammark:incomplete-link` / `streammark:incomplete-image`。
- **块视图统一以 `*BlockView` 命名**（`TableBlockView`、`ListBlockView`…），避免与 Avalonia 12 自带的 `TableView` / `ListView` 冲突。
- **数学包命名为 `StreamMark.RaTeX`**：`StreamMark.Math` 命名空间会遮蔽 `System.Math`。
- **宿主主题用资源别名而不是画刷**：资源字典中画刷上的动态资源从字典所在位置向上查找，找不到宿主注册在窗口 / 主题作用域内的 token（文字变成透明；最早在 AtomUI 宿主下发现）。别名由 StreamMark 从 Markdown 控件本身解析。
- **字体默认继承宿主**：不设置 `Md.FontFamily` / `Md.FontSize` 时沿用控件继承的字体，宿主（如设在窗口上的字体）无需额外配置。
- **删除线自绘**：部分字体（含无头环境的回退字体）缺少删除线度量，`TextDecorations.Strikethrough` 不显示。
- **Mermaid 配色默认复刻 mermaid.js 官方主题（2026-10-18）**：暗色 = dark、亮色 = default（可选 neutral），`MermaidTheme.Native` 保留 Mermaider 原生风格。起因是宿主（当时的 AtomUI）的半透明 token 被丢掉 alpha 后图表成了白底白字；修复后与浏览器里的 mermaid.js 对照，原生风格（子图按色板着色、描边偏淡）和 Web 观感差距明显，所以改为默认复刻。颜色取自 mermaid 12.2 在流程图上实际输出的样式表（`MermaidPalette`）；只用 Mermaider 自带的 `Surface` / `Border` / `Line` 等选项，加上对子图与连线标签的 SVG 重涂，不换渲染引擎（merman 能做到像素级一致，但需要自建 Rust 原生库，已评估过、不采用）。图表高度不再封顶 900px（高流程图文字只有约 3px，浏览器里是约 12px），改为只按宽度适配，6000px 为安全上限。
- **Svg.Skia 固定 5.1.1，托管依赖（Svg.Model / Svg.Custom / ShimSkiaSharp / Svg.SceneGraph）覆盖到 5.2.3（Svg.Animation 例外：5.2.x 依赖 SkiaSharp 4，保持 5.1.1；CI 的 Linux 任务发现了这一点）**：5.1.1 与 Avalonia 12.1 使用相同的 SkiaSharp 3 原生库；5.2.x 的 Svg.Skia 需要 SkiaSharp 4。Svg.Model 5.1.1 内的 `TypeDescriptor.GetConverter` 会产生 2 个 IL2026，5.2.3 已修复。Mermaid 测试（真实渲染）与 Gallery AOT 冒烟均通过。Avalonia 升级到 SkiaSharp 4 后统一升到 5.2+。
- **RaTeX 本地补丁（已上游合入，2026-11-23 移除）**：`\fbox` / `\boxed` 边框原本固定黑色，曾通过 `native/ratex/patches/0001-enclose-border-uses-current-color.patch` 改为当前颜色；升级到上游 `d49c6fad` 后上游已自带该修复，补丁及托管侧回退判断已一并清理。
- **TextMateSharp 不支持 Kotlin**（Grammars 包无该语法）；TOML 映射到 INI 语法。
- **原生库的两条分发路径**：NuGet 包把各 RID 的 RaTeX 库放在 `runtimes/<rid>/native/`，使用方按自己的 RID 自动选用；仓库内通过项目引用使用的可执行项目（示例、测试、基准），由根目录 `Directory.Build.targets` 按其目标 RID 从 `artifacts/native/<rid>/` 复制（SDK 不会把 `RuntimeIdentifier` 传给被引用项目，原先的做法在交叉发布 osx-x64 时复制了 arm64 的库）。
- **本机交叉编译** `tools/streammark/build-ratex.sh <rid>`：在 macOS 上用 cargo-zigbuild 生成全部 6 个桌面 RID。已在本机实测 osx-arm64 与 osx-x64（Rosetta）的 AOT Gallery 冒烟通过；Linux / Windows 产物已验证格式与导出符号，运行验证依赖 CI。
- **第二阶段（2026-10-16）**：插件体系、技术债、图片子系统、AI 对话功能、交互与无障碍；自定义组件只用白名单 HTML 标签。
- **不需要组件补全处理器**：计划中的 `ComponentCloser` 被取消。未闭合的块级容器由 Markdig 在文末关闭，未闭合的行内成对组件在映射时视为包住其后的文字，写了一半的标签由现有 HTML 处理器去掉；这样流式偏移也无需额外映射。
- **`StreamMark.Highlighting.TextMate` 依赖 `StreamMark.Avalonia`**：为了提供 `TextMatePlugin`。高亮器本身仍只依赖 Core 接口。
- **Markdig 的 `CanInterrupt` 对每个候选解析器都会调用（包括没有段落时）**，自定义块解析器只应在当前块是段落时限制打断；`Block.IsOpen` 在续行判断时不可靠，块级组件自行记录关闭状态（`BlockParser.Close`）。
- **`TextRunCache`（Avalonia 不稳定 API）只在 `TextSegmentView` 中使用**：
  - 缓存以换行符分隔的段落为单位，按段落起点保存整段的整形结果；`InvalidateFrom(i)` 只丢弃起点不早于 `i` 的条目。
  - 所以失效位置必须是差异点所在段落的起点。alpha.8 和 alpha.9 直接用差异点失效，结果旧段落（连同它的段落结束）被复用，每次追加都变成一次硬换行；alpha.10 已修复，并加了回归测试 `SmoothStreamWrapTests`。
  - 修正后，流式段落的排版大约快 2 倍；用 `#pragma warning disable CS0618` 限定范围。
- **长代码分段**：超过 200 行的代码块在会话中按 128 行一段拆成多行；切行与高亮放在块共享状态 `CodeBlockState`（块状态存储）中，分段与回收后重建的视图共用，偏移全局化以保证选区跨段连续。各段的横向滚动彼此独立（已知限制）。
- **中文粗体的两个 Avalonia 问题及规避**：
  1. 为缺字文字找回退字体时，Avalonia 优先复用"上一段"的回退字体，不检查字重 / 字形，导致"中文 **粗体**"中的粗体沿用常规字重。`TextSegmentView` 自行按本段字重解析回退字体，让每段的主字体就能覆盖其字符，不进入那条复用路径。
  2. 苹方没有 700 字重，`Typeface(PingFang SC, Bold)` 会匹配到常规字重；回退时依次尝试 700 / 600 / 800 / 900，取第一个真正够粗的字形。
  有回归测试检查整形后字形的字重。
- **流式渲染的 CPU（2026-10-16，实测驱动）**：长段落流式时每帧整段重建 `TextLayout`（O(段落长度) 的整形与断行、每帧 36MB/s 分配）、渐显每帧把整段画两遍（`CombinedGeometry` 裁剪 + `PushOpacityMask` 两个离屏图层）并持续请求帧，是主要开销。改为：逐行排版（`SegmentLayout`，从差异点所在行的前一行重排，与整段排版逐行一致）+ 每行一个视觉单独录制；渐显移到渲染线程的自定义视觉（矩形裁剪 + 逐批不透明度，只画字形）；节奏控制器随显示帧推进。否决：给尾部文字用渐变画刷或逐批透明画刷（仍需界面线程每帧重新录制）；继续依赖 `TextRunCache`（它按段落起点缓存，只对宽度变化有用，对追加无效）。两端对齐仍整段交给 `TextLayout`（词间对齐实现是 Avalonia 内部类型）。区域脏矩形（`UseRegionDirtyRectClipping`）实测无明显收益，未启用。
- **节奏控制器 `SkipCompletedBlocks`**（默认关闭）：积压中已有完整块（空行）时直接放出到最后一个空行，只对未完成的块做逐字平滑。
- **原始 HTML（阶段 3.5）**：
  - 使用自研的容错解析器，代码在 `Core/Html/`，不依赖 AngleSharp：体积小，NativeAOT 安全，并且能控制流式输出中写了一半的 HTML 怎么处理。
  - 解析出的 HTML 树映射到已有的块模型；div、p、center 等映射为 `HtmlBoxBlock`，携带盒子样式和可继承的文字样式。
  - 文字样式通过 `RenderContext.Inherited` 向下传递；字号按与正文字号的比例缩放，标题保持相对大小。
  - **拼接**：`MapSiblings` 用一个开放元素栈跨兄弟块追踪 HTML，处理 GFM 中 HTML 与 Markdown 交错的写法；合并后的块以第一个 HTML 块的起点作为 key。HTML 容器标签同时加入切分器的容器表，未闭合时不冻结。
  - **未知元素**：遵从用户的选择，整段按源码显示，`script`、`style` 同样处理，但绝不执行；单行的自定义元素按 CommonMark 是段落中的行内 HTML，标签按原文显示。
  - **暂不支持**：`colspan`、`rowspan` 和单元格背景色；表格单元格只能放行内内容，单元格中的 `<p>` 和 `<div>` 压平为换行。
  - **作者颜色**：默认模式 `Readable` 用 WCAG 对比度（阈值 2.2）判断是否可读，比较对象是所在的 HTML 背景；没有背景时，按主题文字色推断主题背景。
  - **链接中的图片**：`[![徽章](图片)](链接)` 和 `<a><img></a>` 中，图片的占位字符也带链接区间，点击时打开链接，而不是打开图片查看器。
- **自定义块的载体是 `ComponentBlock`（2026-11-07）**：不开放 `MarkdownBlock` 继承（`AddContent` 是 internal 指纹钩子，开放后插件漏写指纹就会让完成后的内容变化不产生 Update）。插件要新块类型时用 Markdig 扩展 + `MapBlock` 产出 `ComponentBlock`，或直接登记白名单标签。
- **原样内容组件（2026-11-07）**：AI 对话里很多组件的内容是数据（`<tool-call>{json}</tool-call>`），按 Markdown 解析会坏。`ComponentContent.Raw` 让组件内部成为叶子块，`ComponentBlock.Raw` 保留原文，`IsOpen` 表示还没见到结束标签。两个取舍：
  - 原样内容在 remend 之前先被等长遮盖（`DocumentComposer.MaskRawComponents`），而不是让 remend 的扫描器认识组件：remend 是移植代码（见 `Remend/DEVIATIONS.md`），遮盖只在 StreamMark 一侧、对 remend 透明。区间由切分器在一次独立扫描里给出，与冻结判断同一套规则。
  - 不支持同名嵌套，第一个行尾结束标签即结束：数据里出现同名开始标签不该改变结构。
- **插件视图的后台工作（2026-11-07）**：`BlockView.RunInBackground(key, work, apply)` 收拢「同 key 不重复、新 key 取消旧任务、过期结果丢弃、离开可视树取消并在挂回时重跑」；结果缓存用公开的 `BlockResultCache<TKey, TValue>`（LRU，线程安全），图表视图的缓存也换成它。图表与高亮自己的后台流程没有改写成这个辅助方法：它们各有状态显示与会话校验，改写没有收益。
- **阶段 4（对话功能）**：
  - **链接策略**：判断在渲染层的 `TextSegmentView.LinkDecisionFor` 中进行，结果按内容缓存。被禁止的链接按 `IncompleteLink` 样式显示，也不响应点击；需要确认的链接只设置 `RequiresConfirmation`，由宿主决定如何确认。协议中夹带空白或控制字符（`java\tscript:`）的链接一律拒绝。
  - **引用卡片**：显示在 `OverlayLayer` 上，与图片查看器相同，都是自绘控件，不用 `Popup` 和主题模板；悬停 350ms 后显示，离开 200ms 内移进卡片可以保持显示。覆盖层第一次加入子控件前没有尺寸，定位时改用窗口客户区。
  - **代码块折叠**：超过 40 行的已完成代码块默认折叠。流式输出中出现过的代码块（`IsOpen`）完成后不折叠，避免用户正看着的内容突然收起。展开状态保存在块状态存储中。会话视图里，折叠的长代码只占一行；展开或收起时，`ExpandedChangedEvent` 通知面板重新分段。
  - **行号**：画在 `CodeBlockView` 的左侧栏，`CodeTextView` 本身不变，命中测试也不受影响；行号的行高与代码行相同，保证基线对齐。
- **diff 视图**：
  - **解析**：`Core/Diff/UnifiedDiff` 容错解析，能处理流式输出中写了一半的 diff。区段头声明的行数只用来判断"区段已满"；区段未满时，`--- ` / `+++ ` 开头的行是删除或新增的内容，不当作文件头。
  - **并排配对**：连续的删除行与随后的新增行按顺序一一配对。行内改动只取公共前缀和后缀之间的部分；公共部分不足 30% 时视为整行改写，不做行内高亮。
  - **选区**：整块是一个选区片段，文本为原始 diff；每个单元格记录它在原始行中的起点，选区与复制都按原始偏移计算。
  - **语法着色**：旧侧（上下文和删除行）与新侧（上下文和新增行）分别送进高亮会话，在后台计算，结果按原始行号保存在块状态中。区段之间不连续，跨区段的多行结构可能着色不准，这是已知限制。
  - **暂不虚拟化**：块内的行不做虚拟化（整个 diff 是会话中的一行），超大的 diff 暂不分段。
- **diff 默认样式**：按用户要求，默认采用 GitHub Markdown 的样式（`DiffViewMode.Markdown`），不显示行号、文件标题栏和统计；头部按钮在默认样式与并排视图之间切换。带行号的统一视图由宿主通过 `DiffMode` 选用。并排视图宽度不够时，退回宿主默认的非并排方式。
- **阶段 5（交互与无障碍）**：
  - **查找**：在块模型上按片段查找，结果不跨片段；会话中尚未解析的按需条目会被解析。高亮保存在 `SelectionModel` 中，与选区共用 `Changed` 事件，各片段视图像绘制选区一样绘制高亮。
  - **跳转定位**：先按估算位置滚动，目标行实例化后再按字符的实际位置微调一次，放在视口上部约 30% 处。`MarkdownView` 本身不滚动，交给外层滚动容器的 `BringIntoView`。
  - **拖选与点击**：指针移动超过 3 像素才开始拖拽选择并捕获指针，所以单击能送达链接、图片和宿主内容；按在 `ContentEntry` 控件上不开始选择。
  - **右键菜单**：使用 `MenuFlyout`，样式来自宿主主题；关闭时，在 `ContextRequested` 的隧道阶段把事件标记为已处理。
  - **无障碍**：Avalonia 12 没有公开的 `TextPattern`，读屏只能读到名称；标题通过 `AutomationProperties.HeadingLevel` 提供级别。
  - **键盘选区**：Shift+↑/↓ 用字符框上下方的探测点按坐标命中，探测距离逐步加大，以跨过块之间的间距。
  - **修正**：Markdig 中脚注组的区间从文档开头算起，曾使 `FootnotesBlock` 的 key 为 0，与第一个块重复；现在取其中脚注的区间。
- **编辑器预览的集成（2026-10-21，issue #5 / #6 / #7）**：
  - **整篇替换用 `RefreshMarkdown`，不让 `StreamingDocument` 可变**。被否决：让文档支持任意位置编辑（会破坏"源码只追加、冻结前缀永不重解析"，流式与整篇解析的等价性无从保证）；`Markdown` 赋值自动保留状态（会让宿主把旧文档的选区和状态带进一个真正的新文档，赋值仍然表示换文档）。
  - **对齐在 Core（`SnapshotEdit`），不新增 `DiffKind`**：平移的块用 `Update` 表示，`SnapshotDiff` / `DocumentPresenter.Apply` / `TranscriptPanel` 三处 `switch` 不用动。前缀 / 后缀剥离后，中间部分按内容做最长公共子序列（块数乘积超过 100 万则退回按位置替换）。按位置硬配对在一次刷新里有两处编辑时会把状态和选区配到不相干的块上，所以不取。
  - **不接受宿主传入的改动范围**：前缀 / 后缀 + 内容配对对编辑已经足够，宿主给的范围不会让结果更准，却多一个会写错的参数。
  - **`Load` 原子化**：原来是 `Reset` 再 `Complete`，工作线程可能在两者之间取到"已清空但未完成"的文本，产生一个只含尾部补全的中间快照；对刷新而言那会是一帧错误画面。
  - **过期结果靠版本丢弃**：视图记下刷新时的 `StreamingDocument.Version`，`Latest.Version` 没追上就不应用，A → B → C 之间的快照不会上屏。
  - **阅读锚点身份用视图级 `DocumentId`**：`DocumentSnapshot.Generation` 在 `Complete()` 时也递增，同一份流式源码完成前后不同。已知限制：外部直接对绑定的 `StreamingDocument.Reset` 视图察觉不到。
  - **源码位置只做块级**：`TextPoint.Offset` 是片段纯文本偏移，粗体 / 链接的标记不在里面，不能当源码偏移。精确到字符需要给行内内容记源码区间，成本与收益不成比例，先不做。
  - **任务勾选只发请求**：StreamMark 不写文件、不改快照，宿主改自己的原文并管撤销；只在快照完整时发；`ListItemBlock.TaskMarker` 进指纹时记相对列表项起点的位置，整体平移不产生 Update。
