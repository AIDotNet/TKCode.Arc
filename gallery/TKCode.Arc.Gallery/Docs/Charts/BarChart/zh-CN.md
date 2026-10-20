---
title: 柱状图
summary: 跨日、周或月比较同一指标，并可逐根查看任一柱子的数值。
source-hash: 7232de4d9e70
---

## 何时使用

- 同一指标在日、周或月之间的比较，例如每天的活跃分钟数。
- 需要以虚线显示平均值作为参考的数值范围。
- 离散的类别，例如每周的部署次数，每根柱子各自独立。

## 何时不用

- 包含多个数据点或多个数据系列的连续趋势：使用 `LineChart`。
- 与数值并列的紧凑趋势：使用 `Sparkline`。
- 以网格呈现一整年每日活动：使用 `ActivityHeatmap`。

## 用法

标题区在静止时显示平均值；指针或键盘停在某根柱子上时，显示该柱子的数值。`Period` 设置标题下方显示的时间范围。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:BarChart Label="活跃分钟数"
                  Period="2026 年 9 月 24–30 日"
                  Unit="分钟"
                  CategoryLabel="日"
                  ItemsSource="{Binding Week}" />
</UserControl>
```

### 时间范围

时间范围变化时，替换 `ItemsSource` 与 `Period`。键相同的柱子会变形到新的高度与位置，新增的柱子则在旁边生长出来。

```csharp
void Week()
{
    var days = ChartsADemoData.Days(7);
    var values = ChartsADemoData.Daily(7, 46, 30, 3);
    chart.Period = "2026 年 9 月 24–30 日";
    chart.ItemsSource = days.Select((day, i) => new ChartPoint(day, values[i])
    {
        Label = day.ToString("dddd, MMM d", System.Globalization.CultureInfo.GetCultureInfo("en-US")),
        AxisLabel = day.ToString("ddd", System.Globalization.CultureInfo.GetCultureInfo("en-US"))
    }).ToArray();
}
```

`ChartPoint.Label` 是提供给辅助技术朗读的完整名称。`ChartPoint.AxisLabel` 是柱子下方的简短文字。

### 标题标签

`AverageLabel` 为静止时标题区的名称，`ValueLabel` 为逐根查看时柱子数值的名称。若参考线没有用处，可使用 `ShowAverage` 隐藏平均线。

```xml
<arc:BarChart Label="部署次数"
              Period="最近 12 周"
              AverageLabel="每周平均"
              ValueLabel="部署次数"
              CategoryLabel="周"
              ShowAverage="False"
              PlotHeight="140"
              ItemsSource="{Binding Weeks}" />
```

### 坐标轴

`ShowGrid`、`ShowXAxis` 与 `ShowYAxis` 控制参考线。`YMaximum` 固定数值轴的上限，`TickFormatter` 格式化数值轴的刻度。

```xml
<arc:BarChart Label="工单" YMaximum="100" ShowGrid="False" ItemsSource="{Binding Tickets}" />
```

### 读取数值

`ActiveIndexChanged` 会报告指针或键盘所在的柱子。`Average` 给出所有数值的平均值。

```csharp
chart.ActiveIndexChanged += (_, e) => status.Text = e.Index is null
    ? $"平均 {chart.Average:N0}"
    : e.Label;
```

## API 参考

<!-- api: BarChart -->

## 无障碍

- `Label` 为图表的名称。摘要会说明平均值、最高与最低的柱子。
- 方向键在柱子之间移动，PageUp 与 PageDown 每次移动至多七根柱子，Home 与 End 分别跳到第一根与最后一根柱子，按 Escape 停止逐根查看并返回平均值。
- 一张隐藏的数据表列出每个数值，因此即使看不到柱子也能读取图表。
- 时间范围变化时会以礼貌方式播报。
- 柱子对辅助技术隐藏。逐根查看的位置由光标指示，而不是焦点框。

## 动效

- 首次显示时，柱子从最新的一根开始向后依次生长。
- 时间范围变化时，保留的柱子、坐标轴刻度与平均线会变形到新的数值。
- 查看光标在柱子之间平滑滑动，标题数值滚动到新的数字。
- 标题标签在平均值与数值标签之间切换，并伴随短暂的模糊。
- 减弱动效时柱子直接显示为最终高度，数字原地替换。关闭动效时直接到达最终状态。

## 相关组件

- `LineChart`：多数据点的连续趋势。
- `Sparkline`：表格单元格或卡片中的紧凑趋势。
- `ActivityHeatmap`：一整年的每日活动。
- `DonutChart`：柱子需要表示整体的各个部分时使用。
