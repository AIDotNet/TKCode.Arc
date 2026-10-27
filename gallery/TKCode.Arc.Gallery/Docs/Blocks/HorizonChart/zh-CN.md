---
title: Horizon chart
summary: 按每个系列偏离自身基线的程度来比较许多日序列，每个系列折叠成一条细色带行：色带数量用同一根弹簧折叠和展开所有行，各行可按名称、峰值或最新值重新排序，一条十字线用指针、触摸和键盘在所有行上读出同一天。
source-hash: f61e270a0109
---

<Demo name="Preview" />

## 何时使用

`HorizonChart` 在一个屏幕上回答 "这么多序列里哪个偏离了、什么时候、偏了多少"。每一行都是一个日序列，以它自己的基线（预测、目标、平均值）为参照；高于基线的距离用一种颜色填充，低于基线的距离用另一种颜色填充，距离被折叠成多条色带，叠放在一条细长的带上，所以一行只占 36px，而不是一张完整的图表。十四行、119 天大约占 550px。

- **一批相似的对象。** 门店、枢纽、地区、服务或营销活动对照各自的计划：眼睛会找到带有深色色带的行，以及它发生的那一天。
- **发现共同事件。** 十字线穿过所有行，所以一次故障、一场促销或一个假日如果影响了多行，就会呈现为一条竖线，并在每行末尾写出同一天的值。
- **高密度仪表盘。** 每行一张折线图会太高，而一张数字表格又太平淡。

## 何时不用

- **只有几个系列。** 只有三四行时，折线图展示的内容相同，而且有可以读数的刻度。
- **绝对水平。** 各行是相对基线的偏差；基线本身的水平并没有画出来。请用折线图或面积图展示水平。
- **从形状上读取精确数值。** 色带用来发现问题；数字来自十字线和数值列。
- **数百天以上或超过约 40 行。** 展示更多之前请先汇总（用周代替天）：此区块不会代你汇总。

## 用法

此区块派生自 Avalonia 的 `TemplatedControl`。它的数据是一组 `HorizonSeries`（一个名称，加上每天一个带符号的值：正数高于基线，负数低于基线）以及与之对齐的 `Days`，最早的在前。`HorizonDay` 带有 `Label`（"Sep 27"）、`LongLabel`（"Sunday, September 27"）和可选的 `Date`；只设置日期时，标签会根据它格式化。

```xml
<arc:HorizonChart Title="Orders against forecast"
                  Description="Daily orders at each hub compared with its winter forecast"
                  BaselineLabel="winter forecast"
                  Days="{Binding Days}">
    <arc:HorizonSeries Name="Northgate" Values="{Binding NorthgateValues}" />
    <arc:HorizonSeries Name="Harbor Point" Values="{Binding HarborPointValues}" />
</arc:HorizonChart>
```

```csharp
chart.Days = Enumerable.Range(0, 119).Select(i => new HorizonDay(start.AddDays(i))).ToArray();
chart.Series.Add(new HorizonSeries("Northgate", values, key: "northgate"));
chart.Bands = 2;
chart.Sort = HorizonSort.Latest;
```

`SeriesSource` 可以改为接收你自己持有的集合（会跟随 `ObservableCollection` 的变化）。对于带有日期的天，坐标轴会在第一天和每月一号显示刻度；设置 `Ticks`（索引和标签）可以自行放置。值为 NaN 时画成零，读作短横线。

### 色带如何工作

每一行是一个多边形，色带有多少条就画多少次。`Extent` 是填满所有色带的距离：默认是最大绝对值向上取整到 12 的倍数（最大波动为 35 时取 36）。有三条色带时，每条色带跨越范围的三分之一，所以 12 填满第一条，24 填满第二条，36 填满第三条；每一层以 alpha `1 − 0.08^(1/bands)` 绘制，所有色带都填满时叠加为 0.92。因此颜色越深表示离基线越远，而一条色带就是普通的面积图。

折叠过程中，色带数量是一个连续的值：每一行的四个图层和 alpha 共同跟随一根弹簧（一根约 600ms 内稳定的临界阻尼弹簧），所以在折叠中途改变数量，会从当前状态继续。

<Demo name="Palette" />

### 排序

`Sort` 为 `Name`（A 到 Z）、`Peak`（该行最大绝对值大的在前，默认值）或 `Latest`（最后一天的值，高的在前）。值相同时保持数据顺序。变化时，每一行同时用同一根弹簧移到新位置，数字随之移动；十字线和色带数量不受影响。

### 十字线

把指针移到色带列上（名称列和数值列没有反应）即可把十字线放到某一天：图表上方的日期和每行末尾的值会立即变成那一天，状态行写出 "Picked day"。线出现在指针所在处，然后以一根快速的弹簧跟随；指针离开时它淡出，读数回到最近的一天。在触摸屏上，点击或水平拖动会设置十字线，抬起手指后它仍然保留；点击图表旁边则清除它。`SelectedDayIndex` 是双向的，`SelectDay` 和 `ClearSelection` 可在代码中驱动它，`DayChanged` 和 `DayCommand` 报告每一次变化。

<Demo name="Events" />

### 格式化数值与其他单位

`ValueFormatter` 格式化各行的值、刻度条两端的标签和朗读的文字；默认写成带符号的整数百分比，使用真正的减号。如果想要一个不依赖数据的整齐色带边缘，请设置 `Extent`。

```csharp
chart.ValueFormatter = v => v switch { > 0 => $"+{v:0} ms", < 0 => $"−{-v:0} ms", _ => "0 ms" };
chart.Extent = 60;
```

<Demo name="Latency" />

### 窄容器

区块会读取自己的宽度。宽度不超过 900 时，名称列和数值列变窄（116 和 52）；不超过 560 时，内边距降到 16，行高为 34px，文字为 12px，两列宽度为 84 / 44px，两个开关共用一行（放不下时各占一行），颜色刻度条占满整行。

<Demo name="Compact" />

### 文字

`Labels` 保存区块写出或朗读的每一个词（开关标签、状态行、说明及其格式、播报以及表格的列名），带有英文默认值；赋一个新的 `HorizonChartLabels` 即可翻译或改写。`AboveColor` 和 `BelowColor` 取代 Series1 和 Series3 的颜色，当页面自带控件时，可用 `IsSortSwitchVisible`、`IsBandSwitchVisible` 和 `IsLegendVisible` 隐藏相应部分。

## API 参考

<!-- api: HorizonChart -->

<!-- api: HorizonSeries -->

<!-- api: HorizonDay -->

<!-- api: HorizonTick -->

<!-- api: HorizonChartLabels -->

<!-- api: HorizonDayChangedEventArgs -->

<!-- api: HorizonSortChangedEventArgs -->

<!-- api: HorizonBandsChangedEventArgs -->

<!-- api: HorizonSort -->

## 无障碍

- 图表是一个可获得焦点的分组，名为 "{title}. {n} rows"，并以按键提示作为帮助文字。焦点环绘制在图表内部，区块的圆角裁剪不会把它切掉。
- 用键盘移动十字线时，会礼貌地播报日期以及最高和最低的行："Tuesday, March 16. Top Foxhollow +8%, bottom Northgate −31%."。排序和色带数量的变化也会播报；移动指针则不会。
- 一张隐藏的表格按当前顺序列出每一行，包含其最新值、最高值和最低值以及各自的日期，屏幕阅读器因此无需形状就能读出数字。
- 颜色刻度条是一张带有朗读描述的图像（"Color scale from −36% under the forecast to +36% over it, 3 bands per side"），它的说明写明一条色带代表什么。方向同样由带符号的数字传达，而不只靠颜色。
- 两个开关都是有名称的分组；其中的按钮是切换按钮。

### 键盘

- **Tab** 依次前往排序开关、色带开关和图表，各一个停靠点。Tab 进入图表时，十字线会放在最新的一天。
- **← →** 把十字线移动一天；**Shift + ← →** 移动一周。它在第一天和最后一天停住。
- **Home / End** 跳到第一天和最后一天。
- **Escape** 隐藏十字线；焦点留在图表上，下一次按方向键从最新的一天开始。
- 在两个开关中，**← ↑** 和 **→ ↓** 立即改变选择并循环；**Home / End** 跳到两端。

## 动效

- **折叠**：一根弹簧（临界阻尼，600ms 达到静止）把每一行的色带数量带到新值；图层及其 alpha 在每一帧跟随它，什么都不会重建。中途出现新的目标时保留速度。颜色刻度条的格子用 480ms 的先慢后快再慢曲线增长或缩小并改变不透明度，与折叠同时开始；它的说明立即变化。
- **排序**：每一行从原位置弹到应在的位置（约 700ms，无过冲），所有行同时开始；各行绘制不透明的底色，所以一行滑过另一行时会遮住它。
- **十字线**：它在原地出现并用 160ms 淡入，以一根快速的临界阻尼弹簧（约 250ms 达到静止）跟随指针或按键，离开时在原位用 160ms 淡出。文字始终立即变化。
- **开关**：被选中的胶囊在一根无过冲的弹簧上滑动。
- 区块首次出现时不播放任何动画。减弱动效和关闭动效会让每一次折叠、重新排序、十字线移动和刻度条变化立即完成。

## 相关

- `Metric explorer` 和 `Revenue explorer`，其他在时间轴上带有十字线的 Analytics 区块。
- `Cohort retention`，同样用颜色刻度比较随时间变化的许多行。
- `SegmentedControl`，此区块使用的开关。
