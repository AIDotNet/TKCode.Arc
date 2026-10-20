---
title: 刷选图表
summary: 带概览条的密集时间序列：拖动窗口即可缩放，通过手柄调整大小，并在原处查看事件。
source-hash: 369c2da5d313
---

## 何时使用

- 覆盖一年或更长时间的每日序列，读者需要放大查看某个时段。
- 事件复盘与其他带标注事件的时间线，需要在原处读取事件。
- 数千个数据点的数据集，概览条可让完整范围始终可见。

## 何时不用

- 几周的数据或需要对比多个序列：使用 `LineChart`。
- 无需交互的小型趋势：使用 `Sparkline`。
- 包含负值或需要对数刻度：数值轴始终从零开始，因此不适用。

## 用法

`ItemsSource` 保存按日期升序排列的数据点。日期取自 `ChartPoint` 的 X 值，数值轴则为数据点的值。绘图区下方的概览条显示完整范围，窗口决定绘图区显示的内容。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:BrushChart Label="每日活跃用户"
                    Unit="用户"
                    PlotHeight="240"
                    OverviewHeight="52"
                    ItemsSource="{Binding Points}" />
</UserControl>
```

### 标注

`Annotations` 在绘图区与概览条上标记事件。每个标注包含日期、标签以及可选的描述，描述会显示在提示中。

```csharp
chart.Annotations =
[
    new ChartAnnotation(start.AddDays(120), "v2 发布", "新的引导流程已面向所有用户开放。"),
    new ChartAnnotation(start.AddDays(410), "媒体提及", "被每周通讯刊登。"),
    new ChartAnnotation(start.AddDays(530), "定价调整")
];
```

### 预设与窗口

`Range` 为可见窗口，并且是双向绑定的。为其赋新值即可移动窗口，窗口会以弹簧动画滑动到该位置。使用 `RangeChanged` 可以以文字显示窗口范围。

```csharp
chart.Range = new ChartTimeRange(last.AddDays(-30), last);
chart.RangeChanged += (_, e) => range.Text = $"窗口：{e.Range.Start:MMM d, yyyy} – {e.Range.End:MMM d, yyyy}";
```

### 最小跨度

`MinSpan` 限制窗口不会缩小到该跨度以下。默认值为七天。

```xml
<arc:BrushChart Label="请求数" MinSpan="14.00:00:00" ItemsSource="{Binding Points}" />
```

### 格式化

`DateFormatter` 格式化提示与窗口文字中的日期，`TickFormatter` 格式化数值轴。

```xml
<arc:BrushChart Label="营收"
                Unit="USD"
                TickFormatter="{Binding FormatMoney}"
                ItemsSource="{Binding Points}" />
```

## API 参考

<!-- api: BrushChart -->

## 无障碍

- `Label` 为图表的名称。窗口及其两个手柄为滑块，其数值文字即为日期。
- 绘图区是可获得焦点的分组。其读数会播报该日的日期、数值以及当天的任何事件。
- 在绘图区中，左右方向键将十字线移动一个数据点。在窗口中，左右方向键按窗口宽度的十分之一平移，Shift 或 PageUp 与 PageDown 平移二分之一，加号与减号键则以中心为基准缩放。
- 在手柄上，左右方向键移动一个数据点，按住 Shift 则移动十个。Home 与 End 分别跳到第一个与最后一个数据点，按 Escape 或 0 恢复为完整范围。
- 一张隐藏的数据表列出窗口内的每个数据点及其事件。事件以第二种数据系列颜色标记，并附带文字标签或提示，因此不会仅靠颜色来区分。

## 动效

- 首次显示时，折线从左侧一次性绘制出来，使用较长的无回弹弹簧。
- 拖动时窗口与指针一一对应。预设、重置以及 `Range` 的变化则使窗口以弹簧动画滑动。
- 数值轴以弹簧动画调整到可见范围内的最高点。随着密度变化，原始折线与平滑趋势线交叉淡化。
- 十字线在数据点之间平滑滑动。
- 减弱动效时窗口立即到位，十字线保持不动。关闭动效时直接到达最终状态。

## 相关组件

- `LineChart`：几周的数据或多个序列。
- `Sparkline`：无交互的小型趋势。
- `Streamgraph`：多个图层随时间的构成。
- `AnimatedCounter`：变化中的单一总数。
