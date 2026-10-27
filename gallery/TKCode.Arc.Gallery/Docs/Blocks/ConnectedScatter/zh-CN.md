---
title: Connected scatter
summary: 两个一起变化的度量画成一条路径：可以拖动时间或回放的头部点沿着月份前进，读数卡随之计数，目标线为健康一侧着色，一个开关可以让整条路径从一个系列变形到另一个系列。
source-hash: adce3d65008a
---

<Demo name="Preview" />

## 何时使用

`ConnectedScatter` 展示两个度量如何随时间**一起**变化。每个月是 (x, y) 处的一个点，这些点按时间顺序用平滑曲线连接，每一段只要够长就带一个小箭头，头部点标出你当前查看的月份。折线图会画出两条独立的线，而这条路径展示的是两者的关系：这对数值朝哪个方向走、在哪里转向、在哪里越过阈值。

- **单位经济模型。** 获客成本对客户价值，并带有目标比值：当某个渠道从回本变为亏损时，路径会越过目标线。
- **随时间变化的权衡。** 价格与转化率、延迟与吞吐量、人数与产出：任何行进方向比单个数值更重要的数据对。
- **比较渠道。** 分段开关会把同样的月份从一个系列变形到下一个系列，让眼睛跟着变化走。
- **讲述一个故事。** 播放会让头部点以稳定的节奏走过各个月份，首次出现时会把整条路径画一遍。

## 何时不用

- **单一度量随时间变化。** 请使用折线图或面积图（`Metrics dashboard`、`Revenue explorer`）；当另一根轴是时间时，路径没有任何额外价值。
- **同时展示多个系列。** 一次只画一条路径。多个回环叠在一起无法阅读；请使用开关，或小多图布局。
- **无序的点。** 曲线暗示了顺序。对于没有先后次序的类别，请使用普通散点图。
- **数百个点。** 此区块为每个系列大约 8 到 36 个月而设计；每个点都有圆点和箭头，拖动条每个月有一个停靠点。

## 用法

此区块派生自 Avalonia 的 `TemplatedControl`。它的数据是一组 `ScatterSeries`；每个系列有 `Name`、可选的 `Key`，以及按时间顺序排列的 `ScatterPoint`（`Label`、`X`、`Y`）。每个系列都应有相同的月份数，这样开关才能把每个点移到与之对应的点。两根轴是 `ScatterAxis` 对象。

```xml
<arc:ConnectedScatter Title="Unit economics by channel" TargetRatio="3">
    <arc:ConnectedScatter.XAxis>
        <arc:ScatterAxis Name="CAC" Title="Customer acquisition cost" Minimum="300" Maximum="1900" Step="200" />
    </arc:ConnectedScatter.XAxis>
    <arc:ConnectedScatter.YAxis>
        <arc:ScatterAxis Name="LTV" Title="12 month customer value" Minimum="1000" Maximum="6000" Step="1000" />
    </arc:ConnectedScatter.YAxis>
    <arc:ScatterSeries Name="Field sales" Key="field-sales">
        <arc:ScatterPoint Label="Jul 2024" X="1150" Y="3600" />
        <arc:ScatterPoint Label="Aug 2024" X="1175" Y="3630" />
    </arc:ScatterSeries>
</arc:ConnectedScatter>
```

```csharp
scatter.Series.Add(new ScatterSeries("Community", points, "community"));
scatter.SelectedSeriesKey = "community";   // the points morph to the new series
```

`SeriesSource` 可以改为接收你自己持有的集合（会跟随 `ObservableCollection` 的变化）。只有一个系列时，开关会隐藏。

### 结构

- **标题区**：`Title`、说明（`Description`，或根据坐标轴以及第一个和最后一个月份写出的一句话）和系列开关。
- **读数卡**：四格随头部点计数的内容：月份及其位置（"18 of 18"）、两个数值及其相对第一个月的变化（"+$200 since Jan 2025"，或 "Same as Jan 2025"），以及比值，带有状态圆点和 "Above 3x target" 或 "Below 3x target"。状态始终用文字写出，绝不只靠颜色。
- **图表**：纵轴标题和图例（路径、目标线、健康区），绘图区，以及右侧位于其下方的横轴标题。
- **控制条**：一个 Play、Pause 或 Replay 按钮，以及拖动条，其下方是季度标签。

### 坐标轴与目标线

`ScatterAxis` 的范围和步长会严格按你写的使用：不取整到好看的数字，也不强制从零开始，所以放大的坐标轴会从你指定的位置开始。不设置 `Minimum`、`Maximum` 和 `Step`（NaN）时，会根据所有系列的数据推算，因此切换系列时坐标轴保持不动。步长是 1,000 的倍数的刻度读作 "$2k"；其他刻度使用区块的 `ValueFormatter`；`TickFormatter` 会同时取代两者。

`TargetRatio` 会画出经过原点的虚线 y = k · x，为它标注（"3x CAC"），给它上方一侧着色，并在其角落写下 "Healthy, above 3x"。比值读数和卡片颜色对未取整的数值采用同样的判断。设为 NaN 则移除这条线、健康区、状态及其图例项。

<Demo name="Target" />

### 头部点、拖动条与回放

头部点位于真实的曲线上，而不是月份之间的直线上。**按下**拖动条会让头部点以 Smooth 弹簧滑到指针处；追上之后，拖动时它与指针 1:1 跟随，**松开**则会吸附到最近的月份。按键、点击某个点以及回放结束，同样会让头部点停在某个月份上。`MonthChanged` 恰好在此时触发，绝不会逐帧触发，带有索引、系列和点；`MonthChangedCommand` 以相同的参数执行。`SelectedIndex` 是双向的（−1 表示最后一个月），`Position` 是连续位置，包含头部点移动时的小数部分。

**Play** 以恒定的节奏，从当前位置开始，每 `PlaybackMonthDuration`（240 ms）前进一个月；如果已在最后一个月，则从第一个月开始。**Pause** 会让它停在两个月之间；读数卡写出最接近的月份，数值则是插值得到的。到了最后一个月，按钮变成 **Replay**。

<Demo name="Events" />

### 首次出现

区块第一次有四分之一进入屏幕时，头部点会在 `DrawInDuration`（1.35 s，先慢后快再慢）内从第一个月跑到最后一个月，在身后画出轨迹；落定时，虚线的未来路径、空心圆点、未来的箭头和标签会在 240 ms 内一起淡入。设置 `PlayOnFirstAppear="False"` 可让它静止在最后一个月，或调用 `ReplayDrawIn()` 再次展示。减弱动效和关闭动效会跳过这段。

<Demo name="Minimal" />

### 悬停、点击与卡片

指针靠近某个月份 30 px 以内时，会出现光晕和一张卡片，显示该月份、两个数值及其比值。卡片不会移动头部点；在点与点之间会滑行。点击或单击某个点会让头部点以弹簧移到那里；在触屏上，手指按下时会显示卡片。

### 标签、注释与窄容器

第一个和最后一个月份标注在路径的两端外侧，最后一个始终使用 Medium 字重。`ScatterNote` 为某个月份添加事件标签（"Pricing change"），位于你选择的一侧。区块会读取自己的宽度：低于 720 时，读数卡排成 2 × 2 网格，绘图区高 330；低于 560 时，季度标签缩减为第一个月、各个一月和最后一个月；低于 480 时，绘图区高 300，注释、目标标签和健康区标签消失，横轴每隔一个刻度跳过一个，拖动条移到按钮下方；低于 380 时，数值为 18px。

<Demo name="Narrow" />

### 格式与文字

`ValueFormatter` 和 `RatioFormatter` 取代读数卡、卡片、表格和播报中的 "$1,234" 与 "4.8x"。每一个可见的词都是带英文默认值的属性：读数名称、"since" 和 "of" 的注释、状态词、目标和健康区文字、图例、按钮的文字和名称、屏幕阅读器表格以及播报格式。读作 "Jan 2025" 的月份名称会在拖动条下方得到季度标签；其他的则得到大约六个等距的标签。

<Demo name="Weekly" />

## API 参考

<!-- api: ConnectedScatter -->

<!-- api: ScatterSeries -->

<!-- api: ScatterPoint -->

<!-- api: ScatterNote -->

<!-- api: ScatterAxis -->

<!-- api: ScatterLabelSide -->

<!-- api: ScatterMonthChangedEventArgs -->

<!-- api: ScatterSeriesChangedEventArgs -->

## 无障碍

- 拖动条是名为 "Month" 的滑块，范围从 1 到月份数，并带有已落定月份的值文字："Dec 2025: CAC $960, LTV $4,640, 4.8x, above the 3x target."。它在头部点落定时更新，而不是在移动或回放过程中更新。
- 一个礼貌型实时区域会为每个落定的月份朗读同样的文字，系列变化时朗读 "Series name. …"；回放在停止处播报。
- 图表对屏幕阅读器隐藏了细节，卡片也是如此；一张表格列出每个月份、两个数值和比值，标题写明系列和坐标轴。
- 每个控件都有名称；播放按钮的名称随状态变化（"Play month by month"、"Pause"、"Replay from the first month"）。目标和状态都用文字写出。
- 颜色来自主题，浅色和深色下均适用；圆点、箭头和标签在着色的健康区上依然保持对比度。

### 键盘

- **Tab** 依次移到系列开关（一个停靠点，即所选系列）、播放按钮和拖动条。图表不接收焦点。
- 在拖动条上，**← ↓** 后退一个月，**→ ↑** 前进一个月；**PageUp** 前进三个月，**PageDown** 后退三个月；**Home** 和 **End** 跳到第一个和最后一个月。按键在两端停住。从两个月之间的位置出发，会前往所按方向上的下一个整月。每次按键都以 Smooth 弹簧移动头部点，并落定在某个月份上。
- 在系列开关中，**← →** 立即改变选择并循环；**↑ ↓** 前往上一个和下一个系列；**Home** 和 **End** 前往两端。
- 在按钮上按 **Enter** 或 **Space** 会播放、暂停或重播。
- 焦点环只在键盘焦点时绘制；在拖动条上用指针按下不会显示它。

## 动效

- **首次运行**：头部点用先慢后快再慢的曲线跑 1.35 s，读数卡和拖动条随之而动；落定时，未来的各图层一起用 240 ms 淡入。
- **头部点**：按键、点击、松开和切换系列都使用 Smooth 弹簧（无过冲，约 0.65 s）；按下拖动条时它追赶指针，随后 1:1 跟随。回放或拖动时，头部点的光晕用 580 ms 的弹簧曲线增长到 1.25 倍。
- **回放**：恒定速度，每月 240 ms；暂停会保留小数位置。
- **切换系列**：每个点、每个箭头乃至曲线本身，同时以 Smooth 弹簧弹向新系列；读数卡计数；旧标签用 180 ms 淡出，新标签在其后用 240 ms 淡入。
- **悬停**：光晕用 160 ms 淡入；卡片原地淡入，并在点与点之间以 580 ms 的弹簧曲线滑行。
- **拖动条滑块**：悬停时 1.08 倍，按下时 1.12 倍；键盘焦点时其中心圆点增长到 1.5 倍。
- 减弱动效会跳过首次运行和滑行：回放每 960 ms 跳一个月，按键和点击直接跳转，切换系列会立即替换各点，只保留短暂的淡入淡出。关闭动效则让一切立即生效。

## 相关

- `Metrics dashboard`、`KPI drilldown` 和 `Revenue explorer`，其他 Analytics 区块。
- `SegmentedControl`，此区块使用的开关。
- `Line replay`，另一个让系列随时间回放的区块。
