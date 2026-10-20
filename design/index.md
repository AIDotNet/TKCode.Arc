# AtomUI.Arc 设计文档方案（design/）

## Context
- 需求：在 /Users/token/code/AtomUI.Arc（空目录，非 git 仓库）建立基于 AtomUI 的扩展组件库 AtomUI.Arc，复刻 Arc（https://uiarc.dev，React + CSS 变量 + Motion）的风格与动画。
  - AtomUI 版本：6.2.6，Avalonia 12.1.4，LGPL-3.0，源码在 /Users/token/code/AtomUI。
  - Arc 的免费部分为 MIT，Copyright (c) 2026 Elia Kuratli。
- 范围：Arc 侧边栏 22 个大类、110 个免费组件，与用户截图计数一致。
  - 站点快照（2026-10-20）：共 123 个组件，其中 13 个 Pro，含整组 "Advanced charts"。
  - Pro 组件受许可约束，不抓取源码、不复刻。
- 本阶段交付物：只有 design/ 下的中文 Markdown 文档，不写实现代码，也不搭建工程。
  - 每个组件的详细规格在该组件所属阶段开始前编写，即先写文档再写代码。
- 约束：
  - 代码按 大类目录 → 组件目录 → 实现文件 组织。
  - 库目标框架为 net10.0;net11.0。本机已装 SDK 10.0.401 和 11.0.100-rc.2，以及 .NET 10.0.12 运行时。

## 决策汇总（全部已由用户确认）
| # | 决策 | 结论（文档中每项写一份 ADR） |
|---|---|---|
| 1 | AtomUI 依赖 | NuGet + CPM。AtomUI.* 精确锁定为 `[6.2.6]`，Avalonia* 为 `[12.1.4]`，开启传递锁定。AtomUI 每次发版，Arc 都重建并重新发版。 |
| 2 | 控件关系 | 独立实现：控件派生自 Avalonia 基类，只复用 AtomUI 的公开基础设施（白名单 / 内部 API 黑名单见下文）。 |
| 3 | 程序集 | `AtomUI.Arc.Core`（Token、动效引擎、基元、服务）+ `AtomUI.Arc`（22 大类控件）+ `AtomUI.Arc.Icons.Lucide` + `AtomUI.Arc.Fonts`。<br>两个 src 项目共用 `AtomUIThemeControlCatalog=AtomUI.Arc`。<br>入口为 `UseArcCore()` 和 `UseArc()`；`UseArc()` 依赖 `UseArcCore` 与 `UseDesktopControls`。 |
| 4 | 命名 | 类名不加 Arc 前缀，取 Arc 标题的 PascalCase（Checkbox、Combobox、Textarea、MultiSelect、JsonViewer），例外为 ThemeSwitcher。<br>Semantic Part 的 Path 一律以 `arc.` 开头，用来规避 ATOMUIGEN037。<br>Token 类与控件同命名空间。Arc 的任何命名空间都不映射到 https://atomui.net。 |
| 5 | 命名空间 | 扁平：所有控件都在 `AtomUI.Arc`，生成的 Token 类型在 `AtomUI.Arc.DesignTokens`。<br>基础层的公开类型放 `AtomUI.Arc.Motion`、`AtomUI.Arc.Primitives`、`AtomUI.Arc.Localization`，都映射到 xmlns `https://atomui.net/arc`，前缀 `arc`。 |
| 6 | Token | 载体控件 `ArcDesign` + `ArcDesignToken`。<br>accent 通道是 ArcDesign identity 上的 `ColorPrimary`，不写 Algorithm 属性；未覆盖时回落到根级 `ColorPrimary`。<br>每个公开控件都有自己的 `XxxToken`，允许为空。 |
| 7 | 动效 | 自研弹簧引擎，与 framer-motion 的 `physical(visualDuration, bounce)` 等价，打断时保留速度，逐帧驱动。<br>另提供可在 ControlTheme 中声明的 `Spring*Transition` / `ArcSpringEasing`。<br>动效分三级：Full / Reduced / Off。 |
| 8 | 文档粒度 | 架构、Token、动效、基元、工程规范、110 组件清单、路线图、规格模板。详细规格按阶段编写。 |
| 9 | 许可证 | MIT。THIRD-PARTY-NOTICES 保留 Arc（MIT）、Lucide（ISC）、Geist/Inter（OFL）的声明；对 AtomUI 的依赖为 LGPL-3.0，链接已发布的二进制。 |
| 10 | 目标框架 / SDK | 库为 net10.0;net11.0；global.json 锁 `11.0.100-rc.2.26504.105`（allowPrerelease、rollForward=latestFeature）。<br>构建机必须装 .NET 10 运行时，因为 AtomUI 的构建 worker 是 net10。<br>测试跑 net10，另加 net11 腿。Browser 不在范围内（AtomUI 锁定 SDK 10.0.300 + net10-browser）。<br>平台：Windows / macOS / Linux 桌面。 |
| 11 | Gallery 应用 | 自建轻量 Gallery，`gallery/AtomUI.Arc.Gallery` 加桌面宿主，用 Arc 自己的控件。<br>整体复刻 uiarc.dev/components（顶栏、带搜索与筛选的侧边栏、组件页与 On this page）；Appearance 弹层可切换明暗 / accent / 动效等级（语言待做）；示例代码用 Arc 的 CodeBlock 展示；页面显式注册，便于 AOT。 |

## 文档必须写入的关键技术约束（已在 AtomUI 源码中核实）
- **第三方包路径**：见 `/Users/token/code/AtomUI/docs/guides/theming/third-party-control-packages.md` 和 `docs/guides/startup/package-authoring.md`。
  - 生成器、主题资产与注册 targets 由 `AtomUI.Desktop.Controls` 包的 buildTransitive 自动接入，`AtomUI.Core` 包不带这些工具。
  - 入口写法：`GeneratedControlPackageRegistration.Definition.Register(builder, p => p.DependsOn(...))`。
  - `Themes/**/*.axaml` 都会被当作主题资产，文件名以 `Themes` 结尾的会被当成聚合文件跳过。
  - 应用必须在 `UseAtomUI(...)` 里显式调用入口。
- **ATOMUIGEN037**：语义部件样式类型统一生成为 `AtomUI.Theme.Styling.{控件名}{Path}Style`，映射到 https://atomui.net（`src/AtomUI.Generator/SemanticParts/SemanticPartStyleContract.cs`）。
  - 与被引用程序集里同名的公开类型冲突时报错（`SemanticPartStyleTypeValidator.cs`）。
  - Path 只要求 camelCase 的点分段，不要求父部件存在，所以 `Path = "arc.label"` 生成 `ButtonArcLabelStyle`，可行。
- **Token 体系**：外部包不能新增全局 Token 集或算法。
  - 禁用 `[GlobalDesignToken]`、`[ThemeAlgorithm]`、`[TokenValueConverter]`。
  - Own token 的覆盖值在 `CalculateTokenValues` 之后才生效，不能当 seed 用（`src/AtomUI.Core/Theme/Compilation/ThemeCompiler.cs`）。
  - Own token 的类型只能是内置转换器支持的类型；名字不能与全局 Token 同名（ATOMUIGEN019）。
  - 颜色 Token 以 `ImmutableSolidColorBrush` 发布，所以每个颜色变体都要单独做成 Token。
  - `CreateControlTokenBinding` 不能跨 owner 绑定。C# 侧绑定用原始 boxed key（提供 `ArcTokens.Bind` 辅助）或 `ControlTokenAccessor.Capture<ArcDesign>(owner)`（`src/AtomUI.Core/Theme/Resources/ControlTokenAccessor.cs`）。
  - 静态值（如 Transition 的 Duration）用 Arc 自己的 `{arc:ArcTokenValue …}`，参照 `src/AtomUI.Core/Data/SharedTokenResourceValue.cs` 实现。
- **accent 解析 `ArcPalette.Resolve(seed, isDark)`**，按顺序匹配：
  1. 8 个预设的手调明 / 暗表；
  2. 暗色派生过的预设 seed（用公开的 `PaletteGenerator` 预先算出）；
  3. AtomUI 默认的 #1677FF，映射到 Arc 默认预设；
  4. 其他颜色走 OKLCH 推导。
  - 局部 accent 用 `ThemeConfigProvider` 作用域实现。
  - 按外观覆盖：用 Light/Dark 两份主题定义，或 FollowSystem 的两个请求模板。
- **控件 identity**：只有存在 own token 类时，生成器才会产出控件 identity。
  - 所以每个公开控件都要有 `XxxToken`，`ConfigureMotionBindingStyle()` 和 `IsMotionEnabled` 的绑定都依赖它。
  - 控件在 `OnInitialized` 里调用 `DisableTransitions()`，Loaded 之后再 `EnableTransitions()`，保证首帧不播动画。
  - Transitions 只能在 ControlTheme 的 style setter 中声明。
- **可复用的公开 API（白名单）**：
  - 主题管理与 Token：`IThemeManager`、`ThemeConfigBuilder`、`ThemeConfigProvider`。
  - 动效：MotionScene `AbstractMotion` 及其预设（`src/AtomUI.Core/MotionScene/`）。
  - 浮层：`Popup`、`ScopeAwareOverlayLayer`、`ScopeAwareAdornerLayer`、`MotionActor`。
  - 图标：`Icon`、`IconProvider<TKind>`（`src/AtomUI.Core/Controls/Icon/`）、`AbstractIconPackageGenerator`（AtomUI.Icons.Shared）。
  - 图片：`ImageLoader`、`AsyncImage`。
  - 本地化：`[LanguageCatalog]` + xlf。
  - Composition 参考：`SplashProgressBar`、`AbstractSpinIndicator`。
- **不可用的内部 API（黑名单）**：`WaveSpiritDecorator`、`FeedbackStack/*`、`InputControlFrame`、`Notifiable*Transition`、`MotionTransformOptionsAnimator`、`FontCollectionLifetime`、`UseCommonControls`。
- **Avalonia 12.1.4 已提供，直接复用**：
  - `:focus-visible`：键盘焦点环。
  - `EffectiveViewportChanged`：进入视口检测。
  - `TopLevel.RequestAnimationFrame`：逐帧驱动。
  - `BoxShadowsTransition`、`EffectTransition`（BlurEffect）、`AvaloniaHeadlessPlatform.ForceRenderTimerTick`。
  - Avalonia 没有减少动效的系统 API，可选做平台探针。
- **本地化**：提供 en-US、zh-CN、zh-TW，计划补 pt-BR。应用声明的每种语言都必须被所有已启用模块完整覆盖，否则报 ATOMUIREG014。
- **字体与图标**：
  - Inter 复用 Avalonia.Fonts.Inter（`fonts:Inter`）；Geist 用 EmbeddedFontCollection，并在 `OnCommit` 中注册。
  - Lucide 生成器要输出所有 SVG 元素类型，描边宽度按 1.75 × size / 24 缩放。

## design/ 文件树（每个目录的入口都叫 overview.md）
```
design/overview.md                 入口：目标、范围与非目标、阅读顺序、决策摘要、文档状态表
design/glossary.md                 术语对照：Arc ↔ Avalonia ↔ AtomUI（token / part / presence / spring / level）
decisions/overview.md              ADR 索引、模板、状态；0001–0010 每个决策一份：
  0001-atomui-dependency  0002-independent-controls  0003-assemblies-and-packages  0004-naming-and-namespaces
  0005-arcdesign-token  0006-motion-engine  0007-docs-before-code  0008-licensing  0009-tfm-and-sdk  0010-gallery-app
architecture/overview.md           分层图（Avalonia→AtomUI→Arc.Core→Arc→App）、程序集职责、启动注册序列、横切导航
architecture/solution-layout.md    仓库 / 项目 / 命名空间 / xmlns、组件目录文件约定（见下方示例）
architecture/atomui-integration.md 包注册、生成器契约、短名冲突矩阵、API 白名单与黑名单、与 AtomUI 控件混用
architecture/target-frameworks.md  SDK / 运行时 / 工具链 / AOT 矩阵与 CI 要求
architecture/theming/              overview · arc-design-token · token-catalog · accent-and-palette ·
                                   control-tokens · theme-configuration · verification
architecture/motion/               overview · spring-engine · values-and-scheduler · transitions-integration ·
                                   presence-and-layout · reduced-motion · primitives-catalog(P1–P30/S1–S12) · verification
architecture/primitives.md         非动效服务（S5 SizeObserver、S6 视口、S8 浮层、S9 列表键盘导航、S10 手势、S11 格式化、S12 可暂停计时器）
architecture/forms-and-fields.md   字段通用结构（标签 / 描述 / 错误 / 尺寸 / 校验态），供 TextFields / SpecialInputs / Selects / Pickers 复用
architecture/overlays-and-input.md 锚定与模态浮层、关闭、焦点陷阱与归还、遮罩模糊、手势与键盘约定
architecture/icons-and-fonts.md    Lucide 生成器、描边缩放、Core 内置字形子集、字体包与 key
architecture/accessibility.md      焦点环、AutomationPeer、键盘模型、减少动效与减少透明度
architecture/localization.md       语言目录、xlf、ATOMUIREG014、数字与日期格式化
components/overview.md             110 行总清单：类名 / slug / 链接 / 大类 / Arc 依赖 / 原语 / AtomUI 参考 / 阶段 / 状态（阶段分配只以这里为准）
components/arc-catalog-snapshot.md 站点快照：123 / 13 / 110、各类计数、URL 规则、日期
components/spec-template.md        组件详细规格模板
components/charts/chart-foundation.md  图表共享层：比例尺、坐标轴、series 色、路径重采样与形变、指针与提示、可访问数据
components/<22 大类>/overview.md   buttons gestures menus text-fields special-inputs selects toggles sliders pickers
                                   editors navigation expand overlays messages progress avatars cards charts tables
                                   activity media text-effects
roadmap/overview.md                阶段总表、依赖约束、每阶段交付与退出标准、里程碑、风险登记
roadmap/phase-0.md                 基础设施交付物与 spike S0.1–S0.10 的通过标准
engineering/overview.md · coding-guidelines.md · testing.md · gallery-app.md · packaging-and-release.md ·
  licensing.md · docs-verification.md
```
- 单个文件超过约 400 行时按职责拆分；每条事实只在一处维护，其他地方用相对链接引用。

**组件目录约定**（写入 solution-layout.md）：
```
src/AtomUI.Arc/Toggles/Switch/
  Switch.cs  Switch.SemanticParts.cs(Path="arc.*")  SwitchToken.cs  SwitchThumb.cs(internal presenter)
  Themes/SwitchTheme.axaml(+.cs)  Themes/SwitchThumbTheme.axaml  Localization/(按需：LangResourceKind.cs + en-US/zh-CN/zh-TW.xlf)
```
- 仓库布局：`src/{AtomUI.Arc.Core, AtomUI.Arc, AtomUI.Arc.Icons.Lucide, AtomUI.Arc.Fonts}`、`tools/AtomUI.Arc.Icons.Lucide.Generator`、`tests/{AtomUI.Arc.Core.Tests, AtomUI.Arc.Tests, PackageConsumers/*}`、`gallery/{AtomUI.Arc.Gallery, AtomUI.Arc.Gallery.Desktop}`。
- 根目录文件：global.json、NuGet.config（加一个本地 feed）、Directory.Build.props/targets、Directory.Packages.props、build/*.props、.editorconfig、LICENSE、THIRD-PARTY-NOTICES.md。

## 关键文档大纲
- **architecture/overview.md**：定位与非目标 · 设计原则 · 分层图 · 程序集职责表 · 启动与注册序列 · 运行时数据流（Token / 动效） · 横切关注点导航 · 已知限制
- **theming/overview.md**：Token 分层（Global → ArcDesign → Own → ControlTheme） · 载体模型 · 明暗 / accent / 作用域的数据流 · 消费契约（AXAML / 静态值 / C#） · 配置入口 · 约束与诊断
- **theming/token-catalog.md**：唯一的 Token 来源。分组如下（每项注明类型与明 / 暗值）：
  中性色、表面、文字、边框、accent、选择控件、状态色、series、阴影、圆角（18/26/34/pill）、控件高度（36/44/50）、间距、字体（字族 / 字号 12–72 / 字重 400·500 / 字距 / 行高）、时长、缓动、弹簧、MotionMode、模糊、焦点环、图标描边
- **motion/overview.md**：保真目标（与 framer 等价） · 三层模型（ControlTheme 过渡 / 代码 MotionValue / Presence 与 Layout 服务） · 选型决策表 · 与 AtomUI 的边界 · 等级策略 · 性能与生命周期
  - 核心类型：`SpringSpec`、`SpringSolver`、`MotionValue<T>`、`MotionScheduler`、`TopLevelFrameClock` / `ManualFrameClock`、`ArcSpringEasing`、`Spring*Transition`（每控件状态存 ConditionalWeakTable）、`PresenceHost`、`LayoutAnimator`(FLIP)、`SharedElementScope`、`SpringSizeDecorator`
- **atomui-integration.md**：依赖与版本锁 · 包注册（UseArcCore / UseArc / DependsOn / OnCommit / OnFirstUse） · 生成器契约 · 冲突矩阵 · API 白名单与内部 API 替代 · 禁止项 · 与 AtomUI 控件混用（C# 别名指引） · 验证
- **spec-template.md**：
  - 元数据：类名、slug、URL、大类、阶段、依赖、原语、AtomUI 参照、Since
  - Arc 行为摘要 · 范围 · 公共 API（StyledProperty / DirectProperty / 事件 / 命令）
  - 模板与部件 · 状态与伪类 · 视觉到 Token 的映射（明 / 暗）
  - 动效表：触发 → 属性 → 规格 → Reduced/Off 降级
  - Own Token · Semantic Parts（arc.*） · 键盘与可访问性 · 本地化 · 性能与 AOT · 测试计划 · Gallery 用例 · 未决问题
- **大类 overview（22 份统一结构）**：大类定位 · 组件一览表 · 每个组件包含：
  - Arc 行为摘要与适用场景、交互与键盘要点
  - 动效（原语 P#、token）、Avalonia 基类建议
  - AtomUI 可参考实现（只用于学习）、依赖与阶段、风险与未决项
- **roadmap/overview.md**：原则（先文档后代码，原语随首个使用它的组件落地） · 阶段总表（目标 / 数量 / 引入的原语与服务 / 退出标准） · 依赖约束 · 里程碑版本 · 风险登记（版本锁、.NET 11 GA、性能、富文本编辑器）

## 组件清单与分期（110）
阶段：P0 基础设施与试点 · P1 被依赖组件 · P2 轻依赖 · P3 原子组件 · P4 展开与导航 · P5 浮层与菜单 · P6 输入与选择 · P7 手势、物理、计时与反馈 · P8 图表 · P9 数据、活动与重型编辑器

| 大类 (代码目录) | 组件（阶段） |
|---|---|
| Buttons (10) | Button P0, ActionButton P3, SplitButton P5, ButtonGroup P5, FloatingButtonGroup P2, ExpandingButtonGroup P7, CopyButton P1, IconMorph P3, ConfirmMorph P7, ThemeSwitcher P2 |
| Gestures (3) | HoldToConfirm P7, SwipeActions P7, Reactions P5 |
| Menus (3) | DropdownMenu P5, ContextMenu P5, UserMenu P5 |
| TextFields (7) | Input P3, Textarea P3, PasswordField P3, PasswordStrength P6, SearchField P3, ExpandingSearch P6, InlineEdit P6 |
| SpecialInputs (7) | NumberField P6, MoneyInput P6, PhoneInput P6, ScrubInput P7, TagInput P6, MentionInput P6, ShortcutRecorder P6 |
| Selects (5) | Select P5, Combobox P6, MultiSelect P6, MorphSelect P6, ChipGroup P6 |
| Toggles (6) | RadioCards P3, BillingToggle P2, Checkbox P3, RadioGroup P3, Switch P0, SegmentedControl P3 |
| Sliders (2) | Slider P7, ElasticSlider P7 |
| Pickers (5) | Calendar P1, DatePicker P2, DateRangePicker P6, TimePicker P6, ColorPicker P6 |
| Editors (3) | RichTextEditor P9, SignaturePad P9, FileDropzone P9 |
| Navigation (3) | Tabs P4, Breadcrumb P4, Pagination P4 |
| Expand (5) | ScrollArea P4, Accordion P4, ExpandableCard P4, ResizablePanels P7, ExpandableText P4 |
| Overlays (6) | Dialog P5, Drawer P5, BottomSheet P5, Popover P5, HoverCard P5, Tooltip P1 |
| Messages (5) | Alert P3, Toast P7, ToastStack P7, AnnouncementBar P7, CookieConsent P7 |
| Progress (5) | Progress P3, Skeleton P3, Stepper P4, Countdown P7, UsageMeter P3 |
| Avatars (3) | Avatar P1, AvatarGroup P2, Badge P3 |
| Cards (3) | Card P5, MetricCard P2, EmptyState P3 |
| Charts (13) | AnimatedCounter P1；LineChart, BarChart, DonutChart, Streamgraph, BrushChart, WaffleChart, SlopeChart, Sparkline, Gauge, ActivityHeatmap, Ridgeline, Treemap 均为 P8 |
| Tables (6) | SortableDataTable P9, TreeView P9, ReorderableList P7, FilterToolbar P6, CodeBlock P2, JsonViewer P9 |
| Activity (3) | Timeline P9, CommentThread P9, ChatThread P9 |
| Media (3) | ImageCompare P7, Carousel P7, CardStack P7 |
| TextEffects (4) | TextReveal P3, InViewTitle P3, TextMorph P1, TextShimmer P3 |

- 各阶段数量：P0 2 / P1 6 / P2 7 / P3 19 / P4 8 / P5 13 / P6 17 / P7 17 / P8 12 / P9 9，合计 110。
- 硬依赖都排在更早的阶段：ThemeSwitcher→Button、FloatingButtonGroup→Tooltip、BillingToggle / MetricCard→AnimatedCounter、ColorPicker→TextMorph、DatePicker→Calendar、CookieConsent→Button+Switch、AvatarGroup→Avatar、CodeBlock→CopyButton。
- 共享表面：DropdownMenu 的菜单表面复用于 SplitButton、ButtonGroup、SwipeActions；Dialog 的表面复用于 Drawer、BottomSheet、Card 快速预览；Popover 复用于 HoverCard、Reactions。

## Phase 0 spike（写入 roadmap/phase-0.md，每项写明通过标准）
| 编号 | 内容 | 通过标准 |
|---|---|---|
| S0.1 | 工具链 | net10 和 net11 双目标都能构建，零 ATOMUIGEN / ATOMUIREG 诊断 |
| S0.2 | 载体 Token | 明暗切换生效；accent 覆盖在明暗两种外观下都得到预设值；`ThemeConfigProvider` 作用域生效；`Capture<ArcDesign>` 可用；运行时能读到 `MotionMode` |
| S0.3 | 命名 | `arc.label` 构建通过；写成 `icon` 能复现 ATOMUIGEN037 |
| S0.4 | NuGet 消费 | 打包到本地 feed 后，干净缓存的应用能构建并启动；缺一种语言时 ATOMUIREG014 会触发 |
| S0.5 | NativeAOT | osx-arm64 下 net10 与 net11 两个发布产物都能运行、切换主题并播放动画 |
| S0.6 | 弹簧引擎 | 5 个预设与 framer `spring()` 参考值误差 ≤1e-3；打断时速度连续；空闲时不请求帧；200 个弹簧每帧 ≤2ms |
| S0.7 | Spring*Transition | 多个控件共享主题实例时安全；首帧不播动画 |
| S0.8 | Lucide | 16/20/24 尺寸下与参考渲染的像素差在阈值内 |
| S0.9 | Gallery 骨架 | 侧边栏、主题工具栏、示例页、AOT 发布都可用 |
| S0.10 | 浮层与字体 | AtomUI Popup 上的弹簧 presence 正常；Geist/Inter 注册不冲突 |

## 执行步骤（计划批准后）
1. 先写基础文档：overview、glossary、decisions（10 份 ADR）、architecture/*（含 theming/、motion/），内容取自上文的已核实约束。
2. 收集组件事实（只读）：从 https://uiarc.dev/r/catalog.json 和 `/components/<slug>/markdown` 读取 110 个免费组件；不访问 Pro 内容。
3. 写组件文档：
   - components/overview.md（总清单）、arc-catalog-snapshot.md、spec-template.md、charts/chart-foundation.md。
   - 22 份大类 overview 交给 4 个子代理并行起草，统一给出上面的大纲与约定。
   - 最后由我统一校对术语、阶段与链接。
4. 写 roadmap/overview.md、roadmap/phase-0.md、engineering/*。
5. 运行验证（下一节），修正问题后交用户审阅。没有 git 仓库，不提交。

## 验证
在 scratchpad 中用 Python 脚本只读检查 design/，逻辑写进 engineering/docs-verification.md：
1. **清单**：恰好 110 行，slug 不重复；各大类计数按顺序为 10,3,3,7,7,5,6,2,5,3,3,5,6,5,5,3,3,13,6,3,3,4；与快照一致（123−13）。
2. **阶段**：每个组件只出现一次；各阶段计数为 2,6,7,19,8,13,17,17,12,9；硬依赖不晚于依赖方。
3. **原语**：P1–P30 与 S1–S12 都映射到 Core 类型和首个使用组件；没有组件排在它所用原语之前。
4. **Token**：名称唯一；与 AtomUI 6.2.6 的 `SharedTokenKind` 无重名（从 `~/.nuget/packages/atomui.core/6.2.6` 读取）；类型都在允许列表内。
5. **结构**：相对链接全部可解析；目录入口都是 overview.md，没有 index.md；每个 overview 列出全部子文件；单文件不超过约 400 行。
6. **ADR**：每份都有状态、背景、决策、备选方案、后果，且索引完整。
7. **人工通读**：关键约束（`arc.` 路径、XxxToken 必备、accent 不写 Algorithm、版本锁、Browser 排除）在相关文档之间一致。
