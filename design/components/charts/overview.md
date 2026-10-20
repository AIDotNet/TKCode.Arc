# 图表（charts）

> Arc 的数据可视化组件：在仪表盘与卡片里展示趋势、比较、占比、分布与单值，数据切换时从屏上形状形变，并能用指针和键盘读数。

## 1. 大类定位
- 覆盖仪表盘里的常见问题：随时间的走势（LineChart、BrushChart、Streamgraph、Sparkline）、类目与前后比较（BarChart、SlopeChart）、占比（DonutChart、WaffleChart、Treemap）、分布（Ridgeline）、日节律（ActivityHeatmap）、单值对范围（Gauge），以及独立变化的数字（AnimatedCounter）。
- 边界：任务进度与套餐额度归 [进度](../progress/overview.md)（Progress、UsageMeter）；带卡片外框的指标归 [卡片](../cards/overview.md)（MetricCard，依赖 AnimatedCounter）；需要排序与选择的记录归 [表格与数据](../tables/overview.md)。Arc 的 Split bar 与整组 Advanced charts 属于 Pro，不在范围内。
- 共同特征：除 AnimatedCounter 外全部自绘；只读展示加读数交互，不编辑数据；首次进入视口揭示一次，数据变化时从当前形状形变而不是重画；可视图形对读屏隐藏，数据由隐藏摘要、数据表与实时区域提供。

## 2. 组件一览
| 组件 | 类名 | Arc 页面 | 阶段 | Arc 依赖 | 建议基类 | 原语 |
|---|---|---|---|---|---|---|
| Line chart | LineChart | [line-chart](https://uiarc.dev/components/line-chart) | P8 | 无 | Control（自绘） | P15 P16 P17 P18 P21 P29 |
| Bar chart | BarChart | [bar-chart](https://uiarc.dev/components/bar-chart) | P8 | 无 | Control（自绘） | P3 P14 P16 P17 P18 |
| Donut chart | DonutChart | [donut-chart](https://uiarc.dev/components/donut-chart) | P8 | 无 | Control（自绘） | P3 P14 P16 P17 P18 P27 |
| Streamgraph | Streamgraph | [streamgraph](https://uiarc.dev/components/streamgraph) | P8 | 无 | Control（自绘） | P16 P17 P18 P27 |
| Brush chart | BrushChart | [brush-chart](https://uiarc.dev/components/brush-chart) | P8 | 无 | Control（自绘） | P2 P12 P15 P16 P17 P18 P29 |
| Waffle chart | WaffleChart | [waffle-chart](https://uiarc.dev/components/waffle-chart) | P8 | 无 | Control（自绘） | P14 P16 P17 P18 P19 P26 |
| Slope chart | SlopeChart | [slope-chart](https://uiarc.dev/components/slope-chart) | P8 | 无 | Control（自绘） | P14 P15 P16 P17 P18 P27 |
| Sparkline | Sparkline | [sparkline](https://uiarc.dev/components/sparkline) | P8 | 无 | Control（自绘） | P3 P15 P17 P18 P29 |
| Gauge | Gauge | [gauge](https://uiarc.dev/components/gauge) | P8 | 无 | Control（自绘） | P14 P15 P18 P23 |
| Activity heatmap | ActivityHeatmap | [activity-heatmap](https://uiarc.dev/components/activity-heatmap) | P8 | 无 | Control（自绘） | P3 P14 P17 P18 |
| Animated counter | AnimatedCounter | [animated-counter](https://uiarc.dev/components/animated-counter) | P1 | 无 | Control（RollingDigits） | P2 P5 P14 P18 |
| Ridgeline | Ridgeline | [ridgeline](https://uiarc.dev/components/ridgeline) | P8 | 无 | Control（自绘） | P16 P17 P18 P27 |
| Treemap | Treemap | [treemap](https://uiarc.dev/components/treemap) | P8 | 无 | Control（自绘） | P14 P16 P17 P19 |

## 3. 共享约定
- 共享图表层（比例尺、坐标轴、series 色、路径重采样与形变、指针与提示、可访问数据）只在 [chart-foundation.md](./chart-foundation.md) 维护，12 个自绘图表都派生自其中 internal 的 `ChartBase : Control`；下文各组件只写差异。
- 通用属性（下文各表不再重复）：除 AnimatedCounter 外每个图表都有必填的 `label`，映射为 `Label`（string），用作图表、摘要与隐藏表格的名称；多数图表的 `unit` → `Unit`；`formatValue` / `formatTick` / `formatDate` → `ValueFormatter` / `TickFormatter` / `DateFormatter`（`Func<…, string>?`，签名随组件附带系列、索引或类目，默认走 S11 的分组数字与紧凑格式如 1.2K）；`emptyLabel` → `EmptyLabel`；`categoryLabel` → `CategoryLabel`。默认文案（"No data yet"、"No data for this range"、"Total"、"Other"、"Date" 等）全部进入本地化目录。
- 受控模式：Arc 的 `xxx` / `defaultXxx` / `onXxxChange` 三件套统一映射为一个可双向绑定的属性加 `XxxChanged` 事件；数据项与系列必须有稳定的 key 或 id，跨数据集保持 key 才会形变而不是重画。
- 布局：宽度跟随容器（S5），高度由 `PlotHeight` 等属性给出（对应 Arc 的 `height`）；按测得的像素重绘以保持描边清晰；轴标签按最小间距抽稀（LineChart 每 76px、Streamgraph 每 72px），始终保留最新一个。
- 读数交互：可读数的图表把绘图区做成一个可聚焦元素，方向键逐点或逐项，时间序列的 PageUp / PageDown 跳约 1/6，Home / End 到首末，Escape 清除读数；鼠标悬停即读、离开释放；触摸横向拖动读数，纵向滑动仍滚动页面；悬停样式只对鼠标等精确指针生效。
- 可访问性：路径、环、格子等图形设 `AutomationProperties.AccessibilityView=Raw`，数据由隐藏摘要、隐藏数据表与 `LiveSetting=Polite` 的实时区域提供；图例开关是 Toggle（`IToggleProvider`）；身份不只靠颜色（文字标签、箭头字形、阈值标签）。
- 动效：首次进入视口揭示一次（P18，StaggerItem + inOut，S6 的触发比例按图表而定），描线用加长 visualDuration、无回弹的 smooth 弹簧（P15）；数据变化先重采样再从屏上当前形状形变并保留速度（P16，默认 smooth，例外见 chart-foundation）；读数光标与提示框用 snappy 弹簧滑动（P17）；数字用 smooth 弹簧滚动（P14）；Reduced 跳过描线、形变与滑动直接到终态，Off 立即终态。
- 性能：路径、角度与变换由 `MotionValue` 逐帧写入绘制，不触发布局或可视树重建；每个图表一个尺寸观察；数据量遵循 Arc 建议（折线每系列数百点，BrushChart 数千点）。

## 4. 组件

### 4.1 LineChart（Line chart）
`slug: line-chart` · 阶段 P8 · Arc 依赖：无 · 建议基类：Control（自绘）

**Arc 行为摘要**：多系列折线图，带滑动十字线和图例开关，切换区间（7d、30d、90d）时线条从屏上形状形变而不是重画。适合 1 到 3 个度量随时间的走势，以及本期对上期或目标（虚线系列）。紧凑的行内趋势用 Sparkline，离散类目用 BarChart，部分占整体用 DonutChart。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `data` / `series`（必填） | `{ key, label, axisLabel?, values }[]` / `{ key, label, color?, dashed?, area? }[]`，缺值按 0 | `Data`（`IReadOnlyList<LineChartDatum>`）/ `Series`（`IReadOnlyList<LineChartSeries>`，`IsDashed`；`HasArea` 默认只有首个系列为 true） |
| `height` | `220` | `PlotHeight`（double，宽度随容器） |
| `hiddenSeries` / `defaultHiddenSeries` / `onHiddenSeriesChange` | `string[]` | `HiddenSeries`（`IReadOnlyList<string>`，双向）+ `HiddenSeriesChanged` 事件 |
| `onActiveChange` | `(index, datum)`，离开时为 null | `ActiveIndexChanged` 事件（`Index`、`Datum`，可驱动标题读数） |
| `loading` | `false` | `IsLoading`（bool：当前线条变暗，无数据时显示骨架） |
| `legend` / `curve` | 多于 1 个系列时显示 / `"smooth"` | `IsLegendVisible`（bool?，null 为自动）/ `Curve`（`ChartCurve`：Smooth 为不越过数据的单调曲线，Linear） |

- **交互与键盘**：←/↓ 上一个点，→/↑ 下一个点（静止时从最新点开始）；PageUp / PageDown 跳约 1/6 区间；Home / End 到首末点；Escape 放开十字线；触摸用指针捕获读数，抬起即释放。
- **可访问性**：绘图区是 Slider（`IRangeValueProvider`），值文本读出日期与每个可见系列的值；隐藏摘要给出区间与最新值，隐藏数据表列出每个点；图例按钮是 Toggle；加载中由 Polite 区域播报 "Loading"；路径与提示框为 Raw，避免重复朗读。
- **动效**：
  - 首次进入视口从左到右描线，面积区随裁剪揭示（P15、P18、P29；加长的无回弹 smooth）。
  - 新数据在共享 x 位置上重采样，从屏上形状形变；数值比例尺弹簧到新范围，网格线随之滑动；隐藏的系列压平到基线并淡出（P16；smooth）。
  - 十字线在点之间滑动，提示框用独立弹簧跟随并在靠边时翻转（P17；snappy）；加载中线条脉冲（P21，离开视口暂停）。
  - Reduced：跳过描线、形变与滑动，停止加载脉冲；Off：立即终态。
- **Avalonia 实现要点**：`Render` 中以 `StreamGeometry` 自绘，路径由 `MotionValue` 逐帧写入（S1、S2）；值槽固定 48px；平滑曲线每段最多采样 16 点（每条约 192 点）；键盘读数复用 S9 的方向键模型，触摸方向锁定走 S10；首次描线等待 S6。
- **AtomUI 参考**：—（AtomUI 无同类控件）
- **风险与未决**：Avalonia 的 RangeValue 模式没有 valuetext，多系列读数要借 `IValueProvider` 字符串或 HelpText 承载，需验证读屏效果（由 chart-foundation 统一）；按 chart-foundation 的落地顺序，LineChart 在第 4 步引入时间比例尺、多系列、图例、十字线与可访问数据表，是共享层工作量最大的一步。
- **实现状态（P8，已落地）**：`LineChart : ChartBase`；数据用 `ItemsSource` + `XBinding/XSelector` 与 `Series`（`ChartSeries`：`Key`、`Name`、`Values` 或 `ValueBinding/ValueSelector`、`Color`、`IsDashed`、`HasArea`），`PlotHeight=220`、`Curve`、`ShowLegend`（null 为多于 1 个系列时显示）、`ShowGrid/ShowXAxis/ShowYAxis`、`YMinimum/YMaximum`、`TickFormatter`、`ValueFormat/ValueFormatter`、`Unit`、`IsLoading`、`HiddenSeries` + `HiddenSeriesChanged`、`ActiveIndex` + `ActiveIndexChanged`。形变用 `ChartLineMorph`（并集网格、逐点弹簧、保留速度），描线 840ms in-out 裁剪揭示，值轴弹簧，十字线 snappy，提示卡独立弹簧跟随并翻转。可访问性：绘图区 Slider + 值文本，`Summary`、`DataTableText`、`Announcement`（"Loading"）。系列色按任务要求用 Series1–4（Arc 源码的第 2、3 条线为中性灰，可用 `ChartSeries.Color` 还原）。

### 4.2 BarChart（Bar chart）
`slug: bar-chart` · 阶段 P8 · Arc 依赖：无 · 建议基类：Control（自绘）

**Arc 行为摘要**：单系列柱状图，带平均参考线与可擦读的标题数值：静止时标题显示平均值，擦读时显示所指柱子的值。用于一个度量跨天、周或月的比较，稳定的 key 让区间切换时柱子形变。紧凑趋势用 Sparkline，全年日节律用 ActivityHeatmap。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `data`（必填） | `{ key, label, axisLabel?, value }[]` | `Data`（`IReadOnlyList<BarChartDatum>`） |
| `period`（必填） | string，标题下方的区间文字 | `Period`（string） |
| `averageLabel` / `valueLabel` | `"Daily average"` / `"Total"` | `AverageLabel` / `ValueLabel`（string，静止与擦读时的标题标签） |
| `showAverage` | `true` | `IsAverageVisible`（bool） |
| `height` | `176` | `PlotHeight`（double） |

- **交互与键盘**：←/→（↓/↑ 同）在柱子间移动；PageDown / PageUp 最多移动 7 根；Home / End 到首末；Escape 停止擦读并回到平均值。
- **可访问性**：图形是 Image 角色，名称概括平均、最高与最低；可聚焦的 Slider 擦读器逐根读出标签与值；隐藏数据表列出所有值；区间变化以 Polite 播报。
- **动效**：
  - 首次可见时柱子交错长出（P18；StaggerItem + inOut）。
  - 切换区间时柱子、刻度与平均线形变（P16；smooth）。
  - 擦读光标移动（P17；snappy），标题数值滚动（P14；smooth），标题标签在平均与合计之间切换（P3；standard + enter，BlurSoft）。
  - Reduced：柱子直接出现，数值原地替换不移动；Off：立即终态。
- **Avalonia 实现要点**：柱宽上限 28px，宽容器加大间距而不是加粗柱子；标题数值复用 RollingDigits（与 AnimatedCounter 同源）；尺寸观察 S5、首次揭示 S6；隐藏数据表随区间长度增长，长区间注意 Automation 子节点数量。
- **AtomUI 参考**：—（AtomUI 无同类控件）
- **风险与未决**：Arc 的 `formatValue` 同时格式化数值与刻度，与 LineChart 拆分 `formatValue` / `formatTick` 不一致，需在 chart-foundation 统一格式化器约定。
- **实现状态（P8，已落地）**：`BarChart : ChartBase`；数据用 `ItemsSource`（`ChartPoint` 或选择器/绑定），`Period`、`AverageLabel`、`ValueLabel`、`ShowAverage`、`PlotHeight=176`、`ShowGrid/ShowXAxis/ShowYAxis`、`YMaximum`，只读 `Average`。标题：`ArcContentSwap`（Roll）+ `RollingDigits`（自定义 `ValueFormatter`/`ValueFormat` 时改用文字滚动）。柱子从最新一根倒数、按 key 形变（每根柱子高度与槽位各一个 `MotionValue`），首次揭示 morph 弹簧交错长出，离开的柱子 sink 后移除；值轴、均值线弹簧；擦读光标 snappy；刻度沿用值格式（统一了 Arc 的 formatValue 约定，见 chart-foundation §5）。键盘 PageUp/PageDown 最多 7 根；区间变化写入 `Announcement`。焦点不画焦点环（Arc 为 `outline: none`），以读数光标指示。

### 4.3 DonutChart（Donut chart）
`slug: donut-chart` · 阶段 P8 · Arc 依赖：无 · 建议基类：Control（自绘）

**Arc 行为摘要**：环形图，图例与环同步显示或隐藏扇区，中心读数滚动到活动扇区，过小的份额自动并入 Other，切换数据集时弧形变。用于 2 到 6 个部分占一个整体（流量来源、支出类别）。部分很多或需要精确比较用 BarChart，单值对上限用 Gauge 或 UsageMeter，随时间变化用 LineChart。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `data`（必填） | `{ key, label, value, color? }[]`，值不大于 0 的项忽略 | `Data`（`IReadOnlyList<DonutChartDatum>`） |
| `totalLabel` / `otherLabel` | `"Total"` / `"Other"` | `TotalLabel` / `OtherLabel`（string） |
| `size` / `thickness` | `208` / `24` | `Diameter` / `RingThickness`（double，窄容器按比例缩小） |
| `groupBelow` / `maxSegments` | `0.04` / `6`（含 Other） | `GroupBelow`（double）/ `MaxSegments`（int） |
| `activeKey` / `defaultActiveKey` / `onActiveChange` | string 或 null / null | `ActiveKey`（string?，双向）+ `ActiveKeyChanged` 事件（点击环或 Select 模式的图例行时固定） |
| `hiddenKeys` / `defaultHiddenKeys` / `onHiddenKeysChange` | `string[]` / `[]` | `HiddenKeys`（`IReadOnlyList<string>`，双向）+ `HiddenKeysChanged` 事件 |
| `legendAction` / `legend` | `"toggle"` / `true` | `LegendAction`（`DonutLegendAction`：Toggle、Select）/ `IsLegendVisible`（bool） |

- **交互与键盘**：图例行 ↓/→ 下一行（末尾循环），↑/← 上一行，Home / End 首末行；Enter / Space 显示或隐藏扇区（Select 模式下固定或取消）；Escape 清除固定；无图例时环可聚焦，方向键逐扇区，Enter 固定。
- **可访问性**：图例是键盘路径，每行是 Toggle，名称含值、占比与 Other 的成员；显示或隐藏以 Polite 播报新总数；环与中心读数为 Raw，隐藏摘要与数据表列出每个部分；键盘焦点预览扇区，只有键盘焦点画焦点环（S7）。
- **动效**：
  - 首次可见扫出一次：所有扇区从顶部同时出发，各尾边依次落后一拍（P18；StaggerItem + inOut）。
  - 新数据与隐藏扇区只插值起止角度、不插值路径，从屏上位置继续并保留速度，扇区顺序不变、不重挂载（P16；smooth）。
  - 活动扇区沿中线外移并略加厚、其余变暗（P27；snappy），指针按角度命中，跨过间隙不丢悬停（P17）；中心读数像鼓轮滚向活动扇区，总数与占比计数（P14；smooth）。
  - Reduced：弧、外移与文字直接到终态；Off：同 Reduced。
- **Avalonia 实现要点**：角度与外移是 `MotionValue`，每帧一次批量绘制（S1、S2）；中心读数常驻，用等宽数字（`FontFeatures` 启用 tnum）；容器不小于 460px 时图例在侧，否则在下（S5）；悬停样式只对鼠标生效，触摸点击即固定。
- **AtomUI 参考**：—（AtomUI 无同类控件）
- **风险与未决**：中心读数的标签随活动扇区像鼓轮一样切换，可能还需要 P3（主数据未列）；Other 分组与"同一 key 跨数据集保持颜色"属于 series 色规则，应写进 chart-foundation。
- 复核结论（汇总评审）：新增 P3，已同步到[组件总清单](../overview.md)。
- **实现状态（P8，已落地）**：`DonutChart : ChartBase`；`ActiveKey`（双向）+ `ActiveKeyChanged`、`HiddenKeys`（双向）+ `HiddenKeysChanged`、`LegendAction`（`DonutLegendAction.Toggle/Select`）、`ShowLegend`、`Diameter=208`、`RingThickness=24`、`GroupBelow=0.04`、`MaxSegments=6`、`TotalLabel`、`OtherLabel`，只读 `Total`。扇区几何与 Arc 相同（平行边间隙 3px、圆角 4、薄扇区收成楔形），起止角与外移各是 `MotionValue`（首次扫出 0.72s 无回弹、之后 0.5s，外移 snappy）。中心读数用三个 `ArcContentSwap`（Roll，方向按读数次序，近似 Arc 的鼓轮）；图例行自绘，值与占比按 smooth 弹簧计数，行的出现/离开以高度弹簧开合。偏差：中心数字不计数而是滚动切换。

### 4.4 Streamgraph（Streamgraph）
`slug: streamgraph` · 阶段 P8 · Arc 依赖：无 · 建议基类：Control（自绘）

**Arc 行为摘要**：在 wiggle 基线上层层堆叠、随时间流动的流图，切换区间时每层形变，悬停一层即隔离它并读出该时间点的各层数值。用于话题、渠道或类型的构成逐周变化，以及季节性与尖峰的叙事。需要精确值或系列间比较用 LineChart，少量时期或类目用 BarChart；含负值的数据不适用。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `data` / `series`（必填） | `{ key, label, axisLabel?, values }[]` / `{ key, label, color? }[]`，首层居中、其余上下交替 | `Data` / `Series`（与 LineChart 共用数据记录） |
| `height` | `260` | `PlotHeight`（double） |
| `offset` | `"wiggle"` | `Offset`（`StreamOffset`：Wiggle、Silhouette、Zero） |
| `hiddenSeries` / `defaultHiddenSeries` / `onHiddenSeriesChange` | `string[]` | `HiddenSeries`（双向）+ `HiddenSeriesChanged` 事件 |
| `onActiveChange` | `(index, seriesKey)` | `ActiveIndexChanged` 事件（`Index`、`SeriesKey`） |
| `legend` / `directLabels` | `true` / `true` | `IsLegendVisible` / `IsDirectLabelVisible`（bool，放得下时在层最厚处就地命名） |

- **交互与键盘**：Tab 聚焦绘图区并读最新点；←/→ 沿时间移动；↑/↓ 在堆叠中上下切换被隔离的层；PageUp / PageDown 跳 1/6，Home / End 到首末；Escape 清除读数。
- **可访问性**：绘图区是可聚焦 Group，每次读数播报时间点、隔离层的值与总计；图例是 Toggle，悬停或聚焦即隔离该层；隐藏数据表列出每点每层；层间留 2px 表面色间隙并就地命名，身份不只靠深浅。
- **动效**：
  - 首次可见时整条流从中心细线鼓起（P18；加长的无回弹 smooth）。
  - 新数据在共享网格上混合每一层，长度不同的区间也能形变；隐藏层变细到无，其余层重排（P16；smooth）。
  - 十字线与提示框在弹簧上滑动（P17；snappy）。
  - Reduced：直接显示终态形状，保留十字线与提示但不移动；Off：立即终态。
- **Avalonia 实现要点**：每次数据变化把每层重采样到 161 点，绘制时重算基线并直接写路径（S2）；轴标签从最新往回数；直接标签需 `TextLayout` 测量，层太薄或绘图区太窄时隐藏；悬停只重绘提示与少量属性。
- **AtomUI 参考**：—（AtomUI 无同类控件）
- **风险与未决**：隔离一层时其余层的弱化可能属于 P27（主数据未列）；wiggle 与 silhouette 偏移算法归 chart-foundation 的堆叠工具；层数建议不超过 8，其余由调用方并入 Other。
- 复核结论（汇总评审）：新增 P27，已同步到[组件总清单](../overview.md)。
- **实现状态（P8，已落地）**：`Streamgraph : ChartBase`；`Offset`（`StreamOffset.Wiggle/Silhouette/Zero`）、`ShowDirectLabels`、`ShowLegend`、`ShowXAxis`、`HiddenSeries` + `HiddenSeriesChanged`，只读 `ActiveSeriesKey`；`ActiveIndexChanged` 带 `SeriesKey`。161 点 `ChartSpringArray` 形变（保留速度），presence 弹簧压薄隐藏层，首次揭示从中线鼓起（0.9s 无回弹）；wiggle 基线去线性漂移；层间 2px 表面色描边；直接标签带 3px 表面色光晕，层太薄（<19px）时隐藏；隔离时其余层 0.34、提示行 0.5。图例为方形色块，悬停/聚焦预览即隔离该层。

### 4.5 BrushChart（Brush chart）
`slug: brush-chart` · 阶段 P8 · Arc 依赖：无 · 建议基类：Control（自绘）

**Arc 行为摘要**：密集时间序列加概览条：拖动窗口平移、拖手柄调整范围、在空轨道上拖出新窗口，双击重置；视图缩得很小时用 7 点平均线与区间带代替原始线，并可就地阅读事件标注。用于一年以上的日数据与事故复盘。几周的数据或多系列用 LineChart，不需要交互的小趋势用 Sparkline；不适合负值或对数轴。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `data`（必填） | `{ date, value }[]`，按时间升序 | `Data`（`IReadOnlyList<TimePoint>`：`DateTimeOffset`、double） |
| `annotations` | `{ date, label, description? }[]` / `[]` | `Annotations`（`IReadOnlyList<ChartAnnotation>`，主图与概览条都画标记） |
| `range` / `defaultRange` / `onRangeChange` | `[start, end]` 毫秒 / 全序列 | `Range`（`TimeRange?`，双向，新值以弹簧滑动过去）+ `RangeChanged` 事件 |
| `minSpan` | 7 天 | `MinSpan`（TimeSpan） |
| `height` / `overviewHeight` | `240` / `52` | `PlotHeight` / `OverviewHeight`（double） |

- **交互与键盘**：绘图区 ←/→ 在视图内逐点移动十字线；窗口 ←/→ 平移 1/10，Shift 或 PageUp / PageDown 平移一半，+ / - 以中心缩放；手柄 ←/→ 移动一个点，Shift 移动十个；Home / End 移到首末；Escape 或 0 重置为全序列。
- **可访问性**：窗口与两个手柄都是 Slider，值文本为日期；绘图区是可聚焦 Group，读数播报日期、值与当天事件；隐藏数据表列出窗口内每点及其事件；事件标记使用第二 series 色并配文字标签或提示。
- **动效**：
  - 首次可见时线条从左描入一次（P15、P18、P29；加长的无回弹 smooth）。
  - 拖动 1:1 跟手；预设、重置与受控变更以弹簧滑动窗口（P12；smooth）。
  - 值轴弹簧到视图内的最高点（P16；smooth），原始线与平滑趋势随密度交叉淡化（P2；standard + enter），十字线滑动（P17；snappy）。
  - Reduced：窗口直接到位，保留十字线但不移动；Off：立即终态。
- **Avalonia 实现要点**：每像素超过两个点时每列只保留最低与最高，尖峰不丢、路径小；概览路径每个宽度只建一次，滚动平均每次数据变化算一次；手柄命中区 28×44px；日期刻度按宽度在天、周、月、年间选择；事件标签从左到右放置，碰撞即跳过；拖拽走 S10。
- **AtomUI 参考**：—（AtomUI 无同类控件）
- **风险与未决**：绘图区、窗口、两个手柄共四个焦点停靠，Tab 顺序与 Automation 结构需在规格中确定；值轴固定从 0 开始，只适合计数与总量。
- **实现状态（P8，已落地）**：`BrushChart : ChartBase`；`ItemsSource` 的 X 为日期（DateTime/DateTimeOffset/DateOnly），`Annotations`（`ChartAnnotation(Date, Label, Description)`）、`Range`（`ChartTimeRange?`，双向，外部赋值以 smooth 弹簧滑动）+ `RangeChanged`、`MinSpan=7d`、`OverviewHeight=52`、`PlotHeight=240`、`DateFormatter`、`TickFormatter`。概览条 `BrushChartOverview` 与窗口/两个手柄三个焦点停靠（Slider Peer，值文本为日期），拖拽用本地 `ChartDragTracker`（3px 起拖、平移/调整/新建窗口、点按居中、双击重置）。按像素列保留最低最高、7 点平均与区间带随密度交叉淡化，值轴弹簧到视图内最高点。偏差：窗口与手柄在键盘聚焦时画焦点环（Arc 为 `outline: none`）。

### 4.6 WaffleChart（Waffle chart）
`slug: waffle-chart` · 阶段 P8 · Arc 依赖：无 · 建议基类：Control（自绘）

**Arc 行为摘要**：10×10 的单位图，每格代表 1%，数据变化时格子飞到新的分组。用于需要数得出来的占比、跨几个数据集比较相同类目，或在小份额必须可见时替代饼图与环图。份额随时间变化用 LineChart 或 Streamgraph，多类目精确比较用 BarChart。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `data`（必填） | `{ key, label, value, color? }[]`，首类从左下角起逐列填充 | `Data`（`IReadOnlyList<WaffleCategory>`） |
| `rows` / `columns` | `10` / `10` | `Rows` / `Columns`（int） |
| `accentKey` | 首个 key | `AccentKey`（string?，涂第一 series 色，之后三类用辅助色，再往后为中性阶） |
| `activeKey` / `defaultActiveKey` / `onActiveChange` | string 或 null / null | `ActiveKey`（string?，双向）+ `ActiveKeyChanged` 事件 |
| `legend` / `decimals` | `true` / `0` | `IsLegendVisible`（bool）/ `Decimals`（int，占比小数位） |

- **交互与键盘**：Tab 先聚焦网格，再依次到各图例项；方向键逐格移动读数；PageUp / PageDown 跳到上一或下一类目的首格；Home / End 到首末已填格；Escape 清除读数；图例项上 Enter / Space 固定或释放该类。
- **可访问性**：网格是带角色描述与操作说明的可聚焦 Group，格子为 Raw；Polite 区域播报光标处的类目、占比与值；隐藏摘要与数据表列出每类的值、占比与格数；图例项是 Toggle，每类都有文字名称。
- **动效**：
  - 格子持久：数据变化时尽量留在原位，其余按位置交错飞向新块，网格像波浪一样重新填满（P19；morph + StaggerItem）；飞行中的格子中途缩小一下并同步换色（P16）。
  - 图例占比以临界阻尼弹簧滚动（P14；smooth）。
  - 首次可见时格子从左下角依次出现（P18；snappy + StaggerItem）。
  - Reduced：格子与占比立即到位，去掉缩小、出现与交错；Off：同 Reduced。
- **Avalonia 实现要点**：默认 100 格只做变换动画，建议单控件自绘而不是 100 个子元素；悬停由指针坐标算术解析，不挂逐格监听（P17）；网格为正方形、最大 300px，容器不小于 420px 时图例在侧（S5）；图例名过长省略，全名保留在提示、实时区域与数据表中。
- **AtomUI 参考**：—（AtomUI 无同类控件）
- **风险与未决**：首次揭示的"弹出"可能还应引用 P26（主数据未列）；自绘后格子级读数需要自定义 AutomationPeer 承载键盘光标。
- 复核结论（汇总评审）：新增 P26，已同步到[组件总清单](../overview.md)。

### 4.7 SlopeChart（Slope chart）
`slug: slope-chart` · 阶段 P8 · Arc 依赖：无 · 建议基类：Control（自绘）

**Arc 行为摘要**：在两条共享轴上展示前后对比：线条描入，排名变化标在终值旁，切换数据集时每条线滑到新斜率。用于两个时期之间谁升谁降、顺序如何变化，正好两个时刻、3 到 10 项最合适。三个以上时刻用 LineChart，只看最新值用 BarChart；超过约 10 项时标签拥挤。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `data`（必填） | `{ key, label, start, end }[]` | `Data`（`IReadOnlyList<SlopeItem>`） |
| `startLabel` / `endLabel`（必填） | string，如 "Q1"、"Q2" | `StartLabel` / `EndLabel`（string） |
| `formatChange` | `(change, item) => string` | `ChangeFormatter`（`Func<double, SlopeItem, string>?`，如 "+1.5 pts"） |
| `height` / `ranks` | 每项 44px / `true` | `PlotHeight`（double?，null 时按项数计算）/ `IsRankVisible`（bool） |
| `highlightKey` | null | `HighlightKey`（string?，涂第一 series 色） |
| `activeKey` / `onActiveChange` | string 或 null | `ActiveKey`（string?，双向）+ `ActiveKeyChanged` 事件 |

- **交互与键盘**：Tab 聚焦图表并读出终值最高的一项；↓/→ 下一项，↑/← 上一项（按终值排序）；Home / End 到首末项；Escape 清除读数；触摸按住拖动读最近的线。
- **可访问性**：图表是带角色描述与说明的可聚焦 Group，线与标签为 Raw；Polite 区域播报两端值、变化与排名移动；隐藏摘要与数据表按终值排序；排名移动用箭头字形加数字，不只靠颜色。
- **动效**：
  - 首次可见时每条线带小交错从起点描到终点，端点与标签在落线时到达（P15、P18；加长的无回弹 smooth + StaggerItem）。
  - 切换数据集时线、点、标签与引导细线在共享的 morph 弹簧上移动（P16；morph），排名数字按移动方向滚动（P14；smooth）。
  - 焦点项前置、其余淡出（P27；snappy），提示框滑到该线中点（P17；snappy）。
  - Reduced：所有线直接画好，切换时立即移动；Off：立即终态。
- **Avalonia 实现要点**：重叠标签互相推开并用细线连回端点（`TextLayout` 测量加避让算法）；容器小于 440px 时标签列变窄、名称省略，全名留在提示与数据表；名称只在左侧，数值两侧都有；约十项、几十个绘制元素，一个尺寸观察（S5）。
- **AtomUI 参考**：—（AtomUI 无同类控件）
- **风险与未决**：标签避让算法应放进 chart-foundation，BrushChart 的事件标签同样需要碰撞检测。

### 4.8 Sparkline（Sparkline）
`slug: sparkline` · 阶段 P8 · Arc 依赖：无 · 建议基类：Control（自绘）

**Arc 行为摘要**：带标题数值的小折线，可用指针或键盘擦读过去的值。用于卡片、表格行或 KPI 块里的紧凑趋势。需要坐标轴比较离散时期用 BarChart，只关心最新值用 MetricCard。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `data`（必填） | `number[]`，旧到新 | `Data`（`IReadOnlyList<double>`） |
| `value` / `change` | string | `Value` / `Change`（string，静止时的标题与旁注，如 "+4.1%"） |
| `tone` | `"accent"` | `Tone`（`ChartTone`：Accent、Success、Warning、Danger，线与变化值的颜色） |
| `width` / `height` | `160` / `52`（SVG 单位） | `DrawingWidth` / `DrawingHeight`（double，绘制坐标，实际宽度随容器） |
| `labels` | `string[]`，每点一个 | `PointLabels`（`IReadOnlyList<string>?`，擦读时显示） |
| `area` / `interactive` | `true` / `true` | `IsAreaVisible` / `IsInteractive`（bool） |

- **交互与键盘**：←/→（↓/↑ 同）前后移动一点；PageDown / PageUp 约 1/6；Home / End 到首末；Escape 停止擦读并回到最新值；触摸抬起后读数回到最新值。
- **可访问性**：图以 `Label` 命名，绘图为 Raw；可交互时绘图区是可聚焦 Slider，值文本读出点标签与值；Arc 建议传入 `labels`，让擦读读出日期而不是位置。
- **动效**：
  - 首次可见时描线，终点圆点在落线时出现（P15、P18；加长的无回弹 smooth）。
  - 擦读时光标在点间弹簧移动（P17；snappy），之后的点变暗，标题文字滚动切换（P3；standard + enter，BlurSoft）。
  - Reduced：整条线直接显示，光标移动不带弹簧；Off：立即终态。
- **Avalonia 实现要点**：`IsInteractive=false` 时去掉 Tab 停靠与指针处理（密集表格场景）；Arc 每个实例各有尺寸观察与视口揭示，表格中大量实例时应共享 S5 / S6 观察者；`ValueFormatter` 应与 `Value` 的格式一致。
- **AtomUI 参考**：—（AtomUI 无同类控件）
- **风险与未决**：主数据列了 P16，但 Arc 文档没有描述数据切换形变，可能多余；"之后的点变暗"更像遮罩分段（P29，主数据未列），需对照源码确认。
- 复核结论（汇总评审）：新增 P29；移除 P16，已同步到[组件总清单](../overview.md)。
- **实现状态（P8，已落地）**：`Sparkline : ChartBase`；`Value`、`Change`、`Tone`（`ChartTone`）、`DrawingWidth=160`、`DrawingHeight=52`（只定宽高比）、`PointLabels`、`ShowArea`、`IsInteractive`；伪类 `:scrubbing`、`:success`、`:warning`、`:danger` 控制旁注颜色。首次揭示 840ms in-out 笔画（按弧长求笔尖，面积跟随笔尖），落笔后终点圆点 snappy 出现；擦读时光标 snappy 弹簧、之后的线段以 `BorderStrong` 变暗、圆点放大到 10px；新数据在 161 点并集网格上形变。标题与旁注用 `ArcContentSwap`（Roll）+ `SpringSizeDecorator`（宽度 morph 弹簧）。

### 4.9 Gauge（Gauge）
`slug: gauge` · 阶段 P8 · Arc 依赖：无 · 建议基类：Control（自绘）

**Arc 行为摘要**：270° 环形仪表，带百分比读数与可选的带标签阈值区间（如 Healthy、Filling up、Critical）。用于一个值对已知范围，例如磁盘配额或健康分。任务完成度用 Progress，套餐额度用 UsageMeter。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `value`（必填） | number，位于 min 与 max 之间 | `Value`（double） |
| `min` / `max` | `0` / `100` | `Minimum` / `Maximum`（double） |
| `detail` | string | `Detail`（string，次要说明行，如 "360 of 500 GB"） |
| `tone` | `"accent"` | `Tone`（`ChartTone`，没有命中阈值时的环色） |
| `thresholds` | `{ from, tone, label }[]` | `Thresholds`（`IReadOnlyList<GaugeThreshold>`，达到的最高区间决定色调并显示其标签） |

- **交互与键盘**：无键盘交互（只读仪表，Arc 未定义按键）。
- **可访问性**：Arc 用 meter 角色；Avalonia 没有 Meter 类型，建议用 ProgressBar 角色加只读 `IRangeValueProvider`，值文本包含当前区间标签；阈值标签用文字说明状态，不只靠颜色；环与动画读数为 Raw。
- **动效**：
  - 一半进入视口时环填充一次且不回弹（P18、P23；smooth）。
  - 之后环弹簧到新值，弧长随之变化，百分比同步计数（P23、P15、P14；smooth）。
  - 计数越过阈值的那一刻，区间颜色与标签同时切换（跟随计数，不单独过渡）。
  - Reduced：直接落在目标值；Off：同 Reduced。
- **Avalonia 实现要点**：单条描边弧自绘，没有持续循环；宽度 min(100%, 176px) 并保持宽高比；阈值判断基于动画中的显示值而不是目标值；色调映射到 Accent、Success、Warning、Danger Token。
- **AtomUI 参考**：DashboardProgress（参考）（仅供学习行为与边界，不复用其实现）
- **风险与未决**：用 ProgressBar 角色表达 meter 语义的读屏效果需验证；弧的变化统一用描边长度（P15）加弹簧填充（P23）实现，不做几何形变。
- 复核结论（汇总评审）：移除 P16，已同步到[组件总清单](../overview.md)。

### 4.10 ActivityHeatmap（Activity heatmap）
`slug: activity-heatmap` · 阶段 P8 · Arc 依赖：无 · 建议基类：Control（自绘）

**Arc 行为摘要**：贡献图式日历，每天一个着色方格，悬停或聚焦显示提示。用于长周期的日节律（贡献、锻炼），重在连续与空档，而不是精确比较。精确比较用 BarChart，选择日期用 Calendar。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `days`（必填） | `{ date: "YYYY-MM-DD", count }[]`，缺失按 0 | `Days`（`IReadOnlyList<ActivityDay>`：DateOnly、int） |
| `period`（必填） | string，补全摘要 "1,284 contributions in {period}" | `Period`（string） |
| `unit` | `{ one, other }` / contribution、contributions | `Unit`（`PluralNoun`：One、Other，按数量选单复数，与通用 `Unit` 类型不同） |
| `thresholds` | 三个上界，默认最忙一天的四分位 | `Thresholds`（`IReadOnlyList<int>?`，级别 1 到 3 的上界） |
| `weekStartsOn` / `locale` | `0` / `"en-US"` | `FirstDayOfWeek`（DayOfWeek）/ `Culture`（CultureInfo?） |
| `selectedDate` / `onSelectDate` | null | `SelectedDate`（DateOnly?，双向）+ `DateSelected` 事件（点击、Enter、Space） |
| `actions` | ReactNode | `Actions`（object，摘要旁的控件，如区间切换） |

- **交互与键盘**：↑/↓ 前后一天；←/→ 前后一周；Home / End 到首末天；Enter / Space 选中当天；Escape 隐藏提示；触摸点击后提示停留而不是立即隐藏。
- **可访问性**：Arc 用 grid 与带名称的 gridcell，并有解释级别的隐藏图例；Avalonia 用 Table 与 DataItem 角色的 AutomationPeer 子树（自绘时为虚拟节点）；图例色块是 Toggle，高亮某一级别的天；总数经 Status、悬停日经 Polite 播报，提示框为 Raw。
- **动效**：
  - 首次可见格子波浪式进入，波浪总时长封顶（P18；StaggerItem + inOut）。
  - 新区间以扫过方式重新着色（standard 补间 + 按列交错，无对应原语，见风险）。
  - 提示框在格间滑动（P17；snappy）、文字滚动（P3；standard + enter，BlurSoft），总数计数到新值（P14；smooth）。
  - Reduced：去掉波浪、扫过与滑动，改为短淡入淡出（fast 160ms）；Off：立即终态。
- **Avalonia 实现要点**：一年约 370 格，建议单控件自绘并按坐标命中；格子随容器在 9 到 15px 之间缩放，再小则从最新一周起横向滚动；容器小于 440px 时摘要换成短格式；全图共享一个提示框；日期与复数格式化走 S11。
- **AtomUI 参考**：—（AtomUI 无同类控件）
- **风险与未决**：自绘后约 370 个格项的键盘与读屏需要虚拟 AutomationPeer，工作量不小；"扫过重着色"没有对应原语，建议在 chart-foundation 里定义交错调度。

### 4.11 AnimatedCounter（Animated counter）
`slug: animated-counter` · 阶段 P1 · Arc 依赖：无 · 建议基类：Control（RollingDigits）

**Arc 行为摘要**：里程表式数字，每个数位列滚动到新值。用于独立显示且会变化的数字（总数、价格、实时计数），以及滚动进入视口时从 0 滚起的首屏统计。数字需要卡片外框时用 MetricCard，文字而不是数字用 TextMorph。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `value`（必填） | number，传原始数值而不是格式化字符串 | `Value`（double） |
| `label` | string，数字上方的小标签 | `Label`（string?） |
| `prefix` / `suffix` | `""` / `""` | `Prefix` / `Suffix`（string，如 "$"、"%"） |
| `decimals` | `0` | `Decimals`（int，固定小数位） |
| `animateOnView` | `false` | `IsAnimatedOnView`（bool，首次进入视口时所有数位从 0 滚起） |
| `locale` | `"en-US"`（固定，保证服务端与客户端一致） | `Culture`（CultureInfo?，默认值策略见风险） |

- **交互与键盘**：无交互，不可聚焦。
- **可访问性**：完整格式化文本作为名称（Text 角色），滚动数位为 Raw；不播报变化，需要播报时由外层设置 `LiveSetting=Polite`。
- **动效**：
  - 每个数位列按整个数字变化的方向转动，9 与 0 之间回绕（P14；smooth）。
  - 位数变化时数位列与分隔符滑入或滑出，宽度弹簧适配，相邻内容随之轻移（P5、P2；morph）。
  - `IsAnimatedOnView` 时等到 60% 可见才开始，数位带轻微交错（P18；StaggerItem + considered）。
  - Reduced：数位不滚动，直接换成新值（Arc 为直接到位，类别策略允许交叉淡化）；Off：立即。
- **Avalonia 实现要点**：由 Core 的 `RollingDigits`（P14）构成，用等宽数字（`FontFeatures` 启用 tnum，字体不支持时用固定宽度的数字单元格）；格式化走 S11，可见性走 S6；每个数位都是独立动画列，适合少量计数器，不适合表格的每个单元格。
- **AtomUI 参考**：StatisticCountUp（仅供学习行为与边界，不复用其实现）
- **风险与未决**：阶段 P1 就要落地 RollingDigits、S6 与 S11，并且是 BillingToggle、MetricCard（阶段 P2）的硬依赖；`Culture` 默认固定为 en-US（对齐 Arc）还是跟随 `CultureInfo.CurrentUICulture` 待定。

### 4.12 Ridgeline（Ridgeline）
`slug: ridgeline` · 阶段 P8 · Arc 依赖：无 · 建议基类：Control（自绘）

**Arc 行为摘要**：山脊图（joy plot）：每组一条平滑分布曲线，像山脊一样重叠，按中位数着色；悬停抬起一条以读取四分位，切换数据集时每条曲线形变。用于比较多组分布的形状（按月气温、按地区的延迟），关注离散程度、偏态与双峰。每组只需要一个汇总数时用 BarChart。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `series`（必填） | `{ id, label, values: number[] }[]`，原始观测，自上而下 | `Series`（`IReadOnlyList<RidgelineSeries>`，每组建议至少 20 个观测） |
| `domain` | `[min, max]`，默认数据范围加余量 | `Domain`（`ChartRange?`，固定后切换数据集时轴不动） |
| `overlap` / `rowHeight` | `2.4`（行高倍数）/ `30` | `Overlap` / `RowHeight`（double） |
| `bandwidth` | 默认每组按 Silverman 规则 | `Bandwidth`（double?，以值为单位的平滑程度） |
| `tint` | `true` | `IsTinted`（bool，按中位数从浅色到第一 series 色分级，轴下显示色阶） |
| `active` / `defaultActive` / `onActiveChange` | string 或 null / null | `ActiveId`（string?，双向）+ `ActiveIdChanged` 事件 |

- **交互与键盘**：↑/↓ 抬起上一条或下一条山脊；←/→ 按轴步长移动读数光标；Home / End 抬起首末山脊；Escape 放下山脊并清除读数；触摸点击即抬起并在点击处读数。
- **可访问性**：绘图区是可聚焦 Group，Polite 区域播报被抬起山脊的中位数、中间一半、范围与数量；隐藏数据表列出每组的中位数、四分位、极值与数量；每行有文字标签，中位数着色有表格与提示兜底。
- **动效**：
  - 曲线在共享网格上采样，新数据集或新值域时插值每个点，山脊鼓起、滑动与分裂在同一个弹簧里完成（P16；smooth，visualDuration 0.7s）。
  - 首次揭示从顶行开始逐行滚入（P18；StaggerItem + inOut）。
  - 被抬起的山脊是画在上层的副本，弹簧上升 6px，中间一半加阴影并标出中位数（P27；snappy）；提示框跟随指针并限制在图内（P17；snappy）。
  - Reduced：直接画出终态形状；Off：同 Reduced。
- **Avalonia 实现要点**：每个数据集计算一次核密度（每条 96 个采样），动画只插值数组并直接写路径；标签槽宽与刻度数随宽度调整；每组可承载数千个观测；核密度与 Silverman 带宽属于统计工具，与 Streamgraph 的堆叠一起放进 chart-foundation。
- **AtomUI 参考**：—（AtomUI 无同类控件）
- **风险与未决**：数千观测乘以行数的核密度在 UI 线程计算可能卡顿，考虑后台计算后回到 UI 线程；行数建议不超过 20。

### 4.13 Treemap（Treemap）
`slug: treemap` · 阶段 P8 · Arc 依赖：无 · 建议基类：Control（自绘）

**Arc 行为摘要**：方块化（squarified）矩形树图：点击分支下钻，所选块放大填满视图、子块在其中展开，面包屑可逐级返回；切换尺寸或着色度量时每块形变。用于叶子很多的层级占比（按地区、国家、套餐的收入），同时显示第二个度量。相近值的精确比较用 BarChart。

**Arc API 要点**：
| Arc 属性 | 类型 / 默认值 | 建议 Avalonia 属性（类型） |
|---|---|---|
| `data`（必填） | `{ id, label, value?, color?, children? }`，分支值为子节点之和 | `Data`（`TreemapNode`） |
| `colorLabel` / `formatColor` | string / `(value) => string` | `ColorLabel`（string，块取顶层分支的色相并随度量加深，不设时只显示一层）/ `ColorFormatter`（`Func<double, string>?`） |
| `colorDomain` | 默认取叶子的范围 | `ColorDomain`（`ChartRange?`） |
| `focus` / `defaultFocus` / `onFocusChange` | 根节点 | `FocusedNodeId`（string?，双向，避开 `Focus()` 重名）+ `FocusedNodeChanged` 事件 |
| `height` | `420` | `PlotHeight`（double） |

- **交互与键盘**：方向键移到该方向最近的顶层块；Home / End 到首末块；Enter / Space 放大焦点块；Escape / Backspace 缩小一级；触摸点击块读数、点击分支下钻。
- **可访问性**：块组成带按键说明的可聚焦 Group；面包屑是导航区域（`LandmarkType=Navigation`），当前层带当前项标记；Polite 区域播报焦点块的路径、值、占父级份额与着色度量；隐藏数据表列出每个节点。
- **动效**：
  - 下钻时所有块经历同一个仿射缩放：所选块放大填满视图、兄弟块向外飞出、子块在其中展开；返回时沿原路反向（P16；smooth，visualDuration 0.62s）。
  - 切换度量时同一批 id 重新布局，块原位改变大小与颜色（P16、P19；smooth 0.62s），总数滚动（P14；smooth），面包屑滑动（P19；morph）。
  - 文字不随缩放变形：每帧按块尺寸排版，放得下才显示标签；悬停提示跟随（P17；snappy）。
  - Reduced：块直接到位；Off：同 Reduced。
- **Avalonia 实现要点**：每个焦点计算一次方块化布局，动画插值矩形并直接写变换与尺寸；标题与标签用 `TextLayout` 测量，放不下就隐藏；舒适规模为数百个节点；面包屑可参考 Navigation 大类的 Breadcrumb。
- **AtomUI 参考**：—（AtomUI 无同类控件）
- **风险与未决**：主数据没有 Arc 依赖，面包屑自绘还是复用 Breadcrumb（阶段 P4）待定；方块化布局算法放进 chart-foundation 还是 Treemap 内部待定。

## 5. 实现顺序与依赖
- 阶段 P1：AnimatedCounter 先行。Toggles 的 BillingToggle 与 Cards 的 MetricCard（阶段 P2）硬依赖它；随它落地 Core 的 RollingDigits（P14，与 Calendar 同阶段）、首次揭示 P18（首个使用者）、S6 视口可见性与 S11 数字格式化。
- 阶段 P8 之前：P8 用到的原语除 P17 外都已在更早阶段落地（P2、P5、P21、P29 随 Button，P3、P14 随 Calendar，P15 随 CopyButton，P16 随 IconMorph，P19 随 DatePicker，P23 随 Progress，P27 随 AvatarGroup，P12 随阶段 P5 的 Drawer、BottomSheet、UserMenu）；共享层随图表逐步构建，见 [chart-foundation.md](./chart-foundation.md) 的落地顺序。
- 阶段 P8 顺序：前四步与 chart-foundation 一致，Sparkline（线性比例尺、路径、描边、P17 最小版本）→ Gauge（弧形几何）→ BarChart（带状比例尺、坐标轴、提示框）→ LineChart（时间比例尺、多系列、图例、十字线、可访问数据表）；其余建议按 BrushChart（拖拽窗口与按像素抽样）→ Streamgraph、Ridgeline（共享网格、堆叠与核密度）→ DonutChart（角度几何与命中）→ SlopeChart（标签避让）→ WaffleChart、ActivityHeatmap（单元格网格与逐格读数）→ Treemap（层级布局与仿射缩放）。
- 跨大类：图表里的数字滚动复用 AnimatedCounter 的 RollingDigits；ActivityHeatmap 的键盘网格可参考 Calendar（阶段 P1）；Treemap 的面包屑可参考 Breadcrumb（阶段 P4）。

## 6. 相关文档
- [图表基础](./chart-foundation.md)
- [组件总清单](../overview.md) · [规格模板](../spec-template.md)
- [动效原语目录](../../architecture/motion/primitives-catalog.md) · [Token 目录](../../architecture/theming/token-catalog.md)
- [浮层与输入](../../architecture/overlays-and-input.md) · [路线图](../../roadmap/overview.md)
- 相邻大类：[进度](../progress/overview.md) · [卡片](../cards/overview.md) · [表格与数据](../tables/overview.md)
