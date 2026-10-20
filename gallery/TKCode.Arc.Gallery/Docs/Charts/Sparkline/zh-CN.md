---
title: 迷你趋势图
summary: 在数值旁展示紧凑的趋势。
source-hash: 293d5d8d0378
---

## 何时使用

- 卡片、表格行或 KPI 区块中，位于主要数值旁的小型趋势。
- 比起精确数据点，更关注方向与形状的趋势。
- 读者可以拖动查看较早数据点、但不需要完整坐标轴的数值。

## 何时不用

- 需要坐标轴进行比较的离散周期：用 `BarChart`。
- 只有最新数值及其变化、没有趋势的情况：用 `MetricCard`。
- 需要坐标轴、多个系列或图例的趋势：用 `LineChart`。

## 用法

`ItemsSource` 按从旧到新的顺序保存数值。`Value` 与 `Change` 构成静止时显示的主数值与附注。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Sparkline Label="收入"
                   Value="$48.2K"
                   Change="+4.1%"
                   Tone="Success"
                   Width="220"
                   ItemsSource="{Binding Revenue}" />
</UserControl>
```

### 色调

`Tone` 决定线条与变化量的颜色。使用 `Success` 或 `Danger` 表示指标的走向，使用 `Warning` 表示需要关注的数值。

```xml
<arc:Sparkline Label="流失率" Value="2.4%" Change="+0.3 pts" Tone="Danger" ItemsSource="{Binding Churn}" />
<arc:Sparkline Label="队列深度" Value="1,204" Change="上升" Tone="Warning" ItemsSource="{Binding Queue}" />
```

### 数据点标签

`PointLabels` 为每个数据点提供一个标签。读者拖动线条时，将读取并显示该数据点的标签，而不是位置。

```csharp
var labels = days.Select(d => d.ToString("MMM d", CultureInfo.GetCultureInfo("en-US"))).ToArray();
revenue.PointLabels = labels;
```

### 没有主数值

未设置 `Value` 时，拖动所得的数值及其数据点会显示在标签旁。新数据会使线条变形过渡。

```xml
<arc:Sparkline Label="访问量" Width="300" ItemsSource="{Binding Visits}" />
```

### 高密度表格

在行数很多的表格中设置 `IsInteractive="False"`。此时迷你趋势图没有 Tab 停留点，也不处理指针事件。设置 `ShowArea="False"` 可让线条保持简洁。

```xml
<arc:Sparkline Label="api-gateway"
               Width="160"
               DrawingHeight="32"
               ShowArea="False"
               IsInteractive="False"
               ItemsSource="{Binding Gateway}" />
```

## API 参考

<!-- api: Sparkline -->

## 无障碍

- `Label` 为图表命名。绘图本身对辅助技术隐藏。
- 图表可交互时，绘图区是可聚焦的滑块。其数值文字会读出数据点标签与数值。
- 左、右方向键以及上、下方向键每次移动一个数据点。PageUp 与 PageDown 大约移动范围的六分之一，Home 与 End 分别移到第一个与最后一个数据点，Escape 停止拖动并回到最新数值。
- 设置 `PointLabels`，使拖动到的数据点以日期读出，而不是以它在列表中的位置读出。
- 主数值与变化量是纯文字，因此按原样读出。

## 动效

- 首次显示时，线条绘制一次，终点圆点随线条落定而出现。
- 拖动时，光标以灵敏的弹簧在数据点之间移动，光标之后的数据点变暗。
- 主数值与附注滚动为新的文字，主数值的宽度缓动以适应新文字。
- 减弱动效时整条线立即显示，光标移动时不使用弹簧。关闭动效时直接到达最终状态。

## 相关组件

- `BarChart`：需要坐标轴的离散周期。
- `LineChart`：带有坐标轴、多个系列或图例的趋势。
- `MetricCard`：单个数值及其变化，没有趋势。
- `BrushChart`：需要缩放的高密度系列。
