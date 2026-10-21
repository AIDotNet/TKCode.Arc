---
title: 折线图
summary: 多系列折线图，带有平滑滑动的十字准线、图例开关，以及在不同范围之间变形的路径。
source-hash: f501b3e6753f
---

<Demo name="Ranges" />

## 何时使用

- 随时间变化的一到三个指标，例如每天的注册数或收入。
- 用虚线系列将当前周期与上一周期或目标进行对比。
- 7 天、30 天、90 天等范围切换时，应当让同一组线条变形过渡，而不是重新绘制。

## 何时不用

- 与单个数值并排的紧凑趋势：用 `Sparkline`。
- 星期或地区等离散类别：用 `BarChart`。
- 部分与整体的关系：用 `DonutChart` 或 `WaffleChart`。

## 用法

每个 `ChartSeries` 都有稳定的 `Key`，因此数据变化时线条仍保持其身份。设置 `Label` 可为辅助技术命名图表。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:LineChart Label="注册数" CategoryLabel="日期" ItemsSource="{Binding Days}">
        <arc:LineChart.Series>
            <arc:ChartSeries Key="current" Name="本期" Values="{Binding Current}" />
            <arc:ChartSeries Key="previous" Name="上一期" Values="{Binding Previous}" IsDashed="True" />
        </arc:LineChart.Series>
    </arc:LineChart>
</UserControl>
```

### 范围切换

范围变化时替换 `ItemsSource` 与各系列的数值。每条线都会从屏幕上的当前形状变形到新形状，数值轴也会以弹簧效果过渡到新范围。

```csharp
void Range(int days)
{
    chart.ItemsSource = ChartsADemoData.Days(days);
    current.Values = ChartsADemoData.Daily(days, 420, 160, days, 2.4);
    previous.Values = ChartsADemoData.Daily(days, 380, 140, days + 11, 1.1);
}
```

### 单系列与单位

`ChartPoint` 项的列表无需额外的系列。`Unit` 会显示在提示中，`Curve` 可将默认的平滑曲线切换为直线段。

<Demo name="SingleSeries" />

```xml
<arc:LineChart Label="p95 延迟" Unit="ms" Curve="Linear" PlotHeight="180" ItemsSource="{Binding Latency}" />
```

### 图例与隐藏系列

存在多个系列时，图例会为每个系列显示一个开关。`ShowLegend` 可强制显示或隐藏图例。绑定 `HiddenSeries`，即可在视图模型中保存被隐藏的键。

```xml
<arc:LineChart Label="注册数"
               ItemsSource="{Binding Days}"
               ShowLegend="True"
               HiddenSeries="{Binding HiddenKeys}" />
```

### 加载与空状态

`IsLoading` 会使线条变暗，并在没有数据点时显示骨架屏。`EmptyLabel` 用于说明空的范围。

<Demo name="Empty" />

```xml
<arc:LineChart Label="退款" EmptyLabel="此范围内没有退款" IsLoading="{Binding IsBusy}" />
```

### 读取数值

十字准线移动时以及松开时，都会触发 `ActiveIndexChanged`。可用它在标题中显示数值。

```csharp
chart.ActiveIndexChanged += (_, e) => readout.Text = e.Index is null
    ? "悬停或使用方向键读取数值。"
    : $"{e.Label}: {string.Join(", ", e.Values.Select(v => $"{v.Name} {v.Value:N0}"))}";
```

## API 参考

<!-- api: LineChart -->

## 无障碍

- `Label` 为图表命名。绘图区会在隐藏的摘要中公开范围与最新数值，隐藏的数据表则列出每个数据点。
- 绘图区是可聚焦的滑块。方向键每次移动一个数据点，PageUp 与 PageDown 大约移动范围的六分之一，Home 与 End 分别移到第一个与最后一个数据点，Escape 释放十字准线。
- 图例条目是切换按钮。每个系列既由名称标识，也由线型标识，而不仅依靠颜色。
- 加载状态以“正在加载”礼貌地播报。线条与提示对辅助技术隐藏，以避免重复朗读。

## 动效

- 首次显示时，线条从左到右绘制，面积填充随线条一同显现。
- 新范围会在共享的 x 位置上重新采样线条，并从当前形状变形过渡。网格线随数值轴滑动。
- 十字准线在数据点之间滑动，提示由其自身的弹簧跟随，并在边缘处翻转。
- 加载中的线条会脉动，直到数据到达。图表离开视口时脉动暂停。
- 减弱动效时，跳过绘制、变形与滑动过程，并停止加载脉动。关闭动效时直接到达最终状态。

## 相关组件

- `Sparkline`：与数值并排的紧凑趋势。
- `BarChart`：在离散的日、周或月之间对比单个指标。
- `BrushChart`：需要缩放的高密度时间序列。
- `Streamgraph`：需要将多个系列作为堆叠层来阅读时。
