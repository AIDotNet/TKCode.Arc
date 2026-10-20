# 图表基础

> Charts 大类自绘图表共享的基础层：数据模型、比例尺、坐标轴、系列色、路径重采样与形变、指针与提示、首次揭示、键盘与可访问数据、加载与空状态、性能。各图表的差异见 [图表大类 overview](overview.md)。本文描述已落地的实现（P8，LineChart / BarChart / DonutChart / Streamgraph / BrushChart / Sparkline 已使用），其余图表接入时沿用同一套 API。

## 1. 范围

| 图表 | 状态 | 主要使用的基础能力 |
|---|---|---|
| Sparkline | 已实现 | 单位空间曲线、描边绘制（按弧长的笔尖）、弹簧光标、形变 |
| BarChart | 已实现 | 带状槽位（从最新一根倒数）、值轴、每根柱子的 `MotionValue` |
| LineChart | 已实现 | 多系列、图例开关、十字线、提示、共享网格形变、值轴弹簧、加载脉冲 |
| DonutChart | 已实现 | 扇区几何、角度命中、起止角插值、行图例 |
| Streamgraph | 已实现 | 161 点网格、堆叠偏移（wiggle / silhouette / zero）、层隔离、直接标签 |
| BrushChart | 已实现 | 时间比例尺与日历刻度、按像素列降采样、7 点平均带、拖拽窗口、事件标注 |
| Gauge、ActivityHeatmap、WaffleChart、SlopeChart、Ridgeline、Treemap | 另一团队并行实现 | 合并后可改为派生 `ChartBase` |

AnimatedCounter 不依赖本基础层，它基于 Core 的 RollingDigits（P14）。

## 2. 代码位置

```
src/TKCode.Arc/Charts/
├── _Foundation/                 基础层（类型一律以 Chart 开头）
│   ├── ChartBase.cs             public abstract：数据管线、调色板、揭示、读数、图例、格式化、可访问摘要
│   ├── ChartSeries.cs           public：ChartSeries、ChartSeriesCollection
│   ├── ChartTypes.cs            public：ChartPoint、ChartCurve、ChartTone、ChartValueContext、事件参数
│   ├── ChartData.cs             internal：ChartData 快照、ChartDataBuilder、ChartBindingReader
│   ├── ChartScales.cs           internal：ChartLinearScale、ChartBandScale、ChartTimeScale、ChartTicks、ChartTime
│   ├── ChartCurves.cs           internal：单调三次、采样、重采样、并集网格
│   ├── ChartSpringArray.cs      internal：逐分量弹簧数组（P16 形变驱动）
│   ├── ChartLineMorph.cs        internal：共享 x 网格上的线形变
│   ├── ChartValueAxis.cs        internal：随弹簧比例尺滑动的网格线与刻度
│   ├── ChartAxisHelpers.cs      internal：类目轴抽稀与绘制、加载脉冲 ChartBreath
│   ├── ChartTooltip.cs          internal：P17 提示卡
│   ├── ChartLegend.cs           internal：ChartLegend 面板、ChartLegendItem 切换项
│   ├── ChartPlot.cs             internal：模板部件 PART_Plot（自绘表面）与其 AutomationPeer
│   ├── ChartPalette.cs          internal：Token 捕获、系列色、画刷/画笔缓存、文字缓存
│   ├── ChartGeometry.cs         internal：折线、面积、带、柱、扇区的 StreamGeometry
│   └── ChartInput.cs            internal：ChartKeyboard 读数键盘模型、ChartDragTracker 本地拖拽
├── LineChart/ BarChart/ DonutChart/ Streamgraph/ BrushChart/ Sparkline/
```

P16（形变）与 P17（指针跟随）在 Core 中没有通用实现，基础层用 `ChartSpringArray` / `ChartLineMorph` 与 `ChartTooltip` 自行提供；P18 首次揭示直接使用 Core 的 `ViewportVisibility`。BrushChart 的拖拽用本地 `ChartDragTracker`（Core 手势库并行开发中，合并后可替换）。

## 3. 控件结构

- `ChartBase : TemplatedControl`（public abstract，构造函数 `private protected`，因此只能在本程序集内派生）。每个图表的模板至少包含一个 `arc:ChartPlot Name="PART_Plot"`；多系列图表另有 `arc:ChartLegend Name="PART_Legend"`。
- `ChartPlot` 是唯一的绘图表面：测量时向图表要高度（`MeasurePlotHeight`）与宽度（`MeasurePlotWidth`），`Render` 回调图表的 `RenderPlot`，指针、键盘、焦点都转给图表；它实现 `ICustomHitTest`，整块表面都能接收指针（否则只有画出的形状能命中）。
- 图表派生类只实现 `private protected` / `internal` 的钩子：`OnDataChanged(previous, current)`、`RenderPlot(plot, context, size)`、`OnPlotPointer*`、`OnPlotKeyDown`、`OnPlotFocusChanged`、`OnActiveIndexChanged`、`OnRevealed` / `OnRevealCompleted`、`BuildSummary` / `BuildDataTable`、`GetPlotValueText` 等。
- 主题：每个图表一个 `Themes/<Chart>Theme.axaml`，设置 `arc:ArcMotion.Tracks="True"`、模板，以及一组把 ArcDesign Token 写进 internal 属性的 Setter（`SecondaryBrush` `MutedBrush` `GridBrush` `StrongBrush` `SurfaceBrush` `RaisedBrush` `AccentBrush` `Series1Brush`…`Series4Brush` `FloatingShadow` `RestingShadow` `TextSizeXS`… `MediumWeight` 等）。几何（值槽宽、轴行高、描边宽）来自各图表的 `<Chart>Token`，经 `GutterWidth` / `AxisRowHeight` / `StrokeWidth` 写入。

## 4. 数据模型

| 入口 | 说明 |
|---|---|
| `ItemsSource` | 任意对象序列；`INotifyCollectionChanged` 会触发重建 |
| `XSelector` / `XBinding` | 每项的类目或日期（string、DateTime、DateTimeOffset、DateOnly）；`XBinding` 是 XPath 的 AOT 安全形式（`[AssignBinding]`），写作 `{CompiledBinding Date, DataType=local:Day}`（编译器无法从 `ItemsSource` 推断项类型，需要显式 `DataType`；反射式 `{Binding Date}` 也可用但不是 AOT 安全） |
| `YSelector` / `YBinding` | 单系列图表每项的值 |
| `Series`（`[Content]`） | `ChartSeries`：`Key`（稳定身份，默认 `Name`）、`Name`、`Values`（直接给值）或 `ValueSelector` / `ValueBinding`（从 `ItemsSource` 读）、`Color`、`IsDashed`、`HasArea` |
| `ChartPoint(X, Value)` | 现成记录类型，带可选 `Key`、`Label`（完整标签）、`AxisLabel`（空串表示该点不标轴）、`Color` |
| 默认识别 | 不写选择器时识别 `ChartPoint`、数字、`KeyValuePair<string,double>`、`(string,double)`、`(DateTime,double)` 等元组 |

- 数据在属性变化后合并为一次重建（`EnsureData`），生成不可变快照 `ChartData`：`Items`、`X`、`Keys`（重复类目自动去重）、`Labels`、`AxisLabels`、`PointColors`、`Series[]`。较短的系列以 0 补齐。
- 标签：日期默认完整标签 "Tue, Sep 15"、轴标签 "Sep 15"（en-US，与 Arc 相同），可用 `XLabelFormatter` / `XAxisFormatter` 覆盖。
- 绑定读取不用反射：`ChartBindingReader` 把每项设为一个分离元素的 DataContext，读出绑定当前值（编译绑定即 AOT 安全）。
- 隐藏系列：`HiddenSeries`（双向）+ `HiddenSeriesChanged`（LineChart、Streamgraph）；DonutChart 用 `HiddenKeys` + `HiddenKeysChanged`。

## 5. 比例尺与坐标轴

| 能力 | 实现 |
|---|---|
| 线性 / 带状 / 时间比例尺 | `ChartLinearScale`、`ChartBandScale`（槽宽的 58%，上限 28px）、`ChartTimeScale`（UTC 毫秒） |
| "nice" 刻度 | `ChartTicks.Nice`（1 / 2 / 2.5 / 5，最多 4 行，数据不低于 0 时从 0 起）、`FromZero`（BarChart：1 / 2 / 3 / 5）、`TopFromZero`（BrushChart）；`Clean` 去掉浮点噪声 |
| 日历刻度 | `ChartTime.Ticks`：每 76px 一个，依次尝试 1 / 2 / 7 / 14 天、1 / 2 / 3 / 6 / 12 个月、年，落在真实边界上 |
| 类目轴抽稀 | `ChartCategoryAxis.Picks`：每 76px（Streamgraph 72px）一个，从最新一个倒数；绘制时第一个左对齐、最后一个右对齐 |
| 值轴 | `ChartValueAxis`：最小/最大值是 smooth 物理弹簧，网格线与刻度跟着滑动；新刻度 standard 淡入，旧刻度 instant 淡出；可让刻度标签避开均值标签 |
| 格式化 | `ValueFormatter`（`Func<ChartValueContext, string>`，上下文含值、索引、系列）> `ValueFormat`（.NET 格式串）> 图表默认（Arc：最多 1 位小数分组；BarChart 0 位）；`TickFormatter` 默认紧凑格式 1.2K（BarChart 刻度沿用值格式，与 Arc 一致）；`Unit` 追加在读数后 |
| 显示开关 | `ShowGrid`、`ShowXAxis`、`ShowYAxis`、`YMinimum`、`YMaximum`（由支持的图表公开） |
| 网格线 | `Border`，基线 `BorderStrong`；1px 线对齐到像素中心；轴文字 `TextMuted`、`TypeXS` |

## 6. 颜色

- 系列色：数据顺序映射到 `Series1`–`Series4`（随 accent 与明暗变化），之后用前景色混入表面色的中性阶梯：DonutChart 56 / 42 / 32%，Streamgraph 30 / 18 / 24 / 14%（相邻层至少相差 10 阶）；"Other" 用 26%。`ChartSeries.Color` / `ChartPoint.Color` 覆盖单个颜色。
- DonutChart 的颜色跟随 key 第一次出现的顺序，切换数据集时同一 key 保持颜色。
- 与 accent 相关的派生色（BarChart 柱色 `accent 62%/76%`、擦读时 24%/34%，Streamgraph 层色 `series 82%`，面积 9–14%）在绘制时由捕获的 Token 颜色按 OKLab 混合计算，不进入控件 Token。
- `ChartPalette` 在 Token 变化后惰性重新捕获，画刷与画笔按颜色缓存；动画中的颜色渐变量化为 24 阶，使用有限的缓存画刷。

## 7. 渲染与性能

| 场景 | 策略 |
|---|---|
| 绘制 | 每个图表一次 `Render`，几何用 `StreamGeometry`，没有逐点控件 |
| 几何缓存 | 数据、尺寸或形变帧变化时才重建；悬停、十字线移动只重绘，不重建几何 |
| 动画帧 | `ChartSpringArray` 每帧只算一次弹簧闭式解的两个基向量，再线性组合到所有分量；插值写进预分配数组；唯一的逐帧分配是形状确实变化时新建的平台几何 |
| 文字 | `ChartTextCache` 按文字与样式缓存 `FormattedText`（上限 1024），刻度、均值标签等只在值变化时格式化 |
| 降采样 | BrushChart：每像素超过 2 个点时，每个像素列只保留最低与最高（尖峰不丢）；概览路径每个宽度只建一次 |
| 加载 | `ChartBreath`：只在加载、已挂载、可见且动效为 Full 时循环；Reduced 固定 32% |

## 8. 动效

| 能力 | 实现 | 原语 |
|---|---|---|
| 首次揭示 | `ViewportVisibility` 达到图表的触发比例（默认 50%；Arc：Line/Stream/Brush 30%，Bar/Donut 35%，Sparkline 50%）且有数据时只运行一次。默认用加长的无回弹 smooth 弹簧（visualDuration 0.9）；LineChart 与 Sparkline 按 Arc 源码用 840ms in-out 的"笔画"补间（`PenStroke`）。线条用裁剪从左揭示，Sparkline 按弧长计算笔尖 | P18、P15 |
| 数据切换 | `ChartLineMorph`：新形状的顶点与 241（Sparkline 161）点均匀网格取并集，屏上当前值与速度插值到新网格，每个点各自弹簧到目标——从屏上形状出发、被打断时保留速度、最终精确落在新顶点。Streamgraph 用固定 161 点 `ChartSpringArray`；DonutChart 插值起止角度；BarChart 每根柱子的高度与槽位各是一个 `MotionValue` | P16 |
| 隐藏系列 | 每个系列一个 presence（smooth），值压平到基线并淡出；其余系列重新定比例 | P16 |
| 指针 | 十字线 snappy 弹簧滑动（第一次读数直接到位）；`ChartTooltip` 位置用两分量弹簧数组跟随（snappy，k≈584 c≈42.5），靠边翻转并夹在绘图区内，出现时 fast 淡入并从 0.96 放大到 1；非活动项变暗（Bar 柱色、Donut 0.45、Stream 0.34） | P17 |
| Reduced | 揭示改为 fast 整体淡入；形变、光标与提示直接到位；补间（淡入淡出）照常 | — |
| Off | 立即终态 | — |

动效全部由代码驱动：数值都是同一个自绘几何内部的坐标，ControlTheme 过渡无法表达；主题仍设置 `ArcMotion.Tracks` 以缓存动效等级。

## 9. 交互与键盘

| 按键 | 行为（`ChartKeyboard.TryStep`） |
|---|---|
| ← / → | 前后一个点；从静止状态按 → 落在最新点 |
| ↑ / ↓ | 同 → / ←（LineChart、BarChart、Sparkline）；Streamgraph 为切换隔离层 |
| PageUp / PageDown | 约 1/6 区间（BarChart 最多 7 根，BrushChart 视图的 1/8） |
| Home / End | 首末点 |
| Escape | 释放读数 |

- 键盘焦点进入绘图区时（Tab / 方向导航）读数落在最新点，失焦释放。鼠标悬停即读、离开释放；触摸按下读数并捕获指针，抬起释放。
- 图例项 `ChartLegendItem` 是可聚焦的切换按钮（Enter / Space），悬停或键盘聚焦时预览该系列（Streamgraph 隔离该层）。
- `ActiveIndex`（双向）与 `ActiveIndexChanged`（`Index`、`Item`、`Label`、各可见系列的 `Values`、Streamgraph 的 `SeriesKey`）用于驱动外部标题读数。

## 10. 可访问数据

- 图表本身是 Group，名称为 `Label`，帮助文本为 `Summary`（例如 "Signups, Tue, Sep 1 to Fri, Sep 4. This period, latest 240 users."）。
- `ChartPlot` 的 Peer 是 Slider（或 Group），名称如 "Signups, explore by date"；Avalonia 的 RangeValue 模式没有值文本，所以同时实现 `IValueProvider`，值文本读出日期与每个可见系列；读数变化时发出 RangeValue 变更事件。
- 隐藏数据表以 `DataTableText` 提供（表头一行，每行一个点，制表符分隔）；`Announcement` 保存 Arc 实时区域会播报的文字（"Loading"、区间平均、显示/隐藏后的总数、读数）。
- 图例项是 Toggle（`IToggleProvider`）；BarChart 标题等装饰部件标为 `AccessibilityView=Raw`，避免重复朗读。

## 11. 加载、空状态与错误

- `IsLoading`（LineChart）：线条变暗并呼吸；没有数据时显示呼吸的骨架带。
- `EmptyLabel`：没有数据时居中显示（默认 "No data for this range"，DonutChart / BrushChart 为 "No data yet"）；全部系列被隐藏时显示 "Choose a series to show"（Streamgraph 为 layer）。
- 错误由应用在图表外层用 Alert 表示。

## 12. 接入新图表

```csharp
public class WaffleChart : ChartBase                       // 同程序集内派生
{
    private protected override void OnDataChanged(ChartData previous, ChartData current)
        => _cells.AnimateTo(Layout(current), ArcMotionPresets.Morph);   // ChartSpringArray：保留速度的形变
    private protected override void RenderPlot(ChartPlot plot, DrawingContext context, Size size)
    {
        var p = Palette;                                   // Token 色：p.SeriesColor(i)、p.Brush(c)、p.Pen(c, 1)
        context.DrawText(Texts.Get(p, "Other", p.SizeXS, p.TextMuted), default);
    }
    internal override void OnPlotKeyDown(ChartPlot plot, KeyEventArgs e)
    {
        if (ChartKeyboard.TryStep(e.Key, ActiveIndex, 0, Data.Count - 1, 10, true, out var next)) { SetActive(next); e.Handled = true; }
    }
}
```

模板放 `<arc:ChartPlot Name="PART_Plot" Focusable="True"/>`，主题复制 LineChartTheme 的 Token Setter 块。

## 13. 已知限制

- Avalonia 没有屏幕外的表格元素，隐藏数据表以 `DataTableText` 字符串与 `Summary` 帮助文本提供；没有实时区域事件，播报文字保存在 `Announcement`。
- 触摸时绘图区会捕获指针，纵向滑动不会再滚动页面（Arc 的 `touch-action: pan-y` 无直接对应）。
- 形变帧会重建平台几何对象（每个系列每帧一个 StreamGeometry），这是自绘路径动画的固有开销。
