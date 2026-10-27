---
title: Revenue explorer
summary: 每月经常性收入按新增、扩展和流失三层堆叠，配三个头条数字和一条概览条：拖动其窗口或手柄，路径、坐标轴和数字都会滑向新的范围，并提供范围预设、系列图例按钮、与上一周期的对比，以及同时适用于指针和键盘的十字线读数卡。
source-hash: 509e2de1d63e
---

<Demo name="Preview" />

## 何时使用

`RevenueExplorer` 在一张卡片里回答订阅制业务的月度问题：现在有多少经常性收入（MRR），新客户和老客户每月各增加了多少，流失了多少，以及是否比上一周期更好？图表把新增和扩展收入堆叠在零线上方，把流失画在零线下方，因此一个月的净变动就是各层之间的距离。图表下方的概览条容纳每一个月；它的窗口就是图表、三个头条数字和表格所显示的范围。

- **收入复盘页面。** 期末 MRR、每月平均净新增和流失率，各自带着变化量，位于图表上方并随范围变化。
- **在时间中移动。** 拖动概览窗口可以浏览历史，拉动手柄可以放大；路径、坐标轴标签和数字会滑动到位，而不是重新绘制。
- **用过去检验趋势。** "Compare" 会把相同长度的上一周期画成两条虚线，读数卡还会加上上个月的净新增。

## 何时不用

- **单个指标随时间变化。** 没有堆叠也不需要缩放时，折线图（或 `Metric explorer` 区块）更轻量。
- **从一个总额到另一个总额的桥接。** `MRR waterfall` 展示期初 MRR 如何变成期末 MRR。
- **按日或按小时的数据。** 这里的单位是月：窗口至少包含三个月，标签也是月份。
- **超过几年的月份。** 三十六到六十个月效果良好；超出后请汇总为季度。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。它的数据是一组 `RevenueMonth`，从最旧开始，每个日历月一个，不留缺口：`New` 和 `Expansion`（零或更大）、`Churn`（负数；传入正数时会被当作其负数）以及可选的该月期末 `Mrr`。省略 `Mrr` 时，区块会从 `OpeningMrr`（第一个月之前的收入）开始，把每个月的净新增累加到上一个月上。`OpeningMrr` 也是第一个月流失率和第一个变化量的基数。

```xml
<arc:RevenueExplorer Title="Subscription revenue" OpeningMrr="1050000">
    <arc:RevenueMonth Month="2024-03-01" New="41200" Expansion="14800" Churn="-17600" />
    <arc:RevenueMonth Month="2024-04-01" New="43900" Expansion="15300" Churn="-18200" />
</arc:RevenueExplorer>
```

```csharp
revenue.OpeningMrr = 1_050_000;
revenue.Months.AddRange(months);          // or: revenue.MonthsSource = observableMonths;
revenue.SetRange(12, 23);                 // months 12 to 23, sliding there
revenue.ApplyPreset(3);                   // the newest three months, as the "3M" button does
```

`MonthsSource` 接受由你持有的集合（`ObservableCollection` 会被跟踪）；就地修改某个月的数字会重建区块。在你选择范围之前，显示最新的 `DefaultRangeMonths`（12）个月。

### 结构

- **标题区。** `Title`，其下以文字显示的范围（`Mar – Aug 2026`；在同一年之内省略第一个年份），右侧是范围按钮。这些按钮选择最新的 3 个月、6 个月、12 个月和全部；范围为其他值时，没有按钮处于选中状态。可以用你自己的 `Presets` 替换它们。
- **头条数字。** 期末 MRR 及其相对范围前一个月的变化；每月平均净新增及其相对相同长度的上一范围的变化；流失率（流失收入除以这些月份的期初收入）及其以百分点计的变化。数值逐位滚动，箭头在向上和向下之间翻转，文字向上滑出再滑入。基数缺失的变化（最初几个月、`All`）会被省略。流失下降是好事，以绿色绘制。
- **工具栏。** 三个系列图例按钮（`New`、`Expansion`、`Churn`）和对比开关。
- **图表。** 左侧是 Y 轴标签，绘图区下方是月份标签（一月会显示年份），以及各个层。
- **概览。** 每个月以小型层显示，带有年份刻度，窗口两侧各有一个手柄。

### 范围

`RangeStart` 和 `RangeEnd`（月份索引，都包含在内，双向）就是范围；`RangeChanged` 在每次变化时报告其来源：`Preset`、`Drag`、`Keyboard`、`Click` 或 `Api`。拖动窗口时它与指针一比一移动，而范围是最接近的整月，因此数字会在窗口越过某个月时变化；图表以弹簧追随窗口，松开后窗口会落定到整月。手柄移动一条边，且与另一条边的距离永远不会小于 `MinRangeMonths`（3）。点击窗口之外的概览条，会把窗口的中心移到那里。

<Demo name="LongHistory" />

### 系列与对比

`IsNewVisible`、`IsExpansionVisible` 和 `IsChurnVisible`（双向）切换各层：被隐藏的层收缩为零，其上方的层随之下落，Y 轴重新适配，图例按钮的色块变空。取值范围跟随可见的堆叠：上界比最高堆叠高 18%，再加上最深流失的 8%，下界比最深流失低 20%。所有系列都关闭时，坐标轴保留，图表显示 `EmptyText`。

`IsCompareEnabled` 为上一周期的月份画出两条虚线（新增与扩展堆叠的顶部，以及流失），并把它们纳入 Y 轴范围。在读数卡中，会有一行加上该月的净新增。没有上一周期的月份（最初几个月）没有虚线。

<Demo name="Compare" />

### 查看某个月

指向图表：十字线和三个圆点标出范围内最近的月份，一个面板显示三个系列、净新增，以及在对比时上一周期的净值。面板位于十字线右侧 14px 处，快要超出图表时翻到它的左侧；它在月份之间滑行，并淡入淡出。`ScrubIndex`（双向）是被查看的月份，`ScrubChanged` 报告它。

<Demo name="Events" />

### 窄容器

该区块读取自身的宽度。小于 600 时绘图区高 216；小于 560 时内边距收紧到 16，头条数字得到的空间变少；小于 440 时范围按钮独占一行并平均分配，数字上下堆叠且数值靠右，图例按钮缩小。当月份间距小于 40px 时，坐标轴每隔一个（每隔两个……）月份显示一次。

<Demo name="Narrow" />

### 文字、格式和货币

每一个可见的文字都是带有英文默认值的属性（`NewLabel`、`MrrLabel`、`CompareText`、`SinceFormat`、`VersusFormat`、屏幕阅读器名称、播报格式）。金额是紧凑货币格式（`$63.3k`、`$2.56M`）：用 `CurrencySymbol` 更改符号，用 `CultureName` 更改语言，或用 `ValueFormatter`（读数卡、表格、播报）、`AxisFormatter`、`MonthFormatter`、`MonthYearFormatter` 和 `RangeFormatter` 替换文本。头条数字始终遵循 `CurrencySymbol`：它们的数字会滚动，这需要把符号和单位作为单独的部分。

<Demo name="Euro" />

## API 参考

<!-- api: RevenueExplorer -->

<!-- api: RevenueMonth -->

<!-- api: RevenueRangePreset -->

<!-- api: RevenueRangeChangedEventArgs -->

<!-- api: RevenueSeriesChangedEventArgs -->

<!-- api: RevenueCompareChangedEventArgs -->

<!-- api: RevenueScrubChangedEventArgs -->

<!-- api: RevenueSeries -->

<!-- api: RevenueRangeSource -->

## 无障碍

- 图表是一个滑块，名为 "Inspect a month"，覆盖范围内的各个月；它的值文本包含该月的所有数字（"Mar 2026: new $72.5k, expansion $31.5k, churn −$34.1k, net new $69.9k"），其帮助文本概述范围、期末 MRR 和每月平均净新增。
- 概览窗口及其两个手柄是滑块（"Selected range"、"Range start"、"Range end"），其值文本是范围或月份。
- 范围按钮和系列图例按钮是切换按钮（有按下状态）；对比开关是一个开关。每个头条数字作为一个短语朗读，并用文字说出其变化（"MRR, $2.56M, up +42.3% since Feb 2025"）。
- 一张隐藏的表格列出范围内每个月的变动和期末 MRR，并随范围变化。
- 用键盘移动范围或读数卡时，会以礼貌方式播报；读数卡和十字线对屏幕阅读器隐藏，因为相同的文本就是滑块的值。
- 键盘焦点环绘制在图例按钮、开关、绘图区（圆角 12）、窗口（圆角 10）或手柄（方形）外侧 2px 处。

### 键盘

- **Tab** 依次访问范围按钮（一个停靠点；**← →** 更改范围）、三个图例按钮、对比开关、图表、概览窗口、起点手柄和终点手柄。
- **图表：** 聚焦时在最后一个月打开读数卡；**← ↓** 和 **→ ↑** 各移动一个月，**Home / End** 跳到范围的两端，**Escape** 关闭读数卡并保留焦点（下一次方向键重新从最后一个月开始）。这里 **Shift** 不会改变步长。
- **窗口：** **← ↓** 和 **→ ↑** 移动一个月（**Shift** 为三个月），**Home / End** 到历史的两端；宽度保持不变。**起点手柄：** 移动第一个月，与最后一个月的距离永远不小于三个月；**终点手柄** 同理。
- **Enter / Space** 按下图例按钮、开关和范围按钮。

## 动效

- **范围变化**（预设、点击概览条、按键）：图表的 x 取值范围、窗口、手柄和月份标签跟随同一条 Smooth 弹簧并同时到达；路径每一帧重建（没有路径变形）。y 取值范围同样使用弹簧；新的网格线在滑入的同时于 50ms 后淡入，被移除的网格线在 160ms 内淡出。范围文字、头条文字和变化量向上滑出再滑入 0.45em；数字滚动；预设胶囊在 Gentle 弹簧上滑行。
- **拖动**：窗口精确跟随指针，图表持续追赶；松开后窗口以 Gentle 弹簧落定到整月（约半秒）。按下时抓握部件缩放到 1.15；窗口边框在悬停时从强边框色变为弱化文字色，按住时变为强调色。
- **系列**：每一层的厚度在 0 和 1 之间以弹簧变化（Smooth），它的线条随之淡出；空文本在 120ms 后淡入。图例按钮的色块在 240ms 内填满，并在一条 580ms 的曲线上于 1 与 0.8 之间缩放。
- **对比**：一条弹簧把每条虚线从当前的线混合到上一周期的线，同时淡入（在回溯的前 40% 保持不透明）。
- **读数卡**：十字线和圆点跳到每个月；面板在 160ms 内以 0.97 的缩放淡入，在弹簧上滑行，再淡出（参考实现把面板遗留在原处；这里会干净地离开）。
- **减少动效** 会让所有弹簧立即到位，只保留短暂的淡入淡出（读数卡、刻度，文字切换为 120ms 的交叉淡入淡出）。**关闭** 会立即应用所有变化。

## 相关组件

- `Metrics dashboard`、`KPI drilldown` 和 `Metric explorer`，即其他 Analytics 区块。
- `MRR waterfall`，从期初到期末 MRR 的桥接。
- `SegmentedControl`，预设所参照的公开范围开关。
