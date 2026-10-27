---
title: Small multiples
summary: 一组小折线图，每个分组一张，合在一起当作一幅图来读：一条十字线穿过所有面板，纵轴可在各分组自己的范围与统一范围之间切换，面板可按数值、增长或名称重新排序，变化平平的分组可以淡化。
source-hash: 1c23390a1f38
---

<Demo name="Preview" />

## 何时使用

`SmallMultiples` 回答的问题是："这些分组在同一段时间里如何比较？"每个序列都有自己的小面板，横轴相同、编码相同，读者一眼就能看完整个网格，不必在一张图里追踪八条相互交叉的线。

- **一个指标，多个分组。** 各市场的收入、各套餐的注册数、各客服渠道的工单：同一个数字，在相同的月份或周里为多个分组记录。
- **找出变化大的分组。** 每个面板都标出自较早期间以来变化了多少，网格可以按这个排序，几乎没变的分组可以淡化到背景里。
- **在所有面板里读同一期间。** 指向任意面板，十字线就会穿过所有面板移动，网格上方的读数显示该期间和总量。

## 何时不用

- **一张图上的少量序列。** 当两三条线最好直接对比时，`LineChart` 会把它们画在一起，带一条十字线和图例开关。
- **需要深入探索的单一指标。** 请使用带缩放或下钻的单张图表；小倍数图以细节换取全局。
- **表格里的趋势。** 改为在行里放一个 `Sparkline`。
- **精确的数字。** 如果读者更需要数字而不是形状，表格更合适。

## 用法

本区块派生自 Avalonia 的 `TemplatedControl`。给它一组期间（从早到晚），并为每个面板提供一个 `SmallMultiplesSeries`，每个期间一个值。四到十二个面板效果最好：容器宽度达到 640px 时网格为四列，更窄时为两列。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SmallMultiples Title="Revenue by market"
                        Description="Monthly net revenue"
                        SeriesLegendText="Market"
                        GhostLegendText="All markets, scaled to fit" />
</UserControl>
```

```csharp
chart.Periods.AddRange(months.Select(m => new SmallMultiplesPeriod(m.ToString("MMM yyyy"), m.ToString("MMMM yyyy"))));
chart.Series.Add(new SmallMultiplesSeries("us", "United States", usRevenue));
chart.Series.Add(new SmallMultiplesSeries("ca", "Canada", caRevenue));
```

也可以用 `SeriesSource` 和 `PeriodsSource` 传入你自己持有的集合（`ObservableCollection` 会被跟踪）。序列的 `Key` 能在列表重建时保留它的面板以及正在进行的动画。

### 结构

- **头部**：`Title`、`Description` 以及三个控件：排序（Latest、Growth、Name）、刻度（Independent、Shared）和 "Dim flat trends" 开关。宽度小于 640 时，它们换行到标题下方。
- **读数**：十字线所在的期间（或最新期间）、它的位置（"11 of 24"）或 "Latest"、所有序列的合计，以及图例：实线代表一个序列，虚线代表总量。
- **面板**：名称、十字线处的值、带箭头的增长以及与之比较的期间（"vs"），右侧槽位中的纵轴刻度，总量的虚线虚影，面积、折线及其末端圆点。

### 增长与平坦趋势

增长比较的是最新值与 `GrowthLag` 个期间之前的值（默认 12，即一年前的同月；期间不够时与第一期比较）。增长的绝对值小于 `FlatThreshold`（0.05 即 5%）时视为平坦：面板显示 "Flat" 及其百分比，并且在 `IsFlatDimmed` 打开时，面板内容淡化到 38%，而网格线保持不变。

### 刻度

`Scale` 决定面板之间如何比较。**Independent** 让每个序列适配自己的范围，因此比较的是形状；**Shared** 让每个面板都从零开始、使用最大的范围，因此比较的是大小。切换时，坐标轴和折线一起以弹簧过渡。虚影始终使用自己的范围：它展示的是整体形状，而不是可供比较的大小。

<Demo name="SharedScale" />

### 自定义单位

默认把数值读作千美元（$412k、$1.71M）。为面板标题、合计和屏幕阅读器传入 `ValueFormatter`，为紧凑的纵轴刻度传入 `TickFormatter`。这个示例展示每周计数，增长与四周前比较，宽度为 560。

<Demo name="Signups" />

### 跟随十字线

`CrosshairIndex` 是十字线所在的期间，隐藏时为 null；`CrosshairIndexChanged` 报告每次移动。`SetCrosshairIndex` 从代码中移动十字线。`SortChanged` 和 `ScaleChanged` 报告控件的变化；`Sort`、`Scale` 和 `IsFlatDimmed` 都是双向绑定。

<Demo name="Crosshair" />

### 其他文字

每段文字都是一个带英文默认值的属性：选项（`LatestSortText`、`GrowthSortText`、`NameSortText`、`IndependentScaleText`、`SharedScaleText`、`DimFlatText`）、读数（`LatestText`、`PositionFormat`、`TotalText`）、图例（`SeriesLegendText`、`GhostLegendText`）、趋势（`FlatText`、`CompareFormat`、`UpFormat`、`DownFormat`）、屏幕阅读器名称（`SortLabel`、`ScaleLabel`、`PanelsLabel`、`KeyboardHint`）以及各类播报。

## API 参考

<!-- api: SmallMultiples -->

<!-- api: SmallMultiplesSeries -->

<!-- api: SmallMultiplesPeriod -->

<!-- api: SmallMultiplesSort -->

<!-- api: SmallMultiplesScale -->

## 无障碍

- **Tab** 依次经过排序控件、刻度控件、淡化开关，然后是面板；所有面板是一个 Tab 停靠点。
- 在面板上，**Left** 和 **Right** 让十字线在所有面板中移动一个期间；**Page up** 和 **Page down** 移动十二个；**Home** 和 **End** 跳到第一个或最新的期间；**Escape** 清除十字线。用键盘聚焦面板时，十字线落在最新期间。
- 在排序和刻度控件中，方向键选择上一个或下一个选项。
- 键盘移动会以礼貌方式读出：期间、合计以及领先的分组。更改排序、刻度或淡化也会被读出。
- 每个面板的名称包含序列名、十字线处的值以及用文字描述的趋势（"Up 15.3% vs Oct 2025"、"Flat"）。
- 淡化从来不是唯一的信号：每个面板还会显示箭头和带符号的百分比（使用真正的减号），或者 "Flat" 一词。键盘聚焦时，面板四周绘制共享的焦点环。

## 动效

- **切换刻度**：每个面板纵轴范围的两端都沿平滑弹簧移动（约 400 毫秒，无回弹）。绘图区里的一切每一帧都按这个范围重画，所以刻度标签随数值一起移动，折线随之拉伸或压平；新刻度在 240 毫秒内淡入，旧刻度同时淡出。
- **排序**：每个面板从原来的位置出发，以弹簧落入新的格子，并带着自己的分隔线一起移动。
- **十字线**：第一个位置立即显示；之后的移动以短弹簧滑动，每个面板的圆点沿着自己的折线在点与点之间移动。
- **淡化**：平坦面板的内容在 240 毫秒内淡化，网格线保持不变。
- 减少动效时，重新缩放、重新排序和十字线移动都会立即完成。关闭动效时，所有变化立即生效。

## 相关组件

- `LineChart`：在一张图上绘制少量序列，带滑动的十字线。
- `Sparkline`：在数值旁显示紧凑的趋势。
- `Line replay`：Line charts 中的另一个区块，用于按时间回放单个序列。
