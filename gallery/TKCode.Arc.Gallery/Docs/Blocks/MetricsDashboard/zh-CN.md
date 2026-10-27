---
title: Metrics dashboard
summary: 单屏分析概览：带范围开关、会数到总数的指标标签页、可检视并可叠加上一周期的面积图，以及两个排名列表。
source-hash: 6b2b0ac2d55e
---

<Demo name="Preview" />

## 何时使用

`MetricsDashboard` 在一个紧凑的界面中回答“过去 N 天我们表现如何？”。顶部标题说明这些数字的主题，并提供 7D / 30D / 90D 开关和 Export 按钮。其下每个指标是一个标签页，显示该范围内的总数以及与上一周期相比的变化；所选标签页驱动图表。区块的末尾是两个排名列表，用于热门页面和热门来源，或任何其他两组明细。

- **产品与营销概览。** 会话数、注册数、转化率，以及流失率或跳出率，并附上带来流量的页面和引荐来源。
- **团队或客户的使用情况汇总。** 活跃团队、新项目、席位利用率和失败任务，以及最繁忙的工作区和它们所用的套餐。
- **深入报表之前的落地视图。** 使用 `PointInspected`、`SelectedMetricChanged` 和 `RangeChanged`，为某人正在查看的日期、指标或范围打开详情页。

## 何时不用

- **一张图表上有多个系列。** 图表只绘制一个指标，并可按需绘制它的上一周期。需要并排比较多个系列时请使用折线图，系列很多时请使用 `Small multiples`。
- **实时的、按秒变化的数据。** 每个点都是一天或一周。实时运营数据请使用流式图表。
- **任意日期范围。** 范围是以 `EndDate` 结尾的固定长度。当用户需要选择任意起止日期时，请使用日期范围选择器和你自己的图表。
- **超过六个指标。** 四个恰好占满一行，中等布局会把它们排成两行两列。更长的指标集请拆分到多个仪表盘，或使用表格。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`，不含示例数据。每个指标是一个 `MetricsDashboardMetric`，`Daily` 中每天一个值，最后一个值对应 `EndDate`（未设置时为今天）。数据量至少应为最长范围的两倍，这样每个范围都有上一周期可供比较；没有上一周期时，变化行会被隐藏。列表行是 `MetricsDashboardItem`，`Values` 中每个范围一个值，最大的在前。

```xml
<arc:MetricsDashboard x:Name="Dashboard"
                      Title="Insights"
                      Source="northwind.dev"
                      PrimaryListTitle="Top paths"
                      SecondaryListTitle="Referrers"
                      ListValueHeader="Sessions" />
```

```csharp
Dashboard.EndDate = new DateOnly(2026, 10, 18);
Dashboard.Metrics.Add(new MetricsDashboardMetric { Label = "Sessions", Daily = sessions });
Dashboard.Metrics.Add(new MetricsDashboardMetric
{
    Label = "Churn risk", Kind = MetricsDashboardMetricKind.Ratio, HigherIsBetter = false, Daily = churn
});
Dashboard.PrimaryItems.Add(new MetricsDashboardItem { Label = "/", Values = [2602, 10840, 29268] });
```

`MetricsSource`、`PrimaryItemsSource` 和 `SecondaryItemsSource` 接收你自己持有的集合；`ObservableCollection` 会被跟踪，修改指标或条目的属性会更新区块，并让数字数到新的值。

### 计数与比率

`Count` 指标会累加：一个范围或一周的总数是其各天之和，变化为相对值（“11.7%”）。`Ratio` 指标是以百分比为单位的比率：变化为百分点之差（“0.1 pts”）。提供 `Numerators` 和 `Denominators`（转化数与会话数，已用席位与已付费席位）后，每一周和每个范围都是各项总和的精确比率；不提供时则为每日比率的平均值。`HigherIsBetter="False"` 会让下降显示为绿色，适用于流失率、跳出率或错误率。箭头始终表示方向，屏幕阅读器会用文字读出。

### 范围与周

`Ranges`（7、30、90）是开关的各个分段，用 `RangeLabelFormat`（“{0}D”）书写，并用 `RangeNameFormat`（“Last {0} days”）为屏幕阅读器命名；`SelectedRange` 是双向的。长于 `WeeklyThreshold`（60）的范围按周绘制，周从范围的第一天开始计算：90 天得到 13 个点，最后一周只有剩下的 6 天。它的读数卡会这样说明（“Week of Oct 13 (6 days)”），因为不足一周的部分按原样求和，读数会比其他周低。

### 窄容器

区块读取的是自身宽度，而不是窗口宽度。小于 640 时标签页排成两行两列，列表上下堆叠；小于 460 时范围开关撑满标题栏，数值缩小到 18px，绘图区高 168，x 坐标轴最多显示四个日期。当标题和操作无法放在一行时，操作会移到标题下方。

<Demo name="Narrow" />

### 其他数据，其他文字

每一段文字都是带英文默认值的属性：`SubtitleFormat` 和 `SubtitleRangeFormat`、`CompareText`、`PreviousLegendText`、`PreviousValueFormat`（“{0} previous”）、`DateFormat`、`WeekTitleFormat`、`PartialWeekTitleFormat`、`ExportText`、`ExportedText`、各列表标题和 `ListValueHeader`，以及屏幕阅读器文本 `RangeGroupName`、`MetricGroupName`、`ChartNameFormat`、`ChartHelpText` 和 `ChangeContextText`。指标可以用 `ValueFormatter`（标签页和读数卡）、`AxisFormatter`（y 坐标轴）和 `DeltaFormatter`（变化）格式化自己的数字；`ListValueFormatter` 对列表起同样的作用。此演示使用 14、28 和 84 天的范围、精确的席位利用率比率，以及保留两位小数的失败任务率。

<Demo name="ProductUsage" />

### 状态与事件

`SelectedMetricIndex`、`SelectedRange` 和 `IsCompareEnabled` 是双向的，因此视图可以被保存和恢复，或由其他地方驱动。`RangeChanged`、`SelectedMetricChanged` 和 `CompareChanged` 报告每一次变化；每当检视移动时，`PointInspected` 会报告那一天（或那一周）、它的值，并在开启 Compare 时报告上一周期的值。`InspectPoint(index)` 和 `ClearInspection()` 在代码中完成指针和方向键所做的事；`ActiveIndex` 表示正在检视哪个点。

<Demo name="Events" />

### 导出

按钮本身不会导出任何内容。`ExportCommand` 会带着一个 `MetricsDashboardExportRequest`（以天为单位的范围、它的第一天和最后一天、所选指标，以及是否显示上一周期）运行；当它无法执行时，什么都不会发生。随后会触发 `ExportRequested`，按钮显示一个对勾和 `ExportedText` 作为确认，持续 `ExportConfirmDuration`（1.8 秒）。`Export()` 在代码中完成同样的事；`IsExportVisible` 可隐藏该按钮。

<Demo name="Export" />

## API 参考

<!-- api: MetricsDashboard -->

<!-- api: MetricsDashboardMetric -->

<!-- api: MetricsDashboardItem -->

<!-- api: MetricsDashboardExportRequest -->

<!-- api: MetricsDashboardPointEventArgs -->

## 无障碍

### 键盘

- **Tab** 依次经过范围开关、Export、指标标签页、Compare 和图表。范围开关和标签页各算一个停靠点，停在所选项上。
- 在范围开关中，**Left** / **Right**（以及 **Up** / **Down**）移动并选中，到两端会循环；**Home** 和 **End** 跳到第一个和最后一个范围。
- 在指标标签页中，**Left** / **Right** 移动并选中，会循环；**Home** 和 **End** 选中第一个和最后一个指标。
- **Space** 切换 Compare。**Enter** 或 **Space** 按下 Export。
- 在图表上，仅有焦点不会显示任何内容。**Right** 显示最后一个点，**Left** 显示它前面的一个；之后 **Left** / **Right** 逐点移动并停在两端，**Home** 和 **End** 跳到第一个和最后一个点，**Escape** 结束检视。把焦点移开或把指针移出绘图区也会结束检视。

### 屏幕阅读器与焦点

- 区块以其标题命名，标题是 2 级标题；列表标题是 3 级标题，并为各自的部分命名。
- 范围开关是一个由 `RangeGroupName` 命名的组；它的分段是名为“Last 7 days”等的切换按钮。
- 指标标签页是由 `MetricGroupName` 命名的组中的标签页；每个都会读出它的标签、总数和用文字表达的变化，例如“Sessions, 36,686, up 11.7% versus the previous period”，因此含义不依赖颜色。
- 图表是一幅名为“Sessions, last 30 days”的图像，以 `ChartHelpText` 作为帮助文本。被检视的点（“Sep 21, 1,370, 1,111 previous”）在变化时（无论来自键盘还是指针）会以礼貌方式播报。图例、坐标轴、网格线和读数卡本身不会被单独朗读。
- 每个列表行读作“名称，数值”；过长的名称以省略号截断，并以读数卡显示完整内容。
- Export 的确认会被播报；按钮的名称随其文字变化。
- 焦点环只在键盘焦点时绘制：围绕范围分段和 Export，位于标签页边缘内侧（因此相邻项或区块边缘永远不会遮住它），围绕 14px 的 Compare 复选框，以及围绕绘图区。

## 动效

- **范围变化**：开关的滑块在一条快速的临界阻尼弹簧上滑动（约 330 ms）；每个标签页的总数和列表数值同时在 480 ms（.22, 1, .36, 1）内从旧数字数到新数字；图表在 240 ms 内从旧画面交叉淡入到新画面。坐标轴标签、日期、副标题和变化行则立即切换。
- **指标变化**：下划线在一条平滑弹簧上滑向新的标签页（约 530 ms），在 2 × 2 布局中可跨行；图表在 240 ms 内交叉淡入。
- **Compare**：虚线的上一周期在平滑弹簧上淡入淡出（约 700 ms）；复选框的颜色在 160 ms 内变化。
- **检视**：指示线、圆点和读数卡立即出现、移动和消失。读数卡位于绘图区中远离该点的那一半，绝不会盖住它。
- **Export**：文字交叉淡入，图标以淡入加从 .8 起缩放的方式替换，耗时 160 ms；悬停会在 160 ms 内为按钮和标签页着色，按下 Export 会将它缩放到 .97。
- 区块首次出现时没有任何动画。减弱动效会让滑块和下划线直接跳转，立即替换数字和图表，并去掉颜色过渡；关闭则立即应用每一处变化。

## 相关

- `KPI drilldown`、`Metric explorer` 和 `Revenue explorer`，其他带有指标卡片和图表的 Analytics 区块。
- `Small multiples`，一眼查看多个系列。
- `SegmentedControl`，用作独立的范围开关。
