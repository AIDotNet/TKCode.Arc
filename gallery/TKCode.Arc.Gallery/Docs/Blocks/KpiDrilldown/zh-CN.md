---
title: KPI drilldown
summary: 带迷你趋势线和周期变化的 KPI 卡片，可在原位展开为详情视图，包含趋势图、上一周期对比、可用键盘操作的读数卡，以及按维度划分的明细。
source-hash: 12e55a3e2f11
---

<Demo name="Preview" />

## 何时使用

`KpiDrilldown` 是一个保留上下文的概览。卡片网格显示每个指标的最新值、相对上一周期的变化以及该周期的走势；选择一张卡片，它会在原位展开为完整的内容，按 Escape 则收回。

- **财务或增长概览。** 收入、账户数、留存、流失和转化放在同一页，每一项只需一次点击就能看到它的图表和拆分。
- **周期复盘。** 一个范围开关（7、30 或 90 天）让每张卡片和打开的详情视图一起变化，Compare 则把上一周期放在当前周期之后作为对照。
- **变化从何而来？** 明细表按套餐、地区或渠道拆分当前打开的指标，并给出每个成员的占比、值和变化。

## 何时不用

- **指标很多。** 六张卡片一目了然；超过这个数量应使用表格或指标选择器。
- **自由探索。** 详情视图只有一张图表、一个范围和一种拆分。需要缩放、刷选或选择系列时，请使用 metric explorer。
- **实时流式数值。** 该区块围绕按日的系列设计，只有范围、Compare 或维度改变时数据才会变化。

## 用法

该区块派生自 Avalonia 的 `TemplatedControl`，不含数据：请给它 `Metrics`，每个是一个 `KpiMetric`，其 `Values` 是以 `EndDate` 结尾的每日数值（至少是最长范围的两倍，这样上一周期才存在）；并给它用于明细的 `Dimensions`，以及每个指标上与之对应的 `KpiBreakdown`。

```xml
<arc:KpiDrilldown Title="Overview" RangeDays="30">
    <arc:KpiDrilldown.Dimensions>
        <arc:KpiDimension Key="tier" Label="Tier" />
    </arc:KpiDrilldown.Dimensions>
    <arc:KpiMetric Key="arr" Label="Annual run rate" Format="CurrencyCompact" Description="Contracted yearly revenue." />
</arc:KpiDrilldown>
```

```csharp
var metric = new KpiMetric("refunds", "Refund rate", dailyValues)
{
    Kind = KpiKind.Rate,        // the change is a difference in points ("−0.2 pts")
    IsLowerBetter = true,       // a fall is green; the arrow still points down
    Format = KpiValueFormat.Percent,
};
metric.Breakdowns.Add(new KpiBreakdown("tier", [new KpiBreakdownRow("Team", teamValues)]));
```

演示中的所有数字都是用固定种子虚构的。

### 卡片显示什么

数值是系列的最后一天，不随范围变化。变化是将当前周期的最后一天与上一周期的最后一天相比：`Total` 指标为相对百分比，`Rate` 指标为百分点之差。它显示一个箭头、一个符号（真正的减号）和一种颜色；当四舍五入后为零时，显示“unchanged”和一个水平箭头。迷你趋势线是将当前周期重采样为 40 个点，以直线绘制，并按自身范围归一化。

### 详情视图

详情视图显示名称、数值、变化和“vs … in the prior N days”、指标的 `Description`、一张 220px 高的趋势图（72 个点；三条网格线分别标出最高、中间和最低值，以及三个日期）、一个图例和明细。指向图表时，十字线会吸附到最近的点，并显示一个读数卡，包含日期、数值，在开启 Compare 时还包含上一周期同一天的值。

<Demo name="Controlled" />

### 绑定状态与事件

`SelectedKey`、`RangeDays`、`IsCompareEnabled` 和 `SelectedDimensionKey` 是双向的。`Open(key)` 和 `Close()` 完成选择卡片和按 Escape 所做的事。`SelectedChanged`、`RangeChanged`、`CompareChanged` 和 `DimensionChanged` 报告用户做了什么，而 `SelectedCommand` 在详情视图打开时（参数：该指标）或关闭时（参数：null）运行。

<Demo name="FourMetrics" />

### 更少的指标，其他范围

`Ranges` 设置范围开关的天数；没有 `Dimensions` 时会省略明细，`IsCompareEnabled` 则让区块启动时隐藏上一周期。`MetricsSource` 会跟随你自己持有的列表。

<Demo name="Narrow" />

### 窄容器

区块读取的是自身宽度：从 760 起为三列，从 520 起为两列，小于 520 则为一列、22px 的数值、仅有图标的 Compare 按钮，以及不含占比列的明细。

### 文字

除数据之外的每一个词都来自 `Labels`（`KpiDrilldownLabels`）：Compare 文字、范围名称、图例、“vs”行、读数卡中的上一周期行、卡片的朗读名称和图表的摘要。

## API 参考

<!-- api: KpiDrilldown -->

<!-- api: KpiMetric -->

<!-- api: KpiBreakdown -->

<!-- api: KpiBreakdownRow -->

<!-- api: KpiDimension -->

<!-- api: KpiDrilldownLabels -->

<!-- api: KpiKind -->

<!-- api: KpiValueFormat -->

<!-- api: KpiSelectionChangedEventArgs -->

<!-- api: KpiRangeChangedEventArgs -->

<!-- api: KpiCompareChangedEventArgs -->

## 无障碍

### 键盘

- **Tab** 依次到达 Compare、范围开关（一个停靠点；方向键移动选择，Home 和 End 跳转）和每张卡片。
- 卡片上的 **Enter** 或 **Space** 打开它，并将焦点移到关闭按钮。
- 在详情视图中，Tab 依次到达关闭按钮、图表和维度开关；后面的卡片会被跳过。
- 在图表上，**←** 和 **→** 每次将十字线移动一天（第一次按键显示最后一天），**Home** 和 **End** 跳到第一天和最后一天，图表失去焦点时十字线消失。
- **Escape** 从页面上任何位置关闭详情视图，并把焦点返回到该卡片。

### 屏幕阅读器

- 每张卡片是一个按钮，其名称由标签、数值和用文字表达的变化组成（“Refund rate, 2.6%, down 0.2 pts against the prior 30 days. Show details”）。详情视图打开时，卡片会对辅助技术隐藏。
- 图表是一幅图像，名称为其起止的摘要；用键盘移动十字线时，会以礼貌方式播报日期和数值。
- 明细是一个带标签的区域，每个成员一个有名称的行；变化除颜色外还使用箭头或符号。
- Compare 缩小为图标时仍保留其名称。焦点环为 2px，位于元素外侧，仅在键盘操作时显示。

## 动效

- **打开**：卡片隐藏，面板在同一条弹簧上从自身的框体长到卡片区域（内缩 8），真实的宽度、高度和位置都在变化（视觉时长约 0.5 秒，无回弹）；内容保持最终布局并被裁剪。遮罩在 240 ms 内淡入，主体在 120 ms 延迟后用 480 ms 淡入，表面在 480 ms 内变暖。区域的高度随之变化。
- **关闭**：主体在 120 ms 内淡出，阴影在 480 ms 内淡出，面板在同一条弹簧上折回卡片，然后卡片重新出现；焦点返回到卡片。
- **范围、Compare、维度**：每条迷你趋势线和图表线在平滑弹簧上逐点变形，坐标轴标签显示插值后的数字，每个数字逐位滚动，上一周期的线在 240 ms 内淡入，文字交叉淡入（先用 80 ms 淡出，再用 240 ms 淡入），占比条则以弹簧运动。
- 减弱动效时，面板以最终尺寸显示并带 160 ms 的淡入；变形、占比条和数字直接跳转。关闭则立即应用所有变化。

## 相关

- `SegmentedControl`，用于其他区块中的开关。
- `Metric explorer` 和 `Revenue explorer`，其他更深入某个数字的 Analytics 区块。
- `Metrics dashboard`，用于没有下钻的一行 KPI。
