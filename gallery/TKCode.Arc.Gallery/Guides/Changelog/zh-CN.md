---
title: 更新日志
source-hash: 55098180ce25
---

# 更新日志

TKCode.Arc 的所有重要变更，最新的在前。版本遵循[语义化版本](https://semver.org/)。版本带有 `-preview` 后缀期间，API 在各发布版本之间仍可能变化；升级前请阅读说明，并在项目中固定版本号。同样的列表也保存在仓库的 `CHANGELOG.md` 中。

每次发布附带的 NuGet 包：`TKCode.Arc`、`TKCode.Arc.Core`、`TKCode.Arc.Fonts` `TKCode.Arc.Icons.Lucide`、`TKCode.Arc.Blocks`、`TKCode.Arc.AdvancedCharts` 、`TKCode.Arc.Special`、`TKCode.Arc.Workflow`、`TKCode.Arc.Themes.Pixel`、`TKCode.Arc.Themes.Corners`、`TKCode.Arc.Mobile`、`TKCode.Arc.Terminal`、`TKCode.Arc.Audio`、`TKCode.Arc.Audio.Native` 和 `TKCode.Arc.Audio.Browser`。

## 0.3.1-preview.6 (2026-10-28)

### 新增

- **Cascader**(Extras):按层级逐级选择一个节点,如省、市、区。字段、菜单、滑动高亮和清除按钮与 `Select` 一致;父节点的子级在右侧一列中展开,菜单宽度用弹簧过渡到新的宽度。数据通过委托读取(`ChildrenSelector`、`TextSelector`、`ValueSelector`),不使用反射。`SelectedItem`、`SelectedValue` 与 `SelectedPath` 始终保持一致,数据到达之前写入的值会被保留,数据到达后再解析。选项:`LeafOnly`、`ExpandTrigger`、`ShowFullPath`、`PathSeparator`、`IsClearable`、`IsReadOnly`;完整的键盘支持。

## 0.3.1-preview.5 (2026-10-28)

### 新增

- **Audio**(新包 `TKCode.Arc.Audio`、`TKCode.Arc.Audio.Native` 与 `TKCode.Arc.Audio.Browser`):媒体类 Block 使用的 `IAudioPlayer` 抽象,默认静音模拟;桌面经 miniaudio 播放(`ArcAudioNative.Use()`),WebAssembly 经 HTML audio 元素播放(`ArcAudioBrowser.UseAsync()`)。
- **Blocks**:`MediaPlayer`(封面、播放列表、进度条、上一首与下一首、音量与静音)和 `FileUpload`(拖放区、文件列表、进度、校验)。
- **Special**:新增 Cursor 分组,包含 `DotGrid`、`GravityWell` 和 `InkFluid` 三个指针效果。
- **Gallery**:`/demo` 下的 Northstar 仪表盘演示,复刻 uiarc.dev/demo 的十一个页面和登录页;从首页的"查看演示"或顶栏菜单的"演示"进入。

### 修复

- **ArcContentSwap**:控件被切换出去后再次显示不再抛出异常(在 Webhooks 检查面板里切换 Payload 与 Headers 会导致程序崩溃)。
- **Charts**:平均线旁边的坐标刻度保持隐藏,平均值标签不再与它重叠。
- **Webhooks**:Payload 与 Headers 切换后都从第一行开始显示。

## 0.3.1-preview.4 (2026-10-27)

### 新增

- **Terminal**(新包 `TKCode.Arc.Terminal`,net10.0 与 net11.0):`TerminalView` 控件和 libghostty-vt 的托管封装(VT 解析、屏幕状态、选区、输入编码)。原生库随包放在 `runtimes/<rid>/native`,覆盖 Windows、macOS 和 Linux。

## 0.3.1-preview.3 (2026-10-27)

### 新增

- **Blocks**：Analytics、Developer、People、Finance 与 LineCharts 新增约 25 个区块（API 密钥、Webhook、集成、环境变量、团队与角色、指标探索、MRR 瀑布图、同期群留存、分级统计地图、行情终端等），附 Gallery 页面、示例与测试。
- 新增 **RichTextEditor** 与 **ActionSwap** 控件。
- **行动端**：`Dialog`、`Toast`、`ActionSheet`、`Toolbar`、`PullToRefresh`、`Radio` 与 `IconButton`，键盘避让与 Arc 动效。
- **Gallery**：桌面端支持检查更新并设置 macOS Dock 图标；浏览器端通过 HTTP 加载地图数据。

### 变更

- 重做 **Table**、**Editors**、**Selects** 与 **Toggles**。

### 修复

- **TimePicker**、**FilterMenu**：行的空白区域不再响应指针输入 (#20)；**Combobox**：清除后不再打开列表。
- **Password**：显隐选择与输入状态保持一致 (#21)。
- **Gallery**：修复 Windows 上侧边栏搜索框重复提交输入法内容的问题。

## 0.3.1-preview.2 (2026-10-26)

### 新增

- **圆角主题**：新包 `TKCode.Arc.Themes.Corners`（`WithArcCorners(style, scope)`），只通过令牌覆盖为所有 Arc 控件切换直角或缩放圆角；颜色、边框、阴影和动效保持不变。
- **行动端**：`ListGroup` / `ListItem`、`Sheet`、`TextField` 与 `Searchbar`，大标题 `Navbar` 以及更多 `Button` 变体；Gallery 新增列表分组、导航栈与底部面板的页面和示例。
- **Core**：新增更多设计令牌与 `ArcTokens` 辅助方法，用于绑定圆角半径。

### 变更

- **像素主题**：圆角与主题资源改由共享的圆角表重新生成。
- **Blocks**：`WeekCalendar` 使用新的边线令牌绘制边缘。

### 修复

- **Stepper**：当前步骤的光晕不再被裁切 (#19)。

## 0.3.1-preview.1 (2026-10-26)

### 新增

- **移动端**：新包 `TKCode.Arc.Mobile`（`UseArcMobile()`），首批触摸优先控件：`Button`（按压态、44 × 44 触摸目标）、`Switch`、`Page`（页眉、内容与页脚位于安全区内）、`Navbar` 和 `Tabbar`。只依赖 `TKCode.Arc.Core`，有自己的命名空间。尚未在 Android 或 iOS 真机上验证，并且仍会经由 `TKCode.Arc.Core` 间接引入 `Avalonia.Desktop` 与 `AtomUI.Desktop.Controls`。
- **Gallery › Mobile**：新增 Mobile 区，用手机框预览（iPhone 15、Pixel、小屏手机、平板，竖屏与横屏，状态栏、模拟安全区、软键盘占位）。这是桌面上的模拟，不是真机。
- **像素风主题**：新包 `TKCode.Arc.Themes.Pixel`（`UseArcPixelTheme()`）：直角、硬边阴影、受限调色板，明暗两套；像素字体 Silkscreen 与 VT323（SIL OFL）随 `TKCode.Arc.Fonts` 提供（`WithArcPixelFonts()`）。Gallery 的外观面板新增「风格」切换：Arc 或 Pixel。
- **表格 › TreeTable**：带嵌套行、三态选择、保持层级的排序、懒加载子节点与 treegrid 键盘导航的表格。Gallery 用「New」标记新组件，持续 60 天。
- **Core**：边框宽度与圆角的设计 Token（`EdgeThickness`、`FocusEdgeThickness`、`RadiusStep*`、`EdgeThickness*`），主题可以改写它们；所有默认值与原先的取值一致。

### 变更

- **Gallery**（网页）：WebAssembly 宿主重新改为不做提前编译，因为编译版在启动时崩溃。图标地址带上版本号，浏览器会丢弃旧图标。

### 修复

- **Carousel**：计时已耗尽的幻灯片不再不让出线程就再次推进轮播，而是重新开始一个间隔。

## 0.3.0-preview.5 (2026-10-24)

### 新增

- **工作流画布**：新包 `TKCode.Arc.Workflow`（`UseArcWorkflow()`）：`WorkflowCanvas` 显示步骤与连线组成的图并播放运行过程，支持平移缩放、编辑、连线、撤销重做、复制粘贴、节点模板、小地图，500 个步骤的布局约 2 毫秒。另有 `WorkflowNodeCard` 与 `WorkflowZoomControls`。
- **Blocks › 认证**：`SignIn`、`SignupForm`、`LoginSplit`、`LoginCentered`、`LoginImmersive`、`OtpInput`、`OnboardingIllustration` 和 `MultiStepForm`。
- **Core**：`PanZoomTransform`、`WheelRouter` 和 `LayeredGraphLayout`。

### 变更

- **Gallery**（网页）：站点改为 WebAssembly 提前编译，慢速 CPU 上动画更流畅；首次访问下载量更大。

### 修复

- **内存**：许多控件（`TreeView`、`ChatThread`、`Markdown`、图表、`DataGrid`、AI、Projects 和 Special 区块等）在整个生命周期内都监听其显示的数据，使页面一直留在内存中；现在只在位于可视树时监听。`SiteFooter`、`SemanticZoom`、`AgentStatus` 和 Gallery 页面也不再泄漏。
- **菜单**：关闭的面板不再把遮罩留在窗口上。
- **ChatThread**：表情选择器不再向控制台写调试信息。
- **Accordion** 与 **ExpandableCard**：展开时分隔线开销更低，外观不变。
- **Markdown** 与 AI 回答改用 StreamMark `0.1.0`。

## 0.3.0-preview.4 (2026-10-24)

### 新增

- **特别组件 › AI**：`TKCode.Arc.Special` 中的五个智能体界面组件：`AgentStatus`（实时状态胶囊，可展开查看最近的步骤）、`ToolCall` 与 `ToolCallGroup`（显示输入与输出的工具调用行，可批准和重试）、`AgentPlan`（可编辑的计划草稿，批准后成为实时清单）、`AgentTimeline`（在同一时间轴上回放多智能体运行）和 `ContextWindow`（带压缩阈值的 200 格令牌预算网格）。
- **Blocks › 页眉与页脚**：`SiteHeader`、`SiteFooter`、`CtaSection` 和 `NewsletterSignup`，移植自 Arc 的免费区块。
- **Blocks › AI**：模型竞技场（`ModelArena`，两个模型的盲测：流式回答、投票和排行）和文档提取（`DocumentExtraction`，文档旁的表单随模型阅读逐项填写，每个值都链接到其来源文本）。九个 AI 区块现已全部可用。
- **AnnouncementBar**：`DismissalDuration` 让记住的关闭状态在一段时间后失效。

### 变更

- **Gallery**：语言在顶栏有了独立的菜单，首次启动跟随系统语言并会被记住。首页新增友情链接，文档页加载时显示占位内容。

### 修复

- **Combobox**（#9）：聚焦时不再显示深色的环，只把边框变为 `BorderStrong`，与 Arc 一致。
- **Gallery**（#8、#10）：示例不再切掉行内编辑框和焦点环的左侧。
- **Gallery**（#11）：以 NativeAOT 发布的桌面 Gallery 能重新加载文档页和截图。

## 0.3.0-preview.3 (2026-10-23)

### 修复

- **Select**：选项高亮是直角；现在使用主题的选项圆角（14）。
- **Select**：Full 动效级别下，选中后菜单会在字段下方的文字上淡出。现在按文档约定立即关闭；为此 `ArcPopup` 新增了 `IsExitInstant`。Reduced 级别仍然淡出。
- **Combobox、MorphSelect**：在 macOS 上点击输入框可能因 `NSInvalidArgumentException` 导致应用退出。无障碍实时区域不再接收空文本。

## 0.3.0-preview.2 (2026-10-23)

### 新增

- **特别组件**：新包 `TKCode.Arc.Special`（`UseArcSpecial()`）中的五个 AI 动画讲解：`AgentLoop`、`MixtureOfExperts`、`SpeculativeDecoding`、`PromptCache` 和 `ReasoningTree`，基于共用的 `Explainer` 基类。所有数字都由数据计算得出。
- **Blocks**：五个项目区块（`WeekCalendar`、`AvailabilityPicker`、`ProjectBoard`、`ReleaseReadiness`、`KanbanBoard`），六个展示区块（`ActivityTerrain`、`RevenueGlobe`、`CustomerGalaxy`、`SemanticZoom`、`LayoutMorph`、`ExplodedView`），以及首批 AI 区块（模型选择器、智能体记忆、AI 输入框、AI 聊天、AI 回答，以及侧边面板和提示词工作台）。
- **Core**：`ArcSurface.Elevation`（对话框、抽屉、气泡卡片和菜单的内容知道自己位于抬升的界面上）、`RollingDigitsFalloff.None`、新的弹簧、`ArcContentSwap` 模式、`ArcRing`、`WordDiff`、`WordPacer`、`DecoratedTextBlock`，以及另外 23 个 Lucide 图标。
- **Button**：抬升界面上的幽灵按钮现在有清晰可见的悬停和按下填充。
- **特殊输入**：`TokenTextEditor`，令牌作为整体处理的多行编辑器；`MentionInput` 基于它构建。
- **Gallery**：文档站点提供 `llms.txt`、`llms-full.txt`、每个页面的 Markdown 版本，以及供 AI 助手使用的代理技能。

### 变更

- **破坏性变更，仓库迁移**：仓库现在位于 `AIDotNet/TKCode.Arc`，XAML 命名空间随之改变。请把 `xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"` 改为 `xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"`（Lucide 命名空间同样处理）。
- **Sidebar**：`ResizeMode` 现在默认为 `Overlay`；设为 `ResizeMode="Push"` 可恢复之前的行为。
- **性能**：动效调度器每帧只推进每个驱动一次，隐藏的循环不再产生开销，缓存模糊效果和文本排版，ActivityTerrain 与 LayoutMorph 大幅提速。
- **Gallery**：WebAssembly 站点在首帧之前少下载约 1.5 MB。

### 修复

- **Tabs**、**PageHeader**、**Breadcrumb**、**ArcContentSwap**、**DataGrid**：模板不再以 `null` 数据上下文构建。
- **Core**：中途离开窗口的内容切换不再恢复为半透明；锚定弹层的阴影重新使用圆角。
- **Sidebar**：从侧栏轨道展开后，内容不再保留第一帧的宽度。
- **Motion**：从 `Transparent` 渐入或渐出填充时不再闪出灰色（`ArcBrushTransition` 使用预乘 alpha 混合）。
- **TagInput**、**Textarea**、**TimePicker**：去掉了粗重的焦点环；移除了 `FieldFrameToken` 的 `FocusRing*` 值。

完整清单见仓库中的 `CHANGELOG.md`。

## 0.3.0-preview.1 (2026-10-22)

### 新增

- **Core**：`ArcZoom.Scale`，一个可继承的附加属性，供缩放窗口内容的应用使用。锚定弹层（菜单、选择器、工具提示、悬停卡片、气泡卡片）绘制在窗口的覆盖层里，位于被缩放的容器之外，因此原先无法跟随缩放；现在它们会按继承到的比例排版。`ArcPopup.Surface` 返回赋给 `Child` 的那个界面元素（弹层现在把它放进一个缩放元素中，因此 `Child` 本身是那个元素）。
- **ExpandableContent**：把 ExpandableText 的截断、切换按钮和动效用在任意内容上，例如可选择的文本、只读编辑器或渲染后的 Markdown。截断停在内容中第一段可见文本的某一行上（`TextBlock`、`SelectableTextBlock`，或实现了新增 `IExpandableLineSource` 的控件）；其他内容按 `Lines × LineHeight` 截断。`ExpandableText` 与 `ExpandableContent` 共用新增的 `ExpandableClamp` 基类。
- **ExpandableText**、**ExpandableContent**：`IsFadeEnabled` 可以关闭最后一行上的渐隐。

### 变更

- AtomUI 锁定为 6.2.8；0.2.0-preview.1 基于 6.2.7 构建。
- **ExpandableText**：折叠时，文本只排版到截断线之后的一行，展开时才完整排版。600 行的文本折叠时测得约 1ms，而不是约 135ms。带有行内控件的文本仍会完整排版，以便截断线以下的链接可以获得焦点。截断现在停在文本自身的行边界上，而不是 `Lines × LineHeight`。
- **ExpandableText**：`Lines`、`IsExpanded`、`MoreLabel`、`LessLabel`、`LineHeight`、`IsOverflowing` 和 `ExpandedChanged` 现在声明在新增的 `ExpandableClamp` 基类上。通过 `ExpandableText` 使用它们的代码不受影响。

### 修复

- **ExpandableText**：随新的数据上下文到来的 `IsExpanded` 变化（例如在回收的项模板中）不再产生动画，也不再滚动页面。
- **Switch**、**Checkbox**：随新的数据上下文到来的值（例如详情视图被复用于下一项时）会立即显示；滑块不再滑动和拉伸，对勾也不再像用户点击过那样画出。
- **Progress**：`Value` 变化但整数百分比不变时，不再重启弹簧，也不再更新读数和无障碍状态。每秒汇报数百次的下载现在每个百分点只更新一次。
- **SegmentedControl** 以及其他带滑动指示条的切换控件：隐藏的控件不再在窗口的每次布局时放置指示条；控件再次显示时才会放置。
- **GlidingIndicator**：描边画笔在帧之间保留，不再每次渲染都新建。
- TreeView 和 JsonViewer：在下次布局之前对行再次做出更改，而第一次更改的行还在关闭过程中，会在 `RowTop` 中抛出 `IndexOutOfRangeException`。面板现在在行变化后立即刷新行的几何信息。

## 0.2.0-preview.1 (2026-10-21)

### 新增

- **目标框架**：库同时面向 `net8.0`，与 `net10.0`、`net11.0` 并列。net8.0 构建需要 StreamMark `0.1.0-alpha.25`，该版本为其包加入了 net8.0。
- **Markdown**（`TKCode.Arc`）：大纲与查找。提供 `GetOutline`、`ScrollToPoint`、`ScrollToAnchor`、`Find` 和 `GetSelectedText`，并有 `IsDocumentReady`、`MatchCount`、`CurrentMatchIndex`、`HasSelection` 属性，以及 `DocumentReady` 和 `SearchChanged` 事件。宿主可以围绕文档搭建大纲侧栏、查找栏和复制操作。
- **RichTextEditor**（`TKCode.Arc`）：`TrySetMarkdown` 导入 Markdown，并返回 `RichTextImportReport`，列出编辑器无法保留的内容（`RichTextImportIssueKind`：标题层级、代码围栏的信息字符串、波浪线围栏、表格等）。
- **DataGrid**（`TKCode.Arc.AdvancedCharts`，仓库内，尚未发布到 NuGet）：单元格渲染器（徽标、进度、链接、图标文字、复选框、操作）、校验规则（必填、范围、最大长度、模式）、条件格式规则、`CellInvoked` 事件、右键菜单事件、CSV 导出选项，以及筛选与布局状态。
- **Blocks**（`TKCode.Arc.Blocks`，仓库内，尚未发布到 NuGet）：命令面板对话框；带值与日期筛选、详情和操作的搜索结果；侧边栏的折叠与调整大小模式；重做的通知中心。

### 变更

- AtomUI 锁定为 6.2.7；0.1.1-preview.1 基于 6.2.6 构建。AtomUI 依赖随 Arc 版本一同变化。
- DataGrid 的 CSV 导出默认会处理电子表格公式（`NeutralizeCsvFormulas`），超过 `MaxPasteCells`（默认 1,000,000 个单元格）的粘贴会被拒绝。
- 在 .NET 8 和 9 上，DataGrid 的自然文本排序使用内置比较器；在 .NET 10 及更高版本上仍使用 `CompareOptions.NumericOrdering`。

### 修复

- `LiveLoop.Start` 在已经在输出帧时提前返回，因此每次指针移动都启动循环的调用方不再每次重新读取门控条件。

## 0.1.1-preview.1 (2026-10-21)

### 新增

- **组件**：Activity、Content、Editors、Illustrations、Media 和 SpecialInputs 分类，以及 ButtonGroup、SplitButton、Card、Reactions、SwipeActions，还有 Color、Date、DateRange 和 Time 选取器。
- **Core**：`MotionTimeline` 与 `LiveLoop` 动效驱动器、`SharedElement` 基础组件、`ArcBlur` 与 `ArcViewport`。
- **Blocks**（`TKCode.Arc.Blocks`）：AppShell 区块（Sidebar、PageHeader、CommandPalette、SearchResults）以及设置区块（SettingsPage、SecuritySettings、BillingOverview、UsageBilling、CancelFlow、SettingsCommand）。
- **AdvancedCharts**（`TKCode.Arc.AdvancedCharts`）：一个新项目，首先提供 DataGrid 控件。
- **图库**
  - 元件页面即其带实时示例的文档页面；每个示例都是一个 `.axaml` 文件，其“代码”标签页显示该源码。
  - 侧边栏新增文档区，按 `Cmd+K` 或 `Ctrl+K` 可在窗口任何位置聚焦搜索。
  - 从 XML 文档生成的 API 表格，附 zh-CN 与 zh-TW 翻译；外壳文本与文档页面提供 en-US、zh-CN 和 zh-TW 版本。
  - 通过 StreamMark `0.1.0-alpha.23` 实现 Markdown 渲染与代码高亮，这是首个可在浏览器中运行的版本。
  - WebAssembly 宿主（`TKCode.Arc.Gallery.Browser`），以及桌面和浏览器宿主的应用图标。
- **发布**
  - 图库的 WebAssembly 镜像在 GitHub 上构建，以 `ghcr.io/atomuicommunity/tkcode-arc-gallery` 发布，并采用 brotli、预压缩文件和浏览器缓存提供服务。
  - 图库桌面应用的发布构建：Windows 与 Linux（未签名），macOS（已签名并完成公证）。
  - 通过 nuget.org 可信发布，从 CI 打包并发布 NuGet 包。
- **工具**：API 文档翻译流程的脚本，以及图标构建器。
- 简体中文版 README 与赞助者部分。

### 变更

- 各分类的共享辅助代码由 Core 的共享基础组件取代。
- 图库页面从按分类编写的 C# 页面迁移为 Markdown 文档页面与 `.axaml` 示例。
- 为许多控件的公共属性和事件补充了 XML `<summary>` 文本；它们会填充图库的 API 表格。

### 修复

- 动效：等待暂停的循环可能让渲染器空闲且帧请求挂起；现在帧会持续产生。
- HoldToConfirm、ConfirmMorph（长按模式）、Tabs 面板与 Carousel 视口在复用裁剪区域时没有使被裁剪的视觉元素失效，因此长按填充不会移动，且增长中的面板或调整大小后的轮播会保留过期的裁剪区域。
- Countdown：静止的滚轮会被绘制为普通字形，使滴答作响的表盘帧时间减半。
- 当 XAML 将 `Minimum` 设得高于当前 `Maximum` 时（例如 `Minimum="360" Maximum="900"`），Slider 与 ElasticSlider 会因 `Math.Clamp` 抛出异常。
- 切换选中的标签页时，Tabs 不再滚动其所在的页面；标签列表会自行显示选中的标签页。
- 图库浏览器宿主：为 WebAssembly 构建原生 Skia，发布时跳过裁剪。

### 从 0.1.0-preview.1 升级

- 请将所有 TKCode.Arc 包同时更新到相同版本。
- 新增的分类与控件除 `UseArc()` 外无需额外设置。区块与 DataGrid 需要各自的注册（`UseArcBlocks()`、`UseArcAdvancedCharts()`）以及项目引用，详见**安装**。

## 0.1.0-preview.1 (2026-10-20)

首个预览版本。
