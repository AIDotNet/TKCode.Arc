---
title: 斜率图
summary: 两个坐标轴上的前后对比：线条逐条绘出，每个数值旁显示排名变化，切换数据集时每条线都会滑动到新的斜率。
source-hash: 469da0f5d974
---

## 何时使用

- 对两个时间点的前后对比，例如第一季度与第二季度各渠道的转化率。
- 展示哪些项上升或下降，以及两个时间点之间排序如何变化。
- 三到十项。超过约十项时，标签会互相挤压。

## 何时不用

- 三个或更多时间点：用 `LineChart`。
- 只需每项的最新数值：用 `BarChart`。
- 整体中的各部分，而不是两个时间点之间的变化：用 `DonutChart`。

## 用法

每个 `SlopeItem` 是一条线，包含 `Key`、`Label`、`Start` 数值与 `End` 数值。`StartLabel` 与 `EndLabel` 用于命名两个时间点。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SlopeChart Label="各渠道转化率"
                    StartLabel="Q1"
                    EndLabel="Q2"
                    HighlightKey="search"
                    ItemsSource="{Binding Channels}" />
</UserControl>
```

### 数据集

替换 `ItemsSource` 即可切换数据集。每条线、每个点与每个标签都会滑动到新的斜率。在同一步骤中更改 `Label`，使文字与数据保持一致。

```csharp
conversion.Click += (_, _) =>
{
    chart.Label = "各渠道转化率";
    chart.ItemsSource = Conversion;
};
```

### 数值与变化的格式

`ValueFormatter` 格式化两端的数值。`ChangeFormatter` 格式化排名旁的变化量，它接收变化量与对应的项。

```csharp
chart.ValueFormatter = v => $"{v:0.0}%";
chart.ChangeFormatter = (change, _) => $"{(change >= 0 ? "+" : "−")}{Math.Abs(change):0.0} 个百分点";
```

### 高亮与排名

`HighlightKey` 以第一个系列的颜色绘制一项，其余项保持中性色。`IsRankVisible` 用于显示或隐藏排名变化。

```xml
<arc:SlopeChart Label="收盘价"
                StartLabel="2023"
                EndLabel="2024"
                IsRankVisible="False"
                MaxWidth="420"
                ItemsSource="{Binding Values}" />
```

### 读取项目

`ActiveKey` 为指针或键盘所在的项，且为双向绑定。每次变化都会触发 `ActiveKeyChanged`。

```csharp
chart.ActiveKeyChanged += (_, e) => reading.Text = e.Item is { } item
    ? $"{item.Label}: {item.Start} → {item.End}"
    : "没有聚焦的项";
```

## API 参考

<!-- api: SlopeChart -->

<!-- api: SlopeItem -->

## 无障碍

- `Label` 为图表命名。图表是带有角色描述和按键说明的可聚焦分组。
- Tab 键将焦点移到图表上，并优先读取结束数值最高的项。下、右方向键移到下一项，上、左方向键移到上一项，顺序依据结束数值。
- Home 与 End 分别移到第一项与最后一项，Escape 清除读数。触摸按下并拖动会读取最近的线条。
- 礼貌的实时区域会播报两个数值、变化量与排名变化。隐藏的数据表按结束数值的顺序列出各项。
- 排名变化使用带数字的箭头符号，因此不只依靠颜色表示。线条与标签对辅助技术隐藏。

## 动效

- 首次显示时，每条线从起点绘制到终点，并以短暂的错落时差进行。圆点与标签随线条落定而出现。
- 切换数据集时，线条、圆点、标签与引导线在共享的变形弹簧上移动。排名数字会朝变化的方向滚动。
- 获得焦点的项移到最前，其余项淡出。提示滑动到其所在线条的中点。
- 减弱动效时，所有线条一次性绘制完成，变化时也一次性移动。关闭动效时直接到达最终状态。

## 相关组件

- `LineChart`：三个或更多时间点。
- `BarChart`：单一时间点上每项的单个数值。
- `Ridgeline`：各组分布随时间的变化。
