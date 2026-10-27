---
title: Metric explorer
summary: 一排 KPI 卡片，每张都能展开为可拖动检视的图表：卡片表面长成面板，标签页让折线逐点从一个指标变形到另一个指标，范围开关在按日数据上移动一个镜头窗口，并把本周期与上一周期对照绘制。
source-hash: 08fe2ea12c17
---

<Demo name="Preview" />

## 何时使用

`MetricExplorer` 位于增长或财务页面的顶部：四个左右重要的数字，各自带有近期走势，并提供一种无需离开页面就能细看的方式。卡片显示最新值、所选范围内的变化和一条迷你趋势线。打开一张卡片，它就变成同一指标的图表，带有其他指标的标签页、一个范围开关，以及一个跟随指针的读数卡。

- **需要再深一层细节的 KPI 行。** 图表在背后画出上一周期的线，回答“这是偶发波动还是趋势？”，无需单独的页面。
- **周期对比。** 图表上的每一天都与一个周期之前的同一天对比，因此拖动检视时读到的是“Sep 6 vs Aug 7”和变化，而不只是一个值。
- **两三种日数据。** 金额、计数和比率共用一个区块：每个指标各自选择其格式、坐标轴精度，以及上升是否算好。

## 何时不用

- **单个主打数字。** 请使用统计卡片；为一个值而展开图表是多余的开销。
- **几十个指标。** 这一行和标签页条是为三到五个指标设计的。更多时请使用表格或 `Metrics dashboard` 区块。
- **非按日的数据。** 图表假定每天一个值，并将一个范围与其之前相同天数的范围比较。对不规则系列进行刷选和缩放，请使用 `Revenue explorer`。
- **精确查询。** 读数卡一次只显示一天；当用户需要复制数字时，请在区块旁放一个表格。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`。它的内容是一组 `ExplorerMetric`。每个指标有 `Title`（卡片的标签和读数卡的标题）、用于标签页的 `ShortTitle`、用于图表下方那一行的 `Description`，以及 `Values`：一个按日的系列，最早的在前，最后一个值属于 `EndDate`（未设置时为今天）。该系列至少需要最长范围的两倍再加一天，因为图表会把每个范围与它之前的范围对照绘制。

```xml
<arc:MetricExplorer Title="Workspace health" Caption="Refreshed Oct 14, 2026 at 08:30 UTC">
    <arc:ExplorerMetric Title="Annual run-rate" ShortTitle="ARR" ValueFormat="CurrencyCompact"
                        Values="{Binding Arr}" EndDate="{Binding Today}"
                        Description="Yearly value of active contracts, after discounts." />
</arc:MetricExplorer>
```

```csharp
explorer.Metrics.Add(new ExplorerMetric("Weekly active teams", teams, ExplorerValueFormat.Count) { ShortTitle = "Teams" });
explorer.Open(0);          // the surface grows out of the first card
explorer.SelectedRangeIndex = 1;   // 90 days
```

`MetricsSource` 可改为接收你自己持有的集合（`ObservableCollection` 会被跟踪）。修改指标的属性会就地更新区块。

### 结构

- **标题区**：`Title` 在左，`Caption` 在右；没有空间时，说明文字会换行到标题下方。
- **卡片**：标签、最新值、带变化和范围（“30 days”）的箭头，以及以圆点收尾的迷你趋势线。宽度小于 760 时为两列，大于 760 时为四列。悬停会使边缘变深；Enter、Space 或单击会打开卡片。
- **展开的图表**：指标标签页（附带它们的值）、关闭按钮、读数卡、范围开关、带日期跨度的图例、图表，以及指标的描述。

### 数值格式、变化与色调

`ValueFormat` 选择数值的写法：`Count`（“5,874”）、`Currency`（“$12,480”）、`CurrencyCompact`（“$4.62M”、“$412.00K”）、`Percent`（以百分比为单位的值，“27.3%”）或 `Ratio`（“1.12×”）。`ValueFormatter` 可以取而代之，`AxisFormatter` 接收刻度的值和刻度之间的步长，用于 y 标签。y 坐标轴始终有五个刻度，并在 1、1.5、2、2.5、3、4 和 5 乘以 10 的幂中选择能容纳两条线且留有 3% 余量的最细一档；`MaxAxisDecimals` 限制步长最多可有几位小数（紧凑金额为两位，百分比为一位，计数为零位）。

`ChangeMode` 为 `Relative` 时是常见的“+1.6%”，为 `Points` 时用于比率（“+1.2 pts”）。箭头表示方向，颜色表示这是否是好事：设置 `IsHigherBetter="False"` 后，上升显示为红色，下降显示为绿色，因此成本下降会以向下的箭头读作好消息。

<Demo name="Finance" />

### 范围

范围是一个天数窗口：默认为 30、90 和 364（按钮“30D”、“90D”和“12M”）。可用 `Ranges` 或 `RangesSource` 创建自己的范围；每个 `ExplorerRange` 有 `Label`、`Days`、卡片显示的 `CardText`（“30 days”）以及读数卡显示的 `CompareText`（“vs 30 days ago”）。`SparklineStep` 是迷你趋势线相邻点之间的天数：按日为 1，长范围为 7，因此一年保持在 53 个点。`SelectedRangeIndex` 是双向的，卡片会跟随它，所以你留着打开的那张图表决定了收起的卡片上显示什么。

<Demo name="Ranges" />

`ShowPreviousPeriod` 会隐藏虚线以及检视时的第二个圆点。

### 检视某一天

在绘图区上移动、按下并拖动，或触摸它：最近的一天会出现十字线、每条线上的一个圆点，以及坐标轴上的日期胶囊。读数卡里的数字、变化和对比会切换为那一天与一个周期之前对应日期的比较（“Sep 6 vs Aug 7”），变化的数字会滚动。离开绘图区会清除检视；手指抬起也会。

`ScrubIndex` 是被检视的那一天（0 是范围的第一天），`ScrubChanged` 报告日期、值和上一周期的值。单击会让图表获得焦点；使用键盘时，Tab 到图表上会显示最后一天，方向键则逐天移动。

<Demo name="Events" />

### 窄容器

区块读取的是自身宽度。小于 760 时卡片占两列。小于 640 时，展开的图表把内边距收紧到 16，去掉标签页中的值，并把范围开关放到读数卡下方。在 440 及以下，卡片的标签变为两行、数字更小，标签页共占一整行，关闭按钮移到下方、紧挨读数卡，x 坐标轴选用更稀疏的刻度档位。

<Demo name="Compact" />

### 文字与屏幕阅读器文本

每一个可见和可朗读的词都是带英文默认值的属性：图例（`ThisPeriodText`、`PreviousPeriodText`）、关闭按钮、标签页和范围的标签、卡片名称（`CardNameFormat`）、播报（`AnnouncementFormat`、`ClosedAnnouncementFormat`）、检视时的对比（`ScrubCompareFormat`）以及表示方向的词（`UpText`、`DownText`、`FlatText`）。

## API 参考

<!-- api: MetricExplorer -->

<!-- api: ExplorerMetric -->

<!-- api: ExplorerRange -->

<!-- api: ExplorerValueFormat -->

<!-- api: ExplorerChangeMode -->

<!-- api: MetricExplorerMetricEventArgs -->

<!-- api: MetricExplorerRangeEventArgs -->

<!-- api: MetricExplorerScrubEventArgs -->

## 无障碍

- 每张卡片是一个按钮，以其内容命名：“Annual run-rate: $4.62M, up 1.6% over 30 days. Open chart”。
- 标签页条名为“Metric”，范围开关名为“Chart range”；两者都是一个 Tab 停靠点，并跟随所选项。
- 图表是一个滑块，名为“Scrub”加上指标名，其值文本是被检视的那一天及其上一周期的对应日（“Sep 6, 2026: $4.55M. Aug 7, 2026: $4.43M”）。获得焦点时会围绕绘图区绘制一个环。装饰性部分（迷你趋势线、坐标轴、图例）对辅助技术隐藏。
- 在展开的图表中切换指标或范围时，会以礼貌方式播报最新值和变化；关闭图表时会播报显示了多少个指标。检视某一天不会播报，因为滑块已经读出了它。
- 方向除颜色外始终有文字（“up”、“down”、“unchanged”）。
- 颜色来自浅色和深色下的主题令牌；折线、圆点和胶囊在两种主题下都保持对比度。

### 键盘

- **Tab** 依次经过各张卡片，然后（在展开时）经过所选标签页、关闭按钮、所选范围和图表。
- 卡片上的 **Enter / Space** 打开它，并将焦点移到它的标签页。
- 标签页和范围开关上的 **← →** 立即选中上一项或下一项并循环；**Home / End** 跳到两端。
- 图表上的 **← ↓** 和 **→ ↑** 步进到前一天或后一天，并停在两端；**Home / End** 跳到第一天和最后一天。
- **Escape** 首先清除检视；再按一次（或在图表的任何其他控件上按）会关闭图表，并把焦点返回到它的卡片。

## 动效

- **卡片到图表**：卡片的表面在一条平滑弹簧上（约半秒）移动并长成图表的矩形，其他卡片则立即退去；图表的内容带着轻微上浮和模糊淡入，从飞行开始后 120 ms 起。关闭时反向运行：表面缩回它的卡片，其他表面淡入回来，卡片内容再次升起（打开过的那张最后）。区块的高度立即变化。
- **范围开关**：一个罩在按日数据上的镜头。一条弹簧移动视野内的天数，每一帧都为该窗口重建折线，因此点数连续变化，而今天始终固定在右边缘；y 范围跟随同一条弹簧，x 标签随之滑动。标签档位（每周、每两周、每月）进行交叉淡入：旧档位在 160 ms 内离开，新档位在 240 ms 内到来。
- **指标切换**：折线上的每个点都从当前位置弹向新位置，曲线因此从一个指标流向下一个；中途再次切换会从屏幕上的形状开始。y 标签从底部起逐个交叉淡入，间隔 35 ms。读数卡的各行上升并淡入；变化的数字像里程表一样滚动，箭头随之转向。
- **标签页和范围开关**：高亮在同一条弹簧上滑动。
- **检视**出现、移动和消失都没有动画。
- 减弱动效时，表面、折线和高亮会立即落位，只保留 120 ms 的淡入淡出；关闭则立即应用所有变化。

## 相关

- `Metrics dashboard`、`KPI drilldown` 和 `Revenue explorer`，其他围绕 KPI 的 Analytics 区块。
- `SegmentedControl`，该区块使用的范围开关。
- `Cohort retention`，另一个在同一数据集的不同视图之间变形的区块。
