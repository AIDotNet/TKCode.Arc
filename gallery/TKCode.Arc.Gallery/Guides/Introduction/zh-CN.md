---
title: 介绍
source-hash: 34ecfe855e77
---

# 介绍

TKCode.Arc 将 [Arc](https://uiarc.dev) 带到 Avalonia：相同的组件分组、相同的视觉语言和相同的弹簧动效，以原生 Avalonia 控件的形式构建在 [AtomUI](https://github.com/AtomUI/AtomUI) 之上。它移植了免费的 Arc 组件（22 个分组中的 110 个组件），并新增页面级区块、电子表格风格的数据表格和流式 Markdown 渲染器，使桌面产品可以由一套统一的工具包组装而成，而不是拼凑各种样式。

本图库即是该库的文档。每个组件页面都根据组件源码编写，展示可交互的实时示例，并列出生成的 API 参考。

![由纯 Arc 组件搭建的账单概览：指标卡片、状态徽标、表单字段和操作按钮](images/ProductOverview.png)

```xml
<StackPanel Width="760" Spacing="20">
    <Grid ColumnDefinitions="*,Auto">
        <StackPanel Spacing="4">
            <TextBlock Text="Acme Analytics"
                       FontSize="22"
                       FontWeight="Medium"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Workspace overview, October 2026"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="8" VerticalAlignment="Center">
            <arc:Badge Content="Pro plan" Tone="Success" />
            <arc:Button Content="Export report" Variant="Secondary" />
            <arc:Button Content="Invite members" />
        </StackPanel>
    </Grid>
    <UniformGrid Columns="3">
        <arc:MetricCard Label="Monthly revenue" Prefix="$" Value="48210" Change="+6.4%" Context="vs last month" Margin="0,0,8,0" />
        <arc:MetricCard Label="Active accounts" Value="1284" Change="+42" Context="Across 3 regions" Margin="4,0" />
        <arc:MetricCard Label="Churn" Suffix="%" Value="1.8" Decimals="1" Change="-0.3 pts" Context="Lower is better" Margin="8,0,0,0" />
    </UniformGrid>
    <arc:Input Label="Billing email"
               Text="billing@acme.com"
               Description="Invoices and payment receipts are sent here." />
</StackPanel>
```

## 库包含什么

| 部分 | 提供的内容 |
|---|---|
| 组件 | Arc 22 个分组中的 110 个控件：按钮、手势、菜单、文本字段、特殊输入、选择器、切换、滑块、选取器、编辑器、导航、展开、浮层、消息、进度、头像、卡片、图表、表格、活动、媒体和文字效果。 |
| 区块 | 用于应用外壳和设置的页面级组合：Sidebar、PageHeader、CommandPalette、SearchResults、NotificationCenter、EmptyStates、SettingsPage、SettingsCommand、SecuritySettings、BillingOverview、UsageBilling 和 CancelFlow。 |
| AdvancedCharts | `DataGrid`，一个电子表格风格的表格，支持排序、筛选、编辑、选区统计、撤销和导出。 |
| 插图 | 用于落地页和引导页的动画讲解场景（DataFlow、PaymentFlow、MultiRegionFailover、VectorSearch）。 |
| 内容 | 流式 `Markdown` 控件和带语法高亮的 `CodeBlock`，用于文档和 AI 聊天回复。 |
| 图标与字体 | 核心中内置的 187 个 Lucide 字形、作为独立包提供的完整 Lucide 集（2155 个图标），以及 Geist 与 Geist Mono。 |

### 组件分组

| 分组 | 数量 | 典型用途 |
|---|---|---|
| 按钮 | 10 | 主要与次要操作、拆分与成组操作、复制、确认、主题切换 |
| 手势 | 3 | 长按确认、表情反应、列表行的滑动操作 |
| 菜单 | 3 | 右键菜单、下拉菜单、账号菜单 |
| 文本字段 | 7 | 单行与多行输入、密码、搜索、原位编辑 |
| 特殊输入 | 7 | 数字、金额、电话号码、标签、提及、拖动调整和键盘快捷键 |
| 选择器 | 5 | 从列表中单选与多选、可搜索的选择、标签 |
| 切换 | 6 | 开关、复选框、单选组与单选卡片、分段控件、计费周期切换 |
| 滑块 | 2 | 范围与数值，含弹性变体 |
| 选取器 | 5 | 日历、日期、日期范围、时间、颜色 |
| 编辑器 | 3 | 富文本、文件拖放区、签名 |
| 导航 | 3 | 标签页、面包屑、分页 |
| 展开 | 5 | 手风琴、可展开卡片与文字、可调整大小的面板、滚动区域 |
| 浮层 | 6 | 对话框、抽屉、底部面板、弹出框、悬停卡片、提示 |
| 消息 | 5 | 警告、轻提示与轻提示堆叠、公告栏、Cookie 同意 |
| 进度 | 5 | 进度条、骨架屏、步骤条、倒计时、用量仪表 |
| 头像 | 3 | 头像、头像组、徽标 |
| 卡片 | 3 | 内容卡片、指标卡片、空状态 |
| 图表 | 13 | 折线、柱状、环形、仪表盘、迷你图、矩形树图、热力图等 |
| 表格 | 6 | 可排序表格、筛选工具栏、树视图、可重新排序的列表、JSON 与代码查看器 |
| 活动 | 3 | 聊天线程、评论线程、时间线 |
| 媒体 | 3 | 轮播、卡片堆叠、图片对比 |
| 文字效果 | 4 | 文字显现、变形、闪烁、视口内标题 |

## 设计原则

**Arc 保真。** 尺寸、圆角、颜色、缓动曲线和弹簧常数都来自 Arc 自身的 ArcDesign 设计令牌，因此用 TKCode.Arc 构建的界面读起来就像网页上的 Arc。在 Avalonia 与浏览器不同的地方（焦点、键盘、浮层、无障碍），行为被映射为原生桌面的对应做法，而不是照搬网页。

**原生 Avalonia 控件。** 每个控件都派生自 Avalonia 基类型：`Input` 是 `TextBox`，`Button` 是 `Button`，`Select` 和 `MultiSelect` 是 `SelectingItemsControl`。数据绑定、命令、样式、数据校验和键盘操作都与 Avalonia 开发者的预期一致。

**使用设计令牌，而不是硬编码的值。** 颜色、圆角、阴影、字体和动效都来自 ArcDesign 设计令牌。在浅色与深色之间切换，或选择八种强调色之一，会同时重绘每个控件。参见 **主题**。

**克制的动效。** 弹簧与时长遵循 Arc 的动效系统，每个动画都有三个级别（完整、减弱和关闭），可以按窗口或按容器设置。参见 **动效**。

**默认具备无障碍。** 字段由标签命名，错误通过实时区域播报，每个可交互控件都有键盘操作模型，每个页面都记录了辅助技术会读取的内容。

**为 AOT 做好准备。** 本库不使用反射，兼容裁剪与 NativeAOT，因此桌面应用可以发布为体积小、启动快的单文件。

## 包

| 包 | 内容 | 来源 |
|---|---|---|
| `TKCode.Arc` | 组件（`UseArc()`）、流式 `Markdown` 控件和 `CodeBlock`。引入 `TKCode.Arc.Core`。 | NuGet |
| `TKCode.Arc.Core` | ArcDesign 设计令牌与调色板、弹簧动效引擎、共享基础组件、187 个内置 Lucide 字形。 | NuGet |
| `TKCode.Arc.Fonts` | Geist 与 Geist Mono（`WithArcFonts()`）。 | NuGet |
| `TKCode.Arc.Icons.Lucide` | 完整的 Lucide 集（2155 个图标）：`LucideIcon` 控件和 `LucideGeometry` 标记扩展。 | NuGet |
| `TKCode.Arc.Blocks` | 页面级区块（`UseArcBlocks()`）。 | 从仓库构建 |
| `TKCode.Arc.AdvancedCharts` | `DataGrid`（`UseArcAdvancedCharts()`）。 | 从仓库构建 |

**安装**一节说明了如何将它们添加到应用中。

## 要求与平台

- **.NET**：这些包面向 `net8.0`、`net10.0` 和 `net11.0`。
- **Avalonia 与 AtomUI**：Avalonia `12.1.4` 与 AtomUI `6.2.8`，锁定为精确版本。请在你的应用中使用相同的版本。
- **平台**：Windows、macOS 和 Linux 桌面端。图库也可以通过其 WebAssembly 宿主在浏览器中运行。
- **文本与代码的渲染**：Markdown 与语法高亮使用 StreamMark，由 `TKCode.Arc` 包引入。

## 使用本图库

- **侧边栏。** 文档（本指南）、组件（Arc 的 22 个分组，加上 Charts 中的 DataGrid、Content 和 Illustrations）以及区块。`Cmd+K` 或 `Ctrl+K` 可在任何位置聚焦搜索；All、Ready 与规划中筛选器可缩小列表范围。
- **组件页面。** 页眉显示包及其安装命令。顶部的预览是实时的。每个页面的各节顺序相同：何时使用、何时不用、用法（包含实时示例、业务示例和使用指南）、从 XML 文档生成的 API 参考、无障碍、动效和相关组件。
- **示例。** 每个实时示例都有“预览”和“代码”两个标签页，“代码”标签页显示其 `.axaml` 与代码后置文件。较大的业务界面截图会显示生成它的确切 XAML，并随浅色或深色外观变化。
- **复制页面。** 将整个页面复制为 Markdown，并包含每个示例的源码，可直接粘贴到问题单、设计评审或 AI 助手中。
- **外观。** “外观”面板可在浅色与深色之间切换，选择八种强调色之一，设置动效级别，并切换语言（English、简体中文、繁體中文）。你看到的一切，包括图片，都会遵循这些设置。

## 状态与版本

TKCode.Arc 处于预览阶段。版本遵循[语义化版本](https://semver.org/)；只要版本带有 `-preview` 后缀，API 在各发布版本之间仍可能变化，每个版本都会在 **更新日志** 中列出破坏性变更。请在项目中固定确切的版本，并在升级前阅读更新日志。

## 许可证

TKCode.Arc 以 MIT 许可证发布。它移植了 Arc（MIT），使用 Lucide（ISC）、Geist 字体（OFL）、StreamMark、TextMateSharp、Oniguruma 和 Markdig，并依赖 AtomUI（LGPL-3.0）。仓库中的 `THIRD-PARTY-NOTICES.md` 列出了全部内容。

## 下一步

- **安装**：添加这些包，并显示你的第一个 Arc 界面。
- **主题**：浅色与深色、强调色和设计令牌。
- **动效**：动效级别，以及为你自己的控件编写动效。
- **AI 与 MCP**：在 Arc 代码库中与 AI 助手协作。
