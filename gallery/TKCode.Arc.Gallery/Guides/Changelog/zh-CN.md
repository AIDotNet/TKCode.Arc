---
title: 更新日志
source-hash: 350f15cc72a5
---

# 更新日志

TKCode.Arc 的所有重要变更，最新的在前。版本遵循[语义化版本](https://semver.org/)。版本带有 `-preview` 后缀期间，API 在各发布版本之间仍可能变化；升级前请阅读说明，并在项目中固定版本号。同样的列表也保存在仓库的 `CHANGELOG.md` 中。

每次发布附带的 NuGet 包：`TKCode.Arc`、`TKCode.Arc.Core`、`TKCode.Arc.Fonts` 和 `TKCode.Arc.Icons.Lucide`。`TKCode.Arc.Blocks` 与 `TKCode.Arc.AdvancedCharts` 从仓库构建。

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
